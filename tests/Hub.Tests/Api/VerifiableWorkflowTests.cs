using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// Focused packet 048 API checks. Each test owns a disposable database so feature flags cannot leak.
public sealed class VerifiableWorkflowTests
{
    static async Task<(HubFactory Factory, TestData Data)> Enabled()
    {
        var f = new HubFactory();
        var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.commitment_reliability", new { value = true })).EnsureSuccessStatusCode();
        (await admin.Put("/api/v1/admin/settings/flag.allocation_preview", new { value = true })).EnsureSuccessStatusCode();
        return (f, new TestData(f));
    }

    static async Task<(Project Project, Guid TaskId)> TaskFixture(HubFactory f, TestData d)
    {
        var project = await d.Project();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex), estimatedHours = 4m, dueDate = "2026-09-14" });
        return (project, task.G("id"));
    }

    [Fact]
    [Trait("flag-on", "commitment_reliability")]
    public async Task Not_met_requires_active_category_and_records_the_category()
    {
        var (f, d) = await Enabled();
        using (f)
        {
            var (project, taskId) = await TaskFixture(f, d);
            var category = new CommitmentMissCategory { Name = "Dependency", SortOrder = 1 };
            var snapshot = new WeeklyPlanSnapshot { ProjectId = project.Id, WeekStart = new DateOnly(2026, 9, 14), CapturedAt = f.Clock.Now, CapturedBy = d.User(TestData.Pm), CommittedCount = 1 };
            var row = new OutputCommitment { ProjectId = project.Id, Key = "C-0001", TargetType = "Task", TargetId = taskId, PerformerId = d.User(TestData.Alex), WeekStart = snapshot.WeekStart, TargetDate = snapshot.WeekStart, State = CommitmentState.Committed, SnapshotId = snapshot.Id };
            await f.DbAsync(async db => { db.Set<CommitmentMissCategory>().Add(category); db.WeeklyPlanSnapshots.Add(snapshot); db.OutputCommitments.Add(row); return await db.SaveChangesAsync(); });
            var path = $"/api/v1/projects/{project.Id}/weekly-commitments/{row.Id}/transition";
            await (await f.As(TestData.Alex).Post(path, new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), row.RowVersion, CommitmentState.NotMet, "Dependency slipped", null))).Json(400);
            var currentVersion = await f.DbAsync(db => db.OutputCommitments.Where(x => x.Id == row.Id).Select(x => x.RowVersion).SingleAsync());
            await (await f.As(TestData.Alex).Post(path, new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), currentVersion, CommitmentState.NotMet, "Dependency slipped", null, category.Id))).Json();
            Assert.Equal(CommitmentState.NotMet, await f.DbAsync(db => db.OutputCommitments.Where(x => x.Id == row.Id).Select(x => x.State).SingleAsync()));
            Assert.Equal(category.Id, await f.DbAsync(db => db.OutputCommitmentEvents.Where(e => e.CommitmentId == row.Id).Select(e => e.MissCategoryId).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "commitment_reliability")]
    public async Task Confirmation_is_independent_of_performer_and_requires_manager()
    {
        var (f, d) = await Enabled();
        using (f)
        {
            var admin = f.As(TestData.Admin);
            (await admin.Put("/api/v1/admin/settings/commitment_met_confirmation", new { value = true })).EnsureSuccessStatusCode();
            var (project, taskId) = await TaskFixture(f, d);
            var snapshot = new WeeklyPlanSnapshot { ProjectId = project.Id, WeekStart = new DateOnly(2026, 9, 14), CapturedAt = f.Clock.Now, CapturedBy = d.User(TestData.Pm), CommittedCount = 1 };
            var row = new OutputCommitment { ProjectId = project.Id, Key = "C-0002", TargetType = "Task", TargetId = taskId, PerformerId = d.User(TestData.Alex), WeekStart = snapshot.WeekStart, TargetDate = snapshot.WeekStart, State = CommitmentState.Committed, SnapshotId = snapshot.Id };
            await f.DbAsync(async db => { db.WeeklyPlanSnapshots.Add(snapshot); db.OutputCommitments.Add(row); return await db.SaveChangesAsync(); });
            var rowVersion = await f.DbAsync(db => db.OutputCommitments.Where(x => x.Id == row.Id).Select(x => x.RowVersion).SingleAsync());
            var path = $"/api/v1/projects/{project.Id}/weekly-commitments/{row.Id}/confirm";
            var moved = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/weekly-commitments/{row.Id}/transition",
                new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), rowVersion, CommitmentState.Met, "Performer completed output", "https://example.test/evidence"))).Json();
            rowVersion = moved.I("rowVersion");
            Assert.Equal(CommitmentState.AwaitingConfirmation, await f.DbAsync(db => db.OutputCommitments.Where(x => x.Id == row.Id).Select(x => x.State).SingleAsync()));
            await (await f.As(TestData.Alex).Post(path, new CommitmentReliabilityEndpoints.ConfirmBody(Guid.NewGuid(), rowVersion))).Json(403);
            await (await f.As(TestData.Pm).Post(path, new CommitmentReliabilityEndpoints.ConfirmBody(Guid.NewGuid(), rowVersion))).Json();
            Assert.Equal(CommitmentState.Met, await f.DbAsync(db => db.OutputCommitments.Where(x => x.Id == row.Id).Select(x => x.State).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-on", "commitment_reliability")]
    public async Task Reliability_returns_null_percent_for_zero_commitments()
    {
        var (f, d) = await Enabled();
        using (f)
        {
            var project = await d.Project();
            await f.DbAsync(async db => { db.WeeklyPlanSnapshots.Add(new WeeklyPlanSnapshot { ProjectId = project.Id, WeekStart = new DateOnly(2026, 9, 14), CapturedAt = f.Clock.Now, CapturedBy = d.User(TestData.Pm), CommittedCount = 0 }); return await db.SaveChangesAsync(); });
            var body = await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}/commitment-reliability")).Json();
            var reliability = body["snapshots"]!.AsArray().Single()!["reliability"]!;
            Assert.Null(reliability["percent"]);
            Assert.Equal(0, reliability["committed"]!.GetValue<int>());
        }
    }

    [Fact]
    [Trait("flag-on", "allocation_preview")]
    public async Task Allocation_preview_returns_week_projection_and_rolls_back_writes()
    {
        var (f, d) = await Enabled();
        using (f)
        {
            var (project, taskId) = await TaskFixture(f, d);
            var day = new DateOnly(2026, 9, 14);
            var root = $"/api/v1/projects/{project.Id}/allocations";
            var body = new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Alex), AllocationPurpose.Production, day, day, 4, [], [new("Task", taskId, day)], null);
            var preview = await (await f.As(TestData.Pm).Post(root + "?preview=true", body)).Json();
            Assert.True(preview["preview"]!.GetValue<bool>());
            Assert.NotEmpty(preview["weeks"]!.AsArray());
            Assert.Empty(await f.DbAsync(db => db.Allocations.Where(a => a.ProjectId == project.Id).ToListAsync()));
            var saved = await (await f.As(TestData.Pm).Post(root, body with { RequestId = Guid.NewGuid() })).Json();
            Assert.Equal(1, await f.DbAsync(db => db.Allocations.CountAsync(a => a.Id == saved.G("id"))));
            var id = saved.G("id");
            var dated = await (await f.As(TestData.Sam).GetAsync(root + $"/{id}/confirmation-preview")).Json();
            var confirm = new AllocationEndpoints.ConfirmBody(Guid.NewGuid(), dated.I("rowVersion"), dated["days"]!.AsArray().Select(x => new AllocationEndpoints.DateVersionInput(DateOnly.Parse(x!.S("date")), x.I("dateVersion"))).ToArray(), null);
            var projected = await (await f.As(TestData.Sam).Post(root + $"/{id}/confirm?preview=true", confirm)).Json();
            Assert.True(projected["preview"]!.GetValue<bool>());
            Assert.Equal(AllocationStatus.Proposed, await f.DbAsync(db => db.Allocations.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));
            await (await f.As(TestData.Sam).Post(root + $"/{id}/confirm", confirm)).Json();
            Assert.Equal(AllocationStatus.Confirmed, await f.DbAsync(db => db.Allocations.Where(a => a.Id == id).Select(a => a.Status).SingleAsync()));
        }
    }

    [Fact]
    [Trait("flag-off", "verifiable_log")]
    [Trait("flag-off", "commitment_reliability")]
    [Trait("flag-off", "allocation_preview")]
    [Trait("flag-off", "revision_check")]
    public async Task New_workflow_routes_are_dark_by_default()
    {
        using var f = new HubFactory(); var d = new TestData(f); var p = await d.Project(); var admin = f.As(TestData.Admin);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/audit-chains")).StatusCode);
        var activity = await (await admin.GetAsync($"/api/v1/reports/project-activity-log?projectId={p.Id}")).Json();
        Assert.DoesNotContain(activity["parameters"]!.AsArray(), x => x!["label"]!.GetValue<string>().StartsWith("Log integrity"));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/projects/{p.Id}/commitment-reliability")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/projects/{p.Id}/revision-check")).StatusCode);
        var preview = new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Alex), AllocationPurpose.Production,
            new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 14), 4, [], [], null);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/allocations?preview=true", preview)).StatusCode);
    }

    [Fact]
    [Trait("flag-on", "revision_check")]
    public async Task Nonstandard_revision_does_not_block_issue_and_clearing_convention_removes_marks()
    {
        using var f = new HubFactory(); var d = new TestData(f); var p = await d.Project(); var pm = f.As(TestData.Pm);
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.revision_check", new { value = true })).Json();
        var deliverable = await (await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Revision package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), requiresReview = false })).Json(201);
        await (await pm.Put($"/api/v1/projects/{p.Id}/revision-check", new RevisionCheckEndpoints.ConventionBody(d.Version(p.Id), "letters"))).Json();
        var id = deliverable.G("id"); var version = await f.DbAsync(db => db.Deliverables.Where(x => x.Id == id).Select(x => x.RowVersion).SingleAsync());
        await (await pm.Post($"/api/v1/deliverables/{id}/issue", new { revision = "Unexpected-99", issuedTo = "Synthetic recipient", confirmOpenTasks = true, rowVersion = version })).Json();
        var marked = await (await pm.GetAsync($"/api/v1/projects/{p.Id}/revision-check")).Json();
        Assert.NotEmpty(marked["deliverables"]!.AsArray().Single()!["marks"]!.AsArray());
        await (await pm.Put($"/api/v1/projects/{p.Id}/revision-check", new RevisionCheckEndpoints.ConventionBody(d.Version(p.Id), null))).Json();
        var cleared = await (await pm.GetAsync($"/api/v1/projects/{p.Id}/revision-check")).Json();
        Assert.Null(cleared["convention"]); Assert.Empty(cleared["deliverables"]!.AsArray());
    }

    [Fact]
    [Trait("flag-on", "revision_check")]
    public async Task Deliverable_without_a_revision_or_issues_reads_without_marks()
    {
        using var f = new HubFactory(); var d = new TestData(f); var p = await d.Project(); var pm = f.As(TestData.Pm);
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.revision_check", new { value = true })).Json();
        var deliverable = await (await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Unrevised package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), requiresReview = false })).Json(201);
        await (await pm.Put($"/api/v1/projects/{p.Id}/revision-check", new RevisionCheckEndpoints.ConventionBody(d.Version(p.Id), "letters"))).Json();
        var read = await (await pm.GetAsync($"/api/v1/projects/{p.Id}/revision-check")).Json();
        var row = read["deliverables"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == deliverable.G("id"))!;
        Assert.Empty(row["marks"]!.AsArray()); Assert.Empty(row["issues"]!.AsArray());
        Assert.Equal(System.Net.HttpStatusCode.OK, (await pm.GetAsync($"/api/v1/projects/{p.Id}/deliverables?revisionWarning=true")).StatusCode);
        using var scope = f.Services.CreateScope();
        await new RevisionRecheckJob().Run(scope.ServiceProvider, default); // the per-minute recheck reads the same marks
    }
}
