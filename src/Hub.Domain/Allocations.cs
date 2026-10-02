namespace Hub.Domain;

public static class AllocationStatus
{
    public const string Proposed = "Proposed", Confirmed = "Confirmed", Declined = "Declined", Cancelled = "Cancelled", Completed = "Completed";
    public static readonly string[] All = [Proposed, Confirmed, Declined, Cancelled, Completed];
}

public static class AllocationPurpose
{
    public const string Production = "Production", Review = "Review";
    public static readonly string[] All = [Production, Review];
}

public static class AvailabilityCategory
{
    public const string Unavailable = "Unavailable", Reduced = "Reduced", Additional = "Additional";
    public static readonly string[] All = [Unavailable, Reduced, Additional];
}

public sealed record AllocationDemand(decimal ReservedHours, decimal LinkedRemainingHours);

/// <summary>Exact decimal staffing arithmetic. Actual time and task estimates are never changed here.</summary>
public static class AllocationRules
{
    public static bool Step(string from, string to) => (from, to) switch
    {
        (AllocationStatus.Proposed, AllocationStatus.Confirmed or AllocationStatus.Declined or AllocationStatus.Cancelled) => true,
        (AllocationStatus.Confirmed, AllocationStatus.Completed or AllocationStatus.Cancelled) => true,
        _ => false,
    };

    public static string AfterMaterialEdit(string status) => status == AllocationStatus.Confirmed ? AllocationStatus.Proposed : status;

    /// <summary>An explicit override replaces normal capacity, including on a holiday.</summary>
    public static decimal DailyCapacity(DateOnly day, decimal weeklyHours, WorkCalendar calendar, decimal? dayOverride)
    {
        if (weeklyHours < 0 || dayOverride < 0) throw new ArgumentOutOfRangeException(nameof(weeklyHours));
        if (dayOverride is { } exact) return exact;
        if (!calendar.IsWorkingDay(day)) return 0;
        var monday = Workload.WeekOf(day);
        var workingDays = calendar.WorkingDays(monday, monday.AddDays(6)).Count();
        return workingDays == 0 ? 0 : weeklyHours / workingDays;
    }

    /// <summary>Explicit day hours take precedence; the remainder is spread exactly over other eligible days.</summary>
    public static IReadOnlyDictionary<DateOnly, decimal> Spread(DateOnly from, DateOnly through, decimal totalHours,
        WorkCalendar calendar, IReadOnlyDictionary<DateOnly, decimal>? dayOverrides = null)
    {
        if (through < from || totalHours <= 0) throw new ArgumentOutOfRangeException(nameof(totalHours));
        dayOverrides ??= new Dictionary<DateOnly, decimal>();
        if (dayOverrides.Any(x => x.Key < from || x.Key > through || x.Value < 0)) throw new ArgumentOutOfRangeException(nameof(dayOverrides));
        var fixedHours = dayOverrides.Values.Sum();
        if (fixedHours > totalHours) throw new ArgumentOutOfRangeException(nameof(dayOverrides));
        var free = calendar.WorkingDays(from, through).Where(d => !dayOverrides.ContainsKey(d)).ToArray();
        var remaining = totalHours - fixedHours;
        if (free.Length == 0 && remaining > 0) throw new ArgumentException("A positive allocation needs an eligible day.", nameof(through));
        var result = dayOverrides.ToDictionary(x => x.Key, x => x.Value);
        if (free.Length > 0)
        {
            var perDay = remaining / free.Length;
            for (var i = 0; i < free.Length; i++) result[free[i]] = i == free.Length - 1 ? remaining - perDay * (free.Length - 1) : perDay;
        }
        return result;
    }

    /// <summary>Linked forecast and its reservation represent the same commitment, so count the larger one.</summary>
    public static decimal Committed(IEnumerable<AllocationDemand> confirmed, decimal unlinkedRemainingHours)
    {
        var rows = confirmed.ToArray();
        if (unlinkedRemainingHours < 0 || rows.Any(x => x.ReservedHours < 0 || x.LinkedRemainingHours < 0))
            throw new ArgumentOutOfRangeException(nameof(confirmed));
        return rows.Sum(x => Math.Max(x.ReservedHours, x.LinkedRemainingHours)) + unlinkedRemainingHours;
    }
}
