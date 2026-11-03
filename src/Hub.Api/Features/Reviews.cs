using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ReviewEndpoints
{
    public sealed record Assignment(Guid ProjectDisciplineId, Guid ReviewerId, DateOnly DueDate);
    public sealed record CreateBody(Guid RequestId, string Title, string Purpose, Guid ProjectDisciplineId, Guid CoordinatorId, Guid[] SourceRevisionIds, Assignment[] Assignments, bool RequiredForIssue, string? Reason,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid[]? IssuedRevisionIds = null);
    public sealed record RoundBody(Guid RequestId, int RowVersion, string Purpose, Guid[] SourceRevisionIds, Assignment[] Assignments, string Reason, string? RemovalImpact,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid[]? IssuedRevisionIds = null);
    public sealed record ActionBody(Guid RequestId, int RowVersion, string Action, string? Reason);
    public sealed record DecisionBody(Guid RequestId, int RowVersion, string Status, string Rationale);
    public sealed record ReassignBody(Guid RequestId, int RowVersion, Guid OwnerId, string Reason);
    public sealed record FindingBody(Guid RequestId, int RowVersion, Guid SourceRevisionId, Guid ProjectDisciplineId, Guid ResolverId, string Text, string Severity, Guid? IssueId = null);
    public sealed record FindingAction(Guid RequestId, int RowVersion, string Action, string Reason, string? EvidenceUrl, Guid? OwnerId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResponseOutcome = null);
    public sealed record Filter(string? Q, string? Status, Guid? OwnerId, Guid? DisciplineId, bool? Mine);
    static readonly Col[] Columns = Hub.Api.Records.ReviewPackageRecord.ReviewsColumns;

    /// One package of the register. OutstandingDisciplines counts the current round's reviews not yet approved, BlockingFindings its open blocking findings, and WaitingDays the whole days since the round started while it waits on reviewers.
    public sealed record ReviewRow(Guid Id, string Key, string Title, string Status, Guid CoordinatorId, int RoundNumber, int RowVersion, int OutstandingDisciplines, int BlockingFindings, int WaitingDays);
    /// An issue linked from a finding of a current round, with its page of the list.
    public sealed record LinkedIssuePage(IReadOnlyList<DisciplineCoordinationEndpoints.CoordinationIssueRow> Items, int TotalCount, int Page, int PageSize);
    sealed record Projection(ReviewPackage P, int OutstandingDisciplines, int BlockingFindings, DateTimeOffset? Started);

    // The rows below mirror the stored rows, audit fields included, in the order each row has always been written (the OrgStructure `*Response` precedent).
    public sealed record ReviewPackageRow(string Title, string Purpose, Guid CoordinatorId, Guid ProjectDisciplineId, string Status, Guid? CurrentRoundId, int RoundNumber, bool RequiredForIssue,
        string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName, Guid? AuditDisciplineId, Guid ProjectId, string Key, int Seq, DateTimeOffset? DeletedAt, Guid? DeletedBy,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static ReviewPackageRow From(ReviewPackage p) => new(p.Title, p.Purpose, p.CoordinatorId, p.ProjectDisciplineId, p.Status, p.CurrentRoundId, p.RoundNumber, p.RequiredForIssue,
            p.AuditType, p.AuditProjectId, p.AuditKey, p.AuditName, p.AuditDisciplineId, p.ProjectId, p.Key, p.Seq, p.DeletedAt, p.DeletedBy, p.CreatedAt, p.CreatedBy, p.UpdatedAt, p.UpdatedBy, p.RowVersion, p.Id);
    }
    public sealed record ReviewRoundRow(Guid PackageId, int Number, string Purpose, string? Reason, string? RemovalImpact, DateTimeOffset? StartedAt, string Status, string AuditType, Guid ProjectId,
        Guid? AuditProjectId, string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static ReviewRoundRow From(ReviewRound r) => new(r.PackageId, r.Number, r.Purpose, r.Reason, r.RemovalImpact, r.StartedAt, r.Status, r.AuditType, r.ProjectId,
            r.AuditProjectId, r.AuditKey, r.AuditName, r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, r.RowVersion, r.Id);
    }
    /// One discipline's review of a round: who reviews it, by when, and what they decided.
    public sealed record ReviewAssignmentRow(Guid RoundId, Guid ProjectDisciplineId, Guid ReviewerId, DateOnly DueDate, string Status, string? Rationale, DateTimeOffset? DecidedAt, Guid? DecidedBy,
        string AuditType, Guid ProjectId, Guid? AuditProjectId, string? AuditKey, string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
    {
        public static ReviewAssignmentRow From(DisciplineReview a) => new(a.RoundId, a.ProjectDisciplineId, a.ReviewerId, a.DueDate, a.Status, a.Rationale, a.DecidedAt, a.DecidedBy,
            a.AuditType, a.ProjectId, a.AuditProjectId, a.AuditKey, a.AuditName, a.CreatedAt, a.CreatedBy, a.UpdatedAt, a.UpdatedBy, a.RowVersion, a.Id);
    }
    public sealed record ReviewFindingRow(Guid PackageId, Guid RoundId, Guid? CarriedFromId, Guid? IssueId, Guid? SourceRevisionId, Guid ProjectDisciplineId, Guid? OriginatorId, Guid ResolverId,
        Guid? VerifierId, string Text, string Severity, string Status, string? Response, string? EvidenceUrl, Guid? WithdrawalAcknowledgedBy, string AuditType, string? AuditName, Guid ProjectId,
        Guid? AuditProjectId, string? AuditKey, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResponseOutcome = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? DeliverableIssueId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? ExternalReviewId = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewRegisterOriginator? Originator = null)
    {
        public static ReviewFindingRow From(ReviewFinding f, bool registerScreens = false) => new(f.PackageId, f.RoundId, f.CarriedFromId, f.IssueId, f.SourceRevisionId, f.ProjectDisciplineId, f.OriginatorId, f.ResolverId,
            f.VerifierId, f.Text, f.Severity, f.Status, f.Response, f.EvidenceUrl, f.WithdrawalAcknowledgedBy, f.AuditType, f.AuditName, f.ProjectId,
            f.AuditProjectId, f.AuditKey, f.CreatedAt, f.CreatedBy, f.UpdatedAt, f.UpdatedBy, f.RowVersion, f.Id, ResponseOutcome: registerScreens ? f.ResponseOutcome : null,
            DeliverableIssueId: registerScreens ? f.DeliverableIssueId : null, ExternalReviewId: registerScreens ? f.ExternalReviewId : null);
    }
    /// One step of a finding's history.
    public sealed record ReviewFindingEventRow(Guid FindingId, string Action, string Reason, string? EvidenceUrl, string AuditType, Guid ProjectId, Guid? AuditProjectId, string? AuditKey,
        string? AuditName, DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResponseOutcome = null)
    {
        public static ReviewFindingEventRow From(FindingEvent e, bool registerScreens = false) => new(e.FindingId, e.Action, e.Reason, e.EvidenceUrl, e.AuditType, e.ProjectId, e.AuditProjectId, e.AuditKey,
            e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id, registerScreens ? e.ResponseOutcome : null);
    }
    /// A source revision a round reviews, with the authors captured when the round was built.
    public sealed record ReviewManifestRow(Guid Id, Guid RoundId, Guid DeliverableId, Guid? SourceRevisionId, Guid[] AuthorIds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ChangeEndpoints.SourceRevisionRow? Source,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewIssuedSourceRead? IssuedSource = null);
    /// One package as the detail panel reads it, with every round, assignment, finding and event. The can flags are the viewer's own rights.
    public sealed record ReviewDetail(ReviewPackageRow Package, IReadOnlyList<ReviewRoundRow> Rounds, IReadOnlyList<ReviewAssignmentRow> Assignments, IReadOnlyList<ReviewFindingRow> Findings,
        IReadOnlyList<ReviewManifestRow> Manifest, IReadOnlyList<ReviewFindingEventRow> Events, IReadOnlyList<Guid> EligiblePeople, Guid ActorId, bool CanCoordinate, bool CanManage,
        bool CanWrite, bool CanReassignVerifier,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReviewRoundPresentation>? Presentation = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ExternalReviewRead>? ExternalReviews = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? CanRecordExternalReview = null);
    public sealed record ReviewSourcePerformer(string TaskKey, RecordContextPerson Person);
    public sealed record ReviewSourcePerformers(Guid SourceRevisionId, string SourceKey, bool Available, IReadOnlyList<ReviewSourcePerformer> Performers);
    public sealed record ReviewAssignmentGuidance(bool AllowSelfReview, bool SourcesAvailable, Guid[] AuthorIds, Guid[] ExcludedReviewerIds,
        IReadOnlyList<ReviewSourcePerformers> Sources);
    public sealed record ReviewAssignmentPerson(Guid AssignmentId, RecordContextPerson Reviewer);
    public sealed record ReviewRoundPresentation(Guid RoundId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] ReviewAssignmentGuidance? Guidance, Guid[] MissingReviewerAssignmentIds,
        IReadOnlyList<ReviewAssignmentPerson> Reviewers, bool StartAllowed, string? StartReason,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewIssuedAssignmentGuidance? IssuedGuidance = null);

    public sealed record ReviewFindingLocation(Guid Id, Guid PackageId, Guid RoundId, string Key, string Title,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/review-assignment-guidance", AssignmentGuidance);
        api.MapGet("/projects/{projectId:guid}/reviews", List);
        api.MapGet("/projects/{projectId:guid}/reviews/options", Options);
        api.MapGet("/projects/{projectId:guid}/reviews/linked-issues", LinkedIssues);
        api.MapGet("/projects/{projectId:guid}/reviews/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}", Detail);
        api.MapGet("/projects/{projectId:guid}/review-findings/{id:guid}", FindingDetail);
        api.MapPost("/projects/{projectId:guid}/reviews", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/rounds", NewRound).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/action", Action).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assign", AssignCoordinator).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assignments/{assignmentId:guid}/decision", Decide).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assignments/{assignmentId:guid}/assign", AssignReviewer).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/findings", AddFinding).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/findings/{findingId:guid}/action", FindingCommand).WithMetadata(new Coordination.AtomicCommand());
    }
    internal static async Task<ReviewPackage> Load(HubDb db, Guid project, Guid id) =>
        await ReviewIssuedSources.VisiblePackages(db, (await db.CurrentSettings()).IsFlagOn(DeliverableEndpoints.RegisterFlag))
            .FirstOrDefaultAsync(x => x.ProjectId == project && x.Id == id) ?? throw ApiException.NotFound();
    internal static void Live(ReviewPackage p) => Check.That(p.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded), "status", "review.frozen");
    static async Task<ReviewRound> Round(HubDb db, ReviewPackage p) => await db.ReviewRounds.SingleAsync(r => r.Id == p.CurrentRoundId);
    internal static async Task<Guid[]> Authors(HubDb db, Guid round)
    {
        var manifest = await db.ReviewManifestItems.Where(m => m.RoundId == round).ToListAsync();
        var issueIds = manifest.Select(item => item.DeliverableIssueId).OfType<Guid>().Distinct().ToArray();
        var parent = issueIds.Length > 0 ? await db.ReviewRounds.AsNoTracking().SingleAsync(value => value.Id == round) : null;
        var issuedSources = parent is null ? new Dictionary<Guid, ReviewIssuedSources.Source>() : await ReviewIssuedSources.ReadMany(db, parent.ProjectId, issueIds);
        var authors = new HashSet<Guid>();
        foreach (var item in manifest)
        {
            if (item.DeliverableIssueId is { } issueId)
            {
                var source = ReviewIssuedSources.Require(issuedSources[issueId]);
                Check.That(item.ProjectId == parent!.ProjectId && item.SourceRevisionId is null && source.Deliverable.Id == item.DeliverableId
                    && item.AuthorIds.Order().SequenceEqual(source.Authors!.Order()), "deliverableIssueId", "externalReview.integrity");
                authors.UnionWith(source.Authors!);
            }
            else
            {
                Check.That(item.SourceRevisionId is not null, "sourceRevisionId", "coord.reference");
                authors.UnionWith(item.AuthorIds); authors.UnionWith(await Coordination.Authors(db, item.DeliverableId));
            }
        }
        return authors.ToArray();
    }
    internal static async Task<Guid[]> FindingAuthors(HubDb db, ReviewFinding finding)
    {
        var ids = new HashSet<Guid>(); var seen = new HashSet<Guid>(); ReviewFinding? cursor = finding;
        if (finding.ExternalReviewId is not null)
        {
            var chain = new List<ReviewFinding>(); var limit = Math.Max(1, (await db.CurrentSettings()).ChainDepthLimit);
            while (cursor is not null)
            {
                Check.That(seen.Add(cursor.Id) && seen.Count <= limit && cursor.ProjectId == finding.ProjectId && cursor.PackageId == finding.PackageId
                    && cursor.ExternalReviewId == finding.ExternalReviewId, "findingId", "externalReview.integrity");
                chain.Add(cursor); ids.Add(cursor.ResolverId);
                cursor = cursor.CarriedFromId is { } prior ? await db.ReviewFindings.AsNoTracking().SingleOrDefaultAsync(value => value.Id == prior)
                    ?? throw ApiException.Invalid("findingId", "externalReview.integrity") : null;
            }
            var roundIds = chain.Select(value => value.RoundId).Distinct().ToArray();
            var rounds = await db.ReviewRounds.AsNoTracking().Where(value => roundIds.Contains(value.Id) && value.PackageId == finding.PackageId && value.ProjectId == finding.ProjectId).ToDictionaryAsync(value => value.Id);
            Check.That(rounds.Count == roundIds.Length && chain.Zip(chain.Skip(1), (current, previous) => rounds[current.RoundId].Number > rounds[previous.RoundId].Number).All(valid => valid), "findingId", "externalReview.integrity");
            var manifest = await db.ReviewManifestItems.AsNoTracking().Where(item => roundIds.Contains(item.RoundId)).ToArrayAsync();
            Check.That(roundIds.All(round => manifest.Any(item => item.RoundId == round)) && manifest.All(item => item.ProjectId == finding.ProjectId
                && item.SourceRevisionId is null && item.DeliverableIssueId is not null), "deliverableIssueId", "externalReview.integrity");
            var sources = await ReviewIssuedSources.ReadMany(db, finding.ProjectId, manifest.Select(item => item.DeliverableIssueId!.Value).Distinct().ToArray());
            foreach (var item in manifest)
            {
                var source = ReviewIssuedSources.Require(sources[item.DeliverableIssueId!.Value]);
                Check.That(item.DeliverableId == source.Deliverable.Id && item.AuthorIds.Order().SequenceEqual(source.Authors!.Order()), "deliverableIssueId", "externalReview.integrity");
                ids.UnionWith(source.Authors!);
            }
            var findingIds = chain.Select(value => value.Id).ToArray();
            var responses = await db.FindingEvents.AsNoTracking().Where(value => findingIds.Contains(value.FindingId) && value.Action == FindingStatus.Responded).ToArrayAsync();
            Check.That(responses.All(value => value.ProjectId == finding.ProjectId), "findingId", "externalReview.integrity");
            ids.UnionWith(responses.Select(value => value.CreatedBy).OfType<Guid>());
            return ids.ToArray();
        }
        while (cursor != null) {
            if (!seen.Add(cursor.Id)) break;
            ids.UnionWith(await Authors(db, cursor.RoundId)); ids.Add(cursor.ResolverId);
            ids.UnionWith((await db.FindingEvents.Where(e => e.FindingId == cursor.Id && e.Action == FindingStatus.Responded).Select(e => e.CreatedBy).ToListAsync()).OfType<Guid>());
            cursor = cursor.CarriedFromId is { } prior ? await db.ReviewFindings.SingleAsync(f => f.Id == prior) : null;
        }
        return ids.ToArray();
    }
    static async Task Independent(HubDb db, Project project, Guid reviewer, Guid[] authors, bool allowSelf)
    { await Coordination.Person(db, project, reviewer, "reviewerId"); Check.That(ReviewRules.Independent(reviewer, authors, allowSelf), "reviewerId", "review.independent"); }
    internal static async Task ValidateRound(HubDb db, Project project, ReviewPackage package, bool allowSelf, bool lockRows = true)
    {
        await Coordination.Person(db, project, package.CoordinatorId, "coordinatorId");
        var items = await db.ReviewManifestItems.Where(m => m.RoundId == package.CurrentRoundId).ToListAsync();
        Check.That(items.Count > 0, "sourceRevisionIds", "error.required");
        var issuedSources = await ReviewIssuedSources.ReadMany(db, project.Id, items.Select(item => item.DeliverableIssueId).OfType<Guid>().Distinct().ToArray());
        foreach (var item in items) {
            if (item.DeliverableIssueId is { } issueId)
            {
                var issued = ReviewIssuedSources.Require(issuedSources[issueId]);
                Check.That(item.ProjectId == project.Id && item.SourceRevisionId is null && issued.Deliverable.Id == item.DeliverableId,
                    "deliverableIssueId", "externalReview.integrity");
                continue;
            }
            await Coordination.Target(db, project, "Deliverable", item.DeliverableId, lockRow: lockRows);
            var revision = await Coordination.Revision(db, project.Id, item.SourceRevisionId ?? throw ApiException.Invalid("sourceRevisionId", "coord.reference"));
            Check.That(await Coordination.Published(db, revision), "sourceRevisionId", "review.source_unpublished");
            var head = await Coordination.Head(db, revision);
            Check.That(head is null || head.CurrentRevisionId == revision.Id, "sourceRevisionId", "review.source_changed");
        }
        var authors = await Authors(db, package.CurrentRoundId!.Value);
        if (issuedSources.Count > 0)
            await ExternalReviewEndpoints.KnownRoundFindings(db, project, package.Id, package.CurrentRoundId.Value);
        foreach (var a in await db.DisciplineReviews.Where(a => a.RoundId == package.CurrentRoundId).ToListAsync()) {
            await Coordination.Discipline(db, project.Id, a.ProjectDisciplineId);
            await Independent(db, project, a.ReviewerId, authors, allowSelf);
        }
    }
    static async Task<ReviewRound> BuildRound(HubDb db, Project project, ReviewPackage p, string purpose, Guid[] sources, Assignment[] assignments, string? reason, string? impact, bool allowSelf, Guid[]? issuedIds = null)
    {
        var selected = issuedIds ?? sources;
        Check.That(selected is { Length: > 0 and <= 200 } && selected.Distinct().Count() == selected.Length, issuedIds is null ? "sourceRevisionIds" : "issuedRevisionIds", "review.manifest_rule");
        if (issuedIds is not null) Check.That(sources is { Length: 0 } && !p.RequiredForIssue, "issuedRevisionIds", "externalReview.sourceMode");
        Check.That(assignments is { Length: > 0 and <= 50 } && assignments.Select(a => a.ProjectDisciplineId).Distinct().Count() == assignments.Length, "assignments", "review.assignments");
        var round = new ReviewRound { ProjectId = project.Id, PackageId = p.Id, Number = p.RoundNumber + 1, Purpose = Check.Required(purpose, "purpose", 2000), Reason = reason, RemovalImpact = impact };
        db.ReviewRounds.Add(round);
        var authors = new HashSet<Guid>(); var deliverables = new HashSet<Guid>();
        var issuedSources = issuedIds is null ? new Dictionary<Guid, ReviewIssuedSources.Source>() : await ReviewIssuedSources.ReadMany(db, project.Id, issuedIds);
        foreach (var rid in selected!) {
            if (issuedIds is not null)
            {
                var issued = ReviewIssuedSources.Require(issuedSources[rid]);
                Check.That(deliverables.Add(issued.Deliverable.Id), "issuedRevisionIds", "review.manifest_rule");
                authors.UnionWith(issued.Authors!);
                db.ReviewManifestItems.Add(new ReviewManifestItem { ProjectId = project.Id, RoundId = round.Id,
                    DeliverableId = issued.Deliverable.Id, SourceRevisionId = null, DeliverableIssueId = rid, AuthorIds = issued.Authors! });
                continue;
            }
            var r = await Coordination.Revision(db, project.Id, rid);
            Check.That(r.DeliverableId is not null && deliverables.Add(r.DeliverableId.Value), "sourceRevisionIds", "review.manifest_rule");
            var work = await Coordination.Target(db, project, "Deliverable", r.DeliverableId!.Value);
            Check.That(await Coordination.Published(db, r), "sourceRevisionIds", "review.source_unpublished");
            var head = await Coordination.Head(db, r);
            Check.That(head is null || head.CurrentRevisionId == r.Id, "sourceRevisionIds", "review.source_changed");
            var captured = r.AuthorIds.Concat(await Coordination.Authors(db, work.Id)).Distinct().ToArray(); authors.UnionWith(captured);
            db.ReviewManifestItems.Add(new ReviewManifestItem { ProjectId = project.Id, RoundId = round.Id, DeliverableId = work.Id, SourceRevisionId = r.Id, AuthorIds = captured });
        }
        foreach (var a in assignments!) {
            await Coordination.Discipline(db, project.Id, a.ProjectDisciplineId);
            await Independent(db, project, a.ReviewerId, authors.ToArray(), allowSelf);
            Check.That(a.DueDate != default, "dueDate", "error.required");
            db.DisciplineReviews.Add(new DisciplineReview { ProjectId = project.Id, RoundId = round.Id, ProjectDisciplineId = a.ProjectDisciplineId, ReviewerId = a.ReviewerId, DueDate = a.DueDate });
        }
        return round;
    }
    internal static async Task Notify(HubDb db, Notifier notify, Project project, ReviewPackage p, IEnumerable<Guid> owners) =>
        await notify.Send(NotificationEvents.ReviewPackageChanged, owners.Select(x => (Guid?)x), new NotifyItem(project.Id, "ReviewPackage", p.Id, p.Key, $"/projects/{project.ProjectNumber}/reviews?panel=ReviewPackage:{p.Id}", project.ProjectNumber), Text.Get("review.notification", p.Key, p.Title, p.Status));

    // A published replacement changes the reviewed content. Start a fresh round in the
    // publication transaction so no current approval can continue to refer to the old head.
    public static async Task AdvanceForPublishedRevision(HubDb db, Notifier notify, Project project, SourceRevision oldRevision, SourceRevision newRevision)
    {
        var affected = await db.ReviewManifestItems.Where(m => m.ProjectId == project.Id && m.SourceRevisionId == oldRevision.Id)
            .Join(db.ReviewPackages, m => m.RoundId, p => p.CurrentRoundId, (m, p) => new { m, p })
            .Where(x => x.p.Status != ReviewStatus.Cancelled && x.p.Status != ReviewStatus.Superseded).ToListAsync();
        foreach (var affectedRound in affected)
        {
            var package = affectedRound.p;
            var oldRound = await db.ReviewRounds.SingleAsync(r => r.Id == package.CurrentRoundId);
            var round = new ReviewRound { ProjectId = project.Id, PackageId = package.Id, Number = package.RoundNumber + 1,
                Purpose = oldRound.Purpose, Reason = $"Source revision {oldRevision.Revision} replaced by {newRevision.Revision}" };
            db.ReviewRounds.Add(round);
            foreach (var item in await db.ReviewManifestItems.Where(m => m.RoundId == oldRound.Id).ToListAsync())
            {
                var changed = item.SourceRevisionId == oldRevision.Id;
                db.ReviewManifestItems.Add(new ReviewManifestItem { ProjectId = project.Id, RoundId = round.Id,
                    DeliverableId = item.DeliverableId, SourceRevisionId = changed ? newRevision.Id : item.SourceRevisionId,
                    AuthorIds = changed ? newRevision.AuthorIds.Concat(await Coordination.Authors(db, item.DeliverableId)).Distinct().ToArray() : item.AuthorIds });
            }
            var reviewers = await db.DisciplineReviews.Where(a => a.RoundId == oldRound.Id).ToListAsync();
            foreach (var assignment in reviewers)
                db.DisciplineReviews.Add(new DisciplineReview { ProjectId = project.Id, RoundId = round.Id,
                    ProjectDisciplineId = assignment.ProjectDisciplineId, ReviewerId = assignment.ReviewerId, DueDate = assignment.DueDate });
            foreach (var finding in await db.ReviewFindings.Where(f => f.RoundId == oldRound.Id &&
                (f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.Severity == "Blocking" && f.WithdrawalAcknowledgedBy == null))).ToListAsync())
                db.ReviewFindings.Add(new ReviewFinding { ProjectId = project.Id, PackageId = package.Id, RoundId = round.Id,
                    CarriedFromId = finding.Id, IssueId = finding.IssueId, SourceRevisionId = finding.SourceRevisionId == oldRevision.Id ? newRevision.Id : finding.SourceRevisionId,
                    ProjectDisciplineId = finding.ProjectDisciplineId, OriginatorId = finding.OriginatorId, ResolverId = finding.ResolverId,
                    VerifierId = finding.VerifierId, Text = finding.Text, Severity = finding.Severity });
            oldRound.Status = ReviewStatus.Superseded;
            package.CurrentRoundId = round.Id; package.RoundNumber = round.Number; package.Status = ReviewStatus.Draft;
            db.Audit.Note(oldRound, reason: round.Reason); db.Audit.Note(package, reason: round.Reason);
            await Notify(db, notify, project, package, reviewers.Select(a => a.ReviewerId).Append(package.CoordinatorId).Distinct());
        }
    }
    internal static async Task Recompute(HubDb db, Notifier notify, Project project, ReviewPackage package, bool allowSelf)
    {
        await db.SaveChangesAsync();
        var r = await Round(db, package);
        if (r.StartedAt is null) return;
        var states = await db.DisciplineReviews.Where(a => a.RoundId == r.Id).Select(a => a.Status).ToListAsync();
        var findings = await db.ReviewFindings.Where(f => f.RoundId == r.Id).ToListAsync();
        var status = ReviewRules.PackageStatus(states, findings.Any(f => ReviewRules.BlockingOpen(f.Severity, f.Status, f.WithdrawalAcknowledgedBy != null)));
        if (status == ReviewStatus.Approved) await ValidateRound(db, project, package, allowSelf);
        package.Status = r.Status = status;
        await SubmissionEndpoints.InvalidateForReviewPackage(db, project.Id, package.Id, notify);
    }
    static async Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, SettingsStore settings, TimeProvider clock)
    {
        if (body.IssuedRevisionIds is not null)
        {
            Http.RequireFlag(await settings.Get(db), DeliverableEndpoints.RegisterFlag);
            var (_, context) = await access.Project(projectId, false);
            Access.Demand(Permissions.ManageCoordination(access.Actor, context, body.ProjectDisciplineId));
            await ExternalReviewEndpoints.GuardSourceReplay(projectId, body.RequestId, new { operation = "review.create", body }, body.IssuedRevisionIds, access, db);
        }
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.create", body }, access, db, clock, async (project, ctx) => {
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, body.ProjectDisciplineId));
            await Coordination.Person(db, project, body.CoordinatorId, "coordinatorId"); await Coordination.Discipline(db, project.Id, body.ProjectDisciplineId);
            var p = new ReviewPackage { ProjectId = project.Id, Title = Check.Required(body.Title, "title"), Purpose = Check.Required(body.Purpose, "purpose", 2000), CoordinatorId = body.CoordinatorId, ProjectDisciplineId = body.ProjectDisciplineId, RequiredForIssue = body.RequiredForIssue };
            (p.Seq, p.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "review"); db.ReviewPackages.Add(p);
            var r = await BuildRound(db, project, p, body.Purpose, body.SourceRevisionIds, body.Assignments, body.Reason, null, (await settings.Get(db)).AllowSelfReview, body.IssuedRevisionIds);
            await db.SaveChangesAsync(); p.CurrentRoundId = r.Id; p.RoundNumber = r.Number;
            if (body.RequiredForIssue) {
                foreach (var d in await db.Deliverables.Where(d => db.ReviewManifestItems.Any(m => m.RoundId == r.Id && m.DeliverableId == d.Id)).ToListAsync()) {
                    Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, d.ProjectDisciplineId));
                    Check.That(d.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "sourceRevisionIds", "review.issued");
                    if (d.RequiredReviewPackageId != null) { Access.Demand(Permissions.ManageTeam(access.Actor, ctx)); Check.Reason(body.Reason); }
                    d.RequiredReviewPackageId = p.Id; db.Audit.Note(d, reason: body.Reason);
                }
            }
            return p;
        }, beforeReplay: body.IssuedRevisionIds is null ? null : async (project, context) =>
        {
            Http.RequireFlag(await settings.Get(db), DeliverableEndpoints.RegisterFlag);
            Access.Demand(Permissions.ManageCoordination(access.Actor, context, body.ProjectDisciplineId));
            await ExternalReviewEndpoints.GuardSourceReplay(projectId, body.RequestId, new { operation = "review.create", body }, body.IssuedRevisionIds, access, db);
        });
    }
    static async Task<Coordination.Result> NewRound(Guid projectId, Guid id, RoundBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        if (body.IssuedRevisionIds is not null) Http.RequireFlag(await settings.Get(db), DeliverableEndpoints.RegisterFlag);
        await ExternalReviewEndpoints.GuardReplay(projectId, id, "round", null, null, access, db);
        if (body.IssuedRevisionIds is not null)
            await ExternalReviewEndpoints.GuardSourceReplay(projectId, body.RequestId, new { operation = "review.round", id, body }, body.IssuedRevisionIds, access, db);
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.round", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion);
            Check.That(await ReviewIssuedSources.IsIssuedPackage(db, id) == (body.IssuedRevisionIds is not null), "issuedRevisionIds", "externalReview.sourceMode");
            Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
            var reason = Check.Reason(body.Reason); var old = await Round(db, p);
            var oldDisc = await db.DisciplineReviews.Where(a => a.RoundId == old.Id).Select(a => a.ProjectDisciplineId).ToListAsync();
            if (oldDisc.Except((body.Assignments ?? []).Select(a => a.ProjectDisciplineId)).Any()) {
                Access.Demand(Permissions.ManageTeam(access.Actor, ctx)); Check.Required(body.RemovalImpact, "removalImpact", 4000);
            }
            var round = await BuildRound(db, project, p, body.Purpose, body.SourceRevisionIds, body.Assignments!, reason, body.RemovalImpact, (await settings.Get(db)).AllowSelfReview, body.IssuedRevisionIds);
            var manifest = db.ReviewManifestItems.Local.Where(m => m.RoundId == round.Id).ToList();
            var oldDeliverables = await db.ReviewManifestItems.Where(m => m.RoundId == old.Id).Select(m => m.DeliverableId).ToListAsync();
            var newDeliverables = manifest.Select(m => m.DeliverableId).ToHashSet();
            var removedDeliverables = oldDeliverables.Where(d => !newDeliverables.Contains(d)).ToArray();
            if (removedDeliverables.Length > 0) {
                Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
                Check.Required(body.RemovalImpact, "removalImpact", 4000);
            }
            // BuildRound has validated the immutable captured authors of every newly selected issue.
            Guid[] nextIssuedAuthors = body.IssuedRevisionIds is null ? [] : manifest.SelectMany(item => item.AuthorIds).Distinct().ToArray();
            foreach (var f in await db.ReviewFindings.Where(f => f.RoundId == old.Id && (f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.Severity == "Blocking" && f.WithdrawalAcknowledgedBy == null))).ToListAsync()) {
                if (f.ExternalReviewId is not null)
                {
                    await ExternalReviewEndpoints.ValidateFinding(db, project, f);
                    var retainedAuthors = await FindingAuthors(db, f);
                    if (f.VerifierId is { } verifier)
                        await Independent(db, project, verifier, retainedAuthors.Concat(nextIssuedAuthors).Distinct().ToArray(), (await settings.Get(db)).AllowSelfReview);
                }
                var deliverableId = f.DeliverableIssueId is { } issueId
                    ? (await ReviewIssuedSources.Require(db, project.Id, issueId)).Deliverable.Id
                    : (await Coordination.Revision(db, project.Id, f.SourceRevisionId ?? throw ApiException.Invalid("sourceRevisionId", "coord.reference"))).DeliverableId;
                var mapped = manifest.FirstOrDefault(m => m.DeliverableId == deliverableId);
                db.ReviewFindings.Add(new ReviewFinding { ProjectId = project.Id, PackageId = p.Id, RoundId = round.Id, CarriedFromId = f.Id,
                    IssueId = f.IssueId,
                    SourceRevisionId = mapped is null ? f.SourceRevisionId : mapped.SourceRevisionId,
                    DeliverableIssueId = mapped is null ? f.DeliverableIssueId : mapped.DeliverableIssueId, ExternalReviewId = f.ExternalReviewId,
                    ProjectDisciplineId = f.ProjectDisciplineId, OriginatorId = f.OriginatorId, ResolverId = f.ResolverId, VerifierId = f.VerifierId, Text = f.Text, Severity = f.Severity });
            }
            old.Status = ReviewStatus.Superseded; db.Audit.Note(old, reason: reason);
            await db.SaveChangesAsync(); p.CurrentRoundId = round.Id; p.RoundNumber = round.Number; p.Purpose = round.Purpose; p.Status = ReviewStatus.Draft;
            await SubmissionEndpoints.InvalidateForReviewPackage(db, project.Id, p.Id, notify);
            if (p.RequiredForIssue) foreach (var deliverableId in removedDeliverables) {
                var d = await db.Deliverables.SingleAsync(d => d.Id == deliverableId);
                if (d.RequiredReviewPackageId != p.Id) continue;
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, d.ProjectDisciplineId));
                d.RequiredReviewPackageId = null; db.Audit.Note(d, reason: reason);
            }
            if (p.RequiredForIssue) foreach (var m in manifest) {
                var d = await db.Deliverables.SingleAsync(d => d.Id == m.DeliverableId);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, d.ProjectDisciplineId));
                Check.That(d.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted), "sourceRevisionIds", "review.issued");
                Check.That(d.RequiredReviewPackageId == null || d.RequiredReviewPackageId == p.Id, "sourceRevisionIds", "review.other_gate");
                d.RequiredReviewPackageId = p.Id; db.Audit.Note(d, reason: reason);
            }
            db.Audit.Note(p, reason: reason); return p;
        }, beforeReplay: async (project, context) =>
        {
            await ExternalReviewEndpoints.GuardReplay(projectId, id, "round", null, null, access, db, project, context);
            if (body.IssuedRevisionIds is not null)
                await ExternalReviewEndpoints.GuardSourceReplay(projectId, body.RequestId, new { operation = "review.round", id, body }, body.IssuedRevisionIds, access, db);
        });
    }
    static async Task<Coordination.Result> Action(Guid projectId, Guid id, ActionBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        await ExternalReviewEndpoints.GuardReplay(projectId, id, body.Action, null, null, access, db);
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.action", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion);
            Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
            var round = await Round(db, p);
            if (body.Action == "start") {
                Check.That(p.Status == ReviewStatus.Draft, "status", "review.frozen");
                await ValidateRound(db, project, p, (await settings.Get(db)).AllowSelfReview);
                round.StartedAt = clock.GetUtcNow(); round.Status = p.Status = ReviewStatus.InReview;
            } else {
                Check.OneOf(body.Action, ["cancel"], "action"); Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, p.ProjectDisciplineId));
                var reason = Check.Reason(body.Reason); p.Status = round.Status = ReviewStatus.Cancelled; db.Audit.Note(p, reason: reason); db.Audit.Note(round, reason: reason);
                // A cancelled package can never approve: release the deliverables it gated, as removing them from the manifest does.
                await SubmissionEndpoints.InvalidateForReviewPackage(db, project.Id, p.Id, notify);
                foreach (var d in await db.Deliverables.Where(d => d.ProjectId == project.Id && d.RequiredReviewPackageId == p.Id).ToListAsync()) {
                    Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, d.ProjectDisciplineId));
                    d.RequiredReviewPackageId = null; db.Audit.Note(d, reason: reason);
                }
            }
            await Notify(db, notify, project, p, await db.DisciplineReviews.Where(a => a.RoundId == round.Id).Select(a => a.ReviewerId).ToListAsync()); return p;
        }, beforeReplay: (project, context) => ExternalReviewEndpoints.GuardReplay(projectId, id, body.Action, null, null, access, db, project, context));
    }
    static async Task<Coordination.Result> AssignCoordinator(Guid projectId, Guid id, ReassignBody body, Access access, HubDb db, TimeProvider clock)
    {
        await ExternalReviewEndpoints.GuardReplay(projectId, id, "coordinator", null, null, access, db);
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.coordinator", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion); Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, p.ProjectDisciplineId));
            await Coordination.Person(db, project, body.OwnerId); p.CoordinatorId = body.OwnerId; db.Audit.Note(p, reason: Check.Reason(body.Reason)); return p;
        }, beforeReplay: (project, context) => ExternalReviewEndpoints.GuardReplay(projectId, id, "coordinator", null, null, access, db, project, context));
    }
    static async Task<Coordination.Result> AssignReviewer(Guid projectId, Guid id, Guid assignmentId, ReassignBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        await ExternalReviewEndpoints.GuardReplay(projectId, id, "reviewer", assignmentId, null, access, db);
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.reviewer", id, assignmentId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
            var a = await db.DisciplineReviews.SingleOrDefaultAsync(a => a.Id == assignmentId && a.RoundId == p.CurrentRoundId) ?? throw ApiException.NotFound(); Coordination.Version(a, body.RowVersion);
            await Independent(db, project, body.OwnerId, await Authors(db, a.RoundId), (await settings.Get(db)).AllowSelfReview);
            a.ReviewerId = body.OwnerId; a.Status = DisciplineReviewStatus.Pending; a.DecidedAt = null; a.DecidedBy = null; a.Rationale = null; db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await Recompute(db, notify, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [body.OwnerId]); return a;
        }, beforeReplay: (project, context) => ExternalReviewEndpoints.GuardReplay(projectId, id, "reviewer", assignmentId, null, access, db, project, context));
    }
    static async Task<Coordination.Result> Decide(Guid projectId, Guid id, Guid assignmentId, DecisionBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        await ExternalReviewEndpoints.GuardReplay(projectId, id, "decision", assignmentId, null, access, db);
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.decision", id, assignmentId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); var round = await Round(db, p); Check.That(round.StartedAt != null, "status", "review.start_first");
            var a = await db.DisciplineReviews.SingleOrDefaultAsync(a => a.Id == assignmentId && a.RoundId == round.Id) ?? throw ApiException.NotFound(); Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, a.ReviewerId));
            await ValidateRound(db, project, p, (await settings.Get(db)).AllowSelfReview);
            Check.OneOf(body.Status, [DisciplineReviewStatus.InReview, DisciplineReviewStatus.ChangesRequired, DisciplineReviewStatus.Approved], "status");
            a.Status = body.Status; a.Rationale = Check.Required(body.Rationale, "rationale", 4000); a.DecidedAt = clock.GetUtcNow(); a.DecidedBy = access.Me.Id;
            await Recompute(db, notify, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [p.CoordinatorId]); return a;
        }, beforeReplay: (project, context) => ExternalReviewEndpoints.GuardReplay(projectId, id, "decision", assignmentId, null, access, db, project, context));
    }
    static async Task<Coordination.Result> AddFinding(Guid projectId, Guid id, FindingBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        if (await ReviewIssuedSources.IsIssuedPackage(db, id))
        {
            Http.RequireFlag(await settings.Get(db), DeliverableEndpoints.RegisterFlag);
            await access.Project(projectId, false);
            throw ApiException.Invalid("sourceRevisionId", "externalReview.sourceMode");
        }
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.finding", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion); var round = await Round(db, p); Check.That(round.StartedAt != null, "status", "review.start_first");
            Check.That(await db.DisciplineReviews.AnyAsync(a => a.RoundId == round.Id && a.ReviewerId == access.Me.Id && a.ProjectDisciplineId == body.ProjectDisciplineId), "reviewerId", "review.named_reviewer");
            await Coordination.Person(db, project, access.Me.Id); await Coordination.Person(db, project, body.ResolverId, "resolverId");
            Check.That(await db.ReviewManifestItems.AnyAsync(m => m.RoundId == round.Id && m.SourceRevisionId == body.SourceRevisionId), "sourceRevisionId", "coord.reference");
            Check.That(ReviewRules.Independent(access.Me.Id, [body.ResolverId], (await settings.Get(db)).AllowSelfReview), "resolverId", "review.independent");
            Check.OneOf(body.Severity, ["Blocking", "Advisory"], "severity");
            if (body.IssueId is { } linkedIssue)
                Check.That(await db.Issues.AnyAsync(i => i.Id == linkedIssue && i.ProjectId == project.Id), "issueId", "coord.reference");
            var f = new ReviewFinding { ProjectId = project.Id, PackageId = p.Id, RoundId = round.Id, SourceRevisionId = body.SourceRevisionId, ProjectDisciplineId = body.ProjectDisciplineId,
                IssueId = body.IssueId, OriginatorId = access.Me.Id, VerifierId = access.Me.Id, ResolverId = body.ResolverId, Text = Check.Required(body.Text, "text", 4000), Severity = body.Severity };
            db.ReviewFindings.Add(f); await Recompute(db, notify, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [f.ResolverId]); return f;
        });
    }
    static async Task<Coordination.Result> FindingCommand(Guid projectId, Guid id, Guid findingId, FindingAction body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        var registerScreens = (await settings.Get(db)).IsFlagOn(DeliverableEndpoints.RegisterFlag);
        if (!registerScreens && body.ResponseOutcome is not null) throw ApiException.NotFound();
        await ExternalReviewEndpoints.GuardReplay(projectId, id, "findingAction", findingId, body, access, db);
        // Keep new-field validation inside Run: a committed legacy body must replay
        // before fresh-command outcome requirements are considered.
        return await Coordination.Run(projectId, body.RequestId, new { operation = "review.finding.action", id, findingId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p);
            var f = await db.ReviewFindings.SingleOrDefaultAsync(f => f.Id == findingId && f.PackageId == id && f.RoundId == p.CurrentRoundId) ?? throw ApiException.NotFound(); Coordination.Version(f, body.RowVersion);
            if (f.ExternalReviewId is not null) await ExternalReviewEndpoints.ValidateFinding(db, project, f);
            var reason = Check.Reason(body.Reason); var self = (await settings.Get(db)).AllowSelfReview;
            var outcome = ReviewResponses.Validate(registerScreens, body.Action, body.ResponseOutcome);
            var eventEvidence = body.EvidenceUrl;
            if (body.Action is "assignResolver" or "assignVerifier") {
                if (body.Action == "assignVerifier") Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
                else Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
                var person = body.OwnerId ?? throw ApiException.Invalid("ownerId", "error.required"); await Coordination.Person(db, project, person);
                Check.That(f.Status is FindingStatus.Open or FindingStatus.Responded, "status", "review.frozen");
                if (body.Action == "assignVerifier") {
                    var authors = await FindingAuthors(db, f);
                    await Independent(db, project, person, authors, self);
                    Check.That(await db.ProjectMembers.AnyAsync(m => m.ProjectId == project.Id && m.UserId == person && m.RemovedAt == null && m.Roles.Contains(ProjectRole.Reviewer))
                        || await db.DisciplineReviews.AnyAsync(a => a.RoundId == f.RoundId && a.ReviewerId == person), "ownerId", "review.named_reviewer");
                    f.VerifierId = person;
                } else { if (f.VerifierId is { } verifier) Check.That(ReviewRules.Independent(verifier, [person], self), "ownerId", "review.independent"); f.ResolverId = person; f.Status = FindingStatus.Open; f.Response = null; f.EvidenceUrl = null; f.ResponseOutcome = null; }
            } else if (body.Action == "acknowledgeWithdrawal") {
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, p.CoordinatorId)); await Coordination.Person(db, project, p.CoordinatorId);
                Check.That(f.Status == FindingStatus.Withdrawn && f.Severity == "Blocking", "status", "review.frozen"); f.WithdrawalAcknowledgedBy = access.Me.Id;
            } else {
                Check.That(ReviewRules.FindingStep(f.Status, body.Action), "status", "review.finding_transition");
                var actor = body.Action == FindingStatus.Responded ? f.ResolverId : f.VerifierId
                    ?? throw ApiException.Invalid("verifierId", "externalReview.verifierRequired");
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, actor)); await Coordination.Person(db, project, actor);
                if (body.Action != FindingStatus.Responded)
                {
                    if (f.ExternalReviewId is not null) await ExternalReviewEndpoints.NamedVerifier(db, project, f.RoundId, actor);
                    await Independent(db, project, actor, await FindingAuthors(db, f), self);
                }
                if (body.Action == FindingStatus.Responded) { f.Response = reason; f.EvidenceUrl = Coordination.Url(body.EvidenceUrl); f.ResponseOutcome = outcome; }
                if (body.Action == FindingStatus.VerifiedClosed)
                {
                    Check.That(f.Response != null && f.EvidenceUrl != null, "evidenceUrl", "error.required");
                    if (f.ExternalReviewId is not null) eventEvidence = Coordination.Url(body.EvidenceUrl);
                }
                f.Status = body.Action;
            }
            db.Audit.Note(f, reason: reason); db.FindingEvents.Add(new FindingEvent { ProjectId = project.Id, FindingId = f.Id, Action = body.Action, Reason = reason, EvidenceUrl = eventEvidence, ResponseOutcome = outcome });
            await Recompute(db, notify, project, p, self); await Notify(db, notify, project, p, new Guid?[] { f.ResolverId, f.VerifierId, p.CoordinatorId }.OfType<Guid>()); return f;
        }, beforeReplay: (project, context) => ExternalReviewEndpoints.GuardReplay(projectId, id, "findingAction", findingId, body, access, db, project, context));
    }
    public static async Task Gate(HubDb db, Project project, Deliverable deliverable, string? issuingRevision, bool allowSelf, string? issuingUrl = null, bool lockRows = true)
    {
        if (deliverable.RequiredReviewPackageId is not { } pid) return;
        var p = await Load(db, project.Id, pid);
        Check.That(!await ReviewIssuedSources.IsIssuedPackage(db, pid), "requiredReviewPackageId", "externalReview.sourceMode");
        Check.That(p.Status == ReviewStatus.Approved, "requiredReviewPackageId", "review.gate");
        await ValidateRound(db, project, p, allowSelf, lockRows);
        var revision = await db.ReviewManifestItems.Where(m => m.RoundId == p.CurrentRoundId && m.DeliverableId == deliverable.Id).Join(db.SourceRevisions, m => m.SourceRevisionId, r => r.Id, (m, r) => r).SingleOrDefaultAsync();
        Check.That(revision != null && revision.Revision == (issuingRevision ?? deliverable.Revision) && (issuingUrl == null || revision.Url == issuingUrl), "revision", "review.gate_revision");
    }
    static IQueryable<ReviewPackage> Query(HubDb db, Guid project, Filter f, Guid actor, bool issuedEnabled)
    {
        var q = ReviewIssuedSources.VisiblePackages(db, issuedEnabled).AsNoTracking().Where(p => p.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(p => p.Title.Contains(f.Q) || p.Key.Contains(f.Q));
        if (f.Status is { Length: > 0 }) { Check.OneOf(f.Status, ReviewStatus.All, "status"); q = q.Where(p => p.Status == f.Status); }
        if (f.OwnerId is { } owner) q = q.Where(p => p.CoordinatorId == owner);
        if (f.DisciplineId is { } discipline) q = q.Where(p => db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ProjectDisciplineId == discipline));
        if (f.Mine == true) q = q.Where(p => p.CoordinatorId == actor || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == actor));
        return q.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Seq);
    }
    static async Task<List<ReviewRow>> Rows(HubDb db, IQueryable<ReviewPackage> query, DateTimeOffset now)
    {
        var rows = await query.Select(p => new Projection(p, db.DisciplineReviews.Count(a => a.RoundId == p.CurrentRoundId && a.Status != DisciplineReviewStatus.Approved),
            db.ReviewFindings.Count(f => f.RoundId == p.CurrentRoundId && f.Severity == "Blocking" && f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.Severity == "Blocking" && f.WithdrawalAcknowledgedBy == null)),
            db.ReviewRounds.Where(r => r.Id == p.CurrentRoundId).Select(r => r.StartedAt).FirstOrDefault())).ToListAsync();
        return rows.Select(x => new ReviewRow(x.P.Id, x.P.Key, x.P.Title, x.P.Status, x.P.CoordinatorId, x.P.RoundNumber, x.P.RowVersion, x.OutstandingDisciplines, x.BlockingFindings,
            x.Started is { } start && (x.P.Status == ReviewStatus.InReview || x.P.Status == ReviewStatus.ChangesRequired) ? Math.Max(0, (int)(now - start).TotalDays) : 0)).ToList();
    }
    static async Task<Ok<Page<ReviewRow>>> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    { await access.Project(projectId, false); var q = Query(db, projectId, filter, access.Me.Id, (await store.Get(db)).IsFlagOn(DeliverableEndpoints.RegisterFlag)); var (pg, size) = Http.Paging(page, pageSize); return TypedResults.Ok(new Page<ReviewRow>(await Rows(db, q.Skip((pg - 1) * size).Take(size), clock.GetUtcNow()), pg, size, await q.CountAsync())); }
    static async Task<Ok<LinkedIssuePage>> LinkedIssues(Guid projectId, Guid? disciplineId, Guid? ownerId, int? page, int? pageSize, Access access, HubDb db, SettingsStore store)
    {
        await access.Project(projectId, false);
        var findings = ReviewIssuedSources.VisibleFindings(db, (await store.Get(db)).IsFlagOn(DeliverableEndpoints.RegisterFlag));
        var q = db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId &&
            findings.Any(f => f.ProjectId == projectId && f.IssueId == i.Id &&
                db.ReviewPackages.Any(p => p.Id == f.PackageId && p.ProjectId == projectId && p.CurrentRoundId == f.RoundId) &&
                (disciplineId == null || f.ProjectDisciplineId == disciplineId || i.ProjectDisciplineId == disciplineId ||
                    db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == disciplineId))));
        if (ownerId is { } owner) q = q.Where(i => i.OwnerId == owner);
        var (pg, size) = Http.Paging(page, pageSize);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(i => i.Key).Skip((pg - 1) * size).Take(size)
            .Select(i => new DisciplineCoordinationEndpoints.CoordinationIssueRow(i.Id, i.Key, i.Title, i.Status, i.OwnerId,
                db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.DisplayName).FirstOrDefault(), i.ProjectDisciplineId)).ToListAsync();
        return TypedResults.Ok(new LinkedIssuePage(rows, total, pg, size));
    }
    static Task<Ok<ChangeEndpoints.CoordinationOptions>> Options(Guid projectId, Access access, HubDb db, SettingsStore store, TimeProvider clock) => ChangeEndpoints.Options(projectId, access, db, store, clock);
    static async Task<ReviewAssignmentGuidance> Guidance(HubDb db, Project project, Guid[] ids, OrgSettings settings, IEnumerable<Guid>? capturedAuthors = null)
    {
        var revisions = await db.SourceRevisions.AsNoTracking().Where(r => r.ProjectId == project.Id && ids.Contains(r.Id)).ToListAsync();
        if (revisions.Count != ids.Length) throw ApiException.NotFound();
        var deliverableIds = revisions.Select(r => r.DeliverableId).OfType<Guid>().Distinct().ToArray();
        var available = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == project.Id && deliverableIds.Contains(d.Id)).Select(d => d.Id).ToListAsync();
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id && available.Contains(t.DeliverableId ?? Guid.Empty))
            .Select(t => new { t.DeliverableId, t.Key, t.AssigneeId }).ToListAsync();
        var peopleIds = tasks.Select(t => t.AssigneeId).OfType<Guid>().Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(u => peopleIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var eligible = await Coordination.People(db, project).Select(u => u.Id).ToListAsync();
        var authors = new HashSet<Guid>(revisions.SelectMany(r => r.AuthorIds));
        authors.UnionWith(capturedAuthors ?? []);
        foreach (var source in revisions.Where(r => r.DeliverableId is { } id && available.Contains(id)))
            authors.UnionWith(await Coordination.Authors(db, source.DeliverableId!.Value));
        var authorIds = authors.Where(eligible.Contains).Order().ToArray();
        RecordContextPerson Person(Guid? id) => id is null ? new(null, null, "Unassigned") : people.TryGetValue(id.Value, out var person)
            ? new(person.Id, person.DisplayName, person.IsActive ? "Active" : "Inactive") : new(id, null, "Unknown");
        var rows = revisions.OrderBy(r => r.SourceKey).ThenBy(r => r.Id).Select(r => new ReviewSourcePerformers(r.Id, r.SourceKey,
            r.DeliverableId is { } id && available.Contains(id), tasks.Where(t => t.DeliverableId == r.DeliverableId)
                .OrderBy(t => t.Key).Select(t => new ReviewSourcePerformer(t.Key, Person(t.AssigneeId))).ToArray())).ToArray();
        return new(settings.AllowSelfReview, ids.Length > 0 && rows.All(r => r.Available), authorIds,
            settings.AllowSelfReview ? [] : authorIds, rows);
    }
    static async Task<Ok<ReviewAssignmentGuidance>> AssignmentGuidance(Guid projectId, string? sourceRevisionIds, Access access, HubDb db, SettingsStore store)
    {
        var (project, _) = await access.Project(projectId, false);
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("screen_patterns")) throw ApiException.NotFound();
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        (project, _) = await access.Project(projectId, false);
        var values = (sourceRevisionIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Check.That(values.Length <= 200 && values.All(value => Guid.TryParse(value, out _)), "sourceRevisionIds", "review.manifest_rule");
        return TypedResults.Ok(await Guidance(db, project, values.Select(Guid.Parse).Distinct().ToArray(), settings));
    }
    static async Task<Ok<ReviewDetail>> Detail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store)
    {
        var (project, ctx) = await access.Project(projectId, false);
        var settings = await store.Get(db);
        await using var snapshot = settings.IsFlagOn("screen_patterns") ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        if (snapshot is not null) (project, ctx) = await access.Project(projectId, false);
        var p = await Load(db, projectId, id);
        var rounds = await db.ReviewRounds.Where(r => r.PackageId == id).OrderByDescending(r => r.Number).ToListAsync(); var ids = rounds.Select(r => r.Id).ToArray();
        var assignments = await db.DisciplineReviews.Where(a => ids.Contains(a.RoundId)).ToListAsync();
        var findings = await db.ReviewFindings.Where(f => f.PackageId == id).OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToListAsync();
        if (rounds.Any(round => round.ProjectId != projectId) || assignments.Any(assignment => assignment.ProjectId != projectId)
            || findings.Any(finding => finding.ProjectId != projectId || !ids.Contains(finding.RoundId))) throw ApiException.NotFound();
        var manifest = await db.ReviewManifestItems.Where(m => ids.Contains(m.RoundId)).ToListAsync();
        var sourceIds = manifest.Select(item => item.SourceRevisionId).OfType<Guid>().Distinct().ToArray();
        var sources = await db.SourceRevisions.AsNoTracking().Where(source => source.ProjectId == projectId && sourceIds.Contains(source.Id)).ToDictionaryAsync(source => source.Id);
        var issuedSources = await ReviewIssuedSources.ReadMany(db, projectId, manifest.Select(item => item.DeliverableIssueId).OfType<Guid>().Distinct().ToArray());
        var manifestRows = new List<ReviewManifestRow>();
        foreach (var item in manifest)
        {
            if (item.ProjectId != projectId) throw ApiException.NotFound();
            if (item.DeliverableIssueId is { } issueId)
            {
                var source = issuedSources[issueId];
                Check.That(item.SourceRevisionId is null && source.Deliverable.Id == item.DeliverableId, "deliverableIssueId", "externalReview.integrity");
                manifestRows.Add(new(item.Id, item.RoundId, item.DeliverableId, null, item.AuthorIds, null, ReviewIssuedSources.Row(source)));
            }
            else
            {
                if (item.SourceRevisionId is not { } sourceId || !sources.TryGetValue(sourceId, out var source)) throw ApiException.NotFound();
                manifestRows.Add(new(item.Id, item.RoundId, item.DeliverableId, item.SourceRevisionId, item.AuthorIds, ChangeEndpoints.SourceRevisionRow.From(source)));
            }
        }
        var issuedPackage = await ReviewIssuedSources.IsIssuedPackage(db, id);
        if (issuedPackage)
        {
            Check.That(!p.RequiredForIssue && manifest.All(item => item.SourceRevisionId is null && item.DeliverableIssueId is not null), "issuedRevisionIds", "externalReview.integrity");
            foreach (var finding in findings) await ExternalReviewEndpoints.ValidateFinding(db, project, finding, requireKnown: false);
        }
        var external = issuedPackage ? await ExternalReviewEndpoints.ReadAll(db, projectId, id) : null;
        var events = await db.FindingEvents.Where(e => db.ReviewFindings.Any(f => f.Id == e.FindingId && f.PackageId == id)).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync();
        var reader = new RecordContextReader(db, project, settings);
        reader.SeedReview(p, rounds, assignments, findings, manifest, events);
        var findingRows = new List<ReviewFindingRow>();
        foreach (var finding in findings)
        {
            var row = ReviewFindingRow.From(finding, settings.IsFlagOn(DeliverableEndpoints.RegisterFlag));
            if (finding.ExternalReviewId is { } receiptId)
            {
                var receipt = external?.SingleOrDefault(value => value.Id == receiptId) ?? throw ApiException.NotFound();
                row = row with { Originator = ExternalReviewEndpoints.Originator(receipt) };
            }
            if (settings.IsFlagOn("screen_patterns") && finding.ProjectId == projectId
                && rounds.Any(r => r.Id == finding.RoundId && r.ProjectId == projectId))
                row = row with { Context = await reader.Read(RecordContextKind.ReviewFinding, finding.Id) };
            findingRows.Add(row);
        }
        var eligible = await Coordination.People(db, project).Select(u => u.Id).ToListAsync();
        var canCoordinate = Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId).Ok;
        var canWrite = Permissions.CoordinationWrite(access.Actor, ctx).Ok;
        List<ReviewRoundPresentation>? presentation = settings.IsFlagOn("screen_patterns") ? [] : null;
        if (presentation is not null) foreach (var round in rounds)
        {
            var roundManifest = manifest.Where(m => m.RoundId == round.Id).ToArray();
            var issuedGuidance = issuedPackage ? await ReviewIssuedSources.Guidance(db, project,
                roundManifest.Select(m => m.DeliverableIssueId ?? throw ApiException.Invalid("deliverableIssueId", "externalReview.integrity")).Distinct().ToArray(), settings) : null;
            var guidance = issuedPackage ? null : await Guidance(db, project, roundManifest.Select(m => m.SourceRevisionId
                ?? throw ApiException.Invalid("sourceRevisionId", "coord.reference")).Distinct().ToArray(), settings, roundManifest.SelectMany(m => m.AuthorIds));
            var required = assignments.Where(a => a.RoundId == round.Id).ToArray();
            var missing = required.Where(a => a.ReviewerId == Guid.Empty || !eligible.Contains(a.ReviewerId)).Select(a => a.Id).ToArray();
            var reviewerIds = required.Select(a => a.ReviewerId).ToArray();
            var reviewerPeople = await db.Users.AsNoTracking().Where(u => reviewerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
            var reviewers = required.Select(a => new ReviewAssignmentPerson(a.Id, a.ReviewerId == Guid.Empty ? new(null, null, "Unassigned")
                : reviewerPeople.TryGetValue(a.ReviewerId, out var person) ? new(person.Id, person.DisplayName, person.IsActive ? "Active" : "Inactive") : new(a.ReviewerId, null, "Unknown"))).ToArray();
            var start = canCoordinate && canWrite && p.Status == ReviewStatus.Draft && round.Id == p.CurrentRoundId;
            string? startReason = null;
            if (start)
            {
                if (missing.Length > 0) { start = false; startReason = Text.Get("coord.person"); }
                else if (required.Length == 0) { start = false; startReason = Text.Get("review.assignments"); }
                else if (!(guidance?.SourcesAvailable ?? issuedGuidance?.SourcesAvailable ?? false)) { start = false; startReason = Text.Get("review.manifest_rule"); }
                else try { await ValidateRound(db, project, p, settings.AllowSelfReview, lockRows: false); }
                    catch (ApiException ex) { start = false; startReason = ex.Message; }
            }
            presentation.Add(new(round.Id, guidance, missing, reviewers, start, startReason, issuedGuidance));
        }
        return TypedResults.Ok(new ReviewDetail(ReviewPackageRow.From(p), rounds.Select(ReviewRoundRow.From).ToList(), assignments.Select(ReviewAssignmentRow.From).ToList(), findingRows,
            manifestRows,
            events.Select(e => ReviewFindingEventRow.From(e, settings.IsFlagOn(DeliverableEndpoints.RegisterFlag))).ToList(), eligible, access.Me.Id,
            canCoordinate, Permissions.ManageCoordination(access.Actor, ctx, p.ProjectDisciplineId).Ok,
            canWrite, Permissions.ManageTeam(access.Actor, ctx).Ok, presentation, external,
            issuedPackage ? (await ExternalReviewEndpoints.RecordPermission(db, access.Actor, ctx)).Ok && p.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded)
                && rounds.Any(round => round.Id == p.CurrentRoundId && round.StartedAt is not null)
                && manifestRows.Where(item => item.RoundId == p.CurrentRoundId).All(item => item.IssuedSource?.Selectable == true) : null));
    }
    static async Task<Ok<ReviewFindingLocation>> FindingDetail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore store)
    {
        var (project, _) = await access.Project(projectId, false);
        var settings = await store.Get(db);
        var contextOn = settings.IsFlagOn("screen_patterns");
        if (!contextOn && !settings.IsFlagOn("register_screens")) throw ApiException.NotFound();
        var finding = await ReviewIssuedSources.VisibleFindings(db, settings.IsFlagOn(DeliverableEndpoints.RegisterFlag)).AsNoTracking().SingleOrDefaultAsync(f => f.ProjectId == projectId && f.Id == id) ?? throw ApiException.NotFound();
        var package = await Load(db, projectId, finding.PackageId);
        if (!await db.ReviewRounds.AsNoTracking().AnyAsync(r => r.Id == finding.RoundId && r.PackageId == finding.PackageId && r.ProjectId == projectId)) throw ApiException.NotFound();
        if (!contextOn) return TypedResults.Ok(new ReviewFindingLocation(id, finding.PackageId, finding.RoundId, package.Key, finding.Text));
        var reader = new RecordContextReader(db, project, settings);
        var context = await reader.Read(RecordContextKind.ReviewFinding, id) ?? throw ApiException.NotFound();
        var identity = reader.Identity(RecordContextKind.ReviewFinding, id);
        return TypedResults.Ok(new ReviewFindingLocation(id, finding.PackageId, finding.RoundId, identity.Key, identity.Title, context));
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false); var rows = await Rows(db, Query(db, projectId, filter, access.Me.Id,
            (await store.Get(db)).IsFlagOn(DeliverableEndpoints.RegisterFlag)).Take(Export.MaxRows + 1), clock.GetUtcNow());
        return await ExportFile.Send(db, store, format, Text.Get("export.reviews", project.ProjectNumber), Columns, JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id, $"{project.ProjectNumber}-reviews", clock);
    }
}
