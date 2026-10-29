using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The project list, header, create, edit, lifecycle, closeout, date review, star and header-link routes answer with typed records that must serialise
/// to the bytes the app already reads (040-S15-Projects). Its own host and database, so the list holds only these projects and a flag turned on here
/// cannot reach other tests.
public sealed class ProjectsContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData d = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static string[] Texts(JsonNode? n) => [.. n!.AsArray().Select(x => x!.GetValue<string>())];
    async Task Flag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Header(string as_, object idOrNumber) => await f.As(as_).GetAsync($"/api/v1/projects/{idOrNumber}").Result.Json();

    static readonly string[] RowKeys =
    [
        "id", "projectNumber", "name", "status", "priority", "visibility", "startDate", "targetCompletionDate", "client", "pm", "officeId", "office", "phaseId", "phase", "projectTypeId",
        "computedHealth", "reportedHealth", "healthReasons", "healthOverrideNote", "healthOverrideAt", "overrideBy", "overrideActive", "progressPct", "overdueTasks", "blockedTasks",
        "overdueDecisions", "highIssues", "attentionCritical", "attentionWarning", "nextMilestone", "nextSubmission", "myRoles", "starred", "lastActivityAt",
    ];
    static readonly string[] DetailKeys =
    [
        "id", "projectNumber", "name", "clientId", "clientReference", "projectManagerId", "pmName", "officeId", "projectTypeId", "description", "location", "status", "phaseId", "startDate",
        "targetCompletionDate", "priority", "visibility", "internalNotes", "coordinationDay", "allowViewerComments", "lastCoordinationReviewedAt", "lastCoordinationReviewedBy", "statusChangedAt",
        "activatedAt", "completedAt", "archivedAt", "createdFromTemplateId", "templateVersion", "rowVersion", "templateName", "client", "office", "phase", "links", "disciplines", "health",
        "progressPct", "nextMilestone", "nextSubmission", "myRoles", "follow", "starred", "setupChecklist", "suggestArchive", "editWindowDaysLeft", "permissions",
    ];
    static readonly string[] PermissionKeys =
    [
        "edit", "manageTeam", "manageMilestones", "healthOverride", "comment", "raiseRegister", "runCoordination", "createEvent", "enterTime", "proposeAllocation", "snooze",
        "manageExternalParties", "reviewDates", "captureWeeklyPlan", "createSubmission", "createTaskIn", "createDeliverableIn", "createBasisIn", "assignBasisIn", "leadOf", "isPm", "transitions",
        "changeNumber", "setVisibility", "needsReason",
    ];
    static readonly string[] HealthKeys = ["computed", "reported", "overrideActive", "healthOverride", "healthOverrideNote", "overrideBy", "healthOverrideAt", "healthOverrideExpiresAt", "reasons", "inputs", "evaluatedAt"];
    static readonly string[] MilestoneKeys = ["id", "key", "name", "date", "milestoneType", "isComplete", "deliverables", "status"];
    static readonly string[] LinkKeys = ["id", "title", "url", "linkType", "rowVersion"];

    [Fact]
    public async Task Create_edit_and_transition_answers_keep_their_ids_versions_and_refusals()
    {
        var number = TestData.Number();
        var pm = f.As(TestData.Pm);

        // Creating: 201 with a Location, the new id and the number as stored.
        var made = await pm.Post("/api/v1/projects", new { projectNumber = number, name = "Contract create", clientId = await d.Client(), officeId = await d.Office() });
        var created = await made.Json(201);
        Assert.Equal(["id", "projectNumber"], Keys(created));
        Assert.Equal(number, created.S("projectNumber"));
        Assert.Equal($"/api/v1/projects/{created.S("id")}", made.Headers.Location!.OriginalString);
        var duplicate = await (await pm.Post("/api/v1/projects", new { projectNumber = number, name = "Again", clientId = await d.Client(), officeId = await d.Office() })).Json(409);
        Assert.Equal((created.G("id"), number), (duplicate.G("existingProjectId"), duplicate.S("existingProjectNumber")));
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post("/api/v1/projects", new { projectNumber = "no spaces allowed", name = "Bad", clientId = await d.Client(), officeId = await d.Office() })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/projects", new { projectNumber = TestData.Number(), name = "x", clientId = await d.Client(), officeId = await d.Office() })).StatusCode);

        // Editing: 200 with the id and the new version, which is also the ETag; a stale version is a 409 and a missing one a 428.
        var id = created.G("id");
        var version = d.Version(id);
        var edited = await pm.Patch($"/api/v1/projects/{id}", new { description = "Edited once", location = "Hamilton" }, version);
        var saved = await edited.Json();
        Assert.Equal(["id", "rowVersion"], Keys(saved));
        Assert.Equal((id, d.Version(id)), (saved.G("id"), saved.I("rowVersion")));
        Assert.True(saved.I("rowVersion") > version);
        Assert.Equal($"\"{saved.I("rowVersion")}\"", edited.Headers.ETag!.Tag);
        Assert.Equal("concurrency_conflict", (await pm.Patch($"/api/v1/projects/{id}", new { description = "Stale" }, version).Result.Json(409)).S("code"));
        Assert.Equal(428, (int)(await pm.Patch($"/api/v1/projects/{id}", new { description = "Unversioned" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Patch($"/api/v1/projects/{id}", new { description = "Not mine" }, saved.I("rowVersion"))).StatusCode);

        // Lifecycle: 200 with the id, the new status and the new version; an illegal move is 422, a missing reason 400, a stale version 409.
        var held = await pm.Post($"/api/v1/projects/{id}/transition", new { toStatus = "Active", rowVersion = saved.I("rowVersion") }).Result.Json();
        Assert.Equal(["id", "status", "rowVersion"], Keys(held));
        Assert.Equal((id, "Active", d.Version(id)), (held.G("id"), held.S("status"), held.I("rowVersion")));
        Assert.Equal("concurrency_conflict", (await pm.Post($"/api/v1/projects/{id}/transition", new { toStatus = "On Hold", reason = "Stale", rowVersion = saved.I("rowVersion") }).Result.Json(409)).S("code"));
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{id}/transition", new { toStatus = "On Hold", rowVersion = held.I("rowVersion") })).StatusCode);
        Assert.Equal("illegal_transition", (await pm.Post($"/api/v1/projects/{id}/transition", new { toStatus = "Setup", reason = "Back", rowVersion = held.I("rowVersion") }).Result.Json(422)).S("code"));
        var onHold = await pm.Post($"/api/v1/projects/{id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = held.I("rowVersion") }).Result.Json();
        Assert.Equal(("On Hold", id), (onHold.S("status"), onHold.G("id")));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/projects/{Guid.NewGuid()}/transition", new { toStatus = "Active", rowVersion = 1 })).StatusCode);
    }

    [Fact]
    public async Task List_keeps_its_property_names_order_nulls_and_nesting()
    {
        var p = await d.Project(activate: false);
        var active = await d.Project();
        var pm = f.As(TestData.Pm);
        await pm.Post($"/api/v1/projects/{active.Id}/milestones", new { name = "Gate", milestoneType = "Design Submission", date = "2026-10-12" }).Result.Json(201);
        await pm.Post($"/api/v1/projects/{active.Id}/milestones", new { name = "Early", milestoneType = "Kickoff", date = "2026-09-30" }).Result.Json(201);
        await f.Evaluate(active.Id);
        (await pm.Post($"/api/v1/projects/{active.Id}/star", new { })).EnsureSuccessStatusCode();

        // The envelope: items, the 1-based page, the size, the total, the sort as asked (null when none) and the unused filters, null.
        var list = await pm.GetAsync($"/api/v1/projects?mine=true&ids={p.Id},{active.Id}").Result.Json();
        Assert.Equal(["items", "page", "pageSize", "totalCount", "sort", "filters"], Keys(list));
        Assert.Equal((1, 200, 2), (list.I("page"), list.I("pageSize"), list.I("totalCount")));
        Assert.True(list["sort"] is null);
        Assert.True(list.AsObject().ContainsKey("sort") && list.AsObject().ContainsKey("filters"));
        var items = list["items"]!.AsArray();
        Assert.All(items, r => Assert.Equal(RowKeys, Keys(r)));
        Assert.Equal([active.Id, p.Id], items.Select(r => r!.G("id"))); // starred first

        // An active, evaluated, starred project with two dated milestones and one submission.
        var row = items[0]!;
        Assert.Equal((active.ProjectNumber, active.Name, "Active", "Medium", "Open"), (row.S("projectNumber"), row.S("name"), row.S("status"), row.S("priority"), row.S("visibility")));
        Assert.Equal(["id", "name"], Keys(row["client"]));
        Assert.Equal(["id", "displayName"], Keys(row["pm"]));
        Assert.Equal((active.ClientId, active.ProjectManagerId), (row["client"]!.G("id"), row["pm"]!.G("id")));
        Assert.Equal("Priya Nair", row["pm"]!.S("displayName"));
        Assert.Equal((active.OfficeId, null), (row.G("officeId"), (object?)row["phaseId"]?.ToString()));
        Assert.True(row["phase"] is null && row["projectTypeId"] is null && row["startDate"] is null && row["targetCompletionDate"] is null);
        Assert.True(row.AsObject().ContainsKey("phase") && row.AsObject().ContainsKey("startDate")); // nulls are written, not left out
        Assert.Equal(row.S("computedHealth"), row.S("reportedHealth"));
        Assert.IsType<JsonArray>(row["healthReasons"]);
        Assert.False(row["overrideActive"]!.GetValue<bool>());
        Assert.True(row["healthOverrideNote"] is null && row["healthOverrideAt"] is null && row["overrideBy"] is null);
        Assert.All(new[] { "overdueTasks", "blockedTasks", "overdueDecisions", "highIssues", "attentionCritical", "attentionWarning" }, k => Assert.Equal(0, row.I(k)));
        Assert.Equal(["id", "key", "name", "date", "status"], Keys(row["nextMilestone"]));
        Assert.Equal(("Early", "2026-09-30"), (row["nextMilestone"]!.S("name"), row["nextMilestone"]!.S("date")));
        Assert.Equal(["id", "key", "name", "date", "status"], Keys(row["nextSubmission"]));
        Assert.Equal(("Gate", "2026-10-12"), (row["nextSubmission"]!.S("name"), row["nextSubmission"]!.S("date")));
        Assert.Equal(["PM"], Texts(row["myRoles"]));
        Assert.True(row["starred"]!.GetValue<bool>());
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", row.S("lastActivityAt"));
        Assert.Equal(f.Db(db => db.ProjectStates.Single(s => s.ProjectId == active.Id).ProgressPct), (int?)row["progressPct"]);

        // A project still in set-up: grey, no milestone, not starred, no evaluation counts written as zero.
        var setup = items[1]!;
        Assert.Equal(("Setup", "Grey", "Grey", false), (setup.S("status"), setup.S("computedHealth"), setup.S("reportedHealth"), setup["starred"]!.GetValue<bool>()));
        Assert.True(setup["nextMilestone"] is null && setup["nextSubmission"] is null && setup["progressPct"] is null);
        Assert.True(setup.AsObject().ContainsKey("nextMilestone") && setup.AsObject().ContainsKey("progressPct"));
        Assert.Equal(0, setup.I("overdueTasks"));

        // A reason behind the health keeps its members; an override shows its note, who set it and when, and reports its colour while it lasts.
        await f.DbAsync(async db =>
        {
            var state = await db.ProjectStates.SingleAsync(s => s.ProjectId == active.Id);
            state.HealthReasons = """[{"rule":"R1","text":"Two tasks overdue","colour":"Yellow","values":{"overdue":2},"threshold":1}]""";
            return await db.SaveChangesAsync();
        });
        var overridden = await pm.Post($"/api/v1/projects/{active.Id}/health-override", new { health = "Red", note = "Waiting on the client", rowVersion = d.Version(active.Id) }).Result.Json();
        Assert.NotNull(overridden);
        var after = (await pm.GetAsync($"/api/v1/projects?ids={active.Id}").Result.Json())["items"]![0]!;
        Assert.Equal(RowKeys, Keys(after));
        Assert.Equal("Red", after.S("reportedHealth"));
        Assert.True(after["overrideActive"]!.GetValue<bool>());
        Assert.Equal(("Waiting on the client", "Priya Nair"), (after.S("healthOverrideNote"), after.S("overrideBy")));
        Assert.Equal("""[{"rule":"R1","text":"Two tasks overdue","colour":"Yellow","values":{"overdue":2},"threshold":1}]""", after["healthReasons"]!.ToJsonString());

        // The filters, the sort and the page size are answered in the envelope as asked.
        var sorted = await pm.GetAsync($"/api/v1/projects?ids={p.Id},{active.Id}&sort=number:desc&pageSize=1&page=2").Result.Json();
        Assert.Equal(("number:desc", 1, 2, 2), (sorted.S("sort"), sorted.I("pageSize"), sorted.I("page"), sorted.I("totalCount")));
        Assert.Single(sorted["items"]!.AsArray());
        Assert.Equal(["Setup"], (await pm.GetAsync($"/api/v1/projects?ids={p.Id},{active.Id}&status=Setup").Result.Json())["items"]!.AsArray().Select(r => r!.S("status")));
        Assert.Equal([active.Id], (await pm.GetAsync($"/api/v1/projects?ids={p.Id},{active.Id}&starred=true").Result.Json())["items"]!.AsArray().Select(r => r!.G("id")));

        // The contract names the query members with a capital (Mine, Ids, PageSize, ...); they bind as the page's lowercase names do.
        var typed = await pm.GetAsync($"/api/v1/projects?Mine=true&Ids={p.Id},{active.Id}&PageSize=1&IncludeArchived=true&Sort=name:desc").Result.Json();
        Assert.Equal((1, 2, "name:desc"), (typed.I("pageSize"), typed.I("totalCount"), typed.S("sort")));
        Assert.Equal(["Setup"], (await pm.GetAsync($"/api/v1/projects?Ids={p.Id},{active.Id}&Status=Setup&Mine=true").Result.Json())["items"]!.AsArray().Select(r => r!.S("status")));

        // A viewer who may not see a restricted project does not get its row; a member is shown it with their role.
        var asAlex = (await f.As(TestData.Alex).GetAsync($"/api/v1/projects?ids={active.Id}").Result.Json())["items"]![0]!;
        Assert.Equal(["TeamMember"], Texts(asAlex["myRoles"]));
        var asMarc = (await f.As(TestData.Marc).GetAsync($"/api/v1/projects?ids={active.Id}").Result.Json())["items"]![0]!;
        Assert.Equal(["TeamMember", "DisciplineLead"], Texts(asMarc["myRoles"])); // the lead role is added last
    }

    [Fact]
    public async Task Header_keeps_its_property_names_order_nulls_nesting_and_flag_gated_members()
    {
        var setup = await d.Project(activate: false);
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        await pm.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Gate", milestoneType = "Design Submission", date = "2026-10-12" }).Result.Json(201);
        await f.Evaluate(p.Id);
        (await pm.Post($"/api/v1/projects/{p.Id}/star", new { })).EnsureSuccessStatusCode();
        (await pm.Post($"/api/v1/projects/{p.Id}/links", new { url = "https://example.test/one", title = "One" })).EnsureSuccessStatusCode();

        // The header read by id and by number is the same answer, in this order, with every null written.
        var h = await Header(TestData.Pm, p.Id);
        Assert.Equal(h.ToJsonString(), (await Header(TestData.Pm, p.ProjectNumber)).ToJsonString());
        Assert.Equal(DetailKeys, Keys(h));
        Assert.Equal((p.ProjectNumber, p.Name, "Active", "Medium", "Open", "Priya Nair"), (h.S("projectNumber"), h.S("name"), h.S("status"), h.S("priority"), h.S("visibility"), h.S("pmName")));
        Assert.Equal((p.ClientId, p.OfficeId, p.ProjectManagerId), (h.G("clientId"), h.G("officeId"), h.G("projectManagerId")));
        Assert.True(h["clientReference"] is null && h["description"] is null && h["completedAt"] is null && h["templateName"] is null && h["lastCoordinationReviewedBy"] is null);
        Assert.True(h["internalNotes"] is null && h["phase"] is null);
        Assert.True(h.AsObject().ContainsKey("follow") && h.AsObject().ContainsKey("internalNotes"));
        Assert.True(h["allowViewerComments"]!.GetValue<bool>());
        Assert.False(string.IsNullOrEmpty(h.S("client")));
        Assert.False(string.IsNullOrEmpty(h.S("office")));
        Assert.Equal(f.Db(db => db.Projects.Single(x => x.Id == p.Id).RowVersion), h.I("rowVersion"));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", h.S("activatedAt"));
        Assert.True(h["starred"]!.GetValue<bool>());
        Assert.Equal(["PM"], Texts(h["myRoles"]));

        var links = h["links"]!.AsArray();
        Assert.Equal(LinkKeys, Keys(Assert.Single(links)));
        Assert.Equal(("One", "https://example.test/one"), (links[0]!.S("title"), links[0]!.S("url")));

        var disciplines = h["disciplines"]!.AsArray();
        Assert.Equal(["Civil", "Electrical"], disciplines.Select(x => x!.S("name")));
        Assert.All(disciplines, x => Assert.Equal(["id", "disciplineId", "name", "code", "colour", "leadUserId", "isActive", "rowVersion", "leadName"], Keys(x)));
        Assert.Equal(("Marc Dubois", true), (disciplines[0]!.S("leadName"), disciplines[0]!["isActive"]!.GetValue<bool>()));

        Assert.Equal(HealthKeys, Keys(h["health"]));
        Assert.Equal(h["health"]!.S("computed"), h["health"]!.S("reported"));
        Assert.False(h["health"]!["overrideActive"]!.GetValue<bool>());
        Assert.True(h["health"]!["healthOverride"] is null && h["health"]!["overrideBy"] is null && h["health"]!["healthOverrideExpiresAt"] is null);
        Assert.IsType<JsonArray>(h["health"]!["reasons"]);
        Assert.IsType<JsonObject>(h["health"]!["inputs"]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T", h["health"]!.S("evaluatedAt"));
        Assert.Equal(MilestoneKeys, Keys(h["nextMilestone"]));
        Assert.Equal(MilestoneKeys, Keys(h["nextSubmission"]));
        Assert.Equal(("Gate", "Design Submission", false, 0), (h["nextMilestone"]!.S("name"), h["nextMilestone"]!.S("milestoneType"), h["nextMilestone"]!["isComplete"]!.GetValue<bool>(), h["nextMilestone"]!.I("deliverables")));
        Assert.True(h["setupChecklist"] is null && h["editWindowDaysLeft"] is null && !h["suggestArchive"]!.GetValue<bool>());

        // The permissions: every flag is a verdict with its reason (null when allowed), the lists are discipline rows, the rest are plain values. No restore key while the flag is off.
        var perms = h["permissions"]!;
        Assert.Equal(PermissionKeys, Keys(perms));
        foreach (var k in new[] { "edit", "manageTeam", "manageMilestones", "healthOverride", "comment", "raiseRegister", "runCoordination", "createEvent", "enterTime", "proposeAllocation", "snooze",
            "manageExternalParties", "reviewDates", "captureWeeklyPlan", "createSubmission" })
        {
            Assert.Equal(["ok", "reason"], Keys(perms[k]));
            Assert.True(perms[k]!.AsObject().ContainsKey("reason"));
        }
        Assert.True(perms["edit"]!["ok"]!.GetValue<bool>() && perms["edit"]!["reason"] is null);
        Assert.True(perms["reviewDates"]!["ok"]!.GetValue<bool>() && perms["reviewDates"]!["reason"] is null);
        Assert.Equal(new[] { d.ProjectDiscipline(p.Id, "Civil"), d.ProjectDiscipline(p.Id, "Electrical") }.Order(), perms["createTaskIn"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())).Order());
        Assert.Equal(new[] { d.ProjectDiscipline(p.Id, "Civil"), d.ProjectDiscipline(p.Id, "Electrical") }.Order(), perms["createDeliverableIn"]!.AsArray().Select(x => Guid.Parse(x!.GetValue<string>())).Order());
        Assert.True(perms["isPm"]!.GetValue<bool>());
        Assert.Contains("On Hold", Texts(perms["transitions"]));
        Assert.False(perms["changeNumber"]!.GetValue<bool>());
        Assert.False(perms["needsReason"]!.GetValue<bool>());

        // A project in set-up carries its checklist; a member who cannot manage sees refusal texts and no internal notes.
        var s = await Header(TestData.Pm, setup.Id);
        Assert.Equal(DetailKeys, Keys(s));
        Assert.Equal(["hasMilestone", "everyDisciplineHasLead", "everySubmissionHasDeliverable"], Keys(s["setupChecklist"]));
        Assert.Equal((false, true, true), (s["setupChecklist"]!["hasMilestone"]!.GetValue<bool>(), s["setupChecklist"]!["everyDisciplineHasLead"]!.GetValue<bool>(), s["setupChecklist"]!["everySubmissionHasDeliverable"]!.GetValue<bool>()));
        Assert.True(s["nextMilestone"] is null && s["nextSubmission"] is null && s["progressPct"] is null);
        (await pm.Patch($"/api/v1/projects/{p.Id}", new { internalNotes = "Private note", coordinationDay = "Tuesday" }, d.Version(p.Id))).EnsureSuccessStatusCode();
        Assert.Equal("Private note", (await Header(TestData.Pm, p.Id)).S("internalNotes"));
        var alex = await Header(TestData.Alex, p.Id);
        Assert.Equal(DetailKeys, Keys(alex));
        Assert.Equal("Private note", alex.S("internalNotes")); // a member sees them
        Assert.True((await Header(TestData.Rita, p.Id))["internalNotes"] is null); // someone outside the team does not
        Assert.False(alex["permissions"]!["edit"]!["ok"]!.GetValue<bool>());
        Assert.Equal(Hub.Api.Text.Get("perm.pm", "Active"), alex["permissions"]!["edit"]!.S("reason"));
        Assert.Equal("Tuesday", alex.S("coordinationDay"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Alex).GetAsync("/api/v1/projects/NOPE-0000")).StatusCode);

        // A reported override and a follow are written under health and follow.
        var overridden = await pm.Post($"/api/v1/projects/{p.Id}/health-override", new { health = "Yellow", note = "Client review", rowVersion = d.Version(p.Id) }).Result.Json();
        Assert.NotNull(overridden);
        var o = await Header(TestData.Pm, p.Id);
        Assert.Equal(HealthKeys, Keys(o["health"]));
        Assert.Equal(("Yellow", true, "Yellow", "Client review", "Priya Nair"), (o["health"]!.S("reported"), o["health"]!["overrideActive"]!.GetValue<bool>(), o["health"]!.S("healthOverride"), o["health"]!.S("healthOverrideNote"), o["health"]!.S("overrideBy")));
        var follow = (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/follow", new { level = "AllActivity" }));
        if (follow.IsSuccessStatusCode) Assert.Equal(["level", "source"], Keys((await Header(TestData.Alex, p.Id))["follow"]));

        // The flag-gated members: the restore verdict joins the permissions, the note and the latest planned date join the header, each only while its flag is on.
        await Flag("show_deleted", true);
        await Flag("project_status_note", true);
        await Flag("schedule_baseline", true);
        try
        {
            await f.DbAsync(async db => { db.Milestones.Add(new Milestone { ProjectId = p.Id, Seq = 907, Key = "M0907", Name = "Late gate", Date = new DateOnly(2027, 3, 1) }); return await db.SaveChangesAsync(); });
            await f.Evaluate(p.Id);
            var enabledWithoutNote = await Header(TestData.Pm, p.Id);
            Assert.Equal([.. DetailKeys, "statusNote"], Keys(enabledWithoutNote));
            Assert.True(enabledWithoutNote.AsObject().ContainsKey("statusNote") && enabledWithoutNote["statusNote"] is null);
            (await pm.Patch($"/api/v1/projects/{p.Id}", new { statusNote = "On track", targetCompletionDate = "2026-12-15" }, d.Version(p.Id))).EnsureSuccessStatusCode();
            var on = await Header(TestData.Pm, p.Id);
            Assert.Equal([.. DetailKeys, "statusNote", "latestPlanned"], Keys(on));
            Assert.Equal([.. PermissionKeys, "restore"], Keys(on["permissions"]));
            Assert.Equal(["ok", "reason"], Keys(on["permissions"]!["restore"]));
            Assert.Equal(["text", "authorId", "author", "at", "header"], Keys(on["statusNote"]));
            Assert.Equal(["date", "itemKey", "targetDate", "rule"], Keys(on["latestPlanned"]));
            Assert.Equal(("2027-03-01", "M0907", "2026-12-15", "FR-INT-15"), (on["latestPlanned"]!.S("date"), on["latestPlanned"]!.S("itemKey"), on["latestPlanned"]!.S("targetDate"), on["latestPlanned"]!.S("rule")));
        }
        finally { await Flag("show_deleted", false); await Flag("project_status_note", false); await Flag("schedule_baseline", false); }
        var off = await Header(TestData.Pm, p.Id);
        Assert.Equal(DetailKeys, Keys(off));
        Assert.Equal(PermissionKeys, Keys(off["permissions"]));
    }

    [Fact]
    public async Task Closeout_preview_and_date_review_keep_their_property_names_order_and_nulls()
    {
        var p = await d.Project();
        var pm = f.As(TestData.Pm);
        var saved = f.Clock.Now;
        try
        {
            // Nothing open: empty lists and a zero count are written, in this order.
            var empty = await pm.GetAsync($"/api/v1/projects/{p.Id}/closeout").Result.Json();
            Assert.Equal(["status", "openTasks", "tasks", "deliverables", "decisions", "openRegisterItems"], Keys(empty));
            Assert.Equal(("Active", 0, 0), (empty.S("status"), empty.I("openTasks"), empty.I("openRegisterItems")));
            Assert.All(new[] { "tasks", "deliverables", "decisions" }, k => Assert.Empty(empty[k]!.AsArray()));

            // One open item of each kind and a register item of each.
            var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Open task", dueDate = "2026-09-20" });
            var deliverable = await pm.Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Open package", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = "2026-09-25" }).Result.Json(201);
            var decision = await pm.Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Open question", description = "What must be decided", requiredByDate = "2026-09-30", impactLevel = "Medium", impactDescription = "Drawings cannot progress", ownerUserId = d.User(TestData.Pm) }).Result.Json(201);
            await pm.Post($"/api/v1/projects/{p.Id}/risks", new { title = "Open risk", description = "What could happen", probability = 2, impact = 2 }).Result.Json(201);
            var open = await pm.GetAsync($"/api/v1/projects/{p.Id}/closeout").Result.Json();
            Assert.Equal(["status", "openTasks", "tasks", "deliverables", "decisions", "openRegisterItems"], Keys(open));
            Assert.Equal((1, 1, 1, 1), (open.I("openTasks"), open["deliverables"]!.AsArray().Count, open["decisions"]!.AsArray().Count, open.I("openRegisterItems")));
            Assert.Equal(["id", "key", "name", "status"], Keys(open["tasks"]![0]));
            Assert.Equal(["id", "key", "name", "status"], Keys(open["deliverables"]![0]));
            Assert.Equal(["id", "key", "name", "status"], Keys(open["decisions"]![0]));
            Assert.Equal((task.S("key"), "Open task"), (open["tasks"]![0]!.S("key"), open["tasks"]![0]!.S("name")));
            Assert.Equal((deliverable.S("key"), "Open package"), (open["deliverables"]![0]!.S("key"), open["deliverables"]![0]!.S("name")));
            Assert.Equal((decision.S("key"), "Open question"), (open["decisions"]![0]!.S("key"), open["decisions"]![0]!.S("name"))); // the subject is the name
            Assert.Equal(HttpStatusCode.NotFound, (await pm.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/closeout")).StatusCode);

            // Date review: null window with empty lists while nothing was on hold, then the window and the work whose dates passed.
            var none = await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json();
            Assert.Equal(["window", "tasks", "deliverables"], Keys(none));
            Assert.True(none.AsObject().ContainsKey("window") && none["window"] is null);
            Assert.Empty(none["tasks"]!.AsArray());
            Assert.Empty(none["deliverables"]!.AsArray());
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{p.Id}/date-review")).StatusCode);

            await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "On Hold", reason = "Client paused the work", rowVersion = d.Version(p.Id) }).Result.Json();
            f.Clock.SetDate(2026, 10, 5);
            await pm.Post($"/api/v1/projects/{p.Id}/transition", new { toStatus = "Active", reason = "Client resumed the work", rowVersion = d.Version(p.Id) }).Result.Json();
            var review = await pm.GetAsync($"/api/v1/projects/{p.Id}/date-review").Result.Json();
            Assert.Equal(["window", "tasks", "deliverables"], Keys(review));
            Assert.Equal(["heldFrom", "resumedOn", "days"], Keys(review["window"]));
            Assert.Equal(("2026-09-14", "2026-10-05", 21), (review["window"]!.S("heldFrom"), review["window"]!.S("resumedOn"), review["window"]!.I("days")));
            Assert.Equal([task.S("key")], review["tasks"]!.AsArray().Select(x => x!.S("key")));
            Assert.Equal([deliverable.S("key")], review["deliverables"]!.AsArray().Select(x => x!.S("key")));
            Assert.Contains("name", Keys(review["tasks"]![0]));
            Assert.Contains("dueDate", Keys(review["deliverables"]![0]));
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Star_and_header_link_commands_answer_with_their_status_body_and_refusals()
    {
        var p = await d.Project();
        var other = await d.Project(pm: TestData.Marc);
        var (pm, alex) = (f.As(TestData.Pm), f.As(TestData.Alex));
        async Task<bool> Starred(HttpClient c) => (await c.GetAsync($"/api/v1/projects/{p.Id}").Result.Json())["starred"]!.GetValue<bool>();

        // Starring and unstarring: 204 and no body, each a second time too; the star shows on the header and the list and belongs to the person.
        Assert.False(await Starred(pm));
        var star = await pm.PostAsync($"/api/v1/projects/{p.Id}/star", null);
        Assert.Equal(HttpStatusCode.NoContent, star.StatusCode);
        Assert.Empty(await star.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await pm.PostAsync($"/api/v1/projects/{p.Id}/star", null)).StatusCode);
        Assert.Equal(1, f.Db(db => db.ProjectStars.Count(s => s.ProjectId == p.Id)));
        Assert.True(await Starred(pm));
        Assert.False(await Starred(alex));
        Assert.Equal([p.Id], (await pm.GetAsync($"/api/v1/projects?starred=true&ids={p.Id},{other.Id}").Result.Json())["items"]!.AsArray().Select(r => r!.G("id")));
        var unstar = await pm.DeleteAsync($"/api/v1/projects/{p.Id}/star");
        Assert.Equal(HttpStatusCode.NoContent, unstar.StatusCode);
        Assert.Empty(await unstar.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await pm.DeleteAsync($"/api/v1/projects/{p.Id}/star")).StatusCode);
        Assert.False(await Starred(pm));
        Assert.Equal(HttpStatusCode.NotFound, (await pm.PostAsync($"/api/v1/projects/{Guid.NewGuid()}/star", null)).StatusCode);

        // Header links: 201 with a Location and the row, in this order; the type is detected; a bad address is a 400 and a non-PM is refused.
        var made = await pm.Post($"/api/v1/projects/{p.Id}/links", new { url = "https://example.test/folder", title = "Project folder" });
        var link = await made.Json(201);
        Assert.Equal(LinkKeys, Keys(link));
        Assert.Equal(("Project folder", "https://example.test/folder"), (link.S("title"), link.S("url")));
        Assert.Equal($"/api/v1/links/{link.S("id")}", made.Headers.Location!.OriginalString);
        Assert.Equal(f.Db(db => db.DocumentLinks.Single(l => l.Id == link.G("id")).RowVersion), link.I("rowVersion"));
        var typed = await pm.Post($"/api/v1/projects/{p.Id}/links", new { url = "https://example.test/sheet", linkType = "Other" }).Result.Json(201);
        Assert.Equal(("Other", false), (typed.S("linkType"), string.IsNullOrEmpty(typed.S("title"))));
        Assert.Equal(HttpStatusCode.BadRequest, (await pm.Post($"/api/v1/projects/{p.Id}/links", new { url = "not a link", title = "Bad" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.Post($"/api/v1/projects/{p.Id}/links", new { url = "https://example.test/x", title = "Mine" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Post($"/api/v1/projects/{Guid.NewGuid()}/links", new { url = "https://example.test/x", title = "Nowhere" })).StatusCode);
        Assert.Equal(["Project folder", typed.S("title")], (await Header(TestData.Pm, p.Id))["links"]!.AsArray().Select(x => x!.S("title")));

        // Removing: a stale version is 409, a missing one 428, then 204 with no body; a second removal and another project's link are 404.
        Assert.Equal(428, (int)(await pm.DeleteAsync($"/api/v1/projects/{p.Id}/links/{link.S("id")}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await pm.Delete($"/api/v1/projects/{p.Id}/links/{link.S("id")}", link.I("rowVersion") + 5)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await alex.Delete($"/api/v1/projects/{p.Id}/links/{link.S("id")}", link.I("rowVersion"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.Delete($"/api/v1/projects/{other.Id}/links/{link.S("id")}", link.I("rowVersion"))).StatusCode); // not the PM of that project
        var gone = await pm.Delete($"/api/v1/projects/{p.Id}/links/{link.S("id")}", link.I("rowVersion"));
        Assert.Equal(HttpStatusCode.NoContent, gone.StatusCode);
        Assert.Empty(await gone.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await pm.Delete($"/api/v1/projects/{p.Id}/links/{link.S("id")}", link.I("rowVersion"))).StatusCode);
        Assert.Equal([typed.S("title")], (await Header(TestData.Pm, p.Id))["links"]!.AsArray().Select(x => x!.S("title")));
    }
}
