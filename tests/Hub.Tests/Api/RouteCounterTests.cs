using System.Net;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class RouteCounterTests(HubFactory f)
{
    async Task Enabled(bool on)
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.route_counter", new { value = on }).Result.Json();
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/route_counter_enabled", new { value = on }).Result.Json();
    }

    [Fact, Trait("flag-off", "route_counter")] // AC-PRO-09, AC-PRO-23
    public async Task Both_gates_off_store_nothing()
    {
        await Enabled(false);
        var before = await f.DbAsync(db => db.RouteCounts.SumAsync(r => r.Opens));
        (await f.As(TestData.Pm).Post("/api/v1/route-counts", new { route = "my-work", elapsedMs = 100 })).EnsureSuccessStatusCode();
        Assert.Equal(before, await f.DbAsync(db => db.RouteCounts.SumAsync(r => r.Opens)));
    }

    [Fact, Trait("flag-on", "route_counter")] // AC-PRO-08
    public async Task Raw_urls_queries_and_numeric_routes_are_dropped()
    {
        await Enabled(true);
        try
        {
            var before = await f.DbAsync(db => db.RouteCounts.SumAsync(r => r.Opens));
            foreach (var route in new[] { "projects/123/tasks?secret=x", "/projects/123/tasks", "123" })
                (await f.As(TestData.Pm).Post("/api/v1/route-counts", new { route, elapsedMs = 100 })).EnsureSuccessStatusCode();
            Assert.Equal(before, await f.DbAsync(db => db.RouteCounts.SumAsync(r => r.Opens)));
        }
        finally { await Enabled(false); }
    }

    [Fact, Trait("flag-on", "route_counter")] // AC-PRO-08
    public async Task Each_band_aggregates_by_day_and_route_without_identity_columns()
    {
        await Enabled(true);
        try
        {
            foreach (var ms in new[] { 0, 499, 500, 999, 1000, 1999, 2000, 4999, 5000, 60000 })
                (await f.As(TestData.Pm).Post("/api/v1/route-counts", new { route = "my-work", elapsedMs = ms })).EnsureSuccessStatusCode();
            var row = await f.DbAsync(db => db.RouteCounts.SingleAsync(x => x.RouteKey == "my-work"));
            Assert.Equal(10, row.Opens);
            Assert.Equal(10, row.Under500Ms + row.From500To999Ms + row.From1000To1999Ms + row.From2000To4999Ms + row.Over5000Ms);
            var model = f.Db(db => db.Model.FindEntityType(typeof(RouteCount))!);
            Assert.DoesNotContain(model.GetProperties(), p => p.Name.Contains("User", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Session", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Item", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Text", StringComparison.OrdinalIgnoreCase) || p.Name.Contains("Timestamp", StringComparison.OrdinalIgnoreCase));
        }
        finally { await Enabled(false); }
    }

    [Fact, Trait("flag-on", "route_counter")] // AC-PRO-08
    public async Task Concurrent_opens_upsert_without_lost_counts()
    {
        await Enabled(true);
        try
        {
            var before = await f.DbAsync(db => db.RouteCounts.Where(r => r.RouteKey == "preferences").Select(r => new { r.Opens, r.From500To999Ms }).SingleOrDefaultAsync());
            var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ =>
                f.As(TestData.Pm).Post("/api/v1/route-counts", new { route = "preferences", elapsedMs = 750 })));
            Assert.All(responses, r => r.EnsureSuccessStatusCode());
            var row = await f.DbAsync(db => db.RouteCounts.SingleAsync(x => x.RouteKey == "preferences"));
            Assert.Equal(8, row.Opens - (before?.Opens ?? 0));
            Assert.Equal(8, row.From500To999Ms - (before?.From500To999Ms ?? 0));
        }
        finally { await Enabled(false); }
    }

    [Fact, Trait("flag-off", "route_counter"), Trait("flag-on", "route_counter")] // AC-PRO-08, AC-PRO-09, AC-PRO-23
    public async Task Flag_off_hides_route_counter_setting_and_pilot_route_report_has_no_day_column()
    {
        await Enabled(false);
        var me = await (await f.As(TestData.Pm).GetAsync("/api/v1/me")).Json();
        Assert.False(me["settings"]!.AsObject().ContainsKey("routeCounterEnabled"));

        await Enabled(true);
        try
        {
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/route_counter_enabled", new { value = false })).Json();
            var disabled = await (await f.As(TestData.Pm).GetAsync("/api/v1/me")).Json();
            Assert.False(disabled["settings"]!["routeCounterEnabled"]!.GetValue<bool>());
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/route_counter_enabled", new { value = true })).Json();
            var enabled = await (await f.As(TestData.Pm).GetAsync("/api/v1/me")).Json();
            Assert.True(enabled["settings"]!["routeCounterEnabled"]!.GetValue<bool>());
            var reports = await (await f.As(TestData.Admin).GetAsync("/api/v1/reports")).Json();
            Assert.Contains(reports.AsArray(), r => r!["code"]?.GetValue<string>() == "pilot-route-use");
            var report = await (await f.As(TestData.Admin).GetAsync("/api/v1/reports/pilot-route-use?weeks=1")).Json();
            Assert.DoesNotContain(report["columns"]!.AsArray(), c => c!["path"]?.GetValue<string>() == "day");
            Assert.Contains(report["columns"]!.AsArray(), c => c!["path"]?.GetValue<string>() == "week");
        }
        finally { await Enabled(false); }
    }
    [Fact, Trait("flag-on", "route_counter")] // AC-PRO-08: the aggregate endpoint has its own bounded request budget.
    public async Task Route_counter_rate_limit_rejects_the_121st_request()
    {
        using var separate = new HubFactory(); var admin = separate.As(TestData.Admin); var client = separate.As(TestData.Pm);
        (await admin.Put("/api/v1/admin/settings/flag.route_counter", new { value = true })).EnsureSuccessStatusCode();
        (await admin.Put("/api/v1/admin/settings/route_counter_enabled", new { value = true })).EnsureSuccessStatusCode();
        for (var i = 0; i < 120; i++)
            (await client.Post("/api/v1/route-counts", new { route = "my-work", elapsedMs = 100 })).EnsureSuccessStatusCode();
        var limited = await client.Post("/api/v1/route-counts", new { route = "my-work", elapsedMs = 100 });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode); Assert.NotNull(limited.Headers.RetryAfter);
        Assert.Equal(120, await separate.DbAsync(db => db.RouteCounts.Where(r => r.RouteKey == "my-work").Select(r => r.Opens).SingleAsync()));
    }

}
