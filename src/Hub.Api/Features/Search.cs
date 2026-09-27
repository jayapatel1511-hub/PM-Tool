using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Global search and key lookup (§18.1, FR-SRCH-01/02): numbers, names, keys, subjects and people, and since packet 020
/// task and deliverable descriptions and comments, with the matching text; only projects the caller may view (restricted
/// ones included only for their members, as everywhere); archived and cancelled projects on request.
public static partial class SearchEndpoints
{
    public static readonly string[] Groups = ["projects", "tasks", "deliverables", "milestones", "decisions", "handoffs", "reviews", "changes", "comments", "people"];

    [GeneratedRegex(@"@\[([^\]]+)\]\([0-9a-fA-F-]{36}\)")]
    private static partial Regex Mention();

    /// FR-004: about 80 characters around the first match, mention tokens shown as "@Name", or null when it is not there.
    public static string? Snippet(string? text, string term)
    {
        if (string.IsNullOrEmpty(text) || term.Length == 0) return null;
        var plain = Mention().Replace(text, "@$1").ReplaceLineEndings(" ");
        var i = plain.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (i < 0) return null;
        var (start, end) = (Math.Max(0, i - 40), Math.Min(plain.Length, i + term.Length + 40));
        return (start > 0 ? "…" : "") + plain[start..end].Trim() + (end < plain.Length ? "…" : "");
    }

    [GeneratedRegex(@"^[A-Za-z0-9][\w-]*-(T|D|M|DEC|R|I|A|H|RV|CH)\d+$", RegexOptions.IgnoreCase)]
    public static partial Regex KeyPattern();

    public static void Map(RouteGroupBuilder api) => api.MapGet("/search", Search);

