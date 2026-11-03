using System.Text.Json.Nodes;

namespace Hub.Tests.Api;

public sealed class WorkActivityFlagBoundaryTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public WorkActivityFlagBoundaryTests() { d = new(f); f.Clock.SetDate(2026, 11, 1); }
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Independent_small_ux_keeps_legacy_actor_wire_until_a_new_consuming_flag_is_enabled()
    {
        var project = await d.Project();
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        await Flag("small_ux", true);
        await Flag("work_screens", false);
        await Flag("people_screens", false);
        async Task<JsonNode> Row() => (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/tasks")).Json())["items"]!.AsArray().Single(row => row!.G("id") == task.G("id"))!;
        var legacy = await Row();
        Assert.True(legacy.AsObject().ContainsKey("lastActivityActorName"));
        Assert.Equal("Priya Nair", legacy.S("lastActivityActorName"));
        foreach (var field in new[] { "lastActivityActorId", "lastActivityActorType", "lastActivityEventAt", "seen", "workActivity" })
            Assert.False(legacy.AsObject().ContainsKey(field), field);
        foreach (var enabled in new[] { "people_screens", "work_screens" })
        {
            await Flag(enabled, true);
            var on = await Row();
            Assert.Equal(d.User(TestData.Pm), on.G("lastActivityActorId"));
            Assert.Equal("User", on.S("lastActivityActorType"));
            Assert.NotNull(on["lastActivityEventAt"]);
            var detail = await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{task.G("id")}")).Json();
            Assert.Equal(enabled == "work_screens", detail.AsObject().ContainsKey("workActivity"));
            Assert.False(detail["task"]!.AsObject().ContainsKey("workActivity"));
            await Flag(enabled, false);
            Assert.True(JsonNode.DeepEquals(legacy, await Row()));
        }
    }
}
