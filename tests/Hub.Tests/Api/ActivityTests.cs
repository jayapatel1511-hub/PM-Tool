using System.Net;
using System.Text;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Activity History (§13.14, FR-AUD-02, FR-AUD-03, FR-VIEW-03): filters, names resolved at read time, item history, export.
[Collection("api")]
public sealed class ActivityTests(HubFactory f)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task Project_activity_filters_and_resolves_names()
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-01" });
        (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.S("id")}", new { assigneeId = d.User(TestData.Alex) }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
        var pm = f.As(TestData.Pm);

        var all = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?pageSize=200").Result.Json();
        var items = all["items"]!.AsArray();
        Assert.True(all.I("totalCount") >= 5); // project, disciplines, members, activation, task, assignment
        var assigned = items.First(x => x!.S("itemId") == t.S("id") && x["categories"]!.AsArray().Any(c => c!.GetValue<string>() == "assignment"))!;
        Assert.Equal("Marc Dubois", assigned.S("actorName"));
        Assert.Contains("Alex Chen", assigned.S("summary")); // the id is shown as a name
        Assert.True(DateTimeOffset.Parse(items[0]!.S("occurredAt")) >= DateTimeOffset.Parse(items[^1]!.S("occurredAt"))); // newest first

        var byMarc = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?actorId={d.User(TestData.Marc)}").Result.Json();
        Assert.All(byMarc["items"]!.AsArray(), x => Assert.Equal("Marc Dubois", x!.S("actorName")));
        var tasksOnly = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?itemType=Task&category=assignment,date").Result.Json();
        Assert.All(tasksOnly["items"]!.AsArray(), x => Assert.Equal("Task", x!.S("itemType")));
        var important = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?importantOnly=true").Result.Json();
        Assert.All(important["items"]!.AsArray(), x => Assert.Contains(x!["categories"]!.AsArray(), c => ActivityImportant.Contains(c!.GetValue<string>())));
        var future = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?from=2030-01-01").Result.Json();
        Assert.Equal(0, future.I("totalCount"));
        var paged = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?pageSize=2&page=2").Result.Json();
        Assert.Equal(2, paged["items"]!.AsArray().Count);
        var byDiscipline = await pm.GetAsync($"/api/v1/projects/{p.Id}/activity?disciplineId={d.ProjectDiscipline(p.Id, "Civil")}&to=2030-01-01").Result.Json();
        Assert.Contains(byDiscipline["items"]!.AsArray(), x => x!.S("itemId") == t.S("id"));
    }

    static readonly string[] ActivityImportant = ["status", "assignment", "date", "deletion"];

    [Fact]
    public async Task Item_history_includes_dependencies_that_name_the_item()
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm);
        var b = await d.NewTask(p.Id, TestData.Pm, new { dependsOn = new[] { a.G("id") } });
        var history = await f.As(TestData.Alex).GetAsync($"/api/v1/items/Task/{a.S("id")}/activity").Result.Json();
        var rows = history["items"]!.AsArray();
        Assert.Contains(rows, x => x!.S("itemType") == ItemType.Dependency && x.S("itemKey").Contains(b.S("key")));
        Assert.Contains(rows, x => x!.S("itemId") == a.S("id") && x.S("action") == "Created");
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/items/Task/{Guid.NewGuid()}/activity")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/items/Unknown/{a.S("id")}/activity")).StatusCode);
        var project = await f.As(TestData.Pm).GetAsync($"/api/v1/items/Project/{p.Id}/activity").Result.Json();
        Assert.Contains(project["items"]!.AsArray(), x => x!.S("action") == "StatusChanged");
        Assert.DoesNotContain(project["items"]!.AsArray(), x => x!.S("itemType") == ItemType.Dependency); // the project number is in every key
    }

    [Fact]
    public async Task Export_is_a_csv_with_a_bom_and_is_itself_logged() // FR-AUD-03, §20.3
    {
        var p = await d.Project();
        await d.NewTask(p.Id, TestData.Pm, new { name = "Survey, \"phase 1\"" });
        var res = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/activity/export?itemType=Task");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("text/csv", res.Content.Headers.ContentType!.MediaType);
        var file = res.Content.Headers.ContentDisposition!.FileNameStar ?? res.Content.Headers.ContentDisposition.FileName!.Trim('"');
        Assert.StartsWith($"{p.ProjectNumber}-activity-", file); // dated, e.g. P1234-activity-20260914-0900.csv
        Assert.EndsWith(".csv", file);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var text = Encoding.UTF8.GetString(bytes[3..]);
        Assert.StartsWith("Timestamp,Actor,Action,Item,Change,Reason,Source", text);
        Assert.Contains("\"\"phase 1\"\"", text); // quotes escaped
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(x => x.ProjectId == p.Id && x.ItemType == ItemType.Report && x.Action == "Exported" && x.ActorUserId == d.User(TestData.Pm))));
    }

    [Fact]
    public async Task Restricted_project_activity_is_hidden_from_non_members() // §8.7
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var p = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{p.Id}/activity")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/activity")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Lena).GetAsync($"/api/v1/projects/{p.Id}/activity")).StatusCode); // Executive
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }
}
