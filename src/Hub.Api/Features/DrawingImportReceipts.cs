using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Drawing intake uses the existing one-day receipt store, with the successful receipt
/// committed inside the same transaction as all creates. No uploaded file is retained.
public static class DrawingImportReceipts
{
    /// Both preview and commit bypass the generic after-handler POST receipt middleware.
    public sealed record AtomicRequest;
    sealed record DrawingImportReceiptEnvelope<T>(string PayloadHash, T Result);
    const string ReceiptContentType = "application/vnd.tuesday.drawing-import-receipt+json";

    internal static async Task<T> Execute<T>(Guid projectId, object payload, HttpContext http,
        Access access, HubDb db, SettingsStore store, TimeProvider clock,
        Func<Project, ProjectContext, Task<T>> create,
        Func<T, Project, ProjectContext, Task> authoriseReplay)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, DeliverableEndpoints.RegisterFlag);
        await access.Project(projectId, track: false);
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault()?.Trim();
        Check.That(!string.IsNullOrEmpty(key) && key.Length <= 128 && !key.Any(char.IsControl), "Idempotency-Key", "error.required");
        var path = http.Request.Path.Value!;
        var hash = Coordination.Hash(payload);
        return await Tx.Run(db, async () =>
        {
            // A user/key is unique across projects and routes. Serialise that scope before
            // taking a project lock, including two competing requests for different projects.
            var lockKey = access.Actor.Id.ToString("D") + ":" + key;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 470059))");
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.project WHERE id = {0} FOR UPDATE", projectId).ToListAsync();
            var (project, context) = await access.Project(projectId);
            Access.Demand(Permissions.Writable(access.Actor, context));
            Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
            var now = clock.GetUtcNow();
            var prior = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == access.Actor.Id && row.Key == key);
            // Generic middleware backdates its pending claims by one day. They are
            // not successful expired receipts and this route never takes them over.
            if (prior is { Status: 0 }) throw ApiException.Conflict("idempotency_in_progress", "error.idempotency_in_progress");
            if (prior is not null && prior.CreatedAt > now.AddDays(-1))
            {
                if (prior.Path != path || prior.ContentType != ReceiptContentType || prior.Status != 200)
                    throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                var saved = JsonSerializer.Deserialize<DrawingImportReceiptEnvelope<T>>(prior.Body, JsonOpts.Web)
                    ?? throw new InvalidOperationException("Invalid committed drawing import receipt.");
                if (saved.PayloadHash != hash) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                // The caller checks today's create authority for every recorded discipline.
                // Stale preview/file/reference labels do not change an already committed result.
                await authoriseReplay(saved.Result, project, context);
                return saved.Result;
            }
            if (prior is not null)
            {
                var removed = await db.Idempotency.Where(row => row.Id == prior.Id && row.Status == prior.Status && row.CreatedAt == prior.CreatedAt)
                    .ExecuteDeleteAsync();
                if (removed != 1) throw ApiException.Conflict("idempotency_in_progress", "error.idempotency_in_progress");
            }
            // Claim the unique row BEFORE any create. The claim remains uncommitted
            // with the entire batch, so generic middleware racing this insert either
            // owns the key first (we refuse) or waits for our full commit/rollback.
            var receiptId = Guid.CreateVersion7();
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO hub.idempotency_record (id,user_id,key,path,status,content_type,body,created_at)
                VALUES ({receiptId},{access.Actor.Id},{key},{path},0,{ReceiptContentType},{Array.Empty<byte>()},{now})
                ON CONFLICT (user_id,key) DO NOTHING
                """);
            if (claimed != 1) throw ApiException.Conflict("idempotency_in_progress", "error.idempotency_in_progress");
            var result = await create(project, context);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new DrawingImportReceiptEnvelope<T>(hash, result), JsonOpts.Web);
            Check.That(bytes.Length <= 2 * 1024 * 1024, "import", "drawingImport.receiptLimit");
            var filled = await db.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE hub.idempotency_record SET status=200,body={bytes},created_at={now}
                WHERE id={receiptId} AND user_id={access.Actor.Id} AND status=0
                """);
            if (filled != 1) throw new InvalidOperationException("Drawing import lost its transaction-owned receipt.");
            await db.SaveChangesAsync();
            return result;
        });
    }
}
