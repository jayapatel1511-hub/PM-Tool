using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class PersonAvailabilityOverrideRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.PersonAvailabilityOverride), "PersonAvailabilityOverride", ["PersonId", "WorkDate", "AvailableHours", "Category", "ReconciledLeaveId"], false, "personal record; no item-history surface today"); }
