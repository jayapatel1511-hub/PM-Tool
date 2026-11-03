using System.Text.Json.Serialization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

/// Decision Register (§12.9, §13.8, DEC-01..DEC-07, FR-DEC-01..04) and project external parties (FR-ORG-08).
public static partial class DecisionEndpoints
{
    public sealed record LinkInput(string TargetType, Guid TargetId, string? Relation = null);
    public sealed record CreateBody(string Subject, string Description, Guid? RequestedById, Guid? OwnerUserId, Guid? OwnerExternalPartyId, DateOnly? DateRequested,
        DateOnly? RequiredByDate, string ImpactLevel, string ImpactDescription, LinkInput[]? Links,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CategoryId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? OptionsConsidered = null);
    public sealed record TransitionBody(string ToStatus, string? Reason = null, string? DecisionText = null, DateOnly? DecisionDate = null, DateOnly? NewRequiredBy = null, bool? NotifyAssignees = null, int? RowVersion = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Rationale = null);
    public sealed record PartyBody(string Name, string? Organisation, string? Email, string? Role, bool? IsClient, string? Notes);
    public sealed record ExternalPartyRow(Guid Id, string Name, string? Organisation, string? Email, string? Role, bool IsClient, string? Notes, bool IsActive, int RowVersion);
    public sealed record ExternalPartyCreated(Guid Id, string Name, string? Organisation, bool IsClient, int RowVersion);
    public sealed record ExternalPartyMutation(Guid Id, int RowVersion);
    public sealed record DecisionLinkSummary(Guid Id, string Key, string Name);
    public sealed record DecisionRow(Guid Id, Guid ProjectId, string Key, string Subject, string Status, Guid RequestedById, string? RequestedByName,
        Guid? OwnerUserId, Guid? OwnerExternalPartyId, string? OwnerName, string? OwnerOrganisation, bool OwnerIsClient, DateOnly DateRequested,
        DateOnly RequiredByDate, DateOnly OriginalRequiredByDate, string ImpactLevel, string ImpactDescription, string? DecisionText, DateOnly? DecisionDate,
        int RowVersion, int? DaysUntil, bool IsOverdue, int DaysOverdue, bool IsDueSoon, bool IsInactiveOwner, IReadOnlyList<Guid> BlockingTaskIds,
        IReadOnlyList<DecisionLinkSummary> Deliverables, IReadOnlyList<DecisionLinkSummary> Milestones,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DecisionMetadataSummary? Metadata = null);
    public sealed record DecisionProject(Guid Id, string ProjectNumber, string Name, string Status);
    public sealed record DecisionPermission(bool Ok, string? Reason);
    public sealed record DecisionTransition(string To, bool Ok, string? Reason);
    public sealed record DecisionPermissions(DecisionPermission Edit, IReadOnlyList<DecisionTransition> Transitions, bool Comment);
    public sealed partial record DecisionDetailResponse(DecisionRow Decision, string Description, string? DeferralReason, string? CancelledReason, string? DecidedBy,
        DecisionProject Project, IReadOnlyList<ItemLinkRow> Links, DecisionPermissions Permissions, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DecisionMetadataDetail? Metadata = null);
    public sealed record DecisionCreated(Guid Id, string Key, int RowVersion);
    public sealed record DecisionMutation(Guid Id, int RowVersion);
    public sealed record DecisionTransitionMutation(Guid Id, string Status, int RowVersion);
    public sealed record DecisionLinkMutation(Guid Id);
    sealed record Target(string Kind, Guid Id, string Key, string Name, string? Status, DateOnly? Date, string? Person, Guid? PersonId);

