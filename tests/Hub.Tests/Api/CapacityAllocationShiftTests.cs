using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S31 fixtures use normal create/edit/confirm/role commands. No forged confirmation or row-version state.
public sealed class CapacityAllocationShiftTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Mon = new(2026, 11, 9);
    public CapacityAllocationShiftTests() => d = new(f);
    public void Dispose() => f.Dispose();
    string Root(Guid project) => $"/api/v1/projects/{project}/allocations";
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value = true })).EnsureSuccessStatusCode();
    sealed record Fixture(Guid Id, Guid Task, AllocationEndpoints.CreateBody Body);
    async Task<Fixture> Create(Project p, decimal hours = 8, string actor = TestData.Pm, DateOnly? from = null, DateOnly? through = null,
        AllocationEndpoints.DayInput[]? days = null, Guid? task = null, Guid? role = null)
    {
        var first = from ?? Mon; var last = through ?? Mon.AddDays(4);
        task ??= (await d.NewTask(p.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 0m, startDate = first, dueDate = last })).G("id");
        var body = new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Alex), AllocationPurpose.Production, first, last, hours, days ?? [], [new("Task", task.Value, first)], null, role);
        var saved = await (await f.As(actor).Post(Root(p.Id), body)).Json(); return new(saved.G("id"), task.Value, body);
    }
    async Task<JsonNode> Detail(Guid p, Guid id) => await (await f.As(TestData.Pm).GetAsync(Root(p) + $"/{id}")).Json();
    async Task Confirm(Guid p, Guid id)
    {
        var preview = await (await f.As(TestData.Sam).GetAsync(Root(p) + $"/{id}/confirmation-preview")).Json();
        await (await f.As(TestData.Sam).Post(Root(p) + $"/{id}/confirm", new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), preview.I("rowVersion"),
            preview["days"]!.AsArray().Select(day => new AllocationEndpoints.DateVersionInput(DateOnly.Parse(day!.S("date")), day.I("dateVersion"))).ToArray(),
            preview["days"]!.AsArray().Any(day => day!["overByHours"]!.GetValue<decimal>() > 0) ? "Synthetic confirmed capacity exception" : null))).Json();
    }
    async Task<JsonNode> Preview(Guid p, int weeks, string mode = "Selected", params Guid[] ids)
        => await (await f.As(TestData.Pm).Post(Root(p) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody(mode, weeks, ids))).Json();
    AllocationEndpoints.AllocationShiftBody ApplyBody(JsonNode preview, string reason = "Move staffing to revised dates")
        => new(Guid.NewGuid(), preview.S("mode"), preview.I("weeks"), preview["allocations"]!.AsArray().Select(row => new AllocationEndpoints.AllocationShiftVersion(row!.G("id"), row.I("rowVersion"))).ToArray(), reason);
    async Task<string> State() => await f.DbAsync(async db => JsonSerializer.Serialize(new {
        Allocations = await db.Allocations.OrderBy(row => row.Id).ToArrayAsync(), Days = await db.AllocationDayOverrides.OrderBy(row => row.Id).ToArrayAsync(),
        Links = await db.AllocationWorkLinks.OrderBy(row => row.Id).ToArrayAsync(), Dates = await db.PersonDateVersions.OrderBy(row => row.Id).ToArrayAsync(),
        Commands = await db.CoordinationCommands.OrderBy(row => row.Id).ToArrayAsync(), Audit = await db.ActivityLog.OrderBy(row => row.Id).ToArrayAsync(),
        Notices = await db.Notifications.CountAsync(), Outbox = await db.Outbox.CountAsync(), Emails = await db.Emails.CountAsync(), Receipts = await db.Idempotency.CountAsync(),
        Tasks = await db.Tasks.OrderBy(row => row.Id).Select(row => new { row.Id, row.StartDate, row.DueDate, row.EstimatedHours, row.ProgressPct, row.RowVersion }).ToArrayAsync(),
        Roles = await db.OpenRoles.OrderBy(row => row.Id).ToArrayAsync(), Schedules = await db.PersonSchedules.CountAsync(), Leave = await db.ApprovedLeaves.CountAsync(),
    }));

    [Fact]
    public async Task Three_row_shift_previews_purely_moves_children_reproposes_confirmed_and_retries_once()
    {
        var p = await d.Project(); var one = await Create(p, 12.345m, days: [new(Mon, 2.125m)]); var two = await Create(p); var three = await Create(p);
        await Confirm(p.Id, one.Id); var prior = await Detail(p.Id, one.Id);
        var snapshot = await f.DbAsync(db => db.Allocations.Where(row => row.Id == one.Id).Select(row => row.ConfirmationSnapshot).SingleAsync());
        await Enable(); var before = await State();
        using (var request = new HttpRequestMessage(HttpMethod.Post, Root(p.Id) + "/shift-preview") {
            Content = JsonContent.Create(new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 2, [one.Id, two.Id, three.Id])),
        }) { request.Headers.Add("Idempotency-Key", "synthetic-pure-shift-preview"); await (await f.As(TestData.Pm).SendAsync(request)).Json(); }
        var preview = await Preview(p.Id, 2, "Selected", one.Id, two.Id, three.Id);
        Assert.Equal(before, await State()); Assert.Equal(3, preview["allocations"]!.AsArray().Count);
        Assert.All(preview["allocations"]!.AsArray(), row => { Assert.Equal(("2026-11-09", "2026-11-13", "2026-11-23", "2026-11-27"),
            (row!.S("fromDate"), row.S("throughDate"), row.S("newFromDate"), row.S("newThroughDate"))); Assert.Equal(d.User(TestData.Alex), row.G("personId")); });
        Assert.Single(preview["allocations"]!.AsArray().Where(row => row!["requiresConfirmation"]!.GetValue<bool>()));
        var oldAudit = await f.DbAsync(db => db.ActivityLog.Select(row => row.Id).ToArrayAsync()); var body = ApplyBody(preview);
        var saved = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body)).Json();
        // The stored receipt hash is the exact legacy anonymous payload wire.
        Assert.Equal(Coordination.Hash(new { operation = "allocation.shift", body }), f.Db(db => db.CoordinationCommands.Single(row => row.RequestId == body.RequestId).PayloadHash));
        Assert.Equal(new[] { one.Id, two.Id, three.Id }.Order().First(), saved.G("id"));
        foreach (var item in new[] { one, two, three })
        {
            var current = await Detail(p.Id, item.Id); Assert.Equal(("2026-11-23", "2026-11-27", AllocationStatus.Proposed), (current.S("fromDate"), current.S("throughDate"), current.S("status")));
            Assert.Equal(item.Body.PlannedHours, current["plannedHours"]!.GetValue<decimal>()); Assert.Equal("2026-11-23", Assert.Single(current["links"]!.AsArray())!.S("workDate"));
            Assert.Equal(item.Task, current["links"]![0]!.G("workId")); Assert.Equal("2026-11-13", f.Db(db => db.Tasks.Single(task => task.Id == item.Task).DueDate)!.Value.ToString("yyyy-MM-dd"));
        }
        var detail = await Detail(p.Id, one.Id); Assert.Equal(("2026-11-23", 2.125m), (detail["days"]![0]!.S("workDate"), detail["days"]![0]!["hours"]!.GetValue<decimal>()));
        Assert.Equal(prior["confirmedAt"]!.ToJsonString(), detail["confirmedAt"]!.ToJsonString()); Assert.Equal(prior.G("confirmedBy"), detail.G("confirmedBy"));
        Assert.Equal(snapshot, await f.DbAsync(db => db.Allocations.Where(row => row.Id == one.Id).Select(row => row.ConfirmationSnapshot).SingleAsync()));
        var activities = await f.DbAsync(db => db.ActivityLog.Where(row => !oldAudit.Contains(row.Id) && row.ItemType == "ResourceAllocation").ToArrayAsync());
        Assert.Equal(3, activities.Length); Assert.All(activities, row => { Assert.Equal(body.Reason, row.Reason); Assert.Equal(d.User(TestData.Pm), row.ActorUserId); });
        var after = await State(); Assert.Equal(saved.ToJsonString(), (await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body)).Json()).ToJsonString()); Assert.Equal(after, await State());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body with { Weeks = 3 })).StatusCode);
        Assert.Equal(after, await State()); await Confirm(p.Id, one.Id); Assert.Equal(AllocationStatus.Confirmed, (await Detail(p.Id, one.Id)).S("status"));
    }

    [Fact]
    public async Task Moving_selected_source_slices_together_allows_a_chain_but_unselected_collision_is_atomic()
    {
        var p = await d.Project(); var one = await Create(p); var two = await Create(p, from: Mon.AddDays(14), through: Mon.AddDays(18), task: one.Task);
        await Enable(); var before = await State();
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 2, [one.Id]))).StatusCode);
        var current = await Detail(p.Id, one.Id); var colliding = new AllocationEndpoints.AllocationShiftBody(Guid.NewGuid(), "Selected", 2, [new(one.Id, current.I("rowVersion"))], "Collision must roll back");
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", colliding)).StatusCode); Assert.Equal(before, await State());
        var preview = await Preview(p.Id, 2, "Selected", one.Id, two.Id); await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", ApplyBody(preview))).Json();
        Assert.Equal("2026-11-23", (await Detail(p.Id, one.Id)).S("fromDate")); Assert.Equal("2026-12-07", (await Detail(p.Id, two.Id)).S("fromDate"));
    }

    [Fact]
    public async Task Whole_project_uses_all_pages_and_rejects_a_changed_cohort_or_one_stale_row()
    {
        var p = await d.Project(); var first = await Create(p); await Enable();
        var old = ApplyBody(await Preview(p.Id, 1, "WholeProject")); await Create(p);
        var before = await State(); Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", old)).StatusCode); Assert.Equal(before, await State());
        var stale = ApplyBody(await Preview(p.Id, 1, "WholeProject")); var detail = await Detail(p.Id, first.Id);
        await (await f.As(TestData.Pm).Post(Root(p.Id) + $"/{first.Id}/edit", new AllocationEndpoints.EditBody(Guid.NewGuid(), detail.I("rowVersion"), first.Body.PersonId,
            first.Body.Purpose, first.Body.FromDate, first.Body.ThroughDate, 9, first.Body.Days, first.Body.Links, "Changed before shift"))).Json();
        before = await State(); Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", stale)).StatusCode); Assert.Equal(before, await State());
        for (var index = 2; index < 51; index++) await Create(p);
        var list = await (await f.As(TestData.Pm).GetAsync(Root(p.Id))).Json(); Assert.Equal(51, list.I("totalCount")); Assert.Equal(50, list["items"]!.AsArray().Count);
        var preview = await Preview(p.Id, -2, "WholeProject"); Assert.Equal(51, preview["allocations"]!.AsArray().Count);
        await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", ApplyBody(preview))).Json();
        Assert.Equal(51, await f.DbAsync(db => db.Allocations.CountAsync(row => row.ProjectId == p.Id && row.FromDate == Mon.AddDays(-14))));
    }

    [Fact]
    public async Task Calendar_change_overflow_zero_invalid_reference_and_reason_refuse_without_partial_writes()
    {
        var p = await d.Project(); var one = await Create(p, from: Mon, through: Mon); var two = await Create(p); await Enable();
        var body = ApplyBody(await Preview(p.Id, 1, "Selected", one.Id, two.Id));
        await (await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { officeId = await d.Office(), date = Mon.AddDays(7), name = "Synthetic shifted-date holiday" })).Json(201);
        var before = await State(); Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body)).StatusCode); Assert.Equal(before, await State());
        foreach (var weeks in new[] { 0, int.MaxValue, int.MinValue, -1000000 })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", weeks, [one.Id]))).StatusCode);
            Assert.Equal(before, await State());
        }
        var invalidReason = ApplyBody(await Preview(p.Id, 2, "Selected", one.Id, two.Id), "bad");
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", invalidReason)).StatusCode); Assert.Equal(before, await State());
        // Normal task cancellation of the actual linked task invalidates a once-valid preview.
        var linked = await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{one.Task}")).Json();
        await d.Move(TestData.Marc, linked["task"] ?? linked, TaskStatuses.Cancelled, new { reason = "Synthetic linked work is no longer required" });
        before = await State(); Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body with { RequestId = Guid.NewGuid(), Weeks = 2 })).StatusCode); Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Selected_creator_permissions_terminal_foreign_and_flag_off_match_existing_edit_boundary()
    {
        var p = await d.Project(); var own = await Create(p, actor: TestData.Marc); var other = await Create(p); var foreign = await Create(await d.Project());
        var normal = await (await f.As(TestData.Pm).GetAsync(Root(p.Id))).Json();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [own.Id]))).StatusCode);
        var ownDetail = await Detail(p.Id, own.Id); var body = new AllocationEndpoints.AllocationShiftBody(Guid.NewGuid(), "Selected", 1, [new(own.Id, ownDetail.I("rowVersion"))], "Shift own allocation");
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body)).StatusCode);
        Assert.Equal(normal.ToJsonString(), (await (await f.As(TestData.Pm).GetAsync(Root(p.Id))).Json()).ToJsonString()); await Enable();
        var before = await State();
        foreach (var actor in new[] { TestData.Alex, TestData.Rita }) Assert.Equal(HttpStatusCode.Forbidden, (await f.As(actor).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [own.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("WholeProject", 1, []))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Marc).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [other.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [own.Id, foreign.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [own.Id, own.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Post(Root(p.Id) + "/shift", body)).StatusCode);
        Assert.Equal(before, await State()); await (await f.As(TestData.Marc).Post(Root(p.Id) + "/shift", body)).Json();
        var terminal = await Detail(p.Id, other.Id); await (await f.As(TestData.Pm).Post(Root(p.Id) + $"/{other.Id}/cancel", new AllocationEndpoints.CancelBody(Guid.NewGuid(), terminal.I("rowVersion"), "Cancel before shifting"))).Json();
        before = await State(); Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift-preview", new AllocationEndpoints.AllocationShiftPreviewBody("Selected", 1, [other.Id]))).StatusCode);
        Assert.DoesNotContain((await Preview(p.Id, 1, "WholeProject"))["allocations"]!.AsArray(), row => row!.G("id") == other.Id); Assert.Equal(before, await State());
    }

    [Fact]
    public async Task Review_link_dates_hours_and_work_identity_shift_but_review_due_date_and_round_do_not()
    {
        var p = await d.Project(); var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new {
            name = "Synthetic shift review source", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false,
        })).Json(201)).G("id");
        var revision = (await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable),
            civil, d.User(TestData.Alex), "Deliverable", "shift-review", "Synthetic shift review source", "A", "https://example.test/shift.pdf", "Synthetic survey", "Synthetic scope",
            null, null, null, null, null, null))).Json()).G("id");
        var package = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Synthetic shift review", "Review date identity",
            civil, d.User(TestData.Marc), [revision], [new(electrical, d.User(TestData.Omar), Mon)], false, null))).Json()).G("id");
        var round = f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value);
        var review = f.Db(db => db.DisciplineReviews.Single(row => row.RoundId == round).Id);
        var saved = await (await f.As(TestData.Pm).Post(Root(p.Id), new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Omar), AllocationPurpose.Review,
            Mon, Mon, 3.125m, [new(Mon, 3.125m)], [new("Review", review, Mon, 2.375m)], null))).Json();
        await Enable(); var preview = await Preview(p.Id, -2, "Selected", saved.G("id"));
        await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", ApplyBody(preview))).Json(); var detail = await Detail(p.Id, saved.G("id"));
        Assert.Equal(("2026-10-26", "2026-10-26", 3.125m), (detail.S("fromDate"), detail.S("throughDate"), detail["plannedHours"]!.GetValue<decimal>()));
        Assert.Equal(("2026-10-26", 3.125m), (detail["days"]![0]!.S("workDate"), detail["days"]![0]!["hours"]!.GetValue<decimal>()));
        Assert.Equal(("Review", review, "2026-10-26", 2.375m), (detail["links"]![0]!.S("workType"), detail["links"]![0]!.G("workId"), detail["links"]![0]!.S("workDate"), detail["links"]![0]!["reviewHours"]!.GetValue<decimal>()));
        Assert.Equal((Mon, round), f.Db(db => db.DisciplineReviews.Where(row => row.Id == review).Select(row => new { row.DueDate, row.RoundId }).AsEnumerable().Select(row => (row.DueDate, row.RoundId)).Single()));
    }

    [Fact]
    public async Task Inactive_person_after_preview_is_revalidated_without_moving_any_selected_row()
    {
        var p = await d.Project(); var one = await Create(p); var two = await Create(p); await Enable();
        var body = ApplyBody(await Preview(p.Id, 2, "Selected", one.Id, two.Id));
        var admin = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/users?q=alex@hub.test")).Json(); var user = Assert.Single(admin.AsArray())!;
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{one.Body.PersonId}", new { isActive = false }, user.I("rowVersion"))).Json();
        var before = await State(); Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", body)).StatusCode);
        Assert.Equal(before, await State()); Assert.Equal("2026-11-09", (await Detail(p.Id, one.Id)).S("fromDate"));
    }

    [Fact]
    public async Task Filled_role_dates_remain_unchanged_and_two_confirmed_date_unions_are_invalidated()
    {
        var p = await d.Project(); var role = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new {
            roleLabel = "Synthetic filled role", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), fromDate = Mon, throughDate = Mon.AddDays(4), plannedHours = 8,
        })).Json(201)).G("id");
        var one = await Create(p, role: role); var two = await Create(p); await Confirm(p.Id, one.Id); await Confirm(p.Id, two.Id); await Enable();
        var roleBefore = await f.DbAsync(db => db.OpenRoles.AsNoTracking().SingleAsync(row => row.Id == role));
        var dates = await f.DbAsync(db => db.PersonDateVersions.Where(row => row.PersonId == one.Body.PersonId && row.WorkDate >= Mon && row.WorkDate <= Mon.AddDays(4)).ToDictionaryAsync(row => row.WorkDate, row => row.RowVersion));
        var preview = await Preview(p.Id, 2, "Selected", one.Id, two.Id); var notice = preview["allocations"]!.AsArray().Single(row => row!.G("id") == one.Id)!["unchangedRoles"]![0]!;
        Assert.Equal((role, "2026-11-09", "2026-11-13"), (notice.G("id"), notice.S("fromDate"), notice.S("throughDate")));
        await (await f.As(TestData.Pm).Post(Root(p.Id) + "/shift", ApplyBody(preview))).Json();
        Assert.Equal(JsonSerializer.Serialize(roleBefore), JsonSerializer.Serialize(await f.DbAsync(db => db.OpenRoles.AsNoTracking().SingleAsync(row => row.Id == role))));
        var next = await f.DbAsync(db => db.PersonDateVersions.Where(row => row.PersonId == one.Body.PersonId && row.WorkDate >= Mon && row.WorkDate <= Mon.AddDays(4)).ToDictionaryAsync(row => row.WorkDate, row => row.RowVersion));
        Assert.Equal(5, next.Count); Assert.All(dates, pair => Assert.True(next[pair.Key] > pair.Value));
        Assert.Equal(AllocationStatus.Proposed, (await Detail(p.Id, one.Id)).S("status")); Assert.Equal(AllocationStatus.Proposed, (await Detail(p.Id, two.Id)).S("status"));
    }
}
