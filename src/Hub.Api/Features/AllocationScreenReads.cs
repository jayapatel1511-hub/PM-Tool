using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// 053-S30 read projections. Allocation/estimate/capacity calculations remain in their existing canonical functions.
public static class AllocationScreenReads
{
    public sealed record ComparisonWeek(DateOnly Week, decimal? ConfirmedHours, decimal KnownConfirmedHours, int UnavailableConfirmed,
        decimal? ProposedHours, decimal KnownProposedHours, int UnavailableProposed,
        decimal? RemainingEstimateHours, decimal KnownRemainingEstimateHours, int UnestimatedTasks);
    public sealed record ComparisonDiscipline(Guid? ProjectDisciplineId, string? DisciplineName, bool Unattributed,
        int UnattributedAllocations, int UndatedTasks, ComparisonWeek[] Weeks);
    public sealed record DisciplineComparison(DateOnly[] Weeks, string AllocationCoverage, ComparisonDiscipline[] Disciplines, DateTimeOffset AsOf);
    public sealed record ProposalCapacityBody(Guid PersonId, DateOnly FromDate, DateOnly ThroughDate, bool IncludeProposal,
        string Purpose, decimal PlannedHours, AllocationEndpoints.DayInput[] Days, AllocationEndpoints.LinkInput[] Links);
    public sealed record ProposalCapacityWeek(DateOnly Week, decimal? AvailableHours, decimal? BeforeHours, decimal? AfterHours,
        bool? OverCapacity, bool Provisional);
    public sealed record ProposalCapacityRead(Guid? PersonId, DateOnly FromDate, DateOnly ThroughDate, string State,
        bool IncludesProposal, int? UnestimatedTasks, int? UndatedTasks, ProposalCapacityWeek[] Weeks, DateTimeOffset AsOf);

    internal static async Task<bool> InPlannerScope(Guid personId, Access access, HubDb db)
        => await (await PeopleScope.Widen(db, access.Actor, PlanningEndpoints.People(db, access))).AnyAsync(person => person.Id == personId);

    internal static ProposalCapacityRead OutsideScope(DateOnly from, DateOnly through, TimeProvider clock)
        => new(null, from, through, "OutsideScope", false, null, null, [], clock.GetUtcNow());

    /// Uses the confirmation calculation, with only an optional in-memory candidate overlay. No alternate load formula or write.
    internal static async Task<ProposalCapacityRead> Proposal(ResourceAllocation candidate, bool includeProposal, Access access, HubDb db,
        SettingsStore store, TimeProvider clock, AllocationEndpoints.DayInput[]? days = null, AllocationEndpoints.LinkInput[]? links = null)
    {
        if (!await InPlannerScope(candidate.PersonId, access, db)) return OutsideScope(candidate.FromDate, candidate.ThroughDate, clock);
        var settings = await store.Get(db);
        var first = Workload.WeekOf(candidate.FromDate); var last = Workload.WeekOf(candidate.ThroughDate).AddDays(6);
        var dates = Enumerable.Range(0, (last.DayNumber - first.DayNumber + 1) / 7).Select(index => first.AddDays(index * 7)).ToArray();
        var availability = await PersonCapacity.Load(db, [candidate.PersonId], first, last, settings);
        List<AllocationEndpoints.CapacityDay>? capacity = null; var state = "Complete";
        try { capacity = await AllocationEndpoints.Capacity(db, access, candidate, store, clock, first, last, days, links); }
        catch (ApiException error) when (error.Status == 403) { state = "Partial"; }
        catch (ApiException error) when (error.Code == "allocation_calendar_changed") { state = "Unavailable"; }
        int? unestimated = null; int? undated = null;
        if (capacity is not null)
        {
            var taskInputs = await db.Tasks.AsNoTracking().Where(task => task.AssigneeId == candidate.PersonId && task.DeletedAt == null
                && task.Status != TaskStatuses.Complete && task.Status != TaskStatuses.Cancelled && task.Status != TaskStatuses.OnHold
                && db.Projects.Any(project => project.Id == task.ProjectId && (project.Status == ProjectStatus.Setup || project.Status == ProjectStatus.Active)))
                .Select(task => new TaskEstimateInput(task.EstimatedHours, task.DueDate)).ToArrayAsync();
            unestimated = taskInputs.Count(task => task.EstimatedHours is null); undated = taskInputs.Count(task => task.DueDate is null);
            if (unestimated > 0 || undated > 0) state = "Incomplete";
        }
        var weeks = dates.Select(week => {
            var inputs = Enumerable.Range(0, 7).Select(index => availability[(candidate.PersonId, week.AddDays(index))]).ToArray();
            var provisional = inputs.Any(day => day.Provisional);
            var daily = capacity?.Where(day => Workload.WeekOf(day.Date) == week).ToArray();
            // A known daily overload remains useful with incomplete estimates, but false must never claim a complete assessment.
            bool? over = daily is null || !includeProposal || provisional ? null : daily.Any(day => day.OverByHours > 0) ? true : state == "Complete" ? false : null;
            return new ProposalCapacityWeek(week, provisional ? null : inputs.Sum(day => day.Available), daily?.Sum(day => day.ConfirmedHours),
                includeProposal ? daily?.Sum(day => day.ResultingHours) : null, over, provisional);
        }).ToArray();
        if (state == "Complete" && weeks.Any(week => week.Provisional)) state = "Provisional";
        return new(candidate.PersonId, candidate.FromDate, candidate.ThroughDate, state, includeProposal, unestimated, undated, weeks, clock.GetUtcNow());
    }

