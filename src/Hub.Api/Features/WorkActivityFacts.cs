using System.Linq.Expressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Exact-item display facts. History's key-prefix/related-record expansion is deliberately not used.
public static class WorkActivityFacts
{
    static IQueryable<T> Typed<T>(IQueryable<T> query, IReadOnlyCollection<WorkSeenKey> items)
    {
        var row = Expression.Parameter(typeof(T), "row");
        var type = Expression.Property(row, "ItemType");
        Expression id = Expression.Property(row, "ItemId");
        if (id.Type == typeof(Guid)) id = Expression.Convert(id, typeof(Guid?));
        Expression predicate = Expression.Constant(false);
        foreach (var group in items.Distinct().GroupBy(item => item.ItemType))
        {
            var ids = group.Select(item => (Guid?)item.ItemId).ToArray();
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(Guid?)], Expression.Constant(ids), id);
            predicate = Expression.OrElse(predicate, Expression.AndAlso(Expression.Equal(type, Expression.Constant(group.Key)), contains));
        }
        return query.Where(Expression.Lambda<Func<T, bool>>(predicate, row));
    }
    public static async Task<IReadOnlyDictionary<WorkSeenKey, WorkActivityFact>> ReadAsync(
        HubDb db, Access access, OrgSettings settings, IReadOnlyCollection<WorkSeenKey> items, CancellationToken ct = default)
    {
        if (!settings.IsFlagOn("work_screens") || !access.Actor.IsActive || items.Count == 0)
            return new Dictionary<WorkSeenKey, WorkActivityFact>();
        var projects = await access.VisibleProjectIds().ToArrayAsync(ct);
        var allowed = new List<WorkSeenKey>();
        foreach (var group in items.Distinct().GroupBy(key => key.ItemType))
        {
            var ids = group.Select(key => key.ItemId).ToArray();
            async Task<Guid[]> Select<T>(IQueryable<T> query, bool project = false) where T : class =>
                await query.Where(row => ids.Contains(EF.Property<Guid>(row, "Id"))
                    && projects.Contains(EF.Property<Guid>(row, project ? "Id" : "ProjectId")))
                    .Select(row => EF.Property<Guid>(row, "Id")).ToArrayAsync(ct);
            var found = group.Key switch {
                "Task" => await Select(db.Tasks.AsNoTracking()),
                "Deliverable" => await Select(db.Deliverables.AsNoTracking()),
                "Milestone" => await Select(db.Milestones.AsNoTracking()),
                "Decision" => await Select(db.Decisions.AsNoTracking()),
                "Action" => await Select(db.Actions.AsNoTracking()),
                "Risk" => await Select(db.Risks.AsNoTracking()),
                "Issue" => await Select(db.Issues.AsNoTracking()),
                "Project" => await Select(db.Projects.AsNoTracking(), true),
                "Handoff" => await Select(db.Handoffs.AsNoTracking()),
                "ChangeNotice" => await Select(db.ChangeNotices.AsNoTracking()),
                "OpenRole" => await Select(db.OpenRoles.AsNoTracking()),
                _ => []
            };
            allowed.AddRange(found.Select(id => new WorkSeenKey(group.Key, id)));
        }
        return await Core(db, allowed, ActivityEndpoints.Visible(db, access, projects),
            db.Comments.AsNoTracking().Where(comment => projects.Contains(comment.ProjectId)), ct);
    }
    /// Caller has just selected these exact authorized canonical rows. Never accepts client-supplied arbitrary IDs.
    internal static Task<IReadOnlyDictionary<WorkSeenKey, WorkActivityFact>> ReadAuthorisedAsync(
        HubDb db, IReadOnlyCollection<WorkSeenKey> items, CancellationToken ct = default) =>
        Core(db, items, db.ActivityLog.AsNoTracking(), db.Comments.AsNoTracking(), ct);

    static async Task<IReadOnlyDictionary<WorkSeenKey, WorkActivityFact>> Core(HubDb db, IReadOnlyCollection<WorkSeenKey> items,
        IQueryable<ActivityLog> logs, IQueryable<Comment> comments, CancellationToken ct)
    {
        if (items.Count == 0) return new Dictionary<WorkSeenKey, WorkActivityFact>();
        var keys = items.Distinct().ToArray();
        var latest = await Typed(logs, keys).GroupBy(log => new ItemGroupKey<Guid?> { ItemType = log.ItemType, ItemId = log.ItemId })
            .Select(group => group.OrderByDescending(log => log.OccurredAt).ThenByDescending(log => log.Id).First()).ToListAsync(ct);
        var actorIds = latest.Where(log => log.ActorType is "User" or "Admin").Select(log => log.ActorUserId).OfType<Guid>().Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName, ct);
        var discussions = keys.Where(key => ItemType.Commentable.Contains(key.ItemType)).ToArray();
        var counts = await Typed(comments.Where(comment => comment.DeletedAt == null), discussions)
            .GroupBy(comment => new ItemGroupKey<Guid> { ItemType = comment.ItemType, ItemId = comment.ItemId }).Select(group => new ItemCommentCount(group.Key.ItemType, group.Key.ItemId, group.Count())).ToListAsync(ct);
        var events = latest.Where(log => log.ItemId != null).ToDictionary(log => new WorkSeenKey(log.ItemType, log.ItemId!.Value));
        var totals = counts.ToDictionary(count => new WorkSeenKey(count.ItemType, count.ItemId), count => count.Count);
        return keys.ToDictionary(key => key, key => {
            events.TryGetValue(key, out var log);
            var supported = ItemType.Commentable.Contains(key.ItemType);
            return new WorkActivityFact(log?.Id, log?.OccurredAt, log?.ActorUserId,
                log?.ActorType == "System" ? Text.Get("common.system")
                    : log is { ActorType: "User" or "Admin", ActorUserId: { } actor } ? names.GetValueOrDefault(actor) : null,
                log?.ActorType, supported, supported ? totals.GetValueOrDefault(key) : null);
        });
    }
}

/// Member-init so EF Core can translate it as a composite GROUP BY key.
file sealed class ItemGroupKey<TId>
{
    public string ItemType { get; init; } = "";
    public TId ItemId { get; init; } = default!;
}
file sealed record ItemCommentCount(string ItemType, Guid ItemId, int Count);
