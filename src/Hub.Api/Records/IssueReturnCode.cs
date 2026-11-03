using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueReturnCodeRecord { public static RegistryEntry Entry { get; } = new(typeof(IssueReturnCode), "ReferenceData", ["Code", "Name", "IsActive", "SortOrder"], false, "organisation response codes have no item-history surface; existing organisation history retains its scope")
    {
        ExportNameOrder = 31,
        ExportNames = async (db, access, ids) =>
        {
            if (!(await db.CurrentSettings()).IsFlagOn("register_screens")) return [];
            return (await db.IssueReturnCodes.Where(row => ids.Contains(row.Id)).Select(row => new { row.Id, row.Code, row.Name }).ToListAsync()).Select(row => (row.Id, row.Code + " " + row.Name));
        }
    }; }
