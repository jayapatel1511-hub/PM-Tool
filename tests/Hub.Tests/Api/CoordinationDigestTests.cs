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
        var sections = new[] { "submissions", "allocations", "basisImpacts", "constraints", "commitments", "issueVerifications", "readinessExceptions" };
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
}
