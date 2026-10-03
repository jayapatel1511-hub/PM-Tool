using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Task time entries (§36.8 FR-VIS-10, AC-VIS-08): actual effort recorded against tasks by the person who did it. Hours
/// stay apart from estimates, progress, health, workload, billing and payroll (FR-005): nothing here writes to them.
public static class TimeEndpoints
{
    public sealed record EntryBody(Guid TaskId, DateOnly WorkDate, decimal Hours, string? Note);
    public sealed record TimeQuery(DateOnly? From, DateOnly? To, Guid? ProjectId, Guid? TaskId, Guid? UserId, string? Scope);
    public sealed record ReasonBody(string? Reason, int? RowVersion);

    static readonly Col[] Cols =
    [
        new("workDate", "date", "date"), new("person", "person"), new("projectNumber", "project"), new("taskKey", "key"), new("taskName", "task"),
        new("hours", "hours", "number"), new("note", "note"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/time", async ([AsParameters] TimeQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (from, to) = await Range(f, db, store, clock);
            var rows = await Visible(f with { From = from, To = to }, access, db, me);
            return new
            {
                From = from, To = to, Entries = rows,
                Total = rows.Sum(r => r.Hours),
                ByDay = rows.GroupBy(r => r.WorkDate).OrderBy(g => g.Key).Select(g => new { Date = g.Key, Hours = g.Sum(r => r.Hours) }),
                ByTask = rows.GroupBy(r => (r.TaskId, r.TaskKey, r.TaskName, r.ProjectNumber)).Select(g => new { g.Key.TaskId, g.Key.TaskKey, g.Key.TaskName, g.Key.ProjectNumber, Hours = g.Sum(r => r.Hours) }).OrderBy(x => x.TaskKey),
                ByProject = rows.GroupBy(r => (r.ProjectId, r.ProjectNumber, r.ProjectName)).Select(g => new { g.Key.ProjectId, g.Key.ProjectNumber, g.Key.ProjectName, Hours = g.Sum(r => r.Hours) }).OrderBy(x => x.ProjectNumber),
                CanReview = await CanReview(access, db, me),
            };
        });
        api.MapGet("/time/export", async (string? format, [AsParameters] TimeQuery f, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (from, to) = await Range(f, db, store, clock);
            var (rows, parameters) = await ExportRows(f with { From = from, To = to }, access, db, me, http.Request.Query);
            var auditProjectId = f.ProjectId;
            if (auditProjectId is { } requestedProjectId
                && !await access.VisibleProjects().AnyAsync(p => p.Id == requestedProjectId))
                auditProjectId = null;
            return await ExportFile.Send(db, store, format, Text.Get("nav.time"), Cols, rows, parameters, auditProjectId, "task-hours", clock);
        });
        api.MapPost("/time", Create);
        api.MapPatch("/time/{id:guid}", Edit);
        api.MapDelete("/time/{id:guid}", async (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] ReasonBody? body, HttpContext http, Access access, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var (e, _, ctx) = await Load(db, access, id);
            var (allow, needsReason) = Permissions.EditTime(access.Actor, ctx, e.UserId);
            Access.Demand(allow);
            await Http.CheckVersion(db, http, e, body?.RowVersion);
            if (needsReason) db.Audit.Reason = Check.Reason(body?.Reason); // a PM correcting someone else's entry
            e.DeletedAt = clock.GetUtcNow();
            e.DeletedBy = me.Id;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    internal static async Task<(DateOnly From, DateOnly To)> Range(TimeQuery f, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var today = clock.Today(await store.Get(db));
        var from = f.From ?? Workload.WeekOf(today);
        var to = f.To ?? from.AddDays(6);
        Check.That(to >= from && to.DayNumber - from.DayNumber <= 370, "to", "time.range");
        return (from, to);
    }

    public sealed record Row(Guid Id, Guid ProjectId, string ProjectNumber, string ProjectName, Guid TaskId, string TaskKey, string TaskName, Guid UserId, string Person,
        DateOnly WorkDate, decimal Hours, string? Note, int RowVersion, bool CanEdit, bool EditNeedsReason);

    /// Someone who may see anyone else's entries: a PM or lead of a visible project, or a Supervisor.
    static async Task<bool> CanReview(Access access, HubDb db, CurrentUser me)
    {
        var a = access.Actor;
        if (a.Supervisor || a.Admin || a.Executive) return true;
        return await access.VisibleProjects().AnyAsync(p => p.ProjectManagerId == me.Id
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == me.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM))
            || db.ProjectDisciplines.Any(pd => pd.ProjectId == p.Id && pd.LeadUserId == me.Id));
    }

    /// FR-004 / §36.8: the caller's own entries, plus entries they may review — PMs their project's, leads their discipline's,
    /// Supervisors their direct reports' on projects they can see. Nothing from a project the caller cannot see.
    public static async Task<List<Row>> Visible(TimeQuery f, Access access, HubDb db, CurrentUser me)
    {
        var projects = access.VisibleProjects();
        if (f.ProjectId is { } pid) projects = projects.Where(p => p.Id == pid);
        var ids = projects.Select(p => p.Id);
        // DeletedAt is tested here as well: the task lookup below ignores query filters (deleted tasks keep their hours),
        // and EF applies IgnoreQueryFilters to the whole query, this entity's soft-delete filter included.
        var q = db.TimeEntries.AsNoTracking().Where(e => e.DeletedAt == null && ids.Contains(e.ProjectId) && e.WorkDate >= f.From && e.WorkDate <= f.To);
        if (f.TaskId is { } tid) q = q.Where(e => e.TaskId == tid);
        if (f.UserId is { } uid) q = q.Where(e => e.UserId == uid);
        if (f.Scope != "all") q = q.Where(e => e.UserId == me.Id);
        var rows = await q.Select(e => new
        {
            e.Id, e.ProjectId, e.TaskId, e.UserId, e.WorkDate, e.Hours, e.Note, e.RowVersion,
            Task = db.Tasks.IgnoreQueryFilters().Where(t => t.Id == e.TaskId).Select(t => new { t.Key, t.Name, t.ProjectDisciplineId }).First(),
            Person = db.Users.Where(u => u.Id == e.UserId).Select(u => new { u.DisplayName, u.SupervisorId }).First(),
        }).OrderBy(e => e.WorkDate).ThenBy(e => e.Task.Key).ToListAsync();
        var contexts = new Dictionary<Guid, (Project P, ProjectContext Ctx)>();
        var result = new List<Row>();
        foreach (var r in rows)
        {
            if (!contexts.TryGetValue(r.ProjectId, out var pc)) contexts[r.ProjectId] = pc = await access.Project(r.ProjectId, track: false);
            if (r.UserId != me.Id && !Permissions.ViewTimeEntry(access.Actor, pc.Ctx, r.UserId, r.Task.ProjectDisciplineId, r.Person.SupervisorId)) continue;
            var (allow, reason) = Permissions.EditTime(access.Actor, pc.Ctx, r.UserId);
            result.Add(new Row(r.Id, r.ProjectId, pc.P.ProjectNumber, pc.P.Name, r.TaskId, r.Task.Key, r.Task.Name, r.UserId, r.Person.DisplayName, r.WorkDate, r.Hours, r.Note,
                r.RowVersion, allow.Ok, reason));
        }
        return result;
    }

    /// The export holds exactly the visible rows and a total that equals their sum (§19, SC-002).
    public static async Task<(JsonArray Rows, List<(string, string)> Parameters)> ExportRows(TimeQuery f, Access access, HubDb db, CurrentUser me, IQueryCollection query)
    {
        var rows = await Visible(f, access, db, me);
        var json = JsonSerializer.SerializeToNode(rows.Select(r => new { r.WorkDate, r.Person, r.ProjectNumber, r.TaskKey, r.TaskName, r.Hours, r.Note }), JsonOpts.Web)!.AsArray();
        json.Add(new JsonObject { ["person"] = Text.Get("time.total"), ["hours"] = rows.Sum(r => r.Hours) });
        var parameters = new List<(string, string)> { (Text.Get("param.from"), f.From!.Value.ToString("yyyy-MM-dd")), (Text.Get("param.to"), f.To!.Value.ToString("yyyy-MM-dd")) };
        parameters.Add((Text.Get("time.entries"), Text.Get(f.Scope == "all" ? "time.scope.all" : "time.scope.mine")));
        parameters.AddRange(query.Where(k => k.Key is "projectId" or "taskId" or "userId" && !string.IsNullOrWhiteSpace(k.Value)).Select(k => (Text.Get($"param.{k.Key}"), k.Value.ToString())));
        return (json, parameters);
    }

    static async Task<(TaskTimeEntry E, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var e = await db.TimeEntries.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(e.ProjectId, track: false);
        var task = await db.Tasks.IgnoreQueryFilters().Where(t => t.Id == e.TaskId).Select(t => t.ProjectDisciplineId).FirstAsync();
        var supervisor = await db.Users.Where(u => u.Id == e.UserId).Select(u => u.SupervisorId).FirstAsync();
        if (e.UserId != access.Me.Id && !Permissions.ViewTimeEntry(access.Actor, ctx, e.UserId, task, supervisor)) throw ApiException.NotFound();
        e.AuditKey = await db.Tasks.IgnoreQueryFilters().Where(t => t.Id == e.TaskId).Select(t => t.Key).FirstAsync();
        return (e, p, ctx);
    }

    static decimal Hours(decimal h, string field)
    {
        Check.That(h > 0 && h <= 24, field, "time.hours");
        Check.That(decimal.Round(h, 2) == h, field, "time.hours_precision");
        return h;
    }

    /// FR-002, SC-003: one person's non-deleted hours on one date stay within 24, even for simultaneous saves — the check and the
    /// write share a transaction holding a lock for that person and date.
    static async Task DailyCap(HubDb db, Guid userId, DateOnly date, decimal adding, Guid? except)
    {
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))", $"time:{userId}:{date:yyyy-MM-dd}");
        var already = await db.TimeEntries.Where(e => e.UserId == userId && e.WorkDate == date && e.Id != except).SumAsync(e => (decimal?)e.Hours) ?? 0;
        if (already + adding > 24) throw ApiException.Invalid("hours", "time.day_over", (24 - already).ToString("0.##", CultureInfo.InvariantCulture));
    }

    static async Task<IResult> Create(EntryBody body, Access access, HubDb db, CurrentUser me)
    {
        var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == body.TaskId) ?? throw ApiException.Invalid("taskId", "error.not_found");
        var (p, ctx) = await access.Project(task.ProjectId, track: false);
        Access.Demand(Permissions.EnterTime(access.Actor, ctx)); // Archived and Cancelled projects refuse; Complete tasks accept late entries
        var hours = Hours(body.Hours, "hours");
        var e = await Tx.Run(db, async () =>
        {
            await DailyCap(db, me.Id, body.WorkDate, hours, null);
            var e = new TaskTimeEntry { ProjectId = p.Id, TaskId = task.Id, UserId = me.Id, WorkDate = body.WorkDate, Hours = hours, Note = Check.Optional(body.Note, "note", 1000), AuditKey = task.Key };
            db.TimeEntries.Add(e);
            await db.SaveChangesAsync();
            return e;
        });
        return Results.Created($"/api/v1/time/{e.Id}", new { e.Id, e.RowVersion });
    }

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me)
    {
        var (e, _, ctx) = await Load(db, access, id);
        var (allow, needsReason) = Permissions.EditTime(access.Actor, ctx, e.UserId); // the owner edits; a PM corrects with a reason
        Access.Demand(allow);
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, e, patch.RowVersion);
        if (patch.Has("taskId")) Check.That(patch.Id("taskId") == e.TaskId, "taskId", "time.task_fixed"); // entries never move between tasks
        if (needsReason) db.Audit.Reason = Check.Reason(patch.Str("reason"));
        await Tx.Run(db, async () =>
        {
            if (patch.Has("workDate")) e.WorkDate = patch.Date("workDate") ?? throw ApiException.Invalid("workDate", "error.required");
            if (patch.Has("hours")) e.Hours = Hours(patch.Dec("hours") ?? 0, "hours");
            if (patch.Has("note")) e.Note = Check.Optional(patch.Str("note"), "note", 1000);
            await DailyCap(db, e.UserId, e.WorkDate, e.Hours, e.Id);
            await db.SaveChangesAsync();
            return 0;
        });
        return Results.Ok(new { e.Id, e.RowVersion });
    }
}
