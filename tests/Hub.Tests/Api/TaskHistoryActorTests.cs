using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class TaskHistoryActorTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public TaskHistoryActorTests() { d = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value })).Json();
    async Task<JsonNode> Get(string path, string who = TestData.Pm, int expect = 200) => await (await f.As(who).GetAsync(path)).Json(expect);
    static string History(Guid id, string query = "") => $"/api/v1/items/Task/{id}/activity{query}";
    static string Actors(Guid id) => $"/api/v1/tasks/{id}/activity/actors";
    string Stored() => f.Db(db => JsonSerializer.Serialize(new {
        Logs = db.ActivityLog.AsNoTracking().OrderBy(a => a.Id).ToArray(), Tasks = db.Tasks.AsNoTracking().OrderBy(t => t.Id).ToArray(),
        Contributions = db.Set<TaskContribution>().AsNoTracking().OrderBy(c => c.Id).ToArray(),
        Assignments = db.WorkAssignmentOccurrences.OrderBy(r => r.Token).ToArray(), Seen = db.WorkSeenAcknowledgements.OrderBy(r => r.RecipientId).ThenBy(r => r.Token).ToArray(),
        Outbox = db.Outbox.OrderBy(r => r.Id).ToArray(), Notifications = db.Notifications.OrderBy(r => r.Id).ToArray()
    }));
    ActivityLog Log(Guid project, string type, Guid id, string key, Guid? actor, DateTimeOffset? at = null) => new()
    {
        ProjectId = project, ItemType = type, ItemId = id, ItemKey = key, ItemName = "Synthetic retained history",
        ActorUserId = actor, ActorType = actor is null ? "System" : "User", Action = "Updated", Categories = ["general"], OccurredAt = at ?? f.Clock.Now
    };

    [Fact]
    public async Task Actor_choices_use_full_history_beyond_200_include_inactive_and_filter_before_exact_paging_without_system_identity()
    {
        await Flag(true); var project = await d.Project(); var task = await d.NewTask(project.Id); var id = task.G("id");
        var users = Enumerable.Range(0, 205).Select(index => new AppUser { Email = $"synthetic-history-{index}@hub.test", DisplayName = $"Synthetic history actor {index:000}", IsActive = index != 0 }).ToArray();
        var system = Log(project.Id, ItemType.Task, id, task.S("key"), null, f.Clock.Now.AddHours(1));
        var old = users[0]; var oldRows = Enumerable.Range(0, 3).Select(index => Log(project.Id, ItemType.Task, id, task.S("key"), old.Id, f.Clock.Now.AddDays(-10).AddMinutes(index))).ToArray();
        await f.DbAsync(async db => { // Labelled synthetic historical cohort in this test's disposable database, not a directory mock.
            db.Users.AddRange(users); db.ActivityLog.AddRange(users.Skip(1).Select(user => Log(project.Id, ItemType.Task, id, task.S("key"), user.Id)));
            db.ActivityLog.AddRange(oldRows); db.ActivityLog.Add(system); return await db.SaveChangesAsync(); });
        var before = Stored(); var first = await Get(History(id, "?pageSize=200&page=1"));
        Assert.Equal(209, first.I("totalCount")); Assert.Equal(200, first["items"]!.AsArray().Count);
        Assert.DoesNotContain(first["items"]!.AsArray(), row => row!["actorId"]?.GetValue<Guid>() == old.Id);
        Assert.Contains(first["items"]!.AsArray(), row => row!.G("id") == system.Id && row["actorId"] is null && row.S("actorType") == "System");
        var choices = (await Get(Actors(id))).AsArray(); Assert.Equal(206, choices.Count);
        Assert.DoesNotContain(choices, row => row!.G("id") == Guid.Empty || row.G("id") == d.User(TestData.Diane));
        var historical = Assert.Single(choices, row => row!.G("id") == old.Id)!;
        Assert.Equal(old.DisplayName, historical.S("displayName")); Assert.False(historical["isActive"]!.GetValue<bool>());
        Assert.Equal(users.Select(u => u.Id).Append(d.User(TestData.Marc)).Order(), choices.Select(row => row!.G("id")).Order());
        var one = await Get(History(id, $"?actorId={old.Id}&pageSize=2&page=1")); var two = await Get(History(id, $"?actorId={old.Id}&pageSize=2&page=2"));
        Assert.Equal(3, one.I("totalCount")); Assert.Equal(3, two.I("totalCount")); Assert.Equal(2, one["items"]!.AsArray().Count); Assert.Single(two["items"]!.AsArray());
        var actual = one["items"]!.AsArray().Concat(two["items"]!.AsArray()).ToArray();
        Assert.All(actual, row => { Assert.Equal(old.Id, row!.G("actorId")); Assert.NotEqual("System", row.S("actorType")); });
        Assert.Equal(oldRows.OrderByDescending(row => row.OccurredAt).ThenByDescending(row => row.Id).Select(row => row.Id), actual.Select(row => row!.G("id")));
        Assert.Equal(before, Stored());
    }

    [Fact]
    public async Task Cohort_and_filter_reuse_direct_same_key_dependency_and_source_privacy_boundaries_in_the_actual_project()
    {
        await Flag(true); var project = await d.Project(); var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var id = task.G("id"); var key = task.S("key"); var actor = d.User(TestData.Alex); var viewer = TestData.Diane;
        await (await f.As(TestData.Alex).Post($"/api/v1/items/Task/{id}/comments", new { body = "Synthetic task discussion" })).Json(201);
        // Retained link/dependency history includes removed children, so source presence is not required for these rows.
        var link = Log(project.Id, ItemType.DocumentLink, Guid.NewGuid(), key, d.User(TestData.Omar));
        var incoming = Log(project.Id, ItemType.Dependency, Guid.NewGuid(), $"OTHER-T0001 → {key}", d.User(TestData.Jill));
        var outgoing = Log(project.Id, ItemType.Dependency, Guid.NewGuid(), $"{key} → OTHER-T0002", d.User(TestData.Jill));
        var prefixCollision = Log(project.Id, ItemType.Dependency, Guid.NewGuid(), $"{key}0 → OTHER-T0002", d.User(TestData.Sam));
        var other = await d.Project(); var foreign = Log(other.Id, ItemType.Task, Guid.NewGuid(), key, d.User(TestData.Lena));
        var time = await (await f.As(TestData.Alex).Post("/api/v1/time", new { taskId = id, workDate = "2026-11-02", hours = 1m, note = "Synthetic private history time" })).Json(201);
        var calendar = await (await f.As(TestData.Alex).Post("/api/v1/calendar/events", new { projectId = project.Id, type = "Meeting", title = "Synthetic private calendar history",
            start = "2026-11-02T09:00", end = "2026-11-02T10:00", visibility = "Private" })).Json(201);
        var privateEvent = Log(project.Id, ItemType.CalendarEvent, calendar.G("id"), key, d.User(TestData.Rita));
        await f.DbAsync(async db => { db.ActivityLog.AddRange(link, incoming, outgoing, prefixCollision, foreign, privateEvent); return await db.SaveChangesAsync(); });
        var before = Stored(); var rows = await Get(History(id, "?pageSize=200"), viewer); var choices = (await Get(Actors(id), viewer)).AsArray();
        Assert.Contains(rows["items"]!.AsArray(), row => row!.G("id") == link.Id);
        Assert.Contains(rows["items"]!.AsArray(), row => row!.G("id") == incoming.Id); Assert.Contains(rows["items"]!.AsArray(), row => row!.G("id") == outgoing.Id);
        Assert.Contains(rows["items"]!.AsArray(), row => row!.S("itemType") == ItemType.Comment && row.G("actorId") == actor);
        foreach (var hidden in new[] { privateEvent.Id, foreign.Id, prefixCollision.Id }) Assert.DoesNotContain(rows["items"]!.AsArray(), row => row!.G("id") == hidden);
        Assert.DoesNotContain(rows["items"]!.AsArray(), row => row!.G("itemId") == time.G("id"));
        Assert.Equal(rows["items"]!.AsArray().Count, rows.I("totalCount"));
        Assert.Equal(new[] { d.User(TestData.Marc), actor, d.User(TestData.Omar), d.User(TestData.Jill) }.Order(), choices.Select(row => row!.G("id")).Order());
        foreach (var hiddenActor in new[] { TestData.Rita, TestData.Lena, TestData.Sam })
            Assert.Equal(0, (await Get(History(id, $"?actorId={d.User(hiddenActor)}"), viewer)).I("totalCount"));
        var jill = await Get(History(id, $"?actorId={d.User(TestData.Jill)}&pageSize=1&page=2"), viewer);
        Assert.Equal(2, jill.I("totalCount")); Assert.Single(jill["items"]!.AsArray());
        var owner = await Get(History(id, $"?actorId={actor}&pageSize=200"), TestData.Alex);
        Assert.Contains(owner["items"]!.AsArray(), row => row!.G("itemId") == time.G("id")); Assert.Equal(before, Stored());
    }

    [Fact]
    public async Task Canonical_task_key_finds_first_contribution_history_without_any_prior_parent_task_event()
    {
        await Flag(true); var project = await d.Project(); var source = await d.NewTask(project.Id); var id = Guid.NewGuid();
        var member = new TaskCollaborator { TaskId = id, UserId = d.User(TestData.Jill), AddedAt = f.Clock.Now };
        var key = $"{project.ProjectNumber}-T0501";
        // Explicit legacy/import fixture: clone an actual valid record without manufacturing or deleting a Task audit row.
        await f.DbAsync(async db => {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hub.task
                SELECT clone.* FROM hub.task t
                CROSS JOIN LATERAL jsonb_populate_record(NULL::hub.task, to_jsonb(t) || jsonb_build_object(
                    'id', {id}, 'seq', 501, 'name', 'Synthetic imported task without prior log', 'key', {key})) AS clone
                WHERE t.id={source.G("id")}
                """);
            db.Collaborators.Add(member); return await db.SaveChangesAsync(); });
        Assert.False(f.Db(db => db.ActivityLog.Any(row => row.ItemId == id)));
        await (await f.As(TestData.Jill).Patch($"/api/v1/tasks/{id}/contribution", new { collaboratorId = member.Id, rowVersion = 0, marked = true, note = "Synthetic first historical child" })).Json();
        Assert.False(f.Db(db => db.ActivityLog.Any(row => row.ItemType == ItemType.Task && row.ItemId == id)));
        var read = await Get(History(id)); Assert.Equal(1, read.I("totalCount"));
        Assert.Equal("TaskContribution", Assert.Single(read["items"]!.AsArray())!.S("itemType"));
        Assert.Equal(d.User(TestData.Jill), Assert.Single((await Get(Actors(id))).AsArray())!.G("id"));
    }

    [Fact]
    public async Task New_actor_query_is_task_flag_scoped_unknown_is_empty_malformed_is_refused_and_off_preserves_legacy_wire()
    {
        var project = await d.Project(); var task = await d.NewTask(project.Id); var id = task.G("id"); await Flag(false);
        var baseline = await Get(History(id)); var bytes = Stored();
        Assert.True(JsonNode.DeepEquals(baseline, await Get(History(id, "?actorId=not-a-guid"))));
        await Get(Actors(id), expect: 404); Assert.Equal(bytes, Stored());
        await Flag(true);
        foreach (var bad in new[] { "not-a-guid", Guid.Empty.ToString(), "Task:bad" }) await Get(History(id, $"?actorId={Uri.EscapeDataString(bad)}"), expect: 400);
        var unknown = await Get(History(id, $"?actorId={Guid.NewGuid()}")); Assert.Equal(0, unknown.I("totalCount")); Assert.Empty(unknown["items"]!.AsArray());
        var projectHistory = await Get($"/api/v1/items/Project/{project.Id}/activity");
        Assert.True(JsonNode.DeepEquals(projectHistory, await Get($"/api/v1/items/Project/{project.Id}/activity?actorId=not-a-guid")));
        Assert.True(JsonNode.DeepEquals(baseline, await Get(History(id))));
        var stored = Stored(); await Get(Actors(Guid.NewGuid()), expect: 404); await Get(History(Guid.NewGuid()), expect: 404); Assert.Equal(stored, Stored());
    }

    [Fact]
    public async Task Restricted_project_current_visibility_governs_actor_choices_and_history_without_directory_leakage()
    {
        await Flag(true); var project = await d.Project(); var task = await d.NewTask(project.Id); var id = task.G("id");
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { visibility = Visibility.Restricted }, d.Version(project.Id))).Json();
        await Get(Actors(id), TestData.Alex); await Get(History(id), TestData.Alex);
        await Get($"/api/v1/projects/{project.Id}", TestData.Jill, 404); var before = Stored();
        await Get(Actors(id), TestData.Jill, 404); await Get(History(id), TestData.Jill, 404); Assert.Equal(before, Stored());
        await f.DbAsync(async db => { var member = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null);
            member.RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        before = Stored(); await Get(Actors(id), TestData.Alex, 404); await Get(History(id), TestData.Alex, 404); Assert.Equal(before, Stored());
    }

    [Fact]
    public async Task Stored_contribution_only_actor_is_removed_before_history_paging_and_counts_while_off()
    {
        await Flag(true); var project = await d.Project(); var task = await d.NewTask(project.Id); var id = task.G("id");
        await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/collaborators", new { userId = d.User(TestData.Jill) })).Json(204);
        var member = f.Db(db => db.Collaborators.Single(c => c.TaskId == id && c.UserId == d.User(TestData.Jill)).Id);
        await (await f.As(TestData.Jill).Patch($"/api/v1/tasks/{id}/contribution", new { collaboratorId = member, rowVersion = 0, marked = true, note = "Synthetic exclusively contribution actor" })).Json();
        var before = f.Db(db => db.ActivityLog.Where(row => row.ItemType == "TaskContribution").Select(row => new { row.Id, row.Changes, row.RowHash, row.ItemKey }).ToArray());
        var on = await Get(History(id, $"?actorId={d.User(TestData.Jill)}&pageSize=1")); Assert.Equal(1, on.I("totalCount")); Assert.Single(on["items"]!.AsArray());
        Assert.Contains((await Get(Actors(id))).AsArray(), row => row!.G("id") == d.User(TestData.Jill));
        await Flag(false); var off = await Get(History(id, "?pageSize=1&page=1"));
        var expected = f.Db(db => db.ActivityLog.Count(row => row.ProjectId == project.Id && row.ItemType != "TaskContribution" && row.ItemKey == task.S("key")));
        Assert.Equal(expected, off.I("totalCount")); Assert.Single(off["items"]!.AsArray()); Assert.NotEqual("TaskContribution", off["items"]![0]!.S("itemType"));
        await Get(Actors(id), expect: 404); await Flag(true);
        Assert.Equal(1, (await Get(History(id, $"?actorId={d.User(TestData.Jill)}"))).I("totalCount"));
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(f.Db(db => db.ActivityLog.Where(row => row.ItemType == "TaskContribution").Select(row => new { row.Id, row.Changes, row.RowHash, row.ItemKey }).ToArray())));
    }

    [Fact]
    public async Task Actor_choice_database_read_count_does_not_scale_with_history_actor_count()
    {
        await Flag(true); var project = await d.Project(); var task = await d.NewTask(project.Id); var id = task.G("id"); var capture = new Reads();
        using var app = f.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<HubDb>(options => options.AddInterceptors(capture))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Pm);
        await (await client.GetAsync(Actors(id))).Json(); capture.Sql.Clear(); await (await client.GetAsync(Actors(id))).Json(); var small = capture.Sql.Count; Assert.True(small > 0);
        await f.DbAsync(async db => { for (var index = 0; index < 201; index++) { var user = new AppUser { Email = $"synthetic-actor-scale-{index}@hub.test", DisplayName = $"Synthetic scale actor {index}" };
                db.Users.Add(user); db.ActivityLog.Add(Log(project.Id, ItemType.Task, id, task.S("key"), user.Id)); } return await db.SaveChangesAsync(); });
        capture.Sql.Clear(); var large = await (await client.GetAsync(Actors(id))).Json(); Assert.Equal(202, large.AsArray().Count); Assert.Equal(small, capture.Sql.Count);
    }

    sealed class Reads : DbCommandInterceptor
    {
        internal readonly List<string> Sql = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
