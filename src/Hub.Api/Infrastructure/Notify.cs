using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public sealed record NotifyItem(Guid? ProjectId, string? ItemType, Guid? ItemId, string? ItemKey, string? Link, string? ProjectNumber = null);

/// Creates personal notifications and queues immediate emails (§17). Rules applied here, once, for every
/// event: no self-notifications, inactive people get nothing, Muted follows keep only direct assignments
/// and mentions, per-event channel preferences, 5-minute collapse, 24-hour email de-duplication, and
/// assignment notices held while a project is in Setup (§17.1, §17.4, §17.5, §12.18).
public sealed class Notifier(HubDb db, AuditContext audit, SettingsStore store, TimeProvider clock, IConfiguration cfg)
{
    static readonly HashSet<string> HeldInSetup = [NotificationEvents.TaskAssigned, NotificationEvents.ReviewerSet, NotificationEvents.DeliverableOwned];

    public async Task Send(string eventType, IEnumerable<Guid?> recipients, NotifyItem item, string title, string? body = null, Guid? actorOverride = null)
    {
        var actor = actorOverride ?? audit.ActorId;
        var ids = recipients.OfType<Guid>().Where(r => r != actor).Distinct().ToList();
        if (ids.Count == 0) return;
        var def = NotificationEvents.Get(eventType);
        string? projectStatus = null;
        if (item.ProjectId is { } pid)
        {
            projectStatus = await db.Projects.Where(p => p.Id == pid).Select(p => p.Status).FirstOrDefaultAsync();
            if (projectStatus is ProjectStatus.Archived or ProjectStatus.Cancelled) return;
            if (projectStatus == ProjectStatus.Setup && HeldInSetup.Contains(eventType)) return; // batched on activation (§17.5)
        }
        var users = await db.Users.Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => new { u.Id, u.Email }).ToListAsync();
        var muted = item.ProjectId is { } p2
            ? (await db.Follows.Where(f => f.ProjectId == p2 && ids.Contains(f.UserId) && f.Level == FollowLevel.Muted).Select(f => f.UserId).ToListAsync()).ToHashSet()
            : [];
        var prefs = await db.NotificationPreferences.Where(n => ids.Contains(n.UserId) && n.EventType == eventType).ToDictionaryAsync(n => n.UserId);
        var s = await store.Get(db);
        var defaults = s.NotificationDefaults.GetValueOrDefault(eventType) ?? new Channels(def.App, def.Email);
        var now = clock.GetUtcNow();
        var collapseSince = now.AddMinutes(-5);
        var dedupSince = now.AddHours(-24);
        var baseUrl = (cfg["Email:BaseUrl"] ?? "").TrimEnd('/');

        foreach (var u in users)
        {
            if (NotificationEvents.ProjectScoped.Contains(eventType) && item.ProjectId is { } handoffProject
                && !await EmailProjectAccess.Allowed(db, u.Id, [handoffProject])) continue;
            if (muted.Contains(u.Id) && !def.DirectAssignment) continue;
            var pref = prefs.GetValueOrDefault(u.Id);
            var app = pref?.InApp ?? defaults.App;
            var mail = pref?.Email ?? defaults.Email;
            if (app)
            {
                var collapse = $"{eventType}:{item.ItemId}:{actor}";
                var existing = await db.Notifications.Where(n => n.UserId == u.Id && n.CollapseKey == collapse && n.ReadAt == null && n.UpdatedAt >= collapseSince)
                    .OrderByDescending(n => n.UpdatedAt).FirstOrDefaultAsync();
                if (existing is not null)
                {
                    existing.Count++;
                    existing.Title = Text.Get("notify.collapsed", title, existing.Count);
                    existing.UpdatedAt = now;
                    existing.CorrelationId = audit.CorrelationId;
                }
                else
                    db.Notifications.Add(new Notification
                    {
                        UserId = u.Id, EventType = eventType, ProjectId = item.ProjectId, ItemType = item.ItemType, ItemId = item.ItemId, ItemKey = item.ItemKey,
                        Title = title, Body = body, LinkPath = item.Link, CollapseKey = collapse, ActorUserId = actor, CorrelationId = audit.CorrelationId,
                        CreatedAt = now, UpdatedAt = now,
                    });
            }
            if (mail && !string.IsNullOrEmpty(u.Email))
            {
                var dedup = $"{eventType}:{item.ItemId}:{u.Id}";
                if (await db.Emails.AnyAsync(e => e.DedupKey == dedup && e.CreatedAt >= dedupSince)) continue;
                var subject = item.ItemKey is null ? title : $"[{item.ItemKey}] {title}";
                var link = item.Link is null ? "" : $"\n\n{Text.Get("email.open")}: {baseUrl}{item.Link}";
                db.Emails.Add(new EmailMessage
                {
                    UserId = u.Id, ToAddress = u.Email, Subject = subject, Kind = "Immediate", DedupKey = dedup, CreatedAt = now, NextAttemptAt = now,
                    RequiredProjectIds = NotificationEvents.ProjectScoped.Contains(eventType) && item.ProjectId is { } scopedProject ? [scopedProject] : [],
                    BodyText = $"{title}{(body is null ? "" : "\n\n" + body)}{link}\n\n{Text.Get("email.footer")}",
                });
            }
        }
    }

    public Task Send(string eventType, Guid? recipient, NotifyItem item, string title, string? body = null) => Send(eventType, [recipient], item, title, body);

    /// Display name used in notification sentences; inactive people keep their "(Inactive)" suffix.
    public async Task<string> Name(Guid? userId) => userId is null ? Text.Get("common.system")
        : await db.Users.Where(u => u.Id == userId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefaultAsync() ?? "";

    public async Task<string> ActorName() => await Name(audit.ActorId);
}
