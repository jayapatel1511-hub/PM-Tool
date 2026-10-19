using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class WorkConstraintRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.WorkConstraint), "WorkConstraint", ["TargetType", "TargetId", "Category", "Description", "RemovalOwnerId", "AffectedOwnerId", "NeededBy", "SourceUrl", "State", "ResolutionEvidenceUrl", "VerifiedBy", "VerifiedAt", "LinkedType", "LinkedId"], false, "no item-history surface today; child and organisation history retain existing queries");     public static readonly Hub.Api.Features.Col[] ReadinessConstraintColumns = [new("key", "key"), new("work", "item"), new("category", "constraintCategory"),
        new("description", "description"), new("removalOwner", "removalOwner"), new("affectedOwner", "affectedOwner"),
        new("neededBy", "neededBy", "date"), new("state", "status"), new("linked", "linkedRecord"), new("sourceUrl", "sourceEvidence")];
}
