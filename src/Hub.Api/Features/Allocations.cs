using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class AllocationEndpoints
{
    public sealed record DayInput(DateOnly WorkDate, decimal Hours);
    public sealed record LinkInput(string WorkType, Guid WorkId, DateOnly WorkDate, decimal? ReviewHours = null);
    public sealed record CreateBody(Guid RequestId, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string? Reason);
    public sealed record EditBody(Guid RequestId, int RowVersion, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string Reason);
    public sealed record CancelBody(Guid RequestId, int RowVersion, string Reason);
    public sealed record AvailabilityBody(int ExpectedRowVersion, decimal AvailableHours, string Category);
    public sealed record DateVersionInput(DateOnly WorkDate, int RowVersion);
    public sealed record ConfirmBody(Guid RequestId, int RowVersion, DateVersionInput[] DateVersions, string? OverCapacityReason);
    public sealed record CapacityDay(DateOnly Date, decimal AvailableHours, decimal ConfirmedHours,
        decimal ProposedHours, decimal ResultingHours, decimal OverByHours, int DateVersion);
    public sealed record Filter(string? Q, Guid? PersonId, string? Purpose, string? Status, DateOnly? From, DateOnly? To);
    sealed record AllocationRow(Guid Id, Guid PersonId, string? PersonName, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, string Status, int RowVersion);

    internal static IQueryable<ResourceAllocation> VisibleQuery(HubDb db, Actor actor, IQueryable<Guid> managedProjectIds) =>
        db.Allocations.AsNoTracking().Where(a => managedProjectIds.Contains(a.ProjectId) || a.CreatedBy == actor.Id || ((actor.Supervisor || actor.Admin)
            && db.Users.Any(u => u.Id == a.PersonId && (actor.Admin || u.SupervisorId == actor.Id))));

    static readonly Col[] ExportColumns =
    [
        new("personName", "person"), new("purpose", "type"), new("fromDate", "date", "date", "From date"),
        new("throughDate", "date", "date", "Through date"), new("plannedHours", "hours", "number"), new("status", "status"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/allocations", List);
        api.MapGet("/projects/{projectId:guid}/allocations/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/allocations/review-options", ReviewOptions);
        api.MapGet("/projects/{projectId:guid}/allocations/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/allocations", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/edit", Edit).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/cancel", Cancel).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/decline", Decline).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/complete", Complete).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/users/{personId:guid}/availability", Availability);
        api.MapPut("/users/{personId:guid}/availability/{date}", SetAvailability);
        api.MapGet("/projects/{projectId:guid}/allocations/{id:guid}/confirmation-preview", ConfirmationPreview);
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/confirm", Confirm).WithMetadata(new Coordination.AtomicCommand());
    }

    static async Task NotifyChanged(HubDb db, Notifier notify, Project project, ResourceAllocation a)
    {
        var person = await db.Users.Where(u => u.Id == a.PersonId)
            .Select(u => new { u.DisplayName, u.SupervisorId }).FirstAsync();
        await notify.Send(NotificationEvents.AllocationChanged,
            [project.ProjectManagerId, a.CreatedBy, person.SupervisorId],
            new NotifyItem(project.Id, "ResourceAllocation", a.Id, null,
                $"/projects/{project.ProjectNumber}/allocations?allocation={a.Id}", project.ProjectNumber),
            Text.Get("notify.allocation_changed", person.DisplayName, a.Status));
    }

    static async Task LockPerson(HubDb db, Guid personId) =>
        await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.app_user WHERE id = {0} FOR UPDATE", personId).ToListAsync();

    static async Task TouchDates(HubDb db, Guid personId, IEnumerable<DateOnly> dates)
    {
        var target = dates.Distinct().Order().ToArray();
        if (target.Length == 0) return;
        var first = target[0]; var last = target[^1];
        var existing = await db.PersonDateVersions.Where(v => v.PersonId == personId && v.WorkDate >= first && v.WorkDate <= last)
            .ToDictionaryAsync(v => v.WorkDate);
        foreach (var day in target)
            if (existing.TryGetValue(day, out var version)) db.Entry(version).Property(v => v.WorkDate).IsModified = true;
            else db.PersonDateVersions.Add(new PersonDateVersion { PersonId = personId, WorkDate = day, RowVersion = 1 });
    }

    static async Task ValidateSaved(HubDb db, Project project, ResourceAllocation a, SettingsStore store)
    {
        var days = await db.AllocationDayOverrides.AsNoTracking().Where(d => d.AllocationId == a.Id)
            .Select(d => new DayInput(d.WorkDate, d.Hours)).ToArrayAsync();
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(l => l.AllocationId == a.Id && l.ReleasedAt == null)
            .Select(l => new LinkInput(l.WorkType, l.WorkId, l.WorkDate, l.ReviewHours)).ToArrayAsync();
        await Validate(db, project, a.PersonId, a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, days, links, store);
    }

    static async Task<List<CapacityDay>> Capacity(HubDb db, Access access, ResourceAllocation candidate,
        SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        var today = clock.Today(settings);
        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == candidate.PersonId);
        var holidays = settings.WorkingDaysEnabled
            ? await db.Holidays.Where(h => h.OfficeId == null || h.OfficeId == person.OfficeId).Select(h => h.Date).ToListAsync() : [];
        var personCalendar = settings.WorkingDaysEnabled ? new WorkCalendar(holidays) : WorkCalendar.Weekdays;
        var confirmed = await db.Allocations.AsNoTracking().Where(a => a.PersonId == person.Id && a.Id != candidate.Id
            && a.Status == AllocationStatus.Confirmed && a.FromDate <= candidate.ThroughDate && a.ThroughDate >= candidate.FromDate).ToListAsync();
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.AssigneeId == person.Id && t.DeletedAt == null
            && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold
            && db.Projects.Any(p => p.Id == t.ProjectId && (p.Status == ProjectStatus.Setup || p.Status == ProjectStatus.Active)))
            .Select(t => new LoadTask(t.Id, t.ProjectId, t.EstimatedHours, t.ProgressPct, t.StartDate, t.DueDate)).ToListAsync();
        var projectIds = confirmed.Select(a => a.ProjectId).Append(candidate.ProjectId).Concat(tasks.Select(t => t.ProjectId)).Distinct().ToArray();
        if (!access.Actor.Admin)
        {
            var visible = await access.VisibleProjectIdSet();
            // No aggregate difference or overload amount may reveal hidden assignments.
            if (projectIds.Any(p => !visible.Contains(p))) throw ApiException.Forbidden("perm.workload");
        }
        var taskCalendar = await Calendars.For(db, settings, tasks.Select(t => t.ProjectId).Distinct().ToArray());
        var taskDemand = tasks.ToDictionary(t => t.TaskId, t => Workload.SpreadDays(t, today, taskCalendar(t.ProjectId)).ByDay);
        var all = confirmed.Append(candidate).ToArray();
        var allocationIds = all.Select(a => a.Id).ToArray();
        var overrides = await db.AllocationDayOverrides.AsNoTracking().Where(d => allocationIds.Contains(d.AllocationId)).ToListAsync();
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(l => allocationIds.Contains(l.AllocationId) && l.ReleasedAt == null).ToListAsync();
        var reservations = new Dictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>();
        foreach (var a in all)
        {
            var dayHours = overrides.Where(d => d.AllocationId == a.Id).ToDictionary(d => d.WorkDate, d => d.Hours);
            try { reservations[a.Id] = AllocationRules.Spread(a.FromDate, a.ThroughDate, a.PlannedHours, personCalendar, dayHours); }
            catch (ArgumentException) { throw ApiException.Conflict("allocation_calendar_changed", "coord.stale"); }
        }
        var capacityOverrides = await db.AvailabilityOverrides.AsNoTracking().Where(o => o.PersonId == person.Id
            && o.WorkDate >= candidate.FromDate && o.WorkDate <= candidate.ThroughDate).ToDictionaryAsync(o => o.WorkDate, o => o.AvailableHours);
        var versions = await db.PersonDateVersions.AsNoTracking().Where(v => v.PersonId == person.Id
            && v.WorkDate >= candidate.FromDate && v.WorkDate <= candidate.ThroughDate).ToDictionaryAsync(v => v.WorkDate, v => v.RowVersion);
        var result = new List<CapacityDay>();
        for (var date = candidate.FromDate; date <= candidate.ThroughDate; date = date.AddDays(1))
        {
            var taskHours = taskDemand.Values.Sum(days => days.GetValueOrDefault(date));
            var linkedTaskHours = 0m;
            var demands = new List<AllocationDemand>();
            foreach (var a in all)
            {
                var linked = 0m;
                foreach (var link in links.Where(l => l.AllocationId == a.Id && l.WorkDate == date))
                {
                    if (link.WorkType == "Task")
                    {
                        var hours = taskDemand.GetValueOrDefault(link.WorkId)?.GetValueOrDefault(date) ?? 0;
                        linked += hours; linkedTaskHours += hours;
                    }
                    else linked += link.ReviewHours ?? 0;
                }
                demands.Add(new AllocationDemand(reservations[a.Id].GetValueOrDefault(date), linked));
            }
            var unlinked = Math.Max(0, taskHours - linkedTaskHours);
            var existing = AllocationRules.Committed(demands.Take(confirmed.Count), unlinked +
                links.Where(l => l.AllocationId == candidate.Id && l.WorkDate == date && l.WorkType == "Task")
                    .Sum(l => taskDemand.GetValueOrDefault(l.WorkId)?.GetValueOrDefault(date) ?? 0));
            var resulting = AllocationRules.Committed(demands, unlinked);
            var available = AllocationRules.DailyCapacity(date, person.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours,
                personCalendar, capacityOverrides.TryGetValue(date, out var overrideHours) ? overrideHours : (decimal?)null);
            result.Add(new CapacityDay(date, available, existing, candidate.Status == AllocationStatus.Proposed
                ? Math.Max(demands[^1].ReservedHours, demands[^1].LinkedRemainingHours) : 0, resulting,
                Math.Max(0, resulting - available), versions.GetValueOrDefault(date)));
        }
        return result;
    }

    static async Task<object> ConfirmationPreview(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, ctx) = await access.Project(projectId, false);
        var a = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == id) ?? throw ApiException.NotFound();
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.PersonId);
        Check.That(person is not null && person.IsActive && await Coordination.People(db, project).AnyAsync(u => u.Id == a.PersonId), "personId", "coord.person");
        var supervisorId = person!.SupervisorId;
        Access.Demand(Permissions.ConfirmAllocation(access.Actor, ctx, supervisorId));
        Check.That(a.Status == AllocationStatus.Proposed, "status", "error.invalid");
        await ValidateSaved(db, project, a, store);
        return new { a.Id, a.RowVersion, Days = await Capacity(db, access, a, store, clock) };
    }

    static Task<Coordination.Result> Confirm(Guid projectId, Guid id, ConfirmBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.confirm", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.PersonId);
            Check.That(person is not null && person.IsActive && await Coordination.People(db, project).AnyAsync(u => u.Id == a.PersonId), "personId", "coord.person");
            Access.Demand(Permissions.ConfirmAllocation(access.Actor, ctx, person.SupervisorId));
            Check.That(a.Status == AllocationStatus.Proposed, "status", "error.invalid");
            await LockPerson(db, person.Id);
            await ValidateSaved(db, project, a, store);
            var days = await Capacity(db, access, a, store, clock);
            Check.That(body.DateVersions is { Length: > 0 } && body.DateVersions.Length == days.Count
                && body.DateVersions.Select(v => v.WorkDate).Distinct().Count() == days.Count,
                "dateVersions", "error.invalid");
            var expected = body.DateVersions.ToDictionary(v => v.WorkDate, v => v.RowVersion);
            if (days.Any(d => !expected.TryGetValue(d.Date, out var version) || version != d.DateVersion))
                throw ApiException.Conflict("concurrency_conflict", "coord.stale");
            if (days.Any(d => d.OverByHours > 0)) Check.Reason(body.OverCapacityReason, "overCapacityReason");
            a.Status = AllocationStatus.Confirmed; a.ConfirmedBy = access.Me.Id; a.ConfirmedAt = clock.GetUtcNow();
            a.OverCapacityReason = days.Any(d => d.OverByHours > 0) ? body.OverCapacityReason!.Trim() : null;
            a.ConfirmationSnapshot = JsonSerializer.Serialize(days, JsonOpts.Web);
            await TouchDates(db, person.Id, days.Select(d => d.Date));
            db.Audit.Note(a, reason: a.OverCapacityReason is null ? null : "Over-capacity reason recorded");
            await NotifyChanged(db, notify, project, a);
            return a;
        });

    static async Task<object> Availability(Guid personId, DateOnly from, DateOnly through, HubDb db, Access access)
    {
        Check.That(from != default && through >= from && through.DayNumber - from.DayNumber <= 366, "through", "error.date_range");
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
        if (!(access.Actor.Id == personId || access.Actor.Admin || (access.Actor.Supervisor && person.SupervisorId == access.Actor.Id)))
            throw ApiException.NotFound();
        return await db.AvailabilityOverrides.AsNoTracking().Where(x => x.PersonId == personId && x.WorkDate >= from && x.WorkDate <= through)
            .OrderBy(x => x.WorkDate).Select(x => new { x.WorkDate, x.AvailableHours, x.Category, x.RowVersion }).ToListAsync();
    }

    static async Task<object> SetAvailability(Guid personId, DateOnly date, AvailabilityBody body, HubDb db, Access access)
    {
        Check.That(date != default, "date", "error.required");
        Check.That(body.AvailableHours is >= 0 and <= 24, "availableHours", "error.invalid");
        Check.OneOf(body.Category, AvailabilityCategory.All, "category");
        return await Tx.Run(db, async () =>
        {
            // A person row serializes availability and confirmations across projects.
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.app_user WHERE id = {0} FOR UPDATE", personId).ToListAsync();
            var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.ActOnStaff(access.Actor, person.SupervisorId));
            var row = await db.AvailabilityOverrides.SingleOrDefaultAsync(x => x.PersonId == personId && x.WorkDate == date);
            var existing = row is not null;
            if (row is null)
            {
                if (body.ExpectedRowVersion != 0) throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                row = new PersonAvailabilityOverride { PersonId = personId, WorkDate = date, RowVersion = 1 };
                db.AvailabilityOverrides.Add(row);
            }
            else Coordination.Version(row, body.ExpectedRowVersion);
            row.AvailableHours = body.AvailableHours; row.Category = body.Category;
            if (existing) db.Entry(row).Property(x => x.AvailableHours).IsModified = true;
            var version = await db.PersonDateVersions.SingleOrDefaultAsync(v => v.PersonId == personId && v.WorkDate == date);
            if (version is null) db.PersonDateVersions.Add(new PersonDateVersion { PersonId = personId, WorkDate = date, RowVersion = 1 });
            else db.Entry(version).Property(v => v.WorkDate).IsModified = true;
            await db.SaveChangesAsync();
            return new { row.WorkDate, row.AvailableHours, row.Category, row.RowVersion };
        });
    }

    static IQueryable<ResourceAllocation> Query(Guid projectId, Filter filter, Access access, HubDb db, IQueryable<Guid> managedProjectIds)
    {
        var q = VisibleQuery(db, access.Actor, managedProjectIds).Where(a => a.ProjectId == projectId);
        if (filter.PersonId is { } personId) q = q.Where(a => a.PersonId == personId);
        if (!string.IsNullOrWhiteSpace(filter.Purpose)) q = q.Where(a => a.Purpose == filter.Purpose);
        if (!string.IsNullOrWhiteSpace(filter.Status)) q = q.Where(a => a.Status == filter.Status);
        if (filter.From is { } from) q = q.Where(a => a.ThroughDate >= from);
        if (filter.To is { } to) q = q.Where(a => a.FromDate <= to);
        if (!string.IsNullOrWhiteSpace(filter.Q))
        {
            var term = $"%{SearchEndpoints.Escape(filter.Q.Trim())}%";
            q = q.Where(a => EF.Functions.ILike(a.Purpose, term, @"\") || EF.Functions.ILike(a.Status, term, @"\")
                || db.Users.Any(u => u.Id == a.PersonId && EF.Functions.ILike(u.DisplayName, term, @"\")));
        }
        return q;
    }

    static IQueryable<AllocationRow> Rows(Guid projectId, Filter filter, Access access, HubDb db, IQueryable<Guid> managedProjectIds) =>
        Query(projectId, filter, access, db, managedProjectIds).OrderBy(a => a.FromDate).ThenBy(a => a.Id)
            .Select(a => new AllocationRow(a.Id, a.PersonId, db.Users.Where(u => u.Id == a.PersonId).Select(u => u.DisplayName).FirstOrDefault(),
                a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, a.Status, a.RowVersion));

    static async Task<object> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        Check.That(!filter.From.HasValue || !filter.To.HasValue || filter.To >= filter.From, "to", "error.date_range");
        var managedProjects = Permissions.IsPM(access.Actor, ctx)
            ? db.Projects.Where(p => p.Id == projectId).Select(p => p.Id)
            : db.Projects.Where(_ => false).Select(p => p.Id);
        var q = Rows(projectId, filter, access, db, managedProjects);
        var (pg, size) = Http.Paging(page, pageSize);
        return new Page<AllocationRow>(await q.Skip((pg - 1) * size).Take(size).ToListAsync(), pg, size, await q.CountAsync());
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access,
        HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, ctx) = await access.Project(projectId, false);
        Check.That(!filter.From.HasValue || !filter.To.HasValue || filter.To >= filter.From, "to", "error.date_range");
        var managedProjects = Permissions.IsPM(access.Actor, ctx)
            ? db.Projects.Where(p => p.Id == projectId).Select(p => p.Id)
            : db.Projects.Where(_ => false).Select(p => p.Id);
        var rows = await Rows(projectId, filter, access, db, managedProjects).Take(Export.MaxRows + 1).ToListAsync();
        return await ExportFile.Send(db, store, format, $"Allocations {project.ProjectNumber}", ExportColumns,
            JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id,
            $"{project.ProjectNumber}-allocations", clock);
    }

    static async Task<object> ReviewOptions(Guid projectId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
        return await db.DisciplineReviews.AsNoTracking().Where(r => r.ProjectId == projectId
            && r.Status != DisciplineReviewStatus.Approved
            && db.ReviewPackages.Any(p => p.ProjectId == projectId && p.CurrentRoundId == r.RoundId
                && p.Status != ReviewStatus.Cancelled && p.Status != ReviewStatus.Superseded && p.Status != ReviewStatus.Approved))
            .OrderBy(r => r.DueDate).Select(r => new { r.Id, r.ReviewerId, r.DueDate,
                PackageId = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Id).FirstOrDefault(),
                PackageKey = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Key).FirstOrDefault(),
                PackageTitle = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Title).FirstOrDefault() })
            .ToListAsync();
    }

    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (project, ctx) = await access.Project(projectId, false);
        var actor = access.Actor;
        var manager = Permissions.IsPM(actor, ctx);
        var a = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == id &&
            (manager || a.CreatedBy == actor.Id || ((actor.Supervisor || actor.Admin) &&
                db.Users.Any(u => u.Id == a.PersonId && (actor.Admin || u.SupervisorId == actor.Id))))) ?? throw ApiException.NotFound();
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.PersonId);
        var personEligible = person is not null && person.IsActive && await Coordination.People(db, project).AnyAsync(u => u.Id == a.PersonId);
        var personState = person is null ? "Missing" : !person.IsActive ? "Inactive" : personEligible ? "Eligible" : "Removed";
        var canConfirm = a.Status == AllocationStatus.Proposed && personEligible && Permissions.ConfirmAllocation(actor, ctx, person.SupervisorId).Ok;
        var canManage = Permissions.ProposeAllocation(actor, ctx).Ok && (manager || a.CreatedBy == actor.Id);
        return new { a.Id, a.ProjectId, a.PersonId, PersonName = person?.DisplayName, PersonState = personState, PersonEligible = personEligible,
            a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, a.Status,
            a.ConfirmedBy, a.ConfirmedAt, OverCapacityWarningRecorded = a.OverCapacityReason != null, a.RowVersion, CanConfirm = canConfirm, CanManage = canManage,
            Days = await db.AllocationDayOverrides.AsNoTracking().Where(d => d.AllocationId == id).OrderBy(d => d.WorkDate)
                .Select(d => new { d.WorkDate, d.Hours }).ToListAsync(),
            Links = await db.AllocationWorkLinks.AsNoTracking().Where(l => l.AllocationId == id && l.ReleasedAt == null)
                .OrderBy(l => l.WorkDate).Select(l => new { l.WorkType, l.WorkId, l.WorkDate, l.ReviewHours,
                    ReviewPackageId = l.WorkType == "Review" ? db.ReviewPackages.Where(p => db.DisciplineReviews.Any(r => r.Id == l.WorkId && r.RoundId == p.CurrentRoundId))
                        .Select(p => (Guid?)p.Id).FirstOrDefault() : null }).ToListAsync() };
    }

    static async Task Validate(HubDb db, Project p, Guid personId, string purpose, DateOnly from, DateOnly through,
        decimal hours, DayInput[] days, LinkInput[] links, SettingsStore store)
    {
        await Coordination.Person(db, p, personId, "personId");
        Check.OneOf(purpose, AllocationPurpose.All, "purpose");
        Check.That(from != default && through >= from && through.DayNumber - from.DayNumber <= 366, "throughDate", "error.date_range");
        Check.That(hours > 0 && hours <= 10000, "plannedHours", "error.positive");
        Check.That(days is { Length: <= 366 } && days.All(d => d.WorkDate >= from && d.WorkDate <= through && d.Hours >= 0)
            && days.Select(d => d.WorkDate).Distinct().Count() == days.Length, "days", "error.invalid");
        Check.That(links is { Length: > 0 and <= 500 } && links.All(l => l.WorkDate >= from && l.WorkDate <= through)
            && links.Select(l => (l.WorkType, l.WorkId, l.WorkDate)).Distinct().Count() == links.Length, "links", "error.invalid");
        var settings = await store.Get(db);
        var office = await db.Users.Where(u => u.Id == personId).Select(u => u.OfficeId).SingleAsync();
        var holidays = settings.WorkingDaysEnabled
            ? await db.Holidays.Where(h => h.OfficeId == null || h.OfficeId == office).Select(h => h.Date).ToListAsync() : [];
        var calendar = settings.WorkingDaysEnabled ? new WorkCalendar(holidays) : WorkCalendar.Weekdays;
        try { AllocationRules.Spread(from, through, hours, calendar, days.ToDictionary(d => d.WorkDate, d => d.Hours)); }
        catch (ArgumentException) { throw ApiException.Invalid("days", "error.invalid"); }
        foreach (var link in links)
        {
            Check.That(link.WorkId != Guid.Empty, "links", "error.invalid");
            if (link.WorkType == "Task")
            {
                Check.That(purpose == AllocationPurpose.Production && link.ReviewHours is null && await db.Tasks.AnyAsync(t => t.Id == link.WorkId
                    && t.ProjectId == p.Id && t.AssigneeId == personId
                    && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold
                    && t.DeletedAt == null),
                    "links", "coord.reference");
            }
            else if (link.WorkType == "Review")
            {
                Check.That(purpose == AllocationPurpose.Review && link.ReviewHours is > 0 and <= 10000 && await db.DisciplineReviews.AnyAsync(r => r.Id == link.WorkId
                    && r.ProjectId == p.Id && r.ReviewerId == personId && r.Status != DisciplineReviewStatus.Approved && db.ReviewPackages.Any(pkg =>
                        pkg.ProjectId == p.Id && pkg.CurrentRoundId == r.RoundId
                        && pkg.Status != ReviewStatus.Cancelled && pkg.Status != ReviewStatus.Superseded && pkg.Status != ReviewStatus.Approved)),
                    "links", "coord.reference");
            }
            else throw ApiException.Invalid("links", "error.invalid");
        }
    }

    static Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.create", body }, access, db, clock, async (project, ctx) =>
        {
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate,
                body.PlannedHours, body.Days, body.Links, store);
            var a = new ResourceAllocation { ProjectId = project.Id, PersonId = body.PersonId, Purpose = body.Purpose,
                FromDate = body.FromDate, ThroughDate = body.ThroughDate, PlannedHours = body.PlannedHours };
            db.Allocations.Add(a);
            foreach (var day in body.Days)
                db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = a.Id, WorkDate = day.WorkDate, Hours = day.Hours });
            foreach (var link in body.Links)
                db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = a.Id, PersonId = a.PersonId,
                    WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate, ReviewHours = link.ReviewHours });
            db.Audit.Note(a, reason: body.Reason);
            await NotifyChanged(db, notify, project, a);
            return a;
        });

    static Task<Coordination.Result> Edit(Guid projectId, Guid id, EditBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.edit", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || a.CreatedBy == access.Actor.Id
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(a.Status is AllocationStatus.Proposed or AllocationStatus.Confirmed, "status", "error.invalid");
            var wasConfirmed = a.Status == AllocationStatus.Confirmed;
            var priorPerson = a.PersonId; var priorFrom = a.FromDate; var priorThrough = a.ThroughDate;
            if (wasConfirmed) await LockPerson(db, priorPerson);
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate,
                body.PlannedHours, body.Days, body.Links, store);
            var oldLinks = await db.AllocationWorkLinks.Where(l => l.AllocationId == a.Id && l.ReleasedAt == null).ToListAsync();
            var oldDays = await db.AllocationDayOverrides.Where(d => d.AllocationId == a.Id).ToListAsync();
            var now = clock.GetUtcNow();
            foreach (var link in oldLinks) link.ReleasedAt = now;
            db.AllocationDayOverrides.RemoveRange(oldDays);
            // Free unique person/work/day slices before replacement, within Coordination.Run's transaction.
            await db.SaveChangesAsync();
            foreach (var day in body.Days)
                db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = a.Id, WorkDate = day.WorkDate, Hours = day.Hours });
            foreach (var link in body.Links)
                db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = a.Id, PersonId = body.PersonId,
                    WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate, ReviewHours = link.ReviewHours });
            a.PersonId = body.PersonId; a.Purpose = body.Purpose; a.FromDate = body.FromDate; a.ThroughDate = body.ThroughDate;
            a.PlannedHours = body.PlannedHours; a.Status = AllocationRules.AfterMaterialEdit(a.Status);
            if (wasConfirmed) await TouchDates(db, priorPerson, Enumerable.Range(0, priorThrough.DayNumber - priorFrom.DayNumber + 1)
                .Select(i => priorFrom.AddDays(i)));
            // A child-only edit must invalidate the allocation version seen by another editor.
            db.Entry(a).Property(x => x.PlannedHours).IsModified = true;
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await NotifyChanged(db, notify, project, a);
            return a;
        });

    static Task<Coordination.Result> Cancel(Guid projectId, Guid id, CancelBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.cancel", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || a.CreatedBy == access.Actor.Id
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(AllocationRules.Step(a.Status, AllocationStatus.Cancelled), "status", "error.invalid");
            var wasConfirmed = a.Status == AllocationStatus.Confirmed;
            if (wasConfirmed) await LockPerson(db, a.PersonId);
            a.Status = AllocationStatus.Cancelled;
            foreach (var link in await db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToListAsync())
                link.ReleasedAt = clock.GetUtcNow();
            if (wasConfirmed) await TouchDates(db, a.PersonId, Enumerable.Range(0, a.ThroughDate.DayNumber - a.FromDate.DayNumber + 1)
                .Select(i => a.FromDate.AddDays(i)));
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await NotifyChanged(db, notify, project, a);
            return a;
        });

    static Task<Coordination.Result> Decline(Guid projectId, Guid id, CancelBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.decline", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.PersonId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.ConfirmAllocation(access.Actor, ctx, person.SupervisorId));
            Check.That(AllocationRules.Step(a.Status, AllocationStatus.Declined), "status", "error.invalid");
            a.Status = AllocationStatus.Declined;
            foreach (var link in await db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToListAsync())
                link.ReleasedAt = clock.GetUtcNow();
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await NotifyChanged(db, notify, project, a);
            return a;
        });

    static Task<Coordination.Result> Complete(Guid projectId, Guid id, CancelBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.complete", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || a.CreatedBy == access.Actor.Id
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(AllocationRules.Step(a.Status, AllocationStatus.Completed), "status", "error.invalid");
            await LockPerson(db, a.PersonId);
            a.Status = AllocationStatus.Completed;
            foreach (var link in await db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToListAsync())
                link.ReleasedAt = clock.GetUtcNow();
            await TouchDates(db, a.PersonId, Enumerable.Range(0, a.ThroughDate.DayNumber - a.FromDate.DayNumber + 1)
                .Select(i => a.FromDate.AddDays(i)));
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await NotifyChanged(db, notify, project, a);
            return a;
        });
}
