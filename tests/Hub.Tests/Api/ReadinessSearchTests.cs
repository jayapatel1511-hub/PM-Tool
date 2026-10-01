using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Global search (§18.1, FR-SRCH-01/02) finds readiness constraints and weekly promises by key or text, only in projects
/// the caller may view, and an exact CT/WC key opens the record.
[Collection("api")]
public sealed class ReadinessSearchTests(HubFactory f)
{
    readonly TestData data = new(f);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Search(string who, string q, string? type = null) =>
        await (await f.As(who).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(q)}{(type is null ? "" : $"&type={type}")}")).Json();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    [Fact]
    public async Task Constraints_and_promises_are_searchable_by_key_and_text_within_visible_projects()
    {
        var project = await data.Project();
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        var marker = "zq" + Guid.NewGuid().ToString("N")[..8];
        var constraint = (await Post(TestData.Alex, $"/api/v1/projects/{project.Id}/readiness/Task/{task}/constraints", new ReadinessEndpoints.ConstraintBody(
            Guid.NewGuid(), Version<WorkTask>(task), "Handoff", $"Survey {marker} pending", data.User(TestData.Pm), new DateOnly(2026, 9, 21),
            "https://example.test/survey"))).G("id");
        var monday = new DateOnly(2026, 9, 21);
        var promise = (await Post(TestData.Pm, $"/api/v1/projects/{project.Id}/weekly-commitments/Task/{task}", new WeeklyCommitmentsEndpoints.ProposeBody(
            Guid.NewGuid(), Version<WorkTask>(task), monday, monday.AddDays(2), $"Issue {marker} layout", "Checked"))).G("id");
        var constraintKey = f.Db(db => db.WorkConstraints.Single(c => c.Id == constraint).Key);
        var promiseKey = f.Db(db => db.OutputCommitments.Single(c => c.Id == promise).Key);

        var byText = await Search(TestData.Alex, marker);
        var hit = Assert.Single(byText["groups"]!["constraints"]!.AsArray())!;
        Assert.Equal((constraint, constraintKey, project.ProjectNumber, ConstraintState.Open), (hit.G("id"), hit.S("key"), hit.S("projectNumber"), hit.S("status")));
        Assert.Equal(promise, Assert.Single(byText["groups"]!["commitments"]!.AsArray())!.G("id"));
        Assert.Equal((1, 1), (byText["counts"]!.I("constraints"), byText["counts"]!.I("commitments")));

        var exact = (await Search(TestData.Alex, constraintKey.ToLowerInvariant()))["exact"]!;
        Assert.Equal(("WorkConstraint", constraint, project.ProjectNumber), (exact.S("type"), exact.G("id"), exact.S("projectNumber")));
        Assert.Equal("OutputCommitment", (await Search(TestData.Alex, promiseKey))["exact"]!.S("type"));
        var located = await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}/readiness/constraints/{constraint}")).Json();
        Assert.Equal(("Task", task), (located.S("targetType"), located.G("targetId")));

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var outsider = await Search(TestData.Rita, marker);
        Assert.Equal((0, 0), (outsider["counts"]!.I("constraints"), outsider["counts"]!.I("commitments")));
        Assert.Null((await Search(TestData.Rita, constraintKey))["exact"]);
        await (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{project.Id}/readiness/constraints/{constraint}")).Json(404);
        Assert.Equal(1, (await Search(TestData.Alex, marker, "constraints"))["counts"]!.I("constraints")); // a member still finds it
    }
}
