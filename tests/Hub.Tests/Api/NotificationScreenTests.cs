using System.Net;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// S45-A only: canonical action/follow reads; transport evidence and S47 remain separate.
public sealed class NotificationScreenTests
{
    const string Event = "SyntheticScreenProjection";
    static async Task Flag(HubFactory f, bool on) =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = on })).EnsureSuccessStatusCode();
    static async Task<JsonNode> Read(HubFactory f, string query = "") =>
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/me/notifications/screen?type={Event}{query}")).Json();
    static Notification Notice(HubFactory f, Guid user, Guid? project, string? kind, Guid? item, int age = 0) => new()
    {
        UserId = user, EventType = Event, ProjectId = project, ItemType = kind, ItemId = item, ItemKey = "SYN-NOTICE",
        Title = "Synthetic notice " + age, LinkPath = "/time", Count = 1,
        CreatedAt = f.Clock.GetUtcNow().AddMinutes(-age), UpdatedAt = f.Clock.GetUtcNow().AddMinutes(-age),
    };
    static async Task Store(HubFactory f, Notification[] rows) =>
        await f.DbAsync(async db => { db.Notifications.AddRange(rows); return await db.SaveChangesAsync(); });
    static string State(HubFactory f) => f.Db(db => JsonSerializer.Serialize(new
    {
        notices = db.Notifications.AsNoTracking().Where(row => row.EventType == Event).OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Count, row.ReadAt, row.EmailedAt, row.DigestIncludedAt }).ToArray(),
        follows = db.Follows.AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.Level, row.Source, row.LastSeenAt }).ToArray(),
        visits = db.UserSettings.AsNoTracking().OrderBy(row => row.UserId).Select(row => new { row.UserId, row.MyWorkSeenRevision, row.MyWorkLastVisitAt, row.MyWorkLastMarkAllAt }).ToArray(),
        logs = db.ActivityLog.Count(), receipts = db.Idempotency.Count(), emails = db.Emails.Count(),
    }));

    [Fact]
    public async Task Full_cohort_filter_counts_two_current_actors_beyond_the_first_fifty_with_patterns_off_and_rechecks_changed_actors()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = false })).EnsureSuccessStatusCode();
        var p = await d.Project(); var alex = d.User(TestData.Alex);
        var own = await d.NewTask(p.Id, extra: new { assigneeId = alex });
        var other = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Marc) });
        var reviewing = await d.NewTask(p.Id, extra: new { assigneeId = alex, reviewerId = d.User(TestData.Marc) });
        await f.DbAsync(async db => { var task = await db.Tasks.SingleAsync(row => row.Id == reviewing.G("id")); task.Status = TaskStatuses.InReview; return await db.SaveChangesAsync(); });
        var rows = new[] { Notice(f, alex, p.Id, "Task", own.G("id"), 100), Notice(f, alex, p.Id, "Task", own.G("id"), 101),
            Notice(f, alex, p.Id, "Task", other.G("id"), 1), Notice(f, alex, p.Id, "Task", reviewing.G("id"), 2), Notice(f, alex, p.Id, "Unsupported", Guid.NewGuid(), 3) }
            .Concat(Enumerable.Range(4, 52).Select(age => Notice(f, alex, p.Id, "Task", other.G("id"), age))).ToArray();
        rows[0].EmailedAt = f.Clock.GetUtcNow(); rows[0].DigestIncludedAt = f.Clock.GetUtcNow();
        await Store(f, rows); (await f.As(TestData.Alex).GetAsync("/api/v1/me")).EnsureSuccessStatusCode(); var before = State(f);
        var all = await Read(f, "&pageSize=1"); Assert.Equal(57, all.I("totalCount"));
        Assert.False(all["items"]![0]!["screen"]!["needsYourAction"]!.GetValue<bool>());
        var first = await Read(f, "&needsAction=true&pageSize=1"); var second = await Read(f, "&needsAction=true&pageSize=1&page=2");
        Assert.Equal(2, first.I("totalCount")); Assert.Equal(2, second.I("totalCount"));
        Assert.Equal(56, first["contextCoverage"]!.I("known")); Assert.Equal(1, first["contextCoverage"]!.I("unknown"));
        var selected = first["items"]!.AsArray().Concat(second["items"]!.AsArray()).ToArray();
        Assert.Equal(rows.Take(2).Select(row => row.Id).Order(), selected.Select(row => row!["notification"]!.G("id")).Order());
        Assert.All(selected, row =>
        {
            Assert.True(row!["screen"]!["needsYourAction"]!.GetValue<bool>()); Assert.Equal("Known", row["screen"]!.S("contextState"));
            Assert.Equal("Unknown", row["screen"]!["delivery"]!.S("state")); Assert.Equal("Unavailable", row["screen"]!["delivery"]!.S("historyCoverage"));
            Assert.Equal(p.Id, row["screen"]!["follow"]!.G("projectId"));
        });
        Assert.Empty((await Read(f, "&needsAction=true&pageSize=1&page=3"))["items"]!.AsArray());
        Assert.Equal(before, State(f));
        await f.DbAsync(async db => { var task = await db.Tasks.SingleAsync(row => row.Id == own.G("id")); task.Status = TaskStatuses.InReview; task.ReviewerId = d.User(TestData.Marc); return await db.SaveChangesAsync(); });
        var current = await Read(f, "&needsAction=true"); Assert.Equal(0, current.I("totalCount")); Assert.Equal(1, current["contextCoverage"]!.I("unknown"));
    }

    [Fact]
    public async Task Unknown_mismatched_deleted_targets_stay_unknown_and_hidden_recipient_and_compound_scope_rows_are_excluded_before_counts()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        var visible = await d.Project(); var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
        // Visibility is an existing PATCH field, not part of the create contract; prove the source is actually hidden.
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
        Assert.Equal(Visibility.Restricted, f.Db(db => db.Projects.Where(row => row.Id == hidden.Id).Select(row => row.Visibility).Single()));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{hidden.Id}")).StatusCode);
        var hiddenTask = await d.NewTask(hidden.Id, TestData.Pm, new { name = "SYNTHETIC HIDDEN TARGET", assigneeId = d.User(TestData.Pm) });
        var deleted = await d.NewTask(visible.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(row => row.Id == deleted.G("id"))).DeletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        var alex = d.User(TestData.Alex);
        var unknown = new[] { Notice(f, alex, visible.Id, "Task", hiddenTask.G("id")), Notice(f, alex, visible.Id, "Task", deleted.G("id")), Notice(f, alex, visible.Id, "Unsupported", Guid.NewGuid()) };
        var hiddenRow = Notice(f, alex, hidden.Id, "Task", hiddenTask.G("id")); hiddenRow.Title = "SYNTHETIC HIDDEN DIRECT";
        var compound = Notice(f, alex, visible.Id, "Task", deleted.G("id")); compound.RequiredProjectIds = [visible.Id, hidden.Id]; compound.Title = "SYNTHETIC HIDDEN COMPOUND";
        var other = Notice(f, d.User(TestData.Jill), visible.Id, "Task", deleted.G("id")); other.Title = "SYNTHETIC OTHER RECIPIENT";
        await Store(f, [.. unknown, hiddenRow, compound, other]);
        var result = await Read(f, "&pageSize=1"); Assert.Equal(3, result.I("totalCount"));
        Assert.Equal(0, result["contextCoverage"]!.I("known")); Assert.Equal(3, result["contextCoverage"]!.I("unknown"));
        var complete = await Read(f, "&pageSize=100");
        Assert.Equal(unknown.Select(row => row.Id).Order(), complete["items"]!.AsArray().Select(row => row!["notification"]!.G("id")).Order());
        Assert.All(complete["items"]!.AsArray(), row =>
        {
            Assert.Null(row!["screen"]!["needsYourAction"]); Assert.Equal("Unknown", row["screen"]!.S("contextState"));
        });
        foreach (var secret in new[] { hidden.Name, "SYNTHETIC HIDDEN TARGET", hiddenRow.Title, compound.Title, other.Title }) Assert.DoesNotContain(secret, complete.ToJsonString());
        var action = await Read(f, "&needsAction=true"); Assert.Equal(0, action.I("totalCount")); Assert.Equal(3, action["contextCoverage"]!.I("unknown"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).Put($"/api/v1/projects/{hidden.Id}/follow", new { level = "Muted" })).StatusCode);
    }

    [Fact]
    public async Task Follow_uses_existing_own_command_without_read_side_effects_and_dark_route_leaves_the_original_wire_intact()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true); var p = await d.Project();
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var row = Notice(f, d.User(TestData.Alex), p.Id, "Task", task.G("id")); await Store(f, [row]);
        var unread = f.Db(db => db.Notifications.Single(item => item.Id == row.Id).ReadAt);
        var reply = await (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/follow", new { level = "Muted", userId = d.User(TestData.Jill) })).Json();
        Assert.Equal("Muted", reply.S("level")); Assert.Equal("Manual", reply.S("source"));
        var result = await Read(f); var follow = result["items"]![0]!["screen"]!["follow"]!;
        Assert.Equal("Muted", follow.S("level")); Assert.Equal("Manual", follow.S("source"));
        Assert.Equal(unread, f.Db(db => db.Notifications.Single(item => item.Id == row.Id).ReadAt));
        Assert.False(f.Db(db => db.Follows.Any(item => item.ProjectId == p.Id && item.UserId == d.User(TestData.Jill) && item.Source == "Manual")));
        await Flag(f, false); var before = State(f);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync("/api/v1/me/notifications/screen?needsAction=true")).StatusCode);
        var legacy = await (await f.As(TestData.Alex).GetAsync($"/api/v1/me/notifications?type={Event}&needsAction=true")).Json();
        Assert.False(legacy.AsObject().ContainsKey("contextCoverage")); Assert.Equal(1, legacy.I("totalCount"));
        var item = legacy["items"]![0]!; Assert.Equal(row.Id, item.G("id")); Assert.False(item.AsObject().ContainsKey("screen")); Assert.False(item.AsObject().ContainsKey("notification"));
        Assert.Equal(before, State(f));
    }

    [Fact]
    public async Task Many_distinct_task_primaries_and_present_or_missing_states_are_batched_without_changing_current_action_counts()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true); var p = await d.Project();
        var alex = d.User(TestData.Alex); var marc = d.User(TestData.Marc); var discipline = d.ProjectDiscipline(p.Id, "Civil");
        var tasks = Enumerable.Range(0, 64).Select(index => new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline,
            Seq = 1000 + index, Key = $"{p.ProjectNumber}-T{1000 + index}", Name = "Synthetic distinct notification task " + index,
            Status = TaskStatuses.NotStarted, AssigneeId = index < 2 ? alex : marc }).ToArray();
        await f.DbAsync(async db =>
        {
            db.Tasks.AddRange(tasks);
            // Negative state-cache entries matter: half the tasks deliberately have no materialized TaskState.
            db.TaskStates.AddRange(tasks.Where((_, index) => index % 2 == 0).Select(task => new TaskState { TaskId = task.Id, ProjectId = p.Id }));
            db.Notifications.AddRange(tasks.Select((task, index) => Notice(f, alex, p.Id, "Task", task.Id, index < 2 ? 100 + index : index)));
            var small = Notice(f, alex, p.Id, "Task", tasks[0].Id); small.EventType = Event + "Small"; db.Notifications.Add(small);
            return await db.SaveChangesAsync();
        });
        var capture = new PrimaryReads();
        using var app = f.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<HubDb>(options => options.AddInterceptors(capture))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Alex);
        var path = $"/api/v1/me/notifications/screen?type={Event}&needsAction=true&pageSize=1";
        await (await client.GetAsync(path + "&page=1")).Json(); // Warm the settings/auth host only; the next requests still load current facts.
        capture.Sql.Clear(); var smallRead = await (await client.GetAsync($"/api/v1/me/notifications/screen?type={Event}Small&pageSize=1")).Json();
        var smallTasks = capture.Count("task"); var smallStates = capture.Count("task_state");
        Assert.Equal(1, smallRead.I("totalCount")); Assert.True(smallTasks > 0); Assert.True(smallStates > 0);
        capture.Sql.Clear(); var large = await (await client.GetAsync(path)).Json();
        var largeTasks = capture.Count("task"); var largeStates = capture.Count("task_state");
        Assert.Equal(2, large.I("totalCount")); Assert.Single(large["items"]!.AsArray());
        Assert.Equal(64, large["contextCoverage"]!.I("known")); Assert.Equal(0, large["contextCoverage"]!.I("unknown"));
        Assert.Equal(smallTasks, largeTasks); Assert.Equal(smallStates, largeStates);
        Assert.InRange(largeTasks, 1, 3); Assert.InRange(largeStates, 1, 2);
        Assert.True(large["items"]![0]!["screen"]!["needsYourAction"]!.GetValue<bool>());
        var page2 = await (await client.GetAsync(path + "&page=2")).Json(); Assert.Equal(2, page2.I("totalCount"));
        Assert.NotEqual(large["items"]![0]!["notification"]!.G("id"), page2["items"]![0]!["notification"]!.G("id"));
        Assert.Equal(64, tasks.Select(task => task.Id).Distinct().Count());
    }

    [Fact]
    public async Task Terminal_task_with_stale_missing_blocker_is_known_not_actionable_rather_than_unknown()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true); var p = await d.Project();
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        await f.DbAsync(async db =>
        {
            var row = await db.Tasks.SingleAsync(row => row.Id == task.G("id")); row.Status = TaskStatuses.Complete;
            var state = await db.TaskStates.SingleOrDefaultAsync(item => item.TaskId == row.Id);
            if (state is null) db.TaskStates.Add(state = new TaskState { TaskId = row.Id, ProjectId = p.Id });
            state.IsBlocked = true; state.BlockedBy = JsonSerializer.Serialize(new[] { new { type = "Task", id = Guid.NewGuid() } });
            return await db.SaveChangesAsync();
        });
        var notice = Notice(f, d.User(TestData.Alex), p.Id, "Task", task.G("id")); await Store(f, [notice]);
        var page = await Read(f); var screen = page["items"]![0]!["screen"]!;
        Assert.False(screen["needsYourAction"]!.GetValue<bool>()); Assert.Equal("Known", screen.S("contextState"));
        Assert.Equal(1, page["contextCoverage"]!.I("known")); Assert.Equal(0, page["contextCoverage"]!.I("unknown"));
        Assert.Equal(0, (await Read(f, "&needsAction=true")).I("totalCount"));
    }

    [Fact]
    public async Task Typed_finding_uses_current_child_actor_instead_of_package_owner_or_parent_id_with_patterns_off()
    {
        using var f = new HubFactory(); var d = new TestData(f); await Flag(f, true);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = false })).EnsureSuccessStatusCode();
        var p = await d.Project(); var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
        async Task<JsonNode> Post(string who, string url, object body, int status = 200) => await (await f.As(who).Post(url, body)).Json(status);
        var deliverable = (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic finding source", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        var revision = (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "finding", "Synthetic finding source", "A", "https://example.invalid/finding.pdf", "Survey", "Scope", null, null, null, null, null, null))).G("id");
        var package = (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Synthetic typed finding review",
            "Verify finding", civil, d.User(TestData.Marc), [revision], [new ReviewEndpoints.Assignment(electrical, d.User(TestData.Omar), new DateOnly(2026, 9, 24))], true, null))).G("id");
        await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/reviews/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start review"));
        var finding = (await Post(TestData.Omar, $"/api/v1/projects/{p.Id}/reviews/{package}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(),
            Version<ReviewPackage>(package), revision, electrical, d.User(TestData.Alex), "Synthetic current child action", "Blocking"))).G("id");
        var child = Notice(f, d.User(TestData.Alex), p.Id, "ReviewFinding", finding);
        var parent = Notice(f, d.User(TestData.Alex), p.Id, "ReviewPackage", package);
        var substituted = Notice(f, d.User(TestData.Alex), p.Id, "ReviewFinding", package);
        await Store(f, [child, parent, substituted]);
        var page = await Read(f, "&needsAction=true"); var item = Assert.Single(page["items"]!.AsArray())!;
        Assert.Equal(child.Id, item["notification"]!.G("id")); Assert.Equal(finding, item["notification"]!.G("itemId")); Assert.Equal("ReviewFinding", item["notification"]!.S("itemType"));
        Assert.NotEqual(d.User(TestData.Alex), f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CoordinatorId));
        var all = await Read(f); var wrong = all["items"]!.AsArray().Single(row => row!["notification"]!.G("id") == substituted.Id)!;
        Assert.Null(wrong["screen"]!["needsYourAction"]); Assert.Equal("Unknown", wrong["screen"]!.S("contextState"));
        await f.DbAsync(async db => { var row = await db.ReviewFindings.SingleAsync(row => row.Id == finding); row.Status = FindingStatus.Responded;
            row.Response = "Synthetic recorded response"; row.EvidenceUrl = "https://example.invalid/evidence"; row.VerifierId = d.User(TestData.Marc); return await db.SaveChangesAsync(); });
        Assert.DoesNotContain((await Read(f, "&needsAction=true"))["items"]!.AsArray(), row => row!["notification"]!.G("id") == child.Id);
    }

    sealed class PrimaryReads : DbCommandInterceptor
    {
        internal readonly List<string> Sql = [];
        internal int Count(string table) => Sql.Count(sql => sql.Replace("\"", "").Contains($"FROM hub.{table} ", StringComparison.OrdinalIgnoreCase));
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
