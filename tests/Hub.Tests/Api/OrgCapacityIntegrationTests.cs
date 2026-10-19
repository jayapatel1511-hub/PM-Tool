using System.Net;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class OrgCapacityIntegrationTests(HubFactory f)
{
    [Fact] // AC-UNT-08, FR-UNT-16, FR-UNT-19
    public async Task Team_capacity_uses_end_of_week_home_unit_and_descendant_filter()
    {
        var email = $"org-capacity-{Guid.NewGuid():N}@hub.test";
        var person = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id");
        var week = new DateOnly(2026, 10, 19); // week 43; the move is effective at week 44.
        async Task SetFlag(string flag, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{flag}", new { value })).EnsureSuccessStatusCode();
        var savedClock = f.Clock.Now;
        await SetFlag("org_structure", false);
        await SetFlag("capacity_reports", false);
        try
        {
            var ids = await f.DbAsync(async db =>
            {
                var maxSort = await db.Set<OrgLevel>().Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
                var levelRoot = new OrgLevel { Name = $"Region {Guid.NewGuid():N}", SortOrder = maxSort + 1 };
                var levelTeam = new OrgLevel { Name = $"Team {Guid.NewGuid():N}", SortOrder = maxSort + 2 };
                db.Set<OrgLevel>().AddRange(levelRoot, levelTeam);
                var rootUnit = new OrgUnit { Name = $"Atlantic {Guid.NewGuid():N}", LevelId = levelRoot.Id, LeaderId = await db.Users.Where(u => u.Email == TestData.Jill).Select(u => u.Id).SingleAsync() };
                var civilUnit = new OrgUnit { Name = $"Civil {Guid.NewGuid():N}", LevelId = levelTeam.Id, ParentId = rootUnit.Id };
                var structuralUnit = new OrgUnit { Name = $"Structural {Guid.NewGuid():N}", LevelId = levelTeam.Id, ParentId = rootUnit.Id };
                db.Set<OrgUnit>().AddRange(rootUnit, civilUnit, structuralUnit);
                await db.SaveChangesAsync();
                return (Root: rootUnit.Id, Civil: civilUnit.Id, Structural: structuralUnit.Id);
            });
            var root = ids.Root; var civil = ids.Civil; var structural = ids.Structural;

            await SetFlag("org_structure", true);
            f.Clock.Now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
            await f.DbAsync(async db =>
            {
                var user = await db.Users.SingleAsync(u => u.Id == person);
                user.SupervisorId = await db.Users.Where(u => u.Email == TestData.Sam).Select(u => u.Id).SingleAsync();
                user.WeeklyCapacityHours = 40m;
                user.UnitId = civil;
                return await db.SaveChangesAsync();
            });
            f.Clock.Now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
            await f.DbAsync(async db =>
            {
                (await db.Users.SingleAsync(u => u.Id == person)).UnitId = structural;
                return await db.SaveChangesAsync();
            });
            await SetFlag("capacity_reports", true);

            var catalogue = await (await f.As(TestData.Jill).GetAsync("/api/v1/reports")).Json();
            Assert.Contains(catalogue.AsArray().Single(r => r!.S("code") == "team-capacity")!["params"]!.AsArray(), p => p!.S("key") == "unitId");
            var leaderReport = await (await f.As(TestData.Jill).GetAsync($"/api/v1/reports/team-capacity?from={week:yyyy-MM-dd}&weeks=1&unitId={root}")).Json();
            Assert.Contains(leaderReport["rows"]!.AsArray(), r => r!.S("unit").StartsWith("Civil ", StringComparison.Ordinal)
                && r.S("measure") == "Capacity" && r["w0"]!.GetValue<decimal>() == 40m);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Post("/api/v1/planning/entries", new
            { personId = person, hoursPerWeek = 4m, startWeek = week, endWeek = week, label = "Read scope grants no planning write" })).StatusCode);

            var rootReport = await (await f.As(TestData.Admin).GetAsync($"/api/v1/reports/team-capacity?from={week:yyyy-MM-dd}&weeks=1&unitId={root}")).Json();
            Assert.Contains(rootReport["columns"]!.AsArray(), c => c!.S("path") == "unit");
            Assert.Contains(rootReport["rows"]!.AsArray(), r => r!.S("unit").StartsWith("Civil ", StringComparison.Ordinal)
                && r.S("measure") == "Capacity" && r["w0"]!.GetValue<decimal>() == 40m);
            Assert.DoesNotContain(rootReport["rows"]!.AsArray(), r => r!.S("unit").StartsWith("Structural ", StringComparison.Ordinal)
                && r.S("measure") == "Capacity" && r["w0"]!.GetValue<decimal>() == 40m);

            var civilReport = await (await f.As(TestData.Admin).GetAsync($"/api/v1/reports/team-capacity?from={week:yyyy-MM-dd}&weeks=1&unitId={civil}")).Json();
            Assert.All(civilReport["rows"]!.AsArray(), r => Assert.StartsWith("Civil ", r!.S("unit")));

            await SetFlag("org_structure", false);
            var offCatalogue = await (await f.As(TestData.Admin).GetAsync("/api/v1/reports")).Json();
            Assert.DoesNotContain(offCatalogue.AsArray().Single(r => r!.S("code") == "team-capacity")!["params"]!.AsArray(), p => p!.S("key") == "unitId");
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).GetAsync($"/api/v1/reports/team-capacity?from={week:yyyy-MM-dd}&weeks=1")).StatusCode);
            var off = await (await f.As(TestData.Admin).GetAsync($"/api/v1/reports/team-capacity?from={week:yyyy-MM-dd}&weeks=1&unitId={root}")).Json();
            Assert.DoesNotContain(off["columns"]!.AsArray(), c => c!.S("path") is "unit" or "unitPath");
            Assert.DoesNotContain(off["rows"]!.AsArray(), r => r!.AsObject().ContainsKey("unit"));
        }
        finally
        {
            await SetFlag("org_structure", false);
            await SetFlag("capacity_reports", false);
            f.Clock.Now = savedClock;
        }
    }
}
