using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// §25 (Rec): a create retried with the same `Idempotency-Key` within a day gets the first response back instead of making
/// a second item. Only successful POST responses are kept; a key reused for a different path is refused. The key is claimed
/// before the handler runs (§48.7), so a retry racing the first request gets 409 `idempotency_in_progress` instead of running.
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    const int MaxBody = 256 * 1024;
    const int Pending = 0;
    /// §48.11 D7: a pending claim older than this is treated as abandoned (a crash between the handler and the fill).
    static readonly TimeSpan AbandonAfter = TimeSpan.FromMinutes(2);

    /// Inserts a pending receipt; null when the person already has a row for this key. A pending claim's `created_at` is one
    /// day before the claim, so a one-day receipt lookup (the previous image's) never sees it (FR-OPS-14).
    public static async Task<Guid?> Claim(HubDb db, Guid userId, string key, string path, DateTimeOffset now)
    {
        var id = Guid.CreateVersion7();
        var rows = await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO hub.idempotency_record (id, user_id, key, path, status, content_type, body, created_at) VALUES ({id}, {userId}, {key}, {path}, {Pending}, NULL, {Array.Empty<byte>()}, {now.AddDays(-1)}) ON CONFLICT (user_id, key) DO NOTHING");
        return rows == 1 ? id : null;
    }

    static Task Release(HubDb db, Guid id) => db.Idempotency.Where(x => x.Id == id && x.Status == Pending).ExecuteDeleteAsync();

    public async Task Invoke(HttpContext ctx, HubDb db, CurrentUser me, TimeProvider clock)
    {
        // Handoff commands use an atomic, permission-checked receipt in their own transaction.
        if (ctx.GetEndpoint()?.Metadata.GetMetadata<Hub.Api.Features.HandoffEndpoints.AtomicCommand>() is not null
            || ctx.GetEndpoint()?.Metadata.GetMetadata<Coordination.AtomicCommand>() is not null)
        { await next(ctx); return; }
        if (ctx.Request.Path == "/api/v1/route-counts" || ctx.Request.Path.StartsWithSegments("/api/v1/me/calendar-subscription")) { await next(ctx); return; }
        var key = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (!HttpMethods.IsPost(ctx.Request.Method) || string.IsNullOrEmpty(key) || !me.Resolved) { await next(ctx); return; }
        Check.That(key.Length <= 100, "Idempotency-Key", "error.too_long", 100);
        if (ctx.Request.Path.StartsWithSegments("/api/v1/planning") || ctx.Request.Path == "/api/v1/projects/import")
        {
            await Hub.Api.Features.PlanningIdempotency.Run(ctx, next, db, me, clock, key);
            return;
        }
        var path = ctx.Request.Path.Value ?? "";
        // FR-OPS-14: claim the key in its own committed statement before the handler runs, so two racing retries never both run.
        var claim = await Claim(db, me.Id, key, path, clock.GetUtcNow());
        while (claim is null)
        {
            var seen = await db.Idempotency.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id && x.Key == key);
            if (seen is null) { claim = await Claim(db, me.Id, key, path, clock.GetUtcNow()); continue; }
            var now = clock.GetUtcNow();
            var pending = seen.Status == Pending;
            var live = pending ? seen.CreatedAt.AddDays(1) > now - AbandonAfter : seen.CreatedAt > now.AddDays(-1);
            if (live)
            {
                if (seen.Path != path) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                if (pending)
                {
                    ctx.Response.OnStarting(() => { ctx.Response.Headers.RetryAfter = "1"; return Task.CompletedTask; });
                    throw ApiException.Conflict("idempotency_in_progress", "error.idempotency_in_progress");
                }
                ctx.Response.StatusCode = seen.Status;
                if (seen.ContentType is { } type) ctx.Response.ContentType = type;
                ctx.Response.Headers["Idempotent-Replayed"] = "true";
                await ctx.Response.Body.WriteAsync(seen.Body);
                return;
            }
            // An old receipt or an abandoned claim is replaced; the row's previous values guard against a racing replacement.
            var replaced = await db.Idempotency.Where(x => x.Id == seen.Id && x.Status == seen.Status && x.CreatedAt == seen.CreatedAt)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Path, path).SetProperty(x => x.Status, Pending).SetProperty(x => x.ContentType, (string?)null)
                    .SetProperty(x => x.Body, Array.Empty<byte>()).SetProperty(x => x.CreatedAt, now.AddDays(-1)));
            if (replaced == 1) claim = seen.Id;
        }
        var id = claim.Value;
        var original = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try { await next(ctx); }
        catch { await Release(db, id); throw; }
        finally { ctx.Response.Body = original; }
        if (ctx.Response.StatusCode is >= 200 and < 300 && buffer.Length <= MaxBody)
        {
            // Filled with ExecuteUpdate so entities the handler left tracked are never flushed here.
            int status = ctx.Response.StatusCode; string? type = ctx.Response.ContentType; var body = buffer.ToArray(); var at = clock.GetUtcNow();
            await db.Idempotency.Where(x => x.Id == id && x.Status == Pending).ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, status)
                .SetProperty(x => x.ContentType, type).SetProperty(x => x.Body, body).SetProperty(x => x.CreatedAt, at));
        }
        else await Release(db, id);
        buffer.Position = 0;
        await buffer.CopyToAsync(original);
    }
}
