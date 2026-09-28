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
        Access access, HubDb db, TimeProvider clock)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await access.Project(projectId, track: false);
        return await Build(projectId, disciplineId, ownerId, from, to, access.Me.Id, db, clock.GetUtcNow());
    }

    static async Task<object> Workspace(Guid? projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        string? scopeKind, string? scopeProjectIds, Guid? scopeWorkspaceId,
        Access access, HubDb db, TimeProvider clock)
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
        foreach (var project in projects)
        {
            var localDiscipline = disciplineId is { } globalDiscipline
                ? await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == project.Id &&
                    d.DisciplineId == globalDiscipline && d.IsActive).Select(d => (Guid?)d.Id).FirstOrDefaultAsync()
                : memberships.FirstOrDefault(m => m.ProjectId == project.Id)?.PrimaryDisciplineId;
            if (disciplineId is not null && localDiscipline is null) continue;
            rows.Add(new { project.Id, project.ProjectNumber, project.Name, DisciplineId = localDiscipline,
                Data = await Build(project.Id, localDiscipline, ownerId, from, to, access.Me.Id, db, evaluatedAt) });
        }
        return new { EvaluatedAt = evaluatedAt, ActorId = access.Me.Id, Projects = rows, ProjectChoices = choices, Disciplines = disciplineChoices, Owners = owners,
            Scope = projectId is null ? "Workspace" : "Project" };
    }

    static async Task<object> Build(Guid projectId, Guid? disciplineId, Guid? ownerId, DateOnly? from, DateOnly? to,
        Guid actorId, HubDb db, DateTimeOffset evaluatedAt)
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

        return new
        {
            EvaluatedAt = evaluatedAt,
            Handoffs = handoffRows,
            Outgoing = outgoing,
            Incoming = incoming,
            Changes = changeRows,
            Reviews = reviewRows,
            LinkedIssues = issueRows,
            Uses = useRows,
            UsesTotal = useRows.Count,
            LinkedIssuesTotal = issueRows.Count,
            HandoffsTotal = handoffRows.Count,
            ChangesTotal = changeRows.Count,
            ReviewsTotal = reviewRows.Count,
            BlockerGroups = blockerGroups,
        };
    }
}
