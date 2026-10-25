using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Called by 046's re-baseline action before its final save, under that command's project lock/transaction.
public static class BaselineHistory
{
    public static readonly Col[] DeltaColumns = [new("baselineNumber", "baseline", "number", "Baseline"),
        new("estimateDelta", "estimateDelta", "number", "Estimate change (h)"), new("remainingDelta", "remainingDelta", "number", "Remaining change (h)"),
        new("dueDaysDelta", "dueDaysDelta", "number", "Due-date change (days)"), new("ownerChange", "ownerChange", Label: "Owner change")];

    public static async Task AddDeltas(HubDb db, JsonArray rows)
    {
        var ids = rows.Select(r => Guid.Parse(r!["id"]!.GetValue<string>())).ToArray();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => ids.Contains(d.Id)).ToListAsync();
        var baselineIds = deliverables.Select(d => d.CurrentBaselineId).OfType<Guid>().ToArray();
        var baselines = await db.Set<DeliverableBaseline>().AsNoTracking().Where(b => baselineIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id);
        var tasks = await db.Tasks.AsNoTracking().Where(t => ids.Contains(t.DeliverableId ?? Guid.Empty) && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.DeliverableId, t.EstimatedHours, t.ProgressPct, t.Status }).ToListAsync();
        var owners = deliverables.Select(d => d.OwnerId).Concat(baselines.Values.Select(b => b.OwnerId)).OfType<Guid>().Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => owners.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        foreach (var row in rows)
        {
            var d = deliverables.First(x => x.Id == Guid.Parse(row!["id"]!.GetValue<string>()));
            var b = d.CurrentBaselineId is { } bid ? baselines.GetValueOrDefault(bid) : null;
            var linked = tasks.Where(t => t.DeliverableId == d.Id).ToArray();
            row!["baselineNumber"] = b?.Number;
            row["estimateDelta"] = b == null ? null : linked.Sum(t => t.EstimatedHours ?? 0) - b.EstimatedHoursTotal;
            row["remainingDelta"] = b == null ? null : linked.Where(t => t.Status != TaskStatuses.Complete).Sum(t => (t.EstimatedHours ?? 0) * (100 - t.ProgressPct) / 100m) - b.RemainingHours;
            row["dueDaysDelta"] = b?.DueDate is { } baseline && d.DueDate is { } due ? due.DayNumber - baseline.DayNumber : null;
            row["ownerChange"] = b == null || b.OwnerId == d.OwnerId ? null : $"{(b.OwnerId is { } old ? names.GetValueOrDefault(old) : "Unassigned")} → {(d.OwnerId is { } current ? names.GetValueOrDefault(current) : "Unassigned")}";
        }
    }

    public static async Task Capture(HubDb db, Deliverable deliverable, bool clear, string reason, Guid actor, DateTimeOffset now)
    {
        if (!(await db.CurrentSettings()).IsFlagOn("baseline_history")) return;
        if (db.Database.CurrentTransaction == null) throw new InvalidOperationException("Baseline capture requires the re-baseline transaction.");
        if (clear) { deliverable.CurrentBaselineId = null; return; }
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.DeliverableId == deliverable.Id && t.DeletedAt == null && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.EstimatedHours, t.ProgressPct, t.Status }).ToListAsync();
        var baseline = new DeliverableBaseline { ProjectId = deliverable.ProjectId, DeliverableId = deliverable.Id,
            Number = (await db.Set<DeliverableBaseline>().Where(b => b.DeliverableId == deliverable.Id).MaxAsync(b => (int?)b.Number) ?? 0) + 1,
            StartDate = deliverable.BaselineStartDate, DueDate = deliverable.BaselineDueDate, OwnerId = deliverable.OwnerId,
            EstimatedHoursTotal = tasks.Sum(t => t.EstimatedHours ?? 0),
            RemainingHours = tasks.Where(t => t.Status != TaskStatuses.Complete).Sum(t => (t.EstimatedHours ?? 0) * (100 - t.ProgressPct) / 100m),
            ActorId = actor, RecordedAt = now, Reason = Check.Reason(reason) };
        db.Add(baseline); deliverable.CurrentBaselineId = baseline.Id;
    }
}

public static class BaselineHistoryEndpoints
{
    public sealed record Person(Guid Id, string DisplayName);
    public sealed record Delta(decimal EstimateHours, decimal RemainingHours, int? DueDays, Guid? OwnerBefore, Guid? OwnerAfter);
    public sealed record History(IReadOnlyList<DeliverableBaseline> Records, IReadOnlyList<Person> People, Guid? Current, Delta? Delta);
    public static void Map(RouteGroupBuilder api) => api.MapGet("/deliverables/{id:guid}/baselines", async (Guid id, Access access, HubDb db) =>
    {
        var (d, _, _) = await DeliverableEndpoints.Load(db, access, id);
        var records = await db.Set<DeliverableBaseline>().AsNoTracking().Where(b => b.DeliverableId == id).OrderBy(b => b.Number).ToListAsync();
        var current = records.FirstOrDefault(b => b.Id == d.CurrentBaselineId);
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.DeliverableId == id && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.EstimatedHours, t.ProgressPct, t.Status }).ToListAsync();
        var personIds = records.SelectMany(b => new Guid?[] { b.OwnerId, b.ActorId }).Append(d.OwnerId).OfType<Guid>().Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(u => personIds.Contains(u.Id)).Select(u => new Person(u.Id, u.DisplayName)).ToListAsync();
        return new History(records, people, d.CurrentBaselineId, current == null ? null : new Delta(
            tasks.Sum(t => t.EstimatedHours ?? 0) - current.EstimatedHoursTotal,
            tasks.Where(t => t.Status != TaskStatuses.Complete).Sum(t => (t.EstimatedHours ?? 0) * (100 - t.ProgressPct) / 100m) - current.RemainingHours,
            d.DueDate is { } due && current.DueDate is { } baseline ? due.DayNumber - baseline.DayNumber : (int?)null,
            current.OwnerId, d.OwnerId));
    }).RequireFlag("baseline_history");
}
