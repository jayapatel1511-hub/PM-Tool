using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S32 own-week presentation only. Commands, actual totals and capacity inputs retain their canonical owners.
public static partial class TimeEndpoints
{
    public sealed record TimeWeekDay(DateOnly Date, decimal? NormalHours, decimal? AvailableHours, decimal? TimeAwayHours,
        decimal? HolidayHours, decimal? AdditionalHours, bool Provisional, bool Unavailable);
    public sealed record TimeWeekTask(Guid TaskId, string TaskKey, string TaskName, Guid ProjectId, string ProjectNumber, string ProjectName,
        bool CanCreate, bool Deleted, bool Assigned, bool PreviousWeek, Row[] Entries);
    public sealed record TimeWeekView(DateOnly From, DateOnly To, DateOnly Today, Guid PersonId, decimal? CapacityHours,
        TimeWeekDay[] Days, TimeWeekTask[] Tasks, DateTimeOffset AsOf);

    static async Task<Ok<TimeWeekView>> OwnWeek([AsParameters] TimeQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        Check.That(f.Scope != "all", "scope", "time.week_own");
        Check.That(f.UserId is null || f.UserId == me.Id, "userId", "time.week_own");
        var today = clock.Today(settings);
        var from = f.From ?? Workload.WeekOf(today);
        // Guard both the exact seven days and their preceding week before DateOnly arithmetic.
        Check.That(from.DayOfWeek == DayOfWeek.Monday && from.DayNumber >= 7 && from.DayNumber <= DateOnly.MaxValue.DayNumber - 6,
            "from", "time.week_range");
        var to = from.AddDays(6);
        Check.That(f.To is null || f.To == to, "to", "time.week_range");
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var own = f with { From = from, To = to, UserId = me.Id, Scope = null };
        var entries = await Visible(own, access, db, me);
        var previous = await Visible(own with { From = from.AddDays(-7), To = from.AddDays(-1) }, access, db, me);
        var previousIds = previous.Select(row => row.TaskId).ToHashSet();
        // Materialise the permitted project IDs before IgnoreQueryFilters on historical tasks, so it cannot remove
        // the projects' own soft-delete filters. No hidden project task/name can enter through actual-entry history.
        var projects = access.VisibleProjects().AsNoTracking();
        if (f.ProjectId is { } projectId) projects = projects.Where(project => project.Id == projectId);
        var allowed = await projects.ToDictionaryAsync(project => project.Id);
        var allowedIds = allowed.Keys.ToArray();
        var assigned = db.Tasks.AsNoTracking().Where(task => allowedIds.Contains(task.ProjectId) && task.AssigneeId == me.Id
            && task.Status != TaskStatuses.Complete && task.Status != TaskStatuses.Cancelled);
        if (f.TaskId is { } taskId) assigned = assigned.Where(task => task.Id == taskId);
        var assignedIds = (await assigned.Select(task => task.Id).ToArrayAsync()).ToHashSet();
        var ids = assignedIds.Concat(previousIds).Concat(entries.Select(row => row.TaskId)).Distinct().ToArray();
        var tasks = await db.Tasks.IgnoreQueryFilters().AsNoTracking().Where(task => allowedIds.Contains(task.ProjectId) && ids.Contains(task.Id))
            .OrderBy(task => task.Key).ThenBy(task => task.Id).ToArrayAsync();
        var contexts = new Dictionary<Guid, ProjectContext>();
        var rows = new List<TimeWeekTask>();
        foreach (var task in tasks)
        {
            var project = allowed[task.ProjectId];
            if (!contexts.TryGetValue(project.Id, out var context)) contexts[project.Id] = context = await access.Context(project);
            rows.Add(new(task.Id, task.Key, task.Name, project.Id, project.ProjectNumber, project.Name,
                task.DeletedAt is null && Permissions.EnterTime(access.Actor, context).Ok, task.DeletedAt is not null,
                assignedIds.Contains(task.Id), previousIds.Contains(task.Id),
                entries.Where(entry => entry.TaskId == task.Id).OrderBy(entry => entry.WorkDate).ThenBy(entry => entry.Id).ToArray()));
        }
        var capacity = await PersonCapacity.Load(db, [me.Id], from, to, settings);
        var days = Enumerable.Range(0, 7).Select(offset =>
        {
            var date = from.AddDays(offset);
            return capacity.TryGetValue((me.Id, date), out var day)
                ? new TimeWeekDay(date, day.Normal, day.Available, day.Leave, day.Holiday, day.Additional, day.Provisional, false)
                : new TimeWeekDay(date, null, null, null, null, null, false, true);
        }).ToArray();
        decimal? total = days.Any(day => day.Unavailable || day.Provisional) ? null : days.Sum(day => day.AvailableHours!.Value);
        return TypedResults.Ok(new TimeWeekView(from, to, today, me.Id, total, days,
            rows.OrderBy(row => row.ProjectNumber).ThenBy(row => row.TaskKey).ThenBy(row => row.TaskId).ToArray(), clock.GetUtcNow()));
    }
}
