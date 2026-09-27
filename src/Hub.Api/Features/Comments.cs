using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Comments with @mentions (§12.8, C-01..C-08, FR-COM-01..03). Review and Status Note comments are written by the
/// task and deliverable transitions; everything else about an item lives in its History.
public static partial class CommentEndpoints
{
    public sealed record CommentBody(string Body);

    /// Mentions are stored in the text as `@[Display Name](user-id)`, inserted by the composer's people picker.
    [GeneratedRegex(@"@\[([^\]\n]{1,120})\]\(([0-9a-fA-F-]{36})\)")]
    private static partial Regex MentionToken();

    public static List<Guid> Mentions(string body) => MentionToken().Matches(body).Select(m => Guid.Parse(m.Groups[2].Value)).Distinct().ToList();

    public sealed record Target(Guid ProjectId, string Key, string Name, string Link, Guid?[] Interested);

    /// The commented item: its project, key, deep link and the people who own or watch it (§17.2 "owner, watchers").
    public static async Task<Target?> Item(HubDb db, string type, Guid id)
    {
        var watchers = await db.Watchers.Where(w => w.ItemType == type && w.ItemId == id).Select(w => (Guid?)w.UserId).ToListAsync();
        async Task<string> Num(Guid pid) => await db.Projects.Where(p => p.Id == pid).Select(p => p.ProjectNumber).FirstAsync();
        switch (type)
        {
            case ItemType.Task:
                var t = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (t is null) return null;
                var collaborators = await db.Collaborators.Where(c => c.TaskId == id).Select(c => (Guid?)c.UserId).ToListAsync();
                return new(t.ProjectId, t.Key, t.Name, $"/projects/{await Num(t.ProjectId)}/tasks?panel=Task:{id}", [t.AssigneeId, t.ReviewerId, .. collaborators, .. watchers]);
            case ItemType.Deliverable:
                var d = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return d is null ? null : new(d.ProjectId, d.Key, d.Name, $"/projects/{await Num(d.ProjectId)}/deliverables?panel=Deliverable:{id}", [d.OwnerId, d.ReviewerId, .. watchers]);
            case ItemType.Milestone:
                var m = await db.Milestones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                if (m is null) return null;
                var pm = await db.Projects.Where(p => p.Id == m.ProjectId).Select(p => (Guid?)p.ProjectManagerId).FirstAsync();
                return new(m.ProjectId, m.Key, m.Name, $"/projects/{await Num(m.ProjectId)}/milestones?panel=Milestone:{id}", [pm, .. watchers]);
            case ItemType.Decision:
                var dec = await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return dec is null ? null : new(dec.ProjectId, dec.Key, dec.Subject, $"/projects/{await Num(dec.ProjectId)}/decisions?panel=Decision:{id}", [dec.OwnerUserId, dec.RequestedById, .. watchers]);
            case ItemType.Risk:
                var r = await db.Risks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return r is null ? null : new(r.ProjectId, r.Key, r.Title, $"/projects/{await Num(r.ProjectId)}/risks?panel=Risk:{id}", [r.OwnerId, .. watchers]);
            case ItemType.Issue:
                var i = await db.Issues.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return i is null ? null : new(i.ProjectId, i.Key, i.Title, $"/projects/{await Num(i.ProjectId)}/issues?panel=Issue:{id}", [i.OwnerId, .. watchers]);
            case ItemType.Action:
                var a = await db.Actions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
                return a is null ? null : new(a.ProjectId, a.Key, a.Text, $"/projects/{await Num(a.ProjectId)}/meetings?panel=Action:{id}", [a.OwnerUserId, .. watchers]);
            default:
                return null;
        }
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/items/{type}/{id:guid}/comments", async (string type, Guid id, Access access, HubDb db, TimeProvider clock) =>
        {
            Check.OneOf(type, ItemType.Commentable, "type");
            var target = await Item(db, type, id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(target.ProjectId, track: false);
            var a = access.Actor;
            var now = clock.GetUtcNow();
            var list = await db.Comments.AsNoTracking().Where(c => c.ItemType == type && c.ItemId == id).OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync();
            var ids = list.Select(c => c.Id).ToList();
            var mentions = await db.Mentions.Where(m => ids.Contains(m.CommentId)).ToListAsync();
            var people = list.Select(c => c.AuthorId).Concat(mentions.Select(m => m.UserId)).Distinct().ToList();
            var names = await db.Users.Where(u => people.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)");
            var readOnly = ProjectStatus.IsReadOnly(ctx.Status);
            return new
            {
                CanComment = Permissions.Comment(a, ctx).Ok,
                CommentReason = Permissions.Comment(a, ctx) is { Ok: false } no ? Text.Get(no.Why!, no.Arg ?? ctx.Status) : null,
                Items = list.Select(c =>
                {
                    var deleted = c.DeletedAt is not null;
                    return new
                    {
                        c.Id, c.AuthorId, AuthorName = names.GetValueOrDefault(c.AuthorId), c.CommentKind, c.ReviewRound, c.CreatedAt, c.EditedAt,
                        Deleted = deleted, c.DeletedByPm,
                        Body = deleted ? (a.Admin ? c.Body : null) : c.Body, // C-03: the text stays for Admin audit only
                        Mentions = mentions.Where(m => m.CommentId == c.Id).Select(m => new { Id = m.UserId, Name = names.GetValueOrDefault(m.UserId) }),
                        CanEdit = !deleted && !readOnly && c.CommentKind == CommentKind.General && Permissions.EditComment(a, c.AuthorId, c.CreatedAt, now).Ok,
                        CanDelete = !deleted && !readOnly && Permissions.DeleteComment(a, ctx, c.AuthorId).Ok,
                    };
                }),
            };
        });

        api.MapPost("/items/{type}/{id:guid}/comments", async (string type, Guid id, CommentBody body, Access access, HubDb db, Notifier notify, TimeProvider clock) =>
        {
            Check.OneOf(type, ItemType.Commentable, "type");
            var target = await Item(db, type, id) ?? throw ApiException.NotFound();
            var (p, ctx) = await access.Project(target.ProjectId);
            Access.Demand(Permissions.Comment(access.Actor, ctx));
            var text = Check.Required(body.Body, "body", 8000);
            var c = new Comment { ProjectId = p.Id, ItemType = type, ItemId = id, AuthorId = access.Me.Id, Body = text, CreatedAt = clock.GetUtcNow(), AuditKey = target.Key };
            db.Comments.Add(c);
            var mentioned = await AddMentions(db, access, p, ctx, c, text, target);
            await db.SaveChangesAsync();
            await Notices(db, notify, p, type, id, target, mentioned, []);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/comments/{c.Id}", new { c.Id });
        });

        api.MapPatch("/comments/{id:guid}", async (Guid id, CommentBody body, Access access, HubDb db, Notifier notify, TimeProvider clock) =>
        {
            var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (p, ctx) = await access.Project(c.ProjectId);
            if (ProjectStatus.IsReadOnly(ctx.Status)) throw ApiException.Forbidden("perm.project_read_only", ctx.Status);
            if (c.DeletedAt is not null || c.CommentKind != CommentKind.General) throw ApiException.Rule("comment_locked", "comment.locked");
            Access.Demand(Permissions.EditComment(access.Actor, c.AuthorId, c.CreatedAt, clock.GetUtcNow())); // C-02
            var target = await Item(db, c.ItemType, c.ItemId) ?? throw ApiException.NotFound();
            var before = (await db.Mentions.Where(m => m.CommentId == id).Select(m => m.UserId).ToListAsync()).ToHashSet();
            c.Body = Check.Required(body.Body, "body", 8000);
            c.EditedAt = clock.GetUtcNow();
            await db.Mentions.Where(m => m.CommentId == id).ExecuteDeleteAsync();
            var mentioned = await AddMentions(db, access, p, ctx, c, c.Body, target);
            await db.SaveChangesAsync();
            await Notices(db, notify, p, c.ItemType, c.ItemId, target, mentioned.Where(m => !before.Contains(m)).ToList(), [], commentOnItem: false);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        api.MapDelete("/comments/{id:guid}", async (Guid id, Access access, HubDb db, TimeProvider clock) =>
        {
            var c = await db.Comments.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(c.ProjectId);
            if (ProjectStatus.IsReadOnly(ctx.Status)) throw ApiException.Forbidden("perm.project_read_only", ctx.Status);
            if (c.DeletedAt is not null) return Results.NoContent();
            Access.Demand(Permissions.DeleteComment(access.Actor, ctx, c.AuthorId)); // C-03, C-04
            c.DeletedAt = clock.GetUtcNow();
            c.DeletedBy = access.Me.Id;
            c.DeletedByPm = c.AuthorId != access.Me.Id;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    /// C-05, C-06: mentions resolve to active people; each becomes a watcher; non-members are notified but not added to
    /// the team, and the PM sees "mentioned non-member" in activity. On a Restricted project only people who can see it
    /// may be mentioned.
    static async Task<List<Guid>> AddMentions(HubDb db, Access access, Project p, ProjectContext ctx, Comment c, string text, Target target)
    {
        var ids = Mentions(text).Where(x => x != access.Me.Id).ToList();
        if (ids.Count == 0) return [];
        var people = await db.Users.Where(u => ids.Contains(u.Id) && u.IsActive).Select(u => new { u.Id, u.DisplayName }).ToListAsync();
        var members = (await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.RemovedAt == null && ids.Contains(m.UserId)).Select(m => m.UserId).ToListAsync()).ToHashSet();
        foreach (var u in people)
        {
            var member = members.Contains(u.Id) || u.Id == p.ProjectManagerId;
            if (!member && p.Visibility == Visibility.Restricted)
                throw ApiException.Invalid("body", "comment.mention_restricted", u.DisplayName);
            db.Mentions.Add(new CommentMention { CommentId = c.Id, UserId = u.Id });
            await TaskEndpoints.Watch(db, p.Id, c.ItemType, c.ItemId, u.Id, "Mention");
            if (!member)
                db.LogEvent(ItemType.Comment, c.Id, "MentionedNonMember", "collaboration", p.Id, target.Key, u.DisplayName,
                    new[] { new { field = "UserId", old = (object?)null, @new = (object)u.Id } });
        }
        return people.Select(u => u.Id).ToList();
    }

    static async Task Notices(HubDb db, Notifier notify, Project p, string type, Guid id, Target target, List<Guid> mentioned, List<Guid> skip, bool commentOnItem = true)
    {
        var actor = await notify.ActorName();
        var item = new NotifyItem(p.Id, type, id, target.Key, target.Link, p.ProjectNumber);
        if (mentioned.Count > 0)
            await notify.Send(NotificationEvents.Mention, mentioned.Cast<Guid?>(), item, Text.Get("notify.mention", actor, target.Key, target.Name));
        if (commentOnItem)
            await notify.Send(NotificationEvents.CommentOnItem, target.Interested.Where(x => x is { } g && !mentioned.Contains(g) && !skip.Contains(g)), item,
                Text.Get("notify.comment", actor, target.Key, target.Name));
    }
}
