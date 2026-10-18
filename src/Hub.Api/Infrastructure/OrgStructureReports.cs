using System.Text.Json;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Past reports use the recorded placement, including the old value when the first change follows the requested week.
public static class OrgStructureReports
{
    public sealed record PlacementChange(Guid PersonId, Guid? UnitId, DateTimeOffset At);
    public static Guid? UnitAt(IEnumerable<PlacementChange> changes, Guid personId, DateTimeOffset end) =>
        changes.Where(x => x.PersonId == personId && x.At <= end).OrderByDescending(x => x.At).Select(x => x.UnitId).FirstOrDefault();

    public static async Task<Dictionary<Guid, Guid?>> At(HubDb db, Guid[] people, DateTimeOffset end)
    {
        var current = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.UnitId);
        var rows = await db.ActivityLog.AsNoTracking().Where(a => a.ItemType == "User" && a.ItemId != null && people.Contains(a.ItemId.Value))
            .OrderBy(a => a.OccurredAt).ThenBy(a => a.Id).Select(a => new { a.ItemId, a.OccurredAt, a.Changes }).ToListAsync();
        foreach (var person in people)
        {
            var changes = rows.Where(r => r.ItemId == person).SelectMany(r => JsonDocument.Parse(r.Changes).RootElement.EnumerateArray()
                .Where(c => c.TryGetProperty("field", out var field) && field.GetString() == "UnitId")
                .Select(c => new { r.OccurredAt, Old = c.GetProperty("old").ValueKind == JsonValueKind.Null ? (Guid?)null : c.GetProperty("old").GetGuid(),
                    New = c.GetProperty("new").ValueKind == JsonValueKind.Null ? (Guid?)null : c.GetProperty("new").GetGuid() })).ToArray();
            var before = changes.LastOrDefault(c => c.OccurredAt < end);
            var after = changes.FirstOrDefault(c => c.OccurredAt >= end);
            if (before != null) current[person] = before.New;
            else if (after != null) current[person] = after.Old;
        }
        return current;
    }

    public static string UnitPath(Guid? unit, IReadOnlyDictionary<Guid, OrgUnit> units)
    {
        var names = new List<string>(); var seen = new HashSet<Guid>();
        while (unit is { } id && units.TryGetValue(id, out var value) && seen.Add(id)) { names.Add(value.Name); unit = value.ParentId; }
        names.Reverse(); return string.Join(" / ", names);
    }
}
