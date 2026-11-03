using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S38 provider evidence: normal source relationships first; large/import-damage cases are explicitly synthetic.
public sealed class CoordinationConsequenceAgendaTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public CoordinationConsequenceAgendaTests() => data = new(f);
    public void Dispose() => f.Dispose();
    sealed record Setup(Project Project, Guid Handoff, Guid Direct, Guid Linked, Guid Constraint, Guid Civil, Guid Electrical);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    string Root(Setup s) => $"/api/v1/projects/{s.Project.Id}";
    string Links(Setup s) => Root(s) + $"/handoffs/{s.Handoff}/readiness-linked-tasks";
    string Agenda(Setup s) => Root(s) + "/coordination/readiness-agenda?from=2026-11-09&to=2026-11-15";
    async Task<JsonNode> Get(string path, int status = 200, string who = TestData.Rita) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task Flag(bool on, string name = "register_screens") => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();
    async Task<Guid> Constraint(Setup s, Guid task, string category = "Handoff", string date = "2026-11-08") =>
        (await Post(TestData.Omar, Root(s) + $"/readiness/Task/{task}/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(task),
            category, "Synthetic explicit readiness link", data.User(TestData.Pm), DateOnly.Parse(date), "https://example.test/g59-input", ItemType.Handoff, s.Handoff))).G("id");
    async Task<Guid> Proposal(Setup s, Guid task, string date = "2026-11-12") =>
        (await Post(TestData.Omar, Root(s) + $"/weekly-commitments/Task/{task}", new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), Version<WorkTask>(task),
            new(2026, 11, 9), DateOnly.Parse(date), "Synthetic promised output", "Independently checked output"))).G("id");

    async Task<Setup> New(bool deliverableTarget = false)
    {
        var p = await data.Project(); var root = $"/api/v1/projects/{p.Id}";
        var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var source = (await Post(TestData.Marc, root + "/deliverables", new { name = "Synthetic survey", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), source, Version<Deliverable>(source), civil,
            data.User(TestData.Alex), "Deliverable", "g59-survey", "Synthetic survey", "A", "https://example.test/g59-survey", "Survey team", "Synthetic scope", null, null, null, null, null, null));
        var a = (await data.NewTask(p.Id, TestData.Omar, new { name = "Direct task", assigneeId = data.User(TestData.Omar), dueDate = "2026-11-10", requiresReview = false }, "Electrical")).G("id");
        var b = (await data.NewTask(p.Id, TestData.Omar, new { name = "Explicitly constrained task", assigneeId = data.User(TestData.Omar), dueDate = "2026-11-12", requiresReview = false }, "Electrical")).G("id");
        Guid? targetDeliverable = deliverableTarget ? (await Post(TestData.Omar, root + "/deliverables", new { name = "Direct output", projectDisciplineId = electrical,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Omar), requiresReview = false }, 201)).G("id") : null;
        var h = (await Post(TestData.Marc, root + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Synthetic late handoff", source, Version<Deliverable>(source),
            "A", "https://example.test/g59-survey", electrical, data.User(TestData.Alex), data.User(TestData.Omar), deliverableTarget ? null : a, targetDeliverable,
            "Set the layout", "Confirm survey scope", new(2026, 11, 7), new(2026, 11, 10), null, null), 201)).G("id");
        await Post(TestData.Alex, root + $"/handoffs/{h}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, Version<Handoff>(h), "Submit the survey", null));
        var s = new Setup(p, h, a, b, Guid.Empty, civil, electrical);
        if (!deliverableTarget) s = s with { Constraint = await Constraint(s, b) };
        await Flag(true); return s;
    }

    string State(Guid project) => f.Db(db => JsonSerializer.Serialize(new {
        tasks = db.Tasks.IgnoreQueryFilters().AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).Select(x => new { x.Id, x.Status, x.DueDate, x.DeletedAt, x.RowVersion }).ToArray(),
        handoffs = db.Handoffs.IgnoreQueryFilters().AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToArray(),
        constraints = db.WorkConstraints.AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToArray(),
        promises = db.OutputCommitments.AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToArray(),
        assessments = db.ReadinessAssessments.AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToArray(),
        checks = db.ReadinessChecks.AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.Id).ToArray(),
        states = db.TaskStates.AsNoTracking().Where(x => x.ProjectId == project).OrderBy(x => x.TaskId).ToArray(),
        logs = db.ActivityLog.Count(x => x.ProjectId == project), receipts = db.CoordinationCommands.Count(), handoffReceipts = db.HandoffCommands.Count(),
        notifications = db.Notifications.Count(), emails = db.Emails.Count(), outbox = db.Outbox.Count(), idempotency = db.Idempotency.Count(),
    }));

    [Fact]
    public async Task Direct_plus_explicit_constraint_has_two_distinct_tasks_recorded_due_and_exact_provenance()
    {
        var s = await New(); var overlap = await Constraint(s, s.Direct, "Other"); await Constraint(s, s.Linked, "Scope");
        // A structural successor with no explicit link must never be inferred into this cohort.
        var successor = (await data.NewTask(s.Project.Id, TestData.Omar, new { name = "Unlinked successor", assigneeId = data.User(TestData.Omar), dependsOn = new[] { s.Direct }, dueDate = "2026-11-09" }, "Electrical")).G("id");
        var before = State(s.Project.Id); var result = await Get(Links(s)); var summary = result["summary"]!;
        Assert.Equal("ProjectOpenReadinessLinks", summary.S("scope")); Assert.Equal(2, summary.I("taskCount"));
        Assert.Equal(1, summary.I("directTaskCount")); Assert.Equal(2, summary.I("constraintLinkedTaskCount")); Assert.Equal("2026-11-10", summary.S("firstRecordedDueDate"));
        var rows = result["tasks"]!["items"]!.AsArray(); Assert.Equal(2, rows.Count);
        var a = rows.Single(x => x!.G("id") == s.Direct)!; Assert.Equal(2, a["relationships"]!.AsArray().Count);
        Assert.Contains(a["relationships"]!.AsArray(), x => x!.S("relationship") == "DirectTarget");
        var c = a["relationships"]!.AsArray().Single(x => x!["constraintId"]?.GetValue<Guid>() == overlap)!;
        Assert.Equal(data.User(TestData.Pm), c["removalOwner"]!.G("id")); Assert.Equal(data.User(TestData.Omar), c["affectedOwner"]!.G("id"));
        var detail = await Get(Root(s) + $"/handoffs/{s.Handoff}"); Assert.Equal(summary.ToJsonString(), detail["row"]!["readinessLinks"]!.ToJsonString());
        var weekly = await Get(Root(s) + "/discipline-coordination?page=1&pageSize=4");
        Assert.Equal(2, weekly["handoffs"]![0]!["readinessLinks"]!.I("taskCount"));
        var structural = weekly["blockerGroups"]!.AsArray().Single(x => x!.G("handoffId") == s.Handoff)!;
        Assert.Contains(structural["taskIds"]!.AsArray(), x => x!.GetValue<Guid>() == successor);
        Assert.DoesNotContain(rows, x => x!.G("id") == successor);
        Assert.Equal(before, State(s.Project.Id));
    }

    [Theory]
    [InlineData(HandoffStatus.Accepted)]
    [InlineData(HandoffStatus.Incorporated)]
    public async Task Acceptance_retains_independent_constraint_until_its_own_verified_removal_and_history_is_not_a_block(string terminal)
    {
        var s = await New(); await Post(TestData.Omar, Root(s) + $"/handoffs/{s.Handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Accepted, Version<Handoff>(s.Handoff), null, "Survey independently checked"));
        if (terminal == HandoffStatus.Incorporated) await Post(TestData.Omar, Root(s) + $"/handoffs/{s.Handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), terminal, Version<Handoff>(s.Handoff), null, "Survey incorporated into the named target"));
        var result = await Get(Links(s)); Assert.True(result["summary"]!["directReceiptSatisfied"]!.GetValue<bool>());
        Assert.True(result["summary"]!["isHistoricalMismatch"]!.GetValue<bool>()); Assert.False(result["summary"]!["isOverdue"]!.GetValue<bool>());
        Assert.Equal(2, result["summary"]!.I("taskCount")); Assert.Equal(ConstraintState.Open, f.Db(db => db.WorkConstraints.Single(c => c.Id == s.Constraint).State));
        var path = Root(s) + $"/readiness/Task/{s.Linked}/constraints/{s.Constraint}/transition";
        await Post(TestData.Pm, path, new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(s.Constraint), ConstraintState.ResolutionProposed, "Input was accepted; propose removal", "https://example.test/g59-removal"));
        Assert.Equal(2, (await Get(Links(s)))["summary"]!.I("taskCount"));
        await Post(TestData.Omar, path, new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(s.Constraint), ConstraintState.VerifiedRemoved, "Verified removal separately", null));
        Assert.Equal(1, (await Get(Links(s)))["summary"]!.I("taskCount"));
    }

    [Fact]
    public async Task Cancelled_handoff_has_no_direct_readiness_input_but_does_not_erase_a_linked_constraint()
    {
        var s = await New(); await Post(TestData.Marc, Root(s) + $"/handoffs/{s.Handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Cancelled, Version<Handoff>(s.Handoff), "Cancel the handoff; constraint still needs review", null));
        var result = await Get(Links(s)); Assert.Equal(1, result["summary"]!.I("taskCount")); Assert.Equal(0, result["summary"]!.I("directTaskCount"));
        Assert.Null(result["summary"]!["directReceiptSatisfied"]); Assert.Equal(s.Linked, result["tasks"]!["items"]![0]!.G("id"));
    }

    [Theory]
    [InlineData(TaskStatuses.Complete, 1)]
    [InlineData(TaskStatuses.Cancelled, 1)]
    [InlineData(TaskStatuses.OnHold, 2)]
    public async Task Terminal_task_exclusion_and_on_hold_inclusion_follow_current_source_status(string state, int expected)
    {
        var s = await New(); var task = await Get($"/api/v1/tasks/{s.Linked}", who: TestData.Omar);
        await data.Move(TestData.Omar, task["task"]!, state, new { reason = "Synthetic lifecycle check" });
        Assert.Equal(expected, (await Get(Links(s)))["summary"]!.I("taskCount"));
    }

    [Fact]
    public async Task Deliverable_target_is_known_zero_without_task_links_and_explicit_links_add_only_their_exact_tasks()
    {
        var s = await New(true); var zero = await Get(Links(s)); Assert.Equal("Known", zero["summary"]!.S("state")); Assert.Equal(0, zero["summary"]!.I("taskCount"));
        Assert.Equal(0, zero["tasks"]!.I("totalCount")); Assert.Null(zero["summary"]!["firstRecordedDueDate"]);
        await Constraint(s, s.Direct); await Constraint(s, s.Linked); Assert.Equal(2, (await Get(Links(s)))["summary"]!.I("taskCount"));
    }

    [Fact]
    public async Task Missing_dates_and_deleted_tasks_are_not_invented_deadlines_or_unknown_empty_counts()
    {
        var s = await New();
        // Explicit imported-date/deletion scenario; no browser/evaluator is used to fabricate these source facts.
        await f.DbAsync(async db => { foreach (var t in await db.Tasks.Where(t => t.ProjectId == s.Project.Id).ToArrayAsync()) t.DueDate = null; return await db.SaveChangesAsync(); });
        var result = await Get(Links(s)); Assert.Equal(2, result["summary"]!.I("taskCount")); Assert.Equal(0, result["summary"]!.I("datedTaskCount")); Assert.Null(result["summary"]!["firstRecordedDueDate"]);
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == s.Direct)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        Assert.Equal(1, (await Get(Links(s)))["summary"]!.I("taskCount"));
    }

    async Task Large(Setup s)
    {
        // Deliberate provider population above50; ordinary commands establish the semantics in the other cases.
        var performer = data.User(TestData.Omar); var removalOwner = data.User(TestData.Pm);
        await f.DbAsync(async db => {
            for (var i = 0; i < 55; i++) {
                var task = new WorkTask { ProjectId = s.Project.Id, Seq = 100 + i, Key = $"SYN-G59-T{i:D3}", Name = "Synthetic population", ProjectDisciplineId = s.Electrical,
                    AssigneeId = performer, DueDate = new(2026, 11, 13), Status = TaskStatuses.NotStarted };
                db.Tasks.Add(task);
                db.WorkConstraints.Add(new WorkConstraint { ProjectId = s.Project.Id, Seq = 100 + i, Key = $"SYN-G59-C{i:D3}", TargetType = ItemType.Task, TargetId = task.Id,
                    Category = "Other", Description = "Synthetic population constraint", RemovalOwnerId = removalOwner, AffectedOwnerId = performer,
                    NeededBy = new(2026, 11, 10), SourceUrl = "https://example.test/g59", LinkedType = ItemType.Handoff, LinkedId = s.Handoff });
                db.OutputCommitments.Add(new OutputCommitment { ProjectId = s.Project.Id, Seq = 100 + i, Key = $"SYN-G59-P{i:D3}", TargetType = ItemType.Task, TargetId = task.Id,
                    PerformerId = performer, IntendedOutput = "Synthetic population proposal", CompletionCriteria = "Checked", WeekStart = new(2026, 11, 9), TargetDate = new(2026, 11, 13), State = CommitmentState.Proposed });
            }
            return await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Distinct_link_summary_and_independent_agenda_pages_cover_more_than_fifty_without_a_silent_cap()
    {
        var s = await New(); await Large(s); var before = State(s.Project.Id);
        var links = await Get(Links(s) + "?page=2&pageSize=50"); Assert.Equal(57, links["summary"]!.I("taskCount")); Assert.Equal(57, links["tasks"]!.I("totalCount")); Assert.Equal(7, links["tasks"]!["items"]!.AsArray().Count);
        var agenda = await Get(Agenda(s) + "&constraintsPage=2&proposalsPage=1&pageSize=10");
        Assert.Equal(56, agenda["constraints"]!.I("totalCount")); Assert.Equal(55, agenda["proposals"]!.I("totalCount")); Assert.Equal(2, agenda["constraints"]!.I("page")); Assert.Equal(1, agenda["proposals"]!.I("page"));
        Assert.Equal(10, agenda["constraints"]!["items"]!.AsArray().Count); Assert.Equal(10, agenda["proposals"]!["items"]!.AsArray().Count); Assert.Equal(before, State(s.Project.Id));
    }

    [Fact]
    public async Task Meeting_carryover_all_categories_and_recorded_weeks_keep_actual_performer_and_independent_states()
    {
        var s = await New(); await Constraint(s, s.Direct, "Scope", "2026-11-14"); await Constraint(s, s.Direct, "Other", "2026-11-16");
        var proposed = await Proposal(s, s.Direct);
        // The committed exclusion needs its own genuinely Ready target; the linked task's open constraint must keep blocking commitment.
        var readyTarget = (await data.NewTask(s.Project.Id, TestData.Omar, new { name = "Independent ready output", assigneeId = data.User(TestData.Omar),
            dueDate = "2026-11-14", requiresReview = false }, "Electrical")).G("id");
        var readiness = Root(s) + $"/readiness/Task/{readyTarget}";
        await Post(TestData.Omar, readiness, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(readyTarget),
            "Independent coordinated output", "Checked against its applicable sources"));
        foreach (var code in ReadinessCheckCode.All.Where(code => code != ReadinessCheckCode.ProductionOwner))
        {
            var detail = await Get(readiness, who: TestData.Omar);
            var check = detail["checks"]!.AsArray().Single(row => row!.S("code") == code)!;
            await Post(TestData.Pm, readiness + $"/checks/{Uri.EscapeDataString(code)}/applicability",
                new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"), check.I("rowVersion"), false,
                    "No applicable source exists for this isolated synthetic work", null));
        }
        Assert.Equal(ReadinessState.Ready, (await Get(readiness, who: TestData.Omar))["assessment"]!.S("state"));
        var committed = await Proposal(s, readyTarget, "2026-11-14");
        await Post(TestData.Omar, Root(s) + $"/weekly-commitments/{committed}/transition", new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), Version<OutputCommitment>(committed), CommitmentState.Committed, "Commit my own output", null));
        Assert.Equal(CommitmentState.Committed, f.Db(db => db.OutputCommitments.Single(row => row.Id == committed).State));
        Assert.Equal(ReadinessState.Ready, f.Db(db => db.OutputCommitments.Single(row => row.Id == committed).ReadinessAtCommit));
        // A changed coordination weekday must not hide the stored Monday proposal.
        var currentOwner = data.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).CoordinationDay = "Wednesday";
            (await db.Tasks.SingleAsync(t => t.Id == s.Direct)).AssigneeId = currentOwner; return await db.SaveChangesAsync(); });
        var before = State(s.Project.Id); var agenda = await Get(Agenda(s)); Assert.Equal(2, agenda["constraints"]!.I("totalCount")); Assert.Equal(1, agenda["proposals"]!.I("totalCount"));
        Assert.Equal("NeededByThroughEndIncludingOverdue", agenda.S("constraintScope")); Assert.Equal("2026-11-09", agenda.S("from"));
        var row = Assert.Single(agenda["proposals"]!["items"]!.AsArray())!; Assert.Equal(proposed, row.G("id")); Assert.Equal(data.User(TestData.Omar), row["performer"]!.G("id")); Assert.Equal("2026-11-09", row.S("weekStart"));
        Assert.Equal(before, State(s.Project.Id));
    }

    [Fact]
    public async Task Flag_off_old_shape_on_off_on_and_restricted_access_are_independent_of_screen_patterns()
    {
        var s = await New(); var on = await Get(Links(s));
        await Flag(false); Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Links(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Agenda(s))).StatusCode);
        var off = await Get(Root(s) + $"/handoffs/{s.Handoff}"); Assert.Null(off["row"]!["readinessLinks"]);
        await Flag(true); var before = State(s.Project.Id); Assert.Equal(on.ToJsonString(), (await Get(Links(s))).ToJsonString()); Assert.Equal(before, State(s.Project.Id));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Links(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Agenda(s))).StatusCode);
    }

    [Fact]
    public async Task Malformed_filters_and_foreign_link_damage_never_leak_names_or_claim_a_zero_cohort()
    {
        var s = await New(); var other = await data.Project(); var task = (await data.NewTask(other.Id, extra: new { name = "Foreign secret" })).G("id");
        await Get(Links(s) + "?from=2026-11-01", 400); await Get(Agenda(s) + "&unexpected=true", 400);
        await Get(Root(s) + "/coordination/readiness-agenda?from=2026-11-09&to=2027-02-09", 400);
        await Get(Agenda(s) + $"&disciplineId={data.ProjectDiscipline(other.Id, "Civil")}", 400);
        // Explicit corrupted imported target; global UUID existence must not leak its facts through a project-local reader.
        await f.DbAsync(async db => { (await db.WorkConstraints.SingleAsync(c => c.Id == s.Constraint)).TargetId = task; return await db.SaveChangesAsync(); });
        var before = State(s.Project.Id); var result = await Get(Links(s)); Assert.Equal("Unavailable", result["summary"]!.S("state"));
        Assert.Null(result["summary"]!["taskCount"]); Assert.Null(result["tasks"]); Assert.DoesNotContain("Foreign secret", result.ToJsonString());
        await Get(Agenda(s), 400); Assert.Equal(before, State(s.Project.Id));
    }
}
