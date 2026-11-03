using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-SCR-13: evidence-backed presentation only. No dates, statuses, baselines or review rounds are written.
public static class HomeVisualDeadlineNotes
{
    public sealed record Evidence(Guid Id, DateTimeOffset At, string Action, string[] Categories, string Changes);
    sealed record Transition(string? From, string To);
    sealed record Activity(Guid Id, Guid? ProjectId, string Type, Guid? ItemId, DateTimeOffset At, string Action, List<string> Categories, string Changes);
    sealed record Baseline(Guid ProjectId, Guid Id, DateOnly? Date);

    public static async Task<WorkspaceEndpoints.HomeDeadlines> Apply(HubDb db, WorkspaceEndpoints.HomeDeadlines read, OrgSettings settings, DateOnly today, DateTimeOffset now)
    {
        if (read.Items.Count == 0) return read;
        var reviewed = read.Items.Where(x => IsReview(x.Type, x.Status)).ToArray();
        var reviewIds = reviewed.Select(x => x.Id).Distinct().ToArray();
        var projectIds = read.Items.Select(x => x.ProjectId).Distinct().ToArray();
        List<Activity> activity = reviewIds.Length == 0 ? [] : await db.ActivityLog.AsNoTracking()
            .Where(a => a.ItemId != null && reviewIds.Contains(a.ItemId.Value) && a.ProjectId != null && projectIds.Contains(a.ProjectId.Value)
                && (a.ItemType == ItemType.Task || a.ItemType == ItemType.Deliverable))
            .Select(a => new Activity(a.Id, a.ProjectId, a.ItemType, a.ItemId, a.OccurredAt, a.Action, a.Categories, a.Changes)).ToListAsync();
        var byRecord = activity.ToLookup(a => (a.ProjectId, a.Type, a.ItemId));
        var baselines = new Dictionary<(Guid Project, string Type, Guid Id), DateOnly?>();
        if (settings.IsFlagOn("schedule_baseline"))
        {
            var deliverables = read.Items.Where(x => x.Type == ItemType.Deliverable).Select(x => x.Id).ToArray();
            foreach (var row in await db.Deliverables.AsNoTracking().Where(d => projectIds.Contains(d.ProjectId) && deliverables.Contains(d.Id))
                         .Select(d => new Baseline(d.ProjectId, d.Id, d.BaselineDueDate)).ToListAsync())
                baselines[(row.ProjectId, ItemType.Deliverable, row.Id)] = row.Date;
            var milestones = read.Items.Where(x => x.Type == ItemType.Milestone).Select(x => x.Id).ToArray();
            foreach (var row in await db.Milestones.AsNoTracking().Where(m => projectIds.Contains(m.ProjectId) && milestones.Contains(m.Id))
                         .Select(m => new Baseline(m.ProjectId, m.Id, m.BaselineDate)).ToListAsync())
                baselines[(row.ProjectId, ItemType.Milestone, row.Id)] = row.Date;
        }
        return read with { Items = read.Items.Select(item =>
        {
            WorkspaceEndpoints.HomeDeadlineNote? note = null;
            if (IsReview(item.Type, item.Status))
            {
                var evidence = byRecord[(item.ProjectId, item.Type, item.Id)].Select(a => new Evidence(a.Id, a.At, a.Action, a.Categories.ToArray(), a.Changes));
                var entered = ReviewEntered(item.Status!, evidence, now);
                if (entered is { } at)
                {
                    var days = today.DayNumber - DateOnly.FromDateTime(Clock.Local(at, settings).DateTime).DayNumber;
                    if (days >= 0) note = new("InReview", days);
                }
                // An unknown review age does not become a baseline note or an invented zero-day review.
            }
            else if (baselines.TryGetValue((item.ProjectId, item.Type, item.Id), out var baseline))
            {
                var slip = Evaluator.SinceBaseline(item.Date, baseline);
                if (slip is > 0 && Evaluator.SlipShown(null, item.Date, null, baseline, settings.SlipDisplayDays))
                    note = new("LaterThanBaseline", slip.Value);
            }
            return item with { Note = note };
        }).ToList() };
    }

    static bool IsReview(string type, string? status) => type == ItemType.Task ? status is TaskStatuses.ReadyForReview or TaskStatuses.InReview
        : type == ItemType.Deliverable && status == DeliverableStatus.InReview;

    /// Follow the current continuous review episode backwards. Missing, malformed, disconnected or ambiguous evidence
    /// gives no age. Old review rounds cannot extend the age after a revision/restart.
    public static DateTimeOffset? ReviewEntered(string currentStatus, IEnumerable<Evidence> source, DateTimeOffset now)
    {
        var expected = currentStatus;
        foreach (var batch in source.GroupBy(e => e.At).OrderByDescending(g => g.Key))
        {
            var transitions = new List<Transition>();
            foreach (var evidence in batch)
            {
                var statusEvent = evidence.Categories.Contains("status") || evidence.Action == "StatusChanged";
                var statusSeen = false;
                try
                {
                    using var doc = JsonDocument.Parse(evidence.Changes);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) { if (statusEvent) return null; continue; }
                    foreach (var change in doc.RootElement.EnumerateArray())
                    {
                        if (change.ValueKind != JsonValueKind.Object || !change.TryGetProperty("field", out var field)
                            || field.ValueKind != JsonValueKind.String || !string.Equals(field.GetString(), "Status", StringComparison.OrdinalIgnoreCase)) continue;
                        statusSeen = true;
                        if (!change.TryGetProperty("new", out var to) || to.ValueKind != JsonValueKind.String) return null;
                        string? before = null;
                        if (change.TryGetProperty("old", out var old) && old.ValueKind != JsonValueKind.Null)
                        {
                            if (old.ValueKind != JsonValueKind.String) return null;
                            before = old.GetString();
                        }
                        if (before != to.GetString()) transitions.Add(new(before, to.GetString()!));
                    }
                    if (statusEvent && !statusSeen) return null;
                }
                catch (JsonException) { if (statusEvent) return null; }
            }
            if (transitions.Count == 0) continue;
            var unique = transitions.Distinct().ToArray();
            if (batch.Key > now || unique.Length != 1) return null;
            var transition = unique[0];
            if (transition.To != expected || !TaskStatuses.IsReview(transition.To)) return null;
            if (transition.From == null || !TaskStatuses.IsReview(transition.From)) return batch.Key;
            expected = transition.From;
        }
        return null;
    }
}
