using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ReviewEndpoints
{
    public sealed record Assignment(Guid ProjectDisciplineId, Guid ReviewerId, DateOnly DueDate);
    public sealed record CreateBody(Guid RequestId, string Title, string Purpose, Guid ProjectDisciplineId, Guid CoordinatorId, Guid[] SourceRevisionIds, Assignment[] Assignments, bool RequiredForIssue, string? Reason);
    public sealed record RoundBody(Guid RequestId, int RowVersion, string Purpose, Guid[] SourceRevisionIds, Assignment[] Assignments, string Reason, string? RemovalImpact);
    public sealed record ActionBody(Guid RequestId, int RowVersion, string Action, string? Reason);
    public sealed record DecisionBody(Guid RequestId, int RowVersion, string Status, string Rationale);
    public sealed record ReassignBody(Guid RequestId, int RowVersion, Guid OwnerId, string Reason);
    public sealed record FindingBody(Guid RequestId, int RowVersion, Guid SourceRevisionId, Guid ProjectDisciplineId, Guid ResolverId, string Text, string Severity, Guid? IssueId = null);
    public sealed record FindingAction(Guid RequestId, int RowVersion, string Action, string Reason, string? EvidenceUrl, Guid? OwnerId);
    public sealed record Filter(string? Q, string? Status, Guid? OwnerId, Guid? DisciplineId, bool? Mine);
    static readonly Col[] Columns = [new("key", "key"), new("title", "name"), new("status", "status"), new("roundNumber", "round"), new("outstandingDisciplines", "reviewOutstanding"), new("blockingFindings", "reviewBlocking"), new("waitingDays", "reviewWaiting")];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/reviews", List);
        api.MapGet("/projects/{projectId:guid}/reviews/options", Options);
        api.MapGet("/projects/{projectId:guid}/reviews/linked-issues", LinkedIssues);
        api.MapGet("/projects/{projectId:guid}/reviews/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/reviews", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/rounds", NewRound).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/action", Action).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assign", AssignCoordinator).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assignments/{assignmentId:guid}/decision", Decide).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/assignments/{assignmentId:guid}/assign", AssignReviewer).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/findings", AddFinding).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/findings/{findingId:guid}/action", FindingCommand).WithMetadata(new Coordination.AtomicCommand());
    }
    static async Task<ReviewPackage> Load(HubDb db, Guid project, Guid id) => await db.ReviewPackages.FirstOrDefaultAsync(x => x.ProjectId == project && x.Id == id) ?? throw ApiException.NotFound();
    static void Live(ReviewPackage p) => Check.That(p.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded), "status", "review.frozen");
    static async Task<ReviewRound> Round(HubDb db, ReviewPackage p) => await db.ReviewRounds.SingleAsync(r => r.Id == p.CurrentRoundId);
    static async Task<Guid[]> Authors(HubDb db, Guid round)
    {
        var manifest = await db.ReviewManifestItems.Where(m => m.RoundId == round).ToListAsync();
        var authors = manifest.SelectMany(m => m.AuthorIds).ToHashSet();
        foreach (var item in manifest) authors.UnionWith(await Coordination.Authors(db, item.DeliverableId));
        return authors.ToArray();
    }
    static async Task<Guid[]> FindingAuthors(HubDb db, ReviewFinding finding)
    {
        var ids = new HashSet<Guid>(); var seen = new HashSet<Guid>(); ReviewFinding? cursor = finding;
        while (cursor != null && seen.Add(cursor.Id)) {
            ids.UnionWith(await Authors(db, cursor.RoundId)); ids.Add(cursor.ResolverId);
            ids.UnionWith((await db.FindingEvents.Where(e => e.FindingId == cursor.Id && e.Action == FindingStatus.Responded).Select(e => e.CreatedBy).ToListAsync()).OfType<Guid>());
            cursor = cursor.CarriedFromId is { } prior ? await db.ReviewFindings.SingleAsync(f => f.Id == prior) : null;
        }
        return ids.ToArray();
    }
    static async Task Independent(HubDb db, Project project, Guid reviewer, Guid[] authors, bool allowSelf)
    { await Coordination.Person(db, project, reviewer, "reviewerId"); Check.That(ReviewRules.Independent(reviewer, authors, allowSelf), "reviewerId", "review.independent"); }
    static async Task ValidateRound(HubDb db, Project project, ReviewPackage package, bool allowSelf)
    {
        await Coordination.Person(db, project, package.CoordinatorId, "coordinatorId");
        var items = await db.ReviewManifestItems.Where(m => m.RoundId == package.CurrentRoundId).ToListAsync();
        Check.That(items.Count > 0, "sourceRevisionIds", "error.required");
        foreach (var item in items) {
            await Coordination.Target(db, project, "Deliverable", item.DeliverableId);
            var revision = await Coordination.Revision(db, project.Id, item.SourceRevisionId);
            Check.That(await Coordination.Published(db, revision), "sourceRevisionId", "review.source_unpublished");
            var head = await Coordination.Head(db, revision);
            Check.That(head is null || head.CurrentRevisionId == revision.Id, "sourceRevisionId", "review.source_changed");
        }
        var authors = await Authors(db, package.CurrentRoundId!.Value);
        foreach (var a in await db.DisciplineReviews.Where(a => a.RoundId == package.CurrentRoundId).ToListAsync()) {
            await Coordination.Discipline(db, project.Id, a.ProjectDisciplineId);
            await Independent(db, project, a.ReviewerId, authors, allowSelf);
        }
    }
    static async Task<ReviewRound> BuildRound(HubDb db, Project project, ReviewPackage p, string purpose, Guid[] sources, Assignment[] assignments, string? reason, string? impact, bool allowSelf)
    {
        Check.That(sources is { Length: > 0 and <= 200 } && sources.Distinct().Count() == sources.Length, "sourceRevisionIds", "review.manifest");
        Check.That(assignments is { Length: > 0 and <= 50 } && assignments.Select(a => a.ProjectDisciplineId).Distinct().Count() == assignments.Length, "assignments", "review.assignments");
        var round = new ReviewRound { ProjectId = project.Id, PackageId = p.Id, Number = p.RoundNumber + 1, Purpose = Check.Required(purpose, "purpose", 2000), Reason = reason, RemovalImpact = impact };
        db.ReviewRounds.Add(round);
        var authors = new HashSet<Guid>(); var deliverables = new HashSet<Guid>();
        foreach (var rid in sources!) {
            var r = await Coordination.Revision(db, project.Id, rid);
            Check.That(r.DeliverableId is not null && deliverables.Add(r.DeliverableId.Value), "sourceRevisionIds", "review.manifest");
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
    static async Task Notify(HubDb db, Notifier notify, Project project, ReviewPackage p, IEnumerable<Guid> owners) =>
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
    static async Task Recompute(HubDb db, Project project, ReviewPackage package, bool allowSelf)
    {
        await db.SaveChangesAsync();
        var r = await Round(db, package);
        if (r.StartedAt is null) return;
        var states = await db.DisciplineReviews.Where(a => a.RoundId == r.Id).Select(a => a.Status).ToListAsync();
        var findings = await db.ReviewFindings.Where(f => f.RoundId == r.Id).ToListAsync();
        var status = ReviewRules.PackageStatus(states, findings.Any(f => ReviewRules.BlockingOpen(f.Severity, f.Status, f.WithdrawalAcknowledgedBy != null)));
        if (status == ReviewStatus.Approved) await ValidateRound(db, project, package, allowSelf);
        package.Status = r.Status = status;
        await SubmissionEndpoints.InvalidateForReviewPackage(db, project.Id, package.Id);
    }
    static Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, SettingsStore settings, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.create", body }, access, db, clock, async (project, ctx) => {
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, body.ProjectDisciplineId));
            await Coordination.Person(db, project, body.CoordinatorId, "coordinatorId"); await Coordination.Discipline(db, project.Id, body.ProjectDisciplineId);
            var p = new ReviewPackage { ProjectId = project.Id, Title = Check.Required(body.Title, "title"), Purpose = Check.Required(body.Purpose, "purpose", 2000), CoordinatorId = body.CoordinatorId, ProjectDisciplineId = body.ProjectDisciplineId, RequiredForIssue = body.RequiredForIssue };
            (p.Seq, p.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "review"); db.ReviewPackages.Add(p);
            var r = await BuildRound(db, project, p, body.Purpose, body.SourceRevisionIds, body.Assignments, body.Reason, null, (await settings.Get(db)).AllowSelfReview);
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
        });
    static Task<Coordination.Result> NewRound(Guid projectId, Guid id, RoundBody body, Access access, HubDb db, SettingsStore settings, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.round", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion);
            Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
            var reason = Check.Reason(body.Reason); var old = await Round(db, p);
            var oldDisc = await db.DisciplineReviews.Where(a => a.RoundId == old.Id).Select(a => a.ProjectDisciplineId).ToListAsync();
            if (oldDisc.Except((body.Assignments ?? []).Select(a => a.ProjectDisciplineId)).Any()) {
                Access.Demand(Permissions.ManageTeam(access.Actor, ctx)); Check.Required(body.RemovalImpact, "removalImpact", 4000);
            }
            var round = await BuildRound(db, project, p, body.Purpose, body.SourceRevisionIds, body.Assignments!, reason, body.RemovalImpact, (await settings.Get(db)).AllowSelfReview);
            var manifest = db.ReviewManifestItems.Local.Where(m => m.RoundId == round.Id).ToList();
            var oldDeliverables = await db.ReviewManifestItems.Where(m => m.RoundId == old.Id).Select(m => m.DeliverableId).ToListAsync();
            var newDeliverables = manifest.Select(m => m.DeliverableId).ToHashSet();
            var removedDeliverables = oldDeliverables.Where(d => !newDeliverables.Contains(d)).ToArray();
            if (removedDeliverables.Length > 0) {
                Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
                Check.Required(body.RemovalImpact, "removalImpact", 4000);
            }
            foreach (var f in await db.ReviewFindings.Where(f => f.RoundId == old.Id && (f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.Severity == "Blocking" && f.WithdrawalAcknowledgedBy == null))).ToListAsync()) {
                var source = await Coordination.Revision(db, project.Id, f.SourceRevisionId);
                db.ReviewFindings.Add(new ReviewFinding { ProjectId = project.Id, PackageId = p.Id, RoundId = round.Id, CarriedFromId = f.Id,
                    IssueId = f.IssueId,
                    SourceRevisionId = manifest.FirstOrDefault(m => m.DeliverableId == source.DeliverableId)?.SourceRevisionId ?? f.SourceRevisionId,
                    ProjectDisciplineId = f.ProjectDisciplineId, OriginatorId = f.OriginatorId, ResolverId = f.ResolverId, VerifierId = f.VerifierId, Text = f.Text, Severity = f.Severity });
            }
            old.Status = ReviewStatus.Superseded; db.Audit.Note(old, reason: reason);
            await db.SaveChangesAsync(); p.CurrentRoundId = round.Id; p.RoundNumber = round.Number; p.Purpose = round.Purpose; p.Status = ReviewStatus.Draft;
            await SubmissionEndpoints.InvalidateForReviewPackage(db, project.Id, p.Id);
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
        });
    static Task<Coordination.Result> Action(Guid projectId, Guid id, ActionBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.action", id, body }, access, db, clock, async (project, ctx) => {
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
            }
            await Notify(db, notify, project, p, await db.DisciplineReviews.Where(a => a.RoundId == round.Id).Select(a => a.ReviewerId).ToListAsync()); return p;
        });
    static Task<Coordination.Result> AssignCoordinator(Guid projectId, Guid id, ReassignBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.coordinator", id, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Coordination.Version(p, body.RowVersion); Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, p.ProjectDisciplineId));
            await Coordination.Person(db, project, body.OwnerId); p.CoordinatorId = body.OwnerId; db.Audit.Note(p, reason: Check.Reason(body.Reason)); return p;
        });
    static Task<Coordination.Result> AssignReviewer(Guid projectId, Guid id, Guid assignmentId, ReassignBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.reviewer", id, assignmentId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); Access.Demand(Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId));
            var a = await db.DisciplineReviews.SingleOrDefaultAsync(a => a.Id == assignmentId && a.RoundId == p.CurrentRoundId) ?? throw ApiException.NotFound(); Coordination.Version(a, body.RowVersion);
            await Independent(db, project, body.OwnerId, await Authors(db, a.RoundId), (await settings.Get(db)).AllowSelfReview);
            a.ReviewerId = body.OwnerId; a.Status = DisciplineReviewStatus.Pending; a.DecidedAt = null; a.DecidedBy = null; a.Rationale = null; db.Audit.Note(a, reason: Check.Reason(body.Reason));
            await Recompute(db, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [body.OwnerId]); return a;
        });
    static Task<Coordination.Result> Decide(Guid projectId, Guid id, Guid assignmentId, DecisionBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.decision", id, assignmentId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p); var round = await Round(db, p); Check.That(round.StartedAt != null, "status", "review.start_first");
            var a = await db.DisciplineReviews.SingleOrDefaultAsync(a => a.Id == assignmentId && a.RoundId == round.Id) ?? throw ApiException.NotFound(); Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, a.ReviewerId));
            await ValidateRound(db, project, p, (await settings.Get(db)).AllowSelfReview);
            Check.OneOf(body.Status, [DisciplineReviewStatus.InReview, DisciplineReviewStatus.ChangesRequired, DisciplineReviewStatus.Approved], "status");
            a.Status = body.Status; a.Rationale = Check.Required(body.Rationale, "rationale", 4000); a.DecidedAt = clock.GetUtcNow(); a.DecidedBy = access.Me.Id;
            await Recompute(db, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [p.CoordinatorId]); return a;
        });
    static Task<Coordination.Result> AddFinding(Guid projectId, Guid id, FindingBody body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.finding", id, body }, access, db, clock, async (project, ctx) => {
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
            db.ReviewFindings.Add(f); await Recompute(db, project, p, (await settings.Get(db)).AllowSelfReview); await Notify(db, notify, project, p, [f.ResolverId]); return f;
        });
    static Task<Coordination.Result> FindingCommand(Guid projectId, Guid id, Guid findingId, FindingAction body, Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "review.finding.action", id, findingId, body }, access, db, clock, async (project, ctx) => {
            var p = await Load(db, project.Id, id); Live(p);
            var f = await db.ReviewFindings.SingleOrDefaultAsync(f => f.Id == findingId && f.PackageId == id && f.RoundId == p.CurrentRoundId) ?? throw ApiException.NotFound(); Coordination.Version(f, body.RowVersion);
            var reason = Check.Reason(body.Reason); var self = (await settings.Get(db)).AllowSelfReview;
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
                } else { Check.That(ReviewRules.Independent(f.VerifierId, [person], self), "ownerId", "review.independent"); f.ResolverId = person; f.Status = FindingStatus.Open; f.Response = null; f.EvidenceUrl = null; }
            } else if (body.Action == "acknowledgeWithdrawal") {
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, p.CoordinatorId)); await Coordination.Person(db, project, p.CoordinatorId);
                Check.That(f.Status == FindingStatus.Withdrawn && f.Severity == "Blocking", "status", "review.frozen"); f.WithdrawalAcknowledgedBy = access.Me.Id;
            } else {
                Check.That(ReviewRules.FindingStep(f.Status, body.Action), "status", "review.finding_transition");
                var actor = body.Action == FindingStatus.Responded ? f.ResolverId : f.VerifierId;
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, actor)); await Coordination.Person(db, project, actor);
                if (body.Action != FindingStatus.Responded) await Independent(db, project, actor, await FindingAuthors(db, f), self);
                if (body.Action == FindingStatus.Responded) { f.Response = reason; f.EvidenceUrl = Coordination.Url(body.EvidenceUrl); }
                if (body.Action == FindingStatus.VerifiedClosed) Check.That(f.Response != null && f.EvidenceUrl != null, "evidenceUrl", "error.required");
                f.Status = body.Action;
            }
            db.Audit.Note(f, reason: reason); db.FindingEvents.Add(new FindingEvent { ProjectId = project.Id, FindingId = f.Id, Action = body.Action, Reason = reason, EvidenceUrl = body.EvidenceUrl });
            await Recompute(db, project, p, self); await Notify(db, notify, project, p, [f.ResolverId, f.VerifierId, p.CoordinatorId]); return f;
        });
    public static async Task Gate(HubDb db, Project project, Deliverable deliverable, string? issuingRevision, bool allowSelf, string? issuingUrl = null)
    {
        if (deliverable.RequiredReviewPackageId is not { } pid) return;
        var p = await Load(db, project.Id, pid);
        Check.That(p.Status == ReviewStatus.Approved, "requiredReviewPackageId", "review.gate");
        await ValidateRound(db, project, p, allowSelf);
        var revision = await db.ReviewManifestItems.Where(m => m.RoundId == p.CurrentRoundId && m.DeliverableId == deliverable.Id).Join(db.SourceRevisions, m => m.SourceRevisionId, r => r.Id, (m, r) => r).SingleOrDefaultAsync();
        Check.That(revision != null && revision.Revision == (issuingRevision ?? deliverable.Revision) && (issuingUrl == null || revision.Url == issuingUrl), "revision", "review.gate_revision");
    }
    static IQueryable<ReviewPackage> Query(HubDb db, Guid project, Filter f, Guid actor)
    {
        var q = db.ReviewPackages.AsNoTracking().Where(p => p.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(f.Q)) q = q.Where(p => p.Title.Contains(f.Q) || p.Key.Contains(f.Q));
        if (f.Status is { Length: > 0 }) { Check.OneOf(f.Status, ReviewStatus.All, "status"); q = q.Where(p => p.Status == f.Status); }
        if (f.OwnerId is { } owner) q = q.Where(p => p.CoordinatorId == owner);
        if (f.DisciplineId is { } discipline) q = q.Where(p => db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ProjectDisciplineId == discipline));
        if (f.Mine == true) q = q.Where(p => p.CoordinatorId == actor || db.DisciplineReviews.Any(a => a.RoundId == p.CurrentRoundId && a.ReviewerId == actor));
        return q.OrderByDescending(p => p.UpdatedAt).ThenBy(p => p.Seq);
    }
    static async Task<List<object>> Rows(HubDb db, IQueryable<ReviewPackage> query, DateTimeOffset now)
    {
        var rows = await query.Select(p => new { P = p, OutstandingDisciplines = db.DisciplineReviews.Count(a => a.RoundId == p.CurrentRoundId && a.Status != DisciplineReviewStatus.Approved),
            BlockingFindings = db.ReviewFindings.Count(f => f.RoundId == p.CurrentRoundId && f.Severity == "Blocking" && f.Status != FindingStatus.VerifiedClosed && (f.Status != FindingStatus.Withdrawn || f.Severity == "Blocking" && f.WithdrawalAcknowledgedBy == null)),
            Started = db.ReviewRounds.Where(r => r.Id == p.CurrentRoundId).Select(r => r.StartedAt).FirstOrDefault() }).ToListAsync();
        return rows.Select(x => (object)new { x.P.Id, x.P.Key, x.P.Title, x.P.Status, x.P.CoordinatorId, x.P.RoundNumber, x.P.RowVersion, x.OutstandingDisciplines, x.BlockingFindings, WaitingDays = x.Started is { } start && (x.P.Status == ReviewStatus.InReview || x.P.Status == ReviewStatus.ChangesRequired) ? Math.Max(0, (int)(now - start).TotalDays) : 0 }).ToList();
    }
    static async Task<object> List(Guid projectId, [AsParameters] Filter filter, int? page, int? pageSize, Access access, HubDb db, TimeProvider clock)
    { await access.Project(projectId, false); var q = Query(db, projectId, filter, access.Me.Id); var (pg, size) = Http.Paging(page, pageSize); return new Page<object>(await Rows(db, q.Skip((pg - 1) * size).Take(size), clock.GetUtcNow()), pg, size, await q.CountAsync()); }
    static async Task<object> LinkedIssues(Guid projectId, Guid? disciplineId, Guid? ownerId, int? page, int? pageSize, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        var q = db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId &&
            db.ReviewFindings.Any(f => f.ProjectId == projectId && f.IssueId == i.Id &&
                db.ReviewPackages.Any(p => p.Id == f.PackageId && p.ProjectId == projectId && p.CurrentRoundId == f.RoundId) &&
                (disciplineId == null || f.ProjectDisciplineId == disciplineId || i.ProjectDisciplineId == disciplineId ||
                    db.IssueAffectedDisciplines.Any(x => x.IssueId == i.Id && x.ProjectDisciplineId == disciplineId))));
        if (ownerId is { } owner) q = q.Where(i => i.OwnerId == owner);
        var (pg, size) = Http.Paging(page, pageSize);
        var total = await q.CountAsync();
        var rows = await q.OrderBy(i => i.Key).Skip((pg - 1) * size).Take(size)
            .Select(i => new { i.Id, i.Key, i.Title, i.Status, i.OwnerId,
                OwnerName = db.Users.Where(u => u.Id == i.OwnerId).Select(u => u.DisplayName).FirstOrDefault(),
                i.ProjectDisciplineId }).ToListAsync();
        return new { Items = rows, TotalCount = total, Page = pg, PageSize = size };
    }
    static async Task<object> Options(Guid projectId, Access access, HubDb db) => await ChangeEndpoints.Options(projectId, access, db);
    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (project, ctx) = await access.Project(projectId, false); var p = await Load(db, projectId, id);
        var rounds = await db.ReviewRounds.Where(r => r.PackageId == id).OrderByDescending(r => r.Number).ToListAsync(); var ids = rounds.Select(r => r.Id).ToArray();
        return new { Package = p, Rounds = rounds, Assignments = await db.DisciplineReviews.Where(a => ids.Contains(a.RoundId)).ToListAsync(), Findings = await db.ReviewFindings.Where(f => f.PackageId == id).OrderBy(f => f.CreatedAt).ThenBy(f => f.Id).ToListAsync(),
            Manifest = await db.ReviewManifestItems.Where(m => ids.Contains(m.RoundId)).Join(db.SourceRevisions, m => m.SourceRevisionId, s => s.Id, (m, s) => new { m.Id, m.RoundId, m.DeliverableId, m.SourceRevisionId, m.AuthorIds, Source = s }).ToListAsync(),
            Events = await db.FindingEvents.Where(e => db.ReviewFindings.Any(f => f.Id == e.FindingId && f.PackageId == id)).OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync(),
            EligiblePeople = await Coordination.People(db, project).Select(u => u.Id).ToListAsync(), ActorId = access.Me.Id,
            CanCoordinate = Permissions.CoordinateReview(access.Actor, ctx, p.ProjectDisciplineId, p.CoordinatorId).Ok, CanManage = Permissions.ManageCoordination(access.Actor, ctx, p.ProjectDisciplineId).Ok,
            CanWrite = Permissions.CoordinationWrite(access.Actor, ctx).Ok, CanReassignVerifier = Permissions.ManageTeam(access.Actor, ctx).Ok };
    }
    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false); var rows = await Rows(db, Query(db, projectId, filter, access.Me.Id).Take(Export.MaxRows + 1), clock.GetUtcNow());
        return await ExportFile.Send(db, store, format, Text.Get("export.reviews", project.ProjectNumber), Columns, JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id, $"{project.ProjectNumber}-reviews", clock);
    }
}
