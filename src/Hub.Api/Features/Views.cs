using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Saved views (§18.4, FR-VIEW-04) and manual board order (§13.4, §36.3). A view stores a list's query parameters
/// (filters, sort, grouping, columns), never results; on reading, parameters the list no longer knows or values that no
/// longer exist are dropped and named, so the view still opens.
public static class ViewEndpoints
{
    public sealed record ViewBody(string Name, string ListType, Guid? ProjectId, string? Scope, Dictionary<string, string>? Params, bool? IsDefault);
    public sealed record OrderBody(Guid[] TaskIds);

    const string Personal = "Personal", ProjectScope = "Project";
    static readonly string[] PortfolioGroups = ["none", "client", "office", "projectType"];
    public static readonly IReadOnlyDictionary<string, string[]> Lists = Hub.Api.Records.ScreenLists.SavedViewKeys;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/views", async (string listType, Guid? projectId, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            Check.OneOf(listType, [.. Lists.Keys], "listType");
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
            var rows = new List<object>();
            foreach (var v in views)
            {
                var (kept, dropped) = await Clean(db, listType, projectId, JsonSerializer.Deserialize<Dictionary<string, string>>(v.Filters) ?? []);
                rows.Add(new
                {
                    v.Id, v.Name, v.Scope, v.ProjectId, v.IsDefault, v.RowVersion, Params = kept, Dropped = dropped, Owner = owners.GetValueOrDefault(v.OwnerId),
                    CanEdit = v.Scope == Personal ? v.OwnerId == me.Id : canShare,
                });
            }
            return new { Views = rows, CanShare = canShare };
        });
        api.MapPost("/views", async (ViewBody body, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            Check.OneOf(body.ListType, [.. Lists.Keys], "listType");
            var scope = body.Scope ?? Personal;
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
            Apply(v, body.Params ?? [], body.ListType);
            db.SavedViews.Add(v);
            if (body.IsDefault == true) await MakeDefault(db, v, me.Id);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/views/{v.Id}", new { v.Id, v.RowVersion });
        });
        api.MapPatch("/views/{id:guid}", async (Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            var v = await Editable(db, access, me, id, await store.Get(db));
            var patch = new Patch(body);
            await Http.CheckVersion(db, http, v, patch.RowVersion);
            if (patch.Has("name")) v.Name = Check.Required(patch.Str("name"), "name", 100);
            if (patch.Has("params")) Apply(v, JsonSerializer.Deserialize<Dictionary<string, string>>(patch.Raw.GetProperty("params").GetRawText()) ?? [], v.ListType);
            if (patch.Has("isDefault"))
            {
                if (patch.Bool("isDefault") == true) await MakeDefault(db, v, me.Id);
                else v.IsDefault = false;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { v.Id, v.RowVersion });
        });
        api.MapDelete("/views/{id:guid}", async (Guid id, Access access, HubDb db, CurrentUser me, SettingsStore store) =>
        {
            db.SavedViews.Remove(await Editable(db, access, me, id, await store.Get(db))); // a preference, not a work item: removed outright
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // §13.4, FR-004: the lane's cards in the order the team arranged them, shared by the project's members.
        api.MapPut("/projects/{id:guid}/board-order", async (Guid id, OrderBody body, Access access, HubDb db) =>
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
            return Results.NoContent();
        });
    }

    static async Task<SavedView> Editable(HubDb db, Access access, CurrentUser me, Guid id, OrgSettings settings)
    {
        var v = await db.SavedViews.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (v.ListType == "planner") await PeopleScope.DemandWorkload(db, access.Actor);
        if (v.ListType == "risks") Http.RequireFlag(settings, "record_engine_additions");
        if (v.ListType == "staff") { Http.RequireFlag(settings, "small_ux"); Access.Demand(Permissions.ViewStaff(access.Actor)); }
        if (v.Scope == Personal) { if (v.OwnerId != me.Id) throw ApiException.NotFound(); return v; }
        var (_, ctx) = await access.Project(v.ProjectId!.Value, track: false);
        Access.Demand(Permissions.ManageSavedProjectView(access.Actor, ctx)); // members see project views; only the PM and leads change them
        return v;
    }

    /// Definitions only (FR-003): the list's own parameters, with sort, grouping and columns also kept in their fields.
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
        v.Filters = JsonSerializer.Serialize(kept);
        v.Sort = kept.GetValueOrDefault("sort");
        v.GroupBy = kept.GetValueOrDefault("group") ?? kept.GetValueOrDefault("swim");
        v.Columns = JsonSerializer.Serialize(kept.TryGetValue("cols", out var c) ? Http.List(c) : []);
    }

    /// One default per person, list and project (§18.4).
    static async Task MakeDefault(HubDb db, SavedView v, Guid me)
    {
        foreach (var other in await db.SavedViews.Where(x => x.OwnerId == me && x.ListType == v.ListType && x.ProjectId == v.ProjectId && x.IsDefault && x.Id != v.Id).ToListAsync())
            other.IsDefault = false;
        v.IsDefault = true;
    }

    /// Drops parameters the list no longer accepts and references that no longer resolve (edge case of the spec).
    static async Task<(Dictionary<string, string> Kept, List<string> Dropped)> Clean(HubDb db, string listType, Guid? projectId, Dictionary<string, string> ps)
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
            var ok = await Hub.Api.Records.ScreenLists.ValidReference(db, projectId, k, value);
            if (ok) kept[k] = value; else dropped.Add(k);
        }
        return (kept, dropped);
    }

}
