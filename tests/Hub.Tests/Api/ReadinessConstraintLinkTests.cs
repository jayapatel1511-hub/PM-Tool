using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// FR-RDY-03 and §10.8: a constraint links the existing same-project decision, issue or handoff that represents it, and
/// constraints and promises carry readable CT/WC keys.
[Collection("api")]
public sealed class ReadinessConstraintLinkTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    async Task<Guid> Decision(Project p) => (await Post(TestData.Pm, $"/api/v1/projects/{p.Id}/decisions", new
    {
        subject = "Confirm drainage outfall", description = "Outfall location decides the grading", ownerUserId = data.User(TestData.Alex),
        requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Grading waits on it"
    }, 201)).G("id");

    async Task<Guid> Issue(Project p) => (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/issues", new
    {
        title = "Utility clash at the outfall", severity = "High", ownerId = data.User(TestData.Alex),
        projectDisciplineId = data.ProjectDiscipline(p.Id, "Civil")
    }, 201)).G("id");

    ReadinessEndpoints.ConstraintBody Constraint(Guid task, string? type = null, Guid? id = null) =>
        new(Guid.NewGuid(), Version<WorkTask>(task), "Decision", "Await the outfall decision", data.User(TestData.Pm),
            new DateOnly(2026, 9, 21), "https://example.test/outfall", type, id);

    [Fact]
    public async Task Constraint_links_a_same_project_record_and_shows_its_current_status()
    {
        var project = await data.Project();
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{task}/constraints";
        var decision = await Decision(project);
        var issue = await Issue(project);
        var foreign = await Decision(await data.Project());

        Assert.NotNull((await Post(TestData.Alex, path, Constraint(task, ItemType.Decision, foreign), 400))["errors"]?["linkedId"]);
        Assert.NotNull((await Post(TestData.Alex, path, Constraint(task, ItemType.Decision), 400))["errors"]?["linkedId"]);
        Assert.NotNull((await Post(TestData.Alex, path, Constraint(task, "Risk", decision), 400))["errors"]?["linkedType"]);
        await f.DbAsync(async db => { (await db.Issues.SingleAsync(i => i.Id == issue)).DeletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        await Post(TestData.Alex, path, Constraint(task, ItemType.Issue, issue), 400); // a deleted record cannot be linked

        var linked = Constraint(task, ItemType.Decision, decision);
        var first = (await Post(TestData.Alex, path, linked)).G("id");
        Assert.Equal(first, (await Post(TestData.Alex, path, linked)).G("id")); // a retry neither duplicates nor takes a key
        var plain = (await Post(TestData.Alex, path, Constraint(task))).G("id");
        Assert.Equal(new[] { $"{project.ProjectNumber}-CT001", $"{project.ProjectNumber}-CT002" },
            f.Db(db => db.WorkConstraints.Where(c => c.ProjectId == project.Id).OrderBy(c => c.Seq).Select(c => c.Key).ToArray()));

        var rows = (await (await f.As(TestData.Rita).GetAsync(path)).Json()).AsArray();
        var row = rows.Single(r => r!.G("id") == first)!;
        Assert.Equal((ItemType.Decision, decision, DecisionStatus.Pending), (row["linked"]!.S("type"), row["linked"]!.G("id"), row["linked"]!.S("status")));
        Assert.Null(rows.Single(r => r!.G("id") == plain)!["linked"]);
        var decided = await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{decision}/transition", new { toStatus = "Decided",
            decisionText = "Use the east outfall", decisionDate = "2026-09-14", rowVersion = Version<Decision>(decision) })).Json(200);
        Assert.Equal(DecisionStatus.Decided, decided.S("status"));
        var refreshed = (await (await f.As(TestData.Alex).GetAsync(path)).Json()).AsArray().Single(r => r!.G("id") == first)!;
        Assert.Equal(DecisionStatus.Decided, refreshed["linked"]!.S("status"));
        Assert.Equal(ConstraintState.Open, refreshed.S("state")); // the record's outcome is displayed, never applied

        var options = (await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}/readiness/link-options?type=Decision")).Json()).AsArray();
        Assert.Equal(new[] { decision }, options.Select(o => o!.G("id")).ToArray());
        Assert.Empty((await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}/readiness/link-options?type=Issue")).Json()).AsArray());
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}/readiness/link-options?type=Risk")).Json(400);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{project.Id}/readiness/link-options?type=Decision")).Json(404);
    }

    [Fact]
    public async Task Promises_take_per_project_commitment_keys()
    {
        var project = await data.Project();
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        var root = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var monday = new DateOnly(2026, 9, 21);
        WeeklyCommitmentsEndpoints.ProposeBody Body() => new(Guid.NewGuid(), Version<WorkTask>(task), monday, monday.AddDays(2), "Layout", "Checked");
        var first = (await Post(TestData.Pm, $"{root}/Task/{task}", Body())).G("id");
        await Post(TestData.Pm, $"{root}/Task/{task}", Body() with { WeekStart = monday.AddDays(1) }, 400); // a refused proposal takes no key
        var second = (await Post(TestData.Pm, $"{root}/Task/{task}", Body())).G("id");
        Assert.Equal(($"{project.ProjectNumber}-WC001", $"{project.ProjectNumber}-WC002"),
            (f.Db(db => db.OutputCommitments.Single(c => c.Id == first).Key), f.Db(db => db.OutputCommitments.Single(c => c.Id == second).Key)));
    }
}
