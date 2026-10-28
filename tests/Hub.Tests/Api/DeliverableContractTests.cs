using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The deliverable routes answer with typed records that must serialise to the bytes the app already reads (040-S15-Deliverables).
/// Its own host and database (not the shared "api" collection): the shape snapshot's data-dependent reads, such as the following feed's
/// array-valued changes, were seen only when the shared host's tests and their order stayed as they were.
public sealed class DeliverableContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData d = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static string[] Texts(JsonNode? n) => [.. n!.AsArray().Select(x => x!.GetValue<string>())];

    async Task Flag(string name, bool on) => await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on }).Result.Json();
    Task<int> V(JsonNode n) => f.DbAsync(db => db.Deliverables.Where(x => x.Id == n.G("id")).Select(x => x.RowVersion).FirstAsync());

    [Fact]
    public async Task Create_edit_transition_issue_delete_restore_and_bulk_answers_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm); var marc = f.As(TestData.Marc); var diane = f.As(TestData.Diane); var alex = f.As(TestData.Alex);
        var type = await d.DeliverableType();
        var ms = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Contract pin", milestoneType = "Design Submission", date = "2027-03-01" }).Result.Json(201);

        // Create: 201, a Location, the id, key, row version, due date and the warnings (one when the due date is after the milestone).
        var made = await marc.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Pin A", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = type, milestoneId = ms.G("id"), dueDate = "2027-03-10" });
        var a = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion", "dueDate", "warnings"], Keys(a));
        Assert.Equal($"/api/v1/deliverables/{a.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal("2027-03-10", a.S("dueDate")); Assert.Equal(0, a.I("rowVersion"));
        Assert.Single(a["warnings"]!.AsArray());
        var plain = await marc.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Pin B", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = type }).Result.Json(201);
        Assert.True(plain.AsObject().ContainsKey("dueDate")); Assert.Null(plain["dueDate"]); Assert.Empty(plain["warnings"]!.AsArray()); // null, not omitted; [] not omitted

        // Edit: the id, the new row version and the warnings, with the ETag header.
        var edited = await marc.Patch($"/api/v1/deliverables/{a.S("id")}", new { name = "Pin A renamed", priority = "Low" }, await V(a));
        var e = await edited.Json();
        Assert.Equal(["id", "rowVersion", "warnings"], Keys(e));
        Assert.Equal($"\"{e.I("rowVersion")}\"", edited.Headers.ETag!.Tag);
        var late = await marc.Patch($"/api/v1/deliverables/{a.S("id")}", new { milestoneId = ms.G("id"), dueDate = "2027-03-20" }, await V(a)).Result.Json();
        Assert.Equal([Hub.Api.Text.Get("deliverable.after_milestone", a.S("key"), (await f.DbAsync(db => db.Milestones.Where(m => m.Id == ms.G("id")).Select(m => m.Key).SingleAsync())), "2027-03-01")], Texts(late["warnings"]));

        // The discipline move asks first: the 409 problem carries the tasks to move, each as key and name.
        var t1 = await d.NewTask(p.Id, TestData.Pm, new { name = "Pin task", deliverableId = a.G("id") });
        var ask = await pm.Patch($"/api/v1/deliverables/{a.S("id")}", new { projectDisciplineId = d.ProjectDiscipline(p.Id, "Electrical") }, await V(a));
        var asked = await ask.Json(409);
        Assert.Equal("confirm_move_tasks", asked.S("code"));
        var moving = Assert.Single(asked["tasks"]!.AsArray())!;
        Assert.Equal(["key", "name"], Keys(moving)); Assert.Equal(t1.S("key"), moving.S("key"));

        // Transition: the id, the status and the row version.
        var moved = await marc.Post($"/api/v1/deliverables/{a.S("id")}/transition", new { toStatus = "In Progress", rowVersion = await V(a) }).Result.Json();
        Assert.Equal(["id", "status", "rowVersion"], Keys(moved)); Assert.Equal("In Progress", moved.S("status"));
        await marc.Patch($"/api/v1/deliverables/{a.S("id")}", new { reviewerId = d.User(TestData.Diane) }, await V(a)).Result.Json();
        await marc.Post($"/api/v1/deliverables/{a.S("id")}/transition", new { toStatus = "In Review", rowVersion = await V(a) }).Result.Json();
        await diane.Post($"/api/v1/deliverables/{a.S("id")}/transition", new { toStatus = "Ready to Issue", rowVersion = await V(a) }).Result.Json();

        // Issue: the 409 asks about the open task (id, key, name, status); the 200 repeats them as openTasks.
        var confirm = await marc.Post($"/api/v1/deliverables/{a.S("id")}/issue", new { revision = "Rev A", rowVersion = await V(a) }).Result.Json(409);
        Assert.Equal("confirm_open_tasks", confirm.S("code"));
        Assert.Equal(["id", "key", "name", "status"], Keys(Assert.Single(confirm["tasks"]!.AsArray())));
        var issued = await marc.Post($"/api/v1/deliverables/{a.S("id")}/issue", new { revision = "Rev A", issuedTo = "City", confirmOpenTasks = true, rowVersion = await V(a) }).Result.Json();
        Assert.Equal(["id", "status", "rowVersion", "openTasks"], Keys(issued)); Assert.Equal("Issued", issued.S("status"));
        var open = Assert.Single(issued["openTasks"]!.AsArray())!;
        Assert.Equal(["id", "key", "name", "status"], Keys(open)); Assert.Equal((t1.G("id"), t1.S("key"), "Pin task", "Not Started"), (open.G("id"), open.S("key"), open.S("name"), open.S("status")));
        var cleanly = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Pin C", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = type, requiresReview = false }).Result.Json(201);
        var none = await pm.Post($"/api/v1/deliverables/{cleanly.S("id")}/issue", new { rowVersion = await V(cleanly) }).Result.Json();
        Assert.Equal(["id", "status", "rowVersion", "openTasks"], Keys(none)); Assert.Empty(none["openTasks"]!.AsArray()); // [] not omitted

        // Bulk: the count and the skipped rows (id, key, reason); a person who may not edit is skipped, not refused.
        var bulk = $"/api/v1/projects/{p.Id}/deliverables/bulk";
        var set = await pm.Post(bulk, new { ids = new[] { plain.G("id") }, operation = "setOwner", @params = new { ownerId = d.User(TestData.Alex) } }).Result.Json();
        Assert.Equal(["updated", "skipped"], Keys(set)); Assert.Equal(1, set.I("updated")); Assert.Empty(set["skipped"]!.AsArray());
        var skipped = await alex.Post(bulk, new { ids = new[] { cleanly.G("id") }, operation = "setOwner", @params = new { ownerId = d.User(TestData.Alex) } }).Result.Json();
        Assert.Equal(0, skipped.I("updated"));
        var row = Assert.Single(skipped["skipped"]!.AsArray())!;
        Assert.Equal(["id", "key", "reason"], Keys(row)); Assert.Equal(cleanly.S("key"), row.S("key"));

        // Delete answers 204 with no body, and a second delete is 404; Restore answers the id and row version.
        var gone = await pm.DeleteAsync($"/api/v1/deliverables/{plain.S("id")}");
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode); Assert.Empty(await gone.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.DeleteAsync($"/api/v1/deliverables/{plain.S("id")}")).StatusCode);
        await Flag("show_deleted", true);
        try
        {
            var restored = await pm.PostAsync($"/api/v1/deliverables/{plain.S("id")}/restore", null).Result.Json();
            Assert.Equal(["id", "rowVersion"], Keys(restored)); Assert.Equal(plain.G("id"), restored.G("id"));
        }
        finally { await Flag("show_deleted", false); }
    }

    [Fact]
    public async Task Detail_keeps_its_envelope_nested_records_and_the_small_ux_related_tasks()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm); var marc = f.As(TestData.Marc);
        var type = await d.DeliverableType(); var civil = d.ProjectDiscipline(p.Id, "Civil");
        var a = await marc.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Detail A", projectDisciplineId = civil, deliverableTypeId = type, dueDate = "2027-03-10", requiresReview = false }).Result.Json(201);
        var b = await marc.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Detail B", projectDisciplineId = civil, deliverableTypeId = type }).Result.Json(201);
        var ta = await d.NewTask(p.Id, TestData.Pm, new { name = "Detail task A", deliverableId = a.G("id") });
        var tb = await d.NewTask(p.Id, TestData.Pm, new { name = "Detail task B", deliverableId = b.G("id") });
        await pm.Post($"/api/v1/tasks/{tb.S("id")}/dependencies", new { predecessorTaskId = ta.G("id") }).Result.Json(201); // derives B after A
        var link = await pm.Post($"/api/v1/deliverables/{b.S("id")}/dependencies", new { predecessorDeliverableId = a.G("id"), type = "StartToStart", lagDays = 2, note = "Pin note" }).Result.Json(201);
        await marc.Post($"/api/v1/deliverables/{a.S("id")}/issue", new { revision = "Rev A", issuedTo = "City", transmittalUrl = "https://example.test/t/1", note = "Pin issue", confirmOpenTasks = true, rowVersion = await V(a) }).Result.Json();
        await f.Evaluate(p.Id);

        string[] top = ["deliverable", "description", "transmittalUrl", "issueNote", "acceptedDate", "onHoldReason", "cancelledReason", "previousStatus", "project",
            "derivedPredecessors", "derivedSuccessors", "issues", "explicitPredecessors", "explicitSuccessors", "permissions"];
        var detailA = await pm.GetAsync($"/api/v1/deliverables/{a.S("id")}").Result.Json();
        Assert.Equal(top, Keys(detailA)); // relatedTasks is absent while small_ux is off, not null
        Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detailA["project"]));
        Assert.Equal(["ok", "reason"], Keys(detailA["permissions"]!["edit"]));
        Assert.Equal(["edit", "delete", "transitions", "issue", "linkDeliverables", "issueNeedsConfirm", "readyToIssueGuard", "comment", "createTask", "needsReason"], Keys(detailA["permissions"]));
        var transition = detailA["permissions"]!["transitions"]!.AsArray().First()!;
        Assert.Equal(["to", "allowed", "needsReason"], Keys(transition));
        Assert.Equal(["id", "issuedDate", "revision", "issuedTo", "transmittalUrl", "note", "issuedBy"], Keys(Assert.Single(detailA["issues"]!.AsArray())));
        Assert.Equal(["Rev A", "City", "https://example.test/t/1", "Pin issue"],
            [detailA["issues"]![0]!.S("revision"), detailA["issues"]![0]!.S("issuedTo"), detailA["issues"]![0]!.S("transmittalUrl"), detailA["issues"]![0]!.S("note")]);
        Assert.Equal(["id", "key", "name", "status", "dueDate"], Keys(Assert.Single(detailA["derivedSuccessors"]!.AsArray())));
        Assert.Equal(b.G("id"), detailA["derivedSuccessors"]![0]!.G("id"));
        var explicitSuccessor = Assert.Single(detailA["explicitSuccessors"]!.AsArray())!;
        Assert.Equal(["dependencyId", "type", "lagDays", "note", "id", "key", "name", "status", "dueDate", "issuedDate", "projectDisciplineId", "satisfied"], Keys(explicitSuccessor));
        Assert.Equal((link.G("id"), "StartToStart", 2, "Pin note", b.G("id")), (explicitSuccessor.G("dependencyId"), explicitSuccessor.S("type"), explicitSuccessor.I("lagDays"), explicitSuccessor.S("note"), explicitSuccessor.G("id")));
        Assert.Empty(detailA["explicitPredecessors"]!.AsArray());

        var detailB = await pm.GetAsync($"/api/v1/deliverables/{b.S("id")}").Result.Json();
        Assert.Equal(a.G("id"), Assert.Single(detailB["explicitPredecessors"]!.AsArray())!.G("id"));
        Assert.Equal(a.G("id"), Assert.Single(detailB["derivedPredecessors"]!.AsArray())!.G("id"));
        Assert.True(detailB["explicitPredecessors"]![0]!.AsObject().ContainsKey("issuedDate")); Assert.NotNull(detailB["explicitPredecessors"]![0]!["issuedDate"]); // the issued predecessor
        Assert.Empty(detailB["issues"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(detailB["description"], null)); Assert.True(detailB.AsObject().ContainsKey("description")); // null, not omitted

        await Flag("small_ux", true);
        try
        {
            await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Detail from task", projectDisciplineId = civil, deliverableTypeId = type, fromTaskId = ta.G("id") }).Result.Json(201);
            var related = await f.DbAsync(db => db.ItemLinks.Where(l => l.SourceId == ta.G("id")).Select(l => l.TargetId).SingleAsync());
            var on = await pm.GetAsync($"/api/v1/deliverables/{related}").Result.Json();
            Assert.Equal([.. top, "relatedTasks"], Keys(on));
            Assert.Equal(["id", "key", "name", "status"], Keys(Assert.Single(on["relatedTasks"]!.AsArray())));
            Assert.Empty((await pm.GetAsync($"/api/v1/deliverables/{a.S("id")}").Result.Json())["relatedTasks"]!.AsArray()); // [] when nothing is linked, not omitted
        }
        finally { await Flag("small_ux", false); }
    }

    [Fact]
    public async Task Register_rows_keep_their_order_and_omit_the_baseline_fields_while_their_flag_is_off()
    {
        var p = await d.Project();
        var marc = f.As(TestData.Marc);
        var a = await marc.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Row A", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2027-03-10", startDate = "2027-03-01" }).Result.Json(201);
        await d.NewTask(p.Id, TestData.Pm, new { name = "Row task", deliverableId = a.G("id") });
        await f.Evaluate(p.Id);
        string[] row = ["id", "projectId", "key", "name", "projectDisciplineId", "deliverableTypeId", "ownerId", "reviewerId", "milestoneId", "startDate", "dueDate", "originalDueDate", "originalStartDate",
            "priority", "status", "revision", "issuedDate", "issuedTo", "requiresReview", "requiredReviewPackageId", "rowVersion", "createdAt", "lastActivityAt", "disciplineName", "disciplineColour",
            "disciplineOrder", "typeName", "ownerName", "ownerActive", "reviewerName", "milestoneKey", "milestoneName", "milestoneDate", "state", "tasks"];
        var list = await marc.GetAsync($"/api/v1/projects/{p.Id}/deliverables?includeTasks=true").Result.Json();
        Assert.Equal(row, Keys(Assert.Single(list.AsArray())));
        Assert.Equal(["id", "key", "name", "status", "dueDate", "deliverableId", "assignee"], Keys(list[0]!["tasks"]![0]));
        var without = await marc.GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json();
        Assert.Equal(row, Keys(without[0])); // tasks is present and null without includeTasks
        Assert.Null(without[0]!["tasks"]);
        Assert.Equal(["progressPct", "taskTotal", "taskComplete", "taskCancelled", "taskOpen", "taskOverdue", "taskBlocked", "estimatedHoursTotal", "remainingHours", "isOverdue", "daysOverdue",
            "isDueSoon", "isAtRisk", "atRiskReasons", "isUnassigned", "isStale", "isDateInconsistent", "inconsistencyDetail", "slipDays", "isInactiveOwner", "issuedWithOpenWork",
            "milestoneCancelled", "isWaiting", "isBlocked", "blockedBy", "blockingCount", "derivedPredecessorIds", "derivedSuccessorIds"], Keys(list[0]!["state"]));

        await Flag("schedule_baseline", true);
        try
        {
            var on = await marc.GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json();
            Assert.Equal(["slipShown"], Keys(on[0]).Except(row));
        }
        finally { await Flag("schedule_baseline", false); }
    }
}
