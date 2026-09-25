using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
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
    public sealed record Body(string? Name, Guid[]? ProjectIds);
    public sealed record Row(Guid Id, string Name, List<Guid> ProjectIds, int Hidden);
    public sealed record Widget(string Id, bool Hidden);
    public sealed record LayoutBody(Widget[] Widgets);

    /// The overview's fixed widgets in their default order (FR-VIS-07); a person may reorder or hide them, not redefine them.
    public static readonly string[] Widgets = ["metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/workspaces", (Access access, HubDb db) => List(access, db));
        api.MapPost("/workspaces", async (Body body, Access access, HubDb db, TimeProvider clock) =>
        {
            var ws = new Workspace { OwnerId = access.Me.Id, CreatedAt = clock.GetUtcNow() };
            await Apply(ws, body, access, db, creating: true);
            db.Workspaces.Add(ws);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/workspaces/{ws.Id}", (await List(access, db)).First(w => w.Id == ws.Id));
        });
        api.MapPatch("/workspaces/{id:guid}", async (Guid id, Body body, Access access, HubDb db) =>
        {
            var ws = await Own(db, access, id);
            await Apply(ws, body, access, db, creating: false);
            await db.SaveChangesAsync();
            return (await List(access, db)).First(w => w.Id == id);
        });
        api.MapDelete("/workspaces/{id:guid}", async (Guid id, Access access, HubDb db) =>
        {
            var ws = await Own(db, access, id);
            await Tx.Run(db, async () =>
            {
                db.Workspaces.Remove(ws);
                await db.SaveChangesAsync();
                return await db.BoardOrders.Where(b => b.WorkspaceId == id).ExecuteDeleteAsync();
            });
            return Results.NoContent();
        });
        // §36.3 FR-VIS-04: a named workspace's board keeps its owner's own card order, separate from each project's.
        api.MapPut("/workspaces/{id:guid}/board-order", async (Guid id, ViewEndpoints.OrderBody body, Access access, HubDb db) =>
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
            return Results.NoContent();
        });

        api.MapGet("/home", Home);
        api.MapPut("/me/dashboard-layout", async (LayoutBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var widgets = Normalise(body.Widgets);
            var row = await db.DashboardLayouts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (row is null) db.DashboardLayouts.Add(row = new DashboardLayout { UserId = me.Id });
            row.Widgets = System.Text.Json.JsonSerializer.Serialize(widgets, JsonOpts.Web);
            row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return new { Layout = widgets, IsDefault = false };
        });
        api.MapDelete("/me/dashboard-layout", async (HubDb db, CurrentUser me) =>
        {
            await db.DashboardLayouts.Where(x => x.UserId == me.Id).ExecuteDeleteAsync();
            return new { Layout = Normalise([]), IsDefault = true };
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
                w.Id, w.Name, Total = w.Projects.Count,
                Visible = w.Projects.Where(p => visible.Contains(p.ProjectId)).OrderBy(p => p.SortOrder).Select(p => p.ProjectId).ToList(),
            }).ToListAsync();
        return rows.Select(w => new Row(w.Id, w.Name, w.Visible, w.Total - w.Visible.Count)).ToList();
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
            if (!kept.TryGetValue(ids[i], out var row)) ws.Projects.Add(row = new WorkspaceProject { ProjectId = ids[i] });
            row.SortOrder = i;
        }
    }

    // ---------- Overview dashboard (§36.6, FR-VIS-07, AC-VIS-05) ----------

    /// Snapshot tiles and charts are current values for the scope; only Upcoming Deadlines follows the date range. Every
    /// task figure is the count of the cross-project list with the same filter, so a number always equals the list it opens.
    static async Task<object> Home(string? projects, DateOnly? from, DateOnly? to, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var s = await store.Get(db);
        var today = clock.Today(s);
        var start = from ?? today;
        var end = to ?? today.AddDays(13);
        Check.That(end >= start && end.DayNumber - start.DayNumber <= 366, "to", "home.range");

        var scope = Scope.Projects(access, db, projects);
        var active = scope.Where(p => p.Status == ProjectStatus.Active);
        var activeCount = await active.CountAsync();
        var green = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id && x.ComputedHealth == Health.Green));
        var evaluated = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id
            && (x.ComputedHealth == Health.Green || x.ComputedHealth == Health.Yellow || x.ComputedHealth == Health.Red)));

        var tasks = TaskEndpoints.InScope(access, db, projects);
        Task<int> Count(string key, string value) => TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { [key] = value }), me.Id, today, s).CountAsync();
        var byStatus = new List<object>();
        foreach (var lane in Workflow.Lanes)
        {
            var statuses = string.Join(',', LaneStatuses(lane));
            byStatus.Add(new { Lane = lane, Statuses = statuses, Count = await Count("status", statuses) });
        }

        var pids = scope.Select(p => p.Id);
        var names = await scope.Select(p => new { p.Id, p.ProjectNumber, p.Name }).ToDictionaryAsync(p => p.Id);
        var counts = await tasks.Where(t => t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold)
            .GroupBy(t => new { t.ProjectId, t.Status }).Select(g => new { g.Key.ProjectId, g.Key.Status, N = g.Count() }).ToListAsync();
        var byProject = counts.GroupBy(c => c.ProjectId).Select(g => new
        {
            ProjectId = g.Key, names[g.Key].ProjectNumber, names[g.Key].Name,
            Lanes = Workflow.Lanes.Select(l => new { Lane = l, Statuses = string.Join(',', LaneStatuses(l)), Count = g.Where(c => Workflow.Lane(c.Status) == l).Sum(c => c.N) }).ToList(),
            Total = g.Sum(c => c.N),
        }).OrderByDescending(p => p.Total).ThenBy(p => p.ProjectNumber).ToList();

        var members = await ActiveMembers(db, pids).Select(m => m.UserId).Distinct().CountAsync();
        var layout = await db.DashboardLayouts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id);

        return new
        {
            Today = today, From = start, To = end, AsOf = clock.GetUtcNow(),
            Metrics = new
            {
                ActiveProjects = activeCount, OpenTasks = await Count("open", "true"), OverdueTasks = await Count("overdue", "true"),
                OnTrack = new { Green = green, Evaluated = evaluated, Pct = evaluated == 0 ? (int?)null : green * 100 / evaluated },
                TeamMembers = members,
            },
            TasksByStatus = byStatus,
            TasksByProject = byProject,
            UpcomingDeadlines = await Deadlines(db, pids, names.ToDictionary(n => n.Key, n => n.Value.ProjectNumber), start, end),
            Layout = layout is null ? Normalise([]) : Normalise(System.Text.Json.JsonSerializer.Deserialize<Widget[]>(layout.Widgets, JsonOpts.Web) ?? []),
            IsDefault = layout is null,
        };
    }

    static IEnumerable<string> LaneStatuses(string lane) => TaskStatuses.All.Where(x => Workflow.Lane(x) == lane);

    /// Open task, deliverable and milestone due dates in the range, soonest first (the calendar's deadline projection).
    static async Task<object> Deadlines(HubDb db, IQueryable<Guid> pids, Dictionary<Guid, string> numbers, DateOnly from, DateOnly to)
    {
        const int Max = 100;
        var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { Type = ItemType.Task, t.Id, t.ProjectId, t.Key, t.Name, Date = t.DueDate!.Value, Status = (string?)t.Status,
                Owner = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                Overdue = db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue) }).Take(Max + 1).ToListAsync();
        var dels = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to
                && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
            .Select(d => new { Type = ItemType.Deliverable, d.Id, d.ProjectId, d.Key, d.Name, Date = d.DueDate!.Value, Status = (string?)d.Status,
                Owner = db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                Overdue = db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue) }).Take(Max + 1).ToListAsync();
        var ms = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled && !m.IsComplete)
            .Select(m => new { Type = ItemType.Milestone, m.Id, m.ProjectId, m.Key, m.Name, Date = m.Date!.Value,
                Status = db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault(), Owner = (string?)null,
                Overdue = db.MilestoneStates.Any(x => x.MilestoneId == m.Id && x.Status == MilestoneStatus.Overdue) }).Take(Max + 1).ToListAsync();
        var all = tasks.Concat(dels).Concat(ms).OrderBy(x => x.Date).ThenBy(x => x.Key).ToList();
        return new
        {
            Total = all.Count, Truncated = all.Count > Max,
            Items = all.Take(Max).Select(x => new { x.Type, x.Id, x.ProjectId, ProjectNumber = numbers.GetValueOrDefault(x.ProjectId), x.Key, x.Name, x.Date, x.Status, x.Owner, x.Overdue }),
        };
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
    static async Task<object> Files(string? projects, string? q, string? type, Access access, HubDb db)
    {
        const int Max = 2000;
        var list = await Scope.Projects(access, db, projects).AsNoTracking().OrderBy(p => p.ProjectNumber).ToListAsync();
        var pids = list.Select(p => p.Id).ToList();
        var links = db.DocumentLinks.AsNoTracking().Where(l => pids.Contains(l.ProjectId));
        if (!string.IsNullOrWhiteSpace(type)) links = links.Where(l => l.LinkType == type);
        if (!string.IsNullOrWhiteSpace(q)) { var term = $"%{q.Trim()}%"; links = links.Where(l => EF.Functions.ILike(l.Title, term) || EF.Functions.ILike(l.Url, term)); }
        var rows = await links.OrderBy(l => l.Title).Take(Max + 1)
            .Select(l => new { l.Id, l.ProjectId, l.ItemType, l.ItemId, l.Title, l.Url, l.LinkType, l.AddedAt, AddedBy = db.Users.Where(u => u.Id == l.AddedBy).Select(u => u.DisplayName).FirstOrDefault() })
            .ToListAsync();
        // Item labels; a link on a deleted task or deliverable is left out with its item.
        var taskIds = rows.Where(r => r.ItemType == ItemType.Task).Select(r => r.ItemId).ToList();
        var delIds = rows.Where(r => r.ItemType == ItemType.Deliverable).Select(r => r.ItemId).ToList();
        var items = (await db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).Select(t => new { t.Id, t.Key, t.Name }).ToListAsync())
            .Concat(await db.Deliverables.AsNoTracking().Where(d => delIds.Contains(d.Id)).Select(d => new { d.Id, d.Key, d.Name }).ToListAsync())
            .ToDictionary(x => x.Id);
        var shown = rows.Take(Max).Where(r => r.ItemType == ItemType.Project || items.ContainsKey(r.ItemId)).ToList();
        var result = new List<object>();
        foreach (var p in list)
        {
            var own = shown.Where(r => r.ProjectId == p.Id).ToList();
            var canAdd = Permissions.EditProject(access.Actor, await access.Context(p)).Ok; // project links belong to the PM (§12.7)
            if (own.Count == 0 && !canAdd) continue;
            result.Add(new
            {
                ProjectId = p.Id, p.ProjectNumber, p.Name, CanAdd = canAdd, Count = own.Count,
                Items = own.GroupBy(r => (r.ItemType, r.ItemId)).OrderBy(g => g.Key.ItemType == ItemType.Project ? 0 : 1).ThenBy(g => items.GetValueOrDefault(g.Key.ItemId)?.Key).Select(g => new
                {
                    g.Key.ItemType, g.Key.ItemId, Key = g.Key.ItemType == ItemType.Project ? p.ProjectNumber : items[g.Key.ItemId].Key,
                    Name = g.Key.ItemType == ItemType.Project ? p.Name : items[g.Key.ItemId].Name,
                    Links = g.Select(r => new { r.Id, r.Title, r.Url, r.LinkType, IsNetworkPath = Links.IsUnc(r.Url), r.AddedBy, r.AddedAt }),
                }),
            });
        }
        return new { Projects = result, Total = shown.Count, Truncated = rows.Count > Max, Types = LinkType.All };
    }

    // ---------- Team (§36.1, FR-VIS-01) ----------

    /// Everyone with an active membership in the scope's projects, with their roles and led disciplines per project.
    static async Task<object> Team(string? projects, Access access, HubDb db)
    {
        var scope = Scope.Projects(access, db, projects);
        var pids = scope.Select(p => p.Id);
        var names = await scope.Select(p => new { p.Id, p.ProjectNumber, p.Name }).ToDictionaryAsync(p => p.Id);
        var members = await ActiveMembers(db, pids).Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { u.Id, u.DisplayName, u.JobTitle, u.Email, m.ProjectId, m.Roles }).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.LeadUserId != null && d.IsActive)
            .Select(d => new { d.ProjectId, Lead = d.LeadUserId!.Value, d.Discipline!.Name }).ToListAsync();
        var people = members.GroupBy(m => m.Id).Select(g => new
        {
            UserId = g.Key, g.First().DisplayName, g.First().JobTitle, g.First().Email,
            Projects = g.OrderBy(m => names[m.ProjectId].ProjectNumber).Select(m => new
            {
                m.ProjectId, names[m.ProjectId].ProjectNumber, ProjectName = names[m.ProjectId].Name, m.Roles,
                Leads = leads.Where(l => l.ProjectId == m.ProjectId && l.Lead == g.Key).Select(l => l.Name).OrderBy(n => n).ToList(),
            }).ToList(),
        }).OrderBy(p => p.DisplayName).ToList();
        return new { People = people, Projects = names.Count };
    }
}
