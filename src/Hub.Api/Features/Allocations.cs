using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class AllocationEndpoints
{
    public sealed record DayInput(DateOnly WorkDate, decimal Hours);
    public sealed record LinkInput(string WorkType, Guid WorkId, DateOnly WorkDate, decimal? ReviewHours = null);
    public sealed record CreateBody(Guid RequestId, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string? Reason, Guid? OpenRoleId = null);
    public sealed record EditBody(Guid RequestId, int RowVersion, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string Reason);
    public sealed record CancelBody(Guid RequestId, int RowVersion, string Reason);
    public sealed record AvailabilityBody(int ExpectedRowVersion, decimal AvailableHours, string Category);
    public sealed record DateVersionInput(DateOnly WorkDate, int RowVersion);
    public sealed record ConfirmBody(Guid RequestId, int RowVersion, DateVersionInput[] DateVersions, string? OverCapacityReason);
    public sealed record CapacityDay(DateOnly Date, decimal AvailableHours, decimal ConfirmedHours,
        decimal ProposedHours, decimal ResultingHours, decimal OverByHours, int DateVersion);
    public sealed record Filter(string? Q, Guid? PersonId, string? Purpose, string? Status, DateOnly? From, DateOnly? To);
    /// PersonState is Eligible, Inactive, Removed (no longer on the project) or Missing.
    public sealed record AllocationRow(Guid Id, Guid PersonId, string? PersonName, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, string Status, int RowVersion, string PersonState);
    public sealed record AllocationDay(DateOnly WorkDate, decimal Hours);
    /// A task or review the allocation reserves time for; the review hours and package are set only for a review.
    public sealed record AllocationLink(string WorkType, Guid WorkId, DateOnly WorkDate, decimal? ReviewHours, Guid? ReviewPackageId);
    /// The confirmation fields are null until the allocation has been confirmed; a confirmed record that is edited keeps them.
    public sealed record AllocationDetail(Guid Id, Guid ProjectId, Guid PersonId, string? PersonName, string PersonState, bool PersonEligible, string Purpose,
        DateOnly FromDate, DateOnly ThroughDate, decimal PlannedHours, string Status, Guid? ConfirmedBy, DateTimeOffset? ConfirmedAt,
        bool OverCapacityWarningRecorded, int RowVersion, bool CanConfirm, bool CanManage, List<AllocationDay> Days, List<AllocationLink> Links);
    public sealed record ConfirmationPreviewRead(Guid Id, int RowVersion, List<CapacityDay> Days);
    public sealed record ReviewOptionRow(Guid Id, Guid ReviewerId, DateOnly DueDate, Guid PackageId, string PackageKey, string PackageTitle);
    public sealed record DayAvailabilityRow(DateOnly WorkDate, decimal AvailableHours, string Category, int RowVersion);
    sealed record PersonContact(string DisplayName, Guid? SupervisorId);

    internal static IQueryable<ResourceAllocation> VisibleQuery(HubDb db, Actor actor, IQueryable<Guid> managedProjectIds) =>
        db.Allocations.AsNoTracking().Where(a => managedProjectIds.Contains(a.ProjectId) || a.CreatedBy == actor.Id || ((actor.Supervisor || actor.Admin)
            && db.Users.Any(u => u.Id == a.PersonId && (actor.Admin || u.SupervisorId == actor.Id))));

    static readonly Col[] ExportColumns = Hub.Api.Records.ResourceAllocationRecord.AllocationsExportColumns;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/allocations", List);
        api.MapGet("/projects/{projectId:guid}/allocations/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/allocations/review-options", ReviewOptions);
        api.MapGet("/projects/{projectId:guid}/allocations/deadlines", Deadlines);
        api.MapGet("/projects/{projectId:guid}/allocations/discipline-context", DisciplineContext);
        api.MapPost("/projects/{projectId:guid}/allocations/capacity-preview", ProposalCapacity);
        api.MapPost("/projects/{projectId:guid}/allocations/shift-preview", ShiftPreview).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/shift", Shift).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/projects/{projectId:guid}/allocations/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/allocations", Create).AddEndpointFilter<AllocationPreviewFilter>().AddOpenApiOperationTransformer(AllocationPreviewFilter.Describe).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/edit", Edit).AddEndpointFilter<AllocationPreviewFilter>().AddOpenApiOperationTransformer(AllocationPreviewFilter.Describe).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/cancel", Cancel).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/decline", Decline).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/complete", Complete).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/users/{personId:guid}/availability", Availability);
        api.MapPut("/users/{personId:guid}/availability/{date}", SetAvailability);
        api.MapGet("/projects/{projectId:guid}/allocations/{id:guid}/confirmation-preview", ConfirmationPreview);
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/confirm", Confirm).AddEndpointFilter<AllocationPreviewFilter>().AddOpenApiOperationTransformer(AllocationPreviewFilter.Describe).WithMetadata(new Coordination.AtomicCommand());
    }

    static async Task NotifyChanged(HubDb db, Notifier notify, Project project, ResourceAllocation a)
    {
        var person = await db.Users.Where(u => u.Id == a.PersonId)
            .Select(u => new PersonContact(u.DisplayName, u.SupervisorId)).FirstAsync();
        await notify.Send(NotificationEvents.AllocationChanged,
            [project.ProjectManagerId, a.CreatedBy, person.SupervisorId],
            new NotifyItem(project.Id, "ResourceAllocation", a.Id, null,
                $"/projects/{project.ProjectNumber}/allocations?allocation={a.Id}", project.ProjectNumber),
            Text.Get("notify.allocation_changed", person.DisplayName, a.Status));
    }

    static async Task LockPerson(HubDb db, Guid personId) =>
        await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.app_user WHERE id = {0} FOR UPDATE", personId).ToListAsync();

    /// FR-ROLE-03 (D1): the named open role is locked, then must be Open, on this project, and cover the allocation's dates.
    static async Task<OpenRole> LockOpenRole(HubDb db, Guid projectId, Guid roleId, DateOnly from, DateOnly through)
    {
        await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.open_role WHERE id = {0} FOR UPDATE", roleId).ToListAsync();
        var role = await db.OpenRoles.FirstOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId);
        Check.That(role is not null, "openRoleId", "coord.reference");
        if (role!.Status == OpenRoleStatus.Filled) throw ApiException.Conflict("open_role_filled", "openRole.filled");
        Check.That(role.Status == OpenRoleStatus.Open, "openRoleId", "openRole.not_open");
        Check.That(from >= role.FromDate && through <= role.ThroughDate, "openRoleId", "openRole.range");
        return role;
    }

    /// FR-ROLE-03 (D1): a declined or cancelled allocation returns the role it filled to Open.
    static async Task ReopenRole(HubDb db, Guid allocationId)
    {
        if (await db.OpenRoles.FirstOrDefaultAsync(r => r.FilledAllocationId == allocationId) is { } role)
        { role.Status = OpenRoleStatus.Open; role.FilledAllocationId = null; }
    }

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

    internal static async Task<List<CapacityDay>> Capacity(HubDb db, Access access, ResourceAllocation candidate,
        SettingsStore store, TimeProvider clock, DateOnly? viewFrom = null, DateOnly? viewThrough = null,
        DayInput[]? candidateDays = null, LinkInput[]? candidateLinks = null)
    {
        var settings = await store.Get(db);
        var first = viewFrom ?? candidate.FromDate; var last = viewThrough ?? candidate.ThroughDate;
        var today = clock.Today(settings);
        var person = await db.Users.AsNoTracking().SingleAsync(u => u.Id == candidate.PersonId);
        var confirmed = await db.Allocations.AsNoTracking().Where(a => a.PersonId == person.Id && a.Id != candidate.Id
            && a.Status == AllocationStatus.Confirmed && a.FromDate <= last && a.ThroughDate >= first).ToListAsync();
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
        // Pure preview drafts override only the hypothetical candidate's inputs. Persisted commands keep their original read path.
        if (candidateDays is not null)
        {
            overrides.RemoveAll(day => day.AllocationId == candidate.Id);
            overrides.AddRange(candidateDays.Select(day => new AllocationDayOverride { AllocationId = candidate.Id, WorkDate = day.WorkDate, Hours = day.Hours }));
        }
        if (candidateLinks is not null)
        {
            links.RemoveAll(link => link.AllocationId == candidate.Id);
            links.AddRange(candidateLinks.Select(link => new AllocationWorkLink { AllocationId = candidate.Id, PersonId = candidate.PersonId,
                WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate, ReviewHours = link.ReviewHours }));
        }
        var personCalendar = (await PersonCapacity.Calendars(db, [person.Id], new[] { last, all.Max(a => a.ThroughDate) }.Max()))[person.Id];
        var reservations = new Dictionary<Guid, IReadOnlyDictionary<DateOnly, decimal>>();
        foreach (var a in all)
        {
            var dayHours = overrides.Where(d => d.AllocationId == a.Id).ToDictionary(d => d.WorkDate, d => d.Hours);
            try { reservations[a.Id] = a.PlannedHours == 0 ? new Dictionary<DateOnly, decimal>() : AllocationRules.Spread(a.FromDate, a.ThroughDate, a.PlannedHours, personCalendar, dayHours); }
            catch (ArgumentException) { throw ApiException.Conflict("allocation_calendar_changed", "coord.stale"); }
        }
        var capacityOverrides = await PersonCapacity.Load(db, [person.Id], first, last, settings);
        var versions = await db.PersonDateVersions.AsNoTracking().Where(v => v.PersonId == person.Id
            && v.WorkDate >= first && v.WorkDate <= last).ToDictionaryAsync(v => v.WorkDate, v => v.RowVersion);
        var result = new List<CapacityDay>();
        for (var date = first; date <= last; date = date.AddDays(1))
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
            var available = capacityOverrides[(person.Id, date)].Available;
            result.Add(new CapacityDay(date, available, existing, candidate.Status == AllocationStatus.Proposed
                ? Math.Max(demands[^1].ReservedHours, demands[^1].LinkedRemainingHours) : 0, resulting,
                Math.Max(0, resulting - available), versions.GetValueOrDefault(date)));
        }
        return result;
    }

    static async Task<Ok<ConfirmationPreviewRead>> ConfirmationPreview(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, ctx) = await access.Project(projectId, false);
        var a = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == id) ?? throw ApiException.NotFound();
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == a.PersonId);
        Check.That(person is not null && person.IsActive && await Coordination.People(db, project).AnyAsync(u => u.Id == a.PersonId), "personId", "coord.person");
        var supervisorId = person!.SupervisorId;
        Access.Demand(Permissions.ConfirmAllocation(access.Actor, ctx, supervisorId));
        Check.That(a.Status == AllocationStatus.Proposed, "status", "error.invalid");
        await ValidateSaved(db, project, a, store);
        return TypedResults.Ok(new ConfirmationPreviewRead(a.Id, a.RowVersion, await Capacity(db, access, a, store, clock)));
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

    static async Task<Ok<List<DayAvailabilityRow>>> Availability(Guid personId, DateOnly from, DateOnly through, HubDb db, Access access)
    {
        Check.That(from != default && through >= from && through.DayNumber - from.DayNumber <= 366, "through", "error.date_range");
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
        if (!(access.Actor.Id == personId || access.Actor.Admin || (access.Actor.Supervisor && person.SupervisorId == access.Actor.Id)))
            throw ApiException.NotFound();
        return TypedResults.Ok(await db.AvailabilityOverrides.AsNoTracking().Where(x => x.PersonId == personId && x.WorkDate >= from && x.WorkDate <= through)
            .OrderBy(x => x.WorkDate).Select(x => new DayAvailabilityRow(x.WorkDate, x.AvailableHours, x.Category, x.RowVersion)).ToListAsync());
    }

    static async Task<Ok<DayAvailabilityRow>> SetAvailability(Guid personId, DateOnly date, AvailabilityBody body, HubDb db, Access access)
    {
        Check.That(date != default, "date", "error.required");
        Check.That(body.AvailableHours is >= 0 and <= 24, "availableHours", "error.invalid");
        Check.OneOf(body.Category, AvailabilityCategory.All, "category");
        return TypedResults.Ok(await Tx.Run(db, async () =>
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
            return new DayAvailabilityRow(row.WorkDate, row.AvailableHours, row.Category, row.RowVersion);
        }));
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

    static IQueryable<AllocationRow> Rows(Project project, Filter filter, Access access, HubDb db, IQueryable<Guid> managedProjectIds)
    {
        var eligible = Coordination.People(db, project);
        return Query(project.Id, filter, access, db, managedProjectIds).OrderBy(a => a.FromDate).ThenBy(a => a.Id)
            .Select(a => new AllocationRow(a.Id, a.PersonId, db.Users.Where(u => u.Id == a.PersonId).Select(u => u.DisplayName).FirstOrDefault(),
                a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, a.Status, a.RowVersion,
                !db.Users.Any(u => u.Id == a.PersonId) ? "Missing" : !db.Users.Any(u => u.Id == a.PersonId && u.IsActive) ? "Inactive"
                    : eligible.Any(u => u.Id == a.PersonId) ? "Eligible" : "Removed"));
    }

    /// S30 baseline while inputs are incomplete, or canonical proposal comparison when §52 preview is off. Never persists a draft.
    static async Task<Ok<AllocationScreenReads.ProposalCapacityRead>> ProposalCapacity(Guid projectId, AllocationScreenReads.ProposalCapacityBody body,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var (project, context) = await access.Project(projectId, false);
        Access.Demand(Permissions.ProposeAllocation(access.Actor, context));
        Check.That(body.FromDate != default && body.ThroughDate >= body.FromDate && body.ThroughDate.DayNumber - body.FromDate.DayNumber <= 366,
            "throughDate", "error.date_range");
        if (!await AllocationScreenReads.InPlannerScope(body.PersonId, access, db))
            return TypedResults.Ok(AllocationScreenReads.OutsideScope(body.FromDate, body.ThroughDate, clock));
        await Coordination.Person(db, project, body.PersonId, "personId");
        if (body.IncludeProposal)
        {
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate, body.PlannedHours, body.Days, body.Links, store);
            // The command's active-link unique index would reject these inputs. Do not preview a second reservation of the same work slice.
            var reserved = await db.AllocationWorkLinks.AsNoTracking().Where(link => link.PersonId == body.PersonId && link.ReleasedAt == null
                && link.WorkDate >= body.FromDate && link.WorkDate <= body.ThroughDate)
                .Select(link => new ReservedSlice(link.WorkType, link.WorkId, link.WorkDate)).ToArrayAsync();
            Check.That(!body.Links.Any(link => reserved.Any(existing => existing.WorkType == link.WorkType && existing.WorkId == link.WorkId && existing.WorkDate == link.WorkDate)),
                "links", "error.invalid");
        }
        var candidate = new ResourceAllocation { ProjectId = projectId, PersonId = body.PersonId, Purpose = body.Purpose,
            FromDate = body.FromDate, ThroughDate = body.ThroughDate, PlannedHours = body.IncludeProposal ? body.PlannedHours : 0 };
        return TypedResults.Ok(await AllocationScreenReads.Proposal(candidate, body.IncludeProposal, access, db, store, clock,
            body.IncludeProposal ? body.Days : [], body.IncludeProposal ? body.Links : []));
    }

    static async Task<Ok<AllocationScreenReads.DisciplineComparison>> DisciplineContext(Guid projectId, DateOnly? from, int? weeks,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var (_, context) = await access.Project(projectId, false);
        Check.That(weeks is null or (>= 1 and <= 26), "weeks", "error.range");
        var first = Workload.WeekOf(from ?? clock.Today(settings));
        var window = Enumerable.Range(0, weeks ?? settings.PlanningHorizonWeeks).Select(index => first.AddDays(index * 7)).ToArray();
        var isPm = Permissions.IsPM(access.Actor, context);
        var managed = db.Projects.Where(project => project.Id == projectId && isPm).Select(project => project.Id);
        var visible = VisibleQuery(db, access.Actor, managed).Where(allocation => allocation.ProjectId == projectId);
        return TypedResults.Ok(await AllocationScreenReads.Compare(projectId, window, visible, isPm || access.Actor.Admin, db, clock));
    }

    static async Task<Ok<PlanningDeadlines.PlanningDeadlineContext>> Deadlines(Guid projectId, DateOnly? from, int? weeks,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        await access.Project(projectId, false);
        Check.That(weeks is null or (>= 1 and <= 26), "weeks", "error.range");
        var first = Workload.WeekOf(from ?? clock.Today(settings));
        var window = Enumerable.Range(0, weeks ?? settings.PlanningHorizonWeeks).Select(i => first.AddDays(i * 7)).ToArray();
        return TypedResults.Ok(await PlanningDeadlines.Read([projectId], window, access, db, clock));
    }

    static async Task<Ok<Page<AllocationRow>>> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db)
    {
        var (project, ctx) = await access.Project(projectId, false);
        Check.That(!filter.From.HasValue || !filter.To.HasValue || filter.To >= filter.From, "to", "error.date_range");
        var managedProjects = Permissions.IsPM(access.Actor, ctx)
            ? db.Projects.Where(p => p.Id == projectId).Select(p => p.Id)
            : db.Projects.Where(_ => false).Select(p => p.Id);
        var q = Rows(project, filter, access, db, managedProjects);
        var (pg, size) = Http.Paging(page, pageSize);
        return TypedResults.Ok(new Page<AllocationRow>(await q.Skip((pg - 1) * size).Take(size).ToListAsync(), pg, size, await q.CountAsync()));
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access,
        HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, ctx) = await access.Project(projectId, false);
        Check.That(!filter.From.HasValue || !filter.To.HasValue || filter.To >= filter.From, "to", "error.date_range");
        var managedProjects = Permissions.IsPM(access.Actor, ctx)
            ? db.Projects.Where(p => p.Id == projectId).Select(p => p.Id)
            : db.Projects.Where(_ => false).Select(p => p.Id);
        var rows = await Rows(project, filter, access, db, managedProjects).Take(Export.MaxRows + 1).ToListAsync();
        return await ExportFile.Send(db, store, format, $"Allocations {project.ProjectNumber}", ExportColumns,
            JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id,
            $"{project.ProjectNumber}-allocations", clock);
    }

    static async Task<Ok<List<ReviewOptionRow>>> ReviewOptions(Guid projectId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
        return TypedResults.Ok(await db.DisciplineReviews.AsNoTracking().Where(r => r.ProjectId == projectId
            && r.Status != DisciplineReviewStatus.Approved
            && db.ReviewPackages.Any(p => p.ProjectId == projectId && p.CurrentRoundId == r.RoundId
                && p.Status != ReviewStatus.Cancelled && p.Status != ReviewStatus.Superseded && p.Status != ReviewStatus.Approved))
            .OrderBy(r => r.DueDate).Select(r => new ReviewOptionRow(r.Id, r.ReviewerId, r.DueDate,
                db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Id).FirstOrDefault(),
                db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Key).FirstOrDefault()!,
                db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Title).FirstOrDefault()!))
            .ToListAsync());
    }

    static async Task<Ok<AllocationDetail>> Detail(Guid projectId, Guid id, Access access, HubDb db)
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
        return TypedResults.Ok(new AllocationDetail(a.Id, a.ProjectId, a.PersonId, person?.DisplayName, personState, personEligible,
            a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, a.Status,
            a.ConfirmedBy, a.ConfirmedAt, a.OverCapacityReason != null, a.RowVersion, canConfirm, canManage,
            await db.AllocationDayOverrides.AsNoTracking().Where(d => d.AllocationId == id).OrderBy(d => d.WorkDate)
                .Select(d => new AllocationDay(d.WorkDate, d.Hours)).ToListAsync(),
            await db.AllocationWorkLinks.AsNoTracking().Where(l => l.AllocationId == id && l.ReleasedAt == null)
                .OrderBy(l => l.WorkDate).Select(l => new AllocationLink(l.WorkType, l.WorkId, l.WorkDate, l.ReviewHours,
                    l.WorkType == "Review" ? db.ReviewPackages.Where(p => db.DisciplineReviews.Any(r => r.Id == l.WorkId && r.RoundId == p.CurrentRoundId))
                        .Select(p => (Guid?)p.Id).FirstOrDefault() : null)).ToListAsync()));
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
        var calendar = (await PersonCapacity.Calendars(db, [personId], through))[personId];
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
            var role = body.OpenRoleId is { } roleId ? await LockOpenRole(db, project.Id, roleId, body.FromDate, body.ThroughDate) : null;
            var a = new ResourceAllocation { ProjectId = project.Id, PersonId = body.PersonId, Purpose = body.Purpose,
                FromDate = body.FromDate, ThroughDate = body.ThroughDate, PlannedHours = body.PlannedHours };
            db.Allocations.Add(a);
            if (role is not null) { role.Status = OpenRoleStatus.Filled; role.FilledAllocationId = a.Id; } // AC-ROLE-03: same transaction
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
            if (a.Status == AllocationStatus.Confirmed) await LockPerson(db, a.PersonId);
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate,
                body.PlannedHours, body.Days, body.Links, store);
            await ReleaseMaterial(db, [a.Id], clock.GetUtcNow());
            return await ApplyMaterial(project, a, body, db, notify);
        });

    /// Release all source slices together, inside the caller's existing material-edit transaction.
    static async Task ReleaseMaterial(HubDb db, Guid[] ids, DateTimeOffset now)
    {
        var oldLinks = await db.AllocationWorkLinks.Where(link => ids.Contains(link.AllocationId) && link.ReleasedAt == null).ToListAsync();
        var oldDays = await db.AllocationDayOverrides.Where(day => ids.Contains(day.AllocationId)).ToListAsync();
        foreach (var link in oldLinks) link.ReleasedAt = now;
        db.AllocationDayOverrides.RemoveRange(oldDays);
        await db.SaveChangesAsync();
    }

    /// Canonical single/bulk material replacement. Batch callers invalidate old confirmed date unions once per person.
    static async Task<ResourceAllocation> ApplyMaterial(Project project, ResourceAllocation a, EditBody body, HubDb db, Notifier notify, bool touchDates = true)
    {
        var wasConfirmed = a.Status == AllocationStatus.Confirmed;
        var priorPerson = a.PersonId; var priorFrom = a.FromDate; var priorThrough = a.ThroughDate;
        foreach (var day in body.Days)
            db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = a.Id, WorkDate = day.WorkDate, Hours = day.Hours });
        foreach (var link in body.Links)
            db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = a.Id, PersonId = body.PersonId,
                WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate, ReviewHours = link.ReviewHours });
        a.PersonId = body.PersonId; a.Purpose = body.Purpose; a.FromDate = body.FromDate; a.ThroughDate = body.ThroughDate;
        a.PlannedHours = body.PlannedHours; a.Status = AllocationRules.AfterMaterialEdit(a.Status);
        if (wasConfirmed && touchDates) await TouchDates(db, priorPerson, Enumerable.Range(0, priorThrough.DayNumber - priorFrom.DayNumber + 1)
            .Select(i => priorFrom.AddDays(i)));
        // A child-only edit must invalidate the allocation version seen by another editor.
        db.Entry(a).Property(x => x.PlannedHours).IsModified = true;
        db.Audit.Note(a, reason: Check.Reason(body.Reason));
        await NotifyChanged(db, notify, project, a);
        return a;
    }

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
            await ReopenRole(db, a.Id);
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
            await ReopenRole(db, a.Id);
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

file sealed record ReservedSlice(string WorkType, Guid WorkId, DateOnly WorkDate);
