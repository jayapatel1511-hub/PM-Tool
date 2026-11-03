using System.Net;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class IdentityColourApiTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public IdentityColourApiTests() => d = new(f);
    public void Dispose() => f.Dispose();
    HttpClient Admin => f.As(TestData.Admin);
    async Task Flag(bool on) => (await Admin.Put("/api/v1/admin/settings/flag.visual_system", new { value = on })).EnsureSuccessStatusCode();

    [Fact]
    [Trait("flag-off", "visual_system")]
    public async Task Flag_off_preserves_legacy_shape_and_does_not_store_colour()
    {
        await Flag(false);
        var p = await d.Project(activate: false);
        var response = await f.As(TestData.Pm).GetAsync($"/api/v1/projects?ids={p.Id}");
        var row = (await response.Json())["items"]![0]!;
        Assert.False(row.AsObject().ContainsKey("identityColour"));
        Assert.Null(f.Db(db => db.Projects.Single(x => x.Id == p.Id).IdentityColour));
    }

    [Fact]
    [Trait("flag-on", "visual_system")]
    public async Task Archived_null_does_not_increment_live_counts_and_backfill_is_idempotent()
    {
        await Flag(false);
        var projects = new List<Project>();
        for (var i = 0; i < 5; i++) projects.Add(await d.Project(activate: false));
        var archivedNull = await d.Project(activate: false);
        var activeNull = await d.Project(activate: false);
        await f.DbAsync(async db =>
        {
            var colours = IdentityColour.Palette;
            var rows = await db.Projects.Where(p => projects.Select(x => x.Id).Contains(p.Id)).OrderBy(p => p.ProjectNumber).ToListAsync();
            for (var i = 0; i < rows.Count; i++) { rows[i].IdentityColour = colours[i]; rows[i].ProjectNumber = $"100-{i}"; }
            var archived = await db.Projects.SingleAsync(p => p.Id == archivedNull.Id);
            archived.Status = ProjectStatus.Archived;
            archived.ProjectNumber = "000-ARCH";
            var active = await db.Projects.SingleAsync(p => p.Id == activeNull.Id);
            active.ProjectNumber = "999-ACT";
            await db.SaveChangesAsync();
            return 0;
        });
        await Flag(true);
        Assert.Equal(IdentityColour.Coral, f.Db(db => db.Projects.Single(x => x.Id == archivedNull.Id).IdentityColour));
        Assert.Equal(IdentityColour.Coral, f.Db(db => db.Projects.Single(x => x.Id == activeNull.Id).IdentityColour));
        var sixth = await d.Project(activate: false);
        Assert.Equal(IdentityColour.Mint, f.Db(db => db.Projects.Single(x => x.Id == sixth.Id).IdentityColour));
        var before = f.Db(db => db.Projects.Where(x => x.IdentityColour != null).Select(x => new { x.Id, x.IdentityColour }).ToDictionary(x => x.Id, x => x.IdentityColour));
        await Flag(true);
        var after = f.Db(db => db.Projects.Where(x => x.IdentityColour != null).Select(x => new { x.Id, x.IdentityColour }).ToDictionary(x => x.Id, x => x.IdentityColour));
        Assert.Equal(before, after);
    }

    [Fact]
    [Trait("flag-on", "visual_system")]
    public async Task Sixth_setup_project_gets_first_palette_colour_when_five_are_balanced()
    {
        await Flag(false);
        for (var i = 0; i < 5; i++) await d.Project(activate: false);
        await Flag(true);
        var sixth = await d.Project(activate: false);
        Assert.Equal(IdentityColour.Coral, f.Db(db => db.Projects.Single(x => x.Id == sixth.Id).IdentityColour));
    }

    [Fact]
    [Trait("flag-on", "visual_system")]
    public async Task Pm_can_change_colour_once_with_audit_and_stale_or_non_pm_is_refused()
    {
        await Flag(true);
        var p = await d.Project(activate: false);
        var created = await f.As(TestData.Pm).Post("/api/v1/projects", new { projectNumber = TestData.Number(), name = "Colour audit", clientId = await d.Client(), officeId = await d.Office() });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var createdId = (await created.Json(201)).G("id");
        Assert.Contains(f.Db(db => db.ActivityLog.Where(x => x.ProjectId == createdId && x.Action == "Created").Select(x => x.Changes).ToList()), changes => changes.Contains("IdentityColour"));
        var version = d.Version(p.Id);
        var changed = await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { identityColour = IdentityColour.Cyan }, version);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(IdentityColour.Cyan, f.Db(db => db.Projects.Single(x => x.Id == p.Id).IdentityColour));
        var colourChanges = f.Db(db => db.ActivityLog.Where(x => x.ProjectId == p.Id && x.Action == "Updated").Select(x => x.Changes).ToList());
        Assert.Single(colourChanges, changes => changes.Contains("IdentityColour"));
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { identityColour = IdentityColour.Rose }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/projects/{p.Id}", new { identityColour = IdentityColour.Rose }, d.Version(p.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { identityColour = "#ffffff" }, d.Version(p.Id))).StatusCode);
    }

    [Fact]
    [Trait("flag-on", "visual_system")]
    [Trait("flag-off", "visual_system")]
    public async Task Rollback_keeps_stored_colour_but_restores_legacy_response_shape()
    {
        await Flag(true);
        var p = await d.Project(activate: false);
        var stored = f.Db(db => db.Projects.Single(x => x.Id == p.Id).IdentityColour);
        Assert.NotNull(stored);
        var responseOn = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}");
        Assert.Equal(stored, (await responseOn.Json()).S("identityColour"));
        await Flag(false);
        var responseOff = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}");
        Assert.False((await responseOff.Json()).AsObject().ContainsKey("identityColour"));
        Assert.Equal(stored, f.Db(db => db.Projects.Single(x => x.Id == p.Id).IdentityColour));
    }
}
