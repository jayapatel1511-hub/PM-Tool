using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Api.Records;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Timeline-only saved definitions. These values never alter the canonical scheduling reads.
internal static class TimelineViewParameters
{
    static readonly string[] Statuses = ["Not Started", "In Progress", "In Review", "Revision Required", "Ready to Issue", "Issued", "Accepted", "On Hold"];
    static readonly string[] CommonLayers = ["dependencies", "milestones", "originalDates"];
    public static bool Owns(string type) => type is "timeline" or "workspace-timeline";

    public static void Demand(string type, Guid? projectId, string? scope, OrgSettings settings)
    {
        if (!Owns(type)) return;
        Http.RequireFlag(settings, "work_screens");
        if (type == "timeline") Check.That(projectId != null, "projectId", "view.project_needed");
        else Check.That(projectId == null && (scope == null || scope == "Personal"), "scope", "view.personal_only");
    }

    public static async Task<Dictionary<string, string>> Write(string type, Guid? projectId, Dictionary<string, string> parameters, Access access, HubDb db, OrgSettings settings)
    {
        var (kept, dropped) = await Read(type, projectId, parameters, access, db, settings);
        // Unknown/transient keys follow the existing view policy; meaningful invalid known values refuse writes.
        foreach (var key in dropped.Where(ScreenLists.SavedViewKeys[type].Contains))
            throw ApiException.Invalid(key, "error.invalid");
        return kept;
    }

    public static async Task<(Dictionary<string, string> Kept, List<string> Dropped)> Read(string type, Guid? projectId, Dictionary<string, string> parameters, Access access, HubDb db, OrgSettings settings)
    {
        var known = ScreenLists.SavedViewKeys[type];
        var kept = new Dictionary<string, string>();
        var dropped = new List<string>();
        foreach (var (key, raw) in parameters)
        {
            if (!known.Contains(key)) { dropped.Add(key); continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var value = raw.Trim();
            var normal = value.Length <= 2000 ? await Normalise(type, projectId, key, value, access, db, settings) : null;
            if (normal is null) dropped.Add(key); else kept[key] = normal;
        }
        if (kept.TryGetValue("from", out var from) && kept.TryGetValue("to", out var to) && string.CompareOrdinal(from, to) > 0)
        {
            kept.Remove("from"); kept.Remove("to"); dropped.Add("from"); dropped.Add("to");
        }
        return (kept, dropped);
    }

    static async Task<string?> Normalise(string type, Guid? projectId, string key, string value, Access access, HubDb db, OrgSettings settings)
    {
        var project = type == "timeline";
        var chain = project && settings.IsFlagOn("timeline_handoffs_chain");
        switch (key)
        {
            case "zoom": return value is "week" or "month" or "quarter" ? value : null;
            case "hideCompleted": return value is "true" or "false" ? value : null;
            case "group": return value == (project ? "discipline" : "project") ? value : null;
            case "cols": return value == "items,dates" ? value : null;
            case "from": case "to": case "start":
                return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? value : null;
            case "status": return Statuses.Contains(value) ? value : null;
            case "layers":
                if (value == "none") return value;
                var layers = Tokens(value);
                return layers is { Length: > 0 } && layers.All(layer => CommonLayers.Contains(layer)
                    || settings.IsFlagOn("schedule_baseline") && layer == "baselineDates" || chain && layer == "handoffs") ? Canonical(layers) : null;
            case "disciplineId":
                // The existing Timeline includes retained work in deactivated project disciplines.
                return await Single(value, id => db.ProjectDisciplines.AnyAsync(d => d.Id == id && d.ProjectId == projectId));
            case "milestoneId":
                return await Single(value, id => db.Milestones.AnyAsync(m => m.Id == id && m.ProjectId == projectId && !m.IsCancelled));
            case "chainTo":
                return chain ? await Single(value, id => db.Milestones.AnyAsync(m => m.Id == id && m.ProjectId == projectId && !m.IsCancelled)) : null;
            case "ws":
                return await Single(value, id => db.Workspaces.AnyAsync(w => w.Id == id && w.OwnerId == access.Me.Id));
            case "projects":
                if (value is "mine" or "all" or "none") return value;
                var projects = Ids(value);
                return projects is not null && await access.VisibleProjects().CountAsync(p => projects.Contains(p.Id)) == projects.Length ? Canonical(projects.Select(id => id.ToString())) : null;
            case "collapsed":
                if (value == "none" || !project && value == "all") return value;
                var closed = Tokens(value);
                if (closed is not { Length: > 0 }) return null;
                foreach (var token in closed)
                {
                    if (project && token == "handoffs" && chain) continue;
                    if (!Guid.TryParse(token, out var id) || id == Guid.Empty) return null;
                    var exists = project ? await db.ProjectDisciplines.AnyAsync(d => d.Id == id && d.ProjectId == projectId)
                        : await access.VisibleProjects().AnyAsync(p => p.Id == id);
                    if (!exists) return null;
                }
                return Canonical(closed.Select(token => Guid.TryParse(token, out var id) ? id.ToString() : token));
            case "expanded":
                if (value == "none") return value;
                var expanded = Tokens(value);
                if (expanded is not { Length: > 0 }) return null;
                var normal = new List<string>();
                foreach (var token in expanded)
                {
                    var loose = token.StartsWith("loose:", StringComparison.Ordinal);
                    if (!Guid.TryParse(loose ? token[6..] : token, out var id) || id == Guid.Empty) return null;
                    var exists = loose ? await db.ProjectDisciplines.AnyAsync(d => d.Id == id && d.ProjectId == projectId)
                        : await db.Deliverables.AnyAsync(d => d.Id == id && d.ProjectId == projectId);
                    if (!exists) return null;
                    normal.Add((loose ? "loose:" : "") + id);
                }
                return Canonical(normal);
            default: return null;
        }
    }

    static string[]? Tokens(string value)
    {
        var tokens = value.Split(',').Select(token => token.Trim()).ToArray();
        return tokens.Any(string.IsNullOrEmpty) ? null : tokens.Distinct(StringComparer.Ordinal).ToArray();
    }
    static Guid[]? Ids(string value)
    {
        var tokens = Tokens(value);
        if (tokens is not { Length: > 0 }) return null;
        var ids = new List<Guid>();
        foreach (var token in tokens) { if (!Guid.TryParse(token, out var id) || id == Guid.Empty) return null; ids.Add(id); }
        return ids.Distinct().ToArray();
    }
    static string Canonical(IEnumerable<string> values) => string.Join(',', values.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    static async Task<string?> Single(string value, Func<Guid, Task<bool>> exists) => Guid.TryParse(value, out var id) && id != Guid.Empty && await exists(id) ? id.ToString() : null;
}
