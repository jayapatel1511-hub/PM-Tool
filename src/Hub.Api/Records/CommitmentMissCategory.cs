using Hub.Api.Data;
namespace Hub.Api.Records;
public static class CommitmentMissCategoryRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.CommitmentMissCategory), "ReferenceData", ["Name", "IsActive", "SortOrder"], false, "no item-history surface; existing child and organisation queries retain their scope"); }
