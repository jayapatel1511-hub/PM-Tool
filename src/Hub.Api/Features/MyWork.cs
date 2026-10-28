using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// My Work (§13.10, FR-MYW-01) and My Staff (§13.19, FR-ASG-05/06, ASG-08..ASG-10). Both read the same permitted,
/// materialised state, so a person's My Staff row reconciles with their My Work.
public static class MyWorkEndpoints
{
    static readonly string[] Live = [ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold];
    static readonly string[] OpenDecision = [DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred];

    /// One person's My Work: their tasks, reviews, deliverables, decisions, actions and projects, with what they wait on and what waits on them (§13.10).
    public sealed record MyWorkView(MyWorkPerson Person, bool ReadOnly, DateOnly Today, EvaluationEndpoints.AttentionList Attention, IReadOnlyList<TaskQueries.Row> Tasks, MyWorkReviews Reviews,
        IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables, IReadOnlyDictionary<Guid, string> People, IReadOnlyList<MyWorkSuccessor> Successors, IReadOnlyList<MyWorkDecision> Decisions,
        IReadOnlyList<MeetingEndpoints.MeetingActionRow> Actions, IReadOnlyList<MyWorkProject> Projects, IReadOnlyList<MyWorkMilestone> Milestones, IReadOnlyList<TaskQueries.Row> Completed);
    public sealed record MyWorkPerson(Guid Id, string DisplayName, string? JobTitle, bool IsActive);
    /// What the person is asked to review: tasks ready for or in review, and deliverables in review.
    public sealed record MyWorkReviews(IReadOnlyList<TaskQueries.Row> Tasks, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables);
    /// A task that waits on one of the person's tasks, with its assignee's name.
    public sealed record MyWorkSuccessor(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, string? AssigneeName);
    public sealed record MyWorkDecision(Guid Id, Guid ProjectId, string ProjectNumber, string Key, string Subject, string Status, DateOnly RequiredByDate, string ImpactLevel, string Role, bool IsOverdue, bool IsDueSoon);
    /// A project the person manages, belongs to, leads a discipline on or follows, with their roles, follow level and next milestone.
    public sealed record MyWorkProject(Guid Id, string ProjectNumber, string Name, string Status, IReadOnlyList<string> Roles, IReadOnlyList<string> Leads, MyWorkFollow? Follow,
        string ComputedHealth, string ReportedHealth, MyWorkNextMilestone? NextMilestone);
    public sealed record MyWorkFollow(string Level, string Source);
    public sealed record MyWorkNextMilestone(Guid Id, string Key, string Name, DateOnly Date);
    public sealed record MyWorkMilestone(Guid Id, Guid ProjectId, string ProjectNumber, string Key, string Name, string MilestoneType, DateOnly Date, string? Status);

