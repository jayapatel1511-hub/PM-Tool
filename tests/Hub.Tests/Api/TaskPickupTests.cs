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
            var teamRow = offers.AsArray().Single(offer => offer!.G("taskId") == id)!;
            Assert.True(teamRow["canPickUp"]!.GetValue<bool>());
            // FR-PKP-04 / AC-PKP-09: the direct manager is outside the team but sees the offer to assign it, not to pick it up.
            var managerRow = (await (await f.As(TestData.Sam).GetAsync("/api/v1/pickup-offers")).Json()).AsArray().Single(offer => offer!.G("taskId") == id)!;
            Assert.False(managerRow["canPickUp"]!.GetValue<bool>());
            Assert.True(managerRow["canAssign"]!.GetValue<bool>());
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = version })).StatusCode);
            Assert.DoesNotContain((await (await f.As(TestData.Diane).GetAsync("/api/v1/pickup-offers")).Json()).AsArray(), offer => offer!.G("taskId") == id);
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
    public async Task Pickup_report_range_uses_organisation_days()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "America/Halifax" })).EnsureSuccessStatusCode();
            f.Clock.Now = new DateTimeOffset(2026, 9, 15, 2, 30, 0, TimeSpan.Zero); // 23:30 on 14 September in Halifax
            var project = await d.Project(); await AddProjectPeople(f, d, project.Id);
            var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Evening offer" })).Json(201);
            async Task<int> Rows(string range) =>
                (await (await f.As(TestData.Jill).GetAsync($"/api/v1/reports/pickup-offers?projectId={project.Id}{range}")).Json())["rows"]!.AsArray().Count;
            Assert.Equal(1, await Rows("")); // the default range ends today, 14 September in Halifax
            Assert.Equal(0, await Rows("&from=2026-09-15&to=2026-09-15"));
        }
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public async Task Pickup_report_follows_the_offer_assignee_not_the_current_one()
    {
        var (f, d) = await EnabledFactory();
        using (f)
        {
            var picked = await d.Project(); await AddProjectPeople(f, d, picked.Id);
            var pickedTask = await d.NewTask(picked.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{pickedTask.G("id")}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            await (await f.As(TestData.Jill).Post($"/api/v1/tasks/{pickedTask.G("id")}/pickup", new { rowVersion = await d.TaskVersion(pickedTask) })).Json();
            var moved = await d.Project(); await AddProjectPeople(f, d, moved.Id);
            var movedTask = await d.NewTask(moved.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{movedTask.G("id")}/pickup-offer", new { reason = "Needs cover" })).Json(201);
            (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{movedTask.G("id")}", new { assigneeId = d.User(TestData.Diane) }, await d.TaskVersion(movedTask))).EnsureSuccessStatusCode();
            async Task<int> Rows(string who, Guid projectId) =>
                (await (await f.As(who).GetAsync($"/api/v1/reports/pickup-offers?projectId={projectId}")).Json())["rows"]!.AsArray().Count;
            Assert.Equal(1, await Rows(TestData.Jill, picked.Id)); // the picker is in the offer's team
            Assert.Equal(1, await Rows(TestData.Jill, moved.Id)); // the offer's team keeps it after a reassignment outside the team
            Assert.Equal(0, await Rows(TestData.Omar, moved.Id)); // the new assignee's team never saw the offer
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
    static async Task AssertPickupRecipients(HubFactory f, TestData d, Guid taskId, string eventType, params string[] people)
    {
        var expected = people.Select(d.User).Order().ToArray();
        var actual = await f.DbAsync(db => db.Notifications.Where(n => n.ItemId == taskId && n.EventType == eventType)
            .OrderBy(n => n.UserId).Select(n => new { n.UserId, n.Count }).ToListAsync());
        Assert.Equal(expected, actual.Select(n => n.UserId).ToArray());
        Assert.All(actual, n => Assert.Equal(1, n.Count));
    }

    [Fact]
    public async Task Pickup_events_persist_exact_recipient_sets_with_role_overlap_and_actor_exclusion()
    {
        var (f, d) = await EnabledFactory(); using (f)
        {
            var p = await d.Project();
            await AddProjectPeople(f, d, p.Id);
            async Task<System.Text.Json.Nodes.JsonNode> Offered(string actor)
            {
                var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
                await (await f.As(actor).Post($"/api/v1/tasks/{task.G("id")}/pickup-offer", new { reason = "Synthetic recipient matrix" })).Json(201);
                return task;
            }
            var own = await Offered(TestData.Alex);
            // Marc is both a teammate and the lead: exactly one persisted notice, not a collapsed duplicate.
            await AssertPickupRecipients(f, d, own.G("id"), NotificationEvents.TaskPickupOffered, TestData.Jill, TestData.Marc, TestData.Sam, TestData.Pm);
            var onBehalf = await Offered(TestData.Sam);
            await AssertPickupRecipients(f, d, onBehalf.G("id"), NotificationEvents.TaskPickupOffered, TestData.Alex, TestData.Jill, TestData.Marc, TestData.Pm);
            await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{onBehalf.G("id")}/pickup", new { rowVersion = await d.TaskVersion(onBehalf) })).Json();
            await AssertPickupRecipients(f, d, onBehalf.G("id"), NotificationEvents.TaskPickedUp, TestData.Alex, TestData.Sam, TestData.Pm);
            var assigned = await Offered(TestData.Pm);
            await (await f.As(TestData.Sam).Post($"/api/v1/tasks/{assigned.G("id")}/pickup-offer/assign", new { userId = d.User(TestData.Jill), rowVersion = await d.TaskVersion(assigned) })).Json();
            await AssertPickupRecipients(f, d, assigned.G("id"), NotificationEvents.TaskPickupAssigned, TestData.Jill, TestData.Alex, TestData.Pm);
            await (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{own.G("id")}/pickup-offer")).Json(204);
            await AssertPickupRecipients(f, d, own.G("id"), NotificationEvents.TaskPickupClosed, TestData.Alex);
            var self = await Offered(TestData.Alex);
            await (await f.As(TestData.Alex).DeleteAsync($"/api/v1/tasks/{self.G("id")}/pickup-offer")).Json(204);
            await AssertPickupRecipients(f, d, self.G("id"), NotificationEvents.TaskPickupClosed);
        }
    }

    [Theory]
    [InlineData("offer")] [InlineData("pickup")] [InlineData("assign")] [InlineData("withdraw")]
    public async Task Pickup_command_notifications_apply_recipient_filters(string command)
    {
        foreach (var state in new[] { "inactive", "preference", "removed", "muted" })
        {
            var (f, d) = await EnabledFactory(); using (f)
            {
                await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
                var p = await d.Project();
                await AddProjectPeople(f, d, p.Id);
                await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = d.User(TestData.Sam), Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now }); return await db.SaveChangesAsync(); });
                var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
                var id = task.G("id");
                var recipient = command switch { "offer" => TestData.Jill, "pickup" => TestData.Sam, "assign" => TestData.Marc, _ => TestData.Alex };
                var eventType = command switch { "offer" => NotificationEvents.TaskPickupOffered, "pickup" => NotificationEvents.TaskPickedUp, "assign" => NotificationEvents.TaskPickupAssigned, _ => NotificationEvents.TaskPickupClosed };
                if (command != "offer")
                    await (await f.As(recipient).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Synthetic recipient filters" })).Json(201);
                await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).Json();
                var recipientId = d.User(recipient);
                await f.DbAsync(async db =>
                {
                    if (state == "inactive") (await db.Users.SingleAsync(u => u.Id == recipientId)).IsActive = false;
                    if (state == "preference") db.NotificationPreferences.Add(new NotificationPreference { UserId = recipientId, EventType = eventType, InApp = false, Email = false });
                    if (state == "removed") foreach (var member in await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == recipientId).ToListAsync()) member.RemovedAt = f.Clock.Now;
                    if (state == "muted")
                    {
                        var follow = await db.Follows.SingleOrDefaultAsync(x => x.ProjectId == p.Id && x.UserId == recipientId);
                        if (follow is null) db.Follows.Add(new ProjectFollow { ProjectId = p.Id, UserId = recipientId, Level = FollowLevel.Muted });
                        else follow.Level = FollowLevel.Muted;
                    }
                    return await db.SaveChangesAsync();
                });
                if (command == "offer")
                    await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Synthetic recipient filters" })).Json(201);
                else if (command == "pickup")
                    await (await f.As(TestData.Jill).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = await d.TaskVersion(task) })).Json();
                else if (command == "assign")
                    await (await f.As(TestData.Sam).Post($"/api/v1/tasks/{id}/pickup-offer/assign", new { userId = d.User(TestData.Jill), rowVersion = await d.TaskVersion(task) })).Json();
                else await (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{id}/pickup-offer")).Json(204);
                var expected = command switch
                {
                    "offer" => new[] { TestData.Marc, TestData.Sam, TestData.Pm },
                    "pickup" => new[] { TestData.Alex, TestData.Marc, TestData.Pm },
                    // A muted offerer is not the person the task is assigned to, so the mute applies (§12.18).
                    "assign" => new[] { TestData.Alex, TestData.Jill },
                    _ => Array.Empty<string>(),
                };
                await AssertPickupRecipients(f, d, id, eventType, expected);
            }
        }
    }

    [Theory]
    [InlineData("normal", false)] [InlineData("inactive", false)] [InlineData("preference", false)]
    [InlineData("removed", false)] [InlineData("muted", false)] [InlineData("self", false)]
    [InlineData("normal", true)] [InlineData("inactive", true)] [InlineData("preference", true)]
    [InlineData("removed", true)] [InlineData("muted", true)]
    public async Task Automatic_pickup_closure_obeys_current_recipient_access_and_preferences(string recipientState, bool projectClosure)
    {
        var (f, d) = await EnabledFactory(); using (f)
        {
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
            var p = await d.Project();
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id"); var alex = d.User(TestData.Alex);
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Synthetic closure matrix" })).Json(201);
            await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).Json();
            await f.DbAsync(async db =>
            {
                if (recipientState == "inactive") (await db.Users.SingleAsync(u => u.Id == alex)).IsActive = false;
                if (recipientState == "preference") db.NotificationPreferences.Add(new NotificationPreference { UserId = alex, EventType = NotificationEvents.TaskPickupClosed, InApp = false, Email = false });
                if (recipientState == "removed") foreach (var m in await db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == alex).ToListAsync()) m.RemovedAt = f.Clock.Now;
                if (recipientState == "muted")
                {
                    var follow = await db.Follows.SingleOrDefaultAsync(x => x.ProjectId == p.Id && x.UserId == alex);
                    if (follow is null) db.Follows.Add(new ProjectFollow { ProjectId = p.Id, UserId = alex, Level = FollowLevel.Muted });
                    else follow.Level = FollowLevel.Muted;
                }
                return await db.SaveChangesAsync();
            });
            if (projectClosure)
                await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
            else await d.Move(recipientState == "self" ? TestData.Alex : TestData.Pm, task, TaskStatuses.Complete);
            await AssertPickupRecipients(f, d, id, NotificationEvents.TaskPickupClosed, recipientState == "normal" ? [TestData.Alex] : []);
            await f.DbAsync(db => db.SaveChangesAsync());
            await AssertPickupRecipients(f, d, id, NotificationEvents.TaskPickupClosed, recipientState == "normal" ? [TestData.Alex] : []);
            Assert.Equal(TaskPickupOffer.TaskClosed, await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == id).Select(o => o.CloseCause).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-off", "task_pickup")]
    public async Task Offers_from_an_enabled_period_are_left_alone_while_the_flag_is_off()
    {
        var (f, d) = await EnabledFactory(); using (f)
        {
            var p = await d.Project();
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id");
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason = "Synthetic flag-off closure" })).Json(201);
            (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.task_pickup", new { value = false })).EnsureSuccessStatusCode();
            await d.Move(TestData.Pm, task, TaskStatuses.Complete);
            // FR-PKP-13: with the flag off, saves neither close offers nor send pickup notifications.
            Assert.Null(await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == id).Select(o => o.ClosedAt).SingleAsync()));
            await AssertPickupRecipients(f, d, id, NotificationEvents.TaskPickupClosed);
        }
    }

    [Fact]
    public async Task Restricted_pickup_lists_reports_and_commands_require_current_membership()
    {
        var (f, d) = await EnabledFactory(); using (f)
        {
            await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
            var p = await d.Project();
            var task = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
            var id = task.G("id"); var jill = d.User(TestData.Jill);
            const string reason = "Restricted synthetic pickup reason", note = "Restricted synthetic pickup note";
            await (await f.As(TestData.Alex).Post($"/api/v1/tasks/{id}/pickup-offer", new { reason, note, hoursRemaining = 13.25m })).Json(201);
            await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, d.Version(p.Id))).Json();
            var before = await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => new { t.AssigneeId, t.Status, t.RowVersion }).SingleAsync());
            async Task Hidden()
            {
                foreach (var url in new[] { $"/api/v1/pickup-offers?projectId={p.Id}", $"/api/v1/reports/pickup-offers?projectId={p.Id}" })
                {
                    var body = await (await f.As(TestData.Jill).GetAsync(url)).Json();
                    var text = body.ToJsonString();
                    Assert.DoesNotContain(id.ToString(), text); Assert.DoesNotContain(p.ProjectNumber, text);
                    Assert.DoesNotContain(reason, text); Assert.DoesNotContain(note, text); Assert.DoesNotContain("13.25", text);
                    if (body is System.Text.Json.Nodes.JsonArray array) Assert.Empty(array); else Assert.Empty(body["rows"]!.AsArray());
                }
                var denied = await f.As(TestData.Jill).Post($"/api/v1/tasks/{id}/pickup", new { rowVersion = before.RowVersion });
                Assert.Contains(denied.StatusCode, new[] { HttpStatusCode.Forbidden, HttpStatusCode.NotFound });
                Assert.Equal(before, await f.DbAsync(db => db.Tasks.Where(t => t.Id == id).Select(t => new { t.AssigneeId, t.Status, t.RowVersion }).SingleAsync()));
                Assert.Null(await f.DbAsync(db => db.Set<TaskPickupOffer>().Where(o => o.TaskId == id).Select(o => o.ClosedAt).SingleAsync()));
            }
            await Hidden(); // same-team nonmember
            await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = jill, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now }); return await db.SaveChangesAsync(); });
            var visible = await (await f.As(TestData.Jill).GetAsync($"/api/v1/pickup-offers?projectId={p.Id}")).Json();
            var row = Assert.Single(visible.AsArray())!;
            Assert.Equal(id, row.G("taskId")); Assert.Equal(reason, row.S("reason")); Assert.Equal(note, row.S("note")); Assert.Equal(13.25m, row["hoursRemaining"]!.GetValue<decimal>());
            var report = await (await f.As(TestData.Jill).GetAsync($"/api/v1/reports/pickup-offers?projectId={p.Id}")).Json();
            Assert.Equal(p.ProjectNumber, Assert.Single(report["rows"]!.AsArray())!.S("project"));
            await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == p.Id && m.UserId == jill)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
            await Hidden(); // formerly authorized same-team member
        }
    }

}
