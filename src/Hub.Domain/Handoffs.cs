namespace Hub.Domain;

/// Packet 025, §10.8 / FR-HND-02..06. Receipt, acceptance and incorporation are separate evidence.
public static class HandoffStatus
{
    public const string Draft = "Draft", Submitted = "Submitted", ClarificationRequested = "Clarification Requested",
        Returned = "Returned", Accepted = "Accepted", Incorporated = "Incorporated", Cancelled = "Cancelled";
    public static readonly string[] All = [Draft, Submitted, ClarificationRequested, Returned, Accepted, Incorporated, Cancelled];
}

public sealed record HandoffFacts(Guid SendingDisciplineId, Guid ReceivingDisciplineId, Guid SendingOwnerId,
    Guid ReceivingOwnerId, string Status, Guid? SubmittedBy = null);

public static class HandoffRules
{
    public static bool Step(string from, string to) => (from, to) switch
    {
        (HandoffStatus.Draft, HandoffStatus.Submitted) => true,
        (HandoffStatus.Submitted, HandoffStatus.Accepted or HandoffStatus.ClarificationRequested or HandoffStatus.Returned) => true,
        (HandoffStatus.ClarificationRequested or HandoffStatus.Returned, HandoffStatus.Submitted) => true,
        (HandoffStatus.Accepted, HandoffStatus.Incorporated) => true,
        (_, HandoffStatus.Cancelled) => AllOpen.Contains(from),
        _ => false,
    };
    static readonly string[] AllOpen = [HandoffStatus.Draft, HandoffStatus.Submitted, HandoffStatus.ClarificationRequested, HandoffStatus.Returned, HandoffStatus.Accepted];
    public static bool Editable(string status) => status is HandoffStatus.Draft or HandoffStatus.ClarificationRequested or HandoffStatus.Returned;
    public static bool NeedsReason(string from, string to) => to is HandoffStatus.Returned or HandoffStatus.ClarificationRequested or HandoffStatus.Cancelled
        || (to == HandoffStatus.Submitted && from != HandoffStatus.Draft);
    public static bool DateMismatch(DateOnly needed, DateOnly? promised) => promised > needed;
    public static bool Overdue(string status, DateOnly needed, DateOnly today, string projectStatus) =>
        projectStatus != ProjectStatus.OnHold && needed < today && status is not (HandoffStatus.Accepted or HandoffStatus.Incorporated or HandoffStatus.Cancelled);
    public static bool SelfReceipt(HandoffFacts h) => h.SendingOwnerId == h.ReceivingOwnerId || h.SubmittedBy == h.ReceivingOwnerId;
}
