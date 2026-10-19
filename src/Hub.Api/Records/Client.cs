using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ClientRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Client), "ReferenceData", ["Name", "ShortName", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 15,
        Names = async (db, list) => (await db.Clients.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 12,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Clients.Where(c => list.Contains(c.Id) && db.Projects.Any(p => visible.Contains(p.Id) && p.ClientId == c.Id)).Select(c => new { c.Id, c.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)); }
    };
}