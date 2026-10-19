using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReviewRoundRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReviewRound), "ReviewRound", ["PackageId", "Number", "Purpose", "Reason", "RemovalImpact", "StartedAt", "Status"], false, "no item-history surface today; child and organisation history retain existing queries"); }
