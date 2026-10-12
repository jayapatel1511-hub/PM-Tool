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

    // ---------- Packet 036, verification.md §8.2: SYN-105 links L1–L6 on dates A, B and C (SC-003, AC-DEPT-02 to -04) ----------

    static readonly WorkCalendar Nov11Holiday = new([D(2026, 11, 11)]); // organisation holiday, a Wednesday
    static readonly Dictionary<string, DateOnly> Dates = new() { ["A"] = D(2026, 11, 9), ["B"] = D(2026, 11, 12), ["C"] = D(2026, 11, 16) };
    const string L2Flag = "Starts 2026-11-12 before P2 is due 2026-11-13", L4Flag = "Starts 2026-11-12 before P4 starts 2026-11-13", L5Flag = "Due 2026-11-19 before P5 is due 2026-11-20";

    /// SYN-105 as it stands on `today`, each item in the status the table gives it on that date. Returns the successors S1–S6.
    static (Scenario S, TaskSnap[] Successors, TaskSnap[] Predecessors) Syn105(DateOnly today, bool workingDays = true)
    {
        var s = new Scenario { Today = today, Calendar = Nov11Holiday };
        s.Settings = s.Settings with { WorkingDaysEnabled = workingDays, TaskDueSoonDays = 5 };
        bool byB = today >= Dates["B"], byC = today >= Dates["C"];
        var p1 = s.Task("P1", byB ? TaskStatuses.Complete : TaskStatuses.InProgress, due: D(2026, 11, 13), completed: byB ? D(2026, 11, 10) : null);
        var s1 = s.Task("S1", TaskStatuses.NotStarted, start: D(2026, 11, 16), due: D(2026, 11, 27));
        var p2 = s.Task("P2", byB ? TaskStatuses.Complete : TaskStatuses.InProgress, due: D(2026, 11, 10), completed: byB ? D(2026, 11, 10) : null);
        var s2 = s.Task("S2", TaskStatuses.NotStarted, start: D(2026, 11, 12), due: D(2026, 11, 20));
        var p3 = s.Task("P3", byB ? TaskStatuses.InProgress : TaskStatuses.NotStarted, start: D(2026, 11, 9), due: D(2026, 11, 20), startedOn: byB ? D(2026, 11, 10) : null);
        var s3 = s.Task("S3", TaskStatuses.NotStarted, start: D(2026, 11, 10), due: D(2026, 11, 20));
        var p4 = s.Task("P4", byB ? TaskStatuses.InProgress : TaskStatuses.NotStarted, start: D(2026, 11, 10), due: D(2026, 11, 27), startedOn: byB ? D(2026, 11, 10) : null);
        var s4 = s.Task("S4", TaskStatuses.NotStarted, start: D(2026, 11, 12), due: D(2026, 11, 27));
        var p5 = s.Task("P5", TaskStatuses.InProgress, due: D(2026, 11, 20), startedOn: D(2026, 11, 2));
        var s5 = s.Task("S5", byC ? TaskStatuses.Complete : TaskStatuses.InProgress, start: D(2026, 11, 9), due: D(2026, 11, 19), completed: byC ? D(2026, 11, 16) : null);
        var p6 = s.Task("P6", TaskStatuses.NotStarted, due: D(2026, 11, 13));
        var s6 = s.Task("S6", TaskStatuses.InProgress, start: D(2026, 11, 9), due: D(2026, 11, 16), startedOn: D(2026, 11, 9));
        s.Depends(s1, p1);                                       // L1 FS 0
        s.Depends(s2, p2, 2);                                    // L2 FS 2
        s.Depends(s3, p3, 0, DependencyTypes.StartToStart);      // L3 SS 0
        s.Depends(s4, p4, 2, DependencyTypes.StartToStart);      // L4 SS 2
        s.Depends(s5, p5, 0, DependencyTypes.FinishToFinish);    // L5 FF 0
        s.Depends(s6, p6, 1, DependencyTypes.FinishToFinish);    // L6 FF 1
        return (s, [s1, s2, s3, s4, s5, s6], [p1, p2, p3, p4, p5, p6]);
    }

    static string State(TaskResult r) => r.IsBlocked ? "Blocked" : r.IsWaiting ? "Waiting" : "Clear";
    static string State(DeliverableResult r) => r.IsBlocked ? "Blocked" : r.IsWaiting ? "Waiting" : "Clear";

    [Theory] // Columns L1..L6. Date flags apply while the successor is not terminal.
    [InlineData("A", "Waiting", "Waiting", "Waiting", "Waiting", "Clear", "Clear", null, L2Flag, null, L4Flag, L5Flag, null)]
    [InlineData("B", "Clear", "Blocked", "Clear", "Blocked", "Clear", "Clear", null, L2Flag, null, L4Flag, L5Flag, null)]
    [InlineData("C", "Clear", "Clear", "Clear", "Clear", "Clear", "Clear", null, L2Flag, null, L4Flag, null, null)]
    public void Worked_example_matches_the_table_on_each_date(string date, string l1, string l2, string l3, string l4, string l5, string l6,
        string? f1, string? f2, string? f3, string? f4, string? f5, string? f6)
    {
        var (s, succ, _) = Syn105(Dates[date]);
        var r = s.Run();
        Assert.Equal([l1, l2, l3, l4, l5, l6], succ.Select(x => State(r.T(x))));
        Assert.Equal([f1, f2, f3, f4, f5, f6], succ.Select(x => r.T(x).Inconsistencies.SingleOrDefault()?.Text));
        Assert.All(succ.Select(x => r.T(x).Inconsistencies.SingleOrDefault()), x => Assert.True(x is null || x.Rule == "D-12"));
    }

    [Fact]
    public void Worked_example_reasons_and_the_finish_link_note() // AC-DEPT-02, AC-DEPT-03
    {
        var (s, succ, pred) = Syn105(Dates["B"]);
        var r = s.Run();
        Assert.Equal("lag 2 days", r.T(succ[1]).Blockers.Single().Reason); // L2: the holiday moves the lag end to Fri 11-13
        Assert.Equal(("P4", "start link, lag 2 days"), (r.T(succ[3]).Blockers.Single().Key, r.T(succ[3]).Blockers.Single().Reason)); // L4 names the start link and its lag
        Assert.Equal([succ[3].Id], r.T(pred[3]).BlockingTaskIds); // a started predecessor inside the lag is Blocking Others
        Assert.Empty(r.T(pred[4]).BlockingTaskIds); // L5: a finish link never holds a start
        Assert.DoesNotContain("finished_before_predecessor", r.T(succ[4]).Notes); // S5 not finished yet

        var c = Syn105(Dates["C"]);
        var rc = c.S.Run();
        Assert.Contains("finished_before_predecessor", rc.T(c.Successors[4]).Notes); // L5 on C: the note replaces the flag
        Assert.DoesNotContain("finished_before_predecessor", rc.T(c.Successors[5]).Notes); // L6: S6 still In Progress

        var a = Syn105(Dates["A"]);
        Assert.Equal("start link", a.S.Run().T(a.Successors[2]).Blockers.Single().Reason); // L3 on A: P3 has not started
    }

    [Fact]
    public void L3_would_be_blocked_under_the_old_finish_to_start_rule()
    {
        var (s, succ, _) = Syn105(Dates["B"]);
        s.Replace(s.Deps, s.Deps[2], s.Deps[2] with { Type = DependencyTypes.FinishToStart });
        Assert.True(s.Run().T(succ[2]).IsBlocked);
    }

    [Fact]
    public void Control_with_working_days_off_clears_L2_and_L4_on_date_B() // §8.2 control
    {
        var (s, succ, _) = Syn105(Dates["B"], workingDays: false);
        var r = s.Run();
        Assert.Equal(["Clear", "Clear", "Clear", "Clear", "Clear", "Clear"], succ.Select(x => State(r.T(x))));
        Assert.Equal([null, null, null, null, L5Flag, null], succ.Select(x => r.T(x).Inconsistencies.SingleOrDefault()?.Text)); // lag ends Thu 11-12
    }

    [Fact]
    public void Deliverable_links_repeat_L2_and_L4() // AC-DEPT-02b: Issued counts as finish; StartedOn = left Not Started
    {
        foreach (var (date, l2, l4) in new[] { ("A", "Waiting", "Waiting"), ("B", "Blocked", "Blocked"), ("C", "Clear", "Clear") })
        {
            var today = Dates[date];
            bool byB = today >= Dates["B"];
            var s = new Scenario { Today = today, Calendar = Nov11Holiday };
            s.Settings = s.Settings with { WorkingDaysEnabled = true, DeliverableDueSoonDays = 5 }; // the same due-soon window as the task table
            var p2 = s.Deliverable("P2", D(2026, 11, 10), status: byB ? DeliverableStatus.Issued : DeliverableStatus.InProgress, issued: byB ? D(2026, 11, 10) : null);
            var s2 = s.Deliverable("S2", D(2026, 11, 20), status: DeliverableStatus.NotStarted, start: D(2026, 11, 12));
            var p4 = s.Deliverable("P4", D(2026, 11, 27), status: byB ? DeliverableStatus.InProgress : DeliverableStatus.NotStarted, start: D(2026, 11, 10), startedOn: byB ? D(2026, 11, 10) : null);
            var s4 = s.Deliverable("S4", D(2026, 11, 27), status: DeliverableStatus.NotStarted, start: D(2026, 11, 12));
            s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), p2.Id, s2.Id, 2));
            s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), p4.Id, s4.Id, 2, DependencyTypes.StartToStart));
            var r = s.Run();
            Assert.Equal((l2, l4), (State(r.D(s2)), State(r.D(s4))));
            Assert.Equal((L2Flag, L4Flag), (r.D(s2).Inconsistencies.Single().Text, r.D(s4).Inconsistencies.Single().Text));
            if (date == "B") Assert.Equal(("lag 2 days", "start link, lag 2 days"), (r.D(s2).Blockers.Single().Reason, r.D(s4).Blockers.Single().Reason));
        }
    }

    [Fact]
    public void A_start_link_to_a_cancelled_predecessor_is_satisfied() // D-04
    {
        var s = new Scenario();
        var p = s.Task("P", TaskStatuses.Cancelled, start: s.Today.AddDays(5));
        var t = s.Task("S", TaskStatuses.NotStarted, start: s.Today.AddDays(-1), due: s.Today.AddDays(20));
        s.Depends(t, p, 3, DependencyTypes.StartToStart);
        var r = s.Run().T(t);
        Assert.Equal("Clear", State(r));
        Assert.Contains("predecessor_cancelled", r.Notes);
    }

    [Fact]
    public void A_start_link_date_check_needs_both_start_dates() // AC-DEPT-04
    {
        var s = new Scenario();
        var p = s.Task("P", TaskStatuses.NotStarted, due: s.Today.AddDays(30)); // no start date
        var t = s.Task("S", TaskStatuses.NotStarted, start: s.Today.AddDays(1), due: s.Today.AddDays(10)); // due before P is due: irrelevant to a start link
        s.Depends(t, p, 2, DependencyTypes.StartToStart);
        Assert.False(s.Run().T(t).IsDateInconsistent);
        s.Replace(s.Tasks, p, p with { StartDate = s.Today.AddDays(5) });
        Assert.True(s.Run().T(t).IsDateInconsistent);
        s.Replace(s.Tasks, t, t with { StartDate = null });
        Assert.False(s.Run().T(t).IsDateInconsistent);
    }

    [Fact]
    public void Satisfaction_rule_is_one_pure_function() // FR-DEPT-03, AC-DEPT-05
    {
        var today = D(2026, 11, 12);
        var after = DependencyRules.After(Nov11Holiday);
        Assert.Equal(D(2026, 11, 13), after(D(2026, 11, 10), 2));
        Assert.Equal(D(2026, 11, 12), DependencyRules.After(null)(D(2026, 11, 10), 2));
        Assert.Equal(LinkState.InLag, DependencyRules.State(DependencyTypes.FinishToStart, TaskStatuses.Complete, null, D(2026, 11, 10), 2, after, today));
        Assert.Equal(LinkState.InLag, DependencyRules.State(DependencyTypes.FinishToStart, DeliverableStatus.Accepted, null, D(2026, 11, 10), 2, after, today));
        Assert.Equal(LinkState.Waiting, DependencyRules.State(DependencyTypes.FinishToStart, DeliverableStatus.ReadyToIssue, null, null, 0, after, today));
        Assert.Equal(LinkState.Waiting, DependencyRules.State(DependencyTypes.StartToStart, TaskStatuses.OnHold, null, null, 0, after, today));
        Assert.True(DependencyRules.Satisfied(DependencyTypes.StartToStart, TaskStatuses.InProgress, null, null, 2, after, today)); // started with no date: nothing to count from
        Assert.True(DependencyRules.Satisfied(DependencyTypes.StartToStart, TaskStatuses.OnHold, D(2026, 11, 2), null, 2, after, today)); // started earlier, then held
        Assert.True(DependencyRules.Satisfied(DependencyTypes.StartToStart, TaskStatuses.Complete, null, D(2026, 11, 2), 2, after, today));
        Assert.True(DependencyRules.Satisfied(DependencyTypes.FinishToFinish, TaskStatuses.NotStarted, null, null, 5, after, today));
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
