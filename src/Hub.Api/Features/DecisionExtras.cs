using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Register enhancements (§28 item 2, packet 013): linking one decision to many items at once, the project's decision
/// log, and the open decisions owned by the client as a clean export for the client call.
public static class DecisionExtraEndpoints
{
    public sealed record BulkLinkBody(string TargetType, Guid[] TargetIds, string? Relation);

    static readonly Col[] ClientCols =
    [
        new("key", "key", "key"), new("subject", "subject"), new("description", "toDecide"), new("requiredBy", "requiredBy", "date"),
        new("timing", "timing"), new("impact", "impact"), new("status", "status"), new("owner", "owner"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/decisions/{id:guid}/links/bulk", BulkLink);
        api.MapGet("/projects/{id:guid}/decision-log", Log);
        api.MapGet("/projects/{id:guid}/decisions/client-export", ClientExport);
    }

    /// FR-002: every target the caller may change is linked in one save and logged; the rest are reported, not linked.
    static async Task<object> BulkLink(Guid id, BulkLinkBody body, Access access, HubDb db)
    {
        var (d, p, ctx) = await DecisionEndpoints.Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, DecisionEndpoints.Facts(d)));
        var ids = body.TargetIds.Distinct().ToList();
        Check.That(ids.Count is > 0 and <= 500, "targetIds", "error.required");
        var a = access.Actor;
        var linked = new List<object>();
        var skipped = new List<object>();
        var existing = (await db.ItemLinks.Where(x => x.SourceId == d.Id && ids.Contains(x.TargetId)).Select(x => x.TargetId).ToListAsync()).ToHashSet();
        // Each target with its key and whether the caller may change it; items of other projects are not found.
        var targets = body.TargetType switch
        {
            ItemType.Task => await Tasks(db, p.Id, ids, a, ctx),
            ItemType.Deliverable => (await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == p.Id && ids.Contains(x.Id)).ToListAsync())
                .Select(x => (x.Id, x.Key, Permissions.EditDeliverable(a, ctx, new DeliverableFacts(x.ProjectDisciplineId, x.OwnerId, x.ReviewerId)))).ToList(),
            ItemType.Milestone => (await db.Milestones.AsNoTracking().Where(x => x.ProjectId == p.Id && ids.Contains(x.Id)).ToListAsync())
                .Select(x => (x.Id, x.Key, Permissions.ManageMilestones(a, ctx))).ToList(),
            _ => throw ApiException.Invalid("targetType", "error.one_of", string.Join(", ", ItemType.Task, ItemType.Deliverable, ItemType.Milestone)),
        };
        foreach (var missing in ids.Except(targets.Select(x => x.Id))) skipped.Add(new { Id = missing, Key = (string?)null, Reason = Text.Get("decision.other_project") });
        foreach (var (tid, key, allow) in targets)
        {
            if (existing.Contains(tid)) { skipped.Add(new { Id = tid, Key = key, Reason = Text.Get("decision.link_exists") }); continue; }
            if (!allow.Ok) { skipped.Add(new { Id = tid, Key = key, Reason = Text.Get(allow.Why ?? "perm.admin", allow.Arg ?? "") }); continue; }
            await DecisionEndpoints.Link(db, access, p, d, new DecisionEndpoints.LinkInput(body.TargetType, tid, body.Relation));
            linked.Add(new { Id = tid, Key = key });
        }
        await db.SaveChangesAsync();
        return new { Linked = linked, Skipped = skipped };
    }

    static async Task<List<(Guid Id, string Key, Allow Allow)>> Tasks(HubDb db, Guid projectId, List<Guid> ids, Actor a, ProjectContext ctx)
    {
        var list = new List<(Guid, string, Allow)>();
        foreach (var t in await db.Tasks.AsNoTracking().Where(x => x.ProjectId == projectId && ids.Contains(x.Id)).ToListAsync())
            list.Add((t.Id, t.Key, Permissions.EditTask(a, ctx, await TaskEndpoints.Facts(db, t))));
        return list;
    }

    /// FR-003: decided, deferred, cancelled and reopened decisions, newest first, from the activity history, so a decision
    /// deferred twice and then decided shows each step with its text or reason, its dates and who recorded it.
    static async Task<object> Log(Guid id, Access access, HubDb db)
    {
        await access.Project(id, track: false);
        var rows = await db.ActivityLog.AsNoTracking()
            .Where(x => x.ProjectId == id && x.ItemType == ItemType.Decision && (x.Action == "Decided" || x.Action == "Deferred" || x.Action == "Reopened" || x.Action == "StatusChanged"))
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => new { x.ItemId, x.ItemKey, x.ItemName, x.Action, x.Reason, x.OccurredAt, Changes = x.Changes, Actor = db.Users.Where(u => u.Id == x.ActorUserId).Select(u => u.DisplayName).FirstOrDefault() })
            .Take(1000).ToListAsync();
        var ids = rows.Select(r => r.ItemId).OfType<Guid>().Distinct().ToList();
        var owners = await db.Decisions.AsNoTracking().Where(d => ids.Contains(d.Id)).Select(d => new
        {
            d.Id, d.Status,
            Owner = d.OwnerUserId != null ? db.Users.Where(u => u.Id == d.OwnerUserId).Select(u => u.DisplayName).FirstOrDefault()
                : db.ExternalParties.Where(e => e.Id == d.OwnerExternalPartyId).Select(e => e.Name).FirstOrDefault(),
        }).ToDictionaryAsync(d => d.Id);
        var entries = new List<object>();
        foreach (var r in rows)
        {
            var changes = JsonNode.Parse(r.Changes)?.AsArray() ?? [];
            string? New(string field) => changes.FirstOrDefault(c => c?["field"]?.GetValue<string>() == field)?["new"]?.ToString();
            var status = New("Status");
            var kind = r.Action switch
            {
                "Decided" => DecisionStatus.Decided, "Deferred" => DecisionStatus.Deferred, "Reopened" => "Reopened",
                _ => status == DecisionStatus.Cancelled ? DecisionStatus.Cancelled : null,
            };
            if (kind is null) continue;
            var o = r.ItemId is { } did ? owners.GetValueOrDefault(did) : null;
            entries.Add(new
            {
                DecisionId = r.ItemId, r.ItemKey, Subject = r.ItemName, Kind = kind, r.OccurredAt, RecordedBy = r.Actor, Owner = o?.Owner, CurrentStatus = o?.Status,
                Text = kind == DecisionStatus.Decided ? New("DecisionText") : r.Reason, DecisionDate = kind == DecisionStatus.Decided ? New("DecisionDate") : null,
                NewRequiredBy = kind == DecisionStatus.Deferred ? New("RequiredByDate") : null,
            });
        }
        return entries;
    }

    /// FR-001: open decisions owned by the client (or one chosen external party) — subject, what must be decided, when it
    /// is needed, days until or overdue, impact and status, overdue first — and nothing internal: no notes, no comments.
    static async Task<IResult> ClientExport(Guid id, Guid? partyId, string? format, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(id, track: false);
        var today = clock.Today(await store.Get(db));
        var parties = await db.ExternalParties.AsNoTracking().Where(x => x.ProjectId == id && (partyId == null ? x.IsClient : x.Id == partyId))
            .Select(x => new { x.Id, x.Name }).ToListAsync();
        if (partyId is not null && parties.Count == 0) throw ApiException.NotFound();
        var who = parties.Count == 0 ? Text.Get("decision.the_client") : string.Join(", ", parties.Select(x => x.Name));
        var pids = parties.Select(x => x.Id).ToList();
        var open = await db.Decisions.AsNoTracking().Where(d => d.ProjectId == id && d.OwnerExternalPartyId != null && pids.Contains(d.OwnerExternalPartyId.Value)
            && DecisionEndpoints.Open.Contains(d.Status)).ToListAsync();
        if (open.Count == 0) throw ApiException.Rule("nothing_to_export", "decision.none_for_client", null, who); // says so rather than sending an empty file
        var names = parties.ToDictionary(x => x.Id, x => x.Name);
        var rows = open.Select(d => (d, days: d.RequiredByDate.DayNumber - today.DayNumber)).OrderBy(x => x.days < 0 ? 0 : 1).ThenBy(x => x.d.RequiredByDate).ThenBy(x => x.d.Key)
            .Select(x => new
            {
                x.d.Key, x.d.Subject, x.d.Description, RequiredBy = x.d.RequiredByDate,
                Timing = x.days < 0 ? Text.Get("decision.days_overdue", -x.days) : x.days == 0 ? Text.Get("decision.due_today") : Text.Get("decision.days_left", x.days),
                Impact = string.IsNullOrWhiteSpace(x.d.ImpactDescription) ? x.d.ImpactLevel : $"{x.d.ImpactLevel}: {x.d.ImpactDescription}", x.d.Status,
                Owner = names[x.d.OwnerExternalPartyId!.Value],
            });
        var json = JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray();
        return await ExportFile.Send(db, store, format, Text.Get("decision.client_export", who, p.ProjectNumber), ClientCols, json,
            [(Text.Get("param.projectId"), $"{p.ProjectNumber} {p.Name}"), (Text.Get("col.owner"), who)], p.Id, "client-decisions", clock);
    }
}
