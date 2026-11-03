using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S23-B first six task-row reports. Full source counts, not formatted/capped report rows.
public sealed class ReportTaskInputsTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public ReportTaskInputsTests() { d = new(f); f.Clock.SetDate(2026, 9, 14); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(string path, string who = TestData.Pm, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    Task<JsonNode> Run(string code, string query, string who = TestData.Pm) => Get($"/api/v1/reports/{code}?{query}", who);
    Task<JsonNode> List(string code, string metric, string query, int page = 1, int size = 50, string who = TestData.Pm) =>
        Get($"/api/v1/reports/{code}/task-inputs/{metric}?{query}&page={page}&pageSize={size}", who);
    static string Parameters(JsonNode summary) => string.Join("&", summary["parameters"]!.AsObject().Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!.GetValue<string>())}"));
    static Guid[] Ids(JsonNode page) => page["items"]!.AsArray().Select(row => row!.G("id")).ToArray();
    static void Counts(JsonNode report, int unestimated, int undated)
    {
        var summary = report["taskInputs"]!;
        Assert.Equal((unestimated, unestimated, undated, undated), (summary["unestimated"]!.I("count"), summary["unestimated"]!.I("knownCount"), summary["openUndated"]!.I("count"), summary["openUndated"]!.I("knownCount")));
        Assert.Equal(("ReportTaskRows", "CompleteForVisibleParameters", 0), (summary.S("basis"), summary.S("scopeCoverage"), summary.I("unavailableReferences")));
        Assert.True(JsonNode.DeepEquals(report["savedParameters"], summary["parameters"]));
    }
    // Explicit synthetic clock-aging fixture, not evidence of a workflow command.
    async Task Old(params Guid[] tasks) => await f.DbAsync(db => db.Tasks.Where(t => tasks.Contains(t.Id)).ExecuteUpdateAsync(set => set.SetProperty(t => t.LastActivityAt, new DateTimeOffset(2026, 8, 1, 13, 0, 0, TimeSpan.Zero))));
    async Task<string> Durable(Guid project) => await f.DbAsync(async db => JsonSerializer.Serialize(new {
        Project = await db.Projects.AsNoTracking().Where(p => p.Id == project).Select(p => new { p.Id, p.RowVersion, p.Status, p.HealthOverride, p.HealthOverrideNote, p.HealthOverrideAt, p.HealthOverrideBy, p.HealthOverrideExpiresAt }).SingleAsync(),
        Health = await db.ProjectStates.AsNoTracking().Where(s => s.ProjectId == project).Select(s => new { s.ComputedHealth, s.HealthReasons, s.Counts }).SingleAsync(),
        Tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project).OrderBy(t => t.Id).Select(t => new { t.Id, t.RowVersion, t.Status, t.EstimatedHours, t.LastActivityAt }).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Notifications = await db.Notifications.CountAsync(), Commands = await db.CoordinationCommands.CountAsync(),
    }));

    [Fact]
    public async Task Stale_exact_source_overlap_paging_and_live_reread_are_read_only_and_keep_custom_days()
    {
        var p = await d.Project(); var a = await d.NewTask(p.Id); var b = await d.NewTask(p.Id, extra: new { dueDate = "2026-10-01" });
        var c = await d.NewTask(p.Id, extra: new { estimatedHours = 0m }); var dated = await d.NewTask(p.Id, extra: new { estimatedHours = 5m, dueDate = "2026-10-01" });
        var held = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var complete = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) }); var cancelled = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        await d.Move(TestData.Pm, held, TaskStatuses.OnHold, new { reason = "Synthetic hold for the scoped report case" });
        await d.Move(TestData.Alex, complete, TaskStatuses.InProgress); await d.Move(TestData.Alex, complete, TaskStatuses.Complete);
        await d.Move(TestData.Pm, cancelled, TaskStatuses.Cancelled, new { reason = "Synthetic cancellation for the scoped report case" });
        await Old(a.G("id"), b.G("id"), c.G("id"), dated.G("id"), held.G("id"), complete.G("id"), cancelled.G("id")); await f.Evaluate(p.Id); await Flag();
        var before = await Durable(p.Id); var report = await Run("stale-work", $"projectId={p.Id}&days=20"); Counts(report, 2, 2); Assert.Equal(4, report.I("total"));
        var summary = report["taskInputs"]!; var query = Parameters(summary);
        Assert.Contains("days=20", summary["unestimated"]!.S("href")); Assert.DoesNotContain("ids=", summary["unestimated"]!.S("href"));
        var first = await List("stale-work", "unestimated", query, 1, 1); var second = await List("stale-work", "unestimated", query, 2, 1);
        Assert.Equal(new[] { a.G("id"), b.G("id") }, Ids(first).Concat(Ids(second))); Assert.Equal((2, 2), (first.I("total"), second.I("knownTotal")));
        Assert.Empty(Ids(await List("stale-work", "unestimated", query, 3, 1)));
        Assert.Equal(new[] { a.G("id"), c.G("id") }, Ids(await List("stale-work", "open-undated", query)));
        Assert.Equal(summary.S("asOf"), first.S("asOf")); Assert.Equal(before, await Durable(p.Id));
        var version = await d.TaskVersion(a);
        await (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{a.G("id")}", new { estimatedHours = 6m, rowVersion = version }, version)).Json();
        f.Clock.SetDate(2026, 9, 15); var fresh = await List("stale-work", "unestimated", query);
        Assert.Equal(1, fresh.I("total")); Assert.Equal(new[] { b.G("id") }, Ids(fresh)); Assert.NotEqual(first.S("asOf"), fresh.S("asOf"));
    }

    [Fact]
    public async Task Due_week_keeps_week_discipline_assignee_and_mine_and_dated_cohort_has_zero_undated()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var civil = await d.Discipline("Civil");
        var a = await d.NewTask(p.Id, extra: new { assigneeId = alex, dueDate = "2026-09-15" });
        var b = await d.NewTask(p.Id, extra: new { assigneeId = alex, dueDate = "2026-09-20" });
        await d.NewTask(p.Id, extra: new { assigneeId = alex, dueDate = "2026-09-21" }); await d.NewTask(p.Id, extra: new { assigneeId = alex });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, dueDate = "2026-09-15" }, "Electrical");
        await d.NewTask(p.Id, extra: new { dueDate = "2026-09-15" }); await f.Evaluate(p.Id); await Flag();
        var report = await Run("tasks-due-this-week", $"projectId={p.Id}&disciplineId={civil}&assigneeId={alex}&mine=true&week=2026-09-16", TestData.Alex);
        Counts(report, 2, 0); Assert.Equal(2, report.I("total")); Assert.Equal("2026-09-14", report["taskInputs"]!["parameters"]!.S("week"));
        var listed = await List("tasks-due-this-week", "unestimated", Parameters(report["taskInputs"]!), who: TestData.Alex);
        Assert.Equal(new[] { a.G("id"), b.G("id") }, Ids(listed)); Assert.Empty(Ids(await List("tasks-due-this-week", "open-undated", Parameters(report["taskInputs"]!), who: TestData.Alex)));
    }

    [Fact]
    public async Task Overdue_custom_minimum_is_not_broadened_to_the_generic_overdue_destination()
    {
        var p = await d.Project(); var older = await d.NewTask(p.Id, extra: new { dueDate = "2026-09-01" });
        await d.NewTask(p.Id, extra: new { dueDate = "2026-09-12" }); await d.NewTask(p.Id); await f.Evaluate(p.Id); await Flag();
        var report = await Run("overdue-tasks", $"projectId={p.Id}&minDaysOverdue=5"); Counts(report, 1, 0); Assert.Null(report["listLink"]);
        Assert.Equal("5", report["taskInputs"]!["parameters"]!.S("minDaysOverdue"));
        Assert.Equal(new[] { older.G("id") }, Ids(await List("overdue-tasks", "unestimated", Parameters(report["taskInputs"]!))));
    }

    [Fact]
    public async Task Blocker_type_matches_full_stored_predicate_including_legacy_missing_blocking_and_excludes_false()
    {
        var p = await d.Project(); var tasks = new List<JsonNode>(); for (var i = 0; i < 4; i++) tasks.Add(await d.NewTask(p.Id));
        await f.Evaluate(p.Id);
        var blockers = new[] { "[{\"type\":\"manual\",\"name\":\"Manual\",\"blocking\":true}]",
            "[{\"type\":\"manual\",\"name\":\"Manual\",\"blocking\":true},{\"type\":\"decision\",\"name\":\"Not blocking\",\"blocking\":false}]",
            "[{\"type\":\"decision\",\"name\":\"Blocking\",\"blocking\":true}]", "[{\"type\":\"decision\",\"name\":\"Legacy blocking\"}]" };
        // Deliberate stored-evaluation recovery fixtures, including legacy missing blocking, not workflow claims.
        await f.DbAsync(async db => { for (var i = 0; i < tasks.Count; i++) { var state = await db.TaskStates.SingleAsync(s => s.TaskId == tasks[i].G("id")); state.IsBlocked = true; state.BlockedBy = blockers[i]; } return await db.SaveChangesAsync(); });
        await Flag(); var report = await Run("blocked-tasks", $"projectId={p.Id}&blockerType=decision"); Counts(report, 2, 2);
        Assert.Equal(new[] { tasks[2].G("id"), tasks[3].G("id") }, Ids(await List("blocked-tasks", "unestimated", Parameters(report["taskInputs"]!))));
        Assert.Equal(2, report.I("total")); Assert.Null(report["listLink"]); Assert.Contains("blockerType=decision", report["taskInputs"]!["openUndated"]!.S("href"));
    }

    [Fact]
    public async Task Blocking_source_counts_only_its_actual_task_rows_and_preserves_zero_estimate()
    {
        var p = await d.Project(); var a = await d.NewTask(p.Id); var zero = await d.NewTask(p.Id, extra: new { estimatedHours = 0m }); await d.NewTask(p.Id);
        await d.NewTask(p.Id, extra: new { dependsOn = new[] { a.G("id"), zero.G("id") }, estimatedHours = 1m });
        await f.Evaluate(p.Id);
        await Flag(); var report = await Run("tasks-blocking-others", $"projectId={p.Id}"); Counts(report, 1, 2); Assert.Equal(2, report.I("total"));
        Assert.Equal(new[] { a.G("id") }, Ids(await List("tasks-blocking-others", "unestimated", Parameters(report["taskInputs"]!))));
        Assert.Equal(new[] { a.G("id"), zero.G("id") }, Ids(await List("tasks-blocking-others", "open-undated", Parameters(report["taskInputs"]!))));
    }

    [Fact]
    public async Task Mixed_review_queue_counts_its_filtered_task_branch_without_deliverables_or_other_reviewers()
    {
        var p = await d.Project(); var marc = d.User(TestData.Marc);
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), requiresReview = true });
        var other = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), requiresReview = true });
        foreach (var (work, reviewer) in new[] { (task, marc), (other, d.User(TestData.Omar)) })
        { await d.Move(TestData.Alex, work, TaskStatuses.InProgress); await d.Move(TestData.Alex, work, TaskStatuses.ReadyForReview, new { reviewerId = reviewer }); }
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic reviewed deliverable", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), reviewerId = marc, requiresReview = true })).Json(201);
        foreach (var toStatus in new[] { DeliverableStatus.InProgress, DeliverableStatus.InReview })
        {
            var rowVersion = await f.DbAsync(db => db.Deliverables.Where(d => d.Id == deliverable.G("id")).Select(d => d.RowVersion).SingleAsync());
            await (await f.As(TestData.Alex).Post($"/api/v1/deliverables/{deliverable.G("id")}/transition", new { toStatus, rowVersion })).Json();
        }
        await f.Evaluate(p.Id); await Flag(); var report = await Run("review-queue", $"projectId={p.Id}&reviewerId={marc}"); Counts(report, 1, 1);
        Assert.Equal(2, report.I("total")); Assert.Contains(report["rows"]!.AsArray(), row => row!.S("itemType") == ItemType.Deliverable);
        Assert.Equal(new[] { task.G("id") }, Ids(await List("review-queue", "unestimated", Parameters(report["taskInputs"]!))));
    }

    [Fact]
    public async Task Full_counts_pass_screen_and_export_caps_while_pager_and_export_keep_their_bounds()
    {
        var p = await d.Project(); var prototype = await d.NewTask(p.Id); await Old(prototype.G("id"));
        var count = Hub.Api.Infrastructure.Export.MaxRows + 5;
        // Explicit scale fixture clones a valid API-created task; no claim about workflow execution for clones.
        await f.DbAsync(async db => { db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2)); return await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO hub.task
            SELECT clone.* FROM hub.task t CROSS JOIN generate_series(2, {2}) s
            CROSS JOIN LATERAL jsonb_populate_record(NULL::hub.task, to_jsonb(t) || jsonb_build_object(
                'id', gen_random_uuid(), 'seq', s, 'key', {1} || '-T' || s::text)) AS clone
            WHERE t.id = {0}
            """, prototype.G("id"), p.ProjectNumber, count); });
        await Flag(); var report = await Run("stale-work", $"projectId={p.Id}&days=20"); Counts(report, count, count);
        Assert.Equal(2000, report["rows"]!.AsArray().Count); Assert.True(report["totalIsLowerBound"]!.GetValue<bool>());
        Assert.Equal(Hub.Api.Infrastructure.Export.MaxRows + 1, report.I("total"));
        var last = await List("stale-work", "unestimated", Parameters(report["taskInputs"]!), (count + 199) / 200, 200);
        Assert.Equal(count, last.I("total")); Assert.Equal(count, last.I("knownTotal")); Assert.Equal(count % 200, Ids(last).Length);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/stale-work/export?projectId={p.Id}&days=20&format=csv")).StatusCode);
    }

    [Fact]
    public async Task Flag_scope_reference_and_metric_guards_preserve_off_body_and_export_and_never_widen_empty_scope()
    {
        var p = await d.Project(); var task = await d.NewTask(p.Id, extra: new { dueDate = "2026-09-15" }); await f.Evaluate(p.Id);
        var query = $"projectId={p.Id}&week=2026-09-14"; var before = await Run("tasks-due-this-week", query);
        var csv = await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/tasks-due-this-week/export?{query}&format=csv");
        await Get($"/api/v1/reports/tasks-due-this-week/task-inputs/unestimated?{query}", status: 404);
        await Flag(); var after = await Run("tasks-due-this-week", query); Counts(after, 1, 0);
        after.AsObject().Remove("taskInputs"); after.AsObject().Remove("savedParameters"); Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(csv, await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/tasks-due-this-week/export?{query}&format=csv"));
        Assert.False((await Run("upcoming-milestones", $"projectId={p.Id}")).AsObject().ContainsKey("taskInputs"));
        await Get($"/api/v1/reports/upcoming-milestones/task-inputs/unestimated?projectId={p.Id}", status: 404);
        await Get($"/api/v1/reports/tasks-due-this-week/task-inputs/invented?{query}", status: 400);
        foreach (var malformed in new[] { "projectId=broken", "projectId=", $"projectId={p.Id},broken", "assigneeId=broken" })
            await Get($"/api/v1/reports/tasks-due-this-week/task-inputs/unestimated?{malformed}", status: 400);
        var hidden = await d.Project(); var secret = await d.NewTask(hidden.Id, extra: new { name = "PRIVATE INPUT SENTINEL", dueDate = "2026-09-15" }); await f.Evaluate(hidden.Id);
        await f.DbAsync(db => db.Projects.Where(row => row.Id == hidden.Id).ExecuteUpdateAsync(set => set.SetProperty(row => row.Visibility, Visibility.Restricted)));
        var invisible = await List("tasks-due-this-week", "unestimated", $"projectId={hidden.Id}&week=2026-09-14", who: TestData.Sam);
        Assert.Equal(0, invisible.I("total")); Assert.Empty(Ids(invisible)); Assert.DoesNotContain("PRIVATE INPUT SENTINEL", invisible.ToJsonString()); Assert.DoesNotContain(secret.G("id").ToString(), invisible.ToJsonString());
        var empty = await List("tasks-due-this-week", "unestimated", $"projectId={Guid.NewGuid()}&week=2026-09-14"); Assert.Equal(0, empty.I("total")); Assert.Empty(Ids(empty));
        await Flag(false); Assert.True(JsonNode.DeepEquals(before, await Run("tasks-due-this-week", query)));
        Assert.Equal(task.G("id"), before["rows"]![0]!.G("id"));
    }

    [Fact]
    public async Task Comma_padded_scalar_references_refuse_on_report_list_and_export_without_dropping_filters()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var civil = await d.Discipline("Civil");
        await d.NewTask(p.Id, extra: new { assigneeId = alex, dueDate = "2026-09-15" });
        await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Marc), dueDate = "2026-09-15" });
        await Flag();
        foreach (var (code, parameter, reference) in new[] {
            ("tasks-due-this-week", "assigneeId", alex),
            ("tasks-due-this-week", "disciplineId", civil),
            ("review-queue", "reviewerId", d.User(TestData.Marc)) })
        foreach (var malformed in new[] { $"{reference},", $",{reference}", $",{reference}," })
        {
            var query = $"projectId={p.Id}&{parameter}={Uri.EscapeDataString(malformed)}";
            await Get($"/api/v1/reports/{code}?{query}", status: 400);
            await Get($"/api/v1/reports/{code}/task-inputs/unestimated?{query}", status: 400);
            await Get($"/api/v1/reports/{code}/export?{query}&format=csv", status: 400);
        }
        var valid = await Run("tasks-due-this-week", $"projectId={p.Id}&assigneeId={alex}&week=2026-09-14");
        Counts(valid, 1, 0); Assert.Equal(1, valid.I("total"));
    }

    [Fact]
    public async Task Maximum_page_offsets_return_empty_with_the_full_count_instead_of_wrapping()
    {
        var p = await d.Project(); await d.NewTask(p.Id, extra: new { dueDate = "2026-09-15" }); await Flag();
        var query = $"projectId={p.Id}&week=2026-09-14";
        Assert.Single(Ids(await List("tasks-due-this-week", "unestimated", query, 1, 50)));
        foreach (var page in new[] { 42_949_674, 1_073_741_825, int.MaxValue })
        foreach (var size in new[] { 50, 200 })
        {
            var high = await List("tasks-due-this-week", "unestimated", query, page, size);
            Assert.Equal(page, high.I("page")); Assert.Equal(size, high.I("pageSize"));
            Assert.Equal(1, high.I("total")); Assert.Equal(1, high.I("knownTotal")); Assert.Empty(Ids(high));
        }
    }

    [Fact]
    public async Task Malformed_and_repeated_supported_scalars_refuse_instead_of_becoming_defaults_while_numeric_clamping_stays()
    {
        var p = await d.Project(); await Flag();
        foreach (var (code, query) in new[] {
            ("tasks-due-this-week", "mine=garbage"), ("tasks-due-this-week", "mine=%20true%20"), ("tasks-due-this-week", "mine=true&mine=false"),
            ("tasks-due-this-week", "week=garbage"), ("tasks-due-this-week", "week=2026-9-14"),
            ("tasks-due-this-week", "week=9999-12-31"), ("tasks-due-this-week", "week=2026-09-14&week=2026-09-21"),
            ("overdue-tasks", "minDaysOverdue=garbage"), ("overdue-tasks", "minDaysOverdue=1&minDaysOverdue=2"),
            ("blocked-tasks", "blockerType=invented"), ("blocked-tasks", "blockerType=%20decision%20"), ("blocked-tasks", "blockerType=manual&blockerType=decision"),
            ("stale-work", "days=2147483647") })
        {
            await Get($"/api/v1/reports/{code}?projectId={p.Id}&{query}", status: 400);
            await Get($"/api/v1/reports/{code}/task-inputs/unestimated?projectId={p.Id}&{query}", status: 400);
        }
        Assert.Equal("1", (await Run("overdue-tasks", $"projectId={p.Id}&minDaysOverdue=-3"))["savedParameters"]!.S("minDaysOverdue"));
        Assert.Equal("1", (await List("overdue-tasks", "unestimated", $"projectId={p.Id}&minDaysOverdue=-3"))["parameters"]!.S("minDaysOverdue"));
        await Flag(false);
        foreach (var (code, query) in new[] { ("tasks-due-this-week", "mine=garbage"), ("tasks-due-this-week", "week=garbage"), ("overdue-tasks", "minDaysOverdue=garbage") })
            Assert.False((await Run(code, $"projectId={p.Id}&{query}")).AsObject().ContainsKey("taskInputs"));
    }
}
