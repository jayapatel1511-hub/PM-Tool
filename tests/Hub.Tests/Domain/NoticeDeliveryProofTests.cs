using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Tests.Domain;

// Pure codec/transition tests only: assigning Accepted below is not a provider or delivery integration test.
public sealed class NoticeDeliveryProofTests
{
    static readonly DateTimeOffset Captured = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    static Guid G(int number) => Guid.Parse($"00000000-0000-0000-0000-{number:D12}");

    static NoticeBulkOrigin Source(int count = 2, int projectCount = 2, int extraScopes = 0)
    {
        var tasks = Enumerable.Range(0, projectCount).Select(index => new NoticeChangedTask(G(1000 + index), G(10000 + index))).ToImmutableArray();
        var remaining = extraScopes;
        var notices = Enumerable.Range(0, count).Select(index =>
        {
            var task = tasks[index % tasks.Length];
            var extra = Math.Min(projectCount - 1, remaining); remaining -= extra;
            var scope = Enumerable.Range(0, extra + 1).Select(offset => tasks[(index % tasks.Length + offset) % tasks.Length].ProjectId).ToImmutableArray();
            return new NoticeBulkObligation(index + 1, NotificationEvents.TaskAssigned, ItemType.Task, task.TaskId, task.ProjectId,
                scope, [], G(2), true, true, true, null, $"{NotificationEvents.TaskAssigned}:{task.TaskId:D}:{G(1):D}", Captured, NoticeBulkCoverage.Task);
        }).ToImmutableArray();
        var covered = notices.Select(value => value.SourceItemId).Distinct().ToImmutableArray();
        var keys = Enumerable.Range(1, count).ToImmutableArray();
        return new(1, NoticeProofLimits.Kind, G(4), G(2), G(1), Captured, tasks, notices, keys, keys, G(3), 1, covered, covered);
    }

    [Fact]
    public void Canonical_hash_roundtrips_reordering_and_does_not_hash_itself_or_progress()
    {
        var first = NoticeDeliveryProof.Admit(Source());
        var reversed = Source() with { ChangedTasks = Source().ChangedTasks.Reverse().ToImmutableArray(),
            Obligations = Source().Obligations.Reverse().ToImmutableArray(), AppCohortKeys = [2, 1], EmailCohortKeys = [2, 1] };
        var second = NoticeDeliveryProof.Admit(reversed);
        Assert.Equal(NoticeDeliveryProof.Encode(first), NoticeDeliveryProof.Encode(second));
        var queued = NoticeDeliveryProof.WithProgress(first, first.Progress.Select(value => value with
            { State = NoticeIntentDisposition.Queued, EmailId = G(20), UpdatedAt = Captured.AddMinutes(1) }).ToImmutableArray());
        Assert.Equal(first.Origin.OriginHash, queued.Origin.OriginHash);
        Assert.True(NoticeDeliveryProof.HasSameImmutableOrigin(NoticeDeliveryProof.Encode(first), NoticeDeliveryProof.Encode(queued), G(1)));
        var decoded = NoticeDeliveryProof.Decode(NoticeDeliveryProof.Encode(queued), G(1), G(4));
        Assert.Equal(NoticeDeliveryProof.Encode(queued), NoticeDeliveryProof.Encode(decoded));
        var pretty = JsonNode.Parse(NoticeDeliveryProof.Encode(queued))!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        Assert.Equal(NoticeDeliveryProof.Encode(queued), NoticeDeliveryProof.Encode(NoticeDeliveryProof.Decode(pretty, G(1), G(4))));
    }

