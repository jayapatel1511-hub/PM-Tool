using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Loads one project into the pure Evaluator, writes the materialised state tables and attention items, and
/// raises notifications for transitions only — "became blocked", "unblocked", new Critical items (§23.5).
public sealed class EvaluationService(HubDb db, SettingsStore store, TimeProvider clock, Notifier notify, ILogger<EvaluationService> log)
{
    static readonly JsonSerializerOptions Json = JsonOpts.Web;

    public async Task<EvalResult?> EvaluateProject(Guid projectId, bool notifyTransitions = true, CancellationToken ct = default)
    {
        // First-load evaluations can arrive concurrently from several reads or outbox events. Serialize
        // the complete read/evaluate/materialise unit per project so missing snapshot rows cannot race
        // into duplicate primary-key inserts. The project row is already the coordination lock used by
        // other project-wide commands and is held until every state/attention write is committed.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Coordination.Lock(db, projectId);
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Id == projectId, ct);
        if (p is null) return null;
        var s = await store.Get(db);
        var now = clock.GetUtcNow();
        var today = clock.Today(s);
        DateOnly L(DateTimeOffset t) => Clock.LocalDate(t, s);

        var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == projectId && m.RemovedAt == null).Select(m => new { m.UserId, m.Roles }).ToListAsync(ct);
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == projectId).Select(d => new DisciplineSnap(d.Id, d.Discipline!.Name, d.LeadUserId, d.IsActive, d.SortOrder)).ToListAsync(ct);
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId)
            .Select(m => new MilestoneSnap(m.Id, m.Key, m.Name, m.MilestoneType, m.Date, m.OriginalDate, m.IsComplete, m.IsCancelled, m.ProjectDisciplineId)).ToListAsync(ct);
        var deliverablesRaw = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId).ToListAsync(ct);
        var deliverables = deliverablesRaw.Select(d => new DeliverableSnap(d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.ReviewerId, d.MilestoneId, d.StartDate,
            d.DueDate, d.OriginalDueDate, d.Status, d.Priority, L(d.LastActivityAt), d.IssuedDate)).ToList();
        var tasksRaw = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync(ct);
        var taskIds = tasksRaw.Select(t => t.Id).ToList();
        var collaborators = await db.Collaborators.AsNoTracking().Where(c => taskIds.Contains(c.TaskId)).ToListAsync(ct);
        var tasks = tasksRaw.Select(t => new TaskSnap(t.Id, t.Key, t.Name, t.ProjectDisciplineId, t.DeliverableId, t.MilestoneId, t.AssigneeId, t.ReviewerId, t.Status,
            t.Priority, t.StartDate, t.DueDate, t.ProgressPct, t.EstimatedHours, t.ManualBlockType, t.ManualBlockReason, t.ManualBlockSetAt is { } mb ? L(mb) : null,
            L(t.LastActivityAt), t.ReviewRequestedAt is { } rr ? L(rr) : null, t.DueDateChangeCount, t.CompletedAt is { } ca ? L(ca) : null,
            collaborators.Where(c => c.TaskId == t.Id).Select(c => c.UserId).ToList())).ToList();
        var deps = await db.Dependencies.AsNoTracking().Where(d => d.ProjectId == projectId).Select(d => new DependencySnap(d.Id, d.PredecessorTaskId, d.SuccessorTaskId, d.LagDays)).ToListAsync(ct);
        var links = await db.ItemLinks.AsNoTracking().Where(l => l.ProjectId == projectId && l.SourceType == ItemType.Decision && l.TargetType == ItemType.Task && l.Relation == ItemRelation.BlockedByDecision)
            .Select(l => new { l.SourceId, l.TargetId }).ToListAsync(ct);
        var decisions = (await db.Decisions.AsNoTracking().Where(d => d.ProjectId == projectId).ToListAsync(ct))
            .Select(d => new DecisionSnap(d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, d.ImpactLevel, d.OwnerUserId, d.RequestedById,
                links.Where(l => l.SourceId == d.Id).Select(l => l.TargetId).ToList())).ToList();
        var issues = await db.Issues.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => new IssueSnap(x.Id, x.Key, x.Title, x.Status, x.Severity, x.OwnerId, x.TargetResolutionDate, x.ProjectDisciplineId)).ToListAsync(ct);
        var risks = await db.Risks.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => new RiskSnap(x.Id, x.Key, x.Title, x.Status, x.Probability, x.Impact, x.ReviewDate, x.OwnerId, x.ProjectDisciplineId)).ToListAsync(ct);
        var actions = await db.Actions.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => new ActionSnap(x.Id, x.Key, x.Text, x.Status, x.OwnerType, x.OwnerUserId, x.OwnerDisciplineId, x.DueDate)).ToListAsync(ct);
        var dlDeps = await db.DeliverableDependencies.AsNoTracking().Where(d => d.ProjectId == projectId).Select(d => new DependencySnap(d.Id, d.PredecessorDeliverableId, d.SuccessorDeliverableId, d.LagDays)).ToListAsync(ct);
        var snoozes = await db.Snoozes.AsNoTracking().Where(z => z.ProjectId == projectId && z.EndedAt == null && z.SnoozedUntil > now)
            .Select(z => new SnoozeSnap(z.RuleId, z.ItemType, z.ItemId, z.SnoozedUntil, z.SeverityAtSnooze)).ToListAsync(ct);
        var prevStates = await db.TaskStates.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.TaskId, ct);
        var userIds = tasks.SelectMany(t => new[] { t.AssigneeId, t.ReviewerId }).Concat(deliverables.SelectMany(d => new[] { d.OwnerId, d.ReviewerId }))
            .Concat(decisions.Select(d => d.OwnerUserId)).Concat(disciplines.Select(d => d.LeadId)).Concat(members.Select(m => (Guid?)m.UserId))
            .Concat(tasks.SelectMany(t => t.Collaborators ?? []).Cast<Guid?>()).Append(p.ProjectManagerId).OfType<Guid>().Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).Select(u => new UserSnap(u.Id, u.DisplayName, u.IsActive, u.SupervisorId)).ToListAsync(ct);
        WorkCalendar? calendar = null;
        if (s.WorkingDaysEnabled)
            calendar = new WorkCalendar(await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == p.OfficeId).Select(h => h.Date).ToListAsync(ct));

        var input = new EvalInput(
            new ProjectSnap(p.Id, p.ProjectNumber, p.Status, p.ProjectManagerId, members.Where(m => m.Roles.Contains(ProjectRole.PM)).Select(m => m.UserId).ToList(),
                p.HealthOverride, p.HealthOverrideExpiresAt, now),
            today, s, users, disciplines, milestones, deliverables, tasks, deps, decisions, members.Select(m => m.UserId).ToHashSet(),
            prevStates.Where(x => x.Value.IsBlocked && x.Value.BlockedSince is not null).ToDictionary(x => x.Key, x => x.Value.BlockedSince!.Value),
            snoozes, issues, risks, actions, dlDeps, calendar);
        var r = Evaluator.Evaluate(input);

        // ---------- Materialise (reads use these tables; no rule logic in SQL, §23.5 step 6) ----------
        var byKey = tasks.ToDictionary(t => t.Id);
        var was = prevStates.ToDictionary(x => x.Key, x => (x.Value.IsBlocked, x.Value.IsWaiting)); // read before the rows below are overwritten
        foreach (var tr in r.Tasks)
        {
            if (!prevStates.TryGetValue(tr.TaskId, out var st)) { st = new TaskState { TaskId = tr.TaskId, ProjectId = projectId }; db.TaskStates.Add(st); }
            st.IsOverdue = tr.IsOverdue; st.DaysOverdue = tr.DaysOverdue; st.IsDueSoon = tr.IsDueSoon; st.IsWaiting = tr.IsWaiting; st.IsBlocked = tr.IsBlocked;
            st.BlockedSince = tr.BlockedSince; st.DaysBlocked = tr.DaysBlocked; st.BlockedBy = JsonSerializer.Serialize(tr.Blockers, Json);
            st.IsBlocking = tr.IsBlocking; st.BlockingCount = tr.BlockingTaskIds.Count; st.BlockingTaskIds = [.. tr.BlockingTaskIds];
            st.IsStale = tr.IsStale; st.StaleDays = tr.StaleDays; st.IsUnassigned = tr.IsUnassigned; st.IsMissingDueDate = tr.IsMissingDueDate;
            st.IsDateInconsistent = tr.IsDateInconsistent; st.InconsistencyDetail = JsonSerializer.Serialize(tr.Inconsistencies, Json);
            st.IsInactiveOwner = tr.IsInactiveOwner; st.IsHeldPastDue = tr.IsHeldPastDue; st.IsReviewStalled = tr.IsReviewStalled;
            st.AffectedMilestoneIds = [.. tr.AffectedMilestoneIds]; st.Notes = [.. tr.Notes]; st.EvaluatedAt = now;
        }
        foreach (var gone in prevStates.Keys.Except(r.Tasks.Select(x => x.TaskId))) db.TaskStates.Remove(prevStates[gone]);

        var prevDel = await db.DeliverableStates.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.DeliverableId, ct);
        foreach (var dr in r.Deliverables)
        {
            if (!prevDel.TryGetValue(dr.DeliverableId, out var st)) { st = new DeliverableState { DeliverableId = dr.DeliverableId, ProjectId = projectId }; db.DeliverableStates.Add(st); }
            st.ProgressPct = dr.ProgressPct; st.TaskTotal = dr.TaskTotal; st.TaskComplete = dr.TaskComplete; st.TaskCancelled = dr.TaskCancelled; st.TaskOpen = dr.TaskOpen;
            st.TaskOverdue = dr.TaskOverdue; st.TaskBlocked = dr.TaskBlocked; st.EstimatedHoursTotal = dr.EstimatedHoursTotal; st.RemainingHours = dr.RemainingHours;
            st.IsOverdue = dr.IsOverdue; st.DaysOverdue = dr.DaysOverdue; st.IsDueSoon = dr.IsDueSoon; st.IsAtRisk = dr.IsAtRisk;
            st.AtRiskReasons = JsonSerializer.Serialize(dr.AtRiskReasons, Json); st.IsUnassigned = dr.IsUnassigned; st.IsStale = dr.IsStale;
            st.IsDateInconsistent = dr.IsDateInconsistent; st.InconsistencyDetail = JsonSerializer.Serialize(dr.Inconsistencies, Json); st.SlipDays = dr.SlipDays;
            st.IsInactiveOwner = dr.IsInactiveOwner; st.IssuedWithOpenWork = dr.IssuedWithOpenWork; st.MilestoneCancelled = dr.MilestoneCancelled;
            st.IsWaiting = dr.IsWaiting; st.IsBlocked = dr.IsBlocked; st.BlockedBy = JsonSerializer.Serialize(dr.Blockers, Json); st.BlockingCount = dr.BlockingCount;
            st.DerivedPredecessorIds = [.. dr.DerivedPredecessorIds]; st.DerivedSuccessorIds = [.. dr.DerivedSuccessorIds]; st.EvaluatedAt = now;
        }
        foreach (var gone in prevDel.Keys.Except(r.Deliverables.Select(x => x.DeliverableId))) db.DeliverableStates.Remove(prevDel[gone]);

        var prevMs = await db.MilestoneStates.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.MilestoneId, ct);
        var msTransitions = new List<(MilestoneSnap M, string Status)>();
        foreach (var mr in r.Milestones)
        {
            if (!prevMs.TryGetValue(mr.MilestoneId, out var st)) { st = new MilestoneState { MilestoneId = mr.MilestoneId, ProjectId = projectId }; db.MilestoneStates.Add(st); }
            if (mr.Status is MilestoneStatus.AtRisk or MilestoneStatus.Overdue && st.Status != mr.Status && st.EvaluatedAt != default)
                msTransitions.Add((milestones.First(m => m.Id == mr.MilestoneId), mr.Status));
            st.Status = mr.Status; st.StatusReasons = JsonSerializer.Serialize(mr.Reasons, Json); st.DaysRemaining = mr.DaysRemaining; st.SlipDays = mr.SlipDays;
            st.DeliverableTotal = mr.DeliverableTotal; st.DeliverableIssued = mr.DeliverableIssued; st.TaskTotal = mr.TaskTotal; st.TaskComplete = mr.TaskComplete;
            st.TaskOpen = mr.TaskOpen; st.TaskOverdue = mr.TaskOverdue; st.TaskBlocked = mr.TaskBlocked; st.EvaluatedAt = now;
        }
        foreach (var gone in prevMs.Keys.Except(r.Milestones.Select(x => x.MilestoneId))) db.MilestoneStates.Remove(prevMs[gone]);

        var prevDec = await db.DecisionStates.Where(x => x.ProjectId == projectId).ToDictionaryAsync(x => x.DecisionId, ct);
        var decBecameOverdue = new List<DecisionSnap>();
        foreach (var dr in r.Decisions)
        {
            if (!prevDec.TryGetValue(dr.DecisionId, out var st)) { st = new DecisionState { DecisionId = dr.DecisionId, ProjectId = projectId }; db.DecisionStates.Add(st); }
            if (dr.IsOverdue && !st.IsOverdue) decBecameOverdue.Add(decisions.First(d => d.Id == dr.DecisionId));
            st.IsOverdue = dr.IsOverdue; st.DaysOverdue = dr.DaysOverdue; st.IsDueSoon = dr.IsDueSoon; st.BlockingCount = dr.BlockingTaskIds.Count;
            st.BlockingTaskIds = [.. dr.BlockingTaskIds]; st.IsInactiveOwner = dr.IsInactiveOwner; st.EvaluatedAt = now;
        }
        foreach (var gone in prevDec.Keys.Except(r.Decisions.Select(x => x.DecisionId))) db.DecisionStates.Remove(prevDec[gone]);

        var ps = await db.ProjectStates.FirstOrDefaultAsync(x => x.ProjectId == projectId, ct);
        if (ps is null) { ps = new ProjectState { ProjectId = projectId }; db.ProjectStates.Add(ps); }
        var visibleAttention = r.Attention.Where(a => !a.Snoozed).ToList();
        ps.ComputedHealth = r.Project.Health.Health; ps.HealthReasons = JsonSerializer.Serialize(r.Project.Health.Reasons, Json);
        ps.Inputs = JsonSerializer.Serialize(r.Project.Health.Inputs, Json); ps.Counts = JsonSerializer.Serialize(r.Project.Counts, Json);
        ps.DisciplineStates = JsonSerializer.Serialize(r.Project.Disciplines, Json); ps.NextMilestoneId = r.Project.NextMilestoneId;
        ps.NextSubmissionMilestoneId = r.Project.NextSubmissionId; ps.ProgressPct = r.Project.ProgressPct;
        ps.OverdueTasks = r.Project.Counts["tasksOverdue"]; ps.BlockedTasks = r.Project.Counts["tasksBlocked"]; ps.OverdueDecisions = r.Project.Counts["decisionsOverdue"];
        ps.HighIssues = r.Project.Counts["issuesHigh"]; ps.AttentionCritical = visibleAttention.Count(a => a.Severity == Severity.Critical);
        ps.AttentionWarning = visibleAttention.Count(a => a.Severity == Severity.Warning); ps.EvaluatedAt = now;

        // ATT-02: an item is identified by rule and item; it keeps its first-detected time while the condition holds.
        var prevAtt = await db.Attention.Where(a => a.ProjectId == projectId).ToListAsync(ct);
        var prevByKey = prevAtt.GroupBy(a => (a.RuleId, a.ItemType, a.ItemId)).ToDictionary(g => g.Key, g => g.First());
        var newCritical = new List<AttentionResult>();
        var newA03 = new List<AttentionResult>();
        var seenKeys = new HashSet<(string, string, Guid)>();
        foreach (var a in r.Attention)
        {
            var key = (a.RuleId, a.ItemType, a.ItemId);
            if (!seenKeys.Add(key)) continue; // one item per rule and item
            if (!prevByKey.TryGetValue(key, out var row))
            {
                row = new AttentionItem { ProjectId = projectId, RuleId = a.RuleId, ItemType = a.ItemType, ItemId = a.ItemId, FirstDetectedAt = now };
                db.Attention.Add(row);
                if (a.Severity == Severity.Critical && !a.Snoozed) newCritical.Add(a);
                if (a.RuleId == "A-03") newA03.Add(a);
            }
            else if (a.Severity == Severity.Critical && row.Severity != Severity.Critical && !a.Snoozed) newCritical.Add(a);
            row.ItemKey = a.ItemKey; row.ItemName = a.ItemName; row.Severity = a.Severity; row.Message = a.Message; row.Why = JsonSerializer.Serialize(a.Why, Json);
            row.RouteToUserIds = [.. a.RouteTo]; row.OwnerUserId = a.OwnerId; row.ProjectDisciplineId = a.DisciplineId; row.DaysOverdueOrBlocked = a.DaysOverdueOrBlocked;
            row.Priority = a.Priority; row.DueDate = a.DueDate; row.LastEvaluatedAt = now;
        }
        foreach (var gone in prevAtt.Where(a => !seenKeys.Contains((a.RuleId, a.ItemType, a.ItemId)))) db.Attention.Remove(gone);
        await db.SaveChangesAsync(ct);

        if (notifyTransitions && p.Status == ProjectStatus.Active)
        {
            try { await Notices(p, r, was, tasksRaw, byKey, msTransitions, decBecameOverdue, newCritical, newA03, disciplines, deliverablesRaw); }
            catch (Exception e) { log.LogError(e, "Transition notices failed for project {ProjectId}", projectId); }
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return r;
    }

    async Task Notices(Project p, EvalResult r, Dictionary<Guid, (bool IsBlocked, bool IsWaiting)> prev, List<WorkTask> tasksRaw, Dictionary<Guid, TaskSnap> tasks,
        List<(MilestoneSnap M, string Status)> msTransitions, List<DecisionSnap> decOverdue, List<AttentionResult> newCritical, List<AttentionResult> newA03,
        List<DisciplineSnap> disciplines, List<Deliverable> deliverables)
    {
        var link = (string type, Guid id) => $"/projects/{p.ProjectNumber}/tasks?panel={type}:{id}";
        foreach (var tr in r.Tasks)
        {
            var t = tasks[tr.TaskId];
            var raw = tasksRaw.First(x => x.Id == t.Id);
            var seen = prev.TryGetValue(t.Id, out var before);
            var (wasBlocked, wasWaiting) = before;
            var item = new NotifyItem(p.Id, ItemType.Task, t.Id, t.Key, link("Task", t.Id), p.ProjectNumber);
            if (tr.IsBlocked && !wasBlocked && seen)
            {
                var manualBySelf = tr.Blockers.All(b => b.Type == "manual") && raw.ManualBlockSetBy == t.AssigneeId;
                if (!manualBySelf)
                {
                    var first = tr.Blockers.FirstOrDefault(b => b.Blocking);
                    await notify.Send(NotificationEvents.TaskBlocked, [t.AssigneeId], item,
                        Text.Get("notify.task_blocked", t.Key, t.Name, first?.Key ?? first?.Name ?? "", first?.Overdue == true ? Text.Get("notify.overdue_word") : ""), actorOverride: Guid.Empty);
                }
            }
            if (!tr.IsBlocked && !tr.IsWaiting && (wasBlocked || wasWaiting) && !TaskStatuses.IsTerminal(t.Status) && t.Status != TaskStatuses.OnHold) // D-07: a held task is not startable
                await notify.Send(NotificationEvents.TaskUnblocked, [t.AssigneeId], item, Text.Get("notify.task_unblocked", t.Key, t.Name), actorOverride: Guid.Empty);
        }
        foreach (var a in newA03) // "once, on first detection" by email to the predecessor assignee
            await notify.Send(NotificationEvents.BlockingOverdue, [a.OwnerId], new NotifyItem(p.Id, ItemType.Task, a.ItemId, a.ItemKey, link("Task", a.ItemId), p.ProjectNumber),
                Text.Get("notify.blocking_overdue", a.ItemKey ?? "", a.ItemName ?? "", a.Message), actorOverride: Guid.Empty);
        foreach (var a in newCritical.Where(x => x.RuleId != "A-03"))
            await notify.Send(NotificationEvents.AttentionCritical, a.RouteTo.Cast<Guid?>(), new NotifyItem(p.Id, a.ItemType, a.ItemId, a.ItemKey, $"/projects/{p.ProjectNumber}?attention={a.RuleId}", p.ProjectNumber),
                Text.Get("notify.attention_critical", a.RuleId, Evaluator.RuleNames[a.RuleId], a.ItemKey ?? a.ItemName ?? "", a.Message), actorOverride: Guid.Empty);
        foreach (var (m, status) in msTransitions)
        {
            var leads = deliverables.Where(d => d.MilestoneId == m.Id).Select(d => disciplines.FirstOrDefault(x => x.Id == d.ProjectDisciplineId)?.LeadId);
            await notify.Send(NotificationEvents.MilestoneStatus, [p.ProjectManagerId, .. leads], new NotifyItem(p.Id, ItemType.Milestone, m.Id, m.Key, $"/projects/{p.ProjectNumber}/milestones?panel=Milestone:{m.Id}", p.ProjectNumber),
                Text.Get("notify.milestone_status", m.Key, m.Name, status), actorOverride: Guid.Empty);
        }
        foreach (var d in decOverdue)
            await notify.Send(NotificationEvents.DecisionOverdue, [d.RequestedById, d.OwnerUserId, p.ProjectManagerId], new NotifyItem(p.Id, ItemType.Decision, d.Id, d.Key, $"/projects/{p.ProjectNumber}/decisions?panel=Decision:{d.Id}", p.ProjectNumber),
                Text.Get("notify.decision_overdue", d.Key, d.Subject, d.RequiredBy.ToString("yyyy-MM-dd")), actorOverride: Guid.Empty);
    }

    /// Evaluates now when the stored state is missing or from before today's date (a missed nightly run).
    public async Task EnsureFresh(Guid projectId)
    {
        var s = await store.Get(db);
        var state = await db.ProjectStates.AsNoTracking().Where(x => x.ProjectId == projectId).Select(x => (DateTimeOffset?)x.EvaluatedAt).FirstOrDefaultAsync();
        if (state is null || Clock.LocalDate(state.Value, s) < clock.Today(s)) await EvaluateProject(projectId, notifyTransitions: state is not null);
    }
}

