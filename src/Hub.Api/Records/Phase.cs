using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class PhaseRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Phase), "ReferenceData", ["Name", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 17,
        Names = async (db, list) => (await db.Phases.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 14,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Phases.Where(p => list.Contains(p.Id) && db.Projects.Any(x => visible.Contains(x.Id) && x.PhaseId == p.Id)).Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(x => (x.Id, x.Name)); }
    };
}