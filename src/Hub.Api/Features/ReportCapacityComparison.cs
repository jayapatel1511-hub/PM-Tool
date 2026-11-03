using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class ReportEndpoints
{
    public sealed record ReportCapacityComparison(string GroupBy, string Basis, string Unit,
        DateTimeOffset AsOf, string ScopeCoverage, IReadOnlyList<ReportPeriod> Periods,
        IReadOnlyList<ReportCapacityComparisonRow> Rows, int ReturnedGroups, int TotalGroups, bool Truncated);
    public sealed record ReportCapacityComparisonRow(string Key, Guid? DisciplineId, string Discipline,
        string PeriodKey, ReportUnitCell Capacity, ReportUnitCell Confirmed, ReportUnitCell Expected,
        ReportUnitCell Possible, ReportUnitCell Demand, ReportUnitCell Deficit, int PartialPeople, int ProvisionalPeople);

    // Projection input only: the endpoint returns the grouped records above, never these person identities.
    public sealed record CapacityComparisonMember(Guid Id, Guid? DisciplineId, decimal? Capacity,
        decimal? Confirmed, decimal? Expected, decimal? Possible, bool Partial, bool Provisional);

    /// One canonical displayed cohort per week. No forecasting, source widening or personal FTE denominator.
    public static ReportCapacityComparison ProjectCapacityComparison(IReadOnlyList<DateOnly> weeks,
        IReadOnlyList<IReadOnlyList<CapacityComparisonMember>> membersByWeek,
        IReadOnlyDictionary<Guid, string> disciplineNames, string unit, string period,
        decimal? standardWeeklyHours, DateTimeOffset asOf, string scopeCoverage)
    {
        if (unit is not ("hours" or "fte")) throw new ArgumentException("Unsupported report unit.", nameof(unit));
        var periods = ReportUnitMath.Periods(weeks, period, standardWeeklyHours);
        var weekIndices = weeks.Select((week, index) => (week, index)).ToDictionary(value => value.week, value => value.index);
        if (weeks.Count > 26 || membersByWeek.Count != weeks.Count) throw new ArgumentException("Comparison requires the displayed report weeks.");
        if (membersByWeek.Any(members => members.Select(person => person.Id).Distinct().Count() != members.Count
            || members.Any(person => person.Capacity is < 0 || person.Confirmed is < 0 || person.Expected is < 0 || person.Possible is < 0)))
            throw new ArgumentException("Each displayed person must have one nonnegative canonical source per week.");
        var partialPeople = membersByWeek.SelectMany(members => members).Where(person => person.Partial).Select(person => person.Id).ToHashSet();
        var groups = membersByWeek.SelectMany(members => members).Select(person => person.DisciplineId).Distinct()
            .Select(id => (Id: id, Name: id is { } key ? disciplineNames.GetValueOrDefault(key) ?? Text.Get("reportChart.unknownDiscipline") : Text.Get("reportChart.notSet")))
            .OrderBy(group => group.Id is null).ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase).ThenBy(group => group.Id).ToArray();
        var kept = groups.Take(periods.Count == 0 ? 0 : 2000 / periods.Count).ToArray();
        var rows = new List<ReportCapacityComparisonRow>();
        foreach (var group in kept)
        {
            var members = membersByWeek.Select(people => people.Where(person => person.DisciplineId == group.Id).ToArray()).ToArray();
            var capacity = members.Select(people => people.Sum(person => person.Capacity ?? 0)).ToArray();
            var confirmed = members.Select(people => people.Sum(person => person.Confirmed ?? 0)).ToArray();
            var expected = members.Select(people => people.Sum(person => person.Expected ?? 0)).ToArray();
            var possible = members.Select(people => people.Sum(person => person.Possible ?? 0)).ToArray();
            var demand = confirmed.Zip(expected, (c, e) => c + e).ToArray();
            var provisional = members.Select(people => people.Any(person => person.Provisional)).ToArray();
            var incomplete = members.Select(people => people.Any(person => partialPeople.Contains(person.Id))).ToArray();
            var capacityUnknown = members.Select((people, index) => provisional[index] || people.Any(person => person.Capacity is null)).ToArray();
            var confirmedUnknown = members.Select((people, index) => incomplete[index] || people.Any(person => person.Confirmed is null)).ToArray();
            var expectedUnknown = members.Select((people, index) => incomplete[index] || people.Any(person => person.Expected is null)).ToArray();
            var possibleUnknown = members.Select((people, index) => incomplete[index] || people.Any(person => person.Possible is null)).ToArray();
            var demandUnknown = confirmedUnknown.Zip(expectedUnknown, (c, e) => c || e).ToArray();
            foreach (var bucket in periods)
            {
                var indices = bucket.Weeks.Select(week => weekIndices[week]).ToArray();
                ReportUnitCell Quantity(string path, decimal[] source, bool[] unknown, bool availability = false)
                {
                    var cell = ReportUnitMath.Quantity(path, indices.Select(index => unknown[index] ? (decimal?)null : source[index]).ToArray(),
                        indices.Select(index => source[index]).ToArray(), 0, unit, standardWeeklyHours, scopeCoverage);
                    return availability && indices.Any(index => provisional[index]) ? cell with { Value = null, Coverage = "ProvisionalAvailability" } : cell;
                }
                var deficitUnknown = indices.Any(index => capacityUnknown[index] || demandUnknown[index]);
                var deficit = deficitUnknown
                    ? new ReportUnitCell("deficit", "Hours", null, null, 0, indices.Any(index => provisional[index]) ? "ProvisionalAvailability" : "KnownSubtotal")
                    : ReportUnitMath.Quantity("deficit", indices.Select(index => (decimal?)Math.Max(0, demand[index] - capacity[index])).ToArray(),
                        indices.Select(index => Math.Max(0, demand[index] - capacity[index])).ToArray(), 0, unit, standardWeeklyHours, scopeCoverage);
                rows.Add(new(group.Id is { } id ? $"discipline:{id:D}" : "discipline:none", group.Id, group.Name, bucket.Key,
                    Quantity("capacity", capacity, capacityUnknown, true), Quantity("confirmed", confirmed, confirmedUnknown),
                    Quantity("expected", expected, expectedUnknown), Quantity("possible", possible, possibleUnknown), Quantity("demand", demand, demandUnknown), deficit,
                    indices.SelectMany(index => members[index]).Where(person => partialPeople.Contains(person.Id)).Select(person => person.Id).Distinct().Count(),
                    indices.SelectMany(index => members[index]).Where(person => person.Provisional).Select(person => person.Id).Distinct().Count()));
            }
        }
        return new("homeDiscipline", "PLN09ConfirmedExpected", unit, asOf, scopeCoverage, periods, rows, kept.Length, groups.Length, kept.Length < groups.Length);
    }

    static async Task CaptureTeamCapacityComparison(Ctx c, PlanningEndpoints.GridResult grid,
        IReadOnlyList<IReadOnlyList<PlanningEndpoints.PersonRow>> membersByWeek)
    {
        if (!c.CaptureCapacityComparison || !c.InputsEnabled) return;
        var ids = membersByWeek.SelectMany(members => members).Select(person => person.HomeDisciplineId).OfType<Guid>().Distinct().ToArray();
        var names = ids.Length == 0 ? new Dictionary<Guid, string>() : await c.Db.Disciplines.AsNoTracking().Where(discipline => ids.Contains(discipline.Id)).ToDictionaryAsync(discipline => discipline.Id, discipline => discipline.Name);
        var source = membersByWeek.Select((people, index) => (IReadOnlyList<CapacityComparisonMember>)people.Select(person => {
            var cell = person.Cells.FirstOrDefault(cell => cell.Week == grid.Weeks[index]);
            return new CapacityComparisonMember(person.Id, person.HomeDisciplineId, cell?.Capacity, cell?.Confirmed, cell?.Expected, cell?.Possible, person.Partial, cell?.Provisional ?? false);
        }).ToArray()).ToArray();
        c.CapacityComparison = ProjectCapacityComparison(grid.Weeks, source, names, c.Str("unit") ?? "hours", c.Str("period") ?? "weeks",
            c.S.DefaultWeeklyCapacityHours, c.Now, c.InputContext?.Coverage ?? (grid.PartialView ? "VisibleOnly" : InputCoverage));
    }
}
