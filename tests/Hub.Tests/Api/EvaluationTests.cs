using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// Packet 005 through the API: AC-DEP-01/02/04/05/07, AC-ATT-02/04, AC-HLT-03/05, AC-PRJ-03, §16.7, §22, outbox worker.
[Collection("api")]
public sealed class EvaluationTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    /// Runs `body` with the clock moved to a date, then puts it back (the clock is shared by the collection).
    async Task At(int y, int m, int day, Func<Task> body)
    {
        var saved = f.Clock.Now;
        f.Clock.SetDate(y, m, day);
        try { await body(); } finally { f.Clock.Now = saved; }
    }

    Task<List<AttentionItem>> Attention(Guid projectId) => f.DbAsync(db => db.Attention.Where(a => a.ProjectId == projectId).ToListAsync());
    Task<TaskState> State(JsonNode t) => f.DbAsync(db => db.TaskStates.FirstAsync(s => s.TaskId == t.G("id")));

    [Fact]
    public async Task Concurrent_first_evaluations_materialise_each_snapshot_once()
    {
        var project = await d.Project();
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });

        var evaluations = Enumerable.Range(0, 8).Select(async _ =>
        {
            using var scope = f.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
            await scope.ServiceProvider.GetRequiredService<EvaluationService>().EvaluateProject(project.Id, notifyTransitions: false);
        });
        await Task.WhenAll(evaluations);

        var counts = f.Db(db => new
        {
            Tasks = db.TaskStates.Count(s => s.ProjectId == project.Id),
            Deliverables = db.DeliverableStates.Count(s => s.ProjectId == project.Id),
            Milestones = db.MilestoneStates.Count(s => s.ProjectId == project.Id),
            Decisions = db.DecisionStates.Count(s => s.ProjectId == project.Id),
            Projects = db.ProjectStates.Count(s => s.ProjectId == project.Id),
        });
        Assert.Equal(2, counts.Tasks);
        Assert.Equal(0, counts.Deliverables);
        Assert.Equal(0, counts.Milestones);
        Assert.Equal(0, counts.Decisions);
        Assert.Equal(1, counts.Projects);
    }

    [Fact]
    public async Task Dependency_sides_cycle_refusal_and_chain() // AC-DEP-01, AC-DEP-02, AC-DEP-07
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var a = await d.NewTask(p.Id, TestData.Pm);
        var b = await d.NewTask(p.Id, TestData.Pm);
        var c = await d.NewTask(p.Id, TestData.Pm);
        var e = await d.NewTask(p.Id, TestData.Pm);
        await pm.Post($"/api/v1/tasks/{b.S("id")}/dependencies", new { predecessorTaskId = a.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{c.S("id")}/dependencies", new { predecessorTaskId = b.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{e.S("id")}/dependencies", new { predecessorTaskId = c.G("id") }).Result.Json(201);

        var ofA = await pm.GetAsync($"/api/v1/tasks/{a.S("id")}/dependencies").Result.Json();
        Assert.Equal(b.S("key"), ofA["blocks"]![0]!["task"]!.S("key"));
        var ofB = await pm.GetAsync($"/api/v1/tasks/{b.S("id")}/dependencies").Result.Json();
        Assert.Equal(a.S("key"), ofB["dependsOn"]![0]!["task"]!.S("key"));

        var loop = await pm.Post($"/api/v1/tasks/{a.S("id")}/dependencies", new { predecessorTaskId = c.G("id") }).Result.Json(409);
        Assert.Equal("dependency_cycle", loop.S("code"));
        Assert.Equal(new[] { a.S("key"), b.S("key"), c.S("key"), a.S("key") }, loop["cyclePath"]!.AsArray().Select(x => x!.GetValue<string>()));
        var candidates = await pm.GetAsync($"/api/v1/tasks/{a.S("id")}/dependency-candidates").Result.Json();
        Assert.True(candidates.AsArray().First(x => x!.S("key") == c.S("key"))!["disabled"]!.GetValue<bool>());

        var chain = await pm.GetAsync($"/api/v1/tasks/{b.S("id")}/chain").Result.Json();
        Assert.Equal(a.S("key"), Assert.Single(chain["predecessors"]!.AsArray())!["task"]!.S("key"));
        Assert.Equal(new[] { (c.S("key"), 1), (e.S("key"), 2) }, chain["successors"]!.AsArray().Select(x => (x!["task"]!.S("key"), x.I("level"))));
    }

    [Fact]
    public async Task Overdue_predecessor_blocks_successor_until_complete() // AC-DEP-04, AC-DEP-05, D-06, D-07, D-11, D-13
    {
        var p = await d.Project();
        var m = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Tender", milestoneType = "Tender", date = "2026-10-30" }).Result.Json(201);
        var a = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), startDate = "2026-09-10", dueDate = "2026-09-18" });
        var b = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Jill), startDate = "2026-09-19", dueDate = "2026-09-30", milestoneId = m.G("id"), dependsOn = new[] { a.G("id") } });
        await d.Move(TestData.Alex, a, TaskStatuses.InProgress);
        await f.Evaluate(p.Id);
        Assert.True((await State(b)).IsWaiting);
        Assert.False((await State(b)).IsBlocked);

        await At(2026, 9, 21, async () =>
        {
            await f.Evaluate(p.Id);
            var sb = await State(b);
            Assert.True(sb.IsBlocked);
            Assert.Contains(a.S("key"), sb.BlockedBy);
            var sa = await State(a);
            Assert.True(sa.IsBlocking);
            Assert.Equal(1, sa.BlockingCount);
            var att = await Attention(p.Id);
            var a03 = att.Single(x => x.RuleId == "A-03" && x.ItemId == a.G("id"));
            Assert.Equal(Severity.Critical, a03.Severity);
            Assert.Contains(att, x => x.RuleId == "A-02" && x.ItemId == b.G("id"));
            Assert.Contains(m.G("id"), sb.AffectedMilestoneIds);
            Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Jill) && n.ItemId == b.G("id") && n.EventType == NotificationEvents.TaskBlocked)));
            Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Alex) && n.ItemId == a.G("id") && n.EventType == NotificationEvents.BlockingOverdue)));

            await d.Move(TestData.Alex, a, TaskStatuses.Complete);
            await f.Evaluate(p.Id);
            Assert.False((await State(b)).IsBlocked);
            Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Jill) && n.ItemId == b.G("id") && n.EventType == NotificationEvents.TaskUnblocked)));
        });
    }

    [Fact]
    public async Task Putting_a_waiting_task_on_hold_does_not_say_you_can_start() // D-07
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-20" });
        var b = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Jill), dueDate = "2026-10-30", dependsOn = new[] { a.G("id") } });
        await f.Evaluate(p.Id);
        Assert.True((await State(b)).IsWaiting);
        await d.Move(TestData.Pm, b, TaskStatuses.OnHold, new { reason = "Client paused this part" });
        await f.Evaluate(p.Id);
        Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.ItemId == b.G("id") && n.EventType == NotificationEvents.TaskUnblocked)));
    }

    [Fact]
    public async Task Snoozed_item_leaves_the_list_is_logged_and_returns() // AC-ATT-02, ATT-03
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-10" });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Jill), dependsOn = new[] { a.G("id") } });
        await f.Evaluate(p.Id);
        var pm = f.As(TestData.Pm);
        var list = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention").Result.Json();
        var top = list["items"]![0]!;
        Assert.Equal("A-03", top.S("ruleId")); // AC-ATT-01: blocking-and-overdue ranks first
        Assert.NotNull(top["why"]); // AC-ATT-06
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/attention/{top.S("id")}/snooze", new { days = 31, note = "Waiting on the client" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/attention/{top.S("id")}/snooze", new { days = 7, note = "Waiting on the client" })).StatusCode);
        await pm.Post($"/api/v1/attention/{top.S("id")}/snooze", new { days = 7, note = "Waiting on the client" }).Result.Json();

        var after = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention").Result.Json();
        Assert.DoesNotContain(after["items"]!.AsArray(), x => x!.S("id") == top.S("id"));
        Assert.Equal(1, after.I("snoozed"));
        var withSnoozed = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention?includeSnoozed=true").Result.Json();
        Assert.True(withSnoozed["items"]!.AsArray().First(x => x!.S("id") == top.S("id"))!["snoozed"]!.GetValue<bool>());
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(x => x.ProjectId == p.Id && x.Action == "Snoozed" && x.Reason == "Waiting on the client")));

        await At(2026, 9, 22, async () =>
        {
            var back = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention").Result.Json();
            Assert.Contains(back["items"]!.AsArray(), x => x!.S("ruleId") == "A-03");
        });
    }

    static string[] Keys(JsonNode? n) => n!.AsObject().Select(x => x.Key).ToArray();

    [Fact]
    public async Task Attention_snooze_override_and_state_answers_keep_their_property_names_order_kinds_and_refusals() // the typed records must serialise to the bytes the Dashboard, Health dialog and My Work already read
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-10" });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Jill), dependsOn = new[] { a.G("id") } });
        await f.Evaluate(p.Id);
        var pm = f.As(TestData.Pm);
        string[] rowKeys = ["id", "projectId", "projectNumber", "ruleId", "ruleName", "itemType", "itemId", "itemKey", "itemName", "severity", "message", "why", "routeToUserIds", "ownerUserId",
            "ownerName", "projectDisciplineId", "daysOverdueOrBlocked", "priority", "dueDate", "firstDetectedAt", "ageDays", "snoozed", "snoozedUntil", "snoozeNote"];

        var list = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention").Result.Json();
        Assert.Equal(["items", "snoozed", "total", "truncated"], Keys(list));
        Assert.Equal((0, false), (list.I("snoozed"), list["truncated"]!.GetValue<bool>()));
        var items = list["items"]!.AsArray().Select(x => x!).ToArray();
        Assert.Equal(items.Length, list.I("total"));
        Assert.True(items.Length >= 2);
        Assert.Equal("A-03", items[0].S("ruleId")); // ranked: blocking-and-overdue first
        Assert.All(items, x =>
        {
            Assert.Equal(rowKeys, Keys(x));
            Assert.All(new[] { "id", "projectId", "projectNumber", "ruleId", "ruleName", "itemType", "itemId", "severity", "message", "firstDetectedAt" }, k => Assert.Equal(JsonValueKind.String, x[k]!.GetValueKind()));
            Assert.All(new[] { "daysOverdueOrBlocked", "ageDays" }, k => Assert.Equal(JsonValueKind.Number, x[k]!.GetValueKind()));
            Assert.Equal(JsonValueKind.Array, x["routeToUserIds"]!.GetValueKind());
            Assert.Equal(JsonValueKind.Object, x["why"]!.GetValueKind());
            Assert.Equal(JsonValueKind.False, x["snoozed"]!.GetValueKind());
            Assert.Equal((null, null), (x["snoozedUntil"], x["snoozeNote"])); // present (the key order above) and null
            Assert.Equal(p.ProjectNumber, x.S("projectNumber"));
        });
        Assert.Equal((U(TestData.Alex), "Alex Chen"), (items[0].G("ownerUserId"), items[0].S("ownerName")));
        Assert.Equal(JsonValueKind.String, items[0]["itemKey"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, items[0]["dueDate"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, items[0]["priority"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, items[0]["projectDisciplineId"]!.GetValueKind());

        var snoozed = await pm.Post($"/api/v1/attention/{items[0].S("id")}/snooze", new { days = 7, note = "Waiting on the client" }).Result.Json();
        Assert.Equal(["id", "snoozedUntil"], Keys(snoozed));
        Assert.Equal(JsonValueKind.String, snoozed["id"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, snoozed["snoozedUntil"]!.GetValueKind());
        var all = await pm.GetAsync($"/api/v1/projects/{p.Id}/attention?includeSnoozed=true").Result.Json();
        Assert.Equal(["items", "snoozed", "total", "truncated"], Keys(all));
        Assert.Equal((1, items.Length - 1), (all.I("snoozed"), all.I("total")));
        var row = all["items"]!.AsArray().Single(x => x!.S("id") == items[0].S("id"))!;
        Assert.Equal(rowKeys, Keys(row));
        Assert.Equal((true, "Waiting on the client", snoozed.S("snoozedUntil")), (row["snoozed"]!.GetValue<bool>(), row.S("snoozeNote"), row.S("snoozedUntil")));
        Assert.Equal(items.Length - 1, (await pm.GetAsync($"/api/v1/projects/{p.Id}/attention").Result.Json())["items"]!.AsArray().Count);
        Assert.Equal(["items", "snoozed", "total", "truncated"], Keys(await pm.GetAsync($"/api/v1/projects/{p.Id}/attention?severity=Nothing").Result.Json())); // an empty list is still the same envelope
        Assert.Empty((await pm.GetAsync($"/api/v1/projects/{p.Id}/attention?severity=Nothing").Result.Json())["items"]!.AsArray());

        var before = d.Version(p.Id);
        var set = await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Green", note = "Recovery plan agreed", rowVersion = before }).Result.Json();
        Assert.Equal(["id", "rowVersion", "healthOverrideExpiresAt"], Keys(set));
        Assert.Equal((p.Id, JsonValueKind.Number, JsonValueKind.String), (set.G("id"), set["rowVersion"]!.GetValueKind(), set["healthOverrideExpiresAt"]!.GetValueKind()));
        Assert.True(set.I("rowVersion") > before);
        var cleared = await pm.Delete($"/api/v1/projects/{p.Id}/health-override", set.I("rowVersion")).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(cleared));
        Assert.Equal((p.Id, JsonValueKind.Number), (cleared.G("id"), cleared["rowVersion"]!.GetValueKind()));
        Assert.True(cleared.I("rowVersion") > set.I("rowVersion"));

        var state = await pm.GetAsync($"/api/v1/projects/{p.Id}/state").Result.Json();
        Assert.Equal(["computedHealth", "healthReasons", "inputs", "counts", "disciplines", "nextMilestoneId", "nextSubmissionMilestoneId", "progressPct", "evaluatedAt"], Keys(state));
        Assert.Equal(JsonValueKind.String, state["computedHealth"]!.GetValueKind());
        Assert.Equal((JsonValueKind.Array, JsonValueKind.Object, JsonValueKind.Object, JsonValueKind.Array), (state["healthReasons"]!.GetValueKind(), state["inputs"]!.GetValueKind(), state["counts"]!.GetValueKind(), state["disciplines"]!.GetValueKind()));
        Assert.Equal((null, null), (state["nextMilestoneId"], state["nextSubmissionMilestoneId"])); // present (the key order above) and null
        Assert.Equal(JsonValueKind.String, state["evaluatedAt"]!.GetValueKind());

        var ghost = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{ghost}/attention")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{ghost}/state")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/attention/{ghost}/snooze", new { days = 7, note = "Nobody" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/projects/{ghost}/health-override", new { health = "Green", note = "Nobody", rowVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Delete($"/api/v1/projects/{ghost}/health-override", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/attention/{items[1].S("id")}/snooze", new { days = 0, note = "Too short" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/attention/{items[1].S("id")}/snooze", new { days = 7, note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Green", note = "", rowVersion = cleared.I("rowVersion") })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Green", note = "Stale view", rowVersion = before })).StatusCode);
    }

    [Fact]
    public async Task Setup_and_on_hold_projects_have_no_attention_and_grey_health() // AC-ATT-04, AC-PRJ-03, G-05
    {
        var setup = await d.Project(activate: false);
        await d.NewTask(setup.Id, TestData.Pm, new { dueDate = "2026-09-01" });
        await f.Evaluate(setup.Id);
        Assert.Empty(await Attention(setup.Id));

        var p = await d.Project();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-01" });
        await f.Evaluate(p.Id);
        Assert.NotEmpty(await Attention(p.Id));
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = d.Version(p.Id) }).Result.Json();
        await f.Evaluate(p.Id);
        Assert.Empty(await Attention(p.Id));
        var state = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/state").Result.Json();
        Assert.Equal("Grey", state.S("computedHealth"));
        Assert.Contains("on hold", state["healthReasons"]!.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, state["counts"]!.I("tasksOverdue"));
    }

    [Fact]
    public async Task Health_override_shows_both_values_and_expires_nightly() // AC-HLT-03, FR-HLT-02, A-15
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "30% Submission", milestoneType = "Design Submission", date = "2026-09-11" }).Result.Json(201); // overdue milestone: Red (AC-MS-03)
        await f.Evaluate(p.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Grey", note = "Nothing to see", rowVersion = d.Version(p.Id) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Green", note = "Recovery plan agreed", rowVersion = d.Version(p.Id) })).StatusCode);
        await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Green", note = "Recovery plan agreed", rowVersion = d.Version(p.Id) }).Result.Json();

        var detail = await pm.GetAsync($"/api/v1/projects/{p.Id}").Result.Json();
        Assert.Equal("Green", detail["health"]!.S("reported"));
        Assert.Equal("Red", detail["health"]!.S("computed"));
        var row = (await pm.GetAsync($"/api/v1/projects?q={p.ProjectNumber}").Result.Json())["items"]!.AsArray().Single(x => x!.S("id") == p.Id.ToString())!;
        Assert.Equal("Green", row.S("reportedHealth"));
        Assert.Equal("Red", row.S("computedHealth"));

        await At(2026, 9, 29, async () =>
        {
            await f.RunJob<NightlyJob>();
            var saved = await f.DbAsync(db => db.Projects.FirstAsync(x => x.Id == p.Id));
            Assert.Null(saved.HealthOverride);
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(x => x.ItemId == p.Id && x.Action == "HealthOverrideExpired" && x.ActorType == "System")));
            Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Pm) && n.ProjectId == p.Id && n.EventType == NotificationEvents.HealthOverride)));
            Assert.Contains(await Attention(p.Id), x => x.RuleId == "A-15");
        });
    }

    [Fact]
    public async Task Clearing_health_override_preserves_version_checks_and_missing_state_is_rebuilt()
    {
        var project = await d.Project();
        var pm = f.As(TestData.Pm);
        var beforeVersion = d.Version(project.Id);
        var set = await (await pm.Post($"/api/v1/projects/{project.Id}/health-override", new { health = "Yellow", note = "Synthetic review", rowVersion = beforeVersion })).Json();
        var currentVersion = set.I("rowVersion");
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Delete($"/api/v1/projects/{project.Id}/health-override", beforeVersion)).StatusCode);
        Assert.Equal("Yellow", f.Db(db => db.Projects.Single(p => p.Id == project.Id).HealthOverride));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Delete($"/api/v1/projects/{project.Id}/health-override", currentVersion)).StatusCode);
        var cleared = await (await pm.Delete($"/api/v1/projects/{project.Id}/health-override", currentVersion)).Json();
        Assert.Equal(project.Id, cleared.G("id"));
        Assert.True(cleared.I("rowVersion") > currentVersion);
        Assert.Null(f.Db(db => db.Projects.Single(p => p.Id == project.Id).HealthOverride));
        await f.DbAsync(db => db.ProjectStates.Where(p => p.ProjectId == project.Id).ExecuteDeleteAsync());
        var state = await (await pm.GetAsync($"/api/v1/projects/{project.Id}/state")).Json();
        Assert.Equal("Grey", state.S("computedHealth")); // An active project without work has no health inputs.
        Assert.NotNull(state["healthReasons"]);
        Assert.True(f.Db(db => db.ProjectStates.Any(p => p.ProjectId == project.Id)));
    }

    [Fact]
    public async Task Nightly_run_writes_one_snapshot_per_active_project_and_rolls_the_date() // AC-HLT-05, §16.7
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-24" });
        await At(2026, 9, 24, async () =>
        {
            await f.Evaluate(p.Id);
            Assert.False((await State(t)).IsOverdue);
        });
        await At(2026, 9, 25, async () =>
        {
            await f.RunJob<NightlyJob>();
            await f.RunJob<NightlyJob>();
            var today = new DateOnly(2026, 9, 25);
            var counts = await f.DbAsync(db => db.Projects.Where(x => x.Status == ProjectStatus.Active)
                .Select(x => db.HealthSnapshots.Count(s => s.ProjectId == x.Id && s.SnapshotDate == today)).ToListAsync());
            Assert.All(counts, c => Assert.Equal(1, c));
            Assert.True((await State(t)).IsOverdue); // due yesterday: overdue with no user action
            var snap = await f.DbAsync(db => db.HealthSnapshots.FirstAsync(x => x.ProjectId == p.Id && x.SnapshotDate == today));
            Assert.Equal(1, System.Text.Json.Nodes.JsonNode.Parse(snap.Counts)!["tasksOverdue"]!.GetValue<int>()); // the day's counts are kept for weekly trends (packet 011)
        });
    }

    [Fact]
    public async Task Outbox_worker_evaluates_changed_projects() // §23.5, FR-ENG-01
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-09-01" });
        Assert.True(await f.DbAsync(db => db.Outbox.AnyAsync(o => o.ProjectId == p.Id && o.ProcessedAt == null)));
        for (var i = 0; i < 20 && await f.DbAsync(db => db.Outbox.AnyAsync(o => o.ProjectId == p.Id && o.ProcessedAt == null)); i++)
            await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);
        Assert.False(await f.DbAsync(db => db.Outbox.AnyAsync(o => o.ProjectId == p.Id && o.ProcessedAt == null)));
        Assert.True((await State(t)).IsOverdue);
    }

    /// FR-BKL-09 (review fixes 2026-10-28): switching `flag.backlog` queues every Active project for re-evaluation through the outbox,
    /// so stored health follows the switch, and its rollback, at once rather than at the next nightly run.
    [Fact, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [Trait("Spec", "FR-BKL-09")]
    public async Task Switching_the_backlog_flag_re_evaluates_stored_health()
    {
        var p = await d.Project();
        string In(int days) => DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(days).ToString("yyyy-MM-dd");
        for (var i = 0; i < 3; i++) await d.NewTask(p.Id, TestData.Pm, new { dueDate = In(-2) });
        for (var i = 0; i < 6; i++) await d.NewTask(p.Id, TestData.Pm, new { dueDate = In(20) });
        for (var i = 0; i < 6; i++) await d.NewTask(p.Id, TestData.Pm); // Backlog
        await f.Evaluate(p.Id);
        await f.DbAsync(db => db.Outbox.Where(o => o.ProjectId == p.Id).ExecuteUpdateAsync(u => u.SetProperty(o => o.ProcessedAt, f.Clock.Now)));
        Task<string> Computed() => f.DbAsync(db => db.ProjectStates.Where(s => s.ProjectId == p.Id).Select(s => s.ComputedHealth).FirstAsync());
        async Task Switch(bool on)
        {
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.backlog", new { value = on })).EnsureSuccessStatusCode();
            for (var i = 0; i < 20 && await f.DbAsync(db => db.Outbox.AnyAsync(o => o.EventType == "SettingsChanged" && o.ProcessedAt == null)); i++)
                await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);
        }
        // 3 overdue of 15 open is 20 %, Yellow; with the Backlog left out, 3 of 9 is 33 %, Red (FR-BKL-05).
        Assert.Equal(Health.Yellow, await Computed());
        try
        {
            await Switch(true);
            Assert.Equal(Health.Red, await Computed());
            await Switch(false);
            Assert.Equal(Health.Yellow, await Computed());
        }
        finally { await Switch(false); }
    }

    [Fact]
    public async Task Settings_change_is_retained_until_every_project_evaluates()
    {
        var failed = await d.Project();
        var healthy = await d.Project();
        var trigger = "fail_settings_eval_" + failed.Id.ToString("N")[^8..];
        var eventId = await f.DbAsync(async db =>
        {
            await db.Outbox.ExecuteDeleteAsync();
            await db.ProjectStates.Where(x => x.ProjectId == failed.Id || x.ProjectId == healthy.Id).ExecuteDeleteAsync();
            var e = new OutboxEvent { EventType = "SettingsChanged", CreatedAt = f.Clock.Now, Payload = "{}" };
            db.Outbox.Add(e);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION hub.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test settings evaluation failure'; END $$;
                CREATE TRIGGER {trigger} BEFORE INSERT OR UPDATE ON hub.project_state FOR EACH ROW WHEN (NEW.project_id = '{failed.Id}') EXECUTE FUNCTION hub.{trigger}();
                """);
            return e.Id;
        });

        try
        {
            await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);
            var first = await f.DbAsync(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Id == eventId));
            Assert.Equal(1, first.Attempts);
            Assert.Null(first.ProcessedAt);
            Assert.Null(first.DeadLetteredAt);
            Assert.False(await f.DbAsync(db => db.ProjectStates.AnyAsync(x => x.ProjectId == failed.Id)));

            await f.DbAsync(db => db.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS {trigger} ON hub.project_state; DROP FUNCTION IF EXISTS hub.{trigger}();"));
            await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);

            var second = await f.DbAsync(db => db.Outbox.AsNoTracking().SingleAsync(x => x.Id == eventId));
            Assert.NotNull(second.ProcessedAt);
            Assert.Equal(1, second.Attempts);
            Assert.True(await f.DbAsync(db => db.ProjectStates.AnyAsync(x => x.ProjectId == failed.Id)));
            Assert.True(await f.DbAsync(db => db.ProjectStates.AnyAsync(x => x.ProjectId == healthy.Id)));
        }
        finally
        {
            await f.DbAsync(async db =>
            {
                await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS {trigger} ON hub.project_state; DROP FUNCTION IF EXISTS hub.{trigger}();");
                return await db.Outbox.Where(x => x.Id == eventId).ExecuteDeleteAsync();
            });
        }
    }

    [Fact]
    public async Task Large_project_re_evaluates_within_two_seconds() // §22: 2,000 tasks
    {
        var p = await d.Project();
        var pd = d.ProjectDiscipline(p.Id, "Civil");
        await f.DbAsync(async db =>
        {
            var pr = await db.Projects.FirstAsync(x => x.Id == p.Id);
            var start = new DateOnly(2026, 9, 1);
            var tasks = Enumerable.Range(1, 2000).Select(i => new WorkTask
            {
                ProjectId = p.Id, Seq = 10000 + i, Key = $"{pr.ProjectNumber}-T{10000 + i}", Name = $"Load {i}", ProjectDisciplineId = pd,
                Status = i % 3 == 0 ? TaskStatuses.InProgress : TaskStatuses.NotStarted, StartDate = start, DueDate = start.AddDays(i % 60),
                LastActivityAt = f.Clock.Now, StatusChangedAt = f.Clock.Now,
            }).ToList();
            db.Tasks.AddRange(tasks);
            await db.SaveChangesAsync();
            db.Dependencies.AddRange(Enumerable.Range(0, 1000).Select(i => new TaskDependency
                { ProjectId = p.Id, PredecessorTaskId = tasks[i].Id, SuccessorTaskId = tasks[i + 1000].Id, CreatedBy = Guid.Empty, CreatedAt = f.Clock.Now }));
            return await db.SaveChangesAsync();
        });
        await f.Evaluate(p.Id); // first run inserts the state rows
        var sw = Stopwatch.StartNew();
        await f.Evaluate(p.Id);
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 2000, $"Re-evaluation took {sw.ElapsedMilliseconds} ms");
        Assert.Equal(2000, await f.DbAsync(db => db.TaskStates.CountAsync(s => s.ProjectId == p.Id)));
    }
}
