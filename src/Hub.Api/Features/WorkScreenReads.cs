using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hub.Api.Features;

/// <summary>
/// Read bridge for exact canonical rows ALREADY AUTHORIZED by the caller. This is not an authorization
/// or item-discovery API. The key-based entry point requires the caller's existing RepeatableRead
/// (or stronger Serializable) transaction to cover its original row selection and all bridge reads.
/// The bound entry point must receive the exact returned source versions and role values, never newer
/// replacement reads. Neither entry point enrolls, acknowledges, saves, or widens the selected keys.
/// </summary>
public static class WorkScreenReads
{
    public sealed record WorkScreenReadEvidence(IReadOnlyDictionary<WorkSeenKey, WorkSeenDecoration> Seen,
        IReadOnlyDictionary<WorkSeenKey, WorkActivityFact> Activity);

    static WorkScreenReadEvidence Empty() => new(new Dictionary<WorkSeenKey, WorkSeenDecoration>(), new Dictionary<WorkSeenKey, WorkActivityFact>());

    /// <summary>Use exact caller-selected canonical bounds; repeated rows share one activity lookup.</summary>
    public static Task<WorkScreenReadEvidence> ReadAsync(HubDb db, Access access, OrgSettings settings, WorkSeenContext context,
        IReadOnlyCollection<WorkSeenReadBound> items, CancellationToken ct = default)
    {
        if (!settings.IsFlagOn("work_screens")) return Task.FromResult(Empty());
        var keys = items.Select(item => item.Key).Distinct().ToArray();
        return Core(db, access, settings, context, items, keys, ct);
    }

    /// <summary>
    /// Caller has already authorized these exact returned typed keys, inside the same snapshot.
    /// Only the nine mapped canonical kinds are loaded for seen bounds. Milestone/other authorized
    /// activity keys stay in the full key set without acquiring fabricated assignment roles.
    /// </summary>
    public static async Task<WorkScreenReadEvidence> ReadAuthorisedAsync(HubDb db, Access access, OrgSettings settings, WorkSeenContext context,
        IReadOnlyCollection<WorkSeenKey> authorisedKeys, CancellationToken ct = default)
    {
        // No model/database/business queries, transaction requirement or ID discovery while dark.
        if (!settings.IsFlagOn("work_screens")) return Empty();
        var keys = authorisedKeys.Distinct().ToArray();
        if (keys.Length == 0) return Empty();
        var isolation = db.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel;
        if (isolation is not (IsolationLevel.RepeatableRead or IsolationLevel.Serializable))
            throw new InvalidOperationException("WorkScreenReads requires the caller's existing consistent snapshot, including original authorization and row selection.");

        var bounds = new List<WorkSeenReadBound>();
        if (WorkSeenEligibility.Own(access, settings, context))
        {
            async Task Load<T>(IQueryable<T> query, Guid[] ids) where T : Entity
            {
                // Keep canonical query filters and read only the exact selected kind/IDs; no Include.
                foreach (var entity in await query.Where(entity => ids.Contains(entity.Id)).ToListAsync(ct))
                    bounds.Add(WorkAssignmentPublication.Bound(db, entity));
            }
            foreach (var group in keys.GroupBy(key => key.ItemType))
            {
                var ids = group.Select(key => key.ItemId).ToArray();
                switch (group.Key)
                {
                    case "Task": await Load(db.Tasks.AsNoTracking(), ids); break;
                    case "Deliverable": await Load(db.Deliverables.AsNoTracking(), ids); break;
                    case "Decision": await Load(db.Decisions.AsNoTracking(), ids); break;
                    case "Action": await Load(db.Actions.AsNoTracking(), ids); break;
                    case "Risk": await Load(db.Risks.AsNoTracking(), ids); break;
                    case "Issue": await Load(db.Issues.AsNoTracking(), ids); break;
                    case "Handoff": await Load(db.Handoffs.AsNoTracking(), ids); break;
                    case "ChangeNotice": await Load(db.ChangeNotices.AsNoTracking(), ids); break;
                    case "Project": await Load(db.Projects.AsNoTracking(), ids); break;
                }
            }
        }
        // Common core retains Milestone activity keys without a second enrichment query.
        return await Core(db, access, settings, context, bounds, keys, ct);
    }

    static async Task<WorkScreenReadEvidence> Core(HubDb db, Access access, OrgSettings settings, WorkSeenContext context,
        IReadOnlyCollection<WorkSeenReadBound> bounds, IReadOnlyCollection<WorkSeenKey> keys, CancellationToken ct)
    {
        if (!settings.IsFlagOn("work_screens") || keys.Count == 0) return Empty();
        IReadOnlyDictionary<WorkSeenKey, WorkSeenDecoration> seen = new Dictionary<WorkSeenKey, WorkSeenDecoration>();
        if (bounds.Count > 0 && WorkSeenEligibility.Own(access, settings, context))
            seen = await WorkSeenReads.DecorateAsync(db, access, settings, context, bounds, ct);
        var activity = await WorkActivityFacts.ReadAuthorisedAsync(db, keys, ct);
        return new(seen, activity);
    }
}
