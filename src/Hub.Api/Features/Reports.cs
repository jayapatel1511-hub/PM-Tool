using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// A column of a report or list export: a JSON path into the row, a header text key (`col.*`) and a type that decides
/// how the SPA shows it and how XLSX stores it (dates stay dates, §19).
public sealed record Col(string Path, string Header, string Type = "text", string? Label = null)
{
    public string Title => Label ?? Text.Get($"col.{Header}");
}

/// Standard reports (§19, FR-RPT-01, FR-ASG-08): fixed, rule-based queries with a few parameters, permission-filtered,
/// shown as tables and exported to CSV or XLSX. Each report reuses the list query it corresponds to, so its rows equal
/// the list "Open as filtered list" opens (SC-004).
public static class ReportEndpoints
{
    public sealed record Param(string Key, string Type, string[]? Options = null, string? Default = null);
    sealed record Def(string Code, Param[] Params, Col[] Cols, Func<Ctx, Task<Result>> Run, Func<Actor, bool>? Allowed = null);
    sealed record Result(JsonArray Rows, string? ListLink = null, Col[]? Columns = null);

    static readonly string[] Live = [ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Complete];
    const int ScreenRows = 2000;

    static Param Projects => new("projectId", "projects");
    static Param Discipline => new("disciplineId", "discipline");

