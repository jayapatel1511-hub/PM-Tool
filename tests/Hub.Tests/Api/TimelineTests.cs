using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 018: timeline data (FR-001, FR-002, FR-004), who may drag what (FR-003, §8.5.2), a dragged date changing only
/// that item (§12.16, SC-002) and the milestone cascade reaching tasks (FR-005, M-04).
[Collection("api")]
public sealed class TimelineTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task<JsonNode> Deliverable(Guid projectId, object extra)
    {
        var body = new Dictionary<string, object?> { ["name"] = "Drawing " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, "Civil"), ["deliverableTypeId"] = await d.DeliverableType() };
        foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    Task<JsonNode> Timeline(Guid projectId, string as_ = TestData.Pm) => f.As(as_).GetAsync($"/api/v1/projects/{projectId}/timeline").Result.Json();

    async Task ScheduleFlag(bool enabled, Func<Task> check)
    {
        var admin = f.As(TestData.Admin); var old = (await (await admin.GetAsync("/api/v1/me")).Json())["flags"]!["schedule_baseline"]!.GetValue<bool>();
        try { await (await admin.Put("/api/v1/admin/settings/flag.schedule_baseline", new { value = enabled })).Json(); await check(); }
        finally { await (await admin.Put("/api/v1/admin/settings/flag.schedule_baseline", new { value = old })).Json(); }
    }

    async Task HandoffFlag(bool enabled, Func<Task> check)
    {
        var admin = f.As(TestData.Admin);
        var old = (await (await admin.GetAsync("/api/v1/me")).Json())["flags"]!["timeline_handoffs_chain"]!.GetValue<bool>();
        try { await (await admin.Put("/api/v1/admin/settings/flag.timeline_handoffs_chain", new { value = enabled })).Json(); await check(); }
        finally { await (await admin.Put("/api/v1/admin/settings/flag.timeline_handoffs_chain", new { value = old })).Json(); }
    }

    async Task<(Project Project, List<Handoff> Handoffs)> HandoffTimelineFixture()
    {
        var p = await d.Project();
        var source = await Deliverable(p.Id, new { });
        var target = await d.NewTask(p.Id, TestData.Omar, new { assigneeId = U(TestData.Omar) }, "Electrical");
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var rows = HandoffStatus.All.Append(HandoffStatus.Submitted).Select((status, i) => new Handoff {
            ProjectId = p.Id, Seq = i + 1, Key = $"{p.ProjectNumber}-H{i + 1:000}", Title = "Synthetic timeline " + status + i,
            Status = status, SourceDeliverableId = source.G("id"), SourceRowVersion = source.I("rowVersion"),
            SendingDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), ReceivingDisciplineId = d.ProjectDiscipline(p.Id, "Electrical"),
            SendingOwnerId = U(TestData.Marc), ReceivingOwnerId = U(TestData.Omar), TargetTaskId = target.G("id"),
            NeededBy = i == HandoffStatus.All.Length ? today.AddDays(5) : today.AddDays(-2),
            PromisedBy = i == HandoffStatus.All.Length ? null : today.AddDays(1), DeclaredRevision = "A", SourceUrl = "https://example.test/synthetic.pdf",
            IntendedUse = "Synthetic input", AcceptanceCriteria = "Synthetic criteria",
        }).ToList();
        await f.DbAsync(async db => { db.Handoffs.AddRange(rows); return await db.SaveChangesAsync(); });
        return (p, rows);
    }

    [Theory, Trait("Spec", "AC-INT-01"), Trait("Spec", "AC-INT-27"), Trait("flag-off", "timeline_handoffs_chain"), Trait("flag-on", "timeline_handoffs_chain")]
    [InlineData(false)] [InlineData(true)]
    public async Task Project_timeline_handoffs_match_list_visibility_and_rules_without_changing_old_reads_or_cross_project_gantt(bool enabled)
    {
        var (p, handoffs) = await HandoffTimelineFixture();
        JsonNode? before = null;
        await HandoffFlag(false, async () => before = await Timeline(p.Id));
        var versions = f.Db(db => db.Handoffs.Where(h => h.ProjectId == p.Id).ToDictionary(h => h.Id, h => h.RowVersion));
        var logs = f.Db(db => db.ActivityLog.Count(l => l.ProjectId == p.Id));
        await HandoffFlag(enabled, async () => {
            foreach (var who in new[] { TestData.Pm, TestData.Marc, TestData.Omar, TestData.Alex })
            {
                var own = await Timeline(p.Id, who);
                if (!enabled) { Assert.False(own.AsObject().ContainsKey("handoffs")); continue; }
                var list = (await (await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/handoffs?pageSize=200")).Json())["items"]!.AsArray();
                var actual = own["handoffs"]!.AsArray();
                Assert.Equal(handoffs.Where(h => h.Status != HandoffStatus.Cancelled).OrderBy(h => h.NeededBy).ThenBy(h => h.Seq).Select(h => h.Id), actual.Select(h => h!.G("id")));
                foreach (var row in actual)
                {
                    var source = list.Single(h => h!.G("id") == row!.G("id"))!;
                    foreach (var key in new[] { "id", "key", "title", "status", "sendingDisciplineId", "receivingDisciplineId", "neededBy", "promisedBy", "dateMismatch", "isOverdue" })
                        Assert.True(JsonNode.DeepEquals(source[key], row![key]), key);
                    Assert.Equal(source["isOverdue"]!.GetValue<bool>() ? 2 : 0, row!.I("daysOverdue"));
                }
                Assert.Contains(actual, h => h!.S("status") == HandoffStatus.Incorporated);
                Assert.Contains(actual, h => h!["promisedBy"] is null && !h["dateMismatch"]!.GetValue<bool>() && !h["isOverdue"]!.GetValue<bool>());
                own.AsObject().Remove("handoffs");
                foreach (var task in own["tasks"]!.AsArray()) task!.AsObject().Remove("affectedMilestoneIds");
                if (who == TestData.Pm) Assert.True(JsonNode.DeepEquals(before, own), "all preceding project fields remain equal");
                var cross = await (await f.As(who).GetAsync($"/api/v1/timeline?projects={p.Id}")).Json();
                Assert.False(cross["projects"]![0]!["data"]!.AsObject().ContainsKey("handoffs"));
            }
        });
        Assert.Equal(versions.OrderBy(x => x.Key), f.Db(db => db.Handoffs.Where(h => h.ProjectId == p.Id).ToDictionary(h => h.Id, h => h.RowVersion)).OrderBy(x => x.Key));
        Assert.Equal(logs, f.Db(db => db.ActivityLog.Count(l => l.ProjectId == p.Id)));
    }

    [Fact, Trait("Spec", "AC-INT-27")]
    public async Task Handoff_needed_date_overdue_is_suppressed_on_hold_and_deleted_rows_are_excluded()
    {
        var (p, rows) = await HandoffTimelineFixture();
        await f.DbAsync(async db => {
            (await db.Projects.SingleAsync(x => x.Id == p.Id)).Status = ProjectStatus.OnHold;
            (await db.Handoffs.SingleAsync(x => x.Id == rows[0].Id)).DeletedAt = f.Clock.Now;
            return await db.SaveChangesAsync();
        });
        await HandoffFlag(true, async () => {
            var shown = (await Timeline(p.Id))["handoffs"]!.AsArray();
            Assert.DoesNotContain(shown, h => h!.G("id") == rows[0].Id);
            Assert.All(shown, h => { Assert.False(h!["isOverdue"]!.GetValue<bool>()); Assert.Equal(0, h.I("daysOverdue")); });
            Assert.Contains(shown, h => h!["dateMismatch"]!.GetValue<bool>());
        });
    }

    [Theory, Trait("Spec", "AC-INT-27")]
    [InlineData(false)] [InlineData(true)]
    public async Task Handoff_timeline_refuses_an_unreadable_project_and_an_unknown_project(bool enabled)
    {
        var (p, _) = await HandoffTimelineFixture();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await HandoffFlag(enabled, async () => {
            var outsider = f.As(TestData.Rita);
            await (await outsider.GetAsync($"/api/v1/projects/{p.Id}/handoffs")).Json(404);
            await (await outsider.GetAsync($"/api/v1/projects/{p.Id}/timeline")).Json(404);
            await (await outsider.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/timeline")).Json(404);
        });
    }

    [Fact, Trait("Spec", "AC-INT-27")]
    public async Task An_empty_project_has_an_empty_handoff_group_read_only_while_enabled()
    {
        var p = await d.Project();
        await HandoffFlag(true, async () => Assert.Empty((await Timeline(p.Id))["handoffs"]!.AsArray()));
    }

    [Theory, Trait("Spec", "AC-INT-28"), Trait("Spec", "AC-INT-01"), Trait("flag-off", "timeline_handoffs_chain"), Trait("flag-on", "timeline_handoffs_chain")]
    [InlineData(false)] [InlineData(true)]
    public async Task Timeline_uses_stored_affected_milestones_at_the_evaluators_depth_without_recalculating_or_changing_cross_project_reads(bool enabled)
    {
        var p = await d.Project(); var pm = f.As(TestData.Pm); var admin = f.As(TestData.Admin);
        var milestone = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic chain end", milestoneType = "Other", date = "2026-10-23" }).Result.Json(201);
        var target = await Deliverable(p.Id, new { milestoneId = milestone.G("id") });
        var tasks = new List<JsonNode>();
        for (var i = 0; i < 4; i++) tasks.Add(await d.NewTask(p.Id, extra: new { deliverableId = i == 3 ? (Guid?)target.G("id") : null }));
        for (var i = 1; i < tasks.Count; i++) await pm.Post($"/api/v1/tasks/{tasks[i].G("id")}/dependencies", new { predecessorTaskId = tasks[i - 1].G("id") }).Result.Json(201);
        var old = (await (await admin.GetAsync("/api/v1/admin/settings")).Json()).AsArray().Single(x => x!.S("key") == "chain_depth_limit")!["value"]!.DeepClone();
        try
        {
            await (await admin.Put("/api/v1/admin/settings/chain_depth_limit", new { value = 2 })).Json();
            await f.Evaluate(p.Id);
            var stored = f.Db(db => db.TaskStates.Where(s => s.ProjectId == p.Id).ToDictionary(s => s.TaskId, s => s.AffectedMilestoneIds));
            Assert.Empty(stored[tasks[0].G("id")]);
            Assert.All(tasks.Skip(1), t => Assert.Equal(new[] { milestone.G("id") }, stored[t.G("id")]));
            // Change the setting without running evaluation: the Timeline must still return the stored two-hop result.
            await (await admin.Put("/api/v1/admin/settings/chain_depth_limit", new { value = 1 })).Json();
            var logs = f.Db(db => db.ActivityLog.Count(l => l.ProjectId == p.Id));
            var versions = f.Db(db => db.Tasks.Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => t.RowVersion).ToList());
            await HandoffFlag(enabled, async () => {
                var own = (await Timeline(p.Id))["tasks"]!.AsArray();
                Assert.Equal(4, own.Count);
                foreach (var t in own)
                    if (enabled) Assert.Equal(stored[t!.G("id")], t["affectedMilestoneIds"]!.AsArray().Select(x => x!.GetValue<Guid>()));
                    else Assert.False(t!.AsObject().ContainsKey("affectedMilestoneIds"));
                var cross = await (await pm.GetAsync($"/api/v1/timeline?projects={p.Id}")).Json();
                Assert.All(cross["projects"]![0]!["data"]!["tasks"]!.AsArray(), t => Assert.False(t!.AsObject().ContainsKey("affectedMilestoneIds")));
            });
            Assert.Equal(logs, f.Db(db => db.ActivityLog.Count(l => l.ProjectId == p.Id)));
            Assert.Equal(versions, f.Db(db => db.Tasks.Where(t => t.ProjectId == p.Id).OrderBy(t => t.Id).Select(t => t.RowVersion).ToList()));
            await f.DbAsync(async db => { db.TaskStates.RemoveRange(await db.TaskStates.Where(s => s.ProjectId == p.Id).ToListAsync()); return await db.SaveChangesAsync(); });
            await HandoffFlag(true, async () => Assert.All((await Timeline(p.Id))["tasks"]!.AsArray(), t => Assert.Empty(t!["affectedMilestoneIds"]!.AsArray())));
        }
        finally { await (await admin.Put("/api/v1/admin/settings/chain_depth_limit", new { value = old })).Json(); }
    }

    [Theory, Trait("Spec", "AC-INT-01"), Trait("Spec", "AC-INT-17"), Trait("flag-off", "schedule_baseline"), Trait("flag-on", "schedule_baseline")]
    [InlineData(false)] [InlineData(true)]
    public async Task Latest_planned_note_matches_project_and_both_timelines_without_changing_health_or_storing_state(bool enabled)
    {
        var p = await d.Project(); var pm = f.As(TestData.Pm); var target = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(20);
        var milestone = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Latest open milestone", milestoneType = "Other", date = target.AddDays(12) }).Result.Json(201);
        await Deliverable(p.Id, new { dueDate = target.AddDays(7), ownerId = U(TestData.Alex) });
        await d.NewTask(p.Id, extra: new { dueDate = target.AddDays(90) });
        var complete = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Closed milestone", milestoneType = "Other", date = target.AddDays(100) }).Result.Json(201);
        var cancelled = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Cancelled milestone", milestoneType = "Other", date = target.AddDays(110) }).Result.Json(201);
        var deleted = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Deleted milestone", milestoneType = "Other", date = target.AddDays(120) }).Result.Json(201);
        var closed = new List<(Guid Id, string Status)>();
        foreach (var status in new[] { DeliverableStatus.Issued, DeliverableStatus.Accepted, DeliverableStatus.Cancelled })
            closed.Add(((await Deliverable(p.Id, new { dueDate = target.AddDays(60) })).G("id"), status));
        await f.DbAsync(async db => {
            (await db.Projects.SingleAsync(x => x.Id == p.Id)).TargetCompletionDate = target;
            (await db.Milestones.SingleAsync(x => x.Id == complete.G("id"))).IsComplete = true;
            (await db.Milestones.SingleAsync(x => x.Id == cancelled.G("id"))).IsCancelled = true;
            (await db.Milestones.SingleAsync(x => x.Id == deleted.G("id"))).DeletedAt = f.Clock.Now;
            foreach (var (id, status) in closed) (await db.Deliverables.SingleAsync(x => x.Id == id)).Status = status;
            return await db.SaveChangesAsync();
        });
        await f.Evaluate(p.Id);
        var before = await (await pm.GetAsync($"/api/v1/projects/{p.Id}")).Json();
        var health = before["health"]!.ToJsonString(); var version = before.I("rowVersion");
        var logs = await f.DbAsync(db => db.ActivityLog.CountAsync(x => x.ProjectId == p.Id));
        var notices = await f.DbAsync(db => db.Notifications.CountAsync(x => x.ProjectId == p.Id));
        await ScheduleFlag(enabled, async () => {
            foreach (var who in new[] { TestData.Pm, TestData.Jill })
            {
                var client = f.As(who);
                var project = await (await client.GetAsync($"/api/v1/projects/{p.ProjectNumber}")).Json();
                var own = await Timeline(p.Id, who);
                var cross = await (await client.GetAsync($"/api/v1/timeline?projects={p.Id}")).Json();
                foreach (var row in new[] { project, own, cross["projects"]!.AsArray().Single()!["data"]! })
                {
                    Assert.Equal(enabled, row.AsObject().ContainsKey("latestPlanned"));
                    if (!enabled) continue;
                    var note = row["latestPlanned"]!;
                    Assert.Equal(milestone.S("key"), note.S("itemKey")); Assert.Equal(target.AddDays(12).ToString("yyyy-MM-dd"), note.S("date"));
                    Assert.Equal(target.ToString("yyyy-MM-dd"), note.S("targetDate")); Assert.Equal("FR-INT-15", note.S("rule"));
                }
                Assert.Equal(health, project["health"]!.ToJsonString()); Assert.Equal(version, project.I("rowVersion"));
            }
            Assert.Equal(logs, await f.DbAsync(db => db.ActivityLog.CountAsync(x => x.ProjectId == p.Id)));
            Assert.Equal(notices, await f.DbAsync(db => db.Notifications.CountAsync(x => x.ProjectId == p.Id)));
        });
    }

    [Fact, Trait("Spec", "AC-INT-17")]
    public async Task Latest_planned_uses_current_deliverable_dates_on_each_read_and_omits_equal_or_earlier_dates()
    {
        var p = await d.Project(); var pm = f.As(TestData.Pm); var target = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(20);
        var item = await Deliverable(p.Id, new { dueDate = target.AddDays(50) }); var id = item.G("id");
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).TargetCompletionDate = target; return await db.SaveChangesAsync(); });
        await ScheduleFlag(true, async () => {
            await (await pm.Post($"/api/v1/deliverables/{id}/rebaseline", new { reason = "Previous schedule reference", rowVersion = item.I("rowVersion") })).Json();
            var current = (await (await pm.GetAsync($"/api/v1/deliverables/{id}")).Json())["deliverable"]!;
            await (await pm.Patch($"/api/v1/deliverables/{id}", new { dueDate = target.AddDays(12) }, current.I("rowVersion"))).Json();
            async Task Check(int? days)
            {
                var project = await (await pm.GetAsync($"/api/v1/projects/{p.Id}")).Json(); var own = await Timeline(p.Id);
                var cross = await (await pm.GetAsync($"/api/v1/timeline?projects={p.Id}")).Json();
                foreach (var row in new[] { project, own, cross["projects"]!.AsArray().Single()!["data"]! })
                {
                    Assert.Equal(days is not null, row.AsObject().ContainsKey("latestPlanned"));
                    if (days is null) continue;
                    Assert.Equal(item.S("key"), row["latestPlanned"]!.S("itemKey")); Assert.Equal(target.AddDays(days.Value).ToString("yyyy-MM-dd"), row["latestPlanned"]!.S("date"));
                }
            }
            await Check(12);
            current = (await (await pm.GetAsync($"/api/v1/deliverables/{id}")).Json())["deliverable"]!;
            Assert.Equal(target.AddDays(50).ToString("yyyy-MM-dd"), current.S("originalDueDate")); Assert.Equal(current.S("originalDueDate"), current.S("baselineDueDate"));
            await (await pm.Patch($"/api/v1/deliverables/{id}", new { startDate = target.AddDays(13), dueDate = (string?)null }, current.I("rowVersion"))).Json();
            await Check(13);
            foreach (var offset in new[] { 13, 14 })
            {
                var project = await (await pm.GetAsync($"/api/v1/projects/{p.Id}")).Json();
                await (await pm.Patch($"/api/v1/projects/{p.Id}", new { targetCompletionDate = target.AddDays(offset) }, project.I("rowVersion"))).Json();
                await Check(null);
            }
        });
    }

    [Theory, Trait("Spec", "AC-INT-17")]
    [InlineData(ProjectStatus.Active, false)] [InlineData(ProjectStatus.Setup, true)]
    [InlineData(ProjectStatus.Complete, true)] [InlineData(ProjectStatus.Archived, true)] [InlineData(ProjectStatus.Cancelled, true)]
    public async Task Latest_planned_note_is_omitted_without_an_active_project_and_target(string status, bool targetKnown)
    {
        var p = await d.Project(); var target = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(20);
        await Deliverable(p.Id, new { dueDate = target.AddDays(12) });
        await f.DbAsync(async db => { var project = await db.Projects.SingleAsync(x => x.Id == p.Id); project.TargetCompletionDate = targetKnown ? target : null; project.Status = status; return await db.SaveChangesAsync(); });
        await ScheduleFlag(true, async () => {
            Assert.False((await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}")).Json()).AsObject().ContainsKey("latestPlanned"));
            Assert.False((await Timeline(p.Id)).AsObject().ContainsKey("latestPlanned"));
        });
    }

    [Theory, Trait("Spec", "AC-INT-01"), Trait("Spec", "AC-INT-14"), Trait("flag-off", "schedule_baseline"), Trait("flag-on", "schedule_baseline")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Slip_display_is_server_decided_without_changing_slip_filters_or_exports(bool enabled)
    {
        var admin = f.As(TestData.Admin); var pm = f.As(TestData.Pm); var p = await d.Project();
        var previousFlag = (await (await admin.GetAsync("/api/v1/me")).Json())["flags"]!["schedule_baseline"]!.GetValue<bool>();
        var previousThreshold = (await (await admin.GetAsync("/api/v1/admin/settings")).Json()).AsArray().Single(r => r!.S("key") == "slip_display_days")!["value"]!.DeepClone();
        var milestones = new List<Guid>(); var deliverables = new List<Guid>(); var tasks = new List<Guid>();
        foreach (var move in new[] { 2, 4 })
        {
            var milestone = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = $"Moved {move}", milestoneType = "Other", date = "2026-10-23" }).Result.Json(201);
            milestones.Add(milestone.G("id"));
            var del = await Deliverable(p.Id, new { milestoneId = milestone.G("id"), startDate = "2026-10-05", dueDate = "2026-10-16" }); deliverables.Add(del.G("id"));
            var task = await d.NewTask(p.Id, extra: new { startDate = "2026-10-05", dueDate = "2026-10-16", deliverableId = del.G("id") }); tasks.Add(task.G("id"));
            await pm.Post($"/api/v1/milestones/{milestone.G("id")}/change-date", new { newDate = new DateOnly(2026, 10, 23).AddDays(move), reason = "Synthetic schedule check", rowVersion = milestone.I("rowVersion") }).Result.Json();
            (await pm.Patch($"/api/v1/deliverables/{del.G("id")}", new { startDate = new DateOnly(2026, 10, 5).AddDays(move), dueDate = new DateOnly(2026, 10, 16).AddDays(move) }, del.I("rowVersion"))).EnsureSuccessStatusCode();
            (await pm.Patch($"/api/v1/tasks/{task.G("id")}", new { startDate = new DateOnly(2026, 10, 5).AddDays(move), dueDate = new DateOnly(2026, 10, 16).AddDays(move) }, await d.TaskVersion(task))).EnsureSuccessStatusCode();
        }
        var startOnly = await d.NewTask(p.Id, extra: new { startDate = "2026-10-05", dueDate = "2026-10-16" });
        (await pm.Patch($"/api/v1/tasks/{startOnly.G("id")}", new { startDate = "2026-10-09" }, await d.TaskVersion(startOnly))).EnsureSuccessStatusCode();
        await f.Evaluate(p.Id);
        var paths = new[] { $"projects/{p.Id}/timeline", $"timeline?projects={p.Id}", $"projects/{p.Id}/milestones", $"projects/{p.Id}/deliverables?includeTasks=true",
            $"milestones/{milestones[0]}", $"deliverables/{deliverables[0]}", $"projects/{p.Id}/deliverables?indicator=slipped" };
        async Task<JsonNode[]> Reads() => await Task.WhenAll(paths.Select(async path => await (await pm.GetAsync("/api/v1/" + path)).Json()));
        void RemoveShown(JsonNode? node)
        {
            if (node is JsonObject obj) { obj.Remove("slipShown"); foreach (var value in obj.Select(x => x.Value).ToArray()) RemoveShown(value); }
            else if (node is JsonArray array) foreach (var value in array) RemoveShown(value);
        }
        try
        {
            await (await admin.Put("/api/v1/admin/settings/flag.schedule_baseline", new { value = enabled })).Json();
            await (await admin.Put("/api/v1/admin/settings/slip_display_days", new { value = 0 })).Json();
            var zero = await Reads();
            var milestoneCsv = await pm.GetStringAsync($"/api/v1/projects/{p.Id}/milestones/export?format=csv");
            var deliverableCsv = await pm.GetStringAsync($"/api/v1/projects/{p.Id}/deliverables/export?format=csv&indicator=slipped");
            await (await admin.Put("/api/v1/admin/settings/slip_display_days", new { value = 3 })).Json();
            var three = await Reads();
            for (var index = 0; index < 2; index++)
            {
                foreach (var list in new[] { "tasks", "deliverables", "milestones" })
                {
                    var item = three[0][list]!.AsArray().Single(n => n!.G("id") == (list == "tasks" ? tasks[index] : list == "deliverables" ? deliverables[index] : milestones[index]))!;
                    if (enabled) Assert.Equal(index == 1, item["slipShown"]!.GetValue<bool>()); else Assert.False(item.AsObject().ContainsKey("slipShown"));
                }
                Assert.Equal(index == 0 ? 2 : 4, three[2].AsArray().Single(n => n!.G("id") == milestones[index])!.I("slipDays"));
                Assert.Equal(index == 0 ? 2 : 4, three[3].AsArray().Single(n => n!.G("id") == deliverables[index])!["state"]!.I("slipDays"));
            }
            if (enabled)
            {
                Assert.True(three[0]["tasks"]!.AsArray().Single(n => n!.G("id") == startOnly.G("id"))!["slipShown"]!.GetValue<bool>());
                Assert.All(zero[0]["tasks"]!.AsArray(), n => Assert.True(n!["slipShown"]!.GetValue<bool>()));
                Assert.False(three[4]["milestone"]!["slipShown"]!.GetValue<bool>());
                Assert.False(three[4]["deliverables"]![0]!["slipShown"]!.GetValue<bool>());
                Assert.False(three[5]["deliverable"]!["slipShown"]!.GetValue<bool>());
            }
            Assert.Equal(2, three[6].AsArray().Count);
            for (var i = 0; i < paths.Length; i++) { RemoveShown(zero[i]); RemoveShown(three[i]); Assert.True(JsonNode.DeepEquals(zero[i], three[i]), paths[i]); }
            Assert.Equal(milestoneCsv, await pm.GetStringAsync($"/api/v1/projects/{p.Id}/milestones/export?format=csv"));
            Assert.Equal(deliverableCsv, await pm.GetStringAsync($"/api/v1/projects/{p.Id}/deliverables/export?format=csv&indicator=slipped"));
        }
        finally
        {
            await (await admin.Put("/api/v1/admin/settings/flag.schedule_baseline", new { value = previousFlag })).Json();
            await (await admin.Put("/api/v1/admin/settings/slip_display_days", new { value = previousThreshold })).Json();
        }
    }

    static string[] Keys(JsonNode? n) => n!.AsObject().Select(x => x.Key).ToArray();

    [Fact]
    public async Task Both_timelines_keep_their_property_names_order_and_types() // the typed records must serialise to the bytes the Timeline and Gantt pages already read
    {
        var (p, _) = await HandoffTimelineFixture();
        var pm = f.As(TestData.Pm); var target = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(20);
        var a = await Deliverable(p.Id, new { dueDate = target.AddDays(7), startDate = target.AddDays(1) });
        var b = await Deliverable(p.Id, new { dueDate = target.AddDays(9) });
        var first = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = a.G("id"), assigneeId = U(TestData.Alex), startDate = "2026-09-01", dueDate = "2026-09-10" });
        var second = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = b.G("id"), startDate = "2026-09-21", dueDate = "2026-10-02" });
        await pm.Post($"/api/v1/tasks/{second.S("id")}/dependencies", new { predecessorTaskId = first.G("id"), type = DependencyTypes.StartToStart, lagDays = 2 }).Result.Json(201);
        var explicitLink = await Deliverable(p.Id, new { name = "Follows the first" });
        await pm.Post($"/api/v1/deliverables/{explicitLink.S("id")}/dependencies", new { predecessorDeliverableId = a.G("id"), type = DependencyTypes.FinishToFinish, lagDays = 3 }).Result.Json(201);
        var milestone = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Submission", milestoneType = "Design Submission", date = target.AddDays(30) }).Result.Json(201);
        await f.DbAsync(async db => {
            (await db.Projects.SingleAsync(x => x.Id == p.Id)).TargetCompletionDate = target;
            var baselined = await db.Deliverables.SingleAsync(x => x.Id == a.G("id")); baselined.BaselineStartDate = target; baselined.BaselineDueDate = target.AddDays(2);
            (await db.Milestones.SingleAsync(x => x.Id == milestone.G("id"))).BaselineDate = target.AddDays(20);
            return await db.SaveChangesAsync();
        });
        await f.Evaluate(p.Id);

        string[] task = ["id", "key", "name", "deliverableId", "projectDisciplineId", "milestoneId", "startDate", "dueDate", "originalStartDate", "originalDueDate", "status", "progressPct", "assignee", "rowVersion", "createdOn", "isOverdue", "canEditDates", "dueNeedsReason"];
        string[] deliverable = ["id", "key", "name", "startDate", "dueDate", "originalStartDate", "originalDueDate", "status", "rowVersion", "createdOn", "progressPct", "isOverdue", "canEditDates"];
        string[] milestoneKeys = ["id", "key", "name", "date", "isComplete", "status"];
        void Check(JsonNode data, bool schedule, bool handoffs)
        {
            Assert.Equal(new[] { "permissions", "tasks", "dependencies", "deliverableLinks", "deliverables", "milestones" }
                .Concat(schedule ? ["latestPlanned"] : []).Concat(handoffs ? ["handoffs"] : []), Keys(data));
            Assert.Equal(["manageMilestones", "needsReason"], Keys(data["permissions"]));
            Assert.Equal(task.Concat(schedule ? ["slipShown"] : []).Concat(handoffs ? ["affectedMilestoneIds"] : []), Keys(data["tasks"]![0]));
            Assert.Equal(["id", "predecessorTaskId", "successorTaskId", "type", "lagDays", "highlighted"], Keys(data["dependencies"]![0]));
            foreach (var link in data["deliverableLinks"]!.AsArray()) Assert.Equal(["from", "to", "type", "lagDays", "highlighted", "derived"], Keys(link));
            Assert.Equal(2, data["deliverableLinks"]!.AsArray().Count); // the explicit link and the one derived from the task link
            Assert.Equal(schedule ? deliverable.Concat(["slipShown"]) : deliverable, Keys(data["deliverables"]!.AsArray().Single(x => x!.G("id") == explicitLink.G("id"))));
            Assert.Equal(schedule ? deliverable.Concat(["slipShown", "baselineStartDate", "baselineDueDate", "baselineSlipShown"]) : deliverable,
                Keys(data["deliverables"]!.AsArray().Single(x => x!.G("id") == a.G("id")))); // baselines only with the flag and when set
            Assert.Equal(schedule ? milestoneKeys.Concat(["originalDate", "slipShown", "baselineDate", "baselineSlipShown"]) : milestoneKeys, Keys(data["milestones"]![0]));
            if (schedule) Assert.Equal(["date", "itemKey", "targetDate", "rule"], Keys(data["latestPlanned"]));
            if (handoffs) Assert.Equal(["id", "key", "title", "status", "sendingDisciplineId", "receivingDisciplineId", "neededBy", "promisedBy", "dateMismatch", "isOverdue", "daysOverdue"], Keys(data["handoffs"]![0]));
        }
        async Task Both(bool schedule, bool handoffs)
        {
            Check(await Timeline(p.Id), schedule, handoffs);
            var cross = await (await pm.GetAsync($"/api/v1/timeline?projects={p.Id}")).Json();
            Assert.Equal(["projects", "truncated", "max"], Keys(cross));
            Assert.Equal(["project", "data"], Keys(cross["projects"]![0]));
            Assert.Equal(["id", "projectNumber", "name", "status", "startDate", "targetCompletionDate"], Keys(cross["projects"]![0]!["project"]));
            Check(cross["projects"]![0]!["data"]!, schedule, false); // the cross-project view has neither the handoffs nor the chain ids
            Assert.Equal((false, 25), (cross["truncated"]!.GetValue<bool>(), cross.I("max")));
        }
        await Both(false, false);
        await ScheduleFlag(true, () => HandoffFlag(true, () => Both(true, true)));
        await ScheduleFlag(true, () => HandoffFlag(false, () => Both(true, false)));
        await ScheduleFlag(false, () => HandoffFlag(true, () => Both(false, true)));

        var row = (await Timeline(p.Id))["tasks"]!.AsArray().Single(x => x!.G("id") == first.G("id"))!;
        Assert.Equal(JsonValueKind.String, row["createdOn"]!.GetValueKind()); Assert.Equal(JsonValueKind.Number, row["rowVersion"]!.GetValueKind());
        Assert.Equal(JsonValueKind.String, row["assignee"]!.GetValueKind());
        var dep = (await Timeline(p.Id))["dependencies"]![0]!;
        Assert.Equal(JsonValueKind.Number, dep["lagDays"]!.GetValueKind()); Assert.Equal(JsonValueKind.True, dep["highlighted"]!.GetValueKind()); // its predecessor is unfinished and overdue
    }

    [Fact]
    public async Task Tasks_arrows_and_permissions_for_the_timeline() // FR-001, FR-002, FR-003, US2 scenario 3
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var a = await Deliverable(p.Id, new { dueDate = "2026-09-30" });
        var b = await Deliverable(p.Id, new { dueDate = "2026-10-20" });
        var late = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = a.G("id"), assigneeId = U(TestData.Alex), startDate = "2026-09-01", dueDate = "2026-09-10" });
        var next = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = b.G("id"), assigneeId = U(TestData.Alex), startDate = "2026-09-21", dueDate = "2026-10-02" });
        var calm = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = b.G("id"), startDate = "2026-10-05", dueDate = "2026-10-09" });
        await pm.Post($"/api/v1/tasks/{next.S("id")}/dependencies", new { predecessorTaskId = late.G("id") }).Result.Json(201);
        await pm.Post($"/api/v1/tasks/{calm.S("id")}/dependencies", new { predecessorTaskId = next.G("id") }).Result.Json(201);
        await f.Evaluate(p.Id);

        var tl = await Timeline(p.Id);
        var tasks = tl["tasks"]!.AsArray();
        Assert.Equal(3, tasks.Count);
        var lateRow = tasks.Single(x => x!.G("id") == late.G("id"))!;
        Assert.Equal(("2026-09-01", "2026-09-10", a.S("id")), (lateRow.S("startDate"), lateRow.S("dueDate"), lateRow.S("deliverableId")));
        Assert.True(lateRow["isOverdue"]!.GetValue<bool>());
        var deps = tl["dependencies"]!.AsArray();
        Assert.True(deps.Single(x => x!.G("predecessorTaskId") == late.G("id"))!["highlighted"]!.GetValue<bool>()); // unfinished and overdue
        Assert.False(deps.Single(x => x!.G("predecessorTaskId") == next.G("id"))!["highlighted"]!.GetValue<bool>());
        var link = tl["deliverableLinks"]!.AsArray().Single()!; // derived: a task in B follows a task in A
        Assert.Equal((a.S("id"), b.S("id"), true, true), (link.S("from"), link.S("to"), link["derived"]!.GetValue<bool>(), link["highlighted"]!.GetValue<bool>()));
        Assert.Null(link["lagDays"]); // a derived link has no lag of its own

        Assert.True(tl["permissions"]!["manageMilestones"]!.GetValue<bool>());
        Assert.All(tasks, x => Assert.True(x!["canEditDates"]!.GetValue<bool>()));
        var alex = await Timeline(p.Id, TestData.Alex);
        var alexRow = alex["tasks"]!.AsArray().Single(x => x!.G("id") == late.G("id"))!;
        Assert.True(alexRow["canEditDates"]!.GetValue<bool>()); // the assignee, with a reason (T-16)
        Assert.True(alexRow["dueNeedsReason"]!.GetValue<bool>());
        Assert.False(alex["tasks"]!.AsArray().Single(x => x!.G("id") == calm.G("id"))!["canEditDates"]!.GetValue<bool>());
        Assert.False(alex["permissions"]!["manageMilestones"]!.GetValue<bool>());
        var jill = await Timeline(p.Id, TestData.Jill);
        Assert.All(jill["tasks"]!.AsArray(), x => Assert.False(x!["canEditDates"]!.GetValue<bool>()));
        Assert.All(jill["deliverables"]!.AsArray(), x => Assert.False(x!["canEditDates"]!.GetValue<bool>()));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Patch($"/api/v1/tasks/{calm.S("id")}", new { dueDate = "2026-10-12" }, await d.TaskVersion(calm))).StatusCode);
    }

    [Fact]
    public async Task A_dragged_task_changes_only_its_own_dates_and_keeps_a_baseline() // §12.16 US2 scenario 1, FR-004, SC-002
    {
        var p = await d.Project();
        var first = await d.NewTask(p.Id, TestData.Pm, new { startDate = "2026-09-21", dueDate = "2026-09-25" });
        var then = await d.NewTask(p.Id, TestData.Pm, new { startDate = "2026-09-28", dueDate = "2026-10-02" });
        await f.As(TestData.Pm).Post($"/api/v1/tasks/{then.S("id")}/dependencies", new { predecessorTaskId = first.G("id") }).Result.Json(201);
        // What the confirmation saves after a 3-day drag.
        (await f.As(TestData.Pm).Patch($"/api/v1/tasks/{first.S("id")}", new { startDate = "2026-09-24", dueDate = "2026-09-28" }, await d.TaskVersion(first))).EnsureSuccessStatusCode();
        var rows = (await Timeline(p.Id))["tasks"]!.AsArray();
        var moved = rows.Single(x => x!.G("id") == first.G("id"))!;
        Assert.Equal(("2026-09-24", "2026-09-28"), (moved.S("startDate"), moved.S("dueDate")));
        Assert.Equal(("2026-09-21", "2026-09-25"), (moved.S("originalStartDate"), moved.S("originalDueDate"))); // the ghost bar
        var succ = rows.Single(x => x!.G("id") == then.G("id"))!;
        Assert.Equal(("2026-09-28", "2026-10-02"), (succ.S("startDate"), succ.S("dueDate"))); // not moved
        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ItemId == first.G("id") || a.ItemId == then.G("id")).ToListAsync()); // jsonb: filtered here
        Assert.Contains(log, x => x.ItemId == first.G("id") && x.Changes.Contains("DueDate") && x.Changes.Contains("2026-09-28"));
        Assert.DoesNotContain(log, x => x.ItemId == then.G("id") && x.Action != "Created" && x.Changes.Contains("DueDate"));

        var del = await Deliverable(p.Id, new { dueDate = "2026-10-09" });
        var dv = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.RowVersion).FirstAsync());
        (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{del.S("id")}", new { dueDate = "2026-10-19" }, dv)).EnsureSuccessStatusCode(); // US3: moved by 10 days
        var list = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json();
        var row = list.AsArray().Single(x => x!.G("id") == del.G("id"))!;
        Assert.Equal(("2026-10-19", "2026-10-09"), (row.S("dueDate"), row.S("originalDueDate")));
    }

    [Fact]
    public async Task The_milestone_cascade_can_include_tasks() // FR-005, M-04, US4
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var m = await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "60% Submission", milestoneType = "Design Submission", date = "2026-11-02" }).Result.Json(201);
        var del = await Deliverable(p.Id, new { milestoneId = m.G("id"), startDate = "2026-10-01", dueDate = "2026-10-30" });
        var t1 = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id"), startDate = "2026-10-05", dueDate = "2026-10-16" });
        var t2 = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id"), dueDate = "2026-10-28" });
        var other = await d.NewTask(p.Id, TestData.Pm, new { dueDate = "2026-10-28" });
        var preview = await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-11-16", cascadeDeliverables = true, includeTasks = true, dryRun = true }).Result.Json();
        Assert.Equal(2, preview["tasks"]!.AsArray().Count);
        await pm.Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2026-11-16", reason = "Client extension", cascadeDeliverables = true, includeTasks = true, rowVersion = m.I("rowVersion") }).Result.Json();
        var (dl, a, b, o, logs) = await f.DbAsync(async db => (
            await db.Deliverables.FirstAsync(x => x.Id == del.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == t1.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == t2.G("id")),
            await db.Tasks.FirstAsync(x => x.Id == other.G("id")),
            await db.ActivityLog.Where(x => x.Action == "Cascade" && (x.ItemId == t1.G("id") || x.ItemId == t2.G("id") || x.ItemId == del.G("id"))).CountAsync()));
        Assert.Equal((new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 13)), (dl.StartDate!.Value, dl.DueDate!.Value)); // +14 days
        Assert.Equal((new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 30)), (a.StartDate!.Value, a.DueDate!.Value));
        Assert.Equal(new DateOnly(2026, 11, 11), b.DueDate);
        Assert.Equal(new DateOnly(2026, 10, 28), o.DueDate); // not in the cascade, not moved
        Assert.Equal(3, logs); // one log row per shifted item
    }
}
