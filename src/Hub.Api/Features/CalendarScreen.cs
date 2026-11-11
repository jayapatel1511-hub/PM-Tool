using System.Data;
using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S15: current authorized Calendar projections. Every command remains on its canonical source route.
public static class CalendarScreenEndpoints
{
    public const string Flag = "work_screens";
    public sealed record CalendarScreenChoice(Guid Id, string Name);
    public sealed record CalendarScreenDeadline(string SourceType, Guid Id, string Key, string Title, Guid ProjectId, string ProjectNumber,
        DateOnly? DueDate, DateOnly? StartDate, int RowVersion, bool CanMove, bool NeedsReason, string? MoveReason);
    public sealed record CalendarScreenAwayDay(Guid PersonId, string Person, DateOnly Date, string State, decimal? Hours, bool Unverified);
    public sealed record CalendarScreenAwayRead(string State, IReadOnlyList<CalendarScreenAwayDay> Days);
    public sealed record CalendarScreenRead(CalendarEndpoints.CalendarRange Range, IReadOnlyList<CalendarScreenDeadline> Deadlines,
        Page<CalendarScreenDeadline> Undated, CalendarScreenAwayRead Away, IReadOnlyList<CalendarScreenChoice> People,
        IReadOnlyList<CalendarScreenChoice> Disciplines, DateTimeOffset AsOf);
    public sealed record CalendarScreenPreview(CalendarScreenDeadline Source, DateOnly DueDate,
        DependencyEndpoints.DateContextRead? Context, MilestoneEndpoints.DatePreview? Milestone);
    sealed record Source(CalendarScreenDeadline Row, IVersioned Version, Guid? PersonId, Guid? DisciplineId);
    sealed record Candidate(string SourceType, Guid Id, string ProjectNumber, string Key, Guid? PersonId, Guid? DisciplineId);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/calendar/screen", Read).RequireFlag(Flag);
        api.MapGet("/calendar/screen/{type}/{id:guid}/date-preview", Preview).RequireFlag(Flag);
    }

    static (int Page, int Size) Paging(int? page, int? pageSize)
    {
        var p = page ?? 1; var size = pageSize ?? 50;
        Check.That(p >= 1 && size is >= 1 and <= 100 && (long)(p - 1) * size <= int.MaxValue, "page", "error.invalid");
        return (p, size);
    }

    static async Task<Ok<CalendarScreenRead>> Read(DateOnly from, DateOnly to, string? projectIds, string? types, Guid? personId,
        Guid? disciplineId, bool? showAway, int? undatedPage, int? undatedPageSize, Access access, HubDb db, CurrentUser me,
        SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db); Http.RequireFlag(settings, Flag);
        Check.That(to >= from && to < DateOnly.MaxValue && to.DayNumber - from.DayNumber <= 92, "to", "calendar.range");
        Check.That(personId != Guid.Empty && disciplineId != Guid.Empty, "personId", "error.invalid");
        var (page, size) = Paging(undatedPage, undatedPageSize);
        // The shared client transport omits empty strings; this new reader alone accepts an explicit no-layer token.
        if (types?.Trim() == "none") types = "";
        foreach (var type in Http.List(types)) Check.OneOf(type, [CalendarEndpoints.Deadlines, .. CalendarEventType.All], "types");
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var range = await CalendarEndpoints.ReadRange(from, to, projectIds, types, access, db, me, settings, explicitEmptyTypes: true);
        var projectQuery = Scope.Projects(access, db, projectIds);
        var pids = projectQuery.Select(project => project.Id);
        var wantsDeadlines = types is null || Http.List(types).Contains(CalendarEndpoints.Deadlines);
        var sources = new List<Source>();
        foreach (var item in range.Entries.OfType<CalendarEndpoints.CalendarDeadline>())
            sources.Add(await LoadSource(item.SourceType, item.Id, projectIds, access, db));
        var undated = new List<Candidate>();
        if (wantsDeadlines)
        {
            var tasks = await db.Tasks.AsNoTracking().Where(task => pids.Contains(task.ProjectId) && task.DueDate == null
                && task.Status != TaskStatuses.Complete && task.Status != TaskStatuses.Cancelled).Select(task => new Candidate(ItemType.Task, task.Id,
                    db.Projects.Where(project => project.Id == task.ProjectId).Select(project => project.ProjectNumber).First(), task.Key, task.AssigneeId,
                    db.ProjectDisciplines.Where(row => row.Id == task.ProjectDisciplineId && row.ProjectId == task.ProjectId).Select(row => (Guid?)row.DisciplineId).FirstOrDefault())).ToListAsync();
            var deliverables = await db.Deliverables.AsNoTracking().Where(item => pids.Contains(item.ProjectId) && item.DueDate == null
                && item.Status != DeliverableStatus.Issued && item.Status != DeliverableStatus.Accepted && item.Status != DeliverableStatus.Cancelled).Select(item => new Candidate(ItemType.Deliverable, item.Id,
                    db.Projects.Where(project => project.Id == item.ProjectId).Select(project => project.ProjectNumber).First(), item.Key, item.OwnerId,
                    db.ProjectDisciplines.Where(row => row.Id == item.ProjectDisciplineId && row.ProjectId == item.ProjectId).Select(row => (Guid?)row.DisciplineId).FirstOrDefault())).ToListAsync();
            undated.AddRange(tasks); undated.AddRange(deliverables);
        }
        var cohort = await PlanningEndpoints.CalendarPeople(db, access, settings);
        var planners = cohort is null ? [] : await cohort.Select(user => new CalendarPlanner(user.Id, user.DisplayName, user.HomeDisciplineId)).ToListAsync();
        var owners = sources.Select(source => source.PersonId).Concat(undated.Select(source => source.PersonId)).OfType<Guid>()
            .Concat(range.Entries.OfType<CalendarEndpoints.CalendarEventRow>().Select(item => item.OwnerId)).Concat(planners.Select(user => user.Id)).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(user => owners.Contains(user.Id)).OrderBy(user => user.DisplayName).ThenBy(user => user.Id)
            .Select(user => new CalendarScreenChoice(user.Id, user.DisplayName)).ToListAsync();
        var disciplineIds = sources.Select(source => source.DisciplineId).Concat(undated.Select(source => source.DisciplineId)).OfType<Guid>()
            .Concat(planners.Select(user => user.HomeDisciplineId).OfType<Guid>()).Distinct().ToArray();
        var disciplines = await db.Disciplines.AsNoTracking().Where(discipline => disciplineIds.Contains(discipline.Id)).OrderBy(discipline => discipline.Name).ThenBy(discipline => discipline.Id)
            .Select(discipline => new CalendarScreenChoice(discipline.Id, discipline.Name)).ToListAsync();
        bool Match(Source source) => (personId is null || source.PersonId == personId) && (disciplineId is null || source.DisciplineId == disciplineId);
        var kept = sources.Where(Match).ToDictionary(source => (source.Row.SourceType, source.Row.Id));
        var entries = range.Entries.Where(item => item switch {
            CalendarEndpoints.CalendarDeadline deadline => kept.ContainsKey((deadline.SourceType, deadline.Id)),
            CalendarEndpoints.CalendarEventRow ev => disciplineId is null && (personId is null || ev.OwnerId == personId),
            _ => false }).ToList();
        var tray = undated.Where(source => (personId is null || source.PersonId == personId) && (disciplineId is null || source.DisciplineId == disciplineId))
            .OrderBy(source => source.ProjectNumber, StringComparer.Ordinal).ThenBy(source => source.SourceType, StringComparer.Ordinal)
            .ThenBy(source => source.Key, StringComparer.Ordinal).ThenBy(source => source.Id).ToArray();
        var trayRows = new List<CalendarScreenDeadline>();
        foreach (var source in tray.Skip((page - 1) * size).Take(size)) trayRows.Add((await LoadSource(source.SourceType, source.Id, projectIds, access, db)).Row);
        var away = new CalendarScreenAwayRead(showAway == true ? "Unavailable" : "Hidden", []);
        if (showAway == true && cohort is not null)
        {
            if (personId is null || planners.Any(person => person.Id == personId))
            {
                var selected = planners.Where(person => (personId is null || person.Id == personId) && (disciplineId is null || person.HomeDisciplineId == disciplineId)).ToArray();
                away = await Away(db, selected.Select(person => new CalendarScreenChoice(person.Id, person.DisplayName)).ToArray(), from, to, settings);
            }
        }
        return TypedResults.Ok(new CalendarScreenRead(range with { Entries = entries }, kept.Values.Select(source => source.Row).ToArray(),
            new Page<CalendarScreenDeadline>(trayRows, page, size, tray.Length),
            away, people, disciplines, clock.GetUtcNow()));
    }

    static async Task<CalendarScreenAwayRead> Away(HubDb db, CalendarScreenChoice[] people, DateOnly from, DateOnly to, OrgSettings settings)
    {
        var ids = people.Select(person => person.Id).ToArray();
        var days = await PersonCapacity.Load(db, ids, from, to, settings);
        var leaves = await db.ApprovedLeaves.AsNoTracking().Where(leave => ids.Contains(leave.PersonId) && leave.Status != "Cancelled" && leave.FromDate <= to && leave.ThroughDate >= from)
            .Select(leave => new CalendarLeave(leave.PersonId, leave.FromDate, leave.ThroughDate, leave.Status)).ToListAsync();
        var legacy = await db.AvailabilityOverrides.AsNoTracking().Where(value => ids.Contains(value.PersonId) && value.ReconciledLeaveId == null
            && value.WorkDate >= from && value.WorkDate <= to && (value.Category == "Unavailable" || value.Category == "Reduced"))
            .Select(value => new CalendarLegacyAway(value.PersonId, value.WorkDate)).ToListAsync();
        var rows = new List<CalendarScreenAwayDay>();
        foreach (var person in people)
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var records = leaves.Where(leave => leave.PersonId == person.Id && leave.FromDate <= date && leave.ThroughDate >= date).ToArray();
            if (records.Length == 0 && !legacy.Any(value => value.PersonId == person.Id && value.WorkDate == date)) continue;
            var day = days[(person.Id, date)];
            var unknown = records.Any(leave => leave.Status is not ("Approved" or "Unverified"));
            if (!unknown && (day.NonWorking || day.Holiday > 0) && day.Leave == 0) continue;
            if (unknown || day.Normal <= 0)
            { rows.Add(new(person.Id, person.Name, date, "Unknown", null, day.Provisional)); continue; }
            if (day.Leave <= 0) continue;
            rows.Add(new(person.Id, person.Name, date, day.Available == 0 && day.Leave >= day.Normal ? "Full" : "Reduced", day.Leave, day.Provisional));
        }
        return new("Available", rows);
    }

    static async Task<Source> LoadSource(string type, Guid id, string? projectIds, Access access, HubDb db)
    {
        Project project; ProjectContext context; IVersioned entity; Guid? person = null; Guid? discipline = null;
        DateOnly? due; DateOnly? start = null; string key; string name; Allow permission; bool open; bool reason;
        if (type == ItemType.Task)
        {
            var (task, p, ctx) = await TaskEndpoints.Load(db, access, id); project = p; context = ctx; entity = task;
            var facts = await TaskEndpoints.Facts(db, task); permission = Permissions.ChangeDueDate(access.Actor, context, facts);
            open = !TaskStatuses.IsTerminal(task.Status); reason = Permissions.DueChangeNeedsReason(access.Actor, context, facts);
            person = task.AssigneeId; discipline = task.ProjectDisciplineId; due = task.DueDate; start = task.StartDate; key = task.Key; name = task.Name;
        }
        else if (type == ItemType.Deliverable)
        {
            var (item, p, ctx) = await DeliverableEndpoints.Load(db, access, id); project = p; context = ctx; entity = item;
            permission = Permissions.EditDeliverable(access.Actor, context, await DeliverableEndpoints.Facts(db, item));
            open = item.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted or DeliverableStatus.Cancelled); reason = false;
            person = item.OwnerId; discipline = item.ProjectDisciplineId; due = item.DueDate; start = item.StartDate; key = item.Key; name = item.Name;
        }
        else if (type == ItemType.Milestone)
        {
            var (item, p, ctx) = await MilestoneEndpoints.Load(db, access, id); project = p; context = ctx; entity = item;
            permission = Permissions.ManageMilestones(access.Actor, context); open = !item.IsComplete && !item.IsCancelled; reason = true;
            due = item.Date; key = item.Key; name = item.Name;
        }
        else throw ApiException.NotFound();
        if (!await Scope.Projects(access, db, projectIds).AnyAsync(p => p.Id == project.Id)) throw ApiException.NotFound();
        if (discipline is { } pd) discipline = await db.ProjectDisciplines.AsNoTracking().Where(row => row.Id == pd && row.ProjectId == project.Id).Select(row => (Guid?)row.DisciplineId).SingleOrDefaultAsync();
        var can = permission.Ok && open;
        return new(new(type, id, key, name, project.Id, project.ProjectNumber, due, start, entity.RowVersion, can,
            reason || ProjectStatus.NeedsCorrectionReason(project.Status), can ? null : !open ? Text.Get("calendarScreen.closed") : Text.Get(permission.Why ?? "calendarScreen.cannotMove", permission.Arg ?? "")), entity, person, discipline);
    }

    static async Task<Ok<CalendarScreenPreview>> Preview(string type, Guid id, DateOnly dueDate, int rowVersion, string? projectIds,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db); Http.RequireFlag(settings, Flag);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
        var source = await LoadSource(type, id, projectIds, access, db);
        if (!source.Row.CanMove) throw ApiException.Forbidden("calendarScreen.cannotMove");
        await Http.CheckBodyVersion(db, source.Version, rowVersion);
        if (type == ItemType.Milestone)
        {
            var milestone = (Milestone)source.Version;
            var (preview, _, _) = await MilestoneEndpoints.PreviewDateChange(db, milestone, dueDate, false, false);
            return TypedResults.Ok(new CalendarScreenPreview(source.Row, dueDate, null, preview));
        }
        var context = await DependencyEndpoints.ReadDateContext(type, id, null, dueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), access, db, settings, clock);
        return TypedResults.Ok(new CalendarScreenPreview(source.Row, dueDate, context, null));
    }
}

file sealed record CalendarPlanner(Guid Id, string DisplayName, Guid? HomeDisciplineId);
file sealed record CalendarLeave(Guid PersonId, DateOnly FromDate, DateOnly ThroughDate, string Status);
file sealed record CalendarLegacyAway(Guid PersonId, DateOnly WorkDate);
