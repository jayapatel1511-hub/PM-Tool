using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// 053-S06: Home's additive count context and its two independent dark flags.
public sealed class HomeOverviewTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeOverviewTests() => d = new(f);
    public void Dispose() => f.Dispose();
    HttpClient Admin => f.As(TestData.Admin);

    async Task Set(string key, object value) => (await Admin.Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Home(string as_, string query = "") => await (await f.As(as_).GetAsync($"/api/v1/home{query}")).Json();
    async Task Flags(bool visual, bool home) { await Set("flag.visual_system", visual); await Set("flag.home_overview", home); }

    [Fact]
    public async Task Home_flags_are_independent_and_legacy_read_has_no_context()
    {
        try
        {
            foreach (var (visual, home) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                await Flags(visual, home);
                var body = await Home(TestData.Pm);
                // Identity colours are nullable project data; this assertion is intentionally about flag isolation,
                // not about manufacturing a colour for projects that do not have one.
                Assert.Equal(home, body["countContext"] is not null);
            }
        }
        finally { await Flags(false, false); }
    }

    [Fact]
    public async Task Context_uses_scoped_project_denominator_and_deterministic_top_project()
    {
        try
        {
            await Flags(false, true);
            var a = await d.Project(tweak: x => x["projectNumber"] = "P-TIE-A");
            var b = await d.Project(tweak: x => x["projectNumber"] = "P-TIE-B");
            await d.NewTask(a.Id, TestData.Pm); await d.NewTask(b.Id, TestData.Pm);
            var body = await Home(TestData.Pm, $"?projects={a.Id},{b.Id}");
            var c = body["countContext"]!;
            Assert.Equal(2, c.I("projectsInScope"));
            Assert.Equal(new[] { a.Id, b.Id }, c["projectIds"]!.AsArray().Select(x => x!.GetValue<Guid>()).Order());
            Assert.Equal("P-TIE-A", c["openTasksProject"]!.S("projectNumber"));
            Assert.Equal(1, c["openTasksProject"]!.I("count"));
        }
        finally { await Flags(false, false); }
    }

    [Fact]
    public async Task Project_status_filter_narrows_every_metric_and_deadline_read()
    {
        try
        {
            await Flags(false, true);
            var active = await d.Project();
            var held = await d.Project();
            await f.As(TestData.Pm).Post($"/api/v1/projects/{held.Id}/transition", new { toStatus = ProjectStatus.OnHold, reason = "paused for test", rowVersion = d.Version(held.Id) });
            await d.NewTask(active.Id, TestData.Pm, new { dueDate = "2026-10-31" });
            await d.NewTask(held.Id, TestData.Pm, new { dueDate = "2026-10-31" });
            var body = await Home(TestData.Pm, $"?projects={active.Id},{held.Id}&projectStatus=Active&from=2026-10-31&to=2026-10-31");
            var c = body["countContext"]!;
            Assert.Equal(["Active"], c["projectStatuses"]!.AsArray().Select(x => x!.GetValue<string>()));
            Assert.Equal(1, c.I("projectsInScope"));
            Assert.Equal(1, body["metrics"]!.I("openTasks"));
            Assert.All(body["upcomingDeadlines"]!["items"]!.AsArray(), x => Assert.Equal(active.Id, x!.G("projectId")));
        }
        finally { await Flags(false, false); }
    }

    [Fact]
    public async Task Count_context_does_not_expose_a_restricted_project_in_an_explicit_scope()
    {
        await Flags(false, true);
        var visible = await d.Project(); var hidden = await d.Project();
        await d.NewTask(visible.Id, TestData.Pm);
        await d.NewTask(hidden.Id, TestData.Pm);
        await d.NewTask(hidden.Id, TestData.Pm);
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == hidden.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        var body = await Home(TestData.Rita, $"?projects={visible.Id},{hidden.Id}");
        var context = body["countContext"]!;
        Assert.Equal(1, context.I("projectsInScope"));
        Assert.Equal(visible.Id, Assert.Single(context["projectIds"]!.AsArray())!.GetValue<Guid>());
        Assert.Equal(1, body["metrics"]!.I("openTasks"));
        Assert.DoesNotContain(hidden.Id.ToString(), body.ToJsonString());
        Assert.DoesNotContain(hidden.ProjectNumber, body.ToJsonString());
    }

    [Fact]
    public async Task Due_window_uses_setting_and_counts_backlog_without_due_date()
    {
        try
        {
            await Flags(false, true);
            await Set("home_due_window_days", 3);
            var p = await d.Project();
            var today = DateOnly.Parse((await Home(TestData.Pm, $"?projects={p.Id}"))["today"]!.GetValue<string>());
            await d.NewTask(p.Id, TestData.Pm, new { dueDate = today.ToString("yyyy-MM-dd") });
            await d.NewTask(p.Id, TestData.Pm, new { dueDate = today.AddDays(3).ToString("yyyy-MM-dd") });
            await d.NewTask(p.Id, TestData.Pm); // backlog / no due date
            var body = await Home(TestData.Pm, $"?projects={p.Id}");
            var c = body["countContext"]!;
            Assert.Equal(3, c.I("dueWithinDays"));
            Assert.Equal(2, c.I("dueWithinTasks"));
            Assert.Equal(1, c.I("openTasksWithoutDueDate"));
            var tasks = await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks?projects={p.Id}&open=true&withoutDueDate=true")).Json();
            Assert.Equal(c.I("openTasksWithoutDueDate"), tasks.I("totalCount"));
        }
        finally { await Set("home_due_window_days", 7); await Flags(false, false); }
    }

    [Fact]
    public async Task Setting_defaults_to_seven_and_is_hidden_when_home_flag_is_off()
    {
        try
        {
            await Set("home_due_window_days", 7); await Flags(false, false);
            var rows = (await (await Admin.GetAsync("/api/v1/admin/settings")).Json()).AsArray();
            Assert.DoesNotContain(rows, x => x!["key"]!.GetValue<string>() == "home_due_window_days");
            await Flags(false, true);
            var body = await Home(TestData.Pm);
            Assert.Equal(7, body["countContext"]!.I("dueWithinDays"));
        }
        finally { await Set("home_due_window_days", 7); await Flags(false, false); }
    }

    [Fact]
    public async Task Home_flag_counts_all_upcoming_deadlines_beyond_the_display_cap()
    {
        try
        {
            await Flags(false, true);
            var p = await d.Project();
            var today = DateOnly.Parse((await Home(TestData.Pm, $"?projects={p.Id}"))["today"]!.GetValue<string>());
            for (var i = 0; i < 105; i++) await d.NewTask(p.Id, TestData.Pm, new { dueDate = today.ToString("yyyy-MM-dd") });
            var body = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}");
            Assert.Equal(105, body["upcomingDeadlines"]!.I("total"));
            Assert.True(body["upcomingDeadlines"]!["truncated"]!.GetValue<bool>());
            Assert.Equal(105, body["countContext"]!.I("dueWithinTasks"));

            var first = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&deadlinePage=1&deadlinePageSize=100");
            var second = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&deadlinePage=2&deadlinePageSize=100");
            Assert.Equal((1, 100, 2), (first["upcomingDeadlines"]!.I("page"), first["upcomingDeadlines"]!.I("pageSize"), first["upcomingDeadlines"]!.I("totalPages")));
            Assert.Equal((2, 100, 2), (second["upcomingDeadlines"]!.I("page"), second["upcomingDeadlines"]!.I("pageSize"), second["upcomingDeadlines"]!.I("totalPages")));
            var firstIds = first["upcomingDeadlines"]!["items"]!.AsArray().Select(x => x!.G("id")).ToHashSet();
            var secondIds = second["upcomingDeadlines"]!["items"]!.AsArray().Select(x => x!.G("id")).ToHashSet();
            Assert.Equal(100, firstIds.Count); Assert.Equal(5, secondIds.Count); Assert.Empty(firstIds.Intersect(secondIds));
            var small1 = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&deadlinePage=1&deadlinePageSize=2");
            var small2 = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&deadlinePage=2&deadlinePageSize=2");
            var smallIds = small1["upcomingDeadlines"]!["items"]!.AsArray().Concat(small2["upcomingDeadlines"]!["items"]!.AsArray()).Select(x => x!.G("id")).ToArray();
            Assert.Equal(4, smallIds.Distinct().Count());
            var clamped = await Home(TestData.Pm, $"?projects={p.Id}&from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}&deadlinePage={int.MaxValue}&deadlinePageSize=2");
            Assert.Equal(53, clamped["upcomingDeadlines"]!.I("page"));
            Assert.Single(clamped["upcomingDeadlines"]!["items"]!.AsArray());
            await Flags(false, false);
            var legacy = await Home(TestData.Pm, $"?projects={p.Id}&deadlinePage=2&deadlinePageSize=100");
            Assert.Null(legacy["upcomingDeadlines"]!["page"]);
            Assert.Null(legacy["upcomingDeadlines"]!["pageSize"]);
            Assert.Null(legacy["upcomingDeadlines"]!["totalPages"]);
        }
        finally { await Flags(false, false); }
    }

    [Fact]
    public async Task Deadline_pages_merge_all_three_types_without_holes()
    {
        await Flags(false, true);
        var p = await d.Project();
        var today = DateOnly.Parse((await Home(TestData.Pm, $"?projects={p.Id}"))["today"]!.GetValue<string>());
        var task = await d.NewTask(p.Id, TestData.Pm, new { dueDate = today.ToString("yyyy-MM-dd") });
        var deliverable = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Synthetic due deliverable", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), dueDate = today.ToString("yyyy-MM-dd")
        })).Json(201);
        var milestone = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new
        {
            name = "Synthetic due milestone", milestoneType = "Kickoff", date = today.ToString("yyyy-MM-dd")
        })).Json(201);
        var first = (await Home(TestData.Pm, $"?projects={p.Id}&deadlinePage=1&deadlinePageSize=2"))["upcomingDeadlines"]!;
        var second = (await Home(TestData.Pm, $"?projects={p.Id}&deadlinePage=2&deadlinePageSize=2"))["upcomingDeadlines"]!;
        Assert.Equal(3, first.I("total"));
        Assert.Equal(2, first["items"]!.AsArray().Count); Assert.Single(second["items"]!.AsArray());
        var ids = first["items"]!.AsArray().Concat(second["items"]!.AsArray()).Select(x => x!.G("id")).ToArray();
        Assert.Equal(3, ids.Distinct().Count());
        Assert.Equal(new[] { task.G("id"), deliverable.G("id"), milestone.G("id") }.Order(), ids.Order());
    }

    [Fact]
    public async Task Home_disciplines_count_distinct_ids_and_counts_null_once()
    {
        try
        {
            await Flags(false, true);
            var p = await d.Project();
            var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill); var marc = d.User(TestData.Marc);
            var first = new Discipline { Name = "Same discipline", Code = "SD-A", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            var second = new Discipline { Name = "Same discipline", Code = "SD-B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
            Guid? oldAlex = null, oldJill = null, oldMarc = null;
            await f.DbAsync(async db =>
            {
                db.Disciplines.AddRange(first, second);
                var users = await db.Users.Where(u => u.Id == alex || u.Id == jill || u.Id == marc).ToListAsync();
                oldAlex = users.Single(u => u.Id == alex).HomeDisciplineId; oldJill = users.Single(u => u.Id == jill).HomeDisciplineId; oldMarc = users.Single(u => u.Id == marc).HomeDisciplineId;
                users.Single(u => u.Id == alex).HomeDisciplineId = first.Id;
                users.Single(u => u.Id == jill).HomeDisciplineId = second.Id;
                users.Single(u => u.Id == marc).HomeDisciplineId = null;
                var memberIds = await db.ProjectMembers.Where(m => m.ProjectId == p.Id).Select(m => m.UserId).ToListAsync();
                foreach (var u in await db.Users.Where(u => memberIds.Contains(u.Id) && u.Id != alex).ToListAsync()) u.HomeDisciplineId = null;
                db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = jill, Roles = ["TeamMember"], AddedAt = DateTimeOffset.UtcNow });
                return await db.SaveChangesAsync();
            });
            var body = await Home(TestData.Pm, $"?projects={p.Id}");
            Assert.Equal(3, body["countContext"]!.I("homeDisciplineCount"));
            await f.DbAsync(async db =>
            {
                var users = await db.Users.Where(u => u.Id == alex || u.Id == jill || u.Id == marc).ToListAsync();
                users.Single(u => u.Id == alex).HomeDisciplineId = oldAlex; users.Single(u => u.Id == jill).HomeDisciplineId = oldJill; users.Single(u => u.Id == marc).HomeDisciplineId = oldMarc;
                return await db.SaveChangesAsync();
            });
        }
        finally { await Flags(false, false); }
    }
}
