using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Packet 025. A separate handoff is one receiver's receipt for one purpose, never a task completion.
public static class HandoffEndpoints
{
    public sealed record AtomicCommand;
    public sealed record DraftBody(Guid RequestId, string Title, Guid SourceDeliverableId, int SourceRowVersion,
        string DeclaredRevision, string SourceUrl, Guid ReceivingDisciplineId, Guid SendingOwnerId, Guid ReceivingOwnerId,
        Guid? TargetTaskId, Guid? TargetDeliverableId, string IntendedUse, string AcceptanceCriteria,
        DateOnly NeededBy, DateOnly? PromisedBy, int? RowVersion, string? Reason);
    public sealed record MoveBody(Guid RequestId, string ToStatus, int? RowVersion, string? Reason, string? CriteriaOutcome);
    public sealed record AssignBody(Guid RequestId, Guid SendingOwnerId, Guid ReceivingOwnerId, int? RowVersion, string Reason);
    public sealed record Filter(string? Q, string? Status, string? Direction, Guid? DisciplineId, bool? Overdue);
    public sealed record CommandResult(Guid Id, int RowVersion);

    /// One handoff as the register and the detail panel read it: the stored receipt with the names, keys and flags worked out beside it.
    public sealed record HandoffRow(Guid Id, string Key, string Title, string Status, int RowVersion, Guid SourceDeliverableId, int SourceRowVersion, string DeclaredRevision, string SourceUrl,
        Guid SendingDisciplineId, Guid ReceivingDisciplineId, Guid SendingOwnerId, Guid ReceivingOwnerId, Guid? TargetTaskId, Guid? TargetDeliverableId, string IntendedUse, string AcceptanceCriteria,
        DateOnly NeededBy, DateOnly? PromisedBy, Guid? CurrentRevisionId, Guid? IncorporatedRevisionId, string? SenderName, string? ReceiverName, string? SourceKey, string? TargetKey,
        bool OwnersAvailable, bool SourceChanged, bool DateMismatch, bool IsOverdue);

    /// What the create form chooses from: the live deliverables, tasks and people of the project, and whether this actor may start a handoff.
    public sealed record HandoffOptions(IReadOnlyList<HandoffSource> Sources, IReadOnlyList<HandoffDiscipline> Disciplines, IReadOnlyList<HandoffPerson> People,
        IReadOnlyList<HandoffTask> Tasks, bool CanCreate);
    public sealed record HandoffSource(Guid Id, string Key, string Name, Guid ProjectDisciplineId, Guid? OwnerId, string? Revision, string? TransmittalUrl, int RowVersion, DateOnly? DueDate);
    public sealed record HandoffDiscipline(Guid Id, string Name);
    public sealed record HandoffPerson(Guid Id, string DisplayName);
    public sealed record HandoffTask(Guid Id, string Key, string Name, Guid ProjectDisciplineId, Guid? OwnerId, DateOnly? DueDate);

    /// One handoff with what this actor may do to it, the change assessments about its source, its receipt history and each submission it kept.
    public sealed record HandoffDetail(HandoffRow Row, HandoffDetailPermissions Permissions, IReadOnlyList<HandoffChangeAssessment> ChangeAssessments,
        IReadOnlyList<HandoffHistoryRow> History, IReadOnlyList<HandoffRevisionRow> Revisions);
    public sealed record HandoffPermission(bool Ok, string? Reason);
    public sealed record HandoffTransition(string To, HandoffPermission Permission);
    public sealed record HandoffDetailPermissions(HandoffPermission Edit, HandoffPermission Assign, IReadOnlyList<HandoffTransition> Transitions);
    public sealed record HandoffChangeAssessment(Guid Id, string Status, Guid ChangeNoticeId, string Key, string Title, string NoticeStatus);
    public sealed record HandoffHistoryRow(Guid Id, string FromStatus, string ToStatus, Guid? RevisionId, string? Reason, string? CriteriaOutcome, DateTimeOffset CreatedAt, Guid? CreatedBy, string? Actor);
    /// The snapshot a submission froze: the purpose, criteria and dates as sent, and the source revision it named.
    public sealed record HandoffRevisionRow(Guid Id, Guid? PreviousRevisionId, string IntendedUse, string AcceptanceCriteria, DateOnly NeededBy, DateOnly PromisedBy, Guid SendingOwnerId,
        Guid ReceivingOwnerId, Guid? TargetTaskId, Guid? TargetDeliverableId, string? Response, DateTimeOffset CreatedAt, Guid? CreatedBy, HandoffRevisionSource Source);
    public sealed record HandoffRevisionSource(Guid Id, string SourceKey, string Title, string Revision, string Url, int SourceRowVersion, DateTimeOffset CreatedAt, Guid? CreatedBy);

