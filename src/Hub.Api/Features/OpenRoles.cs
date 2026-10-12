using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Packet 036, FR-ROLE-01..05: unfilled project demand before a person is named. It is filled by proposing a normal
/// resource allocation that names it (D1, AllocationEndpoints.Create); creating, editing and cancelling follow allocation
/// proposals (D2); reads use the project visibility scope (AC-ROLE-05).
public static class OpenRoleEndpoints
{
    public sealed record CancelBody(int? RowVersion, string? Reason);
    sealed record Row(Guid Id, Guid ProjectId, string RoleLabel, Guid? ProjectDisciplineId, string? DisciplineName, DateOnly FromDate,
        DateOnly ThroughDate, decimal PlannedHours, string Status, Guid? FilledAllocationId, string? Note, int RowVersion, bool CanManage);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/open-roles", List);
        api.MapPost("/projects/{projectId:guid}/open-roles", Create);
        api.MapPatch("/open-roles/{id:guid}", Edit);
        api.MapPost("/open-roles/{id:guid}/cancel", Cancel);
    }

    /// The project office's working days, with holidays always skipped, as an allocation's person calendar does.
    internal static async Task<Func<Guid, WorkCalendar>> Calendars(HubDb db, IReadOnlyCollection<Guid> projectIds)
    {
        var offices = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.OfficeId);
        var holidays = await db.Holidays.AsNoTracking().Select(h => new { h.OfficeId, h.Date }).ToListAsync();
        return pid => new WorkCalendar(holidays.Where(h => h.OfficeId == null || h.OfficeId == offices.GetValueOrDefault(pid)).Select(h => h.Date));
    }

    /// AC-ROLE-02: the role's hours spread over working days exactly as an allocation's are (last-day remainder rule).
    internal static IReadOnlyDictionary<DateOnly, decimal> Spread(OpenRole r, WorkCalendar calendar)
    {
        try { return AllocationRules.Spread(r.FromDate, r.ThroughDate, r.PlannedHours, calendar); }
        catch (ArgumentException) { return new Dictionary<DateOnly, decimal>(); } // a later holiday removed every working day
    }

    static bool CanManage(Actor actor, ProjectContext ctx, OpenRole r) =>
        Permissions.ProposeAllocation(actor, ctx).Ok && (Permissions.IsPM(actor, ctx) || r.CreatedBy == actor.Id);

    static Row ToRow(OpenRole r, string? disciplineName, bool canManage) => new(r.Id, r.ProjectId, r.RoleLabel, r.ProjectDisciplineId, disciplineName,
        r.FromDate, r.ThroughDate, r.PlannedHours, r.Status, r.FilledAllocationId, r.Note, r.RowVersion, canManage);

    /// A6, P-05: on a Complete project every change is a correction that needs a reason, as allocation proposals do.
    static string? Reason(Project p, string? reason) => p.Status == ProjectStatus.Complete ? Check.Reason(reason) : Check.Optional(reason, "reason", 2000);

    static Task<string?> DisciplineName(HubDb db, Guid? id) =>
        db.ProjectDisciplines.Where(d => d.Id == id).Select(d => d.Discipline!.Name).FirstOrDefaultAsync();

    static async Task<object> List(Guid projectId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var roles = await db.OpenRoles.AsNoTracking().Where(r => r.ProjectId == projectId).OrderBy(r => r.FromDate).ThenBy(r => r.RoleLabel)
            .Select(r => new { Role = r, DisciplineName = db.ProjectDisciplines.Where(d => d.Id == r.ProjectDisciplineId).Select(d => d.Discipline!.Name).FirstOrDefault() })
            .ToListAsync();
        return roles.Select(x => ToRow(x.Role, x.DisciplineName, CanManage(access.Actor, ctx, x.Role)));
    }

    static async Task Apply(HubDb db, Guid projectId, OpenRole r, Patch p, bool create)
    {
        if (create || p.Has("roleLabel")) r.RoleLabel = Check.Required(p.Str("roleLabel"), "roleLabel", 80);
        if (create || p.Has("projectDisciplineId"))
        {
            r.ProjectDisciplineId = p.Id("projectDisciplineId");
            if (r.ProjectDisciplineId is { } discipline) await Coordination.Discipline(db, projectId, discipline);
        }
        if (create || p.Has("fromDate")) r.FromDate = p.Date("fromDate") ?? default;
        if (create || p.Has("throughDate")) r.ThroughDate = p.Date("throughDate") ?? default;
        if (create || p.Has("plannedHours")) r.PlannedHours = p.Dec("plannedHours") ?? 0;
        if (create || p.Has("note")) r.Note = Check.Optional(p.Str("note"), "note", 2000);
        Check.That(r.FromDate != default && r.ThroughDate >= r.FromDate && r.ThroughDate.DayNumber - r.FromDate.DayNumber <= 366, "throughDate", "openRole.dates");
        Check.That(r.PlannedHours is > 0 and <= 10000, "plannedHours", "error.positive");
        var calendar = (await Calendars(db, [projectId]))(projectId);
        Check.That(calendar.WorkingDays(r.FromDate, r.ThroughDate).Any(), "throughDate", "openRole.dates");
    }

    static async Task<IResult> Create(Guid projectId, JsonElement body, Access access, HubDb db)
    {
        var (project, ctx) = await access.Project(projectId, false);
        Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
        var role = new OpenRole { ProjectId = project.Id };
        var p = new Patch(body);
        var reason = Reason(project, p.Str("reason"));
        await Apply(db, project.Id, role, p, true);
        db.OpenRoles.Add(role);
        db.Audit.Note(role, reason: reason);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/open-roles/{role.Id}", ToRow(role, await DisciplineName(db, role.ProjectDisciplineId), true));
    }

    /// Only an Open role changes; a Filled role is changed by declining or cancelling its allocation first.
    static async Task<(OpenRole Role, Project Project)> Writable(Guid id, Access access, HubDb db)
    {
        var role = await db.OpenRoles.FirstOrDefaultAsync(r => r.Id == id) ?? throw ApiException.NotFound();
        var (project, ctx) = await access.Project(role.ProjectId, false);
        Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
        Access.Demand(Permissions.IsPM(access.Actor, ctx) || role.CreatedBy == access.Actor.Id ? Allow.Yes : Allow.No("perm.owner"));
        return (role, project);
    }

    static async Task<IResult> Edit(Guid id, JsonElement body, HttpContext http, Access access, HubDb db)
    {
        var p = new Patch(body);
        var (role, project) = await Writable(id, access, db);
        await Http.CheckVersion(db, http, role, p.RowVersion);
        Check.That(role.Status == OpenRoleStatus.Open, "status", "openRole.not_open");
        var reason = Reason(project, p.Str("reason"));
        await Apply(db, role.ProjectId, role, p, false);
        db.Audit.Note(role, reason: reason);
        await db.SaveChangesAsync();
        return Results.Ok(ToRow(role, await DisciplineName(db, role.ProjectDisciplineId), true));
    }

    static async Task<IResult> Cancel(Guid id, CancelBody body, HttpContext http, Access access, HubDb db)
    {
        var (role, project) = await Writable(id, access, db);
        await Http.CheckVersion(db, http, role, body.RowVersion);
        Check.That(role.Status == OpenRoleStatus.Open, "status", "openRole.not_open");
        var reason = Reason(project, body.Reason);
        role.Status = OpenRoleStatus.Cancelled;
        db.Audit.Note(role, reason: reason);
        await db.SaveChangesAsync();
        return Results.Ok(ToRow(role, await DisciplineName(db, role.ProjectDisciplineId), true));
    }
}
