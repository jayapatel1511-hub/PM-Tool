using Hub.Api.Data;
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

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/timeline", async (Guid id, Access access, HubDb db, SettingsStore store) =>
        {
            var (p, ctx) = await access.Project(id, track: false);
            return await Build(p, ctx, access, db, await store.Get(db));
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
            return new
            {
                t.Id, t.Key, t.Name, t.DeliverableId, t.ProjectDisciplineId, t.MilestoneId, t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate,
                t.Status, t.ProgressPct, t.Assignee, t.RowVersion, CreatedOn = Clock.LocalDate(t.CreatedAt, s), IsOverdue = t.Overdue,
                CanEditDates = canEdit, DueNeedsReason = canEdit && Permissions.DueChangeNeedsReason(a, ctx, facts),
            };
        }).ToList();
        var byId = tasks.ToDictionary(t => t.Id);
        var ids = byId.Keys.ToList();
        // §12.16: an arrow is highlighted while its predecessor is unfinished and overdue.
        var deps = (await db.Dependencies.AsNoTracking().Where(d => ids.Contains(d.PredecessorTaskId) && ids.Contains(d.SuccessorTaskId)).ToListAsync())
            .Select(d => new { d.Id, d.PredecessorTaskId, d.SuccessorTaskId, d.LagDays, Highlighted = byId[d.PredecessorTaskId].Overdue && !TaskStatuses.IsTerminal(byId[d.PredecessorTaskId].Status) }).ToList();
        var derived = deps.Where(d => byId[d.PredecessorTaskId].DeliverableId is { } a1 && byId[d.SuccessorTaskId].DeliverableId is { } b1 && a1 != b1)
            .GroupBy(d => (From: byId[d.PredecessorTaskId].DeliverableId!.Value, To: byId[d.SuccessorTaskId].DeliverableId!.Value))
            .Select(g => new { g.Key.From, g.Key.To, Highlighted = g.Any(x => x.Highlighted), Derived = true }).ToList();
        var explicitLinks = await db.DeliverableDependencies.AsNoTracking().Where(x => x.ProjectId == id)
            .Select(x => new { From = x.PredecessorDeliverableId, To = x.SuccessorDeliverableId,
                Highlighted = db.DeliverableStates.Any(s => s.DeliverableId == x.PredecessorDeliverableId && s.IsOverdue), Derived = false }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == id)
            .Select(d => new
            {
                d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.ReviewerId, d.StartDate, d.DueDate, d.OriginalStartDate, d.OriginalDueDate, d.Status, d.RowVersion, d.CreatedAt,
                State = db.DeliverableStates.Where(x => x.DeliverableId == d.Id).Select(x => new { x.ProgressPct, x.IsOverdue }).FirstOrDefault(),
            }).ToListAsync();
        var milestones = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsCancelled && m.Date != null)
            .Select(m => new { m.Id, m.Key, m.Name, m.Date, m.IsComplete, Status = m.IsComplete ? MilestoneStatus.Complete : db.MilestoneStates.Where(x => x.MilestoneId == m.Id).Select(x => x.Status).FirstOrDefault() })
            .OrderBy(m => m.Date).ToListAsync();
        return new
        {
            Permissions = new { ManageMilestones = Permissions.ManageMilestones(a, ctx).Ok, NeedsReason = p.Status == ProjectStatus.Complete },
            Tasks = taskRows,
            Dependencies = deps,
            DeliverableLinks = explicitLinks.Concat(derived).GroupBy(l => (l.From, l.To)).Select(g => new { g.Key.From, g.Key.To, Highlighted = g.Any(x => x.Highlighted), Derived = g.All(x => x.Derived) }),
            Deliverables = deliverables.Select(d => new
            {
                d.Id, d.Key, d.Name, d.StartDate, d.DueDate, d.OriginalStartDate, d.OriginalDueDate, d.Status, d.RowVersion, CreatedOn = Clock.LocalDate(d.CreatedAt, s),
                ProgressPct = d.State?.ProgressPct ?? 0, IsOverdue = d.State?.IsOverdue ?? false,
                CanEditDates = Permissions.EditDeliverable(a, ctx, new DeliverableFacts(d.ProjectDisciplineId, d.OwnerId, d.ReviewerId)).Ok,
            }),
            Milestones = milestones,
        };
    }
}
