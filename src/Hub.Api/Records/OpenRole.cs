using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class OpenRoleRecord { public static RegistryEntry Entry => new(typeof(Hub.Api.Data.OpenRole), "OpenRole", ["RoleLabel", "ProjectDisciplineId", "FromDate", "ThroughDate", "PlannedHours", "Status", "FilledAllocationId", "Note", "DeletedAt"], true, null)
    {
        Project = async (db, id) => await db.OpenRoles.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
}
