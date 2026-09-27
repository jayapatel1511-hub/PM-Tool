using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Engineering Deliverables Register (§12.4, §13.6, §15.5, Appendix C).
public static class DeliverableEndpoints
{
    public sealed record CreateBody(string Name, Guid ProjectDisciplineId, Guid DeliverableTypeId, string? Description, Guid? OwnerId, Guid? ReviewerId,
        Guid? MilestoneId, DateOnly? StartDate, DateOnly? DueDate, string? Priority, string? Revision, bool? RequiresReview);
    public sealed record TransitionBody(string ToStatus, string? Reason, string? Comment, int? RowVersion);
    public sealed record IssueBody(DateOnly? IssuedDate, string? Revision, string? IssuedTo, string? TransmittalUrl, string? Note, bool? ConfirmOpenTasks, int? RowVersion);
    public sealed record BulkBody(Guid[] Ids, string Operation, JsonElement? Params, string? Reason);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/deliverables", List);
        api.MapPost("/projects/{id:guid}/deliverables", Create);
        api.MapPost("/projects/{id:guid}/deliverables/bulk", Bulk);
        api.MapGet("/deliverables/{id}", Get);
        api.MapPatch("/deliverables/{id:guid}", Edit);
        api.MapPost("/deliverables/{id:guid}/transition", Transition);
        api.MapPost("/deliverables/{id:guid}/issue", Issue);
        api.MapDelete("/deliverables/{id:guid}", Delete);
    }

    public static async Task<(Deliverable D, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var d = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(d.ProjectId);
        return (d, p, ctx);
    }

    public static async Task<DeliverableFacts> Facts(HubDb db, Deliverable d) => new(d.ProjectDisciplineId, d.OwnerId, d.ReviewerId,
        await db.Tasks.AnyAsync(t => t.DeliverableId == d.Id && t.Status == TaskStatuses.Complete));

    // ---------- Register (FR-DEL-05, §13.6) ----------

    public sealed record DeliverableQuery(string? DisciplineId, string? Status, string? MilestoneId, string? OwnerId, string? TypeId, DateOnly? DueFrom, DateOnly? DueTo,
        string? Indicator, bool? RequiresReview, string? Q, string? Sort, bool? IncludeTasks);

    public static async Task<List<object>> List(Guid id, [AsParameters] DeliverableQuery f, Access access, HubDb db)
    {
        await access.Project(id, track: false);
        var query = Filter(db, db.Deliverables.AsNoTracking().Where(d => d.ProjectId == id), f.DisciplineId, f.Status, f.MilestoneId, f.OwnerId, f.TypeId, f.DueFrom, f.DueTo, f.Indicator, f.RequiresReview, f.Q);
        return await Rows(db, query, f.IncludeTasks == true);
    }

    /// The register's filters, shared with the dashboard counts so a number always equals the list it opens (§13.1).
    public static IQueryable<Deliverable> Filter(HubDb db, IQueryable<Deliverable> query, string? disciplineId = null, string? status = null, string? milestoneId = null,
        string? ownerId = null, string? typeId = null, DateOnly? dueFrom = null, DateOnly? dueTo = null, string? indicator = null, bool? requiresReview = null, string? q = null)
    {
        var disc = Http.Ids(disciplineId); if (disc.Length > 0) query = query.Where(d => disc.Contains(d.ProjectDisciplineId));
        var st = Http.List(status); if (st.Length > 0) query = query.Where(d => st.Contains(d.Status));
        var ms = Http.Ids(milestoneId); if (ms.Length > 0) query = query.Where(d => d.MilestoneId != null && ms.Contains(d.MilestoneId.Value));
        var own = Http.Ids(ownerId); if (own.Length > 0) query = query.Where(d => d.OwnerId != null && own.Contains(d.OwnerId.Value));
        var types = Http.Ids(typeId); if (types.Length > 0) query = query.Where(d => types.Contains(d.DeliverableTypeId));
        if (dueFrom is { } f) query = query.Where(d => d.DueDate >= f);
        if (dueTo is { } t) query = query.Where(d => d.DueDate <= t);
        if (requiresReview is { } rr) query = query.Where(d => d.RequiresReview == rr);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(d => EF.Functions.ILike(d.Name, $"%{q.Trim()}%") || EF.Functions.ILike(d.Key, $"%{q.Trim()}%"));
        foreach (var ind in Http.List(indicator))
            query = ind switch
            {
                "overdue" => query.Where(d => db.DeliverableStates.Any(s => s.DeliverableId == d.Id && s.IsOverdue)),
                "atRisk" => query.Where(d => db.DeliverableStates.Any(s => s.DeliverableId == d.Id && s.IsAtRisk)),
                "dueSoon" => query.Where(d => db.DeliverableStates.Any(s => s.DeliverableId == d.Id && s.IsDueSoon)),
                "unassigned" => query.Where(d => d.OwnerId == null),
                "dateInconsistent" => query.Where(d => db.DeliverableStates.Any(s => s.DeliverableId == d.Id && s.IsDateInconsistent)),
                "slipped" => query.Where(d => db.DeliverableStates.Any(s => s.DeliverableId == d.Id && s.SlipDays > 0)),
                "open" => query.Where(d => d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled),
                _ => query,
            };
        return query;
    }

    public static async Task<List<object>> Rows(HubDb db, IQueryable<Deliverable> q, bool includeTasks = false)
    {
        var list = await q.OrderBy(d => d.DueDate == null).ThenBy(d => d.DueDate).ThenBy(d => d.Seq).Select(d => new
        {
            d.Id, d.ProjectId, d.Key, d.Name, d.ProjectDisciplineId, d.DeliverableTypeId, d.OwnerId, d.ReviewerId, d.MilestoneId, d.StartDate, d.DueDate,
            d.OriginalDueDate, d.OriginalStartDate, d.Priority, d.Status, d.Revision, d.IssuedDate, d.IssuedTo, d.RequiresReview, d.RequiredReviewPackageId, d.RowVersion, d.CreatedAt, d.LastActivityAt,
            Discipline = db.ProjectDisciplines.Where(x => x.Id == d.ProjectDisciplineId).Select(x => new { x.Discipline!.Name, x.Discipline.Colour, x.SortOrder }).FirstOrDefault(),
            Type = db.DeliverableTypes.Where(x => x.Id == d.DeliverableTypeId).Select(x => x.Name).FirstOrDefault(),
            Owner = db.Users.Where(u => u.Id == d.OwnerId).Select(u => new { u.DisplayName, u.IsActive }).FirstOrDefault(),
            Reviewer = db.Users.Where(u => u.Id == d.ReviewerId).Select(u => new { u.DisplayName, u.IsActive }).FirstOrDefault(),
            Milestone = db.Milestones.Where(m => m.Id == d.MilestoneId).Select(m => new { m.Key, m.Name, m.Date, m.IsCancelled }).FirstOrDefault(),
            State = db.DeliverableStates.Where(s => s.DeliverableId == d.Id).FirstOrDefault(),
        }).ToListAsync();
        var tasks = includeTasks
            ? (await db.Tasks.AsNoTracking().Where(t => t.DeliverableId != null && list.Select(x => x.Id).Contains(t.DeliverableId.Value)).OrderBy(t => t.SortOrder).ThenBy(t => t.Seq)
                .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.DeliverableId, Assignee = db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.DisplayName).FirstOrDefault() })
                .ToListAsync()).ToLookup(t => t.DeliverableId)
            : null;
        return list.Select(d => (object)new
        {
            d.Id, d.ProjectId, d.Key, d.Name, d.ProjectDisciplineId, d.DeliverableTypeId, d.OwnerId, d.ReviewerId, d.MilestoneId, d.StartDate, d.DueDate,
            d.OriginalDueDate, d.OriginalStartDate, d.Priority, d.Status, d.Revision, d.IssuedDate, d.IssuedTo, d.RequiresReview, d.RequiredReviewPackageId, d.RowVersion, d.CreatedAt, d.LastActivityAt,
            DisciplineName = d.Discipline?.Name, DisciplineColour = d.Discipline?.Colour, DisciplineOrder = d.Discipline?.SortOrder ?? 0, TypeName = d.Type,
            OwnerName = d.Owner?.DisplayName, OwnerActive = d.Owner?.IsActive ?? true, ReviewerName = d.Reviewer?.DisplayName,
            MilestoneKey = d.Milestone?.Key, MilestoneName = d.Milestone?.Name, MilestoneDate = d.Milestone?.Date,
            State = d.State is null ? null : new
            {
                d.State.ProgressPct, d.State.TaskTotal, d.State.TaskComplete, d.State.TaskCancelled, d.State.TaskOpen, d.State.TaskOverdue, d.State.TaskBlocked,
                d.State.EstimatedHoursTotal, d.State.RemainingHours, d.State.IsOverdue, d.State.DaysOverdue, d.State.IsDueSoon, d.State.IsAtRisk,
                AtRiskReasons = J.El(d.State.AtRiskReasons), d.State.IsUnassigned, d.State.IsStale, d.State.IsDateInconsistent,
                InconsistencyDetail = J.El(d.State.InconsistencyDetail), d.State.SlipDays, d.State.IsInactiveOwner, d.State.IssuedWithOpenWork,
                d.State.MilestoneCancelled, d.State.IsWaiting, d.State.IsBlocked, BlockedBy = J.El(d.State.BlockedBy), d.State.BlockingCount,
                d.State.DerivedPredecessorIds, d.State.DerivedSuccessorIds,
            },
            Tasks = tasks?[d.Id].ToList(),
        }).ToList();
    }

    // ---------- Create (FR-DEL-01, DL-01, DL-03, DL-13, AC-DEL-01, AC-PERM-02) ----------

    static async Task<IResult> Create(Guid id, CreateBody body, Access access, HubDb db, TeamService team, Notifier notify)
    {
        var (p, ctx) = await access.Project(id);
        var pd = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.Id == body.ProjectDisciplineId && x.ProjectId == id) ?? throw ApiException.Invalid("projectDisciplineId", "error.required");
        var perm = Permissions.CreateDeliverable(access.Actor, ctx, pd.Id);
        if (!perm) throw ApiException.Forbidden(perm.Why!, await DisciplineName(db, pd.Id));
        Check.That(pd.IsActive, "projectDisciplineId", "deliverable.inactive_discipline");
        Check.That(await db.DeliverableTypes.AnyAsync(x => x.Id == body.DeliverableTypeId), "deliverableTypeId", "error.required");
        var owner = body.OwnerId ?? pd.LeadUserId ?? throw ApiException.Invalid("ownerId", "error.required");
        await ActivePerson(db, owner, "ownerId");
        if (body.ReviewerId is { } rev) await ActivePerson(db, rev, "reviewerId");
        Milestone? ms = null;
        if (body.MilestoneId is { } mid) ms = await db.Milestones.FirstOrDefaultAsync(m => m.Id == mid && m.ProjectId == id) ?? throw ApiException.Invalid("milestoneId", "error.not_found");
        var due = body.DueDate ?? ms?.Date; // FR-DEL-06 / AC-DEL-01
        if (body.StartDate is { } s && due is { } du) Check.That(s <= du, "startDate", "deliverable.start_after_due"); // DL-13
        if (body.Priority is not null) Check.OneOf(body.Priority, Priority.All, "priority");
        var d = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, id, p.ProjectNumber, "deliverable");
            var d = new Deliverable
            {
                ProjectId = id, Seq = seq, Key = key, Name = Check.Required(body.Name, "name", 200), ProjectDisciplineId = pd.Id, DeliverableTypeId = body.DeliverableTypeId,
                Description = Check.Optional(body.Description, "description", 8000), OwnerId = owner, ReviewerId = body.ReviewerId, MilestoneId = body.MilestoneId,
                StartDate = body.StartDate, DueDate = due, OriginalStartDate = body.StartDate, OriginalDueDate = due, Priority = body.Priority ?? Priority.Medium,
                Revision = Check.Optional(body.Revision, "revision", 50), RequiresReview = body.RequiresReview ?? true, LastActivityAt = DateTimeOffset.UtcNow, SortOrder = seq,
            };
            db.Deliverables.Add(d);
            await team.EnsureMember(p, owner, ProjectRole.TeamMember);
            await team.EnsureMember(p, body.ReviewerId, ProjectRole.Reviewer);
            await db.SaveChangesAsync();
            await notify.Send(NotificationEvents.DeliverableOwned, owner, Item(p, d), Text.Get("notify.deliverable_owned", await notify.ActorName(), d.Key, d.Name));
            await db.SaveChangesAsync();
            return d;
        });
        var warnings = Warnings(d, ms);
        return Results.Created($"/api/v1/deliverables/{d.Id}", new { d.Id, d.Key, d.RowVersion, d.DueDate, warnings });
    }

    static string[] Warnings(Deliverable d, Milestone? ms) =>
        ms?.Date is { } md && d.DueDate is { } dd && dd > md ? [Text.Get("deliverable.after_milestone", d.Key, ms.Key, md.ToString("yyyy-MM-dd"))] : []; // DL-03

    public static async Task ActivePerson(HubDb db, Guid id, string field) =>
        Check.That(await db.Users.AnyAsync(u => u.Id == id && u.IsActive), field, "team.inactive_user"); // G-11

    static async Task<string> DisciplineName(HubDb db, Guid pdId) =>
        await db.ProjectDisciplines.Where(x => x.Id == pdId).Select(x => x.Discipline!.Name).FirstAsync();

    // ---------- Detail (§13.6.1) ----------

    static async Task<object> Get(string id, Access access, HubDb db)
    {
        var d = (Guid.TryParse(id, out var g) ? await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(d.ProjectId, track: false);
        var row = (await Rows(db, db.Deliverables.AsNoTracking().Where(x => x.Id == d.Id), includeTasks: true)).Single();
        var facts = await Facts(db, d);
        var a = access.Actor;
        var transitions = DeliverableStatus.All.Where(to => to != DeliverableStatus.Issued && Workflow.DeliverableStep(d.Status, to, d.RequiresReview, d.PreviousStatus))
            .Select(to => new { To = to, Allowed = Permissions.DeliverableTransition(a, ctx, facts, d.Status, to).Ok, NeedsReason = Workflow.DeliverableNeedsReason(to) || to == DeliverableStatus.RevisionRequired });
        var guard = Workflow.DeliverableGuard(d.Status, DeliverableStatus.ReadyToIssue, d.RequiresReview);
        var issuePerm = Permissions.DeliverableTransition(a, ctx, facts, DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued);
        var canIssue = issuePerm.Ok && (d.Status == DeliverableStatus.ReadyToIssue
            || ((Permissions.IsPM(a, ctx) || Permissions.IsDL(ctx, d.ProjectDisciplineId)) && (d.Status == DeliverableStatus.InReview || (!d.RequiresReview && d.Status is DeliverableStatus.InProgress or DeliverableStatus.NotStarted))));
        var derivedPreds = await DerivedDeps(db, d.Id, predecessors: true);
        var derivedSuccs = await DerivedDeps(db, d.Id, predecessors: false);
        var issues = await db.DeliverableIssues.AsNoTracking().Where(i => i.DeliverableId == d.Id).OrderByDescending(i => i.IssuedDate).ThenByDescending(i => i.CreatedAt)
            .Select(i => new { i.Id, i.IssuedDate, i.Revision, i.IssuedTo, i.TransmittalUrl, i.Note, IssuedBy = db.Users.Where(u => u.Id == i.IssuedBy).Select(u => u.DisplayName).FirstOrDefault() }).ToListAsync();
        var edit = Permissions.EditDeliverable(a, ctx, facts);
        return new
        {
            Deliverable = row, d.Description, d.TransmittalUrl, d.IssueNote, d.AcceptedDate, d.OnHoldReason, d.CancelledReason, d.PreviousStatus,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            DerivedPredecessors = derivedPreds, DerivedSuccessors = derivedSuccs, Issues = issues,
            ExplicitPredecessors = await ExplicitDeps(db, d.Id, predecessors: true), ExplicitSuccessors = await ExplicitDeps(db, d.Id, predecessors: false),
            Permissions = new
            {
                Edit = new { edit.Ok, Reason = edit.Why is null ? null : Text.Get(edit.Why, await DisciplineName(db, d.ProjectDisciplineId)) },
                Delete = Permissions.DeleteDeliverable(a, ctx, facts).Ok, Transitions = transitions, Issue = canIssue,
                LinkDeliverables = Permissions.ManageDeliverableDependency(a, ctx, d.ProjectDisciplineId, d.ProjectDisciplineId).Ok, // the other end's lead may also link
                IssueNeedsConfirm = d.Status != DeliverableStatus.ReadyToIssue,
                ReadyToIssueGuard = guard is null ? null : Text.Get(guard), Comment = Permissions.Comment(a, ctx).Ok,
                CreateTask = Permissions.CreateTask(a, ctx, d.ProjectDisciplineId).Ok, NeedsReason = p.Status == ProjectStatus.Complete,
            },
        };
    }

    /// D-18: deliverable B depends on A when a task in B has a predecessor in A (read-only).
    /// Explicit deliverable dependencies (FR-DEP-09, packet 021) with their lag and note, shown apart from derived ones.
    static async Task<List<object>> ExplicitDeps(HubDb db, Guid deliverableId, bool predecessors)
    {
        var edges = await db.DeliverableDependencies.AsNoTracking()
            .Where(e => predecessors ? e.SuccessorDeliverableId == deliverableId : e.PredecessorDeliverableId == deliverableId).OrderBy(e => e.CreatedAt).ToListAsync();
        var ids = edges.Select(e => predecessors ? e.PredecessorDeliverableId : e.SuccessorDeliverableId).ToList();
        var items = await db.Deliverables.AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return [.. edges.Where(e => items.ContainsKey(predecessors ? e.PredecessorDeliverableId : e.SuccessorDeliverableId)).Select(e =>
        {
            var x = items[predecessors ? e.PredecessorDeliverableId : e.SuccessorDeliverableId];
            return (object)new { DependencyId = e.Id, e.LagDays, e.Note, x.Id, x.Key, x.Name, x.Status, x.DueDate, x.IssuedDate, x.ProjectDisciplineId };
        })];
    }

    static async Task<List<object>> DerivedDeps(HubDb db, Guid deliverableId, bool predecessors)
    {
        var q = predecessors
            ? from dep in db.Dependencies join s in db.Tasks on dep.SuccessorTaskId equals s.Id join pr in db.Tasks on dep.PredecessorTaskId equals pr.Id
              where s.DeliverableId == deliverableId && pr.DeliverableId != null && pr.DeliverableId != deliverableId select pr.DeliverableId!.Value
            : from dep in db.Dependencies join s in db.Tasks on dep.SuccessorTaskId equals s.Id join pr in db.Tasks on dep.PredecessorTaskId equals pr.Id
              where pr.DeliverableId == deliverableId && s.DeliverableId != null && s.DeliverableId != deliverableId select s.DeliverableId!.Value;
        var ids = await q.Distinct().ToListAsync();
        return (await db.Deliverables.AsNoTracking().Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Name, x.Status, x.DueDate }).ToListAsync()).Cast<object>().ToList();
    }

    // ---------- Edit (FR-DEL-01, DL-10, E-16) ----------

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db, TeamService team, Notifier notify)
    {
        var (d, p, ctx) = await Load(db, access, id);
        var facts = await Facts(db, d);
        var perm = Permissions.EditDeliverable(access.Actor, ctx, facts);
        if (!perm) throw ApiException.Forbidden(perm.Why!, await DisciplineName(db, d.ProjectDisciplineId));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, d, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        var oldOwner = d.OwnerId;
        if (patch.Has("name")) d.Name = Check.Required(patch.Str("name"), "name", 200);
        if (patch.Has("description")) d.Description = Check.Optional(patch.Str("description"), "description", 8000);
        if (patch.Has("deliverableTypeId")) d.DeliverableTypeId = patch.Id("deliverableTypeId") ?? throw ApiException.Invalid("deliverableTypeId", "error.required");
        if (patch.Has("ownerId")) { var o = patch.Id("ownerId") ?? throw ApiException.Invalid("ownerId", "error.required"); await ActivePerson(db, o, "ownerId"); d.OwnerId = o; await team.EnsureMember(p, o, ProjectRole.TeamMember); }
        if (patch.Has("reviewerId")) { var r = patch.Id("reviewerId"); if (r is { } rv) { await ActivePerson(db, rv, "reviewerId"); await team.EnsureMember(p, rv, ProjectRole.Reviewer); } d.ReviewerId = r; }
        if (patch.Has("priority")) { var pr = patch.Str("priority"); Check.OneOf(pr, Priority.All, "priority"); d.Priority = pr!; }
        if (patch.Has("revision")) d.Revision = Check.Optional(patch.Str("revision"), "revision", 50);
        if (patch.Has("requiresReview")) d.RequiresReview = patch.Bool("requiresReview") ?? true;
        if (patch.Has("startDate")) d.StartDate = patch.Date("startDate");
        if (patch.Has("dueDate")) d.DueDate = patch.Date("dueDate");
        Milestone? ms = null;
        if (patch.Has("milestoneId"))
        {
            var mid = patch.Id("milestoneId");
            if (mid is { } m) ms = await db.Milestones.FirstOrDefaultAsync(x => x.Id == m && x.ProjectId == d.ProjectId) ?? throw ApiException.Invalid("milestoneId", "error.not_found");
            d.MilestoneId = mid;
            if (d.DueDate is null && ms?.Date is { } md) d.DueDate = md;
        }
        else if (d.MilestoneId is { } cur) ms = await db.Milestones.FirstOrDefaultAsync(x => x.Id == cur);
        if (d.StartDate is { } s && d.DueDate is { } du) Check.That(s <= du, "startDate", "deliverable.start_after_due");
        d.OriginalDueDate ??= d.DueDate;
        d.OriginalStartDate ??= d.StartDate;
        if (patch.Has("projectDisciplineId"))
        {
            var pdId = patch.Id("projectDisciplineId") ?? throw ApiException.Invalid("projectDisciplineId", "error.required");
            if (pdId != d.ProjectDisciplineId)
            {
                var pd = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.Id == pdId && x.ProjectId == d.ProjectId && x.IsActive) ?? throw ApiException.Invalid("projectDisciplineId", "error.not_found");
                Access.Demand(Permissions.CreateDeliverable(access.Actor, ctx, pd.Id));
                var tasks = await db.Tasks.Where(t => t.DeliverableId == d.Id).ToListAsync();
                if (tasks.Count > 0 && patch.Bool("confirmMoveTasks") != true) // DL-10
                    throw ApiException.Conflict("confirm_move_tasks", "deliverable.confirm_move_tasks", new { tasks = tasks.Select(t => new { t.Key, t.Name }) }, tasks.Count);
                foreach (var t in tasks) t.ProjectDisciplineId = pd.Id;
                d.ProjectDisciplineId = pd.Id;
            }
        }
        d.LastActivityAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        if (d.OwnerId != oldOwner)
        {
            await notify.Send(NotificationEvents.DeliverableOwned, d.OwnerId, Item(p, d), Text.Get("notify.deliverable_owned", await notify.ActorName(), d.Key, d.Name));
            await notify.Send(NotificationEvents.WorkReassignedAway, oldOwner, Item(p, d), Text.Get("notify.reassigned_away", await notify.ActorName(), d.Key, d.Name));
            await db.SaveChangesAsync();
        }
        Http.ETag(http, d);
        return Results.Ok(new { d.Id, d.RowVersion, warnings = Warnings(d, ms) });
    }

    // ---------- Lifecycle (DL-02, DL-04, AC-DEL-03, AC-DEL-04, E-24) ----------

    static async Task<IResult> Transition(Guid id, TransitionBody body, HttpContext http, Access access, HubDb db, Notifier notify, TimeProvider clock, SettingsStore store)
    {
        var projectId = await db.Deliverables.Where(d => d.Id == id).Select(d => (Guid?)d.ProjectId).FirstOrDefaultAsync() ?? throw ApiException.NotFound();
        await access.Project(projectId, false);
        return await Tx.Run(db, async () => {
        await Coordination.Lock(db, projectId);
        var (d, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, d, body.RowVersion);
        var from = d.Status;
        var to = body.ToStatus;
        Check.OneOf(to, DeliverableStatus.All, "toStatus");
        if (to == DeliverableStatus.Issued) throw ApiException.Rule("use_issue", "deliverable.use_issue");
        if (Workflow.DeliverableGuard(from, to, d.RequiresReview) is { } guard) throw ApiException.Rule("guard", guard); // AC-DEL-03
        if (!Workflow.DeliverableStep(from, to, d.RequiresReview, d.PreviousStatus)) throw ApiException.Rule("illegal_transition", "deliverable.illegal_transition", null, from, to);
        Access.Demand(Permissions.DeliverableTransition(access.Actor, ctx, await Facts(db, d), from, to));
        if (to == DeliverableStatus.InReview && d.ReviewerId is null) throw ApiException.Invalid("reviewerId", "deliverable.reviewer_required"); // DL-04
        string? reason = Workflow.DeliverableNeedsReason(to) ? Check.Reason(body.Reason) : body.Reason?.Trim();
        ProjectEndpoints.CorrectionReason(p, reason);
        if (to == DeliverableStatus.ReadyToIssue) await ReviewEndpoints.Gate(db, p, d, null, (await store.Get(db)).AllowSelfReview);
        var now = clock.GetUtcNow();
        if (to == DeliverableStatus.OnHold) { d.PreviousStatus = from; d.OnHoldReason = reason; }
        if (to == DeliverableStatus.Cancelled) { d.PreviousStatus = from; d.CancelledReason = reason; }
        if (from == DeliverableStatus.OnHold) d.OnHoldReason = null;
        if (to == DeliverableStatus.Accepted) d.AcceptedDate = clock.Today(await store.Get(db));
        if (to == DeliverableStatus.RevisionRequired)
        {
            var text = Check.Required(body.Comment, "comment", 8000); // AC-DEL-04: comment mandatory, stored as a Review comment
            db.Comments.Add(new Comment { ProjectId = d.ProjectId, ItemType = ItemType.Deliverable, ItemId = d.Id, AuthorId = db.Audit.ActorId!.Value, Body = text,
                CommentKind = CommentKind.Review, CreatedAt = now, AuditKey = d.Key });
        }
        else if (!string.IsNullOrWhiteSpace(body.Comment))
            db.Comments.Add(new Comment { ProjectId = d.ProjectId, ItemType = ItemType.Deliverable, ItemId = d.Id, AuthorId = db.Audit.ActorId!.Value, Body = body.Comment.Trim(),
                CommentKind = CommentKind.StatusNote, CreatedAt = now, AuditKey = d.Key });
        d.Status = to;
        d.StatusChangedAt = now;
        d.LastActivityAt = now;
        db.Audit.Note(d, reason: reason);
        await db.SaveChangesAsync();
        if (to == DeliverableStatus.RevisionRequired)
            await notify.Send(NotificationEvents.ReviewOutcome, d.OwnerId, Item(p, d), Text.Get("notify.deliverable_revision", await notify.ActorName(), d.Key, d.Name), body.Comment);
        else if (to == DeliverableStatus.InReview)
            await notify.Send(NotificationEvents.ReviewRequested, d.ReviewerId, Item(p, d), Text.Get("notify.deliverable_review", await notify.ActorName(), d.Key, d.Name));
        await db.SaveChangesAsync();
        return Results.Ok(new { d.Id, d.Status, d.RowVersion });
        });
    }

    /// DL-05, DL-06, AC-DEL-05: issue records date, revision and recipient; open tasks need confirmation and stay open.
    static async Task<IResult> Issue(Guid id, IssueBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var projectId = await db.Deliverables.Where(d => d.Id == id).Select(d => (Guid?)d.ProjectId).FirstOrDefaultAsync() ?? throw ApiException.NotFound();
        await access.Project(projectId, false);
        return await Tx.Run(db, async () => {
        await Coordination.Lock(db, projectId);
        var (d, p, ctx) = await Load(db, access, id);
        await Http.CheckVersion(db, http, d, body.RowVersion);
        var facts = await Facts(db, d);
        var a = access.Actor;
        Access.Demand(Permissions.DeliverableTransition(a, ctx, facts, DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued));
        var manager = Permissions.IsPM(a, ctx) || Permissions.IsDL(ctx, d.ProjectDisciplineId);
        var allowedFrom = d.Status == DeliverableStatus.ReadyToIssue
            || (manager && (d.Status == DeliverableStatus.InReview || (!d.RequiresReview && d.Status is DeliverableStatus.InProgress or DeliverableStatus.NotStarted)));
        if (!allowedFrom) throw ApiException.Rule("guard", d.RequiresReview ? "guard.ready_to_issue_needs_review" : "deliverable.issue_not_ready");
        var open = await db.Tasks.Where(t => t.DeliverableId == d.Id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.Id, t.Key, t.Name, t.Status }).ToListAsync();
        if (open.Count > 0 && body.ConfirmOpenTasks != true)
            throw ApiException.Conflict("confirm_open_tasks", "deliverable.open_tasks", new { tasks = open }, open.Count);
        var today = clock.Today(await store.Get(db));
        var issued = body.IssuedDate ?? today;
        Check.That(issued <= today, "issuedDate", "milestone.future_completion");
        if (body.TransmittalUrl is { Length: > 0 } tu) Check.That(Links.IsValid(tu), "transmittalUrl", "link.invalid");
        await ReviewEndpoints.Gate(db, p, d, Check.Optional(body.Revision, "revision", 50), (await store.Get(db)).AllowSelfReview);
        var now = clock.GetUtcNow();
        d.Status = DeliverableStatus.Issued;
        d.IssuedDate = issued;
        d.Revision = Check.Optional(body.Revision, "revision", 50) ?? d.Revision;
        d.IssuedTo = Check.Optional(body.IssuedTo, "issuedTo", 300);
        d.TransmittalUrl = Check.Optional(body.TransmittalUrl, "transmittalUrl", 2000);
        d.IssueNote = Check.Optional(body.Note, "note", 2000);
        d.StatusChangedAt = now;
        d.LastActivityAt = now;
        db.DeliverableIssues.Add(new DeliverableIssue { ProjectId = d.ProjectId, DeliverableId = d.Id, IssuedDate = issued, Revision = d.Revision, IssuedTo = d.IssuedTo,
            TransmittalUrl = d.TransmittalUrl, Note = d.IssueNote, IssuedBy = me.Id, CreatedAt = now });
        db.Audit.Note(d, action: "Issued", reason: open.Count > 0 ? Text.Get("deliverable.issued_with_open", string.Join(", ", open.Select(o => o.Key))) : null);
        await db.SaveChangesAsync();
        return Results.Ok(new { d.Id, d.Status, d.RowVersion, openTasks = open });
        });
    }

    static async Task<IResult> Delete(Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me)
    {
        var (d, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.DeleteDeliverable(access.Actor, ctx, await Facts(db, d)));
        if (await db.Tasks.AnyAsync(t => t.DeliverableId == id)) throw ApiException.Rule("has_tasks", "deliverable.has_tasks"); // DL-09
        d.DeletedAt = clock.GetUtcNow();
        d.DeletedBy = me.Id;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    // ---------- Bulk (§13.6: set milestone, shift due dates, set owner; T-23 row by row) ----------

    static async Task<object> Bulk(Guid id, BulkBody body, Access access, HubDb db, TeamService team)
    {
        var (p, ctx) = await access.Project(id);
        var p2 = body.Params is { } pe ? new Patch(pe) : new Patch(default);
        var items = await db.Deliverables.Where(d => d.ProjectId == id && body.Ids.Contains(d.Id)).ToListAsync();
        var skipped = new List<object>();
        var updated = 0;
        if (body.Operation == "shiftDueDates") db.Audit.Reason = Check.Reason(body.Reason); // G-09 bulk date shift
        Milestone? ms = null;
        if (body.Operation == "setMilestone" && p2.Id("milestoneId") is { } mid) ms = await db.Milestones.FirstOrDefaultAsync(m => m.Id == mid && m.ProjectId == id) ?? throw ApiException.Invalid("milestoneId", "error.not_found");
        foreach (var d in items)
        {
            var perm = Permissions.EditDeliverable(access.Actor, ctx, await Facts(db, d));
            if (!perm) { skipped.Add(new { d.Id, d.Key, reason = Text.Get(perm.Why!, "") }); continue; }
            switch (body.Operation)
            {
                case "setMilestone": d.MilestoneId = ms?.Id; if (d.DueDate is null && ms?.Date is { } md) d.DueDate = md; break;
                case "shiftDueDates":
                    var days = p2.Int("days") ?? throw ApiException.Invalid("days", "error.required");
                    if (d.DueDate is { } du) d.DueDate = du.AddDays(days);
                    if (d.StartDate is { } s) d.StartDate = s.AddDays(days);
                    break;
                case "setOwner":
                    var o = p2.Id("ownerId") ?? throw ApiException.Invalid("ownerId", "error.required");
                    await ActivePerson(db, o, "ownerId");
                    d.OwnerId = o;
                    await team.EnsureMember(p, o, ProjectRole.TeamMember);
                    break;
                default: throw ApiException.Invalid("operation", "error.one_of", "setMilestone, shiftDueDates, setOwner");
            }
            updated++;
        }
        await db.SaveChangesAsync();
        return new { updated, skipped };
    }

    public static NotifyItem Item(Project p, Deliverable d) => new(p.Id, ItemType.Deliverable, d.Id, d.Key, $"/projects/{p.ProjectNumber}/deliverables?panel=Deliverable:{d.Id}", p.ProjectNumber);
}
