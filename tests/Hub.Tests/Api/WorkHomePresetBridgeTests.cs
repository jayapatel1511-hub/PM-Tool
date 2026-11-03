using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S08 destination reads keep the same current-actor cohort as Home; scalar presets do not expand into URL ID lists.
public sealed class WorkHomePresetBridgeTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkHomePresetBridgeTests() => data = new(f);
    public void Dispose() => f.Dispose();
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.home_overview", new { value = true })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(string who, string url) => await (await f.As(who).GetAsync(url)).Json();
    static Guid[] Ids(JsonNode body) => body["items"]!.AsArray().Select(x => x!.G("id")).Order().ToArray();

    [Fact]
    public async Task Disabled_preset_query_preserves_the_exact_prior_task_list()
    {
        var p = await data.Project(); await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var url = $"/api/v1/tasks?projects={p.Id}";
        Assert.Equal((await Get(TestData.Marc, url)).ToJsonString(), (await Get(TestData.Marc, url + "&homePreset=NotARole&homeProjectStatus=NotAStatus")).ToJsonString());
        var projectUrl = $"/api/v1/projects/{p.Id}/tasks";
        Assert.Equal((await Get(TestData.Marc, projectUrl)).ToJsonString(), (await Get(TestData.Marc, projectUrl + "?homePreset=NotARole&homeProjectStatus=NotAStatus")).ToJsonString());
    }

    [Fact]
    public async Task Home_project_lifecycle_filter_intersects_visible_scope_preset_and_task_filters_on_both_routes()
    {
        var active = await data.Project(); var held = await data.Project(); var alex = data.User(TestData.Alex);
        var first = (await data.NewTask(active.Id, TestData.Marc, new { assigneeId = alex })).G("id");
        var second = (await data.NewTask(held.Id, TestData.Marc, new { assigneeId = alex })).G("id");
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{held.Id}/transition", new { toStatus = ProjectStatus.OnHold, reason = "Awaiting external survey", rowVersion = data.Version(held.Id) })).Json();
        await Enable();
        var url = $"/api/v1/tasks?projects={active.Id},{held.Id}&homePreset=DisciplineLead&assigneeId={alex}";
        Assert.Equal([first], Ids(await Get(TestData.Marc, url + "&homeProjectStatus=Active")));
        Assert.Equal([second], Ids(await Get(TestData.Marc, url + "&homeProjectStatus=" + Uri.EscapeDataString(ProjectStatus.OnHold))));
        Assert.Equal(new[] { first, second }.Order(), Ids(await Get(TestData.Marc, url + "&homeProjectStatus=" + Uri.EscapeDataString($"{ProjectStatus.Active},{ProjectStatus.OnHold}"))));
        Assert.Empty(Ids(await Get(TestData.Marc, $"/api/v1/tasks?projects={held.Id}&homeProjectStatus=Active")));
        Assert.Empty(Ids(await Get(TestData.Marc, $"/api/v1/projects/{held.Id}/tasks?homeProjectStatus=Active")));
        Assert.Equal([first], Ids(await Get(TestData.Marc, $"/api/v1/projects/{active.Id}/tasks?homeProjectStatus=Active")));
        await (await f.As(TestData.Marc).GetAsync(url + "&homeProjectStatus=NotAStatus")).Json(400);
        await (await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{active.Id}/tasks?homeProjectStatus=NotAStatus")).Json(400);
    }

    [Fact]
    public async Task Discipline_preset_intersects_current_leadship_projects_and_all_explicit_filters_on_both_routes()
    {
        var a = await data.Project(); var b = await data.Project();
        var alex = data.User(TestData.Alex); var jill = data.User(TestData.Jill);
        var first = (await data.NewTask(a.Id, TestData.Marc, new { assigneeId = alex })).G("id");
        var second = (await data.NewTask(b.Id, TestData.Marc, new { assigneeId = jill })).G("id");
        await data.NewTask(a.Id, TestData.Omar, new { assigneeId = alex }, "Electrical");
        await Enable();
        var url = $"/api/v1/tasks?projects={a.Id},{b.Id}&homePreset=DisciplineLead&assigneeId={alex},{jill}";
        Assert.Equal(new[] { first, second }.Order(), Ids(await Get(TestData.Marc, url)));
        Assert.Equal([second], Ids(await Get(TestData.Marc, $"/api/v1/tasks?projects={a.Id},{b.Id}&homePreset=DisciplineLead&assigneeId={jill}")));
        Assert.Equal([first], Ids(await Get(TestData.Marc, $"/api/v1/projects/{a.Id}/tasks?homePreset=DisciplineLead")));
        var discipline = data.ProjectDiscipline(b.Id, "Civil"); var omar = data.User(TestData.Omar);
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(d => d.Id == discipline)).LeadUserId = omar; return await db.SaveChangesAsync(); });
        Assert.Equal([first], Ids(await Get(TestData.Marc, url)));
    }

    [Fact]
    public async Task Supervisor_and_project_manager_presets_keep_their_exact_current_roles()
    {
        var owned = await data.Project(); var other = await data.Project(pm: TestData.Marc);
        var alex = data.User(TestData.Alex); var omar = data.User(TestData.Omar);
        var first = (await data.NewTask(owned.Id, TestData.Marc, new { assigneeId = alex })).G("id");
        var otherTask = (await data.NewTask(other.Id, TestData.Marc, new { assigneeId = omar })).G("id");
        await Enable();
        var scope = $"projects={owned.Id},{other.Id}";
        Assert.Equal([first], Ids(await Get(TestData.Sam, $"/api/v1/tasks?{scope}&homePreset=Supervisor")));
        Assert.Equal([first], Ids(await Get(TestData.Pm, $"/api/v1/tasks?{scope}&homePreset=ProjectManager")));
        Assert.Equal([otherTask], Ids(await Get(TestData.Pm, $"/api/v1/tasks?projects={other.Id}")));
    }
}
