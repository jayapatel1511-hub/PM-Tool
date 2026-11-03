using System.Text.Json.Serialization;

namespace Hub.Api.Features;

public readonly record struct WorkSeenKey(string ItemType, Guid ItemId);
public sealed record WorkSeenRoleRecipient(string Role, Guid RecipientId);
public sealed record WorkSeenReadBound(WorkSeenKey Key, Guid ProjectId, int SourceVersion, IReadOnlyList<WorkSeenRoleRecipient> Roles);
public sealed record WorkSeenObservedToken(Guid Token, string Role, int SourceVersion);
public sealed record WorkSeenContext(Guid SubjectId, DateOnly Today, DateTimeOffset Now);
public sealed record WorkSeenDecoration(string State, IReadOnlyList<WorkSeenObservedToken> ObservedTokens);
public sealed record WorkActivityFact(Guid? EventId, DateTimeOffset? OccurredAt, Guid? ActorId, string? ActorName, string? ActorType,
    bool Commentable, int? CommentCount);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkSeenVisitBody(Guid RequestId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkSeenOpenedBody(Guid RequestId, string ItemType, Guid ItemId, Guid[] ObservedTokens);
public sealed record WorkSeenCommandResult(Guid RequestId, DateTimeOffset CommittedAt, long Revision,
    DateTimeOffset? FirstVisitAt, DateTimeOffset? PreviousVisitAt, DateTimeOffset? LastVisitAt, DateTimeOffset? LastMarkAllAt);
