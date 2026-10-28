using Microsoft.AspNetCore.Http.HttpResults;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Task pickup rules and route handlers. HubModules.Map must call Map(api) from the integration slice.
public static class TaskPickupEndpoints
{
    public sealed record OfferBody(string Reason, decimal? HoursRemaining, DateOnly? NeededBy, string? Note, bool? KeepWatching = null);
    public sealed record PickupBody(int? RowVersion);
    public sealed record AssignBody(Guid UserId, int? RowVersion);
    public sealed record OfferCreated(Guid TaskId);
    public sealed record PickupResult(Guid Id, Guid? AssigneeId, string? CloseCause);
    public sealed record LoadResult(DateOnly Week, string? Band, decimal? Hours, decimal Capacity, bool Partial);
    public sealed record ListTask(string Key, string Name, Guid ProjectId, string ProjectNumber, Guid ProjectDisciplineId, string DisciplineName, DateOnly? DueDate, int RowVersion);
    public sealed record ListRow(Guid Id, Guid TaskId, Guid OfferedById, string OfferedBy, string Reason, decimal? HoursRemaining, DateOnly? NeededBy, string? Note, DateTimeOffset OpenedAt, ListTask Task, bool CanPickUp, bool CanAssign);
    public sealed record DetailTask(string Key, string Name, Guid ProjectId, Guid ProjectDisciplineId, DateOnly? DueDate);
    public sealed record Allowed(bool PickUp, bool Assign, bool Withdraw);
    public sealed record Candidate(Guid Id, string? DisplayName);
    public sealed record DetailResult(Guid Id, string Reason, string? Note, DateTimeOffset OpenedAt, bool KeepWatching, int RowVersion,
        DetailTask Task, Allowed Allowed, IReadOnlyList<Candidate> Candidates);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/tasks/{id:guid}/pickup-offer", Offer).AddEndpointFilter<PickupCommandFilter>();
        api.MapDelete("/tasks/{id:guid}/pickup-offer", Withdraw).AddEndpointFilter<PickupCommandFilter>();
        api.MapPost("/tasks/{id:guid}/pickup", PickUp).AddEndpointFilter<PickupCommandFilter>();
        api.MapPost("/tasks/{id:guid}/pickup-offer/assign", Assign).AddEndpointFilter<PickupCommandFilter>();
        api.MapGet("/tasks/{id:guid}/pickup-offer", Detail);
        api.MapGet("/pickup-offers", List).RequireFlag("task_pickup");
        api.MapGet("/pickup-offers/my-load", async (DateOnly? week, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            Check.That(week == null || week.Value.Year is >= 1900 and <= 9998, "week", "error.date_range");
            var (weeks, people, _) = await WorkloadEndpoints.Grid(new(null, null, null, null, null, week, null), access, db, me, store, clock, ownOnly: true);
            var person = people.SingleOrDefault() ?? throw ApiException.NotFound();
            var cell = person.Cells[0];
            var visible = access.VisibleProjectIds();
            var live = db.Projects.Where(p => WorkloadEndpoints.LiveProjects.Contains(p.Status)).Select(p => p.Id);
            var partial = !access.SeesAllRestricted && (await db.Tasks.AnyAsync(t => t.AssigneeId == me.Id && live.Contains(t.ProjectId)
                && !visible.Contains(t.ProjectId) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold)
                || await db.Allocations.AnyAsync(a => a.PersonId == me.Id && a.Status == AllocationStatus.Confirmed && live.Contains(a.ProjectId)
                    && !visible.Contains(a.ProjectId) && a.FromDate <= weeks[0].AddDays(6) && a.ThroughDate >= weeks[0]));
            return TypedResults.Ok(new LoadResult(weeks[0], partial ? null : cell.Band.ToString().ToLowerInvariant(),
                partial ? (decimal?)null : cell.Committed, cell.Available, partial));
        }).RequireFlag("task_pickup");
    }

    static async Task<bool> Enabled(HubDb db, SettingsStore store) => (await store.Get(db)).IsFlagOn("task_pickup");

    static async Task<Created<OfferCreated>> Offer(Guid id, OfferBody body, Access access, HubDb db, CurrentUser me, TimeProvider clock, SettingsStore store, Notifier notify)
    {
        if (!await Enabled(db, store)) throw ApiException.NotFound();
        var (task, project, ctx) = await TaskEndpoints.Load(db, access, id);
        if (task.Status is TaskStatuses.Complete or TaskStatuses.Cancelled || task.DeletedAt is not null)
            throw ApiException.Rule("task_closed", "task.pickup_closed");
        var assignee = task.AssigneeId is { } aid ? await db.Users.FirstOrDefaultAsync(u => u.Id == aid) : null;
        var canOffer = task.AssigneeId == me.Id || TaskPickupRules.CanAssign(access.Actor, ctx, task.ProjectDisciplineId, assignee?.SupervisorId);
        Access.Demand(canOffer ? Allow.Yes : Allow.No("perm.pickup_offer"));
        Check.That(!string.IsNullOrWhiteSpace(body.Reason) && body.Reason.Trim().Length <= 2000, "reason", "error.required");
        Check.That(body.Note is null or { Length: <= 4000 }, "note", "error.length");
        Check.That(body.HoursRemaining is null || body.HoursRemaining is >= 0 and <= 99999.99m && body.HoursRemaining == Math.Round(body.HoursRemaining.Value, 2), "hoursRemaining", "error.range");
        Check.That(!await db.Set<TaskPickupOffer>().AnyAsync(x => x.TaskId == id && x.ClosedAt == null), "taskId", "task.pickup_already_open");
        var now = clock.GetUtcNow();
        db.Set<TaskPickupOffer>().Add(new TaskPickupOffer
        {
            TaskId = id, OfferedById = me.Id, AssigneeId = task.AssigneeId, Reason = body.Reason.Trim(),
            HoursRemaining = body.HoursRemaining, NeededBy = body.NeededBy, Note = body.Note?.Trim(), KeepWatching = body.KeepWatching ?? true, OpenedAt = now,
        });
        db.Audit.Reason = body.Reason.Trim();
        db.LogEvent(ItemType.Task, task.Id, "PickupOffered", "assignment", project.Id, task.Key, task.Name,
            new[] { new { field = "PickupOffer", old = (object?)null, @new = body.Reason.Trim() } }, reason: body.Reason.Trim(), disciplineId: task.ProjectDisciplineId);
        Check.That(assignee is { IsActive: true }, "assigneeId", "coord.person");
        var structure = (await store.Get(db)).IsFlagOn("org_structure");
        var peers = await db.Users.Where(u => u.IsActive).ToListAsync();
        var recipients = peers.Where(u => TaskPickupRules.SameTeam(structure, assignee!.UnitId, u.UnitId, assignee.SupervisorId, u.SupervisorId, assignee.IsActive, u.IsActive)).Select(u => (Guid?)u.Id).ToList();
        recipients.Add(assignee!.SupervisorId);
        recipients.Add(assignee.Id);
        recipients.Add(project.ProjectManagerId);
        var lead = await db.ProjectDisciplines.Where(d => d.Id == task.ProjectDisciplineId).Select(d => d.LeadUserId).FirstOrDefaultAsync(); recipients.Add(lead);
        await notify.Send(NotificationEvents.TaskPickupOffered, recipients, TaskEndpoints.Item(project, task), Text.Get("notify.task_pickup_offered", task.Key, task.Name), body.Reason.Trim());
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/tasks/{id}/pickup-offer", new OfferCreated(id));
    }

    static async Task<NoContent> Withdraw(Guid id, Access access, HubDb db, CurrentUser me, TimeProvider clock, SettingsStore store, Notifier notify)
    {
        if (!await Enabled(db, store)) throw ApiException.NotFound();
        var (task, _, ctx) = await TaskEndpoints.Load(db, access, id);
        var offer = await db.Set<TaskPickupOffer>().FirstOrDefaultAsync(x => x.TaskId == id && x.ClosedAt == null) ?? throw ApiException.NotFound();
        Access.Demand(TaskPickupRules.CanWithdraw(access.Actor, ctx, task.ProjectDisciplineId, offer) ? Allow.Yes : Allow.No("perm.pickup_withdraw"));
        offer.ClosedAt = clock.GetUtcNow(); offer.CloseCause = TaskPickupOffer.Withdrawn;
        var withdrawn = TaskPickupRules.CauseText(TaskPickupOffer.Withdrawn);
        db.Audit.Reason = withdrawn;
        db.LogEvent(ItemType.Task, task.Id, "PickupWithdrawn", "assignment", task.ProjectId, task.Key, task.Name, reason: withdrawn, disciplineId: task.ProjectDisciplineId);
        await notify.Send(NotificationEvents.TaskPickupClosed, offer.OfferedById, TaskEndpoints.Item(await db.Projects.FirstAsync(p => p.Id == task.ProjectId), task), Text.Get("notify.task_pickup_closed", task.Key, task.Name), withdrawn);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    static async Task<Ok<PickupResult>> PickUp(Guid id, PickupBody body, HttpContext http, Access access, HubDb db, CurrentUser me, TeamService team, TimeProvider clock, SettingsStore store, Notifier notify)
    {
        if (!await Enabled(db, store)) throw ApiException.NotFound();
        var (task, project, ctx) = await TaskEndpoints.Load(db, access, id);
        await Http.CheckVersion(db, http, task, body.RowVersion);
        var offer = await db.Set<TaskPickupOffer>().FirstOrDefaultAsync(x => x.TaskId == id && x.ClosedAt == null) ?? throw ApiException.NotFound();
        var assignee = task.AssigneeId is { } aid ? await db.Users.FirstOrDefaultAsync(u => u.Id == aid) : null;
        var picker = await db.Users.FirstOrDefaultAsync(u => u.Id == me.Id && u.IsActive) ?? throw ApiException.Forbidden("perm.inactive");
        var orgStructure = (await store.Get(db)).IsFlagOn("org_structure");
        Access.Demand(assignee is not null && TaskPickupRules.SameTeam(orgStructure, assignee.UnitId, picker.UnitId, assignee.SupervisorId, picker.SupervisorId, assignee.IsActive, picker.IsActive) && picker.Id != assignee.Id
            ? Allow.Yes : Allow.No("perm.pickup_team"));
        var old = task.AssigneeId;
        await Tx.Run<int>(db, async () =>
        {
            await TaskEndpoints.AssignTo(db, project, task, picker.Id, team, await store.Get(db));
            offer.ClosedAt = clock.GetUtcNow(); offer.CloseCause = TaskPickupOffer.PickedUp;
            if (old is { } previous && offer.KeepWatching) await TaskEndpoints.Watch(db, project.Id, ItemType.Task, task.Id, previous, "Pickup");
            db.Audit.Reason = offer.Reason;
            db.Audit.Note(task, action: "TaskPickedUp", reason: offer.Reason);
            var manager = old is { } oa ? await db.Users.Where(u => u.Id == oa).Select(u => u.SupervisorId).FirstOrDefaultAsync() : null;
            var lead = await db.ProjectDisciplines.Where(d => d.Id == task.ProjectDisciplineId).Select(d => d.LeadUserId).FirstOrDefaultAsync();
            await notify.Send(NotificationEvents.TaskPickedUp, new Guid?[] { old, offer.OfferedById, manager, project.ProjectManagerId, lead }, TaskEndpoints.Item(project, task), Text.Get("notify.task_picked_up", task.Key, task.Name));
            return await db.SaveChangesAsync();
        });
        return TypedResults.Ok(new PickupResult(task.Id, task.AssigneeId, offer.CloseCause));
    }

    static async Task<Ok<PickupResult>> Assign(Guid id, AssignBody body, HttpContext http, Access access, HubDb db, TeamService team, TimeProvider clock, SettingsStore store, Notifier notify)
    {
        if (!await Enabled(db, store)) throw ApiException.NotFound();
        var (task, project, ctx) = await TaskEndpoints.Load(db, access, id);
        await Http.CheckVersion(db, http, task, body.RowVersion);
        var offer = await db.Set<TaskPickupOffer>().FirstOrDefaultAsync(x => x.TaskId == id && x.ClosedAt == null) ?? throw ApiException.NotFound();
        var assignee = task.AssigneeId is { } aid ? await db.Users.FirstOrDefaultAsync(u => u.Id == aid) : null;
        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == body.UserId && u.IsActive) ?? throw ApiException.Invalid("userId", "error.not_found");
        Access.Demand(TaskPickupRules.CanAssign(access.Actor, ctx, task.ProjectDisciplineId, assignee?.SupervisorId)
            ? Allow.Yes : Allow.No("perm.pickup_assign"));
        var orgStructure = (await store.Get(db)).IsFlagOn("org_structure");
        Access.Demand(assignee is not null && TaskPickupRules.SameTeam(orgStructure, assignee.UnitId, target.UnitId, assignee.SupervisorId, target.SupervisorId, assignee.IsActive, target.IsActive)
            ? Allow.Yes : Allow.No("perm.pickup_team"));
        var old = task.AssigneeId;
        await Tx.Run<int>(db, async () =>
        {
            await TaskEndpoints.AssignTo(db, project, task, target.Id, team, await store.Get(db));
            offer.ClosedAt = clock.GetUtcNow(); offer.CloseCause = TaskPickupOffer.AssignedByManager;
            db.Audit.Reason = offer.Reason;
            db.Audit.Note(task, action: "TaskAssignedFromPickup", reason: offer.Reason);
            await notify.Send(NotificationEvents.TaskPickupAssigned, new Guid?[] { target.Id, old, offer.OfferedById }, TaskEndpoints.Item(project, task), Text.Get("notify.task_pickup_assigned", task.Key, task.Name),
                assignedTo: [target.Id]);
            return await db.SaveChangesAsync();
        });
        return TypedResults.Ok(new PickupResult(task.Id, task.AssigneeId, offer.CloseCause));
    }

    static async Task<Ok<IReadOnlyList<ListRow>>> List(Access access, HubDb db, CurrentUser me, DateOnly? neededBy, Guid? projectId, Guid? disciplineId, Guid? taskId, SettingsStore store)
    {
        if (!await Enabled(db, store)) return TypedResults.Ok<IReadOnlyList<ListRow>>([]);
        if (access.Actor.ReadOnly) return TypedResults.Ok<IReadOnlyList<ListRow>>([]);
        var visible = access.VisibleProjectIds();
        var q = db.Set<TaskPickupOffer>().AsNoTracking().Where(o => o.ClosedAt == null && visible.Contains(db.Tasks.Where(t => t.Id == o.TaskId).Select(t => t.ProjectId).FirstOrDefault()));
        if (projectId is { } pid) q = q.Where(o => db.Tasks.Any(t => t.Id == o.TaskId && t.ProjectId == pid));
        if (disciplineId is { } did) q = q.Where(o => db.Tasks.Any(t => t.Id == o.TaskId && t.ProjectDisciplineId == did));
        if (taskId is { } tid) q = q.Where(o => o.TaskId == tid);
        if (neededBy is { } date) q = q.Where(o => o.NeededBy == date);
        var viewer = await db.Users.AsNoTracking().Where(u => u.Id == me.Id).Select(u => new { u.Id, u.UnitId, u.SupervisorId, u.IsActive }).FirstAsync();
        var orgStructure = (await store.Get(db)).IsFlagOn("org_structure");
        var rows = await q.OrderBy(o => o.NeededBy).ThenBy(o => db.Tasks.Where(t => t.Id == o.TaskId).Select(t => t.DueDate).FirstOrDefault()).ThenBy(o => o.OpenedAt).Select(o => new
        {
            o.Id, o.TaskId, o.OfferedById, OfferedBy = db.Users.Where(u => u.Id == o.OfferedById).Select(u => u.DisplayName).First(),
            o.Reason, o.HoursRemaining, o.NeededBy, o.Note, o.OpenedAt,
            Task = db.Tasks.Where(t => t.Id == o.TaskId).Select(t => new { t.Key, t.Name, t.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.ProjectNumber).First(), t.ProjectDisciplineId, DisciplineName = db.ProjectDisciplines.Where(d => d.Id == t.ProjectDisciplineId).Select(d => d.Discipline!.Name).First(), t.DueDate, t.AssigneeId, t.RowVersion }).First(),
        }).ToListAsync();
        var assignees = await db.Users.AsNoTracking().Where(u => rows.Select(r => r.Task.AssigneeId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);
        // FR-PKP-04: a row is listed for the assignee's team (Pick up) and for those who may assign it (Assign).
        var contexts = new Dictionary<Guid, ProjectContext>();
        foreach (var project in await db.Projects.AsNoTracking().Where(p => rows.Select(r => r.Task.ProjectId).Contains(p.Id)).ToListAsync())
            contexts[project.Id] = await access.Context(project);
        var result = new List<ListRow>();
        foreach (var row in rows)
        {
            if (row.Task.AssigneeId is not { } aid || !assignees.TryGetValue(aid, out var assignee) || aid == viewer.Id) continue;
            var canPickUp = TaskPickupRules.SameTeam(orgStructure, assignee.UnitId, viewer.UnitId, assignee.SupervisorId, viewer.SupervisorId, assignee.IsActive, viewer.IsActive);
            var canAssign = TaskPickupRules.CanAssign(access.Actor, contexts[row.Task.ProjectId], row.Task.ProjectDisciplineId, assignee.SupervisorId);
            if (!canPickUp && !canAssign) continue;
            result.Add(new ListRow(row.Id, row.TaskId, row.OfferedById, row.OfferedBy, row.Reason, row.HoursRemaining, row.NeededBy, row.Note, row.OpenedAt,
                new ListTask(row.Task.Key, row.Task.Name, row.Task.ProjectId, row.Task.ProjectNumber, row.Task.ProjectDisciplineId, row.Task.DisciplineName, row.Task.DueDate, row.Task.RowVersion),
                canPickUp, canAssign));
        }
        return TypedResults.Ok<IReadOnlyList<ListRow>>(result);
    }

    static async Task<Ok<DetailResult>> Detail(Guid id, Access access, HubDb db, CurrentUser me, SettingsStore store)
    {
        if (!await Enabled(db, store)) throw ApiException.NotFound();
        var (task, project, ctx) = await TaskEndpoints.Load(db, access, id);
        var offer = await db.Set<TaskPickupOffer>().AsNoTracking().FirstOrDefaultAsync(x => x.TaskId == id && x.ClosedAt == null) ?? throw ApiException.NotFound();
        var viewer = await db.Users.AsNoTracking().FirstAsync(u => u.Id == me.Id);
        var owner = task.AssigneeId is { } ownerId ? await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ownerId) : null;
        var org = (await store.Get(db)).IsFlagOn("org_structure");
        var sameTeam = owner is not null && owner.Id != me.Id && TaskPickupRules.SameTeam(org, owner.UnitId, viewer.UnitId, owner.SupervisorId, viewer.SupervisorId, owner.IsActive, viewer.IsActive);
        var canAssign = TaskPickupRules.CanAssign(access.Actor, ctx, task.ProjectDisciplineId, owner?.SupervisorId);
        var candidates = canAssign ? await db.Users.AsNoTracking().Where(u => u.IsActive && u.Id != task.AssigneeId).ToListAsync() : [];
        var eligible = candidates.Where(u => owner is not null && TaskPickupRules.SameTeam(org, owner.UnitId, u.UnitId, owner.SupervisorId, u.SupervisorId, owner.IsActive, u.IsActive))
            .Select(u => new { u.Id, u.DisplayName }).ToList();
        return TypedResults.Ok(new DetailResult(offer.Id, offer.Reason, offer.Note, offer.OpenedAt, offer.KeepWatching, task.RowVersion,
            new DetailTask(task.Key, task.Name, task.ProjectId, task.ProjectDisciplineId, task.DueDate),
            new Allowed(sameTeam && Permissions.Writable(access.Actor, ctx).Ok, canAssign && Permissions.Writable(access.Actor, ctx).Ok,
                Permissions.Writable(access.Actor, ctx).Ok && TaskPickupRules.CanWithdraw(access.Actor, ctx, task.ProjectDisciplineId, offer)),
            eligible.Select(u => new Candidate(u.Id, u.DisplayName)).ToList()));
    }

}

