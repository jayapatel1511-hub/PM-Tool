using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class TaskGroupingTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public TaskGroupingTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Get(string path, string who = TestData.Alex) => await (await f.As(who).GetAsync(path)).Json();
    async Task Flag(string flag, bool enabled) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{flag}", new { value = enabled })).EnsureSuccessStatusCode();
    async Task<JsonNode> Add(Project p, string name, decimal? hours, string? due = "2026-09-20") => await data.NewTask(p.Id,
        extra: new { name, assigneeId = data.User(TestData.Alex), estimatedHours = hours, dueDate = due, requiresReview = false });
    static string ProjectPath(Project p) => $"/api/v1/projects/{p.Id}/tasks";
    static JsonNode Only(JsonNode page, string dimension = "discipline") => Assert.Single(page["groups"]!.AsArray(), g => g!.S("dimension") == dimension)!;
    async Task Complete(JsonNode task) { await data.Move(TestData.Alex, task, TaskStatuses.InProgress); await data.Move(TestData.Alex, task, TaskStatuses.Complete); }
    static JsonNode Metrics(JsonNode group)
    {
        var result = group.DeepClone(); result.AsObject().Remove("pageShown"); result.AsObject().Remove("pageTaskIds"); return result;
    }

    [Fact]
    public async Task Four_normal_tasks_show_full_filtered_forty_hours_fifty_percent_and_one_overdue_on_every_page()
    {
        var p = await data.Project(); var a = await Add(p, "First complete", 10); var b = await Add(p, "Second complete", 10);
        await Add(p, "Overdue open", 15, "2026-09-13"); await Add(p, "Future open", 5);
        await Complete(a); await Complete(b); await f.Evaluate(p.Id); await Flag("work_screens", true);
        var path = ProjectPath(p) + "?group=discipline&sort=key:asc&pageSize=2";
        var first = await Get(path + "&page=1"); var second = await Get(path + "&page=2"); var g = Only(first);
        Assert.Equal(4, first.I("totalCount")); Assert.Equal(4, g.I("rowCount")); Assert.Equal(40m, g["estimatedHours"]!.GetValue<decimal>());
        Assert.Equal(0, g.I("unestimatedCount")); Assert.Equal(1, g.I("overdueCount")); Assert.Equal(0, g.I("unavailableStateCount"));
        Assert.Equal(50, g.I("progressPct")); Assert.Equal("Hours", g.S("progressBasis")); Assert.Equal(4, g.I("progressTaskCount"));
        Assert.Equal("2026-09-13", g.S("nextDueDate")); Assert.Equal(0, g.I("undatedOpenCount")); Assert.Equal(2, g.I("pageShown"));
        Assert.True(JsonNode.DeepEquals(Metrics(g), Metrics(Only(second))));
        Assert.Equal(first["items"]!.AsArray().Select(r => r!.G("id")), g["pageTaskIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        Assert.Equal(f.Clock.Now, first["asOf"]!.GetValue<DateTimeOffset>());
        var filtered = Only(await Get(ProjectPath(p) + "?group=discipline&status=Not%20Started"));
        Assert.Equal(2, filtered.I("rowCount")); Assert.Equal(20m, filtered["estimatedHours"]!.GetValue<decimal>()); Assert.Equal(0, filtered.I("progressPct"));
    }

    [Fact]
    public async Task Mixed_zero_and_cancelled_estimates_keep_header_basis_separate_from_progress_denominator()
    {
        var p = await data.Project(); var zero = await Add(p, "Zero complete", 0); await Add(p, "Zero open", 0); await Complete(zero);
        await f.Evaluate(p.Id); await Flag("work_screens", true); var path = ProjectPath(p) + "?group=discipline";
        var allZero = Only(await Get(path)); Assert.Equal("Hours", allZero.S("progressBasis")); Assert.Null(allZero["progressPct"]);
        Assert.Equal(0, allZero.I("unestimatedCount")); Assert.Equal(0m, allZero["estimatedHours"]!.GetValue<decimal>());
        await Add(p, "Unknown estimate", null); var cancelled = await Add(p, "Cancelled estimate", 20);
        await data.Move(TestData.Marc, cancelled, TaskStatuses.Cancelled, new { reason = "Cancel synthetic work" });
        var mixed = Only(await Get(path + "&includeCancelled=true"));
        Assert.Equal(4, mixed.I("rowCount")); Assert.Equal(20m, mixed["estimatedHours"]!.GetValue<decimal>()); Assert.Equal(1, mixed.I("unestimatedCount"));
        Assert.Equal(3, mixed.I("progressTaskCount")); Assert.Equal("Count", mixed.S("progressBasis")); Assert.Equal(30, mixed.I("progressPct"));
        var completeAndCancelled = Only(await Get(path + "&status=Complete,Cancelled"));
        Assert.Equal(2, completeAndCancelled.I("rowCount")); Assert.Equal(20m, completeAndCancelled["estimatedHours"]!.GetValue<decimal>());
        Assert.Equal(1, completeAndCancelled.I("progressTaskCount")); Assert.Null(completeAndCancelled["progressPct"]); Assert.Equal("Hours", completeAndCancelled.S("progressBasis"));
        var onlyCancelled = Only(await Get(path + "&status=Cancelled")); Assert.Equal("NoTasks", onlyCancelled.S("progressBasis")); Assert.Null(onlyCancelled["progressPct"]);
    }

    [Fact]
    public async Task Next_due_includes_held_open_work_and_missing_state_remains_unavailable_without_get_writes()
    {
        var p = await data.Project(); var held = await Add(p, "Held early work", 4, "2026-09-12");
        await data.Move(TestData.Marc, held, TaskStatuses.OnHold, new { reason = "Await synthetic direction" });
        var future = await Add(p, "Future unknown state", 4); await Add(p, "Undated open work", null, null);
        var completed = await Add(p, "Earlier completed work", 2, "2026-09-11"); await Complete(completed); await f.Evaluate(p.Id);
        // Explicit imported/unevaluated-state recovery fixture; never manufacture a zero overdue signal.
        var futureId = future.G("id"); await f.DbAsync(db => db.TaskStates.Where(s => s.TaskId == futureId).ExecuteDeleteAsync());
        await Flag("work_screens", true);
        string Stored() => f.Db(db => JsonSerializer.Serialize(new {
            Tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => new { t.Id, t.RowVersion, t.Status, t.AssigneeId }).ToList(),
            States = db.TaskStates.AsNoTracking().Where(s => db.Tasks.Any(t => t.Id == s.TaskId && t.ProjectId == p.Id)).OrderBy(s => s.TaskId).ToList(),
            Activity = db.ActivityLog.Count(a => a.ProjectId == p.Id)
        }));
        var before = Stored(); var group = Only(await Get(ProjectPath(p) + "?group=discipline"));
        Assert.Equal("2026-09-12", group.S("nextDueDate")); Assert.Equal(1, group.I("undatedOpenCount")); Assert.Equal(1, group.I("unavailableStateCount"));
        Assert.Equal(before, Stored());
        var none = Only(await Get(ProjectPath(p) + "?group=discipline&ids=" + completed.G("id")));
        Assert.Null(none["nextDueDate"]); Assert.Equal(0, none.I("undatedOpenCount"));
    }

    [Fact]
    public async Task Two_hundred_and_one_tasks_share_one_stable_full_group_across_three_pages()
    {
        var p = await data.Project(); var ids = new List<Guid>();
        for (var i = 0; i < 201; i++) ids.Add((await Add(p, $"Normal paged task {i + 1}", 1)).G("id"));
        await Flag("work_screens", true); var found = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await Get($"/api/v1/tasks?projects={p.Id}&group=discipline&sort=key:asc&pageSize=100&page={page}");
            var g = Only(result); Assert.Equal(201, g.I("rowCount")); Assert.Equal(201m, g["estimatedHours"]!.GetValue<decimal>());
            Assert.Equal(page == 3 ? 1 : 100, g.I("pageShown")); Assert.Equal(201, result.I("totalCount"));
            found.AddRange(result["items"]!.AsArray().Select(r => r!.G("id")));
        }
        Assert.Equal(ids, found); Assert.Equal(201, found.Distinct().Count());
        var beyond = await Get($"/api/v1/tasks?projects={p.Id}&group=discipline&sort=key:asc&pageSize=100&page={int.MaxValue}");
        Assert.Empty(beyond["items"]!.AsArray()); Assert.Equal(201, beyond.I("totalCount"));
        Assert.Empty(beyond["groups"]!.AsArray()); Assert.Equal(int.MaxValue, beyond.I("page"));
    }

    [Fact]
    public async Task Three_projects_keep_identical_labels_and_null_memberships_separate_before_paging_and_authorisation()
    {
        var projects = new[] { await data.Project(), await data.Project(), await data.Project() };
        foreach (var p in projects) await Add(p, "Same task name", 2);
        await Flag("work_screens", true); var scope = string.Join(',', projects.Select(p => p.Id));
        var all = await Get($"/api/v1/tasks?projects={scope}&group=deliverable&sort=name:asc&pageSize=1");
        var groups = all["groups"]!.AsArray(); Assert.Single(groups, g => g!.S("dimension") == "project");
        var leaves = groups.Where(g => g!.S("dimension") == "deliverable").ToList();
        for (var page = 2; page <= 3; page++) leaves.AddRange((await Get($"/api/v1/tasks?projects={scope}&group=deliverable&sort=name:asc&pageSize=1&page={page}"))["groups"]!.AsArray().Where(g => g!.S("dimension") == "deliverable"));
        Assert.Equal(3, leaves.Count);
        Assert.All(leaves, g => { Assert.Null(g!["value"]); Assert.Equal(1, g.I("rowCount")); Assert.True(g["referenceAvailable"]!.GetValue<bool>()); });
        Assert.Equal(3, leaves.Select(g => g!.S("key")).Distinct().Count());
        var firstId = Assert.Single(all["items"]!.AsArray())!.G("projectId"); Assert.Equal(projects.OrderBy(p => p.ProjectNumber, StringComparer.Ordinal).First().Id, firstId);
        var hidden = projects[2]; var alex = data.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == hidden.Id && m.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var visible = await Get($"/api/v1/tasks?projects={scope}&group=discipline"); Assert.Equal(2, visible.I("totalCount"));
        Assert.DoesNotContain(hidden.ProjectNumber, visible.ToJsonString()); Assert.Equal(2, visible["groups"]!.AsArray().Count(g => g!.S("dimension") == "discipline"));
        await (await f.As(TestData.Alex).GetAsync(ProjectPath(hidden) + "?group=discipline")).Json(404);
    }

    [Fact]
    public async Task Preset_snapshot_and_normal_commands_keep_the_exact_filtered_group_and_row_permission_authority()
    {
        var managed = await data.Project(); var other = await data.Project(pm: TestData.Marc);
        var task = await Add(managed, "Managed work", 3); await Add(other, "Other PM work", 9);
        await Flag("home_overview", true); await Flag("work_screens", true);
        var scope = $"/api/v1/tasks?projects={managed.Id},{other.Id}&homePreset=ProjectManager&homeProjectStatus=Active&group=discipline";
        var result = await Get(scope, TestData.Pm); Assert.Equal(1, result.I("totalCount")); Assert.Equal(3m, Only(result)["estimatedHours"]!.GetValue<decimal>());
        var id = task.G("id"); var version = f.Db(db => db.Tasks.Single(t => t.Id == id).RowVersion);
        await (await f.As(TestData.Rita).Patch($"/api/v1/tasks/{id}", new { estimatedHours = 6m }, version)).Json(403);
        (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{id}", new { estimatedHours = 6m, dueDate = "2026-09-16" }, version)).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{id}", new { estimatedHours = 10m }, version)).Json(409);
        var refreshed = Only(await Get(scope, TestData.Pm)); Assert.Equal(6m, refreshed["estimatedHours"]!.GetValue<decimal>()); Assert.Equal("2026-09-16", refreshed.S("nextDueDate"));
        var readOnly = await Get(ProjectPath(managed) + "?group=discipline", TestData.Rita);
        Assert.False(Assert.Single(readOnly["items"]!.AsArray())!["can"]!["edit"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Missing_deliverable_metadata_stays_unavailable_and_does_not_become_a_known_empty_milestone()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil");
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Imported source marker", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false })).Json(201);
        var did = deliverable.G("id");
        await data.NewTask(p.Id, extra: new { name = "Task with imported missing reference", assigneeId = data.User(TestData.Alex), deliverableId = did });
        // Explicit damaged-import fixture: a normal delete would correctly refuse a deliverable with tasks.
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(d => d.Id == did)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        await Flag("work_screens", true);
        var d = Only(await Get(ProjectPath(p) + "?group=deliverable"), "deliverable");
        Assert.Equal(did.ToString(), d.S("value")); Assert.False(d["referenceAvailable"]!.GetValue<bool>()); Assert.Null(d["label"]);
        var m = Only(await Get(ProjectPath(p) + "?group=milestone"), "milestone");
        Assert.False(m["referenceAvailable"]!.GetValue<bool>()); Assert.NotNull(m["value"]); Assert.Null(m["label"]);
    }

    [Fact]
    public async Task Grouped_sql_paging_reuses_the_existing_within_group_sort_expressions()
    {
        var p = await data.Project();
        foreach (var (name, estimate, date) in new[] { ("C", 3m, "2026-09-19"), ("A", 1m, "2026-09-17"), ("B", 2m, "2026-09-18") })
        { await Add(p, name, estimate, date); f.Clock.Now = f.Clock.Now.AddMinutes(1); }
        await Flag("work_screens", true);
        foreach (var sort in new[] { "key:asc", "key:desc", "name:asc", "name:desc", "estimate:asc", "estimate:desc", "dueDate:asc", "dueDate:desc", "created:desc", "lastActivity:desc", "board" })
        {
            var path = ProjectPath(p) + $"?sort={sort}&pageSize=2";
            var legacy = await Get(path + "&group=none"); var grouped = await Get(path + "&group=discipline");
            Assert.Equal(legacy["items"]!.AsArray().Select(row => row!.G("id")), grouped["items"]!.AsArray().Select(row => row!.G("id")));
        }
    }

    [Theory]
    [InlineData("assignee")]
    [InlineData("status")]
    [InlineData("due")]
    public async Task Other_group_dimensions_execute_their_real_sql_keys_and_full_metrics(string dimension)
    {
        var p = await data.Project(); var first = await Add(p, "First grouping boundary", 10, "2026-09-13");
        var second = await data.NewTask(p.Id, extra: new { name = "Second grouping boundary", assigneeId = data.User(TestData.Marc), estimatedHours = 20m, dueDate = "2026-09-16", requiresReview = false });
        await data.NewTask(p.Id, extra: new { name = "Unassigned undated grouping boundary", estimatedHours = 5m, requiresReview = false });
        if (dimension == "status")
        {
            await Complete(first);
            await data.Move(TestData.Marc, second, TaskStatuses.OnHold, new { reason = "Held for the grouping boundary test" });
        }
        await f.Evaluate(p.Id); await Flag("work_screens", true);
        var result = await Get(ProjectPath(p) + $"?group={dimension}&sort=key:asc");
        var groups = result["groups"]!.AsArray(); Assert.Equal(3, result.I("totalCount")); Assert.Equal(3, groups.Count);
        Assert.All(groups, group => { Assert.Equal(1, group!.I("rowCount")); Assert.Equal(1, group.I("pageShown")); Assert.True(group["referenceAvailable"]!.GetValue<bool>()); });
        Assert.Equal(35m, groups.Sum(group => group!["estimatedHours"]!.GetValue<decimal>()));
        string?[] expected = dimension switch
        {
            "assignee" => [data.User(TestData.Alex).ToString(), data.User(TestData.Marc).ToString(), null],
            "status" => [TaskStatuses.Complete, TaskStatuses.OnHold, TaskStatuses.NotStarted],
            _ => ["overdue", "thisWeek", "noDate"],
        };
        Assert.Equal(expected.OrderBy(value => value, StringComparer.Ordinal), groups.Select(group => group!["value"]?.GetValue<string>()).OrderBy(value => value, StringComparer.Ordinal));
        if (dimension == "due")
        {
            Assert.Equal(1, groups.Single(group => group!.S("value") == "overdue")!.I("overdueCount"));
            Assert.Equal(1, groups.Single(group => group!.S("value") == "noDate")!.I("undatedOpenCount"));
        }
        if (dimension == "status") Assert.Equal(100, groups.Single(group => group!.S("value") == TaskStatuses.Complete)!.I("progressPct"));
    }

    [Fact]
    public async Task Milestone_groups_follow_direct_and_deliverable_inherited_references_and_refuse_foreign_assignment()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil");
        async Task<JsonNode> Milestone(Project project, string name, string date) => await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones",
            new { name, milestoneType = MilestoneType.ClientWorkshop, date })).Json(201);
        var direct = await Milestone(p, "Direct milestone boundary", "2026-09-25");
        var inherited = await Milestone(p, "Inherited milestone boundary", "2026-09-26");
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Milestone source boundary", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), milestoneId = inherited.G("id"), requiresReview = false })).Json(201);
        var a = await data.NewTask(p.Id, extra: new { name = "Direct milestone task", milestoneId = direct.G("id"), estimatedHours = 3m });
        var b = await data.NewTask(p.Id, extra: new { name = "Inherited milestone task", deliverableId = deliverable.G("id"), estimatedHours = 2m });
        var c = await data.NewTask(p.Id, extra: new { name = "No milestone task", estimatedHours = 1m });
        await Flag("work_screens", true);
        var result = await Get(ProjectPath(p) + "?group=milestone"); var groups = result["groups"]!.AsArray();
        Assert.Equal(3, groups.Count); Assert.Equal(3, result.I("totalCount"));
        foreach (var (task, milestone, hours, label) in new[] { (a, direct, 3m, "Direct milestone boundary"), (b, inherited, 2m, "Inherited milestone boundary") })
        {
            var group = groups.Single(group => group!["value"]?.GetValue<string>() == milestone.G("id").ToString())!;
            Assert.True(group["referenceAvailable"]!.GetValue<bool>()); Assert.Contains(label, group.S("label"));
            Assert.Equal(hours, group["estimatedHours"]!.GetValue<decimal>()); Assert.Equal(task.G("id"), Assert.Single(group["pageTaskIds"]!.AsArray())!.GetValue<Guid>());
        }
        Assert.Equal(c.G("id"), Assert.Single(groups.Single(group => group!["value"] is null)!["pageTaskIds"]!.AsArray())!.GetValue<Guid>());
        var foreign = await Milestone(await data.Project(), "Foreign milestone boundary", "2026-09-27");
        await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/tasks", new { name = "Refused foreign milestone", projectDisciplineId = civil, milestoneId = foreign.G("id") })).Json(400);
        Assert.Equal(3, (await Get(ProjectPath(p) + "?group=milestone")).I("totalCount"));
    }

    [Fact]
    public async Task Disabled_group_parameter_preserves_legacy_bytes_and_enabled_ungrouped_shape()
    {
        var p = await data.Project(); await Add(p, "First", 2); await Add(p, "Second", 4);
        await Flag("small_ux", true); var path = ProjectPath(p) + "?sort=key:asc&pageSize=1&page=2";
        var before = await Get(path); Assert.True(JsonNode.DeepEquals(before, await Get(path + "&group=not-a-group")));
        Assert.Null(before["groups"]); Assert.Null(before["asOf"]);
        Assert.All(before["items"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("board") || row.AsObject().ContainsKey("workActivity")));
        void RemoveAddedFacts(JsonNode page)
        {
            Assert.NotEmpty(page["items"]!.AsArray());
            foreach (var row in page["items"]!.AsArray())
            {
                Assert.Equal("Known", row!["submissionDate"]!.S("state")); Assert.Null(row["submissionDate"]!["targetDate"]);
                Assert.True(row.AsObject().Remove("submissionDate"));
                var board = row["board"]!;
                Assert.Null(board["reviewRequestedAt"]); Assert.Null(board["reviewWaitingDays"]); Assert.Equal(5, board.I("reviewStaleDays"));
                Assert.Equal("Known", board["disciplineLead"]!.S("state")); Assert.Equal(data.User(TestData.Marc), board["disciplineLead"]!.G("id")); Assert.Equal("Marc Dubois", board["disciplineLead"]!.S("name"));
                Assert.Equal("Known", board["projectManager"]!.S("state")); Assert.Equal(data.User(TestData.Pm), board["projectManager"]!.G("id")); Assert.Equal("Priya Nair", board["projectManager"]!.S("name"));
                Assert.True(row.AsObject().Remove("board"));
                // S10 work activity and seen state ride on the same flag (FR-SCR-15, FR-SCR-16).
                foreach (var key in new[] { "workActivity", "seen", "lastActivityActorId", "lastActivityActorType", "lastActivityEventAt" }) row.AsObject().Remove(key);
            }
        }
        await Flag("work_screens", true); var ungrouped = await Get(path + "&group=none"); RemoveAddedFacts(ungrouped);
        Assert.True(JsonNode.DeepEquals(before, ungrouped));
        var enabled = await Get(path + "&group=discipline"); enabled.AsObject().Remove("groups"); enabled.AsObject().Remove("asOf"); RemoveAddedFacts(enabled);
        Assert.True(JsonNode.DeepEquals(before, enabled));
        await (await f.As(TestData.Alex).GetAsync(path + "&group=not-a-group")).Json(400);
        await Flag("work_screens", false); Assert.True(JsonNode.DeepEquals(before, await Get(path + "&group=none")));
    }
}
