using System.Text.Json.Nodes;

namespace Hub.Tests.Api;

/// Person links use the report's exact authorised User references, preserving normal exports and dark payloads.
public sealed class ReportPersonReferenceTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public ReportPersonReferenceTests() { d = new(f); f.Clock.SetDate(2026, 11, 1); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Task_reference_is_the_actual_assignee_not_the_task_and_is_absent_when_dark()
    {
        var project = await d.Project();
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-28" });
        await f.Evaluate(project.Id);
        var route = $"/api/v1/reports/overdue-tasks?projectId={project.Id}";
        async Task<JsonNode> Read() => (await f.As(TestData.Pm).GetAsync(route).Result.Json())["rows"]!.AsArray().Single(r => r!.G("id") == task.G("id"))!;
        await Flag(false);
        var before = await Read();
        Assert.False(before.AsObject().ContainsKey("userReferences"));
        await Flag(true);
        var on = await Read();
        var person = on["userReferences"]!["assigneeName"]!;
        Assert.Equal("User", person.S("kind"));
        Assert.Equal(d.User(TestData.Alex), person.G("id"));
        Assert.NotEqual(task.G("id"), person.G("id"));
        Assert.Equal(on.S("assigneeName"), person.S("displayName"));
        on.AsObject().Remove("userReferences");
        Assert.True(JsonNode.DeepEquals(before, on));
    }

    [Fact]
    public async Task An_external_owner_with_the_same_display_name_is_not_a_user_reference()
    {
        var project = await d.Project();
        var pm = f.As(TestData.Pm);
        var party = await pm.Post($"/api/v1/projects/{project.Id}/external-parties", new { name = "Alex Chen", isClient = true }).Result.Json(201);
        var decision = await pm.Post($"/api/v1/projects/{project.Id}/decisions", new
        {
            subject = "Synthetic client decision", description = "Typed owner test", ownerExternalPartyId = party.G("id"),
            requiredByDate = "2026-11-03", impactLevel = "Medium", impactDescription = "Synthetic"
        }).Result.Json(201);
        await Flag(true);
        var result = await pm.GetAsync($"/api/v1/reports/open-decisions?projectId={project.Id}").Result.Json();
        var row = result["rows"]!.AsArray().Single(r => r!.G("id") == decision.G("id"))!;
        Assert.Equal("Alex Chen", row.S("ownerName"));
        Assert.Null(row["userReferences"]?["ownerName"]);
        Assert.Equal(d.User(TestData.Pm), row["userReferences"]!["requestedByName"]!.G("id"));
    }

    [Fact]
    public async Task Two_entries_for_one_task_keep_distinct_person_openers_and_reference_metadata_is_not_exported()
    {
        var project = await d.Project();
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        var actor = f.As(TestData.Alex);
        var entries = new List<Guid>();
        foreach (var hours in new[] { 1m, 2m })
            entries.Add((await actor.Post("/api/v1/time", new { taskId = task.G("id"), workDate = "2026-11-01", hours }).Result.Json(201)).G("id"));
        var query = $"projectId={project.Id}&from=2026-11-01&to=2026-11-01&scope=mine";
        await Flag(true);
        var result = await actor.GetAsync($"/api/v1/reports/task-hours?{query}").Result.Json();
        var rows = result["rows"]!.AsArray().Where(r => r!["entryId"] is not null).Select(r => r!).ToArray();
        Assert.Equal(entries.Order(), rows.Select(r => r.G("entryId")).Order());
        Assert.All(rows, row => Assert.Equal(d.User(TestData.Alex), row["userReferences"]!["person"]!.G("id")));
        Assert.Equal(2, rows.Select(row => row["userReferences"]!["person"]!.S("sourceKey")).Distinct().Count());
        using var export = await actor.GetAsync($"/api/v1/reports/task-hours/export?format=csv&{query}");
        export.EnsureSuccessStatusCode();
        var csv = await export.Content.ReadAsStringAsync();
        Assert.Contains("Alex Chen", csv);
        Assert.DoesNotContain("userReferences", csv);
        Assert.DoesNotContain("sourceKey", csv);
        Assert.All(entries, entry => Assert.DoesNotContain(entry.ToString(), csv));
    }
}
