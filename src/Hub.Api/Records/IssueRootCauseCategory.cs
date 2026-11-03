using Hub.Api.Data;

namespace Hub.Api.Records;

public static class IssueRootCauseCategoryRecord
{
    public static RegistryEntry Entry { get; } = new(typeof(IssueRootCauseCategory), "ReferenceData", ["Name", "IsActive", "SortOrder"], false,
        "organisation issue root causes have no item-history surface; organisation history retains its existing scope");
}
