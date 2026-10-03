using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 009: global search and key lookup (AC-SRCH-01..03, FR-SRCH-01/02, §18.1), the list filtering model and exports
/// (§18.3, FR-007, FR-010), the standard reports (FR-RPT-01, FR-ASG-08, §19, SC-004) and readable history (AC-AUD-01/02).
[Collection("api")]
public sealed class SearchReportsTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    static string Token() => "Zq" + Guid.NewGuid().ToString("N")[..6];

    Task<JsonNode> Search(string as_, string q, string extra = "") => f.As(as_).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(q)}{extra}").Result.Json();

    static JsonArray Group(JsonNode r, string g) => r["groups"]![g]!.AsArray();

    [Fact]
    public async Task Key_lookup_grouped_results_archived_toggle_and_visibility() // AC-SRCH-01, AC-SRCH-02, FR-SRCH-01, FR-SRCH-02, §18.1
    {
        var word = Token();
        var p = await d.Project(tweak: b => b["name"] = $"{word} Dundurn Roads");
        var t = await d.NewTask(p.Id, TestData.Pm, new { name = $"{word} drainage review" });
        var del = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = $"{word} drawings", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = $"{word} 60% submission", milestoneType = "Design Submission", date = "2026-11-02" }).Result.Json(201);
        var karen = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/external-parties", new { name = "Karen Li", isClient = true }).Result.Json(201);
        await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/decisions", new { subject = $"{word} pavement", description = "Options", ownerExternalPartyId = karen.G("id"),
            requiredByDate = "2026-10-01", impactLevel = "High", impactDescription = "Drawings wait" }).Result.Json(201);

        var r = await Search(TestData.Alex, word);
        Assert.Equal(p.ProjectNumber, Group(r, "projects").Single()!.S("projectNumber"));
        Assert.Equal(t.S("key"), Group(r, "tasks").Single()!.S("key"));
        Assert.Equal(del.S("key"), Group(r, "deliverables").Single()!.S("key"));
        Assert.Single(Group(r, "milestones"));
        Assert.Single(Group(r, "decisions"));
        Assert.Null(r["exact"]);

        // Key lookup opens the item; a project number opens the project.
        var byKey = await Search(TestData.Alex, t.S("key").ToLowerInvariant());
        Assert.Equal(("Task", t.S("id"), p.ProjectNumber), (byKey["exact"]!.S("type"), byKey["exact"]!.S("id"), byKey["exact"]!.S("projectNumber")));
        Assert.Equal(t.S("key"), Group(byKey, "tasks")[0]!.S("key")); // exact key ranks first
        Assert.Equal("Project", (await Search(TestData.Alex, p.ProjectNumber))["exact"]!.S("type"));
        Assert.Null((await Search(TestData.Alex, $"{p.ProjectNumber}-T9999"))["exact"]);

        // Archived and cancelled projects only on request.
        var gone = await d.Project(tweak: b => b["name"] = $"{word} closed job");
        await f.As(TestData.Pm).Post($"/api/v1/projects/{gone.Id}/transition", new { toStatus = "Cancelled", reason = "Client cancelled the work", rowVersion = d.Version(gone.Id) }).Result.Json();
        Assert.Single(Group(await Search(TestData.Alex, word), "projects"));
        Assert.Equal(2, Group(await Search(TestData.Alex, word, "&includeArchived=true"), "projects").Count);

        // One type with paging for the results page.
        var page = await Search(TestData.Alex, word, "&type=tasks&limit=50");
        Assert.Equal(new[] { "tasks" }, page["groups"]!.AsObject().Select(x => x.Key));
        Assert.Equal(1, page["counts"]!.AsObject()["tasks"]!.GetValue<int>());

        // People: who may open whose My Work.
        var people = Group(await Search(TestData.Alex, "Diane"), "people");
        Assert.Equal("Diane Roy", people[0]!.S("displayName"));
        Assert.False(people[0]!["canViewWork"]!.GetValue<bool>());
        Assert.True(Group(await Search(TestData.Lena, "Diane"), "people")[0]!["canViewWork"]!.GetValue<bool>()); // Executive
        Assert.True(Group(await Search(TestData.Sam, "Alex Chen"), "people")[0]!["canViewWork"]!.GetValue<bool>()); // his direct report
    }

    [Fact]
    public async Task Restricted_items_never_appear_in_search() // §18.1, AC-PERM-05
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            var word = Token();
            var p = await d.Project(tweak: b => b["name"] = $"{word} confidential");
            await d.NewTask(p.Id, TestData.Pm, new { name = $"{word} task" });
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            var outsider = await Search(TestData.Jill, word);
            Assert.Empty(Group(outsider, "projects"));
            Assert.Empty(Group(outsider, "tasks"));
            Assert.Null((await Search(TestData.Jill, p.ProjectNumber))["exact"]);
            Assert.Single(Group(await Search(TestData.Alex, word), "tasks")); // a member
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    }

    [Fact]
    public async Task Filters_combine_links_reproduce_and_exports_hold_the_filtered_rows() // §18.3, AC-SRCH-03, FR-007, FR-010
    {
        var p = await d.Project();
        var a = await d.NewTask(p.Id, TestData.Pm, new { name = "Survey, \"phase 1\"", assigneeId = U(TestData.Alex), dueDate = "2026-10-05" });
        var b = await d.NewTask(p.Id, TestData.Pm, new { name = "Road profile", assigneeId = U(TestData.Alex), dueDate = "2026-10-06" });
        var c = await d.NewTask(p.Id, TestData.Pm, new { name = "Traffic count", assigneeId = U(TestData.Jill), dueDate = "2026-10-07" });
        await d.Move(TestData.Alex, b, TaskStatuses.InProgress);
        await d.Move(TestData.Jill, c, TaskStatuses.InProgress);
        var query = $"status={Uri.EscapeDataString("Not Started,In Progress")}&assigneeId={U(TestData.Alex)}";

        // OR within a field, AND across fields; the same link gives another user the same rows.
        var mine = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks?{query}").Result.Json();
        Assert.Equal(new[] { a.S("key"), b.S("key") }, mine["items"]!.AsArray().Select(x => x!.S("key")));
        var theirs = await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{p.Id}/tasks?{query}").Result.Json();
        Assert.Equal(mine["items"]!.AsArray().Select(x => x!.S("id")), theirs["items"]!.AsArray().Select(x => x!.S("id")));

        var csv = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks/export?format=csv&{query}");
        Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
        var lines = Encoding.UTF8.GetString(bytes[3..]).Split("\r\n", StringSplitOptions.RemoveEmptyEntries).SelectMany(l => l.Split('\n', StringSplitOptions.RemoveEmptyEntries)).ToList();
        Assert.StartsWith("Key,Task,Deliverable,Discipline,Assignee", lines[0]);
        Assert.Equal(3, lines.Count); // header and the two filtered rows
        Assert.Contains("\"Survey, \"\"phase 1\"\"\"", lines[1]);

        var xlsx = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks/export?format=xlsx&{query}");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
        using var zip = new ZipArchive(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync()));
        string Part(string name) { using var r = new StreamReader(zip.GetEntry(name)!.Open()); return r.ReadToEnd(); }
        var sheet = Part("xl/worksheets/sheet1.xml");
        Assert.Contains("state=\"frozen\"", sheet); // frozen header row
        Assert.Contains("s=\"2\"", sheet); // due dates typed as dates
        var parameters = Part("xl/worksheets/sheet2.xml");
        Assert.Contains("Alex Chen", parameters); // the assignee filter, by name
        Assert.Contains("Generated", parameters);

        // Exports are recorded, contents are not.
        var logged = await f.DbAsync(db => db.ActivityLog.Where(x => x.ProjectId == p.Id && x.ItemType == ItemType.Report && x.Action == "Exported").ToListAsync());
        Assert.Equal(2, logged.Count);
        Assert.All(logged, x => { Assert.Equal($"{p.ProjectNumber}-tasks", x.ItemKey); Assert.DoesNotContain("Road profile", x.ItemName); Assert.Equal(U(TestData.Pm), x.ActorUserId); });

        // The registers export the same way.
        foreach (var list in new[] { "deliverables", "decisions", "milestones", "activity" })
            Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/{list}/export?format=xlsx")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/export?format=csv&ids={p.Id}")).StatusCode);
    }

    [Fact]
    public async Task Restricted_filter_ids_are_not_resolved_in_another_projects_export_parameters()
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true });
        try
        {
            var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
            var hiddenDeliverable = await f.As(TestData.Pm).Post($"/api/v1/projects/{hidden.Id}/deliverables", new
            {
                name = "Confidential grading package", projectDisciplineId = d.ProjectDiscipline(hidden.Id, "Civil"), deliverableTypeId = await d.DeliverableType(),
            }).Result.Json(201);
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
            var visible = await d.Project();

            var export = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{visible.Id}/tasks/export?format=xlsx&deliverableId={hiddenDeliverable.G("id")}");
            Assert.Equal(HttpStatusCode.OK, export.StatusCode);
            using var zip = new ZipArchive(new MemoryStream(await export.Content.ReadAsByteArrayAsync()));
            using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml")!.Open());
            var parameters = await reader.ReadToEndAsync();
            Assert.DoesNotContain("Confidential grading package", parameters);
            Assert.DoesNotContain(hiddenDeliverable.S("key"), parameters);
            Assert.Contains(hiddenDeliverable.S("id"), parameters);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }); }
    }

    [Fact]
    public async Task A_report_reads_at_most_one_row_past_the_export_cap()
    {
        var p = await d.Project();
        var cap = Hub.Api.Infrastructure.Export.MaxRows;
        var prefix = "00000000-" + p.Id.ToString()[9..24];
        var first = Guid.Parse(prefix + "000000000001");
        var last = Guid.Parse(prefix + (cap + 2).ToString("D12"));
        try
        {
            await f.DbAsync(db => db.Database.ExecuteSqlRawAsync(
                "INSERT INTO hub.project_health_snapshot (id, project_id, snapshot_date, computed_health, reported_health, inputs) " +
                "SELECT ({2} || lpad(s::text, 12, '0'))::uuid, {0}, DATE '1900-01-01' + s, 'Green', 'Green', jsonb_build_object() FROM generate_series(1, {1}) s", p.Id, cap + 2, prefix));
            var report = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/health-history?projectId={p.Id}&from=1900-01-01&to=2199-12-31").Result.Json();
            Assert.Equal(cap + 1, report.I("total")); // enough to know it is over the export cap, never the whole scope in memory
            Assert.True(report["truncated"]!.GetValue<bool>());
            Assert.True(report["totalIsLowerBound"]!.GetValue<bool>());
            var small = await (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/health-history?projectId={p.Id}&from=1900-01-02&to=1900-01-02")).Json();
            Assert.Equal(1, small.I("total"));
            Assert.False(small["totalIsLowerBound"]!.GetValue<bool>());
        }
        finally
        {
            await f.DbAsync(db => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM hub.project_health_snapshot WHERE project_id = {0} AND id BETWEEN {1} AND {2}", p.Id, first, last));
        }
    }

    [Fact]
    public async Task Blocked_report_filters_decision_sources_before_the_export_cap_and_exports_every_match()
    {
        var p = await d.Project();
        var client = f.As(TestData.Pm);
        var cap = Hub.Api.Infrastructure.Export.MaxRows + 1;
        var manual = await d.NewTask(p.Id, TestData.Pm, new { name = "Survey hold", dueDate = "2026-09-01" });
        await (await client.Post($"/api/v1/tasks/{manual.G("id")}/block", new
        {
            type = BlockType.Information, reason = "Wait for survey", rowVersion = await d.TaskVersion(manual),
        })).Json();
        var blocked = await d.NewTask(p.Id, TestData.Pm, new { name = "Decision hold", dueDate = "2026-10-01" });
        foreach (var (target, requiredBy) in new[] { (manual, "2026-09-30"), (blocked, "2026-09-10") })
            await (await client.Post($"/api/v1/projects/{p.Id}/decisions", new
            {
                subject = "Approve layout", description = "Layout approval", ownerUserId = U(TestData.Pm), requiredByDate = requiredBy,
                impactLevel = Impact.High, impactDescription = "Layout waits", links = new[] { new { targetType = ItemType.Task, targetId = target.G("id"), relation = ItemRelation.BlockedByDecision } },
            })).Json(201);
        await f.Evaluate(p.Id);
        var manualState = await f.DbAsync(db => db.TaskStates.SingleAsync(x => x.TaskId == manual.G("id")));
        var manualBlockers = JsonNode.Parse(manualState.BlockedBy)!.AsArray();
        Assert.True(manualState.IsBlocked);
        Assert.Contains(manualBlockers, x => x!.S("type") == "manual" && x["blocking"]!.GetValue<bool>());
        Assert.Contains(manualBlockers, x => x!.S("type") == "decision" && !x["blocking"]!.GetValue<bool>());
        var decisionState = await f.DbAsync(db => db.TaskStates.SingleAsync(x => x.TaskId == blocked.G("id")));
        var decisionBlocker = Assert.Single(JsonNode.Parse(decisionState.BlockedBy)!.AsArray())!;
        Assert.Equal("decision", decisionBlocker.S("type"));
        Assert.True(decisionBlocker["blocking"]!.GetValue<bool>());

        var legacyId = Guid.NewGuid();
        var legacySeq = cap + 101;
        var legacyKey = $"{p.ProjectNumber}-T{legacySeq}";
        try
        {
            await f.DbAsync(async db =>
            {
                // The indexed 50,001-row fixture gets a bounded setup timeout; API reads retain their normal timeout.
                db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
                // Clone valid API rows and evaluated states; the early sources have only a manual blocker and a false decision.
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO hub.task
                    SELECT (jsonb_populate_record(NULL::hub.task, to_jsonb(t) || jsonb_build_object(
                        'id', gen_random_uuid(), 'seq', s + 100, 'key', {1} || '-T' || (s + 100)::text))).*
                    FROM hub.task t CROSS JOIN generate_series(1, {2}) s WHERE t.id = {0}
                    """, manual.G("id"), p.ProjectNumber, cap);
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO hub.task_state
                    SELECT (jsonb_populate_record(NULL::hub.task_state, to_jsonb(st) || jsonb_build_object('task_id', t.id))).*
                    FROM hub.task_state st CROSS JOIN hub.task t
                    WHERE st.task_id = {0} AND t.project_id = {1} AND t.seq BETWEEN 101 AND {2}
                    """, manual.G("id"), p.Id, cap + 100);
                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO hub.task
                    SELECT (jsonb_populate_record(NULL::hub.task, to_jsonb(t) || jsonb_build_object('id', {1}, 'seq', {2}, 'key', {3}))).*
                    FROM hub.task t WHERE t.id = {0}
                    """, blocked.G("id"), legacyId, legacySeq, legacyKey);
                return await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO hub.task_state
                    SELECT (jsonb_populate_record(NULL::hub.task_state, to_jsonb(st) || jsonb_build_object(
                        'task_id', {1}, 'blocked_by', jsonb_build_array((st.blocked_by -> 0) - 'blocking')))).*
                    FROM hub.task_state st WHERE st.task_id = {0}
                    """, blocked.G("id"), legacyId);
            });
            Assert.Equal(cap + 3, await f.DbAsync(db => db.TaskStates.CountAsync(x => x.ProjectId == p.Id && x.IsBlocked)));
            Assert.False(await f.DbAsync(db => db.Tasks.Where(x => x.ProjectId == p.Id).OrderBy(x => x.DueDate).ThenBy(x => x.Seq)
                .Take(cap).AnyAsync(x => x.Id == blocked.G("id") || x.Id == legacyId)));

            var query = $"projectId={p.Id}&blockerType=decision";
            var report = await (await client.GetAsync($"/api/v1/reports/blocked-tasks?{query}")).Json();
            var expectedKeys = new[] { blocked.S("key"), legacyKey };
            Assert.Equal(expectedKeys, report["rows"]!.AsArray().Select(x => x!.S("key"))); // neither match may disappear behind the raw source cap
            Assert.Equal(2, report.I("total"));
            Assert.False(report["truncated"]!.GetValue<bool>());
            Assert.False(report["totalIsLowerBound"]!.GetValue<bool>());
            var csv = await client.GetAsync($"/api/v1/reports/blocked-tasks/export?format=csv&{query}");
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Equal("text/csv", csv.Content.Headers.ContentType!.MediaType);
            var lines = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync()).TrimStart('\uFEFF')
                .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length);
            Assert.Equal(expectedKeys, lines.Skip(1).Select(line => line.Split(',')[0])); // complete filtered CSV, no manual/false-decision sources
        }
        finally
        {
            // Task-state clones are removed by the task FK's cascade; keep both API seed tasks.
            await f.DbAsync(async db =>
            {
                db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
                return await db.Database.ExecuteSqlRawAsync(
                    "DELETE FROM hub.task WHERE project_id = {0} AND seq BETWEEN 101 AND {1}", p.Id, legacySeq);
            });
        }
    }

    [Fact]
    public async Task Issue_report_prioritises_high_severity_before_the_uuid_source_cap()
    {
        var p = await d.Project();
        var cap = Hub.Api.Infrastructure.Export.MaxRows + 1;
        var high = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/issues", new
        {
            title = "Urgent site access", description = "Access is blocked", severity = Impact.High, dateRaised = "2026-09-01", targetResolutionDate = "2026-09-01",
        })).Json(201);
        var prefix = "00000000-" + p.Id.ToString()[9..24];
        try
        {
            await f.DbAsync(async db =>
            {
                db.Database.SetCommandTimeout(TimeSpan.FromMinutes(2));
                return await db.Database.ExecuteSqlRawAsync("""
                INSERT INTO hub.issue
                SELECT (jsonb_populate_record(NULL::hub.issue, to_jsonb(i) || jsonb_build_object(
                    'id', ({1} || lpad(s::text, 12, '0'))::uuid, 'seq', s + 1, 'key', {2} || '-I' || (s + 1)::text,
                    'severity', 'Low', 'target_resolution_date', DATE '2026-09-30'))).*
                FROM hub.issue i CROSS JOIN generate_series(1, {3}) s WHERE i.id = {0}
                """, high.G("id"), prefix, p.ProjectNumber, cap);
            });
            Assert.Equal(cap + 1, await f.DbAsync(db => db.Issues.CountAsync(x => x.ProjectId == p.Id)));
            Assert.False(await f.DbAsync(db => db.Issues.Where(x => x.ProjectId == p.Id).OrderBy(x => x.Id).Take(cap).AnyAsync(x => x.Id == high.G("id"))));

            var report = await (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/open-issues-high-risks?projectId={p.Id}")).Json();
            Assert.Equal(high.G("id"), report["rows"]![0]!.G("id")); // the highest-priority issue must survive the source cap
            Assert.Equal(Impact.High, report["rows"]![0]!.S("severity"));
            Assert.Equal(cap, report.I("total"));
            Assert.True(report["truncated"]!.GetValue<bool>());
            Assert.True(report["totalIsLowerBound"]!.GetValue<bool>());
        }
        finally
        {
            await f.DbAsync(db => db.Database.ExecuteSqlRawAsync(
                "DELETE FROM hub.issue WHERE project_id = {0} AND seq BETWEEN 2 AND {1}", p.Id, cap + 1));
        }
    }

    [Fact]
    public async Task Report_parameters_name_only_projects_the_caller_can_see()
    {
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true });
        try
        {
            var hidden = await d.Project();
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).EnsureSuccessStatusCode();
            var visible = await d.Project();
            var report = await f.As(TestData.Diane).GetAsync($"/api/v1/reports/stale-work?projectId={hidden.Id},{visible.Id}").Result.Json();
            var parameters = report["parameters"]!.ToJsonString();
            Assert.DoesNotContain(hidden.ProjectNumber, parameters); // a non-member learns nothing from a restricted project's ID
            Assert.Contains(visible.ProjectNumber, parameters);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }); }
    }

    [Fact]
    public async Task Reports_equal_their_lists_and_respect_scope() // FR-RPT-01, FR-ASG-08, §19, SC-004
    {
        var p1 = await d.Project();
        var p2 = await d.Project();
        var late1 = await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-10" });
        var late2 = await d.NewTask(p2.Id, TestData.Pm, new { assigneeId = U(TestData.Alex), dueDate = "2026-09-12" });
        var succ = await d.NewTask(p1.Id, TestData.Pm, new { assigneeId = U(TestData.Jill), dueDate = "2026-09-30" });
        await f.As(TestData.Pm).Post($"/api/v1/tasks/{succ.S("id")}/dependencies", new { predecessorTaskId = late1.G("id") }).Result.Json(201);
        await f.Evaluate(p1.Id);
        await f.Evaluate(p2.Id);

        var catalogue = await f.As(TestData.Alex).GetAsync("/api/v1/reports").Result.Json();
        Assert.Contains(catalogue.AsArray(), r => r!.S("code") == "overdue-tasks");
        Assert.DoesNotContain(catalogue.AsArray(), r => r!.S("code") == "staff-assignments");
        Assert.Contains((await f.As(TestData.Sam).GetAsync("/api/v1/reports").Result.Json()).AsArray(), r => r!.S("code") == "staff-assignments");

        var both = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/overdue-tasks?projectId={p1.Id},{p2.Id}&minDaysOverdue=1").Result.Json();
        Assert.Equal(new[] { "key", "name", "projectNumber", "assigneeName", "dueDate", "state.daysOverdue", "state.blockingCount", "affectedMilestones", "status" },
            both["columns"]!.AsArray().Select(c => c!.S("path")));
        Assert.Equal(new[] { late1.S("key"), late2.S("key") }, both["rows"]!.AsArray().Select(r => r!.S("key")));
        Assert.Equal(4, both["rows"]![0]!["state"]!.I("daysOverdue"));
        Assert.Equal(1, both["rows"]![0]!["state"]!.I("blockingCount"));
        Assert.Null(both["listLink"]); // two projects: no single list to open
        Assert.Single((await f.As(TestData.Pm).GetAsync($"/api/v1/reports/overdue-tasks?projectId={p1.Id},{p2.Id}&minDaysOverdue=3").Result.Json())["rows"]!.AsArray());

        // One project: "Open as filtered list", and the list holds exactly the report's rows.
        var one = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/overdue-tasks?projectId={p1.Id}").Result.Json();
        Assert.Equal($"/projects/{p1.ProjectNumber}/tasks?overdue=true", one.S("listLink"));
        var list = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p1.Id}/tasks?overdue=true").Result.Json();
        Assert.Equal(one.I("total"), list.I("totalCount"));
        var blocking = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/tasks-blocking-others?projectId={p1.Id}").Result.Json();
        Assert.Equal(late1.S("key"), blocking["rows"]!.AsArray().Single()!.S("key"));

        var xlsx = await f.As(TestData.Pm).GetAsync($"/api/v1/reports/overdue-tasks/export?format=xlsx&projectId={p1.Id}&minDaysOverdue=2");
        using (var zip = new ZipArchive(new MemoryStream(await xlsx.Content.ReadAsByteArrayAsync())))
        {
            using var r = new StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml")!.Open());
            var parameters = r.ReadToEnd();
            Assert.Contains("Minimum days overdue", parameters);
            Assert.Contains(p1.ProjectNumber, parameters);
        }
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(x => x.ItemKey == "overdue-tasks" && x.Action == "Exported" && x.ProjectId == p1.Id)));

        // The rest of the catalogue runs.
        foreach (var code in new[] { "tasks-due-this-week", "blocked-tasks", "upcoming-deliverables", "deliverable-status-by-project", "upcoming-milestones", "open-decisions", "review-queue", "stale-work" })
            await f.As(TestData.Pm).GetAsync($"/api/v1/reports/{code}?projectId={p1.Id}").Result.Json();
        Assert.Contains(late1.S("key"), (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/project-activity-log?projectId={p1.Id}").Result.Json())["rows"]!.ToJsonString());

        // FR-ASG-08: Staff Assignments for a Supervisor's direct reports.
        var staff = await f.As(TestData.Sam).GetAsync($"/api/v1/reports/staff-assignments?projectId={p1.Id}").Result.Json();
        var alex = staff["rows"]!.AsArray().Single(r => r!.S("person") == "Alex Chen")!;
        Assert.Equal(p1.ProjectNumber, alex.S("projectNumber"));
        Assert.Equal("Team Member", alex.S("roles"));
        Assert.Equal(1, alex.I("openTasks"));
        Assert.Equal(1, alex.I("overdueTasks"));
        Assert.Equal("Priya Nair", alex.S("addedBy"));
        Assert.DoesNotContain(staff["rows"]!.AsArray(), r => r!.S("person") == "Priya Nair"); // not Sam's report
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/reports/staff-assignments")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).GetAsync("/api/v1/reports/staff-assignments?scope=all")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Lena).GetAsync("/api/v1/reports/staff-assignments?scope=all")).StatusCode);
    }

    [Fact]
    public async Task History_shows_dated_changes_with_reasons_and_deletion_snapshots() // AC-AUD-01, AC-AUD-02
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm, new { name = "Pavement design", assigneeId = U(TestData.Alex), dueDate = "2027-01-10" });
        (await f.As(TestData.Alex).Patch($"/api/v1/tasks/{t.S("id")}", new { dueDate = "2027-01-17", reason = "Client extension" }, await d.TaskVersion(t))).EnsureSuccessStatusCode();
        var history = (await f.As(TestData.Jill).GetAsync($"/api/v1/items/Task/{t.S("id")}/activity").Result.Json())["items"]!.AsArray();
        var moved = history.First(h => h!["changes"]!.AsArray().Any(c => c!.S("field") == "DueDate"))!;
        var change = moved["changes"]!.AsArray().Single(c => c!.S("field") == "DueDate")!;
        Assert.Equal(("2027-01-10", "2027-01-17"), (change.S("oldLabel"), change.S("newLabel")));
        Assert.Equal("Client extension", moved.S("reason"));
        Assert.Equal("Alex Chen", moved.S("actorName"));

        var next = await d.NewTask(p.Id, TestData.Pm);
        await f.As(TestData.Pm).Post($"/api/v1/tasks/{next.S("id")}/dependencies", new { predecessorTaskId = t.G("id") }).Result.Json(201);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();
        var log = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/activity?category=deletion&pageSize=50").Result.Json())["items"]!.AsArray();
        var deleted = log.Single(x => x!.S("action") == "Deleted" && x.S("itemType") == ItemType.Task)!;
        Assert.Equal(("Pavement design", "Alex Chen", "2027-01-17"), (deleted["snapshot"]!.S("name"), deleted["snapshot"]!.S("assignee"), deleted["snapshot"]!.S("dueDate")));
        Assert.Contains(log, x => x!.S("itemType") == ItemType.Dependency && x.S("itemKey") == $"deleted with {t.S("key")}");
    }
}
