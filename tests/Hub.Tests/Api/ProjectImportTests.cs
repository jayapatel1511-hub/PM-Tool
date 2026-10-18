using Hub.Domain;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ProjectImportTests(HubFactory f)
{
    async Task Flag(bool on) => await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.project_import", new { value = on }).Result.Json();

    [Fact] // AC-PRO-18, AC-PRO-19, AC-PRO-23
    public async Task Preview_classifies_create_exists_and_error_and_non_admin_is_refused()
    {
        var d = new TestData(f);
        var existing = await d.Project(activate: false);
        var client = await d.Client(); var office = await d.Office();
        var clientName = await f.DbAsync(db => db.Clients.Where(x => x.Id == client).Select(x => x.Name).SingleAsync());
        var officeName = await f.DbAsync(db => db.Offices.Where(x => x.Id == office).Select(x => x.Name).SingleAsync());
        var csv = $"project number,name,client,office,project manager email\n{existing.ProjectNumber},Existing,{clientName},{officeName},{TestData.Pm}\nP-IMPORT-1,New,{clientName},{officeName},{TestData.Pm}\nP-IMPORT-2,Bad,Unknown,{officeName},{TestData.Pm}";
        await Flag(true);
        try
        {
            var preview = await (await f.As(TestData.Admin).Post("/api/v1/projects/import-preview", new { csv })).Json();
            Assert.Equal(1, preview["creates"]!.GetValue<int>());
            Assert.Equal(1, preview["exists"]!.GetValue<int>());
            Assert.Equal(1, preview["errors"]!.GetValue<int>());
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post("/api/v1/projects/import-preview", new { csv })).StatusCode);
        }
        finally { await Flag(false); }
    }

    [Fact] // AC-PRO-18, AC-PRO-19, AC-PRO-23
    public async Task Errors_refuse_commit_without_writes_and_corrected_commit_creates_setup_project_and_audit()
    {
        var d = new TestData(f); var client = await d.Client(); var office = await d.Office();
        var clientName = await f.DbAsync(db => db.Clients.Where(x => x.Id == client).Select(x => x.Name).SingleAsync());
        var officeName = await f.DbAsync(db => db.Offices.Where(x => x.Id == office).Select(x => x.Name).SingleAsync());
        var number = TestData.Number();
        var invalid = $"project number,name,client,office,project manager email\n{number},New,Unknown,{officeName},{TestData.Pm}";
        var valid = $"project number,name,client,office,project manager email\n{number},New,{clientName},{officeName},{TestData.Pm}";
        await Flag(true);
        try
        {
            var before = await f.DbAsync(db => db.Projects.CountAsync());
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Post("/api/v1/projects/import", new { csv = invalid })).StatusCode);
            Assert.Equal(before, await f.DbAsync(db => db.Projects.CountAsync()));

            var applied = await (await f.As(TestData.Admin).Post("/api/v1/projects/import", new { csv = valid })).Json();
            Assert.Equal(1, applied["creates"]!.GetValue<int>());
            var project = await f.DbAsync(db => db.Projects.SingleAsync(p => p.ProjectNumber == number));
            Assert.Equal(ProjectStatus.Setup, project.Status);
            Assert.Equal(await f.DbAsync(db => db.Users.Where(u => u.Email == TestData.Pm).Select(u => u.Id).SingleAsync()), project.ProjectManagerId);
            Assert.True(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == project.ProjectManagerId && m.Roles.Contains(ProjectRole.PM))));
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemType == "ProjectImport" && a.Action == "Imported" && a.Source == "Import")));
            (await f.As(TestData.Admin).GetAsync("/api/v1/admin/activity")).EnsureSuccessStatusCode();
        }
        finally { await Flag(false); }
    }

    [Fact] // AC-PRO-18, AC-PRO-19, AC-PRO-23
    public async Task Corrected_commit_replays_idempotency_key_and_does_not_duplicate()
    {
        var d = new TestData(f); var client = await d.Client(); var office = await d.Office();
        var clientName = await f.DbAsync(db => db.Clients.Where(x => x.Id == client).Select(x => x.Name).SingleAsync());
        var officeName = await f.DbAsync(db => db.Offices.Where(x => x.Id == office).Select(x => x.Name).SingleAsync());
        var number = TestData.Number();
        var csv = $"project number,name,client,office,project manager email\n{number},Replay,{clientName},{officeName},{TestData.Pm}";
        await Flag(true);
        try
        {
            var key = Guid.NewGuid().ToString("N");
            async Task<HttpResponseMessage> Send()
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/projects/import") { Content = JsonContent.Create(new { csv }) };
                req.Headers.Add("Idempotency-Key", key);
                return await f.As(TestData.Admin).SendAsync(req);
            }
            var first = await Send(); var replay = await Send();
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
            Assert.Equal((await first.Content.ReadAsStringAsync()), await replay.Content.ReadAsStringAsync());
            Assert.Equal(1, await f.DbAsync(db => db.Projects.CountAsync(p => p.ProjectNumber == number)));
        }
        finally { await Flag(false); }
    }

    [Fact] // AC-PRO-18, AC-PRO-19, AC-PRO-23
    public async Task Oversized_and_duplicate_rows_are_rejected_or_classified_as_errors()
    {
        var d = new TestData(f); var client = await d.Client(); var office = await d.Office();
        var clientName = await f.DbAsync(db => db.Clients.Where(x => x.Id == client).Select(x => x.Name).SingleAsync());
        var officeName = await f.DbAsync(db => db.Offices.Where(x => x.Id == office).Select(x => x.Name).SingleAsync());
        await Flag(true);
        try
        {
            var repeated = string.Join("\n", Enumerable.Repeat($"{TestData.Number()},Name,{clientName},{officeName},{TestData.Pm}", 1001));
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Post("/api/v1/projects/import-preview", new { csv = "project number,name,client,office,project manager email\n" + repeated })).StatusCode);
            var duplicateNumber = TestData.Number();
            var duplicate = $"project number,name,client,office,project manager email\n{duplicateNumber},One,{clientName},{officeName},{TestData.Pm}\n{duplicateNumber},Two,{clientName},{officeName},{TestData.Pm}";
            var result = await (await f.As(TestData.Admin).Post("/api/v1/projects/import-preview", new { csv = duplicate })).Json();
            Assert.Equal(1, result["errors"]!.GetValue<int>());
            var badPm = $"project number,name,client,office,project manager email\n{TestData.Number()},Bad PM,{clientName},{officeName},missing-pm@hub.test";
            var badPmResult = await (await f.As(TestData.Admin).Post("/api/v1/projects/import-preview", new { csv = badPm })).Json();
            Assert.Equal(1, badPmResult["errors"]!.GetValue<int>());
            var huge = new string('x', 256 * 1024 + 1);
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Post("/api/v1/projects/import-preview", new { csv = huge })).StatusCode);
        }
        finally { await Flag(false); }
    }
}
