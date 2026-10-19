using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class LeaveImportStateRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.LeaveImportState), "LeaveImportState", ["SourceSystem", "ImportedAt", "SourceUpdatedAt"], false, "personal record; no item-history surface today"); }
