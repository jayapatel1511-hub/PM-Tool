using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 030 projection semantics: "What changed?" has one definition for the project view, My Work and its export; a
/// change row counts Pending Assessment apart from acknowledgement and carries the whole-project count beside a scoped one
/// (FR-DCV-03, FR-DCV-04); input-use rows name the consuming work and the source revision.
[Collection("api")]
public sealed class DisciplineCoordinationTests(HubFactory f)
{
    readonly TestData d = new(f);
    int Rv<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);
    static JsonNode Row(JsonNode view) => Assert.Single(view["changes"]!.AsArray())!;
    static (int, int, int) Counts(JsonNode row) => (row.I("pendingAssessments"), row.I("acknowledgedPending"), row.I("projectPending"));

    [Fact]
    public async Task Change_rows_share_one_definition_and_count_pending_apart_from_acknowledgement()
    {
        var p = await d.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var (civil, electrical) = (d.ProjectDiscipline(p.Id, "Civil"), d.ProjectDiscipline(p.Id, "Electrical"));
        var survey = (await f.As(TestData.Marc).Post($"{root}/deliverables", new { name = "Coordinated survey", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false }).Result.Json(201)).G("id");
        int Head() => f.Db(db => db.SourceHeads.Single(h => h.ProjectId == p.Id).RowVersion);
        ChangeEndpoints.RegisterBody Revision(string rev, Guid? old) => new(Guid.NewGuid(), survey, Rv<Deliverable>(survey), civil, d.User(TestData.Alex), "Deliverable",
            "survey", "Coordinated survey", rev, $"https://example.test/survey-{rev}.pdf", "Survey team", "East corridor", null, old, old is null ? null : Head(),
            old is null ? null : "Changed corridor alignment", old is null ? null : new DateOnly(2026, 9, 14), old is null ? null : new DateOnly(2026, 9, 18));
        var a = (await f.As(TestData.Alex).Post($"{root}/source-revisions", Revision("A", null)).Result.Json()).G("id");
        var electricalTask = await d.NewTask(p.Id, TestData.Omar, new { name = "Service alignment", assigneeId = d.User(TestData.Omar), dueDate = "2026-09-20" }, "Electrical");
        var civilTask = await d.NewTask(p.Id, TestData.Marc, new { name = "Grading tie-in", assigneeId = d.User(TestData.Marc), dueDate = "2026-09-21" });
        foreach (var (who, task) in new[] { (TestData.Omar, electricalTask.G("id")), (TestData.Marc, civilTask.G("id")) })
            await f.As(who).Post($"{root}/input-uses", new ChangeEndpoints.AdoptBody(Guid.NewGuid(), "Task", task, Rv<WorkTask>(task), a, a, null,
                "Coordinate the corridor", "Incorporated into design basis")).Result.Json();
        var notice = (await f.As(TestData.Alex).Post($"{root}/source-revisions", Revision("B", a)).Result.Json()).G("id");
        await f.As(TestData.Alex).Post($"{root}/changes/{notice}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Rv<ChangeNotice>(notice), Head(), null)).Result.Json();
        var omars = f.Db(db => db.ChangeAssessments.Single(x => x.ChangeNoticeId == notice && x.TargetId == electricalTask.G("id")));
        await f.As(TestData.Omar).Post($"{root}/changes/{notice}/assessments/{omars.Id}", new ChangeEndpoints.AssessmentBody(Guid.NewGuid(), omars.RowVersion,
            "acknowledge", null, null, null, null, null, null, null, null, null, null, null, null)).Result.Json(); // seen, still Pending Assessment
        var b = f.Db(db => db.ChangeNotices.Single(c => c.Id == notice).NewRevisionId);
        var draft = (await f.As(TestData.Alex).Post($"{root}/source-revisions", Revision("C", b)).Result.Json()).G("id"); // registered, not published

        // Project-wide: the open notice with both assessments pending, one acknowledged; an unpublished draft is not yet a change.
        var all = await f.As(TestData.Pm).GetAsync($"{root}/discipline-coordination").Result.Json();
        Assert.Equal((notice, 1), (Row(all).G("id"), all.I("changesTotal")));
        Assert.Equal((2, 1, 2), Counts(Row(all)));
        Assert.NotEqual(notice, draft);
        Assert.Equal("Draft", f.Db(db => db.ChangeNotices.Single(c => c.Id == draft).Status));

        // Civil issued it and holds one unacknowledged assessment; Electrical's one is acknowledged; each states the project's two.
        var civilView = await f.As(TestData.Marc).GetAsync($"{root}/discipline-coordination?disciplineId={civil}").Result.Json();
        Assert.Equal((1, 0, 2), Counts(Row(civilView)));
        Assert.Equal((1, 1, 2), Counts(Row(await f.As(TestData.Omar).GetAsync($"{root}/discipline-coordination?disciplineId={electrical}").Result.Json())));
        Assert.Equal((1, 1, 2), Counts(Row(await f.As(TestData.Pm).GetAsync($"{root}/discipline-coordination?ownerId={d.User(TestData.Omar)}").Result.Json())));

        // My Work answers the question with the same rows for the same scope (its card count and CSV read these rows).
        var workspace = await f.As(TestData.Marc).GetAsync($"/api/v1/discipline-coordination?projectId={p.Id}&disciplineId={await d.Discipline("Civil")}").Result.Json();
        Assert.Equal(civilView["changes"]!.ToJsonString(), Assert.Single(workspace["projects"]!.AsArray())!["data"]!["changes"]!.ToJsonString());

        // Input uses name the consuming work and the source revision they use.
        Assert.Equal(new[] { civilTask.S("key"), electricalTask.S("key") }.Order(), all["uses"]!.AsArray().Select(u => u!.S("targetKey")).Order());
        var civilUse = Assert.Single(civilView["uses"]!.AsArray())!;
        Assert.Equal((civilTask.S("key"), "Grading tie-in", "A", f.Db(db => db.Deliverables.Single(x => x.Id == survey).Key)),
            (civilUse.S("targetKey"), civilUse.S("targetName"), civilUse.S("revision"), civilUse.S("sourceKey")));

        // Packet 030 bounded projection: a populated second page keeps the exact filtered total and source revision row.
        var pageTwo = await f.As(TestData.Pm).GetAsync($"{root}/discipline-coordination?page=2&pageSize=1").Result.Json();
        Assert.Equal(2, pageTwo.I("usesTotal"));
        Assert.Equal((2, 1), (pageTwo.I("usesPage"), pageTwo.I("usesPageSize")));
        var secondUse = Assert.Single(pageTwo["uses"]!.AsArray())!;
        Assert.Equal("A", secondUse.S("revision"));
        Assert.Equal(civilTask.S("key"), secondUse.S("targetKey"));
    }
}
