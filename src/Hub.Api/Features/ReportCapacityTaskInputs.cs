using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class ReportEndpoints
{
    sealed record TaskInputContext(IQueryable<WorkTask> Tasks, string Basis, string Coverage, int UnavailableReferences = 0);
    sealed record WorkloadReportRead(List<DateOnly> Weeks, List<WorkloadEndpoints.PersonLoad> People, DateOnly Today);
    sealed class ReportReadClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    static TaskInputContext InputContext(Ctx c) => c.InputContext ?? (c.InputTasks is { } rows
        ? new(rows, InputBasis, InputCoverage) : throw new InvalidOperationException("Eligible report did not establish its task input source."));

    static void ValidateRemainingInputRange(Def definition, Ctx c)
    {
        if (definition.Code is "team-capacity" or "allocations-by-source")
        {
            var weeks = c.Int("weeks") ?? c.S.PlanningHorizonWeeks;
            Check.That(weeks is >= 1 and <= 26, "weeks", "error.range");
            var first = Workload.WeekOf(c.Date("from") ?? c.Today);
            var days = 7L * (weeks + (long)c.S.PlanningUnderWeeks - 1);
            Check.That(first.DayNumber + days <= DateOnly.MaxValue.DayNumber, "from", "error.invalid");
        }
        if (definition.Code == "task-hours" && c.Date("to") is null)
            Check.That((c.Date("from") ?? Workload.WeekOf(c.Today)).DayNumber <= DateOnly.MaxValue.DayNumber - 6, "from", "error.invalid");
    }

    static void SetPeopleInputs(Ctx c, IEnumerable<Guid> actualPeople, string basis, bool partial, Guid? projectId = null)
    {
        if (!c.InputsEnabled) return;
        var ids = actualPeople.Distinct().ToArray();
        // The identities came from the canonical authorised read. An empty cohort remains an explicit empty SQL source.
        var people = c.Db.Users.AsNoTracking().Where(person => ids.Contains(person.Id)).Select(person => person.Id);
        c.InputContext = new(WorkloadEndpoints.TaskQuery(c.Db, c.Access, people, null, projectId), basis,
            partial ? "VisibleOnly" : InputCoverage);
    }

    static async Task<WorkloadReportRead> ReadWorkloadInputs(Ctx c)
    {
        if (c.InputsEnabled && c.InputWorkload is { } existing) return existing;
        var (weeks, people, today) = await WorkloadEndpoints.Grid(new WorkloadEndpoints.WorkloadQuery(c.Id("supervisorId"), c.Id("disciplineId"), c.Id("officeId"), c.Id("projectId"), null, null, "name"),
            c.Access, c.Db, c.Me, c.Store, c.InputsEnabled ? c.ReadClock : c.Clock);
        var read = new WorkloadReportRead(weeks, people, today);
        if (c.InputsEnabled)
        {
            c.InputWorkload = read;
            // Discipline selected these people. Their other visible work stays in the task context, exactly as in Grid.
            SetPeopleInputs(c, people.Select(person => person.Id), "WorkloadPeopleContext", people.Any(person => person.PartialScope), c.Id("projectId"));
        }
        return read;
    }

    static async Task SetDisciplineInputs(Ctx c)
    {
        // This is DisciplineRows' canonical people source; TaskQuery owns all live/status/project task predicates.
        await PeopleScope.DemandWorkload(c.Db, c.Access.Actor);
        var people = await PeopleScope.Widen(c.Db, c.Access.Actor, WorkloadEndpoints.People(c.Db, c.Access, c.Me));
        if (c.Id("officeId") is { } office) people = people.Where(person => person.OfficeId == office);
        c.InputContext = new(WorkloadEndpoints.TaskQuery(c.Db, c.Access, people.Select(person => person.Id), null, null), "WorkloadPeopleContext", "VisibleOnly");
    }

    static async Task<(DateOnly From, DateOnly To, List<TimeEndpoints.Row> Rows)> ReadRecordedInputs(Ctx c)
    {
        var (from, to) = await TimeEndpoints.Range(new(c.Date("from"), c.Date("to"), null, null, null, null), c.Db, c.Store, c.InputsEnabled ? c.ReadClock : c.Clock);
        var filter = new TimeEndpoints.TimeQuery(from, to, c.Id("projectId"), null, c.Id("userId"), c.Str("scope") == "team" ? "all" : "mine");
        var rows = c.InputTime ?? await TimeEndpoints.Visible(filter, c.Access, c.Db, c.Me);
        if (c.InputsEnabled && c.InputTime is null)
        {
            c.InputTime = rows;
            var entries = rows.Select(row => row.Id).ToArray();
            var referenceCount = rows.Select(row => row.TaskId).Distinct().Count();
            var projects = c.Access.VisibleProjects().Select(project => project.Id);
            // Preserve the permission-checked entry/project/task pair. Do not IgnoreQueryFilters for current task facts.
            var current = c.Db.Tasks.AsNoTracking().Where(task => projects.Contains(task.ProjectId) && c.Db.TimeEntries.Any(entry => entries.Contains(entry.Id)
                && entry.TaskId == task.Id && entry.ProjectId == task.ProjectId));
            var unavailable = referenceCount - await current.CountAsync();
            c.InputContext = new(current, "CurrentRecordedTaskContext", unavailable > 0 ? "VisibleOnly" : InputCoverage, unavailable);
        }
        return (from, to, rows);
    }

    static async Task EnsureRemainingInputs(Def definition, Ctx c)
    {
        if (c.InputTasks is not null || c.InputContext is not null) return;
        switch (definition.Code)
        {
            case "workload-employee": await ReadWorkloadInputs(c); break;
            case "workload-discipline": await SetDisciplineInputs(c); break;
            // The normal report's historical unit loop establishes the exact displayed-week person union.
            case "team-capacity": await TeamCapacity(c); break;
            case "allocations-by-source": await CapacityGrid(c); break;
            case "task-hours": await ReadRecordedInputs(c); break;
        }
    }
}
