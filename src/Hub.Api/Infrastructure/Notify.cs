using System.Collections.Immutable;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public sealed record NotifyItem(Guid? ProjectId, string? ItemType, Guid? ItemId, string? ItemKey, string? Link,
    string? ProjectNumber = null, NoticeRawSource? NoticeSource = null);

/// Creates personal notifications and queues immediate emails (§17). Rules applied here, once, for every
/// event: no self-notifications, inactive people get nothing, Muted follows keep only direct assignments
/// and mentions, per-event channel preferences, 5-minute collapse, 24-hour email de-duplication, and
/// assignment notices held while a project is in Setup (§17.1, §17.4, §17.5, §12.18).
public sealed class Notifier(HubDb db, AuditContext audit, SettingsStore store, TimeProvider clock, IConfiguration cfg)
{
    internal sealed class PendingCapture(NoticeCaptureBuffer buffer)
    {
        internal readonly NoticeCaptureBuffer Buffer = buffer;
        internal readonly List<NoticeRawCall> Calls = [];
        internal readonly List<(Guid UserId, NoticePolicyChange Change)> PolicyChanges = [];
        internal bool Sealed;
    }

    NoticeRawCall RawCall(int ordinal, string eventType, Guid[] recipients, NotifyItem item,
        string title, string? body, Guid? actor, IReadOnlyCollection<Guid>? assignedTo)
    {
        if (item.ItemType == "PlanningEntry" && item.NoticeSource is null)
            throw new InvalidOperationException("Enhanced planning notices require their complete canonical source scope before capture.");
        var source = item.NoticeSource ?? new NoticeRawSource(NoticeSourceKind.Work, item.ProjectId,
            item.ItemType, item.ItemId, item.ItemKey, item.Link,
            new NoticeRequiredScope(item.ProjectId is { } project ? [project] : [], []), "Immediate",
            new NoticeAppReference(item.ItemType, item.ItemId, item.ItemKey, item.Link));
        var baseUrl = (cfg["Email:BaseUrl"] ?? "").TrimEnd('/');
        var subject = item.ItemKey is null ? title : $"[{item.ItemKey}] {title}";
        var link = source.LinkPath is null ? "" : $"\n\n{Text.Get("email.open")}: {baseUrl}{source.LinkPath}";
        return new(ordinal, audit.CorrelationId, eventType, recipients.ToImmutableArray(), actor,
            assignedTo?.ToImmutableArray(), source,
            new(title, body, subject, $"{title}{(body is null ? "" : "\n\n" + body)}{link}\n\n{Text.Get("email.footer")}", null));
    }

    void BufferOrdinary(string eventType, Guid[] recipients, NotifyItem item, string title,
        string? body, Guid? actor, IReadOnlyCollection<Guid>? assignedTo)
    {
        var pending = db.PendingNoticeCapture ??= new(NotificationDelivery.BeginCapture(db, audit.CorrelationId));
        if (pending.Sealed) throw new InvalidOperationException("A notice arrived after its complete recipient set was sealed.");
        var call = RawCall(checked(pending.Calls.Count + 1), eventType, recipients, item, title, body, actor, assignedTo);
        NotificationDelivery.Buffer(pending.Buffer, call);
        pending.Calls.Add(call);
    }

    void BufferBulk(TaskBulkCapture capture, string eventType, Guid[] recipients, NotifyItem item,
        string title, string? body, Guid? actor, IReadOnlyCollection<Guid>? assignedTo)
    {
        if (capture.Staged) throw new InvalidOperationException("Task bulk notices arrived after summary staging.");
        var pending = capture.Enhanced ??= new(NotificationDelivery.BeginCapture(db, capture.CommandId, NoticeCaptureMode.TaskBulk));
        if (pending.Sealed) throw new InvalidOperationException("A task bulk recipient set is already sealed.");
        var call = RawCall(checked(pending.Calls.Count + 1), eventType, recipients, item, title, body, actor, assignedTo);
        NotificationDelivery.Buffer(pending.Buffer, call);
        pending.Calls.Add(call);
    }

