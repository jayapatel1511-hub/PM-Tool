using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 023: the team calendar (FR-VIS-06, AC-VIS-04, §36.5): deadline projections, the three event types with times
/// in the organisation's zone, type toggles, editing and cancelling, and no disclosure of restricted projects.
[Collection("api")]
public sealed class CalendarTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Range(string as_, string query) => f.As(as_).GetAsync($"/api/v1/calendar?from=2026-09-21&to=2026-09-27&{query}").Result.Json();
    static JsonNode Event(Guid? projectId, string type, string title, string start, string end, string? visibility = null) =>
        JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { type, title, projectId, start, end, visibility, location = "Site office" }))!;

    [Fact]
    public async Task Week_shows_deadlines_and_events_under_their_toggles() // FR-001..FR-003, AC-VIS-04, US1
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = "Survey review", dueDate = "2026-09-22" });
        await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Drainage report", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-23" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-09-24" }).Result.Json(201);
        var meeting = await pm.Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Coordination", "2026-09-21T09:00", "2026-09-21T10:00")).Result.Json(201);
        await pm.Post("/api/v1/calendar/events", Event(p.Id, "Site Work", "Test pits", "2026-09-22T07:30", "2026-09-22T15:00")).Result.Json(201);
        var mine = await f.As(TestData.Alex).Post("/api/v1/calendar/events", Event(null, "Internal Task", "Prepare QA notes", "2026-09-23T13:00", "2026-09-23T14:30")).Result.Json(201);
        Assert.Equal(("Private", "America/Halifax"), (mine.S("visibility"), mine.S("timeZone"))); // without a project it is private
        Assert.Equal("2026-09-21T12:00:00+00:00", meeting.S("startAt")); // 09:00 in Halifax (ADT) is 12:00 UTC
        Assert.Equal("2026-09-21T09:00", meeting.S("start"));

        var week = (await Range(TestData.Pm, $"projectIds={p.Id}"))["entries"]!.AsArray();
        var deadlines = week.Where(e => e!.S("kind") == "deadline").ToList();
        Assert.Equal(new[] { "Deliverable", "Milestone", "Task" }, deadlines.Select(e => e!.S("sourceType")).Order());
        Assert.Equal(("2026-09-22", task.S("id")), (deadlines.Single(e => e!.S("sourceType") == "Task")!.S("date"), deadlines.Single(e => e!.S("sourceType") == "Task")!.S("id")));
        Assert.Null(deadlines[0]!["start"]); // date-only: never an invented time
        Assert.Equal(new[] { "Meeting", "Site Work" }, week.Where(e => e!.S("kind") == "event").Select(e => e!.S("type")));
        Assert.DoesNotContain(week, e => e!["title"]!.GetValue<string>() == "Prepare QA notes"); // Alex's private event

        var alexWeek = (await Range(TestData.Alex, $"projectIds={p.Id}"))["entries"]!.AsArray();
        Assert.Contains(alexWeek, e => e!.S("title") == "Prepare QA notes"); // the owner sees it with the project scope chosen
        Assert.Equal(new[] { "Coordination" }, (await Range(TestData.Pm, $"projectIds={p.Id}&types=Meeting"))["entries"]!.AsArray().Select(e => e!.S("title")));
        Assert.Equal(3, (await Range(TestData.Pm, $"projectIds={p.Id}&types=Deadline"))["entries"]!.AsArray().Count);
        Assert.Equal(new[] { "Test pits" }, (await Range(TestData.Pm, $"projectIds={p.Id}&types=Site Work"))["entries"]!.AsArray().Select(e => e!.S("title")));
    }

    [Fact]
    public async Task Validation_editing_rights_and_cancellation() // FR-003, US2
    {
        var p = await d.Project();
        var alex = f.As(TestData.Alex);
        var bad = await alex.Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Backwards", "2026-09-21T10:00", "2026-09-21T09:00")).Result.Json(400);
        Assert.NotNull(bad["errors"]!["end"]);
        Assert.NotNull((await alex.Post("/api/v1/calendar/events", Event(null, "Meeting", "No project", "2026-09-21T09:00", "2026-09-21T10:00")).Result.Json(400))["errors"]!["projectId"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Not on team", "2026-09-21T09:00", "2026-09-21T10:00"))).StatusCode);

        var e = await alex.Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Design review", "2026-09-24T14:00", "2026-09-24T15:00")).Result.Json(201);
        var moved = await alex.Patch($"/api/v1/calendar/events/{e.S("id")}", new { start = "2026-09-24T15:00", end = "2026-09-24T16:30" }, e.I("rowVersion")).Result.Json();
        Assert.Equal("2026-09-24T15:00", moved.S("start"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Patch($"/api/v1/calendar/events/{e.S("id")}", new { title = "Taken over" }, moved.I("rowVersion"))).StatusCode); // a lead, not the owner or PM
        var byPm = await f.As(TestData.Pm).Patch($"/api/v1/calendar/events/{e.S("id")}", new { location = "Boardroom" }, moved.I("rowVersion")).Result.Json();
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == e.G("id") && a.ActorUserId == U(TestData.Alex) && a.Categories.Contains("date"))));

        await f.As(TestData.Pm).Post($"/api/v1/calendar/events/{e.S("id")}/cancel", new { rowVersion = byPm.I("rowVersion") }).Result.Json();
        Assert.DoesNotContain((await Range(TestData.Pm, $"projectIds={p.Id}"))["entries"]!.AsArray(), x => x!.S("kind") == "event" && x.S("id") == e.S("id"));
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == e.G("id") && a.ActorUserId == U(TestData.Pm) && a.Categories.Contains("status"))));
        Assert.False((await f.As(TestData.Pm).GetAsync($"/api/v1/calendar/events/{e.S("id")}").Result.Json())["canEdit"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Restricted_projects_reveal_nothing() // FR-005, AC-VIS-04, SC-003
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var p = await d.Project();
            await d.NewTask(p.Id, TestData.Pm, new { name = "Confidential survey", dueDate = "2026-09-22" });
            var e = await f.As(TestData.Pm).Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Confidential meeting", "2026-09-22T09:00", "2026-09-22T10:00")).Result.Json(201);
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            var outsider = await Range(TestData.Jill, "");
            Assert.DoesNotContain(outsider["entries"]!.AsArray(), x => x!["projectId"]?.GetValue<string>() == p.Id.ToString());
            Assert.DoesNotContain(outsider.ToJsonString(), "Confidential");
            Assert.Empty((await Range(TestData.Jill, $"projectIds={p.Id}"))["entries"]!.AsArray()); // naming the project directly reveals nothing either
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/calendar/events/{e.S("id")}")).StatusCode);
            Assert.Contains((await Range(TestData.Alex, $"projectIds={p.Id}"))["entries"]!.AsArray(), x => x!.S("title") == "Confidential meeting"); // a member
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
        var privateOne = await f.As(TestData.Alex).Post("/api/v1/calendar/events", Event(null, "Internal Task", "My own", "2026-09-25T09:00", "2026-09-25T09:30")).Result.Json(201);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/calendar/events/{privateOne.S("id")}")).StatusCode);
    }
}
