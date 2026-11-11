using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ExternalReviewCommentBody(string Text, Guid ProjectDisciplineId, Guid ResolverId, string Severity, Guid? IssueId = null);
public sealed record ExternalReviewRecordBody(Guid RequestId, int? RowVersion, Guid DeliverableIssueId, int? IssueRowVersion,
    Guid ExternalPartyId, int? PartyRowVersion, DateOnly ReceivedDate, Guid ReturnCodeId, int? ReturnCodeRowVersion,
    string Reason, ExternalReviewCommentBody[] Comments);
public sealed record ExternalReviewPartyRead(Guid Id, int RowVersion, string Name, string? Organisation);
public sealed record ExternalReviewCodeRead(Guid Id, int RowVersion, string Code, string Name);
public sealed record ExternalReviewOptions(IReadOnlyList<ReviewIssuedSourceRead> IssuedRevisions,
    IReadOnlyList<ExternalReviewPartyRead> Parties, IReadOnlyList<ExternalReviewCodeRead> ReturnCodes, bool CanRecord);
public sealed record ExternalReviewRead(Guid Id, int RowVersion, Guid PackageId, Guid RoundId, Guid DeliverableIssueId,
    Guid ExternalPartyId, string PartyName, string? PartyOrganisation, string PartyState, DateOnly ReceivedDate,
    Guid ReturnCodeId, string ReturnCode, string ReturnLabel, Guid? RecordedBy, DateTimeOffset RecordedAt, int CommentCount);

