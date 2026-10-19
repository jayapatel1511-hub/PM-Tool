using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class SubmissionManifestItemRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.SubmissionManifestItem), "SubmissionManifestItem", ["PackageId", "ManifestVersion", "DeliverableId", "SourceRevisionId", "ReviewRoundId", "Required"], false, "no item-history surface today; child and organisation history retain existing queries"); }
