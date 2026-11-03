using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-SCR-10: compare an explicit common, currently visible cohort. No filtered current count is paired with a whole-project historic count.
public static class HomeVisualTrends
{
    public sealed record HomeTrendMetric(string Id, string State, string? Reason, int? Current, int? Previous, int? Delta, string Tone);
    public sealed record HomeTrendProject(string ProjectNumber, DateOnly SnapshotDate, DateTimeOffset CapturedAt);
    public sealed record HomeTrendsRead(DateOnly TargetDate, int CompareDays, DateTimeOffset? CompareFrom, DateTimeOffset? CompareThrough,
        int ExcludedProjects, List<HomeTrendProject> Projects, List<HomeTrendMetric> Metrics);
    sealed record Project(Guid Id, string Number, string Status, string Health);
    sealed record Historic(Guid ProjectId, DateOnly Date, string? Capture);
    static readonly string[] Metrics = ["ActiveProjects", "OpenTasks", "OverdueTasks", "DueWithin", "OnTrack"];

    public static async Task<HomeTrendsRead> Build(HubDb db, IQueryable<Hub.Api.Data.Project> scope, OrgSettings settings, DateOnly today, DateTimeOffset now,
        string? preset, string[] selectedStatuses)
    {
        var target = today.AddDays(-settings.TrendCompareDays);
        var projects = await scope.Select(p => new Project(p.Id, p.ProjectNumber, p.Status,
            db.ProjectStates.Where(s => s.ProjectId == p.Id).Select(s => s.ComputedHealth).FirstOrDefault() ?? Health.Grey)).ToListAsync();
        if (preset is HomePresets.DisciplineLead or HomePresets.Supervisor)
            return Unavailable(target, settings.TrendCompareDays, projects.Count, "FilteredScope");
        if (projects.Count == 0) return Unavailable(target, settings.TrendCompareDays, 0, "NoComparableProjects");
        var ids = projects.Select(p => p.Id).ToArray();
        // Do not skip a newer incompatible/legacy row in order to cherry-pick an older compatible capture.
        var historic = await db.HealthSnapshots.AsNoTracking().Where(s => ids.Contains(s.ProjectId) && s.SnapshotDate <= target
                && !db.HealthSnapshots.Any(later => later.ProjectId == s.ProjectId && later.SnapshotDate <= target && later.SnapshotDate > s.SnapshotDate))
            .Select(s => new Historic(s.ProjectId, s.SnapshotDate, s.HomeMetrics)).ToDictionaryAsync(s => s.ProjectId);
        var bases = await HomeSnapshots.AccessBases(db, settings, ids);
        var cohort = new List<(Project Current, Historic Row, HomeMetricCapture Old)>();
        foreach (var project in projects)
        {
            if (!historic.TryGetValue(project.Id, out var snapshot) || HomeSnapshots.Parse(snapshot.Capture) is not { } old) continue;
            if (old.DueWindowDays != settings.HomeDueWindowDays || old.OrgTimeZone != settings.OrgTimeZone
                || old.CapturedAt > now || DateOnly.FromDateTime(Clock.Local(old.CapturedAt, settings).DateTime) != snapshot.Date
                || !bases.TryGetValue(project.Id, out var basis) || old.AccessBasis != basis
                || (selectedStatuses.Length > 0 && !selectedStatuses.Contains(old.ProjectStatus))) continue;
            cohort.Add((project, snapshot, old));
        }
        if (cohort.Count == 0) return Unavailable(target, settings.TrendCompareDays, projects.Count, "NoComparableProjects");
        var commonIds = cohort.Select(c => c.Current.Id).ToArray();
        var tasks = db.Tasks.AsNoTracking().Where(t => commonIds.Contains(t.ProjectId));
        Task<int> Count(string key) => TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { [key] = "true" }), Guid.Empty, today, settings).CountAsync();
        var open = await Count("open"); var overdue = await Count("overdue"); var end = today.AddDays(settings.HomeDueWindowDays);
        var dueTasks = await tasks.CountAsync(t => t.DueDate >= today && t.DueDate <= end && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled);
        var dueDeliverables = await db.Deliverables.CountAsync(d => commonIds.Contains(d.ProjectId) && d.DueDate >= today && d.DueDate <= end
            && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted);
        var dueMilestones = await db.Milestones.CountAsync(m => commonIds.Contains(m.ProjectId) && m.Date >= today && m.Date <= end && !m.IsComplete && !m.IsCancelled);
        static bool Evaluated(string status, string health) => status == ProjectStatus.Active && health is Health.Green or Health.Yellow or Health.Red;
        var currentEvaluated = cohort.Count(c => Evaluated(c.Current.Status, c.Current.Health));
        var oldEvaluated = cohort.Count(c => Evaluated(c.Old.ProjectStatus, c.Old.ComputedHealth));
        int? currentPct = currentEvaluated == 0 ? null : cohort.Count(c => c.Current.Status == ProjectStatus.Active && c.Current.Health == Health.Green) * 100 / currentEvaluated;
        int? oldPct = oldEvaluated == 0 ? null : cohort.Count(c => c.Old.ProjectStatus == ProjectStatus.Active && c.Old.ComputedHealth == Health.Green) * 100 / oldEvaluated;
        var excluded = projects.Count - cohort.Count;
        return new HomeTrendsRead(target, settings.TrendCompareDays, cohort.Min(c => c.Old.CapturedAt), cohort.Max(c => c.Old.CapturedAt), excluded,
            [.. cohort.OrderBy(c => c.Current.Number, StringComparer.Ordinal).Select(c => new HomeTrendProject(c.Current.Number, c.Row.Date, c.Old.CapturedAt))],
            [Compare("ActiveProjects", cohort.Count(c => c.Current.Status == ProjectStatus.Active), cohort.Count(c => c.Old.ProjectStatus == ProjectStatus.Active)),
             Compare("OpenTasks", open, cohort.Sum(c => c.Old.OpenTasks)),
             Compare("OverdueTasks", overdue, cohort.Sum(c => c.Old.OverdueTasks)),
             Compare("DueWithin", dueTasks + dueDeliverables + dueMilestones, cohort.Sum(c => c.Old.DueTasks + c.Old.DueDeliverables + c.Old.DueMilestones)),
             Compare("OnTrack", currentPct, oldPct)]);
    }

    static HomeTrendsRead Unavailable(DateOnly target, int days, int excluded, string reason) => new(target, days, null, null, excluded, [],
        [.. Metrics.Select(id => new HomeTrendMetric(id, "Unavailable", reason, null, null, null, "Neutral"))]);

    public static HomeTrendMetric Compare(string id, int? current, int? previous)
    {
        if (current == null || previous == null) return new(id, "Unavailable", "NotEvaluated", current, previous, null, "Neutral");
        var delta = current.Value - previous.Value;
        var tone = delta == 0 ? "Neutral" : id == "OverdueTasks" ? delta < 0 ? "Good" : "Bad"
            : id == "OnTrack" ? delta > 0 ? "Good" : "Bad" : "Neutral";
        return new(id, "Comparable", null, current, previous, delta, tone);
    }
}
