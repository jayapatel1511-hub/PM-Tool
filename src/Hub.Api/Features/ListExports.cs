using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// "Export what is on screen" for every list (§13.3, §22, FR-007): each export runs the list's own query with the same
/// query string, so the file holds exactly the filtered rows.
public static class ListExportEndpoints
{
    static readonly Col[] TaskCols =
    [
        new("key", "key", "key"), new("name", "task"), new("deliverableKey", "deliverable"), new("disciplineName", "discipline"), new("assigneeName", "assignee"),
        new("reviewerName", "reviewer"), new("status", "status"), new("priority", "priority"), new("startDate", "start", "date"), new("dueDate", "due", "date"),
        new("progressPct", "progress", "percent"), new("estimatedHours", "estimate", "number"), new("indicators", "indicators"), new("lastActivityAt", "lastActivity", "datetime"),
    ];
    static readonly Col[] DeliverableCols =
    [
        new("key", "key", "key"), new("name", "deliverable"), new("typeName", "type"), new("disciplineName", "discipline"), new("ownerName", "owner"),
        new("reviewerName", "reviewer"), new("milestoneKey", "milestone"), new("dueDate", "due", "date"), new("status", "status"), new("state.progressPct", "progress", "percent"),
        new("indicators", "indicators"), new("revision", "revision"), new("issuedDate", "issued", "date"),
    ];
    static readonly Col[] DecisionCols =
    [
        new("key", "key", "key"), new("subject", "subject"), new("ownerName", "owner"), new("requestedByName", "requestedBy"), new("dateRequested", "requested", "date"),
        new("requiredByDate", "requiredBy", "date"), new("days", "days", "number"), new("impactLevel", "impact"), new("status", "status"), new("blocking", "blockingCount", "number"),
        new("decisionText", "decision"), new("decisionDate", "decisionDate", "date"),
    ];
    static readonly Col[] MilestoneCols =
    [
        new("key", "key", "key"), new("name", "milestone"), new("milestoneType", "type"), new("date", "date", "date"), new("slipDays", "slip", "number"), new("status", "status"),
        new("deliverablesIssued", "deliverablesIssued"), new("taskOpen", "openTasks", "number"),
    ];
    static readonly Col[] ProjectCols =
    [
        new("projectNumber", "project"), new("name", "name"), new("client.name", "client"), new("pm.displayName", "pm"), new("status", "status"), new("phase", "phase"),
        new("office", "office"), new("reportedHealth", "health"), new("progressPct", "progress", "percent"), new("overdueTasks", "overdueTasks", "number"),
        new("nextMilestone.name", "nextMilestone"), new("targetCompletionDate", "target", "date"), new("priority", "priority"),
    ];
    static readonly Col[] ActivityCols =
    [
        new("occurredAt", "timestamp", "datetime"), new("actorName", "actor"), new("action", "action"), new("item", "item"), new("summary", "change"),
        new("reason", "reason"), new("source", "source"),
    ];

    static JsonArray Json(IEnumerable<object> rows) => JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray();

    static JsonArray Indicators(JsonArray rows)
    {
        foreach (var r in rows) r!["indicators"] = ReportEndpoints.Indicators(r["state"]);
        return rows;
    }

    /// The filters as the Parameters sheet shows them, with IDs resolved to names.
    internal static async Task<List<(string, string)>> Filters(HubDb db, HttpContext http, Access access)
    {
        var pairs = http.Request.Query.Where(k => k.Key is not ("format" or "page" or "pageSize" or "panel") && !string.IsNullOrWhiteSpace(k.Value)).ToList();
        var ids = pairs.SelectMany(p => Http.Ids(p.Value)).ToHashSet();
        var names = await VisibleNames(db, access, ids);
        return pairs.Select(p =>
        {
            var label = Text.Get($"param.{p.Key}");
            var value = string.Join(", ", Http.List(p.Value).Select(v => Guid.TryParse(v, out var g) && names.TryGetValue(g, out var n) ? n : v));
            return (label.StartsWith("param.") ? p.Key : label, value);
        }).ToList();
    }

