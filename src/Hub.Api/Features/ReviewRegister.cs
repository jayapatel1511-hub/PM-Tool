using System.Data;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ReviewRegisterQuery(Guid? DisciplineId, string? Severity, string? OriginatorKind, Guid? OriginatorId,
    Guid? ResolverId, bool? AllRounds, int? Page, int? PageSize);
public sealed record ReviewRegisterPerson(Guid? Id, string? Name, string State);
public sealed record ReviewRegisterOriginator(string Kind, Guid? UserId, Guid? ExternalPartyId, string? Name, string State,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Organisation = null);
public sealed record ReviewRegisterDiscipline(Guid Id, string? Name, string State);
public sealed record ReviewRegisterSource(Guid Id, string Key, string? Revision, Guid? DeliverableId, bool Available,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReviewIssuedSourceRead? IssuedSource = null);
public sealed record ReviewRegisterActionability(bool IsCurrentRound, bool IsPackageLive, bool CanAct,
    string NextAction, ReviewRegisterPerson? NextActor, string? Reason);
public sealed record ReviewRegisterRow(Guid Id, Guid PackageId, string PackageKey, string PackageTitle, string PackageStatus,
    Guid RoundId, int RoundNumber, bool IsCurrentRound, Guid ProjectDisciplineId, string? DisciplineName, string DisciplineState,
    ReviewRegisterSource Source, string Text, string Severity, string Status, string? Response, int RowVersion,
    DateTimeOffset CreatedAt, Guid? CarriedFromId, bool PendingWithdrawalAcknowledgement, ReviewRegisterOriginator Originator,
    ReviewRegisterPerson ResolutionOwner, ReviewRegisterPerson Verifier, ReviewRegisterPerson Coordinator, ReviewRegisterActionability Actionability,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResponseOutcome = null);
public sealed record ReviewRegisterFilters(Guid? DisciplineId, string? Severity, string? OriginatorKind, Guid? OriginatorId, Guid? ResolverId);
public sealed record ReviewRegisterOptions(ReviewRegisterDiscipline[] Disciplines, string[] Severities,
    ReviewRegisterOriginator[] Originators, ReviewRegisterPerson[] ResolutionOwners);
public sealed record ReviewRegisterPage(ReviewRegisterRow[] Items, int Page, int PageSize, int TotalCount, bool AllRounds,
    ReviewRegisterFilters Filters, ReviewRegisterOptions Options);

/// Project-scoped unresolved review findings. This read never invokes a command, Seen operation, SaveChanges or a receipt writer.
public static class ReviewRegister
{
    sealed class ReviewRegisterJoin
    {
        public ReviewFinding Finding { get; init; } = null!;
        public ReviewPackage Package { get; init; } = null!;
        public ReviewRound Round { get; init; } = null!;
        public SourceRevision? Source { get; init; }
        public DeliverableIssue? IssuedSource { get; init; }
        public ExternalReview? Receipt { get; init; }
        public Deliverable Deliverable { get; init; } = null!;
        public ProjectDiscipline Discipline { get; init; } = null!;
        public Discipline Reference { get; init; } = null!;
    }

    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/review-findings", Read)
        .RequireFlag(DeliverableEndpoints.RegisterFlag);

