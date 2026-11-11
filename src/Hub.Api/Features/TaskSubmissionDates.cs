using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// List-only projection over an already authorised, filtered and bounded task page.
/// The caller supplies a single read snapshot; package state and readiness are never changed.
public static class TaskSubmissionDates
{
    public sealed record SubmissionDateFact(string State, DateOnly? TargetDate);

    public static async Task<IReadOnlyList<TaskQueries.Row>> Apply(HubDb db, IReadOnlyList<TaskQueries.Row> rows)
    {
        if (rows.Count == 0) return rows;
        var deliverableIds = rows.Where(row => row.DeliverableId.HasValue).Select(row => row.DeliverableId!.Value).Distinct().ToArray();
        var projects = rows.Select(row => row.ProjectId).Distinct().ToArray();
        // Default live-record filters deliberately keep deleted/missing references unavailable.
        var deliverables = await db.Deliverables.AsNoTracking().Where(row => deliverableIds.Contains(row.Id))
            .Select(row => new DeliverableProject(row.Id, row.ProjectId)).ToDictionaryAsync(row => row.Id, row => row.ProjectId);
        var dates = await (from item in db.SubmissionManifestItems.AsNoTracking()
            join package in db.SubmissionPackages.AsNoTracking() on item.PackageId equals package.Id
            join deliverable in db.Deliverables.AsNoTracking() on item.DeliverableId equals deliverable.Id
            where deliverableIds.Contains(item.DeliverableId) && projects.Contains(package.ProjectId)
                && item.ProjectId == package.ProjectId && deliverable.ProjectId == package.ProjectId && item.ManifestVersion == package.ManifestVersion
                && (package.Status == SubmissionStatus.Draft || package.Status == SubmissionStatus.Checking || package.Status == SubmissionStatus.Ready)
            group package.TargetDate by new SubmissionDateKey { ProjectId = item.ProjectId, DeliverableId = item.DeliverableId } into membership
            select new SubmissionDateRow(membership.Key.ProjectId, membership.Key.DeliverableId, membership.Min())).ToListAsync();
        var byReference = dates.ToDictionary(row => (row.ProjectId, row.DeliverableId), row => row.Date);
        return rows.Select(row => row with {
            SubmissionDate = row.DeliverableId is not { } deliverable ? new("Known", null)
                : !deliverables.TryGetValue(deliverable, out var projectId) || projectId != row.ProjectId ? new("Unavailable", null)
                : new("Known", byReference.TryGetValue((row.ProjectId, deliverable), out var date) ? date : null)
        }).ToArray();
    }
}

file sealed record DeliverableProject(Guid Id, Guid ProjectId);
// A GroupBy key must be a member-init expression for EF to translate it.
file sealed class SubmissionDateKey { public Guid ProjectId { get; init; } public Guid DeliverableId { get; init; } }
file sealed record SubmissionDateRow(Guid ProjectId, Guid DeliverableId, DateOnly Date);
