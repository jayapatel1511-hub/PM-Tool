using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Server-side authorisation (§8.9, §25.2): resolves the caller's project context and applies the
/// pure matrix in Hub.Domain.Permissions. Anything the caller may not see is reported as 404.
public sealed class Access(HubDb db, CurrentUser me)
{
    public CurrentUser Me => me;
    public Actor Actor => me.Actor;

    public static void Demand(Allow a)
    {
        if (!a.Ok) throw ApiException.Forbidden(a.Why ?? "perm.admin", a.Arg ?? "");
    }

    public async Task<ProjectContext> Context(Project p)
    {
        var member = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == p.Id && m.UserId == me.Id && m.RemovedAt == null)
            .Select(m => new { m.Roles, m.PrimaryDisciplineId }).FirstOrDefaultAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == p.Id && d.LeadUserId == me.Id).Select(d => d.Id).ToListAsync();
        return new ProjectContext(p.Id, p.Status, p.Visibility, p.ProjectManagerId, p.AllowViewerComments,
            (member?.Roles ?? []).ToHashSet(), leads.ToHashSet(), member?.PrimaryDisciplineId);
    }

    /// Loads a project the caller can view, or 404 (AC-PERM-05: restricted is indistinguishable from missing).
    public async Task<(Project Project, ProjectContext Ctx)> Project(Guid projectId, bool track = true)
    {
        var q = track ? db.Projects : db.Projects.AsNoTracking();
        var p = await q.FirstOrDefaultAsync(x => x.Id == projectId) ?? throw ApiException.NotFound();
        var ctx = await Context(p);
        if (!Permissions.CanView(Actor, ctx)) throw ApiException.NotFound();
        return (p, ctx);
    }

    public async Task<(Project Project, ProjectContext Ctx)> ProjectByKeyOrId(string idOrNumber, bool track = true)
    {
        if (Guid.TryParse(idOrNumber, out var id)) return await Project(id, track);
        var pid = await db.Projects.Where(p => p.ProjectNumber == idOrNumber).Select(p => (Guid?)p.Id).FirstOrDefaultAsync() ?? throw ApiException.NotFound();
        return await Project(pid, track);
    }

    public bool SeesAllRestricted => me.IsAdmin || me.IsExecutive;

    /// Projects the caller may view (§8.7), for list, search, aggregate and export queries.
    public IQueryable<Project> VisibleProjects()
    {
        if (SeesAllRestricted) return db.Projects;
        var uid = me.Id;
        return db.Projects.Where(p => p.Visibility != Visibility.Restricted || p.ProjectManagerId == uid
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null));
    }

    public IQueryable<Guid> VisibleProjectIds() => VisibleProjects().Select(p => p.Id);

    public async Task<HashSet<Guid>> VisibleProjectIdSet() => [.. await VisibleProjectIds().ToListAsync()];
}
