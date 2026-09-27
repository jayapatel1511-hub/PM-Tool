namespace Hub.Domain;

// The deterministic rules engine (§10.3, §12.6, §12.12, §15, §16): one pure function from a project's data,
// "today" and the organisation settings to every derived indicator, status, health colour and attention item,
// each with the rule and values that produced it (constitution II). No I/O, no clock, no randomness.

public sealed record ProjectSnap(Guid Id, string Number, string Status, Guid PrimaryPmId, IReadOnlyList<Guid> PmUserIds,
    string? HealthOverride, DateTimeOffset? OverrideExpiresAt, DateTimeOffset Now);
public sealed record UserSnap(Guid Id, string Name, bool IsActive, Guid? SupervisorId);
public sealed record DisciplineSnap(Guid Id, string Name, Guid? LeadId, bool IsActive, int SortOrder);
public sealed record MilestoneSnap(Guid Id, string Key, string Name, string Type, DateOnly? Date, DateOnly? OriginalDate, bool IsComplete, bool IsCancelled, Guid? DisciplineId);
public sealed record DeliverableSnap(Guid Id, string Key, string Name, Guid DisciplineId, Guid? OwnerId, Guid? ReviewerId, Guid? MilestoneId,
    DateOnly? StartDate, DateOnly? DueDate, DateOnly? OriginalDueDate, string Status, string Priority, DateOnly LastActivity, DateOnly? IssuedDate = null);
public sealed record TaskSnap(Guid Id, string Key, string Name, Guid DisciplineId, Guid? DeliverableId, Guid? MilestoneId, Guid? AssigneeId, Guid? ReviewerId,
    string Status, string Priority, DateOnly? StartDate, DateOnly? DueDate, int ProgressPct, decimal? EstimatedHours, string? ManualBlockType,
    string? ManualBlockReason, DateOnly? ManualBlockSince, DateOnly LastActivity, DateOnly? ReviewRequestedOn, int DueDateChangeCount, DateOnly? CompletedOn,
    IReadOnlyList<Guid>? Collaborators = null);
public sealed record DependencySnap(Guid Id, Guid PredecessorId, Guid SuccessorId, int LagDays = 0);
public sealed record DecisionSnap(Guid Id, string Key, string Subject, string Status, DateOnly RequiredBy, string Impact, Guid? OwnerUserId, Guid RequestedById,
    IReadOnlyList<Guid> BlockedTaskIds);
public sealed record IssueSnap(Guid Id, string Key, string Title, string Status, string Severity, Guid OwnerId, DateOnly? TargetDate, Guid? DisciplineId);
public sealed record RiskSnap(Guid Id, string Key, string Title, string Status, int Probability, int Impact, DateOnly? ReviewDate, Guid OwnerId, Guid? DisciplineId);
public sealed record ActionSnap(Guid Id, string Key, string Text, string Status, string OwnerType, Guid? OwnerUserId, Guid? OwnerDisciplineId, DateOnly? DueDate);
public sealed record SnoozeSnap(string RuleId, string ItemType, Guid ItemId, DateTimeOffset Until, string SeverityAtSnooze);

public sealed record EvalInput(
    ProjectSnap Project, DateOnly Today, OrgSettings Settings,
    IReadOnlyList<UserSnap> Users, IReadOnlyList<DisciplineSnap> Disciplines, IReadOnlyList<MilestoneSnap> Milestones,
    IReadOnlyList<DeliverableSnap> Deliverables, IReadOnlyList<TaskSnap> Tasks, IReadOnlyList<DependencySnap> Dependencies,
    IReadOnlyList<DecisionSnap> Decisions, IReadOnlySet<Guid> ActiveMemberIds,
    IReadOnlyDictionary<Guid, DateOnly>? PreviousBlockedSince = null, IReadOnlyList<SnoozeSnap>? Snoozes = null,
    IReadOnlyList<IssueSnap>? Issues = null, IReadOnlyList<RiskSnap>? Risks = null, IReadOnlyList<ActionSnap>? Actions = null,
    IReadOnlyList<DependencySnap>? DeliverableDependencies = null, WorkCalendar? Calendar = null);

public sealed record Blocker(string Type, Guid? Id, string? Key, string? Name, string? Status, DateOnly? Due, bool Overdue, Guid? OwnerId, string? Reason, bool Blocking);
public sealed record Reason(string Text, string? Rule = null, string? Colour = null, object? Threshold = null, IReadOnlyDictionary<string, object?>? Values = null);

public sealed record TaskResult(Guid TaskId, bool IsOverdue, int DaysOverdue, bool IsDueSoon, bool IsWaiting, bool IsBlocked, DateOnly? BlockedSince, int DaysBlocked,
    IReadOnlyList<Blocker> Blockers, bool IsBlocking, IReadOnlyList<Guid> BlockingTaskIds, bool IsStale, int StaleDays, bool IsUnassigned, bool IsMissingDueDate,
    bool IsDateInconsistent, IReadOnlyList<Reason> Inconsistencies, bool IsInactiveOwner, bool IsHeldPastDue, bool IsReviewStalled,
    IReadOnlyList<Guid> AffectedMilestoneIds, IReadOnlyList<string> Notes);
public sealed record DeliverableResult(Guid DeliverableId, int? ProgressPct, int TaskTotal, int TaskComplete, int TaskCancelled, int TaskOpen, int TaskOverdue,
    int TaskBlocked, decimal EstimatedHoursTotal, decimal RemainingHours, bool IsOverdue, int DaysOverdue, bool IsDueSoon, bool IsAtRisk, IReadOnlyList<Reason> AtRiskReasons,
    bool IsUnassigned, bool IsStale, bool IsDateInconsistent, IReadOnlyList<Reason> Inconsistencies, int SlipDays, bool IsInactiveOwner, bool IssuedWithOpenWork,
    bool MilestoneCancelled, bool IsWaiting, bool IsBlocked, IReadOnlyList<Blocker> Blockers, IReadOnlyList<Guid> DerivedPredecessorIds, IReadOnlyList<Guid> DerivedSuccessorIds,
    int BlockingCount = 0);
public sealed record MilestoneResult(Guid MilestoneId, string? Status, IReadOnlyList<Reason> Reasons, int? DaysRemaining, int SlipDays, int DeliverableTotal,
    int DeliverableIssued, int TaskTotal, int TaskComplete, int TaskOpen, int TaskOverdue, int TaskBlocked);
public sealed record DecisionResult(Guid DecisionId, bool IsOverdue, int DaysOverdue, bool IsDueSoon, IReadOnlyList<Guid> BlockingTaskIds, bool IsInactiveOwner);
public sealed record HealthResult(string Health, IReadOnlyList<Reason> Reasons, IReadOnlyDictionary<string, int> Inputs);
public sealed record DisciplineResult(Guid DisciplineId, string Name, Guid? LeadId, int SortOrder, string Health, IReadOnlyList<Reason> Reasons, int Open, int Overdue,
    int Blocked, int Waiting, int DeliverablesDue14, string? NextDueKey, string? NextDueName, DateOnly? NextDueDate, string? NextDueType, Guid? NextDueId);
public sealed record AttentionResult(string RuleId, string Severity, string ItemType, Guid ItemId, string? ItemKey, string? ItemName, string Message,
    Reason Why, IReadOnlyList<Guid> RouteTo, Guid? OwnerId, Guid? DisciplineId, int DaysOverdueOrBlocked, string? Priority, DateOnly? DueDate, bool Snoozed);
public sealed record ProjectResult(HealthResult Health, IReadOnlyList<DisciplineResult> Disciplines, IReadOnlyDictionary<string, int> Counts,
    Guid? NextMilestoneId, Guid? NextSubmissionId, int? ProgressPct);
public sealed record EvalResult(IReadOnlyList<TaskResult> Tasks, IReadOnlyList<DeliverableResult> Deliverables, IReadOnlyList<MilestoneResult> Milestones,
    IReadOnlyList<DecisionResult> Decisions, ProjectResult Project, IReadOnlyList<AttentionResult> Attention);

public static class Evaluator
{
    public static bool IsTaskTerminal(string s) => TaskStatuses.IsTerminal(s);