/// Consumes outbox events a moment after each save and re-evaluates the affected projects (§23.5 steps 2–3).
public sealed class EvaluationWorker(IServiceProvider root, EvaluationSignal signal, IConfiguration cfg, ILogger<EvaluationWorker> log, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (cfg["Evaluation:Worker"] == "false") return;
        var debounce = TimeSpan.FromMilliseconds(int.TryParse(cfg["Evaluation:DebounceMs"], out var ms) ? ms : 400);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await signal.WaitAsync(TimeSpan.FromSeconds(10), ct);
                await Task.Delay(debounce, ct); // batch the saves of one user action per project
                await ProcessOnce(root, clock, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception e) { log.LogError(e, "Evaluation worker failed"); await Task.Delay(TimeSpan.FromSeconds(5), ct); }
        }
    }

    public static async Task<int> ProcessOnce(IServiceProvider root, TimeProvider clock, CancellationToken ct)
    {
        using var scope = root.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            if (!await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock(hashtext('evaluation-worker')) AS \"Value\"").SingleAsync(ct)) return 0;
            var events = await db.Outbox.Where(o => o.ProcessedAt == null && o.Attempts < 10).OrderBy(o => o.CreatedAt).Take(500).ToListAsync(ct);
            var projects = events.Where(e => e.ProjectId is not null).Select(e => e.ProjectId!.Value).ToHashSet();
            foreach (var e in events.Where(e => e.EventType == "UserChanged"))
            {
                var uid = JsonDocument.Parse(e.Payload).RootElement.GetProperty("userId").GetGuid();
                foreach (var pid in await db.ProjectMembers.Where(m => m.UserId == uid && m.RemovedAt == null).Select(m => m.ProjectId)
                    .Union(db.Tasks.Where(t => t.AssigneeId == uid || t.ReviewerId == uid).Select(t => t.ProjectId))
                    .Union(db.Deliverables.Where(d => d.OwnerId == uid).Select(d => d.ProjectId)).Distinct().ToListAsync(ct))
                    projects.Add(pid);
            }
            foreach (var pid in projects)
            {
                using var work = root.CreateScope();
                work.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
                try { await work.ServiceProvider.GetRequiredService<EvaluationService>().EvaluateProject(pid, ct: ct); }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    root.GetRequiredService<ILogger<EvaluationWorker>>().LogError(ex, "Evaluation failed for project {ProjectId}", pid);
                    foreach (var e in events.Where(e => e.ProjectId == pid)) { e.Attempts++; e.LastError = ex.GetType().Name; }
                    events.RemoveAll(e => e.ProjectId == pid);
                }
            }
            var now = clock.GetUtcNow();
            foreach (var e in events) e.ProcessedAt = now;
            await db.SaveChangesAsync(ct);
            return projects.Count;
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock(hashtext('evaluation-worker'))", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }
}

