using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class OfficeRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Office), "ReferenceData", ["Name", "Code", "TimeZone", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 16,
        Names = async (db, list) => (await db.Offices.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 13,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Offices.Where(o => list.Contains(o.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.OfficeId == o.Id)).Select(o => new { o.Id, o.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)); }
    };
}