using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Tests.Api;

public sealed partial class ExternalReviewTests
{
    static readonly string[] ExternalHeaders = ["Round", "Comment", "Originator", "Discipline", "Severity", "Status", "Response", "Outcome"];
    static Dictionary<string, string> ExportParts(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return zip.Entries.ToDictionary(entry => entry.FullName, entry => { using var reader = new StreamReader(entry.Open()); return reader.ReadToEnd(); });
    }
    static string[][] ExportRows(byte[] bytes, string format)
    {
        if (format == "csv")
        {
            using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'))) {
                TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false,
            };
            parser.SetDelimiters(","); var rows = new List<string[]>();
            while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
            var first = rows.FindIndex(row => row.SequenceEqual(ExternalHeaders)); Assert.True(first >= 0);
            return rows.Skip(first).ToArray();
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Parse(ExportParts(bytes)["xl/worksheets/sheet1.xml"]).Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell =>
            (string?)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)) : cell.Element(ns + "v")?.Value ?? "").ToArray()).ToArray();
    }
    string ExportPath(Scenario s, string format) => PackagePath(s) + $"/response-sheet?format={format}";
    int ExportLogs(Scenario s) => F.Db(db => db.ActivityLog.Count(row => row.ProjectId == s.Project.Id && row.Action == "Exported"));

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task External_response_sheet_keeps_all_rounds_exact_eight_columns_snapshot_party_and_safe_bytes(string format)
    {
        const string party = "=PARTY(\"é, quoted\")";
        const string comment = "=COMMENT(\"quoted, value\"); café\nsecond line";
        const string response = "=RESPONSE(\"quoted; value\"); 桥\nsecond line";
        var s = await New(partyName: party); var body = Body(s, comment);
        await Record(s, body); var blocking = Findings(s).Single(row => row.Severity == "Blocking");
        await Act(s, blocking.Id, "assignVerifier", TestData.Pm, owner: Data.User(TestData.Jill));
        await Act(s, blocking.Id, FindingStatus.Responded, TestData.Alex, outcome: "Not incorporated", reason: response);
        await Act(s, blocking.Id, FindingStatus.VerifiedClosed, TestData.Jill, evidence: VerifierEvidence);
        await Round(s, s.Issue); await Start(s);
        (await F.As(TestData.Pm).Patch($"/api/v1/external-parties/{s.Party}", new { name = "Synthetic renamed party", organisation = "Renamed organisation" }, V<ExternalParty>(s.Party))).EnsureSuccessStatusCode();
        await Record(s, Body(s) with { Comments = [new("Actual second-round external comment", s.Civil, Data.User(TestData.Alex), "Blocking")] });
        var before = State(s); var logs = ExportLogs(s); var users = F.Db(db => db.Users.Count());
        // These unrelated register controls cannot reduce a fixed complete-history response sheet.
        var download = await F.As(TestData.Rita).GetAsync(ExportPath(s, format) + "&page=1&pageSize=1&severity=Blocking&allRounds=false&originatorKind=User");
        download.EnsureSuccessStatusCode(); var bytes = await download.Content.ReadAsByteArrayAsync(); var rows = ExportRows(bytes, format);
        Assert.Equal(ExternalHeaders, rows[0]); Assert.Equal(5, rows.Length); Assert.All(rows, row => Assert.Equal(8, row.Length));
        Assert.Equal(new[] { "1", "1", "2", "2" }, rows.Skip(1).Select(row => row[0]).Order());
        var safeParty = format == "csv" ? "'" + party : party;
        var safeComment = format == "csv" ? "'" + comment : comment; var safeResponse = format == "csv" ? "'" + response : response;
        var closed = rows.Skip(1).Single(row => row[1] == safeComment);
        Assert.Equal(safeParty, closed[2]); Assert.Equal(FindingStatus.VerifiedClosed, closed[5]);
        Assert.Equal(safeResponse, closed[6]); Assert.Equal("Not incorporated", closed[7]);
        Assert.Equal(3, rows.Skip(1).Count(row => row[2] == safeParty));
        Assert.Equal(1, rows.Skip(1).Count(row => row[2] == "Synthetic renamed party"));
        Assert.Equal(2, rows.Skip(1).Count(row => row[1] == body.Comments[1].Text)); // Original advisory plus its distinct carry.
        var allText = format == "csv" ? Encoding.UTF8.GetString(bytes) : string.Join("\n", ExportParts(bytes).Values);
        foreach (var secret in new[] { PartyEmail, PartyNotes, ResolverEvidence, VerifierEvidence, "issuedAuthorIds" }) Assert.DoesNotContain(secret, allText);
        if (format == "xlsx") {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            Assert.Empty(XDocument.Parse(ExportParts(bytes)["xl/worksheets/sheet1.xml"]).Descendants(ns + "f"));
        }
        Assert.Equal(logs + 1, ExportLogs(s)); Assert.Equal(users, F.Db(db => db.Users.Count())); Assert.Equal(before, State(s));
    }

    [Fact]
    public async Task External_export_parent_scope_and_dark_guards_refuse_before_any_export_log_or_read_side_effect()
    {
        var s = await New(); await Record(s); var other = await Data.Project(); var before = State(s); var logs = ExportLogs(s);
        var foreignPath = $"/api/v1/projects/{other.Id}/reviews/{s.Package}/response-sheet?format=csv";
        Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(foreignPath)).StatusCode);
        Assert.Equal(logs, ExportLogs(s)); Assert.Equal(before, State(s));
        await Flag(false); before = State(s);
        foreach (var format in new[] { "csv", "xlsx" })
            Assert.Equal(HttpStatusCode.NotFound, (await F.As(TestData.Pm).GetAsync(ExportPath(s, format))).StatusCode);
        Assert.Equal(logs, ExportLogs(s)); Assert.Equal(before, State(s));
    }
}
