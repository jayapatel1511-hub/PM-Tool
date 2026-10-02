using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class MeEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        // Current user, roles, capabilities and the settings the SPA needs to render dates (§25.7 GET /me).
        api.MapGet("/me", async (HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var u = await db.Users.AsNoTracking().Include(x => x.Roles).FirstAsync(x => x.Id == me.Id);
            var s = await store.Get(db);
            var pref = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id) ?? new UserSetting { UserId = me.Id };
            var a = me.Actor;
            var directReports = await db.Users.CountAsync(x => x.SupervisorId == me.Id && x.IsActive);
            // New Item offers Task only to someone who may create one somewhere (§36.1); each project still checks it.
            var uid = me.Id;
            var createTask = !a.ReadOnly && await db.Projects.AnyAsync(p => (p.Status == ProjectStatus.Setup || p.Status == ProjectStatus.Active || p.Status == ProjectStatus.OnHold)
                && (p.ProjectManagerId == uid || db.ProjectDisciplines.Any(d => d.ProjectId == p.Id && d.LeadUserId == uid)
                    || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null
                        && (m.Roles.Contains(ProjectRole.PM) || (m.Roles.Contains(ProjectRole.TeamMember) && m.PrimaryDisciplineId != null)))));
            return new
            {
                u.Id, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.SupervisorId, u.IsTemplateEditor,
                Roles = u.Roles.Select(r => new { r.Role, r.Source }).OrderBy(r => r.Role),
                SystemRoles = me.Roles.Order(),
                Capabilities = new
                {
                    CreateProject = Permissions.CreateProject(a).Ok,
                    CreateTask = createTask,
                    Portfolio = Permissions.ViewPortfolio(a).Ok,
                    Workload = Permissions.ViewWorkload(a).Ok,
                    Staff = Permissions.ViewStaff(a).Ok,
                    AllStaff = Permissions.ViewAllStaff(a),
                    Admin = a.Admin,
                    Templates = Permissions.ManageTemplates(a, u.IsTemplateEditor).Ok,
                    ReadOnly = a.ReadOnly,
                    DirectReports = directReports,
                },
                Settings = new
                {
                    s.DateFormat, s.OrgTimeZone, Today = clock.Today(s), s.IdleTimeoutHours, s.RestrictedProjectsEnabled, s.AllowSelfReview,
                    s.TaskDueSoonDays, s.MilestoneApproachingDays, s.ChainDepthLimit, s.DefaultWeeklyCapacityHours,
                    s.CoordinationLookaheadWeeks, s.WorkingDaysEnabled,
                },
                Preferences = new { pref.DigestEnabled, pref.DigestTimeLocal, pref.DenseRows, pref.WeeklySummaryEnabled },
            };
        });

        // Records a successful sign-in once per session (§20.1 access events).
        api.MapPost("/me/sign-in", async (HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            var u = await db.Users.FirstAsync(x => x.Id == me.Id);
            u.LastSignInAt = clock.GetUtcNow();
            db.LogEvent(ItemType.User, u.Id, "SignedIn", "access", key: u.Email, name: u.DisplayName);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Active people for pickers (G-11: inactive people cannot be chosen; pass active=false to include them).
        api.MapGet("/users", async (HubDb db, string? q, bool? active, string? ids, int? limit) =>
        {
            var query = db.Users.AsNoTracking();
            var idList = Http.Ids(ids);
            if (idList.Length > 0) query = query.Where(u => idList.Contains(u.Id));
            else if (active != false) query = query.Where(u => u.IsActive);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => EF.Functions.ILike(u.DisplayName, $"%{q.Trim()}%") || EF.Functions.ILike(u.Email, $"%{q.Trim()}%"));
            return await query.OrderBy(u => u.DisplayName).Take(Math.Clamp(limit ?? 50, 1, 500))
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.IsActive, u.SupervisorId }).ToListAsync();
        });

        // Active reference data for every picker; inactive entries only appear on items that already use them.
        api.MapGet("/reference", async (HubDb db) => new
        {
            Disciplines = await db.Disciplines.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Code, x.Colour, x.IsActive, x.SortOrder }).ToListAsync(),
            Clients = await db.Clients.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.ShortName, x.IsActive }).ToListAsync(),
            Offices = await db.Offices.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.Code, x.TimeZone, x.IsActive }).ToListAsync(),
            DeliverableTypes = await db.DeliverableTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.DefaultDisciplineId, x.IsActive }).ToListAsync(),
            Phases = await db.Phases.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.IsActive, x.SortOrder }).ToListAsync(),
            ProjectTypes = await db.ProjectTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new { x.Id, x.Name, x.IsActive }).ToListAsync(),
        });
    }
}
