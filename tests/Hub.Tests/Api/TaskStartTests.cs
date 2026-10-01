using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-RDY-02 task start: work that is Not Ready or Needs Assessment (including never assessed) starts only with the
/// starter's acknowledgement, a reason and a PM/Discipline Lead authorisation; Ready and Proceed under Assumption start
/// normally; review, access and lifecycle guards come first. Moves pass `acknowledgeReadiness` to opt out of TestData's
/// authorised retry.
[Collection("api")]
public sealed class TaskStartTests(HubFactory f)
{
    readonly TestData data = new(f);

    static string[] Needs(JsonNode problem) => [.. problem["needs"]!.AsArray().Select(n => n!.GetValue<string>())];
    string Status(Guid id) => f.Db(db => db.Tasks.AsNoTracking().Single(x => x.Id == id).Status);
    List<TaskStartAuthorisation> Authorisations(Guid id) =>
        f.Db(db => db.TaskStartAuthorisations.AsNoTracking().Where(a => a.TaskId == id).OrderBy(a => a.CreatedAt).ToList());
    Task<JsonNode> View(string who, Guid id) => f.As(who).GetAsync($"/api/v1/tasks/{id}/start-readiness").Result.Json();

    async Task<JsonNode> Assess(Guid projectId, Guid taskId, string owner = TestData.Alex)
    {
        var version = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        return await (await f.As(owner).Post($"/api/v1/projects/{projectId}/readiness/Task/{taskId}",
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Survey layout", "Layout checked against site survey"))).Json();
    }

    /// Ready: only the (source-checked) production owner applies; the other checks are recorded not applicable.
    async Task Ready(Guid projectId, Guid taskId)
    {
        var assessment = await Assess(projectId, taskId);
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            {
                check.Applies = check.Code == ReadinessCheckCode.ProductionOwner;
                check.Satisfied = check.Applies == true ? true : null;
            }
            await db.SaveChangesAsync(); return 0;
        });
    }

    [Fact]
    public async Task Unassessed_start_needs_acknowledgement_reason_and_an_authorisation_that_is_used_once()
    {
        var p = await data.Project();
        var t = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var id = t.G("id");

        var refused = await data.Move(TestData.Alex, t, TaskStatuses.InProgress, new { acknowledgeReadiness = false }, expect: 422);
        Assert.Equal("start_authorisation_required", refused.S("code"));
        Assert.Equal(ReadinessState.NeedsAssessment, refused.S("readinessState"));
        Assert.False(refused["assessed"]!.GetValue<bool>());
        Assert.False(refused["canAuthorise"]!.GetValue<bool>());
        Assert.Equal(["acknowledgement", "reason", "authorisation"], Needs(refused));
        Assert.Contains("Needs Assessment (no readiness assessment has been recorded)", refused.S("detail"));
        var unauthorised = await data.Move(TestData.Alex, t, TaskStatuses.InProgress,
            new { acknowledgeReadiness = true, reason = "Survey crew is on site" }, expect: 422);
        Assert.Equal(["authorisation"], Needs(unauthorised));
        Assert.Equal(TaskStatuses.NotStarted, Status(id));

        // Only the PM or the discipline lead authorises, with an explicit acknowledgement and a reason.
        await data.AuthoriseStart(t, TestData.Alex, reason: "I would like to start", expect: 403);
        await data.AuthoriseStart(t, TestData.Pm, acknowledged: false, reason: "Survey crew is on site", expect: 400);
        await data.AuthoriseStart(t, TestData.Pm, reason: "ok", expect: 400);
        var authorisation = await data.AuthoriseStart(t, TestData.Pm, reason: "Survey crew is on site this week");
        var view = await View(TestData.Alex, id);
        Assert.True(view["needsAuthorisation"]!.GetValue<bool>());
        Assert.False(view["canAuthorise"]!.GetValue<bool>());
        Assert.True((await View(TestData.Marc, id))["canAuthorise"]!.GetValue<bool>());
        Assert.Equal(authorisation.G("id"), view["authorisation"]!.G("id"));
        Assert.Equal("Survey crew is on site this week", view["authorisation"]!.S("reason"));

        // The starter still acknowledges the warning; the authorisation is then used by this start.
        var noAck = await data.Move(TestData.Alex, t, TaskStatuses.InProgress,
            new { acknowledgeReadiness = false, reason = "Starting under the PM's authorisation" }, expect: 422);
        Assert.Equal(["acknowledgement"], Needs(noAck));
        var started = await data.Move(TestData.Alex, t, TaskStatuses.InProgress,
            new { acknowledgeReadiness = true, reason = "Starting under the PM's authorisation" });
        Assert.Equal(TaskStatuses.InProgress, started.S("status"));
        var row = Assert.Single(Authorisations(id));
        Assert.Equal((data.User(TestData.Pm), data.User(TestData.Alex), ReadinessState.NeedsAssessment),
            (row.AuthorisedBy, row.StartedBy!.Value, row.ReadinessAtAuthorisation));
        Assert.NotNull(row.StartedAt);
        var audit = f.Db(db => db.ActivityLog.AsNoTracking().Single(l => l.ItemId == id && l.Action == "StatusChanged"));
        Assert.Equal("Starting under the PM's authorisation", audit.Reason);

        // Used once: cancelled and restored, the work needs a fresh authorisation to start again.
        await data.Move(TestData.Pm, t, TaskStatuses.Cancelled, new { reason = "Scope moved to phase 2" });
        await data.Move(TestData.Pm, t, TaskStatuses.NotStarted, new { reason = "Scope returned to phase 1" });
        var again = await data.Move(TestData.Alex, t, TaskStatuses.InProgress,
            new { acknowledgeReadiness = true, reason = "Restarting the survey" }, expect: 422);
        Assert.Equal(["authorisation"], Needs(again));
    }

