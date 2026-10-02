using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Project Dashboard (§13.1, FR-DASH-01) and Weekly Coordination (§12.13, §13.9, FR-WC-01/02). Every count is the row
/// count of the list its link opens, computed by the same list query (§12.17 "same code path").
public static class DashboardEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/dashboard", Dashboard);
        api.MapGet("/projects/{id:guid}/coordination", Coordination);
        api.MapPost("/projects/{id:guid}/coordination/reviewed", async (Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.RunCoordination(access.Actor, ctx)); // FR-004: the PM or a lead
            p.LastCoordinationReviewedAt = clock.GetUtcNow();
            p.LastCoordinationReviewedBy = me.Id;
            db.Audit.Note(p, action: "CoordinationReviewed");
            await db.SaveChangesAsync();
            return Results.Ok(new { p.LastCoordinationReviewedAt, p.RowVersion });
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

    static async Task<object> Count<T>(IQueryable<T> q, string link) => new { Value = await q.CountAsync(), Link = link };

    static async Task<object> Dashboard(Guid id, Guid? disciplineId, bool? importantOnly, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock, CurrentUser me)
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
        var disciplines = JsonSerializer.Deserialize<List<JsonElement>>(state?.DisciplineStates ?? "[]", JsonOpts.Web) ?? [];
        if (disciplineId is { } dsc) disciplines = disciplines.Where(x => x.GetProperty("disciplineId").GetGuid() == dsc).ToList();

        return new
        {
            Milestones = milestones,
            Tasks = new
            {
                Total = await Count(sc.Tasks(), sc.Link()),
                Complete = await Count(sc.Tasks(("status", TaskStatuses.Complete)), sc.Link(("status", TaskStatuses.Complete))),
                InProgress = await Count(sc.Tasks(("status", TaskStatuses.InProgress)), sc.Link(("status", TaskStatuses.InProgress))),
                Review = await Count(sc.Tasks(("readyForReview", "true")), sc.Link(("readyForReview", "true"))),
                Overdue = await Count(sc.Tasks(("overdue", "true")), sc.Link(("overdue", "true"))),
                Blocked = await Count(blocked, sc.Link(("blocked", "true"))),
                Waiting = await Count(sc.Tasks(("waiting", "true")), sc.Link(("waiting", "true"))),
                Unassigned = await Count(sc.Tasks(("unassigned", "true")), sc.Link(("unassigned", "true"))),
            },
            Deliverables = new
            {
                Upcoming = await Count(sc.Deliverables(dueFrom: today, dueTo: today.AddDays(14), indicator: "open"),
                    sc.Link(("dueFrom", today.ToString("yyyy-MM-dd")), ("dueTo", today.AddDays(14).ToString("yyyy-MM-dd")), ("indicator", "open"))),
                AtRisk = await Count(sc.Deliverables(indicator: "atRisk"), sc.Link(("indicator", "atRisk"))),
                Issued = await Count(sc.Deliverables(status: $"{DeliverableStatus.Issued},{DeliverableStatus.Accepted}"), sc.Link(("status", $"{DeliverableStatus.Issued},{DeliverableStatus.Accepted}"))),
                Total = await Count(sc.Deliverables(), sc.Link()),
            },
            Decisions = new
            {
                Pending = new { Value = await db.Decisions.CountAsync(d => d.ProjectId == id && openDecision.Contains(d.Status)), Link = $"status={Uri.EscapeDataString(string.Join(",", openDecision))}" },
                Overdue = new { Value = await db.Decisions.CountAsync(d => d.ProjectId == id && openDecision.Contains(d.Status) && db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsOverdue)), Link = "indicator=overdue" },
            },
            Issues = new { Open = counts.GetValueOrDefault("issuesOpen"), High = counts.GetValueOrDefault("issuesHigh") },
            Risks = new { High = counts.GetValueOrDefault("risksHigh") },
            Disciplines = disciplines,
            DueThisWeek = new { Items = await TaskQueries.Rows(db, TaskQueries.Sort(dueThisWeek, null).Take(8)), Total = await dueThisWeek.CountAsync(), Link = sc.Link(("dueThisWeek", "true")) },
            Blocked = new { Items = await TaskQueries.Rows(db, TaskQueries.Sort(blocked, null).Take(8)), Total = await blocked.CountAsync(), Link = sc.Link(("blocked", "true")) },
            Activity = await ActivityEndpoints.Render(db, recent),
            State = state is null ? null : new { state.EvaluatedAt },
        };
    }

    public sealed record MilestoneRow(Guid Id, string Key, string Name, string MilestoneType, DateOnly Date, DateOnly? OriginalDate, Guid? ProjectDisciplineId,
        int DaysRemaining, string? Status, int DeliverableTotal, int DeliverableIssued, int TaskTotal, int TaskComplete, int TaskOverdue, int TaskBlocked, JsonElement? Reasons);

    /// Next non-complete milestones with overdue ones first, then by date (§13.1 milestone strip, AC-DASH-03).
    static async Task<List<MilestoneRow>> MilestoneRows(HubDb db, Guid projectId, DateOnly today, int take) =>
        (await db.Milestones.AsNoTracking().Where(m => m.ProjectId == projectId && !m.IsComplete && !m.IsCancelled && m.Date != null)
            .Select(m => new { m, State = db.MilestoneStates.FirstOrDefault(x => x.MilestoneId == m.Id) }).ToListAsync())
        .OrderBy(x => x.m.Date < today ? 0 : 1).ThenBy(x => x.m.Date).Take(take)
        .Select(x => new MilestoneRow(x.m.Id, x.m.Key, x.m.Name, x.m.MilestoneType, x.m.Date!.Value, x.m.OriginalDate, x.m.ProjectDisciplineId,
            x.m.Date.Value.DayNumber - today.DayNumber, x.State?.Status, x.State?.DeliverableTotal ?? 0, x.State?.DeliverableIssued ?? 0, x.State?.TaskTotal ?? 0,
            x.State?.TaskComplete ?? 0, x.State?.TaskOverdue ?? 0, x.State?.TaskBlocked ?? 0, J.El(x.State?.StatusReasons))).ToList();

    // ---------- Weekly Coordination (§12.13) ----------

    static async Task<object> Coordination(Guid id, Guid? disciplineId, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock, CurrentUser me)
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
            .Select(d => new
            {
                d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, d.ImpactLevel, d.OwnerUserId,
                OwnerName = db.Users.Where(u => u.Id == d.OwnerUserId).Select(u => u.DisplayName).FirstOrDefault()
                    ?? db.ExternalParties.Where(x => x.Id == d.OwnerExternalPartyId).Select(x => x.Name).FirstOrDefault(),
                IsOverdue = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.IsOverdue).FirstOrDefault(),
                DaysOverdue = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => x.DaysOverdue).FirstOrDefault(),
                Blocking = blockingLinks.Count(l => l.SourceId == d.Id),
            }).ToListAsync();

        // Discipline round: counts from the evaluated state plus the top three open items per discipline (9).
        var disciplineStates = JsonSerializer.Deserialize<List<JsonElement>>(state?.DisciplineStates ?? "[]", JsonOpts.Web) ?? [];
        if (disciplineId is { } only) disciplineStates = disciplineStates.Where(x => x.GetProperty("disciplineId").GetGuid() == only).ToList();
        var open = await sc.Tasks(("open", "true")).Where(t => t.Status != TaskStatuses.OnHold).Select(t => new
        {
            t.Id, t.ProjectDisciplineId, t.DueDate,
            Overdue = db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue), Blocked = db.TaskStates.Any(x => x.TaskId == t.Id && x.IsBlocked),
        }).ToListAsync();
        var topIds = open.GroupBy(t => t.ProjectDisciplineId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.Overdue).ThenByDescending(t => t.Blocked)
            .ThenBy(t => t.DueDate ?? DateOnly.MaxValue).Take(3).Select(t => t.Id).ToList());
        var allTop = topIds.Values.SelectMany(x => x).ToList();
        var topRows = (await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(t => allTop.Contains(t.Id)))).ToDictionary(r => (Guid)((dynamic)r).Id);
        var top = topIds.ToDictionary(kv => kv.Key, kv => kv.Value.Select(x => topRows[x]).ToList());

        // Recently completed since the last review (11).
        var completedTasks = sc.Tasks(("status", TaskStatuses.Complete)).Where(t => t.CompletedAt >= since);
        var issuedDels = sc.Deliverables().Where(d => db.DeliverableIssues.Any(i => i.DeliverableId == d.Id && i.CreatedAt >= since));
        var decided = await db.Decisions.AsNoTracking().Where(d => d.ProjectId == id && d.Status == DecisionStatus.Decided && d.StatusChangedAt >= since && disciplineId == null)
            .Select(d => new { d.Id, d.Key, d.Subject, d.DecisionText, d.DecisionDate }).ToListAsync();

        var heldTasks = sc.Tasks(("status", TaskStatuses.OnHold));
        var heldDels = sc.Deliverables(status: DeliverableStatus.OnHold);
        // As in the issue register, a discipline sees the issues it leads or is affected by, each once (AC-LOC-01).
        var issues = await db.Issues.AsNoTracking().Where(i => i.ProjectId == id && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)
                && (disciplineId == null || i.ProjectDisciplineId == disciplineId || db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == disciplineId)))
            .OrderByDescending(i => i.Severity == Impact.High).ThenBy(i => i.TargetResolutionDate).Select(i => new { i.Id, i.Key, i.Title, i.Status, i.Severity, i.TargetResolutionDate }).ToListAsync();
        var high = RegisterEndpoints.Scores([Impact.High]); // RSK-01
        var risks = await db.Risks.AsNoTracking().Where(r => r.ProjectId == id && (r.Status == RiskStatus.Open || r.Status == RiskStatus.Monitoring) && high.Contains(r.Probability * r.Impact)
            && (disciplineId == null || r.ProjectDisciplineId == disciplineId)).Select(r => new { r.Id, r.Key, r.Title, r.Status, r.Probability, r.Impact, r.ReviewDate }).ToListAsync();

        // MTG-02: actions owned by the client or another external party, waited on rather than sent.
        var waiting = disciplineId is null ? await MeetingEndpoints.Rows(db, db.Actions.AsNoTracking().Where(a => a.ProjectId == id && a.OwnerType == ActionOwnerType.ExternalParty
            && (a.Status == ActionStatus.Open || a.Status == ActionStatus.InProgress)), today) : [];

        var reviewer = p.LastCoordinationReviewedBy is { } rb ? await db.Users.Where(u => u.Id == rb).Select(u => u.DisplayName).FirstOrDefaultAsync() : null;
        return new
        {
            Window = new { ThisWeek = new { From = start, To = end }, NextWeek = new { From = nextStart, To = nextEnd }, Since = since, p.LastCoordinationReviewedAt, ReviewedBy = reviewer,
                CanMarkReviewed = Permissions.RunCoordination(access.Actor, ctx).Ok },
            Headline = new
            {
                ComputedHealth = state?.ComputedHealth, HealthReasons = J.El(state?.HealthReasons),
                OverdueTasks = await Count(overdueTasks, sc.Link(("overdue", "true"))), BlockedTasks = await Count(blockedTasks, sc.Link(("blocked", "true"))),
                DecisionsOverdue = decisions.Count(d => d.IsOverdue), DeliverablesAtRisk = await Count(atRisk, sc.Link(("indicator", "atRisk"))),
                NextMilestone = milestones.FirstOrDefault(m => m.Date >= today),
            },
            Milestones = milestones,
            Deliverables = await DeliverableEndpoints.Rows(db, delDue),
            Decisions = decisions,
            Blocked = await TaskQueries.Rows(db, blockedTasks),
            Overdue = await TaskQueries.Rows(db, overdueTasks),
            DueThisWeek = await TaskQueries.Rows(db, sc.Tasks(("dueFrom", D(today)), ("dueTo", D(end)), ("open", "true")).Where(t => t.Status != TaskStatuses.OnHold)),
            Disciplines = disciplineStates.Select(x => new { Discipline = x, Top = top.GetValueOrDefault(x.GetProperty("disciplineId").GetGuid()) ?? [] }),
            Issues = issues, Risks = risks, Waiting = waiting,
            Completed = new { Tasks = await TaskQueries.Rows(db, completedTasks), Deliverables = await DeliverableEndpoints.Rows(db, issuedDels), Decisions = decided },
            Upcoming = new
            {
                Tasks = await TaskQueries.Rows(db, sc.Tasks(("dueFrom", D(nextStart)), ("dueTo", D(nextEnd)), ("open", "true"))),
                Deliverables = await DeliverableEndpoints.Rows(db, sc.Deliverables(dueFrom: end.AddDays(1), dueTo: end.AddDays(14), indicator: "open")),
            },
            Held = new { Tasks = await TaskQueries.Rows(db, heldTasks), Deliverables = await DeliverableEndpoints.Rows(db, heldDels) },
        };
    }
}
