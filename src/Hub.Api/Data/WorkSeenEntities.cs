namespace Hub.Api.Data;

/// Private append-only assignment evidence. Token order and RecordedAt never determine assignment order.
public sealed class WorkAssignmentOccurrence
{
    public Guid Token { get; set; } = Guid.CreateVersion7();
    public Guid RecipientId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public Guid ProjectId { get; set; }
    public string Role { get; set; } = "";
    public int SourceVersion { get; set; }
    public Guid? SourceActivityId { get; set; }
    public DateTimeOffset? SourceOccurredAt { get; set; }
    public string Provenance { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}

/// Private insert-only receipt for one recipient's exact assignment token, never a moving item cursor.
public sealed class WorkSeenAcknowledgement
{
    public Guid RecipientId { get; set; }
    public Guid Token { get; set; }
    public DateTimeOffset AcknowledgedAt { get; set; }
    public string Provenance { get; set; } = "";
    public Guid? RequestId { get; set; }
}

/// Committed atomically with personal state and acknowledgements; not subject to generic POST expiry.
public sealed class WorkSeenCommandReceipt
{
    public Guid UserId { get; set; }
    public Guid RequestId { get; set; }
    public string Action { get; set; } = "";
    public string PayloadHash { get; set; } = "";
    public string ResponseJson { get; set; } = "{}";
    public DateTimeOffset CommittedAt { get; set; }
}
