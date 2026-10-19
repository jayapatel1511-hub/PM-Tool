using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PickerRankTests(HubFactory f)
{
    readonly TestData data = new(f);
    HttpClient Admin => f.As(TestData.Admin);

    async Task Flag(bool enabled, Func<Task> check)
    {
        var previous = (await (await Admin.GetAsync("/api/v1/me")).Json())["flags"]!["picker_project_rank"]!.GetValue<bool>();
        try { await (await Admin.Put("/api/v1/admin/settings/flag.picker_project_rank", new { value = enabled })).Json(); await check(); }
        finally { await (await Admin.Put("/api/v1/admin/settings/flag.picker_project_rank", new { value = previous })).Json(); }
    }

    async Task<AppUser> Person(string name, string? title = null, Guid? discipline = null, bool active = true)
    {
        var user = new AppUser { Email = $"synthetic-picker-{Guid.NewGuid():N}@hub.test", DisplayName = name, JobTitle = title, HomeDisciplineId = discipline, IsActive = active };
        await f.DbAsync(async db => { db.Users.Add(user); return await db.SaveChangesAsync(); });
        return user;
    }

    static string Ids(params AppUser[] users) => string.Join(',', users.Select(user => user.Id));
    static Guid[] Ordered(JsonNode rows) => rows.AsArray().Select(row => row!.G("id")).ToArray();

    [Fact, Trait("Spec", "AC-INT-06"), Trait("flag-on", "picker_project_rank")]
    public async Task Viewer_then_current_and_recent_team_then_everyone_else_with_marks_and_limit()
    {
        var p = await data.Project();
        AppUser pat = await Person("Z Pat"), lee = await Person("B Lee"), sam = await Person("C Sam"), kim = await Person("A Kim");
        await f.DbAsync(async db => {
            foreach (var (user, removed) in new[] { (lee, (DateTimeOffset?)f.Clock.Now.AddDays(-30)), (sam, null), (kim, (DateTimeOffset?)f.Clock.Now.AddDays(-120)) })
                db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = user.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now.AddDays(-150), RemovedAt = removed });
            return await db.SaveChangesAsync();
        });
        var client = f.As(pat.Email); var query = $"/api/v1/users?ids={Ids(pat, lee, sam, kim)}";
        await Flag(true, async () => {
            var rows = await (await client.GetAsync(query + $"&projectId={p.Id}")).Json();
            Assert.Equal(new[] { pat.Id, lee.Id, sam.Id, kim.Id }, Ordered(rows));
            Assert.Equal(new[] { false, true, true, false }, rows.AsArray().Select(row => row!["onProject"]!.GetValue<bool>()));
            Assert.Equal(new[] { pat.Id, lee.Id }, Ordered(await (await client.GetAsync(query + $"&projectId={p.Id}&limit=2")).Json()));
            var noProject = await (await client.GetAsync(query)).Json();
            Assert.Equal(new[] { kim.Id, lee.Id, sam.Id, pat.Id }, Ordered(noProject)); Assert.All(noProject.AsArray(), row => Assert.Null(row!["onProject"]));
        });
    }

    [Fact, Trait("Spec", "AC-INT-07")]
    public async Task Name_and_email_matches_precede_new_job_and_home_discipline_matches_inside_each_tier()
    {
        var p = await data.Project(); var civil = await data.Discipline("Civil");
        AppUser title = await Person("A title only", "Civil engineer"), home = await Person("B discipline only", discipline: civil), named = await Person("Y Civil name"), emailed = await Person("Z email match");
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == emailed.Id)).Email = $"civil-{Guid.NewGuid():N}@hub.test"; return await db.SaveChangesAsync(); });
        var query = $"/api/v1/users?ids={Ids(title, home, named, emailed)}&q=%20cIvIl%20";
        await Flag(true, async () => {
            Assert.Equal(new[] { named.Id, emailed.Id, title.Id, home.Id }, Ordered(await (await Admin.GetAsync(query)).Json()));
            await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = home.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now }); return await db.SaveChangesAsync(); });
            Assert.Equal(new[] { home.Id, named.Id, emailed.Id, title.Id }, Ordered(await (await Admin.GetAsync(query + $"&projectId={p.Id}")).Json()));
        });
    }

    [Fact, Trait("Spec", "AC-INT-01"), Trait("flag-off", "picker_project_rank")]
    public async Task Off_ignores_project_and_preserves_search_order_shape_and_inactive_rules()
    {
        AppUser title = await Person("A title only", "Civil engineer"), named = await Person("Y Civil name"), inactive = await Person("Z Civil inactive", active: false);
        var query = $"/api/v1/users?ids={Ids(title, named)}&q=civil";
        await Flag(false, async () => {
            var before = await (await Admin.GetAsync(query)).Json();
            var ignored = await (await Admin.GetAsync(query + $"&projectId={Guid.NewGuid()}")).Json();
            Assert.True(JsonNode.DeepEquals(before, ignored)); Assert.Equal(new[] { named.Id }, Ordered(before));
            Assert.Equal(new[] { "displayName", "email", "homeDisciplineId", "id", "isActive", "jobTitle", "officeId", "supervisorId" }, before[0]!.AsObject().Select(pair => pair.Key).Order());
            var contract = await (await Admin.GetAsync("/api/v1/openapi.json")).Json();
            var response = contract["paths"]!["/api/v1/users"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["schema"]!;
            var schemaName = response["items"]!["$ref"]!.GetValue<string>().Split('/')[^1];
            var schema = contract["components"]!["schemas"]![schemaName]!;
            Assert.DoesNotContain(schema["required"]!.AsArray(), field => field!.GetValue<string>() == "onProject");
            foreach (var field in new[] { "displayName", "email" }) Assert.Contains(schema["properties"]![field]!["type"]!.AsArray(), kind => kind!.GetValue<string>() == "null");
            Assert.Empty((await (await Admin.GetAsync($"/api/v1/users?q={inactive.Email}")).Json()).AsArray());
            Assert.Equal(new[] { inactive.Id }, Ordered(await (await Admin.GetAsync($"/api/v1/users?q={inactive.Email}&active=false")).Json()));
        });
        await Flag(true, async () => {
            Assert.Empty((await (await Admin.GetAsync($"/api/v1/users?q={inactive.Email}")).Json()).AsArray());
            Assert.Equal(new[] { inactive.Id }, Ordered(await (await Admin.GetAsync($"/api/v1/users?ids={inactive.Id}")).Json()));
        });
    }

    [Fact, Trait("Spec", "AC-INT-07")]
    public async Task Unreadable_project_is_indistinguishable_from_missing_even_with_no_matching_people()
    {
        var p = await data.Project(); await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await Flag(true, async () => {
            var client = f.As(TestData.Rita);
            var denied = await (await client.GetAsync($"/api/v1/users?projectId={p.Id}&q=no-such-person")).Json(404);
            var missing = await (await client.GetAsync($"/api/v1/users?projectId={Guid.NewGuid()}&q=no-such-person")).Json(404);
            Assert.Equal(denied.S("code"), missing.S("code"));
        });
    }

    [Fact, Trait("Spec", "AC-INT-06")]
    public async Task Recent_window_is_inclusive_and_uses_the_configured_setting()
    {
        var p = await data.Project(); AppUser boundary = await Person("A boundary"), outside = await Person("B outside");
        await f.DbAsync(async db => { foreach (var (u, days) in new[] { (boundary, 90), (outside, 91) }) db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = u.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now.AddDays(-150), RemovedAt = f.Clock.Now.AddDays(-days) }); return await db.SaveChangesAsync(); });
        var previous = (await (await Admin.GetAsync("/api/v1/admin/settings")).Json()).AsArray().Single(row => row!.S("key") == "picker_recent_days")!["value"]!.GetValue<int>();
        await Flag(true, async () => {
            try {
                foreach (var (days, expected) in new[] { (90, new[] { true, false }), (91, new[] { true, true }), (0, new[] { false, false }) }) {
                    await (await Admin.Put("/api/v1/admin/settings/picker_recent_days", new { value = days })).Json();
                    var rows = await (await Admin.GetAsync($"/api/v1/users?projectId={p.Id}&ids={Ids(boundary, outside)}")).Json();
                    Assert.Equal(expected, rows.AsArray().Select(row => row!["onProject"]!.GetValue<bool>()));
                }
            } finally { await (await Admin.Put("/api/v1/admin/settings/picker_recent_days", new { value = previous })).Json(); }
        });
    }

    [Fact]
    public void Recent_setting_has_a_valid_default_and_uses_the_existing_integer_bounds()
    {
        var def = OrgSettings.Def("picker_recent_days")!; Assert.Equal(90, def.Default); Assert.Equal(90, new OrgSettings().PickerRecentDays);
        Assert.Equal(30, OrgSettings.From(new Dictionary<string, JsonElement> { [def.Key] = JsonSerializer.SerializeToElement(30) }).PickerRecentDays);
        foreach (var valid in new[] { 0, 90, 3650 }) Assert.Null(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(valid)));
        foreach (var invalid in new[] { -1, 3651 }) Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(invalid)));
    }
}
