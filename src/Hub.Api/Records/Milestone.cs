using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class MilestoneRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Milestone), "Milestone", ["Name", "MilestoneType", "Date", "Description", "ProjectDisciplineId", "CompletesPhaseId", "IsClientFacing", "IsComplete", "CompletedDate", "IsCancelled", "CancelledReason", "DeletedAt", "BaselineDate"], true, null)
    {
        NameOrder = 5,
        Names = async (db, list) => (await db.Milestones.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")),
        ExportNameOrder = 6,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Milestones.IgnoreQueryFilters().Where(m => list.Contains(m.Id) && visible.Contains(m.ProjectId)).Select(m => new { m.Id, m.Key, m.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)); },
        Project = async (db, id) => await db.Milestones.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ListExportsMilestoneCols = [
        new("key", "key", "key"), new("name", "milestone"), new("milestoneType", "type"), new("date", "date", "date"), new("slipDays", "slip", "number"), new("status", "status"),
        new("deliverablesIssued", "deliverablesIssued"), new("taskOpen", "openTasks", "number"),
    ];
}