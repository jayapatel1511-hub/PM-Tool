using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class ReviewDisciplineDefaultsTests
{
    [Fact] // AC-SCR-64: real type configuration is one optimistic parent version/audit; equal set is a no-op.
    public async Task Whole_set_edit_is_versioned_audited_once_and_noop_does_not_rewrite_links()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var type = await t.Data.DeliverableType(); var civil = await t.Data.Discipline("Civil"); var electrical = await t.Data.Discipline("Electrical");
        var original = t.F.Db(db => db.DeliverableTypes.Single(row => row.Id == type)); var logs = Logs(t, type);
        var result = await Put(t, type, [civil, electrical], original.RowVersion); Assert.Equal(original.RowVersion + 1, result.I("rowVersion")); Assert.Equal(logs + 1, Logs(t, type));
        var stored = t.F.Db(db => db.DeliverableTypes.Single(row => row.Id == type)); Assert.Equal(original.Name, stored.Name); Assert.Equal(original.DefaultDisciplineId, stored.DefaultDisciplineId);
        var changes = JsonNode.Parse(t.F.Db(db => db.ActivityLog.Where(row => row.ItemId == type && row.Action == "Updated").OrderByDescending(row => row.OccurredAt).ThenByDescending(row => row.Id).First().Changes))!.AsArray();
        Assert.Equal(2, changes.Count); Assert.All(changes, row => { Assert.Equal("RequiredReviewDisciplineId", row!.S("field")); Assert.Null(row["old"]); Assert.Contains(row.G("new"), new[] { civil, electrical }); });
        var before = State(t, type); await Put(t, type, [electrical, civil], result.I("rowVersion")); Assert.Equal(before, State(t, type));
        await Put(t, type, [civil], original.RowVersion, 409); Assert.Equal(before, State(t, type));
        await Put(t, type, [civil], null, 428); Assert.Equal(before, State(t, type));
        await Put(t, type, [civil], result.I("rowVersion"), who: TestData.Pm, status: 403); Assert.Equal(before, State(t, type));
    }

    [Fact] // AC-SCR-64: unchanged inactive links may stay or be removed; inactive types cannot acquire new links.
    public async Task Inactive_set_diff_and_invalid_inputs_are_atomic()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var type = await t.Data.DeliverableType(); var civil = await t.Data.Discipline("Civil"); var electrical = await t.Data.Discipline("Electrical");
        await Put(t, type, [civil], Version(t, type));
        await t.F.DbAsync(async db => { (await db.Disciplines.SingleAsync(row => row.Id == civil)).IsActive = false; (await db.DeliverableTypes.SingleAsync(row => row.Id == type)).IsActive = false; return await db.SaveChangesAsync(); });
        var before = State(t, type); await Put(t, type, [civil], Version(t, type)); Assert.Equal(before, State(t, type));
        foreach (var ids in new[] { new[] { electrical }, new[] { civil, civil }, new[] { Guid.Empty }, new[] { Guid.NewGuid() }, new[] { t.Data.User(TestData.Alex) }, Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()).ToArray() }) {
            await Put(t, type, ids, Version(t, type), 400); Assert.Equal(before, State(t, type));
        }
        await Put(t, type, [], Version(t, type)); Assert.Empty(t.F.Db(db => db.DeliverableReviewDefaults.Where(row => row.DeliverableTypeId == type).ToArray()));
        before = State(t, type); await Put(t, type, [civil], Version(t, type), 400); Assert.Equal(before, State(t, type));
        await t.F.DbAsync(async db => { (await db.DeliverableTypes.SingleAsync(row => row.Id == type)).IsActive = true; return await db.SaveChangesAsync(); });
        before = State(t, type); await Put(t, type, [civil], Version(t, type), 400); Assert.Equal(before, State(t, type)); // Active type still cannot add an inactive discipline.
    }

    [Fact] // AC-SCR-64: actual two Drawing Package revisions map a complete union, coordinator can choose an explicit different set.
    public async Task Two_drawing_packages_map_civil_and_geotechnical_once_without_changing_existing_reviews()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var civil = await t.Data.Discipline("Civil"); var electrical = await t.Data.Discipline("Electrical");
        var refs = (await t.Data.Ref())["disciplines"]!.AsArray(); var geotechnical = refs.FirstOrDefault(row => row!.S("name") == "Geotechnical")?.G("id")
            ?? (await t.Post(TestData.Admin, "/api/v1/admin/disciplines", new { name = "Geotechnical", code = "GT", colour = "#64748b" }, 201)).G("id");
        var project = await t.Data.Project(tweak: body => body["disciplines"] = new[] {
            new { disciplineId = civil, leadUserId = (Guid?)t.Data.User(TestData.Marc) }, new { disciplineId = electrical, leadUserId = (Guid?)t.Data.User(TestData.Omar) },
            new { disciplineId = geotechnical, leadUserId = (Guid?)t.Data.User(TestData.Pm) } });
        var a = await t.New(project); var b = await t.New(project, "Second drawing"); var type = await t.Data.DeliverableType();
        await Put(t, type, [civil, geotechnical], Version(t, type)); var before = t.State(project.Id); var defaultsBefore = State(t, type);
        var suggestions = await Suggest(t, project.Id, $"{a.Source},{b.Source}"); var rows = suggestions["disciplines"]!.AsArray();
        Assert.Equal(2, rows.Count); Assert.Equal(new[] { civil, geotechnical }.Order(), rows.Select(row => row!.G("disciplineId")).Order()); Assert.All(rows, row => Assert.Equal("Available", row!.S("state")));
        Assert.Equal(2, suggestions["sourceTypes"]!.AsArray().Count); Assert.Equal(before, t.State(project.Id)); Assert.Equal(defaultsBefore, State(t, type));
        // Defaults are advisory: a new real package with just Civil remains a valid explicit coordinator choice.
        await t.Post(TestData.Marc, $"/api/v1/projects/{project.Id}/reviews", new Hub.Api.Features.ReviewEndpoints.CreateBody(Guid.NewGuid(), "Explicit Civil review", "Coordinator-chosen scope", a.Civil,
            t.Data.User(TestData.Marc), [a.Source, b.Source], [new(a.Civil, t.Data.User(TestData.Pm), new(2026, 11, 12))], false, null));
        Assert.Equal(2, t.F.Db(db => db.DisciplineReviews.Count(row => row.RoundId == a.Round))); // Existing package unchanged.
        await t.Flag(false); var stored = State(t, type);
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Admin).GetAsync(SettingsPath(type))).StatusCode);
        await Put(t, type, [], Version(t, type), 404); await Suggest(t, project.Id, a.Source.ToString(), 404); Assert.Equal(stored, State(t, type));
        await t.Flag(true); Assert.Equal(2, (await Suggest(t, project.Id, a.Source.ToString()))["disciplines"]!.AsArray().Count);
    }

    [Fact] // AC-SCR-64: missing/inactive mappings and invalid sources are not silently empty defaults.
    public async Task Strict_sources_and_inactive_mapping_states_preserve_unknown_distinctions()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var other = await t.New(); var type = await t.Data.DeliverableType(); var civil = await t.Data.Discipline("Civil");
        var extra = (await t.Post(TestData.Admin, "/api/v1/admin/disciplines", new { name = "Synthetic unconfigured review discipline", code = "SYN", colour = "#64748b" }, 201)).G("id");
        await Put(t, type, [civil, extra], Version(t, type)); var before = t.State(s.Project.Id);
        var rows = (await Suggest(t, s.Project.Id, s.Source.ToString()))["disciplines"]!.AsArray(); Assert.Equal("NotInProject", rows.Single(row => row!.G("disciplineId") == extra)!.S("state"));
        foreach (var raw in new[] { "", "not-a-guid", Guid.Empty.ToString(), $"{s.Source},{s.Source}" }) await Suggest(t, s.Project.Id, raw, 400);
        await Suggest(t, s.Project.Id, other.Source.ToString(), 404); await Suggest(t, s.Project.Id, Guid.NewGuid().ToString(), 404); Assert.Equal(before, t.State(s.Project.Id));
        await t.F.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(d => d.Id == s.Civil)).IsActive = false; return await db.SaveChangesAsync(); });
        rows = (await Suggest(t, s.Project.Id, s.Source.ToString()))["disciplines"]!.AsArray(); Assert.Equal("InactiveProjectDiscipline", rows.Single(row => row!.G("disciplineId") == civil)!.S("state"));
        await t.F.DbAsync(async db => { (await db.Disciplines.SingleAsync(d => d.Id == civil)).IsActive = false; return await db.SaveChangesAsync(); });
        rows = (await Suggest(t, s.Project.Id, s.Source.ToString()))["disciplines"]!.AsArray(); Assert.Equal("InactiveDiscipline", rows.Single(row => row!.G("disciplineId") == civil)!.S("state"));
        await t.F.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(d => d.Id == s.Civil)).IsActive = true; (await db.Disciplines.SingleAsync(d => d.Id == civil)).IsActive = true; (await db.DeliverableTypes.SingleAsync(d => d.Id == type)).IsActive = false; return await db.SaveChangesAsync(); });
        rows = (await Suggest(t, s.Project.Id, s.Source.ToString()))["disciplines"]!.AsArray(); Assert.Equal("InactiveType", rows.Single(row => row!.G("disciplineId") == civil)!.S("state"));
    }

    [Fact] // AC-SCR-64: a union over 50 is complete and explicitly exceeds the ordinary assignment limit.
    public async Task Mixed_type_union_is_not_truncated_and_obsolete_sources_are_refused()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var a = await t.New(); var b = await t.New(a.Project, "Second type source");
        var drawing = await t.Data.DeliverableType(); var report = await t.Data.DeliverableType("Report");
        var ids = await t.F.DbAsync(async db => {
            var references = Enumerable.Range(1, 51).Select(index => new Discipline { Name = $"Synthetic union discipline {index}", Code = $"S{index}" }).ToArray();
            db.Disciplines.AddRange(references); (await db.Deliverables.SingleAsync(d => d.Id == b.Deliverable)).DeliverableTypeId = report;
            await db.SaveChangesAsync(); return references.Select(d => d.Id).ToArray();
        });
        await Put(t, drawing, ids.Take(30).ToArray(), Version(t, drawing)); await Put(t, report, ids.Skip(30).ToArray(), Version(t, report));
        var result = await Suggest(t, a.Project.Id, $"{a.Source},{b.Source}"); Assert.Equal(51, result["disciplines"]!.AsArray().Count); Assert.True(result["exceedsAssignmentLimit"]!.GetValue<bool>());
        var replacement = await t.PublishReplacement(a); await Suggest(t, a.Project.Id, a.Source.ToString(), 400); await Suggest(t, a.Project.Id, replacement.ToString());
        var head = t.F.Db(db => db.SourceHeads.Single(row => row.ProjectId == a.Project.Id && row.CurrentRevisionId == replacement));
        var proposal = await t.Post(TestData.Alex, t.Root(a) + "/source-revisions", new Hub.Api.Features.ChangeEndpoints.RegisterBody(Guid.NewGuid(), a.Deliverable,
            t.Version<Deliverable>(a.Deliverable), a.Civil, t.Data.User(TestData.Alex), "Deliverable", "response-sheet", "Synthetic unpublished source", "C",
            "https://example.test/source-C.pdf", "Synthetic author", "Proposed scope", null, replacement, head.RowVersion,
            "Proposed revision still needs publication", new(2026, 11, 2), new(2026, 11, 12)));
        var unpublished = t.F.Db(db => db.ChangeNotices.Single(row => row.Id == proposal.G("id")).NewRevisionId); await Suggest(t, a.Project.Id, unpublished.ToString(), 400);
    }

    [Fact] // AC-SCR-64: shared parent version protects ordinary type edits; late provider failure rolls back set/version/audit.
    public async Task Concurrent_type_edit_and_provider_failure_cannot_leave_partial_configuration()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var type = await t.Data.DeliverableType(); var civil = await t.Data.Discipline("Civil"); var version = Version(t, type);
        var put = t.F.As(TestData.Admin).Put(SettingsPath(type), new { disciplineIds = new[] { civil }, rowVersion = version });
        var patch = t.F.As(TestData.Admin).Patch($"/api/v1/admin/deliverableTypes/{type}", new { name = "Synthetic concurrent type edit" }, version);
        var replies = await Task.WhenAll(put, patch); Assert.Equal(new[] { 200, 409 }, replies.Select(reply => (int)reply.StatusCode).Order()); Assert.Equal(version + 1, Version(t, type));
        await Put(t, type, [], Version(t, type));
        await t.F.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION hub.s36_test_reject_default() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic review default failure'; END $$;
            CREATE TRIGGER s36_test_reject_default BEFORE INSERT ON hub.deliverable_review_default FOR EACH ROW EXECUTE FUNCTION hub.s36_test_reject_default();
            """));
        var before = State(t, type);
        try { await Put(t, type, [civil], Version(t, type), 500); Assert.Equal(before, State(t, type)); }
        finally { await t.F.DbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER s36_test_reject_default ON hub.deliverable_review_default; DROP FUNCTION hub.s36_test_reject_default();")); }
    }

    static string SettingsPath(Guid type) => $"/api/v1/admin/deliverableTypes/{type}/review-defaults";
    static int Version(ReviewResponseFixture t, Guid type) => t.F.Db(db => db.DeliverableTypes.Single(row => row.Id == type).RowVersion);
    static int Logs(ReviewResponseFixture t, Guid type) => t.F.Db(db => db.ActivityLog.Count(row => row.ItemId == type && row.Action == "Updated"));
    static async Task<JsonNode> Put(ReviewResponseFixture t, Guid type, Guid[] ids, int? version, int status = 200, string who = TestData.Admin)
        => await (await t.F.As(who).Put(SettingsPath(type), new { disciplineIds = ids, rowVersion = version })).Json(status);
    static async Task<JsonNode> Suggest(ReviewResponseFixture t, Guid project, string sources, int status = 200)
        => await (await t.F.As(TestData.Pm).GetAsync($"/api/v1/projects/{project}/reviews/default-disciplines?sourceRevisionIds={Uri.EscapeDataString(sources)}")).Json(status);
    static string State(ReviewResponseFixture t, Guid type) => t.F.Db(db => JsonSerializer.Serialize(new {
        type = db.DeliverableTypes.Where(row => row.Id == type).Select(row => new { row.Id, row.Name, row.DefaultDisciplineId, row.IsActive, row.RowVersion, row.UpdatedAt, row.UpdatedBy }).Single(),
        links = db.DeliverableReviewDefaults.Where(row => row.DeliverableTypeId == type).OrderBy(row => row.DisciplineId).ToArray(), logs = db.ActivityLog.Count(row => row.ItemId == type),
        receipts = db.Idempotency.Count(), commands = db.CoordinationCommands.Count(), seen = db.WorkSeenAcknowledgements.Count()
    }, JsonOpts.Web));
}
