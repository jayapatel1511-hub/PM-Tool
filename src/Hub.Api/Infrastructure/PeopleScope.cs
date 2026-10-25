using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Read-only people scope used by staff, workload and planning queries. Write authorization remains in Permissions.
public static class PeopleScope
{
    public static async Task<IQueryable<AppUser>> Widen(HubDb db, Actor actor, IQueryable<AppUser> baseline)
    {
        if (!await db.Settings.AnyAsync(s => s.Key == "flag.org_structure" && s.Value == "true") || actor.Admin || actor.Executive) return baseline;
        if (!actor.Supervisor && !await LeadsUnit(db, actor)) return baseline;
        var indirect = await db.Settings.AnyAsync(s => s.Key == "include_indirect_reports" && s.Value == "true");
        var scope = await Load(db, actor, indirect);
        return baseline.Union(db.Users.AsNoTracking().Where(u => scope.Contains(u.Id)));
    }

    public static async Task<IQueryable<AppUser>> FilterUnit(HubDb db, IQueryable<AppUser> people, Guid? unitId)
    {
        if (unitId is not { } id || !await db.Settings.AnyAsync(s => s.Key == "flag.org_structure" && s.Value == "true")) return people;
        if (id == Guid.Empty) return people.Where(u => u.UnitId == null);
        var tree = await db.Set<OrgUnit>().AsNoTracking().Select(u => new OrgTree.Unit(u.Id, u.ParentId, 0, u.LeaderId)).ToListAsync();
        var units = OrgTree.Descendants(id, tree);
        return people.Where(u => u.UnitId != null && units.Contains(u.UnitId.Value));
    }

    public static async Task DemandWorkload(HubDb db, Actor actor)
    {
        if (Permissions.ViewWorkload(actor).Ok) return;
        Access.Demand(await db.Settings.AnyAsync(s => s.Key == "flag.org_structure" && s.Value == "true") && await LeadsUnit(db, actor)
            ? Allow.Yes : Permissions.ViewWorkload(actor));
    }

    public static Task<bool> LeadsUnit(HubDb db, Actor actor) => db.Set<OrgUnit>().AnyAsync(u => u.LeaderId == actor.Id && u.ClosedOn == null);

    public static async Task<HashSet<Guid>> Load(HubDb db, Actor actor, bool includeIndirectReports = false, bool includeLedUnits = true)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.SupervisorId, UnitId = EF.Property<Guid?>(x, "UnitId") }).ToListAsync();
        if (actor.Admin || actor.Executive) return users.Select(x => x.Id).ToHashSet();
        if (!includeLedUnits && !includeIndirectReports) return users.Where(x => x.SupervisorId == actor.Id).Select(x => x.Id).ToHashSet();
        var units = await db.Set<OrgUnit>().AsNoTracking().Select(x => new OrgTree.Unit(x.Id, x.ParentId,
            db.Set<OrgLevel>().Where(l => l.Id == x.LevelId).Select(l => l.SortOrder).FirstOrDefault(), x.LeaderId)).ToListAsync();
        var people = users.Select(x => new OrgTree.Person(x.Id, x.SupervisorId, x.UnitId)).ToArray();
        return [.. OrgTree.PeopleScope(people.FirstOrDefault(x => x.Id == actor.Id) ?? new(actor.Id, null, null), people, includeLedUnits ? units : [], includeIndirectReports)];
    }

    public static async Task<HashSet<Guid>> LoadUnit(HubDb db, Actor actor, Guid unitId)
    {
        var units = await db.Set<OrgUnit>().AsNoTracking().Select(x => new OrgTree.Unit(x.Id, x.ParentId, 0, x.LeaderId)).ToListAsync();
        if (!units.Any(x => x.Id == unitId && x.LeaderId == actor.Id)) return [];
        var ids = OrgTree.Descendants(unitId, units);
        return await db.Users.AsNoTracking().Where(x => x.IsActive && EF.Property<Guid?>(x, "UnitId") != null && ids.Contains(EF.Property<Guid?>(x, "UnitId")!.Value)).Select(x => x.Id).ToHashSetAsync();
    }

}
