using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 051 Part C, slice S08 (§55.4): the nine checks, the recorded review and the Admin-only guard.
/// Each test gets its own database, so every count is exactly what the test arranged.
public sealed class AdminReviewTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public AdminReviewTests() => data = new TestData(f);
    public void Dispose() => f.Dispose();

    static readonly string[] Keys = ["inactiveOnTeams", "noManager", "inAppRoles", "noSignIn", "inactiveProjectManager", "idleProjects", "settingsChanged", "inactiveReference"];

    HttpClient Admin => f.As(TestData.Admin);

    async Task Flag(string name, bool on) => (await Admin.Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();

    async Task PatchUser(string email, object body)
    {
        var user = f.Db(db => db.Users.Where(u => u.Email == email).Select(u => new { u.Id, u.RowVersion }).First());
        (await Admin.Patch($"/api/v1/admin/users/{user.Id}", body, user.RowVersion)).EnsureSuccessStatusCode();
    }

    /// FR-ADR-02: every count on the page, then the list its link opens; returns each check's list by key.
    async Task<(JsonNode Overview, Dictionary<string, JsonArray> Lists)> CountsAndLists()
    {
        var overview = await Admin.GetAsync("/api/v1/admin/review").Result.Json();
        var lists = new Dictionary<string, JsonArray>();
        foreach (var check in overview["checks"]!.AsArray())
        {
            Assert.DoesNotContain("adminReview.", check!.S("title") + check.S("rule")); // text, never a raw key
            var list = await Admin.GetAsync("/api/v1" + check.S("listUrl")).Result.Json();
            Assert.Equal(check.S("key"), list.S("key"));
            Assert.Equal(check.I("count"), list["rows"]!.AsArray().Count);
            lists[check.S("key")] = list["rows"]!.AsArray();
        }
        return (overview, lists);
    }

    static string[] Names(JsonArray rows) => [.. rows.Select(r => r!.S("name")).Order()];

    [Fact]
    [Trait("flag-off", "admin_review")]
    public async Task With_the_flag_off_an_Admin_finds_no_review_and_others_are_refused_as_on_every_admin_route() // AC-ADR-06
    {
        foreach (var (who, status) in new[] { (TestData.Admin, HttpStatusCode.NotFound), (TestData.Sam, HttpStatusCode.Forbidden) })
        {
            var c = f.As(who);
            Assert.Equal(status, (await c.GetAsync("/api/v1/admin/review")).StatusCode);
            Assert.Equal(status, (await c.GetAsync("/api/v1/admin/review/checks/noManager")).StatusCode);
            Assert.Equal(status, (await c.Post("/api/v1/admin/review", new { note = "x" })).StatusCode);
        }
        Assert.Equal(0, await f.DbAsync(db => db.Set<AdminReview>().CountAsync()));
        Assert.False((await Admin.GetAsync("/api/v1/me").Result.Json())["flags"]!["admin_review"]!.GetValue<bool>());
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task Counts_equal_their_lists_and_show_exactly_the_arranged_records() // AC-ADR-01
    {
        await Flag("admin_review", true);
        await data.Project(); // alex on the Civil team, omar leads Electrical
        f.Clock.Now = f.Clock.Now.AddHours(1);
        (await Admin.Post("/api/v1/admin/review", new { })).EnsureSuccessStatusCode(); // the flag change is before this review

        await PatchUser(TestData.Alex, new { isActive = false });
        await PatchUser(TestData.Omar, new { isActive = false });
        var lena = data.User(TestData.Lena);
        await PatchUser(TestData.Admin, new { supervisorId = lena });
        await PatchUser(TestData.Rita, new { supervisorId = lena }); // lena is now the one active user with no manager
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        (await Admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = 6 })).EnsureSuccessStatusCode();
        f.Clock.Now = f.Clock.Now.AddDays(1); // the setting changed yesterday

        var (overview, lists) = await CountsAndLists();
        Assert.Equal(Keys, overview["checks"]!.AsArray().Select(c => c!.S("key")));
        int Count(string key) => overview["checks"]!.AsArray().Single(c => c!.S("key") == key)!.I("count");
        Assert.Equal(2, Count("inactiveOnTeams"));
        Assert.Equal(1, Count("noManager"));
        Assert.Equal(1, Count("settingsChanged"));
        Assert.Equal(new[] { data.User(TestData.Alex), data.User(TestData.Omar) }.Order(), lists["inactiveOnTeams"].Select(r => r!.G("id")).Order());
        Assert.Equal([lena], lists["noManager"].Select(r => r!.G("id")));
        Assert.Equal(["task_due_soon_days"], Names(lists["settingsChanged"]));
        Assert.Equal($"/people/{data.User(TestData.Alex)}/reassign-work", lists["inactiveOnTeams"].Single(r => r!.G("id") == data.User(TestData.Alex))!.S("link"));
        Assert.Equal(0, Count("inactiveProjectManager"));
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task Every_check_counts_what_its_list_shows() // FR-ADR-01, FR-ADR-02
    {
        await Flag("admin_review", true);
        var marcs = await data.Project(pm: TestData.Marc);
        var client = await f.DbAsync(db => db.Clients.SingleAsync(c => c.Id == marcs.ClientId));
        (await Admin.Patch($"/api/v1/admin/clients/{client.Id}", new { isActive = false }, client.RowVersion)).EnsureSuccessStatusCode();
        await PatchUser(TestData.Marc, new { isActive = false });
        f.Clock.Now = f.Clock.Now.AddDays(100); // past admin_review_idle_days and status_stale_days
        (await f.As(TestData.Alex).PostAsync("/api/v1/me/sign-in", null)).EnsureSuccessStatusCode();

        var (overview, lists) = await CountsAndLists();
        Assert.Equal([marcs.Id], lists["inactiveProjectManager"].Select(r => r!.G("id")));
        Assert.Equal([marcs.Id], lists["idleProjects"].Select(r => r!.G("id")));
        Assert.Equal([client.Id], lists["inactiveReference"].Select(r => r!.G("id")));
        Assert.Equal("clients", lists["inactiveReference"][0]!.S("detail"));
        Assert.Contains(data.User(TestData.Marc), lists["inactiveOnTeams"].Select(r => r!.G("id")));
        // Seeded roles were all granted in the Hub; marc's no longer count once he is inactive.
        Assert.Equal(6, lists["inAppRoles"].Count);
        Assert.DoesNotContain(data.User(TestData.Marc), lists["inAppRoles"].Select(r => r!.G("id")));
        Assert.DoesNotContain(data.User(TestData.Alex), lists["noSignIn"].Select(r => r!.G("id"))); // signed in today
        Assert.Contains(data.User(TestData.Jill), lists["noSignIn"].Select(r => r!.G("id")));
        Assert.Equal("No sign-in for 90 days", overview["checks"]!.AsArray().Single(c => c!.S("key") == "noSignIn")!.S("title"));
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync("/api/v1/admin/review/checks/nothing")).StatusCode);
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task The_home_unit_check_appears_only_with_the_structure_flag() // FR-ADR-01, FR-UNT-07
    {
        await Flag("admin_review", true);
        var (overview, _) = await CountsAndLists();
        Assert.DoesNotContain("noHomeUnit", overview["checks"]!.AsArray().Select(c => c!.S("key")));
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync("/api/v1/admin/review/checks/noHomeUnit")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.Post("/api/v1/admin/review", new { checks = new[] { new { key = "noHomeUnit", note = "x" } } })).StatusCode);

        await Flag("org_structure", true);
        await f.DbAsync(async db =>
        {
            var level = new OrgLevel { Name = "Review level", SortOrder = (await db.Set<OrgLevel>().MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1 };
            var unit = new OrgUnit { Name = "Review unit", LevelId = level.Id };
            db.AddRange(level, unit);
            (await db.Users.SingleAsync(u => u.Email == TestData.Alex)).UnitId = unit.Id;
            return await db.SaveChangesAsync();
        });
        (overview, var lists) = await CountsAndLists();
        Assert.Equal(9, overview["checks"]!.AsArray().Count);
        Assert.Equal("noHomeUnit", overview["checks"]![2]!.S("key"));
        Assert.Equal(9, lists["noHomeUnit"].Count); // ten seeded people, alex placed
        Assert.DoesNotContain(data.User(TestData.Alex), lists["noHomeUnit"].Select(r => r!.G("id")));
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task A_recorded_review_keeps_its_counts_and_notes_and_is_never_changed() // AC-ADR-02, FR-ADR-03, FR-ADR-04
    {
        await Flag("admin_review", true);
        await data.Project();
        var before = await Admin.GetAsync("/api/v1/admin/review").Result.Json();
        Assert.Null(before["last"]);
        Assert.True(before["overdue"]!.GetValue<bool>()); // never recorded

        f.Clock.Now = f.Clock.Now.AddHours(1);
        var recorded = await Admin.Post("/api/v1/admin/review", new
        {
            note = "Quarterly access review",
            checks = new[] { new { key = "inactiveOnTeams", note = "Checked with HR" } },
        }).Result.Json(201);
        Assert.Equal("Jordan Lee", recorded.S("reviewer"));
        Assert.Equal(1, await f.DbAsync(db => db.Set<AdminReview>().CountAsync()));
        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemType == "AdminReview").ToListAsync());
        Assert.Equal(recorded.G("id"), Assert.Single(log).ItemId);
        Assert.Equal("Recorded", log[0].Action);

        await PatchUser(TestData.Alex, new { isActive = false });
        var after = await Admin.GetAsync("/api/v1/admin/review").Result.Json();
        var last = after["last"]!;
        Assert.Equal(recorded.G("id"), last.G("id"));
        Assert.Equal("Quarterly access review", last.S("note"));
        Assert.Equal(Keys, last["checks"]!.AsArray().Select(c => c!.S("key")));
        var then = last["checks"]!.AsArray().Single(c => c!.S("key") == "inactiveOnTeams")!;
        Assert.Equal("Checked with HR", then.S("note"));
        Assert.Equal(0, then.I("count"));
        Assert.Equal(1, after["checks"]!.AsArray().Single(c => c!.S("key") == "inactiveOnTeams")!.I("count")); // then and now
        Assert.Equal(DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(90).ToString("yyyy-MM-dd"), after.S("dueOn")); // 11:00 in Halifax
        Assert.False(after["overdue"]!.GetValue<bool>());

        // Unknown keys are refused and record nothing; a second review is a new record beside the first.
        Assert.Equal(HttpStatusCode.BadRequest, (await Admin.Post("/api/v1/admin/review", new { checks = new[] { new { key = "invented", note = "x" } } })).StatusCode);
        f.Clock.Now = f.Clock.Now.AddDays(91);
        Assert.True((await Admin.GetAsync("/api/v1/admin/review").Result.Json())["overdue"]!.GetValue<bool>());
        (await Admin.Post("/api/v1/admin/review", new { note = "Correction" })).EnsureSuccessStatusCode();
        Assert.Equal(2, await f.DbAsync(db => db.Set<AdminReview>().CountAsync()));
        Assert.Equal("Quarterly access review", await f.DbAsync(db => db.Set<AdminReview>().Where(r => r.Id == recorded.G("id")).Select(r => r.Note).SingleAsync()));

        // No edit or delete path exists, and the store refuses both.
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.Patch($"/api/v1/admin/review/{recorded.S("id")}", new { note = "edited" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.Delete($"/api/v1/admin/review/{recorded.S("id")}", null)).StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db =>
        {
            (await db.Set<AdminReview>().SingleAsync(r => r.Id == recorded.G("id"))).Note = "edited";
            return await db.SaveChangesAsync();
        }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db =>
        {
            db.Remove(await db.Set<AdminReview>().SingleAsync(r => r.Id == recorded.G("id")));
            return await db.SaveChangesAsync();
        }));
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task Only_a_System_Administrator_may_open_or_record_it() // AC-ADR-04, FR-ADR-07
    {
        await Flag("admin_review", true);
        foreach (var who in new[] { TestData.Sam, TestData.Pm, TestData.Lena })
        {
            var c = f.As(who);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/review")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/v1/admin/review/checks/noManager")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await c.Post("/api/v1/admin/review", new { note = "x" })).StatusCode);
        }
        Assert.Equal(0, await f.DbAsync(db => db.Set<AdminReview>().CountAsync()));
    }
}
