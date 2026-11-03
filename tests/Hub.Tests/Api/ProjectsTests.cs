using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 002: AC-PRJ-01..06, AC-PERM-04/05, AC-TEAM-02/04, TM-01, TM-05, P-07, ASG-01, ASG-10.
[Collection("api")]
public sealed class ProjectsTests(HubFactory f, Xunit.Abstractions.ITestOutputHelper output)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task Home_empty_project_cohort_stays_empty_in_its_drilldown()
    {
        var project = await d.Project();
        var admin = f.As(TestData.Admin);
        try
        {
            (await admin.Put("/api/v1/admin/settings/flag.home_overview", new { value = true })).EnsureSuccessStatusCode();
            var empty = await (await f.As(TestData.Pm).GetAsync("/api/v1/projects?mine=false&status=Active&ids=none")).Json();
            Assert.Empty(empty["items"]!.AsArray());
            var one = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects?mine=false&status=Active&ids={project.Id}")).Json();
            Assert.Equal(project.Id, Assert.Single(one["items"]!.AsArray())!.G("id"));
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.home_overview", new { value = false })).EnsureSuccessStatusCode(); }
    }

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
        // Narrowed to this project: the run creates more than one 200-row page of projects, in random number order.
        var list = await pm.GetAsync($"/api/v1/projects?mine=true&ids={p.Id}").Result.Json();
        Assert.DoesNotContain(list["items"]!.AsArray(), x => x!.G("id") == p.Id);
        var all = await pm.GetAsync($"/api/v1/projects?mine=true&includeArchived=true&ids={p.Id}").Result.Json();
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
        var r = await pm.Delete($"/api/v1/projects/{p.Id}/members/{pmMember.S("id")}", pmMember.I("rowVersion"));
        Assert.Equal("primary_pm", (await r.Json(422)).S("code"));

        await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Alex), roles = new[] { "Viewer" } }).Result.Json(201);
        var alexRows = await f.DbAsync(db => db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null).ToListAsync());
        Assert.Single(alexRows);
        Assert.Equal(["TeamMember", "Viewer"], alexRows[0].Roles.Order());

        var civil = d.ProjectDiscipline(p.Id, "Civil");
        (await pm.Delete($"/api/v1/projects/{p.Id}/disciplines/{civil}", f.Db(db => db.ProjectDisciplines.Single(d => d.Id == civil).RowVersion))).EnsureSuccessStatusCode(); // no work yet: removed or deactivated
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

        var outsider = await f.As(TestData.Sam).Delete($"/api/v1/projects/{p.Id}/members/{source.Id}?reassignTo={d.User(TestData.Diane)}&reason=Staffing%20change", source.RowVersion);
        Assert.Equal(HttpStatusCode.Forbidden, outsider.StatusCode);
        Assert.Equal(d.User(TestData.Alex), await f.DbAsync(db => db.Tasks.Where(t => t.Id == task.G("id")).Select(t => t.AssigneeId).SingleAsync()));

        var directReport = await f.As(TestData.Sam).Delete($"/api/v1/projects/{p.Id}/members/{source.Id}?reassignTo={d.User(TestData.Jill)}&reason=Staffing%20change", source.RowVersion);
        Assert.Equal(HttpStatusCode.OK, directReport.StatusCode);
        Assert.Equal(d.User(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == task.G("id")).Select(t => t.AssigneeId).SingleAsync()));
        Assert.True(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Jill) && m.RemovedAt == null)));
    }

    [Fact]
    public async Task A_member_s_primary_discipline_must_belong_to_the_project()
    {
        var p = await d.Project();
        var other = await d.Project();
        var alex = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null));
        var refused = await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/members/{alex.Id}", new { primaryDisciplineId = d.ProjectDiscipline(other.Id, "Electrical") }, alex.RowVersion);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(alex.PrimaryDisciplineId, await f.DbAsync(db => db.ProjectMembers.Where(m => m.Id == alex.Id).Select(m => m.PrimaryDisciplineId).SingleAsync()));
        alex = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.Id == alex.Id));
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/members/{alex.Id}", new { primaryDisciplineId = d.ProjectDiscipline(p.Id, "Electrical") }, alex.RowVersion)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Member_changes_reject_a_stale_version() // G-07, §25.8
    {
        var p = await d.Project();
        var member = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null));
        var pm = f.As(TestData.Pm);

        var stale = await pm.Patch($"/api/v1/projects/{p.Id}/members/{member.Id}", new { roles = new[] { ProjectRole.TeamMember, ProjectRole.Reviewer } }, member.RowVersion - 1);
        var problem = await stale.Json(409);
        Assert.Equal("concurrency_conflict", problem.S("code"));
        Assert.Equal([ProjectRole.TeamMember], await f.DbAsync(db => db.ProjectMembers.Where(m => m.Id == member.Id).Select(m => m.Roles).SingleAsync()));

        var updated = await pm.Patch($"/api/v1/projects/{p.Id}/members/{member.Id}", new { roles = new[] { ProjectRole.TeamMember, ProjectRole.Reviewer } }, member.RowVersion);
        updated.EnsureSuccessStatusCode();
        var current = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.Id == member.Id));
        Assert.True(current.RowVersion > member.RowVersion);
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Delete($"/api/v1/projects/{p.Id}/members/{member.Id}", member.RowVersion)).StatusCode);
    }

    [Fact]
    public async Task Team_read_keeps_its_property_names_order_nulls_and_nesting() // the typed records must serialise to the bytes the app already reads (040-S15-Team)
    {
        static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");

        // The project's manager: the whole envelope, nulls written and not omitted, no reason to refuse.
        var team = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/team").Result.Json();
        Assert.Equal(["disciplines", "members", "canManage", "manageReason"], Keys(team));
        Assert.True(team["canManage"]!.GetValue<bool>());
        Assert.True(team.AsObject().ContainsKey("manageReason")); Assert.Null(team["manageReason"]);

        var disciplines = team["disciplines"]!.AsArray();
        Assert.Equal([civil, electrical], disciplines.Select(x => x!.G("id"))); // by sort order
        Assert.All(disciplines, x => Assert.Equal(["id", "disciplineId", "name", "code", "colour", "leadUserId", "isActive", "sortOrder", "rowVersion"], Keys(x)));
        Assert.Equal(("Civil", d.User(TestData.Marc), true, 0), (disciplines[0]!.S("name"), disciplines[0]!.G("leadUserId"), disciplines[0]!["isActive"]!.GetValue<bool>(), disciplines[0]!.I("sortOrder")));
        Assert.Equal(("Electrical", d.User(TestData.Omar), 1), (disciplines[1]!.S("name"), disciplines[1]!.G("leadUserId"), disciplines[1]!.I("sortOrder")));
        Assert.Equal(await d.Discipline("Civil"), disciplines[0]!.G("disciplineId"));
        Assert.StartsWith("#", disciplines[0]!.S("colour"));
        Assert.False(string.IsNullOrEmpty(disciplines[0]!.S("code")));
        Assert.Equal(f.Db(db => db.ProjectDisciplines.Single(x => x.Id == civil).RowVersion), disciplines[0]!.I("rowVersion"));

        var members = team["members"]!.AsArray();
        Assert.Equal(new[] { TestData.Alex, TestData.Marc, TestData.Omar, TestData.Pm }.Select(e => d.User(e)).Order(), members.Select(x => x!.G("userId")).Order());
        Assert.Equal(members.Select(x => x!.S("displayName")).Order(StringComparer.Ordinal), members.Select(x => x!.S("displayName"))); // ordered by name
        Assert.All(members, x => Assert.Equal(["id", "userId", "displayName", "email", "jobTitle", "officeId", "isActive", "roles", "primaryDisciplineId", "addedAt", "addedBy", "rowVersion", "leadOf", "isPrimaryPm", "canStaff"], Keys(x)));
        var pm = members.Single(x => x!.G("userId") == p.ProjectManagerId)!;
        Assert.True(pm["isPrimaryPm"]!.GetValue<bool>());
        Assert.Contains("PM", pm["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Empty(pm["leadOf"]!.AsArray());
        Assert.Null(pm["primaryDisciplineId"]);
        Assert.True(pm["isActive"]!.GetValue<bool>());
        var alex = members.Single(x => x!.G("userId") == d.User(TestData.Alex))!;
        Assert.Equal(["TeamMember"], alex["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(civil, alex.G("primaryDisciplineId"));
        Assert.False(alex["isPrimaryPm"]!.GetValue<bool>());
        Assert.Equal(("alex@hub.test", "Alex Chen"), (alex.S("email"), alex.S("displayName")));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", alex.S("addedAt"));
        Assert.Equal(f.Db(db => db.ProjectMembers.Single(x => x.Id == alex.G("id")).RowVersion), alex.I("rowVersion"));
        Assert.Equal([civil], members.Single(x => x!.G("userId") == d.User(TestData.Marc))!["leadOf"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())));
        Assert.All(new[] { "jobTitle", "officeId", "addedBy" }, k => Assert.True(alex.AsObject().ContainsKey(k)));
        Assert.All(members, x => Assert.True(x!["canStaff"]!.GetValue<bool>() is true or false));

        // A member who cannot manage the team: the same envelope, the refusal text in place of the null.
        var asAlex = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/team").Result.Json();
        Assert.Equal(["disciplines", "members", "canManage", "manageReason"], Keys(asAlex));
        Assert.False(asAlex["canManage"]!.GetValue<bool>());
        Assert.Equal(Hub.Api.Text.Get("perm.pm", p.Status), asAlex.S("manageReason"));
        Assert.Equal(members.Count, asAlex["members"]!.AsArray().Count);

        // A supervisor of a team member may staff that report (canStaff) without managing the team.
        var asSam = await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{p.Id}/team").Result.Json();
        Assert.False(asSam["canManage"]!.GetValue<bool>());
        Assert.True(asSam["members"]!.AsArray().Single(x => x!.G("userId") == d.User(TestData.Alex))!["canStaff"]!.GetValue<bool>());
        Assert.False(asSam["members"]!.AsArray().Single(x => x!.G("userId") == p.ProjectManagerId)!["canStaff"]!.GetValue<bool>());

        // A project nobody can see: 404, not an empty team.
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{Guid.NewGuid()}/team")).StatusCode);
    }

    [Fact]
    public async Task Member_commands_and_open_items_keep_their_answers_and_shape() // 040-S15-Team
    {
        static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var civil = d.ProjectDiscipline(p.Id, "Civil");

        // Adding: 201 with a Location, the new id and its row version.
        var added = await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Diane), roles = new[] { "Reviewer" }, primaryDisciplineId = civil });
        var diane = await added.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(diane));
        Assert.Equal($"/api/v1/projects/{p.Id}/members/{diane.S("id")}", added.Headers.Location!.OriginalString);
        Assert.Equal(f.Db(db => db.ProjectMembers.Single(x => x.Id == diane.G("id")).RowVersion), diane.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Jill), roles = new[] { "Viewer" } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = Guid.NewGuid(), roles = new[] { "Viewer" } })).StatusCode);

        // Changing: 204 with an empty body; a missing member is a 404 and a person who cannot manage the team a 403.
        var changed = await pm.Patch($"/api/v1/projects/{p.Id}/members/{diane.S("id")}", new { roles = new[] { "Reviewer", "Viewer" }, primaryDisciplineId = (Guid?)null }, diane.I("rowVersion"));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode); Assert.Empty(await changed.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/projects/{p.Id}/members/{Guid.NewGuid()}", new { roles = new[] { "Viewer" } })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/projects/{p.Id}/members/{diane.S("id")}", new { roles = new[] { "Viewer" } }, diane.I("rowVersion") + 1)).StatusCode);

        // Open items: tasks the person owns or reviews and deliverables they own, none finished; the answer's own order is tasks, deliverables, count.
        var alexMember = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null));
        var owned = await d.NewTask(p.Id, TestData.Pm, new { name = "Open pin owned", assigneeId = d.User(TestData.Alex), dueDate = "2026-10-09" });
        var reviewed = await d.NewTask(p.Id, TestData.Pm, new { name = "Open pin reviewed", assigneeId = d.User(TestData.Marc), requiresReview = true, reviewerId = d.User(TestData.Alex) });
        var finished = await d.NewTask(p.Id, TestData.Pm, new { name = "Open pin finished", assigneeId = d.User(TestData.Alex) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(x => x.Id == finished.G("id"))).Status = TaskStatuses.Complete; return await db.SaveChangesAsync(); });
        var deliverable = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Open pin report", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), dueDate = "2026-10-16" }).Result.Json(201);
        var items = await pm.GetAsync($"/api/v1/projects/{p.Id}/members/{alexMember.Id}/open-items").Result.Json();
        Assert.Equal(["tasks", "deliverables", "count"], Keys(items));
        Assert.Equal(3, items.I("count"));
        var tasks = items["tasks"]!.AsArray();
        Assert.Equal(2, tasks.Count);
        Assert.All(tasks, x => Assert.Equal(["id", "key", "name", "status", "dueDate", "role"], Keys(x)));
        var asAssignee = tasks.Single(x => x!.G("id") == owned.G("id"))!;
        Assert.Equal((owned.S("key"), "Open pin owned", TaskStatuses.NotStarted, "2026-10-09", "Assignee"), (asAssignee.S("key"), asAssignee.S("name"), asAssignee.S("status"), asAssignee.S("dueDate"), asAssignee.S("role")));
        var asReviewer = tasks.Single(x => x!.G("id") == reviewed.G("id"))!;
        Assert.Equal(("Reviewer", "Open pin reviewed"), (asReviewer.S("role"), asReviewer.S("name")));
        Assert.True(asReviewer.AsObject().ContainsKey("dueDate")); Assert.Null(asReviewer["dueDate"]);
        var owns = Assert.Single(items["deliverables"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "status", "dueDate"], Keys(owns));
        Assert.Equal((deliverable.G("id"), "Open pin report", DeliverableStatus.NotStarted, "2026-10-16"), (owns.G("id"), owns.S("name"), owns.S("status"), owns.S("dueDate")));
        var none = await pm.GetAsync($"/api/v1/projects/{p.Id}/members/{diane.S("id")}/open-items").Result.Json();
        Assert.Equal(["tasks", "deliverables", "count"], Keys(none));
        Assert.Empty(none["tasks"]!.AsArray()); Assert.Empty(none["deliverables"]!.AsArray()); Assert.Equal(0, none.I("count"));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{p.Id}/members/{Guid.NewGuid()}/open-items")).StatusCode);

        // Removing: 200 with how many open items moved, and a stale version is a conflict.
        var stale = await pm.Delete($"/api/v1/projects/{p.Id}/members/{alexMember.Id}?reassignTo={d.User(TestData.Marc)}&reason=Moved", alexMember.RowVersion - 1);
        Assert.Equal("concurrency_conflict", (await stale.Json(409)).S("code"));
        var moved = await pm.Delete($"/api/v1/projects/{p.Id}/members/{alexMember.Id}?reassignTo={d.User(TestData.Marc)}&reason=Moved", alexMember.RowVersion).Result.Json(200);
        Assert.Equal(["reassigned"], Keys(moved));
        Assert.Equal(3, moved.I("reassigned")); // one task as assignee, one as reviewer and the deliverable
        var left = await pm.Delete($"/api/v1/projects/{p.Id}/members/{diane.S("id")}", f.Db(db => db.ProjectMembers.Single(x => x.Id == diane.G("id")).RowVersion)).Result.Json(200);
        Assert.Equal(["reassigned"], Keys(left)); Assert.Equal(0, left.I("reassigned"));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Delete($"/api/v1/projects/{p.Id}/members/{diane.S("id")}", 1)).StatusCode);
    }

    [Fact]
    public async Task Project_discipline_commands_answer_with_their_ids_versions_and_refusals() // POST, PATCH and DELETE were uncalled (040-S15-Team)
    {
        static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var structural = await d.Discipline("Structural");
        var geotechnical = await d.Discipline("Geotechnical");

        // Adding a discipline the project lacks: 201 with a Location, the id and the version; the lead joins the team.
        var made = await pm.Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = structural, leadUserId = d.User(TestData.Jill) });
        var added = await made.Json(201);
        Assert.Equal(["id", "rowVersion"], Keys(added));
        Assert.Equal($"/api/v1/projects/{p.Id}/disciplines/{added.S("id")}", made.Headers.Location!.OriginalString);
        var row = await f.DbAsync(db => db.ProjectDisciplines.SingleAsync(x => x.Id == added.G("id")));
        Assert.Equal((structural, d.User(TestData.Jill), 2, true, row.RowVersion), (row.DisciplineId, row.LeadUserId, row.SortOrder, row.IsActive, added.I("rowVersion")));
        Assert.True(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Jill) && m.RemovedAt == null)));

        // Adding it again reactivates the one row: 200, the same two properties, no Location, no second row.
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(x => x.Id == added.G("id"))).IsActive = false; return await db.SaveChangesAsync(); });
        var again = await pm.Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = structural });
        var reactivated = await again.Json(200);
        Assert.Equal(["id", "rowVersion"], Keys(reactivated));
        Assert.Equal(added.G("id"), reactivated.G("id")); Assert.Null(again.Headers.Location);
        Assert.True(reactivated.I("rowVersion") > added.I("rowVersion"));
        Assert.Equal(1, await f.DbAsync(db => db.ProjectDisciplines.CountAsync(x => x.ProjectId == p.Id && x.DisciplineId == structural)));
        Assert.True(await f.DbAsync(db => db.ProjectDisciplines.Where(x => x.Id == added.G("id")).Select(x => x.IsActive).SingleAsync()));

        // Refusals: a person who cannot manage the team, an unknown discipline and a missing project.
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = geotechnical })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/projects/{Guid.NewGuid()}/disciplines", new { disciplineId = geotechnical })).StatusCode);

        // Changing: 204 with an empty body; the lead, the active flag and the order all move; stale and missing are refused.
        var version = f.Db(db => db.ProjectDisciplines.Single(x => x.Id == added.G("id")).RowVersion);
        var patched = await pm.Patch($"/api/v1/projects/{p.Id}/disciplines/{added.S("id")}", new { leadUserId = d.User(TestData.Omar), isActive = false, sortOrder = 5 }, version);
        Assert.Equal(HttpStatusCode.NoContent, patched.StatusCode); Assert.Empty(await patched.Content.ReadAsStringAsync());
        var after = await f.DbAsync(db => db.ProjectDisciplines.SingleAsync(x => x.Id == added.G("id")));
        Assert.Equal((d.User(TestData.Omar), false, 5), (after.LeadUserId, after.IsActive, after.SortOrder));
        Assert.Equal("concurrency_conflict", (await (await pm.Patch($"/api/v1/projects/{p.Id}/disciplines/{added.S("id")}", new { isActive = true }, version)).Json(409)).S("code"));
        var cleared = await pm.Patch($"/api/v1/projects/{p.Id}/disciplines/{added.S("id")}", new { leadUserId = (Guid?)null }, after.RowVersion);
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Null(await f.DbAsync(db => db.ProjectDisciplines.Where(x => x.Id == added.G("id")).Select(x => x.LeadUserId).SingleAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/projects/{p.Id}/disciplines/{Guid.NewGuid()}", new { isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/projects/{p.Id}/disciplines/{added.S("id")}", new { isActive = true }, 1)).StatusCode);

        // Removing: a discipline with work is refused; one nothing names is deleted with a 204 and an empty body; one a member names is deactivated.
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        await d.NewTask(p.Id, TestData.Pm, new { name = "Discipline pin work" });
        var civilVersion = f.Db(db => db.ProjectDisciplines.Single(x => x.Id == civil).RowVersion);
        Assert.Equal("discipline_in_use", (await (await pm.Delete($"/api/v1/projects/{p.Id}/disciplines/{civil}", civilVersion)).Json(422)).S("code"));
        var unused = await pm.Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = geotechnical }).Result.Json(201);
        var deleted = await pm.Delete($"/api/v1/projects/{p.Id}/disciplines/{unused.S("id")}", unused.I("rowVersion"));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); Assert.Empty(await deleted.Content.ReadAsStringAsync());
        Assert.False(await f.DbAsync(db => db.ProjectDisciplines.AnyAsync(x => x.Id == unused.G("id"))));
        var named = await pm.Post($"/api/v1/projects/{p.Id}/disciplines", new { disciplineId = await d.Discipline("Survey") }).Result.Json(201);
        var alex = await f.DbAsync(db => db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex) && m.RemovedAt == null));
        (await pm.Patch($"/api/v1/projects/{p.Id}/members/{alex.Id}", new { primaryDisciplineId = named.G("id") }, alex.RowVersion)).EnsureSuccessStatusCode();
        var kept = await pm.Delete($"/api/v1/projects/{p.Id}/disciplines/{named.S("id")}", f.Db(db => db.ProjectDisciplines.Single(x => x.Id == named.G("id")).RowVersion));
        Assert.Equal(HttpStatusCode.NoContent, kept.StatusCode); Assert.Empty(await kept.Content.ReadAsStringAsync());
        Assert.False(await f.DbAsync(db => db.ProjectDisciplines.Where(x => x.Id == named.G("id")).Select(x => x.IsActive).SingleAsync()));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Delete($"/api/v1/projects/{p.Id}/disciplines/{Guid.NewGuid()}", 1)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Delete($"/api/v1/projects/{p.Id}/disciplines/{civil}", civilVersion)).StatusCode);
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

    /// AC-FIG-08 for decisions 1, 2, 3, 5, 6, 9 and 10 of §45.5: each project-level flag equals its endpoint's decision for Admin,
    /// project PM, Discipline Lead, Team Member, Read Only and Executive on every project status. Every case where today's client
    /// rule differs is one the client offered and the endpoint refuses, and the page keeps it visible, disabled (FR-FIG-09, D5).
    [Fact]
    public async Task Project_permission_flags_match_their_endpoints_for_every_role_and_status()
    {
        string[] users = [TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Rita, TestData.Lena];
        var mismatches = new List<string>();
        var differences = new List<string>();
        foreach (var status in ProjectStatus.All)
        {
            var p = await d.Project(activate: status != ProjectStatus.Setup);
            await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = d.User(TestData.Rita), roles = new[] { "TeamMember" } }); // kept if allowed
            var party = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/external-parties", new { name = "Karen Li" }).Result.Json(201);
            if (status != ProjectStatus.Setup && status != ProjectStatus.Active)
                await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Status = status; return await db.SaveChangesAsync(); });
            var day = Weekday.Parse(p.CoordinationDay);
            var week = Enumerable.Range(0, 7).Select(i => new DateOnly(2026, 10, 5).AddDays(i)).First(x => x.DayOfWeek == day);
            foreach (var user in users)
            {
                var c = f.As(user);
                var perms = (await c.GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["permissions"]!;
                bool Flag(string name) => perms[name]!["ok"]!.GetValue<bool>();
                async Task<bool> Refused(Task<HttpResponseMessage> call) => (await call).StatusCode == HttpStatusCode.Forbidden;
                // Each call passes the permission check only to fail validation after it, so nothing is written.
                // Evaluation may clear attention rows it did not raise, so a fresh row is added just before each call.
                var attention = await f.DbAsync(async db =>
                {
                    var a = new Hub.Api.Data.AttentionItem { ProjectId = p.Id, RuleId = "ATT-TEST", ItemType = ItemType.Project, ItemId = Guid.NewGuid(), Message = "Parity",
                        FirstDetectedAt = DateTimeOffset.UtcNow, LastEvaluatedAt = DateTimeOffset.UtcNow };
                    db.Attention.Add(a);
                    await db.SaveChangesAsync();
                    return a.Id;
                });
                var endpoint = new Dictionary<string, bool>
                {
                    ["proposeAllocation"] = !await Refused(c.Post($"/api/v1/projects/{p.Id}/allocations", new { requestId = Guid.NewGuid(), personId = Guid.Empty,
                        purpose = "Production", fromDate = week, throughDate = week, plannedHours = 0, days = Array.Empty<object>(), links = Array.Empty<object>() })),
                    ["snooze"] = !await Refused(c.Post($"/api/v1/attention/{attention}/snooze", new { days = 0, note = "" })),
                    ["manageExternalParties"] = !await Refused(c.Patch($"/api/v1/external-parties/{party.S("id")}", new { name = "" })),
                    ["reviewDates"] = !await Refused(c.GetAsync($"/api/v1/projects/{p.Id}/date-review")),
                    ["captureWeeklyPlan"] = !await Refused(c.Post($"/api/v1/projects/{p.Id}/weekly-commitments/snapshot", new { requestId = Guid.NewGuid(), weekStart = week, reason = "" })),
                    ["createSubmission"] = !await Refused(c.Post($"/api/v1/projects/{p.Id}/submissions", new { requestId = Guid.NewGuid(), title = "", purpose = "", recipientReference = "",
                        coordinatorId = Guid.Empty, milestoneId = Guid.Empty, targetDate = week, manifest = Array.Empty<object>(), optionalChecks = Array.Empty<object>() })),
                };
                foreach (var (name, allowed) in endpoint)
                    if (Flag(name) != allowed) mismatches.Add($"{user} {status} {name}: flag {Flag(name)}, endpoint {allowed}");

                // Today's client rules (Allocations.tsx, Dashboard.tsx, Decisions.tsx, ProjectLayout.tsx, Readiness.tsx, Submissions.tsx).
                var isPm = perms["isPm"]!.GetValue<bool>();
                var lead = perms["leadOf"]!.AsArray().Count > 0;
                var options = await c.GetAsync($"/api/v1/projects/{p.Id}/changes/options");
                var o = options.IsSuccessStatusCode ? await options.Json() : null;
                var manages = o?["manageDisciplineIds"]!.AsArray().Count > 0;
                var client = new Dictionary<string, bool>
                {
                    ["proposeAllocation"] = isPm || manages, ["snooze"] = isPm || lead, ["manageExternalParties"] = isPm && Flag("edit"),
                    ["reviewDates"] = isPm || lead, ["captureWeeklyPlan"] = isPm && o?["canWrite"]!.GetValue<bool>() == true, ["createSubmission"] = isPm || manages,
                };
                foreach (var (name, offered) in client)
                    if (offered != Flag(name))
                    {
                        differences.Add($"{user} {status} {name}: client {offered}, flag {Flag(name)} ({perms[name]!["reason"]})");
                        // The page keeps such a control visible and disabled: the client offered it, and the account or status refuses it.
                        Assert.True(offered && (user == TestData.Rita || status is ProjectStatus.Complete or ProjectStatus.Archived or ProjectStatus.Cancelled),
                            differences[^1]);
                    }
            }
        }
        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
        output.WriteLine("Legacy client differences (all endpoint-refused):\n" + string.Join("\n", differences));
        Assert.NotEmpty(differences); // e.g. the PM on an Archived project, which today's client offers and every endpoint refuses
    }
}
