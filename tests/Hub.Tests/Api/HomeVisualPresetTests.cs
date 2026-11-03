using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// S08/AC-SCR-14: explicit per-person choices and exact current-role destination reads.
public sealed class HomeVisualPresetTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeVisualPresetTests() => d = new(f);
    public void Dispose() => f.Dispose();
    Task Set(string key, object value) => f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value }).Result.Json();
    async Task<JsonNode> Get(string who, string url) => await (await f.As(who).GetAsync(url)).Json();
    Task<JsonNode> Home(string who, string query) => Get(who, "/api/v1/home?" + query);
    static JsonArray Items(JsonNode node) => node["items"]!.AsArray();

    [Fact]
    public async Task Held_roles_only_explicit_choice_saved_per_person_no_implicit_get_and_role_loss_refuses_instead_of_widening()
    {
        await Set("flag.home_overview", true);
        var project = await d.Project(); var other = await d.Project(pm: TestData.Marc);
        var priya = d.User(TestData.Pm); var marc = d.User(TestData.Marc);
        var civil = d.ProjectDiscipline(project.Id, "Civil");
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(p => p.Id == civil)).LeadUserId = priya; return await db.SaveChangesAsync(); });
        var query = $"projects={project.Id},{other.Id}";
        var before = await f.DbAsync(db => db.ActivityLog.CountAsync());
        var initial = await Home(TestData.Pm, query);
        Assert.Equal(new[] { "ProjectManager", "DisciplineLead" }, initial["preset"]!["available"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Null(initial["preset"]!["code"]); Assert.Null(initial["preset"]!["savedPreset"]);
        Assert.Equal(2, initial["metrics"]!.I("activeProjects"));
        Assert.Equal(before, await f.DbAsync(db => db.ActivityLog.CountAsync()));
        await (await f.As(TestData.Pm).Put("/api/v1/me/home-preset", new { code = "ProjectManager" })).Json(204);
        var chosen = await Home(TestData.Pm, query + "&homePreset=ProjectManager");
        Assert.Equal(1, chosen["preset"]!.I("projects")); Assert.Equal(1, chosen["metrics"]!.I("activeProjects"));
        Assert.Equal("ProjectManager", chosen["preset"]!.S("savedPreset"));
        var revisit = await Home(TestData.Pm, query);
        Assert.Null(revisit["preset"]!["code"]); Assert.Equal(2, revisit["metrics"]!.I("activeProjects")); // client restoration is explicit URL state
        Assert.Null((await Home(TestData.Marc, query))["preset"]!["savedPreset"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Put("/api/v1/me/home-preset", new { code = "Supervisor" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync("/api/v1/home?projects=all&homePreset=Invented")).StatusCode);
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == project.Id)).ProjectManagerId = marc;
            foreach (var member in await db.ProjectMembers.Where(m => m.UserId == priya && m.RemovedAt == null).ToListAsync()) member.RemovedAt = f.Clock.Now;
            (await db.ProjectDisciplines.SingleAsync(p => p.Id == civil)).LeadUserId = marc;
            return await db.SaveChangesAsync();
        });
        (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/users/{priya}/roles/{SystemRole.ProjectManager}")).EnsureSuccessStatusCode();
        Assert.Null((await Home(TestData.Pm, query))["preset"]!["savedPreset"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync("/api/v1/home?projects=all&homePreset=ProjectManager")).StatusCode);
        await (await f.As(TestData.Pm).Put("/api/v1/me/home-preset", new { code = (string?)null })).Json(204);
        Assert.Null(await f.DbAsync(db => db.UserSettings.Where(s => s.UserId == priya).Select(s => s.HomePreset).SingleAsync()));
    }

    [Fact]
    public async Task Discipline_scope_reconciles_task_charts_undated_due_types_runway_and_exact_destination_filters()
    {
        f.Clock.SetDate(2026, 10, 31); await Set("flag.home_overview", true);
        var project = await d.Project(); var other = await d.Project();
        var civil = d.ProjectDiscipline(project.Id, "Civil"); var electrical = d.ProjectDiscipline(project.Id, "Electrical");
        var type = await d.DeliverableType(); var due = new DateOnly(2026, 10, 31);
        await d.NewTask(project.Id, extra: new { dueDate = "2026-10-30" });
        await d.NewTask(project.Id, extra: new { dueDate = "2026-10-31" }); await d.NewTask(project.Id);
        await d.NewTask(project.Id, TestData.Omar, new { dueDate = "2026-10-31" }, "Electrical");
        await f.DbAsync(async db =>
        {
            // Marc no longer leads the other project; its membership alone cannot retain it in this preset.
            foreach (var pd in await db.ProjectDisciplines.Where(p => p.ProjectId == other.Id).ToListAsync()) pd.LeadUserId = d.User(TestData.Omar);
            db.Deliverables.AddRange(new[] { civil, electrical }.Select((pd, i) => new Deliverable { ProjectId = project.Id, ProjectDisciplineId = pd,
                DeliverableTypeId = type, Seq = i + 1, Key = $"{project.ProjectNumber}-D{i + 1:0000}", Name = "Synthetic due deliverable", DueDate = due }));
            db.Milestones.AddRange(new Guid?[] { civil, electrical, null }.Select((pd, i) => new Milestone { ProjectId = project.Id, ProjectDisciplineId = pd,
                Seq = i + 1, Key = $"{project.ProjectNumber}-M{i + 1:0000}", Name = "Synthetic due milestone", Date = due }));
            return await db.SaveChangesAsync();
        });
        var query = $"projects={project.Id},{other.Id}&homePreset=DisciplineLead&projectStatus=Active&from=2026-10-31&to=2026-10-31";
        await f.Evaluate(project.Id);
        var body = await Home(TestData.Marc, query); var metrics = body["metrics"]!; var context = body["countContext"]!;
        Assert.Equal(1, context.I("projectsInScope")); Assert.Equal(3, metrics.I("openTasks")); Assert.Equal(1, metrics.I("overdueTasks"));
        Assert.Equal(1, context.I("openTasksWithoutDueDate"));
        Assert.Equal(1, context.I("dueWithinTasks")); Assert.Equal(1, context.I("dueWithinDeliverables")); Assert.Equal(1, context.I("dueWithinMilestones"));
        Assert.Equal(3, body["upcomingDeadlines"]!.I("total")); Assert.Equal(3, body["runway"]!["days"]![0]!.I("total"));
        Assert.Equal(3, Assert.Single(body["tasksByProject"]!.AsArray())!.I("total"));
        Assert.Equal(metrics.I("openTasks"), body["tasksByStatus"]!.AsArray().Where(l => l!.S("lane") != "Done").Sum(l => l!.I("count")) + body["onHold"]!.I("count"));
        Assert.True(body["preset"]!["unownedMilestonesExcluded"]!.GetValue<bool>()); Assert.Equal(1, body["preset"]!.I("disciplines"));
        Assert.All(body["trends"]!["metrics"]!.AsArray(), m => { Assert.Equal("FilteredScope", m!.S("reason")); Assert.Null(m["delta"]); });
        var destination = $"/api/v1/tasks?projects={project.Id},{other.Id}&homePreset=DisciplineLead&homeProjectStatus=Active";
        foreach (var (filter, expected) in new[] { ("open=true", metrics.I("openTasks")), ("overdue=true", metrics.I("overdueTasks")), ("open=true&withoutDueDate=true", context.I("openTasksWithoutDueDate")), ("dueFrom=2026-10-31&dueTo=2026-10-31&open=true", context.I("dueWithinTasks")) })
            Assert.Equal(expected, (await Get(TestData.Marc, destination + "&" + filter)).I("totalCount"));
        var team = await Get(TestData.Marc, $"/api/v1/team?projects={project.Id},{other.Id}&homePreset=DisciplineLead&homeProjectStatus=Active");
        Assert.Equal(metrics.I("teamMembers"), team["people"]!.AsArray().Count); Assert.Equal(1, team.I("projects"));
    }

    [Fact]
    public async Task Supervisor_direct_reports_then_section54_indirect_and_led_unit_scope_keep_milestones_excluded()
    {
        await Set("flag.home_overview", true); f.Clock.SetDate(2026, 10, 31);
        var project = await d.Project(); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill); var marc = d.User(TestData.Marc); var omar = d.User(TestData.Omar);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == jill)).SupervisorId = marc; return await db.SaveChangesAsync(); });
        foreach (var person in new[] { alex, jill, omar }) await d.NewTask(project.Id, extra: new { assigneeId = person, dueDate = "2026-10-31" });
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/milestones", new { name = "Synthetic unowned milestone", milestoneType = MilestoneType.ClientWorkshop, date = "2026-10-31" })).Json(201);
        var query = $"projects={project.Id}&homePreset=Supervisor&from=2026-10-31&to=2026-10-31";
        var direct = await Home(TestData.Sam, query);
        Assert.Equal(1, direct["metrics"]!.I("openTasks")); Assert.Equal(1, direct["countContext"]!.I("dueWithinTasks")); Assert.Equal(0, direct["countContext"]!.I("dueWithinMilestones"));
        Assert.Equal(1, direct["upcomingDeadlines"]!.I("total")); Assert.True(direct["preset"]!["unownedMilestonesExcluded"]!.GetValue<bool>());
        Assert.All(direct["trends"]!["metrics"]!.AsArray(), m => Assert.Equal("FilteredScope", m!.S("reason")));
        // An executive's off-state preset is still direct reports (Omar), not all organisation members.
        Assert.Equal(1, (await Home(TestData.Lena, query))["metrics"]!.I("openTasks"));
        await Set("flag.org_structure", true); await Set("include_indirect_reports", true);
        var level = (await Get(TestData.Admin, "/api/v1/admin/organisation/levels")).AsArray().First(l => l!.S("name") == "Team")!;
        var unit = await (await f.As(TestData.Admin).Post("/api/v1/admin/organisation/units", new { name = "Synthetic Home led unit", levelId = level.G("id"), leaderId = d.User(TestData.Sam) })).Json(201);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == omar)).UnitId = unit.G("id"); return await db.SaveChangesAsync(); });
        var widened = await Home(TestData.Sam, query);
        Assert.Equal(3, widened["metrics"]!.I("openTasks")); Assert.Equal(3, widened["upcomingDeadlines"]!.I("total"));
        Assert.Equal(3, (await Get(TestData.Sam, $"/api/v1/tasks?projects={project.Id}&homePreset=Supervisor&open=true")).I("totalCount"));
        Assert.Equal(3, (await Home(TestData.Lena, query))["metrics"]!.I("openTasks"));
        Assert.All(widened["upcomingDeadlines"]!["items"]!.AsArray(), item => Assert.Equal("Task", item!.S("type")));
    }

    [Fact]
    public async Task Scalar_preset_covers_more_than_fifty_projects_without_clipping_counts_or_links()
    {
        await Set("flag.home_overview", true);
        var seed = await d.Project(); var priya = d.User(TestData.Pm); var discipline = await d.Discipline("Civil");
        await d.NewTask(seed.Id);
        await f.DbAsync(async db =>
        {
            for (var i = 0; i < 52; i++)
            {
                var p = new Project { ProjectNumber = $"P-S08-{i:000}", Name = "Synthetic large cohort", ClientId = seed.ClientId, OfficeId = seed.OfficeId, ProjectManagerId = priya, Status = ProjectStatus.Active };
                var pd = new ProjectDiscipline { ProjectId = p.Id, DisciplineId = discipline };
                db.Projects.Add(p); db.ProjectDisciplines.Add(pd);
                db.Tasks.Add(new WorkTask { ProjectId = p.Id, ProjectDisciplineId = pd.Id, Seq = 1, Key = $"{p.ProjectNumber}-T0001", Name = "Synthetic cohort task" });
            }
            return await db.SaveChangesAsync();
        });
        var home = await Home(TestData.Pm, "projects=all&homePreset=ProjectManager");
        Assert.Equal(53, home["metrics"]!.I("activeProjects")); Assert.Equal(53, home["metrics"]!.I("openTasks"));
        Assert.Equal(53, home["tasksByProject"]!.AsArray().Count); Assert.Equal(53, home["preset"]!.I("projects"));
        Assert.Equal(53, (await Get(TestData.Pm, "/api/v1/tasks?projects=all&homePreset=ProjectManager&open=true&pageSize=100")).I("totalCount"));
        var projects = await Get(TestData.Pm, "/api/v1/projects?mine=false&status=Active&homePreset=ProjectManager&homeProjects=all&pageSize=100");
        Assert.Equal(53, projects.I("totalCount")); Assert.Equal(53, Items(projects).Count);
    }

    [Fact]
    public async Task Mine_status_and_access_remain_part_of_project_and_team_destinations_then_dark_queries_are_ignored()
    {
        await Set("flag.home_overview", true); await Set("restricted_projects_enabled", true);
        var mine = await d.Project(); var nonmember = await d.Project(); var hidden = await d.Project(); var held = await d.Project();
        var sam = d.User(TestData.Sam); var alex = d.User(TestData.Alex);
        await f.DbAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = mine.Id, UserId = sam, Roles = [ProjectRole.Viewer], AddedAt = f.Clock.Now });
            (await db.Projects.SingleAsync(p => p.Id == held.Id)).Status = ProjectStatus.OnHold;
            db.ProjectMembers.Add(new ProjectMember { ProjectId = held.Id, UserId = sam, Roles = [ProjectRole.Viewer], AddedAt = f.Clock.Now });
            (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        foreach (var p in new[] { mine, nonmember, hidden, held }) await d.NewTask(p.Id, extra: new { assigneeId = alex });
        var home = await Home(TestData.Sam, "projects=mine&homePreset=Supervisor&projectStatus=Active");
        Assert.Equal(1, home["metrics"]!.I("activeProjects")); Assert.Equal(1, home["metrics"]!.I("openTasks"));
        var projects = await Get(TestData.Sam, "/api/v1/projects?mine=false&status=Active&homePreset=Supervisor&homeProjects=mine");
        Assert.Equal(mine.Id, Assert.Single(Items(projects))!.G("id"));
        Assert.Equal(2, (await Get(TestData.Sam, "/api/v1/projects?mine=false&status=Active&homePreset=Supervisor&homeProjects=all")).I("totalCount")); // catches lost base-scope transport
        var team = await Get(TestData.Sam, "/api/v1/team?projects=mine&homePreset=Supervisor&homeProjectStatus=Active");
        Assert.Equal(1, team.I("projects")); Assert.Equal(home["metrics"]!.I("teamMembers"), team["people"]!.AsArray().Count);
        Assert.Equal(1, (await Get(TestData.Sam, "/api/v1/tasks?projects=mine&homePreset=Supervisor&homeProjectStatus=Active&open=true")).I("totalCount"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync("/api/v1/team?homeProjectStatus=Invented")).StatusCode);
        var permitted = await Home(TestData.Sam, "projects=all&homePreset=Supervisor");
        Assert.DoesNotContain(hidden.Id.ToString(), permitted.ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, permitted.ToJsonString());
        await Set("flag.home_overview", false);
        var off = await Home(TestData.Sam, "projects=mine&homePreset=Invented&projectStatus=Invented"); Assert.Null(off["preset"]); Assert.Null(off["trends"]);
        var plainTeam = await Get(TestData.Sam, "/api/v1/team?projects=mine");
        Assert.Equal(plainTeam.ToJsonString(), (await Get(TestData.Sam, "/api/v1/team?projects=mine&homePreset=Invented&homeProjectStatus=Invented")).ToJsonString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).Put("/api/v1/me/home-preset", new { code = "Supervisor" })).StatusCode);
    }
}
