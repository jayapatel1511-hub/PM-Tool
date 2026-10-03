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
    static readonly string[] TaskKeys = ["q", "disciplineId", "deliverableId", "status", "priority", "mine", "overdue", "blocked", "blocking", "dueThisWeek", "unassigned",
        "readyForReview", "waiting", "open", "stale", "dueSoon", "noDueDate", "dateInconsistent", "heldPastDue", "dueFrom", "dueTo", "milestoneId", "assigneeId", "ids",
        "sort", "group", "cols"];

    /// The parameters each list accepts (§18.3); anything else in a stored view is dropped when it is opened.
    public static readonly Dictionary<string, string[]> Lists = new()
    {
        ["tasks"] = TaskKeys,
        ["reviews"] = ["q", "status", "ownerId", "disciplineId", "mine"],
        ["changes"] = ["q", "status", "ownerId", "mine"],
        ["handoffs"] = ["q", "status", "direction", "disciplineId", "overdue"],
        ["submissions"] = ["q", "status", "coordinatorId", "milestoneId", "targetFrom", "targetTo"],
        ["allocations"] = ["q", "personId", "purpose", "status", "from", "to"],
        ["design-basis"] = ["kind", "status", "discipline", "scope", "overdue", "affectedWorkId"],
        ["readiness"] = ["from", "to"],
        ["issues"] = ["q", "status", "severity", "ownerId", "disciplineId", "indicator", "location", "document", "revision", "verification", "alignment", "issueType", "stationFrom", "stationTo", "stationUnits", "group", "sort", "cols"],
        ["board"] = [.. TaskKeys, "swim", "side"],
        ["deliverables"] = ["q", "disciplineId", "status", "milestoneId", "ownerId", "typeId", "indicator", "dueFrom", "dueTo", "requiresReview", "group", "sort", "cols"],
        ["decisions"] = ["q", "status", "ownerId", "ownerType", "impact", "requiredFrom", "requiredTo", "blocking", "indicator", "sort", "cols"],
        ["milestones"] = ["type", "showCompleted"],
        ["projects"] = ["q", "status", "pmId", "clientId", "officeId", "phaseId", "disciplineId", "health", "projectTypeId", "includeArchived", "mine", "starred", "priority", "sort", "submissionWithinDays"],
        ["portfolio"] = ["q", "status", "pmId", "clientId", "officeId", "phaseId", "disciplineId", "health", "projectTypeId", "mine", "submissionWithinDays"],
        ["workload"] = ["supervisorId", "disciplineId", "officeId", "projectId", "indicator", "from", "sort"],
        // Cross-project lists keep their project scope too (§36.1, FR-VIS-02); it is re-read against access when opened.
        ["workspace-tasks"] = [.. TaskKeys, "projects", "ws", "projectId"],
        ["workspace-board"] = [.. TaskKeys, "projects", "ws", "projectId", "swim", "side"],
        ["mywork"] = ["tab", "who", "projects", "ws", "project", "discipline", "status", "priority", "from", "to", "hideWaiting", "sort"],
        ["coordination"] = ["discipline", "owner", "from", "to"],
        ["workspace-coordination"] = ["tab", "projects", "ws", "projectId", "disciplineId", "ownerId", "from", "to"],
    };

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/views", async (string listType, Guid? projectId, Access access, HubDb db, CurrentUser me) =>
        {
            Check.OneOf(listType, [.. Lists.Keys], "listType");
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
        api.MapPost("/views", async (ViewBody body, Access access, HubDb db, CurrentUser me) =>
        {
            Check.OneOf(body.ListType, [.. Lists.Keys], "listType");
            var scope = body.Scope ?? Personal;
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
        api.MapPatch("/views/{id:guid}", async (Guid id, JsonElement body, HttpContext http, Access access, HubDb db, CurrentUser me) =>
        {
            var v = await Editable(db, access, me, id);
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
        api.MapDelete("/views/{id:guid}", async (Guid id, Access access, HubDb db, CurrentUser me) =>
        {
            db.SavedViews.Remove(await Editable(db, access, me, id)); // a preference, not a work item: removed outright
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

    static async Task<SavedView> Editable(HubDb db, Access access, CurrentUser me, Guid id)
    {
        var v = await db.SavedViews.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (v.Scope == Personal) { if (v.OwnerId != me.Id) throw ApiException.NotFound(); return v; }
        var (_, ctx) = await access.Project(v.ProjectId!.Value, track: false);
        Access.Demand(Permissions.ManageSavedProjectView(access.Actor, ctx)); // members see project views; only the PM and leads change them
        return v;
    }

    /// Definitions only (FR-003): the list's own parameters, with sort, grouping and columns also kept in their fields.
    static void Apply(SavedView v, Dictionary<string, string> ps, string listType)
    {
        var known = Lists[listType];
        var kept = ps.Where(p => known.Contains(p.Key) && !string.IsNullOrWhiteSpace(p.Value)).ToDictionary(p => p.Key, p => p.Value.Trim());
        Check.That(kept.Values.All(x => x.Length <= 2000), "params", "error.too_long", 2000);
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
        var known = Lists[listType];
        var kept = new Dictionary<string, string>();
        var dropped = new List<string>();
        foreach (var (k, value) in ps)
        {
            if (!known.Contains(k)) { dropped.Add(k); continue; }
            var ids = Http.Ids(value);
            var ok = k switch
            {
                "disciplineId" when projectId is not null => await Count(db.ProjectDisciplines.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.IsActive).Select(x => x.Id), ids),
                "discipline" when projectId is not null => await Count(db.ProjectDisciplines.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.IsActive).Select(x => x.Id), ids),
                "disciplineId" => await Count(db.Disciplines.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "owner" when projectId is not null => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "deliverableId" => await Count(db.Deliverables.Where(x => ids.Contains(x.Id)).Select(x => x.Id), ids),
                "milestoneId" => await Count(db.Milestones.Where(x => ids.Contains(x.Id) && !x.IsCancelled).Select(x => x.Id), ids),
                "coordinatorId" or "personId" when projectId is not null => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive
                    && db.ProjectMembers.Any(m => m.ProjectId == projectId && m.UserId == x.Id && m.RemovedAt == null)).Select(x => x.Id), ids),
                "affectedWorkId" when projectId is not null => await Count(db.Tasks.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.DeletedAt == null).Select(x => x.Id)
                    .Concat(db.Deliverables.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.DeletedAt == null).Select(x => x.Id)), ids),
                "assigneeId" or "ownerId" or "pmId" or "supervisorId" => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids)
                    || (k == "ownerId" && await Count(db.ExternalParties.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids)),
                "typeId" => await Count(db.DeliverableTypes.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "clientId" => await Count(db.Clients.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "officeId" => await Count(db.Offices.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "phaseId" => await Count(db.Phases.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "projectTypeId" => await Count(db.ProjectTypes.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "projectId" => await Count(db.Projects.Where(x => ids.Contains(x.Id)).Select(x => x.Id), ids),
                _ => true,
            };
            if (ok) kept[k] = value; else dropped.Add(k);
        }
        return (kept, dropped);
    }

    static async Task<bool> Count(IQueryable<Guid> found, Guid[] ids) => ids.Length > 0 && await found.CountAsync() == ids.Length;
}
