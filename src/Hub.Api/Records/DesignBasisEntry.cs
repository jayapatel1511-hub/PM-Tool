using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DesignBasisEntryRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.DesignBasisEntry), "DesignBasisEntry", ["Kind", "Title", "OwnerId", "ProjectDisciplineId", "IndependentApproverId", "CurrentVersionId"], false, "no item-history surface today; child and organisation history retain existing queries");     public static readonly Hub.Api.Features.Col[] DesignBasisExportColumns = [new("key", "key"), new("title", "name"),
        new("kind", "kind", Label: "Kind"), new("discipline", "discipline"),
        new("owner", "owner", Label: "Owner"), new("version", "revision", Label: "Version"),
        new("status", "status"), new("scope", "scope", Label: "Scope"),
        new("statement", "statement", Label: "Value or statement"), new("numericValue", "value", "number", "Numeric value"),
        new("units", "units", Label: "Units"), new("sourceSystem", "sourceSystem", Label: "Source system"),
        new("stableSourceId", "sourceId", Label: "Stable source ID"), new("sourceUrl", "sourceUrl", Label: "Source URL"),
        new("declaredRevision", "declaredRevision", Label: "Declared source revision"),
        new("confirmationDueDate", "due", "date", "Confirmation due date")];
}
