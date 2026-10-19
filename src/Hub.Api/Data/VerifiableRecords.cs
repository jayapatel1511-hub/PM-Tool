using System.Globalization;
using System.Text.Json;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

public sealed class ActivityChainHead
{
    public Guid ChainId { get; set; }
    public long HeadSeq { get; set; }
    public string HeadHash { get; set; } = AuditChain.ZeroHash;
    public long LastVerifiedSeq { get; set; }
    public string LastVerifiedHash { get; set; } = AuditChain.ZeroHash;
    public DateTimeOffset? LastVerifiedAt { get; set; }
    public long? FirstBrokenSeq { get; set; }
    public long? AlertedBrokenSeq { get; set; }
}

public sealed class CommitmentMissCategory : RefData;

public sealed class DeliverableBaseline : Entity
{
    public Guid ProjectId { get; set; }
    public Guid DeliverableId { get; set; }
    public int Number { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public Guid? OwnerId { get; set; }
    public decimal EstimatedHoursTotal { get; set; }
    public decimal RemainingHours { get; set; }
    public string Reason { get; set; } = "";
    public Guid ActorId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
}

public static class VerifiableRecordsModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<ActivityChainHead>().HasKey(h => h.ChainId);
        mb.Entity<ActivityChainHead>().Property(h => h.ChainId).ValueGeneratedNever();
        mb.Entity<ActivityChainHead>().Property(h => h.HeadHash).HasMaxLength(64);
        mb.Entity<ActivityLog>().HasIndex(a => new { a.ChainId, a.ChainSeq }).IsUnique();
        mb.Entity<ActivityLog>().ToTable(t => t.HasCheckConstraint("ck_activity_chain_columns", "(chain_id IS NULL AND chain_seq IS NULL AND prev_hash IS NULL AND row_hash IS NULL) OR (chain_id IS NOT NULL AND chain_seq IS NOT NULL AND prev_hash IS NOT NULL AND row_hash IS NOT NULL AND chain_seq > 0 AND length(prev_hash) = 64 AND length(row_hash) = 64)"));
        mb.Entity<CommitmentMissCategory>();
        mb.Entity<OutputCommitmentEvent>().HasOne<CommitmentMissCategory>().WithMany().HasForeignKey(e => e.MissCategoryId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<DeliverableBaseline>().HasIndex(b => new { b.DeliverableId, b.Number }).IsUnique();
        mb.Entity<DeliverableBaseline>().HasOne<Deliverable>().WithMany().HasForeignKey(b => b.DeliverableId).OnDelete(DeleteBehavior.Restrict);
        mb.Entity<DeliverableBaseline>().Property(b => b.EstimatedHoursTotal).HasPrecision(12, 2);
        mb.Entity<DeliverableBaseline>().Property(b => b.RemainingHours).HasPrecision(12, 2);
    }
}

public static class AuditChainRows
{
    public static Guid Chain(ActivityLog row) => row.ProjectId ?? Guid.Empty;
    public static DateTimeOffset Microseconds(DateTimeOffset time) => new(time.UtcTicks - time.UtcTicks % 10, TimeSpan.Zero);

