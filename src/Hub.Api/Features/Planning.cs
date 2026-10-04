using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Packet 034: one authorized input set drives cells, totals, explanations and exports.
public static class PlanningEndpoints
{
    public sealed record CreateBody(Guid PersonId, decimal HoursPerWeek, DateOnly StartWeek, DateOnly EndWeek,
        string Label, string SourceCategory = PlanningSource.Other, Guid? ProjectId = null, Guid? ProjectDisciplineId = null,
        string? Confidence = null, string? Visibility = null, string? Notes = null);
    public sealed record QuickBody(Guid PersonId, DateOnly Week, string Text);
    public sealed record VisibilityBody(int RowVersion, string Visibility, string? Reason = null);
    public sealed record VersionBody(int RowVersion, string? Reason = null);
    public sealed record DateVersionInput(DateOnly WorkDate, int RowVersion);
    public sealed record TimeAwayBody(Guid PersonId, DateOnly From, DateOnly Through, string Action,
        decimal AvailableHours = 0, string Category = "Unavailable", DateVersionInput[]? Versions = null);
    public sealed record GridQuery(DateOnly? From = null, int? Weeks = null, Guid? PersonId = null,
        Guid? SupervisorId = null, Guid? DisciplineId = null, Guid? OfficeId = null, Guid? ProjectId = null,
        string? Source = null, string? Confidence = null, string? Visibility = null, string? Indicator = null,
        string? Q = null, string? Sort = null, DateOnly? Week = null, bool IncludeMyDrafts = true, string? DraftAccessReason = null);
    public sealed record EntryQuery(Guid? PersonId = null, bool? Mine = null, DateOnly? From = null, DateOnly? To = null,
        string? Source = null, string? Confidence = null, string? Visibility = null, Guid? ProjectId = null,
        string? Q = null, bool? Stale = null, bool IncludeMyDrafts = true, string? DraftAccessReason = null,
        string? Sort = null, int? Page = null, int? PageSize = null);
    public sealed record EntryRow(Guid Id, Guid PersonId, string PersonName, Guid OwnerId, string OwnerName,
        string OwnerKind, string Label, string SourceCategory, Guid? ProjectId, string? ProjectNumber,
        string? ProjectName, Guid? ProjectDisciplineId, string? DisciplineName, decimal HoursPerWeek,
        DateOnly StartWeek, DateOnly EndWeek, string Confidence, string Visibility, string? Notes,
        DateTimeOffset LastValidatedAt, bool Stale, string[] Warnings, bool CanEdit, bool CanChangeVisibility,
        bool CorrectionOnly, int RowVersion, bool MatchesFilter = true,
        decimal? WeekHours = null, decimal? CountedHours = null, decimal? CoveredHours = null);
    public sealed record DayRow(DateOnly Date, decimal Normal, decimal Available, string? Category, decimal TimeAway, decimal Additional);
    public sealed record CellRow(DateOnly Week, decimal Capacity, decimal TimeAway, decimal AdditionalAvailability,
        decimal Approved, decimal Confirmed, decimal Expected, decimal Possible, decimal Remaining,
        decimal TaskEstimates, int UnestimatedTasks, bool OverPlanned, bool UnderPlanned, int StaleEntries, decimal OwnDraftHours);
    public sealed record AllocationWeek(DateOnly Week, decimal Hours);
    public sealed record ApprovedRow(Guid AllocationId, Guid ProjectId, string ProjectNumber, string ProjectName,
        string ApprovalStatus, bool CanOpen, AllocationWeek[] Weeks);
    public sealed record PersonRow(Guid Id, string DisplayName, Guid? SupervisorId, string? SupervisorName,
        decimal WeeklyCapacity, bool CanCreateSelfEntry, bool CanCreateManagerEntry, bool CanRecordTimeAway,
        string[] Indicators, CellRow[] Cells, EntryRow[] Entries, ApprovedRow[] ApprovedAllocations);
    public sealed record PlannerSettings(int HorizonWeeks, int OverPct, int UnderPct, int UnderWeeks, int StaleDays, int MaxHoursPerWeek);
    public sealed record GridResult(DateOnly[] Weeks, DateOnly CurrentWeek, DateTimeOffset EvaluatedAt,
        bool PartialView, bool DraftAccess, PlannerSettings Settings, PersonRow[] People);
    sealed record ReadModel(GridResult Grid, Dictionary<(Guid, DateOnly), DayRow[]> Days);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/planning/grid", Grid);
        api.MapGet("/planning/cells/{personId:guid}/{week}", Cell);
        api.MapGet("/planning/entries", List);
        api.MapPost("/planning/entries", Create);
        api.MapPost("/planning/entries/quick", Quick);
        api.MapPatch("/planning/entries/{id:guid}", Edit);
        api.MapPost("/planning/entries/{id:guid}/visibility", ChangeVisibility);
        api.MapPost("/planning/entries/{id:guid}/still-valid", StillValid);
        api.MapGet("/planning/entries/{id:guid}", Detail);
        api.MapGet("/planning/entries/{id:guid}/activity", Activity);
        api.MapGet("/planning/entries/export", EntriesExport);
        api.MapGet("/planning/grid/export", GridExport);
        api.MapPost("/planning/time-away", TimeAway);
        api.MapDelete("/planning/entries/{id:guid}", Delete);
    }

    internal static IQueryable<AppUser> People(HubDb db, Access access)
        => WorkloadEndpoints.People(db, access, access.Me)
            .Union(db.Users.AsNoTracking().Where(u => u.IsActive && u.Id == access.Actor.Id));

    /// PLN-04 is applied before counting, paging, coverage and aggregation.
    internal static IQueryable<PlanningEntry> VisibleEntries(HubDb db, Access access, bool includeMyDrafts = true, bool correction = false)
    {
        var uid = access.Actor.Id;
        var people = People(db, access).Select(u => u.Id);
        var projects = access.VisibleProjectIds();
        return db.PlanningEntries.AsNoTracking().Where(e => people.Contains(e.PersonId)
            && (e.ProjectId == null || projects.Contains(e.ProjectId.Value))
            && (e.Visibility != PlanningVisibility.Draft || correction || (includeMyDrafts && e.CreatedBy == uid)));
    }

    /// Requestless PLN-04 counterpart for the recipient's digest; no correction mode exists in a digest.
    internal static IQueryable<PlanningEntry> VisibleForUser(HubDb db, Guid uid)
    {
        var roles = db.UserRoles.Where(r => r.UserId == uid).Select(r => r.Role);
        var all = roles.Contains(SystemRole.Admin) || roles.Contains(SystemRole.Executive);
        var managed = db.Projects.Where(p => WorkloadEndpoints.LiveProjects.Contains(p.Status)
            && (p.ProjectManagerId == uid || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))).Select(p => p.Id);
        var members = db.ProjectMembers.Where(m => m.RemovedAt == null && managed.Contains(m.ProjectId)).Select(m => m.UserId);
        var projects = db.Projects.Where(p => all || p.Visibility == Hub.Domain.Visibility.Open || p.ProjectManagerId == uid
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null)).Select(p => p.Id);
        var people = db.Users.Where(u => u.IsActive && (u.Id == uid || all || roles.Contains(SystemRole.Supervisor) && u.SupervisorId == uid
            || roles.Contains(SystemRole.ProjectManager) && members.Contains(u.Id))).Select(u => u.Id);
        return db.PlanningEntries.AsNoTracking().Where(e => db.Users.Any(u => u.Id == uid && u.IsActive) && people.Contains(e.PersonId) && (e.ProjectId == null || projects.Contains(e.ProjectId.Value))
            && (e.Visibility != PlanningVisibility.Draft || e.CreatedBy == uid));
    }

    static bool Correction(Access access, string? reason)
    {
        if (reason is null) return false;
        Access.Demand(access.Actor.Admin ? Allow.Yes : Allow.No("perm.admin"));
        Check.That(reason.Trim().Length >= 5, "reason", "error.reason_required");
        return true;
    }

    internal static async Task LogAccess(HubDb db, Access access, string? reason, IEnumerable<Guid> people, TimeProvider clock, bool save = true)
    {
        if (reason is null) return;
        var scope = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToListAsync();
        db.ActivityLog.Add(new ActivityLog { ActorUserId = access.Actor.Id, ActorType = "Admin", ItemType = "PlanningDraftAccess",
            Action = "Viewed", Reason = reason.Trim(), Categories = ["admin"], Changes = "[]",
            Snapshot = JsonSerializer.Serialize(scope, JsonOpts.Web), Source = "API", OccurredAt = clock.GetUtcNow() });
        if (save) await db.SaveChangesAsync();
    }

    static async Task EnsurePerson(HubDb db, Access access, Guid id)
    {
        if (!await People(db, access).AnyAsync(u => u.Id == id)) throw ApiException.NotFound();
    }

    static WorkCalendar Calendar(OrgSettings settings, AppUser person, IEnumerable<Holiday> holidays)
        => settings.WorkingDaysEnabled ? new WorkCalendar(holidays.Where(h => h.OfficeId == null || h.OfficeId == person.OfficeId).Select(h => h.Date)) : WorkCalendar.Weekdays;

    static DayRow[] Days(AppUser person, DateOnly week, OrgSettings settings, IEnumerable<Holiday> holidays,
        IReadOnlyDictionary<(Guid, DateOnly), PersonAvailabilityOverride> overrides)
    {
        var calendar = Calendar(settings, person, holidays);
        var weekly = person.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours;
        return Enumerable.Range(0, 7).Select(i =>
        {
            var date = week.AddDays(i); var value = overrides.GetValueOrDefault((person.Id, date));
            var normal = AllocationRules.DailyCapacity(date, weekly, calendar, null);
            var (away, additional) = PlanningRules.TimeAway(normal, value?.AvailableHours, value?.Category);
            return new DayRow(date, normal, AllocationRules.DailyCapacity(date, weekly, calendar, value?.AvailableHours), value?.Category, away, additional);
        }).ToArray();
    }

    static async Task<EntryRow[]> Rows(HubDb db, Access access, IReadOnlyList<PlanningEntry> entries, OrgSettings settings, TimeProvider clock)
    {
        if (entries.Count == 0) return [];
        var ids = entries.SelectMany(e => new[] { e.PersonId, e.CreatedBy!.Value }).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var roles = await db.UserRoles.AsNoTracking().Where(r => ids.Contains(r.UserId)).ToListAsync();
        var projectIds = entries.Select(e => e.ProjectId).OfType<Guid>().Distinct().ToArray();
        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => projectIds.Contains(d.ProjectId))
            .Select(d => new { d.Id, d.Discipline!.Name }).ToDictionaryAsync(d => d.Id, d => d.Name);
        var holidays = settings.WorkingDaysEnabled ? await db.Holidays.AsNoTracking().ToListAsync() : [];
        var from = entries.Min(e => e.StartWeek); var through = entries.Max(e => e.EndWeek).AddDays(6);
        var overrides = await db.AvailabilityOverrides.AsNoTracking().Where(o => ids.Contains(o.PersonId) && o.WorkDate >= from && o.WorkDate <= through)
            .ToDictionaryAsync(o => (o.PersonId, o.WorkDate));
        var now = clock.GetUtcNow(); var current = Workload.WeekOf(clock.Today(settings));
        return entries.Select(e =>
        {
            var person = users[e.PersonId]; var owner = users[e.CreatedBy!.Value];
            var self = owner.Id == person.Id;
            var stillManages = owner.IsActive && (self || roles.Any(r => r.UserId == owner.Id && r.Role == SystemRole.Admin)
                || (person.SupervisorId == owner.Id && roles.Any(r => r.UserId == owner.Id && r.Role == SystemRole.Supervisor)));
            var project = e.ProjectId is { } pid ? projects.GetValueOrDefault(pid) : null;
            var warnings = new List<string>();
            if (project is not null && !WorkloadEndpoints.LiveProjects.Contains(project.Status)) warnings.Add(PlanningWarning.ProjectNotActive);
            if (!self && !stillManages) warnings.Add(PlanningWarning.OwnerCannotManage);
            for (var week = e.StartWeek; week <= e.EndWeek; week = week.AddDays(7))
                if (e.HoursPerWeek > Days(person, week, settings, holidays, overrides).Sum(d => d.Available)) { warnings.Add(PlanningWarning.AboveCapacity); break; }
            var canEdit = Permissions.ManagePlanningEntry(access.Actor,
                new(person.Id, owner.Id, person.SupervisorId, e.Visibility, self, stillManages, person.IsActive)).Ok;
            return new EntryRow(e.Id, person.Id, person.DisplayName, owner.Id, owner.DisplayName, self ? "Self" : "Manager",
                e.Label, e.SourceCategory, e.ProjectId, project?.ProjectNumber, project?.Name, e.ProjectDisciplineId,
                e.ProjectDisciplineId is { } did ? disciplines.GetValueOrDefault(did) : null, e.HoursPerWeek, e.StartWeek,
                e.EndWeek, e.Confidence, e.Visibility, e.Notes, e.LastValidatedAt,
                PlanningRules.Stale(e.EndWeek, current, e.LastValidatedAt, now, settings.PlanningStaleDays), warnings.ToArray(),
                canEdit, canEdit && !self, canEdit && owner.Id != access.Actor.Id, e.RowVersion);
        }).ToArray();
    }

    static bool Matches(EntryRow row, Guid? project, string? source, string? confidence, string? visibility, string? q)
        => (project is null || row.ProjectId == project)
        && (string.IsNullOrWhiteSpace(source) || Http.List(source).Contains(row.SourceCategory))
        && (string.IsNullOrWhiteSpace(confidence) || Http.List(confidence).Contains(row.Confidence))
        && (string.IsNullOrWhiteSpace(visibility) || Http.List(visibility).Contains(row.OwnerKind == "Self" ? "Self" : row.Visibility))
        && (string.IsNullOrWhiteSpace(q) || row.Label.Contains(q, StringComparison.OrdinalIgnoreCase) || row.Notes?.Contains(q, StringComparison.OrdinalIgnoreCase) == true);

    static async Task<ReadModel> Read(GridQuery query, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        if (query.PersonId != access.Actor.Id) Access.Demand(Permissions.ViewWorkload(access.Actor));
        if (query.PersonId is { } requested) await EnsurePerson(db, access, requested);
        var correction = Correction(access, query.DraftAccessReason);
        var settings = await store.Get(db); var today = clock.Today(settings); var current = Workload.WeekOf(today);
        var first = Workload.WeekOf(query.From ?? today);
        Check.That(query.Weeks is null or (>= 1 and <= 26), "weeks", "error.range");
        var displayCount = query.Weeks ?? settings.PlanningHorizonWeeks;
        var weeks = Enumerable.Range(0, displayCount + settings.PlanningUnderWeeks - 1).Select(i => first.AddDays(i * 7)).ToArray();
        var people = People(db, access);
        if (query.PersonId is { } personId) people = people.Where(u => u.Id == personId);
        if (query.SupervisorId is { } supervisorId) people = people.Where(u => u.SupervisorId == supervisorId);
        if (query.OfficeId is { } officeId) people = people.Where(u => u.OfficeId == officeId);
        var users = await people.ToListAsync(); var ids = users.Select(u => u.Id).ToArray();
        var supervisors = await db.Users.AsNoTracking().Where(u => users.Select(x => x.SupervisorId).Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var entries = await VisibleEntries(db, access, query.IncludeMyDrafts, correction)
            .Where(e => ids.Contains(e.PersonId) && e.StartWeek <= weeks[weeks.Length - 1] && e.EndWeek >= first).ToListAsync();
        var entryRows = await Rows(db, access, entries, settings, clock);
        var tasks = await WorkloadEndpoints.Tasks(db, access, people.Select(u => u.Id), null, null);
        var taskCalendars = await Calendars.For(db, settings, tasks.Select(t => t.ProjectId).Distinct().ToArray());
        var forecasts = tasks.ToDictionary(t => t.Id, t => Workload.Spread(new(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate), today, taskCalendars(t.ProjectId)));
        var disciplineIds = query.DisciplineId is { } discipline ? await db.ProjectDisciplines.AsNoTracking().Where(d => d.DisciplineId == discipline).Select(d => d.Id).ToListAsync() : null;
        var allocations = await db.Allocations.AsNoTracking().Where(a => ids.Contains(a.PersonId) && a.Status == AllocationStatus.Confirmed
            && access.VisibleProjectIds().Contains(a.ProjectId) && db.Projects.Any(p => p.Id == a.ProjectId && WorkloadEndpoints.LiveProjects.Contains(p.Status))
            && a.FromDate <= weeks[weeks.Length - 1].AddDays(6) && a.ThroughDate >= first).ToListAsync();
        var allocationIds = allocations.Select(a => a.Id).ToArray();
        var explicitDays = await db.AllocationDayOverrides.AsNoTracking().Where(d => allocationIds.Contains(d.AllocationId)).ToListAsync();
        var projects = await db.Projects.AsNoTracking().Where(p => allocations.Select(a => a.ProjectId).Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var actorMemberships = await db.ProjectMembers.AsNoTracking().Where(m => m.UserId == access.Actor.Id && m.RemovedAt == null).ToListAsync();
        var availability = await db.AvailabilityOverrides.AsNoTracking().Where(o => ids.Contains(o.PersonId) && o.WorkDate >= first && o.WorkDate <= weeks[weeks.Length - 1].AddDays(6)).ToDictionaryAsync(o => (o.PersonId, o.WorkDate));
        var holidays = settings.WorkingDaysEnabled ? await db.Holidays.AsNoTracking().ToListAsync() : [];
        var dayRows = new Dictionary<(Guid, DateOnly), DayRow[]>();
        var rows = new List<PersonRow>(); var partial = !access.SeesAllRestricted;
        foreach (var user in users)
        {
            var personEntries = entryRows.Where(e => e.PersonId == user.Id).Select(e => e with { MatchesFilter = Matches(e, query.ProjectId, query.Source, query.Confidence, query.Visibility, query.Q) }).ToArray();
            var personTasks = tasks.Where(t => t.AssigneeId == user.Id).ToArray();
            var calendar = Calendar(settings, user, holidays);
            var projected = allocations.Where(a => a.PersonId == user.Id).Select(a =>
            {
                var project = projects[a.ProjectId];
                IReadOnlyDictionary<DateOnly, decimal> spread;
                try { spread = AllocationRules.Spread(a.FromDate, a.ThroughDate, a.PlannedHours, calendar, explicitDays.Where(d => d.AllocationId == a.Id).ToDictionary(d => d.WorkDate, d => d.Hours)); }
                catch (ArgumentException) { throw ApiException.Conflict("allocation_calendar_changed", "coord.stale"); }
                // §37.6 permits PM/managing roles, Supervisors, Admins and Executives to open details, not the allocated person solely by assignment.
                var canOpen = access.Actor.Admin || a.CreatedBy == access.Actor.Id || (access.Actor.Supervisor && user.SupervisorId == access.Actor.Id)
                    || project.ProjectManagerId == access.Actor.Id || actorMemberships.Any(m => m.ProjectId == project.Id && m.Roles.Contains(ProjectRole.PM));
                return new ApprovedRow(a.Id, project.Id, project.ProjectNumber, project.Name, AllocationStatus.Confirmed, canOpen,
                    weeks.Select(w => new AllocationWeek(w, spread.Where(d => Workload.WeekOf(d.Key) == w).Sum(d => d.Value))).ToArray());
            }).ToArray();
            var cells = weeks.Select(week =>
            {
                var days = Days(user, week, settings, holidays, availability); dayRows[(user.Id, week)] = days;
                var input = entries.Where(e => e.PersonId == user.Id && e.StartWeek <= week && e.EndWeek >= week).ToArray();
                var approved = projected.GroupBy(a => a.ProjectId).ToDictionary(g => g.Key, g => g.Sum(a => a.Weeks.First(w => w.Week == week).Hours));
                var capacity = days.Sum(d => d.Available);
                var bands = PlanningRules.Combine(capacity, input.Select(e => new EntryWeek(e.Id, e.ProjectId, e.Confidence, e.HoursPerWeek, e.StartWeek, e.CreatedAt)).ToArray(), approved);
                return new CellRow(week, capacity, days.Sum(d => d.TimeAway), days.Sum(d => d.Additional), bands.Approved,
                    bands.Confirmed, bands.Expected, bands.Possible, bands.Remaining,
                    personTasks.Sum(t => forecasts[t.Id].ByWeek.GetValueOrDefault(week)),
                    personTasks.Count(t => t.EstimatedHours == null && t.DueDate is { } due && Workload.WeekOf(due) == week),
                    PlanningRules.OverPlanned(bands, capacity, settings.PlanningOverPct), false,
                    personEntries.Count(e => e.Stale && e.StartWeek <= week && e.EndWeek >= week),
                    input.Where(e => e.CreatedBy == access.Actor.Id && e.Visibility == PlanningVisibility.Draft).Sum(e => e.HoursPerWeek));
            }).ToArray();
            var under = PlanningRules.UnderPlanned(cells.Select(c => new UnderWeek(c.Capacity, c.TimeAway, c.Confirmed + c.Expected + c.Possible, c.UnestimatedTasks > 0)).ToArray(),
                Array.FindIndex(weeks, w => w >= current) is var start && start >= 0 ? start : weeks.Length,
                settings.PlanningUnderPct, settings.PlanningUnderWeeks, partial);
            cells = cells.Select((c, i) => c with { UnderPlanned = under[i] }).ToArray();
            cells = cells.Take(displayCount).ToArray();
            personEntries = personEntries.Where(e => e.StartWeek <= weeks[displayCount - 1] && e.EndWeek >= first).ToArray();
            projected = projected.Select(a => a with { Weeks = a.Weeks.Take(displayCount).ToArray() }).ToArray();
            if (disciplineIds is not null && !personEntries.Any(e => e.ProjectDisciplineId is { } d && disciplineIds.Contains(d))
                && !personTasks.Any(t => disciplineIds.Contains(t.ProjectDisciplineId))) continue;
            var entryFilter = query.ProjectId is not null || !string.IsNullOrWhiteSpace(query.Source) || !string.IsNullOrWhiteSpace(query.Confidence) || !string.IsNullOrWhiteSpace(query.Visibility) || !string.IsNullOrWhiteSpace(query.Q);
            if (entryFilter && !personEntries.Any(e => e.MatchesFilter)) continue;
            string[] indicators = [.. cells.Any(c => c.OverPlanned) ? new[] { PlanningIndicator.OverPlanned } : [],
                .. cells.Any(c => c.UnderPlanned) ? new[] { PlanningIndicator.UnderPlanned } : [],
                .. personEntries.Any(e => e.Stale) ? new[] { PlanningIndicator.StalePlan } : []];
            if (!string.IsNullOrEmpty(query.Indicator) && !indicators.Contains(query.Indicator)) continue;
            var create = Permissions.CreatePlanningEntry(access.Actor, user.Id, user.SupervisorId).Ok;
            rows.Add(new(user.Id, user.DisplayName, user.SupervisorId, user.SupervisorId is { } sid ? supervisors.GetValueOrDefault(sid) : null,
                user.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours, create && user.Id == access.Actor.Id,
                create && user.Id != access.Actor.Id, Permissions.RecordTimeAway(access.Actor, user.SupervisorId, user.Id).Ok,
                indicators, cells, personEntries, projected));
        }
        var sortWeek = Workload.WeekOf(query.Week ?? current);
        CellRow SortCell(PersonRow p) => p.Cells.FirstOrDefault(c => c.Week == sortWeek) ?? p.Cells[0];
        var sorted = query.Sort switch
        {
            "name" => rows.OrderBy(p => p.DisplayName),
            "over" => rows.OrderByDescending(p => SortCell(p).Confirmed + SortCell(p).Expected - SortCell(p).Capacity).ThenBy(p => p.DisplayName),
            _ => rows.OrderBy(p => SortCell(p).Remaining).ThenBy(p => p.DisplayName),
        };
        if (correction) await LogAccess(db, access, query.DraftAccessReason, ids, clock);
        return new(new(weeks.Take(displayCount).ToArray(), current, clock.GetUtcNow(), partial, correction,
            new(settings.PlanningHorizonWeeks, settings.PlanningOverPct, settings.PlanningUnderPct, settings.PlanningUnderWeeks, settings.PlanningStaleDays, settings.PlanningMaxHoursPerWeek), sorted.ToArray()), dayRows);
    }

    static async Task<IResult> Grid([AsParameters] GridQuery query, Access access, HubDb db, SettingsStore store, TimeProvider clock)
        => Results.Ok((await Read(query, access, db, store, clock)).Grid);

    static async Task<IResult> Cell(Guid personId, DateOnly week, bool? includeMyDrafts, string? draftAccessReason,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Check.That(week.DayOfWeek == DayOfWeek.Monday, "week", "planning.week");
        var settings = await store.Get(db);
        var model = await Read(new(week, settings.PlanningUnderWeeks, personId, IncludeMyDrafts: includeMyDrafts ?? true, DraftAccessReason: draftAccessReason), access, db, store, clock);
        var person = model.Grid.People.Single(); var cell = person.Cells[0]; var days = model.Days[(personId, week)];
        var raw = await VisibleEntries(db, access, includeMyDrafts ?? true, model.Grid.DraftAccess).Where(e => e.PersonId == personId && e.StartWeek <= week && e.EndWeek >= week).ToListAsync();
        var approved = person.ApprovedAllocations.GroupBy(a => a.ProjectId).ToDictionary(g => g.Key, g => g.Sum(a => a.Weeks[0].Hours));
        var counts = PlanningRules.Combine(cell.Capacity, raw.Select(e => new EntryWeek(e.Id, e.ProjectId, e.Confidence, e.HoursPerWeek, e.StartWeek, e.CreatedAt)).ToArray(), approved).Entries.ToDictionary(e => e.Id);
        return Results.Ok(new { PersonId = personId, PersonName = person.DisplayName, Week = week, model.Grid.PartialView,
            Capacity = new { person.WeeklyCapacity, CapacityOverride = await db.Users.AnyAsync(u => u.Id == personId && u.WeeklyCapacityHours != null),
                WorkingDays = days.Count(d => d.Normal > 0), cell.Capacity, cell.TimeAway, cell.AdditionalAvailability, Days = days },
            ApprovedAllocations = person.ApprovedAllocations.Where(a => a.Weeks[0].Hours > 0).Select(a => new { a.AllocationId, a.ProjectId, a.ProjectNumber, a.ProjectName, Hours = a.Weeks[0].Hours, a.ApprovalStatus, a.CanOpen }),
            Entries = person.Entries.Where(e => counts.ContainsKey(e.Id)).Select(e => e with { WeekHours = e.HoursPerWeek, CountedHours = counts[e.Id].Counted, CoveredHours = counts[e.Id].Covered }),
            Bands = new { cell.Confirmed, cell.Expected, cell.Possible }, cell.Remaining,
            TaskEstimates = new { Hours = cell.TaskEstimates, cell.UnestimatedTasks },
            Indicators = new[] {
                new { Code = PlanningIndicator.OverPlanned, Rule = "PLN-10", Evaluated = true, Active = cell.OverPlanned, Threshold = settings.PlanningOverPct,
                    Explanation = Text.Get("planning.explain_over", cell.Confirmed + cell.Expected, settings.PlanningOverPct, cell.Capacity, cell.Capacity * settings.PlanningOverPct / 100) },
                new { Code = PlanningIndicator.UnderPlanned, Rule = "PLN-11", Evaluated = !model.Grid.PartialView && settings.PlanningUnderPct > 0, Active = cell.UnderPlanned, Threshold = settings.PlanningUnderPct,
                    Explanation = Text.Get(model.Grid.PartialView ? "planning.explain_partial" : "planning.explain_under", settings.PlanningUnderPct, settings.PlanningUnderWeeks) },
                new { Code = PlanningIndicator.StalePlan, Rule = "PLN-12", Evaluated = true, Active = cell.StaleEntries > 0, Threshold = settings.PlanningStaleDays,
                    Explanation = Text.Get("planning.explain_stale", cell.StaleEntries, settings.PlanningStaleDays) } } });
    }

    static async Task<EntryRow[]> EntryRows(EntryQuery query, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        if (query.PersonId is { } id) await EnsurePerson(db, access, id);
        var correction = Correction(access, query.DraftAccessReason); var settings = await store.Get(db);
        var q = VisibleEntries(db, access, query.IncludeMyDrafts, correction);
        if (query.PersonId is { } pid) q = q.Where(e => e.PersonId == pid);
        if (query.Mine == true) q = q.Where(e => e.CreatedBy == access.Actor.Id);
        if (query.From is { } from) q = q.Where(e => e.EndWeek >= from);
        if (query.To is { } to) q = q.Where(e => e.StartWeek <= to);
        var rows = (await Rows(db, access, await q.ToListAsync(), settings, clock))
            .Where(e => Matches(e, query.ProjectId, query.Source, query.Confidence, query.Visibility, query.Q) && (query.Stale == null || e.Stale == query.Stale)).ToArray();
        if (correction) await LogAccess(db, access, query.DraftAccessReason, query.PersonId is { } p ? new[] { p } : await People(db, access).Select(u => u.Id).ToArrayAsync(), clock);
        return query.Sort switch { "person" => rows.OrderBy(e => e.PersonName).ThenBy(e => e.StartWeek).ThenBy(e => e.Id).ToArray(),
            "lastValidated" => rows.OrderBy(e => e.LastValidatedAt).ThenBy(e => e.Id).ToArray(),
            _ => rows.OrderBy(e => e.StartWeek).ThenBy(e => e.Id).ToArray() };
    }

    static async Task<IResult> List([AsParameters] EntryQuery query, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var rows = await EntryRows(query, access, db, store, clock); var (page, size) = Http.Paging(query.Page, query.PageSize);
        return Results.Ok(new Page<EntryRow>(rows.Skip((page - 1) * size).Take(size).ToArray(), page, size, rows.Length));
    }

    static async Task<IResult> Detail(Guid id, string? draftAccessReason, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var correction = Correction(access, draftAccessReason);
        var entry = await VisibleEntries(db, access, true, correction).SingleOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
        if (correction) await LogAccess(db, access, draftAccessReason, [entry.PersonId], clock);
        return Results.Ok((await Rows(db, access, [entry], await store.Get(db), clock))[0]);
    }

    static async Task<IResult> Activity(Guid id, string? draftAccessReason, int? page, int? pageSize,
        Access access, HubDb db, TimeProvider clock)
    {
        var correction = Correction(access, draftAccessReason);
        var entry = await VisibleEntries(db, access, true, correction).SingleOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
        if (correction) await LogAccess(db, access, draftAccessReason, [entry.PersonId], clock);
        var (pg, size) = Http.Paging(page, pageSize);
        var q = db.ActivityLog.AsNoTracking().Where(a => a.ItemType == "PlanningEntry" && a.ItemId == id);
        var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
        return Results.Ok(new Page<object>(await ActivityEndpoints.Render(db, rows), pg, size, await q.CountAsync()));
    }

    static async Task Validate(HubDb db, Access access, PlanningEntry entry, OrgSettings settings, bool checkLink)
    {
        Check.That(PlanningRules.ValidateWeeks(entry.StartWeek, entry.EndWeek) is null, "startWeek", "planning.week");
        Check.That(PlanningRules.ValidateHours(entry.HoursPerWeek, settings.PlanningMaxHoursPerWeek) is null, "hoursPerWeek", "planning.hours");
        Check.That(!string.IsNullOrWhiteSpace(entry.Label) && entry.Label.Trim().Length <= 120, "label", "error.required");
        Check.That(entry.Notes is null || entry.Notes.Length <= 2000, "notes", "error.too_long", 2000);
        Check.OneOf(entry.SourceCategory, PlanningSource.All, "sourceCategory");
        Check.OneOf(entry.Confidence, PlanningConfidence.All, "confidence");
        Check.OneOf(entry.Visibility, PlanningVisibility.All, "visibility");
        Check.That(entry.CreatedBy != entry.PersonId || entry.Visibility == PlanningVisibility.Confirmed, "visibility", "planning.visibility");
        Check.That((entry.SourceCategory == PlanningSource.MajorProject) == (entry.ProjectId != null), "projectId", "planning.project");
        Check.That(entry.ProjectDisciplineId is null || entry.ProjectId is not null, "projectDisciplineId", "planning.project");
        if (checkLink && entry.ProjectId is { } id)
        {
            var project = await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id) ?? throw ApiException.NotFound();
            await access.Project(id, false);
            Check.That(project.Status is ProjectStatus.Setup or ProjectStatus.Active or ProjectStatus.OnHold, "projectId", "planning.project");
            Check.That(await EmailProjectAccess.Allowed(db, entry.PersonId, [id]), "projectId", "planning.project");
            if (entry.ProjectDisciplineId is { } discipline)
                Check.That(await db.ProjectDisciplines.AnyAsync(d => d.Id == discipline && d.ProjectId == id && d.IsActive), "projectDisciplineId", "planning.project");
        }
        entry.Label = entry.Label.Trim();
    }

    static async Task<PlanningEntry> Writable(Guid id, string? reason, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        // A reason on an Admin command is its explicit data-correction mode. Otherwise private records remain 404.
        var correction = access.Actor.Admin && reason is not null;
        if (correction) Correction(access, reason);
        var entry = await VisibleEntries(db, access, true, correction).SingleOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
        var row = (await Rows(db, access, [entry], await store.Get(db), clock))[0];
        Access.Demand(row.CanEdit ? Allow.Yes : Allow.No("planning.owner_authority"));
        if (row.CorrectionOnly) { Correction(access, reason ?? ""); Check.That(reason?.Trim().Length >= 5, "reason", "error.reason_required"); }
        if (correction) await LogAccess(db, access, reason, [entry.PersonId], clock, false);
        return await db.PlanningEntries.SingleAsync(e => e.Id == id);
    }

    internal static IQueryable<Notification> VisibleNotifications(HubDb db, Access access)
    {
        var projects = access.VisibleProjectIds();
        var entries = VisibleEntries(db, access).Select(e => e.Id);
        return db.Notifications.Where(n => n.UserId == access.Actor.Id && (n.ProjectId == null || projects.Contains(n.ProjectId.Value))
            && (n.ItemType != "PlanningEntry" || n.ItemId == null || entries.Contains(n.ItemId.Value)));
    }

    internal static async Task<bool> PlanningEmailAllowed(HubDb db, Guid userId, Guid[] ids)
    {
        var wanted = ids.Distinct().ToArray();
        return await VisibleForUser(db, userId).CountAsync(e => wanted.Contains(e.Id)) == wanted.Length;
    }

    static async Task<bool> PersonCanSeeProject(HubDb db, Guid personId, Guid? projectId)
    {
        if (!await db.Users.AnyAsync(u => u.Id == personId && u.IsActive)) return false;
        if (projectId is null) return true;
        var elevated = await db.UserRoles.AnyAsync(r => r.UserId == personId && (r.Role == SystemRole.Admin || r.Role == SystemRole.Executive));
        return await db.Projects.AnyAsync(p => p.Id == projectId && (elevated || p.Visibility != Hub.Domain.Visibility.Restricted || p.ProjectManagerId == personId
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == personId && m.RemovedAt == null)));
    }

    static async Task Notice(HubDb db, Access access, Notifier notify, PlanningEntry entry, bool wasVisible, TimeProvider clock, bool deleted = false)
    {
        var self = entry.CreatedBy == entry.PersonId;
        var visible = entry.Visibility != PlanningVisibility.Draft && !deleted;
        if (self && entry.CreatedBy == access.Actor.Id || !wasVisible && !visible) return;
        var withdrawn = wasVisible && !visible;
        var dedup = $"{NotificationEvents.PlanningEntryChanged}:{entry.Id}:{entry.PersonId}";
        if (withdrawn)
        {
            var old = await db.Notifications.Where(n => n.UserId == entry.PersonId && n.ItemType == "PlanningEntry" && n.ItemId == entry.Id).ToListAsync();
            foreach (var n in old) { n.Title = Text.Get("planning.withdrawn"); n.Body = null; n.ItemKey = null; n.ItemId = null; n.LinkPath = "/my-work"; }
            var queued = await db.Emails.Where(m => m.SentAt == null && m.SuppressedAt == null && m.RequiredPlanningEntryIds.Contains(entry.Id)).ToListAsync();
            foreach (var m in queued) { m.SuppressedAt = clock.GetUtcNow(); m.LastError = "PlanningAssignmentWithdrawn"; m.Subject = Text.Get("planning.withdrawn"); m.BodyText = Text.Get("planning.withdrawn"); m.BodyHtml = null; }
        }
        if (!await PersonCanSeeProject(db, entry.PersonId, entry.ProjectId)) return;
        var title = Text.Get(withdrawn ? "planning.withdrawn" : "planning.changed", entry.Label);
        var body = withdrawn ? null : $"{Text.Get($"planning.confidence.{entry.Confidence}")} · {Text.Get(self ? "planning.visibility.Self" : $"planning.visibility.{entry.Visibility}")}";
        var link = withdrawn ? "/my-work" : $"/my-work?myWeek={entry.StartWeek:yyyy-MM-dd}";
        await notify.Send(NotificationEvents.PlanningEntryChanged, entry.PersonId,
            new NotifyItem(null, "PlanningEntry", entry.Id, null, link), title, body);
        foreach (var n in db.Notifications.Local.Where(n => n.UserId == entry.PersonId && n.ItemType == "PlanningEntry" && n.CollapseKey?.Contains(entry.Id.ToString()) == true))
        { n.Body = body; n.ItemId = withdrawn ? null : entry.Id; n.LinkPath = link; }
        foreach (var m in db.Emails.Local.Where(m => m.DedupKey == dedup && db.Entry(m).State == EntityState.Added))
        { m.RequiredPlanningEntryIds = withdrawn ? [] : [entry.Id]; m.Kind = withdrawn ? "PlanningWithdrawal" : "PlanningEntryChanged"; }
    }

    static void AuditChange(HubDb db, Access access, PlanningEntry entry, string action, string? reason)
        => db.Audit.Note(entry, action: entry.CreatedBy != access.Actor.Id ? "DataCorrection" : action,
            reason: entry.CreatedBy != access.Actor.Id ? reason?.Trim() : null,
            categories: entry.CreatedBy != access.Actor.Id ? ["admin", "data-correction"] : null);

    static async Task<IResult> Create(CreateBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var person = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == body.PersonId && u.IsActive) ?? throw ApiException.NotFound();
        Access.Demand(Permissions.CreatePlanningEntry(access.Actor, person.Id, person.SupervisorId));
        var defaults = PlanningRules.Defaults(person.Id == access.Actor.Id, false);
        var entry = new PlanningEntry { PersonId = body.PersonId, CreatedBy = access.Actor.Id, HoursPerWeek = body.HoursPerWeek,
            StartWeek = body.StartWeek, EndWeek = body.EndWeek, Label = body.Label, SourceCategory = body.SourceCategory,
            ProjectId = body.ProjectId, ProjectDisciplineId = body.ProjectDisciplineId, Confidence = body.Confidence ?? defaults.Confidence,
            Visibility = body.Visibility ?? defaults.Visibility, Notes = body.Notes, LastValidatedAt = clock.GetUtcNow() };
        var settings = await store.Get(db); await Validate(db, access, entry, settings, true);
        db.PlanningEntries.Add(entry);
        await Notice(db, access, notify, entry, false, clock);
        await db.SaveChangesAsync(); // Entry, immutable audit, notice and email share this transaction.
        return Results.Created($"/api/v1/planning/entries/{entry.Id}", (await Rows(db, access, [entry], settings, clock))[0]);
    }

    static async Task<IResult> Quick(QuickBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var value = PlanningRules.ParseQuickAdd(body.Text, (await store.Get(db)).PlanningMaxHoursPerWeek);
        Check.That(value is not null, "text", "planning.quick_add_format");
        var defaults = PlanningRules.Defaults(body.PersonId == access.Actor.Id, value!.Possible);
        return await Create(new(body.PersonId, value.Hours, body.Week, body.Week, value.Label, PlanningSource.OtherProject,
            Confidence: defaults.Confidence, Visibility: defaults.Visibility), access, db, store, clock, notify);
    }

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db,
        SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var patch = new Patch(body); var reason = patch.Str("reason");
        var entry = await Writable(id, reason, access, db, store, clock);
        await Http.CheckVersion(db, http, entry, patch.RowVersion);
        var wasVisible = entry.Visibility != PlanningVisibility.Draft;
        var originalProject = entry.ProjectId; var originalDiscipline = entry.ProjectDisciplineId; var originalPerson = entry.PersonId;
        if (patch.Has("personId"))
        {
            var personId = patch.Id("personId") ?? throw ApiException.Invalid("personId", "error.required");
            if (personId != entry.PersonId)
            {
                Check.That(entry.CreatedBy != entry.PersonId && personId != entry.CreatedBy, "personId", "planning.person_fixed");
                var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
                Access.Demand(Permissions.CreatePlanningEntry(access.Actor, target.Id, target.SupervisorId)); entry.PersonId = personId;
            }
        }
        if (patch.Has("hoursPerWeek")) entry.HoursPerWeek = patch.Dec("hoursPerWeek") ?? 0;
        if (patch.Has("startWeek")) entry.StartWeek = patch.Date("startWeek") ?? default;
        if (patch.Has("endWeek")) entry.EndWeek = patch.Date("endWeek") ?? default;
        if (patch.Has("label")) entry.Label = patch.Str("label") ?? "";
        if (patch.Has("sourceCategory")) entry.SourceCategory = patch.Str("sourceCategory") ?? "";
        if (patch.Has("projectId")) entry.ProjectId = patch.Id("projectId");
        if (patch.Has("projectDisciplineId")) entry.ProjectDisciplineId = patch.Id("projectDisciplineId");
        if (patch.Has("confidence")) entry.Confidence = patch.Str("confidence") ?? "";
        if (patch.Has("notes")) entry.Notes = patch.Str("notes");
        var settings = await store.Get(db);
        await Validate(db, access, entry, settings, originalProject != entry.ProjectId || originalDiscipline != entry.ProjectDisciplineId || originalPerson != entry.PersonId);
        entry.LastValidatedAt = clock.GetUtcNow();
        db.Entry(entry).Property(e => e.LastValidatedAt).IsModified = true;
        AuditChange(db, access, entry, originalPerson != entry.PersonId ? "Moved" : "Changed", reason);
        if (originalPerson != entry.PersonId && wasVisible)
        {
            var moved = new PlanningEntry { Id = entry.Id, PersonId = originalPerson, CreatedBy = entry.CreatedBy, Visibility = PlanningVisibility.Draft };
            await Notice(db, access, notify, moved, true, clock);
        }
        await Notice(db, access, notify, entry, wasVisible && originalPerson == entry.PersonId, clock);
        await db.SaveChangesAsync(); return Results.Ok((await Rows(db, access, [entry], settings, clock))[0]);
    }

    static async Task<IResult> ChangeVisibility(Guid id, VisibilityBody body, HttpContext http, Access access, HubDb db,
        SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var entry = await Writable(id, body.Reason, access, db, store, clock);
        await Http.CheckVersion(db, http, entry, body.RowVersion);
        Check.That(PlanningRules.StepVisibility(entry.CreatedBy == entry.PersonId, entry.Visibility, body.Visibility), "visibility", "planning.visibility");
        var wasVisible = entry.Visibility != PlanningVisibility.Draft; entry.Visibility = body.Visibility;
        entry.LastValidatedAt = clock.GetUtcNow(); db.Entry(entry).Property(e => e.LastValidatedAt).IsModified = true;
        AuditChange(db, access, entry, "VisibilityChanged", body.Reason); await Notice(db, access, notify, entry, wasVisible, clock);
        await db.SaveChangesAsync(); return Results.Ok((await Rows(db, access, [entry], await store.Get(db), clock))[0]);
    }

    static async Task<IResult> StillValid(Guid id, VersionBody body, HttpContext http, Access access, HubDb db,
        SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var entry = await Writable(id, body.Reason, access, db, store, clock); await Http.CheckVersion(db, http, entry, body.RowVersion);
        entry.LastValidatedAt = clock.GetUtcNow(); db.Entry(entry).Property(e => e.LastValidatedAt).IsModified = true;
        AuditChange(db, access, entry, "Validated", body.Reason); await Notice(db, access, notify, entry, entry.Visibility != PlanningVisibility.Draft, clock);
        await db.SaveChangesAsync(); return Results.Ok((await Rows(db, access, [entry], await store.Get(db), clock))[0]);
    }

    static async Task<IResult> Delete(Guid id, int? rowVersion, string? reason, HttpContext http, Access access, HubDb db,
        SettingsStore store, TimeProvider clock, Notifier notify)
    {
        var entry = await Writable(id, reason, access, db, store, clock); await Http.CheckVersion(db, http, entry, rowVersion);
        var wasVisible = entry.Visibility != PlanningVisibility.Draft;
        entry.DeletedAt = clock.GetUtcNow(); entry.DeletedBy = access.Actor.Id;
        AuditChange(db, access, entry, "Deleted", reason); await Notice(db, access, notify, entry, wasVisible, clock, true);
        await db.SaveChangesAsync(); return Results.NoContent();
    }

    static async Task<IResult> TimeAway(TimeAwayBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Check.That(body.From != default && body.Through >= body.From && body.Through.DayNumber - body.From.DayNumber < 366, "through", "error.date_range");
        Check.OneOf(body.Action, ["Record", "Clear"], "action");
        if (body.Action == "Record") { Check.That(body.AvailableHours is >= 0 and <= 24, "availableHours", "error.range"); Check.OneOf(body.Category, ["Unavailable", "Reduced"], "category"); }
        Check.That(body.Versions is null || body.Versions.Select(v => v.WorkDate).Distinct().Count() == body.Versions.Length, "versions", "error.invalid");
        var settings = await store.Get(db);
        return await Tx.Run(db, async () =>
        {
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.app_user WHERE id = {0} FOR UPDATE", body.PersonId).ToListAsync();
            var person = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == body.PersonId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.RecordTimeAway(access.Actor, person.SupervisorId, person.Id));
            var holidays = settings.WorkingDaysEnabled ? await db.Holidays.AsNoTracking().ToListAsync() : [];
            var calendar = Calendar(settings, person, holidays);
            var overrides = await db.AvailabilityOverrides.Where(o => o.PersonId == person.Id && o.WorkDate >= body.From && o.WorkDate <= body.Through).ToDictionaryAsync(o => o.WorkDate);
            var dates = Enumerable.Range(0, body.Through.DayNumber - body.From.DayNumber + 1).Select(i => body.From.AddDays(i))
                .Where(d => body.Action == "Record" ? calendar.IsWorkingDay(d) : overrides.TryGetValue(d, out var o) && o.Category is "Unavailable" or "Reduced").ToArray();
            Check.That(body.Action == "Clear" || dates.Length > 0, "range", "planning.time_away_days");
            var expected = (body.Versions ?? []).ToDictionary(v => v.WorkDate, v => v.RowVersion);
            var stale = dates.Where(d => expected.GetValueOrDefault(d) != (overrides.GetValueOrDefault(d)?.RowVersion ?? 0)).ToArray();
            if (stale.Length > 0) throw ApiException.Conflict("planning.stale_dates", "coord.stale", new { staleDates = stale });
            var versions = await db.PersonDateVersions.Where(v => v.PersonId == person.Id && dates.Contains(v.WorkDate)).ToDictionaryAsync(v => v.WorkDate);
            var changed = new List<PersonAvailabilityOverride>();
            foreach (var date in dates)
            {
                var row = overrides.GetValueOrDefault(date);
                if (body.Action == "Clear") { db.AvailabilityOverrides.Remove(row!); }
                else
                {
                    if (row is null) { row = new() { PersonId = person.Id, WorkDate = date, RowVersion = 1 }; db.AvailabilityOverrides.Add(row); }
                    else db.Entry(row).Property(o => o.AvailableHours).IsModified = true;
                    row.AvailableHours = body.AvailableHours; row.Category = body.Category; changed.Add(row);
                }
                if (versions.TryGetValue(date, out var version)) db.Entry(version).Property(v => v.WorkDate).IsModified = true;
                else { version = new() { PersonId = person.Id, WorkDate = date, RowVersion = 1 }; db.PersonDateVersions.Add(version); versions[date] = version; }
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { body.PersonId,
                Days = changed.Select(o => new { Date = o.WorkDate, o.AvailableHours, o.Category, o.RowVersion, DateVersion = versions[o.WorkDate].RowVersion }).ToArray(),
                Cleared = body.Action == "Clear" ? dates.Select(d => new { Date = d, DateVersion = versions[d].RowVersion }).ToArray() : [] });
        });
    }

    static string VisibilityLabel(EntryRow row) => Text.Get(row.OwnerKind == "Self" ? "planning.visibility.Self" : $"planning.visibility.{row.Visibility}");
    static IReadOnlyList<(string, string)> Parameters(HttpContext http)
        => http.Request.Query.Where(p => p.Key != "format" && p.Key != "draftAccessReason").Select(p => (p.Key, p.Value.ToString())).ToArray();

    static async Task<IResult> EntriesExport([AsParameters] EntryQuery query, string? format, HttpContext http,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Access.Demand(Permissions.ViewWorkload(access.Actor));
        var rows = await EntryRows(query, access, db, store, clock);
        var data = rows.Select(e => new { e.PersonName, e.OwnerName, e.OwnerKind, e.Label,
            SourceCategory = Text.Get($"planning.source.{e.SourceCategory}"), e.ProjectNumber, e.DisciplineName,
            e.HoursPerWeek, e.StartWeek, e.EndWeek, Confidence = Text.Get($"planning.confidence.{e.Confidence}"),
            Visibility = VisibilityLabel(e), e.LastValidatedAt, e.Stale, Warnings = e.Warnings.Select(w => Text.Get($"planning.warning.{w}")).ToArray() });
        Col[] cols = [new("personName", Text.Get("common.person")), new("ownerName", Text.Get("col.owner")), new("ownerKind", Text.Get("planning.owner_kind")),
            new("label", Text.Get("planning.label")), new("sourceCategory", Text.Get("planning.source")), new("projectNumber", Text.Get("common.project")),
            new("disciplineName", Text.Get("common.discipline")), new("hoursPerWeek", Text.Get("planning.hours_per_week"), "number"),
            new("startWeek", Text.Get("planning.start_week"), "date"), new("endWeek", Text.Get("planning.end_week"), "date"),
            new("confidence", Text.Get("planning.confidence")), new("visibility", Text.Get("planning.visibility")),
            new("lastValidatedAt", Text.Get("planning.last_validated"), "datetime"), new("stale", Text.Get("planning.stale")), new("warnings", Text.Get("planning.warnings"))];
        return await ExportFile.Send(db, store, format, Text.Get("planning.entries_title"), cols,
            JsonSerializer.SerializeToNode(data, JsonOpts.Web)!.AsArray(), Parameters(http), null, "planning", clock);
    }

    static async Task<IResult> GridExport([AsParameters] GridQuery query, string? format, HttpContext http,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Access.Demand(Permissions.ViewWorkload(access.Actor));
        var grid = (await Read(query, access, db, store, clock)).Grid;
        var rows = new JsonArray(); var cols = new List<Col> { new("person", Text.Get("common.person")), new("supervisor", Text.Get("common.supervisor")),
            new("weeklyCapacity", Text.Get("planning.weekly_capacity"), "number"), new("partialView", Text.Get("planning.partial")), new("includeMyDrafts", Text.Get("planning.include_drafts")) };
        for (var i = 0; i < grid.Weeks.Length; i++)
            foreach (var field in new[] { "capacity", "timeAway", "approved", "confirmed", "expected", "possible", "remaining", "taskEstimates", "indicators" })
                cols.Add(new($"w{i}.{field}", $"{grid.Weeks[i]:yyyy-MM-dd} · {Text.Get($"planning.export.{field}")}", field == "indicators" ? "text" : "number"));
        foreach (var person in grid.People)
        {
            var row = new JsonObject { ["person"] = person.DisplayName, ["supervisor"] = person.SupervisorName, ["weeklyCapacity"] = person.WeeklyCapacity, ["partialView"] = grid.PartialView, ["includeMyDrafts"] = query.IncludeMyDrafts };
            for (var i = 0; i < person.Cells.Length; i++)
            {
                var c = person.Cells[i];
                row[$"w{i}"] = new JsonObject { ["capacity"] = c.Capacity, ["timeAway"] = c.TimeAway, ["approved"] = c.Approved,
                    ["confirmed"] = c.Confirmed, ["expected"] = c.Expected, ["possible"] = c.Possible, ["remaining"] = c.Remaining,
                    ["taskEstimates"] = c.TaskEstimates, ["indicators"] = string.Join("; ", new[] { c.OverPlanned ? PlanningIndicator.OverPlanned : null,
                        c.UnderPlanned ? PlanningIndicator.UnderPlanned : null, c.StaleEntries > 0 ? PlanningIndicator.StalePlan : null }.OfType<string>().Select(x => Text.Get($"planning.indicator.{x}"))) };
            }
            rows.Add(row);
        }
        var parameters = Parameters(http).ToList();
        if (!http.Request.Query.ContainsKey("includeMyDrafts")) parameters.Add(("includeMyDrafts", "true"));
        return await ExportFile.Send(db, store, format, Text.Get("planning.grid_title"), cols, rows, parameters, null, "planning-grid", clock);
    }
}

