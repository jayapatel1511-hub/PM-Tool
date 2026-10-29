using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// The project scope of the workspace views (§36.1, FR-VIS-02): "mine" (projects the caller manages or is a member of,
/// as the project list's default), "all" permitted projects, or explicit ids. Visibility is applied on every read, so a
/// named set, saved view or shared link never widens what the reader may see; archived and cancelled projects drop out.
public static class Scope
{
    public const string Mine = "mine", All = "all";

    public static IQueryable<Project> Projects(Access access, HubDb db, string? projects)
    {
        var q = access.VisibleProjects().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        if (string.IsNullOrWhiteSpace(projects) || projects == All) return q;
        if (projects == Mine)
        {
            var me = access.Me.Id;
            return q.Where(p => p.ProjectManagerId == me || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == me && m.RemovedAt == null));
        }
        var ids = Http.Ids(projects);
        return q.Where(p => ids.Contains(p.Id));
    }
}

/// Named workspaces, the overview dashboard, the Files library and the Team list (§36.1, §36.6, §36.8; FR-VIS-01/02/07/09).
public static class WorkspaceEndpoints
{
    public sealed record Body(string? Name, Guid[]? ProjectIds, int? RowVersion = null);
    public sealed record Row(Guid Id, string Name, List<Guid> ProjectIds, int Hidden, int RowVersion);
    public sealed record Widget(string Id, bool Hidden);
    public sealed record LayoutBody(Widget[] Widgets);
    /// The saved layout a person's dashboard answers with, and whether it is still the default.
    public sealed record LayoutAnswer(List<Widget> Layout, bool IsDefault);

    /// One lane of the board, the statuses it counts and how many tasks are in it.
    public sealed record HomeLane(string Lane, string Statuses, int Count);
    public sealed record HomeOnTrack(int Green, int Evaluated, int? Pct);
    public sealed record HomeMetrics(int ActiveProjects, int OpenTasks, int OverdueTasks, HomeOnTrack OnTrack, int TeamMembers);
    public sealed record HomeOnHold(string Status, int Count);
    public sealed record HomeProject(Guid ProjectId, string ProjectNumber, string Name, List<HomeLane> Lanes, int Total);
    public sealed record HomeDeadline(string Type, Guid Id, Guid ProjectId, string? ProjectNumber, string Key, string Name, DateOnly Date, string? Status, string? Owner, bool Overdue);
    public sealed record HomeDeadlines(int Total, bool Truncated, List<HomeDeadline> Items);
    /// The overview: current totals for the scope, the lanes, each project's lanes, the deadlines in the range and the person's layout.
    public sealed record HomeRead(DateOnly Today, DateOnly From, DateOnly To, DateTimeOffset AsOf, HomeMetrics Metrics, List<HomeLane> TasksByStatus, HomeOnHold OnHold,
        List<HomeProject> TasksByProject, HomeDeadlines UpcomingDeadlines, List<Widget> Layout, bool IsDefault);

    public sealed record FileLinkRow(Guid Id, string Title, string Url, string LinkType, bool IsNetworkPath, string? AddedBy, DateTimeOffset AddedAt);
    public sealed record FileItemRow(string ItemType, Guid ItemId, string Key, string Name, List<FileLinkRow> Links);
    public sealed record FileProjectRow(Guid ProjectId, string ProjectNumber, string Name, bool CanAdd, int Count, List<FileItemRow> Items);
    public sealed record FilesRead(List<FileProjectRow> Projects, int Total, bool Truncated, IReadOnlyList<string> Types);

    public sealed record TeamProjectRow(Guid ProjectId, string ProjectNumber, string ProjectName, List<string> Roles, List<string> Leads);
    public sealed record TeamPersonRow(Guid UserId, string DisplayName, string? JobTitle, string Email, List<TeamProjectRow> Projects);
    public sealed record TeamRead(List<TeamPersonRow> People, int Projects);

    /// What the reads below take from the database before the names are put beside them.
    sealed record Named(Guid Id, string ProjectNumber, string Name);
    sealed record StatusCount(Guid ProjectId, string Status, int N);
    sealed record Due(string Type, Guid Id, Guid ProjectId, string Key, string Name, DateOnly Date, string? Status, string? Owner, bool Overdue);
    sealed record LinkJoin(Guid Id, Guid ProjectId, string ItemType, Guid ItemId, string Title, string Url, string LinkType, DateTimeOffset AddedAt, string? AddedBy);
    sealed record ItemLabel(Guid Id, string Key, string Name);
    sealed record MemberJoin(Guid Id, string DisplayName, string? JobTitle, string Email, Guid ProjectId, List<string> Roles);
    sealed record LeadRow(Guid ProjectId, Guid Lead, string Name);

