using System.Net;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 049 request-level safety checks. These tests use an isolated PostgreSQL factory with the
/// feature flags enabled, so the shared flag-off fixture remains untouched.
public sealed class TaskPickupTests
{
    static async Task<(HubFactory Factory, TestData Data)> EnabledFactory()
    {
        var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.task_pickup", new { value = true })).EnsureSuccessStatusCode();
        (await admin.Put("/api/v1/admin/settings/flag.org_structure", new { value = false })).EnsureSuccessStatusCode();
        return (f, new TestData(f));
    }

    static Task AddProjectPeople(HubFactory f, TestData d, Guid projectId) => f.DbAsync(async db =>
    {
        foreach (var userId in new[] { d.User(TestData.Jill), d.User(TestData.Marc), d.User(TestData.Diane), d.User(TestData.Rita) })
            if (!await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId && m.RemovedAt == null))
                db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = userId, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
        return await db.SaveChangesAsync();
    });

    static Task SetUnits(HubFactory f, TestData d) => f.DbAsync(async db =>
    {
        var level = new OrgLevel { Name = "Pickup test level " + Guid.NewGuid().ToString("N"), SortOrder = (await db.Set<OrgLevel>().MaxAsync(x => (int?)x.SortOrder) ?? 0) + 1 };
        db.Set<OrgLevel>().Add(level);
        var assigneeUnit = new OrgUnit { Name = "Pickup unit A " + Guid.NewGuid().ToString("N"), LevelId = level.Id };
        var peerUnit = new OrgUnit { Name = "Pickup unit B " + Guid.NewGuid().ToString("N"), LevelId = level.Id };
        var outsideUnit = new OrgUnit { Name = "Pickup unit C " + Guid.NewGuid().ToString("N"), LevelId = level.Id };
        db.Set<OrgUnit>().AddRange(assigneeUnit, peerUnit, outsideUnit);
        await db.SaveChangesAsync();
        var users = await db.Users.Where(u => new[] { d.User(TestData.Alex), d.User(TestData.Jill), d.User(TestData.Diane) }.Contains(u.Id)).ToListAsync();
        users.Single(u => u.Id == d.User(TestData.Alex)).UnitId = assigneeUnit.Id;
        users.Single(u => u.Id == d.User(TestData.Jill)).UnitId = assigneeUnit.Id;
        users.Single(u => u.Id == d.User(TestData.Diane)).UnitId = outsideUnit.Id;
        return await db.SaveChangesAsync();
    });

    [Fact]
    [Trait("flag-off", "task_pickup")]
    public async Task Pickup_routes_are_inert_when_the_flag_is_off()
    {
        using var f = new HubFactory();
        var d = new TestData(f);
        var project = await d.Project();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var response = await f.As(TestData.Alex).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Needs cover" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync("/api/v1/pickup-offers")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync("/api/v1/pickup-offers/my-load")).StatusCode);
        var detail = await (await f.As(TestData.Alex).GetAsync($"/api/v1/tasks/{task.G("id")}")).Json();
        Assert.Null(detail["task"]?["pickupOfferOpen"]);
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Offer_leaves_task_fields_unchanged_and_pickup_closes_offer_watches_previous_assignee()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-30" });
            var id = task.G("id");
            var before = await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => new { t.Name, t.Status, t.DueDate, t.AssigneeId, t.RowVersion }).SingleAsync());
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Invalid hours", hoursRemaining = 100000m })).Json(400);
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Needs cover", keepWatching = true })).Json(201);
            var marked = await (await f.As(TestData.Alex).GetAsync($"/api/v1/tasks/{id}")).Json();
            Assert.True(marked["task"]?["pickupOfferOpen"]?.GetValue<bool>());
            var afterOffer = await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => new { t.Name, t.Status, t.DueDate, t.AssigneeId, t.RowVersion }).SingleAsync());
            Assert.Equal(before, afterOffer);
            await (await f.As(TestData.Jill).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = afterOffer.RowVersion })).Json();
            Assert.Equal(d.User(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
            Assert.True(await f.DbAsync(db => db.Watchers.AnyAsync(w => w.ItemId == id && w.UserId == d.User(TestData.Alex))));
            Assert.Equal(TaskPickupOffer.PickedUp, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == id).Select(o => o.CloseCause).SingleAsync()));
            Assert.False(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.ItemId == id && n.UserId == d.User(TestData.Jill) && n.EventType == NotificationEvents.TaskPickedUp)));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Outside_team_and_read_only_picker_are_forbidden_without_mutation()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            var version = await d.TaskVersion(task);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Diane).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version })).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version })).StatusCode);
            Assert.Equal(d.User(TestData.Alex), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
            Assert.Equal(version, await d.TaskVersion(task));
            Assert.Null(await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == id).Select(o => o.ClosedAt).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Manager_can_assign_only_an_inside_team_target()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            var version = await d.TaskVersion(task);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup-offer/assign", new { userId = d.User(TestData.Diane), rowVersion = version })).StatusCode);
            Assert.Equal(d.User(TestData.Alex), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
            var offers = await (await f.As(TestData.Jill).GetAsync("/api/v1/pickup-offers")).Json();
            Assert.Contains(offers.AsArray(), offer => offer!.G("taskId") == id);
            var detail = await (await f.As(TestData.Sam).GetAsync($"/api/v1/tasks/{id}/pickup-offer")).Json();
            Assert.True(detail["allowed"]!["assign"]!.GetValue<bool>());
            Assert.Contains(detail["candidates"]!.AsArray(), candidate => candidate!.G("id") == d.User(TestData.Jill));
            var assigned = await (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup-offer/assign", new { userId = d.User(TestData.Jill), rowVersion = version })).Json();
            Assert.Equal(d.User(TestData.Jill), assigned.G("assigneeId"));
            Assert.Equal(TaskPickupOffer.AssignedByManager, assigned.S("closeCause"));
            Assert.Equal(d.User(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Parallel_same_team_pickup_is_first_wins()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            var version = await d.TaskVersion(task);
            var results = await Task.WhenAll(
                f.As(TestData.Jill).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version }),
                f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version }));
            Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
            Assert.Equal(1, results.Count(r => r.StatusCode == HttpStatusCode.Conflict));
            var winner = await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync());
            Assert.Contains(winner!.Value, new[] { d.User(TestData.Jill), d.User(TestData.Marc) });
            Assert.Equal(1, await f.DbAsync(db => db.Set<TaskPickupOffer>().CountAsync(o => o.TaskId == id && o.ClosedAt != null)));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Completion_and_direct_reassignment_close_open_offers()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            var complete = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var completeId = complete.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{completeId}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            await d.Move(TestData.Alex, complete, TaskStatuses.Complete);
            Assert.Equal(TaskPickupOffer.TaskClosed, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == completeId).Select(o => o.CloseCause).SingleAsync()));

            var reassigned = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var reassignedId = reassigned.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{reassignedId}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{reassignedId}", new { assigneeId = d.User(TestData.Jill) }, await d.TaskVersion(reassigned))).EnsureSuccessStatusCode();
            Assert.Equal(TaskPickupOffer.ReassignedElsewhere, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == reassignedId).Select(o => o.CloseCause).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Project_completion_and_archive_close_open_offers()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var completeProject = await d.Project();
            await AddProjectPeople(f, d, completeProject.Id);
            var completeTask = await d.NewTask(completeProject.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var completeId = completeTask.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{completeId}/pickup-offer", new { reason = "Project closing" })).Json(201);
            await f.DbAsync(async db =>
            {
                db.Projects.Single(p => p.Id == completeProject.Id).Status = ProjectStatus.Complete;
                return await db.SaveChangesAsync();
            });
            Assert.Equal(TaskPickupOffer.TaskClosed, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == completeId).Select(o => o.CloseCause).SingleAsync()));

            var archiveProject = await d.Project();
            await AddProjectPeople(f, d, archiveProject.Id);
            var archiveTask = await d.NewTask(archiveProject.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var archiveId = archiveTask.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{archiveId}/pickup-offer", new { reason = "Project archived" })).Json(201);
            await f.DbAsync(async db =>
            {
                db.Projects.Single(p => p.Id == archiveProject.Id).Status = ProjectStatus.Archived;
                return await db.SaveChangesAsync();
            });
            Assert.Equal(TaskPickupOffer.TaskClosed, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == archiveId).Select(o => o.CloseCause).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    [Trait("flag-on", "org_structure")]
    public async Task Org_structure_units_override_shared_manager_fallback_for_pickup_teams()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.org_structure", new { value = true })).EnsureSuccessStatusCode();
            var project = await d.Project();
            await AddProjectPeople(f, d, project.Id);
            var unitA = Guid.NewGuid();
            var unitB = Guid.NewGuid();
            await SetUnits(f, d);
            var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Unit coverage" })).Json(201);
            var version = await d.TaskVersion(task);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Diane).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version })).StatusCode);
            Assert.Equal(d.User(TestData.Alex), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
            await (await f.As(TestData.Jill).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version })).Json();
            Assert.Equal(d.User(TestData.Jill), await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => t.AssigneeId).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Direct_manager_without_supervisor_role_cannot_use_manager_pickup_actions()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var project = await d.Project();
            await AddProjectPeople(f, d, project.Id);
            await f.DbAsync(async db =>
            {
                var sam = await db.Users.SingleAsync(u => u.Id == d.User(TestData.Sam));
                db.UserRoles.RemoveRange(db.UserRoles.Where(r => r.UserId == sam.Id && r.Role == SystemRole.Supervisor));
                return await db.SaveChangesAsync();
            });
            var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Manager cover" })).StatusCode);
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            var version = await d.TaskVersion(task);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).DeleteAsync($"/api/v1/tasks/{id}/pickup-offer")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup-offer/assign", new { userId = d.User(TestData.Jill), rowVersion = version })).StatusCode);
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Deleted_task_offer_remains_in_historical_pickup_report_for_same_team_viewer()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var project = await d.Project();
            await AddProjectPeople(f, d, project.Id);
            var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Historical coverage" })).Json(201);
            await f.DbAsync(async db =>
            {
                var row = await db.Tasks.SingleAsync(t => t.Id == id);
                row.DeletedAt = f.Clock.GetUtcNow();
                row.DeletedBy = d.User(TestData.Alex);
                return await db.SaveChangesAsync();
            });
            var report = await (await f.As(TestData.Jill).GetAsync($"/api/v1/reports/pickup-offers?projectId={project.Id}")).Json();
            Assert.Contains(report["rows"]!.AsArray(), row => row?["project"]?.GetValue<string>() == project.ProjectNumber);
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Own_load_hint_does_not_grant_workspace_workload_access()
    {
        var (f, d) = await EnabledFactory(); using (f)
        {
            var viewer = f.As(TestData.Jill);
            var hint = await (await viewer.GetAsync("/api/v1/pickup-offers/my-load?week=2026-10-26")).Json();
            Assert.NotNull(hint["band"]); Assert.NotNull(hint["capacity"]); Assert.Equal("2026-10-26", hint.S("week"));
            Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/v1/workload")).StatusCode);
            var admin = f.As(TestData.Admin);
            await (await admin.Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
            var project = await d.Project();
            await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Jill), estimatedHours = 40m, dueDate = "2026-10-26" });
            await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { visibility = "Restricted" }, d.Version(project.Id))).Json();
            var jill = d.User(TestData.Jill);
            await f.DbAsync(async db => { foreach (var member in await db.ProjectMembers.Where(m => m.ProjectId == project.Id && m.UserId == jill).ToListAsync()) member.RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
            var partial = await (await viewer.GetAsync("/api/v1/pickup-offers/my-load?week=2026-10-26")).Json();
            Assert.True(partial["partial"]!.GetValue<bool>()); Assert.Null(partial["band"]); Assert.Null(partial["hours"]);
        }
    }
}
