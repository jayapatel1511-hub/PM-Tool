using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Read-only, package-batched provenance and actor facts for the one pure context evaluator.
internal sealed class ReviewFindingContextData(HubDb db, Project project, OrgSettings settings)
{
    internal sealed record Hydrated(RecordContextFacts Facts, string Key, string Title);
    sealed record Seed(ReviewPackage Package, IReadOnlyList<ReviewRound> Rounds, IReadOnlyList<DisciplineReview> Assignments,
        IReadOnlyList<ReviewFinding> Findings, IReadOnlyList<ReviewManifestItem> Manifest, IReadOnlyList<FindingEvent> Events);
    sealed record Snapshot(Seed Raw, Dictionary<Guid, ReviewRound> Rounds, Dictionary<Guid, ReviewFinding> Findings,
        Dictionary<Guid, SourceRevision> Sources, Dictionary<Guid, Deliverable> Deliverables,
        Dictionary<Guid, DeliverableIssue> Issues, Dictionary<Guid, Guid[]> IssueAuthors, Dictionary<Guid, ExternalReview> ExternalReviews,
        Dictionary<Guid, HashSet<Guid>> CurrentAuthors, HashSet<Guid> ForeignReferences, HashSet<Guid> Eligible, Dictionary<Guid, HashSet<string>> Roles,
        IReadOnlyList<ProjectMember> Members, IReadOnlyList<ProjectDiscipline> Disciplines);
    readonly Dictionary<Guid, Seed> seeds = [];
    readonly Dictionary<Guid, Snapshot> packages = [];
    readonly Dictionary<Guid, (bool Known, HashSet<Guid> Authors)> roundAuthors = [];
    readonly Dictionary<Guid, Task<bool>> approval = [];
    sealed record IssuedAncestry(bool Valid, IReadOnlyList<ReviewFinding> Nodes);
    readonly Dictionary<Guid, IssuedAncestry> issuedAncestry = [];

    public void SeedPackage(ReviewPackage package, IReadOnlyList<ReviewRound> rounds, IReadOnlyList<DisciplineReview> assignments,
        IReadOnlyList<ReviewFinding> findings, IReadOnlyList<ReviewManifestItem> manifest, IReadOnlyList<FindingEvent> events)
    {
        if (package.ProjectId != project.Id) throw ApiException.NotFound();
        seeds[package.Id] = new(package, rounds, assignments, findings, manifest, events);
    }

