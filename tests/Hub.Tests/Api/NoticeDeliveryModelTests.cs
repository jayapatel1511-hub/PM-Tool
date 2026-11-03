using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Api.Records;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

// Private EF/PostgreSQL guard checks only. No capture authorization, worker, ACL or external delivery/acceptance proof.
public sealed class NoticeDeliveryModelTests : IDisposable
{
    readonly HubFactory f = new();
    public NoticeDeliveryModelTests() => f.Clock.SetDate(2026, 11, 2);
    public void Dispose() => f.Dispose();
    DateTimeOffset Now => f.Clock.GetUtcNow();
    async Task<Guid> Recipient() => (await (await f.As($"synthetic-notice-model-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
    NoticeDeliveryIntent Intent(Guid user) => new() { UserId = user, OccurrenceKey = "synthetic:" + Guid.NewGuid(), EventType = NotificationEvents.TaskAssigned,
        OriginalSubject = "Synthetic captured subject", OriginalBodyText = "Synthetic captured body", SuppressionKey = "synthetic-suppression:" + Guid.NewGuid(),
        CreatedAt = Now, UpdatedAt = Now };
    NoticeDeliverySlot Digest(Guid user) => new() { UserId = user, Kind = NoticeSlotKind.Digest, LocalDate = new(2026, 11, 2),
        OrgTimeZoneId = "America/Halifax", ScheduledLocalDate = new(2026, 11, 2), ScheduledLocalTime = "09:00", ScheduledAtUtc = Now,
        FrequencyAtCreation = NoticeDigestFrequency.Daily, WindowFrom = Now.AddDays(-1), WindowThrough = Now, CreatedAt = Now, UpdatedAt = Now };
    EmailMessage Immediate(Guid user, Guid occurrence) => new() { UserId = user, ToAddress = "synthetic-recipient@hub.test", Subject = "Synthetic queued subject",
        BodyText = "Synthetic immutable queued body", CreatedAt = Now, Kind = "Immediate", NoticeProtocolVersion = 1, RenderedOccurrenceIds = [occurrence] };
    Task<int> Store(params object[] rows) => f.DbAsync(async db => { db.AddRange(rows); return await db.SaveChangesAsync(); });
    string PublicEffects() => f.Db(db => JsonSerializer.Serialize(new {
        Audit = db.ActivityLog.AsNoTracking().OrderBy(row => row.Id).ToArray(), Outbox = db.Outbox.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Assignments = db.WorkAssignmentOccurrences.AsNoTracking().OrderBy(row => row.Token).ToArray()
    }));
    async Task DatabaseRefuses(Action<HubDb> change, string sqlState = PostgresErrorCodes.CheckViolation)
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { change(db); return await db.SaveChangesAsync(); }));
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }
    async Task GuardRefuses<T>(Guid id, Action<T> change) where T : class
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => {
            var row = await db.Set<T>().FindAsync(id); Assert.NotNull(row);
            change(row); return await db.SaveChangesAsync();
        }));
    }
    NoticeBulkProof Proof(Guid user)
    {
        // Same minimal typed source shape as NoticeDeliveryProofTests, with an email-only cohort. These source IDs are
        // codec inputs, not a claim that a business command authorized them; trusted capture binding is later helper work.
        var task = Guid.NewGuid(); var project = Guid.NewGuid(); var actor = Guid.NewGuid();
        var obligation = new NoticeBulkObligation(1, NotificationEvents.TaskAssigned, ItemType.Task, task, project, [project], [], actor,
            true, false, true, null, $"{NotificationEvents.TaskAssigned}:{task:D}:{user:D}", Now, NoticeBulkCoverage.Task);
        return NoticeDeliveryProof.Admit(new(1, NoticeProofLimits.Kind, Guid.NewGuid(), actor, user, Now,
            [new(task, project)], [obligation], [], [1], null, null, [], [task]));
    }

    [Fact]
    public async Task Nullable_private_fragments_preserve_old_self_and_email_shapes_without_business_audit_or_assignment_publication()
    {
        var email = $"synthetic-notice-defaults-{Guid.NewGuid():N}@hub.test";
        var self = await (await f.As(email).GetAsync("/api/v1/me")).Json(); var user = self.G("id");
        var before = PublicEffects();
        await f.DbAsync(async db => { db.UserSettings.Add(new UserSetting { UserId = user }); return await db.SaveChangesAsync(); });
        var settings = f.Db(db => db.UserSettings.AsNoTracking().Single(row => row.UserId == user));
        Assert.Null(settings.NoticeDigestFrequency); Assert.Null(settings.HoldImmediateEmailWhenAway); Assert.Null(settings.TimeReminderEnabled);
        Assert.Null(settings.TimeReminderWeekday); Assert.Null(settings.TimeReminderTimeLocal); Assert.Null(settings.NoticePreferencesRevision);
        Assert.True(settings.HoldDigestWhenAway);
        await f.DbAsync(async db => { var row = await db.UserSettings.SingleAsync(value => value.UserId == user);
            row.NoticeDigestFrequency = NoticeDigestFrequency.Weekly; row.HoldImmediateEmailWhenAway = true;
            row.TimeReminderEnabled = true; row.TimeReminderWeekday = 2; row.TimeReminderTimeLocal = "09:15"; row.NoticePreferencesRevision = 1;
            return await db.SaveChangesAsync(); });
        Assert.True(JsonNode.DeepEquals(self, await (await f.As(email).GetAsync("/api/v1/me")).Json()));
        var raw = JsonSerializer.SerializeToNode(f.Db(db => db.UserSettings.AsNoTracking().Single(row => row.UserId == user)), JsonOpts.Web)!;
        foreach (var key in new[] { "noticeDigestFrequency", "holdImmediateEmailWhenAway", "timeReminderEnabled", "timeReminderWeekday", "timeReminderTimeLocal", "noticePreferencesRevision" })
            Assert.False(raw.AsObject().ContainsKey(key));
        var intent = Intent(user); var slot = Digest(user); var legacy = new EmailMessage { Subject = "Synthetic legacy", BodyText = "Legacy body", ToAddress = "legacy@hub.test", Kind = "Weekly", CreatedAt = Now, SentAt = Now };
        await Store(intent, slot, legacy); Assert.Null(f.Db(db => db.Emails.Single(row => row.Id == legacy.Id).ProviderAcceptedAt));
        foreach (var type in new[] { typeof(NoticeDeliveryIntent), typeof(NoticeDeliverySlot) }) {
            Assert.False(typeof(IAuditable).IsAssignableFrom(type)); Assert.False(typeof(Audited).IsAssignableFrom(type));
            Assert.DoesNotContain(EntityRegistry.Entries, entry => entry.ClrType == type);
        }
        var privateMail = Immediate(user, intent.Id); privateMail.NoticeClaimToken = Guid.NewGuid(); privateMail.NoticeClaimVersion = 1;
        privateMail.NoticeClaimedAt = Now; privateMail.TransportKind = NoticeTransportKind.Graph; privateMail.TransportOutcome = NoticeTransportOutcome.InFlight;
        privateMail.TransportObservedAt = Now; privateMail.AcceptanceUncertainAt = Now; privateMail.BulkRenderedMembershipJson = "[]";
        var oldKeys = JsonSerializer.SerializeToNode(legacy, JsonOpts.Web)!.AsObject().Select(pair => pair.Key).Order().ToArray();
        var mailJson = JsonSerializer.SerializeToNode(privateMail, JsonOpts.Web)!.AsObject();
        Assert.Equal(oldKeys, mailJson.Select(pair => pair.Key).Order());
        foreach (var key in new[] { "noticeProtocolVersion", "noticeSlotId", "noticeCompositionVersion", "renderedOccurrenceIds", "bulkRenderedMembershipJson", "noticeDigestPolicyJson",
            "noticeClaimToken", "noticeClaimVersion", "noticeClaimedAt", "transportKind", "transportOutcome", "transportObservedAt", "providerAcceptedAt", "acceptanceUncertainAt" })
            Assert.False(mailJson.ContainsKey(key));
        Assert.Equal(before, PublicEffects());
    }

    [Fact]
    public async Task PostgreSQL_enforces_private_keys_foreign_keys_and_disposition_slot_preference_counter_primitives()
    {
        var user = await Recipient(); var other = await Recipient(); var intent = Intent(user); var slot = Digest(user); await Store(intent, slot);
        var duplicate = Intent(user); duplicate.OccurrenceKey = intent.OccurrenceKey;
        await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(duplicate), PostgresErrorCodes.UniqueViolation);
        var missingUser = Intent(Guid.NewGuid()); await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(missingUser), PostgresErrorCodes.ForeignKeyViolation);
        var missingSlotUser = Digest(Guid.NewGuid()); await DatabaseRefuses(db => db.NoticeDeliverySlots.Add(missingSlotUser), PostgresErrorCodes.ForeignKeyViolation);
        var badDisposition = Intent(user); badDisposition.Disposition = "NotAState"; await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(badDisposition));
        var badOrdinal = Intent(user); badOrdinal.NotificationCountAtCapture = 0; await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(badOrdinal));
        var badPolicyBudget = Intent(user); badPolicyBudget.PolicyCheckLocalDate = new(2026, 11, 2); badPolicyBudget.PolicyChecksInDay = 9;
        await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(badPolicyBudget));
        var notification = new Notification { UserId = user, EventType = NotificationEvents.TaskAssigned, Title = "Synthetic collapse", Count = 4, CreatedAt = Now, UpdatedAt = Now }; await Store(notification);
        var captured = Intent(user); captured.NotificationId = notification.Id; captured.NotificationCountAtCapture = 4; await Store(captured);
        var sameOrdinal = Intent(user); sameOrdinal.NotificationId = notification.Id; sameOrdinal.NotificationCountAtCapture = 4;
        await DatabaseRefuses(db => db.NoticeDeliveryIntents.Add(sameOrdinal), PostgresErrorCodes.UniqueViolation);
        var duplicateSlot = Digest(user); await DatabaseRefuses(db => db.NoticeDeliverySlots.Add(duplicateSlot), PostgresErrorCodes.UniqueViolation);
        foreach (var alter in new Action<NoticeDeliverySlot>[] { row => row.Kind = "NotAKind", row => row.WindowFrom = row.WindowThrough!.Value.AddMinutes(1),
            row => row.CompositionVersion = 4, row => row.ExternalAttempts = 9 }) {
            var invalid = Digest(other); alter(invalid); await DatabaseRefuses(db => db.NoticeDeliverySlots.Add(invalid));
        }
        var reminder = Digest(other); reminder.Kind = NoticeSlotKind.Reminder; reminder.FrequencyAtCreation = null; reminder.WindowFrom = reminder.WindowThrough = null;
        reminder.CompositionVersion = 1; await DatabaseRefuses(db => db.NoticeDeliverySlots.Add(reminder));
        await DatabaseRefuses(db => db.UserSettings.Add(new UserSetting { UserId = other, TimeReminderEnabled = true, TimeReminderWeekday = 2 }));
        // A recipient FK establishes existence only. Cross-recipient membership/access is a capture-helper obligation.
        Assert.Equal(2, f.Db(db => db.NoticeDeliveryIntents.Count(row => row.UserId == user)));
        Assert.Equal(1, f.Db(db => db.NoticeDeliverySlots.Count(row => row.UserId == user)));
    }

    [Fact]
    public async Task Email_protocol_separates_legacy_single_occurrence_digest_claim_and_real_transport_acceptance_metadata()
    {
        var user = await Recipient(); var intent = Intent(user); var slot = Digest(user); slot.CompositionVersion = 1; slot.State = NoticeSlotState.Queued;
        await Store(intent, slot);
        foreach (var kind in new[] { "Immediate", "PlanningEntryChanged", "PlanningWithdrawal" })
        {
            var single = Immediate(user, intent.Id); single.Kind = kind; await Store(single);
            var current = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == single.Id));
            Assert.Equal(kind, current.Kind); Assert.Single(current.RenderedOccurrenceIds!); Assert.Null(current.NoticeSlotId); Assert.Null(current.NoticeCompositionVersion);
            var multiple = Immediate(user, intent.Id); multiple.Kind = kind; multiple.RenderedOccurrenceIds = [intent.Id, Guid.NewGuid()];
            await DatabaseRefuses(db => db.Emails.Add(multiple));
            var wrongSlot = Immediate(user, intent.Id); wrongSlot.Kind = kind; wrongSlot.NoticeSlotId = slot.Id; wrongSlot.NoticeCompositionVersion = 1;
            await DatabaseRefuses(db => db.Emails.Add(wrongSlot));
        }
        var partialClaim = Immediate(user, intent.Id); partialClaim.NoticeClaimToken = Guid.NewGuid(); await DatabaseRefuses(db => db.Emails.Add(partialClaim));
        var noSlot = Immediate(user, intent.Id); noSlot.Kind = "Digest"; await DatabaseRefuses(db => db.Emails.Add(noSlot));
        var missingSlot = Immediate(user, intent.Id); missingSlot.Kind = "Digest"; missingSlot.NoticeSlotId = Guid.NewGuid(); missingSlot.NoticeCompositionVersion = 1;
        await DatabaseRefuses(db => db.Emails.Add(missingSlot), PostgresErrorCodes.ForeignKeyViolation);
        var digest = Immediate(user, intent.Id); digest.Kind = "Digest"; digest.NoticeSlotId = slot.Id; digest.NoticeCompositionVersion = 1; await Store(digest);
        var duplicate = Immediate(user, intent.Id); duplicate.Kind = "Digest"; duplicate.NoticeSlotId = slot.Id; duplicate.NoticeCompositionVersion = 1;
        await DatabaseRefuses(db => db.Emails.Add(duplicate), PostgresErrorCodes.UniqueViolation);
        foreach (var transport in new[] { NoticeTransportKind.Log, NoticeTransportKind.Unknown }) {
            var invalid = AcceptedMetadata(user, intent.Id, transport); await DatabaseRefuses(db => db.Emails.Add(invalid));
        }
        var accepted = AcceptedMetadata(user, intent.Id, NoticeTransportKind.Graph); await Store(accepted);
        var stored = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == accepted.Id));
        Assert.Equal(NoticeTransportKind.Graph, stored.TransportKind); Assert.Equal(NoticeTransportOutcome.Accepted, stored.TransportOutcome);
        Assert.Equal(Now, stored.ProviderAcceptedAt); Assert.NotNull(stored.NoticeClaimToken); Assert.Equal(1, stored.NoticeClaimVersion);
        await GuardRefuses<EmailMessage>(accepted.Id, row => row.ProviderAcceptedAt = Now.AddMinutes(1));
        await GuardRefuses<EmailMessage>(accepted.Id, row => row.NoticeClaimToken = Guid.NewGuid());
        await GuardRefuses<EmailMessage>(accepted.Id, row => row.Attempts++);
        await f.DbAsync(async db => { var row = await db.NoticeDeliveryIntents.SingleAsync(value => value.Id == intent.Id);
            row.SuppressionAnchorAt = Now; row.Disposition = NoticeIntentDisposition.Accepted; row.DeliveryEmailId = accepted.Id; return await db.SaveChangesAsync(); });
        string AcceptedBytes() => f.Db(db => JsonSerializer.Serialize(db.Emails.Where(row => row.Id == accepted.Id).Select(row => new {
            row.Subject, row.BodyText, row.BodyHtml, row.RenderedOccurrenceIds, row.ProviderAcceptedAt, row.TransportKind, row.TransportOutcome,
            row.TransportObservedAt, row.NoticeClaimToken, row.NoticeClaimVersion, row.NoticeClaimedAt, row.Attempts }).Single()));
        var acceptedBytes = AcceptedBytes();
        await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => { row.Disposition = NoticeIntentDisposition.AccessRemoved; row.DeliveryEmailId = digest.Id; });
        await f.DbAsync(async db => { (await db.NoticeDeliveryIntents.SingleAsync(row => row.Id == intent.Id)).Disposition = NoticeIntentDisposition.AccessRemoved; return await db.SaveChangesAsync(); });
        var withdrawn = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.Id == intent.Id));
        Assert.Equal(NoticeIntentDisposition.AccessRemoved, withdrawn.Disposition); Assert.Equal(accepted.Id, withdrawn.DeliveryEmailId); Assert.Equal(acceptedBytes, AcceptedBytes());
        await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => row.Disposition = NoticeIntentDisposition.Queued);
        await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => row.DeliveryEmailId = digest.Id);
        await GuardRefuses<EmailMessage>(accepted.Id, row => row.Subject += "rewritten after withdrawal"); Assert.Equal(acceptedBytes, AcceptedBytes());
        // This is an admissible metadata fixture, never evidence of actual Graph/SMTP acceptance or inbox delivery.
    }

    EmailMessage AcceptedMetadata(Guid user, Guid occurrence, string transport)
    {
        var row = Immediate(user, occurrence); row.NoticeClaimToken = Guid.NewGuid(); row.NoticeClaimVersion = 1; row.NoticeClaimedAt = Now;
        row.Attempts = 1;
        row.TransportKind = transport; row.TransportOutcome = NoticeTransportOutcome.Accepted; row.TransportObservedAt = Now; row.ProviderAcceptedAt = Now;
        return row;
    }

    [Fact]
    public async Task Captured_intent_identity_content_anchor_and_typed_bulk_origin_are_immutable_but_valid_progress_can_advance()
    {
        var user = await Recipient(); var proof = Proof(user); var intent = Intent(user); intent.SuppressionAnchorAt = Now;
        intent.BulkPolicyJson = NoticeDeliveryProof.Encode(proof); await Store(intent);
        foreach (var alter in new Action<NoticeDeliveryIntent>[] { row => row.OccurrenceKey += "changed", row => row.UserId = Guid.NewGuid(),
            row => row.EventType = NotificationEvents.DueDateChanged, row => row.OriginalSubject += "changed", row => row.OriginalBodyText += "changed",
            row => row.RequiredProjectIds = [Guid.NewGuid()], row => row.NotificationCountAtCapture = 7, row => row.SuppressionAnchorAt = Now.AddMinutes(1) })
            await GuardRefuses(intent.Id, alter);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { db.NoticeDeliveryIntents.Remove(await db.NoticeDeliveryIntents.SingleAsync(row => row.Id == intent.Id)); return await db.SaveChangesAsync(); }));
        var email = Immediate(user, intent.Id); await Store(email);
        var progress = NoticeDeliveryProof.WithProgress(proof, [new(1, NoticeIntentDisposition.Queued, Now.AddMinutes(1), EmailId: email.Id)]);
        await f.DbAsync(async db => { var row = await db.NoticeDeliveryIntents.SingleAsync(value => value.Id == intent.Id);
            row.BulkPolicyJson = NoticeDeliveryProof.Encode(progress); row.Disposition = NoticeIntentDisposition.Queued; row.DeliveryEmailId = email.Id;
            row.UpdatedAt = Now.AddMinutes(1); return await db.SaveChangesAsync(); });
        var current = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.Id == intent.Id));
        Assert.True(NoticeDeliveryProof.HasSameImmutableOrigin(intent.BulkPolicyJson, current.BulkPolicyJson!, user));
        Assert.Equal(NoticeIntentDisposition.Queued, NoticeDeliveryProof.Decode(current.BulkPolicyJson!, user, proof.Origin.CommandId).Progress[0].State);
        var differentOrigin = NoticeDeliveryProof.Admit(proof.Origin with { CommandId = Guid.NewGuid(), OriginHash = "" });
        await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => row.BulkPolicyJson = NoticeDeliveryProof.Encode(differentOrigin));
        var tampered = JsonNode.Parse(current.BulkPolicyJson!)!; tampered["origin"]!["originalActorId"] = Guid.NewGuid().ToString();
        await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => row.BulkPolicyJson = tampered.ToJsonString());
        var unanchored = Intent(user); await Store(unanchored);
        await f.DbAsync(async db => { (await db.NoticeDeliveryIntents.SingleAsync(row => row.Id == unanchored.Id)).SuppressionAnchorAt = Now; return await db.SaveChangesAsync(); });
        await GuardRefuses<NoticeDeliveryIntent>(unanchored.Id, row => row.SuppressionAnchorAt = null);
        await f.DbAsync(async db => { (await db.NoticeDeliveryIntents.SingleAsync(row => row.Id == unanchored.Id)).Disposition = NoticeIntentDisposition.AccessRemoved; return await db.SaveChangesAsync(); });
        await GuardRefuses<NoticeDeliveryIntent>(unanchored.Id, row => row.Disposition = NoticeIntentDisposition.Pending);
    }

    [Fact]
    public async Task Slot_window_budgets_and_linked_email_payload_membership_transport_and_uncertainty_cannot_be_rewritten()
    {
        var user = await Recipient(); var intent = Intent(user); var slot = Digest(user); slot.CompositionVersion = 1; slot.ExternalAttempts = 1; await Store(intent, slot);
        var email = Immediate(user, intent.Id); email.Kind = "Digest"; email.NoticeSlotId = slot.Id; email.NoticeCompositionVersion = 1;
        email.Attempts = 1; email.NoticeClaimToken = Guid.NewGuid(); email.NoticeClaimVersion = 2; email.NoticeClaimedAt = Now;
        email.TransportKind = NoticeTransportKind.Smtp; email.TransportOutcome = NoticeTransportOutcome.Uncertain;
        email.TransportObservedAt = Now; email.AcceptanceUncertainAt = Now; await Store(email);
        foreach (var alter in new Action<NoticeDeliverySlot>[] { row => row.WindowFrom = row.WindowFrom!.Value.AddHours(-1),
            row => row.ScheduledAtUtc = Now.AddHours(1), row => row.CompositionVersion = 0, row => row.ExternalAttempts = 0 })
            await GuardRefuses(slot.Id, alter);
        await f.DbAsync(async db => { var row = await db.NoticeDeliverySlots.SingleAsync(value => value.Id == slot.Id); row.CompositionVersion = 3; row.ExternalAttempts = 8; return await db.SaveChangesAsync(); });
        await DatabaseRefuses(db => { var row = db.NoticeDeliverySlots.Single(value => value.Id == slot.Id); row.CompositionVersion = 4; });
        await DatabaseRefuses(db => { var row = db.NoticeDeliverySlots.Single(value => value.Id == slot.Id); row.ExternalAttempts = 9; });
        foreach (var alter in new Action<EmailMessage>[] { row => row.Subject += "changed", row => row.BodyText += "changed", row => row.RenderedOccurrenceIds = [Guid.NewGuid()],
            row => row.BulkRenderedMembershipJson = "[]", row => row.RequiredProjectIds = [Guid.NewGuid()], row => row.NoticeSlotId = Guid.NewGuid(),
            row => row.TransportKind = NoticeTransportKind.Graph, row => row.AcceptanceUncertainAt = null, row => row.Attempts = 0, row => row.NoticeClaimVersion = 1 })
            await GuardRefuses(email.Id, alter);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { db.NoticeDeliverySlots.Remove(await db.NoticeDeliverySlots.SingleAsync(row => row.Id == slot.Id)); return await db.SaveChangesAsync(); }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { db.Emails.Remove(await db.Emails.SingleAsync(row => row.Id == email.Id)); return await db.SaveChangesAsync(); }));
        await f.DbAsync(async db => { var row = await db.Emails.SingleAsync(value => value.Id == email.Id); row.LastError = "Synthetic diagnostic, no provider call"; return await db.SaveChangesAsync(); });
        var stored = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == email.Id));
        Assert.Equal(email.Subject, stored.Subject); Assert.Equal(email.RenderedOccurrenceIds, stored.RenderedOccurrenceIds);
        Assert.Equal(NoticeTransportKind.Smtp, stored.TransportKind); Assert.Equal(Now, stored.AcceptanceUncertainAt); Assert.Equal(1, stored.Attempts);
    }

    [Fact]
    public async Task Notification_cleanup_sets_links_null_but_retains_captured_ordinal_and_emitted_reminder_for_tracked_and_bulk_deletion()
    {
        var user = await Recipient(); var before = PublicEffects();
        foreach (var tracked in new[] { true, false })
        {
            var notification = new Notification { UserId = user, EventType = NotificationEvents.TaskAssigned, Title = "Synthetic cleanup", Count = 7, CreatedAt = Now, UpdatedAt = Now };
            await Store(notification); var intent = Intent(user); intent.NotificationId = notification.Id; intent.NotificationCountAtCapture = 7;
            var slot = Digest(user); slot.Kind = NoticeSlotKind.Reminder; slot.LocalDate = slot.LocalDate.AddDays(tracked ? 0 : -7);
            slot.ScheduledLocalDate = slot.LocalDate; slot.ScheduledAtUtc = Now.AddDays(tracked ? 0 : -7);
            slot.FrequencyAtCreation = null; slot.WindowFrom = slot.WindowThrough = null; slot.State = NoticeSlotState.Emitted; slot.ReminderNotificationId = notification.Id;
            await Store(intent, slot); // Labelled historical emitted fixture; no reminder scheduler or app emission worker ran.
            await f.DbAsync(async db => {
                if (!tracked) return await db.Notifications.Where(row => row.Id == notification.Id).ExecuteDeleteAsync();
                await db.NoticeDeliveryIntents.SingleAsync(row => row.Id == intent.Id); await db.NoticeDeliverySlots.SingleAsync(row => row.Id == slot.Id);
                db.Notifications.Remove(await db.Notifications.SingleAsync(row => row.Id == notification.Id)); return await db.SaveChangesAsync();
            });
            var retained = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.Id == intent.Id));
            var reminder = f.Db(db => db.NoticeDeliverySlots.AsNoTracking().Single(row => row.Id == slot.Id));
            Assert.False(f.Db(db => db.Notifications.Any(row => row.Id == notification.Id)));
            Assert.Null(retained.NotificationId); Assert.Equal(7, retained.NotificationCountAtCapture);
            Assert.Null(reminder.ReminderNotificationId); Assert.Equal(NoticeSlotState.Emitted, reminder.State); Assert.Equal(0, reminder.CompositionVersion); Assert.Equal(0, reminder.ExternalAttempts);
            await GuardRefuses<NoticeDeliverySlot>(slot.Id, row => row.State = NoticeSlotState.Ready);
            await GuardRefuses<NoticeDeliveryIntent>(intent.Id, row => row.NotificationCountAtCapture = 8);
        }
        Assert.Equal(before, PublicEffects());
    }

    [Fact]
    public async Task Digest_policy_json_is_private_nullable_jsonb_but_immutable_when_linked_and_forbidden_on_other_mail()
    {
        var user = await Recipient(); var intent = Intent(user); var slot = Digest(user);
        slot.State = NoticeSlotState.Queued; slot.CompositionVersion = 3; await Store(intent, slot);
        var before = PublicEffects();
        var mapping = f.Db(db => {
            var property = db.Model.FindEntityType(typeof(EmailMessage))!.FindProperty(nameof(EmailMessage.NoticeDigestPolicyJson))!;
            return (property.IsNullable, ColumnType: property.GetColumnType());
        });
        Assert.True(mapping.IsNullable); Assert.Equal("jsonb", mapping.ColumnType);
        var legacy = new EmailMessage { Kind = "Weekly", ToAddress = "synthetic-legacy@hub.test", Subject = "Synthetic old mail", BodyText = "Old body", CreatedAt = Now };
        await Store(legacy);
        Assert.Null(f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == legacy.Id).NoticeDigestPolicyJson));
        var badLegacy = new EmailMessage { Kind = "Weekly", ToAddress = "synthetic-legacy@hub.test", Subject = "Synthetic bad legacy", BodyText = "Old body", CreatedAt = Now, NoticeDigestPolicyJson = "{}" };
        await DatabaseRefuses(db => db.Emails.Add(badLegacy));
        foreach (var kind in new[] { "Immediate", "PlanningEntryChanged", "PlanningWithdrawal" }) {
            var bad = Immediate(user, intent.Id); bad.Kind = kind; bad.NoticeDigestPolicyJson = "{}";
            await DatabaseRefuses(db => db.Emails.Add(bad));
        }
        EmailMessage Mail(int version, string? witness) {
            var row = Immediate(user, intent.Id); row.Kind = "Digest"; row.NoticeSlotId = slot.Id;
            row.NoticeCompositionVersion = version; row.NoticeDigestPolicyJson = witness; return row;
        }
        // An object is a storage-shape fixture, NOT a valid delivery policy witness. Queue/claim must validate it.
        var captured = Mail(1, "{\"modelFixture\":true}"); var oldDigest = Mail(2, null);
        await Store(captured, oldDigest);
        var stored = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == captured.Id));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(captured.NoticeDigestPolicyJson!), JsonNode.Parse(stored.NoticeDigestPolicyJson!)));
        Assert.Null(f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == oldDigest.Id).NoticeDigestPolicyJson));
        var raw = JsonSerializer.SerializeToNode(stored, JsonOpts.Web)!.AsObject();
        Assert.False(raw.ContainsKey("noticeDigestPolicyJson"));
        Assert.Equal(JsonSerializer.SerializeToNode(legacy, JsonOpts.Web)!.AsObject().Select(pair => pair.Key).Order(), raw.Select(pair => pair.Key).Order());
        var array = Mail(3, "[]"); await DatabaseRefuses(db => db.Emails.Add(array));
        var invalidJson = Mail(3, "not-json"); await DatabaseRefuses(db => db.Emails.Add(invalidJson), PostgresErrorCodes.InvalidTextRepresentation);
        await GuardRefuses<EmailMessage>(captured.Id, row => row.NoticeDigestPolicyJson = "{}");
        await GuardRefuses<EmailMessage>(captured.Id, row => row.NoticeDigestPolicyJson = null);
        await GuardRefuses<EmailMessage>(oldDigest.Id, row => row.NoticeDigestPolicyJson = "{}");
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(stored.NoticeDigestPolicyJson!), JsonNode.Parse(f.Db(db => db.Emails.Single(row => row.Id == captured.Id).NoticeDigestPolicyJson)!)));
        Assert.Equal(before, PublicEffects());
    }

    NoticeDeliverySlot LaterDigest(Guid user, int days = 1)
    {
        var slot = Digest(user); slot.LocalDate = slot.LocalDate.AddDays(days); slot.ScheduledLocalDate = slot.LocalDate;
        slot.ScheduledAtUtc = Now.AddDays(days); slot.WindowFrom = Now.AddDays(-2); slot.WindowThrough = Now.AddDays(days);
        return slot;
    }

    string SlotContents() => f.Db(db => JsonSerializer.Serialize(db.NoticeDeliverySlots.AsNoTracking().OrderBy(row => row.Id).Select(row => new {
        row.Id, row.UserId, row.Kind, row.LocalDate, row.OrgTimeZoneId, row.ScheduledLocalDate, row.ScheduledLocalTime, row.ScheduledAtUtc,
        row.FrequencyAtCreation, row.WindowFrom, row.WindowThrough, row.CreatedAt, row.UpdatedAt, row.State,
        row.CompositionVersion, row.ExternalAttempts, row.ReminderNotificationId,
    }).ToArray()));

    string MailContents() => f.Db(db => JsonSerializer.Serialize(db.Emails.AsNoTracking().OrderBy(row => row.Id).Select(row => new {
        row.Id, row.UserId, row.Kind, row.Subject, row.BodyText, row.BodyHtml, row.CreatedAt, row.SentAt, row.Attempts,
        row.NoticeProtocolVersion, row.NoticeSlotId, row.NoticeCompositionVersion, row.RenderedOccurrenceIds,
        row.BulkRenderedMembershipJson, row.NoticeDigestPolicyJson, row.NoticeClaimToken, row.NoticeClaimVersion, row.NoticeClaimedAt,
        row.TransportKind, row.TransportOutcome, row.TransportObservedAt, row.ProviderAcceptedAt, row.AcceptanceUncertainAt,
    }).ToArray()));

    [Fact]
    public async Task Tracked_digest_transfer_retains_original_windows_budgets_mail_and_history_and_cannot_clear_repoint_or_cycle()
    {
        var user = await Recipient(); var source = Digest(user); source.State = NoticeSlotState.Exhausted;
        source.CompositionVersion = 3; source.ExternalAttempts = 8;
        var target = LaterDigest(user); var other = LaterDigest(user, 2); var intent = Intent(user);
        await Store(source, target, other, intent);
        var mail = Immediate(user, intent.Id); mail.Kind = "Digest"; mail.NoticeSlotId = source.Id; mail.NoticeCompositionVersion = 3;
        mail.NoticeDigestPolicyJson = "{\"modelFixture\":true}"; await Store(mail);
        var before = PublicEffects(); var contents = SlotContents(); var mailBytes = MailContents();
        await f.DbAsync(async db => {
            await db.NoticeDeliverySlots.SingleAsync(row => row.Id == target.Id); // Explicitly tracked target, not just a real FK.
            (await db.NoticeDeliverySlots.SingleAsync(row => row.Id == source.Id)).CarryForwardToSlotId = target.Id;
            return await db.SaveChangesAsync();
        });
        Assert.Equal(target.Id, f.Db(db => db.NoticeDeliverySlots.Single(row => row.Id == source.Id).CarryForwardToSlotId));
        Assert.Equal(contents, SlotContents()); Assert.Equal(mailBytes, MailContents()); Assert.Equal(before, PublicEffects());
        // Reassigning the existing value is a no-op, never a fresh transfer/budget/window rewrite.
        await f.DbAsync(async db => { (await db.NoticeDeliverySlots.SingleAsync(row => row.Id == source.Id)).CarryForwardToSlotId = target.Id; return await db.SaveChangesAsync(); });
        await GuardRefuses<NoticeDeliverySlot>(source.Id, row => row.CarryForwardToSlotId = null);
        await GuardRefuses<NoticeDeliverySlot>(source.Id, row => row.CarryForwardToSlotId = other.Id);
        Assert.Equal(contents, SlotContents()); Assert.Equal(mailBytes, MailContents());
        // Give the second source the state prerequisite so the attempted reverse edge tests strictly increasing coverage.
        await f.DbAsync(async db => { var row = await db.NoticeDeliverySlots.SingleAsync(value => value.Id == target.Id);
            row.State = NoticeSlotState.Exhausted; row.CompositionVersion = 3; row.ExternalAttempts = 8; return await db.SaveChangesAsync(); });
        var beforeCycle = SlotContents();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => {
            await db.NoticeDeliverySlots.SingleAsync(row => row.Id == source.Id);
            (await db.NoticeDeliverySlots.SingleAsync(row => row.Id == target.Id)).CarryForwardToSlotId = source.Id;
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(beforeCycle, SlotContents()); Assert.Equal(mailBytes, MailContents()); Assert.Equal(before, PublicEffects());
        Assert.Null(f.Db(db => db.NoticeDeliverySlots.Single(row => row.Id == target.Id).CarryForwardToSlotId));
        // Model-level window binding only; no safe-email provenance, external acceptance or worker execution is proved here.
    }

    [Theory]
    [InlineData("notExhausted")]
    [InlineData("flipToExhausted")]
    [InlineData("flipAway")]
    [InlineData("nonCoverFrom")]
    [InlineData("sameThrough")]
    [InlineData("self")]
    [InlineData("untracked")]
    [InlineData("added")]
    [InlineData("windowRewrite")]
    [InlineData("budgetReset")]
    public async Task Carry_guard_refuses_invalid_source_coverage_target_tracking_and_invented_or_rewritten_provenance(string variant)
    {
        var user = await Recipient(); var source = Digest(user); source.State = NoticeSlotState.Exhausted;
        source.CompositionVersion = 3; source.ExternalAttempts = 8; var target = LaterDigest(user);
        if (variant is "notExhausted" or "flipToExhausted") source.State = NoticeSlotState.Ready;
        if (variant == "nonCoverFrom") target.WindowFrom = source.WindowFrom!.Value.AddMinutes(1);
        if (variant == "sameThrough") target.WindowThrough = source.WindowThrough;
        if (variant == "added") await Store(target); else await Store(source, target);
        var before = PublicEffects(); var contents = SlotContents();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => {
            if (variant != "untracked") await db.NoticeDeliverySlots.SingleAsync(row => row.Id == target.Id);
            else await db.NoticeDeliverySlots.AsNoTracking().SingleAsync(row => row.Id == target.Id);
            NoticeDeliverySlot row;
            if (variant == "added") { row = source; db.NoticeDeliverySlots.Add(row); }
            else row = await db.NoticeDeliverySlots.SingleAsync(value => value.Id == source.Id);
            row.CarryForwardToSlotId = variant == "self" ? source.Id : target.Id;
            if (variant == "flipToExhausted") row.State = NoticeSlotState.Exhausted;
            if (variant == "flipAway") row.State = NoticeSlotState.Ready;
            if (variant == "windowRewrite") row.WindowFrom = row.WindowFrom!.Value.AddDays(-1);
            if (variant == "budgetReset") row.ExternalAttempts = 0;
            return await db.SaveChangesAsync();
        }));
        Assert.Equal(contents, SlotContents()); Assert.Equal(before, PublicEffects());
        Assert.False(f.Db(db => db.NoticeDeliverySlots.Any(row => row.CarryForwardToSlotId != null)));
    }

    async Task ProviderRefuses(Func<HubDb, Task<int>> command, string sqlState)
    {
        // ExecuteUpdate/Delete bypass HubDb.SaveChanges: these assertions belong to PostgreSQL, not the tracked guard.
        var error = await Assert.ThrowsAsync<PostgresException>(() => f.DbAsync(command));
        Assert.Equal(sqlState, error.SqlState);
    }

    [Fact]
    public async Task PostgreSQL_carry_pointer_is_same_user_same_kind_nonself_Digest_and_target_deletion_is_restricted()
    {
        var user = await Recipient(); var other = await Recipient();
        var source = Digest(user); source.State = NoticeSlotState.Exhausted; source.CompositionVersion = 3; source.ExternalAttempts = 8;
        var target = LaterDigest(user); var foreign = Digest(other);
        var reminder = Digest(user); reminder.Kind = NoticeSlotKind.Reminder; reminder.WindowFrom = reminder.WindowThrough = null;
        reminder.FrequencyAtCreation = null;
        await Store(source, target, foreign, reminder); var before = PublicEffects(); var contents = SlotContents();
        await ProviderRefuses(db => db.NoticeDeliverySlots.Where(row => row.Id == source.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarryForwardToSlotId, (Guid?)foreign.Id)), PostgresErrorCodes.ForeignKeyViolation);
        await ProviderRefuses(db => db.NoticeDeliverySlots.Where(row => row.Id == source.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarryForwardToSlotId, (Guid?)reminder.Id)), PostgresErrorCodes.ForeignKeyViolation);
        await ProviderRefuses(db => db.NoticeDeliverySlots.Where(row => row.Id == source.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarryForwardToSlotId, (Guid?)source.Id)), PostgresErrorCodes.CheckViolation);
        await ProviderRefuses(db => db.NoticeDeliverySlots.Where(row => row.Id == reminder.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarryForwardToSlotId, (Guid?)target.Id)), PostgresErrorCodes.CheckViolation);
        Assert.Equal(contents, SlotContents());
        await f.DbAsync(db => db.NoticeDeliverySlots.Where(row => row.Id == source.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(row => row.CarryForwardToSlotId, (Guid?)target.Id)));
        await ProviderRefuses(db => db.NoticeDeliverySlots.Where(row => row.Id == target.Id).ExecuteDeleteAsync(), PostgresErrorCodes.ForeignKeyViolation);
        Assert.Equal(contents, SlotContents()); Assert.Equal(before, PublicEffects());
        Assert.Equal(target.Id, f.Db(db => db.NoticeDeliverySlots.Single(row => row.Id == source.Id).CarryForwardToSlotId));
        // SQL does not prove source exhaustion, retained-window coverage or mail safety; those are separate guards/helpers.
    }
}
