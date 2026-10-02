using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Activity History (§13.14, FR-VIEW-03, FR-AUD-02, FR-AUD-03). The log is read-only everywhere.
public static class ActivityEndpoints
{
    public static readonly string[] Important = ["status", "assignment", "date", "deletion"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/activity", async (Guid id, Access access, HubDb db, DateOnly? from, DateOnly? to, Guid? actorId,
            string? itemType, string? category, Guid? disciplineId, bool? importantOnly, int? page, int? pageSize) =>
        {
            await access.Project(id, track: false);
            var (pg, size) = Http.Paging(page, pageSize);
            var q = Filter(await Visible(db, access, id), from, to, actorId, itemType, category, disciplineId, importantOnly);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return new Page<object>(await Render(db, rows), pg, size, total);
        });

        api.MapGet("/items/{type}/{id:guid}/activity", async (string type, Guid id, HubDb db, Access access, int? page, int? pageSize) =>
        {
            var projectId = await ItemProject(db, type, id) ?? throw ApiException.NotFound();
            await access.Project(projectId, track: false);
            var (pg, size) = Http.Paging(page, pageSize ?? 100);
            // The item's own rows plus comments, links, hours and dependencies that name it by key ("T1 → T2", "deleted with T1").
            // A project's own history is its rows only: the project number is part of every item key.
            var key = type == ItemType.Project ? null : await db.ActivityLog.Where(a => a.ItemId == id && a.ItemKey != null).Select(a => a.ItemKey).FirstOrDefaultAsync();
            var q = (await Visible(db, access, projectId)).Where(a => (a.ItemId == id || (key != null && a.ItemId != id
                && ((a.ItemType != ItemType.Dependency && a.ItemKey == key)
                    || (a.ItemType == ItemType.Dependency && (a.ItemKey!.StartsWith(key + " ") || a.ItemKey.EndsWith(" " + key)))))));
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return new Page<object>(await Render(db, rows), pg, size, total);
        });
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
        return db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && visibleProjects.Contains(a.ProjectId.Value)
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

    public static async Task<Guid?> ItemProject(HubDb db, string type, Guid id) => type switch
    {
        ItemType.Task => await db.Tasks.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Deliverable => await db.Deliverables.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Milestone => await db.Milestones.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Decision => await db.Decisions.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Risk => await db.Risks.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Issue => await db.Issues.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Action => await db.Actions.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        "ReviewPackage" => await db.ReviewPackages.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        "ChangeNotice" => await db.ChangeNotices.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        "ReviewFinding" => await db.ReviewFindings.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        "ChangeAssessment" => await db.ChangeAssessments.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        "Handoff" => await db.Handoffs.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync(),
        ItemType.Project => await db.Projects.Where(x => x.Id == id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(),
        _ => null,
    };

    /// Renders log rows for people: IDs resolved to names at read time (§20.3), and a one-line summary.
    public static async Task<List<object>> Render(HubDb db, List<ActivityLog> rows)
    {
        var parsed = rows.Select(r => (Row: r, Changes: JsonSerializer.Deserialize<List<Change>>(r.Changes, JsonOpts.Web) ?? [])).ToList();
        var ids = new HashSet<Guid>();
        foreach (var (r, cs) in parsed)
        {
            if (r.ActorUserId is { } a) ids.Add(a);
            foreach (var c in cs) { AddId(c.Old, ids); AddId(c.New, ids); }
        }
        var names = await Names(db, ids);
        string? L(JsonElement? v) => v is { ValueKind: JsonValueKind.String } s && Guid.TryParse(s.GetString(), out var g) && names.TryGetValue(g, out var n) ? n
            : v is { } x && x.ValueKind != JsonValueKind.Null ? (x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString()) : null;
        return parsed.Select(p =>
        {
            var changes = p.Changes.Select(c => new { c.Field, Old = c.Old, New = c.New, OldLabel = L(c.Old), NewLabel = L(c.New) }).ToList();
            var summary = string.Join("; ", changes.Select(c => $"{Label(c.Field)}: {c.OldLabel ?? "—"} → {c.NewLabel ?? "—"}"));
            return (object)new
            {
                p.Row.Id, p.Row.OccurredAt, ActorId = p.Row.ActorUserId,
                ActorName = p.Row.ActorUserId is { } a && names.TryGetValue(a, out var an) ? an : null,
                p.Row.ActorType, p.Row.ProjectId, p.Row.ItemType, p.Row.ItemId, p.Row.ItemKey, p.Row.ItemName, p.Row.Action,
                p.Row.Categories, Changes = changes, Summary = summary, p.Row.Reason, p.Row.Source, p.Row.CorrelationId,
                Snapshot = p.Row.Snapshot is null ? (JsonElement?)null : JsonDocument.Parse(p.Row.Snapshot).RootElement.Clone(),
            };
        }).ToList();
    }

    public sealed record Change(string Field, JsonElement? Old, JsonElement? New);

    static void AddId(JsonElement? v, HashSet<Guid> ids)
    {
        if (v is { ValueKind: JsonValueKind.String } s && Guid.TryParse(s.GetString(), out var g)) ids.Add(g);
    }

    /// Field labels for summaries; the SPA translates field names itself.
    static string Label(string field) => field.EndsWith("Id") && field.Length > 2 ? field[..^2] : field;

    public static async Task<Dictionary<Guid, string>> Names(HubDb db, HashSet<Guid> ids)
    {
        var d = new Dictionary<Guid, string>();
        if (ids.Count == 0) return d;
        var list = ids.ToList();
        void Add(IEnumerable<(Guid Id, string Name)> xs) { foreach (var (id, n) in xs) d.TryAdd(id, n); }
        Add((await db.Users.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.DisplayName, x.IsActive }).ToListAsync())
            .Select(x => (x.Id, x.IsActive ? x.DisplayName : x.DisplayName + " (Inactive)")));
        Add((await db.ProjectDisciplines.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Discipline!.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Disciplines.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Tasks.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")));
        Add((await db.Deliverables.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")));
        Add((await db.Milestones.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")));
        Add((await db.Decisions.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Subject }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Subject}")));
        Add((await db.Risks.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Title }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Title}")));
        Add((await db.Issues.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Title }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Title}")));
        Add((await db.Actions.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Text }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Text}")));
        Add((await db.Meetings.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Title }).ToListAsync()).Select(x => (x.Id, x.Title)));
        Add((await db.Clients.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Offices.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Phases.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.ProjectTypes.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.DeliverableTypes.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.ExternalParties.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.Organisation }).ToListAsync())
            .Select(x => (x.Id, x.Organisation is null ? x.Name : $"{x.Name} ({x.Organisation})")));
        return d;
    }
}
