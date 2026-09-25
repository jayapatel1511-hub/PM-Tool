using System.Globalization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Notification centre, preferences, follows and the Following feed (§13.17, §17.4, §12.18, FR-NOT-01..03, FR-ASG-02/03).
public static class NotificationEndpoints
{
    public sealed record ReadBody(Guid[]? Ids, bool? All);
    public sealed record ChannelBody(bool App, bool Email);
    public sealed record DigestBody(bool Enabled, string? Time, string[]? SectionsOff, bool? WeeklySummary);
    public sealed record FollowBody(string Level);

    public static void Map(RouteGroupBuilder api)
    {
        // ---------- Personal notifications ----------

        api.MapGet("/me/notifications", async (HubDb db, Access access, CurrentUser me, bool? unread, string? type, Guid? projectId, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize ?? 50);
            var visible = access.VisibleProjectIds();
            var q = db.Notifications.AsNoTracking().Where(n => n.UserId == me.Id && (n.ProjectId == null || visible.Contains(n.ProjectId.Value)));
            if (unread == true) q = q.Where(n => n.ReadAt == null);
            var types = Http.List(type);
            if (types.Length > 0) q = q.Where(n => types.Contains(n.EventType));
            if (projectId is { } pid) q = q.Where(n => n.ProjectId == pid);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(n => n.UpdatedAt).ThenByDescending(n => n.Id).Skip((pg - 1) * size).Take(size)
                .Select(n => new
                {
                    n.Id, n.EventType, n.ProjectId, ProjectNumber = db.Projects.Where(p => p.Id == n.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault(),
                    n.ItemType, n.ItemId, n.ItemKey, n.Title, n.Body, n.LinkPath, n.ActorUserId,
                    ActorName = db.Users.Where(u => u.Id == n.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(), n.Count, n.CreatedAt, n.UpdatedAt, n.ReadAt,
                }).ToListAsync();
            return new Page<object>([.. rows], pg, size, total);
        });

        // FR-003 (§17.6, packet 020): a cheap stamp the SPA polls every few seconds; it asks for the counts again only when
        // the stamp changes — a notification arrives or is read, a followed project changes, or a read marker moves.
        api.MapGet("/me/notifications/pulse", async (HubDb db, Access access, CurrentUser me) =>
        {
            var unread = await db.Notifications.CountAsync(n => n.UserId == me.Id && n.ReadAt == null);
            var latest = await db.Notifications.Where(n => n.UserId == me.Id).MaxAsync(n => (DateTimeOffset?)n.CreatedAt);
            var follows = await db.Follows.AsNoTracking().Where(f => f.UserId == me.Id && f.Level != FollowLevel.Muted).Select(f => new { f.ProjectId, f.LastSeenAt }).ToListAsync();
            var ids = follows.Select(f => (Guid?)f.ProjectId).ToList();
            var activity = ids.Count == 0 ? null : await db.ActivityLog.Where(a => ids.Contains(a.ProjectId) && a.ActorUserId != me.Id).MaxAsync(a => (DateTimeOffset?)a.OccurredAt);
            var seen = follows.Max(f => f.LastSeenAt);
            return new { Stamp = $"{unread}:{latest?.UtcTicks}:{activity?.UtcTicks}:{seen?.UtcTicks}" };
        });

        // The counts behind the bell and the Following tab; the SPA re-reads them when the pulse changes.
        api.MapGet("/me/notifications/unread-count", async (HubDb db, Access access, CurrentUser me, TimeProvider clock) =>
        {
            var visible = access.VisibleProjectIds();
            var personal = await db.Notifications.CountAsync(n => n.UserId == me.Id && n.ReadAt == null && (n.ProjectId == null || visible.Contains(n.ProjectId.Value)));
            var following = (await FollowingUnread(db, access, me.Id)).Values.Sum();
            return new { notifications = personal, following };
        });

        api.MapPost("/me/notifications/read", async (ReadBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var q = db.Notifications.Where(n => n.UserId == me.Id && n.ReadAt == null);
            if (body.All != true) { var ids = body.Ids ?? []; q = q.Where(n => ids.Contains(n.Id)); }
            var now = clock.GetUtcNow();
            var n = await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, now));
            return Results.Ok(new { marked = n });
        });

        // ---------- Preferences (§17.4) ----------

