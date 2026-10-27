using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 051 Part C, slice S10 (§55.4, FR-ADR-07): the Portfolio page's admin strip, the last recorded review's date and
/// counts for Executives and nothing else. Each test gets its own database.
public sealed class AdminReviewStripTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public AdminReviewStripTests() => data = new TestData(f);
    public void Dispose() => f.Dispose();

    HttpClient Admin => f.As(TestData.Admin);
    const string Strip = "/api/v1/portfolio/admin-review";

    [Fact]
    [Trait("flag-off", "admin_review")]
    public async Task With_the_flag_off_the_strip_is_absent_for_everyone() // AC-ADR-06, FR-ADR-08
    {
        foreach (var (who, status) in new[] { (TestData.Lena, HttpStatusCode.NotFound), (TestData.Admin, HttpStatusCode.NotFound), (TestData.Sam, HttpStatusCode.Forbidden) })
            Assert.Equal(status, (await f.As(who).GetAsync(Strip)).StatusCode);
        var portfolio = await f.As(TestData.Lena).GetAsync("/api/v1/portfolio").Result.Json();
        Assert.Equal(["ownOnly", "projects", "tiles"], portfolio.AsObject().Select(p => p.Key).Order()); // the page's own answer is untouched
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task Executives_see_the_last_reviews_date_and_counts_and_nothing_else() // AC-ADR-06, FR-ADR-07
    {
        (await Admin.Put("/api/v1/admin/settings/flag.admin_review", new { value = true })).EnsureSuccessStatusCode();
        await data.Project();

        // Nothing recorded yet: the strip says so with no counts.
        var none = await f.As(TestData.Lena).GetAsync(Strip).Result.Json();
        Assert.Null(none["reviewedAt"]);
        Assert.Empty(none["checks"]!.AsArray());

        f.Clock.Now = f.Clock.Now.AddHours(1);
        (await Admin.Post("/api/v1/admin/review", new { note = "Private note", checks = new[] { new { key = "noManager", note = "Private check note" } } })).EnsureSuccessStatusCode();
        // A change after the review moves the page's live count but not the recorded one the strip shows.
        var alex = f.Db(db => db.Users.Where(u => u.Email == TestData.Alex).Select(u => new { u.Id, u.RowVersion }).First());
        (await Admin.Patch($"/api/v1/admin/users/{alex.Id}", new { isActive = false }, alex.RowVersion)).EnsureSuccessStatusCode();

        var live = await Admin.GetAsync("/api/v1/admin/review").Result.Json();
        Assert.Equal(1, live["checks"]!.AsArray().Single(c => c!.S("key") == "inactiveOnTeams")!.I("count"));
        foreach (var who in new[] { TestData.Lena, TestData.Admin })
        {
            var strip = await f.As(who).GetAsync(Strip).Result.Json();
            Assert.Equal(["checks", "reviewedAt"], strip.AsObject().Select(p => p.Key).Order()); // no reviewer, no notes
            Assert.Equal(new DateTimeOffset(f.Clock.Now.UtcDateTime), DateTimeOffset.Parse(strip.S("reviewedAt")));
            var checks = strip["checks"]!.AsArray();
            Assert.Equal(live["last"]!["checks"]!.AsArray().Select(c => c!.S("key")), checks.Select(c => c!.S("key")));
            Assert.All(checks, c => Assert.Equal(["count", "key", "title"], c!.AsObject().Select(p => p.Key).Order()));
            Assert.DoesNotContain("adminReview.", string.Join("", checks.Select(c => c!.S("title"))));
            Assert.Equal(0, checks.Single(c => c!.S("key") == "inactiveOnTeams")!.I("count")); // as recorded, not as now
            Assert.DoesNotContain("Private", strip.ToJsonString());
        }

        foreach (var who in new[] { TestData.Sam, TestData.Pm, TestData.Rita })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).GetAsync(Strip)).StatusCode);
    }
}
