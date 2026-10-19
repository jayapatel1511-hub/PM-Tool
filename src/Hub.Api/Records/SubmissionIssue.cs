using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class SubmissionIssueRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.SubmissionIssue), "SubmissionIssue", ["PackageId", "ManifestVersion", "AuthorisedBy", "AuthorisedAt", "Destination", "TransmittalUrl"], false, "no item-history surface today; child and organisation history retain existing queries"); }
