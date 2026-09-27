using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class SubmissionApiTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int expected = 200) => await (await f.As(who).Post(path, body)).Json(expected);
    async Task<JsonNode> Get(string who, string path) => await (await f.As(who).GetAsync(path)).Json();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task Issue_rechecks_fingerprint_and_preserves_the_first_snapshot()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Design submission", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Site grading", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var did = deliverable.G("id");
        var revision = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "grading", "Site grading", "A", "https://example.test/grading-a.pdf", "Design team", "Submission", null, null, null, null, null, null));
        var createBody = new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Design package", "Permit review", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(revision.G("id"))],
            [new("Traffic control plan applies", data.User(TestData.Alex), civil)], null, null);
        await Post(TestData.Alex, root + "/submissions", createBody, 403);
        var created = await Post(TestData.Marc, root + "/submissions", createBody with { RequestId = Guid.NewGuid() });
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Pm, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null), 403);
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.Applicability));
        await Post(TestData.Alex, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), optional.RowVersion,
            SubmissionCheckStatus.NotApplicable, "https://example.test/na.pdf", "Not in scope"), 403);
        await Post(TestData.Pm, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), optional.RowVersion,
            SubmissionCheckStatus.NotApplicable, "https://example.test/na.pdf", "Not in scope"));
        Assert.Equal(SubmissionCheckStatus.NotApplicable, f.Db(db => db.SubmissionChecks.Single(c => c.Id == optional.Id).Status));
        var detail = await Get(TestData.Marc, path);
        Assert.True(detail["readiness"]!["ready"]!.GetValue<bool>());
        var fingerprint = detail["readiness"]!["fingerprint"]!.GetValue<string>();
        await Post(TestData.Alex, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), Version<SubmissionCheck>(optional.Id),
            SubmissionCheckStatus.Pass, "https://example.test/traffic-plan.pdf", "Plan checked"));
        var issue = new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1, fingerprint, "Municipality", "https://example.test/transmittal-1", null);
        await Post(TestData.Marc, path + "/issue", issue, 403);
        await Post(TestData.Admin, path + "/issue", issue with { RequestId = Guid.NewGuid() }, 403);
        await Post(TestData.Pm, path + "/issue", issue, 409);
        Assert.Equal(0, f.Db(db => db.SubmissionIssues.Count(i => i.PackageId == id)));
        var fresh = await Get(TestData.Pm, path);
        var currentIssue = issue with { RequestId = Guid.NewGuid(), ExpectedFingerprint = fresh["readiness"]!["fingerprint"]!.GetValue<string>() };
        var issued = await Post(TestData.Pm, path + "/issue", currentIssue);
        Assert.Equal(id, issued.G("id"));
        Assert.Equal(SubmissionStatus.Issued, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        Assert.Single(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
        var snapshot = f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).ManifestSnapshot);
        Assert.Contains("grading-a.pdf", snapshot);
        Assert.Equal("A", JsonNode.Parse(snapshot)![0]!["revision"]!.GetValue<string>());
        await Post(TestData.Pm, path + "/issue", currentIssue);
        Assert.Single(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
        Assert.Equal(snapshot, f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).ManifestSnapshot));

        var successor = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Corrected design package", "Permit review", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(revision.G("id"))], [], id, "Corrected transmittal"));
        var nextId = successor.G("id"); var nextPath = root + $"/submissions/{nextId}";
        await Post(TestData.Marc, nextPath + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(nextId), null));
        var nextDetail = await Get(TestData.Pm, nextPath);
        await Post(TestData.Pm, nextPath + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(nextId), 1,
            nextDetail["readiness"]!["fingerprint"]!.GetValue<string>(), "Municipality", "https://example.test/transmittal-2", "Corrected transmittal"));
        Assert.Equal(SubmissionStatus.Superseded, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        Assert.Equal(snapshot, f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).ManifestSnapshot));
        Assert.Equal("https://example.test/transmittal-1", f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).TransmittalUrl));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => {
            var first = await db.SubmissionIssues.SingleAsync(i => i.PackageId == id);
            first.TransmittalUrl = "https://example.test/rewritten";
            await db.SaveChangesAsync(); return 0;
        }));
        Assert.Equal("https://example.test/transmittal-1", f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).TransmittalUrl));
    }

    [Fact]
    public async Task Publishing_revision_B_invalidates_ready_A_and_optional_evidence()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Issue milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Stormwater plan", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var did = deliverable.G("id");
        ChangeEndpoints.RegisterBody Registration(string revision, Guid? old = null) => new(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "stormwater", "Stormwater plan", revision, $"https://example.test/stormwater-{revision}.pdf", "Design team", "Submission", null,
            old, old == null ? null : f.Db(db => db.SourceHeads.Single(h => h.ProjectId == project.Id).RowVersion),
            old == null ? null : "Stormwater revision changed", old == null ? null : new DateOnly(2026, 9, 14), old == null ? null : new DateOnly(2026, 9, 18));
        var a = await Post(TestData.Alex, root + "/source-revisions", Registration("A"));
        var created = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Stormwater issue", "Permit", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(a.G("id"))],
            [new("Traffic control applies", data.User(TestData.Alex), civil)], null, null));
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.Applicability));
        await Post(TestData.Alex, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), optional.RowVersion,
            SubmissionCheckStatus.Pass, "https://example.test/traffic.pdf", "Scope checked"));
        Assert.Equal(SubmissionStatus.Ready, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        var notice = await Post(TestData.Alex, root + "/source-revisions", Registration("B", a.G("id")));
        var noticeId = notice.G("id");
        await Post(TestData.Alex, root + $"/changes/{noticeId}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(noticeId),
            f.Db(db => db.SourceHeads.Single(h => h.ProjectId == project.Id).RowVersion), null));
        Assert.Equal(SubmissionStatus.Checking, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        Assert.Equal(SubmissionCheckStatus.Pending, f.Db(db => db.SubmissionChecks.Single(c => c.Id == optional.Id).Status));
        var blocked = await Get(TestData.Pm, path);
        Assert.False(blocked["readiness"]!["ready"]!.GetValue<bool>());
        Assert.Contains("submission.revision_changed", blocked["readiness"]!["blockers"]!.ToJsonString());
        await Post(TestData.Pm, path + $"/checks/{optional.Id}/assign", new SubmissionEndpoints.CheckAssignBody(Guid.NewGuid(), Version<SubmissionPackage>(id),
            Version<SubmissionCheck>(optional.Id), data.User(TestData.Marc), "Original check owner unavailable"));
        Assert.Equal(data.User(TestData.Marc), f.Db(db => db.SubmissionChecks.Single(c => c.Id == optional.Id).OwnerId));
        await Post(TestData.Pm, path + "/assign", new SubmissionEndpoints.AssignBody(Guid.NewGuid(), Version<SubmissionPackage>(id), data.User(TestData.Pm),
            "Coordinator changed for submission"));
        var b = f.Db(db => db.ChangeNotices.Single(c => c.Id == noticeId).NewRevisionId);
        await Post(TestData.Marc, path + "/manifest", new SubmissionEndpoints.ManifestBody(Guid.NewGuid(), Version<SubmissionPackage>(id), [new(b)], "Use new stormwater revision"), 403);
        await Post(TestData.Pm, path + "/manifest", new SubmissionEndpoints.ManifestBody(Guid.NewGuid(), Version<SubmissionPackage>(id), [new(b)], "Use new stormwater revision"));
        Assert.Equal(2, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).ManifestVersion));
        Assert.Equal(a.G("id"), f.Db(db => db.SubmissionManifestItems.Single(m => m.PackageId == id && m.ManifestVersion == 1).SourceRevisionId));
    }

    [Fact]
    public async Task Electrical_blocking_finding_refuses_issue_and_cannot_be_waived()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil"); var electrical = data.ProjectDiscipline(project.Id, "Electrical");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Permit milestone", MilestoneType.PermitSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Site servicing", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = true }, 201);
        var did = deliverable.G("id");
        var revision = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "servicing", "Site servicing", "A", "https://example.test/servicing-a.pdf", "Design team", "Permit", null, null, null, null, null, null));
        var rid = revision.G("id");
        var review = await Post(TestData.Marc, root + "/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Civil / Electrical", "Check servicing interfaces", civil,
            data.User(TestData.Marc), [rid], [new(civil, data.User(TestData.Pm), new DateOnly(2026, 10, 1)),
                new(electrical, data.User(TestData.Omar), new DateOnly(2026, 10, 1))], true, null));
        var reviewId = review.G("id");
        await Post(TestData.Marc, root + $"/reviews/{reviewId}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(reviewId), "start", null));
        var finding = await Post(TestData.Omar, root + $"/reviews/{reviewId}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(reviewId),
            rid, electrical, data.User(TestData.Alex), "Resolve electrical clearance", "Blocking"));
        var created = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Permit issue", "Permit review", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(rid)], [], null, null));
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var blocked = await Get(TestData.Pm, path);
        Assert.False(blocked["readiness"]!["ready"]!.GetValue<bool>());
        Assert.Contains(finding.S("id"), blocked["readiness"]!["blockers"]!.ToJsonString());
        var mandatory = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.BlockingFindings));
        await Post(TestData.Pm, path + $"/checks/{mandatory.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), mandatory.RowVersion,
            SubmissionCheckStatus.NotApplicable, "https://example.test/waiver", "Ignore electrical finding"), 400);
        await Post(TestData.Pm, path + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1,
            blocked["readiness"]!["fingerprint"]!.GetValue<string>(), "Municipality", "https://example.test/transmittal", null), 422);
        Assert.Empty(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
    }
}
