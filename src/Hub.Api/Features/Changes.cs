using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class ChangeEndpoints
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
    static readonly Col[] Columns = Hub.Api.Records.ChangeNoticeRecord.ChangesColumns;

    // The rows below mirror the stored rows, audit fields included, in the order each row has always been written (the OrgStructure `*Response` precedent).
    /// An immutable registered source revision, as stored.
    public sealed record SourceRevisionRow(Guid ProjectId, Guid? DeliverableId, int SourceRowVersion, string SourceIdentity, string SourceSystem, string ExternalIdentifier, string Issuer,
        string Scope, DateTimeOffset? SourceCheckedAt, Guid? SupersedesId, Guid[] AuthorIds, string IdentityHash, string SourceKey, string Title, string Revision, string Url,
        string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy,
        int RowVersion, Guid Id)
    {
        public static SourceRevisionRow From(SourceRevision r) => new(r.ProjectId, r.DeliverableId, r.SourceRowVersion, r.SourceIdentity, r.SourceSystem, r.ExternalIdentifier, r.Issuer,
            r.Scope, r.SourceCheckedAt, r.SupersedesId, r.AuthorIds, r.IdentityHash, r.SourceKey, r.Title, r.Revision, r.Url, r.AuditType, r.AuditProjectId, r.AuditKey, r.AuditName,
            r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, r.RowVersion, r.Id);
    }
    /// The current revision of one source identity, as stored.
    public sealed record SourceHeadRow(string Identity, Guid CurrentRevisionId, Guid OwnerId, Guid ProjectDisciplineId, string AuditType, Guid ProjectId, Guid? AuditProjectId,
        string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static SourceHeadRow From(SourceHead h) => new(h.Identity, h.CurrentRevisionId, h.OwnerId, h.ProjectDisciplineId, h.AuditType, h.ProjectId, h.AuditProjectId,
            h.AuditKey, h.AuditName, h.CreatedAt, h.CreatedBy, h.UpdatedAt, h.UpdatedBy, h.RowVersion, h.Id);
    }
    /// What the viewer may start: Changes needs a source they may publish, Reviews a discipline they manage.
    public sealed record CoordinationCanCreate(bool Changes, bool Reviews);
    public sealed record CoordinationPerson(Guid Id, string DisplayName, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] PersonFacts.Notices? PersonNotices = null);
    public sealed record CoordinationDiscipline(Guid Id, string Name);
    /// A registered revision with its source identity; Published and IsCurrent say whether it can be reviewed or adopted now.
    public sealed record CoordinationSource(SourceRevisionRow Revision, string Identity, bool Published, bool IsCurrent);
    /// A deliverable the viewer can name as a source; CanCommit and CanManage are the viewer's own rights over it.
    public sealed record CoordinationDeliverable(Guid Id, string Key, string Name, Guid ProjectDisciplineId, Guid? OwnerId, int RowVersion, string? Revision, string? TransmittalUrl,
        string Status, bool CanCommit, bool CanManage);
    /// A task the viewer can name as work that adopts a source; CanCommit and CanManage are the viewer's own rights over it.
    public sealed record CoordinationTask(Guid Id, string Key, string Name, Guid ProjectDisciplineId, Guid? OwnerId, int RowVersion, string Status, bool CanCommit, bool CanManage);
    /// What the Changes and Reviews registers choose from, and what the viewer may create. Both options routes answer it.
    public sealed record CoordinationOptions(Guid ActorId, bool CanWrite, Guid[] ManageDisciplineIds, CoordinationCanCreate CanCreate, IReadOnlyList<CoordinationPerson> People,
        IReadOnlyList<CoordinationDiscipline> Disciplines, IReadOnlyList<CoordinationSource> Sources, IReadOnlyList<SourceHeadRow> Heads,
        IReadOnlyList<CoordinationDeliverable> Deliverables, IReadOnlyList<CoordinationTask> Tasks);
    /// One notice of the register. PendingAssessments counts its assessments that are not yet complete.
    public sealed record ChangeRow(Guid Id, string Key, string Title, string Status, int RowVersion, Guid OwnerId, string Scope, DateOnly AssessmentDueDate, int PendingAssessments);
    public sealed record ChangeNoticeRow(string Title, Guid OwnerId, Guid ProjectDisciplineId, Guid OldRevisionId, Guid NewRevisionId, string Description, string Scope, DateOnly EffectiveDate, DateOnly AssessmentDueDate,
        string Status, DateTimeOffset? PublishedAt, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName, Guid? AuditDisciplineId, Guid ProjectId, string Key, int Seq, DateTimeOffset? DeletedAt,
        Guid? DeletedBy, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static ChangeNoticeRow From(ChangeNotice c) => new(c.Title, c.OwnerId, c.ProjectDisciplineId, c.OldRevisionId, c.NewRevisionId, c.Description, c.Scope, c.EffectiveDate, c.AssessmentDueDate,
            c.Status, c.PublishedAt, c.AuditType, c.AuditProjectId, c.AuditKey, c.AuditName, c.AuditDisciplineId, c.ProjectId, c.Key, c.Seq, c.DeletedAt,
            c.DeletedBy, c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, c.RowVersion, c.Id);
    }
    /// One work item a notice asks its owner to assess against the new revision.
    public sealed record ChangeAssessmentRow(Guid ChangeNoticeId, string TargetType, Guid TargetId, Guid OwnerId, Guid? ReviewerId, Guid? InputUseId, Guid? HandoffId, Guid RevisionUsedId, string Status,
        DateTimeOffset? AcknowledgedAt, string? Rationale, string? EvidenceUrl, Guid? CorrectionTaskId, decimal? EffortImpactHours, int? DateImpactDays, bool RetainOldRevision, Guid? RetentionApprovedBy,
        string? RetentionReason, Guid? VerifiedBy, DateTimeOffset? VerifiedAt, string AuditType, Guid ProjectId, Guid? AuditProjectId, string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy,
        DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static ChangeAssessmentRow From(ChangeAssessment a) => new(a.ChangeNoticeId, a.TargetType, a.TargetId, a.OwnerId, a.ReviewerId, a.InputUseId, a.HandoffId, a.RevisionUsedId, a.Status,
            a.AcknowledgedAt, a.Rationale, a.EvidenceUrl, a.CorrectionTaskId, a.EffortImpactHours, a.DateImpactDays, a.RetainOldRevision, a.RetentionApprovedBy,
            a.RetentionReason, a.VerifiedBy, a.VerifiedAt, a.AuditType, a.ProjectId, a.AuditProjectId, a.AuditKey, a.AuditName, a.CreatedAt, a.CreatedBy,
            a.UpdatedAt, a.UpdatedBy, a.RowVersion, a.Id);
    }
    /// A source revision a work item has adopted as an input, and the use it was adopted for.
    public sealed record InputUseRow(string TargetType, Guid TargetId, Guid OwnerId, string SourceIdentity, Guid SourceRevisionId, string IntendedUse, DateTimeOffset AdoptedAt, Guid AdoptedBy, string AuditType,
        Guid ProjectId, Guid? AuditProjectId, string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static InputUseRow From(InputUse u) => new(u.TargetType, u.TargetId, u.OwnerId, u.SourceIdentity, u.SourceRevisionId, u.IntendedUse, u.AdoptedAt, u.AdoptedBy, u.AuditType,
            u.ProjectId, u.AuditProjectId, u.AuditKey, u.AuditName, u.CreatedAt, u.CreatedBy, u.UpdatedAt, u.UpdatedBy, u.RowVersion, u.Id);
    }
    /// One step of an input use's history: the revision it moved to, the use and the reason.
    public sealed record InputAdoptionRow(Guid InputUseId, Guid SourceRevisionId, string IntendedUse, string Reason, string AuditType, Guid ProjectId, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static InputAdoptionRow From(InputAdoption a) => new(a.InputUseId, a.SourceRevisionId, a.IntendedUse, a.Reason, a.AuditType, a.ProjectId, a.AuditProjectId, a.AuditKey, a.AuditName,
            a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy, a.RowVersion, a.Id);
    }
    /// The assessment a notice asked of one target, for the target's inputs list.
    public sealed record InputUseAssessment(string TargetType, Guid TargetId, string Status, Guid ChangeNoticeId, string Key, Guid NewRevisionId, string NoticeStatus);
    /// A page of input uses with the notice assessments and adoption history of the targets on it.
    public sealed record InputUsePage(IReadOnlyList<InputUseRow> Items, int Page, int PageSize, int TotalCount, IReadOnlyList<InputUseAssessment> Assessments, IReadOnlyList<InputAdoptionRow> History);
    /// One notice as the detail panel reads it. CompleteAssessmentIds names the assessments that can close it; the can flags are the viewer's own rights; Head is null when the source has none.
    public sealed partial record ChangeDetail(ChangeNoticeRow Notice, IReadOnlyList<ChangeAssessmentRow> Assessments, IReadOnlyList<Guid> CompleteAssessmentIds, SourceRevisionRow OldRevision, SourceRevisionRow NewRevision,
        SourceHeadRow? Head, Guid ActorId, bool CanWrite, bool CanPublish, bool CanManage, IReadOnlyList<Guid> EligiblePeople, IReadOnlyList<InputUseRow> Uses,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    public sealed record ChangeAssessmentLocation(Guid Id, Guid NoticeId, string Key, string Title, RecordContextView Context);
    public sealed record ReferenceImpactDocument(string Identifier, string Revision, string Kind);
    public sealed record ReferenceImpactRevision(string SourceKey, string ExternalIdentifier, string Revision);
    /// A superseded source reference on a resolved issue, with the owner's and verifier's decisions. A reference or revision that no longer resolves is null.
    public sealed record ReferenceImpactRow(Guid Id, Guid DocumentReferenceId, Guid PreviousRevisionId, Guid CurrentRevisionId, Guid OwnerId, Guid? VerifierId, string Status, string? OwnerDisposition,
        string? OwnerReason, string? VerifierDisposition, string? VerifierReason, DateTimeOffset? OwnerDecidedAt, DateTimeOffset? VerifierDecidedAt, int RowVersion, ReferenceImpactDocument? DocumentReference,
        ReferenceImpactRevision? PreviousRevision, ReferenceImpactRevision? CurrentRevision);
    sealed record PendingCount(Guid Id, int Count);
    sealed record NoticeHandoff(Handoff H, SourceRevision Source);
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/changes", List);
        api.MapGet("/projects/{projectId:guid}/changes/options", Options);
        api.MapGet("/projects/{projectId:guid}/changes/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/changes/{id:guid}", Detail);
        api.MapGet("/projects/{projectId:guid}/change-assessments/{id:guid}", AssessmentDetail);
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
                if (head is not null) { HeadVersion(head, body.HeadRowVersion); Check.That(head.CurrentRevisionId == old, "supersedesId", "change.current_rule"); }
                else Check.That(await Coordination.Published(db, previous), "supersedesId", "change.current_rule");
            } else Check.That(head == null, "supersedesId", "change.explicit_supersedes");
            Check.That(body.SourceCheckedAt is null || body.SourceCheckedAt <= clock.GetUtcNow(), "sourceCheckedAt", "change.future_check");
            var revision = new SourceRevision { ProjectId = p.Id, DeliverableId = body.DeliverableId, SourceRowVersion = sourceVersion, SourceIdentity = identity,
                SourceSystem = system, ExternalIdentifier = external, SourceKey = sourceKey, Title = Check.Required(body.Title, "title"), Revision = Check.Required(body.Revision, "revision", 100),
                Url = Coordination.Url(body.Url), Issuer = Check.Required(body.Issuer, "issuer", 200), Scope = Check.Required(body.Scope, "scope", 2000), SourceCheckedAt = body.SourceCheckedAt, SupersedesId = previous?.Id, AuthorIds = authors };
            revision.IdentityHash = Coordination.Hash(new { identity, revision.Revision, revision.Url, revision.Issuer, revision.Scope, revision.SupersedesId, sourceVersion });
            var snapshot = await db.SourceRevisions.SingleOrDefaultAsync(r => r.ProjectId == p.Id && r.IdentityHash == revision.IdentityHash);
            // Register the receiver's existing immutable handoff snapshot rather than replacing B with another B.
            if (snapshot is null && previous is not null)
                snapshot = await db.SourceRevisions.FirstOrDefaultAsync(r => r.ProjectId == p.Id && r.SourceIdentity == identity
                    && r.SourceRowVersion == sourceVersion && r.Revision == revision.Revision && r.Url == revision.Url
                    && r.Title == revision.Title && r.Issuer == revision.Issuer && r.Scope == revision.Scope && r.SourceCheckedAt == revision.SourceCheckedAt
                    && db.HandoffRevisions.Any(h => h.SourceRevisionId == r.Id && h.PreviousRevisionId != null
                        && db.HandoffRevisions.Any(old => old.Id == h.PreviousRevisionId && old.SourceRevisionId == previous.Id)));
            if (snapshot is not null) {
                Check.That(previous is not null && !await Coordination.Published(db, snapshot)
                    && !await db.ChangeNotices.AnyAsync(c => c.NewRevisionId == snapshot.Id)
                    && await db.HandoffRevisions.AnyAsync(h => h.SourceRevisionId == snapshot.Id), "revision", "change.duplicate_revision");
                revision = snapshot;
            } else db.SourceRevisions.Add(revision);
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
        var documents = await db.IssueDocumentReferences.Where(d => d.ProjectId == p.Id && d.ReplacedById == null && d.Revision == oldRevision.Revision && d.SourceUrl == oldRevision.Url &&
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
            if (head.CurrentRevisionId != c.OldRevisionId) throw ApiException.Conflict("source_changed", "change.current_rule", new { currentRevisionId = head.CurrentRevisionId, currentRowVersion = head.RowVersion });
            if (next.DeliverableId is { } source) {
                var w = await Coordination.Target(db, p, "Deliverable", source);
                Access.Demand(Permissions.PublishSource(access.Actor, ctx, w.DisciplineId, w.OwnerId));
            }
            foreach (var use in await db.InputUses.Where(u => u.ProjectId == p.Id && u.SourceIdentity == head.Identity && u.SourceRevisionId != next.Id).ToListAsync())
                await AddAssessment(db, p, c, use.TargetType, use.TargetId, use.SourceRevisionId, use.Id, null);
            var handoffs = await (from h in db.Handoffs.Where(h => h.ProjectId == p.Id && h.Status != HandoffStatus.Cancelled && h.CurrentRevisionId != null)
                join r in db.HandoffRevisions on h.CurrentRevisionId equals (Guid?)r.Id
                join sr in db.SourceRevisions on r.SourceRevisionId equals sr.Id
                select new NoticeHandoff(h, sr)).ToListAsync();
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
            await SubmissionEndpoints.InvalidateForPublishedRevision(db, p.Id, c.OldRevisionId, notify);
            await Notify(notify, p, c, (await db.ChangeAssessments.Where(a => a.ChangeNoticeId == c.Id).Select(a => a.OwnerId).ToListAsync())
                .Concat(issueImpactRecipients).Distinct()); return c;
        });
    static Task<Coordination.Result> Adopt(Guid projectId, AdoptBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "input.adopt", body }, access, db, clock, async (p, ctx) => {
            var w = await Coordination.Target(db, p, body.TargetType, body.TargetId); Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, w.OwnerId));
            if (w.RowVersion != body.TargetRowVersion) throw ApiException.Conflict("target_changed", "coord.stale", new { currentRowVersion = w.RowVersion });
            Check.That(w.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "targetId", "review.issued");
            var r = await Coordination.Revision(db, p.Id, body.SourceRevisionId); var head = await Coordination.Head(db, r);
            if (head == null || head.CurrentRevisionId != body.ExpectedCurrentRevisionId) throw ApiException.Conflict("source_changed", "change.current_rule");
            Check.That(head.CurrentRevisionId == r.Id, "sourceRevisionId", "change.retain_via_assessment");
            var use = await db.InputUses.SingleOrDefaultAsync(u => u.ProjectId == p.Id && u.TargetType == w.Type && u.TargetId == w.Id && u.SourceIdentity == head.Identity);
            if (use != null) Coordination.Version(use, body.InputUseRowVersion);
            Check.That(!await db.ChangeAssessments.AnyAsync(a => a.ProjectId == p.Id && a.TargetType == w.Type && a.TargetId == w.Id && db.ChangeNotices.Any(c => c.Id == a.ChangeNoticeId && c.Status == ChangeStatus.Open && c.NewRevisionId == r.Id)), "sourceRevisionId", "change.adopt_via_assessment");
            return await Coordination.Adopt(db, p, w, r, body.IntendedUse, Check.Reason(body.Reason), access.Me.Id, clock.GetUtcNow());
        });
    internal static IQueryable<ChangeAssessment> Incomplete(HubDb db) => db.ChangeAssessments.Where(a =>
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
                Check.That(ChangeRules.CorrectionReady(task?.Status, ReviewRules.Independent(reviewer, new[] { a.OwnerId, task?.OwnerId ?? Guid.Empty, task?.AuthorId ?? Guid.Empty }, self), !string.IsNullOrWhiteSpace(a.EvidenceUrl)), "correctionTaskId", "change.correction_rule");
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
                    if (head == null || head.CurrentRevisionId != body.ExpectedCurrentRevisionId || head.CurrentRevisionId != revision.Id) throw ApiException.Conflict("source_changed", "change.current_rule");
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
    public static async Task<Ok<CoordinationOptions>> Options(Guid projectId, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var org = await store.Get(db);
        await using var noticeSnapshot = org.IsFlagOn("capacity_screens") ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var (p, ctx) = await access.Project(projectId, false);
        var sources = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == p.Id).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var heads = await db.SourceHeads.AsNoTracking().Where(h => h.ProjectId == p.Id).ToListAsync();
        var published = new HashSet<Guid>();
        foreach (var r in sources) if (await Coordination.Published(db, r)) published.Add(r.Id);
        var disciplines = await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.IsActive).Select(d => d.Id).ToListAsync();
        var managed = disciplines.Where(d => Permissions.ManageCoordination(access.Actor, ctx, d).Ok).ToHashSet();
        var ownedDeliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == p.Id && d.OwnerId == access.Actor.Id && d.Status != DeliverableStatus.Cancelled).Select(d => d.ProjectDisciplineId).ToListAsync();
        bool CanManage(Guid discipline) => managed.Contains(discipline);
        bool CanCommit(Guid? owner, Guid discipline) => owner is { } id && Permissions.NamedCoordinationAction(access.Actor, ctx, id).Ok || CanManage(discipline);
        var people = await Coordination.People(db, p).OrderBy(u => u.DisplayName).Select(u => new CoordinationPerson(u.Id, u.DisplayName, null)).ToListAsync();
        var notices = await PersonFacts.ReadNotices(db, people.Select(person => person.Id).ToArray(), org, clock);
        if (notices.Count > 0) people = people.Select(person => person with { PersonNotices = notices.GetValueOrDefault(person.Id) }).ToList();
        return TypedResults.Ok(new CoordinationOptions(access.Me.Id, Permissions.CoordinationWrite(access.Actor, ctx).Ok,
            managed.ToArray(),
            new CoordinationCanCreate(managed.Count > 0 || ownedDeliverables.Any(d => Permissions.PublishSource(access.Actor, ctx, d, access.Actor.Id).Ok) || heads.Any(h => Permissions.PublishSource(access.Actor, ctx, h.ProjectDisciplineId, h.OwnerId).Ok), managed.Count > 0),
            people,
            await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.IsActive).Select(d => new CoordinationDiscipline(d.Id, d.Discipline!.Name)).ToListAsync(),
            sources.Select(r => new CoordinationSource(SourceRevisionRow.From(r), Coordination.Identity(r), published.Contains(r.Id), !heads.Any(h => h.Identity == Coordination.Identity(r)) || heads.Any(h => h.CurrentRevisionId == r.Id))).ToList(),
            heads.Select(SourceHeadRow.From).ToList(),
            (await db.Deliverables.Where(d => d.ProjectId == p.Id && d.Status != DeliverableStatus.Cancelled).Select(d => new CoordinationDeliverable(d.Id, d.Key, d.Name, d.ProjectDisciplineId, d.OwnerId, d.RowVersion, d.Revision, d.TransmittalUrl, d.Status, false, false)).ToListAsync())
                .Select(d => d with { CanCommit = CanCommit(d.OwnerId, d.ProjectDisciplineId), CanManage = CanManage(d.ProjectDisciplineId) }).ToList(),
            (await db.Tasks.Where(t => t.ProjectId == p.Id && t.Status != TaskStatuses.Cancelled).Select(t => new CoordinationTask(t.Id, t.Key, t.Name, t.ProjectDisciplineId, t.AssigneeId, t.RowVersion, t.Status, false, false)).ToListAsync())
                .Select(t => t with { CanCommit = CanCommit(t.OwnerId, t.ProjectDisciplineId), CanManage = CanManage(t.ProjectDisciplineId) }).ToList()));
    }
    static async Task<Ok<InputUsePage>> Uses(Guid projectId, string? targetType, Guid? targetId, int? page, int? pageSize, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        var q = db.InputUses.AsNoTracking().Where(u => u.ProjectId == projectId);
        if (targetType != null) q = q.Where(u => u.TargetType == targetType); if (targetId != null) q = q.Where(u => u.TargetId == targetId);
        var (pg, size) = Http.Paging(page, pageSize); var total = await q.CountAsync();
        var uses = await q.OrderByDescending(u => u.AdoptedAt).ThenBy(u => u.Id).Skip((pg - 1) * size).Take(size).ToListAsync(); var ids = uses.Select(u => u.Id).ToArray();
        var targetIds = uses.Select(u => u.TargetId).ToArray();
        return TypedResults.Ok(new InputUsePage(uses.Select(InputUseRow.From).ToList(), pg, size, total,
            await db.ChangeAssessments.Where(a => a.ProjectId == projectId && targetIds.Contains(a.TargetId)).Join(db.ChangeNotices, a => a.ChangeNoticeId, c => c.Id, (a, c) => new InputUseAssessment(a.TargetType, a.TargetId, a.Status, a.ChangeNoticeId, c.Key, c.NewRevisionId, c.Status)).ToListAsync(),
            (await db.InputAdoptions.Where(a => ids.Contains(a.InputUseId)).OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).ToListAsync()).Select(InputAdoptionRow.From).ToList()));
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
    static async Task<List<ChangeRow>> Rows(HubDb db, IQueryable<ChangeNotice> query)
    {
        var rows = await query.ToListAsync(); var ids = rows.Select(c => c.Id).ToArray();
        var counts = await Incomplete(db).Where(a => ids.Contains(a.ChangeNoticeId)).GroupBy(a => a.ChangeNoticeId).Select(g => new PendingCount(g.Key, g.Count())).ToDictionaryAsync(x => x.Id, x => x.Count);
        return rows.Select(c => new ChangeRow(c.Id, c.Key, c.Title, c.Status, c.RowVersion, c.OwnerId, c.Scope, c.AssessmentDueDate, counts.GetValueOrDefault(c.Id))).ToList();
    }
    static async Task<Ok<Page<ChangeRow>>> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db)
    { await access.Project(projectId, false); var q = Query(db, projectId, filter, access.Me.Id); var (pg, size) = Http.Paging(page, pageSize); return TypedResults.Ok(new Page<ChangeRow>(await Rows(db, q.Skip((pg - 1) * size).Take(size)), pg, size, await q.CountAsync())); }
    static async Task<Ok<ChangeDetail>> Detail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("work_screens")
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var (p, ctx) = await access.Project(projectId, false); var c = await Load(db, p.Id, id);
        var assessments = await db.ChangeAssessments.Where(a => a.ChangeNoticeId == id).ToListAsync(); var newRevision = await Coordination.Revision(db, p.Id, c.NewRevisionId);
        var incomplete = await Incomplete(db).Where(a => a.ChangeNoticeId == id).Select(a => a.Id).ToListAsync();
        var complete = assessments.Where(a => !incomplete.Contains(a.Id)).Select(a => a.Id).ToList();
        var useIds = assessments.Select(a => a.InputUseId).OfType<Guid>().ToArray();
        var oldRevision = await Coordination.Revision(db, p.Id, c.OldRevisionId); var head = await Coordination.Head(db, newRevision);
        var reader = new RecordContextReader(db, p, settings);
        reader.SeedChange(c, assessments, complete, newRevision, head);
        var context = await reader.Read(RecordContextKind.ChangeNotice, c.Id);
        return TypedResults.Ok(await WorkScreenResponseMetadata.AttachDetailAsync(new ChangeDetail(ChangeNoticeRow.From(c), assessments.Select(ChangeAssessmentRow.From).ToList(), complete, SourceRevisionRow.From(oldRevision), SourceRevisionRow.From(newRevision), head is null ? null : SourceHeadRow.From(head),
            access.Me.Id, Permissions.CoordinationWrite(access.Actor, ctx).Ok,
            Permissions.PublishSource(access.Actor, ctx, c.ProjectDisciplineId, newRevision.DeliverableId is { } did ? await db.Deliverables.Where(x => x.Id == did).Select(x => x.OwnerId).FirstOrDefaultAsync() : head?.OwnerId).Ok,
            Permissions.ManageCoordination(access.Actor, ctx, c.ProjectDisciplineId).Ok, await Coordination.People(db, p).Select(u => u.Id).ToListAsync(),
            (await db.InputUses.Where(u => u.ProjectId == p.Id && useIds.Contains(u.Id)).ToListAsync()).Select(InputUseRow.From).ToList(), context), c, access, db, settings, clock));
    }
    static async Task<Ok<ChangeAssessmentLocation>> AssessmentDetail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store)
    {
        var (project, _) = await access.Project(projectId, false);
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("screen_patterns")) throw ApiException.NotFound();
        var assessment = await db.ChangeAssessments.AsNoTracking().SingleOrDefaultAsync(a => a.ProjectId == projectId && a.Id == id) ?? throw ApiException.NotFound();
        // The visible, nondeleted parent is part of child authorization.
        await Load(db, projectId, assessment.ChangeNoticeId);
        var reader = new RecordContextReader(db, project, settings);
        var context = await reader.Read(RecordContextKind.ChangeAssessment, id) ?? throw ApiException.NotFound();
        var identity = reader.Identity(RecordContextKind.ChangeAssessment, id);
        return TypedResults.Ok(new ChangeAssessmentLocation(id, assessment.ChangeNoticeId, identity.Key, identity.Title, context));
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (p, _) = await access.Project(projectId, false); var rows = await Rows(db, Query(db, projectId, filter, access.Me.Id).Take(Export.MaxRows + 1));
        return await ExportFile.Send(db, store, format, Text.Get("export.changes", p.ProjectNumber), Columns, JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), p.Id, $"{p.ProjectNumber}-changes", clock);
    }
    static async Task<Ok<List<ReferenceImpactRow>>> ListIssueImpacts(Guid id, Access access, HubDb db)
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
        return TypedResults.Ok(impacts.Select(a => new ReferenceImpactRow(a.Id, a.DocumentReferenceId, a.PreviousRevisionId, a.CurrentRevisionId, a.OwnerId, a.VerifierId, a.Status,
            a.OwnerDisposition, a.OwnerReason, a.VerifierDisposition, a.VerifierReason, a.OwnerDecidedAt, a.VerifierDecidedAt, a.RowVersion,
            documents.TryGetValue(a.DocumentReferenceId, out var d) ? new ReferenceImpactDocument(d.Identifier, d.Revision, d.Kind) : null,
            revisions.TryGetValue(a.PreviousRevisionId, out var old) ? new ReferenceImpactRevision(old.SourceKey, old.ExternalIdentifier, old.Revision) : null,
            revisions.TryGetValue(a.CurrentRevisionId, out var current) ? new ReferenceImpactRevision(current.SourceKey, current.ExternalIdentifier, current.Revision) : null)).ToList());
    }
    static async Task<Coordination.Result> DecideIssueImpact(Guid id, Guid impactId, IssueImpactBody body, Access access, HubDb db, TimeProvider clock)
    {
        var (_, project, _) = await RegisterEndpoints.LoadIssue(db, access, id);
        return await Coordination.Run(project.Id, body.RequestId, new { operation = "issue.reference_impact", id, impactId, body }, access, db, clock, async (p, ctx) => {
            var issue = await db.Issues.SingleOrDefaultAsync(i => i.ProjectId == p.Id && i.Id == id) ?? throw ApiException.NotFound();
            var impact = await db.IssueReferenceImpactAssessments.SingleOrDefaultAsync(a => a.ProjectId == p.Id && a.IssueId == id && a.Id == impactId)
                ?? throw ApiException.NotFound();
            Coordination.Version(impact, body.RowVersion);
            Check.That(impact.Status == IssueReferenceImpactStatus.Pending, "status", "issue.reference_impact_pending");
            Check.OneOf(body.Disposition, IssueReferenceImpactDisposition.All, "disposition");
            var reason = Check.Reason(body.Reason);
            var now = clock.GetUtcNow();
            // The named owner and verifier decide only while they are on the team (a leaver keeps project read access).
            if (access.Me.Id == impact.OwnerId && issue.OwnerId == access.Me.Id && ctx.IsMember)
            {
                Check.That(impact.OwnerDisposition is null, "disposition", "issue.reference_impact_decided");
                impact.OwnerDisposition = body.Disposition; impact.OwnerReason = reason; impact.OwnerDecidedBy = access.Me.Id; impact.OwnerDecidedAt = now;
            }
            else if (impact.VerifierId == access.Me.Id && issue.OwnerId != access.Me.Id && ctx.IsMember)
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
