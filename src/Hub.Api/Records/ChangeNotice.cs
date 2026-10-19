using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ChangeNoticeRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ChangeNotice), "ChangeNotice", ["Title", "OwnerId", "ProjectDisciplineId", "OldRevisionId", "NewRevisionId", "Description", "Scope", "EffectiveDate", "AssessmentDueDate", "Status", "PublishedAt"], true, null)
    {
        NameOrder = 13,
        Names = async (db, list) => (await db.ChangeNotices.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Title }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Title}")),
        Project = async (db, id) => await db.ChangeNotices.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ChangesColumns = [new("key", "key"), new("title", "name"), new("status", "status"), new("assessmentDueDate", "due", "date"), new("pendingAssessments", "changePending"), new("scope", "changeScope")];
}