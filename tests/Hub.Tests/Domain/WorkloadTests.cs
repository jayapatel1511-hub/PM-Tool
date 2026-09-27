using Hub.Domain;

namespace Hub.Tests.Domain;

/// §12.15 worked examples for the workload calculation (packet 017, FR-001, FR-002, FR-004).
public sealed class WorkloadTests
{
    static readonly DateOnly Monday = new(2026, 9, 14);
    static readonly Guid P1 = Guid.NewGuid(), P2 = Guid.NewGuid();
    static LoadTask T(decimal? est, int pct = 0, DateOnly? start = null, DateOnly? due = null) => new(Guid.NewGuid(), P1, est, pct, start, due);

    [Fact]
    public void Remaining_hours_spread_evenly_over_working_days_and_summed_per_week() // US1 scenario 1
    {
        var r = Workload.Spread(T(24, 50, due: new DateOnly(2026, 9, 25)), Monday); // ten working days, today included
        Assert.Equal(12, r.Remaining);
        Assert.Equal(6.0m, Math.Round(r.ByWeek[Monday], 2));
        Assert.Equal(6.0m, Math.Round(r.ByWeek[Monday.AddDays(7)], 2));
        Assert.False(r.Overdue);
    }

    [Fact]
    public void Overdue_work_lands_in_the_current_week_and_undated_work_is_not_spread() // US1 scenario 2
    {
        var late = Workload.Spread(T(10, due: new DateOnly(2026, 9, 10)), Monday.AddDays(2));
        Assert.True(late.Overdue);
        Assert.Equal(10, late.ByWeek.Single(x => x.Key == Monday).Value);
        var undated = Workload.Spread(T(8), Monday);
        Assert.Empty(undated.ByWeek);
        Assert.Equal(8, undated.NoDueDate);
    }

    [Fact]
    public void Unestimated_and_finished_work_contributes_no_hours() // FR-001
    {
        Assert.Null(Workload.Spread(T(null, due: Monday.AddDays(3)), Monday).Remaining);
        Assert.Empty(Workload.Spread(T(null, due: Monday.AddDays(3)), Monday).ByWeek);
        var done = Workload.Spread(T(16, 100, due: Monday.AddDays(3)), Monday);
        Assert.Equal(0, done.Remaining);
        Assert.Empty(done.ByWeek);
        Assert.Equal(7.5m, Workload.Remaining(10, 25));
        Assert.Equal(10, Workload.Remaining(10, -5)); // progress is clamped to 0..100
    }

    [Fact]
    public void The_window_starts_at_the_later_of_start_and_today() // FR-002
    {
        var later = Workload.Spread(T(10, start: Monday.AddDays(7), due: Monday.AddDays(11)), Monday);
        Assert.Equal(10, later.ByWeek.Single().Value);
        Assert.Equal(Monday.AddDays(7), later.ByWeek.Single().Key);
        var begun = Workload.Spread(T(10, start: Monday.AddDays(-20), due: Monday.AddDays(4)), Monday); // started long ago: from today
        Assert.Equal(10, Math.Round(begun.ByWeek[Monday], 2));
        var inverted = Workload.Spread(T(6, start: Monday.AddDays(9), due: Monday.AddDays(8)), Monday); // start after due: the due day
        Assert.Equal(6, inverted.ByWeek[Monday.AddDays(7)]);
        var weekend = Workload.Spread(T(4, due: Monday.AddDays(6)), Monday.AddDays(5)); // Saturday to Sunday: kept in the due week
        Assert.Equal(4, weekend.ByWeek[Monday]);
    }

    [Fact]
    public void Over_under_and_capacity_edges() // FR-004, US2 scenarios 1 and 2
    {
        Assert.True(Workload.OverAssigned(80, 140));
        Assert.True(Workload.OverAssigned(111, 0));
        Assert.False(Workload.OverAssigned(110, 110));
        Assert.True(Workload.UnderAssigned(25, 30, 0));
        Assert.False(Workload.UnderAssigned(25, 30, 3)); // never while work is unestimated
        Assert.False(Workload.UnderAssigned(25, 45, 0));
        Assert.Equal(140, Workload.Pct(56, 40));
        Assert.Equal(0, Workload.Pct(0, 0));
        Assert.Equal(999, Workload.Pct(5, 0));
        Assert.Equal(Monday, Workload.WeekOf(Monday.AddDays(6)));
    }

    [Fact]
    public void Deadline_clusters_need_three_tasks_across_two_projects_within_three_days() // FR-004, US2 scenario 3
    {
        Assert.True(Workload.DeadlineCluster([(Monday.AddDays(3), P1), (Monday.AddDays(4), P1), (Monday.AddDays(5), P2)], Monday));
        Assert.False(Workload.DeadlineCluster([(Monday.AddDays(3), P1), (Monday.AddDays(4), P1), (Monday.AddDays(5), P1)], Monday)); // one project
        Assert.False(Workload.DeadlineCluster([(Monday.AddDays(3), P1), (Monday.AddDays(4), P2), (Monday.AddDays(6), P2)], Monday)); // four days apart
        Assert.False(Workload.DeadlineCluster([(Monday.AddDays(14), P1), (Monday.AddDays(14), P2), (Monday.AddDays(15), P2)], Monday)); // beyond 14 days
        Assert.False(Workload.DeadlineCluster([(Monday.AddDays(-1), P1), (Monday, P2), (Monday, P2)], Monday)); // overdue ones are not "coming"
    }
}
