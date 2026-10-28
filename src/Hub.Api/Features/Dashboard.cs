using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Project Dashboard (§13.1, FR-DASH-01) and Weekly Coordination (§12.13, §13.9, FR-WC-01/02). Every count is the row
/// count of the list its link opens, computed by the same list query (§12.17 "same code path").
public static class DashboardEndpoints
{
    /// §49.5 and §49.6 (packet 045 Part D): the discipline status segments here and Portfolio's overdue deliverables.
    public const string RollupsFlag = "status_rollups";

    /// FR-RCV-14 / D10: the four status bar segments, in order. Cancelled is in none of them.
    static readonly string[] NotStartedSegment = [DeliverableStatus.NotStarted];
    static readonly string[] InProgressSegment = [DeliverableStatus.InProgress, DeliverableStatus.InReview, DeliverableStatus.RevisionRequired, DeliverableStatus.ReadyToIssue];
    static readonly string[] IssuedSegment = [DeliverableStatus.Issued, DeliverableStatus.Accepted];
    static readonly string[] OnHoldSegment = [DeliverableStatus.OnHold];

    /// A figure with the query string of the list it was counted from, so every number opens the list behind it (§12.17).
    public sealed record DashboardCount(int Value, string Link);
    /// The Project Dashboard (§13.1). `Tasks.Backlog`, `DisciplineStatus` and `DisciplineBacklog` are sent only while their flags are on, so the response is unchanged with them off (FR-DLV-08).
    public sealed record DashboardView(IReadOnlyList<MilestoneRow> Milestones, DashboardTasks Tasks, DashboardDeliverables Deliverables, DashboardDecisions Decisions, DashboardIssues Issues,
        DashboardRisks Risks, DashboardRegisters Registers, IReadOnlyList<DashboardDiscipline> Disciplines, IReadOnlyDictionary<string, DashboardCount> DisciplineDue14, DashboardTaskList DueThisWeek,
        DashboardTaskList Blocked, IReadOnlyList<ActivityEndpoints.ActivityEntry> Activity, DashboardState? State,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, DashboardStatusSegments>? DisciplineStatus = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, DashboardCount>? DisciplineBacklog = null);
    public sealed record DashboardTasks(DashboardCount Total, DashboardCount Complete, DashboardCount InProgress, DashboardCount Review, DashboardCount Overdue, DashboardCount Blocked,
        DashboardCount Waiting, DashboardCount Unassigned, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DashboardCount? Backlog = null);
    public sealed record DashboardDeliverables(DashboardCount Upcoming, DashboardCount AtRisk, DashboardCount Issued, DashboardCount Total);
    public sealed record DashboardDecisions(DashboardCount Pending, DashboardCount Overdue);
    public sealed record DashboardIssues(int Open, int High);
    public sealed record DashboardRisks(int High);
    public sealed record DashboardRegisters(DashboardCount IssuesOpen, DashboardCount IssuesHigh, DashboardCount RisksHigh);
    /// A few of a list's rows, its full size and the query string of the whole list.
    public sealed record DashboardTaskList(IReadOnlyList<TaskQueries.Row> Items, int Total, string Link);
    public sealed record DashboardState(DateTimeOffset EvaluatedAt);
    /// One discipline's deliverables by status segment (FR-RCV-14).
    public sealed record DashboardStatusSegments(DashboardCount NotStarted, DashboardCount InProgress, DashboardCount Issued, DashboardCount OnHold);
    /// One discipline's evaluated row as the evaluator stored it. The members follow the stored document's own order, which the database returns shortest name first (jsonb),
    /// so the response is the same bytes it was when the stored document was passed through.
    public sealed record DashboardDiscipline(string Name, int Open, string Health, Guid? LeadId, int Blocked, int Overdue, IReadOnlyList<DisciplineReason> Reasons, int Waiting, Guid? NextDueId,
        int SortOrder, string? NextDueKey, DateOnly? NextDueDate, string? NextDueName, string? NextDueType, Guid DisciplineId, int DeliverablesDue14);
    /// A reason behind a colour or indicator (§16), in the stored order.
    public sealed record DisciplineReason(string? Rule, string Text, string? Colour, IReadOnlyDictionary<string, object?>? Values, object? Threshold);

