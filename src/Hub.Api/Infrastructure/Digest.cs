using System.Globalization;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// The daily digest (§17.3, FR-NOT-02, FR-ASG-04): one plain email per person at their digest time, only when there is
/// something to say. Each item appears once, in its most severe section; sections are capped at 10 rows.
public static class Digest
{
    public sealed record Row(Guid ItemId, string? Key, string Name, string Project, string Detail, string Link);
    public sealed record Section(string Code, List<Row> Rows, int Total);
    public sealed record Updates(string ProjectNumber, string ProjectName, int Count, Dictionary<string, int> ByType, List<string> Top);
    public sealed record Result(string Subject, string Body, List<Section> Sections, List<Updates> ProjectUpdates)
    { public Guid[] RequiredProjectIds { get; init; } = []; }

    const int Cap = 10;

    /// Sections a person can switch off (FR-002, packet 020), in digest order.
    public static readonly string[] SectionCodes = ["overdue", "dueSoon", "blocked", "reviews", "decisions", "handoffs", "reviewPackages", "changes", "attention", "milestones", "staff", "updates"];
    static readonly string[] ImportantCategories = ["status", "assignment", "date", "decision"];

    /// Active projects the person can see: open ones, and restricted ones they belong to (§8.7); Setup and On Hold are left out.
    static async Task<Dictionary<Guid, Project>> Projects(HubDb db, Guid userId, bool seesAll)
    {
        var member = db.ProjectMembers.Where(m => m.UserId == userId && m.RemovedAt == null).Select(m => m.ProjectId);
        return await db.Projects.AsNoTracking().Where(p => p.Status == ProjectStatus.Active
            && (seesAll || p.Visibility == Visibility.Open || p.ProjectManagerId == userId || member.Contains(p.Id))).ToDictionaryAsync(p => p.Id);
    }

