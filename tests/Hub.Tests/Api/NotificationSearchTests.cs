using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 020: the weekly PM summary (US1), digest sections (US2), the unread pulse (US3), and search in descriptions and
/// comments (US4).
[Collection("api")]
public sealed class NotificationSearchTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task At(int y, int m, int day, int hourUtc, Func<Task> body)
    {
        var saved = f.Clock.Now;
        f.Clock.Now = new DateTimeOffset(y, m, day, hourUtc, 0, 0, TimeSpan.Zero);
        try { await body(); } finally { f.Clock.Now = saved; }
    }

    async Task<Guid> Person(string email)
    {
        (await f.As(email).GetAsync("/api/v1/me")).EnsureSuccessStatusCode(); // first sign-in provisions the person
        return U(email);
    }

    [Fact]
    public async Task The_weekly_summary_matches_each_dashboard_and_can_be_turned_off() // US1, FR-001, SC-001
    {
        var marc = f.As(TestData.Marc);
        var projects = new List<Hub.Api.Data.Project>();
        for (var i = 0; i < 3; i++) projects.Add(await d.Project(pm: TestData.Marc));
        await d.NewTask(projects[0].Id, TestData.Marc, new { name = "Late grading plan", dueDate = "2026-09-10" });
        var held = await d.NewTask(projects[0].Id, TestData.Marc, new { name = "Survey control", dueDate = "2026-10-10" });
        await marc.Post($"/api/v1/tasks/{held.S("id")}/block", new { type = BlockType.Client, reason = "Waiting for site access", rowVersion = await d.TaskVersion(held) }).Result.Json();
        await marc.Post($"/api/v1/projects/{projects[1].Id}/decisions", new
        {
            subject = "Pavement structure", description = "Which structure", ownerUserId = U(TestData.Marc), requiredByDate = "2026-09-10", impactLevel = "High", impactDescription = "Drawings wait",
        }).Result.Json(201);
        await d.NewTask(projects[2].Id, TestData.Marc, new { name = "On track", dueDate = "2026-10-30" });
        foreach (var p in projects) await f.Evaluate(p.Id);

        var preview = await marc.GetAsync("/api/v1/me/weekly-summary").Result.Json();
        foreach (var p in projects)
        {
            var part = preview["projects"]!.AsArray().Single(x => x!.G("projectId") == p.Id)!;
            var dash = await marc.GetAsync($"/api/v1/projects/{p.Id}/dashboard").Result.Json();
            var health = (await marc.GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["health"]!;
            Assert.Equal((dash["tasks"]!["overdue"]!.I("value"), dash["tasks"]!["blocked"]!.I("value"), dash["decisions"]!["overdue"]!.I("value")),
                (part.I("overdue"), part.I("blocked"), part.I("decisionsOverdue"))); // SC-001: the dashboard's numbers
            Assert.Equal((health.S("computed"), health.S("reported")), (part.S("computed"), part.S("reported")));
        }
        var first = preview["projects"]!.AsArray().Single(x => x!.G("projectId") == projects[0].Id)!;
        Assert.Equal((1, 1), (first.I("overdue"), first.I("blocked")));
        Assert.Equal(1, preview["projects"]!.AsArray().Single(x => x!.G("projectId") == projects[1].Id)!.I("decisionsOverdue"));

        var dedup = $"weekly:{U(TestData.Marc)}:2026-09-14";
        await At(2026, 9, 14, 13, async () => // Monday, 10:00 in Halifax: the default coordination day
        {
            await f.RunJob<WeeklySummaryJob>();
            var mail = await f.DbAsync(db => db.Emails.SingleAsync(e => e.DedupKey == dedup));
            Assert.StartsWith("Hub weekly summary — ", mail.Subject);
            var visibleSummaryProjects = preview["projects"]!.AsArray().Select(x => x!.G("projectId")).Order();
            Assert.Equal(visibleSummaryProjects, mail.RequiredProjectIds.Order());
            Assert.Contains($"{projects[0].ProjectNumber} {projects[0].Name}", mail.BodyText);
            Assert.Contains("Overdue tasks: 1 · Blocked tasks: 1 · Overdue decisions: 0", mail.BodyText);
            await f.RunJob<WeeklySummaryJob>();
            Assert.Equal(1, await f.DbAsync(db => db.Emails.CountAsync(e => e.DedupKey == dedup))); // once a week
            Assert.False(await f.DbAsync(db => db.Emails.AnyAsync(e => e.UserId == U(TestData.Rita) && e.Kind == "WeeklySummary"))); // not a PM
        });

        (await marc.Put("/api/v1/me/preferences/digest", new { enabled = true, weeklySummary = false })).EnsureSuccessStatusCode();
        try
        {
            Assert.False((await marc.GetAsync("/api/v1/me/preferences").Result.Json())["digest"]!["weeklySummaryEnabled"]!.GetValue<bool>());
            await At(2026, 9, 21, 13, async () =>
            {
                await f.RunJob<WeeklySummaryJob>();
                Assert.False(await f.DbAsync(db => db.Emails.AnyAsync(e => e.DedupKey == $"weekly:{U(TestData.Marc)}:2026-09-21"))); // turned off
            });
        }
        finally { (await marc.Put("/api/v1/me/preferences/digest", new { enabled = true, weeklySummary = true })).EnsureSuccessStatusCode(); }
    }

    [Fact]
    public async Task The_weekly_summary_goes_on_the_earliest_coordination_day() // FR-001 timing
    {
        Assert.Equal(DayOfWeek.Tuesday, WeeklySummaryJob.SendDay(["Wednesday", "Tuesday"]));
        Assert.Equal(DayOfWeek.Monday, WeeklySummaryJob.SendDay(["Wednesday", null])); // a project without a coordination day meets on Monday
        Assert.Equal(DayOfWeek.Monday, WeeklySummaryJob.SendDay([]));
        Assert.Equal(DayOfWeek.Friday, WeeklySummaryJob.SendDay(["Sunday", "Friday"])); // the working week starts on Monday
        var nobody = await Person("nopm." + Guid.NewGuid().ToString("N")[..6] + "@hub.test");
        Assert.Equal(HttpStatusCode.NoContent, (await f.As(await f.DbAsync(db => db.Users.Where(u => u.Id == nobody).Select(u => u.Email).FirstAsync())).GetAsync("/api/v1/me/weekly-summary")).StatusCode);
    }

    [Fact]
    public async Task A_switched_off_digest_section_is_left_out_with_its_count() // US2, FR-002
    {
        var p = await d.Project();
        async Task<Guid> Follower(string email)
        {
            var me = await Person(email);
            await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember }, primaryDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }).Result.Json(201);
            (await f.As(email).Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
            return me;
        }
        var withUpdates = "all." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        var withoutUpdates = "some." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        var a = await Follower(withUpdates);
        var b = await Follower(withoutUpdates);
        (await f.As(withoutUpdates).Put("/api/v1/me/preferences/digest", new { enabled = true, sectionsOff = new[] { "updates" } })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(withoutUpdates).Put("/api/v1/me/preferences/digest", new { enabled = true, sectionsOff = new[] { "gossip" } })).StatusCode);
        var prefs = (await f.As(withoutUpdates).GetAsync("/api/v1/me/preferences").Result.Json())["digest"]!;
        Assert.Equal(new[] { "updates" }, prefs["sectionsOff"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Contains("updates", prefs["sections"]!.AsArray().Select(x => x!.GetValue<string>()));

        await At(2026, 10, 20, 12, async () => // Tuesday, 09:00 in Halifax
        {
            foreach (var who in new[] { a, b }) await d.NewTask(p.Id, TestData.Pm, new { name = "Grading plan", assigneeId = who, dueDate = "2026-10-15" });
            await d.NewTask(p.Id, TestData.Marc, new { name = "Someone else's work" }); // a change by another person
            await f.Evaluate(p.Id);
            await f.RunJob<DigestJob>();
            var on = await f.DbAsync(db => db.Emails.SingleAsync(e => e.UserId == a && e.Kind == "Digest"));
            var off = await f.DbAsync(db => db.Emails.SingleAsync(e => e.UserId == b && e.Kind == "Digest"));
            Assert.Contains("project update", on.Subject);
            Assert.Contains("PROJECT UPDATES", on.BodyText);
            Assert.Equal("Hub digest — 1 overdue", off.Subject); // no update count
            Assert.DoesNotContain("PROJECT UPDATES", off.BodyText);
            Assert.Contains("OVERDUE (1)", off.BodyText);
        });
    }

    [Fact]
    public async Task The_pulse_changes_when_a_notification_arrives_or_is_read() // US3, FR-003
    {
        var p = await d.Project();
        var alex = f.As(TestData.Alex);
        async Task<string> Pulse() => (await alex.GetAsync("/api/v1/me/notifications/pulse").Result.Json()).S("stamp");
        var before = await Pulse();
        Assert.Equal(before, await Pulse()); // steady while nothing happens
        await d.NewTask(p.Id, TestData.Marc, new { name = "For Alex", assigneeId = U(TestData.Alex) });
        var arrived = await Pulse();
        Assert.NotEqual(before, arrived);
        (await alex.Post("/api/v1/me/notifications/read", new { all = true })).EnsureSuccessStatusCode();
        Assert.NotEqual(arrived, await Pulse());
        Assert.Equal(0, (await alex.GetAsync("/api/v1/me/notifications/unread-count").Result.Json()).I("notifications"));
    }

    [Fact]
    public async Task Search_finds_descriptions_and_live_comments_within_what_the_person_may_see() // US4, FR-004, §18.1
    {
        var p = await d.Project();
        var phrase = "hydro pole relocation " + Guid.NewGuid().ToString("N")[..6];
        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Utility coordination" });
        var marc = f.As(TestData.Marc);
        await marc.Post($"/api/v1/items/Task/{task.S("id")}/comments", new { body = $"Ask Hydro One about the {phrase} before the 60% package, {TestData.Alex}" }).Result.Json(201);
        var gone = await marc.Post($"/api/v1/items/Task/{task.S("id")}/comments", new { body = $"Old thought on the {phrase}" }).Result.Json(201);
        (await marc.DeleteAsync($"/api/v1/comments/{gone.S("id")}")).EnsureSuccessStatusCode();
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Utility relocation plan", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(),
            description = $"Shows the {phrase} and the four new poles on the east side",
        }).Result.Json(201);

        var r = await f.As(TestData.Alex).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(phrase)}&limit=20").Result.Json();
        var hit = Assert.Single(r["groups"]!["comments"]!.AsArray())!; // the deleted comment never appears
        Assert.Equal((task.S("key"), "Task", "Utility coordination"), (hit.S("key"), hit.S("itemType"), hit.S("name")));
        Assert.Contains(phrase, hit.S("match"));
        var del = Assert.Single(r["groups"]!["deliverables"]!.AsArray())!;
        Assert.StartsWith("Shows the " + phrase, del.S("match"));
        Assert.Equal(1, r["counts"]!.I("comments"));
        var byName = (await f.As(TestData.Alex).GetAsync($"/api/v1/search?q=Utility%20relocation%20plan").Result.Json())["groups"]!["deliverables"]!.AsArray();
        Assert.Contains(byName, x => x!.S("name") == "Utility relocation plan" && x["match"] is null); // a name match needs no snippet

        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            var outsider = await f.As(TestData.Jill).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(phrase)}").Result.Json();
            Assert.Equal((0, 0), (outsider["counts"]!.I("comments"), outsider["counts"]!.I("deliverables"))); // §18.1: restricted stays restricted
            Assert.Equal(1, (await f.As(TestData.Alex).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(phrase)}").Result.Json())["counts"]!.I("comments")); // a member
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }

    [Fact]
    public void A_snippet_shows_the_text_around_the_match_with_mentions_as_names()
    {
        var id = Guid.NewGuid();
        var snip = Hub.Api.Features.SearchEndpoints.Snippet($"A very long preamble that goes on and on about the @[Diane Roy]({id}) note on the hydro pole relocation near MH3", "hydro pole")!;
        Assert.StartsWith("…", snip);
        Assert.EndsWith("@Diane Roy note on the hydro pole relocation near MH3", snip);
        Assert.Null(Hub.Api.Features.SearchEndpoints.Snippet("Nothing here", "hydro"));
        Assert.Null(Hub.Api.Features.SearchEndpoints.Snippet(null, "hydro"));
    }
}
