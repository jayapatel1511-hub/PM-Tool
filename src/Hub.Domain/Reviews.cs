namespace Hub.Domain;

public static class ReviewStatus
{
    public const string Draft = "Draft", InReview = "In Review", ChangesRequired = "Changes Required", Approved = "Approved", Superseded = "Superseded", Cancelled = "Cancelled";
    public static readonly string[] All = [Draft, InReview, ChangesRequired, Approved, Superseded, Cancelled];
}
public static class DisciplineReviewStatus
{
    public const string Pending = "Pending", InReview = "In Review", ChangesRequired = "Changes Required", Approved = "Approved";
    public static readonly string[] All = [Pending, InReview, ChangesRequired, Approved];
}
public static class FindingStatus
{
    public const string Open = "Open", Responded = "Responded", VerifiedClosed = "Verified Closed", Withdrawn = "Withdrawn";
    public static readonly string[] All = [Open, Responded, VerifiedClosed, Withdrawn];
}
public static class ReviewRules
{
    public static bool Independent(Guid reviewer, IEnumerable<Guid> authors, bool allowSelfReview) => allowSelfReview || !authors.Contains(reviewer);
    public static bool FindingStep(string from, string to) => (from, to) switch {
        (FindingStatus.Open, FindingStatus.Responded or FindingStatus.Withdrawn) => true,
        (FindingStatus.Responded, FindingStatus.Open or FindingStatus.VerifiedClosed or FindingStatus.Withdrawn) => true,
        _ => false };
    public static bool BlockingOpen(string severity, string status, bool withdrawalAcknowledged) => severity == "Blocking"
        && status != FindingStatus.VerifiedClosed && (status != FindingStatus.Withdrawn || !withdrawalAcknowledged);
    public static string PackageStatus(IEnumerable<string> assignments, bool blockingOpen)
    {
        var states = assignments.ToArray();
        if (states.Length > 0 && states.All(s => s == DisciplineReviewStatus.Approved) && !blockingOpen) return ReviewStatus.Approved;
        return blockingOpen || states.Contains(DisciplineReviewStatus.ChangesRequired) ? ReviewStatus.ChangesRequired : ReviewStatus.InReview;
    }
}
