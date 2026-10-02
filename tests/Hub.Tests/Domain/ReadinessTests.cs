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

    /// The §10.8 precedence table over every combination: an unknown check, a known failed check, a failed Basis check, an
    /// open constraint, the other checks satisfied or not applicable, and each kind of assumption permission.
    public static TheoryData<bool, bool, bool, bool, bool, string> Combinations()
    {
        var rows = new TheoryData<bool, bool, bool, bool, bool, string>();
        foreach (var unknown in new[] { false, true }) foreach (var failed in new[] { false, true }) foreach (var basis in new[] { false, true })
        foreach (var constraint in new[] { false, true }) foreach (var notApplicable in new[] { false, true })
        foreach (var permit in new[] { "none", "valid", "expired", "changed", "noVerifier", "unapproved", "noRisk" })
            rows.Add(unknown, failed, basis, constraint, notApplicable, permit);
        return rows;
    }

    [Theory]
    [MemberData(nameof(Combinations))]
    public void Precedence_is_needs_assessment_then_not_ready_and_every_reason_stays_listed(bool unknown, bool failed, bool basis,
        bool constraint, bool notApplicable, string permit)
    {
        var checks = ReadinessCheckCode.All.Select(code => code switch
        {
            ReadinessCheckCode.Handoff when unknown => new ReadinessCheck(code, true, null),
            ReadinessCheckCode.ReviewGate when failed => new ReadinessCheck(code, true, false),
            ReadinessCheckCode.Basis when basis => new ReadinessCheck(code, true, false),
            _ => notApplicable ? new ReadinessCheck(code, false, null) : new ReadinessCheck(code, true, true),
        }).ToArray();
        var valid = new ReadinessPermission(true, Today, true, true, "Preliminary layout", "Confirm utility depth");
        var permission = permit switch
        {
            "valid" => valid, "expired" => valid with { ExpiresOn = Today.AddDays(-1) }, "changed" => valid with { SameBasisVersion = false },
            "noVerifier" => valid with { HasVerifier = false }, "unapproved" => valid with { Approved = false }, "noRisk" => valid with { Risk = " " },
            _ => null,
        };
        var result = ReadinessRules.Evaluate(checks, permission, Today, constraint ? [ReadinessRules.ConstraintBlocker] : null);

        // An approved assumption that expired, changed version or lost its verifier returns the work to assessment (AC-RDY-03).
        var reassess = basis && permit is "expired" or "changed" or "noVerifier";
        var expected = unknown || reassess ? ReadinessState.NeedsAssessment
            : !failed && !basis && !constraint ? ReadinessState.Ready
            : basis && !failed && !constraint && permit == "valid" ? ReadinessState.ProceedUnderAssumption
            : ReadinessState.NotReady;
        Assert.Equal(expected, result.State);
        Assert.Equal(unknown ? [ReadinessCheckCode.Handoff] : Array.Empty<string>(), result.Unknown);
        string[] blocked = [.. new[] { failed ? ReadinessCheckCode.ReviewGate : null, basis ? ReadinessCheckCode.Basis : null,
            constraint ? ReadinessRules.ConstraintBlocker : null }.OfType<string>()];
        Assert.Equal(blocked.Order(), result.Blocked.Order()); // known failures stay visible even under Needs Assessment
    }

    [Fact]
    public void Open_constraint_never_hides_an_unknown_check()
    {
        var unknown = ReadinessRules.Evaluate(Set(ReadinessCheckCode.Decision, null, null), null, Today, [ReadinessRules.ConstraintBlocker]);
        Assert.Equal((ReadinessState.NeedsAssessment, ReadinessCheckCode.Decision, ReadinessRules.ConstraintBlocker),
            (unknown.State, Assert.Single(unknown.Unknown), Assert.Single(unknown.Blocked)));
        Assert.Equal(ReadinessState.NotReady, ReadinessRules.Evaluate(Clear(), null, Today, [ReadinessRules.ConstraintBlocker]).State);
    }

    [Fact]
    public void Assumption_permission_cannot_override_an_unissued_submission_gate()
    {
        var permit = new ReadinessPermission(true, Today, true, true, "Preliminary layout", "Confirm utility depth");
        var gated = Set(ReadinessCheckCode.Basis, true, false).Select(x => x.Code == ReadinessCheckCode.SubmissionGate ? x with { Satisfied = false } : x);
        Assert.Equal(ReadinessState.NotReady, ReadinessRules.Evaluate(gated, permit, Today).State);
        var unknown = Set(ReadinessCheckCode.Basis, true, false).Select(x => x.Code == ReadinessCheckCode.SubmissionGate ? x with { Satisfied = null } : x);
        Assert.Equal(ReadinessState.NeedsAssessment, ReadinessRules.Evaluate(unknown, permit, Today).State);
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
