namespace Hub.Domain;

public static class RecordContextKind
{
    public const string Task = ItemType.Task, Deliverable = ItemType.Deliverable, Milestone = ItemType.Milestone, Decision = ItemType.Decision, Risk = ItemType.Risk, Issue = ItemType.Issue, Action = ItemType.Action, MeetingAction = Action, Handoff = ItemType.Handoff, ChangeNotice = ItemType.ChangeNotice, WorkConstraint = "WorkConstraint", Constraint = WorkConstraint, ReviewFinding = "ReviewFinding", ReviewComment = ReviewFinding, SubmissionPackage = "SubmissionPackage", Submission = SubmissionPackage;
    public const string ChangeAssessment = "ChangeAssessment", SubmissionCheck = "SubmissionCheck", ReviewPackage = "ReviewPackage";
    public static readonly string[] All = [Task, Deliverable, Milestone, Decision, Risk, Issue, Action, Handoff, ChangeNotice, WorkConstraint, ReviewFinding, SubmissionPackage];
}
public static class RecordContextAction
{
    public const string RunChecks = "run_current_checks";
    public const string AcknowledgeWithdrawal = "acknowledge_withdrawal";
    public const string AdoptRevision = "adopt_current_revision", VerifyChange = "verify_change_update";
    public const string FollowLinkedTask = "follow_linked_task", ReviewPrerequisites = "review_prerequisites", None = "none", NotAssessed = "not_assessed", Start = "start", Complete = "complete", ResolveInput = "resolve_input", Resume = "resume", Review = "review", Revise = "revise", Transition = "transition", AssessChange = "assess_change", FollowUp = "complete_or_verify_follow_up", Submit = "submit", Decide = "decide", Accept = "accept", Respond = "respond_and_resubmit", Incorporate = "incorporate", Remove = "remove", VerifyRemoval = "verify_removal", RespondToComment = "respond_to_comment", VerifyClosure = "verify_closure", StartChecking = "start_checking", ResolveCheck = "resolve_check", Issue = "issue", ReviewOrResolve = "review_resolve_complete";
}
public enum RecordContextActorState { None, Active, Missing, Inactive, Unassigned, Unknown, External, Ambiguous, Partial }
public sealed record RecordContextActor(string Id, RecordContextActorState State = RecordContextActorState.Active);
public sealed record RecordContextInput(RecordContextFacts Facts, bool Failed = false, bool Pending = false, string? Title = null) { public string Kind => Facts.Kind; public string Id => Facts.Id; public DateOnly? NeededBy => Facts.NeededBy; }
public sealed record RecordContextAssessmentProgress(string NoticeStatus, bool Complete, bool TargetMatches,
    bool TargetEligible, bool HasRationale, bool HasEvidence, bool RetainOld, bool RetentionApproved,
    bool AdoptedCurrent, bool CanAdopt, bool CorrectionEligible, bool ReviewerEligible, bool ReviewerIndependent);
public sealed record RecordContextFindingProgress(bool RelationshipsValid, bool PackageLive, bool CurrentRound,
    bool ResolverEligible, bool VerifierEligible, bool CoordinatorEligible, bool AuthorsKnown, bool VerifierIndependent,
    bool HasResponseEvidence, bool SeverityBlocking, bool WithdrawalAcknowledged, bool PostActionApprovalAllowed);
