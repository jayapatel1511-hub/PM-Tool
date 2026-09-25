using Hub.Domain;
using static Hub.Domain.TaskStatuses;

namespace Hub.Tests.Domain;

/// Every status pair against the transition tables: projects §10.2, deliverables §12.4 DL-02, tasks §12.5 T-10..T-14,
/// decisions DEC-02/03/07, and the Phase 2 registers (§10.2). Where the Appendix C diagrams draw fewer arrows than the
/// rule tables (In Review → On Hold/Cancelled for tasks), the earlier rule tables are followed.
public sealed class WorkflowTests
{
    static IEnumerable<(string From, string To)> Pairs(string[] all) => from a in all from b in all select (a, b);

    static void Matches(string[] all, HashSet<(string, string)> allowed, Func<string, string, bool> step)
    {
        foreach (var (f, t) in Pairs(all)) Assert.True(allowed.Contains((f, t)) == step(f, t), $"{f} → {t} should be {(allowed.Contains((f, t)) ? "allowed" : "refused")}");
    }

    [Fact]
    public void Project_steps_follow_10_2()
    {
        const string S = ProjectStatus.Setup, A = ProjectStatus.Active, H = ProjectStatus.OnHold, C = ProjectStatus.Complete, R = ProjectStatus.Archived, X = ProjectStatus.Cancelled;
        Matches(ProjectStatus.All, [(S, A), (A, H), (H, A), (A, C), (A, X), (H, X), (S, X), (C, R), (C, A), (R, C)], Workflow.ProjectAllowed);
        Assert.Equal(new[] { A, X }, Workflow.ProjectNext(S));
        Assert.Empty(Workflow.ProjectNext(X));
        var needsReason = new HashSet<(string, string)> { (A, H), (A, X), (H, X), (S, X), (A, C), (C, A), (R, C) }; // unarchive is an Admin correction, so it is logged with a reason
        foreach (var (f, t) in Pairs(ProjectStatus.All).Where(p => Workflow.ProjectAllowed(p.From, p.To)))
            Assert.True(needsReason.Contains((f, t)) == Workflow.ProjectNeedsReason(f, t), $"{f} → {t}");
    }

    static HashSet<(string, string)> TaskTable(bool review, string previous)
    {
        var s = new HashSet<(string, string)>
        {
            (NotStarted, InProgress), (ReadyForReview, InReview), (ReadyForReview, InProgress), (InReview, Complete), (InReview, RevisionRequired),
            (RevisionRequired, InProgress), (Complete, InProgress), (Cancelled, NotStarted), (OnHold, previous), (OnHold, Cancelled),
            review ? (InProgress, ReadyForReview) : (InProgress, Complete),
        };
        foreach (var f in new[] { NotStarted, InProgress, ReadyForReview, InReview, RevisionRequired }) { s.Add((f, OnHold)); s.Add((f, Cancelled)); }
        return s;
    }

    [Theory]
    [InlineData(false, NotStarted)]
    [InlineData(true, NotStarted)]
    [InlineData(false, InProgress)]
    [InlineData(true, ReadyForReview)]
    [InlineData(true, InReview)]
    [InlineData(false, RevisionRequired)]
    public void Task_steps_follow_T10_to_T14(bool review, string previous) =>
        Matches(TaskStatuses.All, TaskTable(review, previous), (f, t) => Workflow.TaskStep(f, t, review, previous));

    [Fact]
    public void On_hold_without_a_stored_previous_status_returns_to_not_started()
    {
        Assert.True(Workflow.TaskStep(OnHold, NotStarted, false));
        Assert.False(Workflow.TaskStep(OnHold, InProgress, false));
    }

    [Fact]
    public void Task_paths_pass_through_in_progress_once()
    {
        Assert.Equal(new[] { InProgress, Complete }, Workflow.TaskPath(NotStarted, Complete, requiresReview: false));
        Assert.Equal(new[] { InProgress, ReadyForReview }, Workflow.TaskPath(NotStarted, ReadyForReview, requiresReview: true));
        Assert.Equal(new[] { InProgress, ReadyForReview }, Workflow.TaskPath(RevisionRequired, ReadyForReview, requiresReview: true)); // resubmitting a revision
        Assert.Null(Workflow.TaskPath(NotStarted, Complete, requiresReview: true)); // R-01: no route around review
        Assert.Null(Workflow.TaskPath(InProgress, Complete, requiresReview: true));
        Assert.Null(Workflow.TaskPath(NotStarted, InReview, requiresReview: true));
        Assert.Null(Workflow.TaskPath(InProgress, InProgress, requiresReview: false));
        Assert.Equal(new[] { OnHold }, Workflow.TaskPath(InReview, OnHold, requiresReview: true));
        Assert.Equal(new[] { InProgress }, Workflow.TaskPath(OnHold, InProgress, false, previous: InProgress));
    }

    [Fact]
    public void Task_reasons_are_required_for_hold_cancel_reopen_and_restore()
    {
        var needs = new HashSet<(string, string)> { (Complete, InProgress), (Cancelled, NotStarted) };
        foreach (var (f, t) in Pairs(TaskStatuses.All))
            Assert.True((t is OnHold or Cancelled || needs.Contains((f, t))) == Workflow.TaskNeedsReason(f, t), $"{f} → {t}");
    }

