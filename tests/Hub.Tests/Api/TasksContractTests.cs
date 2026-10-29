using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The task routes answer with typed records that must serialise to the bytes the app already reads (040-S15-Tasks).
/// Its own host and database (not the shared "api" collection), like the other contract tests, so the shape snapshot's
/// data-dependent reads are recorded the same way in a full run.
public sealed class TasksContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData d = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static string[] Texts(JsonNode? n) => [.. n!.AsArray().Select(x => x!.GetValue<string>())];
    Task<int> V(JsonNode n) => d.TaskVersion(n);

    [Fact]
    public async Task Create_edit_move_block_delete_and_restore_answers_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var (pm, marc, alex, rita) = (f.As(TestData.Pm), f.As(TestData.Marc), f.As(TestData.Alex), f.As(TestData.Rita));
        var deliverable = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "85% package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-10-01",
        }).Result.Json(201);

        // Create: 201 with a Location, the id, the key, the version and the warnings (one when the due date is after the deliverable's).
        var made = await marc.Post($"/api/v1/projects/{p.Id}/tasks", new
        {
            name = "Pin A", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableId = deliverable.G("id"), assigneeId = d.User(TestData.Alex), dueDate = "2026-10-20",
        });
        var a = await made.Json(201);
        Assert.Equal(["id", "key", "rowVersion", "warnings"], Keys(a));
        Assert.Equal($"/api/v1/tasks/{a.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal(await V(a), a.I("rowVersion"));
        Assert.Equal([Hub.Api.Text.Get("task.after_deliverable", a.S("key"), deliverable.S("key"), "2026-10-01")], Texts(a["warnings"]));
        var plain = await marc.Post($"/api/v1/projects/{p.Id}/tasks", new { name = "Pin B", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }).Result.Json(201);
        Assert.Equal(["id", "key", "rowVersion", "warnings"], Keys(plain));
        Assert.Empty(plain["warnings"]!.AsArray()); // [] not omitted
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/projects/{p.Id}/tasks", new { name = "No", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/projects/{p.Id}/tasks", new { name = "", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marc.Post($"/api/v1/projects/{Guid.NewGuid()}/tasks", new { name = "Gone" })).StatusCode);

        // Edit: the id, the new version, the due-date change count and the warnings, with the ETag header the page reads.
        var v0 = await V(a);
        var edit = await marc.Patch($"/api/v1/tasks/{a.S("id")}", new { name = "Pin A renamed", priority = "High" }, v0);
        var edited = await edit.Json();
        Assert.Equal(["id", "rowVersion", "dueDateChangeCount", "warnings"], Keys(edited));
        Assert.Equal((a.G("id"), v0 + 1, 0), (edited.G("id"), edited.I("rowVersion"), edited.I("dueDateChangeCount")));
        Assert.Equal($"\"{v0 + 1}\"", edit.Headers.ETag!.Tag);
        var moved = await marc.Patch($"/api/v1/tasks/{a.S("id")}", new { dueDate = "2026-10-25", reason = "Review slipped" }, await V(a)).Result.Json();
        Assert.Equal(1, moved.I("dueDateChangeCount"));
        Assert.Equal([Hub.Api.Text.Get("task.after_deliverable", a.S("key"), "", "2026-10-01")], Texts(moved["warnings"]));
        Assert.Empty((await marc.Patch($"/api/v1/tasks/{plain.S("id")}", new { name = "Pin B renamed" }, await V(plain)).Result.Json())["warnings"]!.AsArray());
        Assert.Equal(428, (int)(await marc.Patch($"/api/v1/tasks/{a.S("id")}", new { name = "No version" })).StatusCode);
        Assert.Equal("concurrency_conflict", (await marc.Patch($"/api/v1/tasks/{a.S("id")}", new { name = "Stale" }, v0).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Patch($"/api/v1/tasks/{a.S("id")}", new { name = "No" }, await V(a))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Patch($"/api/v1/tasks/{a.S("id")}", new { progressPct = 15 }, await V(a))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marc.Patch($"/api/v1/tasks/{Guid.NewGuid()}", new { name = "Gone" }, 1)).StatusCode);

        // Transition: the id, the status, the version, the progress, the review round and the warnings.
        var started = await d.Move(TestData.Marc, a, TaskStatuses.InProgress);
        Assert.Equal(["id", "status", "rowVersion", "progressPct", "reviewRound", "warnings"], Keys(started));
        Assert.Equal((a.G("id"), TaskStatuses.InProgress, await V(a), 0, 0), (started.G("id"), started.S("status"), started.I("rowVersion"), started.I("progressPct"), started.I("reviewRound")));
        Assert.Empty(started["warnings"]!.AsArray());
        var done = await d.Move(TestData.Marc, a, TaskStatuses.Complete);
        Assert.Equal((TaskStatuses.Complete, 100), (done.S("status"), done.I("progressPct")));
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/tasks/{a.S("id")}/transition", new { toStatus = "Nope", rowVersion = await V(a) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/tasks/{plain.S("id")}/transition", new { toStatus = TaskStatuses.InProgress, rowVersion = await V(plain) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marc.Post($"/api/v1/tasks/{Guid.NewGuid()}/transition", new { toStatus = TaskStatuses.InProgress })).StatusCode);

        // Block and unblock: the id and the new version, both ways, and the refusals (unblock was never called before).
        var blocked = await marc.Post($"/api/v1/tasks/{plain.S("id")}/block", new { type = "Client", reason = "Survey data is late", rowVersion = await V(plain) }).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(blocked));
        Assert.Equal((plain.G("id"), await V(plain)), (blocked.G("id"), blocked.I("rowVersion")));
        var row = (await pm.GetAsync($"/api/v1/tasks/{plain.S("id")}").Result.Json())["task"]!;
        Assert.Equal(("Client", "Survey data is late"), (row.S("manualBlockType"), row.S("manualBlockReason")));
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/tasks/{plain.S("id")}/block", new { type = "Nope", reason = "x", rowVersion = await V(plain) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/tasks/{plain.S("id")}/block", new { type = "Client", reason = "x", rowVersion = await V(plain) })).StatusCode);
        Assert.Equal(428, (int)(await marc.PostAsync($"/api/v1/tasks/{plain.S("id")}/unblock", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Action($"/api/v1/tasks/{plain.S("id")}/unblock", new { }, await V(plain))).StatusCode);
        Assert.Equal("concurrency_conflict", (await marc.Action($"/api/v1/tasks/{plain.S("id")}/unblock", new { }, blocked.I("rowVersion") - 1).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.NotFound, (await marc.Action($"/api/v1/tasks/{Guid.NewGuid()}/unblock", new { }, 1)).StatusCode);
        var cleared = await marc.Action($"/api/v1/tasks/{plain.S("id")}/unblock", new { }, await V(plain)).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(cleared));
        Assert.Equal((plain.G("id"), await V(plain)), (cleared.G("id"), cleared.I("rowVersion")));
        row = (await pm.GetAsync($"/api/v1/tasks/{plain.S("id")}").Result.Json())["task"]!;
        Assert.Null(row["manualBlockType"]); Assert.Null(row["manualBlockReason"]);

        // Delete and restore: the count of removed links, then the id and the version.
        var removed = await pm.DeleteAsync($"/api/v1/tasks/{plain.S("id")}").Result.Json();
        Assert.Equal(["removedDependencies"], Keys(removed));
        Assert.Equal(0, removed.I("removedDependencies"));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.DeleteAsync($"/api/v1/tasks/{plain.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.PostAsync($"/api/v1/tasks/{plain.S("id")}/restore", null)).StatusCode);
        var restored = await pm.PostAsync($"/api/v1/tasks/{plain.S("id")}/restore", null).Result.Json();
        Assert.Equal(["id", "rowVersion"], Keys(restored));
        Assert.Equal((plain.G("id"), await V(plain)), (restored.G("id"), restored.I("rowVersion")));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.DeleteAsync($"/api/v1/tasks/{plain.S("id")}")).StatusCode);
    }

    [Fact]
    public async Task Collaborator_and_watcher_commands_were_uncalled_and_keep_their_answers() // DELETE tasks/{id}/collaborators/{userId}, POST items/{type}/{id}/watchers
    {
        var p = await d.Project();
        var (pm, marc, alex, jill, rita) = (f.As(TestData.Pm), f.As(TestData.Marc), f.As(TestData.Alex), f.As(TestData.Jill), f.As(TestData.Rita));
        var t = await d.NewTask(p.Id, TestData.Marc, new { assigneeId = d.User(TestData.Alex) });
        var (id, jillId, dianeId) = (t.S("id"), d.User(TestData.Jill), d.User(TestData.Diane));
        Task<int> Collaborators() => f.DbAsync(db => db.Collaborators.CountAsync(c => c.TaskId == t.G("id")));

        // Collaborators: 204 with no body, adding twice adds once, removing something that is not there is still 204.
        var added = await marc.Post($"/api/v1/tasks/{id}/collaborators", new { userId = jillId });
        Assert.Equal((HttpStatusCode.NoContent, ""), (added.StatusCode, await added.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NoContent, (await marc.Post($"/api/v1/tasks/{id}/collaborators", new { userId = jillId })).StatusCode);
        Assert.Equal(1, await Collaborators());
        Assert.Equal([jillId], (await pm.GetAsync($"/api/v1/tasks/{id}").Result.Json())["collaborators"]!.AsArray().Select(x => x!.G("id")));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post($"/api/v1/tasks/{id}/collaborators", new { userId = dianeId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.DeleteAsync($"/api/v1/tasks/{id}/collaborators/{jillId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await marc.DeleteAsync($"/api/v1/tasks/{Guid.NewGuid()}/collaborators/{jillId}")).StatusCode);
        Assert.Equal(1, await Collaborators());
        var removed = await marc.DeleteAsync($"/api/v1/tasks/{id}/collaborators/{jillId}");
        Assert.Equal((HttpStatusCode.NoContent, ""), (removed.StatusCode, await removed.Content.ReadAsStringAsync()));
        Assert.Equal(0, await Collaborators());
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == t.G("id") && a.Action == "CollaboratorRemoved")));
        Assert.Equal(HttpStatusCode.NoContent, (await marc.DeleteAsync($"/api/v1/tasks/{id}/collaborators/{jillId}")).StatusCode);
        (await marc.Post($"/api/v1/tasks/{id}/collaborators", new { userId = jillId })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await jill.DeleteAsync($"/api/v1/tasks/{id}/collaborators/{jillId}")).StatusCode); // one may leave a task
        Assert.Equal(0, await Collaborators());

        // Watchers: the read lists user, name and source; adding is 204 with no body (one's own watch, or a commenter adding another).
        var url = $"/api/v1/items/Task/{id}/watchers";
        var watch = await alex.Post(url, new { userId = d.User(TestData.Alex) });
        Assert.Equal((HttpStatusCode.NoContent, ""), (watch.StatusCode, await watch.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.NoContent, (await alex.Post(url, new { userId = d.User(TestData.Alex) })).StatusCode); // twice adds once
        Assert.Equal(HttpStatusCode.NoContent, (await pm.Post(url, new { userId = dianeId })).StatusCode);
        var list = (await pm.GetAsync(url).Result.Json()).AsArray();
        Assert.All(list, x => Assert.Equal(["userId", "displayName", "source"], Keys(x)));
        Assert.Equal(["Manual"], list.Select(x => x!.S("source")).Distinct());
        Assert.Contains(list, x => x!.G("userId") == d.User(TestData.Alex) && x.S("displayName") == "Alex Chen");
        Assert.Equal(1, list.Count(x => x!.G("userId") == d.User(TestData.Alex)));
        Assert.Equal(HttpStatusCode.Forbidden, (await rita.Post(url, new { userId = dianeId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/items/Task/{Guid.NewGuid()}/watchers", new { userId = dianeId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/items/Nope/{id}/watchers", new { userId = dianeId })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/items/Task/{Guid.NewGuid()}/watchers")).StatusCode);
        var gone = await pm.DeleteAsync($"{url}/{dianeId}");
        Assert.Equal((HttpStatusCode.NoContent, ""), (gone.StatusCode, await gone.Content.ReadAsStringAsync()));
        Assert.DoesNotContain((await pm.GetAsync(url).Result.Json()).AsArray(), x => x!.G("userId") == dianeId);
        var other = await d.Project(pm: TestData.Marc, tweak: body => body["members"] = Array.Empty<object>());
        var elsewhere = await d.NewTask(other.Id, TestData.Marc);
        Assert.Equal(HttpStatusCode.NotFound, (await marc.Post($"/api/v1/items/Task/{elsewhere.S("id")}/watchers", new { userId = Guid.NewGuid() })).StatusCode); // a person who may not see the project
    }

    [Fact]
    public async Task Delete_preview_and_bulk_answers_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var (pm, marc, alex) = (f.As(TestData.Pm), f.As(TestData.Marc), f.As(TestData.Alex));
        var a = await d.NewTask(p.Id, TestData.Pm, new { name = "Pin before" });
        var b = await d.NewTask(p.Id, TestData.Pm, new { name = "Pin after", assigneeId = d.User(TestData.Alex), dependsOn = new[] { a.G("id") } });

        // Delete preview: the links with their other end (a blocks b, b depends on a), the suggestion to cancel and the two permissions.
        var preview = await pm.GetAsync($"/api/v1/tasks/{a.S("id")}/delete-preview").Result.Json();
        Assert.Equal(["dependencies", "suggestCancel", "canDelete", "canCancel"], Keys(preview));
        var link = Assert.Single(preview["dependencies"]!.AsArray())!;
        Assert.Equal(["id", "predecessorTaskId", "successorTaskId", "key", "name", "direction", "assigneeId"], Keys(link));
        Assert.Equal((a.G("id"), b.G("id"), b.S("key"), "Pin after", "blocks", d.User(TestData.Alex)), (link.G("predecessorTaskId"), link.G("successorTaskId"), link.S("key"), link.S("name"), link.S("direction"), link.G("assigneeId")));
        Assert.Equal((false, true, true), (preview["suggestCancel"]!.GetValue<bool>(), preview["canDelete"]!.GetValue<bool>(), preview["canCancel"]!.GetValue<bool>()));
        var back = Assert.Single((await pm.GetAsync($"/api/v1/tasks/{b.S("id")}/delete-preview").Result.Json())["dependencies"]!.AsArray())!;
        Assert.Equal(("dependsOn", null), (back.S("direction"), (string?)back["assigneeId"]));
        var empty = await pm.GetAsync($"/api/v1/tasks/{(await d.NewTask(p.Id, TestData.Pm)).S("id")}/delete-preview").Result.Json();
        Assert.True(empty.AsObject().ContainsKey("dependencies")); Assert.Empty(empty["dependencies"]!.AsArray());
        var worked = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        await d.Move(TestData.Alex, worked, TaskStatuses.InProgress);
        (await pm.Patch($"/api/v1/tasks/{worked.S("id")}", new { progressPct = 30 }, await V(worked))).EnsureSuccessStatusCode();
        Assert.True((await pm.GetAsync($"/api/v1/tasks/{worked.S("id")}/delete-preview").Result.Json())["suggestCancel"]!.GetValue<bool>()); // work done: suggest cancelling
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}/delete-preview")).StatusCode);
        var viewer = await f.As(TestData.Rita).GetAsync($"/api/v1/tasks/{a.S("id")}/delete-preview").Result.Json();
        Assert.False(viewer["canDelete"]!.GetValue<bool>()); Assert.False(viewer["canCancel"]!.GetValue<bool>());

        // Bulk: the count updated, the tasks skipped with their id, key and reason, and the warnings.
        var c = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        var done = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        await d.Move(TestData.Alex, done, TaskStatuses.InProgress); await d.Move(TestData.Alex, done, TaskStatuses.Complete);
        var result = await pm.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { c.G("id"), done.G("id") }, operation = "hold", reason = "Waiting for survey data" }).Result.Json();
        Assert.Equal(["updated", "skipped", "warnings"], Keys(result));
        Assert.Equal(1, result.I("updated"));
        var skipped = Assert.Single(result["skipped"]!.AsArray())!;
        Assert.Equal(["id", "key", "reason"], Keys(skipped));
        Assert.Equal((done.G("id"), done.S("key")), (skipped.G("id"), skipped.S("key")));
        Assert.False(string.IsNullOrEmpty(skipped.S("reason")));
        Assert.Empty(result["warnings"]!.AsArray());
        var shifted = await pm.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { c.G("id") }, operation = "setPriority", @params = new { priority = "High" } }).Result.Json();
        Assert.Equal((1, 0), (shifted.I("updated"), shifted["skipped"]!.AsArray().Count));
        Assert.Equal("High", await f.DbAsync(db => db.Tasks.Where(x => x.Id == c.G("id")).Select(x => x.Priority).SingleAsync()));
        var refused = await alex.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { b.G("id") }, operation = "assign", @params = new { assigneeId = d.User(TestData.Jill) } }).Result.Json();
        Assert.Equal((0, 1), (refused.I("updated"), refused["skipped"]!.AsArray().Count));
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { c.G("id") }, operation = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = new[] { c.G("id") }, operation = "hold" })).StatusCode); // a reason is required
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/projects/{Guid.NewGuid()}/tasks/bulk", new { taskIds = new[] { c.G("id") }, operation = "setPriority" })).StatusCode);
    }

    [Fact]
    public async Task List_and_detail_keep_their_property_names_and_order()
    {
        var p = await d.Project();
        var (pm, marc) = (f.As(TestData.Pm), f.As(TestData.Marc));
        var t = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-30" });
        (await marc.Post($"/api/v1/tasks/{t.S("id")}/collaborators", new { userId = d.User(TestData.Jill) })).EnsureSuccessStatusCode();

        // The page: items, the page number and size, the total and the sort, then the unused filters, written as null.
        foreach (var url in new[] { $"/api/v1/projects/{p.Id}/tasks", $"/api/v1/tasks?projects={p.Id}&sort=dueDate:desc" })
        {
            var page = await pm.GetAsync(url).Result.Json();
            Assert.Equal(["items", "page", "pageSize", "totalCount", "sort", "filters"], Keys(page));
            Assert.Equal((1, 200, 1), (page.I("page"), page.I("pageSize"), page.I("totalCount")));
            Assert.Null(page["filters"]);
            var row = Assert.Single(page["items"]!.AsArray())!;
            Assert.Equal(t.G("id"), row.G("id"));
            Assert.Equal(["id", "projectId", "projectNumber", "projectName", "projectStatus", "key", "name", "projectDisciplineId", "disciplineName", "disciplineCode", "disciplineColour",
                "disciplineOrder", "deliverableId", "deliverableKey", "deliverableName", "deliverableDueDate", "deliverableStatus", "deliverableOrder", "milestoneId", "effectiveMilestoneId",
                "milestoneKey", "milestoneName", "milestoneDate", "milestoneDerived", "assigneeId", "assigneeName", "assigneeActive", "reviewerId", "reviewerName", "requiresReview",
                "priority", "startDate", "dueDate", "originalStartDate", "originalDueDate", "status", "lane", "progressPct", "estimatedHours", "reviewRound", "dueDateChangeCount",
                "lastActivityAt", "statusChangedAt", "completedAt", "createdAt", "createdBy", "createdByName", "sortOrder", "rowVersion", "manualBlockType", "manualBlockReason",
                "manualBlockSetAt", "commentCount", "predecessorCount", "successorCount", "collaboratorIds", "can", "state"], Keys(row));
            Assert.Equal(["assign", "edit", "changeDue", "block"], Keys(row["can"]));
        }
        Assert.Equal("dueDate:desc", (await pm.GetAsync($"/api/v1/tasks?projects={p.Id}&sort=dueDate:desc").Result.Json()).S("sort"));

        // The detail, by id and by key: the row, the free text, the reasons, the project, the people and every permission.
        foreach (var id in new[] { t.S("id"), t.S("key") })
        {
            var detail = await pm.GetAsync($"/api/v1/tasks/{id}").Result.Json();
            Assert.Equal(["task", "description", "onHoldReason", "cancelledReason", "previousStatus", "reviewRequestedAt", "project", "collaborators", "watchers", "permissions"], Keys(detail));
            Assert.Equal(["id", "projectNumber", "name", "status"], Keys(detail["project"]));
            Assert.All(detail["collaborators"]!.AsArray(), x => Assert.Equal(["id", "displayName", "isActive"], Keys(x)));
            Assert.Equal(["edit", "assign", "setReviewer", "dueNeedsReason", "dueDate", "block", "delete", "restore", "comment", "transitions", "completeHint", "isReviewer", "dependencies",
                "enterTime", "needsReason", "allowSelfReview", "authoriseStart"], Keys(detail["permissions"]));
            Assert.Equal(["ok", "reason"], Keys(detail["permissions"]!["edit"]));
            Assert.All(detail["permissions"]!["transitions"]!.AsArray(), x => Assert.Equal(["to", "allowed", "reason", "needsReason", "via"], Keys(x)));
            Assert.Equal(t.G("id"), detail["task"]!.G("id"));
        }
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/tasks/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync("/api/v1/tasks/NO-SUCH-KEY")).StatusCode);
        var viewer = await f.As(TestData.Rita).GetAsync($"/api/v1/tasks/{t.S("id")}").Result.Json();
        Assert.False(viewer["permissions"]!["edit"]!["ok"]!.GetValue<bool>()); Assert.False(string.IsNullOrEmpty(viewer["permissions"]!["edit"]!.S("reason")));
    }
}
