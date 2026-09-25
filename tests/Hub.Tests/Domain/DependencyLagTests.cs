using Hub.Domain;

namespace Hub.Tests.Domain;

/// Packet 021 worked examples: deliverable links with lag and Blocking Others, working-day lag, date checks with lag,
/// blocked and stale days in working days, and workload spread over an office's working days.
public sealed class DependencyLagTests
{
    static DateOnly D(int y, int m, int d) => new(y, m, d);
    static readonly WorkCalendar MondayHoliday = new([D(2026, 10, 12)]); // Thanksgiving, a Monday

    [Fact]
    public void A_deliverable_waits_through_the_lag_after_its_predecessor_is_issued() // US2-1, FR-002, FR-003
    {
        var s = new Scenario { Today = D(2027, 2, 5) };
        var a = s.Deliverable("A", D(2027, 2, 1), status: DeliverableStatus.Issued, issued: D(2027, 2, 1));
        var b = s.Deliverable("B", D(2027, 3, 30), status: DeliverableStatus.NotStarted, start: D(2027, 2, 20));
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), a.Id, b.Id, 10));
        var r = s.Run().D(b);
        Assert.True(r.IsWaiting);
        Assert.False(r.IsBlocked);
        Assert.Equal(("A", "lag 10 days"), (r.Blockers.Single().Key, r.Blockers.Single().Reason));

        s.Replace(s.Deliverables, b, b with { StartDate = D(2027, 2, 8) });
        s.Today = D(2027, 2, 8); // the start date arrives inside the lag
        Assert.True(s.Run().Deliverables.Single(x => x.DeliverableId == b.Id).IsBlocked);

        s.Today = D(2027, 2, 11); // 2027-02-01 + 10 days
        var done = s.Run().Deliverables.Single(x => x.DeliverableId == b.Id);
        Assert.Equal((false, false, 0), (done.IsWaiting, done.IsBlocked, done.Blockers.Count));
    }

    [Fact]
    public void A_deliverable_that_holds_up_another_is_Blocking_Others_and_a_cancelled_one_holds_up_nothing() // US1-2, FR-002
    {
        var s = new Scenario();
        var a = s.Deliverable("A", s.Today.AddDays(20));
        var b = s.Deliverable("B", s.Today.AddDays(40), status: DeliverableStatus.NotStarted, start: s.Today.AddDays(-1));
        var c = s.Deliverable("C", s.Today.AddDays(40), status: DeliverableStatus.NotStarted, start: s.Today.AddDays(30)); // not started yet: waiting only
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), a.Id, b.Id));
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), a.Id, c.Id));
        var r = s.Run();
        Assert.True(r.D(b).IsBlocked);
        Assert.True(r.D(c).IsWaiting);
        Assert.Equal(1, r.D(a).BlockingCount);

        s.Replace(s.Deliverables, a, a with { Status = DeliverableStatus.Cancelled });
        var after = s.Run();
        Assert.Empty(after.Deliverables.Single(x => x.DeliverableId == b.Id).Blockers);
        Assert.Equal(0, after.Deliverables.Single(x => x.DeliverableId == a.Id).BlockingCount);
    }

    [Fact]
    public void Date_checks_add_the_lag_to_the_predecessor_due_date() // US2-2, D-12
    {
        var s = new Scenario();
        var a = s.Deliverable("A", D(2026, 10, 1));
        var b = s.Deliverable("B", D(2026, 11, 30), status: DeliverableStatus.NotStarted, start: D(2026, 10, 5));
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), a.Id, b.Id, 10));
        var r = s.Run().D(b);
        Assert.Equal("D-12", r.Inconsistencies.Single().Rule);
        Assert.Contains("2026-10-11", r.Inconsistencies.Single().Text);

        var t = new Scenario { Today = D(2026, 10, 1), Calendar = MondayHoliday };
        var ta = t.Task("A", TaskStatuses.NotStarted, due: D(2026, 10, 9)); // a Friday
        var tb = t.Task("B", TaskStatuses.NotStarted, start: D(2026, 10, 13), due: D(2026, 10, 30));
        t.Deps.Add(new DependencySnap(Guid.NewGuid(), ta.Id, tb.Id, 2));
        Assert.False(t.Run().T(tb).IsDateInconsistent); // calendar days: 10-11
        t.Settings = t.Settings with { WorkingDaysEnabled = true };
        var wd = t.Run().T(tb); // working days skip the weekend and the Monday holiday: 10-14
        Assert.True(wd.IsDateInconsistent);
        Assert.Contains("2026-10-14", wd.Inconsistencies.Single().Text);
    }

    [Fact]
    public void A_task_lag_counts_working_days_when_they_are_on() // FR-003, FR-005
    {
        var s = new Scenario { Today = D(2026, 10, 13), Calendar = MondayHoliday };
        var a = s.Task("A", TaskStatuses.Complete, due: D(2026, 10, 9), completed: D(2026, 10, 9));
        var b = s.Task("B", TaskStatuses.NotStarted, start: D(2026, 10, 20), due: D(2026, 10, 30));
        s.Deps.Add(new DependencySnap(Guid.NewGuid(), a.Id, b.Id, 2));
        Assert.False(s.Run().T(b).IsWaiting); // calendar: 10-11 has passed
        s.Settings = s.Settings with { WorkingDaysEnabled = true };
        Assert.True(s.Run().T(b).IsWaiting); // working days: 10-14 is still ahead
    }

    [Fact]
    public void Blocked_and_stale_days_do_not_grow_over_a_weekend_and_a_holiday() // SC-001, FR-005
    {
        var s = new Scenario { Today = D(2026, 10, 13), Calendar = MondayHoliday }; // Tuesday after Thanksgiving
        s.Settings = s.Settings with { BlockedAttentionDays = 3, TaskStaleDays = 2 };
        var blocked = s.Task("T1", block: BlockType.Client);
        s.Replace(s.Tasks, blocked, blocked with { ManualBlockSince = D(2026, 10, 9) }); // blocked since Friday
        var stale = s.Task("T2", lastActivity: D(2026, 10, 9));
        var calendar = s.Run();
        Assert.Equal(4, calendar.T(blocked).DaysBlocked);
        Assert.True(calendar.T(stale).IsStale);
        Assert.Contains(calendar.Attention, x => x.RuleId == "A-02" && x.ItemId == blocked.Id);

        s.Settings = s.Settings with { WorkingDaysEnabled = true };
        var working = s.Run();
        Assert.Equal(1, working.T(blocked).DaysBlocked);
        Assert.False(working.T(stale).IsStale);
        Assert.DoesNotContain(working.Attention, x => x.RuleId == "A-02" && x.ItemId == blocked.Id);
    }

    [Fact]
    public void Workload_spreads_over_the_offices_working_days() // FR-005, §12.15
    {
        var t = new LoadTask(Guid.NewGuid(), Guid.NewGuid(), 40, 0, D(2026, 10, 12), D(2026, 10, 16)); // Mon–Fri with a Monday holiday
        Assert.Equal(40, Workload.Spread(t, D(2026, 10, 12)).ByWeek.Values.Sum());
        Assert.Equal(40, Workload.Spread(t, D(2026, 10, 12), MondayHoliday).ByWeek.Values.Sum());
        var perDay = Workload.Spread(t with { EstimatedHours = 8 }, D(2026, 10, 12), MondayHoliday);
        Assert.Equal(8m, perDay.ByWeek[Workload.WeekOf(D(2026, 10, 12))]); // all four remaining days are in the same week
        var nextWeek = new LoadTask(Guid.NewGuid(), Guid.NewGuid(), 10, 0, D(2026, 10, 9), D(2026, 10, 13)); // Fri, (Mon holiday), Tue
        var spread = Workload.Spread(nextWeek, D(2026, 10, 9), MondayHoliday);
        Assert.Equal((5m, 5m), (spread.ByWeek[Workload.WeekOf(D(2026, 10, 9))], spread.ByWeek[Workload.WeekOf(D(2026, 10, 13))])); // 2 working days, not 3
    }
}
