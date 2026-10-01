using System.Net;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 002: AC-PRJ-01..06, AC-PERM-04/05, AC-TEAM-02/04, TM-01, TM-05, P-07, ASG-01, ASG-10.
[Collection("api")]
public sealed class ProjectsTests(HubFactory f)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task Newly_added_restricted_member_receives_the_membership_notice()
    {
        var admin = f.As(TestData.Admin);
        await admin.Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true });
        try {
            var p = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).EnsureSuccessStatusCode();
            await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Diane), roles = new[] { "Reviewer" } })).Json(201);
            Assert.True(f.Db(db => db.Notifications.Any(n => n.ProjectId == p.Id && n.UserId == d.User(TestData.Diane) && n.EventType == NotificationEvents.AddedToProject)));
        } finally { await admin.Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }); }
    }

    [Fact]
    public async Task Duplicate_numbers_are_refused_in_any_case_with_a_link() // AC-PRJ-01
    {
        var p = await d.Project(activate: false);
        var r = await f.As(TestData.Pm).Post("/api/v1/projects", new { projectNumber = p.ProjectNumber.ToLowerInvariant(), name = "Again", clientId = await d.Client(), officeId = await d.Office() });
        var body = await r.Json(409);
        Assert.Equal("duplicate_project_number", body.S("code"));
        Assert.Equal(p.Id, body.G("existingProjectId"));
    }

    [Fact]
    public async Task Creator_is_primary_pm_member_and_creation_is_logged() // AC-PRJ-02, AC-TEAM-02, ASG-01
    {
        var p = await d.Project(activate: false);
        Assert.Equal(d.User(TestData.Pm), p.ProjectManagerId);
        Assert.Equal(ProjectStatus.Setup, p.Status);
        var (pmRoles, marcFollow, created, leadNote) = await f.DbAsync(async db => (
            await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == p.ProjectManagerId).Select(m => m.Roles).FirstAsync(),
            await db.Follows.FirstAsync(x => x.ProjectId == p.Id && x.UserId == d.User(TestData.Marc)),
            await db.ActivityLog.AnyAsync(a => a.ProjectId == p.Id && a.ItemType == ItemType.Project && a.Action == "Created"),
            await db.Notifications.AnyAsync(n => n.ProjectId == p.Id && n.UserId == d.User(TestData.Marc) && n.EventType == NotificationEvents.BecameDisciplineLead)));
        Assert.Contains(ProjectRole.PM, pmRoles);
        Assert.Equal(FollowLevel.AllActivity, marcFollow.Level);
        Assert.Equal(FollowSource.Assignment, marcFollow.Source);
        Assert.True(created);
        Assert.True(leadNote);
    }

    [Fact]
    public async Task Only_project_manager_role_holders_create_projects() // AC-AUTH-03
    {
        var r = await f.As(TestData.Alex).Post("/api/v1/projects", new { projectNumber = TestData.Number(), name = "x", clientId = await d.Client(), officeId = await d.Office() });
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
        var me = await f.As(TestData.Alex).GetAsync("/api/v1/me").Result.Json();
        Assert.False(me["capabilities"]!["createProject"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Lifecycle_with_reasons_closeout_archive_and_unarchive() // AC-PRJ-03..06, P-02, P-03
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var noReason = await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "On Hold", rowVersion = d.Version(p.Id) });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = d.Version(p.Id) }).Result.Json();
        var illegal = await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Complete", reason = "trying", rowVersion = d.Version(p.Id) });
        Assert.Equal("illegal_transition", (await illegal.Json(422)).S("code"));
        await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Active", rowVersion = d.Version(p.Id) }).Result.Json();
        var closeout = await pm.GetAsync($"/api/v1/projects/{p.Id}/closeout").Result.Json();
        Assert.Equal(0, closeout.I("openTasks"));
        await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Complete", reason = "All work issued", closeout = new { tasks = "cancel" }, rowVersion = d.Version(p.Id) }).Result.Json();

        var editNoReason = await pm.Patch($"/api/v1/projects/{p.Id}", new { location = "Hamilton" }, d.Version(p.Id));
        Assert.Equal(HttpStatusCode.BadRequest, editNoReason.StatusCode);
        (await pm.Patch($"/api/v1/projects/{p.Id}", new { location = "Hamilton", reason = "Correct the site name" }, d.Version(p.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch($"/api/v1/projects/{p.Id}", new { location = "X", reason = "not mine" }, d.Version(p.Id))).StatusCode);

        await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Archived", rowVersion = d.Version(p.Id) }).Result.Json();
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.Patch($"/api/v1/projects/{p.Id}", new { location = "Y", reason = "still editing" }, d.Version(p.Id))).StatusCode);
        var list = await pm.GetAsync("/api/v1/projects?mine=true").Result.Json();
        Assert.DoesNotContain(list["items"]!.AsArray(), x => x!.G("id") == p.Id);
        var all = await pm.GetAsync("/api/v1/projects?mine=true&includeArchived=true").Result.Json();
        Assert.Contains(all["items"]!.AsArray(), x => x!.G("id") == p.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Complete", rowVersion = d.Version(p.Id) })).StatusCode);
        await f.As(TestData.Admin).Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Complete", reason = "Correction needed", rowVersion = d.Version(p.Id) }).Result.Json();
        var reasons = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == p.Id && a.Action == "StatusChanged").Select(a => a.Reason).ToListAsync());
        Assert.Contains("Client paused the work", reasons);
        Assert.Contains("Correction needed", reasons);
    }

    [Fact]
    public async Task Read_only_and_discipline_leads_cannot_edit_project_information() // AC-PERM-04, §8.5.2
    {
        var p = await d.Project();
        var ro = await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}").Result.Json();
        Assert.False(ro["permissions"]!["edit"]!["ok"]!.GetValue<bool>());
        Assert.False(ro["permissions"]!["comment"]!["ok"]!.GetValue<bool>());
        var dl = await f.As(TestData.Marc).Patch($"/api/v1/projects/{p.Id}", new { name = "Renamed" }, d.Version(p.Id));
        var body = await dl.Json(403);
        Assert.Contains("Project Manager", body.S("detail"));
    }

    [Fact]
    public async Task Restricted_projects_are_invisible_to_non_members() // AC-PERM-05
    {
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        var p = await d.Project();
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{p.ProjectNumber}")).StatusCode);
        var list = await f.As(TestData.Jill).GetAsync($"/api/v1/projects?q={p.ProjectNumber}").Result.Json();
        Assert.Empty(list["items"]!.AsArray());
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Lena).GetAsync($"/api/v1/projects/{p.Id}")).StatusCode);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Team_guards_primary_pm_duplicates_and_disciplines_in_use() // AC-TEAM-04, TM-01, TM-05
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var team = await pm.GetAsync($"/api/v1/projects/{p.Id}/team").Result.Json();
        var pmMember = team["members"]!.AsArray().First(m => m!.G("userId") == p.ProjectManagerId)!;
        var r = await pm.DeleteAsync($"/api/v1/projects/{p.Id}/members/{pmMember.S("id")}");
        Assert.Equal("primary_pm", (await r.Json(422)).S("code"));

        await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Alex), roles = new[] { "Viewer" } }).Result.Json(201);
        var alexRows = await f.DbAsync(db => db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null).ToListAsync());
        Assert.Single(alexRows);
        Assert.Equal(["TeamMember", "Viewer"], alexRows[0].Roles.Order());

        var civil = d.ProjectDiscipline(p.Id, "Civil");
        (await pm.DeleteAsync($"/api/v1/projects/{p.Id}/disciplines/{civil}")).EnsureSuccessStatusCode(); // no work yet: removed or deactivated
    }

    [Fact]
    public async Task Reviewer_only_members_follow_my_items_only() // ASG-01
    {
        var p = await d.Project();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Diane), roles = new[] { "Reviewer" } }).Result.Json(201);
        var follow = await f.DbAsync(db => db.Follows.FirstAsync(x => x.ProjectId == p.Id && x.UserId == d.User(TestData.Diane)));
        Assert.Equal(FollowLevel.MyItemsOnly, follow.Level);
    }

    [Fact]
    public async Task Supervisors_staff_their_direct_reports_only() // AC-ASG-09, ASG-10
    {
        var p = await d.Project();
        (await f.As(TestData.Sam).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Jill), roles = new[] { "TeamMember" } })).EnsureSuccessStatusCode();
        var pmNotice = await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == p.ProjectManagerId && n.EventType == NotificationEvents.SupervisorStaffing && n.ProjectId == p.Id));
        Assert.True(pmNotice);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Diane), roles = new[] { "TeamMember" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Jill), roles = new[] { "PM" } })).StatusCode);
    }

    [Fact]
    public async Task Supervisor_reassigns_only_to_a_project_member_or_direct_report()
    {
        var p = await d.Project();
        var source = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null));
        var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });

        var outsider = await f.As(TestData.Sam).DeleteAsync($"/api/v1/projects/{p.Id}/members/{source.Id}?reassignTo={d.User(TestData.Diane)}&reason=Staffing%20change");
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Assert.Equal(d.User(TestData.Alex), await f.DbAsync(db => db.Tasks.Where(t => t.Id == task.G("id")).Select(t => t.AssigneeId).SingleAsync()));

        var directReport = await f.As(TestData.Sam).DeleteAsync($"/api/v1/projects/{p.Id}/members/{source.Id}?reassignTo={d.User(TestData.Jill)}&reason=Staffing%20change");
        Assert.Equal(HttpStatusCode.OK, directReport.StatusCode);
        Assert.Equal(d.User(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == task.G("id")).Select(t => t.AssigneeId).SingleAsync()));
        Assert.True(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Jill) && m.RemovedAt == null)));
    }

    [Fact]
    public async Task Project_number_changes_are_admin_only() // P-07
    {
        var p = await d.Project();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { projectNumber = TestData.Number() }, d.Version(p.Id))).StatusCode);
        (await f.As(TestData.Admin).Patch($"/api/v1/projects/{p.Id}", new { projectNumber = TestData.Number() }, d.Version(p.Id))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Changing_the_primary_pm_keeps_the_old_pm_as_team_member() // FR-016, E-02
    {
        var p = await d.Project();
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { projectManagerId = d.User(TestData.Marc) }, d.Version(p.Id))).EnsureSuccessStatusCode();
        var (marc, priya) = await f.DbAsync(async db => (
            await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Marc) && m.RemovedAt == null).Select(m => m.Roles).FirstAsync(),
            await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Pm) && m.RemovedAt == null).Select(m => m.Roles).FirstAsync()));
        Assert.Contains(ProjectRole.PM, marc);
        Assert.Equal([ProjectRole.TeamMember], priya);
    }

    [Fact]
    public async Task A_resumed_project_lists_the_work_whose_dates_passed_while_on_hold() // E-05 (Rec)
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var saved = f.Clock.Now;
        try
        {
            var during = await d.NewTask(p.Id, TestData.Marc, new { name = "Due during the hold", dueDate = "2026-09-20" });
            await d.NewTask(p.Id, TestData.Marc, new { name = "Due after it", dueDate = "2026-11-30" });
            var del = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new
            {
                name = "30% package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-25",
            }).Result.Json(201);
            await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = d.Version(p.Id) }).Result.Json();
            Assert.Null((await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json())["window"]); // nothing while it is on hold

            f.Clock.SetDate(2026, 10, 5);
            await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Active", reason = "Client resumed the work", rowVersion = d.Version(p.Id) }).Result.Json();
            var r = await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json();
            Assert.Equal(("2026-09-14", "2026-10-05", 21), (r["window"]!.S("heldFrom"), r["window"]!.S("resumedOn"), r["window"]!.I("days")));
            Assert.Equal(new[] { during.S("key") }, r["tasks"]!.AsArray().Select(x => x!.S("key")));
            Assert.Equal(new[] { del.S("key") }, r["deliverables"]!.AsArray().Select(x => x!.S("key")));

            (await pm.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { during.G("id") }, operation = "shiftDueDates", @params = new { days = 21 }, reason = "Resumed after the hold" })).EnsureSuccessStatusCode();
            (await pm.Post($"/api/v1/projects/{p.Id}/deliverables/bulk", new { ids = new[] { del.G("id") }, operation = "shiftDueDates", @params = new { days = 21 }, reason = "Resumed after the hold" })).EnsureSuccessStatusCode();
            var after = await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json();
            Assert.Equal((0, 0), (after["tasks"]!.AsArray().Count, after["deliverables"]!.AsArray().Count)); // shifted out of the hold window

            f.Clock.SetDate(2026, 11, 20);
            Assert.Null((await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json())["window"]); // the banner's time has passed
        }
        finally { f.Clock.Now = saved; }
    }
}