    internal void ObservePlanningWithdrawal(Guid userId, Guid entryId, Guid? sourceProjectId)
    {
        var pending = db.PendingNoticeCapture ??= new(NotificationDelivery.BeginCapture(db, audit.CorrelationId));
        if (pending.Sealed) throw new InvalidOperationException("A planning withdrawal arrived after its recipient set was sealed.");
        pending.PolicyChanges.Add((userId, new(NoticePolicyChangeKind.PlanningWithdrawal,
            ProjectId: sourceProjectId, PlanningEntryId: entryId)));
    }

    internal async Task FlushPending(CancellationToken ct = default)
    {
        var pending = db.PendingNoticeCapture;
        if (pending is null) return;
        var session = await LockPending(pending, ct);
        foreach (var recipient in pending.PolicyChanges.GroupBy(value => value.UserId)
            .OrderBy(group => group.Key.ToString("D"), StringComparer.Ordinal))
        {
            // All keys are already held as one batch. This cannot introduce a new recipient lock.
            var context = await NotificationDelivery.LockRecipientAsync(db, recipient.Key, ct);
            foreach (var change in recipient.Select(value => value.Change).Distinct())
                await NotificationDelivery.ObservePolicyChangeAsync(context, change, clock.GetUtcNow(), ct);
        }
        if (pending.PolicyChanges.Count > 0)
        {
            DemandNoticeOnlyChanges();
            await db.SaveChangesAsync(ct);
        }
        if (session is null) return; // Retained policy observations are valid while the feature is off.
        var now = clock.GetUtcNow();
        var evaluation = await NotificationDelivery.EvaluateCaptureAsync(session, now, ct);
        var bindings = await StageAppRows(pending.Calls, evaluation, now, ct);
        await NotificationDelivery.StageOrdinaryAsync(session, evaluation, bindings, now, ct);
        DemandNoticeOnlyChanges();
        await db.SaveChangesAsync(ct);
    }

    async Task<NoticeCaptureSession?> LockPending(PendingCapture pending, CancellationToken ct)
    {
        DemandNoticeOnlyChanges();
        if (db.Database.CurrentTransaction is null || pending.Sealed)
            throw new InvalidOperationException("A command's notice capture is flushed once by its actual transaction owner.");
        pending.Sealed = true;
        var projects = pending.Calls.SelectMany(call => call.Source.Scope.ProjectIds
                .Concat(call.Source.ProjectId is { } id ? new[] { id } : []))
            .Concat(pending.PolicyChanges.Select(value => value.Change.ProjectId).OfType<Guid>())
            .Distinct().Order().ToImmutableArray();
        foreach (var project in projects) await Coordination.Lock(db, project);
        foreach (var task in pending.Calls.Where(call => call.Source.ItemType == ItemType.Task && call.Source.ItemId != null)
            .Select(call => call.Source.ItemId!.Value).Distinct().Order())
            await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM hub.task WHERE id={task} FOR UPDATE").ToListAsync(ct);
        var recipients = pending.Calls.SelectMany(call => call.RecipientIds)
            .Concat(pending.PolicyChanges.Select(value => value.UserId));
        await NoticeRecipientLock.AcquireMany(db, recipients, ct);
        return pending.Calls.Count == 0 ? null : await NotificationDelivery.LockCaptureAsync(pending.Buffer, projects, ct);
    }

