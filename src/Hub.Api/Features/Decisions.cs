using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Decision Register (§12.9, §13.8, DEC-01..DEC-07, FR-DEC-01..04) and project external parties (FR-ORG-08).
public static class DecisionEndpoints
{
    public sealed record LinkInput(string TargetType, Guid TargetId, string? Relation);
    public sealed record CreateBody(string Subject, string Description, Guid? RequestedById, Guid? OwnerUserId, Guid? OwnerExternalPartyId, DateOnly? DateRequested,
        DateOnly? RequiredByDate, string ImpactLevel, string ImpactDescription, LinkInput[]? Links);
    public sealed record TransitionBody(string ToStatus, string? Reason, string? DecisionText, DateOnly? DecisionDate, DateOnly? NewRequiredBy, bool? NotifyAssignees, int? RowVersion);
    public sealed record PartyBody(string Name, string? Organisation, string? Email, string? Role, bool? IsClient, string? Notes);
    sealed record Target(Guid Id, string Key, string Name, string? Status, DateOnly? Date, string? Person);

    internal static readonly string[] Open = [DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred];
    static readonly string[] LinkTargets = [ItemType.Task, ItemType.Deliverable, ItemType.Milestone];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/decisions", Register);
        api.MapPost("/projects/{id:guid}/decisions", Create);
        api.MapGet("/decisions/{id}", Get);
        api.MapPatch("/decisions/{id:guid}", Edit);
        api.MapPost("/decisions/{id:guid}/transition", Transition);
        api.MapPost("/decisions/{id:guid}/links", AddLink);
        api.MapDelete("/item-links/{id:guid}", RemoveLink);
        api.MapGet("/projects/{id:guid}/external-parties", async (Guid id, Access access, HubDb db) =>
        {
            await access.Project(id, track: false);
            return await db.ExternalParties.AsNoTracking().Where(x => x.ProjectId == id).OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
                .Select(x => new { x.Id, x.Name, x.Organisation, x.Email, x.Role, x.IsClient, x.Notes, x.IsActive, x.RowVersion }).ToListAsync();
        });
        api.MapPost("/projects/{id:guid}/external-parties", async (Guid id, PartyBody b, Access access, HubDb db) =>
        {
            var (_, ctx) = await access.Project(id);
            Access.Demand(Permissions.CreateExternalParty(access.Actor, ctx)); // FR-009: any team member creates
            var x = new ExternalParty
            {
                ProjectId = id, Name = Check.Required(b.Name, "name", 200), Organisation = Check.Optional(b.Organisation, "organisation", 200),
                Email = Check.Optional(b.Email, "email", 200), Role = Check.Optional(b.Role, "role", 200), IsClient = b.IsClient ?? false, Notes = Check.Optional(b.Notes, "notes", 2000),
            };
            db.ExternalParties.Add(x);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/external-parties/{x.Id}", new { x.Id, x.Name, x.Organisation, x.IsClient, x.RowVersion });
        });
        api.MapPatch("/external-parties/{id:guid}", async (Guid id, JsonElement body, HttpContext http, Access access, HubDb db) =>
        {
            var x = await db.ExternalParties.FirstOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(x.ProjectId);
            Access.Demand(Permissions.ManageExternalParties(access.Actor, ctx)); // and the PM edits them
            var patch = new Patch(body);
            await Http.CheckVersion(db, http, x, patch.RowVersion);
            if (patch.Has("name")) x.Name = Check.Required(patch.Str("name"), "name", 200);
            if (patch.Has("organisation")) x.Organisation = Check.Optional(patch.Str("organisation"), "organisation", 200);
            if (patch.Has("email")) x.Email = Check.Optional(patch.Str("email"), "email", 200);
            if (patch.Has("role")) x.Role = Check.Optional(patch.Str("role"), "role", 200);
            if (patch.Has("isClient")) x.IsClient = patch.Bool("isClient") ?? false;
            if (patch.Has("notes")) x.Notes = Check.Optional(patch.Str("notes"), "notes", 2000);
            if (patch.Has("isActive")) x.IsActive = patch.Bool("isActive") ?? true;
            await db.SaveChangesAsync();
            return Results.Ok(new { x.Id, x.RowVersion });
        });
    }

    public static async Task<(Decision D, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var d = await db.Decisions.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(d.ProjectId);
        return (d, p, ctx);
    }

    /// Owners and the PM edit decisions; others edit the ones they requested (FR-001, §8.5.2).
    internal static OwnedFacts Facts(Decision d) => new(d.OwnerUserId, d.RequestedById);

    public static NotifyItem Item(Project p, Decision d) => new(p.Id, ItemType.Decision, d.Id, d.Key, $"/projects/{p.ProjectNumber}/decisions?panel=Decision:{d.Id}", p.ProjectNumber);

    // ---------- Register (§13.8, FR-010) ----------

    public sealed record DecisionQuery(string? Status, Guid? OwnerId, string? OwnerType, string? Impact, DateOnly? RequiredFrom, DateOnly? RequiredTo, bool? Blocking,
        string? Indicator, string? Q);

    public static async Task<List<object>> Register(Guid id, [AsParameters] DecisionQuery f, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock)
    {
        var (status, ownerId, ownerType, impact, requiredFrom, requiredTo, blocking, indicator, q) = f;
        await access.Project(id, track: false);
        await eval.EnsureFresh(id);
        var query = db.Decisions.AsNoTracking().Where(d => d.ProjectId == id);
        var st = Http.List(status); if (st.Length > 0) query = query.Where(d => st.Contains(d.Status));
        if (ownerId is { } o) query = query.Where(d => d.OwnerUserId == o || d.OwnerExternalPartyId == o);
        query = ownerType switch
        {
            "internal" => query.Where(d => d.OwnerUserId != null),
            "external" => query.Where(d => d.OwnerExternalPartyId != null),
            "client" => query.Where(d => db.ExternalParties.Any(x => x.Id == d.OwnerExternalPartyId && x.IsClient)),
            _ => query,
        };
        var imp = Http.List(impact); if (imp.Length > 0) query = query.Where(d => imp.Contains(d.ImpactLevel));
        if (requiredFrom is { } rf) query = query.Where(d => d.RequiredByDate >= rf);
        if (requiredTo is { } rt) query = query.Where(d => d.RequiredByDate <= rt);
        if (!string.IsNullOrWhiteSpace(q)) { var term = $"%{q.Trim()}%"; query = query.Where(d => EF.Functions.ILike(d.Subject, term) || EF.Functions.ILike(d.Key, term)); }
        if (blocking == true) query = query.Where(d => db.DecisionStates.Any(x => x.DecisionId == d.Id && x.BlockingCount > 0));
        foreach (var ind in Http.List(indicator))
            query = ind switch
            {
                "overdue" => query.Where(d => db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsOverdue)),
                "dueSoon" => query.Where(d => db.DecisionStates.Any(x => x.DecisionId == d.Id && x.IsDueSoon)),
                "open" => query.Where(d => Open.Contains(d.Status)),
                _ => query,
            };
        return await Rows(db, query, clock.Today(await store.Get(db)));
    }

    public static async Task<List<object>> Rows(HubDb db, IQueryable<Decision> query, DateOnly today)
    {
        var rows = await query.Select(d => new
        {
            d.Id, d.ProjectId, d.Key, d.Subject, d.Status, d.RequestedById, d.OwnerUserId, d.OwnerExternalPartyId, d.DateRequested, d.RequiredByDate,
            d.OriginalRequiredByDate, d.ImpactLevel, d.ImpactDescription, d.DecisionText, d.DecisionDate, d.RowVersion,
            RequestedByName = db.Users.Where(u => u.Id == d.RequestedById).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            OwnerName = db.Users.Where(u => u.Id == d.OwnerUserId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            External = db.ExternalParties.Where(x => x.Id == d.OwnerExternalPartyId).Select(x => new { x.Name, x.Organisation, x.IsClient }).FirstOrDefault(),
            State = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => new { x.IsOverdue, x.DaysOverdue, x.IsDueSoon, x.IsInactiveOwner, x.BlockingTaskIds }).FirstOrDefault(),
            Deliverables = (from l in db.ItemLinks where l.SourceId == d.Id && l.TargetType == ItemType.Deliverable
                            join x in db.Deliverables on l.TargetId equals x.Id select new { x.Id, x.Key, x.Name }).ToList(),
            Milestones = (from l in db.ItemLinks where l.SourceId == d.Id && l.TargetType == ItemType.Milestone
                          join x in db.Milestones on l.TargetId equals x.Id select new { x.Id, x.Key, x.Name }).ToList(),
        }).ToListAsync();
        // FR-DEC-04: overdue first, then required-by ascending, then impact (High first).
        static int Weight(string i) => i switch { Impact.High => 0, Impact.Medium => 1, _ => 2 };
        return rows.OrderByDescending(r => r.State?.IsOverdue == true && DecisionStatus.IsOpen(r.Status)).ThenBy(r => r.RequiredByDate).ThenBy(r => Weight(r.ImpactLevel)).ThenBy(r => r.Key)
            .Select(r =>
            {
                var open = DecisionStatus.IsOpen(r.Status);
                var st = open ? r.State : null; // closed decisions are never overdue or blocking, even before re-evaluation
                return (object)new
                {
                    r.Id, r.ProjectId, r.Key, r.Subject, r.Status, r.RequestedById, r.RequestedByName, r.OwnerUserId, r.OwnerExternalPartyId,
                    OwnerName = r.OwnerName ?? r.External?.Name, OwnerOrganisation = r.External?.Organisation, OwnerIsClient = r.External?.IsClient ?? false,
                    r.DateRequested, r.RequiredByDate, r.OriginalRequiredByDate, r.ImpactLevel, r.ImpactDescription, r.DecisionText, r.DecisionDate, r.RowVersion,
                    DaysUntil = open ? r.RequiredByDate.DayNumber - today.DayNumber : (int?)null,
                    IsOverdue = st?.IsOverdue ?? false, DaysOverdue = st?.DaysOverdue ?? 0, IsDueSoon = st?.IsDueSoon ?? false, IsInactiveOwner = st?.IsInactiveOwner ?? false,
                    BlockingTaskIds = st?.BlockingTaskIds ?? [], r.Deliverables, r.Milestones,
                };
            }).ToList();
    }

    // ---------- Raise (FR-001, DEC-01, §17.2) ----------

    static async Task<IResult> Create(Guid id, CreateBody body, Access access, HubDb db, Notifier notify, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var today = clock.Today(await store.Get(db));
        await Owner(db, id, body.OwnerUserId, body.OwnerExternalPartyId);
        if (body.RequestedById is { } rq) await DeliverableEndpoints.ActivePerson(db, rq, "requestedById");
        Check.OneOf(body.ImpactLevel, Impact.All, "impactLevel");
        var required = body.RequiredByDate ?? throw ApiException.Invalid("requiredByDate", "error.required");
        var d = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, id, p.ProjectNumber, "decision");
            var d = new Decision
            {
                ProjectId = id, Seq = seq, Key = key, Subject = Check.Required(body.Subject, "subject", 300), Description = Check.Required(body.Description, "description", 8000),
                RequestedById = body.RequestedById ?? me.Id, OwnerUserId = body.OwnerUserId, OwnerExternalPartyId = body.OwnerExternalPartyId,
                DateRequested = body.DateRequested ?? today, RequiredByDate = required, OriginalRequiredByDate = required,
                ImpactLevel = body.ImpactLevel, ImpactDescription = Check.Required(body.ImpactDescription, "impactDescription", 2000),
                StatusChangedAt = clock.GetUtcNow(), LastActivityAt = clock.GetUtcNow(),
            };
            db.Decisions.Add(d);
            foreach (var l in body.Links ?? []) await Link(db, access, p, d, l);
            await team.EnsureMember(p, d.OwnerUserId, ProjectRole.TeamMember); // the owner must be able to open what they are asked to decide
            await notify.Send(NotificationEvents.DecisionAssigned, d.OwnerUserId, Item(p, d), Text.Get("notify.decision_assigned", await notify.ActorName(), d.Key, d.Subject));
            await db.SaveChangesAsync();
            return d;
        });
        return Results.Created($"/api/v1/decisions/{d.Id}", new { d.Id, d.Key, d.RowVersion });
    }

    /// DEC-01: exactly one owner, an active person or one of the project's active external parties.
    static async Task Owner(HubDb db, Guid projectId, Guid? user, Guid? external)
    {
        Check.That(user is null != external is null, "ownerUserId", "decision.one_owner");
        if (user is { } u) await DeliverableEndpoints.ActivePerson(db, u, "ownerUserId");
        if (external is { } x) Check.That(await db.ExternalParties.AnyAsync(e => e.Id == x && e.ProjectId == projectId && e.IsActive), "ownerExternalPartyId", "error.not_found");
    }

    internal static async Task<ItemLink> Link(HubDb db, Access access, Project p, Decision d, LinkInput l)
    {
        Check.OneOf(l.TargetType, LinkTargets, "targetType");
        var relation = l.Relation ?? (l.TargetType == ItemType.Task ? ItemRelation.BlockedByDecision : ItemRelation.Related);
        Check.OneOf(relation, [ItemRelation.BlockedByDecision, ItemRelation.Related], "relation");
        Check.That(relation == ItemRelation.Related || l.TargetType == ItemType.Task, "relation", "decision.blocks_tasks_only");
        return await NewLink(db, access, p, ItemType.Decision, d.Id, d.Key, l.TargetType, l.TargetId, relation);
    }

    /// A register item's link to a task, deliverable or milestone of the same project, logged as "Linked".
    internal static async Task<ItemLink> NewLink(HubDb db, Access access, Project p, string sourceType, Guid sourceId, string sourceKey, string targetType, Guid targetId, string relation)
    {
        Check.OneOf(targetType, LinkTargets, "targetType");
        var found = targetType switch
        {
            ItemType.Task => await db.Tasks.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
            ItemType.Deliverable => await db.Deliverables.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
            _ => await db.Milestones.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
        };
        Check.That(found, "targetId", "decision.other_project");
        if (await db.ItemLinks.AnyAsync(x => x.SourceId == sourceId && x.TargetId == targetId)) throw ApiException.Conflict("link_exists", "decision.link_exists");
        var link = new ItemLink { ProjectId = p.Id, SourceType = sourceType, SourceId = sourceId, TargetType = targetType, TargetId = targetId, Relation = relation,
            CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = sourceKey };
        db.ItemLinks.Add(link);
        db.Audit.Note(link, action: "Linked");
        return link;
    }

    /// The linked tasks, deliverables and milestones of a register item, oldest link first, with their status, date and person.
    internal static async Task<List<object>> LinkRows(HubDb db, Guid sourceId)
    {
        var links = await db.ItemLinks.AsNoTracking().Where(l => l.SourceId == sourceId && LinkTargets.Contains(l.TargetType)).OrderBy(l => l.CreatedAt).ToListAsync();
        var ids = links.Select(l => l.TargetId).ToList();
        var targets = (await db.Tasks.AsNoTracking().Where(t => ids.Contains(t.Id))
                .Select(t => new Target(t.Id, t.Key, t.Name, t.Status, t.DueDate, db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync())
            .Concat(await db.Deliverables.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Target(x.Id, x.Key, x.Name, x.Status, x.DueDate, null)).ToListAsync())
            .Concat(await db.Milestones.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new Target(x.Id, x.Key, x.Name, null, x.Date, null)).ToListAsync())
            .ToDictionary(x => x.Id);
        return [.. links.Where(l => targets.ContainsKey(l.TargetId)).Select(l => targets[l.TargetId] is var t
            ? (object)new { l.Id, l.TargetType, l.TargetId, l.Relation, t.Key, t.Name, t.Status, t.Date, t.Person } : null!)];
    }

    // ---------- Detail, edit, links ----------

    static async Task<object> Get(string id, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock)
    {
        var d = (Guid.TryParse(id, out var g) ? await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(d.ProjectId, track: false);
        await eval.EnsureFresh(p.Id);
        var row = (await Rows(db, db.Decisions.AsNoTracking().Where(x => x.Id == d.Id), clock.Today(await store.Get(db)))).Single();
        var a = access.Actor;
        object P(Allow x) => new { x.Ok, Reason = x.Ok ? null : Text.Get(x.Why!, x.Arg ?? p.Status) };
        return new
        {
            Decision = row, d.Description, d.DeferralReason, d.CancelledReason,
            DecidedBy = d.DecidedById is { } by ? await db.Users.Where(u => u.Id == by).Select(u => u.DisplayName).FirstOrDefaultAsync() : null,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            Links = await LinkRows(db, d.Id),
            Permissions = new
            {
                Edit = P(Permissions.EditRegisterItem(a, ctx, Facts(d))),
                Transitions = DecisionStatus.All.Where(to => Workflow.DecisionStep(d.Status, to))
                    .Select(to => Allowed(a, ctx, d, to) is var x ? new { To = to, x.Ok, Reason = x.Ok ? null : Text.Get(x.Why!, x.Arg ?? to) } : null),
                Comment = Permissions.Comment(a, ctx).Ok,
            },
        };
    }

    /// The owner or PM decide, defer and cancel (§8.5.2 "Record a decision outcome"); only the PM reopens (DEC-07);
    /// moving between Pending and Under Review is an edit.
    static Allow Allowed(Actor a, ProjectContext ctx, Decision d, string to) => to switch
    {
        DecisionStatus.Decided or DecisionStatus.Deferred or DecisionStatus.Cancelled => Permissions.DecideOrDefer(a, ctx, Facts(d)),
        DecisionStatus.Pending when d.Status == DecisionStatus.Decided => Permissions.Writable(a, ctx, pmOnly: true),
        _ => Permissions.EditRegisterItem(a, ctx, Facts(d)),
    };

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, Notifier notify, TeamService team, TimeProvider clock)
    {
        var (d, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(d)));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, d, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        if (patch.Has("subject")) d.Subject = Check.Required(patch.Str("subject"), "subject", 300);
        if (patch.Has("description")) d.Description = Check.Required(patch.Str("description"), "description", 8000);
        if (patch.Has("impactLevel")) { var i = patch.Str("impactLevel"); Check.OneOf(i, Impact.All, "impactLevel"); d.ImpactLevel = i!; }
        if (patch.Has("impactDescription")) d.ImpactDescription = Check.Required(patch.Str("impactDescription"), "impactDescription", 2000);
        if (patch.Has("requestedById")) { var r = patch.Id("requestedById") ?? throw ApiException.Invalid("requestedById", "error.required"); await DeliverableEndpoints.ActivePerson(db, r, "requestedById"); d.RequestedById = r; }
        if (patch.Has("dateRequested")) d.DateRequested = patch.Date("dateRequested") ?? throw ApiException.Invalid("dateRequested", "error.required");
        if (patch.Has("requiredByDate"))
        {
            Check.That(DecisionStatus.IsOpen(d.Status), "requiredByDate", "decision.date_closed");
            d.RequiredByDate = patch.Date("requiredByDate") ?? throw ApiException.Invalid("requiredByDate", "error.required");
        }
        var oldOwner = d.OwnerUserId;
        if (patch.Has("ownerUserId") || patch.Has("ownerExternalPartyId"))
        {
            var u = patch.Has("ownerUserId") ? patch.Id("ownerUserId") : patch.Has("ownerExternalPartyId") ? null : d.OwnerUserId;
            var x = patch.Has("ownerExternalPartyId") ? patch.Id("ownerExternalPartyId") : patch.Has("ownerUserId") ? null : d.OwnerExternalPartyId;
            await Owner(db, d.ProjectId, u, x);
            d.OwnerUserId = u;
            d.OwnerExternalPartyId = x;
        }
        d.LastActivityAt = clock.GetUtcNow();
        if (d.OwnerUserId != oldOwner)
        {
            await team.EnsureMember(p, d.OwnerUserId, ProjectRole.TeamMember);
            await notify.Send(NotificationEvents.DecisionAssigned, d.OwnerUserId, Item(p, d), Text.Get("notify.decision_assigned", await notify.ActorName(), d.Key, d.Subject));
        }
        await db.SaveChangesAsync();
        Http.ETag(http, d);
        return Results.Ok(new { d.Id, d.RowVersion });
    }

    static async Task<IResult> AddLink(Guid id, LinkInput body, Access access, HubDb db)
    {
        var (d, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(d)));
        var link = await Link(db, access, p, d, body);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/item-links/{link.Id}", new { link.Id });
    }

    /// Unlinks a decision's, risk's or issue's item.
    static async Task<IResult> RemoveLink(Guid id, Access access, HubDb db, TimeProvider clock)
    {
        var l = await db.ItemLinks.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (key, ctx, facts) = l.SourceType switch
        {
            ItemType.Decision => await Load(db, access, l.SourceId) is var (d, _, c) ? (d.Key, c, Facts(d)) : default,
            ItemType.Risk => await RegisterEndpoints.LoadRisk(db, access, l.SourceId) is var (r, _, c) ? (r.Key, c, RegisterEndpoints.Facts(r)) : default,
            ItemType.Issue => await RegisterEndpoints.LoadIssue(db, access, l.SourceId) is var (i, _, c) ? (i.Key, c, RegisterEndpoints.Facts(i)) : default,
            _ => throw ApiException.NotFound(),
        };
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, facts));
        l.DeletedAt = clock.GetUtcNow();
        l.DeletedBy = access.Me.Id;
        l.AuditKey = key;
        db.Audit.Note(l, action: "Unlinked");
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---------- Record, defer, cancel, reopen (DEC-02, DEC-03, DEC-07, FR-011, Workflow 11) ----------

    static async Task<IResult> Transition(Guid id, TransitionBody body, HttpContext http, Access access, HubDb db, Notifier notify, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (d, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, d, body.RowVersion);
        Check.OneOf(body.ToStatus, DecisionStatus.All, "toStatus");
        var (from, to) = (d.Status, body.ToStatus);
        if (!Workflow.DecisionStep(from, to)) throw ApiException.Rule("illegal_transition", "decision.illegal_transition", null, from, to);
        Access.Demand(Allowed(access.Actor, ctx, d, to));
        var today = clock.Today(await store.Get(db));
        string? reason = null;
        switch (to)
        {
            case DecisionStatus.Decided: // DEC-02, AC-DEC-06
                d.DecisionText = Check.Required(body.DecisionText, "decisionText", 8000);
                d.DecisionDate = body.DecisionDate ?? today;
                Check.That(d.DecisionDate <= today, "decisionDate", "decision.future_date");
                d.DecidedById = me.Id;
                break;
            case DecisionStatus.Deferred: // DEC-03: the previous date stays in the history
                var next = body.NewRequiredBy ?? throw ApiException.Invalid("newRequiredBy", "error.required");
                Check.That(next > today, "newRequiredBy", "decision.defer_future");
                d.DeferralReason = reason = Check.Reason(body.Reason);
                d.RequiredByDate = next;
                break;
            case DecisionStatus.Cancelled:
                d.CancelledReason = reason = Check.Reason(body.Reason);
                break;
            case DecisionStatus.Pending when from == DecisionStatus.Decided: // DEC-07
                reason = Check.Reason(body.Reason);
                (d.DecisionText, d.DecisionDate, d.DecidedById) = (null, null, null);
                break;
        }
        reason ??= string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        ProjectEndpoints.CorrectionReason(p, reason);
        var now = clock.GetUtcNow();
        d.Status = to;
        d.StatusChangedAt = now;
        d.LastActivityAt = now;
        db.Audit.Note(d, action: from == DecisionStatus.Decided ? "Reopened" : to switch { DecisionStatus.Decided => "Decided", DecisionStatus.Deferred => "Deferred", _ => null }, reason: reason);
        if (to == DecisionStatus.Decided) // §17.2: linked task assignees (unless unticked, FR-011) and the requester
        {
            List<Guid?> assignees = body.NotifyAssignees == false ? [] : await db.ItemLinks.Where(l => l.SourceId == d.Id && l.TargetType == ItemType.Task)
                .Join(db.Tasks, l => l.TargetId, t => t.Id, (l, t) => t.AssigneeId).ToListAsync();
            await notify.Send(NotificationEvents.DecisionRecorded, [.. assignees, d.RequestedById], Item(p, d),
                Text.Get("notify.decision_recorded", await notify.ActorName(), d.Key, d.Subject), d.DecisionText);
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { d.Id, d.Status, d.RowVersion });
    }
}
