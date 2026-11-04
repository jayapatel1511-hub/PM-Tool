using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

namespace Hub.Api.Features;

/// Milestones (§12.3, §13.7, §15.4): planning, date changes with cascade, completion, cancellation.
public static class MilestoneEndpoints
{
    public sealed record CreateBody(string Name, string MilestoneType, DateOnly? Date, string? Description, Guid? ProjectDisciplineId, Guid? CompletesPhaseId, bool? IsClientFacing);
    public sealed record ChangeDateBody(DateOnly NewDate, string? Reason, bool? CascadeDeliverables, bool? IncludeTasks, bool? DryRun, int? RowVersion, Guid? DateReasonCategoryId = null);
    public sealed record CompleteBody(DateOnly? CompletedDate, bool? Confirm, int? RowVersion);
    public sealed record ReasonBody(string? Reason, int? RowVersion);
    public sealed record RebaselineBody(string? Reason, bool? Clear, int? RowVersion);
    public sealed record RebaselineRead(Guid Id, int RowVersion);
    public sealed record MilestoneCreated(Guid Id, string Key, int RowVersion);
    /// The milestone's id and its new row version, as the edit, reopen and restore routes answer.
    public sealed record MilestoneVersion(Guid Id, int RowVersion);
    public sealed record MilestonePhase(Guid Id, string Name);
    /// The milestone completed: the phase it completes, when that is not the project's phase, is only suggested (§9.3).
    public sealed record MilestoneCompleted(Guid Id, int RowVersion, MilestonePhase? SuggestPhase);
    public sealed record RetargetDeliverable(Guid Id, string Key, string Name, int RowVersion);
    /// The milestone cancelled and the deliverables still aimed at it, for the retarget dialog.
    public sealed record MilestoneCancelled(Guid Id, int RowVersion, IReadOnlyList<RetargetDeliverable> Retarget);
    public sealed record OpenDeliverable(Guid Id, string Key, string Name, string Status);
    /// The extra of the 409 problem that asks first: the deliverables not yet issued.
    sealed record DeliverablesToConfirm(IReadOnlyList<OpenDeliverable> Deliverables);
    public sealed record DateMove(string Key, string Name, DateOnly? OldDate, DateOnly NewDate, int SlipDays, int Delta);
    public sealed record DeliverableShift(Guid Id, string Key, string Name, DateOnly? OldDue, DateOnly NewDue);
    public sealed record TaskShift(Guid Id, string Key, string Name, DateOnly? OldDue, DateOnly? NewDue, DateOnly? OldStart, DateOnly? NewStart);
    public sealed record DeliverableLeftBehind(string Key, string Name, DateOnly? DueDate);
    /// What a date change would move. The shifted rows are enumerated when the response is written, as they always were: after a real move
    /// they read the dates already moved (the app reads only the dry run).
    public sealed record DatePreview(DateMove Milestone, IEnumerable<DeliverableShift> Deliverables, IEnumerable<TaskShift> Tasks, IReadOnlyList<DeliverableLeftBehind> Inconsistent);
    public sealed record DateChanged(Guid Id, int RowVersion, int Shifted, DatePreview Preview);
    public sealed record MilestoneTask(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, Guid? AssigneeId);
    public sealed record MilestoneProject(Guid Id, string ProjectNumber, string Name, string Status);
    public sealed record MilestonePermissions(RegisterEndpoints.RegisterPermission Manage, bool Comment);
    /// One milestone with its deliverables, the tasks aimed straight at it, the phase it completes and what the caller may do with it.
    public sealed record MilestoneDetail(MilestoneRow Milestone, IReadOnlyList<DeliverableEndpoints.DeliverableRow> Deliverables, IReadOnlyList<MilestoneTask> DirectTasks,
        MilestonePhase? CompletesPhase, MilestoneProject Project, MilestonePermissions Permissions, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/milestones", async Task<Ok<List<MilestoneRow>>> (Guid id, [AsParameters] MilestoneQuery f, Access access, HubDb db, SettingsStore store) =>
            TypedResults.Ok(await List(id, f, access, db, store)));
        api.MapPost("/projects/{id:guid}/milestones", Create);
        api.MapGet("/milestones/{id}", Get);
        api.MapPatch("/milestones/{id:guid}", Edit);
        api.MapPost("/milestones/{id:guid}/change-date", ChangeDate).AddOpenApiOperationTransformer(DescribeChangeDate);
        DateReasons.Map(api);
        api.MapPost("/milestones/{id:guid}/rebaseline", Rebaseline);
        api.MapPost("/milestones/{id:guid}/complete", Complete);
        api.MapPost("/milestones/{id:guid}/reopen", Reopen);
        api.MapPost("/milestones/{id:guid}/cancel", Cancel);
        api.MapDelete("/milestones/{id:guid}", Delete);
        api.MapPost("/milestones/{id:guid}/restore", Restore).RequireFlag(RecoveryEndpoints.Flag);
    }

    public static async Task<(Milestone M, Project P, ProjectContext Ctx)> Load(HubDb db, Access access, Guid id)
    {
        var m = await db.Milestones.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(m.ProjectId);
        return (m, p, ctx);
    }

    public sealed record MilestoneQuery(string? Type, string? Status, Guid? DisciplineId, bool? ShowCompleted);
    public sealed record MilestoneRow(Guid Id, Guid ProjectId, string Key, string Name, string MilestoneType, DateOnly? Date, DateOnly? OriginalDate,
        string? Description, Guid? ProjectDisciplineId, Guid? CompletesPhaseId, bool IsClientFacing, bool IsComplete, DateOnly? CompletedDate,
        bool IsCancelled, string? CancelledReason, int RowVersion, bool IsSubmission, string? Status, JsonElement? StatusReasons, int SlipDays,
        int? DaysRemaining, int DeliverableTotal, int DeliverableIssued, int TaskTotal, int TaskComplete, int TaskOpen, int TaskOverdue, int TaskBlocked,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SlipShown = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateOnly? BaselineDate = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? SinceBaselineDays = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? BaselineSlipShown = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? CanRebaseline = null);

    public static async Task<List<MilestoneRow>> List(Guid id, [AsParameters] MilestoneQuery f, Access access, HubDb db, SettingsStore store)
    {
        var (type, status, disciplineId, showCompleted) = f;
        await access.Project(id, track: false);
        var q = db.Milestones.AsNoTracking().Where(m => m.ProjectId == id);
        if (!string.IsNullOrEmpty(type)) q = q.Where(m => m.MilestoneType == type);
        if (disciplineId is { } d) q = q.Where(m => m.ProjectDisciplineId == d);
        if (showCompleted != true) q = q.Where(m => !m.IsComplete && !m.IsCancelled);
        var rows = await Rows(db, q, await store.Get(db));
        var statuses = Http.List(status);
        return statuses.Length == 0 ? rows : rows.Where(r => statuses.Contains(r.Status ?? "")).ToList();
    }

    /// What the register's query reads for each milestone, before the counts and the evaluated state are put beside it.
    sealed record RowSource(Guid Id, Guid ProjectId, string Key, string Name, string MilestoneType, DateOnly? Date, DateOnly? OriginalDate, DateOnly? BaselineDate, string? Description,
        Guid? ProjectDisciplineId, Guid? CompletesPhaseId, bool IsClientFacing, bool IsComplete, DateOnly? CompletedDate, bool IsCancelled, string? CancelledReason, int RowVersion,
        StateSource? State, int Deliverables, int Issued);
    sealed record StateSource(string? Status, string StatusReasons, int? DaysRemaining, int SlipDays, int DeliverableTotal, int DeliverableIssued, int TaskTotal, int TaskComplete, int TaskOpen,
        int TaskOverdue, int TaskBlocked);

    public static async Task<List<MilestoneRow>> Rows(HubDb db, IQueryable<Milestone> q, OrgSettings? schedule = null)
    {
        var list = await q.OrderBy(m => m.Date == null).ThenBy(m => m.Date).ThenBy(m => m.SortOrder).Select(m => new RowSource(
            m.Id, m.ProjectId, m.Key, m.Name, m.MilestoneType, m.Date, m.OriginalDate, m.BaselineDate, m.Description, m.ProjectDisciplineId, m.CompletesPhaseId,
            m.IsClientFacing, m.IsComplete, m.CompletedDate, m.IsCancelled, m.CancelledReason, m.RowVersion,
            db.MilestoneStates.Where(s => s.MilestoneId == m.Id).Select(s => new StateSource(s.Status, s.StatusReasons, s.DaysRemaining, s.SlipDays, s.DeliverableTotal,
                s.DeliverableIssued, s.TaskTotal, s.TaskComplete, s.TaskOpen, s.TaskOverdue, s.TaskBlocked)).FirstOrDefault(),
            db.Deliverables.Count(d => d.MilestoneId == m.Id && d.Status != DeliverableStatus.Cancelled),
            db.Deliverables.Count(d => d.MilestoneId == m.Id && (d.Status == DeliverableStatus.Issued || d.Status == DeliverableStatus.Accepted)))).ToListAsync();
        return list.Select(m => new MilestoneRow(
            m.Id, m.ProjectId, m.Key, m.Name, m.MilestoneType, m.Date, m.OriginalDate, m.Description, m.ProjectDisciplineId, m.CompletesPhaseId,
            m.IsClientFacing, m.IsComplete, m.CompletedDate, m.IsCancelled, m.CancelledReason, m.RowVersion,
            MilestoneType.IsSubmission(m.MilestoneType),
            m.IsComplete ? MilestoneStatus.Complete : m.IsCancelled ? MilestoneStatus.Cancelled : m.State?.Status,
            J.El(m.State?.StatusReasons), m.Date is { } d && m.OriginalDate is { } o ? d.DayNumber - o.DayNumber : 0,
            m.State?.DaysRemaining, m.Deliverables, m.Issued,
            m.State?.TaskTotal ?? 0, m.State?.TaskComplete ?? 0, m.State?.TaskOpen ?? 0, m.State?.TaskOverdue ?? 0, m.State?.TaskBlocked ?? 0,
            schedule?.IsFlagOn("schedule_baseline") == true ? Evaluator.SlipShown(null, m.Date, null, m.OriginalDate, schedule.SlipDisplayDays) : null,
            schedule?.IsFlagOn("schedule_baseline") == true ? m.BaselineDate : null,
            schedule?.IsFlagOn("schedule_baseline") == true ? Evaluator.SinceBaseline(m.Date, m.BaselineDate) : null,
            schedule?.IsFlagOn("schedule_baseline") == true && m.BaselineDate is not null ? Evaluator.SlipShown(null, m.Date, null, m.BaselineDate, schedule.SlipDisplayDays) : null)).ToList();
    }

    static async Task<Created<MilestoneCreated>> Create(Guid id, CreateBody body, Access access, HubDb db)
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
        return TypedResults.Created($"/api/v1/milestones/{m.Id}", new MilestoneCreated(m.Id, m.Key, m.RowVersion));
    }

    static async Task<Ok<MilestoneDetail>> Get(string id, Access access, HubDb db, SettingsStore store)
    {
        var m = (Guid.TryParse(id, out var g) ? await db.Milestones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == g)
            : await db.Milestones.AsNoTracking().FirstOrDefaultAsync(x => x.Key == id)) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(m.ProjectId, track: false);
        var settings = await store.Get(db);
        var row = (await Rows(db, db.Milestones.AsNoTracking().Where(x => x.Id == m.Id), settings)).Single();
        row = row with { CanRebaseline = settings.IsFlagOn("schedule_baseline") ? Permissions.Rebaseline(access.Actor, ctx, m.IsComplete || m.IsCancelled).Ok : null };
        var deliverables = await DeliverableEndpoints.Rows(db, db.Deliverables.AsNoTracking().Where(d => d.MilestoneId == m.Id), schedule: settings);
        var directTasks = await db.Tasks.AsNoTracking().Where(t => t.MilestoneId == m.Id).OrderBy(t => t.Seq)
            .Select(t => new MilestoneTask(t.Id, t.Key, t.Name, t.Status, t.DueDate, t.AssigneeId)).ToListAsync();
        var phase = m.CompletesPhaseId is { } ph ? await db.Phases.Where(x => x.Id == ph).Select(x => new MilestonePhase(x.Id, x.Name)).FirstOrDefaultAsync() : null;
        var can = Permissions.ManageMilestones(access.Actor, ctx);
        return TypedResults.Ok(new MilestoneDetail(row, deliverables, directTasks, phase, new MilestoneProject(p.Id, p.ProjectNumber, p.Name, p.Status),
            new MilestonePermissions(new RegisterEndpoints.RegisterPermission(can.Ok, can.Why is null ? null : Text.Get(can.Why, can.Arg ?? p.Status)), Permissions.Comment(access.Actor, ctx).Ok),
            await new RecordContextReader(db, p, settings).Read(RecordContextKind.Milestone, m.Id)));
    }

    static async Task<Ok<MilestoneVersion>> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db)
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
        if (patch.Has("projectDisciplineId"))
        {
            var pd = patch.Id("projectDisciplineId");
            if (pd is { } pdid) Check.That(await db.ProjectDisciplines.AnyAsync(x => x.Id == pdid && x.ProjectId == m.ProjectId), "projectDisciplineId", "error.not_found"); // as at creation
            m.ProjectDisciplineId = pd;
        }
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
        return TypedResults.Ok(new MilestoneVersion(m.Id, m.RowVersion));
    }

    static async Task<Ok<RebaselineRead>> Rebaseline(Guid id, RebaselineBody body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "schedule_baseline");
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.Rebaseline(access.Actor, ctx, m.IsComplete || m.IsCancelled));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        db.Audit.Reason = Check.Reason(body.Reason);
        Check.That(body.Clear == true || m.Date is not null, "date", "error.required");
        m.BaselineDate = body.Clear == true ? null : m.Date;
        await db.SaveChangesAsync();
        return TypedResults.Ok(new RebaselineRead(m.Id, m.RowVersion));
    }

    /// Both answers are a 200, so the generator keeps one: the document says either of the two.
    static async Task DescribeChangeDate(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var preview = await context.GetOrCreateSchemaAsync(typeof(DatePreview), null, cancellationToken);
        var changed = await context.GetOrCreateSchemaAsync(typeof(DateChanged), null, cancellationToken);
        operation.Responses!["200"].Content!["application/json"].Schema = new OpenApiSchema { AnyOf = [preview, changed] };
    }

    /// M-02..M-04, AC-MS-04, AC-MS-05, FR-MS-05; `includeTasks` extends the cascade to tasks (packet 018).
    // Read-only core: callers authorize the actual source first. Existing command keeps its own version/reason/commit rules below.
    internal static async Task<(DatePreview Preview, List<Deliverable> Deliverables, List<WorkTask> Tasks)> PreviewDateChange(
        HubDb db, Milestone m, DateOnly newDate, bool cascadeDeliverables, bool includeTasks)
    {
        var id = m.Id;
        var old = m.Date;
        var delta = old is { } o ? newDate.DayNumber - o.DayNumber : 0;
        var deliverables = cascadeDeliverables && delta != 0
            ? await db.Deliverables.Where(d => d.MilestoneId == id && d.DueDate != null && d.Status != DeliverableStatus.Issued
                && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled).OrderBy(d => d.Seq).ToListAsync()
            : [];
        var dIds = deliverables.Select(d => d.Id).ToList();
        var tasks = includeTasks && dIds.Count > 0
            ? await db.Tasks.Where(t => t.DeliverableId != null && dIds.Contains(t.DeliverableId.Value) && t.Status != TaskStatuses.Complete
                && t.Status != TaskStatuses.Cancelled && (t.DueDate != null || t.StartDate != null)).OrderBy(t => t.Seq).ToListAsync()
            : [];
        var preview = new DatePreview(
            new DateMove(m.Key, m.Name, old, newDate, m.OriginalDate is { } od ? newDate.DayNumber - od.DayNumber : 0, delta),
            deliverables.Select(d => new DeliverableShift(d.Id, d.Key, d.Name, d.DueDate, d.DueDate!.Value.AddDays(delta))),
            tasks.Select(t => new TaskShift(t.Id, t.Key, t.Name, t.DueDate, t.DueDate?.AddDays(delta), t.StartDate, t.StartDate?.AddDays(delta))),
            await db.Deliverables.Where(d => d.MilestoneId == id && d.DueDate > newDate && !dIds.Contains(d.Id)).Select(d => new DeliverableLeftBehind(d.Key, d.Name, d.DueDate)).ToListAsync());
        return (preview, deliverables, tasks);
    }

    static async Task<Results<Ok<DatePreview>, Ok<DateChanged>>> ChangeDate(Guid id, ChangeDateBody body, HttpContext http, Access access, HubDb db, Notifier notify, SettingsStore store)
        => await Tx.Notice<Results<Ok<DatePreview>, Ok<DateChanged>>>(db, store, async () =>
    {
        var (m, p, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        Check.That(!m.IsComplete && !m.IsCancelled, "newDate", "milestone.closed");
        var old = m.Date;
        var delta = old is { } o ? body.NewDate.DayNumber - o.DayNumber : 0;
        var (preview, deliverables, tasks) = await PreviewDateChange(db, m, body.NewDate, body.CascadeDeliverables == true, body.IncludeTasks == true);
        if (body.DryRun == true) return TypedResults.Ok(preview);
        await Http.CheckVersion(db, http, m, body.RowVersion);
        var reason = Check.Reason(body.Reason); // G-09: milestone date change needs a reason
        db.Audit.Reason = reason;
        if (old is not null && delta != 0) await DateReasons.Require(db, store, body.DateReasonCategoryId);
        m.Date = body.NewDate;
        m.OriginalDate ??= body.NewDate;
        foreach (var d in deliverables) { d.DueDate = d.DueDate!.Value.AddDays(delta); if (d.StartDate is { } s) d.StartDate = s.AddDays(delta); db.Audit.Note(d, action: "Cascade", reason: reason); }
        foreach (var t in tasks) { t.DueDate = t.DueDate?.AddDays(delta); t.StartDate = t.StartDate?.AddDays(delta); db.Audit.Note(t, action: "Cascade", reason: reason); }
        await db.SaveChangesAsync();
        var leads = await db.ProjectDisciplines.Where(x => x.ProjectId == p.Id && x.LeadUserId != null).Select(x => x.LeadUserId).ToListAsync();
        await notify.Send(NotificationEvents.MilestoneDateChanged, [p.ProjectManagerId, .. leads], Item(p, m),
            Text.Get("notify.milestone_moved", await notify.ActorName(), m.Key, m.Name, old?.ToString("yyyy-MM-dd") ?? "—", body.NewDate.ToString("yyyy-MM-dd"), (delta >= 0 ? "+" : "") + delta), reason);
        await db.SaveChangesAsync();
        return TypedResults.Ok(new DateChanged(m.Id, m.RowVersion, deliverables.Count + tasks.Count, preview));
    });

    static async Task<Ok<MilestoneCompleted>> Complete(Guid id, CompleteBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
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
            .Select(x => new OpenDeliverable(x.Id, x.Key, x.Name, x.Status)).ToListAsync();
        if (open.Count > 0 && body.Confirm != true) // M-05, AC-MS-06
            throw ApiException.Conflict("confirm_open_deliverables", "milestone.open_deliverables", new DeliverablesToConfirm(open), open.Count);
        ProjectEndpoints.CorrectionReason(p, null);
        m.IsComplete = true;
        m.CompletedDate = completed;
        if (open.Count > 0) db.Audit.Note(m, reason: Text.Get("milestone.completed_with_open", string.Join(", ", open.Select(o => o.Key))));
        await db.SaveChangesAsync();
        var phase = m.CompletesPhaseId is { } ph && p.PhaseId != ph ? await db.Phases.Where(x => x.Id == ph).Select(x => new MilestonePhase(x.Id, x.Name)).FirstOrDefaultAsync() : null;
        return TypedResults.Ok(new MilestoneCompleted(m.Id, m.RowVersion, phase)); // never advanced automatically (§9.3)
    }

    static async Task<Ok<MilestoneVersion>> Reopen(Guid id, ReasonBody body, HttpContext http, Access access, HubDb db)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        Check.That(m.IsComplete, "reason", "milestone.not_complete");
        db.Audit.Note(m, action: "Reopened", reason: Check.Reason(body.Reason)); // M-10
        m.IsComplete = false;
        m.CompletedDate = null;
        await db.SaveChangesAsync();
        return TypedResults.Ok(new MilestoneVersion(m.Id, m.RowVersion));
    }

    /// M-07, E-17: links are kept; targeted deliverables are flagged for retargeting and listed for the bulk dialog.
    static async Task<Ok<MilestoneCancelled>> Cancel(Guid id, ReasonBody body, HttpContext http, Access access, HubDb db)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        await Http.CheckVersion(db, http, m, body.RowVersion);
        m.IsCancelled = true;
        m.CancelledReason = Check.Reason(body.Reason);
        db.Audit.Note(m, reason: m.CancelledReason);
        await db.SaveChangesAsync();
        var retarget = await db.Deliverables.Where(d => d.MilestoneId == id && d.Status != DeliverableStatus.Cancelled).Select(d => new RetargetDeliverable(d.Id, d.Key, d.Name, d.RowVersion)).ToListAsync();
        return TypedResults.Ok(new MilestoneCancelled(m.Id, m.RowVersion, retarget));
    }

    static async Task<NoContent> Delete(Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me)
    {
        var (m, _, ctx) = await Load(db, access, id);
        Access.Demand(Permissions.ManageMilestones(access.Actor, ctx));
        if (await db.Deliverables.AnyAsync(d => d.MilestoneId == id) || await db.Tasks.AnyAsync(t => t.MilestoneId == id))
            throw ApiException.Rule("milestone_in_use", "milestone.in_use");
        m.DeletedAt = clock.GetUtcNow();
        m.DeletedBy = me.Id;
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }

    /// FR-RCV-03: like the task restore; the save hook logs "Restored" and queues re-evaluation.
    static async Task<Ok<MilestoneVersion>> Restore(Guid id, HttpContext http, Access access, HubDb db)
    {
        var m = await db.Milestones.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == id && x.DeletedAt != null) ?? throw ApiException.NotFound();
        var (_, ctx) = await access.Project(m.ProjectId);
        Access.Demand(Permissions.RestoreItem(access.Actor, ctx));
        await RecoveryEndpoints.Guard(db, http, m, null, null);
        m.DeletedAt = null;
        m.DeletedBy = null;
        await db.SaveChangesAsync();
        return TypedResults.Ok(new MilestoneVersion(m.Id, m.RowVersion));
    }

    public static NotifyItem Item(Project p, Milestone m) => new(p.Id, ItemType.Milestone, m.Id, m.Key, $"/projects/{p.ProjectNumber}/milestones?panel=Milestone:{m.Id}", p.ProjectNumber);
}
