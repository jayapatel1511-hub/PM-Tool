using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkActivityCommentsTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkActivityCommentsTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Get(string path, string who = TestData.Alex) => await (await f.As(who).GetAsync(path)).Json();
    async Task Flags(bool small, bool work)
    {
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.small_ux", new { value = small })).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value = work })).EnsureSuccessStatusCode();
    }
    int Version(Guid id) => f.Db(db => db.Tasks.AsNoTracking().Single(t => t.Id == id).RowVersion);
    string Name(string who) => f.Db(db => db.Users.Single(u => u.Email == who).DisplayName);
    async Task Comment(Guid task, string body, string who = TestData.Alex) =>
        (await f.As(who).Post($"/api/v1/items/Task/{task}/comments", new { body })).EnsureSuccessStatusCode();
    async Task Edit(Guid task, string name, string who = TestData.Pm) =>
        (await f.As(who).Patch($"/api/v1/tasks/{task}", new { name }, Version(task))).EnsureSuccessStatusCode();
    static JsonNode Row(JsonNode page, Guid id) => page["items"]!.AsArray().Single(r => r!.G("id") == id)!;
    static void StripActor(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            obj.Remove("lastActivityActorName");
            foreach (var value in obj.ToList()) if (value.Value is { } child) StripActor(child);
        }
        else if (node is JsonArray array) foreach (var child in array) if (child is not null) StripActor(child);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Each_flag_independently_includes_existing_actor_projection_and_preserves_row_shape_and_comment_count(bool small, bool work)
    {
        var p = await data.Project(); var task = (await data.NewTask(p.Id, extra: new { name = "Activity and comments", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-18" })).G("id");
        for (var i = 0; i < 3; i++) await Comment(task, $"Normal comment {i + 1}");
        f.Clock.Now = f.Clock.Now.AddHours(2); await Edit(task, "Activity updated by the PM");
        var projectPath = $"/api/v1/projects/{p.Id}/tasks?sort=lastActivity:desc";
        var workspacePath = $"/api/v1/tasks?projects={p.Id}&sort=lastActivity:desc";
        await Flags(false, false);
        var offProject = await Get(projectPath); var offWorkspace = await Get(workspacePath); var offWork = await Get("/api/v1/me/work");
        await Flags(small, work);
        var project = await Get(projectPath); var workspace = await Get(workspacePath); var own = await Get("/api/v1/me/work");
        var ownRow = own["tasks"]!.AsArray().Single(r => r!.G("id") == task)!;
        foreach (var row in new[] { Row(project, task), Row(workspace, task) })
        {
            Assert.Equal(small || work, row.AsObject().ContainsKey("lastActivityActorName"));
            if (small || work) Assert.Equal(Name(TestData.Pm), row.S("lastActivityActorName"));
            Assert.Equal(3, row.I("commentCount"));
            Assert.Equal(f.Clock.Now, row["lastActivityAt"]!.GetValue<DateTimeOffset>());
        }
        Assert.Equal(work, ownRow.AsObject().ContainsKey("lastActivityActorName"));
        if (work) Assert.Equal(Name(TestData.Pm), ownRow.S("lastActivityActorName"));
        Assert.Equal(3, ownRow.I("commentCount")); Assert.Equal(f.Clock.Now, ownRow["lastActivityAt"]!.GetValue<DateTimeOffset>());
        StripActor(project); StripActor(workspace); StripActor(own);
        Assert.True(JsonNode.DeepEquals(offProject, project)); Assert.True(JsonNode.DeepEquals(offWorkspace, workspace)); Assert.True(JsonNode.DeepEquals(offWork, own));
    }

    [Fact]
    public async Task Assigned_collaborator_reviewer_and_completed_streams_share_the_actual_projection_without_read_writes()
    {
        var p = await data.Project(); var alex = data.User(TestData.Alex); var marc = data.User(TestData.Marc);
        var assigned = (await data.NewTask(p.Id, extra: new { name = "Assigned work", assigneeId = alex })).G("id");
        var shared = (await data.NewTask(p.Id, extra: new { name = "Collaborative work", assigneeId = marc })).G("id");
        (await f.As(TestData.Pm).Post($"/api/v1/tasks/{shared}/collaborators", new { userId = alex })).EnsureSuccessStatusCode();
        var review = await data.NewTask(p.Id, extra: new { name = "Review requested", assigneeId = marc, reviewerId = alex, requiresReview = true });
        await data.Move(TestData.Marc, review, TaskStatuses.InProgress); await data.Move(TestData.Marc, review, TaskStatuses.ReadyForReview);
        var done = await data.NewTask(p.Id, extra: new { name = "Completed work", assigneeId = alex, requiresReview = false });
        await data.Move(TestData.Alex, done, TaskStatuses.InProgress); await data.Move(TestData.Alex, done, TaskStatuses.Complete);
        foreach (var id in new[] { assigned, shared, review.G("id"), done.G("id") }) await Comment(id, "A normal discussion comment");
        await Flags(false, true);
        string Stored() => f.Db(db => JsonSerializer.Serialize(new {
            Tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => new { t.Id, t.Status, t.RowVersion, t.AssigneeId, t.ReviewerId }).ToList(),
            Preferences = db.UserSettings.AsNoTracking().OrderBy(x => x.UserId).ToList(),
            Activities = db.ActivityLog.Count(a => a.ProjectId == p.Id), Comments = db.Comments.Count(c => c.ProjectId == p.Id)
        }));
        var before = Stored(); var own = await Get("/api/v1/me/work");
        Assert.Equal(new[] { assigned, shared }.Order(), own["tasks"]!.AsArray().Select(r => r!.G("id")).Order());
        var reviewer = Assert.Single(own["reviews"]!["tasks"]!.AsArray())!; Assert.Equal(review.G("id"), reviewer.G("id"));
        var completed = Assert.Single(own["completed"]!.AsArray())!; Assert.Equal(done.G("id"), completed.G("id"));
        foreach (var row in own["tasks"]!.AsArray().Concat(own["reviews"]!["tasks"]!.AsArray()).Concat(own["completed"]!.AsArray()))
        {
            var id = row!.G("id");
            var actor = f.Db(db => db.ActivityLog.Where(a => a.ItemType == ItemType.Task && a.ItemId == id).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Select(a => a.ActorUserId).First());
            Assert.Equal(f.Db(db => db.Users.Where(u => u.Id == actor).Select(u => u.DisplayName).First()), row.S("lastActivityActorName"));
            Assert.Equal(1, row.I("commentCount"));
        }
        var fromTaskRoute = await Get($"/api/v1/tasks?projects={p.Id}&status=Complete&assigneeId={alex}&sort=completed:desc");
        Assert.Equal(completed.S("lastActivityActorName"), Row(fromTaskRoute, done.G("id")).S("lastActivityActorName"));
        var personView = await Get($"/api/v1/me/work?userId={alex}", TestData.Pm);
        Assert.True(personView["readOnly"]!.GetValue<bool>());
        Assert.Equal(before, Stored());
    }

    [Fact]
    public async Task Workspace_page_two_keeps_exact_full_count_scope_sort_and_activity_from_its_own_rows()
    {
        var p = await data.Project(); var ids = new List<Guid>();
        // Normal API creates deliberately cross the real workspace page100 boundary.
        for (var i = 0; i < 101; i++) ids.Add((await data.NewTask(p.Id, extra: new { name = $"Paged work {i + 1}", assigneeId = data.User(TestData.Alex) })).G("id"));
        var last = ids[^1]; await Comment(last, "First page-two discussion"); await Comment(last, "Second page-two discussion"); await Comment(last, "Third page-two discussion");
        f.Clock.Now = f.Clock.Now.AddHours(1); await Edit(last, "Page two actual latest actor"); await Flags(false, true);
        var first = await Get($"/api/v1/tasks?projects={p.Id}&sort=key:asc&page=1&pageSize=100");
        var second = await Get($"/api/v1/tasks?projects={p.Id}&sort=key:asc&page=2&pageSize=100");
        Assert.Equal(101, first.I("totalCount")); Assert.Equal(101, second.I("totalCount")); Assert.Equal(100, first["items"]!.AsArray().Count);
        var row = Assert.Single(second["items"]!.AsArray())!; Assert.Equal(last, row.G("id")); Assert.Equal(Name(TestData.Pm), row.S("lastActivityActorName")); Assert.Equal(3, row.I("commentCount"));
        Assert.DoesNotContain(first["items"]!.AsArray(), r => r!.G("id") == last);
        var activity = await Get($"/api/v1/tasks?projects={p.Id}&sort=lastActivity:desc&page=1&pageSize=1");
        Assert.Equal(last, Assert.Single(activity["items"]!.AsArray())!.G("id")); Assert.Equal(101, activity.I("totalCount"));
    }

    [Fact]
    public async Task Legacy_missing_and_system_activity_stay_distinct_with_deterministic_log_ties()
    {
        var p = await data.Project(); var missing = Guid.NewGuid();
        var system = (await data.NewTask(p.Id, extra: new { name = "System activity", assigneeId = data.User(TestData.Alex) })).G("id");
        var actor = data.User(TestData.Pm);
        // Explicit imported-record recovery fixture: a cloned historical task has no log provenance.
        // Never remove/disable the append-only log guard or erase a real command's evidence.
        await f.DbAsync(async db => {
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hub.task
                SELECT clone.* FROM hub.task t
                CROSS JOIN LATERAL jsonb_populate_record(NULL::hub.task, to_jsonb(t) || jsonb_build_object(
                    'id', {missing}, 'seq', t.seq + 1, 'name', 'Missing legacy log',
                    'key', {p.ProjectNumber} || '-T' || lpad((t.seq + 1)::text, 4, '0'))) AS clone
                WHERE t.id = {system}
                """);
            var later = f.Clock.Now.AddMinutes(1);
            db.ActivityLog.AddRange(new ActivityLog { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), ProjectId = p.Id, ItemType = ItemType.Task, ItemId = system, ActorType = "User", ActorUserId = actor, Action = "Edited", OccurredAt = later },
                new ActivityLog { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), ProjectId = p.Id, ItemType = ItemType.Task, ItemId = system, ActorType = "System", Action = "Edited", OccurredAt = later });
            return await db.SaveChangesAsync();
        });
        await Flags(false, true); var on = await Get($"/api/v1/tasks?projects={p.Id}&sort=key:asc");
        Assert.True(Row(on, missing).AsObject().ContainsKey("lastActivityActorName")); Assert.Null(Row(on, missing)["lastActivityActorName"]);
        Assert.Equal("System", Row(on, system).S("lastActivityActorName"));
        await Flags(false, false); var off = await Get($"/api/v1/tasks?projects={p.Id}&sort=key:asc");
        Assert.All(off["items"]!.AsArray(), r => Assert.False(r!.AsObject().ContainsKey("lastActivityActorName")));
        StripActor(on); Assert.True(JsonNode.DeepEquals(off, on));
    }

    [Fact]
    public async Task Restricted_projects_and_person_work_scope_still_hide_foreign_activity_and_comments()
    {
        var visible = await data.Project(); var other = await data.Project(pm: TestData.Marc); var alex = data.User(TestData.Alex);
        var a = (await data.NewTask(visible.Id, extra: new { name = "Managed visible discussion", assigneeId = alex })).G("id");
        var b = (await data.NewTask(other.Id, extra: new { name = "Other managed discussion", assigneeId = alex })).G("id");
        await Comment(a, "Managed comment"); await Comment(b, "Other project comment"); await Flags(false, true);
        var pmView = await Get($"/api/v1/me/work?userId={alex}", TestData.Pm);
        Assert.Contains(pmView["tasks"]!.AsArray(), r => r!.G("id") == a); Assert.DoesNotContain(pmView["tasks"]!.AsArray(), r => r!.G("id") == b);
        await (await f.As(TestData.Omar).GetAsync($"/api/v1/me/work?userId={alex}")).Json(403);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == other.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == other.Id && m.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var own = await Get("/api/v1/me/work"); Assert.DoesNotContain(own["tasks"]!.AsArray(), r => r!.G("id") == b);
        var tasks = await Get($"/api/v1/tasks?projects={visible.Id},{other.Id}"); Assert.Equal(a, Assert.Single(tasks["items"]!.AsArray())!.G("id"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{other.Id}/tasks")).Json(404);
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/items/Task/{b}/comments")).Json(404);
        var readOnly = await Get($"/api/v1/tasks?projects={visible.Id}", TestData.Rita); Assert.Equal(1, Row(readOnly, a).I("commentCount"));
        Assert.False(Row(readOnly, a)["can"]!["edit"]!.GetValue<bool>());
    }
}
