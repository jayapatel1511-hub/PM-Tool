using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 019: personal and project saved views (FR-001..FR-003, §18.4) and manual board order (FR-004, §13.4).
[Collection("api")]
public sealed class ViewsTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Views(string as_, Guid projectId, string list = "tasks") => f.As(as_).GetAsync($"/api/v1/views?listType={list}&projectId={projectId}").Result.Json();

    [Fact]
    public async Task Personal_views_restore_their_definition_and_one_is_default() // FR-001, FR-003, US1
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var alex = f.As(TestData.Alex);
        var ps = new Dictionary<string, string> { ["disciplineId"] = civil.ToString(), ["overdue"] = "true", ["group"] = "assignee", ["sort"] = "dueDate:desc", ["cols"] = "status,dueDate", ["panel"] = "Task:x" };
        var first = await alex.Post("/api/v1/views", new { name = "Civil overdue", listType = "tasks", projectId = p.Id, @params = ps, isDefault = true }).Result.Json(201);
        var second = await alex.Post("/api/v1/views", new { name = "Mine this week", listType = "tasks", projectId = p.Id, @params = new Dictionary<string, string> { ["mine"] = "true", ["dueThisWeek"] = "true" } }).Result.Json(201);

        var list = (await Views(TestData.Alex, p.Id))["views"]!.AsArray();
        var civilView = list.Single(v => v!.S("name") == "Civil overdue")!;
        Assert.Equal(civil.ToString(), civilView["params"]!.S("disciplineId"));
        Assert.Equal(("true", "assignee", "dueDate:desc", "status,dueDate"), (civilView["params"]!.S("overdue"), civilView["params"]!.S("group"), civilView["params"]!.S("sort"), civilView["params"]!.S("cols")));
        Assert.Null(civilView["params"]!["panel"]); // not part of a list's definition
        Assert.True(civilView["isDefault"]!.GetValue<bool>());
        Assert.Empty((await Views(TestData.Jill, p.Id))["views"]!.AsArray()); // personal means personal

        (await alex.Patch($"/api/v1/views/{second.S("id")}", new { isDefault = true }, second.I("rowVersion"))).EnsureSuccessStatusCode();
        var after = (await Views(TestData.Alex, p.Id))["views"]!.AsArray();
        Assert.Equal(new[] { "Mine this week" }, after.Where(v => v!["isDefault"]!.GetValue<bool>()).Select(v => v!.S("name")));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).DeleteAsync($"/api/v1/views/{first.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/views/{first.S("id")}")).StatusCode);
    }

    [Fact]
    public async Task Project_views_are_shared_and_changed_only_by_the_pm_and_leads() // FR-002, US2
    {
        var p = await d.Project();
        var shared = await f.As(TestData.Marc).Post("/api/v1/views", new { name = "Blocked by discipline", listType = "board", projectId = p.Id, scope = "Project",
            @params = new Dictionary<string, string> { ["blocked"] = "true", ["swim"] = "discipline" } }).Result.Json(201);
        var seen = (await Views(TestData.Alex, p.Id, "board"))["views"]!.AsArray().Single()!;
        Assert.Equal(("Blocked by discipline", "Project", "Marc Dubois", false), (seen.S("name"), seen.S("scope"), seen.S("owner"), seen["canEdit"]!.GetValue<bool>()));
        Assert.False((await Views(TestData.Alex, p.Id, "board"))["canShare"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/views/{shared.S("id")}", new { name = "Mine now" }, shared.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Team view", listType = "board", projectId = p.Id, scope = "Project" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).DeleteAsync($"/api/v1/views/{shared.S("id")}")).StatusCode);
        (await f.As(TestData.Pm).Patch($"/api/v1/views/{shared.S("id")}", new { name = "Blocked, by discipline" }, shared.I("rowVersion"))).EnsureSuccessStatusCode();
        var stale = await f.As(TestData.Marc).Patch($"/api/v1/views/{shared.S("id")}", new { name = "Lost update" }, shared.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); // G-07: someone changed it first
    }

    [Fact]
    public async Task A_view_whose_references_are_gone_opens_without_them() // spec edge case, FR-003
    {
        var p = await d.Project();
        var del = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Temporary", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        var v = await f.As(TestData.Alex).Post("/api/v1/views", new { name = "That deliverable", listType = "tasks", projectId = p.Id,
            @params = new Dictionary<string, string> { ["deliverableId"] = del.S("id"), ["status"] = "In Progress" } }).Result.Json(201);
        var dv = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.RowVersion).FirstAsync());
        (await f.As(TestData.Pm).SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/deliverables/{del.S("id")}") { Headers = { { "If-Match", $"\"{dv}\"" } } })).EnsureSuccessStatusCode();
        await f.DbAsync(async db => { var row = await db.SavedViews.FirstAsync(x => x.Id == v.G("id")); row.Filters = row.Filters.Replace("{", "{\"retired\":\"x\","); return await db.SaveChangesAsync(); });
        var opened = (await Views(TestData.Alex, p.Id))["views"]!.AsArray().Single()!;
        Assert.Equal(new[] { "deliverableId", "retired" }, opened["dropped"]!.AsArray().Select(x => x!.GetValue<string>()).Order());
        Assert.Equal("In Progress", opened["params"]!.S("status"));
    }

    [Fact]
    public async Task Coordination_views_restore_filters_without_copying_rows_or_crossing_project_scope()
    {
        var p = await d.Project();
        var other = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var filters = new Dictionary<string, string> { ["discipline"] = civil.ToString(), ["owner"] = U(TestData.Alex).ToString(),
            ["from"] = "2026-09-28", ["to"] = "2026-10-05", ["meeting"] = "1", ["panel"] = "Handoff:old" };
        (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Civil coordination", listType = "coordination", projectId = p.Id,
            @params = filters, isDefault = true })).EnsureSuccessStatusCode();
        var opened = (await Views(TestData.Alex, p.Id, "coordination"))["views"]!.AsArray().Single()!;
        Assert.Equal(civil.ToString(), opened["params"]!.S("discipline"));
        Assert.Equal(U(TestData.Alex).ToString(), opened["params"]!.S("owner"));
        Assert.Equal("2026-09-28", opened["params"]!.S("from"));
        Assert.Null(opened["params"]!["meeting"]);
        Assert.Null(opened["params"]!["panel"]);
        Assert.Empty((await Views(TestData.Alex, other.Id, "coordination"))["views"]!.AsArray());

        (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "My coordination", listType = "workspace-coordination",
            @params = new Dictionary<string, string> { ["tab"] = "coordination", ["projects"] = p.Id.ToString(), ["projectId"] = p.Id.ToString(),
                ["disciplineId"] = (await d.Discipline("Civil")).ToString(), ["ownerId"] = U(TestData.Alex).ToString(), ["from"] = "2026-09-28" } })).EnsureSuccessStatusCode();
        var workspace = (await f.As(TestData.Alex).GetAsync("/api/v1/views?listType=workspace-coordination").Result.Json())["views"]!.AsArray().Single()!;
        Assert.Equal("coordination", workspace["params"]!.S("tab"));
        Assert.Equal(p.Id.ToString(), workspace["params"]!.S("projects"));
        Assert.Equal(p.Id.ToString(), workspace["params"]!.S("projectId"));
        Assert.Equal((await d.Discipline("Civil")).ToString(), workspace["params"]!.S("disciplineId"));
        Assert.Empty((await f.As(TestData.Jill).GetAsync("/api/v1/views?listType=workspace-coordination").Result.Json())["views"]!.AsArray());
    }

    [Theory]
    [InlineData("submissions")]
    [InlineData("allocations")]
    [InlineData("design-basis")]
    [InlineData("readiness")]
    [InlineData("issues")]
    public async Task Coordination_views_keep_supported_filters_and_do_not_share_personal_or_hidden_project_data(string kind)
    {
        var project = await d.Project();
        var task = await d.NewTask(project.Id, TestData.Pm);
        var ps = kind switch {
            "submissions" => new Dictionary<string, string> { ["q"] = "Permit", ["status"] = "Ready", ["coordinatorId"] = U(TestData.Alex).ToString(), ["targetFrom"] = "2026-10-01" },
            "allocations" => new Dictionary<string, string> { ["q"] = "Production", ["personId"] = U(TestData.Alex).ToString(), ["purpose"] = "Production", ["from"] = "2026-10-01" },
            "design-basis" => new Dictionary<string, string> { ["kind"] = "Assumption", ["discipline"] = d.ProjectDiscipline(project.Id, "Civil").ToString(), ["affectedWorkId"] = task.S("id") },
            "readiness" => new Dictionary<string, string> { ["from"] = "2026-10-01", ["to"] = "2026-10-14" },
            _ => new Dictionary<string, string> { ["location"] = "North", ["verification"] = "Open", ["document"] = "Drawing", ["alignment"] = "Road A", ["issueType"] = "Drawing", ["group"] = "location" },
        };
        var stored = new Dictionary<string, string>(ps) { ["panel"] = "Task:open", ["allocation"] = "open", ["basis"] = "open" };
        await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Scoped coordination", listType = kind, projectId = project.Id, @params = stored, isDefault = true }).Result.Json(201);
        var view = (await Views(TestData.Alex, project.Id, kind))["views"]!.AsArray().Single()!;
        foreach (var pair in ps) Assert.Equal(pair.Value, view["params"]!.S(pair.Key));
        Assert.Null(view["params"]!["panel"]); Assert.Null(view["params"]!["allocation"]); Assert.Null(view["params"]!["basis"]);
        Assert.Empty((await Views(TestData.Jill, project.Id, kind))["views"]!.AsArray());
        if (kind == "design-basis") {
            await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == task.G("id"))).DeletedAt = DateTimeOffset.UtcNow; return await db.SaveChangesAsync(); });
            var cleaned = (await Views(TestData.Alex, project.Id, kind))["views"]!.AsArray().Single()!;
            Assert.Null(cleaned["params"]!["affectedWorkId"]);
            Assert.Contains("affectedWorkId", cleaned["dropped"]!.AsArray().Select(x => x!.GetValue<string>()));
        }
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await f.As(TestData.Rita).GetAsync($"/api/v1/views?listType={kind}&projectId={project.Id}").Result.Json(404);
    }

    [Fact]
    public async Task Manual_board_order_is_shared_by_the_team() // FR-004, US3, SC-002
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm, new { name = "Alpha", dueDate = "2026-10-01" });
        var b = await d.NewTask(p.Id, TestData.Pm, new { name = "Bravo", dueDate = "2026-10-02" });
        var c = await d.NewTask(p.Id, TestData.Pm, new { name = "Charlie", dueDate = "2026-10-03" });
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/board-order", new { taskIds = new[] { c.G("id"), a.G("id"), b.G("id") } })).StatusCode);
        var marc = await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{p.Id}/tasks?sort=board").Result.Json();
        Assert.Equal(new[] { "Charlie", "Alpha", "Bravo" }, marc["items"]!.AsArray().Select(x => x!.S("name")));
        (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/board-order", new { taskIds = new[] { a.G("id"), c.G("id") } })).EnsureSuccessStatusCode();
        var again = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks?sort=board").Result.Json();
        Assert.Equal(new[] { "Alpha", "Charlie", "Bravo" }, again["items"]!.AsArray().Select(x => x!.S("name")));

        var viewer = await f.As(TestData.Admin).Post($"/api/v1/projects/{p.Id}/members", new { userId = U(TestData.Diane), roles = new[] { "Viewer" } });
        Assert.True(viewer.IsSuccessStatusCode || viewer.StatusCode == HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Diane).Put($"/api/v1/projects/{p.Id}/board-order", new { taskIds = new[] { b.G("id") } })).StatusCode);
        var other = await d.NewTask((await d.Project()).Id, TestData.Pm);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/board-order", new { taskIds = new[] { other.G("id") } })).StatusCode);
    }
}
