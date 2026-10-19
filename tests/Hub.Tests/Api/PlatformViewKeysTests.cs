using System.Text.Json.Nodes;

namespace Hub.Tests.Api;

/// Packet 040 S06: saved-view keys round-trip the mode and filter values used by the eight typed pages.
[Collection("api")]
public sealed class PlatformViewKeysTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<JsonNode> SaveAndRead(string as_, string listType, Dictionary<string, string> parameters, Guid? projectId = null)
    {
        var body = new { name = $"Platform {listType} {Guid.NewGuid():N}", listType, projectId, @params = parameters };
        var created = await f.As(as_).Post("/api/v1/views", body).Result.Json(201);
        var query = projectId is null ? $"listType={listType}" : $"listType={listType}&projectId={projectId}";
        var views = (await f.As(as_).GetAsync($"/api/v1/views?{query}").Result.Json())["views"]!.AsArray();
        return views.Single(view => view!["id"]!.GetValue<string>() == created["id"]!.GetValue<string>())!;
    }

    [Fact]
    public async Task Task_views_keep_on_hold_false_across_all_four_task_lists()
    {
        var project = await d.Project();
        foreach (var listType in new[] { "tasks", "board", "workspace-tasks", "workspace-board" })
        {
            var parameters = new Dictionary<string, string> { ["onHold"] = "false", ["status"] = "In Progress" };
            var view = await SaveAndRead(TestData.Alex, listType, parameters, listType is "tasks" or "board" ? project.Id : null);
            Assert.Equal("false", view["params"]!["onHold"]!.GetValue<string>());
            Assert.Equal("In Progress", view["params"]!["status"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Workload_and_planner_keep_office_grouping()
    {
        var workload = await SaveAndRead(TestData.Sam, "workload", new Dictionary<string, string> { ["groupBy"] = "office" });
        Assert.Equal("office", workload["params"]!["groupBy"]!.GetValue<string>());

        var planner = await SaveAndRead(TestData.Sam, "planner", new Dictionary<string, string> { ["groupBy"] = "office" });
        Assert.Equal("office", planner["params"]!["groupBy"]!.GetValue<string>());
    }

    [Fact]
    public async Task Allocation_tab_and_planner_layout_modes_round_trip()
    {
        var project = await d.Project();
        var roles = await SaveAndRead(TestData.Alex, "allocations", new Dictionary<string, string> { ["tab"] = "roles" }, project.Id);
        Assert.Equal("roles", roles["params"]!["tab"]!.GetValue<string>());

        var list = await SaveAndRead(TestData.Alex, "allocations", new Dictionary<string, string> { ["tab"] = "list" }, project.Id);
        Assert.Equal("list", list["params"]!["tab"]!.GetValue<string>());

        foreach (var layout in new[] { "grid", "list", "projects" })
        {
            var planner = await SaveAndRead(TestData.Sam, "planner", new Dictionary<string, string> { ["layout"] = layout });
            Assert.Equal(layout, planner["params"]!["layout"]!.GetValue<string>());
        }
    }
}
