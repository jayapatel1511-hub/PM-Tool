using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 017 through the API: the grid's numbers and flags (FR-001..FR-004), scope and Restricted exclusion (Workflow
/// 10, §36.1), rebalancing (FR-006) and the workload reports (FR-007). Fresh people keep other tests' work out of the sums.
[Collection("api")]
public sealed class WorkloadApiTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    /// A new person reporting to Sam.
    async Task<(Guid Id, string Name)> Report()
    {
        var email = $"w{Guid.NewGuid().ToString("N")[..8]}@hub.test";
        await f.As(email).GetAsync("/api/v1/me").Result.Json();
        var row = (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users?q={email}").Result.Json())[0]!;
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{row.S("id")}", new { supervisorId = U(TestData.Sam) }, row.I("rowVersion"))).EnsureSuccessStatusCode();
        return (row.G("id"), row.S("displayName"));
    }

    static JsonNode Person(JsonNode grid, Guid id) => grid["people"]!.AsArray().Single(p => p!.G("id") == id)!;
    static decimal Hours(JsonNode person, int week) => person["cells"]![week]!["hours"]!.GetValue<decimal>();

    [Fact]
    public async Task Grid_hours_follow_the_stated_method_and_flags_fire() // FR-001..FR-004, US1, US2
    {
        var p1 = await d.Project();
        var p2 = await d.Project();
        var busy = await Report();
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = busy.Id, estimatedHours = 24, dueDate = "2026-09-25" }); // 12 h left after 50 %: 6 + 6
        var half = (await f.DbAsync(db => db.Tasks.OrderByDescending(t => t.CreatedAt).FirstAsync(t => t.AssigneeId == busy.Id))).Id;
        await f.DbAsync(async db => { var t = await db.Tasks.FirstAsync(x => x.Id == half); t.ProgressPct = 50; return await db.SaveChangesAsync(); });
        await d.NewTask(p2.Id, TestData.Pm, new { assigneeId = busy.Id, estimatedHours = 20, dueDate = "2026-09-18" });  // 20 h this week
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = busy.Id, estimatedHours = 10, dueDate = "2026-09-10" });  // overdue: this week
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = busy.Id });                                              // unestimated
        await d.NewTask(p2.Id, TestData.Pm, new { assigneeId = busy.Id, estimatedHours = 5 });                          // no due date bucket

        var over = await Report();
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = over.Id, estimatedHours = 56, startDate = "2026-09-21", dueDate = "2026-09-25" }); // 140 % next week
        var idle = await Report();
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = idle.Id, estimatedHours = 10, dueDate = "2026-10-09" }); // 2.5 h a week
        var idleUnknown = await Report();
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = idleUnknown.Id, estimatedHours = 10, dueDate = "2026-10-09" });
        await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = idleUnknown.Id });
        var crowded = await Report();
        foreach (var (pid, due) in new[] { (p1.Id, "2026-09-21"), (p1.Id, "2026-09-22"), (p2.Id, "2026-09-23") })
            await d.NewTask(pid, TestData.Pm, new { assigneeId = crowded.Id, estimatedHours = 1, dueDate = due });
        foreach (var p in new[] { p1, p2 }) await f.Evaluate(p.Id);

        var grid = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
        Assert.Equal("2026-09-14", grid["weeks"]![0]!.GetValue<string>());
        Assert.Equal(8, grid["weeks"]!.AsArray().Count);
        var b = Person(grid, busy.Id);
        Assert.Equal(36, Hours(b, 0)); // 6 + 20 + 10
        Assert.Equal(6, Hours(b, 1));
        Assert.Equal(90, b["cells"]![0]!["pct"]!.GetValue<decimal>());
        Assert.Equal(5, b["noDueDate"]!.GetValue<decimal>());
        Assert.Equal((5, 1, 1, 2), (b.I("openTasks"), b.I("unestimated"), b.I("overdue"), b.I("projects")));
        Assert.Equal(40, b["capacity"]!.GetValue<decimal>());
        Assert.True(Person(grid, over.Id)["overAssigned"]!.GetValue<bool>());
        Assert.Equal("over", Person(grid, over.Id).S("indicator"));
        Assert.True(Person(grid, idle.Id)["underAssigned"]!.GetValue<bool>());
        Assert.False(Person(grid, idleUnknown.Id)["underAssigned"]!.GetValue<bool>()); // never with unestimated work
        Assert.True(Person(grid, crowded.Id)["cluster"]!.GetValue<bool>());
        Assert.False(Person(grid, busy.Id)["cluster"]!.GetValue<bool>());

        var overOnly = await f.As(TestData.Sam).GetAsync("/api/v1/workload?indicator=over").Result.Json();
        Assert.Contains(overOnly["people"]!.AsArray(), p => p!.G("id") == over.Id);
        Assert.DoesNotContain(overOnly["people"]!.AsArray(), p => p!.G("id") == idle.Id);
        var byProject = await f.As(TestData.Sam).GetAsync($"/api/v1/workload?projectId={p2.Id}").Result.Json();
        Assert.Equal(20, Hours(Person(byProject, busy.Id), 0)); // only that project's work
        Assert.DoesNotContain(byProject["people"]!.AsArray(), p => p!.G("id") == over.Id);
    }

    [Fact]
    public async Task Restricted_work_is_absent_and_rebalancing_notifies_and_recomputes() // Workflow 10, §36.1, FR-006
    {
        var p = await d.Project();
        var from = await Report();
        var to = await Report();
        var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = from.Id, estimatedHours = 16, dueDate = "2026-09-18" });
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var secret = await d.Project();
            await d.NewTask(secret.Id, TestData.Pm, new { assigneeId = from.Id, estimatedHours = 30, dueDate = "2026-09-18" });
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{secret.Id}", new { visibility = Visibility.Restricted }, d.Version(secret.Id))).EnsureSuccessStatusCode();
            var sams = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
            Assert.Equal(16, Hours(Person(sams, from.Id), 0)); // the Restricted project's 30 h are not in Sam's grid
            var lenas = await f.As(TestData.Lena).GetAsync($"/api/v1/workload?supervisorId={U(TestData.Sam)}").Result.Json();
            Assert.Equal(46, Hours(Person(lenas, from.Id), 0)); // an Executive sees Restricted projects
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }

        var drill = await f.As(TestData.Sam).GetAsync($"/api/v1/workload/{from.Id}/tasks").Result.Json();
        var row = drill["projects"]!.AsArray().SelectMany(x => x!["tasks"]!.AsArray()).Single(x => x!.G("id") == task.G("id"))!;
        Assert.True(row["canReassign"]!.GetValue<bool>());
        Assert.Equal(16, row["weeks"]![0]!.GetValue<decimal>());

        (await f.As(TestData.Sam).Patch($"/api/v1/tasks/{task.S("id")}", new { assigneeId = to.Id }, await d.TaskVersion(task))).EnsureSuccessStatusCode();
        var (toHeard, fromHeard, pmHeard, logged) = await f.DbAsync(async db => (
            await db.Notifications.AnyAsync(n => n.UserId == to.Id && n.EventType == NotificationEvents.TaskAssigned && n.ItemId == task.G("id")),
            await db.Notifications.AnyAsync(n => n.UserId == from.Id && n.EventType == NotificationEvents.WorkReassignedAway && n.ItemId == task.G("id")),
            await db.Notifications.AnyAsync(n => n.UserId == U(TestData.Pm) && n.EventType == NotificationEvents.SupervisorStaffing && n.ItemId == task.G("id")),
            await db.ActivityLog.AnyAsync(a => a.ItemId == task.G("id") && a.ActorUserId == U(TestData.Sam) && a.Categories.Contains("assignment"))));
        Assert.True(toHeard); Assert.True(fromHeard); Assert.True(pmHeard); Assert.True(logged);
        var after = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
        Assert.Equal(0, Hours(Person(after, from.Id), 0));
        Assert.Equal(16, Hours(Person(after, to.Id), 0));

        // Capacity: Supervisors for their reports, Admins for anyone.
        (await f.As(TestData.Sam).Put($"/api/v1/users/{to.Id}/capacity", new { hours = 20 })).EnsureSuccessStatusCode();
        var part = Person(await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json(), to.Id);
        Assert.Equal((20m, true, 80m), (part["capacity"]!.GetValue<decimal>(), part["capacityOverride"]!.GetValue<bool>(), part["cells"]![0]!["pct"]!.GetValue<decimal>()));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Put($"/api/v1/users/{U(TestData.Pm)}/capacity", new { hours = 20 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/workload")).StatusCode);
    }

    [Fact]
    public async Task Workload_reports_and_export() // FR-007, §19
    {
        var p = await d.Project();
        var who = await Report();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = who.Id, estimatedHours = 8, dueDate = "2026-09-15" });
        var employee = await f.As(TestData.Sam).GetAsync("/api/v1/reports/workload-employee").Result.Json();
        Assert.Contains("Week of 2026-09-14", employee["columns"]!.AsArray().Select(c => c!.S("header")));
        var mine = employee["rows"]!.AsArray().Single(r => r!.S("person") == who.Name)!;
        Assert.Equal(8, mine["w0"]!.GetValue<decimal>());
        var discipline = await f.As(TestData.Lena).GetAsync("/api/v1/reports/workload-discipline").Result.Json();
        Assert.Contains(discipline["rows"]!.AsArray(), r => r!.S("discipline") == "Civil");
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Sam).GetAsync("/api/v1/workload/export?format=xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Sam).GetAsync("/api/v1/reports/workload-employee/export?format=csv")).StatusCode);
    }
}