    sealed record WorkDiscipline(Guid Id, Guid ProjectDisciplineId, Guid? PersonId);
    sealed class Bucket(int weeks)
    {
        public decimal[] Confirmed { get; } = new decimal[weeks];
        public decimal[] Proposed { get; } = new decimal[weeks];
        public decimal[] Remaining { get; } = new decimal[weeks];
        public int[] UnavailableConfirmed { get; } = new int[weeks];
        public int[] UnavailableProposed { get; } = new int[weeks];
        public int[] Unestimated { get; } = new int[weeks];
        public int Allocations; public int Undated;
    }

    sealed record ReservationDiscipline(Guid Id, bool IsActive, string Name);
    sealed record ReservationFact(ResourceAllocation Allocation, Guid? ProjectDisciplineId, IReadOnlyDictionary<DateOnly, decimal>? Spread);
    sealed record ReservationFacts(Dictionary<Guid, ReservationDiscipline> Disciplines, ReservationFact[] Reservations);

    /// The single current-valid attribution + full person-calendar/day-override spread read, reused by weekly and exact-date projections.
    static async Task<ReservationFacts> ReadReservations(Guid projectId, ResourceAllocation[] allocations, DateOnly through, HubDb db)
    {
        var ids = allocations.Select(allocation => allocation.Id).ToArray();
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(link => ids.Contains(link.AllocationId) && link.ReleasedAt == null).ToArrayAsync();
        var taskIds = links.Where(link => link.WorkType == "Task").Select(link => link.WorkId).Distinct().ToArray();
        var reviewIds = links.Where(link => link.WorkType == "Review").Select(link => link.WorkId).Distinct().ToArray();
        // Attribution uses the same current-work eligibility as AllocationEndpoints.Validate, not historical membership alone.
        var taskDisciplines = await db.Tasks.AsNoTracking().Where(task => task.ProjectId == projectId && taskIds.Contains(task.Id)
            && task.Status != TaskStatuses.Complete && task.Status != TaskStatuses.Cancelled && task.Status != TaskStatuses.OnHold && task.DeletedAt == null)
            .Select(task => new WorkDiscipline(task.Id, task.ProjectDisciplineId, task.AssigneeId)).ToDictionaryAsync(task => task.Id);
        var reviewDisciplines = await db.DisciplineReviews.AsNoTracking().Where(review => review.ProjectId == projectId && reviewIds.Contains(review.Id)
            && review.Status != DisciplineReviewStatus.Approved && db.ReviewPackages.Any(package => package.ProjectId == projectId
                && package.CurrentRoundId == review.RoundId && package.Status != ReviewStatus.Cancelled
                && package.Status != ReviewStatus.Superseded && package.Status != ReviewStatus.Approved))
            .Select(review => new WorkDiscipline(review.Id, review.ProjectDisciplineId, review.ReviewerId)).ToDictionaryAsync(review => review.Id);
        var roles = await db.OpenRoles.AsNoTracking().Where(role => role.ProjectId == projectId && role.Status == OpenRoleStatus.Filled
            && role.FilledAllocationId != null && ids.Contains(role.FilledAllocationId.Value)).Select(role => new FilledRoleDiscipline(role.FilledAllocationId, role.ProjectDisciplineId)).ToArrayAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(discipline => discipline.ProjectId == projectId)
            .Select(discipline => new ReservationDiscipline(discipline.Id, discipline.IsActive, discipline.Discipline!.Name)).ToDictionaryAsync(discipline => discipline.Id);
        var overrides = await db.AllocationDayOverrides.AsNoTracking().Where(day => ids.Contains(day.AllocationId)).ToArrayAsync();
        var calendars = await PersonCapacity.Calendars(db, allocations.Select(allocation => allocation.PersonId).Distinct().ToArray(),
            allocations.Select(allocation => allocation.ThroughDate).Append(through).Max());
        var reservations = new List<ReservationFact>();
        foreach (var allocation in allocations)
        {
            var current = links.Where(link => link.AllocationId == allocation.Id).ToArray();
            var attributed = new HashSet<Guid>(); var unresolved = current.Length == 0;
            foreach (var link in current)
            {
                var work = link.WorkType == "Task" ? taskDisciplines.GetValueOrDefault(link.WorkId)
                    : link.WorkType == "Review" ? reviewDisciplines.GetValueOrDefault(link.WorkId) : null;
                var validPurpose = link.WorkType == "Task" ? allocation.Purpose == AllocationPurpose.Production && link.ReviewHours is null
                    : link.WorkType == "Review" && allocation.Purpose == AllocationPurpose.Review && link.ReviewHours is > 0 and <= 10000;
                if (!validPurpose || work is null || work.PersonId != allocation.PersonId || link.PersonId != allocation.PersonId
                    || !disciplines.ContainsKey(work.ProjectDisciplineId) || link.WorkDate < allocation.FromDate || link.WorkDate > allocation.ThroughDate) unresolved = true;
                else attributed.Add(work.ProjectDisciplineId);
            }
            foreach (var role in roles.Where(role => role.FilledAllocationId == allocation.Id && role.ProjectDisciplineId != null))
                if (disciplines.ContainsKey(role.ProjectDisciplineId!.Value)) attributed.Add(role.ProjectDisciplineId.Value); else unresolved = true;
            var key = !unresolved && attributed.Count == 1 ? attributed.Single() : (Guid?)null;
            IReadOnlyDictionary<DateOnly, decimal>? spread = null;
            try { spread = AllocationRules.Spread(allocation.FromDate, allocation.ThroughDate, allocation.PlannedHours,
                calendars[allocation.PersonId], overrides.Where(day => day.AllocationId == allocation.Id).ToDictionary(day => day.WorkDate, day => day.Hours)); }
            catch (ArgumentException) { /* A changed calendar is an unavailable input, never a zero allocation. */ }
            reservations.Add(new(allocation, key, spread));
        }
        return new(disciplines, reservations.ToArray());
    }

