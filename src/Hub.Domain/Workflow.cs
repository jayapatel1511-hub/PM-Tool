namespace Hub.Domain;

/// Structural status machines (§10.2, §12.4, §12.5, Appendix C). Who may take each step is in Permissions.
public static class Workflow
{
    // ---------- Projects (§10.2, P-02, P-03) ----------

    static readonly (string From, string To)[] ProjectSteps =
    [
        (ProjectStatus.Setup, ProjectStatus.Active), (ProjectStatus.Active, ProjectStatus.OnHold), (ProjectStatus.OnHold, ProjectStatus.Active),
        (ProjectStatus.Active, ProjectStatus.Complete), (ProjectStatus.Active, ProjectStatus.Cancelled), (ProjectStatus.OnHold, ProjectStatus.Cancelled),
        (ProjectStatus.Setup, ProjectStatus.Cancelled), (ProjectStatus.Complete, ProjectStatus.Archived), (ProjectStatus.Complete, ProjectStatus.Active),
        (ProjectStatus.Archived, ProjectStatus.Complete),
    ];

    public static bool ProjectAllowed(string from, string to) => ProjectSteps.Contains((from, to));
    public static IEnumerable<string> ProjectNext(string from) => ProjectSteps.Where(s => s.From == from).Select(s => s.To);
    public static bool ProjectNeedsReason(string from, string to) =>
        to is ProjectStatus.OnHold or ProjectStatus.Cancelled or ProjectStatus.Complete || (from == ProjectStatus.Complete && to == ProjectStatus.Active);

    // ---------- Tasks (T-10 to T-14) ----------

    public static bool TaskStep(string from, string to, bool requiresReview, string? previous = null) => (from, to) switch
    {
        (TaskStatuses.NotStarted, TaskStatuses.InProgress) => true,
        (TaskStatuses.InProgress, TaskStatuses.ReadyForReview) => requiresReview,
        (TaskStatuses.InProgress, TaskStatuses.Complete) => !requiresReview,
        (TaskStatuses.ReadyForReview, TaskStatuses.InReview) => true,
        (TaskStatuses.ReadyForReview, TaskStatuses.InProgress) => true,
        (TaskStatuses.InReview, TaskStatuses.Complete) => true,
        (TaskStatuses.InReview, TaskStatuses.RevisionRequired) => true,
        (TaskStatuses.RevisionRequired, TaskStatuses.InProgress) => true,
        (TaskStatuses.Complete, TaskStatuses.InProgress) => true,          // reopen
        (TaskStatuses.Cancelled, TaskStatuses.NotStarted) => true,         // restore
        (TaskStatuses.OnHold, var t) => t == (previous ?? TaskStatuses.NotStarted) || t == TaskStatuses.Cancelled,
        (var f, TaskStatuses.OnHold) => !TaskStatuses.IsTerminal(f) && f != TaskStatuses.OnHold,
        (var f, TaskStatuses.Cancelled) => !TaskStatuses.IsTerminal(f),
        _ => false,
    };

    /// Steps from `from` to `to`, allowing one pass through In Progress (a Not Started task completed from
    /// a checkbox, or a revision resubmitted), so no guard is skipped. Null when unreachable.
    public static string[]? TaskPath(string from, string to, bool requiresReview, string? previous = null)
    {
        if (from == to) return null;
        if (TaskStep(from, to, requiresReview, previous)) return [to];
        if (from is TaskStatuses.NotStarted or TaskStatuses.RevisionRequired && to != TaskStatuses.InProgress
            && TaskStep(from, TaskStatuses.InProgress, requiresReview) && TaskStep(TaskStatuses.InProgress, to, requiresReview))
            return [TaskStatuses.InProgress, to];
        return null;
    }

    public static bool TaskNeedsReason(string from, string to) =>
        to is TaskStatuses.OnHold or TaskStatuses.Cancelled || (from == TaskStatuses.Complete && to == TaskStatuses.InProgress)
        || (from == TaskStatuses.Cancelled && to == TaskStatuses.NotStarted);

    // ---------- Deliverables (DL-02, DL-04, Appendix C, E-24) ----------

    public static bool DeliverableStep(string from, string to, bool requiresReview, string? previous = null) => (from, to) switch
    {
        (DeliverableStatus.NotStarted, DeliverableStatus.InProgress) => true,
        (DeliverableStatus.InProgress, DeliverableStatus.InReview) => true,
        (DeliverableStatus.InReview, DeliverableStatus.RevisionRequired) => true,
        (DeliverableStatus.RevisionRequired, DeliverableStatus.InProgress) => true,
        (DeliverableStatus.InReview, DeliverableStatus.ReadyToIssue) => true,
        (DeliverableStatus.InProgress, DeliverableStatus.ReadyToIssue) => !requiresReview,
        (DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued) => true,
        (DeliverableStatus.Issued, DeliverableStatus.Accepted) => true,
        (DeliverableStatus.Issued, DeliverableStatus.RevisionRequired) => true,
        (DeliverableStatus.OnHold, var t) => t == (previous ?? DeliverableStatus.InProgress) || t == DeliverableStatus.Cancelled,
        (var f, DeliverableStatus.OnHold) => f is DeliverableStatus.NotStarted or DeliverableStatus.InProgress or DeliverableStatus.InReview
            or DeliverableStatus.RevisionRequired or DeliverableStatus.ReadyToIssue,
        (var f, DeliverableStatus.Cancelled) => f != DeliverableStatus.Cancelled,
        _ => false,
    };

