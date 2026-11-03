using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class CalendarRescheduleTests : IDisposable
{
    readonly HubFactory f = new(); readonly CalendarScreenData c;
    public CalendarRescheduleTests() => c = new(f);
    public void Dispose() => f.Dispose();

    [Fact]
    public async Task Date_context_off_still_previews_real_successor_inconsistency_without_persisting_and_due_only_commit_keeps_start()
    {
        await c.Flag("work_screens", true); await c.Flag("date_context", false); var project = await c.Data.Project();
        var task = await c.Data.NewTask(project.Id, TestData.Pm, new { name = "Synthetic predecessor", startDate = "2026-11-02", dueDate = "2026-11-03" });
        var next = await c.Data.NewTask(project.Id, TestData.Pm, new { name = "Synthetic successor", startDate = "2026-11-04", dueDate = "2026-11-05" });
        await (await f.As(TestData.Pm).Post($"/api/v1/tasks/{next.G("id")}/dependencies", new { predecessorTaskId = task.G("id"), type = "FinishToStart", lagDays = 0 })).Json(201);
        var version = await c.Data.TaskVersion(task); var before = c.State(); var preview = await c.Preview("Task", task.G("id"), version, project.Id, "2026-11-06");
        Assert.Contains(preview["context"]!["dates"]!["successors"]!.AsArray(), row => row!.G("id") == next.G("id")); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).GetAsync($"/api/v1/items/Task/{task.G("id")}/date-context?dueDate=2026-11-06")).Json(404);
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-06", reason = "Synthetic Calendar move" }, version)).Json();
        var actual = await f.DbAsync(db => db.Tasks.AsNoTracking().SingleAsync(row => row.Id == task.G("id")));
        Assert.Equal(new DateOnly(2026, 11, 2), actual.StartDate); Assert.Equal(new DateOnly(2026, 11, 6), actual.DueDate);
        Assert.Equal(new DateOnly(2026, 11, 5), await f.DbAsync(db => db.Tasks.Where(row => row.Id == next.G("id")).Select(row => row.DueDate).SingleAsync()));
        Assert.Single(await f.DbAsync(db => db.ActivityLog.Where(row => row.ItemId == actual.Id && row.Reason == "Synthetic Calendar move").ToListAsync()));
    }

    [Fact]
    public async Task Stale_preview_and_stale_commit_keep_original_version_and_do_not_promote_or_change_any_dates()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var task = await c.Data.NewTask(project.Id, TestData.Pm, new { dueDate = "2026-11-02" });
        var version = await c.Data.TaskVersion(task); await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { name = "Actual concurrent edit" }, version)).Json();
        var before = c.State(); await c.Preview("Task", task.G("id"), version, project.Id, expect: 409);
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-05", reason = "Stale Calendar intent" }, version)).Json(409);
        Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Undated_task_and_drawing_set_due_only_and_due_before_existing_start_is_refused_without_side_effects()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project();
        var task = await c.Data.NewTask(project.Id, TestData.Pm, new { startDate = "2026-11-04" }); var drawing = await c.Deliverable(project.Id, due: null);
        var before = c.State(); await c.Preview("Task", task.G("id"), await c.Data.TaskVersion(task), project.Id, "2026-11-03"); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-03", reason = "Invalid Calendar day" }, await c.Data.TaskVersion(task))).Json(400); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-05", reason = "Place undated task" }, await c.Data.TaskVersion(task))).Json();
        var dv = await f.DbAsync(db => db.Deliverables.Where(row => row.Id == drawing.G("id")).Select(row => row.RowVersion).SingleAsync());
        await c.Preview("Deliverable", drawing.G("id"), dv, project.Id);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{drawing.G("id")}", new { dueDate = "2026-11-05", reason = "Place undated drawing" }, dv)).Json();
        Assert.Equal(new DateOnly(2026, 11, 4), await f.DbAsync(db => db.Tasks.Where(row => row.Id == task.G("id")).Select(row => row.StartDate).SingleAsync()));
        Assert.Equal(new DateOnly(2026, 11, 2), await f.DbAsync(db => db.Deliverables.Where(row => row.Id == drawing.G("id")).Select(row => row.StartDate).SingleAsync()));
    }

    [Fact]
    public async Task Milestone_get_preview_has_no_receipt_or_business_write_and_commit_requires_reason_without_any_cascade()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var milestone = await c.Milestone(project.Id); var drawing = await c.Deliverable(project.Id, milestone: milestone.G("id"));
        var version = await f.DbAsync(db => db.Milestones.Where(row => row.Id == milestone.G("id")).Select(row => row.RowVersion).SingleAsync());
        var before = c.State(); var client = f.As(TestData.Pm); client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var preview = await (await client.GetAsync($"/api/v1/calendar/screen/Milestone/{milestone.G("id")}/date-preview?dueDate=2026-11-02&rowVersion={version}&projectIds={project.Id}")).Json();
        Assert.NotNull(preview["milestone"]); Assert.Null(preview["context"]); Assert.Empty(preview["milestone"]!["deliverables"]!.AsArray());
        Assert.Contains(preview["milestone"]!["inconsistent"]!.AsArray(), row => row!.S("key") == drawing.S("key")); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).Post($"/api/v1/milestones/{milestone.G("id")}/change-date", new { newDate = "2026-11-02", cascadeDeliverables = false, includeTasks = false, rowVersion = version })).Json(400); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).Post($"/api/v1/milestones/{milestone.G("id")}/change-date", new { newDate = "2026-11-02", cascadeDeliverables = false, includeTasks = false, reason = "Synthetic Calendar milestone move", rowVersion = version })).Json();
        Assert.Equal(new DateOnly(2026, 11, 3), await f.DbAsync(db => db.Deliverables.Where(row => row.Id == drawing.G("id")).Select(row => row.DueDate).SingleAsync()));
    }

    [Fact]
    public async Task Foreign_scope_and_nonmember_sources_never_gain_preview_or_write_authority()
    {
        await c.Flag("work_screens", true); await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var project = await c.Data.Project(); var other = await c.Data.Project(); var task = await c.Data.NewTask(project.Id, TestData.Pm);
        // CreateBody has no visibility member: establish restriction through the maintained project command.
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { visibility = Visibility.Restricted }, c.Data.Version(project.Id))).Json();
        Assert.Equal(Visibility.Restricted, (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}")).Json()).S("visibility"));
        var outsider = c.Data.User(TestData.Jill);
        await f.DbAsync(async db => { foreach (var member in await db.ProjectMembers.Where(row => row.ProjectId == project.Id && row.UserId == outsider && row.RemovedAt == null).ToListAsync()) member.RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        Assert.False(f.Db(db => db.ProjectMembers.Any(row => row.ProjectId == project.Id && row.UserId == outsider && row.RemovedAt == null)));
        await (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{project.Id}")).Json(404);
        await (await f.As(TestData.Jill).GetAsync($"/api/v1/tasks/{task.G("id")}")).Json(404);
        var before = c.State(); await c.Preview("Task", task.G("id"), await c.Data.TaskVersion(task), other.Id, expect: 404);
        await c.Preview("Task", task.G("id"), await c.Data.TaskVersion(task), project.Id, who: TestData.Jill, expect: 404); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Assignee_due_authority_requires_reason_and_lost_assignment_is_rechecked_without_changing_start()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var task = await c.Data.NewTask(project.Id, TestData.Pm,
            new { assigneeId = c.Data.User(TestData.Alex), dueDate = "2026-11-03", startDate = "2026-11-02" });
        var version = await c.Data.TaskVersion(task); var preview = await c.Preview("Task", task.G("id"), version, project.Id, who: TestData.Alex);
        Assert.True(preview["source"]!["canMove"]!.GetValue<bool>()); Assert.True(preview["source"]!["needsReason"]!.GetValue<bool>());
        var before = c.State(); await (await f.As(TestData.Alex).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-05" }, version)).Json(400); Assert.Equal(before, c.State());
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", new { assigneeId = c.Data.User(TestData.Marc) }, version)).Json();
        before = c.State(); await c.Preview("Task", task.G("id"), await c.Data.TaskVersion(task), project.Id, who: TestData.Alex, expect: 403);
        await (await f.As(TestData.Alex).Patch($"/api/v1/tasks/{task.G("id")}", new { dueDate = "2026-11-05", reason = "Former assignee" }, await c.Data.TaskVersion(task))).Json(403); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Repeating_an_unkeyed_due_PATCH_does_not_forge_atomic_replay_or_repeat_activity()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var task = await c.Data.NewTask(project.Id, TestData.Pm, new { dueDate = "2026-11-02" });
        var version = await c.Data.TaskVersion(task); var body = new { dueDate = "2026-11-05", reason = "One deliberate Calendar intent" };
        await c.Preview("Task", task.G("id"), version, project.Id);
        await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", body, version)).Json();
        var before = c.State(); await (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{task.G("id")}", body, version)).Json(409); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task A_synthetic_completed_milestone_remains_in_the_legacy_cohort_but_its_move_and_preview_are_refused()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var milestone = await c.Milestone(project.Id);
        await f.DbAsync(async db => { var row = await db.Milestones.SingleAsync(row => row.Id == milestone.G("id")); row.IsComplete = true; row.CompletedDate = new(2026, 11, 4); return await db.SaveChangesAsync(); });
        var before = c.State(); var read = await c.Read(project.Id);
        var source = Assert.Single(read["deadlines"]!.AsArray(), row => row!.G("id") == milestone.G("id"))!;
        Assert.False(source["canMove"]!.GetValue<bool>()); Assert.Contains(read["range"]!["entries"]!.AsArray(), row => row!.G("id") == milestone.G("id"));
        await c.Preview("Milestone", milestone.G("id"), source.I("rowVersion"), project.Id, expect: 403); Assert.Equal(before, c.State());
    }
}
