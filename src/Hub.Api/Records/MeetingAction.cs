using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class MeetingActionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.MeetingAction), "Action", ["Text", "OwnerType", "OwnerUserId", "OwnerDisciplineId", "OwnerExternalPartyId", "DueDate", "Status", "RelatedTaskId", "RelatedDecisionId", "DeletedAt"], true, null)
    {
        NameOrder = 9,
        Names = async (db, list) => (await db.Actions.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Text }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Text}")),
        ExportNameOrder = 10,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Actions.IgnoreQueryFilters().Where(a => list.Contains(a.Id) && visible.Contains(a.ProjectId)).Select(a => new { a.Id, a.Key, a.Text }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Text)); },
        Project = async (db, id) => await db.Actions.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] MeetingsActionCols = [
        new("key", "key", "key"), new("text", "actionText"), new("meetingTitle", "meeting"), new("meetingDate", "meetingDate", "date"), new("ownerName", "owner"),
        new("ownerTypeLabel", "ownerType"), new("dueDate", "due", "date"), new("daysOverdue", "daysOverdue", "number"), new("status", "status"), new("taskKey", "task"),
    ];
}