    public async Task<Hydrated?> Read(Guid id)
    {
        var finding = packages.Values.SelectMany(x => x.Findings.Values).FirstOrDefault(x => x.Id == id)
            ?? seeds.Values.SelectMany(x => x.Findings).FirstOrDefault(x => x.Id == id && x.ProjectId == project.Id)
            ?? await db.ReviewFindings.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id);
        if (finding is null || !settings.IsFlagOn("register_screens") && (finding.ExternalReviewId is not null || finding.DeliverableIssueId is not null)) return null;
        var data = await Package(finding.PackageId);
        if (data is null || !data.Findings.TryGetValue(id, out finding)) return null;
        var p = data.Raw.Package;
        var round = data.Rounds.GetValueOrDefault(finding.RoundId);
        var valid = round is not null && SourceValid(data, finding);
        var current = valid && p.CurrentRoundId == finding.RoundId;
        var live = p.Status is not (ReviewStatus.Cancelled or ReviewStatus.Superseded);
        var (known, authors) = Authors(data, finding);
        var resultState = finding.Status == FindingStatus.Open ? FindingStatus.Responded : finding.Status == FindingStatus.Responded ? FindingStatus.VerifiedClosed : finding.Status;
        var resultAck = finding.WithdrawalAcknowledgedBy is not null || finding.Status == FindingStatus.Withdrawn;
        var actionable = finding.Status is FindingStatus.Open or FindingStatus.Responded
            || finding.Status == FindingStatus.Withdrawn && finding.Severity == "Blocking" && finding.WithdrawalAcknowledgedBy is null;
        var canApprove = actionable && current && live && valid && await PostActionApproval(data, finding, resultState, resultAck);
        var facts = new RecordContextFacts(RecordContextKind.ReviewFinding, id.ToString(), finding.Status, finding.ResolverId.ToString(),
            NeededBy: null, CoordinatorId: p.CoordinatorId.ToString(), OriginatorId: finding.OriginatorId?.ToString(), VerifierId: finding.VerifierId?.ToString())
        {
            FindingProgress = new(valid, live, current, Named(data, finding.ResolverId), Named(data, finding.VerifierId), Named(data, p.CoordinatorId),
                known, known && finding.VerifierId is { } verifier && ReviewRules.Independent(verifier, authors, settings.AllowSelfReview),
                finding.Response is not null && finding.EvidenceUrl is not null, finding.Severity == "Blocking", finding.WithdrawalAcknowledgedBy is not null, canApprove),
        };
        // Findings have no independent key or due date; the package key is context only.
        return new(facts, p.Key, finding.Text);
    }

    bool Named(Snapshot data, Guid? candidate)
    {
        if (candidate is not { } id) return false;
        var roles = data.Roles.GetValueOrDefault(id) ?? [];
        var memberRoles = data.Members.Where(x => x.UserId == id).SelectMany(x => x.Roles).ToHashSet();
        var leads = data.Disciplines.Where(x => x.IsActive && x.LeadUserId == id).Select(x => x.Id).ToHashSet();
        var actor = new Actor(id, data.Eligible.Contains(id), roles);
        var context = new ProjectContext(project.Id, project.Status, project.Visibility, project.ProjectManagerId,
            project.AllowViewerComments, memberRoles, leads, null);
        return Permissions.NamedCoordinationAction(actor, context, id).Ok;
    }

    (bool Known, HashSet<Guid> Authors) Authors(Snapshot data, ReviewFinding finding)
    {
        if (finding.DeliverableIssueId is not null || finding.ExternalReviewId is not null)
        {
            var ancestry = IssuedChain(data, finding); var captured = new HashSet<Guid>();
            if (!ancestry.Valid) return (false, captured);
            foreach (var node in ancestry.Nodes)
            {
                var provenance = RoundAuthors(data, node.RoundId);
                if (!provenance.Known) return (false, captured);
                captured.UnionWith(provenance.Authors); captured.Add(node.ResolverId);
                var events = data.Raw.Events.Where(item => item.FindingId == node.Id && item.Action == FindingStatus.Responded).ToArray();
                if (events.Any(item => item.ProjectId != project.Id)) return (false, captured);
                captured.UnionWith(events.Select(item => item.CreatedBy).OfType<Guid>());
            }
            return (true, captured);
        }
        var ids = new HashSet<Guid>(); var seen = new HashSet<Guid>(); ReviewFinding? cursor = finding;
        while (cursor is not null)
        {
            if (!seen.Add(cursor.Id) || seen.Count > Math.Max(1, settings.ChainDepthLimit)
                || cursor.ProjectId != project.Id || cursor.PackageId != data.Raw.Package.Id || !data.Rounds.ContainsKey(cursor.RoundId)
                || !SourceValid(data, cursor)) return (false, ids);
            var provenance = RoundAuthors(data, cursor.RoundId);
            if (!provenance.Known) return (false, ids);
            ids.UnionWith(provenance.Authors); ids.Add(cursor.ResolverId);
            var events = data.Raw.Events.Where(x => x.FindingId == cursor.Id && x.Action == FindingStatus.Responded).ToArray();
            if (events.Any(x => x.ProjectId != project.Id)) return (false, ids);
            ids.UnionWith(events.Select(x => x.CreatedBy).OfType<Guid>());
            if (cursor.CarriedFromId is not { } previous) break;
            if (!data.Findings.TryGetValue(previous, out cursor)) return (false, ids);
        }
        return (true, ids);
    }

    (bool Known, HashSet<Guid> Authors) RoundAuthors(Snapshot data, Guid round)
    {
        if (roundAuthors.TryGetValue(round, out var cached)) return cached;
        var manifest = data.Raw.Manifest.Where(x => x.RoundId == round).ToArray();
        var ids = new HashSet<Guid>(); var issuedCount = manifest.Count(x => x.DeliverableIssueId is not null);
        var known = manifest.Length > 0 && (issuedCount == 0 || issuedCount == manifest.Length);
        foreach (var item in manifest)
        {
            if (item.ProjectId != project.Id || !data.Deliverables.TryGetValue(item.DeliverableId, out var deliverable))
            { known = false; continue; }
            if (item.DeliverableIssueId is { } issueId)
            {
                if (item.SourceRevisionId is not null || !data.Issues.TryGetValue(issueId, out var issue)
                    || issue.DeliverableId != item.DeliverableId || !data.IssueAuthors.TryGetValue(issueId, out var captured)
                    || !captured.ToHashSet().SetEquals(item.AuthorIds)) { known = false; continue; }
                ids.UnionWith(captured);
                continue; // Current ownership cannot become authorship of an immutable historical issue.
            }
            if (item.SourceRevisionId is not { } revisionId || data.ForeignReferences.Contains(item.DeliverableId)
                || !data.Sources.TryGetValue(revisionId, out var source) || source.DeliverableId != item.DeliverableId)
            { known = false; continue; }
            ids.UnionWith(item.AuthorIds);
            ids.UnionWith(new[] { deliverable.OwnerId, deliverable.CreatedBy }.OfType<Guid>());
            if (data.CurrentAuthors.TryGetValue(deliverable.Id, out var authors)) ids.UnionWith(authors);
        }
        return roundAuthors[round] = (known, ids);
    }

    bool SourceValid(Snapshot data, ReviewFinding finding)
    {
        if (finding.DeliverableIssueId is null)
            return finding.ExternalReviewId is null && finding.SourceRevisionId is { } source && data.Sources.ContainsKey(source);
        return IssuedChain(data, finding).Valid;
    }

    IssuedAncestry IssuedChain(Snapshot data, ReviewFinding finding)
    {
        if (issuedAncestry.TryGetValue(finding.Id, out var cached)) return cached;
        IssuedAncestry Invalid() => issuedAncestry[finding.Id] = new(false, []);
        if (!settings.IsFlagOn("register_screens") || finding.SourceRevisionId is not null || finding.OriginatorId is not null
            || finding.ExternalReviewId is not { } externalId || !data.ExternalReviews.TryGetValue(externalId, out var incoming)
            || incoming.ProjectId != project.Id || incoming.PackageId != data.Raw.Package.Id || finding.PackageId != incoming.PackageId
            || !data.Rounds.ContainsKey(incoming.RoundId)
            || !data.Issues.TryGetValue(incoming.DeliverableIssueId, out var original) || !data.IssueAuthors.ContainsKey(original.Id)
            || data.Raw.Package.RequiredForIssue) return Invalid();
        var nodes = new List<ReviewFinding>(); var seen = new HashSet<Guid>(); var cursor = finding;
        while (true)
        {
            if (!seen.Add(cursor.Id) || seen.Count > Math.Max(1, settings.ChainDepthLimit)
                || cursor.ProjectId != project.Id || cursor.PackageId != incoming.PackageId || cursor.ExternalReviewId != incoming.Id
                || cursor.ProjectDisciplineId != finding.ProjectDisciplineId || cursor.SourceRevisionId is not null || cursor.OriginatorId is not null
                || cursor.DeliverableIssueId is not { } issueId || !data.Issues.TryGetValue(issueId, out var issue)
                || !data.IssueAuthors.ContainsKey(issueId) || issue.DeliverableId != original.DeliverableId
                || !data.Rounds.ContainsKey(cursor.RoundId) || !RoundAuthors(data, cursor.RoundId).Known) return Invalid();
            nodes.Add(cursor);
            if (cursor.CarriedFromId is not { } previousId) break;
            if (!data.Findings.TryGetValue(previousId, out var previous)) return Invalid();
            cursor = previous;
        }
        ReviewManifestItem[] Manifest(ReviewFinding node) => data.Raw.Manifest.Where(item => item.RoundId == node.RoundId && item.DeliverableId == original.DeliverableId).ToArray();
        bool Assigned(ReviewFinding node) => data.Raw.Assignments.Any(item => item.ProjectId == project.Id && item.RoundId == node.RoundId && item.ProjectDisciplineId == finding.ProjectDisciplineId);
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index]; var round = data.Rounds[node.RoundId]; var manifest = Manifest(node);
            if (manifest.Length > 1) return Invalid();
            if (manifest.Length == 1)
            {
                var item = manifest[0];
                if (item.ProjectId != project.Id || item.SourceRevisionId is not null || item.DeliverableIssueId != node.DeliverableIssueId
                    || !item.AuthorIds.Order().SequenceEqual(data.IssueAuthors[node.DeliverableIssueId!.Value].Order())) return Invalid();
            }
            if (index == nodes.Count - 1)
            {
                if (manifest.Length != 1 || node.RoundId != incoming.RoundId || node.DeliverableIssueId != incoming.DeliverableIssueId) return Invalid();
                break;
            }
            var previous = nodes[index + 1]; var previousRound = data.Rounds[previous.RoundId];
            if (previousRound.Number >= round.Number) return Invalid();
            var sourceRemoved = manifest.Length == 0 && Manifest(previous).Length != 0;
            var disciplineRemoved = !Assigned(node) && Assigned(previous);
            if ((sourceRemoved || disciplineRemoved) && (string.IsNullOrWhiteSpace(round.Reason) || string.IsNullOrWhiteSpace(round.RemovalImpact))) return Invalid();
            // Once absent, later carry retains that exact source; an unrelated/future manifest is never a fallback.
            if (manifest.Length == 0 && node.DeliverableIssueId != previous.DeliverableIssueId) return Invalid();
        }
        return issuedAncestry[finding.Id] = new(true, nodes.ToArray());
    }

    async Task<bool> PostActionApproval(Snapshot data, ReviewFinding finding, string state, bool acknowledged)
    {
        var round = data.Rounds.GetValueOrDefault(finding.RoundId);
        if (round?.StartedAt is null) return true; // Current Draft findings can act; Recompute skips approval.
        var assignments = data.Raw.Assignments.Where(x => x.RoundId == finding.RoundId).ToArray();
        var findings = data.Raw.Findings.Where(x => x.RoundId == finding.RoundId).ToArray();
        if (assignments.Any(x => x.ProjectId != project.Id) || findings.Any(x => x.ProjectId != project.Id)) return false;
        var blocked = findings.Any(x => ReviewRules.BlockingOpen(x.Severity, x.Id == finding.Id ? state : x.Status,
            x.Id == finding.Id ? acknowledged : x.WithdrawalAcknowledgedBy is not null));
        if (ReviewRules.PackageStatus(assignments.Select(x => x.Status), blocked) != ReviewStatus.Approved) return true;
        if (!approval.TryGetValue(data.Raw.Package.Id, out var gate))
            approval[data.Raw.Package.Id] = gate = ApprovalGate(data);
        return await gate;
    }

    async Task<bool> ApprovalGate(Snapshot data)
    {
        try
        {
            // Exact canonical read validation, without command locks, mutation or Recompute.
            await ReviewEndpoints.ValidateRound(db, project, data.Raw.Package, settings.AllowSelfReview, lockRows: false);
            return true;
        }
        catch (ApiException error) when (error.Status is 400 or 404 or 422) { return false; }
    }

    async Task<Snapshot?> Package(Guid id)
    {
        if (packages.TryGetValue(id, out var cached)) return cached;
        if (!seeds.TryGetValue(id, out var seed))
        {
            var package = await ReviewIssuedSources.VisiblePackages(db, settings.IsFlagOn("register_screens")).AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id);
            if (package is null) return null;
            var rounds = await db.ReviewRounds.AsNoTracking().Where(x => x.PackageId == id).ToListAsync();
            var roundIds = rounds.Where(x => x.ProjectId == project.Id).Select(x => x.Id).ToArray();
            var assignments = await db.DisciplineReviews.AsNoTracking().Where(x => roundIds.Contains(x.RoundId)).ToListAsync();
            var findings = await ReviewIssuedSources.VisibleFindings(db, settings.IsFlagOn("register_screens")).AsNoTracking().Where(x => x.PackageId == id).ToListAsync();
            var ids = findings.Where(x => x.ProjectId == project.Id).Select(x => x.Id).ToArray();
            seed = new(package, rounds, assignments, findings,
                await db.ReviewManifestItems.AsNoTracking().Where(x => roundIds.Contains(x.RoundId)).ToListAsync(),
                await db.FindingEvents.AsNoTracking().Where(x => ids.Contains(x.FindingId)).ToListAsync());
        }
        if (!settings.IsFlagOn("register_screens") && seed.Manifest.Any(item => item.DeliverableIssueId is not null)) return null;
        var scopedFindings = seed.Findings.Where(x => x.ProjectId == project.Id && x.PackageId == id).ToDictionary(x => x.Id);
        var scopedRounds = seed.Rounds.Where(x => x.ProjectId == project.Id && x.PackageId == id).ToDictionary(x => x.Id);
        var sourceIds = scopedFindings.Values.Select(x => x.SourceRevisionId).Concat(seed.Manifest.Select(x => x.SourceRevisionId)).OfType<Guid>().Distinct().ToArray();
        var sources = await db.SourceRevisions.AsNoTracking().Where(x => x.ProjectId == project.Id && sourceIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var externalIds = scopedFindings.Values.Select(x => x.ExternalReviewId).OfType<Guid>().Distinct().ToArray();
        var external = externalIds.Length == 0 ? new Dictionary<Guid, ExternalReview>()
            : await db.ExternalReviews.AsNoTracking().Where(x => x.ProjectId == project.Id && x.PackageId == id && externalIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var issueIds = scopedFindings.Values.Select(x => x.DeliverableIssueId).Concat(seed.Manifest.Select(x => x.DeliverableIssueId))
            .OfType<Guid>().Concat(external.Values.Select(x => x.DeliverableIssueId)).Distinct().ToArray();
        var issues = issueIds.Length == 0 ? new Dictionary<Guid, DeliverableIssue>()
            : await db.DeliverableIssues.AsNoTracking().Where(x => x.ProjectId == project.Id && issueIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var issueAuthors = await IssuedAuthorship.ReadManyAsync(db, issues.Values.ToArray());
        var deliverableIds = seed.Manifest.Where(x => x.ProjectId == project.Id).Select(x => x.DeliverableId).Distinct().ToArray();
        var deliverables = await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == project.Id && deliverableIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var taskAuthors = await db.Tasks.AsNoTracking().Where(x => x.ProjectId == project.Id && x.DeliverableId != null && deliverableIds.Contains(x.DeliverableId.Value))
            .Select(x => new { x.DeliverableId, x.AssigneeId, x.CreatedBy }).ToListAsync();
        // Canonical Authors sees every task referencing this output. A malformed foreign
        // reference is unknown provenance, never a reason to ignore a private author.
        var foreignReferences = (await db.Tasks.AsNoTracking().Where(x => x.ProjectId != project.Id && x.DeliverableId != null && deliverableIds.Contains(x.DeliverableId.Value))
            .Select(x => x.DeliverableId!.Value).Distinct().ToListAsync()).ToHashSet();
        var currentAuthors = taskAuthors.GroupBy(x => x.DeliverableId!.Value).ToDictionary(x => x.Key, x => x.SelectMany(t => new[] { t.AssigneeId, t.CreatedBy }).OfType<Guid>().ToHashSet());
        var eligible = (await Coordination.People(db, project).Select(x => x.Id).ToListAsync()).ToHashSet();
        var members = await db.ProjectMembers.AsNoTracking().Where(x => x.ProjectId == project.Id && x.RemovedAt == null).ToListAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(x => x.ProjectId == project.Id).ToListAsync();
        var actorIds = scopedFindings.Values.SelectMany(x => new[] { x.ResolverId, x.VerifierId }).OfType<Guid>().Append(seed.Package.CoordinatorId).Distinct().ToArray();
        var roles = (await db.UserRoles.AsNoTracking().Where(x => actorIds.Contains(x.UserId)).ToListAsync()).GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.Select(r => r.Role).ToHashSet());
        return packages[id] = new(seed, scopedRounds, scopedFindings, sources, deliverables, issues, issueAuthors, external, currentAuthors, foreignReferences, eligible, roles, members, disciplines);
    }
}
