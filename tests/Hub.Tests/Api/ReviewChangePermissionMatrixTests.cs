using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Hub.Api.Features;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ReviewChangePermissionMatrixTests(HubFactory f, Xunit.Abstractions.ITestOutputHelper output)
{
    readonly TestData d = new(f);

    [Fact] // AC-FIG-08 decisions 7, 8 and 13: options and command gates across the full role/status matrix.
    public async Task Coordination_option_flags_match_real_commands_for_every_role_and_status()
    {
        var users = new[] { TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Rita, TestData.Lena };
        foreach (var status in ProjectStatus.All)
        {
            var project = await d.Project(activate: true);
            var discipline = d.ProjectDiscipline(project.Id, "Civil");
            var tasks = new Dictionary<string, Guid>();
            var deliverables = new Dictionary<string, Guid>();
            var sourceIds = new Dictionary<string, Guid>();
            foreach (var user in users)
            {
                tasks[user] = (await d.NewTask(project.Id, TestData.Alex, extra: new { assigneeId = d.User(TestData.Alex) })).G("id");
                var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
                {
                    name = $"Permission source {user}", projectDisciplineId = discipline, deliverableTypeId = await d.DeliverableType(),
                    ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false,
                })).Json(201);
                deliverables[user] = deliverable.G("id");
                var seededSource = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
                    Guid.NewGuid(), deliverables[user], f.Db(db => db.Deliverables.Single(x => x.Id == deliverables[user]).RowVersion), discipline,
                    d.User(TestData.Alex), "Deliverable", $"matrix-{user}", $"Permission source {user}", "A", $"https://example.test/source-{user}.pdf",
                    "Synthetic issuer", "Synthetic scope", null, null, null, "Valid source registration", DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), null))).Json();
                sourceIds[user] = seededSource.G("id");
                await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/readiness/Task/{tasks[user]}",
                    new ReadinessEndpoints.CreateBody(Guid.NewGuid(), f.Db(db => db.Tasks.Single(x => x.Id == tasks[user]).RowVersion), "Synthetic output", "Synthetic criteria", $"Seed readiness for {user} {status}"))).Json();
                await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/readiness/Deliverable/{deliverables[user]}",
                    new ReadinessEndpoints.CreateBody(Guid.NewGuid(), f.Db(db => db.Deliverables.Single(x => x.Id == deliverables[user]).RowVersion), "Synthetic output", "Synthetic criteria", $"Seed readiness for {user} {status}"))).Json();
            }
            await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == project.Id)).Status = status; return await db.SaveChangesAsync(); });

            foreach (var user in users)
            {
                var taskId = tasks[user];
                var options = await (await f.As(user).GetAsync($"/api/v1/projects/{project.Id}/changes/options")).Json();
                var task = options["tasks"]!.AsArray().Single(x => x!.G("id") == taskId);
                var deliverableId = deliverables[user];
                var deliverableRow = options["deliverables"]!.AsArray().Single(x => x!.G("id") == deliverableId);
                var managed = options["manageDisciplineIds"]!.AsArray().Any(x => x!.GetValue<Guid>() == discipline);
                var canWrite = options["canWrite"]!.GetValue<bool>();
                async Task AssertCommit(string type, Guid id, JsonNode row)
                {
                    var canCommit = row["canCommit"]!.GetValue<bool>();
                    var legacy = row["ownerId"]!.GetValue<Guid>() == options["actorId"]!.GetValue<Guid>() || managed;
                    var rowVersion = row["rowVersion"]!.GetValue<int>();
                    var response = await f.As(user).Post($"/api/v1/projects/{project.Id}/weekly-commitments/{type}/{id}",
                        new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), rowVersion, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 7), "Synthetic output", "Synthetic criteria", $"Valid commitment for {user} {status}"));
                    Assert.True(canCommit ? response.IsSuccessStatusCode : response.StatusCode == HttpStatusCode.Forbidden,
                        $"{user} {status} {type} commit returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                    if (legacy != canCommit) output.WriteLine($"{user} {status} decision7.{type}: client {legacy}, flag {canCommit}, HTTP {(int)response.StatusCode}");
                }
                await AssertCommit("Task", taskId, task);
                await AssertCommit("Deliverable", deliverableId, deliverableRow);

                async Task AssertApplicability(string type, Guid id, JsonNode row)
                {
                    var canManage = row["canManage"]!.GetValue<bool>();
                    var assessment = f.Db(db => db.ReadinessAssessments.Single(x => x.TargetType == type && x.TargetId == id));
                    var check = f.Db(db => db.ReadinessChecks.Single(x => x.AssessmentId == assessment.Id && x.Code == ReadinessCheckCode.ProductionOwner));
                    var response = await f.As(user).Post($"/api/v1/projects/{project.Id}/readiness/{type}/{id}/checks/{ReadinessCheckCode.ProductionOwner}/applicability",
                        new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), assessment.RowVersion, check.RowVersion, true, "Synthetic applicability", "https://example.test/applicability"));
                    Assert.True(canManage ? response.IsSuccessStatusCode : response.StatusCode == HttpStatusCode.Forbidden,
                        $"{user} {status} {type} applicability returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
                    if ((canWrite && managed) != canManage) output.WriteLine($"{user} {status} decision8.{type}: client {canWrite && managed}, flag {canManage}, HTTP {(int)response.StatusCode}");
                }
                await AssertApplicability("Task", taskId, task);
                await AssertApplicability("Deliverable", deliverableId, deliverableRow);

                var head = f.Db(db => db.SourceHeads.Single(x => x.ProjectId == project.Id && x.Identity == $"deliverable:{deliverableId}"));
                var ownedSourceBody = new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverableId,
                    f.Db(db => db.Deliverables.Single(x => x.Id == deliverableId).RowVersion), discipline, d.User(TestData.Alex), "Deliverable",
                    $"matrix-{user}", $"Owned source {user} {status}", "B", "https://example.test/owned-source.pdf", "Synthetic issuer", "Synthetic scope",
                    null, sourceIds[user], head.RowVersion, "Valid owned source", DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(7));
                var sourceWrite = await f.As(user).Post($"/api/v1/projects/{project.Id}/source-revisions", ownedSourceBody);
                var canCreateChange = options["canCreate"]!["changes"]!.GetValue<bool>();
                Assert.True(canCreateChange ? sourceWrite.IsSuccessStatusCode : sourceWrite.StatusCode == HttpStatusCode.Forbidden,
                    $"{user} {status} source returned {(int)sourceWrite.StatusCode}: {await sourceWrite.Content.ReadAsStringAsync()}");
                if (canWrite != canCreateChange) output.WriteLine($"{user} {status} decision13.changes: client {canWrite}, flag {canCreateChange}, HTTP {(int)sourceWrite.StatusCode}");
                var reviewBody = new ReviewEndpoints.CreateBody(Guid.NewGuid(), $"Review {user} {status}", "Permission matrix review", discipline, d.User(TestData.Marc),
                    [sourceIds[user]], [new ReviewEndpoints.Assignment(discipline, d.User(TestData.Pm), DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(7))], false, $"Valid review for {user} {status}");
                var review = await f.As(user).Post($"/api/v1/projects/{project.Id}/reviews", reviewBody);
                var canCreateReview = options["canCreate"]!["reviews"]!.GetValue<bool>();
                Assert.True(canCreateReview ? review.IsSuccessStatusCode : review.StatusCode == HttpStatusCode.Forbidden,
                    $"{user} {status} review returned {(int)review.StatusCode}: {await review.Content.ReadAsStringAsync()}");
                var legacyReview = canWrite && options["manageDisciplineIds"]!.AsArray().Count > 0;
                if (legacyReview != canCreateReview) output.WriteLine($"{user} {status} decision13.reviews: client {legacyReview}, flag {canCreateReview}, HTTP {(int)review.StatusCode}");
            }
        }
    }

    [Fact] // AC-FIG-08 decision 12: change-assessment management follows the work row's canManage flag.
    public async Task Change_assessment_management_matches_work_row_for_every_role_and_status()
    {
        var users = new[] { TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Rita, TestData.Lena };
        foreach (var status in ProjectStatus.All)
        foreach (var user in users)
        {
            var project = await d.Project(activate: true);
            var discipline = d.ProjectDiscipline(project.Id, "Civil");
            var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
            {
                name = "Assessment matrix deliverable", projectDisciplineId = discipline,
                deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false,
            })).Json(201);
            var deliverableId = deliverable.G("id");
            var sourceA = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
                Guid.NewGuid(), deliverableId, f.Db(db => db.Deliverables.Single(x => x.Id == deliverableId).RowVersion), discipline,
                d.User(TestData.Alex), "Deliverable", "assessment-matrix", "Assessment matrix source", "A", "https://example.test/assessment-a.pdf",
                "Synthetic", "Assessment", null, null, null, "Seed source A", null, null))).Json();
            var sourceAId = sourceA.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/input-uses", new ChangeEndpoints.AdoptBody(
                Guid.NewGuid(), "Deliverable", deliverableId, f.Db(db => db.Deliverables.Single(x => x.Id == deliverableId).RowVersion), sourceAId,
                sourceAId, null, "Coordinate the assessment fixture", "Seed the current source"))).Json();
            var head = f.Db(db => db.SourceHeads.Single(x => x.ProjectId == project.Id && x.Identity == $"deliverable:{deliverableId}"));
            var sourceB = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
                Guid.NewGuid(), deliverableId, f.Db(db => db.Deliverables.Single(x => x.Id == deliverableId).RowVersion), discipline,
                d.User(TestData.Alex), "Deliverable", "assessment-matrix", "Assessment matrix source", "B", "https://example.test/assessment-b.pdf",
                "Synthetic", "Assessment", null, sourceAId, head.RowVersion, "Create assessment", DateOnly.FromDateTime(f.Clock.Now.UtcDateTime),
                DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(7)))).Json();
            await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/changes/{sourceB.G("id")}/publish",
                new ChangeEndpoints.PublishBody(Guid.NewGuid(), sourceB.I("rowVersion"), head.RowVersion, null))).Json();
            await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == project.Id)).Status = status; return await db.SaveChangesAsync(); });

            var options = await (await f.As(user).GetAsync($"/api/v1/projects/{project.Id}/changes/options")).Json();
            var row = options["deliverables"]!.AsArray().Single(x => x!.G("id") == deliverableId);
            var canManage = row["canManage"]!.GetValue<bool>();
            var legacyManage = options["manageDisciplineIds"]!.AsArray().Any(x => x!.GetValue<Guid>() == discipline);
            var assessmentId = f.Db(db => db.ChangeAssessments.Single(x => x.TargetId == deliverableId).Id);
            var rowVersion = f.Db(db => db.ChangeAssessments.Single(x => x.Id == assessmentId).RowVersion);
            var response = await f.As(user).Post($"/api/v1/projects/{project.Id}/changes/{sourceB.G("id")}/assessments/{assessmentId}",
                new ChangeEndpoints.AssessmentBody(Guid.NewGuid(), rowVersion, "assign", null, "Assign the assessment to its current work owner", null,
                    null, null, null, null, d.User(TestData.Alex), d.User(TestData.Marc), null, null, null));
            Assert.True(canManage ? response.IsSuccessStatusCode : response.StatusCode == HttpStatusCode.Forbidden,
                $"{user} {status} assessment assignment returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}; canManage={canManage}");
            if (legacyManage != canManage) output.WriteLine($"{user} {status} decision12: client {legacyManage}, flag {canManage}, HTTP {(int)response.StatusCode}");
        }
    }
}
