using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record WorkInputAffectedTask(Guid Id, string Key, string Name);
public sealed record WorkInputEffect(string Kind, Guid Id, IReadOnlyList<WorkInputAffectedTask> OtherOpenTasks);
public sealed record WorkInputRevision(Guid Id, string Key, string Title, string Revision, DateTimeOffset RegisteredAt, DateTimeOffset? SourceCheckedAt);
public sealed record WorkInputAssessment(Guid Id, Guid NoticeId, string Key, string Status, string NoticeStatus);
public sealed record WorkInputSourceUse(Guid Id, string IntendedUse, WorkInputRevision? RevisionUsed, WorkInputRevision? CurrentRegisteredRevision,
    bool AssessmentAvailable, IReadOnlyList<WorkInputAssessment> Assessments);

/// Read-only S03 facts. Resolving actors/actions stays exclusively in RecordContextReader/Rules.
/// Effects count the existing materialized blocker references; this does not rejudge dependencies/readiness.
public static class WorkInputPatterns
{
    public static async Task<IReadOnlyList<WorkInputEffect>> Effects(HubDb db, Guid projectId, Guid taskId, RecordContextView context)
    {
        var relevant = context.Inputs.Select(x => (x.Kind, x.Id)).ToHashSet();
        if (relevant.Count == 0) return [];
        var tasks = await db.TaskStates.AsNoTracking().Where(s => s.ProjectId == projectId && (s.IsWaiting || s.IsBlocked))
            .Join(db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && t.Id != taskId
                && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled),
                s => s.TaskId, t => t.Id, (s, t) => new BlockedTask { Id = t.Id, Key = t.Key, Name = t.Name, BlockedBy = s.BlockedBy }).ToListAsync();
        var groups = relevant.ToDictionary(x => x, _ => new List<WorkInputAffectedTask>());
        foreach (var task in tasks)
            foreach (var identity in References(task.BlockedBy).Where(relevant.Contains))
                groups[identity].Add(new(task.Id, task.Key, task.Name));
        return relevant.OrderBy(x => x.Kind, StringComparer.Ordinal).ThenBy(x => x.Id)
            .Select(identity => new WorkInputEffect(identity.Kind, identity.Id,
                groups[identity].DistinctBy(x => x.Id).OrderBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray())).ToArray();
    }

    // Identity extraction only: use every actual input of Waiting or Blocked tasks,
    // including a waiting input whose escalation flag `blocking` is still false.
    // No stale JSON title, owner, date or state becomes a current record fact.
    static IEnumerable<(string Kind, Guid Id)> References(string json)
    {
        JsonElement[] rows;
        try { rows = JsonSerializer.Deserialize<JsonElement[]>(json, JsonOpts.Web) ?? []; }
        catch (JsonException) { return []; }
        var result = new HashSet<(string, Guid)>();
        foreach (var row in rows)
        {
            if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String
                || !row.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.String || !Guid.TryParse(id.GetString(), out var guid)) continue;
            var kind = type.GetString()?.ToLowerInvariant() switch
            {
                "task" => RecordContextKind.Task, "decision" => RecordContextKind.Decision,
                "handoff" => RecordContextKind.Handoff, "constraint" or "workconstraint" => RecordContextKind.WorkConstraint,
                _ => null,
            };
            if (kind is not null) result.Add((kind, guid));
        }
        return result;
    }

    public static async Task<IReadOnlyList<WorkInputSourceUse>> Sources(HubDb db, Guid projectId, Guid taskId)
    {
        // A Source Use is independent of task state. Not Started work is included.
        var uses = await db.InputUses.AsNoTracking().Where(u => u.ProjectId == projectId && u.TargetType == ItemType.Task && u.TargetId == taskId)
            .OrderBy(u => u.SourceIdentity).ThenBy(u => u.Id).ToListAsync();
        if (uses.Count == 0) return [];
        var identities = uses.Select(u => u.SourceIdentity).Distinct().ToArray();
        var heads = await db.SourceHeads.AsNoTracking().Where(h => h.ProjectId == projectId && identities.Contains(h.Identity)).ToDictionaryAsync(h => h.Identity);
        var revisionIds = uses.Select(u => u.SourceRevisionId).Concat(heads.Values.Select(h => h.CurrentRevisionId)).Distinct().ToArray();
        var revisions = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == projectId && revisionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id);
        var currentIds = heads.Values.Select(h => h.CurrentRevisionId).Distinct().ToArray();
        var assessments = await db.ChangeAssessments.AsNoTracking().Where(a => a.ProjectId == projectId && a.TargetType == ItemType.Task && a.TargetId == taskId)
            .Join(db.ChangeNotices.AsNoTracking().Where(n => n.ProjectId == projectId && currentIds.Contains(n.NewRevisionId) && n.Status != ChangeStatus.Draft),
                a => a.ChangeNoticeId, n => n.Id, (a, n) => new NoticeAssessment { Assessment = a, Notice = n }).ToListAsync();
        WorkInputRevision? Revision(Guid id, string identity) => revisions.TryGetValue(id, out var r) && Coordination.Identity(r) == identity
            ? new(r.Id, r.SourceKey, r.Title, r.Revision, r.CreatedAt, r.SourceCheckedAt) : null;
        return uses.Select(use =>
        {
            var used = Revision(use.SourceRevisionId, use.SourceIdentity);
            var current = heads.TryGetValue(use.SourceIdentity, out var head) ? Revision(head.CurrentRevisionId, use.SourceIdentity) : null;
            var available = used is not null && current is not null;
            var matches = available ? assessments.Where(x => x.Notice.NewRevisionId == current!.Id
                    && (x.Assessment.InputUseId is null || x.Assessment.InputUseId == use.Id))
                .OrderBy(x => x.Notice.Key, StringComparer.Ordinal).ThenBy(x => x.Assessment.Id)
                .Select(x => new WorkInputAssessment(x.Assessment.Id, x.Notice.Id, x.Notice.Key, x.Assessment.Status, x.Notice.Status)).ToArray() : [];
            return new WorkInputSourceUse(use.Id, use.IntendedUse, used, current, available, matches);
        }).OrderBy(x => x.RevisionUsed?.Key ?? x.CurrentRegisteredRevision?.Key ?? "", StringComparer.Ordinal).ThenBy(x => x.Id).ToArray();
    }
}

// Join result selectors stay member-init projections for EF.
file sealed class BlockedTask { public Guid Id { get; init; } public string Key { get; init; } = ""; public string Name { get; init; } = ""; public string BlockedBy { get; init; } = ""; }
file sealed class NoticeAssessment { public ChangeAssessment Assessment { get; init; } = null!; public ChangeNotice Notice { get; init; } = null!; }
