using System.Net;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class SubmissionPermissionMatrixTests(HubFactory f, Xunit.Abstractions.ITestOutputHelper output)
{
    readonly TestData d = new(f);

    [Fact] // AC-FIG-08 decision 10: superseding an Issued package follows createSubmission for every role/status.
    public async Task Submission_supersede_matches_create_submission_for_every_role_and_status()
    {
        var users = new[] { TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Rita, TestData.Lena };
        foreach (var status in ProjectStatus.All)
        {
            var project = await d.Project(activate: true);
            var milestone = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones",
                new MilestoneEndpoints.CreateBody("Supersede milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 11, 15), null, null, null, true))).Json(201);
            var milestoneId = milestone.G("id");
            var discipline = d.ProjectDiscipline(project.Id, "Civil");
            var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
            { name = "Supersede matrix deliverable", projectDisciplineId = discipline, deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false })).Json(201);
            var did = deliverable.G("id");
            var revision = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
                Guid.NewGuid(), did, f.Db(db => db.Deliverables.Single(x => x.Id == did).RowVersion), discipline, d.User(TestData.Alex),
                "Deliverable", "supersede-matrix", "Supersede matrix deliverable", "A", "https://example.test/supersede.pdf", "Synthetic", "Submission", null, null, null, null, null, null))).Json();
            var original = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/submissions",
                new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Issued package", "Synthetic supersede fixture", "Synthetic recipient", d.User(TestData.Pm),
                    milestoneId, new DateOnly(2026, 11, 15), [new(revision.G("id"))], [], null, null))).Json();
            var originalId = original.G("id");
            await f.DbAsync(async db =>
            {
                (await db.SubmissionPackages.SingleAsync(x => x.Id == originalId)).Status = SubmissionStatus.Issued;
                (await db.Projects.SingleAsync(x => x.Id == project.Id)).Status = status;
                return await db.SaveChangesAsync();
            });

            foreach (var user in users)
            {
                var client = f.As(user);
                var projectView = await (await client.GetAsync($"/api/v1/projects/{project.Id}")).Json();
                var detail = await (await client.GetAsync($"/api/v1/projects/{project.Id}/submissions/{originalId}")).Json();
                var options = await (await client.GetAsync($"/api/v1/projects/{project.Id}/changes/options")).Json();
                var legacy = detail["canAuthorise"]!.GetValue<bool>() || options["manageDisciplineIds"]!.AsArray().Count > 0;
                var allowed = projectView["permissions"]!["createSubmission"]!["ok"]!.GetValue<bool>();
                var response = await client.Post($"/api/v1/projects/{project.Id}/submissions", new SubmissionEndpoints.CreateBody(
                    Guid.NewGuid(), $"Superseding package {user} {status}", "Synthetic supersede validation", "Synthetic recipient", d.User(TestData.Pm),
                    milestoneId, new DateOnly(2026, 11, 15), [new(revision.G("id"))], [], originalId, "Permission matrix correction"));
                Assert.True(allowed ? response.IsSuccessStatusCode : response.StatusCode == HttpStatusCode.Forbidden,
                    $"{user} {status} supersede returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}; createSubmission={allowed}");
                if (legacy != allowed) output.WriteLine($"{user} {status} decision10: client {legacy}, flag {allowed}, HTTP {(int)response.StatusCode}");
            }
        }
    }
}
