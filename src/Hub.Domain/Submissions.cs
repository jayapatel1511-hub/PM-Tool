namespace Hub.Domain;

public static class SubmissionStatus
{
    public const string Draft = "Draft", Checking = "Checking", Ready = "Ready", Issued = "Issued", Superseded = "Superseded", Cancelled = "Cancelled";
    public static readonly string[] All = [Draft, Checking, Ready, Issued, Superseded, Cancelled];
}

public static class SubmissionCheckStatus
{
    public const string Pending = "Pending", Pass = "Pass", NotApplicable = "Not Applicable";
    public static readonly string[] All = [Pending, Pass, NotApplicable];
}

public static class SubmissionCheckKind
{
    public const string Deliverable = "Deliverable", CurrentRevision = "Current Revision", IndependentReview = "Independent Review", BlockingFindings = "Blocking Findings", Handoff = "Handoff", ChangeAssessment = "Change Assessment", Access = "Access", Applicability = "Applicability";
    public static readonly string[] All = [Deliverable, CurrentRevision, IndependentReview, BlockingFindings, Handoff, ChangeAssessment, Access, Applicability];
    public static bool Waivable(string kind) => kind == Applicability;
}

public static class SubmissionRules
{
    public static bool Ready(IEnumerable<(bool Required, string Status)> checks)
    {
        var rows = checks.ToArray();
        return rows.Length > 0 && rows.All(c => !c.Required || c.Status == SubmissionCheckStatus.Pass);
    }
    public static bool Step(string from, string to) => (from, to) switch {
        (SubmissionStatus.Draft, SubmissionStatus.Checking) => true,
        (SubmissionStatus.Checking, SubmissionStatus.Ready) => true,
        (SubmissionStatus.Ready, SubmissionStatus.Checking) => true,
        (SubmissionStatus.Checking or SubmissionStatus.Ready, SubmissionStatus.Issued) => true,
        (SubmissionStatus.Issued, SubmissionStatus.Superseded) => true,
        (SubmissionStatus.Draft or SubmissionStatus.Checking or SubmissionStatus.Ready, SubmissionStatus.Cancelled) => true,
        _ => false };
    public static bool CanIssue(string status, bool ready) => (status is SubmissionStatus.Checking or SubmissionStatus.Ready) && ready;
}
