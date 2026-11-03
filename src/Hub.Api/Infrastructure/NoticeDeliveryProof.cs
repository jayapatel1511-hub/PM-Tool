using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Domain;

namespace Hub.Api.Infrastructure;

public sealed class NoticeProofException(string code) : InvalidOperationException(code)
{
    public string Code { get; } = code;
}

/// Private, closed v1 proof codec. It grants no access and observes no transport; callers supply trusted source facts.
public static class NoticeDeliveryProof
{
    static readonly JsonSerializerOptions OriginReadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true, MaxDepth = 16,
    };
    static readonly HashSet<string> SourceKinds = [ItemType.Project, ItemType.Milestone, ItemType.Deliverable, ItemType.Task,
        ItemType.Decision, ItemType.Risk, ItemType.Issue, ItemType.Meeting, ItemType.Action, ItemType.Comment, ItemType.DocumentLink,
        ItemType.Dependency, ItemType.Member, ItemType.Discipline, ItemType.ExternalParty, ItemType.User, ItemType.ReferenceData,
        ItemType.Setting, ItemType.TimeEntry, ItemType.CalendarEvent, ItemType.Template, ItemType.Handoff, ItemType.ChangeNotice,
        ItemType.Report, ItemType.Snooze, ItemType.ProjectLink, ItemType.Holiday];

    public static int CompleteProgressReservedBytes(int count)
    {
        Require(count is > 0 and <= NoticeProofLimits.MaxObligations, "InvalidObligationCount");
        return checked(count * NoticeProofLimits.ProgressRecordReservationBytes + 2);
    }

    public static NoticeBulkProof Admit(NoticeBulkOrigin source)
    {
        var origin = PrepareOrigin(source, requireHash: false);
        Require(CompleteProgressReservedBytes(origin.Obligations.Length) <= NoticeProofLimits.ProgressAllowanceBytes, "ProgressReservationTooLarge");
        var progress = origin.Obligations.Select(value => new NoticeBulkProgress(value.Key,
            value.EmailEligible ? NoticeIntentDisposition.Pending : ExcludedState(value.EmailExclusionReason!),
            origin.CapturedAt, value.EmailExclusionReason)).ToImmutableArray();
        var proof = new NoticeBulkProof(origin, progress);
        ValidateProgress(proof);
        return proof;
    }

    public static NoticeBulkProof Decode(string json, Guid expectedUserId, Guid expectedCommandId)
    {
        Require(expectedUserId != Guid.Empty && expectedCommandId != Guid.Empty, "InvalidProofBinding");
        return DecodeCore(json, expectedUserId, expectedCommandId);
    }

    public static bool HasSameImmutableOrigin(string originalJson, string currentJson, Guid expectedUserId)
    {
        try
        {
            Require(expectedUserId != Guid.Empty, "InvalidProofBinding");
            var before = DecodeCore(originalJson, expectedUserId, null);
            var after = DecodeCore(currentJson, expectedUserId, before.Origin.CommandId);
            if (!WriteOrigin(before.Origin, true).AsSpan().SequenceEqual(WriteOrigin(after.Origin, true))) return false;
            ValidateTransitions(before, after.Progress);
            return true;
        }
        catch (NoticeProofException) { return false; }
    }

    public static string Encode(NoticeBulkProof proof)
    {
        Require(proof is not null, "InvalidProof");
        var origin = PrepareOrigin(proof.Origin, requireHash: true);
        proof = proof with { Origin = origin, Progress = OrderedProgress(proof.Progress) };
        ValidateProgress(proof);
        // Capture reserved every permitted future record. Never admit on today's smaller combined byte count.
        using var stream = new LimitedBuffer(NoticeProofLimits.MaxCanonicalBytes, "InvalidProofReservation");
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WritePropertyName("origin");
            writer.WriteRawValue(WriteOrigin(origin, true), skipInputValidation: false);
            writer.WritePropertyName("progress"); writer.WriteStartArray();
            foreach (var value in proof.Progress)
            {
                writer.WriteStartObject(); writer.WriteNumber("k", value.Key); writer.WriteString("s", value.State);
                writer.WriteString("t", Utc(value.UpdatedAt)); NullableString(writer, "r", value.RefusalReason);
                NullableGuid(writer, "e", value.EmailId); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.WriteEndObject(); writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static NoticeBulkProof WithProgress(NoticeBulkProof proof, ImmutableArray<NoticeBulkProgress> progress)
    {
        Require(proof is not null, "InvalidProof");
        // Validate original admission/hash as well as progress; a caller-created record is not admission evidence.
        var before = proof with { Origin = PrepareOrigin(proof.Origin, true), Progress = OrderedProgress(proof.Progress) };
        ValidateProgress(before);
        progress = OrderedProgress(progress);
        var after = before with { Progress = progress };
        ValidateProgress(after); ValidateTransitions(before, progress);
        return after;
    }

    public static NoticeBulkRenderedMembership Render(Guid intentId, NoticeBulkProof proof, ImmutableArray<int> keys)
    {
        Require(intentId != Guid.Empty, "InvalidRenderedBinding");
        var admitted = Decode(Encode(proof), proof.Origin.UserId, proof.Origin.CommandId);
        Require(!keys.IsDefaultOrEmpty && keys.Distinct().Count() == keys.Length
            && keys.All(admitted.Origin.EmailCohortKeys.Contains), "InvalidRenderedKeys");
        return new(intentId, admitted.Origin.UserId, admitted.Origin.CommandId, admitted.Origin.OriginHash, keys.Order().ToImmutableArray());
    }

    public static void ValidateRendered(NoticeBulkProof proof, NoticeBulkRenderedMembership rendered, Guid expectedIntentId)
    {
        Require(rendered is not null && expectedIntentId != Guid.Empty && rendered.IntentId == expectedIntentId
            && rendered.UserId == proof.Origin.UserId && rendered.CommandId == proof.Origin.CommandId
            && rendered.OriginHash == proof.Origin.OriginHash, "InvalidRenderedBinding");
        Render(expectedIntentId, proof, rendered.ObligationKeys);
    }

    /// Pure cohort inclusion only. Real acceptance/recipient/app visibility must be established by the caller.
    public static bool IncludesWholeAppCohort(NoticeBulkProof proof, ImmutableArray<int> provenKeys)
    {
        Require(proof is not null, "InvalidProof");
        proof = proof with { Origin = PrepareOrigin(proof.Origin, true), Progress = OrderedProgress(proof.Progress) };
        ValidateProgress(proof);
        Require(!provenKeys.IsDefault && provenKeys.Distinct().Count() == provenKeys.Length
            && provenKeys.All(proof.Origin.EmailCohortKeys.Contains), "InvalidRenderedKeys");
        return !proof.Origin.AppCohortKeys.IsDefaultOrEmpty && proof.Origin.AppCohortKeys.All(provenKeys.Contains);
    }

    static NoticeBulkProof DecodeCore(string json, Guid expectedUserId, Guid? expectedCommandId)
    {
        try
        {
            // jsonb read formatting can add spaces. This parsing ceiling is separate from canonical admission budgets.
            Require(json is not null && Encoding.UTF8.GetByteCount(json) <= 8 * 1024 * 1024, "InvalidProofEncoding");
            using var document = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 16 });
            UniqueProperties(document.RootElement);
            var root = document.RootElement;
            Fields(root, "origin", "progress");
            var origin = root.GetProperty("origin").Deserialize<NoticeBulkOrigin>(OriginReadOptions);
            Require(origin is not null, "InvalidOrigin");
            origin = PrepareOrigin(origin!, true);
            Require(origin.UserId == expectedUserId && (expectedCommandId is null || origin.CommandId == expectedCommandId), "ForeignProof");
            var values = root.GetProperty("progress");
            Require(values.ValueKind == JsonValueKind.Array && values.GetArrayLength() == origin.Obligations.Length, "InvalidProgressKeys");
            var progress = ImmutableArray.CreateBuilder<NoticeBulkProgress>(values.GetArrayLength());
            foreach (var value in values.EnumerateArray())
            {
                Fields(value, "k", "s", "t", "r", "e");
                Require(DateTimeOffset.TryParseExact(value.GetProperty("t").GetString(), NoticeProofLimits.UtcFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var time), "InvalidProgressTime");
                var reason = value.GetProperty("r"); var email = value.GetProperty("e");
                progress.Add(new(value.GetProperty("k").GetInt32(), value.GetProperty("s").GetString()!, time,
                    reason.ValueKind == JsonValueKind.Null ? null : reason.GetString(),
                    email.ValueKind == JsonValueKind.Null ? null : email.GetGuid()));
            }
            var result = new NoticeBulkProof(origin, OrderedProgress(progress.ToImmutable()));
            ValidateProgress(result);
            return result;
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        {
            if (error is NoticeProofException) throw;
            throw new NoticeProofException("InvalidProofEncoding"); // No proof/content/identity text escapes via diagnostics.
        }
    }

    static NoticeBulkOrigin PrepareOrigin(NoticeBulkOrigin source, bool requireHash)
    {
        ValidateOrigin(source);
        var normalized = source with
        {
            ChangedTasks = source.ChangedTasks.OrderBy(value => Id(value.TaskId), StringComparer.Ordinal).ToImmutableArray(),
            Obligations = source.Obligations.OrderBy(value => value.Key).Select(value => value with
            {
                RequiredProjectIds = OrderIds(value.RequiredProjectIds), RequiredPlanningEntryIds = OrderIds(value.RequiredPlanningEntryIds),
            }).ToImmutableArray(),
            AppCohortKeys = source.AppCohortKeys.Order().ToImmutableArray(), EmailCohortKeys = source.EmailCohortKeys.Order().ToImmutableArray(),
            AppTaskIds = OrderIds(source.AppTaskIds), EmailTaskIds = OrderIds(source.EmailTaskIds),
        };
        var hash = Convert.ToHexString(SHA256.HashData(WriteOrigin(normalized, false))).ToLowerInvariant();
        Require(!requireHash && source.OriginHash.Length == 0 || source.OriginHash == hash, "InvalidOriginHash");
        normalized = normalized with { OriginHash = hash };
        _ = WriteOrigin(normalized, true); // Independent immutable 2 MiB cap, including the hash member.
        return normalized;
    }

    static void ValidateOrigin(NoticeBulkOrigin source)
    {
        Require(source is not null && source.SchemaVersion == NoticeProofLimits.SchemaVersion && source.Kind == NoticeProofLimits.Kind, "InvalidOriginVersion");
        Require(source.CommandId != Guid.Empty && source.OriginalActorId != Guid.Empty && source.UserId != Guid.Empty
            && source.CapturedAt.Offset == TimeSpan.Zero && source.OriginHash is not null
            && (source.OriginHash.Length == 0 || source.OriginHash.Length == 64 && source.OriginHash.All(char.IsAsciiHexDigitLower)), "InvalidOriginIdentity");
        Require(!source.ChangedTasks.IsDefaultOrEmpty && source.ChangedTasks.Length <= NoticeProofLimits.MaxChangedTasks
            && source.ChangedTasks.All(value => value is not null && value.TaskId != Guid.Empty && value.ProjectId != Guid.Empty)
            && source.ChangedTasks.Select(value => value.TaskId).Distinct().Count() == source.ChangedTasks.Length, "InvalidChangedTasks");
        Require(!source.Obligations.IsDefaultOrEmpty && source.Obligations.Length <= NoticeProofLimits.MaxObligations
            && source.Obligations.All(value => value is not null)
            && source.Obligations.Select(value => value.Key).Order().SequenceEqual(Enumerable.Range(1, source.Obligations.Length)), "InvalidObligationKeys");
        var projects = source.ChangedTasks.Select(value => value.ProjectId).ToHashSet();
        foreach (var value in source.Obligations)
        {
            var definition = NotificationEvents.All.FirstOrDefault(known => known.Code == value.EventType);
            Require(definition is not null && SourceKinds.Contains(value.SourceItemType) && value.SourceItemId != Guid.Empty
                && projects.Contains(value.SourceProjectId) && value.SourceActorUserId != source.UserId, "InvalidObligationSource");
            Require(!value.IsDirectRecipient || definition!.DirectAssignment, "InvalidDirectRouting");
            GuidSet(value.RequiredProjectIds, NoticeProofLimits.MaxChangedTasks, "InvalidProjectScope");
            Require(value.RequiredProjectIds.Contains(value.SourceProjectId) && value.RequiredProjectIds.All(projects.Contains), "InvalidProjectScope");
            Require(!value.RequiredPlanningEntryIds.IsDefault && value.RequiredPlanningEntryIds.Length == 0, "InvalidPlanningScope");
            Require(value.SourceItemType == ItemType.Task
                ? value.Coverage == NoticeBulkCoverage.Task && source.ChangedTasks.Any(task => task.TaskId == value.SourceItemId && task.ProjectId == value.SourceProjectId)
                : value.Coverage == NoticeBulkCoverage.ChangedTasksInSourceProject, "InvalidTaskCoverage");
            Require(value.SuppressionKey == $"{value.EventType}:{value.SourceItemId:D}:{source.UserId:D}", "InvalidSuppressionDomain");
            Require(value.EmailEligible
                ? value.EmailExclusionReason is null && value.SuppressionAnchorAt is { } anchor && anchor.Offset == TimeSpan.Zero && anchor <= source.CapturedAt
                : value.SuppressionAnchorAt is null && value.EmailExclusionReason is not null && NoticeRefusalReason.All.Contains(value.EmailExclusionReason)
                    && NoticeIntentDisposition.Refusals.Contains(ExcludedState(value.EmailExclusionReason)), "InvalidOriginalEmailEligibility");
        }
        KeySet(source.AppCohortKeys); KeySet(source.EmailCohortKeys);
        Require(source.AppCohortKeys.Order().SequenceEqual(source.Obligations.Where(value => value.AppEligible).Select(value => value.Key).Order())
            && source.EmailCohortKeys.Order().SequenceEqual(source.Obligations.Where(value => value.EmailEligible).Select(value => value.Key).Order()), "InvalidChannelCohort");
        Require(source.AppCohortKeys.Length == 0
            ? source.AppNotificationId is null && source.NotificationCountAtCapture is null
            : source.AppNotificationId is { } app && app != Guid.Empty && source.NotificationCountAtCapture is > 0, "InvalidAppOccurrence");
        GuidSet(source.AppTaskIds, NoticeProofLimits.MaxChangedTasks, "InvalidAppTasks");
        GuidSet(source.EmailTaskIds, NoticeProofLimits.MaxChangedTasks, "InvalidEmailTasks");
        Require(OrderIds(source.AppTaskIds).SequenceEqual(CoveredTasks(source, source.AppCohortKeys))
            && OrderIds(source.EmailTaskIds).SequenceEqual(CoveredTasks(source, source.EmailCohortKeys)), "InvalidDistinctTaskCount");
    }

    static ImmutableArray<Guid> CoveredTasks(NoticeBulkOrigin source, ImmutableArray<int> keys)
    {
        var included = keys.ToHashSet();
        var notices = source.Obligations.Where(value => included.Contains(value.Key)).ToArray();
        var taskPairs = notices.Where(value => value.Coverage == NoticeBulkCoverage.Task)
            .Select(value => (TaskId: value.SourceItemId, ProjectId: value.SourceProjectId)).ToHashSet();
        var projects = notices.Where(value => value.Coverage == NoticeBulkCoverage.ChangedTasksInSourceProject)
            .Select(value => value.SourceProjectId).ToHashSet();
        return source.ChangedTasks.Where(task => projects.Contains(task.ProjectId) || taskPairs.Contains((task.TaskId, task.ProjectId)))
            .Select(task => task.TaskId).Distinct().OrderBy(Id, StringComparer.Ordinal).ToImmutableArray();
    }

    static void ValidateProgress(NoticeBulkProof proof)
    {
        Require(!proof.Progress.IsDefault && proof.Progress.Length == proof.Origin.Obligations.Length
            && proof.Progress.Select(value => value?.Key ?? 0).Order().SequenceEqual(proof.Origin.Obligations.Select(value => value.Key).Order()), "InvalidProgressKeys");
        var byKey = proof.Origin.Obligations.ToDictionary(value => value.Key);
        foreach (var value in proof.Progress)
        {
            Require(NoticeIntentDisposition.ObligationStates.Contains(value.State) && AsciiCode(value.State, NoticeProofLimits.MaxStateBytes)
                && value.UpdatedAt.Offset == TimeSpan.Zero && value.UpdatedAt >= proof.Origin.CapturedAt, "InvalidProgressState");
            Require(value.RefusalReason is null || AsciiCode(value.RefusalReason, NoticeProofLimits.MaxReasonBytes), "InvalidProgressReason");
            Require(NoticeRefusalReason.Matches(value.State, value.RefusalReason), "InvalidProgressReason");
            Require(value.EmailId != Guid.Empty, "InvalidProgressEmail");
            if (value.State is NoticeIntentDisposition.Queued or NoticeIntentDisposition.ReservedForDigest or NoticeIntentDisposition.Accepted
                or NoticeIntentDisposition.Simulated or NoticeIntentDisposition.Unknown or NoticeIntentDisposition.Uncertain)
                Require(value.EmailId is not null, "MissingProgressEmail");
            var original = byKey[value.Key];
            if (!original.EmailEligible)
                Require(value.State == ExcludedState(original.EmailExclusionReason!) && value.RefusalReason == original.EmailExclusionReason
                    && value.EmailId is null, "RevivedOriginalRefusal");
        }
    }

    static void ValidateTransitions(NoticeBulkProof before, ImmutableArray<NoticeBulkProgress> after)
    {
        var old = before.Progress.ToDictionary(value => value.Key);
        foreach (var value in after)
        {
            var prior = old[value.Key];
            Require(value.UpdatedAt >= prior.UpdatedAt, "RegressedProgressTime");
            if (NoticeIntentDisposition.Terminal.Contains(prior.State))
                Require(value.State == prior.State && value.EmailId == prior.EmailId && value.RefusalReason == prior.RefusalReason, "RevivedTerminalProgress");
            if (prior.State == NoticeIntentDisposition.Uncertain)
                Require((value.State is NoticeIntentDisposition.Uncertain or NoticeIntentDisposition.Accepted || NoticeIntentDisposition.Refusals.Contains(value.State))
                    && value.EmailId == prior.EmailId, "ReleasedUncertainProgress");
        }
    }

    static ImmutableArray<NoticeBulkProgress> OrderedProgress(ImmutableArray<NoticeBulkProgress> values)
    {
        Require(!values.IsDefault && values.All(value => value is not null), "InvalidProgressKeys");
        return values.OrderBy(value => value.Key).ToImmutableArray();
    }

    public static string? EncodeRenderedBulkMap(Guid expectedUserId, ImmutableArray<Guid> renderedOccurrenceIds,
        ImmutableArray<NoticeBulkRenderedMembership> memberships, IReadOnlyDictionary<Guid, NoticeBulkProof> renderedBulkProofs)
    {
        ValidateBulkMap(expectedUserId, renderedOccurrenceIds, memberships, renderedBulkProofs);
        if (memberships.IsEmpty) return null;
        using var stream = new LimitedBuffer(NoticeProofLimits.RenderedBytes, "RenderedMapTooLarge");
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var member in memberships.OrderBy(value => Id(value.IntentId), StringComparer.Ordinal))
            {
                writer.WriteStartObject(); writer.WriteNumber("v", 1); writer.WriteString("i", Id(member.IntentId));
                writer.WriteString("u", Id(member.UserId)); writer.WriteString("c", Id(member.CommandId)); writer.WriteString("h", member.OriginHash);
                IntArray(writer, "k", member.ObligationKeys.Order().ToImmutableArray()); writer.WriteEndObject();
            }
            writer.WriteEndArray(); writer.Flush();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static ImmutableArray<NoticeBulkRenderedMembership> DecodeRenderedBulkMap(string? json, Guid expectedUserId,
        ImmutableArray<Guid> renderedOccurrenceIds, IReadOnlyDictionary<Guid, NoticeBulkProof> renderedBulkProofs) => ClosedRead(() =>
    {
        if (json is null)
        {
            ValidateBulkMap(expectedUserId, renderedOccurrenceIds, [], renderedBulkProofs); return ImmutableArray<NoticeBulkRenderedMembership>.Empty;
        }
        Require(Encoding.UTF8.GetByteCount(json) <= 2 * NoticeProofLimits.RenderedBytes, "InvalidRenderedMapEncoding");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 }); UniqueProperties(document.RootElement);
        Require(document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() is > 0 and <= 10, "InvalidRenderedMap");
        var members = ImmutableArray.CreateBuilder<NoticeBulkRenderedMembership>();
        foreach (var value in document.RootElement.EnumerateArray())
        {
            Fields(value, "v", "i", "u", "c", "h", "k"); Require(value.GetProperty("v").GetInt32() == 1, "InvalidRenderedMapVersion");
            var keys = value.GetProperty("k"); Require(keys.ValueKind == JsonValueKind.Array && keys.GetArrayLength() is > 0 and <= 4096, "InvalidRenderedKeys");
            members.Add(new(value.GetProperty("i").GetGuid(), value.GetProperty("u").GetGuid(), value.GetProperty("c").GetGuid(),
                value.GetProperty("h").GetString()!, keys.EnumerateArray().Select(key => key.GetInt32()).ToImmutableArray()));
        }
        var result = members.ToImmutable(); _ = EncodeRenderedBulkMap(expectedUserId, renderedOccurrenceIds, result, renderedBulkProofs);
        return result.OrderBy(value => Id(value.IntentId), StringComparer.Ordinal).ToImmutableArray();
    });

    static void ValidateBulkMap(Guid user, ImmutableArray<Guid> ids, ImmutableArray<NoticeBulkRenderedMembership> members,
        IReadOnlyDictionary<Guid, NoticeBulkProof> proofs)
    {
        Require(user != Guid.Empty && !ids.IsDefault && ids.Length <= 10 && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Length
            && !members.IsDefault && members.Length <= 10 && members.All(value => value is not null)
            && members.Select(value => value.IntentId).Distinct().Count() == members.Length && proofs is not null
            && proofs.Values.All(value => value is not null)
            && members.Select(value => value.IntentId).OrderBy(Id, StringComparer.Ordinal).SequenceEqual(proofs.Keys.OrderBy(Id, StringComparer.Ordinal)), "InvalidRenderedMapBinding");
        foreach (var member in members)
        {
            Require(ids.Contains(member.IntentId) && member.UserId == user && proofs[member.IntentId].Origin.UserId == user, "ForeignRenderedMap");
            ValidateRendered(proofs[member.IntentId], member, member.IntentId);
        }
    }

    public static string EncodeDigestPolicy(NoticeDigestPolicyDocument document)
    {
        ValidateDigestPolicy(document);
        var policy = document.RenderedPolicy;
        var normalized = document with
        {
            Scope = NormalizeScope(document.Scope), ProjectionFlags = document.ProjectionFlags.OrderBy(value => value.Code, StringComparer.Ordinal).ToImmutableArray(),
            RenderedPolicy = policy with { RenderedSectionCodes = policy.RenderedSectionCodes.Order(StringComparer.Ordinal).ToImmutableArray(),
                UpdateProjectIds = OrderIds(policy.UpdateProjectIds), StaffDirectReportIds = OrderIds(policy.StaffDirectReportIds),
                StaffAssignmentNotificationIds = OrderIds(policy.StaffAssignmentNotificationIds),
                UpdateActivity = policy.UpdateActivity.OrderBy(value => Id(value.ProjectId), StringComparer.Ordinal)
                    .Select(value => value with { ActivityIds = OrderIds(value.ActivityIds) }).ToImmutableArray() }
        };
        using var stream = new LimitedBuffer(1024 * 1024, "DigestPolicyTooLarge");
        using (var writer = new Utf8JsonWriter(stream)) { JsonSerializer.Serialize(writer, normalized, OriginReadOptions); writer.Flush(); }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static NoticeDigestPolicyDocument DecodeDigestPolicy(string json, Guid expectedUserId, Guid expectedSlotId,
        int expectedCompositionVersion, NoticeDigestWindow expectedWindow, NoticeRequiredScope expectedScope) => ClosedRead(() =>
    {
        Require(json is not null && Encoding.UTF8.GetByteCount(json) <= 2 * 1024 * 1024, "InvalidDigestPolicyEncoding");
        using var parsed = JsonDocument.Parse(json!, new JsonDocumentOptions { MaxDepth = 16 }); UniqueProperties(parsed.RootElement);
        var document = parsed.RootElement.Deserialize<NoticeDigestPolicyDocument>(OriginReadOptions);
        Require(document is not null, "InvalidDigestPolicy"); ValidateDigestPolicy(document!);
        Require(document!.UserId == expectedUserId && document.SlotId == expectedSlotId && document.CompositionVersion == expectedCompositionVersion
            && document.Window == expectedWindow && SameScope(document.Scope, expectedScope), "ForeignDigestPolicy");
        _ = EncodeDigestPolicy(document); return document;
    });

    static NoticeRequiredScope NormalizeScope(NoticeRequiredScope scope) => new(OrderIds(scope.ProjectIds), OrderIds(scope.PlanningEntryIds));
    static bool SameScope(NoticeRequiredScope first, NoticeRequiredScope second) => first is not null && second is not null
        && OrderIds(first.ProjectIds).SequenceEqual(OrderIds(second.ProjectIds)) && OrderIds(first.PlanningEntryIds).SequenceEqual(OrderIds(second.PlanningEntryIds));
    static void ValidateDigestPolicy(NoticeDigestPolicyDocument document)
    {
        Require(document is not null && document.SchemaVersion == 1 && document.UserId != Guid.Empty && document.SlotId != Guid.Empty
            && document.CompositionVersion is >= 1 and <= 3 && document.Window is not null && document.Window.From <= document.Window.Through
            && document.Window.From.Offset == TimeSpan.Zero && document.Window.Through.Offset == TimeSpan.Zero, "InvalidDigestPolicyHeader");
        Require(document.Scope is not null && document.RenderedPolicy is not null && !document.ProjectionFlags.IsDefault, "InvalidDigestPolicy");
        GuidSet(document.Scope.ProjectIds, int.MaxValue, "InvalidDigestPolicyScope"); GuidSet(document.Scope.PlanningEntryIds, int.MaxValue, "InvalidDigestPolicyScope");
        var policy = document.RenderedPolicy;
        Require(!policy.RenderedSectionCodes.IsDefault && policy.RenderedSectionCodes.Length <= Digest.SectionCodes.Length
            && policy.RenderedSectionCodes.All(Digest.SectionCodes.Contains) && policy.RenderedSectionCodes.Distinct().Count() == policy.RenderedSectionCodes.Length, "InvalidRenderedSectionCodes");
        GuidSet(policy.UpdateProjectIds, int.MaxValue, "InvalidUpdateScope"); GuidSet(policy.StaffDirectReportIds, int.MaxValue, "InvalidStaffScope");
        GuidSet(policy.StaffAssignmentNotificationIds, int.MaxValue, "InvalidStaffScope");
        Require(!policy.UpdateActivity.IsDefault && policy.UpdateProjectIds.All(document.Scope.ProjectIds.Contains)
            && policy.UpdateActivity.All(value => value is not null) && policy.UpdateActivity.Select(value => value.ProjectId).Distinct().Count() == policy.UpdateActivity.Length
            && OrderIds(policy.UpdateProjectIds).SequenceEqual(policy.UpdateActivity.Select(value => value.ProjectId).OrderBy(Id, StringComparer.Ordinal)), "InvalidUpdateScope");
        foreach (var group in policy.UpdateActivity) { GuidSet(group.ActivityIds, int.MaxValue, "InvalidActivityScope"); Require(!group.ActivityIds.IsEmpty, "InvalidActivityScope"); }
        Require(policy.UpdateActivity.SelectMany(value => value.ActivityIds).Distinct().Count() == policy.UpdateActivity.Sum(value => value.ActivityIds.Length), "DuplicateActivityScope");
        Require(policy.UpdateProjectIds.IsEmpty || policy.RenderedSectionCodes.Contains("updates"), "InvalidUpdatePolicy");
        Require(policy.StaffDirectReportIds.IsEmpty && policy.StaffAssignmentNotificationIds.IsEmpty || policy.RenderedSectionCodes.Contains("staff"), "InvalidStaffPolicy");
        Require(document.ProjectionFlags.All(value => value is not null) && document.ProjectionFlags.Select(value => value.Code).Distinct().Count() == document.ProjectionFlags.Length,
            "InvalidProjectionPolicy");
        var expected = policy.UpdateActivity.IsEmpty ? Array.Empty<string>() : NotificationDelivery.ProjectionCodes;
        Require(document.ProjectionFlags.Select(value => value.Code).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)), "InvalidProjectionPolicy");
    }
    static T ClosedRead<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception error) when (error is JsonException or InvalidOperationException or FormatException or OverflowException or ArgumentException)
        { if (error is NoticeProofException) throw; throw new NoticeProofException("InvalidNoticeProofEncoding"); }
    }

    static byte[] WriteOrigin(NoticeBulkOrigin origin, bool includeHash)
    {
        using var stream = new LimitedBuffer(NoticeProofLimits.OriginBytes, "OriginTooLarge");
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject(); writer.WriteNumber("schemaVersion", origin.SchemaVersion); writer.WriteString("kind", origin.Kind);
            writer.WriteString("commandId", Id(origin.CommandId)); writer.WriteString("originalActorId", Id(origin.OriginalActorId));
            writer.WriteString("userId", Id(origin.UserId)); writer.WriteString("capturedAt", Utc(origin.CapturedAt));
            writer.WritePropertyName("changedTasks"); writer.WriteStartArray();
            foreach (var task in origin.ChangedTasks)
            { writer.WriteStartObject(); writer.WriteString("taskId", Id(task.TaskId)); writer.WriteString("projectId", Id(task.ProjectId)); writer.WriteEndObject(); }
            writer.WriteEndArray(); writer.WritePropertyName("obligations"); writer.WriteStartArray();
            foreach (var value in origin.Obligations)
            {
                writer.WriteStartObject(); writer.WriteNumber("k", value.Key); writer.WriteString("ev", value.EventType);
                writer.WriteString("it", value.SourceItemType); writer.WriteString("id", Id(value.SourceItemId));
                writer.WriteString("p", Id(value.SourceProjectId)); GuidArray(writer, "ps", value.RequiredProjectIds);
                GuidArray(writer, "ns", value.RequiredPlanningEntryIds); NullableGuid(writer, "a", value.SourceActorUserId);
                writer.WriteBoolean("d", value.IsDirectRecipient); writer.WriteBoolean("app", value.AppEligible);
                writer.WriteBoolean("mail", value.EmailEligible); NullableString(writer, "x", value.EmailExclusionReason);
                writer.WriteString("sk", value.SuppressionKey);
                NullableString(writer, "sa", value.SuppressionAnchorAt is { } anchor ? Utc(anchor) : null);
                writer.WriteString("c", value.Coverage); writer.WriteEndObject();
            }
            writer.WriteEndArray(); IntArray(writer, "appCohortKeys", origin.AppCohortKeys); IntArray(writer, "emailCohortKeys", origin.EmailCohortKeys);
            NullableGuid(writer, "appNotificationId", origin.AppNotificationId);
            if (origin.NotificationCountAtCapture is { } count) writer.WriteNumber("notificationCountAtCapture", count); else writer.WriteNull("notificationCountAtCapture");
            GuidArray(writer, "appTaskIds", origin.AppTaskIds); GuidArray(writer, "emailTaskIds", origin.EmailTaskIds);
            if (includeHash) writer.WriteString("originHash", origin.OriginHash);
            writer.WriteEndObject(); writer.Flush();
        }
        return stream.ToArray();
    }

    static void UniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            { Require(names.Add(property.Name), "DuplicateProofProperty"); UniqueProperties(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) UniqueProperties(item);
    }
    static void Fields(JsonElement value, params string[] names) => Require(value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)), "InvalidProofFields");
    static void GuidSet(ImmutableArray<Guid> values, int max, string code) => Require(!values.IsDefault && values.Length <= max
        && values.All(value => value != Guid.Empty) && values.Distinct().Count() == values.Length, code);
    static void KeySet(ImmutableArray<int> values) => Require(!values.IsDefault && values.Length <= NoticeProofLimits.MaxObligations
        && values.All(value => value is >= 1 and <= NoticeProofLimits.MaxObligations) && values.Distinct().Count() == values.Length, "InvalidCohortKeys");
    static bool AsciiCode(string? value, int max) => value is { Length: > 0 } && value.Length <= max
        && value.All(letter => char.IsAsciiLetterOrDigit(letter) || letter == '_');
    static string ExcludedState(string reason) => reason is NoticeRefusalReason.PlanningAssignmentWithdrawn or NoticeRefusalReason.ProjectAccessRemoved
        or NoticeRefusalReason.PlanningAccessRemoved ? NoticeIntentDisposition.AccessRemoved : reason;
    static ImmutableArray<Guid> OrderIds(ImmutableArray<Guid> values) => values.OrderBy(Id, StringComparer.Ordinal).ToImmutableArray();
    static string Id(Guid value) => value.ToString("D").ToLowerInvariant();
    static string Utc(DateTimeOffset value) => value.ToUniversalTime().ToString(NoticeProofLimits.UtcFormat, CultureInfo.InvariantCulture);
    static void Require(bool condition, string code) { if (!condition) throw new NoticeProofException(code); }
    static void NullableString(Utf8JsonWriter writer, string name, string? value)
    { if (value is null) writer.WriteNull(name); else writer.WriteString(name, value); }
    static void NullableGuid(Utf8JsonWriter writer, string name, Guid? value) => NullableString(writer, name, value is { } id ? Id(id) : null);
    static void GuidArray(Utf8JsonWriter writer, string name, ImmutableArray<Guid> values)
    { writer.WritePropertyName(name); writer.WriteStartArray(); foreach (var value in values) writer.WriteStringValue(Id(value)); writer.WriteEndArray(); }
    static void IntArray(Utf8JsonWriter writer, string name, ImmutableArray<int> values)
    { writer.WritePropertyName(name); writer.WriteStartArray(); foreach (var value in values) writer.WriteNumberValue(value); writer.WriteEndArray(); }

    sealed class LimitedBuffer(int maximum, string code) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count) { CheckSize(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { CheckSize(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { CheckSize(1); base.WriteByte(value); }
        void CheckSize(int count) => Require(Length + count <= maximum, code);
    }
}
