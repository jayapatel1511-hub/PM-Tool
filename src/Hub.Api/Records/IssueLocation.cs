using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueLocationRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.IssueLocation), "IssueLocation", ["IssueId", "Kind", "SiteArea", "Building", "Level", "Room", "AssetSystem", "Alignment", "StartStation", "EndStation", "StationUnits", "CoordinateX", "CoordinateY", "CoordinateZ", "CoordinateReferenceSystem", "CoordinateUnits"], false, "no item-history surface today; child and organisation history retain existing queries"); }
