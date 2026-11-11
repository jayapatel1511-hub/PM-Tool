using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Hub.Api.Features;

/// S42 labels for an already authorised Team cohort; no directory, membership or capacity expansion.
public static class TeamScreenReads
{
    public sealed record Reference(Guid Id, string? Name);
    public sealed record Organisation(Reference? HomeDiscipline, Reference? Office, Reference? Supervisor,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Reference? Unit = null);

    public static async Task<Dictionary<Guid, Organisation>> ReadOrganisation(HubDb db, Guid[] authorisedIds, OrgSettings settings)
    {
        if (!settings.IsFlagOn("people_screens") || authorisedIds.Length == 0) return [];
        var unitsOn = settings.IsFlagOn("org_structure");
        var rows = await db.Users.AsNoTracking().Where(user => authorisedIds.Contains(user.Id)).Select(user => new OrganisationRow {
            Id = user.Id, HomeDisciplineId = user.HomeDisciplineId, HomeDisciplineName = db.Disciplines.Where(d => d.Id == user.HomeDisciplineId).Select(d => d.Name).FirstOrDefault(),
            OfficeId = user.OfficeId, OfficeName = db.Offices.Where(o => o.Id == user.OfficeId).Select(o => o.Name).FirstOrDefault(),
            SupervisorId = user.SupervisorId, SupervisorName = db.Users.Where(s => s.Id == user.SupervisorId).Select(s => s.DisplayName).FirstOrDefault(),
            UnitId = unitsOn ? user.UnitId : null,
            UnitName = unitsOn ? db.Set<OrgUnit>().Where(u => u.Id == user.UnitId).Select(u => u.Name).FirstOrDefault() : null
        }).ToArrayAsync();
        return rows.ToDictionary(row => row.Id, row => new Organisation(
            row.HomeDisciplineId is { } home ? new Reference(home, row.HomeDisciplineName) : null,
            row.OfficeId is { } office ? new Reference(office, row.OfficeName) : null,
            row.SupervisorId is { } supervisor ? new Reference(supervisor, row.SupervisorName) : null,
            row.UnitId is { } unit ? new Reference(unit, row.UnitName) : null));
    }
}

file sealed class OrganisationRow
{
    public Guid Id { get; init; }
    public Guid? HomeDisciplineId { get; init; }
    public string? HomeDisciplineName { get; init; }
    public Guid? OfficeId { get; init; }
    public string? OfficeName { get; init; }
    public Guid? SupervisorId { get; init; }
    public string? SupervisorName { get; init; }
    public Guid? UnitId { get; init; }
    public string? UnitName { get; init; }
}
