using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkspaceFilePersonTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public WorkspaceFilePersonTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value })).Json();
    async Task<JsonNode> Files(string projects, string who = TestData.Pm, string extra = "") =>
        await (await f.As(who).GetAsync($"/api/v1/files?projects={projects}{extra}")).Json();
    async Task<Guid> Link(string kind, Guid item, string who, string title, string url) =>
        (await (await f.As(who).Post($"/api/v1/items/{kind}/{item}/links", new { title, url })).Json(201)).G("id");
    static IEnumerable<JsonNode> Links(JsonNode response) => response["projects"]!.AsArray()
        .SelectMany(project => project!["items"]!.AsArray()).SelectMany(item => item!["links"]!.AsArray()).Select(link => link!);
    static string[] Keys(JsonNode row) => row.AsObject().Select(property => property.Key).ToArray();
    static JsonNode WithoutPeople(JsonNode response)
    {
        // S44 has dedicated metadata checks; this comparison retains the exact legacy projection and Person assertions.
        var copy = response.DeepClone(); copy.AsObject().Remove("screen");
        foreach (var link in Links(copy)) link.AsObject().Remove("addedByUser"); return copy;
    }
    string ReadState() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(),
        Links = db.DocumentLinks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.ItemType,
            row.ItemId, row.Title, row.Url, row.LinkType, row.AddedBy, row.AddedAt, row.DeletedAt, row.DeletedBy, row.RowVersion }).ToArray(),
        Projects = db.Projects.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Tasks = db.Tasks.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Users = db.Users.OrderBy(row => row.Id).Select(row => new { row.Id, row.DisplayName, row.IsActive, row.RowVersion }).ToArray()
    }));

    [Fact]
    public async Task Normal_links_keep_their_legacy_payload_and_use_actual_authors_even_with_the_same_name_and_inactive_history()
    {
        var project = await d.Project(); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill); var pm = d.User(TestData.Pm);
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = alex });
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = jill }); // normal membership for Jill's link command
        var projectLink = await Link("Project", project.Id, TestData.Pm, "01 Project site", "https://example.test/project");
        var alexLink = await Link("Task", task.G("id"), TestData.Alex, "02 Levels", "https://example.test/levels");
        var jillLink = await Link("Task", task.G("id"), TestData.Jill, "03 Bridge folder", @"\\files.test\drawings");
        // Historical author identity is preserved when a real User is subsequently renamed or deactivated.
        await f.DbAsync(async db => {
            var users = await db.Users.Where(user => user.Id == alex || user.Id == jill).ToArrayAsync();
            foreach (var user in users) user.DisplayName = "Same synthetic name";
            users.Single(user => user.Id == jill).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var scope = project.Id.ToString(); await Flag(false); var before = ReadState(); var dark = await Files(scope);
        Assert.Equal(3, dark.I("total")); Assert.False(dark["truncated"]!.GetValue<bool>());
        foreach (var link in Links(dark))
            Assert.Equal(["id", "title", "url", "linkType", "isNetworkPath", "addedBy", "addedAt"], Keys(link));
        Assert.Equal(before, ReadState());
        await Flag(true); before = ReadState(); var enabled = await Files(scope);
        var expected = new Dictionary<Guid, Guid> { [projectLink] = pm, [alexLink] = alex, [jillLink] = jill };
        foreach (var link in Links(enabled))
        {
            Assert.Equal(["id", "title", "url", "linkType", "isNetworkPath", "addedBy", "addedAt", "addedByUser"], Keys(link));
            var person = link["addedByUser"]!; Assert.Equal(["id", "displayName", "isActive"], Keys(person));
            Assert.Equal(expected[link.G("id")], person.G("id")); Assert.NotEqual(link.G("id"), person.G("id"));
            Assert.NotEqual(task.G("id"), person.G("id")); Assert.NotEqual(project.Id, person.G("id"));
            Assert.Equal(link.S("addedBy"), person.S("displayName"));
        }
        var namesakes = Links(enabled).Where(link => link.G("id") == alexLink || link.G("id") == jillLink).ToArray();
        Assert.Single(namesakes.Select(link => link.S("addedBy")).Distinct());
        Assert.Equal(2, namesakes.Select(link => link["addedByUser"]!.G("id")).Distinct().Count());
        Assert.True(namesakes.Single(link => link.G("id") == alexLink)["addedByUser"]!["isActive"]!.GetValue<bool>());
        Assert.False(namesakes.Single(link => link.G("id") == jillLink)["addedByUser"]!["isActive"]!.GetValue<bool>());
        Assert.True(namesakes.Single(link => link.G("id") == jillLink)["isNetworkPath"]!.GetValue<bool>());
        Assert.Equal(dark.ToJsonString(), WithoutPeople(enabled).ToJsonString()); Assert.Equal(before, ReadState());
        var filtered = await Files(scope, extra: "&q=bridge&type=Network%20Folder");
        Assert.Equal(jillLink, Assert.Single(Links(filtered)).G("id")); Assert.Equal(1, filtered.I("total"));
        var member = await Files(scope, TestData.Alex);
        Assert.False(member["projects"]![0]!["canAdd"]!.GetValue<bool>()); Assert.Equal(3, member.I("total"));
        Assert.Equal(before, ReadState());
        await Flag(false); before = ReadState(); Assert.Equal(dark.ToJsonString(), (await Files(scope)).ToJsonString()); Assert.Equal(before, ReadState());
    }

    [Fact]
    public async Task Explicit_hidden_project_scope_cannot_disclose_links_or_their_author_metadata()
    {
        var visible = await d.Project();
        var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, hidden.RowVersion)).Json();
        var task = await d.NewTask(hidden.Id, TestData.Pm, new { assigneeId = d.User(TestData.Diane) });
        var shown = await Link("Project", visible.Id, TestData.Pm, "Visible site", "https://example.test/visible");
        var secret = await Link("Task", task.G("id"), TestData.Diane, "Hidden site", "https://example.test/hidden");
        var scope = $"{visible.Id},{hidden.Id}";
        await Flag(false); var dark = await Files(scope, TestData.Alex); Assert.Equal(shown, Assert.Single(Links(dark)).G("id"));
        await Flag(true); var before = ReadState(); var enabled = await Files(scope, TestData.Alex);
        Assert.Equal(dark.ToJsonString(), WithoutPeople(enabled).ToJsonString());
        Assert.DoesNotContain(Links(enabled), link => link.G("id") == secret);
        Assert.DoesNotContain(d.User(TestData.Diane).ToString(), enabled.ToJsonString());
        Assert.DoesNotContain(enabled["projects"]!.AsArray(), project => project!.G("projectId") == hidden.Id);
        Assert.Equal(0, (await Files(hidden.Id.ToString(), TestData.Alex)).I("total"));
        var admin = await Files(scope, TestData.Admin); var hiddenLink = Links(admin).Single(link => link.G("id") == secret);
        Assert.Equal(d.User(TestData.Diane), hiddenLink["addedByUser"]!.G("id")); Assert.Equal(2, admin.I("total"));
        Assert.Equal(before, ReadState());
    }

    [Fact]
    public async Task User_projection_preserves_the_2000_link_cap_and_does_not_drop_a_historical_missing_author()
    {
        var project = await d.Project(); var pm = d.User(TestData.Pm);
        var normal = await Link("Project", project.Id, TestData.Pm, "Link 0000", "https://example.test/0");
        var missingUser = Guid.NewGuid(); Assert.False(f.Db(db => db.Users.Any(user => user.Id == missingUser)));
        var fixtures = Enumerable.Range(1, 2000).Select(index => new DocumentLink { ProjectId = project.Id, ItemType = "Project", ItemId = project.Id,
            Title = $"Link {index:D4}", Url = $"https://example.test/{index}", AddedBy = index == 1 ? missingUser : pm,
            AddedAt = f.Clock.Now, LinkType = LinkType.Other }).ToArray();
        // Bounded synthetic history, not 2,000 operational API commands. The current DocumentLink
        // schema permits orphan author IDs; a left join must retain their normal null caption.
        await f.DbAsync(async db => { db.DocumentLinks.AddRange(fixtures); return await db.SaveChangesAsync(); });
        await Flag(false); var scope = project.Id.ToString(); var dark = await Files(scope);
        var old = Links(dark).ToArray(); Assert.Equal(2000, dark.I("total")); Assert.True(dark["truncated"]!.GetValue<bool>());
        Assert.Equal(normal, old[0].G("id")); Assert.Equal(fixtures[1998].Id, old[^1].G("id"));
        await Flag(true); var before = ReadState(); var enabled = await Files(scope); var links = Links(enabled).ToArray();
        Assert.Equal(2000, links.Length); Assert.Equal(2000, enabled["projects"]![0]!.I("count"));
        Assert.True(enabled["truncated"]!.GetValue<bool>()); Assert.DoesNotContain(links, link => link.G("id") == fixtures[^1].Id);
        var orphan = links.Single(link => link.G("id") == fixtures[0].Id);
        Assert.Null(orphan["addedBy"]); Assert.False(orphan.AsObject().ContainsKey("addedByUser"));
        Assert.Equal(pm, links.Single(link => link.G("id") == normal)["addedByUser"]!.G("id"));
        Assert.Equal(dark.ToJsonString(), WithoutPeople(enabled).ToJsonString()); Assert.Equal(before, ReadState());
    }
}
