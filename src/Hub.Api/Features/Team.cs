using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Team membership rules shared by the Team tab, project creation, auto-add on assignment and
/// supervisor staffing (§12.2 TM-01..TM-08, §12.18 ASG-01..ASG-04, ASG-10, ASG-11).
public sealed class TeamService(HubDb db, Notifier notify, AuditContext audit, TimeProvider clock, CurrentUser me)
{
    /// Looks in this unit of work first, so a person added earlier in the same save is not added twice.
    public async Task<ProjectMember?> Active(Guid projectId, Guid userId) =>
        db.ProjectMembers.Local.FirstOrDefault(m => m.ProjectId == projectId && m.UserId == userId && m.RemovedAt == null)
        ?? await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId && m.RemovedAt == null);

    /// Adds a person (or merges roles, TM-01) and switches following on (ASG-01).
    public async Task<ProjectMember> Add(Project p, Guid userId, IEnumerable<string> roles, Guid? primaryDisciplineId, bool notifyPerson = true)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId) ?? throw ApiException.Invalid("userId", "error.not_found");
        Check.That(user.IsActive, "userId", "team.inactive_user");
        var wanted = roles.Distinct().ToList();
        Check.That(wanted.Count > 0 && wanted.All(r => ProjectRole.All.Contains(r)), "roles", "error.one_of", string.Join(", ", ProjectRole.All));
        var m = await Active(p.Id, userId);
        var isNew = m is null;
        if (m is null)
        {
            m = new ProjectMember { ProjectId = p.Id, UserId = userId, Roles = wanted, PrimaryDisciplineId = primaryDisciplineId, AddedBy = me.Resolved ? me.Id : null, AddedAt = clock.GetUtcNow() };
            db.ProjectMembers.Add(m);
            audit.Note(m, action: "MemberAdded", key: user.Email, categories: ["assignment"]);
        }
        else
        {
            var merged = m.Roles.Union(wanted).ToList();
            if (merged.Count != m.Roles.Count) m.Roles = merged;
            if (primaryDisciplineId is not null && m.PrimaryDisciplineId is null) m.PrimaryDisciplineId = primaryDisciplineId;
        }
        await Follow(p.Id, userId, m.Roles);
        if (isNew && notifyPerson)
        {
            var level = OnlyReviewer(m.Roles) ? FollowLevel.MyItemsOnly : FollowLevel.AllActivity;
            await notify.Send(NotificationEvents.AddedToProject, userId, Item(p),
                Text.Get("notify.added_to_project", await notify.ActorName(), p.ProjectNumber, p.Name, RoleList(m.Roles)),
                Text.Get("notify.follow_hint", Text.Get($"follow.{level}")));
            await SupervisorNotice(user, p, "notify.staff_added");
        }
        return m;
    }

    static bool OnlyReviewer(IEnumerable<string> roles) => roles.All(r => r == ProjectRole.Reviewer);

    /// ASG-01: Assignment follows are created or raised; a Manual follow is never touched by the system.
    public async Task Follow(Guid projectId, Guid userId, IEnumerable<string> roles)
    {
        var level = OnlyReviewer(roles) ? FollowLevel.MyItemsOnly : FollowLevel.AllActivity;
        var f = db.Follows.Local.FirstOrDefault(x => x.ProjectId == projectId && x.UserId == userId)
            ?? await db.Follows.FirstOrDefaultAsync(x => x.ProjectId == projectId && x.UserId == userId);
        var now = clock.GetUtcNow();
        if (f is null) db.Follows.Add(new ProjectFollow { ProjectId = projectId, UserId = userId, Level = level, Source = FollowSource.Assignment, CreatedAt = now, UpdatedAt = now, LastSeenAt = now });
        else if (f.Source == FollowSource.Assignment && f.Level == FollowLevel.MyItemsOnly && level == FollowLevel.AllActivity) { f.Level = level; f.UpdatedAt = now; }
    }

    /// TM-06: assigning work or a review to a non-member adds them and tells the PM (AC-TEAM-01).
    public async Task EnsureMember(Project p, Guid? userId, string role)
    {
        if (userId is not { } uid || await Active(p.Id, uid) is not null) return;
        await Add(p, uid, [role], null);
        var name = await notify.Name(uid);
        await notify.Send(NotificationEvents.MemberAutoAdded, p.ProjectManagerId, Item(p),
            Text.Get("notify.auto_added", name, p.ProjectNumber, Text.Get($"role.{role}")));
    }

    /// Removes a member (TM-03, TM-04, TM-08, ASG-04). Open items are reassigned or left flagged.
    public async Task<int> Remove(Project p, ProjectMember m, Guid? reassignTo, string? reason, bool bySupervisor = false)
    {
        if (m.UserId == p.ProjectManagerId) throw ApiException.Rule("primary_pm", "team.primary_pm");
        var moved = 0;
        if (reassignTo is { } to)
        {
            Check.That(await db.Users.AnyAsync(u => u.Id == to && u.IsActive), "reassignTo", "team.inactive_user");
            foreach (var t in await db.Tasks.Where(t => t.ProjectId == p.Id && t.AssigneeId == m.UserId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled).ToListAsync())
            { t.AssigneeId = to; moved++; }
            foreach (var t in await db.Tasks.Where(t => t.ProjectId == p.Id && t.ReviewerId == m.UserId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled).ToListAsync())
            { t.ReviewerId = to == t.AssigneeId ? null : to; moved++; }
            foreach (var d in await db.Deliverables.Where(d => d.ProjectId == p.Id && d.OwnerId == m.UserId && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled).ToListAsync())
            { d.OwnerId = to; moved++; }
            if (moved > 0) await EnsureMember(p, to, ProjectRole.TeamMember);
        }
        m.RemovedAt = clock.GetUtcNow();
        m.RemovedBy = me.Id;
        audit.Note(m, action: "MemberRemoved", reason: reason, categories: ["assignment", "deletion"]);
        foreach (var pd in await db.ProjectDisciplines.Where(d => d.ProjectId == p.Id && d.LeadUserId == m.UserId).ToListAsync()) pd.LeadUserId = null;
        foreach (var c in await db.Collaborators.Where(c => c.UserId == m.UserId && db.Tasks.Any(t => t.Id == c.TaskId && t.ProjectId == p.Id)).ToListAsync()) db.Collaborators.Remove(c);
        var f = await db.Follows.FirstOrDefaultAsync(x => x.ProjectId == p.Id && x.UserId == m.UserId);
        if (f is not null && (f.Source == FollowSource.Assignment || p.Visibility == Visibility.Restricted)) db.Follows.Remove(f);
        var user = await db.Users.FirstAsync(u => u.Id == m.UserId);
        await notify.Send(NotificationEvents.AddedToProject, m.UserId, Item(p), Text.Get("notify.removed_from_project", await notify.ActorName(), p.ProjectNumber, p.Name));
        if (bySupervisor)
            await notify.Send(NotificationEvents.SupervisorStaffing, p.ProjectManagerId, Item(p), Text.Get("notify.supervisor_removed", await notify.ActorName(), user.DisplayName, p.ProjectNumber, moved));
        else await SupervisorNotice(user, p, "notify.staff_removed");
        return moved;
    }

    /// Sets or clears a Discipline Lead; the lead must be active and is added to the team (TM-07, AC-TEAM-02).
    public async Task SetLead(Project p, ProjectDiscipline pd, Guid? leadId)
    {
        if (pd.LeadUserId == leadId) return;
        if (leadId is { } uid)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Id == uid) ?? throw ApiException.Invalid("leadUserId", "error.not_found");
            Check.That(user.IsActive, "leadUserId", "team.inactive_user");
            var existing = await Active(p.Id, uid);
            if (existing is null) await Add(p, uid, [ProjectRole.TeamMember], pd.Id, notifyPerson: false);
            else { existing.PrimaryDisciplineId ??= pd.Id; await Follow(p.Id, uid, [ProjectRole.TeamMember]); }
            var dname = await db.Disciplines.Where(d => d.Id == pd.DisciplineId).Select(d => d.Name).FirstAsync();
            await notify.Send(NotificationEvents.BecameDisciplineLead, uid, Item(p), Text.Get("notify.became_lead", p.ProjectNumber, p.Name, dname));
            await SupervisorNotice(user, p, "notify.staff_lead");
        }
        pd.LeadUserId = leadId;
    }

    /// ASG-11: the person's supervisor hears when someone else changes their assignments.
    async Task SupervisorNotice(AppUser person, Project p, string key)
    {
        if (person.SupervisorId is not { } sup || sup == me.Id || person.Id == me.Id) return;
        await notify.Send(NotificationEvents.StaffAssignment, sup, Item(p), Text.Get(key, await notify.ActorName(), person.DisplayName, p.ProjectNumber, p.Name));
    }

    /// E-02: the new primary PM gets the PM role; the previous PM stays as a Team Member.
    public async Task ChangePrimaryPm(Project p, Guid newPm)
    {
        if (newPm == p.ProjectManagerId) return;
        var old = p.ProjectManagerId;
        var m = await Add(p, newPm, [ProjectRole.PM], null);
        if (!m.Roles.Contains(ProjectRole.PM)) m.Roles = [.. m.Roles, ProjectRole.PM];
        var oldM = await Active(p.Id, old);
        if (oldM is not null && oldM.Roles.Contains(ProjectRole.PM))
        {
            var roles = oldM.Roles.Where(r => r != ProjectRole.PM).ToList();
            oldM.Roles = roles.Count == 0 ? [ProjectRole.TeamMember] : roles;
        }
        p.ProjectManagerId = newPm;
        await notify.Send(NotificationEvents.AddedToProject, newPm, Item(p), Text.Get("notify.new_pm", p.ProjectNumber, p.Name));
    }

    public static string RoleList(IEnumerable<string> roles) => string.Join(", ", roles.Select(r => Text.Get($"role.{r}")));
    public static NotifyItem Item(Project p) => new(p.Id, ItemType.Project, p.Id, p.ProjectNumber, $"/projects/{p.ProjectNumber}", p.ProjectNumber);
}

