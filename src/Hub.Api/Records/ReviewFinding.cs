using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReviewFindingRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReviewFinding), "ReviewFinding", ["PackageId", "RoundId", "CarriedFromId", "IssueId", "SourceRevisionId", "ProjectDisciplineId", "OriginatorId", "ResolverId", "VerifierId", "Text", "Severity", "Status", "Response", "EvidenceUrl", "WithdrawalAcknowledgedBy", "ResponseOutcome", "DeliverableIssueId", "ExternalReviewId"], true, null)
    {
        Project = async (db, id) => await db.ReviewFindings.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
}
