using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// One current-actor scope shared by Home and the lists its figures open. Arrays
/// stay on the server, so a large cohort is never silently truncated in a URL.
public sealed record HomePresetScope(string Code, Guid[] ProjectIds, Guid[] DisciplineIds, Guid[] PersonIds)
{
    public IQueryable<Project> ApplyProjects(IQueryable<Project> source) => source.Where(p => ProjectIds.Contains(p.Id));
    public IQueryable<WorkTask> ApplyTasks(IQueryable<WorkTask> source)
    {
        var scoped = source.Where(t => ProjectIds.Contains(t.ProjectId));
        return Code switch
        {
            HomePresets.DisciplineLead => scoped.Where(t => DisciplineIds.Contains(t.ProjectDisciplineId)),
            HomePresets.Supervisor => scoped.Where(t => t.AssigneeId != null && PersonIds.Contains(t.AssigneeId.Value)),
            _ => scoped,
        };
    }
    public IQueryable<Deliverable> ApplyDeliverables(IQueryable<Deliverable> source)
    {
        var scoped = source.Where(d => ProjectIds.Contains(d.ProjectId));
        return Code switch
        {
            HomePresets.DisciplineLead => scoped.Where(d => DisciplineIds.Contains(d.ProjectDisciplineId)),
            HomePresets.Supervisor => scoped.Where(d => d.OwnerId != null && PersonIds.Contains(d.OwnerId.Value)),
            _ => scoped,
        };
    }
    public IQueryable<Milestone> ApplyMilestones(IQueryable<Milestone> source)
    {
        var scoped = source.Where(m => ProjectIds.Contains(m.ProjectId));
        return Code switch
        {
            HomePresets.DisciplineLead => scoped.Where(m => m.ProjectDisciplineId != null && DisciplineIds.Contains(m.ProjectDisciplineId.Value)),
            // Milestones have no person owner. A supervisor filter cannot invent one.
            HomePresets.Supervisor => scoped.Where(_ => false),
            _ => scoped,
        };
    }
}

public static class HomePresets
{
    public const string ProjectManager = "ProjectManager", DisciplineLead = "DisciplineLead", Supervisor = "Supervisor";
    public static readonly string[] Codes = [ProjectManager, DisciplineLead, Supervisor];

    public static async Task<string[]> Available(Access access, HubDb db, OrgSettings settings)
    {
        if (!settings.IsFlagOn("home_overview") || !access.Actor.IsActive) return [];
        var actor = access.Actor;
        var visible = Scope.Projects(access, db, Scope.All);
        var available = new List<string>();
        if (actor.SystemPM || await visible.AnyAsync(p => p.ProjectManagerId == actor.Id
            || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == actor.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM))))
            available.Add(ProjectManager);
        if (await db.ProjectDisciplines.AnyAsync(d => d.IsActive && d.LeadUserId == actor.Id && visible.Any(p => p.Id == d.ProjectId)))
            available.Add(DisciplineLead);
        if (actor.Supervisor) available.Add(Supervisor);
        return [.. available];
    }

    /// A missing role is an explicit refusal, never an empty filter that widens the
    /// query. A saved preference is resolved afresh, and GET never saves a choice.
    public static async Task<HomePresetScope?> Resolve(Access access, HubDb db, OrgSettings settings, string? code)
    {
        if (!settings.IsFlagOn("home_overview") || string.IsNullOrWhiteSpace(code)) return null;
        Check.OneOf(code, Codes, "homePreset");
        Check.That((await Available(access, db, settings)).Contains(code), "homePreset", "error.invalid");
        var actor = access.Actor;
        var visible = Scope.Projects(access, db, Scope.All);
        if (code == ProjectManager)
        {
            var ids = await visible.Where(p => p.ProjectManagerId == actor.Id
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == actor.Id && m.RemovedAt == null && m.Roles.Contains(ProjectRole.PM)))
                .OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync();
            return new(code, ids, [], []);
        }
        if (code == DisciplineLead)
        {
            var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.IsActive && d.LeadUserId == actor.Id && visible.Any(p => p.Id == d.ProjectId))
                .OrderBy(d => d.Id).Select(d => new { d.Id, d.ProjectId }).ToArrayAsync();
            return new(code, [.. disciplines.Select(d => d.ProjectId).Distinct().Order()], [.. disciplines.Select(d => d.Id)], []);
        }
        var org = settings.IsFlagOn("org_structure");
        // Before §54, this preset is explicitly direct reports, even for an
        // Executive who can separately inspect the whole organisation.
        var people = org
            ? await PeopleScope.Load(db, actor, settings.IncludeIndirectReports, includeLedUnits: true)
            : await db.Users.AsNoTracking().Where(u => u.IsActive && u.SupervisorId == actor.Id).Select(u => u.Id).ToHashSetAsync();
        return new(code, await visible.OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync(), [], [.. people.Order()]);
    }
}