        api.MapGet("/me/preferences", async (HubDb db, Access access, CurrentUser me, SettingsStore store) =>
        {
            var s = await store.Get(db);
            var mine = await db.NotificationPreferences.AsNoTracking().Where(p => p.UserId == me.Id).ToDictionaryAsync(p => p.EventType);
            var set = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id) ?? new UserSetting { UserId = me.Id };
            var visible = access.VisibleProjects();
            var follows = await db.Follows.AsNoTracking().Where(f => f.UserId == me.Id).Join(visible, f => f.ProjectId, p => p.Id,
                (f, p) => new { f.ProjectId, p.ProjectNumber, p.Name, p.Status, f.Level, f.Source }).OrderBy(x => x.ProjectNumber).ToListAsync();
            return new
            {
                Events = NotificationEvents.All.Select(e =>
                {
                    var d = s.NotificationDefaults.GetValueOrDefault(e.Code) ?? new Channels(e.App, e.Email);
                    var p = mine.GetValueOrDefault(e.Code);
                    return new { e.Code, App = p?.InApp ?? d.App, Email = p?.Email ?? d.Email, DefaultApp = d.App, DefaultEmail = d.Email, Direct = e.DirectAssignment, Custom = p is not null };
                }),
                Digest = new
                {
                    set.DigestEnabled, Time = set.DigestTimeLocal ?? s.DigestSendTimeLocal, OrgTime = s.DigestSendTimeLocal, s.WeekendDigests,
                    Sections = Digest.SectionCodes, SectionsOff = JsonSerializer.Deserialize<string[]>(set.DigestSectionsOff) ?? [], // FR-002
                    set.WeeklySummaryEnabled, ManagesProjects = await WeeklySummary.Managed(db, me.Id).AnyAsync(), // FR-001
                },
                Follows = follows,
            };
        });

        api.MapPut("/me/preferences/events/{code}", async (string code, ChannelBody body, HubDb db, CurrentUser me) =>
        {
            Check.OneOf(code, NotificationEvents.All.Select(e => e.Code).ToArray(), "code");
            var p = await db.NotificationPreferences.FirstOrDefaultAsync(x => x.UserId == me.Id && x.EventType == code);
            if (p is null) db.NotificationPreferences.Add(p = new NotificationPreference { UserId = me.Id, EventType = code });
            p.InApp = body.App;
            p.Email = body.Email;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/me/preferences/events/{code}", async (string code, HubDb db, CurrentUser me) =>
        {
            await db.NotificationPreferences.Where(x => x.UserId == me.Id && x.EventType == code).ExecuteDeleteAsync(); // back to the organisation default
            return Results.NoContent();
        });

        api.MapPut("/me/preferences/digest", async (DigestBody body, HubDb db, CurrentUser me) =>
        {
            if (body.Time is { Length: > 0 } t) Check.That(TimeOnly.TryParseExact(t, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _), "time", "setting.time");
            var set = await db.UserSettings.FirstOrDefaultAsync(x => x.UserId == me.Id);
            if (set is null) db.UserSettings.Add(set = new UserSetting { UserId = me.Id });
            set.DigestEnabled = body.Enabled;
            set.DigestTimeLocal = string.IsNullOrWhiteSpace(body.Time) ? null : body.Time;
            if (body.SectionsOff is { } off)
            {
                Check.That(off.All(Digest.SectionCodes.Contains), "sectionsOff", "error.one_of", string.Join(", ", Digest.SectionCodes));
                set.DigestSectionsOff = JsonSerializer.Serialize(off.Distinct().ToArray());
            }
            if (body.WeeklySummary is { } w) set.WeeklySummaryEnabled = w;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // The weekly PM summary as it would be sent now (FR-001), so a PM can see it before Monday.
        api.MapGet("/me/weekly-summary", async (HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock, IConfiguration cfg) =>
        {
            var s = await store.Get(db);
            var now = clock.GetUtcNow();
            var last = await db.UserSettings.Where(x => x.UserId == me.Id).Select(x => x.LastWeeklySummaryAt).FirstOrDefaultAsync();
            var name = await db.Users.Where(u => u.Id == me.Id).Select(u => u.DisplayName).FirstAsync();
            var r = await WeeklySummary.Build(db, me.Id, name.Split(' ')[0], clock.Today(s), last ?? now.AddDays(-7), now, s, (cfg["Email:BaseUrl"] ?? "").TrimEnd('/'));
            return r is null ? Results.NoContent() : Results.Ok(r);
        });

        // ---------- Following (§12.18 ASG-02, ASG-03) ----------

        api.MapPut("/projects/{id:guid}/follow", async (Guid id, FollowBody body, Access access, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            await access.Project(id, track: false); // ASG-03: only projects the user can view
            Check.OneOf(body.Level, FollowLevel.All, "level");
            var f = await db.Follows.FirstOrDefaultAsync(x => x.ProjectId == id && x.UserId == me.Id);
            var now = clock.GetUtcNow();
            if (f is null) db.Follows.Add(f = new ProjectFollow { ProjectId = id, UserId = me.Id, CreatedAt = now, LastSeenAt = now });
            f.Level = body.Level;
            f.Source = FollowSource.Manual; // ASG-02: the user's choice sticks
            f.UpdatedAt = now;
            await db.SaveChangesAsync();
            return Results.Ok(new { f.Level, f.Source });
        });

        api.MapDelete("/projects/{id:guid}/follow", async (Guid id, HubDb db, CurrentUser me) =>
        {
            await db.Follows.Where(x => x.ProjectId == id && x.UserId == me.Id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

        // ---------- Following feed (ASG-05..ASG-07) ----------

        api.MapGet("/me/following", async (HubDb db, Access access, CurrentUser me, Guid? projectId, bool? importantOnly, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize ?? 100);
            var followed = await Followed(db, access, me.Id);
            var ids = followed.Where(f => projectId is null || f.ProjectId == projectId).Select(f => f.ProjectId).ToList();
            var q = ActivityEndpoints.Filter(db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && ids.Contains(a.ProjectId.Value) && a.ActorUserId != me.Id),
                null, null, null, null, null, null, importantOnly);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            var notified = await NotifiedKeys(db, me.Id, rows);
            var rendered = await ActivityEndpoints.Render(db, rows);
            var byProject = followed.ToDictionary(f => f.ProjectId);
            // Entries sharing a correlation id (bulk actions, cascades) collapse into one with a count (G-10).
            var items = rows.Zip(rendered).GroupBy(x => x.First.CorrelationId is { } c ? $"{x.First.ProjectId}:{c}" : x.First.Id.ToString())
                .Select(g =>
                {
                    var (row, view) = g.First();
                    var f = byProject[row.ProjectId!.Value];
                    var isNotified = g.Any(x => notified.Contains(Key(x.First)));
                    return new
                    {
                        Entry = view, Count = g.Count(), ProjectNumber = f.ProjectNumber, ProjectName = f.Name, Notified = isNotified,
                        Unread = f.Status != ProjectStatus.Setup && !isNotified && row.OccurredAt > (f.LastSeenAt ?? DateTimeOffset.MinValue),
                    };
                }).ToList();
            var unread = await FollowingUnread(db, access, me.Id);
            return new
            {
                Projects = followed.Select(f => new { f.ProjectId, f.ProjectNumber, f.Name, f.Status, f.LastSeenAt, Unread = unread.GetValueOrDefault(f.ProjectId) }),
                Items = items, Page = pg, PageSize = size, TotalCount = total,
            };
        });

        api.MapPost("/me/following/{projectId:guid}/read", async (Guid projectId, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var now = clock.GetUtcNow();
            await db.Follows.Where(f => f.UserId == me.Id && f.ProjectId == projectId).ExecuteUpdateAsync(s => s.SetProperty(f => f.LastSeenAt, now));
            return Results.NoContent();
        });
    }

    public sealed record FollowRow(Guid ProjectId, string ProjectNumber, string Name, string Status, DateTimeOffset? LastSeenAt);

    /// Projects followed at All activity that still produce a feed: not Archived or Cancelled, and still visible (ASG-04, ASG-07, E-28).
    public static async Task<List<FollowRow>> Followed(HubDb db, Access access, Guid userId)
    {
        var visible = access.VisibleProjects();
        return await db.Follows.AsNoTracking().Where(f => f.UserId == userId && f.Level == FollowLevel.AllActivity)
            .Join(visible, f => f.ProjectId, p => p.Id, (f, p) => new { f, p })
            .Where(x => x.p.Status != ProjectStatus.Archived && x.p.Status != ProjectStatus.Cancelled)
            .OrderBy(x => x.p.ProjectNumber)
            .Select(x => new FollowRow(x.p.Id, x.p.ProjectNumber, x.p.Name, x.p.Status, x.f.LastSeenAt)).ToListAsync();
    }

    static string Key(ActivityLog a) => a.CorrelationId?.ToString() ?? "";

    /// ASG-06: log rows whose action already produced a personal notification for the user (same correlation id).
    public static async Task<HashSet<string>> NotifiedKeys(HubDb db, Guid userId, List<ActivityLog> rows)
    {
        var corr = rows.Select(r => r.CorrelationId).OfType<Guid>().Distinct().ToList();
        if (corr.Count == 0) return [];
        return (await db.Notifications.Where(n => n.UserId == userId && n.CorrelationId != null && corr.Contains(n.CorrelationId.Value))
            .Select(n => n.CorrelationId!.Value).Distinct().ToListAsync()).Select(c => c.ToString()).ToHashSet();
    }

    /// Unread entries per followed project: changes by others after the read marker, collapsed by correlation id,
    /// excluding ones that notified the user; Setup projects have no unread counts (ASG-05..ASG-07).
    public static async Task<Dictionary<Guid, int>> FollowingUnread(HubDb db, Access access, Guid userId)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var f in (await Followed(db, access, userId)).Where(f => f.Status != ProjectStatus.Setup))
        {
            var since = f.LastSeenAt ?? DateTimeOffset.MinValue;
            var rows = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == f.ProjectId && a.OccurredAt > since && a.ActorUserId != userId)
                .OrderByDescending(a => a.OccurredAt).Take(1000).ToListAsync();
            var notified = await NotifiedKeys(db, userId, rows);
            var n = rows.Where(r => !notified.Contains(Key(r))).GroupBy(r => r.CorrelationId is { } c ? c.ToString() : r.Id.ToString()).Count();
            if (n > 0) result[f.ProjectId] = n;
        }
        return result;
    }
}
