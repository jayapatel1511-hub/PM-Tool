using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ReviewIssuedSourceRead(Guid DeliverableIssueId, Guid DeliverableId, string DeliverableKey,
    string? Revision, DateOnly IssuedDate, int RowVersion, string AuthorsState, bool Selectable, string? Reason);
public sealed record ReviewIssuedAssignmentGuidance(bool AllowSelfReview, bool SourcesAvailable, Guid[] AuthorIds,
    Guid[] ExcludedReviewerIds, IReadOnlyList<ReviewIssuedSourceRead> Sources);

/// Actual issued identities and stored provenance only. This is not another revision, closure or approval engine.
public static class ReviewIssuedSources
{
    public static IQueryable<ReviewPackage> VisiblePackages(HubDb db, bool issuedReviewsEnabled) => db.ReviewPackages
        .Where(package => issuedReviewsEnabled || !db.ReviewRounds.Any(round => round.PackageId == package.Id
            && db.ReviewManifestItems.Any(item => item.RoundId == round.Id && item.DeliverableIssueId != null)));

    public static IQueryable<ReviewFinding> VisibleFindings(HubDb db, bool issuedReviewsEnabled)
    {
        var packages = VisiblePackages(db, issuedReviewsEnabled).Select(package => package.Id);
        return db.ReviewFindings.Where(finding => issuedReviewsEnabled || finding.ExternalReviewId == null
            && finding.DeliverableIssueId == null && packages.Contains(finding.PackageId));
    }

    internal sealed record Source(DeliverableIssue Issue, Deliverable Deliverable, Guid[]? Authors);

    internal static Task<bool> IsIssuedPackage(HubDb db, Guid packageId) => db.ReviewManifestItems.AsNoTracking()
        .AnyAsync(item => item.DeliverableIssueId != null && db.ReviewRounds.Any(round => round.Id == item.RoundId && round.PackageId == packageId));

    internal static async Task<Dictionary<Guid, Source>> ReadMany(HubDb db, Guid projectId, IReadOnlyCollection<Guid> issueIds)
    {
        var ids = issueIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var issues = await db.DeliverableIssues.AsNoTracking().Where(value => ids.Contains(value.Id) && value.ProjectId == projectId).ToArrayAsync();
        if (issues.Length != ids.Length) throw ApiException.NotFound();
        var deliverableIds = issues.Select(issue => issue.DeliverableId).Distinct().ToArray();
        var deliverables = await db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(value => deliverableIds.Contains(value.Id) && value.ProjectId == projectId).ToDictionaryAsync(value => value.Id);
        if (deliverables.Count != deliverableIds.Length) throw ApiException.NotFound();
        var authors = await IssuedAuthorship.ReadManyAsync(db, issues);
        return issues.ToDictionary(issue => issue.Id, issue => new Source(issue, deliverables[issue.DeliverableId], authors.GetValueOrDefault(issue.Id)));
    }

    internal static async Task<Source> Read(HubDb db, Guid projectId, Guid issueId) => (await ReadMany(db, projectId, [issueId]))[issueId];

    internal static async Task<Source> Require(HubDb db, Guid projectId, Guid issueId)
    {
        return Require(await Read(db, projectId, issueId));
    }

    internal static Source Require(Source source)
    {
        Check.That(source.Authors is { Length: > 0 }, "deliverableIssueId", "externalReview.authorsUnknown");
        Check.That(source.Deliverable.DeletedAt is null, "deliverableIssueId", "coord.reference");
        return source;
    }

    internal static ReviewIssuedSourceRead Row(Source source) => new(source.Issue.Id, source.Deliverable.Id, source.Deliverable.Key,
        source.Issue.Revision, source.Issue.IssuedDate, source.Issue.RowVersion, source.Authors is null ? "Unknown" : "Captured",
        source.Authors is { Length: > 0 } && source.Deliverable.DeletedAt is null,
        source.Authors is null ? Text.Get("externalReview.authorsUnknown") : source.Deliverable.DeletedAt is not null ? Text.Get("coord.reference") : null);

    internal static async Task<ReviewIssuedAssignmentGuidance> Guidance(HubDb db, Project project, Guid[] ids, OrgSettings settings)
    {
        Check.That(ids.Length <= 200 && ids.Distinct().Count() == ids.Length, "issuedRevisionIds", "review.manifest_rule");
        var rows = new List<ReviewIssuedSourceRead>(); var authors = new HashSet<Guid>(); var sources = await ReadMany(db, project.Id, ids);
        foreach (var id in ids)
        {
            var source = sources[id]; rows.Add(Row(source));
            if (source.Authors is not null) authors.UnionWith(source.Authors);
        }
        var eligible = await Coordination.People(db, project).Select(user => user.Id).ToArrayAsync();
        var ordered = authors.Where(eligible.Contains).Order().ToArray();
        return new(settings.AllowSelfReview, ids.Length > 0 && rows.All(row => row.Selectable), ordered,
            settings.AllowSelfReview ? [] : ordered, rows);
    }
}
