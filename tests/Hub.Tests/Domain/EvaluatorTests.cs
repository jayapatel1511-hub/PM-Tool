using Hub.Domain;

namespace Hub.Tests.Domain;

/// Builds small projects for the pure rules engine.
sealed class Scenario
{
    public DateOnly Today = new(2026, 9, 11);
    public OrgSettings Settings = new();
    public string Status = ProjectStatus.Active;
    public readonly Guid Pm = Guid.NewGuid(), CivilLead = Guid.NewGuid(), GeoLead = Guid.NewGuid(), Alex = Guid.NewGuid(), Sam = Guid.NewGuid(), Diane = Guid.NewGuid(), Supervisor = Guid.NewGuid();
    public readonly Guid Civil = Guid.NewGuid(), Geo = Guid.NewGuid();
    public readonly List<UserSnap> Users;
    public readonly List<TaskSnap> Tasks = [];
    public readonly List<DeliverableSnap> Deliverables = [];
    public readonly List<MilestoneSnap> Milestones = [];
    public readonly List<DependencySnap> Deps = [];
    public readonly List<DecisionSnap> Decisions = [];
    public readonly List<SnoozeSnap> Snoozes = [];
    public readonly List<IssueSnap> Issues = [];
    public readonly List<DependencySnap> DeliverableDeps = [];
    public readonly List<RiskSnap> Risks = [];
    public readonly List<ActionSnap> Actions = [];
    public HashSet<Guid>? ActiveMembers;
    public string? OverrideHealth;
    public DateTimeOffset? OverrideExpires;
    public Dictionary<Guid, DateOnly> PreviousBlocked = [];
    public List<DisciplineSnap> Disciplines;
    public WorkCalendar? Calendar;

    public Scenario()
    {
        Users = [new(Pm, "Priya", true, null), new(CivilLead, "Marc", true, null), new(GeoLead, "Omar", true, null), new(Alex, "Alex", true, Supervisor),
            new(Sam, "Sam", true, Supervisor), new(Diane, "Diane", true, null), new(Supervisor, "Sup", true, null)];
        Disciplines = [new(Civil, "Civil", CivilLead, true, 0), new(Geo, "Geotechnical", GeoLead, true, 1)];
    }

    public TaskSnap Task(string key, string status = TaskStatuses.InProgress, DateOnly? start = null, DateOnly? due = null, Guid? assignee = null, Guid? discipline = null,
        Guid? deliverable = null, Guid? milestone = null, string priority = Priority.Medium, DateOnly? lastActivity = null, decimal? est = null, int progress = 0,
        string? block = null, DateOnly? reviewRequested = null, Guid? reviewer = null, int dueChanges = 0, DateOnly? completed = null, bool unassigned = false)
    {
        var t = new TaskSnap(Guid.NewGuid(), key, key + " name", discipline ?? Civil, deliverable, milestone, unassigned ? null : assignee ?? Alex, reviewer, status, priority,
            start, due, progress, est, block, block is null ? null : "reason text", block is null ? null : Today.AddDays(-3), lastActivity ?? Today, reviewRequested, dueChanges, completed);
        Tasks.Add(t);
        return t;
    }

    public DeliverableSnap Deliverable(string key, DateOnly? due = null, Guid? milestone = null, string status = DeliverableStatus.InProgress, Guid? owner = null, Guid? discipline = null,
        DateOnly? originalDue = null, DateOnly? start = null, DateOnly? issued = null)
    {
        var d = new DeliverableSnap(Guid.NewGuid(), key, key + " name", discipline ?? Civil, owner ?? CivilLead, null, milestone, start, due, originalDue ?? due, status, Priority.Medium, Today, issued);
        Deliverables.Add(d);
        return d;
    }

    public MilestoneSnap Milestone(string key, DateOnly? date, string type = MilestoneType.DesignSubmission, bool complete = false, DateOnly? original = null)
    {
        var m = new MilestoneSnap(Guid.NewGuid(), key, key + " name", type, date, original ?? date, complete, false, null);
        Milestones.Add(m);
        return m;
    }

    public void Depends(TaskSnap successor, TaskSnap predecessor, int lag = 0) => Deps.Add(new DependencySnap(Guid.NewGuid(), predecessor.Id, successor.Id, lag));

    public EvalResult Run() => Evaluator.Evaluate(new EvalInput(
        new ProjectSnap(Guid.NewGuid(), "1234", Status, Pm, [Pm], OverrideHealth, OverrideExpires, new DateTimeOffset(Today.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero)),
        Today, Settings, Users, Disciplines, Milestones, Deliverables, Tasks, Deps, Decisions,
        ActiveMembers ?? Users.Select(u => u.Id).ToHashSet(), PreviousBlocked, Snoozes, Issues, Risks, Actions, DeliverableDeps, Calendar));