    /// The overview's fixed widgets in their default order (FR-VIS-07); a person may reorder or hide them, not redefine them.
    public static readonly string[] Widgets = ["metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/workspaces", (Access access, HubDb db) => List(access, db));
        api.MapPost("/workspaces", async Task<Created<Row>> (Body body, Access access, HubDb db, TimeProvider clock) =>
        {
            var ws = new Workspace { OwnerId = access.Me.Id, CreatedAt = clock.GetUtcNow() };
            await Apply(ws, body, access, db, creating: true);
            db.Workspaces.Add(ws);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/workspaces/{ws.Id}", (await List(access, db)).First(w => w.Id == ws.Id));
        });
        api.MapPatch("/workspaces/{id:guid}", async (Guid id, Body body, HttpContext http, Access access, HubDb db) =>
        {
            var ws = await Own(db, access, id);
            await Http.CheckVersion(db, http, ws, body.RowVersion);
            await Apply(ws, body, access, db, creating: false);
            if (body.ProjectIds is not null) db.Entry(ws).Property(w => w.RowVersion).IsModified = true;
            await db.SaveChangesAsync();
            return (await List(access, db)).First(w => w.Id == id);
        });
        api.MapDelete("/workspaces/{id:guid}", async Task<NoContent> (Guid id, HttpContext http, Access access, HubDb db) =>
        {
            var ws = await Own(db, access, id);
            await Http.CheckVersion(db, http, ws);
            await Tx.Run(db, async () =>
            {
                db.Workspaces.Remove(ws);
                await db.SaveChangesAsync();
                return await db.BoardOrders.Where(b => b.WorkspaceId == id).ExecuteDeleteAsync();
            });
            return TypedResults.NoContent();
        });
        // §36.3 FR-VIS-04: a named workspace's board keeps its owner's own card order, separate from each project's.
        api.MapPut("/workspaces/{id:guid}/board-order", async Task<NoContent> (Guid id, ViewEndpoints.OrderBody body, Access access, HubDb db) =>
        {
            await Own(db, access, id);
            var ids = body.TaskIds.Distinct().ToList();
            Check.That(ids.Count is > 0 and <= 500, "taskIds", "error.required");
            var inScope = Scope.Projects(access, db, null).Where(p => db.WorkspaceProjects.Any(w => w.WorkspaceId == id && w.ProjectId == p.Id)).Select(p => p.Id);
            Check.That(await db.Tasks.CountAsync(t => ids.Contains(t.Id) && inScope.Contains(t.ProjectId)) == ids.Count, "taskIds", "workspace.task_outside");
            var rows = await db.BoardOrders.Where(b => b.WorkspaceId == id && ids.Contains(b.TaskId)).ToDictionaryAsync(b => b.TaskId);
            for (var i = 0; i < ids.Count; i++)
            {
                if (!rows.TryGetValue(ids[i], out var row)) db.BoardOrders.Add(row = new BoardOrder { WorkspaceId = id, TaskId = ids[i] });
                row.Position = i + 1;
            }
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });

        api.MapGet("/home", Home);
        api.MapPut("/me/dashboard-layout", async Task<Ok<LayoutAnswer>> (LayoutBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var widgets = Normalise(body.Widgets);
            var row = await db.DashboardLayouts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (row is null) db.DashboardLayouts.Add(row = new DashboardLayout { UserId = me.Id });
            row.Widgets = System.Text.Json.JsonSerializer.Serialize(widgets, JsonOpts.Web);
            row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return TypedResults.Ok(new LayoutAnswer(widgets, false));
        });
        api.MapDelete("/me/dashboard-layout", async Task<Ok<LayoutAnswer>> (HubDb db, CurrentUser me) =>
        {
            await db.DashboardLayouts.Where(x => x.UserId == me.Id).ExecuteDeleteAsync();
            return TypedResults.Ok(new LayoutAnswer(Normalise([]), true));
        });