/// A fresh scope, acting as System, for evaluating one project inside a long job.
static class EvaluationScope
{
    public static IServiceScope For(IServiceProvider sp)
    {
        var scope = sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
        return scope;
    }
}

/// 15-minute safety net: retries failed outbox events and evaluates projects whose state predates today (§23.5 step 5).
public sealed class EvaluationRetryJob : IJob
{
    public string Name => "evaluation-retry";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromMinutes(15), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var n = await EvaluationWorker.ProcessOnce(sp, sp.GetRequiredService<TimeProvider>(), ct);
        var db = sp.GetRequiredService<HubDb>();
        var s = await sp.GetRequiredService<SettingsStore>().Get(db);
        var today = sp.GetRequiredService<TimeProvider>().Today(s);
        var stale = await db.Projects.Where(p => p.Status == ProjectStatus.Active && !db.ProjectStates.Any(x => x.ProjectId == p.Id)).Select(p => p.Id).ToListAsync(ct);
        foreach (var pid in stale)
        {
            using var scope = EvaluationScope.For(sp);
            await scope.ServiceProvider.GetRequiredService<EvaluationService>().EvaluateProject(pid, ct: ct);
        }
        return new { outboxProjects = n, backfilled = stale.Count, today };
    }
}

/// Nightly after midnight in the organisation time zone: date rollover, health snapshots, override and snooze
/// expiry (§16.5, §16.7, §23.5 step 4, AC-HLT-05).
public sealed class NightlyJob : IJob
{
    public const string JobName = "nightly-evaluation";
    public string Name => JobName;
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.DailyAt("00:15", now, last, s);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var clock = sp.GetRequiredService<TimeProvider>();
        var s = await sp.GetRequiredService<SettingsStore>().Get(db);
        var now = clock.GetUtcNow();
        var today = clock.Today(s);
        var notify = sp.GetRequiredService<Notifier>();
        var expired = 0;
        foreach (var p in await db.Projects.Where(p => p.HealthOverride != null && p.HealthOverrideExpiresAt <= now).ToListAsync(ct))
        {
            var reported = p.HealthOverride;
            p.HealthOverride = null; // AC-HLT-03: removed by the system and logged with actor System
            db.Audit.Note(p, action: "HealthOverrideExpired");
            await notify.Send(NotificationEvents.HealthOverride, [p.ProjectManagerId], new NotifyItem(p.Id, ItemType.Project, p.Id, p.ProjectNumber, $"/projects/{p.ProjectNumber}", p.ProjectNumber),
                Text.Get("notify.override_expired", p.ProjectNumber, p.Name, reported ?? ""));
            expired++;
        }
        foreach (var z in await db.Snoozes.Where(z => z.EndedAt == null && z.SnoozedUntil <= now).ToListAsync(ct))
        {
            z.EndedAt = now;
            db.LogEvent(ItemType.Snooze, z.Id, "SnoozeExpired", "attention", z.ProjectId, z.RuleId, null);
        }
        await db.SaveChangesAsync(ct);
        var ids = await db.Projects.Where(p => p.Status == ProjectStatus.Active).Select(p => p.Id).ToListAsync(ct);
        foreach (var pid in ids)
        {
            // One unit of work per project: tracked states never pile up across hundreds of projects (packet 011 scale run).
            using var scope = EvaluationScope.For(sp);
            var pdb = scope.ServiceProvider.GetRequiredService<HubDb>();
            var r = await scope.ServiceProvider.GetRequiredService<EvaluationService>().EvaluateProject(pid, ct: ct);
            if (r is null) continue;
            var p = await pdb.Projects.AsNoTracking().FirstAsync(x => x.Id == pid, ct);
            var reported = p.HealthOverride is { } ho && p.HealthOverrideExpiresAt > now ? ho : r.Project.Health.Health;
            var snap = await pdb.HealthSnapshots.FirstOrDefaultAsync(x => x.ProjectId == pid && x.SnapshotDate == today, ct);
            if (snap is null) { snap = new ProjectHealthSnapshot { ProjectId = pid, SnapshotDate = today }; pdb.HealthSnapshots.Add(snap); }
            snap.ComputedHealth = r.Project.Health.Health;
            snap.ReportedHealth = reported;
            snap.Inputs = JsonSerializer.Serialize(r.Project.Health.Inputs, JsonOpts.Web);
            snap.Counts = JsonSerializer.Serialize(r.Project.Counts, JsonOpts.Web);
            await pdb.SaveChangesAsync(ct);
        }
        // Job run records are operational, not history: keep 90 days (the audit trail is the activity log).
        var pruned = await db.JobRuns.Where(r => r.StartedAt < now.AddDays(-90)).ExecuteDeleteAsync(ct);
        await db.Idempotency.Where(r => r.CreatedAt < now.AddDays(-2)).ExecuteDeleteAsync(ct); // replays last a day (§25)
        return new { projects = ids.Count, overridesExpired = expired, date = today, jobRunsPruned = pruned };
    }
}

/// The working-day calendar of each project: weekends plus its office's holidays and the organisation's (§10.4, packet 021).
public static class Calendars
{
    public static async Task<Func<Guid, WorkCalendar>> For(HubDb db, OrgSettings s, IReadOnlyCollection<Guid> projectIds)
    {
        if (!s.WorkingDaysEnabled) return _ => WorkCalendar.Weekdays;
        var offices = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.OfficeId);
        var holidays = await db.Holidays.AsNoTracking().Select(h => new { h.OfficeId, h.Date }).ToListAsync();
        var cache = new Dictionary<Guid, WorkCalendar>();
        return pid =>
        {
            var office = offices.GetValueOrDefault(pid);
            return cache.TryGetValue(office, out var c) ? c : cache[office] = new WorkCalendar(holidays.Where(h => h.OfficeId == null || h.OfficeId == office).Select(h => h.Date));
        };
    }
}
