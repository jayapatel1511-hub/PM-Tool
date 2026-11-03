using Hub.Api.Data;

namespace Hub.Api.Records;

public static class DecisionCategoryRecord
{
    public static RegistryEntry Entry { get; } = new(typeof(DecisionCategory), "ReferenceData", ["Name", "IsActive", "SortOrder"], false,
        "organisation decision categories have no item-history surface; organisation history retains its existing scope");
}
