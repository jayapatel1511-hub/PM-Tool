using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ExternalPartyRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ExternalParty), "ExternalParty", ["Name", "Organisation", "Email", "Role", "IsClient", "Notes", "IsActive"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 20,
        Names = async (db, list) => (await db.ExternalParties.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Name, x.Organisation }).ToListAsync())
            .Select(x => (x.Id, x.Organisation is null ? x.Name : $"{x.Name} ({x.Organisation})")),
        ExportNameOrder = 17,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.ExternalParties.Where(e => list.Contains(e.Id) && db.Actions.IgnoreQueryFilters().Any(a => visible.Contains(a.ProjectId) && a.OwnerExternalPartyId == e.Id))
                .Select(e => new { e.Id, e.Name, e.Organisation }).ToListAsync()).Select(x => (x.Id, x.Organisation is null ? x.Name : x.Name + " (" + x.Organisation + ")")); }
    };
}