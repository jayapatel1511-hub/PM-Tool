using System.Text.Json.Nodes;
using System.Net;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-SCR01 detail context: the optional, read-only context is dark by default and
/// appears only after the existing record/project authorization has succeeded.
public sealed class RecordContextDetailTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;

    public RecordContextDetailTests() => data = new(f);
    public void Dispose() => f.Dispose();

    async Task EnableContext() =>
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();

    async Task<JsonNode> NewDeliverable(Project project, bool requiresReview = true, Guid? reviewer = null) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Context deliverable", projectDisciplineId = data.ProjectDiscipline(project.Id, "Civil"),
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), reviewerId = reviewer,
            requiresReview,
        }).Result.Json(201);

    async Task<JsonNode> Detail(string path, string user = TestData.Pm) => await (await f.As(user).GetAsync(path)).Json();

    [Fact]
    public async Task Deliverable_detail_omits_context_when_off_and_includes_it_when_on()
    {
        var project = await data.Project();
        var created = await NewDeliverable(project);

        var off = await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{created.G("id")}");
        Assert.Null((await off.Json())["context"]);
        await EnableContext();
        Assert.NotNull((await (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{created.G("id")}")).Json())["context"]);
    }

    [Fact]
    public async Task Deliverable_context_preserves_owner_reviewer_and_review_gate()
    {
        var project = await data.Project();
        var created = await NewDeliverable(project, requiresReview: true, reviewer: data.User(TestData.Diane));
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.Status = DeliverableStatus.InReview;
            d.PreviousStatus = DeliverableStatus.OnHold;
            await db.SaveChangesAsync();
            return 0;
        });
        await EnableContext();
        var context = (await Detail($"/api/v1/deliverables/{created.G("id")}"))["context"]!;
        Assert.Equal(data.User(TestData.Alex), context["owner"]!.G("id"));
        Assert.Equal(data.User(TestData.Alex), context["nextActors"]![0]!.G("id"));
        Assert.Equal(RecordContextAction.Transition, context.S("nextAction"));
        Assert.True(context["requiresReviewGate"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Deliverable_context_reports_unassigned_inactive_and_terminal_owner_states()
    {
        var project = await data.Project();
        var created = await NewDeliverable(project, requiresReview: false);
        await EnableContext();
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.OwnerId = null;
            await db.SaveChangesAsync();
            return 0;
        });
        var unassigned = (await Detail($"/api/v1/deliverables/{created.G("id")}"))["context"]!;
        Assert.Equal("Unassigned", unassigned["owner"]!.S("state"));
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.OwnerId = data.User(TestData.Alex);
            var user = await db.Users.SingleAsync(x => x.Id == d.OwnerId);
            user.IsActive = false;
            await db.SaveChangesAsync();
            return 0;
        });
        var inactive = (await Detail($"/api/v1/deliverables/{created.G("id")}"))["context"]!;
        Assert.Equal("Inactive", inactive["owner"]!.S("state"));
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.Status = DeliverableStatus.Issued;
            await db.SaveChangesAsync();
            return 0;
        });
        var terminal = (await Detail($"/api/v1/deliverables/{created.G("id")}"))["context"]!;
        Assert.True(terminal["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, terminal.S("nextAction"));
    }

    [Fact]
    public async Task Deliverable_context_reports_required_review_package_gate_and_revision_match()
    {
        var project = await data.Project();
        var discipline = data.ProjectDiscipline(project.Id, "Civil");
        var created = await NewDeliverable(project, requiresReview: false);
        var deliverableId = created.G("id");
        await f.DbAsync(async db =>
        {
            var work = await db.Deliverables.SingleAsync(d => d.Id == deliverableId);
            work.Revision = "A"; work.Status = DeliverableStatus.InProgress;
            var package = new ReviewPackage { ProjectId = project.Id, Key = "REV-CONTEXT", Seq = 1, Title = "Context review",
                Purpose = "Confirm package", CoordinatorId = data.User(TestData.Marc), ProjectDisciplineId = discipline,
                Status = ReviewStatus.Draft, RoundNumber = 1, RequiredForIssue = true };
            db.ReviewPackages.Add(package);
            await db.SaveChangesAsync();
            var round = new ReviewRound { ProjectId = project.Id, PackageId = package.Id, Number = 1, Purpose = package.Purpose, Status = ReviewStatus.Draft };
            db.ReviewRounds.Add(round);
            await db.SaveChangesAsync();
            var source = new SourceRevision { ProjectId = project.Id, DeliverableId = deliverableId, SourceRowVersion = 1,
                SourceIdentity = $"context-{Guid.NewGuid():N}", SourceKey = "CONTEXT-SOURCE", ExternalIdentifier = "CTX-A",
                Issuer = "Synthetic review", Scope = "Context gate", Title = "Context source", Revision = "A",
                Url = "https://review.example.test/context-a", IdentityHash = Guid.NewGuid().ToString("N") };
            db.SourceRevisions.Add(source);
            await db.SaveChangesAsync();
            db.SourceHeads.Add(new SourceHead { ProjectId = project.Id, Identity = Coordination.Identity(source), CurrentRevisionId = source.Id,
                OwnerId = data.User(TestData.Alex), ProjectDisciplineId = discipline });
            db.ReviewManifestItems.Add(new ReviewManifestItem { ProjectId = project.Id, RoundId = round.Id,
                DeliverableId = deliverableId, SourceRevisionId = source.Id, AuthorIds = [data.User(TestData.Alex)] });
            package.CurrentRoundId = round.Id;
            (await db.Deliverables.SingleAsync(d => d.Id == deliverableId)).RequiredReviewPackageId = package.Id;
            await db.SaveChangesAsync();
            return 0;
        });
        await EnableContext();
        var draft = (await Detail($"/api/v1/deliverables/{deliverableId}"))["context"]!;
        Assert.False(draft["reviewPackageGate"]!["satisfied"]!.GetValue<bool>());
        Assert.DoesNotContain(DeliverableStatus.ReadyToIssue, draft["nextTransitions"]!.AsArray().Select(x => x!.GetValue<string>()));

        await f.DbAsync(async db =>
        {
            var package = await db.ReviewPackages.SingleAsync(p => p.Key == "REV-CONTEXT");
            package.Status = ReviewStatus.Approved;
            (await db.ReviewRounds.SingleAsync(r => r.PackageId == package.Id)).Status = ReviewStatus.Approved;
            await db.SaveChangesAsync();
            return 0;
        });
        var approved = (await Detail($"/api/v1/deliverables/{deliverableId}"))["context"]!;
        Assert.True(approved["reviewPackageGate"]!["satisfied"]!.GetValue<bool>());
        Assert.Contains(DeliverableStatus.ReadyToIssue, approved["nextTransitions"]!.AsArray().Select(x => x!.GetValue<string>()));

        await f.DbAsync(async db =>
        {
            (await db.Deliverables.SingleAsync(d => d.Id == deliverableId)).Revision = "B";
            await db.SaveChangesAsync();
            return 0;
        });
        var mismatch = (await Detail($"/api/v1/deliverables/{deliverableId}"))["context"]!;
        Assert.False(mismatch["reviewPackageGate"]!["satisfied"]!.GetValue<bool>());
        Assert.DoesNotContain(DeliverableStatus.ReadyToIssue, mismatch["nextTransitions"]!.AsArray().Select(x => x!.GetValue<string>()));
    }

    [Fact]
    public async Task Milestone_context_uses_date_state_and_explicit_terminal_flags_but_null_state_is_unassessed()
    {
        var project = await data.Project();
        var created = await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones", new
            { name = "Context milestone", milestoneType = "Design Submission", date = "2026-12-01" }).Result.Json(201);
        await EnableContext();
        var noState = (await Detail($"/api/v1/milestones/{created.G("id")}"))["context"]!;
        Assert.Equal(RecordContextAction.NotAssessed, noState.S("nextAction"));
        Assert.Equal("Unknown", noState.S("actorState"));
        await f.DbAsync(async db =>
        {
            db.MilestoneStates.Add(new MilestoneState { MilestoneId = created.G("id"), ProjectId = project.Id, Status = MilestoneStatus.OnTrack });
            await db.SaveChangesAsync();
            return 0;
        });
        var withState = (await Detail($"/api/v1/milestones/{created.G("id")}"))["context"]!;
        Assert.Equal("Active", withState.S("actorState"));
        Assert.Equal(RecordContextAction.ReviewPrerequisites, withState.S("nextAction"));
        Assert.Equal("2026-12-01", withState.S("neededBy"));
        await f.DbAsync(async db =>
        {
            var m = await db.Milestones.SingleAsync(x => x.Id == created.G("id"));
            m.IsComplete = true;
            await db.SaveChangesAsync();
            return 0;
        });
        var terminal = (await Detail($"/api/v1/milestones/{created.G("id")}"))["context"]!;
        Assert.True(terminal["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, terminal.S("nextAction"));
    }

    [Fact]
    public async Task Milestone_detail_omits_context_when_off_and_includes_it_when_on()
    {
        var project = await data.Project();
        var created = await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones", new
            { name = "Context milestone", milestoneType = "Design Submission", date = "2026-12-01" }).Result.Json(201);

        Assert.Null((await (await f.As(TestData.Pm).GetAsync($"/api/v1/milestones/{created.G("id")}")).Json())["context"]);
        await EnableContext();
        Assert.NotNull((await (await f.As(TestData.Pm).GetAsync($"/api/v1/milestones/{created.G("id")}")).Json())["context"]);
    }

    [Fact]
    public async Task Decision_detail_omits_context_when_off_and_includes_it_when_on()
    {
        var project = await data.Project();
        var created = await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "Context decision", description = "Context test", ownerUserId = data.User(TestData.Alex),
            requiredByDate = "2026-12-01", impactLevel = "Medium", impactDescription = "Context test",
        }).Result.Json(201);

        Assert.Null((await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{created.G("id")}")).Json())["context"]);
        await EnableContext();
        Assert.NotNull((await (await f.As(TestData.Pm).GetAsync($"/api/v1/decisions/{created.G("id")}")).Json())["context"]);
    }

    [Fact]
    public async Task Decision_context_exposes_external_owner_and_inactive_state_and_terminal_action()
    {
        var project = await data.Project();
        var party = await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/external-parties", new { name = "Context party", isClient = true }).Result.Json(201);
        var created = await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "External context decision", description = "Context test", ownerExternalPartyId = party.G("id"),
            requiredByDate = "2026-12-01", impactLevel = "Medium", impactDescription = "Context test",
        }).Result.Json(201);
        await f.DbAsync(async db =>
        {
            var p = await db.ExternalParties.SingleAsync(x => x.Id == party.G("id"));
            p.IsActive = false;
            await db.SaveChangesAsync();
            return 0;
        });
        await EnableContext();
        var context = (await Detail($"/api/v1/decisions/{created.G("id")}"))["context"]!;
        Assert.Equal("ExternalParty", context["owner"]!["kind"]!.GetValue<string>());
        Assert.Equal("Inactive", context["owner"]!["state"]!.GetValue<string>());
        await f.DbAsync(async db =>
        {
            var d = await db.Decisions.SingleAsync(x => x.Id == created.G("id"));
            d.Status = DecisionStatus.Decided;
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(RecordContextAction.None, (await Detail($"/api/v1/decisions/{created.G("id")}"))["context"]!.S("nextAction"));
    }

    [Fact]
    public async Task Context_does_not_change_permissions_and_restricted_or_deleted_records_remain_404()
    {
        var project = await data.Project();
        var created = await NewDeliverable(project);
        var off = await Detail($"/api/v1/deliverables/{created.G("id")}");
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == project.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync($"/api/v1/deliverables/{created.G("id")}")).StatusCode);
        await EnableContext();
        var on = await Detail($"/api/v1/deliverables/{created.G("id")}");
        Assert.Equal(off["permissions"]!.ToJsonString(), on["permissions"]!.ToJsonString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync($"/api/v1/deliverables/{created.G("id")}")).StatusCode);
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{created.G("id")}")).StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_detail_remains_404_with_context_flag_off_and_on()
    {
        var project = await data.Project();
        var created = await NewDeliverable(project);
        await f.DbAsync(async db =>
        {
            var d = await db.Deliverables.SingleAsync(x => x.Id == created.G("id"));
            d.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{created.G("id")}")).StatusCode);
        await EnableContext();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{created.G("id")}")).StatusCode);
    }
}
