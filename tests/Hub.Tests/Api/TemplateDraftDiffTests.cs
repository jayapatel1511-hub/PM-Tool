using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR/AC-SCR-81/84: exact recorded copy identity; fresh child IDs are not content changes.
public sealed class TemplateDraftDiffTests : IDisposable
{
    readonly HubFactory f = new(); readonly TemplateScreenData t;
    public TemplateDraftDiffTests() { t = new(f); }
    public void Dispose() => f.Dispose();
    static JsonNode Diff(JsonNode template) => template["screen"]!["draftDiff"]!;

    [Fact]
    public async Task Duplicate_names_refs_relationships_and_saved_edits_compare_to_the_actual_copy_not_latest_or_a_rebased_request()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true);
        var source = await t.New(TemplateScreenData.Risks); await t.Publish(source);
        var draft = await t.Draft(source.G("id")); var initial = Diff(draft);
        Assert.Equal("Available", initial.S("state")); Assert.Equal(0, initial.I("totalCount")); Assert.Equal(source.G("id"), initial["source"]!.G("id"));
        Assert.NotEqual(source["tasks"]![0]!.S("ref"), draft["tasks"]![0]!.S("ref"));
        await t.Save(draft, body => {
            body["tasks"]![0]!["name"] = "Renamed first duplicate";
            body["risks"]![0]!["probability"] = 3;
            body["risks"]!.AsArray().RemoveAt(1);
            body["risks"]!.AsArray().Add(new JsonObject { ["ref"] = "new-risk", ["title"] = "Added risk", ["probability"] = 1, ["impact"] = 1 });
        });
        var saved = await t.Read(draft.G("id")); var changed = Diff(saved); var rows = changed["changes"]!.AsArray();
        Assert.Equal(4, changed.I("totalCount")); Assert.Contains(rows, row => row!.S("kind") == "Task" && row.S("change") == "Changed" && row.S("beforeName") == "Same synthetic task");
        Assert.Equal(1, changed["counts"]!.I("added")); Assert.Equal(1, changed["counts"]!.I("removed")); Assert.Equal(2, changed["counts"]!.I("changed"));
        Assert.DoesNotContain(rows, row => row!.S("kind") == "Dependency");
        var returned = await t.Draft(source.G("id"), expect: 200); Assert.Equal(draft.G("id"), returned.G("id"));
        Assert.Equal(changed.ToJsonString(), Diff(returned).ToJsonString());
        var before = t.State(); await t.Read(draft.G("id")); Assert.Equal(before, t.State());
    }

    [Fact]
    public async Task A_retired_v3_can_be_copied_while_v5_exists_and_returning_that_draft_from_v5_does_not_change_origin()
    {
        await t.SetFlag("people_screens", true); var version = await t.New(); Guid source = Guid.Empty;
        for (var number = 1; number <= 5; number++) { await t.Publish(version); if (number == 3) source = version.G("id"); if (number < 5) version = await t.Draft(version.G("id")); }
        var copied = await t.Draft(source); Assert.Equal(source, Diff(copied)["source"]!.G("id")); Assert.Equal(3, Diff(copied)["source"]!.I("version"));
        var again = await t.Draft(version.G("id"), expect: 200); Assert.Equal(copied.G("id"), again.G("id")); Assert.Equal(source, Diff(again)["source"]!.G("id"));
    }

    [Fact]
    public async Task Hidden_sections_have_explicit_incomplete_counts_and_legacy_future_or_incoherent_baselines_never_invent_zero_history()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true);
        var source = await t.New(TemplateScreenData.Risks); await t.Publish(source); var draft = await t.Draft(source.G("id"));
        await t.Save(draft, body => body["risks"]![0]!["title"] = "Changed hidden risk");
        await t.SetFlag("register_screens", false); var hidden = await t.Read(draft.G("id"));
        Assert.Contains(Diff(hidden)["incompleteSections"]!.AsArray(), row => row!.GetValue<string>() == "Risk"); Assert.Equal(0, Diff(hidden).I("totalCount"));
        Assert.DoesNotContain("Changed hidden risk", Diff(hidden).ToJsonString());
        await t.SetFlag("register_screens", true); Assert.Equal(1, Diff(await t.Read(draft.G("id"))).I("totalCount"));
        await f.DbAsync(async db => { var row = await db.Templates.SingleAsync(row => row.Id == draft.G("id")); row.CopiedFromTemplateId = null; row.CopiedFromSnapshot = null; return await db.SaveChangesAsync(); });
        var unknown = Diff(await t.Read(draft.G("id"))); Assert.Equal("NotRecorded", unknown.S("state")); Assert.Null(unknown["counts"]); Assert.Null(unknown["totalCount"]);
        await f.DbAsync(async db => { var row = await db.Templates.SingleAsync(row => row.Id == draft.G("id")); row.CopiedFromTemplateId = source.G("id"); row.CopiedFromSnapshot = "{\"schemaVersion\":2}"; return await db.SaveChangesAsync(); });
        var unavailable = Diff(await t.Read(draft.G("id"))); Assert.Equal("Unavailable", unavailable.S("state")); Assert.Null(unavailable["counts"]);
    }

    [Fact]
    public async Task Diff_pagination_has_exact_visible_total_stable_identity_order_and_ignores_new_controls_off()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true);
        var source = await t.New(TemplateScreenData.Risks); await t.Publish(source); var draft = await t.Draft(source.G("id"));
        await t.Save(draft, body => { body["header"]!["name"] = "Changed header"; body["tasks"]![0]!["name"] = "Changed task"; body["risks"]![0]!["impact"] = 1; });
        var all = Diff(await t.Read(draft.G("id"))); var one = Diff(await t.Read(draft.G("id"), query: "?changePage=1&changePageSize=1"));
        var two = Diff(await t.Read(draft.G("id"), query: "?changePage=2&changePageSize=1"));
        Assert.Equal(3, all.I("totalCount")); Assert.Equal(3, one.I("totalCount")); Assert.Equal(3, two.I("totalCount"));
        Assert.Equal(all["changes"]![0]!.ToJsonString(), Assert.Single(one["changes"]!.AsArray())!.ToJsonString());
        Assert.Equal(all["changes"]![1]!.ToJsonString(), Assert.Single(two["changes"]!.AsArray())!.ToJsonString());
        await t.SetFlag("people_screens", false); var dark = await t.Read(draft.G("id"));
        Assert.Null(dark["screen"]); Assert.Equal(dark.ToJsonString(), (await t.Read(draft.G("id"), query: "?changePage=bad&changePageSize=bad")).ToJsonString());
    }

    [Theory]
    [InlineData("?changePage=bad")]
    [InlineData("?changePage=2147483647&changePageSize=200")]
    [InlineData("?changePageSize=0")]
    public async Task Enabled_page_controls_refuse_invalid_or_overflow_offsets_without_writes(string query)
    {
        await t.SetFlag("people_screens", true); var template = await t.New(); var before = t.State();
        await t.Read(template.G("id"), query: query, expect: 400); Assert.Equal(before, t.State());
    }

    [Fact]
    public async Task Concurrent_new_draft_requests_share_one_real_copy_and_current_version_guards_keep_published_content_immutable()
    {
        await t.SetFlag("people_screens", true); var source = await t.New(); await t.Publish(source);
        var replies = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => f.As(TestData.Admin).Post($"/api/v1/templates/{source.G("id")}/draft", new { })));
        Assert.Equal(new[] { 200, 201 }, replies.Select(reply => (int)reply.StatusCode).Order());
        var rows = await Task.WhenAll(replies.Select(reply => reply.Json((int)reply.StatusCode)));
        Assert.Single(rows.Select(row => row.G("id")).Distinct()); var draft = await t.Read(rows[0].G("id"));
        await t.Publish(draft); var before = t.State(); await t.Save(draft, expect: 422); Assert.Equal(before, t.State());
    }

    [Fact]
    public async Task Every_structural_kind_uses_recorded_identity_including_basis_remove_add_roles_order_and_working_day_header()
    {
        await t.SetFlag("people_screens", true); await t.SetFlag("register_screens", true); await t.SetFlag("template_roles", true); await t.SetFlag("template_working_days", true);
        var source = await t.New(TemplateScreenData.Risks);
        await t.Save(source, body => body["roles"] = new JsonArray(new JsonObject { ["ref"] = "new-role", ["disciplineId"] = source["disciplines"]![0]!.S("disciplineId"),
            ["label"] = "Synthetic role", ["startMilestoneRef"] = source["milestones"]![0]!.S("ref"), ["endMilestoneRef"] = source["milestones"]![1]!.S("ref"),
            ["startOffsetDays"] = 0, ["endOffsetDays"] = 0, ["hours"] = 12.00m }));
        source = await t.Read(source.G("id"));
        var suggestion = new { templateDisciplineId = source["disciplines"]![0]!.G("templateDisciplineId"), kind = "Assumption", title = "Same synthetic basis", scope = "Synthetic scope", statement = "Synthetic statement" };
        await (await f.As(TestData.Admin).Post($"/api/v1/templates/{source.G("id")}/design-basis", suggestion)).Json(201);
        source = await t.Read(source.G("id")); await t.Publish(source); var draft = await t.Draft(source.G("id"));
        Assert.Equal(0, Diff(draft).I("totalCount"));
        await t.Save(draft, body => {
            body["header"]!["offsetDayKind"] = "WorkingDays";
            body["disciplines"]![0]!["isDefaultIncluded"] = false;
            body["milestones"]![0]!["name"] = "Changed milestone";
            body["deliverables"]![0]!["description"] = "Changed description";
            var tasks = body["tasks"]!.AsArray(); var first = tasks[0]!.DeepClone(); tasks.RemoveAt(0); tasks.Add(first);
            body["roles"]![0]!["note"] = "Changed role note"; body["roles"]![0]!["hours"] = 12m;
            body["risks"]![0]!["impact"] = 1;
            body["dependencies"] = new JsonArray();
        });
        var current = await t.Read(draft.G("id")); var basisId = current["basisSuggestions"]![0]!.G("id");
        await (await f.As(TestData.Admin).DeleteAsync($"/api/v1/templates/{draft.G("id")}/design-basis/{basisId}")).Json(204);
        current = await t.Read(draft.G("id"));
        await (await f.As(TestData.Admin).Post($"/api/v1/templates/{draft.G("id")}/design-basis", new { templateDisciplineId = current["disciplines"]![0]!.G("templateDisciplineId"),
            suggestion.kind, suggestion.title, suggestion.scope, statement = "New actual basis row with the same title" })).Json(201);
        var rows = Diff(await t.Read(draft.G("id")))["changes"]!.AsArray();
        Assert.Equal(new[] { "Basis", "Deliverable", "Dependency", "Discipline", "Header", "Milestone", "Risk", "Role", "Task" }, rows.Select(row => row!.S("kind")).Distinct());
        Assert.Equal(new[] { "Added", "Removed" }, rows.Where(row => row!.S("kind") == "Basis").Select(row => row!.S("change")).Order());
        Assert.DoesNotContain(rows.Single(row => row!.S("kind") == "Role")!["fields"]!.AsArray(), field => field!.S("field") == "hours");
    }
}
