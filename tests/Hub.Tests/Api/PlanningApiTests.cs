using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Hub.Api.Features;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PlanningApiTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly Week = new(2026, 9, 14);

    async Task<Guid> FreshReport()
    {
        var email = $"synthetic-planning-{Guid.NewGuid():N}@hub.test";
        var me = await (await f.As(email).GetAsync("/api/v1/me")).Json();
        return await f.DbAsync(async db => { var u = await db.Users.SingleAsync(x => x.Email == email); u.SupervisorId = d.User(TestData.Sam); u.WeeklyCapacityHours = 40m; await db.SaveChangesAsync(); return u.Id; });
    }

    async Task<JsonNode> CreateDraft(Guid personId, string as_ = TestData.Sam, string label = "Proposal support")
        => await (await f.As(as_).Post("/api/v1/planning/entries", new
        {
            personId, hoursPerWeek = 8m, startWeek = Week, endWeek = Week,
            label, sourceCategory = PlanningSource.Proposal, confidence = PlanningConfidence.Expected,
        })).Json(201);

    [Fact, Trait("flag-off", "capacity_polish"), Trait("flag-on", "capacity_polish")]
    public async Task Capacity_polish_contexts_reconcile_project_plans_and_task_estimates_without_changing_capacity() // packet 047 AC-PRO-20
    {
        var person = await FreshReport();
        var p = await d.Project();
        var q = await d.Project();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person, estimatedHours = 10m, dueDate = Week.AddDays(4) });
        await d.NewTask(q.Id, TestData.Pm, new { assigneeId = person, estimatedHours = 4m, dueDate = Week.AddDays(4) });
        var entry = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new
        {
            personId = person, projectId = p.Id, hoursPerWeek = 8m, startWeek = Week, endWeek = Week,
            label = "Planned P work", sourceCategory = PlanningSource.MajorProject, confidence = PlanningConfidence.Expected,
        })).Json(201);
        try
        {
            await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_polish", new { value = false });
            var off = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{person}/{Week:yyyy-MM-dd}")).Json();
            var offGrid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={person}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
            Assert.False(off.AsObject().ContainsKey("projectContexts"));
            Assert.False(offGrid["people"]![0]!.AsObject().ContainsKey("taskContexts"));
            await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_polish", new { value = true });
            var on = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{person}/{Week:yyyy-MM-dd}")).Json();
            var onGrid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={person}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
            Assert.Equal(off["remaining"]!.GetValue<decimal>(), on["remaining"]!.GetValue<decimal>());
            Assert.Equal(off["bands"]!.ToJsonString(), on["bands"]!.ToJsonString());
            var contexts = on["projectContexts"]!.AsArray();
            var pContext = contexts.Single(x => x!.G("projectId") == p.Id);
            var qContext = contexts.Single(x => x!.G("projectId") == q.Id);
            Assert.Equal(8m, pContext["plannedHours"]!.GetValue<decimal>());
            Assert.Equal(10m, pContext["taskEstimates"]!.GetValue<decimal>());
            Assert.Equal(0m, qContext["plannedHours"]!.GetValue<decimal>());
            Assert.Equal(4m, qContext["taskEstimates"]!.GetValue<decimal>());
            Assert.Equal(14m, contexts.Sum(x => x!["taskEstimates"]!.GetValue<decimal>()));
            var taskContexts = onGrid["people"]![0]!["taskContexts"]!.AsArray();
            Assert.Contains(taskContexts, x => x!.G("projectId") == p.Id);
            Assert.Contains(taskContexts, x => x!.G("projectId") == q.Id);
            Assert.Equal(entry.G("projectId"), pContext.G("projectId"));
            var filtered = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={person}&projectId={q.Id}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
            Assert.Contains(filtered["people"]!.AsArray(), row => row!.G("id") == person);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_polish", new { value = false }); }
    }

    [Fact]
    public async Task Create_quick_add_and_list_return_weekly_entry_contract()
    {
        var alex = await FreshReport();
        var created = await CreateDraft(alex);
        Assert.Equal(alex, created.G("personId"));
        Assert.Equal("Draft", created.S("visibility"));
        Assert.Equal("Manager", created.S("ownerKind"));
        var quick = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries/quick",
            new { personId = alex, week = Week, text = "7.5 h review" })).Json(201);
        Assert.Equal(7.5m, quick["hoursPerWeek"]!.GetValue<decimal>());
        var list = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries?personId={alex}&from={Week:yyyy-MM-dd}&to={Week:yyyy-MM-dd}")).Json();
        Assert.True(list["totalCount"]!.GetValue<int>() >= 2);
    }

    [Fact]
    public async Task Post_is_idempotent_and_reusing_key_for_other_body_is_rejected()
    {
        var alex = await FreshReport();
        var body = new { personId = alex, hoursPerWeek = 4m, startWeek = Week, endWeek = Week, label = "Keyed plan", sourceCategory = PlanningSource.Other };
        using var firstRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/planning/entries") { Content = JsonContent.Create(body) };
        firstRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var key = firstRequest.Headers.GetValues("Idempotency-Key").Single();
        var first = await (await f.As(TestData.Sam).SendAsync(firstRequest)).Json(201);
        using var replayRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/planning/entries") { Content = JsonContent.Create(body) };
        replayRequest.Headers.Add("Idempotency-Key", key);
        var replay = await (await f.As(TestData.Sam).SendAsync(replayRequest)).Json(201);
        Assert.Equal(first.G("id"), replay.G("id"));
        using var conflicting = new HttpRequestMessage(HttpMethod.Post, "/api/v1/planning/entries") { Content = JsonContent.Create(body with { label = "Different" }) };
        conflicting.Headers.Add("Idempotency-Key", key);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Sam).SendAsync(conflicting)).StatusCode);
    }

    [Fact]
    public async Task Stale_patch_is_conflict_and_HR_leave_updates_date_version_atomically()
    {
        var alex = await FreshReport();
        var created = await CreateDraft(alex);
        var path = $"/api/v1/planning/entries/{created.G("id")}";
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Sam).Patch(path, new { label = "Changed", rowVersion = created.I("rowVersion") - 1 })).StatusCode);
        var day = Week.AddDays(1);
        await (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = alex, fromDate = day, throughDate = day, sourceSystem = "Synthetic HR", externalId = Guid.NewGuid().ToString(), status = "Approved", rowVersion = 0 })).Json();
        Assert.True(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == alex && v.WorkDate == day).RowVersion) >= 1);

    }

    [Fact]
    public async Task Grid_and_cell_expose_capacity_contributions_and_task_context()
    {
        var alex = await FreshReport();
        var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={alex}&from={Week:yyyy-MM-dd}&weeks=2")).Json();
        var person = grid["people"]!.AsArray().Single();
        Assert.True(person!["cells"]!.AsArray().Count == 2);
        Assert.NotNull(person["approvedAllocations"]);
        var cell = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{alex}/{Week:yyyy-MM-dd}")).Json();
        Assert.NotNull(cell["capacity"]);
        Assert.NotNull(cell["bands"]);
        Assert.NotNull(cell["taskEstimates"]);
    }

    [Fact]
    public async Task Visibility_still_valid_activity_delete_and_exports_are_versioned()
    {
        var alex = await FreshReport();
        var created = await CreateDraft(alex, label: "Lifecycle plan");
        var id = created.G("id");
        var path = $"/api/v1/planning/entries/{id}";
        var published = await (await f.As(TestData.Sam).Post(path + "/visibility", new { rowVersion = created.I("rowVersion"), visibility = PlanningVisibility.Published })).Json();
        var validated = await (await f.As(TestData.Sam).Post(path + "/still-valid", new { rowVersion = published.I("rowVersion") })).Json();
        Assert.True(validated.I("rowVersion") > published.I("rowVersion"));
        var activity = await (await f.As(TestData.Sam).GetAsync(path + "/activity")).Json();
        Assert.True(activity["items"]!.AsArray().Count >= 2);
        var csv = await f.As(TestData.Sam).GetAsync("/api/v1/planning/entries/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        var gridCsv = await f.As(TestData.Sam).GetAsync("/api/v1/planning/grid/export?format=csv");
        Assert.Equal(HttpStatusCode.OK, gridCsv.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Sam).DeleteAsync(path + $"?rowVersion={validated.I("rowVersion")}")).StatusCode);
        await (await f.As(TestData.Sam).GetAsync(path)).Json(404);
    }

    [Fact]
    public async Task List_filters_page_and_exports_keep_the_same_visible_contract()
    {
        var alex = await FreshReport();
        var first = await CreateDraft(alex, label: "Searchable planning label");
        var second = await CreateDraft(alex, label: "Another planning label");
        var list = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries?personId={alex}&q=Searchable&page=1&pageSize=1")).Json();
        Assert.Equal(1, list["page"]!.GetValue<int>());
        Assert.Equal(1, list["pageSize"]!.GetValue<int>());
        Assert.Equal(1, list["items"]!.AsArray().Count);
        Assert.Equal(first.G("id"), list["items"]![0]!.G("id"));
        foreach (var path in new[] { "/api/v1/planning/entries/export?format=csv", "/api/v1/planning/entries/export?format=xlsx", "/api/v1/planning/grid/export?format=csv", "/api/v1/planning/grid/export?format=xlsx" })
        {
            var response = await f.As(TestData.Sam).GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
        }
        Assert.NotEqual(first.G("id"), second.G("id"));
    }

    [Fact]
    public async Task HR_leave_range_is_atomic_and_stale_versions_write_nothing()
    {
        var alex = await FreshReport();
        var from = Week;
        var through = Week.AddDays(4);
        var externalId = Guid.NewGuid().ToString();
        var result = await (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = alex, fromDate = from, throughDate = through, sourceSystem = "Synthetic HR", externalId, status = "Approved", rowVersion = 0 })).Json();
        Assert.Equal(5, f.Db(db => db.PersonDateVersions.Count(x => x.PersonId == alex && x.WorkDate >= from && x.WorkDate <= through)));
        var stale = await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = alex, fromDate = from, throughDate = through, sourceSystem = "Synthetic HR", externalId, hoursPerDay = 4m, status = "Approved", rowVersion = 0 });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Null(f.Db(db => db.ApprovedLeaves.Single(l => l.Id == result.G("id")).HoursPerDay));

    }

    static JsonNode PickerCell(JsonNode grid, Guid id) => grid["people"]!.AsArray().Single(p => p!.G("id") == id)!["cells"]![0]!;

    [Fact]
    public async Task Person_ids_return_picker_capacity_inside_the_viewers_scope() // packet 036 AC-PICK-01a, -01b, -02a, -02d, -03a, R6
    {
        var project = await d.Project();
        var alex = await FreshReport();
        await f.DbAsync(async db => {
            db.Allocations.Add(new() { ProjectId = project.Id, PersonId = alex, FromDate = Week, ThroughDate = Week.AddDays(4), PlannedHours = 20m, Status = AllocationStatus.Confirmed,
                CreatedBy = d.User(TestData.Pm), ConfirmedBy = d.User(TestData.Sam), ConfirmedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        var expected = await CreateDraft(alex);
        await (await f.As(TestData.Sam).Post($"/api/v1/planning/entries/{expected.G("id")}/visibility", new { rowVersion = expected.I("rowVersion"), visibility = PlanningVisibility.Published })).Json();
        await (await f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = alex, hoursPerWeek = 10m, startWeek = Week, endWeek = Week,
            label = "Admin private draft", sourceCategory = PlanningSource.Other })).Json(201);
        var jill = await FreshReport();
        foreach (var day in new[] { Week, Week.AddDays(1) })
            await (await f.As(TestData.Sam).Put($"/api/v1/users/{jill}/availability/{day:yyyy-MM-dd}", new AllocationEndpoints.AvailabilityBody(0, 0, AvailabilityCategory.Unavailable))).Json();
        var (omar, diane) = (d.User(TestData.Omar), d.User(TestData.Diane)); // Lena's reports, outside Sam's scope
        var sam = d.User(TestData.Sam);
        Assert.False(f.Db(db => db.Users.Any(u => (u.Id == omar || u.Id == diane) && u.SupervisorId == sam)));

        var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={alex},{jill},{omar},{diane}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        Assert.Equal(new[] { alex, jill }.Order(), grid["people"]!.AsArray().Select(p => p!.G("id")).Order());
        Assert.Equal(12m, PickerCell(grid, alex)["remaining"]!.GetValue<decimal>()); // 40 - 20 confirmed allocation - 8 expected; the admin's draft is not counted
        Assert.Equal((24m, 16m), (PickerCell(grid, jill)["remaining"]!.GetValue<decimal>(), PickerCell(grid, jill)["timeAway"]!.GetValue<decimal>()));
        var cell = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{alex}/{Week:yyyy-MM-dd}")).Json();
        Assert.Equal(PickerCell(grid, alex)["remaining"]!.GetValue<decimal>(), cell["remaining"]!.GetValue<decimal>());
        var next = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={alex}&from={Week.AddDays(7):yyyy-MM-dd}&weeks=1")).Json();
        Assert.Equal((Week.AddDays(7), 40m), (DateOnly.Parse(PickerCell(next, alex).S("week")), PickerCell(next, alex)["remaining"]!.GetValue<decimal>()));

        // R6: an id outside the scope answers exactly like an id that does not exist.
        var hidden = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={omar}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        var missing = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={Guid.NewGuid()}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        Assert.Empty(hidden["people"]!.AsArray());
        Assert.Equal(missing["people"]!.ToJsonString(), hidden["people"]!.ToJsonString());
        Assert.Equal(missing["partialView"]!.ToJsonString(), hidden["partialView"]!.ToJsonString());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/planning/grid?personIds={d.User(TestData.Jill)}&from={Week:yyyy-MM-dd}&weeks=1")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/planning/grid?personIds={Guid.NewGuid()}&from={Week:yyyy-MM-dd}&weeks=1")).StatusCode);
    }

    [Fact]
    public async Task Picker_rows_say_when_a_person_has_planned_work_the_viewer_cannot_see() // packet 036 FR-PICK-02 with FR-CAP-06, AC-PLN-12
    {
        var project = await d.Project();
        var (jill, alex) = (await FreshReport(), await FreshReport());
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = "Restricted"; db.ProjectMembers.Add(new() { ProjectId = project.Id, UserId = jill, Roles = ["TeamMember"] }); return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = jill, hoursPerWeek = 30m, startWeek = Week, endWeek = Week, label = "Synthetic restricted study",
            sourceCategory = "MajorProject", projectId = project.Id, confidence = PlanningConfidence.Expected, visibility = PlanningVisibility.Published })).Json(201);

        var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={jill},{alex}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        JsonNode Row(Guid id) => grid["people"]!.AsArray().Single(p => p!.G("id") == id)!;
        Assert.True(grid["partialView"]!.GetValue<bool>());
        Assert.Equal((true, false), (Row(jill)["partial"]!.GetValue<bool>(), Row(alex)["partial"]!.GetValue<bool>()));
        Assert.Equal(40m, PickerCell(grid, jill)["remaining"]!.GetValue<decimal>()); // the hidden 30 h is not in the figure, so the flag must say so
        var admin = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?personIds={jill}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        Assert.Equal((false, 10m), (admin["people"]![0]!["partial"]!.GetValue<bool>(), PickerCell(admin, jill)["remaining"]!.GetValue<decimal>()));
    }

    [Fact]
    public async Task Every_free_hours_reader_marks_hidden_confirmed_work_partial() // packet 041 AC-FIG-02, FR-FIG-02
    {
        var person = await FreshReport();
        var project = await d.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; db.Allocations.Add(new() { PersonId = person, ProjectId = project.Id,
            Purpose = AllocationPurpose.Production, FromDate = Week, ThroughDate = Week.AddDays(4), PlannedHours = 20, Status = AllocationStatus.Confirmed }); return await db.SaveChangesAsync(); });
        // The Planner grid, the picker's batch read and My Staff (the same batch read) all go through the planning grid.
        var readers = new[] { $"/api/v1/planning/grid?supervisorId={d.User(TestData.Sam)}&from={Week:yyyy-MM-dd}&weeks=2", $"/api/v1/planning/grid?personIds={person}&from={Week:yyyy-MM-dd}&weeks=1" };
        const string workload = "/api/v1/workload?from=2026-09-14&to=2026-09-20";
        JsonNode Row(JsonNode body) => body["people"]!.AsArray().Single(p => p!.G("id") == person)!;
        foreach (var route in readers)
        {
            var sam = await (await f.As(TestData.Sam).GetAsync(route)).Json();
            Assert.True(Row(sam)["partial"]!.GetValue<bool>(), route);
            Assert.Equal(40m, Row(sam)["cells"]![0]!["remaining"]!.GetValue<decimal>()); // the value is unchanged; the flag says it is an upper bound
            Assert.Empty(Row(sam)["approvedAllocations"]!.AsArray());
            Assert.DoesNotContain(project.Id.ToString(), sam.ToJsonString()); Assert.DoesNotContain(project.ProjectNumber, sam.ToJsonString());
            var admin = await (await f.As(TestData.Admin).GetAsync(route)).Json();
            Assert.Equal((false, 20m), (Row(admin)["partial"]!.GetValue<bool>(), Row(admin)["cells"]![0]!["remaining"]!.GetValue<decimal>()));
        }
        var samLoad = await (await f.As(TestData.Sam).GetAsync(workload)).Json();
        Assert.True(Row(samLoad)["partialScope"]!.GetValue<bool>());
        Assert.Null(Row(samLoad)["cells"]![0]!["uncommitted"]?.GetValue<decimal>()); // partial and not negative
        Assert.DoesNotContain(project.ProjectNumber, Row(samLoad).ToJsonString());
        var adminLoad = Row(await (await f.As(TestData.Admin).GetAsync(workload)).Json());
        Assert.Equal((false, 20m), (adminLoad["partialScope"]!.GetValue<bool>(), adminLoad["cells"]![0]!["uncommitted"]!.GetValue<decimal>()));
    }

    async Task KeptRowsFlag(bool on) => await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.planner_kept_rows", new { value = on }).Result.Json();

    [Fact, Trait("flag-off", "unfilled_beside_capacity"), Trait("flag-on", "unfilled_beside_capacity")]
    public async Task Projects_grid_open_roles_are_opt_in_and_do_not_change_people_or_exports() // packet 047 AC-PRO-12
    {
        var project = await d.Project();
        var role = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/open-roles",
            new { roleLabel = "Civil reviewer", fromDate = Week, throughDate = Week.AddDays(4), plannedHours = 10m })).Json(201);
        var route = $"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=1&includeOpenRoles=true";
        var exportRoute = $"/api/v1/planning/grid/export?format=csv&from={Week:yyyy-MM-dd}&weeks=1&includeOpenRoles=true";
        try
        {
            await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.unfilled_beside_capacity", new { value = false });
            var off = await (await f.As(TestData.Pm).GetAsync(route)).Json();
            Assert.False(off.AsObject().ContainsKey("unfilledRoles"));
            await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.unfilled_beside_capacity", new { value = true });
            var on = await (await f.As(TestData.Pm).GetAsync(route)).Json();
            Assert.Equal(off["people"]!.ToJsonString(), on["people"]!.ToJsonString());
            var row = on["unfilledRoles"]!.AsArray().Single(x => x!.G("id") == role.G("id"));
            Assert.Equal(10m, row["plannedHours"]!.GetValue<decimal>());
            Assert.DoesNotContain("unfilledRoles", await (await f.As(TestData.Pm).GetAsync(exportRoute)).Content.ReadAsStringAsync());
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.unfilled_beside_capacity", new { value = false }); }
    }

    /// Jill's only Proposal entry, changed to Training, under a Proposal filter (AC-FIG-12).
    async Task<(Guid Jill, Guid Entry, string Route)> MovedOutOfFilter()
    {
        var jill = await FreshReport();
        var entry = (await CreateDraft(jill)).G("id");
        await f.DbAsync(async db => { (await db.PlanningEntries.SingleAsync(e => e.Id == entry)).SourceCategory = PlanningSource.Training; return await db.SaveChangesAsync(); });
        return (jill, entry, $"/api/v1/planning/grid?supervisorId={d.User(TestData.Sam)}&source={PlanningSource.Proposal}&from={Week:yyyy-MM-dd}&weeks=1");
    }

    [Fact, Trait("flag-on", "planner_kept_rows")]
    public async Task Kept_entries_and_rows_survive_filters_inside_visibility() // packet 041 AC-FIG-12, FR-FIG-13
    {
        await KeptRowsFlag(true);
        try
        {
            var (jill, entry, route) = await MovedOutOfFilter();
            JsonNode? Row(JsonNode body) => body["people"]!.AsArray().SingleOrDefault(p => p!.G("id") == jill);
            var plain = await (await f.As(TestData.Sam).GetAsync(route)).Json();
            Assert.Null(Row(plain)); Assert.DoesNotContain("\"kept\"", plain.ToJsonString()); // without keep: today's people and fields
            foreach (var keep in new[] { entry, jill })
            {
                var kept = Row(await (await f.As(TestData.Sam).GetAsync($"{route}&keep={keep}")).Json())!;
                Assert.True(kept["kept"]!.GetValue<bool>());
                var keptEntry = kept["entries"]!.AsArray().Single(e => e!.G("id") == entry)!;
                Assert.False(keptEntry["matchesFilter"]!.GetValue<bool>());
            }
            // A kept id the viewer cannot see changes nothing: an Admin's private draft for Jill.
            var hidden = (await (await f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = jill, hoursPerWeek = 4m, startWeek = Week, endWeek = Week,
                label = "Admin private draft", sourceCategory = PlanningSource.Training })).Json(201)).G("id");
            Assert.Null(Row(await (await f.As(TestData.Sam).GetAsync($"{route}&keep={hidden}")).Json()));
            Assert.Null(Row(await (await f.As(TestData.Sam).GetAsync($"{route}&keep={Guid.NewGuid()}")).Json()));
            // At most 50 ids; exports ignore keep.
            var tooMany = string.Join(',', Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()));
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync($"{route}&keep={tooMany}")).StatusCode);
            var name = (await f.DbAsync(db => db.Users.SingleAsync(u => u.Id == jill))).DisplayName;
            var csv = await (await f.As(TestData.Sam).GetAsync($"{route.Replace("/planning/grid?", "/planning/grid/export?format=csv&")}&keep={entry}")).Content.ReadAsStringAsync();
            Assert.DoesNotContain(name, csv);
        }
        finally { await KeptRowsFlag(false); }
    }

    [Fact, Trait("flag-off", "planner_kept_rows")]
    public async Task Grid_ignores_keep_while_the_flag_is_off() // packet 041 FR-FIG-14: with the flag off, keep is ignored and kept is absent
    {
        var (jill, entry, route) = await MovedOutOfFilter();
        var plain = await (await f.As(TestData.Sam).GetAsync(route)).Content.ReadAsStringAsync();
        var kept = await (await f.As(TestData.Sam).GetAsync($"{route}&keep={entry},{jill}")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(jill.ToString(), kept); Assert.DoesNotContain("\"kept\"", kept);
        Assert.Equal(JsonNode.Parse(plain)!["people"]!.ToJsonString(), JsonNode.Parse(kept)!["people"]!.ToJsonString());
        var unfiltered = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={jill}&from={Week:yyyy-MM-dd}&weeks=1&keep={entry}")).Content.ReadAsStringAsync();
        Assert.Contains(jill.ToString(), unfiltered); Assert.DoesNotContain("\"kept\"", unfiltered);
    }

    [Fact]
    public async Task Person_ids_are_limited_scoped_for_project_managers_and_rows_carry_person_fields() // packet 036 AC-PICK-02b, -03c, FR-GRP-02
    {
        var root = $"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=1&personIds=";
        Assert.NotNull((await (await f.As(TestData.Sam).GetAsync(root + string.Join(",", Enumerable.Range(0, 51).Select(_ => Guid.NewGuid())))).Json(400))["errors"]!["personIds"]);
        await (await f.As(TestData.Sam).GetAsync(root + string.Join(",", Enumerable.Range(0, 50).Select(_ => Guid.NewGuid())))).Json();
        await (await f.As(TestData.Sam).GetAsync(root + "not-a-person")).Json(400);

        var project = await d.Project();
        var member = await FreshReport();
        var stranger = await f.DbAsync(async db => { var u = new AppUser { Email = $"synthetic-stranger-{Guid.NewGuid():N}@hub.test", DisplayName = "Synthetic stranger" }; db.Users.Add(u); await db.SaveChangesAsync(); return u.Id; });
        var civil = await d.Discipline("Civil");
        await f.DbAsync(async db => {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = member, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            var u = await db.Users.SingleAsync(x => x.Id == member); u.JobTitle = "Civil Designer (EIT)"; u.HomeDisciplineId = civil;
            return await db.SaveChangesAsync();
        });
        var grid = await (await f.As(TestData.Pm).GetAsync(root + $"{member},{stranger}")).Json();
        var row = grid["people"]!.AsArray().Single()!;
        Assert.Equal(member, row.G("id"));
        Assert.Equal(("Civil Designer (EIT)", civil), (row.S("jobTitle"), row.G("homeDisciplineId")));
        Assert.True(row.AsObject().ContainsKey("officeId"));
    }

    [Fact]
    public async Task Existing_workload_and_allocation_reads_are_unchanged_by_planning_commands()
    {
        var project = await d.Project();
        var alex = await FreshReport();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = alex, startDate = Week, dueDate = Week.AddDays(2), estimatedHours = 8m });
        await f.DbAsync(async db => {
            db.Allocations.Add(new() { ProjectId = project.Id, PersonId = alex, FromDate = Week, ThroughDate = Week.AddDays(4), PlannedHours = 4m, Status = "Confirmed", CreatedBy = d.User(TestData.Pm), ConfirmedBy = d.User(TestData.Sam), ConfirmedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        var readinessPath = $"/api/v1/projects/{project.Id}/readiness/Task/{task.G("id")}";
        await (await f.As(f.Db(db => db.Users.Single(u => u.Id == alex).Email)).Post(readinessPath, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), task.I("rowVersion"), "Synthetic output", "Reviewed output"))).Json();
        var beforeReadiness = await (await f.As(TestData.Pm).GetAsync(readinessPath)).Json();
        var beforeCapacity = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), Week, f.Clock.Now));
        Assert.True(beforeCapacity.Applies); Assert.NotNull(beforeCapacity.Satisfied);
        var beforeWorkload = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload?from=2026-09-14&to=2026-09-20")).Content.ReadAsStringAsync();
        var beforeExport = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload/export?format=csv&from=2026-09-14&to=2026-09-20")).Content.ReadAsByteArrayAsync();
        var beforeAllocations = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/allocations")).Content.ReadAsStringAsync();
        var row = await CreateDraft(alex, label: "Unchanged workload context");
        var patched = await (await f.As(TestData.Sam).Patch($"/api/v1/planning/entries/{row.G("id")}", new { rowVersion = row.I("rowVersion"), label = "Updated context" })).Json();
        var published = await (await f.As(TestData.Sam).Post($"/api/v1/planning/entries/{row.G("id")}/visibility", new { rowVersion = patched.I("rowVersion"), visibility = "Published" })).Json();
        var validated = await (await f.As(TestData.Sam).Post($"/api/v1/planning/entries/{row.G("id")}/still-valid", new { rowVersion = published.I("rowVersion") })).Json();
        var withdrawn = await (await f.As(TestData.Sam).Post($"/api/v1/planning/entries/{row.G("id")}/visibility", new { rowVersion = validated.I("rowVersion"), visibility = "Draft" })).Json();
        await (await f.As(TestData.Sam).DeleteAsync($"/api/v1/planning/entries/{row.G("id")}?rowVersion={withdrawn.I("rowVersion")}")).Json(204);
        var afterWorkload = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload?from=2026-09-14&to=2026-09-20")).Content.ReadAsStringAsync();
        var afterExport = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload/export?format=csv&from=2026-09-14&to=2026-09-20")).Content.ReadAsByteArrayAsync();
        var afterAllocations = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/allocations")).Content.ReadAsStringAsync();
        var afterReadiness = await (await f.As(TestData.Pm).GetAsync(readinessPath)).Json();
        var afterCapacity = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), Week, f.Clock.Now));
        Assert.Equal(beforeCapacity, afterCapacity); Assert.Equal(beforeReadiness.ToJsonString(), afterReadiness.ToJsonString());
        Assert.Equal(beforeWorkload, afterWorkload);
        Assert.Equal(beforeExport, afterExport);
        Assert.Equal(beforeAllocations, afterAllocations);
    }
}
