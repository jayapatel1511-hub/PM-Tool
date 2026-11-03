using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S12-A only: seven bounded task operations, one transaction and one immutable terminal receipt. No Undo engine.
public static class TaskBulkCommandEndpoints
{
    public sealed class AtomicCommand { }
    public sealed record TaskBulkItem(Guid TaskId, Guid ProjectId, [property: JsonRequired] int RowVersion);
    public sealed record TaskBulkDeliverableTarget(Guid ProjectId, Guid? DeliverableId);
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(TaskBulkAssignParameters), "assign")]
    [JsonDerivedType(typeof(TaskBulkDueDateParameters), "setDueDate")]
    [JsonDerivedType(typeof(TaskBulkShiftDatesParameters), "shiftDueDates")]
    [JsonDerivedType(typeof(TaskBulkPriorityParameters), "setPriority")]
    [JsonDerivedType(typeof(TaskBulkDeliverableParameters), "setDeliverable")]
    [JsonDerivedType(typeof(TaskBulkHoldParameters), "hold")]
    [JsonDerivedType(typeof(TaskBulkCancelParameters), "cancel")]
    public abstract record TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkAssignParameters([property: JsonRequired] Guid? AssigneeId) : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkDueDateParameters([property: JsonRequired] DateOnly? DueDate) : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkShiftDatesParameters([property: JsonRequired] int Days) : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkPriorityParameters([property: JsonRequired] string Priority) : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkDeliverableParameters([property: JsonRequired] TaskBulkDeliverableTarget[] Targets) : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkHoldParameters : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkCancelParameters : TaskBulkParameters;
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkIntent(int SchemaVersion, TaskBulkItem[] Items, string Operation, TaskBulkParameters Params, string? Reason);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskBulkCommandBody(Guid RequestId, TaskBulkIntent Intent);
    public sealed record TaskBulkRow(int Index, string Outcome,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? TaskId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ProjectId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Key = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RowVersion = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid[]? ActivityIds = null);
    public sealed record TaskBulkCounts(int Changed, int Unchanged, int Refused, int Unavailable);
    public sealed record TaskBulkPreviewCounts(int Eligible, int Unchanged, int Refused, int Unavailable);
    public sealed record TaskBulkPreview(IReadOnlyList<TaskBulkRow> Rows, TaskBulkPreviewCounts Counts);
    public sealed record TaskBulkCommandResult(Guid CommandId, DateTimeOffset CompletedAt, IReadOnlyList<TaskBulkRow> Rows, TaskBulkCounts Counts, IReadOnlyList<string> Warnings);

    // Internal, bounded by 200 rows and these operation fields; no arbitrary task/comment snapshots or inverse capability.
    sealed record FieldFacts(Guid? AssigneeId, DateOnly? StartDate, DateOnly? DueDate, string Priority, Guid? DeliverableId, Guid? MilestoneId,
        string Status, string? PreviousStatus, int ProgressPct, int ReviewRound, int DueDateChangeCount, DateOnly? OriginalStartDate,
        DateOnly? OriginalDueDate, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ReviewRequestedAt);
    sealed record TerminalRow(int Index, Guid TaskId, Guid ProjectId, string? Key, string Outcome, string? Code, string? Reason,
        int? RowVersion, FieldFacts? Before, FieldFacts? After, Guid[] ActivityIds, string[] Warnings);
    sealed record ReceiptRows(TerminalRow[] Rows, Guid[] SideEffectActivityIds, Guid[] NotificationIds, Guid[] EmailIds);
    sealed record Plan(TaskBulkItem Intent, int Index, Project Project, ProjectContext Context, WorkTask Task, FieldFacts Before, FieldFacts After);
    sealed record Validation(List<Plan> Plans, List<TerminalRow> Rows);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapPost("/tasks/bulk/preview", Preview).WithMetadata(new AtomicCommand()).RequireFlag("work_screens");
        api.MapPost("/tasks/bulk/commands", Execute).WithMetadata(new AtomicCommand()).RequireFlag("work_screens");
    }

    static TaskBulkIntent Envelope(TaskBulkIntent value)
    {
        Check.That(value is not null && value.SchemaVersion == 1, "schemaVersion", "error.one_of", "1");
        Check.That(value.Items is { Length: > 0 and <= 200 }, "items", "error.one_of", "1–200");
        Check.That(value.Items.All(i => i is not null && i.TaskId != Guid.Empty && i.ProjectId != Guid.Empty && i.RowVersion >= 0)
            && value.Items.Select(i => i.TaskId).Distinct().Count() == value.Items.Length, "items", "error.validation");
        var operation = value.Params switch {
            TaskBulkAssignParameters => "assign", TaskBulkDueDateParameters => "setDueDate", TaskBulkShiftDatesParameters => "shiftDueDates",
            TaskBulkPriorityParameters => "setPriority", TaskBulkDeliverableParameters => "setDeliverable", TaskBulkHoldParameters => "hold", TaskBulkCancelParameters => "cancel", _ => "" };
        Check.That(operation.Length > 0 && operation == value.Operation, "operation", "error.one_of", "assign, setDueDate, shiftDueDates, setPriority, setDeliverable, hold, cancel");
        if (value.Params is TaskBulkPriorityParameters priority) Check.OneOf(priority.Priority, Priority.All, "priority");
        if (value.Params is TaskBulkDeliverableParameters targets)
        {
            var projects = value.Items.Select(i => i.ProjectId).ToHashSet();
            Check.That(targets.Targets is not null && targets.Targets.Length == projects.Count && targets.Targets.All(t => t is not null)
                && targets.Targets.Select(t => t.ProjectId).Distinct().Count() == projects.Count && targets.Targets.All(t => projects.Contains(t.ProjectId)), "targets", "error.validation");
            value = value with { Params = targets with { Targets = targets.Targets.OrderBy(t => t.ProjectId).ToArray() } };
        }
        return value with { Reason = string.IsNullOrWhiteSpace(value.Reason) ? null : value.Reason.Trim() };
    }

    static async Task<Ok<TaskBulkPreview>> Preview(TaskBulkIntent body, Access access, HubDb db, SettingsStore store)
    {
        var s = await store.Get(db); Http.RequireFlag(s, "work_screens");
        var intent = Envelope(body);
        var validation = await Validate(intent, access, db, s);
        var rows = await ProjectRows(validation.Rows.Concat(validation.Plans.Select(p => Row(p, "eligible", p.Intent.RowVersion))), access, db);
        return TypedResults.Ok(new TaskBulkPreview(rows, new(rows.Count(r => r.Outcome == "eligible"), rows.Count(r => r.Outcome == "unchanged"), rows.Count(r => r.Outcome == "refused"), rows.Count(r => r.Outcome == "unavailable"))));
    }

    static async Task<Ok<TaskBulkCommandResult>> Execute(TaskBulkCommandBody body, Access access, HubDb db, SettingsStore store, Notifier notify, TeamService team, TimeProvider clock)
    {
        var s = await store.Get(db); Http.RequireFlag(s, "work_screens");
        Check.That(body.RequestId != Guid.Empty, "requestId", "error.required");
        var intent = Envelope(body.Intent);
        var json = JsonSerializer.Serialize(intent, JsonOpts.Web);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        var initialCorrelation = db.Audit.CorrelationId; var initialReason = db.Audit.Reason;
        try { return await Tx.Run(db, async () => {
            var identity = $"task-bulk:{access.Me.Id:D}:{body.RequestId:D}";
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({identity}, 0))");
            var previous = await db.TaskBulkCommandReceipts.AsNoTracking().SingleOrDefaultAsync(r => r.ActorId == access.Me.Id && r.RequestId == body.RequestId);
            if (previous is not null)
            {
                if (previous.HashVersion != 1 || previous.IntentHash != hash) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                var stored = JsonSerializer.Deserialize<ReceiptRows>(previous.ResponseJson, JsonOpts.Web) ?? throw new InvalidOperationException("Invalid task bulk receipt.");
                return TypedResults.Ok(await Result(previous, stored, access, db));
            }
            // Lock before the read-only validation phase; all projects first, then tasks in deterministic order.
            foreach (var project in intent.Items.Select(i => i.ProjectId).Distinct().Order()) await Coordination.Lock(db, project);
            foreach (var task in intent.Items.Select(i => i.TaskId).Order())
                await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.task WHERE id = {0} FOR UPDATE", task).ToListAsync();
            var validation = await Validate(intent, access, db, s);
            var receipt = new TaskBulkCommandReceipt { ActorId = access.Me.Id, RequestId = body.RequestId, HashVersion = 1, IntentHash = hash,
                IntentJson = json, CreatedAt = clock.GetUtcNow() };
            db.Audit.CorrelationId = receipt.Id;
            using var capture = notify.BeginTaskBulkCapture(receipt.Id, access.Me.Id);
            var tasks = new Dictionary<Guid, WorkTask>();
            foreach (var plan in validation.Plans)
            {
                var task = await db.Tasks.SingleAsync(t => t.Id == plan.Task.Id);
                // A new race/provider failure here aborts this entire transaction, never a task-only tracker reload.
                await Http.CheckBodyVersion(db, task, plan.Intent.RowVersion);
                db.Audit.Reason = intent.Reason;
                switch (intent.Params)
                {
                    case TaskBulkAssignParameters assignment:
                        await TaskEndpoints.AssignTo(db, plan.Project, task, assignment.AssigneeId, team, s);
                        await notify.Send(NotificationEvents.TaskAssigned, assignment.AssigneeId, TaskEndpoints.Item(plan.Project, task),
                            Text.Get("notify.task_assigned", await notify.ActorName(), task.Key, task.Name));
                        break;
                    case TaskBulkDueDateParameters:
                    case TaskBulkShiftDatesParameters:
                        if (task.DueDate != plan.After.DueDate) { if (task.DueDate is not null) task.DueDateChangeCount++; task.DueDate = plan.After.DueDate; task.OriginalDueDate ??= task.DueDate; }
                        task.StartDate = plan.After.StartDate; task.OriginalStartDate ??= task.StartDate;
                        await notify.Send(NotificationEvents.DueDateChanged, task.AssigneeId, TaskEndpoints.Item(plan.Project, task),
                            Text.Get("notify.bulk_due", await notify.ActorName(), 1, plan.Project.ProjectNumber), intent.Reason);
                        break;
                    case TaskBulkPriorityParameters priority: task.Priority = priority.Priority; break;
                    case TaskBulkDeliverableParameters: task.DeliverableId = plan.After.DeliverableId; task.MilestoneId = plan.After.MilestoneId; break;
                    case TaskBulkHoldParameters:
                    case TaskBulkCancelParameters:
                        await TaskEndpoints.ApplyTransition(db, access, plan.Context, plan.Project, task, plan.After.Status, intent.Reason, null, null, s, clock, team, [], store: store); break;
                }
                if (intent.Params is not (TaskBulkHoldParameters or TaskBulkCancelParameters)) task.OriginalStartDate ??= task.StartDate;
                task.LastActivityAt = clock.GetUtcNow(); tasks.Add(task.Id, task);
            }
            await db.SaveChangesAsync();
            var logs = db.ActivityLog.Local.Where(a => a.CorrelationId == receipt.Id).ToArray();
            foreach (var plan in validation.Plans)
            {
                var task = tasks[plan.Task.Id];
                var activity = logs.Where(a => a.ItemType == ItemType.Task && a.ItemId == task.Id).Select(a => a.Id).ToArray();
                if (activity.Length == 0) throw new InvalidOperationException("A changed task must have an activity entry.");
                validation.Rows.Add(Row(plan, "changed", task.RowVersion) with { After = Facts(task), ActivityIds = activity });
            }
            var summaries = await notify.StageTaskBulkSummaries(capture, tasks.Keys.ToArray());
            var payload = new ReceiptRows(validation.Rows.OrderBy(r => r.Index).ToArray(), logs.Where(a => a.ItemType != ItemType.Task).Select(a => a.Id).ToArray(), summaries.NotificationIds, summaries.EmailIds);
            receipt.ResponseJson = JsonSerializer.Serialize(payload, JsonOpts.Web); receipt.CompletedAt = clock.GetUtcNow();
            db.TaskBulkCommandReceipts.Add(receipt); await db.SaveChangesAsync();
            return TypedResults.Ok(await Result(receipt, payload, access, db));
        }); }
        finally { db.Audit.CorrelationId = initialCorrelation; db.Audit.Reason = initialReason; }
    }

    static FieldFacts Facts(WorkTask t) => new(t.AssigneeId, t.StartDate, t.DueDate, t.Priority, t.DeliverableId, t.MilestoneId, t.Status,
        t.PreviousStatus, t.ProgressPct, t.ReviewRound, t.DueDateChangeCount, t.OriginalStartDate, t.OriginalDueDate, t.StartedAt, t.CompletedAt, t.ReviewRequestedAt);
    static TerminalRow Row(Plan p, string outcome, int version) => new(p.Index, p.Intent.TaskId, p.Intent.ProjectId, p.Task.Key, outcome, null, null, version, p.Before, p.After, [], []);

    static async Task<Validation> Validate(TaskBulkIntent intent, Access access, HubDb db, OrgSettings s)
    {
        var visible = await access.VisibleProjects().Where(p => intent.Items.Select(i => i.ProjectId).Contains(p.Id)).AsNoTracking().ToDictionaryAsync(p => p.Id);
        var contexts = new Dictionary<Guid, ProjectContext>();
        foreach (var p in visible.Values) contexts.Add(p.Id, await access.Context(p));
        var selected = intent.Items.Select(i => i.TaskId).ToArray();
        var found = await db.Tasks.AsNoTracking().Where(t => selected.Contains(t.Id)).ToDictionaryAsync(t => t.Id);
        var plans = new List<Plan>(); var rows = new List<TerminalRow>();
        bool? allowAuthorshipSelfReview = null;
        for (var index = 0; index < intent.Items.Length; index++)
        {
            var input = intent.Items[index];
            if (!visible.TryGetValue(input.ProjectId, out var p) || !found.TryGetValue(input.TaskId, out var task) || task.ProjectId != input.ProjectId)
            { rows.Add(new(index, input.TaskId, input.ProjectId, null, "unavailable", null, null, null, null, null, [], [])); continue; }
            try
            {
                var ctx = contexts[p.Id]; var facts = await TaskEndpoints.Facts(db, task);
                var allow = intent.Operation switch {
                    "assign" => Permissions.AssignTask(access.Actor, ctx, facts), "setDueDate" or "shiftDueDates" => Permissions.ChangeDueDate(access.Actor, ctx, facts),
                    "setPriority" or "setDeliverable" => Permissions.EditTask(access.Actor, ctx, facts),
                    _ => Permissions.TaskTransition(access.Actor, ctx, facts, task.Status, intent.Operation == "hold" ? TaskStatuses.OnHold : TaskStatuses.Cancelled) };
                if (!allow) throw ApiException.Forbidden(allow.Why!, allow.Arg ?? await TaskEndpoints.DisciplineName(db, task.ProjectDisciplineId));
                await Http.CheckBodyVersion(db, task, input.RowVersion);
                ProjectEndpoints.CorrectionReason(p, intent.Reason);
                var before = Facts(task); var after = before;
                switch (intent.Params)
                {
                    case TaskBulkAssignParameters assignment:
                        if (assignment.AssigneeId is { } person)
                        {
                            await DeliverableEndpoints.ActivePerson(db, person, "assigneeId");
                            Check.That(s.AllowSelfReview || person != task.ReviewerId, "assigneeId", "task.self_review");
                            var member = await db.ProjectMembers.AsNoTracking().AnyAsync(m => m.ProjectId == p.Id && m.UserId == person && m.RemovedAt == null);
                            if (!member && p.Visibility == Visibility.Restricted) Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
                        }
                        after = before with { AssigneeId = assignment.AssigneeId }; break;
                    case TaskBulkDueDateParameters date:
                        if (date.DueDate != task.DueDate && Permissions.DueChangeNeedsReason(access.Actor, ctx, facts)) Check.Reason(intent.Reason);
                        after = before with { DueDate = date.DueDate }; break;
                    case TaskBulkShiftDatesParameters shift:
                        Check.Reason(intent.Reason);
                        if (shift.Days != 0 && task.StartDate is not null) Access.Demand(Permissions.EditTask(access.Actor, ctx, facts));
                        try { after = before with { DueDate = task.DueDate?.AddDays(shift.Days), StartDate = task.StartDate?.AddDays(shift.Days) }; }
                        catch (ArgumentOutOfRangeException) { throw ApiException.Invalid("days", "deliverable.start_after_due"); }
                        break;
                    case TaskBulkPriorityParameters priority: after = before with { Priority = priority.Priority }; break;
                    case TaskBulkDeliverableParameters deliverables:
                        var target = deliverables.Targets.Single(t => t.ProjectId == p.Id).DeliverableId;
                        if (target is { } did) Check.That(await db.Deliverables.AnyAsync(d => d.Id == did && d.ProjectId == p.Id), "deliverableId", "task.other_project");
                        after = before with { DeliverableId = target, MilestoneId = target is null ? task.MilestoneId : null }; break;
                    case TaskBulkHoldParameters:
                    case TaskBulkCancelParameters:
                        Check.Reason(intent.Reason);
                        var to = intent.Operation == "hold" ? TaskStatuses.OnHold : TaskStatuses.Cancelled;
                        if (task.Status != to)
                        {
                            var path = Workflow.TaskPath(task.Status, to, task.RequiresReview, task.PreviousStatus);
                            if (path is null) throw ApiException.Rule("illegal_transition", "task.illegal_transition", null, task.Status, to);
                            var current = task.Status;
                            foreach (var step in path) { Access.Demand(Permissions.TaskTransition(access.Actor, ctx, facts, current, step)); current = step; }
                        }
                        after = before with { Status = to }; break;
                }
                // The savehook checks prospective assignee + original creator when authorship actually changes.
                // Reuse its actual-setting/current-round query before staging, so one known conflict cannot abort valid rows.
                if (before.AssigneeId != after.AssigneeId || before.DeliverableId != after.DeliverableId)
                {
                    allowAuthorshipSelfReview ??= await db.AllowSelfReviewForAuthorship();
                    await db.DemandReviewAuthorship(after.DeliverableId, new[] { after.AssigneeId, task.CreatedBy }.OfType<Guid>().ToArray(), allowAuthorshipSelfReview.Value);
                }
                Check.That(after.StartDate is null || after.DueDate is null || after.StartDate <= after.DueDate, "dueDate", "deliverable.start_after_due");
                var plan = new Plan(input, index, p, ctx, task, before, after);
                if (before == after) rows.Add(Row(plan, "unchanged", task.RowVersion)); else plans.Add(plan);
            }
            catch (ApiException error)
            {
                var reason = error.Errors?.Values.SelectMany(v => v).FirstOrDefault() ?? error.Message;
                rows.Add(new(index, input.TaskId, input.ProjectId, task.Key, "refused", error.Code, reason, task.RowVersion, null, null, [], []));
            }
        }
        return new(plans, rows);
    }

    static async Task<TaskBulkRow[]> ProjectRows(IEnumerable<TerminalRow> stored, Access access, HubDb db)
    {
        var rows = stored.OrderBy(r => r.Index).ToArray(); var ids = rows.Select(r => r.TaskId).ToArray();
        var projects = access.VisibleProjectIds();
        var visible = await db.Tasks.AsNoTracking().Where(t => ids.Contains(t.Id) && projects.Contains(t.ProjectId)).Select(t => new { t.Id, t.ProjectId }).ToDictionaryAsync(t => t.Id);
        return rows.Select(r => r.Outcome == "unavailable" || !visible.TryGetValue(r.TaskId, out var task) || task.ProjectId != r.ProjectId
            ? new TaskBulkRow(r.Index, "unavailable") : new TaskBulkRow(r.Index, r.Outcome, r.TaskId, r.ProjectId, r.Key, r.Code, r.Reason, r.RowVersion,
                r.ActivityIds.Length == 0 ? null : r.ActivityIds)).ToArray();
    }
    static async Task<TaskBulkCommandResult> Result(TaskBulkCommandReceipt receipt, ReceiptRows stored, Access access, HubDb db)
    {
        var rows = await ProjectRows(stored.Rows, access, db);
        var visibleIndexes = rows.Where(r => r.Outcome != "unavailable").Select(r => r.Index).ToHashSet();
        return new(receipt.Id, receipt.CompletedAt, rows, new(rows.Count(r => r.Outcome == "changed"), rows.Count(r => r.Outcome == "unchanged"), rows.Count(r => r.Outcome == "refused"), rows.Count(r => r.Outcome == "unavailable")),
            stored.Rows.Where(r => visibleIndexes.Contains(r.Index)).SelectMany(r => r.Warnings).ToArray());
    }
}
