using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-SCR-01 / AC-SCR-01: explicit relations retain their declared project/type,
/// normalize deterministically when enabled, and never hydrate malformed foreign targets.
public sealed class ProjectRelationContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public ProjectRelationContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task Patterns(bool enabled) =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = enabled })).EnsureSuccessStatusCode();
    Task<JsonNode> Read(string path, string as_ = TestData.Alex) => f.As(as_).GetAsync(path).Result.Json();
    Task<JsonNode> Decision(Project p, string subject = "Confirm the crossing") =>
        f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/decisions", new { subject, description = "Choose the recorded option", ownerUserId = d.User(TestData.Alex),
            requiredByDate = "2026-09-21", impactLevel = "Medium", impactDescription = "Confirm before issuing" }).Result.Json(201);
    async Task<JsonNode> Deliverable(Project p, string name = "Civil drawings") =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name, projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), requiresReview = false }).Result.Json(201);
    Task<JsonNode> Meeting(Project p) => f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/meetings", new { title = "Coordination", meetingDate = "2026-09-14", meetingType = "Coordination" }).Result.Json(201);
    Task<JsonNode> Risk(Project p, string title) => f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/risks", new { title, probability = 2, impact = 2, ownerId = d.User(TestData.Alex) }).Result.Json(201);
    Task<JsonNode> Issue(Project p, string title) => f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/issues", new { title, severity = "Medium", ownerId = d.User(TestData.Alex) }).Result.Json(201);
    ItemLink Link(Project p, string sourceType, Guid sourceId, string targetType, Guid targetId, string relation = ItemRelation.Related, int age = 0) =>
        new() { ProjectId = p.Id, SourceType = sourceType, SourceId = sourceId, TargetType = targetType, TargetId = targetId, Relation = relation,
            CreatedAt = f.Clock.GetUtcNow().AddMinutes(age), CreatedBy = d.User(TestData.Pm) };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Decision_relation_hydration_rejects_foreign_missing_deleted_and_mismatched_types(bool enabled)
    {
        var p = await d.Project(); var other = await d.Project();
        var decision = await Decision(p); var local = await d.NewTask(p.Id, extra: new { name = "Local relation" });
        var foreign = await d.NewTask(other.Id, extra: new { name = "Foreign task sentinel", assigneeId = d.User(TestData.Jill), dueDate = "2026-09-30" });
        var foreignD = await Deliverable(other, "Foreign deliverable sentinel");
        var localD = await Deliverable(p, "Wrong declared type sentinel");
        var foreignM = await f.As(TestData.Pm).Post($"/api/v1/projects/{other.Id}/milestones", new { name = "Foreign milestone sentinel", milestoneType = "Design Submission", date = "2026-10-01" }).Result.Json(201);
        var deleted = await d.NewTask(p.Id, extra: new { name = "Deleted task sentinel" });
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == other.Id)).Visibility = Visibility.Restricted;
            (await db.Tasks.SingleAsync(x => x.Id == deleted.G("id"))).DeletedAt = f.Clock.GetUtcNow();
            db.ItemLinks.AddRange(
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, local.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, foreign.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Deliverable, foreignD.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Milestone, foreignM.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, localD.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, deleted.G("id")),
                Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, Guid.NewGuid()),
                Link(p, ItemType.Risk, decision.G("id"), ItemType.Deliverable, localD.G("id")),
                Link(other, ItemType.Decision, decision.G("id"), ItemType.Deliverable, localD.G("id")));
            await db.SaveChangesAsync(); return 0;
        });
        await Patterns(enabled);
        var detail = await Read($"/api/v1/decisions/{decision.G("id")}");
        Assert.Equal(local.G("id"), Assert.Single(detail["links"]!.AsArray())!.G("targetId"));
        Assert.Empty(detail["decision"]!["deliverables"]!.AsArray()); Assert.Empty(detail["decision"]!["milestones"]!.AsArray());
        foreach (var name in new[] { "Foreign task sentinel", "Foreign deliverable sentinel", "Foreign milestone sentinel", "Wrong declared type sentinel", "Deleted task sentinel" })
            Assert.DoesNotContain(name, detail.ToJsonString());
        await f.As(TestData.Rita).GetAsync($"/api/v1/tasks/{foreign.G("id")}").Result.Json(404);
    }

    [Fact]
    public async Task Flagged_links_sort_by_relationship_key_id_and_keep_explicit_provenance_once()
    {
        var p = await d.Project(); var decision = await Decision(p);
        var first = await d.NewTask(p.Id, extra: new { name = "First task" }); var second = await d.NewTask(p.Id, extra: new { name = "Second task" });
        var third = await d.NewTask(p.Id, extra: new { name = "Third task" });
        var rows = new[] {
            Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, third.G("id"), age: -4),
            Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, second.G("id"), age: -3),
            Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, first.G("id"), age: -2),
            Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, first.G("id"), ItemRelation.BlockedByDecision, -1),
        };
        await f.DbAsync(async db => { db.ItemLinks.AddRange(rows); await db.SaveChangesAsync(); return 0; });
        var off = await Read($"/api/v1/decisions/{decision.G("id")}");
        Assert.Equal(rows.Select(x => x.Id), off["links"]!.AsArray().Select(x => x!.G("id")));
        await Patterns(true);
        var on = await Read($"/api/v1/decisions/{decision.G("id")}");
        var normalized = on["links"]!.AsArray();
        Assert.Equal(new[] { rows[3].Id, rows[1].Id, rows[0].Id }, normalized.Select(x => x!.G("id")));
        Assert.Equal(ItemRelation.BlockedByDecision, normalized[0]!.S("relation"));
        on.AsObject().Remove("context"); on["links"] = off["links"]!.DeepClone();
        Assert.Equal(off.ToJsonString(), on.ToJsonString()); // every other valid-record field, including permission response
        await Patterns(false);
        Assert.Equal(off.ToJsonString(), (await Read($"/api/v1/decisions/{decision.G("id")}")).ToJsonString());
    }

    [Fact]
    public async Task A_guid_shared_by_different_record_kinds_is_hydrated_by_declared_kind()
    {
        var p = await d.Project(); var decision = await Decision(p); var task = await d.NewTask(p.Id, extra: new { name = "Task at shared GUID" });
        var type = await d.DeliverableType();
        await f.DbAsync(async db =>
        {
            db.Deliverables.Add(new Deliverable { Id = task.G("id"), ProjectId = p.Id, ProjectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), DeliverableTypeId = type,
                Seq = 101, Key = p.ProjectNumber + "-D101", Name = "Deliverable at shared GUID", OwnerId = d.User(TestData.Alex), RequiresReview = false });
            db.ItemLinks.AddRange(Link(p, ItemType.Decision, decision.G("id"), ItemType.Task, task.G("id")), Link(p, ItemType.Decision, decision.G("id"), ItemType.Deliverable, task.G("id")));
            await db.SaveChangesAsync(); return 0;
        });
        await Patterns(true);
        var links = (await Read($"/api/v1/decisions/{decision.G("id")}"))["links"]!.AsArray();
        Assert.Equal("Task at shared GUID", links.Single(x => x!.S("targetType") == ItemType.Task)!.S("name"));
        Assert.Equal("Deliverable at shared GUID", links.Single(x => x!.S("targetType") == ItemType.Deliverable)!.S("name"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Action_direct_reference_and_register_reads_do_not_expose_another_projects_metadata(bool enabled)
    {
        var p = await d.Project(); var other = await d.Project(); var meeting = await Meeting(p);
        var foreignTask = await d.NewTask(other.Id, extra: new { name = "Foreign action task sentinel" }); var foreignDecision = await Decision(other, "Foreign action decision sentinel");
        var action = await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Local action", ownerType = "User", ownerUserId = d.User(TestData.Alex) }).Result.Json(201);
        await f.DbAsync(async db =>
        {
            var a = await db.Actions.SingleAsync(x => x.Id == action.G("id")); a.RelatedTaskId = foreignTask.G("id"); a.RelatedDecisionId = foreignDecision.G("id");
            db.ItemLinks.Add(Link(p, ItemType.Action, a.Id, ItemType.Task, foreignTask.G("id")));
            await db.SaveChangesAsync(); return 0;
        });
        await Patterns(enabled);
        var detail = await Read($"/api/v1/actions/{action.G("id")}");
        Assert.Null(detail["task"]); Assert.Null(detail["decision"]); Assert.Empty(detail["links"]!.AsArray());
        Assert.Null(detail["action"]!["taskKey"]); Assert.Null(detail["action"]!["decisionKey"]);
        var register = await Read($"/api/v1/projects/{p.Id}/actions");
        foreach (var response in new[] { detail, register })
            foreach (var name in new[] { "Foreign action task sentinel", "Foreign action decision sentinel", foreignTask.S("key"), foreignDecision.S("key") })
                Assert.DoesNotContain(name, response.ToJsonString());
    }

    [Fact]
    public async Task Ordinary_action_create_retains_one_explicit_link_alongside_its_same_direct_reference()
    {
        var p = await d.Project(); var meeting = await Meeting(p); var task = await d.NewTask(p.Id);
        var action = await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Record a crossing check", ownerType = "User", ownerUserId = d.User(TestData.Alex),
            relatedTaskId = task.G("id"), links = new[] { new { targetType = ItemType.Task, targetId = task.G("id") } } }).Result.Json(201);
        await Patterns(true);
        var detail = await Read($"/api/v1/actions/{action.G("id")}");
        Assert.Equal(task.G("id"), detail["task"]!.G("id"));
        var link = Assert.Single(detail["links"]!.AsArray())!;
        Assert.Equal(task.G("id"), link.G("targetId")); Assert.Equal(ItemRelation.Related, link.S("relation"));
        // The panel normalizes these two stored representations without rewriting provenance.
        Assert.Equal(1, await f.DbAsync(db => db.ItemLinks.CountAsync(x => x.SourceId == action.G("id"))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Risk_and_issue_origin_reads_do_not_expose_foreign_metadata(bool enabled)
    {
        var p = await d.Project(); var other = await d.Project();
        var risk = await Risk(p, "Local risk"); var issue = await Issue(p, "Local issue");
        var foreignRisk = await Risk(other, "Foreign risk sentinel"); var foreignIssue = await Issue(other, "Foreign issue sentinel");
        await f.DbAsync(async db =>
        {
            (await db.Risks.SingleAsync(x => x.Id == risk.G("id"))).RealisedIssueId = foreignIssue.G("id");
            (await db.Issues.SingleAsync(x => x.Id == issue.G("id"))).OriginRiskId = foreignRisk.G("id");
            await db.SaveChangesAsync(); return 0;
        });
        await Patterns(enabled);
        var riskDetail = await Read($"/api/v1/risks/{risk.G("id")}"); var issueDetail = await Read($"/api/v1/issues/{issue.G("id")}");
        Assert.Null(riskDetail["realisedIssue"]); Assert.Null(riskDetail["risk"]!["realisedIssueKey"]);
        Assert.Null(issueDetail["originRisk"]); Assert.Null(issueDetail["issue"]!["originRiskKey"]);
        foreach (var response in new[] { riskDetail, issueDetail })
        { Assert.DoesNotContain("Foreign risk sentinel", response.ToJsonString()); Assert.DoesNotContain("Foreign issue sentinel", response.ToJsonString()); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deliverable_explicit_and_derived_relations_do_not_hydrate_foreign_members_or_targets(bool enabled)
    {
        var p = await d.Project(); var other = await d.Project();
        var self = await Deliverable(p, "Local package"); var foreign = await Deliverable(other, "Foreign dependency sentinel");
        var localTask = await d.NewTask(p.Id, extra: new { deliverableId = self.G("id") });
        var foreignTask = await d.NewTask(other.Id, extra: new { name = "Foreign predecessor sentinel", deliverableId = foreign.G("id") });
        var foreignMember = await d.NewTask(other.Id, extra: new { name = "Foreign member sentinel" });
        await f.DbAsync(async db =>
        {
            // These cannot be created through normal commands; legacy reads must still fail closed.
            db.DeliverableDependencies.Add(new() { ProjectId = p.Id, PredecessorDeliverableId = foreign.G("id"), SuccessorDeliverableId = self.G("id") });
            db.Dependencies.Add(new() { ProjectId = p.Id, PredecessorTaskId = foreignTask.G("id"), SuccessorTaskId = localTask.G("id") });
            (await db.Tasks.SingleAsync(x => x.Id == foreignMember.G("id"))).DeliverableId = self.G("id");
            await db.SaveChangesAsync(); return 0;
        });
        await Patterns(enabled);
        var detail = await Read($"/api/v1/deliverables/{self.G("id")}");
        Assert.Empty(detail["explicitPredecessors"]!.AsArray()); Assert.Empty(detail["derivedPredecessors"]!.AsArray());
        Assert.Equal(localTask.G("id"), Assert.Single(detail["deliverable"]!["tasks"]!.AsArray())!.G("id"));
        Assert.DoesNotContain("Foreign dependency sentinel", detail.ToJsonString()); Assert.DoesNotContain("Foreign member sentinel", detail.ToJsonString());
        Assert.DoesNotContain("Foreign predecessor sentinel", detail.ToJsonString());
    }
}
