using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReviewPackageRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReviewPackage), "ReviewPackage", ["Title", "Purpose", "CoordinatorId", "ProjectDisciplineId", "Status", "CurrentRoundId", "RoundNumber", "RequiredForIssue"], true, null)
    {
        Project = async (db, id) => await db.ReviewPackages.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] ReviewsColumns = [new("key", "key"), new("title", "name"), new("status", "status"), new("roundNumber", "round"), new("outstandingDisciplines", "reviewOutstanding"), new("blockingFindings", "reviewBlocking"), new("waitingDays", "reviewWaiting")];
}