    /// Resolves filter IDs only through records in projects visible to the caller. Unknown or
    /// out-of-scope IDs deliberately remain raw query values in the Parameters sheet.
    static async Task<Dictionary<Guid, string>> VisibleNames(HubDb db, Access access, HashSet<Guid> ids)
    {
        var names = new Dictionary<Guid, string>();
        if (ids.Count == 0) return names;
        var visible = access.VisibleProjectIds();
        var list = ids.ToList();
        void Add(IEnumerable<(Guid Id, string Name)> values) { foreach (var value in values) names.TryAdd(value.Id, value.Name); }

        Add((await db.Users.Where(u => list.Contains(u.Id) &&
                (db.Projects.Any(p => visible.Contains(p.Id) && p.ProjectManagerId == u.Id) ||
                 db.ProjectMembers.Any(m => visible.Contains(m.ProjectId) && m.UserId == u.Id && m.RemovedAt == null) ||
                 db.ProjectDisciplines.Any(d => visible.Contains(d.ProjectId) && d.LeadUserId == u.Id) ||
                 db.Tasks.IgnoreQueryFilters().Any(t => visible.Contains(t.ProjectId) && (t.AssigneeId == u.Id || t.ReviewerId == u.Id)) ||
                 db.Deliverables.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && (d.OwnerId == u.Id || d.ReviewerId == u.Id)) ||
                 db.Decisions.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && (d.OwnerUserId == u.Id || d.RequestedById == u.Id)) ||
                 db.Risks.IgnoreQueryFilters().Any(r => visible.Contains(r.ProjectId) && r.OwnerId == u.Id) ||
                 db.Issues.IgnoreQueryFilters().Any(i => visible.Contains(i.ProjectId) && i.OwnerId == u.Id) ||
                 db.Actions.IgnoreQueryFilters().Any(a => visible.Contains(a.ProjectId) && a.OwnerUserId == u.Id) ||
                 db.ActivityLog.Any(a => a.ProjectId != null && visible.Contains(a.ProjectId.Value) && a.ActorUserId == u.Id)))
            .Select(u => new { u.Id, u.DisplayName, u.IsActive }).ToListAsync()).Select(u => (u.Id, u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)")));
        Add((await db.Projects.Where(p => visible.Contains(p.Id) && list.Contains(p.Id)).Select(p => new { p.Id, p.ProjectNumber, p.Name }).ToListAsync())
            .Select(p => (p.Id, p.ProjectNumber + " " + p.Name)));
        Add((await db.ProjectDisciplines.Where(p => visible.Contains(p.ProjectId) && list.Contains(p.Id)).Join(db.Disciplines, p => p.DisciplineId, d => d.Id,
                (p, d) => new { p.Id, d.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Disciplines.Where(d => list.Contains(d.Id) && db.ProjectDisciplines.Any(p => visible.Contains(p.ProjectId) && p.DisciplineId == d.Id))
                .Select(d => new { d.Id, d.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.Tasks.IgnoreQueryFilters().Where(t => list.Contains(t.Id) && visible.Contains(t.ProjectId)).Select(t => new { t.Id, t.Key, t.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)));
        Add((await db.Deliverables.IgnoreQueryFilters().Where(d => list.Contains(d.Id) && visible.Contains(d.ProjectId)).Select(d => new { d.Id, d.Key, d.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)));
        Add((await db.Milestones.IgnoreQueryFilters().Where(m => list.Contains(m.Id) && visible.Contains(m.ProjectId)).Select(m => new { m.Id, m.Key, m.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)));
        Add((await db.Decisions.IgnoreQueryFilters().Where(d => list.Contains(d.Id) && visible.Contains(d.ProjectId)).Select(d => new { d.Id, d.Key, d.Subject }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Subject)));
        Add((await db.Risks.IgnoreQueryFilters().Where(r => list.Contains(r.Id) && visible.Contains(r.ProjectId)).Select(r => new { r.Id, r.Key, r.Title }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Title)));
        Add((await db.Issues.IgnoreQueryFilters().Where(i => list.Contains(i.Id) && visible.Contains(i.ProjectId)).Select(i => new { i.Id, i.Key, i.Title }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Title)));
        Add((await db.Actions.IgnoreQueryFilters().Where(a => list.Contains(a.Id) && visible.Contains(a.ProjectId)).Select(a => new { a.Id, a.Key, a.Text }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Text)));
        Add((await db.Meetings.IgnoreQueryFilters().Where(m => list.Contains(m.Id) && visible.Contains(m.ProjectId)).Select(m => new { m.Id, m.Title }).ToListAsync())
            .Select(x => (x.Id, x.Title)));
        Add((await db.Clients.Where(c => list.Contains(c.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.ClientId == c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)));
        Add((await db.Offices.Where(o => list.Contains(o.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.OfficeId == o.Id)).Select(o => new { o.Id, o.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)));
        Add((await db.Phases.Where(p => list.Contains(p.Id) && db.Projects.Any(x => visible.Contains(x.Id) && x.PhaseId == p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)));
        Add((await db.ProjectTypes.Where(t => list.Contains(t.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.ProjectTypeId == t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)));
        Add((await db.DeliverableTypes.Where(t => list.Contains(t.Id) && db.Deliverables.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && d.DeliverableTypeId == t.Id))
                .Select(t => new { t.Id, t.Name }).ToListAsync()).Select(x => (x.Id, x.Name)));
        Add((await db.ExternalParties.Where(e => list.Contains(e.Id) && db.Actions.IgnoreQueryFilters().Any(a => visible.Contains(a.ProjectId) && a.OwnerExternalPartyId == e.Id))
                .Select(e => new { e.Id, e.Name, e.Organisation }).ToListAsync()).Select(x => (x.Id, x.Organisation is null ? x.Name : x.Name + " (" + x.Organisation + ")")));
        return names;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/tasks/export", async (Guid id, string? format, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var s = await store.Get(db);
            var f = TaskFilter.From(http.Request.Query);
            var q = TaskQueries.Sort(TaskQueries.Apply(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == id), f, me.Id, clock.Today(s), s), f.Sort).Take(Export.MaxRows + 1);
            var rows = Indicators(Json(await TaskQueries.Rows(db, q)));
            return await ExportFile.Send(db, store, format, Text.Get("export.tasks", p.ProjectNumber), TaskCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-tasks", clock);
        });
        api.MapGet("/projects/{id:guid}/deliverables/export", async (Guid id, string? format, [AsParameters] DeliverableEndpoints.DeliverableQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = Indicators(Json(await DeliverableEndpoints.List(id, f, access, db)));
            return await ExportFile.Send(db, store, format, Text.Get("export.deliverables", p.ProjectNumber), DeliverableCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-deliverables", clock);
        });
        api.MapGet("/projects/{id:guid}/decisions/export", async (Guid id, string? format, [AsParameters] DecisionEndpoints.DecisionQuery f, HttpContext http, Access access, HubDb db,
            EvaluationService eval, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = Json(await DecisionEndpoints.Register(id, f, access, db, eval, store, clock));
            foreach (var r in rows)
            {
                r!["days"] = r["isOverdue"]?.GetValue<bool>() == true ? -r["daysOverdue"]!.GetValue<int>() : r["daysUntil"] is JsonNode du ? du.GetValue<int>() : null;
                r["blocking"] = (r["blockingTaskIds"] as JsonArray)?.Count ?? 0;
            }
            return await ExportFile.Send(db, store, format, Text.Get("export.decisions", p.ProjectNumber), DecisionCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-decisions", clock);
        });
        api.MapGet("/projects/{id:guid}/milestones/export", async (Guid id, string? format, [AsParameters] MilestoneEndpoints.MilestoneQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = Json(await MilestoneEndpoints.List(id, f, access, db));
            foreach (var r in rows) r!["deliverablesIssued"] = $"{r["deliverableIssued"]}/{r["deliverableTotal"]}";
            return await ExportFile.Send(db, store, format, Text.Get("export.milestones", p.ProjectNumber), MilestoneCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-milestones", clock);
        });
        api.MapGet("/projects/export", async (string? format, [AsParameters] ProjectEndpoints.ProjectQuery f, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var rows = Json(await ProjectEndpoints.Sorted(f, access, db, me, store, clock));
            return await ExportFile.Send(db, store, format, Text.Get("export.projects"), ProjectCols, rows, await Filters(db, http, access), null, "projects", clock);
        });
        api.MapGet("/projects/{id:guid}/activity/export", async (Guid id, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock, DateOnly? from, DateOnly? to,
            Guid? actorId, string? itemType, string? category, Guid? disciplineId, bool? importantOnly) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var logs = await ActivityEndpoints.Filter(await ActivityEndpoints.Visible(db, access, id), from, to, actorId, itemType, category, disciplineId, importantOnly)
                .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(Export.MaxRows + 1).ToListAsync();
            var rows = Json(await ActivityEndpoints.Render(db, logs));
            foreach (var r in rows) r!["item"] = $"{r["itemKey"]?.GetValue<string>()} {r["itemName"]?.GetValue<string>()}".Trim();
            return await ExportFile.Send(db, store, format, Text.Get("export.activity", p.ProjectNumber), ActivityCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-activity", clock);
        });
    }
}
