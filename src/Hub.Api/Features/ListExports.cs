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
    static readonly Col[] TaskCols = Hub.Api.Records.WorkTaskRecord.ListExportsTaskCols;
    static readonly Col[] DeliverableCols = Hub.Api.Records.DeliverableRecord.ListExportsDeliverableCols;
    static readonly Col[] DecisionCols = Hub.Api.Records.DecisionRecord.ListExportsDecisionCols;
    static readonly Col[] MilestoneCols = Hub.Api.Records.MilestoneRecord.ListExportsMilestoneCols;
    static readonly Col[] ProjectCols = Hub.Api.Records.ProjectRecord.ListExportsProjectCols;
    static readonly Col[] ActivityCols = Hub.Api.Records.ScreenLists.ListExportsActivityCols;

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
        var issueRootCause = http.Request.Path.Value?.EndsWith("/issues/export", StringComparison.OrdinalIgnoreCase) == true
            && (await db.CurrentSettings()).IsFlagOn("register_screens");
        pairs.RemoveAll(pair => string.Equals(pair.Key, "rootCause", StringComparison.OrdinalIgnoreCase) && !issueRootCause);
        var ids = pairs.SelectMany(p => Http.Ids(p.Value)).ToHashSet();
        var names = await VisibleNames(db, access, ids);
        if (issueRootCause)
            foreach (var category in await db.IssueRootCauseCategories.AsNoTracking().Where(category => ids.Contains(category.Id))
                .Select(category => new CategoryName(category.Id, category.Name)).ToListAsync())
                names.TryAdd(category.Id, category.Name);
        return pairs.Select(p =>
        {
            var isCause = string.Equals(p.Key, "rootCause", StringComparison.OrdinalIgnoreCase);
            var label = Text.Get($"param.{(isCause ? "rootCause" : p.Key)}");
            var value = string.Join(", ", Http.List(p.Value).Select(v => isCause && v == "not-recorded"
                ? Text.Get("issueRootCause.notRecorded") : Guid.TryParse(v, out var g) && names.TryGetValue(g, out var n) ? n : v));
            return (label.StartsWith("param.") ? p.Key : label, value);
        }).ToList();
    }

    /// Resolves filter IDs only through records in projects visible to the caller. Unknown or
    /// out-of-scope IDs deliberately remain raw query values in the Parameters sheet.
    static Task<Dictionary<Guid, string>> VisibleNames(HubDb db, Access access, HashSet<Guid> ids) =>
        Hub.Api.Records.EntityRegistry.Names(db, ids, access);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/tasks/export", async (Guid id, string? format, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var s = await store.Get(db);
            var f = TaskFilter.From(http.Request.Query);
            var q = TaskQueries.Sort(TaskQueries.Apply(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == id), f, me.Id, clock.Today(s), s), f.Sort, db, f.WorkspaceId).Take(Export.MaxRows + 1); // ordered as the list (sort=board included)
            var rows = Indicators(Json(await TaskQueries.Rows(db, q, access.Actor)));
            // FR-BKL-08: a Yes/No `backlog` column while `flag.backlog` is on.
            Col[] cols = s.IsFlagOn(TaskRules.BacklogFlag) ? [.. TaskCols, new("backlog", "backlog")] : TaskCols;
            return await ExportFile.Send(db, store, format, Text.Get("export.tasks", p.ProjectNumber), cols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-tasks", clock);
        });
        api.MapGet("/projects/{id:guid}/deliverables/export", async (Guid id, string? format, [AsParameters] DeliverableEndpoints.DeliverableQuery f, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, _) = await access.Project(id, track: false);
            var rows = Indicators(Json(await DeliverableEndpoints.List(id, f, access, db, store)));
            var settings = await store.Get(db);
            var history = settings.IsFlagOn("baseline_history");
            if (history) await BaselineHistory.AddDeltas(db, rows);
            Col[] columns = [.. DeliverableCols,
                .. settings.IsFlagOn("schedule_baseline") ? new Col[] { new("sinceBaselineDays", "sinceBaseline", "number") } : [],
                .. history ? BaselineHistory.DeltaColumns : []];
            return await ExportFile.Send(db, store, format, Text.Get("export.deliverables", p.ProjectNumber), columns, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-deliverables", clock);
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
            var rows = Json(await MilestoneEndpoints.List(id, f, access, db, store));
            foreach (var r in rows) r!["deliverablesIssued"] = $"{r["deliverableIssued"]}/{r["deliverableTotal"]}";
            return await ExportFile.Send(db, store, format, Text.Get("export.milestones", p.ProjectNumber), (await store.Get(db)).IsFlagOn("schedule_baseline") ? [.. MilestoneCols, new("sinceBaselineDays", "sinceBaseline", "number")] : MilestoneCols, rows, await Filters(db, http, access), p.Id, $"{p.ProjectNumber}-milestones", clock);
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

file sealed record CategoryName(Guid Id, string Name);
