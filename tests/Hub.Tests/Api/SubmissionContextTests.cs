using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// API contract coverage for the submission package/check context adapter.  The
/// fixture deliberately goes through the submission command API so every check
/// has a real package, current manifest version and source-revision FK.
public sealed class SubmissionContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public SubmissionContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();

    sealed record Setup(Project Project, Guid Package, Guid Required, Guid Optional, Guid Coordinator, Guid Deliverable, Guid Revision);

    async Task<Setup> New()
    {
        var project = await d.Project();
        var civil = d.ProjectDiscipline(project.Id, "Civil");
        var milestone = (await Post(TestData.Pm, $"/api/v1/projects/{project.Id}/milestones",
            new MilestoneEndpoints.CreateBody("Submission milestone", MilestoneType.DesignSubmission, new DateOnly(2026, 10, 15), null, null, null, true), 201)).G("id");
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Submission source", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(),
            ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false
        })).Json(201)).G("id");
        var revision = (await Post(TestData.Alex, $"/api/v1/projects/{project.Id}/source-revisions",
            new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable), civil, d.User(TestData.Alex),
                "Deliverable", "submission-source", "Submission source", "A", "https://example.test/submission.pdf", "Design team", "Submission",
                null, null, null, null, null, null))).G("id");
        var created = await Post(TestData.Marc, $"/api/v1/projects/{project.Id}/submissions",
            new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Permit package", "Permit review", "Municipality", d.User(TestData.Marc), milestone,
                new DateOnly(2026, 10, 15), [new(revision)], [new("Applicability", d.User(TestData.Alex), civil)], null, null));
        var package = created.G("id");
        var required = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == package && c.Kind == SubmissionCheckKind.Deliverable && c.SourceId == deliverable));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == package && !c.Required));
        return new(project, package, required.Id, optional.Id, d.User(TestData.Marc), deliverable, revision);
    }

    string Package(Setup s) => $"/api/v1/projects/{s.Project.Id}/submissions/{s.Package}";
    string Check(Setup s, Guid? id = null) => $"/api/v1/projects/{s.Project.Id}/submission-checks/{id ?? s.Required}";

    [Fact]
    public async Task Flag_off_omits_context_and_exact_check_route_is_not_found()
    {
        var s = await New();
        var off = await Get(TestData.Marc, Package(s));
        Assert.Null(off["context"]);
        Assert.All(off["checks"]!.AsArray(), row => { Assert.False(row!.AsObject().ContainsKey("context")); Assert.False(row.AsObject().ContainsKey("effectiveStatus")); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Marc).GetAsync(Check(s))).StatusCode);
        await Enable();
        var on = await Get(TestData.Marc, Package(s));
        Assert.NotNull(on["context"]);
        Assert.Equal(off["readiness"]!.ToJsonString(), on["readiness"]!.ToJsonString());
        Assert.Equal(off["effectiveStatus"]!.ToJsonString(), on["effectiveStatus"]!.ToJsonString());
        Assert.All(on["checks"]!.AsArray(), row => { Assert.NotNull(row!["context"]); Assert.NotNull(row["effectiveStatus"]); });
        var child = await Get(TestData.Marc, Check(s));
        Assert.Equal(s.Package, child.G("packageId")); Assert.Equal(1, child["manifestVersion"]!.GetValue<int>());
        Assert.Equal(d.User(TestData.Marc), child["context"]!["owner"]!["id"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Draft_is_owned_by_coordinator_and_start_is_next_action()
    {
        var s = await New(); await Enable();
        var context = (await Get(TestData.Pm, Package(s)))["context"]!;
        Assert.Equal(s.Coordinator, context["owner"]!["id"]!.GetValue<Guid>());
        Assert.Equal(RecordContextAction.StartChecking, context["nextAction"]!.GetValue<string>());
        Assert.Equal("2026-10-15", context["neededBy"]!.GetValue<string>());
        await Post(TestData.Marc, Package(s) + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), null));
        context = (await Get(TestData.Pm, Package(s)))["context"]!;
        Assert.Equal(SubmissionStatus.Ready, f.Db(db => db.SubmissionPackages.Single(p => p.Id == s.Package).Status));
        Assert.Equal(d.User(TestData.Pm), context["nextActors"]![0]!["id"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Optional_applicability_failure_does_not_block_ready_and_check_has_no_inherited_date()
    {
        var s = await New(); await Enable();
        await Post(TestData.Marc, Package(s) + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), null));
        var optional = f.Db(db => db.SubmissionChecks.Single(c => c.Id == s.Optional));
        await Post(TestData.Alex, Package(s) + $"/checks/{s.Optional}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), optional.RowVersion,
            SubmissionCheckStatus.Fail, "https://example.test/optional-fail.pdf", "Optional applicability exception"));
        var child = await Get(TestData.Pm, Check(s, s.Optional));
        Assert.Null(child["context"]!["neededBy"]);
        var package = await Get(TestData.Pm, Package(s));
        Assert.Equal(SubmissionStatus.Ready, package["effectiveStatus"]!.GetValue<string>());
    }

    [Fact]
    public async Task Required_failure_without_a_known_source_does_not_invent_a_resolve_step()
    {
        var s = await New(); await Enable();
        await Post(TestData.Marc, Package(s) + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), null));
        await f.DbAsync(async db =>
        {
            var check = await db.SubmissionChecks.SingleAsync(c => c.Id == s.Required);
            check.Status = SubmissionCheckStatus.Fail; check.Reason = "Required evidence is invalid"; check.EvidenceUrl = "https://example.test/required.pdf";
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await Get(TestData.Pm, Package(s)))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, context["nextAction"]!.GetValue<string>());
        Assert.Equal(d.User(TestData.Marc), context["nextActors"]![0]!["id"]!.GetValue<Guid>());
        Assert.Contains(s.Required.ToString(), context.ToJsonString());
    }

    [Fact]
    public async Task Missing_source_revision_is_not_presented_as_a_pass()
    {
        var s = await New(); await Enable();
        // Simulate a damaged imported reference without deleting a referenced immutable row.
        await f.DbAsync(async db => await db.SourceRevisions.Where(x => x.Id == s.Revision)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.DeliverableId, (Guid?)null)));
        var detail = await Get(TestData.Pm, Package(s));
        var rows = detail["checks"]!.AsArray();
        var revision = rows.Single(x => x!["kind"]!.GetValue<string>() == SubmissionCheckKind.CurrentRevision)!;
        Assert.Equal(SubmissionCheckStatus.Fail, revision["effectiveStatus"]!.GetValue<string>());
        Assert.Equal(RecordContextAction.NotAssessed, revision["context"]!["nextAction"]!.GetValue<string>());
        Assert.Equal(1, revision["context"]!["unavailableInputCount"]!.GetValue<int>());
        foreach (var kind in new[] { SubmissionCheckKind.IndependentReview, SubmissionCheckKind.BlockingFindings, SubmissionCheckKind.Access })
        {
            var row = rows.Single(x => x!["kind"]!.GetValue<string>() == kind)!;
            Assert.Null(row["effectiveStatus"]);
            Assert.Equal(RecordContextAction.NotAssessed, row["context"]!["nextAction"]!.GetValue<string>());
            Assert.False(row["context"]!["terminal"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task Missing_deliverable_does_not_turn_skipped_checks_into_passes()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => await db.Deliverables.Where(x => x.Id == s.Deliverable)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.DeletedAt, f.Clock.Now)));
        var detail = await Get(TestData.Pm, Package(s));
        var rows = detail["checks"]!.AsArray();
        Assert.Equal(SubmissionCheckStatus.Fail, rows.Single(x => x!["id"]!.GetValue<Guid>() == s.Required)!["effectiveStatus"]!.GetValue<string>());
        foreach (var kind in new[] { SubmissionCheckKind.CurrentRevision, SubmissionCheckKind.IndependentReview, SubmissionCheckKind.BlockingFindings, SubmissionCheckKind.Access })
        {
            var row = rows.Single(x => x!["kind"]!.GetValue<string>() == kind)!;
            Assert.Null(row["effectiveStatus"]);
            Assert.Equal(RecordContextAction.NotAssessed, row["context"]!["nextAction"]!.GetValue<string>());
            Assert.False(row["context"]!["terminal"]!.GetValue<bool>());
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_required_review_or_current_round_does_not_pass_findings(bool missingPackage)
    {
        var s = await New(); await Enable(); Guid? reviewId = null;
        if (!missingPackage)
        {
            var civil = d.ProjectDiscipline(s.Project.Id, "Civil");
            var electrical = d.ProjectDiscipline(s.Project.Id, "Electrical");
            reviewId = (await Post(TestData.Marc, $"/api/v1/projects/{s.Project.Id}/reviews",
                new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Required review", "Review submission source", civil, s.Coordinator,
                    [s.Revision], [new ReviewEndpoints.Assignment(electrical, d.User(TestData.Omar), new DateOnly(2026, 10, 12))], true, null))).G("id");
            // Deliberately damaged imported current-round pointer; the historical round remains intact.
            await f.DbAsync(async db => await db.ReviewPackages.Where(x => x.Id == reviewId)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.CurrentRoundId, (Guid?)null)));
        }
        await f.DbAsync(async db => await db.Deliverables.Where(x => x.Id == s.Deliverable)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RequiresReview, true).SetProperty(x => x.RequiredReviewPackageId, reviewId)));
        var rows = (await Get(TestData.Pm, Package(s)))["checks"]!.AsArray();
        var findings = rows.Single(x => x!["kind"]!.GetValue<string>() == SubmissionCheckKind.BlockingFindings)!;
        Assert.Null(findings["effectiveStatus"]);
        Assert.Equal(RecordContextAction.NotAssessed, findings["context"]!["nextAction"]!.GetValue<string>());
        Assert.False(findings["context"]!["terminal"]!.GetValue<bool>());
        Assert.Equal(SubmissionCheckStatus.Pass, rows.Single(x => x!["kind"]!.GetValue<string>() == SubmissionCheckKind.Access)!["effectiveStatus"]!.GetValue<string>());
    }

    [Fact]
    public async Task Check_locator_refuses_another_project_and_an_obsolete_manifest_version()
    {
        var s = await New(); await Enable(); var other = await d.Project();
        await Get(TestData.Pm, $"/api/v1/projects/{other.Id}/submission-checks/{s.Required}", 404);
        await Post(TestData.Marc, Package(s) + "/manifest", new SubmissionEndpoints.ManifestBody(Guid.NewGuid(),
            Version<SubmissionPackage>(s.Package), [new(s.Revision)], "Recheck the manifest"));
        await Get(TestData.Pm, Check(s), 404);
        var current = f.Db(db => db.SubmissionChecks.Single(x => x.PackageId == s.Package && x.ManifestVersion == 2 && x.Kind == SubmissionCheckKind.Deliverable));
        Assert.Equal(2, (await Get(TestData.Pm, Check(s, current.Id)))["manifestVersion"]!.GetValue<int>());
    }

    [Fact]
    public async Task Terminal_pass_check_has_no_next_action_and_restricted_parent_hides_child()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { var c = await db.SubmissionChecks.SingleAsync(c => c.Id == s.Required); c.Status = SubmissionCheckStatus.Pass; c.EvidenceUrl = "https://example.test/pass.pdf"; await db.SaveChangesAsync(); return 0; });
        var child = await Get(TestData.Pm, Check(s));
        Assert.True(child["context"]!["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, child["context"]!["nextAction"]!.GetValue<string>());
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, Check(s), 404);
    }
}
