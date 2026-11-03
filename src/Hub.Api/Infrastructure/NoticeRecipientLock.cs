using System.Data;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Hub.Api.Infrastructure;

/// One transaction-scoped namespace for capture, policy writers and eventual delivery claims.
public static class NoticeRecipientLock
{
    public static string Key(Guid userId) => userId == Guid.Empty
        ? throw new ArgumentException("A notice recipient must be identified.", nameof(userId))
        : "notice-recipient:" + userId.ToString("D").ToLowerInvariant();

    public static async Task Acquire(HubDb db, Guid userId, CancellationToken ct = default)
    {
        var key = Key(userId);
        DemandTransaction(db);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({key}))", ct);
    }

    public static async Task AcquireMany(HubDb db, IEnumerable<Guid> userIds, CancellationToken ct = default)
    {
        // Validate the full set before taking the first key. Never change ordering to Guid's byte ordering.
        var ordered = userIds.Distinct().Select(id => (Id: id, Key: Key(id))).OrderBy(value => value.Key, StringComparer.Ordinal).ToArray();
        DemandTransaction(db);
        foreach (var recipient in ordered) await Acquire(db, recipient.Id, ct);
    }

    static void DemandTransaction(HubDb db)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Notice locking requires an originating transaction.");
        if (transaction.GetDbTransaction().IsolationLevel != IsolationLevel.ReadCommitted)
            throw new InvalidOperationException("Notice policy requires fresh ReadCommitted reads after locking.");
    }
}
