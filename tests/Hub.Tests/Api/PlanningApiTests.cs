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
    public async Task Stale_patch_is_conflict_and_time_away_updates_date_version_atomically()
    {
        var alex = await FreshReport();
        var created = await CreateDraft(alex);
        var path = $"/api/v1/planning/entries/{created.G("id")}";
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Sam).Patch(path, new { label = "Changed", rowVersion = created.I("rowVersion") - 1 })).StatusCode);
        var day = Week.AddDays(1);
        var away = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new
        {
            personId = alex, from = day, through = day, action = "Record", availableHours = 0m, category = AvailabilityCategory.Unavailable,
            versions = new[] { new { workDate = day, rowVersion = 0 } },
        })).Json();
        Assert.Equal(0m, away["days"]![0]! ["availableHours"]!.GetValue<decimal>());
        Assert.True(away["days"]![0]! ["dateVersion"]!.GetValue<int>() >= 1);
        Assert.Equal(1, f.Db(db => db.PersonDateVersions.Count(v => v.PersonId == alex && v.WorkDate == day)));
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
    public async Task Time_away_range_is_atomic_and_stale_versions_write_nothing()
    {
        var alex = await FreshReport();
        var from = Week;
        var through = Week.AddDays(4);
        var versions = Enumerable.Range(0, 5).Select(i => new { workDate = from.AddDays(i), rowVersion = 0 }).ToArray();
        var result = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new
        {
            personId = alex, from, through, action = "Record", availableHours = 0m, category = "Unavailable", versions
        })).Json();
        Assert.Equal(5, result["days"]!.AsArray().Count);
        Assert.All(result["days"]!.AsArray(), day => Assert.True(day!["dateVersion"]!.GetValue<int>() > 0));
        var stale = await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new
        {
            personId = alex, from, through, action = "Record", availableHours = 4m, category = "Reduced",
            versions = new[] { new { workDate = from, rowVersion = 0 } }
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(5, f.Db(db => db.AvailabilityOverrides.Count(x => x.PersonId == alex && x.WorkDate >= from && x.WorkDate <= through)));
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
