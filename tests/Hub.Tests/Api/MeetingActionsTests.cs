using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 015: meetings and their actions (US1), capture in meeting mode (US2), routing to people, leads and nobody outside
/// (US3), and actions that become tasks and follow them (US4).
[Collection("api")]
public sealed class MeetingActionsTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Meeting(Guid projectId, string title = "Client Progress Meeting #4", string type = "Client", string as_ = TestData.Pm, int expect = 201) =>
        f.As(as_).Post($"/api/v1/projects/{projectId}/meetings", new { title, meetingDate = "2026-09-14", meetingType = type, notesLink = "https://contoso.sharepoint.com/minutes/4" })
            .Result.Json(expect);

    Task<JsonNode> Action(JsonNode meeting, string text, string ownerType, Guid owner, string? due = null, Guid? task = null, string as_ = TestData.Pm, int expect = 201) =>
        f.As(as_).Post($"/api/v1/meetings/{meeting.S("id")}/actions", new
        {
            text, ownerType, dueDate = due, relatedTaskId = task,
            ownerUserId = ownerType == "User" ? owner : (Guid?)null, ownerDisciplineId = ownerType == "Discipline" ? owner : (Guid?)null,
            ownerExternalPartyId = ownerType == "External Party" ? owner : (Guid?)null,
        }).Result.Json(expect);

    Task<int> Version(JsonNode a) => f.DbAsync(db => db.Actions.Where(x => x.Id == a.G("id")).Select(x => x.RowVersion).FirstAsync());
    Task<string> Status(JsonNode a) => f.DbAsync(db => db.Actions.Where(x => x.Id == a.G("id")).Select(x => x.Status).FirstAsync());

    async Task<HttpResponseMessage> Move(JsonNode a, string to, string? reason = null, string as_ = TestData.Pm) =>
        await f.As(as_).Post($"/api/v1/actions/{a.S("id")}/transition", new { toStatus = to, reason, rowVersion = await Version(a) });

    async Task<Guid> Client(Guid projectId) =>
        (await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/external-parties", new { name = "City of Hamilton", isClient = true }).Result.Json(201)).G("id");

    [Fact]
    public async Task A_meeting_records_actions_for_a_person_a_discipline_and_the_client() // US1, FR-001, FR-002
    {
        var p = await d.Project();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/meetings", new { title = "Mine", meetingType = "Client" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/meetings", new { title = "Bad link", notesLink = "javascript:alert(1)" })).StatusCode);
        var m = await Meeting(p.Id);
        var a1 = await Action(m, "Send the revised grading plan", "User", U(TestData.Alex), due: "2026-09-18");
        var a2 = await Action(m, "Confirm catch basin spacing", "Discipline", d.ProjectDiscipline(p.Id, "Civil"), due: "2026-09-16");
        var a3 = await Action(m, "Provide the utility locates", "External Party", await Client(p.Id), due: "2026-09-21");
        Assert.Matches(@"^P\w+-A01$", a1.S("key"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post($"/api/v1/meetings/{m.S("id")}/actions",
            new { text = "Two owners", ownerType = "User", ownerUserId = U(TestData.Alex), ownerDisciplineId = d.ProjectDiscipline(p.Id, "Civil") })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post($"/api/v1/meetings/{m.S("id")}/actions", new { text = "No", ownerType = "User", ownerUserId = U(TestData.Alex) })).StatusCode);

        var meetings = (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/meetings").Result.Json()).AsArray();
        Assert.Equal(("Client Progress Meeting #4", 3, 3), (meetings.Single()!.S("title"), meetings.Single()!.I("total"), meetings.Single()!.I("open")));
        var list = (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/actions?meetingId={m.S("id")}").Result.Json()).AsArray();
        Assert.Equal(new[] { a2.S("key"), a1.S("key"), a3.S("key") }, list.Select(x => x!.S("key"))); // by due date
        Assert.Equal(new[] { "Civil", "Alex Chen", "City of Hamilton" }, list.Select(x => x!.S("ownerName")));

        (await Move(a1, "In Progress", as_: TestData.Alex)).EnsureSuccessStatusCode(); // the owner moves their own
        (await Move(a1, "Complete", as_: TestData.Alex)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await Move(a3, "Cancelled")).StatusCode); // cancelling needs a reason
        (await Move(a3, "Cancelled", "The City sent them already")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await Move(a2, "Complete", as_: TestData.Alex)).StatusCode); // Civil's action: the lead or PM
        (await Move(a2, "Complete", as_: TestData.Marc)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Meeting_mode_captures_actions_against_todays_coordination_meeting() // US2, FR-003, MTG-04
    {
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Storm sewer profile" });
        var current = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/meetings/current", new { }).Result.Json();
        Assert.Equal(("Weekly Coordination — 2026-09-14", "Coordination", "2026-09-14"), (current.S("title"), current.S("meetingType"), current.S("meetingDate")));
        var again = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/meetings/current", new { }).Result.Json(); // a lead joins the same meeting
        Assert.Equal(current.S("id"), again.S("id"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/meetings/current", new { })).StatusCode);

        await Action(current, "Check the crossing with the watermain", "User", U(TestData.Marc), due: "2026-09-17", task: task.G("id"));
        await Action(current, "Ask the City for as-builts", "User", U(TestData.Pm), task: task.G("id"));
        var list = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/actions?meetingId={current.S("id")}").Result.Json()).AsArray();
        Assert.Equal(2, list.Count);
        Assert.All(list, x => Assert.Equal((task.S("key"), "Weekly Coordination — 2026-09-14"), (x!.S("taskKey"), x.S("meetingTitle"))));
    }

    [Fact]
    public async Task Actions_reach_the_owner_the_discipline_lead_or_the_PM_and_nothing_goes_outside() // US3, FR-004, FR-005, MTG-01, MTG-02
    {
        var p = await d.Project();
        var m = await Meeting(p.Id);
        var civil = await Action(m, "Confirm catch basin spacing", "Discipline", d.ProjectDiscipline(p.Id, "Civil"), due: "2026-09-10"); // overdue
        var client = await Action(m, "Provide the utility locates", "External Party", await Client(p.Id), due: "2026-09-21");

        var marc = await f.As(TestData.Marc).GetAsync("/api/v1/me/work").Result.Json();
        Assert.Contains(marc["actions"]!.AsArray(), x => x!.S("key") == civil.S("key"));
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Marc) && n.ItemId == civil.G("id") && n.EventType == NotificationEvents.ActionAssigned)));
        Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.ItemId == client.G("id")))); // MTG-02: nothing is sent for an external party
        var wc = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/coordination").Result.Json();
        Assert.Equal(new[] { client.S("key") }, wc["waiting"]!.AsArray().Select(x => x!.S("key")));

        await f.Evaluate(p.Id); // an overdue discipline action is the lead's attention item
        var att = await f.DbAsync(db => db.Attention.Where(a => a.ProjectId == p.Id && a.ItemId == civil.G("id")).ToListAsync());
        Assert.Contains(U(TestData.Marc), Assert.Single(att).RouteToUserIds);

        // A discipline without a lead is flagged to the PM.
        var geo = await d.Discipline("Geotechnical");
        var q = await d.Project(tweak: b => { b["disciplines"] = new[] { new { disciplineId = geo, leadUserId = (Guid?)null } }; b["members"] = Array.Empty<object>(); });
        var m2 = await Meeting(q.Id);
        var r = await f.As(TestData.Pm).Post($"/api/v1/meetings/{m2.S("id")}/actions", new { text = "Borehole plan", ownerType = "Discipline", ownerDisciplineId = d.ProjectDiscipline(q.Id, "Geotechnical") }).Result.Json(201);
        Assert.Contains("no lead", r["warnings"]!.AsArray().Single()!.GetValue<string>());
        var pm = await f.As(TestData.Pm).GetAsync("/api/v1/me/work").Result.Json();
        var mine = pm["actions"]!.AsArray().Single(x => x!.S("key") == r.S("key"))!;
        Assert.True(mine["noLead"]!.GetValue<bool>());
    }

    [Fact]
    public async Task A_converted_action_becomes_a_task_and_follows_its_completion() // US4, FR-006, MTG-03, SC-003
    {
        var p = await d.Project();
        var m = await Meeting(p.Id);
        var a = await Action(m, "Update the pavement design memo for the new traffic counts", "User", U(TestData.Alex), due: "2026-09-25");
        var conv = await f.As(TestData.Pm).Post($"/api/v1/actions/{a.S("id")}/convert", new { rowVersion = await Version(a) }).Result.Json();
        var task = await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{conv.S("taskId")}").Result.Json();
        Assert.Equal(("Update the pavement design memo for the new traffic counts", "Alex Chen", "Civil", "2026-09-25"),
            (task["task"]!.S("name"), task["task"]!.S("assigneeName"), task["task"]!.S("disciplineName"), task["task"]!.S("dueDate")));
        Assert.Contains(a.S("key"), task["description"]!.GetValue<string>());
        Assert.Equal("In Progress", await Status(a));
        var detail = await f.As(TestData.Pm).GetAsync($"/api/v1/actions/{a.S("id")}").Result.Json();
        Assert.Equal((conv.S("taskKey"), true), (detail["task"]!.S("key"), detail["action"]!["converted"]!.GetValue<bool>()));
        Assert.Empty(detail["permissions"]!["transitions"]!.AsArray());
        Assert.Equal(422, (int)(await Move(a, "Complete")).StatusCode); // the task decides
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Post($"/api/v1/actions/{a.S("id")}/convert", new { rowVersion = await Version(a) })).StatusCode);

        var t = task["task"]!;
        await d.Move(TestData.Alex, t, "In Progress");
        Assert.Equal("In Progress", await Status(a));
        await d.Move(TestData.Alex, t, "Complete");
        Assert.Equal("Complete", await Status(a)); // SC-003: they agree
        await d.Move(TestData.Pm, t, "In Progress", new { reason = "Counts revised again" });
        Assert.Equal("In Progress", await Status(a));
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(x => x.ItemId == a.G("id") && x.Action == "Converted")));

        var rep = (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/meeting-actions-outstanding?projectId={p.Id}").Result.Json())["rows"]!.AsArray();
        Assert.Equal((a.S("key"), "Client Progress Meeting #4"), (rep.Single()!.S("key"), rep.Single()!.S("meetingTitle")));
        Assert.Empty((await f.As(TestData.Pm).GetAsync($"/api/v1/reports/meeting-actions-outstanding?projectId={p.Id}&ownerType=External%20Party").Result.Json())["rows"]!.AsArray());
    }
}
