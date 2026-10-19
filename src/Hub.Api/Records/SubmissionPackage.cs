using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class SubmissionPackageRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.SubmissionPackage), "SubmissionPackage", ["Title", "Purpose", "RecipientReference", "CoordinatorId", "MilestoneId", "TargetDate", "Status", "ManifestVersion", "SupersedesPackageId"], false, "no item-history surface today; child and organisation history retain existing queries");     public static readonly Hub.Api.Features.Col[] SubmissionsListColumns = [
        new("key", "key"), new("title", "name"), new("status", "status"),
        new("coordinatorName", "owner", Label: "Coordinator"), new("milestoneName", "milestone"),
        new("targetDate", "targetDate", "date", "Target date"), new("blockerCount", "blockers", "number", "Readiness blockers")];
}
