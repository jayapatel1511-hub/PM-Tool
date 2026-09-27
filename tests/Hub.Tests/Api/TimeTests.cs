using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 024: task time entries (FR-VIS-10, AC-VIS-08): arithmetic, the 24-hour day even under concurrent saves, editable
/// projects only, owner edits and PM corrections with reasons, soft deletion, permitted review and exports.
[Collection("api")]
public sealed class TimeTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    /// A fresh Team Member on the project, so other tests' hours never share a day with these.
    async Task<(Guid Id, string Email)> Member(Project p, Guid? supervisor = null)
    {
        var email = $"t{Guid.NewGuid().ToString("N")[..8]}@hub.test";
        await f.As(email).GetAsync("/api/v1/me").Result.Json();
        var row = (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/users?q={email}").Result.Json())[0]!;
        if (supervisor is { } s) (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{row.S("id")}", new { supervisorId = s }, row.I("rowVersion"))).EnsureSuccessStatusCode();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = row.G("id"), roles = new[] { "TeamMember" }, disciplineId = await d.Discipline("Civil") }).Result.Json(201);
        return (row.G("id"), email);
    }

    Task<HttpResponseMessage> Add(string as_, JsonNode task, string date, decimal hours, string? note = null) =>
        f.As(as_).Post("/api/v1/time", new { taskId = task.G("id"), workDate = date, hours, note });

    [Fact]
    public async Task Entries_add_up_and_the_day_stays_within_24_hours() // AC-VIS-08 US1-1, US1-2, FR-002, edge cases
    {
        var p = await d.Project();
        var me = await Member(p);
        var task = await d.NewTask(p.Id, TestData.Pm, new { estimatedHours = 16 });
        (await Add(me.Email, task, "2026-09-15", 2.5m, "Site notes")).EnsureSuccessStatusCode();
        (await Add(me.Email, task, "2026-09-15", 1.25m)).EnsureSuccessStatusCode();
        var week = await f.As(me.Email).GetAsync("/api/v1/time?from=2026-09-14&to=2026-09-20").Result.Json();
        Assert.Equal(2, week["entries"]!.AsArray().Count);
        Assert.Equal(3.75m, week["total"]!.GetValue<decimal>());
        Assert.Equal(3.75m, week["byDay"]![0]!["hours"]!.GetValue<decimal>());
        Assert.Equal(3.75m, week["byTask"]![0]!["hours"]!.GetValue<decimal>());
        Assert.Equal(3.75m, week["byProject"]![0]!["hours"]!.GetValue<decimal>());

        (await Add(me.Email, task, "2026-09-16", 23m)).EnsureSuccessStatusCode();
        var over = await Add(me.Email, task, "2026-09-16", 2m);
        Assert.Equal(HttpStatusCode.BadRequest, over.StatusCode);
        Assert.Contains("1", (await over.Json(400))["errors"]!["hours"]![0]!.GetValue<string>()); // "at most 1 more hours fit"
        Assert.Equal(23m, await f.DbAsync(db => db.TimeEntries.Where(e => e.UserId == me.Id && e.WorkDate == new DateOnly(2026, 9, 16)).SumAsync(e => e.Hours))); // nothing partial
        foreach (var bad in new[] { 0m, -1m, 24.5m, 1.234m })
            Assert.Equal(HttpStatusCode.BadRequest, (await Add(me.Email, task, "2026-09-17", bad)).StatusCode);

        // FR-005: actual hours never touch the estimate or progress.
        var t = await f.DbAsync(db => db.Tasks.AsNoTracking().FirstAsync(x => x.Id == task.G("id")));
        Assert.Equal((16m, 0), (t.EstimatedHours!.Value, t.ProgressPct));
    }

    [Fact]
    public async Task Concurrent_saves_cannot_both_break_the_day() // SC-003
    {
        var p = await d.Project();
        var me = await Member(p);
        var task = await d.NewTask(p.Id, TestData.Pm);
        var saves = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Add(me.Email, task, "2026-09-18", 13m)));
        Assert.Equal(1, saves.Count(r => r.IsSuccessStatusCode));
        Assert.Equal(13m, await f.DbAsync(db => db.TimeEntries.Where(e => e.UserId == me.Id).SumAsync(e => e.Hours)));
    }

    [Fact]
    public async Task Complete_tasks_accept_late_hours_but_closed_projects_do_not() // US1-3
    {
        var p = await d.Project();
        var me = await Member(p);
        var task = await d.NewTask(p.Id, TestData.Pm);
        await d.Move(TestData.Pm, task, TaskStatuses.InProgress);
        await d.Move(TestData.Pm, task, TaskStatuses.Complete);
        (await Add(me.Email, task, "2026-09-11", 1m)).EnsureSuccessStatusCode();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = U(TestData.Diane), roles = new[] { "Viewer" } }).Result.Json(201);
        Assert.Equal(HttpStatusCode.Forbidden, (await Add(TestData.Diane, task, "2026-09-11", 1m)).StatusCode); // viewers record no effort
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Cancelled", reason = "Client cancelled the work", rowVersion = d.Version(p.Id) }).Result.Json();
        Assert.Equal(HttpStatusCode.Forbidden, (await Add(me.Email, task, "2026-09-11", 1m)).StatusCode);
    }

    [Fact]
    public async Task Owners_edit_pms_correct_with_a_reason_and_deletion_is_soft() // AC-VIS-08 US2
    {
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Pm);
        var mine = await Add(TestData.Alex, task, "2026-08-03", 4m).Result.Json(201);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).Patch($"/api/v1/time/{mine.S("id")}", new { hours = 3 }, mine.I("rowVersion"))).StatusCode);
        var owner = await f.As(TestData.Alex).Patch($"/api/v1/time/{mine.S("id")}", new { hours = 3.5 }, mine.I("rowVersion")).Result.Json();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Patch($"/api/v1/time/{mine.S("id")}", new { hours = 3 }, owner.I("rowVersion"))).StatusCode); // reason required
        var pm = await f.As(TestData.Pm).Patch($"/api/v1/time/{mine.S("id")}", new { hours = 3, reason = "Half hour was travel" }, owner.I("rowVersion")).Result.Json();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Alex).Patch($"/api/v1/time/{mine.S("id")}", new { taskId = (await d.NewTask(p.Id, TestData.Pm)).G("id") }, pm.I("rowVersion"))).StatusCode);
        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == mine.G("id")).OrderBy(a => a.OccurredAt).ToListAsync());
        Assert.Contains(log, a => a.ActorUserId == U(TestData.Pm) && a.Reason == "Half hour was travel" && a.Changes.Contains("3.5"));
        Assert.Equal(task.S("key"), log.Last().ItemKey);

        var req = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/time/{mine.S("id")}") { Content = System.Net.Http.Json.JsonContent.Create(new { }) };
        req.Headers.TryAddWithoutValidation("If-Match", $"\"{pm.I("rowVersion")}\"");
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Alex).SendAsync(req)).StatusCode);
        Assert.True(await f.DbAsync(db => db.TimeEntries.IgnoreQueryFilters().AnyAsync(e => e.Id == mine.G("id") && e.DeletedAt != null))); // kept, not erased
        var left = await f.As(TestData.Alex).GetAsync("/api/v1/time?from=2026-08-03&to=2026-08-09").Result.Json();
        Assert.True(left["total"]!.GetValue<decimal>() == 0, left.ToJsonString());
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == mine.G("id") && a.Action == "Deleted")));
        Assert.True(await f.DbAsync(db => db.TimeEntries.IgnoreQueryFilters().AnyAsync(e => e.Id == mine.G("id") && e.DeletedAt != null))); // kept, not erased
    }

    [Fact]
    public async Task Review_scopes_totals_and_exports_reconcile() // AC-VIS-08 US3, FR-004, SC-002
    {
        var open = await d.Project();
        var report = await Member(open, U(TestData.Sam));
        var other = await Member(open);
        var t1 = await d.NewTask(open.Id, TestData.Pm, new { name = "Survey" });
        (await Add(report.Email, t1, "2026-08-10", 3m)).EnsureSuccessStatusCode();
        (await Add(other.Email, t1, "2026-08-10", 2m)).EnsureSuccessStatusCode();

        var pmView = await f.As(TestData.Pm).GetAsync($"/api/v1/time?from=2026-08-10&to=2026-08-16&scope=all&projectId={open.Id}").Result.Json();
        Assert.Equal(5m, pmView["total"]!.GetValue<decimal>());
        Assert.True(pmView["canReview"]!.GetValue<bool>());
        var samView = await f.As(TestData.Sam).GetAsync($"/api/v1/time?from=2026-08-10&to=2026-08-16&scope=all&projectId={open.Id}").Result.Json();
        Assert.Equal(new[] { 3m }, samView["entries"]!.AsArray().Select(e => e!["hours"]!.GetValue<decimal>())); // only his direct report
        Assert.Empty((await f.As(TestData.Jill).GetAsync($"/api/v1/time?from=2026-08-10&to=2026-08-16&scope=all&projectId={open.Id}").Result.Json())["entries"]!.AsArray());

        var csv = await f.As(TestData.Pm).GetAsync($"/api/v1/time/export?format=csv&from=2026-08-10&to=2026-08-16&scope=all&projectId={open.Id}");
        var text = System.Text.Encoding.UTF8.GetString((await csv.Content.ReadAsByteArrayAsync())[3..]);
        var total = text.Split('\n').Select(l => l.Trim()).Single(l => l.StartsWith(",Total,")).Split(',');
        Assert.Equal(5m, decimal.Parse(total[5], System.Globalization.CultureInfo.InvariantCulture)); // the export total equals the visible sum
        var report2 = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/task-hours?from=2026-08-10&to=2026-08-16&scope=team&projectId={open.Id}").Result.Json();
        Assert.Equal(5m, report2["rows"]!.AsArray().Last()!["hours"]!.GetValue<decimal>());

        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var secret = await d.Project();
            var member = await f.As(TestData.Pm).Post($"/api/v1/projects/{secret.Id}/members", new { userId = report.Id, roles = new[] { "TeamMember" }, disciplineId = await d.Discipline("Civil") });
            Assert.True(member.IsSuccessStatusCode);
            var hidden = await d.NewTask(secret.Id, TestData.Pm, new { name = "Confidential" });
            (await Add(report.Email, hidden, "2026-08-11", 4m, "Secret note")).EnsureSuccessStatusCode();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{secret.Id}", new { visibility = Visibility.Restricted }, d.Version(secret.Id))).EnsureSuccessStatusCode();
            var sam = await f.As(TestData.Sam).GetAsync("/api/v1/time?from=2026-08-10&to=2026-08-16&scope=all").Result.Json();
            Assert.DoesNotContain(sam.ToJsonString(), "Confidential");
            Assert.DoesNotContain(sam.ToJsonString(), "Secret note");
            Assert.Equal(3m, sam["entries"]!.AsArray().Where(e => e!.G("userId") == report.Id).Sum(e => e!["hours"]!.GetValue<decimal>()));
            var samCsv = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Sam).GetAsync("/api/v1/time/export?format=csv&from=2026-08-10&to=2026-08-16&scope=all")).Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain("Confidential", samCsv);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }
}
