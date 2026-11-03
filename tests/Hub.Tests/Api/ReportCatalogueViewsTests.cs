using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S23-A only: catalogue and parameter definitions, never counts/charts/FTE. Private disposable data.
public sealed class ReportCatalogueViewsTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public ReportCatalogueViewsTests() { d = new(f); f.Clock.SetDate(2026, 9, 14); }
    public void Dispose() => f.Dispose();
    async Task Flag(string key, bool value = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Run(string who, string code, string query = "") => await Get(who, $"/api/v1/reports/{code}{(query.Length > 0 ? "?" + query : "")}");
    async Task<JsonNode> Views(string who, string code, Guid? container = null) => await Get(who, $"/api/v1/views?listType=report:{code}{(container is { } id ? $"&projectId={id}" : "")}");
    async Task<JsonNode> Save(string who, string code, Dictionary<string, string> parameters, Guid? container = null, string scope = "Personal", string name = "Synthetic report view", bool isDefault = false, int status = 201) =>
        await (await f.As(who).Post("/api/v1/views", new { name, listType = $"report:{code}", projectId = container, scope, @params = parameters, isDefault })).Json(status);
    static Dictionary<string, string> Parameters(JsonNode run) => run["savedParameters"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!.GetValue<string>());
    static string Query(Dictionary<string, string> parameters) => string.Join("&", parameters.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

    [Fact]
    public async Task Additions_are_absent_off_and_do_not_change_existing_rows_parameters_or_export_bytes()
    {
        var p = await d.Project(); await d.NewTask(p.Id, extra: new { dueDate = new DateOnly(2026, 9, 15) });
        var query = $"projectId={p.Id}&week=2026-09-14";
        var beforeCatalogue = await Get(TestData.Pm, "/api/v1/reports"); var before = await Run(TestData.Pm, "tasks-due-this-week", query);
        var beforeCsv = await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/tasks-due-this-week/export?{query}&format=csv");
        Assert.All(beforeCatalogue.AsArray(), r => Assert.False(r!.AsObject().ContainsKey("question"))); Assert.False(before.AsObject().ContainsKey("savedParameters"));
        await Flag("capacity_screens"); var afterCatalogue = await Get(TestData.Pm, "/api/v1/reports"); var after = await Run(TestData.Pm, "tasks-due-this-week", query);
        Assert.All(afterCatalogue.AsArray(), r => {
            Assert.EndsWith("?", r!.S("question")); r.AsObject().Remove("question");
            var parameters = r["params"]!.AsArray(); var added = parameters.Where(parameter => parameter!.S("key") is "unit" or "period").ToArray();
            if (r.S("code") is "workload-employee" or "workload-discipline")
            {
                Assert.Equal(new[] { "unit", "period" }, added.Select(parameter => parameter!.S("key")));
                Assert.Equal(new[] { "hours", "weeks" }, added.Select(parameter => parameter!.S("default")));
                Assert.Equal(new[] { "hours", "fte" }, added[0]!["options"]!.AsArray().Select(option => option!.S("value")));
                Assert.Equal(new[] { "weeks", "months" }, added[1]!["options"]!.AsArray().Select(option => option!.S("value")));
                foreach (var parameter in added) parameters.Remove(parameter);
            }
            else Assert.Empty(added);
        });
        Assert.True(JsonNode.DeepEquals(beforeCatalogue, afterCatalogue));
        Assert.Equal(1, after["taskInputs"]!["unestimated"]!.I("count")); Assert.Equal(0, after["taskInputs"]!["openUndated"]!.I("count"));
        Assert.True(JsonNode.DeepEquals(after["savedParameters"], after["taskInputs"]!["parameters"]));
        after.AsObject().Remove("savedParameters"); after.AsObject().Remove("taskInputs"); Assert.True(JsonNode.DeepEquals(before, after));
        Assert.Equal(beforeCsv, await f.As(TestData.Pm).GetByteArrayAsync($"/api/v1/reports/tasks-due-this-week/export?{query}&format=csv"));
        await Flag("capacity_screens", false); Assert.True(JsonNode.DeepEquals(before, await Run(TestData.Pm, "tasks-due-this-week", query)));
        await Get(TestData.Pm, "/api/v1/views?listType=report:tasks-due-this-week", 404);
    }

    [Fact]
    public async Task Questions_decorate_only_eligible_reports_and_report_views_keep_owner_flags_and_permissions()
    {
        await Flag("capacity_screens");
        var initially = await Get(TestData.Sam, "/api/v1/reports"); Assert.DoesNotContain(initially.AsArray(), r => r!.S("code") == "team-capacity");
        await Get(TestData.Sam, "/api/v1/views?listType=report:team-capacity", 404);
        await Flag("capacity_reports"); await Flag("task_pickup");
        var sam = await Get(TestData.Sam, "/api/v1/reports");
        Assert.All(sam.AsArray(), report => {
            Assert.False(report!.S("title").StartsWith("report.", StringComparison.Ordinal));
            Assert.False(report.S("description").StartsWith("report.", StringComparison.Ordinal));
        });
        Assert.Equal("Who has room in the coming weeks?", sam.AsArray().Single(r => r!.S("code") == "team-capacity")!.S("question"));
        var alex = await Get(TestData.Alex, "/api/v1/reports"); Assert.DoesNotContain(alex.AsArray(), r => r!.S("code") == "team-capacity");
        await Save(TestData.Alex, "team-capacity", new() { ["weeks"] = "12" }, status: 403);
        await Get(TestData.Sam, "/api/v1/views?listType=report:made-up", 400);
        await Flag("capacity_reports", false); await Save(TestData.Sam, "team-capacity", new() { ["weeks"] = "12" }, status: 404);
    }

    [Fact]
    public async Task Capture_uses_actual_supported_defaults_and_normalized_values_without_ui_state()
    {
        await Flag("capacity_screens"); await Flag("capacity_reports");
        var capacity = Parameters(await Run(TestData.Sam, "team-capacity", "from=2026-09-16&weeks=12&includeMyDrafts=false&catalogue=room&viewProject=opaque&view=owner-only&panel=Task:opaque"));
        Assert.Equal(new[] { "from", "includeMyDrafts", "period", "unit", "weeks" }, capacity.Keys.Order());
        Assert.Equal(("2026-09-14", "12", "false"), (capacity["from"], capacity["weeks"], capacity["includeMyDrafts"]));
        Assert.Equal(("hours", "weeks"), (capacity["unit"], capacity["period"]));
        Assert.Equal("2026-09-14", Parameters(await Run(TestData.Pm, "tasks-due-this-week"))["week"]);
        Assert.Equal("365", Parameters(await Run(TestData.Pm, "upcoming-deliverables", "daysAhead=999"))["daysAhead"]);
        Assert.Equal("1", Parameters(await Run(TestData.Pm, "overdue-tasks", "minDaysOverdue=-3"))["minDaysOverdue"]);
        Assert.Equal("10", Parameters(await Run(TestData.Pm, "stale-work"))["days"]);
        var time = Parameters(await Run(TestData.Alex, "task-hours")); Assert.Equal(("2026-09-14", "2026-09-20", "mine"), (time["from"], time["to"], time["scope"]));
        var history = Parameters(await Run(TestData.Pm, "health-history")); Assert.Equal(("2026-07-20", "2026-09-14"), (history["from"], history["to"]));
    }

    [Fact]
    public async Task Activity_all_history_and_concrete_default_period_remain_distinct_after_save_and_copy()
    {
        var p = await d.Project(); await Flag("capacity_screens");
        var all = Parameters(await Run(TestData.Pm, "project-activity-log", $"projectId={p.Id}&history=all"));
        Assert.Equal("all", all["history"]); Assert.False(all.ContainsKey("from")); Assert.False(all.ContainsKey("to"));
        var dated = Parameters(await Run(TestData.Pm, "project-activity-log", $"projectId={p.Id}&from=2026-08-15&to=2026-09-14&history=all&catalogue=changed"));
        Assert.Equal(("2026-08-15", "2026-09-14"), (dated["from"], dated["to"])); Assert.False(dated.ContainsKey("history")); Assert.False(dated.ContainsKey("catalogue"));
        await Save(TestData.Pm, "project-activity-log", all, name: "All history"); await Save(TestData.Pm, "project-activity-log", dated, name: "Concrete month");
        var seen = (await Views(TestData.Pm, "project-activity-log"))["views"]!.AsArray(); Assert.Equal(2, seen.Count);
        Assert.Equal("all", seen.Single(v => v!.S("name") == "All history")!["params"]!.S("history"));
        f.Clock.SetDate(2026, 9, 20);
        Assert.Equal(dated.OrderBy(p => p.Key), Parameters(await Run(TestData.Marc, "project-activity-log", Query(dated))).OrderBy(p => p.Key));
    }

    [Fact]
    public async Task Personal_report_views_are_owner_only_defaults_are_report_isolated_and_saved_values_are_validated()
    {
        await Flag("capacity_screens"); await Flag("capacity_reports"); var civil = await d.Discipline("Civil");
        var saved = await Save(TestData.Sam, "team-capacity", new() { ["disciplineId"] = civil.ToString(), ["weeks"] = "12", ["includeMyDrafts"] = "false" }, isDefault: true);
        var own = Assert.Single((await Views(TestData.Sam, "team-capacity"))["views"]!.AsArray())!;
        Assert.Equal((saved.G("id"), "12", true), (own.G("id"), own["params"]!.S("weeks"), own["isDefault"]!.GetValue<bool>()));
        Assert.Empty((await Views(TestData.Lena, "team-capacity"))["views"]!.AsArray()); Assert.Empty((await Views(TestData.Sam, "allocations-by-source"))["views"]!.AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Lena).DeleteAsync($"/api/v1/views/{saved.G("id")}")).StatusCode);
        foreach (var invalid in new[] { new Dictionary<string, string> { ["weeks"] = "27" }, new() { ["weeks"] = "nonsense" }, new() { ["includeMyDrafts"] = "maybe" }, new() { ["show"] = "Invented" } })
            await Save(TestData.Sam, invalid.ContainsKey("show") ? "allocations-by-source" : "team-capacity", invalid, status: 400);
    }

    [Fact]
    public async Task Project_container_does_not_become_data_filter_and_global_discipline_is_not_a_project_discipline()
    {
        var p = await d.Project(); var civil = await d.Discipline("Civil"); await Flag("capacity_screens"); await Flag("capacity_reports");
        var saved = await Save(TestData.Pm, "team-capacity", new() { ["disciplineId"] = civil.ToString(), ["weeks"] = "12" }, p.Id, "Project");
        Assert.Empty((await Views(TestData.Alex, "tasks-due-this-week", p.Id))["views"]!.AsArray());
        await Save(TestData.Lena, "team-capacity", new() { ["weeks"] = "12" }, p.Id, "Project", status: 403);
        var visible = Assert.Single((await Views(TestData.Marc, "team-capacity", p.Id))["views"]!.AsArray())!;
        Assert.Equal(civil.ToString(), visible["params"]!.S("disciplineId")); Assert.False(visible["params"]!.AsObject().ContainsKey("projectId"));
        Assert.Equal(saved.G("id"), visible.G("id"));
        var sharedTask = await Save(TestData.Marc, "tasks-due-this-week", new() { ["week"] = "2026-09-14" }, p.Id, "Project");
        var member = Assert.Single((await Views(TestData.Alex, "tasks-due-this-week", p.Id))["views"]!.AsArray())!;
        Assert.False(member["canEdit"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/views/{sharedTask.G("id")}", new { name = "Not mine" }, sharedTask.I("rowVersion"))).StatusCode);
        await (await f.As(TestData.Pm).Patch($"/api/v1/views/{sharedTask.G("id")}", new { name = "Updated by PM" }, sharedTask.I("rowVersion"))).Json();
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Marc).Patch($"/api/v1/views/{sharedTask.G("id")}", new { name = "Stale" }, sharedTask.I("rowVersion"))).StatusCode);
    }

    [Fact]
    public async Task Saved_parameter_link_runs_under_recipient_scope_not_saved_owner_scope()
    {
        var p = await d.Project(); var civil = await d.Discipline("Civil"); await Flag("capacity_screens"); await Flag("capacity_reports");
        var query = $"disciplineId={civil}&weeks=12&from=2026-09-14";
        decimal Capacity(JsonNode report) => report["rows"]![0]!["w0"]!.GetValue<decimal>();
        var samBefore = Capacity(await Run(TestData.Sam, "team-capacity", query)); var lenaBefore = Capacity(await Run(TestData.Lena, "team-capacity", query));
        foreach (var (supervisor, hours) in new[] { (TestData.Sam, 40m), (TestData.Omar, 32m) })
        {
            var email = $"synthetic-reports-{Guid.NewGuid():N}@hub.test"; await Get(email, "/api/v1/me"); var person = d.User(email);
            await f.DbAsync(async db => { var user = await db.Users.SingleAsync(u => u.Id == person); user.SupervisorId = d.User(supervisor); user.WeeklyCapacityHours = hours;
                db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = person, Roles = [ProjectRole.TeamMember], PrimaryDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
            await d.NewTask(p.Id, extra: new { assigneeId = person, estimatedHours = 0m, dueDate = new DateOnly(2026, 9, 18) });
        }
        var ownerRun = await Run(TestData.Lena, "team-capacity", query); var parameters = Parameters(ownerRun);
        await Save(TestData.Lena, "team-capacity", parameters, name: "Civil twelve weeks");
        Assert.Empty((await Views(TestData.Sam, "team-capacity"))["views"]!.AsArray());
        var recipient = await Run(TestData.Sam, "team-capacity", Query(parameters));
        Assert.Equal(40m, Capacity(recipient) - samBefore); Assert.Equal(72m, Capacity(ownerRun) - lenaBefore);
        Assert.Equal(12, recipient["columns"]!.AsArray().Count - 1); Assert.Equal(parameters.OrderBy(p => p.Key), Parameters(recipient).OrderBy(p => p.Key));
    }

    [Fact]
    public async Task Missing_references_are_named_as_dropped_and_hidden_container_never_returns_saved_metadata()
    {
        var p = await d.Project(); var civil = await d.Discipline("Civil"); await Flag("capacity_screens"); await Flag("capacity_reports");
        await Save(TestData.Pm, "team-capacity", new() { ["disciplineId"] = civil.ToString(), ["weeks"] = "12" }, p.Id, "Project", name: "Private report sentinel");
        await f.DbAsync(async db => { (await db.Disciplines.SingleAsync(d => d.Id == civil)).IsActive = false; return await db.SaveChangesAsync(); });
        var viewed = Assert.Single((await Views(TestData.Pm, "team-capacity", p.Id))["views"]!.AsArray())!;
        Assert.Contains(viewed["dropped"]!.AsArray(), value => value!.GetValue<string>() == "disciplineId"); Assert.Null(viewed["params"]!["disciplineId"]);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var response = await f.As(TestData.Sam).GetAsync($"/api/v1/views?listType=report:team-capacity&projectId={p.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.DoesNotContain("Private report sentinel", await response.Content.ReadAsStringAsync());
    }
}
