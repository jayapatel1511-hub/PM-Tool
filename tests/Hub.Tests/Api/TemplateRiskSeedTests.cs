using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

internal sealed class TemplateScreenData(HubFactory f)
{
    public readonly TestData Data = new(f);
    public async Task SetFlag(string flag, bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag." + flag, new { value = on })).Json();
    public async Task<JsonNode> Read(Guid id, string who = TestData.Admin, string query = "", int expect = 200) => await (await f.As(who).GetAsync($"/api/v1/templates/{id}{query}")).Json(expect);
    public async Task<JsonNode> New(object[]? risks = null)
    {
        var created = await (await f.As(TestData.Admin).Post("/api/v1/templates", new { name = "Synthetic S40/S48 template" })).Json(201);
        var civil = await Data.Discipline("Civil");
        var body = new Dictionary<string, object?> {
            ["rowVersion"] = created.I("rowVersion"), ["disciplines"] = new[] { new { disciplineId = civil, isDefaultIncluded = true } },
            ["milestones"] = new[] { new { @ref = "start", name = "Synthetic start", milestoneType = "Kickoff", anchor = "ProjectStart", offset = 0, isClientFacing = false },
                new { @ref = "end", name = "Synthetic end", milestoneType = "Closeout", anchor = "ProjectStart", offset = 10, isClientFacing = false } },
            ["deliverables"] = new[] { new { @ref = "drawing", disciplineId = civil, name = "Synthetic drawing", deliverableTypeId = await Data.DeliverableType(), milestoneRef = "end", offset = -2, requiresReview = true } },
            ["tasks"] = new[] { new { @ref = "one", disciplineId = civil, deliverableRef = "drawing", name = "Same synthetic task", requiresReview = false },
                new { @ref = "two", disciplineId = civil, deliverableRef = "drawing", name = "Same synthetic task", requiresReview = false } },
            ["dependencies"] = new[] { new { predecessor = "one", successor = "two" } }
        };
        if (risks is not null) body["risks"] = risks;
        await (await f.As(TestData.Admin).Put($"/api/v1/templates/{created.G("id")}/structure", body)).Json();
        return await Read(created.G("id"));
    }
    public async Task<JsonNode> Save(JsonNode template, Action<JsonObject>? change = null, string who = TestData.Admin, int expect = 200)
    {
        var body = template.DeepClone().AsObject();
        body["header"] = new JsonObject { ["name"] = template.S("name"), ["description"] = template["description"]?.DeepClone(), ["projectTypeId"] = template["projectTypeId"]?.DeepClone() };
        change?.Invoke(body);
        return await (await f.As(who).Put($"/api/v1/templates/{template.G("id")}/structure", body)).Json(expect);
    }
    public async Task<JsonNode> Publish(JsonNode template, string? key = null)
    {
        var client = f.As(TestData.Admin); if (key is not null) client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        return await (await client.Post($"/api/v1/templates/{template.G("id")}/publish", new { rowVersion = template.I("rowVersion") })).Json();
    }
    public async Task<JsonNode> Draft(Guid source, string? key = null, int expect = 201)
    {
        var client = f.As(TestData.Admin); if (key is not null) client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var created = await (await client.Post($"/api/v1/templates/{source}/draft", new { })).Json(expect);
        return await Read(created.G("id"));
    }
    public Task<Project> Project(Guid source, string? number = null) => Data.Project(activate: false, tweak: body => {
        body["templateId"] = source; body["startDate"] = "2026-11-02"; if (number is not null) body["projectNumber"] = number;
    });
    public string State() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(), Notices = db.Notifications.Count(), Mail = db.Emails.Count(), Outbox = db.Outbox.Count(), Receipts = db.Idempotency.Count(),
        Seen = db.WorkSeenAcknowledgements.Count(), SeenReceipts = db.WorkSeenCommandReceipts.Count(),
        Templates = db.Templates.OrderBy(row => row.Id).Select(row => new { row.Id, row.FamilyId, row.RowVersion, row.Status, row.Version, row.Name, row.CopiedFromTemplateId, row.CopiedFromSnapshot }).ToArray(),
        Seeds = db.TemplateRisks.OrderBy(row => row.Id).Select(row => new { row.Id, row.TemplateId, row.Title, row.Probability, row.Impact, row.Mitigation, row.TriggerIndicator }).ToArray(),
        Projects = db.Projects.OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectManagerId, row.RowVersion, row.CreatedFromTemplateId, row.TemplateVersion, row.NextRiskSeq, row.NextTaskSeq, row.NextDeliverableSeq }).ToArray(),
        Risks = db.Risks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.TemplateRiskId, row.RowVersion, row.Title, row.OwnerId, row.Status, row.Probability, row.Impact, row.Mitigation, row.TriggerIndicator }).ToArray(),
        Tasks = db.Tasks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.RowVersion }).ToArray(),
        Deliverables = db.Deliverables.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.RowVersion }).ToArray(),
        Milestones = db.Milestones.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.RowVersion }).ToArray()
    }));
    public static object[] Risks => [new { @ref = "risk-one", title = "Synthetic first risk", probability = 2, impact = 3, mitigation = "Synthetic mitigation", triggerIndicator = "Synthetic trigger" },
        new { @ref = "risk-two", title = "Synthetic second risk", probability = 1, impact = 2, mitigation = (string?)null, triggerIndicator = (string?)null }];
}