    static readonly Def[] All =
    [
        new("tasks-due-this-week", [Projects, Discipline, new("assigneeId", "person"), new("week", "date"), new("mine", "bool")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("deliverableKey", "deliverable"), new("disciplineName", "discipline"),
             new("assigneeName", "assignee"), new("dueDate", "due", "date"), new("status", "status"), new("indicators", "indicators")], DueThisWeek),
        new("overdue-tasks", [Projects, Discipline, new("assigneeId", "person"), new("minDaysOverdue", "number", Default: "1")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("dueDate", "due", "date"),
             new("state.daysOverdue", "daysOverdue", "number"), new("state.blockingCount", "blockingCount", "number"), new("affectedMilestones", "affectedMilestones"), new("status", "status")], Overdue),
        new("blocked-tasks", [Projects, Discipline, new("blockerType", "select", ["task", "decision", "manual"])],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("state.blockedSince", "blockedSince", "date"),
             new("state.daysBlocked", "daysBlocked", "number"), new("blockers", "blockers"), new("affectedMilestones", "affectedMilestones")], Blocked),
        new("tasks-blocking-others", [Projects],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("dueDate", "due", "date"),
             new("status", "status"), new("state.blockingCount", "successors", "number"), new("successorMilestones", "successorMilestones")], Blocking),
        new("upcoming-deliverables", [Projects, Discipline, new("daysAhead", "number", Default: "14")],
            [new("key", "key", "key"), new("name", "deliverable"), new("projectNumber", "project"), new("disciplineName", "discipline"), new("ownerName", "owner"),
             new("milestoneKey", "milestone"), new("dueDate", "due", "date"), new("status", "status"), new("state.progressPct", "progress", "percent"), new("indicators", "indicators")], UpcomingDeliverables),
        new("deliverable-status-by-project", [Projects, Discipline],
            [new("projectNumber", "project"), new("key", "key", "key"), new("name", "deliverable"), new("disciplineName", "discipline"), new("status", "status"),
             new("dueDate", "due", "date"), new("issuedDate", "issued", "date"), new("revision", "revision")], DeliverableStatus_),
        new("upcoming-milestones", [Projects, new("daysAhead", "number", Default: "30"), new("type", "select", MilestoneType.All)],
            [new("projectNumber", "project"), new("key", "key", "key"), new("name", "milestone"), new("milestoneType", "type"), new("date", "date", "date"), new("status", "status"),
             new("slipDays", "slip", "number"), new("deliverablesIssued", "deliverablesIssued"), new("taskOpen", "openTasks", "number")], UpcomingMilestones),
        new("open-decisions", [Projects, new("ownerType", "select", ["internal", "external", "client"]), new("overdueOnly", "bool")],
            [new("key", "key", "key"), new("subject", "subject"), new("projectNumber", "project"), new("ownerName", "owner"), new("dateRequested", "requested", "date"),
             new("requiredByDate", "requiredBy", "date"), new("days", "days", "number"), new("impactLevel", "impact"), new("blocking", "blockingCount", "number"), new("status", "status")], OpenDecisions),
        new("open-issues-high-risks", [Projects, new("severity", "select", Impact.All)],
            [new("itemTypeLabel", "type"), new("key", "key", "key"), new("title", "title"), new("projectNumber", "project"), new("ownerName", "owner"),
             new("severity", "severity"), new("status", "status"), new("date", "date", "date"), new("daysOverdue", "daysOverdue", "number")], OpenIssuesHighRisks),
        new("meeting-actions-outstanding", [Projects, new("ownerType", "select", [ActionOwnerType.User, ActionOwnerType.Discipline, ActionOwnerType.ExternalParty])],
            [new("key", "key", "key"), new("text", "actionText"), new("projectNumber", "project"), new("meetingTitle", "meeting"), new("ownerName", "owner"),
             new("ownerTypeLabel", "ownerType"), new("dueDate", "due", "date"), new("daysOverdue", "daysOverdue", "number"), new("status", "status")], MeetingActions),
        new("review-queue", [new("reviewerId", "person"), Projects],
            [new("key", "key", "key"), new("name", "item"), new("itemTypeLabel", "type"), new("projectNumber", "project"), new("assigneeName", "assignee"),
             new("reviewerName", "reviewer"), new("readySince", "readySince", "date"), new("daysWaiting", "daysWaiting", "number"), new("round", "round", "number")], ReviewQueue),
        new("stale-work", [Projects, new("days", "number")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("status", "status"),
             new("lastActivityAt", "lastActivity", "datetime"), new("staleDays", "days", "number")], Stale),
        new("project-activity-log", [new("projectId", "project"), new("from", "date"), new("to", "date"), new("category", "select", ["status", "assignment", "date", "deletion", "creation", "comment", "decision", "health", "dependency", "structure"])],
            [new("occurredAt", "timestamp", "datetime"), new("actorName", "actor"), new("action", "action"), new("item", "item"), new("summary", "change"), new("reason", "reason")], ActivityLog_),
        new("staff-assignments", [new("scope", "select", ["direct", "all"], "direct"), Discipline, new("officeId", "office"), new("projectId", "projects"), new("role", "select", ProjectRole.All)],
            [new("person", "person"), new("projectNumber", "project"), new("projectName", "projectName"), new("projectStatus", "projectStatus"), new("roles", "roles"),
             new("primaryDiscipline", "primaryDiscipline"), new("addedAt", "addedOn", "date"), new("addedBy", "addedBy"), new("openTasks", "openTasks", "number"), new("overdueTasks", "overdueTasks", "number")],
            StaffAssignments, a => Permissions.ViewStaff(a).Ok),
        new("projects-at-risk", [new("officeId", "office"), new("pmId", "person"), new("health", "select", [Health.Red, Health.Yellow])],
            [new("projectNumber", "project"), new("name", "name"), new("pm.displayName", "pm"), new("office", "office"), new("computedHealth", "computedHealth"),
             new("reportedHealth", "reportedHealth"), new("why", "why"), new("healthOverrideNote", "overrideNote"), new("nextSubmission.name", "nextSubmission"),
             new("nextSubmission.date", "nextSubmissionDate", "date")], ProjectsAtRisk, a => Permissions.ViewPortfolio(a).Ok),
        new("health-history", [Projects, new("from", "date"), new("to", "date")],
            [new("projectNumber", "project"), new("name", "name"), new("date", "date", "date"), new("computed", "computedHealth"), new("reported", "reportedHealth")],
            HealthHistory, a => Permissions.ViewPortfolio(a).Ok),
        new("workload-employee", [new("supervisorId", "person"), Discipline, new("officeId", "office"), new("projectId", "project")],
            [new("person", "person"), new("capacity", "capacity", "number")], WorkloadEmployee, a => Permissions.ViewWorkload(a).Ok),
        new("workload-discipline", [new("officeId", "office")], [new("discipline", "discipline")], WorkloadDiscipline, a => Permissions.ViewWorkload(a).Ok),
        new("pilot-measures", [Projects, new("weeks", "number", Default: "8")],
            [new("weekOf", "pilotWeek", "date"), new("projects", "projectCount", "number"), new("reviewed", "coordinationReviews", "number"),
             new("unassigned", "unassignedOpen", "number"), new("noDueDate", "noDueDateOpen", "number"), new("blocked", "blockedTasks", "number"),
             new("people", "peopleWithWork", "number"), new("updaters", "statusUpdaters", "number"), new("updatersPct", "statusUpdatersPct", "percent")],
            PilotMeasures, a => Permissions.ViewPortfolio(a).Ok),
        new("task-hours", [new("projectId", "project"), new("userId", "person"), new("from", "date"), new("to", "date"), new("scope", "select", ["mine", "team"], "mine")],
            [new("workDate", "date", "date"), new("person", "person"), new("projectNumber", "project"), new("taskKey", "key"), new("taskName", "task"),
             new("hours", "hours", "number"), new("note", "note")], TaskHours),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reports", (CurrentUser me) => All.Where(r => r.Allowed?.Invoke(me.Actor) ?? true).Select(r => new
        {
            r.Code, Title = Text.Get($"report.{r.Code}"), Description = Text.Get($"report.{r.Code}.about"),
            Params = r.Params.Select(p => new { p.Key, Label = Text.Get($"param.{p.Key}"), p.Type, p.Default,
                Options = p.Options?.Where(o => !(p.Key == "scope" && o == "all" && !Permissions.ViewAllStaff(me.Actor))).Select(o => new { Value = o, Label = Text.Get($"opt.{o}") }) }),
            ItemType = ItemTypeOf(r.Code),
            Columns = r.Cols.Select(c => new { c.Path, Header = c.Title, c.Type }),
        }));
        api.MapGet("/reports/{code}", async (string code, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (def, ctx) = await Prepare(code, http, db, access, me, store, clock);
            var r = await def.Run(ctx);
            return new
            {
                def.Code, Title = Text.Get($"report.{def.Code}"), ItemType = ItemTypeOf(def.Code), Columns = (r.Columns ?? def.Cols).Select(c => new { c.Path, Header = c.Title, c.Type }),
                Rows = new JsonArray([.. r.Rows.Take(ScreenRows).Select(x => x?.DeepClone())]), Total = r.Rows.Count, Truncated = r.Rows.Count > ScreenRows, r.ListLink,
                Parameters = (await ctx.Describe(def.Params)).Select(p => new { Label = p.Item1, Value = p.Item2 }),
            };
        });
        api.MapGet("/reports/{code}/export", async (string code, string? format, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (def, ctx) = await Prepare(code, http, db, access, me, store, clock);
            var r = await def.Run(ctx);
            var title = Text.Get($"report.{def.Code}");
            var parameters = await ctx.Describe(def.Params);
            return await ExportFile.Send(db, store, format, title, r.Columns ?? def.Cols, r.Rows, parameters, await ctx.SingleProjectId(), def.Code, clock);
        });
    }

    /// The kind of item each row is, so a key cell opens its panel (review-queue rows carry their own).
    static string? ItemTypeOf(string code) => code switch
    {
        "upcoming-deliverables" or "deliverable-status-by-project" => ItemType.Deliverable,
        "upcoming-milestones" => ItemType.Milestone,
        "open-decisions" => ItemType.Decision,
        "meeting-actions-outstanding" => ItemType.Action,
        "project-activity-log" or "staff-assignments" or "review-queue" or "open-issues-high-risks" or "projects-at-risk" or "health-history" or "workload-employee" or "workload-discipline" or "task-hours" => null,
        _ => ItemType.Task,
    };

    static async Task<(Def, Ctx)> Prepare(string code, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var def = All.FirstOrDefault(r => r.Code == code) ?? throw ApiException.NotFound();
        if (def.Allowed is { } ok && !ok(me.Actor)) throw ApiException.Forbidden("perm.staff");
        var s = await store.Get(db);
        return (def, new Ctx(http.Request.Query, db, access, me, s, clock.Today(s), clock.GetUtcNow(), store, clock));
    }

    // ---------- Parameters and scope ----------

    sealed class Ctx(IQueryCollection q, HubDb db, Access access, CurrentUser me, OrgSettings s, DateOnly today, DateTimeOffset now, SettingsStore store, TimeProvider clock)
    {
        public HubDb Db => db; public Access Access => access; public CurrentUser Me => me; public OrgSettings S => s; public DateOnly Today => today; public DateTimeOffset Now => now;
        public SettingsStore Store => store; public TimeProvider Clock => clock;
        public string? Str(string k) => string.IsNullOrWhiteSpace(q[k]) ? null : q[k].ToString();
        public Guid? Id(string k) => Guid.TryParse(q[k], out var g) ? g : null;
        public Guid[] Ids(string k) => Http.Ids(q[k]);
        public int? Int(string k) => int.TryParse(q[k], out var i) ? i : null;
        public DateOnly? Date(string k) => DateOnly.TryParse(q[k], CultureInfo.InvariantCulture, out var d) ? d : null;
        public bool Bool(string k) => q[k] == "true";

        /// Projects in scope: the chosen ones the caller may view, or every visible project that is not Archived or Cancelled.
        public IQueryable<Project> Projects()
        {
            var visible = access.VisibleProjects();
            var chosen = Ids("projectId");
            return chosen.Length > 0 ? visible.Where(p => chosen.Contains(p.Id)) : visible.Where(p => Live.Contains(p.Status));
        }

        public IQueryable<Guid> ProjectIds() => Projects().Select(p => p.Id);

        /// The one project a report is scoped to, when exactly one was chosen, for "Open as filtered list".
        public async Task<(Guid Id, string Number)?> Single()
        {
            var chosen = Ids("projectId");
            if (chosen.Length != 1) return null;
            var p = await Projects().Select(p => new { p.Id, p.ProjectNumber }).FirstOrDefaultAsync();
            return p is null ? null : (p.Id, p.ProjectNumber);
        }

        public async Task<Guid?> SingleProjectId() => (await Single())?.Id;

        public async Task<List<(string, string)>> Describe(Param[] ps)
        {
            var list = new List<(string, string)>();
            foreach (var p in ps)
            {
                var raw = Str(p.Key) ?? p.Default;
                if (raw is null || raw == "false") continue;
                var ids = Http.Ids(raw);
                var value = p.Type switch
                {
                    // Only projects the caller may view are named; people and reference data are organisation-wide already.
                    "projects" or "project" => string.Join(", ", await access.VisibleProjects().Where(x => ids.Contains(x.Id)).Select(x => x.ProjectNumber).ToListAsync()),
                    "discipline" => string.Join(", ", await db.Disciplines.Where(x => ids.Contains(x.Id)).Select(x => x.Name).ToListAsync()),
                    "office" => string.Join(", ", await db.Offices.Where(x => ids.Contains(x.Id)).Select(x => x.Name).ToListAsync()),
                    "person" => string.Join(", ", await db.Users.Where(x => ids.Contains(x.Id)).Select(x => x.DisplayName).ToListAsync()),
                    "bool" => Text.Get("common.yes"),
                    "select" => Text.Get($"opt.{raw}"),
                    _ => raw,
                };
                list.Add((Text.Get($"param.{p.Key}"), value));
            }
            if (list.All(x => x.Item1 != Text.Get("param.projectId")) && ps.Any(p => p.Type == "projects")) list.Insert(0, (Text.Get("param.projectId"), Text.Get("report.allVisible")));
            return list;
        }
    }

    static IQueryable<WorkTask> ByDiscipline(Ctx c, IQueryable<WorkTask> q) =>
        c.Id("disciplineId") is { } d ? q.Where(t => c.Db.ProjectDisciplines.Any(pd => pd.Id == t.ProjectDisciplineId && pd.DisciplineId == d)) : q;

    static IQueryable<Deliverable> ByDiscipline(Ctx c, IQueryable<Deliverable> q) =>
        c.Id("disciplineId") is { } d ? q.Where(x => c.Db.ProjectDisciplines.Any(pd => pd.Id == x.ProjectDisciplineId && pd.DisciplineId == d)) : q;

    /// The task list query itself (TaskQueries.Apply), over the report's projects.
    static IQueryable<WorkTask> Tasks(Ctx c, TaskFilter f) =>
        ByDiscipline(c, TaskQueries.Apply(c.Db, c.Db.Tasks.AsNoTracking().Where(t => c.ProjectIds().Contains(t.ProjectId)), f, c.Me.Id, c.Today, c.S));

    static JsonArray Json(IEnumerable<object> rows) => JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray();

    /// "Open as filtered list" for a single-project report: the project's list with the same filters.
    static async Task<string?> Link(Ctx c, string tab, params (string Key, string? Value)[] filters)
    {
        if (await c.Single() is not { } p) return null;
        var parts = filters.Where(f => !string.IsNullOrEmpty(f.Value)).Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value!)}");
        if (c.Id("disciplineId") is { } d && await c.Db.ProjectDisciplines.Where(x => x.ProjectId == p.Id && x.DisciplineId == d).Select(x => (Guid?)x.Id).FirstOrDefaultAsync() is { } pd)
            parts = parts.Append($"disciplineId={pd}");
        var qs = string.Join("&", parts);
        return $"/projects/{p.Number}/{tab}{(qs.Length > 0 ? "?" + qs : "")}";
    }

    public static string Indicators(JsonNode? s)
    {
        if (s is null) return "";
        var parts = new List<string>();
        bool B(string k) => s[k]?.GetValue<bool>() == true;
        int N(string k) => s[k]?.GetValue<int>() ?? 0;
        if (B("isOverdue")) parts.Add(Text.Get("ind.overdue", N("daysOverdue")));
        if (B("isAtRisk")) parts.Add(Text.Get("ind.atRisk"));
        if (B("isBlocked")) parts.Add(Text.Get("ind.blocked"));
        else if (B("isWaiting")) parts.Add(Text.Get("ind.waiting"));
        if (B("isBlocking")) parts.Add(Text.Get("ind.blocking", N("blockingCount")));
        if (B("isDueSoon")) parts.Add(Text.Get("ind.dueSoon"));
        if (B("isStale")) parts.Add(Text.Get("ind.stale"));
        if (B("isUnassigned")) parts.Add(Text.Get("ind.unassigned"));
        if (B("isDateInconsistent")) parts.Add(Text.Get("ind.dateInconsistent"));
        if (B("isInactiveOwner")) parts.Add(Text.Get("ind.inactiveOwner"));
        if (B("isHeldPastDue")) parts.Add(Text.Get("ind.heldPastDue"));
        return string.Join("; ", parts);
    }

    /// Keys and names of milestones, for the "affected milestone" columns.
    static async Task<Dictionary<Guid, string>> MilestoneNames(HubDb db, IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        return await db.Milestones.AsNoTracking().Where(m => list.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => $"{m.Key} {m.Name}");
    }

    static IEnumerable<Guid> GuidList(JsonNode? n) => n is JsonArray a ? a.Select(x => Guid.Parse(x!.GetValue<string>())) : [];

    static async Task<JsonArray> WithMilestones(HubDb db, JsonArray rows)
    {
        var names = await MilestoneNames(db, rows.SelectMany(r => GuidList(r!["state"]?["affectedMilestoneIds"])));
        foreach (var r in rows) r!["affectedMilestones"] = string.Join("; ", GuidList(r["state"]?["affectedMilestoneIds"]).Select(id => names.GetValueOrDefault(id)).OfType<string>());
        return rows;
    }

    // ---------- Reports ----------

    static async Task<Result> DueThisWeek(Ctx c)
    {
        var day = c.Date("week") ?? c.Today;
        var from = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var to = from.AddDays(6);
        var f = new TaskFilter { DueFrom = from, DueTo = to, Indicators = ["open"], AssigneeIds = c.Id("assigneeId") is { } a ? [a] : [], Mine = c.Bool("mine") ? true : null };
        var rows = Json(await TaskQueries.Rows(c.Db, TaskQueries.Sort(Tasks(c, f), null)));
        foreach (var r in rows) r!["indicators"] = Indicators(r["state"]);
        return new(rows, await Link(c, "tasks", ("dueFrom", $"{from:yyyy-MM-dd}"), ("dueTo", $"{to:yyyy-MM-dd}"), ("open", "true"), ("assigneeId", c.Str("assigneeId")), ("mine", c.Bool("mine") ? "true" : null)));
    }

    static async Task<Result> Overdue(Ctx c)
    {
        var min = Math.Max(c.Int("minDaysOverdue") ?? 1, 1);
        var f = new TaskFilter { Indicators = ["overdue"], AssigneeIds = c.Id("assigneeId") is { } a ? [a] : [] };
        var q = Tasks(c, f).Where(t => c.Db.TaskStates.Any(s => s.TaskId == t.Id && s.DaysOverdue >= min));
        var rows = await WithMilestones(c.Db, Json(await TaskQueries.Rows(c.Db, q.OrderBy(t => t.DueDate).ThenBy(t => t.Seq))));
        return new(rows, min > 1 ? null : await Link(c, "tasks", ("overdue", "true"), ("assigneeId", c.Str("assigneeId"))));
    }

    static async Task<Result> Blocked(Ctx c)
    {
        var kind = c.Str("blockerType");
        var rows = await WithMilestones(c.Db, Json(await TaskQueries.Rows(c.Db, Tasks(c, new TaskFilter { Indicators = ["blocked"] }).OrderBy(t => t.DueDate).ThenBy(t => t.Seq))));
        var kept = new JsonArray();
        foreach (var r in rows.ToList())
        {
            var blockers = (r!["state"]?["blockedBy"] as JsonArray ?? []).Where(b => b!["blocking"]?.GetValue<bool>() != false).ToList();
            if (kind is not null && blockers.All(b => b!["type"]?.GetValue<string>() != kind)) continue;
            r["blockers"] = string.Join("; ", blockers.Select(b => $"{b!["key"]?.GetValue<string>() ?? b["name"]?.GetValue<string>()}{(b["key"] is null ? "" : " " + b["name"]?.GetValue<string>())}"
                + (b["reason"]?.GetValue<string>() is { } why ? $" ({why})" : b["overdue"]?.GetValue<bool>() == true ? $" ({Text.Get("ind.overdueWord")})" : "")));
            rows.Remove(r);
            kept.Add(r);
        }
        return new(kept, kind is null ? await Link(c, "tasks", ("blocked", "true")) : null);
    }

    static async Task<Result> Blocking(Ctx c)
    {
        var rows = Json(await TaskQueries.Rows(c.Db, Tasks(c, new TaskFilter { Indicators = ["blocking"] }).OrderBy(t => t.DueDate).ThenBy(t => t.Seq)));
        var succIds = rows.SelectMany(r => GuidList(r!["state"]?["blockingTaskIds"])).Distinct().ToList();
        var succ = await c.Db.Tasks.AsNoTracking().Where(t => succIds.Contains(t.Id))
            .Select(t => new { t.Id, Milestone = t.MilestoneId ?? c.Db.Deliverables.Where(d => d.Id == t.DeliverableId).Select(d => d.MilestoneId).FirstOrDefault() }).ToListAsync();
        var names = await MilestoneNames(c.Db, succ.Select(s => s.Milestone).OfType<Guid>());
        var byTask = succ.ToDictionary(s => s.Id, s => s.Milestone);
        foreach (var r in rows)
            r!["successorMilestones"] = string.Join("; ", GuidList(r["state"]?["blockingTaskIds"]).Select(id => byTask.GetValueOrDefault(id)).OfType<Guid>().Distinct()
                .Select(m => names.GetValueOrDefault(m)).OfType<string>());
        return new(rows, await Link(c, "tasks", ("blocking", "true")));
    }

    static async Task<Result> UpcomingDeliverables(Ctx c)
    {
        var days = Math.Clamp(c.Int("daysAhead") ?? 14, 0, 365);
        var q = ByDiscipline(c, DeliverableEndpoints.Filter(c.Db, c.Db.Deliverables.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId)),
            dueFrom: c.Today, dueTo: c.Today.AddDays(days), indicator: "open"));
        var rows = await WithProject(c.Db, Json(await DeliverableEndpoints.Rows(c.Db, q)));
        foreach (var r in rows) r!["indicators"] = Indicators(r["state"]);
        return new(rows, await Link(c, "deliverables", ("dueFrom", $"{c.Today:yyyy-MM-dd}"), ("dueTo", $"{c.Today.AddDays(days):yyyy-MM-dd}"), ("indicator", "open")));
    }

    static async Task<Result> DeliverableStatus_(Ctx c)
    {
        var q = ByDiscipline(c, c.Db.Deliverables.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId) && d.Status != DeliverableStatus.Cancelled));
        var rows = await WithProject(c.Db, Json(await DeliverableEndpoints.Rows(c.Db, q)));
        var sorted = new JsonArray([.. rows.Select(r => r!.DeepClone()).OrderBy(r => r["projectNumber"]?.GetValue<string>()).ThenBy(r => r["key"]?.GetValue<string>())]);
        return new(sorted, await Link(c, "deliverables"));
    }

    static async Task<JsonArray> WithProject(HubDb db, JsonArray rows)
    {
        var ids = rows.Select(r => Guid.Parse(r!["projectId"]!.GetValue<string>())).Distinct().ToList();
        var numbers = await db.Projects.AsNoTracking().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProjectNumber);
        foreach (var r in rows) r!["projectNumber"] = numbers.GetValueOrDefault(Guid.Parse(r["projectId"]!.GetValue<string>()));
        return rows;
    }

    static async Task<Result> UpcomingMilestones(Ctx c)
    {
        var days = Math.Clamp(c.Int("daysAhead") ?? 30, 0, 365);
        var until = c.Today.AddDays(days);
        var q = c.Db.Milestones.AsNoTracking().Where(m => c.ProjectIds().Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date >= c.Today && m.Date <= until);
        if (c.Str("type") is { } type) q = q.Where(m => m.MilestoneType == type);
        var rows = await WithProject(c.Db, Json(await MilestoneEndpoints.Rows(c.Db, q)));
        foreach (var r in rows) r!["deliverablesIssued"] = $"{r["deliverableIssued"]}/{r["deliverableTotal"]}";
        return new(rows, await Link(c, "milestones", ("type", c.Str("type"))));
    }

    static async Task<Result> OpenDecisions(Ctx c)
    {
        var q = c.Db.Decisions.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId)
            && (d.Status == DecisionStatus.Pending || d.Status == DecisionStatus.UnderReview || d.Status == DecisionStatus.Deferred));
        q = c.Str("ownerType") switch
        {
            "internal" => q.Where(d => d.OwnerUserId != null),
            "external" => q.Where(d => d.OwnerExternalPartyId != null),
            "client" => q.Where(d => c.Db.ExternalParties.Any(x => x.Id == d.OwnerExternalPartyId && x.IsClient)),
            _ => q,
        };
        if (c.Bool("overdueOnly")) q = q.Where(d => c.Db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsOverdue));
        var rows = await WithProject(c.Db, Json(await DecisionEndpoints.Rows(c.Db, q, c.Today)));
        foreach (var r in rows)
        {
            r!["days"] = r["isOverdue"]?.GetValue<bool>() == true ? -r["daysOverdue"]!.GetValue<int>() : r["daysUntil"] is JsonNode du ? du.GetValue<int>() : null;
            r["blocking"] = (r["blockingTaskIds"] as JsonArray)?.Count ?? 0;
        }
        return new(rows, await Link(c, "decisions", ("status", "Pending,Under Review,Deferred"), ("ownerType", c.Str("ownerType")), ("indicator", c.Bool("overdueOnly") ? "overdue" : null)));
    }

    /// §19 (P2): open issues and open risks, most severe first; without a severity, every open issue and the High risks.
    /// The date is the issue's target or the risk's review date, and days overdue count past either (ISS-03, RSK-02).
    static async Task<Result> OpenIssuesHighRisks(Ctx c)
    {
        var sev = c.Str("severity");
        var issues = c.Db.Issues.AsNoTracking().Where(i => c.ProjectIds().Contains(i.ProjectId) && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress));
        if (sev is not null) issues = issues.Where(i => i.Severity == sev);
        var scores = RegisterEndpoints.Scores([sev ?? Impact.High]);
        var risks = c.Db.Risks.AsNoTracking().Where(r => c.ProjectIds().Contains(r.ProjectId) && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring)
            && scores.Contains(r.Probability * r.Impact));
        var rows = new List<(int Weight, string? Date, JsonNode Row)>(); // ISO dates sort as text
        foreach (var i in Json(await RegisterEndpoints.IssueRows(c.Db, issues, c.Today)))
        {
            i!["itemType"] = ItemType.Issue; i["itemTypeLabel"] = Text.Get("itemType.Issue"); i["date"] = i["targetResolutionDate"]?.DeepClone();
            rows.Add((Registers.Weight(i["severity"]!.GetValue<string>()), i["targetResolutionDate"]?.GetValue<string>(), i));
        }
        foreach (var r in Json(await RegisterEndpoints.RiskRows(c.Db, risks, c.Today)))
        {
            var band = r!["band"]!.GetValue<string>();
            r["itemType"] = ItemType.Risk; r["itemTypeLabel"] = Text.Get("itemType.Risk"); r["severity"] = $"{band} ({r["score"]})";
            r["date"] = r["reviewDate"]?.DeepClone(); r["daysOverdue"] = r["reviewOverdueDays"]?.DeepClone();
            rows.Add((Registers.Weight(band), r["reviewDate"]?.GetValue<string>(), r));
        }
        var sorted = new JsonArray([.. rows.OrderBy(x => x.Weight).ThenBy(x => x.Date ?? "9999").Select(x => x.Row.DeepClone())]);
        return new(await WithProject(c.Db, sorted), await Link(c, "issues", ("indicator", "open"), ("severity", sev)));
    }

    /// §19 (P2): open meeting actions by due date, optionally of one owner type.
    static async Task<Result> MeetingActions(Ctx c)
    {
        var q = c.Db.Actions.AsNoTracking().Where(a => c.ProjectIds().Contains(a.ProjectId) && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress));
        if (c.Str("ownerType") is { } ot) q = q.Where(a => a.OwnerType == ot);
        return new(Json(await MeetingEndpoints.Rows(c.Db, q, c.Today)), await Link(c, "meetings", ("indicator", "open"), ("ownerType", c.Str("ownerType"))));
    }

    static async Task<Result> ReviewQueue(Ctx c)
    {
        var reviewer = c.Id("reviewerId");
        var f = new TaskFilter { Indicators = ["readyForReview"], ReviewerIds = reviewer is { } r ? [r] : [] };
        var tasks = Json(await TaskQueries.Rows(c.Db, Tasks(c, f).OrderBy(t => t.ReviewRequestedAt)));
        var taskIds = tasks.Select(x => Guid.Parse(x!["id"]!.GetValue<string>())).ToList();
        var requested = await c.Db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.ReviewRequestedAt ?? t.StatusChangedAt);
        var dq = c.Db.Deliverables.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId) && d.Status == DeliverableStatus.InReview);
        if (reviewer is { } rv) dq = dq.Where(d => d.ReviewerId == rv);
        var dels = await WithProject(c.Db, Json(await DeliverableEndpoints.Rows(c.Db, dq)));
        var since = await dq.ToDictionaryAsync(d => d.Id, d => d.StatusChangedAt);
        var rows = new JsonArray();
        foreach (var t in tasks)
        {
            var at = requested.GetValueOrDefault(Guid.Parse(t!["id"]!.GetValue<string>()));
            rows.Add(new JsonObject
            {
                ["itemType"] = ItemType.Task, ["itemTypeLabel"] = Text.Get("itemType.Task"), ["id"] = t["id"]!.DeepClone(), ["projectNumber"] = t["projectNumber"]!.DeepClone(),
                ["key"] = t["key"]!.DeepClone(), ["name"] = t["name"]!.DeepClone(), ["assigneeName"] = t["assigneeName"]?.DeepClone(), ["reviewerName"] = t["reviewerName"]?.DeepClone(),
                ["readySince"] = at is { } a ? Clock.LocalDate(a, c.S).ToString("yyyy-MM-dd") : null, ["daysWaiting"] = at is { } w ? c.Today.DayNumber - Clock.LocalDate(w, c.S).DayNumber : null,
                ["round"] = t["reviewRound"]!.DeepClone(),
            });
        }
        foreach (var d in dels)
        {
            var at = since.GetValueOrDefault(Guid.Parse(d!["id"]!.GetValue<string>()));
            rows.Add(new JsonObject
            {
                ["itemType"] = ItemType.Deliverable, ["itemTypeLabel"] = Text.Get("itemType.Deliverable"), ["id"] = d["id"]!.DeepClone(), ["projectNumber"] = d["projectNumber"]?.DeepClone(),
                ["key"] = d["key"]!.DeepClone(), ["name"] = d["name"]!.DeepClone(), ["assigneeName"] = d["ownerName"]?.DeepClone(), ["reviewerName"] = d["reviewerName"]?.DeepClone(),
                ["readySince"] = at is { } a ? Clock.LocalDate(a, c.S).ToString("yyyy-MM-dd") : null, ["daysWaiting"] = at is { } w ? c.Today.DayNumber - Clock.LocalDate(w, c.S).DayNumber : null,
            });
        }
        var ordered = new JsonArray([.. rows.Select(r => r!.DeepClone()).OrderByDescending(r => r["daysWaiting"]?.GetValue<int?>() ?? -1)]);
        return new(ordered, dels.Count == 0 ? await Link(c, "tasks", ("readyForReview", "true"), ("reviewerId", c.Str("reviewerId"))) : null);
    }

    static async Task<Result> Stale(Ctx c)
    {
        var days = Math.Max(c.Int("days") ?? c.S.TaskStaleDays, 1);
        var cutoff = c.Now.AddDays(-days);
        var q = Tasks(c, new TaskFilter { Indicators = ["open"] }).Where(t => t.Status != TaskStatuses.OnHold && t.LastActivityAt < cutoff);
        var rows = Json(await TaskQueries.Rows(c.Db, q.OrderBy(t => t.LastActivityAt)));
        foreach (var r in rows)
            r!["staleDays"] = c.Today.DayNumber - Clock.LocalDate(DateTimeOffset.Parse(r["lastActivityAt"]!.GetValue<string>(), CultureInfo.InvariantCulture), c.S).DayNumber;
        return new(rows, days == c.S.TaskStaleDays ? await Link(c, "tasks", ("stale", "true")) : null);
    }

    static async Task<Result> ActivityLog_(Ctx c)
    {
        var id = c.Id("projectId") ?? throw ApiException.Invalid("projectId", "error.required");
        var (p, _) = await c.Access.Project(id, track: false);
        var logs = await ActivityEndpoints.Filter(await ActivityEndpoints.Visible(c.Db, c.Access, id), c.Date("from"), c.Date("to"), null, null, c.Str("category"), null, null)
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(Export.MaxRows + 1).ToListAsync();
        var rows = Json(await ActivityEndpoints.Render(c.Db, logs));
        foreach (var r in rows)
        {
            r!["item"] = $"{r["itemKey"]?.GetValue<string>()} {r["itemName"]?.GetValue<string>()}".Trim();
            r["projectNumber"] = p.ProjectNumber;
        }
        var qs = string.Join("&", new[] { ("from", c.Str("from")), ("to", c.Str("to")), ("category", c.Str("category")) }.Where(x => x.Item2 is not null).Select(x => $"{x.Item1}={x.Item2}"));
        return new(rows, $"/projects/{p.ProjectNumber}/activity{(qs.Length > 0 ? "?" + qs : "")}");
    }

    /// §19 Projects At Risk: active projects whose computed or reported health is Red or Yellow, with the reasons.
    static async Task<Result> ProjectsAtRisk(Ctx c)
    {
        var query = new ProjectEndpoints.ProjectQuery(Q: null, Status: ProjectStatus.Active, PmId: c.Id("pmId"), ClientId: null, OfficeId: c.Id("officeId"), PhaseId: null,
            DisciplineId: null, Health: null, ProjectTypeId: null, IncludeArchived: null, Mine: false, Starred: null, Priority: null, Ids: null, Sort: "submission",
            Page: null, PageSize: null, SubmissionWithinDays: null);
        var (rows, _) = await PortfolioEndpoints.Rows(query, c.Access, c.Db, c.Me, c.Store, c.Clock);
        var wanted = c.Str("health") is { } h ? [h] : new[] { Health.Red, Health.Yellow };
        return new(new JsonArray([.. rows.Where(r => wanted.Contains(r!["computedHealth"]?.GetValue<string>()) || wanted.Contains(r!["reportedHealth"]?.GetValue<string>()))
            .Select(r => r!.DeepClone())]));
    }

    static async Task<Result> WorkloadEmployee(Ctx c)
    {
        var (weeks, people, _) = await WorkloadEndpoints.Grid(new WorkloadEndpoints.WorkloadQuery(c.Id("supervisorId"), c.Id("disciplineId"), c.Id("officeId"), c.Id("projectId"), null, null, "name"),
            c.Access, c.Db, c.Me, c.Store, c.Clock);
        var (cols, rows) = WorkloadEndpoints.EmployeeRows(weeks, people);
        return new(rows, null, cols);
    }

    /// §19 Task Hours (§36.8): the Time view's permitted, non-deleted entries and their total.
    static async Task<Result> TaskHours(Ctx c)
    {
        var from = c.Date("from") ?? Workload.WeekOf(c.Today);
        var to = c.Date("to") ?? from.AddDays(6);
        var f = new TimeEndpoints.TimeQuery(from, to, c.Id("projectId"), null, c.Id("userId"), c.Str("scope") == "team" ? "all" : "mine");
        var rows = await TimeEndpoints.Visible(f, c.Access, c.Db, c.Me);
        var json = Json(rows.Select(r => (object)new { r.WorkDate, r.Person, r.ProjectNumber, r.TaskKey, r.TaskName, r.Hours, r.Note, ItemType = ItemType.Task, Id = r.TaskId }));
        json.Add(new JsonObject { ["person"] = Text.Get("time.total"), ["hours"] = rows.Sum(r => r.Hours) });
        var qs = string.Join("&", new[] { ("from", from.ToString("yyyy-MM-dd")), ("to", to.ToString("yyyy-MM-dd")), ("projectId", c.Str("projectId")), ("userId", c.Str("userId")), ("scope", c.Str("scope") == "team" ? "all" : null) }
            .Where(x => x.Item2 is not null).Select(x => $"{x.Item1}={x.Item2}"));
        return new(json, $"/time?{qs}");
    }

    static async Task<Result> WorkloadDiscipline(Ctx c)
    {
        var (cols, rows) = await WorkloadEndpoints.DisciplineRows(c.Id("officeId"), c.Access, c.Db, c.Me, c.Store, c.Clock);
        return new(rows, null, cols);
    }

    /// §19 Health History from the nightly snapshots (§16.5).
    static async Task<Result> HealthHistory(Ctx c)
    {
        var from = c.Date("from") ?? c.Today.AddDays(-56);
        var to = c.Date("to") ?? c.Today;
        var rows = await c.Db.HealthSnapshots.AsNoTracking().Where(x => c.ProjectIds().Contains(x.ProjectId) && x.SnapshotDate >= from && x.SnapshotDate <= to)
            .Join(c.Db.Projects, x => x.ProjectId, p => p.Id, (x, p) => new { p.ProjectNumber, p.Name, Date = x.SnapshotDate, Computed = x.ComputedHealth, Reported = x.ReportedHealth })
            .OrderBy(x => x.ProjectNumber).ThenBy(x => x.Date).ToListAsync();
        return new(Json(rows));
    }

    /// Packet 011 FR-001: the pilot's weekly readings for the chosen projects — G1 weeks with a coordination review recorded,
    /// G2 open tasks without an owner or due date and G3 blocked tasks (each project's last nightly snapshot of the week),
    /// and G4 how many people with open tasks changed a task's status that week. G5 and G6 are read in the weekly review.
    static async Task<Result> PilotMeasures(Ctx c)
    {
        var weeks = Math.Clamp(c.Int("weeks") ?? 8, 1, 26);
        var pids = await c.ProjectIds().ToListAsync();
        var monday = c.Today.AddDays(-(((int)c.Today.DayOfWeek + 6) % 7));
        var first = monday.AddDays(-7 * (weeks - 1));
        var zone = Clock.Zone(c.S);
        DateTimeOffset Utc(DateOnly d) { var t = d.ToDateTime(TimeOnly.MinValue); return new DateTimeOffset(t, zone.GetUtcOffset(t)).ToUniversalTime(); }
        var since = Utc(first);
        var snaps = await c.Db.HealthSnapshots.AsNoTracking().Where(x => pids.Contains(x.ProjectId) && x.SnapshotDate >= first).Select(x => new { x.ProjectId, x.SnapshotDate, x.Counts }).ToListAsync();
        var log = c.Db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && pids.Contains(a.ProjectId.Value) && a.OccurredAt >= since);
        var reviews = await log.Where(a => a.Action == "CoordinationReviewed").Select(a => new { a.ProjectId, a.OccurredAt }).ToListAsync();
        var changes = await log.Where(a => a.ItemType == ItemType.Task && a.Action == "StatusChanged" && a.ActorUserId != null).Select(a => new { User = a.ActorUserId!.Value, a.OccurredAt }).ToListAsync();
        var people = (await c.Db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.AssigneeId != null && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => t.AssigneeId!.Value).Distinct().ToListAsync()).ToHashSet();
        var rows = new List<object>();
        for (var w = first; w <= monday; w = w.AddDays(7))
        {
            var (from, to) = (Utc(w), Utc(w.AddDays(7)));
            var latest = snaps.Where(x => x.SnapshotDate >= w && x.SnapshotDate < w.AddDays(7)).GroupBy(x => x.ProjectId)
                .Select(g => JsonSerializer.Deserialize<Dictionary<string, int>>(g.MaxBy(x => x.SnapshotDate)!.Counts) ?? []).ToList();
            int? Sum(string k) => latest.Count == 0 ? null : latest.Sum(d => d.GetValueOrDefault(k));
            var updaters = changes.Where(x => x.OccurredAt >= from && x.OccurredAt < to && people.Contains(x.User)).Select(x => x.User).Distinct().Count();
            rows.Add(new
            {
                WeekOf = w, Projects = pids.Count, Reviewed = reviews.Where(x => x.OccurredAt >= from && x.OccurredAt < to).Select(x => x.ProjectId).Distinct().Count(),
                Unassigned = Sum("tasksUnassigned"), NoDueDate = Sum("tasksNoDueDate"), Blocked = Sum("tasksBlocked"),
                People = people.Count, Updaters = updaters, UpdatersPct = people.Count == 0 ? (int?)null : updaters * 100 / people.Count,
            });
        }
        return new(Json(rows));
    }

    /// FR-ASG-08: Supervisors see their direct reports; Executives and Admins may ask for all staff.
    static async Task<Result> StaffAssignments(Ctx c)
    {
        var a = c.Me.Actor;
        var all = c.Str("scope") == "all";
        if (all && !Permissions.ViewAllStaff(a)) throw ApiException.Forbidden("perm.all_staff");
        var people = c.Db.Users.AsNoTracking().Where(u => u.IsActive && (all || u.SupervisorId == c.Me.Id));
        if (c.Id("officeId") is { } office) people = people.Where(u => u.OfficeId == office);
        var projects = c.Access.VisibleProjects().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        if (c.Ids("projectId") is { Length: > 0 } chosen) projects = projects.Where(p => chosen.Contains(p.Id));
        var members = c.Db.ProjectMembers.AsNoTracking().Where(m => m.RemovedAt == null && people.Any(u => u.Id == m.UserId) && projects.Any(p => p.Id == m.ProjectId));
        if (c.Str("role") is { } role) members = members.Where(m => m.Roles.Contains(role));
        if (c.Id("disciplineId") is { } d) members = members.Where(m => c.Db.ProjectDisciplines.Any(pd => pd.Id == m.PrimaryDisciplineId && pd.DisciplineId == d));
        var rows = await members.Select(m => new
        {
            m.UserId, m.ProjectId, m.Roles, m.AddedAt,
            Person = c.Db.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
            Project = c.Db.Projects.Where(p => p.Id == m.ProjectId).Select(p => new { p.ProjectNumber, p.Name, p.Status }).First(),
            PrimaryDiscipline = c.Db.ProjectDisciplines.Where(pd => pd.Id == m.PrimaryDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault(),
            AddedBy = c.Db.Users.Where(u => u.Id == m.AddedBy).Select(u => u.DisplayName).FirstOrDefault(),
            OpenTasks = c.Db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == m.UserId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
            OverdueTasks = c.Db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == m.UserId && c.Db.TaskStates.Any(s => s.TaskId == t.Id && s.IsOverdue)),
        }).ToListAsync();
        return new(Json(rows.OrderBy(r => r.Person).ThenBy(r => r.Project.ProjectNumber).Select(r => (object)new
        {
            r.Person, r.Project.ProjectNumber, ProjectName = r.Project.Name, ProjectStatus = r.Project.Status,
            Roles = string.Join(", ", r.Roles.Select(x => Text.Get($"role.{x}"))), r.PrimaryDiscipline,
            AddedAt = Clock.LocalDate(r.AddedAt, c.S), r.AddedBy, r.OpenTasks, r.OverdueTasks,
        })));
    }
}

