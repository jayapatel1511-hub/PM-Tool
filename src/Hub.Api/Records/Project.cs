using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ProjectRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Project), "Project", ["ProjectNumber", "Name", "ClientId", "ClientReference", "ProjectManagerId", "OfficeId", "ProjectTypeId", "Description", "Location", "Status", "PhaseId", "StartDate", "TargetCompletionDate", "Priority", "Visibility", "InternalNotes", "CoordinationDay", "AllowViewerComments", "HealthOverride", "HealthOverrideNote", "HealthOverrideExpiresAt", "LastCoordinationReviewedAt", "LeadUnitId", "RevisionConvention", "StatusNote", "StatusNoteBy", "StatusNoteAt"], true, null)
    {
        NameOrder = 11,
        Names = async (db, list) => (await db.Projects.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.ProjectNumber, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.ProjectNumber} {x.Name}")),
        ExportNameOrder = 1,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Projects.Where(p => visible.Contains(p.Id) && list.Contains(p.Id)).Select(p => new { p.Id, p.ProjectNumber, p.Name }).ToListAsync())
            .Select(p => (p.Id, p.ProjectNumber + " " + p.Name)); },
        Project = async (db, id) => await db.Projects.Where(x => x.Id == id).Select(x => (Guid?)x.Id).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ListExportsProjectCols = [
        new("projectNumber", "project"), new("name", "name"), new("client.name", "client"), new("pm.displayName", "pm"), new("status", "status"), new("phase", "phase"),
        new("office", "office"), new("reportedHealth", "health"), new("progressPct", "progress", "percent"), new("overdueTasks", "overdueTasks", "number"),
        new("nextMilestone.name", "nextMilestone"), new("targetCompletionDate", "target", "date"), new("priority", "priority"),
    ];
}