/// FR/AC-SCR-70 and FR/AC-SCR-84: real commands/provider; no template-to-project live update channel.
public sealed class TemplateRiskSeedTests : IDisposable
{
    readonly HubFactory f = new(); readonly TemplateScreenData t;
    public TemplateRiskSeedTests() { t = new(f); }
    public void Dispose() => f.Dispose();

    [Fact]
    public async Task Two_seeds_become_Open_risks_owned_by_the_actual_project_PM_and_later_changes_do_not_propagate()
    {
        await t.SetFlag("register_screens", true); var template = await t.New(TemplateScreenData.Risks); await t.Publish(template);
        var project = await t.Project(template.G("id"));
        var risks = await f.DbAsync(db => db.Risks.AsNoTracking().Where(row => row.ProjectId == project.Id).OrderBy(row => row.Seq).ToArrayAsync());
        Assert.Equal(2, risks.Length); Assert.Equal(new[] { "Synthetic first risk", "Synthetic second risk" }, risks.Select(row => row.Title));
        Assert.All(risks, row => { Assert.Equal(project.ProjectManagerId, row.OwnerId); Assert.Equal(RiskStatus.Open, row.Status);
            Assert.NotNull(row.TemplateRiskId); Assert.Null(row.ReviewDate); Assert.Null(row.ProjectDisciplineId); Assert.Null(row.RealisedIssueId); Assert.Null(row.Description); });
        Assert.Equal((2, 3, "Synthetic mitigation", "Synthetic trigger"), (risks[0].Probability, risks[0].Impact, risks[0].Mitigation, risks[0].TriggerIndicator));
        Assert.Equal(2, f.Db(db => db.ActivityLog.Count(row => row.ItemType == ItemType.Risk && row.ProjectId == project.Id && row.Action == "Created")));
        var draft = await t.Draft(template.G("id")); await t.Save(draft, body => body["risks"]![0]!["title"] = "Edited only in template");
        Assert.Equal("Synthetic first risk", f.Db(db => db.Risks.Single(row => row.Id == risks[0].Id).Title));
        await (await f.As(TestData.Pm).Patch($"/api/v1/risks/{risks[0].Id}", new { mitigation = "Changed only in project" }, risks[0].RowVersion)).Json();
        Assert.Equal("Synthetic mitigation", f.Db(db => db.TemplateRisks.Single(row => row.Id == risks[0].TemplateRiskId).Mitigation));
    }

