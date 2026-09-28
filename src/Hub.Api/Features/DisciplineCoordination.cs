using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Packet 030 read projection. Counts and rows are produced from the same permission-checked,
/// filtered source query; callers never have to infer a total from a capped register page.
public static class DisciplineCoordinationEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/discipline-coordination", Get);
        api.MapGet("/discipline-coordination", Workspace);
    }

    static async Task<object> Get(Guid projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        Access access, HubDb db, TimeProvider clock, SettingsStore settings)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await access.Project(projectId, track: false);
        var org = await settings.Get(db);
        return await Build(projectId, disciplineId, ownerId, from, to, access.Me.Id, db, clock.GetUtcNow(),
            clock.Today(org), org.CoordinationLookaheadWeeks * 7 - 1, settings);
    }

    static async Task<object> Workspace(Guid? projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        string? scopeKind, string? scopeProjectIds, Guid? scopeWorkspaceId,
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
        var choices = await visible.OrderBy(p => p.ProjectNumber).Select(p => new { p.Id, p.ProjectNumber, p.Name, p.ProjectManagerId }).ToListAsync();
        var projects = projectId is { } selected ? choices.Where(p => p.Id == selected).ToList() : choices;
        if (projectId is not null && projects.Count == 0) throw ApiException.NotFound();
        var projectIds = projects.Select(p => p.Id).ToArray();
        var memberships = await db.ProjectMembers.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId) &&
            m.UserId == access.Me.Id && m.RemovedAt == null && m.PrimaryDisciplineId != null)
            .Select(m => new { m.ProjectId, m.PrimaryDisciplineId }).ToListAsync();
        var disciplineChoices = await db.ProjectDisciplines.AsNoTracking().Where(d => projectIds.Contains(d.ProjectId) && d.IsActive)
            .Select(d => new { Id = d.DisciplineId, d.Discipline!.Name }).Distinct().OrderBy(d => d.Name).ToListAsync();
        var ownerIds = await db.ProjectMembers.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId) && m.RemovedAt == null)
            .Select(m => m.UserId).Distinct().ToListAsync();
        ownerIds.AddRange(projects.Select(p => p.ProjectManagerId));
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.DisplayName }).ToListAsync();
        var rows = new List<object>();
        var evaluatedAt = clock.GetUtcNow();
        var org = await settings.Get(db);
        var today = clock.Today(org);
        foreach (var project in projects)
        {
            var localDiscipline = disciplineId is { } globalDiscipline
                ? await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                    d.DisciplineId == globalDiscipline && d.IsActive).Select(d => (Guid?)d.Id).FirstOrDefaultAsync()
                : memberships.FirstOrDefault(m => m.ProjectId == project.Id)?.PrimaryDisciplineId;
            if (disciplineId is not null && localDiscipline is null) continue;
            rows.Add(new { project.Id, project.ProjectNumber, project.Name, DisciplineId = localDiscipline,
                Data = await Build(project.Id, localDiscipline, ownerId, from, to, access.Me.Id, db, evaluatedAt, today,
                    org.CoordinationLookaheadWeeks * 7 - 1, settings) });
        }
        return new { EvaluatedAt = evaluatedAt, ActorId = access.Me.Id, Projects = rows, ProjectChoices = choices, Disciplines = disciplineChoices, Owners = owners,
            Scope = projectId is null ? "Workspace" : "Project" };
    }

    static async Task<object> Build(Guid projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        Guid actorId, HubDb db, DateTimeOffset evaluatedAt, DateOnly today, int lookaheadDays, SettingsStore settings)
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
            .Select(h => new { h.Id, h.Key, h.Title, h.Status, h.NeededBy, h.PromisedBy, h.TargetTaskId, h.TargetDeliverableId,
                h.SendingOwnerId, h.ReceivingOwnerId, h.SendingDisciplineId, h.ReceivingDisciplineId }).ToListAsync();
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
            .Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId }).ToListAsync();
        var taskKeys = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId)
            .Where(t => t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.Id, t.Key }).ToDictionaryAsync(t => t.Id, t => t.Key);
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
            return new { HandoffId = h.Id, HandoffKey = h.Key, TaskIds = active,
                TaskKeys = active.Select(id => taskKeys[id]).ToArray() };
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
        var changeRows = await changes.OrderBy(c => c.AssessmentDueDate).ThenBy(c => c.Key)
            .Select(c => new { c.Id, c.Key, c.Title, c.Status, c.OwnerId, PendingAssessments = scopedAssessments.Count(a => a.ChangeNoticeId == c.Id && a.Status == AssessmentStatus.Pending) })
            .ToListAsync();

        var reviews = db.ReviewPackages.AsNoTracking().Where(p => p.ProjectId == projectId);
        if (disciplineId is { } reviewDiscipline) reviews = reviews.Where(p =>
            db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ProjectDisciplineId == reviewDiscipline));
        if (ownerId is { } reviewOwner) reviews = reviews.Where(p => p.CoordinatorId == reviewOwner || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == reviewOwner));
        var reviewRows = await reviews.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Key)
            .Select(p => new { p.Id, p.Key, p.Title, p.Status, p.CoordinatorId,
                OutstandingDisciplines = db.DisciplineReviews.Count(a => a.RoundId == p.CurrentRoundId && a.Status != DisciplineReviewStatus.Approved),
                BlockingFindings = db.ReviewFindings.Count(f => f.RoundId == p.CurrentRoundId && f.Severity == "Blocking" && f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.WithdrawalAcknowledgedBy == null)) })
            .ToListAsync();

        var linkedIssues = db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId &&
            db.ReviewFindings.Any(f => f.ProjectId == projectId && f.IssueId == i.Id &&
                db.ReviewPackages.Any(p => p.Id == f.PackageId && p.CurrentRoundId == f.RoundId) &&
                (disciplineId == null || f.ProjectDisciplineId == disciplineId || i.ProjectDisciplineId == disciplineId)));
        if (ownerId is { } issueOwner) linkedIssues = linkedIssues.Where(i => i.OwnerId == issueOwner);
        var issueRows = await linkedIssues.OrderBy(i => i.Key)
            .Select(i => new { i.Id, i.Key, i.Title, i.Status, i.OwnerId, OwnerName = db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), i.ProjectDisciplineId })
            .ToListAsync();

        var uses = db.InputUses.AsNoTracking().Where(u => u.ProjectId == projectId);
        if (ownerId is { } useOwner) uses = uses.Where(u => u.OwnerId == useOwner);
        if (disciplineId is { } useDiscipline)
        {
            var targetIds = db.Tasks.Where(t => t.ProjectId == projectId && t.ProjectDisciplineId == useDiscipline).Select(t => t.Id)
                .Concat(db.Deliverables.Where(d => d.ProjectId == projectId && d.ProjectDisciplineId == useDiscipline).Select(d => d.Id));
            uses = uses.Where(u => targetIds.Contains(u.TargetId));
        }
        var useRows = await uses.OrderByDescending(u => u.AdoptedAt).ThenBy(u => u.Id).ToListAsync();
        var startability = await Startability(projectId, disciplineId, ownerId, from, to, today,
            lookaheadDays, evaluatedAt, db, settings);
        var changeIds = changeRows.Select(c => c.Id).ToArray();
        var assessmentTargets = await scopedAssessments.Where(a => changeIds.Contains(a.ChangeNoticeId))
            .Select(a => new { a.ChangeNoticeId, a.TargetType, a.TargetId,
                Available = (a.TargetType == ItemType.Task && db.Tasks.Any(t => t.Id == a.TargetId && t.ProjectId == projectId && t.DeletedAt == null)) ||
                    (a.TargetType == ItemType.Deliverable && db.Deliverables.Any(d => d.Id == a.TargetId && d.ProjectId == projectId && d.DeletedAt == null)) })
            .ToListAsync();
        var changeTargets = assessmentTargets.Where(a => a.Available).ToList();
        var unavailableChangeTargets = assessmentTargets.Where(a => !a.Available)
            .GroupBy(a => a.ChangeNoticeId).Select(g => new { ChangeNoticeId = g.Key, Count = g.Count() }).ToList();
        var linkedActions = new List<object>();
        var blockerIds = blockerGroups.Select(g => g.HandoffId).ToArray();
        if (blockerIds.Length > 0 || changeIds.Length > 0)
        {
            var links = await db.ItemLinks.AsNoTracking().Where(l => l.ProjectId == projectId && l.DeletedAt == null &&
                l.SourceType == ItemType.Action && ((l.TargetType == ItemType.Handoff && blockerIds.Contains(l.TargetId)) ||
                    (l.TargetType == ItemType.ChangeNotice && changeIds.Contains(l.TargetId))))
                .Select(l => new { l.SourceId, l.TargetType, l.TargetId }).ToListAsync();
            var linkedIds = links.Select(l => l.SourceId).Distinct().ToArray();
            var actions = await db.Actions.AsNoTracking().Where(a => a.ProjectId == projectId &&
                (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress) &&
                linkedIds.Contains(a.Id))
                .OrderBy(a => a.DueDate).ThenBy(a => a.Key)
                .Select(a => new { a.Id, a.Key, a.Text, a.Status, a.DueDate }).ToListAsync();
            foreach (var link in links.DistinctBy(l => (l.SourceId, l.TargetType, l.TargetId)))
                if (actions.FirstOrDefault(a => a.Id == link.SourceId) is { } action)
                    linkedActions.Add(new { action.Id, action.Key, action.Text, action.Status, action.DueDate,
                        SourceType = link.TargetType, SourceId = link.TargetId });
        }

        return new
        {
            EvaluatedAt = evaluatedAt,
            Handoffs = handoffRows,
            Outgoing = outgoing,
            Incoming = incoming,
            Changes = changeRows,
            ChangeTargets = changeTargets,
            UnavailableChangeTargets = unavailableChangeTargets,
            Reviews = reviewRows,
            LinkedIssues = issueRows,
            Uses = useRows,
            UsesTotal = useRows.Count,
            LinkedIssuesTotal = issueRows.Count,
            HandoffsTotal = handoffRows.Count,
            ChangesTotal = changeRows.Count,
            ReviewsTotal = reviewRows.Count,
            BlockerGroups = blockerGroups,
            Startability = startability.Rows,
            StartabilityFrom = startability.From,
            StartabilityTo = startability.To,
            StartabilityReadyTotal = startability.Rows.Count(r => r.State == ReadinessState.Ready),
            LinkedActions = linkedActions,
        };
    }

    public sealed record StartabilityRow(Guid Id, string TargetType, Guid TargetId, string Key, string Name,
        DateOnly? DueDate, Guid? OwnerId, Guid DisciplineId, string State, string[] Blocked, string[] Unknown);
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
            .Select(t => new { t.Id, t.Key, t.Name, t.DueDate, OwnerId = t.AssigneeId, DisciplineId = t.ProjectDisciplineId })
            .ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && d.DeletedAt == null &&
            d.DueDate >= first && d.DueDate <= last && d.Status != DeliverableStatus.Issued &&
            d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled &&
            d.Status != DeliverableStatus.OnHold &&
            (disciplineId == null || d.ProjectDisciplineId == disciplineId) &&
            (ownerId == null || d.OwnerId == ownerId))
            .Select(d => new { d.Id, d.Key, d.Name, d.DueDate, d.OwnerId, DisciplineId = d.ProjectDisciplineId })
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
