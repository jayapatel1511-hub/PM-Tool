using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S29 normal-command fixed facts, no qualification verification or derived staffing/capacity changes.
public sealed class CapacityPersonFactsTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Today = new(2026, 10, 31);
    public CapacityPersonFactsTests() { d = new(f); f.Clock.SetDate(2026, 10, 31); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Tag(string name) => await (await f.As(TestData.Admin).Post("/api/v1/admin/certificationTags", new { name })).Json(201);
    async Task<JsonNode> Own(string actor = TestData.Omar) => await (await f.As(actor).GetAsync("/api/v1/me/certifications")).Json();
    async Task<JsonNode> Admin(Guid user) => await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users/{user}/certifications")).Json();
    async Task<HttpResponseMessage> Post(string actor, JsonNode tag, DateOnly? date, string? key = null, int? version = null, Guid? adminTarget = null, string? reason = null)
    {
        var own = adminTarget is { } target ? await Admin(target) : await Own(actor);
        using var request = new HttpRequestMessage(HttpMethod.Post, adminTarget is { } user ? $"/api/v1/admin/users/{user}/certifications" : "/api/v1/me/certifications") {
            Content = JsonContent.Create(new { certificationTagId = tag.G("id"), expiresOn = date, rowVersion = version ?? own.I("rowVersion"), reason }) };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return await f.As(actor).SendAsync(request);
    }
    async Task<JsonNode> Cert(JsonNode tag, DateOnly? date, string actor = TestData.Omar) => await (await Post(actor, tag, date)).Json(201);
    async Task<JsonNode> Facts(Guid user, string? type, DateOnly? start, DateOnly? end, string reason = "Synthetic administrative fixed-fact correction")
    {
        var editor = await Admin(user);
        return await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{user}/person-facts", new { personType = type, personStartDate = start, personEndDate = end, reason }, editor.I("rowVersion"))).Json();
    }
    static JsonNode Certification(JsonNode editor, JsonNode tag) => editor["facts"]!["certifications"]!.AsArray().Single(row => row!.G("tagId") == tag.G("id"))!;
    Task<string> State() => f.DbAsync(async db => JsonSerializer.Serialize(new {
        Users = await db.Users.OrderBy(row => row.Id).Select(row => new { row.Id, row.PersonType, row.PersonStartDate, row.PersonEndDate, row.IsActive, row.WeeklyCapacityHours, row.RowVersion }).ToArrayAsync(),
        Certs = await db.UserCertifications.OrderBy(row => row.Id).ToArrayAsync(), Tags = await db.CertificationTags.OrderBy(row => row.Id).ToArrayAsync(),
        Activity = await db.ActivityLog.CountAsync(), Receipts = await db.Idempotency.CountAsync(), Schedules = await db.PersonSchedules.CountAsync(), Dates = await db.PersonDateVersions.CountAsync()
    }));

    [Fact]
    public async Task Omar_records_Confined_space_expiring20_and_authorised_Staff_Team_and_tag_filter_share_the_fact()
    {
        var project = await d.Project(); await Flag();
        var tag = await Tag("Confined space"); var created = await Cert(tag, Today.AddDays(20)); var before = await State();
        var own = await Own(); var fact = Certification(own, tag);
        Assert.Equal(("ExpiresIn", 20, "Confined space", Today.AddDays(20).ToString("yyyy-MM-dd")), (fact.S("expiryState"), fact.I("daysToExpiry"), fact.S("tagName"), fact.S("expiresOn")));
        var staff = await (await f.As(TestData.Lena).GetAsync("/api/v1/staff")).Json();
        var omar = staff["people"]!.AsArray().Single(row => row!.G("id") == d.User(TestData.Omar))!;
        Assert.Equal(fact.ToJsonString(), Assert.Single(omar["personFacts"]!["certifications"]!.AsArray())!.ToJsonString());
        var team = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team?certificationTagId={tag.S("id")}")).Json();
        var member = Assert.Single(team["members"]!.AsArray())!; Assert.Equal(d.User(TestData.Omar), member.G("userId"));
        Assert.Equal(created.G("id"), Assert.Single(member["personFacts"]!["certifications"]!.AsArray())!.G("id"));
        var workspace = await (await f.As(TestData.Pm).GetAsync($"/api/v1/team?projects={project.Id}&certificationTagId={tag.S("id")}")).Json();
        Assert.Equal(d.User(TestData.Omar), Assert.Single(workspace["people"]!.AsArray())!.G("userId"));
        Assert.Equal(before, await State());
        Assert.DoesNotContain("certificationCount", staff.ToJsonString()); Assert.DoesNotContain("rank", member["personFacts"]!.ToJsonString());
    }

    [Fact]
    public async Task Expiry_boundaries_null_and_organisation_timezone_use_one_server_calendar_day()
    {
        await d.Project(); await Flag();
        foreach (var (label, offset, state, days) in new (string, int?, string, int?)[] { ("Unknown", null, "Unknown", null), ("Past", -1, "Expired", null), ("Today", 0, "ExpiresIn", 0), ("Warn", 30, "ExpiresIn", 30), ("Later", 31, "Recorded", null) })
        {
            var tag = await Tag(label); await Cert(tag, offset is { } n ? Today.AddDays(n) : null);
            var row = Certification(await Own(), tag); Assert.Equal(state, row.S("expiryState")); Assert.Equal(days, row["daysToExpiry"]?.GetValue<int>());
        }
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/certification_warn_days", new { value = 31 })).EnsureSuccessStatusCode();
        Assert.Equal("ExpiresIn", (await Own())["facts"]!["certifications"]!.AsArray().Single(row => row!.S("tagName") == "Later")!.S("expiryState"));
        f.Clock.Now = new DateTimeOffset(2026, 11, 1, 0, 30, 0, TimeSpan.Zero);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "America/Halifax" })).EnsureSuccessStatusCode();
        var local = await Own(); Assert.Equal("2026-10-31", local.S("today"));
        Assert.Equal("ExpiresIn", local["facts"]!["certifications"]!.AsArray().Single(row => row!.S("tagName") == "Today")!.S("expiryState"));
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "UTC" })).EnsureSuccessStatusCode();
        var utc = await Own(); Assert.Equal("2026-11-01", utc.S("today"));
        Assert.Equal("Expired", utc["facts"]!["certifications"]!.AsArray().Single(row => row!.S("tagName") == "Today")!.S("expiryState"));
    }

    [Fact]
    public async Task Jill_Coop_end_and_future_start_are_display_only_and_picker_has_no_certifications()
    {
        var project = await d.Project(); await Flag(); var jill = d.User(TestData.Jill);
        var pickerBefore = await (await f.As(TestData.Pm).GetAsync("/api/v1/users?q=Jill")).Json();
        var gridBefore = await (await f.As(TestData.Sam).GetAsync("/api/v1/planning/grid?from=2026-11-02&weeks=2")).Json();
        var factsBefore = f.Db(db => db.Users.Where(user => user.Id == jill).Select(user => new { user.IsActive, user.WeeklyCapacityHours, user.SupervisorId }).Single());
        await Facts(jill, "Co-op", Today.AddDays(1), new DateOnly(2026, 12, 18));
        var read = await Admin(jill); var notice = read["facts"]!["notices"]!;
        Assert.Equal(("Co-op", true, true, "2026-12-18"), (notice.S("personType"), notice["starts"]!.GetValue<bool>(), notice["ends"]!.GetValue<bool>(), notice.S("endDate")));
        var picker = await (await f.As(TestData.Pm).GetAsync("/api/v1/users?q=Jill")).Json();
        Assert.Equal(pickerBefore.AsArray().Select(row => row!.G("id")), picker.AsArray().Select(row => row!.G("id")));
        var candidate = picker.AsArray().Single(row => row!.G("id") == jill)!; Assert.Equal(notice.ToJsonString(), candidate["personNotices"]!.ToJsonString());
        Assert.DoesNotContain("certifications", candidate.ToJsonString());
        var gridAfter = await (await f.As(TestData.Sam).GetAsync("/api/v1/planning/grid?from=2026-11-02&weeks=2")).Json();
        Assert.Equal(gridBefore["people"]!.ToJsonString(), gridAfter["people"]!.ToJsonString());
        Assert.Equal(JsonSerializer.Serialize(factsBefore), JsonSerializer.Serialize(f.Db(db => db.Users.Where(user => user.Id == jill).Select(user => new { user.IsActive, user.WeeklyCapacityHours, user.SupervisorId }).Single())));
        foreach (var (offset, ends) in new[] { (61, false), (60, true), (0, true), (-1, false) })
        { await Facts(jill, null, null, Today.AddDays(offset)); Assert.Equal(ends, (await Admin(jill))["facts"]!["notices"]!["ends"]!.GetValue<bool>()); }
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/availability_window_days", new { value = 0 })).EnsureSuccessStatusCode();
        await Facts(jill, "Contractor", null, Today.AddDays(1)); Assert.False((await Admin(jill))["facts"]!["notices"]!["ends"]!.GetValue<bool>());
        Assert.Equal(0, f.Db(db => db.PersonDateVersions.Count()));
    }

    [Fact]
    public async Task Own_child_ID_and_versions_preserve_history_and_admin_correction_requires_a_reason()
    {
        await d.Project(); await Flag(); var tag = await Tag("EIT"); var saved = await Cert(tag, Today.AddDays(20));
        var before = await State();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(TestData.Omar, tag, null)).StatusCode); Assert.Equal(before, await State());
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await f.As(TestData.Omar).Patch($"/api/v1/me/certifications/{saved.S("id")}", new { expiresOn = Today })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Omar).Patch($"/api/v1/me/certifications/{saved.S("id")}", new { expiresOn = Today, userId = d.User(TestData.Alex) }, saved.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).Patch($"/api/v1/me/certifications/{saved.S("id")}", new { expiresOn = Today }, saved.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{d.User(TestData.Omar)}/certifications/{saved.S("id")}", new { expiresOn = Today }, saved.I("rowVersion"))).StatusCode);
        Assert.Equal(before, await State());
        var correction = await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{d.User(TestData.Omar)}/certifications/{saved.S("id")}", new { expiresOn = Today, reason = "Correct the recorded expiry" }, saved.I("rowVersion"))).Json();
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Omar).Patch($"/api/v1/me/certifications/{saved.S("id")}", new { expiresOn = (DateOnly?)null }, saved.I("rowVersion"))).StatusCode);
        var logs = f.Db(db => db.ActivityLog.Where(row => row.ItemType == "UserCertification" && row.ItemId == saved.G("id")).ToArray());
        Assert.Contains(logs, row => row.ActorUserId == d.User(TestData.Omar));
        Assert.Contains(logs, row => row.ActorUserId == d.User(TestData.Admin) && row.Reason == "Correct the recorded expiry" && row.Changes.Contains(Today.AddDays(20).ToString("yyyy-MM-dd")) && row.Changes.Contains(Today.ToString("yyyy-MM-dd")));
        using var remove = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/me/certifications/{saved.S("id")}") { Content = JsonContent.Create(new { rowVersion = correction.I("rowVersion") }) };
        (await f.As(TestData.Omar).SendAsync(remove)).EnsureSuccessStatusCode();
        Assert.Empty((await Own())["facts"]!["certifications"]!.AsArray());
        var removed = f.Db(db => db.ActivityLog.Single(row => row.ItemId == saved.G("id") && row.Action == "Removed"));
        Assert.Equal(d.User(TestData.Omar), removed.ActorUserId); Assert.Equal("User", removed.ActorType);
        var snapshot = JsonNode.Parse(removed.Snapshot!)!;
        Assert.Equal(d.User(TestData.Omar), snapshot.G("UserId")); Assert.Equal(tag.G("id"), snapshot.G("CertificationTagId"));
        Assert.Equal(Today.ToString("yyyy-MM-dd"), snapshot.S("ExpiresOn"));
    }

    [Fact]
    public async Task Readonly_and_proxy_roles_refuse_and_Admin_can_correct_inactive_people_without_reassigning_child()
    {
        await d.Project(); await Flag(); var tag = await Tag("P.Eng");
        Assert.False((await Own(TestData.Rita))["canEdit"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(TestData.Rita, tag, null)).StatusCode);
        foreach (var actor in new[] { TestData.Sam, TestData.Pm, TestData.Marc, TestData.Lena })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(actor).GetAsync($"/api/v1/admin/users/{d.User(TestData.Omar)}/certifications")).StatusCode);
        var user = await Admin(d.User(TestData.Omar));
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{d.User(TestData.Omar)}", new { isActive = false }, user.I("rowVersion"))).EnsureSuccessStatusCode();
        var before = await State(); Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Omar).GetAsync("/api/v1/me/certifications")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(TestData.Admin, tag, null, adminTarget: d.User(TestData.Omar))).StatusCode); Assert.Equal(before, await State());
        var corrected = await (await Post(TestData.Admin, tag, Today.AddDays(-1), adminTarget: d.User(TestData.Omar), reason: "Record an inactive person's existing certification")).Json(201);
        Assert.Equal("Expired", Certification(await Admin(d.User(TestData.Omar)), tag).S("expiryState"));
        Assert.False(f.Db(db => db.Users.Single(row => row.Id == d.User(TestData.Omar)).IsActive));
        Assert.Equal(d.User(TestData.Omar), f.Db(db => db.UserCertifications.Single(row => row.Id == corrected.G("id")).UserId));
    }

    [Fact]
    public async Task Invalid_parent_facts_stale_versions_and_inactive_tags_fail_atomically_without_erasing_records()
    {
        await d.Project(); await Flag(); var user = d.User(TestData.Jill); var tag = await Tag("Recorded tag"); var cert = await Cert(tag, null);
        var editor = await Admin(user); var before = await State();
        foreach (var body in new object[] { new { personType = "employee", reason = "Synthetic correction" }, new { personStartDate = "2026-12-20", personEndDate = "2026-12-18", reason = "Synthetic correction" },
            new { personType = "Co-op", weeklyCapacityHours = 0, reason = "Synthetic correction" }, new { personType = "Co-op", reason = "" }, new { personEndDate = 7, reason = "Synthetic correction" } })
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{user}/person-facts", body, editor.I("rowVersion"))).StatusCode);
        Assert.Equal(before, await State());
        await Facts(user, "Co-op", null, new DateOnly(2026, 12, 18));
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{user}/person-facts", new { personType = "Employee", reason = "Synthetic stale correction" }, editor.I("rowVersion"))).StatusCode);
        (await f.As(TestData.Admin).Patch($"/api/v1/admin/certificationTags/{tag.S("id")}", new { isActive = false }, tag.I("rowVersion"))).EnsureSuccessStatusCode();
        Assert.False(Certification(await Own(), tag)["tagActive"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(TestData.Alex, tag, null)).StatusCode);
        (await f.As(TestData.Omar).Patch($"/api/v1/me/certifications/{cert.S("id")}", new { expiresOn = Today.AddDays(10) }, cert.I("rowVersion"))).EnsureSuccessStatusCode();
        Assert.Equal(10, Certification(await Own(), tag).I("daysToExpiry"));
    }

    [Fact]
    public async Task Authorised_ID_readers_and_team_scope_never_return_unselected_or_hidden_person_facts()
    {
        var project = await d.Project(); var hidden = await d.Project(); await Flag(); var tag = await Tag("Sensitive assigned tag"); await Cert(tag, Today.AddDays(20));
        var sam = await (await f.As(TestData.Sam).GetAsync("/api/v1/staff")).Json();
        Assert.DoesNotContain("Sensitive assigned tag", sam.ToJsonString());
        var settings = new OrgSettings { Flags = new Dictionary<string, bool> { ["capacity_screens"] = true } };
        var read = await f.DbAsync(db => PersonFacts.Read(db, [d.User(TestData.Alex)], settings, f.Clock));
        Assert.Equal(new[] { d.User(TestData.Alex) }, read.Keys); Assert.Empty(read[d.User(TestData.Alex)].Certifications);
        Assert.Empty(await f.DbAsync(db => PersonFacts.Read(db, [], settings, f.Clock)));
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{hidden.Id}/team?certificationTagId={tag.S("id")}")).StatusCode);
        var scoped = await (await f.As(TestData.Jill).GetAsync($"/api/v1/team?projects={hidden.Id}&certificationTagId={tag.S("id")}")).Json(); Assert.Empty(scoped["people"]!.AsArray());
        foreach (var value in new[] { "bad", Guid.Empty.ToString(), Guid.NewGuid().ToString() }) Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team?certificationTagId={value}")).StatusCode);
    }

    [Fact]
    public async Task Owner_flag_alone_controls_facts_settings_and_same_key_create_replay_still_obeys_current_flag()
    {
        var project = await d.Project();
        var legacy = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team")).Json();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Omar).GetAsync("/api/v1/me/certifications")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Omar).GetAsync("/api/v1/certification-tags")).StatusCode);
        var off = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team?certificationTagId=bad")).Json();
        Assert.NotEmpty(off["members"]!.AsArray());
        foreach (var member in off["members"]!.AsArray())
        {
            Assert.False(member!.AsObject().ContainsKey("personFacts")); // Capacity facts remain independently off.
            var user = f.Db(db => db.Users.Where(user => user.Id == member.G("userId")).Select(user => new { user.HomeDisciplineId, user.OfficeId, user.SupervisorId }).Single());
            var organisation = member["organisationFacts"]!;
            foreach (var (facetKey, expected) in new[] { ("homeDiscipline", user.HomeDisciplineId), ("office", user.OfficeId), ("supervisor", user.SupervisorId) })
            {
                if (expected is { } id) Assert.Equal(id, organisation[facetKey]!.G("id"));
                else Assert.Null(organisation[facetKey]);
            }
            Assert.False(organisation.AsObject().ContainsKey("unit"));
            Assert.True(member.AsObject().Remove("organisationFacts"));
            member.AsObject().Remove("responsibility"); // S43 FR-SCR-76 adds the member's responsibility under the same flag
        }
        Assert.All(legacy["members"]!.AsArray(), member => Assert.False(member!.AsObject().ContainsKey("responsibility")));
        Assert.Equal(legacy.ToJsonString(), off.ToJsonString());
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = false })).EnsureSuccessStatusCode();
        Assert.Equal(legacy.ToJsonString(), (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team?certificationTagId=bad")).Json()).ToJsonString());
        await Flag();
        var tag = await Tag("Stable receipt tag"); var own = await Own(); var key = Guid.NewGuid().ToString();
        var first = await (await Post(TestData.Omar, tag, Today.AddDays(20), key, own.I("rowVersion"))).Json(201);
        var retry = await (await Post(TestData.Omar, tag, Today.AddDays(20), key, own.I("rowVersion"))).Json(201);
        Assert.Equal(first.G("id"), retry.G("id")); Assert.Equal(1, f.Db(db => db.UserCertifications.Count()));
        // A cached successful receipt is not authority to edit after a normal role correction.
        (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{d.User(TestData.Omar)}/roles", new { role = SystemRole.ReadOnly })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(TestData.Omar, tag, Today.AddDays(20), key, own.I("rowVersion"))).StatusCode);
        (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/users/{d.User(TestData.Omar)}/roles/{Uri.EscapeDataString(SystemRole.ReadOnly)}")).EnsureSuccessStatusCode();
        Assert.Equal(first.G("id"), (await (await Post(TestData.Omar, tag, Today.AddDays(20), key, own.I("rowVersion"))).Json(201)).G("id"));
        Assert.True((await Own())["canEdit"]!.GetValue<bool>());
        await Flag(false);
        // Build the identical request directly: the editor route itself must now404 too.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/me/certifications") { Content = JsonContent.Create(new { certificationTagId = tag.G("id"), expiresOn = Today.AddDays(20), rowVersion = own.I("rowVersion") }) };
        request.Headers.Add("Idempotency-Key", key);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Omar).SendAsync(request)).StatusCode);
        Assert.Equal(1, f.Db(db => db.UserCertifications.Count()));
        var final = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/team")).Json(); Assert.Equal(legacy.ToJsonString(), final.ToJsonString());
    }
}
