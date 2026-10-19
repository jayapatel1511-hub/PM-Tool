using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ChainVerification(Guid Chain, long From, bool Valid, long Total, long? FirstBrokenSeq,
    long HeadSeq, string HeadHash, DateTimeOffset VerifiedAt, string Verifier);

public static class AuditChainEndpoints
{
    public sealed record Head(Guid ChainId, long HeadSeq, string HeadHash, long LastVerifiedSeq, string LastVerifiedHash, DateTimeOffset? LastVerifiedAt, long? FirstBrokenSeq, long? AlertedBrokenSeq);
    public sealed record Integrity(long Entries, DateTimeOffset? CheckedAt, long VerifiedEntries, long? FirstBrokenSeq, bool? Valid);
    public static void Map(RouteGroupBuilder api)
    {
        var admin = api.MapGroup("/admin/audit-chains").AddEndpointFilter(async (ctx, next) =>
        {
            Access.Demand(Permissions.Administer(ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>().Actor));
            return await next(ctx);
        }).RequireFlag("verifiable_log");
        admin.MapGet("", async (HubDb db) => await db.Set<ActivityChainHead>().AsNoTracking().OrderBy(h => h.ChainId)
            .Select(h => new Head(h.ChainId, h.HeadSeq, h.HeadHash, h.LastVerifiedSeq, h.LastVerifiedHash, h.LastVerifiedAt, h.FirstBrokenSeq, h.AlertedBrokenSeq)).ToListAsync());
        admin.MapGet("/{id:guid}/verify", async (Guid id, long? from, long? headSeq, string? headHash, HubDb db, TimeProvider clock) =>
            await AuditChainVerification.Check(db, id, from ?? 1, clock.GetUtcNow(), db.Audit.ActorId?.ToString() ?? "System", true, headHash, headSeq));
        admin.MapGet("/{id:guid}/record", async (Guid id, string? format, HubDb db, TimeProvider clock) =>
        {
            var record = await AuditChainVerification.Check(db, id, 1, clock.GetUtcNow(), db.Audit.ActorId?.ToString() ?? "System", true);
            db.LogEvent("AuditChain", id, "Verification record downloaded", "admin", reason: format ?? "json");
            await db.SaveChangesAsync();
            if (format == "csv")
                return TypedResults.File(Encoding.UTF8.GetBytes("chain,from,total,valid,first_broken_seq,head_seq,head_hash,verified_at,verifier\r\n" +
                    $"{record.Chain},{record.From},{record.Total},{record.Valid},{record.FirstBrokenSeq},{record.HeadSeq},{record.HeadHash},{record.VerifiedAt:O},{record.Verifier}\r\n"), "text/csv", "verification-record.csv");
            Check.That(format is null or "json", "format", "error.invalid");
            return TypedResults.File(JsonSerializer.SerializeToUtf8Bytes(record, JsonOpts.Web), "application/json", "verification-record.json");
        });
        api.MapGet("/projects/{id:guid}/activity/integrity", async (Guid id, Access access, HubDb db) =>
        {
            await access.Project(id, false);
            var head = await db.Set<ActivityChainHead>().AsNoTracking().SingleOrDefaultAsync(h => h.ChainId == id);
            return TypedResults.Ok(head == null ? null : new Integrity(head.HeadSeq, head.LastVerifiedAt,
                head.LastVerifiedSeq, head.FirstBrokenSeq,
                head.FirstBrokenSeq != null ? false : head.LastVerifiedAt != null && head.LastVerifiedSeq == head.HeadSeq ? true : (bool?)null));
        }).RequireFlag("verifiable_log");
    }
}

