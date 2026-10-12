using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 036, FR-ROLE-01..05: open roles, filling them through a normal allocation proposal, and their privacy.
[Collection("api")]
public sealed class OpenRoleApiTests(HubFactory f)
{
    readonly TestData data = new(f);
    static readonly DateOnly Mon = new(2026, 10, 5), Fri = new(2026, 10, 9);

    async Task<JsonNode> Role(Guid projectId, string as_ = TestData.Pm, string? label = null, DateOnly? from = null, DateOnly? through = null,
        decimal hours = 20, Guid? disciplineId = null, int expect = 201)
        => await (await f.As(as_).Post($"/api/v1/projects/{projectId}/open-roles", new { roleLabel = label ?? "Civil technologist " + Guid.NewGuid().ToString("N")[..6],
            fromDate = from ?? Mon, throughDate = through ?? Fri, plannedHours = hours, projectDisciplineId = disciplineId })).Json(expect);

    OpenRole Stored(Guid id) => f.Db(db => db.OpenRoles.AsNoTracking().Single(r => r.Id == id));

    /// A new active person reporting to Sam, on the project as a Team Member, with one task in the role's week.
    async Task<(Guid Id, Guid TaskId)> Person(Guid projectId)
    {
        var email = $"open-role-{Guid.NewGuid():N}@hub.test";
        await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var id = data.User(email);
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == id)).SupervisorId = data.User(TestData.Sam);
            db.ProjectMembers.Add(new ProjectMember { ProjectId = projectId, UserId = id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        var task = await data.NewTask(projectId, extra: new { assigneeId = id, estimatedHours = 4m, startDate = Mon, dueDate = Mon });
        return (id, task.G("id"));
    }

    static AllocationEndpoints.CreateBody Proposal((Guid Id, Guid TaskId) person, Guid? roleId, DateOnly? day = null) =>
        new(Guid.NewGuid(), person.Id, AllocationPurpose.Production, day ?? Mon, day ?? Mon, 4, [], [new("Task", person.TaskId, day ?? Mon)], null, roleId);

    [Fact]
    public async Task Create_validates_fields_is_audited_and_refuses_a_stale_edit() // AC-ROLE-01a, -01b, -01d
    {
        var project = await data.Project();
        var civil = data.ProjectDiscipline(project.Id, "Civil");
        var created = await Role(project.Id, label: "Civil technologist", from: new(2026, 10, 19), through: new(2026, 11, 13), hours: 80, disciplineId: civil);
        Assert.Equal(OpenRoleStatus.Open, created.S("status"));
        Assert.Equal("Civil", created.S("disciplineName"));
        Assert.NotNull(created["rowVersion"]);
        var activity = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/activity?itemType=OpenRole")).Json();
        var row = activity["items"]!.AsArray().Single(a => a!.G("itemId") == created.G("id"))!;
        Assert.Equal("Created", row.S("action"));
        var logged = row["changes"]!.AsArray().ToDictionary(c => c!.S("field"), c => c!["new"]!.ToJsonString());
        Assert.Equal("\"Civil technologist\"", logged["RoleLabel"]);
        Assert.Equal("\"2026-10-19\"", logged["FromDate"]);
        Assert.Equal("\"2026-11-13\"", logged["ThroughDate"]);
        Assert.Equal(80m, decimal.Parse(logged["PlannedHours"]));

        var cases = new (object Body, string Field)[]
        {
            (new { roleLabel = "", fromDate = Mon, throughDate = Fri, plannedHours = 8 }, "roleLabel"),
            (new { roleLabel = new string('x', 81), fromDate = Mon, throughDate = Fri, plannedHours = 8 }, "roleLabel"),
            (new { roleLabel = "Backwards", fromDate = Fri, throughDate = Mon, plannedHours = 8 }, "throughDate"),
            (new { roleLabel = "Weekend only", fromDate = new DateOnly(2026, 10, 10), throughDate = new DateOnly(2026, 10, 11), plannedHours = 8 }, "throughDate"),
            (new { roleLabel = "Nothing", fromDate = Mon, throughDate = Fri, plannedHours = 0 }, "plannedHours"),
            (new { roleLabel = "Negative", fromDate = Mon, throughDate = Fri, plannedHours = -5 }, "plannedHours"),
            (new { roleLabel = "Elsewhere", fromDate = Mon, throughDate = Fri, plannedHours = 8, projectDisciplineId = data.ProjectDiscipline((await data.Project()).Id, "Civil") }, "projectDisciplineId"),
        };
        foreach (var (body, field) in cases)
        {
            var refused = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/open-roles", body)).Json(400);
            Assert.NotNull(refused["errors"]![field]);
        }
        Assert.Single(f.Db(db => db.OpenRoles.Where(r => r.ProjectId == project.Id).ToList()));

        var path = $"/api/v1/open-roles/{created.S("id")}";
        var edited = await (await f.As(TestData.Pm).Patch(path, new { roleLabel = "Senior civil technologist" }, created.I("rowVersion"))).Json();
        Assert.Equal(created.I("rowVersion") + 1, edited.I("rowVersion"));
        var stale = await (await f.As(TestData.Pm).Patch(path, new { roleLabel = "Lost edit", plannedHours = 1 }, created.I("rowVersion"))).Json(409);
        Assert.Equal(edited.I("rowVersion"), stale.I("currentRowVersion"));
        Assert.Equal(("Senior civil technologist", 80m), (Stored(created.G("id")).RoleLabel, Stored(created.G("id")).PlannedHours));
        var list = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/open-roles")).Json();
        Assert.True(list.AsArray().Single()!["canManage"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Create_edit_and_cancel_follow_allocation_proposal_permissions() // AC-ROLE-01c, D2
    {
        var project = await data.Project();
        var allowed = new[] { TestData.Pm, TestData.Marc, TestData.Omar, TestData.Admin };
        foreach (var persona in new[] { TestData.Pm, TestData.Marc, TestData.Omar, TestData.Admin, TestData.Lena, TestData.Sam, TestData.Alex, TestData.Jill, TestData.Diane, TestData.Rita })
        {
            var role = await f.As(persona).Post($"/api/v1/projects/{project.Id}/open-roles", new { roleLabel = "Permission check", fromDate = Mon, throughDate = Fri, plannedHours = 8 });
            // The same persona proposing an allocation: refused (403) before validation, or allowed and then invalid (400).
            var proposal = await f.As(persona).Post($"/api/v1/projects/{project.Id}/allocations",
                new AllocationEndpoints.CreateBody(Guid.NewGuid(), Guid.NewGuid(), AllocationPurpose.Production, Mon, Mon, 4, [], [], null));
            Assert.Equal(allowed.Contains(persona) ? HttpStatusCode.Created : HttpStatusCode.Forbidden, role.StatusCode);
            Assert.Equal(allowed.Contains(persona) ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden, proposal.StatusCode);
        }

        var pmRole = await Role(project.Id);
        var leadRole = await Role(project.Id, TestData.Omar);
        await (await f.As(TestData.Omar).Patch($"/api/v1/open-roles/{pmRole.S("id")}", new { note = "Not mine" }, pmRole.I("rowVersion"))).Json(403);
        await (await f.As(TestData.Sam).Patch($"/api/v1/open-roles/{pmRole.S("id")}", new { note = "Not mine" }, pmRole.I("rowVersion"))).Json(403);
        var noted = await (await f.As(TestData.Pm).Patch($"/api/v1/open-roles/{leadRole.S("id")}", new { note = "PM edits any role" }, leadRole.I("rowVersion"))).Json();
        await (await f.As(TestData.Omar).Post($"/api/v1/open-roles/{pmRole.S("id")}/cancel", new { rowVersion = pmRole.I("rowVersion") })).Json(403);
        var cancelled = await (await f.As(TestData.Omar).Post($"/api/v1/open-roles/{leadRole.S("id")}/cancel", new { rowVersion = noted.I("rowVersion"), reason = "Scope dropped" })).Json();
        Assert.Equal(OpenRoleStatus.Cancelled, cancelled.S("status"));
        Assert.NotNull((await (await f.As(TestData.Omar).Post($"/api/v1/open-roles/{leadRole.S("id")}/cancel", new { rowVersion = cancelled.I("rowVersion") })).Json(400))["errors"]!["status"]);
        await (await f.As(TestData.Pm).Patch($"/api/v1/open-roles/{leadRole.S("id")}", new { note = "Too late" }, cancelled.I("rowVersion"))).Json(400);
    }

    [Fact]
    public async Task Changing_roles_on_a_complete_project_needs_a_correction_reason() // A6, P-05: as allocation proposals do
    {
        var project = await data.Project();
        var role = await Role(project.Id);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var root = $"/api/v1/projects/{project.Id}/open-roles";
        var path = $"/api/v1/open-roles/{role.S("id")}";

        foreach (var reason in new[] { null, "", "Late" })
        {
            Assert.NotNull((await (await f.As(TestData.Pm).Post(root, new { roleLabel = "Correction", fromDate = Mon, throughDate = Fri, plannedHours = 8, reason })).Json(400))["errors"]!["reason"]);
            Assert.NotNull((await (await f.As(TestData.Pm).Patch(path, new { note = "Correction", reason }, role.I("rowVersion"))).Json(400))["errors"]!["reason"]);
            Assert.NotNull((await (await f.As(TestData.Pm).Post(path + "/cancel", new { rowVersion = role.I("rowVersion"), reason })).Json(400))["errors"]!["reason"]);
        }
        Assert.Single(f.Db(db => db.OpenRoles.Where(r => r.ProjectId == project.Id).ToList()));
        Assert.Equal((OpenRoleStatus.Open, (string?)null), (Stored(role.G("id")).Status, Stored(role.G("id")).Note));

        var added = await (await f.As(TestData.Pm).Post(root, new { roleLabel = "Correction", fromDate = Mon, throughDate = Fri, plannedHours = 8, reason = "Missed at close-out" })).Json(201);
        var edited = await (await f.As(TestData.Pm).Patch(path, new { note = "Correction", reason = "Hours were booked late" }, role.I("rowVersion"))).Json();
        await (await f.As(TestData.Pm).Post(path + "/cancel", new { rowVersion = edited.I("rowVersion"), reason = "Never staffed" })).Json();
        var history = f.Db(db => db.ActivityLog.Where(a => a.ItemId == added.G("id") || a.ItemId == role.G("id")).Select(a => a.Reason).ToList());
        Assert.Equal(["Hours were booked late", "Missed at close-out", "Never staffed"], history.OfType<string>().Order());
    }

    [Fact]
    public async Task Role_history_reaches_exactly_the_people_who_can_view_the_project() // 036-D1, AC-ROLE-05 R4
    {
        var project = await data.Project(pm: TestData.Marc);
        var role = await Role(project.Id, TestData.Marc);
        await (await f.As(TestData.Marc).Patch($"/api/v1/open-roles/{role.S("id")}", new { note = "Needs drafting software" }, role.I("rowVersion"))).Json();
        var path = $"/api/v1/items/OpenRole/{role.S("id")}/activity";
        foreach (var viewer in new[] { TestData.Marc, TestData.Pm, TestData.Admin }) // the project is not restricted yet: Priya can view it
        {
            var page = await (await f.As(viewer).GetAsync(path)).Json();
            Assert.Equal(["Updated", "Created"], page["items"]!.AsArray().Select(a => a!.S("action")));
            Assert.All(page["items"]!.AsArray(), a => Assert.Equal(role.G("id"), a!.G("itemId")));
        }
        await (await f.As(TestData.Marc).GetAsync($"/api/v1/items/OpenRole/{Guid.NewGuid()}/activity")).Json(404);

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        foreach (var outsider in new[] { TestData.Pm, TestData.Sam })
            await (await f.As(outsider).GetAsync(path)).Json(404);
        foreach (var insider in new[] { TestData.Marc, TestData.Admin })
            Assert.Equal(2, (await (await f.As(insider).GetAsync(path)).Json())["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task Filling_creates_the_allocation_and_marks_the_role_in_one_transaction() // AC-ROLE-03a, -03b, -03d
    {
        var project = await data.Project();
        var person = await Person(project.Id);
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var role = await Role(project.Id);
        var roleId = role.G("id");

        // Outside the role's dates, another project's role, or an unknown role: refused and nothing changes.
        var later = new DateOnly(2026, 10, 13);
        var laterTask = await data.NewTask(project.Id, extra: new { assigneeId = person.Id, estimatedHours = 4m, startDate = later, dueDate = later });
        Assert.NotNull((await (await f.As(TestData.Pm).Post(root, Proposal((person.Id, laterTask.G("id")), roleId, later))).Json(400))["errors"]!["openRoleId"]);
        var elsewhere = await Role((await data.Project()).Id);
        Assert.NotNull((await (await f.As(TestData.Pm).Post(root, Proposal(person, elsewhere.G("id")))).Json(400))["errors"]!["openRoleId"]);
        await (await f.As(TestData.Pm).Post(root, Proposal(person, Guid.NewGuid()))).Json(400);
        Assert.Equal(OpenRoleStatus.Open, Stored(roleId).Status);
        Assert.Equal(OpenRoleStatus.Open, Stored(elsewhere.G("id")).Status);
        Assert.Empty(f.Db(db => db.Allocations.Where(a => a.ProjectId == project.Id).ToList()));

        var filled = await (await f.As(TestData.Pm).Post(root, Proposal(person, roleId))).Json();
        var allocationId = filled.G("id");
        var stored = Stored(roleId);
        Assert.Equal((OpenRoleStatus.Filled, (Guid?)allocationId), (stored.Status, stored.FilledAllocationId));
        Assert.Equal(AllocationStatus.Proposed, f.Db(db => db.Allocations.Single(a => a.Id == allocationId).Status));
        var correlations = f.Db(db => db.ActivityLog.Where(a => (a.ItemId == allocationId && a.Action == "Created") || (a.ItemId == roleId && a.Action == "StatusChanged"))
            .Select(a => a.CorrelationId).ToList());
        Assert.Equal(2, correlations.Count);
        Assert.Single(correlations.Distinct());

        var other = await Person(project.Id);
        var refused = await (await f.As(TestData.Pm).Post(root, Proposal(other, roleId))).Json(409);
        Assert.Equal("open_role_filled", refused.S("code"));
        Assert.Single(f.Db(db => db.Allocations.Where(a => a.ProjectId == project.Id).ToList()));
        var list = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}/open-roles")).Json();
        Assert.Equal(allocationId, list.AsArray().Single(r => r!.G("id") == roleId)!.G("filledAllocationId"));
        await (await f.As(TestData.Pm).Patch($"/api/v1/open-roles/{roleId}", new { note = "Filled roles are fixed" }, stored.RowVersion)).Json(400);
    }

    [Fact]
    public async Task Simultaneous_fills_of_one_role_create_exactly_one_allocation() // AC-ROLE-03c
    {
        var project = await data.Project();
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var first = await Person(project.Id);
        var second = await Person(project.Id);
        for (var round = 0; round < 3; round++)
        {
            var roleId = (await Role(project.Id)).G("id");
            var responses = await Task.WhenAll(
                f.As(TestData.Pm).Post(root, Proposal(first, roleId)),
                f.As(TestData.Marc).Post(root, Proposal(second, roleId)));
            var codes = responses.Select(r => (int)r.StatusCode).Order().ToArray();
            Assert.Equal(new[] { 200, 409 }, codes);
            Assert.Equal("open_role_filled", (await responses.Single(r => r.StatusCode == HttpStatusCode.Conflict).Json(409)).S("code"));
            var winner = (await responses.Single(r => r.IsSuccessStatusCode).Json()).G("id");
            Assert.Equal(winner, Stored(roleId).FilledAllocationId);
            // Release the winner so the next round can reuse both people's task links.
            var row = f.Db(db => db.Allocations.Single(a => a.Id == winner));
            await (await f.As(TestData.Pm).Post($"{root}/{winner}/cancel", new AllocationEndpoints.CancelBody(Guid.NewGuid(), row.RowVersion, "Next round"))).Json();
        }
        Assert.Equal(3, f.Db(db => db.Allocations.Count(a => a.ProjectId == project.Id)));
    }

    [Fact]
    public async Task Declining_or_cancelling_reopens_the_role_and_completing_keeps_it_filled() // AC-ROLE-04a, -04b, -04c
    {
        var project = await data.Project();
        var person = await Person(project.Id);
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var roleId = (await Role(project.Id)).G("id");

        var proposed = await (await f.As(TestData.Pm).Post(root, Proposal(person, roleId))).Json();
        await (await f.As(TestData.Sam).Post($"{root}/{proposed.G("id")}/decline", new AllocationEndpoints.CancelBody(Guid.NewGuid(), proposed.I("rowVersion"), "Use another plan"))).Json();
        var declined = Stored(roleId);
        Assert.Equal((OpenRoleStatus.Open, (Guid?)null), (declined.Status, declined.FilledAllocationId));
        var reopened = f.Db(db => db.ActivityLog.Where(a => a.ItemId == roleId && a.Action == "StatusChanged").OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).First());
        Assert.Contains("\"Filled\"", reopened.Changes);
        Assert.Contains("\"Open\"", reopened.Changes);

        var again = await (await f.As(TestData.Pm).Post(root, Proposal(person, roleId))).Json();
        Assert.Equal(OpenRoleStatus.Filled, Stored(roleId).Status);
        await (await f.As(TestData.Pm).Post($"{root}/{again.G("id")}/cancel", new AllocationEndpoints.CancelBody(Guid.NewGuid(), again.I("rowVersion"), "No longer needed"))).Json();
        Assert.Equal(OpenRoleStatus.Open, Stored(roleId).Status);

        var kept = await (await f.As(TestData.Pm).Post(root, Proposal(person, roleId))).Json();
        var path = $"{root}/{kept.G("id")}";
        var preview = await (await f.As(TestData.Sam).GetAsync(path + "/confirmation-preview")).Json();
        var confirmed = await (await f.As(TestData.Sam).Post(path + "/confirm", new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), kept.I("rowVersion"),
            [.. preview["days"]!.AsArray().Select(d => new AllocationEndpoints.DateVersionInput(DateOnly.Parse(d!.S("date")), d!.I("dateVersion")))], "Covered by the role"))).Json();
        Assert.Equal((OpenRoleStatus.Filled, (Guid?)kept.G("id")), (Stored(roleId).Status, Stored(roleId).FilledAllocationId));
        await (await f.As(TestData.Pm).Post(path + "/complete", new AllocationEndpoints.CancelBody(Guid.NewGuid(), confirmed.I("rowVersion"), "Allocation period closed"))).Json();
        Assert.Equal((OpenRoleStatus.Filled, (Guid?)kept.G("id")), (Stored(roleId).Status, Stored(roleId).FilledAllocationId));
    }

    [Fact]
    public async Task Restricted_project_open_roles_reach_only_people_who_see_the_project() // AC-ROLE-05, R1-R4
    {
        var civil = await data.Discipline("Civil");
        var project = await data.Project(pm: TestData.Marc, tweak: b => b["members"] = new[] { new { userId = data.User(TestData.Jill), roles = new[] { "TeamMember" }, disciplineId = (Guid?)civil } });
        var label = "Hydraulic modeller " + Guid.NewGuid().ToString("N")[..8];
        var role = await Role(project.Id, TestData.Marc, label);
        var roleId = role.G("id");
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var workload = $"/api/v1/workload?from={Mon:yyyy-MM-dd}";

        foreach (var outsider in new[] { TestData.Pm, TestData.Sam, TestData.Alex, TestData.Rita })
        {
            await (await f.As(outsider).GetAsync($"/api/v1/projects/{project.Id}/open-roles")).Json(404);
            await (await f.As(outsider).GetAsync($"/api/v1/projects/{project.Id}/activity")).Json(404);
            await (await f.As(outsider).GetAsync($"/api/v1/items/OpenRole/{roleId}/activity")).Json(404);
            await (await f.As(outsider).Patch($"/api/v1/open-roles/{roleId}", new { note = "Probe" }, role.I("rowVersion"))).Json(404);
            await (await f.As(outsider).Post($"/api/v1/open-roles/{roleId}/cancel", new { rowVersion = role.I("rowVersion") })).Json(404);
            var search = await (await f.As(outsider).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(label)}")).Json();
            search.AsObject().Remove("q"); // the echoed search text
            Assert.DoesNotContain(label, search.ToJsonString(), StringComparison.Ordinal);
            Assert.DoesNotContain(roleId.ToString(), search.ToJsonString(), StringComparison.OrdinalIgnoreCase);
            foreach (var path in new[] { workload, $"/api/v1/workload/export?format=csv&from={Mon:yyyy-MM-dd}" })
            {
                var body = await (await f.As(outsider).GetAsync(path)).Content.ReadAsStringAsync();
                Assert.DoesNotContain(label, body, StringComparison.Ordinal);
                Assert.DoesNotContain(roleId.ToString(), body, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain(project.ProjectNumber, body, StringComparison.Ordinal);
            }
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync(workload)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).GetAsync(workload)).StatusCode);

        foreach (var insider in new[] { TestData.Marc, TestData.Jill, TestData.Admin })
            Assert.Contains((await (await f.As(insider).GetAsync($"/api/v1/projects/{project.Id}/open-roles")).Json()).AsArray(), r => r!.G("id") == roleId);
        foreach (var viewer in new[] { TestData.Marc, TestData.Admin })
            Assert.Contains((await (await f.As(viewer).GetAsync(workload)).Json())["unfilledRoles"]!.AsArray(), r => r!.G("id") == roleId && r!.S("projectNumber") == project.ProjectNumber);
        await (await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{project.Id}/activity?itemType=OpenRole")).Json();

        // R3: open roles raise no notification or email, so nobody outside the project hears of one.
        Assert.False(f.Db(db => db.Notifications.Any(n => n.Title.Contains(label) || (n.Body != null && n.Body.Contains(label)))));
        Assert.False(f.Db(db => db.Emails.Any(m => m.Subject.Contains(label) || m.BodyText.Contains(label))));
    }
}