    public static string Hash(ActivityLog row) => AuditChain.Hash(JsonSerializer.SerializeToElement(new
    {
        chain_id = row.ChainId, chain_seq = row.ChainSeq, prev_hash = row.PrevHash,
        id = row.Id, occurred_at = Microseconds(row.OccurredAt).ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture),
        actor_user_id = row.ActorUserId, actor_type = row.ActorType, project_id = row.ProjectId,
        project_discipline_id = row.ProjectDisciplineId, item_type = row.ItemType, item_id = row.ItemId,
        item_key = row.ItemKey, item_name = row.ItemName, action = row.Action, categories = row.Categories,
        changes = JsonDocument.Parse(row.Changes).RootElement, reason = row.Reason,
        correlation_id = row.CorrelationId, source = row.Source,
        snapshot = row.Snapshot == null ? (JsonElement?)null : JsonDocument.Parse(row.Snapshot).RootElement,
    }));

    public static Task Lock(HubDb db, Guid chain, CancellationToken ct = default) =>
        db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({'A' + chain.ToString()}, 0))", ct);

    public static async Task Assign(HubDb db, DateTimeOffset now, CancellationToken ct, bool initializeLegacy = false)
    {
        var pending = db.ChangeTracker.Entries<ActivityLog>().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToArray();
        foreach (var group in pending.GroupBy(Chain).OrderBy(g => g.Key))
        {
            await Lock(db, group.Key, ct);
            var head = await db.Set<ActivityChainHead>().SingleOrDefaultAsync(h => h.ChainId == group.Key, ct);
            if (head != null && db.Entry(head).State == EntityState.Unchanged) await db.Entry(head).ReloadAsync(ct);
            if (head == null)
            {
                head = new ActivityChainHead { ChainId = group.Key };
                db.Add(head);
                var legacy = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == (group.Key == Guid.Empty ? null : group.Key))
                    .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).ToListAsync(ct);
                if (legacy.Count > 0 && !initializeLegacy)
                    throw new InvalidOperationException("Legacy audit rows require the privileged migration-only initializer before switch-on.");
                if (legacy.Any(a => a.ChainId != null)) throw new InvalidOperationException("Audit chain head missing; refusing to repair evidence.");
                foreach (var row in legacy)
                {
                    Stamp(row, head);
                    // Only the migration-only initializer can seal legacy rows; the runtime role has no UPDATE grant.
                    var count = await db.Database.ExecuteSqlAsync($"UPDATE hub.activity_log SET chain_id={row.ChainId}, chain_seq={row.ChainSeq}, prev_hash={row.PrevHash}, row_hash={row.RowHash} WHERE id={row.Id} AND chain_id IS NULL", ct);
                    if (count != 1) throw new InvalidOperationException("Audit backfill changed concurrently.");
                }
                var started = new ActivityLog { ProjectId = group.Key == Guid.Empty ? null : group.Key,
                    OccurredAt = Microseconds(now), ActorType = "System", Source = "Migration", ItemType = "AuditChain",
                    Action = "Chain started", Categories = ["admin"], Changes = JsonSerializer.Serialize(new[] { new { field = "RetroactiveCount", old = (object?)null, @new = legacy.Count } }) };
                Stamp(started, head); db.ActivityLog.Add(started);
            }
            foreach (var row in group) { row.OccurredAt = Microseconds(row.OccurredAt); Stamp(row, head); }
        }
    }

    static void Stamp(ActivityLog row, ActivityChainHead head)
    {
        row.ChainId = head.ChainId; row.ChainSeq = ++head.HeadSeq; row.PrevHash = head.HeadHash;
        row.RowHash = Hash(row); head.HeadHash = row.RowHash;
    }
}

/// Explicit maintenance step using the migration identity, before HTTP/background work starts.
public static class AuditChainInitialization
{
    public static async Task Run(HubDb db, DateTimeOffset now, CancellationToken ct = default)
    {
        var privileged = await db.Database.SqlQuery<bool>($"SELECT has_table_privilege(current_user, 'hub.activity_log', 'UPDATE') AS \"Value\"").SingleAsync(ct);
        if (!privileged) throw new InvalidOperationException("Audit initialization requires the migration identity; runtime grants must remain unchanged.");
        await Hub.Api.Infrastructure.Tx.Run(db, async () =>
        {
            db.initializingAuditChains = true;
            try
            {
                db.Audit.AsSystem("Migration");
                var flag = await db.Settings.SingleAsync(s => s.Key == "flag.verifiable_log", ct);
                if (flag.Value != "true") db.LogEvent("Setting", null, "Changed", "admin", key: flag.Key,
                    changes: new[] { new { field = "Value", old = false, @new = true } });
                flag.Value = "true";
                await db.SaveChangesAsync(ct);
                var projects = await db.ActivityLog.Select(a => a.ProjectId).Distinct().ToListAsync(ct);
                foreach (var project in projects.OrderBy(p => p ?? Guid.Empty))
                {
                    var chain = project ?? Guid.Empty;
                    if (await db.Set<ActivityChainHead>().AnyAsync(h => h.ChainId == chain, ct)) continue;
                    db.LogEvent("AuditChain", chain, "Chain initialization requested", "admin", projectId: project);
                    await db.SaveChangesAsync(ct);
                }
            }
            finally { db.initializingAuditChains = false; }
            return 0;
        });
    }
}
