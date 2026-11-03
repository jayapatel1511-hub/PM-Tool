using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class DeliverableRegisterRuleTests
{
    [Fact]
    public void Actual_issue_identity_wins_even_when_the_recorded_dates_tie_or_the_identity_order_is_reversed()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var time = DateTimeOffset.Parse("2026-11-02T12:00:00Z");
        DeliverableEndpoints.RegisterIssueIdentity[] issues = [new(first, time), new(second, time)];
        Assert.Equal(second, DeliverableEndpoints.CurrentRegisterIssue(second, issues));
        Assert.Equal(second, DeliverableEndpoints.CurrentRegisterIssue(second, issues.Reverse().ToArray()));
        Assert.Null(DeliverableEndpoints.CurrentRegisterIssue(Guid.NewGuid(), issues));
    }

    [Fact]
    public void Legacy_timestamp_ties_remain_unknown_and_only_a_unique_latest_record_is_selected()
    {
        var a = new DeliverableEndpoints.RegisterIssueIdentity(Guid.NewGuid(), DateTimeOffset.Parse("2026-11-02T12:00:00Z"));
        Assert.Equal(a.Id, DeliverableEndpoints.CurrentRegisterIssue(null, [a, new(Guid.NewGuid(), a.CreatedAt.AddDays(-1))]));
        Assert.Null(DeliverableEndpoints.CurrentRegisterIssue(null, [a, new(Guid.NewGuid(), a.CreatedAt)]));
        Assert.Null(DeliverableEndpoints.CurrentRegisterIssue(null, []));
    }

    [Fact]
    public void Drawing_pattern_is_a_bounded_literal_comparison_with_one_ASCII_digit_per_hash_and_missing_stays_unknown()
    {
        Assert.True(DeliverableNumberingEndpoints.Matches("C-123", "C-###")); Assert.False(DeliverableNumberingEndpoints.Matches("C12", "C-###"));
        Assert.False(DeliverableNumberingEndpoints.Matches("C-١٢٣", "C-###")); Assert.False(DeliverableNumberingEndpoints.Matches("c-123", "C-###"));
        Assert.True(DeliverableNumberingEndpoints.Matches("C.[2]", "C.[#]")); Assert.False(DeliverableNumberingEndpoints.Matches("C-123", "C.###"));
        Assert.Null(DeliverableNumberingEndpoints.Matches(null, "C-###")); Assert.Null(DeliverableNumberingEndpoints.Matches("C12", " "));
        Assert.Equal("C12", DeliverableNumberingEndpoints.Number(" C12 ")); Assert.Null(DeliverableNumberingEndpoints.Pattern(" "));
    }

    [Fact]
    public void Drawing_values_refuse_control_characters_and_excess_length_without_running_user_regex()
    {
        Assert.Throws<ApiException>(() => DeliverableNumberingEndpoints.Number("C12\n"));
        Assert.Throws<ApiException>(() => DeliverableNumberingEndpoints.Pattern("C-\t###"));
        Assert.Throws<ApiException>(() => DeliverableNumberingEndpoints.Pattern(new string('#', 121)));
        Assert.True(DeliverableNumberingEndpoints.Matches("(a+)+", "(a+)+"));
    }
}

