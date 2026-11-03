using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-VSN-06: both timeline routes consume the task list's canonical lane, without a client status mapping.
public sealed class VisualTimelineLaneTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public VisualTimelineLaneTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.visual_system", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Read(string path, string who = TestData.Pm) => await (await f.As(who).GetAsync("/api/v1" + path)).Json();

    [Fact]
    public async Task Project_and_global_timelines_match_canonical_task_list_lanes_and_restore_exact_off_shape_without_writes()
    {
        var p = await d.Project();
        var expected = new Dictionary<Guid, string>();
        var statuses = new Dictionary<Guid, string>();
        // These persisted valid states exercise a read projection, not transition guards.
        foreach (var (status, lane) in new[] { (TaskStatuses.NotStarted, "To Do"), (TaskStatuses.InProgress, "In Progress"),
            (TaskStatuses.ReadyForReview, "Review"), (TaskStatuses.InReview, "Review"), (TaskStatuses.RevisionRequired, "Review"),
            (TaskStatuses.Complete, "Done"), (TaskStatuses.OnHold, "On Hold") })
        {
            var task = await d.NewTask(p.Id, extra: new { name = "Synthetic timeline lane " + status,
                assigneeId = d.User(TestData.Alex), requiresReview = true, reviewerId = d.User(TestData.Omar) });
            expected.Add(task.G("id"), lane); statuses.Add(task.G("id"), status);
        }
        await f.DbAsync(async db =>
        {
            foreach (var row in await db.Tasks.Where(t => t.ProjectId == p.Id).ToListAsync())
            { row.Status = statuses[row.Id]; row.ProgressPct = row.Status == TaskStatuses.Complete ? 100 : 0; }
            return await db.SaveChangesAsync();
        });
        // Materialize derived state before the canonical task-list read; reads below must not be its first evaluation.
        await f.Evaluate(p.Id);
        // The flag's separately tested one-time colour backfill is a command, outside the read-only baseline.
        await Flag(true); await Flag(false);
        var versions = f.Db(db => db.Tasks.Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => t.RowVersion).ToArray());
        var activity = f.Db(db => db.ActivityLog.Count(a => a.ProjectId == p.Id));
        var projectPath = $"/projects/{p.Id}/timeline"; var globalPath = $"/timeline?projects={p.Id}";
        var beforeProject = await Read(projectPath); var beforeGlobal = await Read(globalPath);
        Assert.All(beforeProject["tasks"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("lane")));
        await Flag(true);
        var enabledProject = await Read(projectPath); var enabledGlobal = await Read(globalPath);
        var filter = Uri.EscapeDataString(string.Join(',', statuses.Values.Distinct()));
        var canonical = (await Read($"/projects/{p.Id}/tasks?status={filter}&pageSize=100"))["items"]!.AsArray().ToDictionary(t => t!.G("id"), t => t!.S("lane"));
        foreach (var timeline in new[] { enabledProject, enabledGlobal["projects"]![0]!["data"]! })
        {
            Assert.Equal(expected.Count, timeline["tasks"]!.AsArray().Count);
            foreach (var row in timeline["tasks"]!.AsArray())
            { Assert.Equal(expected[row!.G("id")], row.S("lane")); Assert.Equal(canonical[row.G("id")], row.S("lane")); row.AsObject().Remove("lane"); }
        }
        enabledGlobal["projects"]![0]!["project"]!.AsObject().Remove("identityColour");
        Assert.True(JsonNode.DeepEquals(beforeProject, enabledProject)); Assert.True(JsonNode.DeepEquals(beforeGlobal, enabledGlobal));
        await Flag(false);
        Assert.True(JsonNode.DeepEquals(beforeProject, await Read(projectPath))); Assert.True(JsonNode.DeepEquals(beforeGlobal, await Read(globalPath)));
        Assert.Equal(versions, f.Db(db => db.Tasks.Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => t.RowVersion).ToArray()));
        Assert.Equal(activity, f.Db(db => db.ActivityLog.Count(a => a.ProjectId == p.Id)));
    }

    [Fact]
    public async Task Enabled_lane_read_preserves_restricted_project_scope_and_exact_project_refusal()
    {
        var visible = await d.Project(); var hidden = await d.Project(pm: TestData.Marc, tweak: body => body["members"] = Array.Empty<object>());
        await d.NewTask(visible.Id); await d.NewTask(hidden.Id);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await Flag(true);
        var result = await Read($"/timeline?projects={visible.Id},{hidden.Id}", TestData.Rita);
        Assert.Equal(visible.Id, Assert.Single(result["projects"]!.AsArray())!["project"]!.G("id"));
        Assert.DoesNotContain(hidden.Id.ToString(), result.ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, result.ToJsonString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{hidden.Id}/timeline")).StatusCode);
    }
}
