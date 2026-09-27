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
    async Task<Guid> Finding(Setup s, Guid id, string severity = "Blocking") => (await Post(TestData.Omar, Root(s) + $"/reviews/{id}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(id), s.Revision, s.Electrical, data.User(TestData.Alex), "Verify service clearance", severity))).G("id");
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
        var aid = Assessment(notice, t); var correction = await Target(s);
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
