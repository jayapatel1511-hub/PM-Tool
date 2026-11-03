using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Attention remains the evaluator's ranked list; screen context is a read-only
/// enrichment and must fail closed when its source record is no longer usable.
public sealed class AttentionContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public AttentionContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = on })).EnsureSuccessStatusCode();

    async Task<(Project Project, Guid Task, Guid Reviewer)> ReviewAlert()
    {
        var project = await d.Project();
        var reviewer = d.User(TestData.Diane);
        var task = await d.NewTask(project.Id, TestData.Pm, new { name = "Overdue review item", assigneeId = d.User(TestData.Alex), reviewerId = reviewer, requiresReview = true, dueDate = "2026-09-10" });
        await d.Move(TestData.Alex, task, TaskStatuses.InProgress);
        await d.Move(TestData.Alex, task, TaskStatuses.ReadyForReview);
        await d.Move(TestData.Diane, task, TaskStatuses.InReview);
        await f.Evaluate(project.Id);
        // Deliberately stale alert metadata: current facts must come from the task.
        await f.DbAsync(async db => {
            var taskId = task.G("id");
            foreach (var alert in await db.Attention.Where(x => x.ProjectId == project.Id && x.ItemId == taskId).ToListAsync())
            { alert.OwnerUserId = d.User(TestData.Pm); alert.DueDate = new DateOnly(2026, 9, 1); }
            return await db.SaveChangesAsync();
        });
        return (project, task.G("id"), reviewer);
    }

    [Fact]
    public async Task Flag_off_keeps_attention_bytes_and_flag_on_resolves_current_owner_and_reviewer()
    {
        var s = await ReviewAlert();
        await Flag(false);
        var off = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/attention");
        var rowOff = off["items"]!.AsArray().First(x => x!["itemId"]!.GetValue<Guid>() == s.Task);
        Assert.False(rowOff.AsObject().ContainsKey("context"));
        await Flag(true);
        var on = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/attention");
        var row = on["items"]!.AsArray().First(x => x!["itemId"]!.GetValue<Guid>() == s.Task);
        Assert.NotNull(row["context"]);
        Assert.True(row["context"]!["available"]!.GetValue<bool>());
        Assert.Equal(d.User(TestData.Alex), row["context"]!["owner"]!["id"]!.GetValue<Guid>());
        Assert.Equal(s.Reviewer, row["context"]!["nextActors"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(RecordContextAction.Review, row["context"]!["nextAction"]!.GetValue<string>());
        var legacy = on.DeepClone(); foreach (var alert in legacy["items"]!.AsArray()) alert!.AsObject().Remove("context");
        Assert.Equal(off.ToJsonString(), legacy.ToJsonString());
        Assert.Equal("2026-09-10", row["context"]!.S("neededBy"));
        Assert.Equal(rowOff["itemId"]!.ToJsonString(), row["itemId"]!.ToJsonString());
        Assert.Equal(rowOff["severity"]!.ToJsonString(), row["severity"]!.ToJsonString());
    }

    [Fact]
    public async Task Deleted_foreign_and_unsupported_attention_sources_are_unavailable_without_breaking_the_list()
    {
        var s = await ReviewAlert();
        var foreign = await d.Project();
        var foreignTask = (await d.NewTask(foreign.Id, TestData.Pm, new { name = "Private source task", assigneeId = d.User(TestData.Alex) })).G("id");
        await f.DbAsync(async db =>
        {
            (await db.Tasks.SingleAsync(x => x.Id == s.Task)).DeletedAt = f.Clock.Now;
            db.Attention.AddRange(
                new AttentionItem { ProjectId = s.Project.Id, RuleId = "A-16", ItemType = ItemType.Task, ItemId = foreignTask, ItemKey = "foreign", ItemName = "Imported source reference", Severity = Severity.Warning, Message = "Foreign source", FirstDetectedAt = f.Clock.Now, LastEvaluatedAt = f.Clock.Now },
                new AttentionItem { ProjectId = s.Project.Id, RuleId = "A-16", ItemType = "Unsupported", ItemId = Guid.NewGuid(), ItemKey = "unsupported", ItemName = "Unsupported source", Severity = Severity.Warning, Message = "Unsupported source", FirstDetectedAt = f.Clock.Now, LastEvaluatedAt = f.Clock.Now });
            var state = await db.ProjectStates.SingleAsync(x => x.ProjectId == s.Project.Id); state.EvaluatedAt = f.Clock.Now;
            await db.SaveChangesAsync(); return 0;
        });
        await Flag(true);
        var list = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/attention");
        Assert.NotEmpty(list["items"]!.AsArray());
        foreach (var id in new[] { s.Task, foreignTask })
        {
            var row = list["items"]!.AsArray().First(x => x!["itemId"]!.GetValue<Guid>() == id);
            Assert.False(row["context"]!["available"]!.GetValue<bool>());
            Assert.Equal(RecordContextAction.NotAssessed, row["context"]!["nextAction"]!.GetValue<string>());
            Assert.Null(row["context"]!["neededBy"]);
            Assert.DoesNotContain("Private source task", row["context"]!.ToJsonString());
        }
        var unsupported = list["items"]!.AsArray().Single(x => x!["itemName"]!.GetValue<string>() == "Unsupported source");
        Assert.False(unsupported["context"]!["available"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.NotAssessed, unsupported["context"]!["nextAction"]!.GetValue<string>());
    }

    [Fact]
    public async Task Restricted_project_attention_is_not_visible_to_nonmembers()
    {
        var s = await ReviewAlert(); await Flag(true);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, $"/api/v1/projects/{s.Project.Id}/attention", 404);
    }
}
