namespace Hub.Domain;

/// Working-day calendar (packet 021, §10.4): weekends and an office's statutory holidays are skipped when the
/// organisation counts thresholds in working days. Overdue still means "due date before today" (G-01).
public sealed class WorkCalendar(IEnumerable<DateOnly>? holidays = null)
{
    readonly HashSet<DateOnly> off = [.. holidays ?? []];
    public static readonly WorkCalendar Weekdays = new();

    public bool IsWorkingDay(DateOnly d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !off.Contains(d);

    /// Working days d with from < d ≤ to; negative when `to` is before `from`.
    public int Between(DateOnly from, DateOnly to)
    {
        if (to < from) return -Between(to, from);
        var n = 0;
        for (var d = from.AddDays(1); d <= to; d = d.AddDays(1)) if (IsWorkingDay(d)) n++;
        return n;
    }

    /// The date `days` working days after `from`.
    public DateOnly Add(DateOnly from, int days)
    {
        var d = from;
        while (days > 0) { d = d.AddDays(1); if (IsWorkingDay(d)) days--; }
        return d;
    }

    public IEnumerable<DateOnly> WorkingDays(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1)) if (IsWorkingDay(d)) yield return d;
    }
}
