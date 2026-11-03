using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssuePurposeRecord { public static RegistryEntry Entry { get; } = new(typeof(IssuePurpose), "ReferenceData", ["Name", "IsActive", "SortOrder"], false, "organisation issue purposes have no item-history surface; existing organisation history retains its scope")
    {
        ExportNameOrder = 30,
        ExportNames = async (db, access, ids) =>
        {
            if (!(await db.CurrentSettings()).IsFlagOn("register_screens")) return [];
            return (await db.IssuePurposes.Where(row => ids.Contains(row.Id)).Select(row => new { row.Id, row.Name }).ToListAsync()).Select(row => (row.Id, row.Name));
        }
    }; }
