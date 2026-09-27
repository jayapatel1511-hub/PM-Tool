namespace Hub.Domain;

public static class ChangeStatus
{
    public const string Draft = "Draft", Open = "Open", Closed = "Closed", Cancelled = "Cancelled";
    public static readonly string[] All = [Draft, Open, Closed, Cancelled];
}
public static class AssessmentStatus
{
    public const string Pending = "Pending Assessment", Unaffected = "Unaffected", UpdateRequired = "Update Required", Clarification = "Clarification Needed", Resolved = "Resolved";
    public static readonly string[] All = [Pending, Unaffected, UpdateRequired, Clarification, Resolved];
}
public static class ChangeRules
{
    public static bool Complete(string status, bool evidence, bool retainingOld, bool retentionApproved) =>
        status is AssessmentStatus.Unaffected or AssessmentStatus.Resolved && evidence && (!retainingOld || retentionApproved);
    public static bool CanClose(IEnumerable<bool> assessments) => assessments.All(x => x);
    public static bool CorrectionReady(string? taskStatus, bool independent, bool evidence) => taskStatus == TaskStatuses.Complete && independent && evidence;
}
