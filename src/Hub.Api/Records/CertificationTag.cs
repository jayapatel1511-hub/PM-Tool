using Hub.Api.Data;
namespace Hub.Api.Records;
public static class CertificationTagRecord { public static RegistryEntry Entry { get; } = new(typeof(CertificationTag), "ReferenceData", ["Name", "IsActive", "SortOrder"], false, "organisation vocabulary; no person usage counts or item-history surface"); }
