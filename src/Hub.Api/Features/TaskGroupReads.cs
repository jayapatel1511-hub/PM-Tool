using System.Linq.Expressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// SQL counts and aggregates over the exact authorised task query; only page IDs and their group metrics are materialised.
public static class TaskGroupReads
{
    public sealed record TaskListGroup(string Key, string? ParentKey, string Dimension, Guid ProjectId,
        string ProjectNumber, string ProjectName, string? Value, string? Label, bool ReferenceAvailable,
        int RowCount, decimal EstimatedHours, int UnestimatedCount, int OverdueCount, int UnavailableStateCount,
        int? ProgressPct, string ProgressBasis, int ProgressTaskCount, DateOnly? NextDueDate, int UndatedOpenCount,
        int PageShown, IReadOnlyList<Guid> PageTaskIds);
    internal sealed record Grouped(int Total, IReadOnlyList<Guid> PageIds, IReadOnlyList<TaskListGroup> Groups);
    sealed class KeyRow
    {
        public WorkTask Task { get; init; } = null!;
        public Guid ProjectId { get; init; }
        public string ProjectNumber { get; init; } = "";
        public string ProjectName { get; init; } = "";
        public string? Value { get; init; }
        public string? Label { get; init; }
        public bool Available { get; init; }
        public int Order { get; init; }
        public DateOnly? Date { get; init; }
    }
    sealed class ReadRow { public KeyRow Key { get; init; } = null!; public bool? Overdue { get; init; } }
    sealed record PageFact(Guid Id, Guid ProjectId, string ProjectNumber, string ProjectName, string? Value, string? Label, bool Available);
    sealed record Aggregate(Guid ProjectId, string? Value, int Count, decimal Hours, int Unestimated, int Overdue, int UnknownState,
        int ProgressCount, int ProgressUnknown, decimal ProgressHours, int CompleteCount, decimal CompleteHours, DateOnly? NextDue, int Undated);

