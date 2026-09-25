using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 016: the Portfolio Dashboard (FR-PORT-01, §13.12), computed and reported health side by side (§16.4), the
/// 8-week trend from snapshots (FR-HLT-03, §16.5), who sees what (§8.5.1) and the portfolio reports (§19).
[Collection("api")]
public sealed class PortfolioTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<(Project Red, Project Yellow, Project Green, Project Held)> Four()
    {
        var pm = f.As(TestData.Pm);
        var red = await d.Project();
        await pm.Post($"/api/v1/projects/{red.Id}/milestones", new { name = "Survey complete", milestoneType = "Field Work", date = "2026-09-10" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{red.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-09-24" }).Result.Json(201);
        var yellow = await d.Project();
        await d.NewTask(yellow.Id, TestData.Pm, new { dueDate = "2026-10-15" });
        var party = await pm.Post($"/api/v1/projects/{yellow.Id}/external-parties", new { name = "City", isClient = true }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{yellow.Id}/decisions", new { subject = "Pick a pavement", description = "Options", ownerExternalPartyId = party.G("id"),
            requiredByDate = "2026-09-10", impactLevel = "Medium", impactDescription = "Drawings wait" }).Result.Json(201);
        var green = await d.Project();
        await d.NewTask(green.Id, TestData.Pm, new { dueDate = "2026-10-15" });
        var held = await d.Project();
        await pm.Post($"/api/v1/projects/{held.Id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = d.Version(held.Id) }).Result.Json();
        foreach (var p in new[] { red, yellow, green, held }) await f.Evaluate(p.Id);
        return (red, yellow, green, held);
    }

    static string Ids(params Project[] ps) => string.Join(",", ps.Select(p => p.Id));

    [Fact]
    public async Task Tiles_rows_sorting_and_both_health_values() // FR-PORT-01, §13.12, §16.4, AC edge: On Hold listed but not counted
    {
        var (red, yellow, green, held) = await Four();
        (await f.As(TestData.Pm).Post($"/api/v1/projects/{yellow.Id}/health-override", new { health = "Green", note = "Client agreed a two-week extension to 60%", rowVersion = d.Version(yellow.Id) })).EnsureSuccessStatusCode();

        var r = await f.As(TestData.Lena).GetAsync($"/api/v1/portfolio?ids={Ids(red, yellow, green, held)}").Result.Json();
        var tiles = r["tiles"]!;
        Assert.Equal((3, 1, 1, 1, 1, 1), (tiles.I("active"), tiles.I("red"), tiles.I("yellow"), tiles.I("green"), tiles.I("submissions14"), tiles.I("overdueDecisions")));
        var rows = r["projects"]!.AsArray();
        Assert.Equal(4, rows.Count);
        Assert.Equal(red.ProjectNumber, rows[0]!.S("projectNumber")); // worst first
        Assert.Equal(held.ProjectNumber, rows[^1]!.S("projectNumber"));
        Assert.Equal("On Hold", rows[^1]!.S("status"));
        Assert.Contains("Milestones overdue", rows[0]!.S("why"));
        Assert.Equal("60% Submission", rows[0]!["nextSubmission"]!.S("name"));

        var y = rows.Single(x => x!.S("projectNumber") == yellow.ProjectNumber)!;
        Assert.Equal(("Yellow", "Green"), (y.S("computedHealth"), y.S("reportedHealth"))); // optimism is visible, not hidden
        Assert.Equal("Client agreed a two-week extension to 60%", y.S("healthOverrideNote"));
        Assert.Equal("Priya Nair", y.S("overrideBy"));
        Assert.NotNull(y["healthOverrideAt"]);

        // Filters narrow the same list; a Standard User has no portfolio.
        var office = await f.As(TestData.Lena).GetAsync($"/api/v1/portfolio?ids={Ids(red, yellow, green, held)}&health=Red").Result.Json();
        Assert.Equal(new[] { red.ProjectNumber }, office["projects"]!.AsArray().Select(x => x!.S("projectNumber")));
        var soon = await f.As(TestData.Lena).GetAsync($"/api/v1/portfolio?ids={Ids(red, yellow, green, held)}&submissionWithinDays=14").Result.Json();
        Assert.Equal(new[] { red.ProjectNumber }, soon["projects"]!.AsArray().Select(x => x!.S("projectNumber")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/portfolio")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Lena).GetAsync($"/api/v1/portfolio/export?format=xlsx&ids={Ids(red, yellow)}")).StatusCode);
    }

    [Fact]
    public async Task Trend_shows_each_weeks_computed_health_in_order() // FR-HLT-03, §16.5
    {
        var p = await d.Project();
        var colours = new[] { "Green", "Green", "Yellow", "Yellow", "Red", "Red", "Yellow", "Green" };
        var today = new DateOnly(2026, 9, 14);
        await f.DbAsync(async db =>
        {
            for (var i = 0; i < 8; i++)
            {
                db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = today.AddDays(-7 * (7 - i)), ComputedHealth = colours[i], ReportedHealth = colours[i] });
                db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = today.AddDays(-7 * (7 - i) - 3), ComputedHealth = "Grey", ReportedHealth = "Grey" }); // earlier in the same week
            }
            return await db.SaveChangesAsync();
        });
        var row = (await f.As(TestData.Lena).GetAsync($"/api/v1/portfolio?ids={p.Id}").Result.Json())["projects"]![0]!;
        Assert.Equal(colours, row["trend"]!.AsArray().Select(x => x!.S("computed")));
        Assert.Equal("2026-09-14", row["trend"]![7]!.S("weekEnding"));

        var history = await f.As(TestData.Lena).GetAsync($"/api/v1/reports/health-history?projectId={p.Id}").Result.Json();
        Assert.Equal(16, history["rows"]!.AsArray().Count); // every snapshot in the default last 56 days
    }

    [Fact]
    public async Task Who_sees_which_projects_and_the_at_risk_report() // FR-003, §8.5.1, §19
    {
        var (red, yellow, green, _) = await Four();
        var marcs = await d.Project(pm: TestData.Marc);
        var ids = Ids(red, green, marcs);

        var own = await f.As(TestData.Pm).GetAsync($"/api/v1/portfolio?ids={ids}").Result.Json();
        Assert.True(own["ownOnly"]!.GetValue<bool>());
        Assert.DoesNotContain(own["projects"]!.AsArray(), x => x!.S("projectNumber") == marcs.ProjectNumber);
        var all = await f.As(TestData.Pm).GetAsync($"/api/v1/portfolio?ids={ids}&mine=false").Result.Json();
        Assert.Contains(all["projects"]!.AsArray(), x => x!.S("projectNumber") == marcs.ProjectNumber);
        var supervisor = await f.As(TestData.Marc).GetAsync($"/api/v1/portfolio?ids={ids}").Result.Json(); // PM and Supervisor: everything by default
        Assert.False(supervisor["ownOnly"]!.GetValue<bool>());
        Assert.Equal(3, supervisor["projects"]!.AsArray().Count);

        var catalogue = (await f.As(TestData.Alex).GetAsync("/api/v1/reports").Result.Json()).AsArray();
        Assert.DoesNotContain(catalogue, x => x!.S("code") == "projects-at-risk");
        var atRisk = (await f.As(TestData.Lena).GetAsync("/api/v1/reports/projects-at-risk").Result.Json())["rows"]!.AsArray();
        Assert.Contains(atRisk, x => x!.S("projectNumber") == red.ProjectNumber);
        Assert.Contains(atRisk, x => x!.S("projectNumber") == yellow.ProjectNumber);
        Assert.DoesNotContain(atRisk, x => x!.S("projectNumber") == green.ProjectNumber);
        Assert.Contains("Decisions overdue", atRisk.Single(x => x!.S("projectNumber") == yellow.ProjectNumber)!.S("why"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/reports/projects-at-risk")).StatusCode);
    }
}
