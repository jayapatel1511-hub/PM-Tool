using Hub.Domain;

namespace Hub.Tests.Domain;

/// §55.3 Backlog (packet 051 S05): the derived rule (FR-BKL-01) and its exclusions from date indicators, the Workload
/// forecast, milestone status and health (FR-BKL-05, AC-BKL-05). Readiness is covered in Api/TaskQueriesTests.
public sealed class BacklogTests
{
    static OrgSettings Flag(bool on) => new OrgSettings() with { Flags = new Dictionary<string, bool> { [TaskRules.BacklogFlag] = on } };

    [Fact]
    [Trait("Spec", "FR-BKL-01")]
    public void Only_open_undated_tasks_are_in_the_backlog()
    {
        var day = new DateOnly(2026, 9, 11);
        foreach (var status in TaskStatuses.All)
            foreach (var (start, due) in new (DateOnly?, DateOnly?)[] { (null, null), (day, null), (null, day), (day, day) })
                Assert.Equal(status is TaskStatuses.NotStarted or TaskStatuses.OnHold && start is null && due is null, TaskRules.IsBacklog(status, start, due));
    }

    [Theory, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [InlineData(false), InlineData(true)]
    [Trait("Spec", "FR-BKL-05")]
    public void Backlog_tasks_are_never_overdue_due_soon_or_date_inconsistent(bool on)
    {
        var s = new Scenario { Settings = Flag(on) };
        var past = s.Today.AddDays(-5);
        var m = s.Milestone("M1", past);
        var late = s.Deliverable("D1", due: past, milestone: m.Id, originalDue: past.AddDays(-10));
        var pred = s.Task("T1", due: past, start: past.AddDays(-3));
        var waiting = s.Task("T2", TaskStatuses.NotStarted, deliverable: late.Id);
        var held = s.Task("T3", TaskStatuses.OnHold, milestone: m.Id);
        s.Depends(waiting, pred);
        var r = s.Run();
        foreach (var t in new[] { waiting, held })
        {
            var x = r.T(t);
            Assert.False(x.IsOverdue || x.IsDueSoon || x.IsDateInconsistent || x.IsHeldPastDue, t.Key);
        }
        // Slipped is a deliverable and milestone indicator measured from their own dates; backlog tasks do not move it.
        var without = new Scenario { Settings = Flag(on) };
        var m2 = without.Milestone("M1", past);
        var late2 = without.Deliverable("D1", due: past, milestone: m2.Id, originalDue: past.AddDays(-10));
        var r2 = without.Run();
        Assert.Equal(r2.D(late2).SlipDays, r.D(late).SlipDays);
        Assert.Equal(r2.M(m2).SlipDays, r.M(m).SlipDays);
    }

    [Fact]
    [Trait("Spec", "AC-BKL-05")]
    public void A_backlog_estimate_adds_nothing_to_any_week_and_stays_unscheduled()
    {
        var load = Workload.Spread(new LoadTask(Guid.NewGuid(), Guid.NewGuid(), 40, 0, null, null), new DateOnly(2026, 9, 11));
        Assert.Empty(load.ByWeek);
        Assert.Equal(40, load.NoDueDate);
        Assert.False(load.Overdue);
    }

    [Theory, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [InlineData(false), InlineData(true)]
    [Trait("Spec", "FR-BKL-05")]
    public void Health_and_milestone_status_leave_out_backlog_tasks_only_with_the_flag_on(bool on)
    {
        var s = new Scenario { Settings = Flag(on) };
        var m = s.Milestone("M1", s.Today.AddDays(30));
        for (var i = 0; i < 3; i++) s.Task($"O{i}", due: s.Today.AddDays(-2));
        for (var i = 0; i < 6; i++) s.Task($"F{i}", due: s.Today.AddDays(20));
        for (var i = 0; i < 6; i++) s.Task($"B{i}", TaskStatuses.NotStarted);
        var blocked = s.Task("BB", TaskStatuses.NotStarted, milestone: m.Id, block: "Waiting on client");
        s.PreviousBlocked[blocked.Id] = s.Today.AddDays(-10);
        var r = s.Run();

        Assert.True(r.T(blocked).IsBlocked); // the Blocked indicator itself is not a date indicator and stays
        // Off: 3 overdue of 16 open is 19 %, Yellow, and the blocked backlog task puts the milestone At Risk (today's figures).
        // On: 3 of 9 is 33 %, Red, exactly as without the backlog, and the milestone is On Track.
        Assert.Equal(on ? 9 : 16, r.Project.Health.Inputs["open_tasks"]);
        Assert.Equal(on ? Health.Red : Health.Yellow, r.Project.Health.Health);
        Assert.Equal(on ? 0 : 1, r.Project.Health.Inputs["task_blocked_long"]);
        Assert.Equal(on ? MilestoneStatus.OnTrack : MilestoneStatus.AtRisk, r.M(m).Status);
        // Counts keep every task (FR-BKL-05: they stay in the open-task count).
        Assert.Equal(16, r.Project.Counts["tasksTotal"]);
        Assert.Equal(1, r.M(m).TaskBlocked);
    }
}
