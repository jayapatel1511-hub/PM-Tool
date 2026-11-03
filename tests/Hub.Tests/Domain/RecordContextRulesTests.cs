using Hub.Domain;
namespace Hub.Tests.Domain;

public sealed class RecordContextRulesTests
{
    static readonly DateOnly Due = new(2026, 11, 5);
    static RecordContextFacts F(string kind, string state, string? owner = "owner", string? actor = null, IReadOnlyList<RecordContextActor>? actors = null, IReadOnlyList<RecordContextInput>? inputs = null) => new(kind, "R-1", state, owner, Due, AssigneeId: actor, ReviewerId: actor, Actors: actors, BlockingInputs: inputs);

    [Fact]
    public void Review_preserves_owner_and_uses_selected_reviewer_identity()
    { var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InReview, actor: "diane", actors: [new("diane"), new("alex")])); Assert.Equal("owner", r.OwnerId); Assert.Equal("diane", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextAction.Review, r.NextAction); }

    [Fact]
    public void Terminal_has_no_actor_but_retains_owner_and_date()
    { var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.Complete, actor: "alex")); Assert.True(r.IsTerminal); Assert.Empty(r.NextActors); Assert.Equal(RecordContextAction.None, r.NextAction); Assert.Equal(Due, r.NeededBy); Assert.Equal("owner", r.OwnerId); }

    [Fact]
    public void Failed_precedes_pending_and_input_state_supplies_its_own_actor()
    { var p = new RecordContextInput(F(RecordContextKind.Handoff, HandoffStatus.Submitted, owner: "sender", actors: [new("receiving")], inputs: null) with { }, Pending: true); var failFacts = new RecordContextFacts(RecordContextKind.WorkConstraint, "C-1", ConstraintState.ResolutionProposed, "removal", Due.AddDays(1), AffectedOwnerId: "affected", Actors: [new("affected")]); var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InProgress, actor: "assignee", inputs: [p, new(failFacts, Failed: true)]) with { Blocked = true }); Assert.Equal("C-1", r.PrimaryInputId); Assert.Equal("affected", Assert.Single(r.NextActors).Id); Assert.Equal(2, r.BlockingInputs.Count); }

    [Fact]
    public void Blocked_without_inputs_is_unassessed_not_ordinary_start()
    { var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InProgress, actor: "assignee") with { Blocked = true }); Assert.Equal(RecordContextAction.NotAssessed, r.NextAction); Assert.Empty(r.NextActors); }

    [Fact]
    public void Missing_or_inactive_selected_actor_is_explicit_and_never_replaced()
    { var missing = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InReview, actor: "diane", actors: [new("alex")])); Assert.Equal(RecordContextActorState.Unknown, missing.ActorState); Assert.Equal("diane", Assert.Single(missing.NextActors).Id); var inactive = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InReview, actor: "diane", actors: [new("diane", RecordContextActorState.Inactive)])); Assert.Equal(RecordContextActorState.Inactive, inactive.ActorState); }

    [Fact]
    public void Canonical_non_item_names_are_aliases_of_registered_kinds()
    { Assert.Equal(ItemType.Action, RecordContextKind.MeetingAction); Assert.Equal("WorkConstraint", RecordContextKind.Constraint); Assert.Equal("ReviewFinding", RecordContextKind.ReviewComment); Assert.Equal("SubmissionPackage", RecordContextKind.Submission); }

    [Fact]
    public void Unknown_state_is_not_assessed()
    { var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, "Not canonical")); Assert.Equal(RecordContextAction.NotAssessed, r.NextAction); Assert.Empty(r.NextActors); }

    [Fact]
    public void Constraint_open_uses_removal_owner_and_proposed_uses_affected_owner()
    { var open = RecordContextRules.Evaluate(new(RecordContextKind.WorkConstraint, "C", ConstraintState.Open, "owner", Due, RemovalOwnerId: "remover")); var proposed = RecordContextRules.Evaluate(new(RecordContextKind.WorkConstraint, "C", ConstraintState.ResolutionProposed, "owner", Due, AffectedOwnerId: "affected")); Assert.Equal("remover", Assert.Single(open.NextActors).Id); Assert.Equal("affected", Assert.Single(proposed.NextActors).Id); }

    [Fact]
    public void Missing_reviewer_never_uses_a_different_person_from_the_directory()
    {
        var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InReview, actors: [new("assignee")]));
        Assert.Equal(RecordContextActorState.Unassigned, r.ActorState);
        Assert.Empty(r.NextActors);
    }

    [Theory]
    [InlineData(RecordContextKind.Deliverable)]
    [InlineData(RecordContextKind.Milestone)]
    [InlineData(RecordContextKind.Decision)]
    [InlineData(RecordContextKind.Risk)]
    [InlineData(RecordContextKind.Issue)]
    [InlineData(RecordContextKind.Action)]
    [InlineData(RecordContextKind.WorkConstraint)]
    [InlineData(RecordContextKind.ReviewFinding)]
    public void Every_record_kind_rejects_unknown_states(string kind)
    {
        var r = RecordContextRules.Evaluate(F(kind, "Not canonical", actors: [new("owner")]));
        Assert.Equal(RecordContextAction.NotAssessed, r.NextAction);
        Assert.Empty(r.NextActors);
    }

    [Theory]
    [InlineData(ChangeStatus.Closed)]
    [InlineData(ChangeStatus.Cancelled)]
    public void Closed_change_notices_are_terminal(string state)
    {
        var r = RecordContextRules.Evaluate(F(RecordContextKind.ChangeNotice, state));
        Assert.True(r.IsTerminal);
        Assert.Equal(RecordContextAction.None, r.NextAction);
        Assert.Empty(r.NextActors);
    }

    [Fact]
    public void Ordinary_constraints_are_not_discarded_without_check_flags()
    {
        var input = new RecordContextInput(new(RecordContextKind.WorkConstraint, "C-1", ConstraintState.Open,
            "removal", Due, RemovalOwnerId: "removal", Actors: [new("removal")]));
        var r = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.NotStarted, inputs: [input]) with { Blocked = true });
        Assert.Equal("C-1", r.PrimaryInputId);
        Assert.Equal("removal", Assert.Single(r.NextActors).Id);
    }

    [Fact]
    public void Required_review_gate_remains_visible_and_changes_deliverable_transitions()
    {
        var facts = F(RecordContextKind.Deliverable, DeliverableStatus.InProgress);
        var ordinary = RecordContextRules.Evaluate(facts);
        var reviewed = RecordContextRules.Evaluate(facts with { RequiresReviewGate = true });
        Assert.Contains(DeliverableStatus.ReadyToIssue, ordinary.NextTransitions);
        Assert.DoesNotContain(DeliverableStatus.ReadyToIssue, reviewed.NextTransitions);
        Assert.True(reviewed.RequiresReviewGate);
    }

    [Fact]
    public void Pending_change_actors_are_explicit_sorted_and_distinct()
    {
        var facts = new RecordContextFacts(RecordContextKind.ChangeNotice, "CH-1", ChangeStatus.Open,
            "coordinator", Due, Actors: [new("alex"), new("diane")], HasPendingAssessments: true)
        { PendingAssessmentOwnerIds = ["diane", "alex", "diane"] };
        var r = RecordContextRules.Evaluate(facts);
        Assert.Equal(new[] { "alex", "diane" }, r.NextActors.Select(x => x.Id));
        Assert.Equal(RecordContextActorState.Ambiguous, r.ActorState);
        Assert.Equal("coordinator", r.OwnerId);
    }

    [Fact]
    public void Partial_directory_preserves_each_recorded_pending_actor()
    {
        var facts = new RecordContextFacts(RecordContextKind.ChangeNotice, "CH-1", ChangeStatus.Open,
            "coordinator", Due, Actors: [new("alex")], HasPendingAssessments: true)
        { PendingAssessmentOwnerIds = ["diane", "alex"] };
        var result = RecordContextRules.Evaluate(facts);

        Assert.Equal(new[] { "alex", "diane" }, result.NextActors.Select(x => x.Id));
        Assert.Equal(RecordContextActorState.Active, result.NextActors[0].State);
        Assert.Equal(RecordContextActorState.Unknown, result.NextActors[1].State);
        Assert.Null(result.NextActor);
        Assert.Equal(RecordContextActorState.Ambiguous, result.ActorState);
    }

    [Fact]
    public void Cyclic_blocking_inputs_return_unassessed_instead_of_recursing()
    {
        var aInputs = new List<RecordContextInput>();
        var bInputs = new List<RecordContextInput>();
        var a = new RecordContextFacts(RecordContextKind.Task, "A", TaskStatuses.InProgress,
            "owner", Due, AssigneeId: "assignee", BlockingInputs: aInputs, Waiting: true);
        var b = new RecordContextFacts(RecordContextKind.Task, "B", TaskStatuses.InProgress,
            "owner", Due, AssigneeId: "assignee", BlockingInputs: bInputs, Waiting: true);
        aInputs.Add(new(b, Pending: true));
        bInputs.Add(new(a, Pending: true));

        var result = RecordContextRules.Evaluate(a);

        Assert.Equal(RecordContextAction.NotAssessed, result.NextAction);
        Assert.Empty(result.NextActors);
    }

    [Fact]
    public void Unresolved_nested_input_does_not_get_a_resolve_action()
    {
        var unknown = new RecordContextFacts("NotRegistered", "X", "anything");
        var result = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InProgress,
            inputs: [new(unknown, Pending: true)]) with { Waiting = true });

        Assert.Equal(RecordContextAction.NotAssessed, result.NextAction);
        Assert.Empty(result.NextActors);
    }

    [Fact]
    public void Terminal_predecessor_still_blocking_is_not_presented_as_resolvable()
    {
        var predecessor = new RecordContextFacts(RecordContextKind.Task, "P-1", TaskStatuses.Complete,
            "owner", Due, AssigneeId: "assignee");
        var result = RecordContextRules.Evaluate(F(RecordContextKind.Task, TaskStatuses.InProgress,
            inputs: [new(predecessor, Pending: true)]) with { Blocked = true });

        Assert.Equal(RecordContextAction.NotAssessed, result.NextAction);
        Assert.Equal(RecordContextActorState.Unknown, result.ActorState);
        Assert.Empty(result.NextActors);
    }

    [Fact]
    public void Deliverable_hold_resume_retains_previous_status_and_obeys_review_guard()
    {
        var facts = new RecordContextFacts(RecordContextKind.Deliverable, "D-1", DeliverableStatus.OnHold,
            "owner", Due) { PreviousStatus = DeliverableStatus.ReadyToIssue };
        var result = RecordContextRules.Evaluate(facts);

        Assert.Contains(DeliverableStatus.ReadyToIssue, result.NextTransitions);
        Assert.DoesNotContain(DeliverableStatus.ReadyToIssue, RecordContextRules.Evaluate(facts with { RequiresReviewGate = true }).NextTransitions);
    }
    [Fact]
    public void Deliverable_candidates_obey_reviewer_and_independent_package_prerequisites()
    {
        var facts = new RecordContextFacts(RecordContextKind.Deliverable, "D", DeliverableStatus.InProgress,
            OwnerId: "alex", ReviewerId: null);
        Assert.DoesNotContain(DeliverableStatus.InReview, RecordContextRules.Evaluate(facts).NextTransitions);
        Assert.Contains(DeliverableStatus.ReadyToIssue, RecordContextRules.Evaluate(facts).NextTransitions);
        var gated = facts with { HasRequiredReviewPackage = true, RequiredReviewSatisfied = false };
        Assert.DoesNotContain(DeliverableStatus.ReadyToIssue, RecordContextRules.Evaluate(gated).NextTransitions);
        Assert.Contains(DeliverableStatus.ReadyToIssue, RecordContextRules.Evaluate(gated with { RequiredReviewSatisfied = true }).NextTransitions);
        Assert.Contains(DeliverableStatus.InReview, RecordContextRules.Evaluate(facts with { ReviewerId = "diane" }).NextTransitions);
    }

    [Fact]
    public void Discipline_action_retains_owner_and_uses_only_its_recorded_lead()
    {
        var facts = new RecordContextFacts(RecordContextKind.Action, "A", ActionStatus.Open, "discipline:civil", Due,
            Actors: [new("marc", RecordContextActorState.Inactive)])
        { OwnerKind = "Discipline", DisciplineLeadId = "marc" };
        var result = RecordContextRules.Evaluate(facts);
        Assert.Equal("discipline:civil", result.OwnerId);
        Assert.Equal("marc", Assert.Single(result.NextActors).Id);
        Assert.Equal(RecordContextActorState.Inactive, result.ActorState);
        var noLead = RecordContextRules.Evaluate(facts with { DisciplineLeadId = null });
        Assert.Empty(noLead.NextActors); Assert.Equal(RecordContextActorState.Unassigned, noLead.ActorState);
        Assert.Equal(RecordContextAction.NotAssessed, RecordContextRules.Evaluate(facts with { DisciplineOwnerAvailable = false }).NextAction);
    }

    [Fact]
    public void Converted_action_follows_its_task_reviewer_and_never_invents_complete()
    {
        var task = new RecordContextFacts(RecordContextKind.Task, "T", TaskStatuses.InReview, "alex", Due,
            AssigneeId: "alex", ReviewerId: "diane", Actors: [new("diane")]);
        var action = new RecordContextFacts(RecordContextKind.Action, "A", ActionStatus.InProgress, "discipline:civil", Due.AddDays(-2),
            BlockingInputs: [new(task)]) { FollowsTask = true };
        var result = RecordContextRules.Evaluate(action);
        Assert.Equal("discipline:civil", result.OwnerId); Assert.Equal(action.NeededBy, result.NeededBy);
        Assert.Equal("diane", Assert.Single(result.NextActors).Id);
        Assert.Equal(RecordContextAction.FollowLinkedTask, result.NextAction);
        Assert.Equal("T", result.PrimaryInputId); Assert.Equal(RecordContextKind.Task, result.PrimaryInputKind);
        Assert.Equal(RecordContextAction.NotAssessed, RecordContextRules.Evaluate(action with { BlockingInputs = [] }).NextAction);
        Assert.Equal(RecordContextAction.None, RecordContextRules.Evaluate(action with { State = ActionStatus.Complete }).NextAction);
    }

    [Theory]
    [InlineData(ReviewStatus.Approved, false)]
    [InlineData(ReviewStatus.Draft, false)]
    [InlineData(ReviewStatus.Cancelled, true)]
    [InlineData(ReviewStatus.Superseded, true)]
    public void Coordination_review_summaries_keep_owner_without_inventing_an_action_or_date(string status, bool terminal)
    {
        var result = RecordContextRules.Evaluate(new(RecordContextKind.ReviewPackage, "review", status, "coordinator"));
        Assert.Equal("coordinator", result.OwnerId); Assert.Null(result.NeededBy);
        Assert.Equal(terminal, result.IsTerminal); Assert.Empty(result.NextActors);
        Assert.Equal(terminal ? RecordContextAction.None : RecordContextAction.NotAssessed, result.NextAction);
    }

}