public static class AuditChainVerification
{
    public static Task<ChainVerification> Check(HubDb db, Guid chain, long from, DateTimeOffset now,
        string verifier, bool log = false, string? expectedHead = null, long? expectedHeadSeq = null, CancellationToken ct = default) => Tx.Run(db, async () =>
    {
        Hub.Api.Infrastructure.Check.That(from >= 1, "from", "error.invalid");
        if (log && chain != Guid.Empty) await AuditChainRows.Lock(db, Guid.Empty, ct);
        await AuditChainRows.Lock(db, chain, ct);
        var head = await db.Set<ActivityChainHead>().SingleOrDefaultAsync(h => h.ChainId == chain, ct) ?? throw ApiException.NotFound();
        if (db.Entry(head).State == EntityState.Unchanged) await db.Entry(head).ReloadAsync(ct);
        var through = expectedHeadSeq ?? head.HeadSeq;
        Hub.Api.Infrastructure.Check.That(through >= 1 && through <= head.HeadSeq && (expectedHead == null || expectedHeadSeq != null), "headSeq", "error.invalid");
        Hub.Api.Infrastructure.Check.That(from <= through, "from", "error.invalid");
        var prior = from == 1 ? AuditChain.ZeroHash : await db.ActivityLog.AsNoTracking()
            .Where(a => a.ChainId == chain && a.ChainSeq == from - 1).Select(a => a.RowHash).SingleOrDefaultAsync(ct);
        var anchored = from == 1 || head.LastVerifiedSeq == from - 1 && prior == head.LastVerifiedHash;
        long expected = from, total = 0;
        long? broken = prior == null || from > 1 && head.LastVerifiedSeq == from - 1 && prior != head.LastVerifiedHash ? from - 1 : null;
        // Sequence paging keeps large logs bounded; the chain lock freezes the head for this read.
        while (broken == null && expected <= through)
        {
            var page = await db.ActivityLog.AsNoTracking().Where(a => a.ChainId == chain && a.ChainSeq >= expected && a.ChainSeq <= through)
                .OrderBy(a => a.ChainSeq).Take(1000).ToListAsync(ct);
            if (page.Count == 0) { broken = expected; break; }
            foreach (var row in page)
            {
                if (row.ChainSeq != expected || row.PrevHash != prior || row.RowHash != AuditChainRows.Hash(row) || AuditChainRows.Chain(row) != chain)
                { broken = expected; break; }
                prior = row.RowHash; expected++; total++;
            }
        }
        if (broken == null && (through == head.HeadSeq && prior != head.HeadHash || expectedHead != null && expectedHead != prior)) broken = through;
        if (broken != null || anchored) head.LastVerifiedAt = now;
        if (broken == null && anchored && through >= head.LastVerifiedSeq)
        { head.LastVerifiedSeq = through; head.LastVerifiedHash = prior ?? AuditChain.ZeroHash; }
        // A later partial read cannot clear a previously observed break outside its checked range.
        if (broken != null) head.FirstBrokenSeq = head.FirstBrokenSeq is { } old ? Math.Min(old, broken.Value) : broken;
        else if (from == 1 && through == head.HeadSeq) head.FirstBrokenSeq = null;
        if (broken != null && head.AlertedBrokenSeq != head.FirstBrokenSeq)
        {
            head.AlertedBrokenSeq = head.FirstBrokenSeq;
            var admins = await db.Users.Include(u => u.Roles).Where(u => u.IsActive && u.Roles.Any(r => r.Role == SystemRole.Admin)).ToListAsync(ct);
            foreach (var admin in admins)
            {
                var title = $"Log check failed at entry {head.FirstBrokenSeq}";
                db.Notifications.Add(new Notification { UserId = admin.Id, EventType = "AuditChainBroken", ItemType = "AuditChain", ItemId = chain,
                    Title = title, Body = $"Chain {chain}", LinkPath = "/admin/audit-chains", CreatedAt = now });
                db.Emails.Add(new EmailMessage { UserId = admin.Id, ToAddress = admin.Email, Subject = title,
                    BodyText = $"Chain {chain}; first broken sequence {head.FirstBrokenSeq}. No evidence has been repaired.",
                    DedupKey = $"audit-chain:{chain}:{head.FirstBrokenSeq}:{admin.Id}", CreatedAt = now });
            }
        }
        var result = new ChainVerification(chain, from, broken == null, total, broken, through, prior ?? AuditChain.ZeroHash, now, verifier);
        if (log) db.LogEvent("AuditChain", chain, "Chain verified", "admin", reason: JsonSerializer.Serialize(result, JsonOpts.Web));
        await db.SaveChangesAsync(ct);
        return result;
    });
}

public sealed class AuditChainNightlyJob : IJob
{
    public string Name => "audit-chain-nightly";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => s.IsFlagOn("verifiable_log") && Schedule.DailyAt("02:30", now, last, s);
    public Task<object?> Run(IServiceProvider sp, CancellationToken ct) => AuditChainJobs.Run(sp, false, ct);
}
public sealed class AuditChainWeeklyJob : IJob
{
    public string Name => "audit-chain-weekly";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => s.IsFlagOn("verifiable_log") && Schedule.Every(TimeSpan.FromDays(7), now, last);
    public Task<object?> Run(IServiceProvider sp, CancellationToken ct) => AuditChainJobs.Run(sp, true, ct);
}

public static class AuditChainJobs
{
    public static async Task<object?> Run(IServiceProvider sp, bool full, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        var projects = await db.ActivityLog.Select(a => a.ProjectId).Distinct().ToListAsync(ct);
        // Initialisation also runs when switch-on is the only recent change; future writes use the same chain lock.
        foreach (var project in projects)
        {
            var chain = project ?? Guid.Empty;
            if (await db.Set<ActivityChainHead>().AnyAsync(h => h.ChainId == chain, ct)) continue;
            db.LogEvent("AuditChain", chain, "Chain initialization requested", "admin", projectId: project);
            await db.SaveChangesAsync(ct);
        }
        var heads = await db.Set<ActivityChainHead>().AsNoTracking().OrderBy(h => h.ChainId).ToListAsync(ct);
        var failed = 0;
        foreach (var head in heads)
        {
            var result = await AuditChainVerification.Check(db, head.ChainId, full || head.FirstBrokenSeq != null || head.LastVerifiedSeq >= head.HeadSeq ? 1 : Math.Max(1, head.LastVerifiedSeq + 1), now, "System", ct: ct);
            if (!result.Valid) failed++;
        }
        return new { Chains = heads.Count, Failed = failed, Full = full };
    }
}
