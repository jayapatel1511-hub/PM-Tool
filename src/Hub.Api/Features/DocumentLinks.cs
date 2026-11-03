using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Document links on projects, deliverables and tasks (§12.7, DOC-01..DOC-04, FR-DOC-01..03): pointers only, never files.
public static class DocumentLinkEndpoints
{
    public sealed record AddBody(string Url, string? Title = null, string? LinkType = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DocumentLinkReferenceInput? Reference = null);
    public sealed record LinkRow(Guid Id, string Title, string Url, string LinkType, int RowVersion, bool IsNetworkPath, Guid AddedBy, string? AddedByName, DateTimeOffset AddedAt, bool CanChange,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DocumentLinkReferenceContext? ReferenceContext = null);
    public sealed record LinkListResponse(bool CanAdd, IReadOnlyList<LinkRow> Links, IReadOnlyList<LinkRow> Inherited);
    public sealed record LinkResponse(Guid Id, string Title, string Url, string LinkType, int RowVersion);

    public static async Task<(Guid ProjectId, string Key, Guid? DeliverableId)?> Owner(HubDb db, string type, Guid id)
    {
        switch (type)
        {
            case ItemType.Project:
                var p = await db.Projects.Where(x => x.Id == id).Select(x => new { x.Id, x.ProjectNumber }).FirstOrDefaultAsync();
                return p is null ? null : (p.Id, p.ProjectNumber, null);
            case ItemType.Deliverable:
                var d = await db.Deliverables.Where(x => x.Id == id).Select(x => new { x.ProjectId, x.Key }).FirstOrDefaultAsync();
                return d is null ? null : (d.ProjectId, d.Key, null);
            case ItemType.Task:
                var t = await db.Tasks.Where(x => x.Id == id).Select(x => new { x.ProjectId, x.Key, x.DeliverableId }).FirstOrDefaultAsync();
                return t is null ? null : (t.ProjectId, t.Key, t.DeliverableId);
            default:
                return null;
        }
    }

    /// Project links belong to the PM (project information); links on work items to anyone who may comment.
    internal static Allow CanAdd(string type, Actor a, ProjectContext ctx) => type == ItemType.Project ? Permissions.EditProject(a, ctx) : Permissions.AddLink(a, ctx);

    internal static bool CanChange(DocumentLink l, Actor a, ProjectContext ctx) => CanAdd(l.ItemType, a, ctx).Ok && (l.AddedBy == a.Id || Permissions.IsPM(a, ctx));

    public static IQueryable<DocumentLink> Of(HubDb db, string type, Guid id) =>
        db.DocumentLinks.AsNoTracking().Where(l => l.ItemType == type && l.ItemId == id).OrderBy(l => l.AddedAt);

