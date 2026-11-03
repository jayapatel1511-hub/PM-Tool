using System.Globalization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Saved report definitions use report dimensions, independently of their sharing container.
internal static class ReportViewParameters
{
    public static Task<(Dictionary<string, string> Kept, List<string> Dropped)> Read(ReportEndpoints.ReportSavedViewDefinition definition,
        Dictionary<string, string> parameters, Access access, HubDb db) => Clean(definition, parameters, access, db, false);

    public static async Task<Dictionary<string, string>> Write(ReportEndpoints.ReportSavedViewDefinition definition,
        Dictionary<string, string> parameters, Access access, HubDb db)
        => (await Clean(definition, parameters, access, db, true)).Kept;

    static async Task<(Dictionary<string, string> Kept, List<string> Dropped)> Clean(ReportEndpoints.ReportSavedViewDefinition definition,
        Dictionary<string, string> parameters, Access access, HubDb db, bool writing)
    {
        var supported = definition.Parameters.ToDictionary(parameter => parameter.Key, StringComparer.Ordinal);
        var registered = ReportEndpoints.SavedViewParameterKeys[$"report:{definition.Code}"];
        var kept = new Dictionary<string, string>(); var dropped = new List<string>(); var invalid = new HashSet<string>();
        foreach (var (key, raw) in parameters)
        {
            if (!supported.TryGetValue(key, out var parameter))
            {
                dropped.Add(key);
                // UI state is ignored; a currently disabled data dimension must not silently become a wider scope.
                if (registered.Contains(key)) invalid.Add(key);
                continue;
            }
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var normalized = await Value(definition.Code, parameter, raw.Trim(), access, db);
            if (normalized is null) { invalid.Add(key); dropped.Add(key); }
            else kept[key] = normalized;
        }
        if (kept.TryGetValue("history", out var history) && history == "all" && (kept.ContainsKey("from") || kept.ContainsKey("to")))
            foreach (var key in new[] { "history", "from", "to" }.Where(kept.ContainsKey)) invalid.Add(key);
        if (kept.TryGetValue("from", out var from) && kept.TryGetValue("to", out var to))
        {
            var first = DateOnly.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var last = DateOnly.ParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            if (last < first || definition.Code == "task-hours" && last.DayNumber - first.DayNumber > 370)
            { invalid.Add("from"); invalid.Add("to"); }
        }
        if (definition.Code == "allocations-by-source" && kept.GetValueOrDefault("show") == "entries-without-project")
        {
            // This form is a dated entry list with an h/week rate, not a period matrix.
            foreach (var (key, defaultValue) in new[] { ("unit", "hours"), ("period", "weeks") })
                if (kept.TryGetValue(key, out var value))
                {
                    if (value != defaultValue) invalid.Add(key);
                    else kept.Remove(key); // default-only selectors have no meaning in its saved data map
                }
        }
        if (writing) Check.That(invalid.Count == 0, "params", "view.report_params");
        foreach (var key in invalid) { kept.Remove(key); if (!dropped.Contains(key)) dropped.Add(key); }
        return (kept, dropped);
    }

    static async Task<string?> Value(string code, ReportEndpoints.Param parameter, string value, Access access, HubDb db)
    {
        if (value.Length > 2000) return null;
        var key = parameter.Key;
        if (key == "unitId")
        {
            if (!Guid.TryParse(value, out var unit)) return null;
            return unit == Guid.Empty || await db.Set<OrgUnit>().AnyAsync(row => row.Id == unit) ? unit.ToString() : null;
        }
        if (parameter.Type is "project" or "projects" or "person" or "office" or "discipline")
        {
            var tokens = value.Split(','); var ids = new List<Guid>();
            foreach (var token in tokens) { if (!Guid.TryParse(token.Trim(), out var id) || id == Guid.Empty) return null; ids.Add(id); }
            ids = ids.Distinct().ToList();
            if (ids.Count == 0 || parameter.Type != "projects" && ids.Count != 1) return null;
            var found = parameter.Type switch
            {
                "project" or "projects" => await access.VisibleProjects().CountAsync(row => ids.Contains(row.Id)),
                "person" => await db.Users.CountAsync(row => ids.Contains(row.Id) && row.IsActive),
                "office" => await db.Offices.CountAsync(row => ids.Contains(row.Id) && row.IsActive),
                "discipline" => await db.Disciplines.CountAsync(row => ids.Contains(row.Id) && row.IsActive),
                _ => 0,
            };
            return found == ids.Count ? string.Join(',', ids) : null;
        }
        if (parameter.Type == "date")
            return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
        if (parameter.Type == "bool") return value is "true" or "false" ? value : null;
        if (parameter.Type == "select")
        {
            if (parameter.Options?.Contains(value) != true) return null;
            if (code == "staff-assignments" && key == "scope" && value == "all" && !Permissions.ViewAllStaff(access.Actor)) return null;
            return value;
        }
        if (parameter.Type == "number")
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return null;
            if (key == "weeks")
            {
                if (code == "pilot-measures") number = Math.Clamp(number, 1, 26);
                else if (number is < 1 or > 26) return null;
            }
            else if (key == "daysAhead") number = Math.Clamp(number, 0, 365);
            else if (key is "days" or "minDaysOverdue") number = Math.Max(1, number);
            else return null;
            return number.ToString(CultureInfo.InvariantCulture);
        }
        return null;
    }
}
