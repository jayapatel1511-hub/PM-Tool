using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// My Work (§13.10, FR-MYW-01) and My Staff (§13.19, FR-ASG-05/06, ASG-08..ASG-10). Both read the same permitted,
/// materialised state, so a person's My Staff row reconciles with their My Work.
public static class MyWorkEndpoints
{
    static readonly string[] Live = [ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold];
    static readonly string[] OpenDecision = [DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/work", Work);
        api.MapGet("/staff", Staff);
        api.MapGet("/staff/{userId:guid}/assignments", Assignments);
    }

    /// The projects whose work the viewer may see for this person: every visible project for themselves, their direct
    /// reports (Supervisors), Executives and Admins; for a PM only the projects they manage (FR-009, AC-MYW-04).
    static async Task<List<Guid>> Scope(HubDb db, Access access, Guid personId, Guid? personSupervisorId)
    {
        var visible = access.VisibleProjects().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        var a = access.Actor;
        if (Permissions.ViewPersonWork(a, personId, personSupervisorId)) return await visible.Select(p => p.Id).ToListAsync();
        var managed = await visible.Where(p => (p.ProjectManagerId == a.Id || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == a.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))
            && db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == personId && m.RemovedAt == null)).Select(p => p.Id).ToListAsync();
        if (managed.Count == 0) throw ApiException.Forbidden("perm.person_work");
        return managed;
    }

    static async Task<object> Work(Guid? userId, Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == (userId ?? me.Id)) ?? throw ApiException.NotFound();
        var pids = await Scope(db, access, person.Id, person.SupervisorId);
        var s = await store.Get(db);
        var today = clock.Today(s);
        var now = clock.GetUtcNow();
        var pid = person.Id;

        // The person's collaborations are few: reading them first lets "assignee or collaborator" use an index on each side.
        var collab = await db.Collaborators.AsNoTracking().Where(c => c.UserId == pid).Select(c => c.TaskId).ToListAsync();
        var myTasks = db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled
            && (t.AssigneeId == pid || collab.Contains(t.Id)));
        var tasks = await TaskQueries.Rows(db, TaskQueries.Sort(myTasks, null));
        var reviewTasks = await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.ReviewerId == pid
            && (t.Status == TaskStatuses.ReadyForReview || t.Status == TaskStatuses.InReview)).OrderBy(t => t.ReviewRequestedAt));
        var reviewDeliverables = await DeliverableEndpoints.Rows(db, db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.ReviewerId == pid && d.Status == DeliverableStatus.InReview));
        var deliverables = await DeliverableEndpoints.Rows(db, db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.OwnerId == pid
            && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled));

        // Waiting on others and blocking others need the other people and tasks involved (AC-MYW-02, AC-MYW-03).
        var states = await db.TaskStates.AsNoTracking().Where(x => myTasks.Any(t => t.Id == x.TaskId)).ToListAsync();
        var blockerOwners = states.SelectMany(x => JsonSerializer.Deserialize<List<Blocker>>(x.BlockedBy ?? "[]", JsonOpts.Web) ?? []).Select(b => b.OwnerId).OfType<Guid>();
        var successorIds = states.SelectMany(x => x.BlockingTaskIds).Distinct().ToList();
        var successors = await db.Tasks.AsNoTracking().Where(t => successorIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.AssigneeId, t.ProjectId }).ToListAsync();
        var peopleIds = blockerOwners.Concat(successors.Select(x => x.AssigneeId).OfType<Guid>()).Distinct().ToList();
        var people = await db.Users.Where(u => peopleIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var decisions = await db.Decisions.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && OpenDecision.Contains(d.Status) && (d.OwnerUserId == pid || d.RequestedById == pid))
            .OrderBy(d => d.RequiredByDate).Select(d => new
            {
                d.Id, d.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First(), d.Key, d.Subject, d.Status, d.RequiredByDate,
                d.ImpactLevel, Role = d.OwnerUserId == pid ? "Owner" : "Requester",
                IsOverdue = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsOverdue).FirstOrDefault(),
                IsDueSoon = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsDueSoon).FirstOrDefault(),
            }).ToListAsync();

        // MTG-01: actions the person owns, and their disciplines' actions as lead (the PM's when a discipline has no lead).
        var actions = await MeetingEndpoints.Rows(db, db.Actions.AsNoTracking().Where(a => pids.Contains(a.ProjectId)
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)
            && (a.OwnerUserId == pid || (a.OwnerType == ActionOwnerType.Discipline && db.ProjectDisciplines.Any(d => d.Id == a.OwnerDisciplineId
                && (d.LeadUserId == pid || (d.LeadUserId == null && db.Projects.Any(p => p.Id == a.ProjectId && p.ProjectManagerId == pid))))))), today);

        var projects = await db.Projects.AsNoTracking().Where(p => pids.Contains(p.Id)
                && (p.ProjectManagerId == pid || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == pid && m.RemovedAt == null) || db.Follows.Any(f => f.ProjectId == p.Id && f.UserId == pid)))
            .OrderBy(p => p.ProjectNumber).Select(p => new
            {
                p.Id, p.ProjectNumber, p.Name, p.Status, p.HealthOverride, p.HealthOverrideExpiresAt,
                Roles = db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == pid && m.RemovedAt == null).Select(m => m.Roles).FirstOrDefault(),
                Leads = db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.LeadUserId == pid).Select(d => d.Discipline!.Name).ToList(),
                Follow = db.Follows.Where(f => f.ProjectId == p.Id && f.UserId == pid).Select(f => new { f.Level, f.Source }).FirstOrDefault(),
                Computed = db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => x.ComputedHealth).FirstOrDefault(),
                Next = db.Milestones.Where(m => m.ProjectId == p.Id && !m.IsComplete && !m.IsCancelled && m.Date >= today).OrderBy(m => m.Date)
                    .Select(m => new { m.Id, m.Key, m.Name, m.Date }).FirstOrDefault(),
            }).ToListAsync();
        var myProjectIds = projects.Select(p => p.Id).ToList();
        var milestones = await db.Milestones.AsNoTracking().Where(m => myProjectIds.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date >= today && m.Date <= today.AddDays(30))
            .OrderBy(m => m.Date).Select(m => new
            {
                m.Id, m.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).First(), m.Key, m.Name, m.MilestoneType, m.Date,
                Status = db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault(),
            }).ToListAsync();
        var since = now.AddDays(-14);
        var completed = await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.AssigneeId == pid && t.Status == TaskStatuses.Complete && t.CompletedAt >= since)
            .OrderByDescending(t => t.CompletedAt));
        var attention = await EvaluationEndpoints.Items(db, db.Attention.AsNoTracking().Where(a => pids.Contains(a.ProjectId) && a.RouteToUserIds.Contains(pid)), now, false, limit: 100);

        return new
        {
            Person = new { person.Id, person.DisplayName, person.JobTitle, person.IsActive }, ReadOnly = pid != me.Id, Today = today,
            Attention = attention, Tasks = tasks, Reviews = new { Tasks = reviewTasks, Deliverables = reviewDeliverables }, Deliverables = deliverables,
            People = people, Successors = successors.Select(x => new { x.Id, x.Key, x.Name, x.Status, x.DueDate, AssigneeName = x.AssigneeId is { } a ? people.GetValueOrDefault(a) : null }),
            Decisions = decisions, Actions = actions,
            Projects = projects.Select(p => new
            {
                p.Id, p.ProjectNumber, p.Name, p.Status, Roles = (p.Roles ?? []).Concat(p.Leads.Count > 0 ? ["DisciplineLead"] : []).Distinct(), p.Leads, p.Follow,
                ComputedHealth = p.Status == ProjectStatus.Active ? p.Computed ?? Health.Grey : Health.Grey,
                ReportedHealth = p.Status == ProjectStatus.Active && p.HealthOverride is { } ho && p.HealthOverrideExpiresAt > now ? ho : p.Status == ProjectStatus.Active ? p.Computed ?? Health.Grey : Health.Grey,
                NextMilestone = p.Next,
            }),
            Milestones = milestones, Completed = completed,
        };
    }

    // ---------- My Staff (§13.19) ----------

    sealed record PersonRow(Guid Id, string DisplayName, string? JobTitle, Guid? OfficeId, bool IsActive, Guid? SupervisorId);

    static async Task<(List<PersonRow> People, List<Guid> Visible)> StaffScope(HubDb db, Access access, string? scope, bool? showInactive)
    {
        var a = access.Actor;
        Access.Demand(Permissions.ViewStaff(a)); // AC-ASG-07: Supervisors, Executives and Admins
        var all = scope == "all";
        if (all && !Permissions.ViewAllStaff(a)) throw ApiException.Forbidden("perm.all_staff");
        var q = db.Users.AsNoTracking().Where(u => all || u.SupervisorId == a.Id); // ASG-08: direct reports, read at request time
        if (showInactive != true) q = q.Where(u => u.IsActive);
        var people = await q.OrderBy(u => u.DisplayName).Select(u => new PersonRow(u.Id, u.DisplayName, u.JobTitle, u.OfficeId, u.IsActive, u.SupervisorId)).ToListAsync();
        var visible = await access.VisibleProjects().Where(p => Live.Contains(p.Status)).Select(p => p.Id).ToListAsync(); // ASG-09: only work the viewer may see
        return (people, visible);
    }

    static async Task<object> Staff(string? scope, bool? showInactive, Guid? officeId, Guid? disciplineId, Guid? projectId, string? indicator,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (people, visible) = await StaffScope(db, access, scope, showInactive);
        var s = await store.Get(db);
        var today = clock.Today(s);
        if (officeId is { } o) people = people.Where(p => p.OfficeId == o).ToList();
        var ids = people.Select(p => p.Id).ToList();
        var members = await db.ProjectMembers.AsNoTracking().Where(m => ids.Contains(m.UserId) && m.RemovedAt == null && visible.Contains(m.ProjectId))
            .Select(m => new { m.UserId, m.ProjectId, m.Roles, Discipline = db.ProjectDisciplines.Where(d => d.Id == m.PrimaryDisciplineId).Select(d => (Guid?)d.DisciplineId).FirstOrDefault() }).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => d.LeadUserId != null && ids.Contains(d.LeadUserId.Value) && visible.Contains(d.ProjectId) && d.IsActive)
            .Select(d => new { UserId = d.LeadUserId!.Value, d.ProjectId, d.DisciplineId }).ToListAsync();
        if (projectId is { } pj) { var inProject = members.Where(m => m.ProjectId == pj).Select(m => m.UserId).ToHashSet(); people = people.Where(p => inProject.Contains(p.Id)).ToList(); }
        if (disciplineId is { } dj)
        {
            var inDiscipline = members.Where(m => m.Discipline == dj).Select(m => m.UserId).Concat(leads.Where(l => l.DisciplineId == dj).Select(l => l.UserId)).ToHashSet();
            people = people.Where(p => inDiscipline.Contains(p.Id)).ToList();
        }
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.AssigneeId != null && ids.Contains(t.AssigneeId.Value) && visible.Contains(t.ProjectId)
                && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { UserId = t.AssigneeId!.Value, State = db.TaskStates.Where(x => x.TaskId == t.Id).Select(x => new { x.IsOverdue, x.IsBlocked, x.IsBlocking }).FirstOrDefault() }).ToListAsync();
        var reviews = await db.Tasks.AsNoTracking().Where(t => t.ReviewerId != null && ids.Contains(t.ReviewerId.Value) && visible.Contains(t.ProjectId)
                && (t.Status == TaskStatuses.ReadyForReview || t.Status == TaskStatuses.InReview))
            .Select(t => new { UserId = t.ReviewerId!.Value, Stalled = db.TaskStates.Where(x => x.TaskId == t.Id).Select(x => x.IsReviewStalled).FirstOrDefault() }).ToListAsync();
        var horizon = today.AddDays(14);
        var dels = await db.Deliverables.AsNoTracking().Where(d => d.OwnerId != null && ids.Contains(d.OwnerId.Value) && visible.Contains(d.ProjectId)
                && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled && d.DueDate >= today && d.DueDate <= horizon)
            .Select(d => d.OwnerId!.Value).ToListAsync();
        var lastActivity = await db.ActivityLog.AsNoTracking().Where(a => a.ActorUserId != null && ids.Contains(a.ActorUserId.Value))
            .GroupBy(a => a.ActorUserId!.Value).Select(g => new { UserId = g.Key, At = g.Max(x => x.OccurredAt) }).ToDictionaryAsync(x => x.UserId, x => x.At);

        var rows = people.Select(p =>
        {
            var mine = members.Where(m => m.UserId == p.Id).ToList();
            var led = leads.Where(l => l.UserId == p.Id).ToList();
            var t = tasks.Where(x => x.UserId == p.Id).ToList();
            var projectIds = mine.Select(m => m.ProjectId).Concat(led.Select(l => l.ProjectId)).Distinct().Count();
            return new
            {
                p.Id, p.DisplayName, p.JobTitle, p.OfficeId, p.IsActive, Projects = projectIds,
                Roles = new
                {
                    PM = mine.Count(m => m.Roles.Contains(ProjectRole.PM)), DL = led.Count, Team = mine.Count(m => m.Roles.Contains(ProjectRole.TeamMember)),
                    Reviewer = mine.Count(m => m.Roles.Contains(ProjectRole.Reviewer)), Viewer = mine.Count(m => m.Roles.Contains(ProjectRole.Viewer)),
                },
                Open = t.Count, Overdue = t.Count(x => x.State?.IsOverdue == true), Blocked = t.Count(x => x.State?.IsBlocked == true),
                Blocking = t.Count(x => x.State?.IsBlocking == true), Reviews = reviews.Count(r => r.UserId == p.Id), ReviewsStalled = reviews.Count(r => r.UserId == p.Id && r.Stalled),
                DeliverablesDue = dels.Count(x => x == p.Id), LastActivityAt = lastActivity.TryGetValue(p.Id, out var at) ? at : (DateTimeOffset?)null,
            };
        }).ToList();
        rows = indicator switch
        {
            "overdue" => rows.Where(r => r.Overdue > 0).ToList(),
            "blocked" => rows.Where(r => r.Blocked > 0).ToList(),
            "blocking" => rows.Where(r => r.Blocking > 0).ToList(),
            "reviews" => rows.Where(r => r.Reviews > 0).ToList(),
            _ => rows,
        };
        rows = [.. rows.OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Blocked).ThenBy(r => r.DisplayName)]; // FR-017 default sort
        return new
        {
            Scope = scope == "all" ? "all" : "direct", CanSeeAll = Permissions.ViewAllStaff(access.Actor),
            Tiles = new
            {
                Staff = rows.Count, Assignments = rows.Sum(r => r.Projects), WithOverdue = rows.Count(r => r.Overdue > 0), WithBlocked = rows.Count(r => r.Blocked > 0),
                ReviewsStalled = rows.Sum(r => r.ReviewsStalled),
            },
            People = rows,
        };
    }

    static async Task<object> Assignments(Guid userId, string? scope, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (people, visible) = await StaffScope(db, access, scope, showInactive: true);
        var person = people.FirstOrDefault(p => p.Id == userId) ?? throw ApiException.Forbidden("perm.supervisor");
        var s = await store.Get(db);
        var today = clock.Today(s);
        var now = clock.GetUtcNow();
        var rows = await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == userId && m.RemovedAt == null && visible.Contains(m.ProjectId))
            .Select(m => new
            {
                MemberId = m.Id, m.ProjectId, m.Roles, m.AddedAt,
                Project = db.Projects.Where(p => p.Id == m.ProjectId).Select(p => new { p.ProjectNumber, p.Name, p.Status, p.Visibility, p.ProjectManagerId, p.HealthOverride, p.HealthOverrideExpiresAt, p.AllowViewerComments }).First(),
                PrimaryDiscipline = db.ProjectDisciplines.Where(d => d.Id == m.PrimaryDisciplineId).Select(d => d.Discipline!.Name).FirstOrDefault(),
                Leads = db.ProjectDisciplines.Where(d => d.ProjectId == m.ProjectId && d.LeadUserId == userId).Select(d => d.Discipline!.Name).ToList(),
                Computed = db.ProjectStates.Where(x => x.ProjectId == m.ProjectId).Select(x => x.ComputedHealth).FirstOrDefault(),
                Open = db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                Overdue = db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue)),
                Next = db.Tasks.Where(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.DueDate != null)
                    .OrderBy(t => t.DueDate).Select(t => new { t.Id, t.Key, t.Name, t.DueDate }).FirstOrDefault(),
            }).ToListAsync();
        var a = access.Actor;
        return rows.OrderBy(r => r.Project.ProjectNumber).Select(r =>
        {
            var ctx = new ProjectContext(r.ProjectId, r.Project.Status, r.Project.Visibility, r.Project.ProjectManagerId, r.Project.AllowViewerComments, new HashSet<string>(), new HashSet<Guid>(), null);
            // ASG-10: supervisors remove only Team Member roles; everything else about the team stays with the PM.
            var canRemove = Permissions.StaffOnProject(a, ctx, person.SupervisorId).Ok && r.Roles.All(x => x == ProjectRole.TeamMember) && r.Leads.Count == 0;
            var reported = r.Project.Status == ProjectStatus.Active && r.Project.HealthOverride is { } ho && r.Project.HealthOverrideExpiresAt > now ? ho : r.Project.Status == ProjectStatus.Active ? r.Computed ?? Health.Grey : Health.Grey;
            return new
            {
                r.MemberId, r.ProjectId, r.Project.ProjectNumber, r.Project.Name, r.Project.Status, ReportedHealth = reported, Roles = r.Roles.Concat(r.Leads.Count > 0 ? ["DisciplineLead"] : []),
                r.Leads, r.PrimaryDiscipline, r.AddedAt, r.Open, r.Overdue, NextDue = r.Next, CanRemove = canRemove,
            };
        }).ToList();
    }
}
