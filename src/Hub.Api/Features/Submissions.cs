using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class SubmissionEndpoints
{
    public sealed record ManifestInput(Guid SourceRevisionId);
    public sealed record OptionalCheckInput(string Label, Guid OwnerId, Guid? ProjectDisciplineId);
    public sealed record CreateBody(Guid RequestId, string Title, string Purpose, string RecipientReference, Guid CoordinatorId,
        Guid MilestoneId, DateOnly TargetDate, ManifestInput[] Manifest, OptionalCheckInput[] OptionalChecks, Guid? SupersedesPackageId, string? Reason);
    public sealed record ManifestBody(Guid RequestId, int RowVersion, ManifestInput[] Manifest, string Reason);
    public sealed record EditBody(Guid RequestId, int RowVersion, string Title, string Purpose, string RecipientReference, Guid MilestoneId, DateOnly TargetDate, string Reason);
    public sealed record AssignBody(Guid RequestId, int RowVersion, Guid CoordinatorId, string Reason);
    public sealed record StartBody(Guid RequestId, int RowVersion, string? Reason);
    public sealed record CheckBody(Guid RequestId, int PackageRowVersion, int RowVersion, string Action, string? EvidenceUrl, string? Reason);
    public sealed record CheckAssignBody(Guid RequestId, int PackageRowVersion, int RowVersion, Guid OwnerId, string Reason);
    public sealed record IssueBody(Guid RequestId, int RowVersion, int ManifestVersion, string ExpectedFingerprint, string Destination, string TransmittalUrl, string? Reason);
    public sealed record CancelBody(Guid RequestId, int RowVersion, string Reason);
    public sealed record Filter(string? Q, string? Status, Guid? CoordinatorId, Guid? MilestoneId, DateOnly? TargetFrom, DateOnly? TargetTo);
    static readonly Col[] ListColumns = [
        new("key", "key"), new("title", "name"), new("status", "status"),
        new("coordinatorName", "owner", Label: "Coordinator"), new("milestoneName", "milestone"),
        new("targetDate", "targetDate", "date", "Target date"), new("blockerCount", "blockers", "number", "Readiness blockers")];

    sealed record ListRow(Guid Id, string Key, string Title, string Purpose, string Status, Guid CoordinatorId, string? CoordinatorName,
        Guid MilestoneId, string? MilestoneName, DateOnly TargetDate, int ManifestVersion, int RowVersion, int BlockerCount);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/submissions", List);
        api.MapGet("/projects/{projectId:guid}/submissions/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/submissions/{id:guid}", Detail);
        api.MapGet("/projects/{projectId:guid}/submissions/{id:guid}/export", ExportSnapshot);
        api.MapPost("/projects/{projectId:guid}/submissions", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/manifest", ReplaceManifest).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/edit", Edit).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/assign", Assign).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/start", Start).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/checks/{checkId:guid}", CheckCommand).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/checks/{checkId:guid}/assign", AssignCheck).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/issue", Issue).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/submissions/{id:guid}/cancel", Cancel).WithMetadata(new Coordination.AtomicCommand());
    }

    // Publication and invalidation share the same project transaction. Issued snapshots are untouched.
    public static async Task InvalidateForPublishedRevision(HubDb db, Guid projectId, Guid oldRevisionId, Notifier notify)
    {
        var packages = await db.SubmissionManifestItems.Where(m => m.ProjectId == projectId && m.SourceRevisionId == oldRevisionId)
            .Join(db.SubmissionPackages, m => m.PackageId, p => p.Id, (m, p) => p)
            .Where(p => db.SubmissionManifestItems.Any(m => m.PackageId == p.Id && m.ManifestVersion == p.ManifestVersion && m.SourceRevisionId == oldRevisionId)
                && (p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready || p.Status == SubmissionStatus.Draft))
            .Distinct().ToListAsync();
        foreach (var package in packages)
            await Invalidate(db, notify, package, "Source revision changed");
    }

    public static async Task InvalidateForReviewPackage(HubDb db, Guid projectId, Guid reviewPackageId, Notifier notify)
    {
        var packages = await db.SubmissionPackages.Where(p => p.ProjectId == projectId &&
            (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready) &&
            db.SubmissionManifestItems.Any(m => m.PackageId == p.Id && m.ManifestVersion == p.ManifestVersion &&
                db.Deliverables.Any(d => d.Id == m.DeliverableId && d.RequiredReviewPackageId == reviewPackageId))).ToListAsync();
        foreach (var package in packages)
            await Invalidate(db, notify, package, "Required review changed");
    }

    /// <summary>Invalidates unissued packages whose manifest contains a handoff target.</summary>
    public static async Task InvalidateForHandoff(HubDb db, Guid projectId, Guid? targetTaskId, Guid? targetDeliverableId, Notifier notify)
    {
        var packageIds = await db.SubmissionManifestItems.Where(m => m.ProjectId == projectId &&
            (m.DeliverableId == targetDeliverableId || targetTaskId != null && db.Tasks.Any(t => t.Id == targetTaskId && t.DeliverableId == m.DeliverableId)))
            .Where(m => db.SubmissionPackages.Any(p => p.Id == m.PackageId && p.ManifestVersion == m.ManifestVersion &&
                (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready)))
            .Select(m => m.PackageId).Distinct().ToListAsync();
        foreach (var package in await db.SubmissionPackages.Where(p => p.ProjectId == projectId && packageIds.Contains(p.Id) &&
            (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready)).ToListAsync())
            await Invalidate(db, notify, package, "Handoff changed");
    }

    /// <summary>Invalidates unissued packages whose manifest contains work using a changed basis entry.</summary>
    public static async Task InvalidateForDesignBasisEntry(HubDb db, Guid projectId, Guid entryId, Notifier notify)
    {
        var uses = await db.BasisUses.Where(u => u.ProjectId == projectId && db.DesignBasisVersions.Any(v => v.Id == u.VersionId && v.EntryId == entryId))
            .Select(u => new { u.TargetType, u.TargetId }).ToListAsync();
        var deliverables = uses.Where(u => u.TargetType == "Deliverable").Select(u => u.TargetId).ToArray();
        var tasks = uses.Where(u => u.TargetType == "Task").Select(u => u.TargetId).ToArray();
        var packageIds = await db.SubmissionManifestItems.Where(m => m.ProjectId == projectId &&
            (deliverables.Contains(m.DeliverableId) || db.Tasks.Any(t => tasks.Contains(t.Id) && t.DeliverableId == m.DeliverableId)))
            .Where(m => db.SubmissionPackages.Any(p => p.Id == m.PackageId && p.ManifestVersion == m.ManifestVersion &&
                (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready)))
            .Select(m => m.PackageId).Distinct().ToListAsync();
        foreach (var package in await db.SubmissionPackages.Where(p => p.ProjectId == projectId && packageIds.Contains(p.Id) &&
            (p.Status == SubmissionStatus.Draft || p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready)).ToListAsync())
            await Invalidate(db, notify, package, "Applicable design basis changed");
    }

    static async Task Invalidate(HubDb db, Notifier notify, SubmissionPackage package, string reason)
    {
        package.Status = SubmissionStatus.Checking;
        var checks = await db.SubmissionChecks.Where(c => c.PackageId == package.Id && c.ManifestVersion == package.ManifestVersion).ToListAsync();
        foreach (var check in checks)
        {
            check.Status = SubmissionCheckStatus.Pending; check.EvidenceUrl = null; check.Reason = null; check.ApprovedAt = null; check.ApprovedBy = null;
            db.Audit.Note(check, reason: reason);
        }
        db.Audit.Note(package, reason: reason);
        var project = await db.Projects.SingleAsync(p => p.Id == package.ProjectId);
        await NotifyChanged(notify, project, package, checks.Select(c => (Guid?)c.OwnerId).ToArray());
    }

    static async Task<SubmissionPackage> Load(HubDb db, Guid projectId, Guid id) =>
        await db.SubmissionPackages.SingleOrDefaultAsync(p => p.ProjectId == projectId && p.Id == id) ?? throw ApiException.NotFound();

    static Task NotifyChanged(Notifier notify, Project project, SubmissionPackage package, params Guid?[] additionalRecipients) =>
        notify.Send(NotificationEvents.SubmissionChanged,
            new Guid?[] { project.ProjectManagerId, package.CoordinatorId }.Concat(additionalRecipients),
            new NotifyItem(project.Id, "SubmissionPackage", package.Id, package.Key,
                $"/projects/{project.ProjectNumber}/submissions?panel=SubmissionPackage:{package.Id}", project.ProjectNumber),
            Text.Get("notify.submission_changed", package.Key, package.Status));

    static async Task AddVersion(HubDb db, Project project, SubmissionPackage package, ManifestInput[] manifest, OptionalCheckInput[] optionals)
    {
        Check.That(manifest is { Length: > 0 and <= 200 } && manifest.Select(m => m.SourceRevisionId).Distinct().Count() == manifest.Length, "manifest", "review.manifest");
        Check.That(optionals is { Length: <= 50 }, "optionalChecks", "review.assignments");
        var seen = new HashSet<Guid>();
        foreach (var input in manifest)
        {
            var revision = await Coordination.Revision(db, project.Id, input.SourceRevisionId);
            var deliverableId = revision.DeliverableId ?? throw ApiException.Invalid("manifest", "review.manifest");
            Check.That(seen.Add(deliverableId), "manifest", "review.manifest");
            var work = await Coordination.Target(db, project, "Deliverable", deliverableId);
            Check.That(await Coordination.Published(db, revision), "manifest", "review.source_unpublished");
            var head = await Coordination.Head(db, revision);
            Check.That(head is not null && head.CurrentRevisionId == revision.Id, "manifest", "review.source_changed");
            var reviewId = await db.Deliverables.Where(d => d.Id == deliverableId).Select(d => d.RequiredReviewPackageId).SingleAsync();
            var roundId = reviewId is { } rid ? await db.ReviewPackages.Where(r => r.Id == rid && r.ProjectId == project.Id).Select(r => r.CurrentRoundId).SingleOrDefaultAsync() : null;
            db.SubmissionManifestItems.Add(new SubmissionManifestItem { ProjectId = project.Id, PackageId = package.Id, ManifestVersion = package.ManifestVersion,
                DeliverableId = deliverableId, SourceRevisionId = revision.Id, ReviewRoundId = roundId, Required = true });
            foreach (var kind in new[] { SubmissionCheckKind.Deliverable, SubmissionCheckKind.CurrentRevision, SubmissionCheckKind.IndependentReview, SubmissionCheckKind.BlockingFindings })
                db.SubmissionChecks.Add(new SubmissionCheck { ProjectId = project.Id, PackageId = package.Id, ManifestVersion = package.ManifestVersion,
                    Kind = kind, SourceId = deliverableId, ProjectDisciplineId = work.DisciplineId, OwnerId = package.CoordinatorId,
                    Required = true, EvidenceRule = kind });
        }
        foreach (var kind in new[] { SubmissionCheckKind.Handoff, SubmissionCheckKind.ChangeAssessment, SubmissionCheckKind.Access })
            db.SubmissionChecks.Add(new SubmissionCheck { ProjectId = project.Id, PackageId = package.Id, ManifestVersion = package.ManifestVersion,
                Kind = kind, OwnerId = package.CoordinatorId, Required = true, EvidenceRule = kind });
        foreach (var optional in optionals)
        {
            await Coordination.Person(db, project, optional.OwnerId);
            if (optional.ProjectDisciplineId is { } discipline) await Coordination.Discipline(db, project.Id, discipline);
            db.SubmissionChecks.Add(new SubmissionCheck { ProjectId = project.Id, PackageId = package.Id, ManifestVersion = package.ManifestVersion,
                Kind = SubmissionCheckKind.Applicability, SourceId = Guid.CreateVersion7(), OwnerId = optional.OwnerId,
                ProjectDisciplineId = optional.ProjectDisciplineId, Required = false, EvidenceRule = Check.Required(optional.Label, "label", 500) });
        }
    }

    static async Task<(ManifestInput[] Manifest, OptionalCheckInput[] Optionals)> PreviousVersion(HubDb db, SubmissionPackage package)
    {
        var manifest = await db.SubmissionManifestItems.Where(m => m.PackageId == package.Id && m.ManifestVersion == package.ManifestVersion)
            .OrderBy(m => m.Id).Select(m => new ManifestInput(m.SourceRevisionId)).ToArrayAsync();
        var optionals = await db.SubmissionChecks.Where(c => c.PackageId == package.Id && c.ManifestVersion == package.ManifestVersion && c.Kind == SubmissionCheckKind.Applicability)
            .OrderBy(c => c.Id).Select(c => new OptionalCheckInput(c.EvidenceRule ?? "", c.OwnerId, c.ProjectDisciplineId)).ToArrayAsync();
        return (manifest, optionals);
    }

    static Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.create", body }, access, db, clock, async (project, ctx) => {
            Access.Demand(Permissions.CreateSubmission(access.Actor, ctx));
            await Coordination.Person(db, project, body.CoordinatorId, "coordinatorId");
            Check.That(await db.Milestones.AnyAsync(m => m.Id == body.MilestoneId && m.ProjectId == project.Id && !m.IsCancelled), "milestoneId", "coord.reference");
            Check.That(body.TargetDate != default, "targetDate", "error.required");
            SubmissionPackage? prior = null;
            if (body.SupersedesPackageId is { } oldId) {
                prior = await Load(db, project.Id, oldId);
                Check.That(prior.Status == SubmissionStatus.Issued, "supersedesPackageId", "coord.reference");
            }
            var package = new SubmissionPackage { ProjectId = project.Id, Title = Check.Required(body.Title, "title"), Purpose = Check.Required(body.Purpose, "purpose", 2000),
                RecipientReference = Check.Required(body.RecipientReference, "recipientReference", 1000), CoordinatorId = body.CoordinatorId,
                MilestoneId = body.MilestoneId, TargetDate = body.TargetDate, SupersedesPackageId = prior?.Id };
            (package.Seq, package.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "submission");
            db.SubmissionPackages.Add(package);
            await AddVersion(db, project, package, body.Manifest, body.OptionalChecks);
            await NotifyChanged(notify, project, package);
            return package;
        });

    static Task<Coordination.Result> ReplaceManifest(Guid projectId, Guid id, ManifestBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.manifest", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.CoordinateSubmission(access.Actor, ctx, package.CoordinatorId));
            Check.That(package.Status is SubmissionStatus.Draft or SubmissionStatus.Checking or SubmissionStatus.Ready, "status", "review.frozen");
            var reason = Check.Reason(body.Reason);
            var (_, prior) = await PreviousVersion(db, package);
            package.ManifestVersion++; package.Status = SubmissionStatus.Checking;
            await AddVersion(db, project, package, body.Manifest, prior);
            db.Audit.Note(package, reason: reason); await NotifyChanged(notify, project, package); return package;
        });

    static Task<Coordination.Result> Edit(Guid projectId, Guid id, EditBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.edit", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.CoordinateSubmission(access.Actor, ctx, package.CoordinatorId));
            Check.That(package.Status is SubmissionStatus.Draft or SubmissionStatus.Checking or SubmissionStatus.Ready, "status", "review.frozen");
            Check.That(await db.Milestones.AnyAsync(m => m.Id == body.MilestoneId && m.ProjectId == project.Id && !m.IsCancelled), "milestoneId", "coord.reference");
            Check.That(body.TargetDate != default, "targetDate", "error.required");
            var previous = await PreviousVersion(db, package);
            package.Title = Check.Required(body.Title, "title"); package.Purpose = Check.Required(body.Purpose, "purpose", 2000);
            package.RecipientReference = Check.Required(body.RecipientReference, "recipientReference", 1000);
            package.MilestoneId = body.MilestoneId; package.TargetDate = body.TargetDate;
            package.ManifestVersion++; package.Status = SubmissionStatus.Checking;
            await AddVersion(db, project, package, previous.Manifest, previous.Optionals);
            db.Audit.Note(package, reason: Check.Reason(body.Reason)); await NotifyChanged(notify, project, package); return package;
        });

    static Task<Coordination.Result> Assign(Guid projectId, Guid id, AssignBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.assign", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.AuthoriseSubmission(access.Actor, ctx));
            Check.That(package.Status is SubmissionStatus.Draft or SubmissionStatus.Checking or SubmissionStatus.Ready, "status", "review.frozen");
            await Coordination.Person(db, project, body.CoordinatorId, "coordinatorId");
            Check.That(body.CoordinatorId != package.CoordinatorId, "coordinatorId", "error.duplicate");
            var reason = Check.Reason(body.Reason);
            var previousCoordinatorId = package.CoordinatorId;
            package.CoordinatorId = body.CoordinatorId;
            await Invalidate(db, notify, package, reason);
            foreach (var check in await db.SubmissionChecks.Where(c => c.PackageId == id && c.ManifestVersion == package.ManifestVersion && c.Kind != SubmissionCheckKind.Applicability).ToListAsync())
                check.OwnerId = body.CoordinatorId;
            db.Audit.Note(package, reason: reason); await NotifyChanged(notify, project, package, previousCoordinatorId); return package;
        });

    static Task<Coordination.Result> Start(Guid projectId, Guid id, StartBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.start", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.CoordinateSubmission(access.Actor, ctx, package.CoordinatorId));
            Check.That(package.Status == SubmissionStatus.Draft, "status", "review.frozen");
            package.Status = SubmissionStatus.Checking;
            await db.SaveChangesAsync();
            if ((await SubmissionReadiness.Evaluate(db, package)).Ready) package.Status = SubmissionStatus.Ready;
            db.Audit.Note(package, reason: body.Reason);
            var owners = await db.SubmissionChecks.Where(c => c.PackageId == id && c.ManifestVersion == package.ManifestVersion && c.Kind == SubmissionCheckKind.Applicability)
                .Select(c => (Guid?)c.OwnerId).Distinct().ToArrayAsync();
            await NotifyChanged(notify, project, package, owners);
            return package;
        });

    static Task<Coordination.Result> CheckCommand(Guid projectId, Guid id, Guid checkId, CheckBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.check", id, checkId, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.PackageRowVersion);
            Check.That(package.Status is SubmissionStatus.Checking or SubmissionStatus.Ready, "status", "review.frozen");
            var check = await db.SubmissionChecks.SingleOrDefaultAsync(c => c.Id == checkId && c.PackageId == id && c.ManifestVersion == package.ManifestVersion) ?? throw ApiException.NotFound();
            Coordination.Version(check, body.RowVersion);
            Check.That(check.Kind == SubmissionCheckKind.Applicability && !check.Required, "checkId", "submission.derived_check");
            Check.OneOf(body.Action, [SubmissionCheckStatus.Pass, SubmissionCheckStatus.Fail, SubmissionCheckStatus.NotApplicable, SubmissionCheckStatus.Pending], "action");
            if (body.Action == SubmissionCheckStatus.NotApplicable) Access.Demand(Permissions.AuthoriseSubmission(access.Actor, ctx));
            else Access.Demand(Permissions.SignSubmissionCheck(access.Actor, ctx, check.OwnerId));
            await Coordination.Person(db, project, check.OwnerId);
            if (body.Action == SubmissionCheckStatus.Fail) Check.Reason(body.Reason);
            check.Status = body.Action; check.Reason = body.Action == SubmissionCheckStatus.Pending ? Check.Reason(body.Reason) : body.Action == SubmissionCheckStatus.NotApplicable ? Check.Reason(body.Reason) : body.Reason;
            check.EvidenceUrl = body.Action == SubmissionCheckStatus.Pending ? null : Coordination.Url(body.EvidenceUrl);
            check.ApprovedBy = body.Action == SubmissionCheckStatus.NotApplicable ? access.Me.Id : null;
            check.ApprovedAt = body.Action == SubmissionCheckStatus.NotApplicable ? clock.GetUtcNow() : null;
            if (check.EvidenceUrl is { } url) db.CheckEvidences.Add(new CheckEvidence { ProjectId = project.Id, CheckId = check.Id, EvidenceUrl = url, Note = check.Reason ?? "" });
            package.Status = SubmissionStatus.Checking;
            await db.SaveChangesAsync();
            if ((await SubmissionReadiness.Evaluate(db, package)).Ready) package.Status = SubmissionStatus.Ready;
            db.Audit.Note(check, reason: check.Reason); await NotifyChanged(notify, project, package); return check;
        });

    static Task<Coordination.Result> AssignCheck(Guid projectId, Guid id, Guid checkId, CheckAssignBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.check.assign", id, checkId, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.PackageRowVersion);
            Access.Demand(Permissions.AuthoriseSubmission(access.Actor, ctx));
            Check.That(package.Status is SubmissionStatus.Draft or SubmissionStatus.Checking or SubmissionStatus.Ready, "status", "review.frozen");
            var check = await db.SubmissionChecks.SingleOrDefaultAsync(c => c.Id == checkId && c.PackageId == id && c.ManifestVersion == package.ManifestVersion) ?? throw ApiException.NotFound();
            Coordination.Version(check, body.RowVersion);
            Check.That(check.Kind == SubmissionCheckKind.Applicability, "checkId", "submission.derived_check");
            await Coordination.Person(db, project, body.OwnerId);
            Check.That(body.OwnerId != check.OwnerId, "ownerId", "error.duplicate");
            var reason = Check.Reason(body.Reason);
            var previousOwnerId = check.OwnerId;
            check.OwnerId = body.OwnerId; check.Status = SubmissionCheckStatus.Pending; check.EvidenceUrl = null;
            check.ApprovedAt = null; check.ApprovedBy = null; check.Reason = null;
            package.Status = SubmissionStatus.Checking;
            db.Audit.Note(check, reason: reason); db.Audit.Note(package, reason: reason);
            await NotifyChanged(notify, project, package, previousOwnerId, check.OwnerId);
            return check;
        });

    static Task<Coordination.Result> Issue(Guid projectId, Guid id, IssueBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.issue", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.AuthoriseSubmission(access.Actor, ctx));
            Check.That(package.ManifestVersion == body.ManifestVersion && (package.Status is SubmissionStatus.Checking or SubmissionStatus.Ready), "manifestVersion", "coord.stale");
            var readiness = await SubmissionReadiness.Evaluate(db, package);
            if (readiness.Fingerprint != body.ExpectedFingerprint) throw ApiException.Conflict("submission_stale", "coord.stale", new { readiness.Blockers });
            if (!readiness.Ready) throw ApiException.Rule("submission_blocked", "submission.blocked", new { readiness.Blockers });
            foreach (var derived in await db.SubmissionChecks.Where(c => c.PackageId == id && c.ManifestVersion == package.ManifestVersion && c.Kind != SubmissionCheckKind.Applicability).ToListAsync())
                derived.Status = SubmissionCheckStatus.Pass;
            var manifest = await db.SubmissionManifestItems.Where(m => m.PackageId == id && m.ManifestVersion == package.ManifestVersion)
                .Join(db.SourceRevisions, m => m.SourceRevisionId, r => r.Id, (m, r) => new { m.DeliverableId, m.SourceRevisionId, m.ReviewRoundId, m.Required,
                    r.SourceKey, r.Revision, r.Url, r.Title, r.SourceSystem, r.ExternalIdentifier, r.Issuer, r.Scope, r.SourceCheckedAt,
                    ManuallyRegistered = true }).ToListAsync();
            var checks = await db.SubmissionChecks.Where(c => c.PackageId == id && c.ManifestVersion == package.ManifestVersion).ToListAsync();
            var checkIds = checks.Select(c => c.Id).ToArray();
            var evidence = await db.CheckEvidences.Where(e => checkIds.Contains(e.CheckId)).Select(e => new { e.CheckId, e.EvidenceUrl, e.Note, e.CreatedAt, e.CreatedBy }).ToListAsync();
            var issue = new SubmissionIssue { ProjectId = project.Id, PackageId = id, ManifestVersion = package.ManifestVersion,
                ManifestSnapshot = JsonSerializer.Serialize(manifest, JsonOpts.Web),
                CheckSnapshot = JsonSerializer.Serialize(new { Checks = checks.Select(c => new { c.Id, c.Kind, c.SourceId, c.OwnerId, c.Required, c.Status, c.EvidenceRule, c.EvidenceUrl, c.Reason, c.ApprovedBy, c.ApprovedAt }), Evidence = evidence, Readiness = readiness }, JsonOpts.Web),
                AuthorisedBy = access.Me.Id, AuthorisedAt = clock.GetUtcNow(), Destination = Check.Required(body.Destination, "destination", 1000), TransmittalUrl = Coordination.Url(body.TransmittalUrl) };
            db.SubmissionIssues.Add(issue); package.Status = SubmissionStatus.Issued;
            if (package.SupersedesPackageId is { } oldId) {
                var old = await Load(db, project.Id, oldId);
                Check.That(old.Status == SubmissionStatus.Issued, "supersedesPackageId", "coord.reference");
                old.Status = SubmissionStatus.Superseded; db.Audit.Note(old, reason: body.Reason);
            }
            db.Audit.Note(package, reason: body.Reason); await NotifyChanged(notify, project, package); return package;
        });

    static Task<Coordination.Result> Cancel(Guid projectId, Guid id, CancelBody body, Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "submission.cancel", id, body }, access, db, clock, async (project, ctx) => {
            var package = await Load(db, project.Id, id); Coordination.Version(package, body.RowVersion);
            Access.Demand(Permissions.AuthoriseSubmission(access.Actor, ctx));
            Check.That(SubmissionRules.Step(package.Status, SubmissionStatus.Cancelled), "status", "review.frozen");
            package.Status = SubmissionStatus.Cancelled; db.Audit.Note(package, reason: Check.Reason(body.Reason));
            await NotifyChanged(notify, project, package); return package;
        });

    static IQueryable<SubmissionPackage> Query(HubDb db, Guid projectId, Filter filter)
    {
        var query = db.SubmissionPackages.AsNoTracking().Where(p => p.ProjectId == projectId);
        if (!string.IsNullOrWhiteSpace(filter.Q)) {
            var term = $"%{SearchEndpoints.Escape(filter.Q.Trim())}%";
            query = query.Where(p => EF.Functions.ILike(p.Key, term, @"\") || EF.Functions.ILike(p.Title, term, @"\") || EF.Functions.ILike(p.Purpose, term, @"\"));
        }
        if (!string.IsNullOrWhiteSpace(filter.Status)) {
            Check.OneOf(filter.Status, SubmissionStatus.All, "status");
            query = filter.Status is SubmissionStatus.Ready or SubmissionStatus.Checking
                ? query.Where(p => p.Status == SubmissionStatus.Checking || p.Status == SubmissionStatus.Ready)
                : query.Where(p => p.Status == filter.Status);
        }
        if (filter.CoordinatorId is { } coordinator) query = query.Where(p => p.CoordinatorId == coordinator);
        if (filter.MilestoneId is { } milestone) query = query.Where(p => p.MilestoneId == milestone);
        if (filter.TargetFrom is { } from) query = query.Where(p => p.TargetDate >= from);
        if (filter.TargetTo is { } to) query = query.Where(p => p.TargetDate <= to);
        return query.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Seq);
    }

    static async Task<List<ListRow>> Rows(HubDb db, IReadOnlyList<SubmissionPackage> packages, string? status)
    {
        if (!string.IsNullOrWhiteSpace(status)) Check.OneOf(status, SubmissionStatus.All, "status");
        var userIds = packages.Select(p => p.CoordinatorId).Distinct().ToArray();
        var milestoneIds = packages.Select(p => p.MilestoneId).Distinct().ToArray();
        var names = await db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var milestones = await db.Milestones.Where(m => milestoneIds.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Name);
        var rows = new List<ListRow>();
        foreach (var package in packages)
        {
            var readiness = package.Status is SubmissionStatus.Checking or SubmissionStatus.Ready ? await SubmissionReadiness.Evaluate(db, package) : null;
            var effective = readiness is null ? package.Status : readiness.Ready ? SubmissionStatus.Ready : SubmissionStatus.Checking;
            if (!string.IsNullOrWhiteSpace(status) && effective != status) continue;
            rows.Add(new(package.Id, package.Key, package.Title, package.Purpose, effective, package.CoordinatorId, names.GetValueOrDefault(package.CoordinatorId),
                package.MilestoneId, milestones.GetValueOrDefault(package.MilestoneId), package.TargetDate, package.ManifestVersion, package.RowVersion, readiness?.Blockers.Count ?? 0));
        }
        return rows;
    }

    static async Task<List<ListRow>> FilteredRows(Guid projectId, Filter filter, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        Check.That(!filter.TargetFrom.HasValue || !filter.TargetTo.HasValue || filter.TargetTo >= filter.TargetFrom, "targetTo", "error.date_range");
        var packages = await Query(db, projectId, filter).Take(Export.MaxRows + 1).ToListAsync();
        if (packages.Count > Export.MaxRows) throw ApiException.Rule("export_too_large", "export.too_large", null, Export.MaxRows);
        return await Rows(db, packages, filter.Status);
    }

    static async Task<object> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db)
    {
        var (pg, size) = Http.Paging(page, pageSize);
        if (filter.Status is SubmissionStatus.Ready or SubmissionStatus.Checking) {
            // ponytail: derived status evaluates at most 50,000 candidates; move the shared readiness projection into SQL if projects reach this ceiling.
            var filtered = await FilteredRows(projectId, filter, access, db);
            return new Page<ListRow>(filtered.Skip((pg - 1) * size).Take(size).ToList(), pg, size, filtered.Count);
        }
        await access.Project(projectId, false);
        Check.That(!filter.TargetFrom.HasValue || !filter.TargetTo.HasValue || filter.TargetTo >= filter.TargetFrom, "targetTo", "error.date_range");
        var query = Query(db, projectId, filter);
        var rows = await Rows(db, await query.Skip((pg - 1) * size).Take(size).ToListAsync(), filter.Status);
        return new Page<ListRow>(rows, pg, size, await query.CountAsync());
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false);
        var rows = await FilteredRows(projectId, filter, access, db);
        return await ExportFile.Send(db, store, format, "Submission readiness", ListColumns,
            JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id,
            $"{project.ProjectNumber}-submissions", clock);
    }

    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var package = await Load(db, projectId, id);
        var readiness = package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled ? null : await SubmissionReadiness.Evaluate(db, package);
        return new { Package = package, Readiness = readiness,
            EffectiveStatus = readiness is null || package.Status == SubmissionStatus.Draft ? package.Status : readiness.Ready ? SubmissionStatus.Ready : SubmissionStatus.Checking,
            Manifest = await db.SubmissionManifestItems.Where(m => m.PackageId == id && m.ManifestVersion == package.ManifestVersion).ToListAsync(),
            Checks = await db.SubmissionChecks.Where(c => c.PackageId == id && c.ManifestVersion == package.ManifestVersion).ToListAsync(),
            Evidence = await db.CheckEvidences.Where(e => db.SubmissionChecks.Any(c => c.Id == e.CheckId && c.PackageId == id)).ToListAsync(),
            Issue = await db.SubmissionIssues.SingleOrDefaultAsync(i => i.PackageId == id),
            CanCoordinate = Permissions.CoordinateSubmission(access.Actor, ctx, package.CoordinatorId).Ok,
            CanAuthorise = Permissions.AuthoriseSubmission(access.Actor, ctx).Ok };
    }

    static async Task<IResult> ExportSnapshot(Guid projectId, Guid id, Access access, HubDb db, TimeProvider clock)
    {
        await access.Project(projectId, false);
        var package = await Load(db, projectId, id);
        var manifest = await db.SubmissionManifestItems.AsNoTracking().Where(m => m.PackageId == id).OrderBy(m => m.ManifestVersion).ThenBy(m => m.DeliverableId)
            .Join(db.SourceRevisions, m => m.SourceRevisionId, s => s.Id, (m, s) => new { m.ManifestVersion, m.DeliverableId, m.SourceRevisionId, m.ReviewRoundId, m.Required,
                s.SourceKey, s.Title, s.Revision, s.Url, s.SourceSystem, s.ExternalIdentifier, s.Issuer, s.Scope, s.SourceCheckedAt,
                ManuallyRegistered = true }).ToListAsync();
        var checks = await db.SubmissionChecks.AsNoTracking().Where(c => c.PackageId == id).OrderBy(c => c.ManifestVersion).ThenBy(c => c.Kind).ThenBy(c => c.Id).ToListAsync();
        var checkIds = checks.Select(c => c.Id).ToArray();
        var evidence = await db.CheckEvidences.AsNoTracking().Where(e => checkIds.Contains(e.CheckId)).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync();
        var related = new List<object>();
        var seen = new HashSet<Guid>();
        Guid? ancestor = id;
        while (ancestor is { } aid && seen.Add(aid))
        {
            var row = await db.SubmissionPackages.AsNoTracking().SingleOrDefaultAsync(p => p.Id == aid && p.ProjectId == projectId);
            if (row is null) break;
            var issue = await db.SubmissionIssues.AsNoTracking().SingleOrDefaultAsync(i => i.PackageId == aid);
            related.Add(new { row.Id, row.Key, row.Status, row.SupersedesPackageId, Issue = issue });
            ancestor = row.SupersedesPackageId;
        }
        var successors = await db.SubmissionPackages.AsNoTracking().Where(p => p.ProjectId == projectId && p.SupersedesPackageId == id)
            .Join(db.SubmissionIssues.AsNoTracking(), p => p.Id, i => i.PackageId, (p, i) => new { p.Id, p.Key, p.Status, p.SupersedesPackageId, Issue = i }).ToListAsync();
        var readiness = package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled ? null : await SubmissionReadiness.Evaluate(db, package);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { ExportedAt = clock.GetUtcNow(), Package = package, Manifest = manifest, Checks = checks,
            Evidence = evidence, Unresolved = readiness?.Blockers, IssueHistory = related, SuccessorIssues = successors }, JsonOpts.Web);
        return Results.File(bytes, "application/json", $"{package.Key}-submission.json");
    }
}
