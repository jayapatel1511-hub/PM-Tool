using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Tests.Api;

/// Client-export privacy and joins through normal commands on each test's private synthetic database.
public sealed class DeliverableClientExportTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData data;
    static readonly string[] Headers = ["Number", "Client drawing number", "Title", "Revision", "Purpose of issue", "Issue date", "Return code", "Status"];
    public DeliverableClientExportTests() { data = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Detail(Guid id) => await (await f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{id}")).Json();
    async Task<JsonNode> New(Guid project, string title, object? extra = null, string discipline = "Civil")
    {
        var body = new Dictionary<string, object?> { ["name"] = title, ["projectDisciplineId"] = data.ProjectDiscipline(project, discipline),
            ["deliverableTypeId"] = await data.DeliverableType(), ["ownerId"] = data.User(TestData.Alex), ["requiresReview"] = false };
        if (extra is not null) foreach (var property in extra.GetType().GetProperties()) body[property.Name] = property.GetValue(extra);
        return await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", body)).Json(201);
    }
    async Task<Guid> Ref(string list, string field, string value) => (await (await f.As(TestData.Pm).GetAsync("/api/v1/reference")).Json())[list]!
        .AsArray().Single(row => row!.S(field) == value)!.G("id");
    async Task<Guid> Issue(Guid id, Guid purpose, string revision = "A", string issuedDate = "2026-10-30")
    {
        await (await f.As(TestData.Marc).Post($"/api/v1/deliverables/{id}/issue", new { purposeOfIssueId = purpose, revision, issuedDate,
            issuedTo = "PRIVATE RECIPIENT", note = "PRIVATE TRANSMITTAL NOTE", rowVersion = (await Detail(id))["deliverable"]!.I("rowVersion") })).Json();
        return (await Detail(id))["deliverable"]!["registerInfo"]!["currentIssue"]!.G("id");
    }
    async Task Respond(Guid id, Guid issue, Guid code, string? reason = null)
    {
        var before = await Detail(id);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{id}/issues/{issue}/response", new { returnCodeId = code, responseDate = "2026-11-01",
            rowVersion = before["deliverable"]!.I("rowVersion"), issueRowVersion = before["issues"]!.AsArray().Single(row => row!.G("id") == issue)!["registerInfo"]!.I("rowVersion"), reason })).Json();
    }
    async Task Step(Guid id, string status) => await (await f.As(TestData.Pm).Post($"/api/v1/deliverables/{id}/transition", new { toStatus = status,
        reason = "Synthetic documented lifecycle", comment = "Synthetic recipient revision request", rowVersion = (await Detail(id))["deliverable"]!.I("rowVersion") })).Json();
    async Task Rename(string list, Guid id, object body)
    {
        var row = (await (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/{list}")).Json()).AsArray().Single(row => row!.G("id") == id)!;
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/{list}/{id}", body, row.I("rowVersion"))).Json();
    }
    async Task<byte[]> Download(Guid project, string format, string query = "", string who = TestData.Pm) =>
        await (await f.As(who).GetAsync($"/api/v1/projects/{project}/deliverables/client-export?format={format}&{query}")).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync();
    static Dictionary<string, string> Parts(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return zip.Entries.ToDictionary(entry => entry.FullName, entry => { using var reader = new StreamReader(entry.Open()); return reader.ReadToEnd(); });
    }
    static string[][] CsvRows(byte[] bytes)
    {
        using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'))) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false };
        parser.SetDelimiters(","); var rows = new List<string[]>(); while (!parser.EndOfData) rows.Add(parser.ReadFields()!); return rows.ToArray();
    }
    static string[][] Rows(byte[] bytes, string format)
    {
        if (format == "csv") return CsvRows(bytes);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Parse(Parts(bytes)["xl/worksheets/sheet1.xml"]).Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell => {
            var numeric = cell.Element(ns + "v")?.Value;
            if ((string?)cell.Attribute("s") == "2" && numeric is not null)
                return new DateTime(1899, 12, 30).AddDays(double.Parse(numeric, CultureInfo.InvariantCulture)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)) is { Length: > 0 } textValue ? textValue : numeric ?? "";
        }).ToArray()).ToArray();
    }
    static string AllText(byte[] bytes, string format) => format == "csv" ? Encoding.UTF8.GetString(bytes) : string.Join("\n", Parts(bytes).Values);

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Eight_columns_use_recorded_issue_bundle_and_no_internal_people_notes_hours_or_filter_metadata_escape(string format)
    {
        await Flag(); var project = await data.Project(); const string title = "=PUBLIC-DRAWING(\"quoted, value\")";
        var made = await New(project.Id, title, new { clientDrawingNumber = "00123", reviewerId = data.User(TestData.Diane), description = "PRIVATE DESCRIPTION SENTINEL" }); var id = made.G("id");
        var purpose = await Ref("issuePurposes", "name", "For review"); var code = await Ref("issueReturnCodes", "code", "B");
        var issue = await Issue(id, purpose); await Respond(id, issue, code);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{id}", new { revision = "PRIVATE FUTURE WORKING REVISION" }, (await Detail(id))["deliverable"]!.I("rowVersion"))).Json();
        await data.NewTask(project.Id, TestData.Pm, new { name = "PRIVATE TASK SENTINEL", deliverableId = id, estimatedHours = 1234.567m });
        await Rename("issuePurposes", purpose, new { name = "PRIVATE RENAMED PURPOSE", isActive = false });
        await Rename("issueReturnCodes", code, new { code = "ZZ", name = "PRIVATE RENAMED RETURN", isActive = false });
        var before = await Detail(id); var beforeExports = f.Db(db => db.ActivityLog.Count(entry => entry.ProjectId == project.Id && entry.Action == "Exported"));
        var bytes = await Download(project.Id, format, $"ownerId={data.User(TestData.Alex)}&purposeOfIssueId={purpose}&returnCodeId={code}"); var rows = Rows(bytes, format);
        Assert.Equal(Headers, rows[0]); Assert.Equal(2, rows.Length);
        Assert.Equal(new[] { made.S("key"), "00123", format == "csv" ? "'" + title : title, "A", "For review", "2026-10-30", "B", "Issued" }, rows[1]);
        var text = AllText(bytes, format);
        foreach (var value in new[] { "PRIVATE", "1234.567", "@hub.test", data.User(TestData.Alex).ToString(), purpose.ToString(), code.ToString(), issue.ToString(), "ownerId", "returnCodeId", "purposeOfIssueId" }) Assert.DoesNotContain(value, text);
        foreach (var name in f.Db(db => db.Users.Where(user => user.Email != TestData.Pm).Select(user => user.DisplayName).ToList())) Assert.DoesNotContain(name, text);
        Assert.True(JsonNode.DeepEquals(before, await Detail(id)), "export does not mutate status, issue, version, last activity or other detail facts");
        Assert.Equal(beforeExports + 1, f.Db(db => db.ActivityLog.Count(entry => entry.ProjectId == project.Id && entry.Action == "Exported")));
        var audit = f.Db(db => db.ActivityLog.Single(entry => entry.ProjectId == project.Id && entry.Action == "Exported")); Assert.Equal("[]", audit.Changes); Assert.DoesNotContain("PRIVATE", audit.ItemName!);
        if (format == "csv") Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        else
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheet = XDocument.Parse(Parts(bytes)["xl/worksheets/sheet1.xml"]);
            Assert.Equal("inlineStr", (string?)sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "B2").Attribute("t"));
            Assert.Equal("2", (string?)sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "F2").Attribute("s"));
            Assert.DoesNotContain("<f", Parts(bytes)["xl/worksheets/sheet1.xml"]); Assert.DoesNotContain(Parts(bytes).Keys, name => name.Contains("comments") || name.Contains("externalLink"));
        }
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Backdated_actual_reissue_stays_current_and_an_older_response_never_fills_the_new_issue_export(string format)
    {
        await Flag(); var project = await data.Project(); var made = await New(project.Id, "Synthetic current reissue"); var id = made.G("id");
        var review = await Ref("issuePurposes", "name", "For review"); var construction = await Ref("issuePurposes", "name", "For construction"); var b = await Ref("issueReturnCodes", "code", "B"); var c = await Ref("issueReturnCodes", "code", "C");
        var first = await Issue(id, review, issuedDate: "2026-11-01"); await Respond(id, first, b);
        await Step(id, "Revision Required"); await Step(id, "In Progress"); await Step(id, "Ready to Issue");
        var latest = await Issue(id, construction, "B", "2026-10-29"); await Respond(id, first, c, "Synthetic correction to an older recipient response");
        var before = await Detail(id); Assert.Equal(latest, before["deliverable"]!["registerInfo"]!["currentIssue"]!.G("id"));
        var rows = Rows(await Download(project.Id, format), format);
        Assert.Equal(Headers, rows[0]); Assert.Equal(new[] { made.S("key"), "", "Synthetic current reissue", "B", "For construction", "2026-10-29", "", "Issued" }, Assert.Single(rows.Skip(1)));
        Assert.Single(Rows(await Download(project.Id, format, $"returnCodeId={c}"), format));
        Assert.True(JsonNode.DeepEquals(before, await Detail(id)));
    }

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Combined_no_due_scope_and_other_data_filters_return_exactly_the_canonical_register_and_empty_search_is_not_metadata(string format)
    {
        await Flag(); var project = await data.Project(); var expected = await New(project.Id, "Synthetic undated scoped drawing");
        await New(project.Id, "Synthetic dated control", new { dueDate = "2026-11-10" }); await New(project.Id, "Synthetic other discipline", discipline: "Electrical");
        var terminal = await New(project.Id, "Synthetic issued control"); var purpose = await Ref("issuePurposes", "name", "For review"); var b = await Ref("issueReturnCodes", "code", "B");
        var terminalIssue = await Issue(terminal.G("id"), purpose); await Respond(terminal.G("id"), terminalIssue, b);
        var scope = $"disciplineId={data.ProjectDiscipline(project.Id, "Civil")}&ownerId={data.User(TestData.Alex)}&typeId={await data.DeliverableType()}&status=Not%20Started&noDueDate=true";
        foreach (var query in new[] { scope, scope + "&dueFrom=2026-11-01", $"purposeOfIssueId={purpose}&returnCodeId={b}", "q=PRIVATE%20FILTER%20Alex%20Chen" })
        {
            var listed = (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/deliverables?{query}")).Json()).AsArray();
            var bytes = await Download(project.Id, format, query); var rows = Rows(bytes, format); Assert.Equal(Headers, rows[0]);
            Assert.Equal(listed.Select(row => row!.S("key")).Order(StringComparer.Ordinal), rows.Skip(1).Select(row => row[0]).Order(StringComparer.Ordinal));
            Assert.DoesNotContain("PRIVATE FILTER", AllText(bytes, format)); Assert.DoesNotContain("Alex Chen", AllText(bytes, format));
            if (query == scope) Assert.Equal(expected.S("key"), Assert.Single(rows.Skip(1))[0]);
            if (query.Contains("dueFrom=")) Assert.Single(rows);
        }
    }

    [Fact]
    public async Task Visibility_and_readonly_match_the_register_dark_route_writes_no_access_event_and_internal_export_is_unchanged()
    {
        await Flag(); var project = await data.Project(); await New(project.Id, "Synthetic public register drawing");
        foreach (var format in new[] { "csv", "xlsx" }) Assert.Equal(2, Rows(await Download(project.Id, format, who: TestData.Rita), format).Length);
        var ordinaryPath = $"/api/v1/projects/{project.Id}/deliverables/export?format=csv";
        var original = await (await f.As(TestData.Pm).GetAsync(ordinaryPath)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync();
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var p = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}")).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { visibility = "Restricted" }, p.I("rowVersion"))).Json();
        var registerStatus = (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{project.Id}/deliverables")).StatusCode;
        Assert.Equal(HttpStatusCode.NotFound, registerStatus);
        Assert.Equal(registerStatus, (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{project.Id}/deliverables/client-export?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/deliverables/client-export?format=pdf")).StatusCode);
        await Flag(false); var logs = f.Db(db => db.ActivityLog.Count(entry => entry.Action == "Exported"));
        foreach (var format in new[] { "csv", "xlsx" }) Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/deliverables/client-export?format={format}")).StatusCode);
        Assert.Equal(logs, f.Db(db => db.ActivityLog.Count(entry => entry.Action == "Exported")));
        Assert.Equal(original, await (await f.As(TestData.Pm).GetAsync(ordinaryPath)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
        await Flag(); Assert.Equal(original, await (await f.As(TestData.Pm).GetAsync(ordinaryPath)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Projection_keeps_unissued_revision_but_never_guesses_unknown_history_or_legacy_issuance()
    {
        await Flag(); var project = await data.Project(); var made = await New(project.Id, "Literal drawing title Alex Chen", new { revision = "PLANNED", clientDrawingNumber = "0007" });
        var observed = JsonSerializer.Deserialize<DeliverableEndpoints.DeliverableRow>((await Detail(made.G("id")))["deliverable"]!.ToJsonString(), JsonOpts.Web)!;
        var planned = DeliverableClientExportEndpoints.Project(observed, false); Assert.Equal("PLANNED", planned.Revision); Assert.Equal("0007", planned.ClientDrawingNumber); Assert.Equal("Literal drawing title Alex Chen", planned.Title);
        // Pure projection inputs represent legacy uncertainty; no issued row or pointer is mutated to manufacture API history.
        var historyUnknown = DeliverableClientExportEndpoints.Project(observed, true); Assert.Null(historyUnknown.Revision); Assert.Null(historyUnknown.PurposeOfIssue); Assert.Null(historyUnknown.IssueDate); Assert.Null(historyUnknown.ReturnCode);
        var legacyDate = DeliverableClientExportEndpoints.Project(observed with { IssuedDate = new(2026, 10, 30) }, false); Assert.Null(legacyDate.Revision); Assert.Null(legacyDate.IssueDate);
        Assert.Null(DeliverableClientExportEndpoints.Project(observed with { Status = "Issued" }, false).Revision);
        Assert.Null(DeliverableClientExportEndpoints.Project(observed with { Status = "Accepted" }, false).Revision);
        Assert.Null(DeliverableClientExportEndpoints.Project(observed with { IssuedTo = "Legacy recorded recipient" }, false).Revision);
        Assert.Equal(new[] { "number", "clientDrawingNumber", "title", "revision", "purposeOfIssue", "issueDate", "returnCode", "status" }, JsonSerializer.SerializeToNode(planned, JsonOpts.Web)!.AsObject().Select(pair => pair.Key));
    }
}
