using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// §55.3 My Work → No date, packet 051 S07 (FR-BKL-06, AC-BKL-04): the view is the person's Backlog across projects, read
/// through the same task list as Today, Upcoming and Overdue, so it follows the same project access and the same flag.
[Collection("api")]
public sealed class MyWorkTests(HubFactory f)
{
    readonly TestData d = new(f);
    DateOnly Today => DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
    string In(int days) => Today.AddDays(days).ToString("yyyy-MM-dd");

    async Task Backlog(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.backlog", new { value = on })).EnsureSuccessStatusCode();

    [Fact, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [Trait("Spec", "AC-BKL-04")]
    public async Task No_date_lists_the_persons_backlog_across_projects_and_leaves_restricted_projects_out()
    {
        var tester = "nodate." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        (await f.As(tester).GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var me = d.User(tester);
        var alex = d.User(TestData.Alex);
        var pm = f.As(TestData.Pm);

        async Task<Guid> Add(Guid projectId, object? extra = null, string? status = null)
        {
            var id = (await d.NewTask(projectId, TestData.Pm, extra)).G("id");
            if (status is not null) await f.DbAsync(db => db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, status)));
            return id;
        }
        async Task<Guid> Project()
        {
            var p = await d.Project();
            await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember } }).Result.Json(201);
            return p.Id;
        }

        var a = await Project(); var b = await Project(); var c = await Project(); var hidden = await Project();
        var backlog = new List<Guid>
        {
            await Add(a, new { assigneeId = me }), await Add(a, new { assigneeId = me }, TaskStatuses.OnHold),
            await Add(b, new { assigneeId = me }), await Add(c, new { assigneeId = me }),
        };
        // A collaborator's undated task is the person's work too (My Tasks is assignee or collaborator).
        var shared = await Add(b, new { assigneeId = alex });
        (await pm.Post($"/api/v1/tasks/{shared}/collaborators", new { userId = me })).EnsureSuccessStatusCode();
        backlog.Add(shared);
        // Not the person's Backlog: dated, started, someone else's, Cancelled, Complete.
        var dated = new List<Guid> { await Add(a, new { assigneeId = me, dueDate = In(3) }), await Add(b, new { assigneeId = me, startDate = In(10) }), await Add(c, new { assigneeId = me, dueDate = In(-2) }) };
        var others = new List<Guid>
        {
            await Add(a, new { assigneeId = alex }), await Add(b, new { assigneeId = me }, TaskStatuses.InProgress),
            await Add(b, new { assigneeId = me }, TaskStatuses.Cancelled), await Add(c, new { assigneeId = me }, TaskStatuses.Complete),
        };
        var restricted = await Add(hidden, new { assigneeId = me });
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == hidden)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == hidden && m.UserId == me)).RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        foreach (var id in new[] { a, b, c }) await f.Evaluate(id);

        var person = f.As(tester);
        async Task<List<Guid>> Ids(string q) => (await person.GetAsync($"/api/v1/tasks?pageSize=200&mine=true&{q}").Result.Json())["items"]!.AsArray().Select(x => x!.G("id")).ToList();
        async Task<JsonNode> Work() => await person.GetAsync("/api/v1/me/work").Result.Json();

        // Off (FR-BKL-09): My Work rows carry no backlog field, so nothing in the page can show a No date view.
        Assert.All((await Work())["tasks"]!.AsArray(), x => Assert.Null(x!["backlog"]));

        await Backlog(true);
        try
        {
            // AC-BKL-04: every Backlog item of the person, on all three projects, and nothing else.
            Assert.Equal(backlog.Order(), (await Ids("backlog=true")).Order());
            var tasks = (await Work())["tasks"]!.AsArray();
            Assert.Equal(backlog.Order(), tasks.Where(x => x!["backlog"]!.GetValue<bool>()).Select(x => x!.G("id")).Order());
            Assert.Equal(3, (await Ids("backlog=true")).Select(id => tasks.Single(x => x!.G("id") == id)!.G("projectId")).Distinct().Count());
            Assert.DoesNotContain(restricted, await Ids("backlog=true")); // Restricted project: not a member any more, so not visible, as in every other view
            Assert.DoesNotContain(tasks, x => x!.G("id") == restricted);
            Assert.Equal(backlog.Take(2).Order(), (await Ids($"backlog=true&projects={a}")).Order()); // the workspace scope still narrows it

            // None of them is in Today, Upcoming or Overdue, and the dated work still is.
            var today = await Ids($"dueFrom={In(0)}&dueTo={In(0)}&open=true");
            var upcoming = await Ids($"dueFrom={In(1)}&open=true");
            var overdue = await Ids("overdue=true");
            Assert.Empty(backlog.Concat(others).Intersect(today.Concat(upcoming).Concat(overdue)));
            Assert.Contains(dated[0], upcoming);
            Assert.Contains(dated[2], overdue);
        }
        finally { await Backlog(false); }
    }

    [Fact]
    public async Task Work_staff_and_assignments_keep_their_property_names_order_nulls_and_nesting() // the typed records must serialise to the bytes My Work and My Staff already read
    {
        var p = await d.Project(); var quiet = await d.Project(); var pm = f.As(TestData.Pm);
        var alex = d.User(TestData.Alex); var marc = d.User(TestData.Marc);
        Task<JsonNode> Add(object extra) => d.NewTask(p.Id, TestData.Pm, extra);
        await Add(new { name = "Soon", assigneeId = alex, dueDate = In(3) });
        var predecessor = await Add(new { name = "Their work", assigneeId = d.User(TestData.Jill), dueDate = In(-1) });
        await Add(new { name = "Waiting", assigneeId = alex, dueDate = In(5), dependsOn = new[] { predecessor.G("id") } });
        var blocking = await Add(new { name = "Holds others", assigneeId = alex, dueDate = In(4) });
        var successor = await Add(new { name = "Waits on Alex", assigneeId = marc, dependsOn = new[] { blocking.G("id") } });
        var shared = await Add(new { name = "Shared", assigneeId = marc });
        (await pm.Post($"/api/v1/tasks/{shared.S("id")}/collaborators", new { userId = alex })).EnsureSuccessStatusCode();
        var late = await Add(new { name = "Overdue", assigneeId = alex, dueDate = In(-3) });
        var review = await Add(new { name = "Check my markup", assigneeId = marc, reviewerId = alex, requiresReview = true });
        await d.Move(TestData.Marc, review, TaskStatuses.InProgress); await d.Move(TestData.Marc, review, TaskStatuses.ReadyForReview);
        var done = await Add(new { name = "Finished", assigneeId = alex, requiresReview = false });
        await d.Move(TestData.Alex, done, TaskStatuses.InProgress); await d.Move(TestData.Alex, done, TaskStatuses.Complete);
        async Task<JsonNode> Deliverable(string name, Guid owner, Guid? reviewer = null) => await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables",
            new { name, projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), ownerId = owner, reviewerId = reviewer, dueDate = In(6) }).Result.Json(201);
        var owned = await Deliverable("Owned", alex); var inReview = await Deliverable("In review", marc, alex);
        var dueMismatch = await Add(new { name = "Task due after package", assigneeId = alex, deliverableId = owned.G("id"), dueDate = In(8) });
        var inactiveEmail = $"attention-inactive-{Guid.NewGuid():N}@hub.test";
        var inactive = await f.As(inactiveEmail).GetAsync("/api/v1/me").Result.Json();
        var inactiveId = inactive.G("id");
        Assert.True(await f.DbAsync(db => db.Users.Where(x => x.Id == inactiveId).Select(x => x.IsActive).SingleAsync()));
        await f.DbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(x => x.Id == inactiveId);
            user.SupervisorId = alex;
            return await db.SaveChangesAsync();
        });
        // Assign while active through the normal task endpoint; only then make the owner inactive.
        var inactiveTask = await Add(new { name = "Task with inactive owner", assigneeId = inactiveId, dueDate = In(6) });
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == inactiveId)).IsActive = false;
            return await db.SaveChangesAsync();
        });
        await f.DbAsync(db => db.Deliverables.Where(x => x.Id == inReview.G("id")).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, DeliverableStatus.InReview)));
        var decision = await pm.Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Pick the outfall", description = "Options", ownerUserId = alex, requiredByDate = In(2),
            impactLevel = "High", impactDescription = "Drawings wait" }).Result.Json(201);
        var meeting = await pm.Post($"/api/v1/projects/{p.Id}/meetings", new { title = "Coordination", meetingDate = In(0), meetingType = "Coordination" }).Result.Json(201);
        var action = await pm.Post($"/api/v1/meetings/{meeting.S("id")}/actions", new { text = "Send the survey", ownerType = "User", ownerUserId = alex, dueDate = In(1) }).Result.Json(201);
        var milestone = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = In(10) }).Result.Json(201);
        JsonNode attention;
        try
        {
            await f.Evaluate(p.Id);
            attention = (await f.As(TestData.Alex).GetAsync("/api/v1/me/work").Result.Json())["attention"]!.DeepClone();
            var dueWhy = attention["items"]!.AsArray().Single(x => x!.G("itemId") == dueMismatch.G("id") && x.S("ruleId") == "A-16")!;
            Assert.Equal("A-16", dueWhy.S("ruleId"));
            Assert.Equal("Task", dueWhy.S("itemType"));
            Assert.Equal(In(8), dueWhy["why"]!["values"]!.S("taskDue"));
            Assert.Equal(In(6), dueWhy["why"]!["values"]!.S("deliverableDue"));
            var inactiveWhy = attention["items"]!.AsArray().Single(x => x!.G("itemId") == inactiveTask.G("id") && x.S("ruleId") == "A-18")!;
            Assert.Equal("A-18", inactiveWhy.S("ruleId"));
            Assert.Equal("Task", inactiveWhy.S("itemType"));
            Assert.Equal(inactive.S("displayName"), inactiveWhy["why"]!["values"]!.S("person"));
        }
        finally
        {
            await f.DbAsync(async db =>
            {
                var ids = new[] { dueMismatch.G("id"), inactiveTask.G("id") };
                await db.TaskStates.Where(x => ids.Contains(x.TaskId)).ExecuteDeleteAsync();
                await db.Tasks.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync();
                var user = await db.Users.SingleAsync(x => x.Id == inactiveId);
                user.IsActive = true;
                user.SupervisorId = null;
                return await db.SaveChangesAsync();
            });
        }
        await f.Evaluate(p.Id); await f.Evaluate(quiet.Id);

        var work = await f.As(TestData.Alex).GetAsync("/api/v1/me/work").Result.Json();
        Assert.Equal(["person", "readOnly", "today", "attention", "tasks", "reviews", "deliverables", "people", "successors", "decisions", "actions", "projects", "milestones", "completed"],
            work.AsObject().Select(x => x.Key));
        Assert.Equal(["id", "displayName", "jobTitle", "isActive"], work["person"]!.AsObject().Select(x => x.Key));
        Assert.Equal(("Alex Chen", false, In(0)), (work["person"]!.S("displayName"), work["readOnly"]!.GetValue<bool>(), work.S("today")));
        Assert.Equal(["items", "snoozed", "total", "truncated"], work["attention"]!.AsObject().Select(x => x.Key));
        Assert.Contains(work["attention"]!["items"]!.AsArray(), x => x!.G("itemId") == late.G("id"));
        JsonNode[] Here(JsonNode? rows) => [.. rows!.AsArray().Where(x => x!.G("projectId") == p.Id)!]; // other tests leave Alex work on their own projects
        var tasks = Here(work["tasks"]); Assert.Equal(5, tasks.Length); // assignee on four open ones, collaborator on one
        Assert.Contains(tasks, x => x!.G("id") == shared.G("id"));
        Assert.Equal(["tasks", "deliverables"], work["reviews"]!.AsObject().Select(x => x.Key));
        Assert.Equal(review.G("id"), Assert.Single(Here(work["reviews"]!["tasks"]))!.G("id"));
        Assert.Equal(inReview.G("id"), Assert.Single(Here(work["reviews"]!["deliverables"]))!.G("id"));
        Assert.Equal(owned.G("id"), Assert.Single(Here(work["deliverables"]))!.G("id"));
        Assert.Equal(done.G("id"), Assert.Single(Here(work["completed"]))!.G("id"));
        Assert.Equal("Jill Martin", work["people"]![d.User(TestData.Jill).ToString()]!.GetValue<string>()); // the object is keyed by person id
        var waiting = work["successors"]!.AsArray().Single(x => x!.G("id") == successor.G("id"))!;
        Assert.Equal(["id", "key", "name", "status", "dueDate", "assigneeName"], waiting.AsObject().Select(x => x.Key));
        Assert.Equal((successor.G("id"), "Marc Dubois"), (waiting.G("id"), waiting.S("assigneeName"))); Assert.Null(waiting["dueDate"]); // null, not omitted
        var decided = Assert.Single(Here(work["decisions"]))!;
        Assert.Equal(["id", "projectId", "projectNumber", "key", "subject", "status", "requiredByDate", "impactLevel", "role", "isOverdue", "isDueSoon"], decided.AsObject().Select(x => x.Key));
        Assert.Equal((decision.G("id"), p.ProjectNumber, "Owner", In(2), "High"), (decided.G("id"), decided.S("projectNumber"), decided.S("role"), decided.S("requiredByDate"), decided.S("impactLevel")));
        var agreed = Assert.Single(Here(work["actions"]))!;
        Assert.Equal(["id", "projectId", "projectNumber", "key", "meetingId", "meetingTitle", "meetingDate", "text", "ownerType", "ownerTypeLabel", "ownerUserId", "ownerDisciplineId",
            "ownerExternalPartyId", "ownerName", "leadId", "leadName", "noLead", "partyIsClient", "dueDate", "isOverdue", "daysOverdue", "status", "relatedTaskId", "taskKey",
            "relatedDecisionId", "decisionKey", "converted", "rowVersion"], agreed.AsObject().Select(x => x.Key));
        Assert.Equal((action.G("id"), "Alex Chen", "Coordination", In(1)), (agreed.G("id"), agreed.S("ownerName"), agreed.S("meetingTitle"), agreed.S("dueDate"))); Assert.Null(agreed["ownerDisciplineId"]);
        var projects = work["projects"]!.AsArray(); Assert.Equal(2, projects.Count(x => x!.G("id") == p.Id || x.G("id") == quiet.Id));
        var mine = projects.Single(x => x!.G("id") == p.Id)!;
        Assert.Equal(["id", "projectNumber", "name", "status", "roles", "leads", "follow", "computedHealth", "reportedHealth", "nextMilestone"], mine.AsObject().Select(x => x.Key));
        Assert.Equal(["level", "source"], mine["follow"]!.AsObject().Select(x => x.Key)); Assert.Equal(FollowLevel.AllActivity, mine["follow"]!.S("level"));
        Assert.Equal(["id", "key", "name", "date"], mine["nextMilestone"]!.AsObject().Select(x => x.Key));
        Assert.Equal((milestone.G("id"), In(10)), (mine["nextMilestone"]!.G("id"), mine["nextMilestone"]!.S("date")));
        Assert.Equal("Active", mine.S("status")); Assert.Equal(mine.S("computedHealth"), mine.S("reportedHealth")); Assert.NotEqual(Health.Grey, mine.S("computedHealth")); // an active project shows its computed health
        Assert.Empty(mine["leads"]!.AsArray()); Assert.Equal([ProjectRole.TeamMember], mine["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Null(projects.Single(x => x!.G("id") == quiet.Id)!["nextMilestone"]); // null, not omitted
        var due = Assert.Single(work["milestones"]!.AsArray(), x => x!.G("id") == milestone.G("id"))!;
        Assert.Equal(["id", "projectId", "projectNumber", "key", "name", "milestoneType", "date", "status"], due.AsObject().Select(x => x.Key));
        Assert.Equal(("Design Submission", "60% Submission"), (due.S("milestoneType"), due.S("name")));

        var lead = (await f.As(TestData.Marc).GetAsync("/api/v1/me/work").Result.Json())["projects"]!.AsArray().Single(x => x!.G("id") == p.Id)!;
        Assert.Equal(["Civil"], lead["leads"]!.AsArray().Select(x => x!.GetValue<string>())); Assert.Contains("DisciplineLead", lead["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        var seen = await f.As(TestData.Sam).GetAsync($"/api/v1/me/work?userId={alex}").Result.Json(); // Sam supervises Alex
        Assert.True(seen["readOnly"]!.GetValue<bool>());
        Assert.Equal(work.AsObject().Select(x => x.Key), seen.AsObject().Select(x => x.Key));

        var sam = f.As(TestData.Sam);
        var staff = await sam.GetAsync($"/api/v1/staff?projectId={p.Id}").Result.Json();
        Assert.Equal(["scope", "canSeeAll", "tiles", "people"], staff.AsObject().Select(x => x.Key));
        Assert.Equal(("direct", false), (staff.S("scope"), staff["canSeeAll"]!.GetValue<bool>()));
        Assert.Equal(["staff", "assignments", "withOverdue", "withBlocked", "reviewsStalled"], staff["tiles"]!.AsObject().Select(x => x.Key));
        var row = staff["people"]!.AsArray().Single(x => x!.G("id") == alex)!;
        Assert.Equal(["id", "displayName", "jobTitle", "officeId", "isActive", "projects", "roles", "open", "overdue", "blocked", "blocking", "reviews", "reviewsStalled", "deliverablesDue", "lastActivityAt"],
            row.AsObject().Select(x => x.Key));
        Assert.Equal(["pm", "dl", "team", "reviewer", "viewer"], row["roles"]!.AsObject().Select(x => x.Key));
        Assert.True(row["roles"]!.I("team") >= 2); Assert.True(row.I("deliverablesDue") >= 1); Assert.True(row.I("blocking") >= 1); Assert.NotNull(row["lastActivityAt"]);
        Assert.Equal(staff["people"]!.AsArray().Count, staff["tiles"]!.I("staff"));

        var rows = (await sam.GetAsync($"/api/v1/staff/{alex}/assignments").Result.Json()).AsArray();
        var assignment = rows.Single(x => x!.G("projectId") == p.Id)!;
        Assert.Equal(["memberId", "rowVersion", "projectId", "projectNumber", "name", "status", "reportedHealth", "roles", "leads", "primaryDiscipline", "addedAt", "open", "overdue", "nextDue", "canRemove"],
            assignment.AsObject().Select(x => x.Key));
        Assert.Equal((p.ProjectNumber, "Active", 4, 1, true), (assignment.S("projectNumber"), assignment.S("status"), assignment.I("open"), assignment.I("overdue"), assignment["canRemove"]!.GetValue<bool>()));
        Assert.Equal([ProjectRole.TeamMember], assignment["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(["id", "key", "name", "dueDate"], assignment["nextDue"]!.AsObject().Select(x => x.Key));
        Assert.Equal((late.G("id"), In(-3)), (assignment["nextDue"]!.G("id"), assignment["nextDue"]!.S("dueDate")));
        Assert.Null(rows.Single(x => x!.G("projectId") == quiet.Id)!["nextDue"]); // null, not omitted
        var leading = (await f.As(TestData.Lena).GetAsync($"/api/v1/staff/{marc}/assignments?scope=all").Result.Json()).AsArray().Single(x => x!.G("projectId") == p.Id)!;
        Assert.Equal(["Civil"], leading["leads"]!.AsArray().Select(x => x!.GetValue<string>())); Assert.Contains("DisciplineLead", leading["roles"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.False(leading["canRemove"]!.GetValue<bool>());
        // Alex is Sam's report: a review left waiting would be counted as stalled by a later evaluation under another test's clock.
        await f.DbAsync(db => db.Tasks.Where(x => x.Id == review.G("id")).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, TaskStatuses.Cancelled)));
    }
}
