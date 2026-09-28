using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DesignBasisApiTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task Confirmation_preserves_exact_uses_and_creates_assessments_for_replacement()
    {
        var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0; });
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var a = new DesignBasisEndpoints.VersionInput("Bridge / Pier 1", "Allowable bearing pressure", 100, null,
            "Geotechnical report", "GEO-1", "https://example.test/geotech", "A", new DateOnly(2026, 10, 5), null);
        var create = new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion, "Bearing pressure", owner,
            civil, data.User(TestData.Marc), a, null);
        await Post(TestData.Rita, root, create, 404);
        var entry = await Post(TestData.Pm, root, create with { RequestId = Guid.NewGuid() });
        var id = entry.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id);
        var confirmPath = $"{root}/{id}/versions/{versionId}/confirm";
        await Post(TestData.Marc, confirmPath, new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(), Version<DesignBasisEntry>(id),
            Version<DesignBasisVersion>(versionId), "Geotechnical source checked"), 400);
        await f.DbAsync(async db => { (await db.DesignBasisVersions.SingleAsync(v => v.Id == versionId)).Units = "kPa";
            await db.SaveChangesAsync(); return 0; });
        var confirm = new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(versionId), "Geotechnical source checked");
        var confirmed = await Post(TestData.Marc, confirmPath, confirm);
        Assert.Equal(versionId, confirmed.G("id"));
        Assert.Equal(versionId, (await Post(TestData.Marc, confirmPath, confirm)).G("id"));
        await Post(TestData.Marc, confirmPath, confirm with { RequestId = Guid.NewGuid() }, 409);
        Assert.Equal(versionId, f.Db(db => db.DesignBasisEntries.Single(e => e.Id == id).CurrentVersionId));

        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        await Post(TestData.Pm, $"{root}/{id}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"), 403);
        var use = await Post(TestData.Alex, $"{root}/{id}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId,
            "Task", task.G("id"), "Foundation sizing"));
        var b = a with { NumericValue = 125, Units = "kPa", DeclaredRevision = "B" };
        var proposed = await Post(TestData.Marc, $"{root}/{id}/propose", new DesignBasisEndpoints.ProposeBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(versionId), b, "New geotechnical report"));
        var bId = proposed.G("id");
        await Post(TestData.Marc, $"{root}/{id}/versions/{bId}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(id), Version<DesignBasisVersion>(bId), "Revised report checked"));
        Assert.Equal(BasisStatus.Superseded, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        Assert.Equal(versionId, f.Db(db => db.BasisUses.Single(u => u.Id == use.G("id")).VersionId));
        Assert.Single(f.Db(db => db.BasisImpactAssessments.Where(i => i.BasisUseId == use.G("id") &&
            i.OldVersionId == versionId && i.NewVersionId == bId && i.Status == AssessmentStatus.Pending).ToList()));
        var supersededFilter = await (await f.As(TestData.Pm).GetAsync(root + "?status=Superseded")).Json();
        Assert.DoesNotContain(supersededFilter["items"]!.AsArray(), row => row!.G("id") == id);
        var confirmedFilter = await (await f.As(TestData.Pm).GetAsync(root + "?status=Confirmed")).Json();
        Assert.Contains(confirmedFilter["items"]!.AsArray(), row => row!.G("id") == id);
        var competing = create with { RequestId = Guid.NewGuid(), Version = b with { NumericValue = 150 } };
        await Post(TestData.Pm, root, competing, 409);
        var second = await Post(TestData.Pm, root, competing with { RequestId = Guid.NewGuid(), InspectedDuplicateId = id });
        var otherId = second.G("id");
        var otherVersionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == otherId).Id);
        await Post(TestData.Marc, $"{root}/{otherId}/versions/{otherVersionId}/confirm", new DesignBasisEndpoints.ConfirmBody(Guid.NewGuid(),
            Version<DesignBasisEntry>(otherId), Version<DesignBasisVersion>(otherVersionId), "Conflicting report reviewed"));
        Assert.Single(f.Db(db => db.BasisConflicts.Where(c => !c.Resolved &&
            (c.LeftVersionId == bId || c.RightVersionId == bId) &&
            (c.LeftVersionId == otherVersionId || c.RightVersionId == otherVersionId)).ToList()));
        await (await f.As(TestData.Rita).GetAsync($"{root}/{id}")).Json(404);
    }

    [Fact]
    public async Task Proposed_assumption_requires_approved_unexpired_scope_for_its_consumer()
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var owner = data.User(TestData.Alex);
        var root = $"/api/v1/projects/{project.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Site grading", "Assume existing utility depth", null, null,
            null, null, null, null, new DateOnly(2026, 10, 5), null);
        var entry = await Post(TestData.Pm, root, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
            "Utility depth", owner, civil, data.User(TestData.Marc), input, null));
        var id = entry.G("id");
        var versionId = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == id).Id);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var use = new DesignBasisEndpoints.UseBody(Guid.NewGuid(), versionId, "Task", task.G("id"), "Preliminary layout");
        await Post(TestData.Alex, $"{root}/{id}/uses", use, 400);
        var proceedPath = $"{root}/{id}/versions/{versionId}/proceed";
        await Post(TestData.Marc, proceedPath, new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(),
            Version<DesignBasisVersion>(versionId), "Site grading", owner, new DateOnly(2020, 1, 1), "Confirm before issue"), 400);
        await Post(TestData.Marc, proceedPath, new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(),
            Version<DesignBasisVersion>(versionId), "Site grading", owner, new DateOnly(2027, 1, 1), "Confirm before issue"));
        await Post(TestData.Alex, $"{root}/{id}/uses", use with { RequestId = Guid.NewGuid() });
        Assert.Equal(BasisStatus.Proposed, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == versionId).Status));
        Assert.Single(f.Db(db => db.BasisAssumptionDispositions.Where(d => d.VersionId == versionId && d.OwnerId == owner).ToList()));
    }
}
