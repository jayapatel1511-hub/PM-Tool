using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Api.Features;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Hub.Api.Records;

public sealed record RegistryEntry(Type ClrType, string ItemType, IReadOnlyList<string> Fields, bool HasProjectLookup, string? NoProjectReason)
{
    public Func<HubDb, Guid, Task<Guid?>>? Project { get; init; }
    public int NameOrder { get; init; }
    public Func<HubDb, List<Guid>, Task<IEnumerable<(Guid Id, string Name)>>>? Names { get; init; }
    public int ExportNameOrder { get; init; }
    public Func<HubDb, Access, List<Guid>, Task<IEnumerable<(Guid Id, string Name)>>>? ExportNames { get; init; }
}

/// Reflection is confined here; every audited type declares itself in its own file (§44.2).
public static class EntityRegistry
{
    public static IReadOnlyList<RegistryEntry> Entries { get; } = typeof(EntityRegistry).Assembly.GetTypes()
        .Where(t => t.Namespace == typeof(EntityRegistry).Namespace)
        .Select(t => t.GetProperty("Entry", BindingFlags.Public | BindingFlags.Static))
        .Where(p => p?.PropertyType == typeof(RegistryEntry)).Select(p => (RegistryEntry)p!.GetValue(null)!)
        .OrderBy(e => e.ClrType.Name, StringComparer.Ordinal).ToArray();
    public static RegistryEntry For(Type type) => Entries.Single(x => x.ClrType == type);
    public static RegistryEntry? History(string itemType) => Entries.SingleOrDefault(x => x.ItemType == itemType && x.Project is not null);
    public static async Task<Dictionary<Guid, string>> Names(HubDb db, HashSet<Guid> ids, Access? access = null)
    {
        var names = new Dictionary<Guid, string>();
        if (ids.Count == 0) return names;
        var list = ids.ToList();
        foreach (var entry in Entries.OrderBy(e => access is null ? e.NameOrder : e.ExportNameOrder))
        {
            var rows = access is null ? (entry.Names is null ? [] : await entry.Names(db, list))
                : (entry.ExportNames is null ? [] : await entry.ExportNames(db, access, list));
            foreach (var (id, name) in rows) names.TryAdd(id, name);
        }
        return names;
    }
}