    [Fact]
    public async Task Lead_authorises_inline_after_access_and_review_guards()
    {
        var p = await data.Project();
        var t = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var id = t.G("id");
        var ack = new { acknowledgeReadiness = true, reason = "Lead accepts the readiness risk" };

        // Access, review and lifecycle guards answer first; readiness never stands in for them.
        await data.Move(TestData.Jill, t, TaskStatuses.InProgress, ack, expect: 403);
        await data.Move(TestData.Rita, t, TaskStatuses.InProgress, ack, expect: 403);
        var review = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex), requiresReview = true, reviewerId = data.User(TestData.Diane) });
        Assert.Equal("illegal_transition", (await data.Move(TestData.Marc, review, TaskStatuses.Complete, ack, expect: 422)).S("code"));

        var lead = await data.Move(TestData.Marc, t, TaskStatuses.InProgress, new { acknowledgeReadiness = false }, expect: 422);
        Assert.Equal(["acknowledgement", "reason"], Needs(lead));
        Assert.True(lead["canAuthorise"]!.GetValue<bool>());
        await data.Move(TestData.Marc, t, TaskStatuses.InProgress, ack);
        var row = Assert.Single(Authorisations(id));
        Assert.Equal((data.User(TestData.Marc), data.User(TestData.Marc), "Lead accepts the readiness risk"),
            (row.AuthorisedBy, row.StartedBy!.Value, row.Reason));

        // A one-step Complete passes through In Progress, so it is a start and is guarded the same way.
        var quick = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var checkbox = await data.Move(TestData.Alex, quick, TaskStatuses.Complete, new { acknowledgeReadiness = false }, expect: 422);
        Assert.Equal("start_authorisation_required", checkbox.S("code"));
        Assert.Equal(TaskStatuses.NotStarted, Status(quick.G("id")));

        // A lead outside the task's discipline is not its authoriser.
        var other = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        await data.AuthoriseStart(other, TestData.Omar, reason: "Electrical lead approving civil work", expect: 403);
    }

    [Fact]
    public async Task Authorisation_covers_only_the_readiness_it_acknowledged()
    {
        var p = await data.Project();
        var t = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var id = t.G("id");
        await data.AuthoriseStart(t, TestData.Pm, reason: "Start before the assessment is written");
        await Assess(p.Id, id);
        var view = await View(TestData.Alex, id);
        Assert.Equal(ReadinessState.NeedsAssessment, view.S("readinessState"));
        Assert.True(view["assessed"]!.GetValue<bool>());
        Assert.NotEmpty(view["unknown"]!.AsArray());
        Assert.Null(view["authorisation"]);
        var stale = await data.Move(TestData.Alex, t, TaskStatuses.InProgress,
            new { acknowledgeReadiness = true, reason = "Starting under the earlier authorisation" }, expect: 422);
        Assert.Equal(["authorisation"], Needs(stale));
        Assert.Contains("needs assessment: ", stale.S("detail"));
        Assert.Null(Assert.Single(Authorisations(id)).StartedAt);
    }

    [Fact]
    public async Task Ready_work_starts_normally_and_needs_no_authorisation()
    {
        var p = await data.Project();
        var t = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var id = t.G("id");
        await Ready(p.Id, id);
        var view = await View(TestData.Alex, id);
        Assert.Equal(ReadinessState.Ready, view.S("readinessState"));
        Assert.False(view["needsAuthorisation"]!.GetValue<bool>());
        Assert.Equal("start_authorisation_not_needed", (await data.AuthoriseStart(t, TestData.Pm, reason: "Not needed here", expect: 422)).S("code"));
        Assert.Equal(TaskStatuses.InProgress, (await data.Move(TestData.Alex, t, TaskStatuses.InProgress, new { acknowledgeReadiness = false })).S("status"));
        Assert.Empty(Authorisations(id));
        Assert.Equal("start_authorisation_not_started", (await data.AuthoriseStart(t, TestData.Pm, reason: "Already running", expect: 422)).S("code"));
    }

    [Fact]
    public async Task Proceed_under_assumption_starts_normally()
    {
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var p = await data.Project();
        var owner = data.User(TestData.Alex);
        var civil = data.ProjectDiscipline(p.Id, "Civil");
        var t = await data.NewTask(p.Id, extra: new { assigneeId = owner });
        var id = t.G("id");
        var basisRoot = $"/api/v1/projects/{p.Id}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Start grading", "Assume utility depth", null, null, null, null, null, null, today.AddDays(3), null);
        var entry = await (await f.As(TestData.Pm).Post(basisRoot, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption,
            "Utility depth for start", owner, civil, data.User(TestData.Marc), input, null))).Json();
        var version = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entry.G("id")));
        await (await f.As(TestData.Marc).Post($"{basisRoot}/{entry.G("id")}/versions/{version.Id}/proceed",
            new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(), version.RowVersion, input.Scope, owner, today.AddDays(5), "Limited preliminary work"))).Json();
        await (await f.As(TestData.Alex).Post($"{basisRoot}/{entry.G("id")}/uses",
            new DesignBasisEndpoints.UseBody(Guid.NewGuid(), version.Id, "Task", id, "Preliminary layout under assumption"))).Json();
        var assessment = await Assess(p.Id, id);
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            {
                check.Applies = check.Code is ReadinessCheckCode.Basis or ReadinessCheckCode.ProductionOwner;
                check.Satisfied = check.Applies == true ? check.Code == ReadinessCheckCode.ProductionOwner : null;
            }
            await db.SaveChangesAsync(); return 0;
        });
        var path = $"/api/v1/projects/{p.Id}/readiness/Task/{id}";
        var current = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        await (await f.As(TestData.Pm).Post(path + "/exceptions", new ReadinessEndpoints.ExceptionBody(Guid.NewGuid(),
            current["assessment"]!.I("rowVersion"), version.Id, version.RowVersion, data.User(TestData.Marc),
            "Only preliminary grading layout", "Utility location could alter grading", today.AddDays(1)))).Json();
        Assert.Equal(ReadinessState.ProceedUnderAssumption, (await View(TestData.Alex, id)).S("readinessState"));
        Assert.Equal(TaskStatuses.InProgress, (await data.Move(TestData.Alex, t, TaskStatuses.InProgress, new { acknowledgeReadiness = false })).S("status"));
        Assert.Empty(Authorisations(id));
    }

    [Fact]
    public async Task Bulk_start_reports_refusals_and_starts_none_of_the_refused_tasks()
    {
        var p = await data.Project();
        var ready = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var open = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        await Ready(p.Id, ready.G("id"));
        var bulk = $"/api/v1/projects/{p.Id}/tasks/bulk";

        var first = await (await f.As(TestData.Alex).Post(bulk, new { taskIds = new[] { ready.G("id"), open.G("id") }, operation = "transition",
            @params = new { toStatus = TaskStatuses.InProgress } })).Json();
        Assert.Equal(1, first.I("updated"));
        var skipped = Assert.Single(first["skipped"]!.AsArray())!;
        Assert.Equal(open.G("id"), skipped.G("id"));
        Assert.Contains("is Needs Assessment", skipped.S("reason"));
        Assert.Equal((TaskStatuses.InProgress, TaskStatuses.NotStarted), (Status(ready.G("id")), Status(open.G("id"))));

        var body = new { taskIds = new[] { open.G("id") }, operation = "transition",
            @params = new { toStatus = TaskStatuses.InProgress, acknowledgeReadiness = true }, reason = "Survey crew available today" };
        var performer = await (await f.As(TestData.Alex).Post(bulk, body)).Json();
        Assert.Equal(0, performer.I("updated"));
        Assert.Empty(Authorisations(open.G("id")));
        var lead = await (await f.As(TestData.Marc).Post(bulk, body)).Json();
        Assert.Equal(1, lead.I("updated"));
        Assert.Equal(TaskStatuses.InProgress, Status(open.G("id")));
        Assert.Equal(data.User(TestData.Marc), Assert.Single(Authorisations(open.G("id"))).AuthorisedBy);
    }

    [Fact]
    public async Task Start_authorisation_history_is_immutable()
    {
        var p = await data.Project();
        var t = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var id = (await data.AuthoriseStart(t, TestData.Pm, reason: "Survey crew is on site")).G("id");
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            (await db.TaskStartAuthorisations.SingleAsync(a => a.Id == id)).Reason = "Edited afterwards";
            await db.SaveChangesAsync(); return 0;
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.TaskStartAuthorisations.Remove(await db.TaskStartAuthorisations.SingleAsync(a => a.Id == id));
            await db.SaveChangesAsync(); return 0;
        }));
        await data.Move(TestData.Alex, t, TaskStatuses.InProgress, new { acknowledgeReadiness = true, reason = "Starting as authorised" });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            (await db.TaskStartAuthorisations.SingleAsync(a => a.Id == id)).StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); return 0;
        }));
    }
}
