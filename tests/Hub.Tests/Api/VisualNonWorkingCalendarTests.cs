using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Non-working presentation comes from the person's effective calendar, never zero available hours.
public sealed class VisualNonWorkingCalendarTests : IDisposable
{
    readonly HubFactory f = new();
    static readonly DateOnly W = new(2026, 9, 14);
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    async Task<(Guid Id, string Email)> Person(decimal hours = 40)
    {
        var email = $"synthetic-calendar-{Guid.NewGuid():N}@hub.test";
        var id = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id");
        // Synthetic person reference configuration; operational schedules/holidays below use ordinary commands.
        await f.DbAsync(async db => { var u = await db.Users.SingleAsync(u => u.Id == id); u.SupervisorId = new TestData(f).User(TestData.Sam); u.WeeklyCapacityHours = hours; return await db.SaveChangesAsync(); });
        return (id, email);
    }
    async Task<JsonNode> Availability(Guid id, string who = TestData.Sam)
        => await (await f.As(who).GetAsync($"/api/v1/planning/availability?from={W:yyyy-MM-dd}&through={W.AddDays(6):yyyy-MM-dd}&personId={id}")).Json();
    async Task<JsonNode> Cell(Guid id, string who = TestData.Sam)
        => await (await f.As(who).GetAsync($"/api/v1/planning/cells/{id}/{W:yyyy-MM-dd}")).Json();
    static JsonArray Days(JsonNode availability) => availability["people"]![0]!["days"]!.AsArray();
    static void Strip(JsonNode node)
    {
        if (node is JsonObject obj) { obj.Remove("nonWorking"); foreach (var child in obj.Select(x => x.Value).OfType<JsonNode>()) Strip(child); }
        else if (node is JsonArray arr) foreach (var child in arr.OfType<JsonNode>()) Strip(child);
    }

    [Fact]
    public async Task Both_daily_reads_add_only_flagged_calendar_truth_and_reads_do_not_change_source_records()
    {
        var p = await Person();
        var availabilityOff = await Availability(p.Id); var cellOff = await Cell(p.Id);
        Assert.All(Days(availabilityOff), x => Assert.False(x!.AsObject().ContainsKey("nonWorking")));
        Assert.All(cellOff["capacity"]!["days"]!.AsArray(), x => Assert.False(x!.AsObject().ContainsKey("nonWorking")));
        await Flag("visual_system", true);
        var before = f.Db(db => (db.PersonSchedules.Count(), db.ApprovedLeaves.Count(), db.AvailabilityOverrides.Count(), db.ActivityLog.Count()));
        var availabilityOn = await Availability(p.Id); var cellOn = await Cell(p.Id);
        Assert.Equal(new[] { false, false, false, false, false, true, true }, Days(availabilityOn).Select(x => x!["nonWorking"]!.GetValue<bool>()));
        Assert.Equal(Days(availabilityOn).Select(x => x!["nonWorking"]!.GetValue<bool>()), cellOn["capacity"]!["days"]!.AsArray().Select(x => x!["nonWorking"]!.GetValue<bool>()));
        Assert.Equal(before, f.Db(db => (db.PersonSchedules.Count(), db.ApprovedLeaves.Count(), db.AvailabilityOverrides.Count(), db.ActivityLog.Count())));
        Strip(availabilityOn); Strip(cellOn);
        Assert.True(JsonNode.DeepEquals(availabilityOff, availabilityOn)); Assert.True(JsonNode.DeepEquals(cellOff, cellOn));
        await Flag("visual_system", false);
        Assert.True(JsonNode.DeepEquals(availabilityOff, await Availability(p.Id))); Assert.True(JsonNode.DeepEquals(cellOff, await Cell(p.Id)));
    }

