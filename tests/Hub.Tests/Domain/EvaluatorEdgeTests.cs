using Hub.Domain;

namespace Hub.Tests.Domain;

/// Edge cases of the rules engine beyond the §15.12 worked examples: partial data, the rarer blockers, deliverable and
/// milestone conditions, inactive owners, register items and discipline status (§10.3, §12.12, §16).
public sealed class EvaluatorEdgeTests
{
    static DateOnly D(int m, int d, int y = 2026) => new(y, m, d);

    [Fact]
    public void Edges_to_items_outside_the_project_are_ignored()
    {
        var s = new Scenario();
        var t = s.Task("T1", TaskStatuses.NotStarted, due: D(9, 20));
        var d = s.Deliverable("D1", D(10, 30));
        s.Deps.Add(new DependencySnap(Guid.NewGuid(), Guid.NewGuid(), t.Id));
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), Guid.NewGuid(), d.Id));
        var r = s.Run();
        Assert.False(r.T(t).IsWaiting || r.T(t).IsBlocked);
        Assert.Empty(r.D(d).Blockers);
    }

    [Fact]
    public void Working_days_without_a_holiday_calendar_count_weekdays() // §10.4
    {
        var s = new Scenario { Settings = new OrgSettings { WorkingDaysEnabled = true, TaskDueSoonDays = 2 } }; // Friday 11 Sep
        var t = s.Task("T1", due: D(9, 15)); // Tuesday: two working days, four calendar days
        Assert.True(s.Run().T(t).IsDueSoon);
        s.Settings = s.Settings with { WorkingDaysEnabled = false };
        Assert.False(s.Run().T(t).IsDueSoon);
    }

    [Fact]
    public void Not_started_task_without_due_date_is_flagged_only_under_a_dated_deliverable() // T-03, A-09
    {
        var s = new Scenario();
        var d = s.Deliverable("D1", D(10, 30));
        var flagged = s.Task("T1", TaskStatuses.NotStarted, deliverable: d.Id);
        var loose = s.Task("T2", TaskStatuses.NotStarted);
        var r = s.Run();
        Assert.True(r.T(flagged).IsMissingDueDate);
        Assert.Contains(r.A(flagged.Id), a => a.RuleId == "A-09" && a.Message == "No due date while its deliverable has one");
        Assert.False(r.T(loose).IsMissingDueDate);
    }

    [Fact]
    public void Successor_due_before_its_predecessor_is_date_inconsistent() // D-12
    {
        var s = new Scenario();
        var a = s.Task("T1", due: D(10, 10));
        var b = s.Task("T2", TaskStatuses.NotStarted, due: D(10, 5));
        s.Depends(b, a);
        var r = s.Run();
        Assert.True(r.T(b).IsDateInconsistent);
        Assert.Equal("Due 2026-10-05 before T1 is due 2026-10-10", r.T(b).Inconsistencies.Single().Text);
    }

    [Fact]
    public void Cancelled_decision_is_noted_and_does_not_hold_the_task() // D-15
    {
        var s = new Scenario();
        var t = s.Task("T1", TaskStatuses.NotStarted, due: D(10, 1));
        s.Decisions.Add(new DecisionSnap(Guid.NewGuid(), "DEC1", "Alignment", DecisionStatus.Cancelled, D(9, 1), Impact.High, s.Diane, s.Pm, [t.Id]));
        var r = s.Run();
        Assert.Contains("decision_cancelled", r.T(t).Notes);
        Assert.False(r.T(t).IsBlocked || r.T(t).IsWaiting);
        Assert.Empty(r.A(s.Decisions[0].Id));
    }

    [Fact]
    public void Held_and_finished_successors_are_not_blocked_by_the_predecessor() // D-11, D-13
    {
        var s = new Scenario();
        var cancelledMs = s.Replace(s.Milestones, s.Milestone("M1", D(10, 30)), s.Milestones[0] with { IsCancelled = true });
        var a = s.Task("T1", due: D(9, 1), milestone: cancelledMs.Id);
        s.Depends(s.Task("T2", TaskStatuses.OnHold), a);
        s.Depends(s.Task("T3", TaskStatuses.Complete), a);
        var r = s.Run();
        Assert.True(r.T(a).IsOverdue);
        Assert.False(r.T(a).IsBlocking);
        Assert.DoesNotContain(r.A(a.Id), x => x.RuleId == "A-03");
        Assert.Empty(r.T(a).AffectedMilestoneIds); // a cancelled milestone is no longer affected
        Assert.Equal(MilestoneStatus.Cancelled, r.M(cancelledMs).Status);
    }

    [Fact]
    public void Overdue_decision_counts_only_the_open_tasks_it_blocks() // DEC-04, A-04
    {
        var s = new Scenario();
        var open = s.Task("T1", TaskStatuses.NotStarted, due: D(10, 1));
        var done = s.Task("T2", TaskStatuses.Complete);
        var dec = new DecisionSnap(Guid.NewGuid(), "DEC1", "Pavement", DecisionStatus.Pending, D(9, 5), Impact.Medium, s.Diane, s.Pm, [open.Id, done.Id, Guid.NewGuid()]);
        s.Decisions.Add(dec);
        var r = s.Run();
        Assert.Equal(new[] { open.Id }, r.Decisions.Single().BlockingTaskIds);
        var a04 = r.A(dec.Id).Single(a => a.RuleId == "A-04");
        Assert.Equal(Severity.Critical, a04.Severity);
        Assert.Equal("Required by 2026-09-05 — 6 days overdue; blocking 1 tasks", a04.Message);
    }

    [Fact]
    public void Task_dependencies_across_deliverables_derive_deliverable_dependencies() // D-18, FR-DEP-08
    {
        var s = new Scenario();
        var geo = s.Deliverable("D1", D(10, 15), discipline: s.Geo, owner: s.GeoLead);
        var civ = s.Deliverable("D2", D(10, 30));
        var g = s.Task("T1", due: D(10, 10), discipline: s.Geo, deliverable: geo.Id);
        var c1 = s.Task("T2", TaskStatuses.NotStarted, due: D(10, 20), deliverable: civ.Id);
        var c2 = s.Task("T3", TaskStatuses.NotStarted, due: D(10, 25), deliverable: civ.Id);
        var loose = s.Task("T4", TaskStatuses.NotStarted, due: D(10, 25));
        s.Depends(c1, g);
        s.Depends(c2, c1); // same deliverable: no derived edge
        s.Depends(loose, g); // no deliverable: no derived edge
        var r = s.Run();
        Assert.Equal(new[] { geo.Id }, r.D(civ).DerivedPredecessorIds);
        Assert.Equal(new[] { civ.Id }, r.D(geo).DerivedSuccessorIds);
        Assert.Empty(r.D(geo).DerivedPredecessorIds);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("dueSoon")]
    [InlineData("started")]
    [InlineData("predecessorOverdue")]
    [InlineData("waiting")]
    public void Explicit_deliverable_dependency_waits_or_blocks(string when) // packet 021
    {
        var s = new Scenario();
        var pred = s.Deliverable("D1", when == "predecessorOverdue" ? D(9, 1) : D(10, 20));
        var succ = s.Deliverable("D2", when == "dueSoon" ? D(9, 15) : D(11, 30), status: when == "started" ? DeliverableStatus.InProgress : DeliverableStatus.NotStarted);
        if (when == "start") succ = s.Replace(s.Deliverables, succ, succ with { StartDate = D(9, 10) });
        s.DeliverableDeps.Add(new DependencySnap(Guid.NewGuid(), pred.Id, succ.Id));
        var r = s.Run().D(succ);
        Assert.Equal(when != "waiting", r.IsBlocked);
        Assert.Equal(when == "waiting", r.IsWaiting);
        Assert.Equal(pred.Key, r.Blockers.Single().Key);
    }

    [Fact]
    public void Deliverable_due_soon_with_overdue_tasks_or_an_overdue_milestone_is_at_risk() // DL-08
    {
        var s = new Scenario();
        var passed = s.Milestone("M1", D(9, 1));
        var d = s.Deliverable("D1", D(9, 14), passed.Id);
        s.Task("T1", due: D(9, 1), deliverable: d.Id);
        s.Task("T2", TaskStatuses.Complete, deliverable: d.Id);
        s.Task("T3", TaskStatuses.Complete, deliverable: d.Id);
        var r = s.Run().D(d);
        Assert.True(r.IsAtRisk);
        Assert.Contains(r.AtRiskReasons, x => x.Text.StartsWith("Due within 10 days with 1 overdue and 0 blocked tasks"));
        Assert.Contains(r.AtRiskReasons, x => x.Text == "Milestone M1 is overdue");
    }

    [Fact]
    public void Milestone_status_edge_cases() // §16.2
    {
        var s = new Scenario();
        var undated = s.Milestone("M1", null);
        var slipped = s.Milestone("M2", D(12, 1), original: D(11, 15));
        var overdueDel = s.Deliverable("D1", D(9, 1), slipped.Id);
        var late = s.Deliverable("D2", D(12, 10), slipped.Id);
        var r = s.Run();
        Assert.Null(r.M(undated).Status);
        Assert.Null(r.M(undated).DaysRemaining);
        var m = r.M(slipped);
        Assert.Equal(MilestoneStatus.AtRisk, m.Status);
        Assert.Equal(16, m.SlipDays);
        Assert.Contains(m.Reasons, x => x.Text == "1 targeted deliverables overdue: D1" && x.Colour == "Yellow");
        Assert.Contains(m.Reasons, x => x.Text == "1 deliverables due after the milestone: D2");
        Assert.NotNull(overdueDel);
        Assert.NotNull(late);
    }

    [Fact]
    public void Approaching_milestone_with_blocked_work_alerts_leads_even_when_deliverables_are_issued() // A-05
    {
        var s = new Scenario();
        var m = s.Milestone("M1", D(9, 20));
        var d = s.Deliverable("D1", D(9, 18), m.Id, status: DeliverableStatus.Issued);
        var blocked = s.Task("T1", TaskStatuses.NotStarted, due: D(9, 18), milestone: m.Id, block: BlockType.Client);
        var r = s.Run();
        var a05 = r.A(m.Id).Single(a => a.RuleId == "A-05");
        Assert.Equal(Severity.Warning, a05.Severity); // 9 days out
        Assert.Equal("In 9 days; 0 of 1 deliverables not issued; 1 tasks overdue or blocked", a05.Message);
        Assert.Contains(s.CivilLead, a05.RouteTo);
        Assert.True(r.T(blocked).IsBlocked);
        Assert.NotNull(d);
    }

    [Fact]
    public void Inactive_reviewers_owners_and_decision_owners_are_critical() // A-18
    {
        var s = new Scenario();
        s.Deactivate(s.Diane);
        var t = s.Task("T1", TaskStatuses.ReadyForReview, due: D(10, 1), reviewer: s.Diane, reviewRequested: D(9, 10));
        var owned = s.Deliverable("D1", D(10, 30), owner: s.Diane);
        var reviewed = s.Replace(s.Deliverables, s.Deliverable("D2", D(10, 30)), s.Deliverables[1] with { ReviewerId = s.Diane });
        var dec = new DecisionSnap(Guid.NewGuid(), "DEC1", "Scope", DecisionStatus.UnderReview, D(10, 20), Impact.Low, s.Diane, s.Pm, []);
        s.Decisions.Add(dec);
        var r = s.Run();
        Assert.Equal("Reviewer Diane is inactive", r.A(t.Id).Single(a => a.RuleId == "A-18").Message);
        Assert.Equal("Owner Diane is inactive", r.A(owned.Id).Single(a => a.RuleId == "A-18").Message);
        Assert.Equal("Reviewer Diane is inactive", r.A(reviewed.Id).Single(a => a.RuleId == "A-18").Message);
        Assert.Equal("Owner Diane is inactive", r.A(dec.Id).Single(a => a.RuleId == "A-18").Message);
        Assert.True(r.Decisions.Single().IsInactiveOwner);
    }

    [Fact]
    public void Unowned_deliverables_and_held_ones_follow_A06_and_A13() // A-06, A-13
    {
        var s = new Scenario();
        var unowned = s.Replace(s.Deliverables, s.Deliverable("D1", D(9, 15), status: DeliverableStatus.NotStarted), s.Deliverables[0] with { OwnerId = null });
        var held = s.Deliverable("D2", D(9, 15), status: DeliverableStatus.OnHold);
        var later = s.Replace(s.Deliverables, s.Deliverable("D3", D(12, 15), status: DeliverableStatus.NotStarted), s.Deliverables[2] with { OwnerId = null });
        var r = s.Run();
        Assert.Equal("Due 2026-09-15 with no owner", r.A(unowned.Id).Single(a => a.RuleId == "A-13").Message);
        Assert.Contains(r.A(unowned.Id), a => a.RuleId == "A-06");
        Assert.DoesNotContain(r.A(held.Id), a => a.RuleId == "A-06"); // On Hold is excluded
        Assert.Empty(r.A(later.Id)); // not started and not due soon
    }

    [Fact]
    public void Issues_risks_and_actions_feed_attention_counts_and_routing() // A-07, MTG-01, §13.1
    {
        var s = new Scenario();
        s.Task("T1", due: D(10, 1)); // Civil has work to evaluate
        s.Issues.Add(new IssueSnap(Guid.NewGuid(), "I01", "Utility conflict", IssueStatus.InProgress, Impact.High, s.Sam, D(9, 1), s.Civil));
        s.Issues.Add(new IssueSnap(Guid.NewGuid(), "I02", "Minor", IssueStatus.Open, Impact.Low, s.Sam, null, null));
        s.Risks.Add(new RiskSnap(Guid.NewGuid(), "R01", "Rock", RiskStatus.Open, 3, 2, null, s.Sam, null));
        s.Risks.Add(new RiskSnap(Guid.NewGuid(), "R02", "Rain", RiskStatus.Closed, 3, 3, null, s.Sam, null));
        s.Risks.Add(new RiskSnap(Guid.NewGuid(), "R03", "Permit", RiskStatus.Monitoring, 1, 5, null, s.Sam, null));
        var mine = new ActionSnap(Guid.NewGuid(), "A01", "Send survey", ActionStatus.Open, ActionOwnerType.User, s.Alex, null, D(9, 9));
        var led = new ActionSnap(Guid.NewGuid(), "A02", "Civil comments", ActionStatus.InProgress, ActionOwnerType.Discipline, null, s.Civil, D(9, 10));
        var noLead = new ActionSnap(Guid.NewGuid(), "A03", "Geo comments", ActionStatus.Open, ActionOwnerType.Discipline, null, s.Geo, D(9, 10));
        var future = new ActionSnap(Guid.NewGuid(), "A04", "Later", ActionStatus.Open, ActionOwnerType.User, s.Alex, null, D(9, 30));
        s.Actions.AddRange([mine, led, noLead, future]);
        s.Disciplines[1] = s.Disciplines[1] with { LeadId = null };
        var r = s.Run();
        Assert.Equal("High-severity issue In Progress; target 2026-09-01", r.Attention.Single(a => a.RuleId == "A-07").Message);
        Assert.Equal(10, r.Attention.Single(a => a.RuleId == "A-07").DaysOverdueOrBlocked);
        Assert.Equal(1, r.Project.Counts["issuesHigh"]);
        Assert.Equal(2, r.Project.Counts["issuesOpen"]);
        Assert.Equal(1, r.Project.Counts["risksHigh"]); // open or monitoring with probability × impact ≥ 6
        Assert.Contains(s.Alex, r.A(mine.Id).Single().RouteTo);
        Assert.Contains(s.CivilLead, r.A(led.Id).Single().RouteTo);
        Assert.Contains(s.Pm, r.A(noLead.Id).Single().RouteTo);
        Assert.Empty(r.A(future.Id));
        Assert.Equal(Health.Red, r.Project.Disciplines.Single(d => d.DisciplineId == s.Civil).Health); // the high issue is Civil's
    }

    [Fact]
    public void Discipline_with_an_inactive_lead_turns_yellow() // §16.6, TM-07
    {
        var s = new Scenario();
        s.Task("T1", due: D(10, 1), discipline: s.Geo, assignee: s.Sam);
        s.Deactivate(s.GeoLead);
        var r = s.Run();
        Assert.Equal(Health.Yellow, r.Project.Disciplines.Single(d => d.DisciplineId == s.Geo).Health);
    }

    [Fact]
    public void Expired_snoozes_do_not_hide_items() // ATT-03
    {
        var s = new Scenario();
        var t = s.Task("T1", due: D(9, 8));
        s.Snoozes.Add(new SnoozeSnap("A-01", ItemType.Task, t.Id, new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero), Severity.Warning));
        Assert.False(s.Run().A(t.Id).Single(a => a.RuleId == "A-01").Snoozed);
    }
}
