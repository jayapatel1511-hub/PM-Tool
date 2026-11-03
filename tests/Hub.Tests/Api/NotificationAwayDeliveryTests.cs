using System.Collections.Immutable;
using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// Isolated PostgreSQL admission/claim/completion cases. Senders below only record synthetic envelopes.
// Direct core capture is an explicit trusted-source fixture; producer boundary coverage belongs to Root.
public sealed class NotificationAwayDeliveryTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public NotificationAwayDeliveryTests() => data = new(f);
    public void Dispose() => f.Dispose();

    async Task<(Guid User, Guid Actor, Guid Project, Guid Task)> Setup(bool hold = false)
    {
        var project = await data.Project(); var user = data.User(TestData.Alex); var actor = data.User(TestData.Marc);
        var task = await data.NewTask(project.Id, extra: new { name = "Synthetic notice source" });
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "UTC" })).EnsureSuccessStatusCode();
        await (await f.As(TestData.Alex).Patch("/api/v1/me/notice-preferences", new { revision = 0, holdImmediateEmailWhenAway = hold })).Json();
        return (user, actor, project.Id, task.G("id"));
    }
    static NoticeRawCall Raw((Guid User, Guid Actor, Guid Project, Guid Task) source, Guid correlation, int ordinal = 1,
        string code = NotificationEvents.TaskAssigned, string subject = "Synthetic first subject") => new(ordinal, correlation, code,
        [source.User], source.Actor, [source.User], new(NoticeSourceKind.Work, source.Project, ItemType.Task, source.Task, "SYN-1", "/my-work",
            new([source.Project], []), "Immediate", new(ItemType.Task, source.Task, "SYN-1", "/my-work")),
        new(subject, "Synthetic application body", subject, "Synthetic immutable mail body", null));
    Task<T> Recipient<T>(Guid user, Func<NoticeRecipientContext, Task<T>> work) => f.DbAsync(db => Tx.Run(db, async () =>
    {
        var context = await NotificationDelivery.LockRecipientAsync(db, user); var result = await work(context);
        await db.SaveChangesAsync(); return result;
    }));
    async Task<NoticeCaptureResult> Stage(HubDb db, NoticeRawCall[] calls, NoticeBulkCaptureBasis? bulk = null)
    {
        var buffer = NotificationDelivery.BeginCapture(db, calls[0].CorrelationId, bulk is null ? NoticeCaptureMode.Ordinary : NoticeCaptureMode.TaskBulk);
        foreach (var call in calls) NotificationDelivery.Buffer(buffer, call);
        var session = await NotificationDelivery.LockCaptureAsync(buffer, calls.SelectMany(call => call.Source.Scope.ProjectIds).Distinct().ToImmutableArray());
        var evaluation = await NotificationDelivery.EvaluateCaptureAsync(session, f.Clock.Now);
        var bindings = ImmutableArray.CreateBuilder<NoticeAppBinding>();
        foreach (var group in evaluation.Decisions.Where(value => value.AppEligible).GroupBy(value => bulk is null
            ? $"{value.UserId}:{calls[value.CallOrdinal - 1].EventType}:{calls[value.CallOrdinal - 1].Source.ItemId}:{calls[value.CallOrdinal - 1].ActualActorId}"
            : value.UserId.ToString()))
        {
            var decision = group.First(); var call = calls[decision.CallOrdinal - 1];
            var row = new Notification { UserId = decision.UserId, EventType = call.EventType, ItemType = bulk is null ? call.Source.ItemType : "TaskBulkCommand",
                ItemId = bulk?.CommandReceiptId ?? call.Source.ItemId, Title = call.Content.AppTitle, ActorUserId = call.ActualActorId,
                CorrelationId = call.CorrelationId, RequiredProjectIds = group.SelectMany(value => calls[value.CallOrdinal - 1].Source.Scope.ProjectIds).Distinct().ToArray(),
                LinkPath = "/my-work", CreatedAt = f.Clock.Now, UpdatedAt = f.Clock.Now };
            db.Notifications.Add(row); bindings.Add(new(decision.UserId, bulk is null ? decision.CallOrdinal : null, row.Id, row.Count));
        }
        return bulk is null ? await NotificationDelivery.StageOrdinaryAsync(session, evaluation, bindings.ToImmutable(), f.Clock.Now)
            : await NotificationDelivery.StageBulkAsync(session, evaluation, bulk, bindings.ToImmutable(), f.Clock.Now);
    }
    Task<NoticeCaptureResult> Capture(params NoticeRawCall[] calls) => f.DbAsync(db => Tx.Run(db, async () =>
    { var result = await Stage(db, calls); await db.SaveChangesAsync(); return result; }));
    Task<NoticeClaimResult> Claim(Guid user, Guid email, string kind = NoticeTransportKind.Smtp, NoticeClaimPurpose purpose = NoticeClaimPurpose.Initial)
        => Recipient(user, context => NotificationDelivery.ClaimAsync(context, email, kind, purpose, f.Clock.Now));
    Task<NoticeCompletionResult> Complete(NoticeSendClaim claim, NoticeSendObservation observation)
        => Recipient(claim.UserId, context => NotificationDelivery.CompleteAsync(context, claim, observation));
    EmailMessage Email(Guid id) => f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == id));
    NoticeDeliveryIntent Intent(Guid id) => f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.Id == id));
    async Task<(Guid User, Guid Command, NoticeCaptureResult Captured)> BulkPair()
    {
        var source = await Setup(); var second = await data.NewTask(source.Project, extra: new { name = "Synthetic eligible sibling" });
        var command = Guid.NewGuid();
        var calls = new[] { Raw(source, command), Raw((source.User, source.Actor, source.Project, second.G("id")), command, 2, NotificationEvents.ReviewerSet) };
        var captured = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var result = await Stage(db, calls, new(command, source.Actor, [new(source.Task, source.Project), new(second.G("id"), source.Project)]));
            await db.SaveChangesAsync(); return result;
        }));
        return (source.User, command, captured);
    }

    [Fact]
    public async Task Compatible_occurrences_keep_first_content_and_actual_actor_while_new_command_keeps_rolling_suppression()
    {
        var source = await Setup(); var correlation = Guid.NewGuid();
        var result = await Capture(Raw(source, correlation), Raw(source, correlation, 2, subject: "Synthetic later compatible content"));
        var intent = Intent(Assert.Single(result.IntentIds)); var email = Email(Assert.Single(result.EmailIds));
        Assert.Equal("Synthetic first subject", email.Subject); Assert.Equal(email.Id, intent.DeliveryEmailId);
        Assert.Equal($"notice:v1:{correlation:D}:TaskAssigned:Task:{source.Task:D}:{source.Actor:D}:{source.User:D}", intent.OccurrenceKey);
        var second = await Capture(Raw(source, Guid.NewGuid())); var duplicate = Intent(Assert.Single(second.IntentIds));
        Assert.Empty(second.EmailIds); Assert.Equal(NoticeIntentDisposition.Duplicate, duplicate.Disposition);
        Assert.Null(duplicate.SuppressionAnchorAt); Assert.NotNull(intent.SuppressionAnchorAt);
    }

    [Fact]
    public async Task Incompatible_legacy_collapse_alias_rolls_back_whole_business_transaction()
    {
        var source = await Setup(); var before = f.Db(db => db.Tasks.Single(row => row.Id == source.Task).Name);
        var count = f.Db(db => db.NoticeDeliveryIntents.Count()); var correlation = Guid.NewGuid(); var first = Raw(source, correlation);
        var other = first with { CallOrdinal = 2, Source = first.Source with { ItemType = ItemType.Deliverable } };
        await Assert.ThrowsAsync<ApiException>(() => f.DbAsync(db => Tx.Run(db, async () =>
        {
            (await db.Tasks.SingleAsync(row => row.Id == source.Task)).Name = "Synthetic should roll back";
            await db.SaveChangesAsync(); return await Stage(db, [first, other]);
        })));
        Assert.Equal(before, f.Db(db => db.Tasks.Single(row => row.Id == source.Task).Name));
        Assert.Equal(count, f.Db(db => db.NoticeDeliveryIntents.Count()));
    }

    [Fact]
    public async Task Fresh_flag_off_after_buffering_refuses_before_staging_and_rolls_back_source()
    {
        var source = await Setup(); var before = f.Db(db => db.Tasks.Single(row => row.Id == source.Task).Name);
        await Assert.ThrowsAsync<ApiException>(() => f.DbAsync(db => Tx.Run(db, async () =>
        {
            // Buffer and source mutation deliberately share the owner's transaction, including earlier saves.
            var call = Raw(source, Guid.NewGuid()); var buffer = NotificationDelivery.BeginCapture(db, call.CorrelationId);
            NotificationDelivery.Buffer(buffer, call);
            (await db.Tasks.SingleAsync(row => row.Id == source.Task)).Name = "Synthetic should roll back on flag race";
            (await db.Settings.SingleAsync(row => row.Key == "flag.people_screens")).Value = "false";
            await db.SaveChangesAsync();
            var session = await NotificationDelivery.LockCaptureAsync(buffer, [source.Project]);
            return await NotificationDelivery.EvaluateCaptureAsync(session, f.Clock.Now);
        })));
        Assert.Equal(before, f.Db(db => db.Tasks.Single(row => row.Id == source.Task).Name));
        Assert.Equal("true", f.Db(db => db.Settings.Single(row => row.Key == "flag.people_screens").Value));
    }

    [Fact]
    public async Task Away_capture_is_held_with_original_age_and_24h_domain_without_an_email()
    {
        var source = await Setup(hold: true); var date = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = source.User,
            WorkDate = date, AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 }); return await db.SaveChangesAsync(); });
        var at = f.Clock.Now; var result = await Capture(Raw(source, Guid.NewGuid()));
        var intent = Intent(Assert.Single(result.IntentIds)); Assert.Empty(result.EmailIds);
        Assert.Equal(NoticeIntentDisposition.HeldAway, intent.Disposition); Assert.Null(intent.DeliveryEmailId);
        Assert.Equal(at, intent.CreatedAt); Assert.Equal(at, intent.SuppressionAnchorAt); Assert.NotNull(intent.NextPolicyCheckAt);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var duplicate = await Capture(Raw(source, Guid.NewGuid()));
        Assert.Equal(NoticeIntentDisposition.Duplicate, Intent(Assert.Single(duplicate.IntentIds)).Disposition);
    }

    [Fact]
    public async Task Turning_hold_off_queues_one_original_payload_without_rebasing_its_capture_age()
    {
        var source = await Setup(hold: true); var date = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = source.User,
            WorkDate = date, AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 }); return await db.SaveChangesAsync(); });
        var captured = await Capture(Raw(source, Guid.NewGuid())); var original = Intent(Assert.Single(captured.IntentIds));
        f.Clock.Now = f.Clock.Now.AddDays(2);
        var preferences = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notice-preferences")).Json();
        await (await f.As(TestData.Alex).Patch("/api/v1/me/notice-preferences", new { revision = preferences.I("revision"), holdImmediateEmailWhenAway = false })).Json();
        var after = Intent(original.Id); Assert.Equal(original.CreatedAt, after.CreatedAt); Assert.Equal(original.SuppressionAnchorAt, after.SuppressionAnchorAt);
        Assert.Equal(NoticeIntentDisposition.Queued, after.Disposition); Assert.NotNull(after.DeliveryEmailId);
        Assert.Equal(original.OriginalBodyText, Email(after.DeliveryEmailId!.Value).BodyText);
        Assert.Equal(1, f.Db(db => db.Emails.Count(row => row.NoticeProtocolVersion == 1 && row.RenderedOccurrenceIds!.Contains(original.Id))));
    }

    [Theory]
    [InlineData(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted)]
    [InlineData(NoticeTransportKind.Graph, NoticeTransportOutcome.Accepted)]
    [InlineData(NoticeTransportKind.Log, NoticeTransportOutcome.Simulated)]
    [InlineData(NoticeTransportKind.Unknown, NoticeTransportOutcome.Unknown)]
    public async Task Recording_sender_outcomes_are_pinned_and_only_real_declared_success_counts_as_accepted(string kind, string outcome)
    {
        var source = await Setup(); var captured = await Capture(Raw(source, Guid.NewGuid())); var email = Email(Assert.Single(captured.EmailIds));
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, email.Id, kind)).Claim);
        // Claim persistence is observed through a different DbContext before the recording sender runs.
        Assert.Equal(NoticeTransportOutcome.InFlight, Email(email.Id).TransportOutcome); Assert.Equal(1, Email(email.Id).Attempts);
        var sender = new RecordingSender(kind); var observation = await NoticeEmailTransport.SendAsync(sender, claim, f.Clock, CancellationToken.None);
        Assert.Equal(outcome, observation.Outcome); Assert.Equal(1, sender.Calls); Assert.Equal(claim.Envelope.ToAddress, sender.Received!.ToAddress);
        Assert.NotSame(email, sender.Received); Assert.Equal(email.BodyText, sender.Received.BodyText);
        sender.Received.BodyText = "Synthetic sender mutation";
        var result = await Complete(claim, observation); Assert.True(result.Applied);
        var stored = Email(email.Id); Assert.Equal(email.BodyText, stored.BodyText); Assert.Equal(outcome, stored.TransportOutcome);
        Assert.Equal(outcome == NoticeTransportOutcome.Accepted, stored.ProviderAcceptedAt is not null);
        Assert.Equal(NoticeClaimDecision.AlreadyTerminal, (await Claim(source.User, email.Id, kind)).Decision);
        Assert.False((await Complete(claim, observation)).Applied);
    }

    [Fact]
    public async Task Default_sender_declaration_is_unknown_and_task_throw_is_sticky_uncertainty()
    {
        var source = await Setup(); var result = await Capture(Raw(source, Guid.NewGuid())); var email = Assert.Single(result.EmailIds);
        IEmailSender undeclared = new UndeclaredSender(); Assert.Equal(NoticeTransportKind.Unknown, undeclared.TransportKind);
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, email)).Claim);
        var sender = new RecordingSender(NoticeTransportKind.Smtp, throws: true);
        var observation = await NoticeEmailTransport.SendAsync(sender, claim, f.Clock, CancellationToken.None);
        Assert.Equal(NoticeTransportOutcome.Uncertain, observation.Outcome); Assert.Equal(NoticeFailureCertainty.MayHaveAccepted, observation.FailureCertainty);
        await Complete(claim, observation); var stored = Email(email); Assert.NotNull(stored.AcceptanceUncertainAt);
        Assert.Equal(NoticeClaimDecision.Uncertain, (await Claim(source.User, email)).Decision); Assert.Equal(1, sender.Calls);
        Assert.Equal(email, Intent(Assert.Single(result.IntentIds)).DeliveryEmailId);
        var lateFailure = observation with { Outcome = NoticeTransportOutcome.Failed, FailureCertainty = NoticeFailureCertainty.DefinitelyNotAccepted };
        await Complete(claim, lateFailure); Assert.Equal(stored.AcceptanceUncertainAt, Email(email).AcceptanceUncertainAt);
        Assert.Equal(NoticeTransportOutcome.Uncertain, Email(email).TransportOutcome);
    }

    [Fact]
    public async Task Concurrent_claims_reserve_one_invocation_and_orphan_recovery_keeps_a_matching_late_ack()
    {
        var source = await Setup(); var captured = await Capture(Raw(source, Guid.NewGuid())); var email = Assert.Single(captured.EmailIds);
        var results = await Task.WhenAll(Claim(source.User, email), Claim(source.User, email));
        var claim = Assert.Single(results.Where(value => value.Claim is not null)).Claim!;
        Assert.Equal(1, Email(email).Attempts); Assert.Contains(results, value => value.Decision == NoticeClaimDecision.InFlight);
        await Recipient(source.User, context => NotificationDelivery.ObserveOrphanedClaimAsync(context, email, claim.Token, claim.Version, f.Clock.Now));
        var uncertain = Email(email).AcceptanceUncertainAt; Assert.NotNull(uncertain);
        Assert.Equal(NoticeClaimDecision.Uncertain, (await Claim(source.User, email)).Decision);
        var late = new NoticeSendObservation(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, f.Clock.Now, f.Clock.Now, NoticeFailureCertainty.None);
        Assert.True((await Complete(claim, late)).Applied); Assert.Equal(uncertain, Email(email).AcceptanceUncertainAt);
        Assert.Equal(NoticeTransportOutcome.Accepted, Email(email).TransportOutcome); Assert.Equal(1, Email(email).Attempts);
    }

    [Fact]
    public async Task Stale_completion_and_changed_nonempty_address_cannot_send_or_rewrite_a_payload()
    {
        var source = await Setup(); var result = await Capture(Raw(source, Guid.NewGuid())); var email = Email(Assert.Single(result.EmailIds));
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, email.Id)).Claim);
        var accepted = new NoticeSendObservation(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, f.Clock.Now, f.Clock.Now, NoticeFailureCertainty.None);
        Assert.False((await Complete(claim with { Token = Guid.NewGuid() }, accepted)).Applied);
        Assert.Null(Email(email.Id).ProviderAcceptedAt);
        await Complete(claim, accepted with { Outcome = NoticeTransportOutcome.Failed, ProviderAcceptedAt = null, FailureCertainty = NoticeFailureCertainty.DefinitelyNotAccepted });
        f.Clock.Now = f.Clock.Now.AddHours(1);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(row => row.Id == source.User)).Email = "synthetic-changed@hub.test"; return await db.SaveChangesAsync(); });
        var declined = await Claim(source.User, email.Id, purpose: NoticeClaimPurpose.KnownFailureRetry);
        Assert.Equal(NoticeClaimDecision.Suppress, declined.Decision); Assert.Null(declined.Claim);
        Assert.Equal(NoticeRefusalReason.AddressChanged, Email(email.Id).LastError); Assert.Equal(email.ToAddress, Email(email.Id).ToAddress);
        Assert.Equal(NoticeIntentDisposition.AccessRemoved, Intent(Assert.Single(result.IntentIds)).Disposition); Assert.Equal(1, Email(email.Id).Attempts);
    }

    [Fact]
    public async Task Known_failure_exhaustion_uses_eight_reserved_invocations_without_changing_identity()
    {
        var source = await Setup(); var result = await Capture(Raw(source, Guid.NewGuid())); var email = Assert.Single(result.EmailIds);
        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, email, purpose: attempt == 1 ? NoticeClaimPurpose.Initial : NoticeClaimPurpose.KnownFailureRetry)).Claim);
            Assert.Equal(attempt, Email(email).Attempts);
            await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Failed, f.Clock.Now, null, NoticeFailureCertainty.DefinitelyNotAccepted));
            f.Clock.Now = f.Clock.Now.AddHours(5);
        }
        Assert.Equal(NoticeTransportOutcome.Exhausted, Email(email).TransportOutcome);
        Assert.Equal(NoticeIntentDisposition.Exhausted, Intent(Assert.Single(result.IntentIds)).Disposition);
        Assert.Equal(8, Email(email).Attempts); Assert.Equal(NoticeClaimDecision.AlreadyTerminal, (await Claim(source.User, email)).Decision);
    }

    [Fact]
    public async Task Actual_sender_change_after_safe_failure_suppresses_without_relabelling_replaying_or_spending_an_attempt()
    {
        var source = await Setup(); var captured = await Capture(Raw(source, Guid.NewGuid())); var id = Assert.Single(captured.EmailIds);
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, id)).Claim);
        await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Failed, f.Clock.Now, null, NoticeFailureCertainty.DefinitelyNotAccepted));
        f.Clock.Now = f.Clock.Now.AddHours(1);
        var result = await Claim(source.User, id, NoticeTransportKind.Graph, NoticeClaimPurpose.KnownFailureRetry);
        Assert.Null(result.Claim); Assert.Equal(NoticeClaimDecision.Suppress, result.Decision); Assert.Equal("TransportChanged", result.ReasonCode);
        var email = Email(id); Assert.Equal(NoticeTransportKind.Smtp, email.TransportKind); Assert.Equal(1, email.Attempts);
        Assert.Equal(NoticeTransportOutcome.SuppressedBeforeSend, email.TransportOutcome); Assert.Null(email.ProviderAcceptedAt);
        Assert.Equal(id, Intent(Assert.Single(captured.IntentIds)).DeliveryEmailId);
        Assert.Equal(NoticeIntentDisposition.AccessRemoved, Intent(Assert.Single(captured.IntentIds)).Disposition);
        Assert.Equal(NoticeClaimDecision.AlreadyTerminal, (await Claim(source.User, id)).Decision);
    }

    [Fact]
    public async Task Queued_bulk_partial_opt_out_preserves_eligible_sibling_and_bounds_checks_without_replacing_failed_payload()
    {
        var source = await BulkPair(); var id = Assert.Single(source.Captured.IntentIds); var mailId = Assert.Single(source.Captured.EmailIds);
        var captured = Intent(id); var origin = NoticeDeliveryProof.Decode(captured.BulkPolicyJson!, source.User, source.Command).Origin;
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, mailId)).Claim);
        await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Failed, f.Clock.Now, null, NoticeFailureCertainty.DefinitelyNotAccepted));
        var originalMail = Email(mailId);
        // Simulate a newly committed policy not observed by this queue; the claim must independently recheck it.
        await f.DbAsync(db => Tx.Run(db, async () =>
        {
            await NotificationDelivery.LockRecipientAsync(db, source.User);
            var row = await db.NotificationPreferences.SingleOrDefaultAsync(value => value.UserId == source.User && value.EventType == NotificationEvents.TaskAssigned);
            if (row is null) db.NotificationPreferences.Add(row = new() { UserId = source.User, EventType = NotificationEvents.TaskAssigned });
            row.InApp = true; row.Email = false; await db.SaveChangesAsync(); return 0;
        }));
        f.Clock.Now = f.Clock.Now.AddMinutes(3);
        var refused = await Claim(source.User, mailId, purpose: NoticeClaimPurpose.KnownFailureRetry);
        Assert.Equal(NoticeClaimDecision.Suppress, refused.Decision); Assert.Null(refused.Claim);
        var after = Intent(id); var proof = NoticeDeliveryProof.Decode(after.BulkPolicyJson!, source.User, source.Command);
        Assert.Equal(NoticeIntentDisposition.ChannelOff, proof.Progress[0].State); Assert.Equal(NoticeRefusalReason.ChannelOff, proof.Progress[0].RefusalReason);
        Assert.Equal(NoticeIntentDisposition.HeldAway, proof.Progress[1].State); Assert.Null(proof.Progress[1].EmailId);
        Assert.Null(after.DeliveryEmailId); Assert.Equal(NoticeIntentDisposition.Mixed, after.Disposition); Assert.True(after.NextPolicyCheckAt > f.Clock.Now);
        Assert.Equal(captured.CreatedAt, after.CreatedAt); Assert.Equal(captured.SuppressionAnchorAt, after.SuppressionAnchorAt);
        Assert.Equal(origin.OriginHash, proof.Origin.OriginHash); Assert.Equal(new[] { 1, 2 }, proof.Origin.EmailCohortKeys.ToArray());
        for (var check = 1; check <= 8; check++)
        {
            f.Clock.Now = Intent(id).NextPolicyCheckAt!.Value;
            var sweep = await Recipient(source.User, context => NotificationDelivery.ReconsiderPendingAsync(context, [id], f.Clock.Now));
            Assert.Equal(1, sweep.Examined); Assert.Equal(1, sweep.Parked);
            var parked = Intent(id); var progress = NoticeDeliveryProof.Decode(parked.BulkPolicyJson!, source.User, source.Command).Progress;
            Assert.Equal(check, parked.PolicyChecksInDay); Assert.True(parked.NextPolicyCheckAt > f.Clock.Now);
            Assert.Equal(NoticeIntentDisposition.ChannelOff, progress[0].State); Assert.Equal(NoticeIntentDisposition.HeldAway, progress[1].State);
            Assert.Null(progress[1].EmailId); Assert.Null(parked.DeliveryEmailId);
            Assert.Empty(await f.DbAsync(db => NotificationDelivery.SelectPendingAsync(db, f.Clock.Now)));
        }
        Assert.Equal(DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(1), DateOnly.FromDateTime(Intent(id).NextPolicyCheckAt!.Value.UtcDateTime));
        var retained = Email(mailId);
        Assert.Equal(1, retained.Attempts); Assert.Equal(NoticeTransportKind.Smtp, retained.TransportKind); Assert.Equal(NoticeTransportOutcome.SuppressedBeforeSend, retained.TransportOutcome);
        Assert.Equal(originalMail.ToAddress, retained.ToAddress); Assert.Equal(originalMail.Subject, retained.Subject); Assert.Equal(originalMail.BodyText, retained.BodyText);
        Assert.Equal(originalMail.BulkRenderedMembershipJson, retained.BulkRenderedMembershipJson);
        Assert.Equal(originalMail.NoticeClaimToken, retained.NoticeClaimToken); Assert.Equal(originalMail.NoticeClaimVersion, retained.NoticeClaimVersion);
        Assert.Equal(1, f.Db(db => db.Emails.Count(row => row.NoticeSlotId == null && row.RenderedOccurrenceIds != null && row.RenderedOccurrenceIds.Contains(id))));
        // A later eligible digest may reserve the exact remaining key without touching the old invocation budget.
        f.Clock.Now = Intent(id).NextPolicyCheckAt!.Value.AddHours(8);
        var digest = await Recipient(source.User, async context =>
        {
            var slot = Assert.IsType<NoticeDeliverySlot>(await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now));
            return await NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test");
        });
        Assert.NotNull(digest.EmailId); Assert.NotEqual(mailId, digest.EmailId!.Value);
        var reserved = Intent(id); var remainder = NoticeDeliveryProof.Decode(reserved.BulkPolicyJson!, source.User, source.Command);
        var map = Assert.Single(NoticeDeliveryProof.DecodeRenderedBulkMap(Email(digest.EmailId.Value).BulkRenderedMembershipJson, source.User,
            [id], new Dictionary<Guid, NoticeBulkProof> { [id] = remainder }));
        Assert.Equal(new[] { 2 }, map.ObligationKeys.ToArray()); Assert.Equal(NoticeIntentDisposition.ChannelOff, remainder.Progress[0].State);
        Assert.Equal(NoticeIntentDisposition.ReservedForDigest, remainder.Progress[1].State); Assert.Equal(origin.OriginHash, remainder.Origin.OriginHash);
        Assert.Equal(captured.CreatedAt, reserved.CreatedAt); Assert.Equal(captured.SuppressionAnchorAt, reserved.SuppressionAnchorAt); Assert.Equal(1, Email(mailId).Attempts);
    }

    [Fact]
    public async Task Held_immediate_partial_opt_out_and_hold_release_leave_only_the_eligible_subset_parked()
    {
        var source = await BulkPair(); var id = Assert.Single(source.Captured.IntentIds); var mailId = Assert.Single(source.Captured.EmailIds);
        var captured = Intent(id); var original = Email(mailId);
        await (await f.As(TestData.Alex).Patch("/api/v1/me/notice-preferences", new { revision = 0, holdImmediateEmailWhenAway = true })).Json();
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = source.User,
            WorkDate = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 }); return await db.SaveChangesAsync(); });
        var held = await Claim(source.User, mailId); Assert.Equal(NoticeClaimDecision.HoldAway, held.Decision); Assert.Null(held.Claim);
        Assert.Equal(mailId, Intent(id).DeliveryEmailId); Assert.Equal(0, Email(mailId).Attempts);
        (await f.As(TestData.Alex).Put($"/api/v1/me/preferences/events/{NotificationEvents.TaskAssigned}", new { app = true, email = false })).EnsureSuccessStatusCode();
        var preferences = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notice-preferences")).Json();
        await (await f.As(TestData.Alex).Patch("/api/v1/me/notice-preferences", new { revision = preferences.I("revision"), holdImmediateEmailWhenAway = false })).Json();
        var after = Intent(id); var proof = NoticeDeliveryProof.Decode(after.BulkPolicyJson!, source.User, source.Command);
        Assert.Equal(NoticeIntentDisposition.ChannelOff, proof.Progress[0].State); Assert.Equal(NoticeRefusalReason.ChannelOff, proof.Progress[0].RefusalReason);
        Assert.Equal(NoticeIntentDisposition.HeldAway, proof.Progress[1].State); Assert.Null(proof.Progress[1].EmailId); Assert.Null(after.DeliveryEmailId);
        Assert.True(after.NextPolicyCheckAt > f.Clock.Now); Assert.Equal(captured.CreatedAt, after.CreatedAt); Assert.Equal(captured.SuppressionAnchorAt, after.SuppressionAnchorAt);
        var old = Email(mailId); Assert.Equal(NoticeTransportOutcome.SuppressedBeforeSend, old.TransportOutcome); Assert.Equal(0, old.Attempts);
        Assert.Equal(original.Subject, old.Subject); Assert.Equal(original.BodyText, old.BodyText); Assert.Equal(original.BulkRenderedMembershipJson, old.BulkRenderedMembershipJson);
        Assert.Equal(NoticeClaimDecision.AlreadyTerminal, (await Claim(source.User, mailId)).Decision);
        f.Clock.Now = after.NextPolicyCheckAt!.Value;
        var sweep = await Recipient(source.User, context => NotificationDelivery.ReconsiderPendingAsync(context, [id], f.Clock.Now));
        Assert.Equal(1, sweep.Parked); Assert.True(Intent(id).NextPolicyCheckAt > f.Clock.Now); Assert.Null(Intent(id).DeliveryEmailId);
        Assert.Equal(0, Email(mailId).Attempts); Assert.Equal(1, f.Db(db => db.Emails.Count(row => row.NoticeSlotId == null
            && row.RenderedOccurrenceIds != null && row.RenderedOccurrenceIds.Contains(id))));
    }

    [Fact]
    public async Task Selected_scalar_receipt_still_decodes_a_digest_containing_another_apps_bulk_intent()
    {
        var source = await Setup(hold: true); var date = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = source.User,
            WorkDate = date, AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 }); return await db.SaveChangesAsync(); });
        var correlation = Guid.NewGuid();
        var bulk = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var result = await Stage(db, [Raw(source, correlation)], new(correlation, source.Actor, [new(source.Task, source.Project)]));
            await db.SaveChangesAsync(); return result;
        }));
        var scalar = await Capture(Raw(source, Guid.NewGuid(), code: NotificationEvents.ReviewerSet));
        Assert.Empty(bulk.EmailIds); Assert.Empty(scalar.EmailIds);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var digest = await Recipient(source.User, async context =>
        {
            var slot = Assert.IsType<NoticeDeliverySlot>(await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now));
            return await NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test");
        });
        Assert.NotNull(digest.EmailId);
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, digest.EmailId!.Value)).Claim);
        Assert.Equal(2, claim.Membership.IntentIds.Length); Assert.Single(claim.Membership.BulkMemberships);
        await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, f.Clock.Now, f.Clock.Now, NoticeFailureCertainty.None));
        var intent = Intent(Assert.Single(scalar.IntentIds));
        var receipt = Assert.Single(await f.DbAsync(db => NotificationDelivery.ReadAggregatesAsync(db, source.User, [(intent.NotificationId!.Value, 1)])));
        Assert.Equal("Emailed", receipt.State); Assert.Equal(1, receipt.RealAcceptedCount); Assert.NotNull(receipt.AcceptedAt);
    }

    [Fact]
    public async Task Exact_bulk_app_cohort_cannot_be_proven_by_equal_changed_task_totals()
    {
        var source = await Setup(); var correlation = Guid.NewGuid();
        var calls = new[] { Raw(source, correlation), Raw(source, correlation, 2, NotificationEvents.TaskBlocked) };
        var captured = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var result = await Stage(db, calls, new(correlation, source.Actor, [new(source.Task, source.Project)]));
            await db.SaveChangesAsync(); return result;
        }));
        var intent = Intent(Assert.Single(captured.IntentIds)); var proof = NoticeDeliveryProof.Decode(intent.BulkPolicyJson!, source.User, correlation);
        Assert.Equal(2, proof.Origin.AppCohortKeys.Length); Assert.Single(proof.Origin.EmailCohortKeys);
        Assert.Single(proof.Origin.AppTaskIds); Assert.Single(proof.Origin.EmailTaskIds); Assert.Null(intent.NotificationId);
        var email = Assert.Single(captured.EmailIds); var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, email)).Claim);
        await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, f.Clock.Now, f.Clock.Now, NoticeFailureCertainty.None));
        var row = f.Db(db => db.Notifications.AsNoTracking().Single(value => value.Id == proof.Origin.AppNotificationId));
        var aggregate = Assert.Single(await f.DbAsync(db => NotificationDelivery.ReadAggregatesAsync(db, source.User, [(row.Id, row.Count)])));
        Assert.Equal("Partial", aggregate.State); Assert.Equal(0, aggregate.RealAcceptedCount); Assert.Null(aggregate.AcceptedAt);
    }

    [Fact]
    public async Task Accepted_captured_ordinal_does_not_invent_missing_collapsed_history_or_foreign_visibility()
    {
        var source = await Setup(); var captured = await Capture(Raw(source, Guid.NewGuid())); var intent = Intent(Assert.Single(captured.IntentIds));
        var claim = Assert.IsType<NoticeSendClaim>((await Claim(source.User, Assert.Single(captured.EmailIds))).Claim);
        await Complete(claim, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, f.Clock.Now, f.Clock.Now, NoticeFailureCertainty.None));
        await f.DbAsync(async db => { (await db.Notifications.SingleAsync(row => row.Id == intent.NotificationId)).Count = 2; return await db.SaveChangesAsync(); });
        var read = Assert.Single(await f.DbAsync(db => NotificationDelivery.ReadAggregatesAsync(db, source.User, [(intent.NotificationId!.Value, 2)])));
        Assert.Equal("Partial", read.State); Assert.Equal("Partial", read.HistoryCoverage); Assert.Equal(1, read.RealAcceptedCount); Assert.Null(read.AcceptedAt);
        Assert.Empty(await f.DbAsync(db => NotificationDelivery.ReadAggregatesAsync(db, data.User(TestData.Jill), [(intent.NotificationId!.Value, 2)])));
    }

    [Fact]
    public async Task Every_enhanced_marker_is_excluded_by_the_same_legacy_definition_before_limiting()
    {
        var source = await Setup();
        var ordinary = new EmailMessage { ToAddress = "synthetic@hub.test", Subject = "Synthetic legacy mail", BodyText = "Synthetic", Kind = "Security",
            RequiredProjectIds = [Guid.NewGuid()], RequiredPlanningEntryIds = [Guid.NewGuid()], CreatedAt = f.Clock.Now };
        Assert.True(NotificationDelivery.IsLegacyEmail(ordinary));
        EmailMessage[] marked = [new() { NoticeProtocolVersion = 1 }, new() { NoticeSlotId = Guid.NewGuid() }, new() { NoticeCompositionVersion = 1 },
            new() { RenderedOccurrenceIds = [] }, new() { BulkRenderedMembershipJson = "[]" }, new() { NoticeDigestPolicyJson = "{}" },
            new() { NoticeClaimToken = Guid.NewGuid() }, new() { NoticeClaimVersion = 1 }, new() { NoticeClaimedAt = f.Clock.Now },
            new() { TransportKind = NoticeTransportKind.Unknown }, new() { TransportOutcome = NoticeTransportOutcome.Unknown },
            new() { TransportObservedAt = f.Clock.Now }, new() { ProviderAcceptedAt = f.Clock.Now }, new() { AcceptanceUncertainAt = f.Clock.Now }];
        Assert.All(marked, row => Assert.False(NotificationDelivery.IsLegacyEmail(row)));
        Assert.Single(NotificationDelivery.LegacyEmails(marked.Append(ordinary).AsQueryable()).Take(100));
        var malformed = new EmailMessage { ToAddress = "synthetic@hub.test", Subject = "Synthetic malformed marker", BodyText = "Synthetic",
            NoticeProtocolVersion = 1, UserId = source.User, Kind = "Immediate", RenderedOccurrenceIds = [Guid.Empty], CreatedAt = f.Clock.Now };
        await f.DbAsync(async db => { db.Emails.Add(malformed); return await db.SaveChangesAsync(); });
        await f.DbAsync(db => Tx.Run(db, async () =>
        { var result = await NotificationDelivery.QuarantineMalformedAsync(db, malformed.Id, f.Clock.Now); await db.SaveChangesAsync(); return result; }));
        Assert.NotNull(Email(malformed.Id).SuppressedAt); Assert.Equal(0, Email(malformed.Id).Attempts);
    }

    [Fact]
    public async Task Context_requires_owners_live_ReadCommitted_transaction()
    {
        var source = await Setup();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(db => NotificationDelivery.LockRecipientAsync(db, source.User)));
        await f.DbAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);
            await Assert.ThrowsAsync<InvalidOperationException>(() => NotificationDelivery.LockRecipientAsync(db, source.User)); return 0;
        });
        NoticeRecipientContext? old = null;
        await Recipient(source.User, async context => { old = context; await Task.CompletedTask; return 0; });
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => NotificationDelivery.ReadRecipientPolicyAsync(old!));
    }

    sealed class RecordingSender(string kind, bool throws = false) : IEmailSender
    {
        public string TransportKind => kind;
        public int Calls { get; private set; }
        public EmailMessage? Received { get; private set; }
        public Task Send(EmailMessage message, CancellationToken ct)
        { Calls++; Received = message; return throws ? Task.FromException(new IOException("Synthetic ambiguous failure")) : Task.CompletedTask; }
    }
    sealed class UndeclaredSender : IEmailSender
    { public Task Send(EmailMessage message, CancellationToken ct) => Task.CompletedTask; }
}
