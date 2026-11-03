using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class ReviewDisciplineSummaryTests
{
    [Fact] // AC-SCR-63: arithmetic midpoint and zero are real values; no sample is unknown.
    public void Median_preserves_even_fractional_and_zero_samples()
    {
        Assert.Null(ReviewDisciplineSummaryEndpoints.Median([]));
        Assert.Equal(0m, ReviewDisciplineSummaryEndpoints.Median([0m]));
        Assert.Equal(1.5m, ReviewDisciplineSummaryEndpoints.Median([2m, 1m]));
        Assert.Equal(2m, ReviewDisciplineSummaryEndpoints.Median([3m, 1m, 2m]));
    }

    [Fact] // AC-SCR-63: actual three packages and canonical current-round approvals, never a response-based score.
    public async Task Three_packages_approved_after_one_two_three_rounds_have_median_two_and_max_three()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var project = await t.Data.Project();
        foreach (var rounds in new[] { 1, 2, 3 }) { var s = await t.New(project, $"Synthetic {rounds}-round package"); await Approve(t, s, rounds); }
        var before = t.State(project.Id); var result = await Summary(t, project.Id, TestData.Rita); var civil = Row(result, t.Data.ProjectDiscipline(project.Id, "Civil"));
        Assert.Equal(3, civil.I("approvedPackageCount")); Assert.Equal(3, civil.I("eligiblePackageCount")); Assert.Equal(0, civil.I("nonApplicablePackageCount"));
        Assert.Equal(3, civil.I("packageSampleCount")); Assert.Equal(2m, civil["medianRoundsToApproval"]!.GetValue<decimal>()); Assert.Equal(3, civil.I("maximumRoundsToApproval"));
        Assert.Equal(6, civil.I("startedAssignmentRoundCount")); Assert.Equal(6, civil.I("timedAssignmentRoundCount")); Assert.Equal(1m, civil["medianDaysPerRound"]!.GetValue<decimal>());
        Assert.DoesNotContain("reviewer", result.ToJsonString(), StringComparison.OrdinalIgnoreCase); Assert.Equal(before, t.State(project.Id));
    }

    [Fact] // AC-SCR-63: missing time is unavailable; packages without the discipline are non-applicable.
    public async Task Required_scope_and_partial_timing_have_separate_exact_denominators()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var project = await t.Data.Project(); var a = await t.New(project); await Approve(t, a, 1);
        var b = await t.New(project, "Electrical-only approved current scope");
        await t.Post(TestData.Pm, t.Root(b) + $"/reviews/{b.Package}/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(), t.Version<ReviewPackage>(b.Package),
            "Electrical review only", [b.Source], [new(b.Electrical, t.Data.User(TestData.Omar), new(2026, 11, 12))], "The Civil scope is removed after PM review", "No Civil scope or open findings remain"));
        await Start(t, b); await Decide(t, b, "Electrical", "Approved");
        await t.F.DbAsync(async db => { var assignment = await db.DisciplineReviews.SingleAsync(row => row.RoundId == a.Round && row.ProjectDisciplineId == a.Civil); assignment.DecidedAt = null; return await db.SaveChangesAsync(); });
        var result = await Summary(t, project.Id); var civil = Row(result, a.Civil);
        Assert.Equal(2, civil.I("approvedPackageCount")); Assert.Equal(1, civil.I("eligiblePackageCount")); Assert.Equal(1, civil.I("nonApplicablePackageCount"));
        Assert.Equal(1, civil.I("packageSampleCount")); Assert.Equal(0, civil.I("unavailablePackageCount")); Assert.Equal(1, civil.I("startedAssignmentRoundCount"));
        Assert.Equal(0, civil.I("timedAssignmentRoundCount")); Assert.Equal(1, civil.I("unavailableTimingAssignmentRoundCount")); Assert.Null(civil["medianDaysPerRound"]);
        await t.F.DbAsync(async db => { (await db.ReviewRounds.SingleAsync(row => row.Id == a.Round)).StartedAt = null; return await db.SaveChangesAsync(); });
        civil = Row(await Summary(t, project.Id), a.Civil); Assert.Equal(0, civil.I("packageSampleCount")); Assert.Equal(1, civil.I("unavailablePackageCount")); Assert.Null(civil["medianRoundsToApproval"]);
        await t.F.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(row => row.Id == a.Civil)).IsActive = false; return await db.SaveChangesAsync(); });
        civil = Row(await Summary(t, project.Id), a.Civil); Assert.Equal("Inactive", civil.S("state")); Assert.Equal("Civil", civil.S("name"));
    }

    [Fact] // AC-SCR-63: provider population over 50, distinct packages/rounds, no browser pagination or source/finding multiplier.
    public async Task Complete_population_exceeds_fifty_and_replaced_approved_package_leaves_it()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); await Approve(t, s, 1);
        // Explicit synthetic historical cohort in this private database; normal approval lifecycle is covered above.
        await t.F.DbAsync(async db => {
            var packages = Enumerable.Range(1, 55).Select(index => new ReviewPackage { ProjectId = s.Project.Id, Key = $"SYN-{index}", Seq = 100 + index,
                Title = "Synthetic historical approved package", Purpose = "Synthetic population", CoordinatorId = t.Data.User(TestData.Marc), ProjectDisciplineId = s.Civil }).ToArray();
            db.ReviewPackages.AddRange(packages); await db.SaveChangesAsync();
            foreach (var package in packages) {
                var round = new ReviewRound { ProjectId = s.Project.Id, PackageId = package.Id, Number = 1, Purpose = "Synthetic population", StartedAt = t.F.Clock.Now, Status = ReviewStatus.Approved };
                db.ReviewRounds.Add(round); db.DisciplineReviews.Add(new DisciplineReview { ProjectId = s.Project.Id, RoundId = round.Id, ProjectDisciplineId = s.Civil,
                    ReviewerId = t.Data.User(TestData.Pm), DueDate = new(2026, 11, 12), Status = DisciplineReviewStatus.Approved, DecidedAt = t.F.Clock.Now });
                await db.SaveChangesAsync(); package.CurrentRoundId = round.Id; package.RoundNumber = 1; package.Status = ReviewStatus.Approved;
            }
            return await db.SaveChangesAsync();
        });
        var before = t.State(s.Project.Id); var civil = Row(await Summary(t, s.Project.Id), s.Civil); Assert.Equal(56, civil.I("packageSampleCount"));
        Assert.Equal(56, civil.I("startedAssignmentRoundCount")); Assert.Equal(before, t.State(s.Project.Id));
        await t.PublishReplacement(s); civil = Row(await Summary(t, s.Project.Id), s.Civil); Assert.Equal(55, civil.I("approvedPackageCount"));
    }

    [Fact] // AC-SCR-63 / AC-SCR-84: response metadata never grants approval; read authority, zero/null, flags and query scope remain exact.
    public async Task Response_is_not_independent_verification_and_read_is_scoped_dark_and_side_effect_free()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await Decide(t, s, "Civil", "Approved"); await Decide(t, s, "Electrical", "Approved"); await t.Action(s, finding, "Responded", TestData.Alex, "Incorporated");
        var result = await Summary(t, s.Project.Id); Assert.Equal(0, Row(result, s.Civil).I("approvedPackageCount")); Assert.Null(Row(result, s.Civil)["medianRoundsToApproval"]);
        await t.Action(s, finding, "Verified Closed", TestData.Omar); Assert.Equal(1, Row(await Summary(t, s.Project.Id), s.Civil).I("eligiblePackageCount"));
        var before = t.State(s.Project.Id); await (await t.F.As(TestData.Pm).GetAsync(Path(s.Project.Id) + "?page=2")).Json(400); Assert.Equal(before, t.State(s.Project.Id));
        await t.Flag(false); Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Pm).GetAsync(Path(s.Project.Id))).StatusCode); await t.Flag(true);
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(Path(s.Project.Id))).StatusCode);
        var other = await t.Data.Project();
        await t.F.DbAsync(async db => { (await db.DisciplineReviews.FirstAsync(row => row.RoundId == s.Round)).ProjectId = other.Id; return await db.SaveChangesAsync(); });
        before = t.State(s.Project.Id); await (await t.F.As(TestData.Pm).GetAsync(Path(s.Project.Id))).Json(409); Assert.Equal(before, t.State(s.Project.Id));
    }

    internal static string Path(Guid project) => $"/api/v1/projects/{project}/reviews/discipline-summary";
    internal static async Task<JsonNode> Summary(ReviewResponseFixture t, Guid project, string who = TestData.Pm) => await (await t.F.As(who).GetAsync(Path(project))).Json();
    static JsonNode Row(JsonNode result, Guid discipline) => result["disciplines"]!.AsArray().Single(row => row!.G("projectDisciplineId") == discipline)!;
    internal static async Task Start(ReviewResponseFixture t, ReviewResponseFixture.Setup s) => await t.Post(TestData.Pm, t.Root(s) + $"/reviews/{s.Package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), t.Version<ReviewPackage>(s.Package), "start", null));
    internal static async Task Decide(ReviewResponseFixture t, ReviewResponseFixture.Setup s, string discipline, string status)
    {
        var assignment = t.F.Db(db => db.DisciplineReviews.Single(a => a.RoundId == db.ReviewPackages.Single(p => p.Id == s.Package).CurrentRoundId && a.ProjectDisciplineId == (discipline == "Civil" ? s.Civil : s.Electrical)));
        await t.Post(discipline == "Civil" ? TestData.Pm : TestData.Omar, t.Root(s) + $"/reviews/{s.Package}/assignments/{assignment.Id}/decision", new ReviewEndpoints.DecisionBody(Guid.NewGuid(), assignment.RowVersion, status, "Recorded synthetic discipline result"));
    }
    static async Task Approve(ReviewResponseFixture t, ReviewResponseFixture.Setup s, int rounds)
    {
        for (var index = 1; index <= rounds; index++) {
            t.F.Clock.Now = t.F.Clock.Now.AddDays(1);
            await Decide(t, s, "Civil", index == rounds ? "Approved" : "Changes Required"); await Decide(t, s, "Electrical", "Approved");
            if (index != rounds) { await t.NewRound(s); await Start(t, s); }
        }
    }
}
