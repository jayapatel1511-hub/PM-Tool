using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Batched evidence for exactly the displayed, authorized coordination rows in the caller's read snapshot.
public static class WorkCoordinationReadMetadata
{
    public static async Task Apply(HubDb db, Access access, OrgSettings settings, WorkSeenContext context,
        IReadOnlyCollection<DisciplineCoordinationEndpoints.CoordinationProjection> projections)
    {
        if (!settings.IsFlagOn("work_screens")) return;
        var rows = new List<(IWorkScreenResponse Row, WorkSeenKey Key)>();
        void Add<T>(IEnumerable<T> selected, Func<T, WorkSeenKey> key) where T : IWorkScreenResponse =>
            rows.AddRange(selected.Select(row => ((IWorkScreenResponse)row, key(row))));
        foreach (var projection in projections)
        {
            Add(projection.Handoffs.Concat(projection.Outgoing).Concat(projection.Incoming), row => new("Handoff", row.Id));
            Add(projection.BlockerGroups, row => new("Handoff", row.HandoffId));
            Add(projection.Changes, row => new("ChangeNotice", row.Id));
            Add(projection.Reviews, row => new("ReviewPackage", row.Id));
            Add(projection.LinkedIssues, row => new("Issue", row.Id));
            // A vanished target stays unavailable; its historical activity must not become a substitute for the missing work.
            Add(projection.Uses.Where(row => row.TargetKey is not null), row => new(row.TargetType, row.TargetId));
            Add(projection.LinkedActions, row => new("Action", row.Id));
            Add(projection.UpcomingSubmissions, row => new("SubmissionPackage", row.Id));
            Add(projection.StaffingConflicts, row => new("WorkConstraint", row.Id));
            Add(projection.Startability, row => new(row.TargetType, row.TargetId));
        }
        var facts = await WorkScreenReads.ReadAuthorisedAsync(db, access, settings, context,
            rows.Select(row => row.Key).Distinct().ToArray());
        foreach (var (row, key) in rows) WorkScreenResponseMetadata.Attach(row, key.ItemType, key.ItemId, facts);
    }
}