    internal static readonly string[] Open = [DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred];
    static readonly string[] LinkTargets = [ItemType.Task, ItemType.Deliverable, ItemType.Milestone];
    static readonly string[] ActionLinkTargets = [.. LinkTargets, ItemType.Handoff, ItemType.ChangeNotice];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/decisions", Register);
        api.MapPost("/projects/{id:guid}/decisions", Create).WithMetadata(new DecisionMetadataReplay.Guard());
        api.MapGet("/projects/{id:guid}/decisions/screen", DecisionScreenReads.Read).RequireFlag(DecisionMetadata.Flag);
        api.MapGet("/decisions/{id}", Get);
        api.MapPatch("/decisions/{id:guid}", Edit);
        api.MapPost("/decisions/{id:guid}/transition", Transition).WithMetadata(new DecisionMetadataReplay.Guard(true));
        api.MapPost("/decisions/{id:guid}/links", AddLink);
        api.MapDelete("/item-links/{id:guid}", RemoveLink);
        api.MapGet("/projects/{id:guid}/external-parties", ListParties);
        api.MapPost("/projects/{id:guid}/external-parties", CreateParty);
        api.MapPatch("/external-parties/{id:guid}", EditParty);
    }

    static async Task<Ok<IReadOnlyList<ExternalPartyRow>>> ListParties(Guid id, Access access, HubDb db)
    {
        await access.Project(id, track: false);
        var rows = await db.ExternalParties.AsNoTracking().Where(x => x.ProjectId == id).OrderByDescending(x => x.IsActive).ThenBy(x => x.Name)
            .Select(x => new ExternalPartyRow(x.Id, x.Name, x.Organisation, x.Email, x.Role, x.IsClient, x.Notes, x.IsActive, x.RowVersion)).ToListAsync();
        return TypedResults.Ok<IReadOnlyList<ExternalPartyRow>>(rows);
    }

    static async Task<Created<ExternalPartyCreated>> CreateParty(Guid id, PartyBody b, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(id);
        Access.Demand(Permissions.CreateExternalParty(access.Actor, ctx));
        var x = new ExternalParty { ProjectId = id, Name = Check.Required(b.Name, "name", 200), Organisation = Check.Optional(b.Organisation, "organisation", 200),
            Email = Check.Optional(b.Email, "email", 200), Role = Check.Optional(b.Role, "role", 200), IsClient = b.IsClient ?? false, Notes = Check.Optional(b.Notes, "notes", 2000) };
        db.ExternalParties.Add(x); await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/external-parties/{x.Id}", new ExternalPartyCreated(x.Id, x.Name, x.Organisation, x.IsClient, x.RowVersion));
    }

    static async Task<Ok<ExternalPartyMutation>> EditParty(Guid id, JsonElement body, HttpContext http, Access access, HubDb db)
    {
        var x = await db.ExternalParties.FirstOrDefaultAsync(e => e.Id == id) ?? throw ApiException.NotFound();
        var (_, ctx) = await access.Project(x.ProjectId); Access.Demand(Permissions.ManageExternalParties(access.Actor, ctx));
        var patch = new Patch(body); await Http.CheckVersion(db, http, x, patch.RowVersion);
        if (patch.Has("name")) x.Name = Check.Required(patch.Str("name"), "name", 200);
        if (patch.Has("organisation")) x.Organisation = Check.Optional(patch.Str("organisation"), "organisation", 200);
        if (patch.Has("email")) x.Email = Check.Optional(patch.Str("email"), "email", 200);
        if (patch.Has("role")) x.Role = Check.Optional(patch.Str("role"), "role", 200);
        if (patch.Has("isClient")) x.IsClient = patch.Bool("isClient") ?? false;
        if (patch.Has("notes")) x.Notes = Check.Optional(patch.Str("notes"), "notes", 2000);
        if (patch.Has("isActive")) x.IsActive = patch.Bool("isActive") ?? true;
        await db.SaveChangesAsync(); return TypedResults.Ok(new ExternalPartyMutation(x.Id, x.RowVersion));
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

    public static async Task<List<DecisionRow>> Register(Guid id, [AsParameters] DecisionQuery f, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock)
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
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn(DecisionMetadata.Flag) ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        return await Rows(db, query, clock.Today(settings), settings.IsFlagOn(DecisionMetadata.Flag));
    }

    public static async Task<List<DecisionRow>> Rows(HubDb db, IQueryable<Decision> query, DateOnly today, bool registerScreens = false)
    {
        var rows = await query.Select(d => new
        {
            d.Id, d.ProjectId, d.Key, d.Subject, d.Status, d.RequestedById, d.OwnerUserId, d.OwnerExternalPartyId, d.DateRequested, d.RequiredByDate,
            d.OriginalRequiredByDate, d.ImpactLevel, d.ImpactDescription, d.DecisionText, d.DecisionDate, d.RowVersion, d.CategoryId, d.OptionsConsidered, d.Rationale,
            RequestedByName = db.Users.Where(u => u.Id == d.RequestedById).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            OwnerName = db.Users.Where(u => u.Id == d.OwnerUserId).Select(u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)").FirstOrDefault(),
            External = db.ExternalParties.Where(x => x.Id == d.OwnerExternalPartyId).Select(x => new { x.Name, x.Organisation, x.IsClient }).FirstOrDefault(),
            State = db.DecisionStates.Where(x => x.DecisionId == d.Id).Select(x => new { x.IsOverdue, x.DaysOverdue, x.IsDueSoon, x.IsInactiveOwner, x.BlockingTaskIds }).FirstOrDefault(),
            Deliverables = (from l in db.ItemLinks where l.ProjectId == d.ProjectId && l.SourceType == ItemType.Decision && l.SourceId == d.Id && l.TargetType == ItemType.Deliverable
                            join x in db.Deliverables on l.TargetId equals x.Id where x.ProjectId == d.ProjectId select new { x.Id, x.Key, x.Name }).ToList(),
            Milestones = (from l in db.ItemLinks where l.ProjectId == d.ProjectId && l.SourceType == ItemType.Decision && l.SourceId == d.Id && l.TargetType == ItemType.Milestone
                          join x in db.Milestones on l.TargetId equals x.Id where x.ProjectId == d.ProjectId select new { x.Id, x.Key, x.Name }).ToList(),
        }).ToListAsync();
        var categoryIds = registerScreens ? rows.Select(row => row.CategoryId).OfType<Guid>().Distinct().ToArray() : [];
        var categories = registerScreens ? await db.DecisionCategories.AsNoTracking().Where(category => categoryIds.Contains(category.Id))
            .Select(category => new DecisionCategoryChoice(category.Id, category.Name, category.IsActive)).ToDictionaryAsync(category => category.Id) : new Dictionary<Guid, DecisionCategoryChoice>();
        // FR-DEC-04: overdue first, then required-by ascending, then impact (High first).
        static int Weight(string i) => i switch { Impact.High => 0, Impact.Medium => 1, _ => 2 };
        return rows.OrderByDescending(r => r.State?.IsOverdue == true && DecisionStatus.IsOpen(r.Status)).ThenBy(r => r.RequiredByDate).ThenBy(r => Weight(r.ImpactLevel)).ThenBy(r => r.Key)
            .Select(r =>
            {
                var open = DecisionStatus.IsOpen(r.Status);
                var st = open ? r.State : null; // closed decisions are never overdue or blocking, even before re-evaluation
                return new DecisionRow(r.Id, r.ProjectId, r.Key, r.Subject, r.Status, r.RequestedById, r.RequestedByName, r.OwnerUserId, r.OwnerExternalPartyId,
                    r.OwnerName ?? r.External?.Name, r.External?.Organisation, r.External?.IsClient ?? false, r.DateRequested, r.RequiredByDate,
                    r.OriginalRequiredByDate, r.ImpactLevel, r.ImpactDescription, r.DecisionText, r.DecisionDate, r.RowVersion,
                    open ? r.RequiredByDate.DayNumber - today.DayNumber : null, st?.IsOverdue ?? false, st?.DaysOverdue ?? 0,
                    st?.IsDueSoon ?? false, st?.IsInactiveOwner ?? false, st?.BlockingTaskIds ?? [],
                    r.Deliverables.Select(x => new DecisionLinkSummary(x.Id, x.Key, x.Name)).ToList(),
                    r.Milestones.Select(x => new DecisionLinkSummary(x.Id, x.Key, x.Name)).ToList(),
                    registerScreens ? new DecisionMetadataSummary(r.CategoryId is { } categoryId ? categories.GetValueOrDefault(categoryId) : null, r.OptionsConsidered?.Length, !string.IsNullOrWhiteSpace(r.Rationale)) : null);
            }).ToList();
    }

    // ---------- Raise (FR-001, DEC-01, §17.2) ----------

    static async Task<Created<DecisionCreated>> Create(Guid id, CreateBody body, Access access, HubDb db, Notifier notify, TeamService team, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.RaiseRegisterItem(access.Actor, ctx));
        var today = clock.Today(await store.Get(db));
        var metadata = await DecisionMetadata.Create(db, body.CategoryId, body.OptionsConsidered, (await store.Get(db)).IsFlagOn(DecisionMetadata.Flag));
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
            DecisionMetadata.Apply(d, metadata);
            db.Decisions.Add(d);
            foreach (var l in body.Links ?? []) await Link(db, access, p, d, l);
            await team.EnsureMember(p, d.OwnerUserId, ProjectRole.TeamMember); // the owner must be able to open what they are asked to decide
            await notify.Send(NotificationEvents.DecisionAssigned, d.OwnerUserId, Item(p, d), Text.Get("notify.decision_assigned", await notify.ActorName(), d.Key, d.Subject));
            await db.SaveChangesAsync();
            return d;
        });
        return TypedResults.Created($"/api/v1/decisions/{d.Id}", new DecisionCreated(d.Id, d.Key, d.RowVersion));
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

    /// A register item's link to a same-project source, logged as "Linked". Meeting actions
    /// may also name a handoff or change notice directly so reuse cannot be inferred from
    /// coincidentally shared work targets.
    internal static async Task<ItemLink> NewLink(HubDb db, Access access, Project p, string sourceType, Guid sourceId, string sourceKey, string targetType, Guid targetId, string relation)
    {
        Check.OneOf(targetType, sourceType == ItemType.Action ? ActionLinkTargets : LinkTargets, "targetType");
        var found = targetType switch
        {
            ItemType.Task => await db.Tasks.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
            ItemType.Deliverable => await db.Deliverables.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
            ItemType.Milestone => await db.Milestones.AnyAsync(t => t.Id == targetId && t.ProjectId == p.Id),
            ItemType.Handoff => await db.Handoffs.AnyAsync(h => h.Id == targetId && h.ProjectId == p.Id),
            _ => await db.ChangeNotices.AnyAsync(c => c.Id == targetId && c.ProjectId == p.Id),
        };
        Check.That(found, "targetId", "decision.other_project");
        if (await db.ItemLinks.AnyAsync(x => x.SourceId == sourceId && x.TargetId == targetId)) throw ApiException.Conflict("link_exists", "decision.link_exists");
        var link = new ItemLink { ProjectId = p.Id, SourceType = sourceType, SourceId = sourceId, TargetType = targetType, TargetId = targetId, Relation = relation,
            CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = sourceKey };
        db.ItemLinks.Add(link);
        db.Audit.Note(link, action: "Linked");
        return link;
    }

    /// Same-project, declared-kind links only. The dark screen pattern normalizes visible
    /// targets by kind/id and orders them by relationship, key and id; legacy valid reads
    /// retain their creation order while the flag is off.
    public sealed record ItemLinkRow(Guid Id, string TargetType, Guid TargetId, string Relation, string Key, string Name, string? Status, DateOnly? Date, string? Person,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? PersonId = null);
    internal static async Task<List<ItemLinkRow>> LinkRows(HubDb db, Guid projectId, string sourceType, Guid sourceId, bool screenPatterns, bool peopleScreens = false)
    {
        var links = await db.ItemLinks.AsNoTracking().Where(l => l.ProjectId == projectId && l.SourceType == sourceType && l.SourceId == sourceId
            && l.DeletedAt == null && ActionLinkTargets.Contains(l.TargetType)).OrderBy(l => l.CreatedAt).ToListAsync();
        Guid[] Ids(string kind) => [.. links.Where(l => l.TargetType == kind).Select(l => l.TargetId).Distinct()];
        var taskIds = Ids(ItemType.Task); var deliverableIds = Ids(ItemType.Deliverable); var milestoneIds = Ids(ItemType.Milestone);
        var handoffIds = Ids(ItemType.Handoff); var changeIds = Ids(ItemType.ChangeNotice);
        var targets = (await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && taskIds.Contains(t.Id))
                .Select(t => new Target(ItemType.Task, t.Id, t.Key, t.Name, t.Status, t.DueDate, db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(), t.AssigneeId)).ToListAsync())
            .Concat(await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == projectId && deliverableIds.Contains(x.Id)).Select(x => new Target(ItemType.Deliverable, x.Id, x.Key, x.Name, x.Status, x.DueDate, null, null)).ToListAsync())
            .Concat(await db.Milestones.AsNoTracking().Where(x => x.ProjectId == projectId && milestoneIds.Contains(x.Id)).Select(x => new Target(ItemType.Milestone, x.Id, x.Key, x.Name, null, x.Date, null, null)).ToListAsync())
            .Concat(await db.Handoffs.AsNoTracking().Where(x => x.ProjectId == projectId && handoffIds.Contains(x.Id)).Select(x => new Target(ItemType.Handoff, x.Id, x.Key, x.Title, x.Status, x.NeededBy, null, null)).ToListAsync())
            .Concat(await db.ChangeNotices.AsNoTracking().Where(x => x.ProjectId == projectId && changeIds.Contains(x.Id)).Select(x => new Target(ItemType.ChangeNotice, x.Id, x.Key, x.Title, x.Status, x.AssessmentDueDate, null, null)).ToListAsync())
            .ToDictionary(x => (x.Kind, x.Id));
        var rows = links.Where(l => targets.ContainsKey((l.TargetType, l.TargetId))).Select(l => {
            var t = targets[(l.TargetType, l.TargetId)];
            return new ItemLinkRow(l.Id, l.TargetType, l.TargetId, l.Relation, t.Key, t.Name, t.Status, t.Date, t.Person, peopleScreens ? t.PersonId : null);
        });
        if (screenPatterns)
            rows = rows.OrderBy(l => l.Relation, StringComparer.Ordinal).ThenBy(l => l.Key, StringComparer.Ordinal).ThenBy(l => l.TargetId)
                .ThenBy(l => l.TargetType, StringComparer.Ordinal).ThenBy(l => l.Id).DistinctBy(l => (l.TargetType, l.TargetId));
        return [.. rows];
    }

    // ---------- Detail, edit, links ----------

    static async Task<Ok<DecisionDetailResponse>> Get(string id, Access access, HubDb db, EvaluationService eval, SettingsStore store, TimeProvider clock)
    {
        var d = (Guid.TryParse(id, out var g) ? await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Decisions.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(d.ProjectId, track: false);
        await eval.EnsureFresh(p.Id);
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("work_screens") || settings.IsFlagOn(DecisionMetadata.Flag)
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        if (snapshot is not null)
        {
            d = await db.Decisions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == d.Id) ?? throw ApiException.NotFound();
            (p, ctx) = await access.Project(d.ProjectId, track: false);
        }
        var row = (await Rows(db, db.Decisions.AsNoTracking().Where(x => x.Id == d.Id), clock.Today(settings), settings.IsFlagOn(DecisionMetadata.Flag))).Single();
        var a = access.Actor;
        static DecisionPermission P(Allow x, string fallback) => new(x.Ok, x.Ok ? null : Text.Get(x.Why!, x.Arg ?? fallback));
        var transitions = DecisionStatus.All.Where(to => Workflow.DecisionStep(d.Status, to))
            .Select(to => { var x = Allowed(a, ctx, d, to); return new DecisionTransition(to, x.Ok, x.Ok ? null : Text.Get(x.Why!, x.Arg ?? to)); }).ToList();
        var permissions = new DecisionPermissions(P(Permissions.EditRegisterItem(a, ctx, Facts(d)), p.Status), transitions, Permissions.Comment(a, ctx).Ok);
        var project = new DecisionProject(p.Id, p.ProjectNumber, p.Name, p.Status);
        var decidedBy = d.DecidedById is { } by ? await db.Users.Where(u => u.Id == by).Select(u => u.DisplayName).FirstOrDefaultAsync() : null;
        return TypedResults.Ok(await WorkScreenResponseMetadata.AttachDetailAsync(new DecisionDetailResponse(row, d.Description, d.DeferralReason, d.CancelledReason, decidedBy, project,
            await LinkRows(db, p.Id, ItemType.Decision, d.Id, settings.IsFlagOn("screen_patterns"), settings.IsFlagOn("people_screens")), permissions,
            await new RecordContextReader(db, p, settings).Read(RecordContextKind.Decision, d.Id), settings.IsFlagOn(DecisionMetadata.Flag) ? DecisionMetadata.Detail(d, a, ctx) : null), d, access, db, settings, clock));
    }

    /// The owner or PM decide, defer and cancel (§8.5.2 "Record a decision outcome"); only the PM reopens (DEC-07);
    /// moving between Pending and Under Review is an edit.
    static Allow Allowed(Actor a, ProjectContext ctx, Decision d, string to) => to switch
    {
        DecisionStatus.Decided or DecisionStatus.Deferred or DecisionStatus.Cancelled => Permissions.DecideOrDefer(a, ctx, Facts(d)),
        DecisionStatus.Pending when d.Status == DecisionStatus.Decided => Permissions.Writable(a, ctx, pmOnly: true),
        _ => Permissions.EditRegisterItem(a, ctx, Facts(d)),
    };

    static async Task<Ok<DecisionMutation>> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, Notifier notify, TeamService team, TimeProvider clock, SettingsStore store)
        => await Tx.Notice<Ok<DecisionMutation>>(db, store, async () =>
    {
        var (d, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(d)));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, d, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        var enabled = (await store.Get(db)).IsFlagOn(DecisionMetadata.Flag);
        var metadata = await DecisionMetadata.Patch(db, d, body, enabled, access.Actor, ctx);
        // Stage every old and new field before assigning or invoking membership/notification side effects.
        var subject = patch.Has("subject") ? Check.Required(patch.Str("subject"), "subject", 300) : d.Subject;
        var description = patch.Has("description") ? Check.Required(patch.Str("description"), "description", 8000) : d.Description;
        var impact = patch.Has("impactLevel") ? patch.Str("impactLevel") : d.ImpactLevel;
        if (patch.Has("impactLevel")) Check.OneOf(impact, Impact.All, "impactLevel");
        var impactDescription = patch.Has("impactDescription") ? Check.Required(patch.Str("impactDescription"), "impactDescription", 2000) : d.ImpactDescription;
        var requester = d.RequestedById;
        if (patch.Has("requestedById")) { requester = patch.Id("requestedById") ?? throw ApiException.Invalid("requestedById", "error.required"); await DeliverableEndpoints.ActivePerson(db, requester, "requestedById"); }
        var requested = patch.Has("dateRequested") ? patch.Date("dateRequested") ?? throw ApiException.Invalid("dateRequested", "error.required") : d.DateRequested;
        if (enabled && d.Status == DecisionStatus.Decided && patch.Has("dateRequested")) Check.That(d.DecisionDate is { } date && date >= requested, "dateRequested", "error.invalid");
        var required = d.RequiredByDate;
        if (patch.Has("requiredByDate")) { Check.That(DecisionStatus.IsOpen(d.Status), "requiredByDate", "decision.date_closed"); required = patch.Date("requiredByDate") ?? throw ApiException.Invalid("requiredByDate", "error.required"); }
        var ownerUser = d.OwnerUserId; var ownerParty = d.OwnerExternalPartyId;
        if (patch.Has("ownerUserId") || patch.Has("ownerExternalPartyId"))
        {
            ownerUser = patch.Has("ownerUserId") ? patch.Id("ownerUserId") : null;
            ownerParty = patch.Has("ownerExternalPartyId") ? patch.Id("ownerExternalPartyId") : null;
            await Owner(db, d.ProjectId, ownerUser, ownerParty);
        }
        db.Audit.Reason = patch.Str("reason");
        var oldOwner = d.OwnerUserId;
        (d.Subject, d.Description, d.ImpactLevel, d.ImpactDescription, d.RequestedById, d.DateRequested, d.RequiredByDate, d.OwnerUserId, d.OwnerExternalPartyId)
            = (subject, description, impact!, impactDescription, requester, requested, required, ownerUser, ownerParty);
        DecisionMetadata.Apply(d, metadata);
        d.LastActivityAt = clock.GetUtcNow();
        if (d.OwnerUserId != oldOwner)
        {
            await team.EnsureMember(p, d.OwnerUserId, ProjectRole.TeamMember);
            await notify.Send(NotificationEvents.DecisionAssigned, d.OwnerUserId, Item(p, d), Text.Get("notify.decision_assigned", await notify.ActorName(), d.Key, d.Subject));
        }
        await db.SaveChangesAsync();
        Http.ETag(http, d);
        return TypedResults.Ok(new DecisionMutation(d.Id, d.RowVersion));
    });

    static async Task<Created<DecisionLinkMutation>> AddLink(Guid id, LinkInput body, Access access, HubDb db)
    {
        var (d, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.EditRegisterItem(access.Actor, ctx, Facts(d)));
        var link = await Link(db, access, p, d, body);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/item-links/{link.Id}", new DecisionLinkMutation(link.Id));
    }

    /// Unlinks a decision's, risk's or issue's item.
    static async Task<NoContent> RemoveLink(Guid id, Access access, HubDb db, TimeProvider clock)
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
        return TypedResults.NoContent();
    }

    // ---------- Record, defer, cancel, reopen (DEC-02, DEC-03, DEC-07, FR-011, Workflow 11) ----------

    static async Task<Ok<DecisionTransitionMutation>> Transition(Guid id, TransitionBody body, HttpContext http, Access access, HubDb db, Notifier notify, SettingsStore store, TimeProvider clock, CurrentUser me)
        => await Tx.Notice<Ok<DecisionTransitionMutation>>(db, store, async () =>
    {
        var (d, p, ctx) = await Load(db, access, id);
        var metadataOn = (await store.Get(db)).IsFlagOn(DecisionMetadata.Flag);
        var rationale = DecisionMetadata.Outcome(body.ToStatus, body.Rationale, metadataOn);
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
                if (metadataOn) Check.That(d.DecisionDate >= d.DateRequested, "decisionDate", "error.invalid");
                d.DecidedById = me.Id;
                d.Rationale = rationale;
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
                d.Rationale = null;
                break;
        }
        reason ??= string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        ProjectEndpoints.CorrectionReason(p, reason);
        var now = clock.GetUtcNow();
        d.Status = to;
        d.StatusChangedAt = now;
        d.LastActivityAt = now;
        db.Audit.Note(d, action: from == DecisionStatus.Decided ? "Reopened" : to switch { DecisionStatus.Decided => "Decided", DecisionStatus.Deferred => "Deferred", _ => null }, reason: reason);
        if (from == DecisionStatus.Decided && to == DecisionStatus.Pending)
        {
            var linked = await db.DesignBasisVersions
                .Where(v => v.ProjectId == d.ProjectId && v.DecisionId == d.Id && v.Status == BasisStatus.Confirmed)
                .ToDictionaryAsync(v => v.Id, v => v.EntryId);
            if (linked.Count > 0)
            {
                var versionIds = linked.Keys.ToList();
                var owners = new Dictionary<Guid, List<Guid?>>();
                var linkedUses = await db.BasisUses.Where(u => u.ProjectId == d.ProjectId && versionIds.Contains(u.VersionId)).ToListAsync();
                // Each entry's current use per consuming item, as replacement and withdrawal assess it.
                foreach (var use in linkedUses.GroupBy(u => new { Entry = linked[u.VersionId], u.TargetType, u.TargetId })
                    .Select(g => g.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).First()))
                {
                    if (!await db.BasisImpactAssessments.AnyAsync(a => a.ProjectId == d.ProjectId &&
                        a.BasisUseId == use.Id && a.OldVersionId == use.VersionId && a.NewVersionId == null &&
                        a.Status == AssessmentStatus.Pending))
                    {
                        var assessment = new BasisImpactAssessment { ProjectId = d.ProjectId, BasisUseId = use.Id,
                            OldVersionId = use.VersionId, NewVersionId = null, OwnerId = use.OwnerId };
                        db.BasisImpactAssessments.Add(assessment);
                        db.Audit.Note(assessment, action: "DecisionReopened", reason: reason);
                        if (!owners.TryGetValue(linked[use.VersionId], out var recipients)) owners[linked[use.VersionId]] = recipients = [];
                        recipients.Add(use.OwnerId);
                    }
                }
                // FR-BAS-05: the reopen asks consumers to assess; it does not unconfirm their basis. The notice names the
                // decision and opens the affected entry, so it is sent once per entry like the other basis notices.
                var entryIds = owners.Keys.ToList();
                foreach (var entry in await db.DesignBasisEntries.Where(e => entryIds.Contains(e.Id)).OrderBy(e => e.Seq).ToListAsync())
                    await DesignBasisEndpoints.Notify(notify, p, entry, NotificationEvents.BasisImpactPending, owners[entry.Id],
                        Text.Get("notify.basis_decision_reopened", d.Key, entry.Key));
            }
        }
        if (to == DecisionStatus.Decided) // §17.2: linked task assignees (unless unticked, FR-011) and the requester
        {
            List<Guid?> assignees = body.NotifyAssignees == false ? [] : await db.ItemLinks.Where(l => l.SourceId == d.Id && l.TargetType == ItemType.Task)
                .Join(db.Tasks, l => l.TargetId, t => t.Id, (l, t) => t.AssigneeId).ToListAsync();
            await notify.Send(NotificationEvents.DecisionRecorded, [.. assignees, d.RequestedById], Item(p, d),
                Text.Get("notify.decision_recorded", await notify.ActorName(), d.Key, d.Subject), d.DecisionText);
        }
        await db.SaveChangesAsync();
        return TypedResults.Ok(new DecisionTransitionMutation(d.Id, d.Status, d.RowVersion));
    });
}