        api.MapGet("/files", Files);
        api.MapGet("/team", Team);
    }

    // ---------- Named workspaces (FR-VIS-02) ----------

    /// The caller's workspaces with only the projects they may still see; the rest are counted, never named.
    static async Task<List<Row>> List(Access access, HubDb db)
    {
        var me = access.Me.Id;
        var visible = access.VisibleProjectIds();
        var rows = await db.Workspaces.AsNoTracking().Where(w => w.OwnerId == me).OrderBy(w => w.Name)
                .Select(w => new
            {
                w.Id, w.Name, w.RowVersion, Total = w.Projects.Count,
                Visible = w.Projects.Where(p => visible.Contains(p.ProjectId)).OrderBy(p => p.SortOrder).Select(p => p.ProjectId).ToList(),
            }).ToListAsync();
        return rows.Select(w => new Row(w.Id, w.Name, w.Visible, w.Total - w.Visible.Count, w.RowVersion)).ToList();
    }

    static async Task<Workspace> Own(HubDb db, Access access, Guid id) =>
        await db.Workspaces.Include(w => w.Projects).FirstOrDefaultAsync(w => w.Id == id && w.OwnerId == access.Me.Id) ?? throw ApiException.NotFound();

    static async Task Apply(Workspace ws, Body body, Access access, HubDb db, bool creating)
    {
        if (creating || body.Name is not null)
        {
            var name = Check.Required(body.Name, "name", 100);
            Check.That(!await db.Workspaces.AnyAsync(w => w.OwnerId == ws.OwnerId && w.Name == name && w.Id != ws.Id), "name", "workspace.duplicate");
            ws.Name = name;
        }
        if (!creating && body.ProjectIds is null) return;
        var ids = (body.ProjectIds ?? []).Distinct().ToList();
        Check.That(ids.Count is > 0 and <= 100, "projectIds", "workspace.projects");
        Check.That(await access.VisibleProjects().CountAsync(p => ids.Contains(p.Id)) == ids.Count, "projectIds", "workspace.not_visible");
        // Kept rows stay (the pair is unique), dropped ones go, new ones join in the order given.
        foreach (var gone in ws.Projects.Where(p => !ids.Contains(p.ProjectId)).ToList()) ws.Projects.Remove(gone);
        var kept = ws.Projects.ToDictionary(p => p.ProjectId);
        for (var i = 0; i < ids.Count; i++)
        {
            if (!kept.TryGetValue(ids[i], out var row))
            {
                row = new WorkspaceProject { WorkspaceId = ws.Id, ProjectId = ids[i] };
                db.WorkspaceProjects.Add(row);
                ws.Projects.Add(row);
            }
            row.SortOrder = i;
        }
    }

    // ---------- Overview dashboard (§36.6, FR-VIS-07, AC-VIS-05) ----------

    /// Snapshot tiles and charts are current values for the scope; only Upcoming Deadlines follows the date range. Every
    /// task figure is the count of the cross-project list with the same filter, so a number always equals the list it opens.
    static async Task<Ok<HomeRead>> Home(string? projects, DateOnly? from, DateOnly? to, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var s = await store.Get(db);
        var today = clock.Today(s);
        var start = from ?? today;
        var end = to ?? today.AddDays(13);
        Check.That(end >= start && end.DayNumber - start.DayNumber <= 366, "to", "home.range_rule");

        var scope = Scope.Projects(access, db, projects);
        var active = scope.Where(p => p.Status == ProjectStatus.Active);
        var activeCount = await active.CountAsync();
        var green = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id && x.ComputedHealth == Health.Green));
        var evaluated = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id
            && (x.ComputedHealth == Health.Green || x.ComputedHealth == Health.Yellow || x.ComputedHealth == Health.Red)));

        var tasks = TaskEndpoints.InScope(access, db, projects);
        Task<int> Count(string key, string value) => TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { [key] = value }), me.Id, today, s).CountAsync();
        var byStatus = new List<HomeLane>();
        foreach (var lane in Workflow.Lanes)
        {
            var statuses = string.Join(',', LaneStatuses(lane));
            byStatus.Add(new HomeLane(lane, statuses, await Count("status", statuses)));
        }

        var pids = scope.Select(p => p.Id);
        var names = await scope.Select(p => new Named(p.Id, p.ProjectNumber, p.Name)).ToDictionaryAsync(p => p.Id);
        var counts = await tasks.Where(t => t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold)
            .GroupBy(t => new { t.ProjectId, t.Status }).Select(g => new StatusCount(g.Key.ProjectId, g.Key.Status, g.Count())).ToListAsync();
        var byProject = counts.GroupBy(c => c.ProjectId).Select(g => new HomeProject(g.Key, names[g.Key].ProjectNumber, names[g.Key].Name,
            Workflow.Lanes.Select(l => new HomeLane(l, string.Join(',', LaneStatuses(l)), g.Where(c => Workflow.Lane(c.Status) == l).Sum(c => c.N))).ToList(),
            g.Sum(c => c.N))).OrderByDescending(p => p.Total).ThenBy(p => p.ProjectNumber).ToList();

        var members = await ActiveMembers(db, pids).Select(m => m.UserId).Distinct().CountAsync();
        var layout = await db.DashboardLayouts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id);

        return TypedResults.Ok(new HomeRead(today, start, end, clock.GetUtcNow(),
            new HomeMetrics(activeCount, await Count("open", "true"), await Count("overdue", "true"),
                new HomeOnTrack(green, evaluated, evaluated == 0 ? null : green * 100 / evaluated), members),
            byStatus,
            new HomeOnHold(TaskStatuses.OnHold, await Count("status", TaskStatuses.OnHold)), // AC-CNT-05: the lanes plus this equal Open Tasks
            byProject,
            await Deadlines(db, pids, names.ToDictionary(n => n.Key, n => n.Value.ProjectNumber), start, end),
            layout is null ? Normalise([]) : Normalise(System.Text.Json.JsonSerializer.Deserialize<Widget[]>(layout.Widgets, JsonOpts.Web) ?? []),
            layout is null));
    }

    static IEnumerable<string> LaneStatuses(string lane) => TaskStatuses.All.Where(x => Workflow.Lane(x) == lane);

    /// Open task, deliverable and milestone due dates in the range, soonest first (the calendar's deadline projection).
    /// Each type is ordered before it is cut, so the soonest items are never dropped (AC-CNT-04).
    static async Task<HomeDeadlines> Deadlines(HubDb db, IQueryable<Guid> pids, Dictionary<Guid, string> numbers, DateOnly from, DateOnly to)
    {
        const int Max = 100;
        var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .OrderBy(t => t.DueDate).ThenBy(t => t.Key).Take(Max + 1)
            .Select(t => new Due(ItemType.Task, t.Id, t.ProjectId, t.Key, t.Name, t.DueDate!.Value, t.Status,
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue))).ToListAsync();
        var dels = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to
                && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
            .OrderBy(d => d.DueDate).ThenBy(d => d.Key).Take(Max + 1)
            .Select(d => new Due(ItemType.Deliverable, d.Id, d.ProjectId, d.Key, d.Name, d.DueDate!.Value, d.Status,
                db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue))).ToListAsync();
        var ms = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled && !m.IsComplete)
            .OrderBy(m => m.Date).ThenBy(m => m.Key).Take(Max + 1)
            .Select(m => new Due(ItemType.Milestone, m.Id, m.ProjectId, m.Key, m.Name, m.Date!.Value,
                db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault(), null,
                db.MilestoneStates.Any(x => x.MilestoneId == m.Id && x.Status == MilestoneStatus.Overdue))).ToListAsync();
        var all = tasks.Concat(dels).Concat(ms).OrderBy(x => x.Date).ThenBy(x => x.Key).ToList();
        return new HomeDeadlines(all.Count, all.Count > Max,
            [.. all.Take(Max).Select(x => new HomeDeadline(x.Type, x.Id, x.ProjectId, numbers.GetValueOrDefault(x.ProjectId), x.Key, x.Name, x.Date, x.Status, x.Owner, x.Overdue))]);
    }

    /// Known widgets in the person's order, unknown ones dropped, missing ones appended visible (so new widgets appear).
    public static List<Widget> Normalise(IEnumerable<Widget> given)
    {
        var list = given.Where(w => Widgets.Contains(w.Id)).DistinctBy(w => w.Id).ToList();
        list.AddRange(Widgets.Where(id => list.All(w => w.Id != id)).Select(id => new Widget(id, false)));
        return list;
    }

    /// Active memberships of active people: the Team list and the Team Members figure (FR-VIS-07, FR-VIS-01).
    static IQueryable<ProjectMember> ActiveMembers(HubDb db, IQueryable<Guid> pids) =>
        db.ProjectMembers.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.RemovedAt == null && db.Users.Any(u => u.Id == m.UserId && u.IsActive));

    // ---------- Files (§36.8, FR-VIS-09) ----------

    /// Document links of the scope's projects grouped by project and linked item; a link library only, nothing is stored.
    static async Task<Ok<FilesRead>> Files(string? projects, string? q, string? type, Access access, HubDb db)
    {
        const int Max = 2000;
        var list = await Scope.Projects(access, db, projects).AsNoTracking().OrderBy(p => p.ProjectNumber).ToListAsync();
        var pids = list.Select(p => p.Id).ToList();
        var links = db.DocumentLinks.AsNoTracking().Where(l => pids.Contains(l.ProjectId));
        if (!string.IsNullOrWhiteSpace(type)) links = links.Where(l => l.LinkType == type);
        if (!string.IsNullOrWhiteSpace(q)) { var term = $"%{q.Trim()}%"; links = links.Where(l => EF.Functions.ILike(l.Title, term) || EF.Functions.ILike(l.Url, term)); }
        var rows = await links.OrderBy(l => l.Title).Take(Max + 1)
            .Select(l => new LinkJoin(l.Id, l.ProjectId, l.ItemType, l.ItemId, l.Title, l.Url, l.LinkType, l.AddedAt, db.Users.Where(u => u.Id == l.AddedBy).Select(u => u.DisplayName).FirstOrDefault()))
            .ToListAsync();
        // Item labels; a link on a deleted task or deliverable is left out with its item.
        var taskIds = rows.Where(r => r.ItemType == ItemType.Task).Select(r => r.ItemId).ToList();
        var delIds = rows.Where(r => r.ItemType == ItemType.Deliverable).Select(r => r.ItemId).ToList();
        var items = (await db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).Select(t => new ItemLabel(t.Id, t.Key, t.Name)).ToListAsync())
            .Concat(await db.Deliverables.AsNoTracking().Where(d => delIds.Contains(d.Id)).Select(d => new ItemLabel(d.Id, d.Key, d.Name)).ToListAsync())
            .ToDictionary(x => x.Id);
        var shown = rows.Take(Max).Where(r => r.ItemType == ItemType.Project || items.ContainsKey(r.ItemId)).ToList();
        var result = new List<FileProjectRow>();
        foreach (var p in list)
        {
            var own = shown.Where(r => r.ProjectId == p.Id).ToList();
            var canAdd = Permissions.EditProject(access.Actor, await access.Context(p)).Ok; // project links belong to the PM (§12.7)
            if (own.Count == 0 && !canAdd) continue;
            result.Add(new FileProjectRow(p.Id, p.ProjectNumber, p.Name, canAdd, own.Count,
                [.. own.GroupBy(r => (r.ItemType, r.ItemId)).OrderBy(g => g.Key.ItemType == ItemType.Project ? 0 : 1).ThenBy(g => items.GetValueOrDefault(g.Key.ItemId)?.Key).Select(g => new FileItemRow(
                    g.Key.ItemType, g.Key.ItemId, g.Key.ItemType == ItemType.Project ? p.ProjectNumber : items[g.Key.ItemId].Key,
                    g.Key.ItemType == ItemType.Project ? p.Name : items[g.Key.ItemId].Name,
                    [.. g.Select(r => new FileLinkRow(r.Id, r.Title, r.Url, r.LinkType, Links.IsUnc(r.Url), r.AddedBy, r.AddedAt))]))]));
        }
        return TypedResults.Ok(new FilesRead(result, shown.Count, rows.Count > Max, LinkType.All));
    }

    // ---------- Team (§36.1, FR-VIS-01) ----------

    /// Everyone with an active membership in the scope's projects, with their roles and led disciplines per project.
    static async Task<Ok<TeamRead>> Team(string? projects, Access access, HubDb db)
    {
        var scope = Scope.Projects(access, db, projects);
        var pids = scope.Select(p => p.Id);
        var names = await scope.Select(p => new Named(p.Id, p.ProjectNumber, p.Name)).ToDictionaryAsync(p => p.Id);
        var members = await ActiveMembers(db, pids).Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new MemberJoin(u.Id, u.DisplayName, u.JobTitle, u.Email, m.ProjectId, m.Roles)).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.LeadUserId != null && d.IsActive)
            .Select(d => new LeadRow(d.ProjectId, d.LeadUserId!.Value, d.Discipline!.Name)).ToListAsync();
        var people = members.GroupBy(m => m.Id).Select(g => new TeamPersonRow(g.Key, g.First().DisplayName, g.First().JobTitle, g.First().Email,
            [.. g.OrderBy(m => names[m.ProjectId].ProjectNumber).Select(m => new TeamProjectRow(m.ProjectId, names[m.ProjectId].ProjectNumber, names[m.ProjectId].Name, m.Roles,
                leads.Where(l => l.ProjectId == m.ProjectId && l.Lead == g.Key).Select(l => l.Name).OrderBy(n => n).ToList()))])).OrderBy(p => p.DisplayName).ToList();
        return TypedResults.Ok(new TeamRead(people, names.Count));
    }
}
