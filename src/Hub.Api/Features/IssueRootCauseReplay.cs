using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class IssueRootCauseReplay
{
    public sealed record Guard;
    /// Current authority only: successful receipts may describe an issue that is already resolved or a now-stale version.
    public static async Task Authorise(HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        if (!http.Request.HasJsonContentType()) return;
        http.Request.EnableBuffering(); var position = http.Request.Body.Position; var feature = false;
        try
        {
            var body = await http.Request.ReadFromJsonAsync<JsonElement>(JsonOpts.Web, http.RequestAborted);
            if (body.ValueKind == JsonValueKind.Object)
                foreach (var property in body.EnumerateObject())
                    if (string.Equals(property.Name, "rootCauseCategoryId", StringComparison.OrdinalIgnoreCase)) feature = property.Value.ValueKind != JsonValueKind.Null;
        }
        finally { http.Request.Body.Position = position; }
        if (!feature) return;
        Http.RequireFlag(await store.Get(db), IssueRootCauses.Flag);
        if (!Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id)) throw ApiException.NotFound();
        var issue = await db.Issues.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id) ?? throw ApiException.NotFound();
        var (_, context) = await access.Project(issue.ProjectId, false);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, context, RegisterEndpoints.Facts(issue)));
    }
}
