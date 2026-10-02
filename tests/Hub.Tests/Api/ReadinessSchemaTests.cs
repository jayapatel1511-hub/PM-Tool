using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ReadinessSchemaTests(HubFactory f)
{
    readonly TestData data = new(f);

    [Fact]
    public async Task Migration_guards_unique_checks_and_committed_snapshot_history()
    {
        var project = await data.Project();
        var owner = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var week = new DateOnly(2026, 9, 28);
        var assessment = new ReadinessAssessment { ProjectId = project.Id, TargetType = "Task", TargetId = task.G("id"),
            OwnerId = owner, IntendedOutput = "Preliminary layout", CompletionCriteria = "Drawing reviewed" };
        var snapshot = new WeeklyPlanSnapshot { ProjectId = project.Id, WeekStart = week,
            CapturedAt = f.Clock.GetUtcNow(), CapturedBy = owner, CommittedCount = 1 };
        var commitment = new OutputCommitment { ProjectId = project.Id, SnapshotId = snapshot.Id, TargetType = "Task",
            TargetId = task.G("id"), PerformerId = owner, IntendedOutput = "Preliminary layout",
            CompletionCriteria = "Drawing reviewed", TargetDate = week.AddDays(2), WeekStart = week,
            ReadinessAtCommit = ReadinessState.Ready, State = CommitmentState.Committed };
        await f.DbAsync(async db =>
        {
            db.ReadinessAssessments.Add(assessment);
            db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = project.Id, AssessmentId = assessment.Id,
                Code = ReadinessCheckCode.Handoff, Applies = true, Satisfied = false });
            db.WeeklyPlanSnapshots.Add(snapshot);
            db.OutputCommitments.Add(commitment);
            await db.SaveChangesAsync(); return 0;
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = project.Id, AssessmentId = assessment.Id,
                Code = ReadinessCheckCode.Handoff });
            await db.SaveChangesAsync(); return 0;
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            var row = await db.OutputCommitments.SingleAsync(x => x.Id == commitment.Id);
            row.IntendedOutput = "Changed promise";
            await db.SaveChangesAsync(); return 0;
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            var row = await db.WeeklyPlanSnapshots.SingleAsync(x => x.Id == snapshot.Id);
            row.CommittedCount = 0;
            await db.SaveChangesAsync(); return 0;
        }));
        await f.DbAsync(async db =>
        {
            var row = await db.OutputCommitments.SingleAsync(x => x.Id == commitment.Id);
            row.State = CommitmentState.Met;
            row.CompletionEvidenceUrl = "https://example.test/reviewed-layout";
            await db.SaveChangesAsync(); return 0;
        });
        Assert.Equal(CommitmentState.Met, f.Db(db => db.OutputCommitments.Single(x => x.Id == commitment.Id).State));
    }
}
