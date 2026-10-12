using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 036 dependency types through the API: AC-DEPT-01b–01d, -02b, -05a, -05c and -07, and the StartedAt anchor (D4).
/// Dates follow verification.md §8.2: holiday Wed 2026-11-11, A = Mon 11-09, B = Thu 11-12, C = Mon 11-16.
[Collection("api")]
public sealed class DependencyTypeApiTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<JsonNode> Deliverable(Guid projectId, object extra)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Package " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, "Civil"),
            ["deliverableTypeId"] = await d.DeliverableType(), ["requiresReview"] = false };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    static string TypeError(JsonNode problem) => problem["errors"]!["type"]![0]!.GetValue<string>();

    [Fact]
    public async Task Links_store_each_type_refuse_unknown_types_and_stay_one_per_pair() // AC-DEPT-01b, -01c, -01d
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var a = await d.NewTask(p.Id, TestData.Pm);
        var fs = await d.NewTask(p.Id, TestData.Pm);
        var ss = await d.NewTask(p.Id, TestData.Pm);
        var ff = await d.NewTask(p.Id, TestData.Pm);
        await pm.Post($"/api/v1/tasks/{fs.S("id")}/dependencies", new { predecessorTaskId = a.G("id") }).Result.Json(201); // omitted type: Finish-to-Start
        await pm.Post($"/api/v1/tasks/{ss.S("id")}/dependencies", new { predecessorTaskId = a.G("id"), type = DependencyTypes.StartToStart, lagDays = 2 }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{a.S("id")}/dependencies", new { successorTaskId = ff.G("id"), type = DependencyTypes.FinishToFinish }).Result.Json(201);

        var blocks = (await pm.GetAsync($"/api/v1/tasks/{a.S("id")}/dependencies").Result.Json())["blocks"]!.AsArray();
        string TypeOf(JsonNode t) => blocks.Single(x => x!["task"]!.G("id") == t.G("id"))!.S("type");
        Assert.Equal((DependencyTypes.FinishToStart, DependencyTypes.StartToStart, DependencyTypes.FinishToFinish), (TypeOf(fs), TypeOf(ss), TypeOf(ff)));
        var stored = await f.DbAsync(db => db.Dependencies.Where(x => x.PredecessorTaskId == a.G("id")).ToDictionaryAsync(x => x.SuccessorTaskId, x => (x.DependencyType, x.LagDays)));
        Assert.Equal((DependencyTypes.StartToStart, 2), stored[ss.G("id")]);

        foreach (var bad in new[] { "StartToFinish", "xyz" })
        {
            var refused = await pm.Post($"/api/v1/tasks/{fs.S("id")}/dependencies", new { predecessorTaskId = ss.G("id"), type = bad }).Result.Json(400);
            Assert.Equal(Hub.Api.Text.Get("dep.type"), TypeError(refused));
        }
        Assert.Equal(409, (int)(await pm.Post($"/api/v1/tasks/{ss.S("id")}/dependencies", new { predecessorTaskId = a.G("id"), type = DependencyTypes.FinishToFinish })).StatusCode);
        var kept = await f.DbAsync(db => db.Dependencies.SingleAsync(x => x.SuccessorTaskId == ss.G("id")));
        Assert.Equal((DependencyTypes.StartToStart, 2), (kept.DependencyType, kept.LagDays));

        // The same contract on deliverable links (FR-DEPT-01 covers both link kinds).
        var dp = await Deliverable(p.Id, new { });
        var dsucc = await Deliverable(p.Id, new { });
        var refusedDl = await pm.Post($"/api/v1/deliverables/{dsucc.S("id")}/dependencies", new { predecessorDeliverableId = dp.G("id"), type = "StartToFinish" }).Result.Json(400);
        Assert.Equal(Hub.Api.Text.Get("dep.type"), TypeError(refusedDl));
        await pm.Post($"/api/v1/deliverables/{dsucc.S("id")}/dependencies", new { predecessorDeliverableId = dp.G("id"), type = DependencyTypes.StartToStart, lagDays = 1 }).Result.Json(201);
        Assert.Equal(409, (int)(await pm.Post($"/api/v1/deliverables/{dsucc.S("id")}/dependencies", new { predecessorDeliverableId = dp.G("id"), type = DependencyTypes.FinishToFinish })).StatusCode);
        var link = (await pm.GetAsync($"/api/v1/deliverables/{dsucc.S("id")}").Result.Json())["explicitPredecessors"]!.AsArray().Single()!;
        Assert.Equal((DependencyTypes.StartToStart, 1), (link.S("type"), link.I("lagDays")));
        Assert.False(link["satisfied"]!.GetValue<bool>()); // the predecessor has not started
    }

    [Fact]
    public async Task StartedAt_is_set_once_when_work_first_leaves_Not_Started() // D4, FR-DEPT-02
    {
        var p = await d.Project();
        var saved = f.Clock.Now;
        try
        {
            var del = await Deliverable(p.Id, new { });
            var task = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id"), assigneeId = U(TestData.Alex) });
            Task<(DateTimeOffset? Task, DateTimeOffset? Deliverable)> Started() => f.DbAsync(async db => (
                await db.Tasks.Where(x => x.Id == task.G("id")).Select(x => x.StartedAt).SingleAsync(),
                await db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.StartedAt).SingleAsync()));
            Assert.Equal((null, null), await Started());

            f.Clock.SetDate(2026, 11, 10);
            var first = f.Clock.Now;
            await d.Move(TestData.Pm, task, TaskStatuses.InProgress); // DL-02 also starts the Not Started deliverable
            Assert.Equal((first, first), await Started());

            f.Clock.SetDate(2026, 11, 12);
            await d.Move(TestData.Pm, task, TaskStatuses.OnHold, new { reason = "Waiting on the client" });
            await d.Move(TestData.Pm, task, TaskStatuses.InProgress);
            Assert.Equal((first, first), await Started()); // never moved by later transitions

            var viaTransition = await Deliverable(p.Id, new { });
            await f.As(TestData.Pm).Post($"/api/v1/deliverables/{viaTransition.S("id")}/transition", new { toStatus = DeliverableStatus.InProgress, rowVersion = viaTransition.I("rowVersion") }).Result.Json();
            var issuedStraight = await Deliverable(p.Id, new { });
            await f.As(TestData.Pm).Post($"/api/v1/deliverables/{issuedStraight.S("id")}/issue", new { rowVersion = issuedStraight.I("rowVersion") }).Result.Json();
            var both = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == viaTransition.G("id") || x.Id == issuedStraight.G("id")).Select(x => x.StartedAt).ToListAsync());
            Assert.All(both, s => Assert.Equal(f.Clock.Now, s));
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Evaluator_dependency_lists_deliverable_payload_and_readiness_agree_by_type() // AC-DEPT-05a, -05c, -02b, §8.2
    {
        var admin = f.As(TestData.Admin);
        var pm = f.As(TestData.Pm);
        var office = await d.Office();
        var saved = f.Clock.Now;
        await admin.Post("/api/v1/admin/holidays", new { officeId = office, date = "2026-11-11", name = "Remembrance Day" }).Result.Json(201);
        await admin.Put("/api/v1/admin/settings/working_days_enabled", new { value = true }).Result.Json();
        try
        {
            var p = await d.Project();
            var alex = U(TestData.Alex);
            async Task<(JsonNode P, JsonNode S)> Link(string type, int lag, object pred, object succ)
            {
                var pr = await d.NewTask(p.Id, TestData.Pm, pred);
                var su = await d.NewTask(p.Id, TestData.Pm, succ);
                await pm.Post($"/api/v1/tasks/{su.S("id")}/dependencies", new { predecessorTaskId = pr.G("id"), type, lagDays = lag }).Result.Json(201);
                return (pr, su);
            }
            var l1 = await Link(DependencyTypes.FinishToStart, 0, new { dueDate = "2026-11-13" }, new { assigneeId = alex, startDate = "2026-11-16", dueDate = "2026-11-27" });
            var l2 = await Link(DependencyTypes.FinishToStart, 2, new { dueDate = "2026-11-10" }, new { assigneeId = alex, startDate = "2026-11-12", dueDate = "2026-11-20" });
            var l3 = await Link(DependencyTypes.StartToStart, 0, new { startDate = "2026-11-09", dueDate = "2026-11-20" }, new { assigneeId = alex, startDate = "2026-11-10", dueDate = "2026-11-20" });
            var l4 = await Link(DependencyTypes.StartToStart, 2, new { startDate = "2026-11-10", dueDate = "2026-11-27" }, new { assigneeId = alex, startDate = "2026-11-12", dueDate = "2026-11-27" });
            var l5 = await Link(DependencyTypes.FinishToFinish, 0, new { dueDate = "2026-11-20" }, new { assigneeId = alex, startDate = "2026-11-09", dueDate = "2026-11-19" });
            var l6 = await Link(DependencyTypes.FinishToFinish, 1, new { dueDate = "2026-11-13" }, new { assigneeId = alex, startDate = "2026-11-09", dueDate = "2026-11-16" });
            var links = new[] { l1, l2, l3, l4, l5, l6 };

            // AC-DEPT-02b: L2 and L4 again between deliverables (finish = Issued, start = left Not Started).
            var d2p = await Deliverable(p.Id, new { dueDate = "2026-11-10" });
            var d2s = await Deliverable(p.Id, new { ownerId = alex, startDate = "2026-11-12", dueDate = "2026-11-20" });
            var d4p = await Deliverable(p.Id, new { startDate = "2026-11-10", dueDate = "2026-11-27" });
            var d4s = await Deliverable(p.Id, new { ownerId = alex, startDate = "2026-11-12", dueDate = "2026-11-27" });
            await pm.Post($"/api/v1/deliverables/{d2s.S("id")}/dependencies", new { predecessorDeliverableId = d2p.G("id"), lagDays = 2 }).Result.Json(201);
            await pm.Post($"/api/v1/deliverables/{d4s.S("id")}/dependencies", new { predecessorDeliverableId = d4p.G("id"), type = DependencyTypes.StartToStart, lagDays = 2 }).Result.Json(201);

            // Statuses on 11-10, as the table says: P1 and P2 complete, P3 and P4 started, P5 and both FF successors in progress.
            f.Clock.SetDate(2026, 11, 10);
            foreach (var t in new[] { l1.P, l2.P, l5.P, l5.S, l6.S, l3.P, l4.P }) await d.Move(TestData.Pm, t, TaskStatuses.InProgress);
            foreach (var t in new[] { l1.P, l2.P }) await d.Move(TestData.Pm, t, TaskStatuses.Complete);
            await pm.Post($"/api/v1/deliverables/{d2p.S("id")}/issue", new { rowVersion = d2p.I("rowVersion") }).Result.Json();
            await pm.Post($"/api/v1/deliverables/{d4p.S("id")}/transition", new { toStatus = DeliverableStatus.InProgress, rowVersion = d4p.I("rowVersion") }).Result.Json();

            // Readiness assessments for every successor, created by its owner.
            async Task<string> Assess(string type, Guid id, int version)
            {
                var path = $"/api/v1/projects/{p.Id}/readiness/{type}/{id}";
                await f.As(TestData.Alex).Post(path, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Output", "Criteria")).Result.Json();
                return path;
            }
            var taskPaths = new List<string>();
            foreach (var (_, s) in links) taskPaths.Add(await Assess("Task", s.G("id"), await d.TaskVersion(s)));
            var dlVersions = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == d2s.G("id") || x.Id == d4s.G("id")).ToDictionaryAsync(x => x.Id, x => x.RowVersion));
            var d2Path = await Assess("Deliverable", d2s.G("id"), dlVersions[d2s.G("id")]);
            var d4Path = await Assess("Deliverable", d4s.G("id"), dlVersions[d4s.G("id")]);

            async Task<(bool? Applies, bool? Satisfied)> Predecessor(string path)
            {
                var check = (await f.As(TestData.Alex).GetAsync(path).Result.Json())["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Predecessor)!;
                return (check["applies"]?.GetValue<bool>(), check["satisfied"]?.GetValue<bool>());
            }

            async Task AgreeOn(int day, bool[] expected)
            {
                f.Clock.SetDate(2026, 11, day);
                await f.Evaluate(p.Id);
                var states = await f.DbAsync(db => db.TaskStates.Where(s => s.ProjectId == p.Id).ToDictionaryAsync(s => s.TaskId));
                for (var i = 0; i < links.Length; i++)
                {
                    var (pr, su) = links[i];
                    var why = $"L{i + 1} on 11-{day}";
                    var st = states[su.G("id")];
                    Assert.True(expected[i] == !(st.IsWaiting || st.IsBlocked), $"{why}: evaluator");
                    var dependsOn = (await pm.GetAsync($"/api/v1/tasks/{su.S("id")}/dependencies").Result.Json())["dependsOn"]!.AsArray().Single()!;
                    Assert.True(expected[i] == dependsOn["satisfied"]!.GetValue<bool>(), $"{why}: successor's list");
                    var blocks = (await pm.GetAsync($"/api/v1/tasks/{pr.S("id")}/dependencies").Result.Json())["blocks"]!.AsArray().Single()!;
                    Assert.True(expected[i] == blocks["satisfied"]!.GetValue<bool>(), $"{why}: predecessor's list");
                    var readiness = await Predecessor(taskPaths[i]);
                    if (dependsOn.S("type") == DependencyTypes.FinishToFinish)
                        Assert.False(readiness is (true, false), $"{why}: a Finish-to-Finish link never holds a start (AC-DEPT-05c)");
                    else
                        Assert.True(readiness == (true, expected[i]), $"{why}: readiness {readiness}");
                }
            }
            await AgreeOn(12, [true, false, true, false, true, true]); // date B
            await AgreeOn(16, [true, true, true, true, true, true]);   // date C

            // L2 and L4 between deliverables: blocked on B with the lag date as the flag, clear on C (AC-DEPT-02b).
            async Task Deliverables(int day, bool satisfied)
            {
                f.Clock.SetDate(2026, 11, day);
                await f.Evaluate(p.Id);
                var dstates = await f.DbAsync(db => db.DeliverableStates.Where(s => s.ProjectId == p.Id).ToDictionaryAsync(s => s.DeliverableId));
                var taskStates = await f.DbAsync(db => db.TaskStates.Where(s => s.ProjectId == p.Id).ToDictionaryAsync(s => s.TaskId));
                foreach (var (succ, path, twin, flag) in new[] { (d2s, d2Path, l2.S, $"before {d2p.S("key")} is due 2026-11-13"), (d4s, d4Path, l4.S, $"before {d4p.S("key")} starts 2026-11-13") })
                {
                    var st = dstates[succ.G("id")];
                    Assert.Equal(!satisfied, st.IsBlocked);
                    Assert.False(st.IsWaiting);
                    Assert.Equal((taskStates[twin.G("id")].IsWaiting, taskStates[twin.G("id")].IsBlocked), (st.IsWaiting, st.IsBlocked)); // same as the task link
                    Assert.True(st.IsDateInconsistent);
                    Assert.Contains($"Starts 2026-11-12 {flag}", st.InconsistencyDetail);
                    var payload = (await pm.GetAsync($"/api/v1/deliverables/{succ.S("id")}").Result.Json())["explicitPredecessors"]!.AsArray().Single()!;
                    Assert.Equal(satisfied, payload["satisfied"]!.GetValue<bool>());
                    Assert.True(await Predecessor(path) == (true, satisfied));
                }
            }
            await Deliverables(12, satisfied: false);
            await Deliverables(16, satisfied: true);
        }
        finally
        {
            f.Clock.Now = saved;
            await admin.Put("/api/v1/admin/settings/working_days_enabled", new { value = false }).Result.Json();
            await f.DbAsync(db => db.Holidays.Where(h => h.OfficeId == office && h.Date == new DateOnly(2026, 11, 11)).ExecuteDeleteAsync());
        }
    }

    [Fact]
    public async Task A_copied_structure_keeps_link_types_and_template_links_are_finish_to_start() // AC-DEPT-07
    {
        var src = await d.Project();
        var pm = f.As(TestData.Pm);
        var a = await d.NewTask(src.Id, TestData.Pm, new { name = "Grading design" });
        var b = await d.NewTask(src.Id, TestData.Pm, new { name = "Drainage design" });
        var c = await d.NewTask(src.Id, TestData.Pm, new { name = "Drawings" });
        await pm.Post($"/api/v1/tasks/{b.S("id")}/dependencies", new { predecessorTaskId = a.G("id"), type = DependencyTypes.StartToStart, lagDays = 2 }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{c.S("id")}/dependencies", new { predecessorTaskId = b.G("id"), type = DependencyTypes.FinishToFinish }).Result.Json(201);
        var dp = await Deliverable(src.Id, new { name = "Calcs" });
        var ds = await Deliverable(src.Id, new { name = "Drawing set" });
        await pm.Post($"/api/v1/deliverables/{ds.S("id")}/dependencies", new { predecessorDeliverableId = dp.G("id"), type = DependencyTypes.FinishToFinish, lagDays = 1 }).Result.Json(201);

        var copy = await d.Project(activate: false, tweak: x => { x["copyFromProjectId"] = src.Id; x["members"] = Array.Empty<object>(); });
        var (tasks, deps, dlDeps) = await f.DbAsync(async db => (
            await db.Tasks.Where(x => x.ProjectId == copy.Id).ToDictionaryAsync(x => x.Id, x => x.Name),
            await db.Dependencies.Where(x => x.ProjectId == copy.Id).ToListAsync(),
            await db.DeliverableDependencies.Where(x => x.ProjectId == copy.Id).ToListAsync()));
        Assert.Equal(new[] { ("Drainage design", DependencyTypes.StartToStart, 2), ("Drawings", DependencyTypes.FinishToFinish, 0) },
            deps.Select(x => (tasks[x.SuccessorTaskId], x.DependencyType, x.LagDays)).OrderBy(x => x.Item1));
        Assert.Equal((DependencyTypes.FinishToFinish, 1), (Assert.Single(dlDeps).DependencyType, dlDeps[0].LagDays));

        // Templates carry no link type: an instantiated template's links are Finish-to-Start.
        var tid = await f.DbAsync(db => ReferenceTemplate.Seed(db));
        var leads = new Dictionary<string, string> { ["Civil"] = TestData.Marc, ["Geotechnical"] = TestData.Omar, ["Electrical"] = TestData.Omar, ["Survey"] = TestData.Sam,
            ["Environmental"] = TestData.Diane, ["Project Management"] = TestData.Pm };
        var discs = new List<object>();
        foreach (var (name, lead) in leads) discs.Add(new { disciplineId = await d.Discipline(name), leadUserId = (Guid?)U(lead) });
        var number = TestData.Number();
        var fromTemplate = await pm.Post("/api/v1/projects", new { projectNumber = number, name = "Template " + number, clientId = await d.Client(), officeId = await d.Office(),
            startDate = "2026-10-05", disciplines = discs, templateId = tid }).Result.Json(201);
        var types = await f.DbAsync(db => db.Dependencies.Where(x => x.ProjectId == fromTemplate.G("id")).Select(x => x.DependencyType).ToListAsync());
        Assert.NotEmpty(types);
        Assert.All(types, t => Assert.Equal(DependencyTypes.FinishToStart, t));
    }
}