    /// My Staff: the viewer's reports with their work counts, and the tiles over them (§13.19).
    public sealed record MyStaffList(string Scope, bool CanSeeAll, MyStaffTiles Tiles, IReadOnlyList<MyStaffPerson> People);
    public sealed record MyStaffTiles(int Staff, int Assignments, int WithOverdue, int WithBlocked, int ReviewsStalled);
    public sealed record MyStaffPerson(Guid Id, string DisplayName, string? JobTitle, Guid? OfficeId, bool IsActive, int Projects, MyStaffRoles Roles, int Open, int Overdue, int Blocked, int Blocking,
        int Reviews, int ReviewsStalled, int DeliverablesDue, DateTimeOffset? LastActivityAt);
    /// The person's memberships by role: projects as PM, team member, reviewer and viewer, and the disciplines they lead (DL).
    public sealed record MyStaffRoles(int PM, int DL, int Team, int Reviewer, int Viewer);
    /// One of a person's project assignments, with their open work on it and whether the viewer may remove them.
    public sealed record MyStaffAssignment(Guid MemberId, int RowVersion, Guid ProjectId, string ProjectNumber, string Name, string Status, string ReportedHealth, IReadOnlyList<string> Roles,
        IReadOnlyList<string> Leads, string? PrimaryDiscipline, DateTimeOffset AddedAt, int Open, int Overdue, MyStaffNextDue? NextDue, bool CanRemove);
    public sealed record MyStaffNextDue(Guid Id, string Key, string Name, DateOnly DueDate);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/work", Work);
        api.MapGet("/staff", Staff);
        api.MapGet("/staff/{userId:guid}/assignments", Assignments);
    }

    /// The projects whose work the viewer may see for this person: every visible project for themselves, their direct
    /// reports (Supervisors), Executives and Admins; for a PM only the projects they manage (FR-009, AC-MYW-04).
    static async Task<List<Guid>> Scope(HubDb db, Access access, Guid personId, Guid? personSupervisorId, OrgSettings settings)
    {
        var visible = access.VisibleProjects().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        var a = access.Actor;
        if (settings.IsFlagOn("org_structure") && (a.Supervisor || await PeopleScope.LeadsUnit(db, a))
            && (await PeopleScope.Load(db, a, settings.IncludeIndirectReports)).Contains(personId))
            return await visible.Select(p => p.Id).ToListAsync();
        if (Permissions.ViewPersonWork(a, personId, personSupervisorId)) return await visible.Select(p => p.Id).ToListAsync();
        var managed = await visible.Where(p => (p.ProjectManagerId == a.Id || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == a.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))
            && db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == personId && m.RemovedAt == null)).Select(p => p.Id).ToListAsync();
        if (managed.Count == 0) throw ApiException.Forbidden("perm.person_work");
        return managed;
    }

    static async Task<Ok<MyWorkView>> Work(Guid? userId, Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == (userId ?? me.Id)) ?? throw ApiException.NotFound();
        var s = await store.Get(db);
        var pids = await Scope(db, access, person.Id, person.SupervisorId, s);
        var today = clock.Today(s);
        var now = clock.GetUtcNow();
        var pid = person.Id;

        // The person's collaborations are few: reading them first lets "assignee or collaborator" use an index on each side.
        var collab = await db.Collaborators.AsNoTracking().Where(c => c.UserId == pid).Select(c => c.TaskId).ToListAsync();
        var myTasks = db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled
            && (t.AssigneeId == pid || collab.Contains(t.Id)));
        var tasks = await TaskQueries.Rows(db, TaskQueries.Sort(myTasks, null), access.Actor);
        var reviewTasks = await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.ReviewerId == pid
            && (t.Status == TaskStatuses.ReadyForReview || t.Status == TaskStatuses.InReview)).OrderBy(t => t.ReviewRequestedAt), access.Actor);
        var reviewDeliverables = await DeliverableRows(db, db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.ReviewerId == pid && d.Status == DeliverableStatus.InReview));
        var deliverables = await DeliverableRows(db, db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.OwnerId == pid
            && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled));

        // Waiting on others and blocking others need the other people and tasks involved (AC-MYW-02, AC-MYW-03).
        var states = await db.TaskStates.AsNoTracking().Where(x => myTasks.Any(t => t.Id == x.TaskId)).ToListAsync();
        var blockerOwners = states.SelectMany(x => JsonSerializer.Deserialize<List<Blocker>>(x.BlockedBy ?? "[]", JsonOpts.Web) ?? []).Select(b => b.OwnerId).OfType<Guid>();
        var successorIds = states.SelectMany(x => x.BlockingTaskIds).Distinct().ToList();
        var successors = await db.Tasks.AsNoTracking().Where(t => successorIds.Contains(t.Id))
            .Select(t => new SuccessorFacts(t.Id, t.Key, t.Name, t.Status, t.DueDate, t.AssigneeId)).ToListAsync();
        var peopleIds = blockerOwners.Concat(successors.Select(x => x.AssigneeId).OfType<Guid>()).Distinct().ToList();
        var people = await db.Users.Where(u => peopleIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);

        var decisions = await db.Decisions.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && OpenDecision.Contains(d.Status) && (d.OwnerUserId == pid || d.RequestedById == pid))
            .OrderBy(d => d.RequiredByDate).Select(d => new MyWorkDecision(d.Id, d.ProjectId, db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First(), d.Key, d.Subject, d.Status,
                d.RequiredByDate, d.ImpactLevel, d.OwnerUserId == pid ? "Owner" : "Requester",
                db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsOverdue).FirstOrDefault(),
                db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsDueSoon).FirstOrDefault())).ToListAsync();

        // MTG-01: actions the person owns, and their disciplines' actions as lead (the PM's when a discipline has no lead).
        var actions = await MeetingEndpoints.Rows(db, db.Actions.AsNoTracking().Where(a => pids.Contains(a.ProjectId)
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)
            && (a.OwnerUserId == pid || (a.OwnerType == ActionOwnerType.Discipline && db.ProjectDisciplines.Any(d => d.Id == a.OwnerDisciplineId
                && (d.LeadUserId == pid || (d.LeadUserId == null && db.Projects.Any(p => p.Id == a.ProjectId && p.ProjectManagerId == pid))))))), today);

        var projects = await db.Projects.AsNoTracking().Where(p => pids.Contains(p.Id)
                && (p.ProjectManagerId == pid || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == pid && m.RemovedAt == null) || db.Follows.Any(f => f.ProjectId == p.Id && f.UserId == pid)))
            .OrderBy(p => p.ProjectNumber).Select(p => new ProjectFacts(p.Id, p.ProjectNumber, p.Name, p.Status, p.HealthOverride, p.HealthOverrideExpiresAt,
                db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == pid && m.RemovedAt == null).Select(m => m.Roles).FirstOrDefault(),
                db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.LeadUserId == pid).Select(d => d.Discipline!.Name).ToList(),
                db.Follows.Where(f => f.ProjectId == p.Id && f.UserId == pid).Select(f => new MyWorkFollow(f.Level, f.Source)).FirstOrDefault(),
                db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => x.ComputedHealth).FirstOrDefault(),
                db.Milestones.Where(m => m.ProjectId == p.Id && !m.IsComplete && !m.IsCancelled && m.Date >= today).OrderBy(m => m.Date)
                    .Select(m => new MyWorkNextMilestone(m.Id, m.Key, m.Name, m.Date!.Value)).FirstOrDefault())).ToListAsync();
        var myProjectIds = projects.Select(p => p.Id).ToList();
        var milestones = await db.Milestones.AsNoTracking().Where(m => myProjectIds.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date >= today && m.Date <= today.AddDays(30))
            .OrderBy(m => m.Date).Select(m => new MyWorkMilestone(m.Id, m.ProjectId, db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).First(), m.Key, m.Name, m.MilestoneType,
                m.Date!.Value, db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault())).ToListAsync();
        var since = now.AddDays(-14);
        var completed = await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.AssigneeId == pid && t.Status == TaskStatuses.Complete && t.CompletedAt >= since)
            .OrderByDescending(t => t.CompletedAt), access.Actor);
        var attention = await EvaluationEndpoints.Items(db, db.Attention.AsNoTracking().Where(a => pids.Contains(a.ProjectId) && a.RouteToUserIds.Contains(pid)), now, false, limit: 100);

        return TypedResults.Ok(new MyWorkView(new MyWorkPerson(person.Id, person.DisplayName, person.JobTitle, person.IsActive), pid != me.Id, today,
            attention, tasks, new MyWorkReviews(reviewTasks, reviewDeliverables), deliverables,
            people, [.. successors.Select(x => new MyWorkSuccessor(x.Id, x.Key, x.Name, x.Status, x.DueDate, x.AssigneeId is { } a ? people.GetValueOrDefault(a) : null))],
            decisions, actions,
            [.. projects.Select(p => new MyWorkProject(p.Id, p.ProjectNumber, p.Name, p.Status, [.. (p.Roles ?? []).Concat(p.Leads.Count > 0 ? ["DisciplineLead"] : []).Distinct()], p.Leads, p.Follow,
                p.Status == ProjectStatus.Active ? p.Computed ?? Health.Grey : Health.Grey,
                p.Status == ProjectStatus.Active && p.HealthOverride is { } ho && p.HealthOverrideExpiresAt > now ? ho : p.Status == ProjectStatus.Active ? p.Computed ?? Health.Grey : Health.Grey,
                p.Next))],
            milestones, completed));
    }

    // The deliverable rows are already DeliverableRow records; their helper still lists them as objects.
    static async Task<List<DeliverableEndpoints.DeliverableRow>> DeliverableRows(HubDb db, IQueryable<Deliverable> q) => [.. (await DeliverableEndpoints.Rows(db, q)).Cast<DeliverableEndpoints.DeliverableRow>()];

    sealed record SuccessorFacts(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, Guid? AssigneeId);
    sealed record ProjectFacts(Guid Id, string ProjectNumber, string Name, string Status, string? HealthOverride, DateTimeOffset? HealthOverrideExpiresAt, List<string>? Roles, List<string> Leads,
        MyWorkFollow? Follow, string? Computed, MyWorkNextMilestone? Next);

    // ---------- My Staff (§13.19) ----------

    sealed record PersonRow(Guid Id, string DisplayName, string? JobTitle, Guid? OfficeId, bool IsActive, Guid? SupervisorId);

    static async Task<(List<PersonRow> People, List<Guid> Visible)> StaffScope(HubDb db, Access access, string? scope, bool? showInactive, SettingsStore? store = null)
    {
        var a = access.Actor;
        var settings = store == null ? new OrgSettings() : await store.Get(db);
        var structure = settings.IsFlagOn("org_structure");
        var unitLeader = structure && await PeopleScope.LeadsUnit(db, a);
        Access.Demand(Permissions.ViewStaff(a).Ok || unitLeader ? Allow.Yes : Permissions.ViewStaff(a));
        var all = scope == "all";
        if (all && !Permissions.ViewAllStaff(a)) throw ApiException.Forbidden("perm.all_staff");
        var q = db.Users.AsNoTracking().Where(u => all || u.SupervisorId == a.Id);
        if (!all && structure)
        {
            HashSet<Guid>? scoped = null;
            if (scope?.StartsWith("unit:") == true)
            {
                Check.That(Guid.TryParse(scope[5..], out _), "scope", "error.invalid");
                var unit = Guid.Parse(scope[5..]);
                Check.That(await db.Set<OrgUnit>().AnyAsync(u => u.Id == unit && u.LeaderId == a.Id && u.ClosedOn == null), "scope", "error.invalid");
                scoped = await PeopleScope.LoadUnit(db, a, unit);
            }
            else if (scope == "reports")
            {
                Check.That(settings.IncludeIndirectReports, "scope", "error.invalid");
                scoped = await PeopleScope.Load(db, a, true, includeLedUnits: false);
            }
            else if (unitLeader && !a.Supervisor)
                scoped = await PeopleScope.Load(db, a, settings.IncludeIndirectReports);
            if (scoped != null) q = db.Users.AsNoTracking().Where(u => scoped.Contains(u.Id));
        }
        if (showInactive != true) q = q.Where(u => u.IsActive);
        var people = await q.OrderBy(u => u.DisplayName).Select(u => new PersonRow(u.Id, u.DisplayName, u.JobTitle, u.OfficeId, u.IsActive, u.SupervisorId)).ToListAsync();
        var visible = await access.VisibleProjects().Where(p => Live.Contains(p.Status)).Select(p => p.Id).ToListAsync(); // ASG-09: only work the viewer may see
        return (people, visible);
    }

    static async Task<Ok<MyStaffList>> Staff(string? scope, bool? showInactive, Guid? officeId, Guid? disciplineId, Guid? projectId, string? indicator,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (people, visible) = await StaffScope(db, access, scope, showInactive, store);
        var s = await store.Get(db);
        var today = clock.Today(s);
        if (officeId is { } o) people = people.Where(p => p.OfficeId == o).ToList();
        var ids = people.Select(p => p.Id).ToList();
        var members = await db.ProjectMembers.AsNoTracking().Where(m => ids.Contains(m.UserId) && m.RemovedAt == null && visible.Contains(m.ProjectId))
            .Select(m => new MemberFacts(m.UserId, m.ProjectId, m.Roles, db.ProjectDisciplines.Where(d => d.Id == m.PrimaryDisciplineId).Select(d => (Guid?)d.DisciplineId).FirstOrDefault())).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => d.LeadUserId != null && ids.Contains(d.LeadUserId.Value) && visible.Contains(d.ProjectId) && d.IsActive)
            .Select(d => new LeadFacts(d.LeadUserId!.Value, d.ProjectId, d.DisciplineId)).ToListAsync();
        if (projectId is { } pj) { var inProject = members.Where(m => m.ProjectId == pj).Select(m => m.UserId).ToHashSet(); people = people.Where(p => inProject.Contains(p.Id)).ToList(); }
        if (disciplineId is { } dj)
        {
            var inDiscipline = members.Where(m => m.Discipline == dj).Select(m => m.UserId).Concat(leads.Where(l => l.DisciplineId == dj).Select(l => l.UserId)).ToHashSet();
            people = people.Where(p => inDiscipline.Contains(p.Id)).ToList();
        }
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.AssigneeId != null && ids.Contains(t.AssigneeId.Value) && visible.Contains(t.ProjectId)
                && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new OpenTaskFacts(t.AssigneeId!.Value, db.TaskStates.Where(x => x.TaskId == t.Id).Select(x => new TaskFlags(x.IsOverdue, x.IsBlocked, x.IsBlocking)).FirstOrDefault())).ToListAsync();
        var reviews = await db.Tasks.AsNoTracking().Where(t => t.ReviewerId != null && ids.Contains(t.ReviewerId.Value) && visible.Contains(t.ProjectId)
                && (t.Status == TaskStatuses.ReadyForReview || t.Status == TaskStatuses.InReview))
            .Select(t => new ReviewFacts(t.ReviewerId!.Value, db.TaskStates.Where(x => x.TaskId == t.Id).Select(x => x.IsReviewStalled).FirstOrDefault())).ToListAsync();
        var horizon = today.AddDays(14);
        var dels = await db.Deliverables.AsNoTracking().Where(d => d.OwnerId != null && ids.Contains(d.OwnerId.Value) && visible.Contains(d.ProjectId)
                && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled && d.DueDate >= today && d.DueDate <= horizon)
            .Select(d => d.OwnerId!.Value).ToListAsync();
        // ASG-09: only activity the viewer may see, by the same source boundaries as project history (private time and calendar).
        var lastActivity = await ActivityEndpoints.Visible(db, access, visible).Where(a => a.ActorUserId != null && ids.Contains(a.ActorUserId.Value))
            .GroupBy(a => a.ActorUserId!.Value).Select(g => new ActivityFacts(g.Key, g.Max(x => x.OccurredAt))).ToDictionaryAsync(x => x.UserId, x => x.At);

        var rows = people.Select(p =>
        {
            var mine = members.Where(m => m.UserId == p.Id).ToList();
            var led = leads.Where(l => l.UserId == p.Id).ToList();
            var t = tasks.Where(x => x.UserId == p.Id).ToList();
            var projectIds = mine.Select(m => m.ProjectId).Concat(led.Select(l => l.ProjectId)).Distinct().Count();
            return new MyStaffPerson(p.Id, p.DisplayName, p.JobTitle, p.OfficeId, p.IsActive, projectIds,
                new MyStaffRoles(mine.Count(m => m.Roles.Contains(ProjectRole.PM)), led.Count, mine.Count(m => m.Roles.Contains(ProjectRole.TeamMember)),
                    mine.Count(m => m.Roles.Contains(ProjectRole.Reviewer)), mine.Count(m => m.Roles.Contains(ProjectRole.Viewer))),
                t.Count, t.Count(x => x.State?.IsOverdue == true), t.Count(x => x.State?.IsBlocked == true),
                t.Count(x => x.State?.IsBlocking == true), reviews.Count(r => r.UserId == p.Id), reviews.Count(r => r.UserId == p.Id && r.Stalled),
                dels.Count(x => x == p.Id), lastActivity.TryGetValue(p.Id, out var at) ? at : null);
        }).ToList();
        rows = indicator switch
        {
            "overdue" => rows.Where(r => r.Overdue > 0).ToList(),
            "blocked" => rows.Where(r => r.Blocked > 0).ToList(),
            "blocking" => rows.Where(r => r.Blocking > 0).ToList(),
            "reviews" => rows.Where(r => r.Reviews > 0).ToList(),
            "reviewsStalled" => rows.Where(r => r.ReviewsStalled > 0).ToList(),
            _ => rows,
        };
        rows = [.. rows.OrderByDescending(r => r.Overdue).ThenByDescending(r => r.Blocked).ThenBy(r => r.DisplayName)]; // FR-017 default sort
        return TypedResults.Ok(new MyStaffList(scope ?? "direct", Permissions.ViewAllStaff(access.Actor),
            new MyStaffTiles(rows.Count, rows.Sum(r => r.Projects), rows.Count(r => r.Overdue > 0), rows.Count(r => r.Blocked > 0), rows.Sum(r => r.ReviewsStalled)),
            rows));
    }

    sealed record MemberFacts(Guid UserId, Guid ProjectId, List<string> Roles, Guid? Discipline);
    sealed record LeadFacts(Guid UserId, Guid ProjectId, Guid DisciplineId);
    sealed record TaskFlags(bool IsOverdue, bool IsBlocked, bool IsBlocking);
    sealed record OpenTaskFacts(Guid UserId, TaskFlags? State);
    sealed record ReviewFacts(Guid UserId, bool Stalled);
    sealed record ActivityFacts(Guid UserId, DateTimeOffset At);

    static async Task<Ok<List<MyStaffAssignment>>> Assignments(Guid userId, string? scope, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (people, visible) = await StaffScope(db, access, scope, showInactive: true, store);
        var person = people.FirstOrDefault(p => p.Id == userId) ?? throw ApiException.Forbidden("perm.supervisor");
        var s = await store.Get(db);
        var today = clock.Today(s);
        var now = clock.GetUtcNow();
        var rows = await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == userId && m.RemovedAt == null && visible.Contains(m.ProjectId))
            .Select(m => new AssignmentFacts(m.Id, m.RowVersion, m.ProjectId, m.Roles, m.AddedAt,
                db.Projects.Where(p => p.Id == m.ProjectId).Select(p => new AssignedProject(p.ProjectNumber, p.Name, p.Status, p.Visibility, p.ProjectManagerId, p.HealthOverride, p.HealthOverrideExpiresAt, p.AllowViewerComments)).First(),
                db.ProjectDisciplines.Where(d => d.Id == m.PrimaryDisciplineId).Select(d => d.Discipline!.Name).FirstOrDefault(),
                db.ProjectDisciplines.Where(d => d.ProjectId == m.ProjectId && d.LeadUserId == userId).Select(d => d.Discipline!.Name).ToList(),
                db.ProjectStates.Where(x => x.ProjectId == m.ProjectId).Select(x => x.ComputedHealth).FirstOrDefault(),
                db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue)),
                db.Tasks.Where(t => t.ProjectId == m.ProjectId && t.AssigneeId == userId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.DueDate != null)
                    .OrderBy(t => t.DueDate).Select(t => new MyStaffNextDue(t.Id, t.Key, t.Name, t.DueDate!.Value)).FirstOrDefault())).ToListAsync();
        var a = access.Actor;
        return TypedResults.Ok(rows.OrderBy(r => r.Project.ProjectNumber).Select(r =>
        {
            var ctx = new ProjectContext(r.ProjectId, r.Project.Status, r.Project.Visibility, r.Project.ProjectManagerId, r.Project.AllowViewerComments, new HashSet<string>(), new HashSet<Guid>(), null);
            // ASG-10: supervisors remove only Team Member roles; everything else about the team stays with the PM.
            var canRemove = Permissions.StaffOnProject(a, ctx, person.SupervisorId).Ok && r.Roles.All(x => x == ProjectRole.TeamMember) && r.Leads.Count == 0;
            var reported = r.Project.Status == ProjectStatus.Active && r.Project.HealthOverride is { } ho && r.Project.HealthOverrideExpiresAt > now ? ho : r.Project.Status == ProjectStatus.Active ? r.Computed ?? Health.Grey : Health.Grey;
            return new MyStaffAssignment(r.MemberId, r.MemberRowVersion, r.ProjectId, r.Project.ProjectNumber, r.Project.Name, r.Project.Status, reported, [.. r.Roles.Concat(r.Leads.Count > 0 ? ["DisciplineLead"] : [])],
                r.Leads, r.PrimaryDiscipline, r.AddedAt, r.Open, r.Overdue, r.Next, canRemove);
        }).ToList());
    }

    sealed record AssignedProject(string ProjectNumber, string Name, string Status, string Visibility, Guid ProjectManagerId, string? HealthOverride, DateTimeOffset? HealthOverrideExpiresAt, bool AllowViewerComments);
    sealed record AssignmentFacts(Guid MemberId, int MemberRowVersion, Guid ProjectId, List<string> Roles, DateTimeOffset AddedAt, AssignedProject Project, string? PrimaryDiscipline, List<string> Leads,
        string? Computed, int Open, int Overdue, MyStaffNextDue? Next);
}
