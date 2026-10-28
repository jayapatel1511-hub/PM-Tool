using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Api.Records;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Global search and key lookup (§18.1, FR-SRCH-01/02): numbers, names, keys, subjects and people, and since packet 020
/// task and deliverable descriptions and comments, with the matching text; only projects the caller may view (restricted
/// ones included only for their members, as everywhere); archived and cancelled projects on request.
public static partial class SearchEndpoints
{
    public static readonly string[] Groups = [.. ScreenLists.SearchGroups];

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

    [GeneratedRegex(@"^[A-Za-z0-9][\w-]*-(T|D|M|DEC|R|I|A|H|RV|CH|CT|WC|SUB|B)\d+$", RegexOptions.IgnoreCase)]
    public static partial Regex KeyPattern();

    // One record per row shape of GET /search, in the property order each group has always written. The generated client reads these names.
    public sealed record SearchProjectHit(Guid Id, string ProjectNumber, string Name, string Status, string? Client, string? Pm);
    /// Match is the snippet around the text that matched in the description, null when the key or name matched.
    public sealed record SearchTaskHit(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, string ProjectNumber, string? Assignee, string? Match);
    public sealed record SearchDeliverableHit(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, string ProjectNumber, string? Owner, string? Match);
    public sealed record SearchMilestoneHit(Guid Id, string Key, string Name, DateOnly? Date, bool IsComplete, bool IsCancelled, string ProjectNumber);
    public sealed record SearchDecisionHit(Guid Id, string Key, string Name, string Status, DateOnly RequiredByDate, string ProjectNumber);
    /// Handoffs, readiness constraints and weekly promises: a status, the date the work is needed by and the person who owns it.
    public sealed record SearchOwnedHit(Guid Id, string Key, string Name, string Status, DateOnly DueDate, string ProjectNumber, string? Owner);
    /// Review packages and change notices.
    public sealed record SearchStatusHit(Guid Id, string Key, string Name, string Status, string ProjectNumber);
    public sealed record SearchSubmissionHit(Guid Id, string Key, string Name, DateOnly DueDate, string ProjectNumber, string? Owner);
    public sealed record SearchAllocationHit(Guid Id, string Name, string Status, DateOnly DueDate, string ProjectNumber);
    public sealed record SearchBasisHit(Guid Id, string Key, string Name, string ProjectNumber, string? Owner);
    /// A live comment with the item it sits on and the snippet that matched.
    public sealed record SearchCommentHit(Guid Id, string ItemType, Guid ItemId, string Key, string Name, string ProjectNumber, string? Author, DateTimeOffset CreatedAt, string? Match);
    public sealed record SearchPlanningHit(Guid Id, string Name, string Status, DateOnly DueDate, DateOnly StartWeek, Guid PersonId);
    public sealed record SearchPersonHit(Guid Id, string DisplayName, string Email, string? JobTitle, bool IsActive, bool CanViewWork);

