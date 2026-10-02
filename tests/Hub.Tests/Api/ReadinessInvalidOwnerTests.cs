using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Finding L6: when assessed work's owner leaves the project or is deactivated, or its discipline is deactivated, every
/// read that evaluates readiness keeps working and reports an unmet Production Owner check; commands still refuse.
[Collection("api")]
public sealed class ReadinessInvalidOwnerTests(HubFactory f)
{
    readonly TestData data = new(f);
    static readonly DateOnly From = new(2026, 9, 14), To = new(2026, 10, 4); // the TestClock date opens the window
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    sealed record Work(Project P, Guid Owner, Guid Task, string Root, string Readiness);

    /// An isolated owner (never a shared seed user, so deactivating them cannot affect other tests) with a Ready task due in the window.
    async Task<Work> ReadyWork()
    {
        var p = await data.Project();
        var owner = new AppUser { Email = $"l6-{Guid.NewGuid():N}@hub.test", DisplayName = "L6 owner", OfficeId = p.OfficeId, WeeklyCapacityHours = 40m };
        await f.DbAsync(async db =>
        {
            db.Users.Add(owner);
            db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = owner.Id, Roles = [ProjectRole.TeamMember],
                PrimaryDisciplineId = data.ProjectDiscipline(p.Id, "Civil") });
            return await db.SaveChangesAsync();
        });
        var task = (await data.NewTask(p.Id, extra: new { assigneeId = owner.Id, dueDate = From.AddDays(6) })).G("id");
        var root = $"/api/v1/projects/{p.Id}";
        var readiness = $"{root}/readiness/Task/{task}";
        var assessment = (await (await f.As(owner.Email).Post(readiness, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(task),
            "Grading layout", "Layout checked"))).Json()).G("id");
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment).ToListAsync())
                check.Applies = check.Code == ReadinessCheckCode.ProductionOwner;
            return await db.SaveChangesAsync();
        });
        var w = new Work(p, owner.Id, task, root, readiness);
        Assert.Equal(ReadinessState.Ready, (await Detail(w))["assessment"]!.S("state"));
        return w;
    }

    async Task<JsonNode> Detail(Work w, string who = TestData.Pm) => await (await f.As(who).GetAsync(w.Readiness)).Json();
    string Window(Work w) => $"{w.Root}/readiness/window?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}";

    /// Every read that evaluates or shows the work answers 200 for the PM and an Admin, with the reason on the owner check.
    async Task AssertReadsSurvive(Work w, string reason)
    {
        foreach (var who in new[] { TestData.Pm, TestData.Admin })
        {
            var detail = await Detail(w, who);
            Assert.Equal(ReadinessState.NotReady, detail["assessment"]!.S("state"));
            Assert.Contains(ReadinessCheckCode.ProductionOwner, detail["blocked"]!.AsArray().Select(x => x!.GetValue<string>()));
            Assert.Equal(reason, detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ProductionOwner)!.S("reason"));
            var window = await (await f.As(who).GetAsync(Window(w))).Json();
            Assert.Empty(window["readyOutputs"]!.AsArray());
            var project = await (await f.As(who).GetAsync($"{w.Root}/discipline-coordination")).Json();
            var row = project["startability"]!.AsArray().Single(r => r!.G("targetId") == w.Task)!;
            Assert.Equal(ReadinessState.NotReady, row.S("state"));
            Assert.Equal(HttpStatusCode.OK, (await f.As(who).GetAsync($"/api/v1/discipline-coordination?projectId={w.P.Id}")).StatusCode);
            foreach (var path in new[] { $"{w.Readiness}/constraints", $"{w.Readiness}/submission-prerequisites",
                $"{w.Root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=ready&format=csv",
                $"{w.Root}/readiness/window/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&list=constraints&format=csv",
                $"{w.Root}/weekly-commitments?from={From.AddDays(-6):yyyy-MM-dd}&to={To:yyyy-MM-dd}",
                $"{w.Root}/weekly-commitments/export?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}&format=csv" })
                Assert.True((await f.As(who).GetAsync(path)).StatusCode == HttpStatusCode.OK, $"{who} GET {path}");
        }
        // Task start still evaluates (and still needs an authorisation), rather than reporting an unevaluated readiness.
        var start = await (await f.As(TestData.Pm).GetAsync($"/api/v1/tasks/{w.Task}/start-readiness")).Json();
        Assert.Equal((ReadinessState.NotReady, true), (start.S("readinessState"), start["needsAuthorisation"]!.GetValue<bool>()));
        Assert.Contains(ReadinessCheckCode.ProductionOwner, start["blocked"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Null(start["note"]);
        // Commands keep refusing the invalid work, as before.
        await (await f.As(TestData.Pm).Post($"{w.Readiness}/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(w.Task),
            "Handoff", "Obtain accepted survey", data.User(TestData.Alex), From.AddDays(3), "https://example.test/survey"))).Json(400);
    }

    [Fact]
    public async Task Owner_removed_from_the_project_is_an_unmet_owner_check_not_an_error()
    {
        var w = await ReadyWork();
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == w.P.Id && m.UserId == w.Owner)).RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync(); });
        await AssertReadsSurvive(w, "The production owner is no longer an active project member.");
    }

    [Fact]
    public async Task Deactivated_owner_is_an_unmet_owner_check_not_an_error()
    {
        var w = await ReadyWork();
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == w.Owner)).IsActive = false; return await db.SaveChangesAsync(); });
        await AssertReadsSurvive(w, "The production owner is no longer an active project member.");
    }

    [Fact]
    public async Task Deactivated_discipline_is_an_unmet_owner_check_not_an_error()
    {
        var w = await ReadyWork();
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(d => d.ProjectId == w.P.Id && d.Id == db.Tasks.Single(t => t.Id == w.Task).ProjectDisciplineId)).IsActive = false;
            return await db.SaveChangesAsync(); });
        await AssertReadsSurvive(w, "The work's discipline is no longer active in this project.");
    }
}
