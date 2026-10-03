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
        var project = await data.Project();
        await f.DbAsync(async db => { var row = await db.Projects.SingleAsync(p => p.Id == project.Id); row.Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        var root = $"/api/v1/projects/{project.Id}";
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
        var filtered = await Get(TestData.Pm, root + $"/submissions?status=Ready&coordinatorId={data.User(TestData.Marc)}&milestoneId={milestone.G("id")}&targetFrom=2026-10-15&targetTo=2026-10-15&page=1&pageSize=1");
        Assert.Equal(1, filtered["totalCount"]!.GetValue<int>());
        Assert.Equal("Ready", filtered["items"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(id, filtered["items"]![0]!.G("id"));
        Assert.Equal(0, (await Get(TestData.Pm, root + "/submissions?status=Checking"))["totalCount"]!.GetValue<int>());
        var listExport = await f.As(TestData.Pm).GetAsync(root + $"/submissions/export?status=Ready&coordinatorId={data.User(TestData.Marc)}&milestoneId={milestone.G("id")}&targetFrom=2026-10-15&targetTo=2026-10-15&format=csv");
        Assert.Contains("Design package", await listExport.Content.ReadAsStringAsync());
        await (await f.As(TestData.Rita).GetAsync(root + "/submissions")).Json(404);
        await (await f.As(TestData.Rita).GetAsync(root + "/submissions/export?format=csv")).Json(404);
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
        var notice = f.Db(db => db.Notifications.Single(n => n.UserId == data.User(TestData.Marc) &&
            n.EventType == NotificationEvents.SubmissionChanged && n.ItemId == id && n.ActorUserId == data.User(TestData.Pm)));
        Assert.Equal($"/projects/{project.ProjectNumber}/submissions?panel=SubmissionPackage:{id}", notice.LinkPath);
        var noticesBeforeRetry = f.Db(db => db.Notifications.Count(n => n.EventType == NotificationEvents.SubmissionChanged && n.ItemId == id));
        await Post(TestData.Pm, path + "/issue", currentIssue);
        Assert.Single(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
        Assert.Equal(noticesBeforeRetry, f.Db(db => db.Notifications.Count(n => n.EventType == NotificationEvents.SubmissionChanged && n.ItemId == id)));
        Assert.False(f.Db(db => db.Notifications.Any(n => n.UserId == data.User(TestData.Rita) && n.ItemId == id)));
        Assert.Equal(snapshot, f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).ManifestSnapshot));

        var revisionBNotice = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did,
            Version<Deliverable>(did), civil, data.User(TestData.Alex), "Deliverable", "grading", "Site grading", "B",
            "https://example.test/grading-b.pdf", "Design team", "Submission", null, revision.G("id"),
            f.Db(db => db.SourceHeads.Single(h => h.ProjectId == project.Id).RowVersion), "Corrected grading revision", new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18)));
        var revisionBNoticeId = revisionBNotice.G("id");
        await Post(TestData.Alex, root + $"/changes/{revisionBNoticeId}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(),
            Version<ChangeNotice>(revisionBNoticeId), f.Db(db => db.SourceHeads.Single(h => h.ProjectId == project.Id).RowVersion), null));
        var revisionB = f.Db(db => db.ChangeNotices.Single(c => c.Id == revisionBNoticeId).NewRevisionId);
        var successor = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Corrected design package", "Permit review", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(revisionB)], [], id, "Corrected transmittal"));
        var nextId = successor.G("id"); var nextPath = root + $"/submissions/{nextId}";
        await Post(TestData.Marc, nextPath + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(nextId), null));
        var nextDetail = await Get(TestData.Pm, nextPath);
        await Post(TestData.Pm, nextPath + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(nextId), 1,
            nextDetail["readiness"]!["fingerprint"]!.GetValue<string>(), "Municipality", "https://example.test/transmittal-2", "Corrected transmittal"));
        Assert.Equal(SubmissionStatus.Superseded, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        Assert.Equal(snapshot, f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).ManifestSnapshot));
        Assert.Equal("https://example.test/transmittal-1", f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == id).TransmittalUrl));
        var export = await Get(TestData.Pm, path + "/export");
        Assert.Equal("A", export["manifest"]![0]!["revision"]!.GetValue<string>());
        Assert.Equal("https://example.test/transmittal-1", export["issueHistory"]![0]!["issue"]!["transmittalUrl"]!.GetValue<string>());
        Assert.Equal(nextId.ToString(), export["successorIssues"]![0]!["id"]!.GetValue<string>());
        var successorSnapshot = f.Db(db => db.SubmissionIssues.Single(i => i.PackageId == nextId).ManifestSnapshot);
        Assert.Contains("grading-b.pdf", successorSnapshot);
        Assert.Equal("B", JsonNode.Parse(successorSnapshot)![0]!["revision"]!.GetValue<string>());
        var successorHistorySnapshot = export["successorIssues"]![0]!["issue"]!["manifestSnapshot"]!.GetValue<string>();
        Assert.Equal("B", JsonNode.Parse(successorHistorySnapshot)![0]!["revision"]!.GetValue<string>());
        await (await f.As(TestData.Rita).GetAsync(path + "/export")).Json(404);
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
            [new("Traffic control applies", data.User(TestData.Omar), civil)], null, null));
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.Applicability));
        await Post(TestData.Omar, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), optional.RowVersion,
            SubmissionCheckStatus.Pass, "https://example.test/traffic.pdf", "Scope checked"));
        Assert.Equal(SubmissionStatus.Ready, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
        var notice = await Post(TestData.Alex, root + "/source-revisions", Registration("B", a.G("id")));
        var noticeId = notice.G("id");
        var publish = new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(noticeId),
            f.Db(db => db.SourceHeads.Single(h => h.ProjectId == project.Id).RowVersion), null);
        await Post(TestData.Alex, root + $"/changes/{noticeId}/publish", publish);
        foreach (var recipient in new[] { TestData.Pm, TestData.Marc, TestData.Omar })
        {
            var invalidated = Assert.Single(f.Db(db => db.Notifications.Where(n => n.ItemId == id && n.UserId == data.User(recipient)
                && n.EventType == NotificationEvents.SubmissionChanged && n.ActorUserId == data.User(TestData.Alex)).ToList()));
            Assert.Equal(1, invalidated.Count);
            Assert.Equal($"/projects/{project.ProjectNumber}/submissions?panel=SubmissionPackage:{id}", invalidated.LinkPath);
        }
        var notices = f.Db(db => db.Notifications.Where(n => n.ItemId == id).OrderBy(n => n.Id).Select(n => new { n.Id, n.Count }).ToList());
        await Post(TestData.Alex, root + $"/changes/{noticeId}/publish", publish);
        Assert.Equal(notices, f.Db(db => db.Notifications.Where(n => n.ItemId == id).OrderBy(n => n.Id).Select(n => new { n.Id, n.Count }).ToList()));
        Assert.False(f.Db(db => db.Notifications.Any(n => n.ItemId == id && n.UserId == data.User(TestData.Rita))));
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
        var checking = await Get(TestData.Pm, root + "/submissions?status=Checking&page=1&pageSize=1");
        Assert.Equal(1, checking["totalCount"]!.GetValue<int>());
        Assert.Equal(id, checking["items"]![0]!.G("id"));
        Assert.Equal(0, (await Get(TestData.Pm, root + "/submissions?status=Ready"))["totalCount"]!.GetValue<int>());

        var mandatory = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.BlockingFindings));
        await Post(TestData.Pm, path + $"/checks/{mandatory.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), mandatory.RowVersion,
            SubmissionCheckStatus.NotApplicable, "https://example.test/waiver", "Ignore electrical finding"), 400);
        await Post(TestData.Pm, path + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1,
            blocked["readiness"]!["fingerprint"]!.GetValue<string>(), "Municipality", "https://example.test/transmittal", null), 422);
        Assert.Empty(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
    }

    [Fact]
    public async Task Optional_applicability_fail_is_recorded_without_blocking_issue()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Fail milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Fail target", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var did = deliverable.G("id");
        var revision = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "fail-target", "Fail target", "A", "https://example.test/fail-a.pdf", "Design team", "Submission", null, null, null, null, null, null));
        var created = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Fail package", "Permit", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(revision.G("id"))],
            [new("Traffic control plan applies", data.User(TestData.Alex), civil)], null, null));
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == id && c.Kind == SubmissionCheckKind.Applicability));
        await Post(TestData.Alex, path + $"/checks/{optional.Id}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(id), optional.RowVersion,
            SubmissionCheckStatus.Fail, "https://example.test/fail-evidence", "Plan is outside the registered tolerance"));
        var detail = await Get(TestData.Pm, path);
        Assert.True(detail["readiness"]!["ready"]!.GetValue<bool>());
        Assert.Equal(SubmissionCheckStatus.Fail, detail["checks"]!.AsArray().Single(c => c!.G("id") == optional.Id)!["status"]!.GetValue<string>());
        var filtered = await Get(TestData.Pm, root + "/submissions?status=Ready&page=1&pageSize=10");
        Assert.Contains(filtered["items"]!.AsArray(), item => item!.G("id") == id);
        await Post(TestData.Pm, path + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1,
            detail["readiness"]!["fingerprint"]!.GetValue<string>(), "Municipality", "https://example.test/transmittal", null));
        Assert.Equal(SubmissionStatus.Issued, f.Db(db => db.SubmissionPackages.Single(p => p.Id == id).Status));
    }

    [Fact]
    public async Task Required_review_edit_makes_a_stale_issue_request_refuse_finalization()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Review milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Reviewed target", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = true }, 201);
        var did = deliverable.G("id");
        var revision = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "reviewed-target", "Reviewed target", "A", "https://example.test/reviewed-a.pdf", "Design team", "Submission", null, null, null, null, null, null));
        var rid = revision.G("id");
        var review = await Post(TestData.Marc, root + "/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Reviewed target", "Required review", civil,
            data.User(TestData.Marc), [rid], [new(civil, data.User(TestData.Pm), new DateOnly(2026, 10, 1))], true, null));
        var reviewId = review.G("id");
        await Post(TestData.Marc, root + $"/reviews/{reviewId}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(reviewId), "start", null));
        var assignmentId = f.Db(db => db.DisciplineReviews.Single(a => a.RoundId == db.ReviewPackages.Single(p => p.Id == reviewId).CurrentRoundId).Id);
        await Post(TestData.Pm, root + $"/reviews/{reviewId}/assignments/{assignmentId}/decision", new ReviewEndpoints.DecisionBody(Guid.NewGuid(),
            Version<DisciplineReview>(assignmentId), DisciplineReviewStatus.Approved, "Reviewed the registered revision"));
        var created = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Reviewed package", "Permit", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(rid)], [], null, null));
        var id = created.G("id"); var path = root + $"/submissions/{id}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var beforeEdit = await Get(TestData.Pm, path);
        Assert.True(beforeEdit["readiness"]!["ready"]!.GetValue<bool>());
        var fingerprint = beforeEdit["readiness"]!["fingerprint"]!.GetValue<string>();
        await Post(TestData.Marc, root + $"/reviews/{reviewId}/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(reviewId),
            "Updated required review scope", [rid], [new(civil, data.User(TestData.Pm), new DateOnly(2026, 10, 2))], "Scope edited before issue", null));
        await Post(TestData.Pm, path + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1, fingerprint,
            "Municipality", "https://example.test/transmittal", null), 409);
        Assert.Empty(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == id).ToList()));
        var afterEdit = await Get(TestData.Pm, path);
        Assert.False(afterEdit["readiness"]!["ready"]!.GetValue<bool>());
        Assert.Contains("submission.review_not_current", afterEdit["readiness"]!["blockers"]!.ToJsonString());
    }

    [Fact]
    public async Task Handoff_change_invalidates_unissued_submission_checks_and_evidence()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var milestone = await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Handoff issue", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201);
        var deliverable = await Post(TestData.Marc, root + "/deliverables", new { name = "Handoff target", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var did = deliverable.G("id");
        var revision = await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did), civil, data.User(TestData.Alex),
            "Deliverable", "handoff-target", "Handoff target", "A", "https://example.test/handoff-target.pdf", "Design team", "Submission", null, null, null, null, null, null));
        var created = await Post(TestData.Marc, root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Handoff package", "Permit", "Municipality",
            data.User(TestData.Marc), milestone.G("id"), new DateOnly(2026, 10, 15), [new(revision.G("id"))], [], null, null));
        var packageId = created.G("id");
        var checkId = await f.DbAsync(async db => {
            var package = await db.SubmissionPackages.SingleAsync(p => p.Id == packageId);
            package.Status = SubmissionStatus.Ready;
            var check = await db.SubmissionChecks.FirstAsync(c => c.PackageId == packageId);
            check.Status = SubmissionCheckStatus.Pass; check.EvidenceUrl = "https://example.test/evidence";
            db.CheckEvidences.Add(new CheckEvidence { ProjectId = project.Id, CheckId = check.Id, EvidenceUrl = check.EvidenceUrl, Note = "verified" });
            await db.SaveChangesAsync(); return check.Id;
        });
        var handoff = await Post(TestData.Marc, root + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Target handoff", did,
            Version<Deliverable>(did), "A", "https://example.test/handoff.pdf", civil, data.User(TestData.Alex), data.User(TestData.Marc),
            null, did, "Use in package", "Accepted", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1), null, null), 201);
        var handoffId = handoff.G("id");
        Assert.Equal(SubmissionStatus.Checking, f.Db(db => db.SubmissionPackages.Single(p => p.Id == packageId).Status));
        Assert.Equal(SubmissionCheckStatus.Pending, f.Db(db => db.SubmissionChecks.Single(c => c.Id == checkId).Status));
        await f.DbAsync(async db => {
            var package = await db.SubmissionPackages.SingleAsync(p => p.Id == packageId); package.Status = SubmissionStatus.Ready;
            var check = await db.SubmissionChecks.SingleAsync(c => c.Id == checkId); check.Status = SubmissionCheckStatus.Pass;
            check.EvidenceUrl = "https://example.test/evidence-again";
            return await db.SaveChangesAsync();
        });
        await Post(TestData.Marc, root + $"/handoffs/{handoffId}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Cancelled,
            Version<Handoff>(handoffId), "Handoff source changed", null));
        Assert.Equal(SubmissionStatus.Checking, f.Db(db => db.SubmissionPackages.Single(p => p.Id == packageId).Status));
        var checkAfter = f.Db(db => db.SubmissionChecks.Single(c => c.Id == checkId));
        Assert.Equal(SubmissionCheckStatus.Pending, checkAfter.Status); Assert.Null(checkAfter.EvidenceUrl);

        var basis = await Post(TestData.Marc, root + "/design-basis", new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion,
            "Handoff basis", data.User(TestData.Alex), civil, null,
            new DesignBasisEndpoints.VersionInput("Service", "Initial basis", 10, "kPa", "Manual", "basis-1", "https://example.test/basis-1", "A", new DateOnly(2026, 10, 5), null), "Initial basis"));
        var basisId = basis.G("id");
        var basisVersionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == basisId).Id);
        await Post(TestData.Marc, root + $"/design-basis/{basisId}/versions/{basisVersionId}/confirm",
            new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(basisId), Version<DesignBasisVersion>(basisVersionId), "Confirmed initial basis"));
        await Post(TestData.Alex, root + $"/design-basis/{basisId}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), basisVersionId, "Deliverable", did, "Submission basis"));
        await f.DbAsync(async db => {
            var package = await db.SubmissionPackages.SingleAsync(p => p.Id == packageId); package.Status = SubmissionStatus.Ready;
            var check = await db.SubmissionChecks.FirstAsync(c => c.PackageId == packageId); check.Status = SubmissionCheckStatus.Pass; check.EvidenceUrl = "https://example.test/evidence-2";
            return await db.SaveChangesAsync();
        });
        await Post(TestData.Marc, root + $"/design-basis/{basisId}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(basisId), Version<DesignBasisVersion>(basisVersionId),
            new DesignBasisEndpoints.VersionInput("Service", "Updated basis", 11, "kPa", "Manual", "basis-2", "https://example.test/basis-2", "B", new DateOnly(2026, 10, 5), null), "Basis changed"));
        var proposedBasisVersionId = f.Db(db => db.DesignBasisVersions.Where(v => v.EntryId == basisId && v.Status == BasisStatus.Proposed).Select(v => v.Id).Single());
        await Post(TestData.Marc, root + $"/design-basis/{basisId}/versions/{proposedBasisVersionId}/confirm",
            new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(basisId), Version<DesignBasisVersion>(proposedBasisVersionId), "Confirmed updated basis"));
        Assert.Equal(SubmissionStatus.Checking, f.Db(db => db.SubmissionPackages.Single(p => p.Id == packageId).Status));
        var fresh = await Get(TestData.Pm, $"{root}/submissions/{packageId}");
        Assert.False(fresh["readiness"]!["ready"]!.GetValue<bool>());
        Assert.Contains("submission.change_pending", fresh["readiness"]!["blockers"]!.ToJsonString());
        var checking = await Get(TestData.Pm, root + "/submissions?status=Checking&page=1&pageSize=1");
        Assert.Equal(1, checking["totalCount"]!.GetValue<int>());
        Assert.Equal(packageId, checking["items"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(0, (await Get(TestData.Pm, root + "/submissions?status=Ready&page=1&pageSize=1"))["totalCount"]!.GetValue<int>());
        await Post(TestData.Pm, $"{root}/submissions/{packageId}/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(),
            fresh["package"]!["rowVersion"]!.GetValue<int>(), 1, fresh["readiness"]!["fingerprint"]!.GetValue<string>(),
            "Municipality", "https://example.test/transmittal", null), 422);
        Assert.Empty(f.Db(db => db.SubmissionIssues.Where(i => i.PackageId == packageId).ToList()));
        var impact = f.Db(db => db.BasisImpactAssessments.Single(i => i.NewVersionId == proposedBasisVersionId && i.Status == AssessmentStatus.Pending));
        var use = f.Db(db => db.BasisUses.Single(u => u.Id == impact.BasisUseId));
        var adopt = new DesignBasisEndpoints.ImpactBody(Guid.NewGuid(), impact.RowVersion, use.RowVersion,
            Version<DesignBasisVersion>(proposedBasisVersionId), Version<Deliverable>(did), "Adopt",
            "Replacement basis reviewed and adopted", "https://example.test/basis-impact-review");
        await Post(TestData.Alex, $"{root}/design-basis/{basisId}/impacts/{impact.Id}/decide", adopt);
        Assert.Equal(AssessmentStatus.Resolved, f.Db(db => db.BasisImpactAssessments.Single(i => i.Id == impact.Id).Status));
        Assert.Equal(proposedBasisVersionId, f.Db(db => db.BasisUses.Where(u => u.TargetId == did)
            .OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).Select(u => u.VersionId).First()));
        var recovered = await Get(TestData.Pm, $"{root}/submissions/{packageId}");
        Assert.True(recovered["readiness"]!["ready"]!.GetValue<bool>());
        var ready = await Get(TestData.Pm, root + "/submissions?status=Ready&page=1&pageSize=1");
        Assert.Equal(1, ready["totalCount"]!.GetValue<int>());
        Assert.Equal(packageId, ready["items"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(0, (await Get(TestData.Pm, root + "/submissions?status=Checking&page=1&pageSize=1"))["totalCount"]!.GetValue<int>());
        var issued = await Post(TestData.Pm, $"{root}/submissions/{packageId}/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(),
            recovered["package"]!["rowVersion"]!.GetValue<int>(), 1, recovered["readiness"]!["fingerprint"]!.GetValue<string>(),
            "Municipality", "https://example.test/transmittal", "Basis impact resolved"));
        Assert.Equal(packageId, issued.G("id"));
        Assert.Equal(SubmissionStatus.Issued, f.Db(db => db.SubmissionPackages.Single(p => p.Id == packageId).Status));
    }
}
