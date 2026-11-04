using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class MeEndpoints
{
    public sealed record PickerUser(Guid Id, string? DisplayName, string? Email, string? JobTitle, Guid? OfficeId,
        Guid? HomeDisciplineId, bool IsActive, Guid? SupervisorId, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? OnProject = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PersonFacts.Notices? PersonNotices = null);
    public sealed record CurrentSettings(string? DateFormat, string? OrgTimeZone, DateOnly Today, int IdleTimeoutHours, bool RestrictedProjectsEnabled, bool AllowSelfReview,
        int TaskDueSoonDays, int MilestoneApproachingDays, int ChainDepthLimit, int DefaultWeeklyCapacityHours, int CoordinationLookaheadWeeks, bool WorkingDaysEnabled,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ActivityBurstMinutes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? RouteCounterEnabled = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? AutosaveDelayMs = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? DraftKeepDays = null);
    public sealed record MeRole(string Role, string Source);
    public sealed record MeCapabilities(bool CreateProject, bool CreateTask, bool Portfolio, bool Workload, bool Staff, bool AllStaff, bool Admin, bool Templates, bool ReadOnly, int DirectReports);
    public sealed record MePreferences(bool DigestEnabled, string? DigestTimeLocal, bool DenseRows, bool WeeklySummaryEnabled);
    public sealed record MeView(Guid Id, string DisplayName, string Email, string? JobTitle, Guid? OfficeId, Guid? SupervisorId, bool IsTemplateEditor, IReadOnlyList<MeRole> Roles,
        IReadOnlyList<string> SystemRoles, MeCapabilities Capabilities, CurrentSettings Settings, MePreferences Preferences, IReadOnlyDictionary<string, bool> Flags);

    public sealed record ReferenceDiscipline(Guid Id, string Name, string Code, string Colour, bool IsActive, int SortOrder);
    public sealed record ReferenceClient(Guid Id, string Name, string? ShortName, bool IsActive);
    public sealed record ReferenceOffice(Guid Id, string Name, string Code, string TimeZone, bool IsActive);
    public sealed record ReferenceDeliverableType(Guid Id, string Name, Guid? DefaultDisciplineId, bool IsActive);
    public sealed record ReferencePhase(Guid Id, string Name, bool IsActive, int SortOrder);
    public sealed record ReferenceProjectType(Guid Id, string Name, bool IsActive);
    public sealed record ReferenceLimits(int RoleLabel);
    public sealed record ReferenceIssuePurpose(Guid Id, string Name, bool IsActive, int SortOrder);
    public sealed record ReferenceIssueReturnCode(Guid Id, string Code, string Name, bool IsActive, int SortOrder);
    public sealed record ReferenceView(IReadOnlyList<ReferenceDiscipline> Disciplines, IReadOnlyList<ReferenceClient> Clients, IReadOnlyList<ReferenceOffice> Offices,
        IReadOnlyList<ReferenceDeliverableType> DeliverableTypes, IReadOnlyList<ReferencePhase> Phases, IReadOnlyList<ReferenceProjectType> ProjectTypes, ReferenceLimits Limits,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReferenceIssuePurpose>? IssuePurposes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<ReferenceIssueReturnCode>? IssueReturnCodes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<IssueRootCauseChoice>? IssueRootCauses = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DateReasonChoice>? DateReasons = null);

    /// One person with the facts the picker orders by. Built with an initializer so the ordering can still be translated to SQL.
    sealed record PickerMatch { public required AppUser User { get; init; } public bool OnProject { get; init; } public bool Direct { get; init; } }

    public static void Map(RouteGroupBuilder api)
    {
        // Current user, roles, capabilities and the settings the SPA needs to render dates (§25.7 GET /me).
        api.MapGet("/me", async Task<Ok<MeView>> (HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var u = await db.Users.AsNoTracking().Include(x => x.Roles).FirstAsync(x => x.Id == me.Id);
            var s = await store.Get(db);
            var pref = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id) ?? new UserSetting { UserId = me.Id };
            var a = me.Actor;
            var unitRead = await PeopleScope.ReadsAsUnitLeader(db, s, a);
            var directReports = await db.Users.CountAsync(x => x.SupervisorId == me.Id && x.IsActive);
            // New Item offers Task only to someone who may create one somewhere (§36.1); each project still checks it.
            var uid = me.Id;
            var createTask = !a.ReadOnly && await db.Projects.AnyAsync(p => (p.Status == ProjectStatus.Setup || p.Status == ProjectStatus.Active || p.Status == ProjectStatus.OnHold)
                && (p.ProjectManagerId == uid || db.ProjectDisciplines.Any(d => d.ProjectId == p.Id && d.LeadUserId == uid)
                    || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null
                        && (m.Roles.Contains(ProjectRole.PM) || (m.Roles.Contains(ProjectRole.TeamMember) && m.PrimaryDisciplineId != null)))));
            return TypedResults.Ok(new MeView(u.Id, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.SupervisorId, u.IsTemplateEditor,
                [.. u.Roles.Select(r => new MeRole(r.Role, r.Source)).OrderBy(r => r.Role)],
                [.. me.Roles.Order()],
                new MeCapabilities(Permissions.CreateProject(a).Ok, createTask, Permissions.ViewPortfolio(a).Ok, Permissions.ViewWorkload(a).Ok || unitRead,
                    Permissions.ViewStaff(a).Ok || unitRead, Permissions.ViewAllStaff(a), a.Admin, Permissions.ManageTemplates(a, u.IsTemplateEditor).Ok, a.ReadOnly, directReports),
                new CurrentSettings(s.DateFormat, s.OrgTimeZone, clock.Today(s), s.IdleTimeoutHours, s.RestrictedProjectsEnabled, s.AllowSelfReview,
                    s.TaskDueSoonDays, s.MilestoneApproachingDays, s.ChainDepthLimit, s.DefaultWeeklyCapacityHours, s.CoordinationLookaheadWeeks, s.WorkingDaysEnabled,
                    s.IsFlagOn("small_ux") ? s.ActivityBurstMinutes : null,
                    s.IsFlagOn("route_counter") ? s.RouteCounterEnabled : null,
                    s.IsFlagOn("data_entry_safety") ? s.AutosaveDelayMs : null,
                    s.IsFlagOn("data_entry_safety") ? s.DraftKeepDays : null),
                new MePreferences(pref.DigestEnabled, pref.DigestTimeLocal, pref.DenseRows, pref.WeeklySummaryEnabled),
                s.Flags));
        });

        // Records a successful sign-in once per session (§20.1 access events).
        api.MapPost("/me/sign-in", async Task<NoContent> (HubDb db, CurrentUser me, TimeProvider clock, HttpContext ctx) =>
        {
            // Local authentication already recorded this event before issuing its cookie.
            if (ctx.User.Identity?.AuthenticationType is AuthSetup.LocalScheme or SocialAuth.Scheme) return TypedResults.NoContent();
            var u = await db.Users.FirstAsync(x => x.Id == me.Id);
            u.LastSignInAt = clock.GetUtcNow();
            db.LogEvent(ItemType.User, u.Id, "SignedIn", "access", key: u.Email, name: u.DisplayName);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });

        // Active people for pickers (G-11: inactive people cannot be chosen; pass active=false to include them).
        api.MapGet("/users", async (HubDb db, string? q, bool? active, string? ids, int? limit, Guid? projectId, Access access, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var settings = await store.Get(db);
            await using var snapshot = settings.IsFlagOn("capacity_screens") ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
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
            var matches = query.Select(u => new PickerMatch { User = u, OnProject = project != null && team.Contains(u.Id),
                Direct = pattern == null || EF.Functions.ILike(u.DisplayName, pattern) || EF.Functions.ILike(u.Email, pattern) });
            var ordered = matches.OrderBy(row => row.User.DisplayName);
            if (rank && project != null) ordered = matches.OrderBy(row => row.User.Id == me.Id ? 0 : row.OnProject ? 1 : 2)
                .ThenByDescending(row => row.Direct).ThenBy(row => row.User.DisplayName);
            else if (rank && pattern != null) ordered = matches.OrderByDescending(row => row.Direct).ThenBy(row => row.User.DisplayName);
            var rows = await ordered.Take(Math.Clamp(limit ?? 50, 1, 500)).Select(row => new PickerUser(row.User.Id, row.User.DisplayName, row.User.Email,
                row.User.JobTitle, row.User.OfficeId, row.User.HomeDisciplineId, row.User.IsActive, row.User.SupervisorId, project != null ? row.OnProject : null, null)).ToListAsync();
            var notices = await PersonFacts.ReadNotices(db, rows.Select(row => row.Id).ToArray(), settings, clock);
            return notices.Count > 0 ? rows.Select(row => row with { PersonNotices = notices.GetValueOrDefault(row.Id) }).ToList() : rows;
        });

        // Active reference data for every picker; inactive entries only appear on items that already use them.
        api.MapGet("/reference", async Task<Ok<ReferenceView>> (HubDb db, SettingsStore store) =>
        {
            var register = (await store.Get(db)).IsFlagOn("register_screens");
            return TypedResults.Ok(new ReferenceView(
            await db.Disciplines.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceDiscipline(x.Id, x.Name, x.Code, x.Colour, x.IsActive, x.SortOrder)).ToListAsync(),
            await db.Clients.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceClient(x.Id, x.Name, x.ShortName, x.IsActive)).ToListAsync(),
            await db.Offices.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceOffice(x.Id, x.Name, x.Code, x.TimeZone, x.IsActive)).ToListAsync(),
            await db.DeliverableTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceDeliverableType(x.Id, x.Name, x.DefaultDisciplineId, x.IsActive)).ToListAsync(),
            await db.Phases.AsNoTracking().OrderBy(x => x.SortOrder).Select(x => new ReferencePhase(x.Id, x.Name, x.IsActive, x.SortOrder)).ToListAsync(),
            await db.ProjectTypes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceProjectType(x.Id, x.Name, x.IsActive)).ToListAsync(),
            new ReferenceLimits(OpenRoleLimits.Label),
            register ? await db.IssuePurposes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceIssuePurpose(x.Id, x.Name, x.IsActive, x.SortOrder)).ToListAsync() : null,
            register ? await db.IssueReturnCodes.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new ReferenceIssueReturnCode(x.Id, x.Code, x.Name, x.IsActive, x.SortOrder)).ToListAsync() : null,
            register ? await db.IssueRootCauseCategories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(x => new IssueRootCauseChoice(x.Id, x.Name, x.IsActive, x.SortOrder)).ToListAsync() : null,
            register ? await db.DateReasonCategories.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id).Select(x => new DateReasonChoice(x.Id, x.Name, x.IsActive, x.SortOrder)).ToListAsync() : null));
        });
    }
}