    static async Task<Ok<ReviewRegisterPage>> Read(Guid projectId, [AsParameters] ReviewRegisterQuery filter,
        Access access, HubDb db, SettingsStore store)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, DeliverableEndpoints.RegisterFlag);
        // Counts, options, page and actionability observe one consistent read. Nothing is reserved or written.
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var (project, context) = await access.Project(projectId, false);
        var severity = string.IsNullOrEmpty(filter.Severity) ? null : filter.Severity;
        var originatorKind = string.IsNullOrEmpty(filter.OriginatorKind) ? null : filter.OriginatorKind;
        if (severity is not null) Check.OneOf(severity, ["Blocking", "Advisory"], "severity");
        if (originatorKind is not null) Check.OneOf(originatorKind, ["User", "ExternalParty"], "originatorKind");
        Check.That(filter.DisciplineId != Guid.Empty, "disciplineId", "error.invalid");
        Check.That(filter.OriginatorId != Guid.Empty, "originatorId", "error.invalid");
        Check.That(filter.ResolverId != Guid.Empty, "resolverId", "error.invalid");
        var allRounds = filter.AllRounds == true;
        var scope = Scope(db, projectId, allRounds);
        // Internal malformed rows retain their established safe omission. Issued provenance must not vanish through a join.
        var issuedPackageIds = db.ReviewPackages.AsNoTracking().Where(package => package.ProjectId == projectId
            && (db.ReviewRounds.Any(round => round.PackageId == package.Id
                    && db.ReviewManifestItems.Any(item => item.RoundId == round.Id && item.DeliverableIssueId != null))
                || db.Set<ExternalReview>().Any(receipt => receipt.PackageId == package.Id))).Select(package => package.Id);
        var issuedFindings = db.ReviewFindings.AsNoTracking().Where(finding => finding.ProjectId == projectId
            && db.ReviewPackages.Any(package => package.Id == finding.PackageId && package.ProjectId == projectId && package.DeletedAt == null
                && (allRounds || finding.RoundId == package.CurrentRoundId))
            && (finding.DeliverableIssueId != null || finding.ExternalReviewId != null || issuedPackageIds.Contains(finding.PackageId))
            && (finding.Status == FindingStatus.Open || finding.Status == FindingStatus.Responded
                || finding.Status == FindingStatus.Withdrawn && finding.Severity == "Blocking" && finding.WithdrawalAcknowledgedBy == null)).Select(finding => finding.Id);
        var expectedIssued = await issuedFindings.CountAsync();
        if (await scope.CountAsync(row => issuedFindings.Contains(row.Finding.Id) && row.IssuedSource != null && row.Receipt != null) != expectedIssued)
            throw ApiException.Conflict("review_register_integrity", "externalReview.integrity");

        // Only identities occurring in the authorized unresolved scope can become filter options.
        // These are distinct projections before selected filters/paging, not a user directory or a capped package-page join.
        var disciplineRows = await scope.Select(row => new DisciplineOption { Id = row.Discipline.Id, Name = row.Reference.Name,
            Active = row.Discipline.IsActive && row.Reference.IsActive }).Distinct().OrderBy(row => row.Name).ThenBy(row => row.Id).ToArrayAsync();
        var severities = await scope.Select(row => row.Finding.Severity).Distinct().OrderBy(value => value).ToArrayAsync();
        var originators = await scope.Select(row => row.Finding.OriginatorId).Distinct().ToArrayAsync();
        var receipts = await scope.Where(row => row.Receipt != null).Select(row => row.Receipt!).Distinct().ToArrayAsync();
        var partyIds = receipts.Select(row => row.ExternalPartyId).Distinct().ToArray();
        var parties = await db.ExternalParties.AsNoTracking().Where(party => party.ProjectId == projectId && partyIds.Contains(party.Id)).ToDictionaryAsync(party => party.Id);
        var resolvers = await scope.Select(row => row.Finding.ResolverId).Distinct().ToArrayAsync();
        var query = scope;
        if (filter.DisciplineId is { } discipline) query = query.Where(row => row.Finding.ProjectDisciplineId == discipline);
        if (severity is not null) query = query.Where(row => row.Finding.Severity == severity);
        if (originatorKind == "ExternalParty") query = query.Where(row => row.Receipt != null);
        if (originatorKind == "User") query = query.Where(row => row.Finding.OriginatorId != null && row.Receipt == null);
        if (filter.OriginatorId is { } originator) query = originatorKind == "ExternalParty"
            ? query.Where(row => row.Receipt != null && row.Receipt.ExternalPartyId == originator)
            : query.Where(row => row.Finding.OriginatorId == originator && row.Receipt == null);
        if (filter.ResolverId is { } resolver) query = query.Where(row => row.Finding.ResolverId == resolver);
        var count = await query.CountAsync();
        var (page, size) = Http.Paging(filter.Page, filter.PageSize);
        var offset = (long)(page - 1) * size;
        ReviewRegisterJoin[] selected = offset > int.MaxValue ? [] : await query.OrderBy(row => row.Package.Key)
            .ThenBy(row => row.Round.Number).ThenBy(row => row.Finding.CreatedAt).ThenBy(row => row.Finding.Id)
            .Skip((int)offset).Take(size).ToArrayAsync();
        var personIds = originators.OfType<Guid>().Concat(resolvers).Concat(selected.SelectMany(row => new Guid?[] { row.Finding.VerifierId, row.Package.CoordinatorId }).OfType<Guid>())
            .Where(id => id != Guid.Empty).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(person => personIds.Contains(person.Id)).ToDictionaryAsync(person => person.Id);
        ReviewRegisterPerson Person(Guid? id) => id is null ? new(null, null, "Unassigned") : id == Guid.Empty ? new(null, null, "Unknown")
            : people.TryGetValue(id.Value, out var person) ? new(id, string.IsNullOrWhiteSpace(person.DisplayName) ? null : person.DisplayName,
                !person.IsActive ? "Inactive" : string.IsNullOrWhiteSpace(person.DisplayName) ? "Unknown" : "Active") : new(id, null, "Unknown");
        ReviewRegisterOriginator Originator(Guid? id)
        { var person = Person(id); return new("User", person.Id, null, person.Name, person.State); }
        ReviewRegisterOriginator PartyOriginator(ExternalReview receipt) => new("ExternalParty", null, receipt.ExternalPartyId,
            receipt.PartyNameSnapshot, parties.TryGetValue(receipt.ExternalPartyId, out var party) ? party.IsActive ? "Active" : "Inactive" : "Unknown", receipt.PartyOrganisationSnapshot);
        var originatorOptions = originators.OfType<Guid>().Select(id => Originator(id)).Concat(receipts.Select(PartyOriginator))
            .GroupBy(originator => (originator.Kind, originator.UserId, originator.ExternalPartyId)).Select(group => group.OrderBy(originator => originator.Name, StringComparer.Ordinal).First())
            .OrderBy(originator => originator.Name, StringComparer.Ordinal).ThenBy(originator => originator.Kind).ThenBy(originator => originator.UserId ?? originator.ExternalPartyId).ToArray();
        var options = new ReviewRegisterOptions(disciplineRows.Select(row => new ReviewRegisterDiscipline(row.Id,
                string.IsNullOrWhiteSpace(row.Name) ? null : row.Name, !row.Active ? "Inactive" : string.IsNullOrWhiteSpace(row.Name) ? "Unknown" : "Active")).ToArray(),
            severities, originatorOptions,
            resolvers.Select(id => Person(id)).OrderBy(person => person.Name, StringComparer.Ordinal).ThenBy(person => person.Id).ToArray());

        var factsReader = new ReviewFindingContextData(db, project, settings);
        var viewerEligible = await Coordination.People(db, project).AnyAsync(person => person.Id == access.Actor.Id);
        var issuedSources = await ReviewIssuedSources.ReadMany(db, projectId, selected.Select(row => row.Finding.DeliverableIssueId).OfType<Guid>().Distinct().ToArray());
        var rows = new List<ReviewRegisterRow>();
        foreach (var selectedRow in selected)
        {
            var finding = selectedRow.Finding; var package = selectedRow.Package;
            if (finding.ExternalReviewId is not null) await ExternalReviewEndpoints.ValidateFinding(db, project, finding, requireKnown: false);
            var current = package.CurrentRoundId == finding.RoundId;
            var live = package.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded);
            var hydrated = await factsReader.Read(finding.Id);
            var actors = new Guid?[] { finding.OriginatorId, finding.ResolverId, finding.VerifierId, package.CoordinatorId }.OfType<Guid>().Distinct()
                .Select(id => new RecordContextActor(id.ToString(), Person(id).State switch {
                    "Active" => RecordContextActorState.Active, "Inactive" => RecordContextActorState.Inactive, _ => RecordContextActorState.Unknown
                })).ToArray();
            var evaluated = hydrated is null ? null : RecordContextRules.Evaluate(hydrated.Facts with { Actors = actors });
            var nextAction = evaluated?.NextAction ?? RecordContextAction.NotAssessed;
            var nextActor = evaluated?.NextActors.Count == 1 && Guid.TryParse(evaluated.NextActors[0].Id, out var nextId) ? Person(nextId) : null;
            var permission = Permissions.NamedCoordinationAction(access.Actor, context, nextActor?.Id ?? Guid.Empty);
            string? reason = !current ? "historical_round" : !live ? "package_terminal"
                : !permission.Ok && permission.Why is not "perm.owner" ? permission.Why
                : evaluated is null || nextAction is RecordContextAction.None or RecordContextAction.NotAssessed ? "not_assessed"
                : nextActor is null || nextActor.State != "Active" ? "person_unavailable"
                : nextActor.Id != access.Actor.Id ? "not_your_action"
                : !viewerEligible || !permission.Ok ? permission.Why ?? "perm.not_member" : null;
            if (!access.Actor.IsActive && reason is null) reason = "perm.inactive";
            var available = selectedRow.Deliverable.DeletedAt is null;
            var disciplineState = !selectedRow.Discipline.IsActive || !selectedRow.Reference.IsActive ? "Inactive"
                : string.IsNullOrWhiteSpace(selectedRow.Reference.Name) ? "Unknown" : "Active";
            var issued = selectedRow.IssuedSource is { } issue ? ReviewIssuedSources.Row(issuedSources[issue.Id]) : null;
            if (issued is not null && !issued.Selectable) { reason = "not_assessed"; nextActor = null; nextAction = RecordContextAction.NotAssessed; }
            var source = selectedRow.Source is { } internalSource
                ? new ReviewRegisterSource(internalSource.Id, internalSource.SourceKey, internalSource.Revision, available ? selectedRow.Deliverable.Id : null, available)
                : new ReviewRegisterSource(selectedRow.IssuedSource!.Id, available ? selectedRow.Deliverable.Key : "", selectedRow.IssuedSource.Revision,
                    available ? selectedRow.Deliverable.Id : null, available && issued?.Selectable == true, "IssuedRevision", issued);
            rows.Add(new(finding.Id, package.Id, package.Key, package.Title, package.Status, finding.RoundId, selectedRow.Round.Number, current,
                finding.ProjectDisciplineId, string.IsNullOrWhiteSpace(selectedRow.Reference.Name) ? null : selectedRow.Reference.Name, disciplineState,
                source,
                finding.Text, finding.Severity, finding.Status, finding.Response, finding.RowVersion, finding.CreatedAt, finding.CarriedFromId,
                finding.Status == FindingStatus.Withdrawn && finding.Severity == "Blocking" && finding.WithdrawalAcknowledgedBy is null,
                selectedRow.Receipt is { } receipt ? PartyOriginator(receipt) : Originator(finding.OriginatorId), Person(finding.ResolverId), Person(finding.VerifierId), Person(package.CoordinatorId),
                new(current, live, reason is null, nextAction, nextActor, reason), finding.ResponseOutcome));
        }
        return TypedResults.Ok(new ReviewRegisterPage(rows.ToArray(), page, size, count, allRounds,
            new(filter.DisciplineId, severity, originatorKind ?? (filter.OriginatorId is not null ? "User" : null), filter.OriginatorId, filter.ResolverId), options));
    }

    static IQueryable<ReviewRegisterJoin> Scope(HubDb db, Guid projectId, bool allRounds) =>
        from finding in db.ReviewFindings.AsNoTracking()
        join package in db.ReviewPackages.AsNoTracking() on finding.PackageId equals package.Id
        join round in db.ReviewRounds.AsNoTracking() on finding.RoundId equals round.Id
        join sourceRow in db.SourceRevisions.AsNoTracking() on finding.SourceRevisionId equals (Guid?)sourceRow.Id into sourceRows
        from source in sourceRows.DefaultIfEmpty()
        join issueRow in db.DeliverableIssues.AsNoTracking() on finding.DeliverableIssueId equals (Guid?)issueRow.Id into issueRows
        from issued in issueRows.DefaultIfEmpty()
        join receiptRow in db.Set<ExternalReview>().AsNoTracking() on finding.ExternalReviewId equals (Guid?)receiptRow.Id into receiptRows
        from receipt in receiptRows.DefaultIfEmpty()
        // Ignore only the source deliverable's deletion filter to validate its declared project and expose unavailable history safely.
        join deliverable in db.Deliverables.IgnoreQueryFilters().AsNoTracking() on (source != null ? source.DeliverableId : issued != null ? (Guid?)issued.DeliverableId : null) equals (Guid?)deliverable.Id
        join discipline in db.ProjectDisciplines.AsNoTracking() on finding.ProjectDisciplineId equals discipline.Id
        join reference in db.Disciplines.AsNoTracking() on discipline.DisciplineId equals reference.Id
        where finding.ProjectId == projectId && package.ProjectId == projectId && package.DeletedAt == null
            && round.ProjectId == projectId && round.PackageId == package.Id
            && deliverable.ProjectId == projectId && discipline.ProjectId == projectId
            && (source != null && source.ProjectId == projectId && finding.DeliverableIssueId == null && finding.ExternalReviewId == null && finding.OriginatorId != null
                || source == null && issued != null && issued.ProjectId == projectId && receipt != null && receipt.ProjectId == projectId
                    && receipt.PackageId == package.Id && finding.OriginatorId == null && finding.SourceRevisionId == null
                    && db.DeliverableIssues.Any(original => original.Id == receipt.DeliverableIssueId && original.ProjectId == projectId && original.DeliverableId == issued.DeliverableId)
                    && db.ReviewRounds.Any(received => received.Id == receipt.RoundId && received.PackageId == package.Id && received.ProjectId == projectId)
                    && db.ReviewManifestItems.Any(item => item.RoundId == receipt.RoundId && item.ProjectId == projectId && item.SourceRevisionId == null
                        && item.DeliverableIssueId == receipt.DeliverableIssueId && item.DeliverableId == issued.DeliverableId)
                    && db.ExternalParties.Any(party => party.Id == receipt.ExternalPartyId && party.ProjectId == projectId))
            && (allRounds || finding.RoundId == package.CurrentRoundId)
            && (finding.Status == FindingStatus.Open || finding.Status == FindingStatus.Responded
                || finding.Status == FindingStatus.Withdrawn && finding.Severity == "Blocking" && finding.WithdrawalAcknowledgedBy == null)
        select new ReviewRegisterJoin { Finding = finding, Package = package, Round = round, Source = source, IssuedSource = issued, Receipt = receipt,
            Deliverable = deliverable, Discipline = discipline, Reference = reference };
}

// Distinct/OrderBy follow this projection, so it stays a member-init expression for EF.
file sealed class DisciplineOption { public Guid Id { get; init; } public string Name { get; init; } = ""; public bool Active { get; init; } }
