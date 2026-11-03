using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-81/84: actual source v3 and released v5, scoped historical usage and old cached command wire.
public sealed class TemplateVersionProvenanceTests : IDisposable
{
    readonly HubFactory f = new(); readonly TemplateScreenData t;
    public TemplateVersionProvenanceTests() { t = new(f); }
    public void Dispose() => f.Dispose();
    async Task<JsonNode> Provenance(Guid project, string who = TestData.Pm, int expect = 200) => await (await f.As(who).GetAsync($"/api/v1/projects/{project}/template-provenance")).Json(expect);

    [Fact]
    public async Task Actual_project_source_v3_and_numbered_v5_are_information_only_and_each_version_lists_its_real_projects()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true);
        var version = await t.New(TemplateScreenData.Risks); JsonNode? source = null; Project? project = null;
        for (var number = 1; number <= 5; number++)
        {
            version = await t.Save(version, body => body["header"]!["name"] = "Synthetic version " + number);
            await t.Publish(version);
            if (number == 3) { source = await t.Read(version.G("id")); project = await t.Project(version.G("id"), "250101"); }
            if (number < 5) version = await t.Draft(version.G("id"));
        }
        Assert.NotNull(source); Assert.NotNull(project); var before = t.State();
        var read = await Provenance(project.Id);
        Assert.Equal("Recorded", read.S("state")); Assert.Equal(source.G("id"), read["source"]!.G("id"));
        Assert.Equal(3, read["source"]!.I("version")); Assert.Equal("Synthetic version 3", read["source"]!.S("name"));
        Assert.Equal(5, read["newer"]!.I("version")); Assert.Equal(version.G("id"), read["newer"]!.G("id"));
        Assert.Equal(5, read["currentPublished"]!.I("version"));
        var projects = await (await f.As(TestData.Admin).GetAsync($"/api/v1/templates/{source.G("id")}/projects")).Json();
        Assert.Equal(1, projects.I("totalCount")); Assert.Equal(project.Id, Assert.Single(projects["items"]!.AsArray())!.G("projectId"));
        Assert.Equal(0, (await (await f.As(TestData.Admin).GetAsync($"/api/v1/templates/{version.G("id")}/projects")).Json()).I("totalCount"));
        Assert.Equal(before, t.State());
        // Retiring v5 keeps its actual numbered existence, without inventing a current Published version.
        await (await f.As(TestData.Admin).Post($"/api/v1/templates/{version.G("id")}/retire", new { })).Json(204);
        var retired = await Provenance(project.Id); Assert.Equal(5, retired["newer"]!.I("version")); Assert.Equal("Retired", retired["newer"]!.S("status")); Assert.Null(retired["currentPublished"]);
        await t.SetFlag("people_screens", false); await Provenance(project.Id, expect: 404);
    }

    [Fact]
    public async Task Existing_list_counts_and_usage_pages_apply_current_visibility_and_include_visible_archived_and_cancelled_projects()
    {
        await t.SetFlag("people_screens", true); var template = await t.New(); await t.Publish(template);
        var shown = await t.Project(template.G("id")); var archived = await t.Project(template.G("id")); var cancelled = await t.Project(template.G("id"));
        var hidden = await t.Data.Project(activate: false, tweak: body => { body["templateId"] = template.G("id"); body["members"] = Array.Empty<object>(); });
        var anotherFamily = await t.New(); await t.Publish(anotherFamily); var unrelated = await t.Project(anotherFamily.G("id")); // same version number, different actual source
        var editorId = t.Data.User(TestData.Alex);
        await f.DbAsync(async db => {
            (await db.Users.SingleAsync(row => row.Id == editorId)).IsTemplateEditor = true;
            (await db.Projects.SingleAsync(row => row.Id == archived.Id)).Status = ProjectStatus.Archived;
            (await db.Projects.SingleAsync(row => row.Id == cancelled.Id)).Status = ProjectStatus.Cancelled;
            (await db.Projects.SingleAsync(row => row.Id == hidden.Id)).Visibility = Visibility.Restricted;
            return await db.SaveChangesAsync();
        });
        var before = t.State();
        var list = await (await f.As(TestData.Alex).GetAsync("/api/v1/templates")).Json();
        Assert.Equal(3, list["templates"]!.AsArray().Single(row => row!.G("id") == template.G("id"))!.I("projects"));
        var first = await (await f.As(TestData.Alex).GetAsync($"/api/v1/templates/{template.G("id")}/projects?page=1&pageSize=2")).Json();
        var second = await (await f.As(TestData.Alex).GetAsync($"/api/v1/templates/{template.G("id")}/projects?page=2&pageSize=2")).Json();
        Assert.Equal(3, first.I("totalCount")); Assert.Equal(3, second.I("totalCount"));
        Assert.Equal(new[] { shown.Id, archived.Id, cancelled.Id }.Order(), first["items"]!.AsArray().Concat(second["items"]!.AsArray()).Select(row => row!.G("projectId")).Order());
        Assert.DoesNotContain(hidden.Id.ToString(), first.ToJsonString() + second.ToJsonString());
        Assert.DoesNotContain(unrelated.Id.ToString(), first.ToJsonString() + second.ToJsonString());
        await Provenance(hidden.Id, TestData.Alex, 404); Assert.Equal(before, t.State());
        await t.SetFlag("people_screens", false);
        var old = await (await f.As(TestData.Alex).GetAsync("/api/v1/templates")).Json();
        Assert.Equal(4, old["templates"]!.AsArray().Single(row => row!.G("id") == template.G("id"))!.I("projects"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/templates/{template.G("id")}/projects?page=bad")).Json(404);
    }

    [Fact]
    public async Task Source_null_partial_and_inconsistent_versions_remain_unknown_without_substituting_the_latest()
    {
        await t.SetFlag("people_screens", true); var template = await t.New(); await t.Publish(template); var project = await t.Project(template.G("id"));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).TemplateVersion = 99; return await db.SaveChangesAsync(); });
        var before = t.State(); var inconsistent = await Provenance(project.Id); Assert.Equal("Unavailable", inconsistent.S("state")); Assert.Null(inconsistent["source"]); Assert.Null(inconsistent["newer"]);
        Assert.Equal(before, t.State());
        var noTemplate = await t.Data.Project(); Assert.Equal("NotRecorded", (await Provenance(noTemplate.Id)).S("state"));
    }

    [Fact]
    public async Task Successful_old_POST_receipts_contain_no_new_feature_payload_when_replayed_after_flags_turn_off()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true); var template = await t.New(TemplateScreenData.Risks);
        var key = Guid.NewGuid().ToString(); var first = await t.Publish(template, key);
        Assert.Equal(new[] { "id", "version", "status", "rowVersion" }, first.AsObject().Select(pair => pair.Key));
        var client = f.As(TestData.Admin); var previewKey = Guid.NewGuid().ToString(); client.DefaultRequestHeaders.Add("Idempotency-Key", previewKey);
        var body = new { startDate = "2026-11-02", disciplineIds = new[] { await t.Data.Discipline("Civil") } };
        var preview = await (await client.Post($"/api/v1/templates/{template.G("id")}/preview", body)).Json();
        Assert.False(preview.AsObject().ContainsKey("risks")); Assert.False(preview["counts"]!.AsObject().ContainsKey("risks"));
        var draftKey = Guid.NewGuid().ToString(); var draftClient = f.As(TestData.Admin); draftClient.DefaultRequestHeaders.Add("Idempotency-Key", draftKey);
        var createdResponse = await draftClient.Post($"/api/v1/templates/{template.G("id")}/draft", new { });
        var draft = await createdResponse.Json(201);
        Assert.Equal($"/api/v1/templates/{draft.G("id")}", createdResponse.Headers.Location?.OriginalString);
        Assert.Equal(new[] { "id", "rowVersion" }, draft.AsObject().Select(pair => pair.Key));
        await t.SetFlag("people_screens", false); await t.SetFlag("register_screens", false); var before = t.State();
        Assert.Equal(first.ToJsonString(), (await t.Publish(template, key)).ToJsonString());
        Assert.Equal(preview.ToJsonString(), (await (await client.Post($"/api/v1/templates/{template.G("id")}/preview", body)).Json()).ToJsonString());
        var replayed = await draftClient.Post($"/api/v1/templates/{template.G("id")}/draft", new { });
        // The established generic receipt stores status/content type/body, not the original Location header.
        Assert.Null(replayed.Headers.Location); Assert.Equal("true", Assert.Single(replayed.Headers.GetValues("Idempotent-Replayed")));
        Assert.Equal(draft.ToJsonString(), (await replayed.Json(201)).ToJsonString()); Assert.Equal(before, t.State());
        var dark = await t.Read(draft.G("id")); Assert.Null(dark["risks"]); Assert.Null(dark["screen"]);
        await t.SetFlag("register_screens", true); await t.SetFlag("people_screens", true); var restored = await t.Read(draft.G("id"));
        Assert.Equal(2, restored["risks"]!.AsArray().Count); Assert.Equal("Available", restored["screen"]!["draftDiff"]!.S("state"));
    }
}
