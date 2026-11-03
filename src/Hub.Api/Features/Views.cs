using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Saved views (§18.4, FR-VIEW-04) and manual board order (§13.4, §36.3). A view stores a list's query parameters
/// (filters, sort, grouping, columns), never results; on reading, parameters the list no longer knows or values that no
/// longer exist are dropped and named, so the view still opens.
public static class ViewEndpoints
{
    public sealed record ViewBody(string Name, string ListType, Guid? ProjectId, string? Scope, Dictionary<string, string>? Params, bool? IsDefault);
    public sealed record OrderBody(Guid[] TaskIds);
    /// One saved view as a list's Views menu reads it: its definition (`Params`), the parameters dropped because the list no longer knows them or their value is gone (`Dropped`), and whether the reader may change it.
    public sealed record SavedViewRow(Guid Id, string Name, string Scope, Guid? ProjectId, bool IsDefault, int RowVersion, Dictionary<string, string> Params, List<string> Dropped, string? Owner, bool CanEdit);
    /// The reader's personal views of a list, then the project's shared ones; `CanShare` is whether they may save a view for the whole project.
    public sealed record SavedViewList(IReadOnlyList<SavedViewRow> Views, bool CanShare);
    /// The answer to saving or changing a view: the id and the version the next write must send.
    public sealed record ViewSaved(Guid Id, int RowVersion);

