using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR50 notices decorate already authorised candidates, never staffing or assignment rules.
public sealed class CapacityPersonPickerNoticeTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Today = new(2026, 10, 31);
    public CapacityPersonPickerNoticeTests() { d = new(f); f.Clock.SetDate(2026, 10, 31); }
    public void Dispose() => f.Dispose();
    async Task Setting(string name, object value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{name}", new { value })).EnsureSuccessStatusCode();
    async Task Facts(Guid user, string? type, DateOnly? start, DateOnly? end)
    {
        var row = await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users/{user}/certifications")).Json();
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{user}/person-facts", new { personType = type, personStartDate = start,
            personEndDate = end, reason = "Synthetic recorded picker facts" }, row.I("rowVersion"))).Json();
    }
    async Task<(Guid Project, Guid Task)> Setup()
    {
        await Setting("flag.capacity_screens", true); await Setting("flag.task_pickup", true); await Setting("flag.org_structure", true);
        var project = await d.Project();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Synthetic coverage request" })).Json(201);
        return (project.Id, task.G("id"));
    }
    async Task<JsonArray[]> Sources(Guid project, Guid task)
    {
        var pm = f.As(TestData.Pm); var admin = f.As(TestData.Admin);
        return [
            (await (await pm.GetAsync($"/api/v1/projects/{project}/changes/options")).Json())["people"]!.AsArray(),
            (await (await pm.GetAsync($"/api/v1/tasks/{task}/pickup-offer")).Json())["candidates"]!.AsArray(),
            (await (await pm.GetAsync($"/api/v1/projects/{project}/handoffs/options")).Json())["people"]!.AsArray(),
            (await (await admin.GetAsync("/api/v1/organisation")).Json())["people"]!.AsArray(),
            (await (await pm.GetAsync($"/api/v1/discipline-coordination?projectId={project}")).Json())["owners"]!.AsArray(),
            (await (await admin.GetAsync("/api/v1/planning/hr-identities")).Json()).AsArray()
        ];
    }
    static string WithoutNotices(JsonArray rows)
    {
        var copy = rows.DeepClone().AsArray(); foreach (var row in copy) row!.AsObject().Remove("personNotices"); return copy.ToJsonString();
    }
    Task<string> State() => f.DbAsync(async db => JsonSerializer.Serialize(new {
        Users = await db.Users.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RowVersion, x.IsActive, x.SupervisorId,
            x.WeeklyCapacityHours, x.PersonType, x.PersonStartDate, x.PersonEndDate, x.HrSourceSystem, x.HrPersonKey }).ToArrayAsync(),
        Members = await db.ProjectMembers.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Tasks = await db.Tasks.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.AssigneeId, x.ReviewerId, x.RowVersion }).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Notifications = await db.Notifications.CountAsync(), Receipts = await db.Idempotency.CountAsync(),
        Dates = await db.PersonDateVersions.CountAsync(), Certificates = await db.UserCertifications.CountAsync()
    }));

    [Fact]
    public async Task Six_sources_keep_exact_candidate_order_versions_and_fields_across_owner_flag_pairs_without_writes()
    {
        var s = await Setup(); var marc = d.User(TestData.Marc);
        await Facts(marc, "Co-op", Today.AddDays(1), Today.AddDays(48));
        await Setting("flag.capacity_screens", false); await Setting("flag.people_screens", true); await Setting("flag.visual_system", false);
        var off = await Sources(s.Project, s.Task);
        Assert.All(off, rows => Assert.All(rows, row => Assert.False(row!.AsObject().ContainsKey("personNotices"))));
        await Setting("flag.capacity_screens", true); await Setting("flag.people_screens", false);
        var before = await State(); var on = await Sources(s.Project, s.Task);
        for (var i = 0; i < off.Length; i++) {
            Assert.NotEmpty(on[i]); Assert.Equal(off[i].ToJsonString(), WithoutNotices(on[i]));
            var person = Assert.Single(on[i].Where(row => row!.G("id") == marc))!;
            Assert.Equal(("Co-op", true, true), (person["personNotices"]!.S("personType"), person["personNotices"]!["starts"]!.GetValue<bool>(), person["personNotices"]!["ends"]!.GetValue<bool>()));
            Assert.DoesNotContain("certifications", on[i].ToJsonString());
            if (i < 5) Assert.DoesNotContain("hrPersonKey", on[i].ToJsonString());
        }
        // The HR route retains its existing HR fields, so check privacy only on ordinary picker sources above.
        Assert.Equal(before, await State());
        await Setting("flag.capacity_screens", false);
        var restored = await Sources(s.Project, s.Task);
        for (var i = 0; i < off.Length; i++) Assert.Equal(off[i].ToJsonString(), restored[i].ToJsonString());
    }

    [Fact]
    public async Task All_six_bridges_share_recorded_unknown_start_and_inclusive_end_window_without_status_inference()
    {
        var s = await Setup(); var marc = d.User(TestData.Marc);
        foreach (var (offset, ends) in new[] { (61, false), (60, true), (0, true), (-1, false) }) {
            await Facts(marc, null, offset > 0 ? Today.AddDays(1) : null, Today.AddDays(offset));
            foreach (var rows in await Sources(s.Project, s.Task)) {
                var notice = rows.Single(row => row!.G("id") == marc)!["personNotices"]!;
                Assert.Null(notice["personType"]); Assert.Equal(offset > 0, notice["starts"]!.GetValue<bool>()); Assert.Equal(ends, notice["ends"]!.GetValue<bool>());
                Assert.DoesNotContain("Ended", notice.ToJsonString()); Assert.DoesNotContain("Available", notice.ToJsonString());
            }
        }
        await Setting("availability_window_days", 0); await Facts(marc, "Contractor", Today, Today.AddDays(1));
        foreach (var rows in await Sources(s.Project, s.Task)) {
            var notice = rows.Single(row => row!.G("id") == marc)!["personNotices"]!;
            Assert.False(notice["starts"]!.GetValue<bool>()); Assert.False(notice["ends"]!.GetValue<bool>());
        }
        await Facts(marc, null, null, null);
        foreach (var rows in await Sources(s.Project, s.Task)) {
            var notice = rows.Single(row => row!.G("id") == marc)!["personNotices"]!;
            Assert.Null(notice["personType"]); Assert.Null(notice["startDate"]); Assert.Null(notice["endDate"]);
        }
        Assert.Equal(0, f.Db(db => db.PersonDateVersions.Count()));
    }

    [Fact]
    public async Task New_bridges_use_organisation_timezone_at_a_UTC_midnight_boundary()
    {
        var s = await Setup(); var marc = d.User(TestData.Marc); await Facts(marc, null, Today.AddDays(1), Today.AddDays(1));
        f.Clock.Now = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero);
        await Setting("org_time_zone", "America/Halifax");
        foreach (var rows in await Sources(s.Project, s.Task)) {
            var notice = rows.Single(row => row!.G("id") == marc)!["personNotices"]!;
            Assert.True(notice["starts"]!.GetValue<bool>()); Assert.True(notice["ends"]!.GetValue<bool>());
        }
        await Setting("org_time_zone", "UTC");
        foreach (var rows in await Sources(s.Project, s.Task)) {
            var notice = rows.Single(row => row!.G("id") == marc)!["personNotices"]!;
            Assert.False(notice["starts"]!.GetValue<bool>()); Assert.True(notice["ends"]!.GetValue<bool>());
        }
        await Facts(marc, null, null, Today);
        foreach (var rows in await Sources(s.Project, s.Task)) Assert.False(rows.Single(row => row!.G("id") == marc)!["personNotices"]!["ends"]!.GetValue<bool>());
        await Setting("org_time_zone", "America/Halifax");
        foreach (var rows in await Sources(s.Project, s.Task)) Assert.True(rows.Single(row => row!.G("id") == marc)!["personNotices"]!["ends"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Pickup_refusal_current_owner_and_outside_team_never_gain_candidates_from_dates()
    {
        var s = await Setup(); await Facts(d.User(TestData.Diane), "Employee", Today.AddDays(1), null);
        var before = await State(); var pm = await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{s.Task}/pickup-offer")).Json();
        var ids = pm["candidates"]!.AsArray().Select(row => row!.G("id")).ToArray();
        Assert.Contains(d.User(TestData.Marc), ids); Assert.Contains(d.User(TestData.Jill), ids);
        Assert.DoesNotContain(d.User(TestData.Alex), ids); Assert.DoesNotContain(d.User(TestData.Diane), ids);
        foreach (var actor in new[] { TestData.Alex, TestData.Diane, TestData.Rita }) {
            var detail = await (await f.As(actor).GetAsync($"/api/v1/tasks/{s.Task}/pickup-offer")).Json();
            Assert.False(detail["allowed"]!["assign"]!.GetValue<bool>()); Assert.Empty(detail["candidates"]!.AsArray());
        }
        Assert.Equal(before, await State());
        await (await f.As(TestData.Pm).Post($"/api/v1/tasks/{s.Task}/pickup-offer/assign", new { userId = d.User(TestData.Jill), rowVersion = pm.I("rowVersion") })).Json();
        Assert.Equal(d.User(TestData.Jill), f.Db(db => db.Tasks.Single(task => task.Id == s.Task).AssigneeId));
    }

    [Fact]
    public async Task Inactive_people_and_nonmember_dates_keep_each_source_own_existing_cohort()
    {
        var s = await Setup(); var marc = d.User(TestData.Marc);
        await Facts(marc, "Co-op", Today.AddDays(1), null);
        var editor = await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users/{marc}/certifications")).Json();
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{marc}", new { isActive = false }, editor.I("rowVersion"))).EnsureSuccessStatusCode();
        await Facts(d.User(TestData.Diane), "Contractor", null, Today);
        await Setting("flag.capacity_screens", false); var off = await Sources(s.Project, s.Task);
        await Setting("flag.capacity_screens", true); var before = await State(); var on = await Sources(s.Project, s.Task);
        for (var i = 0; i < on.Length; i++) Assert.Equal(off[i].ToJsonString(), WithoutNotices(on[i]));
        foreach (var index in new[] { 0, 1, 2, 3, 5 }) Assert.DoesNotContain(on[index], row => row!.G("id") == marc);
        foreach (var index in new[] { 0, 1, 2, 4 }) Assert.DoesNotContain(on[index], row => row!.G("id") == d.User(TestData.Diane));
        Assert.Equal(before, await State());
    }

    [Fact]
    public async Task HR_notice_refresh_keeps_canonical_stale_mapping_refusal_and_admin_only_write()
    {
        await Setup(); var user = d.User(TestData.Marc); var admin = f.As(TestData.Admin);
        var prior = (await (await admin.GetAsync("/api/v1/planning/hr-identities")).Json()).AsArray().Single(row => row!.G("id") == user)!;
        await Facts(user, "Co-op", null, Today);
        var refreshed = (await (await admin.GetAsync("/api/v1/planning/hr-identities")).Json()).AsArray().Single(row => row!.G("id") == user)!;
        Assert.True(refreshed.I("rowVersion") > prior.I("rowVersion")); Assert.NotNull(refreshed["personNotices"]);
        var before = await State(); var body = new { sourceSystem = "SyntheticHR", personKey = "synthetic-person", rowVersion = prior.I("rowVersion") };
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Put($"/api/v1/planning/hr-identities/{user}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Put($"/api/v1/planning/hr-identities/{user}", body)).StatusCode);
        Assert.Equal(before, await State());
        await (await admin.Put($"/api/v1/planning/hr-identities/{user}", new { body.sourceSystem, body.personKey, rowVersion = refreshed.I("rowVersion") })).Json();
        Assert.Equal("synthetic-person", f.Db(db => db.Users.Single(row => row.Id == user).HrPersonKey));
    }

    [Fact]
    public async Task Hidden_projects_empty_workspace_scopes_and_nonadmin_HR_requests_keep_existing_boundaries()
    {
        var s = await Setup(); await Setting("restricted_projects_enabled", true);
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{s.Project}", new { visibility = "Restricted" }, d.Version(s.Project))).Json();
        var before = await State();
        foreach (var path in new[] { $"projects/{s.Project}/changes/options", $"projects/{s.Project}/handoffs/options",
            $"tasks/{s.Task}/pickup-offer", $"discipline-coordination?projectId={s.Project}" })
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Diane).GetAsync($"/api/v1/{path}")).StatusCode);
        var empty = await (await f.As(TestData.Pm).GetAsync("/api/v1/discipline-coordination?scopeKind=set&scopeProjectIds=")).Json();
        Assert.Empty(empty["owners"]!.AsArray());
        foreach (var actor in new[] { TestData.Pm, TestData.Diane, TestData.Rita })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(actor).GetAsync("/api/v1/planning/hr-identities")).StatusCode);
        var org = await (await f.As(TestData.Alex).GetAsync("/api/v1/organisation")).Json();
        Assert.DoesNotContain(org["people"]!.AsArray(), row => row!.G("id") == d.User(TestData.Diane));
        Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Populated_units_override_shared_supervisor_and_recorded_dates_keep_pickup_candidates_unchanged()
    {
        var s = await Setup(); var admin = f.As(TestData.Admin); var pm = f.As(TestData.Pm);
        var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill); var marc = d.User(TestData.Marc); var diane = d.User(TestData.Diane);
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var level = levels.First(row => row!.S("name") == "Team")!.G("id");
        var firstUnit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic pickup first unit", levelId = level })).Json(201);
        var otherUnit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic pickup other unit", levelId = level })).Json(201);
        async Task Place(Guid person, Guid unit) => await (await admin.Patch($"/api/v1/admin/organisation/users/{person}/placement",
            new { unitId = unit }, f.Db(db => db.Users.Single(user => user.Id == person).RowVersion))).Json(204);
        await Place(alex, firstUnit.G("id")); await Place(jill, firstUnit.G("id")); await Place(marc, otherUnit.G("id")); await Place(diane, firstUnit.G("id"));
        (await admin.Patch($"/api/v1/admin/users/{diane}", new { isActive = false }, f.Db(db => db.Users.Single(user => user.Id == diane).RowVersion))).EnsureSuccessStatusCode();
        var supervisor = f.Db(db => db.Users.Single(user => user.Id == alex).SupervisorId);
        Assert.NotNull(supervisor); Assert.Equal(supervisor, f.Db(db => db.Users.Single(user => user.Id == marc).SupervisorId));
        Assert.Equal(firstUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == alex).UnitId));
        Assert.Equal(firstUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == jill).UnitId));
        Assert.Equal(otherUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == marc).UnitId));
        var path = $"/api/v1/tasks/{s.Task}/pickup-offer";
        var prior = await (await pm.GetAsync(path)).Json();
        Assert.True(prior["allowed"]!["assign"]!.GetValue<bool>());
        Assert.Equal(jill, Assert.Single(prior["candidates"]!.AsArray())!.G("id"));
        await Facts(jill, "Co-op", Today.AddDays(1), Today.AddDays(48));
        await Facts(marc, "Contractor", null, Today.AddDays(-1)); await Facts(alex, "Employee", Today.AddDays(1), null);
        var before = await State(); var current = await (await pm.GetAsync(path)).Json();
        Assert.Equal(WithoutNotices(prior["candidates"]!.AsArray()), WithoutNotices(current["candidates"]!.AsArray()));
        Assert.Equal(prior["allowed"]!.ToJsonString(), current["allowed"]!.ToJsonString()); Assert.Equal(prior.I("rowVersion"), current.I("rowVersion"));
        var candidate = Assert.Single(current["candidates"]!.AsArray())!;
        Assert.Equal((jill, "Co-op", true, true), (candidate.G("id"), candidate["personNotices"]!.S("personType"),
            candidate["personNotices"]!["starts"]!.GetValue<bool>(), candidate["personNotices"]!["ends"]!.GetValue<bool>()));
        foreach (var actor in new[] { TestData.Alex, TestData.Jill }) {
            var read = await (await f.As(actor).GetAsync(path)).Json();
            Assert.False(read["allowed"]!["assign"]!.GetValue<bool>()); Assert.Empty(read["candidates"]!.AsArray());
        }
        Assert.Equal(before, await State());
        Assert.Equal(firstUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == alex).UnitId));
        Assert.Equal(firstUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == jill).UnitId));
        Assert.Equal(otherUnit.G("id"), f.Db(db => db.Users.Single(user => user.Id == marc).UnitId));
    }

    [Fact]
    public async Task Populated_own_certification_stays_out_of_all_six_authorised_candidate_arrays()
    {
        var s = await Setup(); var marc = d.User(TestData.Marc); var actor = f.As(TestData.Marc);
        await Facts(marc, "Co-op", Today.AddDays(1), Today.AddDays(48));
        const string tagName = "Synthetic six-source certificate sentinel";
        var tag = await (await f.As(TestData.Admin).Post("/api/v1/admin/certificationTags", new { name = tagName })).Json(201);
        var own = await (await actor.GetAsync("/api/v1/me/certifications")).Json();
        var created = await (await actor.Post("/api/v1/me/certifications", new { certificationTagId = tag.G("id"), expiresOn = Today.AddDays(23),
            rowVersion = own.I("rowVersion") })).Json(201);
        var editor = await (await actor.GetAsync("/api/v1/me/certifications")).Json();
        var recorded = Assert.Single(editor["facts"]!["certifications"]!.AsArray())!;
        Assert.Equal((created.G("id"), tag.G("id"), tagName, Today.AddDays(23).ToString("yyyy-MM-dd")),
            (recorded.G("id"), recorded.G("tagId"), recorded.S("tagName"), recorded.S("expiresOn")));
        var before = await State(); var sources = await Sources(s.Project, s.Task);
        Assert.Equal(6, sources.Length);
        foreach (var rows in sources) {
            Assert.NotEmpty(rows); var person = Assert.Single(rows.Where(row => row!.G("id") == marc))!;
            Assert.Equal(("Co-op", true, true), (person["personNotices"]!.S("personType"), person["personNotices"]!["starts"]!.GetValue<bool>(),
                person["personNotices"]!["ends"]!.GetValue<bool>()));
            var wire = rows.ToJsonString();
            foreach (var forbidden in new[] { tagName, created.S("id"), tag.S("id"), "certifications", "certificationTagId", "tagId", "tagName", "expiresOn", "expiryState", "daysToExpiry" })
                Assert.DoesNotContain(forbidden, wire);
        }
        Assert.Equal(before, await State());
        var reread = await (await actor.GetAsync("/api/v1/me/certifications")).Json();
        Assert.Equal(recorded.ToJsonString(), Assert.Single(reread["facts"]!["certifications"]!.AsArray())!.ToJsonString());
    }
}
