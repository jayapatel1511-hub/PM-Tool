using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class BasisConflictRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.BasisConflict), "BasisConflict", ["LeftVersionId", "RightVersionId", "Resolved", "ResolutionVersionId", "ResolvedBy", "ResolvedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
