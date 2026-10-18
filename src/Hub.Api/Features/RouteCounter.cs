using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Aggregate-only route counter. The release flag and lasting organisation setting are
/// checked here; callers may send a path but only its allowlisted pattern is persisted.
public static class RouteCounterEndpoints
{
    public sealed record OpenBody(string Route, int ElapsedMs);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/route-counts", async (OpenBody body, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var settings = await store.Get(db);
            if (!settings.IsFlagOn("route_counter") || !settings.RouteCounterEnabled) return Results.NoContent();
            var route = RouteCounter.Match(body.Route);
            if (route is null) return Results.NoContent();
            var ms = Math.Clamp(body.ElapsedMs, 0, 60000);
            var day = clock.Today(settings);
            var band = RouteCounter.Band(TimeSpan.FromMilliseconds(ms));
            var under500 = band == "under-500" ? 1 : 0; var from500 = band == "500-999" ? 1 : 0;
            var from1000 = band == "1000-1999" ? 1 : 0; var from2000 = band == "2000-4999" ? 1 : 0; var over5000 = band == "5000+" ? 1 : 0;
            await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO hub.route_count
                (id, day, route_key, opens, under500_ms, from500_to999_ms, from1000_to1999_ms, from2000_to4999_ms, over5000_ms)
                VALUES ({Guid.NewGuid()}, {day}, {route}, 1, {under500}, {from500}, {from1000}, {from2000}, {over5000})
                ON CONFLICT (day, route_key) DO UPDATE SET opens = route_count.opens + 1,
                    under500_ms = route_count.under500_ms + EXCLUDED.under500_ms,
                    from500_to999_ms = route_count.from500_to999_ms + EXCLUDED.from500_to999_ms,
                    from1000_to1999_ms = route_count.from1000_to1999_ms + EXCLUDED.from1000_to1999_ms,
                    from2000_to4999_ms = route_count.from2000_to4999_ms + EXCLUDED.from2000_to4999_ms,
                    over5000_ms = route_count.over5000_ms + EXCLUDED.over5000_ms");
            return Results.NoContent();
        }).RequireRateLimiting("route-counter");
    }
}
