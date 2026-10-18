using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// The existing command and hypothetical-capacity calculation run in a transaction that cannot commit.
public sealed class AllocationPreviewFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.Request.Query["preview"] != "true") return await next(context);
        var sp = http.RequestServices; var db = sp.GetRequiredService<HubDb>(); var store = sp.GetRequiredService<SettingsStore>();
        var settings = await store.Get(db); Http.RequireFlag(settings, "allocation_preview");
        await using var tx = await db.Database.BeginTransactionAsync();
        try
        {
            ResourceAllocation? before = null;
            if (Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id))
                before = await db.Allocations.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id);
            var result = await next(context);
            if (result is not Coordination.Result saved) return result;
            var allocation = await db.Allocations.AsNoTracking().SingleAsync(a => a.Id == saved.Id);
            var access = sp.GetRequiredService<Access>(); var clock = sp.GetRequiredService<TimeProvider>();
            var weeks = new List<object>();
            async Task Add(ResourceAllocation candidate, DateOnly from, DateOnly through)
            {
                var days = await AllocationEndpoints.Capacity(db, access, candidate, store, clock, Workload.WeekOf(from), Workload.WeekOf(through).AddDays(6));
                foreach (var group in days.GroupBy(d => Workload.WeekOf(d.Date)))
                {
                    var capacity = group.Sum(d => d.AvailableHours); var planned = group.Sum(d => d.ResultingHours);
                    var pct = capacity == 0 ? (planned == 0 ? 0 : decimal.MaxValue) : planned * 100 / capacity;
                    weeks.Add(new { candidate.PersonId, Week = group.Key, Capacity = capacity, Planned = planned, Free = capacity - planned,
                        Band = Workload.LoadBand(pct, settings).ToString(),
                        Indicators = planned > capacity ? new[] { "Over-allocated" } : Array.Empty<string>() });
                }
            }
            var from = before?.PersonId == allocation.PersonId && before.FromDate < allocation.FromDate ? before.FromDate : allocation.FromDate;
            var through = before?.PersonId == allocation.PersonId && before.ThroughDate > allocation.ThroughDate ? before.ThroughDate : allocation.ThroughDate;
            await Add(allocation, from, through);
            if (before != null && before.PersonId != allocation.PersonId)
                await Add(new ResourceAllocation { ProjectId = before.ProjectId, PersonId = before.PersonId, FromDate = before.FromDate,
                    ThroughDate = before.ThroughDate, PlannedHours = 0 }, before.FromDate, before.ThroughDate);
            return new { Preview = true, Weeks = weeks };
        }
        finally { await tx.RollbackAsync(); db.ChangeTracker.Clear(); }
    }
}