    public static string? Group(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "none") return null;
        Check.OneOf(value, ["discipline", "deliverable", "milestone", "assignee", "status", "due"], "group");
        return value;
    }

    // A narrow parameter substitution reuses the actual TaskQueries.Sort lambdas; there is no approximate second task sort.
    sealed class Replace(Expression from, Expression to) : ExpressionVisitor
    {
        public override Expression? Visit(Expression? node) => node == from ? to : base.Visit(node);
    }

    static IQueryable<KeyRow> Keys(HubDb db, IQueryable<WorkTask> query, string dimension, DateOnly today)
    {
        var weekEnd = today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7));
        Expression<Func<WorkTask, KeyRow>> template = task => new KeyRow { Task = task, ProjectId = task.ProjectId,
            ProjectNumber = db.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault() ?? "",
            ProjectName = db.Projects.Where(p => p.Id == task.ProjectId).Select(p => p.Name).FirstOrDefault() ?? "" };
        var parameter = template.Parameters[0]; var bindings = new List<MemberBinding>(((MemberInitExpression)template.Body).Bindings);
        void Bind<T>(string name, Expression<Func<WorkTask, T>> expression) => bindings.Add(Expression.Bind(typeof(KeyRow).GetProperty(name)!, new Replace(expression.Parameters[0], parameter).Visit(expression.Body)!));
        Expression<Func<WorkTask, string?>> value;
        Expression<Func<WorkTask, string?>> label = task => null;
        Expression<Func<WorkTask, bool>> available = task => true;
        Expression<Func<WorkTask, int>> order = task => 0;
        Expression<Func<WorkTask, DateOnly?>> date = task => null;
        switch (dimension)
        {
            case "discipline":
                value = task => task.ProjectDisciplineId.ToString();
                label = task => db.ProjectDisciplines.Where(d => d.Id == task.ProjectDisciplineId && d.ProjectId == task.ProjectId).Select(d => d.Discipline!.Name).FirstOrDefault();
                available = task => db.ProjectDisciplines.Any(d => d.Id == task.ProjectDisciplineId && d.ProjectId == task.ProjectId);
                order = task => db.ProjectDisciplines.Where(d => d.Id == task.ProjectDisciplineId && d.ProjectId == task.ProjectId).Select(d => (int?)d.SortOrder).FirstOrDefault() ?? int.MaxValue;
                break;
            case "deliverable":
                value = task => task.DeliverableId.HasValue ? task.DeliverableId.Value.ToString() : null;
                label = task => db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.Key + " " + d.Name).FirstOrDefault();
                available = task => task.DeliverableId == null || db.Deliverables.Any(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId);
                order = task => db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => (int?)d.SortOrder).FirstOrDefault() ?? int.MaxValue;
                break;
            case "milestone":
                value = task => task.MilestoneId.HasValue ? task.MilestoneId.Value.ToString() : task.DeliverableId == null ? null
                    : !db.Deliverables.Any(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId) ? "unavailable-deliverable:" + task.DeliverableId.Value.ToString()
                    : db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.MilestoneId.HasValue ? d.MilestoneId.Value.ToString() : null).FirstOrDefault();
                label = task => db.Milestones.Where(m => m.ProjectId == task.ProjectId && m.Id == (task.MilestoneId ?? db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.MilestoneId).FirstOrDefault())).Select(m => m.Key + " " + m.Name).FirstOrDefault();
                available = task => !(task.MilestoneId == null && task.DeliverableId != null && !db.Deliverables.Any(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId))
                    && ((task.MilestoneId ?? db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.MilestoneId).FirstOrDefault()) == null
                        || db.Milestones.Any(m => m.ProjectId == task.ProjectId && m.Id == (task.MilestoneId ?? db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.MilestoneId).FirstOrDefault())));
                date = task => db.Milestones.Where(m => m.ProjectId == task.ProjectId && m.Id == (task.MilestoneId ?? db.Deliverables.Where(d => d.Id == task.DeliverableId && d.ProjectId == task.ProjectId).Select(d => d.MilestoneId).FirstOrDefault())).Select(m => m.Date).FirstOrDefault();
                break;
            case "assignee":
                value = task => task.AssigneeId.HasValue ? task.AssigneeId.Value.ToString() : null;
                label = task => db.Users.Where(u => u.Id == task.AssigneeId).Select(u => u.DisplayName).FirstOrDefault();
                available = task => task.AssigneeId == null || db.Users.Any(u => u.Id == task.AssigneeId);
                break;
            case "status":
                value = task => task.Status; label = task => task.Status;
                order = task => task.Status == TaskStatuses.NotStarted ? 0 : task.Status == TaskStatuses.InProgress ? 1 : task.Status == TaskStatuses.ReadyForReview ? 2
                    : task.Status == TaskStatuses.InReview ? 3 : task.Status == TaskStatuses.RevisionRequired ? 4 : task.Status == TaskStatuses.OnHold ? 5 : task.Status == TaskStatuses.Complete ? 6 : 7;
                break;
            default:
                value = task => db.TaskStates.Any(s => s.TaskId == task.Id && s.IsOverdue) ? "overdue" : task.DueDate == null ? "noDate"
                    : task.DueDate < today && !db.TaskStates.Any(s => s.TaskId == task.Id) ? "unavailable" : task.DueDate == today ? "today"
                    : task.DueDate <= weekEnd ? "thisWeek" : task.DueDate <= weekEnd.AddDays(7) ? "nextWeek" : "later";
                order = task => db.TaskStates.Any(s => s.TaskId == task.Id && s.IsOverdue) ? 0 : task.DueDate == null ? 5
                    : task.DueDate < today && !db.TaskStates.Any(s => s.TaskId == task.Id) ? 6 : task.DueDate == today ? 1 : task.DueDate <= weekEnd ? 2 : task.DueDate <= weekEnd.AddDays(7) ? 3 : 4;
                break;
        }
        Bind(nameof(KeyRow.Value), value); Bind(nameof(KeyRow.Label), label); Bind(nameof(KeyRow.Available), available); Bind(nameof(KeyRow.Order), order); Bind(nameof(KeyRow.Date), date);
        var init = (MemberInitExpression)template.Body;
        return query.Select(Expression.Lambda<Func<WorkTask, KeyRow>>(Expression.MemberInit(init.NewExpression, bindings), parameter));
    }

    static IQueryable<ReadRow> Ordered(IQueryable<ReadRow> source, IQueryable<WorkTask> query, HubDb db, TaskFilter filter)
    {
        IQueryable<ReadRow> result = source.OrderBy(row => row.Key.ProjectNumber).ThenBy(row => row.Key.ProjectId)
            .ThenBy(row => row.Key.Order).ThenBy(row => row.Key.Date == null).ThenBy(row => row.Key.Date)
            .ThenBy(row => row.Key.Label).ThenBy(row => row.Key.Value);
        var expression = TaskQueries.Sort(query, filter.Sort, db, filter.WorkspaceId).Expression;
        var parts = new List<(string Name, LambdaExpression Key)>();
        while (expression is MethodCallExpression call && call.Method.DeclaringType == typeof(Queryable)
            && call.Method.Name is "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending")
        {
            parts.Add((call.Method.Name, (LambdaExpression)((UnaryExpression)call.Arguments[1]).Operand)); expression = call.Arguments[0];
        }
        foreach (var (name, key) in parts.AsEnumerable().Reverse())
        {
            var parameter = Expression.Parameter(typeof(ReadRow), "row");
            var task = Expression.Property(Expression.Property(parameter, nameof(ReadRow.Key)), nameof(KeyRow.Task));
            var rebound = Expression.Lambda(new Replace(key.Parameters[0], task).Visit(key.Body)!, parameter);
            result = result.Provider.CreateQuery<ReadRow>(Expression.Call(typeof(Queryable), name.EndsWith("Descending", StringComparison.Ordinal) ? "ThenByDescending" : "ThenBy",
                [typeof(ReadRow), key.ReturnType], result.Expression, Expression.Quote(rebound)));
        }
        return ((IOrderedQueryable<ReadRow>)result).ThenBy(row => row.Key.Task.Id);
    }

    static IQueryable<Aggregate> AggregateQuery(IQueryable<ReadRow> source, bool parent) => source.GroupBy(row => new GroupKey { ProjectId = row.Key.ProjectId, Value = parent ? null : row.Key.Value })
        .Select(group => new Aggregate(group.Key.ProjectId, group.Key.Value, group.Count(), group.Sum(row => row.Key.Task.EstimatedHours ?? 0m),
            group.Count(row => row.Key.Task.EstimatedHours == null), group.Count(row => row.Overdue == true), group.Count(row => row.Overdue == null),
            group.Count(row => row.Key.Task.Status != TaskStatuses.Cancelled), group.Count(row => row.Key.Task.Status != TaskStatuses.Cancelled && row.Key.Task.EstimatedHours == null),
            group.Sum(row => row.Key.Task.Status != TaskStatuses.Cancelled ? row.Key.Task.EstimatedHours ?? 0m : 0m), group.Count(row => row.Key.Task.Status == TaskStatuses.Complete),
            group.Sum(row => row.Key.Task.Status == TaskStatuses.Complete ? row.Key.Task.EstimatedHours ?? 0m : 0m),
            group.Min(row => row.Key.Task.Status != TaskStatuses.Complete && row.Key.Task.Status != TaskStatuses.Cancelled ? row.Key.Task.DueDate : null),
            group.Count(row => row.Key.Task.Status != TaskStatuses.Complete && row.Key.Task.Status != TaskStatuses.Cancelled && row.Key.Task.DueDate == null)));

    internal static async Task<Grouped> Read(HubDb db, IQueryable<WorkTask> query, TaskFilter filter, string dimension, DateOnly today)
    {
        var total = await query.CountAsync(); var offset = (long)(filter.Page - 1) * filter.PageSize;
        if (offset >= total) return new(total, [], []); // retain root's max-int boundary guard without narrowing the label
        var keys = Keys(db, query, dimension, today);
        var source = from key in keys join state in db.TaskStates.AsNoTracking() on key.Task.Id equals state.TaskId into states
            from state in states.DefaultIfEmpty() select new ReadRow { Key = key, Overdue = state == null ? null : state.IsOverdue };
        var page = await Ordered(source, query, db, filter).Skip((int)offset).Take(filter.PageSize)
            .Select(row => new PageFact(row.Key.Task.Id, row.Key.ProjectId, row.Key.ProjectNumber, row.Key.ProjectName, row.Key.Value, row.Key.Label, row.Key.Available)).ToListAsync();
        // Only visible keys enter this OR predicate. Each group's counts still run over every matching filtered row in SQL.
        var parameter = Expression.Parameter(typeof(ReadRow), "row"); var keyMember = Expression.Property(parameter, nameof(ReadRow.Key));
        Expression predicate = Expression.Constant(false);
        foreach (var key in page.DistinctBy(row => (row.ProjectId, row.Value))) predicate = Expression.OrElse(predicate,
            Expression.AndAlso(Expression.Equal(Expression.Property(keyMember, nameof(KeyRow.ProjectId)), Expression.Constant(key.ProjectId)),
                Expression.Equal(Expression.Property(keyMember, nameof(KeyRow.Value)), Expression.Constant(key.Value, typeof(string)))));
        var visible = source.Where(Expression.Lambda<Func<ReadRow, bool>>(predicate, parameter));
        var leaves = await AggregateQuery(visible, false).ToListAsync();
        var outer = await query.Select(task => task.ProjectId).Distinct().CountAsync() > 1;
        var pids = page.Select(row => row.ProjectId).Distinct().ToArray();
        var parents = outer ? await AggregateQuery(source.Where(row => pids.Contains(row.Key.ProjectId)), true).ToListAsync() : [];
        TaskListGroup GroupRow(string key, string? parent, string kind, PageFact reference, string? value, string? label, bool available, Aggregate metrics, List<Guid> ids)
        {
            var weighted = metrics.ProgressUnknown == 0; var denominator = weighted ? metrics.ProgressHours : metrics.ProgressCount;
            var numerator = weighted ? metrics.CompleteHours : metrics.CompleteCount;
            int? percentage = metrics.ProgressCount == 0 || denominator == 0 ? null : (int)(decimal.Floor(numerator / denominator * 20m) * 5m);
            return new(key, parent, kind, reference.ProjectId, reference.ProjectNumber, reference.ProjectName, value, label, available, metrics.Count,
                metrics.Hours, metrics.Unestimated, metrics.Overdue, metrics.UnknownState, percentage, metrics.ProgressCount == 0 ? "NoTasks" : weighted ? "Hours" : "Count",
                metrics.ProgressCount, metrics.NextDue, metrics.Undated, ids.Count, ids);
        }
        var groups = new List<TaskListGroup>();
        foreach (var project in page.GroupBy(row => row.ProjectId))
        {
            var parent = $"{project.Key}:project"; var reference = project.First();
            if (outer) groups.Add(GroupRow(parent, null, "project", reference, project.Key.ToString(), reference.ProjectName, true,
                parents.Single(row => row.ProjectId == project.Key), project.Select(row => row.Id).ToList()));
            foreach (var leaf in project.GroupBy(row => row.Value)) groups.Add(GroupRow($"{project.Key}:{dimension}:{leaf.Key ?? "none"}", outer ? parent : null, dimension,
                leaf.First(), leaf.Key, leaf.First().Label, leaf.First().Available, leaves.Single(row => row.ProjectId == project.Key && row.Value == leaf.Key), leaf.Select(row => row.Id).ToList()));
        }
        return new(total, page.Select(row => row.Id).ToList(), groups);
    }
}

// A GroupBy key must be a member-init expression for EF to translate it.
file sealed class GroupKey { public Guid ProjectId { get; init; } public string? Value { get; init; } }
