using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// API coverage for the exact ReviewFinding context adapter. The fixture uses the
/// existing review workflow so package/round/manifest foreign keys are genuine.
public sealed class ReviewFindingContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public ReviewFindingContextTests() => d = new(f);
    public void Dispose() => f.Dispose();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();

    sealed record Setup(Project Project, Guid Package, Guid Round, Guid Revision, Guid Finding);

    async Task<Setup> New()
    {
        var p = await d.Project(); var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Finding source", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false })).Json(201)).G("id");
        var rev = (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "finding", "Finding source", "A", "https://example.test/finding.pdf", "Survey", "Scope", null, null, null, null, null, null))).G("id");
        var package = (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Finding review", "Verify findings", civil, d.User(TestData.Marc), [rev], [new ReviewEndpoints.Assignment(electrical, d.User(TestData.Omar), new DateOnly(2026, 9, 24))], true, null))).G("id");
        await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/reviews/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start review"));
        var round = f.Db(db => db.ReviewPackages.Single(x => x.Id == package).CurrentRoundId!.Value);
        var finding = (await Post(TestData.Omar, $"/api/v1/projects/{p.Id}/reviews/{package}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(package), rev, electrical, d.User(TestData.Alex), "Check service clearance", "Blocking"))).G("id");
        return new(p, package, round, rev, finding);
    }
    string Detail(Setup s) => $"/api/v1/projects/{s.Project.Id}/reviews/{s.Package}";
    string Child(Setup s, Guid? id = null) => $"/api/v1/projects/{s.Project.Id}/review-findings/{id ?? s.Finding}";

    [Fact]
    public async Task Flag_off_hides_context_and_exact_child_then_enabled_read_is_project_scoped()
    {
        var s = await New();
        var off = await Get(TestData.Alex, Detail(s));
        Assert.All(off["findings"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("context")));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync(Child(s))).StatusCode);
        await Enable();
        var on = await Get(TestData.Alex, Detail(s));
        Assert.All(on["findings"]!.AsArray(), row => Assert.NotNull(row!["context"]));
        foreach (var key in new[] { "actorId", "canCoordinate", "canManage", "canWrite", "canReassignVerifier" })
            Assert.Equal(off[key]!.ToJsonString(), on[key]!.ToJsonString());
        var child = await Get(TestData.Alex, Child(s));
        Assert.Equal(s.Finding, child.G("id")); Assert.Equal(s.Package, child.G("packageId")); Assert.Equal(s.Round, child.G("roundId"));
        Assert.Null(child["context"]!["neededBy"]);
        Assert.Equal(d.User(TestData.Alex), child["context"]!["owner"]!.G("id"));
        var other = await d.Project(); Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{other.Id}/review-findings/{s.Finding}")).StatusCode);
    }

    [Fact]
    public async Task Responded_uses_recorded_verifier_and_terminal_closure_does_not_fallback_to_originator()
    {
        var s = await New(); await Enable(); var verifier = d.User(TestData.Pm);
        await f.DbAsync(async db => { var x = await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding); x.Status = FindingStatus.Responded; x.Response = "Recorded response"; x.EvidenceUrl = "https://example.test/evidence"; x.VerifierId = verifier; await db.SaveChangesAsync(); return 0; });
        var child = await Get(TestData.Alex, Child(s)); Assert.Equal(verifier, child["context"]!["nextActors"]![0]!.G("id")); Assert.Equal("verify_closure", child["context"]!.S("nextAction"));
        await f.DbAsync(async db => { (await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding)).Status = FindingStatus.VerifiedClosed; await db.SaveChangesAsync(); return 0; });
        child = await Get(TestData.Alex, Child(s)); Assert.True(child["context"]!["terminal"]!.GetValue<bool>()); Assert.Empty(child["context"]!["nextActors"]!.AsArray());
    }

    [Fact]
    public async Task Withdrawn_blocking_without_acknowledgement_still_needs_coordinator()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { var x = await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding); x.Status = FindingStatus.Withdrawn; x.Severity = "Blocking"; x.WithdrawalAcknowledgedBy = null; await db.SaveChangesAsync(); return 0; });
        var child = await Get(TestData.Alex, Child(s)); Assert.Equal(d.User(TestData.Marc), child["context"]!["nextActors"]![0]!.G("id")); Assert.Equal("acknowledge_withdrawal", child["context"]!.S("nextAction"));
        await f.DbAsync(async db => { (await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding)).WithdrawalAcknowledgedBy = d.User(TestData.Marc); await db.SaveChangesAsync(); return 0; });
        child = await Get(TestData.Alex, Child(s)); Assert.True(child["context"]!["terminal"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Cancelled_package_finding_is_not_assessed_and_owner_date_remains_unknown()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { (await db.ReviewPackages.SingleAsync(x => x.Id == s.Package)).Status = ReviewStatus.Cancelled; await db.SaveChangesAsync(); return 0; });
        var child = await Get(TestData.Alex, Child(s)); Assert.Equal(RecordContextAction.NotAssessed, child["context"]!.S("nextAction")); Assert.Empty(child["context"]!["nextActors"]!.AsArray()); Assert.Null(child["context"]!["neededBy"]);
    }

    async Task<Guid> Advance(Setup s)
    {
        await Post(TestData.Marc, $"/api/v1/projects/{s.Project.Id}/reviews/{s.Package}/rounds", new ReviewEndpoints.RoundBody(
            Guid.NewGuid(), Version<ReviewPackage>(s.Package), "Fresh round", [s.Revision],
            [new ReviewEndpoints.Assignment(d.ProjectDiscipline(s.Project.Id, "Electrical"), d.User(TestData.Omar), new DateOnly(2026, 9, 28))], "Updated scope", null));
        return f.Db(db => db.ReviewFindings.Single(x => x.CarriedFromId == s.Finding && x.PackageId == s.Package).Id);
    }

    [Fact]
    public async Task Historical_finding_remains_exact_while_current_draft_can_respond()
    {
        var s = await New(); await Enable(); var carried = await Advance(s);
        var historical = (await Get(TestData.Pm, Child(s)))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, historical.S("nextAction")); Assert.Empty(historical["nextActors"]!.AsArray());
        var current = await Get(TestData.Pm, Child(s, carried)); Assert.NotEqual(s.Round, current.G("roundId"));
        Assert.Equal(RecordContextAction.RespondToComment, current["context"]!.S("nextAction"));
        await Post(TestData.Alex, $"/api/v1/projects/{s.Project.Id}/reviews/{s.Package}/findings/{carried}/action", new ReviewEndpoints.FindingAction(
            Guid.NewGuid(), Version<ReviewFinding>(carried), FindingStatus.Responded, "Respond in draft", "https://example.test/draft-response", null));
        Assert.Equal(FindingStatus.Responded, f.Db(db => db.ReviewFindings.Single(x => x.Id == carried).Status));
    }

    [Theory]
    [InlineData("resolver")]
    [InlineData("verifier")]
    [InlineData("coordinator")]
    public async Task Inactive_named_people_remain_named_without_replacement(string role)
    {
        var s = await New(); await Enable();
        var person = d.User(role == "resolver" ? TestData.Alex : role == "verifier" ? TestData.Omar : TestData.Marc);
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == person)).IsActive = false;
            var finding = await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding);
            if (role == "verifier") { finding.Status = FindingStatus.Responded; finding.Response = "Response"; finding.EvidenceUrl = "https://example.test/evidence"; }
            if (role == "coordinator") finding.Status = FindingStatus.Withdrawn;
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await Get(TestData.Pm, Child(s)))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction"));
        Assert.Equal(person, Assert.Single(context["nextActors"]!.AsArray())!.G("id")); Assert.Equal("Inactive", context["nextActors"]![0]!.S("state"));
        Assert.Null(context["neededBy"]);
    }

    [Theory]
    [InlineData("captured")]
    [InlineData("current_owner")]
    [InlineData("response_author")]
    [InlineData("ancestor")]
    [InlineData("cycle")]
    public async Task Independence_uses_complete_provenance_and_fails_safe(string provenance)
    {
        var s = await New(); await Enable(); var id = provenance is "ancestor" or "cycle" ? await Advance(s) : s.Finding;
        var verifier = d.User(TestData.Omar);
        if (provenance == "current_owner")
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = true })).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            var finding = await db.ReviewFindings.SingleAsync(x => x.Id == id);
            finding.Status = FindingStatus.Responded; finding.Response = "Recorded response"; finding.EvidenceUrl = "https://example.test/evidence";
            if (provenance == "captured")
            {
                // Deliberate legacy-corruption fixture: normal commands keep captured evidence immutable.
                var ids = (await db.ReviewManifestItems.AsNoTracking().SingleAsync(x => x.RoundId == finding.RoundId)).AuthorIds.Append(verifier).ToArray();
                await db.ReviewManifestItems.Where(x => x.RoundId == finding.RoundId).ExecuteUpdateAsync(u => u.SetProperty(x => x.AuthorIds, ids));
            }
            if (provenance == "current_owner") { var revision = await db.SourceRevisions.SingleAsync(x => x.Id == s.Revision); (await db.Deliverables.SingleAsync(x => x.Id == revision.DeliverableId)).OwnerId = verifier; }
            if (provenance == "response_author") db.FindingEvents.Add(new FindingEvent { ProjectId = s.Project.Id, FindingId = id, Action = FindingStatus.Responded, Reason = "Prior response", CreatedBy = verifier });
            if (provenance == "ancestor") (await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding)).ResolverId = verifier;
            if (provenance == "cycle") (await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding)).CarriedFromId = id;
            await db.SaveChangesAsync(); return 0;
        });
        if (provenance == "current_owner")
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = false })).EnsureSuccessStatusCode();
        var context = (await Get(TestData.Pm, Child(s, id)))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction")); Assert.Empty(context["nextActors"]!.AsArray());
        Assert.Equal(d.User(TestData.Alex), context["owner"]!.G("id")); Assert.Null(context["neededBy"]);
    }

    [Fact]
    public async Task Last_blocker_verification_keeps_the_canonical_approval_gate()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db =>
        {
            var finding = await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding);
            finding.Status = FindingStatus.Responded; finding.Response = "Response"; finding.EvidenceUrl = "https://example.test/evidence";
            foreach (var a in await db.DisciplineReviews.Where(x => x.RoundId == s.Round).ToListAsync()) a.Status = DisciplineReviewStatus.Approved;
            (await db.Users.SingleAsync(x => x.Id == d.User(TestData.Marc))).IsActive = false;
            await db.SaveChangesAsync(); return 0;
        });
        Assert.Equal(RecordContextAction.NotAssessed, (await Get(TestData.Pm, Child(s)))["context"]!.S("nextAction"));
        var response = await f.As(TestData.Omar).Post($"/api/v1/projects/{s.Project.Id}/reviews/{s.Package}/findings/{s.Finding}/action",
            new ReviewEndpoints.FindingAction(Guid.NewGuid(), Version<ReviewFinding>(s.Finding), FindingStatus.VerifiedClosed, "Attempt closure", null, null));
        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(FindingStatus.Responded, f.Db(db => db.ReviewFindings.Single(x => x.Id == s.Finding).Status));
    }

    [Fact]
    public async Task Exact_child_requires_accessible_parent_and_round()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, Child(s), 404); await Get(TestData.Pm, Child(s, Guid.NewGuid()), 404);
        await f.DbAsync(async db => { (await db.ReviewPackages.SingleAsync(x => x.Id == s.Package)).DeletedAt = f.Clock.Now; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Pm, Child(s), 404);
    }

    [Fact]
    public async Task Foreign_project_task_authorship_is_unknown_without_exposing_the_reference()
    {
        var s = await New(); await Enable(); var other = await d.Project();
        var foreignTask = (await d.NewTask(other.Id, TestData.Omar, new { assigneeId = d.User(TestData.Omar) }, "Electrical")).G("id");
        var deliverable = f.Db(db => db.SourceRevisions.Single(x => x.Id == s.Revision).DeliverableId!.Value);
        await f.DbAsync(async db =>
        {
            // Deliberate malformed FK fixture; the command API rejects cross-project output links.
            await db.Tasks.Where(x => x.Id == foreignTask).ExecuteUpdateAsync(u => u.SetProperty(x => x.DeliverableId, deliverable));
            var finding = await db.ReviewFindings.SingleAsync(x => x.Id == s.Finding);
            finding.Status = FindingStatus.Responded; finding.Response = "Response"; finding.EvidenceUrl = "https://example.test/evidence";
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await Get(TestData.Pm, Child(s)))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction")); Assert.Empty(context["nextActors"]!.AsArray());
        Assert.DoesNotContain(foreignTask.ToString(), context.ToJsonString()); Assert.DoesNotContain(other.ProjectNumber, context.ToJsonString());
    }
}