public static class TeamEndpoints
{
    public sealed record MemberBody(Guid UserId, string[] Roles, Guid? PrimaryDisciplineId);
    public sealed record DisciplineBody(Guid DisciplineId, Guid? LeadUserId);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/team", async (Guid id, Access access, HubDb db) =>
        {
            var (p, ctx) = await access.Project(id, track: false);
            var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == id).OrderBy(d => d.SortOrder)
                .Select(d => new { d.Id, d.DisciplineId, d.Discipline!.Name, d.Discipline.Code, d.Discipline.Colour, d.LeadUserId, d.IsActive, d.SortOrder }).ToListAsync();
            var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == id && m.RemovedAt == null)
                .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new { m.Id, m.UserId, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.IsActive, u.SupervisorId, m.Roles, m.PrimaryDisciplineId, m.AddedAt, m.AddedBy })
                .OrderBy(m => m.DisplayName).ToListAsync();
            var leadIds = disciplines.Where(d => d.LeadUserId != null).Select(d => d.LeadUserId!.Value).ToHashSet();
            var a = access.Actor;
            return new
            {
                disciplines,
                members = members.Select(m => new { m.Id, m.UserId, m.DisplayName, m.Email, m.JobTitle, m.OfficeId, m.IsActive, m.Roles, m.PrimaryDisciplineId, m.AddedAt, m.AddedBy,
                    LeadOf = disciplines.Where(d => d.LeadUserId == m.UserId).Select(d => d.Id), IsPrimaryPm = m.UserId == p.ProjectManagerId,
                    CanStaff = Permissions.StaffOnProject(a, ctx, m.SupervisorId).Ok && m.Roles.All(r => r == ProjectRole.TeamMember) && !leadIds.Contains(m.UserId) }),
                canManage = Permissions.ManageTeam(a, ctx).Ok,
                manageReason = Permissions.ManageTeam(a, ctx).Why is { } w ? Text.Get(w, p.Status) : null,
            };
        });

        // PM, or a Supervisor adding a direct report as Team Member (§8.5.1, ASG-10).
        api.MapPost("/projects/{id:guid}/members", async (Guid id, MemberBody body, Access access, HubDb db, TeamService team, Notifier notify) =>
        {
            var (p, ctx) = await access.Project(id);
            var person = await db.Users.FirstOrDefaultAsync(u => u.Id == body.UserId) ?? throw ApiException.Invalid("userId", "error.not_found");
            var asPm = Permissions.ManageTeam(access.Actor, ctx);
            if (!asPm)
            {
                var staff = Permissions.StaffOnProject(access.Actor, ctx, person.SupervisorId);
                Access.Demand(staff.Ok && body.Roles.All(r => r == ProjectRole.TeamMember) ? Allow.Yes : staff.Ok ? Allow.No("perm.pm") : staff);
            }
            if (body.PrimaryDisciplineId is { } pdid) Check.That(await db.ProjectDisciplines.AnyAsync(d => d.Id == pdid && d.ProjectId == id), "primaryDisciplineId", "error.not_found");
            var m = await team.Add(p, body.UserId, body.Roles, body.PrimaryDisciplineId);
            if (!asPm)
                await notify.Send(NotificationEvents.SupervisorStaffing, p.ProjectManagerId, TeamService.Item(p),
                    Text.Get("notify.supervisor_added", await notify.ActorName(), person.DisplayName, p.ProjectNumber));
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/projects/{id}/members/{m.Id}", new { m.Id });
        });

        api.MapPatch("/projects/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, JsonElement body, Access access, HubDb db, TeamService team) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
            var patch = new Patch(body);
            var m = await db.ProjectMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.ProjectId == id && x.RemovedAt == null) ?? throw ApiException.NotFound();
            if (patch.Strs("roles") is { } roles)
            {
                Check.That(roles.Length > 0 && roles.All(r => ProjectRole.All.Contains(r)), "roles", "error.one_of", string.Join(", ", ProjectRole.All));
                if (m.UserId == p.ProjectManagerId) Check.That(roles.Contains(ProjectRole.PM), "roles", "team.primary_pm_role");
                m.Roles = [.. roles.Distinct()];
                await team.Follow(id, m.UserId, m.Roles);
            }
            if (patch.Has("primaryDisciplineId")) m.PrimaryDisciplineId = patch.Id("primaryDisciplineId");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // TM-04: open items owned by the person, shown before removal.
        api.MapGet("/projects/{id:guid}/members/{memberId:guid}/open-items", async (Guid id, Guid memberId, Access access, HubDb db) =>
        {
            await access.Project(id, track: false);
            var m = await db.ProjectMembers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == memberId && x.ProjectId == id) ?? throw ApiException.NotFound();
            return await OpenItems(db, id, m.UserId);
        });

        api.MapDelete("/projects/{id:guid}/members/{memberId:guid}", async (Guid id, Guid memberId, Guid? reassignTo, string? reason, Access access, HubDb db, TeamService team) =>
        {
            var (p, ctx) = await access.Project(id);
            var m = await db.ProjectMembers.FirstOrDefaultAsync(x => x.Id == memberId && x.ProjectId == id && x.RemovedAt == null) ?? throw ApiException.NotFound();
            var person = await db.Users.FirstAsync(u => u.Id == m.UserId);
            var asPm = Permissions.ManageTeam(access.Actor, ctx);
            var leads = await db.ProjectDisciplines.AnyAsync(d => d.ProjectId == id && d.LeadUserId == m.UserId);
            if (!asPm)
            {
                var staff = Permissions.StaffOnProject(access.Actor, ctx, person.SupervisorId);
                Access.Demand(staff.Ok && m.Roles.All(r => r == ProjectRole.TeamMember) && !leads ? Allow.Yes : staff.Ok ? Allow.No("perm.pm") : staff);
            }
            var moved = await team.Remove(p, m, reassignTo, reason, bySupervisor: !asPm);
            await db.SaveChangesAsync();
            return Results.Ok(new { reassigned = moved });
        });

        api.MapPost("/projects/{id:guid}/disciplines", async (Guid id, DisciplineBody body, Access access, HubDb db, TeamService team) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
            Check.That(await db.Disciplines.AnyAsync(d => d.Id == body.DisciplineId && d.IsActive), "disciplineId", "error.not_found");
            var existing = await db.ProjectDisciplines.FirstOrDefaultAsync(d => d.ProjectId == id && d.DisciplineId == body.DisciplineId);
            if (existing is not null)
            {
                // TM-02: a discipline appears once; adding it again reactivates it.
                existing.IsActive = true;
                if (body.LeadUserId is not null) await team.SetLead(p, existing, body.LeadUserId);
                await db.SaveChangesAsync();
                return Results.Ok(new { existing.Id });
            }
            var pd = new ProjectDiscipline { ProjectId = id, DisciplineId = body.DisciplineId, SortOrder = await db.ProjectDisciplines.CountAsync(d => d.ProjectId == id) };
            pd.Discipline = await db.Disciplines.FirstAsync(d => d.Id == body.DisciplineId);
            db.ProjectDisciplines.Add(pd);
            await team.SetLead(p, pd, body.LeadUserId);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/projects/{id}/disciplines/{pd.Id}", new { pd.Id });
        });

        api.MapPatch("/projects/{id:guid}/disciplines/{pdId:guid}", async (Guid id, Guid pdId, JsonElement body, Access access, HubDb db, TeamService team) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
            var patch = new Patch(body);
            var pd = await db.ProjectDisciplines.Include(d => d.Discipline).FirstOrDefaultAsync(d => d.Id == pdId && d.ProjectId == id) ?? throw ApiException.NotFound();
            if (patch.Has("leadUserId")) await team.SetLead(p, pd, patch.Id("leadUserId"));
            if (patch.Bool("isActive") is { } active) pd.IsActive = active;
            if (patch.Int("sortOrder") is { } so) pd.SortOrder = so;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // TM-05: only a discipline without non-cancelled work can be removed; otherwise deactivate.
        api.MapDelete("/projects/{id:guid}/disciplines/{pdId:guid}", async (Guid id, Guid pdId, Access access, HubDb db) =>
        {
            var (_, ctx) = await access.Project(id);
            Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
            var pd = await db.ProjectDisciplines.Include(d => d.Discipline).FirstOrDefaultAsync(d => d.Id == pdId && d.ProjectId == id) ?? throw ApiException.NotFound();
            var used = await db.Tasks.IgnoreQueryFilters().AnyAsync(t => t.ProjectDisciplineId == pdId && t.DeletedAt == null && t.Status != TaskStatuses.Cancelled)
                || await db.Deliverables.IgnoreQueryFilters().AnyAsync(d => d.ProjectDisciplineId == pdId && d.DeletedAt == null && d.Status != DeliverableStatus.Cancelled);
            if (used) throw ApiException.Rule("discipline_in_use", "team.discipline_in_use");
            if (await db.Tasks.IgnoreQueryFilters().AnyAsync(t => t.ProjectDisciplineId == pdId) || await db.Deliverables.IgnoreQueryFilters().AnyAsync(d => d.ProjectDisciplineId == pdId)
                || await db.ProjectMembers.AnyAsync(m => m.PrimaryDisciplineId == pdId) || await db.Milestones.IgnoreQueryFilters().AnyAsync(m => m.ProjectDisciplineId == pdId))
                pd.IsActive = false; // referenced by history: deactivate rather than delete
            else db.ProjectDisciplines.Remove(pd);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }

    public static async Task<object> OpenItems(HubDb db, Guid projectId, Guid userId)
    {
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && (t.AssigneeId == userId || t.ReviewerId == userId)
            && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, Role = t.AssigneeId == userId ? "Assignee" : "Reviewer" }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == projectId && d.OwnerId == userId
            && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled)
            .Select(d => new { d.Id, d.Key, d.Name, d.Status, d.DueDate }).ToListAsync();
        return new { tasks, deliverables, count = tasks.Count + deliverables.Count };
    }
}
