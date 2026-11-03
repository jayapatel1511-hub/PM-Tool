using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkDeliverySubmissionTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkDeliverySubmissionTests() => data = new(f);
    public void Dispose() => f.Dispose();
    sealed record Setup(Project Project, Guid Package, Guid Milestone, Guid Deliverable, Guid Revision, Guid Optional, Guid Electrical);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Get(string path, string who = TestData.Pm) => await (await f.As(who).GetAsync(path)).Json();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
    static string Root(Setup s) => $"/api/v1/projects/{s.Project.Id}";
    static string Path(Setup s) => Root(s) + $"/submissions/{s.Package}";
    async Task<Guid> Document(Project p, string discipline, string who, string owner, string title)
    {
        return (await Post(who, $"/api/v1/projects/{p.Id}/deliverables", new { name = title, projectDisciplineId = data.ProjectDiscipline(p.Id, discipline),
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(owner), revision = "A", requiresReview = false }, 201)).G("id");
    }
    ChangeEndpoints.RegisterBody Registration(Setup s, string revision, Guid? supersedes = null) => new(Guid.NewGuid(), s.Deliverable, Version<Deliverable>(s.Deliverable), s.Electrical,
        data.User(TestData.Omar), "Deliverable", $"delivery-{s.Deliverable}", "Electrical source", revision, $"https://example.test/{s.Deliverable}/{revision}.pdf", "Design team", "Submission",
        null, supersedes, supersedes is null ? null : f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.Project.Id && h.Identity == $"deliverable:{s.Deliverable}").RowVersion),
        supersedes is null ? null : "Revised interface", supersedes is null ? null : new(2026, 9, 15), supersedes is null ? null : new(2026, 9, 24));
    async Task<Setup> New(Project? project = null, string title = "Electrical submission", DateOnly? target = null)
    {
        var p = project ?? await data.Project(); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var milestone = (await Post(TestData.Pm, $"/api/v1/projects/{p.Id}/milestones", new MilestoneEndpoints.CreateBody("Delivery milestone", MilestoneType.DesignSubmission, new(2026, 10, 15), null, null, null, true), 201)).G("id");
        var deliverable = await Document(p, "Electrical", TestData.Omar, TestData.Omar, title + " source");
        var s = new Setup(p, Guid.Empty, milestone, deliverable, Guid.Empty, Guid.Empty, electrical);
        var revision = (await Post(TestData.Omar, Root(s) + "/source-revisions", Registration(s, "A"))).G("id");
        var package = (await Post(TestData.Omar, Root(s) + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), title, "Municipal issue", "Municipality", data.User(TestData.Omar),
            milestone, target ?? new(2026, 10, 15), [new(revision)], [new("Optional site note", data.User(TestData.Alex), electrical)], null, null))).G("id");
        s = s with { Revision = revision, Package = package, Optional = f.Db(db => db.SubmissionChecks.Single(c => c.PackageId == package && !c.Required).Id) };
        await Post(TestData.Omar, Path(s) + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(package), null));
        await Optional(s, SubmissionCheckStatus.Pass);
        return s;
    }
    async Task Optional(Setup s, string action) => await Post(TestData.Alex, Path(s) + $"/checks/{s.Optional}", new SubmissionEndpoints.CheckBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package),
        Version<SubmissionCheck>(s.Optional), action, "https://example.test/site-note", "Site note checked for this issue"));
    async Task<Guid> Publish(Setup s)
    {
        var notice = (await Post(TestData.Omar, Root(s) + "/source-revisions", Registration(s, "B", s.Revision))).G("id");
        await Post(TestData.Omar, Root(s) + $"/changes/{notice}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(notice),
            f.Db(db => db.SourceHeads.Single(h => h.ProjectId == s.Project.Id && h.Identity == $"deliverable:{s.Deliverable}").RowVersion),
            [new(ItemType.Deliverable, s.Deliverable, Version<Deliverable>(s.Deliverable))]));
        return notice;
    }
    async Task<Guid> Block(Setup s)
    {
        var source = await Document(s.Project, "Civil", TestData.Marc, TestData.Alex, "Survey handoff source");
        var handoff = (await Post(TestData.Marc, Root(s) + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Survey for electrical issue", source, Version<Deliverable>(source), "A",
            $"https://example.test/{source}/A.pdf", s.Electrical, data.User(TestData.Alex), data.User(TestData.Omar), null, s.Deliverable,
            "Coordinate the issue", "Survey covers the site", new(2026, 9, 20), new(2026, 9, 22), null, null), 201)).G("id");
        await Post(TestData.Alex, Root(s) + $"/handoffs/{handoff}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, Version<Handoff>(handoff), "Submit the survey", null));
        await Publish(s); await Optional(s, SubmissionCheckStatus.Pass);
        return handoff;
    }

    [Fact]
    public async Task Normal_commands_produce_one_fail_two_pending_five_pass_and_three_distinct_blocking_checks()
    {
        var s = await New(); var handoff = await Block(s); await Enable();
        var detail = await Get(Path(s)); var delivery = detail["delivery"]!; var checks = delivery["checks"]!.AsArray();
        Assert.Equal(8, checks.Count); Assert.Equal(1, checks.Count(c => c!.S("state") == SubmissionCheckStatus.Fail));
        Assert.Equal(2, checks.Count(c => c!.S("state") == SubmissionCheckStatus.Pending)); Assert.Equal(5, checks.Count(c => c!.S("state") == SubmissionCheckStatus.Pass));
        Assert.Equal(3, delivery["summary"]!.I("blockingCheckCount")); Assert.Equal(0, delivery["summary"]!.I("packageProblemCount"));
        Assert.Equal(3, checks.Count(c => c!["blocking"]!.GetValue<bool>()));
        var handoffSource = checks.SelectMany(c => c!["reasons"]!.AsArray()).Single(r => r!["source"]?["id"]?.GetValue<Guid>() == handoff)!["source"]!;
        Assert.Equal("Handoff", handoffSource.S("kind")); Assert.Equal("accept", handoffSource.S("nextAction"));
        Assert.Equal(data.User(TestData.Omar), handoffSource["nextActors"]![0]!.G("id"));
        var row = Assert.Single((await Get(Root(s) + "/submissions"))["items"]!.AsArray())!;
        Assert.Equal(3, row["delivery"]!.I("blockingCheckCount"));
        var manifest = Assert.Single(delivery["manifest"]!.AsArray())!;
        Assert.Equal("A", manifest["recordedRevision"]!.S("revision")); Assert.Equal("B", manifest["currentRevision"]!.S("revision")); Assert.True(manifest["revisionMismatch"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Optional_failure_does_not_become_a_required_blocker_and_issued_evidence_stays_immutable()
    {
        var s = await New(); await Optional(s, SubmissionCheckStatus.Fail); await Enable();
        var detail = await Get(Path(s)); Assert.Equal(SubmissionStatus.Ready, detail.S("effectiveStatus"));
        Assert.Equal(0, detail["delivery"]!["summary"]!.I("blockingCheckCount"));
        var optional = detail["delivery"]!["checks"]!.AsArray().Single(c => c!.G("checkId") == s.Optional)!;
        Assert.Equal(SubmissionCheckStatus.Fail, optional.S("state")); Assert.False(optional["blocking"]!.GetValue<bool>());
        await Post(TestData.Pm, Path(s) + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), 1,
            detail["readiness"]!.S("fingerprint"), "Municipality", "https://example.test/issued-transmittal", null));
        var issue = (await Get(Path(s)))["issue"]!.ToJsonString();
        await Publish(s);
        var issued = await Get(Path(s)); Assert.Equal(issue, issued["issue"]!.ToJsonString());
        Assert.Equal("Available", issued["delivery"]!.S("issueAvailability")); Assert.False(issued["delivery"]!["summary"]!["available"]!.GetValue<bool>());
        Assert.Equal("B", Assert.Single(issued["delivery"]!["manifest"]!.AsArray())!["currentRevision"]!.S("revision"));
    }

    [Fact]
    public async Task Failures_keep_exact_manifest_membership_and_never_attach_to_an_unrelated_deliverable()
    {
        var s = await New(); var other = await Document(s.Project, "Civil", TestData.Marc, TestData.Alex, "Unrelated civil sheet");
        var civil = data.ProjectDiscipline(s.Project.Id, "Civil");
        var otherRevision = (await Post(TestData.Alex, Root(s) + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), other, Version<Deliverable>(other), civil,
            data.User(TestData.Alex), "Deliverable", "civil-other", "Unrelated civil sheet", "A", "https://example.test/civil-other.pdf", "Design team", "Submission", null, null, null, null, null, null))).G("id");
        await Post(TestData.Omar, Path(s) + "/manifest", new SubmissionEndpoints.ManifestBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), [new(s.Revision), new(otherRevision)], "Include the exact civil sheet"));
        await Publish(s); await Enable();
        var detail = await Get(Path(s)); var checks = detail["checks"]!.AsArray();
        var otherCheck = checks.Single(c => c!.S("kind") == SubmissionCheckKind.CurrentRevision && c.G("sourceId") == other)!;
        var fact = detail["delivery"]!["checks"]!.AsArray().Single(c => c!.G("checkId") == otherCheck.G("id"))!;
        Assert.Equal(SubmissionCheckStatus.Pass, fact.S("state")); Assert.False(fact["blocking"]!.GetValue<bool>()); Assert.Empty(fact["reasons"]!.AsArray());
        Assert.Equal(2, detail["delivery"]!["manifest"]!.AsArray().Count);
    }

    [Fact]
    public async Task Missing_and_foreign_revision_details_are_unavailable_without_substituting_names_or_passing_checks()
    {
        var s = await New(); var foreign = await New(); await Enable();
        var foreignUrl = $"https://example.test/{foreign.Deliverable}/A.pdf";
        await f.DbAsync(async db => { (await db.SourceHeads.SingleAsync(h => h.ProjectId == s.Project.Id && h.Identity == $"deliverable:{s.Deliverable}")).CurrentRevisionId = foreign.Revision; return await db.SaveChangesAsync(); });
        var detail = await Get(Path(s)); var member = Assert.Single(detail["delivery"]!["manifest"]!.AsArray())!;
        Assert.Null(member["currentRevision"]); Assert.Null(member["revisionMismatch"]); Assert.DoesNotContain(foreignUrl, detail.ToJsonString()); Assert.DoesNotContain(foreign.Project.ProjectNumber, detail.ToJsonString());
        // Deliberately corrupt an imported fixture reference outside the immutable-evidence command path.
        // Normal tracked edits must continue to reject changes to published evidence.
        await f.DbAsync(db => db.SourceRevisions.Where(r => r.Id == s.Revision).ExecuteUpdateAsync(set => set.SetProperty(r => r.DeliverableId, (Guid?)null)));
        var missing = await Get(Path(s)); Assert.Null(Assert.Single(missing["delivery"]!["manifest"]!.AsArray())!["recordedRevision"]);
        Assert.True(missing["delivery"]!["summary"]!.I("unknownCheckCount") > 0);
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(d => d.Id == s.Deliverable)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var deleted = await Get(Path(s)); Assert.Equal(1, deleted["delivery"]!.I("unavailableDeliverables")); Assert.Empty(deleted["delivery"]!["statusCounts"]!.AsArray());
    }

    [Fact]
    public async Task Enabled_global_blocker_order_precedes_paging_and_retains_exact_filters()
    {
        var ready = await New(title: "Early ready package", target: new(2026, 9, 20));
        var blocked = await New(ready.Project, "Later blocked package", new(2026, 10, 20)); await Block(blocked);
        var path = Root(ready) + "/submissions?pageSize=1";
        Assert.Equal(ready.Package, Assert.Single((await Get(path))["items"]!.AsArray())!.G("id"));
        await Enable(); var first = await Get(path); Assert.Equal(2, first.I("totalCount")); Assert.Equal(blocked.Package, Assert.Single(first["items"]!.AsArray())!.G("id"));
        Assert.Equal(ready.Package, Assert.Single((await Get(path + "&page=2"))["items"]!.AsArray())!.G("id"));
        Assert.Equal(ready.Package, Assert.Single((await Get(path + "&Status=Ready"))["items"]!.AsArray())!.G("id"));
        Assert.Equal(blocked.Package, Assert.Single((await Get(path + "&Q=Later&TargetFrom=2026-10-01"))["items"]!.AsArray())!.G("id"));
        var readonlyDetail = await Get(Path(blocked), TestData.Rita); Assert.False(readonlyDetail["canCoordinate"]!.GetValue<bool>()); Assert.False(readonlyDetail["canAuthorise"]!.GetValue<bool>());
    }

    [Fact]
    public async Task New_presentation_is_omitted_off_and_every_legacy_detail_field_is_preserved()
    {
        var s = await New(); var off = await Get(Path(s)); var list = await Get(Root(s) + "/submissions");
        Assert.Null(off["delivery"]); Assert.Null(Assert.Single(list["items"]!.AsArray())!["delivery"]);
        await Enable(); var on = await Get(Path(s)); Assert.NotNull(on["delivery"]);
        on.AsObject().Remove("delivery"); on.AsObject().Remove("context");
        foreach (var check in on["checks"]!.AsArray()) { check!.AsObject().Remove("context"); check.AsObject().Remove("effectiveStatus"); }
        Assert.Equal(off.ToJsonString(), on.ToJsonString());
        var enabledList = await Get(Root(s) + "/submissions"); foreach (var row in enabledList["items"]!.AsArray()) row!.AsObject().Remove("delivery");
        Assert.Equal(list.ToJsonString(), enabledList.ToJsonString());
    }
}