    [Fact]
    public async Task Zero_capacity_and_reported_leave_are_not_nonworking_calendar_evidence()
    {
        var zero = await Person(0); var away = await Person();
        await (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new
        {
            personId = away.Id, sourceSystem = "Synthetic calendar HR", externalId = "CAL-" + Guid.NewGuid().ToString("N"),
            fromDate = W, throughDate = W, status = "Approved", rowVersion = 0,
        })).Json(200);
        await Flag("visual_system", true);
        var z = Days(await Availability(zero.Id))[0]!;
        Assert.Equal(0m, z["normal"]!.GetValue<decimal>()); Assert.Equal(0m, z["available"]!.GetValue<decimal>()); Assert.False(z["nonWorking"]!.GetValue<bool>());
        var a = Days(await Availability(away.Id))[0]!;
        Assert.Equal(8m, a["leave"]!.GetValue<decimal>()); Assert.Equal(0m, a["available"]!.GetValue<decimal>()); Assert.False(a["nonWorking"]!.GetValue<bool>());
        Assert.False((await Cell(away.Id))["capacity"]!["days"]![0]!["nonWorking"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Effective_weekend_schedule_and_applicable_holidays_drive_dates_without_exposing_private_schedules()
    {
        var p = await Person(); var other = await Person();
        Guid office = default;
        await f.DbAsync(async db => { var o = new Office { Code = Guid.NewGuid().ToString("N"), Name = "Synthetic calendar office" }; db.Offices.Add(o); office = o.Id; (await db.Users.SingleAsync(u => u.Id == p.Id)).OfficeId = office; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W.AddDays(-7), weeklyHours = 16, workingDays = new[] { 0, 6 }, rowVersion = 0 })).Json();
        await (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W.AddDays(3), weeklyHours = 40, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).Json();
        await (await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { date = W.AddDays(3), name = "Synthetic local calendar holiday", officeId = office })).Json(201);
        await Flag("visual_system", true);
        var own = await Availability(p.Id, p.Email); var peer = await Availability(other.Id, p.Email);
        Assert.Null(own["people"]![0]!["schedules"]); Assert.False(own["people"]![0]!["canManage"]!.GetValue<bool>());
        Assert.Equal(new[] { true, true, true, true, false, true, true }, Days(own).Select(x => x!["nonWorking"]!.GetValue<bool>()));
        Assert.False(Days(peer)[3]!["nonWorking"]!.GetValue<bool>()); // Another office's holiday is not applicable.
        Assert.Equal(Days(own).Select(x => x!["nonWorking"]!.GetValue<bool>()), (await Cell(p.Id))["capacity"]!["days"]!.AsArray().Select(x => x!["nonWorking"]!.GetValue<bool>()));
        var outsider = $"synthetic-outside-calendar-{Guid.NewGuid():N}@hub.test";
        await (await f.As(outsider).GetAsync("/api/v1/me")).Json();
        Assert.Empty((await Availability(p.Id, outsider))["people"]!.AsArray());
        var denied = await f.As(outsider).GetAsync($"/api/v1/planning/cells/{p.Id}/{W:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode); // Existing planner capability gate refuses before person lookup.
    }

    [Fact]
    public async Task Imported_weekday_vector_follows_its_owner_flag_and_not_legacy_weekend_assumptions()
    {
        var p = await Person();
        // Explicit legacy/import recovery fixture: the old WorkingDays and newer weekday vector disagree.
        await f.DbAsync(async db => { db.PersonSchedules.Add(new PersonSchedule { PersonId = p.Id, FromDate = W, WeeklyHours = 40, WorkingDays = [1, 2, 3, 4, 5], WeekdayHours = [0, 8, 8, 0, 8, 8, 8] }); return await db.SaveChangesAsync(); });
        await Flag("visual_system", true);
        var legacy = Days(await Availability(p.Id)); Assert.False(legacy[2]!["nonWorking"]!.GetValue<bool>()); Assert.True(legacy[5]!["nonWorking"]!.GetValue<bool>());
        await Flag("capacity_polish", true);
        var vector = Days(await Availability(p.Id)); Assert.True(vector[2]!["nonWorking"]!.GetValue<bool>()); Assert.False(vector[5]!["nonWorking"]!.GetValue<bool>());
        Assert.Equal(vector.Select(x => x!["nonWorking"]!.GetValue<bool>()), (await Cell(p.Id))["capacity"]!["days"]!.AsArray().Select(x => x!["nonWorking"]!.GetValue<bool>()));
    }
}
