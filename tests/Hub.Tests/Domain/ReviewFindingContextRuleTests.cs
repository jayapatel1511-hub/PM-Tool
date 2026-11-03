using Hub.Domain;
namespace Hub.Tests.Domain;

public sealed class ReviewFindingContextRuleTests
{
    static RecordContextFindingProgress P => new(true, true, true, true, true, true, true, true, true, true, false, true);
    static RecordContextFacts F(string state = FindingStatus.Open) => new(RecordContextKind.ReviewFinding, "comment", state, "resolver",
        CoordinatorId: "coordinator", OriginatorId: "originator", VerifierId: "verifier",
        Actors: [new("resolver"), new("verifier"), new("coordinator")]) { FindingProgress = P };

    [Fact]
    public void Open_responded_and_withdrawal_use_their_recorded_people()
    {
        foreach (var (state, actor, action) in new[] { (FindingStatus.Open, "resolver", RecordContextAction.RespondToComment),
            (FindingStatus.Responded, "verifier", RecordContextAction.VerifyClosure), (FindingStatus.Withdrawn, "coordinator", RecordContextAction.AcknowledgeWithdrawal) })
        {
            var result = RecordContextRules.Evaluate(F(state));
            Assert.Equal("resolver", result.OwnerId); Assert.Null(result.NeededBy); Assert.Equal(actor, Assert.Single(result.NextActors).Id);
            Assert.Equal(action, result.NextAction); Assert.False(result.IsTerminal);
        }
    }

    [Fact]
    public void Missing_verifier_does_not_fall_back_to_originator()
    {
        var r = RecordContextRules.Evaluate(F(FindingStatus.Responded) with { VerifierId = null });
        Assert.Empty(r.NextActors); Assert.Equal(RecordContextActorState.Unassigned, r.ActorState);
    }

    [Theory]
    [InlineData(FindingStatus.VerifiedClosed)]
    [InlineData(FindingStatus.Withdrawn)]
    public void Terminal_retains_owner_and_absent_date(string state)
    {
        var result = RecordContextRules.Evaluate(F(state) with { FindingProgress = P with { WithdrawalAcknowledged = true } });
        Assert.True(result.IsTerminal); Assert.Empty(result.NextActors); Assert.Equal(RecordContextAction.None, result.NextAction);
        Assert.Equal("resolver", result.OwnerId); Assert.Null(result.NeededBy);
    }

    [Fact]
    public void Advisory_withdrawal_is_terminal_without_acknowledgement()
    { Assert.True(RecordContextRules.Evaluate(F(FindingStatus.Withdrawn) with { FindingProgress = P with { SeverityBlocking = false } }).IsTerminal); }

    [Theory]
    [InlineData("history")]
    [InlineData("frozen")]
    [InlineData("relationship")]
    [InlineData("authors")]
    [InlineData("independence")]
    [InlineData("evidence")]
    [InlineData("approval")]
    public void Unsafe_verification_is_unassessed(string reason)
    {
        var p = reason switch { "history" => P with { CurrentRound = false }, "frozen" => P with { PackageLive = false },
            "relationship" => P with { RelationshipsValid = false }, "authors" => P with { AuthorsKnown = false },
            "independence" => P with { VerifierIndependent = false }, "evidence" => P with { HasResponseEvidence = false }, _ => P with { PostActionApprovalAllowed = false } };
        var r = RecordContextRules.Evaluate(F(FindingStatus.Responded) with { FindingProgress = p });
        Assert.Equal(RecordContextAction.NotAssessed, r.NextAction); Assert.Empty(r.NextActors); Assert.False(r.IsTerminal);
    }

    [Fact]
    public void Response_does_not_require_independent_resolver()
    {
        var r = RecordContextRules.Evaluate(F() with { FindingProgress = P with { AuthorsKnown = false, VerifierIndependent = false } });
        Assert.Equal(RecordContextAction.RespondToComment, r.NextAction); Assert.Equal("resolver", Assert.Single(r.NextActors).Id);
    }

    [Fact]
    public void Inactive_named_actor_is_retained_without_an_executable_action()
    {
        var r = RecordContextRules.Evaluate(F(FindingStatus.Responded) with { FindingProgress = P with { VerifierEligible = false }, Actors = [new("verifier", RecordContextActorState.Inactive)] });
        Assert.Equal("verifier", Assert.Single(r.NextActors).Id); Assert.Equal(RecordContextActorState.Inactive, r.ActorState);
        Assert.Equal(RecordContextAction.NotAssessed, r.NextAction);
    }
}
