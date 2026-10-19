using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class MeEndpoints
{
    public sealed record PickerUser(Guid Id, string? DisplayName, string? Email, string? JobTitle, Guid? OfficeId,
        Guid? HomeDisciplineId, bool IsActive, Guid? SupervisorId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? OnProject = null);
    public sealed record CurrentSettings(string? DateFormat, string? OrgTimeZone, DateOnly Today, int IdleTimeoutHours, bool RestrictedProjectsEnabled, bool AllowSelfReview,
        int TaskDueSoonDays, int MilestoneApproachingDays, int ChainDepthLimit, int DefaultWeeklyCapacityHours, int CoordinationLookaheadWeeks, bool WorkingDaysEnabled,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActivityBurstMinutes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? RouteCounterEnabled = null);

    public static void Map(RouteGroupBuilder api)
    {
        // Current user, roles, capabilities and the settings the SPA needs to render dates (§25.7 GET /me).
        api.MapGet("/me", async (HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var u = await db.Users.AsNoTracking().Include(x => x.Roles).FirstAsync(x => x.Id == me.Id);
            var s = await store.Get(db);
            var pref = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id) ?? new UserSetting { UserId = me.Id };
            var a = me.Actor;
            var unitRead = s.IsFlagOn("org_structure") && await PeopleScope.LeadsUnit(db, a);
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
                    Workload = Permissions.ViewWorkload(a).Ok || unitRead,
                    Staff = Permissions.ViewStaff(a).Ok || unitRead,
                    AllStaff = Permissions.ViewAllStaff(a),
                    Admin = a.Admin,
                    Templates = Permissions.ManageTemplates(a, u.IsTemplateEditor).Ok,
                    ReadOnly = a.ReadOnly,
                    DirectReports = directReports,
                },
                Settings = new CurrentSettings(s.DateFormat, s.OrgTimeZone, clock.Today(s), s.IdleTimeoutHours, s.RestrictedProjectsEnabled, s.AllowSelfReview,
                    s.TaskDueSoonDays, s.MilestoneApproachingDays, s.ChainDepthLimit, s.DefaultWeeklyCapacityHours, s.CoordinationLookaheadWeeks, s.WorkingDaysEnabled,
                    s.IsFlagOn("small_ux") ? s.ActivityBurstMinutes : null,
                    s.IsFlagOn("route_counter") ? s.RouteCounterEnabled : null),
                Preferences = new { pref.DigestEnabled, pref.DigestTimeLocal, pref.DenseRows, pref.WeeklySummaryEnabled },
                Flags = s.Flags,
            };
        });

        // Records a successful sign-in once per session (§20.1 access events).
        api.MapPost("/me/sign-in", async (HubDb db, CurrentUser me, TimeProvider clock, HttpContext ctx) =>
        {
            // Local authentication already recorded this event before issuing its cookie.
            if (ctx.User.Identity?.AuthenticationType is AuthSetup.LocalScheme or SocialAuth.Scheme) return Results.NoContent();
            var u = await db.Users.FirstAsync(x => x.Id == me.Id);
            u.LastSignInAt = clock.GetUtcNow();
            db.LogEvent(ItemType.User, u.Id, "SignedIn", "access", key: u.Email, name: u.DisplayName);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Active people for pickers (G-11: inactive people cannot be chosen; pass active=false to include them).
        api.MapGet("/users", async (HubDb db, string? q, bool? active, string? ids, int? limit, Guid? projectId, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var settings = await store.Get(db);
            var rank = settings.IsFlagOn("picker_project_rank");
            var project = rank ? projectId : null;
            if (project is { } pid) await access.Project(pid, track: false);
            var since = clock.GetUtcNow().AddDays(-settings.PickerRecentDays);
            var team = db.ProjectMembers.Where(m => m.ProjectId == project && (m.RemovedAt == null || m.RemovedAt >= since)).Select(m => m.UserId);
            var query = db.Users.AsNoTracking();
            var idList = Http.Ids(ids);
            if (idList.Length > 0) query = query.Where(u => idList.Contains(u.Id));
            else if (active != false) query = query.Where(u => u.IsActive);
            var pattern = string.IsNullOrWhiteSpace(q) ? null : $"%{q.Trim()}%";
            if (pattern is not null) query = query.Where(u => EF.Functions.ILike(u.DisplayName, pattern) || EF.Functions.ILike(u.Email, pattern)
                || (rank && ((u.JobTitle != null && EF.Functions.ILike(u.JobTitle, pattern))
                    || db.Disciplines.Any(d => d.Id == u.HomeDisciplineId && EF.Functions.ILike(d.Name, pattern)))));
            var matches = query.Select(u => new { User = u, OnProject = project != null && team.Contains(u.Id),
                Direct = pattern == null || EF.Functions.ILike(u.DisplayName, pattern) || EF.Functions.ILike(u.Email, pattern) });
            var ordered = matches.OrderBy(row => row.User.DisplayName);
            if (rank && project != null) ordered = matches.OrderBy(row => row.User.Id == me.Id ? 0 : row.OnProject ? 1 : 2)
                .ThenByDescending(row => row.Direct).ThenBy(row => row.User.DisplayName);
            else if (rank && pattern != null) ordered = matches.OrderByDescending(row => row.Direct).ThenBy(row => row.User.DisplayName);
            return await ordered.Take(Math.Clamp(limit ?? 50, 1, 500)).Select(row => new PickerUser(row.User.Id, row.User.DisplayName, row.User.Email,
                row.User.JobTitle, row.User.OfficeId, row.User.HomeDisciplineId, row.User.IsActive, row.User.SupervisorId, project != null ? row.OnProject : null)).ToListAsync();
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
            Limits = new { RoleLabel = OpenRoleLimits.Label },
        });
    }
}
