using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class SubmissionCheckRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.SubmissionCheck), "SubmissionCheck", ["PackageId", "ManifestVersion", "Kind", "SourceId", "ProjectDisciplineId", "OwnerId", "Required", "Status", "EvidenceRule", "EvidenceUrl", "Reason", "ApprovedBy", "ApprovedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
