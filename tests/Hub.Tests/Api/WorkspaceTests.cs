using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 022: the six-view workspace — named project scope (FR-VIS-02), the grouped Projects board (FR-VIS-03, AC-VIS-01),
/// the cross-project board (FR-VIS-04, AC-VIS-02), the cross-project Gantt (FR-VIS-05, AC-VIS-03), the Home overview
/// (FR-VIS-07, AC-VIS-05), My Work membership views (FR-VIS-08, AC-VIS-06), and Files and Team (FR-VIS-01, FR-VIS-09).
[Collection("api")]
public sealed class WorkspaceTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);
    static string Ids(params Project[] ps) => string.Join(",", ps.Select(p => p.Id));
    Task<JsonNode> Get(string as_, string url) => f.As(as_).GetAsync($"/api/v1/{url}").Result.Json();
    async Task<int> Total(string as_, string url) => (await Get(as_, url)).I("totalCount");

    async Task WithRestricted(Func<Task> work)
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try { await work(); }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }

    [Fact]
    public async Task A_named_workspace_keeps_its_scope_and_never_discloses_a_restricted_project() // US1, FR-VIS-02, AC-VIS-07
    {
        await WithRestricted(async () =>
        {
            var a = await d.Project();
            var b = await d.Project();
            var secret = await d.Project(pm: TestData.Marc, tweak: x => x["members"] = Array.Empty<object>());
            await d.NewTask(a.Id, TestData.Pm, new { name = "Pier footing", dueDate = "2026-09-20" });
            await d.NewTask(b.Id, TestData.Pm, new { name = "Ramp grading", dueDate = "2026-09-21" });
            await d.NewTask(secret.Id, TestData.Marc, new { name = "Confidential survey", dueDate = "2026-09-22" });
            await f.As(TestData.Pm).Post($"/api/v1/items/Task/{(await Get(TestData.Pm, $"tasks?projects={a.Id}"))["items"]![0]!.S("id")}/links", new { url = @"\\fs01\projects\pier", title = "Pier folder" }).Result.Json(201);
            (await f.As(TestData.Marc).Patch($"/api/v1/projects/{secret.Id}", new { visibility = Visibility.Restricted }, d.Version(secret.Id))).EnsureSuccessStatusCode();
            var alex = f.As(TestData.Alex);

            // Alex cannot put a project he cannot see into a workspace, and may name a set of the others.
            Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post("/api/v1/workspaces", new { name = "Atlantic Civil", projectIds = new[] { a.Id, secret.Id } })).StatusCode);
            var ws = await alex.Post("/api/v1/workspaces", new { name = "Atlantic Civil", projectIds = new[] { a.Id, b.Id } }).Result.Json(201);
            Assert.Equal(HttpStatusCode.BadRequest, (await alex.Post("/api/v1/workspaces", new { name = "Atlantic Civil", projectIds = new[] { a.Id } })).StatusCode); // names are unique per person
            Assert.Equal(new[] { a.Id, b.Id }, (await Get(TestData.Alex, "workspaces")).AsArray().Single(w => w!.S("name") == "Atlantic Civil")!["projectIds"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())));
            Assert.DoesNotContain((await Get(TestData.Jill, "workspaces")).AsArray(), w => w!.G("id") == ws.G("id")); // personal
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).Patch($"/api/v1/workspaces/{ws.S("id")}", new { name = "Mine" })).StatusCode);

            // Every view reads the same scope; a link naming the restricted project adds nothing for Alex (FR-VIS-02).
            var scope = Ids(a, b, secret);
            var tasks = (await Get(TestData.Alex, $"tasks?projects={scope}"))["items"]!.AsArray();
            Assert.Equal(new[] { a.ProjectNumber, b.ProjectNumber }.Order(), tasks.Select(t => t!.S("projectNumber")).Distinct().Order());
            var home = await Get(TestData.Alex, $"home?projects={scope}&from=2026-09-01&to=2026-09-30");
            Assert.Equal((2, 2), (home["metrics"]!.I("activeProjects"), home["metrics"]!.I("openTasks")));
            Assert.DoesNotContain(home["upcomingDeadlines"]!["items"]!.AsArray(), x => x!.S("name") == "Confidential survey");
            Assert.DoesNotContain(home["tasksByProject"]!.AsArray(), x => x!.G("projectId") == secret.Id);
            Assert.DoesNotContain((await Get(TestData.Alex, $"calendar?from=2026-09-14&to=2026-09-27&projectIds={scope}"))["entries"]!.AsArray(), e => e!.S("title") == "Confidential survey");
            Assert.Equal(new[] { a.Id, b.Id }.Order(), (await Get(TestData.Alex, $"timeline?projects={scope}"))["projects"]!.AsArray().Select(x => x!["project"]!.G("id")).Order());
            Assert.DoesNotContain((await Get(TestData.Alex, $"files?projects={scope}"))["projects"]!.AsArray(), x => x!.G("projectId") == secret.Id);
            Assert.DoesNotContain((await Get(TestData.Alex, $"team?projects={scope}"))["people"]!.AsArray().SelectMany(x => x!["projects"]!.AsArray()), x => x!.G("projectId") == secret.Id);
            Assert.Equal(0, await Total(TestData.Alex, $"tasks?projects={secret.Id}"));

            // Marc sees it; when Alex later loses access to b, the workspace drops it and says so without naming it.
            Assert.Equal(1, await Total(TestData.Marc, $"tasks?projects={secret.Id}"));
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{b.Id}", new { visibility = Visibility.Restricted }, d.Version(b.Id))).EnsureSuccessStatusCode();
            var alexMember = await f.DbAsync(db => db.ProjectMembers.FirstAsync(m => m.ProjectId == b.Id && m.UserId == U(TestData.Alex)));
            await f.DbAsync(async db => { var m = await db.ProjectMembers.FirstAsync(x => x.Id == alexMember.Id); m.RemovedAt = DateTimeOffset.UtcNow; return await db.SaveChangesAsync(); });
            var after = (await Get(TestData.Alex, "workspaces")).AsArray().Single(w => w!.G("id") == ws.G("id"))!;
            Assert.Equal((1, 1), (after["projectIds"]!.AsArray().Count, after.I("hidden")));
            Assert.Equal(new[] { a.ProjectNumber }, (await Get(TestData.Alex, $"tasks?projects={b.Id},{a.Id}"))["items"]!.AsArray().Select(t => t!.S("projectNumber")).Distinct());
            Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/workspaces/{ws.S("id")}")).StatusCode);
        });
    }

    [Fact]
    public async Task Projects_group_by_lifecycle_with_owner_due_date_and_derived_progress() // AC-VIS-01, FR-VIS-03
    {
        var name = "Grp" + Guid.NewGuid().ToString("N")[..6];
        var active = new List<Project>();
        for (var i = 0; i < 5; i++) active.Add(await d.Project(tweak: x => { x["name"] = $"{name} active {i}"; x["targetCompletionDate"] = "2027-03-31"; }));
        var setup = new List<Project>();
        for (var i = 0; i < 3; i++) setup.Add(await d.Project(activate: false, tweak: x => x["name"] = $"{name} setup {i}"));
        var t1 = await d.NewTask(active[0].Id, TestData.Pm, new { name = "Done one" });
        await d.NewTask(active[0].Id, TestData.Pm, new { name = "Open one" });
        await d.NewTask(active[0].Id, TestData.Pm, new { name = "Open two" });
        var t4 = await d.NewTask(active[0].Id, TestData.Pm, new { name = "Dropped" });
        await d.Move(TestData.Pm, t1, "In Progress");
        await d.Move(TestData.Pm, t1, "Complete");
        await d.Move(TestData.Pm, t4, "Cancelled", new { reason = "Out of scope" });
        foreach (var p in active.Concat(setup)) await f.Evaluate(p.Id);

        var rows = (await Get(TestData.Pm, $"projects?q={name}&pageSize=50"))["items"]!.AsArray();
        Assert.Equal((5, 3), (rows.Count(r => r!.S("status") == "Active"), rows.Count(r => r!.S("status") == "Setup")));
        var first = rows.Single(r => r!.G("id") == active[0].Id)!;
        Assert.Equal(("Priya Nair", "2027-03-31", 33), (first["pm"]!.S("displayName"), first.S("targetCompletionDate"), first.I("progressPct"))); // 1 of 3 non-cancelled, rounded down
        Assert.Null(rows.Single(r => r!.G("id") == active[1].Id)!["progressPct"]); // no tasks: "—"
        Assert.Null(rows.Single(r => r!.G("id") == setup[0].Id)!["targetCompletionDate"]); // no date: "—", never today
        Assert.Equal("Medium", first.S("priority"));
    }

    [Fact]
    public async Task The_cross_project_board_uses_four_lanes_guards_review_and_keeps_a_personal_order() // AC-VIS-02, FR-VIS-04
    {
        var ps = new[] { await d.Project(), await d.Project(), await d.Project() };
        var todo = await d.NewTask(ps[0].Id, TestData.Pm, new { name = "Survey", dueDate = "2026-09-30" });
        var doing = await d.NewTask(ps[1].Id, TestData.Pm, new { name = "Grading", dueDate = "2026-09-29" });
        var review = await d.NewTask(ps[2].Id, TestData.Pm, new { name = "Drainage report", dueDate = "2026-09-28", requiresReview = true, reviewerId = U(TestData.Marc), assigneeId = U(TestData.Alex) });
        await d.Move(TestData.Pm, doing, "In Progress");
        await d.Move(TestData.Alex, review, "In Progress");
        await d.Move(TestData.Alex, review, "Ready for Review");
        await d.Move(TestData.Marc, review, "In Review");
        await d.Move(TestData.Marc, review, "Revision Required", new { comment = "Show the outfall" });
        var ws = await f.As(TestData.Pm).Post("/api/v1/workspaces", new { name = "Board " + Guid.NewGuid().ToString("N")[..4], projectIds = ps.Select(p => p.Id) }).Result.Json(201);
        var scope = string.Join(",", ws["projectIds"]!.AsArray().Select(x => x!.GetValue<string>()));

        var cards = (await Get(TestData.Pm, $"tasks?projects={scope}&doneSince=2026-09-01"))["items"]!.AsArray();
        var lane = cards.ToDictionary(c => c!.S("name"), c => (c!.S("lane"), c.S("status"), c.S("projectNumber")));
        Assert.Equal(("To Do", "Not Started", ps[0].ProjectNumber), lane["Survey"]);
        Assert.Equal(("In Progress", "In Progress", ps[1].ProjectNumber), lane["Grading"]);
        Assert.Equal(("Review", "Revision Required", ps[2].ProjectNumber), lane["Drainage report"]); // never Done, exact status kept
        await d.Move(TestData.Alex, review, "Complete", expect: 422); // a review-required task cannot skip its reviewer

        // Manual order on a named workspace is its owner's own; the projects' shared orders are untouched (FR-VIS-04).
        var ids = new[] { doing.G("id"), todo.G("id") };
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Pm).Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = ids })).StatusCode);
        var ordered = (await Get(TestData.Pm, $"tasks?projects={scope}&sort=board&workspaceId={ws.S("id")}"))["items"]!.AsArray().Select(c => c!.G("id")).Take(2);
        Assert.Equal(ids, ordered);
        Assert.Equal(0, await f.DbAsync(db => db.BoardOrders.CountAsync(o => o.ProjectId != null && ids.Contains(o.TaskId))));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = ids })).StatusCode);
        var other = await d.Project(pm: TestData.Marc);
        var outside = await d.NewTask(other.Id, TestData.Marc);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Put($"/api/v1/workspaces/{ws.S("id")}/board-order", new { taskIds = new[] { outside.G("id") } })).StatusCode);
        // Someone else's workspace id cannot borrow its order.
        var alexWs = await f.As(TestData.Alex).Post("/api/v1/workspaces", new { name = "Borrowed " + Guid.NewGuid().ToString("N")[..4], projectIds = ps.Select(p => p.Id) }).Result.Json(201);
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Alex).Put($"/api/v1/workspaces/{alexWs.S("id")}/board-order", new { taskIds = new[] { todo.G("id") } })).StatusCode);
        Assert.Equal(todo.G("id"), (await Get(TestData.Alex, $"tasks?projects={scope}&sort=board&workspaceId={alexWs.S("id")}"))["items"]![0]!.G("id"));
        Assert.Equal(review.G("id"), (await Get(TestData.Pm, $"tasks?projects={scope}&sort=board&workspaceId={alexWs.S("id")}"))["items"]![0]!.G("id")); // ignored: due-date order
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Pm).DeleteAsync($"/api/v1/workspaces/{ws.S("id")}")).StatusCode);
        Assert.Equal(0, await f.DbAsync(db => db.BoardOrders.CountAsync(o => o.WorkspaceId == ws.G("id"))));
    }

    [Fact]
    public async Task The_gantt_shows_both_project_groups_with_bars_diamonds_and_only_each_projects_arrows() // AC-VIS-03, FR-VIS-05
    {
        var a = await d.Project();
        var b = await d.Project();
        var pm = f.As(TestData.Pm);
        var a1 = await d.NewTask(a.Id, TestData.Pm, new { name = "Design", startDate = "2026-09-14", dueDate = "2026-09-25" });
        var a2 = await d.NewTask(a.Id, TestData.Pm, new { name = "Check", startDate = "2026-09-28", dueDate = "2026-10-02" });
        await d.NewTask(b.Id, TestData.Pm, new { name = "Survey", dueDate = "2026-10-05" });
        await d.NewTask(b.Id, TestData.Pm, new { name = "Undated" });
        await pm.Post($"/api/v1/tasks/{a2.S("id")}/dependencies", new { predecessorTaskId = a1.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{b.Id}/milestones", new { name = "Tender", milestoneType = "Design Submission", date = "2026-10-09" }).Result.Json(201);

        var g = (await Get(TestData.Pm, $"timeline?projects={Ids(a, b)}"))["projects"]!.AsArray();
        Assert.Equal(new[] { a.Id, b.Id }.Order(), g.Select(x => x!["project"]!.G("id")).Order());
        var ga = g.Single(x => x!["project"]!.G("id") == a.Id)!["data"]!;
        var gb = g.Single(x => x!["project"]!.G("id") == b.Id)!["data"]!;
        Assert.Equal(("2026-09-14", "2026-09-25"), (ga["tasks"]!.AsArray().Single(t => t!.S("name") == "Design")!.S("startDate"), ga["tasks"]!.AsArray().Single(t => t!.S("name") == "Design")!.S("dueDate")));
        Assert.Single(ga["dependencies"]!.AsArray());
        Assert.Empty(gb["dependencies"]!.AsArray()); // arrows stay inside a project
        Assert.Equal("Tender", gb["milestones"]!.AsArray().Single()!.S("name"));
        Assert.Null(gb["tasks"]!.AsArray().Single(t => t!.S("name") == "Undated")!["dueDate"]); // listed as Unscheduled
        // A drag that is cancelled sends nothing, so nothing moves: the same read returns the same dates.
        Assert.Equal("2026-09-25", (await Get(TestData.Pm, $"projects/{a.Id}/timeline"))["tasks"]!.AsArray().Single(t => t!.S("name") == "Design")!.S("dueDate"));
    }

    [Fact]
    public async Task Home_figures_reconcile_with_the_lists_they_open_and_the_range_moves_only_deadlines() // AC-VIS-05, FR-VIS-07
    {
        var green = await d.Project();
        var red = await d.Project();
        var setup = await d.Project(activate: false);
        var pm = f.As(TestData.Pm);
        await d.NewTask(green.Id, TestData.Pm, new { name = "Soon", dueDate = "2026-09-18" });
        var done = await d.NewTask(green.Id, TestData.Pm, new { name = "Finished", dueDate = "2026-09-10" });
        await d.Move(TestData.Pm, done, "In Progress");
        await d.Move(TestData.Pm, done, "Complete");
        await d.NewTask(red.Id, TestData.Pm, new { name = "Late", dueDate = "2026-09-01" });
        await d.NewTask(red.Id, TestData.Pm, new { name = "Later", dueDate = "2026-11-02" });
        await pm.Post($"/api/v1/projects/{red.Id}/milestones", new { name = "Missed", milestoneType = "Design Submission", date = "2026-09-04" }).Result.Json(201);
        await d.NewTask(setup.Id, TestData.Pm, new { name = "Kick-off", dueDate = "2026-09-16" });
        foreach (var p in new[] { green, red, setup }) await f.Evaluate(p.Id);
        var scope = Ids(green, red, setup);

        var h = await Get(TestData.Pm, $"home?projects={scope}");
        var m = h["metrics"]!;
        Assert.Equal(m.I("activeProjects"), (await Get(TestData.Pm, $"projects?status=Active&mine=false&ids={scope}"))["items"]!.AsArray().Count);
        Assert.Equal(2, m.I("activeProjects"));
        Assert.Equal(m.I("openTasks"), await Total(TestData.Pm, $"tasks?projects={scope}&open=true"));
        Assert.Equal(4, m.I("openTasks"));
        Assert.Equal(m.I("overdueTasks"), await Total(TestData.Pm, $"tasks?projects={scope}&overdue=true"));
        var onTrack = m["onTrack"]!;
        Assert.Equal(onTrack.I("green"), (await Get(TestData.Pm, $"projects?status=Active&mine=false&ids={scope}&computedHealth=Green"))["items"]!.AsArray().Count);
        Assert.Equal(onTrack.I("evaluated"), (await Get(TestData.Pm, $"projects?status=Active&mine=false&ids={scope}&computedHealth=Green,Yellow,Red"))["items"]!.AsArray().Count);
        Assert.Equal((1, 2, 50), (onTrack.I("green"), onTrack.I("evaluated"), onTrack.I("pct")));
        Assert.Equal(m.I("teamMembers"), (await Get(TestData.Pm, $"team?projects={scope}"))["people"]!.AsArray().Count);
        Assert.Equal(4, m.I("teamMembers")); // priya, marc, omar, alex — each counted once
        foreach (var l in h["tasksByStatus"]!.AsArray())
            Assert.Equal(l!.I("count"), await Total(TestData.Pm, $"tasks?projects={scope}&status={Uri.EscapeDataString(l.S("statuses"))}"));
        Assert.Equal(new[] { 4, 0, 0, 1 }, h["tasksByStatus"]!.AsArray().Select(l => l!.I("count")));
        foreach (var p in h["tasksByProject"]!.AsArray())
            Assert.Equal(p!.I("total"), await Total(TestData.Pm, $"tasks?projects={scope}&projectId={p.S("projectId")}"));

        // Default range: today and the next 13 days; a wider range adds deadlines and leaves every current total alone.
        Assert.Equal(new[] { "Kick-off", "Soon" }, h["upcomingDeadlines"]!["items"]!.AsArray().Select(x => x!.S("name")));
        var wide = await Get(TestData.Pm, $"home?projects={scope}&from=2026-09-01&to=2026-11-30");
        Assert.Equal(new[] { "Late", "Missed", "Kick-off", "Soon", "Later" }, wide["upcomingDeadlines"]!["items"]!.AsArray().Select(x => x!.S("name")));
        Assert.Equal(h["metrics"]!.ToJsonString(), wide["metrics"]!.ToJsonString());
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync($"/api/v1/home?projects={scope}&from=2026-10-01&to=2026-09-01")).StatusCode);

        // With no active project evaluated, On Track is "—", not 0 %.
        var none = (await Get(TestData.Pm, $"home?projects={setup.Id}"))["metrics"]!["onTrack"]!;
        Assert.Equal((0, 0), (none.I("green"), none.I("evaluated")));
        Assert.Null(none["pct"]);
    }

    [Fact]
    public async Task Each_person_arranges_their_own_dashboard_and_can_restore_the_default() // FR-VIS-07 Edit Dashboard
    {
        var pm = f.As(TestData.Pm);
        var saved = await pm.Put("/api/v1/me/dashboard-layout", new { widgets = new object[] {
            new { id = "upcomingDeadlines", hidden = false }, new { id = "metrics", hidden = true }, new { id = "unknown", hidden = false }, new { id = "metrics", hidden = false } } }).Result.Json();
        Assert.Equal(new[] { "upcomingDeadlines", "metrics", "tasksByStatus", "tasksByProject" }, saved["layout"]!.AsArray().Select(w => w!.S("id")));
        var home = await Get(TestData.Pm, "home");
        Assert.False(home["isDefault"]!.GetValue<bool>());
        Assert.True(home["layout"]!.AsArray()[1]!["hidden"]!.GetValue<bool>());
        var other = await Get(TestData.Marc, "home");
        Assert.True(other["isDefault"]!.GetValue<bool>());
        Assert.Equal(new[] { "metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines" }, other["layout"]!.AsArray().Select(w => w!.S("id")));
        (await pm.DeleteAsync("/api/v1/me/dashboard-layout")).EnsureSuccessStatusCode();
        Assert.True((await Get(TestData.Pm, "home"))["isDefault"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Created_by_me_and_assigned_to_me_have_distinct_membership() // AC-VIS-06, FR-VIS-08
    {
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Alex, new { name = "Alex made this for Jill", assigneeId = U(TestData.Jill), dueDate = "2026-09-14", requiresReview = true, reviewerId = U(TestData.Marc) });
        var id = task.G("id");
        bool Has(JsonNode page) => page["items"]!.AsArray().Any(t => t!.G("id") == id);
        Assert.True(Has(await Get(TestData.Alex, $"tasks?projects={p.Id}&createdBy={U(TestData.Alex)}")));
        Assert.False(Has(await Get(TestData.Alex, $"tasks?projects={p.Id}&assigneeId={U(TestData.Alex)}")));
        Assert.False(Has(await Get(TestData.Alex, $"tasks?projects={p.Id}&mine=true"))); // no false assignment
        Assert.True(Has(await Get(TestData.Jill, $"tasks?projects=mine&assigneeId={U(TestData.Jill)}")));
        Assert.True(Has(await Get(TestData.Jill, "tasks?projects=mine&mine=true")));
        Assert.True(Has(await Get(TestData.Jill, $"tasks?projects=mine&mine=true&dueFrom=2026-09-14&dueTo=2026-09-14&open=true"))); // Today
        (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/collaborators", new { userId = U(TestData.Sam) })).EnsureSuccessStatusCode(); // the Civil lead may
        Assert.True(Has(await Get(TestData.Sam, "tasks?projects=mine&mine=true"))); // My Tasks: assignee or collaborator
        Assert.False(Has(await Get(TestData.Sam, $"tasks?projects=mine&assigneeId={U(TestData.Sam)}")));

        await d.Move(TestData.Jill, task, "In Progress");
        await d.Move(TestData.Jill, task, "Complete", expect: 422); // the checkbox's Complete still needs review
        await d.Move(TestData.Jill, task, "Ready for Review");
        await d.Move(TestData.Marc, task, "In Review");
        await d.Move(TestData.Marc, task, "Complete");
        Assert.True(Has(await Get(TestData.Jill, $"tasks?projects=mine&mine=true&status=Complete&completedFrom=2026-08-31&completedTo=2026-09-14")));
        Assert.False(Has(await Get(TestData.Jill, $"tasks?projects=mine&mine=true&status=Complete&completedFrom=2026-08-01&completedTo=2026-08-31")));
    }

    [Fact]
    public async Task Files_group_links_by_project_and_item_and_team_lists_active_members() // FR-VIS-09, FR-VIS-01
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var del = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Bridge drawings", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = "Check levels" });
        await pm.Post($"/api/v1/items/Project/{p.Id}/links", new { url = "https://contoso.sharepoint.com/sites/p/Shared%20Documents", title = "Project site" }).Result.Json(201);
        await pm.Post($"/api/v1/items/Deliverable/{del.S("id")}/links", new { url = @"\\fs01\projects\bridge", title = "Bridge folder" }).Result.Json(201);
        await f.As(TestData.Alex).Post($"/api/v1/items/Task/{task.S("id")}/links", new { url = "https://portal.example.com/levels", title = "Levels" }).Result.Json(201);

        var files = (await Get(TestData.Alex, $"files?projects={p.Id}"))["projects"]!.AsArray().Single()!;
        Assert.False(files["canAdd"]!.GetValue<bool>()); // project links belong to the PM
        Assert.True((await Get(TestData.Pm, $"files?projects={p.Id}"))["projects"]![0]!["canAdd"]!.GetValue<bool>());
        var items = files["items"]!.AsArray();
        Assert.Equal(new[] { "Project", "Deliverable", "Task" }, items.Select(i => i!.S("itemType")));
        var folder = items[1]!["links"]!.AsArray().Single()!;
        Assert.Equal(("Bridge folder", true), (folder.S("title"), folder["isNetworkPath"]!.GetValue<bool>())); // Copy path, never upload
        Assert.Equal(new[] { "Bridge folder" }, (await Get(TestData.Alex, $"files?projects={p.Id}&q=bridge"))["projects"]![0]!["items"]!.AsArray().SelectMany(i => i!["links"]!.AsArray()).Select(l => l!.S("title")));
        Assert.Equal(new[] { "Levels" }, (await Get(TestData.Alex, $"files?projects={p.Id}&type=Other"))["projects"]![0]!["items"]!.AsArray().SelectMany(i => i!["links"]!.AsArray()).Select(l => l!.S("title")));

        var team = (await Get(TestData.Alex, $"team?projects={p.Id}"))["people"]!.AsArray();
        var marc = team.Single(x => x!.S("displayName") == "Marc Dubois")!["projects"]![0]!;
        Assert.Equal(new[] { "Civil" }, marc["leads"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("TeamMember", team.Single(x => x!.S("displayName") == "Alex Chen")!["projects"]![0]!["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        await f.DbAsync(async db => { var u = await db.Users.FirstAsync(x => x.Email == TestData.Omar); u.IsActive = false; return await db.SaveChangesAsync(); });
        try { Assert.DoesNotContain((await Get(TestData.Alex, $"team?projects={p.Id}"))["people"]!.AsArray(), x => x!.S("displayName").StartsWith("Omar")); }
        finally { await f.DbAsync(async db => { var u = await db.Users.FirstAsync(x => x.Email == TestData.Omar); u.IsActive = true; return await db.SaveChangesAsync(); }); }
    }
}
