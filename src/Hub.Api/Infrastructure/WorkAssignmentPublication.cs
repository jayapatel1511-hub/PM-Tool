using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Hub.Api.Infrastructure;

/// Assignment evidence is built beside the exact canonical activity, never in a preference writer.
public static class WorkAssignmentPublication
{
    static bool Supported(Type type) => type == typeof(WorkTask) || type == typeof(Deliverable) || type == typeof(Decision)
        || type == typeof(MeetingAction) || type == typeof(Risk) || type == typeof(Issue) || type == typeof(Handoff)
        || type == typeof(ChangeNotice) || type == typeof(Project);

    static Guid? Id(object? value) => value is Guid id && id != Guid.Empty ? id : null;
    static IReadOnlyList<(string Role, Guid? Recipient)> Roles(Type type, Func<string, object?> value)
    {
        if (type == typeof(WorkTask)) return [("Assignee", Id(value("AssigneeId"))), ("Reviewer", Id(value("ReviewerId")))];
        if (type == typeof(Deliverable)) return [("Owner", Id(value("OwnerId"))), ("Reviewer", Id(value("ReviewerId")))];
        if (type == typeof(Decision)) return [("InternalOwner", value("OwnerExternalPartyId") is null ? Id(value("OwnerUserId")) : null)];
        if (type == typeof(MeetingAction)) return [("InternalOwner", Equals(value("OwnerType"), ActionOwnerType.User) ? Id(value("OwnerUserId")) : null)];
        if (type == typeof(Handoff)) return [("SendingOwner", Id(value("SendingOwnerId"))), ("ReceivingOwner", Id(value("ReceivingOwnerId")))];
        if (type == typeof(Project)) return [("ProjectManager", Id(value("ProjectManagerId")))];
        return [("Owner", Id(value("OwnerId")))];
    }
    static IReadOnlyList<(string Role, Guid Recipient)> Changes(EntityEntry entry)
    {
        if (!Supported(entry.Metadata.ClrType) || entry.State is not (EntityState.Added or EntityState.Modified)) return [];
        var current = Roles(entry.Metadata.ClrType, field => entry.Property(field).CurrentValue);
        IReadOnlyList<(string Role, Guid? Recipient)> before = entry.State == EntityState.Added ? [] : Roles(entry.Metadata.ClrType, field => entry.Property(field).OriginalValue);
        return current.Where(role => role.Recipient is not null && (entry.State == EntityState.Added
            || before.First(old => old.Role == role.Role).Recipient != role.Recipient))
            .Select(role => (role.Role, role.Recipient!.Value)).ToArray();
    }
    public static bool HasChanges(HubDb db)
    {
        db.ChangeTracker.DetectChanges();
        return db.ChangeTracker.Entries().Any(entry => Changes(entry).Count != 0);
    }
    /// Bound only the canonical object the caller actually read, never a newer replacement read.
    public static WorkSeenReadBound Bound(HubDb db, object canonical)
    {
        var entry = db.Entry(canonical);
        if (!Supported(entry.Metadata.ClrType)) throw new ArgumentException("Unsupported assignment kind.", nameof(canonical));
        var item = (IAuditable)canonical;
        return new(new(item.AuditType, ((Entity)canonical).Id), item.AuditProjectId!.Value, ((IVersioned)canonical).RowVersion,
            Roles(entry.Metadata.ClrType, field => entry.Property(field).CurrentValue).Where(role => role.Recipient is not null)
                .Select(role => new WorkSeenRoleRecipient(role.Role, role.Recipient!.Value)).ToArray());
    }
    public static void Publish(HubDb db, EntityEntry entry, ActivityLog? sourceActivity, DateTimeOffset recordedAt)
    {
        var changes = Changes(entry);
        if (changes.Count == 0) return;
        var entity = (Entity)entry.Entity;
        var canonical = (IAuditable)entry.Entity;
        var version = ((IVersioned)entry.Entity).RowVersion;
        if (sourceActivity is null || sourceActivity.ItemId != entity.Id || sourceActivity.ItemType != canonical.AuditType
            || sourceActivity.ProjectId != canonical.AuditProjectId || version < 0)
            throw new InvalidOperationException("Assignment publication requires the exact canonical source activity and version.");
        foreach (var (role, recipient) in changes)
        {
            var duplicate = db.WorkAssignmentOccurrences.Local.SingleOrDefault(item => item.ItemType == canonical.AuditType
                && item.ItemId == entity.Id && item.Role == role && item.SourceVersion == version);
            if (duplicate is not null)
            {
                if (duplicate.RecipientId != recipient || duplicate.SourceActivityId != sourceActivity.Id)
                    throw new InvalidOperationException("Conflicting assignment publication for one canonical role version.");
                continue;
            }
            db.WorkAssignmentOccurrences.Add(new WorkAssignmentOccurrence {
                RecipientId = recipient, ItemType = canonical.AuditType, ItemId = entity.Id,
                ProjectId = canonical.AuditProjectId ?? throw new InvalidOperationException("Assignment requires a project."),
                Role = role, SourceVersion = version, SourceActivityId = sourceActivity.Id, SourceOccurredAt = sourceActivity.OccurredAt,
                Provenance = "Published", RecordedAt = recordedAt
            });
        }
    }
}
