using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S17/S18: FR/AC-SCR-28..31. New figures reuse the planner read and stay dark.
public sealed class ProjectListCompletenessTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    static readonly DateOnly Monday = new(2026, 9, 14);
    public ProjectListCompletenessTests() => d = new(f);
    public void Dispose() => f.Dispose();
    Task<JsonNode> Read(string path, string user = TestData.Admin) => f.As(user).GetAsync(path).Result.Json();
    async Task Setting(string key, object value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
    Task Enabled(bool on = true) => Setting("flag.project_screens", on);
    async Task<JsonNode> Row(Project p, string user = TestData.Admin) =>
        (await Read($"/api/v1/projects?Ids={p.Id}", user))["items"]!.AsArray().Single()!;
    Task<JsonNode> Plan(Guid person, decimal hours, DateOnly? week = null, Project? project = null) =>
        f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = person, hoursPerWeek = hours, startWeek = week ?? Monday, endWeek = week ?? Monday,
            label = "Synthetic visible plan", sourceCategory = project is null ? PlanningSource.Other : PlanningSource.MajorProject, confidence = PlanningConfidence.Expected, visibility = PlanningVisibility.Confirmed, projectId = project?.Id }).Result.Json(201);
    Task<JsonNode> Role(Project p, string name) => f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = name, fromDate = Monday, throughDate = Monday.AddDays(4), plannedHours = 8m }).Result.Json(201);

    [Fact]
    public async Task Staffing_setting_default_validation_and_hidden_off_contract_are_explicit()
    {
        Assert.Equal(4, new OrgSettings().StaffingLookaheadWeeks);
        Assert.Equal(4, OrgSettings.Defs.Single(x => x.Key == "staffing_lookahead_weeks").Default);
        Assert.Equal(2, OrgSettings.From(new Dictionary<string, JsonElement> { ["staffing_lookahead_weeks"] = JsonSerializer.SerializeToElement(2) }).StaffingLookaheadWeeks);
        Assert.DoesNotContain((await Read("/api/v1/admin/settings")).AsArray(), x => x!.S("key") == "staffing_lookahead_weeks");
        foreach (var value in new[] { 0, 27 })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Put("/api/v1/admin/settings/staffing_lookahead_weeks", new { value })).StatusCode);
        await Setting("staffing_lookahead_weeks", 2); await Enabled();
        var settings = (await Read("/api/v1/admin/settings")).AsArray();
        Assert.Equal(2, settings.Single(x => x!.S("key") == "staffing_lookahead_weeks")!["value"]!.GetValue<int>());
        var p = await d.Project(); var row = await Row(p);
        Assert.Equal(2, row["staffing"]!.I("lookaheadWeeks")); Assert.Equal("2026-09-27", row["staffing"]!.S("through"));
        await Enabled(false);
        var off = await Row(p); Assert.False(off.AsObject().ContainsKey("staffing")); Assert.False(off.AsObject().ContainsKey("span"));
        await f.As(TestData.Admin).GetAsync($"/api/v1/projects/{p.Id}/peek").Result.Json(404);
    }

    [Fact]
    public async Task Staffing_counts_open_roles_and_distinct_canonical_over_planned_team_people_without_changing_person_figures()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill);
        await Plan(alex, 50m); await Plan(alex, 50m, Monday.AddDays(7)); await Plan(jill, 50m); // scoped non-team person must not count
        var before = await Read($"/api/v1/planning/grid?personId={alex}&from={Monday:yyyy-MM-dd}&weeks=4");
        await Role(p, "Civil designer"); await Role(p, "Checker");
        var cancelled = await Role(p, "Cancelled demand");
        await f.As(TestData.Pm).Post($"/api/v1/open-roles/{cancelled.G("id")}/cancel", new { rowVersion = cancelled.I("rowVersion"), reason = "No longer needed" }).Result.Json();
        var off = await Read($"/api/v1/projects?Ids={p.Id}");
        await Enabled(); var on = await Read($"/api/v1/projects?Ids={p.Id}"); var row = on["items"]!.AsArray().Single()!;
        Assert.Equal(2, row["staffing"]!.I("openRoles")); Assert.Equal(1, row["staffing"]!.I("overCapacityPeople"));
        Assert.False(row["staffing"]!["limitedScope"]!.GetValue<bool>()); Assert.False(row["staffing"]!["partialView"]!.GetValue<bool>());
        var after = await Read($"/api/v1/planning/grid?personId={alex}&from={Monday:yyyy-MM-dd}&weeks=4");
        Assert.Equal(before["people"]![0]!["cells"]!.ToJsonString(), after["people"]![0]!["cells"]!.ToJsonString());
        row.AsObject().Remove("staffing"); row.AsObject().Remove("span"); row.AsObject().Remove("progressFacts"); row.AsObject().Remove("rowVersion");
        Assert.Equal(off.ToJsonString(), on.ToJsonString());
    }

    [Fact]
    public async Task Staffing_drilldown_filters_team_people_and_retains_their_other_project_inputs()
    {
        var p = await d.Project(); var other = await d.Project();
        await Plan(d.User(TestData.Alex), 50m, project: other); await Plan(d.User(TestData.Marc), 50m, project: other); await Plan(d.User(TestData.Jill), 50m, project: other);
        await Enabled(); var row = await Row(p); Assert.Equal(2, row["staffing"]!.I("overCapacityPeople"));
        var grid = await Read($"/api/v1/planning/grid?teamProjectId={p.Id}&from={Monday:yyyy-MM-dd}&weeks=4&indicator=OverPlanned");
        var ids = grid["people"]!.AsArray().Select(x => x!.G("id")).Order().ToArray();
        Assert.Equal(new[] { d.User(TestData.Alex), d.User(TestData.Marc) }.Order(), ids);
        Assert.All(grid["people"]!.AsArray(), x => Assert.Contains(x!["entries"]!.AsArray(), e => e!.G("projectId") == other.Id));
        Assert.Equal(ids.Length, row["staffing"]!.I("overCapacityPeople"));
        var own = await Row(p, TestData.Alex); Assert.True(own["staffing"]!["limitedScope"]!.GetValue<bool>()); Assert.True(own["staffing"]!["ownPlannerOnly"]!.GetValue<bool>());
        var ownGrid = await Read($"/api/v1/planning/grid?teamProjectId={p.Id}&personId={d.User(TestData.Alex)}&from={Monday:yyyy-MM-dd}&weeks=4&indicator=OverPlanned", TestData.Alex);
        Assert.Equal(d.User(TestData.Alex), Assert.Single(ownGrid["people"]!.AsArray())!.G("id"));
        Assert.Equal(1, own["staffing"]!.I("overCapacityPeople"));
    }

    [Fact]
    public async Task Staffing_partial_scope_does_not_expose_restricted_plan_metadata_or_claim_complete_zero()
    {
        var p = await d.Project(); var hidden = await d.Project(pm: TestData.Marc);
        await f.DbAsync(async db => { var row = await db.Projects.SingleAsync(x => x.Id == hidden.Id); row.Visibility = Visibility.Restricted; row.Name = "Private staffing sentinel"; await db.SaveChangesAsync(); return 0; });
        await Plan(d.User(TestData.Alex), 50m, project: hidden);
        await Enabled(); var row = await Row(p, TestData.Sam); var staffing = row["staffing"]!;
        Assert.True(staffing["partialView"]!.GetValue<bool>()); Assert.True(staffing["limitedScope"]!.GetValue<bool>());
        Assert.Equal(0, staffing.I("overCapacityPeople")); // zero shown over, explicitly partial, not an assertion of spare capacity
        Assert.DoesNotContain("Private staffing sentinel", row.ToJsonString());
        await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?teamProjectId={hidden.Id}&weeks=4").Result.Json(404);
        await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{hidden.Id}/peek").Result.Json(404);
    }

    [Fact]
    public async Task A_lost_allocation_calendar_day_isolated_to_one_person_keeps_project_rows_and_unknown_staffing_private()
    {
        var affected = await d.Project();
        var unaffected = await d.Project(tweak: body => body["members"] = Array.Empty<object>()); // Marc remains lead; Alex is not on this team.
        var hidden = await d.Project(pm: TestData.Marc, tweak: body => body["members"] = Array.Empty<object>());
        await f.DbAsync(async db => { var p = await db.Projects.SingleAsync(x => x.Id == hidden.Id); p.Visibility = Visibility.Restricted; p.Name = "Private calendar sentinel"; await db.SaveChangesAsync(); return 0; });
        await Plan(d.User(TestData.Marc), 50m);
        await Role(affected, "Unfilled designer");
        var task = await d.NewTask(affected.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), estimatedHours = 8m, startDate = Monday, dueDate = Monday });
        var path = $"/api/v1/projects/{affected.Id}/allocations";
        var allocation = await f.As(TestData.Pm).Post(path, new { requestId = Guid.NewGuid(), personId = d.User(TestData.Alex), purpose = AllocationPurpose.Production,
            fromDate = Monday, throughDate = Monday, plannedHours = 8m, days = Array.Empty<object>(), links = new[] { new { workType = "Task", workId = task.G("id"), workDate = Monday } } }).Result.Json();
        var preview = await Read($"{path}/{allocation.G("id")}/confirmation-preview", TestData.Sam);
        var version = Assert.Single(preview["days"]!.AsArray())!.I("dateVersion");
        await f.As(TestData.Sam).Post($"{path}/{allocation.G("id")}/confirm", new { requestId = Guid.NewGuid(), rowVersion = allocation.I("rowVersion"),
            dateVersions = new[] { new { workDate = Monday, rowVersion = version } } }).Result.Json();
        // Normal holiday editing is allowed after confirmation; this removes the allocation's only working day.
        await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { date = Monday, name = "Synthetic calendar change", officeId = (Guid?)null }).Result.Json(201);
        var unavailable = await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={d.User(TestData.Alex)}&from={Monday:yyyy-MM-dd}&weeks=4").Result.Json(409);
        Assert.Equal("allocation_calendar_changed", unavailable.S("code"));
        await Enabled();
        var response = await Read($"/api/v1/projects?Ids={affected.Id},{unaffected.Id},{hidden.Id}", TestData.Sam);
        var rows = response["items"]!.AsArray(); Assert.Equal(2, rows.Count);
        var unknown = rows.Single(x => x!.G("id") == affected.Id)!["staffing"]!;
        Assert.Null(unknown["overCapacityPeople"]); Assert.True(unknown["partialView"]!.GetValue<bool>()); Assert.Equal(1, unknown.I("openRoles"));
        var known = rows.Single(x => x!.G("id") == unaffected.Id)!["staffing"]!;
        Assert.Equal(1, known.I("overCapacityPeople")); Assert.False(known["partialView"]!.GetValue<bool>());
        Assert.DoesNotContain("Private calendar sentinel", response.ToJsonString()); Assert.DoesNotContain(hidden.Id.ToString(), response.ToJsonString());
        Assert.DoesNotContain("allocation_calendar_changed", response.ToJsonString());
        await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{hidden.Id}/peek").Result.Json(404); // authorization still propagates normally
    }

    [Fact]
    public async Task Staffing_window_uses_the_setting_and_closed_roles_never_enter_it()
    {
        var p = await d.Project(); await Plan(d.User(TestData.Alex), 50m, Monday.AddDays(21));
        await Enabled(); await Setting("staffing_lookahead_weeks", 2);
        Assert.Equal(0, (await Row(p))["staffing"]!.I("overCapacityPeople"));
        await Setting("staffing_lookahead_weeks", 4);
        Assert.Equal(1, (await Row(p))["staffing"]!.I("overCapacityPeople"));
        Assert.Equal(0, (await Row(p))["staffing"]!.I("openRoles"));
    }

    [Fact]
    public async Task Peek_reads_three_open_milestones_leads_overdue_counts_and_fresh_status_note()
    {
        var p = await d.Project(); var created = new List<JsonNode>();
        foreach (var date in new[] { "2026-09-18", "2026-09-16", "2026-09-15", "2026-09-17", "2026-09-14" })
            created.Add(await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Milestone " + date, milestoneType = "Design Submission", date }).Result.Json(201));
        await d.NewTask(p.Id, extra: new { dueDate = "2026-09-10" }); await d.NewTask(p.Id, extra: new { dueDate = "2026-09-11" });
        await d.NewTask(p.Id, discipline: "Electrical", as_: TestData.Pm, extra: new { dueDate = "2026-09-10" });
        await f.DbAsync(async db =>
        {
            (await db.Milestones.SingleAsync(m => m.Id == created[4].G("id"))).IsCancelled = true;
            var project = await db.Projects.SingleAsync(x => x.Id == p.Id); project.StatusNote = "Awaiting client decision"; project.StatusNoteBy = d.User(TestData.Pm); project.StatusNoteAt = f.Clock.GetUtcNow();
            (await db.Users.SingleAsync(u => u.Id == d.User(TestData.Omar))).IsActive = false;
            await db.SaveChangesAsync(); return 0;
        });
        await f.Evaluate(p.Id); // Evaluation worker is disabled in HubFactory; use the canonical materialized states.
        await Enabled();
        var noNote = await Read($"/api/v1/projects/{p.Id}/peek", TestData.Alex); Assert.False(noNote["statusNoteEnabled"]!.GetValue<bool>()); Assert.Null(noNote["statusNote"]);
        await Setting("flag.project_status_note", true);
        var peek = await Read($"/api/v1/projects/{p.Id}/peek", TestData.Alex);
        Assert.Equal(new[] { "2026-09-15", "2026-09-16", "2026-09-17" }, peek["milestones"]!.AsArray().Select(m => m!.S("date")));
        Assert.All(peek["milestones"]!.AsArray(), m => Assert.NotNull(m!["status"]));
        var civil = peek["disciplines"]!.AsArray().Single(x => x!.S("name") == "Civil")!;
        var electrical = peek["disciplines"]!.AsArray().Single(x => x!.S("name") == "Electrical")!;
        Assert.Equal(2, civil.I("overdueTasks")); Assert.Equal(d.User(TestData.Marc), civil["lead"]!.G("id"));
        Assert.Equal(1, electrical.I("overdueTasks")); Assert.Equal("Inactive", electrical["lead"]!.S("state"));
        Assert.Equal("Awaiting client decision", peek["statusNote"]!.S("text")); Assert.Equal(d.User(TestData.Pm), peek["statusNote"]!.G("authorId")); Assert.NotNull(peek["statusNote"]!["at"]);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).StatusNote = "Updated since first open"; await db.SaveChangesAsync(); return 0; });
        Assert.Equal("Updated since first open", (await Read($"/api/v1/projects/{p.Id}/peek", TestData.Alex))["statusNote"]!.S("text"));
        Assert.Equal(4, (await Row(p))["span"]!.I("openMilestones"));
    }

    [Fact]
    public async Task Existing_project_filters_are_composable_and_new_row_facts_keep_target_unknown()
    {
        var p = await d.Project(tweak: body => { body["priority"] = "High"; body["startDate"] = "2026-01-05"; body["targetCompletionDate"] = "2027-02-28"; });
        var noTarget = await d.Project(); await Enabled();
        var result = await Read($"/api/v1/projects?PmId={p.ProjectManagerId}&ClientId={p.ClientId}&OfficeId={p.OfficeId}&Priority=High");
        Assert.Equal(p.Id, Assert.Single(result["items"]!.AsArray())!.G("id"));
        var row = await Row(p); Assert.Equal("2026-01-05", row.S("startDate")); Assert.Equal("2027-02-28", row.S("targetCompletionDate")); Assert.Equal("2026-09-14", row["span"]!.S("today"));
        Assert.Null((await Row(noTarget))["targetCompletionDate"]); Assert.Equal(0, (await Row(noTarget))["span"]!.I("openMilestones"));
    }
}
