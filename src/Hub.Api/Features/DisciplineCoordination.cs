using System.Data;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Packet 030 read projection. Counts and rows are produced from the same permission-checked,
/// filtered source query; callers never have to infer a total from a capped register page.
public static partial class DisciplineCoordinationEndpoints
{
    // The rows below are written in the property order each has always had; the blocks of one projection are paged together.
    public sealed partial record CoordinationHandoffRow(Guid Id, string Key, string Title, string Status, DateOnly NeededBy, DateOnly? PromisedBy, Guid? TargetTaskId,
        Guid? TargetDeliverableId, Guid SendingOwnerId, Guid ReceivingOwnerId, Guid SendingDisciplineId, Guid ReceivingDisciplineId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HandoffReadinessLinks? ReadinessLinks = null);
    /// One handoff that holds a target task and every active task downstream of it.
    public sealed partial record CoordinationBlockerGroup(Guid HandoffId, string HandoffKey, Guid[] TaskIds, string[] TaskKeys,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HandoffReadinessLinks? ReadinessLinks = null);
    /// A change notice still Open or still holding Pending Assessment in this scope, with the scoped counts beside the whole-project count.
    public sealed partial record CoordinationChangeRow(Guid Id, string Key, string Title, string Status, Guid OwnerId, int PendingAssessments, int AcknowledgedPending, int ProjectPending,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed partial record CoordinationReviewRow(Guid Id, string Key, string Title, string Status, Guid CoordinatorId, int OutstandingDisciplines, int BlockingFindings,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed partial record CoordinationIssueRow(Guid Id, string Key, string Title, string Status, Guid OwnerId, string? OwnerName, Guid? ProjectDisciplineId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    /// An adopted source revision with the work that uses it; the target and source columns are null when the row they name is gone.
    public sealed partial record CoordinationInputUseRow(Guid Id, string TargetType, Guid TargetId, Guid OwnerId, string SourceIdentity, Guid SourceRevisionId, string IntendedUse,
        DateTimeOffset AdoptedAt, Guid AdoptedBy, int RowVersion, string? TargetKey, string? TargetName, string? SourceUrl, string? SourceKey, string? Revision,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    /// A task or deliverable an assessment of a change names; Available is false once it was deleted or left the project.
    public sealed record CoordinationChangeTarget(Guid ChangeNoticeId, string TargetType, Guid TargetId, bool Available);
    public sealed record CoordinationUnavailableTarget(Guid ChangeNoticeId, int Count);
    /// An open meeting action linked to a blocking handoff or a change notice; SourceType and SourceId name that handoff or notice.
    public sealed partial record CoordinationLinkedAction(Guid Id, string Key, string Text, string Status, DateOnly? DueDate, string SourceType, Guid SourceId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed record SubmissionFailingCheck(Guid? SourceId, string Kind, string Code, string Message, string? SourcePath);
    public sealed partial record SubmissionContextRow(Guid Id, string Key, string Title, string Status, string EffectiveStatus, DateOnly TargetDate,
        Guid CoordinatorId, IReadOnlyList<SubmissionFailingCheck> FailingChecks,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed partial record StaffingContextRow(Guid Id, string Key, string TargetType, Guid TargetId, string Description, DateOnly NeededBy,
        string State, Guid AffectedOwnerId, Guid RemovalOwnerId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);

    /// Counts and rows come from the same filtered source query; Page and PageSize are 1 and 0 when the caller asked for no paging.
    public sealed record CoordinationProjection(
        DateTimeOffset EvaluatedAt,
        IReadOnlyList<CoordinationHandoffRow> Handoffs, IReadOnlyList<CoordinationHandoffRow> Outgoing, IReadOnlyList<CoordinationHandoffRow> Incoming,
        int OutgoingPage, int IncomingPage, int OutgoingTotal, int IncomingTotal, int HandoffPage, int HandoffPageSize,
        IReadOnlyList<CoordinationChangeTarget> ChangeTargets, IReadOnlyList<CoordinationUnavailableTarget> UnavailableChangeTargets, int ChangeTargetsTotal, int UnavailableChangeTargetsTotal,
        IReadOnlyList<CoordinationReviewRow> Reviews, int ReviewsPage, int ReviewsPageSize,
        IReadOnlyList<CoordinationInputUseRow> Uses, int UsesTotal, int UsesPage, int UsesPageSize,
        IReadOnlyList<CoordinationIssueRow> LinkedIssues, int LinkedIssuesTotal, int LinkedIssuesPage, int LinkedIssuesPageSize,
        int HandoffsTotal,
        IReadOnlyList<CoordinationChangeRow> Changes, int ChangesTotal, int ChangesPage, int ChangesPageSize,
        int ReviewsTotal,
        IReadOnlyList<CoordinationBlockerGroup> BlockerGroups, int BlockerGroupsTotal,
        IReadOnlyList<StartabilityRow> Startability, int StartabilityTotal, int StartabilityPage, int StartabilityPageSize,
        DateOnly StartabilityFrom, DateOnly StartabilityTo, int StartabilityReadyTotal,
        IReadOnlyList<CoordinationLinkedAction> LinkedActions, int LinkedActionsTotal,
        IReadOnlyList<SubmissionContextRow> UpcomingSubmissions, int UpcomingSubmissionsTotal, int UpcomingSubmissionsPage,
        IReadOnlyList<StaffingContextRow> StaffingConflicts, int StaffingConflictsTotal, int StaffingConflictsPage,
        int BlockerGroupsPage, int Page, int PageSize);

    public sealed record WorkspaceCoordinationProject(Guid Id, string ProjectNumber, string Name, Guid? DisciplineId, CoordinationProjection Data);
    public sealed record WorkspaceProjectChoice(Guid Id, string ProjectNumber, string Name, Guid ProjectManagerId);
    public sealed record WorkspaceDisciplineChoice(Guid Id, string Name);
    public sealed record WorkspaceOwnerChoice(Guid Id, string DisplayName, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PersonFacts.Notices? PersonNotices = null);
    /// The permitted projects of one scope, each with its projection, and the choices the filters offer; Scope is Project or Workspace.
    public sealed record WorkspaceCoordinationProjection(DateTimeOffset EvaluatedAt, Guid ActorId, IReadOnlyList<WorkspaceCoordinationProject> Projects,
        IReadOnlyList<WorkspaceProjectChoice> ProjectChoices, IReadOnlyList<WorkspaceDisciplineChoice> Disciplines, IReadOnlyList<WorkspaceOwnerChoice> Owners, string Scope);

    sealed record DependencyEdge(Guid PredecessorTaskId, Guid SuccessorTaskId);
    sealed record TaskRef(Guid Id, string Key);
    sealed record MemberDiscipline(Guid ProjectId, Guid? PrimaryDisciplineId);
    sealed record ActionLink(Guid SourceId, string TargetType, Guid TargetId);
    sealed record StartableWork(Guid Id, string Key, string Name, DateOnly? DueDate, Guid? OwnerId, Guid DisciplineId);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/discipline-coordination", Get);
        api.MapGet("/discipline-coordination", Workspace);
    }

    static async Task<Ok<CoordinationProjection>> Get(Guid projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        int? page, int? pageSize,
        Access access, HubDb db, TimeProvider clock, SettingsStore settings)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await access.Project(projectId, track: false);
        var org = await settings.Get(db);
        var projection = await Build(projectId, disciplineId, ownerId, from, to, page, pageSize, access.Me.Id, db, clock.GetUtcNow(),
            clock.Today(org), org.CoordinationLookaheadWeeks * 7 - 1, settings, org);
        await WorkCoordinationReadMetadata.Apply(db, access, org,
            new(access.Me.Id, clock.Today(org), clock.GetUtcNow()), [projection]);
        return TypedResults.Ok(projection);
    }

    static async Task<Ok<WorkspaceCoordinationProjection>> Workspace(Guid? projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        string? scopeKind, string? scopeProjectIds, Guid? scopeWorkspaceId,
        int? page, int? pageSize,
        Access access, HubDb db, TimeProvider clock, SettingsStore settings)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var visible = access.VisibleProjects().AsNoTracking().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        if (scopeKind == "mine") visible = visible.Where(p => p.ProjectManagerId == access.Me.Id ||
            db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == access.Me.Id && m.RemovedAt == null));
        else if (scopeKind == "workspace")
        {
            if (scopeWorkspaceId is not { } workspaceId ||
                !await db.Workspaces.AnyAsync(w => w.Id == workspaceId && w.OwnerId == access.Me.Id))
                throw ApiException.NotFound();
            var ids = await db.WorkspaceProjects.AsNoTracking().Where(wp => wp.WorkspaceId == workspaceId)
                .Select(wp => wp.ProjectId).ToArrayAsync();
            visible = visible.Where(p => ids.Contains(p.Id));
        }
        else if (scopeKind == "set")
        {
            var ids = Http.Ids(scopeProjectIds);
            visible = visible.Where(p => ids.Contains(p.Id));
        }
        else if (scopeKind is not (null or "all")) throw ApiException.Invalid("scopeKind", "coord.reference");
        var choices = await visible.OrderBy(p => p.ProjectNumber).Select(p => new WorkspaceProjectChoice(p.Id, p.ProjectNumber, p.Name, p.ProjectManagerId)).ToListAsync();
        var projects = projectId is { } selected ? choices.Where(p => p.Id == selected).ToList() : choices;
        if (projectId is not null && projects.Count == 0) throw ApiException.NotFound();
        var projectIds = projects.Select(p => p.Id).ToArray();
        var memberships = await db.ProjectMembers.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId) &&
            m.UserId == access.Me.Id && m.RemovedAt == null && m.PrimaryDisciplineId != null)
            .Select(m => new MemberDiscipline(m.ProjectId, m.PrimaryDisciplineId)).ToListAsync();
        var disciplineChoices = await db.Disciplines.AsNoTracking().Where(x => db.ProjectDisciplines.Any(d => projectIds.Contains(d.ProjectId) && d.IsActive && d.DisciplineId == x.Id))
            .OrderBy(x => x.Name).Select(x => new WorkspaceDisciplineChoice(x.Id, x.Name)).ToListAsync();
        var ownerIds = await db.ProjectMembers.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId) && m.RemovedAt == null)
            .Select(m => m.UserId).Distinct().ToListAsync();
        ownerIds.AddRange(projects.Select(p => p.ProjectManagerId));
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).OrderBy(u => u.DisplayName)
            .Select(u => new WorkspaceOwnerChoice(u.Id, u.DisplayName, null)).ToListAsync();
        var rows = new List<WorkspaceCoordinationProject>();
        var evaluatedAt = clock.GetUtcNow();
        var org = await settings.Get(db);
        var today = DateOnly.FromDateTime(Clock.Local(evaluatedAt, org).DateTime);
        var notices = await PersonFacts.ReadNotices(db, owners.Select(person => person.Id).ToArray(), org, today);
        if (notices.Count > 0) owners = owners.Select(person => person with { PersonNotices = notices.GetValueOrDefault(person.Id) }).ToList();
        foreach (var project in projects)
        {
            var localDiscipline = disciplineId is { } globalDiscipline
                ? await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                    d.DisciplineId == globalDiscipline && d.IsActive).Select(d => (Guid?)d.Id).FirstOrDefaultAsync()
                : memberships.FirstOrDefault(m => m.ProjectId == project.Id)?.PrimaryDisciplineId;
            if (disciplineId is not null && localDiscipline is null) continue;
            rows.Add(new(project.Id, project.ProjectNumber, project.Name, localDiscipline,
                await Build(project.Id, localDiscipline, ownerId, from, to, page, pageSize, access.Me.Id, db, evaluatedAt, today,
                    org.CoordinationLookaheadWeeks * 7 - 1, settings, org)));
        }
        await WorkCoordinationReadMetadata.Apply(db, access, org, new(access.Me.Id, today, evaluatedAt), rows.Select(row => row.Data).ToArray());
        return TypedResults.Ok(new WorkspaceCoordinationProjection(evaluatedAt, access.Me.Id, rows, choices, disciplineChoices, owners, projectId is null ? "Workspace" : "Project"));
    }

    static async Task<CoordinationProjection> Build(Guid projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        int? page, int? pageSize,
        Guid actorId, HubDb db, DateTimeOffset evaluatedAt, DateOnly today, int lookaheadDays, SettingsStore settings, OrgSettings org)
    {

        var handoffs = db.Handoffs.AsNoTracking().Where(h => h.ProjectId == projectId);
        if (disciplineId is { } discipline)
            handoffs = handoffs.Where(h => h.SendingDisciplineId == discipline || h.ReceivingDisciplineId == discipline);
        if (ownerId is { } owner)
            handoffs = handoffs.Where(h => h.SendingOwnerId == owner || h.ReceivingOwnerId == owner);
        if (from is { } start)
            handoffs = handoffs.Where(h => (h.PromisedBy ?? h.NeededBy) >= start);
        if (to is { } end)
            handoffs = handoffs.Where(h => (h.PromisedBy ?? h.NeededBy) <= end);
        var handoffRows = await handoffs.OrderBy(h => h.NeededBy).ThenBy(h => h.Key)
            .Select(h => new CoordinationHandoffRow(h.Id, h.Key, h.Title, h.Status, h.NeededBy, h.PromisedBy, h.TargetTaskId, h.TargetDeliverableId,
                h.SendingOwnerId, h.ReceivingOwnerId, h.SendingDisciplineId, h.ReceivingDisciplineId, null, null)).ToListAsync();
        var subject = ownerId ?? actorId;
        var outgoing = handoffRows.Where(h => h.Status != HandoffStatus.Cancelled && h.Status != HandoffStatus.Incorporated &&
            (disciplineId is { } d ? h.SendingDisciplineId == d : h.SendingOwnerId == subject) &&
            (ownerId is null || h.SendingOwnerId == ownerId)).ToList();
        var incoming = handoffRows.Where(h => (h.Status is HandoffStatus.Submitted or HandoffStatus.ClarificationRequested or
            HandoffStatus.Returned or HandoffStatus.Accepted) &&
            (disciplineId is { } d ? h.ReceivingDisciplineId == d : h.ReceivingOwnerId == subject) &&
            (ownerId is null || h.ReceivingOwnerId == ownerId)).ToList();

        // A source handoff can hold a target task and all of its downstream dependants.
        // Materialise the small project graph once so the meeting view can show one blocker
        // group with the exact linked tasks instead of repeating the same source row.
        var edges = await db.Dependencies.AsNoTracking().Where(d => d.ProjectId == projectId && d.DeletedAt == null)
            .Select(d => new DependencyEdge(d.PredecessorTaskId, d.SuccessorTaskId)).ToListAsync();
        var taskKeys = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId)
            .Where(t => t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new TaskRef(t.Id, t.Key)).ToDictionaryAsync(t => t.Id, t => t.Key);
        var successors = edges.GroupBy(e => e.PredecessorTaskId).ToDictionary(g => g.Key, g => g.Select(e => e.SuccessorTaskId).ToArray());
        var blockerGroups = handoffRows.Where(h => h.TargetTaskId is not null &&
            h.Status is not (HandoffStatus.Cancelled or HandoffStatus.Incorporated or HandoffStatus.Accepted) &&
            taskKeys.ContainsKey(h.TargetTaskId.Value)).Select(h =>
        {
            var ids = new HashSet<Guid> { h.TargetTaskId!.Value };
            var frontier = new Queue<Guid>(ids);
            while (frontier.TryDequeue(out var current))
                foreach (var successor in successors.GetValueOrDefault(current, []))
                    if (taskKeys.ContainsKey(successor) && ids.Add(successor)) frontier.Enqueue(successor);
            var active = ids.Where(taskKeys.ContainsKey).OrderBy(id => taskKeys[id]).ToArray();
            return new CoordinationBlockerGroup(h.Id, h.Key, active, active.Select(id => taskKeys[id]).ToArray());
        }).ToList();

        var scopedAssessments = db.ChangeAssessments.AsNoTracking().Where(a => a.ProjectId == projectId);
        if (disciplineId is { } assessmentDiscipline)
            scopedAssessments = scopedAssessments.Where(a =>
                (a.TargetType == "Task" && db.Tasks.Any(t => t.Id == a.TargetId && t.ProjectId == projectId && t.ProjectDisciplineId == assessmentDiscipline)) ||
                (a.TargetType == "Deliverable" && db.Deliverables.Any(d => d.Id == a.TargetId && d.ProjectId == projectId && d.ProjectDisciplineId == assessmentDiscipline)));
        if (ownerId is { } assessmentOwner) scopedAssessments = scopedAssessments.Where(a => a.OwnerId == assessmentOwner);
        var changes = db.ChangeNotices.AsNoTracking().Where(c => c.ProjectId == projectId);
        if (disciplineId is { } changeDiscipline) changes = changes.Where(c => c.ProjectDisciplineId == changeDiscipline ||
            scopedAssessments.Any(a => a.ChangeNoticeId == c.Id));
        if (ownerId is { } changeOwner) changes = changes.Where(c => c.OwnerId == changeOwner ||
            scopedAssessments.Any(a => a.ChangeNoticeId == c.Id));
        // "What changed?" has one definition for every view and export: a notice still Open, or one that still holds
        // Pending Assessment work in this scope. Pending Assessment is counted apart from acknowledgement (FR-DCV-03), and a
        // scoped count travels with the notice's whole-project count so a partial number is never read as the total.
        var changeRows = await changes
            .Where(c => c.Status == ChangeStatus.Open || scopedAssessments.Any(a => a.ChangeNoticeId == c.Id && a.Status == AssessmentStatus.Pending))
            .OrderBy(c => c.AssessmentDueDate).ThenBy(c => c.Key)
            .Select(c => new CoordinationChangeRow(c.Id, c.Key, c.Title, c.Status, c.OwnerId,
                scopedAssessments.Count(a => a.ChangeNoticeId == c.Id && a.Status == AssessmentStatus.Pending),
                scopedAssessments.Count(a => a.ChangeNoticeId == c.Id && a.Status == AssessmentStatus.Pending && a.AcknowledgedAt != null),
                db.ChangeAssessments.Count(a => a.ChangeNoticeId == c.Id && a.Status == AssessmentStatus.Pending), null))
            .ToListAsync();

        var reviews = ReviewIssuedSources.VisiblePackages(db, org.IsFlagOn("register_screens")).AsNoTracking().Where(p => p.ProjectId == projectId);
        if (disciplineId is { } reviewDiscipline) reviews = reviews.Where(p =>
            db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ProjectDisciplineId == reviewDiscipline));
        if (ownerId is { } reviewOwner) reviews = reviews.Where(p => p.CoordinatorId == reviewOwner || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == reviewOwner));
        var reviewRows = await reviews.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Key)
            .Select(p => new CoordinationReviewRow(p.Id, p.Key, p.Title, p.Status, p.CoordinatorId,
                db.DisciplineReviews.Count(a => a.RoundId == p.CurrentRoundId && a.Status != DisciplineReviewStatus.Approved),
                db.ReviewFindings.Count(f => f.RoundId == p.CurrentRoundId && f.Severity == "Blocking" && f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.WithdrawalAcknowledgedBy == null)), null))
            .ToListAsync();

        var linkedIssues = db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId &&
            ReviewIssuedSources.VisibleFindings(db, org.IsFlagOn("register_screens")).Any(f => f.ProjectId == projectId && f.IssueId == i.Id &&
                db.ReviewPackages.Any(p => p.Id == f.PackageId && p.CurrentRoundId == f.RoundId) &&
                (disciplineId == null || f.ProjectDisciplineId == disciplineId || i.ProjectDisciplineId == disciplineId ||
                    db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == disciplineId))));
        if (ownerId is { } issueOwner) linkedIssues = linkedIssues.Where(i => i.OwnerId == issueOwner);
        var issueRows = await linkedIssues.OrderBy(i => i.Key)
            .Select(i => new CoordinationIssueRow(i.Id, i.Key, i.Title, i.Status, i.OwnerId, db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), i.ProjectDisciplineId, null))
            .ToListAsync();

        var uses = db.InputUses.AsNoTracking().Where(u => u.ProjectId == projectId);
        if (ownerId is { } useOwner) uses = uses.Where(u => u.OwnerId == useOwner);
        if (disciplineId is { } useDiscipline)
        {
            var targetIds = db.Tasks.Where(t => t.ProjectId == projectId && t.ProjectDisciplineId == useDiscipline).Select(t => t.Id)
                .Concat(db.Deliverables.Where(d => d.ProjectId == projectId && d.ProjectDisciplineId == useDiscipline).Select(d => d.Id));
            uses = uses.Where(u => targetIds.Contains(u.TargetId));
        }
        var useRows = await uses.OrderByDescending(u => u.AdoptedAt).ThenBy(u => u.Id)
            .Select(u => new CoordinationInputUseRow(u.Id, u.TargetType, u.TargetId, u.OwnerId, u.SourceIdentity, u.SourceRevisionId, u.IntendedUse, u.AdoptedAt, u.AdoptedBy, u.RowVersion,
                u.TargetType == ItemType.Task ? db.Tasks.Where(t => t.ProjectId == projectId && t.Id == u.TargetId).Select(t => t.Key).FirstOrDefault()
                    : u.TargetType == ItemType.Deliverable ? db.Deliverables.Where(d => d.ProjectId == projectId && d.Id == u.TargetId).Select(d => d.Key).FirstOrDefault() : null,
                u.TargetType == ItemType.Task ? db.Tasks.Where(t => t.ProjectId == projectId && t.Id == u.TargetId).Select(t => t.Name).FirstOrDefault()
                    : u.TargetType == ItemType.Deliverable ? db.Deliverables.Where(d => d.ProjectId == projectId && d.Id == u.TargetId).Select(d => d.Name).FirstOrDefault() : null,
                db.SourceRevisions.Where(r => r.ProjectId == projectId && r.Id == u.SourceRevisionId).Select(r => r.Url).FirstOrDefault(),
                db.SourceRevisions.Where(r => r.ProjectId == projectId && r.Id == u.SourceRevisionId).Select(r => r.SourceKey).FirstOrDefault(),
                db.SourceRevisions.Where(r => r.ProjectId == projectId && r.Id == u.SourceRevisionId).Select(r => r.Revision).FirstOrDefault(), null))
            .ToListAsync();

        var paged = page.HasValue || pageSize.HasValue;
        var (pg, size) = Http.Paging(page, pageSize);
        int PageNumber(int count) => paged ? Math.Min(pg, Math.Max(1, (count + size - 1) / size)) : 1;
        IReadOnlyList<T> PageRows<T>(IReadOnlyList<T> rows) =>
            paged ? rows.Skip((PageNumber(rows.Count) - 1) * size).Take(size).ToArray() : rows;

        // FR-DCV-01/03 context comes from the submission and readiness registers. A pending
        // check is an actionable failing check for an unissued package; no readiness is inferred.
        var submissionFirst = from ?? today;
        var submissionLast = to ?? today.AddDays(lookaheadDays);
        var packages = db.SubmissionPackages.AsNoTracking().Where(p => p.ProjectId == projectId &&
            p.Status != SubmissionStatus.Issued && p.Status != SubmissionStatus.Superseded && p.Status != SubmissionStatus.Cancelled &&
            p.TargetDate >= submissionFirst && p.TargetDate <= submissionLast);
        if (disciplineId is { } submissionDiscipline)
            packages = packages.Where(p => db.SubmissionManifestItems.Any(m => m.PackageId == p.Id && m.ManifestVersion == p.ManifestVersion &&
                db.Deliverables.Any(d => d.Id == m.DeliverableId && d.ProjectDisciplineId == submissionDiscipline)));
        if (ownerId is { } submissionOwner)
            packages = packages.Where(p => p.CoordinatorId == submissionOwner || db.SubmissionChecks.Any(c => c.PackageId == p.Id &&
                c.ManifestVersion == p.ManifestVersion && c.OwnerId == submissionOwner));
        var packageRows = await packages.OrderBy(p => p.TargetDate).ThenBy(p => p.Key).ToListAsync();
        var submissionRows = new List<SubmissionContextRow>();
        var submissionReadiness = new Dictionary<Guid, SubmissionReadinessResult>();
        foreach (var package in packageRows)
        {
            var readiness = await SubmissionReadiness.Evaluate(db, package);
            if (org.IsFlagOn("screen_patterns")) submissionReadiness[package.Id] = readiness;
            var effectiveStatus = package.Status is SubmissionStatus.Checking or SubmissionStatus.Ready
                ? readiness.Ready ? SubmissionStatus.Ready : SubmissionStatus.Checking : package.Status;
            submissionRows.Add(new(package.Id, package.Key, package.Title, package.Status, effectiveStatus, package.TargetDate,
                package.CoordinatorId, readiness.Blockers.Select(b => new SubmissionFailingCheck(b.SourceId, b.Kind, b.Code, b.Message, b.SourcePath)).ToArray()));
        }

        var staffing = db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == projectId && c.Category == "Capacity" &&
            c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled && c.NeededBy >= submissionFirst && c.NeededBy <= submissionLast);
        if (disciplineId is { } staffingDiscipline)
            staffing = staffing.Where(c => (c.TargetType == ItemType.Task && db.Tasks.Any(t => t.Id == c.TargetId && t.ProjectDisciplineId == staffingDiscipline)) ||
                (c.TargetType == ItemType.Deliverable && db.Deliverables.Any(d => d.Id == c.TargetId && d.ProjectDisciplineId == staffingDiscipline)));
        if (ownerId is { } staffingOwner) staffing = staffing.Where(c => c.AffectedOwnerId == staffingOwner || c.RemovalOwnerId == staffingOwner);
        var staffingRows = await staffing.OrderBy(c => c.NeededBy).ThenBy(c => c.Key)
            .Select(c => new StaffingContextRow(c.Id, c.Key, c.TargetType, c.TargetId, c.Description, c.NeededBy, c.State, c.AffectedOwnerId, c.RemovalOwnerId, null))
            .ToListAsync();
        var startability = await Startability(projectId, disciplineId, ownerId, from, to, today,
            lookaheadDays, evaluatedAt, db, settings);
        var changeIds = changeRows.Select(c => c.Id).ToArray();
        var assessmentTargets = await scopedAssessments.Where(a => changeIds.Contains(a.ChangeNoticeId))
            .Select(a => new CoordinationChangeTarget(a.ChangeNoticeId, a.TargetType, a.TargetId,
                (a.TargetType == ItemType.Task && db.Tasks.Any(t => t.Id == a.TargetId && t.ProjectId == projectId && t.DeletedAt == null)) ||
                    (a.TargetType == ItemType.Deliverable && db.Deliverables.Any(d => d.Id == a.TargetId && d.ProjectId == projectId && d.DeletedAt == null))))
            .ToListAsync();
        var changeTargets = assessmentTargets.Where(a => a.Available).ToList();
        var unavailableChangeTargets = assessmentTargets.Where(a => !a.Available)
            .GroupBy(a => a.ChangeNoticeId).Select(g => new CoordinationUnavailableTarget(g.Key, g.Count())).ToList();
        var visibleChanges = PageRows(changeRows).Select(c => c.Id).ToHashSet();
        var visibleHandoffs = PageRows(blockerGroups).Select(g => g.HandoffId).ToHashSet();
        var linkedActions = new List<CoordinationLinkedAction>();
        var linkedActionsTotal = 0;
        var blockerIds = blockerGroups.Select(g => g.HandoffId).ToArray();
        if (blockerIds.Length > 0 || changeIds.Length > 0)
        {
            var links = await db.ItemLinks.AsNoTracking().Where(l => l.ProjectId == projectId && l.DeletedAt == null &&
                l.SourceType == ItemType.Action && ((l.TargetType == ItemType.Handoff && blockerIds.Contains(l.TargetId)) ||
                    (l.TargetType == ItemType.ChangeNotice && changeIds.Contains(l.TargetId))))
                .Select(l => new ActionLink(l.SourceId, l.TargetType, l.TargetId)).ToListAsync();
            var linkedIds = links.Select(l => l.SourceId).Distinct().ToArray();
            var actions = await db.Actions.AsNoTracking().Where(a => a.ProjectId == projectId &&
                (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) &&
                linkedIds.Contains(a.Id))
                .OrderBy(a => a.DueDate).ThenBy(a => a.Key).ToListAsync();
            foreach (var link in links.DistinctBy(l => (l.SourceId, l.TargetType, l.TargetId)))
                if (actions.FirstOrDefault(a => a.Id == link.SourceId) is { } action)
                {
                    linkedActionsTotal++;
                    if (!paged || link.TargetType == ItemType.ChangeNotice && visibleChanges.Contains(link.TargetId) ||
                        link.TargetType == ItemType.Handoff && visibleHandoffs.Contains(link.TargetId))
                        linkedActions.Add(new(action.Id, action.Key, action.Text, action.Status, action.DueDate, link.TargetType, link.TargetId));
                }
        }

        var linksProject = org.IsFlagOn("register_screens") ? await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId) : null;
        var readinessLinks = linksProject is not null ? await HandoffConsequencesEndpoints.ReadBatch(db, linksProject,
            PageRows(handoffRows).Concat(PageRows(outgoing)).Concat(PageRows(incoming)).Select(h => h.Id)
                .Concat(PageRows(blockerGroups).Select(g => g.HandoffId)), today) : null;
        CoordinationHandoffRow HandoffLinks(CoordinationHandoffRow h) => readinessLinks is null ? h : h with { ReadinessLinks = readinessLinks.GetValueOrDefault(h.Id) };
        var projection = new CoordinationProjection(
            EvaluatedAt: evaluatedAt,
            Handoffs: PageRows(handoffRows).Select(HandoffLinks).ToArray(),
            Outgoing: PageRows(outgoing).Select(HandoffLinks).ToArray(),
            Incoming: PageRows(incoming).Select(HandoffLinks).ToArray(),
            OutgoingPage: PageNumber(outgoing.Count),
            IncomingPage: PageNumber(incoming.Count),
            OutgoingTotal: outgoing.Count,
            IncomingTotal: incoming.Count,
            HandoffPage: PageNumber(handoffRows.Count),
            HandoffPageSize: paged ? size : handoffRows.Count,
            ChangeTargets: changeTargets.Where(c => !paged || visibleChanges.Contains(c.ChangeNoticeId)).ToArray(),
            UnavailableChangeTargets: unavailableChangeTargets.Where(c => !paged || visibleChanges.Contains(c.ChangeNoticeId)).ToArray(),
            ChangeTargetsTotal: changeTargets.Count,
            UnavailableChangeTargetsTotal: unavailableChangeTargets.Count,
            Reviews: PageRows(reviewRows),
            ReviewsPage: PageNumber(reviewRows.Count),
            ReviewsPageSize: paged ? size : reviewRows.Count,
            Uses: PageRows(useRows),
            UsesTotal: useRows.Count,
            UsesPage: PageNumber(useRows.Count),
            UsesPageSize: paged ? size : useRows.Count,
            LinkedIssues: PageRows(issueRows),
            LinkedIssuesTotal: issueRows.Count,
            LinkedIssuesPage: PageNumber(issueRows.Count),
            LinkedIssuesPageSize: paged ? size : issueRows.Count,
            HandoffsTotal: handoffRows.Count,
            Changes: PageRows(changeRows),
            ChangesTotal: changeRows.Count,
            ChangesPage: PageNumber(changeRows.Count),
            ChangesPageSize: paged ? size : changeRows.Count,
            ReviewsTotal: reviewRows.Count,
            BlockerGroups: PageRows(blockerGroups).Select(g => readinessLinks is null ? g : g with { ReadinessLinks = readinessLinks.GetValueOrDefault(g.HandoffId) }).ToArray(),
            BlockerGroupsTotal: blockerGroups.Count,
            Startability: PageRows(startability.Rows),
            StartabilityTotal: startability.Rows.Count,
            StartabilityPage: PageNumber(startability.Rows.Count),
            StartabilityPageSize: paged ? size : startability.Rows.Count,
            StartabilityFrom: startability.From,
            StartabilityTo: startability.To,
            StartabilityReadyTotal: startability.Rows.Count(r => r.State == ReadinessState.Ready),
            LinkedActions: linkedActions,
            LinkedActionsTotal: linkedActionsTotal,
            UpcomingSubmissions: PageRows(submissionRows),
            UpcomingSubmissionsTotal: submissionRows.Count,
            UpcomingSubmissionsPage: PageNumber(submissionRows.Count),
            StaffingConflicts: PageRows(staffingRows),
            StaffingConflictsTotal: staffingRows.Count,
            StaffingConflictsPage: PageNumber(staffingRows.Count),
            BlockerGroupsPage: PageNumber(blockerGroups.Count),
            Page: paged ? pg : 1,
            PageSize: paged ? size : 0);
        if (!org.IsFlagOn("screen_patterns")) return projection;
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId);
        var contextReader = new RecordContextReader(db, project, org);
        var packageIds = projection.UpcomingSubmissions.Select(p => p.Id).ToArray();
        var currentPackages = packageRows.Where(p => packageIds.Contains(p.Id)).ToDictionary(p => p.Id);
        var packageChecks = await db.SubmissionChecks.AsNoTracking().Where(c => c.ProjectId == projectId && packageIds.Contains(c.PackageId) &&
            db.SubmissionPackages.Any(p => p.ProjectId == projectId && p.Id == c.PackageId && p.ManifestVersion == c.ManifestVersion)).ToListAsync();
        foreach (var row in projection.UpcomingSubmissions)
        {
            var package = currentPackages[row.Id];
            contextReader.SeedSubmission(package, packageChecks.Where(c => c.PackageId == package.Id && c.ManifestVersion == package.ManifestVersion).ToArray(),
                submissionReadiness[package.Id], row.EffectiveStatus);
        }
        var contexts = new Dictionary<(string, Guid), RecordContextView?>();
        async Task<RecordContextView?> Context(string kind, Guid id)
        {
            if (contexts.TryGetValue((kind, id), out var cached)) return cached;
            return contexts[(kind, id)] = kind is "InputUse" or "ReviewPackage"
                ? await contextReader.CoordinationSummary(kind, id) : await contextReader.ReadLoaded(kind, id);
        }
        async Task<IReadOnlyList<T>> Enrich<T>(IReadOnlyList<T> rows, Func<T, (string Kind, Guid Id)> identity, Func<T, RecordContextView?, T> attach)
        {
            var result = new List<T>();
            foreach (var row in rows) { var (kind, id) = identity(row); result.Add(attach(row, await Context(kind, id))); }
            return result;
        }
        return projection with
        {
            Handoffs = await Enrich(projection.Handoffs, r => (RecordContextKind.Handoff, r.Id), (r, c) => r with { Context = c }),
            Outgoing = await Enrich(projection.Outgoing, r => (RecordContextKind.Handoff, r.Id), (r, c) => r with { Context = c }),
            Incoming = await Enrich(projection.Incoming, r => (RecordContextKind.Handoff, r.Id), (r, c) => r with { Context = c }),
            BlockerGroups = await Enrich(projection.BlockerGroups, r => (RecordContextKind.Handoff, r.HandoffId), (r, c) => r with { Context = c }),
            Changes = await Enrich(projection.Changes, r => (RecordContextKind.ChangeNotice, r.Id), (r, c) => r with { Context = c }),
            Reviews = await Enrich(projection.Reviews, r => ("ReviewPackage", r.Id), (r, c) => r with { Context = c }),
            LinkedIssues = await Enrich(projection.LinkedIssues, r => (RecordContextKind.Issue, r.Id), (r, c) => r with { Context = c }),
            Uses = await Enrich(projection.Uses, r => ("InputUse", r.Id), (r, c) => r with { Context = c }),
            LinkedActions = await Enrich(projection.LinkedActions, r => (RecordContextKind.Action, r.Id), (r, c) => r with { Context = c }),
            UpcomingSubmissions = await Enrich(projection.UpcomingSubmissions, r => (RecordContextKind.SubmissionPackage, r.Id), (r, c) => r with { Context = c }),
            StaffingConflicts = await Enrich(projection.StaffingConflicts, r => (RecordContextKind.WorkConstraint, r.Id), (r, c) => r with { Context = c }),
            Startability = await Enrich(projection.Startability, r => (r.TargetType, r.TargetId), (r, c) => r with { Context = c }),
        };
    }

    public sealed partial record StartabilityRow(Guid Id, string TargetType, Guid TargetId, string Key, string Name,
        DateOnly? DueDate, Guid? OwnerId, Guid DisciplineId, string State, string[] Blocked, string[] Unknown,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    sealed record StartabilityWindow(DateOnly From, DateOnly To, List<StartabilityRow> Rows);

    // Re-evaluate stored assessments against their live source checks inside the same snapshot
    // as the other coordination rows. An absent assessment is never reported as Ready.
    static async Task<StartabilityWindow> Startability(Guid projectId, Guid? disciplineId, Guid? ownerId,
        DateOnly? from, DateOnly? to, DateOnly today, int lookaheadDays, DateTimeOffset evaluatedAt,
        HubDb db, SettingsStore settings)
    {
        // A to-only filter may point into the past. Keep the historical handoff view
        // valid and evaluate readiness in the lookahead window ending on that date.
        var first = from ?? (to is { } upper && upper < today
            ? DateOnly.FromDayNumber(Math.Max(0, upper.DayNumber - lookaheadDays)) : today);
        Check.That(to.HasValue || first.DayNumber <= DateOnly.MaxValue.DayNumber - lookaheadDays, "from", "error.invalid");
        var last = to ?? first.AddDays(lookaheadDays);
        Check.That(last >= first, "to", "error.invalid");
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && t.DeletedAt == null &&
            t.DueDate >= first && t.DueDate <= last && t.Status != TaskStatuses.Complete &&
            t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold &&
            (disciplineId == null || t.ProjectDisciplineId == disciplineId) &&
            (ownerId == null || t.AssigneeId == ownerId))
            .Select(t => new StartableWork(t.Id, t.Key, t.Name, t.DueDate, t.AssigneeId, t.ProjectDisciplineId))
            .ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && d.DeletedAt == null &&
            d.DueDate >= first && d.DueDate <= last && d.Status != DeliverableStatus.Issued &&
            d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled &&
            d.Status != DeliverableStatus.OnHold &&
            (disciplineId == null || d.ProjectDisciplineId == disciplineId) &&
            (ownerId == null || d.OwnerId == ownerId))
            .Select(d => new StartableWork(d.Id, d.Key, d.Name, d.DueDate, d.OwnerId, d.ProjectDisciplineId))
            .ToListAsync();
        var taskIds = tasks.Select(t => t.Id).ToArray();
        var deliverableIds = deliverables.Select(d => d.Id).ToArray();
        var assessments = await db.ReadinessAssessments.AsNoTracking().Where(a => a.ProjectId == projectId &&
            ((a.TargetType == "Task" && taskIds.Contains(a.TargetId)) ||
             (a.TargetType == "Deliverable" && deliverableIds.Contains(a.TargetId))))
            .ToListAsync();
        var assessmentIds = assessments.Select(a => a.Id).ToArray();
        var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == projectId &&
            assessmentIds.Contains(c.AssessmentId)).ToListAsync();
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId);
        var rows = new List<StartabilityRow>();
        foreach (var assessment in assessments)
        {
            var result = await ReadinessEndpoints.EvaluateCurrent(db, project, assessment.TargetType,
                assessment.TargetId, assessment, checks.Where(c => c.AssessmentId == assessment.Id).ToList(),
                today, evaluatedAt, settings);
            if (assessment.TargetType == "Task" && tasks.SingleOrDefault(t => t.Id == assessment.TargetId) is { } task)
                rows.Add(new(assessment.Id, "Task", task.Id, task.Key, task.Name, task.DueDate,
                    task.OwnerId, task.DisciplineId, result.State, result.Blocked, result.Unknown));
            else if (assessment.TargetType == "Deliverable" && deliverables.SingleOrDefault(d => d.Id == assessment.TargetId) is { } deliverable)
                rows.Add(new(assessment.Id, "Deliverable", deliverable.Id, deliverable.Key, deliverable.Name,
                    deliverable.DueDate, deliverable.OwnerId, deliverable.DisciplineId,
                    result.State, result.Blocked, result.Unknown));
        }
        return new(first, last, rows.OrderBy(r => r.DueDate).ThenBy(r => r.Key).ToList());
    }
}
