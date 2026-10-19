using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class WeeklyPlanSnapshotRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.WeeklyPlanSnapshot), "WeeklyPlanSnapshot", ["WeekStart", "CapturedAt", "CapturedBy", "CommittedCount"], false, "no item-history surface today; child and organisation history retain existing queries"); }
