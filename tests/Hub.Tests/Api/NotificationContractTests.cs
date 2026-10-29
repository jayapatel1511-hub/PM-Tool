using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The answers of Notifications.cs pinned before and after its typed conversion (040-S15-Notifications): property names and
/// order, nulls written, 204 with an empty body, and the refusals. The commands answering 204 record no shape, so the
/// coverage list showed them uncalled.
[Collection("api")]
public sealed class NotificationContractTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static readonly string[] PageKeys = ["items", "page", "pageSize", "totalCount", "sort", "filters"];
    static readonly string[] NotificationKeys = ["id", "eventType", "projectId", "projectNumber", "itemType", "itemId", "itemKey", "title", "body", "linkPath", "actorUserId", "actorName", "count", "createdAt", "updatedAt", "readAt"];

    static async Task<string> Body(Task<HttpResponseMessage> response) => await (await response).Content.ReadAsStringAsync();

    [Fact]
    public async Task Notification_reads_keep_their_answers_and_shape() // list, counts, pulse, read, preferences, weekly summary and the Following feed
    {
        var p = await d.Project(pm: TestData.Marc);
        var alex = f.As(TestData.Alex);
        (await alex.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
        var start = f.Clock.Now;
        f.Clock.Now = start.AddMinutes(1); // the test clock is frozen: changes must come after the read marker
        try { await Reads(p, alex); } finally { f.Clock.Now = start; }
    }

    async Task Reads(Project p, HttpClient alex)
    {
        await d.NewTask(p.Id, TestData.Marc, new { name = "For Alex", assigneeId = U(TestData.Alex) });
        await d.NewTask(p.Id, TestData.Marc, new { name = "A change Alex follows" });

        // The centre: a page of rows with every property written, nulls included; the counts and the pulse the bell polls.
        var list = await alex.GetAsync($"/api/v1/me/notifications?projectId={p.Id}&pageSize=2").Result.Json();
        Assert.Equal(PageKeys, Keys(list));
        Assert.Equal((1, 2), (list.I("page"), list.I("pageSize")));
        Assert.True(list.I("totalCount") >= 2);
        Assert.Null(list["sort"]); Assert.Null(list["filters"]);
        var row = list["items"]!.AsArray().First(x => x!.S("eventType") == NotificationEvents.TaskAssigned)!;
        Assert.Equal(NotificationKeys, Keys(row));
        Assert.Equal((p.Id, p.ProjectNumber, "Task", U(TestData.Marc), "Marc Dubois"), (row.G("projectId"), row.S("projectNumber"), row.S("itemType"), row.G("actorUserId"), row.S("actorName")));
        Assert.Equal(1, row.I("count"));
        Assert.True(row.AsObject().ContainsKey("readAt")); Assert.Null(row["readAt"]);
        var plain = await alex.GetAsync("/api/v1/me/notifications?unread=true").Result.Json();
        Assert.All(plain["items"]!.AsArray(), x => Assert.Equal(NotificationKeys, Keys(x)));
        var count = await alex.GetAsync("/api/v1/me/notifications/unread-count").Result.Json();
        Assert.Equal(["notifications", "following"], Keys(count));
        Assert.True(count.I("notifications") >= 1 && count.I("following") >= 1);
        var pulse = await alex.GetAsync("/api/v1/me/notifications/pulse").Result.Json();
        Assert.Equal(["stamp"], Keys(pulse));
        Assert.StartsWith($"{count.I("notifications")}:", pulse.S("stamp"));

        // Marking read: how many it changed, once; nothing the second time.
        var marked = await alex.Post("/api/v1/me/notifications/read", new { ids = new[] { row.G("id") } }).Result.Json();
        Assert.Equal(["marked"], Keys(marked));
        Assert.Equal(1, marked.I("marked"));
        Assert.Equal(0, (await alex.Post("/api/v1/me/notifications/read", new { ids = new[] { row.G("id") } }).Result.Json()).I("marked"));
        var read = (await alex.GetAsync($"/api/v1/me/notifications?projectId={p.Id}&pageSize=100").Result.Json())["items"]!.AsArray().Single(x => x!.G("id") == row.G("id"))!;
        Assert.NotNull(read["readAt"]);
        Assert.Equal(["marked"], Keys(await alex.Post("/api/v1/me/notifications/read", new { all = true }).Result.Json()));

        // The Following feed: the followed projects with their unread counts, and collapsed entries with the activity row.
        var feed = await alex.GetAsync($"/api/v1/me/following?projectId={p.Id}").Result.Json();
        Assert.Equal(["projects", "items", "page", "pageSize", "totalCount"], Keys(feed));
        Assert.Equal(["projectId", "projectNumber", "name", "status", "lastSeenAt", "unread"], Keys(feed["projects"]!.AsArray().Single(x => x!.G("projectId") == p.Id)));
        var item = feed["items"]!.AsArray().First(x => x!["entry"]!.S("itemType") == "Task")!;
        Assert.Equal(["entry", "count", "projectNumber", "projectName", "notified", "unread"], Keys(item));
        Assert.Equal([p.ProjectNumber, p.Name], [item.S("projectNumber"), item.S("projectName")]);
        Assert.True(item["unread"]!.GetValue<bool>());
        Assert.Equal(["id", "occurredAt", "actorId", "actorName", "actorType", "projectId", "itemType", "itemId", "itemKey", "itemName", "action", "categories", "changes", "summary", "reason", "source", "correlationId", "snapshot", "snapshotLabels"], Keys(item["entry"]));
        Assert.Equal((1, 100), (feed.I("page"), feed.I("pageSize")));
        Assert.Empty((await alex.GetAsync("/api/v1/me/following?projectId=" + Guid.NewGuid()).Result.Json())["items"]!.AsArray());

        // Preferences: events, the digest and the follows, in the order the page reads them.
        var prefs = await alex.GetAsync("/api/v1/me/preferences").Result.Json();
        Assert.Equal(["events", "digest", "follows"], Keys(prefs));
        Assert.Equal(["code", "app", "email", "defaultApp", "defaultEmail", "direct", "custom"], Keys(prefs["events"]!.AsArray()[0]));
        Assert.Equal(NotificationEvents.All.Select(e => e.Code), prefs["events"]!.AsArray().Select(x => x!.S("code")));
        Assert.Equal(["digestEnabled", "holdDigestWhenAway", "time", "orgTime", "weekendDigests", "sections", "sectionsOff", "weeklySummaryEnabled", "managesProjects"], Keys(prefs["digest"]));
        Assert.Equal(Digest.SectionCodes, prefs["digest"]!["sections"]!.AsArray().Select(x => x!.GetValue<string>()));
        var follow = prefs["follows"]!.AsArray().Single(x => x!.G("projectId") == p.Id)!;
        Assert.Equal(["projectId", "projectNumber", "name", "status", "level", "source"], Keys(follow));
        Assert.Equal((FollowLevel.AllActivity, FollowSource.Manual), (follow.S("level"), follow.S("source")));

        // The weekly summary: the object for a manager of an Active project, 204 with no body for anyone else.
        var weekly = await f.As(TestData.Marc).GetAsync("/api/v1/me/weekly-summary").Result.Json();
        Assert.Equal(["subject", "body", "projects", "requiredProjectIds"], Keys(weekly));
        Assert.Equal(["projectId", "projectNumber", "name", "computed", "reported", "nextSubmission", "nextSubmissionDate", "overdue", "blocked", "decisionsOverdue", "attention", "changes", "changesByType"],
            Keys(weekly["projects"]!.AsArray().First(x => x!.G("projectId") == p.Id)));
        var quiet = await alex.GetAsync("/api/v1/me/weekly-summary");
        Assert.Equal((HttpStatusCode.NoContent, ""), (quiet.StatusCode, await quiet.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Event_preference_density_and_digest_commands_answer_204_with_no_body() // PUT and DELETE me/preferences/events/{code}, PUT density (uncalled)
    {
        var alex = f.As(TestData.Alex);
        var me = U(TestData.Alex);
        int Rows(string code) => f.Db(db => db.NotificationPreferences.Count(x => x.UserId == me && x.EventType == code));
        var code = NotificationEvents.Mention;
        await alex.DeleteAsync($"/api/v1/me/preferences/events/{code}");

        // A channel choice is created, then changed in place; the page reads it back as custom.
        var put = await alex.Put($"/api/v1/me/preferences/events/{code}", new { app = true, email = false });
        Assert.Equal((HttpStatusCode.NoContent, ""), (put.StatusCode, await put.Content.ReadAsStringAsync()));
        Assert.Equal(1, Rows(code));
        var stored1 = f.Db(db => db.NotificationPreferences.AsNoTracking().Single(x => x.UserId == me && x.EventType == code));
        Assert.Equal((true, false), (stored1.InApp, stored1.Email));
        Assert.Equal(HttpStatusCode.NoContent, (await alex.Put($"/api/v1/me/preferences/events/{code}", new { app = false, email = true })).StatusCode);
        Assert.Equal(1, Rows(code));
        var event_ = (await alex.GetAsync("/api/v1/me/preferences").Result.Json())["events"]!.AsArray().Single(x => x!.S("code") == code)!;
        Assert.Equal((false, true, true), (event_["app"]!.GetValue<bool>(), event_["email"]!.GetValue<bool>(), event_["custom"]!.GetValue<bool>()));
        Assert.Equal(0, Rows(NotificationEvents.TaskAssigned)); // a choice is per event
        Assert.False(await f.DbAsync(db => db.NotificationPreferences.AnyAsync(x => x.UserId == U(TestData.Marc) && x.EventType == code && !x.InApp))); // and per person

        // An unknown code is refused with the field named; nothing is stored for it.
        var bad = await alex.Put("/api/v1/me/preferences/events/NotAnEvent", new { app = true, email = true });
        var problem = await bad.Json(400);
        Assert.Equal("code", problem["errors"]!.AsObject().Single().Key);
        Assert.Equal(0, Rows("NotAnEvent"));

        // Reset: back to the organisation default, and answering the same when there is nothing to reset or the code is unknown.
        var del = await alex.DeleteAsync($"/api/v1/me/preferences/events/{code}");
        Assert.Equal((HttpStatusCode.NoContent, ""), (del.StatusCode, await del.Content.ReadAsStringAsync()));
        Assert.Equal(0, Rows(code));
        Assert.False((await alex.GetAsync("/api/v1/me/preferences").Result.Json())["events"]!.AsArray().Single(x => x!.S("code") == code)!["custom"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/me/preferences/events/{code}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync("/api/v1/me/preferences/events/NotAnEvent")).StatusCode);

        // Density: 204 with no body, stored for the person and read back with the profile.
        foreach (var dense in new[] { false, true })
        {
            var density = await alex.Put("/api/v1/me/preferences/density", new { denseRows = dense });
            Assert.Equal((HttpStatusCode.NoContent, ""), (density.StatusCode, await density.Content.ReadAsStringAsync()));
            Assert.Equal(dense, (await alex.GetAsync("/api/v1/me").Result.Json())["preferences"]!["denseRows"]!.GetValue<bool>());
        }

        // The digest command answers the same way, and refuses a time that is not HH:mm with the field named.
        var digest = await alex.Put("/api/v1/me/preferences/digest", new { enabled = true, time = "08:15", sectionsOff = new[] { "updates", "updates" }, weeklySummary = true, holdDigestWhenAway = false });
        Assert.Equal((HttpStatusCode.NoContent, ""), (digest.StatusCode, await digest.Content.ReadAsStringAsync()));
        var stored = (await alex.GetAsync("/api/v1/me/preferences").Result.Json())["digest"]!;
        Assert.Equal(("08:15", true), (stored.S("time"), stored["digestEnabled"]!.GetValue<bool>()));
        Assert.Equal(["updates"], stored["sectionsOff"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("time", (await alex.Put("/api/v1/me/preferences/digest", new { enabled = true, time = "8am" }).Result.Json(400))["errors"]!.AsObject().Single().Key);
        (await alex.Put("/api/v1/me/preferences/digest", new { enabled = true })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Follow_commands_and_the_following_read_marker_keep_their_answers() // PUT and DELETE projects/{id}/follow, POST me/following/{projectId}/read (uncalled)
    {
        var p = await d.Project();
        var other = await d.Project();
        var (alex, me) = (f.As(TestData.Alex), U(TestData.Alex));
        Task<ProjectFollow?> Row(Guid id) => f.DbAsync(db => db.Follows.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id && x.UserId == me));

        // Following: 200 with the level and the source, a manual choice that sticks.
        var put = await alex.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.Muted });
        var json = await put.Json();
        Assert.Equal(["level", "source"], Keys(json));
        Assert.Equal((FollowLevel.Muted, FollowSource.Manual), (json.S("level"), json.S("source")));
        Assert.Equal((FollowLevel.Muted, FollowSource.Manual), ((await Row(p.Id))!.Level, (await Row(p.Id))!.Source));
        Assert.Equal(FollowLevel.AllActivity, (await alex.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.AllActivity }).Result.Json()).S("level"));
        Assert.Equal(1, f.Db(db => db.Follows.Count(x => x.ProjectId == p.Id && x.UserId == me)));
        Assert.Equal("level", (await alex.Put($"/api/v1/projects/{p.Id}/follow", new { level = "Everything" }).Result.Json(400))["errors"]!.AsObject().Single().Key);
        Assert.Equal(HttpStatusCode.NotFound, (await alex.Put($"/api/v1/projects/{Guid.NewGuid()}/follow", new { level = FollowLevel.AllActivity })).StatusCode);

        // The read marker moves to now for the followed project only; a project nobody follows is answered the same and left alone.
        var start = f.Clock.Now;
        try
        {
            f.Clock.Now = start.AddMinutes(30);
            (await alex.Put($"/api/v1/projects/{other.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
            var otherSeen = (await Row(other.Id))!.LastSeenAt;
            f.Clock.Now = start.AddMinutes(45);
            var read = await alex.Post($"/api/v1/me/following/{p.Id}/read", new { });
            Assert.Equal((HttpStatusCode.NoContent, ""), (read.StatusCode, await read.Content.ReadAsStringAsync()));
            Assert.Equal(f.Clock.Now, (await Row(p.Id))!.LastSeenAt);
            Assert.Equal(otherSeen, (await Row(other.Id))!.LastSeenAt);
            Assert.NotEqual(otherSeen, (await Row(p.Id))!.LastSeenAt);
            var feed = await alex.GetAsync($"/api/v1/me/following?projectId={p.Id}").Result.Json();
            Assert.Equal(0, feed["projects"]!.AsArray().Single(x => x!.G("projectId") == p.Id)!.I("unread"));
            Assert.Equal(f.Clock.Now, DateTimeOffset.Parse(feed["projects"]!.AsArray().Single(x => x!.G("projectId") == p.Id)!.S("lastSeenAt")));
            Assert.Equal(HttpStatusCode.NoContent, (await alex.Post($"/api/v1/me/following/{Guid.NewGuid()}/read", new { })).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await f.As(TestData.Jill).Post($"/api/v1/me/following/{p.Id}/read", new { })).StatusCode);
            Assert.False(await f.DbAsync(db => db.Follows.AnyAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Jill)))); // nothing is created by marking read
        }
        finally { f.Clock.Now = start; }

        // Unfollowing: 204 with no body, the row gone; the same answer when there is nothing to remove or the project is unknown.
        var del = await alex.DeleteAsync($"/api/v1/projects/{p.Id}/follow");
        Assert.Equal((HttpStatusCode.NoContent, ""), (del.StatusCode, await del.Content.ReadAsStringAsync()));
        Assert.Null(await Row(p.Id));
        Assert.NotNull(await Row(other.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/projects/{p.Id}/follow")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/projects/{Guid.NewGuid()}/follow")).StatusCode);
        Assert.Equal("", await Body(alex.DeleteAsync($"/api/v1/projects/{other.Id}/follow")));
    }

    [Fact]
    public async Task A_restricted_project_cannot_be_followed_by_someone_who_may_not_see_it() // ASG-03
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var p = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            var jill = f.As(TestData.Jill);
            Assert.Equal(HttpStatusCode.NotFound, (await jill.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.AllActivity })).StatusCode);
            Assert.False(await f.DbAsync(db => db.Follows.AnyAsync(x => x.ProjectId == p.Id && x.UserId == U(TestData.Jill))));
            Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.MyItemsOnly })).StatusCode);
            Assert.DoesNotContain(p.Id, (await jill.GetAsync("/api/v1/me/preferences").Result.Json())["follows"]!.AsArray().Select(x => x!.G("projectId")));
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }
}