    [Fact]
    public async Task Hidden_seeds_survive_old_client_save_and_clone_but_off_instantiation_is_not_backfilled()
    {
        await t.SetFlag("register_screens", true); await t.SetFlag("people_screens", true);
        var source = await t.New(TemplateScreenData.Risks); await t.Publish(source); var draft = await t.Draft(source.G("id"));
        var baseline = f.Db(db => db.Templates.Single(row => row.Id == draft.G("id")).CopiedFromSnapshot);
        await t.SetFlag("register_screens", false); await t.SetFlag("people_screens", false);
        var dark = await t.Read(draft.G("id")); Assert.Null(dark["risks"]); Assert.Null(dark["screen"]);
        await t.Save(dark); Assert.Equal(2, f.Db(db => db.TemplateRisks.Count(row => row.TemplateId == draft.G("id"))));
        Assert.Equal(baseline, f.Db(db => db.Templates.Single(row => row.Id == draft.G("id")).CopiedFromSnapshot));
        var offProject = await t.Project(source.G("id")); Assert.False(f.Db(db => db.Risks.Any(row => row.ProjectId == offProject.Id)));
        await t.Publish(await t.Read(draft.G("id"))); var offClone = await t.Draft(draft.G("id"));
        Assert.Equal(2, f.Db(db => db.TemplateRisks.Count(row => row.TemplateId == offClone.G("id"))));
        Assert.Null(f.Db(db => db.Templates.Single(row => row.Id == offClone.G("id")).CopiedFromTemplateId));
        await t.SetFlag("register_screens", true); await t.SetFlag("people_screens", true);
        Assert.False(f.Db(db => db.Risks.Any(row => row.ProjectId == offProject.Id)));
        Assert.Equal("NotRecorded", (await t.Read(offClone.G("id")))["screen"]!["draftDiff"]!.S("state"));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(1, 4)] [InlineData(null, 2)]
    public async Task Invalid_scores_and_stale_or_unprivileged_writes_refuse_without_mutation(int? probability, int? impact)
    {
        await t.SetFlag("register_screens", true); var template = await t.New(); var before = t.State();
        await t.Save(template, body => body["risks"] = JsonSerializer.SerializeToNode(new[] { new { @ref = "bad", title = "Synthetic", probability, impact } }), expect: 400);
        Assert.Equal(before, t.State());
        await t.Save(template, who: TestData.Alex, expect: 403); Assert.Equal(before, t.State()); // a System PM may manage templates (§8.5.1, checkpoint 33)
        await t.Save(template, body => body["rowVersion"] = template.I("rowVersion") - 1, expect: 409); Assert.Equal(before, t.State());
    }

    [Fact]
    public async Task Forged_seed_fields_and_off_risk_input_refuse_while_omission_and_explicit_clear_have_distinct_meanings()
    {
        await t.SetFlag("register_screens", true); var template = await t.New(TemplateScreenData.Risks); var before = t.State();
        await t.Save(template, body => body["risks"]![0]!["ownerId"] = t.Data.User(TestData.Alex).ToString(), expect: 400); Assert.Equal(before, t.State());
        await t.SetFlag("register_screens", false); var dark = await t.Read(template.G("id")); before = t.State();
        await t.Save(dark, body => body["risks"] = new JsonArray(), expect: 404); Assert.Equal(before, t.State());
        await t.SetFlag("register_screens", true); var current = await t.Read(template.G("id"));
        await t.Save(current, body => body.Remove("risks")); Assert.Equal(2, f.Db(db => db.TemplateRisks.Count(row => row.TemplateId == current.G("id"))));
        await t.Save(await t.Read(template.G("id")), body => body["risks"] = new JsonArray()); Assert.Equal(0, f.Db(db => db.TemplateRisks.Count(row => row.TemplateId == template.G("id"))));
    }

