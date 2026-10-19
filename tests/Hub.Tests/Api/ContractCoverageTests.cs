using System.Text.Json.Nodes;
using Hub.Api.Features;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// Exercises the typed response operations which the contract shape recorder previously missed.
/// These assertions intentionally inspect response fields, rather than treating a 200 as coverage.
[Collection("api")]
public sealed class ContractCoverageTests(HubFactory f)
{
    readonly TestData data = new(f);

    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    async Task<(Project Project, Guid Review, Guid Assignment, Guid Civil, Guid Electrical)> NewReview()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var electrical = data.ProjectDiscipline(project.Id, "Electrical");
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Coverage deliverable", projectDisciplineId = civil, deliverableTypeId = await data.DeliverableType(),
            ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false,
        })).Json(201);
        var did = deliverable.G("id");
        var revision = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex), "Deliverable", "coverage", "Coverage", "A",
            "https://example.test/coverage.pdf", "Coverage", "Review", null, null, null, null, null, null))).Json();
        var review = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/reviews", new ReviewEndpoints.CreateBody(
            Guid.NewGuid(), "Coverage review", "Contract response coverage", civil, data.User(TestData.Marc), [revision.G("id")],
            [new ReviewEndpoints.Assignment(civil, data.User(TestData.Pm), new DateOnly(2026, 9, 18)), new ReviewEndpoints.Assignment(electrical, data.User(TestData.Omar), new DateOnly(2026, 9, 18))], false, null))).Json();
        var reviewId = review.G("id");
        var roundId = f.Db(db => db.ReviewPackages.Single(p => p.Id == reviewId).CurrentRoundId);
        var assignment = f.Db(db => db.DisciplineReviews.Single(a => a.RoundId == roundId && a.ProjectDisciplineId == electrical).Id);
        return (project, reviewId, assignment, civil, electrical);
    }

    [Fact]
    public async Task Reference_and_usage_reads_have_stable_payloads()
    {
        var admin = f.As(TestData.Admin);
        foreach (var kind in new[] { "clients", "deliverableTypes", "disciplines", "offices", "phases", "projectTypes" })
        {
            var rows = await (await admin.GetAsync($"/api/v1/admin/{kind}")).Json();
            Assert.NotEmpty(rows.AsArray());
            var id = rows[0]!.G("id");
            var usage = await (await admin.GetAsync($"/api/v1/admin/{kind}/{id}/usage")).Json();
            Assert.True(usage["projects"]!.GetValue<int>() >= 0);
        }
    }

    [Fact]
    public async Task Development_directory_and_hr_identity_reads_return_people()
    {
        var directory = await (await f.CreateClient().GetAsync("/api/dev/users")).Json();
        Assert.Contains(directory.AsArray(), row => row!["email"]?.GetValue<string>() == TestData.Admin);

        var identities = await (await f.As(TestData.Admin).GetAsync("/api/v1/planning/hr-identities")).Json();
        Assert.NotEmpty(identities.AsArray());
        Assert.All(identities.AsArray(), row =>
        {
            Assert.NotEqual(Guid.Empty, row!.G("id"));
            Assert.False(string.IsNullOrWhiteSpace(row.S("displayName")));
            Assert.True(row["rowVersion"]!.GetValue<int>() >= 0);
        });
    }

    [Fact]
    public async Task Issue_verification_watchers_and_calendar_subscription_reads_are_real_payloads()
    {
        var project = await data.Project();
        var discipline = data.ProjectDiscipline(project.Id, "Civil");
        var issue = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/issues", new
        {
            title = "Contract coverage issue",
            description = "Shape recorder fixture",
            ownerId = data.User(TestData.Alex),
            severity = "Medium",
            projectDisciplineId = discipline,
            issueType = "General",
        })).Json(201);
        var issueId = issue.G("id");

        var verification = await (await f.As(TestData.Pm).GetAsync($"/api/v1/issues/{issueId}/verification")).Json();
        Assert.NotNull(verification);
        Assert.True(verification is JsonArray);

        var task = await data.NewTask(project.Id, TestData.Marc);
        var watchers = await (await f.As(TestData.Marc).GetAsync($"/api/v1/items/Task/{task.G("id")}/watchers")).Json();
        Assert.True(watchers is JsonArray);
        Assert.DoesNotContain(watchers.AsArray(), row => row!["userId"]?.GetValue<Guid>() == data.User(TestData.Marc));

        var subscription = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/calendar-subscription")).Json();
        Assert.False(subscription["enabled"]!.GetValue<bool>());
        Assert.Equal(0, subscription["rowVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task Review_coordinator_and_reviewer_assignment_mutations_return_updated_rows()
    {
        var s = await NewReview();
        var packageVersion = Version<ReviewPackage>(s.Review);
        var coordinator = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{s.Project.Id}/reviews/{s.Review}/assign", new ReviewEndpoints.ReassignBody(
            Guid.NewGuid(), packageVersion, data.User(TestData.Pm), "Coverage coordinator"))).Json();
        Assert.Equal(s.Review, coordinator.G("id"));
        Assert.Equal(data.User(TestData.Pm), f.Db(db => db.ReviewPackages.Single(p => p.Id == s.Review).CoordinatorId));

        var assignmentVersion = Version<DisciplineReview>(s.Assignment);
        var reassigned = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{s.Project.Id}/reviews/{s.Review}/assignments/{s.Assignment}/assign", new ReviewEndpoints.ReassignBody(
            Guid.NewGuid(), assignmentVersion, data.User(TestData.Pm), "Coverage reviewer"))).Json();
        Assert.Equal(s.Assignment, reassigned.G("id"));
        Assert.Equal(data.User(TestData.Pm), f.Db(db => db.DisciplineReviews.Single(a => a.Id == s.Assignment).ReviewerId));
        Assert.True(reassigned["rowVersion"]!.GetValue<int>() > assignmentVersion);
    }

    [Fact]
    public async Task Submission_edit_mutation_returns_new_version_and_preserves_content()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones", new MilestoneEndpoints.CreateBody(
            "Coverage milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, false))).Json(201);
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Submission source", projectDisciplineId = civil, deliverableTypeId = await data.DeliverableType(),
            ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false,
        })).Json(201);
        var did = deliverable.G("id");
        var revision = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex), "Deliverable", "submission", "Submission", "A",
            "https://example.test/submission.pdf", "Coverage", "Submission", null, null, null, null, null, null))).Json();
        var created = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/submissions", new SubmissionEndpoints.CreateBody(
            Guid.NewGuid(), "Original submission", "Original purpose", "Municipality", data.User(TestData.Marc), milestone.G("id"),
            new DateOnly(2026, 10, 15), [new SubmissionEndpoints.ManifestInput(revision.G("id"))], [], null, null))).Json();
        var id = created.G("id");
        var oldVersion = created["rowVersion"]!.GetValue<int>();
        var edited = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/submissions/{id}/edit", new SubmissionEndpoints.EditBody(
            Guid.NewGuid(), oldVersion, "Edited submission", "Edited purpose", "Municipality", milestone.G("id"), new DateOnly(2026, 10, 20), "Coverage edit"))).Json();
        Assert.Equal(id, edited.G("id"));
        Assert.True(edited["rowVersion"]!.GetValue<int>() > oldVersion);
        Assert.Equal("Edited submission", f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Title));
    }
}
