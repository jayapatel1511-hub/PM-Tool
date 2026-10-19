using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReviewManifestItemRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReviewManifestItem), "ReviewManifestItem", ["RoundId", "SourceRevisionId", "DeliverableId", "AuthorIds"], false, "no item-history surface today; child and organisation history retain existing queries"); }