    sealed record RowFacts(Handoff H, string? SenderName, string? ReceiverName, string? SourceKey, int? CurrentSourceVersion, string? TargetKey, bool OwnersAvailable);

    static readonly Col[] Columns = Hub.Api.Records.HandoffRecord.HandoffsColumns;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/handoffs", List);
        api.MapGet("/projects/{projectId:guid}/handoffs/options", Options);
        api.MapGet("/projects/{projectId:guid}/handoffs/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/handoffs/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/handoffs", Create).WithMetadata(new AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/handoffs/{id:guid}/draft", Edit).WithMetadata(new AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/handoffs/{id:guid}/transition", Move).WithMetadata(new AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/handoffs/{id:guid}/assign", Assign).WithMetadata(new AtomicCommand());
    }

    static async Task<Handoff> Load(HubDb db, Guid projectId, Guid id) =>
        await db.Handoffs.FirstOrDefaultAsync(h => h.ProjectId == projectId && h.Id == id) ?? throw ApiException.NotFound();
    static async Task<HandoffFacts> Facts(HubDb db, Handoff h) => new(h.SendingDisciplineId, h.ReceivingDisciplineId,
        h.SendingOwnerId, h.ReceivingOwnerId, h.Status,
        await db.HandoffRevisions.AnyAsync(r => r.HandoffId == h.Id && r.CreatedBy == h.ReceivingOwnerId) ? h.ReceivingOwnerId : null);

