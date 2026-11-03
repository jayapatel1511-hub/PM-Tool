using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 052/053 API contract for the risk, issue and meeting-action context reader.
/// Each test owns its factory so the flag and project visibility cannot leak between cases.
public sealed class OperationalContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;

    public OperationalContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task EnableContext() =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();

    async Task<JsonNode> Meeting(Guid projectId) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/meetings", new { title = "Context meeting", meetingDate = "2026-09-14" }).Result.Json(201);

    async Task<JsonNode> Action(JsonNode meeting, string ownerType, Guid owner, string? due = "2026-09-21") =>
        await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new
        {
            text = "Context action", ownerType, dueDate = due,
            ownerUserId = ownerType == ActionOwnerType.User ? owner : (Guid?)null,
            ownerDisciplineId = ownerType == ActionOwnerType.Discipline ? owner : (Guid?)null,
            ownerExternalPartyId = ownerType == ActionOwnerType.ExternalParty ? owner : (Guid?)null,
        }).Result.Json(201);

    async Task<JsonNode> Risk(Guid projectId, Guid owner, DateOnly? review = null) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/risks", new
        {
            title = "Context risk", ownerId = owner, probability = 2, impact = 3,
            reviewDate = review?.ToString("yyyy-MM-dd"), projectDisciplineId = d.ProjectDiscipline(projectId, "Civil"),
        }).Result.Json(201);

    async Task<JsonNode> Issue(Guid projectId, Guid owner, DateOnly? target = null) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/issues", new
        {
            title = "Context issue", ownerId = owner, raisedById = d.User(TestData.Pm), severity = "High",
            dateRaised = "2026-09-14", targetResolutionDate = target?.ToString("yyyy-MM-dd"),
            projectDisciplineId = d.ProjectDiscipline(projectId, "Civil"),
        }).Result.Json(201);

    [Fact]
    public async Task Context_is_omitted_until_enabled_and_restricted_project_still_returns_404()
    {
        var p = await d.Project();
        var risk = await Risk(p.Id, d.User(TestData.Alex), new(2026, 9, 30));
        var issue = await Issue(p.Id, d.User(TestData.Alex), new(2026, 10, 2));
        var action = await Action(await Meeting(p.Id), ActionOwnerType.User, d.User(TestData.Alex));
        var paths = new[] { $"/api/v1/risks/{risk.G("id")}", $"/api/v1/issues/{issue.G("id")}", $"/api/v1/actions/{action.G("id")}" };
        var permissions = new List<string>();
        foreach (var path in paths)
        {
            var before = await (await f.As(TestData.Pm).GetAsync(path)).Json();
            Assert.Null(before["context"]); permissions.Add(before["permissions"]!.ToJsonString());
        }
        await EnableContext();
        for (var i = 0; i < paths.Length; i++)
        {
            var enabled = await (await f.As(TestData.Pm).GetAsync(paths[i])).Json();
            Assert.NotNull(enabled["context"]); Assert.Equal(permissions[i], enabled["permissions"]!.ToJsonString());
        }
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        foreach (var path in paths) Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(path)).StatusCode);
        await f.DbAsync(async db =>
        {
            (await db.Risks.SingleAsync(x => x.Id == risk.G("id"))).DeletedAt = f.Clock.Now;
            (await db.Issues.SingleAsync(x => x.Id == issue.G("id"))).DeletedAt = f.Clock.Now;
            (await db.Actions.SingleAsync(x => x.Id == action.G("id"))).DeletedAt = f.Clock.Now;
            return await db.SaveChangesAsync();
        });
        foreach (var path in paths) Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Risk_and_issue_context_keep_owner_date_and_discipline_facts()
    {
        await EnableContext();
        var p = await d.Project();
        var owner = d.User(TestData.Alex);
        var risk = await Risk(p.Id, owner, new(2026, 9, 30));
        var issue = await Issue(p.Id, owner, new(2026, 10, 2));
        var riskContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/risks/{risk.G("id")}")).Json())["context"]!;
        var issueContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{issue.G("id")}")).Json())["context"]!;
        Assert.Equal(owner, riskContext["owner"]!.G("id"));
        Assert.Equal("2026-09-30", riskContext.S("neededBy"));
        Assert.Equal(RecordContextAction.Review, riskContext.S("nextAction"));
        Assert.Equal(owner, issueContext["owner"]!.G("id"));
        Assert.Equal("2026-10-02", issueContext.S("neededBy"));
        Assert.Equal(RecordContextAction.ReviewOrResolve, issueContext.S("nextAction"));

        var action = await Action(await Meeting(p.Id), ActionOwnerType.Discipline, d.ProjectDiscipline(p.Id, "Civil"));
        var actionContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{action.G("id")}")).Json())["context"]!;
        Assert.Equal("Discipline", actionContext["owner"]!.S("kind"));
        Assert.Equal(d.ProjectDiscipline(p.Id, "Civil"), actionContext["owner"]!.G("id"));
        Assert.Equal("Civil", actionContext["owner"]!.S("name"));
        Assert.Equal(d.User(TestData.Marc), actionContext["nextActors"]![0]!.G("id"));
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == d.User(TestData.Marc))).IsActive = false;
            (await db.ProjectDisciplines.SingleAsync(x => x.Id == d.ProjectDiscipline(p.Id, "Civil"))).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var inactiveLead = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{action.G("id")}")).Json())["context"]!;
        Assert.Equal("Inactive", inactiveLead["owner"]!.S("state"));
        Assert.Equal(d.User(TestData.Marc), inactiveLead["nextActors"]![0]!.G("id"));
        Assert.Equal("Inactive", inactiveLead["nextActors"]![0]!.S("state"));
    }

    [Fact]
    public async Task Action_context_routes_user_discipline_without_lead_and_external_party()
    {
        await EnableContext();
        var geo = await d.Discipline("Geotechnical");
        var p = await d.Project(tweak: b => b["disciplines"] = new[] { new { disciplineId = geo, leadUserId = (Guid?)null } });
        var meeting = await Meeting(p.Id);
        var user = await Action(meeting, ActionOwnerType.User, d.User(TestData.Alex));
        var discipline = await Action(meeting, ActionOwnerType.Discipline, d.ProjectDiscipline(p.Id, "Geotechnical"));
        var party = (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/external-parties", new { name = "Context party", isClient = true }).Result.Json(201)).G("id");
        var external = await Action(meeting, ActionOwnerType.ExternalParty, party);

        var userContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{user.G("id")}")).Json())["context"]!;
        var noLeadContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{discipline.G("id")}")).Json())["context"]!;
        var externalContext = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{external.G("id")}")).Json())["context"]!;
        Assert.Equal(d.User(TestData.Alex), userContext["nextActors"]![0]!.G("id"));
        Assert.Empty(noLeadContext["nextActors"]!.AsArray());
        Assert.Equal("Unassigned", noLeadContext.S("actorState"));
        Assert.Equal(party, externalContext["owner"]!.G("id"));
        Assert.Equal("ExternalParty", externalContext["owner"]!.S("kind"));
        Assert.Equal(party, Assert.Single(externalContext["nextActors"]!.AsArray())!.G("id"));
        Assert.Equal(RecordContextAction.Complete, externalContext.S("nextAction"));
        await f.DbAsync(async db => { (await db.ExternalParties.SingleAsync(x => x.Id == party)).IsActive = false; return await db.SaveChangesAsync(); });
        var inactiveParty = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{external.G("id")}")).Json())["context"]!;
        Assert.Equal("Inactive", inactiveParty["owner"]!.S("state"));
        Assert.Equal("Inactive", Assert.Single(inactiveParty["nextActors"]!.AsArray())!.S("state"));
    }

    [Theory]
    [InlineData(RecordContextKind.Risk, RiskStatus.Closed)]
    [InlineData(RecordContextKind.Risk, RiskStatus.Realised)]
    [InlineData(RecordContextKind.Issue, IssueStatus.Resolved)]
    [InlineData(RecordContextKind.Issue, IssueStatus.Cancelled)]
    [InlineData(RecordContextKind.Action, ActionStatus.Complete)]
    [InlineData(RecordContextKind.Action, ActionStatus.Cancelled)]
    public async Task Terminal_context_is_complete_and_retains_owner_and_date(string kind, string status)
    {
        await EnableContext();
        var p = await d.Project();
        var owner = d.User(TestData.Alex);
        Guid id;
        string path;
        if (kind == RecordContextKind.Risk) { var x = await Risk(p.Id, owner, new(2026, 9, 30)); id = x.G("id"); path = "risks"; }
        else if (kind == RecordContextKind.Issue) { var x = await Issue(p.Id, owner, new(2026, 10, 2)); id = x.G("id"); path = "issues"; }
        else { var x = await Action(await Meeting(p.Id), ActionOwnerType.User, owner); id = x.G("id"); path = "actions"; }
        await f.DbAsync(async db =>
        {
            if (kind == RecordContextKind.Risk) (await db.Risks.SingleAsync(x => x.Id == id)).Status = status;
            else if (kind == RecordContextKind.Issue) (await db.Issues.SingleAsync(x => x.Id == id)).Status = status;
            else (await db.Actions.SingleAsync(x => x.Id == id)).Status = status;
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/{path}/{id}")).Json())["context"]!;
        Assert.True(context["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, context.S("nextAction"));
        Assert.Empty(context["nextActors"]!.AsArray());
        Assert.Equal(owner, context["owner"]!.G("id"));
    }

    [Fact]
    public async Task Converted_action_follows_linked_task_context_and_cannot_transition_directly()
    {
        await EnableContext();
        var p = await d.Project();
        var due = "2026-09-25";
        var action = await Action(await Meeting(p.Id), ActionOwnerType.User, d.User(TestData.Alex), due);
        var converted = await f.As(TestData.Pm).Post($"/api/v1/actions/{action.G("id")}/convert", new { rowVersion = await f.DbAsync(db => db.Actions.Where(x => x.Id == action.G("id")).Select(x => x.RowVersion).SingleAsync()) }).Result.Json();
        var taskId = converted.G("taskId");
        var taskKey = converted.S("taskKey");
        await f.DbAsync(async db =>
        {
            var t = await db.Tasks.SingleAsync(x => x.Id == taskId);
            t.Status = TaskStatuses.InReview; t.ReviewerId = d.User(TestData.Diane); t.DueDate = DateOnly.Parse(due);
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{action.G("id")}")).Json())["context"]!;
        Assert.Equal("follow_linked_task", context.S("nextAction"));
        Assert.Equal(d.User(TestData.Diane), context["nextActors"]![0]!.G("id"));
        Assert.Equal(taskId, context["inputs"]![0]!.G("id"));
        Assert.Equal("Task", context["inputs"]![0]!.S("kind"));
        Assert.Equal(taskKey, context["inputs"]![0]!.S("key"));
        Assert.Equal(RecordContextAction.Review, context["inputs"]![0]!.S("nextAction"));
        Assert.True(context["inputs"]![0]!["primary"]!.GetValue<bool>());
        Assert.Equal(due, context.S("neededBy"));
        Assert.Empty((await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{action.G("id")}")).Json())["permissions"]!["transitions"]!.AsArray());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("deleted")]
    [InlineData("cross_project")]
    [InlineData("multiple")]
    [InlineData("link_project")]
    [InlineData("source_type")]
    public async Task Invalid_converted_task_targets_are_not_assessed_and_never_leak_ids(string mode)
    {
        await EnableContext();
        var p = await d.Project();
        var action = await Action(await Meeting(p.Id), ActionOwnerType.User, d.User(TestData.Alex));
        var task = await d.NewTask(p.Id, TestData.Alex);
        Guid target = task.G("id");
        if (mode == "missing") target = Guid.NewGuid();
        if (mode == "deleted") await f.DbAsync(async db => { (await db.Tasks.SingleAsync(x => x.Id == target)).DeletedAt = f.Clock.Now; await db.SaveChangesAsync(); return 0; });
        if (mode == "cross_project") { var other = await d.Project(); target = (await d.NewTask(other.Id, TestData.Alex)).G("id"); }
        var linkProject = mode == "link_project" ? (await d.Project()).Id : p.Id;
        await f.DbAsync(async db =>
        {
            db.ItemLinks.Add(new ItemLink { ProjectId = linkProject, SourceType = mode == "source_type" ? ItemType.Task : ItemType.Action, SourceId = action.G("id"), TargetType = ItemType.Task, TargetId = target, Relation = ItemRelation.ConvertedToTask });
            if (mode == "multiple") db.ItemLinks.Add(new ItemLink { ProjectId = p.Id, SourceType = ItemType.Action, SourceId = action.G("id"), TargetType = ItemType.Task, TargetId = Guid.NewGuid(), Relation = ItemRelation.ConvertedToTask });
            await db.SaveChangesAsync(); return 0;
        });
        var body = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{action.G("id")}")).Json()).ToJsonString();
        var context = JsonNode.Parse(body)!["context"]!;
        Assert.Equal("not_assessed", context.S("nextAction"));
        Assert.Empty(context["nextActors"]!.AsArray());
        Assert.DoesNotContain(target.ToString(), context.ToJsonString());
    }
}