    public sealed record ExactDateReservationDiscipline(Guid? ProjectDisciplineId, string? DisciplineName, bool Unattributed,
        decimal? ConfirmedHours, decimal KnownConfirmedHours, int UnavailableConfirmed);
    public sealed record ExactDateReservations(DateOnly FromDate, DateOnly ThroughDate, string AllocationCoverage,
        ExactDateReservationDiscipline[] Disciplines, DateTimeOffset AsOf);
    sealed class ExactReservationBucket
    {
        public decimal Known; public int Unavailable;
    }

    /// Caller owns project access, authorized query/completeness and the read snapshot. No feature guard, access widening, or transaction here.
    public static async Task<ExactDateReservations> ConfirmedByDiscipline(Guid projectId, DateOnly from, DateOnly through,
        IQueryable<ResourceAllocation> visibleAllocations, bool completeAllocations, HubDb db, TimeProvider clock)
    {
        Check.That(through >= from, "throughDate", "error.date_range");
        var allocations = await visibleAllocations.AsNoTracking().Where(allocation => allocation.ProjectId == projectId && allocation.Status == AllocationStatus.Confirmed
            && allocation.FromDate <= through && allocation.ThroughDate >= from).ToArrayAsync();
        var facts = await ReadReservations(projectId, allocations, through, db);
        var buckets = facts.Disciplines.Values.Where(discipline => discipline.IsActive).ToDictionary(discipline => discipline.Id, _ => new ExactReservationBucket());
        foreach (var fact in facts.Reservations)
        {
            var key = fact.ProjectDisciplineId ?? Guid.Empty;
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = new();
            // A failure in the full-range spread remains unavailable, even if a clipped interval might otherwise look empty.
            if (fact.Spread is null) bucket.Unavailable++;
            else bucket.Known += fact.Spread.Where(day => day.Key >= from && day.Key <= through).Sum(day => day.Value);
        }
        var rows = buckets.OrderBy(pair => pair.Key == Guid.Empty).ThenBy(pair => facts.Disciplines.GetValueOrDefault(pair.Key)?.Name, StringComparer.OrdinalIgnoreCase).ThenBy(pair => pair.Key)
            .Select(pair => new ExactDateReservationDiscipline(pair.Key == Guid.Empty ? null : pair.Key, facts.Disciplines.GetValueOrDefault(pair.Key)?.Name,
                pair.Key == Guid.Empty, completeAllocations && pair.Value.Unavailable == 0 ? pair.Value.Known : null,
                pair.Value.Known, pair.Value.Unavailable)).ToArray();
        return new(from, through, completeAllocations ? "Complete" : "VisibleOnly", rows, clock.GetUtcNow());
    }

