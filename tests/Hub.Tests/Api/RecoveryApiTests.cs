using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// Packet 045 S01, §49.2: Show deleted and Restore on the server, behind `flag.show_deleted` (AC-RCV-01 to AC-RCV-07).
[Collection("api")]
public sealed class RecoveryApiTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task Flag(bool on) => await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.show_deleted", new { value = on }).Result.Json();

    async Task WithFlag(Func<Task> body)
    {
        await Flag(true);
        try { await body(); } finally { await Flag(false); }
    }

    async Task<JsonNode> Milestone(Guid projectId) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/milestones", new { name = "Submission " + Guid.NewGuid().ToString("N")[..4], milestoneType = "Design Submission", date = "2027-03-01" }).Result.Json(201);

    async Task<JsonNode> Deliverable(Guid projectId, Guid? milestoneId = null) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", new
        {
            name = "Package " + Guid.NewGuid().ToString("N")[..4], projectDisciplineId = d.ProjectDiscipline(projectId, "Civil"), deliverableTypeId = await d.DeliverableType(), milestoneId,
        }).Result.Json(201);

    Task<HttpResponseMessage> Restore(string as_, string kind, JsonNode item, int? ifMatch = null) =>
        f.As(as_).Action($"/api/v1/{kind}/{item.S("id")}/restore", new { }, ifMatch);

    async Task<JsonArray> Deleted(string kind, Guid projectId, string? q = null) =>
        (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{projectId}/{kind}/deleted{(q is null ? "" : $"?q={q}")}").Result.Json())["items"]!.AsArray();

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Deleted_list_shows_who_and_when_and_restore_brings_dependencies_back() => await WithFlag(async () => // AC-RCV-01
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm);
        var s1 = await d.NewTask(p.Id, TestData.Pm, new { dependsOn = new[] { t.G("id") } });
        var s2 = await d.NewTask(p.Id, TestData.Pm, new { dependsOn = new[] { t.G("id") } });
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();

        var row = Assert.Single(await Deleted("tasks", p.Id));
        Assert.Equal(t.S("key"), row!.S("key"));
        Assert.Equal(await f.DbAsync(db => db.Users.Where(u => u.Email == TestData.Pm).Select(u => u.DisplayName).FirstAsync()), row.S("deletedBy"));
        Assert.NotNull(row["deletedAt"]);
        Assert.Equal("Civil", row.S("discipline"));
        Assert.Null(row["restoreBlocked"]);

        (await Restore(TestData.Pm, "tasks", t, row.I("rowVersion"))).EnsureSuccessStatusCode();
        Assert.Empty(await Deleted("tasks", p.Id));
        Assert.Equal(2, await f.DbAsync(db => db.Dependencies.CountAsync(x => x.PredecessorTaskId == t.G("id") && (x.SuccessorTaskId == s1.G("id") || x.SuccessorTaskId == s2.G("id")))));
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == t.G("id") && a.Action == "Restored")));
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Deliverable_and_milestone_restore_log_Restored_and_reevaluate() => await WithFlag(async () => // AC-RCV-02, FR-RCV-03
    {
        var p = await d.Project();
        var m = await Milestone(p.Id);
        var del = await Deliverable(p.Id);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/deliverables/{del.S("id")}")).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/milestones/{m.S("id")}")).EnsureSuccessStatusCode();
        Assert.Single(await Deleted("deliverables", p.Id));
        Assert.Single(await Deleted("milestones", p.Id));

        Task<int> Queued() => f.DbAsync(db => db.Outbox.CountAsync(o => o.ProjectId == p.Id && o.EventType == "ProjectChanged"));
        var before = await Queued();
        foreach (var (kind, item) in new[] { ("deliverables", del), ("milestones", m) })
        {
            var restored = await Restore(TestData.Pm, kind, item).Result.Json();
            Assert.Equal(item.G("id"), restored.G("id"));
            Assert.True(restored.I("rowVersion") > 0);
            Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Pm).GetAsync($"/api/v1/{kind}/{item.S("key")}")).StatusCode); // same key
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == item.G("id") && a.Action == "Restored")));
        }
        Assert.Equal(before + 2, await Queued()); // each restore queues the project for re-evaluation
        Assert.Empty(await Deleted("deliverables", p.Id));
        Assert.Empty(await Deleted("milestones", p.Id));
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task A_child_of_a_deleted_parent_is_refused_naming_it() => await WithFlag(async () => // AC-RCV-03, FR-RCV-04
    {
        var p = await d.Project();
        var del = await Deliverable(p.Id);
        var t = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id") });
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/deliverables/{del.S("id")}")).EnsureSuccessStatusCode();

        Assert.Equal($"Restore {del.S("key")} first", Assert.Single(await Deleted("tasks", p.Id))!.S("restoreBlocked"));
        var refused = await Restore(TestData.Pm, "tasks", t).Result.Json(422);
        Assert.Equal("parent_deleted", refused.S("code"));
        Assert.Equal(del.S("key"), refused.S("parentKey"));
        (await Restore(TestData.Pm, "deliverables", del)).EnsureSuccessStatusCode();
        (await Restore(TestData.Pm, "tasks", t)).EnsureSuccessStatusCode();

        // A deliverable whose milestone is deleted behaves the same way.
        var m = await Milestone(p.Id);
        var d2 = await Deliverable(p.Id, m.G("id"));
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/deliverables/{d2.S("id")}")).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/milestones/{m.S("id")}")).EnsureSuccessStatusCode();
        Assert.Equal($"Restore {m.S("key")} first", Assert.Single(await Deleted("deliverables", p.Id))!.S("restoreBlocked"));
        Assert.Equal(m.S("key"), (await Restore(TestData.Pm, "deliverables", d2).Result.Json(422)).S("parentKey"));
        (await Restore(TestData.Pm, "milestones", m)).EnsureSuccessStatusCode();
        (await Restore(TestData.Pm, "deliverables", d2)).EnsureSuccessStatusCode();
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Only_PMs_and_Admins_read_or_restore() => await WithFlag(async () => // AC-RCV-04
    {
        var p = await d.Project();
        var t = await d.NewTask(p.Id, TestData.Pm);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();

        Assert.True((await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["permissions"]!["restore"]!["ok"]!.GetValue<bool>());
        foreach (var who in new[] { TestData.Marc, TestData.Alex, TestData.Rita }) // Discipline Lead, Team Member, Read Only
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted/export")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Restore(who, "tasks", t)).StatusCode);
        }
        Assert.False((await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["permissions"]!["restore"]!["ok"]!.GetValue<bool>());
        Assert.Single((await f.As(TestData.Admin).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted").Result.Json())["items"]!.AsArray());

        // A PM on a Cancelled project may not.
        var c = await d.Project();
        var ct = await d.NewTask(c.Id, TestData.Pm);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{ct.S("id")}")).EnsureSuccessStatusCode();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == c.Id)).Status = ProjectStatus.Cancelled; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{c.Id}/tasks/deleted")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Restore(TestData.Pm, "tasks", ct)).StatusCode);

        // A viewer who cannot see a Restricted project gets the same answer as today.
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true }).Result.Json();
        try
        {
            (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = Visibility.Restricted }, d.Version(p.Id))).EnsureSuccessStatusCode();
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Jill).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Restore(TestData.Jill, "tasks", t)).StatusCode);
        }
        finally { await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = false }).Result.Json(); }
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Stale_version_conflicts_and_a_second_restore_is_404() => await WithFlag(async () => // AC-RCV-05, FR-RCV-05
    {
        var p = await d.Project();
        var m = await Milestone(p.Id);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/milestones/{m.S("id")}")).EnsureSuccessStatusCode();
        var version = Assert.Single(await Deleted("milestones", p.Id))!.I("rowVersion");
        Assert.Equal(HttpStatusCode.Conflict, (await Restore(TestData.Pm, "milestones", m, version - 1)).StatusCode);
        Assert.Single(await Deleted("milestones", p.Id)); // nothing stored
        (await Restore(TestData.Pm, "milestones", m, version)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await Restore(TestData.Pm, "milestones", m, version)).StatusCode);

        // The undo toast sends no If-Match and still restores.
        var t = await d.NewTask(p.Id, TestData.Pm);
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).PostAsync($"/api/v1/tasks/{t.S("id")}/restore", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).PostAsync($"/api/v1/tasks/{t.S("id")}/restore", null)).StatusCode);
    });

    [Fact, Trait("flag-on", "show_deleted"), Trait("flag-off", "show_deleted")]
    public async Task Lists_counts_search_and_exports_never_show_deleted_items() // AC-RCV-06
    {
        var word = "zq" + Guid.NewGuid().ToString("N")[..8];
        var p = await d.Project();
        var gone = await d.NewTask(p.Id, TestData.Pm, new { name = $"{word} gone" });
        await d.NewTask(p.Id, TestData.Pm, new { name = $"{word} kept" });
        var del = await Deliverable(p.Id);
        var m = await Milestone(p.Id);
        foreach (var url in new[] { $"tasks/{gone.S("id")}", $"deliverables/{del.S("id")}", $"milestones/{m.S("id")}" })
            (await f.As(TestData.Pm).DeleteAsync($"/api/v1/{url}")).EnsureSuccessStatusCode();

        async Task NoneShown()
        {
            var pm = f.As(TestData.Pm);
            var tasks = await pm.GetAsync($"/api/v1/projects/{p.Id}/tasks").Result.Json();
            Assert.Equal(1, tasks.I("totalCount"));
            Assert.DoesNotContain(tasks["items"]!.AsArray(), x => x!.S("key") == gone.S("key"));
            Assert.Empty((await pm.GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json()).AsArray());
            Assert.DoesNotContain((await pm.GetAsync($"/api/v1/projects/{p.Id}/milestones").Result.Json()).AsArray(), x => x!.S("key") == m.S("key"));
            var search = await pm.GetAsync($"/api/v1/search?q={word}").Result.Json();
            Assert.DoesNotContain(search["groups"]!["tasks"]!.AsArray(), x => x!.S("key") == gone.S("key"));
            var csv = await pm.GetStringAsync($"/api/v1/projects/{p.Id}/tasks/export?format=csv");
            Assert.DoesNotContain(gone.S("key"), csv);
            Assert.DoesNotContain(del.S("key"), await pm.GetStringAsync($"/api/v1/projects/{p.Id}/deliverables/export?format=csv"));
        }

        // Flag off: today's behaviour; the new endpoints and the restore permission field do not exist.
        await NoneShown();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Restore(TestData.Pm, "deliverables", del)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Restore(TestData.Pm, "milestones", m)).StatusCode);
        Assert.Null((await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["permissions"]!["restore"]);

        // Flag on: live lists, search and exports are unchanged.
        await WithFlag(NoneShown);
    }

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Deleted_list_export_matches_the_screen_and_is_logged() => await WithFlag(async () => // AC-RCV-07, FR-RCV-02
    {
        var p = await d.Project();
        var keys = new List<string>();
        for (var i = 0; i < 3; i++)
        {
            var t = await d.NewTask(p.Id, TestData.Pm, new { name = i == 2 ? "Other thing" : $"Grading {i}" });
            (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();
            keys.Add(t.S("key"));
        }
        var screen = await Deleted("tasks", p.Id, "grading");
        Assert.Equal(new[] { keys[0], keys[1] }, screen.Select(x => x!.S("key")).Order()); // searched by name
        Assert.Equal(screen.Select(x => x!["deletedAt"]!.GetValue<DateTimeOffset>()).OrderDescending(), screen.Select(x => x!["deletedAt"]!.GetValue<DateTimeOffset>())); // newest first

        var csv = await f.As(TestData.Pm).GetStringAsync($"/api/v1/projects/{p.Id}/tasks/deleted/export?format=csv&q=grading");
        var lines = csv.TrimStart('﻿').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(screen.Count + 1, lines.Length);
        Assert.Equal(screen.Select(x => x!.S("key")), lines.Skip(1).Select(l => l.Split(',')[0]));
        var xlsx = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/tasks/deleted/export?format=xlsx&q=grading");
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);

        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ProjectId == p.Id && a.Action == "Exported").ToListAsync());
        Assert.Equal(2, log.Count);
        Assert.All(log, a => { Assert.Null(a.Snapshot); Assert.DoesNotContain(keys[0], a.ItemName ?? ""); });
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Deleted_lists_keep_their_property_names_order_and_types() => await WithFlag(async () => // the typed rows must serialise to the bytes the Show deleted list already reads
    {
        var p = await d.Project();
        var m = await Milestone(p.Id);
        var inM = await Deliverable(p.Id, m.G("id"));
        var t = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = inM.G("id") });
        foreach (var url in new[] { $"tasks/{t.S("id")}", $"deliverables/{inM.S("id")}", $"milestones/{m.S("id")}" })
            (await f.As(TestData.Pm).DeleteAsync($"/api/v1/{url}")).EnsureSuccessStatusCode();

        string[] row = ["id", "key", "name", "discipline", "parentKey", "status", "deletedAt", "deletedBy", "rowVersion", "restoreBlocked"];
        var pmName = await f.DbAsync(db => db.Users.Where(u => u.Email == TestData.Pm).Select(u => u.DisplayName).FirstAsync());
        foreach (var (kind, item, parentKey) in new[] { ("tasks", t, inM.S("key")), ("deliverables", inM, m.S("key")), ("milestones", m, (string?)null) })
        {
            var page = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/{kind}/deleted?pageSize=5").Result.Json();
            Assert.Equal(["items", "page", "pageSize", "totalCount", "sort", "filters"], page.AsObject().Select(x => x.Key));
            Assert.Equal((1, 5, 1), (page.I("page"), page.I("pageSize"), page.I("totalCount")));
            Assert.Null(page["sort"]); Assert.Null(page["filters"]);
            var r = Assert.Single(page["items"]!.AsArray())!;
            Assert.Equal(row, r.AsObject().Select(x => x.Key));
            Assert.Equal((item.G("id"), item.S("key"), pmName), (r.G("id"), r.S("key"), r.S("deletedBy")));
            Assert.False(string.IsNullOrEmpty(r.S("name")));
            Assert.Equal(parentKey, r["parentKey"]?.GetValue<string>());
            Assert.Equal(parentKey is null ? null : $"Restore {parentKey} first", r["restoreBlocked"]?.GetValue<string>()); // the milestone has no parent; the other two have a deleted one
            Assert.Equal(JsonValueKind.String, r["deletedAt"]!.GetValueKind()); Assert.Equal(JsonValueKind.Number, r["rowVersion"]!.GetValueKind());
            Assert.Equal(kind == "milestones" ? JsonValueKind.Null : JsonValueKind.String, r["discipline"]?.GetValueKind() ?? JsonValueKind.Null);
        }
    });

    [Fact, Trait("flag-on", "show_deleted")]
    public async Task Deleted_exports_of_deliverables_and_milestones_match_their_screens_and_are_logged() => await WithFlag(async () => // AC-RCV-07, FR-RCV-02: the two exports no other test called
    {
        var p = await d.Project();
        var m = await Milestone(p.Id);
        var inM = await Deliverable(p.Id, m.G("id"));
        var loose = await Deliverable(p.Id);
        foreach (var url in new[] { $"deliverables/{inM.S("id")}", $"deliverables/{loose.S("id")}", $"milestones/{m.S("id")}" })
            (await f.As(TestData.Pm).DeleteAsync($"/api/v1/{url}")).EnsureSuccessStatusCode();

        foreach (var kind in new[] { "deliverables", "milestones" })
        {
            var screen = await Deleted(kind, p.Id);
            Assert.Equal(kind == "deliverables" ? 2 : 1, screen.Count);
            var csv = await f.As(TestData.Pm).GetStringAsync($"/api/v1/projects/{p.Id}/{kind}/deleted/export?format=csv");
            var lines = csv.TrimStart('\ufeff').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Assert.Equal("Key,Name,Discipline,Parent,Status,Deleted at,Deleted by,Restore unavailable", lines[0]);
            Assert.Equal(screen.Count + 1, lines.Length);
            for (var i = 0; i < screen.Count; i++) // the same rows in the same order, cell for cell (the deletion time is shown in the organisation's zone)
            {
                var cells = lines[i + 1].Split(',');
                var r = screen[i]!;
                Assert.Equal(new[] { r.S("key"), r.S("name"), r["discipline"]?.GetValue<string>() ?? "", r["parentKey"]?.GetValue<string>() ?? "", r["status"]?.GetValue<string>() ?? "",
                    r["deletedBy"]?.GetValue<string>() ?? "", r["restoreBlocked"]?.GetValue<string>() ?? "" }, new[] { cells[0], cells[1], cells[2], cells[3], cells[4], cells[6], cells[7] });
            }
            Assert.Equal(kind == "deliverables" ? new[] { inM.S("key") } : Array.Empty<string>(), screen.Where(r => r!["restoreBlocked"] is not null).Select(r => r!.S("key"))); // a deliverable under a deleted milestone says so; nothing else does
            var xlsx = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/{kind}/deleted/export?format=xlsx");
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsx.Content.Headers.ContentType!.MediaType);
            foreach (var who in new[] { TestData.Marc, TestData.Alex, TestData.Rita })
                Assert.Equal(HttpStatusCode.Forbidden, (await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/{kind}/deleted/export")).StatusCode);
        }

        var log = await f.DbAsync(db => db.ActivityLog.Where(a => a.ProjectId == p.Id && a.Action == "Exported").Select(a => a.ItemKey).ToListAsync());
        Assert.Equal(new[] { $"{p.ProjectNumber}-deleted-deliverables", $"{p.ProjectNumber}-deleted-deliverables", $"{p.ProjectNumber}-deleted-milestones", $"{p.ProjectNumber}-deleted-milestones" }, log.Order()); // two exports each; refusals log nothing
    });

    [Fact, Trait("flag-off", "show_deleted")]
    public async Task Task_restore_with_the_flag_off_behaves_as_today() // FR-RCV-04, FR-RCV-05, AC-RCV-05
    {
        var p = await d.Project();
        var del = await Deliverable(p.Id);
        var t = await d.NewTask(p.Id, TestData.Pm, new { deliverableId = del.G("id") });
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/tasks/{t.S("id")}")).EnsureSuccessStatusCode();
        (await f.As(TestData.Pm).DeleteAsync($"/api/v1/deliverables/{del.S("id")}")).EnsureSuccessStatusCode();
        var version = await f.DbAsync(db => db.Tasks.IgnoreQueryFilters().Where(x => x.Id == t.G("id")).Select(x => x.RowVersion).FirstAsync());
        // A deleted deliverable and a stale If-Match: both restore as today.
        (await Restore(TestData.Pm, "tasks", t, version - 1)).EnsureSuccessStatusCode();
        Assert.True(await f.DbAsync(db => db.Tasks.AnyAsync(x => x.Id == t.G("id"))));
    }
}