/// Normal commands on private factory data; no direct workflow/history mutation is used as evidence.
public sealed class DeliverableRegisterTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public DeliverableRegisterTests() { d = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(Guid id, string who = TestData.Pm) => await (await f.As(who).GetAsync($"/api/v1/deliverables/{id}")).Json();
    async Task<JsonNode> New(Guid project, object? extra = null)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Synthetic drawing " + Guid.NewGuid().ToString("N")[..6], ["projectDisciplineId"] = d.ProjectDiscipline(project, "Civil"),
            ["deliverableTypeId"] = await d.DeliverableType(), ["ownerId"] = d.User(TestData.Alex), ["requiresReview"] = false };
        if (extra is not null) foreach (var property in extra.GetType().GetProperties()) body[property.Name] = property.GetValue(extra);
        return await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", body)).Json(201);
    }
    async Task<Guid> Purpose(string name = "For review") => (await (await f.As(TestData.Pm).GetAsync("/api/v1/reference")).Json())["issuePurposes"]!.AsArray().Single(row => row!.S("name") == name)!.G("id");
    async Task<Guid> Code(string code) => (await (await f.As(TestData.Pm).GetAsync("/api/v1/reference")).Json())["issueReturnCodes"]!.AsArray().Single(row => row!.S("code") == code)!.G("id");
    async Task Issue(Guid id, Guid? purpose = null, string revision = "A", string date = "2026-11-02", int status = 200)
    {
        var row = (await Get(id))["deliverable"]!;
        await (await f.As(TestData.Pm).Post($"/api/v1/deliverables/{id}/issue", new { purposeOfIssueId = purpose, issuedDate = date, revision, rowVersion = row.I("rowVersion") })).Json(status);
    }
    async Task Step(Guid id, string to) => await (await f.As(TestData.Pm).Post($"/api/v1/deliverables/{id}/transition", new {
        toStatus = to, reason = "Synthetic documented lifecycle change", comment = "Synthetic client revision request", rowVersion = (await Get(id))["deliverable"]!.I("rowVersion") })).Json();
    static JsonNode Current(JsonNode detail) => detail["deliverable"]!["registerInfo"]!["currentIssue"]!;
    static JsonNode History(JsonNode detail, Guid id) => detail["issues"]!.AsArray().Single(issue => issue!.G("id") == id)!;
    async Task<JsonNode> Response(Guid id, Guid issueId, Guid? code, string? date, string? reason = null, string who = TestData.Pm, int status = 200)
    {
        var detail = await Get(id); var issue = History(detail, issueId);
        return await (await f.As(who).Patch($"/api/v1/deliverables/{id}/issues/{issueId}/response", new { returnCodeId = code, responseDate = date,
            rowVersion = detail["deliverable"]!.I("rowVersion"), issueRowVersion = issue["registerInfo"]!.I("rowVersion"), reason })).Json(status);
    }
    async Task<JsonNode> List(Guid project, string query = "", int status = 200) => await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project}/deliverables?{query}")).Json(status);
    Task<string> Immutable(Guid id) => f.DbAsync(async db => JsonSerializer.Serialize(await db.DeliverableIssues.AsNoTracking().Where(issue => issue.DeliverableId == id).OrderBy(issue => issue.Id)
        .Select(issue => new { issue.Id, issue.ProjectId, issue.DeliverableId, issue.IssuedDate, issue.Revision, issue.IssuedTo, issue.TransmittalUrl, issue.Note, issue.IssuedBy, issue.CreatedAt, issue.PurposeOfIssueId, issue.PurposeLabelSnapshot }).ToListAsync()));

    [Fact]
    public async Task Real_issue_for_review_then_explicit_B_response_matches_current_register_history_and_reference_filters()
    {
        await Flag(); var project = await d.Project(); var id = (await New(project.Id)).G("id"); var purpose = await Purpose(); var b = await Code("B");
        await Issue(id, status: 400); await Issue(id, purpose, date: "2026-10-30"); var issued = await Get(id); var issueId = Current(issued).G("id"); var immutable = await Immutable(id);
        var before = f.Db(db => db.ActivityLog.Count(entry => entry.ItemId == id)); var saved = await Response(id, issueId, b, "2026-11-01");
        var detail = await Get(id); Assert.Equal("Issued", detail["deliverable"]!.S("status")); Assert.Equal(immutable, await Immutable(id));
        Assert.Equal(("For review", "B", "2026-11-01"), (Current(detail).S("purpose"), Current(detail).S("returnCode"), Current(detail).S("responseDate")));
        Assert.Equal("B", History(detail, issueId)["registerInfo"]!.S("returnCode")); Assert.Equal(issued["issues"]!.AsArray().Count, detail["issues"]!.AsArray().Count);
        Assert.Equal(before + 1, f.Db(db => db.ActivityLog.Count(entry => entry.ItemId == id)));
        var audit = f.Db(db => db.ActivityLog.Single(entry => entry.ItemId == id && entry.Action == "IssueResponseRecorded"));
        Assert.Equal("IssueResponseRecorded", audit.Action); Assert.Equal(project.Id, audit.ProjectId); Assert.Contains(issueId.ToString(), audit.Snapshot);
        Assert.True(saved.I("rowVersion") > issued["deliverable"]!.I("rowVersion")); Assert.True(saved.I("issueRowVersion") > History(issued, issueId)["registerInfo"]!.I("rowVersion"));
        Assert.Equal(id, Assert.Single((await List(project.Id, $"purposeOfIssueId={purpose}&returnCodeId={b}")).AsArray())!.G("id"));
        var code = await Code("A"); Assert.Empty((await List(project.Id, $"returnCodeId={code}")).AsArray());
        var count = f.Db(db => db.ActivityLog.Count(entry => entry.ItemId == id)); await Response(id, issueId, b, "2026-11-01"); Assert.Equal(count, f.Db(db => db.ActivityLog.Count(entry => entry.ItemId == id)));
    }

    [Fact]
    public async Task Backdated_reissue_at_the_same_clock_tracks_actual_identity_and_an_old_response_never_overwrites_the_current_one()
    {
        await Flag(); var project = await d.Project(); var id = (await New(project.Id)).G("id"); var review = await Purpose(); var construction = await Purpose("For construction");
        await Issue(id, review); var first = Current(await Get(id)).G("id"); await Response(id, first, await Code("B"), "2026-11-02");
        await Step(id, DeliverableStatus.RevisionRequired); await Step(id, DeliverableStatus.InProgress); await Step(id, DeliverableStatus.ReadyToIssue);
        await Issue(id, construction, "B", "2026-10-31"); var detail = await Get(id); var latest = Current(detail).G("id"); Assert.NotEqual(first, latest);
        Assert.Equal(("B", "For construction"), (Current(detail).S("revision"), Current(detail).S("purpose"))); Assert.Null(Current(detail)["returnCodeId"]);
        await Response(id, first, await Code("C"), "2026-11-02", "Corrected transcription from the recipient response");
        detail = await Get(id); Assert.Equal(latest, Current(detail).G("id")); Assert.Null(Current(detail)["returnCodeId"]); Assert.Equal("C", History(detail, first)["registerInfo"]!.S("returnCode"));
        Assert.Empty((await List(project.Id, $"returnCodeId={await Code("C")}")).AsArray());
        Assert.Equal(latest, f.Db(db => db.Deliverables.Single(deliverable => deliverable.Id == id).CurrentIssueId));
    }

    [Fact]
    public async Task Parent_IfMatch_never_substitutes_for_missing_or_stale_issue_baseline_and_dirty_versions_do_not_advance_on_refusal()
    {
        await Flag(); var project = await d.Project(); var id = (await New(project.Id)).G("id"); await Issue(id, await Purpose()); var detail = await Get(id); var issueId = Current(detail).G("id");
        var parentVersion = detail["deliverable"]!.I("rowVersion"); var issueVersion = History(detail, issueId)["registerInfo"]!.I("rowVersion"); var b = await Code("B"); var path = $"/api/v1/deliverables/{id}/issues/{issueId}/response";
        await (await f.As(TestData.Pm).Patch(path, new { returnCodeId = b, responseDate = "2026-11-02", rowVersion = parentVersion, issueRowVersion = (int?)null }, parentVersion)).Json(428);
        await (await f.As(TestData.Pm).Patch(path, new { returnCodeId = b, responseDate = "2026-11-02", rowVersion = parentVersion, issueRowVersion = issueVersion - 1 }, parentVersion)).Json(409);
        Assert.Equal(parentVersion, (await Get(id))["deliverable"]!.I("rowVersion")); Assert.Null(Current(await Get(id))["returnCodeId"]);
        await (await f.As(TestData.Pm).Patch(path, new { returnCodeId = b, responseDate = "2026-11-02", rowVersion = parentVersion, issueRowVersion = issueVersion }, parentVersion)).Json();
        await (await f.As(TestData.Pm).Patch(path, new { returnCodeId = b, responseDate = "2026-11-02", rowVersion = parentVersion, issueRowVersion = issueVersion }, parentVersion)).Json(409);
    }

    [Fact]
    public async Task Response_pair_dates_authority_and_exact_target_are_guarded_and_correction_clear_does_not_mutate_status()
    {
        await Flag(); var project = await d.Project(); var id = (await New(project.Id)).G("id"); await Issue(id, await Purpose()); var issueId = Current(await Get(id)).G("id"); var code = await Code("B");
        await Response(id, issueId, code, null, status: 400); await Response(id, issueId, null, "2026-11-02", status: 400);
        await Response(id, issueId, code, "2026-11-01", status: 400); await Response(id, issueId, code, "2026-11-03", status: 400);
        foreach (var who in new[] { TestData.Alex, TestData.Omar, TestData.Rita }) await Response(id, issueId, code, "2026-11-02", who: who, status: 403);
        var other = (await New(project.Id)).G("id"); var otherBefore = await Get(other); var originalBefore = await Get(id);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{other}/issues/{issueId}/response", new { returnCodeId = code, responseDate = "2026-11-02",
            rowVersion = otherBefore["deliverable"]!.I("rowVersion"), issueRowVersion = History(originalBefore, issueId)["registerInfo"]!.I("rowVersion") })).Json(404);
        Assert.True(JsonNode.DeepEquals(otherBefore, await Get(other))); Assert.True(JsonNode.DeepEquals(originalBefore, await Get(id)));
        var detail = await Get(id); await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{id}/issues/{issueId}/response", new { returnCodeId = code, responseDate = "2026-11-02", rowVersion = detail["deliverable"]!.I("rowVersion"), issueRowVersion = History(detail, issueId)["registerInfo"]!.I("rowVersion"), revision = "FORBIDDEN" })).Json(400);
        await Response(id, issueId, code, "2026-11-02", who: TestData.Marc); await Response(id, issueId, null, null, status: 400);
        await Response(id, issueId, null, null, "Removed an incorrectly transcribed recipient response"); detail = await Get(id);
        Assert.Null(Current(detail)["returnCodeId"]); Assert.Null(Current(detail)["responseDate"]); Assert.Equal("Issued", detail["deliverable"]!.S("status")); Assert.Single(detail["issues"]!.AsArray());
    }

    [Fact]
    public async Task Retired_or_renamed_references_keep_recorded_snapshots_and_missing_choices_are_never_defaulted()
    {
        await Flag(); var project = await d.Project(); var id = (await New(project.Id)).G("id"); var purpose = await Purpose(); var code = await Code("B"); await Issue(id, purpose); var issueId = Current(await Get(id)).G("id"); await Response(id, issueId, code, "2026-11-02");
        async Task PatchRef(string kind, Guid key, object body) { var row = (await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/{kind}")).Json()).AsArray().Single(row => row!.G("id") == key)!; await (await f.As(TestData.Admin).Patch($"/api/v1/admin/{kind}/{key}", body, row.I("rowVersion"))).Json(); }
        await PatchRef("issuePurposes", purpose, new { name = "Renamed purpose", isActive = false }); await PatchRef("issueReturnCodes", code, new { name = "Renamed return", code = "BB", isActive = false });
        var detail = await Get(id); Assert.Equal(("For review", "B", "Accepted with comments"), (Current(detail).S("purpose"), Current(detail).S("returnCode"), Current(detail).S("returnLabel")));
        Assert.Single((await List(project.Id, $"purposeOfIssueId={purpose}&returnCodeId={code}")).AsArray());
        await Response(id, issueId, code, "2026-11-02"); // existing inactive identity is retained; it is not reassigned from its renamed list label
        var second = (await New(project.Id)).G("id"); await Issue(second, purpose, status: 400);
        await List(project.Id, $"returnCodeId={Guid.NewGuid()}", 400);
        var refs = (await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/issuePurposes")).Json()).AsArray();
        foreach (var row in refs.Where(row => row!["isActive"]!.GetValue<bool>())) await PatchRef("issuePurposes", row!.G("id"), new { isActive = false });
        await Issue(second, status: 400); Assert.Empty((await Get(second))["issues"]!.AsArray());
    }

    [Fact]
    public async Task Project_pattern_is_PM_versioned_and_client_number_mismatch_warns_while_create_and_owner_edit_still_save()
    {
        await Flag(); var project = await d.Project(); var path = $"/api/v1/projects/{project.Id}/deliverable-numbering"; var initial = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Null(initial["pattern"]); await (await f.As(TestData.Marc).Put(path, new { pattern = "C-###", rowVersion = initial.I("rowVersion") })).Json(403);
        await (await f.As(TestData.Pm).Put(path, new { pattern = "C-###", rowVersion = initial.I("rowVersion") })).Json();
        await (await f.As(TestData.Pm).Put(path, new { pattern = "D-###", rowVersion = initial.I("rowVersion") })).Json(409);
        var made = await New(project.Id, new { clientDrawingNumber = "C12" }); Assert.Contains(made["warnings"]!.AsArray(), warning => warning!.GetValue<string>() == "Does not match C-###");
        var id = made.G("id"); var detail = await Get(id); Assert.False(detail["deliverable"]!["registerInfo"]!["numberMatchesPattern"]!.GetValue<bool>());
        await (await f.As(TestData.Alex).Patch($"/api/v1/deliverables/{id}", new { clientDrawingNumber = "C-123" }, detail["deliverable"]!.I("rowVersion"))).Json();
        detail = await Get(id); Assert.True(detail["deliverable"]!["registerInfo"]!["numberMatchesPattern"]!.GetValue<bool>());
        Assert.Empty(detail["issues"]!.AsArray()); Assert.Equal("Not Started", detail["deliverable"]!.S("status"));
    }

    [Fact]
    public async Task No_due_date_filter_keeps_the_exact_four_open_records_and_all_other_filters_remain_ANDed()
    {
        await Flag(); var project = await d.Project(); var expected = new List<Guid>();
        foreach (var state in new[] { "Not Started", "In Progress", "On Hold", "Revision Required" })
        {
            var id = (await New(project.Id)).G("id"); expected.Add(id);
            if (state == "In Progress") await Step(id, state);
            if (state == "On Hold") await Step(id, state);
            if (state == "Revision Required") { await Issue(id, await Purpose()); await Step(id, state); }
        }
        var issued = (await New(project.Id)).G("id"); await Issue(issued, await Purpose());
        var accepted = (await New(project.Id)).G("id"); await Issue(accepted, await Purpose()); await Step(accepted, "Accepted");
        var cancelled = (await New(project.Id)).G("id"); await Step(cancelled, "Cancelled"); await New(project.Id, new { dueDate = "2026-11-12" });
        var all = (await List(project.Id, "indicator=open")).AsArray().Where(row => row!["dueDate"] is null).Select(row => row!.G("id")).ToArray();
        var filtered = (await List(project.Id, "noDueDate=true")).AsArray().Select(row => row!.G("id")).ToArray(); Assert.Equal(expected.Order(), filtered.Order()); Assert.Equal(all.Order(), filtered.Order());
        Assert.Empty((await List(project.Id, "noDueDate=true&dueFrom=2026-11-01")).AsArray()); Assert.Empty((await List(project.Id, "noDueDate=true&status=Issued")).AsArray());
        Assert.Equal(4, (await List(project.Id, $"noDueDate=true&disciplineId={d.ProjectDiscipline(project.Id, "Civil")}")).AsArray().Count);
    }

    [Fact]
    public async Task Dark_commands_wires_and_real_issuance_are_preserved_while_new_metadata_and_routes_stay_hidden()
    {
        var project = await d.Project(); var id = (await New(project.Id)).G("id"); await Issue(id); var off = await Get(id);
        Assert.False(off["deliverable"]!.AsObject().ContainsKey("registerInfo")); Assert.False(off["issues"]![0]!.AsObject().ContainsKey("registerInfo"));
        Assert.NotNull(f.Db(db => db.Deliverables.Single(deliverable => deliverable.Id == id).CurrentIssueId));
        Assert.False((await (await f.As(TestData.Pm).GetAsync("/api/v1/reference")).Json()).AsObject().ContainsKey("issuePurposes"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/deliverable-numbering")).StatusCode);
        await Flag(); var on = await Get(id); Assert.Null(Current(on)["purposeOfIssueId"]); Assert.Null(Current(on)["responseDate"]);
        await Flag(false); Assert.True(JsonNode.DeepEquals(off, await Get(id)));
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{id}", new { clientDrawingNumber = "C12" }, off["deliverable"]!.I("rowVersion"))).Json(400);
        await List(project.Id, "noDueDate=true", 400);
    }

    [Fact]
    public async Task Saved_register_definition_keeps_legacy_fields_while_dark_and_restores_new_filters_layout_and_columns_when_reenabled()
    {
        await Flag(); var project = await d.Project();
        var parameters = new Dictionary<string, string> {
            ["purposeOfIssueId"] = (await Purpose()).ToString(), ["returnCodeId"] = (await Code("B")).ToString(), ["noDueDate"] = "true", ["layout"] = "dates",
            ["group"] = "discipline", ["status"] = "In Progress", ["cols"] = "key,name,due,clientDrawingNumber,purposeOfIssue,returnCode,responseDate"
        };
        async Task<JsonNode> Save(Dictionary<string, string> values, int status = 201) => await (await f.As(TestData.Pm).Post("/api/v1/views", new {
            name = "Synthetic register view " + Guid.NewGuid().ToString("N")[..6], listType = "deliverables", projectId = project.Id, scope = "Personal", @params = values })).Json(status);
        async Task<JsonNode> Read(Guid id) => (await (await f.As(TestData.Pm).GetAsync($"/api/v1/views?listType=deliverables&projectId={project.Id}")).Json())["views"]!.AsArray().Single(row => row!.G("id") == id)!;
        var made = await Save(parameters); var id = made.G("id"); var on = await Read(id);
        foreach (var (key, value) in parameters) Assert.Equal(value, on["params"]!.S(key));
        var stored = f.Db(db => db.SavedViews.Single(view => view.Id == id).Filters);
        await Flag(false); var dark = await Read(id);
        Assert.Equal("key,name,due", dark["params"]!.S("cols")); Assert.Equal("discipline", dark["params"]!.S("group")); Assert.Equal("In Progress", dark["params"]!.S("status"));
        foreach (var key in new[] { "purposeOfIssueId", "returnCodeId", "noDueDate", "layout" }) Assert.Null(dark["params"]![key]);
        var dropped = dark["dropped"]!.AsArray().Select(value => value!.GetValue<string>()).ToArray();
        Assert.Equal(new[] { "cols", "layout", "noDueDate", "purposeOfIssueId", "returnCodeId" }, dropped.Order(StringComparer.Ordinal));
        foreach (var key in new[] { "purposeOfIssueId", "returnCodeId", "noDueDate", "layout", "cols" }) await Save(new() { [key] = parameters[key] }, 404);
        var legacy = await Save(new() { ["group"] = "status", ["cols"] = "key,name,due" }); Assert.Equal("status", (await Read(legacy.G("id")))["params"]!.S("group"));
        Assert.Equal(stored, f.Db(db => db.SavedViews.Single(view => view.Id == id).Filters));
        await Flag(); var restored = await Read(id);
        foreach (var (key, value) in parameters) Assert.Equal(value, restored["params"]!.S(key));
        foreach (var (key, value) in new[] { ("layout", "calendar"), ("noDueDate", "maybe"), ("purposeOfIssueId", Guid.NewGuid().ToString()), ("returnCodeId", Guid.NewGuid().ToString()) })
            await Save(new() { [key] = value }, 400);
        Assert.Equal(stored, f.Db(db => db.SavedViews.Single(view => view.Id == id).Filters));
    }
}
