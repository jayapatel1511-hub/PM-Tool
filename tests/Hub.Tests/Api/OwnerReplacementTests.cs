using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class OwnerReplacementTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int expected = 200) => await (await f.As(who).Post(path, body)).Json(expected);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task New_owner_reconfirms_and_recovers_only_open_owner_actions()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var alex = data.User(TestData.Alex); var omar = data.User(TestData.Omar); var pm = data.User(TestData.Pm);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = alex });
        var taskId = task.G("id");
        var readinessPath = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}";
        var assessment = await Post(TestData.Alex, readinessPath,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(taskId), "Initial output", "Initial criteria"));
        await Post(TestData.Alex, readinessPath, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(taskId), "Duplicate", "Duplicate"), 409);

        var entryId = Guid.CreateVersion7(); var oldVersionId = Guid.CreateVersion7(); var newVersionId = Guid.CreateVersion7();
        var impactId = Guid.CreateVersion7(); var settledImpactId = Guid.CreateVersion7(); var useId = Guid.CreateVersion7(); var settledUseId = Guid.CreateVersion7(); var settledTargetId = Guid.CreateVersion7(); var constraintId = Guid.CreateVersion7(); var promiseId = Guid.CreateVersion7();
        var snapshotId = Guid.CreateVersion7(); var committedPromiseId = Guid.CreateVersion7();
        var assumptionEntryId = Guid.CreateVersion7(); var assumptionVersionId = Guid.CreateVersion7(); var assumptionUseId = Guid.CreateVersion7();
        await f.DbAsync(async db =>
        {
            db.DesignBasisEntries.Add(new DesignBasisEntry { Id = entryId, ProjectId = project.Id, Key = "BAS-001", Seq = 1,
                Kind = BasisKind.Criterion, Title = "Owner replacement basis", OwnerId = alex, ProjectDisciplineId = civil });
            db.DesignBasisVersions.AddRange(
                new DesignBasisVersion { Id = oldVersionId, ProjectId = project.Id, EntryId = entryId, Number = 1, Status = BasisStatus.Superseded,
                    Scope = "Scope", Statement = "Old confirmed basis", SourceSystem = "Manual", DeclaredRevision = "A", SourceUrl = "https://example.test/a" },
                new DesignBasisVersion { Id = newVersionId, ProjectId = project.Id, EntryId = entryId, Number = 2, SupersedesVersionId = oldVersionId,
                    Status = BasisStatus.Confirmed, Scope = "Scope", Statement = "Current confirmed basis", SourceSystem = "Manual", DeclaredRevision = "B", SourceUrl = "https://example.test/b",
                    ConfirmedBy = pm, ConfirmedAt = f.Clock.Now, ConfirmationRationale = "Confirmed current basis" });
            db.DesignBasisEntries.Add(new DesignBasisEntry { Id = assumptionEntryId, ProjectId = project.Id, Key = "BAS-002", Seq = 2,
                Kind = BasisKind.Assumption, Title = "Owner replacement assumption", OwnerId = alex, ProjectDisciplineId = civil });
            db.DesignBasisVersions.Add(new DesignBasisVersion { Id = assumptionVersionId, ProjectId = project.Id, EntryId = assumptionEntryId,
                Number = 1, Status = BasisStatus.Proposed, Scope = "Assumption scope", Statement = "Assumption for owner recovery" });
            db.BasisUses.Add(new BasisUse { Id = settledUseId, ProjectId = project.Id, VersionId = oldVersionId, TargetType = "Deliverable", TargetId = settledTargetId,
                OwnerId = alex, IntendedUse = "Historical settled basis use", CreatedAt = f.Clock.Now.AddDays(-1) });
            db.BasisUses.Add(new BasisUse { Id = useId, ProjectId = project.Id, VersionId = oldVersionId, TargetType = "Task", TargetId = taskId,
                OwnerId = alex, IntendedUse = "Owner replacement recovery", CreatedAt = f.Clock.Now });
            db.BasisUses.Add(new BasisUse { Id = assumptionUseId, ProjectId = project.Id, VersionId = assumptionVersionId, TargetType = "Task", TargetId = taskId,
                OwnerId = alex, IntendedUse = "Owner replacement exception", CreatedAt = f.Clock.Now });
            db.BasisAssumptionDispositions.Add(new BasisAssumptionDisposition { ProjectId = project.Id, VersionId = assumptionVersionId,
                Scope = "Assumption scope", OwnerId = alex, ApprovedBy = pm, ExpiresOn = new DateOnly(2026, 10, 20), Reason = "Original owner approval" });
            db.ReadinessExceptions.Add(new ReadinessException { ProjectId = project.Id, AssessmentId = assessment.G("id"), BasisVersionId = assumptionVersionId,
                ApprovedBy = pm, VerifierId = pm, LimitedWork = "Original owner exception scope", Risk = "Original owner exception risk",
                ExpiresOn = new DateOnly(2026, 10, 15), CreatedAt = f.Clock.Now });
            db.BasisImpactAssessments.Add(new BasisImpactAssessment { Id = impactId, ProjectId = project.Id, BasisUseId = useId,
                OldVersionId = oldVersionId, NewVersionId = newVersionId, OwnerId = alex, Status = AssessmentStatus.Pending });
            db.BasisImpactAssessments.Add(new BasisImpactAssessment { Id = settledImpactId, ProjectId = project.Id, BasisUseId = settledUseId,
                OldVersionId = oldVersionId, NewVersionId = newVersionId, OwnerId = alex, Status = AssessmentStatus.Resolved,
                DecidedBy = alex, DecidedAt = f.Clock.Now, Rationale = "Historical decision" });
            db.WorkConstraints.Add(new WorkConstraint { Id = constraintId, ProjectId = project.Id, Key = "CON-001", Seq = 1,
                TargetType = "Task", TargetId = taskId, Category = "Basis", Description = "Open basis constraint", RemovalOwnerId = pm,
                AffectedOwnerId = alex, NeededBy = new DateOnly(2026, 10, 10), SourceUrl = "https://example.test/constraint" });
            db.OutputCommitments.Add(new OutputCommitment { Id = promiseId, ProjectId = project.Id, Key = "COM-001", Seq = 1,
                TargetType = "Task", TargetId = taskId, PerformerId = alex, WeekStart = new DateOnly(2026, 10, 5), TargetDate = new DateOnly(2026, 10, 7),
                IntendedOutput = "Initial signed promise", CompletionCriteria = "Initial criteria", State = CommitmentState.Proposed, CreatedBy = alex });
            db.WeeklyPlanSnapshots.Add(new WeeklyPlanSnapshot { Id = snapshotId, ProjectId = project.Id, WeekStart = new DateOnly(2026, 9, 28),
                CapturedAt = f.Clock.Now, CapturedBy = pm, CommittedCount = 1 });
            db.OutputCommitments.Add(new OutputCommitment { Id = committedPromiseId, ProjectId = project.Id, Key = "COM-002", Seq = 2,
                TargetType = "Task", TargetId = taskId, PerformerId = alex, WeekStart = new DateOnly(2026, 9, 28), TargetDate = new DateOnly(2026, 9, 30),
                IntendedOutput = "Signed frozen promise", CompletionCriteria = "Frozen criteria", State = CommitmentState.Committed, SnapshotId = snapshotId, CreatedBy = alex });
            await db.SaveChangesAsync();
            db.DesignBasisEntries.Single(e => e.Id == entryId).CurrentVersionId = newVersionId;
            await db.SaveChangesAsync();
            return 0;
        });

        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{taskId}", new { assigneeId = omar, reason = "Owner reassigned for recovery" }, Version<WorkTask>(taskId))).Json();
        var recovered = await Post(TestData.Omar, readinessPath,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(taskId), "Replacement output", "Replacement criteria", "New owner reconfirmed the output after reassignment"));
        Assert.Equal(assessment.G("id"), recovered.G("id"));

        var state = f.Db(db => new
        {
            AssessmentOwner = db.ReadinessAssessments.Single(a => a.Id == assessment.G("id")).OwnerId,
            UseOwner = db.BasisUses.Single(u => u.Id == useId).OwnerId,
            ImpactOwner = db.BasisImpactAssessments.Single(i => i.Id == impactId).OwnerId,
            SettledImpactOwner = db.BasisImpactAssessments.Single(i => i.Id == settledImpactId).OwnerId,
            ConstraintOwner = db.WorkConstraints.Single(c => c.Id == constraintId).AffectedOwnerId,
            PromisePerformer = db.OutputCommitments.Single(c => c.Id == promiseId).PerformerId,
            CommittedPromisePerformer = db.OutputCommitments.Single(c => c.Id == committedPromiseId).PerformerId,
            CheckValues = db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).Select(c => new { c.Applies, c.Satisfied, c.RecordedBy }).ToList()
        });
        Assert.Equal(omar, state.AssessmentOwner); Assert.Equal(omar, state.UseOwner); Assert.Equal(omar, state.ImpactOwner);
        Assert.Equal(alex, state.SettledImpactOwner);
        Assert.Equal(omar, state.ConstraintOwner); Assert.Equal(omar, state.PromisePerformer);
        Assert.Equal(alex, state.CommittedPromisePerformer);
        Assert.All(state.CheckValues, c => { Assert.Null(c.Applies); Assert.Null(c.Satisfied); Assert.Null(c.RecordedBy); });

        var basisPath = $"/api/v1/projects/{project.Id}/design-basis/{entryId}";
        await Post(TestData.Pm, basisPath + "/assign", new DesignBasisEndpoints.AssignBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
            omar, omar, "Reject same-person basis approval"), 400);
        await Post(TestData.Pm, basisPath + "/assign", new DesignBasisEndpoints.AssignBody(Guid.NewGuid(), Version<DesignBasisEntry>(entryId),
            omar, pm, "Replace confirmed basis owner and independent approver"));

        var impact = new DesignBasisEndpoints.ImpactBody(Guid.NewGuid(), Version<BasisImpactAssessment>(impactId), Version<BasisUse>(useId),
            Version<DesignBasisVersion>(newVersionId), Version<WorkTask>(taskId), "Adopt", "New owner adopted the confirmed basis after assignment", "https://example.test/adopt");
        await Post(TestData.Omar, $"{basisPath}/impacts/{impactId}/decide", impact);
        Assert.Equal(AssessmentStatus.Resolved, f.Db(db => db.BasisImpactAssessments.Single(i => i.Id == impactId).Status));

        var constraintPath = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}/constraints/{constraintId}/transition";
        await Post(TestData.Pm, constraintPath, new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(constraintId),
            ConstraintState.ResolutionProposed, "Constraint work completed", "https://example.test/constraint-resolved"));
        await Post(TestData.Omar, constraintPath, new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), Version<WorkConstraint>(constraintId),
            ConstraintState.VerifiedRemoved, "New owner verified the constraint removal", null));

        var reassessed = await (await f.As(TestData.Omar).GetAsync(readinessPath)).Json();
        Assert.Equal(ReadinessState.NeedsAssessment, reassessed["assessment"]!.S("state"));
        Assert.NotEqual(ReadinessState.ProceedUnderAssumption, reassessed["assessment"]!.S("state"));
        var preservedException = f.Db(db => new
        {
            Exception = db.ReadinessExceptions.Single(e => e.AssessmentId == assessment.G("id")),
            Disposition = db.BasisAssumptionDispositions.Single(d => d.VersionId == assumptionVersionId)
        });
        Assert.Equal(assumptionVersionId, preservedException.Exception.BasisVersionId);
        Assert.Equal(alex, preservedException.Disposition.OwnerId);

        var promisePath = $"/api/v1/projects/{project.Id}/weekly-commitments/{promiseId}/transition";
        await Post(TestData.Omar, promisePath, new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), Version<OutputCommitment>(promiseId),
            CommitmentState.Withdrawn, "Withdraw prior proposed promise after reassignment", null));
        var promise = f.Db(db => db.OutputCommitments.Single(c => c.Id == promiseId));
        Assert.Equal(CommitmentState.Withdrawn, promise.State); Assert.Equal(alex, promise.CreatedBy); Assert.Equal(omar, promise.PerformerId);
    }
}