public static class TaskPickupRules
{
    /// A close cause as people read it; the stored value stays the code (notifications, history, the pickup report).
    public static string CauseText(string? cause) => cause switch
    {
        TaskPickupOffer.PickedUp => Text.Get("pickup.cause.picked_up"), TaskPickupOffer.AssignedByManager => Text.Get("pickup.cause.assigned_by_manager"),
        TaskPickupOffer.Withdrawn => Text.Get("pickup.cause.withdrawn"), TaskPickupOffer.TaskClosed => Text.Get("pickup.cause.task_closed"),
        TaskPickupOffer.ReassignedElsewhere => Text.Get("pickup.cause.reassigned_elsewhere"), null => "", _ => cause,
    };

    /// FR-PKP-07: the assignee's direct manager, the project's PM or the task discipline's Lead may assign an offered task.
    public static bool CanAssign(Actor actor, ProjectContext ctx, Guid disciplineId, Guid? assigneeSupervisorId) =>
        Permissions.IsPM(actor, ctx) || Permissions.IsDL(ctx, disciplineId) || actor.Supervisor && assigneeSupervisorId == actor.Id;

    /// FR-PKP-03: the offerer, the assignee, the PM or the Discipline Lead; a direct manager may assign an offer but not withdraw it.
    public static bool CanWithdraw(Actor actor, ProjectContext ctx, Guid disciplineId, TaskPickupOffer offer) =>
        offer.OfferedById == actor.Id || offer.AssigneeId == actor.Id || Permissions.IsPM(actor, ctx) || Permissions.IsDL(ctx, disciplineId);

