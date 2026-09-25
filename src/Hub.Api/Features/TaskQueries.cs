using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// The one task list query behind every task list, board, count and report, so a dashboard number always
/// equals the rows of the list it opens (§12.17, §13.1). Filters map one-to-one to query parameters (§18.3).
public sealed class TaskFilter
{
    public string[] Status = [], Priority = [], Indicators = [];
    public Guid[] AssigneeIds = [], ReviewerIds = [], DisciplineIds = [], DeliverableIds = [], MilestoneIds = [], ProjectIds = [], CreatedBy = [], Ids = [];
    public DateOnly? DueFrom, DueTo, StartFrom, StartTo, CompletedFrom, CompletedTo, DoneSince;
    /// A named workspace whose personal manual order a board sorted by "board" uses (§36.3); checked by the endpoint.
    public Guid? WorkspaceId;
    public bool? RequiresReview, HasDependencies, Mine, IncludeCancelled;
    public bool Unassigned, NoDeliverable;
    public string? Q, Sort;
    public int Page = 1, PageSize = 200;

    /// A filter from query-string style values, for counts that must equal the list a link opens (§13.1).
    public static TaskFilter Of(IDictionary<string, string?> values) =>
        From(new QueryCollection(values.Where(v => v.Value is not null).ToDictionary(v => v.Key, v => new Microsoft.Extensions.Primitives.StringValues(v.Value))));

