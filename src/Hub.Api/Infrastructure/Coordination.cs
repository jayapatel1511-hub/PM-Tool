using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Shared transaction/access plumbing for the concrete review and revision workflows.
public static class Coordination
{
    public sealed record AtomicCommand;
    public sealed record Result(Guid Id, int RowVersion);
    public static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, JsonOpts.Web))));
    public static string Identity(SourceRevision r) => r.SourceIdentity.Length > 0 ? r.SourceIdentity : $"deliverable:{r.DeliverableId}";
    public static async Task Lock(HubDb db, Guid project) => await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.project WHERE id = {0} FOR UPDATE", project).ToListAsync();
    public static async Task<Result> Run(Guid projectId, Guid requestId, object payload, Access access, HubDb db, TimeProvider clock, Func<Project, ProjectContext, Task<Audited>> action)
    {
        Check.That(requestId != Guid.Empty, "requestId", "error.required");
        await access.Project(projectId, false);
        return await Tx.Run(db, async () => {
            await Lock(db, projectId);
            var (p, ctx) = await access.Project(projectId);
            Access.Demand(Permissions.CoordinationWrite(access.Actor, ctx));
            var hash = Hash(payload);
            var prior = await db.CoordinationCommands.SingleOrDefaultAsync(c => c.ProjectId == projectId && c.ActorId == access.Me.Id && c.RequestId == requestId);
            if (prior is not null) {
                if (prior.PayloadHash != hash) throw ApiException.Rule("idempotency_key_reused", "error.idempotency_reused");
                return new Result(prior.ResultId, prior.ResultVersion);
            }
            if (p.Status == ProjectStatus.Complete) {
                var body = JsonSerializer.SerializeToElement(payload, JsonOpts.Web).GetProperty("body");
                var reason = new[] { "reason", "rationale", "description" }.Select(k => body.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                Check.Reason(reason);
            }
            var item = await action(p, ctx);
            await db.SaveChangesAsync();
            db.CoordinationCommands.Add(new CoordinationCommand { ProjectId = projectId, ActorId = access.Me.Id, RequestId = requestId, PayloadHash = hash, ResultId = item.Id, ResultVersion = item.RowVersion, CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return new Result(item.Id, item.RowVersion);
        });
    }
    public static void Version(Audited row, int? version)
    {
        if (version is null) throw new ApiException(428, "precondition_required", Text.Get("error.if_match"));
        if (row.RowVersion != version) throw ApiException.Conflict("concurrency_conflict", "coord.stale", new { currentRowVersion = row.RowVersion, id = row.Id });
    }
    public static IQueryable<AppUser> People(HubDb db, Project p) => db.Users.Where(u => u.IsActive
        && (!db.UserRoles.Any(r => r.UserId == u.Id && r.Role == SystemRole.ReadOnly) || db.UserRoles.Any(r => r.UserId == u.Id && r.Role == SystemRole.Admin))
        && (u.Id == p.ProjectManagerId || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == u.Id && m.RemovedAt == null
            && (m.Roles.Contains(ProjectRole.PM) || m.Roles.Contains(ProjectRole.TeamMember) || m.Roles.Contains(ProjectRole.Reviewer)))
            || db.ProjectDisciplines.Any(d => d.ProjectId == p.Id && d.IsActive && d.LeadUserId == u.Id)));
    public static async Task Person(HubDb db, Project p, Guid id, string field = "ownerId") => Check.That(await People(db, p).AnyAsync(u => u.Id == id), field, "coord.person");
    public static async Task Discipline(HubDb db, Guid project, Guid id) => Check.That(await db.ProjectDisciplines.AnyAsync(d => d.Id == id && d.ProjectId == project && d.IsActive), "projectDisciplineId", "coord.reference");
    public static string Url(string? url) { var s = Check.Required(url, "evidenceUrl", 2000); Check.That(Uri.TryCreate(s, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.Scheme == "http"), "evidenceUrl", "link.invalid"); return s; }
    public sealed record Work(string Type, Guid Id, string Key, string Name, Guid OwnerId, Guid DisciplineId, int RowVersion, string Status, Guid? AuthorId);
    public static async Task<Work> Target(HubDb db, Project p, string type, Guid id, bool lockRow = true)
    {
        Check.OneOf(type, ["Task", "Deliverable"], "targetType");
        if (lockRow) {
            var sql = type == "Task" ? "SELECT id AS \"Value\" FROM hub.task WHERE id = {0} AND project_id = {1} FOR SHARE" : "SELECT id AS \"Value\" FROM hub.deliverable WHERE id = {0} AND project_id = {1} FOR SHARE";
            await db.Database.SqlQueryRaw<Guid>(sql, id, p.Id).ToListAsync();
        }
        Work? w = type == "Task"
            ? await db.Tasks.AsNoTracking().Where(t => t.Id == id && t.ProjectId == p.Id && t.Status != TaskStatuses.Cancelled).Select(t => new Work(type, t.Id, t.Key, t.Name, t.AssigneeId ?? Guid.Empty, t.ProjectDisciplineId, t.RowVersion, t.Status, t.CreatedBy)).FirstOrDefaultAsync()
            : await db.Deliverables.AsNoTracking().Where(d => d.Id == id && d.ProjectId == p.Id && d.Status != DeliverableStatus.Cancelled).Select(d => new Work(type, d.Id, d.Key, d.Name, d.OwnerId ?? Guid.Empty, d.ProjectDisciplineId, d.RowVersion, d.Status, d.CreatedBy)).FirstOrDefaultAsync();
        if (w is null) throw ApiException.Invalid("targetId", "coord.reference");
        await Person(db, p, w.OwnerId); await Discipline(db, p.Id, w.DisciplineId);
        return w;
    }
    public static async Task<Guid[]> Authors(HubDb db, Guid deliverable)
    {
        var d = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(d => d.Id == deliverable) ?? throw ApiException.Invalid("sourceRevisionId", "coord.reference");
        var ids = await db.Tasks.Where(t => t.DeliverableId == deliverable).Select(t => new { t.AssigneeId, t.CreatedBy }).ToListAsync();
        return ids.SelectMany(t => new[] { t.AssigneeId, t.CreatedBy }).Concat([d.OwnerId, d.CreatedBy]).OfType<Guid>().Distinct().ToArray();
    }
    public static async Task<SourceRevision> Snapshot(HubDb db, Project p, Deliverable d, string revision, string url)
    {
        var identity = $"deliverable:{d.Id}";
        var current = await db.SourceHeads.Where(h => h.ProjectId == p.Id && h.Identity == identity).Join(db.SourceRevisions, h => h.CurrentRevisionId, r => r.Id, (h, r) => r).SingleOrDefaultAsync();
        if (current != null && current.Revision == revision && current.Url == url) return current;
        var hash = Hash(new { d.Id, d.RowVersion, DeclaredRevision = revision, SourceUrl = url });
        var r = await db.SourceRevisions.SingleOrDefaultAsync(r => r.ProjectId == p.Id && r.IdentityHash == hash);
        if (r is null) {
            r = new SourceRevision { ProjectId = p.Id, DeliverableId = d.Id, SourceRowVersion = d.RowVersion, IdentityHash = hash, SourceIdentity = $"deliverable:{d.Id}",
                SourceSystem = "Deliverable", ExternalIdentifier = d.Key, SourceKey = d.Key, Title = d.Name, Revision = revision, Url = url,
                Issuer = (await db.Users.Where(u => u.Id == d.OwnerId).Select(u => u.DisplayName).FirstOrDefaultAsync()) ?? p.ProjectNumber,
                Scope = d.Name, AuthorIds = await Authors(db, d.Id) };
            db.SourceRevisions.Add(r);
        }
        if (!await db.SourceHeads.AnyAsync(h => h.ProjectId == p.Id && h.Identity == Identity(r)) && !db.SourceHeads.Local.Any(h => h.ProjectId == p.Id && h.Identity == Identity(r)))
            db.SourceHeads.Add(new SourceHead { ProjectId = p.Id, Identity = Identity(r), CurrentRevisionId = r.Id, OwnerId = d.OwnerId ?? p.ProjectManagerId, ProjectDisciplineId = d.ProjectDisciplineId });
        return r;
    }
    public static async Task<SourceRevision> Revision(HubDb db, Guid project, Guid id) => await db.SourceRevisions.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == project) ?? throw ApiException.NotFound();
    public static async Task<SourceHead?> Head(HubDb db, SourceRevision r) { var identity = Identity(r); return await db.SourceHeads.SingleOrDefaultAsync(h => h.ProjectId == r.ProjectId && h.Identity == identity); }
    public static async Task<bool> Published(HubDb db, SourceRevision revision)
    {
        var head = await Head(db, revision);
        if (head is null) return !await db.ChangeNotices.AnyAsync(c => c.NewRevisionId == revision.Id); // legacy handoff registration
        Guid? cursor = head.CurrentRevisionId;
        var seen = new HashSet<Guid>();
        while (cursor is { } id && seen.Add(id)) { if (id == revision.Id) return true; cursor = await db.SourceRevisions.Where(r => r.Id == id).Select(r => r.SupersedesId).FirstOrDefaultAsync(); }
        return false;
    }
    public static async Task<InputUse> Adopt(HubDb db, Project p, Work w, SourceRevision revision, string purpose, string reason, Guid actor, DateTimeOffset now)
    {
        var identity = Identity(revision);
        var use = await db.InputUses.SingleOrDefaultAsync(u => u.ProjectId == p.Id && u.TargetType == w.Type && u.TargetId == w.Id && u.SourceIdentity == identity);
        if (use is null) { use = new InputUse { ProjectId = p.Id, TargetType = w.Type, TargetId = w.Id, SourceIdentity = identity }; db.InputUses.Add(use); }
        use.OwnerId = w.OwnerId; use.SourceRevisionId = revision.Id; use.IntendedUse = Check.Required(purpose, "intendedUse", 2000); use.AdoptedAt = now; use.AdoptedBy = actor;
        db.Audit.Note(use, reason: reason);
        db.InputAdoptions.Add(new InputAdoption { ProjectId = p.Id, InputUseId = use.Id, SourceRevisionId = revision.Id, IntendedUse = use.IntendedUse, Reason = reason });
        return use;
    }
}
