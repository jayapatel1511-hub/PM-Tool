using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ChangeEndpoints
{
    public sealed record RegisterBody(Guid RequestId, Guid? DeliverableId, int? DeliverableRowVersion, Guid ProjectDisciplineId, Guid OwnerId,
        string SourceSystem, string ExternalIdentifier, string Title, string Revision, string Url, string Issuer, string Scope, DateTimeOffset? SourceCheckedAt,
        Guid? SupersedesId, int? HeadRowVersion, string? Description, DateOnly? EffectiveDate, DateOnly? AssessmentDueDate);
    public sealed record TargetRef(string TargetType, Guid TargetId, int RowVersion);
    public sealed record PublishBody(Guid RequestId, int RowVersion, int? HeadRowVersion, TargetRef[]? AdditionalTargets);
    public sealed record NoticeAction(Guid RequestId, int RowVersion, string Action, string Reason, Guid? OwnerId);
    public sealed record AdoptBody(Guid RequestId, string TargetType, Guid TargetId, int TargetRowVersion, Guid SourceRevisionId, Guid ExpectedCurrentRevisionId, int? InputUseRowVersion, string IntendedUse, string Reason);
    public sealed record AssessmentBody(Guid RequestId, int RowVersion, string Action, string? Status, string? Rationale, string? EvidenceUrl, Guid? CorrectionTaskId,
        int? CorrectionTaskRowVersion, decimal? EffortImpactHours, int? DateImpactDays, Guid? OwnerId, Guid? ReviewerId, int? TargetRowVersion,
        int? InputUseRowVersion, Guid? ExpectedCurrentRevisionId);
    public sealed record IssueImpactBody(Guid RequestId, int RowVersion, string Disposition, string Reason);
    public sealed record Filter(string? Q, string? Status, Guid? OwnerId, bool? Mine);
    static readonly Col[] Columns = [new("key", "key"), new("title", "name"), new("status", "status"), new("assessmentDueDate", "due", "date"), new("pendingAssessments", "changePending"), new("scope", "changeScope")];
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/changes", List);
        api.MapGet("/projects/{projectId:guid}/changes/options", Options);
        api.MapGet("/projects/{projectId:guid}/changes/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/changes/{id:guid}", Detail);
        api.MapGet("/projects/{projectId:guid}/input-uses", Uses);
        api.MapPost("/projects/{projectId:guid}/source-revisions", Register).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/input-uses", Adopt).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/changes/{id:guid}/publish", Publish).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/changes/{id:guid}/action", NoticeCommand).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/changes/{id:guid}/assessments/{assessmentId:guid}", Assess).WithMetadata(new Coordination.AtomicCommand());
        api.MapGet("/issues/{id:guid}/reference-impacts", ListIssueImpacts);
        api.MapPost("/issues/{id:guid}/reference-impacts/{impactId:guid}", DecideIssueImpact).WithMetadata(new Coordination.AtomicCommand());
    }
    static async Task<ChangeNotice> Load(HubDb db, Guid project, Guid id) => await db.ChangeNotices.SingleOrDefaultAsync(c => c.ProjectId == project && c.Id == id) ?? throw ApiException.NotFound();
    static void HeadVersion(SourceHead head, int? version) => Coordination.Version(head, version);
    static async Task Notify(Notifier notify, Project p, ChangeNotice c, IEnumerable<Guid> ids) => await notify.Send(NotificationEvents.ChangeImpact, ids.Select(x => (Guid?)x),
        new NotifyItem(p.Id, "ChangeNotice", c.Id, c.Key, $"/projects/{p.ProjectNumber}/changes?panel=ChangeNotice:{c.Id}", p.ProjectNumber), Text.Get("change.notification", c.Key, c.Title, c.Status));
    static Task<Coordination.Result> Register(Guid projectId, RegisterBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "source.register", body }, access, db, clock, async (p, ctx) => {
            await Coordination.Person(db, p, body.OwnerId); await Coordination.Discipline(db, p.Id, body.ProjectDisciplineId);
            var system = Check.Required(body.SourceSystem, "sourceSystem", 100); var external = Check.Required(body.ExternalIdentifier, "externalIdentifier", 200);
            var identity = body.DeliverableId is { } did ? $"deliverable:{did}" : "external:" + Coordination.Hash(new { system, external });
            Guid[] authors = []; var sourceVersion = 0; var sourceKey = external;
            if (body.DeliverableId is { } deliverable) {
                var w = await Coordination.Target(db, p, "Deliverable", deliverable);
                Check.That(w.DisciplineId == body.ProjectDisciplineId, "projectDisciplineId", "coord.reference");
                if (w.RowVersion != body.DeliverableRowVersion) throw ApiException.Conflict("source_changed", "coord.stale", new { currentRowVersion = w.RowVersion });
                Access.Demand(Permissions.PublishSource(access.Actor, ctx, w.DisciplineId, w.OwnerId));
                if (body.OwnerId != w.OwnerId) Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, w.DisciplineId));
                sourceVersion = w.RowVersion; sourceKey = w.Key; authors = await Coordination.Authors(db, deliverable);
            } else Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, body.ProjectDisciplineId));
            var head = await db.SourceHeads.SingleOrDefaultAsync(h => h.ProjectId == p.Id && h.Identity == identity);
            if (head is not null && body.DeliverableId is null)
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, head.ProjectDisciplineId));
            SourceRevision? previous = null;
            if (body.SupersedesId is { } old) {
                previous = await Coordination.Revision(db, p.Id, old); Check.That(Coordination.Identity(previous) == identity, "supersedesId", "change.same_source");
                if (head is not null) { HeadVersion(head, body.HeadRowVersion); Check.That(head.CurrentRevisionId == old, "supersedesId", "change.current"); }
                else Check.That(await Coordination.Published(db, previous), "supersedesId", "change.current");
            } else Check.That(head == null, "supersedesId", "change.explicit_supersedes");
            Check.That(body.SourceCheckedAt is null || body.SourceCheckedAt <= clock.GetUtcNow(), "sourceCheckedAt", "change.future_check");
            var revision = new SourceRevision { ProjectId = p.Id, DeliverableId = body.DeliverableId, SourceRowVersion = sourceVersion, SourceIdentity = identity,
                SourceSystem = system, ExternalIdentifier = external, SourceKey = sourceKey, Title = Check.Required(body.Title, "title"), Revision = Check.Required(body.Revision, "revision", 100),
                Url = Coordination.Url(body.Url), Issuer = Check.Required(body.Issuer, "issuer", 200), Scope = Check.Required(body.Scope, "scope", 2000), SourceCheckedAt = body.SourceCheckedAt, SupersedesId = previous?.Id, AuthorIds = authors };
            revision.IdentityHash = Coordination.Hash(new { identity, revision.Revision, revision.Url, revision.Issuer, revision.Scope, revision.SupersedesId, sourceVersion });
            Check.That(!await db.SourceRevisions.AnyAsync(r => r.ProjectId == p.Id && r.IdentityHash == revision.IdentityHash), "revision", "change.duplicate_revision");
            db.SourceRevisions.Add(revision);
            if (previous == null) {
                db.SourceHeads.Add(new SourceHead { ProjectId = p.Id, Identity = identity, CurrentRevisionId = revision.Id, OwnerId = body.OwnerId, ProjectDisciplineId = body.ProjectDisciplineId }); return revision;
            }
            if (head == null) db.SourceHeads.Add(new SourceHead { ProjectId = p.Id, Identity = identity, CurrentRevisionId = previous.Id, OwnerId = body.OwnerId, ProjectDisciplineId = body.ProjectDisciplineId });
            var c = new ChangeNotice { ProjectId = p.Id, Title = revision.Title, OwnerId = body.OwnerId, ProjectDisciplineId = body.ProjectDisciplineId,
                OldRevisionId = previous.Id, NewRevisionId = revision.Id, Description = Check.Required(body.Description, "description", 4000), Scope = revision.Scope,
                EffectiveDate = body.EffectiveDate ?? throw ApiException.Invalid("effectiveDate", "error.required"), AssessmentDueDate = body.AssessmentDueDate ?? throw ApiException.Invalid("assessmentDueDate", "error.required") };
            (c.Seq, c.Key) = await Keys.Next(db, p.Id, p.ProjectNumber, "change"); db.ChangeNotices.Add(c); return c;
        });
    static async Task AddAssessment(HubDb db, Project p, ChangeNotice notice, string type, Guid id, Guid revision, Guid? use, Guid? handoff)
    {
        var existing = db.ChangeAssessments.Local.FirstOrDefault(a => a.ChangeNoticeId == notice.Id && a.TargetType == type && a.TargetId == id)
            ?? await db.ChangeAssessments.SingleOrDefaultAsync(a => a.ChangeNoticeId == notice.Id && a.TargetType == type && a.TargetId == id);
        if (existing is not null) { existing.InputUseId ??= use; existing.HandoffId ??= handoff; return; }
        var work = await Coordination.Target(db, p, type, id);
        var lead = await db.ProjectDisciplines.Where(d => d.Id == work.DisciplineId).Select(d => d.LeadUserId).SingleAsync();
        var reviewer = lead != null && lead != work.OwnerId ? lead : p.ProjectManagerId != work.OwnerId ? p.ProjectManagerId : (Guid?)null;
        db.ChangeAssessments.Add(new ChangeAssessment { ProjectId = p.Id, ChangeNoticeId = notice.Id, TargetType = type, TargetId = id, OwnerId = work.OwnerId,
            ReviewerId = reviewer, RevisionUsedId = revision, InputUseId = use, HandoffId = handoff });
    }
    static async Task<Guid[]> CreateIssueImpacts(HubDb db, Project p, SourceRevision oldRevision, SourceRevision newRevision, TimeProvider clock, Guid actor)
    {
        var recipients = new HashSet<Guid>();
        // A drawing number and revision can occur in more than one source. Only an exact
        // registered source link establishes that this reference was superseded.
        var documents = await db.IssueDocumentReferences.Where(d => d.ProjectId == p.Id && d.Revision == oldRevision.Revision && d.SourceUrl == oldRevision.Url &&
            (d.Identifier == oldRevision.ExternalIdentifier || d.Identifier == oldRevision.SourceKey) &&
            db.Issues.Any(i => i.Id == d.IssueId && i.Status == IssueStatus.Resolved)).ToListAsync();
        foreach (var document in documents)
        {
            var exists = await db.IssueReferenceImpactAssessments.AnyAsync(a => a.IssueId == document.IssueId && a.DocumentReferenceId == document.Id &&
                a.PreviousRevisionId == oldRevision.Id && a.CurrentRevisionId == newRevision.Id);
            if (exists) continue;
            var issue = await db.Issues.SingleAsync(i => i.Id == document.IssueId);
            var verifier = await db.IssueVerifications.Where(v => v.IssueId == issue.Id).OrderByDescending(v => v.IssueRowVersion).FirstOrDefaultAsync();
            var impact = new IssueReferenceImpactAssessment { ProjectId = p.Id, IssueId = issue.Id, DocumentReferenceId = document.Id,
                PreviousRevisionId = oldRevision.Id, CurrentRevisionId = newRevision.Id, OwnerId = issue.OwnerId,
                VerifierId = verifier?.VerifierId, Status = IssueReferenceImpactStatus.Pending, CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(), CreatedBy = actor, UpdatedBy = actor };
            db.IssueReferenceImpactAssessments.Add(impact); db.Audit.Note(impact, reason: "Source revision superseded");
            recipients.Add(issue.OwnerId);
            if (verifier is not null) recipients.Add(verifier.VerifierId);
        }
        return [.. recipients];
    }
    static Task<Coordination.Result> Publish(Guid projectId, Guid id, PublishBody body, Access access, HubDb db, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "change.publish", id, body }, access, db, clock, async (p, ctx) => {
            var c = await Load(db, p.Id, id); Coordination.Version(c, body.RowVersion); Check.That(c.Status == ChangeStatus.Draft, "status", "change.draft");
            var next = await Coordination.Revision(db, p.Id, c.NewRevisionId); var head = await Coordination.Head(db, next) ?? throw ApiException.Invalid("source", "coord.reference");
            var publisherOwner = next.DeliverableId is { } sourceId ? (await Coordination.Target(db, p, "Deliverable", sourceId)).OwnerId : head.OwnerId;
            Access.Demand(Permissions.PublishSource(access.Actor, ctx, c.ProjectDisciplineId, publisherOwner)); await Coordination.Person(db, p, c.OwnerId); await Coordination.Discipline(db, p.Id, c.ProjectDisciplineId);
            Access.Demand(Permissions.PublishSource(access.Actor, ctx, head.ProjectDisciplineId, head.OwnerId));
            HeadVersion(head, body.HeadRowVersion);
            if (head.CurrentRevisionId != c.OldRevisionId) throw ApiException.Conflict("source_changed", "change.current", new { currentRevisionId = head.CurrentRevisionId, currentRowVersion = head.RowVersion });
            if (next.DeliverableId is { } source) {
                var w = await Coordination.Target(db, p, "Deliverable", source);
                Access.Demand(Permissions.PublishSource(access.Actor, ctx, w.DisciplineId, w.OwnerId));
            }
            foreach (var use in await db.InputUses.Where(u => u.ProjectId == p.Id && u.SourceIdentity == head.Identity && u.SourceRevisionId != next.Id).ToListAsync())
                await AddAssessment(db, p, c, use.TargetType, use.TargetId, use.SourceRevisionId, use.Id, null);
            var handoffs = await db.Handoffs.Where(h => h.ProjectId == p.Id && h.Status != HandoffStatus.Cancelled && h.CurrentRevisionId != null)
                .Join(db.HandoffRevisions, h => h.CurrentRevisionId, r => (Guid?)r.Id, (h, r) => new { H = h, R = r })
                .Join(db.SourceRevisions, x => x.R.SourceRevisionId, r => r.Id, (x, r) => new { x.H, Source = r }).ToListAsync();
            foreach (var h in handoffs.Where(h => Coordination.Identity(h.Source) == head.Identity && h.Source.Id != next.Id))
                await AddAssessment(db, p, c, h.H.TargetTaskId != null ? "Task" : "Deliverable", h.H.TargetTaskId ?? h.H.TargetDeliverableId!.Value, h.Source.Id, null, h.H.Id);
            Check.That((body.AdditionalTargets?.Length ?? 0) <= 200, "additionalTargets", "change.targets");
            foreach (var target in body.AdditionalTargets ?? []) {
                var w = await Coordination.Target(db, p, target.TargetType, target.TargetId);
                if (w.RowVersion != target.RowVersion) throw ApiException.Conflict("target_changed", "coord.stale", new { currentRowVersion = w.RowVersion, id = w.Id });
                await AddAssessment(db, p, c, w.Type, w.Id, c.OldRevisionId, null, null);
            }
            head.CurrentRevisionId = next.Id; c.Status = ChangeStatus.Open; c.PublishedAt = clock.GetUtcNow();
            var issueImpactRecipients = await CreateIssueImpacts(db, p, await Coordination.Revision(db, p.Id, c.OldRevisionId), next, clock, access.Me.Id);
            await db.SaveChangesAsync();
            await ReviewEndpoints.AdvanceForPublishedRevision(db, notify, p, await Coordination.Revision(db, p.Id, c.OldRevisionId), next);
            await SubmissionEndpoints.InvalidateForPublishedRevision(db, p.Id, c.OldRevisionId);
            await Notify(notify, p, c, (await db.ChangeAssessments.Where(a => a.ChangeNoticeId == c.Id).Select(a => a.OwnerId).ToListAsync())
                .Concat(issueImpactRecipients).Distinct()); return c;
        });
    static Task<Coordination.Result> Adopt(Guid projectId, AdoptBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "input.adopt", body }, access, db, clock, async (p, ctx) => {
            var w = await Coordination.Target(db, p, body.TargetType, body.TargetId); Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, w.OwnerId));
            if (w.RowVersion != body.TargetRowVersion) throw ApiException.Conflict("target_changed", "coord.stale", new { currentRowVersion = w.RowVersion });
            Check.That(w.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "targetId", "review.issued");
            var r = await Coordination.Revision(db, p.Id, body.SourceRevisionId); var head = await Coordination.Head(db, r);
            if (head == null || head.CurrentRevisionId != body.ExpectedCurrentRevisionId) throw ApiException.Conflict("source_changed", "change.current");
            Check.That(head.CurrentRevisionId == r.Id, "sourceRevisionId", "change.retain_via_assessment");
            var use = await db.InputUses.SingleOrDefaultAsync(u => u.ProjectId == p.Id && u.TargetType == w.Type && u.TargetId == w.Id && u.SourceIdentity == head.Identity);
            if (use != null) Coordination.Version(use, body.InputUseRowVersion);
            Check.That(!await db.ChangeAssessments.AnyAsync(a => a.ProjectId == p.Id && a.TargetType == w.Type && a.TargetId == w.Id && db.ChangeNotices.Any(c => c.Id == a.ChangeNoticeId && c.Status == ChangeStatus.Open && c.NewRevisionId == r.Id)), "sourceRevisionId", "change.adopt_via_assessment");
            return await Coordination.Adopt(db, p, w, r, body.IntendedUse, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow());
        });
    static IQueryable<ChangeAssessment> Incomplete(HubDb db) => db.ChangeAssessments.Where(a =>
        (a.Status != AssessmentStatus.Unaffected && a.Status != AssessmentStatus.Resolved) || a.Rationale == null || a.Rationale == "" || a.EvidenceUrl == null || a.EvidenceUrl == ""
        || (a.RetainOldRevision && a.RetentionApprovedBy == null)
        || (!a.RetainOldRevision && !db.InputUses.Any(u => u.Id == a.InputUseId && db.ChangeNotices.Any(c => c.Id == a.ChangeNoticeId && c.NewRevisionId == u.SourceRevisionId)))
        || (a.Status == AssessmentStatus.Resolved && (a.VerifiedBy == null || !db.Tasks.Any(t => t.Id == a.CorrectionTaskId && t.Status == TaskStatuses.Complete))));
    static async Task<bool> Complete(HubDb db, ChangeAssessment a) => !await Incomplete(db).AnyAsync(x => x.Id == a.Id);
    static Task<Coordination.Result> NoticeCommand(Guid projectId, Guid id, NoticeAction body, Access access, HubDb db, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "change.action", id, body }, access, db, clock, async (p, ctx) => {
            var c = await Load(db, p.Id, id); Coordination.Version(c, body.RowVersion); Check.That(c.Status is ChangeStatus.Draft or ChangeStatus.Open, "status", "change.frozen");
            var reason = Check.Reason(body.Reason);
            if (body.Action == "close") {
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, c.OwnerId)); Check.That(c.Status == ChangeStatus.Open, "status", "change.open");
                var assessments = await db.ChangeAssessments.Where(a => a.ChangeNoticeId == c.Id).ToListAsync(); var done = new List<bool>();
                foreach (var a in assessments) { var work = await Coordination.Target(db, p, a.TargetType, a.TargetId); Check.That(work.OwnerId == a.OwnerId, "ownerId", "change.owner_changed"); await Coordination.Person(db, p, a.OwnerId); done.Add(await Complete(db, a)); }
                Check.That(ChangeRules.CanClose(done), "assessments", "change.incomplete"); c.Status = ChangeStatus.Closed;
            } else {
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, c.ProjectDisciplineId));
                if (body.Action == "assign") { var owner = body.OwnerId ?? throw ApiException.Invalid("ownerId", "error.required"); await Coordination.Person(db, p, owner); c.OwnerId = owner; }
                else { Check.OneOf(body.Action, ["cancel"], "action"); c.Status = ChangeStatus.Cancelled; }
            }
            db.Audit.Note(c, reason: reason); await Notify(notify, p, c, await db.ChangeAssessments.Where(a => a.ChangeNoticeId == c.Id).Select(a => a.OwnerId).ToListAsync()); return c;
        });
    static Task<Coordination.Result> Assess(Guid projectId, Guid id, Guid assessmentId, AssessmentBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "change.assess", id, assessmentId, body }, access, db, clock, async (p, ctx) => {
            var c = await Load(db, p.Id, id); Check.That(c.Status == ChangeStatus.Open, "status", "change.open");
            var a = await db.ChangeAssessments.SingleOrDefaultAsync(a => a.Id == assessmentId && a.ChangeNoticeId == id) ?? throw ApiException.NotFound(); Coordination.Version(a, body.RowVersion);
            var w = await Coordination.Target(db, p, a.TargetType, a.TargetId); var self = (await settings.Get(db)).AllowSelfReview;
            if (body.Action == "assign") {
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, w.DisciplineId));
                Check.Reason(body.Rationale); Check.That(body.OwnerId == w.OwnerId, "ownerId", "change.work_owner"); await Coordination.Person(db, p, w.OwnerId);
                var reviewer = body.ReviewerId ?? throw ApiException.Invalid("reviewerId", "error.required"); await Coordination.Person(db, p, reviewer, "reviewerId");
                Check.That(ReviewRules.Independent(reviewer, [w.OwnerId], self), "reviewerId", "review.independent");
                a.OwnerId = w.OwnerId; a.ReviewerId = reviewer; a.Status = AssessmentStatus.Pending; a.AcknowledgedAt = null; a.RetentionApprovedBy = null; a.RetentionReason = null; a.VerifiedBy = null; a.VerifiedAt = null;
            } else if (body.Action == "approveRetention") {
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, w.DisciplineId)); await Coordination.Person(db, p, access.Me.Id);
                Check.That(a.OwnerId == w.OwnerId && a.RetainOldRevision && a.Status is AssessmentStatus.Unaffected or AssessmentStatus.UpdateRequired, "status", "change.retention");
                Check.That(ReviewRules.Independent(access.Me.Id, [a.OwnerId], self), "ownerId", "review.independent");
                a.RetentionReason = Check.Reason(body.Rationale); a.RetentionApprovedBy = access.Me.Id;
            } else if (body.Action == "resolve") {
                var reviewer = a.ReviewerId ?? throw ApiException.Invalid("reviewerId", "error.required"); Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, reviewer)); await Coordination.Person(db, p, reviewer);
                Check.That(a.OwnerId == w.OwnerId && a.Status == AssessmentStatus.UpdateRequired, "status", "change.update_required");
                var task = a.CorrectionTaskId is { } tid ? await Coordination.Target(db, p, "Task", tid) : null;
                if (task != null && task.RowVersion != body.CorrectionTaskRowVersion) throw ApiException.Conflict("task_changed", "coord.stale", new { currentRowVersion = task.RowVersion });
                Check.That(ChangeRules.CorrectionReady(task?.Status, ReviewRules.Independent(reviewer, new[] { a.OwnerId, task?.OwnerId ?? Guid.Empty, task?.AuthorId ?? Guid.Empty }, self), !string.IsNullOrWhiteSpace(a.EvidenceUrl)), "correctionTaskId", "change.correction");
                Check.That(!a.RetainOldRevision || a.RetentionApprovedBy != null, "retention", "change.retention");
                a.VerifiedBy = reviewer; a.VerifiedAt = clock.GetUtcNow(); a.Status = AssessmentStatus.Resolved; Check.Reason(body.Rationale);
            } else {
                Check.That(a.OwnerId == w.OwnerId, "ownerId", "change.owner_changed"); Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, a.OwnerId));
                if (body.Action == "acknowledge") a.AcknowledgedAt = clock.GetUtcNow();
                else if (body.Action == "adopt") {
                    Check.That(a.Status is AssessmentStatus.UpdateRequired or AssessmentStatus.Unaffected, "status", "change.assess_first");
                    if (w.RowVersion != body.TargetRowVersion) throw ApiException.Conflict("target_changed", "coord.stale", new { currentRowVersion = w.RowVersion });
                    Check.That(w.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "targetId", "review.issued");
                    var revision = await Coordination.Revision(db, p.Id, c.NewRevisionId); var head = await Coordination.Head(db, revision);
                    if (head == null || head.CurrentRevisionId != body.ExpectedCurrentRevisionId || head.CurrentRevisionId != revision.Id) throw ApiException.Conflict("source_changed", "change.current");
                    var existing = await db.InputUses.SingleOrDefaultAsync(u => u.ProjectId == p.Id && u.TargetType == w.Type && u.TargetId == w.Id && u.SourceIdentity == head.Identity);
                    if (existing != null) Coordination.Version(existing, body.InputUseRowVersion);
                    var use = await Coordination.Adopt(db, p, w, revision, existing?.IntendedUse ?? c.Scope, Check.Reason(body.Rationale), access.Me.Id, clock.GetUtcNow());
                    a.InputUseId = use.Id; a.RetainOldRevision = false; a.RetentionApprovedBy = null; a.RetentionReason = null;
                } else {
                    Check.OneOf(body.Action, ["disposition"], "action"); Check.OneOf(body.Status, [AssessmentStatus.Unaffected, AssessmentStatus.UpdateRequired, AssessmentStatus.Clarification], "status");
                    a.Rationale = Check.Required(body.Rationale, "rationale", 4000); a.EvidenceUrl = Coordination.Url(body.EvidenceUrl); a.Status = body.Status!;
                    a.RetentionApprovedBy = null; a.RetentionReason = null; a.VerifiedBy = null; a.VerifiedAt = null;
                    if (a.Status == AssessmentStatus.UpdateRequired) {
                        var tid = body.CorrectionTaskId ?? throw ApiException.Invalid("correctionTaskId", "error.required"); var task = await Coordination.Target(db, p, "Task", tid);
                        if (task.RowVersion != body.CorrectionTaskRowVersion) throw ApiException.Conflict("task_changed", "coord.stale", new { currentRowVersion = task.RowVersion });
                        Check.That(task.OwnerId == a.OwnerId, "correctionTaskId", "change.correction_owner");
                        Check.That(body.EffortImpactHours is >= 0 && body.DateImpactDays is not null, "effortImpactHours", "change.estimate");
                        a.CorrectionTaskId = tid; a.EffortImpactHours = body.EffortImpactHours; a.DateImpactDays = body.DateImpactDays;
                    }
                }
            }
            db.Audit.Note(a, reason: body.Rationale); await Notify(notify, p, c, new[] { a.OwnerId, a.ReviewerId, c.OwnerId }.OfType<Guid>()); return a;
        });
    public static async Task<object> Options(Guid projectId, Access access, HubDb db)
    {
        var (p, ctx) = await access.Project(projectId, false);
        var sources = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == p.Id).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var heads = await db.SourceHeads.AsNoTracking().Where(h => h.ProjectId == p.Id).ToListAsync();
        var published = new HashSet<Guid>();
        foreach (var r in sources) if (await Coordination.Published(db, r)) published.Add(r.Id);
        var disciplines = await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.IsActive).Select(d => d.Id).ToListAsync();
        return new { ActorId = access.Me.Id, CanWrite = Permissions.CoordinationWrite(access.Actor, ctx).Ok,
            ManageDisciplineIds = disciplines.Where(d => Permissions.ManageCoordination(access.Actor, ctx, d).Ok).ToArray(),
            People = await Coordination.People(db, p).OrderBy(u => u.DisplayName).Select(u => new { u.Id, u.DisplayName }).ToListAsync(),
            Disciplines = await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.IsActive).Select(d => new { d.Id, d.Discipline!.Name }).ToListAsync(),
            Sources = sources.Select(r => new { Revision = r, Identity = Coordination.Identity(r), Published = published.Contains(r.Id), IsCurrent = !heads.Any(h => h.Identity == Coordination.Identity(r)) || heads.Any(h => h.CurrentRevisionId == r.Id) }), Heads = heads,
            Deliverables = await db.Deliverables.Where(d => d.ProjectId == p.Id && d.Status != DeliverableStatus.Cancelled).Select(d => new { d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.RowVersion, d.Revision, d.TransmittalUrl, d.Status }).ToListAsync(),
            Tasks = await db.Tasks.Where(t => t.ProjectId == p.Id && t.Status != TaskStatuses.Cancelled).Select(t => new { t.Id, t.Key, t.Name, t.ProjectDisciplineId, OwnerId = t.AssigneeId, t.RowVersion, t.Status }).ToListAsync() };
    }
    static async Task<object> Uses(Guid projectId, string? targetType, Guid? targetId, int? page, int? pageSize, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        var q = db.InputUses.AsNoTracking().Where(u => u.ProjectId == projectId);
        if (targetType != null) q = q.Where(u => u.TargetType == targetType); if (targetId != null) q = q.Where(u => u.TargetId == targetId);
        var (pg, size) = Http.Paging(page, pageSize); var total = await q.CountAsync();
        var uses = await q.OrderByDescending(u => u.AdoptedAt).ThenBy(u => u.Id).Skip((pg - 1) * size).Take(size).ToListAsync(); var ids = uses.Select(u => u.Id).ToArray();
        var targetIds = uses.Select(u => u.TargetId).ToArray();
        return new { Items = uses, Page = pg, PageSize = size, TotalCount = total, Assessments = await db.ChangeAssessments.Where(a => a.ProjectId == projectId && targetIds.Contains(a.TargetId)).Join(db.ChangeNotices, a => a.ChangeNoticeId, c => c.Id, (a, c) => new { a.TargetType, a.TargetId, a.Status, a.ChangeNoticeId, c.Key, c.NewRevisionId, NoticeStatus = c.Status }).ToListAsync(), History = await db.InputAdoptions.Where(a => ids.Contains(a.InputUseId)).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync() };
    }
    static IQueryable<ChangeNotice> Query(HubDb db, Guid project, Filter f, Guid actor)
    {
        var q = db.ChangeNotices.AsNoTracking().Where(c => c.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(c => c.Key.Contains(f.Q) || c.Title.Contains(f.Q) || c.Description.Contains(f.Q));
        if (f.Status is { Length: > 0 }) { Check.OneOf(f.Status, ChangeStatus.All, "status"); q = q.Where(c => c.Status == f.Status); }
        if (f.OwnerId is { } owner) q = q.Where(c => c.OwnerId == owner);
        if (f.Mine == true) q = q.Where(c => c.OwnerId == actor || db.ChangeAssessments.Any(a => a.ChangeNoticeId == c.Id && (a.OwnerId == actor || a.ReviewerId == actor)));
        return q.OrderBy(c => c.AssessmentDueDate).ThenBy(c => c.Seq);
    }
    static async Task<List<object>> Rows(HubDb db, IQueryable<ChangeNotice> query)
    {
        var rows = await query.ToListAsync(); var ids = rows.Select(c => c.Id).ToArray();
        var counts = await Incomplete(db).Where(a => ids.Contains(a.ChangeNoticeId)).GroupBy(a => a.ChangeNoticeId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        return rows.Select(c => (object)new { c.Id, c.Key, c.Title, c.Status, c.RowVersion, c.OwnerId, c.Scope, c.AssessmentDueDate, PendingAssessments = counts.GetValueOrDefault(c.Id) }).ToList();
    }
    static async Task<object> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db)
    { await access.Project(projectId, false); var q = Query(db, projectId, filter, access.Me.Id); var (pg, size) = Http.Paging(page, pageSize); return new Page<object>(await Rows(db, q.Skip((pg - 1) * size).Take(size)), pg, size, await q.CountAsync()); }
    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (p, ctx) = await access.Project(projectId, false); var c = await Load(db, p.Id, id);
        var assessments = await db.ChangeAssessments.Where(a => a.ChangeNoticeId == id).ToListAsync(); var newRevision = await Coordination.Revision(db, p.Id, c.NewRevisionId);
        var complete = new List<Guid>(); foreach (var a in assessments) if (await Complete(db, a)) complete.Add(a.Id);
        var useIds = assessments.Select(a => a.InputUseId).OfType<Guid>().ToArray();
        return new { Notice = c, Assessments = assessments, CompleteAssessmentIds = complete, OldRevision = await Coordination.Revision(db, p.Id, c.OldRevisionId), NewRevision = newRevision, Head = await Coordination.Head(db, newRevision),
            ActorId = access.Me.Id, CanWrite = Permissions.CoordinationWrite(access.Actor, ctx).Ok,
            CanPublish = Permissions.PublishSource(access.Actor, ctx, c.ProjectDisciplineId, newRevision.DeliverableId is { } did ? await db.Deliverables.Where(x => x.Id == did).Select(x => x.OwnerId).FirstOrDefaultAsync() : (await Coordination.Head(db, newRevision))?.OwnerId).Ok,
            CanManage = Permissions.ManageCoordination(access.Actor, ctx, c.ProjectDisciplineId).Ok, EligiblePeople = await Coordination.People(db, p).Select(u => u.Id).ToListAsync(),
            Uses = await db.InputUses.Where(u => u.ProjectId == p.Id && useIds.Contains(u.Id)).ToListAsync() };
    }
    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(projectId, false); var rows = await Rows(db, Query(db, projectId, filter, access.Me.Id).Take(Export.MaxRows + 1));
        return await ExportFile.Send(db, store, format, Text.Get("export.changes", p.ProjectNumber), Columns, JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-changes", clock);
    }
    static async Task<object> ListIssueImpacts(Guid id, Access access, HubDb db)
    {
        var (_, project, _) = await RegisterEndpoints.LoadIssue(db, access, id);
        var impacts = await db.IssueReferenceImpactAssessments.AsNoTracking().Where(a => a.ProjectId == project.Id && a.IssueId == id)
            .OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync();
        var documentIds = impacts.Select(a => a.DocumentReferenceId).Distinct().ToArray();
        var revisionIds = impacts.SelectMany(a => new[] { a.PreviousRevisionId, a.CurrentRevisionId }).Distinct().ToArray();
        var documents = await db.IssueDocumentReferences.AsNoTracking().Where(d => d.ProjectId == project.Id && d.IssueId == id && documentIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id);
        var revisions = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == project.Id && revisionIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id);
        return impacts.Select(a => new { a.Id, a.DocumentReferenceId, a.PreviousRevisionId, a.CurrentRevisionId, a.OwnerId, a.VerifierId, a.Status,
            a.OwnerDisposition, a.OwnerReason, a.VerifierDisposition, a.VerifierReason, a.OwnerDecidedAt, a.VerifierDecidedAt, a.RowVersion,
            DocumentReference = documents.TryGetValue(a.DocumentReferenceId, out var d) ? new { d.Identifier, d.Revision, d.Kind } : null,
            PreviousRevision = revisions.TryGetValue(a.PreviousRevisionId, out var old) ? new { old.SourceKey, old.ExternalIdentifier, old.Revision } : null,
            CurrentRevision = revisions.TryGetValue(a.CurrentRevisionId, out var current) ? new { current.SourceKey, current.ExternalIdentifier, current.Revision } : null }).ToList();
    }
    static async Task<Coordination.Result> DecideIssueImpact(Guid id, Guid impactId, IssueImpactBody body, Access access, HubDb db, TimeProvider clock)
    {
        var (_, project, _) = await RegisterEndpoints.LoadIssue(db, access, id);
        return await Coordination.Run(project.Id, body.RequestId, new { operation = "issue.reference_impact", id, impactId, body }, access, db, clock, async (p, _) => {
            var issue = await db.Issues.SingleOrDefaultAsync(i => i.ProjectId == p.Id && i.Id == id) ?? throw ApiException.NotFound();
            var impact = await db.IssueReferenceImpactAssessments.SingleOrDefaultAsync(a => a.ProjectId == p.Id && a.IssueId == id && a.Id == impactId)
                ?? throw ApiException.NotFound();
            Coordination.Version(impact, body.RowVersion);
            Check.That(impact.Status == IssueReferenceImpactStatus.Pending, "status", "issue.reference_impact_pending");
            Check.OneOf(body.Disposition, IssueReferenceImpactDisposition.All, "disposition");
            var reason = Check.Reason(body.Reason);
            var now = clock.GetUtcNow();
            if (access.Me.Id == impact.OwnerId && issue.OwnerId == access.Me.Id)
            {
                Check.That(impact.OwnerDisposition is null, "disposition", "issue.reference_impact_decided");
                impact.OwnerDisposition = body.Disposition; impact.OwnerReason = reason; impact.OwnerDecidedBy = access.Me.Id; impact.OwnerDecidedAt = now;
            }
            else if (impact.VerifierId == access.Me.Id && issue.OwnerId != access.Me.Id)
            {
                Check.That(impact.VerifierDisposition is null, "disposition", "issue.reference_impact_decided");
                impact.VerifierDisposition = body.Disposition; impact.VerifierReason = reason; impact.VerifierDecidedBy = access.Me.Id; impact.VerifierDecidedAt = now;
            }
            else throw ApiException.Forbidden("perm.edit");
            // FR-LOC-04: the owner and, when the issue has one, its verifier decide; an issue resolved without verification
            // (a General issue) is decided by its owner alone instead of waiting for a verifier who does not exist.
            if (impact.OwnerDisposition != null && (impact.VerifierId == null || impact.VerifierDisposition != null))
                impact.Status = impact.OwnerDisposition == IssueReferenceImpactDisposition.Reopen || impact.VerifierDisposition == IssueReferenceImpactDisposition.Reopen
                    ? IssueReferenceImpactStatus.ReopenRequested : IssueReferenceImpactStatus.Unaffected;
            impact.UpdatedAt = now; impact.UpdatedBy = access.Me.Id;
            db.Audit.Note(impact, action: impact.Status == IssueReferenceImpactStatus.ReopenRequested ? "ReopenRequested" : impact.Status == IssueReferenceImpactStatus.Unaffected ? "Unaffected" : null, reason: reason);
            return impact;
        });
    }
}
