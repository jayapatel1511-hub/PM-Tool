using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Remaining S23-B forms, separate from the held six task-row report suite. Private disposable data and normal commands.
public sealed class ReportCapacityTaskInputsTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public ReportCapacityTaskInputsTests() { d = new(f); f.Clock.SetDate(2026, 9, 14); }
    public void Dispose() => f.Dispose();
    async Task Flag(string key, bool on = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(string code, string query = "", string who = TestData.Sam, int status = 200) =>
        await (await f.As(who).GetAsync($"/api/v1/reports/{code}{(query.Length > 0 ? "?" + query : "")}")).Json(status);
    static string Query(JsonNode summary) => string.Join("&", summary["parameters"]!.AsObject().Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!.GetValue<string>())}"));
    Task<JsonNode> List(string code, string metric, string query, string who = TestData.Sam, int page = 1, int size = 50, int status = 200) =>
        Get($"{code}/task-inputs/{metric}", $"{query}{(query.Length > 0 ? "&" : "")}page={page}&pageSize={size}", who, status);
    static Guid[] Ids(JsonNode page) => page["items"]!.AsArray().Select(row => row!.G("id")).ToArray();
    static JsonNode Counts(JsonNode report, int unestimated, int undated, string basis)
    {
        var summary = report["taskInputs"]!;
        Assert.Equal((unestimated, unestimated, undated, undated, 0), (summary["unestimated"]!.I("count"), summary["unestimated"]!.I("knownCount"),
            summary["openUndated"]!.I("count"), summary["openUndated"]!.I("knownCount"), summary.I("unavailableReferences")));
        Assert.Equal(basis, summary.S("basis")); Assert.True(JsonNode.DeepEquals(report["savedParameters"], summary["parameters"]));
        foreach (var metric in new[] { "unestimated", "openUndated" }) { Assert.DoesNotContain("ids=", summary[metric]!.S("href")); Assert.DoesNotContain("personIds=", summary[metric]!.S("href")); }
        return summary;
    }
    async Task<(Guid Id, string Email)> Person(string supervisor = TestData.Sam, Guid? office = null)
    {
        var email = $"report-input-{Guid.NewGuid():N}@hub.test";
        var me = await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var version = await f.DbAsync(db => db.Users.Where(user => user.Id == me.G("id")).Select(user => user.RowVersion).SingleAsync());
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{me.G("id")}", new { supervisorId = d.User(supervisor), officeId = office }, version)).Json();
        return (me.G("id"), email);
    }
    async Task<JsonNode> Entry(string who, JsonNode task, string day, decimal hours = 1m) =>
        await (await f.As(who).Post("/api/v1/time", new { taskId = task.G("id"), workDate = day, hours })).Json(201);
    async Task<string> Durable() => await f.DbAsync(async db => JsonSerializer.Serialize(new {
        Tasks = await db.Tasks.IgnoreQueryFilters().OrderBy(task => task.Id).Select(task => new { task.Id, task.RowVersion, task.DeletedAt, task.Status, task.EstimatedHours, task.DueDate }).ToArrayAsync(),
        Time = await db.TimeEntries.IgnoreQueryFilters().OrderBy(entry => entry.Id).Select(entry => new { entry.Id, entry.RowVersion, entry.DeletedAt, entry.Hours }).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Notifications = await db.Notifications.CountAsync(), Planning = await db.PlanningEntries.CountAsync(), Commands = await db.CoordinationCommands.CountAsync(),
    }));

    [Fact]
    public async Task Civil_employee_scope_has_four_unestimated_two_undated_and_keeps_the_selected_peoples_other_visible_work()
    {
        var p = await d.Project(); var other = await d.Project(); var alice = await Person(); var bob = await Person(); var civil = await d.Discipline("Civil");
        var expected = new List<Guid>();
        foreach (var due in new string?[] { "2026-09-16", "2026-09-17", null })
            expected.Add((await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id, dueDate = due })).G("id"));
        var cross = await d.NewTask(other.Id, TestData.Pm, new { assigneeId = alice.Id }, "Electrical"); expected.Add(cross.G("id"));
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id, estimatedHours = 0m, dueDate = "2026-09-18" });
        var held = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }); await d.Move(TestData.Pm, held, TaskStatuses.OnHold, new { reason = "Synthetic held work is excluded by the canonical Workload source" });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = bob.Id }, "Electrical"); await d.NewTask(p.Id);
        await Flag("capacity_screens"); var before = await Durable();
        var report = await Get("workload-employee", $"disciplineId={civil}"); var summary = Counts(report, 4, 2, "WorkloadPeopleContext");
        Assert.Single(report["rows"]!.AsArray()); Assert.Equal(4, report["rows"]![0]!.I("unestimated"));
        var first = await List("workload-employee", "unestimated", Query(summary), page: 1, size: 2);
        var second = await List("workload-employee", "unestimated", Query(summary), page: 2, size: 2);
        Assert.Equal(expected.Order(), Ids(first).Concat(Ids(second)).Order()); Assert.Equal((4, 4), (first.I("total"), second.I("knownTotal")));
        Assert.Equal(2, (await List("workload-employee", "open-undated", Query(summary))).I("total")); Assert.Equal(before, await Durable());
        Counts(await Get("workload-employee", $"disciplineId={civil}&projectId={p.Id}"), 3, 1, "WorkloadPeopleContext");
        Counts(await Get("workload-employee", $"disciplineId={civil}&projectId={Guid.NewGuid()}"), 0, 0, "WorkloadPeopleContext");
    }

    [Fact]
    public async Task Discipline_report_retains_office_people_scope_and_canonical_project_and_status_exclusions()
    {
        var p = await d.Project(); var office = await d.Office(); var alice = await Person(office: office); var bob = await Person();
        var a = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id });
        var b = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id, dueDate = "2026-09-16" }, "Electrical");
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = bob.Id });
        var held = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }); await d.Move(TestData.Pm, held, TaskStatuses.OnHold, new { reason = "Synthetic hold" });
        var completed = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }); await d.Move(TestData.Pm, completed, TaskStatuses.InProgress); await d.Move(TestData.Pm, completed, TaskStatuses.Complete);
        var cancelled = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }); await d.Move(TestData.Pm, cancelled, TaskStatuses.Cancelled, new { reason = "Synthetic cancellation" });
        var paused = await d.Project(); await d.NewTask(paused.Id, TestData.Pm, new { assigneeId = alice.Id });
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{paused.Id}/transition", new { toStatus = ProjectStatus.OnHold, reason = "Synthetic paused project", rowVersion = d.Version(paused.Id) })).Json();
        await Flag("capacity_screens"); var report = await Get("workload-discipline", $"officeId={office}"); var summary = Counts(report, 2, 1, "WorkloadPeopleContext");
        Assert.Equal(2, report["rows"]!.AsArray().Sum(row => row!.I("unestimated")));
        Assert.Equal(new[] { a.G("id"), b.G("id") }.Order(), Ids(await List("workload-discipline", "unestimated", Query(summary))).Order());
        Counts(await Get("workload-discipline", $"officeId={Guid.NewGuid()}"), 0, 0, "WorkloadPeopleContext");
    }

    async Task<Guid> Unit(string name, Guid? leader = null)
    {
        var levels = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/organisation/levels")).Json();
        var region = levels.AsArray().First(level => level!.S("name") == "Region")!.G("id");
        return (await (await f.As(TestData.Admin).Post("/api/v1/admin/organisation/units", new { name, levelId = region, leaderId = leader })).Json(201)).G("id");
    }
    async Task Place(Guid person, Guid unit) => await (await f.As(TestData.Admin).Patch($"/api/v1/admin/organisation/users/{person}/placement", new { unitId = unit },
        await f.DbAsync(db => db.Users.Where(user => user.Id == person).Select(user => user.RowVersion).SingleAsync()))).Json(204);

    [Fact]
    public async Task Team_capacity_uses_union_of_historical_displayed_unit_week_members_not_current_or_lookahead_placement()
    {
        await Flag("org_structure"); var unit = await Unit("Synthetic included region"); var outside = await Unit("Synthetic other region");
        var moving = await Person(); var later = await Person(); var stable = await Person(); var p = await d.Project();
        await Place(moving.Id, unit); await Place(later.Id, outside); await Place(stable.Id, unit);
        var movingTask = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = moving.Id }); var laterTask = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = later.Id });
        var stableTask = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = stable.Id });
        f.Clock.SetDate(2026, 9, 21); await Place(moving.Id, outside);
        f.Clock.SetDate(2026, 9, 28); await Place(later.Id, unit);
        f.Clock.SetDate(2026, 10, 5); await Flag("capacity_screens"); await Flag("capacity_reports");
        var report = await Get("team-capacity", $"supervisorId={d.User(TestData.Sam)}&unitId={unit}&from=2026-09-16&weeks=2&includeMyDrafts=false");
        var summary = Counts(report, 2, 2, "PlannerPeopleContext"); Assert.Equal(("2026-09-14", "2", unit.ToString()), (summary["parameters"]!.S("from"), summary["parameters"]!.S("weeks"), summary["parameters"]!.S("unitId")));
        Assert.Equal(new[] { movingTask.G("id"), stableTask.G("id") }.Order(), Ids(await List("team-capacity", "unestimated", Query(summary))).Order());
        var laterSummary = Counts(await Get("team-capacity", $"unitId={unit}&from=2026-09-28&weeks=1"), 2, 2, "PlannerPeopleContext");
        Assert.Equal(new[] { laterTask.G("id"), stableTask.G("id") }.Order(), Ids(await List("team-capacity", "unestimated", Query(laterSummary))).Order());
        Counts(await Get("team-capacity", $"unitId={Guid.NewGuid()}&from=2026-09-14&weeks=2"), 0, 0, "PlannerPeopleContext");
    }

    [Fact]
    public async Task Unit_leader_reads_the_existing_widened_cohort_without_a_new_role_or_permission_grant()
    {
        await Flag("org_structure"); var unit = await Unit("Synthetic leader region", d.User(TestData.Diane)); var alice = await Person(); await Place(alice.Id, unit);
        var p = await d.Project(); var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id });
        await Flag("capacity_screens"); await Flag("capacity_reports");
        foreach (var code in new[] { "workload-employee", "workload-discipline", "team-capacity" })
        {
            var report = await Get(code, code == "team-capacity" ? $"unitId={unit}&from=2026-09-14&weeks=1" : "", TestData.Diane);
            var summary = Counts(report, 1, 1, code == "team-capacity" ? "PlannerPeopleContext" : "WorkloadPeopleContext");
            Assert.Equal(new[] { task.G("id") }, Ids(await List(code, "unestimated", Query(summary), TestData.Diane)));
        }
    }

    [Theory]
    [InlineData("hours-by-source")]
    [InlineData("entries-without-project")]
    public async Task Both_source_forms_use_scoped_people_tasks_without_invented_entry_task_source_relationships(string form)
    {
        var p = await d.Project(); var alice = await Person(); var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }, "Electrical");
        await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = alice.Id, label = "Synthetic unlinked planning entry", hoursPerWeek = 4m,
            startWeek = "2026-09-14", endWeek = "2026-09-21", sourceCategory = PlanningSource.Admin, confidence = PlanningConfidence.Confirmed, visibility = PlanningVisibility.Published })).Json(201);
        await Flag("capacity_screens"); await Flag("capacity_reports");
        var query = $"supervisorId={d.User(TestData.Sam)}&from=2026-09-16&weeks=2&includeMyDrafts=false&show={form}&source={PlanningSource.Admin}&confidence={PlanningConfidence.Confirmed}";
        var summary = Counts(await Get("allocations-by-source", query), 1, 1, "PlannerPeopleContext");
        Assert.Equal((form, PlanningSource.Admin, PlanningConfidence.Confirmed), (summary["parameters"]!.S("show"), summary["parameters"]!.S("source"), summary["parameters"]!.S("confidence")));
        Assert.Equal(new[] { task.G("id") }, Ids(await List("allocations-by-source", "unestimated", Query(summary))));
        // Filtering the planning measure to a source with no entry never pretends to filter tasks by that source.
        Counts(await Get("allocations-by-source", query.Replace($"source={PlanningSource.Admin}", $"source={PlanningSource.Proposal}")), 1, 1, "PlannerPeopleContext");
    }

    [Fact]
    public async Task Capacity_discipline_and_own_draft_selection_use_the_returned_people_then_keep_their_other_work()
    {
        var p = await d.Project(); var alice = await Person(); var civil = await d.Discipline("Civil");
        var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id }, "Electrical");
        await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = alice.Id, projectId = p.Id, projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            label = "Synthetic private Civil planning context", hoursPerWeek = 4m, startWeek = "2026-09-14", endWeek = "2026-09-21", sourceCategory = PlanningSource.MajorProject,
            confidence = PlanningConfidence.Expected, visibility = PlanningVisibility.Draft })).Json(201);
        await Flag("capacity_screens"); await Flag("capacity_reports");
        foreach (var code in new[] { "team-capacity", "allocations-by-source" })
        {
            var query = $"disciplineId={civil}&from=2026-09-14&weeks=2";
            var summary = Counts(await Get(code, query + "&includeMyDrafts=true"), 1, 1, "PlannerPeopleContext");
            Assert.Equal(new[] { task.G("id") }, Ids(await List(code, "unestimated", Query(summary))));
            Counts(await Get(code, query + "&includeMyDrafts=false"), 0, 0, "PlannerPeopleContext");
        }
    }

    [Fact]
    public async Task Task_hours_deduplicates_permitted_entries_and_distinguishes_unavailable_current_references_from_zero()
    {
        var p = await d.Project(); var a = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var zero = await d.NewTask(p.Id, extra: new { estimatedHours = 0m });
        var complete = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) }); await d.Move(TestData.Alex, complete, TaskStatuses.InProgress); await d.Move(TestData.Alex, complete, TaskStatuses.Complete);
        var deleted = await d.NewTask(p.Id, extra: new { name = "DELETED CURRENT INPUT SENTINEL" });
        await Entry(TestData.Alex, a, "2026-09-14"); await Entry(TestData.Alex, a, "2026-09-15", 2m); await Entry(TestData.Alex, zero, "2026-09-15");
        await Entry(TestData.Alex, complete, "2026-09-15"); await Entry(TestData.Alex, deleted, "2026-09-15");
        await (await f.As(TestData.Pm).Delete($"/api/v1/tasks/{deleted.G("id")}", await d.TaskVersion(deleted))).Json();
        await Flag("capacity_screens"); var before = await Durable();
        var report = await Get("task-hours", $"projectId={p.Id}&from=2026-09-14&to=2026-09-20&scope=mine", TestData.Alex); var summary = report["taskInputs"]!;
        Assert.Equal(("CurrentRecordedTaskContext", "VisibleOnly", 1), (summary.S("basis"), summary.S("scopeCoverage"), summary.I("unavailableReferences")));
        Assert.Null(summary["unestimated"]!["count"]); Assert.Null(summary["openUndated"]!["count"]);
        Assert.Equal((2, 2), (summary["unestimated"]!.I("knownCount"), summary["openUndated"]!.I("knownCount")));
        Assert.Equal(6m, report["rows"]!.AsArray().Last()!["hours"]!.GetValue<decimal>());
        var unestimated = await List("task-hours", "unestimated", Query(summary), TestData.Alex); Assert.Null(unestimated["total"]); Assert.Equal(2, unestimated.I("knownTotal"));
        Assert.Equal(new[] { a.G("id"), complete.G("id") }.Order(), Ids(unestimated).Order()); Assert.Equal(1, unestimated.I("unavailableReferences"));
        Assert.DoesNotContain("DELETED CURRENT INPUT SENTINEL", unestimated.ToJsonString()); Assert.DoesNotContain(deleted.S("id"), unestimated.ToJsonString());
        Assert.Equal(new[] { a.G("id"), zero.G("id") }.Order(), Ids(await List("task-hours", "open-undated", Query(summary), TestData.Alex)).Order());
        var high = await List("task-hours", "unestimated", Query(summary), TestData.Alex, int.MaxValue, 200); Assert.Null(high["total"]); Assert.Equal(2, high.I("knownTotal")); Assert.Empty(Ids(high));
        Assert.Equal(before, await Durable());
    }

    [Fact]
    public async Task Task_hours_uses_mine_team_user_dates_and_each_entry_permission_before_reference_projection()
    {
        var p = await d.Project(); var alexTask = await d.NewTask(p.Id); var omarTask = await d.NewTask(p.Id, TestData.Pm, new { name = "NONREPORT TIME INPUT SENTINEL" });
        await Entry(TestData.Alex, alexTask, "2026-09-15"); await Entry(TestData.Omar, omarTask, "2026-09-15");
        await Flag("capacity_screens");
        var mine = Counts(await Get("task-hours", $"projectId={p.Id}", TestData.Alex), 1, 1, "CurrentRecordedTaskContext");
        Assert.Equal(("2026-09-14", "2026-09-20", "mine"), (mine["parameters"]!.S("from"), mine["parameters"]!.S("to"), mine["parameters"]!.S("scope")));
        Counts(await Get("task-hours", $"projectId={p.Id}&scope=mine&userId={d.User(TestData.Omar)}", TestData.Alex), 0, 0, "CurrentRecordedTaskContext");
        var excluded = Counts(await Get("task-hours", $"projectId={p.Id}&scope=team&userId={d.User(TestData.Omar)}"), 0, 0, "CurrentRecordedTaskContext");
        var empty = await List("task-hours", "unestimated", Query(excluded)); Assert.Empty(Ids(empty)); Assert.DoesNotContain("NONREPORT TIME INPUT SENTINEL", empty.ToJsonString());
        var permitted = Counts(await Get("task-hours", $"projectId={p.Id}&scope=team&userId={d.User(TestData.Omar)}", TestData.Pm), 1, 1, "CurrentRecordedTaskContext");
        Assert.Equal(new[] { omarTask.G("id") }, Ids(await List("task-hours", "unestimated", Query(permitted), TestData.Pm)));
        Counts(await Get("task-hours", $"projectId={p.Id}&from=2026-09-16&to=2026-09-20&scope=team", TestData.Pm), 0, 0, "CurrentRecordedTaskContext");
        Counts(await Get("task-hours", "scope=mine", TestData.Alex), 1, 1, "CurrentRecordedTaskContext");
    }

    [Fact]
    public async Task Hidden_current_projects_and_permission_revocation_do_not_leak_inputs_or_become_an_all_scope_fallback()
    {
        var p = await d.Project();
        // Project creation does not accept Visibility; establish the real restricted workflow explicitly.
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
        Assert.Equal(Visibility.Restricted, f.Db(db => db.Projects.Single(project => project.Id == p.Id).Visibility));
        var task = await d.NewTask(p.Id, extra: new { name = "RESTRICTED REPORT INPUT SENTINEL", assigneeId = d.User(TestData.Alex) });
        await Entry(TestData.Alex, task, "2026-09-15"); await Flag("capacity_screens");
        var before = Counts(await Get("task-hours", $"projectId={p.Id}&scope=mine", TestData.Alex), 1, 1, "CurrentRecordedTaskContext");
        var hidden = Counts(await Get("task-hours", $"projectId={p.Id}&scope=team"), 0, 0, "CurrentRecordedTaskContext");
        var page = await List("task-hours", "unestimated", Query(hidden)); Assert.Empty(Ids(page)); Assert.DoesNotContain(task.S("id"), page.ToJsonString()); Assert.DoesNotContain("RESTRICTED REPORT INPUT SENTINEL", page.ToJsonString());
        // Normal team removal revokes the restricted project before the copied report/list reference is re-read.
        var alex = d.User(TestData.Alex);
        var member = await f.DbAsync(db => db.ProjectMembers.Where(row => row.ProjectId == p.Id && row.UserId == alex && row.RemovedAt == null).Select(row => new { row.Id, row.RowVersion }).SingleAsync());
        await (await f.As(TestData.Pm).Delete($"/api/v1/projects/{p.Id}/members/{member.Id}?reason=Scoped%20report%20revocation", member.RowVersion)).Json();
        var after = await List("task-hours", "unestimated", Query(before), TestData.Alex); Assert.Equal(0, after.I("total")); Assert.Empty(Ids(after)); Assert.DoesNotContain("RESTRICTED REPORT INPUT SENTINEL", after.ToJsonString());
    }

    [Fact]
    public async Task People_context_counts_are_full_cohort_before_metric_paging_and_retained_report_group_rows()
    {
        var p = await d.Project(); var alice = await Person(); var expected = new List<Guid>();
        for (var i = 0; i < 205; i++) expected.Add((await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id })).G("id"));
        await Flag("capacity_screens"); await Flag("capacity_reports");
        // Input paging belongs only to the metric endpoint, never a report Run/export data parameter.
        foreach (var query in new[] { "page=1", "pageSize=50" })
        {
            await Get("team-capacity", query, status: 400);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync($"/api/v1/reports/team-capacity/export?{query}&format=csv")).StatusCode);
        }
        Assert.Equal(1, (await List("team-capacity", "unestimated", "", page: 1, size: 50)).I("page"));
        foreach (var code in new[] { "workload-employee", "workload-discipline", "team-capacity", "allocations-by-source" })
        {
            var query = code is "team-capacity" or "allocations-by-source" ? "from=2026-09-14&weeks=1" : "";
            var summary = Counts(await Get(code, query), 205, 205, code.StartsWith("workload-", StringComparison.Ordinal) ? "WorkloadPeopleContext" : "PlannerPeopleContext");
            var first = await List(code, "unestimated", Query(summary), size: 200); var last = await List(code, "unestimated", Query(summary), page: 2, size: 200);
            Assert.Equal((205, 205, 200, 5), (first.I("total"), last.I("knownTotal"), Ids(first).Length, Ids(last).Length));
            Assert.Equal(expected.Order(), Ids(first).Concat(Ids(last)).Order());
        }
    }

    // Parse the bounded synthetic CSV fixture independently, then reconcile every primary value and evidence cell.
    static void AssertMatrixExport(JsonNode view, JsonArray originalRows, byte[] bytes)
    {
        var records = System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => {
            var values = new List<string>(); var value = new System.Text.StringBuilder(); var quoted = false;
            for (var index = 0; index < line.Length; index++)
                if (line[index] == '"') { if (quoted && index + 1 < line.Length && line[index + 1] == '"') { value.Append('"'); index++; } else quoted = !quoted; }
                else if (line[index] == ',' && !quoted) { values.Add(value.ToString()); value.Clear(); } else value.Append(line[index]);
            values.Add(value.ToString()); return values.ToArray();
        }).ToArray();
        var columns = view["columns"]!.AsArray(); var unitRows = view["rows"]!.AsArray(); Assert.NotEmpty(unitRows);
        var headerIndex = Array.FindIndex(records, row => row[0] == columns[0]!.S("header")); Assert.True(headerIndex >= 0);
        var headers = records[headerIndex]; var rows = records.Skip(headerIndex + 1).ToArray(); Assert.Equal(unitRows.Count, rows.Length);
        static void EqualValue(JsonNode? expected, string actual)
        {
            if (expected is null) Assert.Equal("", actual);
            else if (expected.GetValueKind() == JsonValueKind.Number) Assert.Equal(expected.GetValue<decimal>(), decimal.Parse(actual, System.Globalization.CultureInfo.InvariantCulture));
            else Assert.Equal(expected.GetValue<string>(), actual);
        }
        for (var index = 0; index < unitRows.Count; index++)
        {
            var source = unitRows[index]!; var rowIndex = source.I("rowIndex"); var cells = source["cells"]!.AsArray(); var row = rows[index]; Assert.Equal(headers.Length, row.Length);
            string At(string header) { var position = Array.IndexOf(headers, header); Assert.True(position >= 0, $"Missing export column: {header}"); return row[position]; }
            foreach (var column in columns)
            {
                var path = column!.S("path"); var cell = cells.SingleOrDefault(cell => cell!.S("path") == path);
                var expected = cell is not null ? cell["value"] : rowIndex < 0 ? path == columns[0]!.S("path") ? JsonValue.Create("Visible report total") : null : originalRows[rowIndex]![path];
                EqualValue(expected, At(column.S("header")));
            }
            foreach (var cell in cells)
            {
                var kind = cell!.S("kind"); if (kind is not ("Hours" or "UndatedHours" or "Count")) continue;
                var header = columns.Single(column => column!.S("path") == cell.S("path"))!.S("header");
                Assert.Equal(Hub.Api.Text.Get("reportUnits.coverage." + cell.S("coverage")), At($"{header} · coverage"));
                if (kind == "Count") continue;
                var provisional = unitRows.SelectMany(row => row!["cells"]!.AsArray()).Any(other => other!.S("path") == cell.S("path") && other.S("coverage") == "ProvisionalAvailability");
                EqualValue(cell["knownHours"], At(provisional ? $"{header} · recorded source hours (h; may be provisional; see coverage)" : $"{header} · known source hours (h)"));
                Assert.Equal(cell.I("missingInputs"), int.Parse(At($"{header} · unestimated inputs"), System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    [Theory]
    [InlineData("workload-employee", "")]
    [InlineData("workload-discipline", "")]
    [InlineData("team-capacity", "from=2026-09-14&weeks=2&includeMyDrafts=false")]
    [InlineData("allocations-by-source", "from=2026-09-14&weeks=2&show=hours-by-source")]
    [InlineData("allocations-by-source", "from=2026-09-14&weeks=2&show=entries-without-project")]
    [InlineData("task-hours", "from=2026-09-14&to=2026-09-20&scope=mine")]
    public async Task New_context_is_additive_and_dark_off_for_every_actual_remaining_form_with_unchanged_export(string code, string query)
    {
        var p = await d.Project(); await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 4m, dueDate = "2026-09-16" });
        if (code == "allocations-by-source")
            await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = d.User(TestData.Alex), label = "Synthetic positive source export", hoursPerWeek = 4m,
                startWeek = "2026-09-14", endWeek = "2026-09-21", sourceCategory = PlanningSource.Admin, confidence = PlanningConfidence.Confirmed, visibility = PlanningVisibility.Published })).Json(201);
        await Flag("capacity_reports"); var before = await Get(code, query, TestData.Pm); Assert.False(before.AsObject().ContainsKey("taskInputs"));
        var csv = await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/{code}/export?{query}{(query.Length > 0 ? "&" : "")}format=csv");
        await List(code, "unestimated", query, TestData.Pm, status: 404); await Flag("capacity_screens");
        var after = await Get(code, query, TestData.Pm); Assert.NotNull(after["taskInputs"]);
        var units = after["unitView"]?.DeepClone();
        var matrix = code is "workload-employee" or "workload-discipline" or "team-capacity"
            || code == "allocations-by-source" && !query.Contains("show=entries-without-project");
        if (matrix)
        {
            Assert.Equal(("hours", "weeks"), (after["unitView"]!.S("unit"), after["unitView"]!.S("period")));
            Assert.Equal(("hours", "weeks"), (after["savedParameters"]!.S("unit"), after["savedParameters"]!.S("period")));
            var parameters = after["parameters"]!.AsArray();
            var added = parameters.Where(parameter => parameter!.S("label") is "Report unit" or "Period").ToArray();
            Assert.Equal(new[] { "Hours", "Weeks" }, added.Select(parameter => parameter!.S("value")));
            foreach (var parameter in added) parameters.Remove(parameter);
            after.AsObject().Remove("unitView");
        }
        else Assert.False(after.AsObject().ContainsKey("unitView"));
        Assert.False(before.AsObject().ContainsKey("capacityComparison")); // S30 adds the comparison to team-capacity under the same flag
        after.AsObject().Remove("taskInputs"); after.AsObject().Remove("savedParameters"); after.AsObject().Remove("capacityComparison"); Assert.True(JsonNode.DeepEquals(before, after));
        var enabledCsv = await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/{code}/export?{query}{(query.Length > 0 ? "&" : "")}format=csv");
        if (matrix)
        {
            var text = System.Text.Encoding.UTF8.GetString(enabledCsv);
            Assert.Contains("Standard full-time hours per week,40", text); Assert.Contains("Displayed ISO weeks,", text);
            AssertMatrixExport(units!, after["rows"]!.AsArray(), enabledCsv);
            if (code == "allocations-by-source") Assert.Contains(units!["rows"]!.AsArray().SelectMany(row => row!["cells"]!.AsArray()), cell => cell!["value"]?.GetValue<decimal>() == 4m);
        }
        else Assert.Equal(csv, enabledCsv);
        await Flag("capacity_screens", false); Assert.True(JsonNode.DeepEquals(before, await Get(code, query, TestData.Pm)));
        Assert.Equal(csv, await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/{code}/export?{query}{(query.Length > 0 ? "&" : "")}format=csv"));
    }

    [Fact]
    public async Task Remaining_report_inputs_keep_owner_flags_permissions_and_all_scalar_unit_and_range_guards()
    {
        await Flag("capacity_screens"); await Flag("capacity_reports");
        foreach (var code in new[] { "workload-employee", "workload-discipline", "team-capacity", "allocations-by-source" })
        { await Get(code, who: TestData.Alex, status: 403); await List(code, "unestimated", "", TestData.Alex, status: 403); }
        foreach (var (code, query) in new[] {
            ("workload-employee", "supervisorId=broken"), ("workload-employee", $"projectId={Guid.NewGuid()},"),
            ("workload-discipline", "officeId=broken"), ("workload-discipline", "invented=data"),
            ("team-capacity", "weeks=0"), ("team-capacity", "weeks=27"), ("team-capacity", "weeks=1&weeks=2"),
            ("team-capacity", "from=9999-12-31"), ("team-capacity", "includeMyDrafts=%20true%20"),
            ("team-capacity", $"unitId={Guid.NewGuid()}"), // unavailable dimension while org_structure is off
            ("allocations-by-source", "show=not-a-form"), ("allocations-by-source", "source=not-a-source"), ("allocations-by-source", "confidence=not-a-confidence"),
            ("task-hours", "scope=all"), ("task-hours", "userId=broken"), ("task-hours", "from=2026-9-14"),
            ("task-hours", "from=9999-12-31"), ("task-hours", "from=2026-09-20&to=2026-09-14"), ("task-hours", "from=2020-01-01&to=2026-09-14") })
        {
            await Get(code, query, status: 400); await List(code, "unestimated", query, status: 400);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync($"/api/v1/reports/{code}/export?{query}&format=csv")).StatusCode);
        }
        await Flag("org_structure");
        foreach (var query in new[] { "unitId=broken", $"unitId={Guid.NewGuid()}&unitId={Guid.NewGuid()}" })
        { await Get("team-capacity", query, status: 400); await List("team-capacity", "unestimated", query, status: 400); }
        await Flag("capacity_reports", false);
        foreach (var code in new[] { "team-capacity", "allocations-by-source" }) { await Get(code, status: 404); await List(code, "unestimated", "", status: 404); }
        Counts(await Get("task-hours", "scope=mine", TestData.Alex), 0, 0, "CurrentRecordedTaskContext");
    }
}