/// Sends list and report rows as CSV (UTF-8 with BOM) or XLSX (header, frozen pane, typed dates, Parameters sheet), and
/// records who exported what, never the contents (§19, §20.1, FR-010).
public static class ExportFile
{
    public static async Task<IResult> Send(HubDb db, SettingsStore store, string? format, string title, IReadOnlyList<Col> cols, JsonArray rows, IReadOnlyList<(string, string)> parameters,
        Guid? projectId, string key, TimeProvider clock)
    {
        if (rows.Count > Export.MaxRows) throw ApiException.Rule("export_too_large", "export.too_large", null, Export.MaxRows);
        var s = await store.Get(db);
        var xlsx = format == "xlsx";
        var described = parameters.Count == 0 ? title : $"{title} ({string.Join("; ", parameters.Select(p => $"{p.Item1}: {p.Item2}"))})";
        db.LogEvent(ItemType.Report, null, "Exported", "access", projectId, key: key, name: described.Length > 480 ? described[..480] : described);
        await db.SaveChangesAsync();
        // Times appear in the organisation's time zone, as on screen (G-03).
        object? Local(object? v) => v is DateTimeOffset t ? new DateTimeOffset(Clock.Local(t, s).DateTime, TimeSpan.Zero) : v;
        var columns = cols.Select(c => new ExportColumn(c.Title, r => Local(Value((JsonNode?)r, c)))).ToList();
        var now = Clock.Local(clock.GetUtcNow(), s);
        var file = $"{key}-{now:yyyyMMdd-HHmm}.{(xlsx ? "xlsx" : "csv")}";
        var items = rows.Cast<object>();
        return xlsx
            ? Results.File(Export.ToXlsx(title, items, columns, [.. parameters, (Text.Get("export.generated"), $"{now:yyyy-MM-dd HH:mm} ({s.OrgTimeZone})")]),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file)
            : Results.File(Export.ToCsv(items, columns), "text/csv; charset=utf-8", file);
    }

    static JsonNode? At(JsonNode? n, string path)
    {
        foreach (var part in path.Split('.')) n = n?[part];
        return n;
    }

    public static object? Value(JsonNode? row, Col c)
    {
        var v = At(row, c.Path);
        if (v is null) return null;
        if (v is JsonArray arr) return string.Join("; ", arr.Select(x => x?.ToString()));
        switch (v.GetValueKind())
        {
            case JsonValueKind.String:
                var s = v.GetValue<string>();
                if (c.Type == "date" && DateOnly.TryParse(s, CultureInfo.InvariantCulture, out var d)) return d;
                if (c.Type == "datetime" && DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, out var t)) return t;
                return s;
            case JsonValueKind.Number: return decimal.Parse(v.ToJsonString(), CultureInfo.InvariantCulture);
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return v.ToJsonString();
        }
    }
}
