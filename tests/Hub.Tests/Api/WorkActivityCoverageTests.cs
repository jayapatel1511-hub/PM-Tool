using Hub.Api;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

namespace Hub.Tests.Api;

public sealed class WorkActivityCoverageTests
{
    [Theory]
    [InlineData("Task")] [InlineData("Deliverable")] [InlineData("Milestone")] [InlineData("Decision")]
    [InlineData("Risk")] [InlineData("Issue")] [InlineData("Action")]
    public async Task Every_commentable_Watching_kind_uses_normal_create_comments_delete_and_current_parent_event(string kind)
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); await s.Flag("my_work_follow_up", true);
        var now = s.Factory.Clock.Now; s.Factory.Clock.Now = now.AddHours(-2);
        var p = await s.Data.Project(); var civil = s.Data.ProjectDiscipline(p.Id, "Civil"); var alex = s.Data.User(TestData.Alex);
        async Task<JsonNode> Post(string path, object body, int status = 201) => await (await s.Factory.As(TestData.Pm).Post(path, body)).Json(status);
        async Task<JsonNode> Action()
        {
            var meeting = await Post($"/api/v1/projects/{p.Id}/meetings/current", new { }, 200);
            return await Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Synthetic watched Action", ownerType = "User", ownerUserId = alex });
        }
        var item = kind switch {
            "Task" => await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = alex }),
            "Deliverable" => await Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic watched Deliverable",
                projectDisciplineId = civil, deliverableTypeId = await s.Data.DeliverableType(), ownerId = alex }),
            "Milestone" => await Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic watched Milestone",
                milestoneType = MilestoneType.DesignSubmission, date = "2026-10-01", projectDisciplineId = civil, isClientFacing = true }),
            "Decision" => await Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Synthetic watched Decision", description = "Synthetic",
                ownerUserId = alex, requiredByDate = "2026-10-01", impactLevel = Impact.Low, impactDescription = "Synthetic watched Decision impact." }),
            "Risk" => await Post($"/api/v1/projects/{p.Id}/risks", new { title = "Synthetic watched Risk", description = "Synthetic",
                probability = 2, impact = 2, ownerId = alex }),
            "Issue" => await Post($"/api/v1/projects/{p.Id}/issues", new { title = "Synthetic watched Issue", description = "Synthetic",
                severity = Impact.Low, ownerId = alex }),
            "Action" => await Action(),
            _ => throw new ArgumentException("Unsupported commentable test kind")
        };
        var key = new WorkSeenKey(kind, item.G("id")); s.Factory.Clock.Now = now;
        await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/{kind}/{key.ItemId}/watchers", new { userId = alex })).Json(204);
        var watching = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/me/work/watching")).Json();
        Assert.Contains(watching["items"]!.AsArray(), row => row!.S("itemType") == kind && row.G("id") == key.ItemId);
        var comments = new List<Guid>();
        for (var index = 0; index < 3; index++)
            comments.Add((await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/{kind}/{key.ItemId}/comments",
                new { body = "Synthetic watched discussion " + index })).Json(201)).G("id"));
        var fact = (await Read(s, [key]))[key];
        Assert.True(fact.Commentable); Assert.Equal(3, fact.CommentCount);
        Assert.Equal(now.AddHours(-2), fact.OccurredAt); Assert.Equal(s.Data.User(TestData.Pm), fact.ActorId);
        Assert.Equal("User", fact.ActorType);
        Assert.Equal(s.Factory.Db(db => db.Users.Single(user => user.Id == s.Data.User(TestData.Pm)).DisplayName), fact.ActorName);
        Assert.Equal(key.ItemId, s.Factory.Db(db => db.ActivityLog.Single(log => log.Id == fact.EventId).ItemId));
        await (await s.Factory.As(TestData.Alex).DeleteAsync("/api/v1/comments/" + comments[1])).Json(204);
        Assert.Equal(2, (await Read(s, [key]))[key].CommentCount);
        var discussion = await (await s.Factory.As(TestData.Alex).GetAsync($"/api/v1/items/{kind}/{key.ItemId}/comments")).Json();
        Assert.Equal(2, discussion["items"]!.AsArray().Count(row => !row!["deleted"]!.GetValue<bool>()));
        int Version() => s.Factory.Db(db => kind switch {
            "Task" => db.Tasks.Single(row => row.Id == key.ItemId).RowVersion,
            "Deliverable" => db.Deliverables.Single(row => row.Id == key.ItemId).RowVersion,
            "Milestone" => db.Milestones.Single(row => row.Id == key.ItemId).RowVersion,
            "Decision" => db.Decisions.Single(row => row.Id == key.ItemId).RowVersion,
            "Risk" => db.Risks.Single(row => row.Id == key.ItemId).RowVersion,
            "Issue" => db.Issues.Single(row => row.Id == key.ItemId).RowVersion,
            "Action" => db.Actions.Single(row => row.Id == key.ItemId).RowVersion,
            _ => throw new ArgumentException()
        });
        var field = kind switch { "Task" or "Deliverable" or "Milestone" => "name", "Decision" => "subject", "Action" => "text", _ => "title" };
        var route = kind switch { "Task" => "tasks", "Deliverable" => "deliverables", "Milestone" => "milestones",
            "Decision" => "decisions", "Risk" => "risks", "Issue" => "issues", "Action" => "actions", _ => throw new ArgumentException() };
        var actor = kind == "Milestone" ? TestData.Pm : TestData.Alex;
        s.Factory.Clock.Now = now.AddMinutes(5);
        await (await s.Factory.As(actor).Patch($"/api/v1/{route}/{key.ItemId}",
            new Dictionary<string, object?> { [field] = "Updated synthetic watched " + kind }, Version())).Json();
        var updated = (await Read(s, [key]))[key];
        Assert.NotEqual(fact.EventId, updated.EventId); Assert.Equal(s.Factory.Clock.Now, updated.OccurredAt);
        Assert.Equal(s.Data.User(actor), updated.ActorId); Assert.Equal("User", updated.ActorType);
        Assert.Equal(2, updated.CommentCount);
    }
    static Task<IReadOnlyDictionary<WorkSeenKey, WorkActivityFact>> Read(WorkSeenTestContext s, WorkSeenKey[] keys, string who = TestData.Alex) =>
        s.Factory.DbAsync(async db => {
            var me = new CurrentUser { Id = s.Data.User(who), IsActive = true, Resolved = true };
            return await WorkActivityFacts.ReadAsync(db, new(db, me), await db.CurrentSettings(), keys);
        });

    [Fact]
    public async Task Exact_event_time_and_actor_stay_paired_when_three_comments_advance_cached_activity_time()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        var now = s.Factory.Clock.Now; s.Factory.Clock.Now = now.AddHours(-2);
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        s.Factory.Clock.Now = now; var key = new WorkSeenKey("Task", task.G("id"));
        var comments = new List<Guid>();
        for (var index = 0; index < 3; index++)
            comments.Add((await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/Task/{key.ItemId}/comments", new { body = "Synthetic comment " + index })).Json(201)).G("id"));
        var fact = (await Read(s, [key]))[key];
        Assert.Equal(now.AddHours(-2), fact.OccurredAt); Assert.Equal(s.Data.User(TestData.Marc), fact.ActorId);
        Assert.Equal("User", fact.ActorType); Assert.Equal(3, fact.CommentCount); Assert.True(fact.Commentable);
        Assert.True(s.Factory.Db(db => db.Tasks.Single(row => row.Id == key.ItemId).LastActivityAt) > fact.OccurredAt);
        var api = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/tasks?mine=true")).Json();
        var renderedSource = Assert.Single(api["items"]!.AsArray(), row => row!.G("id") == key.ItemId)!;
        Assert.Equal(fact.ActorId, renderedSource.G("lastActivityActorId"));
        Assert.Equal(fact.OccurredAt, DateTimeOffset.Parse(renderedSource.S("lastActivityEventAt")));
        await (await s.Factory.As(TestData.Alex).DeleteAsync("/api/v1/comments/" + comments[0])).Json(204);
        Assert.Equal(2, (await Read(s, [key]))[key].CommentCount);
    }

    [Fact]
    public async Task Typed_colliding_ids_and_real_System_or_unknown_actors_do_not_borrow_facts()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        // Deliberate collision fixture: different canonical tables may hold the same GUID.
        await s.Factory.DbAsync(async db => {
            db.Risks.Add(new Risk { Id = id, ProjectId = p.Id, Key = "R-COLLISION", Seq = 999, Title = "Synthetic typed collision",
                Probability = 1, Impact = 1, OwnerId = s.Data.User(TestData.Alex) });
            return await db.SaveChangesAsync();
        });
        var taskKey = new WorkSeenKey("Task", id); var riskKey = new WorkSeenKey("Risk", id);
        var both = await Read(s, [taskKey, riskKey]);
        Assert.NotEqual(both[taskKey].EventId, both[riskKey].EventId);
        Assert.Equal("System", both[riskKey].ActorType);
        Assert.Equal(Text.Get("common.system"), both[riskKey].ActorName);
        await s.Factory.DbAsync(async db => {
            db.ActivityLog.Add(new ActivityLog { ItemType = "Task", ItemId = id, ProjectId = p.Id, ActorType = "User",
                ActorUserId = null, OccurredAt = s.Factory.Clock.Now.AddHours(1), Action = "Synthetic unknown actor" });
            return await db.SaveChangesAsync();
        });
        var unknown = (await Read(s, [taskKey]))[taskKey];
        Assert.Equal("User", unknown.ActorType); Assert.Null(unknown.ActorId); Assert.Null(unknown.ActorName);
        var project = (await Read(s, [new("Project", p.Id)]))[new("Project", p.Id)];
        Assert.False(project.Commentable); Assert.Null(project.CommentCount);
    }

    [Fact]
    public async Task Unauthorized_typed_keys_and_flagoff_return_no_facts()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        // Privacy boundary fixture, not a new assignment producer.
        await s.Factory.DbAsync(async db => {
            (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        Assert.Empty(await Read(s, [new("Task", task.G("id"))], TestData.Rita));
        await s.Flag("work_screens", false);
        Assert.Empty(await Read(s, [new("Task", task.G("id"))]));
    }
}
