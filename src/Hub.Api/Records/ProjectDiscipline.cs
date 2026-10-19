using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ProjectDisciplineRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ProjectDiscipline), "Discipline", ["DisciplineId", "LeadUserId", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 1,
        Names = async (db, list) => (await db.ProjectDisciplines.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Discipline!.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 2,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.ProjectDisciplines.Where(p => visible.Contains(p.ProjectId) && list.Contains(p.Id)).Join(db.Disciplines, p => p.DisciplineId, d => d.Id,
                (p, d) => new { p.Id, d.Name }).ToListAsync()).Select(x => (x.Id, x.Name)); }
    };
}