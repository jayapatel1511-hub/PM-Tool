using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkBoardCompletenessTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkBoardCompletenessTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Get(string path, string who = TestData.Alex) => await (await f.As(who).GetAsync(path)).Json();
    async Task Flag(string key, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value = on })).EnsureSuccessStatusCode();
    static JsonNode Row(JsonNode page, Guid id) => Assert.Single(page["items"]!.AsArray(), row => row!.G("id") == id)!;
    async Task<JsonNode> Add(Project p, string name, decimal? estimate = 2, bool review = false) => await data.NewTask(p.Id,
        extra: new { name, assigneeId = data.User(TestData.Alex), reviewerId = review ? (Guid?)data.User(TestData.Diane) : null, requiresReview = review, estimatedHours = estimate });
    async Task<JsonNode> Ready(Project p, string name)
    {
        var task = await Add(p, name, review: true); await data.Move(TestData.Alex, task, TaskStatuses.InProgress); await data.Move(TestData.Alex, task, TaskStatuses.ReadyForReview); return task;
    }
    static void Lead(JsonNode board, string kind, Guid id, string name)
    {
        Assert.Equal("Known", board[kind]!.S("state")); Assert.Equal(id, board[kind]!.G("id")); Assert.Equal(name, board[kind]!.S("name"));
    }

    [Fact]
    public async Task Ready_entry_survives_new_activity_and_In_Review_with_five_and_six_day_age_without_changing_canonical_threshold()
    {
        var p = await data.Project(); var task = await Ready(p, "Review from original entry"); var id = task.G("id");
        var entered = f.Db(db => db.Tasks.Single(row => row.Id == id).ReviewRequestedAt); Assert.NotNull(entered);
        await Flag("work_screens", true); f.Clock.Now = f.Clock.Now.AddDays(5);
        (await f.As(TestData.Alex).Post($"/api/v1/items/Task/{id}/comments", new { body = "A normal new comment does not restart review waiting." })).EnsureSuccessStatusCode();
        await f.Evaluate(p.Id);
        var atThreshold = Row(await Get($"/api/v1/projects/{p.Id}/tasks"), id);
        Assert.Equal(5, atThreshold["board"]!.I("reviewWaitingDays")); Assert.Equal(5, atThreshold["board"]!.I("reviewStaleDays"));
        Assert.False(atThreshold["state"]!["isReviewStalled"]!.GetValue<bool>()); // Canonical R-05 is still >; FR21 amber presentation is >=.
        await data.Move(TestData.Diane, task, TaskStatuses.InReview); f.Clock.Now = f.Clock.Now.AddDays(1);
        var read = Row(await Get($"/api/v1/tasks?projects={p.Id}"), id);
        Assert.Equal(TaskStatuses.InReview, read.S("status")); Assert.Equal(6, read["board"]!.I("reviewWaitingDays"));
        Assert.Equal(entered, read["board"]!["reviewRequestedAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal(data.User(TestData.Diane), read.G("reviewerId")); Assert.Equal("Diane Roy", read.S("reviewerName"));
        Assert.Equal(entered, f.Db(db => db.Tasks.Single(row => row.Id == id).ReviewRequestedAt));
        var detail = await Get($"/api/v1/tasks/{id}"); Assert.Equal(entered, detail["reviewRequestedAt"]!.GetValue<DateTimeOffset>());
    }

    [Fact]
    public async Task Five_normal_in_progress_cards_supply_twelve_hours_one_overdue_and_actual_lead_without_invented_totals()
    {
        var p = await data.Project(); var estimates = new[] { 0m, 2m, 3m, 3m, 4m }; var ids = new List<Guid>();
        var date = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        for (var i = 0; i < estimates.Length; i++)
        {
            var task = await data.NewTask(p.Id, extra: new { name = $"Header card {i + 1}", assigneeId = data.User(TestData.Alex), estimatedHours = estimates[i], dueDate = date.AddDays(i == 0 ? -1 : 7) });
            await data.Move(TestData.Alex, task, TaskStatuses.InProgress); ids.Add(task.G("id"));
        }
        await f.Evaluate(p.Id); await Flag("work_screens", true);
        var read = await Get($"/api/v1/projects/{p.Id}/tasks?status=In%20Progress&sort=key:asc"); var cards = read["items"]!.AsArray();
        Assert.Equal(5, read.I("totalCount")); Assert.Equal(5, cards.Count); Assert.Equal(ids, cards.Select(row => row!.G("id")));
        Assert.Equal(12m, cards.Sum(row => row!["estimatedHours"]!.GetValue<decimal>())); Assert.Equal(1, cards.Count(row => row!["state"]!["isOverdue"]!.GetValue<bool>()));
        foreach (var row in cards) { Lead(row!["board"]!, "disciplineLead", data.User(TestData.Marc), "Marc Dubois"); Lead(row["board"]!, "projectManager", data.User(TestData.Pm), "Priya Nair"); }
        var unknown = await Add(p, "Unestimated shown separately", null); await data.Move(TestData.Alex, unknown, TaskStatuses.InProgress);
        var mixed = (await Get($"/api/v1/projects/{p.Id}/tasks?status=In%20Progress"))["items"]!.AsArray();
        Assert.Equal(6, mixed.Count); Assert.Equal(1, mixed.Count(row => row!["estimatedHours"] is null)); Assert.Equal(12m, mixed.Sum(row => row!["estimatedHours"]?.GetValue<decimal>() ?? 0));
    }

    [Fact]
    public async Task Distinct_same_named_disciplines_and_projects_keep_current_recorded_leads_and_actual_null_unassigned()
    {
        var first = await data.Project(); var second = await data.Project(pm: TestData.Marc);
        var a = await Add(first, "Same named work"); var b = await Add(second, "Same named work"); await Flag("work_screens", true);
        var path = $"/api/v1/tasks?projects={first.Id},{second.Id}&sort=key:asc";
        var read = await Get(path); var ar = Row(read, a.G("id")); var br = Row(read, b.G("id"));
        Assert.Equal(ar.S("disciplineName"), br.S("disciplineName")); Assert.NotEqual(ar.G("projectDisciplineId"), br.G("projectDisciplineId"));
        Lead(ar["board"]!, "projectManager", data.User(TestData.Pm), "Priya Nair"); Lead(br["board"]!, "projectManager", data.User(TestData.Marc), "Marc Dubois");
        var pd = data.ProjectDiscipline(first.Id, "Civil"); var version = f.Db(db => db.ProjectDisciplines.Single(row => row.Id == pd).RowVersion);
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{first.Id}/disciplines/{pd}", new { leadUserId = (Guid?)null }, version)).EnsureSuccessStatusCode();
        var changed = await Get(path); Assert.Equal("Unassigned", Row(changed, a.G("id"))["board"]!["disciplineLead"]!.S("state"));
        Lead(Row(changed, b.G("id"))["board"]!, "disciplineLead", data.User(TestData.Marc), "Marc Dubois");
    }

    [Fact]
    public async Task Missing_or_future_recorded_review_time_is_unavailable_and_missing_state_is_never_repaired_by_get()
    {
        var p = await data.Project(); var a = await Ready(p, "Imported missing review timestamp"); var b = await Ready(p, "Imported future review timestamp");
        var aid = a.G("id"); var bid = b.G("id");
        // Explicit damaged-import recovery fixtures; normal transition behavior is tested above, never proved by these edits.
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(row => row.Id == aid)).ReviewRequestedAt = null;
            (await db.Tasks.SingleAsync(row => row.Id == bid)).ReviewRequestedAt = f.Clock.Now.AddDays(1); await db.SaveChangesAsync(); return 0; });
        await f.DbAsync(db => db.TaskStates.Where(state => state.TaskId == aid).ExecuteDeleteAsync()); await Flag("work_screens", true);
        string Stored() => f.Db(db => JsonSerializer.Serialize(new {
            Tasks = db.Tasks.AsNoTracking().Where(row => row.ProjectId == p.Id).OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.Status, row.ReviewRequestedAt, row.LastActivityAt }).ToArray(),
            States = db.TaskStates.Count(state => state.TaskId == aid || state.TaskId == bid), Activity = db.ActivityLog.Count(log => log.ProjectId == p.Id)
        }));
        var before = Stored(); var read = await Get($"/api/v1/projects/{p.Id}/tasks");
        Assert.Null(Row(read, aid)["board"]!["reviewWaitingDays"]); Assert.Null(Row(read, bid)["board"]!["reviewWaitingDays"]);
        Assert.Null(Row(read, aid)["state"]); Assert.Equal(before, Stored());
    }

    [Fact]
    public async Task Foreign_discipline_reference_and_missing_current_lead_name_stay_unavailable_instead_of_falling_back_to_pm()
    {
        var p = await data.Project(); var other = await data.Project(); var a = await Add(p, "Foreign discipline recovery"); var b = await Add(p, "Missing lead name recovery");
        var aid = a.G("id"); var foreignPd = data.ProjectDiscipline(other.Id, "Civil"); var marc = data.User(TestData.Marc);
        // Recovery-only imported references and damaged current name. No normal command can create these inputs.
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(row => row.Id == aid)).ProjectDisciplineId = foreignPd;
            (await db.Users.SingleAsync(person => person.Id == marc)).DisplayName = ""; return await db.SaveChangesAsync(); });
        await Flag("work_screens", true); var page = await Get($"/api/v1/projects/{p.Id}/tasks");
        Assert.Equal("Unavailable", Row(page, aid)["board"]!["disciplineLead"]!.S("state"));
        Assert.Equal("Unavailable", Row(page, b.G("id"))["board"]!["disciplineLead"]!.S("state"));
        Lead(Row(page, aid)["board"]!, "projectManager", data.User(TestData.Pm), "Priya Nair");
    }

    [Fact]
    public async Task Manual_order_guarded_transitions_and_restricted_scope_are_unchanged_by_board_facts()
    {
        var p = await data.Project(); var a = await Add(p, "First board work"); var b = await Add(p, "Second board work"); var c = await Add(p, "Third board work");
        await Flag("work_screens", true); var order = new[] { c.G("id"), a.G("id"), b.G("id") };
        (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/board-order", new { taskIds = order })).EnsureSuccessStatusCode();
        Assert.Equal(order, (await Get($"/api/v1/projects/{p.Id}/tasks?sort=board"))["items"]!.AsArray().Select(row => row!.G("id")));
        await (await f.As(TestData.Rita).Post($"/api/v1/tasks/{a.G("id")}/transition", new { toStatus = TaskStatuses.InProgress, rowVersion = await data.TaskVersion(a) })).Json(403);
        await data.Move(TestData.Alex, a, TaskStatuses.InProgress); Assert.Equal(TaskStatuses.InProgress, Row(await Get($"/api/v1/projects/{p.Id}/tasks"), a.G("id")).S("status"));
        var hidden = await data.Project(); await Add(hidden, "Hidden board work"); var alex = data.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == hidden.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(member => member.ProjectId == hidden.Id && member.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var scoped = await Get($"/api/v1/tasks?projects={p.Id},{hidden.Id}"); Assert.Equal(3, scoped.I("totalCount")); Assert.DoesNotContain(hidden.ProjectNumber, scoped.ToJsonString());
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{hidden.Id}/tasks")).Json(404);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Board_fact_is_independent_of_screen_patterns_and_other_rows_remain_unchanged(bool work, bool patterns)
    {
        var p = await data.Project(); var task = await Ready(p, "Independent flag review"); var id = task.G("id");
        await Flag("screen_patterns", patterns); var before = (await Get($"/api/v1/projects/{p.Id}/tasks")).ToJsonString(); await Flag("work_screens", work);
        foreach (var path in new[] { $"/api/v1/projects/{p.Id}/tasks", $"/api/v1/tasks?projects={p.Id}" })
        {
            var row = Row(await Get(path), id); if (work) { Assert.NotNull(row["board"]); Assert.Equal(0, row["board"]!.I("reviewWaitingDays")); } else Assert.False(row.AsObject().ContainsKey("board"));
        }
        var mine = await Get("/api/v1/me/work"); Assert.False(Assert.Single(mine["tasks"]!.AsArray(), row => row!.G("id") == id)!.AsObject().ContainsKey("board"));
        await Flag("work_screens", false); Assert.Equal(before, (await Get($"/api/v1/projects/{p.Id}/tasks")).ToJsonString());
        var empty = await Get($"/api/v1/projects/{p.Id}/tasks?ids={Guid.NewGuid()}"); Assert.Empty(empty["items"]!.AsArray());
    }
}
