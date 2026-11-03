using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-SCR01 task detail context: read-only, explicitly assessed ownership and blocking inputs.
public sealed class TaskContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public TaskContextTests() => data = new(f);
    public void Dispose() => f.Dispose();

    async Task EnableContext()
    {
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
    }

    async Task<JsonNode> Detail(Guid id, string user = TestData.Pm) =>
        await (await f.As(user).GetAsync($"/api/v1/tasks/{id}")).Json();

    async Task<Guid> NewTask(Guid projectId, object? extra = null) =>
        (await data.NewTask(projectId, TestData.Marc, extra)).G("id");

    async Task SetState(Guid id, string status, Guid? reviewer = null, DateOnly? due = null)
    {
        await f.DbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(x => x.Id == id);
            task.Status = status;
            task.ReviewerId = reviewer;
            task.DueDate = due;
            await db.SaveChangesAsync();
            return 0;
        });
    }

    async Task Block(Guid id, params object[] blockers)
    {
        await f.DbAsync(async db =>
        {
            var task = await db.Tasks.AsNoTracking().SingleAsync(x => x.Id == id);
            db.TaskStates.Add(new TaskState
            {
                TaskId = id, ProjectId = task.ProjectId, IsBlocked = true, BlockedSince = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime),
                BlockedBy = JsonSerializer.Serialize(blockers, Web)
            });
            await db.SaveChangesAsync();
            return 0;
        });
    }

    [Fact]
    public async Task Context_is_omitted_when_screen_patterns_is_off_and_present_when_enabled()
    {
        var project = await data.Project();
        var task = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });

        var off = await Detail(task);
        Assert.Null(off["context"]);

        await EnableContext();
        var on = await Detail(task);
        Assert.NotNull(on["context"]);
        Assert.Equal(RecordContextAction.Start, on["context"]!.S("nextAction"));
    }

    [Fact]
    public async Task In_review_keeps_assignee_as_owner_and_uses_reviewer_as_next_actor()
    {
        await EnableContext();
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var diane = data.User(TestData.Diane);
        var task = await NewTask(project.Id, new { assigneeId = alex, reviewerId = diane });
        await SetState(task, TaskStatuses.InReview, diane, DateOnly.FromDateTime(f.Clock.Now.UtcDateTime.AddDays(4)));

        var context = (await Detail(task))["context"]!;
        Assert.Equal(alex, context["owner"]!.G("id"));
        Assert.Equal(diane, context["nextActors"]![0]!.G("id"));
        Assert.Contains("Diane", context["nextActors"]![0]!["name"]!.GetValue<string>());
        Assert.Equal(RecordContextAction.Review, context.S("nextAction"));
    }

    [Fact]
    public async Task Missing_reviewer_is_not_replaced_by_assignee_and_inactive_reviewer_is_labelled()
    {
        await EnableContext();
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var task = await NewTask(project.Id, new { assigneeId = alex });
        await SetState(task, TaskStatuses.InReview, null);
        var missing = (await Detail(task))["context"]!;
        Assert.Equal(alex, missing["owner"]!.G("id"));
        Assert.Empty(missing["nextActors"]!.AsArray());
        Assert.Equal(RecordContextActorState.Unassigned.ToString(), missing.S("actorState"));

        var diane = data.User(TestData.Diane);
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == diane)).IsActive = false;
            await db.SaveChangesAsync();
            return 0;
        });
        await SetState(task, TaskStatuses.InReview, diane);
        var inactive = (await Detail(task))["context"]!;
        Assert.Equal(diane, inactive["nextActors"]![0]!.G("id"));
        Assert.Equal(RecordContextActorState.Inactive.ToString(), inactive["actorState"]!.GetValue<string>());
        Assert.Equal("Inactive", inactive["nextActors"]![0]!["state"]!.GetValue<string>());
    }

    [Fact]
    public async Task Terminal_task_has_no_next_actor_or_action_and_retains_owner_and_date()
    {
        await EnableContext();
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var due = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime.AddDays(2));
        var task = await NewTask(project.Id, new { assigneeId = alex });
        await SetState(task, TaskStatuses.Complete, null, due);
        var context = (await Detail(task))["context"]!;
        Assert.Equal(alex, context["owner"]!.G("id"));
        Assert.Equal(due.ToString("yyyy-MM-dd"), context["neededBy"]!.GetValue<string>());
        Assert.True(context["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, context.S("nextAction"));
        Assert.Empty(context["nextActors"]!.AsArray());
    }

    [Fact]
    public async Task Blocked_task_resolves_primary_predecessor_reviewer_not_assignee()
    {
        await EnableContext();
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var diane = data.User(TestData.Diane);
        var predecessor = await NewTask(project.Id, new { assigneeId = alex, reviewerId = diane });
        await SetState(predecessor, TaskStatuses.InReview, diane);
        var successor = await NewTask(project.Id, new { assigneeId = alex });
        await Block(successor, new { type = "Task", id = predecessor });

        var context = (await Detail(successor))["context"]!;
        Assert.Equal(RecordContextAction.ResolveInput, context.S("nextAction"));
        Assert.Equal(diane, context["nextActors"]![0]!.G("id"));
        Assert.Equal(predecessor, context["inputs"]![0]!.G("id"));
        Assert.True(context["inputs"]![0]!["primary"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Manual_missing_cross_project_and_deleted_blockers_are_not_assessed_or_leaked()
    {
        await EnableContext();
        var project = await data.Project();
        var other = await data.Project();
        var crossProject = await NewTask(other.Id, new { assigneeId = data.User(TestData.Alex) });
        var deleted = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await f.DbAsync(async db =>
        {
            (await db.Tasks.SingleAsync(x => x.Id == deleted)).DeletedAt = f.Clock.Now;
            await db.SaveChangesAsync();
            return 0;
        });
        var missing = Guid.NewGuid();
        var successor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await Block(successor,
            new { type = "Manual", id = (Guid?)null, name = "Secret input" },
            new { type = "Task", id = missing, name = "Missing input" },
            new { type = "Task", id = crossProject, name = "Cross project input" },
            new { type = "Task", id = deleted, name = "Deleted input" });

        var context = (await Detail(successor))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction"));
        Assert.Empty(context["nextActors"]!.AsArray());
        Assert.Empty(context["inputs"]!.AsArray());
        Assert.DoesNotContain("Secret input", context.ToJsonString());
        Assert.DoesNotContain(missing.ToString(), context.ToJsonString());
        Assert.DoesNotContain(crossProject.ToString(), context.ToJsonString());
        Assert.DoesNotContain(deleted.ToString(), context.ToJsonString());
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[{}]")]
    [InlineData("[{\"type\":null}]")]
    [InlineData("[{\"type\":\"Task\",\"id\":\"not-a-guid\"}]")]
    [InlineData("{}") ]
    public async Task Malformed_blocker_rows_are_not_a_task_detail_server_error(string raw)
    {
        await EnableContext();
        var project = await data.Project();
        var successor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await Block(successor, new { type = "Manual" });
        await f.DbAsync(async db =>
        {
            (await db.TaskStates.SingleAsync(x => x.TaskId == successor)).BlockedBy = raw;
            return await db.SaveChangesAsync();
        });

        var context = (await Detail(successor))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction"));
        Assert.Empty(context["nextActors"]!.AsArray());
        Assert.Empty(context["inputs"]!.AsArray());
    }

    [Fact]
    public async Task Repeated_canonical_blockers_are_one_input()
    {
        await EnableContext();
        var project = await data.Project();
        var predecessor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        var successor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await Block(successor, new { type = "task", id = predecessor }, new { type = "Task", id = predecessor });
        var context = (await Detail(successor))["context"]!;
        Assert.Equal(predecessor, Assert.Single(context["inputs"]!.AsArray())!.G("id"));
    }

    [Fact]
    public async Task Terminal_predecessor_is_retained_as_an_unassessed_blocker()
    {
        await EnableContext();
        var project = await data.Project();
        var predecessor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await SetState(predecessor, TaskStatuses.Complete);
        var successor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await Block(successor, new { type = "Task", id = predecessor });

        var context = (await Detail(successor))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction"));
        Assert.Equal("Unknown", context.S("actorState"));
        Assert.Empty(context["nextActors"]!.AsArray());
        Assert.Single(context["inputs"]!.AsArray());
    }

    [Fact]
    public async Task External_decision_owner_keeps_external_and_inactive_state()
    {
        await EnableContext();
        var project = await data.Project();
        var party = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/external-parties", new { name = "Inactive vendor" })).Json(201);
        var decision = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "Vendor decision", description = "Context test", ownerExternalPartyId = party.G("id"), requiredByDate = "2026-11-30", impactLevel = "Medium", impactDescription = "Context test"
        })).Json(201);
        await f.DbAsync(async db =>
        {
            (await db.ExternalParties.SingleAsync(x => x.Id == party.G("id"))).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var successor = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        await Block(successor, new { type = "Decision", id = decision.G("id") });

        var context = (await Detail(successor))["context"]!;
        var actor = context["nextActors"]![0]!;
        Assert.Equal("ExternalParty", actor.S("kind"));
        Assert.Equal("Inactive", actor.S("state"));
        Assert.Contains("Inactive vendor", actor.S("name"));
    }


    [Fact]
    public async Task Context_does_not_widen_commands_and_restricted_project_remains_inaccessible()
    {
        var project = await data.Project();
        var task = await NewTask(project.Id, new { assigneeId = data.User(TestData.Alex) });
        var before = await Detail(task);
        await EnableContext();
        var after = await Detail(task);
        Assert.Equal(before["permissions"]!.ToJsonString(), after["permissions"]!.ToJsonString());
        Assert.Equal(before["task"]!.ToJsonString(), after["task"]!.ToJsonString());

        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync();
            return 0;
        });
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/tasks/{task}")).Json(404);
    }
}
