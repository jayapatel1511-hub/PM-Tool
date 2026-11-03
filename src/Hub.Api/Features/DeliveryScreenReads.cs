using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record DeliverySummary(bool Available, int? BlockingCheckCount, int PackageProblemCount, int UnknownCheckCount);
public sealed record DeliveryProblem(string Code, string Message);
public sealed record DeliveryReason(string Code, string Message, bool SourceExpected, RecordContextInputView? Source);
public sealed record DeliveryCheckView(Guid CheckId, string? State, bool Known, bool Blocking, RecordContextPerson CheckingOwner,
    Guid? DisciplineId, string? DisciplineName, IReadOnlyList<DeliveryReason> Reasons);
public sealed record DeliveryRevision(Guid Id, string Revision, string? Url);
public sealed record DeliveryManifestView(Guid Id, Guid DeliverableId, bool Required, string? Key, string? Title, string? Status,
    bool Available, DeliveryRevision? RecordedRevision, DeliveryRevision? CurrentRevision, bool? RevisionMismatch);
public sealed record DeliveryStatusCount(string Status, int Count);
public sealed record DeliveryPresentation(DeliverySummary Summary, DateTimeOffset EvaluatedAt, bool ChecksComplete, bool ManifestComplete,
    string IssueAvailability, IReadOnlyList<DeliveryCheckView> Checks, IReadOnlyList<DeliveryProblem> PackageProblems,
    IReadOnlyList<DeliveryManifestView> Manifest, IReadOnlyList<DeliveryStatusCount> StatusCounts, int UnavailableDeliverables);

/// Additive read presentation. Canonical states, blocker-to-check provenance and issue guards stay in their existing adapters.
public static class DeliveryScreenReads
{
    internal sealed record Read(SubmissionReadinessResult? Readiness, string State, SubmissionContextData.Snapshot Snapshot, DeliverySummary Summary);

    internal static async Task<Read> Summary(HubDb db, Project project, SubmissionPackage package,
        SubmissionReadinessResult? supplied = null, IReadOnlyList<SubmissionCheck>? suppliedChecks = null)
    {
        var terminal = package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled;
        var readiness = terminal ? null : supplied ?? await SubmissionReadiness.Evaluate(db, package);
        var state = readiness is null || package.Status == SubmissionStatus.Draft ? package.Status : readiness.Ready ? SubmissionStatus.Ready : SubmissionStatus.Checking;
        var checks = suppliedChecks ?? await db.SubmissionChecks.AsNoTracking().Where(c => c.ProjectId == project.Id
            && c.PackageId == package.Id && c.ManifestVersion == package.ManifestVersion).ToListAsync();
        var adapter = new SubmissionContextData(db, project);
        adapter.SeedPackage(package, checks, readiness, state);
        var snapshot = await adapter.Read(package.Id) ?? throw ApiException.NotFound();
        var problems = Problems(snapshot);
        return new(readiness, state, snapshot, new(readiness is not null,
            readiness is null ? null : snapshot.Checks.Where(c => c.Blocking).Select(c => c.Row.Id).Distinct().Count(),
            problems.Count, snapshot.Checks.Count(c => !c.Known || c.State is null)));
    }

    static IReadOnlyList<DeliveryProblem> Problems(SubmissionContextData.Snapshot snapshot)
    {
        var ids = snapshot.Checks.Select(c => c.Row.Id).ToHashSet();
        return (snapshot.Readiness?.Blockers ?? []).Where(b =>
            {
                var info = b.ContextInfo;
                return info is null || !info.Known || info.CheckId is null || !ids.Contains(info.CheckId.Value);
            })
            .DistinctBy(b => (b.Code, b.SourceId, b.ContextInfo?.RecordKind, b.ContextInfo?.RecordId))
            .Select(b => new DeliveryProblem(b.Code, b.Message)).ToArray();
    }

    public static int Priority(DeliverySummary summary) => summary.BlockingCheckCount > 0 ? 0
        : summary.PackageProblemCount > 0 || summary.UnknownCheckCount > 0 ? 1 : summary.Available ? 2 : 3;

