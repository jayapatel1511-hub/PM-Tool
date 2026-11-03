using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S03 read-only effects and source comparison; normal commands establish source-use provenance.
public sealed class WorkInputPatternsTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkInputPatternsTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task<JsonNode> Detail(Guid id) => await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{id}")).Json();
    async Task<Guid> TaskIn(Project p) => (await data.NewTask(p.Id, TestData.Marc, new { assigneeId = data.User(TestData.Alex) })).G("id");
    sealed record SourceSetup(Project Project, Guid Task, Guid Old, Guid Current, Guid Use, Guid Assessment);

    async Task<SourceSetup> WithSource()
    {
        var p = await data.Project();
        var task = await TaskIn(p);
        var civil = data.ProjectDiscipline(p.Id, "Civil");
        var owner = data.User(TestData.Marc);
        var checkedAt = f.Clock.Now.AddDays(-1);
        var first = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), null, null, civil, owner, "Manual", "survey-42", "Survey corridor", "2025A", "https://example.test/survey-A.pdf",
            "Survey team", "East corridor", checkedAt, null, null, null, null, null))).Json()).G("id");
        var use = (await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/input-uses", new ChangeEndpoints.AdoptBody(
            Guid.NewGuid(), ItemType.Task, task, Version<WorkTask>(task), first, first, null, "Set the corridor alignment", "Use the registered survey"))).Json()).G("id");
        var headVersion = f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion);
        var notice = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), null, null, civil, owner, "Manual", "survey-42", "Survey corridor", "2026B", "https://example.test/survey-B.pdf",
            "Survey team", "East corridor", checkedAt, first, headVersion, "Updated survey", new(2026, 9, 17), new(2026, 9, 24)))).Json()).G("id");
        (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/changes/{notice}/publish", new ChangeEndpoints.PublishBody(
            Guid.NewGuid(), Version<ChangeNotice>(notice), f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion), null))).EnsureSuccessStatusCode();
        var current = f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).CurrentRevisionId);
        var assessment = f.Db(db => db.ChangeAssessments.Single(a => a.ProjectId == p.Id && a.InputUseId == use).Id);
        return new(p, task, first, current, use, assessment);
    }

    [Fact]
    public async Task Optional_facts_are_omitted_off_and_the_prior_detail_is_unchanged()
    {
        var setup = await WithSource();
        var off = await Detail(setup.Task);
        Assert.Null(off["sourceUses"]); Assert.Null(off["inputEffects"]); Assert.Null(off["context"]); Assert.Null(off["readinessInputs"]);
        await Enable();
        var on = await Detail(setup.Task);
        Assert.NotNull(on["sourceUses"]); Assert.NotNull(on["inputEffects"]);
        on.AsObject().Remove("sourceUses"); on.AsObject().Remove("inputEffects"); on.AsObject().Remove("context"); on.AsObject().Remove("readinessInputs");
        Assert.Equal(off.ToJsonString(), on.ToJsonString());
    }

    [Fact]
    public async Task Not_started_work_shows_exact_used_and_registered_revision_and_current_assessment()
    {
        var setup = await WithSource(); await Enable();
        var detail = await Detail(setup.Task);
        Assert.Equal(TaskStatuses.NotStarted, detail["task"]!.S("status"));
        var source = Assert.Single(detail["sourceUses"]!.AsArray())!;
        Assert.Equal(setup.Use, source.G("id"));
        Assert.Equal("2025A", source["revisionUsed"]!.S("revision")); Assert.Equal("2026B", source["currentRegisteredRevision"]!.S("revision"));
        Assert.Equal(setup.Old, source["revisionUsed"]!.G("id")); Assert.Equal(setup.Current, source["currentRegisteredRevision"]!.G("id"));
        Assert.Equal("survey-42", source["revisionUsed"]!.S("key"));
        Assert.NotNull(source["revisionUsed"]!["registeredAt"]); Assert.NotNull(source["revisionUsed"]!["sourceCheckedAt"]);
        var assessment = Assert.Single(source["assessments"]!.AsArray())!;
        Assert.Equal(setup.Assessment, assessment.G("id")); Assert.Equal(AssessmentStatus.Pending, assessment.S("status"));
        Assert.Equal(ChangeStatus.Open, assessment.S("noticeStatus")); Assert.True(source["assessmentAvailable"]!.GetValue<bool>());
        Assert.Empty(detail["inputEffects"]!.AsArray());
    }

    [Fact]
    public async Task Foreign_revision_or_mismatched_identity_is_unavailable_not_substituted()
    {
        var setup = await WithSource(); var foreign = await WithSource(); await Enable();
        await f.DbAsync(async db =>
        {
            (await db.InputUses.SingleAsync(x => x.Id == setup.Use)).SourceRevisionId = foreign.Old;
            (await db.SourceHeads.SingleAsync(x => x.ProjectId == setup.Project.Id)).CurrentRevisionId = foreign.Current;
            await db.SaveChangesAsync(); return 0;
        });
        var source = Assert.Single((await Detail(setup.Task))["sourceUses"]!.AsArray())!;
        Assert.Null(source["revisionUsed"]); Assert.Null(source["currentRegisteredRevision"]);
        Assert.False(source["assessmentAvailable"]!.GetValue<bool>()); Assert.Empty(source["assessments"]!.AsArray());
        Assert.DoesNotContain(foreign.Old.ToString(), source.ToJsonString()); Assert.DoesNotContain(foreign.Current.ToString(), source.ToJsonString());
    }

    [Fact]
    public async Task Existing_waiting_predecessor_effects_are_distinct_open_tasks_in_the_authorized_project()
    {
        var p = await data.Project(); var predecessor = await TaskIn(p); var first = await TaskIn(p); var second = await TaskIn(p);
        foreach (var id in new[] { first, second })
            (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/dependencies", new { predecessorTaskId = predecessor })).EnsureSuccessStatusCode();
        await f.Evaluate(p.Id);
        var foreign = await data.Project(); var foreignTask = await TaskIn(foreign);
        await f.DbAsync(async db =>
        {
            // Duplicate/aliased legacy references cannot inflate distinct-task effects.
            (await db.TaskStates.SingleAsync(x => x.TaskId == second)).BlockedBy = JsonSerializer.Serialize(new[] {
                new { type = "task", id = predecessor }, new { type = "Task", id = predecessor } });
            db.TaskStates.Add(new TaskState { ProjectId = p.Id, TaskId = foreignTask, IsWaiting = true,
                BlockedBy = JsonSerializer.Serialize(new[] { new { type = "task", id = predecessor } }) });
            await db.SaveChangesAsync(); return 0;
        });
        await Enable();
        var detail = await Detail(first);
        Assert.True(detail["task"]!["state"]!["isWaiting"]!.GetValue<bool>());
        var effect = Assert.Single(detail["inputEffects"]!.AsArray())!;
        Assert.Equal(RecordContextKind.Task, effect.S("kind")); Assert.Equal(predecessor, effect.G("id"));
        Assert.Equal(second, Assert.Single(effect["otherOpenTasks"]!.AsArray())!.G("id"));
        Assert.DoesNotContain(foreignTask.ToString(), effect.ToJsonString());
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(x => x.Id == second)).Status = TaskStatuses.Complete; return await db.SaveChangesAsync(); });
        Assert.Empty(Assert.Single((await Detail(first))["inputEffects"]!.AsArray())!["otherOpenTasks"]!.AsArray());
    }

    [Fact]
    public async Task Stored_handoff_blocker_references_use_the_receiver_and_count_each_other_task_once()
    {
        var p = await data.Project();
        var source = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Input source", projectDisciplineId = data.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A"
        })).Json(201)).G("id");
        var first = (await data.NewTask(p.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar) }, "Electrical")).G("id");
        var second = (await data.NewTask(p.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar) }, "Electrical")).G("id");
        var handoff = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/handoffs", new HandoffEndpoints.DraftBody(
            Guid.NewGuid(), "Survey input", source, Version<Deliverable>(source), "A", "https://example.test/input.pdf",
            data.ProjectDiscipline(p.Id, "Electrical"), data.User(TestData.Alex), data.User(TestData.Omar), first, null,
            "Use the survey", "Check the corridor", new(2026, 9, 20), new(2026, 9, 22), null, null))).Json(201)).G("id");
        await f.Evaluate(p.Id);
        await f.DbAsync(async db =>
        {
            (await db.Handoffs.SingleAsync(x => x.Id == handoff)).Status = HandoffStatus.Submitted;
            // Compatibility fixture: actual stored handoff references in the existing
            // blocker list. This does not assert that evaluation invents those links.
            foreach (var id in new[] { first, second })
            {
                var state = await db.TaskStates.SingleAsync(x => x.TaskId == id);
                state.IsBlocked = true;
                state.BlockedBy = JsonSerializer.Serialize(new[] { new { type = "handoff", id = handoff }, new { type = "Handoff", id = handoff } });
            }
            await db.SaveChangesAsync(); return 0;
        });
        await Enable();
        foreach (var (current, other) in new[] { (first, second), (second, first) })
        {
            var detail = await Detail(current);
            var input = Assert.Single(detail["context"]!["inputs"]!.AsArray())!;
            Assert.Equal(RecordContextKind.Handoff, input.S("kind")); Assert.Equal(handoff, input.G("id"));
            Assert.Equal(data.User(TestData.Omar), Assert.Single(input["nextActors"]!.AsArray())!.G("id"));
            Assert.Equal("2026-09-20", input.S("neededBy")); Assert.Equal(RecordContextAction.Accept, input.S("nextAction"));
            var effect = Assert.Single(detail["inputEffects"]!.AsArray())!;
            Assert.Equal(other, Assert.Single(effect["otherOpenTasks"]!.AsArray())!.G("id"));
        }
    }

    [Fact]
    public async Task Restricted_task_refusal_remains_404_with_source_patterns_enabled()
    {
        var setup = await WithSource(); await Enable();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == setup.Project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/tasks/{setup.Task}")).Json(404);
    }
}
