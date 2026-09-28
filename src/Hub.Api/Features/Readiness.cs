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
        Guid RemovalOwnerId, DateOnly NeededBy, string SourceUrl);
    public sealed record ConstraintMoveBody(Guid RequestId, int RowVersion, string ToState, string Reason,
        string? EvidenceUrl);
    static readonly string[] ConstraintCategories = ["Handoff", "Decision", "Basis", "Capacity", "Review", "Scope", "Other"];

    // Source-backed checks are recomputed at read/command time. Manual applicability remains useful for
    // checks without a canonical source, but it cannot keep a linked source in a stale Ready state.
    public static async Task<ReadinessResult> EvaluateCurrent(HubDb db, Project project, string targetType,
        Guid targetId, ReadinessAssessment assessment, IReadOnlyList<ReadinessCheckRecord> records, DateOnly today, DateTimeOffset now)
    {
        var target = await Coordination.Target(db, project, targetType, targetId, false);
        var checks = records.ToDictionary(x => x.Code, StringComparer.Ordinal);
        void Source(string code, bool applies, bool satisfied, string reason)
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
            Source(ReadinessCheckCode.Basis, true, versions.Count == currentUses.Count && !conflicts && !pendingImpact &&
                versions.All(v => v.Status == BasisStatus.Confirmed),
                "Linked basis uses and conflicts are current source evidence.");
        }

        Source(ReadinessCheckCode.ProductionOwner, true,
            target.OwnerId != Guid.Empty && await Coordination.People(db, project).AnyAsync(u => u.Id == target.OwnerId),
            "The linked production owner is current source evidence.");

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

        var result = ReadinessRules.Evaluate(checks.Values.Select(c => new ReadinessCheck(c.Code, c.Applies, c.Satisfied)), null, today);
        var openConstraint = await db.WorkConstraints.AsNoTracking().AnyAsync(c => c.ProjectId == project.Id &&
            c.TargetType == targetType && c.TargetId == targetId &&
            (c.State == ConstraintState.Open || c.State == ConstraintState.ResolutionProposed));
        if (openConstraint) result = new ReadinessResult(ReadinessState.NotReady, result.Unknown,
            [.. result.Blocked, "Constraint"]);
        assessment.State = result.State; assessment.EvaluatedAt = now;
        return result;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Create)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/checks/{code}/applicability", SetApplicability)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints", Constraints);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints", AddConstraint)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/constraints/{constraintId:guid}/transition", MoveConstraint)
            .WithMetadata(new Coordination.AtomicCommand());
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
            clock.Today(await settings.Get(db)), clock.GetUtcNow());
        return new { Assessment = assessment, Checks = checks, result.Unknown, result.Blocked };
    }

    static async Task<object> Constraints(Guid projectId, string targetType, Guid targetId, Access access, HubDb db)
    {
        var (project, _) = await access.Project(projectId, false);
        await Coordination.Target(db, project, targetType, targetId, false);
        return await db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == projectId &&
            c.TargetType == targetType && c.TargetId == targetId).OrderBy(c => c.NeededBy).ThenBy(c => c.CreatedAt).ToListAsync();
    }

    static Task<Coordination.Result> AddConstraint(Guid projectId, string targetType, Guid targetId,
        ConstraintBody body, Access access, HubDb db, TimeProvider clock) =>
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
                var row = new WorkConstraint { ProjectId = project.Id, TargetType = target.Type, TargetId = target.Id,
                    Category = body.Category, Description = Check.Required(body.Description, "description", 2000),
                    RemovalOwnerId = body.RemovalOwnerId, AffectedOwnerId = target.OwnerId,
                    NeededBy = body.NeededBy, SourceUrl = Coordination.Url(body.SourceUrl) };
                db.WorkConstraints.Add(row);
                db.Audit.Note(row);
                return row;
            });

    static Task<Coordination.Result> MoveConstraint(Guid projectId, string targetType, Guid targetId,
        Guid constraintId, ConstraintMoveBody body, Access access, HubDb db, TimeProvider clock) =>
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
                return row;
            });
}
