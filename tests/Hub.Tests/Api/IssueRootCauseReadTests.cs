using System.Net;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.VisualBasic.FileIO;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class IssueRootCauseReadTests
{
    [Fact] // AC-SCR-71: counts use all resolved unique issues, not the 50-row browser page, and drilldown/export use exact IDs.
    public async Task Project_wide_counts_reconcile_unknown_retired_and_more_than_fifty_issues_with_the_exact_list()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var other = await t.Data.Project(); var cause = await t.Cause();
        var first = await t.Issue(p.Id, "Known coordination issue"); await t.Move(first, cause);
        await t.F.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            INSERT INTO hub.issue
            SELECT clone.* FROM hub.issue i CROSS JOIN generate_series(1, 55) s
            CROSS JOIN LATERAL jsonb_populate_record(NULL::hub.issue, to_jsonb(i) || jsonb_build_object(
                'id', gen_random_uuid(), 'seq', s+100, 'key', i.key || '-copy-' || s::text, 'title', 'Synthetic resolved copy ' || s::text)) AS clone
            WHERE i.id={0}
            """, first));
        var legacy = await t.Issue(p.Id, "Unknown legacy cause"); await t.Flag(false); await t.Move(legacy); await t.Flag(true);
        await t.Issue(p.Id, "Open control"); var cancelled = await t.Issue(p.Id, "Cancelled control"); await t.Move(cancelled, to: "Cancelled", reason: "Not required after scope correction");
        var hidden = await t.Issue(other.Id, "PRIVATE OTHER PROJECT"); await t.Move(hidden, cause);
        await t.EditCause(cause, new { name = "Renamed coordination cause", isActive = false });
        var before = t.State(p.Id); var summary = await t.Summary(p.Id, TestData.Rita);
        Assert.Equal(57, summary.I("resolvedTotal")); Assert.Equal(1, summary.I("notRecordedCount"));
        var count = Assert.Single(summary["causes"]!.AsArray())!; Assert.Equal(56, count.I("count")); Assert.Equal(cause, count.G("categoryId"));
        Assert.Equal("Renamed coordination cause", count.S("name")); Assert.False(count["isActive"]!.GetValue<bool>());
        var known = await (await t.F.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/issues?status=Resolved&rootCause={cause}")).Json();
        var unknown = await (await t.F.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/issues?status=Resolved&rootCause=not-recorded")).Json();
        Assert.Equal(56, known.AsArray().Count); Assert.Equal(legacy, Assert.Single(unknown.AsArray())!.G("id"));
        Assert.All(known.AsArray(), row => Assert.Equal("Coordination gap", row!["rootCause"]!.S("recordedName")));
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var response = await t.F.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/issues/export?format={format}&status=Resolved&rootCause={cause}"); response.EnsureSuccessStatusCode();
            var exported = ExportRows(await response.Content.ReadAsByteArrayAsync(), format);
            Assert.Equal(57, exported.Length); Assert.Equal(18, exported[0].Length); // Existing columns, including header.
            Assert.Equal(known.AsArray().Select(row => row!.S("key")).Order(), exported.Skip(1).Select(row => row[0]).Order());
            var text = string.Join("\n", exported.Select(row => string.Join("|", row)));
            Assert.Contains("Known coordination issue", text); Assert.DoesNotContain("Unknown legacy cause", text); Assert.DoesNotContain("PRIVATE", text);
            Assert.DoesNotContain("Root cause", text); Assert.DoesNotContain("Renamed coordination cause", text); // No added export column.
        }
        Assert.Equal(before, t.State(p.Id, excludeExports: true));
    }

    static string[][] ExportRows(byte[] bytes, string format)
    {
        if (format == "csv")
        {
            using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'))) {
                TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
            };
            parser.SetDelimiters(","); var rows = new List<string[]>();
            while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
            return rows.ToArray();
        }
        using var zip = new ZipArchive(new MemoryStream(bytes)); using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Load(sheet).Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell =>
            (string?)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)) : cell.Element(ns + "v")?.Value ?? "").ToArray()).ToArray();
    }

    [Theory] // AC-SCR-84: raw new filter is ignored off, parsed strictly on; valid unknown ID returns zero without labels.
    [InlineData("not-a-guid")][InlineData("00000000-0000-0000-0000-000000000000")][InlineData("not-recorded,other")]
    public async Task Filter_is_flag_aware_and_summary_does_not_mutate_any_state(string filter)
    {
        using var t = new IssueRootCauseFixture(); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); await t.Move(issue);
        var path = $"/api/v1/projects/{p.Id}/issues?rootCause={filter}";
        Assert.Single((await (await t.F.As(TestData.Pm).GetAsync(path)).Json()).AsArray()); await t.Flag(true); var before = t.State(p.Id);
        await (await t.F.As(TestData.Pm).GetAsync(path)).Json(400);
        Assert.Empty((await (await t.F.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues?rootCause={Guid.NewGuid()}")).Json()).AsArray());
        var summary = await t.Summary(p.Id); Assert.Equal(1, summary.I("notRecordedCount")); Assert.Equal(before, t.State(p.Id));
    }

    [Fact] // AC-SCR-71 / AC-SCR-84: missing/private project is 404, zero history is known zero, read-only is a read right only.
    public async Task Summary_is_project_authorized_and_empty_is_not_inferred_unknown()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project();
        var empty = await t.Summary(p.Id, TestData.Rita); Assert.Equal(0, empty.I("resolvedTotal")); Assert.Equal(0, empty.I("notRecordedCount")); Assert.Empty(empty["causes"]!.AsArray());
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var before = t.State(p.Id);
        foreach (var id in new[] { p.Id, Guid.NewGuid() }) Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync($"/api/v1/projects/{id}/issues/root-causes")).StatusCode);
        Assert.Equal(before, t.State(p.Id));
    }
}
