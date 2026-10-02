using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 032 notices: queued in the command transaction, never to the actor, never after a refused, stale or
/// replayed command, and only to recipients who still hold project access (FR-MDC-02, FR-MDC-03, FR-MDC-06).
[Collection("api")]
public sealed class ReadinessNotificationTests(HubFactory f)
{
    readonly TestData data = new(f);

    int Notices(Guid itemId, string eventType, Guid userId) => f.Db(db => db.Notifications
        .Where(n => n.ItemId == itemId && n.EventType == eventType && n.UserId == userId).Sum(n => n.Count));

    Task<int> AddMember(Guid projectId, Guid userId) => f.DbAsync(async db =>
    {
        db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Roles = [ProjectRole.TeamMember],
            PrimaryDisciplineId = data.ProjectDiscipline(projectId, "Civil") });
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Constraint_notices_reach_removal_and_affected_owners_only_with_project_access()
    {
        var project = await data.Project();
        var owner = data.User(TestData.Alex);
        var remover = data.User(TestData.Jill);
        await AddMember(project.Id, remover);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var taskId = task.G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}/constraints";
        var create = new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), version, "Handoff", "Obtain accepted drainage input",
            remover, DateOnly.FromDateTime(f.Clock.Now.UtcDateTime.AddDays(7)), "https://review.example.test/handoff/1");

        var id = (await (await f.As(TestData.Alex).Post(path, create)).Json()).G("id");
        Assert.Equal(id, (await (await f.As(TestData.Alex).Post(path, create)).Json()).G("id"));
        Assert.Equal(1, Notices(id, NotificationEvents.ConstraintAction, remover));
        Assert.Equal(0, Notices(id, NotificationEvents.ConstraintAction, owner));

        var move = $"{path}/{id}/transition";
        var rowVersion = f.Db(db => db.WorkConstraints.Single(c => c.Id == id).RowVersion);
        var propose = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), rowVersion, ConstraintState.ResolutionProposed,
            "Source handoff delivered", "https://review.example.test/handoff/accepted");
        await (await f.As(TestData.Alex).Post(move, propose)).Json(403);
        Assert.Equal(0, Notices(id, NotificationEvents.ConstraintAction, owner));
        await (await f.As(TestData.Jill).Post(move, propose)).Json();
        Assert.Equal(1, Notices(id, NotificationEvents.ConstraintAction, owner));
        Assert.Equal(1, Notices(id, NotificationEvents.ConstraintAction, remover));

        var proposedVersion = f.Db(db => db.WorkConstraints.Single(c => c.Id == id).RowVersion);
        var verify = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), proposedVersion, ConstraintState.VerifiedRemoved,
            "Affected work owner checked accepted handoff", null);
        await (await f.As(TestData.Alex).Post(move, verify with { RequestId = Guid.NewGuid(), RowVersion = -1 })).Json(409);
        Assert.Equal(0, Notices(id, NotificationEvents.ConstraintOutcome, remover));
        await (await f.As(TestData.Alex).Post(move, verify)).Json();
        await (await f.As(TestData.Alex).Post(move, verify)).Json();
        Assert.Equal(1, Notices(id, NotificationEvents.ConstraintOutcome, remover));
        Assert.Equal(0, Notices(id, NotificationEvents.ConstraintOutcome, owner));

        var second = (await (await f.As(TestData.Alex).Post(path, create with { RequestId = Guid.NewGuid() })).Json()).G("id");
        Assert.Equal(1, Notices(second, NotificationEvents.ConstraintAction, remover));
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == remover)).RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        var secondVersion = f.Db(db => db.WorkConstraints.Single(c => c.Id == second).RowVersion);
        await (await f.As(TestData.Pm).Post($"{path}/{second}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(),
            secondVersion, ConstraintState.Cancelled, "Drainage input no longer required", null))).Json();
        Assert.Equal(1, Notices(second, NotificationEvents.ConstraintOutcome, owner));
        Assert.Equal(0, Notices(second, NotificationEvents.ConstraintOutcome, remover));
        Assert.False(f.Db(db => db.Emails.Any(e => e.UserId == remover && e.DedupKey!.Contains(second.ToString()))));
    }

    [Fact]
    public async Task Commitment_notices_follow_proposal_signature_and_outcome_without_refused_commands()
    {
        var project = await data.Project();
        var performer = data.User(TestData.Alex);
        var chair = data.User(TestData.Pm);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = performer });
        var taskId = task.G("id");
        var targetVersion = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var monday = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var root = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var proposal = new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), targetVersion, monday,
            monday.AddDays(3), "Civil drawing review package", "Drawing reviewed with source register");

        var row = await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json();
        var id = row.G("id");
        await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json();
        Assert.Equal(1, Notices(id, NotificationEvents.CommitmentProposed, performer));
        Assert.Equal(0, Notices(id, NotificationEvents.CommitmentProposed, chair));

        var move = $"{root}/{id}/transition";
        var commit = new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), row.I("rowVersion"),
            CommitmentState.Committed, "Performer accepts the defined output", null);
        await (await f.As(TestData.Alex).Post(move, commit)).Json(400); // unassessed promise cannot be signed
        Assert.Equal(0, Notices(id, NotificationEvents.CommitmentChanged, chair));

        var assessment = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/readiness/Task/{taskId}",
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Civil drawing review package",
                "Drawing reviewed with source register"))).Json();
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            { check.Applies = check.Code == ReadinessCheckCode.ProductionOwner; check.Satisfied = check.Applies == true ? true : null; }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            return await db.SaveChangesAsync();
        });
        var signed = commit with { RequestId = Guid.NewGuid() };
        await (await f.As(TestData.Alex).Post(move, signed)).Json();
        await (await f.As(TestData.Alex).Post(move, signed)).Json();
        Assert.Equal(1, Notices(id, NotificationEvents.CommitmentChanged, chair));
        Assert.Equal(0, Notices(id, NotificationEvents.CommitmentChanged, performer));

        await (await f.As(TestData.Pm).Post(root + "/snapshot",
            new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), monday, "Coordination week closed"))).Json();
        var committedVersion = f.Db(db => db.OutputCommitments.Single(c => c.Id == id).RowVersion);
        await (await f.As(TestData.Pm).Post(move, new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committedVersion,
            CommitmentState.Met, "Chair cannot attest the performer met criteria", "https://example.test/review"))).Json(403);
        Assert.Equal(0, Notices(id, NotificationEvents.CommitmentChanged, performer));
        await (await f.As(TestData.Pm).Post(move, new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committedVersion,
            CommitmentState.NotMet, "Survey input arrived late", null))).Json();
        Assert.Equal(1, Notices(id, NotificationEvents.CommitmentChanged, performer));
        Assert.Equal(1, Notices(id, NotificationEvents.CommitmentChanged, chair));
    }
}
