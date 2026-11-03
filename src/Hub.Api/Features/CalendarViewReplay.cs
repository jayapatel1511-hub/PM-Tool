using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// A saved-view receipt does not preserve old flag or project authority.
public static class CalendarViewReplay
{
    public sealed class Guard { }
    public sealed record ReceiptContext(Guid? ProjectId, string Scope);
    static readonly object ContextKey = new();
    public static ReceiptContext? Context(HttpContext http) => http.Items.TryGetValue(ContextKey, out var value) ? value as ReceiptContext : null;

    public static Task AuthoriseStored(IdempotencyRecord record, Access access, HubDb db, SettingsStore store) =>
        record.CalendarViewScope is { } scope ? Demand(new(record.CalendarViewProjectId, scope), access, db, store) : Task.CompletedTask;

    public static async Task Authorise(HttpContext http, Access access, HubDb db, SettingsStore store, string key)
    {
        if (http.GetEndpoint()?.Metadata.GetMetadata<Guard>() is null) return;
        Check.That(key.Length <= 100, "Idempotency-Key", "error.too_long", 100);
        if (!http.Request.HasJsonContentType()) return;
        http.Request.EnableBuffering(); var position = http.Request.Body.Position;
        try
        {
            var json = await http.Request.ReadFromJsonAsync<JsonElement>(JsonOpts.Web, http.RequestAborted);
            if (json.ValueKind != JsonValueKind.Object) return;
            var list = json.EnumerateObject().LastOrDefault(property => property.Name.Equals("listType", StringComparison.OrdinalIgnoreCase));
            if (list.Value.ValueKind != JsonValueKind.String || list.Value.GetString() != "calendar") return;
            var body = json.Deserialize<ViewEndpoints.ViewBody>(JsonOpts.Web)!;
            var context = new ReceiptContext(body.ProjectId, body.Scope ?? "Personal");
            await Demand(context, access, db, store);
            http.Items[ContextKey] = context;
        }
        catch (JsonException)
        { /* Ordinary binding or a legacy non-Calendar receipt retains its previous behavior. */ }
        finally { http.Request.Body.Position = position; }
    }

    static async Task Demand(ReceiptContext context, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "work_screens");
        Check.OneOf(context.Scope, ["Personal", "Project"], "scope");
        if (context.ProjectId is { } projectId)
        {
            var (_, project) = await access.Project(projectId, track: false);
            if (context.Scope == "Project") Access.Demand(Permissions.ManageSavedProjectView(access.Actor, project));
        }
        else Check.That(context.Scope == "Personal", "scope", "view.project_needed");
    }
}
