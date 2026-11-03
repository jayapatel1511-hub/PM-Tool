using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Notification centre, preferences, follows and the Following feed (§13.17, §17.4, §12.18, FR-NOT-01..03, FR-ASG-02/03).
public static class NotificationEndpoints
{
    public sealed record ReadBody(Guid[]? Ids = null, bool? All = null); // one or the other: the page sends only what it means
    public sealed record ChannelBody(bool App, bool Email);
    public sealed record DigestBody(bool Enabled, string? Time = null, string[]? SectionsOff = null, bool? WeeklySummary = null, bool? HoldDigestWhenAway = null); // each control saves its own field; the rest stay as stored
    public sealed record DensityBody(bool DenseRows);
    public sealed record FollowBody(string Level);

    public sealed record NotificationRow(Guid Id, string EventType, Guid? ProjectId, string? ProjectNumber, string? ItemType, Guid? ItemId, string? ItemKey, string Title, string? Body,
        string? LinkPath, Guid? ActorUserId, string? ActorName, int Count, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? ReadAt);
    /// The project stamp is written only when the live-stamp flag is on and the project is visible (packet 042).
    public sealed record NotificationPulse(string Stamp, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ProjectStamp = null);
    public sealed record UnreadCount(int Notifications, int Following);
    public sealed record MarkedRead(int Marked);
    public sealed record PreferenceEvent(string Code, bool App, bool Email, bool DefaultApp, bool DefaultEmail, bool Direct, bool Custom);
    public sealed record PreferenceDigest(bool DigestEnabled, bool HoldDigestWhenAway, string Time, string OrgTime, bool WeekendDigests, IReadOnlyList<string> Sections,
        string[] SectionsOff, bool WeeklySummaryEnabled, bool ManagesProjects);
    public sealed record PreferenceFollow(Guid ProjectId, string ProjectNumber, string Name, string Status, string Level, string Source);
    public sealed record PreferencesRead(IReadOnlyList<PreferenceEvent> Events, PreferenceDigest Digest, IReadOnlyList<PreferenceFollow> Follows);
    public sealed record FollowRead(string Level, string Source);
    public sealed record FollowingProject(Guid ProjectId, string ProjectNumber, string Name, string Status, DateTimeOffset? LastSeenAt, int Unread);
    public sealed record FollowingItem(ActivityEndpoints.ActivityEntry Entry, int Count, string ProjectNumber, string ProjectName, bool Notified, bool Unread);
    public sealed record FollowingFeed(IReadOnlyList<FollowingProject> Projects, IReadOnlyList<FollowingItem> Items, int Page, int PageSize, int TotalCount);