    public static async Task<Result?> Build(HubDb db, Guid userId, string firstName, DateOnly today, DateTimeOffset since, DateTimeOffset now, OrgSettings s, string baseUrl,
        IReadOnlySet<string>? off = null)
    {
        off ??= new HashSet<string>();
        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync();
        var projects = await Projects(db, userId, roles.Contains(SystemRole.Admin) || roles.Contains(SystemRole.Executive));
        var pids = projects.Keys.ToList();
        string Num(Guid pid) => projects[pid].ProjectNumber;
        string TaskLink(Guid pid, Guid id) => $"{baseUrl}/projects/{Num(pid)}/tasks?panel=Task:{id}";
        string DelLink(Guid pid, Guid id) => $"{baseUrl}/projects/{Num(pid)}/deliverables?panel=Deliverable:{id}";
        static string D(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";

        var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled
                && (t.AssigneeId == userId || t.ReviewerId == userId))
            .Join(db.TaskStates, t => t.Id, st => st.TaskId, (t, st) => new { t, st }).ToListAsync();
        var dels = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && (d.OwnerId == userId || d.ReviewerId == userId))
            .Join(db.DeliverableStates, d => d.Id, st => st.DeliverableId, (d, st) => new { d, st }).ToListAsync();
        var decisions = await db.Decisions.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && (d.OwnerUserId == userId || d.RequestedById == userId)
                && (d.Status == DecisionStatus.Pending || d.Status == DecisionStatus.UnderReview || d.Status == DecisionStatus.Deferred))
            .Join(db.DecisionStates, d => d.Id, st => st.DecisionId, (d, st) => new { d, st }).ToListAsync();

        var listed = new HashSet<Guid>();
        Section Make(string code, IEnumerable<Row> rows)
        {
            if (off.Contains(code)) return new Section(code, [], 0); // switched off: its items may still appear in a later section
            var fresh = rows.Where(r => listed.Add(r.ItemId)).ToList(); // most severe section wins (§17.5 digest de-dup)
            return new Section(code, fresh.Take(Cap).ToList(), fresh.Count);
        }

        var mine = tasks.Where(x => x.t.AssigneeId == userId).ToList();
        var overdue = Make("overdue", mine.Where(x => x.st.IsOverdue).OrderByDescending(x => x.st.DaysOverdue)
                .Select(x => new Row(x.t.Id, x.t.Key, x.t.Name, Num(x.t.ProjectId), Text.Get("digest.overdue_detail", D(x.t.DueDate), x.st.DaysOverdue), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.OwnerId == userId && x.st.IsOverdue)
                .Select(x => new Row(x.d.Id, x.d.Key, x.d.Name, Num(x.d.ProjectId), Text.Get("digest.overdue_detail", D(x.d.DueDate), x.st.DaysOverdue), DelLink(x.d.ProjectId, x.d.Id)))));
        var blocked = Make("blocked", mine.Where(x => x.st.IsBlocked).OrderByDescending(x => x.st.DaysBlocked)
            .Select(x => new Row(x.t.Id, x.t.Key, x.t.Name, Num(x.t.ProjectId), Text.Get("digest.blocked_detail", Blockers(x.st.BlockedBy)), TaskLink(x.t.ProjectId, x.t.Id))));
        var reviews = Make("reviews", tasks.Where(x => x.t.ReviewerId == userId && TaskStatuses.IsReview(x.t.Status)).OrderBy(x => x.t.ReviewRequestedAt)
                .Select(x => new Row(x.t.Id, x.t.Key, x.t.Name, Num(x.t.ProjectId), Text.Get("digest.review_detail", x.t.Status, D(x.t.DueDate)), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.ReviewerId == userId && x.d.Status == DeliverableStatus.InReview)
                .Select(x => new Row(x.d.Id, x.d.Key, x.d.Name, Num(x.d.ProjectId), Text.Get("digest.review_detail", x.d.Status, D(x.d.DueDate)), DelLink(x.d.ProjectId, x.d.Id)))));
        var decisionRows = Make("decisions", decisions.Where(x => x.st.IsOverdue || x.st.IsDueSoon).OrderBy(x => x.d.RequiredByDate)
            .Select(x => new Row(x.d.Id, x.d.Key, x.d.Subject, Num(x.d.ProjectId),
                x.st.IsOverdue ? Text.Get("digest.decision_overdue", D(x.d.RequiredByDate), x.st.DaysOverdue) : Text.Get("digest.decision_due", D(x.d.RequiredByDate)),
                $"{baseUrl}/projects/{Num(x.d.ProjectId)}/decisions?panel=Decision:{x.d.Id}")));
        var dueSoon = Make("dueSoon", mine.Where(x => x.st.IsDueSoon && !x.st.IsOverdue).OrderBy(x => x.t.DueDate)
                .Select(x => new Row(x.t.Id, x.t.Key, x.t.Name, Num(x.t.ProjectId), Text.Get("digest.due_detail", D(x.t.DueDate)), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.OwnerId == userId && x.st.IsDueSoon && !x.st.IsOverdue)
                .Select(x => new Row(x.d.Id, x.d.Key, x.d.Name, Num(x.d.ProjectId), Text.Get("digest.due_detail", D(x.d.DueDate)), DelLink(x.d.ProjectId, x.d.Id)))));

        // PMs and leads: attention items routed to them, Critical and Warning, not snoozed.
        var snoozed = (await db.Snoozes.Where(z => pids.Contains(z.ProjectId) && z.EndedAt == null && z.SnoozedUntil > now).Select(z => new { z.RuleId, z.ItemId }).ToListAsync())
            .Select(z => (z.RuleId, z.ItemId)).ToHashSet();
        var leads = (await db.ProjectDisciplines.Where(d => pids.Contains(d.ProjectId) && d.LeadUserId == userId).Select(d => d.ProjectId).ToListAsync()).ToHashSet();
        var managed = projects.Values.Where(p => p.ProjectManagerId == userId || leads.Contains(p.Id)).Select(p => p.Id).ToList();
        var attention = (await db.Attention.AsNoTracking().Where(a => managed.Contains(a.ProjectId) && a.RouteToUserIds.Contains(userId)
                && (a.Severity == Severity.Critical || a.Severity == Severity.Warning)).ToListAsync())
            .Where(a => !snoozed.Contains((a.RuleId, a.ItemId))).OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).ToList();
        var attentionRows = Make("attention", attention.Select(a => new Row(a.ItemId, a.ItemKey, a.ItemName ?? "", Num(a.ProjectId), $"{a.Severity} {a.RuleId}: {a.Message}",
            $"{baseUrl}/projects/{Num(a.ProjectId)}")));

        // Milestones approaching in the person's projects (PM, or lead of a discipline with targeted deliverables).
        var horizon = today.AddDays(s.MilestoneApproachingDays);
        var targetedByLead = await db.Deliverables.Where(d => pids.Contains(d.ProjectId) && d.MilestoneId != null
            && db.ProjectDisciplines.Any(pd => pd.Id == d.ProjectDisciplineId && pd.LeadUserId == userId)).Select(d => d.MilestoneId!.Value).Distinct().ToListAsync();
        var ms = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date >= today && m.Date <= horizon
            && (db.Projects.Any(p => p.Id == m.ProjectId && p.ProjectManagerId == userId) || targetedByLead.Contains(m.Id))).OrderBy(m => m.Date).ToListAsync();
        var msStates = await db.MilestoneStates.Where(x => ms.Select(m => m.Id).Contains(x.MilestoneId)).ToDictionaryAsync(x => x.MilestoneId, x => x.Status);
        var milestones = Make("milestones", ms.Select(m => new Row(m.Id, m.Key, m.Name, Num(m.ProjectId),
            Text.Get("digest.milestone_detail", D(m.Date), m.Date!.Value.DayNumber - today.DayNumber, msStates.GetValueOrDefault(m.Id) ?? ""),
            $"{baseUrl}/projects/{Num(m.ProjectId)}/milestones?panel=Milestone:{m.Id}")));

        // Supervisors: direct reports with overdue or blocked work or reviews waiting too long, and staffing changes made by others (FR-ASG-07, ASG-11).
        var reports = await db.Users.AsNoTracking().Where(u => u.SupervisorId == userId && u.IsActive).Select(u => new { u.Id, u.DisplayName }).ToListAsync();
        var staffRows = new List<Row>();
        if (reports.Count > 0)
        {
            var rIds = reports.Select(r => r.Id).ToList();
            var work = await db.Tasks.AsNoTracking().Where(t => t.AssigneeId != null && rIds.Contains(t.AssigneeId.Value) && pids.Contains(t.ProjectId)
                    && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
                .Join(db.TaskStates, t => t.Id, st => st.TaskId, (t, st) => new { UserId = t.AssigneeId!.Value, st.IsOverdue, st.IsBlocked }).ToListAsync();
            var stalled = await db.Tasks.AsNoTracking().Where(t => t.ReviewerId != null && rIds.Contains(t.ReviewerId.Value) && pids.Contains(t.ProjectId))
                .Join(db.TaskStates.Where(st => st.IsReviewStalled), t => t.Id, st => st.TaskId, (t, st) => t.ReviewerId!.Value).ToListAsync();
            foreach (var r in reports)
            {
                int od = work.Count(w => w.UserId == r.Id && w.IsOverdue), bl = work.Count(w => w.UserId == r.Id && w.IsBlocked), rv = stalled.Count(x => x == r.Id);
                if (od + bl + rv > 0) staffRows.Add(new Row(r.Id, null, r.DisplayName, "", Text.Get("digest.staff_detail", od, bl, rv), $"{baseUrl}/my-work?userId={r.Id}"));
            }
            staffRows.AddRange((await db.Notifications.AsNoTracking().Where(n => n.UserId == userId && n.EventType == NotificationEvents.StaffAssignment && n.CreatedAt > since
                    && (n.ProjectId == null || pids.Contains(n.ProjectId.Value)))
                .OrderByDescending(n => n.CreatedAt).ToListAsync()).Select(n => new Row(n.Id, null, n.Title, "", "", $"{baseUrl}{n.LinkPath}")));
        }
        var staff = Make("staff", staffRows);

        // Project updates: changes by others on projects followed at All activity, minus ones that notified the person (ASG-06).
        var updates = new List<Updates>();
        var followed = off.Contains("updates") ? []
            : await db.Follows.Where(f => f.UserId == userId && f.Level == FollowLevel.AllActivity && pids.Contains(f.ProjectId)).Select(f => f.ProjectId).ToListAsync();
        foreach (var pid in followed.OrderBy(Num))
        {
            var rows = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == pid && a.OccurredAt > since && a.ActorUserId != userId && a.ActorUserId != null)
                .OrderByDescending(a => a.OccurredAt).Take(2000).ToListAsync();
            var notified = await NotificationEndpoints.NotifiedKeys(db, userId, rows);
            rows = rows.Where(r => r.CorrelationId is not { } c || !notified.Contains(c.ToString())).ToList();
            if (rows.Count == 0) continue;
            var byType = rows.GroupBy(r => r.Categories.FirstOrDefault(c => ImportantCategories.Contains(c)) ?? "other").ToDictionary(g => g.Key, g => g.Count());
            var top = await ActivityEndpoints.Render(db, rows.Where(r => r.Categories.Any(c => ImportantCategories.Contains(c)) || r.Action == "Issued").Take(5).ToList());
            updates.Add(new Updates(Num(pid), projects[pid].Name, rows.Count, byType,
                top.Select(x => { dynamic v = x; return $"{v.ActorName ?? Text.Get("common.system")} {Text.Get($"digest.action.{v.Action}")} {v.ItemKey} {v.ItemName}: {v.Summary}".Trim(); }).ToList()));
        }

        var handoffRows = await db.Handoffs.AsNoTracking().Where(h => pids.Contains(h.ProjectId)
            && (h.SendingOwnerId == userId || h.ReceivingOwnerId == userId)
            && h.Status != HandoffStatus.Incorporated && h.Status != HandoffStatus.Cancelled).OrderBy(h => h.NeededBy).ToListAsync();
        var handoffs = Make("handoffs", handoffRows.Select(h => new Row(h.Id, h.Key, h.Title, Num(h.ProjectId),
            Text.Get("handoff.digest_detail", h.Status, D(h.NeededBy)), $"{baseUrl}/projects/{Num(h.ProjectId)}/handoffs?panel=Handoff:{h.Id}")));
        var packages = await db.ReviewPackages.AsNoTracking().Where(p => pids.Contains(p.ProjectId) && (p.Status == ReviewStatus.InReview || p.Status == ReviewStatus.ChangesRequired)
            && (p.CoordinatorId == userId || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == userId && a.Status != DisciplineReviewStatus.Approved)
                || db.ReviewFindings.Any(f => f.RoundId == p.CurrentRoundId && (f.ResolverId == userId || f.VerifierId == userId) && (f.Status == FindingStatus.Open || f.Status == FindingStatus.Responded)))).ToListAsync();
        var reviewPackages = Make("reviewPackages", packages.Select(p => new Row(p.Id, p.Key, p.Title, Num(p.ProjectId), Text.Get("review.digest_detail", p.Status, p.RoundNumber), $"{baseUrl}/projects/{Num(p.ProjectId)}/reviews?panel=ReviewPackage:{p.Id}")));
        var notices = await db.ChangeNotices.AsNoTracking().Where(c => pids.Contains(c.ProjectId) && c.Status == ChangeStatus.Open
            && (c.OwnerId == userId || db.ChangeAssessments.Any(a => a.ChangeNoticeId == c.Id && (a.OwnerId == userId || a.ReviewerId == userId) && a.Status != AssessmentStatus.Resolved))).OrderBy(c => c.AssessmentDueDate).ToListAsync();
        var changes = Make("changes", notices.Select(c => new Row(c.Id, c.Key, c.Title, Num(c.ProjectId), Text.Get("change.digest_detail", c.Status, D(c.AssessmentDueDate)), $"{baseUrl}/projects/{Num(c.ProjectId)}/changes?panel=ChangeNotice:{c.Id}")));
        var sections = new List<Section> { overdue, dueSoon, blocked, reviews, decisionRows, handoffs, reviewPackages, changes, attentionRows, milestones, staff }.Where(x => x.Total > 0).ToList();
        if (sections.Count == 0 && updates.Count == 0) return null; // AC-NOT-04: nothing to say, nothing sent

        var parts = new List<string>();
        if (overdue.Total > 0) parts.Add(Text.Get("digest.subject_overdue", overdue.Total));
        if (reviews.Total > 0) parts.Add(Text.Get(reviews.Total == 1 ? "digest.subject_review" : "digest.subject_reviews", reviews.Total));
        if (blocked.Total > 0) parts.Add(Text.Get("digest.subject_blocked", blocked.Total));
        if (parts.Count == 0 && sections.FirstOrDefault() is { } first) parts.Add(Text.Get($"digest.subject_{first.Code}", first.Total));
        var updateCount = updates.Sum(u => u.Count);
        var updatesText = updateCount == 0 ? null : Text.Get(updateCount == 1 ? "digest.subject_update" : "digest.subject_updates", updateCount);
        var subject = Text.Get("digest.subject", parts.Count > 0 ? string.Join(", ", parts) + (updatesText is null ? "" : " · " + updatesText) : updatesText ?? "");

        var body = new StringBuilder();
        body.AppendLine(Text.Get("digest.greeting", firstName, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).AppendLine();
        foreach (var sec in sections)
        {
            body.AppendLine($"{Text.Get($"digest.section.{sec.Code}").ToUpperInvariant()} ({sec.Total})");
            foreach (var r in sec.Rows) body.AppendLine("- " + string.Join(" · ", new[] { $"{r.Key} {r.Name}".Trim(), r.Project, r.Detail }.Where(x => x.Length > 0))).AppendLine($"  {r.Link}");
            if (sec.Total > sec.Rows.Count) body.AppendLine($"  {Text.Get("digest.more", sec.Total - sec.Rows.Count)} {baseUrl}/my-work");
            body.AppendLine();
        }
        if (updates.Count > 0)
        {
            body.AppendLine($"{Text.Get("digest.section.updates").ToUpperInvariant()} ({updateCount})");
            foreach (var u in updates)
            {
                body.AppendLine($"{u.ProjectNumber} {u.ProjectName}: {Text.Get("digest.update_counts", u.Count, string.Join(", ", u.ByType.OrderByDescending(x => x.Value).Select(x => $"{x.Value} {Text.Get($"digest.type.{x.Key}")}")))}");
                foreach (var line in u.Top) body.AppendLine($"- {line}");
            }
            body.AppendLine($"  {Text.Get("digest.following")} {baseUrl}/notifications?tab=following").AppendLine();
        }
        body.AppendLine(Text.Get("digest.footer", $"{baseUrl}/my-work", $"{baseUrl}/preferences"));
        return new Result(subject, body.ToString(), sections, updates) { RequiredProjectIds = [.. pids] };
    }

    static string Blockers(string? json)
    {
        if (string.IsNullOrEmpty(json)) return "";
        var list = JsonSerializer.Deserialize<List<Blocker>>(json, JsonOpts.Web) ?? [];
        return string.Join(", ", list.Where(b => b.Blocking).Select(b => b.Key ?? $"{b.Name}: {b.Reason}"));
    }
}

