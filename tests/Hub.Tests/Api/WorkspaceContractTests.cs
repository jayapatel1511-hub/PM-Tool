using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The workspace, home, layout, files and team routes answer with typed records that must serialise to the bytes the app already reads
/// (040-S15-Workspace). Its own host and database, so a person's saved dashboard layout and the shared host's data cannot reach these reads.
public sealed class WorkspaceContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData d = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static string[] Texts(JsonNode? n) => [.. n!.AsArray().Select(x => x!.GetValue<string>())];
    static readonly string[] RowKeys = ["id", "name", "projectIds", "hidden", "rowVersion"];
    static readonly string[] LaneKeys = ["lane", "statuses", "count"];
    static readonly string[] Lanes = ["To Do", "In Progress", "Review", "Done"];
    static readonly string[] DefaultLayout = ["metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines"];

    async Task<JsonNode> Get(string as_, string url) => await f.As(as_).GetAsync($"/api/v1/{url}").Result.Json();

    [Fact]
    public async Task Workspace_commands_and_list_keep_their_answers_and_shape()
    {
        var (a, b) = (await d.Project(), await d.Project());
        var (pm, alex) = (f.As(TestData.Pm), f.As(TestData.Alex));
        var name = "Contract " + Guid.NewGuid().ToString("N")[..6];

        // Creating: 201 with a Location and the row, every property written, in this order.
        var made = await pm.Post("/api/v1/workspaces", new { name, projectIds = new[] { b.Id, a.Id } });
        var ws = await made.Json(201);
        Assert.Equal(RowKeys, Keys(ws));
        Assert.Equal($"/api/v1/workspaces/{ws.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal((name, 0), (ws.S("name"), ws.I("hidden")));
        Assert.Equal(new[] { b.Id, a.Id }, ws["projectIds"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())));
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/workspaces", new { name, projectIds = new[] { a.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/workspaces", new { name = "", projectIds = new[] { a.Id } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/workspaces", new { name = "No projects", projectIds = Array.Empty<Guid>() })).StatusCode);

        // The list: the same row for each workspace, in name order, for its owner only.
        var list = (await Get(TestData.Pm, "workspaces")).AsArray();
        Assert.All(list, w => Assert.Equal(RowKeys, Keys(w)));
        Assert.Equal(ws.ToJsonString(), list.Single(w => w!.G("id") == ws.G("id"))!.ToJsonString());
        Assert.Empty((await Get(TestData.Alex, "workspaces")).AsArray().Where(w => w!.G("id") == ws.G("id")));

        // Editing: 200 with the same row on the new version; the order follows the ids given.
        var edited = await pm.Patch($"/api/v1/workspaces/{ws.S("id")}", new { name = name + " renamed", projectIds = new[] { a.Id, b.Id } }, ws.I("rowVersion")).Result.Json();
        Assert.Equal(RowKeys, Keys(edited));
        Assert.Equal(name + " renamed", edited.S("name"));
        Assert.Equal(new[] { a.Id, b.Id }, edited["projectIds"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())));
        Assert.True(edited.I("rowVersion") > ws.I("rowVersion"));
        var renamed = await pm.Patch($"/api/v1/workspaces/{ws.S("id")}", new { name = name + " again" }, edited.I("rowVersion")).Result.Json(); // the name alone keeps the projects
        Assert.Equal((name + " again", 2), (renamed.S("name"), renamed["projectIds"]!.AsArray().Count));
        Assert.Equal("concurrency_conflict", (await pm.Patch($"/api/v1/workspaces/{ws.S("id")}", new { name = "Stale" }, ws.I("rowVersion")).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.NotFound, (await alex.Patch($"/api/v1/workspaces/{ws.S("id")}", new { name = "Mine" }, renamed.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch($"/api/v1/workspaces/{ws.S("id")}", new { projectIds = Array.Empty<Guid>() }, renamed.I("rowVersion"))).StatusCode);

        // The manual card order: 204 and no body; an empty list, a task outside the workspace and someone else's workspace are refused as before.
        var task = await d.NewTask(a.Id, TestData.Pm, new { name = "Ordered", dueDate = "2026-09-20" });
        var order = await pm.Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = new[] { task.G("id") } });
        Assert.Equal(HttpStatusCode.NoContent, order.StatusCode);
        Assert.Empty(await order.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = Array.Empty<Guid>() })).StatusCode);
        var other = await d.Project(pm: TestData.Marc);
        var outside = await d.NewTask(other.Id, TestData.Marc);
        Assert.Equal(Hub.Api.Text.Get("workspace.task_outside"), Assert.Single((await pm.Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = new[] { outside.G("id") } }).Result.Json(400))["errors"]!["taskIds"]!.AsArray())!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await alex.Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = new[] { task.G("id") } })).StatusCode);

        // Deleting: 428 without a version, 409 on a stale one, then 204 and no body; the card order goes with it.
        Assert.Equal(428, (int)(await pm.DeleteAsync($"/api/v1/workspaces/{ws.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Delete($"/api/v1/workspaces/{ws.S("id")}", ws.I("rowVersion"))).StatusCode);
        var gone = await pm.Delete($"/api/v1/workspaces/{ws.S("id")}", renamed.I("rowVersion"));
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode);
        Assert.Empty(await gone.Content.ReadAsStringAsync());
        Assert.Equal(0, await f.DbAsync(db => db.BoardOrders.CountAsync(o => o.WorkspaceId == ws.G("id"))));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Delete($"/api/v1/workspaces/{ws.S("id")}", renamed.I("rowVersion"))).StatusCode);
    }

    [Fact]
    public async Task Home_keeps_its_property_names_order_nulls_and_deadline_kinds()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        await d.NewTask(p.Id, TestData.Pm, new { name = "Soon", dueDate = "2026-09-18" });
        var started = await d.NewTask(p.Id, TestData.Pm, new { name = "Going", dueDate = "2026-09-12" }); // overdue
        await d.Move(TestData.Pm, started, "In Progress");
        var held = await d.NewTask(p.Id, TestData.Pm, new { name = "Parked", dueDate = "2026-09-19" });
        await d.Move(TestData.Pm, held, "On Hold", new { reason = "Waiting on survey" });
        await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Drawings", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-20" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Pin", milestoneType = "Design Submission", date = "2026-09-22" }).Result.Json(201);
        var setup = await d.Project(activate: false);

        // Before the project is evaluated, nobody is On Track and the pct is written as null, not left out.
        var raw = await Get(TestData.Pm, $"home?projects={setup.Id}");
        Assert.Equal(["green", "evaluated", "pct"], Keys(raw["metrics"]!["onTrack"]));
        Assert.True(raw["metrics"]!["onTrack"]!.AsObject().ContainsKey("pct")); Assert.Null(raw["metrics"]!["onTrack"]!["pct"]);
        Assert.Empty(raw["tasksByProject"]!.AsArray());
        Assert.Empty(raw["upcomingDeadlines"]!["items"]!.AsArray());

        await f.Evaluate(p.Id);
        var h = await Get(TestData.Pm, $"home?projects={p.Id}");
        Assert.Equal(["today", "from", "to", "asOf", "metrics", "tasksByStatus", "onHold", "tasksByProject", "upcomingDeadlines", "layout", "isDefault"], Keys(h));
        Assert.Equal(("2026-09-14", "2026-09-14", "2026-09-27"), (h.S("today"), h.S("from"), h.S("to")));
        Assert.Equal("2026-09-14T13:00:00+00:00", h.S("asOf"));
        var m = h["metrics"]!;
        Assert.Equal(["activeProjects", "openTasks", "overdueTasks", "onTrack", "teamMembers"], Keys(m));
        Assert.Equal((1, 3, 1), (m.I("activeProjects"), m.I("openTasks"), m.I("overdueTasks")));
        Assert.Equal(["green", "evaluated", "pct"], Keys(m["onTrack"]));
        Assert.Equal(1, m["onTrack"]!.I("evaluated"));

        // The four lanes and the On Hold row each write the lane, the statuses it counts and the count.
        var lanes = h["tasksByStatus"]!.AsArray();
        Assert.Equal(Lanes, lanes.Select(l => l!.S("lane")));
        Assert.All(lanes, l => Assert.Equal(LaneKeys, Keys(l)));
        Assert.Equal(string.Join(',', TaskStatuses.All.Where(x => Workflow.Lane(x) == "To Do")), lanes[0]!.S("statuses"));
        Assert.Equal([1, 1, 0, 0], lanes.Select(l => l!.I("count")));
        Assert.Equal(["status", "count"], Keys(h["onHold"]));
        Assert.Equal((TaskStatuses.OnHold, 1), (h["onHold"]!.S("status"), h["onHold"]!.I("count")));

        var byProject = Assert.Single(h["tasksByProject"]!.AsArray())!;
        Assert.Equal(["projectId", "projectNumber", "name", "lanes", "total"], Keys(byProject));
        Assert.Equal((p.Id, p.ProjectNumber, p.Name, 2), (byProject.G("projectId"), byProject.S("projectNumber"), byProject.S("name"), byProject.I("total"))); // On Hold is not in a lane
        Assert.Equal(Lanes, byProject["lanes"]!.AsArray().Select(l => l!.S("lane")));
        Assert.All(byProject["lanes"]!.AsArray(), l => Assert.Equal(LaneKeys, Keys(l)));

        // The deadlines: the total, whether they were cut, and each item with its owner and status written even when null.
        var dl = h["upcomingDeadlines"]!;
        Assert.Equal(["total", "truncated", "items"], Keys(dl));
        Assert.Equal(false, dl["truncated"]!.GetValue<bool>());
        var items = dl["items"]!.AsArray();
        Assert.Equal((4, 4), (dl.I("total"), items.Count));
        Assert.Equal(["Task", "Task", "Deliverable", "Milestone"], items.Select(x => x!.S("type")));
        Assert.All(items, x => Assert.Equal(["type", "id", "projectId", "projectNumber", "key", "name", "date", "status", "owner", "overdue"], Keys(x)));
        Assert.Equal(["Soon", "Parked", "Drawings", "Pin"], items.Select(x => x!.S("name")));
        Assert.Equal((p.Id, p.ProjectNumber, "2026-09-18", false), (items[0]!.G("projectId"), items[0]!.S("projectNumber"), items[0]!.S("date"), items[0]!["overdue"]!.GetValue<bool>()));
        Assert.Equal("Not Started", items[0]!.S("status"));
        Assert.True(items[3]!.AsObject().ContainsKey("owner")); Assert.Null(items[3]!["owner"]); // a milestone has no owner
        Assert.NotNull(items[2]!["owner"]); // a deliverable's owner is named
        var wide = await Get(TestData.Pm, $"home?projects={p.Id}&from=2026-09-01&to=2026-09-30");
        Assert.Equal("Going", wide["upcomingDeadlines"]!["items"]![0]!.S("name"));
        Assert.True(wide["upcomingDeadlines"]!["items"]![0]!["overdue"]!.GetValue<bool>());

        // The saved layout is the default until someone changes it, and the refusal for a bad range stays a 400.
        Assert.Equal(DefaultLayout, h["layout"]!.AsArray().Select(w => w!.S("id")));
        Assert.All(h["layout"]!.AsArray(), w => { Assert.Equal(["id", "hidden"], Keys(w)); Assert.False(w!["hidden"]!.GetValue<bool>()); });
        Assert.True(h["isDefault"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync($"/api/v1/home?projects={p.Id}&from=2026-09-20&to=2026-09-01")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync($"/api/v1/home?projects={p.Id}&from=2026-09-01&to=2027-12-01")).StatusCode);
    }

    [Fact]
    public async Task Dashboard_layout_commands_answer_the_layout_and_whether_it_is_the_default()
    {
        var jill = f.As(TestData.Jill);
        var saved = await jill.Put("/api/v1/me/dashboard-layout", new { widgets = new object[] { new { id = "upcomingDeadlines", hidden = true }, new { id = "nope", hidden = false } } });
        var s = await saved.Json();
        Assert.Equal(["layout", "isDefault"], Keys(s));
        Assert.False(s["isDefault"]!.GetValue<bool>());
        Assert.Equal(["upcomingDeadlines", "metrics", "tasksByStatus", "tasksByProject"], s["layout"]!.AsArray().Select(w => w!.S("id"))); // unknown dropped, missing appended visible
        Assert.Equal([true, false, false, false], s["layout"]!.AsArray().Select(w => w!["hidden"]!.GetValue<bool>()));
        Assert.All(s["layout"]!.AsArray(), w => Assert.Equal(["id", "hidden"], Keys(w)));
        Assert.Equal(s["layout"]!.ToJsonString(), (await Get(TestData.Jill, "home"))["layout"]!.ToJsonString());

        var empty = await jill.Put("/api/v1/me/dashboard-layout", new { widgets = Array.Empty<object>() }).Result.Json();
        Assert.Equal(DefaultLayout, empty["layout"]!.AsArray().Select(w => w!.S("id")));
        Assert.False(empty["isDefault"]!.GetValue<bool>()); // saved, even though it equals the default

        var restored = await jill.DeleteAsync("/api/v1/me/dashboard-layout");
        var r = await restored.Json();
        Assert.Equal(["layout", "isDefault"], Keys(r));
        Assert.True(r["isDefault"]!.GetValue<bool>());
        Assert.Equal(DefaultLayout, r["layout"]!.AsArray().Select(w => w!.S("id")));
        Assert.True((await Get(TestData.Jill, "home"))["isDefault"]!.GetValue<bool>());
        Assert.True((await jill.DeleteAsync("/api/v1/me/dashboard-layout")).IsSuccessStatusCode); // with nothing saved it still answers the default
    }

    [Fact]
    public async Task Files_and_team_keep_their_property_names_order_and_nulls()
    {
        var p = await d.Project();
        var empty = await d.Project();
        var (pm, alex) = (f.As(TestData.Pm), f.As(TestData.Alex));
        var del = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Bridge drawings", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = "Check levels" });
        await pm.Post($"/api/v1/items/Project/{p.Id}/links", new { url = "https://contoso.sharepoint.com/sites/p/Shared%20Documents", title = "Project site", linkType = "SharePoint" }).Result.Json(201);
        await pm.Post($"/api/v1/items/Deliverable/{del.S("id")}/links", new { url = @"\\fs01\projects\bridge", title = "Bridge folder" }).Result.Json(201);
        await alex.Post($"/api/v1/items/Task/{task.S("id")}/links", new { url = "https://portal.example.com/levels", title = "Levels" }).Result.Json(201);
        var scope = $"{p.Id},{empty.Id}";

        // Files: the projects with links (or that the reader may add to), each item's links, the total and the link types.
        var files = await Get(TestData.Pm, $"files?projects={scope}");
        Assert.Equal(["projects", "total", "truncated", "types"], Keys(files));
        Assert.Equal((3, false), (files.I("total"), files["truncated"]!.GetValue<bool>()));
        Assert.Equal(LinkType.All, Texts(files["types"]));
        var projects = files["projects"]!.AsArray();
        Assert.Equal(new[] { p.ProjectNumber, empty.ProjectNumber }.Order(), projects.Select(x => x!.S("projectNumber")));
        Assert.All(projects, x => Assert.Equal(["projectId", "projectNumber", "name", "canAdd", "count", "items"], Keys(x)));
        var mine = projects.Single(x => x!.G("projectId") == p.Id)!;
        Assert.Equal((p.Name, true, 3), (mine.S("name"), mine["canAdd"]!.GetValue<bool>(), mine.I("count")));
        var nothing = projects.Single(x => x!.G("projectId") == empty.Id)!;
        Assert.Equal((0, 0), (nothing.I("count"), nothing["items"]!.AsArray().Count)); // the PM may add a link, so the project is listed
        var items = mine["items"]!.AsArray();
        Assert.Equal(["Project", "Deliverable", "Task"], items.Select(i => i!.S("itemType")));
        Assert.All(items, i => Assert.Equal(["itemType", "itemId", "key", "name", "links"], Keys(i)));
        Assert.Equal((p.Id, p.ProjectNumber, p.Name), (items[0]!.G("itemId"), items[0]!.S("key"), items[0]!.S("name"))); // a project's own links use its number and name
        Assert.Equal((del.G("id"), del.S("key"), "Bridge drawings"), (items[1]!.G("itemId"), items[1]!.S("key"), items[1]!.S("name")));
        Assert.Equal((task.G("id"), task.S("key"), "Check levels"), (items[2]!.G("itemId"), items[2]!.S("key"), items[2]!.S("name")));
        var link = items[1]!["links"]!.AsArray().Single()!;
        Assert.Equal(["id", "title", "url", "linkType", "isNetworkPath", "addedBy", "addedAt"], Keys(link));
        Assert.Equal(("Bridge folder", @"\\fs01\projects\bridge", true, "Priya Nair"), (link.S("title"), link.S("url"), link["isNetworkPath"]!.GetValue<bool>(), link.S("addedBy")));
        Assert.Equal("Network Folder", link.S("linkType")); // a network path is typed as one when none is given
        Assert.False(items[0]!["links"]![0]!["isNetworkPath"]!.GetValue<bool>());
        Assert.Equal("SharePoint", items[0]!["links"]![0]!.S("linkType"));
        Assert.True(DateTimeOffset.TryParse(link.S("addedAt"), out _));
        var filtered = await Get(TestData.Alex, $"files?projects={scope}&q=levels");
        Assert.Equal((1, false), (filtered.I("total"), Assert.Single(filtered["projects"]!.AsArray())!["canAdd"]!.GetValue<bool>())); // Alex may not add: only the project with a link is listed
        Assert.Equal(["Levels"], filtered["projects"]![0]!["items"]!.AsArray().SelectMany(i => i!["links"]!.AsArray()).Select(l => l!.S("title")));
        var none = await Get(TestData.Alex, $"files?projects={scope}&q=zzzz");
        Assert.Equal(["projects", "total", "truncated", "types"], Keys(none));
        Assert.Equal((0, 0), (none.I("total"), none["projects"]!.AsArray().Count));
        Assert.Equal(LinkType.All, Texts(none["types"]));

        // Team: each person once with a row per project, roles and the disciplines they lead; job title and email written even when empty.
        var team = await Get(TestData.Alex, $"team?projects={scope}");
        Assert.Equal(["people", "projects"], Keys(team));
        Assert.Equal(2, team.I("projects"));
        var people = team["people"]!.AsArray();
        Assert.Equal(people.Select(x => x!.S("displayName")).Order(StringComparer.Ordinal), people.Select(x => x!.S("displayName")));
        Assert.All(people, x => Assert.Equal(["userId", "displayName", "jobTitle", "email", "projects"], Keys(x)));
        var marc = people.Single(x => x!.S("displayName") == "Marc Dubois")!;
        Assert.Equal(TestData.Marc, marc.S("email"));
        Assert.Equal(d.User(TestData.Marc), marc.G("userId"));
        Assert.Equal(["projectId", "projectNumber", "projectName", "roles", "leads"], Keys(marc["projects"]![0]));
        Assert.Equal(["Civil"], Texts(marc["projects"]![0]!["leads"]));
        Assert.Equal(2, marc["projects"]!.AsArray().Count);
        Assert.Equal(new[] { p.ProjectNumber, empty.ProjectNumber }.Order(), marc["projects"]!.AsArray().Select(x => x!.S("projectNumber")));
        var alexRow = people.Single(x => x!.S("displayName") == "Alex Chen")!["projects"]!.AsArray();
        Assert.Contains("TeamMember", alexRow.SelectMany(x => Texts(x!["roles"])));
        Assert.Empty(alexRow[0]!["leads"]!.AsArray());
        var lone = await Get(TestData.Alex, $"team?projects={Guid.NewGuid()}");
        Assert.Equal(["people", "projects"], Keys(lone));
        Assert.Equal((0, 0), (lone["people"]!.AsArray().Count, lone.I("projects")));
    }
}