public sealed record RecordContextSubmissionProgress(bool Assessed, bool CoordinatorEligible, bool ProjectManagerEligible, bool UnmappedBlockers);
public sealed record RecordContextSubmissionCheckProgress(bool Blocking, bool Known, bool OwnerEligible, bool SourcesKnown, bool TerminalPackage = false);
public sealed record RecordContextFacts(string Kind, string Id, string State, string? OwnerId = null, DateOnly? NeededBy = null, string? AssigneeId = null, string? ReviewerId = null, string? CoordinatorId = null, string? CheckingOwnerId = null, string? DecisionOwnerId = null, string? AffectedOwnerId = null, string? OriginatorId = null, string? VerifierId = null, IReadOnlyList<RecordContextActor>? Actors = null, IReadOnlyList<RecordContextInput>? BlockingInputs = null, bool Waiting = false, bool Blocked = false, bool HasPendingAssessments = false, bool HasRequiredUpdates = false, bool RequiresReviewGate = false, string? SendingOwnerId = null, string? ReceivingOwnerId = null, string? RemovalOwnerId = null, string? ProjectManagerId = null)
{ public IReadOnlyList<RecordContextActor> EligibleActors => Actors ?? []; public IReadOnlyList<string> PendingAssessmentOwnerIds { get; init; } = []; public IReadOnlyList<string> RequiredUpdateOwnerIds { get; init; } = []; public IReadOnlyList<RecordContextInput> Inputs => BlockingInputs ?? []; public string? PreviousStatus { get; init; } public bool HasRequiredReviewPackage { get; init; } public bool? RequiredReviewSatisfied { get; init; } public string OwnerKind { get; init; } = "User"; public string? DisciplineLeadId { get; init; } public bool DisciplineOwnerAvailable { get; init; } = true; public bool FollowsTask { get; init; } public RecordContextAssessmentProgress? AssessmentProgress { get; init; } public RecordContextFindingProgress? FindingProgress { get; init; } public RecordContextSubmissionProgress? SubmissionProgress { get; init; } public RecordContextSubmissionCheckProgress? SubmissionCheckProgress { get; init; } }
public sealed record RecordContextResult(string? OwnerId, IReadOnlyList<RecordContextActor> NextActors, DateOnly? NeededBy, string NextAction, bool IsTerminal, string? PrimaryInputId, IReadOnlyList<RecordContextInput> BlockingInputs, RecordContextActorState ActorState) { public RecordContextActor? NextActor => NextActors.Count == 1 ? NextActors[0] : null; public bool RequiresReviewGate { get; init; } public IReadOnlyList<string> NextTransitions { get; init; } = []; public string? PrimaryInputKind { get; init; } }

public static class RecordContextRules
{
    public static RecordContextResult Evaluate(RecordContextFacts f) => Evaluate(f, new(StringComparer.Ordinal));

