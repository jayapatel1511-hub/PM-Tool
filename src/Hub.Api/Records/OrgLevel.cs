using Hub.Api.Data;
namespace Hub.Api.Records;
public static class OrgLevelRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.OrgLevel), "ReferenceData", ["Name", "SortOrder"], false, "no item-history surface; existing child and organisation queries retain their scope"); }