    const string Personal = "Personal", ProjectScope = "Project";
    static readonly string[] PortfolioGroups = ["none", "client", "office", "projectType"];
    public static readonly IReadOnlyDictionary<string, string[]> Lists = Hub.Api.Records.ScreenLists.SavedViewKeys;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/views", async Task<Ok<SavedViewList>> (string listType, Guid? projectId, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            Check.OneOf(listType, [.. Lists.Keys], "listType");
            DemandTeamView(listType, projectId, null, settings);
            CalendarViewParameters.Demand(listType, settings);
            TimelineViewParameters.Demand(listType, projectId, null, settings);
            var report = IsReport(listType) ? await ReportEndpoints.SavedViewDefinition(listType, access, db, store) : null;
            if (listType == "planner") { await PeopleScope.DemandWorkload(db, access.Actor); Check.That(projectId == null, "projectId", "view.personal_only"); }
            if (listType == "risks") Http.RequireFlag(settings, "record_engine_additions"); // §47.5 FR-REC-12
            if (listType == "staff") { Http.RequireFlag(settings, "small_ux"); Access.Demand(Permissions.ViewStaff(access.Actor)); Check.That(projectId == null, "projectId", "view.personal_only"); }
            var ctx = projectId is { } pid ? (await access.Project(pid, track: false)).Ctx : null;
            var views = await db.SavedViews.AsNoTracking().Where(v => v.ListType == listType
                && ((v.Scope == Personal && v.OwnerId == me.Id && v.ProjectId == projectId) || (projectId != null && v.Scope == ProjectScope && v.ProjectId == projectId)))
                .OrderBy(v => v.Scope).ThenBy(v => v.Name).ToListAsync();
            var canShare = ctx is not null && Permissions.ManageSavedProjectView(access.Actor, ctx).Ok;
            var ownerIds = views.Select(v => v.OwnerId).Distinct().ToList();
            var owners = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
            var rows = new List<SavedViewRow>();
            foreach (var v in views)
            {
                var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(v.Filters) ?? [];
                var (kept, dropped) = report is not null ? await ReportViewParameters.Read(report, parameters, access, db)
                    : listType == "calendar" ? await CalendarViewParameters.Read(projectId, parameters, access, db)
                    : TimelineViewParameters.Owns(listType) ? await TimelineViewParameters.Read(listType, projectId, parameters, access, db, settings)
                    : await Clean(db, listType, projectId, parameters, settings);
                rows.Add(new SavedViewRow(v.Id, v.Name, v.Scope, v.ProjectId, v.IsDefault, v.RowVersion, kept, dropped, owners.GetValueOrDefault(v.OwnerId),
                    v.Scope == Personal ? v.OwnerId == me.Id : canShare));
            }
            return TypedResults.Ok(new SavedViewList(rows, canShare));
        });
        api.MapPost("/views", async Task<Created<ViewSaved>> (ViewBody body, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            Check.OneOf(body.ListType, [.. Lists.Keys], "listType");
            var report = IsReport(body.ListType) ? await ReportEndpoints.SavedViewDefinition(body.ListType, access, db, store) : null;
            var scope = body.Scope ?? Personal;
            DemandTeamView(body.ListType, body.ProjectId, scope, settings);
            CalendarViewParameters.Demand(body.ListType, settings);
            TimelineViewParameters.Demand(body.ListType, body.ProjectId, scope, settings);
            if (body.ListType == "planner") { await PeopleScope.DemandWorkload(db, access.Actor); Check.That(scope == Personal && body.ProjectId == null, "scope", "view.personal_only"); }
            if (body.ListType == "risks") Http.RequireFlag(settings, "record_engine_additions");
            if (body.ListType == "staff") { Http.RequireFlag(settings, "small_ux"); Access.Demand(Permissions.ViewStaff(access.Actor)); Check.That(scope == Personal && body.ProjectId == null, "scope", "view.personal_only"); }
            Check.OneOf(scope, [Personal, ProjectScope], "scope");
            if (body.ProjectId is { } pid)
            {
                var (_, ctx) = await access.Project(pid, track: false);
                if (scope == ProjectScope) Access.Demand(Permissions.ManageSavedProjectView(access.Actor, ctx)); // FR-002: the PM and leads share
            }
            else Check.That(scope == Personal, "scope", "view.project_needed");
            var v = new SavedView { OwnerId = me.Id, Scope = scope, ProjectId = body.ProjectId, ListType = body.ListType, Name = Check.Required(body.Name, "name", 100) };
            var parameters = body.Params ?? [];
            if (report is not null) parameters = await ReportViewParameters.Write(report, parameters, access, db);
            else if (body.ListType == "calendar") parameters = await CalendarViewParameters.Write(body.ProjectId, parameters, access, db);
            else if (TimelineViewParameters.Owns(body.ListType)) parameters = await TimelineViewParameters.Write(body.ListType, body.ProjectId, parameters, access, db, settings);
            else if (body.ListType == "deliverables") parameters = await DeliverableViewParameters.Write(parameters, db, settings);
            else if (body.ListType == "issues") parameters = await IssueParameters(parameters, db, settings);
            Apply(v, parameters, body.ListType);
            db.SavedViews.Add(v);
            if (body.IsDefault == true) await MakeDefault(db, v, me.Id);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/views/{v.Id}", new ViewSaved(v.Id, v.RowVersion));
        }).WithMetadata(new CalendarViewReplay.Guard());
        api.MapPatch("/views/{id:guid}", async Task<Ok<ViewSaved>> (Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            var v = await Editable(db, access, me, id, settings, store);
            var patch = new Patch(body);
            await Http.CheckVersion(db, http, v, patch.RowVersion);
            if (patch.Has("name")) v.Name = Check.Required(patch.Str("name"), "name", 100);
            if (patch.Has("params"))
            {
                var parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(patch.Raw.GetProperty("params").GetRawText()) ?? [];
                if (IsReport(v.ListType)) parameters = await ReportViewParameters.Write(await ReportEndpoints.SavedViewDefinition(v.ListType, access, db, store), parameters, access, db);
                else if (v.ListType == "calendar") parameters = await CalendarViewParameters.Write(v.ProjectId, parameters, access, db);
                else if (TimelineViewParameters.Owns(v.ListType)) parameters = await TimelineViewParameters.Write(v.ListType, v.ProjectId, parameters, access, db, settings);
                else if (v.ListType == "deliverables") parameters = await DeliverableViewParameters.Write(parameters, db, settings);
                else if (v.ListType == "issues") parameters = await IssueParameters(parameters, db, settings);
                Apply(v, parameters, v.ListType);
            }
            if (patch.Has("isDefault"))
            {
                if (patch.Bool("isDefault") == true) await MakeDefault(db, v, me.Id);
                else v.IsDefault = false;
            }
            await db.SaveChangesAsync();
            return TypedResults.Ok(new ViewSaved(v.Id, v.RowVersion));
        });
        api.MapDelete("/views/{id:guid}", async Task<NoContent> (Guid id, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            db.SavedViews.Remove(await Editable(db, access, me, id, await store.Get(db), store)); // a preference, not a work item: removed outright
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });

        // §13.4, FR-004: the lane's cards in the order the team arranged them, shared by the project's members.
        api.MapPut("/projects/{id:guid}/board-order", async Task<NoContent> (Guid id, OrderBody body, Access access, HubDb db) =>
        {
            var (_, ctx) = await access.Project(id, track: false);
            Access.Demand(Permissions.ArrangeBoard(access.Actor, ctx));
            var ids = body.TaskIds.Distinct().ToList();
            Check.That(ids.Count > 0 && ids.Count <= 500, "taskIds", "error.required");
            Check.That(await db.Tasks.CountAsync(t => t.ProjectId == id && ids.Contains(t.Id)) == ids.Count, "taskIds", "task.other_project");
            var rows = await db.BoardOrders.Where(b => b.ProjectId == id && ids.Contains(b.TaskId)).ToDictionaryAsync(b => b.TaskId);
            // ponytail: positions are rewritten for the dropped lane only; last write wins, which is what manual order means.
            for (var i = 0; i < ids.Count; i++)
            {
                if (!rows.TryGetValue(ids[i], out var row)) db.BoardOrders.Add(row = new BoardOrder { ProjectId = id, TaskId = ids[i] });
                row.Position = i + 1;
            }
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });
    }

    static bool IsReport(string listType) => listType.StartsWith("report:", StringComparison.Ordinal);

    static async Task<SavedView> Editable(HubDb db, Access access, CurrentUser me, Guid id, OrgSettings settings, SettingsStore store)
    {
        var v = await db.SavedViews.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        DemandTeamView(v.ListType, v.ProjectId, v.Scope, settings);
        CalendarViewParameters.Demand(v.ListType, settings);
        if (v.ListType == "calendar" && v.ProjectId is { } calendarProject) await access.Project(calendarProject, track: false);
        TimelineViewParameters.Demand(v.ListType, v.ProjectId, v.Scope, settings);
        if (v.ListType == "timeline" && v.ProjectId is { } timelineProject) await access.Project(timelineProject, track: false);
        if (IsReport(v.ListType))
        {
            await ReportEndpoints.SavedViewDefinition(v.ListType, access, db, store);
            if (v.ProjectId is { } container) await access.Project(container, track: false);
        }
        if (v.ListType == "planner") await PeopleScope.DemandWorkload(db, access.Actor);
        if (v.ListType == "risks") Http.RequireFlag(settings, "record_engine_additions");
        if (v.ListType == "staff") { Http.RequireFlag(settings, "small_ux"); Access.Demand(Permissions.ViewStaff(access.Actor)); }
        if (v.Scope == Personal) { if (v.OwnerId != me.Id) throw ApiException.NotFound(); return v; }
        var (_, ctx) = await access.Project(v.ProjectId!.Value, track: false);
        Access.Demand(Permissions.ManageSavedProjectView(access.Actor, ctx)); // members see project views; only the PM and leads change them
        return v;
    }

    /// Definitions only (FR-003): the list's own parameters, with sort, grouping and columns also kept in their fields.
    static void DemandTeamView(string listType, Guid? projectId, string? scope, OrgSettings settings)
    {
        if (listType is not ("project-team" or "workspace-team")) return;
        Http.RequireFlag(settings, "people_screens");
        if (listType == "workspace-team") Check.That(projectId == null && (scope == null || scope == Personal), "scope", "view.personal_only");
        else Check.That(projectId != null, "projectId", "view.project_needed");
    }

    static void Apply(SavedView v, Dictionary<string, string> ps, string listType)
    {
        if (listType == "planner" && ps.TryGetValue("view", out var legacy) && legacy is "list" or "projects")
        {
            ps = new Dictionary<string, string>(ps);
            ps.TryAdd("layout", legacy);
            ps.Remove("view");
        }
        var known = Lists[listType];
        var kept = ps.Where(p => known.Contains(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value.Trim());
        Check.That(kept.Values.All(x => x.Length <= 2000), "params", "error.too_long", 2000);
        if (listType == "portfolio" && kept.TryGetValue("group", out var group)) Check.OneOf(group, PortfolioGroups, "group");
        if (listType is "project-team" or "workspace-team" && kept.TryGetValue("group", out var teamGroup))
            Check.OneOf(teamGroup, ["none", "homeDiscipline", "office", "supervisor", "unit"], "group");
        v.Filters = JsonSerializer.Serialize(kept);
        v.Sort = kept.GetValueOrDefault("sort");
        v.GroupBy = kept.GetValueOrDefault("group") ?? kept.GetValueOrDefault("swim");
        v.Columns = JsonSerializer.Serialize(kept.TryGetValue("cols", out var c) ? Http.List(c) : []);
    }

    static async Task<Dictionary<string, string>> IssueParameters(Dictionary<string, string> parameters, HubDb db, OrgSettings settings)
    {
        if (!parameters.TryGetValue("rootCause", out var raw)) return parameters;
        var result = new Dictionary<string, string>(parameters);
        if (!settings.IsFlagOn("register_screens") || string.IsNullOrWhiteSpace(raw))
        { result.Remove("rootCause"); return result; }
        var value = raw.Trim();
        if (value != "not-recorded")
        {
            Check.That(Guid.TryParse(value, out var id) && id != Guid.Empty, "rootCause", "error.invalid");
            Check.That(await db.IssueRootCauseCategories.AnyAsync(category => category.Id == id), "rootCause", "error.invalid");
            value = id.ToString();
        }
        result["rootCause"] = value;
        return result;
    }

    /// One default per person, list and project (§18.4).
    static async Task MakeDefault(HubDb db, SavedView v, Guid me)
    {
        foreach (var other in await db.SavedViews.Where(x => x.OwnerId == me && x.ListType == v.ListType && x.ProjectId == v.ProjectId && x.IsDefault && x.Id != v.Id).ToListAsync())
            other.IsDefault = false;
        v.IsDefault = true;
    }

    /// Drops parameters the list no longer accepts and references that no longer resolve (edge case of the spec).
    static async Task<(Dictionary<string, string> Kept, List<string> Dropped)> Clean(HubDb db, string listType, Guid? projectId, Dictionary<string, string> ps, OrgSettings settings)
    {
        if (listType == "planner" && ps.TryGetValue("view", out var legacy) && legacy is "list" or "projects")
        {
            ps = new Dictionary<string, string>(ps);
            ps.TryAdd("layout", legacy);
            ps.Remove("view");
        }
        var known = Lists[listType];
        var kept = new Dictionary<string, string>();
        var dropped = new List<string>();
        foreach (var (k, value) in ps)
        {
            if (!known.Contains(k)) { dropped.Add(k); continue; }
            if (listType == "issues" && k == "rootCause" && !settings.IsFlagOn("register_screens"))
            { dropped.Add(k); continue; }
            if (listType == "deliverables" && DeliverableViewParameters.Owns(k))
            {
                var (normal, wasDropped) = await DeliverableViewParameters.Read(k, value, db, settings);
                if (normal is not null) kept[k] = normal;
                if (wasDropped) dropped.Add(k);
                continue;
            }
            var ok = await Hub.Api.Records.ScreenLists.ValidReference(db, projectId, k, value, listType);
            if (ok) kept[k] = value; else dropped.Add(k);
        }
        return (kept, dropped);
    }

}
