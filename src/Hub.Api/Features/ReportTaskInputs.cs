using System.Data;
using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class ReportEndpoints
{
    public sealed record ReportTaskInputCount(int? Count, int KnownCount, string Href);
    public sealed record ReportTaskInputs(string Basis, string ScopeCoverage, int UnavailableReferences,
        ReportTaskInputCount Unestimated, ReportTaskInputCount OpenUndated,
        IReadOnlyDictionary<string, string> Parameters, DateTimeOffset AsOf);
    public sealed record ReportTaskInputPage(string Code, string Metric, string Basis, string ScopeCoverage, int UnavailableReferences,
        int? Total, int KnownTotal, int Page, int PageSize, IReadOnlyList<TaskQueries.Row> Items,
        IReadOnlyDictionary<string, string> Parameters, DateTimeOffset AsOf);

    static readonly string[] InputMetrics = ["unestimated", "open-undated"];
    const string InputBasis = "ReportTaskRows", InputCoverage = "CompleteForVisibleParameters";

    static bool HasTaskInputs(Def definition) => definition.TaskSource is not null || definition.Code is
        "workload-employee" or "workload-discipline" or "team-capacity" or "allocations-by-source" or "task-hours";

    static void ValidateTaskInputScope(Def definition, IQueryCollection query, OrgSettings settings, bool taskInputList)
    {
        var parameters = ReportParams(definition, settings);
        if (definition.TaskSource is null)
        {
            var known = parameters.Select(parameter => parameter.Key).Concat(["format", "catalogue", "view", "viewProject", "panel", "taskInput", "inputPage", "inputPageSize"])
                .Concat(taskInputList ? new[] { "page", "pageSize" } : Array.Empty<string>());
            foreach (var key in query.Keys) Check.OneOf(key, known.ToArray(), "parameter");
        }
        foreach (var parameter in parameters)
        {
            if (!query.ContainsKey(parameter.Key)) continue;
            var values = query[parameter.Key];
            Check.That(values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]), parameter.Key, "error.invalid");
            var raw = values[0]!;
            var valid = parameter.Key == "unitId" ? Guid.TryParse(raw, out _) : parameter.Type switch
            {
                "projects" => Http.List(raw) is { Length: > 0 } tokens && tokens.All(token => Guid.TryParse(token, out var id) && id != Guid.Empty),
                "project" or "person" or "discipline" or "office" => Guid.TryParse(raw, out var id) && id != Guid.Empty,
                "bool" => raw is "true" or "false",
                "number" => int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                "date" => DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                    && (parameter.Key != "week" || Workload.WeekOf(day).DayNumber <= DateOnly.MaxValue.DayNumber - 6),
                "select" => parameter.Options?.Contains(raw) == true,
                _ => true,
            };
            Check.That(valid, parameter.Key, "error.invalid");
        }
    }

    static IQueryable<WorkTask> DueThisWeekSource(Ctx c)
    {
        var from = Workload.WeekOf(c.Date("week") ?? c.Today);
        if (c.S.IsFlagOn("capacity_screens")) Check.That(from.DayNumber <= DateOnly.MaxValue.DayNumber - 6, "week", "error.invalid");
        return Tasks(c, new TaskFilter { DueFrom = from, DueTo = from.AddDays(6), Indicators = ["open"],
            AssigneeIds = c.Id("assigneeId") is { } id ? [id] : [], Mine = c.Bool("mine") ? true : null });
    }
    static IQueryable<WorkTask> OverdueSource(Ctx c)
    {
        var minimum = Math.Max(c.Int("minDaysOverdue") ?? 1, 1);
        return Tasks(c, new TaskFilter { Indicators = ["overdue"], AssigneeIds = c.Id("assigneeId") is { } id ? [id] : [] })
            .Where(task => c.Db.TaskStates.Any(state => state.TaskId == task.Id && state.DaysOverdue >= minimum));
    }
    static IQueryable<WorkTask> BlockedSource(Ctx c)
    {
        var query = Tasks(c, new TaskFilter { Indicators = ["blocked"] });
        if (c.Str("blockerType") is not { } kind) return query;
        // The same full-source predicate formerly inline in Blocked; never a post-cap filter.
        var matching = c.Db.Database.SqlQuery<Guid>($"""
            SELECT task_id AS "Value" FROM hub.task_state
            WHERE EXISTS (SELECT 1 FROM jsonb_array_elements(blocked_by) AS b
                WHERE b->>'type' = {kind} AND b->>'blocking' IS DISTINCT FROM 'false')
            """);
        return query.Where(task => matching.Contains(task.Id));
    }
    static IQueryable<WorkTask> BlockingSource(Ctx c) => Tasks(c, new TaskFilter { Indicators = ["blocking"] });
    static IQueryable<WorkTask> ReviewQueueSource(Ctx c) => Tasks(c, new TaskFilter { Indicators = ["readyForReview"], ReviewerIds = c.Id("reviewerId") is { } id ? [id] : [] });
    static IQueryable<WorkTask> StaleSource(Ctx c)
    {
        var days = Math.Max(c.Int("days") ?? c.S.TaskStaleDays, 1);
        if (c.S.IsFlagOn("capacity_screens")) Check.That(days <= (c.Now - DateTimeOffset.MinValue).TotalDays, "days", "error.invalid");
        var cutoff = c.Now.AddDays(-days);
        return Tasks(c, new TaskFilter { Indicators = ["open"] }).Where(task => task.Status != TaskStatuses.OnHold && task.LastActivityAt < cutoff);
    }
    static IQueryable<WorkTask> InputMetric(IQueryable<WorkTask> source, string metric) => metric == "unestimated"
        ? source.Where(task => task.EstimatedHours == null)
        : source.Where(task => task.DueDate == null && task.Status != TaskStatuses.Complete && task.Status != TaskStatuses.Cancelled);

    static string InputHref(string code, string metric, IReadOnlyDictionary<string, string> parameters)
    {
        var values = parameters.Select(pair => (pair.Key, pair.Value)).Concat(new[] { ("taskInput", metric), ("inputPage", "1"), ("inputPageSize", "50") });
        return $"/reports/{Uri.EscapeDataString(code)}?" + string.Join("&", values.Select(pair => $"{Uri.EscapeDataString(pair.Item1)}={Uri.EscapeDataString(pair.Item2)}"));
    }
    static async Task<ReportTaskInputs> TaskInputSummary(Def definition, Ctx context)
    {
        var input = InputContext(context); var source = input.Tasks; var parameters = SavedParameters(definition, context);
        var unestimated = await InputMetric(source, "unestimated").CountAsync();
        var undated = await InputMetric(source, "open-undated").CountAsync();
        return new(input.Basis, input.Coverage, input.UnavailableReferences,
            new(input.UnavailableReferences == 0 ? unestimated : null, unestimated, InputHref(definition.Code, "unestimated", parameters)),
            new(input.UnavailableReferences == 0 ? undated : null, undated, InputHref(definition.Code, "open-undated", parameters)), parameters, context.Now);
    }
    static void MapTaskInputs(RouteGroupBuilder api) => api.MapGet("/reports/{code}/task-inputs/{metric}", TaskInputList);

    static async Task<Ok<ReportTaskInputPage>> TaskInputList(string code, string metric, int? page, int? pageSize,
        HttpContext http, HubDb db, Access access, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var (definition, context) = await Prepare(code, http, db, access, me, store, clock, taskInputList: true);
        Http.RequireFlag(context.S, "capacity_screens");
        if (!HasTaskInputs(definition)) throw ApiException.NotFound();
        Check.OneOf(metric, InputMetrics, "metric");
        var known = ReportParams(definition, context.S).Select(p => p.Key).Concat(["page", "pageSize"]).ToArray();
        foreach (var key in http.Request.Query.Keys) Check.OneOf(key, known, "parameter");
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        await EnsureRemainingInputs(definition, context);
        var input = InputContext(context);
        var query = InputMetric(input.Tasks, metric); var total = await query.CountAsync();
        var (currentPage, size) = Http.Paging(page, pageSize);
        var ordered = query.OrderBy(task => db.Projects.Where(project => project.Id == task.ProjectId).Select(project => project.ProjectNumber).FirstOrDefault())
            .ThenBy(task => task.Seq).ThenBy(task => task.Id);
        var offset = (long)(currentPage - 1) * size;
        IReadOnlyList<TaskQueries.Row> rows = offset >= total ? []
            : await TaskQueries.Rows(db, ordered.Skip((int)offset).Take(size), access.Actor);
        return TypedResults.Ok(new ReportTaskInputPage(code, metric, input.Basis, input.Coverage, input.UnavailableReferences,
            input.UnavailableReferences == 0 ? total : null, total,
            currentPage, size, rows, SavedParameters(definition, context), context.Now));
    }
}
