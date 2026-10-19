using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class MeetingRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Meeting), "Meeting", ["Title", "MeetingDate", "MeetingType", "NotesLink", "CalendarEventId", "DeletedAt"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 10,
        Names = async (db, list) => (await db.Meetings.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Title }).ToListAsync()).Select(x => (x.Id, x.Title)),
        ExportNameOrder = 11,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Meetings.IgnoreQueryFilters().Where(m => list.Contains(m.Id) && visible.Contains(m.ProjectId)).Select(m => new { m.Id, m.Title }).ToListAsync())
            .Select(x => (x.Id, x.Title)); }
    };
}