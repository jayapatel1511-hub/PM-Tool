using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S42: only the existing authorised Team population gains organisation labels; roles and planner scope stay separate.
public sealed class TeamCompletenessTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Week = new(2026, 10, 26);
    public TeamCompletenessTests() { d = new(f); f.Clock.SetDate(2026, 10, 31); }
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    async Task User(Guid id, object patch) => (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{id}", patch,
        f.Db(db => db.Users.Single(user => user.Id == id).RowVersion))).EnsureSuccessStatusCode();
    async Task<JsonNode> Project(Guid id, string actor = TestData.Pm, string query = "") => await (await f.As(actor).GetAsync($"/api/v1/projects/{id}/team{query}")).Json();
    async Task<JsonNode> Workspace(Guid id, string actor = TestData.Pm, string query = "") => await (await f.As(actor).GetAsync($"/api/v1/team?projects={id}{query}")).Json();
    static JsonNode Person(JsonNode read, string collection, Guid id) => read[collection]!.AsArray().Single(row => row!.G("userId") == id)!;
    static string Legacy(JsonNode read, string collection)
    {
        var copy = read.DeepClone(); foreach (var row in copy[collection]!.AsArray()) { row!.AsObject().Remove("organisationFacts"); row.AsObject().Remove("responsibility"); } return copy.ToJsonString();
    }
    Task<string> State() => f.DbAsync(async db => JsonSerializer.Serialize(new {
        Users = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.HomeDisciplineId, u.OfficeId, u.SupervisorId, u.UnitId, u.IsActive, u.WeeklyCapacityHours, u.RowVersion }).ToArrayAsync(),
        Members = await db.ProjectMembers.AsNoTracking().OrderBy(m => m.Id).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Notifications = await db.Notifications.CountAsync(), Dates = await db.PersonDateVersions.CountAsync()
    }));

    [Fact]
    public async Task Home_discipline_is_distinct_from_project_primary_and_shared_with_workspace_without_changing_legacy_cohort()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var office = await d.Office(); var electrical = await d.Discipline("Electrical");
        await User(alex, new { homeDisciplineId = electrical, officeId = office }); await Flag("people_screens", false); await Flag("capacity_screens", false);
        var oldProject = await Project(p.Id); var oldWorkspace = await Workspace(p.Id);
        await Flag("people_screens"); var before = await State(); var project = await Project(p.Id); var workspace = await Workspace(p.Id);
        Assert.Equal(oldProject.ToJsonString(), Legacy(project, "members")); Assert.Equal(oldWorkspace.ToJsonString(), Legacy(workspace, "people"));
        var member = Person(project, "members", alex); var facts = member["organisationFacts"]!;
        Assert.Equal(d.ProjectDiscipline(p.Id, "Civil"), member.G("primaryDisciplineId")); Assert.Equal(electrical, facts["homeDiscipline"]!.G("id"));
        Assert.Equal("Electrical", facts["homeDiscipline"]!.S("name")); Assert.Equal(office, facts["office"]!.G("id"));
        Assert.Equal(d.User(TestData.Sam), facts["supervisor"]!.G("id"));
        Assert.Equal(facts.ToJsonString(), Person(workspace, "people", alex)["organisationFacts"]!.ToJsonString());
        Assert.Equal(before, await State());
        await Flag("people_screens", false);
        Assert.Equal(oldProject.ToJsonString(), (await Project(p.Id)).ToJsonString()); Assert.Equal(oldWorkspace.ToJsonString(), (await Workspace(p.Id)).ToJsonString());
    }

    [Fact]
    public async Task Unknown_references_stay_null_and_workspace_deduplicates_members_across_selected_projects()
    {
        var p = await d.Project(); var q = await d.Project(); var alex = d.User(TestData.Alex);
        await User(alex, new { homeDisciplineId = (Guid?)null, officeId = (Guid?)null, supervisorId = (Guid?)null }); await Flag("people_screens");
        var before = await State(); var read = await (await f.As(TestData.Pm).GetAsync($"/api/v1/team?projects={p.Id},{q.Id}")).Json();
        var person = Person(read, "people", alex); var facts = person["organisationFacts"]!;
        Assert.Null(facts["homeDiscipline"]); Assert.Null(facts["office"]); Assert.Null(facts["supervisor"]); Assert.False(facts.AsObject().ContainsKey("unit"));
        Assert.Equal(2, person["projects"]!.AsArray().Count); Assert.Equal(2, read.I("projects"));
        Assert.Equal(read["people"]!.AsArray().Count, read["people"]!.AsArray().Select(row => row!.G("userId")).Distinct().Count());
        Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Unit_uses_current_placement_and_is_omitted_with_org_flag_off_without_widening_Team()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); await Flag("people_screens"); await Flag("org_structure");
        var admin = f.As(TestData.Admin); var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var level = levels.First(row => row!.S("name") == "Team")!.G("id");
        var a = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic first unit", levelId = level })).Json(201);
        var b = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic current unit", levelId = level })).Json(201);
        async Task Place(Guid unit) => await (await admin.Patch($"/api/v1/admin/organisation/users/{alex}/placement", new { unitId = unit },
            f.Db(db => db.Users.Single(user => user.Id == alex).RowVersion))).Json(204);
        await Place(a.G("id")); Assert.Equal(a.G("id"), Person(await Project(p.Id), "members", alex)["organisationFacts"]!["unit"]!.G("id"));
        await Place(b.G("id")); var before = await State();
        foreach (var (read, collection) in new[] { (await Project(p.Id), "members"), (await Workspace(p.Id), "people") }) {
            Assert.Equal(b.G("id"), Person(read, collection, alex)["organisationFacts"]!["unit"]!.G("id"));
            Assert.DoesNotContain(read[collection]!.AsArray(), row => row!.G("userId") == d.User(TestData.Diane));
        }
        Assert.Equal(before, await State()); await Flag("org_structure", false);
        Assert.False(Person(await Project(p.Id), "members", alex)["organisationFacts"]!.AsObject().ContainsKey("unit"));
        Assert.False(Person(await Workspace(p.Id), "people", alex)["organisationFacts"]!.AsObject().ContainsKey("unit"));
    }

    [Fact]
    public async Task Certification_filter_and_lead_references_remain_independent_of_people_flag_and_hidden_scope()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); await Flag("people_screens"); await Flag("capacity_screens");
        var tag = await (await f.As(TestData.Admin).Post("/api/v1/admin/certificationTags", new { name = "Synthetic Team qualification" })).Json(201);
        var own = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/certifications")).Json();
        await (await f.As(TestData.Alex).Post("/api/v1/me/certifications", new { certificationTagId = tag.G("id"), expiresOn = (DateOnly?)null, rowVersion = own.I("rowVersion") })).Json(201);
        var before = await State(); var project = await Project(p.Id, query: $"?certificationTagId={tag.G("id")}");
        Assert.Equal(alex, Assert.Single(project["members"]!.AsArray())!.G("userId")); Assert.NotNull(Person(project, "members", alex)["organisationFacts"]);
        var marcName = f.Db(db => db.Users.Single(user => user.Id == d.User(TestData.Marc)).DisplayName);
        Assert.Contains(project["disciplines"]!.AsArray(), row => row!["leadName"]?.GetValue<string>() == marcName);
        Assert.Equal(alex, Assert.Single((await Workspace(p.Id, query: $"&certificationTagId={tag.G("id")}"))["people"]!.AsArray())!.G("userId"));
        Assert.Equal(before, await State()); await Flag("capacity_screens", false);
        Assert.True((await Project(p.Id, query: "?certificationTagId=ignored-while-off"))["members"]!.AsArray().Count > 1);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).Json();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Diane).GetAsync($"/api/v1/projects/{p.Id}/team")).StatusCode);
        Assert.Empty((await Workspace(p.Id, TestData.Diane))["people"]!.AsArray());
    }

    [Fact]
    public async Task Two_open_roles_are_separate_canonical_records_and_never_change_Team_people_or_membership()
    {
        var p = await d.Project(); await Flag("people_screens"); var prior = await Project(p.Id); var workspace = await Workspace(p.Id);
        foreach (var (label, hours) in new[] { ("Synthetic Civil role", 12), ("Synthetic Electrical role", 8) })
            await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = label, projectDisciplineId = d.ProjectDiscipline(p.Id, label.Contains("Civil") ? "Civil" : "Electrical"),
                fromDate = Week, throughDate = Week.AddDays(4), plannedHours = hours })).Json(201);
        var before = await State(); var roles = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/open-roles")).Json()).AsArray();
        Assert.Equal(2, roles.Count); Assert.All(roles, role => Assert.Equal("Open", role!.S("status")));
        Assert.Equal(prior.ToJsonString(), (await Project(p.Id)).ToJsonString()); Assert.Equal(workspace.ToJsonString(), (await Workspace(p.Id)).ToJsonString());
        Assert.DoesNotContain("Synthetic Civil role", (await Project(p.Id)).ToJsonString()); Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Picker_current_week_read_keeps_existing_planner_scope_free_and_away_and_refuses_unknown_people()
    {
        await d.Project(); await Flag("people_screens"); var alex = d.User(TestData.Alex); var outside = d.User(TestData.Diane);
        await User(alex, new { weeklyCapacityHours = 40 });
        await (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = alex, sourceSystem = "SyntheticTeam", externalId = "Synthetic approved day",
            fromDate = Week, throughDate = Week, hoursPerDay = 8, status = "Approved", rowVersion = 0 })).Json();
        var before = await State(); var read = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={alex},{outside}&from={Week:yyyy-MM-dd}&weeks=1")).Json();
        var person = Assert.Single(read["people"]!.AsArray())!; Assert.Equal(alex, person.G("id"));
        var cell = Assert.Single(person["cells"]!.AsArray())!; Assert.Equal(8m, cell["timeAway"]!.GetValue<decimal>());
        Assert.Equal(32m, cell["remaining"]!.GetValue<decimal>()); Assert.Equal(before, await State());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personId={outside}&from={Week:yyyy-MM-dd}&weeks=1")).StatusCode);
    }

    [Fact]
    public async Task Team_saved_views_require_owner_flag_and_keep_workspace_personal_and_project_access_sharing_guards()
    {
        var p = await d.Project(); var pm = f.As(TestData.Pm);
        foreach (var type in new[] { "workspace-team", "project-team" }) {
            var projectId = type == "project-team" ? (Guid?)p.Id : null;
            var query = projectId is { } id ? $"&projectId={id}" : "";
            Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/views?listType={type}{query}")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await pm.Post("/api/v1/views", new { name = "Dark Team view", listType = type, projectId })).StatusCode);
        }
        await Flag("people_screens"); await Flag("capacity_screens", false); await Flag("visual_system", false);
        var workspace = await (await pm.Post("/api/v1/views", new { name = "Synthetic personal Team", listType = "workspace-team", scope = "Personal",
            @params = new Dictionary<string, string> { ["projects"] = p.Id.ToString(), ["group"] = "homeDiscipline", ["officeId"] = "not-set", ["cols"] = "homeDiscipline,office" } })).Json(201);
        var workspaceRead = await (await pm.GetAsync("/api/v1/views?listType=workspace-team")).Json();
        Assert.Equal(workspace.G("id"), Assert.Single(workspaceRead["views"]!.AsArray())!.G("id")); Assert.False(workspaceRead["canShare"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync($"/api/v1/views?listType=workspace-team&projectId={p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/views", new { name = "Wrong workspace project", listType = "workspace-team", projectId = p.Id })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/views", new { name = "Wrong workspace sharing", listType = "workspace-team", scope = "Project" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync("/api/v1/views?listType=project-team")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/views", new { name = "Missing Team project", listType = "project-team" })).StatusCode);
        var shared = await (await pm.Post("/api/v1/views", new { name = "Synthetic shared Team", listType = "project-team", projectId = p.Id, scope = "Project",
            @params = new Dictionary<string, string> { ["group"] = "office", ["supervisorId"] = "unavailable" } })).Json(201);
        var alex = f.As(TestData.Alex); var seen = await (await alex.GetAsync($"/api/v1/views?listType=project-team&projectId={p.Id}")).Json();
        Assert.Equal(shared.G("id"), Assert.Single(seen["views"]!.AsArray())!.G("id")); Assert.False(seen["canShare"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.Post("/api/v1/views", new { name = "Refused shared Team", listType = "project-team", projectId = p.Id, scope = "Project" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.Patch($"/api/v1/views/{shared.G("id")}", new { name = "Refused Team edit" }, shared.I("rowVersion"))).StatusCode);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await (await pm.Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).Json();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Diane).GetAsync($"/api/v1/views?listType=project-team&projectId={p.Id}")).StatusCode);
        var count = f.Db(db => db.SavedViews.Count()); await Flag("people_screens", false);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync("/api/v1/views?listType=workspace-team")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/views/{workspace.G("id")}", new { name = "Still dark" }, workspace.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.DeleteAsync($"/api/v1/views/{shared.G("id")}")).StatusCode);
        Assert.Equal(count, f.Db(db => db.SavedViews.Count()));
    }

    [Fact]
    public async Task Team_saved_view_group_validation_and_reference_cleanup_keep_known_ids_and_distinct_unknown_sentinels()
    {
        var p = await d.Project(); await Flag("people_screens"); var pm = f.As(TestData.Pm);
        var before = f.Db(db => db.SavedViews.Count());
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/views", new { name = "Invalid Team group", listType = "workspace-team",
            @params = new Dictionary<string, string> { ["group"] = "invented-department" } })).StatusCode);
        Assert.Equal(before, f.Db(db => db.SavedViews.Count()));
        var discipline = await d.Discipline("Civil");
        var created = await (await pm.Post("/api/v1/views", new { name = "Synthetic Team reference cleanup", listType = "workspace-team",
            @params = new Dictionary<string, string> { ["group"] = "homeDiscipline", ["homeDisciplineId"] = discipline.ToString(), ["officeId"] = Guid.NewGuid().ToString(),
                ["supervisorId"] = "not-a-guid", ["unitId"] = "not-set", ["certificationTagId"] = Guid.NewGuid().ToString(), ["projects"] = p.Id.ToString(),
                ["cols"] = "homeDiscipline,office", ["unrecognisedTeamValue"] = "discard" } })).Json(201);
        var read = Assert.Single((await (await pm.GetAsync("/api/v1/views?listType=workspace-team")).Json())["views"]!.AsArray())!;
        Assert.Equal(discipline.ToString(), read["params"]!.S("homeDisciplineId")); Assert.Equal("not-set", read["params"]!.S("unitId"));
        Assert.Equal(p.Id.ToString(), read["params"]!.S("projects")); Assert.Equal("homeDiscipline,office", read["params"]!.S("cols"));
        Assert.Null(read["params"]!["unrecognisedTeamValue"]);
        Assert.Equal(new[] { "certificationTagId", "officeId", "supervisorId" }, read["dropped"]!.AsArray().Select(value => value!.GetValue<string>()).Order());
        await (await pm.Patch($"/api/v1/views/{created.G("id")}", new { @params = new Dictionary<string, string> {
            ["group"] = "unit", ["homeDisciplineId"] = "not-set", ["officeId"] = "unavailable", ["supervisorId"] = d.User(TestData.Sam).ToString(), ["unitId"] = Guid.Empty.ToString()
        } }, created.I("rowVersion"))).Json();
        var changed = Assert.Single((await (await pm.GetAsync("/api/v1/views?listType=workspace-team")).Json())["views"]!.AsArray())!;
        Assert.Equal("not-set", changed["params"]!.S("homeDisciplineId")); Assert.Equal("unavailable", changed["params"]!.S("officeId"));
        Assert.Equal(d.User(TestData.Sam).ToString(), changed["params"]!.S("supervisorId")); Assert.Equal("unit", changed["params"]!.S("group"));
        Assert.Equal("unitId", Assert.Single(changed["dropped"]!.AsArray())!.GetValue<string>());
        var viewVersion = changed.I("rowVersion"); var state = await State();
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch($"/api/v1/views/{created.G("id")}", new {
            @params = new Dictionary<string, string> { ["group"] = "private-ranking" }
        }, viewVersion)).StatusCode);
        Assert.Equal(state, await State());
        var unchanged = Assert.Single((await (await pm.GetAsync("/api/v1/views?listType=workspace-team")).Json())["views"]!.AsArray())!;
        Assert.Equal(changed.ToJsonString(), unchanged.ToJsonString());
    }
}