    public static void Map(RouteGroupBuilder api)
    {
        // ---------- Personal notifications ----------

        api.MapGet("/me/notifications", async Task<Ok<Page<NotificationRow>>> (HubDb db, Access access, CurrentUser me, bool? unread, string? type, Guid? projectId, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize ?? 50);
            var visible = access.VisibleProjectIds();
            var q = (await PlanningEndpoints.VisibleNotifications(db, access)).AsNoTracking();
            if (unread == true) q = q.Where(n => n.ReadAt == null);
            var types = Http.List(type);
            if (types.Length > 0) q = q.Where(n => types.Contains(n.EventType));
            if (projectId is { } pid) q = q.Where(n => n.ProjectId == pid);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(n => n.UpdatedAt).ThenByDescending(n => n.Id).Skip((pg - 1) * size).Take(size)
                .Select(n => new NotificationRow(n.Id, n.EventType, n.ProjectId, db.Projects.Where(p => p.Id == n.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault(),
                    n.ItemType, n.ItemId, n.ItemKey, n.Title, n.Body, n.LinkPath, n.ActorUserId,
                    db.Users.Where(u => u.Id == n.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(), n.Count, n.CreatedAt, n.UpdatedAt, n.ReadAt)).ToListAsync();
            return TypedResults.Ok(new Page<NotificationRow>(rows, pg, size, total));
        });

        // FR-003 (§17.6, packet 020): a cheap stamp the SPA polls every few seconds; it asks for the counts again only when
        // the stamp changes — a notification arrives or is read, a followed project changes, or a read marker moves.
        api.MapGet("/me/notifications/pulse", async Task<Ok<NotificationPulse>> (HubDb db, Access access, CurrentUser me, SettingsStore store, Guid? projectId) =>
        {
            var visible = access.VisibleProjectIds();
            var permitted = (await PlanningEndpoints.VisibleNotifications(db, access));
            var unread = await permitted.CountAsync(n => n.ReadAt == null);
            var latest = await permitted.MaxAsync(n => (DateTimeOffset?)n.CreatedAt);
            var follows = await db.Follows.AsNoTracking().Where(f => f.UserId == me.Id && f.Level != FollowLevel.Muted && visible.Contains(f.ProjectId)).Select(f => new FollowSeen(f.ProjectId, f.LastSeenAt)).ToListAsync();
            var ids = follows.Select(f => f.ProjectId).ToList();
            var activity = ids.Count == 0 ? null : await VisibleFollowed(db, access, ids, me.Id).MaxAsync(a => (DateTimeOffset?)a.OccurredAt);
            var seen = follows.Max(f => f.LastSeenAt);
            var stamp = $"{unread}:{latest?.UtcTicks}:{activity?.UtcTicks}:{seen?.UtcTicks}";
            var settings = await store.Get(db);
            if (settings.IsFlagOn("people_screens"))
            {
                // Only own work attached to a currently visible app row may affect this opaque stamp.
                // Reuse the canonical group/planning ACL query; orphaned or hidden groups contribute nothing.
                var linked = db.NoticeDeliveryIntents.AsNoTracking().Where(intent => intent.UserId == me.Id
                    && intent.NotificationId != null && permitted.Any(notification => notification.Id == intent.NotificationId.Value));
                var policyUpdated = await linked.MaxAsync(intent => (DateTimeOffset?)intent.UpdatedAt);
                var transportObserved = await db.Emails.AsNoTracking().Where(email => email.UserId == me.Id
                        && linked.Any(intent => intent.DeliveryEmailId == email.Id))
                    .MaxAsync(email => email.TransportObservedAt);
                stamp += $":{policyUpdated?.UtcTicks}:{transportObserved?.UtcTicks}";
            }
            if (!settings.IsFlagOn("live_project_stamp") || projectId is not { } pid)
                return TypedResults.Ok(new NotificationPulse(stamp));
            // Check visibility before either source read so restricted and unknown projects remain indistinguishable.
            var visibleIds = await access.VisibleProjectIdSet();
            if (!visibleIds.Contains(pid)) return TypedResults.Ok(new NotificationPulse(stamp));
            var projectActivity = await (await ActivityEndpoints.Visible(db, access, pid))
                .Select(a => (DateTimeOffset?)a.OccurredAt).MaxAsync();
            var evaluated = await db.ProjectStates.AsNoTracking().Where(s => s.ProjectId == pid)
                .Select(s => (DateTimeOffset?)s.EvaluatedAt).FirstOrDefaultAsync();
            return TypedResults.Ok(new NotificationPulse(stamp, $"{projectActivity?.UtcTicks}:{evaluated?.UtcTicks}"));
        });

        // The counts behind the bell and the Following tab; the SPA re-reads them when the pulse changes.
        api.MapGet("/me/notifications/unread-count", async Task<Ok<UnreadCount>> (HubDb db, Access access, CurrentUser me, TimeProvider clock) =>
        {
            var visible = access.VisibleProjectIds();
            var personal = await (await PlanningEndpoints.VisibleNotifications(db, access)).CountAsync(n => n.ReadAt == null);
            var following = (await FollowingUnread(db, access, me.Id)).Values.Sum();
            return TypedResults.Ok(new UnreadCount(personal, following));
        });

        api.MapPost("/me/notifications/read", async Task<Ok<MarkedRead>> (ReadBody body, HubDb db, CurrentUser me, TimeProvider clock, Access access) =>
        {
            var q = (await PlanningEndpoints.VisibleNotifications(db, access)).Where(n => n.ReadAt == null);
            if (body.All != true) { var ids = body.Ids ?? []; q = q.Where(n => ids.Contains(n.Id)); }
            var now = clock.GetUtcNow();
            var n = await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, now));
            return TypedResults.Ok(new MarkedRead(n));
        });

        // ---------- Preferences (§17.4) ----------

