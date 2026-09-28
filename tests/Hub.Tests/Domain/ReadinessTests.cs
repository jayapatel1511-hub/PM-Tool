using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class ReadinessTests
{
    static readonly DateOnly Today = new(2026, 9, 27);
    static ReadinessCheck[] Clear() => ReadinessCheckCode.All.Select(code => new ReadinessCheck(code, true, true)).ToArray();
    static ReadinessCheck[] Set(string code, bool? applies, bool? satisfied) => Clear()
        .Select(x => x.Code == code ? new ReadinessCheck(code, applies, satisfied) : x).ToArray();

    [Fact]
    public void Submitted_handoff_is_not_ready_and_unknown_applicability_never_becomes_ready()
    {
        var missingHandoff = ReadinessRules.Evaluate(Set(ReadinessCheckCode.Handoff, true, false), null, Today);
        Assert.Equal(ReadinessState.NotReady, missingHandoff.State);
        Assert.Contains(ReadinessCheckCode.Handoff, missingHandoff.Blocked);
        var unknown = ReadinessRules.Evaluate(Set(ReadinessCheckCode.Handoff, null, null), null, Today);
        Assert.Equal(ReadinessState.NeedsAssessment, unknown.State);
        Assert.Contains(ReadinessCheckCode.Handoff, unknown.Unknown);
        Assert.Throws<ArgumentException>(() => ReadinessRules.Evaluate([], null, Today));
    }

    [Fact]
    public void Assumption_permission_is_narrow_and_expiry_or_changed_version_returns_to_assessment()
    {
        var basis = Set(ReadinessCheckCode.Basis, true, false);
        var permit = new ReadinessPermission(true, Today, true, true, "Preliminary layout", "Confirm utility depth");
        Assert.Equal(ReadinessState.ProceedUnderAssumption, ReadinessRules.Evaluate(basis, permit, Today).State);
        Assert.Equal(ReadinessState.NeedsAssessment, ReadinessRules.Evaluate(basis, permit with { ExpiresOn = Today.AddDays(-1) }, Today).State);
        Assert.Equal(ReadinessState.NeedsAssessment, ReadinessRules.Evaluate(basis, permit with { SameBasisVersion = false }, Today).State);
        Assert.Equal(ReadinessState.NotReady, ReadinessRules.Evaluate(basis, permit with { Approved = false }, Today).State);
        Assert.Equal(ReadinessState.Ready, ReadinessRules.Evaluate(Clear(), permit with { ExpiresOn = Today.AddDays(-1) }, Today).State);
        Assert.Equal(ReadinessState.NotReady, ReadinessRules.Evaluate(basis
            .Select(x => x.Code == ReadinessCheckCode.ReviewGate ? x with { Satisfied = false } : x), permit, Today).State);
    }

    [Fact]
    public void Snapshot_keeps_withdrawn_promise_in_denominator_and_requires_performer_commit()
    {
        var owner = Guid.NewGuid(); var chair = Guid.NewGuid();
        Assert.False(WeeklyCommitmentRules.MayCommit(chair, owner));
        Assert.True(WeeklyCommitmentRules.MayCommit(owner, owner));
        Assert.False(WeeklyCommitmentRules.MayClose(CommitmentState.Committed, CommitmentState.Met, false, false));
        Assert.True(WeeklyCommitmentRules.MayClose(CommitmentState.Committed, CommitmentState.NotMet, false, true));
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        var states = ids.ToDictionary(id => id, _ => CommitmentState.Met);
        states[ids[0]] = CommitmentState.Withdrawn;
        var outcome = WeeklyCommitmentRules.Outcome(ids, states);
        Assert.Equal(new WeeklyOutcome(4, 5, 1), outcome);
    }
}
