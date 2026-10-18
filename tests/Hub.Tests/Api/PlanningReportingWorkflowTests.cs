using System.Net;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PlanningReportingWorkflowTests(HubFactory f)
{
    readonly TestData d = new(f);
    async Task Flag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();

    [Fact] // AC-PRO-14, AC-PRO-15, AC-PRO-23
    public async Task Project_note_is_versioned_audited_permission_checked_and_never_changes_health()
    {
        var project = await d.Project(); var pm = f.As(TestData.Pm); var path = $"/api/v1/projects/{project.Id}";
        var before = await (await pm.GetAsync(path)).Json(); Assert.Null(before["statusNote"]);
        await Flag("project_status_note", true);
        try {
            (await pm.Patch(path, new { statusNote = new string('n', 480) }, before.I("rowVersion"))).EnsureSuccessStatusCode();
            var current = await (await pm.GetAsync(path)).Json(); var viewer = await (await f.As(TestData.Rita).GetAsync(path)).Json();
            Assert.Equal(new string('n', 480), viewer["statusNote"]!["text"]!.GetValue<string>());
            Assert.Equal(before["health"]!["computed"]!.ToJsonString(), current["health"]!["computed"]!.ToJsonString());
            Assert.NotNull(viewer["statusNote"]!["header"]!["openAttention"]);
            Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch(path, new { statusNote = new string('n', 501) }, current.I("rowVersion"))).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch(path, new { statusNote = "not allowed" }, current.I("rowVersion"))).StatusCode);
            var activity = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == project.Id).ToListAsync());
            Assert.Contains(activity, a => a.Changes.Contains("StatusNote"));
            var portfolio = await (await f.As(TestData.Admin).GetAsync($"/api/v1/portfolio?projectId={project.Id}&mine=false")).Json();
            Assert.Contains(portfolio["projects"]!.AsArray(), p => p!["id"]!.GetValue<Guid>() == project.Id && p["statusNote"]?["text"]?.GetValue<string>() == new string('n', 480));
            await f.DbAsync(async db => { var row = await db.Projects.SingleAsync(p => p.Id == project.Id); row.Status = ProjectStatus.Complete; row.CompletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
            current = await (await pm.GetAsync(path)).Json();
            Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch(path, new { statusNote = "correction" }, current.I("rowVersion"))).StatusCode);
            (await pm.Patch(path, new { statusNote = "correction", reason = "Verified correction" }, current.I("rowVersion"))).EnsureSuccessStatusCode();
        } finally { await Flag("project_status_note", false); }
    }

    [Fact] // AC-PRO-21, AC-PRO-23
    public async Task Weekday_schedule_has_twenty_hours_and_rejects_leave_above_wednesday_hours()
    {
        var email = $"synthetic-weekday-{Guid.NewGuid():N}@hub.test";
        var person = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id"); var sam = d.User(TestData.Sam); var week = new DateOnly(2026, 9, 14);
        await f.DbAsync(async db => { var user = await db.Users.SingleAsync(u => u.Id == person); user.SupervisorId = sam; user.WeeklyCapacityHours = 40; return await db.SaveChangesAsync(); });
        await Flag("capacity_polish", true);
        try {
            (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{person}", new { fromDate = week, weeklyHours = 20, workingDays = new[] { 1, 2, 3 }, weekdayHours = new[] { 0m, 8m, 8m, 4m, 0m, 0m, 0m }, rowVersion = 0 })).EnsureSuccessStatusCode();
            var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?personIds={person}&from={week:yyyy-MM-dd}&weeks=1")).Json();
            Assert.Equal(20, grid["people"]![0]!["cells"]![0]!["capacity"]!.GetValue<decimal>());
            var view = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/availability?personId={person}&from={week:yyyy-MM-dd}&through={week.AddDays(6):yyyy-MM-dd}")).Json();
            var days = view["people"]![0]!["days"]!.AsArray(); Assert.Equal(4, days.Single(d => d!.S("date") == "2026-09-16")!["normal"]!.GetValue<decimal>());
            Assert.Equal(0, days.Single(d => d!.S("date") == "2026-09-17")!["normal"]!.GetValue<decimal>());
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = person, sourceSystem = "Synthetic HR", externalId = Guid.NewGuid().ToString(), fromDate = week.AddDays(2), throughDate = week.AddDays(2), hoursPerDay = 6, status = "Approved", rowVersion = 0 })).StatusCode);
        } finally { await Flag("capacity_polish", false); }
    }

    [Fact] // AC-PRO-21, AC-PRO-23: preserve the existing schedule CHECK and legacy zero-hour working days.
    public async Task Zero_weekday_vector_keeps_legacy_days_valid_and_legacy_zero_schedule_still_uses_its_calendar()
    {
        var person = (await (await f.As($"synthetic-zero-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
        var sam = d.User(TestData.Sam); var week = new DateOnly(2026, 9, 14); var client = f.As(TestData.Sam);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == person)).SupervisorId = sam; return await db.SaveChangesAsync(); });
        var legacy = await (await client.Put($"/api/v1/planning/schedules/{person}", new { fromDate = week, weeklyHours = 0, workingDays = new[] { 1, 3 }, rowVersion = 0 })).Json();
        async Task<string> Cells() => (await (await client.GetAsync($"/api/v1/planning/grid?personIds={person}&from={week:yyyy-MM-dd}&weeks=1")).Json())["people"]![0]!["cells"]!.ToJsonString();
        var before = await Cells(); await Flag("capacity_polish", true);
        try
        {
            Assert.Equal(before, await Cells());
            (await client.Put($"/api/v1/planning/schedules/{person}", new { fromDate = week, weeklyHours = 0, workingDays = Array.Empty<int>(), weekdayHours = new decimal[7], rowVersion = legacy.I("rowVersion") })).EnsureSuccessStatusCode();
            var row = await f.DbAsync(db => db.PersonSchedules.AsNoTracking().SingleAsync(s => s.PersonId == person && s.FromDate == week));
            Assert.Equal(new[] { 1, 3 }, row.WorkingDays); Assert.Equal(new decimal[7], row.WeekdayHours);
            var available = await (await client.GetAsync($"/api/v1/planning/availability?personId={person}&from={week:yyyy-MM-dd}&through={week.AddDays(6):yyyy-MM-dd}")).Json();
            Assert.All(available["people"]![0]!["days"]!.AsArray(), day => Assert.Equal(0m, day!["normal"]!.GetValue<decimal>()));
            Assert.Equal(before, await Cells());
        }
        finally { await Flag("capacity_polish", false); }
    }

    [Fact] // AC-PRO-16, AC-PRO-17, AC-PRO-23
    public async Task Follow_up_views_track_own_open_watches_and_requested_records_without_writes()
    {
        var project = await d.Project(); var task = await d.NewTask(project.Id, TestData.Pm); var me = d.User(TestData.Pm); var pm = f.As(TestData.Pm);
        (await pm.Post($"/api/v1/items/Task/{task.G("id")}/watchers", new { userId = me })).EnsureSuccessStatusCode();
        await Flag("my_work_follow_up", true);
        try {
            var before = await f.DbAsync(db => db.ActivityLog.CountAsync());
            var watching = await (await pm.GetAsync("/api/v1/me/work/watching")).Json();
            Assert.Contains(watching["items"]!.AsArray(), r => r!.G("id") == task.G("id"));
            Assert.Equal(watching.I("total"), watching["items"]!.AsArray().Count);
            var requested = await (await pm.GetAsync("/api/v1/me/work/requested")).Json(); Assert.Equal(requested.I("total"), requested["items"]!.AsArray().Count);
            Assert.Equal(before, await f.DbAsync(db => db.ActivityLog.CountAsync()));
            (await pm.DeleteAsync($"/api/v1/items/Task/{task.G("id")}/watchers/{me}")).EnsureSuccessStatusCode();
            watching = await (await pm.GetAsync("/api/v1/me/work/watching")).Json(); Assert.DoesNotContain(watching["items"]!.AsArray(), r => r!.G("id") == task.G("id"));
        } finally { await Flag("my_work_follow_up", false); }
    }
}
