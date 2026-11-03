using System.Collections.Immutable;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

// Isolated PostgreSQL/fake-clock formatter, scheduling and protocol coverage, authored for root-owned execution.
// Provider cases commit claims and complete them with explicit fake observations; no external sender is invoked.
// Formatter-only prepared held candidates are explicitly labelled admission fakes.
public sealed class NotificationDigestCompositionTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public NotificationDigestCompositionTests() => data = new(f);
    public void Dispose() => f.Dispose();

    static OrgSettings Settings(bool people = true, bool adminReview = false) => new()
    {
        OrgTimeZone = "UTC",
        Flags = new Dictionary<string, bool> { ["people_screens"] = people, ["admin_review"] = adminReview },
    };
    static ImmutableHashSet<string> Only(params string[] enabled) => Digest.SectionCodes.Except(enabled).ToImmutableHashSet(StringComparer.Ordinal);
    NoticeDigestBuildRequest Request(Guid user, params string[] enabled) => new(Guid.NewGuid(), user, "Test",
        DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), f.Clock.Now, new(f.Clock.Now.AddDays(-1), f.Clock.Now),
        Settings(), "https://hub.test", Only(enabled), []);
    Task<NoticeDigestRenderedPayload?> Build(NoticeDigestBuildRequest request) => f.DbAsync(db => Digest.Build(db, request));
    string State() => f.Db(db => JsonSerializer.Serialize(new
    {
        Emails = db.Emails.Count(), Intents = db.NoticeDeliveryIntents.Count(), Slots = db.NoticeDeliverySlots.Count(),
        Activity = db.ActivityLog.Count(), Outbox = db.Outbox.Count(),
        Notices = db.Notifications.AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.EmailedAt, row.DigestIncludedAt }).ToArray(),
        Preferences = db.UserSettings.AsNoTracking().OrderBy(row => row.UserId).Select(row => new { row.UserId, row.LastDigestAt, row.LastWeeklySummaryAt }).ToArray(),
    }));
    static void Ids(IEnumerable<Guid> expected, IEnumerable<Guid> actual) => Assert.Equal(expected.Order().ToArray(), actual.ToArray());

    async Task<Guid> EnableScheduling(string person = TestData.Alex, string frequency = NoticeDigestFrequency.Daily,
        bool hold = false, string time = "07:00", string[]? sections = null)
    {
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "UTC" })).EnsureSuccessStatusCode();
        (await f.As(person).Put("/api/v1/me/preferences/digest", new { enabled = true, time,
            sectionsOff = sections ?? Only("overdue").ToArray(), holdDigestWhenAway = hold })).EnsureSuccessStatusCode();
        var preferences = await (await f.As(person).GetAsync("/api/v1/me/notice-preferences")).Json();
        await (await f.As(person).Patch("/api/v1/me/notice-preferences", new { revision = preferences.I("revision"), noticeDigestFrequency = frequency })).Json();
        return data.User(person);
    }
    Task<T> Recipient<T>(Guid user, Func<NoticeRecipientContext, Task<T>> action, bool save = true) => f.DbAsync(db => Tx.Run(db, async () =>
    {
        var context = await NotificationDelivery.LockRecipientAsync(db, user);
        var result = await action(context);
        if (save) await db.SaveChangesAsync();
        return result;
    }));
    Task<NoticeDigestDue> Due(Guid user) => Recipient(user, context => NotificationDigestComposition.ReadDueAsync(context, f.Clock.Now), save: false);
    Task<NoticeDigestCompositionResult?> Queue(Guid user) => Recipient(user, async context =>
    {
        var slot = await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now);
        return slot is null ? null : await NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test");
    });
    NoticeDeliverySlot[] Slots(Guid user) => f.Db(db => db.NoticeDeliverySlots.AsNoTracking().Where(row => row.UserId == user && row.Kind == NoticeSlotKind.Digest).OrderBy(row => row.LocalDate).ToArray());
    EmailMessage[] LinkedEmails(Guid user) => f.Db(db => db.Emails.AsNoTracking().Where(row => row.UserId == user && row.Kind == "Digest" && row.NoticeProtocolVersion == 1).OrderBy(row => row.NoticeCompositionVersion).ToArray());

    [Fact]
    public async Task No_held_output_matches_legacy_bytes_and_requires_only_contributing_projects_without_writes()
    {
        var project = await data.Project();
        var unused = await data.Project();
        var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { name = "Synthetic late plan", assigneeId = user, dueDate = "2026-09-10" });
        await data.NewTask(unused.Id, extra: new { name = "Synthetic unrelated future plan", assigneeId = user, dueDate = "2026-10-10" });
        await f.Evaluate(project.Id); await f.Evaluate(unused.Id);
        var request = Request(user, "overdue");
        var before = State();
        var legacy = Assert.IsType<Digest.Result>(await f.DbAsync(db => Digest.Build(db, user, request.FirstName, request.Today,
            request.Window.From, request.ReadAtUtc, request.Settings, request.BaseUrl, request.SectionsOff)));
        var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Assert.Equal(legacy.Subject, result.Subject); Assert.Equal(legacy.Body, result.BodyText);
        Assert.Contains(unused.Id, legacy.RequiredProjectIds);
        Ids([project.Id], result.Scope.ProjectIds); Assert.Empty(result.Scope.PlanningEntryIds);
        Assert.Equal(new[] { "overdue" }, result.RenderedPolicy.RenderedSectionCodes.ToArray());
        Assert.Empty(result.Membership.IntentIds); Assert.Empty(result.Membership.BulkMemberships);
        Assert.False(result.RenderedPolicy.IncludesAdminReview);
        Assert.Equal(request.SlotId, result.SlotId); Assert.Equal(request.Window, result.Window);
        Assert.Equal(before, State());
        Assert.Null(await Build(request with { SectionsOff = Only("reviews") }));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Counted_overflow_keeps_the_eleventh_rows_project_even_when_its_detail_line_is_capped()
    {
        var first = await data.Project(); var overflow = await data.Project(); var user = data.User(TestData.Alex);
        for (var i = 0; i < 10; i++)
            await data.NewTask(first.Id, extra: new { name = $"Synthetic older plan {i}", assigneeId = user, dueDate = "2026-09-01" });
        await data.NewTask(overflow.Id, extra: new { name = "Synthetic eleventh detail", assigneeId = user, dueDate = "2026-09-10" });
        await f.Evaluate(first.Id); await f.Evaluate(overflow.Id);
        var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(Request(user, "overdue")));
        Assert.Contains("OVERDUE (11)", result.BodyText);
        Assert.DoesNotContain("Synthetic eleventh detail", result.BodyText);
        Ids([first.Id, overflow.Id], result.Scope.ProjectIds);
        Assert.Empty(result.Membership.IntentIds);
    }

    [Fact]
    public async Task Staff_notice_history_is_From_exclusive_Through_inclusive_and_retains_actual_multi_project_scope()
    {
        var first = await data.Project(); var second = await data.Project(); var excluded = await data.Project();
        var user = data.User(TestData.Sam); var request = Request(user, "staff");
        request = request with { Window = new(request.Window.From, request.ReadAtUtc.AddHours(-1)) };
        Notification Notice(string title, DateTimeOffset at, Guid recipient, params Guid[] projects) => new()
        {
            UserId = recipient, EventType = NotificationEvents.StaffAssignment, Title = title,
            RequiredProjectIds = projects, CreatedAt = at, UpdatedAt = at, LinkPath = "/my-work",
        };
        var atFrom = Notice("Synthetic excluded From notice", request.Window.From, user, excluded.Id);
        var atThrough = Notice("Synthetic included Through notice", request.Window.Through, user, first.Id, second.Id);
        var after = Notice("Synthetic deferred notice", request.Window.Through.AddMilliseconds(1), user, excluded.Id);
        var foreign = Notice("Synthetic other recipient notice", request.Window.Through, data.User(TestData.Alex), excluded.Id);
        // Historical/imported notification facts are deliberately seeded at exact boundary instants.
        await f.DbAsync(async db => { db.Notifications.AddRange(atFrom, atThrough, after, foreign); return await db.SaveChangesAsync(); });
        var before = State(); var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Assert.Contains(atThrough.Title, result.BodyText);
        foreach (var row in new[] { atFrom, after, foreign }) Assert.DoesNotContain(row.Title, result.BodyText);
        Ids([first.Id, second.Id], result.Scope.ProjectIds);
        Ids([atThrough.Id], result.RenderedPolicy.StaffAssignmentNotificationIds);
        Assert.Empty(result.RenderedPolicy.StaffDirectReportIds);
        var legacy = Assert.IsType<Digest.Result>(await f.DbAsync(db => Digest.Build(db, user, request.FirstName, request.Today,
            request.Window.From, request.ReadAtUtc, request.Settings, request.BaseUrl, request.SectionsOff)));
        Assert.Contains(after.Title, legacy.Body); // Existing overload deliberately keeps its historical lower-bound-only behavior.
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Staff_summary_provenance_covers_counted_work_and_stalled_reviews_but_not_other_report_work()
    {
        var overdue = await data.Project(); var stalled = await data.Project(); var unused = await data.Project();
        var alex = data.User(TestData.Alex); var sam = data.User(TestData.Sam);
        await data.NewTask(overdue.Id, extra: new { assigneeId = alex, dueDate = "2026-09-10" });
        var review = await data.NewTask(stalled.Id, extra: new { assigneeId = data.User(TestData.Marc), reviewerId = alex, dueDate = "2026-10-10" });
        await data.NewTask(unused.Id, extra: new { assigneeId = alex, dueDate = "2026-10-10" });
        foreach (var project in new[] { overdue, stalled, unused }) await f.Evaluate(project.Id);
        // Synthetic evaluator fact isolates provenance from review-age scheduling; real work records use normal commands.
        var reviewId = review.G("id");
        await f.DbAsync(async db => { (await db.TaskStates.SingleAsync(row => row.TaskId == reviewId)).IsReviewStalled = true; return await db.SaveChangesAsync(); });
        var request = Request(sam, "staff") with { Window = new(f.Clock.Now, f.Clock.Now) };
        var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Ids([overdue.Id, stalled.Id], result.Scope.ProjectIds);
        Ids([alex], result.RenderedPolicy.StaffDirectReportIds);
        Assert.Empty(result.RenderedPolicy.StaffAssignmentNotificationIds);
        Assert.Contains("MY STAFF (1)", result.BodyText);
    }

    [Fact]
    public async Task Update_counts_keep_all_contributing_activity_ids_after_window_privacy_and_notification_filters()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex); var actor = data.User(TestData.Marc);
        var request = Request(user, "updates");
        request = request with { Window = new(request.Window.From, request.ReadAtUtc.AddHours(-1)) };
        ActivityLog Activity(string name, DateTimeOffset at, string type = "Task", Guid? by = null) => new()
        {
            ProjectId = project.Id, ActorUserId = by ?? actor, ActorType = "User", OccurredAt = at,
            ItemType = type, ItemId = Guid.NewGuid(), ItemName = name, Action = "Updated", Categories = ["status"],
        };
        var included = Enumerable.Range(0, 7).Select(i => Activity($"Synthetic contributing event {i}", request.Window.Through.AddMinutes(-i))).ToArray();
        var atFrom = Activity("Synthetic excluded From event", request.Window.From);
        var after = Activity("Synthetic deferred event", request.Window.Through.AddMilliseconds(1));
        var own = Activity("Synthetic own event", request.Window.Through, by: user);
        var hidden = Activity("Synthetic hidden contribution", request.Window.Through, "TaskContribution");
        var unreadableTime = Activity("Synthetic unreadable time", request.Window.Through, ItemType.TimeEntry);
        var alreadyNotified = Activity("Synthetic already notified", request.Window.Through);
        alreadyNotified.CorrelationId = Guid.NewGuid();
        var privateEvent = new CalendarEvent { ProjectId = project.Id, OwnerId = actor, Title = "Synthetic private meeting",
            StartAt = request.Window.Through, EndAt = request.Window.Through.AddHours(1), TimeZone = "UTC", Visibility = EventVisibility.Private };
        var privateActivity = Activity("Synthetic private activity", request.Window.Through, ItemType.CalendarEvent);
        privateActivity.ItemId = privateEvent.Id;
        // Synthetic imported audit rows isolate count/top-five behavior and source privacy without dispatching anything.
        await f.DbAsync(async db =>
        {
            var follow = await db.Follows.SingleOrDefaultAsync(row => row.UserId == user && row.ProjectId == project.Id);
            if (follow is null) db.Follows.Add(new ProjectFollow { UserId = user, ProjectId = project.Id, Level = FollowLevel.AllActivity });
            else follow.Level = FollowLevel.AllActivity;
            db.CalendarEvents.Add(privateEvent);
            db.ActivityLog.AddRange(included.Concat([atFrom, after, own, hidden, unreadableTime, alreadyNotified, privateActivity]));
            db.Notifications.Add(new Notification { UserId = user, EventType = NotificationEvents.TaskAssigned, Title = "Synthetic existing app notice",
                ProjectId = project.Id, CorrelationId = alreadyNotified.CorrelationId, CreatedAt = request.Window.Through, UpdatedAt = request.Window.Through });
            return await db.SaveChangesAsync();
        });
        var before = State(); var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Assert.Contains("PROJECT UPDATES (7)", result.BodyText);
        Ids([project.Id], result.Scope.ProjectIds); Ids([project.Id], result.RenderedPolicy.UpdateProjectIds);
        var witness = Assert.Single(result.RenderedPolicy.UpdateActivity);
        Assert.Equal(project.Id, witness.ProjectId); Ids(included.Select(row => row.Id), witness.ActivityIds);
        Assert.DoesNotContain(included[6].ItemName!, result.BodyText); // Its count still requires the sixth/seventh facts.
        foreach (var excluded in new[] { atFrom, after, own, hidden, unreadableTime, alreadyNotified, privateActivity })
            Assert.DoesNotContain(excluded.ItemName!, result.BodyText);
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Planning_scope_uses_actual_recipient_created_entry_ids_including_the_bounded_count_overflow()
    {
        await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json();
        var sam = data.User(TestData.Sam); var alex = data.User(TestData.Alex); var request = Request(sam, "planning");
        var week = Workload.WeekOf(request.Today);
        var entries = Enumerable.Range(1, 12).Select(i => new PlanningEntry
        {
            Id = Guid.Parse($"10000000-0000-0000-0000-{i:000000000000}"), PersonId = alex, CreatedBy = sam,
            HoursPerWeek = 2, StartWeek = week, EndWeek = week, Label = $"Synthetic stale plan {i:00}", LastValidatedAt = request.ReadAtUtc.AddDays(-40),
        }).ToArray();
        var foreign = new PlanningEntry { PersonId = alex, CreatedBy = data.User(TestData.Marc), HoursPerWeek = 2,
            StartWeek = week, EndWeek = week, Label = "Synthetic other creators plan", LastValidatedAt = request.ReadAtUtc.AddDays(-40) };
        // Historical imported plan fixtures retain their old validation date and distinct creator.
        await f.DbAsync(async db => { db.PlanningEntries.AddRange(entries.Concat([foreign])); return await db.SaveChangesAsync(); });
        var before = State(); var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Ids(entries.Take(11).Select(row => row.Id), result.Scope.PlanningEntryIds);
        Assert.Empty(result.Scope.ProjectIds);
        Assert.Contains("(11)", result.BodyText); Assert.DoesNotContain(entries[10].Label, result.BodyText);
        Assert.DoesNotContain(entries[11].Label, result.BodyText); Assert.DoesNotContain(foreign.Label, result.BodyText);
        Assert.Equal(new[] { "planning" }, result.RenderedPolicy.RenderedSectionCodes.ToArray());
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Held_parents_are_ungrouped_capped_at_ten_and_reserve_only_actual_printed_intents_and_bulk_subsets()
    {
        var project = await data.Project(); var overflowProject = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { name = "Synthetic ordinary task", assigneeId = user, dueDate = "2026-09-10" });
        await f.Evaluate(project.Id);
        var request = Request(user, "overdue"); var planning = Guid.NewGuid();
        var candidates = Enumerable.Range(0, 11).Select(i => new NoticeHeldCandidate(Guid.NewGuid(), user,
            request.Window.From.AddDays(-1).AddMinutes(i), $"Synthetic held parent {i:00}", $"Synthetic admitted body {i:00}", "/my-work",
            i == 10 ? new NoticeRequiredScope([overflowProject.Id], []) : new NoticeRequiredScope([project.Id], i == 1 ? [planning] : []), null)).ToArray();
        // These are explicit fake, already-admitted per-parent membership maps; D tests validate real origin proofs.
        candidates[0] = candidates[0] with { BulkMembership = new(candidates[0].IntentId, user, Guid.NewGuid(), new string('a', 64), [1, 3]) };
        candidates[1] = candidates[1] with { BulkMembership = new(candidates[1].IntentId, user, Guid.NewGuid(), new string('b', 64), [2]) };
        var future = new NoticeHeldCandidate(Guid.NewGuid(), user, request.Window.Through.AddMilliseconds(1),
            "Synthetic future held parent", "Synthetic future body", null, new([overflowProject.Id], []), null);
        request = request with { Held = candidates.Reverse().Append(future).ToImmutableArray() };
        var before = State(); var result = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Ids(candidates.Take(10).Select(row => row.IntentId), result.Membership.IntentIds);
        Assert.Equal(2, result.Membership.BulkMemberships.Length);
        foreach (var expected in candidates.Take(2).Select(row => row.BulkMembership!))
        {
            var actual = Assert.Single(result.Membership.BulkMemberships, row => row.IntentId == expected.IntentId);
            Assert.Equal(expected.CommandId, actual.CommandId); Assert.Equal(expected.OriginHash, actual.OriginHash);
            Assert.Equal(expected.ObligationKeys.ToArray(), actual.ObligationKeys.ToArray());
        }
        foreach (var parent in candidates.Take(10)) Assert.Contains($"- {parent.Subject}{Environment.NewLine}", result.BodyText);
        Assert.Contains("HELD UPDATES (10)", result.BodyText);
        Assert.DoesNotContain(candidates[10].Subject, result.BodyText); Assert.DoesNotContain(future.Subject, result.BodyText);
        Assert.Contains("Synthetic ordinary task", result.BodyText);
        Ids([project.Id], result.Scope.ProjectIds); Ids([planning], result.Scope.PlanningEntryIds);
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task All_twenty_off_never_bypasses_with_held_or_admin_content_and_admin_output_has_an_explicit_witness()
    {
        await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json();
        var user = data.User(TestData.Admin); var request = Request(user, "staff");
        var held = new NoticeHeldCandidate(Guid.NewGuid(), user, request.Window.Through,
            "Synthetic held-only update", "Synthetic admitted held body", null, new([], []), null);
        var before = State();
        Assert.Null(await Build(request)); // Enabled switches with genuinely no content remain empty.
        var heldOnly = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request with { Held = [held] }));
        Ids([held.IntentId], heldOnly.Membership.IntentIds); Assert.Empty(heldOnly.RenderedPolicy.RenderedSectionCodes);
        Assert.False(heldOnly.RenderedPolicy.IncludesAdminReview);
        Assert.Null(await Build(request with { SectionsOff = Only(), Held = [held] }));
        Assert.Null(await Build(request with { Settings = Settings(people: false), Held = [held] }));
        request = request with { Settings = Settings(adminReview: true) };
        var admin = Assert.IsType<NoticeDigestRenderedPayload>(await Build(request));
        Assert.True(admin.RenderedPolicy.IncludesAdminReview);
        Assert.Empty(admin.Scope.ProjectIds); Assert.Empty(admin.Scope.PlanningEntryIds);
        Assert.Empty(admin.RenderedPolicy.RenderedSectionCodes); Assert.Contains("/admin/review", admin.BodyText);
        Assert.Null(await Build(request with { SectionsOff = Only(), Held = [held] }));
        var legacy = await f.DbAsync(db => Digest.Build(db, user, request.FirstName, request.Today,
            request.Window.From, request.ReadAtUtc, request.Settings, request.BaseUrl, Only()));
        Assert.NotNull(legacy); // Legacy Build's unswitched line remains intact; its job still owns the all-off gate.
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Malformed_cross_recipient_or_duplicate_held_bindings_refuse_without_mutating_delivery_state()
    {
        await (await f.As(TestData.Alex).GetAsync("/api/v1/me")).Json();
        var user = data.User(TestData.Alex); var request = Request(user, "overdue");
        var held = new NoticeHeldCandidate(Guid.NewGuid(), user, request.Window.Through,
            "Synthetic admitted parent", "Synthetic admitted body", "/my-work", new([], []), null);
        var before = State();
        NoticeDigestBuildRequest[] invalid =
        [
            request with { Window = new(request.Window.Through.AddMinutes(1), request.Window.Through) },
            request with { ReadAtUtc = request.Window.Through.AddMilliseconds(-1) },
            request with { Held = [held with { UserId = data.User(TestData.Jill) }] },
            request with { Held = [held, held] },
            request with { Held = [held with { Scope = new([Guid.Empty], []) }] },
            request with { Held = [held with { LinkPath = "//other.test/private" }] },
            request with { Held = [held with { BulkMembership = new(held.IntentId, data.User(TestData.Jill), Guid.NewGuid(), new string('c', 64), [1]) }] },
        ];
        foreach (var binding in invalid) await Assert.ThrowsAsync<ArgumentException>(() => Build(binding));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => f.DbAsync(db => Digest.Build(db, request, cancellation.Token)));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Weekly_creation_is_Monday_only_but_an_existing_Monday_slot_composes_on_Tuesday_with_its_captured_basis()
    {
        var project = await data.Project(); var alex = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = alex, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        var user = await EnableScheduling(frequency: NoticeDigestFrequency.Weekly);
        var monday = f.Clock.Now;
        f.Clock.Now = monday.AddDays(1);
        var before = State(); Assert.Equal("NotCreationDay", (await Due(user)).CreationDecision);
        Assert.Null(await Queue(user)); Assert.Empty(Slots(user)); Assert.Equal(before, State());
        f.Clock.Now = monday;
        var slot = Assert.IsType<NoticeDeliverySlot>(await Recipient(user, context => NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now)));
        Assert.Equal(monday.AddDays(-7), slot.WindowFrom); Assert.Equal(monday, slot.WindowThrough);
        Assert.Null(f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
        f.Clock.Now = monday.AddDays(1);
        // Today's edited time/zone cannot rewrite the existing Monday slot's UTC schedule or immutable window.
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "23:00", holdDigestWhenAway = false })).EnsureSuccessStatusCode();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "America/Halifax" })).EnsureSuccessStatusCode();
        var due = await Due(user);
        Assert.Equal("NotCreationDay", due.CreationDecision);
        Assert.Equal(NoticeDigestWorkKind.Compose, Assert.Single(due.Existing).Work);
        var result = await Recipient(user, context => NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test"));
        Assert.Equal(NoticeDigestCompositionDecision.Queued, result.Decision);
        var saved = Assert.Single(Slots(user));
        Assert.Equal(slot.LocalDate, saved.LocalDate); Assert.Equal(slot.OrgTimeZoneId, saved.OrgTimeZoneId);
        Assert.Equal(slot.ScheduledAtUtc, saved.ScheduledAtUtc); Assert.Equal(slot.WindowFrom, saved.WindowFrom); Assert.Equal(slot.WindowThrough, saved.WindowThrough);
        Assert.Equal(monday, f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
        var email = Assert.Single(LinkedEmails(user));
        Assert.True((await FailWithFakeProvider(user, email.Id)).Applied);
        Assert.DoesNotContain((await Due(user)).Existing, value => value.SlotId == slot.Id); // Backoff is filtered before the bounded existing-work read.
        f.Clock.Now = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == email.Id).NextAttemptAt!.Value);
        var retry = await Due(user);
        Assert.Equal("NotCreationDay", retry.CreationDecision);
        Assert.Equal(NoticeDigestWorkKind.DispatchExisting, Assert.Single(retry.Existing).Work);
        Assert.True((await FailWithFakeProvider(user, email.Id)).Applied);
        saved = Assert.Single(Slots(user)); Assert.Equal(1, saved.CompositionVersion); Assert.Equal(2, saved.ExternalAttempts);
    }

    [Fact]
    public async Task Scheduling_readers_do_not_create_defaults_and_all_off_disabled_no_address_and_known_away_hold_creation()
    {
        var user = await EnableScheduling();
        await f.DbAsync(db => db.UserSettings.Where(row => row.UserId == user).ExecuteDeleteAsync());
        var before = State();
        Assert.Equal("Due", (await Due(user)).CreationDecision); Assert.Equal(before, State());
        Assert.False(f.Db(db => db.UserSettings.Any(row => row.UserId == user)));
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "07:00", sectionsOff = Digest.SectionCodes, holdDigestWhenAway = false })).EnsureSuccessStatusCode();
        before = State(); Assert.Equal("AllSectionsOff", (await Due(user)).CreationDecision); Assert.Null(await Queue(user)); Assert.Equal(before, State());
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = false, time = "07:00", sectionsOff = Only("overdue").ToArray() })).EnsureSuccessStatusCode();
        before = State(); Assert.Equal("Disabled", (await Due(user)).CreationDecision); Assert.Null(await Queue(user)); Assert.Equal(before, State());
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "07:00", holdDigestWhenAway = false })).EnsureSuccessStatusCode();
        // Synthetic imported missing-address condition; no address is invented and no settings defaults are added by the readers.
        await f.DbAsync(async db => { (await db.Users.SingleAsync(row => row.Id == user)).Email = ""; return await db.SaveChangesAsync(); });
        before = State(); Assert.Equal("NoAddress", (await Due(user)).CreationDecision); Assert.Null(await Queue(user)); Assert.Equal(before, State());
        await f.DbAsync(async db => { (await db.Users.SingleAsync(row => row.Id == user)).Email = TestData.Alex; return await db.SaveChangesAsync(); });
        (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "07:00", holdDigestWhenAway = true })).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = user, WorkDate = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime),
                AvailableHours = 0, Category = AvailabilityCategory.Unavailable });
            return await db.SaveChangesAsync();
        });
        before = State(); Assert.Equal("HeldAway", (await Due(user)).CreationDecision); Assert.Null(await Queue(user)); Assert.Equal(before, State());
        Assert.Empty(Slots(user)); Assert.Empty(LinkedEmails(user));
    }

    [Fact]
    public async Task Concurrent_same_recipient_creation_and_queue_share_one_slot_email_and_preparation_watermark()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling();
        var results = await Task.WhenAll(Queue(user), Queue(user));
        Assert.Contains(results, result => result?.Decision == NoticeDigestCompositionDecision.Queued);
        var slot = Assert.Single(Slots(user)); var email = Assert.Single(LinkedEmails(user));
        Assert.Equal(1, slot.CompositionVersion); Assert.Equal(0, slot.ExternalAttempts);
        Assert.Equal(slot.Id, email.NoticeSlotId); Assert.Equal(1, email.NoticeCompositionVersion);
        Assert.Equal(slot.WindowThrough, f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
        Assert.Null(email.ProviderAcceptedAt); Assert.Null(email.SentAt); Assert.Null(email.AcceptanceUncertainAt);
    }

    [Fact]
    public async Task Tracked_slot_transfer_queue_and_watermark_roll_back_together_on_owner_failure()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling(); var before = State();
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(db => Tx.Run<int>(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var slot = (await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now))!;
            Assert.Equal(NoticeDigestCompositionDecision.Queued,
                (await NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test")).Decision);
            await db.SaveChangesAsync(); // Even a successful SQL save is still inside the owner's rollback boundary.
            throw new InvalidOperationException("Synthetic owner failure after staging queue");
        })));
        Assert.Equal(before, State()); Assert.Empty(Slots(user)); Assert.Empty(LinkedEmails(user));
    }

    [Fact]
    public async Task A_foreign_slot_or_expired_recipient_context_cannot_compose_or_mutate_any_delivery_state()
    {
        var user = await EnableScheduling();
        var slot = (await Recipient(user, context => NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now)))!;
        var jill = data.User(TestData.Jill); var before = State();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Recipient(jill,
            context => NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test")));
        var expired = await Recipient(user, context => Task.FromResult(context), save: false);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => NotificationDigestComposition.ComposeAsync(expired, slot.Id, f.Clock.Now, "https://hub.test"));
        Assert.Equal(before, State()); Assert.Empty(LinkedEmails(user));
    }

    [Fact]
    public async Task Real_held_capture_reserves_ten_parent_intents_and_leaves_the_eleventh_unreserved_without_claiming_acceptance()
    {
        var project = await data.Project(); var user = await EnableScheduling(sections: Only("milestones").ToArray());
        var preferences = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/notice-preferences")).Json();
        await (await f.As(TestData.Alex).Patch("/api/v1/me/notice-preferences", new { revision = preferences.I("revision"), holdImmediateEmailWhenAway = true })).Json();
        // Synthetic planner fixture establishes an actual known-away day before normal task-assignment producer calls.
        var day = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = user, WorkDate = day,
            AvailableHours = 0, Category = AvailabilityCategory.Unavailable }); return await db.SaveChangesAsync(); });
        for (var i = 0; i < 11; i++) await data.NewTask(project.Id, extra: new { name = $"Synthetic held assignment {i:00}", assigneeId = user });
        var intents = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Where(row => row.UserId == user && row.EventType == NotificationEvents.TaskAssigned).OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToArray());
        Assert.Equal(11, intents.Length); Assert.All(intents, row => Assert.Equal(NoticeIntentDisposition.HeldAway, row.Disposition));
        var apps = f.Db(db => JsonSerializer.Serialize(db.Notifications.AsNoTracking().Where(row => row.UserId == user).OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Title, row.Count, row.EmailedAt, row.DigestIncludedAt }).ToArray()));
        await f.DbAsync(db => db.AvailabilityOverrides.Where(row => row.PersonId == user && row.WorkDate == day).ExecuteDeleteAsync());
        var result = Assert.IsType<NoticeDigestCompositionResult>(await Queue(user));
        Assert.Equal(NoticeDigestCompositionDecision.Queued, result.Decision); Ids(intents.Take(10).Select(row => row.Id), result.RenderedIntentIds);
        var email = Assert.Single(LinkedEmails(user));
        Ids(intents.Take(10).Select(row => row.Id), email.RenderedOccurrenceIds!); Assert.Contains("HELD UPDATES (10)", email.BodyText);
        var overflow = f.Db(db => db.NoticeDeliveryIntents.AsNoTracking().Single(row => row.Id == intents[10].Id));
        Assert.Null(overflow.DeliveryEmailId); Assert.Equal(NoticeIntentDisposition.HeldAway, overflow.Disposition);
        Assert.Null(email.ProviderAcceptedAt); Assert.Null(email.SentAt);
        Assert.Equal(apps, f.Db(db => JsonSerializer.Serialize(db.Notifications.AsNoTracking().Where(row => row.UserId == user).OrderBy(row => row.Id)
            .Select(row => new { row.Id, row.Title, row.Count, row.EmailedAt, row.DigestIncludedAt }).ToArray())));
    }

    [Fact]
    public async Task Safe_recomposition_uses_original_window_after_watermark_advance_and_omits_later_staff_history()
    {
        var project = await data.Project(); var user = await EnableScheduling(TestData.Sam, sections: Only("staff", "updates").ToArray());
        var capturedThrough = f.Clock.Now; var actor = data.User(TestData.Marc);
        var old = new Notification { UserId = user, EventType = NotificationEvents.StaffAssignment, ProjectId = project.Id,
            Title = "Synthetic captured staff history", CreatedAt = capturedThrough.AddHours(-1), UpdatedAt = capturedThrough.AddHours(-1), LinkPath = "/my-work" };
        // Deliberate imported history, with exact timestamps. The follow uses the normal stored AllActivity meaning.
        await f.DbAsync(async db =>
        {
            db.Notifications.Add(old);
            db.Follows.Add(new ProjectFollow { UserId = user, ProjectId = project.Id, Level = FollowLevel.AllActivity });
            db.ActivityLog.Add(new ActivityLog { ProjectId = project.Id, ActorUserId = actor, ActorType = "User", ItemType = ItemType.Task,
                ItemId = Guid.NewGuid(), ItemName = "Synthetic captured project update", Action = "Updated", Categories = ["status"], OccurredAt = capturedThrough.AddMinutes(-1) });
            return await db.SaveChangesAsync();
        });
        var queued = Assert.IsType<NoticeDigestCompositionResult>(await Queue(user));
        Assert.Equal(NoticeDigestCompositionDecision.Queued, queued.Decision);
        var slot = Assert.Single(Slots(user)); var original = Assert.Single(LinkedEmails(user));
        Assert.Equal(capturedThrough, f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
        Assert.Contains(old.Title, original.BodyText); Assert.Contains("PROJECT UPDATES", original.BodyText);
        f.Clock.Now = capturedThrough.AddDays(1).AddHours(2);
        var later = new Notification { UserId = user, EventType = NotificationEvents.StaffAssignment, ProjectId = project.Id,
            Title = "Synthetic later staff history", CreatedAt = capturedThrough.AddHours(1), UpdatedAt = capturedThrough.AddHours(1), LinkPath = "/my-work" };
        await f.DbAsync(async db => { db.Notifications.Add(later); return await db.SaveChangesAsync(); });
        var newer = Assert.IsType<NoticeDigestCompositionResult>(await Queue(user));
        Assert.Equal(NoticeDigestCompositionDecision.Queued, newer.Decision); Assert.NotEqual(slot.Id, newer.SlotId);
        var advancedWatermark = f.Clock.Now;
        Assert.Equal(advancedWatermark, f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
        var newerEmail = Assert.Single(LinkedEmails(user), row => row.NoticeSlotId == newer.SlotId);
        Assert.Contains(later.Title, newerEmail.BodyText); Assert.DoesNotContain(old.Title, newerEmail.BodyText);
        (await f.As(TestData.Sam).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "07:00",
            sectionsOff = Only("staff").ToArray(), holdDigestWhenAway = false })).EnsureSuccessStatusCode();
        var refusal = await Recipient(user, context => NotificationDelivery.ClaimAsync(context, original.Id, NoticeTransportKind.Smtp, NoticeClaimPurpose.Initial, f.Clock.Now));
        Assert.Equal(NoticeClaimDecision.Recompose, refusal.Decision); Assert.Null(refusal.Claim);
        var recomposed = await Recipient(user, context => NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test"));
        Assert.Equal(NoticeDigestCompositionDecision.Queued, recomposed.Decision); Assert.Equal(2, recomposed.CompositionVersion);
        var saved = Assert.Single(Slots(user), row => row.Id == slot.Id); Assert.Equal(slot.WindowFrom, saved.WindowFrom); Assert.Equal(capturedThrough, saved.WindowThrough);
        Assert.Equal(0, saved.ExternalAttempts);
        var current = Assert.Single(LinkedEmails(user), row => row.NoticeCompositionVersion == 2);
        Assert.Contains(old.Title, current.BodyText); Assert.DoesNotContain(later.Title, current.BodyText);
        Assert.DoesNotContain("PROJECT UPDATES", current.BodyText);
        Assert.Equal(original.BodyText, f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == original.Id).BodyText));
        Assert.Equal(advancedWatermark, f.Db(db => db.UserSettings.Single(row => row.UserId == user).LastDigestAt));
    }

    [Fact]
    public async Task Stale_preparation_and_removed_admission_refuse_without_committing_partial_slot_queue_or_watermark()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling();
        var prepared = await f.DbAsync(db => Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var slot = (await NotificationDigestComposition.GetOrCreateDueSlotAsync(context, f.Clock.Now))!;
            var preparation = await NotificationDelivery.PrepareDigestAsync(context, slot.Id, f.Clock.Now);
            var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context);
            var payload = (await Digest.Build(db, Request(user, "overdue") with { SlotId = slot.Id, Window = preparation.Window, Settings = policy.Settings, Held = preparation.Held }))!;
            await db.SaveChangesAsync();
            return (Preparation: preparation, Payload: payload);
        }));
        var before = State();
        await Assert.ThrowsAsync<NoticeProofException>(() => Recipient(user,
            context => NotificationDelivery.ReserveDigestAsync(context, prepared.Preparation, prepared.Payload, f.Clock.Now)));
        Assert.Equal(before, State()); Assert.Empty(LinkedEmails(user));
        await Assert.ThrowsAsync<NoticeProofException>(() => f.DbAsync(db => Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user);
            var preparation = await NotificationDelivery.PrepareDigestAsync(context, prepared.Payload.SlotId, f.Clock.Now);
            var policy = await NotificationDelivery.ReadRecipientPolicyAsync(context);
            var payload = (await Digest.Build(db, Request(user, "overdue") with { SlotId = prepared.Payload.SlotId, Window = preparation.Window, Settings = policy.Settings, Held = preparation.Held }))!;
            // A current preference withdrawal is saved inside this transaction before admission is retried.
            (await UserPreferences.GetOrCreate(db, user)).DigestEnabled = false;
            await db.SaveChangesAsync();
            return await NotificationDelivery.ReserveDigestAsync(context, preparation, payload, f.Clock.Now);
        })));
        Assert.Equal(before, State()); Assert.Empty(LinkedEmails(user));
        Assert.True(f.Db(db => db.UserSettings.Single(row => row.UserId == user).DigestEnabled));
    }

    [Fact]
    public async Task A_safely_suppressed_third_version_reaches_core_exhaustion_instead_of_resetting_or_stalling_its_slot()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling(sections: Only("overdue", "updates").ToArray());
        (await f.As(TestData.Alex).Put($"/api/v1/projects/{project.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
        // Creation/assignment history already notified Alex and is deduplicated. A real name-only edit by Marc
        // supplies independent unnotified Task activity; Task.Name is not gated by any projection flag.
        (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{task.G("id")}", new { name = "Synthetic unnotified digest revision" }, await data.TaskVersion(task))).EnsureSuccessStatusCode();
        Assert.Equal(NoticeDigestCompositionDecision.Queued, (await Queue(user))!.Decision);
        var original = Assert.Single(Slots(user));
        NoticeDigestPolicyDocument Policy(EmailMessage email) => NoticeDeliveryProof.DecodeDigestPolicy(email.NoticeDigestPolicyJson!, user,
            original.Id, email.NoticeCompositionVersion!.Value, new(original.WindowFrom!.Value, original.WindowThrough!.Value),
            new(email.RequiredProjectIds.ToImmutableArray(), email.RequiredPlanningEntryIds.ToImmutableArray()));
        var initial = Assert.Single(LinkedEmails(user));
        Assert.Equal(new[] { "overdue", "updates" }, Policy(initial).RenderedPolicy.RenderedSectionCodes.ToArray());
        Assert.Contains("OVERDUE", initial.BodyText); Assert.Contains("PROJECT UPDATES", initial.BodyText);
        foreach (var section in new[] { "overdue", "updates", "overdue" })
        {
            var current = Assert.Single(LinkedEmails(user), row => row.SuppressedAt is null);
            var off = Only(section);
            Assert.Contains(Policy(current).RenderedPolicy.RenderedSectionCodes, code => off.Contains(code));
            (await f.As(TestData.Alex).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "07:00",
                sectionsOff = off.ToArray(), holdDigestWhenAway = false })).EnsureSuccessStatusCode();
            var refusal = await Recipient(user, context => NotificationDelivery.ClaimAsync(context, current.Id, NoticeTransportKind.Smtp, NoticeClaimPurpose.Initial, f.Clock.Now));
            Assert.Equal(NoticeClaimDecision.Recompose, refusal.Decision);
            var due = await Due(user); Assert.Equal(NoticeDigestWorkKind.Recompose, Assert.Single(due.Existing).Work);
            var result = await Recipient(user, context => NotificationDigestComposition.ComposeAsync(context, original.Id, f.Clock.Now, "https://hub.test"));
            Assert.Equal(section == "overdue" && current.NoticeCompositionVersion == 3
                ? NoticeDigestCompositionDecision.Exhausted : NoticeDigestCompositionDecision.Queued, result.Decision);
        }
        var saved = Assert.Single(Slots(user));
        Assert.Equal(3, saved.CompositionVersion); Assert.Equal(0, saved.ExternalAttempts); Assert.Equal(NoticeSlotState.Exhausted, saved.State);
        Assert.Equal(original.WindowFrom, saved.WindowFrom); Assert.Equal(original.WindowThrough, saved.WindowThrough);
        Assert.Equal(3, LinkedEmails(user).Length); Assert.All(LinkedEmails(user), row => Assert.NotNull(row.SuppressedAt));
        Assert.Equal(original.WindowFrom, await Recipient(user, context => NotificationDelivery.ReadCarryForwardFromAsync(context), save: false));
    }

    async Task<NoticeCompletionResult> FailWithFakeProvider(Guid user, Guid emailId, bool uncertain = false)
    {
        var email = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == emailId));
        if (email.NextAttemptAt > f.Clock.Now) f.Clock.Now = email.NextAttemptAt.Value;
        var claim = await Recipient(user, context => NotificationDelivery.ClaimAsync(context, emailId, NoticeTransportKind.Smtp,
            email.Attempts == 0 ? NoticeClaimPurpose.Initial : NoticeClaimPurpose.KnownFailureRetry, f.Clock.Now));
        Assert.Equal(NoticeClaimDecision.Send, claim.Decision); Assert.NotNull(claim.Claim);
        // The claim reservation commits first. Fake external evidence is then completed in a separate recipient transaction.
        return await Recipient(user, context => NotificationDelivery.CompleteAsync(context, claim.Claim!, new(NoticeTransportKind.Smtp, NoticeTransportOutcome.Failed,
            f.Clock.Now, null, uncertain ? NoticeFailureCertainty.MayHaveAccepted : NoticeFailureCertainty.DefinitelyNotAccepted)));
    }
    void NextCreationDay()
    {
        var day = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) day = day.AddDays(1);
        f.Clock.Now = new DateTimeOffset(day.ToDateTime(new TimeOnly(13, 0)), TimeSpan.Zero);
    }

    [Fact]
    public async Task Safe_exhausted_leaf_carries_once_but_later_uncertainty_never_reopens_its_covered_window()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling();
        Assert.Equal(NoticeDigestCompositionDecision.Queued, (await Queue(user))!.Decision);
        var exhausted = Assert.Single(Slots(user)); var firstEmail = Assert.Single(LinkedEmails(user));
        // Explicit in-process provider observations only; no sender, SMTP connection, job or external transport is used.
        for (var attempt = 0; attempt < 8; attempt++) Assert.True((await FailWithFakeProvider(user, firstEmail.Id)).Applied);
        exhausted = Assert.Single(Slots(user)); Assert.Equal(8, exhausted.ExternalAttempts); Assert.Equal(NoticeSlotState.Exhausted, exhausted.State);
        Assert.Equal(exhausted.WindowFrom, await Recipient(user, context => NotificationDelivery.ReadCarryForwardFromAsync(context), save: false));
        NextCreationDay();
        Assert.Equal(NoticeDigestCompositionDecision.Queued, (await Queue(user))!.Decision);
        var carried = Assert.Single(Slots(user), row => row.Id != exhausted.Id);
        Assert.Equal(exhausted.WindowFrom, carried.WindowFrom);
        Assert.Equal(carried.Id, f.Db(db => db.NoticeDeliverySlots.AsNoTracking().Single(row => row.Id == exhausted.Id).CarryForwardToSlotId));
        Assert.Null(await Recipient(user, context => NotificationDelivery.ReadCarryForwardFromAsync(context), save: false));
        var carriedEmail = Assert.Single(LinkedEmails(user), row => row.NoticeSlotId == carried.Id);
        Assert.True((await FailWithFakeProvider(user, carriedEmail.Id, uncertain: true)).HasUncertainty);
        var pinnedBody = carriedEmail.BodyText;
        NextCreationDay();
        Assert.Null((await Due(user)).CarryForwardFrom);
        Assert.Equal(NoticeDigestCompositionDecision.Queued, (await Queue(user))!.Decision);
        var later = Assert.Single(Slots(user), row => row.Id != exhausted.Id && row.Id != carried.Id);
        Assert.Equal(carried.WindowThrough, later.WindowFrom);
        var pinned = f.Db(db => db.Emails.AsNoTracking().Single(row => row.Id == carriedEmail.Id));
        Assert.NotNull(pinned.AcceptanceUncertainAt); Assert.Equal(pinnedBody, pinned.BodyText);
        Assert.Null(f.Db(db => db.NoticeDeliverySlots.AsNoTracking().Single(row => row.Id == carried.Id).CarryForwardToSlotId));
    }

    [Fact]
    public async Task Digest_job_uses_fresh_under_lock_flag_even_when_its_callers_settings_cache_is_stably_off()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = false })).EnsureSuccessStatusCode();
        await using var caller = f.Services.CreateAsyncScope();
        var db = caller.ServiceProvider.GetRequiredService<HubDb>();
        Assert.False((await caller.ServiceProvider.GetRequiredService<SettingsStore>().Get(db)).IsFlagOn("people_screens"));
        // Synthetic out-of-band flag update deliberately leaves the caller's cached SettingsStore stale.
        await f.DbAsync(current => current.Settings.Where(row => row.Key == "flag.people_screens").ExecuteUpdateAsync(set => set.SetProperty(row => row.Value, "true")));
        await new DigestJob().Run(caller.ServiceProvider, CancellationToken.None);
        var digest = Assert.Single(f.Db(current => current.Emails.AsNoTracking().Where(row => row.UserId == user && row.Kind == "Digest").ToArray()));
        Assert.Equal(1, digest.NoticeProtocolVersion); Assert.NotNull(digest.NoticeSlotId); Assert.Single(Slots(user));
        Assert.Null(digest.ProviderAcceptedAt); Assert.Null(digest.SentAt);
    }

    [Fact]
    public async Task Transport_mismatch_parks_the_linked_digest_without_recomposition_carry_or_envelope_attempt_and_kind_changes()
    {
        var project = await data.Project(); var user = data.User(TestData.Alex);
        await data.NewTask(project.Id, extra: new { assigneeId = user, dueDate = "2026-09-10" }); await f.Evaluate(project.Id);
        await EnableScheduling();
        Assert.Equal(NoticeDigestCompositionDecision.Queued, (await Queue(user))!.Decision);
        var slot = Assert.Single(Slots(user)); var queued = Assert.Single(LinkedEmails(user));
        Assert.True((await FailWithFakeProvider(user, queued.Id)).Applied);
        var failed = Assert.Single(LinkedEmails(user));
        f.Clock.Now = failed.NextAttemptAt!.Value;
        static string Pinned(EmailMessage row) => JsonSerializer.Serialize(new
        {
            row.Id, row.UserId, row.Kind, row.ToAddress, row.Subject, row.BodyText, row.BodyHtml, row.CreatedAt, row.DedupKey,
            row.RequiredProjectIds, row.RequiredPlanningEntryIds, row.NoticeProtocolVersion, row.NoticeSlotId, row.NoticeCompositionVersion,
            row.RenderedOccurrenceIds, row.BulkRenderedMembershipJson, row.NoticeDigestPolicyJson, row.TransportKind, row.Attempts,
            row.NoticeClaimToken, row.NoticeClaimVersion, row.NoticeClaimedAt,
        });
        var pinned = Pinned(failed);
        // Positive fake evidence established Smtp/definitely-not-accepted. A later Graph configuration is no retry authority.
        var mismatch = await Recipient(user, context => NotificationDelivery.ClaimAsync(context, failed.Id, NoticeTransportKind.Graph,
            NoticeClaimPurpose.KnownFailureRetry, f.Clock.Now));
        Assert.Equal(NoticeClaimDecision.Suppress, mismatch.Decision); Assert.Equal("TransportChanged", mismatch.ReasonCode); Assert.Null(mismatch.Claim);
        var parked = Assert.Single(LinkedEmails(user));
        Assert.Equal(pinned, Pinned(parked)); Assert.Equal(NoticeTransportKind.Smtp, parked.TransportKind);
        Assert.Equal(1, parked.Attempts); Assert.Equal("TransportChanged", parked.LastError);
        Assert.Equal(NoticeTransportOutcome.SuppressedBeforeSend, parked.TransportOutcome); Assert.NotNull(parked.SuppressedAt);
        Assert.Null(parked.NextAttemptAt); Assert.Null(parked.ProviderAcceptedAt); Assert.Null(parked.AcceptanceUncertainAt); Assert.Null(parked.SentAt);
        var parkedSlot = Assert.Single(Slots(user));
        Assert.Equal(NoticeSlotState.Paused, parkedSlot.State); Assert.Equal(slot.Id, parkedSlot.Id);
        Assert.Equal(1, parkedSlot.CompositionVersion); Assert.Equal(1, parkedSlot.ExternalAttempts);
        Assert.Equal(slot.WindowFrom, parkedSlot.WindowFrom); Assert.Equal(slot.WindowThrough, parkedSlot.WindowThrough); Assert.Null(parkedSlot.CarryForwardToSlotId);
        var before = State();
        var due = await Due(user);
        Assert.NotEqual("Due", due.CreationDecision); Assert.Empty(due.Existing); Assert.False(due.HasMoreExisting); Assert.Null(due.CarryForwardFrom);
        Assert.Equal(NoticeDigestPreparationDecision.Dormant,
            (await Recipient(user, context => NotificationDelivery.PrepareDigestAsync(context, slot.Id, f.Clock.Now), save: false)).Decision);
        Assert.Equal(NoticeDigestCompositionDecision.Dormant,
            (await Recipient(user, context => NotificationDigestComposition.ComposeAsync(context, slot.Id, f.Clock.Now, "https://hub.test"))).Decision);
        Assert.Null(await Recipient(user, context => NotificationDelivery.ReadCarryForwardFromAsync(context), save: false));
        Assert.Equal(before, State()); Assert.Equal(pinned, Pinned(Assert.Single(LinkedEmails(user))));
        NextCreationDay();
        due = await Due(user); Assert.DoesNotContain(due.Existing, value => value.SlotId == slot.Id); Assert.Null(due.CarryForwardFrom);
        Assert.Single(Slots(user)); Assert.Single(LinkedEmails(user));
    }
}
