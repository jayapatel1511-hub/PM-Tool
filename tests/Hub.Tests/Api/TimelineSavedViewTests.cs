using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class TimelineSavedViewTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public TimelineSavedViewTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Get(string path, string who = TestData.Pm) => await (await f.As(who).GetAsync(path)).Json();
    async Task Flag(string key, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value })).EnsureSuccessStatusCode();
    static Dictionary<string, string> Params(string group = "discipline") => new() {
        ["group"] = group, ["cols"] = "items,dates", ["zoom"] = "month", ["layers"] = "dependencies,milestones,originalDates",
        ["hideCompleted"] = "false", ["collapsed"] = "none", ["expanded"] = "none"
    };
    async Task<JsonNode> Save(string who, string type, Guid? project, Dictionary<string, string> parameters, string scope = "Personal", bool isDefault = false) =>
        await (await f.As(who).Post("/api/v1/views", new { name = "Synthetic Timeline view " + Guid.NewGuid().ToString("N")[..6], listType = type, projectId = project, scope, @params = parameters, isDefault })).Json(201);
    async Task<JsonNode> View(Guid id, string type, Guid? project, string who = TestData.Pm) => Assert.Single((await Get($"/api/v1/views?listType={type}" + (project is { } value ? $"&projectId={value}" : ""), who))["views"]!.AsArray(), row => row!.G("id") == id)!;

    [Fact]
    public async Task Priya_shared_Civil_Months_dependencies_definition_opens_for_Marc_without_changing_the_canonical_Timeline()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil");
        var a = await data.NewTask(p.Id, extra: new { name = "Civil schedule work", assigneeId = data.User(TestData.Alex), startDate = "2026-09-14", dueDate = "2026-09-18" });
        await data.NewTask(p.Id, TestData.Omar, new { name = "Electrical schedule work", assigneeId = data.User(TestData.Omar), startDate = "2026-09-14", dueDate = "2026-09-20" }, "Electrical");
        var before = (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString(); await Flag("work_screens", true);
        var parameters = Params(); parameters["disciplineId"] = civil.ToString(); parameters["from"] = "2026-09-01"; parameters["to"] = "2026-10-31";
        parameters["panel"] = "Task:transient"; parameters["taskSection"] = "comments"; parameters["page"] = "9"; parameters["preview"] = "not-a-view-filter";
        var saved = await Save(TestData.Pm, "timeline", p.Id, parameters, "Project"); var opened = await View(saved.G("id"), "timeline", p.Id, TestData.Marc);
        Assert.Equal("Project", opened.S("scope")); Assert.True(opened["canEdit"]!.GetValue<bool>());
        Assert.Equal(civil.ToString(), opened["params"]!.S("disciplineId")); Assert.Equal("month", opened["params"]!.S("zoom")); Assert.Contains("dependencies", opened["params"]!.S("layers"));
        Assert.Equal("discipline", opened["params"]!.S("group")); Assert.Equal("items,dates", opened["params"]!.S("cols"));
        foreach (var transient in new[] { "panel", "taskSection", "page", "preview" }) Assert.Null(opened["params"]![transient]);
        var marcData = await Get($"/api/v1/projects/{p.Id}/timeline", TestData.Marc);
        Assert.Equal(a.G("id"), Assert.Single(marcData["tasks"]!.AsArray(), task => task!.G("projectDisciplineId") == civil)!.G("id"));
        Assert.Equal(before, (await Get($"/api/v1/projects/{p.Id}/timeline?zoom=quarter&layers=none&group=discipline&cols=items,dates")).ToJsonString());
    }

    [Fact]
    public async Task Deactivated_discipline_with_retained_Timeline_work_remains_a_valid_saved_filter()
    {
        var p = await data.Project(); var civil = data.ProjectDiscipline(p.Id, "Civil");
        var task = await data.NewTask(p.Id, extra: new { name = "Retained inactive discipline work", assigneeId = data.User(TestData.Alex), startDate = "2026-09-14", dueDate = "2026-09-25" });
        var version = f.Db(db => db.ProjectDisciplines.Single(discipline => discipline.Id == civil).RowVersion);
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}/disciplines/{civil}", new { isActive = false }, version)).EnsureSuccessStatusCode();
        Assert.False(f.Db(db => db.ProjectDisciplines.Single(discipline => discipline.Id == civil).IsActive));
        await Flag("work_screens", true);
        var before = await Get($"/api/v1/projects/{p.Id}/timeline", TestData.Marc);
        var retained = Assert.Single(before["tasks"]!.AsArray(), row => row!.G("id") == task.G("id"))!;
        Assert.Equal(civil, retained.G("projectDisciplineId")); Assert.Equal("2026-09-25", retained.S("dueDate"));
        var parameters = Params(); parameters["disciplineId"] = civil.ToString();
        var saved = await Save(TestData.Pm, "timeline", p.Id, parameters, "Project"); var opened = await View(saved.G("id"), "timeline", p.Id, TestData.Marc);
        Assert.Equal(civil.ToString(), opened["params"]!.S("disciplineId"));
        Assert.DoesNotContain(opened["dropped"]!.AsArray(), value => value!.GetValue<string>() == "disciplineId");
        Assert.Equal(before.ToJsonString(), (await Get($"/api/v1/projects/{p.Id}/timeline", TestData.Marc)).ToJsonString());
    }

    [Fact]
    public async Task Workspace_personal_views_keep_scope_anchor_zoom_layers_and_collapsed_projects_without_sharing()
    {
        var a = await data.Project(); var b = await data.Project(); await Flag("work_screens", true);
        var parameters = Params("project"); parameters.Remove("expanded"); parameters["projects"] = $"{a.Id},{b.Id}"; parameters["zoom"] = "quarter";
        parameters["start"] = "2026-09-01"; parameters["layers"] = "dependencies,originalDates"; parameters["collapsed"] = "all"; parameters["hideCompleted"] = "true";
        var made = await Save(TestData.Alex, "workspace-timeline", null, parameters, isDefault: true); var read = await View(made.G("id"), "workspace-timeline", null, TestData.Alex);
        foreach (var (key, value) in parameters)
        {
            if (key is "projects" or "layers") Assert.Equal(value.Split(',').Order(StringComparer.Ordinal), read["params"]!.S(key).Split(',').Order(StringComparer.Ordinal));
            else Assert.Equal(value, read["params"]!.S(key));
        }
        Assert.True(read["isDefault"]!.GetValue<bool>()); Assert.False((await Get("/api/v1/views?listType=workspace-timeline", TestData.Alex))["canShare"]!.GetValue<bool>());
        Assert.Empty((await Get("/api/v1/views?listType=workspace-timeline", TestData.Jill))["views"]!.AsArray());
        var ws = await (await f.As(TestData.Alex).Post("/api/v1/workspaces", new { name = "Timeline named scope", projectIds = new[] { a.Id, b.Id } })).Json(201);
        parameters.Remove("projects"); parameters["ws"] = ws.S("id"); var named = await Save(TestData.Alex, "workspace-timeline", null, parameters);
        Assert.Equal(ws.S("id"), (await View(named.G("id"), "workspace-timeline", null, TestData.Alex))["params"]!.S("ws"));
        Assert.False((await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Invalid workspace sharing", listType = "workspace-timeline", scope = "Project", @params = parameters })).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Existing_project_share_edit_version_and_visibility_guards_remain_authoritative()
    {
        var p = await data.Project(); await Flag("work_screens", true); var parameters = Params();
        await (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Member cannot share", listType = "timeline", projectId = p.Id, scope = "Project", @params = parameters })).Json(403);
        var saved = await Save(TestData.Pm, "timeline", p.Id, parameters, "Project"); var id = saved.G("id");
        Assert.False((await View(id, "timeline", p.Id, TestData.Alex))["canEdit"]!.GetValue<bool>());
        await (await f.As(TestData.Alex).Patch($"/api/v1/views/{id}", new { name = "Forbidden edit" }, saved.I("rowVersion"))).Json(403);
        parameters["zoom"] = "week"; parameters["layers"] = "none";
        (await f.As(TestData.Marc).Patch($"/api/v1/views/{id}", new { @params = parameters }, saved.I("rowVersion"))).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/views/{id}", new { name = "Stale write" }, saved.I("rowVersion"))).Json(409);
        Assert.Equal("none", (await View(id, "timeline", p.Id))["params"]!.S("layers"));
        var alex = data.User(TestData.Alex); await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(member => member.ProjectId == p.Id && member.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/views?listType=timeline&projectId={p.Id}")).Json(404);
    }

    [Fact]
    public async Task Invalid_layout_zoom_calendar_dates_and_layers_are_refused_without_creating_a_view()
    {
        var p = await data.Project(); await Flag("work_screens", true);
        foreach (var (key, value) in new[] { ("zoom", "year"), ("group", "assignee"), ("cols", "name,estimate"), ("layers", "madeUpLayer"), ("from", "2026-02-30"), ("to", "not-a-date"), ("hideCompleted", "maybe") })
        {
            var parameters = Params(); parameters[key] = value;
            var response = await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Invalid Timeline definition", listType = "timeline", projectId = p.Id, @params = parameters });
            Assert.InRange((int)response.StatusCode, 400, 499);
        }
        var reversed = Params(); reversed["from"] = "2026-10-01"; reversed["to"] = "2026-09-01";
        Assert.False((await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Reversed range", listType = "timeline", projectId = p.Id, @params = reversed })).IsSuccessStatusCode);
        Assert.False((await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Project required", listType = "timeline", @params = Params() })).IsSuccessStatusCode);
        Assert.False((await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Project forbidden", listType = "workspace-timeline", projectId = p.Id, @params = Params("project") })).IsSuccessStatusCode);
        Assert.Empty((await Get($"/api/v1/views?listType=timeline&projectId={p.Id}"))["views"]!.AsArray());
    }

    [Fact]
    public async Task Independent_flags_drop_and_name_stale_layer_and_chain_parameters_without_rewriting_definition_or_Timeline()
    {
        var p = await data.Project(); await Flag("work_screens", true); await Flag("schedule_baseline", true); await Flag("timeline_handoffs_chain", true);
        var milestone = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new MilestoneEndpoints.CreateBody("Timeline flag guard milestone",
            MilestoneType.DesignSubmission, new(2026, 10, 20), null, null, null, true))).Json(201);
        var parameters = Params(); parameters["layers"] = "baselineDates,dependencies,handoffs,milestones,originalDates"; parameters["chainTo"] = milestone.S("id");
        var onBefore = (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString();
        var made = await Save(TestData.Pm, "timeline", p.Id, parameters); var id = made.G("id");
        var active = await View(id, "timeline", p.Id); Assert.Equal(milestone.S("id"), active["params"]!.S("chainTo"));
        Assert.Contains("baselineDates", active["params"]!.S("layers")); Assert.Contains("handoffs", active["params"]!.S("layers"));
        Assert.Equal(onBefore, (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString());
        var stored = f.Db(db => db.SavedViews.Single(view => view.Id == id).Filters);
        await Flag("schedule_baseline", false); await Flag("timeline_handoffs_chain", false);
        // Independent flags legitimately change optional Timeline fields. Compare within each flag configuration, not on-versus-off.
        var offBefore = (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString(); var unavailable = await View(id, "timeline", p.Id);
        Assert.Null(unavailable["params"]!["layers"]); Assert.Null(unavailable["params"]!["chainTo"]);
        var dropped = unavailable["dropped"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Contains("layers", dropped); Assert.Contains("chainTo", dropped);
        Assert.Equal(offBefore, (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString());
        Assert.Equal(stored, f.Db(db => db.SavedViews.Single(view => view.Id == id).Filters));
        await Flag("schedule_baseline", true); await Flag("timeline_handoffs_chain", true);
        var restored = await View(id, "timeline", p.Id); Assert.Equal(milestone.S("id"), restored["params"]!.S("chainTo")); Assert.Contains("handoffs", restored["params"]!.S("layers"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disabled_new_view_routes_keep_old_Timeline_reads_and_preserve_stored_definitions(bool small)
    {
        var p = await data.Project(); await Flag("small_ux", small);
        var before = (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString(); await Flag("work_screens", true);
        var created = await Save(TestData.Pm, "timeline", p.Id, Params()); await Flag("work_screens", false);
        await (await f.As(TestData.Pm).GetAsync($"/api/v1/views?listType=timeline&projectId={p.Id}")).Json(404);
        await (await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Off", listType = "timeline", projectId = p.Id, @params = Params() })).Json(404);
        await (await f.As(TestData.Pm).Patch($"/api/v1/views/{created.G("id")}", new { name = "Off edit" }, created.I("rowVersion"))).Json(404);
        await (await f.As(TestData.Pm).DeleteAsync($"/api/v1/views/{created.G("id")}")).Json(404);
        Assert.Equal(before, (await Get($"/api/v1/projects/{p.Id}/timeline")).ToJsonString());
        await Flag("work_screens", true); Assert.NotNull(await View(created.G("id"), "timeline", p.Id));
    }
}
