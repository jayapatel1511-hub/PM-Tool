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
