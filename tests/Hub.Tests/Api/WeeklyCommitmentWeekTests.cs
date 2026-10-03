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
    public async Task Recorded_weeks_and_target_window_pages_preserve_snapshots_export_scope_and_privacy()
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

        var first = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Wednesday) with { TargetDate = Monday })).G("id");
        var second = (await Post(TestData.Pm, $"{root}/Task/{task}", Proposal(task, Wednesday) with { TargetDate = Monday.AddDays(1) })).G("id");
        var performer = data.User(TestData.Alex);
        // Synthetic signed rows put 500 out-of-window promises before the visible rows. This test exercises reads,
        // while signature/readiness guards are covered by the weekly lifecycle tests.
        var spillover = Enumerable.Range(100, 500).Select(seq => new OutputCommitment
        {
            ProjectId = project.Id, Seq = seq, Key = $"{project.ProjectNumber}-WC{seq:000}", TargetType = "Task", TargetId = task,
            PerformerId = performer, WeekStart = Wednesday, TargetDate = Wednesday.AddDays(2),
            IntendedOutput = "Earlier output", CompletionCriteria = "Earlier output checked",
            ReadinessAtCommit = ReadinessState.Ready, State = CommitmentState.Committed,
        }).ToArray();
        await f.DbAsync(async db =>
        {
            foreach (var row in await db.OutputCommitments.Where(c => c.ProjectId == project.Id).ToListAsync())
            {
                row.State = CommitmentState.Committed;
                row.ReadinessAtCommit = ReadinessState.Ready;
            }
            db.OutputCommitments.AddRange(spillover);
            return await db.SaveChangesAsync();
        });
        var recordedSnapshot = (await Post(TestData.Pm, $"{root}/snapshot",
            new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), Monday, "Close the recorded week"))).G("id");
        var currentSnapshot = (await Post(TestData.Pm, $"{root}/snapshot",
            new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), Wednesday, "Close the current week"))).G("id");
        await Post(TestData.Alex, $"{root}/{current}/transition", new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(),
            Version<OutputCommitment>(current), CommitmentState.Met, "Earlier output checked", "https://example.test/earlier-output"));
        await Post(TestData.Alex, $"{root}/{spillover[0].Id}/transition", new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(),
            Version<OutputCommitment>(spillover[0].Id), CommitmentState.Withdrawn, "Earlier scope withdrawn", null));

        var recordedRange = $"from={Monday.AddDays(-6):yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}";
        var legacyList = await (await f.As(TestData.Pm).GetAsync($"{root}?{recordedRange}")).Json();
        Assert.Equal(504, legacyList.I("total"));
        Assert.Equal(500, legacyList["commitments"]!.AsArray().Count);
        Assert.True(legacyList["truncated"]!.GetValue<bool>());
        Assert.Null(legacyList["page"]); // no paging fields are added to legacy calls
        var targetRange = $"targetFrom={Monday:yyyy-MM-dd}&targetTo={Monday.AddDays(6):yyyy-MM-dd}";
        var visibleIds = new[] { first, second, legacy };
        for (var page = 1; page <= visibleIds.Length; page++)
        {
            var listed = await (await f.As(TestData.Pm).GetAsync($"{root}?{recordedRange}&{targetRange}&page={page}&pageSize=1")).Json();
            Assert.Equal(page, listed.I("page"));
            Assert.Equal(1, listed.I("pageSize"));
            Assert.Equal(3, listed.I("total"));
            Assert.False(listed["truncated"]!.GetValue<bool>());
            Assert.Equal(visibleIds[page - 1], listed["commitments"]!.AsArray().Single()!.G("id"));
            var snapshots = listed["snapshots"]!.AsArray();
            Assert.Equal(2, snapshots.Count);
            var recorded = snapshots.Single(s => s!.G("id") == recordedSnapshot)!;
            Assert.Equal(1, recorded.I("committedCount"));
            var currentWeek = snapshots.Single(s => s!.G("id") == currentSnapshot)!;
            Assert.Equal(503, currentWeek.I("committedCount"));
            Assert.Equal(1, currentWeek.I("met")); // outcome is outside the target-date window and every page
            Assert.Equal(1, currentWeek.I("withdrawn"));
        }
        var exportPath = $"{root}/export?from={Monday:yyyy-MM-dd}&to={Monday.AddDays(6):yyyy-MM-dd}&format=csv&page=2&pageSize=1";
        var csv = System.Text.Encoding.UTF8.GetString(await (await f.As(TestData.Pm).GetAsync(exportPath)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());
        var keys = f.Db(db => db.OutputCommitments.Where(c => c.ProjectId == project.Id).ToDictionary(c => c.Id, c => c.Key));
        foreach (var id in visibleIds) Assert.Contains(keys[id], csv); // export contains the whole filtered set, not page 2
        Assert.DoesNotContain(keys[current], csv);
        foreach (var row in spillover) Assert.DoesNotContain(row.Key, csv);
        Assert.Equal(1, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.Id == recordedSnapshot).CommittedCount));
        Assert.Equal(503, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.Id == currentSnapshot).CommittedCount));
        Assert.Equal(Monday, f.Db(db => db.OutputCommitments.Single(c => c.Id == legacy).WeekStart));
        Assert.Equal(Monday, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.Id == recordedSnapshot).WeekStart));

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Rita).GetAsync($"{root}?{recordedRange}&{targetRange}&page=2&pageSize=1")).Json(404);
        await (await f.As(TestData.Rita).GetAsync($"{root}?weekStart={Monday:yyyy-MM-dd}")).Json(404);
        await (await f.As(TestData.Rita).GetAsync(exportPath)).Json(404);
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