public static class ExternalReviewEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/reviews/issued-sources", Sources).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapGet("/projects/{projectId:guid}/review-issued-assignment-guidance", Guidance).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}/external-reviews/options", Options).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}/external-reviews", List).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapGet("/projects/{projectId:guid}/reviews/{id:guid}/external-reviews/{externalReviewId:guid}", Detail).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapPost("/projects/{projectId:guid}/reviews/{id:guid}/external-reviews", Record)
            .RequireFlag(DeliverableEndpoints.RegisterFlag).WithMetadata(new Coordination.AtomicCommand());
    }

    static async Task<Project> ReadScope(Guid projectId, Access access, HubDb db)
    {
        Http.RequireFlag(await db.CurrentSettings(), DeliverableEndpoints.RegisterFlag);
        return (await access.Project(projectId, false)).Project;
    }
    static async Task<ReviewPackage> Package(Guid projectId, Guid id, Access access, HubDb db)
    {
        await ReadScope(projectId, access, db);
        var package = await db.ReviewPackages.AsNoTracking().FirstOrDefaultAsync(row => row.ProjectId == projectId && row.Id == id)
            ?? throw ApiException.NotFound();
        if (!await ReviewIssuedSources.IsIssuedPackage(db, id)) throw ApiException.NotFound();
        return package;
    }
    static async Task<Ok<Page<ReviewIssuedSourceRead>>> Sources(Guid projectId, Guid? deliverableId, string? q, int? page, int? pageSize, Access access, HubDb db)
    {
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        await ReadScope(projectId, access, db);
        var sources = db.DeliverableIssues.AsNoTracking().Where(issue => issue.ProjectId == projectId
            && db.Deliverables.Any(deliverable => deliverable.Id == issue.DeliverableId && deliverable.ProjectId == projectId));
        if (deliverableId is { } deliverableFilter) sources = sources.Where(issue => issue.DeliverableId == deliverableFilter);
        if (!string.IsNullOrWhiteSpace(q)) sources = sources.Where(issue => issue.Revision != null && issue.Revision.Contains(q)
            || db.Deliverables.Any(deliverable => deliverable.Id == issue.DeliverableId && (deliverable.Key.Contains(q) || deliverable.Name.Contains(q))));
        var (pg, size) = Http.Paging(page, pageSize);
        var total = await sources.CountAsync(); var rows = new List<ReviewIssuedSourceRead>(); var offset = (long)(pg - 1) * size;
        Guid[] selected = offset > int.MaxValue ? [] : await sources.OrderByDescending(issue => issue.CreatedAt).ThenBy(issue => issue.Id)
            .Skip((int)offset).Take(size).Select(issue => issue.Id).ToArrayAsync();
        var hydrated = await ReviewIssuedSources.ReadMany(db, projectId, selected);
        foreach (var id in selected) rows.Add(ReviewIssuedSources.Row(hydrated[id]));
        return TypedResults.Ok(new Page<ReviewIssuedSourceRead>(rows, pg, size, total));
    }
    static async Task<Ok<ReviewIssuedAssignmentGuidance>> Guidance(Guid projectId, string? issuedRevisionIds, Access access, HubDb db)
    {
        var project = await ReadScope(projectId, access, db); var values = Http.List(issuedRevisionIds);
        Check.That(values.Length <= 200 && values.All(value => Guid.TryParse(value, out var id) && id != Guid.Empty), "issuedRevisionIds", "review.manifest_rule");
        return TypedResults.Ok(await ReviewIssuedSources.Guidance(db, project, values.Select(Guid.Parse).Distinct().ToArray(), await db.CurrentSettings()));
    }
    static async Task<Ok<ExternalReviewOptions>> Options(Guid projectId, Guid id, Access access, HubDb db)
    {
        var package = await Package(projectId, id, access, db); var (_, context) = await access.Project(projectId, false);
        var sourceIds = await db.ReviewManifestItems.AsNoTracking().Where(item => item.ProjectId == projectId && item.RoundId == package.CurrentRoundId)
            .Select(item => item.DeliverableIssueId).ToArrayAsync();
        var selected = sourceIds.Select(issue => issue ?? throw ApiException.Invalid("deliverableIssueId", "externalReview.integrity")).ToArray();
        var hydrated = await ReviewIssuedSources.ReadMany(db, projectId, selected);
        var sources = selected.Select(issue => ReviewIssuedSources.Row(hydrated[issue])).ToArray();
        var parties = await db.ExternalParties.AsNoTracking().Where(party => party.ProjectId == projectId && party.IsActive).OrderBy(party => party.Name).ThenBy(party => party.Id)
            .Select(party => new ExternalReviewPartyRead(party.Id, party.RowVersion, party.Name, party.Organisation)).ToArrayAsync();
        var codes = await db.Set<IssueReturnCode>().AsNoTracking().Where(code => code.IsActive).OrderBy(code => code.SortOrder).ThenBy(code => code.Code)
            .Select(code => new ExternalReviewCodeRead(code.Id, code.RowVersion, code.Code, code.Name)).ToArrayAsync();
        var started = await db.ReviewRounds.AsNoTracking().AnyAsync(round => round.Id == package.CurrentRoundId && round.PackageId == id && round.ProjectId == projectId && round.StartedAt != null);
        return TypedResults.Ok(new ExternalReviewOptions(sources, parties, codes, (await RecordPermission(db, access.Actor, context)).Ok
            && package.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded) && started && sources.Length > 0 && sources.All(source => source.Selectable)));
    }
    static async Task<Ok<Page<ExternalReviewRead>>> List(Guid projectId, Guid id, int? page, int? pageSize, Access access, HubDb db)
    {
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        await Package(projectId, id, access, db); var (pg, size) = Http.Paging(page, pageSize); var offset = (long)(pg - 1) * size;
        var query = db.Set<ExternalReview>().AsNoTracking().Where(row => row.PackageId == id);
        var total = await query.CountAsync();
        List<ExternalReview> selected = offset > int.MaxValue ? [] : await query.OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).Skip((int)offset).Take(size).ToListAsync();
        return TypedResults.Ok(new Page<ExternalReviewRead>(await Hydrate(db, projectId, id, selected), pg, size, total));
    }
    static async Task<Ok<ExternalReviewRead>> Detail(Guid projectId, Guid id, Guid externalReviewId, Access access, HubDb db)
    {
        await Package(projectId, id, access, db);
        var receipt = await db.Set<ExternalReview>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == externalReviewId && row.PackageId == id) ?? throw ApiException.NotFound();
        return TypedResults.Ok((await Hydrate(db, projectId, id, [receipt])).Single());
    }

    internal static async Task<List<ExternalReviewRead>> ReadAll(HubDb db, Guid projectId, Guid packageId)
    {
        var receipts = await db.Set<ExternalReview>().AsNoTracking().Where(row => row.PackageId == packageId)
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).ToListAsync();
        return await Hydrate(db, projectId, packageId, receipts);
    }
    static async Task<List<ExternalReviewRead>> Hydrate(HubDb db, Guid projectId, Guid packageId, IReadOnlyList<ExternalReview> receipts)
    {
        if (receipts.Count == 0) return [];
        var receiptIds = receipts.Select(row => row.Id).ToArray(); var roundIds = receipts.Select(row => row.RoundId).Distinct().ToArray();
        var rounds = await db.ReviewRounds.AsNoTracking().Where(round => roundIds.Contains(round.Id) && round.PackageId == packageId && round.ProjectId == projectId).Select(round => round.Id).ToArrayAsync();
        var partyIds = receipts.Select(row => row.ExternalPartyId).Distinct().ToArray();
        var parties = await db.ExternalParties.AsNoTracking().Where(party => party.ProjectId == projectId && partyIds.Contains(party.Id)).ToDictionaryAsync(party => party.Id);
        var returnIds = receipts.Select(row => row.ReturnCodeId).Distinct().ToArray();
        var returnCodes = await db.Set<IssueReturnCode>().AsNoTracking().Where(code => returnIds.Contains(code.Id)).Select(code => code.Id).ToArrayAsync();
        var issueIds = receipts.Select(row => row.DeliverableIssueId).Distinct().ToArray();
        var issues = await db.DeliverableIssues.AsNoTracking().Where(issue => issue.ProjectId == projectId && issueIds.Contains(issue.Id)).ToDictionaryAsync(issue => issue.Id);
        var deliverableIds = issues.Values.Select(issue => issue.DeliverableId).Distinct().ToArray();
        var deliverables = await db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(deliverable => deliverable.ProjectId == projectId && deliverableIds.Contains(deliverable.Id)).Select(deliverable => deliverable.Id).ToArrayAsync();
        var manifest = await db.ReviewManifestItems.AsNoTracking().Where(item => rounds.Contains(item.RoundId)).ToArrayAsync();
        Check.That(receipts.All(row => row.ProjectId == projectId && rounds.Contains(row.RoundId) && parties.ContainsKey(row.ExternalPartyId) && returnCodes.Contains(row.ReturnCodeId)
            && issues.TryGetValue(row.DeliverableIssueId, out var issue) && deliverables.Contains(issue.DeliverableId)
            && manifest.Any(item => item.ProjectId == projectId && item.RoundId == row.RoundId && item.DeliverableId == issue.DeliverableId
                && item.DeliverableIssueId == issue.Id && item.SourceRevisionId is null)), "externalReviewId", "externalReview.integrity");
        var counts = await db.ReviewFindings.AsNoTracking().Where(finding => finding.ProjectId == projectId && finding.PackageId == packageId
                && finding.ExternalReviewId != null && receiptIds.Contains(finding.ExternalReviewId.Value) && finding.CarriedFromId == null).GroupBy(finding => finding.ExternalReviewId!.Value)
            .Select(group => new ExternalReviewFindingCount(group.Key, group.Count())).ToDictionaryAsync(value => value.Id, value => value.Count);
        return receipts.Select(row => new ExternalReviewRead(row.Id, row.RowVersion, row.PackageId, row.RoundId, row.DeliverableIssueId, row.ExternalPartyId,
            row.PartyNameSnapshot, row.PartyOrganisationSnapshot, parties[row.ExternalPartyId].IsActive ? "Active" : "Inactive", row.ReceivedDate,
            row.ReturnCodeId, row.ReturnCodeSnapshot, row.ReturnLabelSnapshot, row.CreatedBy, row.CreatedAt, counts.GetValueOrDefault(row.Id))).ToList();
    }
    internal static ReviewRegisterOriginator Originator(ExternalReviewRead review) =>
        new("ExternalParty", null, review.ExternalPartyId, review.PartyName, review.PartyState, review.PartyOrganisation);

    internal static async Task<Allow> RecordPermission(HubDb db, Actor actor, ProjectContext context)
    {
        var activeLeads = await db.ProjectDisciplines.AsNoTracking().Where(discipline => discipline.ProjectId == context.ProjectId
            && discipline.IsActive && discipline.LeadUserId == actor.Id).Select(discipline => discipline.Id).ToArrayAsync();
        return Permissions.RecordExternalReview(actor, context with { LeadDisciplineIds = activeLeads.ToHashSet() });
    }

    static async Task<Coordination.Result> Record(Guid projectId, Guid id, ExternalReviewRecordBody body,
        Access access, HubDb db, SettingsStore settings, Notifier notify, TimeProvider clock)
    {
        await Package(projectId, id, access, db);
        var (_, scope) = await access.Project(projectId, false); Access.Demand(await RecordPermission(db, access.Actor, scope));
        var payload = new ExternalReviewRecordPayload("review.external.record", id, body);
        return await Coordination.Run(projectId, body.RequestId, payload, access, db, clock, async (project, context) =>
        {
            Access.Demand(await RecordPermission(db, access.Actor, context));
            var package = await ReviewEndpoints.Load(db, projectId, id); ReviewEndpoints.Live(package); Coordination.Version(package, body.RowVersion);
            var round = await db.ReviewRounds.SingleOrDefaultAsync(value => value.Id == package.CurrentRoundId && value.ProjectId == projectId && value.PackageId == id) ?? throw ApiException.NotFound();
            Check.That(round.StartedAt is not null, "status", "review.start_first");
            await ReviewEndpoints.Authors(db, round.Id); // Unknown provenance is not a self-review exception or clerical recording permission.
            var source = await ReviewIssuedSources.Read(db, projectId, body.DeliverableIssueId); Version(source.Issue.RowVersion, body.IssueRowVersion);
            ReviewIssuedSources.Require(source);
            Check.That(await db.ReviewManifestItems.AnyAsync(item => item.RoundId == round.Id && item.ProjectId == projectId
                && item.DeliverableIssueId == body.DeliverableIssueId && item.DeliverableId == source.Deliverable.Id && item.SourceRevisionId == null), "deliverableIssueId", "coord.reference");
            var party = await db.ExternalParties.AsNoTracking().SingleOrDefaultAsync(value => value.Id == body.ExternalPartyId && value.ProjectId == projectId && value.IsActive) ?? throw ApiException.Invalid("externalPartyId", "coord.reference");
            Version(party.RowVersion, body.PartyRowVersion);
            var code = await db.Set<IssueReturnCode>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == body.ReturnCodeId && value.IsActive) ?? throw ApiException.Invalid("returnCodeId", "coord.reference");
            Version(code.RowVersion, body.ReturnCodeRowVersion);
            Check.That(body.ReceivedDate >= source.Issue.IssuedDate && body.ReceivedDate <= clock.Today(await settings.Get(db)), "receivedDate", "externalReview.receivedDate");
            var reason = Check.Reason(body.Reason);
            Check.That(body.Comments is { Length: > 0 and <= 200 }, "comments", "externalReview.comments");
            var comments = new List<(ExternalReviewCommentBody Body, string Text)>();
            foreach (var comment in body.Comments)
            {
                if (comment is null) throw ApiException.Invalid("comments", "error.required");
                await Coordination.Discipline(db, projectId, comment.ProjectDisciplineId); await Coordination.Person(db, project, comment.ResolverId, "resolverId");
                Check.OneOf(comment.Severity, ["Blocking", "Advisory"], "severity");
                if (comment.IssueId is { } linked) Check.That(await db.Issues.AnyAsync(issue => issue.Id == linked && issue.ProjectId == projectId), "issueId", "coord.reference");
                comments.Add((comment, Check.Required(comment.Text, "text", 4000)));
            }
            var receipt = new ExternalReview { ProjectId = projectId, PackageId = id, RoundId = round.Id, DeliverableIssueId = body.DeliverableIssueId,
                ExternalPartyId = party.Id, ReceivedDate = body.ReceivedDate, ReturnCodeId = code.Id,
                ReturnCodeSnapshot = Check.Required(code.Code, "returnCodeId", 10), ReturnLabelSnapshot = Check.Required(code.Name, "returnCodeId", 120),
                PartyNameSnapshot = Check.Required(party.Name, "externalPartyId", 200), PartyOrganisationSnapshot = Check.Optional(party.Organisation, "externalPartyId", 200) };
            db.Set<ExternalReview>().Add(receipt); db.Audit.Note(receipt, reason: reason);
            foreach (var (comment, text) in comments)
            {
                var finding = new ReviewFinding { ProjectId = projectId, PackageId = id, RoundId = round.Id, DeliverableIssueId = source.Issue.Id,
                    SourceRevisionId = null, OriginatorId = null, ExternalReviewId = receipt.Id, VerifierId = null, ResolverId = comment.ResolverId,
                    ProjectDisciplineId = comment.ProjectDisciplineId, IssueId = comment.IssueId, Text = text, Severity = comment.Severity, Status = FindingStatus.Open };
                db.ReviewFindings.Add(finding); db.Audit.Note(finding, reason: reason);
            }
            await ReviewEndpoints.Recompute(db, notify, project, package, (await settings.Get(db)).AllowSelfReview);
            await ReviewEndpoints.Notify(db, notify, project, package, comments.Select(comment => comment.Body.ResolverId).Distinct());
            return receipt;
        }, beforeReplay: async (project, context) =>
        {
            await Package(projectId, id, access, db);
            Access.Demand(await RecordPermission(db, access.Actor, context));
            // Re-read only the original committed receipt's provenance under the lock. Run owns mismatch/replay.
            var priorCommand = await db.CoordinationCommands.AsNoTracking().SingleOrDefaultAsync(command => command.ProjectId == projectId
                && command.ActorId == access.Me.Id && command.RequestId == body.RequestId);
            if (priorCommand is null || priorCommand.PayloadHash != Coordination.Hash(payload)) return;
            var prior = await db.Set<ExternalReview>().AsNoTracking().SingleOrDefaultAsync(receipt => receipt.Id == priorCommand.ResultId
                && receipt.ProjectId == projectId && receipt.PackageId == id);
            if (prior is null) return;
            await ReviewIssuedSources.Require(db, projectId, prior.DeliverableIssueId);
            await ReviewEndpoints.Authors(db, prior.RoundId);
        });
    }
    static void Version(int actual, int? expected)
    {
        if (expected is null) throw new ApiException(428, "precondition_required", Text.Get("error.if_match"));
        if (actual != expected) throw ApiException.Conflict("concurrency_conflict", "coord.stale", new ExternalReviewStaleVersion(actual));
    }

    internal static async Task NamedVerifier(HubDb db, Project project, Guid roundId, Guid verifier)
    {
        await Coordination.Person(db, project, verifier, "verifierId");
        Check.That(await db.ProjectMembers.AnyAsync(member => member.ProjectId == project.Id && member.UserId == verifier && member.RemovedAt == null && member.Roles.Contains(ProjectRole.Reviewer))
            || await db.DisciplineReviews.AnyAsync(assignment => assignment.ProjectId == project.Id && assignment.RoundId == roundId && assignment.ReviewerId == verifier), "verifierId", "review.named_reviewer");
    }

    internal static async Task GuardSourceReplay(Guid projectId, Guid requestId, object payload, Guid[] issuedIds, Access access, HubDb db)
    {
        var hash = Coordination.Hash(payload);
        if (!await db.CoordinationCommands.AsNoTracking().AnyAsync(command => command.ProjectId == projectId && command.ActorId == access.Me.Id
            && command.RequestId == requestId && command.PayloadHash == hash)) return;
        foreach (var source in (await ReviewIssuedSources.ReadMany(db, projectId, issuedIds)).Values) ReviewIssuedSources.Require(source);
    }

    internal static async Task GuardReplay(Guid projectId, Guid packageId, string action, Guid? targetId, ReviewEndpoints.FindingAction? body, Access access, HubDb db,
        Project? lockedProject = null, ProjectContext? lockedContext = null)
    {
        if (!await ReviewIssuedSources.IsIssuedPackage(db, packageId)) return;
        var package = await Package(projectId, packageId, access, db);
        var (project, context) = lockedProject is not null && lockedContext is not null ? (lockedProject, lockedContext) : await access.Project(projectId, false);
        if (action is "round" or "start" or "reviewer") Access.Demand(Permissions.CoordinateReview(access.Actor, context, package.ProjectDisciplineId, package.CoordinatorId));
        else if (action is "cancel" or "coordinator") Access.Demand(Permissions.ManageCoordination(access.Actor, context, package.ProjectDisciplineId));
        else if (action == "decision")
        {
            var assignment = await db.DisciplineReviews.AsNoTracking().SingleOrDefaultAsync(value => value.Id == targetId && value.ProjectId == projectId
                && db.ReviewRounds.Any(round => round.Id == value.RoundId && round.PackageId == packageId)) ?? throw ApiException.NotFound();
            Access.Demand(Permissions.NamedCoordinationAction(access.Actor, context, assignment.ReviewerId)); await Coordination.Person(db, project, assignment.ReviewerId);
            var authors = await ReviewEndpoints.Authors(db, assignment.RoundId);
            await KnownRoundFindings(db, project, packageId, assignment.RoundId);
            Check.That(ReviewRules.Independent(assignment.ReviewerId, authors, (await db.CurrentSettings()).AllowSelfReview), "reviewerId", "review.independent");
        }
        else if (action == "findingAction")
        {
            var finding = await db.ReviewFindings.AsNoTracking().SingleOrDefaultAsync(value => value.Id == targetId && value.PackageId == packageId && value.ProjectId == projectId) ?? throw ApiException.NotFound();
            await ValidateFinding(db, project, finding); var authors = await ReviewEndpoints.FindingAuthors(db, finding);
            if (body!.Action == "assignVerifier") Access.Demand(Permissions.ManageTeam(access.Actor, context));
            else if (body.Action == "assignResolver") Access.Demand(Permissions.CoordinateReview(access.Actor, context, package.ProjectDisciplineId, package.CoordinatorId));
            else
            {
                var actor = body.Action == "acknowledgeWithdrawal" ? package.CoordinatorId : body.Action == FindingStatus.Responded ? finding.ResolverId
                    : finding.VerifierId ?? throw ApiException.Invalid("verifierId", "externalReview.verifierRequired");
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, context, actor)); await Coordination.Person(db, project, actor);
                if (body.Action is not ("acknowledgeWithdrawal" or FindingStatus.Responded))
                {
                    await NamedVerifier(db, project, finding.RoundId, actor);
                    Check.That(ReviewRules.Independent(actor, authors, (await db.CurrentSettings()).AllowSelfReview), "verifierId", "review.independent");
                }
            }
        }
    }

    internal static async Task KnownRoundFindings(HubDb db, Project project, Guid packageId, Guid roundId)
    {
        foreach (var finding in await db.ReviewFindings.AsNoTracking().Where(value => value.PackageId == packageId && value.RoundId == roundId).ToArrayAsync())
            await ValidateFinding(db, project, finding);
    }

    internal static async Task ValidateFinding(HubDb db, Project project, ReviewFinding finding, bool requireKnown = true)
    {
        var receipt = await db.Set<ExternalReview>().AsNoTracking().SingleOrDefaultAsync(row => row.Id == finding.ExternalReviewId && row.ProjectId == project.Id && row.PackageId == finding.PackageId)
            ?? throw ApiException.Invalid("externalReviewId", "externalReview.integrity");
        Check.That(finding.ProjectId == project.Id && finding.OriginatorId is null && finding.SourceRevisionId is null && finding.DeliverableIssueId is not null,
            "externalReviewId", "externalReview.integrity");
        var chain = new List<ReviewFinding>(); var seen = new HashSet<Guid>(); var cursor = finding;
        var limit = Math.Max(1, (await db.CurrentSettings()).ChainDepthLimit);
        while (true)
        {
            Check.That(seen.Add(cursor.Id) && seen.Count <= limit
                && cursor.ProjectId == project.Id && cursor.PackageId == finding.PackageId && cursor.ExternalReviewId == receipt.Id
                && cursor.ProjectDisciplineId == finding.ProjectDisciplineId && cursor.DeliverableIssueId is not null
                && cursor.SourceRevisionId is null && cursor.OriginatorId is null, "findingId", "externalReview.integrity");
            chain.Add(cursor);
            if (cursor.CarriedFromId is not { } previousId) break;
            cursor = await db.ReviewFindings.AsNoTracking().SingleOrDefaultAsync(value => value.Id == previousId)
                ?? throw ApiException.Invalid("findingId", "externalReview.integrity");
        }
        var sources = await ReviewIssuedSources.ReadMany(db, project.Id, chain.Select(value => value.DeliverableIssueId!.Value).Append(receipt.DeliverableIssueId).Distinct().ToArray());
        if (requireKnown) foreach (var source in sources.Values) ReviewIssuedSources.Require(source);
        var original = sources[receipt.DeliverableIssueId];
        var roundIds = chain.Select(value => value.RoundId).Append(receipt.RoundId).Distinct().ToArray();
        var rounds = await db.ReviewRounds.AsNoTracking().Where(value => roundIds.Contains(value.Id) && value.ProjectId == project.Id && value.PackageId == finding.PackageId).ToDictionaryAsync(value => value.Id);
        Check.That(rounds.Count == roundIds.Length, "findingId", "externalReview.integrity");
        var manifestItems = await db.ReviewManifestItems.AsNoTracking().Where(item => roundIds.Contains(item.RoundId) && item.DeliverableId == original.Deliverable.Id).ToArrayAsync();
        var assignmentRounds = await db.DisciplineReviews.AsNoTracking().Where(assignment => roundIds.Contains(assignment.RoundId)
            && assignment.ProjectId == project.Id && assignment.ProjectDisciplineId == finding.ProjectDisciplineId).Select(assignment => assignment.RoundId).Distinct().ToArrayAsync();
        for (var index = 0; index < chain.Count; index++)
        {
            cursor = chain[index]; var source = sources[cursor.DeliverableIssueId!.Value]; var round = rounds[cursor.RoundId];
            Check.That(source.Deliverable.Id == original.Deliverable.Id, "findingId", "externalReview.integrity");
            var items = manifestItems.Where(item => item.RoundId == cursor.RoundId).ToArray();
            Check.That(items.Length <= 1, "findingId", "externalReview.integrity");
            var manifest = items.SingleOrDefault();
            if (manifest is not null)
                Check.That(manifest.ProjectId == project.Id && manifest.DeliverableIssueId == source.Issue.Id && manifest.SourceRevisionId is null
                    && (source.Authors is null || manifest.AuthorIds.Order().SequenceEqual(source.Authors.Order())), "findingId", "externalReview.integrity");
            if (index == chain.Count - 1)
            {
                Check.That(manifest is not null && cursor.RoundId == receipt.RoundId && cursor.DeliverableIssueId == receipt.DeliverableIssueId,
                    "findingId", "externalReview.integrity");
                return;
            }
            var previous = chain[index + 1]; var previousRound = rounds[previous.RoundId];
            Check.That(previousRound.Number < round.Number, "findingId", "externalReview.integrity");
            if (!assignmentRounds.Contains(cursor.RoundId) && assignmentRounds.Contains(previous.RoundId))
                Check.That(!string.IsNullOrWhiteSpace(round.RemovalImpact) && !string.IsNullOrWhiteSpace(round.Reason), "findingId", "externalReview.integrity");
            if (manifest is null)
            {
                Check.That(previous.DeliverableIssueId == cursor.DeliverableIssueId, "findingId", "externalReview.integrity");
                // Only the transition that actually removed the source needs the impact. Later carry keeps that fact.
                if (manifestItems.Any(item => item.RoundId == previous.RoundId))
                    Check.That(!string.IsNullOrWhiteSpace(round.RemovalImpact) && !string.IsNullOrWhiteSpace(round.Reason), "findingId", "externalReview.integrity");
            }
        }
    }
}

file sealed record ExternalReviewFindingCount(Guid Id, int Count);
// Wire shapes: the payload hash is a stored receipt and the stale version is problem-body JSON; names and order are fixed.
file sealed record ExternalReviewRecordPayload(string Operation, Guid Id, ExternalReviewRecordBody Body);
file sealed record ExternalReviewStaleVersion(int CurrentRowVersion);
