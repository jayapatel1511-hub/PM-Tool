using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DisciplineReviewRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.DisciplineReview), "DisciplineReview", ["RoundId", "ProjectDisciplineId", "ReviewerId", "DueDate", "Status", "Rationale", "DecidedAt", "DecidedBy"], false, "no item-history surface today; child and organisation history retain existing queries"); }
