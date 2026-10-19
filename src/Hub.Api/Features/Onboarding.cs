using Microsoft.AspNetCore.Http.HttpResults;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Personal getting-started card (§51.2). Completion is deliberately scoped to the
/// signed-in person; no other user's records are included in the response.
public static class OnboardingEndpoints
{
    public sealed record Step(string Key, string Label, string? Route, bool Done);
    public sealed record Group(string Role, IReadOnlyList<Step> Steps);
    public sealed record Response(bool Hidden, bool Done, int Total, int Completed, IReadOnlyList<Group> Groups);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/onboarding", Read);
        api.MapPost("/me/onboarding/hide", async (HubDb db, CurrentUser me, TimeProvider clock, SettingsStore store) =>
        {
            if (!(await store.Get(db)).IsFlagOn("onboarding_card")) throw ApiException.NotFound();
            var setting = await UserPreferences.GetOrCreate(db, me.Id);
            setting.OnboardingHiddenAt = clock.GetUtcNow();
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });
        api.MapPost("/me/onboarding/show", async (HubDb db, CurrentUser me, SettingsStore settingsStore) =>
        {
            if (!(await settingsStore.Get(db)).IsFlagOn("onboarding_card")) throw ApiException.NotFound();
            var setting = await UserPreferences.GetOrCreate(db, me.Id);
            setting.OnboardingHiddenAt = null;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });
    }

    static async Task<Results<NotFound, Ok<Response>>> Read(HubDb db, CurrentUser me, Access access, SettingsStore settings)
    {
        var release = await settings.Get(db);
        if (!release.IsFlagOn("onboarding_card")) return TypedResults.NotFound();
        var s = await db.UserSettings.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == me.Id);
        if (!me.IsActive || me.IsReadOnly) return TypedResults.Ok(new Response(true, false, 0, 0, []));
        var visible = access.VisibleProjects().Select(p => p.Id);
        var managed = db.Projects.AsNoTracking().Where(p => visible.Contains(p.Id) && p.ProjectManagerId == me.Id);
        var directReports = db.Users.AsNoTracking().Where(u => u.SupervisorId == me.Id && u.IsActive).Select(u => u.Id);
        var facts = new OnboardingFacts(CanPlanSelf: Permissions.ViewWorkload(me.Actor).Ok,
            ChangedTaskStatus: await db.ActivityLog.AsNoTracking().AnyAsync(a => a.ActorUserId == me.Id && a.ItemType == ItemType.Task && a.Categories.Contains("status") && a.ProjectId != null && visible.Contains(a.ProjectId.Value)),
            Commented: await db.Comments.AsNoTracking().AnyAsync(c => c.AuthorId == me.Id && c.DeletedAt == null && visible.Contains(c.ProjectId)),
            PlannedSelf: await db.PlanningEntries.AsNoTracking().AnyAsync(p => p.PersonId == me.Id && p.CreatedBy == me.Id && p.DeletedAt == null),
            CreatedProject: await managed.AnyAsync(),
            AddedMilestone: await db.Milestones.AsNoTracking().AnyAsync(m => managed.Select(p => p.Id).Contains(m.ProjectId)),
            AssignedTask: await db.Tasks.AsNoTracking().AnyAsync(t => managed.Select(p => p.Id).Contains(t.ProjectId) && t.AssigneeId != null),
            ActivatedProject: await managed.AnyAsync(p => p.Status != ProjectStatus.Setup),
            AddedDeliverable: await db.Deliverables.AsNoTracking().AnyAsync(d => d.CreatedBy == me.Id && visible.Contains(d.ProjectId)),
            ProposedAllocationOrRole: await db.Allocations.AsNoTracking().AnyAsync(a => a.CreatedBy == me.Id && visible.Contains(a.ProjectId)) || await db.OpenRoles.AsNoTracking().AnyAsync(a => a.CreatedBy == me.Id && visible.Contains(a.ProjectId)),
            PlannedDirectReport: await db.PlanningEntries.AsNoTracking().AnyAsync(p => directReports.Contains(p.PersonId) && p.CreatedBy == me.Id && p.Visibility != "Draft"),
            ConfirmedAllocation: await db.Allocations.AsNoTracking().AnyAsync(a => a.ConfirmedBy == me.Id),
            RecordedAway: await db.ActivityLog.AsNoTracking().AnyAsync(a => a.ActorUserId == me.Id && a.ItemType == "PersonAvailabilityOverride" && a.Action == "Created"),
            StarredProject: await db.ProjectStars.AsNoTracking().AnyAsync(s => s.UserId == me.Id && visible.Contains(s.ProjectId)),
            FollowedProject: await db.Follows.AsNoTracking().AnyAsync(f => f.UserId == me.Id && f.Source == FollowSource.Manual && visible.Contains(f.ProjectId)),
            SetSupervisors: me.IsAdmin && await db.Users.AsNoTracking().AnyAsync(u => u.IsActive && u.SupervisorId != null),
            ImportedLeave: me.IsAdmin && await db.LeaveImportStates.AsNoTracking().AnyAsync(),
            ImportedProjects: me.IsAdmin && await db.ActivityLog.AsNoTracking().AnyAsync(a => a.ItemType == "ProjectImport" && a.Action == "Imported"));
        var roles = me.Roles.ToHashSet();
        if (!await directReports.AnyAsync()) roles.Remove(SystemRole.Supervisor);
        if (me.IsAdmin) roles.Remove(SystemRole.ReadOnly);
        if (await db.ProjectDisciplines.AsNoTracking().AnyAsync(d => d.LeadUserId == me.Id && d.IsActive && visible.Contains(d.ProjectId))) roles.Add("DisciplineLead");
        var groups = Onboarding.Groups(roles, facts).Select(g => new Group(g.Role, g.Steps.Select(step => new Step(step.Key, Text.Get($"onboarding.{step.Key}"), step.Route, step.Done)).ToList())).ToList();
        var all = groups.SelectMany(g => g.Steps).ToList();
        return TypedResults.Ok(new Response(s?.OnboardingHiddenAt is not null, all.Count > 0 && all.All(x => x.Done), all.Count, all.Count(x => x.Done), groups));
    }
}
