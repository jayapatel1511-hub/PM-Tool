using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Api.Records;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Calendar definitions contain filters and position only; recipient scope is always read afresh.
internal static class CalendarViewParameters
{
    public static void Demand(string type, OrgSettings settings)
    {
        if (type == "calendar") Http.RequireFlag(settings, "work_screens");
    }

    public static async Task<Dictionary<string, string>> Write(Guid? projectId, Dictionary<string, string> parameters, Access access, HubDb db)
    {
        if (projectId is { } project)
            Check.That(parameters.GetValueOrDefault("projects") is { } selected && Guid.TryParse(selected, out var id) && id == project
                && !parameters.ContainsKey("ws"), "projects", "error.invalid");
        var (kept, dropped) = await Read(projectId, parameters, access, db);
        foreach (var key in dropped.Where(ScreenLists.SavedViewKeys["calendar"].Contains))
            throw ApiException.Invalid(key, "error.invalid");
        return kept;
    }

    public static async Task<(Dictionary<string, string> Kept, List<string> Dropped)> Read(Guid? projectId, Dictionary<string, string> parameters, Access access, HubDb db)
    {
        var kept = new Dictionary<string, string>();
        var dropped = new List<string>();
        foreach (var (key, raw) in parameters)
        {
            if (!ScreenLists.SavedViewKeys["calendar"].Contains(key)) { dropped.Add(key); continue; }
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var value = raw.Trim();
            var normal = value.Length <= 2000 ? await Normalise(key, value, access, db) : null;
            if (normal is null) dropped.Add(key); else kept[key] = normal;
        }
        if (projectId is { } project)
        {
            // A retained shared definition must never fall back to a broader personal project set.
            if (!kept.TryGetValue("projects", out var selected) || selected != project.ToString()) dropped.Add("projects");
            kept["projects"] = project.ToString();
            if (kept.Remove("ws")) dropped.Add("ws");
        }
        return (kept, dropped.Distinct().ToList());
    }

    static async Task<string?> Normalise(string key, string value, Access access, HubDb db)
    {
        switch (key)
        {
            case "mode": return value is "week" or "month" or "agenda" ? value : null;
            case "date": return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? value : null;
            case "showAway": return value == "true" ? value : null;
            case "hide":
                var types = value.Split(',').Select(token => token.Trim()).ToArray();
                return types.Length > 0 && types.Distinct().Count() == types.Length
                    && types.All(type => type == CalendarEndpoints.Deadlines || CalendarEventType.All.Contains(type))
                    ? string.Join(',', types.Order(StringComparer.Ordinal)) : null;
            case "ws":
                return Guid.TryParse(value, out var workspace) && workspace != Guid.Empty
                    && await db.Workspaces.AnyAsync(w => w.Id == workspace && w.OwnerId == access.Me.Id) ? workspace.ToString() : null;
            case "personId":
                // This ID is a business filter, never an authorization to read that person's absences.
                return Guid.TryParse(value, out var person) && person != Guid.Empty
                    && await db.Users.AnyAsync(u => u.Id == person) ? person.ToString() : null;
            case "disciplineId":
                // Calendar's work/away intersection uses organisation disciplines even in project views.
                return Guid.TryParse(value, out var discipline) && discipline != Guid.Empty
                    && await db.Disciplines.AnyAsync(d => d.Id == discipline) ? discipline.ToString() : null;
            case "projects":
                if (value is "mine" or "all" or "none") return value;
                var tokens = value.Split(',').Select(token => token.Trim()).ToArray();
                var ids = new List<Guid>();
                foreach (var token in tokens)
                {
                    if (!Guid.TryParse(token, out var project) || project == Guid.Empty || ids.Contains(project)) return null;
                    ids.Add(project);
                }
                return ids.Count > 0 && await access.VisibleProjects().CountAsync(p => ids.Contains(p.Id)) == ids.Count
                    ? string.Join(',', ids.Select(id => id.ToString()).Order(StringComparer.Ordinal)) : null;
            default: return null;
        }
    }
}