    // One project-row lock serialises handoff writes and duplicate commands. Audit, immutable evidence,
    // notification queue and the command receipt commit together. Read access is checked before locking.
    static async Task<CommandResult> Command(Guid projectId, Guid requestId, object payload, Access access, HubDb db, TimeProvider clock,
        Func<Project, ProjectContext, Task<Handoff>> work)
    {
        Check.That(requestId != Guid.Empty, "requestId", "error.required");
        await access.Project(projectId, track: false);
        return await Tx.Run(db, async () =>
        {
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.project WHERE id = {0} FOR UPDATE", projectId).ToListAsync();
            var (p, ctx) = await access.Project(projectId);
            Access.Demand(Permissions.Writable(access.Actor, ctx));
            var hash = Coordination.Hash(payload);
            var prior = await db.HandoffCommands.AsNoTracking().FirstOrDefaultAsync(c => c.ProjectId == projectId && c.ActorId == access.Me.Id && c.RequestId == requestId);
            if (prior is not null)
            {
                if (prior.PayloadHash != hash) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                return new CommandResult(prior.HandoffId, prior.ResultVersion);
            }
            var h = await work(p, ctx);
            await db.SaveChangesAsync();
            db.HandoffCommands.Add(new HandoffCommand { ProjectId = projectId, ActorId = access.Me.Id, RequestId = requestId,
                PayloadHash = hash, HandoffId = h.Id, ResultVersion = h.RowVersion, CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return new CommandResult(h.Id, h.RowVersion);
        });
    }

    static async Task Owners(HubDb db, Project p, Guid sender, Guid receiver)
    {
        Check.That(await Coordination.People(db, p).AnyAsync(u => u.Id == sender), "sendingOwnerId", "handoff.active_member");
        Check.That(await Coordination.People(db, p).AnyAsync(u => u.Id == receiver), "receivingOwnerId", "handoff.active_member");
    }

    static async Task<Deliverable> Source(HubDb db, Guid projectId, Guid id, int version)
    {
        // The share lock keeps a source edit from racing the snapshot in this transaction.
        await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.deliverable WHERE id = {0} AND project_id = {1} FOR SHARE", id, projectId).ToListAsync();
        var source = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.ProjectId == projectId) ?? throw ApiException.NotFound();
        if (source.RowVersion != version) throw ApiException.Rule("source_changed", "handoff.source_changed");
        Check.That(source.Status != DeliverableStatus.Cancelled, "sourceDeliverableId", "handoff.reference_unavailable");
        return source;
    }

    static async Task Targets(HubDb db, Handoff h)
    {
        Check.That((h.TargetTaskId is null) != (h.TargetDeliverableId is null), "targetTaskId", "handoff.one_target");
        if (h.TargetTaskId is { } lockTask)
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.task WHERE id = {0} AND project_id = {1} FOR SHARE", lockTask, h.ProjectId).ToListAsync();
        if (h.TargetDeliverableId is { } lockDeliverable)
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.deliverable WHERE id = {0} AND project_id = {1} FOR SHARE", lockDeliverable, h.ProjectId).ToListAsync();
        if (h.TargetTaskId is { } task)
            Check.That(await db.Tasks.AnyAsync(t => t.Id == task && t.ProjectId == h.ProjectId && t.ProjectDisciplineId == h.ReceivingDisciplineId && t.Status != TaskStatuses.Cancelled), "targetTaskId", "handoff.reference_unavailable");
        if (h.TargetDeliverableId is { } del)
            Check.That(await db.Deliverables.AnyAsync(d => d.Id == del && d.ProjectId == h.ProjectId && d.ProjectDisciplineId == h.ReceivingDisciplineId && d.Status != DeliverableStatus.Cancelled), "targetDeliverableId", "handoff.reference_unavailable");
        Check.That(await db.ProjectDisciplines.AnyAsync(d => d.Id == h.SendingDisciplineId && d.ProjectId == h.ProjectId && d.IsActive)
            && await db.ProjectDisciplines.AnyAsync(d => d.Id == h.ReceivingDisciplineId && d.ProjectId == h.ProjectId && d.IsActive), "receivingDisciplineId", "handoff.reference_unavailable");
    }

    static async Task Fill(HubDb db, Project p, Handoff h, DraftBody b, Deliverable source)
    {
        h.Title = Check.Required(b.Title, "title", 200);
        h.DeclaredRevision = Check.Required(b.DeclaredRevision, "declaredRevision", 100);
        h.SourceUrl = Check.Required(b.SourceUrl, "sourceUrl", 2000);
        Check.That(Uri.TryCreate(h.SourceUrl, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http", "sourceUrl", "error.url");
        h.IntendedUse = Check.Required(b.IntendedUse, "intendedUse", 2000);
        h.AcceptanceCriteria = Check.Required(b.AcceptanceCriteria, "acceptanceCriteria", 4000);
        Check.That(b.NeededBy != default, "neededBy", "error.required");
        (h.SourceDeliverableId, h.SourceRowVersion, h.SendingDisciplineId, h.ReceivingDisciplineId) = (source.Id, source.RowVersion, source.ProjectDisciplineId, b.ReceivingDisciplineId);
        (h.SendingOwnerId, h.ReceivingOwnerId, h.TargetTaskId, h.TargetDeliverableId) = (b.SendingOwnerId, b.ReceivingOwnerId, b.TargetTaskId, b.TargetDeliverableId);
        (h.NeededBy, h.PromisedBy) = (b.NeededBy, b.PromisedBy);
        await Owners(db, p, h.SendingOwnerId, h.ReceivingOwnerId);
        await Targets(db, h);
    }

    static async Task<Created<CommandResult>> Create(Guid projectId, DraftBody body, Access access, HubDb db, Notifier notify, TimeProvider clock)
    {
        var result = await Command(projectId, body.RequestId, new { operation = "create", body }, access, db, clock, async (p, ctx) =>
        {
            var source = await Source(db, p.Id, body.SourceDeliverableId, body.SourceRowVersion);
            Access.Demand(Permissions.CreateHandoff(access.Actor, ctx, source.ProjectDisciplineId, body.ReceivingDisciplineId, source.OwnerId));
            var h = new Handoff { ProjectId = p.Id };
            await Fill(db, p, h, body, source);
            (h.Seq, h.Key) = await Keys.Next(db, p.Id, p.ProjectNumber, "handoff");
            db.Handoffs.Add(h);
            await SubmissionEndpoints.InvalidateForHandoff(db, p.Id, h.TargetTaskId, h.TargetDeliverableId, notify);
            return h;
        });
        return TypedResults.Created($"/api/v1/projects/{projectId}/handoffs/{result.Id}", result);
    }

    static async Task<CommandResult> Edit(Guid projectId, Guid id, DraftBody body, HttpContext http, Access access, HubDb db, SettingsStore store, Notifier notify, TimeProvider clock)
    {
        body = body with { RowVersion = Http.IfMatch(http, body.RowVersion) };
        return await Command(projectId, body.RequestId, new { operation = "draft", id, body }, access, db, clock, async (p, ctx) =>
        {
            var h = await Load(db, p.Id, id);
            var facts = await Facts(db, h);
            Access.Demand(Permissions.EditHandoff(access.Actor, ctx, facts));
            await Http.CheckVersion(db, http, h, body.RowVersion);
            Check.That(h.SourceDeliverableId == body.SourceDeliverableId, "sourceDeliverableId", "handoff.fixed_source");
            if (h.SendingOwnerId != body.SendingOwnerId || h.ReceivingOwnerId != body.ReceivingOwnerId)
            {
                Access.Demand(Permissions.AssignHandoff(access.Actor, ctx, facts)); Check.Reason(body.Reason);
                await IndependentAssignment(db, h, body.SendingOwnerId, body.ReceivingOwnerId, (await store.Get(db)).AllowSelfReview);
            }
            if (h.CurrentRevisionId is not null)
                Check.That(h.IntendedUse == body.IntendedUse?.Trim() && h.AcceptanceCriteria == body.AcceptanceCriteria?.Trim()
                    && h.TargetTaskId == body.TargetTaskId && h.TargetDeliverableId == body.TargetDeliverableId && h.ReceivingDisciplineId == body.ReceivingDisciplineId,
                    "intendedUse", "handoff.fixed_purpose");
            var source = await Source(db, p.Id, h.SourceDeliverableId, body.SourceRowVersion);
            if (h.SendingDisciplineId != source.ProjectDisciplineId || h.ReceivingDisciplineId != body.ReceivingDisciplineId)
                Access.Demand(Permissions.CreateHandoff(access.Actor, ctx, source.ProjectDisciplineId, body.ReceivingDisciplineId, source.OwnerId));
            var oldTargetTaskId = h.TargetTaskId;
            var oldTargetDeliverableId = h.TargetDeliverableId;
            await Fill(db, p, h, body, source);
            if (db.Entry(h).Properties.Any(property => !Equals(property.CurrentValue, property.OriginalValue))) {
                if (oldTargetTaskId != h.TargetTaskId || oldTargetDeliverableId != h.TargetDeliverableId)
                    await SubmissionEndpoints.InvalidateForHandoff(db, p.Id, oldTargetTaskId, oldTargetDeliverableId, notify);
                await SubmissionEndpoints.InvalidateForHandoff(db, p.Id, h.TargetTaskId, h.TargetDeliverableId, notify);
                db.Audit.Note(h, reason: body.Reason);
            }
            ProjectEndpoints.CorrectionReason(p, body.Reason);
            return h;
        });
    }

    static async Task<CommandResult> Move(Guid projectId, Guid id, MoveBody body, HttpContext http, Access access, HubDb db,
        SettingsStore store, Notifier notify, TimeProvider clock)
    {
        body = body with { RowVersion = Http.IfMatch(http, body.RowVersion) };
        return await Command(projectId, body.RequestId, new { operation = "transition", id, body }, access, db, clock, async (p, ctx) =>
        {
            var h = await Load(db, p.Id, id);
            await Http.CheckVersion(db, http, h, body.RowVersion);
            var facts = await Facts(db, h);
            Access.Demand(Permissions.HandoffTransition(access.Actor, ctx, facts, body.ToStatus, (await store.Get(db)).AllowSelfReview));
            var reason = HandoffRules.NeedsReason(h.Status, body.ToStatus) ? Check.Reason(body.Reason) : Check.Optional(body.Reason, "reason", 4000);
            ProjectEndpoints.CorrectionReason(p, reason);
            string? outcome = null;
            if (body.ToStatus != HandoffStatus.Cancelled)
            {
                await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.deliverable WHERE id = {0} AND project_id = {1} FOR SHARE", h.SourceDeliverableId, p.Id).ToListAsync();
                await Owners(db, p, h.SendingOwnerId, h.ReceivingOwnerId);
                await Targets(db, h);
                Check.That(await db.Deliverables.AnyAsync(d => d.Id == h.SourceDeliverableId && d.ProjectId == p.Id && d.Status != DeliverableStatus.Cancelled), "sourceDeliverableId", "handoff.reference_unavailable");
            }
            if (body.ToStatus == HandoffStatus.Submitted)
            {
                Check.That(h.PromisedBy is not null, "promisedBy", "handoff.promised_required");
                var source = await Source(db, p.Id, h.SourceDeliverableId, h.SourceRowVersion);
                var registered = await Coordination.Snapshot(db, p, source, h.DeclaredRevision, h.SourceUrl);
                var revision = new HandoffRevision { ProjectId = p.Id, HandoffId = h.Id, SourceRevisionId = registered.Id,
                    PreviousRevisionId = h.CurrentRevisionId, SendingOwnerId = h.SendingOwnerId, ReceivingOwnerId = h.ReceivingOwnerId,
                    IntendedUse = h.IntendedUse, AcceptanceCriteria = h.AcceptanceCriteria, NeededBy = h.NeededBy, PromisedBy = h.PromisedBy!.Value,
                    TargetTaskId = h.TargetTaskId, TargetDeliverableId = h.TargetDeliverableId, Response = reason };
                db.HandoffRevisions.Add(revision);
                await db.SaveChangesAsync();
                h.CurrentRevisionId = revision.Id;
            }
            if (body.ToStatus is HandoffStatus.Accepted or HandoffStatus.Incorporated)
                Check.That(h.CurrentRevisionId is not null, "currentRevisionId", "handoff.reference_unavailable");
            if (body.ToStatus == HandoffStatus.Accepted) outcome = Check.Required(body.CriteriaOutcome, "criteriaOutcome", 4000);
            if (body.ToStatus == HandoffStatus.Incorporated)
            {
                outcome = Check.Required(body.CriteriaOutcome, "criteriaOutcome", 4000);
                var receipt = await db.HandoffRevisions.SingleAsync(r => r.Id == h.CurrentRevisionId);
                var sourceRevision = await Coordination.Revision(db, p.Id, receipt.SourceRevisionId);
                var head = await Coordination.Head(db, sourceRevision);
                var unpublishedCorrection = head != null && !await Coordination.Published(db, sourceRevision)
                    && (sourceRevision.SupersedesId == head.CurrentRevisionId
                        || receipt.PreviousRevisionId != null && await db.HandoffRevisions.AnyAsync(r => r.Id == receipt.PreviousRevisionId && r.SourceRevisionId == head.CurrentRevisionId));
                if (head != null && head.CurrentRevisionId != sourceRevision.Id && !unpublishedCorrection)
                    Check.That(await db.ChangeAssessments.AnyAsync(a => a.ProjectId == p.Id && a.TargetId == (h.TargetTaskId ?? h.TargetDeliverableId) && a.RevisionUsedId == sourceRevision.Id && a.RetentionApprovedBy != null && a.RetainOldRevision && db.ChangeNotices.Any(c => c.Id == a.ChangeNoticeId && c.NewRevisionId == head.CurrentRevisionId)), "revision", "change.retention");
                var target = await Coordination.Target(db, p, h.TargetTaskId != null ? "Task" : "Deliverable", h.TargetTaskId ?? h.TargetDeliverableId!.Value);
                Check.That(target.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "targetId", "review.issued");
                await Coordination.Adopt(db, p, target, sourceRevision, h.IntendedUse, outcome, access.Me.Id, clock.GetUtcNow());
                h.IncorporatedRevisionId = h.CurrentRevisionId;
            }
            db.HandoffReceiptEvents.Add(new HandoffReceiptEvent { ProjectId = p.Id, HandoffId = h.Id, RevisionId = h.CurrentRevisionId,
                FromStatus = h.Status, ToStatus = body.ToStatus, Reason = reason, CriteriaOutcome = outcome });
            h.Status = body.ToStatus;
            await SubmissionEndpoints.InvalidateForHandoff(db, p.Id, h.TargetTaskId, h.TargetDeliverableId, notify);
            db.Audit.Note(h, reason: reason);
            await notify.Send(NotificationEvents.HandoffChanged, new Guid?[] { h.SendingOwnerId, h.ReceivingOwnerId },
                new NotifyItem(p.Id, "Handoff", h.Id, h.Key, $"/projects/{p.ProjectNumber}/handoffs?panel=Handoff:{h.Id}", p.ProjectNumber),
                Text.Get("handoff.notification", h.Key, h.Title, h.Status));
            return h;
        });
    }

    static async Task IndependentAssignment(HubDb db, Handoff h, Guid sender, Guid receiver, bool allowSelfReview)
    {
        Check.That(allowSelfReview || (sender != receiver && !await db.HandoffRevisions.AnyAsync(r => r.HandoffId == h.Id && r.CreatedBy == receiver)),
            "receivingOwnerId", "handoff.self_receipt");
    }

    static async Task<CommandResult> Assign(Guid projectId, Guid id, AssignBody body, HttpContext http, Access access, HubDb db, SettingsStore store, Notifier notify, TimeProvider clock)
    {
        body = body with { RowVersion = Http.IfMatch(http, body.RowVersion) };
        return await Command(projectId, body.RequestId, new { operation = "assign", id, body }, access, db, clock, async (p, ctx) =>
        {
            var h = await Load(db, p.Id, id);
            var facts = await Facts(db, h);
            Access.Demand(Permissions.AssignHandoff(access.Actor, ctx, facts));
            await Http.CheckVersion(db, http, h, body.RowVersion);
            Check.That(h.Status is not (HandoffStatus.Cancelled or HandoffStatus.Incorporated), "status", "handoff.fixed");
            var reason = Check.Reason(body.Reason);
            await Owners(db, p, body.SendingOwnerId, body.ReceivingOwnerId);
            await IndependentAssignment(db, h, body.SendingOwnerId, body.ReceivingOwnerId, (await store.Get(db)).AllowSelfReview);
            (h.SendingOwnerId, h.ReceivingOwnerId) = (body.SendingOwnerId, body.ReceivingOwnerId);
            await SubmissionEndpoints.InvalidateForHandoff(db, p.Id, h.TargetTaskId, h.TargetDeliverableId, notify);
            db.Audit.Note(h, reason: reason);
            await notify.Send(NotificationEvents.HandoffChanged, new Guid?[] { h.SendingOwnerId, h.ReceivingOwnerId },
                new NotifyItem(p.Id, "Handoff", h.Id, h.Key, $"/projects/{p.ProjectNumber}/handoffs?panel=Handoff:{h.Id}", p.ProjectNumber),
                Text.Get("handoff.reassigned", h.Key, h.Title));
            return h;
        });
    }

    static IQueryable<Handoff> Query(HubDb db, Project p, Filter f, Guid userId, DateOnly today)
    {
        var q = db.Handoffs.AsNoTracking().Where(h => h.ProjectId == p.Id);
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = "%" + f.Q.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            q = q.Where(h => EF.Functions.ILike(h.Title, term, "\\") || EF.Functions.ILike(h.Key, term, "\\"));
        }
        if (!string.IsNullOrEmpty(f.Status)) { Check.OneOf(f.Status, HandoffStatus.All, "status"); q = q.Where(h => h.Status == f.Status); }
        if (!string.IsNullOrEmpty(f.Direction)) Check.OneOf(f.Direction, ["incoming", "outgoing"], "direction");
        if (f.Direction == "incoming") q = q.Where(h => h.ReceivingOwnerId == userId);
        if (f.Direction == "outgoing") q = q.Where(h => h.SendingOwnerId == userId);
        if (f.DisciplineId is { } d) q = q.Where(h => h.SendingDisciplineId == d || h.ReceivingDisciplineId == d);
        if (f.Overdue == true) q = q.Where(h => p.Status != ProjectStatus.OnHold && h.NeededBy < today
            && h.Status != HandoffStatus.Accepted && h.Status != HandoffStatus.Incorporated && h.Status != HandoffStatus.Cancelled);
        return q.OrderBy(h => h.NeededBy).ThenBy(h => h.Seq);
    }

    static async Task<List<HandoffRow>> Rows(HubDb db, IQueryable<Handoff> query, Project p, DateOnly today)
    {
        var eligible = Coordination.People(db, p).Select(u => u.Id);
        var rows = await query.Select(h => new RowFacts(h,
            db.Users.Where(u => u.Id == h.SendingOwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Users.Where(u => u.Id == h.ReceivingOwnerId).Select(u => u.DisplayName).FirstOrDefault(),
            db.Deliverables.IgnoreQueryFilters().Where(d => d.Id == h.SourceDeliverableId).Select(d => d.Key).FirstOrDefault(),
            db.Deliverables.Where(d => d.Id == h.SourceDeliverableId).Select(d => (int?)d.RowVersion).FirstOrDefault(),
            db.Tasks.IgnoreQueryFilters().Where(t => t.Id == h.TargetTaskId).Select(t => t.Key).FirstOrDefault()
                ?? db.Deliverables.IgnoreQueryFilters().Where(d => d.Id == h.TargetDeliverableId).Select(d => d.Key).FirstOrDefault(),
            eligible.Contains(h.SendingOwnerId) && eligible.Contains(h.ReceivingOwnerId))).ToListAsync();
        return rows.Select(r => new HandoffRow(r.H.Id, r.H.Key, r.H.Title, r.H.Status, r.H.RowVersion,
            r.H.SourceDeliverableId, r.H.SourceRowVersion, r.H.DeclaredRevision, r.H.SourceUrl, r.H.SendingDisciplineId, r.H.ReceivingDisciplineId,
            r.H.SendingOwnerId, r.H.ReceivingOwnerId, r.H.TargetTaskId, r.H.TargetDeliverableId, r.H.IntendedUse, r.H.AcceptanceCriteria,
            r.H.NeededBy, r.H.PromisedBy, r.H.CurrentRevisionId, r.H.IncorporatedRevisionId,
            r.SenderName, r.ReceiverName, r.SourceKey, r.TargetKey, r.OwnersAvailable,
            r.CurrentSourceVersion != r.H.SourceRowVersion,
            HandoffRules.DateMismatch(r.H.NeededBy, r.H.PromisedBy), HandoffRules.Overdue(r.H.Status, r.H.NeededBy, today, p.Status))).ToList();
    }

    static async Task<Ok<Page<HandoffRow>>> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(projectId, track: false);
        var today = clock.Today(await store.Get(db));
        var query = Query(db, p, filter, access.Me.Id, today);
        var (pg, size) = Http.Paging(page, pageSize);
        return TypedResults.Ok(new Page<HandoffRow>(await Rows(db, query.Skip((pg - 1) * size).Take(size), p, today), pg, size, await query.CountAsync()));
    }

    static async Task<Ok<HandoffOptions>> Options(Guid projectId, Access access, HubDb db)
    {
        var (p, ctx) = await access.Project(projectId, track: false);
        var sources = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == p.Id && d.Status != DeliverableStatus.Cancelled)
            .OrderBy(d => d.Seq).Select(d => new HandoffSource(d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.Revision, d.TransmittalUrl, d.RowVersion, d.DueDate)).ToListAsync();
        var disciplines = await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.IsActive).Select(d => new HandoffDiscipline(d.Id, d.Discipline!.Name)).ToListAsync();
        return TypedResults.Ok(new HandoffOptions(sources, disciplines,
            await Coordination.People(db, p).OrderBy(u => u.DisplayName).Select(u => new HandoffPerson(u.Id, u.DisplayName)).ToListAsync(),
            await db.Tasks.Where(t => t.ProjectId == p.Id && t.Status != TaskStatuses.Cancelled).OrderBy(t => t.Seq)
                .Select(t => new HandoffTask(t.Id, t.Key, t.Name, t.ProjectDisciplineId, t.AssigneeId, t.DueDate)).ToListAsync(),
            sources.Any(s => disciplines.Any(d => Permissions.CreateHandoff(access.Actor, ctx, s.ProjectDisciplineId, d.Id, s.OwnerId).Ok))));
    }

