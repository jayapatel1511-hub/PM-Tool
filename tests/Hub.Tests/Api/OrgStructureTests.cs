using System.Net;
using System.Text.Json.Nodes;

namespace Hub.Tests.Api;

public sealed class OrgStructureTests : IDisposable
{
    readonly HubFactory f = new();
    public void Dispose() => f.Dispose();

    [Fact]
    [Trait("flag-off", "org_structure")]
    public async Task Structure_is_dark_when_flag_is_off()
    {
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/organisation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/organisation/levels")).StatusCode);
    }

    [Fact]
    [Trait("flag-on", "org_structure")]
    public async Task Structure_is_admin_only_and_refuses_cycles()
    {
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/admin/organisation/levels")).StatusCode);
            var levels = (await admin.GetAsync("/api/v1/admin/organisation/levels").Result.Json()).AsArray();
            var region = levels.First(l => l!.S("name") == "Region")!;
            var team = levels.First(l => l!.S("name") == "Team")!;
            var root = await admin.Post("/api/v1/admin/organisation/units", new { name = "Atlantic", levelId = region.G("id") }).Result.Json(201);
            // Names are unique under one parent, the top level included, so a group or filter keyed by name never merges two units.
            Assert.Equal(System.Net.HttpStatusCode.Conflict, (await admin.Post("/api/v1/admin/organisation/units", new { name = "Atlantic", levelId = region.G("id") })).StatusCode);
            var child = await admin.Post("/api/v1/admin/organisation/units", new { name = "Civil", levelId = team.G("id"), parentId = root.G("id") }).Result.Json(201);
            var refused = await admin.Patch($"/api/v1/admin/organisation/units/{root.G("id")}", new { parentId = child.G("id"), levelId = region.G("id") }, root.I("rowVersion"));
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = false })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Import_preview_rejects_manager_cycle_and_unknown_manager_without_mutation()
    {
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            var before = (await admin.GetAsync("/api/v1/admin/organisation/units").Result.Json()).AsArray().Count;
            var csv = "unit_path,level,leader_email,discipline,office,email,manager_email\n" +
                      "Cycle,Region,,, ,alex@hub.test,marc@hub.test\n" +
                      "Cycle,Region,,, ,marc@hub.test,alex@hub.test\n" +
                      "Unknown,Region,,, ,alex@hub.test,missing@hub.test";
            var preview = await admin.Post("/api/v1/admin/organisation/import/preview", new { csv });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, preview.StatusCode);
            var after = (await admin.GetAsync("/api/v1/admin/organisation/units").Result.Json()).AsArray().Count;
            Assert.Equal(before, after);
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = false })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Corrected_import_applies_all_rows_atomically()
    {
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var level = await admin.Post("/api/v1/admin/organisation/levels", new { name = "ImportLevel-" + suffix }).Result.Json(201);
            var csv = "unit_path,level,leader_email,discipline,office,email,manager_email\n" +
                      $"Imported-{suffix},Region,,,,,\nImported-{suffix}/{suffix},ImportLevel-{suffix},alex@hub.test,,,,";
            var response = await admin.Post("/api/v1/admin/organisation/import/apply", new { csv });
            response.EnsureSuccessStatusCode();
            var units = (await admin.GetAsync("/api/v1/admin/organisation/units").Result.Json()).AsArray();
            Assert.Contains(units, x => x?["name"]?.GetValue<string>() == suffix);
            Assert.NotNull(level["id"]);
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = false })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Unit_leader_without_supervisor_role_can_read_subtree_but_cannot_write()
    {
        var admin = f.As(TestData.Admin); var d = new TestData(f);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var level = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray().First(l => l!.S("name") == "Team")!;
        var unit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Civil unit", levelId = level.G("id"), leaderId = d.User(TestData.Jill) })).Json(201);
        var alex = d.User(TestData.Alex); var version = f.Db(db => db.Users.First(u => u.Id == alex).RowVersion);
        await (await admin.Patch($"/api/v1/admin/organisation/users/{alex}/placement", new { unitId = unit.G("id") }, version)).Json(204);
        var project = await d.Project(); var task = await d.NewTask(project.Id, extra: new { assigneeId = alex, name = "Visible unit work" });
        await (await admin.Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var restricted = await d.Project();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{restricted.Id}", new { visibility = "Restricted" }, d.Version(restricted.Id))).Json();
        await d.NewTask(restricted.Id, extra: new { assigneeId = alex, name = "Restricted unit secret" });
        var viewer = f.As(TestData.Jill);
        var me = await (await viewer.GetAsync("/api/v1/me")).Json(); Assert.True(me["capabilities"]!["staff"]!.GetValue<bool>());
        var staff = await (await viewer.GetAsync($"/api/v1/staff?scope=unit:{unit.G("id")}")).Json();
        Assert.Contains(staff["people"]!.AsArray(), p => p!.G("id") == alex);
        var work = await (await viewer.GetAsync($"/api/v1/me/work?userId={alex}")).Json(); Assert.DoesNotContain("Restricted unit secret", work.ToJsonString());
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.Patch($"/api/v1/tasks/{task.G("id")}", new { assigneeId = d.User(TestData.Jill) }, await d.TaskVersion(task))).StatusCode);
        // The Planner admits a unit leader (FR-UNT-11), so its saved views do too.
        (await viewer.GetAsync("/api/v1/views?listType=planner")).EnsureSuccessStatusCode();
        await (await viewer.Post("/api/v1/views", new { name = "Unit planner", listType = "planner", @params = new Dictionary<string, string>() })).Json(201);
    }

    [Fact]
    public async Task Closed_parent_refuses_new_or_imported_open_children()
    {
        var admin = f.As(TestData.Admin);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var region = levels.First(l => l!.S("name") == "Region")!; var team = levels.First(l => l!.S("name") == "Team")!;
        var parent = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Closed parent", levelId = region.G("id") })).Json(201);
        await (await admin.Action($"/api/v1/admin/organisation/units/{parent.G("id")}/close", new { }, parent.I("rowVersion"))).Json(204);
        await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Invalid child", levelId = team.G("id"), parentId = parent.G("id") })).Json(400);
        var csv = "unit_path,level\nClosed parent / Invalid child,Team";
        await (await admin.Post("/api/v1/admin/organisation/import/preview", new { csv })).Json(422);
    }

    async Task<Func<string, Guid>> Levels(HttpClient admin)
    {
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        return name => levels.First(l => l!.S("name") == name)!.G("id");
    }

    static async Task<Guid> Unit(HttpClient admin, string name, Guid levelId, Guid? parentId = null, Guid? leaderId = null) =>
        (await (await admin.Post("/api/v1/admin/organisation/units", new { name, levelId, parentId, leaderId })).Json(201)).G("id");

    async Task Place(HttpClient admin, Guid person, Guid unit) => await (await admin.Patch($"/api/v1/admin/organisation/users/{person}/placement",
        new { unitId = unit }, f.Db(db => db.Users.First(u => u.Id == person).RowVersion))).Json(204);

    async Task SetSupervisor(HttpClient admin, Guid person, Guid supervisor, int expect = 200) => await (await admin.Patch($"/api/v1/admin/users/{person}",
        new { supervisorId = supervisor }, f.Db(db => db.Users.First(u => u.Id == person).RowVersion))).Json(expect);

    async Task<HashSet<Guid>> StaffIds(string viewer, string query = "") =>
        (await (await f.As(viewer).GetAsync($"/api/v1/staff{query}")).Json())["people"]!.AsArray().Select(p => p!.G("id")).ToHashSet();

    [Fact] // AC-UNT-01, FR-UNT-15
    public async Task Three_level_path_shows_leader_and_head_count_of_one_at_every_level()
    {
        var admin = f.As(TestData.Admin); var d = new TestData(f);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var level = await Levels(admin);
        var (lena, sam, marc, alex) = (d.User(TestData.Lena), d.User(TestData.Sam), d.User(TestData.Marc), d.User(TestData.Alex));
        var atlantic = await Unit(admin, "Atlantic", level("Region"), leaderId: lena);
        var civil = await Unit(admin, "Civil", level("Business unit"), atlantic, sam);
        var team = await Unit(admin, "Civil Halifax Team", level("Team"), civil, marc);
        await Place(admin, alex, team);
        // Names, levels, leaders and head counts are visible to every signed-in user; the person list follows people scope.
        foreach (var viewer in new[] { TestData.Admin, TestData.Diane })
        {
            var org = await (await f.As(viewer).GetAsync("/api/v1/organisation")).Json();
            var units = org["units"]!.AsArray().ToDictionary(u => u!.G("id"), u => u!);
            Assert.Equal(3, units.Count);
            foreach (var (id, parent, levelName, leader) in new[] { (atlantic, (Guid?)null, "Region", "Lena Brooks"), (civil, atlantic, "Business unit", "Sam Patel"),
                (team, civil, "Team", "Marc Dubois") })
            {
                var unit = units[id];
                Assert.Equal(parent, unit["parentId"]?.GetValue<Guid?>());
                Assert.Equal(levelName, unit.S("level"));
                Assert.Equal(leader, unit.S("leader"));
                Assert.Equal(1, unit.I("headCount"));
            }
            var people = org["people"]!.AsArray();
            if (viewer == TestData.Admin) Assert.Equal(team, people.Single(p => p!.G("id") == alex)!.G("unitId"));
            else Assert.DoesNotContain(people, p => p!.G("id") == alex);
        }
    }

    [Fact] // AC-UNT-05, FR-UNT-06, FR-UNT-08, FR-UNT-12
    public async Task Indirect_reports_follow_the_setting_and_a_reporting_cycle_grants_no_indirect_scope()
    {
        var admin = f.As(TestData.Admin); var d = new TestData(f);
        async Task<(Guid Id, string Email)> Person(string name)
        {
            var email = $"{name}-{Guid.NewGuid():N}@hub.test";
            return ((await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id"), email);
        }
        var (alex, jill, marc) = (d.User(TestData.Alex), d.User(TestData.Jill), d.User(TestData.Marc));
        var nia = (await Person("nia")).Id;
        await SetSupervisor(admin, nia, marc);
        // A reporting cycle from data that predates the amendment: with the flag off, manager links save as before.
        var (quinn, quinnEmail) = await Person("quinn");
        var (q1, q2) = ((await Person("q1")).Id, (await Person("q2")).Id);
        await (await admin.Post($"/api/v1/admin/users/{quinn}/roles", new { role = "Supervisor" })).Json(204);
        await SetSupervisor(admin, q1, quinn); await SetSupervisor(admin, q2, q1); await SetSupervisor(admin, quinn, q2);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();

        // include_indirect_reports off (050-D4 default): Sam sees only direct reports and "All my reports" is refused.
        var org = await (await f.As(TestData.Sam).GetAsync("/api/v1/organisation")).Json();
        Assert.False(org["includeIndirectReports"]!.GetValue<bool>());
        Assert.Null(org["managerCycles"]);
        Assert.Equal([alex, jill, marc], await StaffIds(TestData.Sam));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync("/api/v1/staff?scope=reports")).StatusCode);

        await (await admin.Put("/api/v1/admin/settings/include_indirect_reports", new { value = true })).Json();
        Assert.True((await (await f.As(TestData.Sam).GetAsync("/api/v1/organisation")).Json())["includeIndirectReports"]!.GetValue<bool>());
        Assert.Equal([alex, jill, marc], await StaffIds(TestData.Sam));
        Assert.Equal([alex, jill, marc, nia], await StaffIds(TestData.Sam, "?scope=reports"));
        // The cycle grants no indirect scope: Quinn keeps the direct report only. Admins see everyone it affects.
        Assert.Equal([q1], await StaffIds(quinnEmail, "?scope=reports"));
        var cycles = (await (await admin.GetAsync("/api/v1/organisation")).Json())["managerCycles"]!.AsArray().Select(p => p!.G("id")).ToHashSet();
        Assert.Equal([quinn, q1, q2], cycles);
        Assert.Null((await (await f.As(TestData.Sam).GetAsync("/api/v1/organisation")).Json())["managerCycles"]);
        // With the flag on, a new cycle is refused and nothing changes.
        await SetSupervisor(admin, marc, nia, 400);
        Assert.Equal(d.User(TestData.Sam), f.Db(db => db.Users.First(u => u.Id == marc).SupervisorId));

        await (await admin.Put("/api/v1/admin/settings/include_indirect_reports", new { value = false })).Json();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync("/api/v1/staff?scope=reports")).StatusCode);
        Assert.Equal([alex, jill, marc], await StaffIds(TestData.Sam));
    }

    [Fact] // AC-UNT-06, FR-UNT-13
    public async Task Workload_filtered_to_a_unit_lists_its_subtree_once_and_home_unit_subtotals_add_to_its_total()
    {
        var admin = f.As(TestData.Admin); var d = new TestData(f);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var level = await Levels(admin);
        var atlantic = await Unit(admin, "Atlantic", level("Region"));
        var civil = await Unit(admin, "Civil", level("Business unit"), atlantic);
        var team = await Unit(admin, "Civil Halifax Team", level("Team"), civil);
        var structural = await Unit(admin, "Structural", level("Business unit"), atlantic);
        var structuralTeam = await Unit(admin, "Halifax Team", level("Team"), structural);
        var civilTeam = await Unit(admin, "Halifax Team", level("Team"), civil); // same name, another branch
        var pacific = await Unit(admin, "Pacific", level("Region"));
        var homes = new Dictionary<string, Guid> { [TestData.Alex] = team, [TestData.Jill] = civil, [TestData.Marc] = structuralTeam,
            [TestData.Sam] = atlantic, [TestData.Pm] = civilTeam, [TestData.Diane] = pacific };
        foreach (var (email, unit) in homes) await Place(admin, d.User(email), unit);
        var project = await d.Project();
        var hours = new Dictionary<string, decimal> { [TestData.Alex] = 16m, [TestData.Jill] = 8m, [TestData.Marc] = 12m, [TestData.Sam] = 6m,
            [TestData.Pm] = 5m, [TestData.Diane] = 10m, [TestData.Omar] = 7m };
        foreach (var (email, h) in hours)
            await d.NewTask(project.Id, extra: new { assigneeId = d.User(email), estimatedHours = h, startDate = "2026-09-14", dueDate = "2026-09-25" });

        var grid = await (await admin.GetAsync($"/api/v1/workload?unitId={atlantic}")).Json();
        var people = grid["people"]!.AsArray();
        var expected = homes.Where(h => h.Value != pacific).Select(h => d.User(h.Key)).ToHashSet();
        Assert.Equal(expected.Count, people.Count); // each person exactly once
        Assert.Equal(expected, people.Select(p => p!.G("id")).ToHashSet());
        var placements = (await (await admin.GetAsync($"/api/v1/organisation/placements?ids={string.Join(',', expected)}")).Json()).AsArray()
            .ToDictionary(p => p!.G("id"), p => p!.G("unitId"));
        Assert.All(people, p => Assert.Equal(homes.Single(h => d.User(h.Key) == p!.G("id")).Value, placements[p!.G("id")]));
        static decimal Committed(JsonNode p, int week) => p["cells"]![week]!["committed"]!.GetValue<decimal>();
        var weeks = grid["weeks"]!.AsArray().Count;
        var groups = people.GroupBy(p => placements[p!.G("id")]).ToList();
        Assert.Equal(5, groups.Count);
        for (var w = 0; w < weeks; w++)
            Assert.Equal(people.Sum(p => Committed(p!, w)), groups.Sum(g => g.Sum(p => Committed(p!, w))));
        Assert.Equal(16m + 8m + 12m + 6m + 5m, people.Sum(p => Enumerable.Range(0, weeks).Sum(w => Committed(p!, w))));
        // The filter selects people; it never changes their numbers.
        var unfiltered = (await (await admin.GetAsync("/api/v1/workload")).Json())["people"]!.AsArray().ToDictionary(p => p!.G("id"), p => p!["cells"]!.ToJsonString());
        Assert.All(people, p => Assert.Equal(unfiltered[p!.G("id")], p!["cells"]!.ToJsonString()));
        var csv = await (await admin.GetAsync($"/api/v1/workload/export?unitId={atlantic}&format=csv")).Content.ReadAsStringAsync();
        foreach (var email in hours.Keys)
        {
            var name = f.Db(db => db.Users.First(u => u.Email == email).DisplayName);
            Assert.Equal(expected.Contains(d.User(email)) ? 1 : 0, csv.Split('\n').Count(line => line.TrimStart('﻿', '"').StartsWith(name + (line.TrimStart('﻿').StartsWith('"') ? "\"," : ","), StringComparison.Ordinal)));
        }
    }

    [Fact]
    public async Task Unit_filter_is_shared_by_screen_and_export_reads()
    {
        var admin = f.As(TestData.Admin); var d = new TestData(f);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var region = levels.First(l => l!.S("name") == "Region")!;
        var unit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Filtered unit", levelId = region.G("id") })).Json(201);
        var alex = d.User(TestData.Alex); var alexVersion = f.Db(db => db.Users.First(u => u.Id == alex).RowVersion);
        await (await admin.Patch($"/api/v1/admin/organisation/users/{alex}/placement", new { unitId = unit.G("id") }, alexVersion)).Json(204);
        foreach (var path in new[] { "workload", "planning/grid", "planning/availability?from=2026-09-14&through=2026-09-18" })
        {
            var separator = path.Contains('?') ? "&" : "?";
            var result = await (await admin.GetAsync($"/api/v1/{path}{separator}unitId={unit.G("id")}")).Json();
            Assert.Single(result["people"]!.AsArray()); Assert.Equal(alex, result["people"]!.AsArray()[0]!.G("id"));
        }
        var diane = d.User(TestData.Diane); var excluded = f.Db(db => db.Users.First(u => u.Id == diane).DisplayName);
        foreach (var path in new[] { "workload/export", "planning/grid/export", "planning/availability/export?from=2026-09-14&through=2026-09-18" })
        {
            var separator = path.Contains('?') ? "&" : "?";
            var response = await admin.GetAsync($"/api/v1/{path}{separator}unitId={unit.G("id")}&format=csv"); response.EnsureSuccessStatusCode();
            Assert.DoesNotContain(excluded, await response.Content.ReadAsStringAsync());
        }
    }
}
