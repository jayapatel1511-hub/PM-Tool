using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// Exercises the remaining representative GET contracts through HubFactory.Client so the
/// integration shape recorder observes their complete response bodies.
public sealed class SchemaResponseShapeTests
{
    [Fact]
    [Trait("shape", "integration")]
    public async Task Following_feed_preserves_boolean_audit_change_values()
    {
        using var f = new HubFactory();
        var d = new TestData(f);
        var project = await d.Project();
        var task = await d.NewTask(project.Id);
        var viewer = f.As(TestData.Alex);
        await (await viewer.Put($"/api/v1/projects/{project.Id}/follow", new { level = FollowLevel.AllActivity })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { requiresReview = true }, await d.TaskVersion(task))).Json();
        var feed = await (await viewer.GetAsync($"/api/v1/me/following?projectId={project.Id}")).Json();
        var change = feed["items"]!.AsArray().Select(row => row!["entry"]!)
            .Where(entry => entry.G("itemId") == task.G("id")).SelectMany(entry => entry["changes"]!.AsArray())
            .Single(change => change!.S("field") == "RequiresReview")!;
        Assert.False(change["old"]!.GetValue<bool>());
        Assert.True(change["new"]!.GetValue<bool>());
    }

    [Fact]
    [Trait("shape", "integration")]
    public async Task Audit_and_commitment_category_reads_record_full_shapes()
    {
        using var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        var d = new TestData(f);
        var project = await d.Project();
        await d.NewTask(project.Id);
        await (await admin.Put("/api/v1/admin/settings/flag.commitment_reliability", new { value = true })).Json();
        await f.DbAsync(async db =>
        {
            db.Set<CommitmentMissCategory>().Add(new CommitmentMissCategory { Name = "Synthetic dependency", SortOrder = 1 });
            await AuditChainInitialization.Run(db, f.Clock.Now);
            return 0;
        });
        f.Services.GetRequiredService<SettingsStore>().Invalidate();

        var audit = await (await admin.GetAsync("/api/v1/admin/audit-chains")).Json();
        var chain = audit.AsArray().Single(c => c!["chainId"]!.GetValue<string>() == project.Id.ToString());
        Assert.Equal(project.Id.ToString(), chain!["chainId"]!.GetValue<string>());
        Assert.True(chain["headSeq"]!.GetValue<long>() > 0);
        Assert.Equal(64, chain["headHash"]!.GetValue<string>().Length);
        var categories = await (await admin.GetAsync("/api/v1/admin/commitmentMissCategories")).Json();
        var category = categories.AsArray().Single(c => c!["name"]!.GetValue<string>() == "Synthetic dependency");
        var usage = await (await admin.GetAsync($"/api/v1/admin/commitmentMissCategories/{category.G("id")}/usage")).Json();
        Assert.Equal(0, usage.I("projects"));
        var publicCategories = await (await admin.GetAsync("/api/v1/commitment-miss-categories")).Json();
        Assert.Contains(publicCategories.AsArray(), c => c!["id"]!.GetValue<string>() == category.G("id").ToString());
    }

    [Fact]
    [Trait("shape", "integration")]
    public async Task Organisation_reads_record_tree_placements_and_project_lead_unit_shapes()
    {
        using var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        var d = new TestData(f);
        var alex = d.User(TestData.Alex);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var teamLevel = levels.First(l => l!.S("name") == "Team")!;
        var unit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Shape test unit", levelId = teamLevel.G("id"), leaderId = d.User(TestData.Jill) })).Json(201);
        var unitId = unit.G("id");
        var alexVersion = f.Db(db => db.Users.Where(u => u.Id == alex).Select(u => u.RowVersion).Single());
        await (await admin.Patch($"/api/v1/admin/organisation/users/{alex}/placement", new { unitId }, alexVersion)).Json(204);
        var project = await d.Project();
        await f.DbAsync(async db => { db.Projects.Single(p => p.Id == project.Id).LeadUnitId = unitId; return await db.SaveChangesAsync(); });

        var organisation = await (await admin.GetAsync("/api/v1/organisation")).Json();
        Assert.NotNull(organisation["levels"]);
        Assert.NotNull(organisation["units"]);
        Assert.NotNull(organisation["people"]);
        var placements = await (await admin.GetAsync($"/api/v1/organisation/placements?ids={alex}")).Json();
        var placement = Assert.Single(placements.AsArray());
        Assert.Equal(alex.ToString(), placement!["id"]!.GetValue<string>());
        Assert.Equal(unitId.ToString(), placement["unitId"]!.GetValue<string>());
        var leadUnit = await (await admin.GetAsync($"/api/v1/projects/{project.Id}/lead-unit")).Json();
        Assert.Equal(unitId.ToString(), leadUnit["leadUnitId"]!.GetValue<string>());
    }

    [Fact]
    [Trait("shape", "integration")]
    public async Task Organisation_edits_and_import_preview_preserve_versions_and_validation_atomicity()
    {
        using var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        await (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).Json();
        var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var ids = levels.Select(l => l!.G("id")).ToArray();
        var versions = levels.ToDictionary(l => l!.G("id"), l => l!.I("rowVersion"));
        var reordered = (await (await admin.Post("/api/v1/admin/organisation/levels/order", new { ids, versions })).Json()).AsArray();
        Assert.Equal(ids, reordered.Select(l => l!.G("id")));
        var region = reordered.First(l => l!.S("name") == "Region")!;
        var renamed = await (await admin.Patch($"/api/v1/admin/organisation/levels/{region.G("id")}", new { name = "Shape Region" }, region.I("rowVersion"))).Json();
        Assert.Equal("Shape Region", renamed.S("name"));
        Assert.True(renamed.I("rowVersion") > region.I("rowVersion"));
        var unit = await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Shape root", levelId = region.G("id") })).Json(201);
        var edited = await (await admin.Patch($"/api/v1/admin/organisation/units/{unit.G("id")}", new { name = "Shape renamed root" }, unit.I("rowVersion"))).Json();
        Assert.Equal("Shape renamed root", edited.S("name"));
        Assert.True(edited.I("rowVersion") > unit.I("rowVersion"));
        var before = (await (await admin.GetAsync("/api/v1/admin/organisation/units")).Json()).AsArray().Count;
        var preview = await (await admin.Post("/api/v1/admin/organisation/import/preview", new { csv = "unit_path,level\nPreview root,Shape Region" })).Json();
        Assert.Equal(1, preview.I("rows"));
        Assert.Equal(1, preview.I("creates"));
        Assert.False(preview["applied"]!.GetValue<bool>());
        Assert.NotEmpty(preview["changes"]!.AsArray());
        var invalid = await (await admin.Post("/api/v1/admin/organisation/import/apply", new { csv = "unit_path,level\nInvalid root,Missing level" })).Json(422);
        Assert.False(invalid["applied"]!.GetValue<bool>());
        Assert.NotEmpty(invalid["errors"]!.AsArray());
        Assert.Equal(before, (await (await admin.GetAsync("/api/v1/admin/organisation/units")).Json()).AsArray().Count);
    }

    [Fact]
    [Trait("shape", "integration")]
    public async Task Revision_check_read_records_convention_and_deliverable_marks_shapes()
    {
        using var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        var d = new TestData(f);
        await (await admin.Put("/api/v1/admin/settings/flag.revision_check", new { value = true })).Json();
        var project = await d.Project();
        var pm = f.As(TestData.Pm);
        var deliverable = await (await pm.Post($"/api/v1/projects/{project.Id}/deliverables", new { name = "Shape revision package", projectDisciplineId = d.ProjectDiscipline(project.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), requiresReview = false })).Json(201);
        await (await pm.Put($"/api/v1/projects/{project.Id}/revision-check", new RevisionCheckEndpoints.ConventionBody(d.Version(project.Id), "letters"))).Json();
        var deliverableId = deliverable.G("id");
        var deliverableVersion = f.Db(db => db.Deliverables.Where(x => x.Id == deliverableId).Select(x => x.RowVersion).Single());
        await (await pm.Post($"/api/v1/deliverables/{deliverableId}/issue", new { revision = "Unexpected-99", issuedTo = "Synthetic recipient", confirmOpenTasks = true, rowVersion = deliverableVersion })).Json();

        var body = await (await pm.GetAsync($"/api/v1/projects/{project.Id}/revision-check")).Json();
        Assert.Equal("letters", body["convention"]!["preset"]!.GetValue<string>());
        var marks = Assert.Single(body["deliverables"]!.AsArray());
        Assert.Equal(deliverableId.ToString(), marks!["id"]!.GetValue<string>());
        Assert.NotEmpty(marks["marks"]!.AsArray());
        var issueMarks = Assert.Single(marks["issues"]!.AsArray());
        Assert.Equal("Unexpected-99", issueMarks!["revision"]!.GetValue<string>());
    }
}
