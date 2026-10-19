using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class AllocationDayOverrideRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.AllocationDayOverride), "AllocationDayOverride", ["AllocationId", "WorkDate", "Hours"], false, "no item-history surface today; child and organisation history retain existing queries"); }
