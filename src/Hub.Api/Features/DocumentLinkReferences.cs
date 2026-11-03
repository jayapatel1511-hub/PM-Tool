using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record DocumentLinkReferenceInput(string? SourceSystem = null, string? StableSourceId = null, string? DeclaredRevision = null)
{
    // Keep unknown input until the enabled validator can reject it, including forged provenance/target fields.
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; init; }
}
public sealed record DocumentLinkReferenceUser(Guid Id, string DisplayName, bool IsActive);
public sealed record DocumentLinkReferenceValue(string SourceSystem, string? StableSourceId, string? DeclaredRevision,
    Guid RegisteredBy, DocumentLinkReferenceUser? RegisteredByUser, DateTimeOffset RegisteredAt, string RegistrationKind = "Manual");
public sealed record DocumentLinkReferenceTarget(Guid Id, string Key, string Name, string Status, string? CurrentRevision, int RowVersion);
public sealed record DocumentLinkReferenceComparison(string State, string? DeclaredRevision = null, string? CurrentRevision = null);
public sealed record DocumentLinkReferenceContext(DocumentLinkReferenceValue? Reference, DocumentLinkReferenceTarget? Target,
    DocumentLinkReferenceComparison Comparison);

/// Manual reference metadata only. No network requests, source registration, permission grant or business-state writes.
public static class DocumentLinkReferences
{
    public const string Flag = "people_screens";

    public static async Task Apply(HubDb db, DocumentLink link, DocumentLinkReferenceInput input, Guid actorId, DateTimeOffset now)
    {
        Check.That(input.Extra is not { Count: > 0 }, "reference", "error.invalid");
        static void TextInput(string? value, string field) => Check.That(value is null || !value.Any(ch => char.IsControl(ch) || ch is '\u2028' or '\u2029'), field, "error.invalid");
        TextInput(input.SourceSystem, "reference.sourceSystem"); TextInput(input.StableSourceId, "reference.stableSourceId"); TextInput(input.DeclaredRevision, "reference.declaredRevision");
        var source = Check.Required(input.SourceSystem, "reference.sourceSystem", 200);
        var stable = Check.Optional(input.StableSourceId, "reference.stableSourceId", 200);
        var revision = Check.Optional(input.DeclaredRevision, "reference.declaredRevision", 50);
        Check.That(actorId != Guid.Empty && await db.Users.AnyAsync(user => user.Id == actorId && user.IsActive), "reference", "error.invalid");
        Guid? target = null;
        switch (link.ItemType)
        {
            case ItemType.Project:
                if (link.ItemId != link.ProjectId || !await db.Projects.AnyAsync(project => project.Id == link.ProjectId)) throw ApiException.NotFound();
                break;
            case ItemType.Deliverable:
                if (!await db.Deliverables.AnyAsync(work => work.Id == link.ItemId && work.ProjectId == link.ProjectId)) throw ApiException.NotFound();
                target = link.ItemId;
                break;
            case ItemType.Task:
                var task = await db.Tasks.AsNoTracking().Where(work => work.Id == link.ItemId && work.ProjectId == link.ProjectId)
                    .Select(work => new { work.DeliverableId }).SingleOrDefaultAsync() ?? throw ApiException.NotFound();
                if (task.DeliverableId is { } parent)
                {
                    Check.That(await db.Deliverables.AnyAsync(work => work.Id == parent && work.ProjectId == link.ProjectId), "reference", "linkReference.targetUnavailable");
                    target = parent;
                }
                break;
            default:
                throw ApiException.NotFound();
        }
        // Assign only after every input and actual relationship has been checked. Original link authorship never changes.
        link.SourceSystem = source; link.StableSourceId = stable; link.DeclaredRevision = revision;
        link.ReferenceRegisteredBy = actorId; link.ReferenceRegisteredAt = now.ToUniversalTime(); link.ReferenceDeliverableId = target;
    }

    public static void Clear(DocumentLink link)
    {
        link.SourceSystem = null; link.StableSourceId = null; link.DeclaredRevision = null;
        link.ReferenceRegisteredBy = null; link.ReferenceRegisteredAt = null; link.ReferenceDeliverableId = null;
    }

