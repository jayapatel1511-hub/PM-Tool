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

    async Task<(Guid Id, string Email)> FreshReport(Guid? projectId = null)
    {
        var email = $"allocation-{Guid.NewGuid():N}@hub.test";
        await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var personId = data.User(email);
        await f.DbAsync(async db =>
        {
            var person = await db.Users.SingleAsync(u => u.Id == personId);
            person.SupervisorId = data.User(TestData.Sam);
            if (projectId is { } pid) db.ProjectMembers.Add(new ProjectMember { ProjectId = pid, UserId = personId,
                Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return 0;
        });
        return (personId, email);
    }

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
        var supervisorId = data.User(TestData.Sam);
        var notice = f.Db(db => db.Notifications.Single(n => n.UserId == supervisorId && n.EventType == NotificationEvents.AllocationChanged && n.ItemId == id));
        Assert.Contains($"allocation={id}", notice.LinkPath ?? "");
        var detail = await (await f.As(TestData.Pm).GetAsync($"{root}/{id}")).Json();
        Assert.Equal(AllocationStatus.Proposed, detail.S("status"));
        Assert.False(detail["canConfirm"]!.GetValue<bool>());
        Assert.True(detail["canManage"]!.GetValue<bool>());
        var supervisorDetail = await (await f.As(TestData.Sam).GetAsync($"{root}/{id}")).Json();
        Assert.True(supervisorDetail["canConfirm"]!.GetValue<bool>());
        Assert.False(supervisorDetail["canManage"]!.GetValue<bool>());
        Assert.False(string.IsNullOrWhiteSpace(supervisorDetail.S("personName")));
        var supervisorList = await (await f.As(TestData.Sam).GetAsync(root)).Json();
        Assert.Contains(supervisorList["items"]!.AsArray(), a => a!.G("id") == id && !string.IsNullOrWhiteSpace(a.S("personName")));
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
    public async Task Filtered_allocation_pagination_and_exports_share_visible_records()
    {
        var project = await data.Project();
        var (personId, _) = await FreshReport(project.Id);
        var day = new DateOnly(2026, 10, 5);
        async Task<JsonNode> Create()
        {
            var task = await data.NewTask(project.Id, extra: new { assigneeId = personId, dueDate = day });
            return await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/allocations",
                new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId, AllocationPurpose.Production, day, day, 4,
                    [], [new("Task", task.G("id"), day)], null))).Json();
        }
        var first = await Create();
        var second = await Create();
        var filter = $"q=Production&personId={personId}&purpose=Production&status=Proposed&from={day:yyyy-MM-dd}&to={day:yyyy-MM-dd}";
        var page1 = await (await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{project.Id}/allocations?{filter}&page=1&pageSize=1")).Json();
        var page2 = await (await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{project.Id}/allocations?{filter}&page=2&pageSize=1")).Json();
        Assert.Equal(2, page1.I("totalCount"));
        Assert.Single(page1["items"]!.AsArray());
        Assert.Single(page2["items"]!.AsArray());
        Assert.NotEqual(page1["items"]![0]!.G("id"), page2["items"]![0]!.G("id"));
        Assert.Contains(first.G("id"), new[] { page1["items"]![0]!.G("id"), page2["items"]![0]!.G("id") });
        Assert.Contains(second.G("id"), new[] { page1["items"]![0]!.G("id"), page2["items"]![0]!.G("id") });

        var csv = await (await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{project.Id}/allocations/export?{filter}&format=csv")).Content.ReadAsStringAsync();
        Assert.Equal(3, csv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        var xlsx = await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{project.Id}/allocations/export?{filter}&format=xlsx");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);

        var supervisor = await (await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{project.Id}/allocations?personId={personId}&pageSize=200")).Json();
        Assert.Equal(2, supervisor.I("totalCount"));
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
        var restrictedTask = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex), estimatedHours = 4m, dueDate = day });
        var hidden = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), data.User(TestData.Alex),
            AllocationPurpose.Production, day, day, 4, [], [new("Task", restrictedTask.G("id"), day)], null))).Json();
        var supervisorId = data.User(TestData.Sam);
        Assert.False(f.Db(db => db.Notifications.Any(n => n.UserId == supervisorId && n.ItemId == hidden.G("id"))));
    }

    [Fact]
    public async Task Supervisor_declines_and_pm_completes_with_links_and_versions_released()
    {
        var project = await data.Project();
        var personId = (await FreshReport(project.Id)).Id;
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
        var (alex, email) = await FreshReport();
        var day = new DateOnly(2026, 10, 6);
        var path = $"/api/v1/users/{alex}/availability/{day:yyyy-MM-dd}";
        await (await f.As(TestData.Pm).Put(path, new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json(403);
        var first = await (await f.As(TestData.Sam).Put(path,
            new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json();
        Assert.Equal(4, first["availableHours"]!.GetValue<decimal>());
        var visible = await (await f.As(email).GetAsync($"/api/v1/users/{alex}/availability?from={day:yyyy-MM-dd}&through={day:yyyy-MM-dd}")).Json();
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
    public async Task Simultaneous_cross_project_confirmation_accepts_only_one_stale_date_version()
    {
        var firstProject = await data.Project();
        var secondProject = await data.Project();
        var personId = (await FreshReport(firstProject.Id)).Id;
        await f.DbAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = secondProject.Id, UserId = personId,
                Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync();
            return 0;
        });
        var day = new DateOnly(2026, 9, 14);
        async Task<(string Path, int Version, int DateVersion)> Proposal(Guid projectId)
        {
            var task = await data.NewTask(projectId, extra: new { assigneeId = personId, estimatedHours = 6m, startDate = day, dueDate = day });
            var root = $"/api/v1/projects/{projectId}/allocations";
            var created = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId,
                AllocationPurpose.Production, day, day, 6, [], [new("Task", task.G("id"), day)], null))).Json();
            var path = $"{root}/{created.G("id")}";
            var preview = await (await f.As(TestData.Sam).GetAsync(path + "/confirmation-preview")).Json();
            return (path, created.I("rowVersion"), preview["days"]![0]!["dateVersion"]!.GetValue<int>());
        }
        var first = await Proposal(firstProject.Id);
        var second = await Proposal(secondProject.Id);
        Assert.Equal(first.DateVersion, second.DateVersion);
        async Task<System.Net.HttpStatusCode> Confirm((string Path, int Version, int DateVersion) row)
        {
            var response = await f.As(TestData.Sam).Post(row.Path + "/confirm", new AllocationEndpoints.ConfirmBody(
                Guid.NewGuid(), row.Version, [new(day, row.DateVersion)], "Approved overlapping demand"));
            return response.StatusCode;
        }
        var outcomes = await Task.WhenAll(Confirm(first), Confirm(second));
        Assert.Equal(new[] { System.Net.HttpStatusCode.OK, System.Net.HttpStatusCode.Conflict }, outcomes.Order());
        Assert.Equal(1, f.Db(db => db.Allocations.Count(a => a.PersonId == personId && a.Status == AllocationStatus.Confirmed)));
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
        Assert.True(confirmedDetail["overCapacityWarningRecorded"]!.GetValue<bool>());
        Assert.Equal("Approved overlap", f.Db(db => db.Allocations.Single(a => a.Id == first.G("id")).OverCapacityReason));
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

    [Fact]
    public async Task Inactive_or_removed_people_keep_allocation_history_and_can_be_replaced()
    {
        var project = await data.Project();
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var day = new DateOnly(2026, 10, 5);

        async Task<JsonNode> Confirmed(Guid personId)
        {
            var task = await data.NewTask(project.Id, extra: new { assigneeId = personId, estimatedHours = 4m, startDate = day, dueDate = day });
            var allocation = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId,
                AllocationPurpose.Production, day, day, 4, [], [new("Task", task.G("id"), day)], null))).Json();
            var preview = await (await f.As(TestData.Sam).GetAsync($"{root}/{allocation.G("id")}/confirmation-preview")).Json();
            return await (await f.As(TestData.Sam).Post($"{root}/{allocation.G("id")}/confirm", new AllocationEndpoints.ConfirmBody(
                Guid.NewGuid(), allocation.I("rowVersion"), [new(day, preview["days"]![0]!["dateVersion"]!.GetValue<int>())], null))).Json();
        }

        async Task ReplaceAndConfirm(JsonNode allocation, Guid replacementId)
        {
            var replacementTask = await data.NewTask(project.Id, extra: new { assigneeId = replacementId, estimatedHours = 4m, startDate = day, dueDate = day });
            var edited = await (await f.As(TestData.Pm).Post($"{root}/{allocation.G("id")}/edit", new AllocationEndpoints.EditBody(
                Guid.NewGuid(), allocation.I("rowVersion"), replacementId, AllocationPurpose.Production, day, day, 4, [],
                [new("Task", replacementTask.G("id"), day)], "Replace unavailable person"))).Json();
            var editedDetail = await (await f.As(TestData.Pm).GetAsync($"{root}/{allocation.G("id")}")).Json();
            Assert.Equal(AllocationStatus.Proposed, editedDetail.S("status"));
            Assert.True(editedDetail["personId"]!.GetValue<Guid>() == replacementId);
            Assert.NotNull(editedDetail["confirmedBy"]);
            Assert.NotNull(editedDetail["confirmedAt"]);
            Assert.NotNull(f.Db(db => db.Allocations.Single(a => a.Id == allocation.G("id")).ConfirmationSnapshot));
            var preview = await (await f.As(TestData.Sam).GetAsync($"{root}/{allocation.G("id")}/confirmation-preview")).Json();
            await (await f.As(TestData.Sam).Post($"{root}/{allocation.G("id")}/confirm", new AllocationEndpoints.ConfirmBody(
                Guid.NewGuid(), edited.I("rowVersion"), [new(day, preview["days"]![0]!["dateVersion"]!.GetValue<int>())], null))).Json();
        }

        var inactive = await FreshReport(project.Id);
        var inactiveAllocation = await Confirmed(inactive.Id);
        await f.DbAsync(async db =>
        {
            db.Users.Single(u => u.Id == inactive.Id).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var inactiveDetail = await (await f.As(TestData.Pm).GetAsync($"{root}/{inactiveAllocation.G("id")}")).Json();
        Assert.Equal("Inactive", inactiveDetail.S("personState"));
        Assert.False(inactiveDetail["personEligible"]!.GetValue<bool>());
        Assert.False(inactiveDetail["canConfirm"]!.GetValue<bool>());
        Assert.Equal(AllocationStatus.Confirmed, inactiveDetail.S("status"));
        await (await f.As(TestData.Sam).GetAsync($"{root}/{inactiveAllocation.G("id")}/confirmation-preview")).Json(400);
        var inactiveAllocationList = await (await f.As(TestData.Pm).GetAsync(root)).Json();
        Assert.Equal("Inactive", inactiveAllocationList["items"]!.AsArray().Single(a => a!.G("id") == inactiveAllocation.G("id"))!.S("personState"));
        await ReplaceAndConfirm(inactiveAllocation, (await FreshReport(project.Id)).Id);

        var removed = await FreshReport(project.Id);
        var removedAllocation = await Confirmed(removed.Id);
        await f.DbAsync(async db =>
        {
            var membership = await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == removed.Id);
            membership.RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        var removedDetail = await (await f.As(TestData.Pm).GetAsync($"{root}/{removedAllocation.G("id")}")).Json();
        Assert.Equal("Removed", removedDetail.S("personState"));
        Assert.False(removedDetail["personEligible"]!.GetValue<bool>());
        Assert.False(removedDetail["canConfirm"]!.GetValue<bool>());
        Assert.Equal(AllocationStatus.Confirmed, removedDetail.S("status"));
        await (await f.As(TestData.Sam).GetAsync($"{root}/{removedAllocation.G("id")}/confirmation-preview")).Json(400);
        var removedAllocationList = await (await f.As(TestData.Pm).GetAsync(root)).Json();
        Assert.Equal("Removed", removedAllocationList["items"]!.AsArray().Single(a => a!.G("id") == removedAllocation.G("id"))!.S("personState"));
        await ReplaceAndConfirm(removedAllocation, (await FreshReport(project.Id)).Id);
    }

    [Fact]
    public async Task Date_only_edit_withdraws_confirmation_and_requires_reconfirmation()
    {
        var project = await data.Project();
        var personId = (await FreshReport(project.Id)).Id;
        var from = new DateOnly(2026, 9, 14);
        var through = from.AddDays(1);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = personId, estimatedHours = 4m, startDate = from, dueDate = through });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var created = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), personId,
            AllocationPurpose.Production, from, from, 4, [], [new("Task", task.G("id"), from)], null))).Json();
        var initialPreview = await (await f.As(TestData.Sam).GetAsync($"{root}/{created.G("id")}/confirmation-preview")).Json();
        var confirmed = await (await f.As(TestData.Sam).Post($"{root}/{created.G("id")}/confirm", new AllocationEndpoints.ConfirmBody(
            Guid.NewGuid(), created.I("rowVersion"), [new(from, initialPreview["days"]![0]!["dateVersion"]!.GetValue<int>())], null))).Json();
        var provenance = f.Db(db => db.Allocations.Where(a => a.Id == created.G("id")).Select(a => new { a.ConfirmedBy, a.ConfirmedAt, a.ConfirmationSnapshot }).Single());

        var edited = await (await f.As(TestData.Pm).Post($"{root}/{created.G("id")}/edit", new AllocationEndpoints.EditBody(
            Guid.NewGuid(), confirmed.I("rowVersion"), personId, AllocationPurpose.Production, through, through, 4, [],
            [new("Task", task.G("id"), through)], "Move allocation date"))).Json();
        var editedDetail = await (await f.As(TestData.Pm).GetAsync($"{root}/{created.G("id")}")).Json();
        Assert.Equal(AllocationStatus.Proposed, editedDetail.S("status"));
        Assert.Equal(provenance.ConfirmedBy, editedDetail["confirmedBy"]!.GetValue<Guid>());
        Assert.Equal(provenance.ConfirmedAt, editedDetail["confirmedAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal(provenance.ConfirmationSnapshot, f.Db(db => db.Allocations.Single(a => a.Id == created.G("id")).ConfirmationSnapshot));

        var reconfirmPreview = await (await f.As(TestData.Sam).GetAsync($"{root}/{created.G("id")}/confirmation-preview")).Json();
        await (await f.As(TestData.Sam).Post($"{root}/{created.G("id")}/confirm", new AllocationEndpoints.ConfirmBody(
            Guid.NewGuid(), edited.I("rowVersion"), [new(through, reconfirmPreview["days"]![0]!["dateVersion"]!.GetValue<int>())], null))).Json();
        var reconfirmedDetail = await (await f.As(TestData.Pm).GetAsync($"{root}/{created.G("id")}")).Json();
        Assert.Equal(AllocationStatus.Confirmed, reconfirmedDetail.S("status"));
    }
}
