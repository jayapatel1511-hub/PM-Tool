using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Hub.Api.Features;

/// The existing command and hypothetical-capacity calculation run in a transaction that cannot commit.
public sealed class AllocationPreviewFilter : IEndpointFilter
{
    public sealed record PreviewWeek(Guid PersonId, DateOnly Week, decimal Capacity, decimal Planned, decimal Free, string Band, string[] Indicators);
    public sealed record AllocationPreviewResponse(bool Preview, IReadOnlyList<PreviewWeek> Weeks,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AllocationScreenReads.ProposalCapacityRead? Comparison = null);

    // The filter returns preview data at the same status as the normal saved command.
    public static async Task Describe(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var saved = await context.GetOrCreateSchemaAsync(typeof(Coordination.Result), null, cancellationToken);
        var preview = await context.GetOrCreateSchemaAsync(typeof(AllocationPreviewResponse), null, cancellationToken);
        operation.Responses!["200"].Content!["application/json"].Schema = new OpenApiSchema { AnyOf = [saved, preview] };
        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter { Name = "preview", In = ParameterLocation.Query, Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean } });
    }

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
            // Only S30 create/Fill adds the scoped before/after read. Edits and confirmation preserve their existing contract and checks.
            var comparison = settings.IsFlagOn("capacity_screens") && before is null && !http.Request.RouteValues.ContainsKey("id")
                ? await AllocationScreenReads.Proposal(allocation, true, access, db, store, clock) : null;
            if (comparison is not null && comparison.State != "Complete")
                return TypedResults.Ok(new AllocationPreviewResponse(true, [], comparison));
            var weeks = new List<PreviewWeek>();
            async Task Add(ResourceAllocation candidate, DateOnly from, DateOnly through)
            {
                var days = await AllocationEndpoints.Capacity(db, access, candidate, store, clock, Workload.WeekOf(from), Workload.WeekOf(through).AddDays(6));
                foreach (var group in days.GroupBy(d => Workload.WeekOf(d.Date)))
                {
                    var capacity = group.Sum(d => d.AvailableHours); var planned = group.Sum(d => d.ResultingHours);
                    // The shared §45.4 rules: Workload.Pct and its band (lower case, as /workload sends it), and the
                    // existing per-day over-capacity warning (FR-VER-12) rather than a second weekly-sum rule.
                    weeks.Add(new PreviewWeek(candidate.PersonId, group.Key, capacity, planned, capacity - planned,
                        Workload.LoadBand(Workload.Pct(planned, capacity), settings).ToString().ToLowerInvariant(),
                        group.Any(d => d.OverByHours > 0) ? ["overCapacity"] : []));
                }
            }
            var from = before?.PersonId == allocation.PersonId && before.FromDate < allocation.FromDate ? before.FromDate : allocation.FromDate;
            var through = before?.PersonId == allocation.PersonId && before.ThroughDate > allocation.ThroughDate ? before.ThroughDate : allocation.ThroughDate;
            await Add(allocation, from, through);
            if (before != null && before.PersonId != allocation.PersonId)
                await Add(new ResourceAllocation { ProjectId = before.ProjectId, PersonId = before.PersonId, FromDate = before.FromDate,
                    ThroughDate = before.ThroughDate, PlannedHours = 0 }, before.FromDate, before.ThroughDate);
            return TypedResults.Ok(new AllocationPreviewResponse(true, weeks, comparison));
        }
        finally { await tx.RollbackAsync(); db.DiscardNotices(); db.ChangeTracker.Clear(); }
    }
}
