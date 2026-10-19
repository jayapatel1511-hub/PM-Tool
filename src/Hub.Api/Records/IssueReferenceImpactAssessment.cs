using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueReferenceImpactAssessmentRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.IssueReferenceImpactAssessment), "IssueReferenceImpactAssessment", ["IssueId", "DocumentReferenceId", "PreviousRevisionId", "CurrentRevisionId", "OwnerId", "VerifierId", "Status", "OwnerDisposition", "OwnerReason", "OwnerDecidedBy", "OwnerDecidedAt", "VerifierDisposition", "VerifierReason", "VerifierDecidedBy", "VerifierDecidedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