    /// The groups the request asked for, in the order they are written: all sixteen, or the one named by `type`. A group not asked for is left out; one with no matches is [].
    public sealed record SearchGroups(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchProjectHit>? Projects = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchTaskHit>? Tasks = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchDeliverableHit>? Deliverables = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchMilestoneHit>? Milestones = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchDecisionHit>? Decisions = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchOwnedHit>? Handoffs = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchStatusHit>? Reviews = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchStatusHit>? Changes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchSubmissionHit>? Submissions = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchAllocationHit>? Allocations = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull), JsonPropertyName("design-basis")] IReadOnlyList<SearchBasisHit>? DesignBasis = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchOwnedHit>? Constraints = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchOwnedHit>? Commitments = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchCommentHit>? Comments = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchPlanningHit>? Planning = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<SearchPersonHit>? People = null);

    /// FR-SRCH-02: the item (or project) whose key or number the whole term spells; Id is null for a project.
    public sealed record SearchExact(string Type, Guid? Id, string ProjectNumber, string Key);
    /// Counts holds the total matches of each group asked for, not just the page returned.
    public sealed record SearchResponse(string Q, SearchExact? Exact, SearchGroups Groups, IReadOnlyDictionary<string, int> Counts);

    sealed record SearchCommentRow(Guid Id, string ItemType, Guid ItemId, string Body, DateTimeOffset CreatedAt, string? Author, string ProjectNumber);
    sealed record SearchItemRow(Guid Id, string Key, string Name);
    sealed record SearchFound(string Type, Guid Id, Guid ProjectId, string Key);

    public static void Map(RouteGroupBuilder api) => api.MapGet("/search", Search);

    /// ILIKE patterns with the caller's text taken literally.
    internal static string Escape(string s) => s.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");

    // ponytail: ILIKE scans; add pg_trgm GIN indexes on names and keys (§18.2) if search passes 1 s at scale (packet 011).
    static async Task<Ok<SearchResponse>> Search(string? q, string? type, bool? includeArchived, int? limit, int? page, Access access, HubDb db, CurrentUser me)
    {
        var term = (q ?? "").Trim();
        if (term.Length == 0) return TypedResults.Ok(new SearchResponse(term, null, new SearchGroups(), new Dictionary<string, int>()));
        var (pg, size) = Http.Paging(page, limit ?? 5);
        var contains = $"%{Escape(term)}%";
        var prefix = $"{Escape(term)}%";
        var lower = term.ToLowerInvariant();
        var projects = access.VisibleProjects();
        if (includeArchived != true) projects = projects.Where(p => p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled);
        var ids = projects.Select(p => p.Id);
        var want = type is null ? Groups : Groups.Where(g => g == type).ToArray();
        var groups = new SearchGroups();
        var counts = new Dictionary<string, int>();
        bool Named(string key, string name) => key.Contains(term, StringComparison.OrdinalIgnoreCase) || name.Contains(term, StringComparison.OrdinalIgnoreCase);
        IQueryable<T> Page<T>(IQueryable<T> x) => x.Skip((pg - 1) * size).Take(size);

        // Ranking (§18.1): exact key, then number/key prefix, then name prefix, then the rest; active projects first; recency last.
        if (want.Contains("projects"))
        {
            var pq = projects.Where(p => EF.Functions.ILike(p.ProjectNumber, contains, @"\") || EF.Functions.ILike(p.Name, contains, @"\")
                || db.Clients.Any(c => c.Id == p.ClientId && EF.Functions.ILike(c.Name, contains, @"\")));
            counts["projects"] = await pq.CountAsync();
            groups = groups with { Projects = await Page(pq.OrderBy(p => p.ProjectNumber.ToLower() == lower ? 0 : EF.Functions.ILike(p.ProjectNumber, prefix, @"\") ? 1 : EF.Functions.ILike(p.Name, prefix, @"\") ? 2 : 3)
                .ThenBy(p => p.Status == ProjectStatus.Active ? 0 : 1).ThenByDescending(p => p.UpdatedAt))
                .Select(p => new SearchProjectHit(p.Id, p.ProjectNumber, p.Name, p.Status, db.Clients.Where(c => c.Id == p.ClientId).Select(c => c.Name).FirstOrDefault(),
                    db.Users.Where(u => u.Id == p.ProjectManagerId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("tasks"))
        {
            var tq = db.Tasks.AsNoTracking().Where(t => ids.Contains(t.ProjectId)
                && (EF.Functions.ILike(t.Key, contains, @"\") || EF.Functions.ILike(t.Name, contains, @"\") || EF.Functions.ILike(t.Description!, contains, @"\")));
            counts["tasks"] = await tq.CountAsync();
            var tasks = await Page(tq.OrderBy(t => t.Key.ToLower() == lower ? 0 : EF.Functions.ILike(t.Key, prefix, @"\") ? 1 : EF.Functions.ILike(t.Name, prefix, @"\") ? 2
                    : EF.Functions.ILike(t.Name, contains, @"\") ? 3 : 4) // a match in the description ranks after names
                .ThenBy(t => db.Projects.Any(p => p.Id == t.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(t => t.LastActivityAt))
                .Select(t => new SearchTaskHit(t.Id, t.Key, t.Name, t.Status, t.DueDate, db.Projects.Where(p => p.Id == t.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(), t.Description)).ToListAsync(); // Match carries the description until the snippet is cut
            groups = groups with { Tasks = tasks.Select(t => t with { Match = Named(t.Key, t.Name) ? null : Snippet(t.Match, term) }).ToList() };
        }
        if (want.Contains("deliverables"))
        {
            var dq = db.Deliverables.AsNoTracking().Where(d => ids.Contains(d.ProjectId)
                && (EF.Functions.ILike(d.Key, contains, @"\") || EF.Functions.ILike(d.Name, contains, @"\") || EF.Functions.ILike(d.Description!, contains, @"\")));
            counts["deliverables"] = await dq.CountAsync();
            var dels = await Page(dq.OrderBy(d => d.Key.ToLower() == lower ? 0 : EF.Functions.ILike(d.Key, prefix, @"\") ? 1 : EF.Functions.ILike(d.Name, prefix, @"\") ? 2
                    : EF.Functions.ILike(d.Name, contains, @"\") ? 3 : 4)
                .ThenBy(d => db.Projects.Any(p => p.Id == d.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(d => d.LastActivityAt))
                .Select(d => new SearchDeliverableHit(d.Id, d.Key, d.Name, d.Status, d.DueDate, db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), d.Description)).ToListAsync(); // Match carries the description until the snippet is cut
            groups = groups with { Deliverables = dels.Select(d => d with { Match = Named(d.Key, d.Name) ? null : Snippet(d.Match, term) }).ToList() };
        }
        if (want.Contains("milestones"))
        {
            var mq = db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId) && (EF.Functions.ILike(m.Key, contains, @"\") || EF.Functions.ILike(m.Name, contains, @"\")));
            counts["milestones"] = await mq.CountAsync();
            groups = groups with { Milestones = await Page(mq.OrderBy(m => m.Key.ToLower() == lower ? 0 : EF.Functions.ILike(m.Key, prefix, @"\") ? 1 : EF.Functions.ILike(m.Name, prefix, @"\") ? 2 : 3)
                .ThenBy(m => db.Projects.Any(p => p.Id == m.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(m => m.UpdatedAt))
                .Select(m => new SearchMilestoneHit(m.Id, m.Key, m.Name, m.Date, m.IsComplete, m.IsCancelled, db.Projects.Where(p => p.Id == m.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync() };
        }
        if (want.Contains("decisions"))
        {
            var cq = db.Decisions.AsNoTracking().Where(d => ids.Contains(d.ProjectId) && (EF.Functions.ILike(d.Key, contains, @"\") || EF.Functions.ILike(d.Subject, contains, @"\")));
            counts["decisions"] = await cq.CountAsync();
            groups = groups with { Decisions = await Page(cq.OrderBy(d => d.Key.ToLower() == lower ? 0 : EF.Functions.ILike(d.Key, prefix, @"\") ? 1 : EF.Functions.ILike(d.Subject, prefix, @"\") ? 2 : 3)
                .ThenBy(d => db.Projects.Any(p => p.Id == d.ProjectId && p.Status == ProjectStatus.Active) ? 0 : 1).ThenByDescending(d => d.LastActivityAt))
                .Select(d => new SearchDecisionHit(d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, db.Projects.Where(p => p.Id == d.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync() };
        }
        if (want.Contains("handoffs"))
        {
            var hq = db.Handoffs.AsNoTracking().Where(h => ids.Contains(h.ProjectId)
                && (EF.Functions.ILike(h.Key, contains, @"\") || EF.Functions.ILike(h.Title, contains, @"\")));
            counts["handoffs"] = await hq.CountAsync();
            groups = groups with { Handoffs = await Page(hq.OrderBy(h => h.Key.ToLower() == lower ? 0 : 1).ThenBy(h => h.NeededBy))
                .Select(h => new SearchOwnedHit(h.Id, h.Key, h.Title, h.Status, h.NeededBy,
                    db.Projects.Where(p => p.Id == h.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == h.ReceivingOwnerId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("reviews")) {
            var rq = db.ReviewPackages.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["reviews"] = await rq.CountAsync();
            groups = groups with { Reviews = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt))
                .Select(x => new SearchStatusHit(x.Id, x.Key, x.Title, x.Status, db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync() };
        }
        if (want.Contains("changes")) {
            var rq = db.ChangeNotices.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["changes"] = await rq.CountAsync();
            groups = groups with { Changes = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt))
                .Select(x => new SearchStatusHit(x.Id, x.Key, x.Title, x.Status, db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync() };
        }
        if (want.Contains("submissions")) {
            var rq = db.SubmissionPackages.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["submissions"] = await rq.CountAsync();
            groups = groups with { Submissions = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id))
                .Select(x => new SearchSubmissionHit(x.Id, x.Key, x.Title, x.TargetDate,
                    db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == x.CoordinatorId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("allocations")) {
            var actor = access.Actor;
            var managed = actor.Admin ? ids : projects.Where(p => p.ProjectManagerId == actor.Id
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == actor.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM))).Select(p => p.Id);
            var rq = AllocationEndpoints.VisibleQuery(db, actor, managed).Where(x => ids.Contains(x.ProjectId)
                && (EF.Functions.ILike(x.Purpose, contains, @"\") || db.Users.Any(u => u.Id == x.PersonId && EF.Functions.ILike(u.DisplayName, contains, @"\"))));
            counts["allocations"] = await rq.CountAsync();
            groups = groups with { Allocations = await Page(rq.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id)).Select(x => new SearchAllocationHit(
                x.Id, db.Users.Where(u => u.Id == x.PersonId).Select(u => u.DisplayName).FirstOrDefault() + " · " + x.Purpose,
                x.Status, x.ThroughDate, db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync() };
        }
        if (want.Contains("design-basis")) {
            var rq = db.DesignBasisEntries.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Title, contains, @"\")));
            counts["design-basis"] = await rq.CountAsync();
            groups = groups with { DesignBasis = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.UpdatedAt).ThenBy(x => x.Id))
                .Select(x => new SearchBasisHit(x.Id, x.Key, x.Title,
                    db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == x.OwnerId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("constraints")) { // packet 032: open the work's readiness inspector
            var rq = db.WorkConstraints.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.Description, contains, @"\")));
            counts["constraints"] = await rq.CountAsync();
            groups = groups with { Constraints = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenBy(x => x.NeededBy))
                .Select(x => new SearchOwnedHit(x.Id, x.Key, x.Description, x.State, x.NeededBy,
                    db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == x.RemovalOwnerId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("commitments")) {
            var rq = db.OutputCommitments.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && (EF.Functions.ILike(x.Key, contains, @"\") || EF.Functions.ILike(x.IntendedOutput, contains, @"\")));
            counts["commitments"] = await rq.CountAsync();
            groups = groups with { Commitments = await Page(rq.OrderBy(x => x.Key.ToLower() == lower ? 0 : 1).ThenByDescending(x => x.WeekStart))
                .Select(x => new SearchOwnedHit(x.Id, x.Key, x.IntendedOutput, x.State, x.TargetDate,
                    db.Projects.Where(p => p.Id == x.ProjectId).Select(p => p.ProjectNumber).First(),
                    db.Users.Where(u => u.Id == x.PerformerId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync() };
        }
        if (want.Contains("comments")) // FR-004: never deleted ones
        {
            var cq = db.Comments.AsNoTracking().Where(c => c.DeletedAt == null && ids.Contains(c.ProjectId) && EF.Functions.ILike(c.Body, contains, @"\"));
            counts["comments"] = await cq.CountAsync();
            var rows = await Page(cq.OrderByDescending(c => c.CreatedAt)).Select(c => new SearchCommentRow(c.Id, c.ItemType, c.ItemId, c.Body, c.CreatedAt,
                db.Users.Where(u => u.Id == c.AuthorId).Select(u => u.DisplayName).FirstOrDefault(),
                db.Projects.Where(p => p.Id == c.ProjectId).Select(p => p.ProjectNumber).First())).ToListAsync();
            var items = await Items(db, rows.Select(r => r.ItemId).ToList());
            // ponytail: a comment on a deleted item is dropped from the page but still counted; filter in SQL if that matters
            groups = groups with { Comments = rows.Where(r => items.ContainsKey(r.ItemId)).Select(r => new SearchCommentHit(r.Id, r.ItemType, r.ItemId,
                items[r.ItemId].Key, items[r.ItemId].Name, r.ProjectNumber, r.Author, r.CreatedAt, Snippet(r.Body, term))).ToList() };
        }
        if (want.Contains("planning"))
        {
            var pq = (await PlanningEndpoints.VisibleEntries(db, access)).Where(e => EF.Functions.ILike(e.Label, contains, @"\")
                || e.Notes != null && EF.Functions.ILike(e.Notes, contains, @"\"));
            counts["planning"] = await pq.CountAsync();
            groups = groups with { Planning = await Page(pq.OrderBy(e => e.StartWeek)).Select(e => new SearchPlanningHit(
                e.Id, db.Users.Where(u => u.Id == e.PersonId).Select(u => u.DisplayName).First() + " · " + e.Label,
                e.CreatedBy == e.PersonId ? "Self" : e.Visibility, e.EndWeek, e.StartWeek, e.PersonId)).ToListAsync() };
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
            groups = groups with { People = await Page(uq.OrderBy(u => u.IsActive ? 0 : 1).ThenBy(u => EF.Functions.ILike(u.DisplayName, prefix, @"\") || EF.Functions.ILike(u.Email, prefix, @"\") ? 0 : 1).ThenBy(u => u.DisplayName))
                .Select(u => new SearchPersonHit(u.Id, u.DisplayName, u.Email, u.JobTitle, u.IsActive,
                    all || u.Id == me.Id || (a.Supervisor && u.SupervisorId == me.Id) || managed.Contains(u.Id))).ToListAsync() };
        }
        return TypedResults.Ok(new SearchResponse(term, await Exact(db, projects, term), groups, counts));
    }

    /// The key and name of each commented item that still exists.
    static async Task<Dictionary<Guid, (string Key, string Name)>> Items(HubDb db, List<Guid> ids)
    {
        var d = new Dictionary<Guid, (string, string)>();
        foreach (var (id, key, name) in (await db.Tasks.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Name)).ToListAsync())
            .Concat(await db.Deliverables.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Name)).ToListAsync())
            .Concat(await db.Milestones.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Name)).ToListAsync())
            .Concat(await db.Decisions.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Subject)).ToListAsync())
            .Concat(await db.Risks.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Title)).ToListAsync())
            .Concat(await db.Issues.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Title)).ToListAsync())
            .Concat(await db.Actions.Where(x => ids.Contains(x.Id)).Select(x => new SearchItemRow(x.Id, x.Key, x.Text)).ToListAsync()))
            d[id] = (key, name);
        return d;
    }

    /// FR-SRCH-02: a value shaped like an item key (or a project number) that matches exactly opens that item.
    static async Task<SearchExact?> Exact(HubDb db, IQueryable<Project> projects, string term)
    {
        var ids = projects.Select(p => p.Id);
        var number = await projects.Where(p => p.ProjectNumber.ToLower() == term.ToLower()).Select(p => p.ProjectNumber).FirstOrDefaultAsync();
        if (number is not null) return new SearchExact(ItemType.Project, null, number, number);
        if (!KeyPattern().IsMatch(term)) return null;
        var key = term.ToUpperInvariant();
        var hit = await db.Tasks.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Task, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Deliverables.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Deliverable, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Milestones.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Milestone, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Decisions.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Decision, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Risks.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Risk, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Issues.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Issue, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Actions.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound(ItemType.Action, t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync()
            ?? await db.Handoffs.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("Handoff", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.ReviewPackages.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("ReviewPackage", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.ChangeNotices.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("ChangeNotice", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.SubmissionPackages.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("SubmissionPackage", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.DesignBasisEntries.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("DesignBasisEntry", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.WorkConstraints.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("WorkConstraint", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        hit ??= await db.OutputCommitments.Where(t => ids.Contains(t.ProjectId) && t.Key.ToUpper() == key).Select(t => new SearchFound("OutputCommitment", t.Id, t.ProjectId, t.Key)).FirstOrDefaultAsync();
        if (hit is null) return null;
        return new SearchExact(hit.Type, hit.Id, await db.Projects.Where(p => p.Id == hit.ProjectId).Select(p => p.ProjectNumber).FirstAsync(), hit.Key);
    }
}
