using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S22 composes existing canonical readers; its disposable fixtures never use operational data.
public sealed class ProjectDashboardCompletenessTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Mon = new(2026, 11, 16);
    public ProjectDashboardCompletenessTests() { d = new(f); f.Clock.SetDate(2026, 11, 16); }
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    string Path(Guid project) => $"/api/v1/projects/{project}/dashboard";
    async Task<JsonNode> Read(Guid project, string who = TestData.Pm, string query = "") => await (await f.As(who).GetAsync(Path(project) + query)).Json();
    static decimal N(JsonNode row, string key) => row[key]!.GetValue<decimal>();
    static JsonNode Civil(JsonNode read) => read["staffing"]!["disciplines"]!.AsArray().Single(r => r!["name"]?.GetValue<string>() == "Civil")!;

    async Task<Guid> Milestone(Guid project, DateOnly date, string name = "Synthetic submission milestone") =>
        (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/milestones",
            new MilestoneEndpoints.CreateBody(name, MilestoneType.DesignSubmission, date, null, null, null, true))).Json(201)).G("id");

    // This fixture isolates target selection, not the separately tested manifest/check command workflow.
    async Task<Guid> Package(Guid project, Guid milestone, DateOnly date, string key = "PKG-002", string status = SubmissionStatus.Draft) => await f.DbAsync(async db => {
        var package = new SubmissionPackage { ProjectId = project, MilestoneId = milestone, TargetDate = date, Key = key,
            Seq = (await db.SubmissionPackages.Where(p => p.ProjectId == project).Select(p => (int?)p.Seq).MaxAsync() ?? 0) + 1,
            Title = "Synthetic " + key, CoordinatorId = d.User(TestData.Marc), Purpose = "Review", RecipientReference = "Synthetic recipient", Status = status };
        db.SubmissionPackages.Add(package); await db.SaveChangesAsync(); return package.Id;
    });

    async Task<Guid> Role(Project p, decimal hours, DateOnly? from = null, DateOnly? through = null) =>
        (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = "Synthetic Civil demand",
            projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), fromDate = from ?? Mon, throughDate = through ?? Mon.AddDays(11), plannedHours = hours })).Json(201)).G("id");

    async Task<Guid> Reserve(Project p, JsonNode task, decimal hours, DateOnly? from = null, DateOnly? through = null,
        AllocationEndpoints.DayInput[]? days = null, bool confirm = true)
    {
        var person = d.User(TestData.Alex); var start = from ?? Mon; var end = through ?? Mon.AddDays(11);
        var root = $"/api/v1/projects/{p.Id}/allocations";
        var created = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), person,
            AllocationPurpose.Production, start, end, hours, days ?? [], [new("Task", task.G("id"), start)], null))).Json();
        if (confirm)
        {
            var preview = await (await f.As(TestData.Sam).GetAsync($"{root}/{created.G("id")}/confirmation-preview")).Json();
            var versions = preview["days"]!.AsArray().Select(day => new AllocationEndpoints.DateVersionInput(DateOnly.Parse(day!.S("date")), day.I("dateVersion"))).ToArray();
            await (await f.As(TestData.Sam).Post($"{root}/{created.G("id")}/confirm", new AllocationEndpoints.ConfirmBody(
                Guid.NewGuid(), created.I("rowVersion"), versions, "Synthetic approved capacity overlap"))).Json();
        }
        return created.G("id");
    }

    async Task<(Project Project, JsonNode Task, Guid Allocation, Guid Role)> NumericFixture()
    {
        var p = await d.Project(); var milestone = await Milestone(p.Id, Mon.AddDays(11)); await Package(p.Id, milestone, Mon.AddDays(11));
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 120m, startDate = Mon, dueDate = Mon.AddDays(11) });
        var allocation = await Reserve(p, task, 80m); var role = await Role(p, 24m);
        await Flag("project_screens"); await f.Evaluate(p.Id); return (p, task, allocation, role);
    }

    async Task<string> Durable(Guid project) => await f.DbAsync(async db => JsonSerializer.Serialize(new {
        Project = await db.Projects.AsNoTracking().Where(p => p.Id == project).Select(p => new { p.RowVersion, p.HealthOverride, p.HealthOverrideNote, p.HealthOverrideAt, p.HealthOverrideBy, p.HealthOverrideExpiresAt, p.Status }).SingleAsync(),
        Tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project).OrderBy(t => t.Id).Select(t => new { t.Id, t.RowVersion, t.Status, t.EstimatedHours, t.ProgressPct }).ToArrayAsync(),
        Allocations = await db.Allocations.AsNoTracking().Where(a => a.ProjectId == project).OrderBy(a => a.Id).Select(a => new { a.Id, a.RowVersion, a.Status, a.ConfirmedAt }).ToArrayAsync(),
        Roles = await db.OpenRoles.AsNoTracking().Where(r => r.ProjectId == project).OrderBy(r => r.Id).Select(r => new { r.Id, r.Status, r.RowVersion, r.FilledAllocationId }).ToArrayAsync(),
        Versions = await db.PersonDateVersions.CountAsync(), Commands = await db.CoordinationCommands.CountAsync(),
        Activity = await db.ActivityLog.CountAsync(), Notices = await db.Notifications.CountAsync(), Outbox = await db.Outbox.CountAsync(), Emails = await db.Emails.CountAsync(),
        Health = await db.ProjectStates.AsNoTracking().Where(s => s.ProjectId == project).Select(s => new { s.ComputedHealth, s.HealthReasons, s.Counts }).SingleAsync(),
    }));

    [Fact]
    public async Task Exact_120_remaining_80_confirmed_24_open_roles_and_40_excess_remain_independent_read_only_facts()
    {
        var (p, _, _, role) = await NumericFixture(); var before = await Durable(p.Id); var read = await Read(p.Id); var row = Civil(read);
        Assert.Equal((120m, 80m, 24m, 40m), (N(row, "remainingHours"), N(row, "confirmedHours"), N(row, "openRoleHours"), N(row, "excessHours")));
        Assert.Equal(("Complete", "Complete", "2026-11-16", "2026-11-27"), (row.S("comparisonState"), read["staffing"]!.S("allocationCoverage"), read["staffing"]!.S("fromDate"), read["staffing"]!.S("throughDate")));
        Assert.Equal(read.S("asOf"), read["staffing"]!.S("asOf")); Assert.Equal(before, await Durable(p.Id));
        Assert.DoesNotContain(row.AsObject().Select(x => x.Key), key => key.Contains("total", StringComparison.OrdinalIgnoreCase));
        await f.DbAsync(async db => { (await db.OpenRoles.SingleAsync(r => r.Id == role)).PlannedHours = 64; return await db.SaveChangesAsync(); });
        var changed = Civil(await Read(p.Id)); Assert.Equal((64m, 40m), (N(changed, "openRoleHours"), N(changed, "excessHours")));
        Assert.DoesNotContain(read["staffing"]!["disciplines"]!.AsArray(), r => r!["unattributed"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Held_work_is_open_but_complete_cancelled_and_out_of_window_work_are_excluded()
    {
        var (p, _, _, _) = await NumericFixture();
        foreach (var status in new[] { TaskStatuses.OnHold, TaskStatuses.Complete, TaskStatuses.Cancelled })
        {
            var task = await d.NewTask(p.Id, extra: new { estimatedHours = status == TaskStatuses.OnHold ? 20m : 777m, dueDate = Mon.AddDays(2) });
            await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == task.G("id"))).Status = status; return await db.SaveChangesAsync(); });
        }
        await d.NewTask(p.Id, extra: new { estimatedHours = 888m, dueDate = Mon.AddDays(-1) });
        await d.NewTask(p.Id, extra: new { estimatedHours = 999m, dueDate = Mon.AddDays(12) });
        var row = Civil(await Read(p.Id)); Assert.Equal((140m, 80m, 24m, 60m), (N(row, "remainingHours"), N(row, "confirmedHours"), N(row, "openRoleHours"), N(row, "excessHours")));
    }

    [Fact]
    public async Task Partial_week_clips_full_original_override_spread_and_roles_to_inclusive_exact_dates()
    {
        var p = await d.Project(); await Milestone(p.Id, Mon.AddDays(2));
        var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 30m, startDate = Mon, dueDate = Mon.AddDays(2) });
        await Reserve(p, task, 40m, Mon, Mon.AddDays(4), [new(Mon, 2), new(Mon.AddDays(1), 12), new(Mon.AddDays(2), 4), new(Mon.AddDays(3), 8), new(Mon.AddDays(4), 14)]);
        var proposed = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 0m, startDate = Mon, dueDate = Mon.AddDays(2) });
        await Reserve(p, proposed, 75m, Mon, Mon.AddDays(4), confirm: false); await Role(p, 30m, Mon, Mon.AddDays(4));
        f.Clock.SetDate(2026, 11, 17); await Flag("project_screens");
        var read = await Read(p.Id); var row = Civil(read);
        Assert.Equal(("2026-11-17", "2026-11-18"), (read["staffing"]!.S("fromDate"), read["staffing"]!.S("throughDate")));
        Assert.Equal((30m, 16m, 12m, 14m), (N(row, "remainingHours"), N(row, "confirmedHours"), N(row, "openRoleHours"), N(row, "excessHours")));
    }

    [Fact]
    public async Task Unknown_work_and_lost_role_calendar_keep_known_subtotals_and_never_assert_a_zero_gap()
    {
        var p = await d.Project(); await Milestone(p.Id, Mon.AddDays(4));
        var work = await d.NewTask(p.Id, extra: new { estimatedHours = 150m, dueDate = Mon.AddDays(2) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == work.G("id"))).ProgressPct = 20; return await db.SaveChangesAsync(); });
        await d.NewTask(p.Id, extra: new { dueDate = Mon.AddDays(2) }); await d.NewTask(p.Id, extra: new { estimatedHours = 900m });
        await Role(p, 12m, Mon, Mon); await Role(p, 24m, Mon.AddDays(1), Mon.AddDays(4));
        await f.DbAsync(async db => { db.Holidays.Add(new Holiday { OfficeId = p.OfficeId, Date = Mon, Name = "Synthetic removed role day" }); return await db.SaveChangesAsync(); });
        await Flag("project_screens"); var read = await Read(p.Id); var row = Civil(read);
        Assert.Equal("Partial", read["staffing"]!.S("state")); Assert.Null(row["remainingHours"]); Assert.Equal(120m, N(row, "knownRemainingHours"));
        Assert.Equal((1, 1), (row.I("unestimatedTasks"), row.I("undatedTasks"))); Assert.Null(row["openRoleHours"]);
        Assert.Equal((24m, 1), (N(row, "knownOpenRoleHours"), row.I("unavailableOpenRoles"))); Assert.Null(row["excessHours"]); Assert.Equal("Unavailable", row.S("comparisonState"));
    }

    [Fact]
    public async Task Limited_visibility_and_positive_unattributed_reservations_prevent_definitive_discipline_comparisons()
    {
        var (p, task, _, _) = await NumericFixture();
        var limited = (await Read(p.Id, TestData.Omar))["staffing"]!;
        Assert.Equal("VisibleOnly", limited.S("allocationCoverage")); Assert.All(limited["disciplines"]!.AsArray(), row => { Assert.Null(row!["confirmedHours"]); Assert.Null(row["excessHours"]); });
        Assert.DoesNotContain("Alex", limited.ToJsonString());
        // A later assignment change invalidates attribution without rewriting the saved confirmation.
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == task.G("id"))).AssigneeId = d.User(TestData.Jill); return await db.SaveChangesAsync(); });
        var read = await Read(p.Id); var row = Civil(read); var staffing = read["staffing"]!;
        Assert.Equal(80m, N(staffing, "unattributedConfirmedHours")); Assert.Equal(80m, N(staffing, "knownUnattributedConfirmedHours"));
        Assert.Null(row["confirmedHours"]); Assert.Equal(0m, N(row, "knownConfirmedHours")); Assert.Null(row["excessHours"]);
        var unknown = Assert.Single(staffing["disciplines"]!.AsArray().Where(r => r!["unattributed"]!.GetValue<bool>()))!;
        Assert.Equal(80m, N(unknown, "knownConfirmedHours")); Assert.Null(unknown["excessHours"]);
    }

    [Fact]
    public async Task Package_target_is_independent_of_context_and_capacity_flags_with_deterministic_open_selection_and_fallback()
    {
        var p = await d.Project(); var milestone = await Milestone(p.Id, Mon.AddDays(2));
        await Package(p.Id, milestone, Mon.AddDays(-1), "TERMINAL", SubmissionStatus.Issued);
        await Package(p.Id, milestone, Mon.AddDays(4), "PKG-Z", SubmissionStatus.Ready);
        var chosen = await Package(p.Id, milestone, Mon.AddDays(4), "PKG-A", SubmissionStatus.Checking);
        await Flag("project_screens");
        foreach (var context in new[] { false, true })
        {
            await Flag("screen_patterns", context); await Flag("capacity_screens", context);
            var target = (await Read(p.Id))["staffing"]!["target"]!;
            Assert.Equal(("SubmissionPackage", chosen, "2026-11-20"), (target.S("kind"), target.G("id"), target.S("date")));
        }
        await f.DbAsync(async db => { foreach (var package in await db.SubmissionPackages.Where(package => package.ProjectId == p.Id).ToListAsync()) package.Status = SubmissionStatus.Cancelled; return await db.SaveChangesAsync(); });
        var fallback = (await Read(p.Id))["staffing"]!["target"]!; Assert.Equal(("Milestone", milestone, "2026-11-18"), (fallback.S("kind"), fallback.G("id"), fallback.S("date")));
    }

    [Fact]
    public async Task Missing_and_overdue_targets_have_no_fabricated_forward_window_or_zero_figures()
    {
        var p = await d.Project(); await Flag("project_screens");
        var missing = (await Read(p.Id))["staffing"]!;
        Assert.Equal("NoTarget", missing.S("state")); Assert.Null(missing["throughDate"]); Assert.Null(missing["target"]); Assert.Null(missing["unattributedConfirmedHours"]); Assert.Empty(missing["disciplines"]!.AsArray());
        var milestone = await Milestone(p.Id, Mon.AddDays(4)); var package = await Package(p.Id, milestone, Mon.AddDays(-1));
        var overdue = (await Read(p.Id))["staffing"]!;
        Assert.Equal(("OverdueTarget", "2026-11-15", package), (overdue.S("state"), overdue.S("throughDate"), overdue["target"]!.G("id")));
        Assert.Equal("Unavailable", overdue.S("allocationCoverage")); Assert.Empty(overdue["disciplines"]!.AsArray()); Assert.Null(overdue["knownUnattributedConfirmedHours"]);
    }

    [Fact]
    public async Task Flags_off_keep_the_exact_existing_dashboard_body_and_project_access_still_refuses_normally()
    {
        var p = await d.Project(); var foreign = await d.Project(); await Milestone(p.Id, Mon.AddDays(4)); await f.Evaluate(p.Id);
        // Initialize the separately tested one-time identity backfill before comparing the Dashboard read bodies.
        await Flag("visual_system"); await Flag("visual_system", false);
        var baseline = await Read(p.Id); Assert.False(baseline.AsObject().ContainsKey("staffing")); Assert.False(baseline.AsObject().ContainsKey("asOf"));
        await Flag("project_screens"); var enabled = await Read(p.Id); Assert.NotNull(enabled["staffing"]);
        enabled.AsObject().Remove("staffing"); enabled.AsObject().Remove("asOf"); Assert.True(JsonNode.DeepEquals(baseline, enabled));
        var foreignDiscipline = d.ProjectDiscipline(foreign.Id, "Civil");
        await (await f.As(TestData.Pm).GetAsync(Path(p.Id) + $"?disciplineId={foreignDiscipline}")).Json(404);
        await Flag("project_screens", false); await Flag("visual_system"); var visual = await Read(p.Id);
        Assert.False(visual.AsObject().ContainsKey("staffing")); Assert.NotNull(visual["asOf"]); visual.AsObject().Remove("asOf");
        // The same entries; the one-time identity backfill's IdentityColour change is shown only while visual_system is on (ActivityScreenFields).
        Assert.Equal(baseline["activity"]!.AsArray().Select(a => a!.S("id")), visual["activity"]!.AsArray().Select(a => a!.S("id")));
        var withoutActivity = baseline.DeepClone(); withoutActivity.AsObject().Remove("activity"); visual.AsObject().Remove("activity");
        Assert.True(JsonNode.DeepEquals(withoutActivity, visual));
        await Flag("visual_system", false); Assert.True(JsonNode.DeepEquals(baseline, await Read(p.Id)));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        foreach (var on in new[] { false, true }) { await Flag("project_screens", on); Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync(Path(p.Id))).StatusCode); }
    }
}
