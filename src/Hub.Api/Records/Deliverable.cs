using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DeliverableRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Deliverable), "Deliverable", ["Name", "ProjectDisciplineId", "DeliverableTypeId", "Description", "OwnerId", "ReviewerId", "MilestoneId", "StartDate", "DueDate", "Priority", "Status", "Revision", "IssuedDate", "IssuedTo", "TransmittalUrl", "AcceptedDate", "OnHoldReason", "CancelledReason", "RequiresReview", "RequiredReviewPackageId", "ClientDrawingNumber", "StartedAt", "DeletedAt", "CurrentBaselineId", "BaselineStartDate", "BaselineDueDate"], true, null)
    {
        NameOrder = 4,
        Names = async (db, list) => (await db.Deliverables.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Name}")),
        ExportNameOrder = 5,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Deliverables.IgnoreQueryFilters().Where(d => list.Contains(d.Id) && visible.Contains(d.ProjectId)).Select(d => new { d.Id, d.Key, d.Name }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Name)); },
        Project = async (db, id) => await db.Deliverables.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ListExportsDeliverableCols = [
        new("key", "key", "key"), new("name", "deliverable"), new("typeName", "type"), new("disciplineName", "discipline"), new("ownerName", "owner"),
        new("reviewerName", "reviewer"), new("milestoneKey", "milestone"), new("dueDate", "due", "date"), new("status", "status"), new("state.progressPct", "progress", "percent"),
        new("indicators", "indicators"), new("revision", "revision"), new("issuedDate", "issued", "date"),
    ];
}