    static RecordContextResult Evaluate(RecordContextFacts f, HashSet<string> path)
    {
        ArgumentNullException.ThrowIfNull(f);
        var visit = $"{f.Kind}:{f.Id}";
        if (!path.Add(visit)) return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown);
        if (!CanonicalStates(f.Kind).Contains(f.State, StringComparer.Ordinal)) return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown);
        if (Terminal(f)) return Result(f, [], RecordContextAction.None, true, RecordContextActorState.None);
        if (f.Kind == RecordContextKind.Action && f.FollowsTask)
        {
            // A converted action follows its one explicitly linked task; it no longer
            // has an independent Complete command. Ambiguous or unavailable links stay unknown.
            var target = f.Inputs.Count == 1 && f.Inputs[0].Kind == RecordContextKind.Task ? f.Inputs[0] : null;
            if (target is null) return Unmapped(f);
            var next = Evaluate(target.Facts, path);
            if (next.NextAction == RecordContextAction.NotAssessed) return Unmapped(f);
            return Result(f, next.NextActors, next.IsTerminal ? RecordContextAction.None : RecordContextAction.FollowLinkedTask,
                next.IsTerminal, next.ActorState, target.Id) with { PrimaryInputKind = target.Kind };
        }
        if (f.Kind == RecordContextKind.Task && (f.Waiting || f.Blocked))
        {
            var p = f.Inputs.OrderBy(x => x.Failed ? 0 : x.Pending ? 1 : 2)
                .ThenBy(x => x.NeededBy is null).ThenBy(x => x.NeededBy)
                .ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (p is null) return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown);
            var input = Evaluate(p.Facts, path);
            // A terminal predecessor can still be a live dependency blocker while its
            // positive lag runs. It has no actor or transition to resolve.
            if (input.IsTerminal)
                return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown, p.Id) with { PrimaryInputKind = p.Kind };
            if (input.NextAction == RecordContextAction.NotAssessed)
                return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown, p.Id) with { PrimaryInputKind = p.Kind };
            return Result(f, input.NextActors, RecordContextAction.ResolveInput, false, input.ActorState, p.Id) with { PrimaryInputKind = p.Kind };
        }
        return f.Kind switch
        {
            RecordContextKind.Task => f.State switch { TaskStatuses.NotStarted or TaskStatuses.InProgress => Generic(f, f.AssigneeId, f.State == TaskStatuses.NotStarted ? RecordContextAction.Start : RecordContextAction.Complete), TaskStatuses.OnHold => Generic(f, f.AssigneeId, RecordContextAction.Resume), TaskStatuses.ReadyForReview or TaskStatuses.InReview => Generic(f, f.ReviewerId, RecordContextAction.Review), TaskStatuses.RevisionRequired => Generic(f, f.AssigneeId, RecordContextAction.Revise), _ => Unmapped(f) },
            RecordContextKind.Deliverable => Deliverable(f),
            RecordContextKind.Milestone => Generic(f, f.ProjectManagerId, RecordContextAction.ReviewPrerequisites),
            RecordContextKind.Decision => Generic(f, f.DecisionOwnerId, RecordContextAction.Decide),
            RecordContextKind.Risk => Generic(f, f.OwnerId, RecordContextAction.Review),
            RecordContextKind.Issue => Generic(f, f.OwnerId, RecordContextAction.ReviewOrResolve),
            RecordContextKind.Action => f.OwnerKind switch
            {
                ActionOwnerType.User or ActionOwnerType.ExternalParty => Generic(f, f.OwnerId, RecordContextAction.Complete),
                ActionOwnerType.Discipline when f.DisciplineOwnerAvailable => Generic(f, f.DisciplineLeadId, RecordContextAction.Complete),
                _ => Unmapped(f),
            },
            RecordContextKind.Handoff => Handoff(f),
            RecordContextKind.ChangeNotice => Change(f, path),
            RecordContextKind.ChangeAssessment => Assessment(f, path),
            RecordContextKind.WorkConstraint => Generic(f, f.State == ConstraintState.ResolutionProposed ? f.AffectedOwnerId : f.RemovalOwnerId, f.State == ConstraintState.ResolutionProposed ? RecordContextAction.VerifyRemoval : RecordContextAction.Remove),
            RecordContextKind.ReviewFinding => Finding(f),
            RecordContextKind.SubmissionPackage => Submission(f),
            RecordContextKind.SubmissionCheck => SubmissionCheck(f),
            _ => Unmapped(f)
        };
    }
    static RecordContextResult Handoff(RecordContextFacts f) => f.State switch { HandoffStatus.Draft => Generic(f, f.SendingOwnerId, RecordContextAction.Submit), HandoffStatus.Submitted => Generic(f, f.ReceivingOwnerId, RecordContextAction.Accept), HandoffStatus.Accepted => Generic(f, f.ReceivingOwnerId, RecordContextAction.Incorporate), HandoffStatus.ClarificationRequested or HandoffStatus.Returned => Generic(f, f.SendingOwnerId, RecordContextAction.Respond), _ => Unmapped(f) };
    static RecordContextResult Generic(RecordContextFacts f, string? selected, string action)
    {
        // The record's named person is authoritative. A lookup never supplies a substitute.
        if (string.IsNullOrWhiteSpace(selected))
            return Result(f, [], action, false, RecordContextActorState.Unassigned);
        if (f.EligibleActors.Count == 0)
            return Result(f, [new(selected, RecordContextActorState.Unknown)], action, false, RecordContextActorState.Unknown);
        var matches = f.EligibleActors.Where(x => x.Id == selected).Distinct().ToArray();
        if (matches.Length == 0)
            return Result(f, [new(selected, RecordContextActorState.Unknown)], action, false, RecordContextActorState.Unknown);
        return Result(f, matches, action, false,
            matches.Length == 1 ? matches[0].State : RecordContextActorState.Ambiguous);
    }

    static RecordContextResult Deliverable(RecordContextFacts f)
    {
        var transitions = DeliverableStatus.All.Where(to =>
            Workflow.DeliverableStep(f.State, to, f.RequiresReviewGate, f.PreviousStatus)
            && Workflow.DeliverableGuard(f.State, to, f.RequiresReviewGate) is null
            && (to != DeliverableStatus.InReview || f.ReviewerId is not null)
            && (to is not (DeliverableStatus.ReadyToIssue or DeliverableStatus.Issued)
                || !f.HasRequiredReviewPackage || f.RequiredReviewSatisfied == true)).ToArray();
        return Generic(f, f.OwnerId, RecordContextAction.Transition) with { NextTransitions = transitions };
    }

    /// Pure presentation classification. Canonical package Ready/fingerprint remain the live evaluator's authority.
    public static string? SubmissionBlockerState(string code) => code switch
    {
        "submission.check_pending" or "submission.handoff_pending" or "submission.change_pending"
            or "submission.review_not_current" or "submission.review_round_not_approved" => SubmissionCheckStatus.Pending,
        "submission.check_failed" or "submission.invalid_waiver" or "submission.empty_manifest" or "submission.empty_checks"
            or "submission.coordinator_inactive" or "submission.check_owner_inactive" or "submission.deliverable_missing"
            or "submission.revision_missing" or "submission.revision_changed" or "submission.source_link_invalid"
            or "submission.review_manifest_mismatch" or "submission.blocking_finding" or "submission.input_changed" => SubmissionCheckStatus.Fail,
        _ => null,
    };

    public static string? SubmissionCheckState(string stored, bool derived, IReadOnlyList<string> blockerCodes)
    {
        var states = blockerCodes.Select(SubmissionBlockerState).ToArray();
        if (states.Any(x => x is null)) return null;
        if (states.Contains(SubmissionCheckStatus.Fail)) return SubmissionCheckStatus.Fail;
        if (states.Contains(SubmissionCheckStatus.Pending)) return SubmissionCheckStatus.Pending;
        return derived ? SubmissionCheckStatus.Pass : SubmissionCheckStatus.All.Contains(stored, StringComparer.Ordinal) ? stored : null;
    }

    static RecordContextResult Submission(RecordContextFacts f)
    {
        var p = f.SubmissionProgress;
        if (p is null) return Unmapped(f);
        if (f.State == SubmissionStatus.Draft)
            return Generic(f, f.CoordinatorId, p.CoordinatorEligible ? RecordContextAction.StartChecking : RecordContextAction.NotAssessed);
        if (!p.Assessed) return Unmapped(f);
        if (f.State == SubmissionStatus.Ready)
            return Generic(f, f.ProjectManagerId, p.ProjectManagerEligible ? RecordContextAction.Issue : RecordContextAction.NotAssessed);
        if (f.State != SubmissionStatus.Checking) return Unmapped(f);
        var check = f.Inputs.Where(x => x.Facts.Kind == RecordContextKind.SubmissionCheck && x.Facts.SubmissionCheckProgress?.Blocking == true)
            .OrderBy(x => x.Facts.State == SubmissionCheckStatus.Fail ? 0 : 1).ThenBy(x => x.NeededBy is null).ThenBy(x => x.NeededBy)
            .ThenBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        if (check is null)
            return Generic(f, f.CoordinatorId, p.CoordinatorEligible && !p.UnmappedBlockers ? RecordContextAction.RunChecks : RecordContextAction.NotAssessed);
        var progress = check.Facts.SubmissionCheckProgress!;
        return Generic(f, check.Facts.OwnerId, progress.Known && progress.SourcesKnown && progress.OwnerEligible && !p.UnmappedBlockers
            ? RecordContextAction.ResolveCheck : RecordContextAction.NotAssessed) with { PrimaryInputId = check.Id, PrimaryInputKind = check.Kind };
    }

    static RecordContextResult SubmissionCheck(RecordContextFacts f)
    {
        var p = f.SubmissionCheckProgress;
        if (p is null || !p.Known) return Unmapped(f);
        return Generic(f, f.OwnerId, p.OwnerEligible && p.SourcesKnown ? RecordContextAction.ResolveCheck : RecordContextAction.NotAssessed);
    }

    static RecordContextResult Finding(RecordContextFacts f)
    {
        var p = f.FindingProgress;
        if (p is null || !p.RelationshipsValid || !p.PackageLive || !p.CurrentRound) return Unmapped(f);
        if (f.State == FindingStatus.Open)
            return Generic(f, f.OwnerId, p.ResolverEligible && p.PostActionApprovalAllowed ? RecordContextAction.RespondToComment : RecordContextAction.NotAssessed);
        if (f.State == FindingStatus.Responded)
        {
            if (!p.AuthorsKnown || !p.VerifierIndependent || !p.HasResponseEvidence || !p.PostActionApprovalAllowed) return Unmapped(f);
            return Generic(f, f.VerifierId, p.VerifierEligible ? RecordContextAction.VerifyClosure : RecordContextAction.NotAssessed);
        }
        if (f.State == FindingStatus.Withdrawn && p.SeverityBlocking && !p.WithdrawalAcknowledged)
            return Generic(f, f.CoordinatorId, p.CoordinatorEligible && p.PostActionApprovalAllowed ? RecordContextAction.AcknowledgeWithdrawal : RecordContextAction.NotAssessed);
        return Unmapped(f);
    }

    static RecordContextResult Assessment(RecordContextFacts f, HashSet<string> path)
    {
        var p = f.AssessmentProgress;
        if (p is null || p.NoticeStatus != ChangeStatus.Open || !p.TargetMatches) return Unmapped(f);
        if (!p.TargetEligible) return Generic(f, f.OwnerId, RecordContextAction.NotAssessed);
        if (f.State is AssessmentStatus.Pending or AssessmentStatus.Clarification || !p.HasRationale || !p.HasEvidence)
            return Generic(f, f.OwnerId, RecordContextAction.AssessChange);
        // An incomplete Resolved row cannot run the resolve command again.
        if (f.State == AssessmentStatus.Resolved) return Unmapped(f);
        if (p.RetainOld && !p.RetentionApproved)
            return Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unassigned);
        if (!p.RetainOld && !p.AdoptedCurrent)
            return p.CanAdopt ? Generic(f, f.OwnerId, RecordContextAction.AdoptRevision) : Unmapped(f);
        if (f.State != AssessmentStatus.UpdateRequired || !p.CorrectionEligible) return Unmapped(f);
        var corrections = f.Inputs.Where(x => x.Kind == RecordContextKind.Task).ToArray();
        var correction = corrections.Length == 1 ? corrections[0] : null;
        if (correction is null) return Unmapped(f);
        if (correction.Facts.State == TaskStatuses.Complete)
        {
            if (f.ReviewerId is null) return Generic(f, null, RecordContextAction.VerifyChange);
            if (!p.ReviewerIndependent) return Unmapped(f);
            return Generic(f, f.ReviewerId, p.ReviewerEligible ? RecordContextAction.VerifyChange : RecordContextAction.NotAssessed);
        }
        var next = Evaluate(correction.Facts, new(path, StringComparer.Ordinal));
        if (next.IsTerminal || next.NextAction == RecordContextAction.NotAssessed) return Unmapped(f);
        return Result(f, next.NextActors, RecordContextAction.FollowLinkedTask, false, next.ActorState, correction.Id)
            with { PrimaryInputKind = correction.Kind };
    }

    static RecordContextResult Change(RecordContextFacts f, HashSet<string> path)
    {
        if (f.Inputs.Count > 0)
        {
            if (f.State != ChangeStatus.Open) return Unmapped(f);
            var work = f.Inputs.Where(x => x.Facts.AssessmentProgress?.Complete != true
                || x.Facts.AssessmentProgress is { TargetMatches: false } or { TargetEligible: false }).ToArray();
            var pending = work.Where(x => x.Facts.State is AssessmentStatus.Pending or AssessmentStatus.Clarification).ToArray();
            var selected = pending.Length > 0 ? pending : work;
            if (selected.Length == 0) return Unmapped(f); // Closing is still the notice command's separate eligibility check.
            var steps = selected.Select(x => Evaluate(x.Facts, new(path, StringComparer.Ordinal))).ToArray();
            var actors = steps.SelectMany(x => x.NextActors).Distinct().OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
            var missing = steps.Any(x => x.NextActors.Count == 0);
            var unknown = steps.Any(x => x.NextAction == RecordContextAction.NotAssessed) || missing;
            return Result(f, actors, unknown ? RecordContextAction.NotAssessed : pending.Length > 0 ? RecordContextAction.AssessChange : RecordContextAction.FollowUp,
                false, missing && actors.Length > 0 ? RecordContextActorState.Partial : unknown ? actors.Length == 0 && steps.All(x => x.ActorState == RecordContextActorState.Unassigned)
                    ? RecordContextActorState.Unassigned : RecordContextActorState.Unknown
                    : actors.Length > 1 ? RecordContextActorState.Ambiguous : steps[0].ActorState);
        }
        var ids = f.HasPendingAssessments ? f.PendingAssessmentOwnerIds : f.RequiredUpdateOwnerIds;
        if (!f.HasPendingAssessments && !f.HasRequiredUpdates) return Unmapped(f);
        var action = f.HasPendingAssessments ? RecordContextAction.AssessChange : RecordContextAction.FollowUp;
        var distinct = ids.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 0) return Result(f, [], action, false, RecordContextActorState.Unassigned);
        var results = distinct.Select(id => Generic(f, id, action)).ToArray();
        return Result(f, results.SelectMany(x => x.NextActors).ToArray(), action, false,
            distinct.Length > 1 ? RecordContextActorState.Ambiguous : results[0].ActorState);
    }

    static IReadOnlyList<string> CanonicalStates(string kind) => kind switch
    {
        RecordContextKind.Task => TaskStatuses.All,
        RecordContextKind.Deliverable => DeliverableStatus.All,
        RecordContextKind.Milestone => MilestoneStatus.All,
        RecordContextKind.Decision => DecisionStatus.All,
        RecordContextKind.Risk => RiskStatus.All,
        RecordContextKind.Issue => IssueStatus.All,
        RecordContextKind.Action => ActionStatus.All,
        RecordContextKind.Handoff => HandoffStatus.All,
        RecordContextKind.ChangeNotice => ChangeStatus.All,
        RecordContextKind.ChangeAssessment => AssessmentStatus.All,
        RecordContextKind.WorkConstraint => ConstraintState.All,
        RecordContextKind.ReviewFinding => FindingStatus.All,
        RecordContextKind.ReviewPackage => ReviewStatus.All,
        RecordContextKind.SubmissionPackage => SubmissionStatus.All,
        RecordContextKind.SubmissionCheck => SubmissionCheckStatus.All,
        _ => []
    };

    static RecordContextResult Unmapped(RecordContextFacts f) => Result(f, [], RecordContextAction.NotAssessed, false, RecordContextActorState.Unknown);
    static bool Terminal(RecordContextFacts f) => f.Kind switch { RecordContextKind.Task => TaskStatuses.IsTerminal(f.State), RecordContextKind.Deliverable => DeliverableStatus.IsTerminal(f.State), RecordContextKind.Milestone => f.State is MilestoneStatus.Complete or MilestoneStatus.Cancelled, RecordContextKind.Decision => f.State is DecisionStatus.Decided or DecisionStatus.Cancelled, RecordContextKind.Risk => f.State is RiskStatus.Closed or RiskStatus.Realised, RecordContextKind.Issue => f.State is IssueStatus.Resolved or IssueStatus.Cancelled, RecordContextKind.Action => f.State is ActionStatus.Complete or ActionStatus.Cancelled, RecordContextKind.Handoff => f.State is HandoffStatus.Incorporated or HandoffStatus.Cancelled, RecordContextKind.ChangeNotice => f.State is ChangeStatus.Closed or ChangeStatus.Cancelled, RecordContextKind.ChangeAssessment => f.AssessmentProgress is { NoticeStatus: ChangeStatus.Closed or ChangeStatus.Cancelled } or { NoticeStatus: ChangeStatus.Open, Complete: true, TargetMatches: true, TargetEligible: true }, RecordContextKind.WorkConstraint => f.State is ConstraintState.VerifiedRemoved or ConstraintState.Cancelled, RecordContextKind.ReviewFinding => f.State == FindingStatus.VerifiedClosed || f.State == FindingStatus.Withdrawn && f.FindingProgress is { RelationshipsValid: true } p && (!p.SeverityBlocking || p.WithdrawalAcknowledged), RecordContextKind.ReviewPackage => f.State is ReviewStatus.Cancelled or ReviewStatus.Superseded, RecordContextKind.SubmissionPackage => f.State is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled, RecordContextKind.SubmissionCheck => f.SubmissionCheckProgress?.TerminalPackage == true || f.State is SubmissionCheckStatus.Pass or SubmissionCheckStatus.NotApplicable, _ => false };
    static RecordContextResult Result(RecordContextFacts f, IReadOnlyList<RecordContextActor> a, string action, bool terminal, RecordContextActorState state, string? primary = null) => new(f.OwnerId, a, f.NeededBy, action, terminal, primary, f.Inputs, state) { RequiresReviewGate = f.RequiresReviewGate };
}
