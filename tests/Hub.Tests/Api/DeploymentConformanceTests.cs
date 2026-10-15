using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DeploymentConformanceTests(HubFactory f)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task Following_counts_every_unread_action_beyond_one_thousand_rows_and_collapses_correlated_changes()
    {
        var email = $"following-{Guid.NewGuid():N}@hub.test"; var client = f.As(email);
        (await client.GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var p = await d.Project(); var task = await d.NewTask(p.Id);
        var actor = d.User(TestData.Pm);
        (await client.Put($"/api/v1/projects/{p.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
        var before = (await (await client.GetAsync("/api/v1/me/notifications/unread-count")).Json()).I("following");
        await f.DbAsync(async db =>
        {
            var duplicate = Guid.NewGuid();
            db.ActivityLog.AddRange(Enumerable.Range(0, 1002).Select(i => new ActivityLog { ProjectId = p.Id,
                ItemType = ItemType.Task, ItemId = task.G("id"), ItemKey = task.S("key"), ItemName = "Synthetic unread action",
                Action = "Updated", Categories = ["date"], Changes = "[]", ActorUserId = actor, ActorType = "User",
                OccurredAt = f.Clock.Now.AddMinutes(1), CorrelationId = i < 2 ? duplicate : Guid.NewGuid() }));
            return await db.SaveChangesAsync();
        });
        Assert.Equal(before + 1001, (await (await client.GetAsync("/api/v1/me/notifications/unread-count")).Json()).I("following"));
        var page = await (await client.GetAsync("/api/v1/me/following?page=2&pageSize=200")).Json();
        Assert.Equal(2, page.I("page")); Assert.True(page.I("totalCount") >= 1002); Assert.NotEmpty(page["items"]!.AsArray());
    }

    [Fact]
    public async Task Workspace_scope_only_changes_advance_the_version_and_reject_the_old_scope()
    {
        var p = await d.Project(); var second = await d.Project();
        var client = f.As(TestData.Pm);
        var ws = await (await client.Post("/api/v1/workspaces", new { name = $"Synthetic scope {Guid.NewGuid():N}", projectIds = new[] { p.Id } })).Json(201);
        var updated = await (await client.Patch($"/api/v1/workspaces/{ws.G("id")}", new { name = ws.S("name"), projectIds = new[] { second.Id } }, ws.I("rowVersion"))).Json();
        Assert.True(updated.I("rowVersion") > ws.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.Patch($"/api/v1/workspaces/{ws.G("id")}", new { name = ws.S("name"), projectIds = new[] { p.Id } }, ws.I("rowVersion"))).StatusCode);
        Assert.Equal(second.Id, (await (await client.GetAsync("/api/v1/workspaces")).Json()).AsArray().Single(w => w!.G("id") == ws.G("id"))!["projectIds"]![0]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Staff_assignment_response_contains_the_version_required_by_its_remove_action()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex);
        var rows = await (await f.As(TestData.Sam).GetAsync($"/api/v1/staff/{alex}/assignments")).Json();
        var member = rows.AsArray().Single(r => r!.G("projectId") == p.Id)!;
        Assert.Equal(f.Db(db => db.ProjectMembers.Single(m => m.Id == member.G("memberId")).RowVersion), member.I("rowVersion"));
    }

    [Fact]
    public async Task Hidden_confirmed_allocations_mark_capacity_partial_without_returning_private_work()
    {
        var email = $"partial-capacity-{Guid.NewGuid():N}@hub.test";
        var person = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id");
        await f.DbAsync(async db => { var u = await db.Users.SingleAsync(x => x.Id == person); u.SupervisorId = d.User(TestData.Sam); u.WeeklyCapacityHours = 40; return await db.SaveChangesAsync(); });
        var week = new DateOnly(2026, 9, 14);
        var route = $"/api/v1/planning/grid?personIds={person}&from={week:yyyy-MM-dd}&weeks=1";
        var before = (await (await f.As(TestData.Sam).GetAsync(route)).Json())["people"]![0]!;
        var project = await d.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; db.Allocations.Add(new() { PersonId = person, ProjectId = project.Id, Purpose = AllocationPurpose.Production,
            FromDate = week, ThroughDate = week.AddDays(4), PlannedHours = 20, Status = AllocationStatus.Confirmed }); return await db.SaveChangesAsync(); });
        Assert.Equal(Visibility.Restricted, f.Db(db => db.Projects.Single(p => p.Id == project.Id).Visibility));
        var response = await (await f.As(TestData.Sam).GetAsync(route)).Json();
        var after = response["people"]![0]!;
        Assert.False(before["partial"]!.GetValue<bool>());
        Assert.True(after["partial"]!.GetValue<bool>());
        Assert.Equal(before["cells"]![0]!["remaining"]!.ToJsonString(), after["cells"]![0]!["remaining"]!.ToJsonString());
        Assert.False(after["cells"]![0]!["underPlanned"]!.GetValue<bool>());
        Assert.Empty(after["approvedAllocations"]!.AsArray());
        Assert.DoesNotContain(project.Id.ToString(), response.ToJsonString());
        Assert.DoesNotContain(project.ProjectNumber, response.ToJsonString());
        var next = (await (await f.As(TestData.Sam).GetAsync(route.Replace(week.ToString("yyyy-MM-dd"), week.AddDays(7).ToString("yyyy-MM-dd"), StringComparison.Ordinal))).Json())["people"]![0]!;
        Assert.False(next["partial"]!.GetValue<bool>());
        await f.DbAsync(async db => { var allocation = await db.Allocations.SingleAsync(a => a.PersonId == person); allocation.FromDate = week.AddDays(7); allocation.ThroughDate = week.AddDays(11); return await db.SaveChangesAsync(); });
        var currentOnly = (await (await f.As(TestData.Sam).GetAsync(route)).Json())["people"]![0]!;
        Assert.False(currentOnly["partial"]!.GetValue<bool>());
        var future = (await (await f.As(TestData.Sam).GetAsync(route.Replace(week.ToString("yyyy-MM-dd"), week.AddDays(7).ToString("yyyy-MM-dd"), StringComparison.Ordinal))).Json())["people"]![0]!;
        Assert.True(future["partial"]!.GetValue<bool>());
    }

    [Fact]
    public void Every_auditable_model_type_has_an_explicit_valid_field_allowlist()
    {
        var missing = f.Db(db => db.Model.GetEntityTypes()
            .Where(e => typeof(IAuditable).IsAssignableFrom(e.ClrType) && !e.ClrType.IsAbstract)
            .Where(e => !AuditRules.Fields.ContainsKey(e.ClrType)).Select(e => e.ClrType.Name).ToArray());
        Assert.Empty(missing);
        var invalid = f.Db(db => db.Model.GetEntityTypes().Where(e => AuditRules.Fields.ContainsKey(e.ClrType))
            .SelectMany(e => AuditRules.Fields[e.ClrType].Where(field => e.FindProperty(field) == null).Select(field => $"{e.ClrType.Name}.{field}")).ToArray());
        Assert.Empty(invalid);
    }

    [Fact]
    public async Task Coordination_changes_persist_actor_reason_values_and_status_category()
    {
        var p = await d.Project(); var task = await d.NewTask(p.Id);
        var id = await f.DbAsync(async db =>
        {
            var row = new WorkConstraint { ProjectId = p.Id, TargetType = "Task", TargetId = task.G("id"), Seq = 999, Key = "CT0999",
                Category = "Handoff", Description = "Synthetic audit check", RemovalOwnerId = d.User(TestData.Pm),
                AffectedOwnerId = d.User(TestData.Alex), NeededBy = new(2026, 10, 20), SourceUrl = "https://example.test/evidence" };
            db.WorkConstraints.Add(row); await db.SaveChangesAsync(); return row.Id;
        });
        Assert.True(f.Db(db => db.ActivityLog.Any(a => a.ItemId == id && a.Action == "Created")));
        await f.DbAsync(async db =>
        {
            var row = await db.WorkConstraints.SingleAsync(c => c.Id == id);
            db.Audit.ActorType = "User"; db.Audit.ActorId = d.User(TestData.Pm);
            db.Audit.Note(row, reason: "Scope no longer needs this input");
            row.State = ConstraintState.Cancelled;
            return await db.SaveChangesAsync();
        });
        var log = f.Db(db => db.ActivityLog.Where(a => a.ItemId == id && a.Categories.Contains("status")).OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).First());
        Assert.Equal(d.User(TestData.Pm), log.ActorUserId);
        Assert.Equal("Scope no longer needs this input", log.Reason);
        Assert.Contains("Open", log.Changes); Assert.Contains("Cancelled", log.Changes);
    }

    [Theory]
    [InlineData("Task")]
    [InlineData("Deliverable")]
    public async Task New_and_edited_comments_refresh_the_targets_activity(string type)
    {
        var saved = f.Clock.Now;
        try
        {
            var p = await d.Project();
            var target = type == "Task" ? await d.NewTask(p.Id) : await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables",
                new { name = "Synthetic discussion target", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() })).Json(201);
            var id = target.G("id");
            f.Clock.Now = saved.AddDays(1);
            var c = await (await f.As(TestData.Marc).Post($"/api/v1/items/{type}/{id}/comments", new { body = "Current coordination discussion" })).Json(201);
            DateTimeOffset Activity() => f.Db(db => type == "Task" ? db.Tasks.Single(t => t.Id == id).LastActivityAt : db.Deliverables.Single(x => x.Id == id).LastActivityAt);
            Assert.Equal(f.Clock.Now, Activity());
            f.Clock.Now = f.Clock.Now.AddMinutes(1);
            (await f.As(TestData.Marc).Patch($"/api/v1/comments/{c.G("id")}", new { body = "Clarified discussion" })).EnsureSuccessStatusCode();
            Assert.Equal(f.Clock.Now, Activity());
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Editable_document_links_reject_stale_updates_and_deletes()
    {
        var p = await d.Project(); var client = f.As(TestData.Pm);
        var link = await (await client.Post($"/api/v1/projects/{p.Id}/links", new { title = "Synthetic reference", url = "https://example.test/source" })).Json(201);
        var edited = await (await client.Patch($"/api/v1/links/{link.G("id")}", new { linkType = "Client Portal" }, link.I("rowVersion"))).Json();
        Assert.True(edited.I("rowVersion") > link.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, (await client.Patch($"/api/v1/links/{link.G("id")}", new { title = "Stale overwrite" }, link.I("rowVersion"))).StatusCode);
        using var stale = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/projects/{p.Id}/links/{link.G("id")}");
        stale.Headers.Add("If-Match", $"\"{link.I("rowVersion")}\"");
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(stale)).StatusCode);
        using var remove = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/links/{link.G("id")}");
        remove.Headers.Add("If-Match", $"\"{edited.I("rowVersion")}\"");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(remove)).StatusCode);
        Assert.True(f.Db(db => db.DocumentLinks.IgnoreQueryFilters().Single(l => l.Id == link.G("id")).DeletedAt != null));
    }
}
