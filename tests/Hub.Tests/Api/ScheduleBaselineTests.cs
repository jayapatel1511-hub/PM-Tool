using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ScheduleBaselineTests(HubFactory f)
{
    readonly TestData d = new(f);
    async Task Flag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();

    [Fact, Trait("Spec", "AC-INT-15"), Trait("Spec", "AC-INT-16")]
    public async Task Baseline_requires_pm_reason_and_current_version_and_preserves_original_slip()
    {
        await Flag("schedule_baseline", true);
        try
        {
            var p = await d.Project(); var pm = f.As(TestData.Pm);
            var m = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic baseline", milestoneType = "Design Submission", date = "2027-03-01" }).Result.Json(201);
            var id = m.G("id");
            await pm.Post($"/api/v1/milestones/{id}/change-date", new { newDate = "2027-03-15", reason = "Synthetic revised schedule", rowVersion = m.I("rowVersion") }).Result.Json();
            await f.Evaluate(p.Id);
            var current = (await pm.GetAsync($"/api/v1/milestones/{id}").Result.Json())["milestone"]!;
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Post($"/api/v1/milestones/{id}/rebaseline", new { reason = "Synthetic reason", rowVersion = current.I("rowVersion") })).StatusCode);
            Assert.False((await pm.Post($"/api/v1/milestones/{id}/rebaseline", new { rowVersion = current.I("rowVersion") })).IsSuccessStatusCode);
            var baseline = await pm.Post($"/api/v1/milestones/{id}/rebaseline", new { reason = "Synthetic PM schedule review", rowVersion = current.I("rowVersion") }).Result.Json();
            Assert.Equal(HttpStatusCode.Conflict, (await pm.Post($"/api/v1/milestones/{id}/rebaseline", new { reason = "Stale synthetic request", rowVersion = current.I("rowVersion") })).StatusCode);
            var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == id && a.Reason == "Synthetic PM schedule review").ToListAsync());
            Assert.Single(log); Assert.Contains("BaselineDate", log[0].Changes);
            await pm.Post($"/api/v1/milestones/{id}/change-date", new { newDate = "2027-03-18", reason = "Synthetic three-day revision", rowVersion = baseline.I("rowVersion") }).Result.Json();
            await f.Evaluate(p.Id);
            var after = (await pm.GetAsync($"/api/v1/milestones/{id}").Result.Json())["milestone"]!;
            Assert.Equal("2027-03-01", after.S("originalDate")); Assert.Equal(17, after.I("slipDays")); Assert.Equal(3, after.I("sinceBaselineDays"));
            var csv = await (await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones/export?format=csv")).Content.ReadAsStringAsync();
            Assert.Contains("Days since re-baseline", csv);
            await pm.Post($"/api/v1/milestones/{id}/rebaseline", new { clear = true, reason = "Use synthetic original", rowVersion = after.I("rowVersion") }).Result.Json();
            var cleared = (await pm.GetAsync($"/api/v1/milestones/{id}").Result.Json())["milestone"]!;
            Assert.Equal(17, cleared.I("slipDays")); Assert.Null(cleared["baselineDate"]); Assert.Null(cleared["sinceBaselineDays"]);
            await Flag("schedule_baseline", false);
            var off = (await pm.GetAsync($"/api/v1/milestones/{id}").Result.Json())["milestone"]!;
            Assert.Equal(17, off.I("slipDays")); Assert.Null(off["slipShown"]);
            Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/milestones/{id}/rebaseline", new { reason = "Synthetic flag off", rowVersion = cleared.I("rowVersion") })).StatusCode);
        }
        finally { await Flag("schedule_baseline", false); }
    }

    [Fact, Trait("Spec", "AC-INT-22")]
    public async Task Creating_deliverable_from_task_links_both_reads_without_moving_task_and_rejects_other_project()
    {
        await Flag("small_ux", true);
        try
        {
            var p = await d.Project(); var other = await d.Project(); var task = await d.NewTask(p.Id, TestData.Pm);
            var pm = f.As(TestData.Pm);
            var type = await d.DeliverableType();
            object Body(Guid source) => new { name = "Synthetic related deliverable", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = type, fromTaskId = source };
            var made = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", Body(task.G("id"))).Result.Json(201);
            var tid = task.G("id"); var did = made.G("id");
            Assert.Null(await f.DbAsync(db => db.Tasks.Where(t => t.Id == tid).Select(t => t.DeliverableId).SingleAsync()));
            Assert.Contains((await pm.GetAsync($"/api/v1/tasks/{tid}").Result.Json())["relatedDeliverables"]!.AsArray(), r => r!.G("id") == did);
            Assert.Contains((await pm.GetAsync($"/api/v1/deliverables/{did}").Result.Json())["relatedTasks"]!.AsArray(), r => r!.G("id") == tid);
            Assert.Single(await f.DbAsync(db => db.ItemLinks.Where(l => l.SourceId == tid && l.TargetId == did && l.Relation == ItemRelation.Related).ToListAsync()));
            var foreign = await d.NewTask(other.Id, TestData.Pm);
            Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/deliverables", Body(foreign.G("id")))).StatusCode);
            await Flag("small_ux", false);
            Assert.Null((await pm.GetAsync($"/api/v1/tasks/{tid}").Result.Json())["relatedDeliverables"]);
        }
        finally { await Flag("small_ux", false); }
    }

    [Fact, Trait("Spec", "AC-INT-21"), Trait("Spec", "AC-INT-05")]
    [Trait("flag-off", "small_ux"), Trait("flag-on", "small_ux")]
    public async Task Task_name_edit_logs_once_and_newest_actor_is_flagged_without_changing_sort()
    {
        var p = await d.Project(); var task = await d.NewTask(p.Id, TestData.Pm); var id = task.G("id"); var pm = f.As(TestData.Pm);
        var oldName = await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.Name).SingleAsync());
        var before = await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ItemId == id));
        await pm.Patch($"/api/v1/tasks/{id}", new { name = "Synthetic renamed work", rowVersion = task.I("rowVersion") }).Result.Json();
        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == id).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).FirstAsync());
        Assert.Equal(before + 1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ItemId == id))); Assert.Contains(oldName, log.Changes); Assert.Contains("Synthetic renamed work", log.Changes);
        await Flag("small_ux", true);
        try
        {
            var on = (await pm.GetAsync($"/api/v1/projects/{p.Id}/tasks?sort=key").Result.Json())["items"]!.AsArray();
            Assert.Equal("Priya Nair", on.Single(t => t!.G("id") == id)!.S("lastActivityActorName"));
            await Flag("small_ux", false);
            var off = (await pm.GetAsync($"/api/v1/projects/{p.Id}/tasks?sort=key").Result.Json())["items"]!.AsArray();
            Assert.Equal(on.Select(t => t!.G("id")), off.Select(t => t!.G("id"))); Assert.Null(off.Single(t => t!.G("id") == id)!["lastActivityActorName"]);
        }
        finally { await Flag("small_ux", false); }
    }
    [Fact]
    public async Task Task_activity_actor_preserves_enabled_null_and_disabled_omission()
    {
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Pm);
        var id = task.G("id");
        var rows = await f.DbAsync(db => Hub.Api.Features.TaskQueries.Rows(db, db.Tasks.Where(t => t.Id == id)));
        var row = Assert.Single(rows);
        var off = System.Text.Json.JsonSerializer.SerializeToNode(row, Hub.Api.Infrastructure.JsonOpts.Web)!.AsObject();
        var on = System.Text.Json.JsonSerializer.SerializeToNode<Hub.Api.Features.TaskQueries.Row>(new Hub.Api.Features.TaskQueries.ActivityRow(row, null), Hub.Api.Infrastructure.JsonOpts.Web)!.AsObject();
        Assert.False(off.ContainsKey("lastActivityActorName"));
        Assert.True(on.ContainsKey("lastActivityActorName"));
        Assert.Null(on["lastActivityActorName"]);
        on.Remove("lastActivityActorName");
        Assert.True(JsonNode.DeepEquals(off, on), "Enabling activity display must preserve every existing task field and add no type discriminator");
    }

}
