using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkDeliveryReadinessTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkDeliveryReadinessTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Get(string path, string who = TestData.Pm) => await (await f.As(who).GetAsync(path)).Json();
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = on })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    static string Root(Project p) => $"/api/v1/projects/{p.Id}";
    static string Path(Project p, Guid id, string type = ItemType.Task) => Root(p) + $"/readiness/{type}/{id}";
    static string Window(Project p) => Root(p) + "/readiness/window?from=2026-09-14&to=2026-09-27";
    async Task<Guid> Task(Project p, string name, string due = "2026-09-18") =>
        (await data.NewTask(p.Id, TestData.Marc, new { name, assigneeId = data.User(TestData.Alex), dueDate = due })).G("id");
    async Task Assess(Project p, Guid id, string type = ItemType.Task, string who = TestData.Alex)
    {
        var version = type == ItemType.Task ? Version<WorkTask>(id) : Version<Deliverable>(id);
        await Post(who, Path(p, id, type), new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Synthetic coordinated output", "Checked against current sources"));
    }
    async Task Ready(Project p, Guid id, string type = ItemType.Task, string who = TestData.Alex)
    {
        await Assess(p, id, type, who);
        foreach (var code in ReadinessCheckCode.All.Where(c => c != ReadinessCheckCode.ProductionOwner))
        {
            var detail = await Get(Path(p, id, type));
            var check = detail["checks"]!.AsArray().Single(c => c!.S("code") == code)!;
            await Post(TestData.Pm, Path(p, id, type) + $"/checks/{Uri.EscapeDataString(code)}/applicability",
                new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"), check.I("rowVersion"), false,
                    "No applicable source exists for this isolated synthetic work", null));
        }
        Assert.Equal(ReadinessState.Ready, (await Get(Path(p, id, type)))["assessment"]!.S("state"));
    }
    async Task<Guid> Decision(Project p, Guid work, Guid? external = null) => (await Post(TestData.Pm, Root(p) + "/decisions",
        new DecisionEndpoints.CreateBody("Confirm the corridor", "A decision controls this exact output", data.User(TestData.Pm),
            external is null ? data.User(TestData.Alex) : null, external, null, new(2026, 9, 20), "High", "The layout waits for the corridor",
            [new(ItemType.Task, work, ItemRelation.BlockedByDecision)]), 201)).G("id");
    string Stored(Project p) => f.Db(db => JsonSerializer.Serialize(new {
        Assessments = db.ReadinessAssessments.AsNoTracking().Where(a => a.ProjectId == p.Id).OrderBy(a => a.Id).ToList(),
        Checks = db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == p.Id).OrderBy(c => c.Id).ToList(),
        Tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => new { t.Id, t.Status, t.RowVersion }).ToList(),
        Activity = db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == p.Id).Count()
    }));
    static JsonNode Item(JsonNode window, Guid id) => window["work"]!["items"]!.AsArray().Single(x => x!.G("targetId") == id)!;

    [Fact]
    public async Task Normal_commands_order_full_cohort_before_independent_paging_and_reads_never_write()
    {
        var p = await data.Project();
        var ready = await Task(p, "Early Ready work", "2026-09-14"); await Ready(p, ready);
        var assumed = await Task(p, "Limited assumption work", "2026-09-15"); await Ready(p, assumed);
        var basisRoot = Root(p) + "/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Synthetic grading", "Assume utility depth", null, null, null, null, null, null, new(2026, 9, 23), null);
        var entry = (await Post(TestData.Pm, basisRoot, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption, "Utility depth",
            data.User(TestData.Alex), data.ProjectDiscipline(p.Id, "Civil"), data.User(TestData.Marc), input, null))).G("id");
        var version = f.Db(db => db.DesignBasisVersions.AsNoTracking().Single(v => v.EntryId == entry));
        await Post(TestData.Marc, basisRoot + $"/{entry}/versions/{version.Id}/proceed", new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(), version.RowVersion,
            input.Scope, data.User(TestData.Alex), new(2026, 9, 24), "Only preliminary layout"));
        await Post(TestData.Alex, basisRoot + $"/{entry}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), version.Id, ItemType.Task, assumed, "Preliminary layout under the scoped assumption"));
        var current = await Get(Path(p, assumed));
        await Post(TestData.Pm, Path(p, assumed) + "/exceptions", new ReadinessEndpoints.ExceptionBody(Guid.NewGuid(), current["assessment"]!.I("rowVersion"),
            version.Id, Version<DesignBasisVersion>(version.Id), data.User(TestData.Marc), "Only preliminary grading", "Utility depth could alter the layout", new(2026, 9, 22)));
        var missing = await Task(p, "No assessment yet", "2026-09-17");
        var blocked = await Task(p, "Later decision blocked work", "2026-09-24"); await Ready(p, blocked);
        var decision = await Decision(p, blocked);
        await Flag(true); var before = Stored(p);
        var pages = new List<JsonNode>();
        for (var page = 1; page <= 4; page++) pages.Add(await Get(Window(p) + $"&workPage={page}&constraintsPage=1&readyPage=1&pageSize=1"));
        Assert.Equal(new[] { blocked, missing, assumed, ready }, pages.Select(x => Assert.Single(x["work"]!["items"]!.AsArray())!.G("targetId")).ToArray());
        Assert.Equal(new[] { ReadinessState.NotReady, ReadinessState.NeedsAssessment, ReadinessState.ProceedUnderAssumption, ReadinessState.Ready },
            pages.Select(x => x["work"]!["items"]![0]!.S("state")).ToArray());
        Assert.All(pages, x => { Assert.Equal(4, x["work"]!.I("total")); Assert.Equal("DatedOpenWork", x["work"]!.S("scope")); Assert.Equal(1, x.I("readyPage")); Assert.Equal(1, x.I("readyOutputsTotal")); });
        Assert.False(Item(pages[1], missing)["hasAssessment"]!.GetValue<bool>());
        var source = Item(pages[0], blocked)["presentation"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == decision)!["source"]!;
        Assert.Equal("decide", source.S("nextAction")); Assert.Equal(data.User(TestData.Alex), Assert.Single(source["nextActors"]!.AsArray())!.G("id"));
        Assert.Equal(before, Stored(p));
        Assert.False(f.Db(db => db.ReadinessAssessments.Any(a => a.ProjectId == p.Id && a.TargetId == missing)));
        Assert.False(f.Db(db => db.ReadinessChecks.Any(c => c.ProjectId == p.Id && c.AssessmentId == Guid.Empty)));
    }

    [Fact]
    public async Task Date_scope_excludes_held_terminal_undated_and_outside_work_but_includes_unassessed_deliverables()
    {
        var p = await data.Project();
        var included = await Task(p, "Included task");
        await Task(p, "Before window", "2026-09-13"); await Task(p, "After window", "2026-09-28");
        await data.NewTask(p.Id, extra: new { name = "Undated", assigneeId = data.User(TestData.Alex) });
        var held = await data.NewTask(p.Id, extra: new { name = "Held", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-18" });
        await data.Move(TestData.Marc, held, TaskStatuses.OnHold, new { reason = "Hold this synthetic work" });
        var cancelled = await data.NewTask(p.Id, extra: new { name = "Cancelled", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-18" });
        await data.Move(TestData.Marc, cancelled, TaskStatuses.Cancelled, new { reason = "Cancel this synthetic work" });
        var deliverable = (await Post(TestData.Omar, Root(p) + "/deliverables", new { name = "Unassessed electrical issue", projectDisciplineId = data.ProjectDiscipline(p.Id, "Electrical"),
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Omar), revision = "A", requiresReview = false, dueDate = "2026-09-20" }, 201)).G("id");
        await Flag(true); var before = Stored(p); var result = await Get(Window(p));
        Assert.Equal(new[] { included, deliverable }.Order(), result["work"]!["items"]!.AsArray().Select(x => x!.G("targetId")).Order());
        Assert.Equal(ItemType.Deliverable, Item(result, deliverable).S("targetType"));
        Assert.Equal(ReadinessState.NeedsAssessment, Item(result, deliverable).S("state"));
        Assert.Equal(before, Stored(p));
    }

    [Fact]
    public async Task Exact_handoff_and_independent_constraint_actions_follow_their_current_state_actors()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var source = (await Post(TestData.Marc, Root(p) + "/deliverables", new { name = "Survey source", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        var direct = (await data.NewTask(p.Id, TestData.Omar, new { name = "Direct receiving work", assigneeId = data.User(TestData.Omar), dueDate = "2026-09-18" }, "Electrical")).G("id");
        var linked = (await data.NewTask(p.Id, TestData.Omar, new { name = "Work with independent constraint", assigneeId = data.User(TestData.Omar), dueDate = "2026-09-18" }, "Electrical")).G("id");
        await Ready(p, direct, who: TestData.Omar); await Ready(p, linked, who: TestData.Omar);
        var handoff = (await Post(TestData.Marc, Root(p) + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Survey handoff", source, Version<Deliverable>(source), "A",
            "https://example.test/survey-A", electrical, data.User(TestData.Alex), data.User(TestData.Omar), direct, null, "Coordinate corridor", "Survey covers corridor",
            new(2026, 9, 20), new(2026, 9, 22), null, null), 201)).G("id");
        await Post(TestData.Alex, Root(p) + $"/handoffs/{handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, Version<Handoff>(handoff), "Submit survey", null));
        var constraint = (await Post(TestData.Omar, Path(p, linked) + "/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(linked), "Handoff",
            "Verify the survey independently", data.User(TestData.Pm), new(2026, 9, 21), "https://example.test/constraint", ItemType.Handoff, handoff))).G("id");
        await Flag(true);
        async Task<JsonNode> Input(Guid target, Guid sourceId) => (await Get(Path(p, target)))["presentation"]!["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == sourceId)!;
        var a = await Input(direct, handoff); var b = await Input(linked, constraint);
        Assert.Equal("DirectTarget", a.S("relationship")); Assert.Equal("accept", a["source"]!.S("nextAction"));
        Assert.Equal(data.User(TestData.Omar), Assert.Single(a["source"]!["nextActors"]!.AsArray())!.G("id"));
        Assert.Equal("Constraint", b.S("relationship")); Assert.Equal("remove", b["source"]!.S("nextAction"));
        Assert.Equal(data.User(TestData.Pm), Assert.Single(b["source"]!["nextActors"]!.AsArray())!.G("id")); Assert.Equal(handoff, b["linkedSource"]!.G("id"));
        await Post(TestData.Omar, Root(p) + $"/handoffs/{handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Accepted, Version<Handoff>(handoff), null, "Survey checked and accepted"));
        b = await Input(linked, constraint); Assert.Equal(ConstraintState.Open, b.S("status")); Assert.Equal(HandoffStatus.Accepted, b.S("linkedStatus"));
        Assert.Equal(ReadinessState.NotReady, (await Get(Path(p, linked)))["assessment"]!.S("state"));
        await Post(TestData.Pm, Path(p, linked) + $"/constraints/{constraint}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(constraint),
            ConstraintState.ResolutionProposed, "Survey accepted; propose removal", "https://example.test/resolution"));
        b = await Input(linked, constraint); Assert.Equal("verify_removal", b["source"]!.S("nextAction"));
        Assert.Equal(data.User(TestData.Omar), Assert.Single(b["source"]!["nextActors"]!.AsArray())!.G("id"));
        var ownerFacts = Assert.Single((await Get(Path(p, linked)))["presentation"]!["constraintOwners"]!.AsArray())!;
        Assert.Equal(constraint, ownerFacts.G("id")); Assert.Equal(data.User(TestData.Pm), ownerFacts["removalOwner"]!.G("id"));
        Assert.Equal(data.User(TestData.Omar), ownerFacts["affectedOwner"]!.G("id")); Assert.Equal("Active", ownerFacts["removalOwner"]!.S("state"));
        Assert.Equal(f.Db(db => db.Users.Single(u => u.Id == data.User(TestData.Pm)).DisplayName), ownerFacts["removalOwner"]!.S("name"));
        Assert.Equal(data.User(TestData.Pm), f.Db(db => db.WorkConstraints.Single(c => c.Id == constraint).RemovalOwnerId));
        Assert.False(b["checkSatisfied"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Applicability_uses_current_project_pm_and_lead_permissions_never_recorder_or_viewer_and_preserves_command_guards()
    {
        var p = await data.Project(); var task = await Task(p, "Unassessed applicability"); await Assess(p, task); await Flag(true);
        var detail = await Get(Path(p, task), TestData.Rita); var actors = detail["presentation"]!["applicability"]!;
        Assert.Equal("Multiple", actors.S("state"));
        Assert.Equal(new[] { data.User(TestData.Pm), data.User(TestData.Marc) }.Order(), actors["candidates"]!.AsArray().Where(x => x!["allowed"]!.GetValue<bool>()).Select(x => x!["person"]!.G("id")).Order());
        Assert.DoesNotContain(actors["candidates"]!.AsArray(), x => x!["person"]!.G("id") == data.User(TestData.Rita) || x["person"]!.G("id") == data.User(TestData.Alex));
        var check = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        var command = new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"), check.I("rowVersion"), false, "No handoff applies to this work", null);
        var route = Path(p, task) + "/checks/Handoff/applicability";
        await Post(TestData.Rita, route, command, 403);
        await Post(TestData.Pm, route, command with { RequestId = Guid.NewGuid(), CheckRowVersion = -1 }, 409);
        await Post(TestData.Pm, route, command); // current command remains authoritative
        await Post(TestData.Pm, route, command with { RequestId = Guid.NewGuid() }, 409);
        var jill = data.User(TestData.Jill);
        // Synthetic current/removed roster facts, not fabricated check states.
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = jill, Roles = [ProjectRole.PM] }); return await db.SaveChangesAsync(); });
        actors = (await Get(Path(p, task)))["presentation"]!["applicability"]!;
        Assert.Contains(actors["candidates"]!.AsArray(), x => x!["person"]!.G("id") == jill && x["allowed"]!.GetValue<bool>());
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == jill)).RemovedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        actors = (await Get(Path(p, task)))["presentation"]!["applicability"]!;
        Assert.DoesNotContain(actors["candidates"]!.AsArray(), x => x!["person"]!.G("id") == jill);
        // Explicit synthetic roster damage exercises recovery without inventing an actor or changing a check result.
        var marc = data.User(TestData.Marc); var pm = data.User(TestData.Pm);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == marc)).IsActive = false;
            db.UserRoles.Add(new UserSystemRole { UserId = pm, Role = SystemRole.ReadOnly }); return await db.SaveChangesAsync(); });
        actors = (await Get(Path(p, task)))["presentation"]!["applicability"]!;
        Assert.Equal("Refused", actors.S("state")); Assert.All(actors["candidates"]!.AsArray(), x => Assert.False(x!["allowed"]!.GetValue<bool>()));
        Assert.Contains(actors["candidates"]!.AsArray(), x => x!["person"]!.S("state") == "Inactive");
        Assert.All(actors["candidates"]!.AsArray(), x => Assert.False(string.IsNullOrWhiteSpace(x!.S("refusal"))));
    }

    [Fact]
    public async Task External_decision_and_source_less_unknown_remain_explicit_and_malformed_capture_is_unavailable()
    {
        var p = await data.Project(); var task = await Task(p, "External approval work"); await Ready(p, task);
        var party = (await Post(TestData.Alex, Root(p) + "/external-parties", new DecisionEndpoints.PartyBody("Synthetic municipal approver", "Municipality", null, "Approver", true, null), 201)).G("id");
        var decision = await Decision(p, task, party); await Flag(true);
        var present = (await Get(Path(p, task)))["presentation"]!;
        var source = present["inputs"]!.AsArray().Single(x => x!["source"]!.G("id") == decision)!["source"]!;
        Assert.Equal("decide", source.S("nextAction")); var actor = Assert.Single(source["nextActors"]!.AsArray())!;
        Assert.Equal(party, actor.G("id")); Assert.Equal("ExternalParty", actor.S("kind"));
        var unknown = await Task(p, "Source-less unknown"); await Assess(p, unknown);
        present = (await Get(Path(p, unknown)))["presentation"]!;
        Assert.Contains(present["checks"]!.AsArray(), x => x!.S("code") == ReadinessCheckCode.Handoff && x["unknown"]!.GetValue<bool>());
        Assert.DoesNotContain(present["inputs"]!.AsArray(), x => x!.S("code") == ReadinessCheckCode.Handoff);
        // Synthetic partial import, rather than a fabricated canonical state.
        await f.DbAsync(db => db.ReadinessChecks.Where(c => c.ProjectId == p.Id && c.Code == ReadinessCheckCode.Handoff &&
            db.ReadinessAssessments.Any(a => a.Id == c.AssessmentId && a.TargetId == unknown)).ExecuteDeleteAsync());
        var before = Stored(p); var row = Item(await Get(Window(p)), unknown);
        Assert.False(row["available"]!.GetValue<bool>()); Assert.Equal(ReadinessState.NeedsAssessment, row.S("state"));
        Assert.Empty(row["presentation"]!["checks"]!.AsArray()); Assert.Equal(before, Stored(p));
    }

    [Fact]
    public async Task Disabled_reads_keep_legacy_shape_and_scope_restrictions_still_hide_the_project()
    {
        var p = await data.Project(); var task = await Task(p, "Legacy Ready work"); await Ready(p, task);
        var offWindow = await Get(Window(p)); var offDetail = await Get(Path(p, task));
        Assert.Null(offWindow["work"]); Assert.Null(offDetail["presentation"]);
        await Flag(true); var onWindow = await Get(Window(p)); var onDetail = await Get(Path(p, task));
        Assert.NotNull(onWindow["work"]); Assert.NotNull(onDetail["presentation"]);
        onWindow.AsObject().Remove("work"); onDetail.AsObject().Remove("presentation");
        Assert.True(JsonNode.DeepEquals(offWindow, onWindow)); Assert.True(JsonNode.DeepEquals(offDetail, onDetail));
        await Flag(false); Assert.True(JsonNode.DeepEquals(offWindow, await Get(Window(p) + "&workPage=999")));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await Flag(true);
        await (await f.As(TestData.Rita).GetAsync(Window(p))).Json(404);
        await (await f.As(TestData.Rita).GetAsync(Path(p, task))).Json(404);
        Assert.NotNull((await Get(Window(p)))["work"]);
    }
}
