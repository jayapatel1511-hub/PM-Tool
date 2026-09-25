using System.Globalization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Team calendar (§36.5, FR-VIS-06, AC-VIS-04): all-day projections of permitted task, deliverable and milestone dates,
/// and Meeting, Site Work and Internal Task events. Times are entered and shown in the organisation's time zone
/// (G-03); nothing from a project the viewer cannot see is returned, not even a count (§8.7).
public static class CalendarEndpoints
{
    public sealed record EventBody(string Type, string Title, Guid? ProjectId, string Start, string End, string? Location, string? Description, string? Visibility);

    public const string Deadlines = "Deadline";

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/calendar", Entries);
        api.MapPost("/calendar/events", Create);
        api.MapGet("/calendar/events/{id:guid}", async (Guid id, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var e = await Visible(db, access, me, id);
            var s = await store.Get(db);
            return await Row(db, access, me, e, s);
        });
        api.MapPatch("/calendar/events/{id:guid}", Edit);
        api.MapPost("/calendar/events/{id:guid}/cancel", async (Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var e = await Visible(db, access, me, id, track: true);
            await Demand(db, access, e);
            await Http.CheckVersion(db, http, e, new Patch(body).RowVersion);
            e.CancelledAt = clock.GetUtcNow();
            e.CancelledBy = me.Id;
            await db.SaveChangesAsync(); // logged as a status change (CancelledAt is in the allow-list)
            return Results.Ok(new { e.Id, e.RowVersion });
        });
    }

    static DateTimeOffset Utc(string local, TimeZoneInfo zone, string field)
    {
        if (!DateTime.TryParse(local, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) throw ApiException.Invalid(field, "error.date");
        var unspecified = DateTime.SpecifyKind(dt, DateTimeKind.Unspecified);
        return new DateTimeOffset(unspecified, zone.GetUtcOffset(unspecified)).ToUniversalTime();
    }

    static string Local(DateTimeOffset utc, TimeZoneInfo zone) => TimeZoneInfo.ConvertTime(utc, zone).ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);

    /// Private events are the owner's alone; project events follow the project's visibility; an event without a project is private.
    static IQueryable<CalendarEvent> Readable(HubDb db, Access access, CurrentUser me) =>
        db.CalendarEvents.AsNoTracking().Where(e => e.OwnerId == me.Id
            || (e.Visibility == EventVisibility.Project && e.ProjectId != null && access.VisibleProjectIds().Contains(e.ProjectId.Value)));

    static async Task<CalendarEvent> Visible(HubDb db, Access access, CurrentUser me, Guid id, bool track = false)
    {
        var ok = await Readable(db, access, me).AnyAsync(e => e.Id == id);
        if (!ok) throw ApiException.NotFound(); // SC-003: the same answer as a missing event
        return await (track ? db.CalendarEvents : db.CalendarEvents.AsNoTracking()).FirstAsync(e => e.Id == id);
    }

    static async Task<Allow> CanEdit(HubDb db, Access access, CalendarEvent e)
    {
        ProjectContext? ctx = null;
        if (e.ProjectId is { } pid) ctx = await access.Context(await db.Projects.AsNoTracking().FirstAsync(p => p.Id == pid));
        return e.CancelledAt is not null ? Allow.No("calendar.cancelled") : Permissions.EditEvent(access.Actor, ctx, e.OwnerId);
    }

    static async Task Demand(HubDb db, Access access, CalendarEvent e) => Access.Demand(await CanEdit(db, access, e));

    static async Task<object> Row(HubDb db, Access access, CurrentUser me, CalendarEvent e, OrgSettings s)
    {
        var zone = Clock.Zone(s);
        var number = e.ProjectId is { } pid ? await db.Projects.Where(p => p.Id == pid).Select(p => new { p.ProjectNumber, p.Name }).FirstOrDefaultAsync() : null;
        var owner = await db.Users.Where(u => u.Id == e.OwnerId).Select(u => u.DisplayName).FirstOrDefaultAsync();
        return new
        {
            Kind = "event", e.Id, e.Type, e.Title, Start = Local(e.StartAt, zone), End = Local(e.EndAt, zone), e.StartAt, e.EndAt, TimeZone = s.OrgTimeZone,
            e.ProjectId, ProjectNumber = number?.ProjectNumber, ProjectName = number?.Name, e.OwnerId, Owner = owner, e.Location, e.Description, e.Visibility,
            Cancelled = e.CancelledAt is not null, e.RowVersion, CanEdit = (await CanEdit(db, access, e)).Ok,
        };
    }

    static async Task<object> Entries(DateOnly from, DateOnly to, string? projectIds, string? types, Access access, HubDb db, CurrentUser me, SettingsStore store)
    {
        Check.That(to >= from && to.DayNumber - from.DayNumber <= 92, "to", "calendar.range");
        var s = await store.Get(db);
        var zone = Clock.Zone(s);
        var want = Http.List(types);
        bool Want(string t) => want.Length == 0 || want.Contains(t);
        // The workspace scope (§36.1): mine, all or chosen ids; visibility and live status applied as everywhere else.
        var projects = Scope.Projects(access, db, projectIds);
        var scoped = !string.IsNullOrWhiteSpace(projectIds) && projectIds != Scope.All;
        var pids = projects.Select(p => p.Id);
        var numbers = await projects.Select(p => new { p.Id, p.ProjectNumber, p.Name }).ToDictionaryAsync(p => p.Id);
        var entries = new List<object>();
        if (Want(Deadlines))
        {
            var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.Complete)
                .Select(t => new { t.Id, t.Key, t.Name, t.DueDate, t.ProjectId, t.Status, Overdue = db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue) }).ToListAsync();
            var dels = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to
                    && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
                .Select(d => new { d.Id, d.Key, d.Name, d.DueDate, d.ProjectId, d.Status, Overdue = db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue) }).ToListAsync();
            var ms = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled)
                .Select(m => new { m.Id, m.Key, m.Name, m.Date, m.ProjectId, Status = m.IsComplete ? MilestoneStatus.Complete : db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault() }).ToListAsync();
            object D(string type, Guid id, string key, string name, DateOnly date, Guid pid, string? status, bool overdue) => new
            {
                Kind = "deadline", SourceType = type, Id = id, Key = key, Title = name, Date = date, ProjectId = pid, numbers[pid].ProjectNumber, ProjectName = numbers[pid].Name, Status = status, Overdue = overdue,
            };
            entries.AddRange(tasks.Select(t => D(ItemType.Task, t.Id, t.Key, t.Name, t.DueDate!.Value, t.ProjectId, t.Status, t.Overdue)));
            entries.AddRange(dels.Select(d => D(ItemType.Deliverable, d.Id, d.Key, d.Name, d.DueDate!.Value, d.ProjectId, d.Status, d.Overdue)));
            entries.AddRange(ms.Select(m => D(ItemType.Milestone, m.Id, m.Key, m.Name, m.Date!.Value, m.ProjectId, m.Status, m.Status == MilestoneStatus.Overdue)));
        }
        var eventTypes = CalendarEventType.All.Where(Want).ToArray();
        if (eventTypes.Length > 0)
        {
            var start = Utc(from.ToString("yyyy-MM-dd") + "T00:00", zone, "from");
            var end = Utc(to.AddDays(1).ToString("yyyy-MM-dd") + "T00:00", zone, "to");
            var q = Readable(db, access, me).Where(e => e.CancelledAt == null && eventTypes.Contains(e.Type) && e.StartAt < end && e.EndAt > start);
            if (scoped) q = q.Where(e => e.ProjectId == null || pids.Contains(e.ProjectId.Value)); // private project-less events stay in the owner's calendar
            foreach (var e in await q.OrderBy(e => e.StartAt).ToListAsync()) entries.Add(await Row(db, access, me, e, s));
        }
        return new { TimeZone = s.OrgTimeZone, From = from, To = to, Entries = entries };
    }

    static async Task<IResult> Create(EventBody body, Access access, HubDb db, CurrentUser me, SettingsStore store)
    {
        var s = await store.Get(db);
        var zone = Clock.Zone(s);
        Check.OneOf(body.Type, CalendarEventType.All, "type");
        var e = new CalendarEvent { OwnerId = me.Id, Type = body.Type, TimeZone = s.OrgTimeZone };
        await Fill(db, access, e, body.Title, body.ProjectId, body.Start, body.End, body.Location, body.Description, body.Visibility, zone);
        if (e.ProjectId is { } pid) Access.Demand(Permissions.CreateProjectEvent(access.Actor, (await access.Project(pid, track: false)).Ctx));
        else Check.That(!access.Actor.ReadOnly, "projectId", "perm.read_only");
        db.CalendarEvents.Add(e);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/calendar/events/{e.Id}", await Row(db, access, me, e, s));
    }

    /// §36.5: Meeting and Site Work need a project; an Internal Task without one is Private; the end follows the start.
    static async Task Fill(HubDb db, Access access, CalendarEvent e, string? title, Guid? projectId, string? start, string? end, string? location, string? description,
        string? visibility, TimeZoneInfo zone)
    {
        e.Title = Check.Required(title, "title", 200);
        Check.That(projectId is not null || e.Type == CalendarEventType.InternalTask, "projectId", "calendar.project_required");
        if (projectId is { } pid) await access.Project(pid, track: false);
        e.ProjectId = projectId;
        e.StartAt = Utc(start ?? "", zone, "start");
        e.EndAt = Utc(end ?? "", zone, "end");
        Check.That(e.EndAt > e.StartAt, "end", "calendar.end_before_start");
        e.Location = Check.Optional(location, "location", 300);
        e.Description = Check.Optional(description, "description", 4000);
        var vis = visibility ?? (projectId is null ? EventVisibility.Private : EventVisibility.Project);
        Check.OneOf(vis, [EventVisibility.Project, EventVisibility.Private], "visibility");
        e.Visibility = projectId is null ? EventVisibility.Private : vis;
    }

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store)
    {
        var e = await Visible(db, access, me, id, track: true);
        await Demand(db, access, e); // the owner or a PM of the event's project
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, e, patch.RowVersion);
        var s = await store.Get(db);
        var zone = Clock.Zone(s);
        if (patch.Has("type")) { var type = patch.Str("type"); Check.OneOf(type, CalendarEventType.All, "type"); e.Type = type!; }
        await Fill(db, access, e, patch.Has("title") ? patch.Str("title") : e.Title, patch.Has("projectId") ? patch.Id("projectId") : e.ProjectId,
            patch.Has("start") ? patch.Str("start") : Local(e.StartAt, zone), patch.Has("end") ? patch.Str("end") : Local(e.EndAt, zone),
            patch.Has("location") ? patch.Str("location") : e.Location, patch.Has("description") ? patch.Str("description") : e.Description,
            patch.Has("visibility") ? patch.Str("visibility") : e.Visibility, zone);
        if (e.ProjectId is { } pid && patch.Has("projectId")) Access.Demand(Permissions.CreateProjectEvent(access.Actor, (await access.Project(pid, track: false)).Ctx));
        await db.SaveChangesAsync();
        return Results.Ok(await Row(db, access, me, e, s));
    }
}
