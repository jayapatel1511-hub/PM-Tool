namespace Hub.Domain;

/// Pure organisation-tree rules. The API owns persistence and authorization; these rules stay easy to test.
public static class OrgTree
{
    public sealed record Unit(Guid Id, Guid? ParentId, int LevelOrder, Guid? LeaderId);
    public sealed record Person(Guid Id, Guid? SupervisorId, Guid? UnitId, bool Active = true);

    public static bool CreatesCycle(Guid unitId, Guid? parentId, IReadOnlyDictionary<Guid, Guid?> parents)
    {
        var seen = new HashSet<Guid> { unitId };
        for (var current = parentId; current is { } id; current = parents.GetValueOrDefault(id))
            if (!seen.Add(id)) return true;
        return false;
    }

    public static bool IsLevelOrderValid(int childLevelOrder, int parentLevelOrder) => childLevelOrder > parentLevelOrder;

    public static IReadOnlySet<Guid> Descendants(Guid rootId, IEnumerable<Unit> units)
    {
        var children = new Dictionary<Guid, List<Guid>>();
        foreach (var unit in units)
            if (unit.ParentId is { } parent)
            {
                if (!children.TryGetValue(parent, out var list)) children[parent] = list = [];
                list.Add(unit.Id);
            }
        var result = new HashSet<Guid>();
        var pending = new Stack<Guid>();
        pending.Push(rootId);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!result.Add(id)) continue;
            foreach (var child in children.GetValueOrDefault(id, [])) pending.Push(child);
        }
        return result;
    }

    /// Returns read scope only. A malformed manager cycle contributes no indirect people.
    public static IReadOnlySet<Guid> PeopleScope(Person viewer, IEnumerable<Person> people, IEnumerable<Unit> units,
        bool includeIndirectReports, bool allStaff = false)
    {
        var active = people.Where(x => x.Active).ToArray();
        if (allStaff) return active.Select(x => x.Id).ToHashSet();
        var result = active.Where(x => x.SupervisorId == viewer.Id).Select(x => x.Id).ToHashSet();
        var managers = active.ToDictionary(x => x.Id, x => x.SupervisorId);
        if (includeIndirectReports && !CreatesCycle(viewer.Id, viewer.SupervisorId, managers))
        {
            var bySupervisor = active.Where(x => x.SupervisorId is not null).GroupBy(x => x.SupervisorId!.Value)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToArray());
            var pending = new Stack<Guid>(result);
            var seen = new HashSet<Guid> { viewer.Id };
            while (pending.Count > 0)
            {
                var id = pending.Pop();
                if (!seen.Add(id) || CreatesCycle(id, managers.GetValueOrDefault(id), managers)) continue;
                foreach (var child in bySupervisor.GetValueOrDefault(id, []))
                {
                    // A cycle is malformed data: do not grant an extra hop through it.
                    if (child == viewer.Id || seen.Contains(child)) continue;
                    result.Add(child); pending.Push(child);
                }
            }
        }
        var led = units.Where(x => x.LeaderId == viewer.Id).Select(x => x.Id).ToArray();
        foreach (var root in led)
        {
            var ids = Descendants(root, units);
            result.UnionWith(active.Where(x => x.UnitId is { } unit && ids.Contains(unit)).Select(x => x.Id));
        }
        return result;
    }
}
