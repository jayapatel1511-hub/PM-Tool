using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S03 normal-command evidence: materialized blockers and readiness-linked work remain different contracts.
public sealed class WorkReadinessSourceEvidenceTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkReadinessSourceEvidenceTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Get(string path, string who = TestData.Omar) => await (await f.As(who).GetAsync(path)).Json();
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    sealed record StateSnapshot(bool Waiting, bool Blocked, string Inputs);
    StateSnapshot State(Guid id) => f.Db(db => { var s = db.TaskStates.AsNoTracking().Single(s => s.TaskId == id); return new StateSnapshot(s.IsWaiting, s.IsBlocked, s.BlockedBy); });
    static JsonNode Check(JsonNode readiness, string code) => readiness["checks"]!.AsArray().Single(x => x!.S("code") == code)!;

    [Fact]
    public async Task Reading_unassessed_work_keeps_unknowns_explicit_and_does_not_create_an_assessment()
    {
        var p = await data.Project(); var task = (await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        await Enable();
        var detail = await Get($"/api/v1/tasks/{task}", TestData.Alex);
        var readiness = detail["readinessInputs"]!;
        Assert.False(readiness["hasAssessment"]!.GetValue<bool>()); Assert.True(readiness["available"]!.GetValue<bool>());
        Assert.Equal(ReadinessState.NeedsAssessment, readiness.S("state"));
        Assert.Contains(ReadinessCheckCode.Handoff, readiness["unknown"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.DoesNotContain(ReadinessCheckCode.ProductionOwner, readiness["unknown"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.False(f.Db(db => db.ReadinessAssessments.Any(a => a.ProjectId == p.Id && a.TargetId == task)));
        Assert.False(f.Db(db => db.ReadinessChecks.Any(c => c.ProjectId == p.Id)));
    }

    [Fact]
    public async Task Normal_shared_predecessor_and_decision_commands_keep_both_open_tasks_in_the_exact_materialized_cohort()
    {
        var p = await data.Project(); var root = $"/api/v1/projects/{p.Id}";
        var predecessor = (await data.NewTask(p.Id, TestData.Marc, new { name = "Shared predecessor", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-13" })).G("id");
        var a = (await data.NewTask(p.Id, TestData.Marc, new { name = "First successor", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-18", dependsOn = new[] { predecessor } })).G("id");
        var b = (await data.NewTask(p.Id, TestData.Marc, new { name = "Second successor", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-18", dependsOn = new[] { predecessor } })).G("id");
        var decision = (await Post(TestData.Pm, root + "/decisions", new DecisionEndpoints.CreateBody("Shared corridor decision", "Both successors need this decision", data.User(TestData.Pm),
            data.User(TestData.Alex), null, null, new(2026, 9, 13), "High", "The shared corridor controls both outputs",
            [new(ItemType.Task, a, ItemRelation.BlockedByDecision), new(ItemType.Task, b, ItemRelation.BlockedByDecision)]), 201)).G("id");
        await f.Evaluate(p.Id); await Enable();
        foreach (var (task, other) in new[] { (a, b), (b, a) })
        {
            var detail = await Get($"/api/v1/tasks/{task}", TestData.Alex);
            // The predecessor and decision are overdue, so D-06/D-15 escalate
            // both tasks to Blocked; Waiting is mutually exclusive in that state.
            Assert.True(detail["task"]!["state"]!["isBlocked"]!.GetValue<bool>());
            Assert.False(detail["task"]!["state"]!["isWaiting"]!.GetValue<bool>());
            foreach (var (kind, inputId) in new[] { ("Task", predecessor), ("Decision", decision) })
            {
                var input = detail["context"]!["inputs"]!.AsArray().Single(x => x!.S("kind") == kind && x.G("id") == inputId)!;
                Assert.Equal(data.User(TestData.Alex), Assert.Single(input["nextActors"]!.AsArray())!.G("id"));
                Assert.Equal(kind == "Decision" ? "decide" : "start", input.S("nextAction"));
                Assert.Equal("2026-09-13", input.S("neededBy"));
                var effect = detail["inputEffects"]!.AsArray().Single(x => x!.S("kind") == kind && x.G("id") == inputId)!;
                Assert.Equal(other, Assert.Single(effect["otherOpenTasks"]!.AsArray())!.G("id"));
            }
        }
        var weekly = await Get(root + "/coordination", TestData.Pm);
        foreach (var task in new[] { a, b })
        {
            var context = weekly["contexts"]!.AsArray().Single(x => x!.S("kind") == "Task" && x.G("id") == task)!["context"]!;
            Assert.Contains(context["inputs"]!.AsArray(), x => x!.G("id") == predecessor);
            Assert.Contains(context["inputs"]!.AsArray(), x => x!.G("id") == decision);
        }
        // The browser grouping suite consumes these exact two identities and task memberships; no historical JSON was seeded here.
    }

    [Fact]
    public async Task One_handoff_target_and_explicit_linked_constraint_keep_independent_readiness_and_source_provenance()
    {
        var p = await data.Project(); var root = $"/api/v1/projects/{p.Id}";
        var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var alex = data.User(TestData.Alex); var omar = data.User(TestData.Omar); var pm = data.User(TestData.Pm);
        const string sourceUrl = "https://example.test/shared-survey-A.pdf";
        var source = await Post(TestData.Marc, root + "/deliverables", new { name = "Shared survey", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = alex, revision = "A", requiresReview = false }, 201);
        var sourceId = source.G("id");
        var revision = (await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), sourceId, Version<Deliverable>(sourceId),
            civil, alex, "Deliverable", "shared-survey", "Shared survey", "A", sourceUrl, "Survey team", "Electrical corridor", null, null, null, null, null, null))).G("id");
        Assert.Equal(revision, f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id && h.Identity == $"deliverable:{sourceId}").CurrentRevisionId));
        var a = (await data.NewTask(p.Id, TestData.Omar, new { name = "Direct layout", assigneeId = omar, dueDate = "2026-09-18" }, "Electrical")).G("id");
        var b = (await data.NewTask(p.Id, TestData.Omar, new { name = "Constrained layout", assigneeId = omar, dueDate = "2026-09-18" }, "Electrical")).G("id");
        await f.Evaluate(p.Id); var beforeA = State(a); var beforeB = State(b);
        var pathA = root + $"/readiness/Task/{a}"; var pathB = root + $"/readiness/Task/{b}";
        await Post(TestData.Omar, pathA, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(a), "Direct layout", "Checked against the survey"));
        await Post(TestData.Omar, pathB, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(b), "Constrained layout", "Checked against the survey"));
        var handoff = (await Post(TestData.Marc, root + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Shared survey handoff", sourceId, Version<Deliverable>(sourceId),
            "A", sourceUrl, electrical, alex, omar, a, null, "Set the electrical corridor", "Confirm the survey covers the interface", new(2026, 9, 20), new(2026, 9, 22), null, null), 201)).G("id");
        await Post(TestData.Alex, root + $"/handoffs/{handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, Version<Handoff>(handoff), "Submit the survey", null));
        var constraint = (await Post(TestData.Omar, pathB + "/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(b), "Handoff",
            "B has its own survey-readiness constraint", pm, new(2026, 9, 25), sourceUrl, ItemType.Handoff, handoff))).G("id");
        var useIds = new Dictionary<Guid, Guid>();
        foreach (var task in new[] { a, b }) useIds[task] = (await Post(TestData.Omar, root + "/input-uses", new ChangeEndpoints.AdoptBody(Guid.NewGuid(), ItemType.Task, task,
            Version<WorkTask>(task), revision, revision, null, "Use the registered survey", "Normal independent source adoption"))).G("id");
        var off = await Get($"/api/v1/tasks/{a}"); Assert.Null(off["readinessInputs"]);
        await Enable();
        var detailA = await Get($"/api/v1/tasks/{a}"); var detailB = await Get($"/api/v1/tasks/{b}");
        var inputA = detailA["readinessInputs"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == handoff)!;
        var inputB = detailB["readinessInputs"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == constraint)!;
        Assert.Equal("DirectTarget", inputA.S("relationship")); Assert.False(inputA["checkSatisfied"]!.GetValue<bool>());
        Assert.Equal(omar, Assert.Single(inputA["source"]!["nextActors"]!.AsArray())!.G("id"));
        Assert.Equal("2026-09-20", inputA["source"]!.S("neededBy")); Assert.Equal("accept", inputA["source"]!.S("nextAction"));
        Assert.Equal("Constraint", inputB.S("relationship")); Assert.Equal("WorkConstraint", inputB["source"]!.S("kind"));
        Assert.Equal(pm, Assert.Single(inputB["source"]!["nextActors"]!.AsArray())!.G("id"));
        Assert.Equal("2026-09-25", inputB["source"]!.S("neededBy")); Assert.Equal("remove", inputB["source"]!.S("nextAction"));
        Assert.Equal(handoff, inputB["linkedSource"]!.G("id")); Assert.Equal(HandoffStatus.Submitted, inputB.S("linkedStatus"));
        Assert.DoesNotContain(detailB["readinessInputs"]!["inputs"]!.AsArray(), x => x!.S("relationship") == "DirectTarget");
        foreach (var detail in new[] { detailA, detailB })
        {
            var taskId = detail["task"]!.G("id"); var use = Assert.Single(detail["sourceUses"]!.AsArray())!;
            Assert.Equal(useIds[taskId], use.G("id")); Assert.Equal(revision, use["revisionUsed"]!.G("id")); Assert.Equal(revision, use["currentRegisteredRevision"]!.G("id"));
            Assert.True(use["assessmentAvailable"]!.GetValue<bool>()); Assert.Empty(use["assessments"]!.AsArray());
            Assert.Equal(source.S("key"), use["revisionUsed"]!.S("key"));
        }
        var readOnly = await Get($"/api/v1/tasks/{b}", TestData.Rita);
        Assert.False(readOnly["permissions"]!["edit"]!["ok"]!.GetValue<bool>());
        var readOnlyConstraint = readOnly["readinessInputs"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == constraint)!;
        Assert.Equal(pm, Assert.Single(readOnlyConstraint["source"]!["nextActors"]!.AsArray())!.G("id"));
        var weekly = await Get(root + "/coordination", TestData.Pm);
        Assert.Contains(weekly["readinessInputs"]!.AsArray(), x => x!.G("taskId") == a);
        Assert.Contains(weekly["readinessInputs"]!.AsArray(), x => x!.G("taskId") == b);
        Assert.False(Check(await Get(pathA), ReadinessCheckCode.Handoff)["satisfied"]!.GetValue<bool>());
        await Post(TestData.Omar, root + $"/handoffs/{handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Accepted, Version<Handoff>(handoff), null, "Survey checked and accepted"));
        Assert.True(Check(await Get(pathA), ReadinessCheckCode.Handoff)["satisfied"]!.GetValue<bool>());
        Assert.False(Check(await Get(pathB), ReadinessCheckCode.Handoff)["satisfied"]!.GetValue<bool>());
        var afterB = await Get($"/api/v1/tasks/{b}"); var stillOpen = afterB["readinessInputs"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == constraint)!;
        Assert.Equal(ConstraintState.Open, stillOpen.S("status")); Assert.Equal(HandoffStatus.Accepted, stillOpen.S("linkedStatus"));
        await Post(TestData.Pm, pathB + $"/constraints/{constraint}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(constraint), ConstraintState.ResolutionProposed, "Survey accepted; propose removal", "https://example.test/constraint-resolution"));
        await Post(TestData.Omar, pathB + $"/constraints/{constraint}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(constraint), ConstraintState.VerifiedRemoved, "Verified the removal independently", null));
        var resolved = await Get($"/api/v1/tasks/{b}");
        Assert.DoesNotContain(resolved["readinessInputs"]!["inputs"]!.AsArray(), x => x!["source"]!.G("id") == constraint);
        Assert.Equal(a, f.Db(db => db.Handoffs.Single(h => h.Id == handoff).TargetTaskId));
        await f.Evaluate(p.Id); Assert.Equal(beforeA, State(a)); Assert.Equal(beforeB, State(b));
        // Readiness presentation is additive; the task identity/status stays canonical before and after acceptance.
        Assert.Equal(TaskStatuses.NotStarted, (await Get($"/api/v1/tasks/{a}"))["task"]!.S("status"));
    }
}