    public static bool DeliverableNeedsReason(string to) => to is DeliverableStatus.OnHold or DeliverableStatus.Cancelled;

    /// Explains a refused deliverable step (§13.6 "status guard messages are explicit").
    public static string? DeliverableGuard(string from, string to, bool requiresReview) =>
        to == DeliverableStatus.ReadyToIssue && requiresReview && from != DeliverableStatus.InReview ? "guard.ready_to_issue_needs_review" : null;

    // ---------- Decisions (DEC-02, DEC-03, DEC-07) ----------

    public static bool DecisionStep(string from, string to) => (from, to) switch
    {
        (DecisionStatus.Pending, DecisionStatus.UnderReview) => true,
        (DecisionStatus.UnderReview, DecisionStatus.Pending) => true,
        (DecisionStatus.Deferred, DecisionStatus.UnderReview) => true,
        (DecisionStatus.Deferred, DecisionStatus.Pending) => true,
        (var f, DecisionStatus.Decided) => DecisionStatus.IsOpen(f),
        (var f, DecisionStatus.Deferred) => DecisionStatus.IsOpen(f),
        (var f, DecisionStatus.Cancelled) => DecisionStatus.IsOpen(f),
        (DecisionStatus.Decided, DecisionStatus.Pending) => true,          // reopen (PM, reason)
        _ => false,
    };

    // ---------- Phase 2 registers ----------

    public static bool RiskStep(string from, string to) => (from, to) switch
    {
        (RiskStatus.Open, RiskStatus.Monitoring) or (RiskStatus.Monitoring, RiskStatus.Open) => true,
        (RiskStatus.Open or RiskStatus.Monitoring, RiskStatus.Closed or RiskStatus.Realised) => true,
        (RiskStatus.Closed, RiskStatus.Open) => true,
        _ => false,
    };

    public static bool IssueStep(string from, string to) => (from, to) switch
    {
        (IssueStatus.Open, IssueStatus.InProgress) or (IssueStatus.InProgress, IssueStatus.Open) => true,
        (IssueStatus.Open or IssueStatus.InProgress, IssueStatus.Resolved or IssueStatus.Cancelled) => true,
        (IssueStatus.Resolved, IssueStatus.InProgress) => true,
        _ => false,
    };

    /// MTG-03: an action converted to a task follows the task: Complete and Cancelled carry over, and any other task status
    /// keeps the action In Progress. Worked example: task Complete → action Complete; task reopened → action In Progress.
    public static string ActionFollowing(string taskStatus) => taskStatus switch
    {
        TaskStatuses.Complete => ActionStatus.Complete,
        TaskStatuses.Cancelled => ActionStatus.Cancelled,
        _ => ActionStatus.InProgress,
    };

    public static bool ActionStep(string from, string to) => (from, to) switch
    {
        (ActionStatus.Open, ActionStatus.InProgress) or (ActionStatus.InProgress, ActionStatus.Open) => true,
        (ActionStatus.Open or ActionStatus.InProgress, ActionStatus.Complete or ActionStatus.Cancelled) => true,
        (ActionStatus.Complete, ActionStatus.InProgress) => true,
        _ => false,
    };

    /// §36.3: board lanes are presentations of canonical status.
    public static string Lane(string taskStatus) => taskStatus switch
    {
        TaskStatuses.NotStarted => "To Do",
        TaskStatuses.InProgress => "In Progress",
        TaskStatuses.ReadyForReview or TaskStatuses.InReview or TaskStatuses.RevisionRequired => "Review",
        TaskStatuses.Complete => "Done",
        _ => taskStatus, // On Hold and Cancelled are only shown through explicit filters
    };

    public static readonly string[] Lanes = ["To Do", "In Progress", "Review", "Done"];

    /// Candidate canonical statuses for a card dropped on a lane.
    public static string[] LaneTargets(string lane) => lane switch
    {
        "To Do" => [TaskStatuses.NotStarted],
        "In Progress" => [TaskStatuses.InProgress],
        "Review" => [TaskStatuses.ReadyForReview, TaskStatuses.InReview, TaskStatuses.RevisionRequired],
        "Done" => [TaskStatuses.Complete],
        _ => [],
    };
}