    public void Deactivate(Guid id) { var i = Users.FindIndex(u => u.Id == id); Users[i] = Users[i] with { IsActive = false }; }
    public T Replace<T>(List<T> list, T old, T updated) { list[list.IndexOf(old)] = updated; return updated; }
}

static class R
{
    public static TaskResult T(this EvalResult r, TaskSnap t) => r.Tasks.Single(x => x.TaskId == t.Id);
    public static DeliverableResult D(this EvalResult r, DeliverableSnap d) => r.Deliverables.Single(x => x.DeliverableId == d.Id);
    public static MilestoneResult M(this EvalResult r, MilestoneSnap m) => r.Milestones.Single(x => x.MilestoneId == m.Id);
    public static List<AttentionResult> A(this EvalResult r, Guid itemId) => r.Attention.Where(a => a.ItemId == itemId).ToList();
}

public sealed class EvaluatorTests
{
    static DateOnly D(int m, int d, int y = 2026) => new(y, m, d);

    // ---------- §15.12 worked examples ----------

    (Scenario S, TaskSnap T31, TaskSnap T42, MilestoneSnap M60) Example(DateOnly today, DateOnly t42Due, DateOnly t31Due)
    {
        var s = new Scenario { Today = today };
        var m60 = s.Milestone("1234-M04", D(1, 29, 2027));
        var geoDel = s.Deliverable("1234-D020", D(1, 15, 2027), m60.Id, discipline: s.Geo, owner: s.GeoLead);
        var civDel = s.Deliverable("1234-D012", D(1, 26, 2027), m60.Id);
        var t31 = s.Task("1234-T0031", due: t31Due, discipline: s.Geo, assignee: s.Sam, deliverable: geoDel.Id);
        var t42 = s.Task("1234-T0042", TaskStatuses.NotStarted, start: D(9, 15), due: t42Due, deliverable: civDel.Id);
        s.Depends(t42, t31);
        return (s, t31, t42, m60);
    }

    [Fact]
    public void Example1_overdue_predecessor_blocks_its_successor()
    {
        var (s, t31, t42, m60) = Example(D(9, 11), D(9, 30), D(9, 10));
        var r = s.Run();
        Assert.True(r.T(t31).IsOverdue);
        Assert.Equal(1, r.T(t31).DaysOverdue);
        Assert.True(r.T(t42).IsBlocked);
        Assert.False(r.T(t42).IsWaiting);
        Assert.True(r.T(t31).IsBlocking);
        Assert.Contains(t42.Id, r.T(t31).BlockingTaskIds);
        Assert.Contains(r.A(t31.Id), a => a.RuleId == "A-01" && a.Severity == Severity.Warning);
        Assert.Contains(r.A(t31.Id), a => a.RuleId == "A-03" && a.Severity == Severity.Critical);
        Assert.Contains(r.A(t42.Id), a => a.RuleId == "A-02" && a.Severity == Severity.Warning);
        Assert.Contains(m60.Id, r.T(t31).AffectedMilestoneIds);
        Assert.Contains(m60.Id, r.T(t42).AffectedMilestoneIds);
        Assert.Equal(t31.Key, r.T(t42).Blockers.Single().Key);
    }

    [Fact]
    public void Example2_normal_waiting_raises_nothing()
    {
        var (s, t31, t42, _) = Example(D(9, 5), D(9, 30), D(9, 10));
        var r = s.Run();
        Assert.True(r.T(t42).IsWaiting);
        Assert.False(r.T(t42).IsBlocked);
        Assert.Empty(r.A(t42.Id));
        Assert.False(r.T(t31).IsOverdue);
    }

    [Fact]
    public void Example3_due_soon_successor_is_blocked_before_the_predecessor_is_late()
    {
        var (s, t31, t42, _) = Example(D(9, 14), D(9, 17), D(9, 16));
        var r = s.Run();
        Assert.False(r.T(t31).IsOverdue);
        Assert.True(r.T(t42).IsBlocked);
    }

    // ---------- Dependencies (AC-DEP-03..09) ----------

    [Fact]
    public void Waiting_not_blocked_when_nothing_is_wrong_yet() // AC-DEP-03
    {
        var s = new Scenario();
        var a = s.Task("A", due: s.Today.AddDays(10));
        var b = s.Task("B", TaskStatuses.NotStarted, start: s.Today.AddDays(12), due: s.Today.AddDays(20));
        s.Depends(b, a);
        var r = s.Run();
        Assert.True(r.T(b).IsWaiting);
        Assert.Empty(r.A(b.Id));
    }

