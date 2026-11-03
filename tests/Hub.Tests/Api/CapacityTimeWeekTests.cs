using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S32 uses normal time/availability commands; this read creates no suggested entries and changes no capacity input.
public sealed class CapacityTimeWeekTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly W = new(2026, 11, 9);
    public CapacityTimeWeekTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(bool on = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value = on })).EnsureSuccessStatusCode();
    Guid Alex => d.User(TestData.Alex);
    string Url(string extra = "", DateOnly? week = null) => $"/api/v1/time/week?from={week ?? W:yyyy-MM-dd}&to={(week ?? W).AddDays(6):yyyy-MM-dd}{extra}";
    async Task<JsonNode> Week(string extra = "", string actor = TestData.Alex, DateOnly? week = null) => await (await f.As(actor).GetAsync(Url(extra, week))).Json();
    async Task<JsonNode> Add(JsonNode task, DateOnly date, decimal hours, string? note = null, string actor = TestData.Alex)
        => await (await f.As(actor).Post("/api/v1/time", new { taskId = task.G("id"), workDate = date, hours, note })).Json(201);
    async Task Schedule(decimal hours = 40, DateOnly? from = null)
        => (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{Alex}", new { fromDate = from ?? W, weeklyHours = hours, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).EnsureSuccessStatusCode();
    async Task Leave(DateOnly date, decimal? hours, string status = "Approved", string reference = "PRIVATE-HR-REFERENCE")
        => (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = Alex, sourceSystem = "PRIVATE-HR-SOURCE", externalId = reference,
            fromDate = date, throughDate = date, hoursPerDay = hours, status, rowVersion = 0 })).EnsureSuccessStatusCode();
    async Task RemoveMembership(Guid projectId)
    {
        var member = f.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == projectId && row.UserId == Alex && row.RemovedAt == null));
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/projects/{projectId}/members/{member.Id}?reason=Synthetic%20membership%20removal");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{member.RowVersion}\"");
        (await f.As(TestData.Pm).SendAsync(request)).EnsureSuccessStatusCode();
    }
    Task<string> State() => f.DbAsync(async db => JsonSerializer.Serialize(new {
        Entries = await db.TimeEntries.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.UserId, row.TaskId, row.WorkDate, row.Hours, row.Note, row.RowVersion, row.DeletedAt }).ToArrayAsync(),
        Tasks = await db.Tasks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.AssigneeId, row.Status, row.EstimatedHours, row.ProgressPct, row.RowVersion, row.DeletedAt }).ToArrayAsync(),
        Schedules = await db.PersonSchedules.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArrayAsync(),
        Leave = await db.ApprovedLeaves.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.Status, row.HoursPerDay }).ToArrayAsync(),
        Overrides = await db.AvailabilityOverrides.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArrayAsync(),
        Versions = await db.PersonDateVersions.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Receipts = await db.Idempotency.CountAsync()
    }));
    static JsonNode TaskRow(JsonNode week, JsonNode task) => week["tasks"]!.AsArray().Single(row => row!.G("taskId") == task.G("id"))!;
    static JsonNode Day(JsonNode week, DateOnly date) => week["days"]!.AsArray().Single(row => row!.S("date") == date.ToString("yyyy-MM-dd"))!;

    [Fact]
    public async Task Verified_Wednesday_away_shows_capacity32_without_writes_and_still_accepts_Wednesday_time()
    {
        var project = await d.Project(); var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = Alex, estimatedHours = 16m });
        await Schedule(); await Leave(W.AddDays(2), 8); await Flag();
        var before = await State(); var week = await Week();
        Assert.Equal(before, await State());
        Assert.Equal((W.ToString("yyyy-MM-dd"), W.AddDays(6).ToString("yyyy-MM-dd"), Alex, 32m),
            (week.S("from"), week.S("to"), week.G("personId"), week["capacityHours"]!.GetValue<decimal>()));
        Assert.Equal(7, week["days"]!.AsArray().Count);
        var wednesday = Day(week, W.AddDays(2));
        Assert.Equal((8m, 0m, 8m, false, false), (wednesday["normalHours"]!.GetValue<decimal>(), wednesday["availableHours"]!.GetValue<decimal>(),
            wednesday["timeAwayHours"]!.GetValue<decimal>(), wednesday["provisional"]!.GetValue<bool>(), wednesday["unavailable"]!.GetValue<bool>()));
        Assert.True(TaskRow(week, task)["canCreate"]!.GetValue<bool>());
        Assert.DoesNotContain("PRIVATE-HR", week.ToJsonString()); Assert.DoesNotContain("sourceSystem", week.ToJsonString()); Assert.DoesNotContain("workingDays", week.ToJsonString());
        var entry = await Add(task, W.AddDays(2), 2m, "Actual work while away");
        var after = await Week(); var saved = Assert.Single(TaskRow(after, task)["entries"]!.AsArray())!;
        Assert.Equal((entry.G("id"), Alex, 2m), (saved.G("id"), saved.G("userId"), saved["hours"]!.GetValue<decimal>()));
        Assert.Equal(32m, after["capacityHours"]!.GetValue<decimal>());
        Assert.Equal((16m, 0), f.Db(db => db.Tasks.Where(row => row.Id == task.G("id")).Select(row => new { row.EstimatedHours, row.ProgressPct }).AsEnumerable().Select(row => (row.EstimatedHours!.Value, row.ProgressPct)).Single()));
        Assert.Equal(1, f.Db(db => db.ApprovedLeaves.Count()));
    }

    [Fact]
    public async Task Assigned_and_exact_previous_seven_days_deduplicate_and_blank_suggestions_persist_nothing()
    {
        var project = await d.Project(); await Flag();
        var assigned = new List<JsonNode>();
        for (var n = 0; n < 3; n++) assigned.Add(await d.NewTask(project.Id, TestData.Pm, new { assigneeId = Alex }));
        await d.Move(TestData.Pm, assigned[2], TaskStatuses.OnHold, new { reason = "Synthetic open held work" });
        var recent = await d.NewTask(project.Id, TestData.Pm);
        var older = await d.NewTask(project.Id, TestData.Pm);
        await Add(assigned[0], W.AddDays(-7), 1);
        await Add(recent, W.AddDays(-1), 3, "Last week's actual note");
        await Add(older, W.AddDays(-8), 4);
        var before = await State(); var week = await Week();
        Assert.Equal(before, await State()); Assert.Equal(3, f.Db(db => db.TimeEntries.Count()));
        Assert.Equal(assigned.Select(row => row.G("id")).Append(recent.G("id")).Order(), week["tasks"]!.AsArray().Select(row => row!.G("taskId")).Order());
        Assert.All(week["tasks"]!.AsArray(), row => Assert.Empty(row!["entries"]!.AsArray()));
        Assert.True(TaskRow(week, assigned[0])["assigned"]!.GetValue<bool>()); Assert.True(TaskRow(week, assigned[0])["previousWeek"]!.GetValue<bool>());
        Assert.False(TaskRow(week, recent)["assigned"]!.GetValue<bool>()); Assert.True(TaskRow(week, recent)["previousWeek"]!.GetValue<bool>());
        Assert.DoesNotContain("Last week's actual note", week.ToJsonString());
        var entry = await Add(assigned[1], W.AddDays(1), 2);
        var actual = await (await f.As(TestData.Alex).GetAsync($"/api/v1/time?from={W:yyyy-MM-dd}&to={W.AddDays(6):yyyy-MM-dd}")).Json();
        var only = Assert.Single(actual["entries"]!.AsArray())!;
        Assert.Equal((entry.G("id"), Alex, 2m), (only.G("id"), only.G("userId"), only["hours"]!.GetValue<decimal>()));
        Assert.Equal(2m, actual["total"]!.GetValue<decimal>());
        var csv = await f.As(TestData.Alex).GetStringAsync($"/api/v1/time/export?format=csv&from={W:yyyy-MM-dd}&to={W.AddDays(6):yyyy-MM-dd}");
        Assert.Contains(assigned[1].S("key"), csv); Assert.DoesNotContain(recent.S("key"), csv);
        Assert.Single(TaskRow(await Week(), assigned[1])["entries"]!.AsArray());
    }

    [Fact]
    public async Task Current_actuals_survive_deleted_or_unassigned_tasks_and_multiple_entries_keep_IDs_notes_and_versions()
    {
        var project = await d.Project(); await Flag();
        var task = await d.NewTask(project.Id, TestData.Pm);
        var a = await Add(task, W.AddDays(1), 2, "Distinct note A"); var b = await Add(task, W.AddDays(1), 1.5m, "Distinct note B");
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{task.S("id")}")).EnsureSuccessStatusCode();
        var before = await State(); var row = TaskRow(await Week(), task); Assert.Equal(before, await State());
        Assert.True(row["deleted"]!.GetValue<bool>()); Assert.False(row["canCreate"]!.GetValue<bool>());
        Assert.False(row["assigned"]!.GetValue<bool>()); Assert.False(row["previousWeek"]!.GetValue<bool>());
        Assert.Equal(2, row["entries"]!.AsArray().Count); Assert.Equal(3.5m, row["entries"]!.AsArray().Sum(e => e!["hours"]!.GetValue<decimal>()));
        Assert.Equal(new[] { "Distinct note A", "Distinct note B" }, row["entries"]!.AsArray().Select(e => e!.S("note")).Order());
        var saved = await (await f.As(TestData.Alex).Patch($"/api/v1/time/{a.S("id")}", new { hours = 2.25m }, a.I("rowVersion"))).Json();
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Alex).Patch($"/api/v1/time/{a.S("id")}", new { hours = 3m }, a.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Patch($"/api/v1/time/{a.S("id")}", new { hours = 0m }, saved.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Post("/api/v1/time", new { taskId = task.G("id"), workDate = W.AddDays(2), hours = 1m })).StatusCode);
        var rows = TaskRow(await Week(), task)["entries"]!.AsArray();
        Assert.Equal((2.25m, "Distinct note A"), (rows.Single(e => e!.G("id") == a.G("id"))!["hours"]!.GetValue<decimal>(), rows.Single(e => e!.G("id") == a.G("id"))!.S("note")));
        Assert.Equal((1.5m, "Distinct note B"), (rows.Single(e => e!.G("id") == b.G("id"))!["hours"]!.GetValue<decimal>(), rows.Single(e => e!.G("id") == b.G("id"))!.S("note")));
        using var remove = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/time/{a.S("id")}") { Content = JsonContent.Create(new { }) };
        remove.Headers.TryAddWithoutValidation("If-Match", $"\"{saved.I("rowVersion")}\"");
        (await f.As(TestData.Alex).SendAsync(remove)).EnsureSuccessStatusCode();
        Assert.Equal(b.G("id"), Assert.Single(TaskRow(await Week(), task)["entries"]!.AsArray())!.G("id"));
    }

    [Fact]
    public async Task Own_only_exact_week_and_scoped_union_do_not_expose_hidden_or_other_person_history()
    {
        var project = await d.Project(); var hidden = await d.Project(); await Flag();
        var visible = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = Alex });
        var secret = await d.NewTask(hidden.Id, TestData.Pm, new { assigneeId = Alex, name = "SECRET TIME TASK" });
        await Add(secret, W.AddDays(-1), 2, "SECRET PREVIOUS NOTE"); await Add(secret, W, 3, "SECRET CURRENT NOTE");
        await Add(visible, W, 1, actor: TestData.Pm);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
        await RemoveMembership(hidden.Id);
        var week = await Week(); Assert.Equal(visible.G("id"), Assert.Single(week["tasks"]!.AsArray())!.G("taskId"));
        Assert.Empty(TaskRow(week, visible)["entries"]!.AsArray());
        foreach (var value in new[] { "SECRET", secret.S("id"), hidden.Id.ToString() }) Assert.DoesNotContain(value, week.ToJsonString());
        Assert.Empty((await Week($"&projectId={hidden.Id}"))["tasks"]!.AsArray());
        Assert.Empty((await Week($"&taskId={secret.S("id")}"))["tasks"]!.AsArray());
        foreach (var query in new[] { "&scope=all", $"&userId={d.User(TestData.Jill)}" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).GetAsync(Url(query))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).GetAsync("/api/v1/time/week?from=2026-11-10&to=2026-11-16")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).GetAsync("/api/v1/time/week?from=2026-11-09&to=2026-11-22")).StatusCode);
        (await f.As(TestData.Pm).GetAsync("/api/v1/time?from=2026-11-10&to=2026-11-22&scope=all")).EnsureSuccessStatusCode();
        var readOnly = await Week(actor: TestData.Rita); Assert.Equal(d.User(TestData.Rita), readOnly.G("personId")); Assert.Empty(readOnly["tasks"]!.AsArray());
        Assert.DoesNotContain("SECRET", readOnly.ToJsonString());
    }

    [Fact]
    public async Task Row_union_is_complete_beyond_default_task_page_and_filters_do_not_broaden_empty_scope()
    {
        var project = await d.Project(); await Flag(); var tasks = new List<JsonNode>();
        for (var n = 0; n < 56; n++) tasks.Add(await d.NewTask(project.Id, TestData.Pm, new { assigneeId = Alex }));
        var week = await Week($"&projectId={project.Id}");
        Assert.Equal(tasks.Select(task => task.G("id")).Order(), week["tasks"]!.AsArray().Select(row => row!.G("taskId")).Order());
        Assert.Equal(tasks[55].G("id"), Assert.Single((await Week($"&taskId={tasks[55].S("id")}"))["tasks"]!.AsArray())!.G("taskId"));
        Assert.Empty((await Week($"&projectId={Guid.NewGuid()}"))["tasks"]!.AsArray());
        Assert.Equal(0, f.Db(db => db.TimeEntries.Count()));
    }

    [Fact]
    public async Task Canonical_partial_leave_holiday_extra_hours_and_effective_schedule_are_not_subtracted_twice()
    {
        await d.Project(); await Schedule(); await Schedule(20, W.AddDays(3)); await Flag();
        await Leave(W.AddDays(2), 4);
        (await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { date = W, name = "PRIVATE HOLIDAY NAME" })).EnsureSuccessStatusCode();
        (await f.As(TestData.Sam).Put($"/api/v1/users/{Alex}/availability/{W.AddDays(1):yyyy-MM-dd}", new { expectedRowVersion = 0, availableHours = 10m, category = "Additional" })).EnsureSuccessStatusCode();
        var before = await State(); var week = await Week(); Assert.Equal(before, await State());
        Assert.Equal(22m, week["capacityHours"]!.GetValue<decimal>());
        Assert.Equal(new[] { 0m, 10m, 4m, 4m, 4m, 0m, 0m }, week["days"]!.AsArray().Select(day => day!["availableHours"]!.GetValue<decimal>()));
        Assert.Equal(8m, Day(week, W)["holidayHours"]!.GetValue<decimal>());
        Assert.Equal(4m, Day(week, W.AddDays(2))["timeAwayHours"]!.GetValue<decimal>());
        Assert.Equal(2m, Day(week, W.AddDays(1))["additionalHours"]!.GetValue<decimal>());
        Assert.DoesNotContain("PRIVATE", week.ToJsonString());
    }

    [Fact]
    public async Task Provisional_daily_context_makes_week_capacity_unknown_while_known_zero_and_inherited_default_remain_numeric()
    {
        await d.Project(); await Flag();
        var user = (await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users?q={TestData.Alex}")).Json()).AsArray().Single(row => row!.G("id") == Alex)!;
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{Alex}", new { weeklyCapacityHours = (decimal?)null }, user.I("rowVersion"))).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/default_weekly_capacity_hours", new { value = 40 })).EnsureSuccessStatusCode();
        Assert.Equal(40m, (await Week())["capacityHours"]!.GetValue<decimal>());
        (await f.As(TestData.Sam).Put($"/api/v1/users/{Alex}/availability/{W.AddDays(1):yyyy-MM-dd}", new { expectedRowVersion = 0, availableHours = 4m, category = "Reduced" })).EnsureSuccessStatusCode();
        var provisional = await Week(); Assert.Null(provisional["capacityHours"]);
        Assert.True(Day(provisional, W.AddDays(1))["provisional"]!.GetValue<bool>());
        Assert.Equal((4m, 4m), (Day(provisional, W.AddDays(1))["availableHours"]!.GetValue<decimal>(), Day(provisional, W.AddDays(1))["timeAwayHours"]!.GetValue<decimal>()));
        // A distinct week has no provisional override. A real zero-hour schedule is known zero, not missing capacity.
        await Schedule(0, W.AddDays(7));
        var zero = await Week(week: W.AddDays(7)); Assert.Equal(0m, zero["capacityHours"]!.GetValue<decimal>());
        Assert.All(zero["days"]!.AsArray(), day => { Assert.False(day!["unavailable"]!.GetValue<bool>()); Assert.False(day["provisional"]!.GetValue<bool>()); Assert.Equal(0m, day["availableHours"]!.GetValue<decimal>()); });
    }

    [Fact]
    public async Task Existing_create_receipt_daily_cap_and_permission_guards_remain_authoritative_and_off_read_is_absent()
    {
        var project = await d.Project(); var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = Alex });
        var old = await (await f.As(TestData.Alex).GetAsync($"/api/v1/time?from={W:yyyy-MM-dd}&to={W.AddDays(6):yyyy-MM-dd}")).Json();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync(Url())).StatusCode); await Flag();
        var key = Guid.NewGuid().ToString();
        async Task<JsonNode> CreateOnce() {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/time") { Content = JsonContent.Create(new { taskId = task.G("id"), workDate = W.AddDays(1), hours = 2m }) };
            request.Headers.Add("Idempotency-Key", key); return await (await f.As(TestData.Alex).SendAsync(request)).Json(201);
        }
        var first = await CreateOnce(); var retry = await CreateOnce(); Assert.Equal(first.G("id"), retry.G("id"));
        Assert.Equal(1, f.Db(db => db.TimeEntries.Count()));
        var simultaneous = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => f.As(TestData.Alex).Post("/api/v1/time", new { taskId = task.G("id"), workDate = W.AddDays(2), hours = 13m })));
        Assert.Single(simultaneous.Where(response => response.IsSuccessStatusCode));
        Assert.Equal(13m, f.Db(db => db.TimeEntries.Where(entry => entry.WorkDate == W.AddDays(2)).Sum(entry => entry.Hours)));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/time", new { taskId = task.G("id"), workDate = W, hours = 1m })).StatusCode);
        await RemoveMembership(project.Id);
        Assert.False(TaskRow(await Week(), task)["canCreate"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/time", new { taskId = task.G("id"), workDate = W, hours = 1m })).StatusCode);
        await Flag(false); Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync(Url())).StatusCode);
        var off = await (await f.As(TestData.Alex).GetAsync($"/api/v1/time?from={W:yyyy-MM-dd}&to={W.AddDays(6):yyyy-MM-dd}")).Json();
        Assert.Equal(old.AsObject().Select(row => row.Key), off.AsObject().Select(row => row.Key));
        Assert.Equal(15m, off["total"]!.GetValue<decimal>());
    }
}
