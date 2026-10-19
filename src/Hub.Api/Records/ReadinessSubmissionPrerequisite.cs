using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReadinessSubmissionPrerequisiteRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReadinessSubmissionPrerequisite), "ReadinessSubmissionPrerequisite", ["TargetType", "TargetId", "PackageId", "Reason", "RemovedAt", "RemovedBy", "RemovalReason"], false, "no item-history surface today; child and organisation history retain existing queries"); }
