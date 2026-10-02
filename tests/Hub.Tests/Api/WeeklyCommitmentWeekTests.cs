using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-RDY-05 (Jay, 2026-10-01): promise weeks start on the project's coordination day; recorded rows keep their week start.
[Collection("api")]
public sealed class WeeklyCommitmentWeekTests(HubFactory f)
{
    readonly TestData data = new(f);
    static readonly DateOnly Monday = new(2026, 9, 21), Wednesday = new(2026, 9, 16); // the TestClock date 2026-09-14 is a Monday
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    async Task<(Project Project, Guid Task, string Root)> Work(string? coordinationDay)
    {
        var project = await data.Project(tweak: b => b["coordinationDay"] = coordinationDay);
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        return (project, task, $"/api/v1/projects/{project.Id}/weekly-commitments");
    }

    WeeklyCommitmentsEndpoints.ProposeBody Proposal(Guid task, DateOnly week, string? reason = null) =>
        new(Guid.NewGuid(), Version<WorkTask>(task), week, week.AddDays(2), "Layout output", "Layout checked", reason);

    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);

    [Fact]
    public async Task Weeks_start_on_the_project_coordination_day_for_propose_snapshot_and_list()
    {
        var (project, task, root) = await Work("Wednesday");
        var refused = await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Monday), 400);
        Assert.NotNull(refused["errors"]?["weekStart"]);
        var id = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Wednesday))).G("id");
        Assert.Equal(Wednesday, f.Db(db => db.OutputCommitments.Single(c => c.Id == id).WeekStart));

        await Post(TestData.Pm, $"{root}/snapshot", new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), Monday, "Close the week"), 400);
        await (await f.As(TestData.Pm).GetAsync($"{root}?weekStart={Monday:yyyy-MM-dd}")).Json(400);
        var week = await (await f.As(TestData.Pm).GetAsync($"{root}?weekStart={Wednesday:yyyy-MM-dd}")).Json();
        Assert.Equal(id, week["commitments"]!.AsArray().Single()!.G("id"));
        await Post(TestData.Pm, $"{root}/snapshot", new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), Wednesday, "Close the week"));
        Assert.Equal(Wednesday, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.ProjectId == project.Id).WeekStart));
    }

    [Fact]
    public async Task Recorded_weeks_keep_their_start_after_the_coordination_day_changes()
    {
        var (project, task, root) = await Work(null);
        var legacy = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Monday))).G("id");
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).CoordinationDay = "Wednesday"; return await db.SaveChangesAsync(); });

        await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Monday), 400); // new promises use the current day
        var current = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Wednesday))).G("id");
        var range = await (await f.As(TestData.Pm).GetAsync($"{root}?from={Wednesday.AddDays(-6):yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}")).Json();
        Assert.Equal(new[] { (current, Wednesday), (legacy, Monday) },
            range["commitments"]!.AsArray().Select(c => (c!.G("id"), DateOnly.Parse(c.S("weekStart")))).ToArray());
        await (await f.As(TestData.Pm).GetAsync($"{root}?weekStart={Monday:yyyy-MM-dd}")).Json();
        await (await f.As(TestData.Pm).GetAsync($"{root}?from={Wednesday:yyyy-MM-dd}&to={Wednesday.AddDays(91):yyyy-MM-dd}")).Json(400);

        await Post(TestData.Pm, $"{root}/snapshot", new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), Monday, "Close the recorded week"));
        Assert.Equal(Monday, f.Db(db => db.OutputCommitments.Single(c => c.Id == legacy).WeekStart));
        Assert.Equal(Monday, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.ProjectId == project.Id).WeekStart));
    }

    [Fact]
    public async Task Complete_project_promise_needs_a_correction_reason()
    {
        var (project, task, root) = await Work(null);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var refused = await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Monday), 400);
        Assert.NotNull(refused["errors"]?["reason"]);
        var id = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Monday, "Record the late closeout promise"))).G("id");
        Assert.Equal(CommitmentState.Proposed, f.Db(db => db.OutputCommitments.Single(c => c.Id == id).State));
    }
}
