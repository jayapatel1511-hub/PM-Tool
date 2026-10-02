using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DesignBasisSchemaTests(HubFactory f)
{
    readonly TestData data = new(f);

    [Fact]
    public async Task Migration_keeps_proposals_flexible_and_confirmed_versions_immutable()
    {
        var project = await data.Project();
        var disciplineId = data.ProjectDiscipline(project.Id, "Civil");
        var ownerId = data.User(TestData.Alex);
        var entry = new DesignBasisEntry { ProjectId = project.Id, Kind = BasisKind.Criterion,
            Title = "Bearing pressure", OwnerId = ownerId, ProjectDisciplineId = disciplineId };
        var version = new DesignBasisVersion { ProjectId = project.Id, EntryId = entry.Id, Number = 1,
            Scope = "Pier 1", Statement = "Allowable bearing pressure", NumericValue = 100 };
        await f.DbAsync(async db => { db.DesignBasisEntries.Add(entry); db.DesignBasisVersions.Add(version); await db.SaveChangesAsync(); return 0; });

        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            var row = await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id);
            row.Status = BasisStatus.Confirmed;
            row.SourceUrl = "https://example.test/basis";
            row.ConfirmationRationale = "Reviewed source";
            row.ConfirmedBy = ownerId;
            row.ConfirmedAt = f.Clock.GetUtcNow();
            await db.SaveChangesAsync(); return 0;
        }));

        await f.DbAsync(async db =>
        {
            var row = await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id);
            row.Units = "kPa";
            row.Status = BasisStatus.Confirmed;
            row.SourceUrl = "https://example.test/basis";
            row.ConfirmationRationale = "Reviewed source";
            row.ConfirmedBy = ownerId;
            row.ConfirmedAt = f.Clock.GetUtcNow();
            await db.SaveChangesAsync(); return 0;
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            var row = await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id);
            row.NumericValue = 125;
            await db.SaveChangesAsync(); return 0;
        }));
        await f.DbAsync(async db =>
        {
            var row = await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id);
            row.Status = BasisStatus.Superseded;
            await db.SaveChangesAsync(); return 0;
        });
        Assert.Equal(100, f.Db(db => db.DesignBasisVersions.Single(v => v.Id == version.Id).NumericValue));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.DesignBasisVersions.Remove(await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id));
            await db.SaveChangesAsync(); return 0;
        }));
    }
}