    [Fact]
    public void Completing_or_cancelling_the_predecessor_clears_the_successor() // AC-DEP-05, AC-DEP-06, D-04
    {
        foreach (var st in new[] { TaskStatuses.Complete, TaskStatuses.Cancelled })
        {
            var s = new Scenario();
            var a = s.Task("A", st, due: s.Today.AddDays(-2));
            var b = s.Task("B", TaskStatuses.InProgress, due: s.Today.AddDays(5));
            s.Depends(b, a);
            var r = s.Run();
            Assert.False(r.T(b).IsBlocked);
            Assert.False(r.T(b).IsWaiting);
            Assert.Equal(st == TaskStatuses.Cancelled, r.T(b).Notes.Contains("predecessor_cancelled"));
        }
    }

    [Fact]
    public void Predecessor_on_hold_stays_unsatisfied()
    {
        var s = new Scenario();
        var a = s.Task("A", TaskStatuses.OnHold, due: s.Today.AddDays(3));
        var b = s.Task("B", TaskStatuses.InProgress, due: s.Today.AddDays(30));
        s.Depends(b, a);
        var r = s.Run();
        Assert.True(r.T(b).IsBlocked);
        Assert.Equal("predecessor on hold", r.T(b).Blockers.Single().Reason);
    }

    [Fact]
    public void Successor_starting_before_predecessor_due_is_date_inconsistent() // AC-DEP-08, D-12
    {
        var s = new Scenario();
        var a = s.Task("A", due: s.Today.AddDays(20));
        var b = s.Task("B", TaskStatuses.NotStarted, start: s.Today.AddDays(10), due: s.Today.AddDays(30));
        s.Depends(b, a);
        var r = s.Run();
        Assert.True(r.T(b).IsDateInconsistent);
        Assert.Contains("A", r.T(b).Inconsistencies.Single().Text);
    }

    [Fact]
    public void Manual_block_blocks_with_reason_and_days() // AC-DEP-09, D-14
    {
        var s = new Scenario();
        var t = s.Task("T", block: BlockType.Client, due: s.Today.AddDays(30));
        var r = s.Run();
        Assert.True(r.T(t).IsBlocked);
        Assert.Equal(3, r.T(t).DaysBlocked);
        Assert.Equal("reason text", r.T(t).Blockers.Single().Reason);
        var cleared = s.Tasks.Select(x => x == t ? x with { ManualBlockType = null, ManualBlockReason = null, ManualBlockSince = null } : x).ToList();
        s.Tasks.Clear(); s.Tasks.AddRange(cleared);
        Assert.False(s.Run().Tasks.Single().IsBlocked);
    }

    [Fact]
    public void Blocked_since_carries_over_between_evaluations()
    {
        var s = new Scenario();
        var a = s.Task("A", due: s.Today.AddDays(-1));
        var b = s.Task("B", due: s.Today.AddDays(30));
        s.Depends(b, a);
        s.PreviousBlocked[b.Id] = s.Today.AddDays(-6);
        var r = s.Run();
        Assert.Equal(6, r.T(b).DaysBlocked);
        Assert.Contains("Tasks blocked longer than 5 days: 1", string.Join("|", r.Project.Health.Reasons.Select(x => x.Text)));
    }

    [Fact]
    public void Affected_milestones_follow_the_chain_to_the_depth_limit() // D-13
    {
        var s = new Scenario();
        var m1 = s.Milestone("M1", s.Today.AddDays(40));
        var m2 = s.Milestone("M2", s.Today.AddDays(80));
        var a = s.Task("A", due: s.Today.AddDays(-1), milestone: m1.Id);
        var b = s.Task("B", milestone: m1.Id);
        var c = s.Task("C", milestone: m2.Id);
        s.Depends(b, a); s.Depends(c, b);
        Assert.Equal([m1.Id, m2.Id], s.Run().T(a).AffectedMilestoneIds);
        s.Settings = s.Settings with { ChainDepthLimit = 1 };
        Assert.Equal([m1.Id], s.Run().T(a).AffectedMilestoneIds);
    }

    [Fact]
    public void Dependency_lag_keeps_the_successor_waiting_until_it_passes() // packet 021
    {
        var s = new Scenario { Today = D(2, 5, 2027) };
        var a = s.Task("A", TaskStatuses.Complete, due: D(2, 1, 2027), completed: D(2, 1, 2027));
        var b = s.Task("B", TaskStatuses.NotStarted, start: D(2, 20, 2027), due: D(3, 1, 2027));
        s.Depends(b, a, lag: 10);
        Assert.True(s.Run().T(b).IsWaiting);
        s.Today = D(2, 11, 2027);
        Assert.False(s.Run().T(b).IsWaiting);
    }

