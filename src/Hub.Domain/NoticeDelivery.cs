using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Hub.Domain;

// Private delivery vocabulary. These types are not business records, permissions or transport receipts.
public static class NoticeDigestFrequency
{
    public const string Daily = "Daily", Weekly = "Weekly";
    public static readonly IReadOnlySet<string> All = new[] { Daily, Weekly }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeSlotKind
{
    public const string Digest = "Digest", Reminder = "TimeReminder";
    public static readonly IReadOnlySet<string> All = new[] { Digest, Reminder }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeIntentDisposition
{
    public const string Pending = "Pending", PendingAvailability = "PendingAvailability", HeldAway = "HeldAway",
        HeldNoDigest = "HeldNoDigest", Queued = "Queued", ReservedForDigest = "ReservedForDigest", Accepted = "Accepted",
        Simulated = "Simulated", Unknown = "Unknown", Uncertain = "Uncertain", Exhausted = "Exhausted", ChannelOff = "ChannelOff",
        NoAddress = "NoAddress", Muted = "Muted", AccessRemoved = "AccessRemoved", RecipientInactive = "RecipientInactive",
        Duplicate = "Duplicate", Mixed = "Mixed";
    public static readonly IReadOnlySet<string> All = new[] { Pending, PendingAvailability, HeldAway, HeldNoDigest, Queued,
        ReservedForDigest, Accepted, Simulated, Unknown, Uncertain, Exhausted, ChannelOff, NoAddress, Muted, AccessRemoved,
        RecipientInactive, Duplicate, Mixed }.ToFrozenSet(StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> ObligationStates = All.Where(value => value != Mixed).ToFrozenSet(StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> Refusals = new[] { ChannelOff, NoAddress, Muted, AccessRemoved, RecipientInactive, Duplicate }.ToFrozenSet(StringComparer.Ordinal);
    public static readonly IReadOnlySet<string> Terminal = Refusals.Concat([Accepted, Simulated, Unknown, Exhausted]).ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeSlotState
{
    public const string Ready = "Ready", Queued = "Queued", Paused = "Paused", Accepted = "Accepted", Simulated = "Simulated",
        Unknown = "Unknown", Uncertain = "Uncertain", Exhausted = "Exhausted", Empty = "Empty", Emitted = "Emitted";
    public static readonly IReadOnlySet<string> All = new[] { Ready, Queued, Paused, Accepted, Simulated, Unknown, Uncertain, Exhausted, Empty, Emitted }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeTransportKind
{
    public const string Unknown = "Unknown", Log = "Log", Smtp = "Smtp", Graph = "Graph";
    public static readonly IReadOnlySet<string> All = new[] { Unknown, Log, Smtp, Graph }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeTransportOutcome
{
    public const string Queued = "Queued", InFlight = "InFlight", Accepted = "Accepted", Simulated = "Simulated", Unknown = "Unknown",
        Failed = "Failed", Uncertain = "Uncertain", Exhausted = "Exhausted", SuppressedBeforeSend = "SuppressedBeforeSend";
    public static readonly IReadOnlySet<string> All = new[] { Queued, InFlight, Accepted, Simulated, Unknown, Failed, Uncertain, Exhausted, SuppressedBeforeSend }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeClaimDecision
{
    public const string Legacy = "Legacy", Send = "Send", Dormant = "Dormant", HoldAway = "HoldAway", HoldNoDigest = "HoldNoDigest",
        PendingAvailability = "PendingAvailability", Suppress = "Suppress", Recompose = "Recompose", InFlight = "InFlight",
        Uncertain = "Uncertain", Exhausted = "Exhausted", AlreadyTerminal = "AlreadyTerminal";
    public static readonly IReadOnlySet<string> All = new[] { Legacy, Send, Dormant, HoldAway, HoldNoDigest, PendingAvailability,
        Suppress, Recompose, InFlight, Uncertain, Exhausted, AlreadyTerminal }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeFailureCertainty
{
    public const string None = "None", DefinitelyNotAccepted = "DefinitelyNotAccepted", MayHaveAccepted = "MayHaveAccepted";
    public static readonly IReadOnlySet<string> All = new[] { None, DefinitelyNotAccepted, MayHaveAccepted }.ToFrozenSet(StringComparer.Ordinal);
}

public static class NoticeRefusalReason
{
    public const string ChannelOff = "ChannelOff", NoAddress = "NoAddress", Muted = "Muted", AccessRemoved = "AccessRemoved",
        RecipientInactive = "RecipientInactive", Duplicate = "Duplicate", PlanningAssignmentWithdrawn = "PlanningAssignmentWithdrawn",
        ProjectAccessRemoved = "ProjectAccessRemoved", PlanningAccessRemoved = "PlanningAccessRemoved", AttemptsExhausted = "AttemptsExhausted",
        AddressChanged = "AddressChanged", TransportChanged = "TransportChanged";
    public static readonly IReadOnlySet<string> All = new[] { ChannelOff, NoAddress, Muted, AccessRemoved, RecipientInactive, Duplicate,
        PlanningAssignmentWithdrawn, ProjectAccessRemoved, PlanningAccessRemoved, AttemptsExhausted, AddressChanged, TransportChanged }.ToFrozenSet(StringComparer.Ordinal);
    public static bool Matches(string state, string? reason) => reason is null
        ? !NoticeIntentDisposition.Refusals.Contains(state)
        : All.Contains(reason) && (state == reason || state == NoticeIntentDisposition.AccessRemoved
            && reason is PlanningAssignmentWithdrawn or ProjectAccessRemoved or PlanningAccessRemoved or AddressChanged or TransportChanged
            || state == NoticeIntentDisposition.Exhausted && reason == AttemptsExhausted);
}

public static class NoticeBulkCoverage
{
    public const string Task = "Task", ChangedTasksInSourceProject = "ChangedTasksInSourceProject";
}

public static class NoticeProofLimits
{
    public const int SchemaVersion = 1, MaxChangedTasks = 200, MaxObligations = 4096, MaxRenderedIntents = 10;
    public const int OriginBytes = 2 * 1024 * 1024, ProgressAllowanceBytes = 1024 * 1024, EnvelopeBytes = 64;
    public const int MaxCanonicalBytes = OriginBytes + ProgressAllowanceBytes + EnvelopeBytes, RenderedBytes = 1024 * 1024;
    public const int ProgressRecordReservationBytes = 200, MaxStateBytes = 32, MaxReasonBytes = 64;
    public const int MaxCompositions = 3, MaxSenderReservations = 8, MaxPolicyChecksPerDay = 8;
    public const string Kind = "TaskBulkSummary", UtcFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
}

public sealed record NoticeChangedTask(Guid TaskId, Guid ProjectId);

// Compact closed private encoding keeps a full 4096-obligation Origin representable inside its independent cap.
public sealed record NoticeBulkObligation([property: JsonPropertyName("k")] int Key,
    [property: JsonPropertyName("ev")] string EventType, [property: JsonPropertyName("it")] string SourceItemType,
    [property: JsonPropertyName("id")] Guid SourceItemId, [property: JsonPropertyName("p")] Guid SourceProjectId,
    [property: JsonPropertyName("ps")] ImmutableArray<Guid> RequiredProjectIds,
    [property: JsonPropertyName("ns")] ImmutableArray<Guid> RequiredPlanningEntryIds,
    [property: JsonPropertyName("a")] Guid? SourceActorUserId, [property: JsonPropertyName("d")] bool IsDirectRecipient,
    [property: JsonPropertyName("app")] bool AppEligible, [property: JsonPropertyName("mail")] bool EmailEligible,
    [property: JsonPropertyName("x")] string? EmailExclusionReason, [property: JsonPropertyName("sk")] string SuppressionKey,
    [property: JsonPropertyName("sa")] DateTimeOffset? SuppressionAnchorAt, [property: JsonPropertyName("c")] string Coverage);

public sealed record NoticeBulkOrigin(int SchemaVersion, string Kind, Guid CommandId, Guid OriginalActorId, Guid UserId,
    DateTimeOffset CapturedAt, ImmutableArray<NoticeChangedTask> ChangedTasks, ImmutableArray<NoticeBulkObligation> Obligations,
    ImmutableArray<int> AppCohortKeys, ImmutableArray<int> EmailCohortKeys, Guid? AppNotificationId, int? NotificationCountAtCapture,
    ImmutableArray<Guid> AppTaskIds, ImmutableArray<Guid> EmailTaskIds, string OriginHash = "");

public sealed record NoticeBulkProgress(int Key, string State, DateTimeOffset UpdatedAt, string? RefusalReason = null, Guid? EmailId = null);
public sealed record NoticeBulkProof(NoticeBulkOrigin Origin, ImmutableArray<NoticeBulkProgress> Progress);
public sealed record NoticeBulkRenderedMembership(Guid IntentId, Guid UserId, Guid CommandId, string OriginHash, ImmutableArray<int> ObligationKeys);
