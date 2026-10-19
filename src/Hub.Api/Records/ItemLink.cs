using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ItemLinkRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ItemLink), "ItemLink", ["TargetType", "TargetId", "Relation", "DeletedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
