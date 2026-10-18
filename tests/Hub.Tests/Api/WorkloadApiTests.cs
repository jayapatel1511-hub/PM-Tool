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
    public async Task Discipline_selects_people_but_keeps_person_totals_consistent()
    {
        var project = await d.Project();
        var person = await Report();
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 4m, startDate = "2026-09-14", dueDate = "2026-09-14" }, "Civil");
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 8m, startDate = "2026-09-14", dueDate = "2026-09-14" }, "Electrical");
        var civil = await d.Discipline("Civil");
        var grid = await f.As(TestData.Sam).GetAsync($"/api/v1/workload?disciplineId={civil}").Result.Json();
        Assert.Equal(12, Hours(Person(grid, person.Id), 0));
        Assert.Equal(12, Person(grid, person.Id)["cells"]![0]!["committed"]!.GetValue<decimal>());
    }

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
        Assert.False(Person(grid, idle.Id)["underAssigned"]!.GetValue<bool>()); // visible work cannot prove spare capacity
        Assert.True(Person(grid, idle.Id)["partialScope"]!.GetValue<bool>());
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
    public async Task Unfilled_roles_spread_like_an_allocation_and_leave_person_rows_unchanged() // packet 036 AC-ROLE-02a, -02b, -02c
    {
        var p = await d.Project();
        var person = await Report();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 16, startDate = "2027-03-15", dueDate = "2027-03-26" });
        var path = $"/api/v1/workload?from=2027-03-08&projectId={p.Id}";
        var before = (await (await f.As(TestData.Pm).GetAsync(path)).Json())["people"]!.ToJsonString();
        // 80 h over the four full working weeks of 15 March to 9 April 2027 (20 working days, no holidays).
        var role = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles",
            new { roleLabel = "Civil technologist", fromDate = "2027-03-15", throughDate = "2027-04-09", plannedHours = 80 })).Json(201);
        decimal[] Cells(JsonNode grid) => [.. grid["unfilledRoles"]!.AsArray().Single(r => r!.G("id") == role.G("id"))!["cells"]!.AsArray().Select(c => c!["hours"]!.GetValue<decimal>())];
        var after = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Equal(before, after["people"]!.ToJsonString()); // no person's hours, percentage or indicator moves
        Assert.Equal(new[] { 0m, 20, 20, 20, 20, 0, 0, 0 }, Cells(after));
        Assert.Equal(p.ProjectNumber, after["unfilledRoles"]![0]!.S("projectNumber"));
        Assert.Empty((await (await f.As(TestData.Pm).GetAsync($"/api/v1/workload?from=2027-03-08&projectId={(await d.Project()).Id}")).Json())["unfilledRoles"]!.AsArray());

        // An organisation holiday on Wednesday 24 March leaves 19 working days: 80/19 h each, the last day taking the remainder.
        var holiday = await (await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { date = "2027-03-24", name = "Synthetic role holiday" })).Json(201);
        try
        {
            Assert.Equal(new[] { 0m, 21.1m, 16.8m, 21.1m, 21.1m, 0, 0, 0 }, Cells(await (await f.As(TestData.Pm).GetAsync(path)).Json()));
            var exact = AllocationRules.Spread(new(2027, 3, 15), new(2027, 4, 9), 80, new WorkCalendar([new DateOnly(2027, 3, 24)]));
            Assert.Equal((19, 80m), (exact.Count, exact.Values.Sum()));
        }
        finally { (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/holidays/{holiday.S("id")}")).EnsureSuccessStatusCode(); }

        await (await f.As(TestData.Pm).Post($"/api/v1/open-roles/{role.S("id")}/cancel", new { rowVersion = role.I("rowVersion") })).Json();
        var cancelled = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Empty(cancelled["unfilledRoles"]!.AsArray());
        Assert.Equal(before, cancelled["people"]!.ToJsonString());
    }

    [Fact] // AC-PRO-13, AC-PRO-23
    public async Task Unassigned_tasks_are_dark_and_the_scoped_show_all_count_matches()
    {
        var project = await d.Project(); var other = await d.Project();
        var first = await d.NewTask(project.Id, TestData.Pm, new { estimatedHours = 4m, startDate = "2026-09-14", dueDate = "2026-09-18" });
        await d.NewTask(project.Id, TestData.Pm, new { estimatedHours = 6m, dueDate = "2026-09-19" });
        await d.NewTask(project.Id, TestData.Pm, new { estimatedHours = 8m }, "Electrical");
        await d.NewTask(other.Id, TestData.Pm);
        var civil = await d.Discipline("Civil"); var pm = f.As(TestData.Pm); var admin = f.As(TestData.Admin);
        var path = $"/api/v1/workload?projectId={project.Id}&disciplineId={civil}";
        var off = await (await pm.GetAsync(path)).Json(); Assert.False(off.AsObject().ContainsKey("unassigned"));
        var export = await (await pm.GetAsync(path.Replace("workload?", "workload/export?format=csv&"))).Content.ReadAsByteArrayAsync();
        (await admin.Put("/api/v1/admin/settings/flag.unfilled_beside_capacity", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            var response = await (await pm.GetAsync(path)).Json(); var unassigned = response["unassigned"]!;
            Assert.Equal(2, unassigned.I("totalCount")); Assert.Equal(2, unassigned["tasks"]!.AsArray().Count);
            Assert.All(unassigned["tasks"]!.AsArray(), task => Assert.True(task!["canAssign"]!.GetValue<bool>()));
            Assert.Equal("2026-09-14", unassigned["tasks"]!.AsArray().Single(t => t!.G("id") == first.G("id"))!.S("startDate"));
            var list = await (await pm.GetAsync("/api/v1" + unassigned.S("showAllPath"))).Json();
            Assert.Equal(unassigned.I("totalCount"), list.I("total"));
            Assert.Equal(off["people"]!.ToJsonString(), response["people"]!.ToJsonString());
            Assert.Equal(export, await (await pm.GetAsync(path.Replace("workload?", "workload/export?format=csv&"))).Content.ReadAsByteArrayAsync());
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.unfilled_beside_capacity", new { value = false })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Person_rows_carry_office_job_title_and_home_discipline() // packet 036 FR-GRP-02
    {
        var person = await Report();
        var civil = await d.Discipline("Civil");
        await f.DbAsync(async db => { var u = await db.Users.SingleAsync(x => x.Id == person.Id); u.JobTitle = "Civil Designer (EIT)"; u.HomeDisciplineId = civil; return await db.SaveChangesAsync(); });
        var row = Person(await (await f.As(TestData.Sam).GetAsync("/api/v1/workload")).Json(), person.Id);
        Assert.Equal("Civil Designer (EIT)", row.S("jobTitle"));
        Assert.Equal(civil, row.G("homeDisciplineId"));
        Assert.True(row.AsObject().ContainsKey("officeId"));
    }

    [Fact]
    public async Task Cells_carry_bands_over_week_and_thresholds_from_settings() // packet 041 AC-FIG-05, AC-FIG-06, AC-FIG-07
    {
        var p = await d.Project();
        var person = await Report();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 40, dueDate = "2026-09-18" });                        // 100 % this week
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 46, startDate = "2026-09-21", dueDate = "2026-09-25" }); // 115 % next week
        var none = await Report();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = none.Id, estimatedHours = 8, dueDate = "2026-09-18" });
        (await f.As(TestData.Sam).Put($"/api/v1/users/{none.Id}/capacity", new { hours = 0 })).EnsureSuccessStatusCode();
        var admin = f.As(TestData.Admin);
        string[] Bands(JsonNode row) => [.. row["cells"]!.AsArray().Take(2).Select(c => c!.S("band"))];
        (int, int, int) Thresholds(JsonNode grid) => (grid["thresholds"]!.I("overPct"), grid["thresholds"]!.I("amberPct"), grid["thresholds"]!.I("underPct"));

        var grid = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
        var row = Person(grid, person.Id);
        Assert.Equal((110, 90, 40), Thresholds(grid));
        Assert.Equal(new[] { "near", "over" }, Bands(row));
        Assert.Equal("2026-09-21", row.S("overWeek")); // AC-FIG-07: the first Over week of this and next week
        Assert.True(row["overAssigned"]!.GetValue<bool>());
        Assert.Equal("over", Person(grid, none.Id)["cells"]![0]!.S("band")); // no availability with committed hours is 999 %
        try
        {
            await admin.Put("/api/v1/admin/settings/workload_amber_pct", new { value = 80 }).Result.Json();
            await admin.Put("/api/v1/admin/settings/workload_over_pct", new { value = 120 }).Result.Json();
            var moved = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
            var after = Person(moved, person.Id);
            Assert.Equal((120, 80, 40), Thresholds(moved));
            Assert.Equal(new[] { "near", "near" }, Bands(after));
            Assert.Null(after["overWeek"]);
            Assert.False(after["overAssigned"]!.GetValue<bool>()); // Over-assigned fires only above 120 %
        }
        finally
        {
            await admin.Put("/api/v1/admin/settings/workload_amber_pct", new { value = 90 }).Result.Json();
            await admin.Put("/api/v1/admin/settings/workload_over_pct", new { value = 110 }).Result.Json();
        }
        var restored = Person(await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json(), person.Id);
        Assert.Equal(new[] { "near", "over" }, Bands(restored));
        Assert.True(restored["overAssigned"]!.GetValue<bool>());
    }

    [Fact, Trait("flag-off", "figure_names")]
    public async Task Uncommitted_is_computed_on_the_server() // packet 041 AC-FIG-03
    {
        var p = await d.Project();
        var person = await Report();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 28, startDate = "2026-09-14", dueDate = "2026-09-18" });
        foreach (var (hours, confidence) in new[] { (10m, PlanningConfidence.Confirmed), (6m, PlanningConfidence.Expected) })
            await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = person.Id, hoursPerWeek = hours, startWeek = "2026-09-14",
                endWeek = "2026-09-14", label = "Figure basis", sourceCategory = PlanningSource.Proposal, confidence })).Json(201);
        decimal? Uncommitted(JsonNode grid) => Person(grid, person.Id)["cells"]![0]!["uncommitted"]?.GetValue<decimal>();

        // Planning entries do not count: 40 − 28 = 12, while the Weekly Planner's remaining capacity is 40 − 10 − 6 = 24 (AC-PLN-11).
        Assert.Equal(12, Uncommitted(await f.As(TestData.Admin).GetAsync("/api/v1/workload").Result.Json())); // Admin sees every project
        var planner = await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={person.Id}&from=2026-09-14&weeks=1").Result.Json();
        Assert.Equal(24, planner["people"]![0]!["cells"]![0]!["remaining"]!.GetValue<decimal>());

        // Sam cannot see every Restricted project, so his rows are partial: spare capacity is withheld, an overload is not.
        var partial = await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json();
        Assert.True(Person(partial, person.Id)["partialScope"]!.GetValue<bool>());
        Assert.Null(Uncommitted(partial));
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 15, startDate = "2026-09-14", dueDate = "2026-09-18" });
        Assert.Equal(-3, Uncommitted(await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json()));
    }

    [Fact, Trait("flag-on", "figure_names")]
    public async Task Figure_names_flag_changes_no_workload_value() // packet 041 AC-FIG-03, FR-FIG-04: only labels change
    {
        var person = await Report();
        await d.NewTask((await d.Project()).Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 28, startDate = "2026-09-14", dueDate = "2026-09-18" });
        async Task<string> Row() => Person(await f.As(TestData.Sam).GetAsync("/api/v1/workload").Result.Json(), person.Id).ToJsonString();
        var off = await Row();
        var admin = f.As(TestData.Admin);
        try
        {
            await admin.Put("/api/v1/admin/settings/flag.figure_names", new { value = true }).Result.Json();
            Assert.True((await f.As(TestData.Sam).GetAsync("/api/v1/me").Result.Json())["flags"]!["figure_names"]!.GetValue<bool>());
            Assert.Equal(off, await Row());
        }
        finally { await admin.Put("/api/v1/admin/settings/flag.figure_names", new { value = false }).Result.Json(); }
    }

    [Fact]
    public async Task Workload_threshold_changes_are_audited_and_validated() // packet 041 AC-FIG-06
    {
        var admin = f.As(TestData.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put("/api/v1/admin/settings/workload_over_pct", new { value = 49 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put("/api/v1/admin/settings/workload_amber_pct", new { value = 301 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put("/api/v1/admin/settings/workload_under_pct", new { value = 101 })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Put("/api/v1/admin/settings/workload_over_pct", new { value = 120 })).StatusCode);
        int Entries(string key) => f.Db(db => db.ActivityLog.Count(a => a.ItemType == ItemType.Setting && a.ItemKey == key));
        foreach (var (key, value, fallback) in new[] { ("workload_over_pct", 120, 110), ("workload_amber_pct", 80, 90), ("workload_under_pct", 30, 40) })
        {
            var before = Entries(key);
            try
            {
                (await admin.Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
                Assert.Equal(before + 1, Entries(key));
            }
            finally { (await admin.Put($"/api/v1/admin/settings/{key}", new { value = fallback })).EnsureSuccessStatusCode(); }
        }
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
