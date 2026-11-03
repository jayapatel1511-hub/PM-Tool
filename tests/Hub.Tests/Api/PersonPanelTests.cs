using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S43 normal-command permissions, narrow reads, membership-note versions and independent dark flags.
public sealed class PersonPanelTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public PersonPanelTests() { d = new(f); f.Clock.SetDate(2026, 10, 31); }
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    Guid User(string actor) => d.User(actor);
    string Panel(Guid person, string query = "") => $"/api/v1/people/{person}/panel{query}";
    string Note(Guid project, Guid member) => $"/api/v1/projects/{project}/members/{member}/responsibility";
    Guid Member(Guid project, string actor = TestData.Alex) => f.Db(db => db.ProjectMembers.Single(m => m.ProjectId == project && m.UserId == User(actor) && m.RemovedAt == null).Id);
    int Version(Guid member) => f.Db(db => db.ProjectMembers.Single(m => m.Id == member).RowVersion);
    async Task<JsonNode> Read(Guid person, string actor = TestData.Pm, string query = "") => await (await f.As(actor).GetAsync(Panel(person, query))).Json();
    async Task<JsonNode> Team(Guid project, string actor = TestData.Pm) => await (await f.As(actor).GetAsync($"/api/v1/projects/{project}/team")).Json();
    static JsonNode TeamMember(JsonNode read, Guid member) => read["members"]!.AsArray().Single(row => row!.G("id") == member)!;
    Task<string> State() => f.DbAsync(async db => JsonSerializer.Serialize(new {
        Members = await db.ProjectMembers.AsNoTracking().OrderBy(m => m.Id).Select(m => new { m.Id, m.Roles, m.PrimaryDisciplineId, m.RemovedAt, m.RowVersion, m.ResponsibilityNote }).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Dates = await db.PersonDateVersions.CountAsync(), Notifications = await db.Notifications.CountAsync()
    }));

    [Fact]
    public async Task Dark_endpoints_refuse_without_mutation_and_Team_omits_the_additive_note()
    {
        var project = await d.Project(); var member = Member(project.Id); await Flag("people_screens", false); var before = await State();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync(Panel(User(TestData.Alex)))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).Patch(Note(project.Id, member), new { responsibilityNote = "checker" }, Version(member))).StatusCode);
        Assert.False(TeamMember(await Team(project.Id), member).AsObject().ContainsKey("responsibility")); Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Rita_sees_only_current_visible_memberships_and_no_contact_or_private_work_facts()
    {
        var visible = await d.Project(); var hidden = await d.Project(); var alex = User(TestData.Alex); await Flag("people_screens");
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = "Restricted" }, d.Version(hidden.Id))).Json();
        var before = await State(); var read = await Read(alex, TestData.Rita);
        Assert.Equal(alex, read.G("id")); Assert.Equal(1, read["projects"]!.I("total")); Assert.Equal(visible.Id, Assert.Single(read["projects"]!["items"]!.AsArray())!.G("projectId"));
        Assert.NotNull(read["organisationFacts"]); foreach (var field in new[] { "email", "weeklyCapacityHours", "allocations", "tasks", "approvedLeave", "personFacts" }) Assert.False(read.AsObject().ContainsKey(field));
        Assert.DoesNotContain(hidden.ProjectNumber, read.ToJsonString()); Assert.Equal(before, await State());
        var member = Member(visible.Id); await (await f.As(TestData.Pm).Delete($"/api/v1/projects/{visible.Id}/members/{member}", Version(member))).Json();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Panel(alex))).StatusCode);
        Assert.Equal(hidden.Id, Assert.Single((await Read(alex, TestData.Alex))["projects"]!["items"]!.AsArray())!.G("projectId"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Panel(Guid.NewGuid()))).StatusCode);
    }

    [Fact]
    public async Task Explicit_Staff_context_must_contain_target_and_rejects_unknown_or_disabled_scope()
    {
        var alex = User(TestData.Alex); await Flag("people_screens");
        Assert.Equal(alex, (await Read(alex, TestData.Sam, "?staffScope=direct")).G("id"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync(Panel(User(TestData.Diane), "?staffScope=direct"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).GetAsync(Panel(alex, "?staffScope=all"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync(Panel(alex, "?staffScope=surprise"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync(Panel(alex, "?staffScope=reports"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync(Panel(alex, $"?staffScope=unit:{Guid.NewGuid()}"))).StatusCode);
        await d.Project(); // An ordinary project-visible panel remains allowed; an explicit invalid Staff context does not inherit it.
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Panel(alex, "?staffScope=direct"))).StatusCode);
        Assert.Equal(alex, (await Read(alex, TestData.Rita)).G("id"));
    }

    [Fact]
    public async Task Unit_leader_uses_current_placement_and_unit_authority_disappears_with_org_flag()
    {
        await Flag("people_screens"); await Flag("org_structure"); var admin = f.As(TestData.Admin); var alex = User(TestData.Alex);
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray(); var level = levels.First(row => row!.S("name") == "Team")!.G("id");
        var unit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic panel unit", levelId = level, leaderId = User(TestData.Jill) })).Json(201);
        await (await admin.Patch($"/api/v1/admin/organisation/users/{alex}/placement", new { unitId = unit.G("id") }, f.Db(db => db.Users.Single(u => u.Id == alex).RowVersion))).Json(204);
        var query = $"?staffScope=unit:{unit.G("id")}";
        Assert.Equal(unit.G("id"), (await Read(alex, TestData.Jill, query))["organisationFacts"]!["unit"]!.G("id"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync(Panel(alex, query))).StatusCode);
        await Flag("org_structure", false);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Jill).GetAsync(Panel(alex, query))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync(Panel(alex))).StatusCode);
    }

    [Fact]
    public async Task Visible_membership_paging_is_stable_capped_and_keeps_empty_out_of_range_distinct()
    {
        await Flag("people_screens"); for (var i = 0; i < 3; i++) await d.Project(); var alex = User(TestData.Alex); var before = await State();
        var all = await Read(alex); Assert.Equal(25, all["projects"]!.I("pageSize")); Assert.Equal(3, all["projects"]!.I("total"));
        var ordered = all["projects"]!["items"]!.AsArray().Select(row => row!.G("projectId")).ToArray();
        var second = await Read(alex, query: "?projectsPage=2&projectsPageSize=1"); Assert.Equal(ordered[1], Assert.Single(second["projects"]!["items"]!.AsArray())!.G("projectId"));
        var max = await Read(alex, query: "?projectsPage=2147483647&projectsPageSize=1000"); Assert.Equal(100, max["projects"]!.I("pageSize")); Assert.Equal(3, max["projects"]!.I("total")); Assert.Empty(max["projects"]!["items"]!.AsArray());
        Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Recorded_facts_use_captured_organisation_day_and_flag_off_omits_them()
    {
        await Flag("people_screens"); await Flag("capacity_screens"); var alex = User(TestData.Alex); var admin = f.As(TestData.Admin);
        var tag = await (await admin.Post("/api/v1/admin/certificationTags", new { name = "Synthetic panel certificate" })).Json(201);
        var own = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/certifications")).Json();
        await (await f.As(TestData.Alex).Post("/api/v1/me/certifications", new { certificationTagId = tag.G("id"), expiresOn = "2026-10-31", rowVersion = own.I("rowVersion") })).Json(201);
        f.Clock.Now = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero);
        (await admin.Put("/api/v1/admin/settings/org_time_zone", new { value = "America/Halifax" })).EnsureSuccessStatusCode();
        var read = await Read(alex, TestData.Alex); Assert.Equal("2026-10-31", read.S("today")); Assert.Equal("2026-10-26", read.S("week"));
        Assert.Equal("ExpiresIn", Assert.Single(read["personFacts"]!["certifications"]!.AsArray())!.S("expiryState"));
        await Flag("capacity_screens", false); Assert.False((await Read(alex, TestData.Alex)).AsObject().ContainsKey("personFacts"));
    }

    [Fact]
    public async Task PM_note_is_trimmed_shared_with_panel_audited_once_and_changes_no_roles_or_capacity()
    {
        var p = await d.Project(); await Flag("people_screens"); var member = Member(p.Id); var alex = User(TestData.Alex);
        var roles = f.Db(db => db.ProjectMembers.Single(m => m.Id == member).Roles.ToArray()); var primary = f.Db(db => db.ProjectMembers.Single(m => m.Id == member).PrimaryDisciplineId);
        var dates = f.Db(db => db.PersonDateVersions.Count()); var notices = f.Db(db => db.Notifications.Count()); var activity = f.Db(db => db.ActivityLog.Count(a => a.ItemId == member)); var prior = Version(member);
        var save = await (await f.As(TestData.Pm).Patch(Note(p.Id, member), new { responsibilityNote = "  checker  " }, prior)).Json();
        Assert.Equal(member, save.G("id")); Assert.Equal(prior + 1, save.I("rowVersion")); Assert.Equal("checker", save.S("responsibilityNote"));
        Assert.Equal("checker", TeamMember(await Team(p.Id), member)["responsibility"]!.S("note"));
        Assert.Equal("checker", Assert.Single((await Read(alex))["projects"]!["items"]!.AsArray())!.S("responsibilityNote"));
        Assert.Equal(activity + 1, f.Db(db => db.ActivityLog.Count(a => a.ItemId == member)));
        var log = f.Db(db => db.ActivityLog.Where(a => a.ItemId == member).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).First());
        Assert.Equal(p.Id, log.ProjectId); Assert.Equal(User(TestData.Pm), log.ActorUserId); Assert.Contains("ResponsibilityNote", log.Changes); Assert.DoesNotContain("Roles", log.Changes);
        Assert.Equal(roles, f.Db(db => db.ProjectMembers.Single(m => m.Id == member).Roles.ToArray())); Assert.Equal(primary, f.Db(db => db.ProjectMembers.Single(m => m.Id == member).PrimaryDisciplineId));
        Assert.Equal(dates, f.Db(db => db.PersonDateVersions.Count())); Assert.Equal(notices, f.Db(db => db.Notifications.Count()));
        await (await f.As(TestData.Pm).Patch(Note(p.Id, member), new { responsibilityNote = (string?)null, rowVersion = save.I("rowVersion") })).Json();
        Assert.Null(TeamMember(await Team(p.Id), member)["responsibility"]!["note"]);
    }

    [Fact]
    public async Task Only_PM_or_active_lead_of_members_actual_primary_can_edit_and_role_PATCH_stays_PM_only()
    {
        var p = await d.Project(); await Flag("people_screens"); var member = Member(p.Id); var url = Note(p.Id, member);
        Assert.True(TeamMember(await Team(p.Id, TestData.Marc), member)["responsibility"]!["canEdit"]!.GetValue<bool>());
        await (await f.As(TestData.Marc).Patch(url, new { responsibilityNote = "Civil checker" }, Version(member))).Json();
        foreach (var actor in new[] { TestData.Omar, TestData.Sam, TestData.Alex, TestData.Rita })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(actor).Patch(url, new { responsibilityNote = "refused" }, Version(member))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch($"/api/v1/projects/{p.Id}/members/{member}", new { roles = new[] { "PM" } }, Version(member))).StatusCode);
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/disciplines/{civil}", new { isActive = false }, f.Db(db => db.ProjectDisciplines.Single(x => x.Id == civil).RowVersion))).Json(204);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch(url, new { responsibilityNote = "inactive lead" }, Version(member))).StatusCode);
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/members/{member}", new { primaryDisciplineId = (Guid?)null }, Version(member))).Json(204);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch(url, new { responsibilityNote = "no primary" }, Version(member))).StatusCode);
        await (await f.As(TestData.Pm).Patch(url, new { responsibilityNote = "PM without primary" }, Version(member))).Json();
    }

    [Fact]
    public async Task Strict_body_versions_wrong_project_and_removed_member_refuse_without_overwriting()
    {
        var p = await d.Project(); var q = await d.Project(); await Flag("people_screens"); var member = Member(p.Id); var url = Note(p.Id, member); var pm = f.As(TestData.Pm); var initial = Version(member);
        Assert.Equal((HttpStatusCode)428, (await pm.Patch(url, new { responsibilityNote = "missing version" })).StatusCode);
        foreach (var body in new object[] { new { rowVersion = initial }, new { responsibilityNote = "checker", roles = new[] { "PM" } }, new { responsibilityNote = new string('x', 81) }, new { responsibilityNote = "two\nlines" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch(url, body, initial)).StatusCode);
        var before = await State(); Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch(Note(q.Id, member), new { responsibilityNote = "wrong project" }, initial)).StatusCode); Assert.Equal(before, await State());
        await (await pm.Patch(url, new { responsibilityNote = "current" }, initial)).Json();
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Patch(url, new { responsibilityNote = "stale" }, initial)).StatusCode);
        Assert.Equal("current", TeamMember(await Team(p.Id), member)["responsibility"]!.S("note"));
        await (await pm.Delete($"/api/v1/projects/{p.Id}/members/{member}", Version(member))).Json();
        before = await State(); Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch(url, new { responsibilityNote = "removed" }, Version(member))).StatusCode); Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Complete_requires_PM_and_correction_reason_then_Archived_refuses_even_PM()
    {
        var p = await d.Project(); await Flag("people_screens"); var member = Member(p.Id); var pm = f.As(TestData.Pm); var url = Note(p.Id, member);
        await (await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Complete", reason = "Synthetic project work is complete", rowVersion = d.Version(p.Id) })).Json();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch(url, new { responsibilityNote = "lead correction", reason = "Synthetic reason" }, Version(member))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch(url, new { responsibilityNote = "PM correction" }, Version(member))).StatusCode);
        await (await pm.Patch(url, new { responsibilityNote = "PM correction", reason = "Synthetic correction reason" }, Version(member))).Json();
        await (await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Archived", reason = "Synthetic archive", rowVersion = d.Version(p.Id) })).Json();
        var before = await State(); Assert.Equal(HttpStatusCode.Forbidden, (await pm.Patch(url, new { responsibilityNote = "archived" }, Version(member))).StatusCode); Assert.Equal(before, await State());
    }
}
