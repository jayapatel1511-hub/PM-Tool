using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 004: AC-TEAM-01, AC-TEAM-03, AC-PERM-03, AC-TSK-02/03/07/08/09/11/12, AC-REV-02/04/05, T-16, T-23, AC-NOT-06, E-04.
[Collection("api")]
public sealed class TasksTests(HubFactory f)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task Assigning_a_non_member_adds_them_and_tells_the_pm() // AC-TEAM-01, TM-06
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Jill) }); // the lead assigns; §17 never notifies the actor, so the PM is not the assigner here
        var (member, notice) = await f.DbAsync(async db => (
            await db.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Jill) && m.RemovedAt == null),
            await db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Pm) && n.EventType == NotificationEvents.MemberAutoAdded && n.ProjectId == p.Id)));
        Assert.NotNull(member);
        Assert.Contains(ProjectRole.TeamMember, member!.Roles);
        Assert.True(notice);
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Jill) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.TaskAssigned)));
    }

    [Fact]
    public async Task Assignee_starts_work_but_cannot_reassign() // AC-PERM-03
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        var moved = await d.Move(TestData.Alex, t, TaskStatuses.InProgress);
        Assert.Equal(TaskStatuses.InProgress, moved.S("status"));
        var reassign = await f.As(TestData.Alex).Patch($"/api/v1/tasks/{t.S("id")}", new { assigneeId = d.User(TestData.Jill) }, await d.TaskVersion(t));
        Assert.Equal(HttpStatusCode.Forbidden, reassign.StatusCode);
    }

    [Fact]
    public async Task Review_flow_requires_reviewer_comment_and_counts_rounds() // AC-TSK-02, AC-TSK-03, AC-REV-02, R-01, R-03
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), requiresReview = true });
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress);
        var detail = await f.As(TestData.Alex).GetAsync($"/api/v1/tasks/{t.S("id")}").Result.Json();
        var transitions = detail["permissions"]!["transitions"]!.AsArray();
        Assert.DoesNotContain(transitions, x => x!.S("to") == TaskStatuses.Complete);
        Assert.Contains(transitions, x => x!.S("to") == TaskStatuses.ReadyForReview && x["allowed"]!.GetValue<bool>());
        Assert.Equal("review_required", (await d.Move(TestData.Alex, t, TaskStatuses.Complete, expect: 422)).S("code"));
        await d.Move(TestData.Alex, t, TaskStatuses.ReadyForReview, expect: 400); // AC-TSK-03: no reviewer yet
        await d.Move(TestData.Alex, t, TaskStatuses.ReadyForReview, new { reviewerId = d.User(TestData.Diane) });
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Diane) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.ReviewRequested)));
        await d.Move(TestData.Diane, t, TaskStatuses.InReview);
        await d.Move(TestData.Diane, t, TaskStatuses.RevisionRequired, expect: 400);
        var rev = await d.Move(TestData.Diane, t, TaskStatuses.RevisionRequired, new { comment = "Check the invert levels" });
        Assert.Equal(1, rev.I("reviewRound"));
        var c = await f.DbAsync(db => db.Comments.FirstAsync(x => x.ItemId == t.G("id") && x.CommentKind == CommentKind.Review));
        Assert.Equal(1, c.ReviewRound);
        Assert.Equal("Check the invert levels", c.Body);
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Alex) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.ReviewOutcome)));
    }

    [Fact]
    public async Task Self_review_is_refused_unless_allowed() // AC-REV-04, R-02
    {
        var p = await d.Project();
        var r = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/tasks", new { name = "Self", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), assigneeId = d.User(TestData.Marc), reviewerId = d.User(TestData.Marc) });
        var body = await r.Json(400);
        Assert.Contains("reviewer", body["errors"]!.ToJsonString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Changing_reviewer_mid_review_notifies_both() // AC-REV-05, R-04
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), requiresReview = true, reviewerId = d.User(TestData.Diane) });
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress);
        await d.Move(TestData.Alex, t, TaskStatuses.ReadyForReview);
        await d.Move(TestData.Diane, t, TaskStatuses.InReview);
        (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.S("id")}", new { reviewerId = d.User(TestData.Jill) }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
        var (toOld, toNew, logged) = await f.DbAsync(async db => (
            await db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Diane) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.ReviewerSet),
            await db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Jill) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.ReviewerSet),
            (await db.ActivityLog.Where(a => a.ItemId == t.G("id")).Select(a => a.Changes).ToListAsync()).Any(c => c.Contains("ReviewerId"))));
        Assert.True(toOld);
        Assert.True(toNew);
        Assert.True(logged);
    }

    [Fact]
    public async Task Completing_sets_progress_and_reopen_needs_reason_and_resets() // AC-TSK-07, AC-TSK-08, T-14, T-20
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex) });
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress);
        (await f.As(TestData.Alex).Patch($"/api/v1/tasks/{t.S("id")}", new { progressPct = 100 }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
        var done = await d.Move(TestData.Alex, t, TaskStatuses.Complete);
        Assert.Equal(100, done.I("progressPct"));
        Assert.NotNull(await f.DbAsync(db => db.Tasks.Where(x => x.Id == t.G("id")).Select(x => x.CompletedAt).FirstAsync()));
        await d.Move(TestData.Alex, t, TaskStatuses.InProgress, new { reason = "Needs rework" }, expect: 403); // assignee may not reopen
        await d.Move(TestData.Marc, t, TaskStatuses.InProgress, expect: 400); // reason required
        var reopened = await d.Move(TestData.Marc, t, TaskStatuses.InProgress, new { reason = "Client changed the alignment" });
        Assert.Equal(90, reopened.I("progressPct"));
        var saved = await f.DbAsync(db => db.Tasks.FirstAsync(x => x.Id == t.G("id")));
        Assert.Null(saved.CompletedAt);
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == t.G("id") && a.Action == "Reopened" && a.Reason == "Client changed the alignment")));
    }

    [Fact]
    public async Task Collaborator_updates_progress_but_not_due_date() // AC-TSK-11, T-15
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-01" });
        (await f.As(TestData.Marc).Post($"/api/v1/tasks/{t.S("id")}/collaborators", new { userId = d.User(TestData.Jill) })).EnsureSuccessStatusCode();
        var jill = f.As(TestData.Jill);
        await d.Move(TestData.Jill, t, TaskStatuses.InProgress);
        (await jill.Patch($"/api/v1/tasks/{t.S("id")}", new { progressPct = 30 }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
        var due = await jill.Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2026-10-15", reason = "More time" }, await d.TaskVersion(t));
        Assert.Equal(HttpStatusCode.Forbidden, due.StatusCode);
    }

    [Fact]
    public async Task Stale_version_is_refused_with_who_and_when() // AC-TSK-12, G-07
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id);
        var v = await d.TaskVersion(t);
        (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.S("id")}", new { name = "First edit" }, v)).EnsureSuccessStatusCode();
        var clash = await f.As(TestData.Pm).Patch($"/api/v1/tasks/{t.S("id")}", new { name = "Second edit" }, v);
        var body = await clash.Json(409);
        Assert.Equal("Marc Dubois", body.S("changedBy"));
        Assert.NotNull(body["changedAt"]);
    }

    [Fact]
    public async Task Assignee_due_change_needs_reason_and_is_counted() // T-16, FR-014
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-01" });
        var alex = f.As(TestData.Alex);
        Assert.Equal(HttpStatusCode.BadRequest, (await alex.Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2026-10-05" }, await d.TaskVersion(t))).StatusCode);
        var ok = await alex.Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2026-10-05", reason = "Waiting on survey" }, await d.TaskVersion(t)).Result.Json();
        Assert.Equal(1, ok.I("dueDateChangeCount"));
        (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2026-10-09" }, await d.TaskVersion(t))).EnsureSuccessStatusCode(); // lead needs no reason
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Alex) && n.ItemId == t.G("id") && n.EventType == NotificationEvents.DueDateChanged)));
        Assert.Equal(2, await f.DbAsync(db => db.Tasks.Where(x => x.Id == t.G("id")).Select(x => x.DueDateChangeCount).FirstAsync()));
    }

    [Fact]
    public async Task Bulk_shift_skips_forbidden_rows_and_sends_one_notice() // FR-TSK-09, T-23, E-20, AC-NOT-06
    {
        var p = await d.Project();
        var civil = new List<JsonNode>();
        for (var i = 0; i < 12; i++) civil.Add(await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), dueDate = "2026-10-01" }));
        var elec = new[] { await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-01" }, "Electrical"), await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-01" }, "Electrical") };
        var ids = civil.Concat(elec).Select(x => x.G("id")).ToArray();
        var marc = f.As(TestData.Marc); // Civil lead: no permission on Electrical tasks
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = ids, operation = "shiftDueDates", @params = new { days = 7 } })).StatusCode);
        var r = await marc.Post($"/api/v1/projects/{p.Id}/tasks/bulk", new { taskIds = ids, operation = "shiftDueDates", @params = new { days = 7 }, reason = "Survey delayed a week" }).Result.Json();
        Assert.Equal(12, r.I("updated"));
        Assert.Equal(2, r["skipped"]!.AsArray().Count);
        Assert.Equal(1, await f.DbAsync(db => db.Notifications.CountAsync(n => n.UserId == d.User(TestData.Alex) && n.ProjectId == p.Id && n.EventType == NotificationEvents.DueDateChanged)));
        Assert.Equal(new DateOnly(2026, 10, 8), await f.DbAsync(db => db.Tasks.Where(x => x.Id == civil[0].G("id")).Select(x => x.DueDate).FirstAsync()));
        Assert.Equal(new DateOnly(2026, 10, 1), await f.DbAsync(db => db.Tasks.Where(x => x.Id == elec[0].G("id")).Select(x => x.DueDate).FirstAsync()));
    }

    [Fact]
    public async Task Delete_removes_edges_and_restore_brings_them_back() // AC-TSK-09, T-08, D-09, E-04
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm);
        var b = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), dependsOn = new[] { a.G("id") } });
        var preview = await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{a.S("id")}/delete-preview").Result.Json();
        Assert.Single(preview["dependencies"]!.AsArray());
        var del = await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{a.S("id")}").Result.Json();
        Assert.Equal(1, del.I("removedDependencies"));
        Assert.Equal(0, await f.DbAsync(db => db.Dependencies.CountAsync(x => x.SuccessorTaskId == b.G("id"))));
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == d.User(TestData.Alex) && n.EventType == NotificationEvents.DependencyRemoved)));
        var snap = await f.DbAsync(db => db.ActivityLog.Where(x => x.ItemId == a.G("id") && x.Action == "Deleted").Select(x => x.Snapshot).FirstAsync());
        Assert.Contains("removedDependencies", snap!);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{a.S("id")}")).StatusCode);
        (await f.As(TestData.Pm).PostAsync($"/api/v1/tasks/{a.S("id")}/restore", null)).EnsureSuccessStatusCode();
        Assert.Equal(1, await f.DbAsync(db => db.Dependencies.CountAsync(x => x.SuccessorTaskId == b.G("id"))));
    }

    [Fact]
    public async Task Removing_a_member_lists_open_items_and_flags_left_tasks() // AC-TEAM-03, TM-04
    {
        var p = await d.Project();
        for (var i = 0; i < 5; i++) await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        var member = await f.DbAsync(db => db.ProjectMembers.FirstAsync(m => m.ProjectId == p.Id && m.UserId == d.User(TestData.Alex)));
        var items = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/members/{member.Id}/open-items").Result.Json();
        Assert.Equal(5, items.I("count"));
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/projects/{p.Id}/members/{member.Id}")).EnsureSuccessStatusCode();
        await f.Evaluate(p.Id);
        var flagged = await f.DbAsync(db => db.TaskStates.Where(s => s.ProjectId == p.Id).ToListAsync());
        Assert.Equal(5, flagged.Count(s => s.Notes.Contains("assignee_not_on_project")));
    }

    [Fact]
    public async Task Lists_filter_sort_and_page() // FR-017, §13.3 default sort
    {
        var p = await d.Project();
        var late = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-20", priority = "Low" });
        var soon = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-09-20", priority = "High", assigneeId = d.User(TestData.Alex) });
        var none = await d.NewTask(p.Id, TestData.Pm);
        var all = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks").Result.Json();
        Assert.Equal(new[] { soon.S("key"), late.S("key"), none.S("key") }, all["items"]!.AsArray().Select(x => x!.S("key")));
        var mine = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/tasks?mine=true").Result.Json();
        Assert.Equal(soon.S("key"), Assert.Single(mine["items"]!.AsArray())!.S("key"));
        var paged = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks?pageSize=2&page=2").Result.Json();
        Assert.Equal(3, paged.I("totalCount"));
        Assert.Single(paged["items"]!.AsArray());
        var viewer = await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{p.Id}/tasks");
        Assert.Equal(HttpStatusCode.OK, viewer.StatusCode); // Read Only sees active projects
    }
}
