using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Tasks and review (§12.5, §13.3, §13.4, §15.6, §15.7).
public static class TaskEndpoints
{
    public sealed record CreateBody(string Name, string? Description, Guid? ProjectDisciplineId, Guid? DeliverableId, Guid? MilestoneId, Guid? AssigneeId,
        Guid? ReviewerId, bool? RequiresReview, string? Priority, DateOnly? StartDate, DateOnly? DueDate, decimal? EstimatedHours, Guid[]? DependsOn);
    public sealed record TransitionBody(string ToStatus, string? Reason, string? Comment, Guid? ReviewerId, int? RowVersion, bool? AcknowledgeReadiness = null);
    public sealed record BlockBody(string Type, string Reason, int? RowVersion);
    public sealed record UserBody(Guid UserId);
    public sealed record BulkBody(Guid[] TaskIds, string Operation, JsonElement? Params, string? Reason);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/tasks", async (Guid id, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            await access.Project(id, track: false);
            var s = await store.Get(db);
            var f = TaskFilter.From(http.Request.Query);
            return await TaskQueries.Page(db, TaskQueries.Apply(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == id), f, me.Id, clock.Today(s), s), f);
        });
        // Cross-project task list for the workspace views (§36.1, §36.3): only projects the caller may see, within the
        // selected scope; a workspace's manual order applies only to its owner.
        api.MapGet("/tasks", async (string? projects, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var s = await store.Get(db);
            var f = TaskFilter.From(http.Request.Query);
            if (f.WorkspaceId is { } ws && !await db.Workspaces.AnyAsync(w => w.Id == ws && w.OwnerId == me.Id)) f.WorkspaceId = null;
            return await TaskQueries.Page(db, TaskQueries.Apply(db, InScope(access, db, projects), f, me.Id, clock.Today(s), s), f);
        });
        api.MapPost("/projects/{id:guid}/tasks", Create);
        api.MapPost("/projects/{id:guid}/tasks/bulk", Bulk);
        api.MapGet("/tasks/{id}", Get);
        api.MapPatch("/tasks/{id:guid}", Edit);
        api.MapPost("/tasks/{id:guid}/transition", Transition);
        TaskStartEndpoints.Map(api);
        api.MapPost("/tasks/{id:guid}/block", SetBlock);
        api.MapPost("/tasks/{id:guid}/unblock", ClearBlock);
        api.MapPost("/tasks/{id:guid}/collaborators", AddCollaborator);
        api.MapDelete("/tasks/{id:guid}/collaborators/{userId:guid}", RemoveCollaborator);
        api.MapGet("/tasks/{id:guid}/delete-preview", DeletePreview);
        api.MapDelete("/tasks/{id:guid}", Delete);
        api.MapPost("/tasks/{id:guid}/restore", Restore);
        api.MapGet("/items/{type}/{id:guid}/watchers", async (string type, Guid id, HubDb db, Access access) =>
        {
            var pid = await ActivityEndpoints.ItemProject(db, type, id) ?? throw ApiException.NotFound();
            await access.Project(pid, track: false);
            return await db.Watchers.Where(w => w.ItemType == type && w.ItemId == id).Join(db.Users, w => w.UserId, u => u.Id, (w, u) => new { w.UserId, u.DisplayName, w.Source }).ToListAsync();
        });
        api.MapPost("/items/{type}/{id:guid}/watchers", async (string type, Guid id, UserBody body, HubDb db, Access access) =>
        {
            var pid = await ActivityEndpoints.ItemProject(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(pid, track: false);
            Access.Demand(body.UserId == access.Me.Id ? Allow.Yes : Permissions.Comment(access.Actor, ctx));
            if (!await EmailProjectAccess.Allowed(db, body.UserId, [pid])) throw ApiException.NotFound();
            await Watch(db, pid, type, id, body.UserId, "Manual");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        api.MapDelete("/items/{type}/{id:guid}/watchers/{userId:guid}", async (string type, Guid id, Guid userId, HubDb db, Access access) =>
        {
            var pid = await ActivityEndpoints.ItemProject(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(pid, track: false);
            Access.Demand(userId == access.Me.Id ? Allow.Yes : Permissions.Comment(access.Actor, ctx));
            await db.Watchers.Where(w => w.ItemType == type && w.ItemId == id && w.UserId == userId).ExecuteDeleteAsync();
            return Results.NoContent();
        });
    }

    /// The tasks of the projects in a workspace scope (§36.1): the base of every cross-project list, board and count.
    public static IQueryable<WorkTask> InScope(Access access, HubDb db, string? projects)
    {
        var ids = Scope.Projects(access, db, projects).Select(p => p.Id);
        return db.Tasks.AsNoTracking().Where(t => ids.Contains(t.ProjectId));
    }

    public static async Task Watch(HubDb db, Guid projectId, string type, Guid itemId, Guid userId, string source)
    {
        if (!await db.Watchers.AnyAsync(w => w.ItemType == type && w.ItemId == itemId && w.UserId == userId)
            && !db.Watchers.Local.Any(w => w.ItemType == type && w.ItemId == itemId && w.UserId == userId))
            db.Watchers.Add(new ItemWatcher { ProjectId = projectId, ItemType = type, ItemId = itemId, UserId = userId, Source = source });
    }

    public static async Task<(WorkTask T, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var t = await db.Tasks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(t.ProjectId);
        return (t, p, ctx);
    }

    public static async Task<TaskFacts> Facts(HubDb db, WorkTask t)
    {
        var collaborators = (await db.Collaborators.Where(c => c.TaskId == t.Id).Select(c => c.UserId).ToListAsync()).ToHashSet();
        var deps = await db.Dependencies.AnyAsync(d => d.PredecessorTaskId == t.Id || d.SuccessorTaskId == t.Id);
        // §8.6: a creator edits the item until someone else has acted on it.
        var window = t.Status == TaskStatuses.NotStarted && !await db.Comments.AnyAsync(c => c.ItemType == ItemType.Task && c.ItemId == t.Id && c.AuthorId != t.CreatedBy);
        var supervisor = t.AssigneeId is { } aid ? await db.Users.Where(u => u.Id == aid).Select(u => u.SupervisorId).FirstOrDefaultAsync() : null;
        return new TaskFacts(t.ProjectDisciplineId, t.AssigneeId, t.ReviewerId, t.CreatedBy, collaborators, t.Status, deps, window, supervisor);
    }

    /// Every step of a path must be allowed for the caller (a checkbox completion passes through In Progress).
    public static Allow PathAllowed(Actor a, ProjectContext ctx, TaskFacts facts, string from, string[] path)
    {
        var cur = from;
        foreach (var step in path)
        {
            var allow = Permissions.TaskTransition(a, ctx, facts, cur, step);
            if (!allow) return allow;
            cur = step;
        }
        return Allow.Yes;
    }

    static string Reason(Allow a, string arg) => Text.Get(a.Why ?? "perm.admin", a.Arg ?? arg);
    static async Task<string> DisciplineName(HubDb db, Guid pdId) => await db.ProjectDisciplines.Where(x => x.Id == pdId).Select(x => x.Discipline!.Name).FirstAsync();

    // ---------- Create (FR-TSK-01, TM-06, R-02, Workflow 4) ----------

    static async Task<IResult> Create(Guid id, CreateBody body, Access access, HubDb db, TeamService team, Notifier notify, SettingsStore store, CurrentUser me, TimeProvider clock)
    {
        var (t, warnings) = await NewTask(id, body, access, db, team, notify, store, me, clock);
        return Results.Created($"/api/v1/tasks/{t.Id}", new { t.Id, t.Key, t.RowVersion, warnings });
    }

    /// Creates a task with every creation rule (T-18, T-19, T-22, R-02, permissions, keys, notices); shared with converting a meeting action.
    internal static async Task<(WorkTask Task, List<string> Warnings)> NewTask(Guid id, CreateBody body, Access access, HubDb db, TeamService team, Notifier notify,
        SettingsStore store, CurrentUser me, TimeProvider clock)
    {
        var (p, ctx) = await access.Project(id);
        var s = await store.Get(db);
        Deliverable? deliverable = null;
        if (body.DeliverableId is { } did) deliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.Id == did && d.ProjectId == id) ?? throw ApiException.Invalid("deliverableId", "task.other_project"); // T-18
        var member = await db.ProjectMembers.Where(m => m.ProjectId == id && m.UserId == me.Id && m.RemovedAt == null).Select(m => m.PrimaryDisciplineId).FirstOrDefaultAsync();
        var pdId = body.ProjectDisciplineId ?? deliverable?.ProjectDisciplineId ?? member ?? ctx.LeadDisciplineIds.FirstOrDefault();
        var pd = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.Id == pdId && x.ProjectId == id) ?? throw ApiException.Invalid("projectDisciplineId", "error.required");
        Check.That(pd.IsActive, "projectDisciplineId", "deliverable.inactive_discipline"); // T-22
        var perm = Permissions.CreateTask(access.Actor, ctx, pd.Id);
        if (!perm) throw ApiException.Forbidden(perm.Why!, await DisciplineName(db, pd.Id));
        var facts = new TaskFacts(pd.Id, null, null, me.Id, new HashSet<Guid>(), TaskStatuses.NotStarted);
        if (body.AssigneeId is { } a) { Access.Demand(Permissions.AssignTask(access.Actor, ctx, facts, atCreation: true)); await DeliverableEndpoints.ActivePerson(db, a, "assigneeId"); }
        if (body.ReviewerId is { } r) { Access.Demand(Permissions.SetReviewer(access.Actor, ctx, facts, atCreation: true)); await DeliverableEndpoints.ActivePerson(db, r, "reviewerId"); }
        if (body.ReviewerId is not null && body.ReviewerId == body.AssigneeId && !s.AllowSelfReview) throw ApiException.Invalid("reviewerId", "task.self_review"); // R-02
        if (body.MilestoneId is { } mid) { Check.That(deliverable is null, "milestoneId", "task.milestone_or_deliverable"); Check.That(await db.Milestones.AnyAsync(m => m.Id == mid && m.ProjectId == id), "milestoneId", "error.not_found"); }
        if (body.StartDate is { } sd && body.DueDate is { } dd) Check.That(sd <= dd, "startDate", "deliverable.start_after_due"); // T-19
        if (body.Priority is not null) Check.OneOf(body.Priority, Priority.All, "priority");
        if (body.EstimatedHours is { } eh) Check.That(eh >= 0 && eh <= 10000, "estimatedHours", "error.positive");
        var now = clock.GetUtcNow();
        var t = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, id, p.ProjectNumber, "task");
            var t = new WorkTask
            {
                ProjectId = id, Seq = seq, Key = key, Name = Check.Required(body.Name, "name", 200), Description = Check.Optional(body.Description, "description", 8000),
                ProjectDisciplineId = pd.Id, DeliverableId = deliverable?.Id, MilestoneId = deliverable is null ? body.MilestoneId : null, AssigneeId = body.AssigneeId,
                ReviewerId = body.ReviewerId, RequiresReview = body.RequiresReview ?? false, Priority = body.Priority ?? Priority.Medium, StartDate = body.StartDate,
                DueDate = body.DueDate, OriginalStartDate = body.StartDate, OriginalDueDate = body.DueDate, EstimatedHours = body.EstimatedHours,
                LastActivityAt = now, StatusChangedAt = now, SortOrder = seq,
            };
            db.Tasks.Add(t);
            await team.EnsureMember(p, t.AssigneeId, ProjectRole.TeamMember);
            await team.EnsureMember(p, t.ReviewerId, ProjectRole.Reviewer);
            await db.SaveChangesAsync();
            foreach (var pred in body.DependsOn ?? [])
                await DependencyEndpoints.AddEdge(db, access, p, ctx, pred, t.Id, null);
            await db.SaveChangesAsync();
            var actor = await notify.ActorName();
            await notify.Send(NotificationEvents.TaskAssigned, t.AssigneeId, Item(p, t), Text.Get("notify.task_assigned", actor, t.Key, t.Name));
            await notify.Send(NotificationEvents.ReviewerSet, t.ReviewerId, Item(p, t), Text.Get("notify.reviewer_set", actor, t.Key, t.Name));
            await db.SaveChangesAsync();
            return t;
        });
        var warnings = new List<string>();
        if (deliverable?.DueDate is { } ddue && t.DueDate > ddue) warnings.Add(Text.Get("task.after_deliverable", t.Key, deliverable.Key, ddue.ToString("yyyy-MM-dd"))); // T-04
        return (t, warnings);
    }

    // ---------- Detail (§13.3.1, §25.9 permissions.allowedTransitions) ----------

    static async Task<object> Get(string id, Access access, HubDb db, SettingsStore store)
    {
        var t = (Guid.TryParse(id, out var g) ? await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(t.ProjectId, track: false);
        var s = await store.Get(db);
        var row = (await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(x => x.Id == t.Id))).Single();
        var facts = await Facts(db, t);
        var a = access.Actor;
        var dname = await DisciplineName(db, t.ProjectDisciplineId);
        var transitions = TaskStatuses.All.Where(to => to != t.Status)
            .Select(to => (to, path: Workflow.TaskPath(t.Status, to, t.RequiresReview, t.PreviousStatus)))
            .Where(x => x.path is not null)
            .Select(x =>
            {
                var allow = PathAllowed(a, ctx, facts, t.Status, x.path!);
                return new { To = x.to, Allowed = allow.Ok, Reason = allow.Ok ? null : Reason(allow, dname), NeedsReason = Workflow.TaskNeedsReason(t.Status, x.to), Via = x.path!.Length > 1 ? x.path[0] : null };
            }).ToList();
        var notAllowedComplete = t.RequiresReview && t.Status == TaskStatuses.InProgress ? Text.Get("task.requires_review_hint") : null;
        var collaborators = await db.Collaborators.Where(c => c.TaskId == t.Id).Join(db.Users, c => c.UserId, u => u.Id, (c, u) => new { u.Id, u.DisplayName, u.IsActive }).ToListAsync();
        var watchers = await db.Watchers.Where(w => w.ItemType == ItemType.Task && w.ItemId == t.Id).Join(db.Users, w => w.UserId, u => u.Id, (w, u) => new { u.Id, u.DisplayName, w.Source }).ToListAsync();
        var edit = Permissions.EditTask(a, ctx, facts);
        var assign = Permissions.AssignTask(a, ctx, facts);
        var due = Permissions.ChangeDueDate(a, ctx, facts);
        var block = Permissions.ManualBlock(a, ctx, facts);
        var del = Permissions.DeleteTask(a, ctx, facts);
        object P(Allow x) => new { x.Ok, Reason = x.Ok ? null : Reason(x, dname) };
        return new
        {
            Task = row, t.Description, t.OnHoldReason, t.CancelledReason, t.PreviousStatus, t.ReviewRequestedAt,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            Collaborators = collaborators, Watchers = watchers,
            Permissions = new
            {
                Edit = P(edit), Assign = P(assign), SetReviewer = P(Permissions.SetReviewer(a, ctx, facts)), DueDate = P(due),
                DueNeedsReason = Permissions.DueChangeNeedsReason(a, ctx, facts), Block = P(block), Delete = P(del),
                Restore = Permissions.RestoreItem(a, ctx).Ok, Comment = Permissions.Comment(a, ctx).Ok,
                Transitions = transitions, CompleteHint = notAllowedComplete, IsReviewer = t.ReviewerId == a.Id,
                Dependencies = Permissions.ManageDependency(a, ctx, facts, facts).Ok, EnterTime = Permissions.EnterTime(a, ctx).Ok,
                NeedsReason = p.Status == ProjectStatus.Complete, AllowSelfReview = s.AllowSelfReview,
                AuthoriseStart = t.Status == TaskStatuses.NotStarted && Permissions.ManageCoordination(a, ctx, t.ProjectDisciplineId).Ok,
            },
        };
    }

    // ---------- Edit (T-15..T-22, R-02, R-04, E-03) ----------

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, Notifier notify, SettingsStore store, TimeProvider clock)
    {
        var (t, p, ctx) = await Load(db, access, id);
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, t, patch.RowVersion);
        var facts = await Facts(db, t);
        var a = access.Actor;
        var dname = await DisciplineName(db, t.ProjectDisciplineId);
        void Need(Allow x) { if (!x) throw ApiException.Forbidden(x.Why!, x.Arg ?? dname); }
        var s = await store.Get(db);
        var reason = patch.Str("reason");
        ProjectEndpoints.CorrectionReason(p, reason);
        db.Audit.Reason = reason;
        var actor = await notify.ActorName();
        var editable = new[] { "name", "description", "requiresReview", "priority", "estimatedHours", "progressPct", "startDate" };
        if (editable.Any(patch.Has)) Need(Permissions.EditTask(a, ctx, facts));
        if (patch.Has("name")) t.Name = Check.Required(patch.Str("name"), "name", 200);
        if (patch.Has("description")) t.Description = Check.Optional(patch.Str("description"), "description", 8000);
        if (patch.Has("requiresReview")) t.RequiresReview = patch.Bool("requiresReview") ?? false;
        if (patch.Has("priority")) { var pr = patch.Str("priority"); Check.OneOf(pr, Priority.All, "priority"); t.Priority = pr!; }
        if (patch.Has("estimatedHours")) { var eh = patch.Dec("estimatedHours"); Check.That(eh is null or (>= 0 and <= 10000), "estimatedHours", "error.positive"); t.EstimatedHours = eh; }
        if (patch.Has("progressPct"))
        {
            var pct = patch.Int("progressPct") ?? 0;
            Check.That(pct is >= 0 and <= 100 && pct % 10 == 0, "progressPct", "task.progress_steps"); // T-20
            Check.That(t.Status != TaskStatuses.Complete || pct == 100, "progressPct", "task.complete_is_100");
            t.ProgressPct = pct;
        }
        if (patch.Has("startDate")) t.StartDate = patch.Date("startDate");
        if (patch.Has("dueDate"))
        {
            var nd = patch.Date("dueDate");
            if (nd != t.DueDate)
            {
                Need(Permissions.ChangeDueDate(a, ctx, facts));
                if (Permissions.DueChangeNeedsReason(a, ctx, facts)) db.Audit.Reason = Check.Reason(reason); // T-16, Q11
                var old = t.DueDate;
                t.DueDate = nd;
                if (old is not null) t.DueDateChangeCount++;
                t.OriginalDueDate ??= nd;
                await notify.Send(NotificationEvents.DueDateChanged, [t.AssigneeId, t.ReviewerId], Item(p, t),
                    Text.Get("notify.due_changed", actor, t.Key, t.Name, old?.ToString("yyyy-MM-dd") ?? "—", nd?.ToString("yyyy-MM-dd") ?? "—"), reason);
            }
        }
        t.OriginalStartDate ??= t.StartDate;
        if (t.StartDate is { } sd && t.DueDate is { } dd) Check.That(sd <= dd, "startDate", "deliverable.start_after_due"); // T-19
        if (patch.Has("assigneeId"))
        {
            var na = patch.Id("assigneeId");
            if (na != t.AssigneeId)
            {
                Need(Permissions.AssignTask(a, ctx, facts));
                if (na is { } u) { await DeliverableEndpoints.ActivePerson(db, u, "assigneeId"); if (u == t.ReviewerId && !s.AllowSelfReview) throw ApiException.Invalid("assigneeId", "task.self_review"); }
                var old = t.AssigneeId;
                t.AssigneeId = na;
                await team.EnsureMember(p, na, ProjectRole.TeamMember);
                await notify.Send(NotificationEvents.TaskAssigned, na, Item(p, t), Text.Get("notify.task_assigned", actor, t.Key, t.Name));
                await notify.Send(NotificationEvents.WorkReassignedAway, old, Item(p, t), Text.Get("notify.reassigned_away", actor, t.Key, t.Name));
                if (!Permissions.IsPM(a, ctx) && !Permissions.IsDL(ctx, t.ProjectDisciplineId)) // Workflow 10: a supervisor's move reaches the PM too
                    await notify.Send(NotificationEvents.SupervisorStaffing, p.ProjectManagerId, Item(p, t),
                        Text.Get("notify.task_moved", actor, t.Key, t.Name, await notify.Name(old), await notify.Name(na)));
            }
        }
        if (patch.Has("reviewerId"))
        {
            var nr = patch.Id("reviewerId");
            if (nr != t.ReviewerId)
            {
                Need(Permissions.SetReviewer(a, ctx, facts));
                if (nr is { } u) { await DeliverableEndpoints.ActivePerson(db, u, "reviewerId"); if (u == t.AssigneeId && !s.AllowSelfReview) throw ApiException.Invalid("reviewerId", "task.self_review"); } // R-02
                var old = t.ReviewerId;
                t.ReviewerId = nr;
                await team.EnsureMember(p, nr, ProjectRole.Reviewer);
                await notify.Send(NotificationEvents.ReviewerSet, nr, Item(p, t), Text.Get("notify.reviewer_set", actor, t.Key, t.Name));
                if (TaskStatuses.IsReview(t.Status)) // R-04: both reviewers hear about a change mid-review
                    await notify.Send(NotificationEvents.ReviewerSet, old, Item(p, t), Text.Get("notify.reviewer_removed", actor, t.Key, t.Name));
            }
        }
        if (patch.Has("deliverableId") || patch.Has("milestoneId") || patch.Has("projectDisciplineId"))
        {
            Need(Permissions.EditTask(a, ctx, facts));
            if (patch.Has("deliverableId"))
            {
                var ndid = patch.Id("deliverableId");
                if (ndid is { } nd2)
                {
                    var nd = await db.Deliverables.FirstOrDefaultAsync(d => d.Id == nd2) ?? throw ApiException.Invalid("deliverableId", "error.not_found");
                    Check.That(nd.ProjectId == t.ProjectId, "deliverableId", "task.other_project"); // T-18
                    t.MilestoneId = null; // T-17/T-21: milestone context follows the deliverable; dependencies are kept
                }
                t.DeliverableId = ndid;
            }
            if (patch.Has("milestoneId"))
            {
                var nm = patch.Id("milestoneId");
                if (nm is { } m) { Check.That(t.DeliverableId is null, "milestoneId", "task.milestone_or_deliverable"); Check.That(await db.Milestones.AnyAsync(x => x.Id == m && x.ProjectId == t.ProjectId), "milestoneId", "error.not_found"); }
                t.MilestoneId = nm;
            }
            if (patch.Has("projectDisciplineId"))
            {
                var npd = patch.Id("projectDisciplineId") ?? throw ApiException.Invalid("projectDisciplineId", "error.required");
                var pd = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.Id == npd && x.ProjectId == t.ProjectId && x.IsActive) ?? throw ApiException.Invalid("projectDisciplineId", "error.not_found"); // T-22
                if (!(Permissions.IsPM(a, ctx) || Permissions.IsDL(ctx, pd.Id))) Need(Allow.No("perm.dl_own", pd.Id.ToString()));
                t.ProjectDisciplineId = pd.Id;
            }
        }
        t.LastActivityAt = clock.GetUtcNow();
        await db.SaveChangesAsync();
        Http.ETag(http, t);
        var warnings = new List<string>();
        if (t.DeliverableId is { } dId && await db.Deliverables.Where(d => d.Id == dId).Select(d => d.DueDate).FirstOrDefaultAsync() is { } ddue && t.DueDate > ddue)
            warnings.Add(Text.Get("task.after_deliverable", t.Key, "", ddue.ToString("yyyy-MM-dd")));
        return Results.Ok(new { t.Id, t.RowVersion, t.DueDateChangeCount, warnings });
    }

    // ---------- Transitions (T-10..T-14, R-01..R-04, C-07, C-08, DL-02) ----------

    static async Task<IResult> Transition(Guid id, TransitionBody body, HttpContext http, Access access, HubDb db, Notifier notify, TeamService team, SettingsStore store, TimeProvider clock)
    {
        var (t, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, t, body.RowVersion);
        Check.OneOf(body.ToStatus, TaskStatuses.All, "toStatus");
        var s = await store.Get(db);
        var warnings = new List<string>();
        var previousStatus = t.Status;
        await ApplyTransition(db, access, ctx, p, t, body.ToStatus, body.Reason, body.Comment, body.ReviewerId, s, clock, team, warnings,
            body.AcknowledgeReadiness == true, store);
        await db.SaveChangesAsync();
        if (t.Status != previousStatus) await TransitionNotices(notify, p, t, body.Comment);
        await db.SaveChangesAsync();
        return Results.Ok(new { t.Id, t.Status, t.RowVersion, t.ProgressPct, t.ReviewRound, warnings });
    }

    public static async Task ApplyTransition(HubDb db, Access access, ProjectContext ctx, Project p, WorkTask t, string to, string? reason, string? comment,
        Guid? reviewerId, OrgSettings s, TimeProvider clock, TeamService team, List<string> warnings,
        bool acknowledgeReadiness = false, SettingsStore? store = null)
    {
        var from = t.Status;
        if (from == to)
        {
            Access.Demand(Permissions.TaskTransition(access.Actor, ctx, await Facts(db, t), from, to));
            return;
        }
        var a = access.Actor;
        // AC-TSK-03: a reviewer can be chosen in the same step as Ready for Review.
        if (reviewerId is { } rid && to is TaskStatuses.ReadyForReview or TaskStatuses.Complete && t.RequiresReview && t.ReviewerId != rid)
        {
            Access.Demand(t.AssigneeId == a.Id || t.CreatedBy == a.Id ? Allow.Yes : Permissions.SetReviewer(a, ctx, await Facts(db, t)));
            await DeliverableEndpoints.ActivePerson(db, rid, "reviewerId");
            if (rid == t.AssigneeId && !s.AllowSelfReview) throw ApiException.Invalid("reviewerId", "task.self_review");
            t.ReviewerId = rid;
            await team.EnsureMember(p, rid, ProjectRole.Reviewer);
        }
        var path = Workflow.TaskPath(from, to, t.RequiresReview, t.PreviousStatus);
        if (path is null)
            throw to == TaskStatuses.Complete && t.RequiresReview && from == TaskStatuses.InProgress
                ? ApiException.Rule("review_required", "task.requires_review_hint") // R-01
                : ApiException.Rule("illegal_transition", "task.illegal_transition", null, from, to);
        var facts = await Facts(db, t);
        var dname = await DisciplineName(db, t.ProjectDisciplineId);
        var cur = from;
        foreach (var step in path)
        {
            var allow = Permissions.TaskTransition(a, ctx, facts, cur, step);
            if (!allow) throw ApiException.Forbidden(allow.Why!, allow.Arg ?? dname);
            cur = step;
        }
        if (to == TaskStatuses.ReadyForReview && t.ReviewerId is null) throw ApiException.Invalid("reviewerId", "task.reviewer_required"); // AC-TSK-03
        if (Workflow.TaskNeedsReason(from, to)) reason = Check.Reason(reason);
        ProjectEndpoints.CorrectionReason(p, reason);
        // FR-RDY-02: a start that is not Ready needs acknowledgement, a reason and PM/lead authorisation, checked only
        // after the review, access and lifecycle guards above so it cannot stand in for any of them.
        if (TaskStartEndpoints.IsStart(from, path))
            await TaskStartEndpoints.Guard(db, access, ctx, p, t, acknowledgeReadiness, reason, s, store, clock);
        var now = clock.GetUtcNow();
        var authorId = db.Audit.ActorId!.Value;
        if (from == TaskStatuses.NotStarted && path.Contains(TaskStatuses.InProgress))
        {
            var state = await db.TaskStates.AsNoTracking().FirstOrDefaultAsync(x => x.TaskId == t.Id);
            if (state?.IsBlocked == true) warnings.Add(Text.Get("task.started_blocked", t.Key)); // D-01: a warning, not a lock
        }
        switch (to)
        {
            case TaskStatuses.RevisionRequired:
                var text = Check.Required(comment, "comment", 8000); // R-03
                t.ReviewRound++;
                db.Comments.Add(new Comment { ProjectId = t.ProjectId, ItemType = ItemType.Task, ItemId = t.Id, AuthorId = authorId, Body = text, CommentKind = CommentKind.Review, ReviewRound = t.ReviewRound, CreatedAt = now, AuditKey = t.Key });
                comment = null;
                break;
            case TaskStatuses.OnHold: t.PreviousStatus = from; t.OnHoldReason = reason; break;
            case TaskStatuses.Cancelled: t.PreviousStatus = from; t.CancelledReason = reason; break;
            case TaskStatuses.Complete: t.CompletedAt = now; t.ProgressPct = 100; break;
        }
        if (from == TaskStatuses.Complete && to == TaskStatuses.InProgress) { t.CompletedAt = null; t.ProgressPct = 90; } // T-14, T-20
        if (from == TaskStatuses.OnHold) t.OnHoldReason = null;
        if (from == TaskStatuses.Cancelled) t.CancelledReason = null;
        if (to == TaskStatuses.ReadyForReview && !TaskStatuses.IsReview(from)) t.ReviewRequestedAt = now;
        if (!TaskStatuses.IsReview(to)) t.ReviewRequestedAt = null;
        if (to == TaskStatuses.InProgress && t.ProgressPct == 0 && from == TaskStatuses.NotStarted) t.ProgressPct = 0;
        if (!string.IsNullOrWhiteSpace(comment)) // C-08: a note typed with a status change is a Status Note comment
            db.Comments.Add(new Comment { ProjectId = t.ProjectId, ItemType = ItemType.Task, ItemId = t.Id, AuthorId = authorId, Body = comment.Trim(), CommentKind = CommentKind.StatusNote, CreatedAt = now, AuditKey = t.Key });
        t.Status = to;
        t.StatusChangedAt = now;
        t.LastActivityAt = now;
        db.Audit.Note(t, action: from == TaskStatuses.Complete ? "Reopened" : from == TaskStatuses.Cancelled ? "Restored" : null, reason: reason);
        // DL-02 recommendation: the first task in progress moves a Not Started deliverable to In Progress.
        if (path.Contains(TaskStatuses.InProgress) && t.DeliverableId is { } did)
        {
            var d = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == did);
            if (d is { Status: DeliverableStatus.NotStarted }) { d.Status = DeliverableStatus.InProgress; d.StatusChangedAt = now; d.LastActivityAt = now; }
        }
    }

    static async Task TransitionNotices(Notifier notify, Project p, WorkTask t, string? comment)
    {
        var actor = await notify.ActorName();
        switch (t.Status)
        {
            case TaskStatuses.ReadyForReview:
                await notify.Send(NotificationEvents.ReviewRequested, t.ReviewerId, Item(p, t),
                    t.ReviewRound > 0 ? Text.Get("notify.review_requested_round", actor, t.Key, t.Name, t.ReviewRound + 1) : Text.Get("notify.review_requested", actor, t.Key, t.Name));
                break;
            case TaskStatuses.InReview:
                await notify.Send(NotificationEvents.TaskChanged, t.AssigneeId, Item(p, t), Text.Get("notify.review_started", actor, t.Key, t.Name));
                break;
            case TaskStatuses.Complete when t.RequiresReview:
                await notify.Send(NotificationEvents.ReviewOutcome, t.AssigneeId, Item(p, t), Text.Get("notify.review_approved", actor, t.Key, t.Name));
                break;
            case TaskStatuses.RevisionRequired:
                await notify.Send(NotificationEvents.ReviewOutcome, t.AssigneeId, Item(p, t), Text.Get("notify.review_revision", actor, t.Key, t.Name, t.ReviewRound), comment);
                break;
        }
    }

    // ---------- Manual block (FR-TSK-07, D-14) ----------

    static async Task<IResult> SetBlock(Guid id, BlockBody body, HttpContext http, Access access, HubDb db, TimeProvider clock, CurrentUser me)
    {
        var (t, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, t, body.RowVersion);
        var allow = Permissions.ManualBlock(access.Actor, ctx, await Facts(db, t));
        if (!allow) throw ApiException.Forbidden(allow.Why!, await DisciplineName(db, t.ProjectDisciplineId));
        Check.OneOf(body.Type, BlockType.All, "type");
        t.ManualBlockType = body.Type;
        t.ManualBlockReason = Check.Required(body.Reason, "reason", 1000);
        t.ManualBlockSetAt = clock.GetUtcNow();
        t.ManualBlockSetBy = me.Id;
        t.LastActivityAt = clock.GetUtcNow();
        db.Audit.Note(t, action: "Blocked");
        await db.SaveChangesAsync();
        return Results.Ok(new { t.Id, t.RowVersion });
    }

    static async Task<IResult> ClearBlock(Guid id, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        var (t, _, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, t, null);
        var allow = Permissions.ManualBlock(access.Actor, ctx, await Facts(db, t));
        if (!allow) throw ApiException.Forbidden(allow.Why!, await DisciplineName(db, t.ProjectDisciplineId));
        t.ManualBlockType = null; t.ManualBlockReason = null; t.ManualBlockSetAt = null; t.ManualBlockSetBy = null;
        t.LastActivityAt = clock.GetUtcNow();
        db.Audit.Note(t, action: "Unblocked");
        await db.SaveChangesAsync();
        return Results.Ok(new { t.Id, t.RowVersion });
    }

    // ---------- Collaborators (FR-TSK-08, T-15, E-07) ----------

    static async Task<IResult> AddCollaborator(Guid id, UserBody body, Access access, HubDb db, TeamService team, Notifier notify, TimeProvider clock)
    {
        var (t, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.Writable(access.Actor, ctx));
        var facts = await Facts(db, t);
        Access.Demand(Permissions.AssignTask(access.Actor, ctx, facts).Ok || t.AssigneeId == access.Me.Id ? Allow.Yes : Allow.No("perm.task_assign"));
        await DeliverableEndpoints.ActivePerson(db, body.UserId, "userId");
        if (!await db.Collaborators.AnyAsync(c => c.TaskId == id && c.UserId == body.UserId))
        {
            db.Collaborators.Add(new TaskCollaborator { TaskId = id, UserId = body.UserId, AddedAt = clock.GetUtcNow(), AddedBy = access.Me.Id });
            db.LogEvent(ItemType.Task, t.Id, "CollaboratorAdded", "assignment", t.ProjectId, t.Key, t.Name, new[] { new { field = "Collaborator", old = (object?)null, @new = (object)body.UserId } }, disciplineId: t.ProjectDisciplineId);
            await team.EnsureMember(p, body.UserId, ProjectRole.TeamMember);
            await notify.Send(NotificationEvents.TaskAssigned, body.UserId, Item(p, t), Text.Get("notify.collaborator_added", await notify.ActorName(), t.Key, t.Name));
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    }

    static async Task<IResult> RemoveCollaborator(Guid id, Guid userId, Access access, HubDb db)
    {
        var (t, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.Writable(access.Actor, ctx));
        var facts = await Facts(db, t);
        Access.Demand(Permissions.AssignTask(access.Actor, ctx, facts).Ok || t.AssigneeId == access.Me.Id || userId == access.Me.Id ? Allow.Yes : Allow.No("perm.task_assign"));
        var c = await db.Collaborators.FirstOrDefaultAsync(x => x.TaskId == id && x.UserId == userId);
        if (c is not null)
        {
            db.Collaborators.Remove(c);
            db.LogEvent(ItemType.Task, t.Id, "CollaboratorRemoved", "assignment", t.ProjectId, t.Key, t.Name, new[] { new { field = "Collaborator", old = (object)userId, @new = (object?)null } }, disciplineId: t.ProjectDisciplineId);
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    }

    // ---------- Delete and restore (T-08, T-09, D-09, E-04, AC-TSK-09) ----------

    static async Task<object> DeletePreview(Guid id, Access access, HubDb db)
    {
        var (t, _, ctx) = await Load(db, access, id);
        var deps = await DependencyList(db, id);
        var hasWork = t.ProgressPct > 0 || await db.Comments.AnyAsync(c => c.ItemType == ItemType.Task && c.ItemId == id);
        var allow = Permissions.DeleteTask(access.Actor, ctx, await Facts(db, t));
        var cancel = Permissions.TaskTransition(access.Actor, ctx, await Facts(db, t), t.Status, TaskStatuses.Cancelled);
        return new { dependencies = deps, suggestCancel = hasWork && !TaskStatuses.IsTerminal(t.Status), canDelete = allow.Ok, canCancel = cancel.Ok && !TaskStatuses.IsTerminal(t.Status) };
    }

    static async Task<List<object>> DependencyList(HubDb db, Guid id) =>
        (await db.Dependencies.Where(d => d.PredecessorTaskId == id || d.SuccessorTaskId == id)
            .Select(d => new { d.Id, d.PredecessorTaskId, d.SuccessorTaskId,
                Other = db.Tasks.Where(x => x.Id == (d.PredecessorTaskId == id ? d.SuccessorTaskId : d.PredecessorTaskId)).Select(x => new { x.Key, x.Name, x.AssigneeId }).First() })
            .ToListAsync()).Select(d => (object)new { d.Id, d.PredecessorTaskId, d.SuccessorTaskId, d.Other.Key, d.Other.Name, Direction = d.PredecessorTaskId == id ? "blocks" : "dependsOn", d.Other.AssigneeId }).ToList();

    static async Task<IResult> Delete(Guid id, Access access, HubDb db, Notifier notify, TimeProvider clock, CurrentUser me)
    {
        var (t, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.DeleteTask(access.Actor, ctx, await Facts(db, t)));
        var now = clock.GetUtcNow();
        var edges = await db.Dependencies.Where(d => d.PredecessorTaskId == id || d.SuccessorTaskId == id).ToListAsync();
        var successorIds = edges.Where(e => e.PredecessorTaskId == id).Select(e => e.SuccessorTaskId).ToList();
        var successorAssignees = await db.Tasks.Where(x => successorIds.Contains(x.Id)).Select(x => x.AssigneeId).ToListAsync();
        foreach (var e in edges) { e.DeletedAt = now; e.DeletedBy = me.Id; e.AuditKey = $"deleted with {t.Key}"; }
        var assignee = await db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefaultAsync();
        var snapshot = JsonSerializer.Serialize(new
        {
            t.Key, t.Name, Assignee = assignee, t.Status, DueDate = t.DueDate?.ToString("yyyy-MM-dd"),
            RemovedDependencies = edges.Select(e => new { e.Id, e.PredecessorTaskId, e.SuccessorTaskId }),
        }, JsonOpts.Web);
        t.DeletedAt = now;
        t.DeletedBy = me.Id;
        db.Audit.Note(t, snapshot: snapshot);
        await db.SaveChangesAsync();
        var actor = await notify.ActorName();
        await notify.Send(NotificationEvents.DependencyRemoved, [.. successorAssignees, p.ProjectManagerId], Item(p, t) with { Link = null },
            Text.Get("notify.dependency_removed_deleted", actor, t.Key, t.Name));
        await db.SaveChangesAsync();
        return Results.Ok(new { removedDependencies = edges.Count });
    }

    static async Task<IResult> Restore(Guid id, Access access, HubDb db)
    {
        var t = await db.Tasks.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt != null) ?? throw ApiException.NotFound();
        var (_, ctx) = await access.Project(t.ProjectId);
        Access.Demand(Permissions.RestoreItem(access.Actor, ctx));
        t.DeletedAt = null;
        t.DeletedBy = null;
        var deletedAt = await db.ActivityLog.Where(a => a.ItemId == id && a.Action == "Deleted").OrderByDescending(a => a.OccurredAt).Select(a => a.OccurredAt).FirstOrDefaultAsync();
        foreach (var e in await db.Dependencies.IgnoreQueryFilters().Where(d => (d.PredecessorTaskId == id || d.SuccessorTaskId == id) && d.DeletedAt != null && d.DeletedAt >= deletedAt.AddSeconds(-5)).ToListAsync())
        {
            var other = e.PredecessorTaskId == id ? e.SuccessorTaskId : e.PredecessorTaskId;
            if (await db.Tasks.AnyAsync(x => x.Id == other)) { e.DeletedAt = null; e.DeletedBy = null; e.AuditKey = $"restored with {t.Key}"; } // E-04: edges come back
        }
        db.Audit.Note(t, action: "Restored");
        await db.SaveChangesAsync();
        return Results.Ok(new { t.Id, t.RowVersion });
    }

    // ---------- Bulk (FR-TSK-09, T-23, E-20, AC-NOT-06) ----------

    static async Task<object> Bulk(Guid id, BulkBody body, Access access, HubDb db, TeamService team, Notifier notify, SettingsStore store, TimeProvider clock)
    {
        var (p, ctx) = await access.Project(id);
        var s = await store.Get(db);
        var prm = body.Params is { } pe ? new Patch(pe) : new Patch(default);
        var tasks = await db.Tasks.Where(t => t.ProjectId == id && body.TaskIds.Contains(t.Id)).OrderBy(t => t.Seq).ToListAsync();
        var a = access.Actor;
        var updated = new List<WorkTask>();
        var skipped = new List<object>();
        var warnings = new List<string>();
        string? reason = body.Reason?.Trim();
        if (body.Operation is "shiftDueDates" or "hold" or "cancel") reason = Check.Reason(reason); // G-09 bulk date shift, hold, cancel
        db.Audit.Reason = reason;
        Guid? assignee = body.Operation == "assign" ? prm.Id("assigneeId") : null;
        if (assignee is { } au) await DeliverableEndpoints.ActivePerson(db, au, "assigneeId");
        Deliverable? deliverable = null;
        if (body.Operation == "setDeliverable" && prm.Id("deliverableId") is { } did)
            deliverable = await db.Deliverables.FirstOrDefaultAsync(d => d.Id == did && d.ProjectId == id) ?? throw ApiException.Invalid("deliverableId", "task.other_project");
        var dname = new Dictionary<Guid, string>();
        foreach (var t in tasks)
        {
            var facts = await Facts(db, t);
            Allow allow = body.Operation switch
            {
                "assign" => Permissions.AssignTask(a, ctx, facts),
                "setDueDate" or "shiftDueDates" => Permissions.ChangeDueDate(a, ctx, facts),
                "setPriority" or "setDeliverable" => Permissions.EditTask(a, ctx, facts),
                "hold" => Permissions.TaskTransition(a, ctx, facts, t.Status, TaskStatuses.OnHold),
                "cancel" => Permissions.TaskTransition(a, ctx, facts, t.Status, TaskStatuses.Cancelled),
                "transition" => Workflow.TaskPath(t.Status, prm.Str("toStatus") ?? "", t.RequiresReview, t.PreviousStatus) is not null ? Allow.Yes : Allow.No("task.illegal_transition"),
                _ => throw ApiException.Invalid("operation", "error.one_of", "assign, setDueDate, shiftDueDates, setPriority, setDeliverable, hold, cancel, transition"),
            };
            if (allow && body.Operation is "setDueDate" or "shiftDueDates" && Permissions.DueChangeNeedsReason(a, ctx, facts) && string.IsNullOrWhiteSpace(reason)) allow = Allow.No("error.reason_required");
            if (!allow) { skipped.Add(new { t.Id, t.Key, reason = Text.Get(allow.Why!, allow.Arg ?? (dname.TryGetValue(t.ProjectDisciplineId, out var n) ? n : dname[t.ProjectDisciplineId] = await DisciplineName(db, t.ProjectDisciplineId))) }); continue; }
            try
            {
                switch (body.Operation)
                {
                    case "assign":
                        if (assignee == t.AssigneeId) continue;
                        if (assignee is { } x && x == t.ReviewerId && !s.AllowSelfReview) throw ApiException.Invalid("assigneeId", "task.self_review");
                        t.AssigneeId = assignee;
                        break;
                    case "setDueDate":
                        var nd = prm.Date("dueDate");
                        if (nd != t.DueDate) { if (t.DueDate is not null) t.DueDateChangeCount++; t.DueDate = nd; }
                        break;
                    case "shiftDueDates":
                        var days = prm.Int("days") ?? throw ApiException.Invalid("days", "error.required");
                        if (t.DueDate is { } d) { t.DueDate = d.AddDays(days); t.DueDateChangeCount++; }
                        if (t.StartDate is { } st) t.StartDate = st.AddDays(days);
                        break;
                    case "setPriority":
                        var pr = prm.Str("priority"); Check.OneOf(pr, Priority.All, "priority"); t.Priority = pr!;
                        break;
                    case "setDeliverable":
                        t.DeliverableId = deliverable?.Id; if (deliverable is not null) t.MilestoneId = null;
                        break;
                    case "hold": await ApplyTransition(db, access, ctx, p, t, TaskStatuses.OnHold, reason, null, null, s, clock, team, warnings); break;
                    case "cancel": await ApplyTransition(db, access, ctx, p, t, TaskStatuses.Cancelled, reason, null, null, s, clock, team, warnings); break;
                    case "transition": await ApplyTransition(db, access, ctx, p, t, prm.Str("toStatus")!, reason, null, null, s, clock, team, warnings,
                        prm.Bool("acknowledgeReadiness") == true, store); break;
                }
                t.LastActivityAt = clock.GetUtcNow();
                if (t.StartDate is { } s1 && t.DueDate is { } d1 && s1 > d1) throw ApiException.Invalid("dueDate", "deliverable.start_after_due");
                updated.Add(t);
            }
            catch (ApiException e)
            {
                // A refused task keeps nothing from this attempt, including a start authorisation it would have used or recorded.
                foreach (var x in db.ChangeTracker.Entries().Where(x => x.Entity == t
                    || x.Entity is TaskStartAuthorisation sa && sa.TaskId == t.Id && x.State != EntityState.Unchanged).ToList())
                    if (x.State == EntityState.Added) x.State = EntityState.Detached; else x.Reload();
                skipped.Add(new { t.Id, t.Key, reason = e.Message });
            }
        }
        if (updated.Count > 0 && assignee is { } newA) await team.EnsureMember(p, newA, ProjectRole.TeamMember);
        await db.SaveChangesAsync();
        // One summary notification per recipient, not one per task (§17.5, AC-NOT-06).
        var actor = await notify.ActorName();
        var item = new NotifyItem(p.Id, ItemType.Project, p.Id, p.ProjectNumber, $"/projects/{p.ProjectNumber}/tasks", p.ProjectNumber);
        if (body.Operation == "assign" && assignee is { } to && updated.Count > 0)
            await notify.Send(NotificationEvents.TaskAssigned, to, item, Text.Get("notify.bulk_assigned", actor, updated.Count, p.ProjectNumber));
        if (body.Operation is "setDueDate" or "shiftDueDates")
            foreach (var g in updated.GroupBy(t => t.AssigneeId).Where(g => g.Key is not null))
                await notify.Send(NotificationEvents.DueDateChanged, g.Key, item, Text.Get("notify.bulk_due", actor, g.Count(), p.ProjectNumber), reason);
        await db.SaveChangesAsync();
        return new { updated = updated.Count, skipped, warnings };
    }

    public static NotifyItem Item(Project p, WorkTask t) => new(p.Id, ItemType.Task, t.Id, t.Key, $"/projects/{p.ProjectNumber}/tasks?panel=Task:{t.Id}", p.ProjectNumber);
}
