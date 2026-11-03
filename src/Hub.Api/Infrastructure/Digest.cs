using System.Globalization;
using System.Collections.Immutable;
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
    sealed record ExceptionRow(Guid Id, Guid ProjectId, Guid AssessmentId, Guid BasisVersionId, Guid VerifierId,
        string LimitedWork, DateOnly ExpiresOn, string TargetType, Guid TargetId, Guid OwnerId);
    public sealed record Section(string Code, List<Row> Rows, int Total);
    public sealed record Updates(string ProjectNumber, string ProjectName, int Count, Dictionary<string, int> ByType, List<string> Top);
    public sealed record Result(string Subject, string Body, List<Section> Sections, List<Updates> ProjectUpdates)
    { public Guid[] RequiredProjectIds { get; init; } = []; }

    const int Cap = 10;

    /// Sections a person can switch off (FR-002, packet 020), in digest order.
    public static readonly string[] SectionCodes = ["overdue", "dueSoon", "blocked", "reviews", "decisions", "handoffs", "reviewPackages", "changes", "submissions", "allocations", "planning", "basisImpacts", "constraints", "commitments", "issueVerifications", "readinessExceptions", "attention", "milestones", "staff", "updates"];
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
        => await BuildCore(db, userId, firstName, today, since, now, s, baseUrl, off, null, null, [], CancellationToken.None);

    /// Enhanced formatter only: the caller/core owns current admission, immutable slot binding and persistence.
    public static async Task<NoticeDigestRenderedPayload?> Build(HubDb db, NoticeDigestBuildRequest request, CancellationToken ct = default)
    {
        NotificationDigestComposition.Validate(request);
        ct.ThrowIfCancellationRequested();
        if (!request.Settings.IsFlagOn("people_screens") || DigestJob.SectionsAllOff(request.SectionsOff.ToHashSet())) return null;
        var held = NotificationDigestComposition.HeldLines(request);
        var ledger = new NotificationDigestComposition.RenderLedger();
        var result = await BuildCore(db, request.UserId, request.FirstName, request.Today, request.Window.From, request.ReadAtUtc,
            request.Settings, request.BaseUrl, request.SectionsOff, request, ledger, held, ct);
        ct.ThrowIfCancellationRequested();
        return result is null ? null : ledger.Complete(request, result, held);
    }

    static async Task<Result?> BuildCore(HubDb db, Guid userId, string firstName, DateOnly today, DateTimeOffset since, DateTimeOffset now,
        OrgSettings s, string baseUrl, IReadOnlySet<string>? off, NoticeDigestBuildRequest? enhanced,
        NotificationDigestComposition.RenderLedger? ledger, ImmutableArray<NoticeHeldCandidate> held, CancellationToken ct)
    {
        off ??= new HashSet<string>();
        var roles = await db.UserRoles.Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync();
        var projects = await Projects(db, userId, roles.Contains(SystemRole.Admin) || roles.Contains(SystemRole.Executive));
        var pids = projects.Keys.ToList();
        string Num(Guid pid) => projects[pid].ProjectNumber;
        string TaskLink(Guid pid, Guid id) => $"{baseUrl}/projects/{Num(pid)}/tasks?panel=Task:{id}";
        string DelLink(Guid pid, Guid id) => $"{baseUrl}/projects/{Num(pid)}/deliverables?panel=Deliverable:{id}";
        static string D(DateOnly? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";
        Row ProjectRow(Guid projectId, Guid itemId, string? key, string name, string detail, string link)
        {
            var row = new Row(itemId, key, name, Num(projectId), detail, link);
            return ledger?.Project(row, projectId) ?? row;
        }

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
            ct.ThrowIfCancellationRequested();
            ledger?.Include(code, fresh);
            return new Section(code, fresh.Take(Cap).ToList(), fresh.Count);
        }

        var mine = tasks.Where(x => x.t.AssigneeId == userId).ToList();
        var overdue = Make("overdue", mine.Where(x => x.st.IsOverdue).OrderByDescending(x => x.st.DaysOverdue)
                .Select(x => ProjectRow(x.t.ProjectId, x.t.Id, x.t.Key, x.t.Name, Text.Get("digest.overdue_detail", D(x.t.DueDate), x.st.DaysOverdue), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.OwnerId == userId && x.st.IsOverdue)
                .Select(x => ProjectRow(x.d.ProjectId, x.d.Id, x.d.Key, x.d.Name, Text.Get("digest.overdue_detail", D(x.d.DueDate), x.st.DaysOverdue), DelLink(x.d.ProjectId, x.d.Id)))));
        var blocked = Make("blocked", mine.Where(x => x.st.IsBlocked).OrderByDescending(x => x.st.DaysBlocked)
            .Select(x => ProjectRow(x.t.ProjectId, x.t.Id, x.t.Key, x.t.Name, Text.Get("digest.blocked_detail", Blockers(x.st.BlockedBy)), TaskLink(x.t.ProjectId, x.t.Id))));
        var reviews = Make("reviews", tasks.Where(x => x.t.ReviewerId == userId && TaskStatuses.IsReview(x.t.Status)).OrderBy(x => x.t.ReviewRequestedAt)
                .Select(x => ProjectRow(x.t.ProjectId, x.t.Id, x.t.Key, x.t.Name, Text.Get("digest.review_detail", x.t.Status, D(x.t.DueDate)), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.ReviewerId == userId && x.d.Status == DeliverableStatus.InReview)
                .Select(x => ProjectRow(x.d.ProjectId, x.d.Id, x.d.Key, x.d.Name, Text.Get("digest.review_detail", x.d.Status, D(x.d.DueDate)), DelLink(x.d.ProjectId, x.d.Id)))));
        var decisionRows = Make("decisions", decisions.Where(x => x.st.IsOverdue || x.st.IsDueSoon).OrderBy(x => x.d.RequiredByDate)
            .Select(x => ProjectRow(x.d.ProjectId, x.d.Id, x.d.Key, x.d.Subject,
                x.st.IsOverdue ? Text.Get("digest.decision_overdue", D(x.d.RequiredByDate), x.st.DaysOverdue) : Text.Get("digest.decision_due", D(x.d.RequiredByDate)),
                $"{baseUrl}/projects/{Num(x.d.ProjectId)}/decisions?panel=Decision:{x.d.Id}")));
        var dueSoon = Make("dueSoon", mine.Where(x => x.st.IsDueSoon && !x.st.IsOverdue).OrderBy(x => x.t.DueDate)
                .Select(x => ProjectRow(x.t.ProjectId, x.t.Id, x.t.Key, x.t.Name, Text.Get("digest.due_detail", D(x.t.DueDate)), TaskLink(x.t.ProjectId, x.t.Id)))
            .Concat(dels.Where(x => x.d.OwnerId == userId && x.st.IsDueSoon && !x.st.IsOverdue)
                .Select(x => ProjectRow(x.d.ProjectId, x.d.Id, x.d.Key, x.d.Name, Text.Get("digest.due_detail", D(x.d.DueDate)), DelLink(x.d.ProjectId, x.d.Id)))));

        // PMs and leads: attention items routed to them, Critical and Warning, not snoozed.
        var snoozed = (await db.Snoozes.Where(z => pids.Contains(z.ProjectId) && z.EndedAt == null && z.SnoozedUntil > now).Select(z => new { z.RuleId, z.ItemId }).ToListAsync())
            .Select(z => (z.RuleId, z.ItemId)).ToHashSet();
        var leads = (await db.ProjectDisciplines.Where(d => pids.Contains(d.ProjectId) && d.LeadUserId == userId).Select(d => d.ProjectId).ToListAsync()).ToHashSet();
        var managed = projects.Values.Where(p => p.ProjectManagerId == userId || leads.Contains(p.Id)).Select(p => p.Id).ToList();
        var attention = (await db.Attention.AsNoTracking().Where(a => managed.Contains(a.ProjectId) && a.RouteToUserIds.Contains(userId)
                && (a.Severity == Severity.Critical || a.Severity == Severity.Warning)).ToListAsync())
            .Where(a => !snoozed.Contains((a.RuleId, a.ItemId))).OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).ToList();
        var attentionRows = Make("attention", attention.Select(a => ProjectRow(a.ProjectId, a.ItemId, a.ItemKey, a.ItemName ?? "", $"{a.Severity} {a.RuleId}: {a.Message}",
            $"{baseUrl}/projects/{Num(a.ProjectId)}")));

        // Milestones approaching in the person's projects (PM, or lead of a discipline with targeted deliverables).
        var horizon = today.AddDays(s.MilestoneApproachingDays);
        var targetedByLead = await db.Deliverables.Where(d => pids.Contains(d.ProjectId) && d.MilestoneId != null
            && db.ProjectDisciplines.Any(pd => pd.Id == d.ProjectDisciplineId && pd.LeadUserId == userId)).Select(d => d.MilestoneId!.Value).Distinct().ToListAsync();
        var ms = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date >= today && m.Date <= horizon
            && (db.Projects.Any(p => p.Id == m.ProjectId && p.ProjectManagerId == userId) || targetedByLead.Contains(m.Id))).OrderBy(m => m.Date).ToListAsync();
        var msStates = await db.MilestoneStates.Where(x => ms.Select(m => m.Id).Contains(x.MilestoneId)).ToDictionaryAsync(x => x.MilestoneId, x => x.Status);
        var milestones = Make("milestones", ms.Select(m => ProjectRow(m.ProjectId, m.Id, m.Key, m.Name,
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
                .Join(db.TaskStates, t => t.Id, st => st.TaskId, (t, st) => new { UserId = t.AssigneeId!.Value, t.ProjectId, st.IsOverdue, st.IsBlocked }).ToListAsync(ct);
            var stalled = await db.Tasks.AsNoTracking().Where(t => t.ReviewerId != null && rIds.Contains(t.ReviewerId.Value) && pids.Contains(t.ProjectId))
                .Join(db.TaskStates.Where(st => st.IsReviewStalled), t => t.Id, st => st.TaskId, (t, st) => new { UserId = t.ReviewerId!.Value, t.ProjectId }).ToListAsync(ct);
            foreach (var r in reports)
            {
                int od = work.Count(w => w.UserId == r.Id && w.IsOverdue), bl = work.Count(w => w.UserId == r.Id && w.IsBlocked), rv = stalled.Count(x => x.UserId == r.Id);
                if (od + bl + rv > 0)
                {
                    var row = new Row(r.Id, null, r.DisplayName, "", Text.Get("digest.staff_detail", od, bl, rv), $"{baseUrl}/my-work?userId={r.Id}");
                    ledger?.Staff(row, work.Where(w => w.UserId == r.Id && (w.IsOverdue || w.IsBlocked)).Select(w => w.ProjectId)
                        .Concat(stalled.Where(x => x.UserId == r.Id).Select(x => x.ProjectId)), r.Id);
                    staffRows.Add(row);
                }
            }
            var staffNotices = db.Notifications.AsNoTracking().Where(n => n.UserId == userId && n.EventType == NotificationEvents.StaffAssignment && n.CreatedAt > since
                    && (n.ProjectId == null || pids.Contains(n.ProjectId.Value))
                    && (n.RequiredProjectIds == null || n.RequiredProjectIds.Length > 0
                        && n.RequiredProjectIds.All(id => id != Guid.Empty && pids.Contains(id))));
            if (enhanced is not null) staffNotices = staffNotices.Where(n => n.CreatedAt <= enhanced.Window.Through);
            foreach (var notice in await staffNotices.OrderByDescending(n => n.CreatedAt).ToListAsync(ct))
            {
                var row = new Row(notice.Id, null, notice.Title, "", "", $"{baseUrl}{notice.LinkPath}");
                ledger?.Notification(row, notice);
                staffRows.Add(row);
            }
        }
        var staff = Make("staff", staffRows);

        // Project updates: changes by others on projects followed at All activity, minus ones that notified the person (ASG-06).
        var updates = new List<Updates>();
        var followed = off.Contains("updates") ? []
            : await db.Follows.Where(f => f.UserId == userId && f.Level == FollowLevel.AllActivity && pids.Contains(f.ProjectId)).Select(f => f.ProjectId).ToListAsync();
        foreach (var pid in followed.OrderBy(Num))
        {
            var activity = (await VisibleActivity(db, userId, pid)).Where(a => a.OccurredAt > since && a.ActorUserId != null && a.ActorUserId != userId);
            if (enhanced is not null) activity = activity.Where(a => a.OccurredAt <= enhanced.Window.Through);
            var rows = await activity.OrderByDescending(a => a.OccurredAt).Take(2000).ToListAsync(ct);
            var notified = await NotificationEndpoints.NotifiedKeys(db, userId, rows, pids);
            rows = rows.Where(r => r.CorrelationId is not { } c || !notified.Contains(c.ToString())).ToList();
            if (rows.Count == 0) continue;
            ledger?.Updates(pid, rows.Select(row => row.Id));
            var byType = rows.GroupBy(r => r.Categories.FirstOrDefault(c => ImportantCategories.Contains(c)) ?? "other").ToDictionary(g => g.Key, g => g.Count());
            var top = await ActivityEndpoints.Render(db, rows.Where(r => r.Categories.Any(c => ImportantCategories.Contains(c)) || r.Action == "Issued").Take(5).ToList());
            updates.Add(new Updates(Num(pid), projects[pid].Name, rows.Count, byType,
                top.Select(x => { dynamic v = x; return $"{v.ActorName ?? Text.Get("common.system")} {Text.Get($"digest.action.{v.Action}")} {v.ItemKey} {v.ItemName}: {v.Summary}".Trim(); }).ToList()));
        }

        var handoffRows = await db.Handoffs.AsNoTracking().Where(h => pids.Contains(h.ProjectId)
            && (h.SendingOwnerId == userId || h.ReceivingOwnerId == userId)
            && h.Status != HandoffStatus.Incorporated && h.Status != HandoffStatus.Cancelled).OrderBy(h => h.NeededBy).ToListAsync();
        var handoffs = Make("handoffs", handoffRows.Select(h => ProjectRow(h.ProjectId, h.Id, h.Key, h.Title,
            Text.Get("handoff.digest_detail", h.Status, D(h.NeededBy)), $"{baseUrl}/projects/{Num(h.ProjectId)}/handoffs?panel=Handoff:{h.Id}")));
        var packages = await ReviewIssuedSources.VisiblePackages(db, s.IsFlagOn("register_screens")).AsNoTracking().Where(p => pids.Contains(p.ProjectId) && (p.Status == ReviewStatus.InReview || p.Status == ReviewStatus.ChangesRequired)
            && (p.CoordinatorId == userId || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == userId && a.Status != DisciplineReviewStatus.Approved)
                || db.ReviewFindings.Any(f => f.RoundId == p.CurrentRoundId && (f.ResolverId == userId || f.VerifierId == userId) && (f.Status == FindingStatus.Open || f.Status == FindingStatus.Responded)))).ToListAsync();
        var reviewPackages = Make("reviewPackages", packages.Select(p => ProjectRow(p.ProjectId, p.Id, p.Key, p.Title, Text.Get("review.digest_detail", p.Status, p.RoundNumber), $"{baseUrl}/projects/{Num(p.ProjectId)}/reviews?panel=ReviewPackage:{p.Id}")));
        var notices = await db.ChangeNotices.AsNoTracking().Where(c => pids.Contains(c.ProjectId) && c.Status == ChangeStatus.Open
            && (c.OwnerId == userId || db.ChangeAssessments.Any(a => a.ChangeNoticeId == c.Id && (a.OwnerId == userId || a.ReviewerId == userId) && a.Status != AssessmentStatus.Resolved))).OrderBy(c => c.AssessmentDueDate).ToListAsync();
        var changes = Make("changes", notices.Select(c => ProjectRow(c.ProjectId, c.Id, c.Key, c.Title, Text.Get("change.digest_detail", c.Status, D(c.AssessmentDueDate)), $"{baseUrl}/projects/{Num(c.ProjectId)}/changes?panel=ChangeNotice:{c.Id}")));
        // Pending actions remain digest items even when their assignment already produced an in-app notice (FR-MDC-06).
        var submissionRows = await db.SubmissionPackages.AsNoTracking().Where(p => pids.Contains(p.ProjectId) &&
            (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready) &&
            (p.CoordinatorId == userId || db.SubmissionChecks.Any(c => c.PackageId == p.Id && c.ManifestVersion == p.ManifestVersion &&
                c.OwnerId == userId && c.Status != SubmissionCheckStatus.Pass && c.Status != SubmissionCheckStatus.NotApplicable)))
            .OrderBy(p => p.TargetDate).ThenBy(p => p.Id).ToListAsync();
        var submissions = Make("submissions", submissionRows.Select(p => ProjectRow(p.ProjectId, p.Id, p.Key, p.Title,
            Text.Get("digest.coordination_detail", p.Status, D(p.TargetDate)), $"{baseUrl}/projects/{Num(p.ProjectId)}/submissions?panel=SubmissionPackage:{p.Id}")));
        var managedProjectIds = db.Projects.Where(p => p.ProjectManagerId == userId ||
            db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM))).Select(p => p.Id);
        var allocationRows = await AllocationEndpoints.VisibleQuery(db, new Actor(userId, true, roles.ToHashSet()), managedProjectIds)
            .Where(a => pids.Contains(a.ProjectId) && a.Status == AllocationStatus.Proposed).OrderBy(a => a.FromDate).ThenBy(a => a.Id)
            .Join(db.Users.AsNoTracking(), a => a.PersonId, u => u.Id, (a, u) => new { a, u.DisplayName }).ToListAsync();
        var allocations = Make("allocations", allocationRows.Select(x => ProjectRow(x.a.ProjectId, x.a.Id, null, x.DisplayName,
            Text.Get("digest.coordination_detail", x.a.Purpose, D(x.a.FromDate)), $"{baseUrl}/projects/{Num(x.a.ProjectId)}/allocations?allocation={x.a.Id}")));
        // Planning is person-scoped and therefore never enters a project digest or activity
        // feed.  It is included only in the recipient's own daily digest, and only for
        // entries the recipient owns, as required by the stale-plan rule.
        var planningRows = await Hub.Api.Features.PlanningEndpoints.VisibleForUser(db, userId)
            .Where(e => e.CreatedBy == userId && e.EndWeek >= Workload.WeekOf(today)
                && e.LastValidatedAt < now.AddDays(-s.PlanningStaleDays))
            .OrderBy(e => e.EndWeek).ThenBy(e => e.Id).Take(Cap + 1).ToListAsync();
        var planning = Make("planning", planningRows.Select(e =>
        {
            var row = new Row(e.Id, null, e.Label, "", Text.Get("digest.planning_stale", e.LastValidatedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                e.PersonId == userId ? $"{baseUrl}/my-work?myWeek={e.StartWeek:yyyy-MM-dd}" : $"{baseUrl}/planner?personId={e.PersonId}&entry={e.Id}");
            return ledger?.Planning(row, e.Id) ?? row;
        }));
        var impactRows = await db.BasisImpactAssessments.AsNoTracking().Where(a => pids.Contains(a.ProjectId) && a.OwnerId == userId &&
            a.Status != AssessmentStatus.Unaffected && a.Status != AssessmentStatus.Resolved)
            .Join(db.DesignBasisVersions, a => a.OldVersionId, v => v.Id, (a, v) => new { a, v.EntryId })
            .Join(db.DesignBasisEntries, x => x.EntryId, e => e.Id, (x, e) => new { x.a, e.Id, e.Key, e.Title }).OrderBy(x => x.a.Id).ToListAsync();
        var basisImpacts = Make("basisImpacts", impactRows.Select(x => ProjectRow(x.a.ProjectId, x.a.Id, x.Key, x.Title, x.a.Status,
            $"{baseUrl}/projects/{Num(x.a.ProjectId)}/design-basis?basis={x.Id}")));
        var constraintRows = await db.WorkConstraints.AsNoTracking().Where(c => pids.Contains(c.ProjectId) &&
            (c.State == ConstraintState.Open && c.RemovalOwnerId == userId || c.State == ConstraintState.ResolutionProposed && c.AffectedOwnerId == userId))
            .OrderBy(c => c.NeededBy).ThenBy(c => c.Id).ToListAsync();
        var constraints = Make("constraints", constraintRows.Select(c => ProjectRow(c.ProjectId, c.Id, c.Key, c.Description,
            Text.Get("digest.coordination_detail", c.State, D(c.NeededBy)), $"{baseUrl}/projects/{Num(c.ProjectId)}/readiness?panel=WorkConstraint:{c.Id}")));
        var commitmentRows = await db.OutputCommitments.AsNoTracking().Where(c => pids.Contains(c.ProjectId) && c.PerformerId == userId &&
            (c.State == CommitmentState.Proposed || c.State == CommitmentState.Committed)).OrderBy(c => c.TargetDate).ThenBy(c => c.Id).ToListAsync();
        var commitments = Make("commitments", commitmentRows.Select(c => ProjectRow(c.ProjectId, c.Id, c.Key, c.IntendedOutput,
            Text.Get("digest.coordination_detail", c.State, D(c.TargetDate)), $"{baseUrl}/projects/{Num(c.ProjectId)}/readiness?panel=OutputCommitment:{c.Id}")));
        var verificationRows = await db.IssueVerifications.AsNoTracking().Where(v => pids.Contains(v.ProjectId) && v.VerifierId == userId &&
            v.Status == IssueVerificationStatus.Proposed && v.Id == db.IssueVerifications.Where(latest => latest.IssueId == v.IssueId)
                .OrderByDescending(latest => latest.IssueRowVersion).ThenByDescending(latest => latest.Id).Select(latest => latest.Id).First())
            .Join(db.Issues.Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress), v => v.IssueId, i => i.Id,
                (v, i) => new { v, i.Id, i.Key, i.Title, i.TargetResolutionDate }).OrderBy(x => x.TargetResolutionDate).ThenBy(x => x.v.Id).ToListAsync();
        var issueVerifications = Make("issueVerifications", verificationRows.Select(x => ProjectRow(x.v.ProjectId, x.v.Id, x.Key, x.Title,
            Text.Get("digest.coordination_detail", x.v.Status, D(x.TargetResolutionDate)), $"{baseUrl}/projects/{Num(x.v.ProjectId)}/issues?panel=Issue:{x.Id}")));
        var exceptionCandidates = await db.ReadinessExceptions.AsNoTracking().Where(e => pids.Contains(e.ProjectId) && e.VerifierId == userId && e.ExpiresOn >= today
            && e.Id == db.ReadinessExceptions.Where(latest => latest.AssessmentId == e.AssessmentId)
                .OrderByDescending(latest => latest.CreatedAt).ThenByDescending(latest => latest.Id).Select(latest => latest.Id).First())
            .Join(db.ReadinessAssessments, e => e.AssessmentId, a => a.Id, (e, a) => new ExceptionRow(e.Id, e.ProjectId, e.AssessmentId,
                e.BasisVersionId, e.VerifierId, e.LimitedWork, e.ExpiresOn, a.TargetType, a.TargetId, a.OwnerId)).ToListAsync();
        var exceptionRows = new List<ExceptionRow>();
        foreach (var candidate in exceptionCandidates)
        {
            var targetOwner = candidate.TargetType == "Task"
                ? await db.Tasks.AsNoTracking().Where(t => t.Id == candidate.TargetId && t.ProjectId == candidate.ProjectId && t.DeletedAt == null).Select(t => (Guid?)t.AssigneeId).SingleOrDefaultAsync()
                : await db.Deliverables.AsNoTracking().Where(d => d.Id == candidate.TargetId && d.ProjectId == candidate.ProjectId && d.DeletedAt == null).Select(d => d.OwnerId).SingleOrDefaultAsync();
            if (targetOwner is not { } owner || owner != candidate.OwnerId || owner == candidate.VerifierId) continue;
            if (!await Coordination.People(db, projects[candidate.ProjectId]).AnyAsync(u => u.Id == owner)) continue;

            var basis = await ReadinessEndpoints.EvaluateBasis(db, projects[candidate.ProjectId], candidate.TargetType, candidate.TargetId);
            if (basis.ExceptionBasisVersionId != candidate.BasisVersionId) continue;
            var exceptionScope = await db.DesignBasisVersions.AsNoTracking().Where(v => v.Id == candidate.BasisVersionId)
                .Select(v => v.Scope).SingleOrDefaultAsync();
            if (exceptionScope is null) continue;
            if (!await db.BasisAssumptionDispositions.AsNoTracking().AnyAsync(d => d.ProjectId == candidate.ProjectId &&
                    d.VersionId == candidate.BasisVersionId && d.OwnerId == owner && d.ApprovedBy != owner &&
                    d.ExpiresOn >= candidate.ExpiresOn && d.Scope.ToLower() == exceptionScope.ToLower())) continue;
            if (!await Coordination.People(db, projects[candidate.ProjectId]).AnyAsync(u => u.Id == candidate.VerifierId)) continue;
            exceptionRows.Add(candidate);
        }
        exceptionRows = exceptionRows.OrderBy(x => x.ExpiresOn).ThenBy(x => x.Id).ToList();
        var readinessExceptions = Make("readinessExceptions", exceptionRows.Select(x => ProjectRow(x.ProjectId, x.Id, null, x.LimitedWork,
            Text.Get("digest.exception_detail", D(x.ExpiresOn)), x.TargetType == "Task" ? TaskLink(x.ProjectId, x.TargetId) : DelLink(x.ProjectId, x.TargetId))));
        var sections = new List<Section> { overdue, dueSoon, blocked, reviews, decisionRows, handoffs, reviewPackages, changes,
            submissions, allocations, basisImpacts, constraints, commitments, issueVerifications, readinessExceptions, attentionRows, milestones, staff }.Where(x => x.Total > 0).ToList();
        if (planning.Total > 0) sections.Add(planning);
        // FR-ADR-04: one line for every Admin while the Admin review is overdue (never recorded counts as overdue). It is a line,
        // not a section, so it has no switch; with `flag.admin_review` off it is absent (FR-ADR-08). It is something to say.
        string? reviewLine = null;
        if (roles.Contains(SystemRole.Admin) && s.IsFlagOn(AdminReviewEndpoints.Flag))
        {
            var (dueOn, overdueReview) = AdminReviewEndpoints.Due(await db.Set<AdminReview>().MaxAsync(r => (DateTimeOffset?)r.ReviewedAt), s, today);
            if (overdueReview) reviewLine = $"{(dueOn is { } d ? Text.Get("digest.adminReview", D(d)) : Text.Get("digest.adminReviewNone"))} {baseUrl}/admin/review";
        }
        if (reviewLine is not null) ledger?.AdminReview();
        if (sections.Count == 0 && updates.Count == 0 && reviewLine is null && held.IsEmpty) return null; // AC-NOT-04: nothing to say, nothing sent

        var parts = new List<string>();
        if (overdue.Total > 0) parts.Add(Text.Get("digest.subject_overdue", overdue.Total));
        if (reviews.Total > 0) parts.Add(Text.Get(reviews.Total == 1 ? "digest.subject_review" : "digest.subject_reviews", reviews.Total));
        if (blocked.Total > 0) parts.Add(Text.Get("digest.subject_blocked", blocked.Total));
        if (parts.Count == 0 && sections.FirstOrDefault() is { } first) parts.Add(Text.Get($"digest.subject_{first.Code}", first.Total));
        var updateCount = updates.Sum(u => u.Count);
        var updatesText = updateCount == 0 ? null : Text.Get(updateCount == 1 ? "digest.subject_update" : "digest.subject_updates", updateCount);
        if (parts.Count == 0 && updatesText is null && reviewLine is not null) parts.Add(Text.Get("digest.subject_adminReview"));
        if (parts.Count == 0 && updatesText is null && !held.IsEmpty) parts.Add(Text.Get(held.Length == 1 ? "digest.subject_heldOne" : "digest.subject_held", held.Length));
        var subject = Text.Get("digest.subject", parts.Count > 0 ? string.Join(", ", parts) + (updatesText is null ? "" : " · " + updatesText) : updatesText ?? "");

        var body = new StringBuilder();
        body.AppendLine(Text.Get("digest.greeting", firstName, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))).AppendLine();
        if (reviewLine is not null) body.AppendLine(reviewLine).AppendLine();
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
        if (!held.IsEmpty)
        {
            body.AppendLine($"{Text.Get("digest.heldTitle").ToUpperInvariant()} ({held.Length})");
            foreach (var parent in held)
            {
                body.AppendLine($"- {parent.Subject}");
                foreach (var line in parent.BodyText.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n'))
                    body.AppendLine($"  {line}");
                if (parent.LinkPath is { } link) body.AppendLine($"  {baseUrl}{link}");
            }
            body.AppendLine();
        }
        body.AppendLine(Text.Get("digest.footer", $"{baseUrl}/my-work", $"{baseUrl}/preferences"));
        return new Result(subject, body.ToString(), sections, updates) { RequiredProjectIds = [.. pids] };
    }

    /// Apply the same source-level privacy boundary as project activity reads when no request Access is available.
    internal static async Task<IQueryable<ActivityLog>> VisibleActivity(HubDb db, Guid userId, Guid projectId)
    {
        var isAdmin = await db.UserRoles.AnyAsync(r => r.UserId == userId && r.Role == SystemRole.Admin);
        var isSupervisor = await db.UserRoles.AnyAsync(r => r.UserId == userId && r.Role == SystemRole.Supervisor);
        var isPm = isAdmin || await db.Projects.AnyAsync(p => p.Id == projectId && p.ProjectManagerId == userId)
            || await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM));
        var leadDisciplineIds = await db.ProjectDisciplines.Where(d => d.ProjectId == projectId && d.LeadUserId == userId).Select(d => d.Id).ToListAsync();
        var directReportIds = isSupervisor
            ? await db.Users.Where(u => u.SupervisorId == userId).Select(u => u.Id).ToListAsync()
            : [];
        var entries = db.TimeEntries.IgnoreQueryFilters().Where(e => e.ProjectId == projectId &&
            (e.UserId == userId || isPm ||
             (leadDisciplineIds.Count > 0 && db.Tasks.IgnoreQueryFilters().Any(t => t.Id == e.TaskId && leadDisciplineIds.Contains(t.ProjectDisciplineId))) ||
             (directReportIds.Count > 0 && directReportIds.Contains(e.UserId)))).Select(e => (Guid?)e.Id);
        return ActivityScreenFields.VisibleStored(db, db.ActivityLog.AsNoTracking()).Where(a => a.ProjectId == projectId
            && (a.ItemType != ItemType.TimeEntry || entries.Contains(a.ItemId))
            && (a.ItemType != ItemType.CalendarEvent || !db.CalendarEvents.Any(e => e.Id == a.ItemId && e.Visibility == EventVisibility.Private)));
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
    const int BatchSize = 100;
    public string Name => "daily-digest";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromMinutes(5), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var clock = sp.GetRequiredService<TimeProvider>();
        var baseUrl = (sp.GetRequiredService<IConfiguration>()["Email:BaseUrl"] ?? "").TrimEnd('/');
        await using var scan = sp.CreateAsyncScope();
        var scanner = scan.ServiceProvider.GetRequiredService<HubDb>();
        Guid? cursor = null;
        int sent = 0, empty = 0, held = 0, scanned = 0, enhancedQueued = 0, enhancedEmpty = 0, dormant = 0, notEligible = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var users = scanner.Users.AsNoTracking().Where(user => user.IsActive || scanner.NoticeDeliverySlots.Any(slot => slot.UserId == user.Id
                && slot.Kind == NoticeSlotKind.Digest && (slot.State == NoticeSlotState.Ready || slot.State == NoticeSlotState.Queued
                    || slot.State == NoticeSlotState.Paused || slot.State == NoticeSlotState.Uncertain)));
            if (cursor is { } after) users = users.Where(user => user.Id.CompareTo(after) > 0);
            var batch = await users.OrderBy(user => user.Id).Select(user => user.Id).Take(BatchSize).ToArrayAsync(ct);
            if (batch.Length == 0) break;
            foreach (var userId in batch)
            {
                ct.ThrowIfCancellationRequested();
                // Every recipient gets a fresh tracker and system audit context. A fault aborts this run after rollback;
                // no pending mutations or caught transaction failure can leak into the next recipient's save.
                await using var scope = sp.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
                var db = scope.ServiceProvider.GetRequiredService<HubDb>();
                var outcome = await Tx.Run(db, async () =>
                {
                    var context = await NotificationDelivery.LockRecipientAsync(db, userId, ct);
                    var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context, ct);
                    if (!policy.Settings.IsFlagOn("people_screens"))
                    {
                        var legacy = await LegacyRecipient(context, policy, clock, baseUrl, ct);
                        // No cached job-start flag can choose legacy. If the live flag changed during the read-only build,
                        // discard that result and use the enhanced path before staging any legacy mail/watermark.
                        if (legacy != "enhanced") return (Queued: legacy == "queued" ? 1 : 0, Empty: legacy == "empty" ? 1 : 0,
                            Held: legacy == "held" ? 1 : 0, Enhanced: false, Dormant: false, NotEligible: false);
                    }
                    var now = clock.GetUtcNow();
                    var due = await NotificationDigestComposition.ReadDueAsync(context, now, ct);
                    int queued = 0, noContent = 0;
                    foreach (var existing in due.Existing.Where(slot => slot.Work is NoticeDigestWorkKind.Compose or NoticeDigestWorkKind.Recompose))
                    {
                        var result = await NotificationDigestComposition.ComposeAsync(context, existing.SlotId, clock.GetUtcNow(), baseUrl, ct);
                        if (result.Decision == NoticeDigestCompositionDecision.Queued) queued++;
                        if (result.Decision == NoticeDigestCompositionDecision.Empty) noContent++;
                    }
                    // The current due reader is repeated after existing work, since a successful preparation can advance its watermark.
                    var slot = await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, clock.GetUtcNow(), ct);
                    if (slot is not null && !due.Existing.Any(existing => existing.SlotId == slot.Id))
                    {
                        var result = await NotificationDigestComposition.ComposeAsync(context, slot.Id, clock.GetUtcNow(), baseUrl, ct);
                        if (result.Decision == NoticeDigestCompositionDecision.Queued) queued++;
                        if (result.Decision == NoticeDigestCompositionDecision.Empty) noContent++;
                    }
                    await db.SaveChangesAsync(ct); // Slot, safe carry transfer, watermark, membership and immutable email commit together.
                    return (Queued: queued, Empty: noContent,
                        Held: due.CreationDecision is "HeldAway" or "PendingAvailability" or "AllSectionsOff" ? 1 : 0,
                        Enhanced: true, Dormant: due.CreationDecision == "Dormant",
                        NotEligible: due.CreationDecision is "RecipientInactive" or "Disabled" or "NoAddress");
                });
                scanned++; sent += outcome.Queued; empty += outcome.Empty; held += outcome.Held;
                if (outcome.Enhanced) { enhancedQueued += outcome.Queued; enhancedEmpty += outcome.Empty; }
                if (outcome.Dormant) dormant++;
                if (outcome.NotEligible) notEligible++;
            }
            cursor = batch[^1];
            if (batch.Length < BatchSize) break;
        }
        return new { sent, empty, held, scanned, enhancedQueued, enhancedEmpty, dormant, notEligible }; // sent remains queued payloads, never provider acceptance.
    }

    static async Task<string> LegacyRecipient(NoticeRecipientContext context, NoticeRecipientPolicy policy, TimeProvider clock,
        string baseUrl, CancellationToken ct)
    {
        var db = context.Db;
        if (!policy.IsActive) return "skipped";
        var now = clock.GetUtcNow(); var s = policy.Settings; var local = Clock.Local(now, s); var today = DateOnly.FromDateTime(local.DateTime);
        if (!s.WeekendDigests && local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return "skipped";
        var user = await db.Users.AsNoTracking().SingleAsync(row => row.Id == context.UserId, ct);
        var pref = await db.UserSettings.SingleOrDefaultAsync(row => row.UserId == user.Id, ct);
        if (pref is { DigestEnabled: false }) return "skipped";
        var off = JsonSerializer.Deserialize<string[]>(pref?.DigestSectionsOff ?? "[]")!.ToHashSet();
        if (SectionsAllOff(off)) return "skipped";
        var at = TimeOnly.TryParseExact(pref?.DigestTimeLocal ?? s.DigestSendTimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : new TimeOnly(7, 0);
        if (TimeOnly.FromDateTime(local.DateTime) < at) return "skipped";
        var scheduled = new DateTimeOffset(today.ToDateTime(at), Clock.Zone(s).GetUtcOffset(today.ToDateTime(at))).ToUniversalTime();
        if (pref?.LastDigestAt is { } last && last >= scheduled) return "skipped";
        // Enhanced captured work stays linked and dormant while off; it is never converted to an unlinked same-day legacy payload.
        if (await db.NoticeDeliverySlots.AsNoTracking().AnyAsync(slot => slot.UserId == user.Id && slot.Kind == NoticeSlotKind.Digest && slot.LocalDate == today, ct)) return "skipped";
        var prefs = pref is null ? new Dictionary<Guid, UserSetting>() : new Dictionary<Guid, UserSetting> { [user.Id] = pref };
        if ((await HeldPeople(db, [user.Id], prefs, today, s)).Contains(user.Id)) return "held";
        var digest = await Digest.Build(db, user.Id, user.DisplayName.Split(' ')[0], today, pref?.LastDigestAt ?? now.AddDays(-1), now, s, baseUrl, off);
        if ((await NotificationDelivery.ReadRecipientPolicyAsync(context, ct)).Settings.IsFlagOn("people_screens")) return "enhanced";
        var queued = digest is not null && !string.IsNullOrEmpty(user.Email);
        if (queued)
            db.Emails.Add(new EmailMessage { UserId = user.Id, ToAddress = user.Email, Subject = digest!.Subject, BodyText = digest.Body,
                Kind = "Digest", RequiredProjectIds = digest.RequiredProjectIds, DedupKey = $"digest:{user.Id}:{today:yyyy-MM-dd}", CreatedAt = now, NextAttemptAt = now });
        pref ??= await UserPreferences.GetOrCreate(db, user.Id, ct);
        pref.LastDigestAt = now;
        await db.SaveChangesAsync(ct);
        return queued ? "queued" : "empty";
    }

    // FR-DIG-01: share the planner's day rule with the watchdog; a deliberate hold is not a late digest.
    internal static async Task<HashSet<Guid>> HeldPeople(HubDb db, IEnumerable<Guid> people,
        IReadOnlyDictionary<Guid, UserSetting> prefs, DateOnly today, OrgSettings settings)
    {
        var ids = people.Where(id => prefs.GetValueOrDefault(id)?.HoldDigestWhenAway != false).ToArray();
        if (ids.Length == 0) return [];
        var days = await PersonCapacity.Load(db, ids, today, today, settings);
        return days.Where(d => d.Value.Normal > 0 && d.Value.Available == 0).Select(d => d.Key.Item1).ToHashSet();
    }

    internal static bool SectionsAllOff(HashSet<string> off) => Digest.SectionCodes.All(off.Contains);
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
            var changes = await (await Digest.VisibleActivity(db, userId, p.Id)).Where(a => a.OccurredAt > since && a.ActorUserId != null && a.ActorUserId != userId)
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
            if (pref is null) { pref = await UserPreferences.GetOrCreate(db, u.Id, ct); prefs[u.Id] = pref; }
            pref.LastWeeklySummaryAt = now;
            await db.SaveChangesAsync(ct);
        }
        return new { sent, date = today };
    }
}
