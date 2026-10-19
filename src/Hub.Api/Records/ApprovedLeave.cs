using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ApprovedLeaveRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ApprovedLeave), "ApprovedLeave", ["PersonId", "SourceSystem", "ExternalId", "SourceKind", "Status", "FromDate", "ThroughDate", "HoursPerDay", "SourceUpdatedAt", "ImportedAt"], false, "personal record; no item-history surface today"); }
