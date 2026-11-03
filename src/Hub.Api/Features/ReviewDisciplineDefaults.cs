using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ReviewDefaultSettings(Guid DeliverableTypeId, int RowVersion, Guid[] DisciplineIds);
public sealed record ReviewDefaultSettingsWrite(Guid[]? DisciplineIds, int? RowVersion);
public sealed record ReviewDefaultSuggestions(Guid ProjectId, Guid[] SourceRevisionIds, ReviewDefaultSourceType[] SourceTypes,
    ReviewDefaultSuggestion[] Disciplines, bool ExceedsAssignmentLimit);
public sealed record ReviewDefaultSourceType(Guid SourceRevisionId, Guid DeliverableId, Guid DeliverableTypeId, int TypeRowVersion, bool TypeActive);
public sealed record ReviewDefaultSuggestion(Guid DisciplineId, string? Name, Guid? ProjectDisciplineId, string State, Guid[] DeliverableTypeIds);

public static class ReviewDisciplineDefaults
{
    public static void MapAdmin(RouteGroupBuilder admin)
    {
        admin.MapGet("/deliverableTypes/{id:guid}/review-defaults", Read).RequireFlag("register_screens");
        admin.MapPut("/deliverableTypes/{id:guid}/review-defaults", Write).RequireFlag("register_screens");
    }
    public static void MapProject(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/reviews/default-disciplines", Suggestions).RequireFlag("register_screens");
    static async Task Authorise(Access access, HubDb db, SettingsStore store)
    { Access.Demand(Permissions.Administer(access.Actor)); Http.RequireFlag(await store.Get(db), "register_screens"); }

    static async Task<Ok<ReviewDefaultSettings>> Read(Guid id, Access access, HubDb db, SettingsStore store)
    {
        await Authorise(access, db, store);
        await using var snapshot = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var type = await db.DeliverableTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id) ?? throw ApiException.NotFound();
        return TypedResults.Ok(new ReviewDefaultSettings(id, type.RowVersion, await db.DeliverableReviewDefaults.Where(d => d.DeliverableTypeId == id).OrderBy(d => d.DisciplineId).Select(d => d.DisciplineId).ToArrayAsync()));
    }
    static async Task<Ok<ReviewDefaultSettings>> Write(Guid id, ReviewDefaultSettingsWrite body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        await Authorise(access, db, store);
        await using var transaction = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync() : null;
        var type = await db.DeliverableTypes.SingleOrDefaultAsync(t => t.Id == id) ?? throw ApiException.NotFound();
        await Http.CheckVersion(db, http, type, body.RowVersion);
        Check.That(body.DisciplineIds is { Length: <= 50 }, "disciplineIds", "reviewDefaults.required");
        var wanted = body.DisciplineIds!;
        Check.That(wanted.All(x => x != Guid.Empty) && wanted.Distinct().Count() == wanted.Length, "disciplineIds", "error.invalid");
        var known = await db.Disciplines.Where(d => wanted.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        Check.That(known.Count == wanted.Length, "disciplineIds", "error.invalid");
        var existing = await db.DeliverableReviewDefaults.Where(d => d.DeliverableTypeId == id).ToListAsync();
        var oldIds = existing.Select(d => d.DisciplineId).ToHashSet(); var added = wanted.Except(oldIds).Order().ToArray();
        var removed = oldIds.Except(wanted).Order().ToArray();
        Check.That((type.IsActive || added.Length == 0) && added.All(x => known[x].IsActive), "disciplineIds", "reviewDefaults.inactive");
        if (added.Length == 0 && removed.Length == 0) return TypedResults.Ok(new ReviewDefaultSettings(id, type.RowVersion, wanted.Order().ToArray()));
        db.DeliverableReviewDefaults.RemoveRange(existing.Where(d => removed.Contains(d.DisciplineId)));
        db.DeliverableReviewDefaults.AddRange(added.Select(d => new DeliverableReviewDefault { DeliverableTypeId = id, DisciplineId = d }));
        db.Entry(type).Property(t => t.RowVersion).IsModified = true;
        var changes = removed.Select(d => new { field = "RequiredReviewDisciplineId", old = (Guid?)d, @new = (Guid?)null })
            .Concat(added.Select(d => new { field = "RequiredReviewDisciplineId", old = (Guid?)null, @new = (Guid?)d })).ToArray();
        db.LogEvent("ReferenceData", type.Id, "Updated", "admin", key: nameof(DeliverableType), name: type.Name, changes: changes);
        await db.SaveChangesAsync(); if (transaction is not null) await transaction.CommitAsync();
        return TypedResults.Ok(new ReviewDefaultSettings(id, type.RowVersion, wanted.Order().ToArray()));
    }

    static async Task<Ok<ReviewDefaultSuggestions>> Suggestions(Guid projectId, string? sourceRevisionIds, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "register_screens");
        await using var snapshot = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        await access.Project(projectId, false);
        var raw = (sourceRevisionIds ?? "").Split(','); var ids = new List<Guid>();
        Check.That(raw.Length is > 0 and <= 200, "sourceRevisionIds", "error.invalid");
        foreach (var value in raw) { Check.That(Guid.TryParse(value, out var id) && id != Guid.Empty, "sourceRevisionIds", "error.invalid"); ids.Add(id); }
        Check.That(ids.Distinct().Count() == ids.Count, "sourceRevisionIds", "error.invalid");
        var sources = await db.SourceRevisions.AsNoTracking().Where(s => s.ProjectId == projectId && ids.Contains(s.Id)).ToArrayAsync();
        if (sources.Length != ids.Count) throw ApiException.NotFound();
        var deliverableIds = sources.Select(s => s.DeliverableId).OfType<Guid>().Distinct().ToArray();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && deliverableIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var typeIds = deliverables.Values.Select(d => d.DeliverableTypeId).Distinct().ToArray();
        var types = await db.DeliverableTypes.AsNoTracking().Where(t => typeIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        foreach (var source in sources)
        {
            Check.That(source.DeliverableId is { } id && deliverables.ContainsKey(id), "sourceRevisionIds", "coord.reference");
            Check.That(await Coordination.Published(db, source), "sourceRevisionIds", "review.source_unpublished");
            var head = await Coordination.Head(db, source);
            Check.That(head is null || head.CurrentRevisionId == source.Id, "sourceRevisionIds", "review.source_changed");
        }
        var defaults = await db.DeliverableReviewDefaults.AsNoTracking().Where(d => typeIds.Contains(d.DeliverableTypeId)).ToArrayAsync();
        var disciplineIds = defaults.Select(d => d.DisciplineId).Distinct().ToArray();
        var references = await db.Disciplines.AsNoTracking().Where(d => disciplineIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var mappings = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == projectId && disciplineIds.Contains(d.DisciplineId)).ToArrayAsync();
        if (types.Count != typeIds.Length || references.Count != disciplineIds.Length || mappings.GroupBy(d => d.DisciplineId).Any(g => g.Count() > 1))
            throw ApiException.Conflict("review_defaults_integrity", "reviewDefaults.integrity");
        var suggestions = disciplineIds.Select(id =>
        {
            var reference = references[id]; var mapping = mappings.SingleOrDefault(d => d.DisciplineId == id);
            var origins = defaults.Where(d => d.DisciplineId == id).Select(d => d.DeliverableTypeId).Distinct().Order().ToArray();
            var state = mapping is null ? "NotInProject" : !reference.IsActive ? "InactiveDiscipline" : !mapping.IsActive ? "InactiveProjectDiscipline"
                : !origins.Any(t => types[t].IsActive) ? "InactiveType" : "Available";
            return (row: new ReviewDefaultSuggestion(id, reference.Name, mapping?.Id, state, origins), order: mapping?.SortOrder ?? int.MaxValue);
        }).OrderBy(x => x.row.State == "Available" ? 0 : 1).ThenBy(x => x.order).ThenBy(x => x.row.Name, StringComparer.Ordinal).ThenBy(x => x.row.DisciplineId).Select(x => x.row).ToArray();
        var sourceTypes = sources.OrderBy(s => s.Id).Select(s => { var d = deliverables[s.DeliverableId!.Value]; var type = types[d.DeliverableTypeId]; return new ReviewDefaultSourceType(s.Id, d.Id, type.Id, type.RowVersion, type.IsActive); }).ToArray();
        return TypedResults.Ok(new ReviewDefaultSuggestions(projectId, ids.Order().ToArray(), sourceTypes, suggestions, suggestions.Length > 50));
    }
}