    internal static async Task<DisciplineComparison> Compare(Guid projectId, DateOnly[] weeks, IQueryable<ResourceAllocation> visibleAllocations,
        bool completeAllocations, HubDb db, TimeProvider clock)
    {
        var first = weeks[0]; var through = weeks[^1].AddDays(6);
        var allocations = await visibleAllocations.Where(allocation => allocation.Status == AllocationStatus.Confirmed || allocation.Status == AllocationStatus.Proposed)
            .Where(allocation => allocation.FromDate <= through && allocation.ThroughDate >= first).ToArrayAsync();
        var facts = await ReadReservations(projectId, allocations, through, db);
        var disciplines = facts.Disciplines;
        var buckets = new Dictionary<Guid, Bucket>();
        Bucket For(Guid? id)
        {
            var key = id is { } valid && disciplines.ContainsKey(valid) ? valid : Guid.Empty;
            if (!buckets.TryGetValue(key, out var bucket)) buckets[key] = bucket = new(weeks.Length);
            return bucket;
        }
        foreach (var discipline in disciplines.Values.Where(discipline => discipline.IsActive)) For(discipline.Id);
        foreach (var fact in facts.Reservations)
        {
            var allocation = fact.Allocation;
            var key = fact.ProjectDisciplineId;
            var bucket = For(key); if (key is null) bucket.Allocations++;
            var spread = fact.Spread;
            for (var i = 0; i < weeks.Length; i++)
            {
                if (allocation.FromDate > weeks[i].AddDays(6) || allocation.ThroughDate < weeks[i]) continue;
                if (spread is null)
                {
                    if (allocation.Status == AllocationStatus.Confirmed) bucket.UnavailableConfirmed[i]++; else bucket.UnavailableProposed[i]++;
                }
                else
                {
                    var hours = spread.Where(day => Workload.WeekOf(day.Key) == weeks[i]).Sum(day => day.Value);
                    if (allocation.Status == AllocationStatus.Confirmed) bucket.Confirmed[i] += hours; else bucket.Proposed[i] += hours;
                }
            }
        }
        var tasks = await db.Tasks.AsNoTracking().Where(task => task.ProjectId == projectId && task.Status != TaskStatuses.Complete
            && task.Status != TaskStatuses.Cancelled && (task.DueDate == null || task.DueDate >= first && task.DueDate <= through))
            .Select(task => new DisciplineTaskInput(task.ProjectDisciplineId, task.DueDate, task.EstimatedHours, task.ProgressPct)).ToArrayAsync();
        foreach (var task in tasks)
        {
            var bucket = For(task.ProjectDisciplineId);
            if (task.DueDate is not { } due) { bucket.Undated++; continue; }
            var index = Array.IndexOf(weeks, Workload.WeekOf(due));
            if (index < 0) continue;
            var remaining = Workload.Remaining(task.EstimatedHours, task.ProgressPct);
            if (remaining is { } hours) bucket.Remaining[index] += hours; else bucket.Unestimated[index]++;
        }
        var rows = buckets.OrderBy(pair => pair.Key == Guid.Empty).ThenBy(pair => disciplines.GetValueOrDefault(pair.Key)?.Name, StringComparer.OrdinalIgnoreCase).ThenBy(pair => pair.Key)
            .Select(pair => new ComparisonDiscipline(pair.Key == Guid.Empty ? null : pair.Key, disciplines.GetValueOrDefault(pair.Key)?.Name,
                pair.Key == Guid.Empty, pair.Value.Allocations, pair.Value.Undated, weeks.Select((week, index) => new ComparisonWeek(week,
                    pair.Value.UnavailableConfirmed[index] == 0 ? pair.Value.Confirmed[index] : null, pair.Value.Confirmed[index], pair.Value.UnavailableConfirmed[index],
                    pair.Value.UnavailableProposed[index] == 0 ? pair.Value.Proposed[index] : null, pair.Value.Proposed[index], pair.Value.UnavailableProposed[index],
                    pair.Value.Unestimated[index] == 0 ? pair.Value.Remaining[index] : null, pair.Value.Remaining[index], pair.Value.Unestimated[index])).ToArray())).ToArray();
        return new(weeks, completeAllocations ? "Complete" : "VisibleOnly", rows, clock.GetUtcNow());
    }
}

file sealed record TaskEstimateInput(decimal? EstimatedHours, DateOnly? DueDate);
file sealed record FilledRoleDiscipline(Guid? FilledAllocationId, Guid? ProjectDisciplineId);
file sealed record DisciplineTaskInput(Guid ProjectDisciplineId, DateOnly? DueDate, decimal? EstimatedHours, int ProgressPct);