/// Only packet 034 uses this atomic receipt. All existing POST behavior remains in its original middleware branch.
public static class PlanningIdempotency
{
    public static async Task Run(HttpContext ctx, RequestDelegate next, HubDb db, CurrentUser me, TimeProvider clock, string key)
    {
        ctx.Request.EnableBuffering();
        using var request = new MemoryStream(); await ctx.Request.Body.CopyToAsync(request); ctx.Request.Body.Position = 0;
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(request.ToArray()));
        var path = ctx.Request.Path.Value ?? "";
        var lockBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"planning:{me.Id}:{key}"));
        var lockId = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(lockBytes);
        var body = request.ToArray();
        var response = await Tx.Run(db, async () =>
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock({0})", lockId);
            var seen = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == me.Id && r.Key == key);
            if (seen is not null && seen.CreatedAt > clock.GetUtcNow().AddDays(-1))
            {
                if (seen.Path != $"{path}|{hash}") throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                await AuthorizeReplay(path, body, seen.Body, db, me, clock);
                ctx.Response.StatusCode = seen.Status; ctx.Response.ContentType = seen.ContentType;
                ctx.Response.Headers["Idempotent-Replayed"] = "true";
                return seen.Body;
            }
            if (seen is not null) db.Idempotency.Remove(await db.Idempotency.SingleAsync(r => r.Id == seen.Id));
            var original = ctx.Response.Body;
            await using var buffer = new MemoryStream(); ctx.Response.Body = buffer;
            try { await next(ctx); } finally { ctx.Response.Body = original; }
            if (ctx.Response.StatusCode is >= 200 and < 300)
            {
                db.Idempotency.Add(new IdempotencyRecord { UserId = me.Id, Key = key, Path = $"{path}|{hash}", Status = ctx.Response.StatusCode,
                    ContentType = ctx.Response.ContentType, Body = buffer.ToArray(), CreatedAt = clock.GetUtcNow() });
                await db.SaveChangesAsync();
            }
            return buffer.ToArray();
        });
        // Commit the mutation and its receipt before exposing the successful response.
        await ctx.Response.Body.WriteAsync(response);
    }

    static async Task AuthorizeReplay(string path, byte[] request, byte[] response, HubDb db, CurrentUser me, TimeProvider clock)
    {
        var input = JsonNode.Parse(request)!.AsObject();
        JsonNode? Input(string name) => input.FirstOrDefault(x => string.Equals(x.Key, name, StringComparison.OrdinalIgnoreCase)).Value; var access = new Access(db, me);
        if (path.EndsWith("/time-away", StringComparison.Ordinal))
        {
            var personId = Guid.Parse(Input("personId")!.GetValue<string>());
            var person = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.RecordTimeAway(access.Actor, person.SupervisorId, person.Id));
            return;
        }
        var output = JsonNode.Parse(response)!;
        var id = Guid.Parse(output["id"]!.GetValue<string>());
        var reason = Input("reason")?.GetValue<string>();
        var correction = access.Actor.Admin && reason?.Trim().Length >= 5;
        var entry = await PlanningEndpoints.VisibleEntries(db, access, true, correction).SingleOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
        var subject = await db.Users.AsNoTracking().SingleAsync(u => u.Id == entry.PersonId);
        if (correction) await PlanningEndpoints.LogAccess(db, access, reason, [entry.PersonId], clock);
        if (path.EndsWith("/entries", StringComparison.Ordinal) || path.EndsWith("/quick", StringComparison.Ordinal))
            Access.Demand(Permissions.CreatePlanningEntry(access.Actor, subject.Id, subject.SupervisorId));
        else
        {
            Access.Demand(Permissions.ManagePlanningEntry(access.Actor, new(entry.PersonId, entry.CreatedBy!.Value, subject.SupervisorId, entry.Visibility, entry.CreatedBy == entry.PersonId, access.Actor.Admin || access.Actor.Supervisor && subject.SupervisorId == me.Id)).Ok && (entry.CreatedBy == me.Id || correction) ? Allow.Yes : Allow.No("planning.owner_authority"));
        }
    }
}
