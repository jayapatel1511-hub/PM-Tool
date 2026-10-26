using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class OpenRoleRecord { public static RegistryEntry Entry => new(typeof(Hub.Api.Data.OpenRole), "OpenRole", ["RoleLabel", "ProjectDisciplineId", "FromDate", "ThroughDate", "PlannedHours", "Status", "FilledAllocationId", "Note", "DeletedAt"], true, null)
    {
        Project = async (db, id) => await db.OpenRoles.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    /// §47.5 FR-REC-12: the open-roles list's columns, with the role cell's discipline and note as their own columns.
    public static readonly Hub.Api.Features.Col[] ExportColumns = [new("roleLabel", "role"), new("disciplineName", "discipline"), new("fromDate", "from", "date"),
        new("throughDate", "through", "date"), new("plannedHours", "hours", "number"), new("status", "status"), new("filledByName", "filledBy"), new("note", "note")];
}
