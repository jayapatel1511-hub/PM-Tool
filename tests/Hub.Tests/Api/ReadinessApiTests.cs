using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ReadinessApiTests(HubFactory f)
{
    readonly TestData data = new(f);

    [Fact]
    public async Task Detail_recomputes_linked_predecessor_instead_of_cached_ready_state()
    {
        var project = await data.Project();
        var predecessor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var successor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        await f.DbAsync(async db =>
        {
            db.Dependencies.Add(new TaskDependency { ProjectId = project.Id, PredecessorTaskId = predecessor.G("id"), SuccessorTaskId = successor.G("id"), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); return 0;
        });
        var id = successor.G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == id).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{id}";
        var assessment = await (await f.As(TestData.Alex).Post(path,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Output", "Criteria"))).Json();
        await f.DbAsync(async db =>
        {
            var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checks) { check.Applies = true; check.Satisfied = true; }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            await db.SaveChangesAsync(); return 0;
        });
        var detail = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, detail["assessment"]!.S("state"));
        var predecessorCheck = detail["checks"]!.AsArray().Single(c => c!["code"]!.GetValue<string>() == ReadinessCheckCode.Predecessor)!;
        Assert.True(predecessorCheck["applies"]!.GetValue<bool>());
        Assert.False(predecessorCheck["satisfied"]!.GetValue<bool>());
        var monday = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var commitments = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var proposal = await (await f.As(TestData.Pm).Post($"{commitments}/Task/{id}",
            new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), version, monday, monday.AddDays(2),
                "Blocked successor output", "Predecessor complete"))).Json();
        await (await f.As(TestData.Alex).Post($"{commitments}/{proposal.G("id")}/transition",
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), proposal.I("rowVersion"),
                CommitmentState.Committed, "Cannot sign a blocked output", null))).Json(400);
        await f.DbAsync(async db =>
        {
            (await db.Dependencies.SingleAsync(d => d.SuccessorTaskId == id)).DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); return 0;
        });
        var withoutDeletedLink = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, withoutDeletedLink["assessment"]!.S("state"));
        var constraintId = Guid.NewGuid();
        await f.DbAsync(async db =>
        {
            db.WorkConstraints.Add(new WorkConstraint { Id = constraintId, ProjectId = project.Id,
                TargetType = "Task", TargetId = id, Category = "Handoff", Description = "Await source confirmation",
                RemovalOwnerId = data.User(TestData.Pm), AffectedOwnerId = data.User(TestData.Alex),
                NeededBy = monday.AddDays(2), SourceUrl = "https://example.test/source",
                State = ConstraintState.ResolutionProposed });
            await db.SaveChangesAsync(); return 0;
        });
        var proposedRemoval = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, proposedRemoval["assessment"]!.S("state"));
        await f.DbAsync(async db =>
        {
            (await db.WorkConstraints.SingleAsync(c => c.Id == constraintId)).State = ConstraintState.VerifiedRemoved;
            await db.SaveChangesAsync(); return 0;
        });
        var verifiedRemoval = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, verifiedRemoval["assessment"]!.S("state"));
    }

    [Fact]
    public async Task Chair_proposal_requires_performer_confirmation_and_snapshot_retains_withdrawal()
    {
        var project = await data.Project();
        var owner = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var taskId = task.G("id");
        var targetVersion = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var monday = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var root = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var proposal = new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), targetVersion, monday,
            monday.AddDays(3), "Civil drawing review package", "Drawing reviewed with source register");
        var row = await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json();
        Assert.Equal(row.G("id"), (await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json()).G("id"));
        Assert.Equal(CommitmentState.Proposed, f.Db(db => db.OutputCommitments.Single(c => c.Id == row.G("id")).State));
        var move = $"{root}/{row.G("id")}/transition";
        var commit = new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), row.I("rowVersion"),
            CommitmentState.Committed, "Performer accepts the defined output", null);
        await (await f.As(TestData.Pm).Post(move, commit)).Json(403);
        await (await f.As(TestData.Alex).Post(move, commit with { RequestId = Guid.NewGuid() })).Json(400);
        var assessment = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/readiness/Task/{taskId}",
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Civil drawing review package",
                "Drawing reviewed with source register"))).Json();
        await f.DbAsync(async db =>
        {
            var checkRows = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checkRows) { check.Applies = check.Code == ReadinessCheckCode.ProductionOwner; check.Satisfied = check.Applies == true ? true : null; }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            await db.SaveChangesAsync();
            return 0;
        });
        await (await f.As(TestData.Alex).Post(move, commit with { RequestId = Guid.NewGuid() })).Json();
        var snap = await (await f.As(TestData.Pm).Post(root + "/snapshot",
            new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), monday, "Coordination week closed"))).Json();
        Assert.Equal(1, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.Id == snap.G("id")).CommittedCount));
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == taskId)).AssigneeId = data.User(TestData.Omar);
            await db.SaveChangesAsync(); return 0; });
        var committed = f.Db(db => db.OutputCommitments.Single(c => c.Id == row.G("id")));
        await (await f.As(TestData.Pm).Post(move,
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committed.RowVersion,
                CommitmentState.Met, "Chair cannot attest another performer met criteria", "https://example.test/review"))).Json(403);
        await (await f.As(TestData.Pm).Post(move,
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committed.RowVersion,
                CommitmentState.Withdrawn, "Performer reassigned after snapshot", null))).Json();
        var list = await (await f.As(TestData.Pm).GetAsync(root + $"?weekStart={monday:yyyy-MM-dd}")).Json();
        var outcome = list["snapshots"]!.AsArray().Single()!;
        Assert.Equal(1, outcome.I("committedCount"));
        Assert.Equal(1, outcome.I("withdrawn"));
        Assert.Equal(0, outcome.I("met"));
        Assert.Single(f.Db(db => db.OutputCommitmentEvents.Where(e => e.CommitmentId == row.G("id") &&
            e.ToState == CommitmentState.Withdrawn).ToList()));
    }

    [Fact]
    public async Task Named_owner_creates_unassessed_output_with_scoped_and_retry_safe_command()
    {
        var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0; });
        var owner = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var taskId = task.G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}";
        var body = new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version,
            "Preliminary site servicing layout", "Civil reviewer checks drawing and references");
        await (await f.As(TestData.Rita).Post(path, body)).Json(404);
        await (await f.As(TestData.Pm).Post(path, body with { RequestId = Guid.NewGuid() })).Json(403);
        await (await f.As(TestData.Alex).Post(path, body with { RequestId = Guid.NewGuid(), TargetRowVersion = version - 1 })).Json(409);
        var created = await (await f.As(TestData.Alex).Post(path, body)).Json();
        Assert.Equal(created.G("id"), (await (await f.As(TestData.Alex).Post(path, body)).Json()).G("id"));
        var detail = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NeedsAssessment, detail["assessment"]!.S("state"));
        Assert.Equal(ReadinessCheckCode.All.Length, detail["checks"]!.AsArray().Count);
        Assert.All(detail["checks"]!.AsArray().Where(c => c!.S("code") != ReadinessCheckCode.ProductionOwner),
            c => { Assert.Null(c!["applies"]); Assert.Null(c["satisfied"]); });
        var handoff = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        var productionOwner = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ProductionOwner)!;
        Assert.True(productionOwner["applies"]!.GetValue<bool>());
        Assert.True(productionOwner["satisfied"]!.GetValue<bool>());
        await (await f.As(TestData.Pm).Post(path + "/checks/Production%20Owner/applicability",
            new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"),
                productionOwner.I("rowVersion"), false, "No owner claimed", null))).Json(400);
        var applicabilityPath = path + "/checks/Handoff/applicability";
        var applies = new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"),
            handoff.I("rowVersion"), true, "Accepted source handoff required for this layout", null);
        await (await f.As(TestData.Alex).Post(applicabilityPath, applies)).Json(403);
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies with { RequestId = Guid.NewGuid(), CheckRowVersion = -1 })).Json(409);
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies)).Json();
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies)).Json();
        var updated = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        var updatedHandoff = updated["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        Assert.True(updatedHandoff["applies"]!.GetValue<bool>());
        Assert.Null(updatedHandoff["satisfied"]);
        Assert.Equal(ReadinessState.NeedsAssessment, updated["assessment"]!.S("state"));
        var constraintPath = path + "/constraints";
        var createConstraint = new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), version, "Handoff",
            "Obtain accepted drainage input", data.User(TestData.Pm), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            "https://review.example.test/handoff/1");
        var constraint = await (await f.As(TestData.Alex).Post(constraintPath, createConstraint)).Json();
        Assert.Equal(constraint.G("id"), (await (await f.As(TestData.Alex).Post(constraintPath, createConstraint)).Json()).G("id"));
        var row = (await (await f.As(TestData.Alex).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.Open, row.S("state"));
        var movePath = constraintPath + $"/{constraint.G("id")}/transition";
        var propose = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), row.I("rowVersion"),
            ConstraintState.ResolutionProposed, "Source handoff delivered", "https://review.example.test/handoff/accepted");
        await (await f.As(TestData.Alex).Post(movePath, propose)).Json(403);
        await (await f.As(TestData.Pm).Post(movePath, propose)).Json();
        var proposed = (await (await f.As(TestData.Pm).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.ResolutionProposed, proposed.S("state"));
        var verify = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), proposed.I("rowVersion"),
            ConstraintState.VerifiedRemoved, "Affected work owner checked accepted handoff", null);
        await (await f.As(TestData.Pm).Post(movePath, verify)).Json(403);
        await (await f.As(TestData.Alex).Post(movePath, verify with { RequestId = Guid.NewGuid(), RowVersion = -1 })).Json(409);
        await (await f.As(TestData.Alex).Post(movePath, verify)).Json();
        var removed = (await (await f.As(TestData.Alex).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.VerifiedRemoved, removed.S("state"));
        Assert.Equal(data.User(TestData.Alex), removed.G("verifiedBy"));
        await (await f.As(TestData.Rita).GetAsync(path)).Json(404);
        await (await f.As(TestData.Alex).Post(path, body with { RequestId = Guid.NewGuid() })).Json(409);
    }
}
