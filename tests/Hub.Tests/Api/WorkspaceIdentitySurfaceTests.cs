using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-VSN-07/08: project identity follows permitted records, independently of the Home amendment.
public sealed class WorkspaceIdentitySurfaceTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public WorkspaceIdentitySurfaceTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Read(string path, string who = TestData.Pm) => await (await f.As(who).GetAsync("/api/v1/" + path)).Json();
    static string Home(Guid project) => $"home?projects={project}&from=2026-09-14&to=2026-09-20&deadlinePage=1&deadlinePageSize=100";
    static void WithoutIdentity(JsonNode node)
    {
        if (node is JsonObject obj) { obj.Remove("identityColour"); foreach (var child in obj.Select(p => p.Value).OfType<JsonNode>()) WithoutIdentity(child); }
        else if (node is JsonArray rows) foreach (var child in rows.OfType<JsonNode>()) WithoutIdentity(child);
    }
    async Task<Project> Fixture()
    {
        var p = await d.Project(); var date = new DateOnly(2026, 9, 16);
        await d.NewTask(p.Id, extra: new { dueDate = date });
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new DeliverableEndpoints.CreateBody(
            "Synthetic identity drawing", d.ProjectDiscipline(p.Id, "Civil"), await d.DeliverableType(), null, d.User(TestData.Alex), null,
            null, null, date, null, null, false))).Json(201);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new MilestoneEndpoints.CreateBody(
            "Synthetic identity milestone", MilestoneType.DesignSubmission, date, null, null, null, true))).Json(201);
        await (await f.As(TestData.Pm).Post($"/api/v1/items/Project/{p.Id}/links", new { title = "Synthetic identity pointer", url = "https://example.test/identity.pdf" })).Json(201);
        return p;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deadline_identity_uses_the_saved_project_in_both_Home_modes_and_exactly_restores_off_fields(bool overview)
    {
        var p = await Fixture(); await Flag("home_overview", overview);
        // Isolate the separately tested one-time colour backfill from read parity.
        await Flag("visual_system", true); await Flag("visual_system", false);
        var colour = f.Db(db => db.Projects.Single(row => row.Id == p.Id).IdentityColour);
        var baseline = await Read(Home(p.Id));
        Assert.All(baseline["upcomingDeadlines"]!["items"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("identityColour")));
        await Flag("visual_system", true); var enabled = await Read(Home(p.Id));
        var deadlines = enabled["upcomingDeadlines"]!["items"]!.AsArray(); Assert.Equal(3, deadlines.Count);
        Assert.Equal(new[] { "Deliverable", "Milestone", "Task" }, deadlines.Select(row => row!.S("type")).Order());
        Assert.All(deadlines, row => Assert.Equal(colour, row!.S("identityColour")));
        WithoutIdentity(enabled); Assert.True(JsonNode.DeepEquals(baseline, enabled));
        await Flag("visual_system", false); Assert.True(JsonNode.DeepEquals(baseline, await Read(Home(p.Id))));
    }

    [Fact]
    public async Task Team_and_Files_keep_every_existing_field_and_missing_identity_does_not_backfill_on_read()
    {
        var p = await Fixture(); await Flag("visual_system", true); await Flag("visual_system", false);
        var colour = f.Db(db => db.Projects.Single(row => row.Id == p.Id).IdentityColour);
        var filesOff = await Read($"files?projects={p.Id}"); var teamOff = await Read($"team?projects={p.Id}");
        await Flag("visual_system", true);
        var filesOn = await Read($"files?projects={p.Id}"); var teamOn = await Read($"team?projects={p.Id}");
        Assert.Equal(colour, Assert.Single(filesOn["projects"]!.AsArray())!.S("identityColour"));
        var projectRows = teamOn["people"]!.AsArray().SelectMany(person => person!["projects"]!.AsArray()).ToArray(); Assert.NotEmpty(projectRows);
        Assert.All(projectRows, row => Assert.Equal(colour, row!.S("identityColour")));
        WithoutIdentity(filesOn); WithoutIdentity(teamOn);
        Assert.True(JsonNode.DeepEquals(filesOff, filesOn)); Assert.True(JsonNode.DeepEquals(teamOff, teamOn));
        // A valid nullable legacy value remains unknown; GET never assigns a colour.
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).IdentityColour = null; return await db.SaveChangesAsync(); });
        var version = d.Version(p.Id); var activity = f.Db(db => db.ActivityLog.Count());
        var emptyFiles = await Read($"files?projects={p.Id}"); var emptyTeam = await Read($"team?projects={p.Id}"); var home = await Read(Home(p.Id));
        Assert.True(JsonNode.DeepEquals(filesOff, emptyFiles)); Assert.True(JsonNode.DeepEquals(teamOff, emptyTeam));
        Assert.All(home["upcomingDeadlines"]!["items"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("identityColour")));
        Assert.Null(f.Db(db => db.Projects.Single(row => row.Id == p.Id).IdentityColour)); Assert.Equal(version, d.Version(p.Id)); Assert.Equal(activity, f.Db(db => db.ActivityLog.Count()));
    }

    [Fact]
    public async Task Current_PM_colour_change_reaches_all_three_reads_and_restricted_projects_never_contribute_identity()
    {
        var p = await Fixture(); var hidden = await d.Project(pm: TestData.Marc);
        await d.NewTask(hidden.Id, extra: new { dueDate = new DateOnly(2026, 9, 16) });
        await (await f.As(TestData.Marc).Post($"/api/v1/items/Project/{hidden.Id}/links", new { title = "Hidden synthetic pointer", url = "https://example.test/hidden.pdf" })).Json(201);
        await Flag("visual_system", true);
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { identityColour = IdentityColour.Cyan }, d.Version(p.Id))).EnsureSuccessStatusCode();
        Assert.All((await Read(Home(p.Id)))["upcomingDeadlines"]!["items"]!.AsArray(), row => Assert.Equal(IdentityColour.Cyan, row!.S("identityColour")));
        Assert.Equal(IdentityColour.Cyan, Assert.Single((await Read($"files?projects={p.Id}"))["projects"]!.AsArray())!.S("identityColour"));
        Assert.All((await Read($"team?projects={p.Id}"))["people"]!.AsArray().SelectMany(person => person!["projects"]!.AsArray()), row => Assert.Equal(IdentityColour.Cyan, row!.S("identityColour")));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == hidden.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        foreach (var path in new[] { "home?projects=all&from=2026-09-14&to=2026-09-20", "files?projects=all", "team?projects=all" })
        {
            var visible = await Read(path, TestData.Rita);
            Assert.DoesNotContain(hidden.Id.ToString(), visible.ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, visible.ToJsonString()); Assert.Contains(p.Id.ToString(), visible.ToJsonString());
        }
    }
}