/// Runs every five minutes and sends each person's digest once their local digest time has passed (§17.3).
public sealed class DigestJob : IJob
{
    public string Name => "daily-digest";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromMinutes(5), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var s = await sp.GetRequiredService<SettingsStore>().Get(db);
        var baseUrl = (sp.GetRequiredService<IConfiguration>()["Email:BaseUrl"] ?? "").TrimEnd('/');
        var now = clock.GetUtcNow();
        var local = Clock.Local(now, s);
        var today = DateOnly.FromDateTime(local.DateTime);
        if (!s.WeekendDigests && local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return new { skipped = "weekend" };
        var prefs = await db.UserSettings.ToDictionaryAsync(x => x.UserId, ct);
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => new { u.Id, u.Email, u.DisplayName }).ToListAsync(ct);
        int sent = 0, empty = 0;
        foreach (var u in users)
        {
            var pref = prefs.GetValueOrDefault(u.Id);
            if (pref is { DigestEnabled: false }) continue;
            var off = JsonSerializer.Deserialize<string[]>(pref?.DigestSectionsOff ?? "[]")!.ToHashSet();
            if (SectionsAllOff(off)) continue;
            var at = TimeOnly.TryParseExact(pref?.DigestTimeLocal ?? s.DigestSendTimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(7, 0);
            if (TimeOnly.FromDateTime(local.DateTime) < at) continue;
            var scheduled = new DateTimeOffset(today.ToDateTime(at), Clock.Zone(s).GetUtcOffset(today.ToDateTime(at))).ToUniversalTime();
            if (pref?.LastDigestAt is { } last && last >= scheduled) continue; // one digest per person per day (SC-002)
            var digest = await Digest.Build(db, u.Id, u.DisplayName.Split(' ')[0], today, pref?.LastDigestAt ?? now.AddDays(-1), now, s, baseUrl, off);
            if (digest is not null && !string.IsNullOrEmpty(u.Email))
            {
                db.Emails.Add(new EmailMessage { UserId = u.Id, ToAddress = u.Email, Subject = digest.Subject, BodyText = digest.Body, Kind = "Digest", RequiredProjectIds = digest.RequiredProjectIds,
                    DedupKey = $"digest:{u.Id}:{today:yyyy-MM-dd}", CreatedAt = now, NextAttemptAt = now });
                sent++;
            }
            else empty++;
            if (pref is null) { pref = new UserSetting { UserId = u.Id }; db.UserSettings.Add(pref); prefs[u.Id] = pref; }
            pref.LastDigestAt = now;
            await db.SaveChangesAsync(ct);
        }
        return new { sent, empty, date = today };
    }