    [Fact]
    public void Whole_app_inclusion_needs_exact_event_keys_despite_equal_task_counts()
    {
        var source = Source(3);
        var notices = source.Obligations.SetItem(0, source.Obligations[0] with
            { EmailEligible = false, EmailExclusionReason = NoticeRefusalReason.ChannelOff, SuppressionAnchorAt = null })
            .SetItem(2, source.Obligations[2] with { AppEligible = false });
        var proof = NoticeDeliveryProof.Admit(source with { Obligations = notices, AppCohortKeys = [1, 2], EmailCohortKeys = [2, 3] });
        Assert.Equal(2, proof.Origin.AppTaskIds.Length); Assert.Equal(2, proof.Origin.EmailTaskIds.Length);
        var rendered = NoticeDeliveryProof.Render(G(30), proof, [2, 3]);
        Assert.False(NoticeDeliveryProof.IncludesWholeAppCohort(proof, rendered.ObligationKeys));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Render(G(30), proof, [1]));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.ValidateRendered(proof, rendered with { UserId = G(99) }, G(30)));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.ValidateRendered(proof, rendered, G(31)));
        var full = NoticeDeliveryProof.Admit(Source(3));
        Assert.True(NoticeDeliveryProof.IncludesWholeAppCohort(full, [1, 2, 3]));
    }

    [Fact]
    public void Project_notice_coverage_unions_actual_changed_tasks_without_multiplying_count()
    {
        var source = Source(3); var project = source.ChangedTasks[0].ProjectId;
        var tasks = source.ChangedTasks.Select(value => value with { ProjectId = project }).ToImmutableArray();
        var notices = source.Obligations.Select(value => value with { SourceProjectId = project, RequiredProjectIds = [project] }).ToImmutableArray();
        notices = notices.SetItem(1, notices[1] with { SourceItemType = ItemType.Project, SourceItemId = project,
            EventType = NotificationEvents.StaffAssignment, IsDirectRecipient = false, Coverage = NoticeBulkCoverage.ChangedTasksInSourceProject,
            SuppressionKey = $"{NotificationEvents.StaffAssignment}:{project:D}:{G(1):D}" });
        var proof = NoticeDeliveryProof.Admit(source with { ChangedTasks = tasks, Obligations = notices });
        Assert.Equal(3, proof.Progress.Length); Assert.Equal(2, proof.Origin.EmailTaskIds.Length);
        Assert.Equal(1, proof.Origin.NotificationCountAtCapture); // Count ordinal is not the distinct changed-task count.
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(Source() with
            { Obligations = Source().Obligations.SetItem(0, Source().Obligations[0] with { SourceItemId = G(1001), SuppressionKey = $"{NotificationEvents.TaskAssigned}:{G(1001):D}:{G(1):D}" }) }));
    }

    [Fact]
    public void Unknown_duplicate_foreign_missing_and_mutated_proofs_fail_closed_without_raw_diagnostics()
    {
        var proof = NoticeDeliveryProof.Admit(Source()); var json = NoticeDeliveryProof.Encode(proof);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(json, G(99), G(4)));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(json, G(1), G(99)));
        Assert.False(NoticeDeliveryProof.HasSameImmutableOrigin(json, NoticeDeliveryProof.Encode(NoticeDeliveryProof.Admit(Source() with { CommandId = G(99) })), G(1)));
        Assert.False(NoticeDeliveryProof.HasSameImmutableOrigin(json, json, G(99)));
        Assert.False(NoticeDeliveryProof.HasSameImmutableOrigin("not-json", json, G(1)));
        var unknown = JsonNode.Parse(json)!; unknown["origin"]!["schemaVersion"] = 2;
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(unknown.ToJsonString(), G(1), G(4)));
        var missing = JsonNode.Parse(json)!; missing["origin"]!["obligations"]![0]!.AsObject().Remove("mail");
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(missing.ToJsonString(), G(1), G(4)));
        var extra = JsonNode.Parse(json)!; extra["progress"]![0]!["rawReason"] = "Synthetic private text";
        var error = Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(extra.ToJsonString(), G(1), G(4)));
        Assert.DoesNotContain("Synthetic private text", error.Message);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Decode(json.Replace("\"k\":1,", "\"k\":1,\"k\":1,", StringComparison.Ordinal), G(1), G(4)));
        var tampered = JsonNode.Parse(json)!; tampered["origin"]!["originalActorId"] = G(88).ToString();
        Assert.False(NoticeDeliveryProof.HasSameImmutableOrigin(json, tampered.ToJsonString(), G(1)));
    }

    [Fact]
    public void Terminal_refusal_and_uncertain_membership_cannot_be_revived_or_released()
    {
        var proof = NoticeDeliveryProof.Admit(Source(1, 1));
        var refused = NoticeDeliveryProof.WithProgress(proof, [new(1, NoticeIntentDisposition.AccessRemoved, Captured.AddMinutes(1), NoticeRefusalReason.PlanningAssignmentWithdrawn)]);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.WithProgress(refused,
            [new(1, NoticeIntentDisposition.Queued, Captured.AddMinutes(2), EmailId: G(20))]));
        var uncertain = NoticeDeliveryProof.WithProgress(proof, [new(1, NoticeIntentDisposition.Uncertain, Captured.AddMinutes(1), EmailId: G(20))]);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.WithProgress(uncertain,
            [new(1, NoticeIntentDisposition.Exhausted, Captured.AddMinutes(2), NoticeRefusalReason.AttemptsExhausted, G(20))]));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.WithProgress(uncertain,
            [new(1, NoticeIntentDisposition.HeldAway, Captured.AddMinutes(2))]));
        var accepted = NoticeDeliveryProof.WithProgress(uncertain, [new(1, NoticeIntentDisposition.Accepted, Captured.AddMinutes(2), EmailId: G(20))]);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.WithProgress(accepted,
            [new(1, NoticeIntentDisposition.Accepted, Captured.AddMinutes(3), EmailId: G(21))]));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.WithProgress(proof, [new(1, NoticeIntentDisposition.Mixed, Captured)]));
        Assert.False(NoticeDeliveryProof.HasSameImmutableOrigin(NoticeDeliveryProof.Encode(refused), NoticeDeliveryProof.Encode(proof), G(1)));
    }

    [Fact]
    public void Counts_scope_and_fixed_progress_vocabulary_are_bounded_before_capture()
    {
        Assert.Equal(819202, NoticeDeliveryProof.CompleteProgressReservedBytes(4096));
        Assert.True(NoticeDeliveryProof.CompleteProgressReservedBytes(4096) < NoticeProofLimits.ProgressAllowanceBytes);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.CompleteProgressReservedBytes(4097));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(Source(4097)));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(Source(1, 201)));
        var source = Source();
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(source with { Obligations = source.Obligations.SetItem(0,
            source.Obligations[0] with { RequiredProjectIds = [source.ChangedTasks[0].ProjectId, G(99999)] }) }));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(source with { Obligations = source.Obligations.SetItem(0,
            source.Obligations[0] with { RequiredPlanningEntryIds = [G(99)] }) }));
        Assert.All(NoticeIntentDisposition.ObligationStates, value => Assert.InRange(Encoding.UTF8.GetByteCount(value), 1, 32));
        Assert.All(NoticeRefusalReason.All, value => Assert.InRange(Encoding.UTF8.GetByteCount(value), 1, 64));
        Assert.DoesNotContain(NoticeIntentDisposition.Mixed, NoticeIntentDisposition.ObligationStates);
        Assert.Equal("TimeReminder", NoticeSlotKind.Reminder);
        Assert.Equal(3, NoticeProofLimits.MaxCompositions); Assert.Equal(8, NoticeProofLimits.MaxSenderReservations);
    }

    [Fact]
    public void Near_limit_origin_with_all_4096_records_reserves_every_later_legal_outcome()
    {
        // Each extra trusted project Guid adds39 canonical bytes. Find the greatest representable valid Origin.
        var low = 0; var high = 4096 * 199; NoticeBulkProof? largest = null;
        while (low <= high)
        {
            var middle = low + (high - low) / 2;
            try { largest = NoticeDeliveryProof.Admit(Source(4096, 200, middle)); low = middle + 1; }
            catch (NoticeProofException error) when (error.Code == "OriginTooLarge") { high = middle - 1; }
        }
        Assert.NotNull(largest); var proof = largest!;
        var originalJson = NoticeDeliveryProof.Encode(proof);
        var originBytes = Encoding.UTF8.GetByteCount(JsonNode.Parse(originalJson)!["origin"]!.ToJsonString());
        Assert.InRange(NoticeProofLimits.OriginBytes - originBytes, 0, 38);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.Admit(Source(4096, 200, low)));
        var email = G(20);
        foreach (var state in NoticeIntentDisposition.ObligationStates)
        {
            var reason = NoticeIntentDisposition.Refusals.Contains(state) ? state : null;
            if (state == NoticeIntentDisposition.AccessRemoved) reason = NoticeRefusalReason.PlanningAssignmentWithdrawn;
            if (state == NoticeIntentDisposition.Exhausted) reason = NoticeRefusalReason.AttemptsExhausted;
            var progress = proof.Progress.Select(value => new NoticeBulkProgress(value.Key, state,
                new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero).AddTicks(9999999), reason,
                state is NoticeIntentDisposition.Pending or NoticeIntentDisposition.PendingAvailability or NoticeIntentDisposition.HeldAway or NoticeIntentDisposition.HeldNoDigest ? null : email)).ToImmutableArray();
            var next = NoticeDeliveryProof.WithProgress(proof, progress);
            var encoded = NoticeDeliveryProof.Encode(next);
            Assert.Equal(proof.Origin.OriginHash, next.Origin.OriginHash);
            Assert.True(NoticeDeliveryProof.HasSameImmutableOrigin(originalJson, encoded, G(1)));
            Assert.Equal(encoded, NoticeDeliveryProof.Encode(NoticeDeliveryProof.Decode(encoded, G(1), G(4))));
            var progressBytes = Encoding.UTF8.GetByteCount(JsonNode.Parse(encoded)!["progress"]!.ToJsonString());
            Assert.InRange(progressBytes, 1, NoticeDeliveryProof.CompleteProgressReservedBytes(4096));
            Assert.InRange(Encoding.UTF8.GetByteCount(encoded), 1, NoticeProofLimits.MaxCanonicalBytes);
        }
    }

    [Fact]
    public void Rendered_map_is_closed_exactly_bound_and_distinct_from_transport_acceptance()
    {
        var proof = NoticeDeliveryProof.Admit(Source()); var intent = G(30);
        var proofs = new Dictionary<Guid, NoticeBulkProof> { [intent] = proof };
        var member = NoticeDeliveryProof.Render(intent, proof, [2, 1]);
        var json = NoticeDeliveryProof.EncodeRenderedBulkMap(G(1), [intent, G(31)], [member], proofs)!;
        var decoded = Assert.Single(NoticeDeliveryProof.DecodeRenderedBulkMap(json, G(1), [intent, G(31)], proofs));
        Assert.Equal(new[] { 1, 2 }, decoded.ObligationKeys.ToArray());
        Assert.Null(NoticeDeliveryProof.EncodeRenderedBulkMap(G(1), [], [], new Dictionary<Guid, NoticeBulkProof>()));
        Assert.Empty(NoticeDeliveryProof.DecodeRenderedBulkMap(null, G(1), [G(31)], new Dictionary<Guid, NoticeBulkProof>()));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(null, G(1), [intent], proofs));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(json, G(99), [intent], proofs));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(json, G(1), [], proofs));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.EncodeRenderedBulkMap(G(1), [intent], [member, member], proofs));
        var extra = JsonNode.Parse(json)!; extra[0]!["accepted"] = true;
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(extra.ToJsonString(), G(1), [intent], proofs));
        var tampered = JsonNode.Parse(json)!; tampered[0]!["h"] = new string('0', 64);
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(tampered.ToJsonString(), G(1), [intent], proofs));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(json.Replace("\"v\":1", "\"v\":1,\"v\":1", StringComparison.Ordinal), G(1), [intent], proofs));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeRenderedBulkMap(new string(' ', 2 * 1024 * 1024 + 1), G(1), [intent], proofs));
        Assert.All(proof.Progress, value => Assert.Equal(NoticeIntentDisposition.Pending, value.State));
    }

    static NoticeDigestPolicyDocument Policy() => new(1, G(1), G(40), 1, new(Captured.AddDays(-1), Captured), new([G(10000)], []),
        new(["updates", "staff"], [G(10000)], [G(50)], [G(51)], [new(G(10000), [G(60), G(61)])], false),
        new[] { "register_screens", "people_screens", "capacity_screens", "visual_system", "work_screens" }
            .Select(code => new NoticeDigestProjectionFlag(code, true)).ToImmutableArray());

    [Fact]
    public void Ordinary_policy_witness_is_canonical_closed_and_bound_to_actual_window_scope_and_version()
    {
        var policy = Policy(); var json = NoticeDeliveryProof.EncodeDigestPolicy(policy);
        var reversed = policy with { ProjectionFlags = policy.ProjectionFlags.Reverse().ToImmutableArray(),
            RenderedPolicy = policy.RenderedPolicy with { RenderedSectionCodes = ["staff", "updates"],
                UpdateActivity = [new(G(10000), [G(61), G(60)])] } };
        Assert.Equal(json, NoticeDeliveryProof.EncodeDigestPolicy(reversed));
        var decoded = NoticeDeliveryProof.DecodeDigestPolicy(json, G(1), G(40), 1, policy.Window, policy.Scope);
        Assert.Equal(json, NoticeDeliveryProof.EncodeDigestPolicy(decoded));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(json, G(2), G(40), 1, policy.Window, policy.Scope));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(json, G(1), G(41), 1, policy.Window, policy.Scope));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(json, G(1), G(40), 2, policy.Window, policy.Scope));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(json, G(1), G(40), 1, policy.Window with { Through = Captured.AddMinutes(1) }, policy.Scope));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(json, G(1), G(40), 1, policy.Window, new([G(10001)], [])));
        var extra = JsonNode.Parse(json)!; extra["providerAcceptedAt"] = Captured;
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(extra.ToJsonString(), G(1), G(40), 1, policy.Window, policy.Scope));
        var missing = JsonNode.Parse(json)!; missing["renderedPolicy"]!.AsObject().Remove("includesAdminReview");
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(missing.ToJsonString(), G(1), G(40), 1, policy.Window, policy.Scope));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.EncodeDigestPolicy(policy with { ProjectionFlags = [] }));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.EncodeDigestPolicy(policy with { RenderedPolicy = policy.RenderedPolicy with { UpdateProjectIds = [] } }));
        Assert.Throws<NoticeProofException>(() => NoticeDeliveryProof.DecodeDigestPolicy(new string(' ', 2 * 1024 * 1024 + 1), G(1), G(40), 1, policy.Window, policy.Scope));
        var heldOnly = policy with { RenderedPolicy = new([], [], [], [], [], false), ProjectionFlags = [] };
        Assert.NotEmpty(NoticeDeliveryProof.EncodeDigestPolicy(heldOnly));
    }

    [Fact]
    public void Local_schedule_resolves_first_valid_instant_and_earliest_ambiguous_utc_without_losing_requested_time()
    {
        var settings = new OrgSettings { OrgTimeZone = "America/Halifax" };
        var gap = NotificationDelivery.ResolveLocal(settings, new(2026, 3, 8), new TimeOnly(2, 30, 37).Add(TimeSpan.FromTicks(123)));
        Assert.Equal(new TimeOnly(2, 30, 37).Add(TimeSpan.FromTicks(123)), gap.RequestedTime);
        Assert.Equal(new TimeOnly(3, 0), gap.EffectiveTime);
        Assert.Equal(new DateTimeOffset(2026, 3, 8, 6, 0, 0, TimeSpan.Zero), gap.ScheduledAtUtc);
        var overlap = NotificationDelivery.ResolveLocal(settings, new(2026, 11, 1), new(1, 30));
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 4, 30, 0, TimeSpan.Zero), overlap.ScheduledAtUtc);
        Assert.Equal(new DateOnly(2026, 10, 26), NotificationDelivery.WeekMonday(overlap.ScheduledAtUtc, settings));
    }
}
