using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Feature-bearing creates must obey today's flag and canonical authority before a cached receipt is returned.
public static class DocumentLinkCreateReplay
{
    public sealed record Guard;

    public static async Task Authorise(HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        if (!http.Request.HasJsonContentType()) return;
        http.Request.EnableBuffering();
        var position = http.Request.Body.Position;
        var hasReference = false;
        try
        {
            // Use the same charset-aware JSON reader as minimal API binding (including UTF-16).
            var body = await http.Request.ReadFromJsonAsync<JsonElement>(JsonOpts.Web, http.RequestAborted);
            if (body.ValueKind == JsonValueKind.Object)
                foreach (var property in body.EnumerateObject())
                    if (string.Equals(property.Name, "reference", StringComparison.OrdinalIgnoreCase))
                        hasReference = property.Value.ValueKind != JsonValueKind.Null;
        }
        finally { http.Request.Body.Position = position; }
        if (!hasReference) return; // Omitted/null old bodies keep the established receipt behavior.
        Http.RequireFlag(await store.Get(db), DocumentLinkReferences.Flag);
        var type = http.Request.RouteValues["type"]?.ToString();
        Check.OneOf(type, ItemType.Linkable, "type");
        if (!Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id)) throw ApiException.NotFound();
        var owner = await DocumentLinkEndpoints.Owner(db, type!, id) ?? throw ApiException.NotFound();
        var (_, context) = await access.Project(owner.ProjectId, track: false);
        Access.Demand(DocumentLinkEndpoints.CanAdd(type!, access.Actor, context));
    }
}
