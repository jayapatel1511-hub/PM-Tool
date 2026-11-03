using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// API coverage for the ChangeNotice / ChangeAssessment context adapter.
/// The tests use the existing source-revision and publish workflow so assessment rows
/// retain the same foreign keys and ownership semantics as production records.
public sealed class ChangeContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public ChangeContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    static readonly DateOnly Due = new(2026, 9, 24);
    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);

    sealed record Setup(Project Project, Guid Deliverable, Guid OldRevision, Guid NewRevision, Guid Task, Guid Notice, Guid Assessment);

    async Task<Setup> New()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Context survey", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(),
            ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false
        })).Json(201)).G("id");
        var old = (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "survey", "Context survey", "A",
            "https://example.test/context-A.pdf", "Survey team", "East corridor", null, null, null, null, null, null))).G("id");
        var next = await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "survey", "Context survey", "B",
            "https://example.test/context-B.pdf", "Survey team", "East corridor", null, old, f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion),
            "Updated survey", Due.AddDays(-7), Due));
        var task = (await d.NewTask(p.Id, TestData.Alex, new { assigneeId = d.User(TestData.Alex) })).G("id");
        var notice = (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/changes/{next.G("id")}/publish", new ChangeEndpoints.PublishBody(
            Guid.NewGuid(), Version<ChangeNotice>(next.G("id")), f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion),
            [new ChangeEndpoints.TargetRef("Task", task, Version<WorkTask>(task))]))).G("id");
        var assessment = f.Db(db => db.ChangeAssessments.Single(a => a.ChangeNoticeId == notice && a.TargetId == task).Id);
        return new(p, deliverable, old, f.Db(db => db.ChangeNotices.Single(c => c.Id == notice).NewRevisionId), task, notice, assessment);
    }

    string NoticePath(Setup s) => $"/api/v1/projects/{s.Project.Id}/changes/{s.Notice}";
    string AssessmentPath(Setup s) => $"/api/v1/projects/{s.Project.Id}/change-assessments/{s.Assessment}";

    [Fact]
    public async Task Flag_off_omits_context_and_new_child_route_is_unavailable()
    {
        var s = await New();
        var off = await Get(TestData.Alex, NoticePath(s));
        Assert.Null(off["context"]);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync(AssessmentPath(s))).StatusCode);

        await Enable();
        var on = await Get(TestData.Alex, NoticePath(s));
        Assert.NotNull(on["context"]);
        foreach (var key in new[] { "actorId", "canWrite", "canPublish", "canManage", "eligiblePeople" })
            Assert.Equal(off[key]!.ToJsonString(), on[key]!.ToJsonString());
        var child = await Get(TestData.Alex, AssessmentPath(s));
        Assert.Equal(s.Assessment, child.G("id"));
        Assert.Equal(s.Notice, child.G("noticeId"));
        Assert.NotNull(child["context"]);
        Assert.Equal(Due.ToString("yyyy-MM-dd"), child["context"]!.S("neededBy"));
        Assert.Equal(d.User(TestData.Alex), child["context"]!["owner"]!.G("id"));
    }

    [Fact]
    public async Task Pending_and_clarification_keep_recorded_owner_and_exact_date()
    {
        var s = await New(); await Enable();
        var owner = d.User(TestData.Alex);
        var second = d.User(TestData.Omar);
        var secondTask = (await d.NewTask(s.Project.Id, TestData.Omar, new { assigneeId = d.User(TestData.Omar) }, "Electrical")).G("id");
        Guid secondAssessment = Guid.Empty;
        await f.DbAsync(async db =>
        {
            var assessment = await db.ChangeAssessments.SingleAsync(a => a.Id == s.Assessment);
            assessment.Status = AssessmentStatus.Clarification; assessment.OwnerId = owner;
            var other = new ChangeAssessment { ProjectId = s.Project.Id, ChangeNoticeId = s.Notice, TargetType = "Task", TargetId = secondTask,
                OwnerId = second, RevisionUsedId = s.NewRevision, Status = AssessmentStatus.Pending, RetainOldRevision = false };
            db.ChangeAssessments.Add(other); await db.SaveChangesAsync(); secondAssessment = other.Id; return 0;
        });
        var notice = await Get(TestData.Alex, NoticePath(s));
        Assert.Equal(Due.ToString("yyyy-MM-dd"), notice["context"]!.S("neededBy"));
        var ids = notice["context"]!["inputs"]!.AsArray().Select(x => x!.G("id")).ToHashSet();
        Assert.Contains(s.Assessment, ids); Assert.Contains(secondAssessment, ids);
        var names = notice["context"]!["inputs"]!.AsArray().Select(x => x!["nextActors"]![0]!.G("id")).ToHashSet();
        Assert.Contains(owner, names); Assert.Contains(second, names);
    }

    [Fact]
    public async Task Update_required_follows_correction_task_then_recorded_reviewer_without_guessing()
    {
        var s = await New(); await Enable();
        var reviewer = d.User(TestData.Omar);
        await f.DbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == s.Task); task.Status = TaskStatuses.InReview; task.ReviewerId = reviewer;
            var a = await db.ChangeAssessments.SingleAsync(a => a.Id == s.Assessment); a.Status = AssessmentStatus.UpdateRequired; a.OwnerId = d.User(TestData.Alex); a.ReviewerId = reviewer; a.CorrectionTaskId = s.Task; a.Rationale = "Update required"; a.EvidenceUrl = "https://example.test/evidence"; a.RetainOldRevision = true; a.RetentionApprovedBy = d.User(TestData.Marc);
            await db.SaveChangesAsync(); return 0;
        });
        var child = await Get(TestData.Alex, AssessmentPath(s));
        Assert.Equal(reviewer, child["context"]!["nextActors"]![0]!.G("id"));
        Assert.Equal(RecordContextAction.FollowLinkedTask, child["context"]!.S("nextAction"));
        Assert.Equal(s.Task, child["context"]!["inputs"]![0]!.G("id"));
    }

    [Fact]
    public async Task Terminal_notice_retains_owner_date_and_privacy_is_project_scoped()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { (await db.ChangeNotices.SingleAsync(c => c.Id == s.Notice)).Status = ChangeStatus.Closed; await db.SaveChangesAsync(); return 0; });
        var detail = await Get(TestData.Alex, NoticePath(s));
        Assert.True(detail["context"]!["terminal"]!.GetValue<bool>()); Assert.Empty(detail["context"]!["nextActors"]!.AsArray());
        Assert.Equal(Due.ToString("yyyy-MM-dd"), detail["context"]!.S("neededBy"));
        var other = await d.Project();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{other.Id}/change-assessments/{s.Assessment}")).StatusCode);
    }

    [Theory]
    [InlineData("retention")]
    [InlineData("adopt")]
    [InlineData("complete")]
    [InlineData("missing_reviewer")]
    [InlineData("self_review")]
    [InlineData("resolved_reopened")]
    [InlineData("cancelled")]
    [InlineData("deleted")]
    [InlineData("cross_project")]
    [InlineData("inactive_reviewer")]
    public async Task Follow_up_prerequisites_choose_only_the_recorded_canonical_step(string mode)
    {
        var s = await New(); await Enable();
        var correctionProject = mode == "cross_project" ? await d.Project() : s.Project;
        var correction = (await d.NewTask(correctionProject.Id, extra: new { assigneeId = d.User(TestData.Alex) })).G("id");
        var reviewer = d.User(TestData.Omar);
        await f.DbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == correction);
            task.Status = mode is "complete" or "missing_reviewer" or "self_review" or "inactive_reviewer" ? TaskStatuses.Complete : TaskStatuses.InProgress;
            if (mode == "cancelled") task.Status = TaskStatuses.Cancelled;
            if (mode == "deleted") task.DeletedAt = f.Clock.Now;
            var a = await db.ChangeAssessments.SingleAsync(a => a.Id == s.Assessment);
            a.Status = mode == "adopt" ? AssessmentStatus.Unaffected : mode == "resolved_reopened" ? AssessmentStatus.Resolved : AssessmentStatus.UpdateRequired;
            a.CorrectionTaskId = correction; a.ReviewerId = mode == "missing_reviewer" ? null : mode == "self_review" ? a.OwnerId : reviewer;
            a.Rationale = "Recorded disposition"; a.EvidenceUrl = "https://example.test/evidence";
            a.RetainOldRevision = mode != "adopt"; a.RetentionApprovedBy = mode == "retention" ? null : d.User(TestData.Marc);
            if (mode == "inactive_reviewer") (await db.Users.SingleAsync(u => u.Id == reviewer)).IsActive = false;
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await Get(TestData.Pm, AssessmentPath(s)))["context"]!;
        Assert.Equal(d.User(TestData.Alex), context["owner"]!.G("id")); Assert.Equal(Due.ToString("yyyy-MM-dd"), context.S("neededBy"));
        Assert.False(context["terminal"]!.GetValue<bool>());
        var expected = mode switch { "adopt" => RecordContextAction.AdoptRevision, "complete" or "missing_reviewer" => RecordContextAction.VerifyChange, _ => RecordContextAction.NotAssessed };
        Assert.Equal(expected, context.S("nextAction"));
        if (mode == "retention")
        {
            var parent = (await Get(TestData.Pm, NoticePath(s)))["context"]!;
            Assert.Equal("Unassigned", Assert.Single(parent["inputs"]!.AsArray())!.S("actorState"));
        }
        if (mode == "complete") Assert.Equal(reviewer, Assert.Single(context["nextActors"]!.AsArray())!.G("id"));
        if (mode is "retention" or "missing_reviewer") Assert.Empty(context["nextActors"]!.AsArray());
        if (mode is "deleted" or "cross_project") Assert.DoesNotContain(correction.ToString(), context.ToJsonString());
        if (mode == "inactive_reviewer") Assert.Equal("Inactive", context["nextActors"]![0]!.S("state"));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("inactive")]
    [InlineData("deleted")]
    public async Task Unavailable_target_does_not_substitute_a_new_owner(string mode)
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == s.Task);
            if (mode == "changed") task.AssigneeId = d.User(TestData.Diane);
            if (mode == "inactive") (await db.Users.SingleAsync(u => u.Id == d.User(TestData.Alex))).IsActive = false;
            if (mode == "deleted") task.DeletedAt = f.Clock.Now;
            await db.SaveChangesAsync(); return 0;
        });
        var context = (await Get(TestData.Pm, AssessmentPath(s)))["context"]!;
        Assert.Equal(d.User(TestData.Alex), context["owner"]!.G("id")); Assert.Equal(RecordContextAction.NotAssessed, context.S("nextAction"));
        Assert.DoesNotContain(d.User(TestData.Diane).ToString(), context["nextActors"]!.ToJsonString());
        if (mode == "inactive") Assert.Equal("Inactive", context["owner"]!.S("state"));
    }

    [Fact]
    public async Task Child_requires_visible_parent_and_current_project_access()
    {
        var s = await New(); await Enable();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, NoticePath(s), 404); await Get(TestData.Rita, AssessmentPath(s), 404);
        await f.DbAsync(async db => { (await db.ChangeNotices.SingleAsync(c => c.Id == s.Notice)).DeletedAt = f.Clock.Now; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Pm, AssessmentPath(s), 404);
    }
}
