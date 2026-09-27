using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 018: timeline data (FR-001, FR-002, FR-004), who may drag what (FR-003, §8.5.2), a dragged date changing only
/// that item (§12.16, SC-002) and the milestone cascade reaching tasks (FR-005, M-04).
[Collection("api")]
public sealed class TimelineTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<JsonNode> Deliverable(Guid projectId, object extra)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Drawing " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, "Civil"), ["deliverableTypeId"] = await d.DeliverableType() };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    Task<JsonNode> Timeline(Guid projectId, string as_ = TestData.Pm) => f.As(as_).GetAsync($"/api/v1/projects/{projectId}/timeline").Result.Json();

    [Fact]
    public async Task Tasks_arrows_and_permissions_for_the_timeline() // FR-001, FR-002, FR-003, US2 scenario 3
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var a = await Deliverable(p.Id, new { dueDate = "2026-09-30" });
        var b = await Deliverable(p.Id, new { dueDate = "2026-10-20" });
        var late = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = a.G("id"), assigneeId = U(TestData.Alex), startDate = "2026-09-01", dueDate = "2026-09-10" });
        var next = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = b.G("id"), assigneeId = U(TestData.Alex), startDate = "2026-09-21", dueDate = "2026-10-02" });
        var calm = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = b.G("id"), startDate = "2026-10-05", dueDate = "2026-10-09" });
        await pm.Post($"/api/v1/tasks/{next.S("id")}/dependencies", new { predecessorTaskId = late.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{calm.S("id")}/dependencies", new { predecessorTaskId = next.G("id") }).Result.Json(201);
        await f.Evaluate(p.Id);

        var tl = await Timeline(p.Id);
        var tasks = tl["tasks"]!.AsArray();
        Assert.Equal(3, tasks.Count);
        var lateRow = tasks.Single(x => x!.G("id") == late.G("id"))!;
        Assert.Equal(("2026-09-01", "2026-09-10", a.S("id")), (lateRow.S("startDate"), lateRow.S("dueDate"), lateRow.S("deliverableId")));
        Assert.True(lateRow["isOverdue"]!.GetValue<bool>());
        var deps = tl["dependencies"]!.AsArray();
        Assert.True(deps.Single(x => x!.G("predecessorTaskId") == late.G("id"))!["highlighted"]!.GetValue<bool>()); // unfinished and overdue
        Assert.False(deps.Single(x => x!.G("predecessorTaskId") == next.G("id"))!["highlighted"]!.GetValue<bool>());
        var link = tl["deliverableLinks"]!.AsArray().Single()!; // derived: a task in B follows a task in A
        Assert.Equal((a.S("id"), b.S("id"), true, true), (link.S("from"), link.S("to"), link["derived"]!.GetValue<bool>(), link["highlighted"]!.GetValue<bool>()));

        Assert.True(tl["permissions"]!["manageMilestones"]!.GetValue<bool>());
        Assert.All(tasks, x => Assert.True(x!["canEditDates"]!.GetValue<bool>()));
        var alex = await Timeline(p.Id, TestData.Alex);
        var alexRow = alex["tasks"]!.AsArray().Single(x => x!.G("id") == late.G("id"))!;
        Assert.True(alexRow["canEditDates"]!.GetValue<bool>()); // the assignee, with a reason (T-16)
        Assert.True(alexRow["dueNeedsReason"]!.GetValue<bool>());
        Assert.False(alex["tasks"]!.AsArray().Single(x => x!.G("id") == calm.G("id"))!["canEditDates"]!.GetValue<bool>());
        Assert.False(alex["permissions"]!["manageMilestones"]!.GetValue<bool>());
        var jill = await Timeline(p.Id, TestData.Jill);
        Assert.All(jill["tasks"]!.AsArray(), x => Assert.False(x!["canEditDates"]!.GetValue<bool>()));
        Assert.All(jill["deliverables"]!.AsArray(), x => Assert.False(x!["canEditDates"]!.GetValue<bool>()));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Patch($"/api/v1/tasks/{calm.S("id")}", new { dueDate = "2026-10-12" }, await d.TaskVersion(calm))).StatusCode);
    }

    [Fact]
    public async Task A_dragged_task_changes_only_its_own_dates_and_keeps_a_baseline() // §12.16 US2 scenario 1, FR-004, SC-002
    {
        var p = await d.Project();
        var first = await d.NewTask(p.Id, TestData.Pm, new { startDate = "2026-09-21", dueDate = "2026-09-25" });
        var then = await d.NewTask(p.Id, TestData.Pm, new { startDate = "2026-09-28", dueDate = "2026-10-02" });
        await f.As(TestData.Pm).Post($"/api/v1/tasks/{then.S("id")}/dependencies", new { predecessorTaskId = first.G("id") }).Result.Json(201);
        // What the confirmation saves after a 3-day drag.
        (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{first.S("id")}", new { startDate = "2026-09-24", dueDate = "2026-09-28" }, await d.TaskVersion(first))).EnsureSuccessStatusCode();
        var rows = (await Timeline(p.Id))["tasks"]!.AsArray();
        var moved = rows.Single(x => x!.G("id") == first.G("id"))!;
        Assert.Equal(("2026-09-24", "2026-09-28"), (moved.S("startDate"), moved.S("dueDate")));
        Assert.Equal(("2026-09-21", "2026-09-25"), (moved.S("originalStartDate"), moved.S("originalDueDate"))); // the ghost bar
        var succ = rows.Single(x => x!.G("id") == then.G("id"))!;
        Assert.Equal(("2026-09-28", "2026-10-02"), (succ.S("startDate"), succ.S("dueDate"))); // not moved
        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == first.G("id") || a.ItemId == then.G("id")).ToListAsync()); // jsonb: filtered here
        Assert.Contains(log, x => x.ItemId == first.G("id") && x.Changes.Contains("DueDate") && x.Changes.Contains("2026-09-28"));
        Assert.DoesNotContain(log, x => x.ItemId == then.G("id") && x.Action != "Created" && x.Changes.Contains("DueDate"));

        var del = await Deliverable(p.Id, new { dueDate = "2026-10-09" });
        var dv = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.RowVersion).FirstAsync());
        (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{del.S("id")}", new { dueDate = "2026-10-19" }, dv)).EnsureSuccessStatusCode(); // US3: moved by 10 days
        var list = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json();
        var row = list.AsArray().Single(x => x!.G("id") == del.G("id"))!;
        Assert.Equal(("2026-10-19", "2026-10-09"), (row.S("dueDate"), row.S("originalDueDate")));
    }

    [Fact]
    public async Task The_milestone_cascade_can_include_tasks() // FR-005, M-04, US4
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var m = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-11-02" }).Result.Json(201);
        var del = await Deliverable(p.Id, new { milestoneId = m.G("id"), startDate = "2026-10-01", dueDate = "2026-10-30" });
        var t1 = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id"), startDate = "2026-10-05", dueDate = "2026-10-16" });
        var t2 = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id"), dueDate = "2026-10-28" });
        var other = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-28" });
        var preview = await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-11-16", cascadeDeliverables = true, includeTasks = true, dryRun = true }).Result.Json();
        Assert.Equal(2, preview["tasks"]!.AsArray().Count);
        await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-11-16", reason = "Client extension", cascadeDeliverables = true, includeTasks = true, rowVersion = m.I("rowVersion") }).Result.Json();
        var (dl, a, b, o, logs) = await f.DbAsync(async db => (
            await db.Deliverables.FirstAsync(x => x.Id == del.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == t1.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == t2.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == other.G("id")),
            await db.ActivityLog.Where(x => x.Action == "Cascade" && (x.ItemId == t1.G("id") || x.ItemId == t2.G("id") || x.ItemId == del.G("id"))).CountAsync()));
        Assert.Equal((new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 13)), (dl.StartDate!.Value, dl.DueDate!.Value)); // +14 days
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30)), (a.StartDate!.Value, a.DueDate!.Value));
        Assert.Equal(new DateOnly(2026, 11, 11), b.DueDate);
        Assert.Equal(new DateOnly(2026, 10, 28), o.DueDate); // not in the cascade, not moved
        Assert.Equal(3, logs); // one log row per shifted item
    }
}
