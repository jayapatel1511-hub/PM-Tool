using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 006: comments and mentions (AC-COM-01..05), document links (AC-DOC-01..03), notifications and preferences
/// (AC-NOT-01/02/05), following (AC-ASG-02/03/04/06) and the daily digest (AC-NOT-03/04, AC-ASG-05).
[Collection("api")]
public sealed class CollaborationTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task At(int y, int m, int day, int hourUtc, Func<Task> body)
    {
        var saved = f.Clock.Now;
        f.Clock.Now = new DateTimeOffset(y, m, day, hourUtc, 0, 0, TimeSpan.Zero);
        try { await body(); } finally { f.Clock.Now = saved; }
    }

    static string Mention(string name, Guid id) => $"@[{name}]({id})";

    Task<JsonNode> Comments(string as_, string type, JsonNode item) => f.As(as_).GetAsync($"/api/v1/items/{type}/{item.S("id")}/comments").Result.Json();

    [Fact]
    public async Task Mention_notifies_and_watches_edit_window_and_pm_removal() // AC-COM-01, AC-COM-02, AC-COM-03, C-05, C-06
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        var alex = f.As(TestData.Alex);
        var posted = await alex.Post($"/api/v1/items/Task/{t.S("id")}/comments", new { body = $"Can you check the profile, {Mention("Diane Roy", U(TestData.Diane))}? **Urgent**" }).Result.Json(201);
        var (mention, watcher, logged) = await f.DbAsync(async db => (
            await db.Notifications.AnyAsync(n => n.UserId == U(TestData.Diane) && n.EventType == NotificationEvents.Mention && n.ItemId == t.G("id")),
            await db.Watchers.AnyAsync(w => w.ItemId == t.G("id") && w.UserId == U(TestData.Diane) && w.Source == "Mention"),
            await db.ActivityLog.AnyAsync(a => a.ItemId == posted.G("id") && a.Action == "MentionedNonMember")));
        Assert.True(mention);
        Assert.True(watcher);
        Assert.True(logged); // Diane is not on this project's team
        Assert.False(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Diane))));
        Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Pm) && n.EventType == NotificationEvents.CommentOnItem && n.ItemId == t.G("id")))); // the PM does not own or watch the task

        var list = await Comments(TestData.Alex, "Task", t);
        var mine = list["items"]!.AsArray().Single()!;
        Assert.True(mine["canEdit"]!.GetValue<bool>());
        Assert.Equal("Diane Roy", mine["mentions"]![0]!.S("name"));

        await At(2026, 9, 14, 13, async () =>
        {
            f.Clock.Now = f.Clock.Now.AddMinutes(10);
            (await alex.Patch($"/api/v1/comments/{posted.S("id")}", new { body = "Can you check the profile? Updated." })).EnsureSuccessStatusCode();
            f.Clock.Now = f.Clock.Now.AddMinutes(6); // 16 minutes after posting
            Assert.Equal(HttpStatusCode.Forbidden, (await alex.Patch($"/api/v1/comments/{posted.S("id")}", new { body = "Too late" })).StatusCode);
        });
        Assert.NotNull((await Comments(TestData.Alex, "Task", t))["items"]![0]!["editedAt"]);

        var other = await f.As(TestData.Marc).Post($"/api/v1/items/Task/{t.S("id")}/comments", new { body = "Profile looks fine to me" }).Result.Json(201);
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.DeleteAsync($"/api/v1/comments/{other.S("id")}")).StatusCode);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/comments/{other.S("id")}")).EnsureSuccessStatusCode();
        var removed = (await Comments(TestData.Alex, "Task", t))["items"]!.AsArray().Single(x => x!.S("id") == other.S("id"))!;
        Assert.True(removed["deleted"]!.GetValue<bool>());
        Assert.True(removed["deletedByPm"]!.GetValue<bool>());
        Assert.Null(removed["body"]); // C-03: hidden from everyone but Admin
        var asAdmin = (await Comments(TestData.Admin, "Task", t))["items"]!.AsArray().Single(x => x!.S("id") == other.S("id"))!;
        Assert.Equal("Profile looks fine to me", asAdmin.S("body"));
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == other.G("id") && a.Categories.Contains("deletion"))));
    }

    [Fact]
    public async Task Status_notes_viewer_switch_and_restricted_mentions() // AC-COM-04, AC-COM-05, C-08
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress, new { comment = "Starting with the north segment" });
        var note = (await Comments(TestData.Alex, "Task", t))["items"]!.AsArray().Single()!;
        Assert.Equal(CommentKind.StatusNote, note.S("commentKind"));
        Assert.False(note["canEdit"]!.GetValue<bool>()); // only general comments are edited

        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = U(TestData.Jill), roles = new[] { ProjectRole.Viewer } }).Result.Json(201);
        Assert.True((await Comments(TestData.Jill, "Task", t))["canComment"]!.GetValue<bool>());
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { allowViewerComments = false }, d.Version(p.Id))).EnsureSuccessStatusCode();
        var viewer = await Comments(TestData.Jill, "Task", t);
        Assert.False(viewer["canComment"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Post($"/api/v1/items/Task/{t.S("id")}/comments", new { body = "Hello" })).StatusCode);
    }

    [Fact]
    public async Task Document_links_are_typed_copyable_inherited_and_soft_deleted() // AC-DOC-01..03, DOC-01..04
    {
        var p = await d.Project();
        var del = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "60% package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        var t = await d.NewTask(p.Id, TestData.Marc, new { deliverableId = del.G("id") });
        var marc = f.As(TestData.Marc);
        var sp = await marc.Post($"/api/v1/items/Deliverable/{del.S("id")}/links", new { url = "https://contoso.sharepoint.com/sites/p/Shared%20Documents/60%25" }).Result.Json(201);
        Assert.Equal(LinkType.SharePoint, sp.S("linkType"));
        Assert.Equal("60%", sp.S("title"));
        var unc = await marc.Post($"/api/v1/items/Task/{t.S("id")}/links", new { url = @"\\fileserver\projects\2026-0417\calcs", title = "Calcs" }).Result.Json(201);
        Assert.Equal(LinkType.NetworkFolder, unc.S("linkType"));
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/items/Task/{t.S("id")}/links", new { url = "ftp://files/x" })).StatusCode);

        var onTask = await f.As(TestData.Alex).GetAsync($"/api/v1/items/Task/{t.S("id")}/links").Result.Json();
        Assert.True(onTask["links"]![0]!["isNetworkPath"]!.GetValue<bool>());
        var inherited = Assert.Single(onTask["inherited"]!.AsArray())!;
        Assert.Equal(sp.S("id"), inherited.S("id"));
        Assert.False(inherited["canChange"]!.GetValue<bool>()); // DOC-04: read-only on the task

        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).DeleteAsync($"/api/v1/links/{unc.S("id")}")).StatusCode);
        (await marc.DeleteAsync($"/api/v1/links/{unc.S("id")}")).EnsureSuccessStatusCode();
        Assert.Empty((await marc.GetAsync($"/api/v1/items/Task/{t.S("id")}/links").Result.Json())["links"]!.AsArray());
        Assert.True(await f.DbAsync(db => db.DocumentLinks.IgnoreQueryFilters().AnyAsync(l => l.Id == unc.G("id") && l.DeletedAt != null)));

        var pl = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/links", new { title = "Teams", url = "https://teams.microsoft.com/l/channel/abc" }).Result.Json(201);
        Assert.Equal(LinkType.Teams, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["links"]![0]!.S("linkType"));
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/projects/{p.Id}/links/{pl.S("id")}")).EnsureSuccessStatusCode();
        Assert.Empty((await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["links"]!.AsArray());
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == pl.G("id") && a.Categories.Contains("deletion"))));
    }

    [Fact]
    public async Task Assignment_notifies_in_app_and_by_email_but_never_the_actor() // AC-NOT-01, AC-NOT-02, AC-NOT-05
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Marc, new { assigneeId = U(TestData.Alex) });
        var (app, mail) = await f.DbAsync(async db => (
            await db.Notifications.CountAsync(n => n.UserId == U(TestData.Alex) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.TaskAssigned),
            await db.Emails.CountAsync(e => e.UserId == U(TestData.Alex) && e.Subject.Contains(t.S("key")))));
        Assert.Equal(1, app);
        Assert.Equal(1, mail);
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress);
        Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Alex) && n.ItemId == t.G("id") && n.EventType != NotificationEvents.TaskAssigned)));

        var alex = f.As(TestData.Alex);
        (await alex.Put("/api/v1/me/preferences/events/Mention", new { app = true, email = false })).EnsureSuccessStatusCode();
        await f.As(TestData.Marc).Post($"/api/v1/items/Task/{t.S("id")}/comments", new { body = $"{Mention("Alex Chen", U(TestData.Alex))} please see" }).Result.Json(201);
        var (mentionApp, mentionMail) = await f.DbAsync(async db => (
            await db.Notifications.AnyAsync(n => n.UserId == U(TestData.Alex) && n.EventType == NotificationEvents.Mention && n.ItemId == t.G("id")),
            await db.Emails.AnyAsync(e => e.UserId == U(TestData.Alex) && e.Subject.Contains("mentioned"))));
        Assert.True(mentionApp);
        Assert.False(mentionMail);
        var prefs = await alex.GetAsync("/api/v1/me/preferences").Result.Json();
        var m = prefs["events"]!.AsArray().Single(x => x!.S("code") == NotificationEvents.Mention)!;
        Assert.False(m["email"]!.GetValue<bool>());
        Assert.True(m["custom"]!.GetValue<bool>());
        (await alex.DeleteAsync("/api/v1/me/preferences/events/Mention")).EnsureSuccessStatusCode();

        var count = await alex.GetAsync("/api/v1/me/notifications/unread-count").Result.Json();
        Assert.True(count.I("notifications") >= 2);
        var unread = await alex.GetAsync($"/api/v1/me/notifications?unread=true&projectId={p.Id}").Result.Json();
        var events = unread["items"]!.AsArray().Select(x => x!.S("eventType")).ToList();
        Assert.Contains(NotificationEvents.TaskAssigned, events);
        Assert.Contains(NotificationEvents.Mention, events);
        Assert.Contains(NotificationEvents.AddedToProject, events); // team membership at creation, with the follow hint
        var before = unread.I("totalCount");
        await alex.Post("/api/v1/me/notifications/read", new { ids = new[] { unread["items"]![0]!.G("id") } }).Result.Json();
        Assert.Equal(before - 1, (await alex.GetAsync($"/api/v1/me/notifications?unread=true&projectId={p.Id}").Result.Json()).I("totalCount"));
        var filtered = await alex.GetAsync($"/api/v1/me/notifications?type={NotificationEvents.Mention}&projectId={p.Id}").Result.Json();
        Assert.All(filtered["items"]!.AsArray(), x => Assert.Equal(NotificationEvents.Mention, x!.S("eventType")));
        await alex.Post("/api/v1/me/notifications/read", new { all = true }).Result.Json();
        Assert.Equal(0, (await alex.GetAsync($"/api/v1/me/notifications?unread=true&projectId={p.Id}").Result.Json()).I("totalCount"));
    }

    [Fact]
    public async Task Following_feed_counts_others_changes_collapses_bulk_and_respects_choices() // AC-ASG-02, AC-ASG-03, AC-ASG-04, AC-ASG-06
    {
        var p = await d.Project();
        var alex = f.As(TestData.Alex);
        var start = f.Clock.Now;
        void Tick() => f.Clock.Now = f.Clock.Now.AddMinutes(1); // the test clock is frozen otherwise
        try
        {
            Tick(); await alex.Post($"/api/v1/me/following/{p.Id}/read", new { }).Result.Json(204);
            Tick();
            var t = await d.NewTask(p.Id, TestData.Marc, new { dueDate = "2026-10-01" });
            (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2026-10-08" }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
            var own = await d.NewTask(p.Id, TestData.Alex, new { name = "My own note" });
            var feed = await alex.GetAsync($"/api/v1/me/following?projectId={p.Id}").Result.Json();
            var items = feed["items"]!.AsArray();
            Assert.Contains(items, x => x!["entry"]!.S("itemId") == t.S("id") && x["unread"]!.GetValue<bool>());
            Assert.DoesNotContain(items, x => x!["entry"]!.S("itemId") == own.S("id")); // own actions are not in the feed
            Assert.Equal(2, feed["projects"]!.AsArray().Single(x => x!.S("projectId") == p.Id.ToString())!.I("unread")); // created, due date changed

            var ids = new List<Guid>();
            for (var i = 0; i < 5; i++) ids.Add((await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-01" })).G("id"));
            Tick(); await alex.Post($"/api/v1/me/following/{p.Id}/read", new { }).Result.Json(204);
            Tick(); await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = ids, operation = "shiftDueDates", @params = new { days = 7 }, reason = "Survey moved a week" }).Result.Json();
            var bulk = (await alex.GetAsync($"/api/v1/me/following?projectId={p.Id}").Result.Json())["items"]!.AsArray().Where(x => x!["unread"]!.GetValue<bool>()).ToList();
            Assert.Equal(5, Assert.Single(bulk)!.I("count")); // one collapsed entry for the five changes
            Assert.True((await alex.GetAsync("/api/v1/me/notifications/unread-count").Result.Json()).I("following") >= 1);
        }
        finally { f.Clock.Now = start; }

        (await alex.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.MyItemsOnly })).EnsureSuccessStatusCode();
        var member = await f.DbAsync(db => db.ProjectMembers.FirstAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Alex)));
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/members/{member.Id}", new { roles = new[] { ProjectRole.TeamMember, ProjectRole.Reviewer } })).EnsureSuccessStatusCode();
        var follow = await f.DbAsync(db => db.Follows.FirstAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Alex)));
        Assert.Equal(FollowLevel.MyItemsOnly, follow.Level); // ASG-02: the system never overrides the user's choice
        Assert.Equal(FollowSource.Manual, follow.Source);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/projects/{p.Id}/members/{member.Id}")).EnsureSuccessStatusCode();
        Assert.True(await f.DbAsync(db => db.Follows.AnyAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Alex)))); // manual follow kept on an open project

        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = U(TestData.Jill), roles = new[] { ProjectRole.TeamMember } }).Result.Json(201);
        var jillMember = await f.DbAsync(db => db.ProjectMembers.FirstAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Jill) && m.RemovedAt == null));
        Assert.Equal(FollowSource.Assignment, (await f.DbAsync(db => db.Follows.FirstAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Jill)))).Source);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/projects/{p.Id}/members/{jillMember.Id}")).EnsureSuccessStatusCode();
        Assert.False(await f.DbAsync(db => db.Follows.AnyAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Jill)))); // assignment follow removed with the membership
    }

    [Fact]
    public async Task Setup_assignments_arrive_as_one_batch_on_activation() // AC-NOT-07, AC-ASG-01, ASG-07, §17.5
    {
        var p = await d.Project(activate: false);
        for (var i = 0; i < 3; i++) await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Alex) && n.ProjectId == p.Id && n.EventType == NotificationEvents.TaskAssigned)));
        var follow = await f.DbAsync(db => db.Follows.FirstAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Alex)));
        Assert.Equal((FollowLevel.AllActivity, FollowSource.Assignment), (follow.Level, follow.Source)); // AC-ASG-01
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = saved.AddMinutes(5);
            var pr = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json();
            await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Active", rowVersion = pr.I("rowVersion") }).Result.Json();
            var batch = await f.DbAsync(db => db.Notifications.Where(n => n.UserId == U(TestData.Alex) && n.ProjectId == p.Id && n.EventType == NotificationEvents.TaskAssigned).ToListAsync());
            Assert.Contains("3 tasks", Assert.Single(batch).Title);
            var marker = await f.DbAsync(db => db.Follows.Where(x => x.ProjectId == p.Id && x.UserId == U(TestData.Alex)).Select(x => x.LastSeenAt).FirstAsync());
            Assert.Equal(f.Clock.Now, marker); // set-up work does not arrive as a flood of unread changes
            Assert.Equal(0, (await f.As(TestData.Alex).GetAsync($"/api/v1/me/following?projectId={p.Id}").Result.Json())["projects"]!.AsArray()
                .Where(x => x!.S("projectId") == p.Id.ToString()).Sum(x => x!.I("unread")));
        }
        finally { f.Clock.Now = saved; }
    }

    async Task<Guid> Person(string email)
    {
        (await f.As(email).GetAsync("/api/v1/me")).EnsureSuccessStatusCode(); // first sign-in provisions the person
        return U(email);
    }

    [Fact]
    public async Task Digest_has_sections_counts_and_is_skipped_when_empty_or_weekend() // AC-NOT-03, AC-NOT-04, §17.3
    {
        var tester = "digest." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        var quiet = "quiet." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        var me = await Person(tester);
        var nobody = await Person(quiet);
        var p = await d.Project();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember }, primaryDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }).Result.Json(201);
        (await f.As(tester).Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.MyItemsOnly })).EnsureSuccessStatusCode(); // no project updates
        await At(2026, 10, 20, 12, async () => // Tuesday, 09:00 in Halifax
        {
            var o1 = await d.NewTask(p.Id, TestData.Pm, new { name = "Grading plan", assigneeId = me, dueDate = "2026-10-15" });
            var o2 = await d.NewTask(p.Id, TestData.Pm, new { name = "Drainage report", assigneeId = me, dueDate = "2026-10-16" });
            var blocked = await d.NewTask(p.Id, TestData.Pm, new { name = "Survey control", assigneeId = me, dueDate = "2026-11-30" });
            await f.As(TestData.Pm).Post($"/api/v1/tasks/{blocked.S("id")}/block", new { type = BlockType.Client, reason = "Waiting for site access", rowVersion = await d.TaskVersion(blocked) }).Result.Json();
            var review = await d.NewTask(p.Id, TestData.Pm, new { name = "Pavement memo", assigneeId = U(TestData.Alex), reviewerId = me, requiresReview = true, dueDate = "2026-11-02" });
            await d.Move(TestData.Alex, review, TaskStatuses.InProgress);
            await d.Move(TestData.Alex, review, TaskStatuses.ReadyForReview);
            await f.Evaluate(p.Id);
            await f.RunJob<DigestJob>();
            var mail = await f.DbAsync(db => db.Emails.SingleAsync(e => e.UserId == me && e.Kind == "Digest"));
            Assert.Equal("Hub digest — 2 overdue, 1 review, 1 blocked", mail.Subject);
            Assert.Contains("OVERDUE (2)", mail.BodyText);
            Assert.Contains("BLOCKED (1)", mail.BodyText);
            Assert.Contains("REVIEWS WAITING ON YOU (1)", mail.BodyText);
            Assert.Contains("Waiting for site access", mail.BodyText);
            Assert.Contains(o1.S("key"), mail.BodyText);
            Assert.Contains(o2.S("key"), mail.BodyText);
            Assert.False(await f.DbAsync(db => db.Emails.AnyAsync(e => e.UserId == nobody && e.Kind == "Digest"))); // AC-NOT-04
            await f.RunJob<DigestJob>();
            Assert.Equal(1, await f.DbAsync(db => db.Emails.CountAsync(e => e.UserId == me && e.Kind == "Digest"))); // one per day
        });
        await At(2026, 10, 24, 12, async () => // Saturday
        {
            await f.RunJob<DigestJob>();
            Assert.Equal(1, await f.DbAsync(db => db.Emails.CountAsync(e => e.UserId == me && e.Kind == "Digest")));
        });
    }

    [Fact]
    public async Task Project_updates_exclude_changes_that_already_notified_the_person() // AC-ASG-05, ASG-06
    {
        var email = "follower." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        var me = await Person(email);
        var p = await d.Project();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember } }).Result.Json(201);
        await At(2026, 10, 27, 12, async () => await f.RunJob<DigestJob>()); // Tuesday: sets the digest marker
        await At(2026, 10, 28, 11, async () =>
        {
            await d.NewTask(p.Id, TestData.Pm, new { name = "Traffic counts" });
            await d.NewTask(p.Id, TestData.Pm, new { name = "Signal timing" });
            await d.NewTask(p.Id, TestData.Pm, new { name = "Assigned to follower", assigneeId = me });
        });
        await At(2026, 10, 28, 12, async () =>
        {
            await f.RunJob<DigestJob>();
            var mail = await f.DbAsync(db => db.Emails.OrderByDescending(e => e.CreatedAt).FirstAsync(e => e.UserId == me && e.Kind == "Digest"));
            Assert.Contains("PROJECT UPDATES (2)", mail.BodyText);
            Assert.EndsWith("2 project updates", mail.Subject);
            Assert.DoesNotContain("Assigned to follower", mail.BodyText.Split("PROJECT UPDATES")[1]);
        });
    }
}