    /// Fallback team rule for the pre-org-structure schema. Root can use the UnitId overload when enabled.
    public static bool SameTeam(bool orgStructureOn, Guid? assigneeUnitId, Guid? pickerUnitId, Guid? assigneeSupervisorId, Guid? pickerSupervisorId, bool assigneeActive, bool pickerActive)
        => assigneeActive && pickerActive && (orgStructureOn && assigneeUnitId is { } unit
            ? pickerUnitId == unit
            : assigneeSupervisorId is { } supervisor && pickerSupervisorId == supervisor);
}

public static class TaskPickupLifecycle
{
    public static async Task BeforeSave(HubDb db, DateTimeOffset now, Notifier notify, CancellationToken ct = default)
    {
        var closedProjects = db.ChangeTracker.Entries<Project>().Where(e => e.State == EntityState.Modified
            && e.Property(p => p.Status).IsModified && e.Entity.Status is ProjectStatus.Complete or ProjectStatus.Archived).Select(e => e.Entity.Id).ToArray();
        var projectOffers = await db.Set<TaskPickupOffer>().Where(o => o.ClosedAt == null && db.Tasks.Any(t => t.Id == o.TaskId && closedProjects.Contains(t.ProjectId))).ToListAsync(ct);
        foreach (var offer in projectOffers)
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == offer.TaskId, ct);
            offer.ClosedAt = now; offer.CloseCause = TaskPickupOffer.TaskClosed;
            var project = await db.Projects.SingleAsync(p => p.Id == task.ProjectId, ct);
            await notify.Send(NotificationEvents.TaskPickupClosed, offer.OfferedById, TaskEndpoints.Item(project, task),
                Text.Get("notify.task_pickup_closed", task.Key, task.Name), Text.Get("pickup.cause.project_closed"));
            db.LogEvent(ItemType.Task, task.Id, "PickupClosed", "assignment", task.ProjectId, task.Key, task.Name, reason: Text.Get("pickup.cause.project_closed"), disciplineId: task.ProjectDisciplineId);
        }
        var changed = db.ChangeTracker.Entries<WorkTask>().Where(e => e.State is EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in changed)
        {
            var deleted = entry.Entity.DeletedAt is not null && entry.Property(x => x.DeletedAt).IsModified;
            var statusClosed = (entry.Entity.Status is TaskStatuses.Complete or TaskStatuses.Cancelled) && entry.Property(x => x.Status).IsModified;
            var reassigned = entry.Property(x => x.AssigneeId).IsModified && entry.Property(x => x.AssigneeId).OriginalValue != entry.Entity.AssigneeId;
            if (!deleted && !statusClosed && !reassigned) continue;
            var cause = deleted || statusClosed ? TaskPickupOffer.TaskClosed : TaskPickupOffer.ReassignedElsewhere;
            var offers = await db.Set<TaskPickupOffer>().Where(o => o.TaskId == entry.Entity.Id && o.ClosedAt == null).ToListAsync(ct);
            foreach (var offer in offers.Where(o => o.ClosedAt == null))
            {
                offer.ClosedAt = now; offer.CloseCause = cause;
                var project = await db.Projects.SingleAsync(p => p.Id == entry.Entity.ProjectId, ct);
                await notify.Send(NotificationEvents.TaskPickupClosed, offer.OfferedById, TaskEndpoints.Item(project, entry.Entity),
                    Text.Get("notify.task_pickup_closed", entry.Entity.Key, entry.Entity.Name), TaskPickupRules.CauseText(cause));
                db.LogEvent(ItemType.Task, entry.Entity.Id, "PickupClosed", "assignment", entry.Entity.ProjectId, entry.Entity.Key, entry.Entity.Name,
                    reason: TaskPickupRules.CauseText(cause), disciplineId: entry.Entity.ProjectDisciplineId);
            }
        }
    }
}

