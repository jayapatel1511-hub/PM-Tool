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
    public sealed record HomeProjectCount(Guid ProjectId, string ProjectNumber, int Count);
    public sealed record HomeCountContext(
        int ProjectsInScope, IReadOnlyList<Guid> ProjectIds, IReadOnlyList<string> ProjectStatuses,
        HomeProjectCount? OpenTasksProject, HomeProjectCount? OverdueTasksProject,
        int DueWithinDays, DateOnly DueFrom, DateOnly DueTo, int DueWithinTasks, int DueWithinDeliverables, int DueWithinMilestones,
        DateOnly? BusiestDueDate, int BusiestDueCount, int OpenTasksWithoutDueDate,
        List<string> OnTrackCriticalProjects, int HomeDisciplineCount);
    public sealed record HomeOnHold(string Status, int Count);
    public sealed record HomeRunwayItem(string Type, Guid Id, Guid ProjectId, string ProjectNumber, string Key, string Name,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? IdentityColour = null);
    public sealed record HomeRunwayDay(DateOnly Date, bool NonWorking, int Total, List<HomeRunwayItem> Items);
    public sealed record HomeRunway(DateOnly From, DateOnly To, bool TruncatedRange, List<HomeRunwayDay> Days);
    public sealed record HomeCapacityTotals(decimal? GrossHours, decimal? AvailableHours, decimal? PlannedHours, decimal? TimeAwayHours,
        decimal? PossibleHours, decimal? FreeHours, int? Pct, int People, int PartialPeople, int ProvisionalPeople, int MissingWeeks, int Above100People);
    public sealed record HomeCapacityDiscipline(Guid? DisciplineId, string? DisciplineName, HomeCapacityTotals Totals, string PlannerPath);
    public sealed record HomeCapacity(DateOnly Week, DateOnly Through, DateTimeOffset AsOf, HomeCapacityTotals Totals,
        string PlannerPath, string Above100Path, List<HomeCapacityDiscipline> Disciplines);
    public sealed record HomePresetBody(string? Code);
    public sealed record HomePresetContext(string? Code, string? SavedPreset, string[] Available, int Projects, int Disciplines, int People, bool UnownedMilestonesExcluded);
    public sealed record HomeProject(Guid ProjectId, string ProjectNumber, string Name, List<HomeLane> Lanes, int Total,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? IdentityColour = null);
    public sealed record HomeDeadlineNote(string Kind, int Days);
    public sealed record HomeDeadline(string Type, Guid Id, Guid ProjectId, string? ProjectNumber, string Key, string Name, DateOnly Date, string? Status, string? Owner, bool Overdue,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeDeadlineNote? Note = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? IdentityColour = null);
    public sealed record HomeDeadlines(int Total, bool Truncated, List<HomeDeadline> Items,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Page = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? PageSize = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? TotalPages = null);
    /// The overview: current totals for the scope, the lanes, each project's lanes, the deadlines in the range and the person's layout.
    public sealed record HomeRead(DateOnly Today, DateOnly From, DateOnly To, DateTimeOffset AsOf, HomeMetrics Metrics, List<HomeLane> TasksByStatus, HomeOnHold OnHold,
        List<HomeProject> TasksByProject, HomeDeadlines UpcomingDeadlines, List<Widget> Layout, bool IsDefault,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeCountContext? CountContext = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeRunway? Runway = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeCapacity? WeeklyCapacity = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomeVisualTrends.HomeTrendsRead? Trends = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] HomePresetContext? Preset = null);

    public sealed record WorkspaceFileUserReference(Guid Id, string DisplayName, bool IsActive);
    public sealed record FileLinkRow(Guid Id, string Title, string Url, string LinkType, bool IsNetworkPath, string? AddedBy, DateTimeOffset AddedAt,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] WorkspaceFileUserReference? AddedByUser = null);
    public sealed record FileItemRow(string ItemType, Guid ItemId, string Key, string Name, List<FileLinkRow> Links);
    public sealed record FileProjectRow(Guid ProjectId, string ProjectNumber, string Name, bool CanAdd, int Count, List<FileItemRow> Items,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? IdentityColour = null);
    public sealed record FilesRead(List<FileProjectRow> Projects, int Total, bool Truncated, IReadOnlyList<string> Types,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] FileScreenRead? Screen = null);

    public sealed record TeamProjectRow(Guid ProjectId, string ProjectNumber, string ProjectName, List<string> Roles, List<string> Leads,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? IdentityColour = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] string? ResponsibilityNote = null);
    public sealed record TeamPersonRow(Guid UserId, string DisplayName, string? JobTitle, string Email, List<TeamProjectRow> Projects,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PersonFacts.Facts? PersonFacts = null,
        [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] TeamScreenReads.Organisation? OrganisationFacts = null);
    public sealed record TeamRead(List<TeamPersonRow> People, int Projects);

    /// What the reads below take from the database before the names are put beside them.
    sealed record Named(Guid Id, string ProjectNumber, string Name, string? IdentityColour);
    sealed record StatusCount(Guid ProjectId, string Status, int N);
    sealed record Due(string Type, Guid Id, Guid ProjectId, string Key, string Name, DateOnly Date, string? Status, string? Owner, bool Overdue);
    sealed record LinkJoin(Guid Id, Guid ProjectId, string ItemType, Guid ItemId, string Title, string Url, string LinkType, DateTimeOffset AddedAt, string? AddedBy,
        WorkspaceFileUserReference? AddedByUser);
    sealed record ItemLabel(Guid Id, string Key, string Name);
    sealed record MemberJoin(Guid Id, string DisplayName, string? JobTitle, string Email, Guid ProjectId, List<string> Roles, string? ResponsibilityNote);
    sealed record LeadRow(Guid ProjectId, Guid Lead, string Name);

    /// The overview's fixed widgets in their default order (FR-VIS-07); a person may reorder or hide them, not redefine them.
    public static readonly string[] Widgets = ["metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines"];
    static readonly string[] OverviewWidgets = ["metrics", "tasksByProject", "tasksByStatus", "weeklyCapacity", "upcomingDeadlines"];

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
        api.MapPut("/me/home-preset", async Task<NoContent> (HomePresetBody body, HubDb db, CurrentUser me, Access access, SettingsStore store) =>
        {
            var settings = await store.Get(db); Http.RequireFlag(settings, "home_overview");
            var resolved = await HomePresets.Resolve(access, db, settings, body.Code);
            var preference = await UserPreferences.GetOrCreate(db, me.Id);
            preference.HomePreset = resolved?.Code;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });
        api.MapPut("/me/dashboard-layout", async Task<Ok<LayoutAnswer>> (LayoutBody body, HubDb db, CurrentUser me, Access access, SettingsStore store, TimeProvider clock) =>
        {
            var settings = await store.Get(db);
            var overview = settings.IsFlagOn("home_overview");
            var capacity = overview && await CanReadHomeCapacity(access, db, settings);
            var widgets = Normalise(body.Widgets, overview, capacity);
            var row = await db.DashboardLayouts.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (row is null) db.DashboardLayouts.Add(row = new DashboardLayout { UserId = me.Id });
            var stored = widgets.ToList();
            // A rollback or temporary loss of capability must not erase the new widget's saved visibility/order.
            if (!capacity)
            {
                var previous = System.Text.Json.JsonSerializer.Deserialize<Widget[]>(row.Widgets, JsonOpts.Web) ?? [];
                var index = Array.FindIndex(previous, w => w.Id == "weeklyCapacity");
                if (index >= 0) stored.Insert(Math.Min(index, stored.Count), previous[index]);
            }
            row.Widgets = System.Text.Json.JsonSerializer.Serialize(stored, JsonOpts.Web);
            row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return TypedResults.Ok(new LayoutAnswer(widgets, false));
        });
        api.MapDelete("/me/dashboard-layout", async Task<Ok<LayoutAnswer>> (HubDb db, CurrentUser me, Access access, SettingsStore store) =>
        {
            await db.DashboardLayouts.Where(x => x.UserId == me.Id).ExecuteDeleteAsync();
            var settings = await store.Get(db);
            var overview = settings.IsFlagOn("home_overview");
            return TypedResults.Ok(new LayoutAnswer(Normalise([], overview, overview && await CanReadHomeCapacity(access, db, settings)), true));
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
    static async Task<Ok<HomeRead>> Home(string? projects, DateOnly? from, DateOnly? to, string? projectStatus, int? deadlinePage, int? deadlinePageSize, string? homePreset, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var s = await store.Get(db);
        var overview = s.IsFlagOn("home_overview");
        await using var snapshot = overview ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var canCapacity = overview && await CanReadHomeCapacity(access, db, s);
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(Clock.Local(now, s).DateTime);
        var start = from ?? today;
        var end = to ?? today.AddDays(13);
        Check.That(end >= start && end.DayNumber - start.DayNumber <= 366, "to", "home.range_rule");

        var scope = Scope.Projects(access, db, projects);
        var preset = overview ? await HomePresets.Resolve(access, db, s, homePreset) : null;
        if (preset != null) scope = preset.ApplyProjects(scope);
        var selectedStatuses = s.IsFlagOn("home_overview") ? Http.List(projectStatus) : [];
        Check.That(selectedStatuses.All(ProjectStatus.All.Contains), "projectStatus", "error.invalid");
        if (selectedStatuses.Length > 0) scope = scope.Where(p => selectedStatuses.Contains(p.Status));
        var active = scope.Where(p => p.Status == ProjectStatus.Active);
        var activeCount = await active.CountAsync();
        var green = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id && x.ComputedHealth == Health.Green));
        var evaluated = await active.CountAsync(p => db.ProjectStates.Any(x => x.ProjectId == p.Id
            && (x.ComputedHealth == Health.Green || x.ComputedHealth == Health.Yellow || x.ComputedHealth == Health.Red)));

        var tasks = TaskEndpoints.InScope(access, db, projects).Where(t => scope.Any(p => p.Id == t.ProjectId));
        if (preset != null) tasks = preset.ApplyTasks(tasks);
        Task<int> Count(string key, string value) => TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { [key] = value }), me.Id, today, s).CountAsync();
        var byStatus = new List<HomeLane>();
        foreach (var lane in Workflow.Lanes)
        {
            var statuses = string.Join(',', LaneStatuses(lane));
            byStatus.Add(new HomeLane(lane, statuses, await Count("status", statuses)));
        }

        var pids = scope.Select(p => p.Id);
        var deliverables = db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId));
        var milestones = db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId));
        if (preset != null) { deliverables = preset.ApplyDeliverables(deliverables); milestones = preset.ApplyMilestones(milestones); }
        var names = await scope.Select(p => new Named(p.Id, p.ProjectNumber, p.Name, p.IdentityColour)).ToDictionaryAsync(p => p.Id);
        var counts = await tasks.Where(t => t.Status != TaskStatuses.Cancelled && (overview || t.Status != TaskStatuses.OnHold))
            .GroupBy(t => new { t.ProjectId, t.Status }).Select(g => new StatusCount(g.Key.ProjectId, g.Key.Status, g.Count())).ToListAsync();
        var projectLanes = overview ? Workflow.Lanes.Append(TaskStatuses.OnHold) : Workflow.Lanes;
        var grouped = counts.GroupBy(c => c.ProjectId).ToDictionary(g => g.Key, g => g.ToList());
        var chartProjects = overview ? names.Keys.AsEnumerable() : grouped.Keys;
        var byProject = chartProjects.Select(pid => new HomeProject(pid, names[pid].ProjectNumber, names[pid].Name,
            projectLanes.Select(l => new HomeLane(l, string.Join(',', LaneStatuses(l)), grouped.GetValueOrDefault(pid)?.Where(c => Workflow.Lane(c.Status) == l).Sum(c => c.N) ?? 0)).ToList(),
            grouped.GetValueOrDefault(pid)?.Sum(c => c.N) ?? 0, s.IsFlagOn("visual_system") ? names[pid].IdentityColour : null)).OrderByDescending(p => p.Total).ThenBy(p => p.ProjectNumber).ToList();

        var members = await ActiveMembers(db, pids).Select(m => m.UserId).Distinct().CountAsync();
        var layout = await db.DashboardLayouts.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id);

        var openTasks = await Count("open", "true");
        var overdueTasks = await Count("overdue", "true");
        HomeCountContext? countContext = null;
        if (s.IsFlagOn("home_overview"))
        {
            var window = s.HomeDueWindowDays;
            var taskDue = await tasks.CountAsync(t => t.DueDate >= today && t.DueDate <= today.AddDays(window) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled);
            var delDue = await deliverables.CountAsync(x => x.DueDate >= today && x.DueDate <= today.AddDays(window) && x.Status != DeliverableStatus.Cancelled && x.Status != DeliverableStatus.Issued && x.Status != DeliverableStatus.Accepted);
            var milestoneDue = await milestones.CountAsync(x => x.Date >= today && x.Date <= today.AddDays(window) && !x.IsCancelled && !x.IsComplete);
            var dueDates = await tasks.Where(t => t.DueDate >= today && t.DueDate <= today.AddDays(window) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled).Select(t => t.DueDate!.Value)
                .Concat(deliverables.Where(x => x.DueDate >= today && x.DueDate <= today.AddDays(window) && x.Status != DeliverableStatus.Cancelled && x.Status != DeliverableStatus.Issued && x.Status != DeliverableStatus.Accepted).Select(x => x.DueDate!.Value))
                .Concat(milestones.Where(x => x.Date >= today && x.Date <= today.AddDays(window) && !x.IsCancelled && !x.IsComplete).Select(x => x.Date!.Value)).ToListAsync();
            var busiest = dueDates.GroupBy(x => x).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).FirstOrDefault();
            var critical = await active.Where(p => db.ProjectStates.Any(x => x.ProjectId == p.Id && x.ComputedHealth == Health.Red))
                .OrderBy(p => p.ProjectNumber).Select(p => p.ProjectNumber).ToListAsync();
            var disciplineIds = await db.ProjectMembers.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.RemovedAt == null)
                .Join(db.Users.Where(u => u.IsActive), m => m.UserId, u => u.Id, (_, u) => u.HomeDisciplineId).ToListAsync();
            var openWithoutDue = await tasks.Where(t => t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.DueDate == null).CountAsync();
            countContext = new HomeCountContext(names.Count, names.Keys.Order().ToArray(), selectedStatuses,
                await TopProject(db, tasks, "open", "true", me.Id, today, s, names),
                await TopProject(db, tasks, "overdue", "true", me.Id, today, s, names), window, today, today.AddDays(window), taskDue, delDue, milestoneDue,
                busiest?.Key, busiest?.Count() ?? 0, openWithoutDue, critical, disciplineIds.Distinct().Count());
        }

        var capacity = canCapacity ? await WeeklyCapacity(access, db, store, clock, today) : null;
        HomePresetContext? presetContext = null;
        HomeVisualTrends.HomeTrendsRead? trends = null;
        if (overview)
        {
            var available = await HomePresets.Available(access, db, s);
            var saved = await db.UserSettings.AsNoTracking().Where(x => x.UserId == me.Id).Select(x => x.HomePreset).FirstOrDefaultAsync();
            var disciplines = preset?.Code == HomePresets.DisciplineLead ? await db.ProjectDisciplines.CountAsync(d => pids.Contains(d.ProjectId) && preset.DisciplineIds.Contains(d.Id)) : 0;
            presetContext = new(preset?.Code, saved != null && available.Contains(saved) ? saved : null, available, names.Count, disciplines, preset?.PersonIds.Length ?? 0,
                preset?.Code is HomePresets.DisciplineLead or HomePresets.Supervisor);
            trends = await HomeVisualTrends.Build(db, scope, s, today, now, preset?.Code, selectedStatuses);
        }
        HomeDeadlines? notedDeadlines = null;
        var identityColours = s.IsFlagOn("visual_system") ? names.ToDictionary(n => n.Key, n => n.Value.IdentityColour) : null;
        if (overview)
            notedDeadlines = await HomeVisualDeadlineNotes.Apply(db,
                await Deadlines(db, pids, names.ToDictionary(n => n.Key, n => n.Value.ProjectNumber), start, end, true, deadlinePage, deadlinePageSize, preset, identityColours), s, today, now);
        return TypedResults.Ok(new HomeRead(today, start, end, now,
            new HomeMetrics(activeCount, openTasks, overdueTasks,
                new HomeOnTrack(green, evaluated, evaluated == 0 ? null : green * 100 / evaluated), members),
            byStatus,
            new HomeOnHold(TaskStatuses.OnHold, await Count("status", TaskStatuses.OnHold)), // AC-CNT-05: the lanes plus this equal Open Tasks
            byProject,
            notedDeadlines ?? await Deadlines(db, pids, names.ToDictionary(n => n.Key, n => n.Value.ProjectNumber), start, end,
                s.IsFlagOn("home_overview"), deadlinePage, deadlinePageSize, identityColours: identityColours),
            layout is null ? Normalise([], overview, canCapacity) : Normalise(System.Text.Json.JsonSerializer.Deserialize<Widget[]>(layout.Widgets, JsonOpts.Web) ?? [], overview, canCapacity),
            layout is null, countContext, overview ? await Runway(db, pids, names, start, end, s, preset) : null, capacity, trends, presetContext));
    }

    static async Task<bool> CanReadHomeCapacity(Access access, HubDb db, OrgSettings settings)
        => Permissions.ViewWorkload(access.Actor).Ok || await PeopleScope.ReadsAsUnitLeader(db, settings, access.Actor);

    /// FR-SCR-11: the planner's own people scope and one current ISO week. Home project filters never enter this read.
    static async Task<HomeCapacity> WeeklyCapacity(Access access, HubDb db, SettingsStore store, TimeProvider clock, DateOnly today)
    {
        var week = Workload.WeekOf(today);
        var grid = await PlanningEndpoints.ReportGrid(new(From: week, Weeks: 1, IncludeMyDrafts: true, Sort: "name"), access, db, store, clock);
        var disciplineIds = grid.People.Select(p => p.HomeDisciplineId).OfType<Guid>().Distinct().ToArray();
        var disciplines = await db.Disciplines.AsNoTracking().Where(d => disciplineIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name);
        var path = $"/planner?from={week:yyyy-MM-dd}&weeks=1&includeMyDrafts=true&sort=name";
        return new HomeCapacity(week, week.AddDays(6), grid.EvaluatedAt, CapacityTotals(grid.People, week), path, $"{path}&indicator=Above100",
            [.. grid.People.GroupBy(p => p.HomeDisciplineId).OrderBy(g => g.Key == null ? 1 : 0)
                .ThenBy(g => g.Key is { } id ? disciplines.GetValueOrDefault(id) : null, StringComparer.OrdinalIgnoreCase).ThenBy(g => g.Key)
                .Select(g => new HomeCapacityDiscipline(g.Key, g.Key is { } id ? disciplines.GetValueOrDefault(id) : null,
                    CapacityTotals(g, week), $"{path}&homeDisciplineId={g.Key ?? Guid.Empty}"))]);
    }

    /// Aggregate only already-authorized canonical cell figures, before rounding. Missing/unverified capacity stays unknown.
    public static HomeCapacityTotals CapacityTotals(IEnumerable<PlanningEndpoints.PersonRow> source, DateOnly week)
    {
        var people = source.ToArray();
        var cells = people.Select(p => p.Cells.FirstOrDefault(c => c.Week == week)).ToArray();
        var missing = cells.Count(c => c == null);
        var provisional = cells.Count(c => c?.Provisional == true);
        var partial = people.Count(p => p.Partial);
        var known = cells.OfType<PlanningEndpoints.CellRow>().ToArray();
        decimal? planned = missing == 0 ? known.Sum(c => c.Confirmed + c.Expected) : null;
        decimal? away = missing == 0 ? known.Sum(c => c.TimeAway) : null;
        decimal? available = missing == 0 && provisional == 0 ? known.Sum(c => c.Capacity) : null;
        var pct = available > 0 && planned is { } p ? (int?)Math.Round(100m * p / available.Value, 0, MidpointRounding.AwayFromZero) : null;
        return new HomeCapacityTotals(available + away, available, planned, away, missing == 0 ? known.Sum(c => c.Possible) : null,
            available != null && partial == 0 ? known.Sum(c => c.Remaining) : null, pct, people.Length, partial, provisional, missing,
            known.Count(c => !c.Provisional && c.Confirmed + c.Expected > c.Capacity));
    }

    /// A bounded, exact daily projection, independent of the deadline list's 100-row display cap.
    /// Only four item locators are returned per day; overflow opens that day's paged deadline list.
    static async Task<HomeRunway> Runway(HubDb db, IQueryable<Guid> pids, Dictionary<Guid, Named> names, DateOnly from, DateOnly to, OrgSettings settings, HomePresetScope? preset = null)
    {
        var end = from.AddDays(Math.Min(13, to.DayNumber - from.DayNumber));
        var taskSource = preset?.ApplyTasks(db.Tasks.AsNoTracking()) ?? db.Tasks.AsNoTracking();
        var deliverableSource = preset?.ApplyDeliverables(db.Deliverables.AsNoTracking()) ?? db.Deliverables.AsNoTracking();
        var milestoneSource = preset?.ApplyMilestones(db.Milestones.AsNoTracking()) ?? db.Milestones.AsNoTracking();
        var tasks = taskSource.Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= end && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { Type = ItemType.Task, t.Id, t.ProjectId, t.Key, t.Name, Date = t.DueDate!.Value });
        var deliverables = deliverableSource.Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= end && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
            .Select(d => new { Type = ItemType.Deliverable, d.Id, d.ProjectId, d.Key, d.Name, Date = d.DueDate!.Value });
        var milestones = milestoneSource.Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= end && !m.IsCancelled && !m.IsComplete)
            .Select(m => new { Type = ItemType.Milestone, m.Id, m.ProjectId, m.Key, m.Name, Date = m.Date!.Value });
        var source = tasks.Concat(deliverables).Concat(milestones);
        var totals = await source.GroupBy(x => x.Date).Select(g => new { Date = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Date, x => x.Count);
        var calendars = await Calendars.For(db, settings, names.Keys.ToArray());
        var days = new List<HomeRunwayDay>();
        for (var day = from; day <= end; day = day.AddDays(1))
        {
            var items = new List<HomeRunwayItem>();
            if (totals.ContainsKey(day))
            {
                var rows = await source.Where(x => x.Date == day).OrderBy(x => x.Key).ThenBy(x => x.Type).ThenBy(x => x.Id).Take(4).ToListAsync();
                items.AddRange(rows.Select(x => new HomeRunwayItem(x.Type, x.Id, x.ProjectId, names[x.ProjectId].ProjectNumber, x.Key, x.Name,
                    settings.IsFlagOn("visual_system") ? names[x.ProjectId].IdentityColour : null)));
            }
            var nonWorking = names.Count == 0 ? !WorkCalendar.Weekdays.IsWorkingDay(day) : names.Keys.All(pid => !calendars(pid).IsWorkingDay(day));
            days.Add(new HomeRunwayDay(day, nonWorking, totals.GetValueOrDefault(day), items));
            if (day == end) break; // DateOnly.MaxValue is a valid one-day range.
        }
        return new HomeRunway(from, end, end < to, days);
    }

    static async Task<HomeProjectCount?> TopProject(HubDb db, IQueryable<WorkTask> tasks, string key, string value, Guid me, DateOnly today, OrgSettings s, Dictionary<Guid, Named> names)
    {
        var q = TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { [key] = value }), me, today, s);
        var groups = await q.GroupBy(t => t.ProjectId).Select(g => new { ProjectId = g.Key, Count = g.Count() }).ToListAsync();
        return groups.Where(g => names.ContainsKey(g.ProjectId)).OrderByDescending(g => g.Count)
            .ThenBy(g => names[g.ProjectId].ProjectNumber, StringComparer.Ordinal)
            .Select(g => new HomeProjectCount(g.ProjectId, names[g.ProjectId].ProjectNumber, g.Count)).FirstOrDefault();
    }

    static IEnumerable<string> LaneStatuses(string lane) => TaskStatuses.All.Where(x => Workflow.Lane(x) == lane);

    /// Open task, deliverable and milestone due dates in the range, soonest first (the calendar's deadline projection).
    /// Each type is ordered before it is cut, so the soonest items are never dropped (AC-CNT-04).
    static async Task<HomeDeadlines> Deadlines(HubDb db, IQueryable<Guid> pids, Dictionary<Guid, string> numbers, DateOnly from, DateOnly to, bool exactTotal = false, int? requestedPage = null, int? requestedPageSize = null, HomePresetScope? preset = null, IReadOnlyDictionary<Guid, string?>? identityColours = null)
    {
        const int Max = 100;
        var taskSource = preset?.ApplyTasks(db.Tasks.AsNoTracking()) ?? db.Tasks.AsNoTracking();
        var deliverableSource = preset?.ApplyDeliverables(db.Deliverables.AsNoTracking()) ?? db.Deliverables.AsNoTracking();
        var milestoneSource = preset?.ApplyMilestones(db.Milestones.AsNoTracking()) ?? db.Milestones.AsNoTracking();
        var paged = exactTotal && requestedPage is not null;
        var page = paged ? Math.Max(1, requestedPage.GetValueOrDefault()) : 1;
        var pageSize = paged ? Math.Clamp(requestedPageSize ?? Max, 1, Max) : Max;
        if (paged)
        {
            var taskRows = taskSource.Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
                .Select(t => new { Type = ItemType.Task, t.Id, t.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault()!, t.Key, t.Name, Date = t.DueDate!.Value, Status = (string?)t.Status, Owner = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(), Overdue = db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue) });
            var deliverableRows = deliverableSource.Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
                .Select(d => new { Type = ItemType.Deliverable, d.Id, d.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault()!, d.Key, d.Name, Date = d.DueDate!.Value, Status = (string?)d.Status, Owner = db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), Overdue = db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue) });
            var milestoneRows = milestoneSource.Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled && !m.IsComplete)
                .Select(m => new { Type = ItemType.Milestone, m.Id, m.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault()!, m.Key, m.Name, Date = m.Date!.Value, Status = db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault(), Owner = (string?)null, Overdue = db.MilestoneStates.Any(x => x.MilestoneId == m.Id && x.Status == MilestoneStatus.Overdue) });
            var allRows = taskRows.Concat(deliverableRows).Concat(milestoneRows);
            var total = await allRows.CountAsync();
            var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)pageSize);
            var effectivePage = totalPages == 0 ? 1 : Math.Min(page, totalPages);
            var rows = await allRows.OrderBy(x => x.Date).ThenBy(x => x.ProjectNumber).ThenBy(x => x.Key).ThenBy(x => x.Type).ThenBy(x => x.Id)
                .Skip((effectivePage - 1) * pageSize).Take(pageSize).ToListAsync();
            return new HomeDeadlines(total, total > Max,
                [.. rows.Select(x => new HomeDeadline(x.Type, x.Id, x.ProjectId, x.ProjectNumber, x.Key, x.Name, x.Date, x.Status, x.Owner, x.Overdue, IdentityColour: identityColours?.GetValueOrDefault(x.ProjectId)))], effectivePage, pageSize, totalPages);
        }

        var take = Max + 1;
        var tasks = await taskSource.Where(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .OrderBy(t => t.DueDate).ThenBy(t => t.Key).Take(take)
            .Select(t => new Due(ItemType.Task, t.Id, t.ProjectId, t.Key, t.Name, t.DueDate!.Value, t.Status,
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue))).ToListAsync();
        var dels = await deliverableSource.Where(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to
                && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
            .OrderBy(d => d.DueDate).ThenBy(d => d.Key).Take(take)
            .Select(d => new Due(ItemType.Deliverable, d.Id, d.ProjectId, d.Key, d.Name, d.DueDate!.Value, d.Status,
                db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue))).ToListAsync();
        var ms = await milestoneSource.Where(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled && !m.IsComplete)
            .OrderBy(m => m.Date).ThenBy(m => m.Key).Take(take)
            .Select(m => new Due(ItemType.Milestone, m.Id, m.ProjectId, m.Key, m.Name, m.Date!.Value,
                db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault(), null,
                db.MilestoneStates.Any(x => x.MilestoneId == m.Id && x.Status == MilestoneStatus.Overdue))).ToListAsync();
        var all = tasks.Concat(dels).Concat(ms).OrderBy(x => x.Date).ThenBy(x => x.Key).ToList();
        var totalLegacy = exactTotal
            ? await taskSource.CountAsync(t => pids.Contains(t.ProjectId) && t.DueDate >= from && t.DueDate <= to && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
                + await deliverableSource.CountAsync(d => pids.Contains(d.ProjectId) && d.DueDate >= from && d.DueDate <= to && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted)
                + await milestoneSource.CountAsync(m => pids.Contains(m.ProjectId) && m.Date >= from && m.Date <= to && !m.IsCancelled && !m.IsComplete)
            : all.Count;
        return new HomeDeadlines(totalLegacy, totalLegacy > Max,
            [.. all.Take(Max).Select(x => new HomeDeadline(x.Type, x.Id, x.ProjectId, numbers.GetValueOrDefault(x.ProjectId), x.Key, x.Name, x.Date, x.Status, x.Owner, x.Overdue, IdentityColour: identityColours?.GetValueOrDefault(x.ProjectId)))]);
    }

    /// Known widgets in the person's order, unknown ones dropped, missing ones appended visible (so new widgets appear).
    public static List<Widget> Normalise(IEnumerable<Widget> given, bool overview = false, bool capacity = true)
    {
        var order = overview ? OverviewWidgets.Where(id => capacity || id != "weeklyCapacity").ToArray() : Widgets;
        var list = given.Where(w => order.Contains(w.Id)).DistinctBy(w => w.Id).ToList();
        list.AddRange(order.Where(id => list.All(w => w.Id != id)).Select(id => new Widget(id, false)));
        return list;
    }

    /// Active memberships of active people: the Team list and the Team Members figure (FR-VIS-07, FR-VIS-01).
    static IQueryable<ProjectMember> ActiveMembers(HubDb db, IQueryable<Guid> pids) =>
        db.ProjectMembers.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && m.RemovedAt == null && db.Users.Any(u => u.Id == m.UserId && u.IsActive));

    // ---------- Files (§36.8, FR-VIS-09) ----------

    /// Document links of the scope's projects grouped by project and linked item; a link library only, nothing is stored.
    static async Task<Ok<FilesRead>> Files(string? projects, string? q, string? type, string? disciplineId, string? addedById, string? addedThisWeek,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        const int Max = 2000;
        var settings = await store.Get(db);
        // The old branch deliberately does not parse previously unknown query controls.
        if (settings.IsFlagOn(DocumentLinkReferences.Flag))
            return TypedResults.Ok(await FileScreenReads.Read(projects, q, type, disciplineId, addedById, addedThisWeek, access, db, settings, clock));
        var list = await Scope.Projects(access, db, projects).AsNoTracking().OrderBy(p => p.ProjectNumber).ToListAsync();
        var pids = list.Select(p => p.Id).ToList();
        var links = db.DocumentLinks.AsNoTracking().Where(l => pids.Contains(l.ProjectId));
        if (!string.IsNullOrWhiteSpace(type)) links = links.Where(l => l.LinkType == type);
        if (!string.IsNullOrWhiteSpace(q)) { var term = $"%{q.Trim()}%"; links = links.Where(l => EF.Functions.ILike(l.Title, term) || EF.Functions.ILike(l.Url, term)); }
        var people = settings.IsFlagOn("people_screens");
        // Apply the existing limit before the unique User join. A missing User stays null;
        // neither the linked item's ID nor an author's caption can supply a replacement ID.
        var bounded = links.OrderBy(l => l.Title).Take(Max + 1);
        var rows = await (from link in bounded
            join user in db.Users.AsNoTracking() on link.AddedBy equals user.Id into authors
            from author in authors.DefaultIfEmpty()
            orderby link.Title
            select new LinkJoin(link.Id, link.ProjectId, link.ItemType, link.ItemId, link.Title, link.Url, link.LinkType, link.AddedAt,
                author != null ? author.DisplayName : null,
                people && author != null ? new WorkspaceFileUserReference(author.Id, author.DisplayName, author.IsActive) : null)).ToListAsync();
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
                    [.. g.Select(r => new FileLinkRow(r.Id, r.Title, r.Url, r.LinkType, Links.IsUnc(r.Url), r.AddedBy, r.AddedAt, r.AddedByUser))]))], settings.IsFlagOn("visual_system") ? p.IdentityColour : null));
        }
        return TypedResults.Ok(new FilesRead(result, shown.Count, rows.Count > Max, LinkType.All));
    }

    // ---------- Team (§36.1, FR-VIS-01) ----------

    /// Everyone with an active membership in the scope's projects, with their roles and led disciplines per project.
    static async Task<Ok<TeamRead>> Team(string? projects, string? homePreset, string? homeProjectStatus, string? certificationTagId, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("home_overview") || settings.IsFlagOn("capacity_screens") || settings.IsFlagOn("people_screens") ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var scope = Scope.Projects(access, db, projects);
        var preset = await HomePresets.Resolve(access, db, settings, homePreset);
        if (preset != null) scope = preset.ApplyProjects(scope);
        var selectedStatuses = settings.IsFlagOn("home_overview") ? Http.List(homeProjectStatus) : [];
        Check.That(selectedStatuses.All(ProjectStatus.All.Contains), "homeProjectStatus", "error.invalid");
        if (selectedStatuses.Length > 0) scope = scope.Where(p => selectedStatuses.Contains(p.Status));
        var pids = scope.Select(p => p.Id);
        var names = await scope.Select(p => new Named(p.Id, p.ProjectNumber, p.Name, p.IdentityColour)).ToDictionaryAsync(p => p.Id);
        var memberQuery = ActiveMembers(db, pids);
        if (settings.IsFlagOn("capacity_screens") && !string.IsNullOrEmpty(certificationTagId))
        {
            Check.That(Guid.TryParse(certificationTagId, out var tag) && tag != Guid.Empty, "certificationTagId", "error.invalid");
            Check.That(await db.CertificationTags.AnyAsync(t => t.Id == tag), "certificationTagId", "error.not_found");
            memberQuery = memberQuery.Where(member => db.UserCertifications.Any(c => c.UserId == member.UserId && c.CertificationTagId == tag));
        }
        var members = await memberQuery.Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new MemberJoin(u.Id, u.DisplayName, u.JobTitle, u.Email, m.ProjectId, m.Roles, m.ResponsibilityNote)).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.LeadUserId != null && d.IsActive)
            .Select(d => new LeadRow(d.ProjectId, d.LeadUserId!.Value, d.Discipline!.Name)).ToListAsync();
        var people = members.GroupBy(m => m.Id).Select(g => new TeamPersonRow(g.Key, g.First().DisplayName, g.First().JobTitle, g.First().Email,
            [.. g.OrderBy(m => names[m.ProjectId].ProjectNumber).Select(m => new TeamProjectRow(m.ProjectId, names[m.ProjectId].ProjectNumber, names[m.ProjectId].Name, m.Roles,
                leads.Where(l => l.ProjectId == m.ProjectId && l.Lead == g.Key).Select(l => l.Name).OrderBy(n => n).ToList(), settings.IsFlagOn("visual_system") ? names[m.ProjectId].IdentityColour : null,
                settings.IsFlagOn("people_screens") ? m.ResponsibilityNote : null))])).OrderBy(p => p.DisplayName).ToList();
        var personFacts = await PersonFacts.Read(db, people.Select(person => person.UserId).ToArray(), settings, clock);
        if (personFacts.Count > 0) people = people.Select(person => person with { PersonFacts = personFacts.GetValueOrDefault(person.UserId) }).ToList();
        var organisationFacts = await TeamScreenReads.ReadOrganisation(db, people.Select(person => person.UserId).ToArray(), settings);
        if (organisationFacts.Count > 0) people = people.Select(person => person with { OrganisationFacts = organisationFacts.GetValueOrDefault(person.UserId) }).ToList();
        return TypedResults.Ok(new TeamRead(people, names.Count));
    }
}
