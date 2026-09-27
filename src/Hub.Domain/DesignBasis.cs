namespace Hub.Domain;

public static class BasisKind
{
    public const string Criterion = "Criterion", Assumption = "Assumption";
    public static readonly string[] All = [Criterion, Assumption];
}

public static class BasisStatus
{
    public const string Proposed = "Proposed", Confirmed = "Confirmed", Superseded = "Superseded", Withdrawn = "Withdrawn";
    public static readonly string[] All = [Proposed, Confirmed, Superseded, Withdrawn];
}

public static class BasisRules
{
    public static bool MayConfirm(string status, decimal? numericValue, string? units, bool hasSourceEvidence,
        bool hasRationale, bool independent) => status == BasisStatus.Proposed &&
        (numericValue is null || !string.IsNullOrWhiteSpace(units)) && hasSourceEvidence && hasRationale && independent;

    public static bool MayWithdraw(string status) => status is BasisStatus.Proposed or BasisStatus.Confirmed;

    public static bool NeedsAssessment(string usedVersionStatus) => usedVersionStatus is BasisStatus.Superseded or BasisStatus.Withdrawn;

    public static bool AssumptionReady(string status, bool proceedApproved, DateOnly? expiry, DateOnly today) =>
        status == BasisStatus.Confirmed || (status == BasisStatus.Proposed && proceedApproved && expiry >= today);

    public static bool ValuesConflict(string leftKind, string rightKind, string leftStatus, string rightStatus,
        Guid leftDiscipline, Guid rightDiscipline, string leftTitle, string rightTitle, string leftScope, string rightScope,
        string leftValue, string rightValue) =>
        leftKind == rightKind && leftStatus == BasisStatus.Confirmed && rightStatus == BasisStatus.Confirmed &&
        leftDiscipline == rightDiscipline && string.Equals(leftTitle.Trim(), rightTitle.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(leftScope.Trim(), rightScope.Trim(), StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(leftValue.Trim(), rightValue.Trim(), StringComparison.OrdinalIgnoreCase);
}
