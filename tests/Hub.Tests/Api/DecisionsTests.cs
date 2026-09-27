using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 008: the decision register (AC-DEC-01..06, DEC-01..07, FR-DEC-01..04, FR-ORG-08, Workflow 11).
[Collection("api")]
public sealed class DecisionsTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);

    async Task At(int y, int m, int day, Func<Task> body)
    {
        var saved = f.Clock.Now;
        f.Clock.SetDate(y, m, day);
        try { await body(); } finally { f.Clock.Now = saved; }
    }

    Task<JsonNode> Party(Guid projectId, string as_ = TestData.Alex, bool client = true) =>
        f.As(as_).Post($"/api/v1/projects/{projectId}/external-parties", new { name = "Karen Li", organisation = "City of Hamilton", email = "karen@city.example", role = "Client PM", isClient = client }).Result.Json(201);

    Task<JsonNode> Raise(Guid projectId, string as_, object body, int expect = 201) => f.As(as_).Post($"/api/v1/projects/{projectId}/decisions", body).Result.Json(expect);

    static object Body(string subject, string requiredBy, Guid? owner = null, Guid? external = null, string impact = "Medium", object[]? links = null, Guid? requestedBy = null) => new
    {
        subject, description = "What must be decided and the options", ownerUserId = owner, ownerExternalPartyId = external, requiredByDate = requiredBy,
        impactLevel = impact, impactDescription = "Drawings cannot progress", links, requestedById = requestedBy,
    };

    Task<JsonNode> Register(Guid projectId, string query = "", string as_ = TestData.Pm) => f.As(as_).GetAsync($"/api/v1/projects/{projectId}/decisions?{query}").Result.Json();
    Task<int> Version(JsonNode dec) => f.DbAsync(db => db.Decisions.Where(x => x.Id == dec.G("id")).Select(x => x.RowVersion).FirstAsync());

    async Task<JsonNode> Move(string as_, JsonNode dec, object body, int expect = 200)
    {
        var dict = new Dictionary<string, object?> { ["rowVersion"] = await Version(dec) };
        foreach (var p in body.GetType().GetProperties()) dict[p.Name] = p.GetValue(body);
        return await f.As(as_).Post($"/api/v1/decisions/{dec.S("id")}/transition", dict).Result.Json(expect);
    }

    Task<TaskState> State(JsonNode t) => f.DbAsync(db => db.TaskStates.FirstAsync(s => s.TaskId == t.G("id")));
    Task<List<AttentionItem>> A04(Guid projectId) => f.DbAsync(db => db.Attention.Where(a => a.ProjectId == projectId && a.RuleId == "A-04").ToListAsync());

    [Fact]
    public async Task External_owner_register_columns_filters_and_sort() // AC-DEC-01, DEC-06, FR-DEC-04, FR-009, §17.2
    {
        var p = await d.Project();
        var karen = await Party(p.Id);
        var one = await Raise(p.Id, TestData.Alex, Body("Confirm pavement structure", "2026-09-20", external: karen.G("id"), impact: "High"));
        var two = await Raise(p.Id, TestData.Pm, Body("Approve lane closure plan", "2026-09-16", owner: U(TestData.Diane), impact: "Low"));
        var three = await Raise(p.Id, TestData.Pm, Body("Select pump supplier", "2026-09-10", owner: U(TestData.Alex)));
        Assert.EndsWith("-DEC01", one.S("key"));
        await f.Evaluate(p.Id);

        var rows = (await Register(p.Id)).AsArray();
        Assert.Equal(new[] { three.S("key"), two.S("key"), one.S("key") }, rows.Select(r => r!.S("key"))); // overdue first, then required-by
        var ext = rows.Single(r => r!.S("key") == one.S("key"))!;
        Assert.Equal("Karen Li", ext.S("ownerName"));
        Assert.Equal("City of Hamilton", ext.S("ownerOrganisation"));
        Assert.True(ext["ownerIsClient"]!.GetValue<bool>());
        Assert.Equal("Alex Chen", ext.S("requestedByName"));
        Assert.Equal(6, ext.I("daysUntil"));
        var late = rows.Single(r => r!.S("key") == three.S("key"))!;
        Assert.True(late["isOverdue"]!.GetValue<bool>());
        Assert.Equal(4, late.I("daysOverdue"));

        Assert.Equal(new[] { one.S("key") }, (await Register(p.Id, "ownerType=external")).AsArray().Select(r => r!.S("key")));
        Assert.Equal(new[] { one.S("key") }, (await Register(p.Id, "ownerType=client")).AsArray().Select(r => r!.S("key")));
        Assert.Equal(2, (await Register(p.Id, "ownerType=internal")).AsArray().Count);
        Assert.Equal(new[] { two.S("key") }, (await Register(p.Id, "impact=Low")).AsArray().Select(r => r!.S("key")));
        Assert.Equal(2, (await Register(p.Id, "requiredFrom=2026-09-15&requiredTo=2026-09-30")).AsArray().Count);
        Assert.Equal(new[] { three.S("key") }, (await Register(p.Id, "indicator=overdue")).AsArray().Select(r => r!.S("key")));

        // No email ever goes outside the organisation; the internal owner hears at once and joins the team.
        Assert.False(await f.DbAsync(db => db.Emails.AnyAsync(e => e.ToAddress == "karen@city.example")));
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(TestData.Diane) && n.EventType == NotificationEvents.DecisionAssigned && n.ItemId == two.G("id"))));
        Assert.True(await f.DbAsync(db => db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == U(TestData.Diane) && m.RemovedAt == null)));

        // FR-009: team members create external parties; the PM edits them.
        var rv = karen.I("rowVersion");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/external-parties/{karen.S("id")}", new { role = "Owner" }, rv)).StatusCode);
        (await f.As(TestData.Pm).Patch($"/api/v1/external-parties/{karen.S("id")}", new { role = "Owner's engineer" }, rv)).EnsureSuccessStatusCode();
        var parties = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/external-parties").Result.Json();
        Assert.Equal("Owner's engineer", parties[0]!.S("role"));
    }

    [Fact]
    public async Task Overdue_decision_blocks_linked_tasks_and_raises_critical() // AC-DEC-02, AC-DEC-03, DEC-04, D-15
    {
        var p = await d.Project();
        var karen = await Party(p.Id);
        var t1 = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        var t2 = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Alex) });
        var dec = await Raise(p.Id, TestData.Pm, Body("Confirm pavement structure", "2026-09-13", external: karen.G("id"), requestedBy: U(TestData.Marc),
            links: [new { targetType = "Task", targetId = t1.G("id") }, new { targetType = "Task", targetId = t2.G("id") }]));
        await f.Evaluate(p.Id);

        var a04 = Assert.Single(await A04(p.Id));
        Assert.Equal(Severity.Critical, a04.Severity);
        Assert.Contains(U(TestData.Marc), a04.RouteToUserIds); // the requester
        Assert.Contains(U(TestData.Pm), a04.RouteToUserIds);
        foreach (var t in new[] { t1, t2 })
        {
            var st = await State(t);
            Assert.True(st.IsBlocked);
            Assert.Contains(dec.S("key"), st.BlockedBy);
        }
        var dash = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/dashboard").Result.Json();
        Assert.Equal(1, dash["decisions"]!["overdue"]!.I("value"));
        Assert.Equal(2, dash["tasks"]!["blocked"]!.I("value"));

        var row = Assert.Single((await Register(p.Id, "blocking=true")).AsArray())!;
        Assert.Equal(new[] { t1.S("id"), t2.S("id") }.Order(), row["blockingTaskIds"]!.AsArray().Select(x => x!.GetValue<string>()).Order());
        var detail = await f.As(TestData.Alex).GetAsync($"/api/v1/decisions/{dec.S("key")}").Result.Json();
        Assert.Equal(2, detail["links"]!.AsArray().Count(l => l!.S("relation") == ItemRelation.BlockedByDecision));
        Assert.False(detail["permissions"]!["transitions"]!.AsArray().Single(x => x!.S("to") == DecisionStatus.Decided)!["ok"]!.GetValue<bool>()); // Alex is not owner or PM
    }

    [Fact]
    public async Task Defer_record_reopen_and_cancel_follow_workflow_11() // AC-DEC-04, AC-DEC-05, AC-DEC-06, DEC-02, DEC-03, DEC-07
    {
        var p = await d.Project();
        var t1 = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Marc) });
        var t2 = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = U(TestData.Jill) });
        var dec = await Raise(p.Id, TestData.Pm, Body("Confirm pavement structure", "2026-09-13", owner: U(TestData.Alex),
            links: [new { targetType = "Task", targetId = t1.G("id") }, new { targetType = "Task", targetId = t2.G("id") }]));
        await f.Evaluate(p.Id);
        Assert.True((await State(t1)).IsBlocked);

        // DEC-03: a later date and a reason.
        Assert.Equal("validation", (await Move(TestData.Pm, dec, new { toStatus = "Deferred", newRequiredBy = "2026-09-14", reason = "Client asked for time" }, 400)).S("code"));
        await Move(TestData.Pm, dec, new { toStatus = "Deferred", newRequiredBy = "2026-09-21" }, 400);
        await Move(TestData.Pm, dec, new { toStatus = "Deferred", newRequiredBy = "2026-09-21", reason = "Client committed to 21 September" });
        await f.Evaluate(p.Id);
        var s1 = await State(t1);
        Assert.False(s1.IsBlocked);
        Assert.True(s1.IsWaiting);
        Assert.Empty(await A04(p.Id));
        var deferral = await f.DbAsync(db => db.ActivityLog.SingleAsync(a => a.ItemId == dec.G("id") && a.Action == "Deferred"));
        Assert.Contains("2026-09-13", deferral.Changes);
        Assert.Contains("2026-09-21", deferral.Changes);
        Assert.Equal("Client committed to 21 September", deferral.Reason);

        // AC-DEC-06: Decided needs the decision text.
        await Move(TestData.Alex, dec, new { toStatus = "Under Review" });
        await Move(TestData.Alex, dec, new { toStatus = "Decided" }, 400);
        await Move(TestData.Alex, dec, new { toStatus = "Decided", decisionText = "Use 450 mm granular base", decisionDate = "2026-09-14" });
        var stored = await f.DbAsync(db => db.Decisions.AsNoTracking().FirstAsync(x => x.Id == dec.G("id")));
        Assert.Equal(U(TestData.Alex), stored.DecidedById);
        foreach (var who in new[] { TestData.Marc, TestData.Jill, TestData.Pm }) // assignees and the requester
            Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.UserId == U(who) && n.EventType == NotificationEvents.DecisionRecorded && n.ItemId == dec.G("id"))), who);
        await f.Evaluate(p.Id);
        Assert.False((await State(t1)).IsWaiting);

        // DEC-07: only the PM reopens, with a reason; overdue again means blocked again.
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/decisions/{dec.S("id")}/transition",
            new { toStatus = "Pending", reason = "Client changed their mind", rowVersion = await Version(dec) })).StatusCode);
        await Move(TestData.Pm, dec, new { toStatus = "Pending" }, 400);
        await Move(TestData.Pm, dec, new { toStatus = "Pending", reason = "Client changed their mind" });
        Assert.Null((await f.DbAsync(db => db.Decisions.AsNoTracking().FirstAsync(x => x.Id == dec.G("id")))).DecisionText);
        Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.ItemId == dec.G("id") && a.Action == "Reopened" && a.Reason == "Client changed their mind")));
        await At(2026, 9, 22, async () =>
        {
            await f.Evaluate(p.Id);
            Assert.True((await State(t1)).IsBlocked);

            // Workflow 11 exception: cancelling releases the tasks with a note.
            await Move(TestData.Pm, dec, new { toStatus = "Cancelled" }, 400);
            await Move(TestData.Pm, dec, new { toStatus = "Cancelled", reason = "Superseded by DEC02" });
            await f.Evaluate(p.Id);
            var s = await State(t1);
            Assert.False(s.IsBlocked);
            Assert.Contains("decision_cancelled", s.Notes);
        });
        Assert.Equal("illegal_transition", (await Move(TestData.Pm, dec, new { toStatus = "Pending", reason = "Try again later" }, 422)).S("code"));
    }

    [Fact]
    public async Task High_impact_due_soon_owners_links_and_edit_rights() // DEC-01, DEC-05, FR-001, §8.5.2
    {
        var p = await d.Project();
        var karen = await Party(p.Id);
        var soon = await Raise(p.Id, TestData.Alex, Body("Approve utility relocation", "2026-09-17", owner: U(TestData.Diane), impact: "High"));
        await f.Evaluate(p.Id);
        Assert.Equal(Severity.Warning, Assert.Single(await A04(p.Id)).Severity);

        // DEC-01: exactly one owner.
        await Raise(p.Id, TestData.Pm, Body("Two owners", "2026-09-30", owner: U(TestData.Alex), external: karen.G("id")), 400);
        await Raise(p.Id, TestData.Pm, Body("No owner", "2026-09-30"), 400);
        await Raise(p.Id, TestData.Rita, Body("Read only", "2026-09-30", owner: U(TestData.Alex)), 403);

        // Alex requested it, so Alex edits it; a lead who neither owns nor requested it cannot.
        var rv = await Version(soon);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Omar).Patch($"/api/v1/decisions/{soon.S("id")}", new { subject = "Changed" }, rv)).StatusCode);
        (await f.As(TestData.Alex).Patch($"/api/v1/decisions/{soon.S("id")}", new { ownerExternalPartyId = karen.G("id") }, rv)).EnsureSuccessStatusCode();
        var moved = await f.DbAsync(db => db.Decisions.AsNoTracking().FirstAsync(x => x.Id == soon.G("id")));
        Assert.Null(moved.OwnerUserId);
        Assert.Equal(karen.G("id"), moved.OwnerExternalPartyId);

        // Links: only tasks are blocked by decisions; one link per item; links stay in the project.
        var del = await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Drainage report", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType() }).Result.Json(201);
        var alex = f.As(TestData.Alex);
        await alex.Post($"/api/v1/decisions/{soon.S("id")}/links", new { targetType = "Deliverable", targetId = del.G("id"), relation = ItemRelation.BlockedByDecision }).Result.Json(400);
        var link = await alex.Post($"/api/v1/decisions/{soon.S("id")}/links", new { targetType = "Deliverable", targetId = del.G("id") }).Result.Json(201);
        await alex.Post($"/api/v1/decisions/{soon.S("id")}/links", new { targetType = "Deliverable", targetId = del.G("id") }).Result.Json(409);
        var elsewhere = await d.NewTask((await d.Project()).Id, TestData.Pm);
        await alex.Post($"/api/v1/decisions/{soon.S("id")}/links", new { targetType = "Task", targetId = elsewhere.G("id") }).Result.Json(400);
        var row = Assert.Single((await Register(p.Id)).AsArray())!;
        Assert.Equal("Drainage report", row["deliverables"]![0]!.S("name"));
        Assert.Equal(HttpStatusCode.NoContent, (await alex.DeleteAsync($"/api/v1/item-links/{link.S("id")}")).StatusCode);
        Assert.Empty((await alex.GetAsync($"/api/v1/decisions/{soon.S("id")}").Result.Json())["links"]!.AsArray());
    }
}
