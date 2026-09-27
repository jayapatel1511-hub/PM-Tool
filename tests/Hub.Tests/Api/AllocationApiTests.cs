using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class AllocationApiTests(HubFactory f)
{
    readonly TestData data = new(f);

    [Fact]
    public async Task Proposed_allocation_is_scoped_versioned_retry_safe_and_releases_its_link()
    {
        var project = await data.Project();
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var alex = data.User(TestData.Alex);
        var day = new DateOnly(2026, 10, 5);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = alex, estimatedHours = 8m, startDate = day, dueDate = day });
        var link = new AllocationEndpoints.LinkInput("Task", task.G("id"), day);
        var create = new AllocationEndpoints.CreateBody(Guid.NewGuid(), alex, AllocationPurpose.Production, day, day, 8,
            [], [link], null);
        await (await f.As(TestData.Alex).Post(root, create with { RequestId = Guid.NewGuid() })).Json(403);
        var first = await (await f.As(TestData.Pm).Post(root, create)).Json();
        var id = first.G("id");
        var replay = await (await f.As(TestData.Pm).Post(root, create)).Json();
        Assert.Equal(id, replay.G("id"));
        Assert.Single(f.Db(db => db.Allocations.Where(a => a.Id == id).ToList()));
        var detail = await (await f.As(TestData.Pm).GetAsync($"{root}/{id}")).Json();
        Assert.Equal(AllocationStatus.Proposed, detail.S("status"));
        await (await f.As(TestData.Alex).GetAsync($"{root}/{id}")).Json(404);
        var updated = await (await f.As(TestData.Pm).Post($"{root}/{id}/edit",
            new AllocationEndpoints.EditBody(Guid.NewGuid(), first.I("rowVersion"), alex, AllocationPurpose.Production,
                day, day, 8, [new(day, 8)], [link], "Confirm date"))).Json();
        Assert.Single(f.Db(db => db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToList()));
        Assert.Single(f.Db(db => db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt != null).ToList()));
        await (await f.As(TestData.Pm).Post($"{root}/{id}/cancel",
            new AllocationEndpoints.CancelBody(Guid.NewGuid(), first.I("rowVersion"), "No longer needed"))).Json(409);
        await (await f.As(TestData.Pm).Post($"{root}/{id}/cancel",
            new AllocationEndpoints.CancelBody(Guid.NewGuid(), updated.I("rowVersion"), "No longer needed"))).Json();
        Assert.Empty(f.Db(db => db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToList()));
        var replacement = await (await f.As(TestData.Pm).Post(root, create with { RequestId = Guid.NewGuid() })).Json();
        Assert.NotEqual(id, replacement.G("id"));
    }

    [Fact]
    public async Task Restricted_project_and_mismatched_task_are_rejected()
    {
        var project = await data.Project();
        await f.DbAsync(async db => { var p = await db.Projects.SingleAsync(p => p.Id == project.Id); p.Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        var other = await data.Project();
        var day = new DateOnly(2026, 10, 5);
        var task = await data.NewTask(other.Id, extra: new { assigneeId = data.User(TestData.Alex), dueDate = day });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        await (await f.As(TestData.Rita).GetAsync(root)).Json(404);
        await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), data.User(TestData.Alex),
            AllocationPurpose.Production, day, day, 4, [], [new("Task", task.G("id"), day)], null))).Json(400);
        Assert.Empty(f.Db(db => db.Allocations.Where(a => a.ProjectId == project.Id).ToList()));
    }

    [Fact]
    public async Task Supervisor_declines_and_pm_completes_with_links_and_versions_released()
    {
        var project = await data.Project();
        var personId = data.User(TestData.Alex);
        var day = new DateOnly(2026, 9, 14);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = personId, estimatedHours = 4m, startDate = day, dueDate = day });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var create = new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId, AllocationPurpose.Production, day, day, 4,
            [], [new("Task", task.G("id"), day)], null);
        var proposed = await (await f.As(TestData.Pm).Post(root, create)).Json();
        var path = $"{root}/{proposed.G("id")}";
        var decline = new AllocationEndpoints.CancelBody(Guid.NewGuid(), proposed.I("rowVersion"), "Use another plan");
        await (await f.As(TestData.Pm).Post(path + "/decline", decline)).Json(403);
        await (await f.As(TestData.Sam).Post(path + "/decline", decline)).Json();
        Assert.Equal(AllocationStatus.Declined, f.Db(db => db.Allocations.Single(a => a.Id == proposed.G("id")).Status));
        Assert.Empty(f.Db(db => db.AllocationWorkLinks.Where(l => l.AllocationId == proposed.G("id") && l.ReleasedAt == null).ToList()));
        var next = await (await f.As(TestData.Pm).Post(root, create with { RequestId = Guid.NewGuid() })).Json();
        var nextPath = $"{root}/{next.G("id")}";
        var preview = await (await f.As(TestData.Sam).GetAsync(nextPath + "/confirmation-preview")).Json();
        var confirmed = await (await f.As(TestData.Sam).Post(nextPath + "/confirm", new AllocationEndpoints.ConfirmBody(
            Guid.NewGuid(), next.I("rowVersion"), [new(day, preview["days"]![0]!["dateVersion"]!.GetValue<int>())], null))).Json();
        var priorDateVersion = f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion);
        var complete = new AllocationEndpoints.CancelBody(Guid.NewGuid(), confirmed.I("rowVersion"), "Allocation period closed");
        await (await f.As(TestData.Sam).Post(nextPath + "/complete", complete)).Json(403);
        await (await f.As(TestData.Pm).Post(nextPath + "/complete", complete)).Json();
        Assert.Equal(AllocationStatus.Completed, f.Db(db => db.Allocations.Single(a => a.Id == next.G("id")).Status));
        Assert.Empty(f.Db(db => db.AllocationWorkLinks.Where(l => l.AllocationId == next.G("id") && l.ReleasedAt == null).ToList()));
        Assert.True(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion) > priorDateVersion);
        await (await f.As(TestData.Pm).Post(nextPath + "/complete", complete with { RequestId = Guid.NewGuid() })).Json(409);
    }

    [Fact]
    public async Task Supervisor_sets_private_day_capacity_with_expected_version()
    {
        var alex = data.User(TestData.Alex);
        var day = new DateOnly(2026, 10, 6);
        var path = $"/api/v1/users/{alex}/availability/{day:yyyy-MM-dd}";
        await (await f.As(TestData.Pm).Put(path, new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json(403);
        var first = await (await f.As(TestData.Sam).Put(path,
            new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json();
        Assert.Equal(4, first["availableHours"]!.GetValue<decimal>());
        var visible = await (await f.As(TestData.Alex).GetAsync($"/api/v1/users/{alex}/availability?from={day:yyyy-MM-dd}&through={day:yyyy-MM-dd}")).Json();
        Assert.Single(visible.AsArray());
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/users/{alex}/availability?from={day:yyyy-MM-dd}&through={day:yyyy-MM-dd}")).Json(404);
        var second = await (await f.As(TestData.Sam).Put(path,
            new AllocationEndpoints.AvailabilityBody(first.I("rowVersion"), 6, AvailabilityCategory.Additional))).Json();
        Assert.Equal(6, second["availableHours"]!.GetValue<decimal>());
        await (await f.As(TestData.Sam).Put(path,
            new AllocationEndpoints.AvailabilityBody(first.I("rowVersion"), 3, AvailabilityCategory.Reduced))).Json(409);
        Assert.Equal(1, f.Db(db => db.PersonDateVersions.Count(v => v.PersonId == alex && v.WorkDate == day)));
        Assert.Equal(6, f.Db(db => db.AvailabilityOverrides.Single(v => v.PersonId == alex && v.WorkDate == day).AvailableHours));
        var grid = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload?from=2026-10-05")).Json();
        var cell = grid["people"]!.AsArray().Single(p => p!.G("id") == alex)!["cells"]![0]!;
        Assert.Equal(38, cell["available"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Confirmation_deduplicates_task_demand_and_rejects_stale_competing_date()
    {
        var project = await data.Project();
        var email = $"capacity-{Guid.NewGuid():N}@hub.test";
        await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var personId = data.User(email);
        var supervisorId = data.User(TestData.Sam);
        await f.DbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.Id == personId);
            person.SupervisorId = supervisorId;
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = personId,
                Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return 0;
        });
        var day = new DateOnly(2026, 9, 14);
        var linked = await data.NewTask(project.Id, extra: new { assigneeId = personId, estimatedHours = 8m, startDate = day, dueDate = day });
        await data.NewTask(project.Id, extra: new { assigneeId = personId, estimatedHours = 3m, startDate = day, dueDate = day });
        var unknown = await data.NewTask(project.Id, extra: new { assigneeId = personId, startDate = day, dueDate = day });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var first = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId,
            AllocationPurpose.Production, day, day, 12, [], [new("Task", linked.G("id"), day)], null))).Json();
        var second = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId,
            AllocationPurpose.Production, day, day, 1, [], [new("Task", unknown.G("id"), day)], null))).Json();
        var firstPath = $"{root}/{first.G("id")}";
        var secondPath = $"{root}/{second.G("id")}";
        await (await f.As(TestData.Pm).GetAsync(firstPath + "/confirmation-preview")).Json(403);
        var preview = await (await f.As(TestData.Sam).GetAsync(firstPath + "/confirmation-preview")).Json();
        var staleSecond = await (await f.As(TestData.Sam).GetAsync(secondPath + "/confirmation-preview")).Json();
        var row = preview["days"]![0]!;
        Assert.Equal(15, row["resultingHours"]!.GetValue<decimal>());
        Assert.Equal(7, row["overByHours"]!.GetValue<decimal>());
        AllocationEndpoints.DateVersionInput[] Versions(JsonNode p) =>
            [new(day, p["days"]![0]!["dateVersion"]!.GetValue<int>())];
        await (await f.As(TestData.Sam).Post(firstPath + "/confirm",
            new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), first.I("rowVersion"), Versions(preview), null))).Json(400);
        var confirmFirst = new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), first.I("rowVersion"), Versions(preview), "Approved overlap");
        var confirmedFirst = await (await f.As(TestData.Sam).Post(firstPath + "/confirm", confirmFirst)).Json();
        var replay = await (await f.As(TestData.Sam).Post(firstPath + "/confirm", confirmFirst)).Json();
        Assert.Equal(confirmedFirst.I("rowVersion"), replay.I("rowVersion"));
        var grid = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload")).Json();
        var cell = grid["people"]!.AsArray().Single(p => p!.G("id") == personId)!["cells"]![0]!;
        Assert.Equal(11, cell["hours"]!.GetValue<decimal>());
        Assert.Equal(12, cell["confirmed"]!.GetValue<decimal>());
        Assert.Equal(1, cell["proposed"]!.GetValue<decimal>());
        Assert.Equal(15, cell["committed"]!.GetValue<decimal>());
        Assert.Equal(40, cell["available"]!.GetValue<decimal>());
        var csv = await (await f.As(TestData.Sam).GetAsync("/api/v1/workload/export?format=csv")).Content.ReadAsStringAsync();
        Assert.Contains("Confirmed reservation (h)", csv);
        Assert.Contains("Committed load (h)", csv);
        Assert.Contains("Partial project scope", csv);
        await (await f.As(TestData.Sam).Post(secondPath + "/confirm",
            new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), second.I("rowVersion"), Versions(staleSecond), "Approved overlap"))).Json(409);
        var fresh = await (await f.As(TestData.Sam).GetAsync(secondPath + "/confirmation-preview")).Json();
        Assert.True(fresh["days"]![0]!["dateVersion"]!.GetValue<int>() > staleSecond["days"]![0]!["dateVersion"]!.GetValue<int>());
        var confirmedSecond = await (await f.As(TestData.Sam).Post(secondPath + "/confirm",
            new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), second.I("rowVersion"), Versions(fresh), "Approved overlap"))).Json();
        Assert.Equal(2, f.Db(db => db.Allocations.Count(a => a.PersonId == personId && a.Status == AllocationStatus.Confirmed)));
        var confirmedDetail = await (await f.As(TestData.Pm).GetAsync(firstPath)).Json();
        Assert.Null(confirmedDetail["confirmationSnapshot"]);
        Assert.Null(confirmedDetail["overCapacityReason"]);
        var audit = f.Db(db => db.ActivityLog.Where(log => log.ItemId == first.G("id") && log.ItemType == "ResourceAllocation")
            .OrderByDescending(log => log.OccurredAt).First());
        Assert.Null(audit.Snapshot);
        Assert.DoesNotContain("ConfirmationSnapshot", audit.Changes);
        Assert.DoesNotContain("OverCapacityReason", audit.Changes);
        Assert.DoesNotContain("Approved overlap", audit.Reason ?? "");
        var beforeEdit = f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion);
        await (await f.As(TestData.Pm).Post(firstPath + "/edit", new AllocationEndpoints.EditBody(Guid.NewGuid(),
            confirmedFirst.I("rowVersion"), personId, AllocationPurpose.Production, day, day, 12, [],
            [new("Task", linked.G("id"), day)], "Schedule revised"))).Json();
        Assert.Equal(AllocationStatus.Proposed, f.Db(db => db.Allocations.Single(a => a.Id == first.G("id")).Status));
        Assert.True(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion) > beforeEdit);
        var beforeCancel = f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion);
        await (await f.As(TestData.Pm).Post(secondPath + "/cancel", new AllocationEndpoints.CancelBody(Guid.NewGuid(),
            confirmedSecond.I("rowVersion"), "No longer needed"))).Json();
        Assert.True(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == personId && v.WorkDate == day).RowVersion) > beforeCancel);
    }

    [Fact]
    public async Task Confirmation_preview_rejects_a_task_reassigned_after_proposal()
    {
        var project = await data.Project();
        var day = new DateOnly(2026, 9, 14);
        var alex = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = alex, estimatedHours = 4m, dueDate = day });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var allocation = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), alex,
            AllocationPurpose.Production, day, day, 4, [], [new("Task", task.G("id"), day)], null))).Json();
        await f.DbAsync(async db =>
        {
            var changed = await db.Tasks.SingleAsync(t => t.Id == task.G("id"));
            changed.AssigneeId = data.User(TestData.Jill);
            await db.SaveChangesAsync();
            return 0;
        });
        await (await f.As(TestData.Sam).GetAsync($"{root}/{allocation.G("id")}/confirmation-preview")).Json(400);
        Assert.Equal(AllocationStatus.Proposed, f.Db(db => db.Allocations.Single(a => a.Id == allocation.G("id")).Status));
    }
}
