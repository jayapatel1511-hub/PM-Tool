using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S12 Undo (FR-SCR-20, AC-SCR-20, 053-D9): the reverse of a bulk command's changed rows for 10 seconds, through the same
/// validation and apply steps, as new activity entries marked "Undo of"; a row changed since is refused with the reason.
public sealed class TaskBulkUndoTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public TaskBulkUndoTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    const string Command = "/api/v1/tasks/bulk/commands";
    async Task Flag() => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value = true })).Json();
    object Item(Guid project, JsonNode task) => new { taskId = task.G("id"), projectId = project, rowVersion = f.Db(db => db.Tasks.Single(t => t.Id == task.G("id")).RowVersion) };
    async Task<JsonNode> Run(string actor, object[] items, string operation, object parameters, string? reason = "Synthetic bulk correction") =>
        await (await f.As(actor).Post(Command, new { requestId = Guid.NewGuid(), intent = new { schemaVersion = 1, items, operation, @params = parameters, reason } })).Json();
    Task<HttpResponseMessage> Undo(string actor, JsonNode result) => f.As(actor).Post($"{Command}/{result.S("commandId")}/undo", new { });

    [Fact]
    public async Task Cross_project_reassignment_changes_permitted_rows_and_lists_refused_ones_with_reasons() // AC-SCR-19
    {
        await Flag(); var alex = d.User(TestData.Alex);
        var items = new List<object>(); var civil = new List<Guid>();
        foreach (var n in new[] { 3, 3, 2 })
        {
            var p = await d.Project();
            for (var i = 0; i < 2; i++) { var task = await d.NewTask(p.Id); items.Add(Item(p.Id, task)); civil.Add(task.G("id")); }
            if (n == 3) items.Add(Item(p.Id, await d.NewTask(p.Id, as_: TestData.Pm, discipline: "Electrical")));
        }
        Assert.Equal(8, items.Count);
        // Marc leads Civil on each project; he may not reassign the two Electrical tasks.
        var result = await Run(TestData.Marc, [.. items], "assign", new { kind = "assign", assigneeId = alex });
        Assert.Equal(6, result["counts"]!.I("changed")); Assert.Equal(2, result["counts"]!.I("refused"));
        Assert.All(result["rows"]!.AsArray().Where(row => row!.S("outcome") == "refused"), row => Assert.False(string.IsNullOrWhiteSpace(row!.S("reason"))));
        Assert.All(civil, id => Assert.Equal(alex, f.Db(db => db.Tasks.Single(x => x.Id == id).AssigneeId)));
        var entries = f.Db(db => db.ActivityLog.Where(a => a.CorrelationId == result.G("commandId") && a.ItemType == ItemType.Task).Select(a => a.ItemId).Distinct().Count());
        Assert.Equal(6, entries);
    }

    [Fact]
    public async Task Reassignment_undo_restores_each_previous_assignee_and_refuses_the_row_changed_since() // AC-SCR-20
    {
        await Flag(); var p = await d.Project(); var alex = d.User(TestData.Alex); var marc = d.User(TestData.Marc);
        var tasks = new List<JsonNode>();
        for (var i = 0; i < 4; i++) tasks.Add(await d.NewTask(p.Id, extra: new { assigneeId = alex }));
        for (var i = 0; i < 2; i++) tasks.Add(await d.NewTask(p.Id));
        var before = tasks.Select(t => f.Db(db => db.Tasks.Single(x => x.Id == t.G("id")).AssigneeId)).ToArray();
        var result = await Run(TestData.Pm, [.. tasks.Select(t => Item(p.Id, t))], "assign", new { kind = "assign", assigneeId = marc });
        Assert.Equal(6, result["counts"]!.I("changed"));

        // One task changes in between (G-07): its Undo row is refused with the reason, the other five are undone.
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{tasks[5].G("id")}", new { priority = "High" }, await d.TaskVersion(tasks[5]))).Json();
        f.Clock.Now = f.Clock.Now.AddSeconds(9);
        var undo = await (await Undo(TestData.Pm, result)).Json();
        Assert.Equal(5, undo["counts"]!.I("changed")); Assert.Equal(1, undo["counts"]!.I("refused"));
        var refused = Assert.Single(undo["rows"]!.AsArray(), row => row!.S("outcome") == "refused")!;
        Assert.Equal(tasks[5].G("id"), refused.G("taskId")); Assert.False(string.IsNullOrWhiteSpace(refused.S("reason")));
        for (var i = 0; i < 5; i++) Assert.Equal(before[i], f.Db(db => db.Tasks.Single(x => x.Id == tasks[i].G("id")).AssigneeId));
        Assert.Equal(marc, f.Db(db => db.Tasks.Single(x => x.Id == tasks[5].G("id")).AssigneeId));

        var entries = f.Db(db => db.ActivityLog.Where(a => a.CorrelationId == undo.G("commandId") && a.ItemType == ItemType.Task).Select(a => new { a.ItemId, a.Reason }).ToList());
        Assert.Equal(5, entries.Select(e => e.ItemId).Distinct().Count());
        Assert.All(entries, e => Assert.StartsWith("Undo of:", e.Reason));
        Assert.True(f.Db(db => db.ActivityLog.Count(a => a.CorrelationId == result.G("commandId"))) > 0, "The original entries stay");

        // A retry returns the same Undo; nothing is reversed twice and nothing new is written.
        var count = f.Db(db => db.ActivityLog.Count());
        var again = await (await Undo(TestData.Pm, result)).Json();
        Assert.Equal(undo.S("commandId"), again.S("commandId")); Assert.Equal(count, f.Db(db => db.ActivityLog.Count()));
        // An Undo cannot itself be undone, and another person cannot undo Pat's change.
        Assert.Equal(HttpStatusCode.NotFound, (await Undo(TestData.Pm, undo)).StatusCode);
        var other = await Run(TestData.Pm, [Item(p.Id, tasks[0])], "setPriority", new { kind = "setPriority", priority = "Low" });
        Assert.Equal(HttpStatusCode.NotFound, (await Undo(TestData.Marc, other)).StatusCode);
    }

    [Fact]
    public async Task Undo_closes_after_the_window_and_reverses_shift_and_hold_through_the_same_rules()
    {
        await Flag(); var p = await d.Project();
        var dated = await d.NewTask(p.Id, extra: new { dueDate = "2026-10-01" });
        var held = await d.NewTask(p.Id);
        await d.Move(TestData.Marc, held, "In Progress");
        var changes = f.Db(db => db.Tasks.Single(t => t.Id == dated.G("id")).DueDateChangeCount);

        var shift = await Run(TestData.Pm, [Item(p.Id, dated)], "shiftDueDates", new { kind = "shiftDueDates", days = 7 });
        Assert.Equal(new DateOnly(2026, 10, 8), f.Db(db => db.Tasks.Single(t => t.Id == dated.G("id")).DueDate));
        await (await Undo(TestData.Pm, shift)).Json();
        var restored = f.Db(db => db.Tasks.Single(t => t.Id == dated.G("id")));
        Assert.Equal(new DateOnly(2026, 10, 1), restored.DueDate); Assert.Equal(changes, restored.DueDateChangeCount);

        var hold = await Run(TestData.Pm, [Item(p.Id, held)], "hold", new { kind = "hold" });
        Assert.Equal(TaskStatuses.OnHold, f.Db(db => db.Tasks.Single(t => t.Id == held.G("id")).Status));
        await (await Undo(TestData.Pm, hold)).Json();
        Assert.Equal(TaskStatuses.InProgress, f.Db(db => db.Tasks.Single(t => t.Id == held.G("id")).Status));

        var late = await Run(TestData.Pm, [Item(p.Id, held)], "setPriority", new { kind = "setPriority", priority = "Critical" });
        f.Clock.Now = f.Clock.Now.AddSeconds(16);
        var expired = await Undo(TestData.Pm, late);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, expired.StatusCode);
        Assert.Equal("Critical", f.Db(db => db.Tasks.AsNoTracking().Single(t => t.Id == held.G("id")).Priority));
    }
}
