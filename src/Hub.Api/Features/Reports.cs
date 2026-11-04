using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// A column of a report or list export: a JSON path into the row, a header text key (`col.*`) and a type that decides
/// how the SPA shows it and how XLSX stores it (dates stay dates, §19).
/// Label is for a title already resolved through Text.cs (for example with arguments); plain titles use col.{Header}.
public sealed record Col(string Path, string Header, string Type = "text", string? Label = null)
{
    public string Title => Label ?? Text.Get($"col.{Header}");
}

/// Standard reports (§19, FR-RPT-01, FR-ASG-08): fixed, rule-based queries with a few parameters, permission-filtered,
/// shown as tables and exported to CSV or XLSX. Each report reuses the list query it corresponds to, so its rows equal
/// the list "Open as filtered list" opens (SC-004).
public static partial class ReportEndpoints
{
    public sealed record Param(string Key, string Type, string[]? Options = null, string? Default = null);
    /// A column as the screen and the export header show it: its path into the row, its resolved title and its type.
    public sealed record ReportColumn(string Path, string Header, string Type);
    public sealed record ReportOption(string Value, string Label);
    public sealed record ReportParamView(string Key, string Label, string Type, string? Default, IReadOnlyList<ReportOption>? Options);
    /// One report in the catalogue: what it asks for and the columns it shows when it asks for nothing more.
    public sealed record ReportListItem(string Code, string Title, string Description, IReadOnlyList<ReportParamView> Params, string? ItemType, IReadOnlyList<ReportColumn> Columns,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Question = null);
    public sealed record ReportSavedViewDefinition(string Code, Param[] Parameters);
    /// A parameter as the server applied it, in words, so the result states what it covers.
    public sealed record ReportAppliedParameter(string Label, string Value);
    /// One report run. The rows differ by report, so `Rows` stays an array of free-form objects read through `Columns`.
    public sealed record ReportView(string Code, string Title, string? ItemType, IReadOnlyList<ReportColumn> Columns, JsonArray Rows, int Total, bool TotalIsLowerBound, bool Truncated,
        string? ListLink, IReadOnlyList<ReportAppliedParameter> Parameters,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? AsOf = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? SavedParameters = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReportTaskInputs? TaskInputs = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReportUnitView? UnitView = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReportCapacityComparison? CapacityComparison = null);
    sealed record TaskHoursRow(DateOnly WorkDate, string Person, string ProjectNumber, string TaskKey, string TaskName, decimal Hours, string? Note, string ItemType, Guid Id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? PersonId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? EntryId = null);
    sealed record HealthRow(string ProjectNumber, string Name, DateOnly Date, string Computed, string Reported);
    sealed record PilotWeekRow(DateOnly WeekOf, int Projects, int Reviewed, int? Unassigned, int? NoDueDate, int? Blocked, int People, int Updaters, int? UpdatersPct);
    sealed record PickupRow(string Project, string Discipline, DateTimeOffset Offered, DateTimeOffset? Closed, string CloseCause, int DaysOpen);
    sealed record MissReportRow(string? Project, string? Discipline, string? Category, int Count);
    sealed record StaffRow(string Person, string ProjectNumber, string ProjectName, string ProjectStatus, string Roles, string? PrimaryDiscipline, DateOnly AddedAt, string? AddedBy, int OpenTasks, int OverdueTasks,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? PersonId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? AddedById = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? MemberId = null);
    sealed record ProjectRef(Guid Id, string ProjectNumber);
    sealed record SuccessorMilestone(Guid Id, Guid? Milestone);
    sealed record HealthSnapshotRow(string ProjectNumber, string Name, DateOnly Date, string Computed, string Reported);
    sealed record ReportSnapshotSource(Guid ProjectId, DateOnly SnapshotDate, string Counts);
    sealed record ReviewMarker(Guid? ProjectId, DateTimeOffset OccurredAt);
    sealed record StatusChangeMarker(Guid User, DateTimeOffset OccurredAt);
    sealed record PickupProjection(Guid Assignee, Guid ProjectId, Guid ProjectDisciplineId, string Project, string Discipline, DateTimeOffset Offered, DateTimeOffset? Closed, string? CloseCause);
    sealed record CommitmentDimension(Guid ProjectId, Guid DisciplineId, Guid? CategoryId);
    sealed record CommitmentGroupKey(Guid ProjectId, Guid DisciplineId, Guid? CategoryId);
    sealed record StaffProject(string ProjectNumber, string Name, string Status);
    sealed record StaffAssignmentSource(Guid UserId, Guid ProjectId, List<string> Roles, DateTimeOffset AddedAt, string Person, StaffProject Project, string? PrimaryDiscipline, string? AddedBy, int OpenTasks, int OverdueTasks, Guid MemberId, Guid? AddedById);
    sealed record Def(string Code, Param[] Params, Col[] Cols, Func<Ctx, Task<Result>> Run, Func<Actor, bool>? Allowed = null, string? Flag = null,
        Func<Ctx, IQueryable<WorkTask>>? TaskSource = null);
    sealed record Result(JsonArray Rows, string? ListLink = null, Col[]? Columns = null);

    static readonly string[] Live = [ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Complete];
    const int ScreenRows = 2000;
    /// Report queries read one row past the export cap: enough to show the screen and refuse an oversized export, never a whole huge scope.
    const int Cap = Export.MaxRows + 1;

    static Param Projects => new("projectId", "projects");
    static Param Discipline => new("disciplineId", "discipline");

