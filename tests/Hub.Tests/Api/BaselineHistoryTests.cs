using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class BaselineHistoryTests
{
    [Fact]
    [Trait("flag-on", "baseline_history")]
    [Trait("flag-off", "baseline_history")]
    public async Task Numbered_snapshots_are_immutable_clear_retains_history_and_exports_show_deltas()
    {
        using var f = new HubFactory(); var d = new TestData(f); var admin = f.As(TestData.Admin);
        var project = await d.Project();
        var row = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new { name = "Baseline package",
            projectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"), deliverableTypeId = await d.DeliverableType(),
            ownerId = d.User(TestData.Alex), startDate = "2026-09-14", dueDate = "2026-09-18" })).Json(201);
        var id = row.G("id");
        await d.NewTask(project.Id, extra: new { deliverableId = id, estimatedHours = 10m, progressPct = 20 });
        await f.DbAsync(async db => { var task = await db.Tasks.SingleAsync(t => t.DeliverableId == id); task.ProgressPct = 20; return await db.SaveChangesAsync(); });
        async Task Capture(bool clear = false) => await f.DbAsync(async db => await Tx.Run(db, async () =>
        {
            await Coordination.Lock(db, project.Id);
            var deliverable = await db.Deliverables.SingleAsync(x => x.Id == id);
            deliverable.BaselineStartDate = clear ? null : deliverable.StartDate;
            deliverable.BaselineDueDate = clear ? null : deliverable.DueDate;
            await BaselineHistory.Capture(db, deliverable, clear, "Approved PM baseline", d.User(TestData.Pm), f.Clock.Now);
            return await db.SaveChangesAsync();
        }));
        await Capture();
        Assert.Equal(0, await f.DbAsync(db => db.Set<DeliverableBaseline>().CountAsync()));
        await (await admin.Put("/api/v1/admin/settings/flag.baseline_history", new { value = true })).Json();
        await Capture();
        var first = await f.DbAsync(db => db.Set<DeliverableBaseline>().AsNoTracking().SingleAsync());
        Assert.Equal(1, first.Number); Assert.Equal(10m, first.EstimatedHoursTotal); Assert.Equal(8m, first.RemainingHours);
        await f.DbAsync(async db => { var task = await db.Tasks.SingleAsync(t => t.DeliverableId == id); task.EstimatedHours = 15m; return await db.SaveChangesAsync(); });
        var history = await (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{id}/baselines")).Json();
        Assert.Equal(5m, history["delta"]!["estimateHours"]!.GetValue<decimal>());
        var export = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/deliverables/export?format=csv"); export.EnsureSuccessStatusCode();
        Assert.Contains("Estimate change (h)", await export.Content.ReadAsStringAsync());
        await Capture(); await Capture(clear: true);
        Assert.Equal(new[] { 1, 2 }, await f.DbAsync(db => db.Set<DeliverableBaseline>().OrderBy(b => b.Number).Select(b => b.Number).ToArrayAsync()));
        Assert.Null(await f.DbAsync(db => db.Deliverables.Where(x => x.Id == id).Select(x => x.CurrentBaselineId).SingleAsync()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { var b = await db.Set<DeliverableBaseline>().FirstAsync(); b.Reason = "Forbidden"; return await db.SaveChangesAsync(); }));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => f.DbAsync(db => db.Database.ExecuteSqlRawAsync("DELETE FROM hub.deliverable_baseline")));
    }
}
