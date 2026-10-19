using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DeliverableTypeRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.DeliverableType), "ReferenceData", ["Name", "DefaultDisciplineId", "IsActive", "SortOrder"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 19,
        Names = async (db, list) => (await db.DeliverableTypes.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name }).ToListAsync()).Select(x => (x.Id, x.Name)),
        ExportNameOrder = 16,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.DeliverableTypes.Where(t => list.Contains(t.Id) && db.Deliverables.IgnoreQueryFilters().Any(d => visible.Contains(d.ProjectId) && d.DeliverableTypeId == t.Id))
                .Select(t => new { t.Id, t.Name }).ToListAsync()).Select(x => (x.Id, x.Name)); }
    };
}