    [Fact]
    public async Task Global_risks_survive_unticked_disciplines_but_successful_pack_and_project_copy_do_not_seed_risks_or_infer_origin()
    {
        await t.SetFlag("register_screens", true); var source = await t.New(TemplateScreenData.Risks); await t.Publish(source);
        var global = await t.Data.Project(activate: false, tweak: body => { body["templateId"] = source.G("id"); body["disciplines"] = Array.Empty<object>(); });
        Assert.Equal(2, f.Db(db => db.Risks.Count(row => row.ProjectId == global.Id)));
        Assert.False(f.Db(db => db.Tasks.Any(row => row.ProjectId == global.Id)));
        var packTarget = await t.Data.Project(activate: false, tweak: body => body["disciplines"] = Array.Empty<object>());
        var mapping = source["milestones"]!.AsArray().ToDictionary(row => Guid.Parse(row!.S("ref")), _ => (Guid?)null);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{packTarget.Id}/template-packs", new { templateId = source.G("id"), disciplineId = await t.Data.Discipline("Civil"),
            leadUserId = t.Data.User(TestData.Marc), mapping })).Json();
        Assert.False(f.Db(db => db.Risks.Any(row => row.ProjectId == packTarget.Id)));
        Assert.Null(f.Db(db => db.Projects.Single(row => row.Id == packTarget.Id).CreatedFromTemplateId));
        var copied = await t.Data.Project(activate: false, tweak: body => body["copyFromProjectId"] = global.Id);
        Assert.False(f.Db(db => db.Risks.Any(row => row.ProjectId == copied.Id)));
        Assert.Null(copied.CreatedFromTemplateId);
    }

    [Fact]
    public async Task Retrying_one_keyed_project_create_returns_one_project_and_two_seeded_risks_once()
    {
        await t.SetFlag("register_screens", true); var source = await t.New(TemplateScreenData.Risks); await t.Publish(source);
        var client = f.As(TestData.Pm); client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var body = new { projectNumber = TestData.Number(), name = "Synthetic receipt project", clientId = await t.Data.Client(), officeId = await t.Data.Office(), templateId = source.G("id"),
            disciplines = new[] { new { disciplineId = await t.Data.Discipline("Civil"), leadUserId = t.Data.User(TestData.Marc) } } };
        var first = await (await client.Post("/api/v1/projects", body)).Json(201); var before = t.State();
        Assert.Equal(first.ToJsonString(), (await (await client.Post("/api/v1/projects", body)).Json(201)).ToJsonString());
        Assert.Equal(2, f.Db(db => db.Risks.Count(row => row.ProjectId == first.G("id")))); Assert.Equal(before, t.State());
    }

    [Fact]
    public async Task A_provider_failure_in_risk_insertion_rolls_back_the_entire_project_and_all_copied_items_and_activity()
    {
        await t.SetFlag("register_screens", true); var template = await t.New(TemplateScreenData.Risks); await t.Publish(template);
        // Scoped fault injection in this factory's disposable PostgreSQL database only.
        await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION hub.s40_test_reject_seed() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic seed insertion failure'; END $$;
            CREATE TRIGGER s40_test_reject_seed BEFORE INSERT ON hub.risk FOR EACH ROW WHEN (NEW.title = 'Synthetic second risk') EXECUTE FUNCTION hub.s40_test_reject_seed();
            """));
        var before = t.State();
        try
        {
            var body = new { projectNumber = TestData.Number(), name = "Synthetic rollback project", clientId = await t.Data.Client(), officeId = await t.Data.Office(),
                templateId = template.G("id"), startDate = "2026-11-02", disciplines = new[] { new { disciplineId = await t.Data.Discipline("Civil"), leadUserId = t.Data.User(TestData.Marc) } } };
            var response = await f.As(TestData.Pm).Post("/api/v1/projects", body); Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(before, t.State());
        }
        finally { await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER s40_test_reject_seed ON hub.risk; DROP FUNCTION hub.s40_test_reject_seed();")); }
    }
}
