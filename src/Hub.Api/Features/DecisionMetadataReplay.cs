using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Authorisation before generic receipt lookup, deliberately separate from fresh version/workflow feasibility.
public static class DecisionMetadataReplay
{
    public sealed record Guard(bool Transition = false);
    public static async Task Authorise(HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        var guard = http.GetEndpoint()?.Metadata.GetMetadata<Guard>();
        if (guard is null || !http.Request.HasJsonContentType()) return;
        http.Request.EnableBuffering(); var position = http.Request.Body.Position; var feature = false;
        try
        {
            var body = await http.Request.ReadFromJsonAsync<JsonElement>(JsonOpts.Web, http.RequestAborted);
            if (body.ValueKind == JsonValueKind.Object)
                foreach (var property in body.EnumerateObject())
                    if ((guard.Transition ? property.Name.Equals("rationale", StringComparison.OrdinalIgnoreCase)
                        : property.Name.Equals("categoryId", StringComparison.OrdinalIgnoreCase) || property.Name.Equals("optionsConsidered", StringComparison.OrdinalIgnoreCase))
                        && property.Value.ValueKind != JsonValueKind.Null) feature = true;
        }
        finally { http.Request.Body.Position = position; }
        if (!feature) return;
        Http.RequireFlag(await store.Get(db), DecisionMetadata.Flag);
        if (!Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id)) throw ApiException.NotFound();
        if (!guard.Transition)
        {
            var (_, context) = await access.Project(id, track: false);
            Access.Demand(Permissions.RaiseRegisterItem(access.Actor, context));
        }
        else
        {
            var (decision, _, context) = await DecisionEndpoints.Load(db, access, id);
            Access.Demand(Permissions.DecideOrDefer(access.Actor, context, DecisionEndpoints.Facts(decision)));
        }
    }
}