    internal static async Task<DeliveryPresentation> Detail(HubDb db, Project project, SubmissionPackage package, Read read,
        IReadOnlyList<SubmissionEndpoints.SubmissionCheckRow> checks, IReadOnlyList<SubmissionManifestItem> manifest,
        SubmissionIssue? issue, DateTimeOffset now)
    {
        var disciplineIds = checks.Select(c => c.ProjectDisciplineId).OfType<Guid>().Distinct().ToArray();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == project.Id && disciplineIds.Contains(d.Id))
            .Select(d => new { d.Id, d.Discipline!.Name }).ToDictionaryAsync(d => d.Id, d => d.Name);
        var checkViews = new List<DeliveryCheckView>();
        foreach (var check in checks)
        {
            var canonical = read.Snapshot.Checks.SingleOrDefault(c => c.Row.Id == check.Id);
            var reasons = new List<DeliveryReason>();
            foreach (var blocker in (read.Readiness?.Blockers ?? []).Where(b => b.ContextInfo?.CheckId == check.Id))
            {
                var reference = blocker.ContextInfo!;
                var source = check.Context?.Inputs.FirstOrDefault(i => i.Kind == reference.RecordKind && i.Id == reference.RecordId);
                reasons.Add(new(blocker.Code, blocker.Message, reference.RecordKind is not null && reference.RecordId is not null, source));
            }
            if (!string.IsNullOrWhiteSpace(check.Reason) && !reasons.Any(r => r.Message == check.Reason)) reasons.Add(new("recorded", check.Reason, false, null));
            checkViews.Add(new(check.Id, canonical?.State, canonical?.Known == true, canonical?.Blocking == true,
                check.Context?.Owner ?? new(null, null, "Unknown"), check.ProjectDisciplineId,
                check.ProjectDisciplineId is { } discipline ? disciplines.GetValueOrDefault(discipline) : null, reasons));
        }
        var members = manifest.Where(m => m.ProjectId == project.Id && m.PackageId == package.Id && m.ManifestVersion == package.ManifestVersion).ToArray();
        var ids = members.Select(m => m.DeliverableId).Distinct().ToArray();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == project.Id && ids.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var recordedIds = members.Select(m => m.SourceRevisionId).Distinct().ToArray();
        var revisions = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == project.Id && recordedIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        var identities = revisions.Values.Select(Coordination.Identity).Distinct().ToArray();
        var heads = await db.SourceHeads.AsNoTracking().Where(h => h.ProjectId == project.Id && identities.Contains(h.Identity)).ToDictionaryAsync(h => h.Identity);
        var currentIds = heads.Values.Select(h => h.CurrentRevisionId).Distinct().ToArray();
        var current = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == project.Id && currentIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        static DeliveryRevision Revision(SourceRevision r) => new(r.Id, r.Revision,
            Uri.TryCreate(r.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" ? r.Url : null);
        var rows = members.Select(m =>
        {
            var deliverable = deliverables.GetValueOrDefault(m.DeliverableId);
            var recorded = revisions.GetValueOrDefault(m.SourceRevisionId);
            if (recorded?.DeliverableId != m.DeliverableId) recorded = null;
            var identity = recorded is null ? null : Coordination.Identity(recorded);
            var head = identity is null ? null : heads.GetValueOrDefault(identity);
            var registered = head is null ? null : current.GetValueOrDefault(head.CurrentRevisionId);
            if (registered is not null && (registered.DeliverableId != m.DeliverableId || Coordination.Identity(registered) != identity)) registered = null;
            return new DeliveryManifestView(m.Id, m.DeliverableId, m.Required, deliverable?.Key, deliverable?.Name, deliverable?.Status,
                deliverable is not null, recorded is null ? null : Revision(recorded), registered is null ? null : Revision(registered),
                recorded is null || registered is null ? null : recorded.Id != registered.Id);
        }).OrderBy(m => m.Key, StringComparer.Ordinal).ThenBy(m => m.Id).ToArray();
        var statuses = rows.Where(m => m.Available && m.Status is not null).DistinctBy(m => m.DeliverableId)
            .GroupBy(m => m.Status!).OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => new DeliveryStatusCount(g.Key, g.Count())).ToArray();
        var issueAvailability = issue is not null && issue.ProjectId == project.Id && issue.PackageId == package.Id ? "Available"
            : package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded ? "Unavailable" : "NotIssued";
        return new(read.Summary, now, read.Snapshot.Checks.Count == checks.Count, members.Length == manifest.Count,
            issueAvailability, checkViews, Problems(read.Snapshot), rows, statuses, rows.Where(m => !m.Available).Select(m => m.DeliverableId).Distinct().Count());
    }
}