    static bool SectionsAllOff(HashSet<string> off) => Digest.SectionCodes.All(off.Contains);
}

/// The weekly PM summary (§28 item 9, packet 020): one email per PM covering every Active project they manage — computed
/// and reported health, next submission, overdue and blocked tasks, overdue decisions, the top attention items and what
/// changed since the previous summary — counted exactly as the project dashboard counts them (SC-001).
public static class WeeklySummary
{
    public sealed record Part(Guid ProjectId, string ProjectNumber, string Name, string Computed, string Reported, string? NextSubmission, DateOnly? NextSubmissionDate,
        int Overdue, int Blocked, int DecisionsOverdue, List<string> Attention, int Changes, Dictionary<string, int> ChangesByType);
    public sealed record Result(string Subject, string Body, List<Part> Projects)
    { public Guid[] RequiredProjectIds { get; init; } = []; }

    static readonly string[] Important = ["status", "assignment", "date", "decision"];

    /// Active projects the person manages: as the project's PM or with the PM role on its team.
    public static IQueryable<Project> Managed(HubDb db, Guid userId) =>
        db.Projects.AsNoTracking().Where(p => p.Status == ProjectStatus.Active && (p.ProjectManagerId == userId
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))
            && (p.Visibility != Visibility.Restricted
                || p.ProjectManagerId == userId
                || db.UserRoles.Any(r => r.UserId == userId && (r.Role == SystemRole.Admin || r.Role == SystemRole.Executive))
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId && m.RemovedAt == null)));

    public static async Task<Result?> Build(HubDb db, Guid userId, string firstName, DateOnly today, DateTimeOffset since, DateTimeOffset now, OrgSettings s, string baseUrl)
    {
        var projects = await Managed(db, userId).OrderBy(p => p.ProjectNumber).ToListAsync();
        if (projects.Count == 0) return null; // a PM with no Active projects receives nothing
        var snoozed = (await db.Snoozes.Where(z => z.EndedAt == null && z.SnoozedUntil > now).Select(z => new { z.RuleId, z.ItemId }).ToListAsync()).Select(z => (z.RuleId, z.ItemId)).ToHashSet();
        var open = new[] { DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred };
        var parts = new List<Part>();
        foreach (var p in projects)
        {
            var computed = await db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => x.ComputedHealth).FirstOrDefaultAsync() ?? Health.Grey;
            var reported = p.HealthOverride is { } ho && p.HealthOverrideExpiresAt > now ? ho : computed;
            Task<int> Tasks(string key) => TaskQueries.Apply(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == p.Id), TaskFilter.Of(new Dictionary<string, string?> { [key] = "true" }),
                userId, today, s).CountAsync(); // the dashboard's own filters
            var sub = (await db.Milestones.AsNoTracking().Where(m => m.ProjectId == p.Id && !m.IsCancelled && !m.IsComplete && m.Date != null).ToListAsync())
                .Where(m => MilestoneType.IsSubmission(m.MilestoneType)).OrderBy(m => m.Date).FirstOrDefault();
            var attention = (await db.Attention.AsNoTracking().Where(a => a.ProjectId == p.Id && (a.Severity == Severity.Critical || a.Severity == Severity.Warning)).ToListAsync())
                .Where(a => !snoozed.Contains((a.RuleId, a.ItemId))).OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).Take(5)
                .Select(a => $"{a.Severity} {a.RuleId} {a.ItemKey} {a.ItemName}: {a.Message}".Replace("  ", " ")).ToList();
            var changes = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId == p.Id && a.OccurredAt > since && a.ActorUserId != null && a.ActorUserId != userId)
                .Select(a => a.Categories).ToListAsync();
            var byType = changes.GroupBy(c => c.FirstOrDefault(x => Important.Contains(x)) ?? "other").ToDictionary(g => g.Key, g => g.Count());
            parts.Add(new Part(p.Id, p.ProjectNumber, p.Name, computed, reported, sub?.Name, sub?.Date, await Tasks("overdue"), await Tasks("blocked"),
                await db.Decisions.CountAsync(d => d.ProjectId == p.Id && open.Contains(d.Status) && db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsOverdue)),
                attention, changes.Count, byType));
        }

        var reds = parts.Count(x => x.Computed == Health.Red || x.Reported == Health.Red);
        var subject = Text.Get("weekly.subject", Text.Get(parts.Count == 1 ? "weekly.project1" : "weekly.projectN", parts.Count), reds == 0 ? "" : Text.Get("weekly.subject_red", reds),
            parts.Sum(x => x.Overdue));
        var body = new StringBuilder();
        body.AppendLine(Text.Get(parts.Count == 1 ? "weekly.greeting1" : "weekly.greeting", firstName, parts.Count, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).AppendLine();
        foreach (var x in parts)
        {
            body.AppendLine($"{x.ProjectNumber} {x.Name}");
            body.AppendLine("  " + Text.Get("weekly.health", Text.Get($"health.{x.Computed}"), Text.Get($"health.{x.Reported}")));
            body.AppendLine("  " + (x.NextSubmission is null ? Text.Get("weekly.no_submission")
                : Text.Get("weekly.next_submission", x.NextSubmission, x.NextSubmissionDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.NextSubmissionDate.Value.DayNumber - today.DayNumber)));
            body.AppendLine("  " + Text.Get("weekly.counts", x.Overdue, x.Blocked, x.DecisionsOverdue));
            if (x.Attention.Count > 0) { body.AppendLine("  " + Text.Get("weekly.attention")); foreach (var a in x.Attention) body.AppendLine($"  - {a}"); }
            body.AppendLine("  " + (x.Changes == 0 ? Text.Get("weekly.no_changes")
                : Text.Get("weekly.changes", x.Changes, string.Join(", ", x.ChangesByType.OrderByDescending(c => c.Value).Select(c => $"{c.Value} {Text.Get($"digest.type.{c.Key}")}")))));
            body.AppendLine($"  {baseUrl}/projects/{x.ProjectNumber}/dashboard").AppendLine();
        }
        body.AppendLine(Text.Get("weekly.footer", $"{baseUrl}/preferences"));
        return new Result(subject, body.ToString(), parts) { RequiredProjectIds = [.. projects.Select(p => p.Id)] };
    }
}

