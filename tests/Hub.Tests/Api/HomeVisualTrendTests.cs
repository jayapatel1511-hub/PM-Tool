using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// S08: values are read through Home; historic fixtures use the root's real capture adapter.
public sealed class HomeVisualTrendTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeVisualTrendTests() => d = new(f);
    public void Dispose() => f.Dispose();
    Task Set(string key, object value) => f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value }).Result.Json();
    async Task<JsonNode> Home(string projects, string who = TestData.Pm, string extra = "") =>
        await (await f.As(who).GetAsync($"/api/v1/home?projects={projects}{extra}")).Json();
    static JsonNode Metric(JsonNode home, string id) => home["trends"]!["metrics"]!.AsArray().Single(m => m!.S("id") == id)!;
    async Task<HomeMetricCapture> Capture(Guid pid)
    {
        return await f.DbAsync(async db =>
        {
            var settings = await db.CurrentSettings();
            var day = f.Clock.Today(settings);
            var capture = await HomeSnapshots.Capture(db, settings, pid, day, f.Clock.Now);
            db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = pid, SnapshotDate = day,
                ComputedHealth = capture.ComputedHealth, HomeMetrics = JsonSerializer.Serialize(capture, JsonOpts.Web) });
            await db.SaveChangesAsync();
            return capture;
        });
    }
    Task SetHealth(Guid pid, string health) => f.DbAsync(async db =>
    {
        var state = await db.ProjectStates.SingleOrDefaultAsync(p => p.ProjectId == pid);
        if (state == null) db.ProjectStates.Add(new ProjectState { ProjectId = pid, ComputedHealth = health, EvaluatedAt = f.Clock.Now });
        else state.ComputedHealth = health;
        return await db.SaveChangesAsync();
    });

    [Fact]
    public async Task Three_overdue_to_one_compares_only_common_visible_projects_and_discloses_actual_capture_time()
    {
        f.Clock.SetDate(2026, 10, 24);
        await Set("flag.home_overview", true);
        await Set("restricted_projects_enabled", true);
        var project = await d.Project(); var hidden = await d.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var tasks = new List<Guid>();
        for (var i = 0; i < 3; i++) tasks.Add((await d.NewTask(project.Id, extra: new { dueDate = "2026-10-23" })).G("id"));
        await d.NewTask(hidden.Id, extra: new { dueDate = "2026-10-23", name = "Hidden historical task" });
        await f.Evaluate(project.Id); await f.Evaluate(hidden.Id);
        await SetHealth(project.Id, Health.Red);
        var capture = await Capture(project.Id); await Capture(hidden.Id);
        f.Clock.SetDate(2026, 10, 31);
        await f.DbAsync(async db =>
        {
            foreach (var task in await db.Tasks.Where(t => tasks.Take(2).Contains(t.Id)).ToListAsync()) task.Status = TaskStatuses.Complete;
            return await db.SaveChangesAsync();
        });
        var missing = await d.Project(); await d.NewTask(missing.Id, extra: new { dueDate = "2026-10-30" });
        await f.Evaluate(project.Id); await f.Evaluate(missing.Id);
        await SetHealth(project.Id, Health.Green);
        var body = await Home($"{project.Id},{missing.Id},{hidden.Id}", TestData.Rita);
        Assert.Equal(2, body["metrics"]!.I("overdueTasks")); // full current scope still includes the project without history
        var overdue = Metric(body, "OverdueTasks");
        Assert.Equal(1, overdue.I("current")); Assert.Equal(3, overdue.I("previous")); Assert.Equal(-2, overdue.I("delta"));
        Assert.Equal("Good", overdue.S("tone")); Assert.Equal("Comparable", overdue.S("state"));
        Assert.Equal(100, Metric(body, "OnTrack").I("delta")); Assert.Equal("Good", Metric(body, "OnTrack").S("tone"));
        Assert.Equal("Neutral", Metric(body, "OpenTasks").S("tone"));
        var trends = body["trends"]!;
        Assert.Equal("2026-10-24", trends.S("targetDate")); Assert.Equal(1, trends.I("excludedProjects"));
        Assert.Equal(capture.CapturedAt, trends["compareFrom"]!.GetValue<DateTimeOffset>());
        Assert.Equal(capture.CapturedAt, trends["compareThrough"]!.GetValue<DateTimeOffset>());
        Assert.Equal(project.ProjectNumber, Assert.Single(trends["projects"]!.AsArray())!.S("projectNumber"));
        Assert.Equal(5, trends["metrics"]!.AsArray().Count); Assert.DoesNotContain("TeamMembers", trends.ToJsonString());
        Assert.DoesNotContain(hidden.Id.ToString(), body.ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, body.ToJsonString());
        Assert.DoesNotContain("Hidden historical task", body.ToJsonString()); Assert.DoesNotContain(capture.AccessBasis, body.ToJsonString());
    }

    [Fact]
    public async Task Latest_legacy_or_incompatible_capture_is_unknown_and_never_skips_back_to_a_convenient_older_row()
    {
        await Set("flag.home_overview", true); f.Clock.SetDate(2026, 10, 23);
        var project = await d.Project(); await d.NewTask(project.Id, extra: new { dueDate = "2026-10-22" });
        await Capture(project.Id); f.Clock.SetDate(2026, 10, 24); var valid = await Capture(project.Id);
        f.Clock.SetDate(2026, 10, 31);
        var variants = new string?[] { null, "{}", "{\"tasksOverdue\":3}",
            JsonSerializer.Serialize(valid with { Version = 2 }, JsonOpts.Web),
            JsonSerializer.Serialize(valid with { DueWindowDays = 3 }, JsonOpts.Web),
            JsonSerializer.Serialize(valid with { OrgTimeZone = "UTC" }, JsonOpts.Web),
            JsonSerializer.Serialize(valid with { CapturedAt = valid.CapturedAt.AddDays(-1) }, JsonOpts.Web),
            JsonSerializer.Serialize(valid with { CapturedAt = f.Clock.Now.AddDays(1) }, JsonOpts.Web),
            JsonSerializer.Serialize(valid with { ProjectStatus = ProjectStatus.OnHold }, JsonOpts.Web) };
        foreach (var payload in variants)
        {
            await f.DbAsync(async db => { (await db.HealthSnapshots.SingleAsync(p => p.ProjectId == project.Id && p.SnapshotDate == new DateOnly(2026, 10, 24))).HomeMetrics = payload; return await db.SaveChangesAsync(); });
            var home = await Home(project.Id.ToString(), extra: "&projectStatus=Active");
            Assert.Null(home["trends"]!["compareFrom"]); Assert.Empty(home["trends"]!["projects"]!.AsArray());
            Assert.Equal(1, home["trends"]!.I("excludedProjects"));
            Assert.All(home["trends"]!["metrics"]!.AsArray(), m => { Assert.Equal("Unavailable", m!.S("state")); Assert.Null(m["delta"]); });
        }
        await f.DbAsync(async db => { (await db.HealthSnapshots.SingleAsync(p => p.ProjectId == project.Id && p.SnapshotDate == new DateOnly(2026, 10, 24))).HomeMetrics = JsonSerializer.Serialize(valid, JsonOpts.Web); return await db.SaveChangesAsync(); });
        Assert.Equal("Comparable", Metric(await Home(project.Id.ToString()), "OverdueTasks").S("state"));
        // A membership changes away and back: matching present values are insufficient historical permission evidence.
        var alex = d.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == alex)).Roles = [ProjectRole.TeamMember, ProjectRole.Viewer]; return await db.SaveChangesAsync(); });
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == alex)).Roles = [ProjectRole.TeamMember]; return await db.SaveChangesAsync(); });
        Assert.Equal("Unavailable", Metric(await Home(project.Id.ToString()), "OverdueTasks").S("state"));
    }

    [Fact]
    public async Task Read_is_side_effect_free_empty_scope_and_unknown_denominator_are_not_fabricated_zero_trends_and_off_is_omitted()
    {
        await Set("flag.home_overview", true); f.Clock.SetDate(2026, 10, 24);
        var project = await d.Project(); await SetHealth(project.Id, Health.Grey); await Capture(project.Id);
        f.Clock.SetDate(2026, 10, 31);
        var before = await f.DbAsync(db => db.ActivityLog.CountAsync());
        var body = await Home(project.Id.ToString());
        Assert.Equal(before, await f.DbAsync(db => db.ActivityLog.CountAsync()));
        Assert.Equal("NotEvaluated", Metric(body, "OnTrack").S("reason")); Assert.Null(Metric(body, "OnTrack")["delta"]);
        Assert.Equal(0, Metric(body, "OverdueTasks").I("delta")); Assert.Equal("Neutral", Metric(body, "OverdueTasks").S("tone"));
        var empty = await Home("none"); Assert.Equal("Unavailable", Metric(empty, "ActiveProjects").S("state"));
        await Set("flag.home_overview", false);
        var off = await Home(project.Id.ToString(), extra: "&homePreset=Invented&projectStatus=Invented");
        Assert.Null(off["trends"]); Assert.Null(off["preset"]); Assert.Null(off["countContext"]);
        Assert.DoesNotContain("trends", off.AsObject().Select(k => k.Key));
    }

    [Theory]
    [InlineData("ActiveProjects", 1, 3, "Neutral")]
    [InlineData("OpenTasks", 1, 3, "Neutral")]
    [InlineData("DueWithin", 3, 1, "Neutral")]
    [InlineData("OverdueTasks", 1, 3, "Good")]
    [InlineData("OverdueTasks", 3, 1, "Bad")]
    [InlineData("OnTrack", 80, 60, "Good")]
    [InlineData("OnTrack", 60, 80, "Bad")]
    [InlineData("OnTrack", 60, 60, "Neutral")]
    public void Metric_semantics_determine_tone_without_changing_the_sign(string id, int current, int previous, string tone)
    {
        var metric = HomeVisualTrends.Compare(id, current, previous);
        Assert.Equal(current - previous, metric.Delta); Assert.Equal(tone, metric.Tone);
    }
}
