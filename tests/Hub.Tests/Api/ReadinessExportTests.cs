using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-MDC-06: the readiness page's constraint, ready-output and promise lists export the same permitted rows the page shows.
[Collection("api")]
public sealed class ReadinessExportTests(HubFactory f)
{
    readonly TestData data = new(f);
    static readonly DateOnly From = new(2026, 9, 14), To = new(2026, 10, 4);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    async Task<string> Csv(string who, string path) => System.Text.Encoding.UTF8.GetString(await (await f.As(who).GetAsync(path)).EnsureSuccessStatusCode().Content.ReadAsByteArrayAsync());

    [Fact]
    public async Task Exports_reconcile_with_the_readiness_window_and_respect_project_access()
    {
        var project = await data.Project();
        var root = $"/api/v1/projects/{project.Id}";
        var alex = data.User(TestData.Alex);
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = alex, dueDate = From.AddDays(6) })).G("id");
        var readiness = $"{root}/readiness/Task/{task}";
        var assessment = (await Post(TestData.Alex, readiness, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(task),
            "Grading layout", "Layout checked"))).G("id");
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment).ToListAsync())
                check.Applies = check.Code == ReadinessCheckCode.ProductionOwner;
            return await db.SaveChangesAsync();
        });
        var window = $"{root}/readiness/window?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}";
        var ready = await (await f.As(TestData.Pm).GetAsync(window)).Json();
        Assert.Equal(task, ready["readyOutputs"]!.AsArray().Single()!.G("targetId"));
        var readyCsv = await Csv(TestData.Pm, $"{root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=ready&format=csv");
        var taskKey = f.Db(db => db.Tasks.Single(t => t.Id == task).Key);
        Assert.Contains(taskKey, readyCsv);
        Assert.Contains("Grading layout", readyCsv);

        var decision = (await Post(TestData.Pm, $"{root}/decisions", new { subject = "Confirm outfall", description = "Outfall decides grading",
            ownerUserId = alex, requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Grading waits" }, 201)).G("id");
        var constraint = (await Post(TestData.Alex, $"{readiness}/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(task),
            "Decision", "Await the outfall decision", data.User(TestData.Pm), From.AddDays(3), "https://example.test/outfall", ItemType.Decision, decision))).G("id");
        var cancelled = (await Post(TestData.Alex, $"{readiness}/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(task),
            "Scope", "Withdrawn scope question", data.User(TestData.Pm), From.AddDays(3), "https://example.test/scope"))).G("id");
        await Post(TestData.Pm, $"{readiness}/constraints/{cancelled}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(),
            Version<WorkConstraint>(cancelled), ConstraintState.Cancelled, "Scope question withdrawn", null));
        var listed = (await (await f.As(TestData.Pm).GetAsync(window)).Json())["constraints"]!.AsArray().Select(c => c!.S("key")).ToArray();
        var constraintsCsv = await Csv(TestData.Pm, $"{root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=constraints&format=csv");
        var keys = f.Db(db => db.WorkConstraints.Where(c => c.ProjectId == project.Id).ToDictionary(c => c.Id, c => c.Key));
        Assert.Equal(new[] { keys[constraint] }, listed);
        Assert.Contains(keys[constraint], constraintsCsv);
        Assert.DoesNotContain(keys[cancelled], constraintsCsv);
        Assert.Contains("Confirm outfall (Pending)", constraintsCsv);
        Assert.Contains("Priya", constraintsCsv); // removal owner by name

        var commitments = $"{root}/weekly-commitments";
        WeeklyCommitmentsEndpoints.ProposeBody Promise(DateOnly week) => new(Guid.NewGuid(), Version<WorkTask>(task), week, week.AddDays(2), "Layout issued", "Checked");
        var inside = (await Post(TestData.Pm, $"{commitments}/Task/{task}", Promise(From.AddDays(7)))).G("id");
        var outside = (await Post(TestData.Pm, $"{commitments}/Task/{task}", Promise(To.AddDays(1)))).G("id");
        var promiseKeys = f.Db(db => db.OutputCommitments.Where(c => c.ProjectId == project.Id).ToDictionary(c => c.Id, c => c.Key));
        var promisesCsv = await Csv(TestData.Pm, $"{commitments}/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&format=csv");
        Assert.Contains(promiseKeys[inside], promisesCsv);
        Assert.DoesNotContain(promiseKeys[outside], promisesCsv);
        Assert.Equal(400, HttpStatus(await f.As(TestData.Pm).GetAsync($"{commitments}/export?from={From:yyyy-MM-dd}&to={From.AddDays(90):yyyy-MM-dd}")));
        Assert.Equal(400, HttpStatus(await f.As(TestData.Pm).GetAsync($"{root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=everything")));
        Assert.Equal(200, HttpStatus(await f.As(TestData.Pm).GetAsync($"{commitments}/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&format=xlsx")));

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(404, HttpStatus(await f.As(TestData.Rita).GetAsync($"{root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=constraints")));
        Assert.Equal(404, HttpStatus(await f.As(TestData.Rita).GetAsync($"{commitments}/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}")));
    }

    static int HttpStatus(HttpResponseMessage r) => (int)r.StatusCode;
}
