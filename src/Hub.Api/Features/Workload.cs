using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Resource and Workload View (§12.15, §13.11, FR-RES-01, Workflow 10): estimated remaining hours per person per week for
/// eight weeks, from projects the viewer may see, with the unestimated count always beside the hours.
public static class WorkloadEndpoints
{
    public sealed record WorkloadQuery(Guid? SupervisorId, Guid? DisciplineId, Guid? OfficeId, Guid? ProjectId, string? Indicator, DateOnly? From, string? Sort);
    public sealed record CapacityBody(decimal? Hours);

    public const int Weeks = 8;
    static readonly string[] LiveProjects = [ProjectStatus.Setup, ProjectStatus.Active];

    sealed record TaskRow(Guid Id, Guid ProjectId, string Key, string Name, Guid AssigneeId, Guid ProjectDisciplineId, decimal? EstimatedHours, int ProgressPct,
        DateOnly? StartDate, DateOnly? DueDate, string Status, int RowVersion);

    public sealed record PersonLoad(Guid Id, string DisplayName, Guid? SupervisorId, string? SupervisorName, decimal Capacity, bool CapacityOverride,
        IReadOnlyList<(DateOnly Week, decimal Hours, decimal Pct, decimal Available, decimal Confirmed, decimal Proposed, decimal Committed)> Cells,
        decimal NoDueDate, int OpenTasks, int Unestimated, int Overdue, int Projects,
        bool OverAssigned, bool UnderAssigned, bool Cluster, bool CanSetCapacity, bool PartialScope);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/workload", async ([AsParameters] WorkloadQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (weeks, people, today) = await Grid(f, access, db, me, store, clock);
            return new
            {
                Weeks = weeks, Today = today, CurrentWeek = Workload.WeekOf(today), DefaultCapacity = (await store.Get(db)).DefaultWeeklyCapacityHours,
                People = people.Select(p => new
                {
                    p.Id, p.DisplayName, p.SupervisorId, p.SupervisorName, p.Capacity, p.CapacityOverride,
                    Cells = p.Cells.Select(c => new { c.Week, Hours = Math.Round(c.Hours, 1), c.Pct,
                        Available = Math.Round(c.Available, 1), Confirmed = Math.Round(c.Confirmed, 1),
                        Proposed = Math.Round(c.Proposed, 1), Committed = Math.Round(c.Committed, 1) }), NoDueDate = Math.Round(p.NoDueDate, 1),
                    p.OpenTasks, p.Unestimated, p.Overdue, p.Projects, p.OverAssigned, p.UnderAssigned, p.Cluster, p.CanSetCapacity, p.PartialScope,
                    Indicator = p.OverAssigned ? "over" : p.UnderAssigned ? "under" : "ok",
                }),
            };
        });
        api.MapGet("/workload/export", async (string? format, [AsParameters] WorkloadQuery f, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (weeks, people, _) = await Grid(f, access, db, me, store, clock);
            var (cols, rows) = EmployeeRows(weeks, people);
            var parameters = http.Request.Query.Where(k => k.Key != "format" && !string.IsNullOrWhiteSpace(k.Value)).Select(k => (Text.Get($"param.{k.Key}"), k.Value.ToString())).ToList();
            return await ExportFile.Send(db, store, format, Text.Get("nav.workload"), cols, rows, parameters, null, "workload", clock);
        });
        api.MapGet("/workload/{userId:guid}/tasks", PersonTasks);
        api.MapPut("/users/{id:guid}/capacity", async (Guid id, CapacityBody body, Access access, HubDb db) =>
        {
            var u = await db.Users.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.ActOnStaff(access.Actor, u.SupervisorId)); // Supervisors for direct reports, Admins for anyone (FR-006)
            Check.That(body.Hours is null or (>= 0 and <= 80), "hours", "error.positive");
            u.WeeklyCapacityHours = body.Hours;
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id, u.WeeklyCapacityHours });
        });
    }

    /// Who appears (§13.11): everyone for Executives and Admins, direct reports for Supervisors (Q19), and members of the
    /// projects a PM manages. Hours come only from Setup and Active projects the viewer can see (Workflow 10, §36.1).
    static IQueryable<AppUser> People(HubDb db, Access access, CurrentUser me)
    {
        var a = access.Actor;
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        if (a.Admin || a.Executive) return users;
        var managed = db.Projects.Where(p => LiveProjects.Contains(p.Status) && (p.ProjectManagerId == me.Id
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == me.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))).Select(p => p.Id);
        var members = db.ProjectMembers.Where(m => m.RemovedAt == null && managed.Contains(m.ProjectId)).Select(m => m.UserId);
        return users.Where(u => (a.Supervisor && u.SupervisorId == me.Id) || (a.SystemPM && members.Contains(u.Id)));
    }

    static async Task<List<TaskRow>> Tasks(HubDb db, Access access, IQueryable<Guid> people, Guid? disciplineId, Guid? projectId)
    {
        var projects = access.VisibleProjects().Where(p => LiveProjects.Contains(p.Status)).Select(p => p.Id);
        var q = db.Tasks.AsNoTracking().Where(t => t.AssigneeId != null && people.Contains(t.AssigneeId.Value) && projects.Contains(t.ProjectId)
            && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold);
        if (projectId is { } pid) q = q.Where(t => t.ProjectId == pid);
        if (disciplineId is { } d) q = q.Where(t => db.ProjectDisciplines.Any(pd => pd.Id == t.ProjectDisciplineId && pd.DisciplineId == d));
        return await q.Select(t => new TaskRow(t.Id, t.ProjectId, t.Key, t.Name, t.AssigneeId!.Value, t.ProjectDisciplineId, t.EstimatedHours, t.ProgressPct,
            t.StartDate, t.DueDate, t.Status, t.RowVersion)).ToListAsync();
    }

    public static async Task<(List<DateOnly> Weeks, List<PersonLoad> People, DateOnly Today)> Grid(WorkloadQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var actor = access.Actor;
        Access.Demand(Permissions.ViewWorkload(actor));
        var s = await store.Get(db);
        var today = clock.Today(s);
        var first = Workload.WeekOf(f.From ?? today);
        var weeks = Enumerable.Range(0, Weeks).Select(i => first.AddDays(7 * i)).ToList();
        var people = People(db, access, me);
        if (f.SupervisorId is { } sup) people = people.Where(u => u.SupervisorId == sup);
        if (f.OfficeId is { } office) people = people.Where(u => u.OfficeId == office);
        var list = await people.Select(u => new { u.Id, u.DisplayName, u.SupervisorId, u.OfficeId, u.WeeklyCapacityHours,
            Supervisor = db.Users.Where(x => x.Id == u.SupervisorId).Select(x => x.DisplayName).FirstOrDefault() }).ToListAsync();
        // A discipline selects people with work in that discipline. Capacity and reservations
        // belong to a person, not a discipline, so keep their visible project totals together.
        var tasks = await Tasks(db, access, people.Select(u => u.Id), null, f.ProjectId);
        var disciplineTaskIds = f.DisciplineId is { } disciplineId
            ? (await db.ProjectDisciplines.AsNoTracking().Where(pd => pd.DisciplineId == disciplineId).Select(pd => pd.Id).ToListAsync()).ToHashSet()
            : null;
        var cal = await Calendars.For(db, s, [.. tasks.Select(t => t.ProjectId).Distinct()]);
        var byPerson = tasks.ToLookup(t => t.AssigneeId);
        var personIds = list.Select(u => u.Id).ToArray();
        var firstDay = weeks[0]; var lastDay = weeks[^1].AddDays(6);
        var visibleProjects = access.VisibleProjects().Where(p => LiveProjects.Contains(p.Status)).Select(p => p.Id);
        var allocationRows = await db.Allocations.AsNoTracking().Where(a => personIds.Contains(a.PersonId)
            && visibleProjects.Contains(a.ProjectId) && (a.Status == AllocationStatus.Confirmed || a.Status == AllocationStatus.Proposed)
            && a.FromDate <= lastDay && a.ThroughDate >= firstDay).ToListAsync();
        var allocationIds = allocationRows.Select(a => a.Id).ToArray();
        var dayOverrides = await db.AllocationDayOverrides.AsNoTracking().Where(d => allocationIds.Contains(d.AllocationId)).ToListAsync();
        var workLinks = await db.AllocationWorkLinks.AsNoTracking().Where(l => allocationIds.Contains(l.AllocationId)
            && l.ReleasedAt == null && l.WorkDate >= firstDay && l.WorkDate <= lastDay).ToListAsync();
        var availability = await db.AvailabilityOverrides.AsNoTracking().Where(o => personIds.Contains(o.PersonId)
            && o.WorkDate >= firstDay && o.WorkDate <= lastDay).ToListAsync();
        var holidays = s.WorkingDaysEnabled ? await db.Holidays.AsNoTracking().Select(h => new { h.OfficeId, h.Date }).ToListAsync() : [];
        var now = Workload.WeekOf(today);
        var result = new List<PersonLoad>();
        foreach (var u in list)
        {
            var mine = byPerson[u.Id].ToList();
            var personAllocations = allocationRows.Where(a => a.PersonId == u.Id && (f.ProjectId == null || a.ProjectId == f.ProjectId)).ToArray();
            if (f.ProjectId is not null && mine.Count == 0 && personAllocations.Length == 0) continue;
            if (disciplineTaskIds is not null && !mine.Any(t => disciplineTaskIds.Contains(t.ProjectDisciplineId))) continue;
            var capacity = u.WeeklyCapacityHours ?? s.DefaultWeeklyCapacityHours;
            var loads = mine.Select(t => Workload.Spread(new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate), today, cal(t.ProjectId))).ToList();
            decimal Hours(DateOnly w) => loads.Sum(l => l.ByWeek.GetValueOrDefault(w));
            var dailyTasks = mine.ToDictionary(t => t.Id, t => Workload.SpreadDays(
                new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate), today, cal(t.ProjectId)).ByDay);
            var personCalendar = s.WorkingDaysEnabled
                ? new WorkCalendar(holidays.Where(h => h.OfficeId == null || h.OfficeId == u.OfficeId).Select(h => h.Date)) : WorkCalendar.Weekdays;
            var personAvailability = availability.Where(o => o.PersonId == u.Id).ToDictionary(o => o.WorkDate, o => o.AvailableHours);
            var allocationsByWeek = new Dictionary<Guid, (Dictionary<DateOnly, decimal> Reserved, Dictionary<DateOnly, decimal> Linked, Dictionary<DateOnly, decimal> LinkedTasks)>();
            foreach (var a in personAllocations)
            {
                var explicitDays = dayOverrides.Where(d => d.AllocationId == a.Id).ToDictionary(d => d.WorkDate, d => d.Hours);
                IReadOnlyDictionary<DateOnly, decimal> spread;
                try { spread = AllocationRules.Spread(a.FromDate, a.ThroughDate, a.PlannedHours, personCalendar, explicitDays); }
                catch (ArgumentException) { throw ApiException.Conflict("allocation_calendar_changed", "coord.stale"); }
                var reserved = new Dictionary<DateOnly, decimal>(); var linked = new Dictionary<DateOnly, decimal>(); var linkedTasks = new Dictionary<DateOnly, decimal>();
                foreach (var (day, hours) in spread)
                    if (day >= firstDay && day <= lastDay) reserved[Workload.WeekOf(day)] = reserved.GetValueOrDefault(Workload.WeekOf(day)) + hours;
                foreach (var link in workLinks.Where(l => l.AllocationId == a.Id))
                {
                    var week = Workload.WeekOf(link.WorkDate);
                    var demand = link.WorkType == "Task" ? dailyTasks.GetValueOrDefault(link.WorkId)?.GetValueOrDefault(link.WorkDate) ?? 0 : link.ReviewHours ?? 0;
                    linked[week] = linked.GetValueOrDefault(week) + demand;
                    if (link.WorkType == "Task") linkedTasks[week] = linkedTasks.GetValueOrDefault(week) + demand;
                }
                allocationsByWeek[a.Id] = (reserved, linked, linkedTasks);
            }
            var cells = weeks.Select(w =>
            {
                var weekReservations = personAllocations.Where(a => a.Status == AllocationStatus.Confirmed)
                    .Select(a => new AllocationDemand(allocationsByWeek[a.Id].Reserved.GetValueOrDefault(w), allocationsByWeek[a.Id].Linked.GetValueOrDefault(w))).ToArray();
                var linkedTaskHours = personAllocations.Where(a => a.Status == AllocationStatus.Confirmed)
                    .Sum(a => allocationsByWeek[a.Id].LinkedTasks.GetValueOrDefault(w));
                var committed = AllocationRules.Committed(weekReservations, Math.Max(0, Hours(w) - linkedTaskHours));
                var proposed = personAllocations.Where(a => a.Status == AllocationStatus.Proposed)
                    .Sum(a => Math.Max(allocationsByWeek[a.Id].Reserved.GetValueOrDefault(w), allocationsByWeek[a.Id].Linked.GetValueOrDefault(w)));
                var available = Enumerable.Range(0, 7).Sum(i =>
                {
                    var day = w.AddDays(i);
                    return AllocationRules.DailyCapacity(day, capacity, personCalendar,
                        personAvailability.TryGetValue(day, out var overrideHours) ? overrideHours : (decimal?)null);
                });
                return (Week: w, Hours: Hours(w), Pct: Workload.Pct(committed, available), Available: available,
                    Confirmed: weekReservations.Sum(r => r.ReservedHours), Proposed: proposed, Committed: committed);
            }).ToList();
            var thisWeek = cells.FirstOrDefault(c => c.Week == now).Pct;
            var nextWeek = cells.FirstOrDefault(c => c.Week == now.AddDays(7)).Pct;
            var unestimated = mine.Count(t => t.EstimatedHours is null);
            result.Add(new PersonLoad(u.Id, u.DisplayName, u.SupervisorId, u.Supervisor, capacity, u.WeeklyCapacityHours is not null,
                cells, loads.Sum(l => l.NoDueDate), mine.Count, unestimated,
                loads.Count(l => l.Overdue), mine.Select(t => t.ProjectId).Distinct().Count(), Workload.OverAssigned(thisWeek, nextWeek),
                access.SeesAllRestricted && f.ProjectId is null && Workload.UnderAssigned(thisWeek, nextWeek, unestimated),
                Workload.DeadlineCluster(mine.Where(t => t.DueDate is not null).Select(t => (t.DueDate!.Value, t.ProjectId)), today),
                Permissions.ActOnStaff(actor, u.SupervisorId).Ok, !access.SeesAllRestricted || f.ProjectId is not null));
        }
        result = f.Indicator switch
        {
            "over" => result.Where(p => p.OverAssigned).ToList(),
            "under" => result.Where(p => p.UnderAssigned).ToList(),
            "cluster" => result.Where(p => p.Cluster).ToList(),
            "unestimated" => result.Where(p => p.Unestimated > 0).ToList(),
            _ => result,
        };
        var thisWeekIndex = weeks.IndexOf(now);
        decimal Load(PersonLoad p) => thisWeekIndex >= 0 ? p.Cells[thisWeekIndex].Pct : p.Cells[0].Pct;
        result = f.Sort switch
        {
            "name" => [.. result.OrderBy(p => p.DisplayName)],
            "overdue" => [.. result.OrderByDescending(p => p.Overdue).ThenByDescending(Load)],
            "tasks" => [.. result.OrderByDescending(p => p.OpenTasks).ThenBy(p => p.DisplayName)],
            _ => [.. result.OrderByDescending(Load).ThenBy(p => p.DisplayName)],
        };
        return (weeks, result, today);
    }

    /// Person → Project → tasks with each task's hours per week (§13.11 expand row), and whether the viewer may reassign it.
    static async Task<object> PersonTasks(Guid userId, DateOnly? from, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        Access.Demand(Permissions.ViewWorkload(access.Actor));
        var person = await People(db, access, me).Where(u => u.Id == userId).Select(u => new { u.Id, u.SupervisorId }).FirstOrDefaultAsync() ?? throw ApiException.NotFound();
        var s = await store.Get(db);
        var today = clock.Today(s);
        var first = Workload.WeekOf(from ?? today);
        var weeks = Enumerable.Range(0, Weeks).Select(i => first.AddDays(7 * i)).ToList();
        var tasks = await Tasks(db, access, db.Users.Where(u => u.Id == userId).Select(u => u.Id), null, null);
        var projectIds = tasks.Select(t => t.ProjectId).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToListAsync();
        var cal = await Calendars.For(db, s, projectIds);
        var groups = new List<object>();
        foreach (var p in projects.OrderBy(p => p.ProjectNumber))
        {
            var ctx = await access.Context(p);
            var rows = new List<object>();
            foreach (var t in tasks.Where(t => t.ProjectId == p.Id).OrderBy(t => t.DueDate ?? DateOnly.MaxValue))
            {
                var load = Workload.Spread(new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate), today, cal(t.ProjectId));
                var facts = new TaskFacts(t.ProjectDisciplineId, t.AssigneeId, null, null, new HashSet<Guid>(), t.Status, AssigneeSupervisorId: person.SupervisorId);
                rows.Add(new
                {
                    t.Id, t.Key, t.Name, t.Status, t.DueDate, t.EstimatedHours, t.ProgressPct, Remaining = load.Remaining, load.Overdue, NoDueDate = load.NoDueDate, t.RowVersion,
                    Weeks = weeks.Select(w => Math.Round(load.ByWeek.GetValueOrDefault(w), 1)), CanReassign = Permissions.AssignTask(access.Actor, ctx, facts).Ok,
                });
            }
            groups.Add(new { p.Id, p.ProjectNumber, p.Name, Tasks = rows });
        }
        return new { Weeks = weeks, Projects = groups };
    }

    /// Workload by Discipline (§19): the same hours summed by discipline across the people in the viewer's scope.
    public static async Task<(Col[] Cols, JsonArray Rows)> DisciplineRows(Guid? officeId, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        Access.Demand(Permissions.ViewWorkload(access.Actor));
        var s = await store.Get(db);
        var today = clock.Today(s);
        var weeks = Enumerable.Range(0, Weeks).Select(i => Workload.WeekOf(today).AddDays(7 * i)).ToList();
        var people = People(db, access, me);
        if (officeId is { } o) people = people.Where(u => u.OfficeId == o);
        var tasks = await Tasks(db, access, people.Select(u => u.Id), null, null);
        var cal = await Calendars.For(db, s, [.. tasks.Select(t => t.ProjectId).Distinct()]);
        var pdIds = tasks.Select(t => t.ProjectDisciplineId).Distinct().ToList();
        var names = await db.ProjectDisciplines.AsNoTracking().Where(pd => pdIds.Contains(pd.Id)).Select(pd => new { pd.Id, pd.Discipline!.Name }).ToDictionaryAsync(x => x.Id, x => x.Name);
        Col[] cols =
        [
            new("discipline", "discipline"), .. weeks.Select((w, i) => new Col($"w{i}", "week", "number", Text.Get("col.weekOf", w.ToString("yyyy-MM-dd")))),
            new("noDueDate", "noDueDate", "number"), new("openTasks", "openTasks", "number"), new("unestimated", "unestimated", "number"), new("people", "peopleCount", "number"),
        ];
        var rows = new JsonArray();
        foreach (var g in tasks.GroupBy(t => names.GetValueOrDefault(t.ProjectDisciplineId) ?? "").OrderBy(g => g.Key))
        {
            var loads = g.Select(t => Workload.Spread(new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate), today, cal(t.ProjectId))).ToList();
            var row = new JsonObject { ["discipline"] = g.Key, ["noDueDate"] = Math.Round(loads.Sum(l => l.NoDueDate), 1), ["openTasks"] = g.Count(),
                ["unestimated"] = g.Count(t => t.EstimatedHours is null), ["people"] = g.Select(t => t.AssigneeId).Distinct().Count() };
            for (var i = 0; i < weeks.Count; i++) row[$"w{i}"] = Math.Round(loads.Sum(l => l.ByWeek.GetValueOrDefault(weeks[i])), 1);
            rows.Add(row);
        }
        return (cols, rows);
    }

    /// Workload by Employee (§19): one row per person with a column per week.
    public static (Col[] Cols, JsonArray Rows) EmployeeRows(List<DateOnly> weeks, List<PersonLoad> people)
    {
        Col[] cols =
        [
            new("person", "person"), new("supervisor", "supervisor"), new("capacity", "capacity", "number"),
            new("partialScope", "partialScope"),
            .. weeks.SelectMany((w, i) => new[] {
                new Col($"w{i}", "week", "number", Text.Get("col.weekOf", w.ToString("yyyy-MM-dd"))),
                new Col($"w{i}Available", "available", "number", $"{Text.Get("col.weekOf", w.ToString("yyyy-MM-dd"))} · {Text.Get("col.available")}"),
                new Col($"w{i}Confirmed", "confirmed", "number", $"{Text.Get("col.weekOf", w.ToString("yyyy-MM-dd"))} · {Text.Get("col.confirmed")}"),
                new Col($"w{i}Proposed", "proposed", "number", $"{Text.Get("col.weekOf", w.ToString("yyyy-MM-dd"))} · {Text.Get("col.proposed")}"),
                new Col($"w{i}Committed", "committed", "number", $"{Text.Get("col.weekOf", w.ToString("yyyy-MM-dd"))} · {Text.Get("col.committed")}"),
            }),
            new("noDueDate", "noDueDate", "number"), new("openTasks", "openTasks", "number"), new("unestimated", "unestimated", "number"),
            new("overdue", "overdueTasks", "number"), new("projects", "projectCount", "number"), new("indicator", "indicator"),
        ];
        var rows = new JsonArray();
        foreach (var p in people)
        {
            var row = new JsonObject
            {
                ["person"] = p.DisplayName, ["supervisor"] = p.SupervisorName, ["capacity"] = p.Capacity,
                ["partialScope"] = p.PartialScope ? Text.Get("workload.partial") : Text.Get("workload.complete"),
                ["noDueDate"] = Math.Round(p.NoDueDate, 1), ["openTasks"] = p.OpenTasks,
                ["unestimated"] = p.Unestimated, ["overdue"] = p.Overdue, ["projects"] = p.Projects,
                ["indicator"] = Text.Get(p.OverAssigned ? "workload.over" : p.UnderAssigned ? "workload.under" : "workload.ok") + (p.Cluster ? $"; {Text.Get("workload.cluster")}" : ""),
            };
            for (var i = 0; i < p.Cells.Count; i++) {
                row[$"w{i}"] = Math.Round(p.Cells[i].Hours, 1);
                row[$"w{i}Available"] = Math.Round(p.Cells[i].Available, 1);
                row[$"w{i}Confirmed"] = Math.Round(p.Cells[i].Confirmed, 1);
                row[$"w{i}Proposed"] = Math.Round(p.Cells[i].Proposed, 1);
                row[$"w{i}Committed"] = Math.Round(p.Cells[i].Committed, 1);
            }
            rows.Add(row);
        }
        return (cols, rows);
    }
}