    void DemandNoticeOnlyChanges()
    {
        db.ChangeTracker.DetectChanges();
        if (db.ChangeTracker.Entries().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
            && entry.Entity is not (Notification or EmailMessage or NoticeDeliveryIntent or NoticeDeliverySlot)))
            throw new InvalidOperationException("Canonical business saves must finish before notice recipients are sealed.");
    }

    async Task<ImmutableArray<NoticeAppBinding>> StageAppRows(IReadOnlyCollection<NoticeRawCall> calls,
        NoticeCaptureEvaluation evaluation, DateTimeOffset now, CancellationToken ct)
    {
        var byOrdinal = calls.ToDictionary(call => call.CallOrdinal);
        var bindings = ImmutableArray.CreateBuilder<NoticeAppBinding>();
        foreach (var decision in evaluation.Decisions.OrderBy(value => value.CallOrdinal).ThenBy(value => value.UserId))
        {
            if (!decision.AppEligible) continue;
            if (!byOrdinal.TryGetValue(decision.CallOrdinal, out var call) || !call.RecipientIds.Contains(decision.UserId))
                throw new InvalidOperationException("A notice app decision must belong to its canonical raw call.");
            var source = call.Source;
            var collapseKey = $"{call.EventType}:{source.ItemId}:{call.ActualActorId}";
            var since = now.AddMinutes(-5);
            var row = db.Notifications.Local.Where(value => value.UserId == decision.UserId && value.CollapseKey == collapseKey
                    && value.ReadAt == null && value.UpdatedAt >= since)
                .OrderByDescending(value => value.UpdatedAt).FirstOrDefault()
                ?? await db.Notifications.Where(value => value.UserId == decision.UserId && value.CollapseKey == collapseKey
                    && value.ReadAt == null && value.UpdatedAt >= since).OrderByDescending(value => value.UpdatedAt).FirstOrDefaultAsync(ct);
            if (row is null)
            {
                row = new Notification
                {
                    UserId = decision.UserId, EventType = call.EventType, ProjectId = source.ProjectId,
                    ItemType = source.AppReference.ItemType, ItemId = source.AppReference.ItemId,
                    ItemKey = source.AppReference.ItemKey, LinkPath = source.AppReference.LinkPath,
                    RequiredProjectIds = source.Scope.ProjectIds.IsEmpty ? null : source.Scope.ProjectIds.ToArray(),
                    Title = call.Content.AppTitle, Body = call.Content.AppBody, CollapseKey = collapseKey,
                    ActorUserId = call.ActualActorId, CorrelationId = call.CorrelationId, CreatedAt = now, UpdatedAt = now,
                };
                db.Notifications.Add(row);
            }
            else if (row.CorrelationId != call.CorrelationId)
            {
                row.Count++;
                row.Title = Text.Get("notify.collapsed", call.Content.AppTitle, row.Count);
                row.UpdatedAt = now; row.CorrelationId = call.CorrelationId;
            }
            if (source.Kind is NoticeSourceKind.PlanningEntry or NoticeSourceKind.PlanningWithdrawal or NoticeSourceKind.PlanningGroup)
            {
                row.Body = call.Content.AppBody; row.ItemType = source.AppReference.ItemType;
                row.ItemId = source.AppReference.ItemId; row.ItemKey = source.AppReference.ItemKey;
                row.LinkPath = source.AppReference.LinkPath;
            }
            bindings.Add(new(decision.UserId, call.CallOrdinal, row.Id, row.Count));
        }
        return bindings.ToImmutable();
    }

    TaskBulkCapture? taskBulk { get => db.TaskBulkNotifications; set => db.TaskBulkNotifications = value; }
    internal sealed record CapturedTaskNotice(Guid RecipientId, string EventType, Guid ProjectId, Guid? TaskId, bool App, bool Email, string EmailAddress);
    public sealed record TaskBulkNoticeReceipt(Guid[] NotificationIds, Guid[] EmailIds);

    /// Captures canonical calls in this scoped notifier; it never mutes another request.
    public sealed class TaskBulkCapture : IDisposable
    {
        readonly Notifier owner;
        internal readonly Guid CommandId;
        internal readonly Guid ActorId;
        internal readonly List<CapturedTaskNotice> notices = [];
        internal PendingCapture? Enhanced;
        internal bool Staged;
        internal TaskBulkCapture(Notifier owner, Guid commandId, Guid actorId)
        { this.owner = owner; CommandId = commandId; ActorId = actorId; }
        public void Dispose() { if (ReferenceEquals(owner.taskBulk, this)) owner.taskBulk = null; }
    }

    public TaskBulkCapture BeginTaskBulkCapture(Guid commandId, Guid originalActorId)
    {
        if (taskBulk is not null || commandId == Guid.Empty || originalActorId == Guid.Empty)
            throw new InvalidOperationException("A task bulk notification scope must be unique and identified.");
        return taskBulk = new TaskBulkCapture(this, commandId, originalActorId);
    }

    public async Task<TaskBulkNoticeReceipt> StageTaskBulkSummaries(TaskBulkCapture capture, IReadOnlyCollection<Guid> changedTaskIds)
    {
        if (!ReferenceEquals(taskBulk, capture) || capture.Staged || db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Task bulk summaries must be staged once inside their command transaction.");
        capture.Staged = true;
        if (capture.Enhanced is { } enhanced)
            return await StageEnhancedBulk(capture, enhanced, changedTaskIds);
        var changed = await db.Tasks.Where(task => changedTaskIds.Contains(task.Id)).Select(task => new { task.Id, task.ProjectId }).ToListAsync();
        var projects = changed.Select(task => task.ProjectId).ToHashSet();
        var accepted = capture.notices.Where(notice => projects.Contains(notice.ProjectId)
            && (notice.TaskId is null || changed.Any(task => task.Id == notice.TaskId))).ToList();
        var notificationIds = new List<Guid>();
        var emailIds = new List<Guid>();
        var now = clock.GetUtcNow();
        var baseUrl = (cfg["Email:BaseUrl"] ?? "").TrimEnd('/');
        foreach (var recipient in accepted.GroupBy(notice => notice.RecipientId))
        {
            foreach (var channel in new[] { "App", "Email" })
            {
                var included = recipient.Where(notice => channel == "App" ? notice.App : notice.Email).ToList();
                if (included.Count == 0) continue;
                var required = included.Select(notice => notice.ProjectId).Distinct().Order().ToArray();
                if (required.Length == 0) throw new InvalidOperationException("A task summary requires project scope.");
                // Membership can be staged in this same transaction; Send already applied its canonical access check.
                var count = changed.Count(task => included.Any(notice => notice.TaskId == task.Id
                    || notice.TaskId is null && notice.ProjectId == task.ProjectId));
                var title = Text.Get("notify.task_bulk_summary", count);
                var eventType = included.Select(notice => notice.EventType).Order(StringComparer.Ordinal).First();
                var identity = $"task-bulk:{capture.CommandId}:{recipient.Key}:{channel}";
                if (channel == "App")
                {
                    var row = new Notification
                    {
                        UserId = recipient.Key, EventType = eventType, RequiredProjectIds = required,
                        ItemType = "TaskBulkCommand", ItemId = capture.CommandId, Title = title, LinkPath = "/tasks",
                        CollapseKey = identity, ActorUserId = capture.ActorId, CorrelationId = capture.CommandId,
                        CreatedAt = now, UpdatedAt = now,
                    };
                    db.Notifications.Add(row); notificationIds.Add(row.Id);
                }
                else
                {
                    var row = new EmailMessage
                    {
                        UserId = recipient.Key, ToAddress = included[0].EmailAddress, Subject = title,
                        BodyText = $"{title}\n\n{Text.Get("email.open")}: {baseUrl}/tasks\n\n{Text.Get("email.footer")}",
                        Kind = "Immediate", DedupKey = identity, RequiredProjectIds = required,
                        CreatedAt = now, NextAttemptAt = now,
                    };
                    db.Emails.Add(row); emailIds.Add(row.Id);
                }
            }
        }
        return new TaskBulkNoticeReceipt([.. notificationIds], [.. emailIds]);
    }

    async Task<TaskBulkNoticeReceipt> StageEnhancedBulk(TaskBulkCapture capture, PendingCapture pending,
        IReadOnlyCollection<Guid> changedTaskIds)
    {
        var changed = await db.Tasks.Where(task => changedTaskIds.Contains(task.Id))
            .Select(task => new { task.Id, task.ProjectId }).ToListAsync();
        if (changed.Count != changedTaskIds.Distinct().Count())
            throw new InvalidOperationException("A bulk notice requires every actual changed task/project pair.");
        var session = await LockPending(pending, default)
            ?? throw new InvalidOperationException("An enhanced bulk summary must have actual canonical source calls.");
        var now = clock.GetUtcNow();
        var evaluation = await NotificationDelivery.EvaluateCaptureAsync(session, now);
        var calls = pending.Calls.ToDictionary(call => call.CallOrdinal);
        var appBindings = ImmutableArray.CreateBuilder<NoticeAppBinding>();
        var notificationIds = new List<Guid>();
        foreach (var recipient in evaluation.Decisions.Where(decision => decision.AppEligible)
            .GroupBy(decision => decision.UserId).OrderBy(group => group.Key.ToString("D"), StringComparer.Ordinal))
        {
            var included = recipient.Select(decision => calls.GetValueOrDefault(decision.CallOrdinal)
                ?? throw new InvalidOperationException("A bulk app decision must identify its actual canonical call.")).ToArray();
            if (included.Any(call => !call.RecipientIds.Contains(recipient.Key)))
                throw new InvalidOperationException("A bulk app decision cannot substitute a recipient.");
            var covered = changed.Where(task => included.Any(call => call.Source.ProjectId == task.ProjectId
                && (call.Source.ItemType != ItemType.Task || call.Source.ItemId == task.Id))).Select(task => task.Id).Distinct().ToArray();
            if (covered.Length == 0) continue;
            var projects = included.SelectMany(call => call.Source.Scope.ProjectIds).Distinct().Order().ToArray();
            if (projects.Length == 0) throw new InvalidOperationException("A task summary requires its original project scope.");
            var row = new Notification
            {
                UserId = recipient.Key, EventType = included.Select(call => call.EventType).Order(StringComparer.Ordinal).First(),
                RequiredProjectIds = projects, ItemType = "TaskBulkCommand", ItemId = capture.CommandId,
                Title = Text.Get("notify.task_bulk_summary", covered.Length), LinkPath = "/tasks",
                CollapseKey = $"task-bulk:{capture.CommandId}:{recipient.Key}:App", ActorUserId = capture.ActorId,
                CorrelationId = capture.CommandId, CreatedAt = now, UpdatedAt = now,
            };
            db.Notifications.Add(row); notificationIds.Add(row.Id);
            appBindings.Add(new(recipient.Key, null, row.Id, row.Count));
        }
        var basis = new NoticeBulkCaptureBasis(capture.CommandId, capture.ActorId,
            changed.Select(task => new NoticeChangedTask(task.Id, task.ProjectId)).ToImmutableArray());
        var staged = await NotificationDelivery.StageBulkAsync(session, evaluation, basis, appBindings.ToImmutable(), now);
        DemandNoticeOnlyChanges();
        return new([.. notificationIds], staged.EmailIds.ToArray());
    }

    static readonly HashSet<string> HeldInSetup = [NotificationEvents.TaskAssigned, NotificationEvents.ReviewerSet, NotificationEvents.DeliverableOwned];
    internal static bool HeldWhileSetup(string eventType) => HeldInSetup.Contains(eventType);

    /// <param name="assignedTo">For a direct-assignment event sent to several people, the ones the work is assigned to;
    /// only they bypass a Muted follow (§12.18). Null keeps the bypass for every recipient.</param>
    public async Task Send(string eventType, IEnumerable<Guid?> recipients, NotifyItem item, string title, string? body = null, Guid? actorOverride = null, IReadOnlyCollection<Guid>? assignedTo = null)
    {
        var actor = actorOverride ?? audit.ActorId;
        var ids = recipients.OfType<Guid>().Where(r => r != actor).Distinct().ToList();
        if (ids.Count == 0) return;
        var s = await store.Get(db);
        if (s.IsFlagOn("people_screens") || db.PendingNoticeCapture is { Calls.Count: > 0 } || taskBulk?.Enhanced is not null)
        {
            if (taskBulk is { } bulk) BufferBulk(bulk, eventType, ids.ToArray(), item, title, body, actor, assignedTo);
            else BufferOrdinary(eventType, ids.ToArray(), item, title, body, actor, assignedTo);
            return;
        }
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
        var defaults = s.NotificationDefaults.GetValueOrDefault(eventType) ?? new Channels(def.App, def.Email);
        var now = clock.GetUtcNow();
        var collapseSince = now.AddMinutes(-5);
        var dedupSince = now.AddHours(-24);
        var baseUrl = (cfg["Email:BaseUrl"] ?? "").TrimEnd('/');

        foreach (var u in users)
        {
            // Project creation emits membership/lead notices before the new project is flushed.
            // Existing projects must pass the current-access check; the queued email is still
            // locked to the project and rechecked by EmailJob before delivery.
            if (item.ProjectId is { } projectId && await db.Projects.AnyAsync(p => p.Id == projectId)
                && !db.ProjectMembers.Local.Any(m => m.ProjectId == projectId && m.UserId == u.Id && m.RemovedAt == null
                    && db.Entry(m).State == EntityState.Added)
                && !await EmailProjectAccess.Allowed(db, u.Id, [projectId])) continue;
            if (muted.Contains(u.Id) && !(def.DirectAssignment && (assignedTo is null || assignedTo.Contains(u.Id)))) continue;
            var pref = prefs.GetValueOrDefault(u.Id);
            var app = pref?.InApp ?? defaults.App;
            var mail = pref?.Email ?? defaults.Email;
            if (taskBulk is { } capture)
            {
                if (capture.Staged) throw new InvalidOperationException("Task bulk notices arrived after summary staging.");
                if (item.ProjectId is not { } scope) throw new InvalidOperationException("Task bulk notices require project scope.");
                capture.notices.Add(new(u.Id, eventType, scope, item.ItemType == ItemType.Task ? item.ItemId : null,
                    app, mail && !string.IsNullOrEmpty(u.Email), u.Email));
                continue;
            }
            if (app)
            {
                var collapse = $"{eventType}:{item.ItemId}:{actor}";
                var existing = db.Notifications.Local.Where(n => n.UserId == u.Id && n.CollapseKey == collapse && n.ReadAt == null && n.UpdatedAt >= collapseSince)
                    .OrderByDescending(n => n.UpdatedAt).FirstOrDefault()
                    ?? await db.Notifications.Where(n => n.UserId == u.Id && n.CollapseKey == collapse && n.ReadAt == null && n.UpdatedAt >= collapseSince)
                        .OrderByDescending(n => n.UpdatedAt).FirstOrDefaultAsync();
                if (existing is not null)
                {
                    // Source/review invalidation can reach the same recipient twice in one command.
                    if (existing.CorrelationId != audit.CorrelationId)
                    {
                        existing.Count++;
                        existing.Title = Text.Get("notify.collapsed", title, existing.Count);
                        existing.UpdatedAt = now;
                        existing.CorrelationId = audit.CorrelationId;
                    }
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
                if (db.Emails.Local.Any(e => e.DedupKey == dedup && e.CreatedAt >= dedupSince)
                    || await db.Emails.AnyAsync(e => e.DedupKey == dedup && e.CreatedAt >= dedupSince)) continue;
                var subject = item.ItemKey is null ? title : $"[{item.ItemKey}] {title}";
                var link = item.Link is null ? "" : $"\n\n{Text.Get("email.open")}: {baseUrl}{item.Link}";
                db.Emails.Add(new EmailMessage
                {
                    UserId = u.Id, ToAddress = u.Email, Subject = subject, Kind = "Immediate", DedupKey = dedup, CreatedAt = now, NextAttemptAt = now,
                    RequiredProjectIds = item.ProjectId is { } scopedProject ? [scopedProject] : [],
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
