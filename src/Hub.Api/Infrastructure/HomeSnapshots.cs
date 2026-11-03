using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// A whole-project capture with the same definitions as Home. Old evaluator Counts
/// cannot supply these facts. AccessBasis is internal invalidation evidence, never a
/// grant of access and never part of a user-facing comparison DTO.
public sealed record HomeMetricCapture(int Version, DateTimeOffset CapturedAt, string OrgTimeZone,
    int DueWindowDays, string ProjectStatus, string ComputedHealth, int OpenTasks, int OverdueTasks,
    int DueTasks, int DueDeliverables, int DueMilestones, string AccessBasis);

public static class HomeSnapshots
{
    public const int CurrentVersion = 1;

    /// Caller holds a consistent read snapshot. Nightly evaluates before opening it,
    /// so this reuses the exact materialized state read by Home rather than another
    /// due/overdue rule. This method does not create a daily row or change legacy Counts.
    public static async Task<HomeMetricCapture> Capture(HubDb db, OrgSettings settings, Guid projectId,
        DateOnly today, DateTimeOffset now, CancellationToken ct = default)
    {
        if (Clock.LocalDate(now, settings) != today)
            throw new ArgumentException("Home capture date must match its capture instant.", nameof(today));
        var project = await db.Projects.AsNoTracking().SingleAsync(p => p.Id == projectId, ct);
        var tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId);
        var open = TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { ["open"] = "true" }), Guid.Empty, today, settings);
        var overdue = TaskQueries.Apply(db, tasks, TaskFilter.Of(new Dictionary<string, string?> { ["overdue"] = "true" }), Guid.Empty, today, settings);
        var end = today.AddDays(settings.HomeDueWindowDays);
        var dueTasks = await tasks.CountAsync(t => t.DueDate >= today && t.DueDate <= end
            && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled, ct);
        var dueDeliverables = await db.Deliverables.CountAsync(d => d.ProjectId == projectId && d.DueDate >= today && d.DueDate <= end
            && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted, ct);
        var dueMilestones = await db.Milestones.CountAsync(m => m.ProjectId == projectId && m.Date >= today && m.Date <= end && !m.IsComplete && !m.IsCancelled, ct);
        var health = await db.ProjectStates.AsNoTracking().Where(p => p.ProjectId == projectId)
            .Select(p => p.ComputedHealth).FirstOrDefaultAsync(ct) ?? Health.Grey;
        var basis = await AccessBases(db, settings, [projectId], ct);
        return new(CurrentVersion, now, settings.OrgTimeZone, settings.HomeDueWindowDays, project.Status, health,
            await open.CountAsync(ct), await overdue.CountAsync(ct), dueTasks, dueDeliverables, dueMilestones, basis[projectId]);
    }

    /// Conservative equality fingerprint of access inputs, not a second permission
    /// matrix. Current reads still use Access.VisibleProjects. Relevant audit IDs
    /// invalidate a toggle-away-and-back even when current values match the capture.
    /// Last sign-in, profile edits and ordinary work activity deliberately do not.
    public static async Task<Dictionary<Guid, string>> AccessBases(HubDb db, OrgSettings settings,
        IEnumerable<Guid> projectIds, CancellationToken ct = default)
    {
        var ids = projectIds.Distinct().Order().ToArray();
        if (ids.Length == 0) return [];
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new { u.Id, u.IsActive }).ToListAsync(ct);
        var roles = await db.UserRoles.AsNoTracking().OrderBy(r => r.UserId).ThenBy(r => r.Role).ThenBy(r => r.Id)
            .Select(r => new { r.Id, r.UserId, r.Role, r.Source, r.GrantedAt }).ToListAsync(ct);
        const string activeChange = "[{\"field\":\"IsActive\"}]";
        var globalEvents = db.ActivityLog.AsNoTracking()
            .Where(a => (a.ItemType == ItemType.User && (a.Action == "RoleAdded" || a.Action == "RoleRemoved"
                || EF.Functions.JsonContains(a.Changes, activeChange)))
                || (a.ItemType == ItemType.Setting && a.ItemKey == "restricted_projects_enabled"));
        var globalAclEvent = await globalEvents
            .OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id)
            .Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        var globalAclEventCount = await globalEvents.CountAsync(ct);
        var global = Hash(new { version = CurrentVersion, settings.RestrictedProjectsEnabled, users, roles, globalAclEvent, globalAclEventCount });
        var projects = await db.Projects.AsNoTracking().Where(p => ids.Contains(p.Id))
            .OrderBy(p => p.Id).Select(p => new { p.Id, p.Visibility, p.ProjectManagerId }).ToListAsync(ct);
        var members = await db.ProjectMembers.AsNoTracking().Where(m => ids.Contains(m.ProjectId) && m.RemovedAt == null)
            .OrderBy(m => m.ProjectId).ThenBy(m => m.UserId).ThenBy(m => m.Id)
            .Select(m => new { m.Id, m.ProjectId, m.UserId, m.Roles, m.PrimaryDisciplineId, m.AddedAt, m.RowVersion }).ToListAsync(ct);
        const string visibilityChange = "[{\"field\":\"Visibility\"}]";
        const string managerChange = "[{\"field\":\"ProjectManagerId\"}]";
        const string leadChange = "[{\"field\":\"LeadUserId\"}]";
        var events = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && ids.Contains(a.ProjectId.Value)
            && (a.ItemType == ItemType.Member
                || (a.ItemType == ItemType.Project && (EF.Functions.JsonContains(a.Changes, visibilityChange) || EF.Functions.JsonContains(a.Changes, managerChange)))
                || (a.ItemType == ItemType.Discipline && EF.Functions.JsonContains(a.Changes, leadChange))))
            .GroupBy(a => a.ProjectId!.Value)
            .Select(g => new { ProjectId = g.Key, Count = g.Count(), EventId = g.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Select(a => a.Id).First() })
            .ToDictionaryAsync(a => a.ProjectId, a => new { a.EventId, a.Count }, ct);
        return projects.ToDictionary(p => p.Id, p => Hash(new
        {
            global, project = p,
            members = members.Where(m => m.ProjectId == p.Id).Select(m => new { m.Id, m.UserId,
                roles = m.Roles.Order(StringComparer.Ordinal).ToArray(), m.PrimaryDisciplineId, m.AddedAt, m.RowVersion }),
            aclEvent = events.GetValueOrDefault(p.Id),
        }));
    }

    static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOpts.Web))));

    /// Require every property explicitly. Deserializing an absent integer as zero
    /// would turn incomplete historic evidence into a fictitious measured count.
    public static HomeMetricCapture? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string[] numberFields = ["version", "dueWindowDays", "openTasks", "overdueTasks", "dueTasks", "dueDeliverables", "dueMilestones"];
            foreach (var field in numberFields)
                if (!root.TryGetProperty(field, out var value) || !value.TryGetInt32(out var n) || n < 0) return null;
            foreach (var field in new[] { "orgTimeZone", "projectStatus", "computedHealth", "accessBasis" })
                if (!root.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString())) return null;
            if (!root.TryGetProperty("capturedAt", out var captured) || !captured.TryGetDateTimeOffset(out var at) || at == default) return null;
            var result = JsonSerializer.Deserialize<HomeMetricCapture>(json, JsonOpts.Web);
            return result is { Version: CurrentVersion, DueWindowDays: <= 366 }
                && ProjectStatus.All.Contains(result.ProjectStatus) && Health.All.Contains(result.ComputedHealth) ? result : null;
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException or ArgumentException) { return null; }
    }
}
