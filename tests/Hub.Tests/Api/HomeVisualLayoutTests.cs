using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// 053 S07 / AC-SCR-07 and Home's 052 AC-VSN-03 semantics. Test-owned database only.
public sealed class HomeVisualLayoutTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeVisualLayoutTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(bool enabled) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.home_overview", new { value = enabled })).EnsureSuccessStatusCode();
    async Task<JsonNode> Home(Guid projectId, string query = "") => await (await f.As(TestData.Pm).GetAsync($"/api/v1/home?projects={projectId}{query}")).Json();

    [Fact]
    public async Task Overview_includes_held_work_and_new_default_order_without_changing_legacy()
    {
        var p = await d.Project();
        var discipline = d.ProjectDiscipline(p.Id, "Civil");
        await f.DbAsync(async db =>
        {
            var states = new[] { TaskStatuses.NotStarted, TaskStatuses.InProgress, TaskStatuses.ReadyForReview, TaskStatuses.Complete, TaskStatuses.OnHold };
            db.Tasks.AddRange(states.Select((status, i) => new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline, Seq = i + 1, Key = $"{p.ProjectNumber}-T{i + 1:0000}", Name = "Synthetic lane task", Status = status }));
            return await db.SaveChangesAsync();
        });
        var before = await Home(p.Id);
        Assert.Null(before["runway"]);
        Assert.Equal(4, Assert.Single(before["tasksByProject"]!.AsArray())!.I("total"));
        Assert.Equal(new[] { "metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines" }, before["layout"]!.AsArray().Select(w => w!.S("id")));

        await Flag(true);
        var enabled = await Home(p.Id);
        var project = Assert.Single(enabled["tasksByProject"]!.AsArray())!;
        Assert.Equal(5, project.I("total"));
        Assert.Equal(5, project["lanes"]!.AsArray().Sum(l => l!.I("count")));
        Assert.Equal(1, project["lanes"]!.AsArray().Single(l => l!.S("lane") == TaskStatuses.OnHold)!.I("count"));
        Assert.Equal(enabled["metrics"]!.I("openTasks"), enabled["tasksByStatus"]!.AsArray().Where(l => l!.S("lane") != "Done").Sum(l => l!.I("count")) + enabled["onHold"]!.I("count"));
        Assert.Equal(new[] { "metrics", "tasksByProject", "tasksByStatus", "weeklyCapacity", "upcomingDeadlines" }, enabled["layout"]!.AsArray().Select(w => w!.S("id")));

        await Flag(false);
        var after = await Home(p.Id);
        before.AsObject().Remove("asOf"); after.AsObject().Remove("asOf");
        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    [Fact]
    public async Task Runway_is_exact_after_list_cap_keeps_four_locators_and_limits_only_its_range()
    {
        await Flag(true);
        var p = await d.Project();
        var discipline = d.ProjectDiscipline(p.Id, "Civil");
        var deliverableType = await d.DeliverableType();
        var start = new DateOnly(2026, 10, 31); // Saturday: the default non-working calendar.
        await f.DbAsync(async db =>
        {
            db.Tasks.AddRange(Enumerable.Range(1, 105).Select(i => new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline, Seq = i, Key = $"{p.ProjectNumber}-T{i:0000}", Name = $"Synthetic runway task {i}", DueDate = start }));
            db.Tasks.Add(new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline, Seq = 106, Key = $"{p.ProjectNumber}-T0106", Name = "Synthetic later task", DueDate = start.AddDays(20) });
            db.Tasks.Add(new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline, Seq = 107, Key = $"{p.ProjectNumber}-T0107", Name = "Synthetic completed task", DueDate = start, Status = TaskStatuses.Complete });
            db.Deliverables.Add(new Deliverable { ProjectId = p.Id, ProjectDisciplineId = discipline, DeliverableTypeId = deliverableType, Seq = 1, Key = $"{p.ProjectNumber}-D0001", Name = "Synthetic runway deliverable", DueDate = start });
            db.Milestones.Add(new Milestone { ProjectId = p.Id, Seq = 1, Key = $"{p.ProjectNumber}-M0001", Name = "Synthetic runway milestone", Date = start });
            return await db.SaveChangesAsync();
        });
        var body = await Home(p.Id, "&from=2026-10-31&to=2026-11-30");
        Assert.Equal(108, body["upcomingDeadlines"]!.I("total"));
        Assert.Equal(100, body["upcomingDeadlines"]!["items"]!.AsArray().Count);
        var runway = body["runway"]!;
        Assert.Equal("2026-11-13", runway.S("to"));
        Assert.True(runway["truncatedRange"]!.GetValue<bool>());
        var days = runway["days"]!.AsArray();
        Assert.Equal(14, days.Count);
        Assert.True(days[0]!["nonWorking"]!.GetValue<bool>());
        Assert.False(days[2]!["nonWorking"]!.GetValue<bool>());
        Assert.Equal(107, days[0]!.I("total"));
        var shown = days[0]!["items"]!.AsArray();
        Assert.Equal(4, shown.Count);
        Assert.Equal(new[] { "Deliverable", "Milestone", "Task", "Task" }, shown.Select(x => x!.S("type")));
        Assert.Equal(4, shown.Select(x => x!.G("id")).Distinct().Count());
        Assert.All(shown, x => Assert.Null(x!["identityColour"]));
        Assert.All(days.Skip(1), day => Assert.Equal(0, day!.I("total")));
        var exactDay = await Home(p.Id, "&from=2026-10-31&to=2026-10-31&deadlinePage=1");
        Assert.Equal(107, exactDay["upcomingDeadlines"]!.I("total"));
        Assert.Single(exactDay["runway"]!["days"]!.AsArray());
    }

    [Fact]
    public async Task Runway_reapplies_project_access_and_status_filters_and_omits_identity_off()
    {
        await Flag(true);
        var visible = await d.Project(); var hidden = await d.Project(); var held = await d.Project();
        await d.NewTask(visible.Id, TestData.Pm, new { dueDate = "2026-10-31" });
        await d.NewTask(hidden.Id, TestData.Pm, new { name = "Hidden runway title", dueDate = "2026-10-31" });
        await d.NewTask(held.Id, TestData.Pm, new { dueDate = "2026-10-31" });
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == hidden.Id)).Visibility = Visibility.Restricted;
            (await db.Projects.SingleAsync(x => x.Id == held.Id)).Status = ProjectStatus.OnHold;
            (await db.Projects.SingleAsync(x => x.Id == visible.Id)).IdentityColour = "#1597cc";
            return await db.SaveChangesAsync();
        });
        var body = await (await f.As(TestData.Rita).GetAsync($"/api/v1/home?projects={visible.Id},{hidden.Id},{held.Id}&projectStatus=Active&from=2026-10-31&to=2026-10-31")).Json();
        Assert.DoesNotContain(hidden.Id.ToString(), body.ToJsonString());
        Assert.DoesNotContain("Hidden runway title", body.ToJsonString());
        Assert.DoesNotContain(held.Id.ToString(), body.ToJsonString());
        var item = Assert.Single(body["runway"]!["days"]!.AsArray()[0]!["items"]!.AsArray())!;
        Assert.Equal(visible.Id, item.G("projectId"));
        Assert.Null(item["identityColour"]);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.visual_system", new { value = true })).EnsureSuccessStatusCode();
        var coloured = await (await f.As(TestData.Rita).GetAsync($"/api/v1/home?projects={visible.Id},{hidden.Id},{held.Id}&projectStatus=Active&from=2026-10-31&to=2026-10-31")).Json();
        Assert.Equal("#1597cc", Assert.Single(coloured["runway"]!["days"]!.AsArray()[0]!["items"]!.AsArray())!.S("identityColour"));
    }

    [Fact]
    public async Task New_defaults_do_not_reorder_or_unhide_saved_widgets_and_restore_is_flag_aware()
    {
        var p = await d.Project(); var pm = f.As(TestData.Pm);
        await pm.Put("/api/v1/me/dashboard-layout", new { widgets = new[] { new { id = "upcomingDeadlines", hidden = false }, new { id = "tasksByStatus", hidden = true } } }).Result.Json();
        await Flag(true);
        var body = await Home(p.Id);
        Assert.Equal("upcomingDeadlines", body["layout"]![0]!.S("id"));
        Assert.Equal("tasksByStatus", body["layout"]![1]!.S("id"));
        Assert.True(body["layout"]![1]!["hidden"]!.GetValue<bool>());
        var restored = await (await pm.DeleteAsync("/api/v1/me/dashboard-layout")).Json();
        Assert.Equal(new[] { "metrics", "tasksByProject", "tasksByStatus", "weeklyCapacity", "upcomingDeadlines" }, restored["layout"]!.AsArray().Select(x => x!.S("id")));
        await Flag(false);
        var legacy = await Home(p.Id);
        Assert.Equal(new[] { "metrics", "tasksByStatus", "tasksByProject", "upcomingDeadlines" }, legacy["layout"]!.AsArray().Select(x => x!.S("id")));
    }

    [Fact]
    public async Task Empty_projects_keep_their_zero_bar_only_in_the_new_overview()
    {
        var p = await d.Project();
        Assert.Empty((await Home(p.Id))["tasksByProject"]!.AsArray());
        await Flag(true);
        var project = Assert.Single((await Home(p.Id))["tasksByProject"]!.AsArray())!;
        Assert.Equal(0, project.I("total"));
        Assert.All(project["lanes"]!.AsArray(), lane => Assert.Equal(0, lane!.I("count")));
    }
}
