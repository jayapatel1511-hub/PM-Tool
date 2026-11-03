using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Hub.Tests.Api;

/// Root-owned S08 capture/schema boundary. Trend/preset presentation is tested by
/// the Home lane; these cases verify real nightly capture and access invalidation.
public sealed class HomeSnapshotCaptureTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeSnapshotCaptureTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Set(string key, object value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
    Task<string> Basis(Guid id) => f.DbAsync(async db => (await HomeSnapshots.AccessBases(db, new OrgSettings(), [id]))[id]);

    [Fact]
    public async Task Nightly_adds_exact_flagged_capture_without_backfilling_or_changing_legacy_counts_policy()
    {
        var project = await d.Project();
        var setup = await d.Project(activate: false);
        var today = f.Clock.Today(new OrgSettings());
        await Set("home_due_window_days", 3);
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), dueDate = today.AddDays(-2).ToString("yyyy-MM-dd") });
        await d.NewTask(project.Id, TestData.Pm, new { dueDate = today.AddDays(3).ToString("yyyy-MM-dd") });
        await d.NewTask(project.Id, TestData.Pm, new { dueDate = today.AddDays(4).ToString("yyyy-MM-dd") });
        await d.NewTask(project.Id, TestData.Pm);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones", new
        {
            name = "Synthetic Home snapshot milestone", milestoneType = MilestoneType.ClientWorkshop, date = today.AddDays(3)
        })).Json(201);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Synthetic Home snapshot deliverable", projectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), dueDate = today.AddDays(3), requiresReview = false
        })).Json(201);
        await f.RunJob<NightlyJob>();
        var off = await f.DbAsync(db => db.HealthSnapshots.AsNoTracking().SingleAsync(x => x.ProjectId == project.Id && x.SnapshotDate == today));
        Assert.Null(off.HomeMetrics);
        var legacyCounts = off.Counts;
        Assert.False(await f.DbAsync(db => db.HealthSnapshots.AnyAsync(x => x.ProjectId == setup.Id)));

        await Set("flag.home_overview", true);
        await f.RunJob<NightlyJob>();
        var snapshot = await f.DbAsync(db => db.HealthSnapshots.AsNoTracking().SingleAsync(x => x.ProjectId == project.Id && x.SnapshotDate == today));
        Assert.Equal(legacyCounts, snapshot.Counts);
        var capture = Assert.IsType<HomeMetricCapture>(HomeSnapshots.Parse(snapshot.HomeMetrics));
        var home = await (await f.As(TestData.Pm).GetAsync($"/api/v1/home?projects={project.Id}")).Json();
        Assert.Equal(home["metrics"]!.I("openTasks"), capture.OpenTasks);
        Assert.Equal(home["metrics"]!.I("overdueTasks"), capture.OverdueTasks);
        Assert.Equal(home["countContext"]!.I("dueWithinTasks"), capture.DueTasks);
        Assert.Equal(home["countContext"]!.I("dueWithinDeliverables"), capture.DueDeliverables);
        Assert.Equal(home["countContext"]!.I("dueWithinMilestones"), capture.DueMilestones);
        Assert.Equal(4, capture.OpenTasks); Assert.Equal(1, capture.OverdueTasks);
        Assert.Equal(1, capture.DueTasks); Assert.Equal(1, capture.DueDeliverables); Assert.Equal(1, capture.DueMilestones);
        Assert.Equal(f.Clock.Now, capture.CapturedAt);
        Assert.Equal(3, capture.DueWindowDays);
        Assert.Equal(ProjectStatus.Active, capture.ProjectStatus);
        Assert.Equal(await Basis(project.Id), capture.AccessBasis);
        Assert.DoesNotContain(capture.AccessBasis, home.ToJsonString());
        Assert.False(await f.DbAsync(db => db.HealthSnapshots.AnyAsync(x => x.ProjectId == setup.Id)));

        await Set("flag.home_overview", false);
        await d.NewTask(project.Id, TestData.Pm);
        await f.RunJob<NightlyJob>();
        var rollback = await f.DbAsync(db => db.HealthSnapshots.AsNoTracking().SingleAsync(x => x.ProjectId == project.Id && x.SnapshotDate == today));
        Assert.Equal(snapshot.HomeMetrics, rollback.HomeMetrics); // rollback preserves stored evidence, never refreshes it while off
        Assert.NotEqual(snapshot.Counts, rollback.Counts); // existing evaluator/report counts keep updating
    }

    [Fact]
    public async Task Basis_ignores_login_profile_and_task_noise_but_invalidates_restored_acl_values()
    {
        var project = await d.Project();
        var alex = d.User(TestData.Alex);
        var original = await Basis(project.Id);
        await f.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == TestData.Alex);
            user.LastSignInAt = f.Clock.Now.AddMinutes(1);
            user.DisplayName = "Synthetic renamed person";
            return await db.SaveChangesAsync();
        });
        await d.NewTask(project.Id, TestData.Pm);
        Assert.Equal(original, await Basis(project.Id));

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.NotEqual(original, await Basis(project.Id));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Open; return await db.SaveChangesAsync(); });
        var restored = await Basis(project.Id);
        Assert.NotEqual(original, restored); // same frozen clock and restored current values still carry new ACL evidence

        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Email == TestData.Alex)).IsActive = false; return await db.SaveChangesAsync(); });
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Email == TestData.Alex)).IsActive = true; return await db.SaveChangesAsync(); });
        var activeAgain = await Basis(project.Id);
        Assert.NotEqual(restored, activeAgain);
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == alex)).Roles = [ProjectRole.TeamMember, ProjectRole.Viewer]; return await db.SaveChangesAsync(); });
        Assert.NotEqual(activeAgain, await Basis(project.Id));
    }

    [Fact]
    public async Task Global_role_changes_invalidate_the_capture_without_exposing_raw_basis()
    {
        var project = await d.Project();
        var before = await Basis(project.Id);
        var user = d.User(TestData.Alex);
        await (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{user}/roles", new { role = SystemRole.Executive })).Json(204);
        var granted = await Basis(project.Id);
        Assert.NotEqual(before, granted);
        (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/users/{user}/roles/{SystemRole.Executive}")).EnsureSuccessStatusCode();
        Assert.NotEqual(before, await Basis(project.Id));
        Assert.NotEqual(granted, await Basis(project.Id));
    }

    [Fact]
    public void Legacy_incomplete_malformed_and_future_version_captures_remain_unknown()
    {
        var valid = new HomeMetricCapture(1, DateTimeOffset.Parse("2026-10-24T15:00:00Z"), "America/Halifax", 7,
            ProjectStatus.Active, Health.Green, 4, 3, 1, 0, 0, new string('A', 64));
        var json = JsonSerializer.Serialize(valid, JsonOpts.Web);
        Assert.Equal(valid, HomeSnapshots.Parse(json));
        foreach (var value in new[] { (string?)null, "", "{}", "null", "[]", "{", "{\"tasksOverdue\":3}" }) Assert.Null(HomeSnapshots.Parse(value));
        foreach (var field in new[] { "version", "capturedAt", "orgTimeZone", "dueWindowDays", "projectStatus", "computedHealth", "openTasks", "overdueTasks", "dueTasks", "dueDeliverables", "dueMilestones", "accessBasis" })
        {
            var missing = JsonNode.Parse(json)!.AsObject(); missing.Remove(field);
            Assert.Null(HomeSnapshots.Parse(missing.ToJsonString()));
        }
        foreach (var invalid in new[] { valid with { Version = 2 }, valid with { DueWindowDays = 367 }, valid with { OpenTasks = -1 }, valid with { ComputedHealth = "invented" }, valid with { ProjectStatus = "invented" } })
            Assert.Null(HomeSnapshots.Parse(JsonSerializer.Serialize(invalid, JsonOpts.Web)));
    }

    [Fact]
    public async Task Trend_setting_default_range_and_dark_admin_visibility_are_explicit()
    {
        var def = Assert.IsType<SettingDef>(OrgSettings.Def("trend_compare_days"));
        Assert.Equal(7, new OrgSettings().TrendCompareDays);
        foreach (var n in new[] { 1, 7, 366 }) Assert.Null(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(n)));
        foreach (var n in new[] { 0, -1, 367 }) Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(n)));
        Assert.Equal(9, OrgSettings.From(new Dictionary<string, JsonElement> { ["trend_compare_days"] = JsonSerializer.SerializeToElement(9) }).TrendCompareDays);
        var off = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json();
        Assert.DoesNotContain(off.AsArray(), x => x!.S("key") == def.Key);
        await Set("flag.home_overview", true);
        var on = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json();
        Assert.Contains(on.AsArray(), x => x!.S("key") == def.Key);
        await Set("flag.home_overview", false);
        await Set("flag.project_screens", true);
        var portfolio = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json();
        Assert.Contains(portfolio.AsArray(), x => x!.S("key") == def.Key);
        await Set("flag.project_screens", false);
        var dark = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json();
        Assert.DoesNotContain(dark.AsArray(), x => x!.S("key") == def.Key);
    }

    [Fact]
    public async Task Nightly_crossing_local_midnight_does_not_mix_capture_day_with_the_legacy_snapshot_day()
    {
        var beforeMidnight = DateTimeOffset.Parse("2026-09-15T02:59:59Z"); // Sep14 23:59:59 in Halifax
        var afterMidnight = beforeMidnight.AddSeconds(2);
        f.Clock.Now = beforeMidnight;
        var preservedProject = await d.Project();
        await Set("flag.home_overview", true);
        await f.RunJob<NightlyJob>();
        var preservedCapture = await f.DbAsync(db => db.HealthSnapshots.Where(x => x.ProjectId == preservedProject.Id).Select(x => x.HomeMetrics).SingleAsync());
        Assert.NotNull(preservedCapture);
        var project = await d.Project();
        await d.NewTask(project.Id, TestData.Pm, new { dueDate = "2026-09-14" });
        f.Clock.Now = afterMidnight; // project evaluation sees the new day
        using (var scope = f.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
            // The job reads its initial instant/date before midnight, then its
            // capture instant after midnight; inner evaluations use f.Clock.
            await new NightlyJob().Run(new ClockProvider(scope.ServiceProvider,
                new RolloverClock(beforeMidnight, afterMidnight)), CancellationToken.None);
        }
        var oldDate = new DateOnly(2026, 9, 14); var newDate = oldDate.AddDays(1);
        var legacy = await f.DbAsync(db => db.HealthSnapshots.AsNoTracking().SingleAsync(x => x.ProjectId == project.Id && x.SnapshotDate == oldDate));
        Assert.Equal(1, JsonNode.Parse(legacy.Counts)!.I("tasksOverdue")); // existing policy is preserved
        Assert.Null(legacy.HomeMetrics); // no mixed-day Home measurement is persisted
        Assert.Equal(preservedCapture, await f.DbAsync(db => db.HealthSnapshots.Where(x => x.ProjectId == preservedProject.Id && x.SnapshotDate == oldDate).Select(x => x.HomeMetrics).SingleAsync()));
        Assert.False(await f.DbAsync(db => db.HealthSnapshots.AnyAsync(x => x.ProjectId == project.Id && x.SnapshotDate == newDate)));

        await f.RunJob<NightlyJob>();
        var next = await f.DbAsync(db => db.HealthSnapshots.AsNoTracking().SingleAsync(x => x.ProjectId == project.Id && x.SnapshotDate == newDate));
        var capture = Assert.IsType<HomeMetricCapture>(HomeSnapshots.Parse(next.HomeMetrics));
        Assert.Equal(afterMidnight, capture.CapturedAt);
        Assert.Equal(1, capture.OverdueTasks);
        await f.DbAsync(async db =>
        {
            await Assert.ThrowsAsync<ArgumentException>(() => HomeSnapshots.Capture(db, new OrgSettings(), project.Id, oldDate, afterMidnight));
            return 0;
        });
    }

    sealed class RolloverClock(DateTimeOffset before, DateTimeOffset after) : TimeProvider
    {
        int reads;
        public override DateTimeOffset GetUtcNow() => ++reads <= 2 ? before : after;
    }
    sealed class ClockProvider(IServiceProvider services, TimeProvider clock) : IServiceProvider
    {
        public object? GetService(Type type) => type == typeof(TimeProvider) ? clock : services.GetService(type);
    }
}