/// Every fifteen minutes: each PM's weekly summary goes out once a week, on the morning of the earliest coordination day
/// among their Active projects (Monday when none is set), at their digest time.
public sealed class WeeklySummaryJob : IJob
{
    public string Name => "weekly-summary";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromMinutes(15), now, last);

    /// The earliest coordination day in the working week (Monday first). Worked example: Wednesday and Tuesday → Tuesday.
    public static DayOfWeek SendDay(IEnumerable<string?> coordinationDays) =>
        coordinationDays.Select(Weekday.Parse).DefaultIfEmpty(DayOfWeek.Monday).MinBy(d => ((int)d + 6) % 7);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var s = await sp.GetRequiredService<SettingsStore>().Get(db);
        var baseUrl = (sp.GetRequiredService<IConfiguration>()["Email:BaseUrl"] ?? "").TrimEnd('/');
        var now = clock.GetUtcNow();
        var local = Clock.Local(now, s);
        var today = DateOnly.FromDateTime(local.DateTime);
        var active = await db.Projects.AsNoTracking().Where(p => p.Status == ProjectStatus.Active).Select(p => new { p.Id, p.ProjectManagerId, p.CoordinationDay }).ToListAsync(ct);
        var pmRoles = await db.ProjectMembers.AsNoTracking().Where(m => m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM) && active.Select(a => a.Id).Contains(m.ProjectId))
            .Select(m => new { m.UserId, m.ProjectId }).ToListAsync(ct);
        var days = active.Select(a => (User: a.ProjectManagerId, a.CoordinationDay))
            .Concat(pmRoles.Select(m => (User: m.UserId, active.First(a => a.Id == m.ProjectId).CoordinationDay)))
            .GroupBy(x => x.User).ToDictionary(g => g.Key, g => SendDay(g.Select(x => x.CoordinationDay)));
        var prefs = await db.UserSettings.Where(x => days.Keys.Contains(x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive && days.Keys.Contains(u.Id)).Select(u => new { u.Id, u.Email, u.DisplayName }).ToListAsync(ct);
        int sent = 0;
        foreach (var u in users)
        {
            var pref = prefs.GetValueOrDefault(u.Id);
            if (pref is { WeeklySummaryEnabled: false } || days[u.Id] != local.DayOfWeek) continue; // FR-001: they can turn it off
            var at = TimeOnly.TryParseExact(pref?.DigestTimeLocal ?? s.DigestSendTimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(7, 0);
            if (TimeOnly.FromDateTime(local.DateTime) < at) continue;
            var scheduled = new DateTimeOffset(today.ToDateTime(at), Clock.Zone(s).GetUtcOffset(today.ToDateTime(at))).ToUniversalTime();
            if (pref?.LastWeeklySummaryAt is { } last && last >= scheduled) continue; // once a week
            var summary = await WeeklySummary.Build(db, u.Id, u.DisplayName.Split(' ')[0], today, pref?.LastWeeklySummaryAt ?? now.AddDays(-7), now, s, baseUrl);
            if (summary is not null && !string.IsNullOrEmpty(u.Email))
            {
                db.Emails.Add(new EmailMessage { UserId = u.Id, ToAddress = u.Email, Subject = summary.Subject, BodyText = summary.Body, Kind = "WeeklySummary", RequiredProjectIds = summary.RequiredProjectIds,
                    DedupKey = $"weekly:{u.Id}:{today:yyyy-MM-dd}", CreatedAt = now, NextAttemptAt = now });
                sent++;
            }
            if (pref is null) { pref = new UserSetting { UserId = u.Id }; db.UserSettings.Add(pref); prefs[u.Id] = pref; }
            pref.LastWeeklySummaryAt = now;
            await db.SaveChangesAsync(ct);
        }
        return new { sent, date = today };
    }
}