public static class TaskPickupReporting
{
    public static IQueryable<TaskPickupOffer> Rows(HubDb db, IQueryable<Guid> visibleProjects) => db.Set<TaskPickupOffer>().AsNoTracking()
        .Where(o => visibleProjects.Contains(db.Tasks.IgnoreQueryFilters().Where(t => t.Id == o.TaskId).Select(t => t.ProjectId).FirstOrDefault()));

    /// FR-PKP-12 under the list's rules (FR-PKP-04): the team of the offer's assignee (not the task's current one, so a pick-up or
    /// reassignment never moves an offer's history to a team that never saw it), and those who may assign it (direct manager, PM,
    /// Discipline Lead). A since-deactivated assignee's offers stay with their team.
    public static async Task<List<T>> Visible<T>(HubDb db, Access access, AppUser viewer, bool orgStructureOn, IReadOnlyList<T> rows, Func<T, (Guid Assignee, Guid Project, Guid Discipline)> key)
    {
        var keys = rows.Select(key).ToList();
        var ids = keys.Select(k => k.Assignee).Distinct().ToArray();
        var projectIds = keys.Select(k => k.Project).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var contexts = new Dictionary<Guid, ProjectContext>();
        foreach (var project in await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToListAsync())
            contexts[project.Id] = await access.Context(project);
        return rows.Where((_, i) => keys[i] is var (a, p, d) && a != viewer.Id && people.TryGetValue(a, out var u)
            && (TaskPickupRules.SameTeam(orgStructureOn, u.UnitId, viewer.UnitId, u.SupervisorId, viewer.SupervisorId, true, viewer.IsActive)
                || contexts.TryGetValue(p, out var ctx) && TaskPickupRules.CanAssign(access.Actor, ctx, d, u.SupervisorId))).ToList();
    }
}

/// Serialize offer state with task changes before handlers read either row; an offer never races a terminal save.
public sealed class PickupCommandFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var sp = context.HttpContext.RequestServices;
        var db = sp.GetRequiredService<HubDb>();
        var access = sp.GetRequiredService<Access>();
        Http.RequireFlag(await sp.GetRequiredService<SettingsStore>().Get(db), "task_pickup");
        var id = Guid.Parse(context.HttpContext.Request.RouteValues["id"]!.ToString()!);
        var projectId = await db.Tasks.Where(t => t.Id == id).Select(t => (Guid?)t.ProjectId).SingleOrDefaultAsync() ?? throw ApiException.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync();
        await Coordination.Lock(db, projectId);
        await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM hub.task WHERE id={id} FOR UPDATE").ToListAsync();
        var (_, ctx) = await access.Project(projectId, false);
        Access.Demand(Permissions.Writable(access.Actor, ctx));
        var result = await next(context);
        await tx.CommitAsync();
        return result;
    }
}