    public static async Task<Dictionary<Guid, DocumentLinkReferenceContext>> ReadContexts(HubDb db,
        IReadOnlyCollection<DocumentLink> links, IReadOnlyCollection<Guid> readableProjectIds)
    {
        if (links.Count == 0 || readableProjectIds.Count == 0) return [];
        var allowed = readableProjectIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var projects = (await db.Projects.AsNoTracking().Where(project => allowed.Contains(project.Id)).Select(project => project.Id).ToArrayAsync()).ToHashSet();
        var shown = links.Where(link => link.DeletedAt is null && projects.Contains(link.ProjectId)).DistinctBy(link => link.Id).ToArray();
        if (shown.Length == 0) return [];
        var taskIds = shown.Where(link => link.ItemType == ItemType.Task).Select(link => link.ItemId).Distinct().ToArray();
        var tasks = await db.Tasks.AsNoTracking().Where(task => taskIds.Contains(task.Id) && allowed.Contains(task.ProjectId))
            .Select(task => new { task.Id, task.ProjectId, task.DeliverableId }).ToDictionaryAsync(task => task.Id);
        var targetIds = shown.Where(link => link.ItemType == ItemType.Deliverable).Select(link => link.ItemId)
            .Concat(shown.Select(link => link.ReferenceDeliverableId).OfType<Guid>()).Concat(tasks.Values.Select(task => task.DeliverableId).OfType<Guid>()).Distinct().ToArray();
        var targets = await db.Deliverables.AsNoTracking().Where(work => targetIds.Contains(work.Id) && allowed.Contains(work.ProjectId))
            .Select(work => new { work.Id, work.ProjectId, work.Key, work.Name, work.Status, work.Revision, work.RowVersion }).ToDictionaryAsync(work => work.Id);
        var registrars = shown.Select(link => link.ReferenceRegisteredBy).OfType<Guid>().Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(user => registrars.Contains(user.Id))
            .Select(user => new DocumentLinkReferenceUser(user.Id, user.DisplayName, user.IsActive)).ToDictionaryAsync(user => user.Id);
        static string? Known(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        DocumentLinkReferenceTarget? Target(Guid? id, Guid projectId) => id is { } key && targets.TryGetValue(key, out var row) && row.ProjectId == projectId
            ? new(row.Id, row.Key, row.Name, row.Status, Known(row.Revision), row.RowVersion) : null;
        var contexts = new Dictionary<Guid, DocumentLinkReferenceContext>();
        foreach (var link in shown)
        {
            Guid? current = null; var validOwner = false;
            switch (link.ItemType)
            {
                case ItemType.Project:
                    validOwner = link.ItemId == link.ProjectId;
                    break;
                case ItemType.Deliverable:
                    validOwner = Target(link.ItemId, link.ProjectId) is not null; current = link.ItemId;
                    break;
                case ItemType.Task:
                    if (tasks.TryGetValue(link.ItemId, out var task) && task.ProjectId == link.ProjectId)
                    { validOwner = true; current = task.DeliverableId; }
                    break;
            }
            if (!validOwner)
            { contexts[link.Id] = new(null, null, new("TargetUnavailable")); continue; }
            var registered = Known(link.SourceSystem) is { } && link.ReferenceRegisteredBy is { } actor && actor != Guid.Empty && link.ReferenceRegisteredAt.HasValue;
            var value = registered ? new DocumentLinkReferenceValue(link.SourceSystem!.Trim(), Known(link.StableSourceId), Known(link.DeclaredRevision),
                link.ReferenceRegisteredBy!.Value, people.GetValueOrDefault(link.ReferenceRegisteredBy.Value), link.ReferenceRegisteredAt!.Value) : null;
            var declared = value?.DeclaredRevision;
            var changed = registered && link.ItemType == ItemType.Task && current != link.ReferenceDeliverableId;
            var coherent = !registered || current == link.ReferenceDeliverableId;
            var target = coherent && !changed ? Target(current, link.ProjectId) : null;
            DocumentLinkReferenceComparison comparison;
            if (declared is null) comparison = new("NotRecorded");
            // This must precede NoDeliverable: a standalone task gaining a parent is also a changed comparison target.
            else if (changed) { target = null; comparison = new("TargetChanged", declared); }
            else if (link.ReferenceDeliverableId is null && link.ItemType is ItemType.Project or ItemType.Task)
            { target = null; comparison = new("NoDeliverable", declared); }
            else if (target is null) comparison = new("TargetUnavailable", declared);
            else if (target.CurrentRevision is null) comparison = new("CurrentNotRecorded", declared);
            else comparison = new(string.Equals(declared, target.CurrentRevision, StringComparison.Ordinal) ? "Matches" : "Different", declared, target.CurrentRevision);
            contexts[link.Id] = new(value, target, comparison);
        }
        return contexts;
    }
}