    public static TaskFilter From(IQueryCollection q)
    {
        string[] L(string k) => Http.List(q[k]);
        Guid[] I(string k) => Http.Ids(q[k]);
        DateOnly? D(string k) => DateOnly.TryParse(q[k], System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        bool? B(string k) => bool.TryParse(q[k], out var b) ? b : null;
        var f = new TaskFilter
        {
            Status = L("status"), Priority = L("priority"), Indicators = [.. L("indicator"), .. new[] { "overdue", "blocked", "blocking", "waiting", "unassigned", "stale", "dueThisWeek", "readyForReview", "dateInconsistent", "noDueDate", "dueSoon", "heldPastDue", "open" }.Where(k => B(k) == true)],
            AssigneeIds = I("assigneeId"), ReviewerIds = I("reviewerId"), DisciplineIds = I("disciplineId"), DeliverableIds = I("deliverableId"),
            MilestoneIds = I("milestoneId"), ProjectIds = I("projectId"), CreatedBy = I("createdBy"), Ids = I("ids"),
            DueFrom = D("dueFrom"), DueTo = D("dueTo"), StartFrom = D("startFrom"), StartTo = D("startTo"), CompletedFrom = D("completedFrom"), CompletedTo = D("completedTo"), DoneSince = D("doneSince"),
            WorkspaceId = Guid.TryParse(q["workspaceId"], out var ws) ? ws : null,
            RequiresReview = B("requiresReview"), HasDependencies = B("hasDependencies"), Mine = B("mine"), IncludeCancelled = B("includeCancelled"),
            Q = q["q"], Sort = q["sort"], NoDeliverable = B("noDeliverable") == true,
        };
        f.Unassigned = f.Indicators.Contains("unassigned");
        var (page, size) = Http.Paging(int.TryParse(q["page"], out var p) ? p : null, int.TryParse(q["pageSize"], out var s) ? s : 200);
        f.Page = page; f.PageSize = size;
        return f;
    }
}

public static class TaskQueries
{
    public static IQueryable<WorkTask> Apply(HubDb db, IQueryable<WorkTask> q, TaskFilter f, Guid me, DateOnly today, OrgSettings s)
    {
        if (f.Ids.Length > 0) q = q.Where(t => f.Ids.Contains(t.Id));
        if (f.ProjectIds.Length > 0) q = q.Where(t => f.ProjectIds.Contains(t.ProjectId));
        if (f.Status.Length > 0) q = q.Where(t => f.Status.Contains(t.Status));
        else if (f.IncludeCancelled != true) q = q.Where(t => t.Status != TaskStatuses.Cancelled);
        if (f.Priority.Length > 0) q = q.Where(t => f.Priority.Contains(t.Priority));
        if (f.AssigneeIds.Length > 0) q = q.Where(t => t.AssigneeId != null && f.AssigneeIds.Contains(t.AssigneeId.Value));
        if (f.ReviewerIds.Length > 0) q = q.Where(t => t.ReviewerId != null && f.ReviewerIds.Contains(t.ReviewerId.Value));
        if (f.DisciplineIds.Length > 0) q = q.Where(t => f.DisciplineIds.Contains(t.ProjectDisciplineId));
        if (f.DeliverableIds.Length > 0) q = q.Where(t => t.DeliverableId != null && f.DeliverableIds.Contains(t.DeliverableId.Value));
        if (f.NoDeliverable) q = q.Where(t => t.DeliverableId == null);
        if (f.MilestoneIds.Length > 0) q = q.Where(t => (t.MilestoneId != null && f.MilestoneIds.Contains(t.MilestoneId.Value))
            || db.Deliverables.Any(d => d.Id == t.DeliverableId && d.MilestoneId != null && f.MilestoneIds.Contains(d.MilestoneId.Value)));
        if (f.CreatedBy.Length > 0) q = q.Where(t => t.CreatedBy != null && f.CreatedBy.Contains(t.CreatedBy.Value));
        if (f.DueFrom is { } df) q = q.Where(t => t.DueDate >= df);
        if (f.DueTo is { } dt) q = q.Where(t => t.DueDate <= dt);
        if (f.StartFrom is { } sf) q = q.Where(t => t.StartDate >= sf);
        if (f.StartTo is { } st) q = q.Where(t => t.StartDate <= st);
        if (f.CompletedFrom is { } cf) { var ts = new DateTimeOffset(cf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); q = q.Where(t => t.CompletedAt >= ts); }
        if (f.CompletedTo is { } ct) { var ts = new DateTimeOffset(ct.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); q = q.Where(t => t.CompletedAt < ts); }
        // Board Done lane (§13.4, FR-020): open tasks plus those completed since the date.
        if (f.DoneSince is { } ds) { var ts = new DateTimeOffset(ds.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero); q = q.Where(t => t.Status != TaskStatuses.Complete || t.CompletedAt >= ts); }
        if (f.RequiresReview is { } rr) q = q.Where(t => t.RequiresReview == rr);
        if (f.HasDependencies is { } hd) q = q.Where(t => db.Dependencies.Any(d => d.PredecessorTaskId == t.Id || d.SuccessorTaskId == t.Id) == hd);
        // Assignee or collaborator as two index-friendly halves (an OR across them scans every task in scope).
        if (f.Mine == true) q = q.Where(t => t.AssigneeId == me).Concat(q.Where(t => t.AssigneeId != me && db.Collaborators.Any(c => c.TaskId == t.Id && c.UserId == me)));
        if (!string.IsNullOrWhiteSpace(f.Q)) { var term = $"%{f.Q.Trim()}%"; q = q.Where(t => EF.Functions.ILike(t.Name, term) || EF.Functions.ILike(t.Key, term)); }
        var weekEnd = today.AddDays(6 - ((int)today.DayOfWeek + 6) % 7);
        foreach (var ind in f.Indicators)
            q = ind switch
            {
                "overdue" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsOverdue)),
                "blocked" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsBlocked)),
                "blocking" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsBlocking)),
                "waiting" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsWaiting)),
                "stale" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsStale)),
                "dueSoon" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsDueSoon)),
                "dateInconsistent" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsDateInconsistent)),
                "noDueDate" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsMissingDueDate)),
                "heldPastDue" => q.Where(t => db.TaskStates.Any(x => x.TaskId == t.Id && x.IsHeldPastDue)),
                "unassigned" => q.Where(t => t.AssigneeId == null && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                "readyForReview" => q.Where(t => t.Status == TaskStatuses.ReadyForReview || t.Status == TaskStatuses.InReview),
                "dueThisWeek" => q.Where(t => t.DueDate >= today && t.DueDate <= weekEnd && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                "open" => q.Where(t => t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                _ => q,
            };
        return q;
    }

    public static IQueryable<WorkTask> Sort(IQueryable<WorkTask> q, string? sort, HubDb? db = null, Guid? workspaceId = null)
    {
        var (field, desc) = (sort ?? "").Split(':') is [var a, var b] ? (a, b == "desc") : (sort ?? "", false);
        // §13.4 manual board order: the team's arranged positions first, the rest by due date (packet 019); on a named
        // workspace's board, that person's own arrangement (§36.3).
        if (field == "board" && db is not null)
            return (workspaceId is { } ws
                    ? q.OrderBy(t => db.BoardOrders.Where(o => o.WorkspaceId == ws && o.TaskId == t.Id).Select(o => (double?)o.Position).FirstOrDefault() ?? double.MaxValue)
                    : q.OrderBy(t => db.BoardOrders.Where(o => o.ProjectId == t.ProjectId && o.TaskId == t.Id).Select(o => (double?)o.Position).FirstOrDefault() ?? double.MaxValue))
                .ThenBy(t => t.DueDate == null).ThenBy(t => t.DueDate).ThenBy(t => t.Seq);
        return field switch
        {
            "key" => desc ? q.OrderByDescending(t => t.Seq) : q.OrderBy(t => t.Seq),
            "name" => desc ? q.OrderByDescending(t => t.Name) : q.OrderBy(t => t.Name),
            "startDate" => desc ? q.OrderByDescending(t => t.StartDate) : q.OrderBy(t => t.StartDate == null).ThenBy(t => t.StartDate),
            "priority" => desc ? q.OrderByDescending(t => t.Priority == Priority.Critical ? 0 : t.Priority == Priority.High ? 1 : t.Priority == Priority.Medium ? 2 : 3)
                : q.OrderBy(t => t.Priority == Priority.Critical ? 0 : t.Priority == Priority.High ? 1 : t.Priority == Priority.Medium ? 2 : 3).ThenBy(t => t.DueDate),
            "status" => desc ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            "progress" => desc ? q.OrderByDescending(t => t.ProgressPct) : q.OrderBy(t => t.ProgressPct),
            "lastActivity" => desc ? q.OrderByDescending(t => t.LastActivityAt) : q.OrderBy(t => t.LastActivityAt),
            "created" => desc ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt),
            "completed" => desc ? q.OrderByDescending(t => t.CompletedAt) : q.OrderBy(t => t.CompletedAt),
            "estimate" => desc ? q.OrderByDescending(t => t.EstimatedHours) : q.OrderBy(t => t.EstimatedHours),
            "manual" => q.OrderBy(t => t.SortOrder).ThenBy(t => t.Seq),
            "dueDate" when desc => q.OrderByDescending(t => t.DueDate).ThenBy(t => t.Seq),
            // Default: due date ascending with empty dates last, then priority, then key (§13.3).
            _ => q.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate)
                .ThenBy(t => t.Priority == Priority.Critical ? 0 : t.Priority == Priority.High ? 1 : t.Priority == Priority.Medium ? 2 : 3).ThenBy(t => t.Seq),
        };
    }

    public static async Task<List<object>> Rows(HubDb db, IQueryable<WorkTask> q)
    {
        // Scalar columns and counts in one query; related rows by id in a few keyed ones. (Multi-column First() inside the
        // projection made EF rank whole tables — every task state, project and person — per list: packet 011 scale run.)
        var rows = await q.Select(t => new
        {
            t.Id, t.ProjectId, t.Key, t.Name, t.ProjectDisciplineId, t.DeliverableId, t.MilestoneId, t.AssigneeId, t.ReviewerId, t.RequiresReview, t.Priority,
            t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate, t.Status, t.ProgressPct, t.EstimatedHours, t.ReviewRound, t.DueDateChangeCount, t.LastActivityAt,
            t.StatusChangedAt, t.CompletedAt, t.CreatedAt, t.CreatedBy, t.SortOrder, t.RowVersion, t.ManualBlockType, t.ManualBlockReason, t.ManualBlockSetAt,
            Comments = db.Comments.Count(c => c.ItemType == ItemType.Task && c.ItemId == t.Id && c.DeletedAt == null),
            Predecessors = db.Dependencies.Count(d => d.SuccessorTaskId == t.Id),
            Successors = db.Dependencies.Count(d => d.PredecessorTaskId == t.Id),
        }).ToListAsync();
        var ids = rows.Select(t => t.Id).ToList();
        var pids = rows.Select(t => t.ProjectId).Distinct().ToList();
        var pdIds = rows.Select(t => t.ProjectDisciplineId).Distinct().ToList();
        var dIds = rows.Select(t => t.DeliverableId).OfType<Guid>().Distinct().ToList();
        var uIds = rows.SelectMany(t => new[] { t.AssigneeId, t.ReviewerId, t.CreatedBy }).OfType<Guid>().Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.ProjectNumber, p.Name, p.Status }).ToDictionaryAsync(p => p.Id);
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(x => pdIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Discipline!.Name, x.Discipline.Code, x.Discipline.Colour, x.SortOrder }).ToDictionaryAsync(x => x.Id);
        var dels = await db.Deliverables.AsNoTracking().Where(d => dIds.Contains(d.Id)).Select(d => new { d.Id, d.Key, d.Name, d.DueDate, d.Status, d.MilestoneId, d.SortOrder }).ToDictionaryAsync(d => d.Id);
        var users = await db.Users.AsNoTracking().Where(u => uIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName, u.IsActive }).ToDictionaryAsync(u => u.Id);
        var collaborators = (await db.Collaborators.AsNoTracking().Where(c => ids.Contains(c.TaskId)).Select(c => new { c.TaskId, c.UserId }).ToListAsync())
            .ToLookup(c => c.TaskId, c => c.UserId);
        var states = await db.TaskStates.AsNoTracking().Where(x => ids.Contains(x.TaskId)).ToDictionaryAsync(x => x.TaskId);
        var list = rows.Select(t => new
        {
            t.Id, t.ProjectId, t.Key, t.Name, t.ProjectDisciplineId, t.DeliverableId, t.MilestoneId, t.AssigneeId, t.ReviewerId, t.RequiresReview, t.Priority,
            t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate, t.Status, t.ProgressPct, t.EstimatedHours, t.ReviewRound, t.DueDateChangeCount, t.LastActivityAt,
            t.StatusChangedAt, t.CompletedAt, t.CreatedAt, t.CreatedBy, t.SortOrder, t.RowVersion, t.ManualBlockType, t.ManualBlockReason, t.ManualBlockSetAt,
            Project = projects[t.ProjectId], Discipline = disciplines[t.ProjectDisciplineId],
            Deliverable = t.DeliverableId is { } did ? dels.GetValueOrDefault(did) : null,
            Assignee = t.AssigneeId is { } a ? users.GetValueOrDefault(a) : null,
            Reviewer = t.ReviewerId is { } r ? users.GetValueOrDefault(r) : null,
            CreatedByName = t.CreatedBy is { } c ? users.GetValueOrDefault(c)?.DisplayName : null,
            t.Comments, t.Predecessors, t.Successors, Collaborators = collaborators[t.Id].ToList(), State = states.GetValueOrDefault(t.Id),
        }).ToList();
        var msIds = list.Select(t => t.MilestoneId ?? t.Deliverable?.MilestoneId).OfType<Guid>().Distinct().ToList();
        var ms = await db.Milestones.AsNoTracking().Where(m => msIds.Contains(m.Id)).Select(m => new { m.Id, m.Key, m.Name, m.Date }).ToDictionaryAsync(m => m.Id);
        return list.Select(t =>
        {
            var mid = t.MilestoneId ?? t.Deliverable?.MilestoneId;
            var m = mid is { } x && ms.TryGetValue(x, out var mm) ? mm : null;
            return (object)new
            {
                t.Id, t.ProjectId, ProjectNumber = t.Project.ProjectNumber, ProjectName = t.Project.Name, ProjectStatus = t.Project.Status, t.Key, t.Name,
                t.ProjectDisciplineId, DisciplineName = t.Discipline.Name, DisciplineCode = t.Discipline.Code, DisciplineColour = t.Discipline.Colour, DisciplineOrder = t.Discipline.SortOrder,
                t.DeliverableId, DeliverableKey = t.Deliverable?.Key, DeliverableName = t.Deliverable?.Name, DeliverableDueDate = t.Deliverable?.DueDate,
                DeliverableStatus = t.Deliverable?.Status, DeliverableOrder = t.Deliverable?.SortOrder,
                t.MilestoneId, EffectiveMilestoneId = mid, MilestoneKey = m?.Key, MilestoneName = m?.Name, MilestoneDate = m?.Date, MilestoneDerived = t.MilestoneId is null && mid is not null,
                t.AssigneeId, AssigneeName = t.Assignee?.DisplayName, AssigneeActive = t.Assignee?.IsActive ?? true,
                t.ReviewerId, ReviewerName = t.Reviewer?.DisplayName, t.RequiresReview, t.Priority, t.StartDate, t.DueDate, t.OriginalStartDate, t.OriginalDueDate,
                t.Status, Lane = Workflow.Lane(t.Status), t.ProgressPct, t.EstimatedHours, t.ReviewRound, t.DueDateChangeCount, t.LastActivityAt, t.StatusChangedAt, t.CompletedAt,
                t.CreatedAt, t.CreatedBy, t.CreatedByName, t.SortOrder, t.RowVersion, t.ManualBlockType, t.ManualBlockReason, t.ManualBlockSetAt,
                CommentCount = t.Comments, PredecessorCount = t.Predecessors, SuccessorCount = t.Successors, CollaboratorIds = t.Collaborators,
                State = t.State is null ? null : new
                {
                    t.State.IsOverdue, t.State.DaysOverdue, t.State.IsDueSoon, t.State.IsWaiting, t.State.IsBlocked, t.State.BlockedSince, t.State.DaysBlocked,
                    BlockedBy = J.El(t.State.BlockedBy), t.State.IsBlocking, t.State.BlockingCount, t.State.BlockingTaskIds, t.State.IsStale, t.State.StaleDays,
                    t.State.IsUnassigned, t.State.IsMissingDueDate, t.State.IsDateInconsistent, InconsistencyDetail = J.El(t.State.InconsistencyDetail),
                    t.State.IsInactiveOwner, t.State.IsHeldPastDue, t.State.IsReviewStalled, t.State.AffectedMilestoneIds, t.State.Notes, t.State.EvaluatedAt,
                },
            };
        }).ToList();
    }

    public static async Task<Page<object>> Page(HubDb db, IQueryable<WorkTask> q, TaskFilter f)
    {
        var total = await q.CountAsync();
        var rows = await Rows(db, Sort(q, f.Sort, db, f.WorkspaceId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize));
        return new Page<object>(rows, f.Page, f.PageSize, total, f.Sort);
    }
}
