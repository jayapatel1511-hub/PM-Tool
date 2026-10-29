using System.Net;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The milestone routes answer with typed records that must serialise to the bytes the app already reads (040-S15-Milestones).
/// Its own host and database, like DeliverableContractTests: the shape snapshot's data-dependent reads were seen only when the
/// shared host's tests and their order stayed as they were. The test clock reads 2026-09-14.
public sealed class MilestoneContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData d = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];

    async Task Flag(string name, bool on) => await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on }).Result.Json();
    Task<int> V(JsonNode n) => f.DbAsync(db => db.Milestones.IgnoreQueryFilters().Where(x => x.Id == n.G("id")).Select(x => x.RowVersion).FirstAsync());
    Task<JsonNode> Make(Guid projectId, string name, string date, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["name"] = name, ["milestoneType"] = "Design Submission", ["date"] = date };
        if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/milestones", body).Result.Json(201);
    }

    [Fact]
    public async Task Create_edit_complete_reopen_cancel_delete_and_restore_answers_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm); var rita = f.As(TestData.Rita);
        var type = await d.DeliverableType(); var civil = d.ProjectDiscipline(p.Id, "Civil");
        var phase = (await d.Ref())["phases"]![0]!.G("id");

        // Create: 201 with a Location, and the id, key and row version.
        var made = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Pin A", milestoneType = "Design Submission", date = "2026-09-16", completesPhaseId = phase });
        var a = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion"], Keys(a));
        Assert.Equal($"/api/v1/milestones/{a.S("id")}", made.Headers.Location!.OriginalString); Assert.Equal(0, a.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "No", milestoneType = "Other", date = "2026-09-16" })).StatusCode);
        Assert.NotNull((await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "No date", milestoneType = "Other" }).Result.Json(400))["errors"]!["date"]);
        Assert.NotNull((await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Bad", milestoneType = "Nope", date = "2026-09-16" }).Result.Json(400))["errors"]!["milestoneType"]);

        // Edit: the id and the new row version; the version is required and checked.
        var edited = await pm.Patch($"/api/v1/milestones/{a.S("id")}", new { name = "Pin A renamed", description = "Pinned" }, await V(a)).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(edited)); Assert.Equal(1, edited.I("rowVersion"));
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await pm.Patch($"/api/v1/milestones/{a.S("id")}", new { name = "No version" })).StatusCode);
        Assert.Equal("concurrency_conflict", (await pm.Patch($"/api/v1/milestones/{a.S("id")}", new { name = "Stale" }, 0).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Patch($"/api/v1/milestones/{a.S("id")}", new { name = "Nope" }, await V(a))).StatusCode);

        // Complete asks first while a targeted deliverable is open: the 409 lists each as id, key, name and status.
        var del = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Pin D", projectDisciplineId = civil, deliverableTypeId = type, milestoneId = a.G("id") }).Result.Json(201);
        var ask = await pm.Post($"/api/v1/milestones/{a.S("id")}/complete", new { rowVersion = await V(a) }).Result.Json(409);
        Assert.Equal("confirm_open_deliverables", ask.S("code"));
        var open = Assert.Single(ask["deliverables"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "status"], Keys(open)); Assert.Equal((del.G("id"), del.S("key"), "Pin D", "Not Started"), (open.G("id"), open.S("key"), open.S("name"), open.S("status")));
        // Completing suggests the phase it completes, as an id and a name; the project is not moved.
        var done = await pm.Post($"/api/v1/milestones/{a.S("id")}/complete", new { confirm = true, completedDate = "2026-09-13", rowVersion = await V(a) }).Result.Json();
        Assert.Equal(["id", "rowVersion", "suggestPhase"], Keys(done));
        Assert.Equal(["id", "name"], Keys(done["suggestPhase"])); Assert.Equal(phase, done["suggestPhase"]!.G("id"));
        var plain = await Make(p.Id, "Pin B", "2026-09-16");
        var noPhase = await pm.Post($"/api/v1/milestones/{plain.S("id")}/complete", new { rowVersion = await V(plain) }).Result.Json();
        Assert.Equal(["id", "rowVersion", "suggestPhase"], Keys(noPhase)); Assert.Null(noPhase["suggestPhase"]); // null, not omitted

        // Reopen: the id and the row version; it needs a completed milestone and a reason.
        var again = await Make(p.Id, "Pin C", "2026-09-16");
        Assert.NotNull((await pm.Post($"/api/v1/milestones/{again.S("id")}/reopen", new { reason = "Mistake made", rowVersion = await V(again) }).Result.Json(400))["errors"]!["reason"]); // not complete yet
        await pm.Post($"/api/v1/milestones/{again.S("id")}/complete", new { rowVersion = await V(again) }).Result.Json();
        Assert.NotNull((await pm.Post($"/api/v1/milestones/{again.S("id")}/reopen", new { rowVersion = await V(again) }).Result.Json(400))["errors"]!["reason"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/milestones/{again.S("id")}/reopen", new { reason = "Mistake made", rowVersion = await V(again) })).StatusCode);
        var reopened = await pm.Post($"/api/v1/milestones/{again.S("id")}/reopen", new { reason = "Mistake made", rowVersion = await V(again) }).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(reopened)); Assert.Equal(again.G("id"), reopened.G("id"));
        Assert.False(await f.DbAsync(db => db.Milestones.Where(x => x.Id == again.G("id")).Select(x => x.IsComplete).SingleAsync()));

        // Cancel: the id, the row version and the deliverables still aimed at it, each as id, key, name and row version; [] when none.
        var target = await Make(p.Id, "Pin D target", "2026-12-01");
        var held = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Pin E", projectDisciplineId = civil, deliverableTypeId = type, milestoneId = target.G("id") }).Result.Json(201);
        Assert.NotNull((await pm.Post($"/api/v1/milestones/{target.S("id")}/cancel", new { rowVersion = await V(target) }).Result.Json(400))["errors"]!["reason"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/milestones/{target.S("id")}/cancel", new { reason = "Scope dropped", rowVersion = await V(target) })).StatusCode);
        var cancelled = await pm.Post($"/api/v1/milestones/{target.S("id")}/cancel", new { reason = "Scope dropped", rowVersion = await V(target) }).Result.Json();
        Assert.Equal(["id", "rowVersion", "retarget"], Keys(cancelled));
        var row = Assert.Single(cancelled["retarget"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "rowVersion"], Keys(row)); Assert.Equal((held.G("id"), held.S("key"), "Pin E"), (row.G("id"), row.S("key"), row.S("name")));
        var bare = await Make(p.Id, "Pin F", "2026-12-01");
        var none = await pm.Post($"/api/v1/milestones/{bare.S("id")}/cancel", new { reason = "Not needed", rowVersion = await V(bare) }).Result.Json();
        Assert.Equal(["id", "rowVersion", "retarget"], Keys(none)); Assert.Empty(none["retarget"]!.AsArray()); // [] not omitted

        // Delete: 204 with no body; a milestone with deliverables or tasks is refused; a second delete and an unknown id are 404.
        Assert.Equal("milestone_in_use", (await pm.DeleteAsync($"/api/v1/milestones/{target.S("id")}").Result.Json(422)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.DeleteAsync($"/api/v1/milestones/{bare.S("id")}")).StatusCode);
        var gone = await pm.DeleteAsync($"/api/v1/milestones/{bare.S("id")}");
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode); Assert.Empty(await gone.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.DeleteAsync($"/api/v1/milestones/{bare.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.DeleteAsync($"/api/v1/milestones/{Guid.NewGuid()}")).StatusCode);

        // Restore (behind its flag): the id and the row version.
        Assert.Equal(HttpStatusCode.NotFound, (await pm.PostAsync($"/api/v1/milestones/{bare.S("id")}/restore", null)).StatusCode);
        await Flag("show_deleted", true);
        try
        {
            var restored = await pm.PostAsync($"/api/v1/milestones/{bare.S("id")}/restore", null).Result.Json();
            Assert.Equal(["id", "rowVersion"], Keys(restored)); Assert.Equal(bare.G("id"), restored.G("id"));
        }
        finally { await Flag("show_deleted", false); }
    }

    [Fact]
    public async Task Change_date_preview_and_result_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var type = await d.DeliverableType(); var civil = d.ProjectDiscipline(p.Id, "Civil");
        var m = await Make(p.Id, "Move me", "2027-03-01");
        var late = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Late", projectDisciplineId = civil, deliverableTypeId = type, milestoneId = m.G("id"), dueDate = "2027-02-20", startDate = "2027-02-01" }).Result.Json(201);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = "Shifted", deliverableId = late.G("id"), startDate = "2027-02-02" }); // a start and no due date: its due dates are null
        var url = $"/api/v1/milestones/{m.S("id")}/change-date";

        var dry = await pm.Post(url, new { newDate = "2027-03-15", cascadeDeliverables = true, includeTasks = true, dryRun = true }).Result.Json();
        Assert.Equal(["milestone", "deliverables", "tasks", "inconsistent"], Keys(dry));
        Assert.Equal(["key", "name", "oldDate", "newDate", "slipDays", "delta"], Keys(dry["milestone"]));
        Assert.Equal(("2027-03-01", "2027-03-15", 14, 14), (dry["milestone"]!.S("oldDate"), dry["milestone"]!.S("newDate"), dry["milestone"]!.I("slipDays"), dry["milestone"]!.I("delta")));
        var shifted = Assert.Single(dry["deliverables"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "oldDue", "newDue"], Keys(shifted)); Assert.Equal(("2027-02-20", "2027-03-06"), (shifted.S("oldDue"), shifted.S("newDue")));
        var moved = Assert.Single(dry["tasks"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "oldDue", "newDue", "oldStart", "newStart"], Keys(moved));
        Assert.Null(moved["oldDue"]); Assert.Null(moved["newDue"]); Assert.Equal(("2027-02-02", "2027-02-16"), (moved.S("oldStart"), moved.S("newStart"))); // nulls kept, not omitted
        Assert.Empty(dry["inconsistent"]!.AsArray());

        // Moving earlier without the cascade leaves the deliverable due after its milestone: key, name and due date.
        var left = await pm.Post(url, new { newDate = "2027-02-10", dryRun = true }).Result.Json();
        Assert.Empty(left["deliverables"]!.AsArray()); Assert.Empty(left["tasks"]!.AsArray());
        var behind = Assert.Single(left["inconsistent"]!.AsArray())!;
        Assert.Equal(["key", "name", "dueDate"], Keys(behind)); Assert.Equal((late.S("key"), "Late", "2027-02-20"), (behind.S("key"), behind.S("name"), behind.S("dueDate")));
        Assert.Equal(-19, left["milestone"]!.I("slipDays"));

        // A real move: the id, the row version, how many rows shifted and the same preview. It needs a reason of five letters or more.
        Assert.NotNull((await pm.Post(url, new { newDate = "2027-03-15", rowVersion = await V(m) }).Result.Json(400))["errors"]!["reason"]);
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await pm.Post(url, new { newDate = "2027-03-15", reason = "Client asked" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Post(url, new { newDate = "2027-03-15", reason = "Client asked", rowVersion = await V(m) })).StatusCode);
        var done = await pm.Post(url, new { newDate = "2027-03-15", reason = "Client asked", cascadeDeliverables = true, includeTasks = true, rowVersion = await V(m) }).Result.Json();
        Assert.Equal(["id", "rowVersion", "shifted", "preview"], Keys(done)); Assert.Equal(2, done.I("shifted"));
        Assert.Equal(["milestone", "deliverables", "tasks", "inconsistent"], Keys(done["preview"]));
        Assert.Equal(["id", "key", "name", "oldDue", "newDue"], Keys(done["preview"]!["deliverables"]![0]));
        Assert.Equal(["id", "key", "name", "oldDue", "newDue", "oldStart", "newStart"], Keys(done["preview"]!["tasks"]![0]));
        Assert.Equal("2027-03-06", await f.DbAsync(async db => (await db.Deliverables.Where(x => x.Id == late.G("id")).Select(x => x.DueDate).SingleAsync())!.Value.ToString("yyyy-MM-dd")));
        Assert.Equal("2027-02-16", await f.DbAsync(async db => (await db.Tasks.Where(x => x.Id == task.G("id")).Select(x => x.StartDate).SingleAsync())!.Value.ToString("yyyy-MM-dd")));
        var plain = await pm.Post(url, new { newDate = "2027-03-20", reason = "Plain move", rowVersion = await V(m) }).Result.Json();
        Assert.Equal(0, plain.I("shifted")); Assert.Empty(plain["preview"]!["deliverables"]!.AsArray());
        Assert.Equal("too_early", (await pm.Post($"/api/v1/milestones/{m.S("id")}/complete", new { confirm = true, rowVersion = await V(m) }).Result.Json(422)).S("code")); // more than 7 days ahead
        var soon = await Make(p.Id, "Closed", "2026-09-16");
        await pm.Post($"/api/v1/milestones/{soon.S("id")}/complete", new { rowVersion = await V(soon) }).Result.Json();
        Assert.NotNull((await pm.Post($"/api/v1/milestones/{soon.S("id")}/change-date", new { newDate = "2026-09-20", reason = "Client asked", rowVersion = await V(soon) }).Result.Json(400))["errors"]!["newDate"]); // a closed milestone does not move
    }

    [Fact]
    public async Task Register_rows_and_detail_keep_their_order_and_omit_the_baseline_fields_while_their_flag_is_off()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var type = await d.DeliverableType(); var civil = d.ProjectDiscipline(p.Id, "Civil");
        var phase = (await d.Ref())["phases"]![0]!.G("id");
        var m = await Make(p.Id, "Row", "2027-03-01", new { description = "Row note", projectDisciplineId = civil, completesPhaseId = phase });
        await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Row D", projectDisciplineId = civil, deliverableTypeId = type, milestoneId = m.G("id"), dueDate = "2027-02-20" }).Result.Json(201);
        await d.NewTask(p.Id, TestData.Pm, new { name = "Direct", milestoneId = m.G("id"), dueDate = "2027-02-12", assigneeId = d.User(TestData.Alex) });
        await f.Evaluate(p.Id);
        string[] row = ["id", "projectId", "key", "name", "milestoneType", "date", "originalDate", "description", "projectDisciplineId", "completesPhaseId", "isClientFacing", "isComplete", "completedDate",
            "isCancelled", "cancelledReason", "rowVersion", "isSubmission", "status", "statusReasons", "slipDays", "daysRemaining", "deliverableTotal", "deliverableIssued", "taskTotal",
            "taskComplete", "taskOpen", "taskOverdue", "taskBlocked"];

        var list = await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones").Result.Json();
        Assert.Equal(row, Keys(Assert.Single(list.AsArray())));
        Assert.Equal((1, 0, 1), (list[0]!.I("deliverableTotal"), list[0]!.I("deliverableIssued"), list[0]!.I("taskTotal")));
        var bare = await Make(p.Id, "Never evaluated", "2027-04-01");
        await f.DbAsync(async db => { db.MilestoneStates.RemoveRange(db.MilestoneStates.Where(s => s.MilestoneId == bare.G("id"))); await db.SaveChangesAsync(); return 0; });
        var unevaluated = (await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones").Result.Json()).AsArray().Single(x => x!.S("name") == "Never evaluated")!;
        Assert.Equal(row, Keys(unevaluated)); Assert.Null(unevaluated["status"]); Assert.Null(unevaluated["statusReasons"]); Assert.Null(unevaluated["daysRemaining"]); Assert.Equal(0, unevaluated.I("taskTotal"));
        Assert.Empty((await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones?type=Kickoff").Result.Json()).AsArray());
        Assert.Single((await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones?status=On Track").Result.Json()).AsArray());

        string[] top = ["milestone", "deliverables", "directTasks", "completesPhase", "project", "permissions"];
        var detail = await pm.GetAsync($"/api/v1/milestones/{m.S("id")}").Result.Json();
        Assert.Equal(top, Keys(detail)); Assert.Equal(row, Keys(detail["milestone"]));
        Assert.Equal(["id", "key", "name", "status", "dueDate", "assigneeId"], Keys(Assert.Single(detail["directTasks"]!.AsArray())));
        Assert.Equal(["id", "name"], Keys(detail["completesPhase"])); Assert.Equal(phase, detail["completesPhase"]!.G("id"));
        Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detail["project"]));
        Assert.Equal(["manage", "comment"], Keys(detail["permissions"])); Assert.Equal(["ok", "reason"], Keys(detail["permissions"]!["manage"]));
        Assert.Single(detail["deliverables"]!.AsArray());
        var byKey = await pm.GetAsync($"/api/v1/milestones/{m.S("key")}").Result.Json();
        Assert.Equal(m.G("id"), byKey["milestone"]!.G("id"));
        var empty = await pm.GetAsync($"/api/v1/milestones/{bare.S("id")}").Result.Json();
        Assert.True(empty.AsObject().ContainsKey("completesPhase")); Assert.Null(empty["completesPhase"]); Assert.Empty(empty["directTasks"]!.AsArray()); Assert.Empty(empty["deliverables"]!.AsArray()); // null and [] not omitted
        var readOnly = await f.As(TestData.Rita).GetAsync($"/api/v1/milestones/{m.S("id")}").Result.Json();
        Assert.False(readOnly["permissions"]!["manage"]!["ok"]!.GetValue<bool>()); Assert.NotNull(readOnly["permissions"]!["manage"]!["reason"]);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/milestones/{Guid.NewGuid()}")).StatusCode);

        await Flag("schedule_baseline", true);
        try
        {
            var on = await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones").Result.Json();
            Assert.Equal(["slipShown"], Keys(on[0]).Except(row)); // no baseline yet, so its date, days and slip are omitted
            var base0 = await pm.GetAsync($"/api/v1/milestones/{m.S("id")}").Result.Json();
            Assert.Equal(["slipShown", "canRebaseline"], Keys(base0["milestone"]).Except(row));
            await pm.Post($"/api/v1/milestones/{m.S("id")}/rebaseline", new { reason = "Re-plan", rowVersion = await V(m) }).Result.Json();
            var based = await pm.GetAsync($"/api/v1/milestones/{m.S("id")}").Result.Json();
            Assert.Equal(["slipShown", "baselineDate", "sinceBaselineDays", "baselineSlipShown", "canRebaseline"], Keys(based["milestone"]).Except(row));
            var asRita = await f.As(TestData.Rita).GetAsync($"/api/v1/milestones/{m.S("id")}").Result.Json();
            Assert.False(asRita["milestone"]!["canRebaseline"]!.GetValue<bool>());
        }
        finally { await Flag("schedule_baseline", false); }
    }
}
