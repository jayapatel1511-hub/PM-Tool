using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 010: copying a project's structure (§27.1, SC-003), reassigning a departing person's work (FR-ADM-02, E-01,
/// §25.7) and moving a milestone earlier without the cascade (E-09). The cascade itself is AC-MS-05 in packet 003's tests.
[Collection("api")]
public sealed class ExtrasTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    Task<JsonNode> Deliverable(Guid projectId, object? extra = null, string discipline = "Civil")
    {
        var body = new Dictionary<string, object?> { ["name"] = "Drawing " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, discipline) };
        return Task.Run(async () =>
        {
            body["deliverableTypeId"] = await d.DeliverableType();
            if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
            return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
        });
    }

    [Fact]
    public async Task Copying_a_structure_brings_everything_but_people_and_dates() // §27.1, SC-003
    {
        var civil = await d.Discipline("Civil");
        var src = await d.Project(tweak: b => b["disciplines"] = new[] { new { disciplineId = civil, leadUserId = (Guid?)null }, new { disciplineId = d.Discipline("Electrical").Result, leadUserId = (Guid?)U(TestData.Omar) } });
        var pm = f.As(TestData.Pm);
        var m1 = await pm.Post($"/api/v1/projects/{src.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-11-02" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{src.Id}/milestones", new { name = "IFC", milestoneType = "IFC", date = "2027-02-01" }).Result.Json(201);
        var d1 = await Deliverable(src.Id, new { ownerId = U(TestData.Alex), milestoneId = m1.G("id"), dueDate = "2026-10-30" });
        await Deliverable(src.Id, new { ownerId = U(TestData.Omar) }, "Electrical");
        var t1 = await d.NewTask(src.Id, TestData.Pm, new { name = "Survey", assigneeId = U(TestData.Alex), dueDate = "2026-10-01", deliverableId = d1.G("id"), estimatedHours = 8 });
        var t2 = await d.NewTask(src.Id, TestData.Pm, new { name = "Design", assigneeId = U(TestData.Jill), dueDate = "2026-10-15" });
        var t3 = await d.NewTask(src.Id, TestData.Pm, new { name = "Check", dueDate = "2026-10-20" });
        var gone = await d.NewTask(src.Id, TestData.Pm, new { name = "Dropped scope" });
        await pm.Post($"/api/v1/tasks/{t2.S("id")}/dependencies", new { predecessorTaskId = t1.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{t3.S("id")}/dependencies", new { predecessorTaskId = t2.G("id") }).Result.Json(201);
        await d.Move(TestData.Pm, gone, TaskStatuses.Cancelled, new { reason = "Out of scope" });

        var copy = await d.Project(activate: false, tweak: b =>
        {
            b["copyFromProjectId"] = src.Id;
            b["members"] = Array.Empty<object>();
            b["disciplines"] = new[] { new { disciplineId = civil, leadUserId = (Guid?)null } }; // Electrical comes only from the copy
        });
        Assert.Equal(ProjectStatus.Setup, copy.Status);
        var (pds, ms, dels, tasks, deps) = await f.DbAsync(async db => (
            await db.ProjectDisciplines.Where(x => x.ProjectId == copy.Id).ToListAsync(),
            await db.Milestones.Where(x => x.ProjectId == copy.Id).OrderBy(x => x.Seq).ToListAsync(),
            await db.Deliverables.Where(x => x.ProjectId == copy.Id).OrderBy(x => x.Seq).ToListAsync(),
            await db.Tasks.Where(x => x.ProjectId == copy.Id).OrderBy(x => x.Seq).ToListAsync(),
            await db.Dependencies.Where(x => x.ProjectId == copy.Id).ToListAsync()));
        Assert.Equal(2, pds.Count); // Civil once, though both the body and the source name it; Electrical from the source
        Assert.All(pds, x => Assert.Null(x.LeadUserId));
        Assert.Equal(new[] { "60% Submission", "IFC" }, ms.Select(x => x.Name));
        Assert.All(ms, x => Assert.Null(x.Date));
        Assert.Equal(2, dels.Count);
        Assert.All(dels, x => { Assert.Null(x.OwnerId); Assert.Null(x.DueDate); Assert.Equal(DeliverableStatus.NotStarted, x.Status); });
        Assert.Equal(ms[0].Id, dels[0].MilestoneId); // links follow the copies
        Assert.Equal(new[] { "Survey", "Design", "Check" }, tasks.Select(x => x.Name)); // the cancelled task stays behind
        Assert.All(tasks, x => { Assert.Null(x.AssigneeId); Assert.Null(x.DueDate); Assert.Equal(TaskStatuses.NotStarted, x.Status); });
        Assert.Equal(dels[0].Id, tasks[0].DeliverableId);
        Assert.Equal(8, tasks[0].EstimatedHours);
        Assert.Equal($"{copy.ProjectNumber}-T0001", tasks[0].Key);
        Assert.Equal(2, deps.Count);
        Assert.Contains(deps, x => x.PredecessorTaskId == tasks[0].Id && x.SuccessorTaskId == tasks[1].Id);
        Assert.Equal(8, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == copy.Id && a.Action == "Copied" && a.ItemType != ItemType.Dependency))); // 1 discipline, 2 milestones, 2 deliverables, 3 tasks

        // A source the creator cannot see is refused and nothing is created.
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            (await pm.Patch($"/api/v1/projects/{src.Id}", new { visibility = Visibility.Restricted }, d.Version(src.Id))).EnsureSuccessStatusCode();
            var number = TestData.Number();
            var refused = await f.As(TestData.Marc).Post("/api/v1/projects", new { projectNumber = number, name = "Copy attempt", clientId = await d.Client(), officeId = await d.Office(), copyFromProjectId = src.Id });
            Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode); // Marc is not on the source's team
            Assert.False(await f.DbAsync(db => db.Projects.AnyAsync(x => x.ProjectNumber == number)));
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }

    [Fact]
    public async Task A_leavers_work_is_listed_and_reassigned_from_one_place() // FR-ADM-02, E-01, §25.7
    {
        var email = $"leaver{Guid.NewGuid().ToString("N")[..6]}@hub.test";
        await f.As(email).GetAsync("/api/v1/me").Result.Json(); // provisioned on first sign-in
        var leaver = U(email);
        var admin = f.As(TestData.Admin);
        var who = await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json();
        (await admin.Patch($"/api/v1/admin/users/{leaver}", new { supervisorId = U(TestData.Sam) }, who[0]!.I("rowVersion"))).EnsureSuccessStatusCode();

        var p1 = await d.Project();
        var p2 = await d.Project();
        var p3 = await d.Project();
        var t1 = await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = leaver });
        var t2 = await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), reviewerId = leaver, requiresReview = true });
        var d1 = await Deliverable(p1.Id, new { ownerId = leaver });
        var d2 = await Deliverable(p2.Id, new { ownerId = U(TestData.Alex), reviewerId = leaver });
        var karenless = await f.As(TestData.Pm).Post($"/api/v1/projects/{p2.Id}/decisions", new { subject = "Pick a pump", description = "Options", ownerUserId = leaver,
            requiredByDate = "2026-10-01", impactLevel = "Low", impactDescription = "Spec waits" }).Result.Json(201);
        var t3 = await d.NewTask(p2.Id, TestData.Pm, new { assigneeId = leaver });
        var elec = d.ProjectDiscipline(p3.Id, "Electrical");
        (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p3.Id}/disciplines/{elec}", new { leadUserId = leaver }, null)).EnsureSuccessStatusCode();
        var t4 = await d.NewTask(p3.Id, TestData.Pm, new { assigneeId = leaver });
        var t5 = await d.NewTask(p3.Id, TestData.Pm, new { assigneeId = leaver });
        var done = await d.NewTask(p3.Id, TestData.Pm, new { assigneeId = leaver });
        await d.Move(TestData.Pm, done, TaskStatuses.InProgress);
        await d.Move(TestData.Pm, done, TaskStatuses.Complete);
        var deact = await admin.GetAsync($"/api/v1/admin/users?q={email}").Result.Json();
        (await admin.Patch($"/api/v1/admin/users/{leaver}", new { isActive = false }, deact[0]!.I("rowVersion"))).EnsureSuccessStatusCode();

        var work = await admin.GetAsync($"/api/v1/users/{leaver}/open-work").Result.Json();
        var items = work["items"]!.AsArray();
        Assert.False(work["person"]!["isActive"]!.GetValue<bool>());
        Assert.Equal(9, items.Count); // the completed task is not open work
        Assert.Equal(3, items.Select(i => i!.S("projectNumber")).Distinct().Count());
        Assert.Equal(new[] { "DecisionOwner", "DeliverableOwner", "DeliverableReviewer", "DisciplineLead", "TaskAssignee", "TaskReviewer" }, items.Select(i => i!.S("kind")).Distinct().Order());

        // Supervisors act for their direct reports only; others are refused.
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Sam).GetAsync($"/api/v1/users/{leaver}/open-work")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).GetAsync($"/api/v1/users/{leaver}/open-work")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/users/{leaver}/open-work")).StatusCode);

        // One by one, then everything else at once.
        await f.As(TestData.Sam).Post($"/api/v1/users/{leaver}/reassign", new { items = new[] { new { kind = "TaskAssignee", id = t1.G("id") } }, toUserId = U(TestData.Jill) }).Result.Json();
        var rest = items.Where(i => i!.S("id") != t1.S("id")).Select(i => new { kind = i!.S("kind"), id = i.G("id") }).ToArray();
        var all = await admin.Post($"/api/v1/users/{leaver}/reassign", new { items = rest, toUserId = U(TestData.Alex) }).Result.Json();
        Assert.Equal(7, all.I("reassigned"));
        Assert.Equal(t2.S("key"), all["skipped"]!.AsArray().Single()!.S("key")); // Alex cannot review his own task (R-02)
        await admin.Post($"/api/v1/users/{leaver}/reassign", new { items = new[] { new { kind = "TaskReviewer", id = t2.G("id") } }, toUserId = U(TestData.Marc) }).Result.Json();
        Assert.Empty((await admin.GetAsync($"/api/v1/users/{leaver}/open-work").Result.Json())["items"]!.AsArray());

        var state = await f.DbAsync(async db => new
        {
            T1 = await db.Tasks.Where(x => x.Id == t1.G("id")).Select(x => x.AssigneeId).FirstAsync(),
            T2 = await db.Tasks.Where(x => x.Id == t2.G("id")).Select(x => x.ReviewerId).FirstAsync(),
            T4 = await db.Tasks.Where(x => x.Id == t4.G("id")).Select(x => x.AssigneeId).FirstAsync(),
            D1 = await db.Deliverables.Where(x => x.Id == d1.G("id")).Select(x => x.OwnerId).FirstAsync(),
            D2 = await db.Deliverables.Where(x => x.Id == d2.G("id")).Select(x => x.ReviewerId).FirstAsync(),
            Dec = await db.Decisions.Where(x => x.Id == karenless.G("id")).Select(x => x.OwnerUserId).FirstAsync(),
            Lead = await db.ProjectDisciplines.Where(x => x.Id == elec).Select(x => x.LeadUserId).FirstAsync(),
            Logged = await db.ActivityLog.CountAsync(a => a.Reason == "Work reassigned from " + email.Split('@')[0] || a.Reason != null && a.Reason.StartsWith("Work reassigned from")),
            PmHeard = await db.Notifications.CountAsync(n => n.UserId == U(TestData.Pm) && n.EventType == NotificationEvents.SupervisorStaffing && n.Title.Contains("reassigned")),
            AlexHeard = await db.Notifications.AnyAsync(n => n.UserId == U(TestData.Alex) && n.EventType == NotificationEvents.TaskAssigned && n.ItemId == t4.G("id")),
        });
        Assert.Equal(U(TestData.Jill), state.T1);
        Assert.Equal(U(TestData.Marc), state.T2);
        Assert.Equal(U(TestData.Alex), state.T4);
        Assert.Equal(U(TestData.Alex), state.D1);
        Assert.Equal(U(TestData.Alex), state.D2);
        Assert.Equal(U(TestData.Alex), state.Dec);
        Assert.Equal(U(TestData.Alex), state.Lead);
        Assert.True(state.Logged >= 9);
        Assert.True(state.PmHeard >= 3);
        Assert.True(state.AlexHeard);
        Assert.Equal("validation", (await admin.Post($"/api/v1/users/{leaver}/reassign", new { items = new[] { new { kind = "TaskAssignee", id = t5.G("id") } }, toUserId = U(TestData.Alex) }).Result.Json(400)).S("code"));
    }

    [Fact]
    public async Task Several_items_to_a_newcomer_in_one_save_add_them_once() // regression: a person added earlier in the same save is found
    {
        var p = await d.Project(pm: TestData.Marc); // the creating PM also leads Civil: added once, not twice
        Assert.Equal(1, await f.DbAsync(db => db.ProjectMembers.CountAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Marc))));
        var email = $"mover{Guid.NewGuid().ToString("N")[..6]}@hub.test";
        await f.As(email).GetAsync("/api/v1/me").Result.Json();
        var t1 = await d.NewTask(p.Id, TestData.Marc, new { assigneeId = U(email) });
        var t2 = await d.NewTask(p.Id, TestData.Marc, new { assigneeId = U(email) });
        var r = await f.As(TestData.Admin).Post($"/api/v1/users/{U(email)}/reassign", new { items = new[] { new { kind = "TaskAssignee", id = t1.G("id") }, new { kind = "TaskAssignee", id = t2.G("id") } }, toUserId = U(TestData.Diane) }).Result.Json();
        Assert.Equal(2, r.I("reassigned"));
        Assert.Equal(1, await f.DbAsync(db => db.ProjectMembers.CountAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Diane) && m.RemovedAt == null)));
    }

    [Fact]
    public async Task Moving_a_milestone_earlier_without_the_cascade_flags_late_deliverables() // E-09, DL-03
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var m = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-11-02" }).Result.Json(201);
        var del = await Deliverable(p.Id, new { milestoneId = m.G("id"), dueDate = "2026-10-30" });
        var preview = await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-10-23", dryRun = true }).Result.Json();
        Assert.Equal(del.S("key"), preview["inconsistent"]!.AsArray().Single()!.S("key"));
        await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-10-23", reason = "Client wants it sooner", rowVersion = m.I("rowVersion") }).Result.Json();
        await f.Evaluate(p.Id);
        Assert.True(await f.DbAsync(db => db.DeliverableStates.AnyAsync(s => s.DeliverableId == del.G("id") && s.IsDateInconsistent)));
    }
}
