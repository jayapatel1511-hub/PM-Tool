using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S43: one authorised person's facts and only the current project memberships the actor can see.
public static class PersonPanelEndpoints
{
    public sealed record PersonProjectMembership(Guid MemberId, Guid ProjectId, string ProjectNumber, string ProjectName, string ProjectStatus,
        IReadOnlyList<string> Roles, TeamScreenReads.Reference? PrimaryDiscipline, IReadOnlyList<TeamScreenReads.Reference> LeadOf, string? ResponsibilityNote);
    public sealed record PersonProjectPage(IReadOnlyList<PersonProjectMembership> Items, int Page, int PageSize, int Total);
    public sealed record PersonPanelRead(Guid Id, string DisplayName, string? JobTitle, bool IsActive, TeamScreenReads.Organisation OrganisationFacts,
        DateOnly Today, DateOnly Week, DateTimeOffset AsOf, PersonProjectPage Projects,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PersonFacts.Facts? PersonFacts = null);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/people/{userId:guid}/panel", Read).RequireFlag("people_screens");
    }

    static async Task<bool> InStaffScope(Guid userId, string? scope, Access access, HubDb db, SettingsStore store, OrgSettings settings)
    {
        if (scope != null)
        {
            var unitScope = scope.StartsWith("unit:", StringComparison.Ordinal);
            Check.That(scope is "direct" or "all" or "reports" || unitScope && Guid.TryParse(scope[5..], out var id) && id != Guid.Empty, "staffScope", "error.invalid");
            Check.That(scope != "reports" || settings.IsFlagOn("org_structure") && settings.IncludeIndirectReports, "staffScope", "error.invalid");
            Check.That(!unitScope || settings.IsFlagOn("org_structure"), "staffScope", "error.invalid");
        }
        // A project-visible name does not confer Staff authority. Do not load a directory as a fallback.
        var permitted = Permissions.ViewStaff(access.Actor).Ok || settings.IsFlagOn("org_structure") && await PeopleScope.LeadsUnit(db, access.Actor);
        if (!permitted) return false;
        var (people, _) = await MyWorkEndpoints.StaffScope(db, access, scope, showInactive: true, store, settings);
        return people.Any(person => person.Id == userId);
    }

    static async Task<Ok<PersonPanelRead>> Read(Guid userId, string? staffScope, int? projectsPage, int? projectsPageSize,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db); Http.RequireFlag(settings, "people_screens");
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var asOf = clock.GetUtcNow(); var today = Clock.LocalDate(asOf, settings);
        var week = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var visible = access.VisibleProjects().AsNoTracking();
        var memberships = from member in db.ProjectMembers.AsNoTracking()
            join project in visible on member.ProjectId equals project.Id
            where member.UserId == userId && member.RemovedAt == null
            select new PanelMembership { Member = member, Project = project };
        var authorised = staffScope != null
            ? await InStaffScope(userId, staffScope, access, db, store, settings)
            : userId == access.Me.Id || access.Actor.Admin || await memberships.AnyAsync() || await InStaffScope(userId, null, access, db, store, settings);
        if (!authorised) throw ApiException.NotFound();
        var user = await db.Users.AsNoTracking().Where(person => person.Id == userId)
            .Select(person => new PanelPerson(person.Id, person.DisplayName, person.JobTitle, person.IsActive)).SingleOrDefaultAsync() ?? throw ApiException.NotFound();
        var page = Math.Max(1, projectsPage ?? 1); var size = Math.Clamp(projectsPageSize ?? 25, 1, 100);
        var total = await memberships.CountAsync(); var skip = ((long)page - 1) * size;
        var rows = await memberships.OrderBy(row => row.Project.ProjectNumber).ThenBy(row => row.Project.Id)
            .Skip((int)Math.Min(skip, int.MaxValue)).Take(size).ToArrayAsync();
        var projectIds = rows.Select(row => row.Project.Id).ToArray();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(discipline => projectIds.Contains(discipline.ProjectId))
            .Select(discipline => new PanelDiscipline(discipline.Id, discipline.ProjectId, discipline.Discipline!.Name, discipline.LeadUserId)).ToArrayAsync();
        var projects = rows.Select(row => new PersonProjectMembership(row.Member.Id, row.Project.Id, row.Project.ProjectNumber, row.Project.Name, row.Project.Status,
            row.Member.Roles, disciplines.FirstOrDefault(discipline => discipline.Id == row.Member.PrimaryDisciplineId && discipline.ProjectId == row.Project.Id) is { } primary
                ? new TeamScreenReads.Reference(primary.Id, primary.Name) : null,
            disciplines.Where(discipline => discipline.ProjectId == row.Project.Id && discipline.LeadUserId == userId)
                .OrderBy(discipline => discipline.Name).ThenBy(discipline => discipline.Id).Select(discipline => new TeamScreenReads.Reference(discipline.Id, discipline.Name)).ToArray(), row.Member.ResponsibilityNote)).ToArray();
        var organisation = await TeamScreenReads.ReadOrganisation(db, [userId], settings);
        var facts = await PersonFacts.Read(db, [userId], settings, today);
        return TypedResults.Ok(new PersonPanelRead(user.Id, user.DisplayName, user.JobTitle, user.IsActive, organisation[userId], today, week, asOf,
            new PersonProjectPage(projects, page, size, total), facts.GetValueOrDefault(userId)));
    }
}

// Composed further (Any/Count/OrderBy/Skip), so EF needs a member-init projection.
file sealed class PanelMembership { public ProjectMember Member { get; init; } = null!; public Project Project { get; init; } = null!; }
file sealed record PanelPerson(Guid Id, string DisplayName, string? JobTitle, bool IsActive);
file sealed record PanelDiscipline(Guid Id, Guid ProjectId, string Name, Guid? LeadUserId);
