using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// These four partial files share the existing real-command fixture, not a second review/permission engine.
public sealed partial class ExternalReviewTests : IDisposable
{
    readonly ReviewResponseFixture t = new();
    HubFactory F => t.F;
    TestData Data => t.Data;
    const string ResolverEvidence = "https://example.test/external-resolution.pdf";
    const string VerifierEvidence = "https://example.test/external-verification.pdf";
    const string PartyEmail = "private-external-party@example.test";
    const string PartyNotes = "PRIVATE EXTERNAL PARTY NOTES";
    sealed record Scenario(Project Project, Guid Deliverable, Guid Issue, Guid Package, Guid Round,
        Guid Civil, Guid Electrical, Guid Party, Guid Code);
    public void Dispose() => t.Dispose();
    string Root(Scenario s) => $"/api/v1/projects/{s.Project.Id}";
    string PackagePath(Scenario s) => Root(s) + $"/reviews/{s.Package}";
    string RecordPath(Scenario s) => PackagePath(s) + "/external-reviews";
    int V<T>(Guid id) where T : Entity, IVersioned => F.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
    Task Flag(bool value = true) => t.Flag(value);
    async Task SelfReview(bool value) => (await F.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value })).EnsureSuccessStatusCode();
    Task<JsonNode> Post(string who, string path, object body, int status = 200) => t.Post(who, path, body, status);
    async Task<JsonNode> Detail(Scenario s, string who = TestData.Pm) => await (await F.As(who).GetAsync(PackagePath(s))).Json();
    async Task<Guid> Ref(string list, string field, string value) =>
        (await (await F.As(TestData.Admin).GetAsync("/api/v1/reference")).Json())[list]!.AsArray()
            .Single(row => row!.S(field) == value)!.G("id");
    Guid[] Authors(Guid issue) => F.Db(db => db.DeliverableIssues.AsNoTracking().Single(row => row.Id == issue).IssuedAuthorIds)!;
    ReviewEndpoints.Assignment[] Assignments(Scenario s) => [
        new(s.Civil, Data.User(TestData.Pm), new(2026, 11, 12)), new(s.Electrical, Data.User(TestData.Omar), new(2026, 11, 12)),
    ];

    async Task<Guid> RealIssue(Guid deliverable, bool presentationOn, string? revision = "A")
    {
        await Post(TestData.Marc, $"/api/v1/deliverables/{deliverable}/issue", new {
            rowVersion = V<Deliverable>(deliverable), revision, issuedDate = "2026-10-30", issuedTo = "Synthetic external recipient",
            purposeOfIssueId = presentationOn ? await Ref("issuePurposes", "name", "For review") : (Guid?)null,
            confirmOpenTasks = true,
        });
        return F.Db(db => db.Deliverables.Single(row => row.Id == deliverable).CurrentIssueId!.Value);
    }

    async Task<Guid> AnotherIssue(Scenario s, string? revision = "A")
    {
        var deliverable = (await Post(TestData.Marc, Root(s) + "/deliverables", new {
            name = "Actual second issued scope", projectDisciplineId = s.Civil, deliverableTypeId = await Data.DeliverableType(),
            ownerId = Data.User(TestData.Alex), requiresReview = false,
        }, 201)).G("id");
        return await RealIssue(deliverable, true, revision);
    }

    async Task MakeReviewer(Scenario s, string who)
    {
        var user = Data.User(who);
        var member = F.Db(db => db.ProjectMembers.AsNoTracking().SingleOrDefault(row => row.ProjectId == s.Project.Id && row.UserId == user && row.RemovedAt == null));
        if (member is null) await Post(TestData.Pm, Root(s) + "/members", new {
            userId = user, roles = new[] { ProjectRole.Reviewer }, primaryDisciplineId = (Guid?)null,
        }, 201);
        else if (!member.Roles.Contains(ProjectRole.Reviewer))
            (await F.As(TestData.Pm).Patch(Root(s) + $"/members/{member.Id}", new {
                roles = member.Roles.Append(ProjectRole.Reviewer).Distinct().ToArray(), primaryDisciplineId = member.PrimaryDisciplineId,
            }, member.RowVersion)).EnsureSuccessStatusCode();
    }

    async Task<Scenario> New(bool presentationOn = true, bool package = true, bool start = true, string partyName = "Synthetic external reviewer")
    {
        await Flag(presentationOn); var project = await Data.Project();
        var civil = Data.ProjectDiscipline(project.Id, "Civil"); var electrical = Data.ProjectDiscipline(project.Id, "Electrical");
        await Post(TestData.Pm, $"/api/v1/projects/{project.Id}/members", new {
            userId = Data.User(TestData.Jill), roles = new[] { ProjectRole.TeamMember, ProjectRole.Reviewer }, primaryDisciplineId = civil,
        }, 201);
        var deliverable = (await Post(TestData.Marc, $"/api/v1/projects/{project.Id}/deliverables", new {
            name = "Synthetic issued external drawing", projectDisciplineId = civil, deliverableTypeId = await Data.DeliverableType(),
            ownerId = Data.User(TestData.Alex), requiresReview = false,
        }, 201)).G("id");
        var issue = await RealIssue(deliverable, presentationOn);
        var party = (await Post(TestData.Pm, $"/api/v1/projects/{project.Id}/external-parties", new {
            name = partyName, organisation = "Synthetic external organisation", email = PartyEmail, notes = PartyNotes, isClient = true,
        }, 201)).G("id");
        // The feature-OFF picker intentionally omits return codes. Retain an actual seeded metadata ID privately;
        // do not manufacture a code or use TestData's cached OFF reference after the test enables the feature.
        var code = presentationOn ? await Ref("issueReturnCodes", "code", "C")
            : F.Db(db => db.IssueReturnCodes.AsNoTracking().Single(row => row.Code == "C" && row.IsActive).Id);
        var s = new Scenario(project, deliverable, issue, Guid.Empty, Guid.Empty, civil, electrical, party, code);
        return package ? await WithPackage(s, start: start) : s;
    }

    ReviewEndpoints.CreateBody CreateBody(Scenario s, Guid[]? issues = null, Guid? request = null, bool required = false) => new(
        request ?? Guid.NewGuid(), "Synthetic actual issued review", "Review the real recorded issued revision", s.Civil,
        Data.User(TestData.Marc), [], Assignments(s), required, "Supplementary external review, not pre-issue approval", issues ?? [s.Issue]);
    async Task<Scenario> WithPackage(Scenario s, bool start = true)
    {
        var package = (await Post(TestData.Marc, Root(s) + "/reviews", CreateBody(s))).G("id");
        s = s with { Package = package, Round = F.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value) };
        if (start) await Start(s);
        return s;
    }
    Task<JsonNode> Start(Scenario s) => Post(TestData.Marc, PackagePath(s) + "/action",
        new ReviewEndpoints.ActionBody(Guid.NewGuid(), V<ReviewPackage>(s.Package), "start", "Start the actual issued review round"));
    ExternalReviewRecordBody Body(Scenario s, string text = "Check the actual issued service clearance", Guid? request = null) => new(
        request ?? Guid.NewGuid(), V<ReviewPackage>(s.Package), s.Issue, V<DeliverableIssue>(s.Issue), s.Party, V<ExternalParty>(s.Party),
        new(2026, 11, 1), s.Code, V<IssueReturnCode>(s.Code), "Transcribed the actual received external comments", [
            new(text, s.Civil, Data.User(TestData.Alex), "Blocking"),
            new("Confirm the recorded synthetic annotation", s.Electrical, Data.User(TestData.Alex), "Advisory"),
        ]);
    async Task<JsonNode> Record(Scenario s, ExternalReviewRecordBody? body = null, string who = TestData.Jill, int status = 200)
        => await Post(who, RecordPath(s), body ?? Body(s), status);
    ReviewFinding[] Findings(Scenario s) => F.Db(db => db.ReviewFindings.AsNoTracking().Where(row => row.PackageId == s.Package).OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToArray());
    Task<JsonNode> Act(Scenario s, Guid finding, string action, string who, Guid? owner = null, string? evidence = ResolverEvidence,
        string reason = "Recorded actual synthetic review action", string? outcome = null, int status = 200, Guid? request = null, int? version = null)
        => Post(who, PackagePath(s) + $"/findings/{finding}/action", new ReviewEndpoints.FindingAction(request ?? Guid.NewGuid(),
            version ?? V<ReviewFinding>(finding), action, reason, evidence, owner, outcome), status);
    Task<JsonNode> Round(Scenario s, Guid issue, int status = 200) => Post(TestData.Marc, PackagePath(s) + "/rounds",
        new ReviewEndpoints.RoundBody(Guid.NewGuid(), V<ReviewPackage>(s.Package), "Review the explicit next actual issue", [], Assignments(s),
            "The recorded issued content changed and needs a new round", null, [issue]), status);
    async Task<Guid> Reissue(Scenario s, Guid? owner = null)
    {
        if (owner is { } person) (await F.As(TestData.Pm).Patch($"/api/v1/deliverables/{s.Deliverable}", new { ownerId = person }, V<Deliverable>(s.Deliverable))).EnsureSuccessStatusCode();
        foreach (var status in new[] { DeliverableStatus.RevisionRequired, DeliverableStatus.InProgress })
            await Post(TestData.Marc, $"/api/v1/deliverables/{s.Deliverable}/transition", new {
                toStatus = status, comment = "Recorded recipient revision request", reason = "Prepare a real next issue", rowVersion = V<Deliverable>(s.Deliverable),
            });
        return await RealIssue(s.Deliverable, true, "B");
    }
    string State(Scenario s) => F.Db(db => JsonSerializer.Serialize(new {
        Engine = t.State(s.Project.Id),
        External = db.Set<ExternalReview>().AsNoTracking().Where(row => row.ProjectId == s.Project.Id).OrderBy(row => row.Id).ToArray(),
        Manifest = db.ReviewManifestItems.AsNoTracking().Where(row => row.ProjectId == s.Project.Id).OrderBy(row => row.Id).ToArray(),
        Issues = db.DeliverableIssues.AsNoTracking().Where(row => row.ProjectId == s.Project.Id).OrderBy(row => row.Id).Select(row => new {
            row.Id, row.RowVersion, row.IssuedAuthorIds, row.Revision, row.ReturnCodeId, row.ResponseDate,
        }).ToArray(),
        Deliverables = db.Deliverables.AsNoTracking().Where(row => row.ProjectId == s.Project.Id).OrderBy(row => row.Id).Select(row => new {
            row.Id, row.RowVersion, row.Status, row.OwnerId, row.ReviewerId, row.CurrentIssueId, row.RequiredReviewPackageId,
        }).ToArray(),
        App = db.Notifications.Count(), Mail = db.Emails.Count(), Outbox = db.Outbox.Count(), Users = db.Users.Count(), Roles = db.UserRoles.Count(),
    }, JsonOpts.Web));

    [Fact]
    public async Task Member_records_party_comments_without_account_acceptance_or_automatic_signature_and_explicit_verifier_owns_their_evidence()
    {
        var s = await New(); var users = F.Db(db => db.Users.Count()); var roles = F.Db(db => db.UserRoles.Count());
        (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
        var beforeRead = State(s); var options = await (await F.As(TestData.Jill).GetAsync(RecordPath(s) + "/options")).Json();
        Assert.True(options["canRecord"]!.GetValue<bool>());
        Assert.Contains(options["issuedRevisions"]!.AsArray(), row => row!.G("deliverableIssueId") == s.Issue);
        Assert.Contains(options["parties"]!.AsArray(), row => row!.G("id") == s.Party);
        Assert.Contains(options["returnCodes"]!.AsArray(), row => row!.G("id") == s.Code); Assert.Equal(beforeRead, State(s));
        var result = await Record(s); var receiptId = result.G("id"); var findings = Findings(s);
        var detail = await Detail(s);
        var manifest = Assert.Single(detail["manifest"]!.AsArray())!.AsObject();
        Assert.True(manifest.ContainsKey("source")); Assert.Null(manifest["source"]);
        Assert.NotNull(manifest["issuedSource"]); Assert.Equal(s.Issue, manifest["issuedSource"]!.G("deliverableIssueId"));
        var presentation = Assert.Single(detail["presentation"]!.AsArray())!.AsObject();
        Assert.Equal(s.Round, presentation.G("roundId"));
        Assert.True(presentation.ContainsKey("guidance")); Assert.Null(presentation["guidance"]);
        Assert.NotNull(presentation["issuedGuidance"]);
        Assert.Contains(presentation["issuedGuidance"]!["sources"]!.AsArray(), row => row!.G("deliverableIssueId") == s.Issue);
        Assert.Equal(2, findings.Length); Assert.All(findings, finding => {
            Assert.Null(finding.SourceRevisionId); Assert.Null(finding.OriginatorId); Assert.Null(finding.VerifierId);
            Assert.Equal(s.Issue, finding.DeliverableIssueId); Assert.Equal(receiptId, finding.ExternalReviewId);
        });
        Assert.Equal(users, F.Db(db => db.Users.Count())); Assert.Equal(roles, F.Db(db => db.UserRoles.Count()));
        Assert.Equal(DeliverableStatus.Issued, F.Db(db => db.Deliverables.Single(row => row.Id == s.Deliverable).Status));
        Assert.Null(F.Db(db => db.Deliverables.Single(row => row.Id == s.Deliverable).RequiredReviewPackageId));
        Assert.Null(F.Db(db => db.DeliverableIssues.Single(row => row.Id == s.Issue).ReturnCodeId));
        Assert.False(F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).RequiredForIssue));
        var receipt = F.Db(db => db.Set<ExternalReview>().AsNoTracking().Single(row => row.Id == receiptId));
        Assert.Equal(Data.User(TestData.Jill), receipt.CreatedBy); Assert.Equal("C", receipt.ReturnCodeSnapshot);
        var findingId = findings.Single(row => row.Severity == "Blocking").Id;
        await Act(s, findingId, FindingStatus.Responded, TestData.Alex, outcome: "Clarified");
        var before = State(s); var unassigned = await Act(s, findingId, FindingStatus.VerifiedClosed, TestData.Jill, evidence: VerifierEvidence, status: 400);
        Assert.NotNull(unassigned["errors"]?["verifierId"]);
        Assert.Equal(before, State(s)); // Being recorder never assigns signature authority.
        await MakeReviewer(s, TestData.Marc); before = State(s);
        await Act(s, findingId, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Marc), status: 400);
        Assert.Equal(before, State(s)); // The original captured author cannot be designated while self-review is off.
        await Act(s, findingId, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Jill));
        before = State(s);
        await Act(s, findingId, FindingStatus.VerifiedClosed, TestData.Jill, evidence: null, status: 400);
        Assert.Equal(before, State(s));
        await Act(s, findingId, FindingStatus.VerifiedClosed, TestData.Jill, evidence: VerifierEvidence, reason: "Independently checked against the actual issue");
        var closed = Findings(s).Single(row => row.Id == findingId);
        Assert.Equal(FindingStatus.VerifiedClosed, closed.Status); Assert.Equal(ResolverEvidence, closed.EvidenceUrl);
        var verification = F.Db(db => db.FindingEvents.AsNoTracking().Single(row => row.FindingId == findingId && row.Action == FindingStatus.VerifiedClosed));
        Assert.Equal(VerifierEvidence, verification.EvidenceUrl); Assert.Equal(Data.User(TestData.Jill), verification.CreatedBy);
    }

    [Fact]
    public async Task R1_refuses_409_when_a_real_issued_finding_has_a_foreign_discipline_in_a_mixed_register()
    {
        var s = await New(); var receipt = (await Record(s)).G("id");
        var control = await t.New(s.Project, "Actual internal R1 control"); var internalFinding = await t.Finding(control);
        var finding = Findings(s).Single(row => row.ExternalReviewId == receipt && row.Severity == "Blocking").Id;
        var path = Root(s) + "/review-findings";
        var valid = await (await F.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Equal(3, valid.I("totalCount")); Assert.Equal(3, valid["items"]!.AsArray().Count);
        Assert.Contains(valid["items"]!.AsArray(), row => row!.G("id") == finding);
        Assert.Contains(valid["items"]!.AsArray(), row => row!.G("id") == internalFinding);
        var other = await Data.Project(); var foreignDiscipline = Data.ProjectDiscipline(other.Id, "Civil");
        // Root-approved STORAGE-CORRUPTION fixture: alter only one real finding's discipline FK to another
        // actual project's discipline. All source issues, receipts, findings and users above came from real commands.
        Assert.Equal(1, await F.DbAsync(db => db.ReviewFindings.Where(row => row.Id == finding)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.ProjectDisciplineId, foreignDiscipline))));
        var before = State(s); var otherBefore = t.State(other.Id);
        var error = await (await F.As(TestData.Pm).GetAsync(path)).Json(409);
        Assert.Equal("review_register_integrity", error.S("code"));
        Assert.Equal(before, State(s)); Assert.Equal(otherBefore, t.State(other.Id));
        Assert.Equal(foreignDiscipline, F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ProjectDisciplineId));
    }

    [Fact]
    public async Task Current_members_may_transcribe_but_viewers_readonly_and_removed_members_cannot()
    {
        var s = await New();
        await Record(s, who: TestData.Alex); await Record(s, who: TestData.Marc); await Record(s, who: TestData.Pm);
        foreach (var who in new[] { TestData.Diane, TestData.Rita }) {
            await (await F.As(who).GetAsync("/api/v1/me")).Json(); var before = State(s);
            await Record(s, who: who, status: 403); Assert.Equal(before, State(s));
        }
        var viewer = F.As($"synthetic-external-viewer-{Guid.NewGuid():N}@hub.test"); var user = (await (await viewer.GetAsync("/api/v1/me")).Json()).G("id");
        await Post(TestData.Pm, Root(s) + "/members", new { userId = user, roles = new[] { ProjectRole.Viewer }, primaryDisciplineId = (Guid?)null }, 201);
        var snapshot = State(s); Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Post(RecordPath(s), Body(s))).StatusCode); Assert.Equal(snapshot, State(s));
        var member = F.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.UserId == Data.User(TestData.Jill)));
        (await F.As(TestData.Pm).Delete(Root(s) + $"/members/{member.Id}?reason=Recorded%20membership%20removal", member.RowVersion)).EnsureSuccessStatusCode();
        snapshot = State(s); await Record(s, status: 403); Assert.Equal(snapshot, State(s));
    }

    [Fact]
    public async Task Future_issue_capture_is_private_even_off_and_historical_authority_does_not_follow_todays_owner()
    {
        var s = await New(presentationOn: false, package: false);
        var expected = new[] { Data.User(TestData.Marc), Data.User(TestData.Alex) }.Order().ToArray();
        Assert.Equal(expected, Authors(s.Issue)); Assert.Equal(0, F.Db(db => db.SourceRevisions.Count()));
        var raw = JsonSerializer.SerializeToNode(F.Db(db => db.DeliverableIssues.AsNoTracking().Single(row => row.Id == s.Issue)), JsonOpts.Web)!;
        Assert.False(raw.AsObject().ContainsKey("issuedAuthorIds"));
        await Flag(); var later = await Reissue(s, Data.User(TestData.Jill));
        Assert.Equal(expected, Authors(s.Issue));
        Assert.Equal(new[] { Data.User(TestData.Marc), Data.User(TestData.Jill) }.Order(), Authors(later));
        var historicalAuthor = Data.User(TestData.Alex);
        (await F.As(TestData.Admin).Patch($"/api/v1/admin/users/{historicalAuthor}", new { isActive = false }, V<AppUser>(historicalAuthor))).EnsureSuccessStatusCode();
        Assert.Equal(expected, Authors(s.Issue)); // Inactive historical authors are retained, never dropped into apparent independence.
        // The real historical issue A excludes today's owner Jill. No new owner/issuer/current-head inference repairs it.
        var body = CreateBody(s) with { Assignments = [new(s.Civil, Data.User(TestData.Jill), new(2026, 11, 12)), new(s.Electrical, Data.User(TestData.Omar), new(2026, 11, 12))] };
        var package = (await Post(TestData.Marc, Root(s) + "/reviews", body)).G("id");
        s = s with { Package = package, Round = F.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value) };
        await Start(s);
        Assert.Equal(expected, F.Db(db => db.ReviewManifestItems.Single(row => row.RoundId == s.Round).AuthorIds));
        Assert.False(F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).RequiredForIssue));
        Assert.Null(F.Db(db => db.Deliverables.Single(row => row.Id == s.Deliverable).RequiredReviewPackageId));
        var before = State(s);
        await Post(TestData.Marc, Root(s) + "/reviews", CreateBody(s, required: true), 400);
        Assert.Equal(before, State(s));
    }

    [Fact]
    public async Task Explicit_carry_keeps_incoming_identity_resets_response_and_rechecks_independence_against_both_issues()
    {
        var s = await New(); var request = Body(s) with { Comments = [new("Retain the original incoming comment", s.Civil, Data.User(TestData.Alex), "Blocking")] };
        var receipt = await Record(s, request); var original = Assert.Single(Findings(s));
        await Act(s, original.Id, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Jill));
        await Act(s, original.Id, FindingStatus.Responded, TestData.Alex, outcome: "Not incorporated", reason: "Retained existing route after independent consideration");
        var nextIssue = await Reissue(s, Data.User(TestData.Jill)); var before = State(s);
        await Round(s, nextIssue, 400); Assert.Equal(before, State(s));
        await Act(s, original.Id, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Omar));
        await Round(s, nextIssue);
        var carried = Findings(s).Single(row => row.CarriedFromId == original.Id);
        Assert.Equal(nextIssue, carried.DeliverableIssueId); Assert.Equal(original.ExternalReviewId, carried.ExternalReviewId);
        Assert.Equal(FindingStatus.Open, carried.Status); Assert.Null(carried.Response); Assert.Null(carried.ResponseOutcome); Assert.Null(carried.EvidenceUrl);
        Assert.Equal(s.Issue, F.Db(db => db.Set<ExternalReview>().Single(row => row.Id == receipt.G("id")).DeliverableIssueId));
        var replayBefore = State(s); var replay = await Record(s, request);
        Assert.True(JsonNode.DeepEquals(receipt, replay)); Assert.Equal(replayBefore, State(s));
        // Canonical finding actions on a carried current Draft remain possible; incoming new receipts require Start.
        await Act(s, carried.Id, FindingStatus.Responded, TestData.Alex, outcome: "Noted");
        before = State(s); await Act(s, original.Id, FindingStatus.Responded, TestData.Alex, outcome: "Noted", status: 404); Assert.Equal(before, State(s));
    }

    [Fact]
    public async Task Removed_issue_scope_carries_through_a_later_round_without_repeating_the_old_removal_impact()
    {
        var s = await New(); var receipt = await Record(s, Body(s) with { Comments = [new("Retain the actual removed-scope comment", s.Civil, Data.User(TestData.Alex), "Blocking")] });
        var first = Assert.Single(Findings(s));
        var secondIssue = await AnotherIssue(s, revision: null); // A real issue identity does not require an invented revision label/URL.
        var removal = new ReviewEndpoints.RoundBody(Guid.NewGuid(), V<ReviewPackage>(s.Package), "Explicitly remove the original issued scope", [], Assignments(s),
            "The original scope is excluded from this round", "Keep its unresolved incoming finding and original party history", [secondIssue]);
        await Post(TestData.Pm, PackagePath(s) + "/rounds", removal); await Start(s);
        var second = Findings(s).Single(row => row.CarriedFromId == first.Id);
        Assert.Equal(s.Issue, second.DeliverableIssueId); Assert.Equal(first.ExternalReviewId, second.ExternalReviewId);
        var before = State(s); await Record(s, status: 400); Assert.Equal(before, State(s)); // New incoming work cannot target removed current scope.
        var continued = new ReviewEndpoints.RoundBody(Guid.NewGuid(), V<ReviewPackage>(s.Package), "Continue the retained current issued scope", [], Assignments(s),
            "The old removal boundary remains recorded in its actual previous round", null, [secondIssue]);
        await Post(TestData.Marc, PackagePath(s) + "/rounds", continued);
        var third = Findings(s).Single(row => row.CarriedFromId == second.Id);
        Assert.Equal(s.Issue, third.DeliverableIssueId); Assert.Equal(first.ExternalReviewId, third.ExternalReviewId);
        Assert.Equal(FindingStatus.Open, third.Status); Assert.Null(third.VerifierId);
        Assert.Equal(3, F.Db(db => db.ReviewRounds.Count(row => row.PackageId == s.Package)));
        Assert.Equal(receipt.G("id"), F.Db(db => db.Set<ExternalReview>().Single(row => row.PackageId == s.Package).Id));
        var detail = await Detail(s); Assert.Equal(1, Assert.Single(detail["externalReviews"]!.AsArray())!.I("commentCount"));
        (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
        var child = await (await F.As(TestData.Alex).GetAsync(Root(s) + $"/review-findings/{third.Id}")).Json();
        Assert.Equal(RecordContextAction.RespondToComment, child["context"]!.S("nextAction"));
        Assert.Equal(Data.User(TestData.Alex), Assert.Single(child["context"]!["nextActors"]!.AsArray())!.G("id"));
    }

    [Fact]
    public async Task Only_an_inactive_discipline_lead_relation_does_not_grant_recording_or_options_capability()
    {
        var s = await New();
        using var actor = F.As($"synthetic-inactive-external-lead-{Guid.NewGuid():N}@hub.test");
        var me = await (await actor.GetAsync("/api/v1/me")).Json(); var user = me.G("id");
        Assert.Empty(me["roles"]!.AsArray()); Assert.False(me["capabilities"]!["readOnly"]!.GetValue<bool>());
        var discipline = (await Post(TestData.Pm, Root(s) + "/disciplines", new {
            disciplineId = await Data.Discipline("Environmental"), leadUserId = (Guid?)user,
        }, 201)).G("id");
        // All standing is established with normal team commands. SetLead legitimately adds a Team Member;
        // downgrade it to Viewer without clearing the lead, then deactivate that discipline.
        var member = F.Db(db => db.ProjectMembers.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.UserId == user && row.RemovedAt == null));
        (await F.As(TestData.Pm).Patch(Root(s) + $"/members/{member.Id}", new {
            roles = new[] { ProjectRole.Viewer }, primaryDisciplineId = (Guid?)null,
        }, member.RowVersion)).EnsureSuccessStatusCode();
        var activeOptions = await (await actor.GetAsync(RecordPath(s) + "/options")).Json();
        Assert.True(activeOptions["canRecord"]!.GetValue<bool>()); // Active lead is the sole authorizing relation.
        (await F.As(TestData.Pm).Patch(Root(s) + $"/disciplines/{discipline}", new { isActive = false }, V<ProjectDiscipline>(discipline))).EnsureSuccessStatusCode();
        var inactive = F.Db(db => db.ProjectDisciplines.AsNoTracking().Single(row => row.Id == discipline));
        Assert.False(inactive.IsActive); Assert.Equal(user, inactive.LeadUserId);
        Assert.True(F.Db(db => db.Users.Single(row => row.Id == user).IsActive));
        Assert.False(F.Db(db => db.ProjectDisciplines.Any(row => row.ProjectId == s.Project.Id && row.IsActive && row.LeadUserId == user)));
        Assert.False(F.Db(db => db.ProjectMembers.Where(row => row.ProjectId == s.Project.Id && row.UserId == user && row.RemovedAt == null)
            .Any(row => row.Roles.Contains(ProjectRole.PM) || row.Roles.Contains(ProjectRole.TeamMember) || row.Roles.Contains(ProjectRole.Reviewer))));
        Assert.NotEqual(user, F.Db(db => db.Projects.Single(row => row.Id == s.Project.Id).ProjectManagerId));
        var before = State(s);
        var options = await (await actor.GetAsync(RecordPath(s) + "/options")).Json();
        Assert.False(options["canRecord"]!.GetValue<bool>());
        var detail = await (await actor.GetAsync(PackagePath(s))).Json(); Assert.False(detail["canRecordExternalReview"]!.GetValue<bool>());
        var body = Body(s) with { Comments = [new("Inactive lead cannot transcribe this comment", s.Civil, Data.User(TestData.Alex), "Advisory")] };
        Assert.Equal(HttpStatusCode.Forbidden, (await actor.Post(RecordPath(s), body)).StatusCode);
        Assert.Equal(before, State(s));
        Assert.False(F.Db(db => db.CoordinationCommands.Any(row => row.ProjectId == s.Project.Id && row.RequestId == body.RequestId)));
    }
}
