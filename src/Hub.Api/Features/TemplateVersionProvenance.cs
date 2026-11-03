using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record TemplateVersionProject(Guid ProjectId, string ProjectNumber, string Name, string Status, DateTimeOffset CreatedAt);
public sealed record TemplateRecordedSource(Guid Id, string Name, int Version);
public sealed record TemplateReleasedVersion(Guid Id, int Version, string Status);
public sealed record TemplateProjectProvenanceRead(string State, TemplateRecordedSource? Source, TemplateReleasedVersion? Newer,
    TemplateReleasedVersion? CurrentPublished, DateTimeOffset AsOf);
public sealed record TemplateVersionScreenRead(int Projects, TemplateDraftDiffRead? DraftDiff);

public static class TemplateVersionProvenance
{
    public const string Flag = "people_screens";
    public static async Task<T> InFamily<T>(HubDb db, Guid id, Func<Task<T>> work)
    {
        var family = await db.Templates.AsNoTracking().Where(row => row.Id == id).Select(row => (Guid?)row.FamilyId).SingleOrDefaultAsync() ?? throw ApiException.NotFound();
        return await Tx.Run(db, async () => {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({family.ToString()}, 0))");
            return await work();
        });
    }

    public static (int Page, int Size) Paging(string? page, string? pageSize, string pageField = "page", string sizeField = "pageSize")
    {
        static int? Parse(string? value, string field)
        {
            if (string.IsNullOrEmpty(value)) return null;
            Check.That(int.TryParse(value, out var number) && number > 0, field, "error.invalid"); return number;
        }
        var result = Http.Paging(Parse(page, pageField), Parse(pageSize, sizeField));
        Check.That(((long)result.Page - 1) * result.Size <= int.MaxValue, pageField, "error.invalid");
        return result;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/templates/{id:guid}/projects", async Task<Ok<Page<TemplateVersionProject>>> (Guid id, string? page, string? pageSize,
            HubDb db, Access access, SettingsStore store) => {
            Http.RequireFlag(await store.Get(db), Flag);
            var (pg, size) = Paging(page, pageSize);
            await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            await TemplateEndpoints.Visible(db, access, id);
            var query = access.VisibleProjects().AsNoTracking().Where(project => project.CreatedFromTemplateId == id);
            var total = await query.CountAsync();
            var rows = await query.OrderByDescending(project => project.CreatedAt).ThenByDescending(project => project.Id)
                .Skip((pg - 1) * size).Take(size).Select(project => new TemplateVersionProject(project.Id, project.ProjectNumber, project.Name, project.Status, project.CreatedAt)).ToListAsync();
            return TypedResults.Ok(new Page<TemplateVersionProject>(rows, pg, size, total));
        });
        api.MapGet("/projects/{id:guid}/template-provenance", async Task<Ok<TemplateProjectProvenanceRead>> (Guid id, HubDb db, Access access, SettingsStore store, TimeProvider clock) => {
            Http.RequireFlag(await store.Get(db), Flag);
            await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            var (project, _) = await access.Project(id, track: false); var asOf = clock.GetUtcNow();
            if (project.CreatedFromTemplateId is null && project.TemplateVersion is null)
                return TypedResults.Ok(new TemplateProjectProvenanceRead("NotRecorded", null, null, null, asOf));
            if (project.CreatedFromTemplateId is not { } sourceId || project.TemplateVersion is not { } version)
                return TypedResults.Ok(new TemplateProjectProvenanceRead("Unavailable", null, null, null, asOf));
            var source = await db.Templates.AsNoTracking().SingleOrDefaultAsync(row => row.Id == sourceId);
            if (source is null || source.Version != version || version < 1 || source.Status is not (TemplateStatus.Published or TemplateStatus.Retired))
                return TypedResults.Ok(new TemplateProjectProvenanceRead("Unavailable", null, null, null, asOf));
            var released = await db.Templates.AsNoTracking().Where(row => row.FamilyId == source.FamilyId && row.Version != null
                    && (row.Status == TemplateStatus.Published || row.Status == TemplateStatus.Retired))
                .OrderByDescending(row => row.Version).ThenBy(row => row.Id)
                .Select(row => new TemplateReleasedVersion(row.Id, row.Version!.Value, row.Status)).ToListAsync();
            if (released.GroupBy(row => row.Version).Any(group => group.Count() > 1))
                return TypedResults.Ok(new TemplateProjectProvenanceRead("Unavailable", null, null, null, asOf));
            var latest = released.FirstOrDefault(row => row.Version > version);
            var published = released.Where(row => row.Status == TemplateStatus.Published).ToArray();
            return TypedResults.Ok(new TemplateProjectProvenanceRead("Recorded", new(source.Id, source.Name, version), latest,
                published.Length == 1 ? published[0] : null, asOf));
        });
    }
}
