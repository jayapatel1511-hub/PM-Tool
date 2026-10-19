using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class BasisUseRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.BasisUse), "BasisUse", ["VersionId", "TargetType", "TargetId", "OwnerId", "IntendedUse"], false, "no item-history surface today; child and organisation history retain existing queries"); }
