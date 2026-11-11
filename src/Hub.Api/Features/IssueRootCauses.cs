using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record IssueRootCauseChoice(Guid Id, string Name, bool IsActive, int SortOrder);
public sealed record IssueRootCauseValue(Guid CategoryId, string RecordedName);
public sealed record IssueRootCauseCount(Guid CategoryId, string Name, bool IsActive, int Count);
public sealed record IssueRootCauseSummary(Guid ProjectId, int ResolvedTotal, int NotRecordedCount, IssueRootCauseCount[] Causes);

/// Classification of the canonical resolution, never another issue workflow or an inferred historical cause.
public static class IssueRootCauses
{
    public const string Flag = "register_screens";
    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{id:guid}/issues/root-causes", Read).RequireFlag(Flag);

    public static IQueryable<Issue> Filter(IQueryable<Issue> query, string? raw, bool enabled)
    {
        if (!enabled || string.IsNullOrEmpty(raw)) return query;
        if (raw == "not-recorded") return query.Where(issue => issue.Status == IssueStatus.Resolved && issue.RootCauseCategoryId == null);
        Check.That(Guid.TryParse(raw, out var id) && id != Guid.Empty, "rootCause", "error.invalid");
        return query.Where(issue => issue.Status == IssueStatus.Resolved && issue.RootCauseCategoryId == id);
    }

    /// Caller owns the resolution transaction. FOR SHARE prevents non-key rename/retirement until commit.
    public static async Task<IssueRootCauseValue> LockSelection(HubDb db, Guid? categoryId)
    {
        Check.That(categoryId is { } id && id != Guid.Empty, "rootCauseCategoryId", "issueRootCause.required");
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Resolution must retain its category lock through commit.");
        var category = await db.IssueRootCauseCategories.FromSqlInterpolated($"SELECT * FROM hub.issue_root_cause_category WHERE id = {categoryId!.Value} FOR SHARE")
            .AsNoTracking().SingleOrDefaultAsync();
        Check.That(category is not null && category.IsActive, "rootCauseCategoryId", "issueRootCause.activeRequired");
        return new(category!.Id, category.Name);
    }

    static async Task<Ok<IssueRootCauseSummary>> Read(Guid id, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), Flag);
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        await access.Project(id, false);
        var rows = await db.Issues.AsNoTracking().Where(issue => issue.ProjectId == id && issue.Status == IssueStatus.Resolved)
            .Select(issue => new RootCauseRow(issue.RootCauseCategoryId, issue.RootCauseLabelSnapshot)).ToArrayAsync();
        var ids = rows.Select(row => row.RootCauseCategoryId).OfType<Guid>().Distinct().ToArray();
        var categories = await db.IssueRootCauseCategories.AsNoTracking().Where(category => ids.Contains(category.Id)).ToDictionaryAsync(category => category.Id);
        if (rows.Any(row => row.RootCauseCategoryId is { } category
            ? string.IsNullOrWhiteSpace(row.RootCauseLabelSnapshot) || !categories.ContainsKey(category)
            : row.RootCauseLabelSnapshot is not null)) throw ApiException.Conflict("issue_root_cause_integrity", "issueRootCause.integrity");
        var counts = rows.Where(row => row.RootCauseCategoryId.HasValue).GroupBy(row => row.RootCauseCategoryId!.Value)
            .Select(group => new IssueRootCauseCount(group.Key, categories[group.Key].Name, categories[group.Key].IsActive, group.Count()))
            .OrderBy(row => categories[row.CategoryId].SortOrder).ThenBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.CategoryId).ToArray();
        return TypedResults.Ok(new IssueRootCauseSummary(id, rows.Length, rows.Count(row => row.RootCauseCategoryId is null), counts));
    }
}

file sealed record RootCauseRow(Guid? RootCauseCategoryId, string? RootCauseLabelSnapshot);
