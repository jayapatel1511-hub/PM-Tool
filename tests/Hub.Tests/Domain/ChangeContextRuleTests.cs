using Hub.Domain;
namespace Hub.Tests.Domain;

public sealed class ChangeContextRuleTests
{
    static readonly DateOnly Due = new(2026, 11, 5);
    static RecordContextAssessmentProgress P => new(ChangeStatus.Open, false, true, true, true, true, true, true, false, true, true, true, true);
    static RecordContextFacts Task(string state = TaskStatuses.InReview) => new(RecordContextKind.Task, "task", state, "producer", AssigneeId: "producer", ReviewerId: "checker", Actors: [new("producer"), new("checker")]);
    static RecordContextFacts A(string state = AssessmentStatus.UpdateRequired) => new(RecordContextKind.ChangeAssessment, "assessment", state, "assessor", Due,
        ReviewerId: "verifier", Actors: [new("assessor"), new("verifier")], BlockingInputs: [new(Task())]) { AssessmentProgress = P };

    [Theory]
    [InlineData(AssessmentStatus.Pending)]
    [InlineData(AssessmentStatus.Clarification)]
    public void Pending_actor_is_recorded_owner_with_inherited_notice_date(string state)
    {
        var r = RecordContextRules.Evaluate(A(state));
        Assert.Equal("assessor", r.OwnerId); Assert.Equal(Due, r.NeededBy);
        Assert.Equal("assessor", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextAction.AssessChange, r.NextAction);
    }

    [Fact]
    public void Correction_uses_its_reviewer_without_replacing_accountability()
    {
        var r = RecordContextRules.Evaluate(A());
        Assert.Equal("assessor", r.OwnerId); Assert.Equal("checker", Assert.Single(r.NextActors).Id);
        Assert.Equal(RecordContextAction.FollowLinkedTask, r.NextAction); Assert.Equal("task", r.PrimaryInputId);
    }

    [Fact]
    public void Complete_correction_selects_recorded_verifier_and_missing_stays_unassigned()
    {
        var facts = A() with { BlockingInputs = [new(Task(TaskStatuses.Complete))] };
        var r = RecordContextRules.Evaluate(facts);
        Assert.Equal("verifier", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextAction.VerifyChange, r.NextAction);
        var missing = RecordContextRules.Evaluate(facts with { ReviewerId = null });
        Assert.Empty(missing.NextActors); Assert.Equal(RecordContextActorState.Unassigned, missing.ActorState);
    }

    [Fact]
    public void Retention_has_no_invented_approver_and_adoption_is_separate()
    {
        var retained = RecordContextRules.Evaluate(A() with { AssessmentProgress = P with { RetentionApproved = false } });
        Assert.Equal(RecordContextAction.NotAssessed, retained.NextAction); Assert.Empty(retained.NextActors);
        Assert.Equal(RecordContextActorState.Unassigned, retained.ActorState);
        var adoption = RecordContextRules.Evaluate(A(AssessmentStatus.Unaffected) with { AssessmentProgress = P with { RetainOld = false } });
        Assert.Equal(RecordContextAction.AdoptRevision, adoption.NextAction); Assert.Equal("assessor", Assert.Single(adoption.NextActors).Id);
    }

    [Theory]
    [InlineData(AssessmentStatus.Resolved, TaskStatuses.InProgress)]
    [InlineData(AssessmentStatus.UpdateRequired, TaskStatuses.Cancelled)]
    public void Incomplete_resolved_or_cancelled_correction_never_advertises_verification(string assessment, string task)
    {
        var r = RecordContextRules.Evaluate(A(assessment) with { BlockingInputs = [new(Task(task))] });
        Assert.False(r.IsTerminal); Assert.Equal(RecordContextAction.NotAssessed, r.NextAction);
    }

    [Fact]
    public void Complete_sql_row_still_needs_matching_eligible_target()
    {
        var r = RecordContextRules.Evaluate(A(AssessmentStatus.Unaffected) with { AssessmentProgress = P with { Complete = true, TargetMatches = false } });
        Assert.False(r.IsTerminal); Assert.Equal(RecordContextAction.NotAssessed, r.NextAction);
    }

    [Fact]
    public void Parent_preserves_mixed_known_and_unknown_work()
    {
        var known = A(AssessmentStatus.Pending);
        var unknown = A(AssessmentStatus.Clarification) with { Id = "unknown", AssessmentProgress = P with { TargetMatches = false } };
        var parent = new RecordContextFacts(RecordContextKind.ChangeNotice, "notice", ChangeStatus.Open, "noticeOwner", Due,
            BlockingInputs: [new(known), new(unknown)]);
        var r = RecordContextRules.Evaluate(parent);
        Assert.Equal("assessor", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextActorState.Partial, r.ActorState);
        Assert.Equal(RecordContextAction.NotAssessed, r.NextAction);
    }

    [Fact]
    public void Two_assessments_sharing_a_correction_do_not_create_a_false_cycle()
    {
        var parent = new RecordContextFacts(RecordContextKind.ChangeNotice, "notice", ChangeStatus.Open, "noticeOwner", Due,
            BlockingInputs: [new(A()), new(A() with { Id = "assessment2" })]);
        var r = RecordContextRules.Evaluate(parent);
        Assert.Equal("checker", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextAction.FollowUp, r.NextAction);
    }

    [Theory]
    [InlineData(ChangeStatus.Closed)]
    [InlineData(ChangeStatus.Cancelled)]
    public void Terminal_parent_retains_child_owner_and_notice_date(string status)
    {
        var r = RecordContextRules.Evaluate(A() with { AssessmentProgress = P with { NoticeStatus = status } });
        Assert.True(r.IsTerminal); Assert.Empty(r.NextActors); Assert.Equal("assessor", r.OwnerId); Assert.Equal(Due, r.NeededBy);
    }
}
