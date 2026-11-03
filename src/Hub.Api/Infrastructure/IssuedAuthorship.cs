using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Issue-time canonical authors, never a reconstruction of historical work from today's owners.
public static class IssuedAuthorship
{
    internal static Guid[] Normalize(IEnumerable<Guid> ids) => ids.Distinct().OrderBy(id => id.ToString("D"), StringComparer.Ordinal).ToArray();
    internal static bool Shape(Guid[]? ids) => ids is { Length: > 0 } && ids.All(id => id != Guid.Empty)
        && ids.SequenceEqual(Normalize(ids));

    public static async Task<Guid[]?> CaptureAsync(HubDb db, Guid deliverableId, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Issue authorship capture requires the issue transaction.");
        var deliverable = await db.Deliverables.AsNoTracking().SingleOrDefaultAsync(row => row.Id == deliverableId, ct);
        if (deliverable is null) return null;
        await Coordination.Lock(db, deliverable.ProjectId);
        var locked = await db.Deliverables.AsNoTracking().SingleOrDefaultAsync(row => row.Id == deliverableId, ct);
        if (locked is null || locked.ProjectId != deliverable.ProjectId) return null;
        // Canonical Authors reads persisted source rows. Pending author/link/visibility changes have no complete
        // persisted set yet; preserve issuance with Unknown instead of silently omitting them.
        if (db.ChangeTracker.Entries<WorkTask>().Any(entry =>
            (entry.Entity.DeliverableId == deliverableId || entry.State != EntityState.Added && entry.Property(row => row.DeliverableId).OriginalValue == deliverableId)
            && (entry.State is EntityState.Added or EntityState.Deleted || entry.State == EntityState.Modified
                && (entry.Property(row => row.AssigneeId).IsModified || entry.Property(row => row.CreatedBy).IsModified
                    || entry.Property(row => row.DeliverableId).IsModified || entry.Property(row => row.ProjectId).IsModified
                    || entry.Property(row => row.DeletedAt).IsModified)))
            || db.ChangeTracker.Entries<Deliverable>().Any(entry => entry.Entity.Id == deliverableId && entry.State == EntityState.Modified
                && (entry.Property(row => row.OwnerId).IsModified || entry.Property(row => row.CreatedBy).IsModified || entry.Property(row => row.ProjectId).IsModified))) return null;
        // A malformed cross-project link cannot be dropped to manufacture a supposedly complete set.
        if (await db.Tasks.AsNoTracking().AnyAsync(row => row.DeliverableId == deliverableId && row.ProjectId != deliverable.ProjectId, ct)) return null;
        var authors = Normalize(await Coordination.Authors(db, deliverableId));
        if (!Shape(authors) || await db.Users.AsNoTracking().CountAsync(row => authors.Contains(row.Id), ct) != authors.Length) return null;
        return authors;
    }

    public static async Task<Guid[]?> ReadAsync(HubDb db, DeliverableIssue issue, CancellationToken ct = default)
    {
        var known = await ReadManyAsync(db, [issue], ct);
        return known.GetValueOrDefault(issue.Id);
    }

    /// At most three batched reads, with the same stored-snapshot/parent/user binding as a single read.
    /// Unknown entries are absent; duplicate conflicting requests cannot select a more convenient snapshot.
    public static async Task<Dictionary<Guid, Guid[]>> ReadManyAsync(HubDb db, IReadOnlyCollection<DeliverableIssue> issues, CancellationToken ct = default)
    {
        var requested = issues.Where(issue => issue is not null && issue.Id != Guid.Empty).GroupBy(issue => issue.Id).ToDictionary(group => group.Key, group => group.ToArray());
        var ids = requested.Keys.ToArray();
        if (ids.Length == 0) return [];
        var stored = await db.DeliverableIssues.AsNoTracking().Where(issue => ids.Contains(issue.Id)).ToListAsync(ct);
        var candidates = stored.Where(issue => Shape(issue.IssuedAuthorIds) && requested[issue.Id].All(input => input.ProjectId == issue.ProjectId
            && input.DeliverableId == issue.DeliverableId && Shape(input.IssuedAuthorIds) && input.IssuedAuthorIds!.SequenceEqual(issue.IssuedAuthorIds!))).ToArray();
        if (candidates.Length == 0) return [];
        var deliverableIds = candidates.Select(issue => issue.DeliverableId).Distinct().ToArray();
        var parents = await db.Deliverables.AsNoTracking().Where(row => deliverableIds.Contains(row.Id)).Select(row => new { row.Id, row.ProjectId }).ToDictionaryAsync(row => row.Id, ct);
        var authorIds = candidates.SelectMany(issue => issue.IssuedAuthorIds!).Distinct().ToArray();
        // Deliberately no active-user filter: deactivation does not erase captured historical authorship.
        var users = (await db.Users.AsNoTracking().Where(user => authorIds.Contains(user.Id)).Select(user => user.Id).ToListAsync(ct)).ToHashSet();
        return candidates.Where(issue => parents.TryGetValue(issue.DeliverableId, out var parent) && parent.ProjectId == issue.ProjectId
                && issue.IssuedAuthorIds!.All(users.Contains))
            .ToDictionary(issue => issue.Id, issue => issue.IssuedAuthorIds!.ToArray());
    }
}