    /// Weekly Coordination (§12.13): the meeting agenda in the order of its sections.
    public sealed record WeeklyCoordination(WeeklyWindow Window, WeeklyHeadline Headline, IReadOnlyList<MilestoneRow> Milestones, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables,
        IReadOnlyList<WeeklyDecision> Decisions, IReadOnlyList<TaskQueries.Row> Blocked, IReadOnlyList<TaskQueries.Row> Overdue, IReadOnlyList<TaskQueries.Row> DueThisWeek,
        IReadOnlyList<WeeklyRound> Disciplines, IReadOnlyList<WeeklyIssue> Issues, IReadOnlyList<WeeklyRisk> Risks, IReadOnlyList<MeetingEndpoints.MeetingActionRow> Waiting,
        WeeklyCompleted Completed, WeeklyUpcoming Upcoming, WeeklyHeld Held);
    /// This week starts on the project's coordination day; `Since` is the last review, or seven days back when there was none.
    public sealed record WeeklyWindow(WeekSpan ThisWeek, WeekSpan NextWeek, DateTimeOffset Since, DateTimeOffset? LastCoordinationReviewedAt, string? ReviewedBy, bool CanMarkReviewed);
    public sealed record WeekSpan(DateOnly From, DateOnly To);
    public sealed record WeeklyHeadline(string? ComputedHealth, JsonElement? HealthReasons, DashboardCount OverdueTasks, DashboardCount BlockedTasks, int DecisionsOverdue, DashboardCount DeliverablesAtRisk,
        MilestoneRow? NextMilestone);
    public sealed record WeeklyDecision(Guid Id, string Key, string Subject, string Status, DateOnly RequiredByDate, string ImpactLevel, Guid? OwnerUserId, string? OwnerName, bool IsOverdue, int DaysOverdue, int Blocking);
    /// A discipline's evaluated row with its three most pressing open tasks.
    public sealed record WeeklyRound(DashboardDiscipline Discipline, IReadOnlyList<TaskQueries.Row> Top);
    public sealed record WeeklyIssue(Guid Id, string Key, string Title, string Status, string Severity, DateOnly? TargetResolutionDate);
    public sealed record WeeklyRisk(Guid Id, string Key, string Title, string Status, int Probability, int Impact, DateOnly? ReviewDate);
    public sealed record WeeklyDecided(Guid Id, string Key, string Subject, string? DecisionText, DateOnly? DecisionDate);
    public sealed record WeeklyCompleted(IReadOnlyList<TaskQueries.Row> Tasks, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables, IReadOnlyList<WeeklyDecided> Decisions);
    public sealed record WeeklyUpcoming(IReadOnlyList<TaskQueries.Row> Tasks, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables);
    public sealed record WeeklyHeld(IReadOnlyList<TaskQueries.Row> Tasks, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables);
    public sealed record WeeklyReviewed(DateTimeOffset LastCoordinationReviewedAt, int RowVersion);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/dashboard", Dashboard);
        api.MapGet("/projects/{id:guid}/coordination", Coordination);
        api.MapPost("/projects/{id:guid}/coordination/reviewed", async Task<Ok<WeeklyReviewed>> (Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.RunCoordination(access.Actor, ctx)); // FR-004: the PM or a lead
            var at = clock.GetUtcNow();
            p.LastCoordinationReviewedAt = at;
            p.LastCoordinationReviewedBy = me.Id;
            db.Audit.Note(p, action: "CoordinationReviewed");
            await db.SaveChangesAsync();
            return TypedResults.Ok(new WeeklyReviewed(at, p.RowVersion));
        });
    }

    sealed class Scope(HubDb db, Guid projectId, Guid? disciplineId, Guid me, DateOnly today, OrgSettings s)
    {
        public string? Disc => disciplineId?.ToString();

        public IQueryable<WorkTask> Tasks(params (string Key, string? Value)[] filters)
        {
            var d = filters.ToDictionary(x => x.Key, x => x.Value);
            if (Disc is not null) d["disciplineId"] = Disc;
            return TaskQueries.Apply(db, db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId), TaskFilter.Of(d), me, today, s);
        }

        public IQueryable<Deliverable> Deliverables(string? status = null, DateOnly? dueFrom = null, DateOnly? dueTo = null, string? indicator = null) =>
            DeliverableEndpoints.Filter(db, db.Deliverables.AsNoTracking().Where(x => x.ProjectId == projectId), Disc, status, dueFrom: dueFrom, dueTo: dueTo, indicator: indicator);

        /// The query string a count's link carries, so the list opens with the same filter.
        public string Link(params (string Key, string? Value)[] filters) =>
            string.Join("&", filters.Append(("disciplineId", Disc)).Where(x => x.Item2 is not null).Select(x => $"{x.Item1}={Uri.EscapeDataString(x.Item2!)}"));
    }

    static async Task<DashboardCount> Count<T>(IQueryable<T> q, string link) => new(await q.CountAsync(), link);

    static async Task<DashboardCount> Segment(Scope ds, string[] statuses) => await Count(ds.Deliverables(status: string.Join(",", statuses)), ds.Link(("status", string.Join(",", statuses))));

    static async Task<Ok<DashboardView>> Dashboard(Guid id, Guid? disciplineId, bool? importantOnly, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (p, _) = await access.Project(id, track: false);
        await eval.EnsureFresh(id);
        var s = await store.Get(db);
        var today = clock.Today(s);
        var sc = new Scope(db, id, disciplineId, me.Id, today, s);
        var state = await db.ProjectStates.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id);
        var counts = JsonSerializer.Deserialize<Dictionary<string, int>>(state?.Counts ?? "{}") ?? [];
        var openDecision = new[] { DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred };

        var milestones = await MilestoneRows(db, id, today, 5);
        var dueThisWeek = sc.Tasks(("dueThisWeek", "true"));
        var blocked = sc.Tasks(("blocked", "true"));
        var activity = ActivityEndpoints.Filter(await ActivityEndpoints.Visible(db, access, id), null, null, null, null, null, disciplineId, importantOnly);
        var recent = await activity.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Take(15).ToListAsync();
        var disciplines = JsonSerializer.Deserialize<List<DashboardDiscipline>>(state?.DisciplineStates ?? "[]", JsonOpts.Web) ?? [];
        if (disciplineId is { } dsc) disciplines = disciplines.Where(x => x.DisciplineId == dsc).ToList();
        // Open deliverables due in the next 14 days, the Upcoming tile and each discipline's "Due ≤ 14 d" (AC-DASH-02, FR-RCV-09).
        Task<DashboardCount> Due14(Scope x) => Count(x.Deliverables(dueFrom: today, dueTo: today.AddDays(14), indicator: "open"),
            x.Link(("dueFrom", today.ToString("yyyy-MM-dd")), ("dueTo", today.AddDays(14).ToString("yyyy-MM-dd")), ("indicator", "open")));
        var due14 = new Dictionary<string, DashboardCount>();
        var rollups = s.IsFlagOn(RollupsFlag);
        var status = new Dictionary<string, DashboardStatusSegments>();
        // FR-BKL-02 and FR-BKL-07: the Backlog count and each discipline's, counted by the list query their links open.
        var backlogOn = s.IsFlagOn(TaskRules.BacklogFlag);
        var backlog = new Dictionary<string, DashboardCount>();
        foreach (var row in disciplines)
        {
            var dId = row.DisciplineId;
            var ds = new Scope(db, id, dId, me.Id, today, s);
            due14[dId.ToString()] = await Due14(ds);
            if (backlogOn) backlog[dId.ToString()] = await Count(ds.Tasks(("backlog", "true")), ds.Link(("backlog", "true")));
            // FR-RCV-14: each segment is counted by the deliverable list query its link opens.
            if (rollups)
                status[dId.ToString()] = new DashboardStatusSegments(await Segment(ds, NotStartedSegment), await Segment(ds, InProgressSegment), await Segment(ds, IssuedSegment), await Segment(ds, OnHoldSegment));
        }
        // Issues and risks are project-wide whatever the scope (AC-CNT-07), counted by the register's own query (FR-RCV-08).
        RegisterEndpoints.RegisterQuery Reg(string? severity) => new(null, severity, null, null, "open", null);

        return TypedResults.Ok(new DashboardView(
            milestones,
            new DashboardTasks(
                await Count(sc.Tasks(), sc.Link()),
                await Count(sc.Tasks(("status", TaskStatuses.Complete)), sc.Link(("status", TaskStatuses.Complete))),
                await Count(sc.Tasks(("status", TaskStatuses.InProgress)), sc.Link(("status", TaskStatuses.InProgress))),
                await Count(sc.Tasks(("readyForReview", "true")), sc.Link(("readyForReview", "true"))),
                await Count(sc.Tasks(("overdue", "true")), sc.Link(("overdue", "true"))),
                await Count(blocked, sc.Link(("blocked", "true"))),
                await Count(sc.Tasks(("waiting", "true")), sc.Link(("waiting", "true"))),
                await Count(sc.Tasks(("unassigned", "true")), sc.Link(("unassigned", "true"))),
                backlogOn ? await Count(sc.Tasks(("backlog", "true")), sc.Link(("backlog", "true"))) : null),
            new DashboardDeliverables(
                await Due14(sc),
                await Count(sc.Deliverables(indicator: "atRisk"), sc.Link(("indicator", "atRisk"))),
                await Count(sc.Deliverables(status: $"{DeliverableStatus.Issued},{DeliverableStatus.Accepted}"), sc.Link(("status", $"{DeliverableStatus.Issued},{DeliverableStatus.Accepted}"))),
                await Count(sc.Deliverables(), sc.Link())),
            new DashboardDecisions(
                new DashboardCount(await db.Decisions.CountAsync(d => d.ProjectId == id && openDecision.Contains(d.Status)), $"status={Uri.EscapeDataString(string.Join(",", openDecision))}"),
                new DashboardCount(await db.Decisions.CountAsync(d => d.ProjectId == id && openDecision.Contains(d.Status) && db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsOverdue)), "indicator=overdue")),
            new DashboardIssues(counts.GetValueOrDefault("issuesOpen"), counts.GetValueOrDefault("issuesHigh")),
            new DashboardRisks(counts.GetValueOrDefault("risksHigh")),
            new DashboardRegisters(
                await Count(RegisterEndpoints.IssueQuery(db, id, Reg(null), today), "indicator=open"),
                await Count(RegisterEndpoints.IssueQuery(db, id, Reg(Impact.High), today), $"indicator=open&severity={Impact.High}"),
                await Count(RegisterEndpoints.RiskQuery(db, id, Reg(Impact.High), today), $"indicator=open&severity={Impact.High}")),
            disciplines,
            due14,
            new DashboardTaskList(await TaskQueries.Rows(db, TaskQueries.Sort(dueThisWeek, null).Take(8), access.Actor), await dueThisWeek.CountAsync(), sc.Link(("dueThisWeek", "true"))),
            new DashboardTaskList(await TaskQueries.Rows(db, TaskQueries.Sort(blocked, null).Take(8), access.Actor), await blocked.CountAsync(), sc.Link(("blocked", "true"))),
            await ActivityEndpoints.Render(db, recent),
            state is null ? null : new DashboardState(state.EvaluatedAt),
            rollups ? status : null, // FR-DLV-08: each field exists only while its flag is on
            backlogOn ? backlog : null));
    }

    public sealed record MilestoneRow(Guid Id, string Key, string Name, string MilestoneType, DateOnly Date, DateOnly? OriginalDate, Guid? ProjectDisciplineId,
        int DaysRemaining, string? Status, int DeliverableTotal, int DeliverableIssued, int TaskTotal, int TaskComplete, int TaskOverdue, int TaskBlocked, JsonElement? Reasons);

    /// Next non-complete milestones with overdue ones first, then by date (§13.1 milestone strip, AC-DASH-03).
    static async Task<List<MilestoneRow>> MilestoneRows(HubDb db, Guid projectId, DateOnly today, int take) =>
        (await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId && !m.IsComplete && !m.IsCancelled && m.Date != null)
            .Select(m => new MilestoneWithState(m, db.MilestoneStates.FirstOrDefault(x => x.MilestoneId == m.Id))).ToListAsync())
        .OrderBy(x => x.Milestone.Date < today ? 0 : 1).ThenBy(x => x.Milestone.Date).Take(take)
        .Select(x => new MilestoneRow(x.Milestone.Id, x.Milestone.Key, x.Milestone.Name, x.Milestone.MilestoneType, x.Milestone.Date!.Value, x.Milestone.OriginalDate, x.Milestone.ProjectDisciplineId,
            x.Milestone.Date.Value.DayNumber - today.DayNumber, x.State?.Status, x.State?.DeliverableTotal ?? 0, x.State?.DeliverableIssued ?? 0, x.State?.TaskTotal ?? 0,
            x.State?.TaskComplete ?? 0, x.State?.TaskOverdue ?? 0, x.State?.TaskBlocked ?? 0, J.El(x.State?.StatusReasons))).ToList();

    // ---------- Weekly Coordination (§12.13) ----------

    static async Task<Ok<WeeklyCoordination>> Coordination(Guid id, Guid? disciplineId, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id, track: false);
        await eval.EnsureFresh(id);
        var s = await store.Get(db);
        var today = clock.Today(s);
        var now = clock.GetUtcNow();
        var sc = new Scope(db, id, disciplineId, me.Id, today, s);
        // FR-001: "this week" starts on the project's coordination day (Monday if unset).
        var start = today.AddDays(-(((int)today.DayOfWeek - (int)Weekday.Parse(p.CoordinationDay) + 7) % 7));
        var end = start.AddDays(6);
        var nextStart = end.AddDays(1);
        var nextEnd = nextStart.AddDays(6);
        var since = p.LastCoordinationReviewedAt ?? now.AddDays(-7);
        var sinceDate = Clock.LocalDate(since, s);
        string D(DateOnly d) => d.ToString("yyyy-MM-dd");

        var state = await db.ProjectStates.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id);
        var overdueTasks = sc.Tasks(("overdue", "true"));
        var blockedTasks = sc.Tasks(("blocked", "true"));
        var atRisk = sc.Deliverables(indicator: "atRisk");

        // Milestones approaching or overdue, with prerequisite completeness (3).
        var horizon = today.AddDays(s.MilestoneApproachingDays);
        var scopedMilestones = disciplineId is { } dsc
            ? db.Deliverables.Where(d => d.ProjectId == id && d.ProjectDisciplineId == dsc && d.MilestoneId != null).Select(d => d.MilestoneId!.Value) : null;
        var milestones = (await MilestoneRows(db, id, today, 50)).Where(m => m.Date <= horizon).ToList();
        if (scopedMilestones is not null)
        {
            var ids = await scopedMilestones.Distinct().ToListAsync();
            milestones = milestones.Where(m => ids.Contains(m.Id) || m.ProjectDisciplineId == disciplineId).ToList();
        }

        // Deliverables due this week or next, or overdue, not issued (4).
        var delDue = sc.Deliverables(indicator: "open").Where(d => (d.DueDate >= start && d.DueDate <= nextEnd) || db.DeliverableStates.Any(x => x.DeliverableId == d.Id && x.IsOverdue));

        // Decisions required: open and overdue, due soon, or blocking a task (5).
        var openDecision = new[] { DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred };
        var blockingLinks = db.ItemLinks.Where(l => l.ProjectId == id && l.SourceType == ItemType.Decision && l.Relation == ItemRelation.BlockedByDecision
            && db.Tasks.Any(t => t.Id == l.TargetId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && (disciplineId == null || t.ProjectDisciplineId == disciplineId)));
        var decisions = await db.Decisions.AsNoTracking().Where(d => d.ProjectId == id && openDecision.Contains(d.Status)
                && (blockingLinks.Any(l => l.SourceId == d.Id) || (disciplineId == null && db.DecisionStates.Any(x => x.DecisionId == d.Id && (x.IsOverdue || x.IsDueSoon)))))
            .OrderBy(d => d.RequiredByDate)
            .Select(d => new WeeklyDecision(d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, d.ImpactLevel, d.OwnerUserId,
                db.Users.Where(u => u.Id == d.OwnerUserId).Select(u => u.DisplayName).FirstOrDefault()
                    ?? db.ExternalParties.Where(x => x.Id == d.OwnerExternalPartyId).Select(x => x.Name).FirstOrDefault(),
                db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsOverdue).FirstOrDefault(),
                db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.DaysOverdue).FirstOrDefault(),
                blockingLinks.Count(l => l.SourceId == d.Id))).ToListAsync();

        // Discipline round: counts from the evaluated state plus the top three open items per discipline (9).
        var disciplineStates = JsonSerializer.Deserialize<List<DashboardDiscipline>>(state?.DisciplineStates ?? "[]", JsonOpts.Web) ?? [];
        if (disciplineId is { } only) disciplineStates = disciplineStates.Where(x => x.DisciplineId == only).ToList();
        var open = await sc.Tasks(("open", "true")).Where(t => t.Status != TaskStatuses.OnHold)
            .Select(t => new OpenTaskFacts(t.Id, t.ProjectDisciplineId, t.DueDate, db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue), db.TaskStates.Any(x => x.TaskId == t.Id && x.IsBlocked))).ToListAsync();
        var topIds = open.GroupBy(t => t.ProjectDisciplineId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Overdue).ThenByDescending(t => t.Blocked)
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue).Take(3).Select(t => t.Id).ToList());
        var allTop = topIds.Values.SelectMany(x => x).ToList();
        var topRows = (await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => allTop.Contains(t.Id)), access.Actor)).ToDictionary(r => r.Id);
        var top = topIds.ToDictionary(kv => kv.Key, kv => kv.Value.Select(x => topRows[x]).ToList());

        // Recently completed since the last review (11).
        var completedTasks = sc.Tasks(("status", TaskStatuses.Complete)).Where(t => t.CompletedAt >= since);
        var issuedDels = sc.Deliverables().Where(d => db.DeliverableIssues.Any(i => i.DeliverableId == d.Id && i.CreatedAt >= since));
        var decided = await db.Decisions.AsNoTracking().Where(d => d.ProjectId == id && d.Status == DecisionStatus.Decided && d.StatusChangedAt >= since && disciplineId == null)
            .Select(d => new WeeklyDecided(d.Id, d.Key, d.Subject, d.DecisionText, d.DecisionDate)).ToListAsync();

        var heldTasks = sc.Tasks(("status", TaskStatuses.OnHold));
        var heldDels = sc.Deliverables(status: DeliverableStatus.OnHold);
        // As in the issue register, a discipline sees the issues it leads or is affected by, each once (AC-LOC-01).
        var issues = await db.Issues.AsNoTracking().Where(i => i.ProjectId == id && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)
                && (disciplineId == null || i.ProjectDisciplineId == disciplineId || db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == disciplineId)))
            .OrderByDescending(i => i.Severity == Impact.High).ThenBy(i => i.TargetResolutionDate).Select(i => new WeeklyIssue(i.Id, i.Key, i.Title, i.Status, i.Severity, i.TargetResolutionDate)).ToListAsync();
        var high = RegisterEndpoints.Scores([Impact.High]); // RSK-01
        var risks = await db.Risks.AsNoTracking().Where(r => r.ProjectId == id && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring) && high.Contains(r.Probability * r.Impact)
            && (disciplineId == null || r.ProjectDisciplineId == disciplineId)).Select(r => new WeeklyRisk(r.Id, r.Key, r.Title, r.Status, r.Probability, r.Impact, r.ReviewDate)).ToListAsync();

        // MTG-02: actions owned by the client or another external party, waited on rather than sent.
        var waiting = disciplineId is null ? await MeetingEndpoints.Rows(db, db.Actions.AsNoTracking().Where(a => a.ProjectId == id && a.OwnerType == ActionOwnerType.ExternalParty
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)), today) : [];

        var reviewer = p.LastCoordinationReviewedBy is { } rb ? await db.Users.Where(u => u.Id == rb).Select(u => u.DisplayName).FirstOrDefaultAsync() : null;
        return TypedResults.Ok(new WeeklyCoordination(
            new WeeklyWindow(new WeekSpan(start, end), new WeekSpan(nextStart, nextEnd), since, p.LastCoordinationReviewedAt, reviewer, Permissions.RunCoordination(access.Actor, ctx).Ok),
            new WeeklyHeadline(state?.ComputedHealth, J.El(state?.HealthReasons), await Count(overdueTasks, sc.Link(("overdue", "true"))), await Count(blockedTasks, sc.Link(("blocked", "true"))),
                decisions.Count(d => d.IsOverdue), await Count(atRisk, sc.Link(("indicator", "atRisk"))), milestones.FirstOrDefault(m => m.Date >= today)),
            milestones,
            await DeliverableRows(db, delDue),
            decisions,
            await TaskQueries.Rows(db, blockedTasks, access.Actor),
            await TaskQueries.Rows(db, overdueTasks, access.Actor),
            await TaskQueries.Rows(db, sc.Tasks(("dueFrom", D(today)), ("dueTo", D(end)), ("open", "true")).Where(t => t.Status != TaskStatuses.OnHold), access.Actor),
            [.. disciplineStates.Select(x => new WeeklyRound(x, top.GetValueOrDefault(x.DisciplineId) ?? []))],
            issues, risks, waiting,
            new WeeklyCompleted(await TaskQueries.Rows(db, completedTasks, access.Actor), await DeliverableRows(db, issuedDels), decided),
            new WeeklyUpcoming(
                await TaskQueries.Rows(db, sc.Tasks(("dueFrom", D(nextStart)), ("dueTo", D(nextEnd)), ("open", "true")), access.Actor),
                await DeliverableRows(db, sc.Deliverables(dueFrom: end.AddDays(1), dueTo: end.AddDays(14), indicator: "open"))),
            new WeeklyHeld(await TaskQueries.Rows(db, heldTasks, access.Actor), await DeliverableRows(db, heldDels))));
    }

    // The deliverable rows are already DeliverableRow records; their helper still lists them as objects.
    static async Task<List<DeliverableEndpoints.DeliverableRow>> DeliverableRows(HubDb db, IQueryable<Deliverable> q) => [.. (await DeliverableEndpoints.Rows(db, q)).Cast<DeliverableEndpoints.DeliverableRow>()];

    sealed record MilestoneWithState(Milestone Milestone, MilestoneState? State);
    sealed record OpenTaskFacts(Guid Id, Guid ProjectDisciplineId, DateOnly? DueDate, bool Overdue, bool Blocked);
}
