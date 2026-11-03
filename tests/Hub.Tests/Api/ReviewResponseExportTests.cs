using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml.Linq;
using Hub.Api;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Tests.Api;

/// AC-SCR-61 / AC-SCR-84: actual per-package CSV/XLSX bytes, complete all-round scope and dark-route parity.
public sealed class ReviewResponseExportTests
{
    static readonly string[] Headers = ["Round", "Comment", "Originator", "Discipline", "Severity", "Status", "Response", "Outcome"];
    static string Path(ReviewResponseFixture t, ReviewResponseFixture.Setup s, string format) => t.Root(s) + $"/reviews/{s.Package}/response-sheet?format={format}";
    static async Task<byte[]> Download(ReviewResponseFixture t, ReviewResponseFixture.Setup s, string format, string who = TestData.Rita, string extra = "")
        => await (await t.F.As(who).GetAsync(Path(t, s, format) + extra)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync();
    static Dictionary<string, string> Parts(byte[] bytes)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes));
        return zip.Entries.ToDictionary(entry => entry.FullName, entry => { using var reader = new StreamReader(entry.Open()); return reader.ReadToEnd(); });
    }
    static string[][] Rows(byte[] bytes, string format)
    {
        if (format == "csv")
        {
            using var parser = new TextFieldParser(new StringReader(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'))) {
                TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = false
            };
            parser.SetDelimiters(","); var rows = new List<string[]>();
            while (!parser.EndOfData) rows.Add(parser.ReadFields()!);
            var header = rows.FindIndex(row => row.SequenceEqual(Headers));
            Assert.True(header >= 0, "response sheet must contain its eight fixed columns after the metadata block");
            return rows.Skip(header).ToArray();
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        return XDocument.Parse(Parts(bytes)["xl/worksheets/sheet1.xml"]).Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell =>
            (string?)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(text => text.Value)) : cell.Element(ns + "v")?.Value ?? "").ToArray()).ToArray();
    }
    static string AllText(byte[] bytes, string format) => format == "csv" ? Encoding.UTF8.GetString(bytes) : string.Join("\n", Parts(bytes).Values);
    static int Logs(ReviewResponseFixture t, Guid project) => t.F.Db(db => db.ActivityLog.Count(row => row.ProjectId == project && row.Action == "Exported"));

    [Theory] // AC-SCR-61: each stored comment, not just open findings or the current register page.
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Eight_fixed_columns_preserve_complete_history_safe_text_and_actual_people_without_private_metadata(string format)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New();
        const string comment = "=COMMENT(\"quoted, value\"); café\nsecond line";
        const string response = "=RESPONSE(\"quoted; value\"); 桥\nsecond line";
        var closed = await t.Finding(s, comment);
        await t.Action(s, closed, FindingStatus.Responded, TestData.Alex, "Not incorporated", response);
        await t.Action(s, closed, FindingStatus.VerifiedClosed, TestData.Omar);
        var legacy = await t.Finding(s, "Legacy disposition not recorded", "Advisory", "Civil");
        await t.Flag(false); await t.Action(s, legacy, FindingStatus.Responded, TestData.Alex, reason: "A real legacy response without an inferred outcome"); await t.Flag(true);
        var withdrawn = await t.Finding(s, "The withdrawn comment is still part of history", "Advisory");
        await t.Action(s, withdrawn, FindingStatus.Withdrawn, TestData.Omar);
        var carry = await t.Finding(s, "The repeated carried comment is a distinct stored record");
        await t.Action(s, carry, FindingStatus.Responded, TestData.Alex, "Clarified", "Retain only the old round response");
        await t.NewRound(s);
        var other = await t.New(s.Project, "PRIVATE OTHER PACKAGE"); await t.Finding(other, "PRIVATE OTHER PACKAGE COMMENT");
        var omarName = t.F.Db(db => db.Users.Single(row => row.Id == t.Data.User(TestData.Omar)).DisplayName);
        var priyaName = t.F.Db(db => db.Users.Single(row => row.Id == t.Data.User(TestData.Pm)).DisplayName);
        await t.F.DbAsync(async db => {
            var person = await db.Users.SingleAsync(row => row.Id == t.Data.User(TestData.Omar)); person.IsActive = false;
            person.JobTitle = "PRIVATE PERSON FACT SENTINEL";
            return await db.SaveChangesAsync();
        });
        var before = t.State(s.Project.Id); var logs = Logs(t, s.Project.Id);
        // Unrelated register query parameters must not reduce this fixed complete-history export.
        var bytes = await Download(t, s, format, extra: "&page=1&pageSize=1&severity=Blocking&allRounds=false"); var rows = Rows(bytes, format);
        Assert.Equal(Headers, rows[0]); Assert.Equal(7, rows.Length); Assert.All(rows, row => Assert.Equal(8, row.Length));
        var safeComment = format == "csv" ? "'" + comment : comment; var safeResponse = format == "csv" ? "'" + response : response;
        Assert.Equal(new[] { "1", safeComment, omarName, "Electrical", "Blocking", FindingStatus.VerifiedClosed, safeResponse, "Not incorporated" }, rows[1]);
        Assert.Equal(new[] { "1", "Legacy disposition not recorded", priyaName, "Civil", "Advisory", FindingStatus.Responded, "A real legacy response without an inferred outcome", "" }, rows[2]);
        Assert.Equal(new[] { "1", "The withdrawn comment is still part of history", omarName, "Electrical", "Advisory", FindingStatus.Withdrawn, "", "" }, rows[3]);
        Assert.Equal(new[] { "1", "The repeated carried comment is a distinct stored record", omarName, "Electrical", "Blocking", FindingStatus.Responded, "Retain only the old round response", "Clarified" }, rows[4]);
        Assert.Equal(new[] { "2", "2" }, rows.Skip(5).Select(row => row[0]));
        Assert.Equal(new[] { "Legacy disposition not recorded", "The repeated carried comment is a distinct stored record" }.Order(), rows.Skip(5).Select(row => row[1]).Order());
        Assert.All(rows.Skip(5), row => { Assert.Equal(FindingStatus.Open, row[5]); Assert.Equal("", row[6]); Assert.Equal("", row[7]); });
        var text = AllText(bytes, format);
        foreach (var forbidden in new[] { "PRIVATE", "@hub.test", t.Data.User(TestData.Alex).ToString(), s.Package.ToString(), s.Source.ToString(), "pageSize", "allRounds", "responseOutcome" }) Assert.DoesNotContain(forbidden, text);
        Assert.Contains("All rounds", text); Assert.Contains("latest retained response", text); Assert.Contains("not recorded", text);
        Assert.Contains("Generated", text); Assert.Equal(before, t.State(s.Project.Id)); Assert.Equal(logs + 1, Logs(t, s.Project.Id));
        var log = t.F.Db(db => db.ActivityLog.Single(row => row.ProjectId == s.Project.Id && row.Action == "Exported"));
        Assert.Equal(new[] { "access" }, log.Categories); Assert.Equal("[]", log.Changes); Assert.DoesNotContain("PRIVATE", log.ItemName!);
        if (format == "csv") Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        else
        {
            var parts = Parts(bytes); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheet = XDocument.Parse(parts["xl/worksheets/sheet1.xml"]);
            Assert.Equal("inlineStr", (string?)sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "B2").Attribute("t"));
            Assert.Equal("inlineStr", (string?)sheet.Descendants(ns + "c").Single(cell => (string?)cell.Attribute("r") == "G2").Attribute("t"));
            Assert.DoesNotContain("<f", parts["xl/worksheets/sheet1.xml"]);
            Assert.DoesNotContain(parts.Keys, name => name.Contains("externalLink", StringComparison.Ordinal) || name.Contains("comments", StringComparison.Ordinal));
        }
    }

    [Theory] // AC-SCR-61: missing legacy values remain explicit; inactive source work is retained as history.
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Unknown_originator_and_discipline_are_not_substituted_and_legacy_outcome_stays_blank(string format)
    {
        using var t = new ReviewResponseFixture(); var s = await t.New(); var answered = await t.Finding(s, "A legacy response with unknown labels");
        await t.Action(s, answered, FindingStatus.Responded, TestData.Alex, reason: "A response retained from the original workflow");
        await t.Finding(s, "The unanswered comment also remains in the sheet");
        var omar = t.Data.User(TestData.Omar);
        await t.F.DbAsync(async db => {
            (await db.Users.SingleAsync(row => row.Id == omar)).DisplayName = ""; // Genuine missing-name fixture; no invented replacement account.
            var discipline = await db.ProjectDisciplines.SingleAsync(row => row.Id == s.Electrical);
            (await db.Disciplines.SingleAsync(row => row.Id == discipline.DisciplineId)).Name = "";
            (await db.Deliverables.SingleAsync(row => row.Id == s.Deliverable)).DeletedAt = t.F.Clock.Now;
            return await db.SaveChangesAsync();
        });
        await t.Flag(true); var before = t.State(s.Project.Id); var rows = Rows(await Download(t, s, format), format);
        Assert.Equal(3, rows.Length); Assert.Equal(Headers, rows[0]);
        Assert.Equal(Text.Get("reviewResponse.unknownOriginator"), rows[1][2]); Assert.Equal(Text.Get("reviewResponse.unknownDiscipline"), rows[1][3]);
        Assert.Equal("A response retained from the original workflow", rows[1][6]); Assert.Equal("", rows[1][7]);
        Assert.Equal("", rows[2][6]); Assert.Equal("", rows[2][7]); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-84: no export access event while dark; established package-summary exports remain unchanged.
    public async Task Authorization_readonly_filename_dark_route_and_existing_summary_export_keep_their_contracts()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted");
        var summary = t.Root(s) + "/reviews/export?format=csv";
        var original = await (await t.F.As(TestData.Pm).GetAsync(summary)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync();
        foreach (var format in new[] { "csv", "xlsx" })
        {
            using var response = await t.F.As(TestData.Rita).GetAsync(Path(t, s, format)); response.EnsureSuccessStatusCode();
            Assert.Equal(format == "csv" ? "text/csv" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
            var filename = (response.Content.Headers.ContentDisposition!.FileNameStar ?? response.Content.Headers.ContentDisposition.FileName)!.Trim('"');
            Assert.Matches("^[A-Za-z0-9_-]+[.]" + format + "$", filename);
            Assert.Equal(Rows(await Download(t, s, format, TestData.Pm), format).Select(row => string.Join("|", row)),
                Rows(await response.Content.ReadAsByteArrayAsync(), format).Select(row => string.Join("|", row)));
        }
        var other = await t.New(); var logs = Logs(t, s.Project.Id); var before = t.State(s.Project.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await t.F.As(TestData.Rita).GetAsync(Path(t, s, "pdf"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(t.Root(s) + $"/reviews/{Guid.NewGuid()}/response-sheet?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(t.Root(other) + $"/reviews/{s.Package}/response-sheet?format=csv")).StatusCode);
        Assert.Equal(before, t.State(s.Project.Id)); Assert.Equal(logs, Logs(t, s.Project.Id));
        await t.Flag(false); logs = Logs(t, s.Project.Id);
        foreach (var format in new[] { "csv", "xlsx" }) Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(Path(t, s, format))).StatusCode);
        Assert.Equal(logs, Logs(t, s.Project.Id));
        Assert.Equal(original, await (await t.F.As(TestData.Pm).GetAsync(summary)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
        await t.Flag(true);
        Assert.Equal(original, await (await t.F.As(TestData.Pm).GetAsync(summary)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
        await (await t.F.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == s.Project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        logs = Logs(t, s.Project.Id);
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(Path(t, s, "csv"))).StatusCode); Assert.Equal(logs, Logs(t, s.Project.Id));
    }

    [Theory] // AC-SCR-61: a complete sheet refuses corrupt declared relationships instead of silently skipping rows.
    [InlineData("round")]
    [InlineData("source")]
    [InlineData("discipline")]
    [InlineData("deliverable")]
    [InlineData("findingProject")]
    [InlineData("packageDiscipline")]
    public async Task Foreign_relationships_refuse_the_entire_download_without_leaking_or_logging(string malformed)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        var other = await t.New(title: "PRIVATE FOREIGN PACKAGE");
        await t.F.DbAsync(async db => {
            var row = await db.ReviewFindings.SingleAsync(row => row.Id == finding);
            if (malformed == "round") row.RoundId = other.Round;
            if (malformed == "source") row.SourceRevisionId = other.Source;
            if (malformed == "discipline") row.ProjectDisciplineId = other.Civil;
            if (malformed == "findingProject") row.ProjectId = other.Project.Id;
            if (malformed == "packageDiscipline") (await db.ReviewPackages.SingleAsync(package => package.Id == s.Package)).ProjectDisciplineId = other.Civil;
            if (malformed == "deliverable")
                await db.SourceRevisions.Where(source => source.Id == s.Source).ExecuteUpdateAsync(setters => setters.SetProperty(source => source.DeliverableId, (Guid?)other.Deliverable)); // Test-only corruption bypasses immutable source guard.
            return await db.SaveChangesAsync();
        });
        var logs = Logs(t, s.Project.Id); var before = t.State(s.Project.Id);
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var problem = await (await t.F.As(TestData.Rita).GetAsync(Path(t, s, format))).Json(409);
            Assert.Equal("review_response_integrity", problem.S("code")); Assert.DoesNotContain("PRIVATE", problem.ToJsonString());
            Assert.DoesNotContain(other.Project.Id.ToString(), problem.ToJsonString()); Assert.DoesNotContain(other.Source.ToString(), problem.ToJsonString());
        }
        Assert.Equal(before, t.State(s.Project.Id)); Assert.Equal(logs, Logs(t, s.Project.Id));
    }

    [Theory] // AC-SCR-84: a missing project or soft-deleted package returns 404 without an access event or other writes.
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_project_or_deleted_package_is_hidden_without_writes(bool missingProject)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); await t.Finding(s);
        if (!missingProject)
            await t.F.DbAsync(async db => {
                (await db.ReviewPackages.SingleAsync(row => row.Id == s.Package)).DeletedAt = t.F.Clock.Now;
                return await db.SaveChangesAsync();
            });
        var absentProject = Guid.NewGuid(); var logs = Logs(t, s.Project.Id); var before = t.State(s.Project.Id);
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var path = missingProject ? $"/api/v1/projects/{absentProject}/reviews/{s.Package}/response-sheet?format={format}" : Path(t, s, format);
            Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Rita).GetAsync(path)).StatusCode);
        }
        Assert.Equal(logs, Logs(t, s.Project.Id)); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-61: neither format silently truncates a supposedly complete package at Export.MaxRows.
    public async Task One_row_beyond_the_export_limit_refuses_both_formats_before_access_logging()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await t.F.DbAsync(async db => {
            // Private synthetic scale fixture, cloned from a genuine API row. Composite cloning survives additive nullable columns.
            db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
            return await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO hub.review_finding
                SELECT clone.* FROM hub.review_finding f CROSS JOIN generate_series(1, {1}) s
                CROSS JOIN LATERAL jsonb_populate_record(NULL::hub.review_finding, to_jsonb(f) || jsonb_build_object(
                    'id', gen_random_uuid(), 'text', 'Synthetic bulk comment ' || s::text)) AS clone
                WHERE f.id = {0}
                """, finding, Export.MaxRows);
        });
        var count = t.F.Db(db => db.ReviewFindings.Count(row => row.PackageId == s.Package)); Assert.Equal(Export.MaxRows + 1, count);
        var logs = Logs(t, s.Project.Id); var commands = t.F.Db(db => db.CoordinationCommands.Count());
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var problem = await (await t.F.As(TestData.Rita).GetAsync(Path(t, s, format))).Json(422);
            Assert.Equal("export_too_large", problem.S("code"));
        }
        Assert.Equal(count, t.F.Db(db => db.ReviewFindings.Count(row => row.PackageId == s.Package)));
        Assert.Equal(commands, t.F.Db(db => db.CoordinationCommands.Count())); Assert.Equal(logs, Logs(t, s.Project.Id));
        Assert.False(t.F.Db(db => db.FindingEvents.Any()));
    }
}
