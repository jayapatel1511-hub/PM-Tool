using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 051 Part C, slice S09 (§55.4 FR-ADR-06, FR-ADR-07, AC-ADR-05): a recorded Admin review exports as CSV and XLSX with a
/// header block and one row per check, counts and the reviewer's notes only. Each test gets its own database.
public sealed class ExportTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public ExportTests() => data = new TestData(f);
    public void Dispose() => f.Dispose();

    HttpClient Admin => f.As(TestData.Admin);

    async Task Flag(string name, bool on) => (await Admin.Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();

    /// alex is inactive on a live team, so the first check has a person behind its count and the export must not name him.
    async Task<Guid> Recorded()
    {
        await data.Project();
        var alex = f.Db(db => db.Users.Where(u => u.Email == TestData.Alex).Select(u => new { u.Id, u.RowVersion }).First());
        (await Admin.Patch($"/api/v1/admin/users/{alex.Id}", new { isActive = false }, alex.RowVersion)).EnsureSuccessStatusCode();
        var review = await Admin.Post("/api/v1/admin/review", new
        {
            note = "Quarterly access review, then \"quoted\", with commas",
            checks = new[] { new { key = "inactiveOnTeams", note = "Checked with HR" } },
        }).Result.Json(201);
        return review.G("id");
    }

    async Task<byte[]> Bytes(Guid id, string format, string who = TestData.Admin) =>
        await (await f.As(who).GetAsync($"/api/v1/admin/review/{id}/export?format={format}")).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync();

    static string Part(byte[] xlsx, string name)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx));
        using var r = new StreamReader(zip.GetEntry(name)!.Open());
        return r.ReadToEnd();
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task The_csv_holds_the_header_block_and_one_row_per_check_and_names_only_the_reviewer() // AC-ADR-05, FR-ADR-06
    {
        await Flag("admin_review", true);
        await Flag("org_structure", true); // the ninth check appears
        var id = await Recorded();
        var bytes = await Bytes(id, "csv");
        Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes).TrimStart('﻿').Split("\r\n", StringSplitOptions.None).SelectMany(l => l.Split('\n')).ToArray();

        Assert.StartsWith("Date,", lines[0]);
        Assert.Equal("Reviewer,Jordan Lee", lines[1]);
        Assert.Equal("Note,\"Quarterly access review, then \"\"quoted\"\", with commas\"", lines[2]);
        Assert.Equal("", lines[3]);
        Assert.Equal("Check,Rule,Count,Note", lines[4]);
        var rows = lines.Skip(5).Where(l => l != "").ToArray();
        Assert.Equal(9, rows.Length);
        Assert.StartsWith("Inactive users still on teams,", rows[0]);
        Assert.EndsWith(",1,Checked with HR", rows[0]); // alex was on the live team when it was recorded
        Assert.StartsWith("Users without a home unit,", rows[2]);
        Assert.Contains("Reference data deactivated while in use", rows[8]);

        // Counts and the reviewer's notes only: nobody else's name, nor an email, leaves with it.
        var csv = Encoding.UTF8.GetString(bytes);
        foreach (var name in f.Db(db => db.Users.Select(u => u.DisplayName).ToList()).Where(n => n != "Jordan Lee")) Assert.DoesNotContain(name, csv);
        Assert.DoesNotContain("@hub.test", csv);
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task The_xlsx_carries_the_same_rows_and_the_header_block_and_each_export_is_logged() // FR-ADR-06
    {
        await Flag("admin_review", true);
        var id = await Recorded();
        var before = await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.Action == "Exported"));
        var response = await Admin.GetAsync($"/api/v1/admin/review/{id}/export?format=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        var xlsx = await response.Content.ReadAsByteArrayAsync();

        var sheet = Part(xlsx, "xl/worksheets/sheet1.xml");
        Assert.Equal(8, System.Text.RegularExpressions.Regex.Matches(sheet, "<row r=\"([2-9])\"").Count); // eight checks with org_structure off
        Assert.Contains("Inactive users still on teams", sheet);
        Assert.Contains("Checked with HR", sheet);
        Assert.DoesNotContain("Users without a home unit", sheet);
        var parameters = Part(xlsx, "xl/worksheets/sheet2.xml");
        Assert.Contains("Jordan Lee", parameters);
        Assert.Contains("Quarterly access review", parameters);
        Assert.Equal(before + 1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.Action == "Exported")));
        Assert.DoesNotContain("Quarterly access review", await f.DbAsync(db => db.ActivityLog.Where(a => a.Action == "Exported").Select(a => a.ItemName).FirstAsync())); // who and what, never the note
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task Only_a_System_Administrator_may_export_and_an_unknown_review_is_not_found() // FR-ADR-07
    {
        await Flag("admin_review", true);
        var id = await Recorded();
        foreach (var who in new[] { TestData.Sam, TestData.Pm, TestData.Lena, TestData.Rita })
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).GetAsync($"/api/v1/admin/review/{id}/export?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"/api/v1/admin/review/{Guid.NewGuid()}/export?format=csv")).StatusCode);
    }

    [Fact]
    [Trait("flag-off", "admin_review")]
    public async Task With_the_flag_off_there_is_no_export() // FR-ADR-08, AC-ADR-06
    {
        await Flag("admin_review", true);
        var id = await Recorded();
        await Flag("admin_review", false);
        Assert.Equal(HttpStatusCode.NotFound, (await Admin.GetAsync($"/api/v1/admin/review/{id}/export?format=csv")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).GetAsync($"/api/v1/admin/review/{id}/export?format=csv")).StatusCode);
    }
}
