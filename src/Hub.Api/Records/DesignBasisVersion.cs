using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DesignBasisVersionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.DesignBasisVersion), "DesignBasisVersion", ["EntryId", "Number", "SupersedesVersionId", "Status", "Scope", "Statement", "NumericValue", "Units", "SourceSystem", "StableSourceId", "SourceUrl", "DeclaredRevision", "ConfirmationDueDate", "DecisionId", "ConfirmedBy", "ConfirmedAt", "ConfirmationRationale"], false, "no item-history surface today; child and organisation history retain existing queries"); }
