using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// §25 (Rec): a create retried with the same `Idempotency-Key` within a day gets the first response back instead of making
/// a second item. Only successful POST responses are kept; a key reused for a different path is refused.
public sealed class IdempotencyMiddleware(RequestDelegate next)
{
    const int MaxBody = 256 * 1024;

    public async Task Invoke(HttpContext ctx, HubDb db, CurrentUser me, TimeProvider clock)
    {
        var key = ctx.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        if (!HttpMethods.IsPost(ctx.Request.Method) || string.IsNullOrEmpty(key) || !me.Resolved) { await next(ctx); return; }
        Check.That(key.Length <= 100, "Idempotency-Key", "error.too_long", 100);
        var path = ctx.Request.Path.Value ?? "";
        var seen = await db.Idempotency.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id && x.Key == key && x.CreatedAt > clock.GetUtcNow().AddDays(-1));
        if (seen is not null)
        {
            if (seen.Path != path) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
            ctx.Response.StatusCode = seen.Status;
            if (seen.ContentType is { } type) ctx.Response.ContentType = type;
            ctx.Response.Headers["Idempotent-Replayed"] = "true";
            await ctx.Response.Body.WriteAsync(seen.Body);
            return;
        }
        var original = ctx.Response.Body;
        await using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;
        try { await next(ctx); }
        finally { ctx.Response.Body = original; }
        if (ctx.Response.StatusCode is >= 200 and < 300 && buffer.Length <= MaxBody)
        {
            db.Idempotency.Add(new IdempotencyRecord { UserId = me.Id, Key = key, Path = path, Status = ctx.Response.StatusCode,
                ContentType = ctx.Response.ContentType, Body = buffer.ToArray(), CreatedAt = clock.GetUtcNow() });
            // ponytail: two identical requests racing both run; the unique index keeps one record, the other save is dropped
            try { await db.SaveChangesAsync(); } catch (DbUpdateException) { }
        }
        buffer.Position = 0;
        await buffer.CopyToAsync(original);
    }
}
