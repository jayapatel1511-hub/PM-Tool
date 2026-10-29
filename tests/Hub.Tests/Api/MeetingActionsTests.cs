using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
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

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static readonly string[] MeetingKeys = ["id", "projectId", "title", "meetingDate", "meetingType", "notesLink", "calendarEventId", "rowVersion"];
    static readonly string[] ActionKeys = ["id", "projectId", "projectNumber", "key", "meetingId", "meetingTitle", "meetingDate", "text", "ownerType", "ownerTypeLabel", "ownerUserId",
        "ownerDisciplineId", "ownerExternalPartyId", "ownerName", "leadId", "leadName", "noLead", "partyIsClient", "dueDate", "isOverdue", "daysOverdue", "status", "relatedTaskId",
        "taskKey", "relatedDecisionId", "decisionKey", "converted", "rowVersion"];

    [Fact]
    public async Task Meeting_commands_and_list_keep_their_answers_and_shape() // the typed records must serialise to the bytes the app already reads (040-S15-Meetings); PATCH was uncalled
    {
        var p = await d.Project();
        var (pm, alex) = (f.As(TestData.Pm), f.As(TestData.Alex));
        var root = $"/api/v1/projects/{p.Id}";
        int Mv(Guid id) => f.Db(db => db.Meetings.Where(x => x.Id == id).Select(x => x.RowVersion).First());
        Assert.Equal("[]", (await alex.GetAsync($"{root}/meetings").Result.Content.ReadAsStringAsync()));

        // Creating: 201 with a Location and the stored meeting, every property written, nulls included.
        var ev = await pm.Post("/api/v1/calendar/events", new { type = "Meeting", title = "Coordination call", projectId = p.Id, start = "2026-09-14T09:00", end = "2026-09-14T10:00" }).Result.Json(201);
        var made = await pm.Post($"{root}/meetings", new { title = "Client Progress Meeting #4", meetingDate = "2026-09-14", meetingType = "Client", notesLink = "https://contoso.sharepoint.com/minutes/4", calendarEventId = ev.G("id") });
        var m = await made.Json(201);
        Assert.Equal(MeetingKeys, Keys(m));
        Assert.Equal($"/api/v1/meetings/{m.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal((p.Id, "Client Progress Meeting #4", "2026-09-14", "Client", "https://contoso.sharepoint.com/minutes/4", ev.G("id"), Mv(m.G("id"))),
            (m.G("projectId"), m.S("title"), m.S("meetingDate"), m.S("meetingType"), m.S("notesLink"), m.G("calendarEventId"), m.I("rowVersion")));
        var quick = await pm.Post($"{root}/meetings", new { title = "Quick sync" }).Result.Json(201); // type and date default
        Assert.Equal(MeetingKeys, Keys(quick));
        Assert.Equal(("Coordination", "2026-09-14"), (quick.S("meetingType"), quick.S("meetingDate")));
        Assert.True(quick.AsObject().ContainsKey("notesLink")); Assert.Null(quick["notesLink"]);
        Assert.True(quick.AsObject().ContainsKey("calendarEventId")); Assert.Null(quick["calendarEventId"]);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"{root}/meetings", new { title = "Bad event", calendarEventId = Guid.NewGuid() })).StatusCode);

        // Today's coordination meeting: 200, the same properties, and the same row for the next caller.
        var current = await pm.Post($"{root}/meetings/current", new { }).Result.Json();
        Assert.Equal(MeetingKeys, Keys(current));
        Assert.Equal(quick.G("id"), current.G("id")); // today's coordination meeting already exists: the quick sync
        Assert.Equal(current.G("id"), (await f.As(TestData.Marc).Post($"{root}/meetings/current", new { }).Result.Json()).G("id"));

        // Editing (uncalled before): 200 with the same properties on the new version, nulls written for what was cleared.
        var before = Mv(m.G("id"));
        var edited = await pm.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "Client Progress Meeting #4 (revised)", meetingDate = "2026-09-15", meetingType = "Design Review", notesLink = (string?)null, calendarEventId = (Guid?)null }, before).Result.Json();
        Assert.Equal(MeetingKeys, Keys(edited));
        Assert.Equal((m.G("id"), "Client Progress Meeting #4 (revised)", "2026-09-15", "Design Review", before + 1), (edited.G("id"), edited.S("title"), edited.S("meetingDate"), edited.S("meetingType"), edited.I("rowVersion")));
        Assert.Null(edited["notesLink"]); Assert.Null(edited["calendarEventId"]);
        Assert.Equal(428, (int)(await pm.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "No version" })).StatusCode);
        Assert.Equal("concurrency_conflict", (await pm.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "Stale" }, before).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "Mine" }, Mv(m.G("id")))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/meetings/{Guid.NewGuid()}", new { title = "Gone" }, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "", meetingType = "Nope" }, Mv(m.G("id")))).StatusCode);

        // The list: the meeting with the event's title and how many actions are open and in all, newest date first.
        await pm.Patch($"/api/v1/meetings/{m.S("id")}", new { title = "Client Progress Meeting #4", meetingDate = "2026-09-14", meetingType = "Client", calendarEventId = ev.G("id") }, Mv(m.G("id"))).Result.Json();
        await Action(m, "Send the revised grading plan", "User", U(TestData.Alex));
        var done = await Action(m, "Confirm catch basin spacing", "User", U(TestData.Alex));
        (await Move(done, "Complete", as_: TestData.Alex)).EnsureSuccessStatusCode();
        var list = (await alex.GetAsync($"{root}/meetings").Result.Json()).AsArray();
        Assert.Equal(2, list.Count);
        Assert.All(list, x => Assert.Equal([.. MeetingKeys, "calendarEventTitle", "open", "total"], Keys(x)));
        var row = list.Single(x => x!.G("id") == m.G("id"))!;
        Assert.Equal(("Coordination call", 1, 2), (row.S("calendarEventTitle"), row.I("open"), row.I("total")));
        Assert.Equal(f.Db(db => db.Meetings.Where(x => x.Id == m.G("id")).Select(x => x.RowVersion).First()), row.I("rowVersion"));
        var plain = list.Single(x => x!.G("id") == quick.G("id"))!;
        Assert.True(plain.AsObject().ContainsKey("calendarEventTitle")); Assert.Null(plain["calendarEventTitle"]);
        Assert.Equal((0, 0), (plain.I("open"), plain.I("total")));
        Assert.Equal(list.Select(x => x!.S("meetingDate")).OrderDescending(StringComparer.Ordinal), list.Select(x => x!.S("meetingDate"))); // newest first
    }

    [Fact]
    public async Task Action_commands_keep_their_answers_and_shape() // create, edit (uncalled), transition and convert (040-S15-Meetings)
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var m = await Meeting(p.Id);
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Storm sewer profile" });

        // Creating: 201 with a Location, the key, the version and the warnings list (empty unless the discipline has no lead).
        var made = await pm.Post($"/api/v1/meetings/{m.S("id")}/actions", new { text = "Send the revised grading plan", ownerType = "User", ownerUserId = U(TestData.Alex), dueDate = "2026-09-18", relatedTaskId = task.G("id") });
        var a = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion", "warnings"], Keys(a));
        Assert.Equal($"/api/v1/actions/{a.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Matches(@"^\w+-A01$", a.S("key"));
        Assert.Equal((0, 0), (a.I("rowVersion"), a["warnings"]!.AsArray().Count));
        Assert.Equal(await Version(a), a.I("rowVersion"));
        var geo = await d.Discipline("Geotechnical");
        var q = await d.Project(tweak: b => { b["disciplines"] = new[] { new { disciplineId = geo, leadUserId = (Guid?)null } }; b["members"] = Array.Empty<object>(); });
        var qm = await Meeting(q.Id);
        var nolead = await pm.Post($"/api/v1/meetings/{qm.S("id")}/actions", new { text = "Borehole plan", ownerType = "Discipline", ownerDisciplineId = d.ProjectDiscipline(q.Id, "Geotechnical") }).Result.Json(201);
        Assert.Equal(["id", "key", "rowVersion", "warnings"], Keys(nolead));
        Assert.Equal(Hub.Api.Text.Get("action.no_lead_warning"), Assert.Single(nolead["warnings"]!.AsArray())!.GetValue<string>());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/meetings/{Guid.NewGuid()}/actions", new { text = "x", ownerType = "User", ownerUserId = U(TestData.Alex) })).StatusCode);

        // Editing (uncalled before): 200 with the id and the new version, and the ETag header the page reads.
        var v0 = await Version(a);
        var edit = await pm.Patch($"/api/v1/actions/{a.S("id")}", new { text = "Send the revised grading plan v2", dueDate = "2026-09-19" }, v0);
        var edited = await edit.Json();
        Assert.Equal(["id", "rowVersion"], Keys(edited));
        Assert.Equal((a.G("id"), v0 + 1), (edited.G("id"), edited.I("rowVersion")));
        Assert.Equal($"\"{v0 + 1}\"", edit.Headers.ETag!.Tag);
        var stored = await f.DbAsync(db => db.Actions.AsNoTracking().SingleAsync(x => x.Id == a.G("id")));
        Assert.Equal(("Send the revised grading plan v2", new DateOnly(2026, 9, 19)), (stored.Text, stored.DueDate));
        var m2 = await Meeting(p.Id, "Second meeting");
        var moved = await pm.Patch($"/api/v1/actions/{a.S("id")}", new { meetingId = m2.G("id"), ownerType = "Discipline", ownerUserId = (Guid?)null, ownerDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), ownerExternalPartyId = (Guid?)null, relatedTaskId = (Guid?)null }, await Version(a)).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(moved));
        stored = await f.DbAsync(db => db.Actions.AsNoTracking().SingleAsync(x => x.Id == a.G("id")));
        Assert.Equal((m2.G("id"), ActionOwnerType.Discipline, null, (Guid?)null), (stored.MeetingId, stored.OwnerType, stored.OwnerUserId, stored.RelatedTaskId));
        Assert.Equal("concurrency_conflict", (await pm.Patch($"/api/v1/actions/{a.S("id")}", new { text = "Stale" }, v0).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Patch($"/api/v1/actions/{a.S("id")}", new { text = "No" }, await Version(a))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch($"/api/v1/actions/{a.S("id")}", new { text = "" }, await Version(a))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Patch($"/api/v1/actions/{a.S("id")}", new { meetingId = Guid.NewGuid() }, await Version(a))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Patch($"/api/v1/actions/{Guid.NewGuid()}", new { text = "x" }, 1)).StatusCode);

        // Transition: 200 with the id, the new status and the version, in that order.
        var progressed = await Move(a, "In Progress", as_: TestData.Marc).Result.Json();
        Assert.Equal(["id", "status", "rowVersion"], Keys(progressed));
        Assert.Equal((a.G("id"), "In Progress", await Version(a)), (progressed.G("id"), progressed.S("status"), progressed.I("rowVersion")));

        // Convert: 200 with the id, the action's status after following its task, the version, and the new task.
        var other = await Action(m, "Ask for as-builts", "User", U(TestData.Alex));
        var conv = await pm.Post($"/api/v1/actions/{other.S("id")}/convert", new { rowVersion = await Version(other) }).Result.Json();
        Assert.Equal(["id", "status", "rowVersion", "taskId", "taskKey"], Keys(conv));
        Assert.Equal((other.G("id"), "In Progress", await Version(other)), (conv.G("id"), conv.S("status"), conv.I("rowVersion")));
        var taskRow = await f.DbAsync(db => db.Tasks.AsNoTracking().SingleAsync(x => x.Id == conv.G("taskId")));
        Assert.Equal((taskRow.Key, "Ask for as-builts"), (conv.S("taskKey"), taskRow.Name));
        Assert.Equal("already_converted", (await pm.Post($"/api/v1/actions/{other.S("id")}/convert", new { rowVersion = await Version(other) }).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post($"/api/v1/actions/{a.S("id")}/convert", new { rowVersion = await Version(a) })).StatusCode);
    }

    [Fact]
    public async Task Action_detail_keeps_its_nesting_nulls_and_permissions() // GET /actions/{id} was a Task<object> over anonymous objects (040-S15-Meetings)
    {
        var p = await d.Project();
        var (pm, rita) = (f.As(TestData.Pm), f.As(TestData.Rita));
        var m = await Meeting(p.Id);
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Storm sewer profile" });
        var decision = await pm.Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Select pump supplier", description = "Options", ownerUserId = U(TestData.Alex), requiredByDate = "2026-09-20", impactLevel = "Medium", impactDescription = "Drawings cannot progress" }).Result.Json(201);
        var a = await pm.Post($"/api/v1/meetings/{m.S("id")}/actions", new { text = "Send the revised grading plan", ownerType = "User", ownerUserId = U(TestData.Alex), dueDate = "2026-09-18",
            relatedTaskId = task.G("id"), relatedDecisionId = decision.G("id"), links = new[] { new { targetType = "Task", targetId = task.G("id") } } }).Result.Json(201);

        // The whole envelope for the person who can edit: every section written, the related task and decision, one link, the discipline a task would join.
        var detail = await f.As(TestData.Alex).GetAsync($"/api/v1/actions/{a.S("id")}").Result.Json();
        Assert.Equal(["action", "meeting", "project", "task", "decision", "links", "taskDisciplineId", "permissions"], Keys(detail));
        Assert.Equal(ActionKeys, Keys(detail["action"]));
        Assert.Equal((a.G("id"), a.S("key"), "Send the revised grading plan", "Open", "Alex Chen", task.S("key"), decision.S("key")),
            (detail["action"]!.G("id"), detail["action"]!.S("key"), detail["action"]!.S("text"), detail["action"]!.S("status"), detail["action"]!.S("ownerName"), detail["action"]!.S("taskKey"), detail["action"]!.S("decisionKey")));
        Assert.Equal(["id", "title", "meetingDate", "meetingType", "notesLink"], Keys(detail["meeting"]));
        Assert.Equal((m.G("id"), "Client Progress Meeting #4", "2026-09-14", "Client", "https://contoso.sharepoint.com/minutes/4"),
            (detail["meeting"]!.G("id"), detail["meeting"]!.S("title"), detail["meeting"]!.S("meetingDate"), detail["meeting"]!.S("meetingType"), detail["meeting"]!.S("notesLink")));
        Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detail["project"]));
        Assert.Equal((p.Id, p.ProjectNumber, p.Status), (detail["project"]!.G("id"), detail["project"]!.S("projectNumber"), detail["project"]!.S("status")));
        Assert.Equal(["id", "key", "name", "status"], Keys(detail["task"]));
        Assert.Equal((task.G("id"), "Storm sewer profile", "Not Started"), (detail["task"]!.G("id"), detail["task"]!.S("name"), detail["task"]!.S("status")));
        Assert.Equal(["id", "key", "subject", "status"], Keys(detail["decision"]));
        Assert.Equal((decision.G("id"), "Select pump supplier", "Pending"), (detail["decision"]!.G("id"), detail["decision"]!.S("subject"), detail["decision"]!.S("status")));
        var link = Assert.Single(detail["links"]!.AsArray())!;
        Assert.Equal(["id", "targetType", "targetId", "relation", "key", "name", "status", "date", "person"], Keys(link));
        Assert.Equal(("Task", task.G("id"), "related"), (link.S("targetType"), link.G("targetId"), link.S("relation")));
        Assert.Equal(d.ProjectDiscipline(p.Id, "Civil"), detail.G("taskDisciplineId"));
        var permissions = detail["permissions"]!;
        Assert.Equal(["edit", "transitions", "convert", "comment"], Keys(permissions));
        Assert.Equal(["ok", "reason"], Keys(permissions["edit"]));
        Assert.True(permissions["edit"]!["ok"]!.GetValue<bool>()); Assert.Null(permissions["edit"]!["reason"]);
        var transitions = permissions["transitions"]!.AsArray();
        Assert.Equal(["In Progress", "Complete", "Cancelled"], transitions.Select(x => x!.S("to")));
        Assert.All(transitions, x => Assert.Equal(["to", "ok", "reason"], Keys(x)));
        Assert.True(permissions["convert"]!.GetValue<bool>()); Assert.True(permissions["comment"]!.GetValue<bool>());
        Assert.Equal(detail["action"]!.S("key"), (await pm.GetAsync($"/api/v1/actions/{a.S("key")}").Result.Json())["action"]!.S("key")); // by key too

        // The same action for a Read Only account: nothing to do, the refusal text where the null was, no task, decision, link or convert on a bare action.
        var bare = await Action(m, "Provide the utility locates", "External Party", await Client(p.Id));
        var view = await rita.GetAsync($"/api/v1/actions/{bare.S("id")}").Result.Json();
        Assert.Equal(["action", "meeting", "project", "task", "decision", "links", "taskDisciplineId", "permissions"], Keys(view));
        Assert.True(view.AsObject().ContainsKey("task")); Assert.Null(view["task"]);
        Assert.True(view.AsObject().ContainsKey("decision")); Assert.Null(view["decision"]);
        Assert.True(view.AsObject().ContainsKey("taskDisciplineId")); Assert.Null(view["taskDisciplineId"]);
        Assert.Empty(view["links"]!.AsArray());
        Assert.False(view["permissions"]!["edit"]!["ok"]!.GetValue<bool>());
        var why = view["permissions"]!["edit"]!.S("reason");
        Assert.False(string.IsNullOrEmpty(why));
        Assert.All(view["permissions"]!["transitions"]!.AsArray(), x => Assert.Equal((false, why), (x!["ok"]!.GetValue<bool>(), x.S("reason"))));
        Assert.False(view["permissions"]!["convert"]!.GetValue<bool>()); Assert.False(view["permissions"]!["comment"]!.GetValue<bool>());

        // Converted: the task decides, so no transitions and no convert, and no discipline to join.
        var conv = await pm.Post($"/api/v1/actions/{a.S("id")}/convert", new { rowVersion = await Version(a) }).Result.Json();
        var after = await pm.GetAsync($"/api/v1/actions/{a.S("id")}").Result.Json();
        Assert.Equal(conv.S("taskKey"), after["task"]!.S("key"));
        Assert.Empty(after["permissions"]!["transitions"]!.AsArray());
        Assert.False(after["permissions"]!["convert"]!.GetValue<bool>());
        Assert.Null(after["taskDisciplineId"]);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/actions/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync("/api/v1/actions/NO-SUCH-A99")).StatusCode);
    }

    [Fact]
    public async Task Action_register_export_follows_the_register_filters() // GET actions/export was uncalled (040-S15-Meetings)
    {
        var p = await d.Project();
        var m = await Meeting(p.Id);
        var mine = await Action(m, "Send the revised grading plan", "User", U(TestData.Alex), due: "2026-09-18");
        var civil = await Action(m, "Confirm catch basin spacing", "Discipline", d.ProjectDiscipline(p.Id, "Civil"), due: "2026-09-10");
        (await Move(civil, "Complete", as_: TestData.Marc)).EnsureSuccessStatusCode();

        var all = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/actions/export?format=csv");
        Assert.Equal(("text/csv", true), (all.Content.Headers.ContentType!.MediaType, all.Content.Headers.ContentDisposition!.FileName!.Contains("-actions-")));
        var lines = (await all.Content.ReadAsStringAsync()).TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.Equal("Key,Action,Meeting,Meeting date,Owner,Owner type,Due,Days overdue,Status,Task", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Equal($"{civil.S("key")},Confirm catch basin spacing,Client Progress Meeting #4,2026-09-14,Civil,Discipline,2026-09-10,0,Complete,", lines[1]); // by due date
        Assert.Equal($"{mine.S("key")},Send the revised grading plan,Client Progress Meeting #4,2026-09-14,Alex Chen,Person,2026-09-18,0,Open,", lines[2]);

        var open = (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/actions/export?format=csv&status=Open&ownerType=User").Result.Content.ReadAsStringAsync()).TrimEnd().Split('\n');
        Assert.Equal(2, open.Length);
        Assert.StartsWith(mine.S("key"), open[1]);
        var xlsx = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/actions/export?format=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{Guid.NewGuid()}/actions/export?format=csv")).StatusCode);
    }

    // ---------- AC-DCV-05: capture reuses an existing action instead of creating duplicate work ----------

    sealed record Sources(Project P, Guid Meeting, Guid Notice, Guid NoticeTarget, Guid Handoff, Guid HandoffTask);
    int Rv<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    /// A published change with a pending assessment on one Electrical task, a draft handoff blocking another, and today's
    /// coordination meeting.
    async Task<Sources> NewSources()
    {
        var p = await d.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var (civil, electrical) = (d.ProjectDiscipline(p.Id, "Civil"), d.ProjectDiscipline(p.Id, "Electrical"));
        async Task<Guid> Deliverable(string name) => (await f.As(TestData.Marc).Post($"{root}/deliverables", new { name, projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = U(TestData.Alex), revision = "A", requiresReview = false,
            transmittalUrl = $"https://example.test/{name}-A.pdf" }).Result.Json(201)).G("id");
        int Head() => f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion);
        var survey = await Deliverable("survey");
        ChangeEndpoints.RegisterBody Revision(string rev, Guid? old) => new(Guid.NewGuid(), survey, Rv<Deliverable>(survey), civil, U(TestData.Alex), "Deliverable",
            "survey", "Coordinated survey", rev, $"https://example.test/survey-{rev}.pdf", "Survey team", "East corridor", null, old, old is null ? null : Head(),
            old is null ? null : "Changed corridor alignment", old is null ? null : new DateOnly(2026, 9, 14), old is null ? null : new DateOnly(2026, 9, 18));
        var a = (await f.As(TestData.Alex).Post($"{root}/source-revisions", Revision("A", null)).Result.Json()).G("id");
        var target = (await d.NewTask(p.Id, TestData.Omar, new { assigneeId = U(TestData.Omar), dueDate = "2026-09-20" }, "Electrical")).G("id");
        await f.As(TestData.Omar).Post($"{root}/input-uses", new ChangeEndpoints.AdoptBody(Guid.NewGuid(), "Task", target, Rv<WorkTask>(target), a, a, null,
            "Coordinate service alignment", "Incorporated into design basis")).Result.Json();
        var notice = (await f.As(TestData.Alex).Post($"{root}/source-revisions", Revision("B", a)).Result.Json()).G("id");
        await f.As(TestData.Alex).Post($"{root}/changes/{notice}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Rv<ChangeNotice>(notice), Head(), null)).Result.Json();
        var basis = await Deliverable("basis");
        var blocked = (await d.NewTask(p.Id, TestData.Omar, new { assigneeId = U(TestData.Omar), dueDate = "2026-09-16" }, "Electrical")).G("id");
        var handoff = (await f.As(TestData.Marc).Post($"{root}/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Survey to Electrical", basis, Rv<Deliverable>(basis),
            "A", "https://example.test/basis-A.pdf", electrical, U(TestData.Alex), U(TestData.Omar), blocked, null, "Set the electrical service alignment",
            "Survey covers the entire service corridor", new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 18), null, null)).Result.Json(201)).G("id");
        var meeting = (await f.As(TestData.Pm).Post($"{root}/meetings/current", new { }).Result.Json()).G("id");
        return new(p, meeting, notice, target, handoff, blocked);
    }

    /// What meeting mode posts when it captures a new action against a source row: the source and its affected work.
    async Task<Guid> Captured(Sources w, string text, params (string Type, Guid Id)[] links) =>
        (await f.As(TestData.Pm).Post($"/api/v1/meetings/{w.Meeting}/actions", new { text, ownerType = "User", ownerUserId = U(TestData.Omar), dueDate = "2026-09-18",
            links = links.Select(l => new { targetType = l.Type, targetId = l.Id }) }).Result.Json(201)).G("id");

    MeetingEndpoints.ReuseBody Reuse(Guid action, params (string Type, Guid Id)[] links) =>
        new(Guid.NewGuid(), Rv<MeetingAction>(action), [.. links.Select(l => new DecisionEndpoints.LinkInput(l.Type, l.Id, null))], null);

    Task<HttpResponseMessage> Send(Sources w, Guid action, MeetingEndpoints.ReuseBody body, string as_ = TestData.Pm) =>
        f.As(as_).Post($"/api/v1/projects/{w.P.Id}/actions/{action}/reuse", body);

    Guid[] Targets(Guid action) => [.. f.Db(db => db.ItemLinks.Where(l => l.SourceId == action).Select(l => l.TargetId).ToArray()).Order()];
    static (Guid, string, Guid)[] Pairs(JsonNode data) =>
        [.. data["linkedActions"]!.AsArray().Select(x => (x!.G("id"), x.S("sourceType"), x.G("sourceId"))).Order()];

    [Fact]
    public async Task AC_DCV_05_capture_reuses_a_selected_action_for_another_source_without_duplicate_work()
    {
        var w = await NewSources();
        var first = await Captured(w, "Check the revised corridor alignment", (ItemType.ChangeNotice, w.Notice), (ItemType.Task, w.NoticeTarget));
        var second = await Captured(w, "Confirm the corridor clearance", (ItemType.ChangeNotice, w.Notice)); // the change now has several linked actions
        var notices = f.Db(db => db.Notifications.Count(n => n.ItemId == first));
        var version = Rv<MeetingAction>(first);

        var reused = await (await Send(w, first, Reuse(first, (ItemType.Handoff, w.Handoff), (ItemType.Task, w.HandoffTask)))).Json();
        Assert.Equal((first, version + 1), (reused.G("id"), reused.I("rowVersion")));
        Assert.Equal(2, f.Db(db => db.Actions.Count(a => a.ProjectId == w.P.Id))); // reused, not duplicated
        Assert.Equal(new[] { w.Notice, w.NoticeTarget, w.Handoff, w.HandoffTask }.Order(), Targets(first));
        Assert.Equal(notices, f.Db(db => db.Notifications.Count(n => n.ItemId == first))); // nobody is told about it again

        // Reusing an action already linked to this change adds only the missing affected work.
        await (await Send(w, second, Reuse(second, (ItemType.ChangeNotice, w.Notice), (ItemType.Task, w.NoticeTarget)))).Json();
        Assert.Equal(new[] { w.Notice, w.NoticeTarget }.Order(), Targets(second));
        Assert.Equal(2, f.Db(db => db.Actions.Count(a => a.ProjectId == w.P.Id)));
        Assert.Equal(1, f.Db(db => db.ActivityLog.Count(x => x.ItemId == first && x.Action == "Reused")));

        // The reused action shows under each source it is linked to, in the project view and in the workspace projection that
        // the My Work CSV exports, while the action register export still lists it once.
        (Guid, string, Guid)[] expected = [.. new[] { (first, ItemType.ChangeNotice, w.Notice), (first, ItemType.Handoff, w.Handoff), (second, ItemType.ChangeNotice, w.Notice) }.Order()];
        var project = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{w.P.Id}/discipline-coordination").Result.Json();
        Assert.Equal(expected, Pairs(project));
        var paged = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{w.P.Id}/discipline-coordination?page=9&pageSize=1").Result.Json();
        Assert.Equal(expected, Pairs(paged)); // capture context stays complete for each visible source, regardless of requested page
        var workspace = await f.As(TestData.Pm).GetAsync($"/api/v1/discipline-coordination?projectId={w.P.Id}").Result.Json();
        Assert.Equal(expected, Pairs(Assert.Single(workspace["projects"]!.AsArray())!["data"]!));
        var key = f.Db(db => db.Actions.Single(a => a.Id == first).Key);
        var csv = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{w.P.Id}/actions/export?format=csv").Result.Content.ReadAsStringAsync();
        Assert.Single(csv.Split('\n'), line => line.Contains(key));
        var detail = await f.As(TestData.Omar).GetAsync($"/api/v1/actions/{first}").Result.Json();
        Assert.Equal(new[] { ItemType.ChangeNotice, ItemType.Handoff, ItemType.Task, ItemType.Task }.Order(),
            detail["links"]!.AsArray().Select(l => l!.S("targetType")).Order());
    }

    [Fact]
    public async Task Action_reuse_refuses_unpermitted_closed_or_unsourced_requests_and_follows_the_project_lifecycle()
    {
        var w = await NewSources();
        var action = await Captured(w, "Check the revised corridor alignment", (ItemType.ChangeNotice, w.Notice));
        var toHandoff = (ItemType.Handoff, w.Handoff);
        async Task Expect(int status, string who, MeetingEndpoints.ReuseBody? body = null) =>
            await (await Send(w, action, body ?? Reuse(action, toHandoff), who)).Json(status);
        async Task SetStatus(string status) => await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == w.P.Id)).Status = status; return await db.SaveChangesAsync(); });

        await Expect(403, TestData.Alex); // an unrelated member: only the owner, its creator, the owning discipline's lead or the PM
        await Expect(403, TestData.Rita); // Read Only
        var foreign = (await d.NewTask((await d.Project()).Id, TestData.Marc)).G("id");
        await Expect(400, TestData.Pm, Reuse(action, toHandoff, (ItemType.Task, foreign))); // links stay inside the project
        await Expect(400, TestData.Pm, Reuse(action, (ItemType.Task, w.HandoffTask))); // reuse names the handoff or change
        Assert.Equal(new[] { w.Notice }, Targets(action)); // nothing partial was kept

        await SetStatus(ProjectStatus.OnHold);
        await Expect(200, TestData.Omar); // the owner, on hold
        foreach (var closed in new[] { ProjectStatus.Archived, ProjectStatus.Cancelled })
        {
            await SetStatus(closed);
            await Expect(403, TestData.Pm);
        }
        await SetStatus(ProjectStatus.Complete);
        await Expect(403, TestData.Omar); // ownership does not bypass the PM-only rule
        await Expect(400, TestData.Pm); // a correction needs a reason
        await Expect(200, TestData.Pm, Reuse(action, toHandoff) with { Reason = "Late corridor follow-up" });
        Assert.Equal(1, f.Db(db => db.ActivityLog.Count(x => x.ItemId == action && x.Action == "Reused" && x.Reason == "Late corridor follow-up")));

        await SetStatus(ProjectStatus.Active);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == w.P.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await Expect(404, TestData.Jill); // a restricted project's non-member cannot see it, let alone link to it
        (await Move(new JsonObject { ["id"] = action.ToString() }, "Complete")).EnsureSuccessStatusCode();
        await Expect(400, TestData.Pm); // only open work is reused; a closed action needs a separate one
    }

    [Fact]
    public async Task Action_reuse_checks_the_version_it_read_and_replays_a_retried_request()
    {
        var w = await NewSources();
        var action = await Captured(w, "Check the revised corridor alignment", (ItemType.ChangeNotice, w.Notice));
        var body = Reuse(action, (ItemType.Handoff, w.Handoff), (ItemType.Task, w.HandoffTask));
        var notices = f.Db(db => db.Notifications.Count(n => n.ItemId == action));

        await (await Send(w, action, body with { RowVersion = null })).Json(428);
        var stale = await (await Send(w, action, body with { RowVersion = body.RowVersion - 1 })).Json(409);
        Assert.Equal(body.RowVersion, stale.I("currentRowVersion"));
        Assert.Equal(new[] { w.Notice }, Targets(action));

        var first = await (await Send(w, action, body)).Json();
        var retry = await (await Send(w, action, body)).Json(); // the same request again, e.g. after a dropped response
        Assert.Equal((first.G("id"), first.I("rowVersion")), (retry.G("id"), retry.I("rowVersion")));
        Assert.Equal(first.I("rowVersion"), Rv<MeetingAction>(action));
        Assert.Equal(3, Targets(action).Length);
        Assert.Equal(1, f.Db(db => db.ActivityLog.Count(x => x.ItemId == action && x.Action == "Reused")));
        await (await Send(w, action, body with { Reason = "Different request" })).Json(422); // a reused request id with other content
        await (await Send(w, action, body with { RequestId = Guid.NewGuid() })).Json(409); // another chair still holding the old version

        // Two chairs reusing the same version at once: one wins, the other must refresh.
        var current = Reuse(action, (ItemType.Handoff, w.Handoff));
        var race = await Task.WhenAll(Send(w, action, current), Send(w, action, current with { RequestId = Guid.NewGuid() }));
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, race.Select(r => r.StatusCode).Order());
        Assert.Equal(notices, f.Db(db => db.Notifications.Count(n => n.ItemId == action)));
    }
}
