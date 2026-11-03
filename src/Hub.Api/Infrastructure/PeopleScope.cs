using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Read-only people scope used by staff, workload and planning queries. Write authorization remains in Permissions.
public static class PeopleScope
{
    public static async Task<IQueryable<AppUser>> Widen(HubDb db, Actor actor, IQueryable<AppUser> baseline)
    {
        var settings = await db.CurrentSettings();
        if (!settings.IsFlagOn("org_structure") || actor.Admin || actor.Executive) return baseline;
        if (!actor.Supervisor && !await LeadsUnit(db, actor)) return baseline;
        var scope = await Load(db, actor, settings.IncludeIndirectReports);
        return baseline.Union(db.Users.AsNoTracking().Where(u => scope.Contains(u.Id)));
    }

    public static async Task<IQueryable<AppUser>> FilterUnit(HubDb db, IQueryable<AppUser> people, Guid? unitId)
    {
        if (unitId is not { } id || !(await db.CurrentSettings()).IsFlagOn("org_structure")) return people;
        if (id == Guid.Empty) return people.Where(u => u.UnitId == null);
        var tree = await db.Set<OrgUnit>().AsNoTracking().Select(u => new OrgTree.Unit(u.Id, u.ParentId, 0, u.LeaderId)).ToListAsync();
        var units = OrgTree.Descendants(id, tree);
        return people.Where(u => u.UnitId != null && units.Contains(u.UnitId.Value));
    }

    public static async Task DemandWorkload(HubDb db, Actor actor)
    {
        if (Permissions.ViewWorkload(actor).Ok) return;
        Access.Demand(await ReadsAsUnitLeader(db, await db.CurrentSettings(), actor)
            ? Allow.Yes : Permissions.ViewWorkload(actor));
    }

    /// FR-UNT-11: while org_structure is on, a unit leader may read Workload, the Planner and their reports for their units.
    public static async Task<bool> ReadsAsUnitLeader(HubDb db, OrgSettings settings, Actor actor) => settings.IsFlagOn("org_structure") && await LeadsUnit(db, actor);

    public static Task<bool> LeadsUnit(HubDb db, Actor actor) => db.Set<OrgUnit>().AnyAsync(u => u.LeaderId == actor.Id && u.ClosedOn == null);

    public static async Task<HashSet<Guid>> Load(HubDb db, Actor actor, bool includeIndirectReports = false, bool includeLedUnits = true)
    {
        var users = await db.Users.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.SupervisorId, UnitId = EF.Property<Guid?>(x, "UnitId") }).ToListAsync();
        var people = users.Select(x => new OrgTree.Person(x.Id, x.SupervisorId, x.UnitId)).ToArray();
        if (actor.Admin || actor.Executive || !includeLedUnits && !includeIndirectReports)
            return FromSnapshot(actor, people, [], includeIndirectReports, includeLedUnits);
        var units = await db.Set<OrgUnit>().AsNoTracking().Select(x => new OrgTree.Unit(x.Id, x.ParentId,
            db.Set<OrgLevel>().Where(l => l.Id == x.LevelId).Select(l => l.SortOrder).FirstOrDefault(), x.LeaderId)).ToListAsync();
        return FromSnapshot(actor, people, units, includeIndirectReports, includeLedUnits);
    }

    /// The same read-scope calculation over already loaded facts; this never grants write authorization.
    public static HashSet<Guid> FromSnapshot(Actor actor, IReadOnlyCollection<OrgTree.Person> people,
        IReadOnlyCollection<OrgTree.Unit> units, bool includeIndirectReports = false, bool includeLedUnits = true)
    {
        var active = people.Where(person => person.Active).ToArray();
        if (actor.Admin || actor.Executive) return active.Select(person => person.Id).ToHashSet();
        if (!includeLedUnits && !includeIndirectReports)
            return active.Where(person => person.SupervisorId == actor.Id).Select(person => person.Id).ToHashSet();
        return [.. OrgTree.PeopleScope(active.FirstOrDefault(person => person.Id == actor.Id) ?? new(actor.Id, null, null),
            active, includeLedUnits ? units : [], includeIndirectReports)];
    }

    public static async Task<HashSet<Guid>> LoadUnit(HubDb db, Actor actor, Guid unitId)
    {
        var units = await db.Set<OrgUnit>().AsNoTracking().Select(x => new OrgTree.Unit(x.Id, x.ParentId, 0, x.LeaderId)).ToListAsync();
        if (!units.Any(x => x.Id == unitId && x.LeaderId == actor.Id)) return [];
        var ids = OrgTree.Descendants(unitId, units);
        return await db.Users.AsNoTracking().Where(x => x.IsActive && EF.Property<Guid?>(x, "UnitId") != null && ids.Contains(EF.Property<Guid?>(x, "UnitId")!.Value)).Select(x => x.Id).ToHashSetAsync();
    }

}