    static readonly Def[] All =
    [
        new("pilot-route-use", [new("weeks", "number", Default: "8")], [new("routeKey", "route"), new("week", "pilotWeek", "date"), new("opens", "opens", "number"), new("under500Ms", "under500Ms", "number"), new("from500To999Ms", "from500To999Ms", "number"), new("from1000To1999Ms", "from1000To1999Ms", "number"), new("from2000To4999Ms", "from2000To4999Ms", "number"), new("over5000Ms", "over5000Ms", "number")], PilotRouteUse, a => Permissions.ViewPortfolio(a).Ok, "route_counter"),
        new("team-capacity", CapacityParams, [], TeamCapacity, a => Permissions.ViewWorkload(a).Ok, "capacity_reports"),
        new("allocations-by-source", [..CapacityParams, new("show", "select", ["hours-by-source", "entries-without-project"], "hours-by-source"), new("source", "select", PlanningSource.All), new("confidence", "select", PlanningConfidence.All)], [], AllocationsBySource, a => Permissions.ViewWorkload(a).Ok, "capacity_reports"),
        new("pickup-offers", [Projects, Discipline, new("from", "date"), new("to", "date")],
            [new("project", "project"), new("discipline", "discipline"), new("offered", "offered", "datetime"),
             new("closed", "closed", "datetime"), new("closeCause", "closeCause"), new("daysOpen", "daysOpen", "number")], PickupOffers, Flag: "task_pickup"),
        new("commitment-misses", [Projects, Discipline, new("from", "date"), new("to", "date")],
            [new("project", "project"), new("discipline", "discipline"), new("category", "missCategory"),
             new("count", "count", "number")], CommitmentMisses, Flag: "commitment_reliability"),
        new("tasks-due-this-week", [Projects, Discipline, new("assigneeId", "person"), new("week", "date"), new("mine", "bool")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("deliverableKey", "deliverable"), new("disciplineName", "discipline"),
             new("assigneeName", "assignee"), new("dueDate", "due", "date"), new("status", "status"), new("indicators", "indicators")], DueThisWeek, TaskSource: DueThisWeekSource),
        new("overdue-tasks", [Projects, Discipline, new("assigneeId", "person"), new("minDaysOverdue", "number", Default: "1")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("dueDate", "due", "date"),
             new("state.daysOverdue", "daysOverdue", "number"), new("state.blockingCount", "blockingCount", "number"), new("affectedMilestones", "affectedMilestones"), new("status", "status")], Overdue, TaskSource: OverdueSource),
        new("blocked-tasks", [Projects, Discipline, new("blockerType", "select", ["task", "decision", "manual"])],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("state.blockedSince", "blockedSince", "date"),
             new("state.daysBlocked", "daysBlocked", "number"), new("blockers", "blockers"), new("affectedMilestones", "affectedMilestones")], Blocked, TaskSource: BlockedSource),
        new("tasks-blocking-others", [Projects],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("dueDate", "due", "date"),
             new("status", "status"), new("state.blockingCount", "successors", "number"), new("successorMilestones", "successorMilestones")], Blocking, TaskSource: BlockingSource),
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
             new("reviewerName", "reviewer"), new("readySince", "readySince", "date"), new("daysWaiting", "daysWaiting", "number"), new("round", "round", "number")], ReviewQueue, TaskSource: ReviewQueueSource),
        new("stale-work", [Projects, new("days", "number")],
            [new("key", "key", "key"), new("name", "task"), new("projectNumber", "project"), new("assigneeName", "assignee"), new("status", "status"),
             new("lastActivityAt", "lastActivity", "datetime"), new("staleDays", "days", "number")], Stale, TaskSource: StaleSource),
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

    static bool UnitReadableReport(string code) => code is "staff-assignments" or "workload-employee" or "workload-discipline" or "team-capacity" or "allocations-by-source";

    static Param[] ReportParams(Def def, OrgSettings settings, string[]? units = null) => [..
        settings.IsFlagOn("org_structure") && def.Code is "staff-assignments" or "team-capacity"
            ? [.. def.Params, new("unitId", "select", units), .. def.Code == "staff-assignments" ? new Param[] { new("week", "date") } : []]
            : def.Params, .. settings.IsFlagOn("capacity_screens") && HasReportUnits(def) ? UnitParams : []];

    /// Maintained registration union; actual read/write support is the current definition below.
    public static IReadOnlyDictionary<string, string[]> SavedViewParameterKeys { get; } = All.ToDictionary(
        def => $"report:{def.Code}", def => def.Params.Select(p => p.Key)
            .Concat(def.Code is "staff-assignments" or "team-capacity" ? ["unitId"] : Array.Empty<string>())
            .Concat(def.Code == "staff-assignments" ? ["week"] : Array.Empty<string>())
            .Concat(def.Code == "project-activity-log" ? ["history"] : Array.Empty<string>())
            .Concat(HasReportUnits(def) ? ["unit", "period"] : Array.Empty<string>()).Distinct().ToArray());

    /// Views reuses the report's current eligibility; the sharing container never changes its data filters.
    public static async Task<ReportSavedViewDefinition> SavedViewDefinition(string listType, Access access, HubDb db, SettingsStore store)
    {
        var s = await store.Get(db); Http.RequireFlag(s, "capacity_screens");
        var def = listType.StartsWith("report:", StringComparison.Ordinal) ? All.FirstOrDefault(r => listType == $"report:{r.Code}") : null;
        if (def is null) throw ApiException.NotFound();
        await DemandReport(def, access.Actor, db, s);
        return new(def.Code, [.. ReportParams(def, s).Select(p => p.Key == "scope" && !Permissions.ViewAllStaff(access.Actor) && def.Code == "staff-assignments"
                ? p with { Options = p.Options?.Where(option => option != "all").ToArray() } : p),
            .. def.Code == "project-activity-log" ? new Param[] { new("history", "select", ["all"]) } : []]);
    }

    static async Task DemandReport(Def def, Actor actor, HubDb db, OrgSettings s)
    {
        if (def.Allowed is { } ok && !ok(actor) && !(UnitReadableReport(def.Code) && await PeopleScope.ReadsAsUnitLeader(db, s, actor)))
            throw ApiException.Forbidden("perm.staff");
        if (def.Flag != null) Http.RequireFlag(s, def.Flag);
    }

    /// Definitions only: actual supported values, before the UI adds view/catalogue/container/panel state.
    static IReadOnlyDictionary<string, string> SavedParameters(Def def, Ctx c)
    {
        var values = new Dictionary<string, string>();
        void Date(string key, DateOnly date) => values[key] = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        void Number(string key, int value) => values[key] = value.ToString(CultureInfo.InvariantCulture);
        foreach (var p in ReportParams(def, c.S))
        {
            if (p.Key is "unit" or "period" && !UsesReportUnits(def, c)) continue;
            var raw = c.Str(p.Key) ?? p.Default; if (raw is null) continue;
            if (p.Type == "date") { if (c.Date(p.Key) is { } day) Date(p.Key, day); }
            else if (p.Type is "project" or "projects" or "person" or "discipline" or "office")
            { var ids = Http.Ids(raw); if (ids.Length > 0) values[p.Key] = string.Join(",", ids); }
            else if (p.Type == "bool") values[p.Key] = (p.Key == "includeMyDrafts" ? raw != "false" : raw == "true") ? "true" : "false";
            else if (p.Type == "number") { if (int.TryParse(raw, out var number)) Number(p.Key, number); }
            else values[p.Key] = raw;
        }
        switch (def.Code)
        {
            case "team-capacity": case "allocations-by-source":
                Date("from", Workload.WeekOf(c.Date("from") ?? c.Today)); Number("weeks", c.Int("weeks") ?? c.S.PlanningHorizonWeeks);
                values["includeMyDrafts"] = c.Str("includeMyDrafts") == "false" ? "false" : "true"; break;
            case "tasks-due-this-week": Date("week", Workload.WeekOf(c.Date("week") ?? c.Today)); break;
            case "pilot-measures": Number("weeks", Math.Clamp(c.Int("weeks") ?? 8, 1, 26)); break;
            case "pilot-route-use": Number("weeks", c.Int("weeks") ?? 8); break;
            case "upcoming-deliverables": Number("daysAhead", Math.Clamp(c.Int("daysAhead") ?? 14, 0, 365)); break;
            case "upcoming-milestones": Number("daysAhead", Math.Clamp(c.Int("daysAhead") ?? 30, 0, 365)); break;
            case "overdue-tasks": Number("minDaysOverdue", Math.Max(c.Int("minDaysOverdue") ?? 1, 1)); break;
            case "stale-work": Number("days", Math.Max(c.Int("days") ?? c.S.TaskStaleDays, 1)); break;
            case "health-history": Date("from", c.Date("from") ?? c.Today.AddDays(-56)); Date("to", c.Date("to") ?? c.Today); break;
            case "pickup-offers": case "commitment-misses": Date("from", c.Date("from") ?? c.Today.AddDays(-42)); Date("to", c.Date("to") ?? c.Today); break;
            case "task-hours":
                var from = c.Date("from") ?? Workload.WeekOf(c.Today); Date("from", from); Date("to", c.Date("to") ?? from.AddDays(6));
                values["scope"] = c.Str("scope") == "team" ? "team" : "mine"; break;
            case "project-activity-log": if (c.Date("from") is null && c.Date("to") is null) values["history"] = "all"; break;
        }
        return values;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/reports", async Task<Ok<ReportListItem[]>> (CurrentUser me, HubDb db, SettingsStore store) =>
        {
            var settings = await store.Get(db);
            var unitNames = settings.IsFlagOn("org_structure") ? await db.Set<OrgUnit>().AsNoTracking().OrderBy(u => u.Name).ToDictionaryAsync(u => u.Id.ToString(), u => u.Name) : new Dictionary<string, string>();
            var unitRead = await PeopleScope.ReadsAsUnitLeader(db, settings, me.Actor);
            return TypedResults.Ok(All.Where(r => ((r.Allowed?.Invoke(me.Actor) ?? true) || unitRead && (UnitReadableReport(r.Code))) && (r.Flag == null || settings.IsFlagOn(r.Flag))).Select(r => new ReportListItem(
                r.Code, Text.Get($"report.{r.Code}"), Text.Get($"report.{r.Code}.about"),
                [.. ReportParams(r, settings, unitNames.Keys.ToArray()).Select(p => new ReportParamView(p.Key, Text.Get($"param.{p.Key}"), p.Type, p.Key == "weeks" && r.Flag == "capacity_reports" ? settings.PlanningHorizonWeeks.ToString(CultureInfo.InvariantCulture) : p.Default,
                    p.Options is null ? null : [.. p.Options.Where(o => !(p.Key == "scope" && o == "all" && !Permissions.ViewAllStaff(me.Actor))).Select(o => new ReportOption(o, p.Key == "unitId" ? unitNames.GetValueOrDefault(o, o) : Text.Get($"opt.{o}")))]))],
                ItemTypeOf(r.Code), [.. r.Cols.Select(c => new ReportColumn(c.Path, c.Title, c.Type))],
                settings.IsFlagOn("capacity_screens") ? Text.Get($"report.{r.Code}.question") : null)).ToArray());
        });
        api.MapGet("/reports/{code}", async Task<Ok<ReportView>> (string code, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (def, ctx) = await Prepare(code, http, db, access, me, store, clock);
            await using var snapshot = ctx.InputsEnabled
                ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
            ctx.CaptureCapacityComparison = ctx.InputsEnabled && def.Code == "team-capacity";
            var r = await def.Run(ctx);
            var inputs = snapshot is not null ? await TaskInputSummary(def, ctx) : null;
            ctx.UnitUnestimated = inputs?.Unestimated.KnownCount ?? 0;
            return TypedResults.Ok(new ReportView(def.Code, Text.Get($"report.{def.Code}"), ItemTypeOf(def.Code), [.. (r.Columns ?? def.Cols).Select(c => new ReportColumn(c.Path, c.Title, c.Type))],
                ReportPeople.Add(def.Code, ItemTypeOf(def.Code), new JsonArray([.. r.Rows.Take(ScreenRows).Select(x => x?.DeepClone())]), ctx.S.IsFlagOn("people_screens")), r.Rows.Count, r.Rows.Count >= Cap, r.Rows.Count > ScreenRows, r.ListLink,
                [.. (await ctx.Describe(AppliedReportParams(def, ctx), def.Code == "project-activity-log")).Select(p => new ReportAppliedParameter(p.Item1, p.Item2))],
                ctx.S.IsFlagOn("visual_system") ? (snapshot is null ? clock.GetUtcNow() : ctx.Now) : null,
                ctx.S.IsFlagOn("capacity_screens") ? SavedParameters(def, ctx) : null,
                inputs, UnitView(def, ctx, r, ScreenRows), ctx.CapacityComparison));
        });
        api.MapGet("/reports/{code}/export", async (string code, string? format, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (def, ctx) = await Prepare(code, http, db, access, me, store, clock, export: true);
            await using var snapshot = UsesReportUnits(def, ctx) ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
            var r = await def.Run(ctx);
            var title = Text.Get($"report.{def.Code}");
            var parameters = await ctx.Describe(AppliedReportParams(def, ctx), def.Code == "project-activity-log");
            ReportUnitView? units = null;
            if (snapshot is not null)
            {
                ctx.UnitUnestimated = (await TaskInputSummary(def, ctx)).Unestimated.KnownCount;
                units = UnitView(def, ctx, r); r = UnitExport(r, units!);
            }
            var response = await ExportFile.Send(db, store, format, title, r.Columns ?? def.Cols, r.Rows, parameters, await ctx.SingleProjectId(), def.Code, clock,
                units is null ? null : UnitExportHeader(units, parameters));
            if (snapshot is not null) await snapshot.CommitAsync();
            return response;
        });
        MapTaskInputs(api);
    }

    /// The kind of item each row is, so a key cell opens its panel (review-queue rows carry their own).
    static string? ItemTypeOf(string code) => code switch
    {
        "upcoming-deliverables" or "deliverable-status-by-project" => ItemType.Deliverable,
        "upcoming-milestones" => ItemType.Milestone,
        "open-decisions" => ItemType.Decision,
        "meeting-actions-outstanding" => ItemType.Action,
        "team-capacity" or "allocations-by-source" or "pilot-route-use" or "project-activity-log" or "staff-assignments" or "review-queue" or "open-issues-high-risks" or "projects-at-risk" or "health-history" or "workload-employee" or "workload-discipline" or "task-hours" => null,
        _ => ItemType.Task,
    };

    static async Task<(Def, Ctx)> Prepare(string code, HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock, bool taskInputList = false, bool export = false)
    {
        var def = All.FirstOrDefault(r => r.Code == code) ?? throw ApiException.NotFound();
        var s = await store.Get(db);
        await DemandReport(def, me.Actor, db, s);
        var today = clock.Today(s); var now = clock.GetUtcNow();
        var inputs = s.IsFlagOn("capacity_screens") && HasTaskInputs(def);
        if (inputs) { today = Clock.LocalDate(now, s); ValidateTaskInputScope(def, http.Request.Query, s, taskInputList); }
        var context = new Ctx(http.Request.Query, db, access, me, s, today, now, store, clock);
        context.InputsEnabled = inputs;
        context.MemberProjectsOnly = export && !Permissions.ExportAllReportProjects(me.Actor);
        if (inputs) ValidateRemainingInputRange(def, context);
        ValidateUnitForm(def, context);
        context.InputTasks = def.TaskSource?.Invoke(context);
        return (def, context);
    }

    // ---------- Parameters and scope ----------

    sealed class Ctx(IQueryCollection q, HubDb db, Access access, CurrentUser me, OrgSettings s, DateOnly today, DateTimeOffset now, SettingsStore store, TimeProvider clock)
    {
        public HubDb Db => db; public Access Access => access; public CurrentUser Me => me; public OrgSettings S => s; public DateOnly Today => today; public DateTimeOffset Now => now;
        public SettingsStore Store => store; public TimeProvider Clock => clock;
        public bool Partial { get; set; }
        public IQueryable<WorkTask>? InputTasks { get; set; }
        public bool InputsEnabled { get; set; }
        /// A Standard User's export: rows come only from projects they belong to (§8.5.1).
        public bool MemberProjectsOnly { get; set; }
        public IQueryable<Project> Scope() => MemberProjectsOnly ? access.MemberProjects() : access.VisibleProjects();
        public bool CaptureCapacityComparison { get; set; }
        public ReportCapacityComparison? CapacityComparison { get; set; }
        public TaskInputContext? InputContext { get; set; }
        public PlanningEndpoints.GridResult? InputGrid { get; set; }
        public WorkloadReportRead? InputWorkload { get; set; }
        public List<TimeEndpoints.Row>? InputTime { get; set; }
        public ExactUnitSource? UnitSource { get; set; }
        public (string Suffix, string Label)[] UnitMeasures { get; set; } = [];
        public int UnitUnestimated { get; set; }
        public TimeProvider ReadClock { get; } = new ReportReadClock(now);
        public string? Str(string k) => string.IsNullOrWhiteSpace(q[k]) ? null : q[k].ToString();
        public Guid? Id(string k) => Guid.TryParse(q[k], out var g) ? g : null;
        public Guid[] Ids(string k) => Http.Ids(q[k]);
        public int? Int(string k) => int.TryParse(q[k], out var i) ? i : null;
        public DateOnly? Date(string k) => DateOnly.TryParse(q[k], CultureInfo.InvariantCulture, out var d) ? d : null;
        public bool Bool(string k) => q[k] == "true";

        /// Projects in scope: the chosen ones the caller may view, or every visible project that is not Archived or Cancelled.
        public IQueryable<Project> Projects()
        {
            var visible = Scope();
            var chosen = Ids("projectId");
            return chosen.Length > 0 ? visible.Where(p => chosen.Contains(p.Id)) : visible.Where(p => Live.Contains(p.Status));
        }

        public IQueryable<Guid> ProjectIds() => Projects().Select(p => p.Id);

        /// The one project a report is scoped to, when exactly one was chosen, for "Open as filtered list".
        public async Task<(Guid Id, string Number)?> Single()
        {
            var chosen = Ids("projectId");
            if (chosen.Length != 1) return null;
            var p = await Projects().Select(p => new ProjectRef(p.Id, p.ProjectNumber)).FirstOrDefaultAsync();
            return p is null ? null : (p.Id, p.ProjectNumber);
        }

        public async Task<Guid?> SingleProjectId() => (await Single())?.Id;

        public async Task<List<(string, string)>> Describe(Param[] ps, bool integrity = false)
        {
            var list = new List<(string, string)>();
            foreach (var p in ps)
            {
                var raw = Str(p.Key) ?? p.Default;
                if (raw is null || raw == "false" && p.Key != "includeMyDrafts") continue;
                var ids = Http.Ids(raw);
                var value = p.Type switch
                {
                    // Only projects the caller may view are named; people and reference data are organisation-wide already.
                    "projects" or "project" => string.Join(", ", await Scope().Where(x => ids.Contains(x.Id)).Select(x => x.ProjectNumber).ToListAsync()),
                    "discipline" => string.Join(", ", await db.Disciplines.Where(x => ids.Contains(x.Id)).Select(x => x.Name).ToListAsync()),
                    "office" => string.Join(", ", await db.Offices.Where(x => ids.Contains(x.Id)).Select(x => x.Name).ToListAsync()),
                    "person" => string.Join(", ", await db.Users.Where(x => ids.Contains(x.Id)).Select(x => x.DisplayName).ToListAsync()),
                    "bool" => Text.Get(raw == "false" ? "common.no" : "common.yes"),
                    "select" when p.Key == "unitId" && Guid.TryParse(raw, out var unit) => await db.Set<OrgUnit>().Where(u => u.Id == unit).Select(u => u.Name).FirstOrDefaultAsync() ?? raw,
                    "select" => Text.Get($"opt.{raw}"),
                    _ => raw,
                };
                list.Add((Text.Get($"param.{p.Key}"), value));
            }
            if (list.All(x => x.Item1 != Text.Get("param.projectId")) && ps.Any(p => p.Type == "projects")) list.Insert(0, (Text.Get("param.projectId"), Text.Get(MemberProjectsOnly ? "report.allMember" : "report.allVisible")));
            if (Partial) list.Add((Text.Get("planning.partial"), Text.Get("planning.partial_hint")));
            if (integrity && s.IsFlagOn("verifiable_log"))
            {
                var projects = await Projects().Select(p => new ProjectRef(p.Id, p.ProjectNumber)).ToListAsync();
                var ids = projects.Select(p => p.Id).ToArray();
                var heads = await db.Set<ActivityChainHead>().AsNoTracking().Where(h => ids.Contains(h.ChainId)).ToDictionaryAsync(h => h.ChainId);
                foreach (var project in projects)
                {
                    var h = heads.GetValueOrDefault(project.Id);
                    var status = h?.FirstBrokenSeq is { } broken ? Text.Get("audit.log_failed", broken)
                        : h?.LastVerifiedAt is { } at && h.LastVerifiedSeq == h.HeadSeq ? Text.Get("audit.log_verified", h.HeadSeq, Hub.Api.Infrastructure.Clock.Local(at, s).ToString("g"))
                        : Text.Get("audit.log_pending");
                    list.Add(($"{Text.Get("audit.log_integrity")} · {project.ProjectNumber}", status));
                }
            }
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
        var rows = Json(await TaskQueries.Rows(c.Db, TaskQueries.Sort(c.InputTasks!, null).Take(Cap), c.Me.Actor));
        foreach (var r in rows) r!["indicators"] = Indicators(r["state"]);
        return new(rows, await Link(c, "tasks", ("dueFrom", $"{from:yyyy-MM-dd}"), ("dueTo", $"{to:yyyy-MM-dd}"), ("open", "true"), ("assigneeId", c.Str("assigneeId")), ("mine", c.Bool("mine") ? "true" : null)));
    }

    static async Task<Result> Overdue(Ctx c)
    {
        var min = Math.Max(c.Int("minDaysOverdue") ?? 1, 1);
        var q = c.InputTasks!;
        var rows = await WithMilestones(c.Db, Json(await TaskQueries.Rows(c.Db, q.OrderBy(t => t.DueDate).ThenBy(t => t.Seq).Take(Cap), c.Me.Actor)));
        return new(rows, min > 1 ? null : await Link(c, "tasks", ("overdue", "true"), ("assigneeId", c.Str("assigneeId"))));
    }

    static async Task<Result> Blocked(Ctx c)
    {
        var kind = c.Str("blockerType");
        var q = c.InputTasks!;
        var rows = await WithMilestones(c.Db, Json(await TaskQueries.Rows(c.Db, q.OrderBy(t => t.DueDate).ThenBy(t => t.Seq).Take(Cap), c.Me.Actor)));
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
        var rows = Json(await TaskQueries.Rows(c.Db, c.InputTasks!.OrderBy(t => t.DueDate).ThenBy(t => t.Seq).Take(Cap), c.Me.Actor));
        var succIds = rows.SelectMany(r => GuidList(r!["state"]?["blockingTaskIds"])).Distinct().ToList();
        var succ = await c.Db.Tasks.AsNoTracking().Where(t => succIds.Contains(t.Id))
            .Select(t => new SuccessorMilestone(t.Id, t.MilestoneId ?? c.Db.Deliverables.Where(d => d.Id == t.DeliverableId).Select(d => d.MilestoneId).FirstOrDefault())).ToListAsync();
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
        var rows = await WithProject(c.Db, Json(await DeliverableEndpoints.Rows(c.Db, q.OrderBy(d => d.DueDate).ThenBy(d => d.Seq).Take(Cap))));
        foreach (var r in rows) r!["indicators"] = Indicators(r["state"]);
        if (c.S.IsFlagOn("baseline_history")) await BaselineHistory.AddDeltas(c.Db, rows);
        return new(rows, await Link(c, "deliverables", ("dueFrom", $"{c.Today:yyyy-MM-dd}"), ("dueTo", $"{c.Today.AddDays(days):yyyy-MM-dd}"), ("indicator", "open")),
            Columns: c.S.IsFlagOn("baseline_history") ? [.. All.Single(r => r.Code == "upcoming-deliverables").Cols, .. BaselineHistory.DeltaColumns] : null);
    }

    static async Task<Result> DeliverableStatus_(Ctx c)
    {
        var q = ByDiscipline(c, c.Db.Deliverables.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId) && d.Status != DeliverableStatus.Cancelled));
        var rows = await WithProject(c.Db, Json(await DeliverableEndpoints.Rows(c.Db, q.OrderBy(d => c.Db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First()).ThenBy(d => d.Key).Take(Cap))));
        var sorted = new JsonArray([.. rows.Select(r => r!.DeepClone()).OrderBy(r => r["projectNumber"]?.GetValue<string>()).ThenBy(r => r["key"]?.GetValue<string>())]);
        if (c.S.IsFlagOn("baseline_history")) await BaselineHistory.AddDeltas(c.Db, sorted);
        return new(sorted, await Link(c, "deliverables"), Columns: c.S.IsFlagOn("baseline_history") ? [.. All.Single(r => r.Code == "deliverable-status-by-project").Cols, .. BaselineHistory.DeltaColumns] : null);
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
        var rows = await WithProject(c.Db, Json(await MilestoneEndpoints.Rows(c.Db, q.OrderBy(m => m.Date).ThenBy(m => m.Id).Take(Cap), c.S)));
        foreach (var r in rows) r!["deliverablesIssued"] = $"{r["deliverableIssued"]}/{r["deliverableTotal"]}";
        return new(rows, await Link(c, "milestones", ("type", c.Str("type"))), c.S.IsFlagOn("schedule_baseline") ? [.. All.Single(d => d.Code == "upcoming-milestones").Cols, new("sinceBaselineDays", "sinceBaseline", "number")] : null);
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
        var rows = await WithProject(c.Db, Json(await DecisionEndpoints.Rows(c.Db, q.OrderByDescending(d => c.Db.DecisionStates.Any(st => st.DecisionId == d.Id && st.IsOverdue)).ThenBy(d => d.RequiredByDate)
            .ThenBy(d => d.ImpactLevel == Impact.High ? 0 : d.ImpactLevel == Impact.Medium ? 1 : 2).ThenBy(d => d.Key).Take(Cap), c.Today)));
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
        foreach (var i in Json(await RegisterEndpoints.IssueRows(c.Db, issues.OrderBy(i => i.Severity == Impact.High ? 0 : i.Severity == Impact.Medium ? 1 : 2)
            .ThenBy(i => i.TargetResolutionDate ?? DateOnly.MaxValue).ThenBy(i => i.Seq).Take(Cap), c.Today)))
        {
            i!["itemType"] = ItemType.Issue; i["itemTypeLabel"] = Text.Get("itemType.Issue"); i["date"] = i["targetResolutionDate"]?.DeepClone();
            rows.Add((Registers.Weight(i["severity"]!.GetValue<string>()), i["targetResolutionDate"]?.GetValue<string>(), i));
        }
        foreach (var r in Json(await RegisterEndpoints.RiskRows(c.Db, risks.OrderBy(r => r.Probability * r.Impact >= 6 ? 0 : r.Probability * r.Impact >= 3 ? 1 : 2)
            .ThenBy(r => r.ReviewDate ?? DateOnly.MaxValue).ThenByDescending(r => r.Probability * r.Impact).ThenBy(r => r.Seq).Take(Cap), c.Today)))
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
        return new(Json(await MeetingEndpoints.Rows(c.Db, q.OrderBy(a => a.DueDate ?? DateOnly.MaxValue).ThenBy(a => a.Seq).Take(Cap), c.Today)), await Link(c, "meetings", ("indicator", "open"), ("ownerType", c.Str("ownerType"))));
    }

    static async Task<Result> ReviewQueue(Ctx c)
    {
        var reviewer = c.Id("reviewerId");
        var tasks = Json(await TaskQueries.Rows(c.Db, c.InputTasks!.OrderBy(t => t.ReviewRequestedAt == null && t.StatusChangedAt == null)
            .ThenBy(t => t.ReviewRequestedAt ?? t.StatusChangedAt).ThenBy(t => t.Key).Take(Cap), c.Me.Actor));
        var taskIds = tasks.Select(x => Guid.Parse(x!["id"]!.GetValue<string>())).ToList();
        var requested = await c.Db.Tasks.AsNoTracking().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.ReviewRequestedAt ?? t.StatusChangedAt);
        var dq = c.Db.Deliverables.AsNoTracking().Where(d => c.ProjectIds().Contains(d.ProjectId) && d.Status == DeliverableStatus.InReview);
        if (reviewer is { } rv) dq = dq.Where(d => d.ReviewerId == rv);
        dq = dq.OrderBy(d => d.StatusChangedAt == null).ThenBy(d => d.StatusChangedAt).ThenBy(d => d.Key).Take(Cap);
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
            if (c.S.IsFlagOn("people_screens"))
            {
                rows[rows.Count - 1]!["assigneeId"] = t["assigneeId"]?.DeepClone();
                rows[rows.Count - 1]!["reviewerId"] = t["reviewerId"]?.DeepClone();
            }
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
            if (c.S.IsFlagOn("people_screens"))
            {
                rows[rows.Count - 1]!["assigneeId"] = d["ownerId"]?.DeepClone();
                rows[rows.Count - 1]!["reviewerId"] = d["reviewerId"]?.DeepClone();
            }
        }
        var ordered = new JsonArray([.. rows.Select(r => r!.DeepClone()).OrderByDescending(r => r["daysWaiting"]?.GetValue<int?>() ?? -1)]);
        return new(ordered, dels.Count == 0 ? await Link(c, "tasks", ("readyForReview", "true"), ("reviewerId", c.Str("reviewerId"))) : null);
    }

    static async Task<Result> Stale(Ctx c)
    {
        var days = Math.Max(c.Int("days") ?? c.S.TaskStaleDays, 1);
        var q = c.InputTasks!;
        var rows = Json(await TaskQueries.Rows(c.Db, q.OrderBy(t => t.LastActivityAt).Take(Cap), c.Me.Actor));
        foreach (var r in rows)
            r!["staleDays"] = c.Today.DayNumber - Clock.LocalDate(DateTimeOffset.Parse(r["lastActivityAt"]!.GetValue<string>(), CultureInfo.InvariantCulture), c.S).DayNumber;
        return new(rows, days == c.S.TaskStaleDays ? await Link(c, "tasks", ("stale", "true")) : null);
    }

    static async Task<Result> ActivityLog_(Ctx c)
    {
        var id = c.Id("projectId") ?? throw ApiException.Invalid("projectId", "error.required");
        var (p, pc) = await c.Access.Project(id, track: false);
        if (c.MemberProjectsOnly) Access.Demand(Permissions.ExportReportProject(c.Me.Actor, pc));
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
        var (weeks, people, _) = await ReadWorkloadInputs(c);
        var (cols, rows) = WorkloadEndpoints.EmployeeRows(weeks, people);
        if (c.S.IsFlagOn("people_screens")) for (var i = 0; i < people.Count; i++)
        {
            rows[i]!["id"] = people[i].Id;
            rows[i]!["personId"] = people[i].Id;
            rows[i]!["supervisorId"] = people[i].SupervisorId;
        }
        CaptureEmployeeUnits(c, weeks, people, cols);
        return new(rows, null, cols);
    }

    /// §19 Task Hours (§36.8): the Time view's permitted, non-deleted entries and their total.
    static async Task<Result> TaskHours(Ctx c)
    {
        var (from, to, rows) = await ReadRecordedInputs(c);
        var json = Json(rows.Select(r => new TaskHoursRow(r.WorkDate, r.Person, r.ProjectNumber, r.TaskKey, r.TaskName, r.Hours, r.Note, ItemType.Task, r.TaskId,
            c.S.IsFlagOn("people_screens") ? r.UserId : null, c.S.IsFlagOn("people_screens") ? r.Id : null)));
        json.Add(new JsonObject { ["person"] = Text.Get("time.total"), ["hours"] = rows.Sum(r => r.Hours) });
        var qs = string.Join("&", new[] { ("from", from.ToString("yyyy-MM-dd")), ("to", to.ToString("yyyy-MM-dd")), ("projectId", c.Str("projectId")), ("userId", c.Str("userId")), ("scope", c.Str("scope") == "team" ? "all" : null) }
            .Where(x => x.Item2 is not null).Select(x => $"{x.Item1}={x.Item2}"));
        return new(json, $"/time?{qs}");
    }

    static async Task<Result> WorkloadDiscipline(Ctx c)
    {
        if (!c.InputsEnabled)
        {
            var (cols, rows) = await WorkloadEndpoints.DisciplineRows(c.Id("officeId"), c.Access, c.Db, c.Me, c.Store, c.Clock);
            return new(rows, null, cols);
        }
        var read = await WorkloadEndpoints.ReadDisciplineReport(c.Id("officeId"), c.Access, c.Db, c.Me, c.Store, c.ReadClock);
        await SetDisciplineInputs(c);
        c.UnitMeasures = [("", Text.Get("reportUnits.estimatedHours"))];
        c.UnitSource = new(read.Weeks.ToArray(), read.Columns, WithUnitTotal(read.ExactHours.Select((hours, index) => new ExactUnitRow(index, "Hours", "VisibleOnly",
            [UnitMeasure("", c.UnitMeasures[0].Label, hours, read.Rows[index]!["unestimated"]!.GetValue<int>())])).ToArray()));
        return new(read.Rows, null, read.Columns);
    }

    /// §19 Health History from the nightly snapshots (§16.5).
    static async Task<Result> HealthHistory(Ctx c)
    {
        var rangeFrom = c.Date("from") ?? c.Today.AddDays(-56);
        var to = c.Date("to") ?? c.Today;
        var rows = await (from snapshot in c.Db.HealthSnapshots.AsNoTracking()
            where c.ProjectIds().Contains(snapshot.ProjectId) && snapshot.SnapshotDate >= rangeFrom && snapshot.SnapshotDate <= to
            join project in c.Db.Projects on snapshot.ProjectId equals project.Id
            orderby project.ProjectNumber, snapshot.SnapshotDate
            select new HealthSnapshotRow(project.ProjectNumber, project.Name, snapshot.SnapshotDate, snapshot.ComputedHealth, snapshot.ReportedHealth))
            .Take(Cap).ToListAsync();
        return new(Json(rows.Select(x => new HealthRow(x.ProjectNumber, x.Name, x.Date, x.Computed, x.Reported))));
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
        var snaps = await c.Db.HealthSnapshots.AsNoTracking().Where(x => pids.Contains(x.ProjectId) && x.SnapshotDate >= first).Select(x => new ReportSnapshotSource(x.ProjectId, x.SnapshotDate, x.Counts)).ToListAsync();
        var log = c.Db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && pids.Contains(a.ProjectId.Value) && a.OccurredAt >= since);
        var reviews = await log.Where(a => a.Action == "CoordinationReviewed").Select(a => new ReviewMarker(a.ProjectId, a.OccurredAt)).ToListAsync();
        var changes = await log.Where(a => a.ItemType == ItemType.Task && a.Action == "StatusChanged" && a.ActorUserId != null).Select(a => new StatusChangeMarker(a.ActorUserId!.Value, a.OccurredAt)).ToListAsync();
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
            rows.Add(new PilotWeekRow(w, pids.Count, reviews.Where(x => x.OccurredAt >= from && x.OccurredAt < to).Select(x => x.ProjectId).Distinct().Count(),
                Sum("tasksUnassigned"), Sum("tasksNoDueDate"), Sum("tasksBlocked"), people.Count, updaters, people.Count == 0 ? null : updaters * 100 / people.Count));
        }
        return new(Json(rows));
    }

    /// FR-ASG-08: Supervisors see their direct reports; Executives and Admins may ask for all staff.
    static async Task<Result> PickupOffers(Ctx c)
    {
        var from = c.Date("from") ?? c.Today.AddDays(-42); var to = c.Date("to") ?? c.Today;
        Check.That(to >= from, "to", "error.invalid");
        // G-03: the range is organisation-time days, like the default "today", so an evening offer is in today's report.
        var zone = Clock.Zone(c.S);
        DateTimeOffset Utc(DateOnly d) { var t = d.ToDateTime(TimeOnly.MinValue); return new DateTimeOffset(t, zone.GetUtcOffset(t)).ToUniversalTime(); }
        var (start, end) = (Utc(from), Utc(to.AddDays(1)));
        var found = await (from offer in TaskPickupReporting.Rows(c.Db, c.Access.VisibleProjectIds())
            where offer.OpenedAt >= start && offer.OpenedAt < end && offer.AssigneeId != null
            join task in c.Db.Tasks.IgnoreQueryFilters() on offer.TaskId equals task.Id
            where c.ProjectIds().Contains(task.ProjectId) && (c.Id("disciplineId") == null || task.ProjectDisciplineId == c.Id("disciplineId"))
            orderby offer.OpenedAt
            select new PickupProjection(offer.AssigneeId!.Value, task.ProjectId, task.ProjectDisciplineId,
                c.Db.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.ProjectNumber).First(),
                c.Db.ProjectDisciplines.Where(d => d.Id == task.ProjectDisciplineId).Select(d => d.Discipline!.Name).First(),
                offer.OpenedAt, offer.ClosedAt, offer.CloseCause)).ToListAsync();
        // Filter through the list's visibility rule before the cap; limiting first can omit eligible offers.
        var viewer = await c.Db.Users.AsNoTracking().SingleAsync(u => u.Id == c.Me.Id);
        var rows = (await TaskPickupReporting.Visible(c.Db, c.Access, viewer, c.S.IsFlagOn("org_structure"), found,
            r => (r.Assignee, r.ProjectId, r.ProjectDisciplineId))).Take(Cap).ToList();
        return new(Json(rows.Select(r => new PickupRow(r.Project, r.Discipline, r.Offered, r.Closed, TaskPickupRules.CauseText(r.CloseCause),
            (int)((r.Closed ?? c.Now) - r.Offered).TotalDays))), "/pickup");
    }

    static async Task<Result> CommitmentMisses(Ctx c)
    {
        var from = c.Date("from") ?? c.Today.AddDays(-42); var to = c.Date("to") ?? c.Today;
        var commitments = await c.Db.OutputCommitments.AsNoTracking().Where(o => c.ProjectIds().Contains(o.ProjectId) && o.State == CommitmentState.NotMet && o.TargetDate >= from && o.TargetDate <= to)
            .ToListAsync();
        var ids = commitments.Select(o => o.Id).ToArray();
        var events = await c.Db.OutputCommitmentEvents.AsNoTracking().Where(e => ids.Contains(e.CommitmentId) && e.ToState == CommitmentState.NotMet)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).ToListAsync();
        var categories = await c.Db.Set<CommitmentMissCategory>().ToDictionaryAsync(x => x.Id, x => x.Name);
        var projects = await c.Db.Projects.Where(p => c.ProjectIds().Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProjectNumber);
        var targetIds = commitments.Select(o => o.TargetId).ToArray();
        var tasks = await c.Db.Tasks.IgnoreQueryFilters().Where(t => targetIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.ProjectDisciplineId);
        var deliverables = await c.Db.Deliverables.IgnoreQueryFilters().Where(d => targetIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.ProjectDisciplineId);
        var disciplines = await c.Db.ProjectDisciplines.Include(d => d.Discipline).ToDictionaryAsync(d => d.Id, d => d.Discipline!.Name);
        var data = commitments.Select(o => new CommitmentDimension(o.ProjectId, o.TargetType == "Task" ? tasks.GetValueOrDefault(o.TargetId) : deliverables.GetValueOrDefault(o.TargetId),
            events.FirstOrDefault(e => e.CommitmentId == o.Id)?.MissCategoryId)).Where(o => c.Id("disciplineId") == null || o.DisciplineId == c.Id("disciplineId"));
        return new(Json(data.GroupBy(o => new CommitmentGroupKey(o.ProjectId, o.DisciplineId, o.CategoryId)).Select(g => new MissReportRow(projects.GetValueOrDefault(g.Key.ProjectId),
            disciplines.GetValueOrDefault(g.Key.DisciplineId), g.Key.CategoryId is { } category ? categories.GetValueOrDefault(category) : Text.Get("report.notRecorded"), g.Count()))));
    }

    static async Task<Result> StaffAssignments(Ctx c)
    {
        var a = c.Me.Actor;
        var all = c.Str("scope") == "all";
        if (all && !Permissions.ViewAllStaff(a)) throw ApiException.Forbidden("perm.all_staff");
        var people = c.Db.Users.AsNoTracking().Where(u => u.IsActive && (all || u.SupervisorId == c.Me.Id));
        if (!all) people = await PeopleScope.Widen(c.Db, a, people);
        if (c.Id("officeId") is { } office) people = people.Where(u => u.OfficeId == office);
        var projects = c.Scope().Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        if (c.Ids("projectId") is { Length: > 0 } chosen) projects = projects.Where(p => chosen.Contains(p.Id));
        var members = c.Db.ProjectMembers.AsNoTracking().Where(m => m.RemovedAt == null && people.Any(u => u.Id == m.UserId) && projects.Any(p => p.Id == m.ProjectId));
        if (c.Str("role") is { } role) members = members.Where(m => m.Roles.Contains(role));
        if (c.Id("disciplineId") is { } d) members = members.Where(m => c.Db.ProjectDisciplines.Any(pd => pd.Id == m.PrimaryDisciplineId && pd.DisciplineId == d));
        var rows = await members.OrderBy(m => c.Db.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First())
            .ThenBy(m => c.Db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).First()).Take(Cap)
            .Select(m => new StaffAssignmentSource(m.UserId, m.ProjectId, m.Roles, m.AddedAt,
                c.Db.Users.Where(u => u.Id == m.UserId).Select(u => u.DisplayName).First(),
                c.Db.Projects.Where(p => p.Id == m.ProjectId).Select(p => new StaffProject(p.ProjectNumber, p.Name, p.Status)).First(),
                c.Db.ProjectDisciplines.Where(pd => pd.Id == m.PrimaryDisciplineId).Select(pd => pd.Discipline!.Name).FirstOrDefault(),
                c.Db.Users.Where(u => u.Id == m.AddedBy).Select(u => u.DisplayName).FirstOrDefault(),
                c.Db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == m.UserId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                c.Db.Tasks.Count(t => t.ProjectId == m.ProjectId && t.AssigneeId == m.UserId && c.Db.TaskStates.Any(s => s.TaskId == t.Id && s.IsOverdue)), m.Id, m.AddedBy))
            .ToListAsync();
        var data = Json(rows.OrderBy(r => r.Person).ThenBy(r => r.Project.ProjectNumber).Select(r => new StaffRow(r.Person, r.Project.ProjectNumber, r.Project.Name, r.Project.Status,
            string.Join(", ", r.Roles.Select(x => Text.Get($"role.{x}"))), r.PrimaryDiscipline, Clock.LocalDate(r.AddedAt, c.S), r.AddedBy, r.OpenTasks, r.OverdueTasks,
            c.S.IsFlagOn("people_screens") ? r.UserId : null, c.S.IsFlagOn("people_screens") ? r.AddedById : null, c.S.IsFlagOn("people_screens") ? r.MemberId : null)));
        if (!c.S.IsFlagOn("org_structure")) return new(data);
        var ids = rows.Select(r => r.UserId).Distinct().ToArray();
        var week = c.Date("week");
        var end = week is { } date ? new DateTimeOffset(Hub.Domain.Workload.WeekOf(date).AddDays(7).ToDateTime(TimeOnly.MinValue), Clock.Zone(c.S).GetUtcOffset(Hub.Domain.Workload.WeekOf(date).AddDays(7).ToDateTime(TimeOnly.MinValue))).ToUniversalTime() : c.Now.AddTicks(1);
        var placements = await OrgStructureReports.At(c.Db, ids, end);
        var units = await c.Db.Set<OrgUnit>().AsNoTracking().ToDictionaryAsync(u => u.Id);
        HashSet<Guid>? chosenUnits = null;
        if (c.Id("unitId") is { } unit)
            chosenUnits = OrgTree.Descendants(unit, units.Values.Select(u => new OrgTree.Unit(u.Id, u.ParentId, 0, u.LeaderId))).ToHashSet();
        var filtered = new JsonArray();
        for (var i = 0; i < rows.Count; i++)
        {
            var placement = placements.GetValueOrDefault(rows[i].UserId);
            if (chosenUnits != null && (placement == null || !chosenUnits.Contains(placement.Value))) continue;
            var row = data[i]!.DeepClone(); row["unit"] = placement is { } id && units.TryGetValue(id, out var value) ? value.Name : Text.Get("report.noUnit");
            row["unitPath"] = OrgStructureReports.UnitPath(placement, units); filtered.Add(row);
        }
        var columns = All.Single(d => d.Code == "staff-assignments").Cols.Concat(new[] { new Col("unit", "unit"), new Col("unitPath", "unitPath") }).ToArray();
        return new(filtered, Columns: columns);

    }
}

/// Sends list and report rows as CSV (UTF-8 with BOM) or XLSX (header, frozen pane, typed dates, Parameters sheet), and
/// records who exported what, never the contents (§19, §20.1, FR-010). `header` lines open the CSV and join the Parameters
/// sheet without being logged, for text that is content rather than a filter (the Admin review's reviewer note, §55.4).
public static class ExportFile
{
    public static async Task<IResult> Send(HubDb db, SettingsStore store, string? format, string title, IReadOnlyList<Col> cols, JsonArray rows, IReadOnlyList<(string, string)> parameters,
        Guid? projectId, string key, TimeProvider clock, IReadOnlyList<(string, string)>? header = null)
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
            ? Results.File(Export.ToXlsx(title, items, columns, [.. parameters, .. header ?? [], (Text.Get("export.generated"), $"{now:yyyy-MM-dd HH:mm} ({s.OrgTimeZone})")]),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file)
            : Results.File(Export.ToCsv(items, columns, header), "text/csv; charset=utf-8", file);
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