        api.MapGet("/me/preferences", async Task<Ok<PreferencesRead>> (HubDb db, Access access, CurrentUser me, SettingsStore store) =>
        {
            var s = await store.Get(db);
            var mine = await db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == me.Id).ToDictionaryAsync(p => p.EventType);
            var set = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id) ?? new UserSetting { UserId = me.Id };
            var visible = access.VisibleProjects();
            var follows = await (from f in db.Follows.AsNoTracking().Where(f => f.UserId == me.Id)
                join p in visible on f.ProjectId equals p.Id
                orderby p.ProjectNumber
                select new PreferenceFollow(f.ProjectId, p.ProjectNumber, p.Name, p.Status, f.Level, f.Source)).ToListAsync();
            var events = NotificationEvents.All.Select(e =>
            {
                var d = s.NotificationDefaults.GetValueOrDefault(e.Code) ?? new Channels(e.App, e.Email);
                var p = mine.GetValueOrDefault(e.Code);
                return new PreferenceEvent(e.Code, p?.InApp ?? d.App, p?.Email ?? d.Email, d.App, d.Email, e.DirectAssignment, p is not null);
            }).ToList();
            var digest = new PreferenceDigest(set.DigestEnabled, set.HoldDigestWhenAway, set.DigestTimeLocal ?? s.DigestSendTimeLocal, s.DigestSendTimeLocal, s.WeekendDigests,
                Digest.SectionCodes, JsonSerializer.Deserialize<string[]>(set.DigestSectionsOff) ?? [], // FR-002
                set.WeeklySummaryEnabled, await WeeklySummary.Managed(db, me.Id).AnyAsync()); // FR-001
            return TypedResults.Ok(new PreferencesRead(events, digest, follows));
        });

        api.MapPut("/me/preferences/events/{code}", async Task<NoContent> (string code, ChannelBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            return await Tx.Run(db, async () =>
            {
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id);
                Check.OneOf(code, NotificationEvents.All.Select(e => e.Code).ToArray(), "code");
                var p = await db.NotificationPreferences.FirstOrDefaultAsync(x => x.UserId == me.Id && x.EventType == code);
                if (p is null) db.NotificationPreferences.Add(p = new NotificationPreference { UserId = me.Id, EventType = code });
                p.InApp = body.App;
                p.Email = body.Email;
                await db.SaveChangesAsync();
                await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                    new(NoticePolicyChangeKind.EventChannels, EventType: code), clock.GetUtcNow());
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            });
        });

        api.MapDelete("/me/preferences/events/{code}", async Task<NoContent> (string code, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            return await Tx.Run(db, async () =>
            {
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id);
                await db.NotificationPreferences.Where(x => x.UserId == me.Id && x.EventType == code).ExecuteDeleteAsync(); // back to the organisation default
                if (NotificationEvents.All.Any(e => e.Code == code))
                {
                    await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                        new(NoticePolicyChangeKind.EventReset, EventType: code), clock.GetUtcNow());
                    await db.SaveChangesAsync();
                }
                return TypedResults.NoContent();
            });
        });

        api.MapPut("/me/preferences/digest", async Task<NoContent> (DigestBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            return await Tx.Run(db, async () =>
            {
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id);
                if (body.Time is { Length: > 0 } t) Check.That(TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "time", "setting.time");
                if (body.SectionsOff is { } sections) Check.That(sections.All(Digest.SectionCodes.Contains), "sectionsOff", "error.one_of", string.Join(", ", Digest.SectionCodes));
                var set = await UserPreferences.GetOrCreate(db, me.Id);
                set.DigestEnabled = body.Enabled;
                set.DigestTimeLocal = string.IsNullOrWhiteSpace(body.Time) ? null : body.Time;
                if (body.SectionsOff is { } off)
                {
                    set.DigestSectionsOff = JsonSerializer.Serialize(off.Distinct().ToArray());
                }
                if (body.WeeklySummary is { } w) set.WeeklySummaryEnabled = w;
                if (body.HoldDigestWhenAway is { } hold) set.HoldDigestWhenAway = hold;
                await db.SaveChangesAsync();
                await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                    new(NoticePolicyChangeKind.Digest), clock.GetUtcNow());
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            });
        });

        api.MapPut("/me/preferences/density", async Task<NoContent> (DensityBody body, HubDb db, CurrentUser me) =>
        {
            return await Tx.Run(db, async () =>
            {
                await NoticeRecipientLock.Acquire(db, me.Id);
                var set = await UserPreferences.GetOrCreate(db, me.Id);
                set.DenseRows = body.DenseRows;
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            });
        });

        // The weekly PM summary as it would be sent now (FR-001), so a PM can see it before Monday.
        api.MapGet("/me/weekly-summary", async Task<Results<Ok<WeeklySummary.Result>, NoContent>> (HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock, IConfiguration cfg) =>
        {
            var s = await store.Get(db);
            var now = clock.GetUtcNow();
            var last = await db.UserSettings.Where(x => x.UserId == me.Id).Select(x => x.LastWeeklySummaryAt).FirstOrDefaultAsync();
            var name = await db.Users.Where(u => u.Id == me.Id).Select(u => u.DisplayName).FirstAsync();
            var r = await WeeklySummary.Build(db, me.Id, name.Split(' ')[0], clock.Today(s), last ?? now.AddDays(-7), now, s, (cfg["Email:BaseUrl"] ?? "").TrimEnd('/'));
            if (r is null) return TypedResults.NoContent();
            return TypedResults.Ok(r);
        });

        // ---------- Following (§12.18 ASG-02, ASG-03) ----------

        api.MapPut("/projects/{id:guid}/follow", async Task<Ok<FollowRead>> (Guid id, FollowBody body, Access access, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            return await Tx.Run(db, async () =>
            {
                await Coordination.Lock(db, id); // Producers take the project lock before the recipient lock too.
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id);
                await access.Project(id, track: false); // ASG-03: only projects the user can view
                Check.OneOf(body.Level, FollowLevel.All, "level");
                var f = await db.Follows.FirstOrDefaultAsync(x => x.ProjectId == id && x.UserId == me.Id);
                var now = clock.GetUtcNow();
                if (f is null) db.Follows.Add(f = new ProjectFollow { ProjectId = id, UserId = me.Id, CreatedAt = now, LastSeenAt = now });
                f.Level = body.Level;
                f.Source = FollowSource.Manual; // ASG-02: the user's choice sticks
                f.UpdatedAt = now;
                await db.SaveChangesAsync();
                await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                    new(NoticePolicyChangeKind.Follow, ProjectId: id), clock.GetUtcNow());
                await db.SaveChangesAsync();
                return TypedResults.Ok(new FollowRead(f.Level, f.Source));
            });
        });

        api.MapDelete("/projects/{id:guid}/follow", async Task<NoContent> (Guid id, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            return await Tx.Run(db, async () =>
            {
                await Coordination.Lock(db, id);
                var recipient = await NotificationDelivery.LockRecipientAsync(db, me.Id);
                await db.Follows.Where(x => x.ProjectId == id && x.UserId == me.Id).ExecuteDeleteAsync();
                await NotificationDelivery.ObservePolicyChangeAsync(recipient,
                    new(NoticePolicyChangeKind.FollowReset, ProjectId: id), clock.GetUtcNow());
                await db.SaveChangesAsync();
                return TypedResults.NoContent();
            });
        });

        // ---------- Following feed (ASG-05..ASG-07) ----------

        api.MapGet("/me/following", async Task<Ok<FollowingFeed>> (HubDb db, Access access, CurrentUser me, Guid? projectId, bool? importantOnly, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize ?? 100);
            var followed = await Followed(db, access, me.Id);
            var ids = followed.Where(f => projectId is null || f.ProjectId == projectId).Select(f => f.ProjectId).ToList();
            var q = ActivityEndpoints.Filter(VisibleFollowed(db, access, ids, me.Id),
                null, null, null, null, null, null, importantOnly);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            var notified = await NotifiedKeys(db, me.Id, rows, await access.VisibleProjectIds().ToListAsync());
            var rendered = await ActivityEndpoints.Render(db, rows);
            var byProject = followed.ToDictionary(f => f.ProjectId);
            // Entries sharing a correlation id (bulk actions, cascades) collapse into one with a count (G-10).
            var items = rows.Zip(rendered).GroupBy(x => x.First.CorrelationId is { } c ? $"{x.First.ProjectId}:{c}" : x.First.Id.ToString())
                .Select(g =>
                {
                    var (row, view) = g.First();
                    var f = byProject[row.ProjectId!.Value];
                    var isNotified = g.Any(x => notified.Contains(Key(x.First)));
                    return new FollowingItem(view, g.Count(), f.ProjectNumber, f.Name, isNotified,
                        f.Status != ProjectStatus.Setup && !isNotified && row.OccurredAt > (f.LastSeenAt ?? DateTimeOffset.MinValue));
                }).ToList();
            var unread = await FollowingUnread(db, access, me.Id);
            var projects = followed.Select(f => new FollowingProject(f.ProjectId, f.ProjectNumber, f.Name, f.Status, f.LastSeenAt, unread.GetValueOrDefault(f.ProjectId))).ToList();
            return TypedResults.Ok(new FollowingFeed(projects, items, pg, size, total));
        });

        api.MapPost("/me/following/{projectId:guid}/read", async Task<NoContent> (Guid projectId, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            await db.Follows.Where(f => f.UserId == me.Id && f.ProjectId == projectId).ExecuteUpdateAsync(s => s.SetProperty(f => f.LastSeenAt, now));
            return TypedResults.NoContent();
        });
    }

    public sealed record FollowRow(Guid ProjectId, string ProjectNumber, string Name, string Status, DateTimeOffset? LastSeenAt);
    sealed record FollowSeen(Guid ProjectId, DateTimeOffset? LastSeenAt);

    /// Projects followed at All activity that still produce a feed: not Archived or Cancelled, and still visible (ASG-04, ASG-07, E-28).
    public static async Task<List<FollowRow>> Followed(HubDb db, Access access, Guid userId)
    {
        var visible = access.VisibleProjects();
        return await (from f in db.Follows.AsNoTracking().Where(f => f.UserId == userId && f.Level == FollowLevel.AllActivity)
            join p in visible on f.ProjectId equals p.Id
            where p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled
            orderby p.ProjectNumber
            select new FollowRow(p.Id, p.ProjectNumber, p.Name, p.Status, f.LastSeenAt)).ToListAsync();
    }

    static string Key(ActivityLog a) => a.CorrelationId?.ToString() ?? "";

    /// ASG-06: log rows whose action already produced a personal notification for the user (same correlation id).
    public static async Task<HashSet<string>> NotifiedKeys(HubDb db, Guid userId, List<ActivityLog> rows, IReadOnlyCollection<Guid> requiredProjectScope)
    {
        var corr = rows.Select(r => r.CorrelationId).OfType<Guid>().Distinct().ToList();
        if (corr.Count == 0) return [];
        return (await db.Notifications.Where(n => n.UserId == userId && n.CorrelationId != null && corr.Contains(n.CorrelationId.Value)
                && (n.RequiredProjectIds == null || n.RequiredProjectIds.Length > 0
                    && n.RequiredProjectIds.All(id => id != Guid.Empty && requiredProjectScope.Contains(id))))
            .Select(n => n.CorrelationId!.Value).Distinct().ToListAsync()).Select(c => c.ToString()).ToHashSet();
    }

    /// Unread entries per followed project: changes by others after the read marker, collapsed by correlation id,
    /// excluding ones that notified the user; Setup projects have no unread counts (ASG-05..ASG-07).
    public static async Task<Dictionary<Guid, int>> FollowingUnread(HubDb db, Access access, Guid userId)
    {
        var result = new Dictionary<Guid, int>();
        var visibleProjects = access.VisibleProjectIds();
        foreach (var f in (await Followed(db, access, userId)).Where(f => f.Status != ProjectStatus.Setup))
        {
            var since = f.LastSeenAt ?? DateTimeOffset.MinValue;
            var rows = (await ActivityEndpoints.Visible(db, access, f.ProjectId)).Where(a => a.OccurredAt > since && a.ActorUserId != userId
                && (a.CorrelationId == null || !db.Notifications.Any(n => n.UserId == userId && n.CorrelationId == a.CorrelationId
                    && (n.RequiredProjectIds == null || n.RequiredProjectIds.Length > 0
                        && n.RequiredProjectIds.All(id => id != Guid.Empty && visibleProjects.Contains(id))))));
            var n = await rows.Select(r => r.CorrelationId ?? r.Id).Distinct().CountAsync();
            if (n > 0) result[f.ProjectId] = n;
        }
        return result;
    }

    static IQueryable<ActivityLog> VisibleFollowed(HubDb db, Access access, IEnumerable<Guid> projectIds, Guid userId) =>
        ActivityEndpoints.Visible(db, access, projectIds.Distinct().ToArray()).Where(a => a.ActorUserId != userId);
}