    public static async Task<DocumentLink> Add(HubDb db, Guid projectId, string type, Guid itemId, string key, AddBody body, Guid me, DateTimeOffset now, bool referenceEnabled = false)
    {
        // Shared creator guard protects existing producers too, not only this module's HTTP route.
        if (!referenceEnabled && body.Reference is not null) throw ApiException.NotFound();
        var url = Check.Required(body.Url, "url", 2000);
        Check.That(Links.IsValid(url), "url", "link.invalid"); // DOC-01
        if (body.LinkType is { } lt) Check.OneOf(lt, LinkType.All, "linkType");
        var link = new DocumentLink
        {
            ProjectId = projectId, ItemType = type, ItemId = itemId, Url = url, Title = Check.Optional(body.Title, "title", 200) ?? Links.DefaultTitle(url), // DOC-02
            LinkType = body.LinkType ?? Links.Detect(url), AddedBy = me, AddedAt = now, AuditKey = key,
        };
        if (body.Reference is not null) await DocumentLinkReferences.Apply(db, link, body.Reference, me, now);
        db.DocumentLinks.Add(link);
        await db.SaveChangesAsync();
        return link;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/items/{type}/{id:guid}/links", async (string type, Guid id, Access access, HubDb db, SettingsStore store) =>
        {
            var enabled = (await store.Get(db)).IsFlagOn(DocumentLinkReferences.Flag);
            await using var snapshot = enabled && db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
            Check.OneOf(type, ItemType.Linkable, "type");
            var o = await Owner(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(o.ProjectId, track: false);
            var own = await Of(db, type, id).Where(link => link.ProjectId == o.ProjectId).ToListAsync();
            var parent = o.DeliverableId is { } did && await db.Deliverables.AnyAsync(work => work.Id == did && work.ProjectId == o.ProjectId)
                ? o.DeliverableId : null;
            var inherited = parent is { } parentId ? await Of(db, ItemType.Deliverable, parentId).Where(link => link.ProjectId == o.ProjectId).ToListAsync() : []; // DOC-04
            var ids = own.Concat(inherited).Select(l => l.AddedBy).Distinct().ToList();
            var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
            var contexts = enabled ? await DocumentLinkReferences.ReadContexts(db, own.Concat(inherited).ToArray(), [o.ProjectId]) : null;
            LinkRow Row(DocumentLink l, bool mine) => new(l.Id, l.Title, l.Url, l.LinkType, l.RowVersion, Links.IsUnc(l.Url), l.AddedBy,
                names.GetValueOrDefault(l.AddedBy), l.AddedAt, mine && CanChange(l, access.Actor, ctx), contexts?.GetValueOrDefault(l.Id));
            return TypedResults.Ok(new LinkListResponse(CanAdd(type, access.Actor, ctx).Ok,
                own.Select(l => Row(l, true)).ToList(), inherited.Select(l => Row(l, false)).ToList()));
        });

        api.MapPost("/items/{type}/{id:guid}/links", async (string type, Guid id, AddBody body, Access access, HubDb db, TimeProvider clock, SettingsStore store) =>
        {
            Check.OneOf(type, ItemType.Linkable, "type");
            var o = await Owner(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(o.ProjectId);
            Access.Demand(CanAdd(type, access.Actor, ctx));
            var link = await Add(db, o.ProjectId, type, id, o.Key, body, access.Me.Id, clock.GetUtcNow(), (await store.Get(db)).IsFlagOn(DocumentLinkReferences.Flag));
            return TypedResults.Created($"/api/v1/links/{link.Id}", new LinkResponse(link.Id, link.Title, link.Url, link.LinkType, link.RowVersion));
        }).WithMetadata(new DocumentLinkCreateReplay.Guard());

        api.MapPatch("/links/{id:guid}", async (Guid id, JsonElement body, Access access, HubDb db, HttpContext http, SettingsStore store, TimeProvider clock) =>
        {
            var l = await db.DocumentLinks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(l.ProjectId);
            Access.Demand(CanChange(l, access.Actor, ctx) ? Allow.Yes : Allow.No("perm.owner"));
            var patch = new Patch(body);
            var enabled = (await store.Get(db)).IsFlagOn(DocumentLinkReferences.Flag);
            if (patch.Has("reference") && !enabled) throw ApiException.NotFound();
            await Http.CheckVersion(db, http, l, patch.Int("rowVersion"));
            var oldUrl = l.Url;
            if (patch.Has("url")) { var url = Check.Required(patch.Str("url"), "url", 2000); Check.That(Links.IsValid(url), "url", "link.invalid"); l.Url = url; }
            if (patch.Has("title")) l.Title = Check.Required(patch.Str("title"), "title", 200);
            if (patch.Has("linkType")) { var lt = patch.Str("linkType"); Check.OneOf(lt, LinkType.All, "linkType"); l.LinkType = lt!; }
            if (patch.Has("reference"))
            {
                var owner = await Owner(db, l.ItemType, l.ItemId) ?? throw ApiException.NotFound();
                if (owner.ProjectId != l.ProjectId) throw ApiException.NotFound();
                var reference = body.GetProperty("reference");
                if (reference.ValueKind == JsonValueKind.Null) DocumentLinkReferences.Clear(l);
                else
                {
                    Check.That(reference.ValueKind == JsonValueKind.Object, "reference", "error.invalid");
                    var input = reference.Deserialize<DocumentLinkReferenceInput>(JsonOpts.Web) ?? throw ApiException.Invalid("reference", "error.required");
                    await DocumentLinkReferences.Apply(db, l, input, access.Me.Id, clock.GetUtcNow());
                }
            }
            else if (!string.Equals(oldUrl, l.Url, StringComparison.Ordinal)) DocumentLinkReferences.Clear(l);
            l.AuditKey = (await Owner(db, l.ItemType, l.ItemId))?.Key;
            await db.SaveChangesAsync();
            return TypedResults.Ok(new LinkResponse(l.Id, l.Title, l.Url, l.LinkType, l.RowVersion));
        });

        api.MapDelete("/links/{id:guid}", async (Guid id, Access access, HubDb db, TimeProvider clock, HttpContext http) =>
        {
            var l = await db.DocumentLinks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(l.ProjectId);
            Access.Demand(CanChange(l, access.Actor, ctx) ? Allow.Yes : Allow.No("perm.owner"));
            await Http.CheckVersion(db, http, l);
            l.DeletedAt = clock.GetUtcNow(); // DOC-03: soft delete, logged
            l.DeletedBy = access.Me.Id;
            l.AuditKey = (await Owner(db, l.ItemType, l.ItemId))?.Key;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });
    }
}
