using Hub.Api.Data;
namespace Hub.Api.Records;
public static class OrgUnitRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.OrgUnit), "ReferenceData", ["Name", "ParentId", "LevelId", "LeaderId", "DisciplineId", "OfficeId", "ClosedOn"], false, "no item-history surface; existing child and organisation queries retain their scope"); }
