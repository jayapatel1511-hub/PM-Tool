using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

internal sealed class SubmissionContextData(HubDb db, Project project)
{
    internal sealed record Check(SubmissionCheck Row, string? State, bool Blocking, bool Known, bool OwnerEligible,
        IReadOnlyList<SubmissionBlockerContext> References, IReadOnlyList<string> Codes);
    internal sealed record Snapshot(SubmissionPackage Package, SubmissionReadinessResult? Readiness, string State, IReadOnlyList<Check> Checks,
        bool CoordinatorEligible, bool ManagerEligible, bool Unmapped);
    sealed record Seed(SubmissionPackage Package, IReadOnlyList<SubmissionCheck> Checks, SubmissionReadinessResult? Readiness, string State);
    readonly Dictionary<Guid, Seed> seeds = [];
    readonly Dictionary<Guid, Snapshot> data = [];

    public void SeedPackage(SubmissionPackage package, IReadOnlyList<SubmissionCheck> checks, SubmissionReadinessResult? readiness, string state)
    {
        if (package.ProjectId != project.Id) throw ApiException.NotFound();
        seeds[package.Id] = new(package, checks, readiness, state);
    }

    public async Task<(Snapshot Package, Check Check)?> FindCheck(Guid id)
    {
        var cached = data.Values.FirstOrDefault(x => x.Checks.Any(c => c.Row.Id == id));
        if (cached is not null) return (cached, cached.Checks.Single(c => c.Row.Id == id));
        var row = await db.SubmissionChecks.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id);
        if (row is null) return null;
        var package = await Read(row.PackageId);
        var check = package?.Checks.SingleOrDefault(c => c.Row.Id == id);
        return package is not null && check is not null ? (package, check) : null;
    }

    public async Task<Snapshot?> Read(Guid id)
    {
        if (data.TryGetValue(id, out var cached)) return cached;
        if (!seeds.TryGetValue(id, out var seed))
        {
            var package = await db.SubmissionPackages.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id);
            if (package is null) return null;
            var readiness = package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled ? null : await SubmissionReadiness.Evaluate(db, package);
            var state = readiness is null || package.Status == SubmissionStatus.Draft ? package.Status : readiness.Ready ? SubmissionStatus.Ready : SubmissionStatus.Checking;
            seed = new(package, await db.SubmissionChecks.AsNoTracking().Where(x => x.PackageId == id && x.ManifestVersion == package.ManifestVersion).ToListAsync(), readiness, state);
        }
        var terminal = seed.Package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled;
        var checks = seed.Checks.Where(x => x.ProjectId == project.Id && x.PackageId == id && x.ManifestVersion == seed.Package.ManifestVersion).ToArray();
        var actorIds = checks.Select(x => x.OwnerId).Append(seed.Package.CoordinatorId).Append(project.ProjectManagerId).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(x => actorIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var roles = (await db.UserRoles.AsNoTracking().Where(x => actorIds.Contains(x.UserId)).ToListAsync()).GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.Select(r => r.Role).ToHashSet());
        var members = await db.ProjectMembers.AsNoTracking().Where(x => x.ProjectId == project.Id && x.RemovedAt == null).ToListAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(x => x.ProjectId == project.Id && x.IsActive).ToListAsync();
        var eligible = terminal ? new HashSet<Guid>() : (await Coordination.People(db, project).Select(x => x.Id).ToListAsync()).ToHashSet();
        (Actor Actor, ProjectContext Context) Person(Guid person)
        {
            var actor = new Actor(person, users.GetValueOrDefault(person)?.IsActive == true, roles.GetValueOrDefault(person) ?? []);
            var context = new ProjectContext(project.Id, project.Status, project.Visibility, project.ProjectManagerId, project.AllowViewerComments,
                members.Where(x => x.UserId == person).SelectMany(x => x.Roles).ToHashSet(), disciplines.Where(x => x.LeadUserId == person).Select(x => x.Id).ToHashSet(), null);
            return (actor, context);
        }
        var coord = Person(seed.Package.CoordinatorId); var manager = Person(project.ProjectManagerId);
        var infos = seed.Readiness?.Blockers ?? [];
        var unmapped = infos.Any(x => x.ContextInfo is not { Known: true }
            || x.ContextInfo.CheckId is { } cid && !checks.Any(c => c.Id == cid)
            || x.ContextInfo.CheckId is null && x.Code is not ("submission.empty_manifest" or "submission.empty_checks"));
        var rows = new List<Check>();
        foreach (var row in checks)
        {
            var blockers = infos.Where(x => x.ContextInfo?.CheckId == row.Id).ToArray();
            var codes = blockers.Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray();
            var derived = row.Kind != SubmissionCheckKind.Applicability && row.EvidenceRule is not null;
            var state = terminal ? SubmissionCheckStatus.All.Contains(row.Status, StringComparer.Ordinal) ? row.Status : null
                : SubmissionCheckKind.All.Contains(row.Kind, StringComparer.Ordinal) && seed.Readiness is not null
                    && (!derived || seed.Readiness.AssessedCheckIds.Contains(row.Id) || codes.Length > 0)
                    ? RecordContextRules.SubmissionCheckState(row.Status, derived, codes) : null;
            var owner = Person(row.OwnerId);
            rows.Add(new(row, state, blockers.Length > 0, state is not null && blockers.All(x => x.ContextInfo?.Known == true),
                (seed.Package.Status is SubmissionStatus.Checking or SubmissionStatus.Ready) && eligible.Contains(row.OwnerId) && Permissions.SignSubmissionCheck(owner.Actor, owner.Context, row.OwnerId).Ok,
                blockers.Select(x => x.ContextInfo!).Where(x => x.RecordKind is not null && x.RecordId is not null)
                    .DistinctBy(x => (x.RecordKind, x.RecordId)).ToArray(), codes));
        }
        return data[id] = new(seed.Package, seed.Readiness, seed.State, rows,
            Permissions.CoordinateSubmission(coord.Actor, coord.Context, seed.Package.CoordinatorId).Ok,
            Permissions.AuthoriseSubmission(manager.Actor, manager.Context).Ok, unmapped);
    }
}
