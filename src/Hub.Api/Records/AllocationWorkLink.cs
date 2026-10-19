using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class AllocationWorkLinkRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.AllocationWorkLink), "AllocationWorkLink", ["AllocationId", "PersonId", "WorkType", "WorkId", "WorkDate", "ReviewHours", "ReleasedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
