using Hub.Api.Data;
using System.Text.Json.Serialization;
using System.Text.Json;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Timeline data beyond the registers (§12.16, FR-VIEW-02, §36.4): tasks with their original dates, task dependencies,
/// derived and explicit deliverable dependencies with the "unfinished and overdue" highlight, and who may drag what.
/// Dragging saves through the normal item edits, so the same rules, reasons and logs apply; nothing else moves.
public static class TimelineEndpoints
{
    /// A cross-project Gantt draws at most this many projects; beyond it the view asks for a narrower scope.
    public const int MaxProjects = 25;

    public sealed record TimelineTask(Guid Id, string Key, string Name, Guid? DeliverableId, Guid ProjectDisciplineId, Guid? MilestoneId,
        DateOnly? StartDate, DateOnly? DueDate, DateOnly? OriginalStartDate, DateOnly? OriginalDueDate, string Status, int ProgressPct,
        string? Assignee, int RowVersion, DateOnly CreatedOn, bool IsOverdue, bool CanEditDates, bool DueNeedsReason,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SlipShown);
    public sealed record TimelineDeliverable(Guid Id, string Key, string Name, DateOnly? StartDate, DateOnly? DueDate,
        DateOnly? OriginalStartDate, DateOnly? OriginalDueDate, string Status, int RowVersion, DateOnly CreatedOn, int ProgressPct,
        bool IsOverdue, bool CanEditDates, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SlipShown,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? BaselineStartDate = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? BaselineDueDate = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BaselineSlipShown = null);
    public sealed record TimelineMilestone(Guid Id, string Key, string Name, DateOnly? Date, bool IsComplete, string? Status,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? OriginalDate,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SlipShown,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? BaselineDate = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BaselineSlipShown = null);
    public sealed record TimelineHandoff(Guid Id, string Key, string Title, string Status, Guid SendingDisciplineId, Guid ReceivingDisciplineId,
        DateOnly NeededBy, DateOnly? PromisedBy, bool DateMismatch, bool IsOverdue, int DaysOverdue);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/timeline", async (Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (p, ctx) = await access.Project(id, track: false);
            var settings = await store.Get(db);
            var result = await Build(p, ctx, access, db, settings);
            if (!settings.IsFlagOn("timeline_handoffs_chain")) return result;
            var today = clock.Today(settings);
            // Handoffs share the list's project visibility; the cross-project Gantt does not include this addition.
            var handoffs = await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == id && h.Status != HandoffStatus.Cancelled)
                .OrderBy(h => h.NeededBy).ThenBy(h => h.Seq).ToListAsync();
            var json = JsonSerializer.SerializeToNode(result, JsonOpts.Web)!.AsObject();
            // D-13 is already bounded by chain_depth_limit when evaluated; this read never traverses the graph.
            var affected = await db.TaskStates.AsNoTracking().Where(s => s.ProjectId == id)
                .ToDictionaryAsync(s => s.TaskId, s => s.AffectedMilestoneIds);
            foreach (var task in json["tasks"]!.AsArray())
                task!["affectedMilestoneIds"] = JsonSerializer.SerializeToNode(affected.GetValueOrDefault(task["id"]!.GetValue<Guid>()) ?? [], JsonOpts.Web);
            json["handoffs"] = JsonSerializer.SerializeToNode(handoffs.Select(h => new TimelineHandoff(h.Id, h.Key, h.Title, h.Status,
                h.SendingDisciplineId, h.ReceivingDisciplineId, h.NeededBy, h.PromisedBy, HandoffRules.DateMismatch(h.NeededBy, h.PromisedBy),
                HandoffRules.Overdue(h.Status, h.NeededBy, today, p.Status),
                HandoffRules.Overdue(h.Status, h.NeededBy, today, p.Status) ? today.DayNumber - h.NeededBy.DayNumber : 0)), JsonOpts.Web);
            return json;
        });
        // §36.4 FR-VIS-05: the selected permitted projects as groups; each project's data is built exactly as its own
        // timeline, so arrows never join two projects and dates reconcile with the project lists.
        api.MapGet("/timeline", async (string? projects, Access access, HubDb db, SettingsStore store) =>
        {
            var s = await store.Get(db);
            var list = await Scope.Projects(access, db, projects).AsNoTracking().OrderBy(p => p.ProjectNumber).Take(MaxProjects + 1).ToListAsync();
            var rows = new List<object>();
            // ponytail: one project at a time (five queries each), fine up to MaxProjects; batch per query if scopes grow.
            foreach (var p in list.Take(MaxProjects))
                rows.Add(new { Project = new { p.Id, p.ProjectNumber, p.Name, p.Status, p.StartDate, p.TargetCompletionDate }, Data = await Build(p, await access.Context(p), access, db, s) });
            return new { Projects = rows, Truncated = list.Count > MaxProjects, Max = MaxProjects };
        });
    }

    static async Task<object> Build(Project p, ProjectContext ctx, Access access, HubDb db, OrgSettings s)
    {
        var id = p.Id;
        var a = access.Actor;
        var schedule = s.IsFlagOn("schedule_baseline");
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == id && t.Status != TaskStatuses.Cancelled)
            .Select(t => new
            {
                t.Id, t.Key, t.Name, t.DeliverableId, t.ProjectDisciplineId, t.MilestoneId, t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate,
                t.Status, t.ProgressPct, t.AssigneeId, t.ReviewerId, t.CreatedBy, t.RowVersion, t.CreatedAt,
                Assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                Overdue = db.TaskStates.Any(s => s.TaskId == t.Id && s.IsOverdue),
            }).ToListAsync();
        var none = new HashSet<Guid>();
        var taskRows = tasks.Select(t =>
        {
            var facts = new TaskFacts(t.ProjectDisciplineId, t.AssigneeId, t.ReviewerId, t.CreatedBy, none, t.Status);
            var canEdit = Permissions.ChangeDueDate(a, ctx, facts).Ok && Permissions.EditTask(a, ctx, facts).Ok;
            return new TimelineTask(t.Id, t.Key, t.Name, t.DeliverableId, t.ProjectDisciplineId, t.MilestoneId, t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate,
                t.Status, t.ProgressPct, t.Assignee, t.RowVersion, Clock.LocalDate(t.CreatedAt, s), t.Overdue,
                canEdit, canEdit && Permissions.DueChangeNeedsReason(a, ctx, facts),
                schedule ? Evaluator.SlipShown(t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate, s.SlipDisplayDays) : null);
        }).ToList();
        var byId = tasks.ToDictionary(t => t.Id);
        var ids = byId.Keys.ToList();
        // §12.16: an arrow is highlighted while its predecessor is unfinished and overdue.
        var deps = (await db.Dependencies.AsNoTracking().Where(d => ids.Contains(d.PredecessorTaskId) && ids.Contains(d.SuccessorTaskId)).ToListAsync())
            .Select(d => new { d.Id, d.PredecessorTaskId, d.SuccessorTaskId, Type = d.DependencyType, d.LagDays, Highlighted = byId[d.PredecessorTaskId].Overdue && !TaskStatuses.IsTerminal(byId[d.PredecessorTaskId].Status) }).ToList();
        var derived = deps.Where(d => byId[d.PredecessorTaskId].DeliverableId is { } a1 && byId[d.SuccessorTaskId].DeliverableId is { } b1 && a1 != b1)
            .GroupBy(d => (From: byId[d.PredecessorTaskId].DeliverableId!.Value, To: byId[d.SuccessorTaskId].DeliverableId!.Value))
            .Select(g => new { g.Key.From, g.Key.To, Type = g.Select(x => x.Type).Distinct().Count() == 1 ? g.First().Type : DependencyTypes.FinishToStart, // mixed task links draw as Finish-to-Start
                LagDays = (int?)null, Highlighted = g.Any(x => x.Highlighted), Derived = true }).ToList(); // AC-DEPT-06: a derived link has no lag of its own
        var explicitLinks = await db.DeliverableDependencies.AsNoTracking().Where(x => x.ProjectId == id)
            .Select(x => new { From = x.PredecessorDeliverableId, To = x.SuccessorDeliverableId, Type = x.DependencyType, LagDays = (int?)x.LagDays,
                Highlighted = db.DeliverableStates.Any(s => s.DeliverableId == x.PredecessorDeliverableId && s.IsOverdue), Derived = false }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == id)
            .Select(d => new
            {
                d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.ReviewerId, d.StartDate, d.DueDate, d.OriginalStartDate, d.OriginalDueDate, d.BaselineStartDate, d.BaselineDueDate, d.Status, d.RowVersion, d.CreatedAt,
                State = db.DeliverableStates.Where(x => x.DeliverableId == d.Id).Select(x => new { x.ProgressPct, x.IsOverdue }).FirstOrDefault(),
            }).ToListAsync();
        // AC-TL-03: undated milestones come back too (Date null, last), so the views list them under Unscheduled.
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsCancelled)
            .Select(m => new { m.Id, m.Key, m.Name, m.Date, m.OriginalDate, m.BaselineDate, m.IsComplete, Status = m.IsComplete ? MilestoneStatus.Complete : db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault() })
            .OrderBy(m => m.Date == null).ThenBy(m => m.Date).ToListAsync();
        var result = new
        {
            Permissions = new { ManageMilestones = Permissions.ManageMilestones(a, ctx).Ok, NeedsReason = p.Status == ProjectStatus.Complete },
            Tasks = taskRows,
            Dependencies = deps,
            DeliverableLinks = explicitLinks.Concat(derived).GroupBy(l => (l.From, l.To)).Select(g => new { g.Key.From, g.Key.To,
                Type = (g.FirstOrDefault(x => !x.Derived) ?? g.First()).Type, LagDays = g.FirstOrDefault(x => !x.Derived)?.LagDays, Highlighted = g.Any(x => x.Highlighted), Derived = g.All(x => x.Derived) }),
            Deliverables = deliverables.Select(d => new TimelineDeliverable(d.Id, d.Key, d.Name, d.StartDate, d.DueDate, d.OriginalStartDate, d.OriginalDueDate, d.Status, d.RowVersion,
                Clock.LocalDate(d.CreatedAt, s), d.State?.ProgressPct ?? 0, d.State?.IsOverdue ?? false,
                Permissions.EditDeliverable(a, ctx, new DeliverableFacts(d.ProjectDisciplineId, d.OwnerId, d.ReviewerId)).Ok,
                schedule ? Evaluator.SlipShown(d.StartDate, d.DueDate, d.OriginalStartDate, d.OriginalDueDate, s.SlipDisplayDays) : null,
                schedule ? d.BaselineStartDate : null, schedule ? d.BaselineDueDate : null,
                schedule && (d.BaselineStartDate is not null || d.BaselineDueDate is not null) ? Evaluator.SlipShown(d.StartDate, d.DueDate, d.BaselineStartDate, d.BaselineDueDate, s.SlipDisplayDays) : null)),
            Milestones = milestones.Select(m => new TimelineMilestone(m.Id, m.Key, m.Name, m.Date, m.IsComplete, m.Status, schedule ? m.OriginalDate : null,
                schedule ? Evaluator.SlipShown(null, m.Date, null, m.OriginalDate, s.SlipDisplayDays) : null, schedule ? m.BaselineDate : null,
                schedule && m.BaselineDate is not null ? Evaluator.SlipShown(null, m.Date, null, m.BaselineDate, s.SlipDisplayDays) : null)),
        };
        var latest = schedule ? Evaluator.LatestPlanned(p.Status, p.TargetCompletionDate,
            milestones.Select(m => new DateItem(m.Id, m.Key, null, m.Date, m.IsComplete))
                .Concat(deliverables.Select(d => new DateItem(d.Id, d.Key, d.StartDate, d.DueDate, DeliverableStatus.IsTerminal(d.Status))))) : null;
        if (latest is null) return result;
        var json = JsonSerializer.SerializeToNode(result, JsonOpts.Web)!.AsObject();
        json["latestPlanned"] = JsonSerializer.SerializeToNode(latest, JsonOpts.Web);
        return json;
    }
}
