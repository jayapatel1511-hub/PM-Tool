using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Jay's Submission Gate relationship (docs/decisions.md, 2026-10-01): PM or responsible lead links work to prerequisite
/// packages in the same project; the gate passes only when every linked package, followed through supersession, is Issued.
[Collection("api")]
public sealed class ReadinessSubmissionGateTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    sealed record World(Project P, string Root, Guid Milestone, Guid Deliverable, Guid Revision, Guid Task, string Readiness);

    async Task<World> New()
    {
        var p = await data.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var civil = data.ProjectDiscipline(p.Id, "Civil");
        var alex = data.User(TestData.Alex);
        var milestone = (await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Permit submission",
            MilestoneType.DesignSubmission, new DateOnly(2026, 10, 30), null, null, null, true), 201)).G("id");
        var deliverable = (await Post(TestData.Marc, root + "/deliverables", new { name = "Survey package", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = alex, revision = "A", requiresReview = false }, 201)).G("id");
        var revision = (await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, alex, "Deliverable", "survey", "Survey package", "A", "https://example.test/survey-a.pdf",
            "Survey team", "Corridor", null, null, null, null, null, null))).G("id");
        var task = (await data.NewTask(p.Id, extra: new { assigneeId = alex })).G("id");
        var readiness = $"{root}/readiness/Task/{task}";
        var assessment = (await Post(TestData.Alex, readiness, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(task),
            "Grading layout", "Layout checked against the permit set"))).G("id");
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment).ToListAsync())
                check.Applies = check.Code == ReadinessCheckCode.SubmissionGate ? null : check.Code == ReadinessCheckCode.ProductionOwner;
            return await db.SaveChangesAsync();
        });
        return new World(p, root, milestone, deliverable, revision, task, readiness);
    }

    async Task<Guid> Package(World w, Guid? supersedes = null) =>
        (await Post(TestData.Marc, w.Root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Permit package", "Permit review",
            "Municipality", data.User(TestData.Marc), w.Milestone, new DateOnly(2026, 10, 30), [new(w.Revision)], [], supersedes, null))).G("id");

    async Task Issue(World w, Guid package)
    {
        var path = $"{w.Root}/submissions/{package}";
        await Post(TestData.Marc, path + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(package), null));
        var fingerprint = (await (await f.As(TestData.Pm).GetAsync(path)).Json())["readiness"]!["fingerprint"]!.GetValue<string>();
        await Post(TestData.Pm, path + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(package), 1, fingerprint,
            "Municipality", "https://example.test/transmittal", null));
    }

    ReadinessEndpoints.PrerequisiteBody Link(World w, Guid package) =>
        new(Guid.NewGuid(), Version<WorkTask>(w.Task), package, "Permit set must be issued before grading starts");

    async Task<(string State, string[] Blocked, string[] Unknown)> State(World w)
    {
        var detail = await (await f.As(TestData.Alex).GetAsync(w.Readiness)).Json();
        string[] Codes(string key) => detail[key]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        return (detail["assessment"]!.S("state"), Codes("blocked"), Codes("unknown"));
    }

    async Task Applicability(World w, bool applies)
    {
        var assessment = f.Db(db => db.ReadinessAssessments.Single(a => a.TargetId == w.Task));
        var check = f.Db(db => db.ReadinessChecks.Single(c => c.AssessmentId == assessment.Id && c.Code == ReadinessCheckCode.SubmissionGate));
        await Post(TestData.Pm, $"{w.Readiness}/checks/Submission%20Gate/applicability", new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(),
            assessment.RowVersion, check.RowVersion, applies, applies ? "Permit submission is a prerequisite" : "No submission precedes this work", null));
    }

    [Fact]
    public async Task Gate_follows_linked_packages_through_issue_supersession_and_cancellation()
    {
        var w = await New();
        Assert.Equal(ReadinessState.NeedsAssessment, (await State(w)).State);
        await Applicability(w, false);
        Assert.Equal(ReadinessState.Ready, (await State(w)).State); // no link: the reasoned Not Applicable stands
        await Applicability(w, true);
        await f.DbAsync(async db => { (await db.ReadinessChecks.SingleAsync(c => c.Code == ReadinessCheckCode.SubmissionGate &&
            db.ReadinessAssessments.Any(a => a.Id == c.AssessmentId && a.TargetId == w.Task))).Satisfied = true; return await db.SaveChangesAsync(); });
        var unlinked = await State(w);
        Assert.Equal(ReadinessState.NeedsAssessment, unlinked.State); // a stored result never satisfies the gate
        Assert.Contains(ReadinessCheckCode.SubmissionGate, unlinked.Unknown);

        var first = await Package(w);
        var path = $"{w.Readiness}/submission-prerequisites";
        var link = Link(w, first);
        var linkId = (await Post(TestData.Pm, path, link)).G("id");
        Assert.Equal(linkId, (await Post(TestData.Pm, path, link)).G("id")); // retried command returns the same link
        await Post(TestData.Pm, path, Link(w, first), 409);
        var draft = await State(w);
        Assert.Equal(ReadinessState.NotReady, draft.State);
        Assert.Contains(ReadinessCheckCode.SubmissionGate, draft.Blocked);

        await Issue(w, first);
        Assert.Equal(ReadinessState.Ready, (await State(w)).State);
        var successor = await Package(w, first);
        Assert.Equal(ReadinessState.Ready, (await State(w)).State); // the Issued package still governs until it is superseded
        await Issue(w, successor);
        Assert.Equal(SubmissionStatus.Superseded, f.Db(db => db.SubmissionPackages.Single(p => p.Id == first).Status));
        Assert.Equal(ReadinessState.Ready, (await State(w)).State);
        var listed = (await (await f.As(TestData.Alex).GetAsync(path)).Json()).AsArray().Single()!;
        Assert.Equal(successor, listed["effective"]!.G("id"));
        Assert.Equal(SubmissionStatus.Issued, listed["effective"]!.S("status"));

        var cancelled = await Package(w);
        var second = (await Post(TestData.Marc, path, Link(w, cancelled))).G("id");
        await Post(TestData.Pm, $"{w.Root}/submissions/{cancelled}/cancel", new SubmissionEndpoints.CancelBody(Guid.NewGuid(),
            Version<SubmissionPackage>(cancelled), "Package withdrawn by the municipality"));
        Assert.Equal(ReadinessState.NotReady, (await State(w)).State);
        var remove = new ReadinessEndpoints.PrerequisiteRemoveBody(Guid.NewGuid(), Version<ReadinessSubmissionPrerequisite>(second), "Cancelled package no longer applies");
        await Post(TestData.Pm, $"{path}/{second}/remove", remove with { RequestId = Guid.NewGuid(), RowVersion = -1 }, 409);
        await Post(TestData.Alex, $"{path}/{second}/remove", remove with { RequestId = Guid.NewGuid() }, 403);
        await Post(TestData.Pm, $"{path}/{second}/remove", remove);
        await Post(TestData.Pm, $"{path}/{second}/remove", remove with { RequestId = Guid.NewGuid(), RowVersion = Version<ReadinessSubmissionPrerequisite>(second) }, 400);
        Assert.Equal(ReadinessState.Ready, (await State(w)).State);
        var removed = f.Db(db => db.ReadinessSubmissionPrerequisites.Single(l => l.Id == second));
        Assert.Equal(("Cancelled package no longer applies", data.User(TestData.Pm)), (removed.RemovalReason, removed.RemovedBy!.Value));
        Assert.Equal(2, (await (await f.As(TestData.Alex).GetAsync(path)).Json()).AsArray().Count); // removed links stay in history
    }

    [Fact]
    public async Task Links_are_refused_for_self_gating_other_projects_stale_targets_and_unauthorised_roles()
    {
        var w = await New();
        var package = await Package(w);
        var path = $"{w.Readiness}/submission-prerequisites";
        await Post(TestData.Alex, path, Link(w, package), 403);          // the performer is not the PM or responsible lead
        await Post(TestData.Omar, path, Link(w, package), 403);          // another discipline's lead
        await Post(TestData.Rita, path, Link(w, package), 403);          // Read Only
        await Post(TestData.Pm, path, Link(w, package) with { TargetRowVersion = -1 }, 409);
        var foreign = await New();
        var refused = await Post(TestData.Pm, path, Link(w, await Package(foreign)), 400);
        Assert.NotNull(refused["errors"]?["packageId"]);

        var own = $"{w.Root}/readiness/Deliverable/{w.Deliverable}/submission-prerequisites";
        var self = await Post(TestData.Pm, own, new ReadinessEndpoints.PrerequisiteBody(Guid.NewGuid(), Version<Deliverable>(w.Deliverable), package,
            "Gate the survey on its own package"), 400);
        Assert.NotNull(self["errors"]?["packageId"]);
        var child = (await data.NewTask(w.P.Id, extra: new { assigneeId = data.User(TestData.Alex), deliverableId = w.Deliverable })).G("id");
        await Post(TestData.Pm, $"{w.Root}/readiness/Task/{child}/submission-prerequisites", new ReadinessEndpoints.PrerequisiteBody(Guid.NewGuid(),
            Version<WorkTask>(child), package, "Gate the survey task on its own package"), 400);
        Assert.False(f.Db(db => db.ReadinessSubmissionPrerequisites.Any(l => l.ProjectId == w.P.Id)));

        await Post(TestData.Marc, path, Link(w, package)); // the responsible Civil lead may link
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == w.P.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        await Post(TestData.Pm, path, Link(w, package), 403);
        Assert.Single(f.Db(db => db.ReadinessSubmissionPrerequisites.Where(l => l.ProjectId == w.P.Id).ToList()));
    }

    [Fact]
    public async Task Linked_package_that_later_lists_the_output_leaves_the_gate_unknown()
    {
        var w = await New();
        var package = await Package(w);
        var path = $"{w.Readiness}/submission-prerequisites";
        await Post(TestData.Pm, path, Link(w, package));
        await Issue(w, package);
        Assert.Equal(ReadinessState.Ready, (await State(w)).State);
        // The task later joins the deliverable that the issued package lists, so the link would gate the output on itself.
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == w.Task)).DeliverableId = w.Deliverable; return await db.SaveChangesAsync(); });
        var state = await State(w);
        Assert.Equal(ReadinessState.NeedsAssessment, state.State);
        Assert.Contains(ReadinessCheckCode.SubmissionGate, state.Unknown);
        Assert.True((await (await f.As(TestData.Alex).GetAsync(path)).Json()).AsArray().Single()!["listsOutput"]!.GetValue<bool>());
    }
}