    /// ILIKE patterns with the caller's text taken literally.
    static string Escape(string s) => s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    // ponytail: ILIKE scans; add pg_trgm GIN indexes on names and keys (§18.2) if search passes 1 s at scale (packet 011).
    static async Task<object> Search(string? q, string? type, bool? includeArchived, int? limit, int? page, Access access, HubDb db, CurrentUser me)
    {
        var term = (q ?? "").Trim();
        if (term.Length == 0) return new { q = term, exact = (object?)null, groups = new Dictionary<string, object>(), counts = new Dictionary<string, int>() };
        var (pg, size) = Http.Paging(page, limit ?? 5);
        var contains = $"%{Escape(term)}%";
        var prefix = $"{Escape(term)}%";
        var lower = term.ToLowerInvariant();
        var projects = access.VisibleProjects();
        if (includeArchived != true) projects = projects.Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        var ids = projects.Select(p => p.Id);
        var want = type is null ? Groups : Groups.Where(g => g == type).ToArray();
        var groups = new Dictionary<string, object>();
        var counts = new Dictionary<string, int>();
        bool Named(string key, string name) => key.Contains(term, StringComparison.OrdinalIgnoreCase) || name.Contains(term, StringComparison.OrdinalIgnoreCase);
        IQueryable<T> Page<T>(IQueryable<T> x) => x.Skip((pg - 1) * size).Take(size);

        // Ranking (§18.1): exact key, then number/key prefix, then name prefix, then the rest; active projects first; recency last.
        if (want.Contains("projects"))
        {
            var pq = projects.Where(p => EF.Functions.ILike(p.ProjectNumber, contains, @"\") || EF.Functions.ILike(p.Name, contains, @"\")
                || db.Clients.Any(c => c.Id == p.ClientId && EF.Functions.ILike(c.Name, contains, @"\")));
            counts["projects"] = await pq.CountAsync();
            groups["projects"] = await Page(pq.OrderBy(p => p.ProjectNumber.ToLower() == lower ? 0 : EF.Functions.ILike(p.ProjectNumber, prefix, @"\") ? 1 : EF.Functions.ILike(p.Name, prefix, @"\") ? 2 : 3)
                .ThenBy(p => p.Status == ProjectStatus.Active ? 0 : 1).ThenByDescending(p => p.UpdatedAt))
                .Select(p => new { p.Id, p.ProjectNumber, p.Name, p.Status, Client = db.Clients.Where(c => c.Id == p.ClientId).Select(c => c.Name).FirstOrDefault(),
                    Pm = db.Users.Where(u => u.Id == p.ProjectManagerId).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
        }
        if (want.Contains("tasks"))
        {
            var tq = db.Tasks.AsNoTracking().Where(t => ids.Contains(t.ProjectId)
                && (EF.Functions.ILike(t.Key, contains, @"\") || EF.Functions.ILike(t.Name, contains, @"\") || EF.Functions.ILike(t.Description!, contains, @"\")));
            counts["tasks"] = await tq.CountAsync();
            var tasks = await Page(tq.OrderBy(t => t.Key.ToLower() == lower ? 0 : EF.Functions.ILike(t.Key, prefix, @"\") ? 1 : EF.Functions.ILike(t.Name, prefix, @"\") ? 2
                    : EF.Functions.ILike(t.Name, contains, @"\") ? 3 : 4) // a match in the description ranks after names
                .ThenBy(t => db.Projects.Any(p => p.Id == t.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(t => t.LastActivityAt))
                .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.Description, ProjectNumber = db.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.ProjectNumber).First(),
                    Assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
            groups["tasks"] = tasks.Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.ProjectNumber, t.Assignee, Match = Named(t.Key, t.Name) ? null : Snippet(t.Description, term) });
        }
        if (want.Contains("deliverables"))
        {
            var dq = db.Deliverables.AsNoTracking().Where(d => ids.Contains(d.ProjectId)
                && (EF.Functions.ILike(d.Key, contains, @"\") || EF.Functions.ILike(d.Name, contains, @"\") || EF.Functions.ILike(d.Description!, contains, @"\")));
            counts["deliverables"] = await dq.CountAsync();
            var dels = await Page(dq.OrderBy(d => d.Key.ToLower() == lower ? 0 : EF.Functions.ILike(d.Key, prefix, @"\") ? 1 : EF.Functions.ILike(d.Name, prefix, @"\") ? 2
                    : EF.Functions.ILike(d.Name, contains, @"\") ? 3 : 4)
                .ThenBy(d => db.Projects.Any(p => p.Id == d.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(d => d.LastActivityAt))
                .Select(d => new { d.Id, d.Key, d.Name, d.Status, d.DueDate, d.Description, ProjectNumber = db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First(),
                    Owner = db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
            groups["deliverables"] = dels.Select(d => new { d.Id, d.Key, d.Name, d.Status, d.DueDate, d.ProjectNumber, d.Owner, Match = Named(d.Key, d.Name) ? null : Snippet(d.Description, term) });
        }
        if (want.Contains("milestones"))
        {
            var mq = db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId) && (EF.Functions.ILike(m.Key, contains, @"\") || EF.Functions.ILike(m.Name, contains, @"\")));
            counts["milestones"] = await mq.CountAsync();
            groups["milestones"] = await Page(mq.OrderBy(m => m.Key.ToLower() == lower ? 0 : EF.Functions.ILike(m.Key, prefix, @"\") ? 1 : EF.Functions.ILike(m.Name, prefix, @"\") ? 2 : 3)
                .ThenBy(m => db.Projects.Any(p => p.Id == m.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(m => m.UpdatedAt))
                .Select(m => new { m.Id, m.Key, m.Name, m.Date, m.IsComplete, m.IsCancelled, ProjectNumber = db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).First() }).ToListAsync();
        }
        if (want.Contains("decisions"))
        {
            var cq = db.Decisions.AsNoTracking().Where(d => ids.Contains(d.ProjectId) && (EF.Functions.ILike(d.Key, contains, @"\") || EF.Functions.ILike(d.Subject, contains, @"\")));
            counts["decisions"] = await cq.CountAsync();
            groups["decisions"] = await Page(cq.OrderBy(d => d.Key.ToLower() == lower ? 0 : EF.Functions.ILike(d.Key, prefix, @"\") ? 1 : EF.Functions.ILike(d.Subject, prefix, @"\") ? 2 : 3)
                .ThenBy(d => db.Projects.Any(p => p.Id == d.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(d => d.LastActivityAt))
                .Select(d => new { d.Id, d.Key, Name = d.Subject, d.Status, d.RequiredByDate, ProjectNumber = db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First() }).ToListAsync();
        }
        if (want.Contains("handoffs"))
        {
            var hq = db.Handoffs.AsNoTracking().Where(h => ids.Contains(h.ProjectId)
                && (EF.Functions.ILike(h.Key, contains, @"\") || EF.Functions.ILike(h.Title, contains, @"\")));
            counts["handoffs"] = await hq.CountAsync();
            groups["handoffs"] = await Page(hq.OrderBy(h => h.Key.ToLower() == lower ? 0 : 1).ThenBy(h => h.NeededBy))
                .Select(h => new { h.Id, h.Key, Name = h.Title, h.Status, DueDate = h.NeededBy,
                    ProjectNumber = db.Projects.Where(p => p.Id == h.ProjectId).Select(p => p.ProjectNumber).First(),
                    Owner = db.Users.Where(u => u.Id == h.ReceivingOwnerId).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
        }
        if (want.Contains("reviews")) {
            var rq = db.ReviewPackages.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["reviews"] = await rq.CountAsync();
            groups["reviews"] = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt))
                .Select(x => new { x.Id, x.Key, Name = x.Title, x.Status, ProjectNumber = db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First() }).ToListAsync();
        }
        if (want.Contains("changes")) {
            var rq = db.ChangeNotices.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["changes"] = await rq.CountAsync();
            groups["changes"] = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt))
                .Select(x => new { x.Id, x.Key, Name = x.Title, x.Status, ProjectNumber = db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First() }).ToListAsync();
        }
        if (want.Contains("comments")) // FR-004: never deleted ones
        {
            var cq = db.Comments.AsNoTracking().Where(c => c.DeletedAt == null && ids.Contains(c.ProjectId) && EF.Functions.ILike(c.Body, contains, @"\"));
            counts["comments"] = await cq.CountAsync();
            var rows = await Page(cq.OrderByDescending(c => c.CreatedAt)).Select(c => new
            {
                c.Id, c.ItemType, c.ItemId, c.Body, c.CreatedAt, Author = db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.DisplayName).FirstOrDefault(),
                ProjectNumber = db.Projects.Where(p => p.Id == c.ProjectId).Select(p => p.ProjectNumber).First(),
            }).ToListAsync();
            var items = await Items(db, rows.Select(r => r.ItemId).ToList());
            // ponytail: a comment on a deleted item is dropped from the page but still counted; filter in SQL if that matters
            groups["comments"] = rows.Where(r => items.ContainsKey(r.ItemId)).Select(r => new
            {
                r.Id, r.ItemType, r.ItemId, items[r.ItemId].Key, items[r.ItemId].Name, r.ProjectNumber, r.Author, r.CreatedAt, Match = Snippet(r.Body, term),
            }).ToList();
        }
        if (want.Contains("people"))
        {
            var a = access.Actor;
            var all = a.Admin || a.Executive;
            // People whose My Work the caller may open: themselves, direct reports, everyone for Executives and Admins, and
            // members of projects the caller manages (the PM view of §13.10, AC-MYW-04).
            var managed = db.ProjectMembers.Where(m => m.RemovedAt == null && db.Projects.Any(p => p.Id == m.ProjectId && p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled
                && (p.ProjectManagerId == me.Id || db.ProjectMembers.Any(x => x.ProjectId == p.Id && x.UserId == me.Id && x.RemovedAt == null && x.Roles.Contains(ProjectRole.PM))))).Select(m => m.UserId);
            var uq = db.Users.AsNoTracking().Where(u => EF.Functions.ILike(u.DisplayName, contains, @"\") || EF.Functions.ILike(u.Email, contains, @"\"));
            counts["people"] = await uq.CountAsync();
            groups["people"] = await Page(uq.OrderBy(u => u.IsActive ? 0 : 1).ThenBy(u => EF.Functions.ILike(u.DisplayName, prefix, @"\") || EF.Functions.ILike(u.Email, prefix, @"\") ? 0 : 1).ThenBy(u => u.DisplayName))
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.JobTitle, u.IsActive,
                    CanViewWork = all || u.Id == me.Id || (a.Supervisor && u.SupervisorId == me.Id) || managed.Contains(u.Id) }).ToListAsync();
        }
        return new { q = term, exact = await Exact(db, projects, term), groups, counts };
    }

    /// The key and name of each commented item that still exists.
    static async Task<Dictionary<Guid, (string Key, string Name)>> Items(HubDb db, List<Guid> ids)
    {
        var d = new Dictionary<Guid, (string, string)>();
        foreach (var (id, key, name) in (await db.Tasks.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name))
            .Concat((await db.Deliverables.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name)))
            .Concat((await db.Milestones.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name)))
            .Concat((await db.Decisions.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, Name = x.Subject }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name)))
            .Concat((await db.Risks.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, Name = x.Title }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name)))
            .Concat((await db.Issues.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, Name = x.Title }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name)))
            .Concat((await db.Actions.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, Name = x.Text }).ToListAsync()).Select(x => (x.Id, x.Key, x.Name))))
            d[id] = (key, name);
        return d;
    }

    /// FR-SRCH-02: a value shaped like an item key (or a project number) that matches exactly opens that item.
    static async Task<object?> Exact(HubDb db, IQueryable<Project> projects, string term)
    {
        var ids = projects.Select(p => p.Id);
        var number = await projects.Where(p => p.ProjectNumber.ToLower() == term.ToLower()).Select(p => p.ProjectNumber).FirstOrDefaultAsync();
        if (number is not null) return new { type = ItemType.Project, id = (Guid?)null, projectNumber = number, key = number };
        if (!KeyPattern().IsMatch(term)) return null;
        var key = term.ToUpperInvariant();
        var hit = await db.Tasks.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Task, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Deliverables.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Deliverable, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Milestones.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Milestone, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Decisions.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Decision, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Risks.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Risk, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Issues.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Issue, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Actions.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = ItemType.Action, t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync()
            ?? await db.Handoffs.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = "Handoff", t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync();
        hit ??= await db.ReviewPackages.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = "ReviewPackage", t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync();
        hit ??= await db.ChangeNotices.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new { type = "ChangeNotice", t.Id, t.ProjectId, t.Key }).FirstOrDefaultAsync();
        if (hit is null) return null;
        return new { hit.type, id = (Guid?)hit.Id, projectNumber = await db.Projects.Where(p => p.Id == hit.ProjectId).Select(p => p.ProjectNumber).FirstAsync(), hit.Key };
    }
}
