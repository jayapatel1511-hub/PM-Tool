using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 007: Project Dashboard (AC-DASH-01..03), Weekly Coordination (AC-WC-01/02/05/07, FR-001), My Work (AC-MYW-01..04),
/// My Staff and supervisor staffing (AC-ASG-07..09, §8.5.1 reassignment).
[Collection("api")]
public sealed class SurfacesTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<JsonNode> Seeded()
    {
        var p = await d.Project();
        var pred = await d.NewTask(p.Id, TestData.Pm, new { name = "Geotech recommendations", assigneeId = U(TestData.Omar), dueDate = "2026-09-10" }, "Electrical");
        for (var i = 0; i < 3; i++) await d.NewTask(p.Id, TestData.Pm, new { name = $"Grading {i}", assigneeId = U(TestData.Alex), startDate = "2026-09-12", dueDate = "2026-09-30", dependsOn = new[] { pred.G("id") } });
        await d.NewTask(p.Id, TestData.Pm, new { name = "Survey", assigneeId = U(TestData.Alex), dueDate = "2026-09-16" }); // due this week
        await d.NewTask(p.Id, TestData.Pm, new { name = "Unassigned drawing", dueDate = "2026-09-25" });
        var done = await d.NewTask(p.Id, TestData.Pm, new { name = "Kickoff notes", assigneeId = U(TestData.Alex) });
        await d.Move(TestData.Alex, done, TaskStatuses.Complete);
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Late check", milestoneType = "Other", date = "2026-09-01" }).Result.Json(201);
        foreach (var (n, date) in new[] { ("M a", "2026-10-01"), ("M b", "2026-10-15"), ("M c", "2026-11-01"), ("M d", "2026-12-01"), ("M e", "2027-01-15") })
            await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = n, milestoneType = "Other", date }).Result.Json(201);
        await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Civil package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-20" }).Result.Json(201);
        await f.Evaluate(p.Id);
        return await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json();
    }

    [Fact]
    public async Task Every_dashboard_count_equals_the_list_it_opens() // AC-DASH-01, AC-DASH-02, SC-004
    {
        var p = await Seeded();
        var pm = f.As(TestData.Pm);
        var dash = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/dashboard").Result.Json();
        Assert.Equal(3, dash["tasks"]!["blocked"]!.I("value"));
        foreach (var (key, c) in dash["tasks"]!.AsObject())
        {
            var list = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/tasks?pageSize=200&{c!.S("link")}").Result.Json();
            Assert.True(c.I("value") == list.I("totalCount"), $"tasks.{key}: dashboard {c.I("value")} vs list {list.I("totalCount")}");
        }
        foreach (var (key, c) in dash["deliverables"]!.AsObject())
        {
            var list = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/deliverables?{c!.S("link")}").Result.Json();
            Assert.True(c.I("value") == list.AsArray().Count, $"deliverables.{key}");
        }
        var civil = d.ProjectDiscipline(p.G("id"), "Civil");
        var row = dash["disciplines"]!.AsArray().Single(x => x!.G("disciplineId") == civil)!;
        var civilOpen = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/tasks?disciplineId={civil}&open=true").Result.Json();
        Assert.Equal(row.I("open"), civilOpen.I("totalCount"));
        var civilBlocked = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/tasks?disciplineId={civil}&blocked=true").Result.Json();
        Assert.Equal(row.I("blocked"), civilBlocked.I("totalCount"));
        Assert.Equal(2, dash["disciplines"]!.AsArray().Count); // one row per active discipline

        var scoped = await pm.GetAsync($"/api/v1/projects/{p.S("id")}/dashboard?disciplineId={civil}").Result.Json();
        Assert.Equal(3, scoped["tasks"]!["blocked"]!.I("value"));
        Assert.Contains($"disciplineId={civil}", scoped["tasks"]!["blocked"]!.S("link"));
        Assert.Single(scoped["disciplines"]!.AsArray());
        Assert.Equal(3, dash["blocked"]!.I("total"));
        Assert.True(dash["activity"]!.AsArray().Count <= 15);
    }

    [Fact]
    public async Task Milestone_strip_shows_five_with_overdue_first() // AC-DASH-03
    {
        var p = await Seeded();
        var ms = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.S("id")}/dashboard").Result.Json())["milestones"]!.AsArray();
        Assert.Equal(5, ms.Count);
        Assert.Equal("Late check", ms[0]!.S("name"));
        Assert.True(ms[0]!.I("daysRemaining") < 0);
        Assert.Equal(new[] { "M a", "M b", "M c", "M d" }, ms.Skip(1).Select(x => x!.S("name")));
    }

    [Fact]
    public async Task Coordination_sections_scope_and_review_marker() // AC-WC-01, AC-WC-02, AC-WC-05, AC-WC-07, FR-001
    {
        var p = await Seeded();
        var id = p.S("id");
        var pm = f.As(TestData.Pm);
        (await pm.Patch($"/api/v1/projects/{id}", new { coordinationDay = "Wednesday" }, d.Version(p.G("id")))).EnsureSuccessStatusCode();
        var wc = await pm.GetAsync($"/api/v1/projects/{id}/coordination").Result.Json();
        Assert.Equal("2026-09-09", wc["window"]!["thisWeek"]!.S("from")); // Monday 14 Sep belongs to the week starting Wednesday 9 Sep
        Assert.Equal("2026-09-15", wc["window"]!["thisWeek"]!.S("to"));
        var blocked = wc["blocked"]!.AsArray();
        Assert.Equal(3, blocked.Count);
        var predKey = blocked.Select(x => x!["state"]!["blockedBy"]![0]!.S("key")).Distinct().Single(); // one cause for the three tasks
        Assert.EndsWith("T0001", predKey);
        Assert.Contains(wc["overdue"]!.AsArray(), x => x!.S("name") == "Geotech recommendations");
        Assert.Empty(wc["dueThisWeek"]!.AsArray()); // nothing is due on 14–15 Sep
        Assert.Contains(wc["upcoming"]!["tasks"]!.AsArray(), x => x!.S("name") == "Survey"); // 16 Sep falls in next week (16–22 Sep)
        Assert.Contains(wc["completed"]!["tasks"]!.AsArray(), x => x!.S("name") == "Kickoff notes"); // within the last 7 days, never reviewed

        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).PostAsync($"/api/v1/projects/{id}/coordination/reviewed", null)).StatusCode);
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = saved.AddMinutes(1);
            await pm.PostAsync($"/api/v1/projects/{id}/coordination/reviewed", null).Result.Json();
            f.Clock.Now = saved.AddMinutes(2);
            var after = await pm.GetAsync($"/api/v1/projects/{id}/coordination").Result.Json();
            Assert.Empty(after["completed"]!["tasks"]!.AsArray()); // AC-WC-01: only what finished since the review
            Assert.Equal("Priya Nair", after["window"]!.S("reviewedBy"));
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == p.G("id") && a.Action == "CoordinationReviewed")));
        }
        finally { f.Clock.Now = saved; }

        var elec = d.ProjectDiscipline(p.G("id"), "Electrical");
        var scoped = await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{id}/coordination?disciplineId={elec}").Result.Json();
        Assert.Empty(scoped["blocked"]!.AsArray()); // the blocked tasks are Civil's
        Assert.All(scoped["overdue"]!.AsArray(), x => Assert.Equal(elec, x!.G("projectDisciplineId")));
        Assert.Single(scoped["disciplines"]!.AsArray());
    }

    [Fact]
    public async Task My_work_sections_and_who_may_see_them() // AC-MYW-01, AC-MYW-02, AC-MYW-03, AC-MYW-04
    {
        var tester = "mywork." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        (await f.As(tester).GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var me = U(tester);
        var p = await d.Project();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember } }).Result.Json(201);
        var mine = new List<JsonNode>();
        for (var i = 0; i < 3; i++) mine.Add(await d.NewTask(p.Id, TestData.Pm, new { name = $"Mine {i}", assigneeId = me, dueDate = "2026-09-30" }));
        var shared = await d.NewTask(p.Id, TestData.Pm, new { name = "Shared", assigneeId = U(TestData.Alex) });
        (await f.As(TestData.Pm).Post($"/api/v1/tasks/{shared.S("id")}/collaborators", new { userId = me })).EnsureSuccessStatusCode();
        for (var i = 0; i < 2; i++)
        {
            var r = await d.NewTask(p.Id, TestData.Pm, new { name = $"Review {i}", assigneeId = U(TestData.Alex), reviewerId = me, requiresReview = true });
            await d.Move(TestData.Alex, r, TaskStatuses.InProgress);
            await d.Move(TestData.Alex, r, TaskStatuses.ReadyForReview);
        }
        await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Owned", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), ownerId = me }).Result.Json(201);
        var blocker = await d.NewTask(p.Id, TestData.Pm, new { name = "Other's work", assigneeId = U(TestData.Jill), dueDate = "2026-09-01" });
        (await f.As(TestData.Pm).Post($"/api/v1/tasks/{mine[0].S("id")}/dependencies", new { predecessorTaskId = blocker.G("id") })).EnsureSuccessStatusCode();
        var waiting = await d.NewTask(p.Id, TestData.Pm, new { name = "Waits on me", assigneeId = U(TestData.Alex), dependsOn = new[] { mine[1].G("id") } });
        await f.Evaluate(p.Id);

        var work = await f.As(tester).GetAsync("/api/v1/me/work").Result.Json();
        Assert.Equal(4, work["tasks"]!.AsArray().Count); // assignee on 3, collaborator on 1
        Assert.Equal(2, work["reviews"]!["tasks"]!.AsArray().Count);
        Assert.Single(work["deliverables"]!.AsArray());
        Assert.False(work["readOnly"]!.GetValue<bool>());
        var blockedRow = work["tasks"]!.AsArray().Single(x => x!.S("id") == mine[0].S("id"))!;
        Assert.True(blockedRow["state"]!["isBlocked"]!.GetValue<bool>());
        var owner = blockedRow["state"]!["blockedBy"]![0]!.G("ownerId");
        Assert.Equal("Jill Martin", work["people"]![owner.ToString()]!.GetValue<string>()); // AC-MYW-02: blocker and its owner
        var blocking = work["tasks"]!.AsArray().Single(x => x!.S("id") == mine[1].S("id"))!;
        Assert.Contains(waiting.G("id"), blocking["state"]!["blockingTaskIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
        Assert.Equal("Alex Chen", work["successors"]!.AsArray().Single(x => x!.S("id") == waiting.S("id"))!.S("assigneeName")); // AC-MYW-03
        Assert.Contains(work["projects"]!.AsArray(), x => x!.G("id") == p.Id && x["follow"]!.S("level") == FollowLevel.AllActivity);

        var sup = f.As(TestData.Sam);
        var alexWork = await sup.GetAsync($"/api/v1/me/work?userId={U(TestData.Alex)}").Result.Json(); // Sam supervises Alex
        Assert.True(alexWork["readOnly"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).GetAsync($"/api/v1/me/work?userId={U(TestData.Alex)}")).StatusCode);
        var pmView = await f.As(TestData.Pm).GetAsync($"/api/v1/me/work?userId={me}").Result.Json(); // PM of the person's project
        Assert.All(pmView["tasks"]!.AsArray(), x => Assert.Equal(p.Id, x!.G("projectId")));
    }

    [Fact]
    public async Task My_staff_last_activity_counts_only_activity_the_supervisor_may_see() // AC-ASG-09
    {
        var supervisor = $"s{Guid.NewGuid().ToString("N")[..8]}@hub.test";
        var report = $"r{Guid.NewGuid().ToString("N")[..8]}@hub.test";
        HttpClient Supervisor() => f.As(supervisor, "Hub.Supervisor");
        await Supervisor().GetAsync("/api/v1/me").Result.Json();
        await f.As(report).GetAsync("/api/v1/me").Result.Json();
        var row = (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users?q={report}").Result.Json())[0]!;
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{row.S("id")}", new { supervisorId = U(supervisor) }, row.I("rowVersion"))).EnsureSuccessStatusCode();
        async Task<JsonNode?> LastActivity() => (await Supervisor().GetAsync("/api/v1/staff").Result.Json())["people"]!.AsArray()
            .Single(x => x!.G("id") == row.G("id"))!["lastActivityAt"];
        async Task CommentAsReport(Guid projectId)
        {
            await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/members", new { userId = row.G("id"), roles = new[] { "TeamMember" } }).Result.Json(201);
            var task = await d.NewTask(projectId, TestData.Pm);
            await f.As(report).Post($"/api/v1/items/Task/{task.S("id")}/comments", new { body = "Checked the grading levels" }).Result.Json(201);
        }

        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var hidden = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
            await CommentAsReport(hidden.Id);
            Assert.Null(await LastActivity()); // the report's only activity is in a project the supervisor cannot see
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
        await CommentAsReport((await d.Project()).Id);
        Assert.NotNull(await LastActivity());
    }

    [Fact]
    public async Task My_staff_scopes_counts_and_staffing() // AC-ASG-07, AC-ASG-08, AC-ASG-09, ASG-10
    {
        var sam = f.As(TestData.Sam);
        var mine = await sam.GetAsync("/api/v1/staff").Result.Json();
        var names = mine["people"]!.AsArray().Select(x => x!.S("displayName")).ToList();
        Assert.Contains("Alex Chen", names);
        Assert.DoesNotContain("Sam Patel", names);
        var lena = await f.As(TestData.Lena).GetAsync("/api/v1/staff").Result.Json();
        Assert.Contains("Sam Patel", lena["people"]!.AsArray().Select(x => x!.S("displayName")));
        Assert.DoesNotContain("Alex Chen", lena["people"]!.AsArray().Select(x => x!.S("displayName")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.GetAsync("/api/v1/staff?scope=all")).StatusCode);
        Assert.True((await f.As(TestData.Lena).GetAsync("/api/v1/staff?scope=all").Result.Json())["people"]!.AsArray().Count >= 10);

        int AlexOpen(JsonNode staff) => staff["people"]!.AsArray().Single(x => x!.S("displayName") == "Alex Chen")!.I("open");
        var before = AlexOpen(await sam.GetAsync("/api/v1/staff").Result.Json());
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var hidden = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
            await d.NewTask(hidden.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
            await d.NewTask(hidden.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
            Assert.Equal(before, AlexOpen(await sam.GetAsync("/api/v1/staff").Result.Json())); // AC-ASG-08: Sam cannot see that project
            var assignments = await sam.GetAsync($"/api/v1/staff/{U(TestData.Alex)}/assignments").Result.Json();
            Assert.DoesNotContain(assignments.AsArray(), x => x!.G("projectId") == hidden.Id);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }

        var target = await d.Project();
        var jillRemoved = await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == target.Id && m.UserId == U(TestData.Jill)));
        Assert.False(jillRemoved);
        (await sam.Post($"/api/v1/projects/{target.Id}/members", new { userId = U(TestData.Jill), roles = new[] { ProjectRole.TeamMember }, primaryDisciplineId = d.ProjectDiscipline(target.Id, "Civil") })).EnsureSuccessStatusCode();
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Pm) && n.ProjectId == target.Id && n.EventType == NotificationEvents.SupervisorStaffing && n.Title.Contains("Sam Patel"))));
        Assert.Equal(FollowLevel.AllActivity, await f.DbAsync(db => db.Follows.Where(x => x.ProjectId == target.Id && x.UserId == U(TestData.Jill)).Select(x => x.Level).FirstAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.Post($"/api/v1/projects/{target.Id}/members", new { userId = U(TestData.Diane), roles = new[] { ProjectRole.TeamMember } })).StatusCode); // not his report
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.Post($"/api/v1/projects/{target.Id}/members", new { userId = U(TestData.Jill), roles = new[] { ProjectRole.PM } })).StatusCode);
        var jillRow = (await sam.GetAsync($"/api/v1/staff/{U(TestData.Jill)}/assignments").Result.Json()).AsArray().Single(x => x!.G("projectId") == target.Id)!;
        Assert.True(jillRow["canRemove"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Supervisors_reassign_their_direct_reports_tasks_only() // §8.5.1
    {
        var p = await d.Project();
        var alexTask = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        var dianeTask = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Diane) });
        var sam = f.As(TestData.Sam);
        (await sam.Patch($"/api/v1/tasks/{alexTask.S("id")}", new { assigneeId = U(TestData.Jill) }, await d.TaskVersion(alexTask))).EnsureSuccessStatusCode();
        Assert.Equal(U(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == alexTask.G("id")).Select(t => t.AssigneeId).FirstAsync()));
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.Patch($"/api/v1/tasks/{dianeTask.S("id")}", new { assigneeId = U(TestData.Jill) }, await d.TaskVersion(dianeTask))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.Patch($"/api/v1/tasks/{alexTask.S("id")}", new { dueDate = "2026-12-01" }, await d.TaskVersion(alexTask))).StatusCode); // reassignment only
    }
}
