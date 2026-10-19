using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ChangeAssessmentRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ChangeAssessment), "ChangeAssessment", ["ChangeNoticeId", "TargetType", "TargetId", "OwnerId", "ReviewerId", "InputUseId", "HandoffId", "RevisionUsedId", "Status", "AcknowledgedAt", "Rationale", "EvidenceUrl", "CorrectionTaskId", "EffortImpactHours", "DateImpactDays", "RetainOldRevision", "RetentionApprovedBy", "RetentionReason", "VerifiedBy", "VerifiedAt"], true, null)
    {
        Project = async (db, id) => await db.ChangeAssessments.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
}