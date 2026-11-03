using System.Text.Json.Nodes;
using Hub.Api;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S04: presentation follows the canonical independent-review guards, including degraded legacy assignments.
public sealed class WorkReviewerPresentationTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkReviewerPresentationTests() => data = new(f);
    public void Dispose() => f.Dispose();
    sealed record Setup(Project Project, Guid Deliverable, Guid Revision, Guid Review, Guid Assignment);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Get(string path, int status = 200) => await (await f.As(TestData.Marc).GetAsync(path)).Json(status);
    async Task Set(string key, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    static string Root(Setup s) => $"/api/v1/projects/{s.Project.Id}";
    async Task<Setup> New()
    {
        var p = await data.Project();
        var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var deliverable = (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/deliverables", new { name = "Survey source", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        await data.NewTask(p.Id, TestData.Marc, new { name = "Produce the survey", deliverableId = deliverable, assigneeId = data.User(TestData.Alex) });
        var revision = (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, data.User(TestData.Alex), "Deliverable", "survey", "Survey source", "A", "https://example.test/survey-A.pdf",
            "Survey team", "Electrical interface", null, null, null, null, null, null))).G("id");
        var review = (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Survey interface review",
            "Check the current survey", civil, data.User(TestData.Marc), [revision], [new(electrical, data.User(TestData.Omar), new(2026, 9, 24))], true, null))).G("id");
        var assignment = f.Db(db => db.DisciplineReviews.Single(a => db.ReviewPackages.Any(r => r.Id == review && r.CurrentRoundId == a.RoundId)).Id);
        return new(p, deliverable, revision, review, assignment);
    }
    async Task<JsonNode> Detail(Setup s) => await Get(Root(s) + $"/reviews/{s.Review}");
    static JsonNode Presentation(JsonNode detail) => Assert.Single(detail["presentation"]!.AsArray())!;

    [Fact]
    public async Task Flag_off_omits_guidance_and_presentation_and_preserves_the_prior_detail()
    {
        var s = await New();
        var off = await Detail(s); Assert.Null(off["presentation"]);
        await Get(Root(s) + $"/review-assignment-guidance?sourceRevisionIds={s.Revision}", 404);
        await Set("flag.screen_patterns", true);
        var on = await Detail(s); Assert.True(Presentation(on)["startAllowed"]!.GetValue<bool>());
        on.AsObject().Remove("presentation");
        Assert.Equal(off.ToJsonString(), on.ToJsonString());
        var guidance = await Get(Root(s) + $"/review-assignment-guidance?sourceRevisionIds={s.Revision}");
        Assert.Contains(data.User(TestData.Alex), guidance["excludedReviewerIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        Assert.Contains(data.User(TestData.Marc), guidance["excludedReviewerIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        var source = Assert.Single(guidance["sources"]!.AsArray())!;
        Assert.True(source["available"]!.GetValue<bool>());
        Assert.Equal(data.User(TestData.Alex), Assert.Single(source["performers"]!.AsArray())!["person"]!.G("id"));
    }

    [Fact]
    public async Task Inactive_required_reviewer_is_named_and_disables_start_without_weakening_the_command()
    {
        var s = await New(); await Set("flag.screen_patterns", true);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == data.User(TestData.Omar))).IsActive = false; return await db.SaveChangesAsync(); });
        var presentation = Presentation(await Detail(s));
        Assert.Equal([s.Assignment], presentation["missingReviewerAssignmentIds"]!.AsArray().Select(x => x!.GetValue<Guid>()).ToArray());
        Assert.Equal("Inactive", Assert.Single(presentation["reviewers"]!.AsArray())!["reviewer"]!.S("state"));
        Assert.False(presentation["startAllowed"]!.GetValue<bool>());
        Assert.Equal(Text.Get("coord.person"), presentation.S("startReason"));
        await Post(TestData.Marc, Root(s) + $"/reviews/{s.Review}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(s.Review), "start", "Start this round"), 400);
        Assert.Equal(ReviewStatus.Draft, f.Db(db => db.ReviewPackages.Single(p => p.Id == s.Review).Status));
        Assert.Null(f.Db(db => db.ReviewRounds.Single(r => db.ReviewPackages.Any(p => p.Id == s.Review && p.CurrentRoundId == r.Id)).StartedAt));
    }

    [Fact]
    public async Task Author_exclusion_and_explicit_self_review_setting_follow_the_same_server_rule()
    {
        var s = await New(); await Set("flag.screen_patterns", true);
        var path = Root(s) + $"/reviews/{s.Review}/assignments/{s.Assignment}/assign";
        await Post(TestData.Marc, path, new ReviewEndpoints.ReassignBody(Guid.NewGuid(), Version<DisciplineReview>(s.Assignment), data.User(TestData.Alex), "Review the survey"), 400);
        Assert.Equal(data.User(TestData.Omar), f.Db(db => db.DisciplineReviews.Single(a => a.Id == s.Assignment).ReviewerId));
        await Set("allow_self_review", true);
        await Post(TestData.Marc, path, new ReviewEndpoints.ReassignBody(Guid.NewGuid(), Version<DisciplineReview>(s.Assignment), data.User(TestData.Alex), "Self-review explicitly allowed"));
        var guidance = Presentation(await Detail(s))["guidance"]!;
        Assert.True(guidance["allowSelfReview"]!.GetValue<bool>());
        Assert.Empty(guidance["excludedReviewerIds"]!.AsArray());
        Assert.Contains(data.User(TestData.Alex), guidance["authorIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        await Set("allow_self_review", false);
        Assert.False(Presentation(await Detail(s))["startAllowed"]!.GetValue<bool>());
        await Post(TestData.Marc, Root(s) + $"/reviews/{s.Review}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(s.Review), "start", "Try an independent review"), 400);
    }

    [Fact]
    public async Task Deleted_source_stays_unavailable_and_another_projects_revision_is_never_substituted()
    {
        var s = await New(); var other = await New(); await Set("flag.screen_patterns", true);
        await Get(Root(s) + $"/review-assignment-guidance?sourceRevisionIds={other.Revision}", 404);
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(d => d.Id == s.Deliverable)).DeletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        var presentation = Presentation(await Detail(s));
        Assert.False(presentation["guidance"]!["sourcesAvailable"]!.GetValue<bool>());
        var source = Assert.Single(presentation["guidance"]!["sources"]!.AsArray())!;
        Assert.False(source["available"]!.GetValue<bool>()); Assert.Empty(source["performers"]!.AsArray());
        Assert.False(presentation["startAllowed"]!.GetValue<bool>());
        Assert.Equal(Text.Get("review.manifest_rule"), presentation.S("startReason"));
    }
}
