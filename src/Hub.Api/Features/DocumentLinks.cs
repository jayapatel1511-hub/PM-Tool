using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Document links on projects, deliverables and tasks (§12.7, DOC-01..DOC-04, FR-DOC-01..03): pointers only, never files.
public static class DocumentLinkEndpoints
{
    public sealed record AddBody(string Url, string? Title, string? LinkType);

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
    static Allow CanAdd(string type, Actor a, ProjectContext ctx) => type == ItemType.Project ? Permissions.EditProject(a, ctx) : Permissions.AddLink(a, ctx);

    static bool CanChange(DocumentLink l, Actor a, ProjectContext ctx) => CanAdd(l.ItemType, a, ctx).Ok && (l.AddedBy == a.Id || Permissions.IsPM(a, ctx));

    public static IQueryable<DocumentLink> Of(HubDb db, string type, Guid id) =>
        db.DocumentLinks.AsNoTracking().Where(l => l.ItemType == type && l.ItemId == id).OrderBy(l => l.AddedAt);

    public static async Task<DocumentLink> Add(HubDb db, Guid projectId, string type, Guid itemId, string key, AddBody body, Guid me, DateTimeOffset now)
    {
        var url = Check.Required(body.Url, "url", 2000);
        Check.That(Links.IsValid(url), "url", "link.invalid"); // DOC-01
        if (body.LinkType is { } lt) Check.OneOf(lt, LinkType.All, "linkType");
        var link = new DocumentLink
        {
            ProjectId = projectId, ItemType = type, ItemId = itemId, Url = url, Title = Check.Optional(body.Title, "title", 200) ?? Links.DefaultTitle(url), // DOC-02
            LinkType = body.LinkType ?? Links.Detect(url), AddedBy = me, AddedAt = now, AuditKey = key,
        };
        db.DocumentLinks.Add(link);
        await db.SaveChangesAsync();
        return link;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/items/{type}/{id:guid}/links", async (string type, Guid id, Access access, HubDb db) =>
        {
            Check.OneOf(type, ItemType.Linkable, "type");
            var o = await Owner(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(o.ProjectId, track: false);
            var own = await Of(db, type, id).ToListAsync();
            var inherited = o.DeliverableId is { } did ? await Of(db, ItemType.Deliverable, did).ToListAsync() : []; // DOC-04
            var ids = own.Concat(inherited).Select(l => l.AddedBy).Distinct().ToList();
            var names = await db.Users.Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
            object Row(DocumentLink l, bool mine) => new
            {
                l.Id, l.Title, l.Url, l.LinkType, IsNetworkPath = Links.IsUnc(l.Url), l.AddedBy, AddedByName = names.GetValueOrDefault(l.AddedBy), l.AddedAt,
                CanChange = mine && CanChange(l, access.Actor, ctx),
            };
            return new { CanAdd = CanAdd(type, access.Actor, ctx).Ok, Links = own.Select(l => Row(l, true)), Inherited = inherited.Select(l => Row(l, false)) };
        });

        api.MapPost("/items/{type}/{id:guid}/links", async (string type, Guid id, AddBody body, Access access, HubDb db, TimeProvider clock) =>
        {
            Check.OneOf(type, ItemType.Linkable, "type");
            var o = await Owner(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(o.ProjectId);
            Access.Demand(CanAdd(type, access.Actor, ctx));
            var link = await Add(db, o.ProjectId, type, id, o.Key, body, access.Me.Id, clock.GetUtcNow());
            return Results.Created($"/api/v1/links/{link.Id}", new { link.Id, link.Title, link.Url, link.LinkType });
        });

        api.MapPatch("/links/{id:guid}", async (Guid id, System.Text.Json.JsonElement body, Access access, HubDb db) =>
        {
            var l = await db.DocumentLinks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(l.ProjectId);
            Access.Demand(CanChange(l, access.Actor, ctx) ? Allow.Yes : Allow.No("perm.owner"));
            var patch = new Patch(body);
            if (patch.Has("url")) { var url = Check.Required(patch.Str("url"), "url", 2000); Check.That(Links.IsValid(url), "url", "link.invalid"); l.Url = url; }
            if (patch.Has("title")) l.Title = Check.Required(patch.Str("title"), "title", 200);
            if (patch.Has("linkType")) { var lt = patch.Str("linkType"); Check.OneOf(lt, LinkType.All, "linkType"); l.LinkType = lt!; }
            l.AuditKey = (await Owner(db, l.ItemType, l.ItemId))?.Key;
            await db.SaveChangesAsync();
            return Results.Ok(new { l.Id, l.Title, l.Url, l.LinkType });
        });

        api.MapDelete("/links/{id:guid}", async (Guid id, Access access, HubDb db, TimeProvider clock) =>
        {
            var l = await db.DocumentLinks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(l.ProjectId);
            Access.Demand(CanChange(l, access.Actor, ctx) ? Allow.Yes : Allow.No("perm.owner"));
            l.DeletedAt = clock.GetUtcNow(); // DOC-03: soft delete, logged
            l.DeletedBy = access.Me.Id;
            l.AuditKey = (await Owner(db, l.ItemType, l.ItemId))?.Key;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
