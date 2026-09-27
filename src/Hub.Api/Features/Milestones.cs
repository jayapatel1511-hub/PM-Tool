using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Milestones (§12.3, §13.7, §15.4): planning, date changes with cascade, completion, cancellation.
public static class MilestoneEndpoints
{
    public sealed record CreateBody(string Name, string MilestoneType, DateOnly? Date, string? Description, Guid? ProjectDisciplineId, Guid? CompletesPhaseId, bool? IsClientFacing);
    public sealed record ChangeDateBody(DateOnly NewDate, string? Reason, bool? CascadeDeliverables, bool? IncludeTasks, bool? DryRun, int? RowVersion);
    public sealed record CompleteBody(DateOnly? CompletedDate, bool? Confirm, int? RowVersion);
    public sealed record ReasonBody(string? Reason, int? RowVersion);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/milestones", List);
        api.MapPost("/projects/{id:guid}/milestones", Create);
        api.MapGet("/milestones/{id}", Get);
        api.MapPatch("/milestones/{id:guid}", Edit);
        api.MapPost("/milestones/{id:guid}/change-date", ChangeDate);
        api.MapPost("/milestones/{id:guid}/complete", Complete);
        api.MapPost("/milestones/{id:guid}/reopen", Reopen);
        api.MapPost("/milestones/{id:guid}/cancel", Cancel);
        api.MapDelete("/milestones/{id:guid}", Delete);
    }

    public static async Task<(Milestone M, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var m = await db.Milestones.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(m.ProjectId);
        return (m, p, ctx);
    }

    public sealed record MilestoneQuery(string? Type, string? Status, Guid? DisciplineId, bool? ShowCompleted);

    public static async Task<List<object>> List(Guid id, [AsParameters] MilestoneQuery f, Access access, HubDb db)
    {
        var (type, status, disciplineId, showCompleted) = f;
        await access.Project(id, track: false);
        var q = db.Milestones.AsNoTracking().Where(m => m.ProjectId == id);
        if (!string.IsNullOrEmpty(type)) q = q.Where(m => m.MilestoneType == type);
        if (disciplineId is { } d) q = q.Where(m => m.ProjectDisciplineId == d);
        if (showCompleted != true) q = q.Where(m => !m.IsComplete && !m.IsCancelled);
        var rows = await Rows(db, q);
        var statuses = Http.List(status);
        return statuses.Length == 0 ? rows : rows.Where(r => statuses.Contains((string)((dynamic)r).Status ?? "")).ToList();
    }

    public static async Task<List<object>> Rows(HubDb db, IQueryable<Milestone> q)
    {
        var list = await q.OrderBy(m => m.Date == null).ThenBy(m => m.Date).ThenBy(m => m.SortOrder).Select(m => new
        {
            m.Id, m.ProjectId, m.Key, m.Name, m.MilestoneType, m.Date, m.OriginalDate, m.Description, m.ProjectDisciplineId, m.CompletesPhaseId,
            m.IsClientFacing, m.IsComplete, m.CompletedDate, m.IsCancelled, m.CancelledReason, m.RowVersion,
            State = db.MilestoneStates.Where(s => s.MilestoneId == m.Id).Select(s => new { s.Status, s.StatusReasons, s.DaysRemaining, s.SlipDays, s.DeliverableTotal,
                s.DeliverableIssued, s.TaskTotal, s.TaskComplete, s.TaskOpen, s.TaskOverdue, s.TaskBlocked }).FirstOrDefault(),
            Deliverables = db.Deliverables.Count(d => d.MilestoneId == m.Id && d.Status != DeliverableStatus.Cancelled),
            Issued = db.Deliverables.Count(d => d.MilestoneId == m.Id && (d.Status == DeliverableStatus.Issued || d.Status == DeliverableStatus.Accepted)),
        }).ToListAsync();
        return list.Select(m => (object)new
        {
            m.Id, m.ProjectId, m.Key, m.Name, m.MilestoneType, m.Date, m.OriginalDate, m.Description, m.ProjectDisciplineId, m.CompletesPhaseId,
            m.IsClientFacing, m.IsComplete, m.CompletedDate, m.IsCancelled, m.CancelledReason, m.RowVersion,
            IsSubmission = MilestoneType.IsSubmission(m.MilestoneType),
            Status = m.IsComplete ? MilestoneStatus.Complete : m.IsCancelled ? MilestoneStatus.Cancelled : m.State?.Status,
            StatusReasons = J.El(m.State?.StatusReasons),
            SlipDays = m.Date is { } d && m.OriginalDate is { } o ? d.DayNumber - o.DayNumber : 0,
            DaysRemaining = m.State?.DaysRemaining,
            DeliverableTotal = m.Deliverables, DeliverableIssued = m.Issued,
            TaskTotal = m.State?.TaskTotal ?? 0, TaskComplete = m.State?.TaskComplete ?? 0, TaskOpen = m.State?.TaskOpen ?? 0,
            TaskOverdue = m.State?.TaskOverdue ?? 0, TaskBlocked = m.State?.TaskBlocked ?? 0,
        }).ToList();
    }

    static async Task<IResult> Create(Guid id, CreateBody body, Access access, HubDb db)
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        Check.OneOf(body.MilestoneType, MilestoneType.All, "milestoneType");
        Check.That(body.Date is not null, "date", "error.required"); // M-01
        if (body.ProjectDisciplineId is { } pd) Check.That(await db.ProjectDisciplines.AnyAsync(x => x.Id == pd && x.ProjectId == id), "projectDisciplineId", "error.not_found");
        var m = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, id, p.ProjectNumber, "milestone");
            var m = new Milestone
            {
                ProjectId = id, Seq = seq, Key = key, Name = Check.Required(body.Name, "name", 200), MilestoneType = body.MilestoneType, Date = body.Date,
                OriginalDate = body.Date, Description = Check.Optional(body.Description, "description"), ProjectDisciplineId = body.ProjectDisciplineId,
                CompletesPhaseId = body.CompletesPhaseId, IsClientFacing = body.IsClientFacing ?? MilestoneType.IsSubmission(body.MilestoneType), SortOrder = seq,
            };
            db.Milestones.Add(m);
            await db.SaveChangesAsync();
            return m;
        });
        return Results.Created($"/api/v1/milestones/{m.Id}", new { m.Id, m.Key, m.RowVersion });
    }

    static async Task<object> Get(string id, Access access, HubDb db)
    {
        var m = (Guid.TryParse(id, out var g) ? await db.Milestones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Milestones.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(m.ProjectId, track: false);
        var row = (await Rows(db, db.Milestones.AsNoTracking().Where(x => x.Id == m.Id))).Single();
        var deliverables = await DeliverableEndpoints.Rows(db, db.Deliverables.AsNoTracking().Where(d => d.MilestoneId == m.Id));
        var directTasks = await db.Tasks.AsNoTracking().Where(t => t.MilestoneId == m.Id).OrderBy(t => t.Seq)
            .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.AssigneeId }).ToListAsync();
        var phase = m.CompletesPhaseId is { } ph ? await db.Phases.Where(x => x.Id == ph).Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync() : null;
        var can = Permissions.ManageMilestones(access.Actor, ctx);
        return new
        {
            Milestone = row, Deliverables = deliverables, DirectTasks = directTasks, CompletesPhase = phase,
            Project = new { p.Id, p.ProjectNumber, p.Name, p.Status },
            Permissions = new { Manage = new { can.Ok, Reason = can.Why is null ? null : Text.Get(can.Why, can.Arg ?? p.Status) }, Comment = Permissions.Comment(access.Actor, ctx).Ok },
        };
    }

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db)
    {
        var (m, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        var patch = new Patch(body);
        await Http.CheckVersion(db, http, m, patch.RowVersion);
        ProjectEndpoints.CorrectionReason(p, patch.Str("reason"));
        db.Audit.Reason = patch.Str("reason");
        if (patch.Has("name")) m.Name = Check.Required(patch.Str("name"), "name", 200);
        if (patch.Has("milestoneType")) { var t = patch.Str("milestoneType"); Check.OneOf(t, MilestoneType.All, "milestoneType"); m.MilestoneType = t!; }
        if (patch.Has("description")) m.Description = Check.Optional(patch.Str("description"), "description");
        if (patch.Has("projectDisciplineId")) m.ProjectDisciplineId = patch.Id("projectDisciplineId");
        if (patch.Has("completesPhaseId")) m.CompletesPhaseId = patch.Id("completesPhaseId");
        if (patch.Has("isClientFacing")) m.IsClientFacing = patch.Bool("isClientFacing") ?? false;
        if (patch.Has("date"))
        {
            // Undated milestones (from templates or copies) get their first date here; later moves go through change-date.
            Check.That(m.Date is null, "date", "milestone.use_change_date");
            m.Date = patch.Date("date") ?? throw ApiException.Invalid("date", "error.required");
            m.OriginalDate ??= m.Date;
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { m.Id, m.RowVersion });
    }

    /// M-02..M-04, AC-MS-04, AC-MS-05, FR-MS-05; `includeTasks` extends the cascade to tasks (packet 018).
    static async Task<IResult> ChangeDate(Guid id, ChangeDateBody body, HttpContext http, Access access, HubDb db, Notifier notify)
    {
        var (m, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        Check.That(!m.IsComplete && !m.IsCancelled, "newDate", "milestone.closed");
        var old = m.Date;
        var delta = old is { } o ? body.NewDate.DayNumber - o.DayNumber : 0;
        var deliverables = body.CascadeDeliverables == true && delta != 0
            ? await db.Deliverables.Where(d => d.MilestoneId == id && d.DueDate != null && d.Status != DeliverableStatus.Issued
                && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled).OrderBy(d => d.Seq).ToListAsync()
            : [];
        var dIds = deliverables.Select(d => d.Id).ToList();
        var tasks = body.IncludeTasks == true && dIds.Count > 0
            ? await db.Tasks.Where(t => t.DeliverableId != null && dIds.Contains(t.DeliverableId.Value) && t.Status != TaskStatuses.Complete
                && t.Status != TaskStatuses.Cancelled && (t.DueDate != null || t.StartDate != null)).OrderBy(t => t.Seq).ToListAsync()
            : [];
        var preview = new
        {
            milestone = new { m.Key, m.Name, oldDate = old, newDate = body.NewDate, slipDays = m.OriginalDate is { } od ? body.NewDate.DayNumber - od.DayNumber : 0, delta },
            deliverables = deliverables.Select(d => new { d.Id, d.Key, d.Name, oldDue = d.DueDate, newDue = d.DueDate!.Value.AddDays(delta) }),
            tasks = tasks.Select(t => new { t.Id, t.Key, t.Name, oldDue = t.DueDate, newDue = t.DueDate?.AddDays(delta), oldStart = t.StartDate, newStart = t.StartDate?.AddDays(delta) }),
            inconsistent = await db.Deliverables.Where(d => d.MilestoneId == id && d.DueDate > body.NewDate && !dIds.Contains(d.Id)).Select(d => new { d.Key, d.Name, d.DueDate }).ToListAsync(),
        };
        if (body.DryRun == true) return Results.Ok(preview);
        await Http.CheckVersion(db, http, m, body.RowVersion);
        var reason = Check.Reason(body.Reason); // G-09: milestone date change needs a reason
        db.Audit.Reason = reason;
        m.Date = body.NewDate;
        m.OriginalDate ??= body.NewDate;
        foreach (var d in deliverables) { d.DueDate = d.DueDate!.Value.AddDays(delta); if (d.StartDate is { } s) d.StartDate = s.AddDays(delta); db.Audit.Note(d, action: "Cascade", reason: reason); }
        foreach (var t in tasks) { t.DueDate = t.DueDate?.AddDays(delta); t.StartDate = t.StartDate?.AddDays(delta); db.Audit.Note(t, action: "Cascade", reason: reason); }
        await db.SaveChangesAsync();
        var leads = await db.ProjectDisciplines.Where(x => x.ProjectId == p.Id && x.LeadUserId != null).Select(x => x.LeadUserId).ToListAsync();
        await notify.Send(NotificationEvents.MilestoneDateChanged, [p.ProjectManagerId, .. leads], Item(p, m),
            Text.Get("notify.milestone_moved", await notify.ActorName(), m.Key, m.Name, old?.ToString("yyyy-MM-dd") ?? "—", body.NewDate.ToString("yyyy-MM-dd"), (delta >= 0 ? "+" : "") + delta), reason);
        await db.SaveChangesAsync();
        return Results.Ok(new { m.Id, m.RowVersion, shifted = deliverables.Count + tasks.Count, preview });
    }

    static async Task<IResult> Complete(Guid id, CompleteBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (m, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        Check.That(!m.IsComplete && !m.IsCancelled, "completedDate", "milestone.closed");
        var today = clock.Today(await store.Get(db));
        if (m.Date is { } d && d > today.AddDays(7)) throw ApiException.Rule("too_early", "milestone.too_early"); // M-09
        var completed = body.CompletedDate ?? today;
        Check.That(completed <= today, "completedDate", "milestone.future_completion"); // M-06
        var open = await db.Deliverables.Where(x => x.MilestoneId == id && x.Status != DeliverableStatus.Issued && x.Status != DeliverableStatus.Accepted && x.Status != DeliverableStatus.Cancelled)
            .Select(x => new { x.Id, x.Key, x.Name, x.Status }).ToListAsync();
        if (open.Count > 0 && body.Confirm != true) // M-05, AC-MS-06
            throw ApiException.Conflict("confirm_open_deliverables", "milestone.open_deliverables", new { deliverables = open }, open.Count);
        ProjectEndpoints.CorrectionReason(p, null);
        m.IsComplete = true;
        m.CompletedDate = completed;
        if (open.Count > 0) db.Audit.Note(m, reason: Text.Get("milestone.completed_with_open", string.Join(", ", open.Select(o => o.Key))));
        await db.SaveChangesAsync();
        var phase = m.CompletesPhaseId is { } ph && p.PhaseId != ph ? await db.Phases.Where(x => x.Id == ph).Select(x => new { x.Id, x.Name }).FirstOrDefaultAsync() : null;
        return Results.Ok(new { m.Id, m.RowVersion, suggestPhase = phase }); // never advanced automatically (§9.3)
    }

    static async Task<IResult> Reopen(Guid id, ReasonBody body, HttpContext http, Access access, HubDb db)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        Check.That(m.IsComplete, "reason", "milestone.not_complete");
        db.Audit.Note(m, action: "Reopened", reason: Check.Reason(body.Reason)); // M-10
        m.IsComplete = false;
        m.CompletedDate = null;
        await db.SaveChangesAsync();
        return Results.Ok(new { m.Id, m.RowVersion });
    }

    /// M-07, E-17: links are kept; targeted deliverables are flagged for retargeting and listed for the bulk dialog.
    static async Task<IResult> Cancel(Guid id, ReasonBody body, HttpContext http, Access access, HubDb db)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        m.IsCancelled = true;
        m.CancelledReason = Check.Reason(body.Reason);
        db.Audit.Note(m, reason: m.CancelledReason);
        await db.SaveChangesAsync();
        var retarget = await db.Deliverables.Where(d => d.MilestoneId == id && d.Status != DeliverableStatus.Cancelled).Select(d => new { d.Id, d.Key, d.Name, d.RowVersion }).ToListAsync();
        return Results.Ok(new { m.Id, m.RowVersion, retarget });
    }

    static async Task<IResult> Delete(Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        if (await db.Deliverables.AnyAsync(d => d.MilestoneId == id) || await db.Tasks.AnyAsync(t => t.MilestoneId == id))
            throw ApiException.Rule("milestone_in_use", "milestone.in_use");
        m.DeletedAt = clock.GetUtcNow();
        m.DeletedBy = me.Id;
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    public static NotifyItem Item(Project p, Milestone m) => new(p.Id, ItemType.Milestone, m.Id, m.Key, $"/projects/{p.ProjectNumber}/milestones?panel=Milestone:{m.Id}", p.ProjectNumber);
}
