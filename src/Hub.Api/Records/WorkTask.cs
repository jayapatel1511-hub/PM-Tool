using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class WorkTaskRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.WorkTask), "Task", ["Name", "Description", "ProjectDisciplineId", "DeliverableId", "MilestoneId", "AssigneeId", "ReviewerId", "RequiresReview", "Priority", "StartDate", "DueDate", "Status", "ProgressPct", "EstimatedHours", "ManualBlockType", "ManualBlockReason", "OnHoldReason", "CancelledReason", "ReviewRound", "StartedAt", "DeletedAt"], true, null)
    {
        NameOrder = 3,
        Names = async (db, list) => (await db.Tasks.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")),
        ExportNameOrder = 4,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Tasks.IgnoreQueryFilters().Where(t => list.Contains(t.Id) && visible.Contains(t.ProjectId)).Select(t => new { t.Id, t.Key, t.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)); },
        Project = async (db, id) => await db.Tasks.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ListExportsTaskCols = [
        new("key", "key", "key"), new("name", "task"), new("deliverableKey", "deliverable"), new("disciplineName", "discipline"), new("assigneeName", "assignee"),
        new("reviewerName", "reviewer"), new("status", "status"), new("priority", "priority"), new("startDate", "start", "date"), new("dueDate", "due", "date"),
        new("progressPct", "progress", "percent"), new("estimatedHours", "estimate", "number"), new("indicators", "indicators"), new("lastActivityAt", "lastActivity", "datetime"),
    ];
}