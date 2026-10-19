using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ProjectTypeRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ProjectType), "ReferenceData", ["Name", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 18,
        Names = async (db, list) => (await db.ProjectTypes.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 15,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.ProjectTypes.Where(t => list.Contains(t.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.ProjectTypeId == t.Id)).Select(t => new { t.Id, t.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)); }
    };
}