    static HashSet<(string, string)> DeliverableTable(bool review, string previous)
    {
        const string N = DeliverableStatus.NotStarted, P = DeliverableStatus.InProgress, V = DeliverableStatus.InReview, R = DeliverableStatus.RevisionRequired,
            T = DeliverableStatus.ReadyToIssue, I = DeliverableStatus.Issued, A = DeliverableStatus.Accepted, H = DeliverableStatus.OnHold, X = DeliverableStatus.Cancelled;
        var s = new HashSet<(string, string)> { (N, P), (P, V), (V, R), (R, P), (V, T), (T, I), (I, A), (I, R), (H, previous), (H, X) };
        if (!review) s.Add((P, T));
        foreach (var f in new[] { N, P, V, R, T }) s.Add((f, H));
        foreach (var f in DeliverableStatus.All.Where(x => x != X)) s.Add((f, X)); // DL-02: "any → Cancelled"
        return s;
    }

    [Theory]
    [InlineData(true, DeliverableStatus.InProgress)]
    [InlineData(false, DeliverableStatus.InProgress)]
    [InlineData(true, DeliverableStatus.NotStarted)]
    [InlineData(false, DeliverableStatus.ReadyToIssue)]
    public void Deliverable_steps_follow_DL02(bool review, string previous) =>
        Matches(DeliverableStatus.All, DeliverableTable(review, previous), (f, t) => Workflow.DeliverableStep(f, t, review, previous));

    [Fact]
    public void Deliverable_hold_defaults_to_in_progress_and_guards_explain_refusals()
    {
        Assert.True(Workflow.DeliverableStep(DeliverableStatus.OnHold, DeliverableStatus.InProgress, true));
        Assert.False(Workflow.DeliverableStep(DeliverableStatus.OnHold, DeliverableStatus.NotStarted, true));
        Assert.Equal("guard.ready_to_issue_needs_review", Workflow.DeliverableGuard(DeliverableStatus.InProgress, DeliverableStatus.ReadyToIssue, requiresReview: true));
        Assert.Null(Workflow.DeliverableGuard(DeliverableStatus.InReview, DeliverableStatus.ReadyToIssue, requiresReview: true));
        Assert.Null(Workflow.DeliverableGuard(DeliverableStatus.InProgress, DeliverableStatus.ReadyToIssue, requiresReview: false));
        Assert.Null(Workflow.DeliverableGuard(DeliverableStatus.InProgress, DeliverableStatus.InReview, requiresReview: true));
        foreach (var t in DeliverableStatus.All)
            Assert.Equal(t is DeliverableStatus.OnHold or DeliverableStatus.Cancelled, Workflow.DeliverableNeedsReason(t));
    }

    [Fact]
    public void Decision_steps_follow_DEC02_DEC03_DEC07()
    {
        const string P = DecisionStatus.Pending, U = DecisionStatus.UnderReview, D = DecisionStatus.Decided, F = DecisionStatus.Deferred, X = DecisionStatus.Cancelled;
        var allowed = new HashSet<(string, string)> { (P, U), (U, P), (F, U), (F, P), (D, P) };
        foreach (var open in new[] { P, U, F }) { allowed.Add((open, D)); allowed.Add((open, F)); allowed.Add((open, X)); } // a deferral may be deferred again with a new date
        Matches(DecisionStatus.All, allowed, Workflow.DecisionStep);
    }

    [Fact]
    public void Register_steps_follow_10_2()
    {
        const string O = RiskStatus.Open, M = RiskStatus.Monitoring, C = RiskStatus.Closed, R = RiskStatus.Realised;
        Matches(RiskStatus.All, [(O, M), (M, O), (O, C), (O, R), (M, C), (M, R), (C, O)], Workflow.RiskStep);
        const string IO = IssueStatus.Open, IP = IssueStatus.InProgress, IR = IssueStatus.Resolved, IX = IssueStatus.Cancelled;
        Matches(IssueStatus.All, [(IO, IP), (IP, IO), (IO, IR), (IO, IX), (IP, IR), (IP, IX), (IR, IP)], Workflow.IssueStep);
        const string AO = ActionStatus.Open, AP = ActionStatus.InProgress, AC = ActionStatus.Complete, AX = ActionStatus.Cancelled;
        Matches(ActionStatus.All, [(AO, AP), (AP, AO), (AO, AC), (AO, AX), (AP, AC), (AP, AX), (AC, AP)], Workflow.ActionStep);
    }

    [Fact]
    public void Board_lanes_present_canonical_statuses() // §36.3, FR-VIS-04
    {
        var lanes = TaskStatuses.All.ToDictionary(s => s, Workflow.Lane);
        Assert.Equal("To Do", lanes[NotStarted]);
        Assert.Equal("In Progress", lanes[InProgress]);
        Assert.All(new[] { ReadyForReview, InReview, RevisionRequired }, s => Assert.Equal("Review", lanes[s]));
        Assert.Equal("Done", lanes[Complete]);
        Assert.Equal(OnHold, lanes[OnHold]); // never silently Done
        Assert.Equal(Cancelled, lanes[Cancelled]);
        foreach (var lane in Workflow.Lanes)
            Assert.All(Workflow.LaneTargets(lane), s => Assert.Equal(lane, Workflow.Lane(s)));
        Assert.Empty(Workflow.LaneTargets("Elsewhere"));
    }
}
