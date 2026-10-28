using System.Net;
using System.Text.Json;
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

    static string[] Keys(JsonNode? n) => n!.AsObject().Select(x => x.Key).ToArray();

    [Fact]
    public async Task Entries_and_event_answers_keep_their_property_names_order_kinds_and_refusals() // the typed records must serialise to the bytes Calendar.tsx and the meeting form already read
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        await d.NewTask(p.Id, TestData.Pm, new { name = "Survey review", dueDate = "2026-09-22" });
        await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Drainage report", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-23" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-09-24" }).Result.Json(201);
        await f.Evaluate(p.Id); // the state rows the deadlines' status and overdue read
        string[] deadlineKeys = ["kind", "sourceType", "id", "key", "title", "date", "projectId", "projectNumber", "projectName", "status", "overdue"];
        string[] eventKeys = ["kind", "id", "type", "title", "start", "end", "startAt", "endAt", "timeZone", "projectId", "projectNumber", "projectName", "ownerId", "owner", "location", "description", "visibility", "cancelled", "rowVersion", "canEdit"];

        var post = await pm.Post("/api/v1/calendar/events", Event(p.Id, "Meeting", "Coordination", "2026-09-21T09:00", "2026-09-21T10:00"));
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var meeting = await post.Json(201);
        Assert.Equal($"/api/v1/calendar/events/{meeting.S("id")}", post.Headers.Location!.ToString());
        Assert.Equal(eventKeys, Keys(meeting));
        var privateOne = await f.As(TestData.Alex).Post("/api/v1/calendar/events", Event(null, "Internal Task", "Prepare QA notes", "2026-09-23T13:00", "2026-09-23T14:30")).Result.Json(201);

        void CheckEvent(JsonNode e, bool project)
        {
            Assert.Equal(eventKeys, Keys(e));
            Assert.Equal("event", e.S("kind"));
            Assert.All(new[] { "id", "type", "title", "start", "end", "startAt", "endAt", "timeZone", "ownerId", "owner", "location", "visibility" }, k => Assert.Equal(JsonValueKind.String, e[k]!.GetValueKind()));
            Assert.Equal(JsonValueKind.Number, e["rowVersion"]!.GetValueKind());
            Assert.Equal(JsonValueKind.False, e["cancelled"]!.GetValueKind());
            Assert.Equal(JsonValueKind.True, e["canEdit"]!.GetValueKind());
            Assert.Null(e["description"]); // present (the key order above) and null
            if (project) Assert.All(new[] { "projectId", "projectNumber", "projectName" }, k => Assert.Equal(JsonValueKind.String, e[k]!.GetValueKind()));
            else Assert.Equal((null, null, null), (e["projectId"], e["projectNumber"], e["projectName"]));
        }
        CheckEvent(meeting, project: true);
        CheckEvent(privateOne, project: false);
        Assert.Equal((p.ProjectNumber, p.Name), (meeting.S("projectNumber"), meeting.S("projectName")));

        var range = await Range(TestData.Pm, $"projectIds={p.Id}");
        Assert.Equal(["timeZone", "from", "to", "entries"], Keys(range));
        Assert.Equal(("America/Halifax", "2026-09-21", "2026-09-27"), (range.S("timeZone"), range.S("from"), range.S("to")));
        var entries = range["entries"]!.AsArray().Select(e => e!).ToArray();
        var deadlines = entries.Where(e => e.S("kind") == "deadline").ToArray();
        Assert.Equal(["Task", "Deliverable", "Milestone"], deadlines.Select(e => e.S("sourceType"))); // tasks, then deliverables, then milestones
        Assert.All(deadlines, e =>
        {
            Assert.Equal(deadlineKeys, Keys(e));
            Assert.All(new[] { "sourceType", "id", "key", "title", "date", "projectId", "projectNumber", "projectName" }, k => Assert.Equal(JsonValueKind.String, e[k]!.GetValueKind()));
            Assert.Equal(JsonValueKind.String, e["status"]!.GetValueKind());
            Assert.True(e["overdue"]!.GetValueKind() is JsonValueKind.True or JsonValueKind.False);
        });
        Assert.Equal(["2026-09-22", "2026-09-23", "2026-09-24"], deadlines.Select(e => e.S("date")));
        CheckEvent(entries.Single(e => e.S("kind") == "event"), project: true);
        CheckEvent((await Range(TestData.Alex, "types=Internal Task"))["entries"]!.AsArray().Single(e => e!.S("id") == privateOne.S("id"))!, project: false);
        var none = await Range(TestData.Pm, $"projectIds={p.Id}&types=Site Work"); // an empty week is still the same envelope
        Assert.Equal(["timeZone", "from", "to", "entries"], Keys(none));
        Assert.Empty(none["entries"]!.AsArray());

        var one = await pm.GetAsync($"/api/v1/calendar/events/{meeting.S("id")}").Result.Json();
        CheckEvent(one, project: true);
        Assert.Equal(meeting.ToJsonString(), one.ToJsonString()); // the create answer and the detail read are the same row
        var moved = await pm.Patch($"/api/v1/calendar/events/{meeting.S("id")}", new { title = "Coordination (moved)", description = "Agenda attached" }, meeting.I("rowVersion")).Result.Json();
        Assert.Equal(eventKeys, Keys(moved));
        Assert.Equal(("Coordination (moved)", "Agenda attached", meeting.I("rowVersion") + 1), (moved.S("title"), moved.S("description"), moved.I("rowVersion")));
        var cancelled = await pm.Post($"/api/v1/calendar/events/{meeting.S("id")}/cancel", new { rowVersion = moved.I("rowVersion") }).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(cancelled));
        Assert.Equal((meeting.G("id"), moved.I("rowVersion") + 1), (cancelled.G("id"), cancelled.I("rowVersion")));
        var after = await pm.GetAsync($"/api/v1/calendar/events/{meeting.S("id")}").Result.Json();
        Assert.Equal((true, false, cancelled.I("rowVersion")), (after["cancelled"]!.GetValue<bool>(), after["canEdit"]!.GetValue<bool>(), after.I("rowVersion")));

        var ghost = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/calendar/events/{ghost}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/calendar/events/{ghost}", new { title = "Nobody" }, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/calendar/events/{ghost}/cancel", new { rowVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Alex).Patch($"/api/v1/calendar/events/{privateOne.S("id")}", new { title = "Stale" }, privateOne.I("rowVersion") + 5)).StatusCode); // the owner on an old row version
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/calendar/events/{privateOne.S("id")}", new { title = "Not yours" }, privateOne.I("rowVersion"))).StatusCode); // another user's private event
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync("/api/v1/calendar?from=2026-09-21&to=2026-12-31")).StatusCode); // more than 92 days
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.GetAsync("/api/v1/calendar?from=2026-09-27&to=2026-09-21")).StatusCode);
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
