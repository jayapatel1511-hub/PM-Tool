namespace Hub.Api.Data;

/// Private captured notice occurrence; never a business record, audit row or public response.
public sealed class NoticeDeliveryIntent : Entity
{
    public Guid UserId { get; set; }
    public string OccurrenceKey { get; set; } = "";
    public string EventType { get; set; } = "";
    public string? ItemType { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemKey { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? ActorUserId { get; set; }
    public bool IsDirectRecipient { get; set; }
    public Guid[] RequiredProjectIds { get; set; } = [];
    public Guid[] RequiredPlanningEntryIds { get; set; } = [];
    public Guid? NotificationId { get; set; }
    public int? NotificationCountAtCapture { get; set; }
    public string OriginalSubject { get; set; } = "";
    public string OriginalBodyText { get; set; } = "";
    public string? OriginalLinkPath { get; set; }
    public string SuppressionKey { get; set; } = "";
    public DateTimeOffset? SuppressionAnchorAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string Disposition { get; set; } = "Pending";
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? DeliveryEmailId { get; set; }
    public string? BulkPolicyJson { get; set; }
    public DateTimeOffset? NextPolicyCheckAt { get; set; }
    public DateOnly? PolicyCheckLocalDate { get; set; }
    public int? PolicyChecksInDay { get; set; }
}

/// Private scheduled digest or own in-app reminder; claims belong only to EmailMessage.
public sealed class NoticeDeliverySlot : Entity
{
    public Guid UserId { get; set; }
    public string Kind { get; set; } = "";
    public DateOnly LocalDate { get; set; }
    public string OrgTimeZoneId { get; set; } = "";
    public DateOnly ScheduledLocalDate { get; set; }
    public string ScheduledLocalTime { get; set; } = "";
    public DateTimeOffset ScheduledAtUtc { get; set; }
    public string? FrequencyAtCreation { get; set; }
    public DateTimeOffset? WindowFrom { get; set; }
    public DateTimeOffset? WindowThrough { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string State { get; set; } = "Ready";
    public int CompositionVersion { get; set; }
    public int ExternalAttempts { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? ReminderNotificationId { get; set; }
    public Guid? CarryForwardToSlotId { get; set; }
}
