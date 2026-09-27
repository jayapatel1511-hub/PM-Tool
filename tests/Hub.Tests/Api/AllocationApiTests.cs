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
    }
}
