using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Private own-user facts, committed together with their exact immutable command receipt.
public static class MyWorkSeenEndpoints
{
    public sealed record AtomicCommand;
    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/me/work/visits", async Task<Ok<WorkSeenCommandResult>> (WorkSeenVisitBody body, HubDb db, Access access, SettingsStore settings, TimeProvider clock) =>
            TypedResults.Ok(await Run("Visit", body.RequestId, new { }, null, db, access, settings, clock))).WithMetadata(new AtomicCommand());
        api.MapPost("/me/work/seen-all", async Task<Ok<WorkSeenCommandResult>> (WorkSeenVisitBody body, HubDb db, Access access, SettingsStore settings, TimeProvider clock) =>
            TypedResults.Ok(await Run("MarkAll", body.RequestId, new { }, null, db, access, settings, clock))).WithMetadata(new AtomicCommand());
        api.MapPost("/me/work/opened", async Task<Ok<WorkSeenCommandResult>> (WorkSeenOpenedBody body, HubDb db, Access access, SettingsStore settings, TimeProvider clock) => {
            Own(access, await settings.Get(db));
            Check.That(body.ItemId != Guid.Empty, "itemId", "error.required");
            Check.That(body.ObservedTokens is { Length: > 0 and <= 32 } && body.ObservedTokens.All(token => token != Guid.Empty), "observedTokens", "error.invalid");
            var tokens = body.ObservedTokens.Distinct().Order().ToArray();
            var normalized = body with { ObservedTokens = tokens };
            return TypedResults.Ok(await Run("Opened", body.RequestId, new { normalized.ItemType, normalized.ItemId, observedTokens = tokens },
                normalized, db, access, settings, clock));
        }).WithMetadata(new AtomicCommand());
    }
    static void Own(Access access, OrgSettings settings)
    {
        Http.RequireFlag(settings, "work_screens");
        if (!access.Actor.IsActive || access.Actor.Id == Guid.Empty) throw ApiException.Forbidden("error.inactive_account");
        // ReadOnly prevents business writes, not this person's private acknowledgement/preferences.
    }
    static async Task<WorkSeenReadBound> Current(HubDb db, Access access, WorkSeenOpenedBody body)
    {
        object? item = body.ItemType switch {
            "Task" => await db.Tasks.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Deliverable" => await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Decision" => await db.Decisions.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Action" => await db.Actions.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Risk" => await db.Risks.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Issue" => await db.Issues.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Handoff" => await db.Handoffs.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "ChangeNotice" => await db.ChangeNotices.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            "Project" => await db.Projects.AsNoTracking().FirstOrDefaultAsync(row => row.Id == body.ItemId),
            _ => throw ApiException.Invalid("itemType", "error.invalid")
        };
        if (item is null) throw ApiException.NotFound();
        var bound = WorkAssignmentPublication.Bound(db, item);
        await access.Project(bound.ProjectId, track: false);
        if (!bound.Roles.Any(role => role.RecipientId == access.Actor.Id)) throw ApiException.NotFound();
        return bound;
    }
    static async Task<IReadOnlyList<WorkAssignmentOccurrence>> Observed(HubDb db, Access access, WorkSeenOpenedBody body)
    {
        var bound = await Current(db, access, body);
        var roles = bound.Roles.Where(role => role.RecipientId == access.Actor.Id).Select(role => role.Role).ToArray();
        var rows = await db.WorkAssignmentOccurrences.AsNoTracking().Where(row => body.ObservedTokens.Contains(row.Token)
            && row.RecipientId == access.Actor.Id && row.ItemType == body.ItemType && row.ItemId == body.ItemId
            && row.ProjectId == bound.ProjectId && row.SourceVersion <= bound.SourceVersion && roles.Contains(row.Role)).ToListAsync();
        if (rows.Count != body.ObservedTokens.Length) throw ApiException.NotFound();
        return rows;
    }
    static async Task<WorkSeenCommandResult> Run(string action, Guid requestId, object payload, WorkSeenOpenedBody? opened,
        HubDb db, Access access, SettingsStore store, TimeProvider clock)
    {
        var org = await store.Get(db);
        Own(access, org);
        Check.That(requestId != Guid.Empty, "requestId", "error.required");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { action, payload }, JsonOpts.Web)))).ToLowerInvariant();
        return await Tx.Run(db, async () => {
            var preferences = await UserPreferences.GetOrCreate(db, access.Actor.Id);
            await db.Database.SqlQuery<Guid>($"SELECT user_id AS \"Value\" FROM hub.user_setting WHERE user_id={access.Actor.Id} FOR UPDATE").ToListAsync();
            await db.Entry(preferences).ReloadAsync();
            Own(access, await store.Get(db));
            // Recheck authority even when the exact prior response is about to replay.
            var observed = opened is null ? null : await Observed(db, access, opened);
            var prior = await db.WorkSeenCommandReceipts.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == access.Actor.Id && row.RequestId == requestId);
            if (prior is not null)
            {
                if (prior.Action != action || prior.PayloadHash != hash) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                return JsonSerializer.Deserialize<WorkSeenCommandResult>(prior.ResponseJson, JsonOpts.Web)
                    ?? throw new InvalidOperationException("Invalid committed seen-state response.");
            }
            var now = clock.GetUtcNow();
            var context = new WorkSeenContext(access.Actor.Id, Clock.LocalDate(now, org), now);
            if (action == "Visit")
            {
                if (preferences.MyWorkFirstVisitAt is null)
                {
                    // One MVCC statement: no canonical item facts, IDs or hidden totals are read/returned.
                    await db.Database.ExecuteSqlInterpolatedAsync($"""
                        INSERT INTO hub.work_seen_acknowledgement (recipient_id,token,acknowledged_at,provenance,request_id)
                        SELECT recipient_id,token,{now},'InitialBaseline',{requestId} FROM hub.work_assignment_occurrence
                        WHERE recipient_id={access.Actor.Id} ON CONFLICT(recipient_id,token) DO NOTHING
                        """);
                    preferences.MyWorkFirstVisitAt = now;
                }
                preferences.MyWorkPreviousVisitAt = preferences.MyWorkLastVisitAt;
                preferences.MyWorkLastVisitAt = now;
            }
            else if (action == "MarkAll")
            {
                if (preferences.MyWorkFirstVisitAt is null) throw ApiException.Conflict("work_seen_not_enrolled", "error.invalid");
                await WorkSeenEligibility.AcknowledgeAllAsync(db, access, org, context, requestId);
                preferences.MyWorkLastMarkAllAt = now;
            }
            else
            {
                var tokens = observed!.Select(row => row.Token).ToArray();
                var existing = (await db.WorkSeenAcknowledgements.AsNoTracking().Where(row => row.RecipientId == access.Actor.Id && tokens.Contains(row.Token))
                    .Select(row => row.Token).ToListAsync()).ToHashSet();
                foreach (var token in tokens.Where(token => !existing.Contains(token)))
                    db.WorkSeenAcknowledgements.Add(new WorkSeenAcknowledgement { RecipientId = access.Actor.Id, Token = token,
                        AcknowledgedAt = now, Provenance = "Opened", RequestId = requestId });
            }
            preferences.MyWorkSeenRevision++;
            var response = new WorkSeenCommandResult(requestId, now, preferences.MyWorkSeenRevision, preferences.MyWorkFirstVisitAt,
                preferences.MyWorkPreviousVisitAt, preferences.MyWorkLastVisitAt, preferences.MyWorkLastMarkAllAt);
            db.WorkSeenCommandReceipts.Add(new WorkSeenCommandReceipt { UserId = access.Actor.Id, RequestId = requestId, Action = action,
                PayloadHash = hash, ResponseJson = JsonSerializer.Serialize(response, JsonOpts.Web), CommittedAt = now });
            await db.SaveChangesAsync();
            return response;
        });
    }
}
