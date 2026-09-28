namespace Hub.Domain;

public static class ReadinessState
{
    public const string NeedsAssessment = "Needs Assessment", NotReady = "Not Ready", Ready = "Ready",
        ProceedUnderAssumption = "Proceed under Assumption";
    public static readonly string[] All = [NeedsAssessment, NotReady, Ready, ProceedUnderAssumption];
}

public static class ReadinessCheckCode
{
    public const string Handoff = "Handoff", Predecessor = "Predecessor", Decision = "Decision",
        Basis = "Basis", ProductionOwner = "Production Owner", ProductionCapacity = "Production Capacity",
        ReviewCapacity = "Review Capacity", ReviewGate = "Review Gate", SubmissionGate = "Submission Gate";
    public static readonly string[] All = [Handoff, Predecessor, Decision, Basis, ProductionOwner,
        ProductionCapacity, ReviewCapacity, ReviewGate, SubmissionGate];
}

/// <summary>Applicability and result are distinct: unknown is never treated as false or satisfied.</summary>
public sealed record ReadinessCheck(string Code, bool? Applies, bool? Satisfied);

public sealed record ReadinessPermission(bool Approved, DateOnly ExpiresOn, bool SameBasisVersion,
    bool HasVerifier, string LimitedWork, string Risk);

public sealed record ReadinessResult(string State, string[] Unknown, string[] Blocked);

public static class ReadinessRules
{
    public static ReadinessResult Evaluate(IEnumerable<ReadinessCheck> inputs, ReadinessPermission? permission, DateOnly today)
    {
        var checks = inputs.ToArray();
        if (checks.Length != ReadinessCheckCode.All.Length ||
            checks.Select(x => x.Code).Distinct().Count() != checks.Length ||
            checks.Any(x => !ReadinessCheckCode.All.Contains(x.Code)))
            throw new ArgumentException("Readiness requires each unique canonical check.", nameof(inputs));
        var unknown = checks.Where(x => x.Applies is null || x.Applies == true && x.Satisfied is null)
            .Select(x => x.Code).ToArray();
        var blocked = checks.Where(x => x.Applies == true && x.Satisfied == false).Select(x => x.Code).ToArray();
        if (unknown.Length > 0 || blocked.Contains(ReadinessCheckCode.Basis) && permission is { Approved: true } &&
            (permission.ExpiresOn < today || !permission.SameBasisVersion || !permission.HasVerifier))
            return new(ReadinessState.NeedsAssessment, unknown, blocked);
        if (blocked.Length == 0) return new(ReadinessState.Ready, [], []);
        var narrow = blocked.Length == 1 && blocked[0] == ReadinessCheckCode.Basis &&
            permission is { Approved: true, SameBasisVersion: true, HasVerifier: true } &&
            permission.ExpiresOn >= today && !string.IsNullOrWhiteSpace(permission.LimitedWork) &&
            !string.IsNullOrWhiteSpace(permission.Risk);
        return new(narrow ? ReadinessState.ProceedUnderAssumption : ReadinessState.NotReady, [], blocked);
    }
}

public static class CommitmentState
{
    public const string Proposed = "Proposed", Committed = "Committed", Met = "Met", NotMet = "Not Met", Withdrawn = "Withdrawn";
    public static readonly string[] All = [Proposed, Committed, Met, NotMet, Withdrawn];
}

public sealed record WeeklyOutcome(int Met, int SnapshotCommitted, int Withdrawn);

public static class WeeklyCommitmentRules
{
    public static bool MayCommit(Guid actor, Guid performer) => actor != Guid.Empty && actor == performer;
    public static bool MayClose(string from, string to, bool hasEvidence, bool hasReason) =>
        from == CommitmentState.Committed && (to == CommitmentState.Met && hasEvidence ||
            to == CommitmentState.NotMet && hasReason || to == CommitmentState.Withdrawn && hasReason);

    /// <summary>The original committed IDs fix the denominator even after later withdrawal.</summary>
    public static WeeklyOutcome Outcome(IEnumerable<Guid> committedAtSnapshot, IReadOnlyDictionary<Guid, string> current)
    {
        var ids = committedAtSnapshot.ToArray();
        if (ids.Length != ids.Distinct().Count() || ids.Any(id => id == Guid.Empty || !current.ContainsKey(id)))
            throw new ArgumentException("Snapshot references must be unique and present.", nameof(committedAtSnapshot));
        var values = ids.Select(id => current[id]).ToArray();
        if (values.Any(status => !CommitmentState.All.Contains(status)))
            throw new ArgumentException("Unknown commitment status.", nameof(current));
        return new(values.Count(status => status == CommitmentState.Met), ids.Length,
            values.Count(status => status == CommitmentState.Withdrawn));
    }
}
