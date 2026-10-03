using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class CoordinationDigestTests(HubFactory f)
{
    [Fact]
    public async Task Pending_coordination_actions_have_capped_opt_out_digest_sections_with_current_access()
    {
        var data = new TestData(f);
        var email = $"coord-digest.{Guid.NewGuid():N}@hub.test";
        (await f.As(email).GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var me = data.User(email);
        var pm = data.User(TestData.Pm);
        var project = await data.Project();
        var task = await data.NewTask(project.Id, extra: new { assigneeId = me });
        var taskId = task.G("id");
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var issueId = Guid.CreateVersion7();
        await f.DbAsync(async db =>
        {
            var milestone = new Milestone { ProjectId = project.Id, Seq = 901, Key = "M0901", Name = "Digest gate" };
            db.Milestones.Add(milestone);
            db.SubmissionPackages.Add(new SubmissionPackage { ProjectId = project.Id, Seq = 901, Key = "S0901", Title = "Check-only submission", CoordinatorId = me,
                MilestoneId = milestone.Id, TargetDate = today, Status = SubmissionStatus.Checking });
            db.Allocations.Add(new ResourceAllocation { ProjectId = project.Id, PersonId = me, CreatedBy = me, Purpose = AllocationPurpose.Production,
                FromDate = today, ThroughDate = today, PlannedHours = 2 });
            var basis = new DesignBasisEntry { ProjectId = project.Id, Seq = 901, Key = "B0901", Title = "Digest basis", OwnerId = pm,
                ProjectDisciplineId = data.ProjectDiscipline(project.Id, "Civil") };
            var version = new DesignBasisVersion { ProjectId = project.Id, EntryId = basis.Id, Number = 1, Scope = "Civil", Statement = "Test assumption" };
            var use = new BasisUse { ProjectId = project.Id, VersionId = version.Id, TargetType = "Task", TargetId = taskId, OwnerId = me };
            db.AddRange(basis, version, use);
            db.BasisImpactAssessments.Add(new BasisImpactAssessment { ProjectId = project.Id, BasisUseId = use.Id, OldVersionId = version.Id, OwnerId = me });
            db.WorkConstraints.Add(new WorkConstraint { ProjectId = project.Id, Seq = 901, Key = "C0901", TargetType = "Task", TargetId = taskId,
                Category = "Handoff", Description = "Provide input", RemovalOwnerId = me, AffectedOwnerId = pm, NeededBy = today, SourceUrl = "https://example.test/input" });
            var assessment = new ReadinessAssessment { ProjectId = project.Id, TargetType = "Task", TargetId = taskId, OwnerId = me };
            db.ReadinessAssessments.Add(assessment);
            db.BasisAssumptionDispositions.Add(new BasisAssumptionDisposition { ProjectId = project.Id, VersionId = version.Id,
                Scope = version.Scope, OwnerId = me, ApprovedBy = pm, ExpiresOn = today.AddDays(3), Reason = "Current digest fixture" });
            db.ReadinessExceptions.Add(new ReadinessException { ProjectId = project.Id, AssessmentId = assessment.Id, BasisVersionId = version.Id,
                ApprovedBy = pm, VerifierId = me, ExpiresOn = today.AddDays(1), LimitedWork = "Review limited work", Risk = "Provisional input" });
            for (var i = 1; i <= 12; i++)
                db.OutputCommitments.Add(new OutputCommitment { ProjectId = project.Id, Seq = i, Key = $"W{i:0000}", TargetType = "Task", TargetId = taskId,
                    PerformerId = me, IntendedOutput = "Confirm promise", CompletionCriteria = "Checked", TargetDate = today, WeekStart = today });
            db.Issues.Add(new Issue { Id = issueId, ProjectId = project.Id, Seq = 901, Key = "I0901", Title = "Verify correction", OwnerId = pm,
                RaisedById = pm, IssueType = IssueType.Coordination, DateRaised = today });
            db.IssueVerifications.Add(new IssueVerification { ProjectId = project.Id, IssueId = issueId, IssueRowVersion = 1, VerifierId = me });
            return await db.SaveChangesAsync();
        });
        using var scope = f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        Task<Digest.Result?> Build(IReadOnlySet<string>? off = null) => Digest.Build(db, me, "Test", today,
            f.Clock.Now.AddDays(-1), f.Clock.Now, new OrgSettings(), "https://example.test", off);
        var sections = new[] { "submissions", "allocations", "basisImpacts", "constraints", "commitments", "issueVerifications" };
        var result = Assert.IsType<Digest.Result>(await Build());
        foreach (var code in sections) Assert.Contains(result.Sections, s => s.Code == code && s.Total > 0);
        var promises = Assert.Single(result.Sections, s => s.Code == "commitments");
        Assert.Equal(12, promises.Total); Assert.Equal(10, promises.Rows.Count);
        Assert.Contains("https://example.test/projects/", result.Body);
        Assert.Null(await Build(Digest.SectionCodes.ToHashSet()));
        await f.DbAsync(async other =>
        {
            other.IssueVerifications.Add(new IssueVerification { ProjectId = project.Id, IssueId = issueId, IssueRowVersion = 2, VerifierId = me,
                Status = IssueVerificationStatus.Verified, EvidenceUrl = "https://example.test/checked" });
            return await other.SaveChangesAsync();
        });
        Assert.DoesNotContain((await Build())!.Sections, s => s.Code == "issueVerifications");
        await f.DbAsync(async other =>
        {
            var previous = await other.ReadinessExceptions.SingleAsync(e => e.ProjectId == project.Id);
            other.ReadinessExceptions.Add(new ReadinessException { ProjectId = project.Id, AssessmentId = previous.AssessmentId,
                BasisVersionId = previous.BasisVersionId, ApprovedBy = pm, VerifierId = pm,
                ExpiresOn = today.AddDays(2), LimitedWork = "Replacement approval", Risk = "Updated scope" });
            return await other.SaveChangesAsync();
        });
        Assert.DoesNotContain((await Build())!.Sections, s => s.Code == "readinessExceptions");
        await f.DbAsync(async other =>
        {
            (await other.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            (await other.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == me)).RemovedAt = f.Clock.Now;
            return await other.SaveChangesAsync();
        });
        Assert.Null(await Build());
    }

    [Fact]
    public async Task Owner_recovery_hides_stale_exception_assignment_but_keeps_current_exception_assignment()
    {
        var data = new TestData(f);
        var project = await data.Project();
        var alex = data.User(TestData.Alex);
        var omar = data.User(TestData.Omar);
        var marc = data.User(TestData.Marc);
        var jill = data.User(TestData.Jill);
        var pm = data.User(TestData.Pm);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = alex });
        var taskId = task.G("id");
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var assessmentId = Guid.CreateVersion7();
        var entryId = Guid.CreateVersion7();
        var versionId = Guid.CreateVersion7();
        var useId = Guid.CreateVersion7();
        await f.DbAsync(async db =>
        {
            foreach (var userId in new[] { marc, jill })
            {
                var member = await db.ProjectMembers.SingleOrDefaultAsync(m => m.ProjectId == project.Id && m.UserId == userId);
                if (member is null) db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = userId, Roles = [ProjectRole.TeamMember] });
                else { member.Roles = [ProjectRole.TeamMember]; member.RemovedAt = null; }
            }
            db.DesignBasisEntries.Add(new DesignBasisEntry { Id = entryId, ProjectId = project.Id, Seq = 991, Key = "B0991",
                Kind = BasisKind.Assumption, Title = "Digest recovery assumption", OwnerId = alex,
                ProjectDisciplineId = data.ProjectDiscipline(project.Id, "Civil") });
            db.DesignBasisVersions.Add(new DesignBasisVersion { Id = versionId, ProjectId = project.Id, EntryId = entryId,
                Number = 1, Scope = "Digest scope", Statement = "Recovery assumption" });
            db.BasisUses.Add(new BasisUse { Id = useId, ProjectId = project.Id, VersionId = versionId, TargetType = "Task", TargetId = taskId,
                OwnerId = alex, IntendedUse = "Digest recovery" });
            db.ReadinessAssessments.Add(new ReadinessAssessment { Id = assessmentId, ProjectId = project.Id, TargetType = "Task",
                TargetId = taskId, OwnerId = alex });
            db.BasisAssumptionDispositions.Add(new BasisAssumptionDisposition { ProjectId = project.Id, VersionId = versionId,
                Scope = "Digest scope", OwnerId = alex, ApprovedBy = pm, ExpiresOn = today.AddDays(5), Reason = "Original owner disposition" });
            db.ReadinessExceptions.Add(new ReadinessException { ProjectId = project.Id, AssessmentId = assessmentId, BasisVersionId = versionId,
                ApprovedBy = pm, VerifierId = marc, ExpiresOn = today.AddDays(2), LimitedWork = "Stale exception", Risk = "Old owner" });
            return await db.SaveChangesAsync();
        });

        using var scope = f.Services.CreateScope();
        var digestDb = scope.ServiceProvider.GetRequiredService<HubDb>();
        var preRecoveryDigest = await Digest.Build(digestDb, marc, "Marc", today, f.Clock.Now.AddDays(-1), f.Clock.Now,
            new OrgSettings(), "https://example.test");
        Assert.Contains(preRecoveryDigest?.Sections ?? [], s => s.Code == "readinessExceptions" && s.Rows.Any(r => r.Name == "Stale exception"));

        await f.DbAsync(async db =>
        {
            db.Tasks.Single(t => t.Id == taskId).AssigneeId = omar;
            return await db.SaveChangesAsync();
        });
        var recovery = await f.As(TestData.Omar).Post($"/api/v1/projects/{project.Id}/readiness/Task/{taskId}", new
        {
            requestId = Guid.NewGuid(), targetRowVersion = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion),
            assessmentRowVersion = f.Db(db => db.ReadinessAssessments.Single(a => a.Id == assessmentId).RowVersion),
            intendedOutput = "Reconfirm output", completionCriteria = "Reconfirm criteria", reason = "Replacement owner reconfirmed"
        });
        Assert.True(recovery.IsSuccessStatusCode, await recovery.Content.ReadAsStringAsync());

        var oldVerifierDigest = await Digest.Build(digestDb, marc, "Marc", today, f.Clock.Now.AddDays(-1), f.Clock.Now,
            new OrgSettings(), "https://example.test");
        Assert.DoesNotContain(oldVerifierDigest?.Sections ?? [], s => s.Code == "readinessExceptions" && s.Rows.Any(r => r.Name == "Stale exception"));

        await f.DbAsync(async db =>
        {
            db.BasisAssumptionDispositions.Add(new BasisAssumptionDisposition { ProjectId = project.Id, VersionId = versionId,
                Scope = "Digest scope", OwnerId = omar, ApprovedBy = pm, ExpiresOn = today.AddDays(5), Reason = "Replacement owner disposition" });
            db.ReadinessExceptions.Add(new ReadinessException { ProjectId = project.Id, AssessmentId = assessmentId, BasisVersionId = versionId,
                ApprovedBy = pm, VerifierId = jill, ExpiresOn = today.AddDays(4), LimitedWork = "Current exception", Risk = "Replacement owner" });
            return await db.SaveChangesAsync();
        });

        var currentVerifierDigest = await Digest.Build(digestDb, jill, "Jill", today, f.Clock.Now.AddDays(-1), f.Clock.Now,
            new OrgSettings(), "https://example.test");
        var current = Assert.Single(currentVerifierDigest!.Sections, s => s.Code == "readinessExceptions");
        Assert.Contains(current.Rows, r => r.Name == "Current exception");

        Assert.Equal(2, f.Db(db => db.ReadinessExceptions.Count(e => e.AssessmentId == assessmentId)));
    }
}