    static async Task<Ok<HandoffDetail>> Detail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, ctx) = await access.Project(projectId, track: false);
        var h = await Load(db, projectId, id);
        var facts = await Facts(db, h);
        var settings = await store.Get(db);
        HandoffPermission Permission(Allow allow) => new(allow.Ok, allow.Why is null ? null : Text.Get(allow.Why, allow.Arg ?? ""));
        return TypedResults.Ok(new HandoffDetail(
            (await Rows(db, db.Handoffs.AsNoTracking().Where(x => x.Id == id), p, clock.Today(settings))).Single(),
            new HandoffDetailPermissions(Permission(Permissions.EditHandoff(access.Actor, ctx, facts)), Permission(Permissions.AssignHandoff(access.Actor, ctx, facts)),
                HandoffStatus.All.Where(to => HandoffRules.Step(h.Status, to)).Select(to => new HandoffTransition(to,
                    Permission(Permissions.HandoffTransition(access.Actor, ctx, facts, to, settings.AllowSelfReview)))).ToList()),
            await db.ChangeAssessments.Where(a => a.ProjectId == projectId && (a.HandoffId == id || a.TargetId == (h.TargetTaskId ?? h.TargetDeliverableId)
                    && db.ChangeNotices.Any(c => c.Id == a.ChangeNoticeId && db.SourceRevisions.Any(r => r.Id == c.NewRevisionId && r.DeliverableId == h.SourceDeliverableId))))
                .Join(db.ChangeNotices, a => a.ChangeNoticeId, c => c.Id, (a, c) => new HandoffChangeAssessment(a.Id, a.Status, a.ChangeNoticeId, c.Key, c.Title, c.Status)).ToListAsync(),
            await db.HandoffReceiptEvents.AsNoTracking().Where(e => e.HandoffId == h.Id).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id)
                .Select(e => new HandoffHistoryRow(e.Id, e.FromStatus, e.ToStatus, e.RevisionId, e.Reason, e.CriteriaOutcome, e.CreatedAt, e.CreatedBy,
                    db.Users.Where(u => u.Id == e.CreatedBy).Select(u => u.DisplayName).FirstOrDefault())).ToListAsync(),
            await db.HandoffRevisions.AsNoTracking().Where(r => r.HandoffId == h.Id).OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
                .Join(db.SourceRevisions, r => r.SourceRevisionId, s => s.Id, (r, s) => new HandoffRevisionRow(r.Id, r.PreviousRevisionId,
                    r.IntendedUse, r.AcceptanceCriteria, r.NeededBy, r.PromisedBy, r.SendingOwnerId, r.ReceivingOwnerId,
                    r.TargetTaskId, r.TargetDeliverableId, r.Response, r.CreatedAt, r.CreatedBy,
                    new HandoffRevisionSource(s.Id, s.SourceKey, s.Title, s.Revision, s.Url, s.SourceRowVersion, s.CreatedAt, s.CreatedBy))).ToListAsync()));
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(projectId, track: false);
        var today = clock.Today(await store.Get(db));
        var rows = await Rows(db, Query(db, p, filter, access.Me.Id, today).Take(Export.MaxRows + 1), p, today);
        return await ExportFile.Send(db, store, format, Text.Get("export.handoffs", p.ProjectNumber), Columns,
            JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-handoffs", clock);
    }
}
