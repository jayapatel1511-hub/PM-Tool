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
