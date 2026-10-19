using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class BasisImpactAssessmentRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.BasisImpactAssessment), "BasisImpactAssessment", ["BasisUseId", "OldVersionId", "NewVersionId", "WithdrawalVersionId", "OwnerId", "Status", "Rationale", "EvidenceUrl", "DecidedBy", "DecidedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
