using System.Net;
using Hub.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

/// Packet 001: sign-in, roles, reference data, settings, audit (AC-AUTH-01..04, AC-AUD-04, FR-015, FR-027).
[Collection("api")]
public sealed class FoundationsTests(HubFactory f)
{
    const string Admin = "jordan@hub.test";

    [Fact]
    public async Task First_sign_in_creates_a_standard_user() // AC-AUTH-01
    {
        var email = $"new{Guid.NewGuid():N}"[..12] + "@hub.test";
        var me = await f.As(email).GetAsync("/api/v1/me").Result.Json();
        Assert.Equal(email, me.S("email"));
        Assert.Empty(me["systemRoles"]!.AsArray());
        Assert.False(me["capabilities"]!["createProject"]!.GetValue<bool>());
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemKey == email && a.Action == "Provisioned")));
    }

    [Fact]
    public async Task Requests_without_sign_in_are_refused_except_health() // AC-AUTH-04
    {
        var anon = f.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/reference")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anon.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Group_roles_follow_the_directory_and_manual_roles_stay() // AC-AUTH-03, US3 scenario 2
    {
        var email = $"grp{Guid.NewGuid():N}"[..12] + "@hub.test";
        var asPm = await f.As(email, "Hub.ProjectManager").GetAsync("/api/v1/me").Result.Json();
        Assert.True(asPm["capabilities"]!["createProject"]!.GetValue<bool>());
        var id = asPm.G("id");
        (await f.As(Admin).Post($"/api/v1/admin/users/{id}/roles", new { role = "Supervisor" })).EnsureSuccessStatusCode();

        var after = await f.As(email, "none").GetAsync("/api/v1/me").Result.Json();
        Assert.False(after["capabilities"]!["createProject"]!.GetValue<bool>());
        var roles = after["roles"]!.AsArray().Select(r => (r!["role"]!.GetValue<string>(), r["source"]!.GetValue<string>())).ToList();
        Assert.Equal([("Supervisor", "Manual")], roles);
    }

    [Fact]
    public async Task Administration_is_refused_to_non_admins()
    {
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As("priya@hub.test").GetAsync("/api/v1/admin/disciplines")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As("priya@hub.test").Put("/api/v1/admin/settings/task_due_soon_days", new { value = 3 })).StatusCode);
    }

    [Fact]
    public async Task Threshold_change_is_logged_with_old_and_new_values() // US2 scenario 3
    {
        (await f.As(Admin).Put("/api/v1/admin/settings/review_stale_days", new { value = 6 })).EnsureSuccessStatusCode();
        var row = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemKey == "review_stale_days").OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).FirstAsync());
        Assert.Contains("\"old\": 5", row.Changes);
        Assert.Contains("\"new\": 6", row.Changes);
        Assert.Equal("Admin", row.ActorType);
        Assert.NotNull(row.ActorUserId);
        (await f.As(Admin).Put("/api/v1/admin/settings/review_stale_days", new { value = 5 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "Mars/Olympus" })).StatusCode);
    }

    [Fact]
    public async Task Coordination_lookahead_is_bounded_and_visible_to_project_users()
    {
        const string path = "/api/v1/admin/settings/coordination_lookahead_weeks";
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(Admin).Put(path, new { value = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(Admin).Put(path, new { value = 13 })).StatusCode);
        try
        {
            (await f.As(Admin).Put(path, new { value = 5 })).EnsureSuccessStatusCode();
            var me = await (await f.As(TestData.Alex).GetAsync("/api/v1/me")).Json();
            Assert.Equal(5, me["settings"]!["coordinationLookaheadWeeks"]!.GetValue<int>());
        }
        finally { (await f.As(Admin).Put(path, new { value = 3 })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task Reference_data_is_deactivated_not_deleted_with_usage_count() // FR-015, E-18
    {
        var admin = f.As(Admin);
        var d = await admin.Post("/api/v1/admin/disciplines", new { name = "Hydrology " + Guid.NewGuid().ToString("N")[..4], code = "HYD", colour = "#0ea5e9" }).Result.Json(201);
        var usage = await admin.GetAsync($"/api/v1/admin/disciplines/{d.S("id")}/usage").Result.Json();
        Assert.Equal(0, usage.I("projects"));
        (await admin.Patch($"/api/v1/admin/disciplines/{d.S("id")}", new { isActive = false }, d.I("rowVersion"))).EnsureSuccessStatusCode();
        var refs = await admin.GetAsync("/api/v1/reference").Result.Json();
        var row = refs["disciplines"]!.AsArray().First(x => x!["id"]!.GetValue<string>() == d.S("id"))!;
        Assert.False(row["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Stale_version_is_refused_with_409() // G-07
    {
        var admin = f.As(Admin);
        var d = await admin.Post("/api/v1/admin/clients", new { name = "Client " + Guid.NewGuid().ToString("N")[..6] }).Result.Json(201);
        (await admin.Patch($"/api/v1/admin/clients/{d.S("id")}", new { shortName = "A" }, d.I("rowVersion"))).EnsureSuccessStatusCode();
        var stale = await admin.Patch($"/api/v1/admin/clients/{d.S("id")}", new { shortName = "B" }, d.I("rowVersion"));
        var body = await stale.Json(409);
        Assert.Equal("concurrency_conflict", body.S("code"));
        Assert.Equal("Jordan Lee", body.S("changedBy"));
    }

    [Fact]
    public async Task Last_active_admin_cannot_lose_the_role() // FR-027
    {
        var jordan = await f.DbAsync(db => db.Users.FirstAsync(u => u.Email == Admin));
        var r = await f.As(Admin).DeleteAsync($"/api/v1/admin/users/{jordan.Id}/roles/Admin");
        Assert.Equal("last_admin", (await r.Json(422)).S("code"));
        var self = await f.As(Admin).Patch($"/api/v1/admin/users/{jordan.Id}", new { supervisorId = jordan.Id }, jordan.RowVersion);
        Assert.Equal(HttpStatusCode.BadRequest, self.StatusCode);
    }

    [Fact]
    public async Task Inactive_people_are_refused_and_hidden_from_pickers() // FR-005, G-11
    {
        var email = $"leaver{Guid.NewGuid():N}"[..14] + "@hub.test";
        var me = await f.As(email).GetAsync("/api/v1/me").Result.Json();
        var u = await f.DbAsync(db => db.Users.FirstAsync(x => x.Email == email));
        (await f.As(Admin).Patch($"/api/v1/admin/users/{u.Id}", new { isActive = false }, u.RowVersion)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(email).GetAsync("/api/v1/me")).StatusCode);
        var picker = await f.As(Admin).GetAsync($"/api/v1/users?q={email}").Result.Json();
        Assert.Empty(picker.AsArray());
        Assert.NotNull(me);
    }

    [Fact]
    public async Task Activity_log_rows_cannot_be_changed_or_deleted() // AC-AUD-04
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() => f.DbAsync(db => db.Database.ExecuteSqlRawAsync("UPDATE hub.activity_log SET action = 'x'")));
        Assert.Contains("append-only", ex.MessageText);
        await Assert.ThrowsAsync<PostgresException>(() => f.DbAsync(db => db.Database.ExecuteSqlRawAsync("DELETE FROM hub.activity_log")));
    }

    [Fact]
    public async Task Outside_development_only_Entra_tokens_sign_in() // FR-AUTH-01, threat model T-02
    {
        using var prod = f.WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Production");
            b.UseSetting("Auth:Mode", "Development"); // even if someone sets it, production never honours the header
            b.UseSetting("Auth:Entra:TenantId", "00000000-0000-0000-0000-000000000001");
            b.UseSetting("Auth:Entra:Audience", "api://hub-test");
        });
        var c = prod.CreateClient();
        c.DefaultRequestHeaders.Add("X-Dev-User", TestData.Admin);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal("Entra", (await c.GetAsync("/api/v1/config").Result.Json()).S("authMode"));
        c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-token");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/v1/projects")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_API_path_is_a_404_problem_not_the_application_page() // §25.6
    {
        var r = await f.As(TestData.Alex).GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
    }
}
