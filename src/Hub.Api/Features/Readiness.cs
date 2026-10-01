using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ReadinessEndpoints
{
    public sealed record CreateBody(Guid RequestId, int TargetRowVersion, string IntendedOutput, string CompletionCriteria);
    public sealed record ApplicabilityBody(Guid RequestId, int AssessmentRowVersion, int CheckRowVersion,
        bool Applies, string Reason, string? EvidenceUrl);
    public sealed record ConstraintBody(Guid RequestId, int TargetRowVersion, string Category, string Description,
        Guid RemovalOwnerId, DateOnly NeededBy, string SourceUrl, string? LinkedType = null, Guid? LinkedId = null);
    public sealed record ConstraintMoveBody(Guid RequestId, int RowVersion, string ToState, string Reason,
        string? EvidenceUrl);
    public sealed record ExceptionBody(Guid RequestId, int AssessmentRowVersion, Guid BasisVersionId,
        int BasisVersionRowVersion, Guid VerifierId, string LimitedWork, string Risk, DateOnly ExpiresOn);
    public sealed record PrerequisiteBody(Guid RequestId, int TargetRowVersion, Guid PackageId, string Reason);
    public sealed record PrerequisiteRemoveBody(Guid RequestId, int RowVersion, string Reason);
    static readonly string[] ConstraintCategories = ["Handoff", "Decision", "Basis", "Capacity", "Review", "Scope", "Other"];
    static readonly string[] LinkTypes = [ItemType.Decision, ItemType.Issue, ItemType.Handoff];
    public sealed record LinkedRecord(string Type, Guid Id, string Key, string Title, string Status);

    // FR-RDY-03: a constraint points at the same-project decision, issue or handoff that already represents it instead of
    // duplicating it. Deleted records drop out through the soft-deletion query filters, so a stale link reads as unavailable.
    static IQueryable<LinkedRecord> Linkable(HubDb db, Guid projectId, string type, Guid? id = null) => type switch
    {
        ItemType.Decision => db.Decisions.AsNoTracking().Where(d => d.ProjectId == projectId && (id == null || d.Id == id))
            .OrderByDescending(d => d.Seq).Select(d => new LinkedRecord(ItemType.Decision, d.Id, d.Key, d.Subject, d.Status)),
        ItemType.Issue => db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId && (id == null || i.Id == id))
            .OrderByDescending(i => i.Seq).Select(i => new LinkedRecord(ItemType.Issue, i.Id, i.Key, i.Title, i.Status)),
        _ => db.Handoffs.AsNoTracking().Where(h => h.ProjectId == projectId && (id == null || h.Id == id))
            .OrderByDescending(h => h.Seq).Select(h => new LinkedRecord(ItemType.Handoff, h.Id, h.Key, h.Title, h.Status)),
    };

    // Source-backed checks are recomputed at read/command time. Manual applicability remains useful for
    // checks without a canonical source, but it cannot keep a linked source in a stale Ready state.
    public static async Task<ReadinessResult> EvaluateCurrent(HubDb db, Project project, string targetType,
        Guid targetId, ReadinessAssessment assessment, IReadOnlyList<ReadinessCheckRecord> records, DateOnly today, DateTimeOffset now,
        SettingsStore? settings = null)
    {
        var target = await Coordination.Target(db, project, targetType, targetId, false);
        var checks = records.ToDictionary(x => x.Code, StringComparer.Ordinal);
        void Source(string code, bool applies, bool? satisfied, string reason)
        {
            if (!checks.TryGetValue(code, out var row)) return;
            row.Applies = applies; row.Satisfied = satisfied; row.Reason = reason;
        }

        var handoffs = await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == project.Id &&
            (targetType == "Task" ? h.TargetTaskId == targetId : h.TargetDeliverableId == targetId) &&
            h.Status != HandoffStatus.Cancelled).Select(h => h.Status).ToListAsync();
        if (handoffs.Count > 0)
            Source(ReadinessCheckCode.Handoff, true, handoffs.All(s => s is HandoffStatus.Accepted or HandoffStatus.Incorporated),
                "Linked handoffs are current source evidence.");

        if (targetType == "Task")
        {
            var predecessorIds = await db.Dependencies.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                d.SuccessorTaskId == targetId && d.DeletedAt == null).Select(d => d.PredecessorTaskId).Distinct().ToListAsync();
            if (predecessorIds.Count > 0)
            {
                var predecessors = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id &&
                    predecessorIds.Contains(t.Id)).Select(t => t.Status).ToListAsync();
                Source(ReadinessCheckCode.Predecessor, true, predecessors.Count == predecessorIds.Count &&
                    predecessors.All(s => s == TaskStatuses.Complete), "Linked predecessor tasks are current source evidence.");
            }
        }
        else
        {
            var predecessorIds = await db.DeliverableDependencies.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                d.SuccessorDeliverableId == targetId && d.DeletedAt == null).Select(d => d.PredecessorDeliverableId).Distinct().ToListAsync();
            if (predecessorIds.Count > 0)
            {
                var predecessors = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                    predecessorIds.Contains(d.Id)).Select(d => d.Status).ToListAsync();
                Source(ReadinessCheckCode.Predecessor, true, predecessors.Count == predecessorIds.Count &&
                    predecessors.All(s => s is DeliverableStatus.Issued or DeliverableStatus.Accepted),
                    "Linked predecessor deliverables are current source evidence.");
            }
        }

        var decisionIds = await db.ItemLinks.AsNoTracking().Where(l => l.ProjectId == project.Id && l.DeletedAt == null && l.TargetType == targetType &&
            l.TargetId == targetId && l.SourceType == ItemType.Decision && l.Relation == ItemRelation.BlockedByDecision)
            .Select(l => l.SourceId).Distinct().ToListAsync();
        if (decisionIds.Count > 0)
        {
            var statuses = await db.Decisions.AsNoTracking().Where(d => decisionIds.Contains(d.Id)).Select(d => d.Status).ToListAsync();
            Source(ReadinessCheckCode.Decision, true, statuses.Count == decisionIds.Count && statuses.All(s => s == DecisionStatus.Decided),
                "Linked decisions are current source evidence.");
        }

        Guid? exceptionBasisVersionId = null;
        var allUses = await db.BasisUses.AsNoTracking().Where(u => u.ProjectId == project.Id && u.TargetType == targetType && u.TargetId == targetId)
            .Join(db.DesignBasisVersions.AsNoTracking(), u => u.VersionId, v => v.Id,
                (u, v) => new { u.Id, u.VersionId, u.CreatedAt, v.EntryId }).ToListAsync();
        var currentUses = allUses.GroupBy(u => u.EntryId)
            .Select(g => g.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).First()).ToList();
        if (currentUses.Count > 0)
        {
            var useIds = currentUses.Select(u => u.Id).ToArray();
            var versionIds = currentUses.Select(u => u.VersionId).ToArray();
            var versions = await db.DesignBasisVersions.AsNoTracking().Where(v => versionIds.Contains(v.Id)).ToListAsync();
            var conflicts = await db.BasisConflicts.AsNoTracking().Where(c => c.ProjectId == project.Id &&
                (versionIds.Contains(c.LeftVersionId) || versionIds.Contains(c.RightVersionId)))
                .AnyAsync(c => !c.Resolved);
            var pendingImpact = await db.BasisImpactAssessments.AsNoTracking().AnyAsync(a => a.ProjectId == project.Id &&
                useIds.Contains(a.BasisUseId) && a.Status == AssessmentStatus.Pending);
            var proposed = versions.Where(v => v.Status == BasisStatus.Proposed).ToList();
            if (versions.Count == currentUses.Count && proposed.Count == 1 && !conflicts && !pendingImpact &&
                versions.All(v => v.Status == BasisStatus.Confirmed || v.Id == proposed[0].Id))
            {
                var candidate = proposed[0];
                if (await db.DesignBasisEntries.AsNoTracking().AnyAsync(e => e.ProjectId == project.Id &&
                    e.Id == candidate.EntryId && e.Kind == BasisKind.Assumption))
                    exceptionBasisVersionId = candidate.Id;
            }
            Source(ReadinessCheckCode.Basis, true, versions.Count == currentUses.Count && !conflicts && !pendingImpact &&
                versions.All(v => v.Status == BasisStatus.Confirmed),
                "Linked basis uses and conflicts are current source evidence.");
        }

        Source(ReadinessCheckCode.ProductionOwner, true,
            target.OwnerId != Guid.Empty && await Coordination.People(db, project).AnyAsync(u => u.Id == target.OwnerId),
            "The linked production owner is current source evidence.");

        // A required review package is a live source gate. The stored readiness check cannot
        // preserve Ready after the package or its current round moves back to review.
        var reviewRequirement = targetType == "Deliverable"
            ? await db.Deliverables.AsNoTracking().Where(d => d.Id == targetId && d.ProjectId == project.Id)
                .Select(d => new { d.RequiresReview, d.RequiredReviewPackageId }).SingleAsync()
            : null;
        var requiredReviewPackageId = reviewRequirement?.RequiredReviewPackageId;
        if (requiredReviewPackageId is { } packageId)
        {
            var review = await db.ReviewPackages.AsNoTracking().Where(p => p.Id == packageId && p.ProjectId == project.Id)
                .Select(p => new { p.Status, p.CurrentRoundId }).SingleOrDefaultAsync();
            var round = review?.CurrentRoundId is { } roundId
                ? await db.ReviewRounds.AsNoTracking().Where(r => r.Id == roundId && r.ProjectId == project.Id && r.PackageId == packageId)
                    .Select(r => r.Status).SingleOrDefaultAsync()
                : null;
            var containsTarget = review?.CurrentRoundId is { } currentRoundId &&
                await db.ReviewManifestItems.AsNoTracking().AnyAsync(m => m.ProjectId == project.Id &&
                    m.RoundId == currentRoundId && m.DeliverableId == targetId);
            Source(ReadinessCheckCode.ReviewGate, true,
                review?.Status == ReviewStatus.Approved && round == ReviewStatus.Approved && containsTarget,
                "The required review package and current round are current source evidence.");
        }
        else if (reviewRequirement?.RequiresReview == true)
            Source(ReadinessCheckCode.ReviewGate, true, false,
                "A required review package has not been linked in this project scope.");
        // Without a canonical package requirement, retain the PM/lead applicability decision.

        // Jay's Submission Gate (2026-10-01): every linked prerequisite package must be Issued. With no link, a reasoned
        // Not Applicable stands and anything else stays unknown; satisfaction is never taken from a stored record.
        var gate = await SubmissionGate(db, project.Id, targetType, targetId);
        if (gate is not null) Source(ReadinessCheckCode.SubmissionGate, true, gate.Value.Satisfied, gate.Value.Reason);
        else if (checks.TryGetValue(ReadinessCheckCode.SubmissionGate, out var gateRecord) && gateRecord.Applies == true)
            Source(ReadinessCheckCode.SubmissionGate, true, null, "No prerequisite submission package is linked.");

        var capacity = await ProductionCapacity(db, project, targetType, targetId, today, now, settings);
        if (!(capacity.Satisfied is null && checks.TryGetValue(ReadinessCheckCode.ProductionCapacity, out var capacityRecord) && capacityRecord.Applies == false))
            Source(ReadinessCheckCode.ProductionCapacity, capacity.Applies, capacity.Satisfied, capacity.Reason);

        var reviewCapacity = await ReviewCapacity(db, project, targetType, targetId, today, settings);
        var hasCanonicalReviewRequirement = reviewRequirement?.RequiresReview == true && reviewRequirement.RequiredReviewPackageId is not null;
        if (hasCanonicalReviewRequirement)
            Source(ReadinessCheckCode.ReviewCapacity, reviewCapacity.Applies, reviewCapacity.Satisfied, reviewCapacity.Reason);

        var activeConstraints = await db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == project.Id &&
            c.TargetType == targetType && c.TargetId == targetId &&
            c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled).Select(c => c.Category).ToListAsync();
        foreach (var category in activeConstraints.Distinct())
        {
            var code = category switch
            {
                "Handoff" => ReadinessCheckCode.Handoff,
                "Decision" => ReadinessCheckCode.Decision,
                "Basis" => ReadinessCheckCode.Basis,
                "Capacity" => ReadinessCheckCode.ProductionCapacity,
                "Review" => ReadinessCheckCode.ReviewGate,
                _ => null,
            };
            if (code is not null) Source(code, true, false, "An active linked constraint blocks this check.");
        }

        var latestException = await db.ReadinessExceptions.AsNoTracking().Where(e => e.ProjectId == project.Id &&
            e.AssessmentId == assessment.Id).OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).FirstOrDefaultAsync();
        ReadinessPermission? permission = null;
        if (latestException is not null)
            permission = new ReadinessPermission(true, latestException.ExpiresOn,
                exceptionBasisVersionId == latestException.BasisVersionId && activeConstraints.Count == 0,
                latestException.VerifierId != target.OwnerId && await Coordination.People(db, project)
                    .AnyAsync(u => u.Id == latestException.VerifierId), latestException.LimitedWork, latestException.Risk);
        var result = ReadinessRules.Evaluate(checks.Values.Select(c => new ReadinessCheck(c.Code, c.Applies, c.Satisfied)), permission, today);
        var openConstraint = await db.WorkConstraints.AsNoTracking().AnyAsync(c => c.ProjectId == project.Id &&
            c.TargetType == targetType && c.TargetId == targetId &&
            (c.State == ConstraintState.Open || c.State == ConstraintState.ResolutionProposed));
        if (openConstraint) result = new ReadinessResult(ReadinessState.NotReady, result.Unknown,
            [.. result.Blocked, "Constraint"]);
        assessment.State = result.State; assessment.EvaluatedAt = now;
        return result;
    }

    /// Evaluates the assigned production owner's capacity over the target's authoritative due-date window.
    /// Missing owner, dates, or estimates remain unknown. Confirmed reservations use their dated spread and
    /// allocation day overrides; availability overrides replace the normal daily capacity.
    public sealed record CapacityEvaluation(bool Applies, bool? Satisfied, string Reason);

    /// Evaluates dated confirmed review allocations for every active assignment in a required package.
    /// Review effort is never inferred from package or assignment status. Missing, stale, or ambiguous
    /// links remain unknown so a readiness result cannot become Ready from incomplete review evidence.
    public static async Task<CapacityEvaluation> ReviewCapacity(HubDb db, Project project, string targetType,
        Guid targetId, DateOnly today, SettingsStore? suppliedSettings = null)
    {
        if (targetType != "Deliverable")
            return new(false, null, "Review capacity does not apply without a required review package.");

        var requirement = await db.Deliverables.AsNoTracking().Where(d => d.Id == targetId && d.ProjectId == project.Id && d.DeletedAt == null)
            .Select(d => new { d.RequiresReview, d.RequiredReviewPackageId }).SingleOrDefaultAsync();
        if (requirement is null || !requirement.RequiresReview)
            return new(false, null, "Review capacity does not apply without a required review package.");
        if (requirement.RequiredReviewPackageId is null)
            return new(true, null, "Review capacity is unknown until the required review package is linked.");

        var settings = suppliedSettings is null
            ? OrgSettings.From((await db.Settings.AsNoTracking().ToListAsync()).ToDictionary(r => r.Key,
                r => System.Text.Json.JsonDocument.Parse(r.Value).RootElement.Clone()))
            : await suppliedSettings.Get(db);
        var package = await db.ReviewPackages.AsNoTracking().Where(p => p.Id == requirement.RequiredReviewPackageId && p.ProjectId == project.Id)
            .Select(p => new { p.Id, p.CurrentRoundId, p.Status }).SingleOrDefaultAsync();
        if (package?.CurrentRoundId is not { } roundId)
            return new(true, null, "Review capacity is unknown until the required package has a current round.");
        if (!await db.ReviewRounds.AsNoTracking().AnyAsync(r => r.Id == roundId && r.ProjectId == project.Id && r.PackageId == package.Id))
            return new(true, null, "Review capacity is unknown until the required package points to a current round in this project.");

        var activeStatuses = new[] { DisciplineReviewStatus.Pending, DisciplineReviewStatus.InReview, DisciplineReviewStatus.ChangesRequired };
        var assignments = await db.DisciplineReviews.AsNoTracking().Where(a => a.ProjectId == project.Id && a.RoundId == roundId &&
            activeStatuses.Contains(a.Status)).ToListAsync();
        if (assignments.Count == 0)
            return new(false, null, "Review capacity does not apply because the current round has no active review assignments.");
        if (assignments.Any(a => a.ReviewerId == Guid.Empty || a.DueDate < today))
            return new(true, null, "Review capacity is unknown until every active assignment has a current reviewer and date.");

        var reviewers = assignments.Select(a => a.ReviewerId).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(u => reviewers.Contains(u.Id) && u.IsActive)
            .Select(u => new { u.Id, u.OfficeId, u.WeeklyCapacityHours }).ToDictionaryAsync(u => u.Id);
        var currentMembers = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == project.Id && reviewers.Contains(m.UserId))
            .Select(m => m.UserId).ToListAsync();
        if (people.Count != reviewers.Length || reviewers.Any(id => !people.ContainsKey(id) || !currentMembers.Contains(id)))
            return new(true, null, "Review capacity is unknown until every active review assignment has a current project reviewer.");

        var assignmentIds = assignments.Select(a => a.Id).ToHashSet();
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(l => l.WorkType == "Review" && l.ReleasedAt == null &&
            assignmentIds.Contains(l.WorkId)).ToListAsync();
        if (links.Count == 0 || links.Any(l => l.ReviewHours is not > 0 || l.WorkDate < today))
            return new(true, null, "Review capacity is unknown until every active review assignment has a dated allocation link.");
        if (links.GroupBy(l => (l.WorkId, l.WorkDate)).Any(g => g.Count() != 1))
            return new(true, null, "Review capacity is unknown while review allocation links are duplicated for a date.");

        var assignmentById = assignments.ToDictionary(a => a.Id);
        if (links.Any(l => !assignmentById.TryGetValue(l.WorkId, out var assignment) ||
            l.PersonId != assignment!.ReviewerId || l.WorkDate > assignment.DueDate))
            return new(true, null, "Review capacity is unknown until review allocation links match the active reviewer dates.");
        if (assignments.Any(a => !links.Any(l => l.WorkId == a.Id)))
            return new(true, null, "Review capacity is unknown until every active review assignment has a dated allocation link.");

        var allocationIds = links.Select(l => l.AllocationId).Distinct().ToArray();
        var allocations = await db.Allocations.AsNoTracking().Where(a => allocationIds.Contains(a.Id)).ToListAsync();
        if (allocations.Count != allocationIds.Length || allocations.Any(a => a.Status != AllocationStatus.Confirmed ||
            a.Purpose != AllocationPurpose.Review || a.ProjectId != project.Id || a.FromDate > a.ThroughDate))
            return new(true, null, "Review capacity is unknown until all review links point to confirmed in-scope review allocations.");
        var allocationById = allocations.ToDictionary(a => a.Id);
        if (links.Any(l => !allocationById.TryGetValue(l.AllocationId, out var allocation) ||
            allocation!.PersonId != l.PersonId || l.WorkDate < allocation.FromDate || l.WorkDate > allocation.ThroughDate))
            return new(true, null, "Review capacity is unknown until review links match the confirmed reviewer and allocation dates.");

        var maxDue = assignments.Max(a => a.DueDate);
        if (maxDue > today.AddDays(366))
            return new(true, null, "Review capacity needs a bounded assignment date window.");
        var allReviewerAllocations = await db.Allocations.AsNoTracking().Where(a => reviewers.Contains(a.PersonId) &&
            a.Status == AllocationStatus.Confirmed && a.FromDate <= maxDue && a.ThroughDate >= today).ToListAsync();
        if (allReviewerAllocations.Any(a => a.ProjectId != project.Id))
            return new(true, null, "Review capacity is unknown while confirmed reviewer reservations are outside this readiness scope.");
        var nullableReviewers = reviewers.Select(id => (Guid?)id).ToArray();
        var activeTasks = await db.Tasks.AsNoTracking().AnyAsync(t => nullableReviewers.Contains(t.AssigneeId) && t.DeletedAt == null &&
            t.ProgressPct < 100 && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold);
        if (activeTasks)
            return new(true, null, "Review capacity is unknown while reviewer production workload is not isolated.");

        var holidaysByOffice = new Dictionary<Guid, IReadOnlyList<DateOnly>>();
        var reservations = new Dictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>();
        foreach (var allocation in allReviewerAllocations)
        {
            if (!people.TryGetValue(allocation.PersonId, out var person))
                return new(true, null, "Review capacity is unknown until every reviewer remains active.");
            var officeKey = person.OfficeId ?? Guid.Empty;
            if (!holidaysByOffice.TryGetValue(officeKey, out var holidays))
            {
                holidays = settings.WorkingDaysEnabled
                    ? await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == person.OfficeId).Select(h => h.Date).ToListAsync()
                    : [];
                holidaysByOffice[officeKey] = holidays;
            }
            var calendar = new WorkCalendar(holidays);
            var overrides = await db.AllocationDayOverrides.AsNoTracking().Where(o => o.AllocationId == allocation.Id)
                .ToDictionaryAsync(o => o.WorkDate, o => o.Hours);
            try { reservations[allocation.Id] = AllocationRules.Spread(allocation.FromDate, allocation.ThroughDate, allocation.PlannedHours, calendar, overrides); }
            catch (ArgumentException) { return new(true, null, "Review capacity needs reassessment after a calendar or allocation change."); }
        }

        var capacityOverrides = await db.AvailabilityOverrides.AsNoTracking().Where(o => reviewers.Contains(o.PersonId) &&
            o.WorkDate >= today && o.WorkDate <= maxDue).ToListAsync();
        var linkDemand = links.GroupBy(l => (l.AllocationId, l.WorkDate)).ToDictionary(g => g.Key, g => g.Sum(l => l.ReviewHours!.Value));
        foreach (var reviewer in reviewers)
        {
            var person = people[reviewer];
            var holidays = holidaysByOffice[person.OfficeId ?? Guid.Empty];
            var calendar = new WorkCalendar(holidays);
            var overrides = capacityOverrides.Where(o => o.PersonId == reviewer).ToDictionary(o => o.WorkDate, o => o.AvailableHours);
            for (var day = today; day <= maxDue; day = day.AddDays(1))
            {
                var available = AllocationRules.DailyCapacity(day, person.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours, calendar,
                    overrides.TryGetValue(day, out var exact) ? exact : null);
                var committed = allReviewerAllocations.Where(a => a.PersonId == reviewer).Sum(a =>
                    Math.Max(reservations[a.Id].GetValueOrDefault(day), linkDemand.GetValueOrDefault((a.Id, day))));
                if (committed > available)
                    return new(true, false, "Confirmed review allocations exceed a reviewer's dated capacity.");
            }
        }
        return new(true, true, "Confirmed review allocations fit within each reviewer's dated capacity.");
    }

    public static async Task<CapacityEvaluation> ProductionCapacity(HubDb db, Project project, string targetType,
        Guid targetId, DateOnly today, DateTimeOffset now) => await ProductionCapacity(db, project, targetType, targetId, today, now, null);

    static async Task<CapacityEvaluation> ProductionCapacity(HubDb db, Project project, string targetType,
        Guid targetId, DateOnly today, DateTimeOffset now, SettingsStore? suppliedSettings)
    {
        // The overload is intended for direct callers; Detail supplies the configured store below through
        // the current evaluator's normal settings path. Keep this source check deterministic in tests.
        var orgSettings = suppliedSettings is null
            ? OrgSettings.From((await db.Settings.AsNoTracking().ToListAsync()).ToDictionary(r => r.Key,
                r => System.Text.Json.JsonDocument.Parse(r.Value).RootElement.Clone()))
            : await suppliedSettings.Get(db);
        var defaultWeekly = orgSettings.DefaultWeeklyCapacityHours;
        var ownerAndWindow = targetType == "Task"
            ? await db.Tasks.AsNoTracking().Where(t => t.Id == targetId && t.ProjectId == project.Id && t.DeletedAt == null)
                .Select(t => new { OwnerId = t.AssigneeId, t.StartDate, t.DueDate, t.EstimatedHours, t.ProgressPct })
                .SingleOrDefaultAsync()
            : await db.Deliverables.AsNoTracking().Where(d => d.Id == targetId && d.ProjectId == project.Id && d.DeletedAt == null)
                .Select(d => new { OwnerId = d.OwnerId, d.StartDate, d.DueDate, EstimatedHours = (decimal?)null, ProgressPct = 0 })
                .SingleOrDefaultAsync();
        if (ownerAndWindow is null || ownerAndWindow.OwnerId is not { } owner || owner == Guid.Empty ||
            ownerAndWindow.DueDate is not { } due || due < today)
            return new(true, null, "Production capacity needs an active owner and a current target date.");

        var person = await db.Users.AsNoTracking().Where(u => u.Id == owner && u.IsActive)
            .Select(u => new { u.Id, u.OfficeId, u.WeeklyCapacityHours }).SingleOrDefaultAsync();
        if (person is null || !await Coordination.People(db, project).AnyAsync(u => u.Id == owner))
            return new(true, null, "Production capacity needs a current project owner.");

        var sourceTasks = targetType == "Task"
            ? await db.Tasks.AsNoTracking().Where(t => t.Id == targetId && t.ProjectId == project.Id && t.EstimatedHours != null)
                .Select(t => new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate)).ToListAsync()
            : await db.Tasks.AsNoTracking().Where(t => t.DeliverableId == targetId && t.AssigneeId == owner &&
                    t.DeletedAt == null && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold)
                .Select(t => new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate)).ToListAsync();
        if (sourceTasks.Count == 0 && targetType == "Deliverable")
            return new(false, null, "Production capacity does not apply because this deliverable has no open production tasks.");
        if (sourceTasks.Count == 0 || sourceTasks.Any(t => t.EstimatedHours is null))
            return new(true, null, "Production capacity is unknown until the required production estimate is recorded.");

        var through = due;
        if (through > today.AddDays(366))
            return new(true, null, "Production capacity needs a bounded target date window.");
        if (sourceTasks.Any(t => t.DueDate is null || t.DueDate > through || t.StartDate > through))
            return new(true, null, "Production capacity is unknown until all required production work is dated within the target window.");
        var sourceTaskIds = sourceTasks.Select(t => t.TaskId).ToHashSet();
        var hasOtherDemand = await db.Tasks.AsNoTracking().AnyAsync(t => t.AssigneeId == owner && t.DeletedAt == null &&
            !sourceTaskIds.Contains(t.Id) && t.ProgressPct < 100 &&
            t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold &&
            (t.DueDate == null || t.StartDate == null || t.StartDate <= through));
        if (hasOtherDemand)
            return new(true, null, "Production capacity is unknown until the owner's other active workload is in scope.");

        var holidays = orgSettings.WorkingDaysEnabled
            ? await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == person.OfficeId)
                .Select(h => h.Date).ToListAsync()
            : [];
        var calendar = new WorkCalendar(holidays);
        var overrides = await db.AvailabilityOverrides.AsNoTracking().Where(o => o.PersonId == owner &&
            o.WorkDate >= today && o.WorkDate <= through).ToDictionaryAsync(o => o.WorkDate, o => o.AvailableHours);
        var availableByDay = new Dictionary<DateOnly, decimal>();
        for (var day = today; day <= through; day = day.AddDays(1))
            availableByDay[day] = AllocationRules.DailyCapacity(day, person.WeeklyCapacityHours ?? defaultWeekly, calendar,
                overrides.TryGetValue(day, out var hours) ? hours : null);

        var allocations = await db.Allocations.AsNoTracking().Where(a => a.PersonId == owner &&
            a.Status == AllocationStatus.Confirmed && a.FromDate <= through && a.ThroughDate >= today).ToListAsync();
        // A project-scoped readiness result must not infer spare capacity from assignments whose
        // visibility has not been established. Keep the result unknown rather than leaking or
        // silently under-counting another project's commitment.
        if (allocations.Any(a => a.ProjectId != project.Id))
            return new(true, null, "Production capacity is unknown while confirmed reservations are outside this readiness scope.");
        var allocationIds = allocations.Select(a => a.Id).ToArray();
        var taskDemand = sourceTasks.ToDictionary(t => t.TaskId,
            t => Workload.SpreadDays(t, today, calendar).ByDay);
        var demandByDay = taskDemand.Values.SelectMany(days => days).Where(x => x.Key <= through)
            .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(l => allocationIds.Contains(l.AllocationId) &&
            l.ReleasedAt == null && l.WorkType == "Task" && sourceTaskIds.Contains(l.WorkId)).ToListAsync();
        var dayOverrides = await db.AllocationDayOverrides.AsNoTracking().Where(o => allocationIds.Contains(o.AllocationId))
            .ToListAsync();
        var reservationByDay = new Dictionary<DateOnly, decimal>();
        var linkedReservationByDay = new Dictionary<DateOnly, decimal>();
        var linkedAllocationDays = links.Select(l => (l.AllocationId, l.WorkDate)).ToHashSet();
        foreach (var allocation in allocations)
        {
            var explicitDays = dayOverrides.Where(o => o.AllocationId == allocation.Id)
                .ToDictionary(o => o.WorkDate, o => o.Hours);
            IReadOnlyDictionary<DateOnly, decimal> spread;
            try { spread = AllocationRules.Spread(allocation.FromDate, allocation.ThroughDate, allocation.PlannedHours, calendar, explicitDays); }
            catch (ArgumentException) { return new(true, null, "Production capacity needs reassessment after a calendar change."); }
            foreach (var (day, hours) in spread.Where(x => x.Key >= today && x.Key <= through))
            {
                reservationByDay[day] = reservationByDay.GetValueOrDefault(day) + hours;
                if (linkedAllocationDays.Contains((allocation.Id, day)))
                    linkedReservationByDay[day] = linkedReservationByDay.GetValueOrDefault(day) + hours;
            }
        }
        var linkedByDay = links.DistinctBy(l => (l.WorkId, l.WorkDate)).GroupBy(l => l.WorkDate).ToDictionary(g => g.Key,
            g => g.Sum(l => taskDemand.GetValueOrDefault(l.WorkId)?.GetValueOrDefault(l.WorkDate) ?? 0m));
        // The target estimate is already included in demand. Subtract linked target demand once
        // per day from the aggregate reservations, even if competing allocations link the target.
        var satisfied = availableByDay.All(x => demandByDay.GetValueOrDefault(x.Key) +
            reservationByDay.GetValueOrDefault(x.Key) - Math.Min(linkedReservationByDay.GetValueOrDefault(x.Key),
                linkedByDay.GetValueOrDefault(x.Key)) <= x.Value);
        return new(true, satisfied, satisfied
            ? "Confirmed reservations fit within the owner's dated production capacity."
            : "Confirmed reservations and the required production estimate exceed the owner's dated capacity.");
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Create)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/checks/{code}/applicability", SetApplicability)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/exceptions", ApproveException)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints", Constraints);
        api.MapGet("/projects/{projectId:guid}/readiness/link-options", LinkOptions);
        api.MapGet("/projects/{projectId:guid}/readiness/window", Window);
        api.MapGet("/projects/{projectId:guid}/readiness/window/export", ExportWindow);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints", AddConstraint)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints/{constraintId:guid}/transition", MoveConstraint)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/submission-prerequisites", Prerequisites);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/submission-prerequisites", AddPrerequisite)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/submission-prerequisites/{linkId:guid}/remove", RemovePrerequisite)
            .WithMetadata(new Coordination.AtomicCommand());
    }

    // A Superseded package is followed to the successor whose issue superseded it; anything else is final.
    static async Task<SubmissionPackage?> EffectivePackage(HubDb db, Guid projectId, Guid packageId)
    {
        var package = await db.SubmissionPackages.AsNoTracking().SingleOrDefaultAsync(p => p.ProjectId == projectId && p.Id == packageId);
        for (var seen = new HashSet<Guid>(); package is { Status: SubmissionStatus.Superseded } && seen.Add(package.Id);)
        {
            var id = package.Id;
            var next = await db.SubmissionPackages.AsNoTracking().Where(p => p.ProjectId == projectId && p.SupersedesPackageId == id &&
                (p.Status == SubmissionStatus.Issued || p.Status == SubmissionStatus.Superseded)).OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();
            if (next is null) break;
            package = next;
        }
        return package;
    }

    // The output's own deliverable: the deliverable itself, or the deliverable a task belongs to.
    static async Task<Guid?> OwnDeliverable(HubDb db, string targetType, Guid targetId) => targetType == "Task"
        ? await db.Tasks.AsNoTracking().Where(t => t.Id == targetId).Select(t => t.DeliverableId).SingleOrDefaultAsync()
        : targetId;

    static async Task<bool> ContainsOutput(HubDb db, SubmissionPackage package, Guid? deliverableId) => deliverableId is { } id &&
        await db.SubmissionManifestItems.AsNoTracking().AnyAsync(m => m.PackageId == package.Id && m.ManifestVersion == package.ManifestVersion && m.DeliverableId == id);

    /// Null without an active link. A link whose current package now lists the output itself cannot gate it, so it is unknown.
    static async Task<(bool? Satisfied, string Reason)?> SubmissionGate(HubDb db, Guid projectId, string targetType, Guid targetId)
    {
        var links = await db.ReadinessSubmissionPrerequisites.AsNoTracking().Where(l => l.ProjectId == projectId &&
            l.TargetType == targetType && l.TargetId == targetId && l.RemovedAt == null).Select(l => l.PackageId).ToListAsync();
        if (links.Count == 0) return null;
        var deliverableId = await OwnDeliverable(db, targetType, targetId);
        var (issued, selfGating) = (true, false);
        foreach (var link in links)
        {
            var package = await EffectivePackage(db, projectId, link);
            issued &= package?.Status == SubmissionStatus.Issued;
            selfGating |= package is null || await ContainsOutput(db, package, deliverableId);
        }
        return selfGating ? (null, "A linked submission package now lists this output; remove or replace the link.")
            : issued ? (true, "Every linked submission package is Issued.") : (false, "A linked submission package is not Issued.");
    }

    static Task<Coordination.Result> Create(Guid projectId, string targetType, Guid targetId, CreateBody body,
        Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.create", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                if (target.RowVersion != body.TargetRowVersion)
                    throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, target.OwnerId));
                if (await db.ReadinessAssessments.AnyAsync(a => a.ProjectId == project.Id &&
                    a.TargetType == target.Type && a.TargetId == target.Id))
                    throw ApiException.Conflict("readiness_exists", "error.duplicate");
                var assessment = new ReadinessAssessment { ProjectId = project.Id, TargetType = target.Type,
                    TargetId = target.Id, OwnerId = target.OwnerId,
                    IntendedOutput = Check.Required(body.IntendedOutput, "intendedOutput", 2000),
                    CompletionCriteria = Check.Required(body.CompletionCriteria, "completionCriteria", 2000),
                    State = ReadinessState.NeedsAssessment, EvaluatedAt = clock.GetUtcNow() };
                db.ReadinessAssessments.Add(assessment);
                foreach (var code in ReadinessCheckCode.All)
                    db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = project.Id,
                        AssessmentId = assessment.Id, Code = code });
                return assessment;
            });

    static Task<Coordination.Result> SetApplicability(Guid projectId, string targetType, Guid targetId, string code,
        ApplicabilityBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.applicability", targetType, targetId, code, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                var assessment = await db.ReadinessAssessments.SingleOrDefaultAsync(a => a.ProjectId == project.Id &&
                    a.TargetType == target.Type && a.TargetId == target.Id) ?? throw ApiException.NotFound();
                Coordination.Version(assessment, body.AssessmentRowVersion);
                Check.OneOf(code, ReadinessCheckCode.All, "code");
                if (code == ReadinessCheckCode.ProductionOwner)
                    Check.That(body.Applies, "applies", "error.required");
                var check = await db.ReadinessChecks.SingleOrDefaultAsync(c => c.ProjectId == project.Id &&
                    c.AssessmentId == assessment.Id && c.Code == code) ?? throw ApiException.NotFound();
                Coordination.Version(check, body.CheckRowVersion);
                check.Applies = body.Applies; check.Satisfied = body.Applies ? check.Satisfied : null;
                check.Reason = Check.Reason(body.Reason);
                check.EvidenceUrl = string.IsNullOrWhiteSpace(body.EvidenceUrl) ? null : Coordination.Url(body.EvidenceUrl);
                check.RecordedBy = access.Me.Id;
                var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.Id).ToListAsync();
                assessment.State = ReadinessRules.Evaluate(checks.Select(c => new ReadinessCheck(c.Code, c.Applies, c.Satisfied)),
                    null, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)).State;
                assessment.EvaluatedAt = clock.GetUtcNow();
                db.Audit.Note(check, reason: check.Reason);
                db.Audit.Note(assessment, reason: check.Reason);
                return check;
            });

    static Task<Coordination.Result> ApproveException(Guid projectId, string targetType, Guid targetId,
        ExceptionBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.exception", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                var assessment = await db.ReadinessAssessments.SingleOrDefaultAsync(a => a.ProjectId == project.Id &&
                    a.TargetType == target.Type && a.TargetId == target.Id) ?? throw ApiException.NotFound();
                Coordination.Version(assessment, body.AssessmentRowVersion);
                Check.That(assessment.OwnerId == target.OwnerId, "ownerId", "coord.stale");
                var version = await db.DesignBasisVersions.SingleOrDefaultAsync(v => v.ProjectId == project.Id &&
                    v.Id == body.BasisVersionId) ?? throw ApiException.NotFound();
                Coordination.Version(version, body.BasisVersionRowVersion);
                var entry = await db.DesignBasisEntries.SingleAsync(e => e.ProjectId == project.Id && e.Id == version.EntryId);
                Check.That(entry.Kind == BasisKind.Assumption && version.Status == BasisStatus.Proposed,
                    "basisVersionId", "basis.assumption");
                var latestUse = await db.BasisUses.AsNoTracking().Where(u => u.ProjectId == project.Id &&
                    u.TargetType == target.Type && u.TargetId == target.Id &&
                    db.DesignBasisVersions.Any(v => v.Id == u.VersionId && v.EntryId == entry.Id))
                    .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).FirstOrDefaultAsync();
                Check.That(latestUse?.VersionId == version.Id, "basisVersionId", "basis.assumption");
                await Coordination.Person(db, project, body.VerifierId, "verifierId");
                Check.That(body.VerifierId != target.OwnerId && body.VerifierId != access.Me.Id,
                    "verifierId", "coord.separation");
                var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                Check.That(body.ExpiresOn >= today, "expiresOn", "basis.assumption");
                Check.That(await db.BasisAssumptionDispositions.AnyAsync(d => d.ProjectId == project.Id &&
                    d.VersionId == version.Id && d.OwnerId == target.OwnerId && d.ExpiresOn >= body.ExpiresOn &&
                    d.ApprovedBy != target.OwnerId && d.Scope.ToLower() == version.Scope.ToLower()),
                    "basisVersionId", "basis.assumption");
                var row = new ReadinessException { ProjectId = project.Id, AssessmentId = assessment.Id,
                    BasisVersionId = version.Id, ApprovedBy = access.Me.Id, VerifierId = body.VerifierId,
                    LimitedWork = Check.Required(body.LimitedWork, "limitedWork", 2000),
                    Risk = Check.Required(body.Risk, "risk", 2000), ExpiresOn = body.ExpiresOn };
                db.ReadinessExceptions.Add(row);
                db.Audit.Note(row);
                return row;
            });

    static async Task<object> Detail(Guid projectId, string targetType, Guid targetId, Access access, HubDb db, TimeProvider clock,
        SettingsStore settings)
    {
        var (project, _) = await access.Project(projectId, false);
        Check.OneOf(targetType, ["Task", "Deliverable"], "targetType");
        var assessment = await db.ReadinessAssessments.AsNoTracking().SingleOrDefaultAsync(a =>
            a.ProjectId == projectId && a.TargetType == targetType && a.TargetId == targetId) ?? throw ApiException.NotFound();
        var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == projectId &&
            c.AssessmentId == assessment.Id).OrderBy(c => c.Code).ToListAsync();
        var result = await EvaluateCurrent(db, project, targetType, targetId, assessment, checks,
            clock.Today(await settings.Get(db)), clock.GetUtcNow(), settings);
        return new { Assessment = assessment, Checks = checks, result.Unknown, result.Blocked,
            Exceptions = await db.ReadinessExceptions.AsNoTracking().Where(e => e.ProjectId == projectId &&
                e.AssessmentId == assessment.Id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync() };
    }

    static async Task<object> Constraints(Guid projectId, string targetType, Guid targetId, Access access, HubDb db)
    {
        var (project, _) = await access.Project(projectId, false);
        await Coordination.Target(db, project, targetType, targetId, false);
        var rows = await db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == projectId &&
            c.TargetType == targetType && c.TargetId == targetId).OrderBy(c => c.NeededBy).ThenBy(c => c.CreatedAt).ToListAsync();
        var result = new List<object>();
        foreach (var c in rows)
            result.Add(new { c.Id, c.Key, c.RowVersion, c.TargetType, c.TargetId, c.Category, c.Description, c.RemovalOwnerId,
                c.AffectedOwnerId, c.NeededBy, c.SourceUrl, c.State, c.ResolutionEvidenceUrl, c.VerifiedBy, c.VerifiedAt,
                c.LinkedType, c.LinkedId, c.CreatedAt, c.CreatedBy,
                Linked = c.LinkedType is { } type ? await Linkable(db, projectId, type, c.LinkedId).FirstOrDefaultAsync() : null });
        return result;
    }

    static async Task<List<LinkedRecord>> LinkOptions(Guid projectId, string type, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        Check.OneOf(type, LinkTypes, "type");
        return await Linkable(db, projectId, type).Take(500).ToListAsync();
    }

    public sealed record ReadyOutput(Guid Id, string TargetType, Guid TargetId, string Key, string Name, DateOnly? DueDate,
        Guid? OwnerId, Guid DisciplineId, string IntendedOutput, string CompletionCriteria, string State);

    /// The readiness window behind both the page and its exports (FR-RDY-07, FR-MDC-06): open constraints needed by the end of
    /// the window, and work due in it whose current evaluation is Ready.
    static async Task<(DateOnly First, DateOnly Last, IQueryable<WorkConstraint> Constraints, List<ReadyOutput> Ready)> WindowData(
        Project project, DateOnly? from, DateOnly? to, HubDb db, SettingsStore settings, TimeProvider clock)
    {
        var projectId = project.Id;
        var org = await settings.Get(db);
        var first = from ?? clock.Today(org);
        var defaultDays = org.CoordinationLookaheadWeeks * 7 - 1;
        Check.That(to.HasValue || first.DayNumber <= DateOnly.MaxValue.DayNumber - defaultDays, "from", "error.invalid");
        var last = to ?? first.AddDays(defaultDays);
        Check.That(last >= first && last.DayNumber - first.DayNumber <= 83, "to", "error.invalid");

        var constraints = db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == projectId &&
            c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled &&
            c.NeededBy <= last).OrderBy(c => c.NeededBy).ThenBy(c => c.CreatedAt);

        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && t.DeletedAt == null &&
            t.DueDate >= first && t.DueDate <= last && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold)
            .Select(t => new { t.Id, t.Key, t.Name, t.DueDate, OwnerId = t.AssigneeId, t.ProjectDisciplineId })
            .ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && d.DeletedAt == null &&
            d.DueDate >= first && d.DueDate <= last && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.OnHold)
            .Select(d => new { d.Id, d.Key, d.Name, d.DueDate, OwnerId = d.OwnerId, d.ProjectDisciplineId })
            .ToListAsync();
        var assessments = await db.ReadinessAssessments.AsNoTracking().Where(a => a.ProjectId == projectId &&
            ((a.TargetType == "Task" && tasks.Select(t => t.Id).Contains(a.TargetId)) ||
             (a.TargetType == "Deliverable" && deliverables.Select(d => d.Id).Contains(a.TargetId))))
            .ToListAsync();
        var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == projectId &&
            assessments.Select(a => a.Id).Contains(c.AssessmentId)).ToListAsync();
        var today = clock.Today(org);
        var ready = new List<ReadyOutput>();
        foreach (var assessment in assessments)
        {
            var result = await EvaluateCurrent(db, project, assessment.TargetType, assessment.TargetId, assessment,
                checks.Where(c => c.AssessmentId == assessment.Id).ToList(), today, clock.GetUtcNow(), settings);
            if (result.State != ReadinessState.Ready) continue;
            if (assessment.TargetType == "Task" && tasks.SingleOrDefault(t => t.Id == assessment.TargetId) is { } task)
                ready.Add(new(assessment.Id, assessment.TargetType, task.Id, task.Key, task.Name, task.DueDate, task.OwnerId,
                    task.ProjectDisciplineId, assessment.IntendedOutput, assessment.CompletionCriteria, result.State));
            else if (assessment.TargetType == "Deliverable" && deliverables.SingleOrDefault(d => d.Id == assessment.TargetId) is { } deliverable)
                ready.Add(new(assessment.Id, assessment.TargetType, deliverable.Id, deliverable.Key, deliverable.Name, deliverable.DueDate,
                    deliverable.OwnerId, deliverable.ProjectDisciplineId, assessment.IntendedOutput, assessment.CompletionCriteria, result.State));
        }
        return (first, last, constraints, [.. ready.OrderBy(r => r.DueDate).ThenBy(r => r.Key)]);
    }

    static async Task<object> Window(Guid projectId, DateOnly? from, DateOnly? to, Access access, HubDb db,
        SettingsStore settings, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false);
        var window = await WindowData(project, from, to, db, settings, clock);
        var constraintsTotal = await window.Constraints.CountAsync();
        var constraints = await window.Constraints.Take(500).ToListAsync();
        var readyTotal = window.Ready.Count;
        return new { From = window.First, To = window.Last, Constraints = constraints, ConstraintsTotal = constraintsTotal,
            ConstraintsTruncated = constraintsTotal > constraints.Count, ReadyOutputs = window.Ready.Take(500),
            ReadyOutputsTotal = readyTotal, ReadyOutputsTruncated = readyTotal > 500 };
    }

    static readonly Col[] ConstraintColumns = [new("key", "key"), new("work", "item"), new("category", "constraintCategory"),
        new("description", "description"), new("removalOwner", "removalOwner"), new("affectedOwner", "affectedOwner"),
        new("neededBy", "neededBy", "date"), new("state", "status"), new("linked", "linkedRecord"), new("sourceUrl", "sourceEvidence")];
    static readonly Col[] ReadyColumns = [new("key", "key"), new("name", "name"), new("targetType", "type"), new("owner", "owner"),
        new("discipline", "discipline"), new("dueDate", "due", "date"), new("intendedOutput", "intendedOutput"),
        new("completionCriteria", "completionCriteria"), new("state", "readiness")];

    /// FR-MDC-06: the page's constraint or ready-output list as a file, from the same window query and permission scope.
    static async Task<IResult> ExportWindow(Guid projectId, DateOnly? from, DateOnly? to, string list, string? format, HttpContext http,
        Access access, HubDb db, SettingsStore settings, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false);
        Check.OneOf(list, ["constraints", "ready"], "list");
        var window = await WindowData(project, from, to, db, settings, clock);
        var filters = await ListExportEndpoints.Filters(db, http);
        async Task<Dictionary<Guid, string>> Names(IEnumerable<Guid> ids)
        {
            var set = ids.Distinct().ToArray();
            return await db.Users.AsNoTracking().Where(u => set.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        }
        if (list == "ready")
        {
            var users = await Names(window.Ready.Where(r => r.OwnerId != null).Select(r => r.OwnerId!.Value));
            var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == projectId)
                .Join(db.Disciplines, pd => pd.DisciplineId, d => d.Id, (pd, d) => new { pd.Id, d.Name }).ToDictionaryAsync(d => d.Id, d => d.Name);
            var ready = window.Ready.Select(r => new { r.Key, r.Name, r.TargetType, Owner = r.OwnerId is { } o ? users.GetValueOrDefault(o) : null,
                Discipline = disciplines.GetValueOrDefault(r.DisciplineId), r.DueDate, r.IntendedOutput, r.CompletionCriteria, r.State });
            return await ExportFile.Send(db, settings, format, Text.Get("export.readyOutputs", project.ProjectNumber), ReadyColumns,
                JsonSerializer.SerializeToNode(ready, JsonOpts.Web)!.AsArray(), filters, project.Id, $"{project.ProjectNumber}-ready-outputs", clock);
        }
        var rows = await window.Constraints.Take(Export.MaxRows + 1).ToListAsync();
        var people = await Names(rows.SelectMany(r => new[] { r.RemovalOwnerId, r.AffectedOwnerId }));
        var work = await WorkLabels(db, projectId, rows.Select(r => r.TargetId).ToArray());
        var linked = await LinkedLabels(db, projectId, rows.Where(r => r.LinkedId != null).Select(r => r.LinkedId!.Value).ToArray());
        var constraints = rows.Select(c => new { c.Key, Work = work.GetValueOrDefault(c.TargetId), c.Category, c.Description,
            RemovalOwner = people.GetValueOrDefault(c.RemovalOwnerId), AffectedOwner = people.GetValueOrDefault(c.AffectedOwnerId), c.NeededBy, c.State,
            Linked = c.LinkedId is { } id ? linked.GetValueOrDefault(id) ?? Text.Get("readiness.linked_unavailable") : null, c.SourceUrl });
        return await ExportFile.Send(db, settings, format, Text.Get("export.readinessConstraints", project.ProjectNumber), ConstraintColumns,
            JsonSerializer.SerializeToNode(constraints, JsonOpts.Web)!.AsArray(), filters, project.Id, $"{project.ProjectNumber}-readiness-constraints", clock);
    }

    /// "KEY Name" for the tasks and deliverables a readiness export lists.
    public static async Task<Dictionary<Guid, string>> WorkLabels(HubDb db, Guid projectId, Guid[] ids) =>
        (await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && ids.Contains(t.Id)).Select(t => new { t.Id, Label = t.Key + " " + t.Name }).ToListAsync())
        .Concat(await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && ids.Contains(d.Id)).Select(d => new { d.Id, Label = d.Key + " " + d.Name }).ToListAsync())
        .ToDictionary(x => x.Id, x => x.Label);

    static async Task<Dictionary<Guid, string>> LinkedLabels(HubDb db, Guid projectId, Guid[] ids) =>
        (await db.Decisions.AsNoTracking().Where(d => d.ProjectId == projectId && ids.Contains(d.Id)).Select(d => new { d.Id, Label = d.Key + " " + d.Subject + " (" + d.Status + ")" }).ToListAsync())
        .Concat(await db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId && ids.Contains(i.Id)).Select(i => new { i.Id, Label = i.Key + " " + i.Title + " (" + i.Status + ")" }).ToListAsync())
        .Concat(await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == projectId && ids.Contains(h.Id)).Select(h => new { h.Id, Label = h.Key + " " + h.Title + " (" + h.Status + ")" }).ToListAsync())
        .ToDictionary(x => x.Id, x => x.Label);

    static Task<Coordination.Result> AddConstraint(Guid projectId, string targetType, Guid targetId,
        ConstraintBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "constraint.create", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                if (target.RowVersion != body.TargetRowVersion)
                    throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                var ownerAction = Permissions.NamedCoordinationAction(access.Actor, ctx, target.OwnerId);
                if (!ownerAction.Ok) Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                await Coordination.Person(db, project, body.RemovalOwnerId, "removalOwnerId");
                Check.That(body.RemovalOwnerId != target.OwnerId, "removalOwnerId", "coord.separation");
                Check.OneOf(body.Category, ConstraintCategories, "category");
                Check.That(body.LinkedType is null == body.LinkedId is null, "linkedId", "error.required");
                if (body.LinkedType is { } linkedType)
                {
                    Check.OneOf(linkedType, LinkTypes, "linkedType");
                    Check.That(await Linkable(db, project.Id, linkedType, body.LinkedId).AnyAsync(), "linkedId", "coord.reference");
                }
                var row = new WorkConstraint { ProjectId = project.Id, TargetType = target.Type, TargetId = target.Id,
                    Category = body.Category, Description = Check.Required(body.Description, "description", 2000),
                    RemovalOwnerId = body.RemovalOwnerId, AffectedOwnerId = target.OwnerId,
                    NeededBy = body.NeededBy, SourceUrl = Coordination.Url(body.SourceUrl),
                    LinkedType = body.LinkedType, LinkedId = body.LinkedId };
                (row.Seq, row.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "constraint");
                db.WorkConstraints.Add(row);
                db.Audit.Note(row);
                await NotifyConstraint(notify, project, target, row, NotificationEvents.ConstraintAction, [row.RemovalOwnerId],
                    Text.Get("notify.constraint_assigned", row.Category, target.Key));
                return row;
            });

    static Task<Coordination.Result> MoveConstraint(Guid projectId, string targetType, Guid targetId,
        Guid constraintId, ConstraintMoveBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "constraint.transition", targetType, targetId, constraintId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                var row = await db.WorkConstraints.SingleOrDefaultAsync(c => c.Id == constraintId &&
                    c.ProjectId == project.Id && c.TargetType == target.Type && c.TargetId == target.Id)
                    ?? throw ApiException.NotFound();
                Coordination.Version(row, body.RowVersion);
                Check.OneOf(body.ToState, ConstraintState.All, "toState");
                var reason = Check.Reason(body.Reason);
                if (body.ToState == ConstraintState.ResolutionProposed)
                {
                    Check.That(row.State == ConstraintState.Open, "toState", "coord.transition");
                    Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, row.RemovalOwnerId));
                    row.ResolutionEvidenceUrl = Coordination.Url(body.EvidenceUrl);
                }
                else if (body.ToState == ConstraintState.VerifiedRemoved)
                {
                    Check.That(row.State == ConstraintState.ResolutionProposed, "toState", "coord.transition");
                    Check.That(target.OwnerId == row.AffectedOwnerId, "affectedOwnerId", "coord.stale");
                    Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, row.AffectedOwnerId));
                    Check.That(row.ResolutionEvidenceUrl is not null, "evidenceUrl", "error.required");
                    row.VerifiedBy = access.Me.Id;
                    row.VerifiedAt = clock.GetUtcNow();
                }
                else if (body.ToState == ConstraintState.Cancelled)
                {
                    Check.That(row.State == ConstraintState.Open || row.State == ConstraintState.ResolutionProposed,
                        "toState", "coord.transition");
                    Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                }
                else throw ApiException.Invalid("toState", "coord.transition");
                row.State = body.ToState;
                db.Audit.Note(row, reason: reason);
                if (row.State == ConstraintState.ResolutionProposed)
                    await NotifyConstraint(notify, project, target, row, NotificationEvents.ConstraintAction, [row.AffectedOwnerId],
                        Text.Get("notify.constraint_proposed", row.Category, target.Key));
                else
                    await NotifyConstraint(notify, project, target, row, NotificationEvents.ConstraintOutcome,
                        [row.RemovalOwnerId, row.AffectedOwnerId], Text.Get("notify.constraint_outcome", row.Category, target.Key, row.State));
                return row;
            });

    // Queued inside the command transaction: a refused, conflicting or replayed command adds no notice (FR-MDC-03, FR-MDC-06).
    static Task NotifyConstraint(Notifier notify, Project project, Coordination.Work target, WorkConstraint row, string eventType,
        Guid?[] recipients, string title) =>
        notify.Send(eventType, recipients, new NotifyItem(project.Id, "WorkConstraint", row.Id, target.Key,
            $"/projects/{project.ProjectNumber}/readiness", project.ProjectNumber), title);

    static async Task<object> Prerequisites(Guid projectId, string targetType, Guid targetId, Access access, HubDb db)
    {
        var (project, _) = await access.Project(projectId, false);
        var target = await Coordination.Target(db, project, targetType, targetId, false);
        var deliverableId = await OwnDeliverable(db, target.Type, target.Id);
        var links = await db.ReadinessSubmissionPrerequisites.AsNoTracking().Where(l => l.ProjectId == projectId &&
            l.TargetType == target.Type && l.TargetId == target.Id).OrderBy(l => l.CreatedAt).ThenBy(l => l.Id).ToListAsync();
        var rows = new List<object>();
        foreach (var link in links)
        {
            var package = await db.SubmissionPackages.AsNoTracking().Where(p => p.ProjectId == projectId && p.Id == link.PackageId)
                .Select(p => new { p.Id, p.Key, p.Title, p.Status }).SingleAsync();
            var effective = await EffectivePackage(db, projectId, link.PackageId);
            rows.Add(new { link.Id, link.RowVersion, link.PackageId, link.Reason, link.CreatedAt, link.CreatedBy,
                link.RemovedAt, link.RemovedBy, link.RemovalReason, Package = package,
                Effective = effective is null ? null : new { effective.Id, effective.Key, effective.Title, effective.Status },
                ListsOutput = effective is not null && await ContainsOutput(db, effective, deliverableId) });
        }
        return rows;
    }

    static Task<Coordination.Result> AddPrerequisite(Guid projectId, string targetType, Guid targetId,
        PrerequisiteBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.prerequisite.add", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                if (target.RowVersion != body.TargetRowVersion)
                    throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                var reason = Check.Reason(body.Reason);
                var package = await db.SubmissionPackages.AsNoTracking().SingleOrDefaultAsync(p => p.ProjectId == project.Id && p.Id == body.PackageId)
                    ?? throw ApiException.Invalid("packageId", "coord.reference");
                // The output's own submission never gates itself, including through the successor the gate would follow.
                var deliverableId = await OwnDeliverable(db, target.Type, target.Id);
                var effective = await EffectivePackage(db, project.Id, package.Id);
                Check.That(!await ContainsOutput(db, package, deliverableId) && !await ContainsOutput(db, effective!, deliverableId),
                    "packageId", "readiness.prerequisite_self");
                if (await db.ReadinessSubmissionPrerequisites.AnyAsync(l => l.ProjectId == project.Id && l.TargetType == target.Type &&
                    l.TargetId == target.Id && l.PackageId == package.Id && l.RemovedAt == null))
                    throw ApiException.Conflict("prerequisite_exists", "error.duplicate");
                var row = new ReadinessSubmissionPrerequisite { ProjectId = project.Id, TargetType = target.Type,
                    TargetId = target.Id, PackageId = package.Id, Reason = reason };
                db.ReadinessSubmissionPrerequisites.Add(row);
                db.Audit.Note(row, reason: reason);
                return row;
            });

    static Task<Coordination.Result> RemovePrerequisite(Guid projectId, string targetType, Guid targetId, Guid linkId,
        PrerequisiteRemoveBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.prerequisite.remove", targetType, targetId, linkId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                var row = await db.ReadinessSubmissionPrerequisites.SingleOrDefaultAsync(l => l.Id == linkId && l.ProjectId == project.Id &&
                    l.TargetType == target.Type && l.TargetId == target.Id) ?? throw ApiException.NotFound();
                Coordination.Version(row, body.RowVersion);
                Check.That(row.RemovedAt is null, "linkId", "coord.transition");
                row.RemovalReason = Check.Reason(body.Reason);
                row.RemovedAt = clock.GetUtcNow();
                row.RemovedBy = access.Me.Id;
                db.Audit.Note(row, action: "Removed", reason: row.RemovalReason);
                return row;
            });
}
