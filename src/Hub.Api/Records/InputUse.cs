using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class InputUseRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.InputUse), "InputUse", ["TargetType", "TargetId", "OwnerId", "SourceIdentity", "SourceRevisionId", "IntendedUse", "AdoptedAt", "AdoptedBy"], false, "no item-history surface today; child and organisation history retain existing queries"); }
