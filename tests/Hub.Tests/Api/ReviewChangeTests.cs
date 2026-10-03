using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;
[Collection("api")]
public sealed class ReviewChangeTests(HubFactory f)
{
    readonly TestData data = new(f);
    sealed record Setup(Project P, Guid Deliverable, Guid Revision, Guid Civil, Guid Electrical);
    string Root(Setup s) => $"/api/v1/projects/{s.P.Id}";
    async Task<JsonNode> Post(string who, string path, object body, int expected = 200) => await (await f.As(who).Post(path, body)).Json(expected);
    async Task<JsonNode> Get(string who, string path) => await (await f.As(who).GetAsync(path)).Json();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task<Setup> New()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var d = await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/deliverables", new { name = "Coordinated survey", projectDisciplineId = civil, deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var s = new Setup(p, d.G("id"), Guid.Empty, civil, electrical);
        var revision = await Post(TestData.Alex, Root(s) + "/source-revisions", Registration(s)); return s with { Revision = revision.G("id") };
    }
    ChangeEndpoints.RegisterBody Registration(Setup s, string revision = "A", Guid? old = null) => new(Guid.NewGuid(), s.Deliverable, Version<Deliverable>(s.Deliverable), s.Civil, data.User(TestData.Alex), "Deliverable", "survey", "Coordinated survey", revision, $"https://example.test/{revision}.pdf", "Survey team", "East corridor", null, old,
        old == null ? null : f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).RowVersion), old == null ? null : "Changed corridor alignment", old == null ? null : new DateOnly(2026, 9, 14), old == null ? null : new DateOnly(2026, 9, 18));
    ReviewEndpoints.Assignment[] Assignments(Setup s) => [new(s.Civil, data.User(TestData.Pm), new(2026, 9, 18)), new(s.Electrical, data.User(TestData.Omar), new(2026, 9, 18))];
    ReviewEndpoints.CreateBody ReviewBody(Setup s) => new(Guid.NewGuid(), "Civil / Electrical interface", "Check clearance and the shared corridor", s.Civil, data.User(TestData.Marc), [s.Revision], Assignments(s), true, null);
    async Task<Guid> Review(Setup s)
    {
        var p = await Post(TestData.Marc, Root(s) + "/reviews", ReviewBody(s)); var id = p.G("id");
        await ReviewAction(s, id, "start"); return id;
    }
    async Task<JsonNode> ReviewAction(Setup s, Guid id, string action, int expected = 200) => await Post(TestData.Marc, Root(s) + $"/reviews/{id}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(id), action, "Confirmed package scope"), expected);
    Guid Assignment(Guid package, Guid discipline) => f.Db(db => db.DisciplineReviews.Single(a => a.ProjectDisciplineId == discipline && db.ReviewPackages.Any(p => p.Id == package && p.CurrentRoundId == a.RoundId)).Id);

    [Fact]
    public async Task Another_discipline_lead_cannot_set_a_deliverable_issue_gate()
    {
        var s = await New();
        var body = ReviewBody(s) with { ProjectDisciplineId = s.Electrical, CoordinatorId = data.User(TestData.Omar) };
        await Post(TestData.Omar, Root(s) + "/reviews", body, 403);
        Assert.Null(f.Db(db => db.Deliverables.Single(d => d.Id == s.Deliverable).RequiredReviewPackageId));
        Assert.False(f.Db(db => db.ReviewPackages.Any(p => p.ProjectId == s.P.Id)));
    }

    [Fact]
    public async Task External_source_supersession_requires_the_existing_head_discipline()
    {
        var s = await New();
        var body = Registration(s) with { DeliverableId = null, DeliverableRowVersion = null, OwnerId = data.User(TestData.Marc), SourceSystem = "External", ExternalIdentifier = "civil-survey" };
        var first = await Post(TestData.Marc, Root(s) + "/source-revisions", body);
        var head = f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id && h.CurrentRevisionId == first.G("id")));
        var next = body with { RequestId = Guid.NewGuid(), Revision = "B", Url = "https://example.test/B.pdf", SupersedesId = first.G("id"), HeadRowVersion = head.RowVersion,
            ProjectDisciplineId = s.Electrical, OwnerId = data.User(TestData.Omar), Description = "Attempt discipline relabelling", EffectiveDate = new(2026, 9, 14), AssessmentDueDate = new(2026, 9, 18) };
        await Post(TestData.Omar, Root(s) + "/source-revisions", next, 403);
        Assert.Equal(first.G("id"), f.Db(db => db.SourceHeads.Single(h => h.Id == head.Id).CurrentRevisionId));
        Assert.False(f.Db(db => db.ChangeNotices.Any(c => c.ProjectId == s.P.Id)));
    }

    [Fact]
    public async Task Coordination_includes_review_assigned_to_other_discipline()
    {
        var setup = await New();
        var packageId = await Review(setup);
        var electrical = await Get(TestData.Omar,
            $"/api/v1/projects/{setup.P.Id}/discipline-coordination?disciplineId={setup.Electrical}");
        Assert.Contains(electrical["reviews"]!.AsArray(), row => row!.G("id") == packageId);
    }

    [Fact]
    public async Task Coordination_shows_cross_discipline_change_to_assessment_owner()
    {
        var setup = await New();
        var target = await Target(setup);
        var deletedTarget = await Target(setup);
        await Adopt(setup, target, setup.Revision, setup.Revision);
        await Adopt(setup, deletedTarget, setup.Revision, setup.Revision);
        var noticeId = await Notice(setup);
        await Publish(setup, noticeId);
        await f.DbAsync(async db => {
            var task = await db.Tasks.SingleAsync(t => t.Id == deletedTarget);
            task.DeletedAt = f.Clock.GetUtcNow();
            await db.SaveChangesAsync(); return 0;
        });
        var meeting = await Post(TestData.Pm, Root(setup) + "/meetings/current", new { });
        var action = await Post(TestData.Pm, $"/api/v1/meetings/{meeting.G("id")}/actions", new {
            text = "Check the revised alignment", ownerType = "User", ownerUserId = data.User(TestData.Omar),
            dueDate = "2026-09-18", relatedTaskId = target,
            links = new[] { new { targetType = ItemType.ChangeNotice, targetId = noticeId } }
        }, 201);

        var electrical = await Get(TestData.Omar,
            $"/api/v1/projects/{setup.P.Id}/discipline-coordination?disciplineId={setup.Electrical}&ownerId={data.User(TestData.Omar)}");
        var notice = Assert.Single(electrical["changes"]!.AsArray());
        Assert.Equal(noticeId, notice!.G("id"));
        Assert.Equal(1, notice["pendingAssessments"]!.GetValue<int>());
        Assert.Single(electrical["changeTargets"]!.AsArray());
        Assert.Equal(target, electrical["changeTargets"]![0]!.G("targetId"));
        var ownerOnly = await Get(TestData.Omar,
            $"/api/v1/projects/{setup.P.Id}/discipline-coordination?ownerId={data.User(TestData.Omar)}");
        Assert.Equal(1, Assert.Single(ownerOnly["unavailableChangeTargets"]!.AsArray())!["count"]!.GetValue<int>());
        var linked = Assert.Single(electrical["linkedActions"]!.AsArray());
        Assert.Equal(action.G("id"), linked!.G("id"));
        Assert.Equal(noticeId, linked.G("sourceId"));
        var civil = await Get(TestData.Alex,
            $"/api/v1/projects/{setup.P.Id}/discipline-coordination?disciplineId={setup.Civil}");
        Assert.Contains(civil["changes"]!.AsArray(), row => row!.G("id") == noticeId);
    }

    [Fact]
    public async Task Review_allocation_options_link_current_assignment_and_explicit_effort()
    {
        var setup = await New();
        var packageId = await Review(setup);
        var assignmentId = Assignment(packageId, setup.Electrical);
        var basePath = Root(setup) + "/allocations";
        var options = await Get(TestData.Pm, basePath + "/review-options");
        Assert.Contains(options.AsArray(), row => row!.G("id") == assignmentId && row.G("packageId") == packageId);
        await (await f.As(TestData.Rita).GetAsync(basePath + "/review-options")).Json(403);
        var date = new DateOnly(2026, 9, 17);
        var allocation = await Post(TestData.Pm, basePath, new AllocationEndpoints.CreateBody(Guid.NewGuid(), data.User(TestData.Omar),
            AllocationPurpose.Review, date, date, 4, [], [new("Review", assignmentId, date, 3)], null));
        var detail = await Get(TestData.Pm, basePath + $"/{allocation.G("id")}");
        Assert.Equal(packageId, detail["links"]![0]!.G("reviewPackageId"));
        Assert.Equal(3, detail["links"]![0]!["reviewHours"]!.GetValue<decimal>());
        var grid = await (await f.As(TestData.Lena).GetAsync("/api/v1/workload")).Json();
        var person = grid["people"]!.AsArray().Single(p => p!.G("id") == data.User(TestData.Omar))!;
        Assert.Equal(4, person["cells"]![0]!["proposed"]!.GetValue<decimal>());
    }
    async Task Decide(Setup s, Guid id, Guid discipline, string who, string state = DisciplineReviewStatus.Approved, int expected = 200)
    {
        var aid = Assignment(id, discipline); await Post(who, Root(s) + $"/reviews/{id}/assignments/{aid}/decision", new ReviewEndpoints.DecisionBody(Guid.NewGuid(), Version<DisciplineReview>(aid), state, "Checked against the registered revision manifest"), expected);
    }
    async Task<Guid> Finding(Setup s, Guid id, string severity = "Blocking", Guid? issueId = null) => (await Post(TestData.Omar, Root(s) + $"/reviews/{id}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(id), s.Revision, s.Electrical, data.User(TestData.Alex), "Verify service clearance", severity, issueId))).G("id");
    async Task FindingAction(Setup s, Guid id, Guid finding, string who, string action, string? evidence = null, Guid? owner = null, int expected = 200) => await Post(who, Root(s) + $"/reviews/{id}/findings/{finding}/action", new ReviewEndpoints.FindingAction(Guid.NewGuid(), Version<ReviewFinding>(finding), action, "Checked and recorded supporting evidence", evidence, owner), expected);
    async Task<Guid> Notice(Setup s, string revision = "B", Guid? old = null)
    { return (await Post(TestData.Alex, Root(s) + "/source-revisions", Registration(s, revision, old ?? s.Revision))).G("id"); }
    async Task Publish(Setup s, Guid notice, ChangeEndpoints.TargetRef[]? targets = null, int expected = 200)
    { await Post(TestData.Alex, Root(s) + $"/changes/{notice}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(notice), f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).RowVersion), targets), expected); }
    async Task<Guid> Target(Setup s, string who = TestData.Omar) => (await data.NewTask(s.P.Id, who, new { assigneeId = data.User(who), dueDate = "2026-09-20" }, who == TestData.Omar ? "Electrical" : "Civil")).G("id");
    async Task Adopt(Setup s, Guid target, Guid revision, Guid expectedHead, string who = TestData.Omar, int expected = 200)
    { await Post(who, Root(s) + "/input-uses", new ChangeEndpoints.AdoptBody(Guid.NewGuid(), "Task", target, Version<WorkTask>(target), revision, expectedHead, f.Db(db => db.InputUses.Where(u => u.TargetId == target).Select(u => (int?)u.RowVersion).FirstOrDefault()), "Coordinate service alignment", "Incorporated into design basis"), expected); }
    Guid Assessment(Guid notice, Guid target) => f.Db(db => db.ChangeAssessments.Single(a => a.ChangeNoticeId == notice && a.TargetId == target).Id);
    async Task Assess(Setup s, Guid notice, Guid aid, string who, string action, string? status = null, Guid? correction = null, Guid? reviewer = null, int expected = 200)
    {
        var a = f.Db(db => db.ChangeAssessments.AsNoTracking().Single(x => x.Id == aid)); var head = f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id));
        await Post(who, Root(s) + $"/changes/{notice}/assessments/{aid}", new ChangeEndpoints.AssessmentBody(Guid.NewGuid(), a.RowVersion, action, status, "Verified the recorded engineering impact", "https://example.test/evidence.pdf", correction, correction == null ? null : Version<WorkTask>(correction.Value), 8, 2, a.OwnerId, reviewer, Version<WorkTask>(a.TargetId), a.InputUseId == null ? null : Version<InputUse>(a.InputUseId.Value), head.CurrentRevisionId), expected);
    }
    async Task Close(Setup s, Guid notice, int expected = 200) => await Post(TestData.Alex, Root(s) + $"/changes/{notice}/action", new ChangeEndpoints.NoticeAction(Guid.NewGuid(), Version<ChangeNotice>(notice), "close", "All dispositions and evidence checked", null), expected);

    [Fact]
    public async Task AC_MRV_01_02_all_disciplines_and_independent_finding_verification_gate_issue()
    {
        var s = await New(); var id = await Review(s); var finding = await Finding(s, id);
        await Decide(s, id, s.Civil, TestData.Marc, expected: 403); // coordinator cannot sign
        await Decide(s, id, s.Civil, TestData.Pm);
        Assert.NotEqual(ReviewStatus.Approved, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/transition", new { rowVersion = Version<Deliverable>(s.Deliverable), toStatus = DeliverableStatus.InProgress });
        await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/transition", new { rowVersion = Version<Deliverable>(s.Deliverable), toStatus = DeliverableStatus.ReadyToIssue }, 400);
        var issue = new { rowVersion = Version<Deliverable>(s.Deliverable), revision = "A", confirmOpenTasks = true };
        await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/issue", issue, 400);
        await Decide(s, id, s.Electrical, TestData.Omar);
        await FindingAction(s, id, finding, TestData.Alex, FindingStatus.Responded, "https://example.test/clearance.pdf");
        Assert.Equal(ReviewStatus.ChangesRequired, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        await FindingAction(s, id, finding, TestData.Alex, FindingStatus.VerifiedClosed, expected: 403);
        await FindingAction(s, id, finding, TestData.Omar, FindingStatus.VerifiedClosed);
        Assert.Equal(ReviewStatus.Approved, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/issue", new { rowVersion = Version<Deliverable>(s.Deliverable), revision = "B" }, 400);
        await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/issue", issue);
        Assert.Equal(DeliverableStatus.Issued, f.Db(db => db.Deliverables.Single(d => d.Id == s.Deliverable).Status));
        Assert.Equal(2, f.Db(db => db.FindingEvents.Count(e => e.FindingId == finding)));
    }
    [Fact]
    public async Task AC_MRV_03_approved_round_is_replaced_on_publication_without_reusing_approvals()
    {
        var s = await New(); var id = await Review(s);
        await Decide(s, id, s.Civil, TestData.Pm);
        await Decide(s, id, s.Electrical, TestData.Omar);
        Assert.Equal(ReviewStatus.Approved, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        var oldRound = f.Db(db => db.ReviewPackages.Single(p => p.Id == id).CurrentRoundId!.Value);

        var notice = await Notice(s); await Publish(s, notice);
        var current = f.Db(db => db.ReviewPackages.Single(p => p.Id == id));
        Assert.Equal(ReviewStatus.Draft, current.Status);
        Assert.Equal(2, current.RoundNumber);
        Assert.Equal(ReviewStatus.Superseded, f.Db(db => db.ReviewRounds.Single(r => r.Id == oldRound).Status));
        Assert.Equal(2, f.Db(db => db.DisciplineReviews.Count(a => a.RoundId == oldRound && a.Status == DisciplineReviewStatus.Approved)));
        Assert.Equal(2, f.Db(db => db.DisciplineReviews.Count(a => a.RoundId == current.CurrentRoundId && a.Status == DisciplineReviewStatus.Pending)));
        var revisionB = f.Db(db => db.ChangeNotices.Single(c => c.Id == notice).NewRevisionId);
        Assert.Equal(revisionB, f.Db(db => db.ReviewManifestItems.Single(m => m.RoundId == current.CurrentRoundId).SourceRevisionId));
    }

    [Fact]
    public async Task AC_MRV_03_05_new_revision_resets_decisions_and_carries_findings_without_erasing_history()
    {
        var s = await New(); var id = await Review(s); var finding = await Finding(s, id);
        await Decide(s, id, s.Civil, TestData.Pm); var oldRound = f.Db(db => db.ReviewPackages.Single(p => p.Id == id).CurrentRoundId!.Value);
        var notice = await Notice(s); await Publish(s, notice); var revisionB = f.Db(db => db.ChangeNotices.Single(c => c.Id == notice).NewRevisionId);
        Assert.Equal(ReviewStatus.Superseded, f.Db(db => db.ReviewRounds.Single(r => r.Id == oldRound).Status));
        Assert.Equal(revisionB, f.Db(db => db.ReviewManifestItems.Single(m => m.RoundId == db.ReviewPackages.Single(p => p.Id == id).CurrentRoundId).SourceRevisionId));
        await Decide(s, id, s.Electrical, TestData.Omar, expected: 400); // New round needs an explicit start and fresh decisions.
        var detail = await Get(TestData.Omar, Root(s) + $"/reviews/{id}");
        Assert.Equal(2, detail["rounds"]!.AsArray().Count); Assert.Equal("Draft", detail["package"]!.S("status"));
        Assert.True(f.Db(db => db.DisciplineReviews.Where(a => a.RoundId == oldRound).Any(a => a.Status == DisciplineReviewStatus.Approved)));
        Assert.Equal(2, f.Db(db => db.DisciplineReviews.Count(a => a.RoundId != oldRound && db.ReviewRounds.Any(r => r.Id == a.RoundId && r.PackageId == id) && a.Status == DisciplineReviewStatus.Pending)));
        Assert.True(f.Db(db => db.ReviewFindings.Any(x => x.CarriedFromId == finding && x.SourceRevisionId == revisionB && x.Status == FindingStatus.Open)));
        await FindingAction(s, id, finding, TestData.Alex, FindingStatus.Responded, "https://example.test/fixed.pdf", expected: 404);
        var round = new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(id), "Updated corridor check", [revisionB], Assignments(s), "Scope update", null);
        var removal = round with { RequestId = Guid.NewGuid(), RowVersion = Version<ReviewPackage>(id), Assignments = [Assignments(s)[0]], RemovalImpact = "Electrical finding remains assigned and blocking" };
        await Post(TestData.Marc, Root(s) + $"/reviews/{id}/rounds", removal, 403);
        await Post(TestData.Pm, Root(s) + $"/reviews/{id}/rounds", removal with { RemovalImpact = null }, 400);
        await Post(TestData.Pm, Root(s) + $"/reviews/{id}/rounds", removal);
        Assert.Equal(3, f.Db(db => db.ReviewRounds.Count(r => r.PackageId == id)));
        Assert.Equal(3, f.Db(db => db.ReviewFindings.Count(x => x.PackageId == id)));
    }

    [Fact]
    public async Task AC_LOC_04_linked_review_and_discipline_view_share_one_issue_and_owner_audit()
    {
        var s = await New();
        var issue = await Post(TestData.Alex, Root(s) + "/issues", new { title = "Shared service clearance", severity = "High", ownerId = data.User(TestData.Alex), projectDisciplineId = s.Civil }, 201);
        var issueId = issue.G("id");
        var reviewId = await Review(s);
        var otherProject = await data.Project();
        var otherIssue = await Post(TestData.Pm, $"/api/v1/projects/{otherProject.Id}/issues", new { title = "Other project issue", severity = "High", ownerId = data.User(TestData.Pm) }, 201);
        var linkBody = new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(reviewId), s.Revision, s.Electrical,
            data.User(TestData.Alex), "Verify service clearance", "Blocking", otherIssue.G("id"));
        await Post(TestData.Omar, Root(s) + $"/reviews/{reviewId}/findings", linkBody, 400);
        await Post(TestData.Rita, Root(s) + $"/reviews/{reviewId}/findings", linkBody with { IssueId = issueId, RequestId = Guid.NewGuid() }, 403);
        var findingId = await Finding(s, reviewId, issueId: issueId);
        var detail = await Get(TestData.Omar, Root(s) + $"/reviews/{reviewId}");
        Assert.Equal(issueId, detail["findings"]!.AsArray().Single(x => x!.G("id") == findingId)!.G("issueId"));
        var linked = await Get(TestData.Omar, Root(s) + $"/reviews/linked-issues?disciplineId={s.Electrical}");
        Assert.Equal(issueId, linked["items"]!.AsArray().Single()!.G("id"));
        Assert.Equal(1, linked.I("totalCount"));
        Assert.Equal(1, f.Db(db => db.Issues.Count(i => i.Id == issueId)));
        var before = f.Db(db => db.ActivityLog.Count(a => a.ItemId == issueId && a.ItemType == "Issue"));
        var oldVersion = Version<Issue>(issueId);
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{issueId}", new { ownerId = data.User(TestData.Pm) }, oldVersion)).EnsureSuccessStatusCode();
        Assert.Equal(System.Net.HttpStatusCode.Conflict, (await f.As(TestData.Pm).Patch($"/api/v1/issues/{issueId}", new { ownerId = data.User(TestData.Marc) }, oldVersion)).StatusCode);
        linked = await Get(TestData.Omar, Root(s) + $"/reviews/linked-issues?disciplineId={s.Electrical}");
        Assert.Equal(data.User(TestData.Pm), linked["items"]!.AsArray().Single()!.G("ownerId"));
        Assert.Equal(before + 1, f.Db(db => db.ActivityLog.Count(a => a.ItemId == issueId && a.ItemType == "Issue")));
        Assert.Equal(1, f.Db(db => db.Issues.Count(i => i.Id == issueId)));
        var notice = await Notice(s); await Publish(s, notice);
        var currentRound = f.Db(db => db.ReviewPackages.Single(p => p.Id == reviewId).CurrentRoundId);
        Assert.Equal(issueId, f.Db(db => db.ReviewFindings.Single(x => x.RoundId == currentRound && x.CarriedFromId == findingId).IssueId));
    }

    [Fact]
    public async Task AC_LOC_03_published_source_revision_creates_two_party_impact_for_closed_issue()
    {
        var s = await New();
        var issue = await Post(TestData.Alex, Root(s) + "/issues", new { title = "Closed source reference", severity = "High", ownerId = data.User(TestData.Alex), projectDisciplineId = s.Civil }, 201);
        var issueId = issue.G("id");
        int IssueVersion() => f.Db(db => db.Issues.Single(i => i.Id == issueId).RowVersion);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/documents", new { kind = "Drawing", identifier = "survey", revision = "A", sourceUrl = "https://example.test/A.pdf", isAvailable = true, rowVersion = IssueVersion() }, 201);
        await Post(TestData.Pm, $"/api/v1/issues/{issueId}/verification", new { verifierId = data.User(TestData.Marc), status = "Proposed", note = "Appoint independent verifier", rowVersion = IssueVersion() }, 201);
        await Post(TestData.Marc, $"/api/v1/issues/{issueId}/verification", new { verifierId = data.User(TestData.Marc), status = "Verified", evidenceUrl = "https://example.test/evidence", rowVersion = IssueVersion() }, 201);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/transition", new { toStatus = "Resolved", resolution = "Closed against revision A", rowVersion = IssueVersion() });

        var unrelated = await Post(TestData.Alex, Root(s) + "/issues", new { title = "Different source with same drawing number", severity = "High", ownerId = data.User(TestData.Alex), projectDisciplineId = s.Civil }, 201);
        var unrelatedId = unrelated.G("id");
        int UnrelatedVersion() => f.Db(db => db.Issues.Single(i => i.Id == unrelatedId).RowVersion);
        await Post(TestData.Alex, $"/api/v1/issues/{unrelatedId}/documents", new { kind = "Drawing", identifier = "survey", revision = "A", sourceUrl = "https://different.example.test/A.pdf", isAvailable = true, rowVersion = UnrelatedVersion() }, 201);
        await Post(TestData.Pm, $"/api/v1/issues/{unrelatedId}/verification", new { verifierId = data.User(TestData.Marc), status = "Proposed", note = "Appoint independent verifier", rowVersion = UnrelatedVersion() }, 201);
        await Post(TestData.Marc, $"/api/v1/issues/{unrelatedId}/verification", new { verifierId = data.User(TestData.Marc), status = "Verified", evidenceUrl = "https://example.test/evidence", rowVersion = UnrelatedVersion() }, 201);
        await Post(TestData.Alex, $"/api/v1/issues/{unrelatedId}/transition", new { toStatus = "Resolved", resolution = "Different source", rowVersion = UnrelatedVersion() });

        var notice = await Notice(s);
        await Publish(s, notice);
        Assert.Empty((await Get(TestData.Alex, $"/api/v1/issues/{unrelatedId}/reference-impacts")).AsArray());
        var impacts = await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts");
        Assert.Single(impacts.AsArray());
        Assert.Equal("Pending", impacts.AsArray()[0]!.S("status"));
        var impactId = impacts.AsArray()[0]!.G("id");
        var ownerDisposition = new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), impacts.AsArray()[0]!.I("rowVersion"), "Unaffected", "Owner reviewed superseding revision");
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts/{impactId}", ownerDisposition);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts/{impactId}", ownerDisposition);
        var pending = await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts");
        Assert.Equal("Pending", pending.AsArray()[0]!.S("status"));
        Assert.Equal("Unaffected", pending.AsArray()[0]!.S("ownerDisposition"));
        await Post(TestData.Omar, $"/api/v1/issues/{issueId}/reference-impacts/{impactId}",
            new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), pending.AsArray()[0]!.I("rowVersion"), "Unaffected", "Unrelated member"), 403);
        (await f.As(TestData.Pm).Patch($"/api/v1/issues/{issueId}", new { ownerId = data.User(TestData.Omar) }, IssueVersion())).EnsureSuccessStatusCode();
        var reassigned = await Get(TestData.Omar, $"/api/v1/issues/{issueId}/reference-impacts");
        Assert.Equal(data.User(TestData.Omar), reassigned.AsArray()[0]!.G("ownerId"));
        Assert.Null(reassigned.AsArray()[0]!["ownerDisposition"]);
        await Post(TestData.Omar, $"/api/v1/issues/{issueId}/reference-impacts/{impactId}",
            new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), reassigned.AsArray()[0]!.I("rowVersion"), "Unaffected", "New owner reviewed superseding revision"));
        pending = await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts");
        var verifierDisposition = new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), pending.AsArray()[0]!.I("rowVersion"), "Unaffected", "Independent verifier reviewed superseding revision");
        await Post(TestData.Marc, $"/api/v1/issues/{issueId}/reference-impacts/{impactId}", verifierDisposition);
        var final = await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts");
        Assert.Equal("Unaffected", final.AsArray()[0]!.S("status"));
        Assert.Equal("A", f.Db(db => db.IssueDocumentReferences.Single(d => d.IssueId == issueId).Revision));
    }
    [Fact]
    public async Task General_issue_without_a_verifier_settles_its_reference_impact_on_the_owner_decision()
    {
        var s = await New();
        var issue = await Post(TestData.Alex, Root(s) + "/issues", new { title = "General source reference", severity = "Low", ownerId = data.User(TestData.Alex), projectDisciplineId = s.Civil }, 201);
        var issueId = issue.G("id");
        int IssueVersion() => f.Db(db => db.Issues.Single(i => i.Id == issueId).RowVersion);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/documents", new { kind = "Drawing", identifier = "survey", revision = "A", sourceUrl = "https://example.test/A.pdf", isAvailable = true, rowVersion = IssueVersion() }, 201);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/transition", new { toStatus = "Resolved", resolution = "Closed without coordination verification", rowVersion = IssueVersion() });
        await Publish(s, await Notice(s));
        var impact = Assert.Single((await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts")).AsArray())!;
        Assert.Null(impact["verifierId"]);
        // FR-LOC-04: with no verifier the owner's decision settles the check, so the existing reopen workflow is not blocked forever.
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts/{impact.G("id")}",
            new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), impact.I("rowVersion"), "Reopen", "Revision B moves the corridor"));
        Assert.Equal("ReopenRequested", Assert.Single((await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts")).AsArray())!.S("status"));
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/transition", new { toStatus = "In Progress", reason = "Revision B moves the corridor", rowVersion = IssueVersion() });
        Assert.Equal(IssueStatus.InProgress, f.Db(db => db.Issues.Single(i => i.Id == issueId).Status));
    }
    [Fact]
    public async Task Named_owner_and_verifier_removed_from_the_team_cannot_decide_a_reference_impact()
    {
        var s = await New();
        var issue = await Post(TestData.Alex, Root(s) + "/issues", new { title = "Reference held by leavers", severity = "High", ownerId = data.User(TestData.Alex), projectDisciplineId = s.Civil }, 201);
        var issueId = issue.G("id");
        int IssueVersion() => f.Db(db => db.Issues.Single(i => i.Id == issueId).RowVersion);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/documents", new { kind = "Drawing", identifier = "survey", revision = "A", sourceUrl = "https://example.test/A.pdf", isAvailable = true, rowVersion = IssueVersion() }, 201);
        await Post(TestData.Pm, $"/api/v1/issues/{issueId}/verification", new { verifierId = data.User(TestData.Marc), status = "Proposed", note = "Appoint independent verifier", rowVersion = IssueVersion() }, 201);
        await Post(TestData.Marc, $"/api/v1/issues/{issueId}/verification", new { verifierId = data.User(TestData.Marc), status = "Verified", evidenceUrl = "https://example.test/evidence", rowVersion = IssueVersion() }, 201);
        await Post(TestData.Alex, $"/api/v1/issues/{issueId}/transition", new { toStatus = "Resolved", resolution = "Closed against revision A", rowVersion = IssueVersion() });
        await Publish(s, await Notice(s));
        var impact = Assert.Single((await Get(TestData.Alex, $"/api/v1/issues/{issueId}/reference-impacts")).AsArray())!;
        // The owner and the verifier leave the team (the Civil lead's lead role goes with his membership); the project stays open to them.
        foreach (var who in new[] { TestData.Alex, TestData.Marc })
        {
            var member = f.Db(db => db.ProjectMembers.Single(m => m.ProjectId == s.P.Id && m.UserId == data.User(who) && m.RemovedAt == null).Id);
            (await f.As(TestData.Pm).DeleteAsync($"{Root(s)}/members/{member}?reason=Left%20the%20team")).EnsureSuccessStatusCode();
        }
        foreach (var who in new[] { TestData.Alex, TestData.Marc })
            await Post(who, $"/api/v1/issues/{issueId}/reference-impacts/{impact.G("id")}",
                new ChangeEndpoints.IssueImpactBody(Guid.NewGuid(), impact.I("rowVersion"), "Unaffected", "Decided after leaving the team"), 403);
        var after = Assert.Single((await Get(TestData.Pm, $"/api/v1/issues/{issueId}/reference-impacts")).AsArray())!;
        Assert.Null(after["ownerDisposition"]);
        Assert.Null(after["verifierDisposition"]);
    }
    [Fact]
    public async Task Removing_a_manifest_deliverable_requires_impact_review_and_releases_its_issue_gate()
    {
        var s = await New();
        var second = await Post(TestData.Marc, Root(s) + "/deliverables", new {
            name = "Second survey", projectDisciplineId = s.Civil, deliverableTypeId = await data.DeliverableType(),
            ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201);
        var secondId = second.G("id");
        var secondRevision = await Post(TestData.Alex, Root(s) + "/source-revisions", Registration(s with { Deliverable = secondId }));
        var created = await Post(TestData.Marc, Root(s) + "/reviews", ReviewBody(s) with {
            SourceRevisionIds = [s.Revision, secondRevision.G("id")] });
        var id = created.G("id");
        await ReviewAction(s, id, "start");

        var round = new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(id), "One survey remains",
            [s.Revision], Assignments(s), "Second survey left the review scope", null);
        await Post(TestData.Marc, Root(s) + $"/reviews/{id}/rounds", round, 403);
        await Post(TestData.Pm, Root(s) + $"/reviews/{id}/rounds", round with { RequestId = Guid.NewGuid() }, 400);
        await Post(TestData.Pm, Root(s) + $"/reviews/{id}/rounds", round with {
            RequestId = Guid.NewGuid(), RemovalImpact = "Second survey now has a separate issue path" });

        Assert.Null(f.Db(db => db.Deliverables.Single(d => d.Id == secondId).RequiredReviewPackageId));
        Assert.Equal(id, f.Db(db => db.Deliverables.Single(d => d.Id == s.Deliverable).RequiredReviewPackageId));
    }
    [Fact]
    public async Task AC_MRV_04_authorship_and_reviewer_reassignment_cannot_bypass_independence()
    {
        var s = await New(); var bad = ReviewBody(s) with { Assignments = [new(s.Civil, data.User(TestData.Alex), new(2026, 9, 18))] };
        await Post(TestData.Marc, Root(s) + "/reviews", bad, 400);
        Assert.False(f.Db(db => db.ReviewPackages.Any(p => p.ProjectId == s.P.Id)));
        var id = await Review(s); var aid = Assignment(id, s.Electrical);
        await Post(TestData.Marc, Root(s) + $"/reviews/{id}/assignments/{aid}/assign", new ReviewEndpoints.ReassignBody(Guid.NewGuid(), Version<DisciplineReview>(aid), data.User(TestData.Alex), "Transfer this review"), 400);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{s.Deliverable}", new { ownerId = data.User(TestData.Omar), rowVersion = Version<Deliverable>(s.Deliverable) })).Json(400);
        Assert.Equal(data.User(TestData.Alex), f.Db(db => db.Deliverables.Single(x => x.Id == s.Deliverable).OwnerId));
        await data.NewTask(s.P.Id, TestData.Marc, new { assigneeId = data.User(TestData.Alex), deliverableId = s.Deliverable });
        var tasks = f.Db(db => db.Tasks.Where(t => t.DeliverableId == s.Deliverable).Select(t => t.Id).ToArray());
        await Post(TestData.Pm, $"/api/v1/projects/{s.P.Id}/tasks/bulk", new { taskIds = tasks, operation = "assign", @params = new { assigneeId = data.User(TestData.Omar) } }, 400);
        Assert.All(f.Db(db => db.Tasks.Where(t => tasks.Contains(t.Id)).Select(t => t.AssigneeId).ToList()), x => Assert.Equal(data.User(TestData.Alex), x));
    }
    [Fact]
    public async Task Reassigning_a_resolver_does_not_make_their_old_response_independent_evidence()
    {
        var s = await New(); var id = await Review(s); var finding = await Finding(s, id);
        await FindingAction(s, id, finding, TestData.Marc, "assignResolver", owner: data.User(TestData.Pm));
        await FindingAction(s, id, finding, TestData.Pm, FindingStatus.Responded, "https://example.test/prior-response.pdf");
        await FindingAction(s, id, finding, TestData.Marc, "assignResolver", owner: data.User(TestData.Alex));
        var row = f.Db(db => db.ReviewFindings.Single(f => f.Id == finding));
        Assert.Equal(FindingStatus.Open, row.Status); Assert.Null(row.Response); Assert.Null(row.EvidenceUrl);
        await FindingAction(s, id, finding, TestData.Pm, "assignVerifier", owner: data.User(TestData.Pm), expected: 400);
        var responder = data.User(TestData.Pm);
        Assert.True(f.Db(db => db.FindingEvents.Any(e => e.FindingId == finding && e.Action == FindingStatus.Responded && e.CreatedBy == responder)));
    }

    [Fact]
    public async Task Withdrawal_needs_named_verifier_then_coordinator_ack_and_has_history()
    {
        var s = await New(); var id = await Review(s); var finding = await Finding(s, id);
        await Decide(s, id, s.Civil, TestData.Pm); await Decide(s, id, s.Electrical, TestData.Omar);
        await FindingAction(s, id, finding, TestData.Marc, FindingStatus.Withdrawn, expected: 403);
        await FindingAction(s, id, finding, TestData.Omar, FindingStatus.Withdrawn);
        Assert.Equal(ReviewStatus.ChangesRequired, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        await FindingAction(s, id, finding, TestData.Marc, "acknowledgeWithdrawal");
        Assert.Equal(ReviewStatus.Approved, f.Db(db => db.ReviewPackages.Single(p => p.Id == id).Status));
        Assert.Equal(2, f.Db(db => db.FindingEvents.Count(e => e.FindingId == finding)));
    }
    [Fact]
    public async Task AC_CHG_01_02_03_only_linked_targets_assessed_ack_is_not_disposition_and_retention_is_explicit()
    {
        var s = await New(); var targets = new[] { await Target(s), await Target(s), await Target(s) }; var unrelated = await Target(s);
        foreach (var t in targets) await Adopt(s, t, s.Revision, s.Revision);
        var notice = await Notice(s); Assert.Equal(s.Revision, f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).CurrentRevisionId));
        await Publish(s, notice, [new("Task", targets[0], Version<WorkTask>(targets[0]))]); // explicit target is deduplicated
        Assert.Equal(3, f.Db(db => db.ChangeAssessments.Count(a => a.ChangeNoticeId == notice)));
        Assert.False(f.Db(db => db.ChangeAssessments.Any(a => a.TargetId == unrelated)));
        var a1 = Assessment(notice, targets[0]); await Assess(s, notice, a1, TestData.Omar, "acknowledge");
        Assert.Equal(AssessmentStatus.Pending, f.Db(db => db.ChangeAssessments.Single(a => a.Id == a1).Status)); await Close(s, notice, 400);
        foreach (var t in targets) {
            var aid = Assessment(notice, t); await Assess(s, notice, aid, TestData.Omar, "disposition", AssessmentStatus.Unaffected);
            await Assess(s, notice, aid, TestData.Omar, "approveRetention", expected: 400); // owner may not approve their own retention
            await Assess(s, notice, aid, TestData.Pm, "approveRetention");
        }
        await Close(s, notice);
        Assert.All(f.Db(db => db.InputUses.Where(u => u.ProjectId == s.P.Id).ToList()), u => Assert.Equal(s.Revision, u.SourceRevisionId));
        Assert.NotEqual(s.Revision, f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).CurrentRevisionId));
        Assert.All(f.Db(db => db.Tasks.Where(t => targets.Contains(t.Id)).ToList()), t => { Assert.Equal(TaskStatuses.NotStarted, t.Status); Assert.Equal(new DateOnly(2026, 9, 20), t.DueDate); });
    }
    [Fact]
    public async Task AC_CHG_04_concurrent_retries_and_stale_head_cannot_mix_publication_or_adoption()
    {
        var s = await New(); var t = await Target(s); await Adopt(s, t, s.Revision, s.Revision); var notice = await Notice(s);
        var body = new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(notice), f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).RowVersion), null);
        var results = await Task.WhenAll(Post(TestData.Alex, Root(s) + $"/changes/{notice}/publish", body), Post(TestData.Alex, Root(s) + $"/changes/{notice}/publish", body));
        Assert.Equal(results[0].ToJsonString(), results[1].ToJsonString()); Assert.Single(f.Db(db => db.ChangeAssessments.Where(a => a.ChangeNoticeId == notice).ToList()));
        var current = f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.P.Id).CurrentRevisionId);
        await Adopt(s, t, current, s.Revision, expected: 409);
        Assert.Equal(s.Revision, f.Db(db => db.InputUses.Single(u => u.TargetId == t).SourceRevisionId));
        await Post(TestData.Alex, Root(s) + $"/changes/{notice}/publish", body with { AdditionalTargets = [] }, 422); // request ID cannot change meaning
        var stale = Registration(s, "C", current); var c1 = await Post(TestData.Alex, Root(s) + "/source-revisions", stale); var c2 = await Post(TestData.Alex, Root(s) + "/source-revisions", stale with { RequestId = Guid.NewGuid(), Revision = "AA", Url = "https://example.test/AA.pdf" });
        await Publish(s, c1.G("id")); await Publish(s, c2.G("id"), expected: 409);
        Assert.Equal(ChangeStatus.Draft, f.Db(db => db.ChangeNotices.Single(c => c.Id == c2.G("id")).Status));
    }
    [Fact]
    public async Task AC_CHG_05_update_needs_completed_correction_and_independent_verification()
    {
        var s = await New(); var t = await Target(s); await Adopt(s, t, s.Revision, s.Revision); var notice = await Notice(s); await Publish(s, notice);
        var aid = Assessment(notice, t); var wrongOwnerCorrection = await Target(s, TestData.Alex);
        await Assess(s, notice, aid, TestData.Omar, "disposition", AssessmentStatus.UpdateRequired, wrongOwnerCorrection, expected: 400);
        var correction = await Target(s);
        await Assess(s, notice, aid, TestData.Omar, "disposition", AssessmentStatus.UpdateRequired, correction);
        await Assess(s, notice, aid, TestData.Omar, "adopt");
        await Assess(s, notice, aid, TestData.Pm, "resolve", correction: correction, expected: 400); await Close(s, notice, 400);
        // Complete through the existing task workflow; new change commands never mutate task completion or dates.
        await data.Move(TestData.Omar, new JsonObject { ["id"] = correction.ToString() }, TaskStatuses.InProgress);
        await data.Move(TestData.Omar, new JsonObject { ["id"] = correction.ToString() }, TaskStatuses.Complete);
        await Assess(s, notice, aid, TestData.Omar, "resolve", correction: correction, expected: 403);
        await Assess(s, notice, aid, TestData.Pm, "resolve", correction: correction); await Close(s, notice);
        Assert.Equal(AssessmentStatus.Resolved, f.Db(db => db.ChangeAssessments.Single(a => a.Id == aid).Status));
        Assert.Equal(new DateOnly(2026, 9, 20), f.Db(db => db.Tasks.Single(x => x.Id == t).DueDate));
        Assert.Equal(2, f.Db(db => db.InputAdoptions.Count(a => db.InputUses.Any(u => u.Id == a.InputUseId && u.TargetId == t))));
    }
    [Fact]
    public async Task Review_and_change_surfaces_are_scoped_searchable_exportable_and_audited()
    {
        var s = await New(); var review = await Review(s); var notice = await Notice(s); await Publish(s, notice);
        await f.DbAsync(async db => { var p = await db.Projects.SingleAsync(p => p.Id == s.P.Id); p.Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        foreach (var path in new[] { "/reviews", $"/reviews/{review}", "/reviews/options", "/reviews/export?format=csv", "/changes", $"/changes/{notice}", "/changes/options", "/input-uses", "/changes/export?format=csv" })
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Diane).GetAsync(Root(s) + path)).StatusCode);
        foreach (var pair in new[] { ("reviews", review), ("changes", notice) }) {
            var list = await Get(TestData.Pm, Root(s) + $"/{pair.Item1}?mine=true&page=1&pageSize=1"); Assert.Equal(1, list.I("pageSize"));
            var csv = await f.As(TestData.Pm).GetAsync(Root(s) + $"/{pair.Item1}/export?format=csv"); Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        }
        var key = f.Db(db => db.ReviewPackages.Single(p => p.Id == review).Key);
        Assert.Equal("ReviewPackage", (await Get(TestData.Pm, "/api/v1/search?q=" + key))["exact"]!.S("type"));
        Assert.Null((await Get(TestData.Diane, "/api/v1/search?q=" + key))["exact"]);
        Assert.True(f.Db(db => db.ActivityLog.Any(a => a.ProjectId == s.P.Id && a.ItemType == "ReviewPackage")));
        Assert.True(f.Db(db => db.ActivityLog.Any(a => a.ProjectId == s.P.Id && a.ItemType == "SourceHead")));
        var draft = ReviewBody(s) with { RequestId = Guid.NewGuid() };
        await Post(TestData.Rita, Root(s) + "/reviews", draft, 404);
        await f.DbAsync(async db => { var p = await db.Projects.SingleAsync(p => p.Id == s.P.Id); p.Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        await ReviewAction(s, review, "cancel", 403);
        Assert.Equal(4, (await Get(TestData.Pm, Root(s) + $"/reviews/{review}"))["assignments"]!.AsArray().Count);
    }
}