    public static EvalResult Evaluate(EvalInput i)
    {
        var s = i.Settings;
        var today = i.Today;
        var active = i.Project.Status == ProjectStatus.Active; // G-05: only Active projects are evaluated
        var cal = s.WorkingDaysEnabled ? i.Calendar ?? WorkCalendar.Weekdays : null;
        int Until(DateOnly d) => cal is null ? d.DayNumber - today.DayNumber : cal.Between(today, d);        // "within N days" (§10.6)
        int Since(DateOnly d) => cal is null ? today.DayNumber - d.DayNumber : cal.Between(d, today);
        DateOnly After(DateOnly d, int lag) => lag <= 0 ? d : cal is null ? d.AddDays(lag) : cal.Add(d, lag); // a lag counts working days when on (packet 021)
        var users = i.Users.ToDictionary(u => u.Id);
        bool Inactive(Guid? id) => id is { } x && users.TryGetValue(x, out var u) && !u.IsActive;
        var tasks = i.Tasks.ToDictionary(t => t.Id);
        var deliverables = i.Deliverables.ToDictionary(d => d.Id);
        var milestones = i.Milestones.ToDictionary(m => m.Id);
        var disciplines = i.Disciplines.ToDictionary(d => d.Id);
        var preds = i.Dependencies.Where(d => tasks.ContainsKey(d.PredecessorId) && tasks.ContainsKey(d.SuccessorId)).ToLookup(d => d.SuccessorId);
        var succs = i.Dependencies.Where(d => tasks.ContainsKey(d.PredecessorId) && tasks.ContainsKey(d.SuccessorId)).ToLookup(d => d.PredecessorId);
        var decisionLinks = i.Decisions.SelectMany(d => d.BlockedTaskIds.Select(t => (Task: t, Decision: d))).ToLookup(x => x.Task, x => x.Decision);
        Guid? MilestoneOf(TaskSnap t) => t.DeliverableId is { } did && deliverables.TryGetValue(did, out var d) ? d.MilestoneId : t.MilestoneId;

        bool Satisfied(DependencySnap e)
        {
            var p = tasks[e.PredecessorId];
            if (p.Status == TaskStatuses.Cancelled) return true; // D-04
            if (p.Status != TaskStatuses.Complete) return false;
            return e.LagDays <= 0 || p.CompletedOn is not { } c || After(c, e.LagDays) <= today; // lag (packet 021)
        }
        bool TaskOverdue(TaskSnap t) => active && t.DueDate is { } d && d < today && !TaskStatuses.IsTerminal(t.Status) && t.Status != TaskStatuses.OnHold; // G-01

        // ---------- Tasks ----------
        var taskResults = new Dictionary<Guid, TaskResult>();
        foreach (var t in i.Tasks)
        {
            var terminal = TaskStatuses.IsTerminal(t.Status);
            var held = t.Status == TaskStatuses.OnHold;
            var open = !terminal && !held;
            var overdue = TaskOverdue(t);
            var daysOverdue = overdue ? today.DayNumber - t.DueDate!.Value.DayNumber : 0;
            var dueSoon = active && open && t.DueDate is { } dd && dd >= today && Until(dd) <= s.TaskDueSoonDays; // G-02
            var unsatisfied = preds[t.Id].Where(e => !Satisfied(e)).ToList();
            var blockers = new List<Blocker>();
            var depBlocking = false;
            if (active && open && unsatisfied.Count > 0)
            {
                // D-06: waiting becomes blocked when the task should have started, is due soon, is being worked, or a predecessor is overdue.
                var cond = (t.StartDate is { } sd && sd <= today) || (t.DueDate is { } du && Until(du) <= s.TaskDueSoonDays)
                    || TaskStatuses.IsStarted(t.Status) || unsatisfied.Any(e => TaskOverdue(tasks[e.PredecessorId]));
                depBlocking = cond;
                foreach (var e in unsatisfied)
                {
                    var p = tasks[e.PredecessorId];
                    blockers.Add(new Blocker("task", p.Id, p.Key, p.Name, p.Status, p.DueDate, TaskOverdue(p), p.AssigneeId,
                        p.Status == TaskStatuses.OnHold ? "predecessor on hold" : e.LagDays > 0 && p.Status == TaskStatuses.Complete ? $"lag {e.LagDays} days" : null, cond));
                }
            }
            var decBlocking = false;
            var decWaiting = false;
            if (active && open)
                foreach (var dec in decisionLinks[t.Id])
                {
                    if (!DecisionStatus.IsOpen(dec.Status)) continue;
                    var od = dec.RequiredBy < today; // D-15
                    if (od) decBlocking = true; else decWaiting = true;
                    blockers.Add(new Blocker("decision", dec.Id, dec.Key, dec.Subject, dec.Status, dec.RequiredBy, od, dec.OwnerUserId, null, od));
                }
            var manual = active && open && t.ManualBlockType is not null; // D-14
            if (manual) blockers.Add(new Blocker("manual", null, null, t.ManualBlockType, null, null, false, null, t.ManualBlockReason, true));
            var blocked = manual || depBlocking || decBlocking;
            var waiting = !blocked && active && open && (unsatisfied.Count > 0 || decWaiting);
            DateOnly? since = null;
            if (blocked)
            {
                var prev = i.PreviousBlockedSince is not null && i.PreviousBlockedSince.TryGetValue(t.Id, out var pb) ? pb : today;
                since = manual && t.ManualBlockSince is { } ms && ms < prev ? ms : prev;
            }
            var reviewStalled = active && TaskStatuses.IsReview(t.Status) && t.ReviewRequestedOn is { } rr && Since(rr) > s.ReviewStaleDays; // R-05
            var staleDays = Since(t.LastActivity);
            var stale = active && (t.Status is TaskStatuses.InProgress || TaskStatuses.IsReview(t.Status)) && staleDays > s.TaskStaleDays;
            var parent = t.DeliverableId is { } pid && deliverables.TryGetValue(pid, out var pdel) ? pdel : null;
            var missingDue = active && !terminal && t.DueDate is null && (TaskStatuses.IsStarted(t.Status) || (t.Status == TaskStatuses.NotStarted && parent?.DueDate is not null));
            var inconsistencies = new List<Reason>();
            if (active && !terminal)
            {
                if (parent?.DueDate is { } pdd && t.DueDate is { } tdd && tdd > pdd) // T-04
                    inconsistencies.Add(new Reason($"Due {tdd:yyyy-MM-dd} is after deliverable {parent.Key} due {pdd:yyyy-MM-dd}", "T-04"));
                foreach (var e in preds[t.Id]) // D-12 (lag extends the predecessor's due date, packet 021)
                {
                    var p = tasks[e.PredecessorId];
                    if (p.DueDate is not { } pd) continue;
                    var pde = After(pd, e.LagDays);
                    if (t.StartDate is { } ts && ts < pde) inconsistencies.Add(new Reason($"Starts {ts:yyyy-MM-dd} before {p.Key} is due {pde:yyyy-MM-dd}", "D-12"));
                    else if (t.DueDate is { } td && td < pde) inconsistencies.Add(new Reason($"Due {td:yyyy-MM-dd} before {p.Key} is due {pde:yyyy-MM-dd}", "D-12"));
                }
            }
            var notes = new List<string>();
            if (preds[t.Id].Any(e => tasks[e.PredecessorId].Status == TaskStatuses.Cancelled)) notes.Add("predecessor_cancelled");
            if (decisionLinks[t.Id].Any(d => d.Status == DecisionStatus.Cancelled)) notes.Add("decision_cancelled");
            if (!terminal && parent is { Status: DeliverableStatus.Issued or DeliverableStatus.Accepted }) notes.Add("deliverable_issued");
            if (!terminal && t.AssigneeId is { } aid && !i.ActiveMemberIds.Contains(aid)) notes.Add("assignee_not_on_project");
            if (t.DueDateChangeCount >= 3 && t.Status != TaskStatuses.Complete) notes.Add("due_moved");
            taskResults[t.Id] = new TaskResult(t.Id, overdue, daysOverdue, dueSoon, waiting, blocked, since, since is { } sn ? Since(sn) : 0, // blocked days count working days when on (FR-005)
                blockers, false, [], stale, staleDays, !terminal && t.AssigneeId is null, missingDue, inconsistencies.Count > 0, inconsistencies,
                active && !terminal && (Inactive(t.AssigneeId) || Inactive(t.ReviewerId)), active && held && t.DueDate is { } hd && hd < today, reviewStalled, [], notes);
        }
        // D-11: a task is Blocking Others when an open successor waits on it; D-13: affected milestones through the chain.
        foreach (var t in i.Tasks)
        {
            var r = taskResults[t.Id];
            var blocking = active && !TaskStatuses.IsTerminal(t.Status)
                ? succs[t.Id].Where(e => !Satisfied(e)).Select(e => tasks[e.SuccessorId]).Where(x => !TaskStatuses.IsTerminal(x.Status) && x.Status != TaskStatuses.OnHold).Select(x => x.Id).ToList()
                : [];
            var affected = new List<Guid>();
            if (MilestoneOf(t) is { } own) affected.Add(own);
            var frontier = new List<Guid> { t.Id };
            var seen = new HashSet<Guid> { t.Id };
            for (var depth = 0; depth < s.ChainDepthLimit && frontier.Count > 0; depth++)
            {
                frontier = frontier.SelectMany(f => succs[f].Select(e => e.SuccessorId)).Where(seen.Add).ToList();
                foreach (var f in frontier) if (MilestoneOf(tasks[f]) is { } m && !affected.Contains(m)) affected.Add(m);
            }
            taskResults[t.Id] = r with { IsBlocking = blocking.Count > 0, BlockingTaskIds = blocking, AffectedMilestoneIds = affected.Where(m => milestones.ContainsKey(m) && !milestones[m].IsCancelled).ToList() };
        }

        // ---------- Decisions ----------
        var decisionResults = i.Decisions.Select(d =>
        {
            var open = DecisionStatus.IsOpen(d.Status);
            var od = active && open && d.RequiredBy < today; // G-01 for decisions
            var blockingIds = active && open ? d.BlockedTaskIds.Where(tasks.ContainsKey).Where(x => taskResults[x] is { } r && (r.IsBlocked || r.IsWaiting)).ToList() : [];
            return new DecisionResult(d.Id, od, od ? today.DayNumber - d.RequiredBy.DayNumber : 0,
                active && open && d.RequiredBy >= today && Until(d.RequiredBy) <= s.DecisionDueSoonDays, blockingIds, active && open && Inactive(d.OwnerUserId));
        }).ToList();
        var decisionById = decisionResults.ToDictionary(d => d.DecisionId);

        // ---------- Deliverables (DL-07, DL-08, DL-11, DL-12, D-18) ----------
        var tasksByDeliverable = i.Tasks.Where(t => t.DeliverableId is not null).ToLookup(t => t.DeliverableId!.Value);
        var derivedPred = new Dictionary<Guid, HashSet<Guid>>();
        var derivedSucc = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var e in i.Dependencies.Where(e => tasks.ContainsKey(e.PredecessorId) && tasks.ContainsKey(e.SuccessorId)))
            if (tasks[e.PredecessorId].DeliverableId is { } a && tasks[e.SuccessorId].DeliverableId is { } b && a != b)
            {
                (derivedPred.TryGetValue(b, out var x) ? x : derivedPred[b] = []).Add(a);
                (derivedSucc.TryGetValue(a, out var y) ? y : derivedSucc[a] = []).Add(b);
            }
        var dlPreds = (i.DeliverableDependencies ?? []).Where(e => deliverables.ContainsKey(e.PredecessorId) && deliverables.ContainsKey(e.SuccessorId)).ToLookup(e => e.SuccessorId);
        var deliverableResults = new Dictionary<Guid, DeliverableResult>();
        foreach (var d in i.Deliverables)
        {
            var ts = tasksByDeliverable[d.Id].ToList();
            var cancelled = ts.Count(t => t.Status == TaskStatuses.Cancelled);
            var complete = ts.Count(t => t.Status == TaskStatuses.Complete);
            var live = ts.Where(t => t.Status != TaskStatuses.Cancelled).ToList();
            int? progress = null;
            if (live.Count > 0)
            {
                // DL-07: by count, or by estimated hours when every non-cancelled task has an estimate; rounded down to 5.
                var byHours = live.All(t => t.EstimatedHours is > 0);
                var ratio = byHours ? (double)(live.Where(t => t.Status == TaskStatuses.Complete).Sum(t => t.EstimatedHours!.Value) / live.Sum(t => t.EstimatedHours!.Value))
                    : (double)complete / live.Count;
                progress = (int)Math.Floor(ratio * 100 / 5) * 5;
            }
            var openTasks = ts.Where(t => !TaskStatuses.IsTerminal(t.Status)).ToList();
            var tOverdue = openTasks.Count(t => taskResults[t.Id].IsOverdue);
            var tBlocked = openTasks.Count(t => taskResults[t.Id].IsBlocked);
            var terminal = DeliverableStatus.IsTerminal(d.Status);
            var dHeld = d.Status == DeliverableStatus.OnHold;
            var overdue = active && d.DueDate is { } due && due < today && !terminal && !dHeld;
            var dueSoon = active && !terminal && !dHeld && d.DueDate is { } dd && dd >= today && Until(dd) <= s.DeliverableDueSoonDays;
            var ms = d.MilestoneId is { } mid && milestones.TryGetValue(mid, out var m) ? m : null;
            var msOverdue = active && ms is { IsComplete: false, IsCancelled: false, Date: { } md } && md < today;
            var atRisk = new List<Reason>();
            if (active && !terminal && !dHeld)
            {
                if (overdue) atRisk.Add(new Reason($"Overdue by {today.DayNumber - d.DueDate!.Value.DayNumber} days", "DL-08"));
                if (dueSoon && (tOverdue > 0 || tBlocked > 0)) atRisk.Add(new Reason($"Due within {s.DeliverableDueSoonDays} days with {tOverdue} overdue and {tBlocked} blocked tasks", "DL-08", Threshold: s.DeliverableDueSoonDays));
                if (dueSoon && (progress ?? 0) < 50) atRisk.Add(new Reason($"Due within {s.DeliverableDueSoonDays} days at {(progress is null ? "no" : progress + " %")} progress (below 50 %)", "DL-08", Threshold: 50));
                if (msOverdue) atRisk.Add(new Reason($"Milestone {ms!.Key} is overdue", "DL-08"));
            }
            var inconsistencies = new List<Reason>();
            if (active && !terminal && ms is { IsCancelled: false, Date: { } mdate } && d.DueDate is { } ddue && ddue > mdate) // DL-03
                inconsistencies.Add(new Reason($"Due {ddue:yyyy-MM-dd} is after milestone {ms.Key} on {mdate:yyyy-MM-dd}", "DL-03"));
            var remaining = openTasks.Where(t => t.EstimatedHours is not null && t.Status != TaskStatuses.OnHold).Sum(t => t.EstimatedHours!.Value * (1 - t.ProgressPct / 100m));
            // Explicit deliverable dependencies (packet 021) produce the same waiting/blocked states; satisfied means Issued, Accepted or Cancelled.
            var dBlockers = new List<Blocker>();
            var dBlocked = false;
            if (active && !terminal && !dHeld)
                foreach (var e in dlPreds[d.Id])
                {
                    var p = deliverables[e.PredecessorId];
                    // Satisfied: Cancelled, or Issued/Accepted once the lag after the issue date has passed (FR-002, FR-003).
                    var inLag = p.Status is DeliverableStatus.Issued or DeliverableStatus.Accepted && e.LagDays > 0 && p.IssuedDate is { } iss && After(iss, e.LagDays) > today;
                    if (DeliverableStatus.IsTerminal(p.Status) && !inLag) continue;
                    var pOverdue = !inLag && p.DueDate is { } pd && pd < today;
                    var cond = (d.StartDate is { } sd && sd <= today) || (d.DueDate is { } du && Until(du) <= s.DeliverableDueSoonDays) || d.Status != DeliverableStatus.NotStarted || pOverdue;
                    dBlocked |= cond;
                    dBlockers.Add(new Blocker("deliverable", p.Id, p.Key, p.Name, p.Status, p.DueDate, pOverdue, p.OwnerId, inLag ? $"lag {e.LagDays} days" : null, cond));
                }
            if (active && !terminal)
                foreach (var e in dlPreds[d.Id]) // D-12 for deliverables: the successor starts after the predecessor's due date plus the lag
                    if (deliverables[e.PredecessorId] is { DueDate: { } pdue } pr && d.StartDate is { } dsd && dsd < After(pdue, e.LagDays))
                        inconsistencies.Add(new Reason($"Starts {dsd:yyyy-MM-dd} before {pr.Key} is due {After(pdue, e.LagDays):yyyy-MM-dd}", "D-12"));
            deliverableResults[d.Id] = new DeliverableResult(d.Id, progress, ts.Count, complete, cancelled, openTasks.Count, tOverdue, tBlocked,
                live.Sum(t => t.EstimatedHours ?? 0), remaining, overdue, overdue ? today.DayNumber - d.DueDate!.Value.DayNumber : 0, dueSoon, atRisk.Count > 0, atRisk,
                !terminal && d.OwnerId is null, active && d.Status is DeliverableStatus.InProgress or DeliverableStatus.InReview && Since(d.LastActivity) > s.TaskStaleDays,
                inconsistencies.Count > 0, inconsistencies, d.DueDate is { } a1 && d.OriginalDueDate is { } o1 ? Math.Max(0, a1.DayNumber - o1.DayNumber) : 0,
                active && !terminal && (Inactive(d.OwnerId) || Inactive(d.ReviewerId)), d.Status is DeliverableStatus.Issued or DeliverableStatus.Accepted && openTasks.Count > 0,
                !terminal && ms is { IsCancelled: true }, active && !dBlocked && dBlockers.Count > 0, dBlocked, dBlockers,
                derivedPred.TryGetValue(d.Id, out var dp) ? [.. dp] : [], derivedSucc.TryGetValue(d.Id, out var dsu) ? [.. dsu] : []);
        }

