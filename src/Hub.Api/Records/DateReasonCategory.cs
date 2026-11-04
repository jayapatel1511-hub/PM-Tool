using Hub.Api.Data;

namespace Hub.Api.Records;

public static class DateReasonCategoryRecord
{
    public static RegistryEntry Entry { get; } = new(typeof(DateReasonCategory), "ReferenceData", ["Name", "IsActive", "SortOrder"], false,
        "organisation date-move reasons have no item-history surface; organisation history retains its existing scope");
}
