namespace Hub.Domain;

/// One open, assigned task as the workload calculation sees it (§12.15).
public sealed record LoadTask(Guid TaskId, Guid ProjectId, decimal? EstimatedHours, int ProgressPct, DateOnly? StartDate, DateOnly? DueDate);

/// Where one task's remaining hours land: per ISO week (Monday), or in the "no due date" bucket.
public sealed record TaskLoad(decimal? Remaining, IReadOnlyDictionary<DateOnly, decimal> ByWeek, decimal NoDueDate, bool Overdue);
public sealed record TaskDailyLoad(decimal? Remaining, IReadOnlyDictionary<DateOnly, decimal> ByDay, decimal NoDueDate, bool Overdue);

/// The workload calculation of §12.15 as pure functions, stated on screen in plain words. It deliberately ignores
/// leave, holidays and logged hours (§12.15 "What it deliberately does not do", §36.8).
public static class Workload
{
    public const decimal OverPct = 110, AmberPct = 90, UnderPct = 40;

    /// Load bands tested in order (§45.4, FR-FIG-05): Over above the over setting, Near above the amber setting,
    /// Within from the under setting, Light below. An amber value at or above the over value leaves no Near band.
    public enum Band { Over, Near, Within, Light }

    public static Band LoadBand(decimal pct, decimal overPct = OverPct, decimal amberPct = AmberPct, decimal underPct = UnderPct) =>
        pct > overPct ? Band.Over : pct > amberPct ? Band.Near : pct >= underPct ? Band.Within : Band.Light;

    public static Band LoadBand(decimal pct, OrgSettings s) => LoadBand(pct, s.WorkloadOverPct, s.WorkloadAmberPct, s.WorkloadUnderPct);

    public static DateOnly WeekOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    /// remaining = estimate × (1 − progress); no estimate means no hours (the task is "unestimated").
    public static decimal? Remaining(decimal? estimate, int progressPct) =>
        estimate is { } e ? Math.Round(e * (1 - Math.Clamp(progressPct, 0, 100) / 100m), 2) : null;

    /// Spreads the remainder evenly over the working days (Mon–Fri, less holidays when working days are on) from
    /// max(start, today) to the due date; overdue work lands in the current week; work without a due date is not spread.
    public static TaskLoad Spread(LoadTask t, DateOnly today, WorkCalendar? calendar = null)
    {
        var daily = SpreadDays(t, today, calendar);
        var weeks = new Dictionary<DateOnly, decimal>();
        foreach (var (day, hours) in daily.ByDay)
            weeks[WeekOf(day)] = weeks.GetValueOrDefault(WeekOf(day)) + hours;
        return new(daily.Remaining, weeks, daily.NoDueDate, daily.Overdue);
    }

    /// The same forecast as Spread, before the display groups dates into weeks.
    public static TaskDailyLoad SpreadDays(LoadTask t, DateOnly today, WorkCalendar? calendar = null)
    {
        var remaining = Remaining(t.EstimatedHours, t.ProgressPct);
        var daysByDate = new Dictionary<DateOnly, decimal>();
        if (remaining is not { } hours || hours <= 0) return new(remaining, daysByDate, 0, t.DueDate < today);
        if (t.DueDate is not { } due) return new(remaining, daysByDate, hours, false);
        if (due < today) { daysByDate[today] = hours; return new(remaining, daysByDate, 0, true); }
        var from = t.StartDate is { } s && s > today ? s : today;
        if (from > due) from = due;
        var days = (calendar ?? WorkCalendar.Weekdays).WorkingDays(from, due).ToList();
        if (days.Count == 0) { daysByDate[due] = hours; return new(remaining, daysByDate, 0, false); } // a weekend-only window keeps its hours on its due date
        var perDay = hours / days.Count;
        for (var i = 0; i < days.Count; i++) daysByDate[days[i]] = i == days.Count - 1 ? hours - perDay * (days.Count - 1) : perDay;
        return new(remaining, daysByDate, 0, false);
    }

    /// Uncommitted (§45.3, FR-FIG-03): available capacity − committed load. A partial row never claims spare capacity
    /// (FR-CAP-06), so its value is null unless it is negative.
    public static decimal? Uncommitted(decimal available, decimal committed, bool partial) =>
        available - committed is var u && (u < 0 || !partial) ? u : null;

    public static decimal Pct(decimal hours, decimal capacity) => capacity <= 0 ? (hours > 0 ? 999 : 0) : Math.Round(hours * 100 / capacity, 0);

    /// Over-assigned: above the over setting (default 110 %) in the current or the next week.
    public static bool OverAssigned(decimal thisWeekPct, decimal nextWeekPct, decimal overPct = OverPct) => thisWeekPct > overPct || nextWeekPct > overPct;

    /// Under-assigned: below the under setting (default 40 %) for the next two weeks, and never while any of the person's work is unestimated.
    public static bool UnderAssigned(decimal thisWeekPct, decimal nextWeekPct, int unestimated, decimal underPct = UnderPct) =>
        unestimated == 0 && thisWeekPct < underPct && nextWeekPct < underPct;

    /// Deadline cluster: three or more tasks across two or more projects due within any three-day window in the next 14 days.
    public static bool DeadlineCluster(IEnumerable<(DateOnly Due, Guid ProjectId)> tasks, DateOnly today)
    {
        var soon = tasks.Where(x => x.Due >= today && x.Due <= today.AddDays(13)).ToList();
        for (var d = today; d <= today.AddDays(13); d = d.AddDays(1))
        {
            var start = d;
            var window = soon.Where(x => x.Due >= start && x.Due <= start.AddDays(2)).ToList();
            if (window.Count >= 3 && window.Select(x => x.ProjectId).Distinct().Count() >= 2) return true;
        }
        return false;
    }
}
