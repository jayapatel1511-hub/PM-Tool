using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// Actual HTTP producers and public projections; transport completion below is an explicit recording fake.
public sealed class NoticeProducerIntegrationTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public NoticeProducerIntegrationTests() => data = new(f);
    public void Dispose() => f.Dispose();

    async Task Enable()
    {
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "UTC" })).EnsureSuccessStatusCode();
    }

    [Theory]
    [InlineData(NoticeTransportKind.Smtp, NoticeTransportOutcome.Accepted, "Emailed")]
    [InlineData(NoticeTransportKind.Log, NoticeTransportOutcome.Simulated, "Simulated")]
    public async Task Actual_assignment_capture_public_receipt_and_opaque_pulse_distinguish_real_acceptance_from_simulation(string transport, string outcome, string state)
    {
        await Enable();
        var project = await data.Project(); var user = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = user });
        var intent = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.UserId == user
            && row.EventType == NotificationEvents.TaskAssigned && row.ItemId == task.G("id")));
        Assert.NotNull(intent.NotificationId); Assert.NotNull(intent.DeliveryEmailId);
        var before = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notifications/pulse")).Json();
        var appBefore = f.Db(db => db.Notifications.AsNoTracking().Single(row => row.Id == intent.NotificationId));
        var claim = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var result = await NotificationDelivery.ClaimAsync(context, intent.DeliveryEmailId!.Value, transport, NoticeClaimPurpose.Initial, f.Clock.Now);
            await db.SaveChangesAsync(); return Assert.IsType<NoticeSendClaim>(result.Claim);
        }));
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var result = await NotificationDelivery.CompleteAsync(context, claim,
                new(transport, outcome, f.Clock.Now, outcome == NoticeTransportOutcome.Accepted ? f.Clock.Now : null, NoticeFailureCertainty.None));
            await db.SaveChangesAsync(); return result;
        }));
        var after = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notifications/pulse")).Json();
        Assert.NotEqual(before.S("stamp"), after.S("stamp"));
        var page = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notifications/screen?pageSize=200")).Json();
        var row = page["items"]!.AsArray().Single(node => node!["notification"]!.G("id") == intent.NotificationId)!;
        var delivery = row["screen"]!["delivery"]!;
        Assert.Equal(state, delivery.S("state")); Assert.Equal("Known", delivery.S("historyCoverage"));
        Assert.Equal(1, delivery.I("knownOccurrences")); Assert.Equal(1, delivery.I("totalOccurrences"));
        Assert.Equal(outcome == NoticeTransportOutcome.Accepted ? 1 : 0, delivery.I("realEmailedOccurrences"));
        Assert.Equal(outcome == NoticeTransportOutcome.Accepted, delivery["acceptedAt"] is not null);
        Assert.DoesNotContain("renderedOccurrenceIds", page.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("noticeDigestPolicyJson", page.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        var appAfter = f.Db(db => db.Notifications.AsNoTracking().Single(n => n.Id == intent.NotificationId));
        Assert.Equal(appBefore.Count, appAfter.Count); Assert.Equal(appBefore.UpdatedAt, appAfter.UpdatedAt);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task Automatic_follow_rereads_a_manual_choice_committed_after_its_original_read_with_unrelated_flags_off(bool automaticDelete, bool replacement, bool restricted)
    {
        await Enable();
        foreach (var flag in new[] { "verifiable_log", "task_pickup" })
            (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{flag}", new { value = false })).EnsureSuccessStatusCode();
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await f.DbAsync(async db =>
        {
            if (restricted) (await db.Projects.SingleAsync(row => row.Id == project.Id)).Visibility = Visibility.Restricted;
            var follow = await db.Follows.SingleAsync(row => row.ProjectId == project.Id && row.UserId == user);
            follow.Source = FollowSource.Assignment; follow.Level = FollowLevel.MyItemsOnly;
            return await db.SaveChangesAsync();
        });
        await f.DbAsync(async db =>
        {
            var stale = await db.Follows.SingleAsync(row => row.ProjectId == project.Id && row.UserId == user);
            if (automaticDelete) db.Follows.Remove(stale);
            else { stale.Level = FollowLevel.AllActivity; stale.UpdatedAt = f.Clock.Now; }
            if (replacement) (await f.As(TestData.Alex).DeleteAsync($"/api/v1/projects/{project.Id}/follow")).EnsureSuccessStatusCode();
            (await f.As(TestData.Alex).Put($"/api/v1/projects/{project.Id}/follow", new { level = FollowLevel.Muted })).EnsureSuccessStatusCode();
            await db.SaveChangesAsync(); return true;
        });
        var final = f.Db(db => db.Follows.AsNoTracking().SingleOrDefault(row => row.ProjectId == project.Id && row.UserId == user));
        if (automaticDelete && restricted) Assert.Null(final);
        else { Assert.NotNull(final); Assert.Equal(FollowSource.Manual, final.Source); Assert.Equal(FollowLevel.Muted, final.Level); }
    }

    [Fact]
    public async Task Operations_counts_a_captured_weekly_dispatch_on_Tuesday_without_creating_another_slot_or_alarm_for_a_reminder()
    {
        f.Clock.Now = new DateTimeOffset(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);
        await Enable(); var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = new DateOnly(2026, 11, 1) });
        await f.Evaluate(project.Id);
        await f.DbAsync(async db =>
        {
            foreach (var id in await db.Users.Where(row => row.IsActive).Select(row => row.Id).ToArrayAsync())
            {
                var setting = await UserPreferences.GetOrCreate(db, id);
                setting.DigestEnabled = id == user; setting.HoldDigestWhenAway = false; setting.DigestTimeLocal = "07:00";
                setting.NoticeDigestFrequency = NoticeDigestFrequency.Weekly;
                setting.DigestSectionsOff = System.Text.Json.JsonSerializer.Serialize(Digest.SectionCodes.Except(["overdue"]).ToArray());
            }
            return await db.SaveChangesAsync();
        });
        var slotId = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var slot = Assert.IsType<NoticeDeliverySlot>(await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now));
            var result = await NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test");
            Assert.Equal(NoticeDigestCompositionDecision.Queued, result.Decision);
            await db.SaveChangesAsync(); return slot.Id;
        }));
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var settings = new OrgSettings { OrgTimeZone = "UTC", Flags = new Dictionary<string, bool> { ["people_screens"] = true } };
        var before = f.Db(db => (db.NoticeDeliverySlots.Count(), db.Emails.Count(), db.ActivityLog.Count()));
        var facts = await f.DbAsync(db => OpsChecks.Gather(db, settings, f.Clock.Now, f.Clock.Now.AddMinutes(-5)));
        Assert.Equal(0, facts.CapturedDigestsNotBuilt); Assert.Equal(1, facts.CapturedDigestsUnsent);
        Assert.Equal(0, facts.TimeRemindersDue); Assert.Contains(OpsChecks.Evaluate(facts), row => row.Kind == "DigestLate");
        Assert.Equal(before, f.Db(db => (db.NoticeDeliverySlots.Count(), db.Emails.Count(), db.ActivityLog.Count())));
        Assert.Equal(slotId, f.Db(db => db.NoticeDeliverySlots.Single(row => row.UserId == user).Id));
    }

    [Fact]
    public void Captured_work_can_be_late_on_a_noncreation_day_without_inventing_a_reminder_alert()
    {
        var saturday = new DateTimeOffset(2026, 11, 7, 12, 0, 0, TimeSpan.Zero);
        var facts = new OpsFacts(saturday, new(), null, saturday.AddHours(-1), saturday.AddDays(-1), [], 0, 0,
            CapturedDigestsUnsent: 1, TimeRemindersDue: 10);
        Assert.Equal("DigestLate", Assert.Single(OpsChecks.Evaluate(facts)).Kind);
        Assert.Empty(OpsChecks.Evaluate(facts with { CapturedDigestsUnsent = 0 }));
    }
}
