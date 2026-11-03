using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record TaskHistoryActorChoice(Guid Id, string DisplayName, bool IsActive);

/// Activity History (§13.14, FR-VIEW-03, FR-AUD-02, FR-AUD-03). The log is read-only everywhere.
public static class ActivityEndpoints
{
    public static readonly string[] Important = ["status", "assignment", "date", "deletion"];

    /// One rendered log row, the shape every history list, export and feed carries (§20.3).
    public sealed record ActivityEntry(Guid Id, DateTimeOffset OccurredAt, Guid? ActorId, string? ActorName, string ActorType, Guid? ProjectId, string ItemType, Guid? ItemId,
        string? ItemKey, string? ItemName, string Action, List<string> Categories, List<ChangeLine> Changes, string Summary, string? Reason, string Source,
        Guid? CorrelationId, JsonElement? Snapshot, Dictionary<string, string?>? SnapshotLabels);
    public sealed record ChangeLine(string Field, JsonElement? Old, JsonElement? New, string? OldLabel, string? NewLabel);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/activity", async Task<Ok<Page<ActivityEntry>>> (Guid id, Access access, HubDb db, DateOnly? from, DateOnly? to, Guid? actorId,
            string? itemType, string? category, Guid? disciplineId, bool? importantOnly, int? page, int? pageSize) =>
        {
            await access.Project(id, track: false);
            var (pg, size) = Http.Paging(page, pageSize);
            var q = Filter(await Visible(db, access, id), from, to, actorId, itemType, category, disciplineId, importantOnly);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return TypedResults.Ok(new Page<ActivityEntry>(await Render(db, rows), pg, size, total));
        });

        api.MapGet("/items/{type}/{id:guid}/activity", async Task<Ok<Page<ActivityEntry>>> (string type, Guid id, HubDb db, Access access,
            SettingsStore store, string? actorId, int? page, int? pageSize) =>
        {
            var enhanced = type == ItemType.Task && (await store.Get(db)).IsFlagOn("work_screens");
            var (pg, size) = Http.Paging(page, pageSize ?? 100);
            var q = await ItemQuery(db, access, type, id, enhanced);
            // Raw string intentionally preserves the off-state treatment of new, even malformed parameters.
            if (enhanced && !string.IsNullOrEmpty(actorId))
            {
                Check.That(Guid.TryParse(actorId, out var actor) && actor != Guid.Empty, "actorId", "error.invalid");
                q = q.Where(row => row.ActorUserId == actor);
            }
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return TypedResults.Ok(new Page<ActivityEntry>(await Render(db, rows), pg, size, total));
        });

        api.MapGet("/tasks/{id:guid}/activity/actors", async Task<Ok<TaskHistoryActorChoice[]>> (Guid id, HubDb db, Access access, SettingsStore store) =>
        {
            Http.RequireFlag(await store.Get(db), "work_screens");
            var q = await ItemQuery(db, access, ItemType.Task, id, canonicalTaskKey: true);
            var ids = await q.Where(row => row.ActorUserId != null).Select(row => row.ActorUserId!.Value).Distinct().ToListAsync();
            var users = await db.Users.AsNoTracking().Where(user => ids.Contains(user.Id))
                .Select(user => new { user.Id, user.DisplayName, user.IsActive }).ToDictionaryAsync(user => user.Id);
            var choices = ids.Select(actor => users.TryGetValue(actor, out var user)
                ? new TaskHistoryActorChoice(actor, user.DisplayName, user.IsActive)
                : new TaskHistoryActorChoice(actor, Text.Get("taskHistory.actorUnavailable"), false))
                .OrderBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase).ThenBy(choice => choice.Id).ToArray();
            return TypedResults.Ok(choices);
        }).RequireFlag("work_screens");
    }

    /// The canonical item history population, shared by paging and its complete actor choices.
    public static async Task<IQueryable<ActivityLog>> ItemQuery(HubDb db, Access access, string type, Guid id, bool canonicalTaskKey = false)
    {
        var projectId = await ItemProject(db, type, id) ?? throw ApiException.NotFound();
        await access.Project(projectId, track: false);
        // A legacy/imported Task can have its first history supplied by a contribution child.
        var key = canonicalTaskKey && type == ItemType.Task
            ? await db.Tasks.AsNoTracking().Where(task => task.Id == id && task.ProjectId == projectId).Select(task => task.Key).FirstOrDefaultAsync()
            : null;
        key ??= type == ItemType.Project ? null
            : await db.ActivityLog.Where(row => row.ItemId == id && row.ItemKey != null).Select(row => row.ItemKey).FirstOrDefaultAsync();
        return (await Visible(db, access, projectId)).Where(row => row.ItemId == id || key != null && row.ItemId != id
            && (row.ItemType != ItemType.Dependency && row.ItemKey == key
                || row.ItemType == ItemType.Dependency && (row.ItemKey!.StartsWith(key + " ") || row.ItemKey.EndsWith(" " + key))));
    }

    /// Apply the source records' privacy boundaries to every project/item history and export, including old log rows.
    public static async Task<IQueryable<ActivityLog>> Visible(HubDb db, Access access, Guid projectId)
    {
        await access.Project(projectId, false); // preserve restricted-project 404 semantics before bulk filtering
        return Visible(db, access, new[] { projectId });
    }

    /// Apply the same source privacy boundary to several already-authorized projects in one SQL query.
    public static IQueryable<ActivityLog> Visible(HubDb db, Access access, IReadOnlyCollection<Guid> projectIds)
    {
        var actor = access.Actor;
        var visibleProjects = access.VisibleProjectIds().Where(id => projectIds.Contains(id));
        var pmProjects = db.Projects.Where(p => visibleProjects.Contains(p.Id) &&
            (actor.Admin || p.ProjectManagerId == actor.Id || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == actor.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM))))
            .Select(p => p.Id);
        var leadTasks = db.Tasks.IgnoreQueryFilters().Where(t => visibleProjects.Contains(t.ProjectId)
            && db.ProjectDisciplines.Any(d => d.Id == t.ProjectDisciplineId && d.ProjectId == t.ProjectId && d.LeadUserId == actor.Id)).Select(t => t.Id);
        var directReports = db.Users.Where(u => u.SupervisorId == actor.Id).Select(u => u.Id);
        var visibleEntries = db.TimeEntries.IgnoreQueryFilters().Where(e => visibleProjects.Contains(e.ProjectId) &&
            (e.UserId == actor.Id || pmProjects.Contains(e.ProjectId) || leadTasks.Contains(e.TaskId)
                || actor.Supervisor && directReports.Contains(e.UserId))).Select(e => e.Id);
        return ActivityScreenFields.VisibleStored(db, db.ActivityLog.AsNoTracking()).Where(a => a.ProjectId != null && visibleProjects.Contains(a.ProjectId.Value)
            && (a.ItemType != ItemType.TimeEntry || visibleEntries.Select(id => (Guid?)id).Contains(a.ItemId))
            && (a.ItemType != ItemType.CalendarEvent || !db.CalendarEvents.Any(e => e.Id == a.ItemId && e.Visibility == EventVisibility.Private)));
    }

    public static IQueryable<ActivityLog> Filter(IQueryable<ActivityLog> q, DateOnly? from, DateOnly? to, Guid? actorId, string? itemType,
        string? category, Guid? disciplineId, bool? importantOnly)
    {
        if (from is { } f) q = q.Where(a => a.OccurredAt >= f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (to is { } t) q = q.Where(a => a.OccurredAt < t.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (actorId is { } aid) q = q.Where(a => a.ActorUserId == aid);
        var types = Http.List(itemType);
        if (types.Length > 0) q = q.Where(a => types.Contains(a.ItemType));
        var cats = Http.List(category);
        if (cats.Length > 0) q = q.Where(a => a.Categories.Any(c => cats.Contains(c)));
        if (disciplineId is { } d) q = q.Where(a => a.ProjectDisciplineId == d);
        if (importantOnly == true) q = q.Where(a => a.Categories.Any(c => Important.Contains(c)));
        return q;
    }

    public static Task<Guid?> ItemProject(HubDb db, string type, Guid id) =>
        Hub.Api.Records.EntityRegistry.History(type)?.Project!(db, id) ?? Task.FromResult<Guid?>(null);

    /// Renders log rows for people: IDs resolved to names at read time (§20.3), and a one-line summary.
    public static async Task<List<ActivityEntry>> Render(HubDb db, List<ActivityLog> rows, ActivityScreenFields? visibility = null)
    {
        if (rows.Count == 0) return [];
        var screenFields = visibility ?? await ActivityScreenFields.Read(db);
        var parsed = rows.Where(screenFields.Visible).Select(r => (Row: r,
            Changes: (JsonSerializer.Deserialize<List<Change>>(r.Changes, JsonOpts.Web) ?? []).Where(c => !screenFields.Hidden(r.ItemType, c.Field)).ToList(),
            Snapshot: r.Snapshot is null ? (JsonElement?)null : Unshown(screenFields.MaskSnapshot(r.ItemType, JsonSerializer.Deserialize<JsonElement>(r.Snapshot))))).ToList();
        var ids = new HashSet<Guid>();
        foreach (var (r, cs, snapshot) in parsed)
        {
            if (r.ActorUserId is { } a) ids.Add(a);
            foreach (var c in cs) { AddId(c.Old, ids); AddId(c.New, ids); }
            if (snapshot is { ValueKind: JsonValueKind.Object } s)
                foreach (var field in s.EnumerateObject()) AddId(field.Value, ids);
        }
        var names = await Names(db, ids);
        string? L(JsonElement? v) => v is { ValueKind: JsonValueKind.String } s && Guid.TryParse(s.GetString(), out var g) && names.TryGetValue(g, out var n) ? n
            : v is { } x && x.ValueKind != JsonValueKind.Null ? (x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()) : null;
        return parsed.Select(p =>
        {
            var changes = p.Changes.Select(c => NotShown.Contains(c.Field) ? new ChangeLine(c.Field, null, null, Text.Get("activity.notShown"), Text.Get("activity.notShown"))
                : new ChangeLine(c.Field, c.Old, c.New, L(c.Old), L(c.New))).ToList();
            var snapshotLabels = p.Snapshot is { ValueKind: JsonValueKind.Object } s
                ? s.EnumerateObject().ToDictionary(field => field.Name, field => L(field.Value)) : null;
            var summary = p.Row.Action == "Deleted" && snapshotLabels is not null
                ? string.Join("; ", snapshotLabels.Select(field => $"{Label(field.Key)}: {field.Value ?? "—"}"))
                : string.Join("; ", changes.Select(c => $"{Label(c.Field)}: {c.OldLabel ?? "—"} → {c.NewLabel ?? "—"}"));
            return new ActivityEntry(p.Row.Id, p.Row.OccurredAt, p.Row.ActorUserId,
                p.Row.ActorUserId is { } a && names.TryGetValue(a, out var an) ? an : p.Row.ActorType == "System" ? Text.Get("common.system") : null,
                p.Row.ActorType, p.Row.ProjectId, p.Row.ItemType, p.Row.ItemId, p.Row.ItemKey, p.Row.ItemName, p.Row.Action,
                p.Row.Categories, changes, summary, p.Row.Reason, p.Row.Source, p.Row.CorrelationId, p.Snapshot, snapshotLabels);
        }).ToList();
    }

    public sealed record Change(string Field, JsonElement? Old, JsonElement? New);

    static void AddId(JsonElement? v, HashSet<Guid> ids)
    {
        if (v is { ValueKind: JsonValueKind.String } s && Guid.TryParse(s.GetString(), out var g)) ids.Add(g);
    }

    /// Field labels for summaries; the SPA translates field names itself.
    /// Fields whose values only some readers of the log may see (a project's internal notes are for members and management,
    /// while its log is open to every viewer): the log says they changed, never what to.
    static readonly HashSet<string> NotShown = ["InternalNotes"];
    static JsonElement Unshown(JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object || !NotShown.Any(f => snapshot.TryGetProperty(f, out _))) return snapshot;
        var node = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        foreach (var f in NotShown) node.Remove(f);
        return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
    }

    static string Label(string field) => field.EndsWith("Id") && field.Length > 2 ? field[..^2] : field;

    public static Task<Dictionary<Guid, string>> Names(HubDb db, HashSet<Guid> ids) =>
        Hub.Api.Records.EntityRegistry.Names(db, ids);
}
