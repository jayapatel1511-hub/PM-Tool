using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record AdminUserDiscipline(Guid Id, string Name, bool IsActive);
public sealed record AdminUserScreen(Guid UserId, int UserRowVersion, DateTimeOffset AsOf, Guid? HomeDisciplineId,
    AdminUserDiscipline? HomeDiscipline, string HomeDisciplineState, decimal BaseWeeklyCapacityHours, string CapacityBasis,
    int? PeopleScopeCount, string ScopeBasis, bool? ScopeIncludesSelf, bool ConfiguredInactive);

/// Read inside the caller's repeatable snapshot. No user, permission or configuration is changed.
public static class AdminUserScreenReads
{
    public static async Task<Dictionary<Guid, AdminUserScreen>> Read(HubDb db, IReadOnlyCollection<Guid> userIds,
        OrgSettings settings, TimeProvider clock)
    {
        if (!settings.IsFlagOn("people_screens") || userIds.Count == 0) return [];
        var now = clock.GetUtcNow();
        var subjects = await db.Users.AsNoTracking().Where(user => userIds.Contains(user.Id)).Select(user => new
        {
            user.Id, user.RowVersion, user.HomeDisciplineId, user.WeeklyCapacityHours, user.IsActive,
            Roles = user.Roles.Select(role => role.Role).ToList(),
        }).ToListAsync();
        var disciplineIds = subjects.Select(user => user.HomeDisciplineId).OfType<Guid>().Distinct().ToArray();
        var disciplines = await db.Disciplines.AsNoTracking().Where(discipline => disciplineIds.Contains(discipline.Id))
            .Select(discipline => new AdminUserDiscipline(discipline.Id, discipline.Name, discipline.IsActive)).ToDictionaryAsync(discipline => discipline.Id);
        var supervisors = subjects.Where(user => user.Roles.Contains(SystemRole.Supervisor)).Select(user => user.Id).ToHashSet();
        var structure = settings.IsFlagOn("org_structure");
        List<OrgTree.Person> people = supervisors.Count == 0 ? [] : await db.Users.AsNoTracking().Where(user => user.IsActive)
            .Select(user => new OrgTree.Person(user.Id, user.SupervisorId, EF.Property<Guid?>(user, "UnitId"), true)).ToListAsync();
        List<OrgTree.Unit> units = !structure || supervisors.Count == 0 ? [] : await db.Set<OrgUnit>().AsNoTracking()
            .Select(unit => new OrgTree.Unit(unit.Id, unit.ParentId,
                db.Set<OrgLevel>().Where(level => level.Id == unit.LevelId).Select(level => level.SortOrder).FirstOrDefault(), unit.LeaderId)).ToListAsync();
        var directCounts = people.Where(person => person.SupervisorId != null).GroupBy(person => person.SupervisorId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var result = new Dictionary<Guid, AdminUserScreen>();
        foreach (var user in subjects)
        {
            var discipline = user.HomeDisciplineId is { } id ? disciplines.GetValueOrDefault(id) : null;
            if (discipline is not null && string.IsNullOrWhiteSpace(discipline.Name)) discipline = null;
            int? count = null; bool? self = null;
            if (supervisors.Contains(user.Id))
            {
                if (structure)
                {
                    var actor = new Actor(user.Id, user.IsActive, user.Roles.ToHashSet());
                    var scope = PeopleScope.FromSnapshot(actor, people, units, settings.IncludeIndirectReports);
                    count = scope.Count; self = scope.Contains(user.Id);
                }
                else { count = directCounts.GetValueOrDefault(user.Id); self = false; }
            }
            result[user.Id] = new(user.Id, user.RowVersion, now, user.HomeDisciplineId, discipline,
                user.HomeDisciplineId is null ? "NotSet" : discipline is null ? "Unknown" : discipline.IsActive ? "Active" : "Inactive",
                user.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours, user.WeeklyCapacityHours is null ? "OrganisationDefault" : "Override",
                count, !supervisors.Contains(user.Id) ? "NotApplicable" : structure ? "OrgPeopleScope" : "DirectReports", self, !user.IsActive);
        }
        return result;
    }
}
