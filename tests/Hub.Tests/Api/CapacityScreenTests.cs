using System.Net;
using System.Text.Json.Nodes;
using System.IO.Compression;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// 053 S26/S28: exact canonical figures, scoped availability, reversible scenario and unchanged dark reads.
public sealed class CapacityScreenTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    static readonly DateOnly Week = new(2026, 9, 14);
    public CapacityScreenTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value = on })).EnsureSuccessStatusCode();
    async Task<Guid> Person(string name, Guid? discipline = null, Guid? supervisor = null, Guid? project = null)
    {
        var email = $"capacity-screen-{Guid.NewGuid():N}@hub.test";
        await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var id = d.User(email); var manager = supervisor ?? d.User(TestData.Sam);
        await f.DbAsync(async db =>
        {
            var u = await db.Users.SingleAsync(u => u.Id == id);
            u.DisplayName = name; u.HomeDisciplineId = discipline; u.SupervisorId = manager; u.WeeklyCapacityHours = 40m;
            if (project is { } pid) db.ProjectMembers.Add(new ProjectMember { ProjectId = pid, UserId = id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        return id;
    }
    async Task Plan(Guid person, decimal hours, int weeks = 4, string confidence = PlanningConfidence.Expected)
        => await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = person, hoursPerWeek = hours,
            startWeek = Week, endWeek = Week.AddDays(7 * (weeks - 1)), label = "Synthetic capacity plan", sourceCategory = PlanningSource.Other, confidence })).Json(201);
    string Room(Guid? discipline = null, string extra = "") => $"/api/v1/planning/find-room?from={Week:yyyy-MM-dd}&weeks=4&hoursPerWeek=8{(discipline is { } id ? $"&disciplineId={id}" : "")}{extra}";
    static JsonNode LoadPerson(JsonNode grid, Guid id) => grid["people"]!.AsArray().Single(p => p!.G("id") == id)!;

    [Fact]
    public async Task Find_room_uses_every_exact_planner_week_own_drafts_and_home_discipline_in_name_order()
    {
        await Flag(true); var civil = await d.Discipline("Civil");
        var amy = await Person("Amy synthetic", civil); var zara = await Person("Zara synthetic", civil);
        var busy = await Person("Busy synthetic", civil); var other = await Person("Other discipline", await d.Discipline("Electrical"));
        var outsider = await Person("Outside scope", civil, d.User(TestData.Lena));
        await Plan(amy, 32); await Plan(zara, 8); await Plan(zara, 40, confidence: PlanningConfidence.Possible);
        await Plan(busy, 33, weeks: 1);
        var response = await (await f.As(TestData.Sam).GetAsync(Room(civil))).Json();
        var matches = response["matches"]!.AsArray();
        var ours = matches.Where(p => p!.G("id") == amy || p!.G("id") == zara).ToArray();
        Assert.Equal(new[] { amy, zara }, ours.Select(p => p!.G("id")));
        Assert.All(ours[0]!["weeks"]!.AsArray(), w => Assert.Equal(8m, w!["freeHours"]!.GetValue<decimal>()));
        Assert.All(ours[1]!["weeks"]!.AsArray(), w => Assert.Equal(32m, w!["freeHours"]!.GetValue<decimal>()));
        Assert.DoesNotContain(matches, p => new[] { busy, other, outsider }.Contains(p!.G("id")));
        Assert.DoesNotContain(outsider.ToString(), response.ToJsonString());
        var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={amy}&from={Week:yyyy-MM-dd}&weeks=4")).Json();
        Assert.Equal(grid["people"]![0]!["cells"]!.AsArray().Select(c => c!["remaining"]!.GetValue<decimal>()), ours[0]!["weeks"]!.AsArray().Select(c => c!["freeHours"]!.GetValue<decimal>()));
    }

    [Fact]
    public async Task Find_room_never_matches_partial_or_provisional_availability_and_does_not_leak_hidden_work()
    {
        await Flag(true); var civil = await d.Discipline("Civil");
        var partial = await Person("Partial synthetic", civil); var provisional = await Person("Provisional synthetic", civil);
        var hidden = await d.Project();
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted;
            db.PlanningEntries.Add(new PlanningEntry { PersonId = partial, ProjectId = hidden.Id, HoursPerWeek = 36,
                StartWeek = Week, EndWeek = Week.AddDays(21), Label = "HIDDEN CAPACITY PLAN", SourceCategory = PlanningSource.MajorProject, Confidence = PlanningConfidence.Expected,
                Visibility = PlanningVisibility.Published, CreatedBy = d.User(TestData.Admin), LastValidatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{provisional}/availability/{Week:yyyy-MM-dd}", new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json();
        var result = await (await f.As(TestData.Sam).GetAsync(Room(civil))).Json();
        Assert.DoesNotContain(result["matches"]!.AsArray(), p => p!.G("id") == partial || p.G("id") == provisional);
        Assert.Equal("PartialView", result["unavailable"]!.AsArray().Single(p => p!.G("id") == partial)!.S("reason"));
        Assert.Equal("ProvisionalAvailability", result["unavailable"]!.AsArray().Single(p => p!.G("id") == provisional)!.S("reason"));
        Assert.DoesNotContain(hidden.Id.ToString(), result.ToJsonString());
        Assert.DoesNotContain("HIDDEN CAPACITY PLAN", result.ToJsonString());
        Assert.DoesNotContain(hidden.ProjectNumber, result.ToJsonString());
    }

    [Fact]
    public async Task Find_room_is_dark_permission_guarded_validated_and_read_only()
    {
        await Flag(false);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync(Room())).StatusCode);
        var before = (await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=4")).Json())["people"]!.ToJsonString();
        await Flag(true);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync(Room())).StatusCode);
        foreach (var query in new[] { "hoursPerWeek=0&weeks=4&from=2026-09-14", "hoursPerWeek=8&weeks=27&from=2026-09-14", "hoursPerWeek=8&weeks=4&from=2026-09-15" })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/find-room?{query}")).StatusCode);
        var writes = f.Db(db => db.ActivityLog.Count());
        await (await f.As(TestData.Sam).GetAsync(Room())).Json();
        Assert.Equal(writes, f.Db(db => db.ActivityLog.Count()));
        Assert.Equal(before, (await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=4")).Json())["people"]!.ToJsonString());
    }

    [Fact]
    public async Task Discipline_context_shows_40_free_30_roles_20_unassigned_and_10_short_without_changing_people()
    {
        var civil = await d.Discipline("Civil"); var supervisor = await Person("Synthetic manager"); var project = await d.Project();
        var person = await Person("Civil supply", civil, supervisor, project.Id);
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person, estimatedHours = 4m, dueDate = Week.AddDays(4) });
        var url = $"/api/v1/workload?from={Week:yyyy-MM-dd}&supervisorId={supervisor}&projectId={project.Id}";
        await Flag(false); var before = await (await f.As(TestData.Admin).GetAsync(url)).Json();
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/open-roles", new { roleLabel = "Synthetic Civil role", projectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"),
            fromDate = Week, throughDate = Week.AddDays(4), plannedHours = 30m })).Json(201);
        await d.NewTask(project.Id, TestData.Pm, new { estimatedHours = 20m, dueDate = Week.AddDays(4) });
        await Flag(true); var after = await (await f.As(TestData.Admin).GetAsync(url)).Json();
        Assert.Equal(before["people"]!.ToJsonString(), after["people"]!.ToJsonString());
        var discipline = after["disciplineContext"]!.AsArray().Single(row => row!.G("disciplineId") == civil)!;
        Assert.Equal(1, discipline.I("people")); var cell = discipline["weeks"]![0]!;
        Assert.Equal(40m, cell["freeHours"]!.GetValue<decimal>());
        Assert.Equal(30m, cell["unfilledRoleHours"]!.GetValue<decimal>());
        Assert.Equal(20m, cell["knownUnassignedTaskHours"]!.GetValue<decimal>());
        Assert.Equal(10m, cell["shortfallHours"]!.GetValue<decimal>());
        Assert.Contains(after["people"]!.AsArray(), p => p!.G("id") == person);
    }

    [Fact]
    public async Task Discipline_context_keeps_unknown_estimates_undated_work_and_partial_supply_explicit()
    {
        await Flag(true); var civil = await d.Discipline("Civil"); var manager = await Person("Synthetic manager");
        var person = await Person("Partial supply", civil, manager); var project = await d.Project(); var hidden = await d.Project();
        await d.NewTask(project.Id, TestData.Pm, new { dueDate = Week.AddDays(4) });
        await d.NewTask(project.Id, TestData.Pm, new { estimatedHours = 20m });
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted;
            db.PlanningEntries.Add(new PlanningEntry { PersonId = person, ProjectId = hidden.Id, HoursPerWeek = 4, StartWeek = Week, EndWeek = Week,
                Label = "SECRET SUPPLY", SourceCategory = PlanningSource.MajorProject, Confidence = PlanningConfidence.Expected, Visibility = PlanningVisibility.Published,
                CreatedBy = d.User(TestData.Admin), LastValidatedAt = f.Clock.GetUtcNow() });
            // Sam is allowed this exact person through the ordinary supervisor relation.
            (await db.Users.SingleAsync(u => u.Id == person)).SupervisorId = d.User(TestData.Sam);
            return await db.SaveChangesAsync();
        });
        var result = await (await f.As(TestData.Sam).GetAsync($"/api/v1/workload?from={Week:yyyy-MM-dd}&projectId={project.Id}&disciplineId={civil}")).Json();
        var row = result["disciplineContext"]!.AsArray().Single(r => r!.G("disciplineId") == civil)!;
        Assert.Equal(1, row.I("undatedUnassignedTasks")); var cell = row["weeks"]![0]!;
        Assert.Null(cell["freeHours"]); Assert.Null(cell["shortfallHours"]); Assert.Equal(1, cell.I("unestimatedTasks"));
        Assert.True(cell.I("unavailablePeople") >= 1);
        Assert.DoesNotContain("SECRET SUPPLY", result.ToJsonString()); Assert.DoesNotContain(hidden.Id.ToString(), result.ToJsonString());
    }

    [Fact]
    public async Task Proposed_scenario_reuses_committed_rule_without_double_counting_links_or_writing_and_exports_its_basis()
    {
        var project = await d.Project(); var person = await Person("Scenario synthetic", project: project.Id);
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person, estimatedHours = 8m, startDate = Week, dueDate = Week });
        var allocation = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/allocations", new AllocationEndpoints.CreateBody(Guid.NewGuid(), person,
            AllocationPurpose.Production, Week, Week, 16m, [new(Week, 16m)], [new("Task", task.G("id"), Week)], null))).Json();
        var url = $"/api/v1/workload?from={Week:yyyy-MM-dd}";
        await Flag(false); var off = await (await f.As(TestData.Admin).GetAsync(url)).Json();
        Assert.Equal(off.ToJsonString(), (await (await f.As(TestData.Admin).GetAsync(url + "&showProposedAsConfirmed=true")).Json()).ToJsonString());
        var offExport = await f.As(TestData.Admin).GetByteArrayAsync($"/api/v1/workload/export?format=csv&from={Week:yyyy-MM-dd}");
        Assert.Equal(offExport, await f.As(TestData.Admin).GetByteArrayAsync($"/api/v1/workload/export?format=csv&from={Week:yyyy-MM-dd}&showProposedAsConfirmed=true"));
        await Flag(true); var normal = await (await f.As(TestData.Admin).GetAsync(url)).Json();
        var legacyShape = normal.DeepClone(); legacyShape.AsObject().Remove("disciplineContext");
        Assert.Equal(off.ToJsonString(), legacyShape.ToJsonString());
        var beforeActivity = f.Db(db => db.ActivityLog.Count()); var version = f.Db(db => db.Allocations.Single(a => a.Id == allocation.G("id")).RowVersion);
        var scenario = await (await f.As(TestData.Admin).GetAsync(url + "&showProposedAsConfirmed=true")).Json();
        Assert.Equal(8m, LoadPerson(normal, person)["cells"]![0]!["committed"]!.GetValue<decimal>());
        var cell = LoadPerson(scenario, person)["cells"]![0]!;
        Assert.Equal(16m, cell["committed"]!.GetValue<decimal>()); // max(16 reserved, 8 linked), not 16 + 8 forecast
        Assert.Equal(16m, cell["confirmed"]!.GetValue<decimal>());
        Assert.True(cell["includesProposed"]!.GetValue<bool>()); Assert.True(scenario["includesProposed"]!.GetValue<bool>());
        Assert.Null(LoadPerson(scenario, person)["cells"]![1]!["includesProposed"]);
        Assert.Equal(beforeActivity, f.Db(db => db.ActivityLog.Count()));
        var stored = f.Db(db => db.Allocations.Where(a => a.Id == allocation.G("id")).Select(a => new { a.Status, a.RowVersion }).Single());
        Assert.Equal((AllocationStatus.Proposed, version), (stored.Status, stored.RowVersion));
        Assert.Equal(normal.ToJsonString(), (await (await f.As(TestData.Admin).GetAsync(url)).Json()).ToJsonString());
        using var export = new ZipArchive(new MemoryStream(await f.As(TestData.Admin).GetByteArrayAsync($"/api/v1/workload/export?format=xlsx&from={Week:yyyy-MM-dd}&showProposedAsConfirmed=true")));
        using var parameters = new StreamReader(export.GetEntry("xl/worksheets/sheet2.xml")!.Open());
        Assert.Contains("Includes proposed", await parameters.ReadToEndAsync());
    }
}