    [Fact]
    public void Graph_reports_the_loop_a_new_edge_would_close() // AC-DEP-02, D-03
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), c = Guid.NewGuid();
        var edges = new[] { (a, b), (b, c) };
        Assert.Equal([a, b, c, a], Graph.CycleIfAdded(edges, c, a));
        Assert.Null(Graph.CycleIfAdded(edges, a, c));
        Assert.Equal([a, a], Graph.CycleIfAdded(edges, a, a));
    }

    // ---------- Task indicators and attention (AC-TSK, AC-ATT, AC-REV) ----------

    [Fact]
    public void Overdue_and_held_past_due() // AC-TSK-04, AC-TSK-05, T-06
    {
        var s = new Scenario();
        var t = s.Task("T", due: s.Today.AddDays(-1));
        Assert.Equal(1, s.Run().T(t).DaysOverdue);
        s.Tasks[0] = t with { Status = TaskStatuses.OnHold };
        var r = s.Run();
        Assert.False(r.Tasks[0].IsOverdue);
        Assert.True(r.Tasks[0].IsHeldPastDue);
        Assert.Equal(0, r.Project.Counts["tasksOverdue"]);
    }

    [Fact]
    public void Unassigned_task_starting_today_alerts_lead_and_pm() // AC-TSK-01, A-08
    {
        var s = new Scenario();
        var t = s.Task("T", TaskStatuses.NotStarted, start: s.Today, unassigned: true);
        var a = s.Run().A(t.Id).Single(x => x.RuleId == "A-08");
        Assert.Equal(Severity.Warning, a.Severity);
        Assert.Equal([s.CivilLead, s.Pm], a.RouteTo);
    }

    [Fact]
    public void Stale_work_after_the_threshold_and_rule_switch() // AC-TSK-06, AC-ATT-07
    {
        var s = new Scenario();
        var t = s.Task("T", lastActivity: s.Today.AddDays(-11), due: s.Today.AddDays(30));
        var r = s.Run();
        Assert.True(r.T(t).IsStale);
        var a = r.A(t.Id).Single(x => x.RuleId == "A-10");
        Assert.Equal(Severity.Info, a.Severity);
        Assert.Equal([s.Alex, s.CivilLead], a.RouteTo);
        s.Settings = s.Settings with { RuleEnabled = Rules.Ids.ToDictionary(x => x, x => x != "A-10") };
        r = s.Run();
        Assert.True(r.T(t).IsStale);
        Assert.DoesNotContain(r.A(t.Id), x => x.RuleId == "A-10");
    }

    [Fact]
    public void Overdue_task_blocking_another_is_the_top_critical_item() // AC-ATT-01
    {
        var s = new Scenario();
        var a = s.Task("1234-T0031", due: s.Today.AddDays(-6));
        var b = s.Task("1234-T0057", due: s.Today.AddDays(30));
        s.Task("1234-T0099", due: s.Today.AddDays(-2));
        s.Depends(b, a);
        var top = s.Run().Attention.First();
        Assert.Equal("A-03", top.RuleId);
        Assert.Equal(Severity.Critical, top.Severity);
        Assert.Contains("1234-T0057", top.Message);
        Assert.Contains("6 days overdue", top.Message);
    }

    [Fact]
    public void Snoozes_hide_until_severity_rises() // AC-ATT-02, AC-ATT-03, ATT-03
    {
        var s = new Scenario();
        var t = s.Task("T", due: s.Today.AddDays(-2));
        s.Snoozes.Add(new SnoozeSnap("A-01", ItemType.Task, t.Id, DateTimeOffset.MaxValue, Severity.Warning));
        Assert.True(s.Run().A(t.Id).Single(x => x.RuleId == "A-01").Snoozed);
        s.Tasks[0] = t with { DueDate = s.Today.AddDays(-7) }; // now Critical (> 5 days)
        Assert.False(s.Run().A(t.Id).Single(x => x.RuleId == "A-01").Snoozed);
    }

    [Fact]
    public void Projects_that_are_not_active_produce_nothing() // AC-ATT-04, AC-PRJ-03, G-05
    {
        foreach (var st in new[] { ProjectStatus.Setup, ProjectStatus.OnHold })
        {
            var s = new Scenario { Status = st };
            s.Task("T", due: s.Today.AddDays(-5));
            var r = s.Run();
            Assert.Empty(r.Attention);
            Assert.False(r.Tasks[0].IsOverdue);
            Assert.Equal(Health.Grey, r.Project.Health.Health);
            Assert.Equal(st == ProjectStatus.OnHold ? "Project on hold" : "Project in setup", r.Project.Health.Reasons.Single().Text);
        }
    }

    [Fact]
    public void Inactive_assignee_is_critical_for_lead_pm_and_supervisor() // AC-ATT-05, A-18
    {
        var s = new Scenario();
        s.Users[3] = s.Users[3] with { IsActive = false };
        var t = s.Task("T");
        var a = s.Run().A(t.Id).Single(x => x.RuleId == "A-18");
        Assert.Equal(Severity.Critical, a.Severity);
        Assert.Equal([s.CivilLead, s.Pm, s.Supervisor], a.RouteTo);
    }

    [Fact]
    public void Inactive_discipline_lead_raises_a18_and_yellow_health() // TM-07
    {
        var s = new Scenario();
        s.Users[1] = s.Users[1] with { IsActive = false };
        s.Task("T", due: s.Today.AddDays(30));
        var r = s.Run();
        Assert.Contains(r.Attention, a => a.RuleId == "A-18" && a.ItemType == ItemType.Discipline);
        Assert.Equal(Health.Yellow, r.Project.Health.Health);
    }

    [Fact]
    public void Stalled_review_and_repeated_slips() // AC-REV-03, AC-TSK-10
    {
        var s = new Scenario();
        var t = s.Task("T", TaskStatuses.ReadyForReview, reviewRequested: s.Today.AddDays(-6), reviewer: s.Diane, due: s.Today.AddDays(20), dueChanges: 3);
        var r = s.Run();
        var a = r.A(t.Id).Single(x => x.RuleId == "A-11");
        Assert.Equal(Severity.Warning, a.Severity);
        Assert.Equal([s.Diane, s.CivilLead, s.Pm], a.RouteTo);
        Assert.Contains(r.A(t.Id), x => x.RuleId == "A-20" && x.Severity == Severity.Info);
        Assert.Contains("due_moved", r.T(t).Notes);
    }

    [Fact]
    public void Missing_due_date_and_task_after_deliverable() // A-09, A-16, T-04
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", s.Today.AddDays(20));
        var noDue = s.Task("T1", deliverable: d.Id);
        var late = s.Task("T2", TaskStatuses.NotStarted, due: s.Today.AddDays(25), deliverable: d.Id);
        var r = s.Run();
        Assert.True(r.T(noDue).IsMissingDueDate);
        Assert.Contains(r.A(noDue.Id), x => x.RuleId == "A-09" && x.Severity == Severity.Info);
        Assert.True(r.T(late).IsDateInconsistent);
        Assert.Contains(r.A(late.Id), x => x.RuleId == "A-16");
    }

    [Fact]
    public void Held_past_due_more_than_ten_days() // A-19
    {
        var s = new Scenario();
        var t = s.Task("T", TaskStatuses.OnHold, due: s.Today.AddDays(-11));
        Assert.Contains(s.Run().A(t.Id), x => x.RuleId == "A-19");
    }

    [Fact]
    public void Ranking_is_severity_then_days_then_priority_then_due() // ATT-04
    {
        var s = new Scenario();
        var low = s.Task("T1", due: s.Today.AddDays(-2));
        var high = s.Task("T2", due: s.Today.AddDays(-2), priority: Priority.High);
        var older = s.Task("T3", due: s.Today.AddDays(-4));
        var keys = s.Run().Attention.Where(a => a.RuleId == "A-01").Select(a => a.ItemKey).ToList();
        Assert.Equal(["T3", "T2", "T1"], keys);
        Assert.NotNull(low); Assert.NotNull(high); Assert.NotNull(older);
    }

    // ---------- Milestones (§16.2, AC-MS-01..03) ----------

    [Fact]
    public void Milestone_at_risk_with_unissued_deliverables_inside_the_window() // AC-MS-01, AC-MS-02
    {
        var s = new Scenario { Today = D(1, 16, 2027) };
        var m = s.Milestone("M04", D(1, 29, 2027));
        for (var n = 0; n < 3; n++) s.Deliverable($"D{n}", D(1, 26, 2027), m.Id, DeliverableStatus.Issued);
        s.Deliverable("D3", D(1, 26, 2027), m.Id);
        s.Deliverable("D4", D(1, 26, 2027), m.Id);
        var r = s.Run().M(m);
        Assert.Equal(MilestoneStatus.AtRisk, r.Status);
        Assert.Contains("2 of 5 deliverables not issued", r.Reasons.Single().Text);
        s.Deliverables.RemoveAll(d => d.Status != DeliverableStatus.Issued);
        s.Deliverable("D3", D(1, 26, 2027), m.Id, DeliverableStatus.Issued);
        s.Deliverable("D4", D(1, 26, 2027), m.Id, DeliverableStatus.Issued);
        Assert.Equal(MilestoneStatus.OnTrack, s.Run().M(m).Status);
    }

    [Fact]
    public void Milestone_overdue_is_critical_and_turns_health_red() // AC-MS-03
    {
        var s = new Scenario();
        var m = s.Milestone("M1", s.Today.AddDays(-1));
        var r = s.Run();
        Assert.Equal(MilestoneStatus.Overdue, r.M(m).Status);
        Assert.Contains(r.A(m.Id), a => a.RuleId == "A-14" && a.Severity == Severity.Critical);
        Assert.Equal(Health.Red, r.Project.Health.Health);
    }

    [Fact]
    public void Milestone_risk_conditions_b_d_and_e()
    {
        var s = new Scenario();
        var m = s.Milestone("M1", s.Today.AddDays(40));
        var late = s.Deliverable("D1", s.Today.AddDays(45), m.Id);
        s.Task("T1", due: s.Today.AddDays(-1), deliverable: late.Id);
        var sub = s.Milestone("M2", s.Today.AddDays(10));
        var kick = s.Milestone("M3", s.Today.AddDays(10), MilestoneType.Kickoff);
        var r = s.Run();
        Assert.Equal(2, r.M(m).Reasons.Count); // (b) overdue task and (e) deliverable due after
        Assert.Equal(MilestoneStatus.AtRisk, r.M(sub).Status); // (d) submission with nothing planned
        Assert.Equal(MilestoneStatus.OnTrack, r.M(kick).Status);
        Assert.Contains(r.A(sub.Id), a => a.RuleId == "A-12");
        Assert.True(r.D(late).IsDateInconsistent);
        Assert.Contains(r.A(late.Id), a => a.RuleId == "A-17");
    }

    [Fact]
    public void Approaching_milestone_attention_escalates_within_five_days() // A-05
    {
        var s = new Scenario();
        var m = s.Milestone("M1", s.Today.AddDays(4));
        s.Deliverable("D1", s.Today.AddDays(3), m.Id);
        var a = s.Run().A(m.Id).Single(x => x.RuleId == "A-05");
        Assert.Equal(Severity.Critical, a.Severity);
    }

    // ---------- Deliverables (DL-07, DL-08, AC-DEL-02, AC-DEL-06) ----------

    [Fact]
    public void Progress_by_count_rounded_down_to_five() // AC-DEL-02
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", s.Today.AddDays(30));
        for (var n = 0; n < 6; n++) s.Task($"C{n}", TaskStatuses.Complete, deliverable: d.Id);
        s.Task("X", TaskStatuses.Cancelled, deliverable: d.Id);
        s.Task("O", deliverable: d.Id);
        var r = s.Run().D(d);
        Assert.Equal(85, r.ProgressPct);
        Assert.Equal(6, r.TaskComplete);
        Assert.Equal(1, r.TaskCancelled);
    }

    [Fact]
    public void Progress_by_hours_when_every_task_is_estimated() // DL-07
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", s.Today.AddDays(30));
        s.Task("A", TaskStatuses.Complete, deliverable: d.Id, est: 30);
        s.Task("B", deliverable: d.Id, est: 10);
        Assert.Equal(75, s.Run().D(d).ProgressPct);
        Assert.Null(new Scenario().Run().Deliverables.FirstOrDefault()?.ProgressPct);
    }

    [Fact]
    public void Deliverable_due_soon_at_low_progress_is_at_risk() // AC-DEL-06, A-06
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", s.Today.AddDays(7));
        for (var n = 0; n < 7; n++) s.Task($"T{n}", deliverable: d.Id, due: s.Today.AddDays(6));
        for (var n = 0; n < 3; n++) s.Task($"C{n}", TaskStatuses.Complete, deliverable: d.Id);
        var r = s.Run();
        Assert.True(r.D(d).IsAtRisk);
        var a = r.A(d.Id).Single(x => x.RuleId == "A-06");
        Assert.Equal(Severity.Warning, a.Severity);
        Assert.Equal([s.CivilLead, s.Pm], a.RouteTo); // owner is the lead here, so owner and DL are one person
    }

    [Fact]
    public void Issued_with_open_work_slip_and_unowned() // DL-12, A-13
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", s.Today.AddDays(12), status: DeliverableStatus.Issued, originalDue: s.Today.AddDays(2));
        s.Task("T", deliverable: d.Id);
        var u = new DeliverableSnap(Guid.NewGuid(), "D2", "n", s.Civil, null, null, null, null, s.Today.AddDays(3), null, DeliverableStatus.NotStarted, Priority.Medium, s.Today);
        s.Deliverables.Add(u);
        var r = s.Run();
        Assert.True(r.D(d).IssuedWithOpenWork);
        Assert.Equal(10, r.D(d).SlipDays);
        Assert.Contains("deliverable_issued", r.Tasks.Single().Notes);
        Assert.Contains(r.A(u.Id), a => a.RuleId == "A-13");
    }

    [Fact]
    public void Explicit_deliverable_dependency_blocks_when_due_soon() // packet 021
    {
        var s = new Scenario();
        var a = s.Deliverable("A", s.Today.AddDays(20));
        var b = s.Deliverable("B", s.Today.AddDays(5), status: DeliverableStatus.NotStarted);
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), a.Id, b.Id));
        var r = s.Run();
        Assert.True(r.D(b).IsBlocked);
        Assert.Equal("A", r.D(b).Blockers.Single().Key);
    }

    // ---------- Health (§16.3, AC-HLT-01..04) ----------

    Scenario Open(int open, int overdue)
    {
        var s = new Scenario();
        s.Milestone("M1", s.Today.AddDays(60), MilestoneType.Kickoff);
        for (var n = 0; n < open; n++) s.Task($"T{n}", due: n < overdue ? s.Today.AddDays(-1) : s.Today.AddDays(30));
        return s;
    }

    [Fact]
    public void Overdue_percentage_gives_yellow_then_red() // AC-HLT-01, AC-HLT-02
    {
        var y = Open(41, 7).Run().Project.Health;
        Assert.Equal(Health.Yellow, y.Health);
        Assert.Contains(y.Reasons, r => r.Text == "Overdue tasks 7 of 41 (17%) ≥ 10%");
        Assert.Equal(Health.Red, Open(41, 11).Run().Project.Health.Health);
        Assert.Equal(Health.Green, Open(41, 2).Run().Project.Health.Health); // below the minimum count
    }

    [Fact]
    public void Nothing_to_evaluate_is_grey() // AC-HLT-04
    {
        var h = new Scenario().Run().Project.Health;
        Assert.Equal(Health.Grey, h.Health);
        Assert.Equal("Nothing to evaluate", h.Reasons.Single().Text);
    }

    [Fact]
    public void Deliverable_overdue_more_than_five_days_is_red()
    {
        var s = new Scenario();
        s.Deliverable("D1", s.Today.AddDays(-6));
        s.Task("T", due: s.Today.AddDays(10));
        Assert.Equal(Health.Red, s.Run().Project.Health.Health);
        s.Deliverables[0] = s.Deliverables[0] with { DueDate = s.Today.AddDays(-2) };
        Assert.Equal(Health.Yellow, s.Run().Project.Health.Health);
    }

    [Fact]
    public void Discipline_status_uses_only_that_disciplines_items() // §16.6, FR-029
    {
        var s = new Scenario();
        s.Task("C1", due: s.Today.AddDays(10));
        var d = s.Deliverable("G1", s.Today.AddDays(-8), discipline: s.Geo, owner: s.GeoLead);
        s.Task("G2", discipline: s.Geo, due: s.Today.AddDays(3), deliverable: d.Id);
        var r = s.Run();
        Assert.Equal(Health.Green, r.Project.Disciplines.Single(x => x.DisciplineId == s.Civil).Health);
        var geo = r.Project.Disciplines.Single(x => x.DisciplineId == s.Geo);
        Assert.Equal(Health.Red, geo.Health);
        Assert.Equal("G2", geo.NextDueKey);
    }

    [Fact]
    public void Expired_override_informs_the_pm() // A-15
    {
        var s = new Scenario { OverrideHealth = null, OverrideExpires = new DateTimeOffset(new DateTime(2026, 9, 10, 0, 0, 0), TimeSpan.Zero) };
        s.Task("T", due: s.Today.AddDays(30));
        Assert.Contains(s.Run().Attention, a => a.RuleId == "A-15" && a.RouteTo.SequenceEqual([s.Pm]));
    }

    // ---------- Decisions (D-15, DEC-04, DEC-05, AC-DEC-02..04) ----------

    [Fact]
    public void Overdue_decision_blocks_linked_tasks_and_deferral_releases_them()
    {
        var s = new Scenario();
        var t1 = s.Task("T1", TaskStatuses.NotStarted, due: s.Today.AddDays(30));
        var t2 = s.Task("T2", TaskStatuses.NotStarted, due: s.Today.AddDays(30));
        var dec = new DecisionSnap(Guid.NewGuid(), "DEC01", "Pavement", DecisionStatus.Pending, s.Today.AddDays(-1), Impact.Medium, null, s.Pm, [t1.Id, t2.Id]);
        s.Decisions.Add(dec);
        var r = s.Run();
        Assert.True(r.T(t1).IsBlocked && r.T(t2).IsBlocked);
        Assert.Equal("decision", r.T(t1).Blockers.Single().Type);
        Assert.Contains(r.Attention, a => a.RuleId == "A-04" && a.Severity == Severity.Critical && a.RouteTo.Contains(s.Pm));
        Assert.Equal(1, r.Project.Counts["decisionsOverdue"]);
        Assert.Equal(Health.Red, r.Project.Health.Health); // overdue decision blocking work
        s.Decisions[0] = dec with { Status = DecisionStatus.Deferred, RequiredBy = s.Today.AddDays(7) };
        r = s.Run();
        Assert.True(r.T(t1).IsWaiting);
        Assert.DoesNotContain(r.Attention, a => a.RuleId == "A-04");
        s.Decisions[0] = dec with { Status = DecisionStatus.Decided };
        Assert.False(s.Run().T(t1).IsWaiting);
    }

    [Fact]
    public void High_impact_decision_due_soon_is_a_warning() // DEC-05
    {
        var s = new Scenario();
        s.Decisions.Add(new DecisionSnap(Guid.NewGuid(), "DEC02", "x", DecisionStatus.UnderReview, s.Today.AddDays(3), Impact.High, s.Diane, s.Pm, []));
        Assert.Contains(s.Run().Attention, a => a.RuleId == "A-04" && a.Severity == Severity.Warning);
    }

    [Fact]
    public void High_issue_is_critical_and_red() // A-07, ISS-01 (packet 014)
    {
        var s = new Scenario();
        s.Task("T", due: s.Today.AddDays(30));
        s.Issues.Add(new IssueSnap(Guid.NewGuid(), "I01", "Gate locked", IssueStatus.Open, Impact.High, s.Sam, null, null));
        var r = s.Run();
        Assert.Contains(r.Attention, a => a.RuleId == "A-07" && a.Severity == Severity.Critical);
        Assert.Equal(Health.Red, r.Project.Health.Health);
    }

    // ---------- Counts and progress ----------

    [Fact]
    public void Counts_next_milestone_and_project_progress() // §13.1, M-08, §36.2
    {
        var s = new Scenario();
        var kick = s.Milestone("M1", s.Today.AddDays(3), MilestoneType.Kickoff);
        var sub = s.Milestone("M2", s.Today.AddDays(20));
        s.Task("A", TaskStatuses.Complete);
        s.Task("B", TaskStatuses.Cancelled);
        s.Task("C", TaskStatuses.ReadyForReview, reviewRequested: s.Today);
        var r = s.Run().Project;
        Assert.Equal(kick.Id, r.NextMilestoneId);
        Assert.Equal(sub.Id, r.NextSubmissionId);
        Assert.Equal(50, r.ProgressPct);
        Assert.Equal(2, r.Counts["tasksTotal"]);
        Assert.Equal(1, r.Counts["tasksInReview"]);
        Assert.Null(new Scenario().Run().Project.ProgressPct);
    }

    [Fact]
    public void Working_days_skip_weekends_and_holidays() // packet 021
    {
        var cal = new WorkCalendar([new DateOnly(2026, 10, 12)]); // a Monday holiday
        Assert.Equal(1, cal.Between(new DateOnly(2026, 10, 9), new DateOnly(2026, 10, 13))); // Fri → Tue
        Assert.Equal(new DateOnly(2026, 10, 14), cal.Add(new DateOnly(2026, 10, 9), 2));
        Assert.Equal(-1, cal.Between(new DateOnly(2026, 10, 13), new DateOnly(2026, 10, 9)));
        var s = new Scenario { Today = new DateOnly(2026, 10, 9), Calendar = cal };
        s.Settings = s.Settings with { WorkingDaysEnabled = true, TaskDueSoonDays = 1 };
        var t = s.Task("T", due: new DateOnly(2026, 10, 13));
        Assert.True(s.Run().T(t).IsDueSoon);
        s.Settings = s.Settings with { WorkingDaysEnabled = false };
        Assert.False(s.Run().T(t).IsDueSoon);
    }

    [Fact]
    public void Evaluation_is_deterministic() // FR-033
    {
        var (s, _, _, _) = Example(D(9, 11), D(9, 30), D(9, 10));
        var a = System.Text.Json.JsonSerializer.Serialize(s.Run());
        var b = System.Text.Json.JsonSerializer.Serialize(s.Run());
        Assert.Equal(a, b);
    }
}
