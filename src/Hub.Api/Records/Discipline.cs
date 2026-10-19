using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DisciplineRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Discipline), "ReferenceData", ["Name", "Code", "Colour", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 2,
        Names = async (db, list) => (await db.Disciplines.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 3,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Disciplines.Where(d => list.Contains(d.Id) && db.ProjectDisciplines.Any(p => visible.Contains(p.ProjectId) && p.DisciplineId == d.Id))
                .Select(d => new { d.Id, d.Name }).ToListAsync()).Select(x => (x.Id, x.Name)); }
    };
}