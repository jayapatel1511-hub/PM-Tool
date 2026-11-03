using System.Data;
using System.Globalization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// All-round package history, independently of the unresolved register's filters and pagination.
public static class ReviewResponseExportEndpoints
{
    public sealed record ReviewResponseSheetRow(int Round, string Comment, string Originator, string Discipline,
        string Severity, string Status, string? Response, string? Outcome);

    public static readonly Col[] Columns = [
        new("round", "round", "number"), new("comment", "comment"), new("originator", "originator"), new("discipline", "discipline"),
        new("severity", "severity"), new("status", "status"), new("response", "response"), new("outcome", "outcome"),
    ];

    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}/response-sheet", Send)
        .RequireFlag(DeliverableEndpoints.RegisterFlag);

    static async Task<IResult> Send(Guid projectId, Guid id, string? format, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, DeliverableEndpoints.RegisterFlag);
        await access.Project(projectId, false);
        Check.OneOf(format ?? "csv", ["csv", "xlsx"], "format");
        // Findings and all labels belong to one consistent read; only the normal export access event is written.
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var (project, _) = await access.Project(projectId, false);
        var package = await db.ReviewPackages.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id && row.ProjectId == projectId && row.DeletedAt == null)
            ?? throw ApiException.NotFound();
        // Do not project through inner joins: that could silently lose a malformed stored comment.
        var findings = await db.ReviewFindings.AsNoTracking().Where(row => row.PackageId == id)
            .OrderBy(row => row.Id).Take(Export.MaxRows + 1).ToArrayAsync();
        if (findings.Length > Export.MaxRows) throw ApiException.Rule("export_too_large", "export.too_large", null, Export.MaxRows);
        var rounds = await db.ReviewRounds.AsNoTracking().Where(row => row.PackageId == id).ToDictionaryAsync(row => row.Id);
        var sourceIds = findings.Select(row => row.SourceRevisionId).OfType<Guid>().Distinct().ToArray();
        var sources = await db.SourceRevisions.AsNoTracking().Where(row => sourceIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var receiptIds = findings.Select(row => row.ExternalReviewId).OfType<Guid>().Distinct().ToArray();
        var receipts = await db.Set<ExternalReview>().AsNoTracking().Where(row => receiptIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var issueIds = findings.Select(row => row.DeliverableIssueId).OfType<Guid>().Concat(receipts.Values.Select(row => row.DeliverableIssueId)).Distinct().ToArray();
        var issues = await db.DeliverableIssues.AsNoTracking().Where(row => issueIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var partyIds = receipts.Values.Select(row => row.ExternalPartyId).Distinct().ToArray();
        var parties = await db.ExternalParties.AsNoTracking().Where(row => partyIds.Contains(row.Id)).Select(row => new { row.Id, row.ProjectId }).ToDictionaryAsync(row => row.Id);
        var roundIds = rounds.Keys.ToArray();
        var manifest = await db.ReviewManifestItems.AsNoTracking().Where(row => roundIds.Contains(row.RoundId)).ToArrayAsync();
        var assignments = await db.DisciplineReviews.AsNoTracking().Where(row => roundIds.Contains(row.RoundId))
            .Select(row => new { row.ProjectId, row.RoundId, row.ProjectDisciplineId }).ToArrayAsync();
        var byId = findings.ToDictionary(row => row.Id);
        var deliverableIds = sources.Values.Where(row => row.DeliverableId.HasValue).Select(row => row.DeliverableId!.Value)
            .Concat(issues.Values.Select(row => row.DeliverableId)).Distinct().ToArray();
        // Deleted same-project source work is legitimate historical provenance; never expose its metadata here.
        var deliverables = await db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(row => deliverableIds.Contains(row.Id))
            .Select(row => new { row.Id, row.ProjectId }).ToDictionaryAsync(row => row.Id);
        var disciplineIds = findings.Select(row => row.ProjectDisciplineId).Append(package.ProjectDisciplineId).Distinct().ToArray();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(row => disciplineIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        var referenceIds = disciplines.Values.Select(row => row.DisciplineId).Distinct().ToArray();
        var labels = await db.Disciplines.AsNoTracking().Where(row => referenceIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        bool Issued(ReviewFinding finding, out DeliverableIssue? issue)
        {
            issue = null;
            return finding.SourceRevisionId is null && finding.OriginatorId is null && finding.DeliverableIssueId is { } issueId
                && issues.TryGetValue(issueId, out issue) && issue.ProjectId == projectId
                && deliverables.TryGetValue(issue.DeliverableId, out var work) && work.ProjectId == projectId
                && finding.ExternalReviewId is { } receiptId && receipts.TryGetValue(receiptId, out var receipt)
                && receipt.ProjectId == projectId && receipt.PackageId == id
                && rounds.TryGetValue(receipt.RoundId, out var receivedRound) && receivedRound.ProjectId == projectId
                && parties.TryGetValue(receipt.ExternalPartyId, out var party) && party.ProjectId == projectId
                && issues.TryGetValue(receipt.DeliverableIssueId, out var original) && original.ProjectId == projectId && original.DeliverableId == issue.DeliverableId
                && manifest.Any(item => item.ProjectId == projectId && item.RoundId == receipt.RoundId
                    && item.DeliverableId == original.DeliverableId && item.DeliverableIssueId == original.Id && item.SourceRevisionId is null);
        }
        bool IssuedManifest(ReviewFinding finding, DeliverableIssue issue)
        {
            var cursor = finding; var seen = new HashSet<Guid>();
            while (seen.Add(cursor.Id) && seen.Count <= Math.Max(1, settings.ChainDepthLimit))
            {
                if (!Issued(cursor, out var current) || current!.Id != issue.Id || cursor.ExternalReviewId != finding.ExternalReviewId
                    || cursor.ProjectId != projectId || cursor.PackageId != id || !rounds.TryGetValue(cursor.RoundId, out var round)) return false;
                var items = manifest.Where(item => item.RoundId == cursor.RoundId && item.DeliverableId == issue.DeliverableId).ToArray();
                if (items.Length > 0) return items.Length == 1 && items[0].ProjectId == projectId && items[0].SourceRevisionId is null
                    && items[0].DeliverableIssueId == issue.Id;
                if (cursor.CarriedFromId is not { } prior || !byId.TryGetValue(prior, out var previous)
                    || !rounds.TryGetValue(previous.RoundId, out var previousRound) || previousRound.Number >= round.Number) return false;
                if (manifest.Any(item => item.RoundId == previous.RoundId && item.DeliverableId == issue.DeliverableId)
                    && (string.IsNullOrWhiteSpace(round.Reason) || string.IsNullOrWhiteSpace(round.RemovalImpact))) return false;
                cursor = previous;
            }
            return false;
        }
        bool Valid(ReviewFinding finding)
        {
            if (finding.ProjectId != projectId || !rounds.TryGetValue(finding.RoundId, out var round) || round.ProjectId != projectId
                || !disciplines.TryGetValue(finding.ProjectDisciplineId, out var discipline) || discipline.ProjectId != projectId) return false;
            if (finding.DeliverableIssueId is not null || finding.ExternalReviewId is not null)
            {
                if (!Issued(finding, out var issue) || !IssuedManifest(finding, issue!)) return false;
                if (finding.CarriedFromId is { } prior)
                    return byId.TryGetValue(prior, out var previous) && Issued(previous, out var previousIssue)
                        && previousIssue!.DeliverableId == issue!.DeliverableId && previous.ExternalReviewId == finding.ExternalReviewId
                        && previous.ProjectDisciplineId == finding.ProjectDisciplineId
                        && rounds.TryGetValue(previous.RoundId, out var previousRound) && previousRound.Number < round.Number
                        && (assignments.Any(assignment => assignment.ProjectId == projectId && assignment.RoundId == finding.RoundId && assignment.ProjectDisciplineId == finding.ProjectDisciplineId)
                            || !assignments.Any(assignment => assignment.ProjectId == projectId && assignment.RoundId == previous.RoundId && assignment.ProjectDisciplineId == finding.ProjectDisciplineId)
                            || !string.IsNullOrWhiteSpace(round.Reason) && !string.IsNullOrWhiteSpace(round.RemovalImpact));
                return receipts[finding.ExternalReviewId!.Value].RoundId == finding.RoundId
                    && receipts[finding.ExternalReviewId.Value].DeliverableIssueId == finding.DeliverableIssueId;
            }
            return finding.OriginatorId is not null && finding.SourceRevisionId is { } sourceId && sources.TryGetValue(sourceId, out var source) && source.ProjectId == projectId
                && source.DeliverableId is { } deliverableId && deliverables.TryGetValue(deliverableId, out var work) && work.ProjectId == projectId;
        }
        if (rounds.Values.Any(row => row.ProjectId != projectId)
            || package.CurrentRoundId is { } current && !rounds.ContainsKey(current)
            || !disciplines.TryGetValue(package.ProjectDisciplineId, out var packageDiscipline) || packageDiscipline.ProjectId != projectId
            || findings.Any(row => !Valid(row)))
            throw ApiException.Conflict("review_response_integrity", "reviewResponse.integrity");

        var originatorIds = findings.Select(row => row.OriginatorId).OfType<Guid>().Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(row => originatorIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id);
        string Originator(ReviewFinding finding) => finding.ExternalReviewId is { } receipt ? receipts[receipt].PartyNameSnapshot
            : finding.OriginatorId is { } originator && people.TryGetValue(originator, out var person) && !string.IsNullOrWhiteSpace(person.DisplayName)
                ? person.DisplayName : Text.Get("reviewResponse.unknownOriginator");
        string Discipline(Guid discipline) => labels.TryGetValue(disciplines[discipline].DisciplineId, out var reference) && !string.IsNullOrWhiteSpace(reference.Name)
            ? reference.Name : Text.Get("reviewResponse.unknownDiscipline");
        // A carried comment is a distinct stored finding. Never deduplicate equal text or copy an older outcome into a new row.
        var ordered = findings.OrderBy(row => rounds[row.RoundId].Number).ThenBy(row => row.CreatedAt).ThenBy(row => row.Id);
        var rows = JsonSerializer.SerializeToNode(ordered.Select(row => new ReviewResponseSheetRow(rounds[row.RoundId].Number,
            row.Text, Originator(row), Discipline(row.ProjectDisciplineId), row.Severity, row.Status, row.Response, row.ResponseOutcome)), JsonOpts.Web)!.AsArray();
        List<(string, string)> parameters = [
            (Text.Get("reviewResponse.scope"), Text.Get("reviewResponse.scopeHint")),
            (Text.Get("reviewResponse.legacy"), Text.Get("reviewResponse.legacyHint")),
            (Text.Get("reviewResponse.rows"), rows.Count.ToString(CultureInfo.InvariantCulture)),
        ];
        var now = Clock.Local(clock.GetUtcNow(), settings);
        List<(string, string)> csvHeader = [.. parameters, (Text.Get("export.generated"), $"{now:yyyy-MM-dd HH:mm} ({settings.OrgTimeZone})")];
        var key = new string($"{project.ProjectNumber}-{package.Key}-response-sheet".Where(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_').Take(120).ToArray());
        var result = await ExportFile.Send(db, store, format, Text.Get("export.reviewResponse", project.ProjectNumber, package.Key),
            Columns, rows, parameters, projectId, key, clock, format == "xlsx" ? null : csvHeader);
        if (snapshot is not null) await snapshot.CommitAsync();
        return result;
    }
}