        // Blocking Others for deliverables: explicit successors this one currently blocks (FR-002).
        foreach (var (pid, n) in deliverableResults.Values.SelectMany(r => r.Blockers.Where(b => b.Blocking && b.Type == "deliverable" && b.Id is not null)).GroupBy(b => b.Id!.Value)
            .Select(g => (g.Key, g.Count())).ToList())
            deliverableResults[pid] = deliverableResults[pid] with { BlockingCount = n };

        // ---------- Milestones (§16.2) ----------
        var milestoneResults = new List<MilestoneResult>();
        foreach (var m in i.Milestones)
        {
            var targeted = i.Deliverables.Where(d => d.MilestoneId == m.Id && d.Status != DeliverableStatus.Cancelled).ToList();
            var mts = i.Tasks.Where(t => MilestoneOf(t) == m.Id && t.Status != TaskStatuses.Cancelled).ToList();
            var issued = targeted.Count(d => d.Status is DeliverableStatus.Issued or DeliverableStatus.Accepted);
            var tOverdue = mts.Count(t => taskResults[t.Id].IsOverdue);
            var tBlocked = mts.Count(t => taskResults[t.Id].IsBlocked);
            var reasons = new List<Reason>();
            string? status;
            int? daysRemaining = m.Date is { } dt ? dt.DayNumber - today.DayNumber : null;
            if (m.IsComplete) status = MilestoneStatus.Complete;
            else if (m.IsCancelled) status = MilestoneStatus.Cancelled;
            else if (!active || m.Date is null) status = null;
            else if (m.Date < today) { status = MilestoneStatus.Overdue; reasons.Add(new Reason($"Date {m.Date:yyyy-MM-dd} has passed ({today.DayNumber - m.Date.Value.DayNumber} days)", "§16.2", "Red")); }
            else
            {
                var within = Until(m.Date.Value) <= s.MilestoneApproachingDays;
                var odDel = targeted.Where(d => deliverableResults[d.Id].IsOverdue).ToList();
                if (odDel.Count > 0) reasons.Add(new Reason($"{odDel.Count} targeted deliverables overdue: {string.Join(", ", odDel.Select(d => d.Key))}", "§16.2 a"));
                var badTasks = mts.Where(t => taskResults[t.Id].IsOverdue || taskResults[t.Id].IsBlocked).ToList();
                if (badTasks.Count > 0) reasons.Add(new Reason($"{badTasks.Count(t => taskResults[t.Id].IsOverdue)} tasks overdue and {badTasks.Count(t => taskResults[t.Id].IsBlocked)} blocked: {string.Join(", ", badTasks.Take(5).Select(t => t.Key))}", "§16.2 b"));
                var notIssued = targeted.Where(d => d.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted) && (deliverableResults[d.Id].ProgressPct ?? 0) < 100).ToList();
                if (within && notIssued.Count > 0) reasons.Add(new Reason($"{targeted.Count - issued} of {targeted.Count} deliverables not issued with {daysRemaining} days remaining", "§16.2 c", Threshold: s.MilestoneApproachingDays));
                if (within && MilestoneType.IsSubmission(m.Type) && targeted.Count == 0) reasons.Add(new Reason($"Submission in {daysRemaining} days with no deliverables planned", "§16.2 d", Threshold: s.MilestoneApproachingDays));
                var late = targeted.Where(d => d.DueDate > m.Date).ToList();
                if (late.Count > 0) reasons.Add(new Reason($"{late.Count} deliverables due after the milestone: {string.Join(", ", late.Select(d => d.Key))}", "§16.2 e"));
                status = reasons.Count > 0 ? MilestoneStatus.AtRisk : MilestoneStatus.OnTrack;
                reasons = [.. reasons.Select(r => r with { Colour = "Yellow" })];
            }
            milestoneResults.Add(new MilestoneResult(m.Id, status, reasons, daysRemaining, m.Date is { } a && m.OriginalDate is { } o ? a.DayNumber - o.DayNumber : 0,
                targeted.Count, issued, mts.Count, mts.Count(t => t.Status == TaskStatuses.Complete), mts.Count(t => !TaskStatuses.IsTerminal(t.Status)), tOverdue, tBlocked));
        }
        var msResultById = milestoneResults.ToDictionary(m => m.MilestoneId);

        // ---------- Health (§16.3) and discipline status (§16.6) ----------
        HealthResult Health(IEnumerable<TaskSnap> ts, IEnumerable<DeliverableSnap> ds, IEnumerable<MilestoneSnap> ms, IEnumerable<DecisionSnap> decs, int highIssues, int inactiveExtra)
        {
            var reasons = new List<Reason>();
            var inputs = new Dictionary<string, int>();
            if (!active)
            {
                reasons.Add(new Reason(i.Project.Status switch { ProjectStatus.OnHold => "Project on hold", ProjectStatus.Setup => "Project in setup", _ => $"Project {i.Project.Status.ToLowerInvariant()}" }, "G-05"));
                return new HealthResult(Domain.Health.Grey, reasons, inputs);
            }
            var tl = ts.ToList();
            var openTasks = tl.Where(t => !TaskStatuses.IsTerminal(t.Status) && t.Status != TaskStatuses.OnHold).ToList();
            var liveMs = ms.Where(m => !m.IsComplete && !m.IsCancelled).ToList();
            if (openTasks.Count == 0 && liveMs.Count == 0)
            {
                reasons.Add(new Reason("Nothing to evaluate", "§16.3"));
                return new HealthResult(Domain.Health.Grey, reasons, inputs);
            }
            int msOverdue = liveMs.Count(m => msResultById[m.Id].Status == MilestoneStatus.Overdue), msAtRisk = liveMs.Count(m => msResultById[m.Id].Status == MilestoneStatus.AtRisk);
            var dl = ds.Where(d => deliverableResults[d.Id].IsOverdue).ToList();
            int delOverdue = dl.Count, delOverdue5 = dl.Count(d => deliverableResults[d.Id].DaysOverdue > 5);
            var taskOverdue = openTasks.Count(t => taskResults[t.Id].IsOverdue);
            var pct = openTasks.Count == 0 ? 0 : taskOverdue * 100.0 / openTasks.Count;
            var blockedLong = openTasks.Count(t => taskResults[t.Id].IsBlocked && taskResults[t.Id].DaysBlocked > s.HealthBlockedDaysYellow);
            var dl2 = decs.ToList();
            var decOverdue = dl2.Count(d => decisionById[d.Id].IsOverdue);
            var decOverdueBlocking = dl2.Count(d => decisionById[d.Id].IsOverdue && d.BlockedTaskIds.Any(x => tasks.TryGetValue(x, out var tk) && !TaskStatuses.IsTerminal(tk.Status)));
            var inactive = openTasks.Count(t => taskResults[t.Id].IsInactiveOwner) + ds.Count(d => deliverableResults[d.Id].IsInactiveOwner) + dl2.Count(d => decisionById[d.Id].IsInactiveOwner) + inactiveExtra;
            inputs["ms_overdue"] = msOverdue; inputs["ms_at_risk"] = msAtRisk; inputs["del_overdue"] = delOverdue; inputs["del_overdue_5"] = delOverdue5;
            inputs["open_tasks"] = openTasks.Count; inputs["task_overdue"] = taskOverdue; inputs["task_overdue_pct"] = (int)Math.Round(pct);
            inputs["task_blocked_long"] = blockedLong; inputs["dec_overdue"] = decOverdue; inputs["dec_overdue_blocking"] = decOverdueBlocking;
            inputs["issue_high"] = highIssues; inputs["inactive_owner"] = inactive;
            var enoughOverdue = taskOverdue >= s.HealthOverdueTaskMinYellow;
            var red = false; var yellow = false;
            void R(bool cond, string text, bool isRed, object? threshold = null) { if (!cond) return; reasons.Add(new Reason(text, "§16.3", isRed ? "Red" : "Yellow", threshold)); if (isRed) red = true; else yellow = true; }
            R(msOverdue >= 1, $"Milestones overdue: {msOverdue}", true);
            R(delOverdue5 >= 1, $"Deliverables overdue by more than 5 days: {delOverdue5}", true, 5);
            R(pct >= s.HealthOverdueTaskPctRed && enoughOverdue, $"Overdue tasks {taskOverdue} of {openTasks.Count} ({Math.Round(pct)}%) ≥ {s.HealthOverdueTaskPctRed}%", true, s.HealthOverdueTaskPctRed);
            R(decOverdueBlocking >= 1, $"Overdue decisions blocking work: {decOverdueBlocking}", true);
            R(highIssues >= 1, $"High-severity open issues: {highIssues}", true);
            R(msAtRisk >= 1, $"Milestones at risk: {msAtRisk}", false);
            R(delOverdue >= 1 && delOverdue5 == 0, $"Deliverables overdue: {delOverdue}", false);
            R(pct >= s.HealthOverdueTaskPctYellow && pct < s.HealthOverdueTaskPctRed && enoughOverdue, $"Overdue tasks {taskOverdue} of {openTasks.Count} ({Math.Round(pct)}%) ≥ {s.HealthOverdueTaskPctYellow}%", false, s.HealthOverdueTaskPctYellow);
            R(blockedLong >= 1, $"Tasks blocked longer than {s.HealthBlockedDaysYellow} days: {blockedLong}", false, s.HealthBlockedDaysYellow);
            R(decOverdue >= 1 && decOverdueBlocking == 0, $"Decisions overdue: {decOverdue}", false);
            R(inactive >= 1, $"Items owned by inactive people: {inactive}", false);
            // Contributors below the colour that won are still listed so "Why?" shows every non-zero input.
            if (taskOverdue > 0 && !(pct >= s.HealthOverdueTaskPctYellow && enoughOverdue))
                reasons.Add(enoughOverdue
                    ? new Reason($"Overdue tasks {taskOverdue} of {openTasks.Count} ({Math.Round(pct)}%) below {s.HealthOverdueTaskPctYellow}%", "§16.3", null, s.HealthOverdueTaskPctYellow)
                    : new Reason($"Overdue tasks {taskOverdue} of {openTasks.Count} ({Math.Round(pct)}%): fewer than {s.HealthOverdueTaskMinYellow}, so not counted", "§16.3", null, s.HealthOverdueTaskMinYellow));
            var colour = red ? Domain.Health.Red : yellow ? Domain.Health.Yellow : Domain.Health.Green;
            if (colour == Domain.Health.Green) reasons.Add(new Reason("No health rule fired", "§16.3", "Green"));
            return new HealthResult(colour, reasons, inputs);
        }

        var highIssues = (i.Issues ?? []).Where(x => x.Severity == Impact.High && IssueStatus.IsOpen(x.Status)).ToList();
        var inactiveLeads = i.Disciplines.Count(d => d.IsActive && Inactive(d.LeadId));
        var health = Health(i.Tasks, i.Deliverables, i.Milestones, i.Decisions, highIssues.Count, active ? inactiveLeads : 0);
        var disciplineResults = new List<DisciplineResult>();
        foreach (var disc in i.Disciplines.Where(d => d.IsActive).OrderBy(d => d.SortOrder))
        {
            var dts = i.Tasks.Where(t => t.DisciplineId == disc.Id).ToList();
            var dds = i.Deliverables.Where(d => d.DisciplineId == disc.Id).ToList();
            var msIds = dds.Where(d => d.MilestoneId is not null).Select(d => d.MilestoneId!.Value).ToHashSet();
            var dms = i.Milestones.Where(m => msIds.Contains(m.Id)).ToList();
            var ddecs = i.Decisions.Where(d => d.BlockedTaskIds.Any(x => tasks.TryGetValue(x, out var tk) && tk.DisciplineId == disc.Id)).ToList();
            var dh = Health(dts, dds, dms, ddecs, highIssues.Count(x => x.DisciplineId == disc.Id), active && Inactive(disc.LeadId) ? 1 : 0);
            var openT = dts.Where(t => !TaskStatuses.IsTerminal(t.Status) && t.Status != TaskStatuses.OnHold).ToList();
            var next = openT.Where(t => t.DueDate >= today).Select(t => (Type: ItemType.Task, t.Id, t.Key, t.Name, Due: t.DueDate!.Value))
                .Concat(dds.Where(d => !DeliverableStatus.IsTerminal(d.Status) && d.Status != DeliverableStatus.OnHold && d.DueDate >= today).Select(d => (Type: ItemType.Deliverable, d.Id, d.Key, d.Name, Due: d.DueDate!.Value)))
                .OrderBy(x => x.Due).FirstOrDefault();
            disciplineResults.Add(new DisciplineResult(disc.Id, disc.Name, disc.LeadId, disc.SortOrder, dh.Health, dh.Reasons, openT.Count,
                openT.Count(t => taskResults[t.Id].IsOverdue), openT.Count(t => taskResults[t.Id].IsBlocked), openT.Count(t => taskResults[t.Id].IsWaiting),
                dds.Count(d => !DeliverableStatus.IsTerminal(d.Status) && d.DueDate is { } due && due >= today && due.DayNumber - today.DayNumber <= 14),
                next.Key, next.Name, next.Key is null ? null : next.Due, next.Key is null ? null : next.Type, next.Key is null ? null : next.Id));
        }

        // ---------- Counts, next milestone (M-08), progress (§36.2) ----------
        var nonCancelled = i.Tasks.Where(t => t.Status != TaskStatuses.Cancelled).ToList();
        var counts = new Dictionary<string, int>
        {
            ["tasksTotal"] = nonCancelled.Count,
            ["tasksComplete"] = nonCancelled.Count(t => t.Status == TaskStatuses.Complete),
            ["tasksInProgress"] = nonCancelled.Count(t => t.Status == TaskStatuses.InProgress),
            ["tasksInReview"] = nonCancelled.Count(t => TaskStatuses.IsReview(t.Status)),
            ["tasksOverdue"] = taskResults.Values.Count(r => r.IsOverdue),
            ["tasksBlocked"] = taskResults.Values.Count(r => r.IsBlocked),
            ["tasksWaiting"] = taskResults.Values.Count(r => r.IsWaiting),
            ["tasksUnassigned"] = i.Tasks.Count(t => !TaskStatuses.IsTerminal(t.Status) && t.AssigneeId is null),
            ["tasksNoDueDate"] = i.Tasks.Count(t => !TaskStatuses.IsTerminal(t.Status) && t.DueDate is null), // G2 pilot measure
            ["tasksOnHold"] = i.Tasks.Count(t => t.Status == TaskStatuses.OnHold),
            ["deliverablesTotal"] = i.Deliverables.Count(d => d.Status != DeliverableStatus.Cancelled),
            ["deliverablesUpcoming"] = i.Deliverables.Count(d => !DeliverableStatus.IsTerminal(d.Status) && d.DueDate is { } due && due >= today && due.DayNumber - today.DayNumber <= 14),
            ["deliverablesAtRisk"] = deliverableResults.Values.Count(r => r.IsAtRisk),
            ["deliverablesIssued"] = i.Deliverables.Count(d => d.Status is DeliverableStatus.Issued or DeliverableStatus.Accepted),
            ["decisionsPending"] = i.Decisions.Count(d => DecisionStatus.IsOpen(d.Status)),
            ["decisionsOverdue"] = decisionResults.Count(r => r.IsOverdue),
            ["issuesOpen"] = (i.Issues ?? []).Count(x => IssueStatus.IsOpen(x.Status)),
            ["issuesHigh"] = highIssues.Count,
            ["risksHigh"] = (i.Risks ?? []).Count(r => RiskStatus.IsOpen(r.Status) && Registers.Band(Registers.Score(r.Probability, r.Impact)) == Impact.High), // RSK-01
        };
        var liveMilestones = i.Milestones.Where(m => !m.IsComplete && !m.IsCancelled && m.Date is not null).OrderBy(m => m.Date).ToList();
        int? progressPct = nonCancelled.Count == 0 ? null : (int)Math.Floor(nonCancelled.Count(t => t.Status == TaskStatuses.Complete) * 100.0 / nonCancelled.Count);

        var attention = active ? Attention(i, taskResults, deliverableResults, msResultById, decisionById, highIssues, tasks, deliverables, milestones, disciplines, users, MilestoneOf, Until) : [];
        var project = new ProjectResult(health, disciplineResults, counts, liveMilestones.FirstOrDefault()?.Id,
            liveMilestones.FirstOrDefault(m => MilestoneType.IsSubmission(m.Type))?.Id, progressPct);
        return new EvalResult([.. taskResults.Values], [.. deliverableResults.Values], milestoneResults, decisionResults, project, attention);
    }

    // ---------- Attention rules A-01..A-20 (§12.12, ATT-01..ATT-05) ----------

    static List<AttentionResult> Attention(EvalInput i, Dictionary<Guid, TaskResult> tr, Dictionary<Guid, DeliverableResult> dr, Dictionary<Guid, MilestoneResult> mr,
        Dictionary<Guid, DecisionResult> decr, List<IssueSnap> highIssues, Dictionary<Guid, TaskSnap> tasks, Dictionary<Guid, DeliverableSnap> deliverables,
        Dictionary<Guid, MilestoneSnap> milestones, Dictionary<Guid, DisciplineSnap> disciplines, Dictionary<Guid, UserSnap> users, Func<TaskSnap, Guid?> milestoneOf, Func<DateOnly, int> until)
    {
        var s = i.Settings;
        var today = i.Today;
        var list = new List<AttentionResult>();
        var pms = i.Project.PmUserIds.Append(i.Project.PrimaryPmId).Distinct().ToList();
        Guid? Lead(Guid disciplineId) => disciplines.TryGetValue(disciplineId, out var d) ? d.LeadId : null;
        var allLeads = i.Disciplines.Where(d => d.IsActive && d.LeadId is not null).Select(d => d.LeadId!.Value).ToList();
        string Name(Guid? id) => id is { } x && users.TryGetValue(x, out var u) ? u.Name : "—";
        void Add(string rule, string severity, string itemType, Guid id, string? key, string? name, string message, IEnumerable<Guid?> route, Guid? owner, Guid? discipline,
            int days, string? priority, DateOnly? due, object? threshold, Dictionary<string, object?> values)
        {
            if (!s.IsRuleEnabled(rule)) return; // AC-ATT-07
            var why = new Reason($"{rule} {RuleNames[rule]}: {message}", rule, severity, threshold, values);
            list.Add(new AttentionResult(rule, severity, itemType, id, key, name, message, why, route.OfType<Guid>().Distinct().ToList(), owner, discipline, days, priority, due, false));
        }

        foreach (var t in i.Tasks)
        {
            if (TaskStatuses.IsTerminal(t.Status)) continue;
            var r = tr[t.Id];
            var dl = Lead(t.DisciplineId);
            var due = t.DueDate?.ToString("yyyy-MM-dd");
            var affected = string.Join(", ", r.AffectedMilestoneIds.Where(milestones.ContainsKey).Select(m => milestones[m].Name));
            if (r.IsOverdue) // A-01
                Add("A-01", r.DaysOverdue > 5 || t.Priority == Priority.Critical ? Severity.Critical : Severity.Warning, ItemType.Task, t.Id, t.Key, t.Name,
                    $"Due {due} — {r.DaysOverdue} days overdue" + (affected.Length > 0 ? $"; affects {affected}" : ""),
                    [t.AssigneeId, dl, .. pms.Cast<Guid?>()], t.AssigneeId, t.DisciplineId, r.DaysOverdue, t.Priority, t.DueDate, 5,
                    new() { ["dueDate"] = due, ["daysOverdue"] = r.DaysOverdue, ["priority"] = t.Priority });
            if (r.IsBlocked && r.DaysBlocked >= s.BlockedAttentionDays) // A-02
                Add("A-02", r.IsDueSoon || r.IsOverdue ? Severity.Critical : Severity.Warning, ItemType.Task, t.Id, t.Key, t.Name,
                    $"Blocked {r.DaysBlocked} days by {string.Join(", ", r.Blockers.Where(b => b.Blocking).Select(b => b.Key ?? $"{b.Name}: {b.Reason}"))}" + (affected.Length > 0 ? $"; affects {affected}" : ""),
                    [t.AssigneeId, dl, .. pms.Cast<Guid?>()], t.AssigneeId, t.DisciplineId, r.DaysBlocked, t.Priority, t.DueDate, s.BlockedAttentionDays,
                    new() { ["daysBlocked"] = r.DaysBlocked, ["blockers"] = r.Blockers.Select(b => b.Key ?? b.Name).ToList() });
            if (r.IsOverdue && r.IsBlocking) // A-03
            {
                var succ = r.BlockingTaskIds.Select(x => tasks[x]).ToList();
                Add("A-03", Severity.Critical, ItemType.Task, t.Id, t.Key, t.Name,
                    $"Due {due} — {r.DaysOverdue} days overdue; blocking {string.Join(" and ", succ.Select(x => x.Key))}" + (affected.Length > 0 ? $"; affects {affected}" : ""),
                    [t.AssigneeId, dl, .. succ.Select(x => Lead(x.DisciplineId)), .. pms.Cast<Guid?>()], t.AssigneeId, t.DisciplineId, r.DaysOverdue, t.Priority, t.DueDate, null,
                    new() { ["daysOverdue"] = r.DaysOverdue, ["blocking"] = succ.Select(x => x.Key).ToList() });
            }
            if (t.AssigneeId is null && (TaskStatuses.IsStarted(t.Status) || t.StartDate is { } sd && sd <= today || r.IsDueSoon)) // A-08
                Add("A-08", Severity.Warning, ItemType.Task, t.Id, t.Key, t.Name,
                    TaskStatuses.IsStarted(t.Status) ? $"{t.Status} with no assignee" : t.StartDate <= today ? $"Starts {t.StartDate:yyyy-MM-dd} with no assignee" : $"Due {due} with no assignee",
                    [dl, .. pms.Cast<Guid?>()], null, t.DisciplineId, 0, t.Priority, t.DueDate, s.TaskDueSoonDays, new() { ["status"] = t.Status, ["startDate"] = t.StartDate, ["dueDate"] = due });
            if (r.IsMissingDueDate) // A-09
                Add("A-09", Severity.Info, ItemType.Task, t.Id, t.Key, t.Name, TaskStatuses.IsStarted(t.Status) ? $"{t.Status} with no due date" : "No due date while its deliverable has one",
                    [t.AssigneeId, dl], t.AssigneeId, t.DisciplineId, 0, t.Priority, null, null, new() { ["status"] = t.Status });
            if (t.Status == TaskStatuses.InProgress && r.StaleDays > s.TaskStaleDays) // A-10
                Add("A-10", r.IsDueSoon ? Severity.Warning : Severity.Info, ItemType.Task, t.Id, t.Key, t.Name, $"No update for {r.StaleDays} days",
                    [t.AssigneeId, dl], t.AssigneeId, t.DisciplineId, r.StaleDays, t.Priority, t.DueDate, s.TaskStaleDays, new() { ["daysSinceActivity"] = r.StaleDays });
            if (r.IsReviewStalled) // A-11
            {
                var waitDays = today.DayNumber - t.ReviewRequestedOn!.Value.DayNumber;
                Add("A-11", Severity.Warning, ItemType.Task, t.Id, t.Key, t.Name, $"{t.Status} for {waitDays} days; reviewer {Name(t.ReviewerId)}",
                    [t.ReviewerId, dl, .. pms.Cast<Guid?>()], t.ReviewerId, t.DisciplineId, waitDays, t.Priority, t.DueDate, s.ReviewStaleDays, new() { ["daysWaiting"] = waitDays });
            }
            if (t.DeliverableId is { } did && deliverables.TryGetValue(did, out var pdel) && pdel.DueDate is { } pdd && t.DueDate is { } tdd && tdd > pdd) // A-16
                Add("A-16", Severity.Info, ItemType.Task, t.Id, t.Key, t.Name, $"Due {tdd:yyyy-MM-dd} after deliverable {pdel.Key} due {pdd:yyyy-MM-dd}",
                    [t.AssigneeId, dl], t.AssigneeId, t.DisciplineId, 0, t.Priority, t.DueDate, null, new() { ["taskDue"] = tdd, ["deliverableDue"] = pdd });
            if (r.IsInactiveOwner) // A-18
            {
                var who = users.TryGetValue(t.AssigneeId ?? Guid.Empty, out var ua) && !ua.IsActive ? ua : users.TryGetValue(t.ReviewerId ?? Guid.Empty, out var ur) ? ur : null;
                Add("A-18", Severity.Critical, ItemType.Task, t.Id, t.Key, t.Name, $"{(who?.Id == t.AssigneeId ? "Assignee" : "Reviewer")} {who?.Name} is inactive",
                    [dl, .. pms.Cast<Guid?>(), who?.SupervisorId], t.AssigneeId, t.DisciplineId, 0, t.Priority, t.DueDate, null, new() { ["person"] = who?.Name });
            }
            if (t.Status == TaskStatuses.OnHold && t.DueDate is { } hd && today.DayNumber - hd.DayNumber > 10) // A-19
                Add("A-19", Severity.Info, ItemType.Task, t.Id, t.Key, t.Name, $"On hold {today.DayNumber - hd.DayNumber} days past its due date {hd:yyyy-MM-dd}",
                    [dl, .. pms.Cast<Guid?>()], t.AssigneeId, t.DisciplineId, today.DayNumber - hd.DayNumber, t.Priority, t.DueDate, 10, new() { ["daysPastDue"] = today.DayNumber - hd.DayNumber });
            if (t.DueDateChangeCount >= 3 && t.Status != TaskStatuses.Complete) // A-20
                Add("A-20", Severity.Info, ItemType.Task, t.Id, t.Key, t.Name, $"Due date moved {t.DueDateChangeCount} times",
                    [dl, .. pms.Cast<Guid?>()], t.AssigneeId, t.DisciplineId, 0, t.Priority, t.DueDate, 3, new() { ["changes"] = t.DueDateChangeCount });
        }

        foreach (var d in i.Decisions.Where(d => DecisionStatus.IsOpen(d.Status)))
        {
            var r = decr[d.Id];
            if (r.IsOverdue) // A-04
                Add("A-04", Severity.Critical, ItemType.Decision, d.Id, d.Key, d.Subject, $"Required by {d.RequiredBy:yyyy-MM-dd} — {r.DaysOverdue} days overdue; blocking {d.BlockedTaskIds.Count(x => tasks.ContainsKey(x) && !TaskStatuses.IsTerminal(tasks[x].Status))} tasks",
                    [d.RequestedById, d.OwnerUserId, .. pms.Cast<Guid?>()], d.OwnerUserId, null, r.DaysOverdue, d.Impact, d.RequiredBy, null, new() { ["requiredBy"] = d.RequiredBy, ["daysOverdue"] = r.DaysOverdue });
            else if (r.IsDueSoon && d.Impact == Impact.High)
                Add("A-04", Severity.Warning, ItemType.Decision, d.Id, d.Key, d.Subject, $"High impact, required by {d.RequiredBy:yyyy-MM-dd} (in {until(d.RequiredBy)} days)",
                    [d.RequestedById, d.OwnerUserId, .. pms.Cast<Guid?>()], d.OwnerUserId, null, 0, d.Impact, d.RequiredBy, s.DecisionDueSoonDays, new() { ["requiredBy"] = d.RequiredBy, ["impact"] = d.Impact });
            if (r.IsInactiveOwner) // A-18
                Add("A-18", Severity.Critical, ItemType.Decision, d.Id, d.Key, d.Subject, $"Owner {Name(d.OwnerUserId)} is inactive",
                    [.. pms.Cast<Guid?>(), d.OwnerUserId is { } ou && users.TryGetValue(ou, out var owner) ? owner.SupervisorId : null], d.OwnerUserId, null, 0, d.Impact, d.RequiredBy, null, new());
        }

        foreach (var m in i.Milestones.Where(m => !m.IsComplete && !m.IsCancelled && m.Date is not null))
        {
            var r = mr[m.Id];
            var targeted = i.Deliverables.Where(d => d.MilestoneId == m.Id && d.Status != DeliverableStatus.Cancelled).ToList();
            var leadsOfAffected = targeted.Select(d => Lead(d.DisciplineId)).Distinct().ToList();
            var days = until(m.Date!.Value);
            if (r.Status == MilestoneStatus.Overdue) // A-14
                Add("A-14", Severity.Critical, ItemType.Milestone, m.Id, m.Key, m.Name, $"Date {m.Date:yyyy-MM-dd} passed {today.DayNumber - m.Date.Value.DayNumber} days ago",
                    [.. pms.Cast<Guid?>(), .. allLeads.Cast<Guid?>()], null, m.DisciplineId, today.DayNumber - m.Date.Value.DayNumber, null, m.Date, null, new() { ["date"] = m.Date });
            else if (m.Date >= today && days <= s.MilestoneApproachingDays) // A-05
            {
                var incomplete = targeted.Where(d => d.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted) && (dr[d.Id].ProgressPct ?? 0) < 100).ToList();
                var badTasks = i.Tasks.Where(t => milestoneOf(t) == m.Id && (tr[t.Id].IsOverdue || tr[t.Id].IsBlocked)).ToList();
                if (incomplete.Count > 0 || badTasks.Count > 0)
                    Add("A-05", days <= 5 ? Severity.Critical : Severity.Warning, ItemType.Milestone, m.Id, m.Key, m.Name,
                        $"In {days} days; {targeted.Count - r.DeliverableIssued} of {targeted.Count} deliverables not issued" + (badTasks.Count > 0 ? $"; {badTasks.Count} tasks overdue or blocked" : ""),
                        [.. pms.Cast<Guid?>(), .. leadsOfAffected], null, m.DisciplineId, 0, null, m.Date, s.MilestoneApproachingDays,
                        new() { ["daysRemaining"] = days, ["notIssued"] = targeted.Count - r.DeliverableIssued, ["targeted"] = targeted.Count });
            }
            if (MilestoneType.IsSubmission(m.Type) && targeted.Count == 0) // A-12
                Add("A-12", Severity.Info, ItemType.Milestone, m.Id, m.Key, m.Name, $"{m.Type} with no deliverables planned", [.. pms.Cast<Guid?>()], null, m.DisciplineId, 0, null, m.Date, null, new() { ["type"] = m.Type });
        }

        foreach (var d in i.Deliverables.Where(d => !DeliverableStatus.IsTerminal(d.Status)))
        {
            var r = dr[d.Id];
            var dl = Lead(d.DisciplineId);
            if (d.Status != DeliverableStatus.OnHold && r.IsDueSoon && (r.TaskOverdue > 0 || r.TaskBlocked > 0 || (r.ProgressPct ?? 0) < 50)) // A-06
                Add("A-06", Severity.Warning, ItemType.Deliverable, d.Id, d.Key, d.Name,
                    $"Due {d.DueDate:yyyy-MM-dd} (in {until(d.DueDate!.Value)} days) at {(r.ProgressPct is null ? "no" : r.ProgressPct + " %")} progress; {r.TaskOverdue} overdue and {r.TaskBlocked} blocked tasks",
                    [d.OwnerId, dl, .. pms.Cast<Guid?>()], d.OwnerId, d.DisciplineId, 0, d.Priority, d.DueDate, s.DeliverableDueSoonDays,
                    new() { ["progress"] = r.ProgressPct, ["overdueTasks"] = r.TaskOverdue, ["blockedTasks"] = r.TaskBlocked });
            if (d.OwnerId is null && (d.Status != DeliverableStatus.NotStarted || r.IsDueSoon)) // A-13
                Add("A-13", Severity.Warning, ItemType.Deliverable, d.Id, d.Key, d.Name, d.Status != DeliverableStatus.NotStarted ? $"{d.Status} with no owner" : $"Due {d.DueDate:yyyy-MM-dd} with no owner",
                    [dl, .. pms.Cast<Guid?>()], null, d.DisciplineId, 0, d.Priority, d.DueDate, s.DeliverableDueSoonDays, new() { ["status"] = d.Status });
            if (r.IsDateInconsistent) // A-17
                Add("A-17", Severity.Warning, ItemType.Deliverable, d.Id, d.Key, d.Name, r.Inconsistencies[0].Text, [dl, .. pms.Cast<Guid?>()], d.OwnerId, d.DisciplineId, 0, d.Priority, d.DueDate, null, new());
            if (r.IsInactiveOwner) // A-18
            {
                var who = users.TryGetValue(d.OwnerId ?? Guid.Empty, out var uo) && !uo.IsActive ? uo : users.TryGetValue(d.ReviewerId ?? Guid.Empty, out var ur) ? ur : null;
                Add("A-18", Severity.Critical, ItemType.Deliverable, d.Id, d.Key, d.Name, $"{(who?.Id == d.OwnerId ? "Owner" : "Reviewer")} {who?.Name} is inactive",
                    [dl, .. pms.Cast<Guid?>(), who?.SupervisorId], d.OwnerId, d.DisciplineId, 0, d.Priority, d.DueDate, null, new());
            }
        }

        foreach (var disc in i.Disciplines.Where(d => d.IsActive && d.LeadId is { } l && users.TryGetValue(l, out var u) && !u.IsActive)) // TM-07 → A-18
            Add("A-18", Severity.Critical, ItemType.Discipline, disc.Id, null, disc.Name, $"Discipline Lead {Name(disc.LeadId)} is inactive",
                [.. pms.Cast<Guid?>(), users[disc.LeadId!.Value].SupervisorId], disc.LeadId, disc.Id, 0, null, null, null, new());

        foreach (var issue in highIssues) // A-07 (packet 014)
            Add("A-07", Severity.Critical, ItemType.Issue, issue.Id, issue.Key, issue.Title, $"High-severity issue {issue.Status}" + (issue.TargetDate is { } td ? $"; target {td:yyyy-MM-dd}" : ""),
                [issue.OwnerId, .. pms.Cast<Guid?>()], issue.OwnerId, issue.DisciplineId, issue.TargetDate is { } t2 && t2 < today ? today.DayNumber - t2.DayNumber : 0, Impact.High, issue.TargetDate, null, new());

        foreach (var a in (i.Actions ?? []).Where(a => ActionStatus.IsOpen(a.Status) && a.DueDate is { } ad && ad < today)) // MTG-01: discipline-owned actions reach the lead
        {
            var route = a.OwnerType == ActionOwnerType.Discipline && a.OwnerDisciplineId is { } od ? Lead(od) : a.OwnerUserId;
            if (a.OwnerType == ActionOwnerType.Discipline && a.OwnerDisciplineId is { } od2 && Lead(od2) is null) route = i.Project.PrimaryPmId; // flagged to the PM when the discipline has no lead
            Add("A-01", Severity.Warning, ItemType.Action, a.Id, a.Key, a.Text, $"Action due {a.DueDate:yyyy-MM-dd} — {today.DayNumber - a.DueDate!.Value.DayNumber} days overdue",
                [route, .. pms.Cast<Guid?>()], a.OwnerUserId, a.OwnerDisciplineId, today.DayNumber - a.DueDate.Value.DayNumber, null, a.DueDate, null, new());
        }

        if (i.Project.HealthOverride is null && i.Project.OverrideExpiresAt is { } exp && exp <= i.Project.Now && exp > i.Project.Now.AddDays(-s.HealthOverrideExpiryDays)) // A-15
            Add("A-15", Severity.Info, ItemType.Project, i.Project.Id, i.Project.Number, null, $"Health override expired on {exp:yyyy-MM-dd}", [i.Project.PrimaryPmId], i.Project.PrimaryPmId, null, 0, null, null, s.HealthOverrideExpiryDays, new());

        // ATT-03: a snooze hides an item until it expires or the severity rises.
        var snoozes = i.Snoozes ?? [];
        list = list.Select(a => snoozes.Any(z => z.RuleId == a.RuleId && z.ItemType == a.ItemType && z.ItemId == a.ItemId && z.Until > i.Project.Now
            && Severity.Rank(a.Severity) >= Severity.Rank(z.SeverityAtSnooze)) ? a with { Snoozed = true } : a).ToList();
        return Rank(list);
    }

    /// ATT-04: severity, then days overdue or blocked (descending), then priority, then due date.
    public static List<AttentionResult> Rank(IEnumerable<AttentionResult> items) =>
        items.OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).ThenBy(a => Priority.Rank(a.Priority))
            .ThenBy(a => a.DueDate ?? DateOnly.MaxValue).ThenBy(a => a.RuleId == "A-03" ? 0 : 1).ThenBy(a => a.ItemKey).ThenBy(a => a.RuleId).ToList(); // A-03 is the top task condition (D-11)

    public static readonly IReadOnlyDictionary<string, string> RuleNames = new Dictionary<string, string>
    {
        ["A-01"] = "Task overdue", ["A-02"] = "Task blocked", ["A-03"] = "Overdue predecessor blocking others", ["A-04"] = "Decision overdue or at risk",
        ["A-05"] = "Milestone approaching with incomplete prerequisites", ["A-06"] = "Deliverable due soon with open work", ["A-07"] = "High-severity open issue",
        ["A-08"] = "Task has no owner", ["A-09"] = "Task has no due date", ["A-10"] = "Stale work", ["A-11"] = "Review stalled", ["A-12"] = "Milestone without deliverables",
        ["A-13"] = "Deliverable without owner", ["A-14"] = "Milestone overdue", ["A-15"] = "Health override expired", ["A-16"] = "Task date inconsistent with deliverable",
        ["A-17"] = "Deliverable date inconsistent with milestone", ["A-18"] = "Work assigned to inactive user", ["A-19"] = "Task held past due", ["A-20"] = "Repeated due-date slips",
    };
}

/// Dependency graph helpers (D-03): refuse self-links, duplicates and cycles, reporting the loop.
public static class Graph
{
    /// Path from `from` to `to` following predecessor → successor edges, or null.
    public static List<Guid>? Path(IEnumerable<(Guid Pred, Guid Succ)> edges, Guid from, Guid to)
    {
        var next = edges.ToLookup(e => e.Pred, e => e.Succ);
        var prev = new Dictionary<Guid, Guid>();
        var queue = new Queue<Guid>([from]);
        var seen = new HashSet<Guid> { from };
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            if (n == to)
            {
                var path = new List<Guid> { to };
                while (path[0] != from) path.Insert(0, prev[path[0]]);
                return path;
            }
            foreach (var m in next[n]) if (seen.Add(m)) { prev[m] = n; queue.Enqueue(m); }
        }
        return null;
    }

    /// The loop a new edge pred → succ would close (succ ⇝ pred, then back to succ), or null when it is safe.
    public static List<Guid>? CycleIfAdded(IEnumerable<(Guid Pred, Guid Succ)> edges, Guid pred, Guid succ)
    {
        if (pred == succ) return [pred, succ];
        var path = Path(edges, succ, pred);
        return path is null ? null : [.. path, succ];
    }
}
