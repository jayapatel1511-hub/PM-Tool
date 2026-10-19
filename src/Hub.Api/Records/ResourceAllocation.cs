using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ResourceAllocationRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ResourceAllocation), "ResourceAllocation", ["PersonId", "Purpose", "FromDate", "ThroughDate", "PlannedHours", "Status", "ConfirmedBy", "ConfirmedAt"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 14,
        Names = async (db, list) => (await db.Allocations.Where(x => list.Contains(x.Id))
            .Select(x => new { x.Id, Person = db.Users.Where(u => u.Id == x.PersonId).Select(u => u.DisplayName).FirstOrDefault(), x.FromDate, x.ThroughDate }).ToListAsync())
            .Select(x => (x.Id, $"{x.Person}, {x.FromDate:yyyy-MM-dd} to {x.ThroughDate:yyyy-MM-dd}"))
    };
    public static readonly Hub.Api.Features.Col[] AllocationsExportColumns = [
        new("personName", "person"), new("personState", "person", Label: "Person eligibility"), new("purpose", "type"), new("fromDate", "date", "date", "From date"),
        new("throughDate", "date", "date", "Through date"), new("plannedHours", "hours", "number"), new("status", "status"),
    ];
}