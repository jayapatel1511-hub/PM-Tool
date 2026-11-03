using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR/AC-SCR-34..36 and Portfolio 052 S06: reads retain scope and canonical staffing;
/// recorded positive changes and missing history are different facts.
public sealed class PortfolioCompletenessTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public PortfolioCompletenessTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    Task<JsonNode> Read(string query, string user = TestData.Pm) => f.As(user).GetAsync($"/api/v1/portfolio?{query}").Result.Json();
    Task<int> MilestoneVersion(Guid id) => f.DbAsync(db => db.Milestones.Where(m => m.Id == id).Select(m => m.RowVersion).SingleAsync());

    [Fact]
    public async Task Independent_project_screen_flag_adds_server_time_staffing_and_unknown_history_and_rolls_back_exactly()
    {
        var p = await d.Project(); var before = await Read($"ids={p.Id}");
        await Flag("project_screens", true);
        var enabled = await Read($"ids={p.Id}");
        Assert.Equal(f.Clock.GetUtcNow(), DateTimeOffset.Parse(enabled.S("asOf")));
        Assert.NotNull(enabled["projects"]![0]!["staffing"]); Assert.Null(enabled["projects"]![0]!["span"]);
        Assert.Null(enabled["changes"]!["health"]!["count"]); Assert.Equal(0, enabled["changes"]!["health"]!.I("observed"));
        Assert.Equal(1, enabled["changes"]!["health"]!.I("unavailableProjects"));
        Assert.False(enabled.AsObject().ContainsKey("cardFacts")); // visual_system remains independently off
        await Flag("project_screens", false);
        Assert.Equal(before.ToJsonString(), (await Read($"ids={p.Id}")).ToJsonString());
    }

    [Fact]
    public async Task Complete_daily_readings_and_real_activity_show_one_health_move_two_distinct_slipped_milestones_and_one_status_move()
    {
        f.Clock.SetDate(2026, 9, 1);
        var p = await d.Project(); var status = await d.Project();
        await d.NewTask(p.Id, extra: new { dueDate = "2026-09-07" });
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic overdue fieldwork", milestoneType = "Field Work", date = "2026-09-05" }).Result.Json(201);
        var first = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic submission one", milestoneType = "Design Submission", date = "2026-09-20" }).Result.Json(201);
        var second = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic submission two", milestoneType = "Design Submission", date = "2026-09-21" }).Result.Json(201);
        f.Clock.SetDate(2026, 9, 12);
        await f.As(TestData.Pm).Post($"/api/v1/milestones/{first.G("id")}/change-date", new { newDate = "2026-09-24", reason = "Synthetic client extension", rowVersion = await MilestoneVersion(first.G("id")) }).Result.Json();
        await f.As(TestData.Pm).Post($"/api/v1/milestones/{second.G("id")}/change-date", new { newDate = "2026-09-25", reason = "Synthetic client extension", rowVersion = await MilestoneVersion(second.G("id")) }).Result.Json();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{status.Id}/transition", new { toStatus = "On Hold", reason = "Synthetic review pause", rowVersion = d.Version(status.Id) }).Result.Json();
        f.Clock.SetDate(2026, 9, 13);
        await f.As(TestData.Pm).Post($"/api/v1/milestones/{first.G("id")}/change-date", new { newDate = "2026-09-26", reason = "Synthetic second extension", rowVersion = await MilestoneVersion(first.G("id")) }).Result.Json();
        f.Clock.SetDate(2026, 9, 14); await f.Evaluate(p.Id); await f.Evaluate(status.Id);
        var current = await f.DbAsync(db => db.ProjectStates.Where(s => s.ProjectId == p.Id || s.ProjectId == status.Id).ToDictionaryAsync(s => s.ProjectId, s => s.ComputedHealth));
        Assert.Equal(Health.Red, current[p.Id]);
        await f.DbAsync(async db =>
        {
            for (var i = 0; i < 7; i++) foreach (var project in new[] { p, status })
                db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = project.Id, SnapshotDate = new DateOnly(2026, 9, 7).AddDays(i),
                    ComputedHealth = project.Id == p.Id && i == 0 ? Health.Yellow : current[project.Id], ReportedHealth = current[project.Id] });
            // Future records must not leak into this read, even though stored in the synthetic database.
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Project, ItemId = p.Id, OccurredAt = f.Clock.GetUtcNow().AddDays(1), Action = "StatusChanged", Changes = "[{\"field\":\"Status\",\"old\":\"Active\",\"new\":\"On Hold\"}]" });
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Milestone, ItemId = Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow().AddDays(1), Action = "DateChanged", Changes = "[{\"field\":\"Date\",\"old\":\"2026-09-20\",\"new\":\"2026-09-30\"}]" });
            await db.SaveChangesAsync(); return 0;
        });
        await Flag("project_screens", true);
        var changes = (await Read($"ids={p.Id},{status.Id}"))["changes"]!;
        Assert.Equal(7, changes.I("days")); Assert.Equal("2026-09-07", changes.S("fromDate")); Assert.Equal(2, changes.I("scopeProjects"));
        Assert.Equal(1, changes["health"]!.I("count")); Assert.Equal(p.Id, Assert.Single(changes["health"]!["projects"]!.AsArray())!.G("id"));
        Assert.Equal(2, changes["milestones"]!.I("count")); Assert.Equal(p.Id, Assert.Single(changes["milestones"]!["projects"]!.AsArray())!.G("id"));
        Assert.Equal(1, changes["status"]!.I("count")); Assert.Equal(status.Id, Assert.Single(changes["status"]!["projects"]!.AsArray())!.G("id"));
        foreach (var metric in new[] { "health", "milestones", "status" }) Assert.Equal(0, changes[metric]!.I("unavailableProjects"));
    }

    [Fact]
    public async Task Missing_daily_history_or_unusable_activity_never_becomes_zero_but_positive_recorded_evidence_is_retained()
    {
        f.Clock.SetDate(2026, 9, 1); var p = await d.Project();
        f.Clock.SetDate(2026, 9, 14); await f.Evaluate(p.Id);
        await f.DbAsync(async db =>
        {
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Milestone, ItemId = Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow(), Action = "DateChanged", Changes = "[{\"field\":\"Date\",\"old\":\"not-a-date\",\"new\":\"2026-09-30\"}]" });
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Milestone, ItemId = Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow(), Action = "DateChanged", Changes = "[{\"field\":\"Date\",\"old\":\"2026-09-20\",\"new\":\"2026-09-30\"}]" });
            await db.SaveChangesAsync(); return 0;
        });
        await Flag("project_screens", true);
        var changes = (await Read($"ids={p.Id}"))["changes"]!;
        Assert.Null(changes["health"]!["count"]); Assert.Equal(0, changes["health"]!.I("observed")); Assert.Equal(1, changes["health"]!.I("unavailableProjects"));
        Assert.Null(changes["milestones"]!["count"]); Assert.Equal(1, changes["milestones"]!.I("observed")); Assert.Equal(1, changes["milestones"]!.I("unavailableProjects"));
        Assert.Equal(p.Id, Assert.Single(changes["milestones"]!["projects"]!.AsArray())!.G("id"));
        Assert.Equal(0, changes["status"]!.I("count")); // retained creation + no status event in this window is known
    }

    [Theory]
    [InlineData(true, false, "[]")]
    [InlineData(true, false, "[{\"field\":\"Name\",\"old\":\"Old\",\"new\":\"New\"}]")]
    [InlineData(true, true, "[]")]
    [InlineData(true, true, "[{\"field\":\"Name\",\"old\":\"Old\",\"new\":\"New\"}]")]
    [InlineData(false, false, "[]")]
    [InlineData(false, false, "[{\"field\":\"Name\",\"old\":\"Old\",\"new\":\"New\"}]")]
    [InlineData(false, true, "[]")]
    [InlineData(false, true, "[{\"field\":\"Name\",\"old\":\"Old\",\"new\":\"New\"}]")]
    public async Task Marked_events_with_empty_or_omitted_transition_payload_are_unknown_and_keep_positive_observations(bool projectEvent, bool categoryMarker, string missing)
    {
        f.Clock.SetDate(2026, 9, 1); var p = await d.Project();
        f.Clock.SetDate(2026, 9, 14);
        await f.DbAsync(async db =>
        {
            var type = projectEvent ? ItemType.Project : ItemType.Milestone;
            var action = projectEvent ? "StatusChanged" : "DateChanged";
            var good = projectEvent ? "[{\"field\":\"Status\",\"old\":\"Setup\",\"new\":\"Active\"}]" : "[{\"field\":\"Date\",\"old\":\"2026-09-20\",\"new\":\"2026-09-30\"}]";
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = type, ItemId = projectEvent ? p.Id : Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow(), Action = action, Changes = good });
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = type, ItemId = projectEvent ? p.Id : Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow(),
                Action = categoryMarker ? "Updated" : action, Categories = categoryMarker ? [projectEvent ? "status" : "date"] : [], Changes = missing });
            await db.SaveChangesAsync(); return 0;
        });
        await Flag("project_screens", true);
        var changes = (await Read($"ids={p.Id}"))["changes"]!; var key = projectEvent ? "status" : "milestones";
        Assert.Null(changes[key]!["count"]); Assert.Equal(1, changes[key]!.I("observed")); Assert.Equal(1, changes[key]!.I("unavailableProjects"));
        Assert.Equal(p.Id, Assert.Single(changes[key]!["projects"]!.AsArray())!.G("id"));
        Assert.Equal(0, changes[projectEvent ? "milestones" : "status"]!.I("count"));
    }

    [Fact]
    public async Task Unrelated_ordinary_edits_and_explicit_initial_or_cleared_dates_remain_known_non_moves()
    {
        f.Clock.SetDate(2026, 9, 1); var p = await d.Project();
        f.Clock.SetDate(2026, 9, 14);
        await f.DbAsync(async db =>
        {
            ActivityLog Row(string type, string action, string[] categories, string changes) => new() { ProjectId = p.Id, ItemType = type, ItemId = type == ItemType.Project ? p.Id : Guid.CreateVersion7(), OccurredAt = f.Clock.GetUtcNow(), Action = action, Categories = [.. categories], Changes = changes };
            db.ActivityLog.AddRange(
                Row(ItemType.Project, "Updated", [], "[]"),
                Row(ItemType.Milestone, "Updated", [], "[{\"field\":\"Name\",\"old\":\"Old\",\"new\":\"New\"}]"),
                Row(ItemType.Project, "DateChanged", ["date"], "[{\"field\":\"StartDate\",\"old\":\"2026-09-20\",\"new\":\"2026-09-30\"}]"),
                Row(ItemType.Milestone, "StatusChanged", ["status"], "[{\"field\":\"IsComplete\",\"old\":false,\"new\":true}]"),
                Row(ItemType.Milestone, "DateChanged", ["date"], "[{\"field\":\"Date\",\"old\":null,\"new\":\"2026-09-30\"}]"),
                Row(ItemType.Milestone, "DateChanged", ["date"], "[{\"field\":\"Date\",\"old\":\"2026-09-20\",\"new\":null}]"));
            await db.SaveChangesAsync(); return 0;
        });
        await Flag("project_screens", true);
        var changes = (await Read($"ids={p.Id}"))["changes"]!;
        foreach (var key in new[] { "status", "milestones" }) { Assert.Equal(0, changes[key]!.I("count")); Assert.Equal(0, changes[key]!.I("observed")); Assert.Equal(0, changes[key]!.I("unavailableProjects")); Assert.Empty(changes[key]!["projects"]!.AsArray()); }
    }

    [Fact]
    public async Task Staffing_matches_the_project_adapter_and_hidden_projects_never_enter_change_lists_or_metadata()
    {
        var p = await d.Project(); var hidden = await d.Project(pm: TestData.Marc, tweak: body => body["members"] = Array.Empty<object>());
        await f.DbAsync(async db => { var row = await db.Projects.SingleAsync(x => x.Id == hidden.Id); row.Visibility = Visibility.Restricted; row.Name = "Private portfolio history sentinel"; await db.SaveChangesAsync(); return 0; });
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = "Synthetic vacant reviewer", fromDate = "2026-09-14", throughDate = "2026-09-18", plannedHours = 8m }).Result.Json(201);
        await f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = d.User(TestData.Alex), hoursPerWeek = 50m, startWeek = "2026-09-14", endWeek = "2026-09-14", label = "Synthetic visible plan", sourceCategory = PlanningSource.Other, confidence = PlanningConfidence.Expected, visibility = PlanningVisibility.Confirmed }).Result.Json(201);
        await Flag("project_screens", true);
        var portfolio = await Read($"mine=false&ids={p.Id},{hidden.Id}"); var row = Assert.Single(portfolio["projects"]!.AsArray())!;
        Assert.Equal(p.Id, row.G("id")); Assert.Equal(1, row["staffing"]!.I("openRoles")); Assert.Equal(1, row["staffing"]!.I("overCapacityPeople"));
        var list = await f.As(TestData.Pm).GetAsync($"/api/v1/projects?mine=false&ids={p.Id},{hidden.Id}").Result.Json();
        Assert.Equal(Assert.Single(list["items"]!.AsArray())!["staffing"]!.ToJsonString(), row["staffing"]!.ToJsonString());
        Assert.Equal(1, portfolio["changes"]!.I("scopeProjects")); Assert.DoesNotContain("Private portfolio history sentinel", portfolio.ToJsonString());
        Assert.DoesNotContain(hidden.Id.ToString(), portfolio.ToJsonString());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/portfolio")).StatusCode);
    }

    [Fact]
    public async Task Overdue_decision_card_reconciles_five_decisions_across_two_projects_with_exact_destination_registers()
    {
        var first = await d.Project(); var second = await d.Project(); var empty = await d.Project();
        var pm = f.As(TestData.Pm);
        foreach (var (project, count) in new[] { (first, 3), (second, 2) })
        {
            var party = await pm.Post($"/api/v1/projects/{project.Id}/external-parties", new { name = "Synthetic decision owner", isClient = true }).Result.Json(201);
            for (var i = 0; i < count; i++)
                await pm.Post($"/api/v1/projects/{project.Id}/decisions", new { subject = $"Synthetic overdue choice {i + 1}", description = "Synthetic review evidence", ownerExternalPartyId = party.G("id"),
                    requiredByDate = "2026-09-10", impactLevel = "Medium", impactDescription = "Synthetic dependent work waits" }).Result.Json(201);
        }
        await f.Evaluate(first.Id); await f.Evaluate(second.Id);
        await Flag("visual_system", true);
        var query = $"ids={first.Id},{second.Id},{empty.Id}";
        var before = await Read(query); var destination = await Read($"{query}&overdueDecisions=true");
        var rows = destination["projects"]!.AsArray();
        Assert.Equal(5, before["tiles"]!.I("overdueDecisions")); Assert.Equal(5, destination["tiles"]!.I("overdueDecisions"));
        Assert.Equal(2, before["cardFacts"]!["overdueDecisions"]!.I("projects")); Assert.Equal(1, before["cardFacts"]!["overdueDecisions"]!.I("clients"));
        Assert.Equal(new[] { first.Id, second.Id }.Order(), rows.Select(r => r!.G("id")).Order());
        Assert.Equal(5, rows.Sum(r => r!.I("overdueDecisions")));
        foreach (var row in rows)
        {
            var register = (await pm.GetAsync($"/api/v1/projects/{row!.G("id")}/decisions?indicator=overdue").Result.Json()).AsArray();
            Assert.Equal(row.I("overdueDecisions"), register.Count);
        }
    }

    [Fact]
    public async Task Visual_card_where_facts_use_each_tiles_destination_cohort_even_when_current_rows_are_on_hold()
    {
        var active = await d.Project(); var held = await d.Project();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{held.Id}/transition", new { toStatus = "On Hold", reason = "Synthetic review pause", rowVersion = d.Version(held.Id) }).Result.Json();
        await Flag("visual_system", true);
        var result = await Read($"ids={active.Id},{held.Id}&status=On%20Hold");
        Assert.Equal(held.Id, Assert.Single(result["projects"]!.AsArray())!.G("id")); Assert.Equal(1, result["tiles"]!.I("active"));
        Assert.Equal(1, result["cardFacts"]!["active"]!.I("projects")); Assert.Equal(1, result["cardFacts"]!["active"]!.I("clients"));
        Assert.Equal(0, result["cardFacts"]!["overdueDecisions"]!.I("projects")); Assert.Equal(0, result["cardFacts"]!["overdueDecisions"]!.I("clients"));
        Assert.False(result.AsObject().ContainsKey("changes")); Assert.Null(result["projects"]![0]!["staffing"]);
        var target = await Read($"ids={active.Id},{held.Id}&status=Active"); Assert.Equal(result["tiles"]!.I("active"), target["projects"]!.AsArray().Count);
    }
}
