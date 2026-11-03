using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Full request/transaction rules in each test's private synthetic PostgreSQL database. No hosted/browser acceptance.
public sealed class DrawingImportTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData data;
    public DrawingImportTests() { data = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Enable(bool on = true) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).EnsureSuccessStatusCode();
    static string Route(Guid project, string action) => $"/api/v1/projects/{project}/deliverables/{action}";
    static string[][] Forty(int? blank = null) => Enumerable.Range(1, 40).Select(index => new[] { index == blank ? "" : $"Drawing{index:00}", $"C-{index:000}" }).ToArray();
    async Task<Dictionary<string, object?>> Input(Guid project, string format = "csv", string[]? headers = null, string[][]? rows = null, string? sheet = null)
        => new() { ["format"] = format, ["contentBase64"] = Convert.ToBase64String(Table(format, headers ?? ["title", "client drawing number"], rows ?? Forty())),
            ["defaults"] = new { projectDisciplineId = data.ProjectDiscipline(project, "Civil"), deliverableTypeId = await data.DeliverableType() }, ["worksheet"] = sheet };
    async Task<JsonNode> Preview(Guid project, object body, string who = TestData.Pm, string? key = null)
        => await (await Send(project, "import-preview", body, who, key)).Json();
    static Dictionary<string, object?> CommitBody(Dictionary<string, object?> input, JsonNode preview)
    {
        // Invalid previews have no committable fingerprint. Supplying a deliberately false one exercises
        // direct-command row revalidation, rather than merely the missing-fingerprint precondition.
        var body = new Dictionary<string, object?>(input) { ["previewFingerprint"] = preview["previewFingerprint"]?.GetValue<string>() ?? "invalid-preview-for-refusal", ["projectRowVersion"] = preview.I("projectRowVersion") };
        return body;
    }
    Task<HttpResponseMessage> Send(Guid project, string action, object body, string who = TestData.Pm, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route(project, action)) { Content = JsonContent.Create(body, options: JsonOpts.Web) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return f.As(who).SendAsync(request);
    }
    sealed record DrawingImportState(int Records, int NextSequence, int ProjectVersion, int Members, int Follows, int Notifications, int Outbox, int Occurrences, int Logs, int Receipts);
    DrawingImportState State(Guid project) => f.Db(db => new DrawingImportState(
        db.Deliverables.IgnoreQueryFilters().Count(row => row.ProjectId == project), db.Projects.Single(row => row.Id == project).NextDeliverableSeq,
        db.Projects.Single(row => row.Id == project).RowVersion, db.ProjectMembers.Count(row => row.ProjectId == project), db.Follows.Count(row => row.ProjectId == project),
        db.Notifications.Count(), db.Outbox.Count(), db.WorkAssignmentOccurrences.Count(), db.ActivityLog.Count(row => row.ProjectId == project), db.Idempotency.Count()));

    [Theory]
    [InlineData("csv")]
    [InlineData("xlsx")]
    public async Task Every_row_is_previewed_bad_title_refuses_all_and_corrected_ordinary_file_creates_forty_normal_records(string format)
    {
        await Enable(); var project = await data.Project(); var key = Guid.NewGuid().ToString("N");
        var bad = await Input(project.Id, format, rows: Forty(17)); var before = State(project.Id);
        var preview = await Preview(project.Id, bad, key: key);
        Assert.Equal(before, State(project.Id)); // Even an explicit key creates no generic preview receipt.
        Assert.Equal(40, preview["rows"]!.AsArray().Count);
        Assert.False(preview["canCommit"]!.GetValue<bool>());
        var error = Assert.Single(preview["rows"]!.AsArray().Where(row => row!.S("classification") == "Error"));
        Assert.Equal(18, error!.I("sourceRow")); Assert.NotEmpty(error["errors"]!.AsArray());
        var refused = await Send(project.Id, "import", CommitBody(bad, preview), key: key);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode); Assert.Equal(before, State(project.Id));

        var good = await Input(project.Id, format); var valid = await Preview(project.Id, good, key: key);
        Assert.True(valid["canCommit"]!.GetValue<bool>()); Assert.Equal(40, valid.I("creates")); Assert.Equal(0, valid.I("errors"));
        Assert.All(valid["rows"]!.AsArray(), row => { Assert.True(row!["values"]!["ownerFromLead"]!.GetValue<bool>()); Assert.Null(row["sourceRowKey"]); });
        var body = CommitBody(good, valid);
        var made = await (await Send(project.Id, "import", body, key: key)).Json();
        Assert.Equal(40, made.I("total")); var ids = made["rows"]!.AsArray().Select(row => row!.G("id")).ToArray(); Assert.Equal(40, ids.Distinct().Count());
        f.Db(db => {
            var records = db.Deliverables.Where(row => row.ProjectId == project.Id).OrderBy(row => row.Seq).ToArray();
            Assert.Equal(40, records.Length); Assert.Equal(before.NextSequence + 40, db.Projects.Single(row => row.Id == project.Id).NextDeliverableSeq);
            Assert.All(records, row => { Assert.Equal(DeliverableStatus.NotStarted, row.Status); Assert.Null(row.CurrentIssueId); Assert.Null(row.IssuedDate); Assert.Null(row.Revision); Assert.Equal(data.User(TestData.Marc), row.OwnerId); });
            Assert.Equal("C-001", records[0].ClientDrawingNumber); Assert.Equal("C-040", records[39].ClientDrawingNumber);
            Assert.Empty(db.DeliverableIssues.Where(row => ids.Contains(row.DeliverableId)));
            var logs = db.ActivityLog.Where(row => row.ProjectId == project.Id && row.ItemType == "Deliverable" && row.Action == "Created").ToArray();
            Assert.Equal(40, logs.Length); Assert.All(logs, log => {
                Assert.Equal("Import", log.Source); var snapshot = JsonNode.Parse(log.Snapshot!)!;
                Assert.Equal(valid.S("contentSha256"), snapshot.S("contentHash")); Assert.Equal(format, snapshot.S("format"));
                Assert.Null(snapshot["sourceReference"]); Assert.Null(snapshot["sourceRowKey"]); Assert.Null(snapshot["contentBase64"]); Assert.Null(snapshot["title"]); Assert.Null(snapshot["file"]);
            });
            Assert.False(db.ActivityLog.Any(row => row.ProjectId == project.Id && row.Action == "Imported"));
            var receipt = db.Idempotency.Single(row => row.Key == key); Assert.Equal(200, receipt.Status);
            var receiptText = Encoding.UTF8.GetString(receipt.Body); Assert.DoesNotContain("contentBase64", receiptText); Assert.DoesNotContain((string)good["contentBase64"]!, receiptText);
            return 0;
        });
        var retry = await (await Send(project.Id, "import", body, key: key)).Json();
        Assert.True(JsonNode.DeepEquals(made, retry)); Assert.Equal(40, f.Db(db => db.Deliverables.Count(row => row.ProjectId == project.Id)));
    }

    [Fact]
    public async Task Title_only_defaults_are_explicit_mapping_is_named_and_supplied_provenance_stays_optional()
    {
        await Enable(); var project = await data.Project();
        var minimum = await Input(project.Id, headers: ["title"], rows: [["Ordinary title only"]]);
        var valid = await Preview(project.Id, minimum); Assert.True(valid["canCommit"]!.GetValue<bool>());
        var noDefaults = new Dictionary<string, object?>(minimum); noDefaults.Remove("defaults");
        Assert.False((await Preview(project.Id, noDefaults))["canCommit"]!.GetValue<bool>());
        var provenance = await Input(project.Id, headers: [" SOURCE ROW KEY ", "CLIENT DRAWING NUMBER", "TiTlE", "discipline", "type"],
            rows: [["source-001", "00012", "Mapped title", "Civil", "Drawing Package"]]);
        provenance.Remove("defaults"); provenance["sourceReference"] = " Synthetic source A ";
        var preview = await Preview(project.Id, provenance); Assert.True(preview["canCommit"]!.GetValue<bool>());
        var created = await (await Send(project.Id, "import", CommitBody(provenance, preview), key: Guid.NewGuid().ToString("N"))).Json();
        var id = created["rows"]![0]!.G("id");
        var log = f.Db(db => db.ActivityLog.Single(row => row.ItemId == id && row.Action == "Created")); var snapshot = JsonNode.Parse(log.Snapshot!)!;
        Assert.Equal("source-001", snapshot.S("sourceRowKey")); Assert.Equal(" Synthetic source A ", snapshot.S("sourceReference"));
        Assert.Equal("00012", f.Db(db => db.Deliverables.Single(row => row.Id == id).ClientDrawingNumber));
        var duplicate = await Input(project.Id, headers: ["title", "source row key"], rows: [["One", "same"], ["Two", "same"]]);
        Assert.False((await Preview(project.Id, duplicate))["canCommit"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Pm_can_import_both_disciplines_dl_own_only_and_global_pm_or_readonly_grants_no_other_project_authority()
    {
        await Enable(); var project = await data.Project();
        var input = await Input(project.Id, headers: ["title", "discipline"], rows: [["Civil drawing", "Civil"], ["Electrical drawing", "Electrical"]]);
        Assert.True((await Preview(project.Id, input))["canCommit"]!.GetValue<bool>());
        var before = State(project.Id); var mixed = await Preview(project.Id, input, TestData.Marc);
        Assert.False(mixed["canCommit"]!.GetValue<bool>()); Assert.Equal(1, mixed.I("creates")); Assert.Equal(1, mixed.I("errors"));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import", CommitBody(input, mixed), TestData.Marc, Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Equal(before, State(project.Id));
        var own = await Input(project.Id, headers: ["title"], rows: [["Own drawing"]]);
        Assert.True((await Preview(project.Id, own, TestData.Marc))["canCommit"]!.GetValue<bool>());
        foreach (var who in new[] { TestData.Rita, TestData.Sam, TestData.Jill })
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(project.Id, "import-preview", own, who)).StatusCode);
        var other = await data.Project(pm: TestData.Marc);
        var options = await (await f.As(TestData.Pm).GetAsync(Route(other.Id, "import-options"))).Json();
        Assert.False(options["canImport"]!.GetValue<bool>()); // System ProjectManager role alone is not this project's PM.
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(other.Id, "import-preview", await Input(other.Id))).StatusCode);
    }

    [Fact]
    public async Task Restricted_owner_and_reviewer_nonmembership_are_preview_errors_before_any_auto_add()
    {
        await Enable(); var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        var input = await Input(project.Id, headers: ["title", "owner email", "reviewer email"], rows: [["Restricted drawing", TestData.Jill, TestData.Diane]]);
        var before = State(project.Id); var dl = await Preview(project.Id, input, TestData.Marc);
        Assert.False(dl["canCommit"]!.GetValue<bool>());
        var fields = dl["rows"]![0]!["errors"]!.AsArray().Where(row => row!.S("code") == "restrictedMembership").Select(row => row!.S("field")).ToArray();
        Assert.Contains("owner email", fields); Assert.Contains("reviewer email", fields); Assert.Equal(before, State(project.Id));
        var pm = await Preview(project.Id, input); Assert.True(pm["canCommit"]!.GetValue<bool>()); Assert.Equal(before, State(project.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await Send(project.Id, "import-preview", input, TestData.Rita)).StatusCode);
        await (await Send(project.Id, "import", CommitBody(input, pm), key: Guid.NewGuid().ToString("N"))).Json();
        Assert.True(f.Db(db => db.ProjectMembers.Any(row => row.ProjectId == project.Id && row.UserId == data.User(TestData.Jill) && row.RemovedAt == null)));
    }

    [Fact]
    public async Task Missing_or_stale_baselines_changed_reference_versions_and_removed_restricted_memberships_refuse_without_writes()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id, headers: ["title"], rows: [["One drawing"]]); var preview = await Preview(project.Id, input);
        var before = State(project.Id); var missing = new Dictionary<string, object?>(input) { ["previewFingerprint"] = preview.S("previewFingerprint") };
        Assert.Equal((HttpStatusCode)428, (await Send(project.Id, "import", missing, key: Guid.NewGuid().ToString("N"))).StatusCode);
        var body = CommitBody(input, preview); body["previewFingerprint"] = "wrong";
        Assert.Equal(HttpStatusCode.Conflict, (await Send(project.Id, "import", body, key: Guid.NewGuid().ToString("N"))).StatusCode);
        body = CommitBody(input, preview); body["projectRowVersion"] = preview.I("projectRowVersion") - 1;
        Assert.Equal(HttpStatusCode.Conflict, (await Send(project.Id, "import", body, key: Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Equal(before, State(project.Id));
        var typeId = await data.DeliverableType();
        await f.DbAsync(async db => { (await db.DeliverableTypes.SingleAsync(row => row.Id == typeId)).Name += " renamed"; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict, (await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Equal(before.Records, State(project.Id).Records); Assert.Equal(before.NextSequence, State(project.Id).NextSequence);

        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        input = await Input(project.Id, headers: ["title", "owner email"], rows: [["Member drawing", TestData.Alex]]); preview = await Preview(project.Id, input, TestData.Marc);
        Assert.True(preview["canCommit"]!.GetValue<bool>());
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(row => row.ProjectId == project.Id && row.UserId == data.User(TestData.Alex) && row.RemovedAt == null)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        before = State(project.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import", CommitBody(input, preview), TestData.Marc, Guid.NewGuid().ToString("N"))).StatusCode);
        Assert.Equal(before, State(project.Id));
    }

    [Fact]
    public async Task Same_key_concurrent_and_lost_response_retry_create_once_changed_body_or_path_refuses_and_new_intent_creates_new_records()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id); var preview = await Preview(project.Id, input); var body = CommitBody(input, preview); var key = Guid.NewGuid().ToString("N");
        var responses = await Task.WhenAll(Send(project.Id, "import", body, key: key), Send(project.Id, "import", body, key: key));
        var first = await responses[0].Json(); var second = await responses[1].Json(); Assert.True(JsonNode.DeepEquals(first, second));
        Assert.Equal(40, f.Db(db => db.Deliverables.Count(row => row.ProjectId == project.Id)));
        var retry = await (await Send(project.Id, "import", body, key: key)).Json(); Assert.True(JsonNode.DeepEquals(first, retry));
        var changed = new Dictionary<string, object?>(body) { ["reason"] = "Different submitted body" };
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(project.Id, "import", changed, key: key)).StatusCode);
        var other = await data.Project(); Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Send(other.Id, "import", body, key: key)).StatusCode);
        var again = await Preview(project.Id, input); Assert.True(again["canCommit"]!.GetValue<bool>());
        Assert.All(again["rows"]!.AsArray(), row => { Assert.Equal("Create", row!.S("classification")); Assert.Contains(row["warnings"]!.AsArray(), warning => warning!.S("code") == "possibleDuplicate"); });
        await (await Send(project.Id, "import", CommitBody(input, again), key: Guid.NewGuid().ToString("N"))).Json();
        Assert.Equal(80, f.Db(db => db.Deliverables.Count(row => row.ProjectId == project.Id)));
        // A successful expired key is fresh intent; a new current preview is still mandatory.
        f.Clock.Now = f.Clock.Now.AddDays(2); again = await Preview(project.Id, input);
        await (await Send(project.Id, "import", CommitBody(input, again), key: key)).Json();
        Assert.Equal(120, f.Db(db => db.Deliverables.Count(row => row.ProjectId == project.Id)));
    }

    [Fact]
    public async Task Foreign_generic_pending_claim_is_never_abandoned_and_no_import_effect_escapes()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id); var preview = await Preview(project.Id, input); var key = Guid.NewGuid().ToString("N"); var actor = data.User(TestData.Pm);
        await f.DbAsync(async db => { db.Idempotency.Add(new IdempotencyRecord { UserId = actor, Key = key, Path = "/api/v1/other-command", Status = 0, ContentType = "application/json", Body = [], CreatedAt = f.Clock.Now.AddDays(-1) }); return await db.SaveChangesAsync(); });
        var before = State(project.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(project.Id, "import", CommitBody(input, preview), key: key)).StatusCode);
        Assert.Equal(before, State(project.Id));
        var prior = f.Db(db => db.Idempotency.Single(row => row.UserId == actor && row.Key == key)); Assert.Equal(0, prior.Status); Assert.Equal("/api/v1/other-command", prior.Path); Assert.Empty(prior.Body);
    }

    [Fact]
    public async Task Replay_rechecks_current_flag_and_discipline_authority_but_not_old_reference_or_project_baseline()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id, headers: ["title"], rows: [["One drawing"]]); var preview = await Preview(project.Id, input, TestData.Marc); var body = CommitBody(input, preview); var key = Guid.NewGuid().ToString("N");
        var first = await (await Send(project.Id, "import", body, TestData.Marc, key)).Json();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Name += " revised"; return await db.SaveChangesAsync(); });
        Assert.True(JsonNode.DeepEquals(first, await (await Send(project.Id, "import", body, TestData.Marc, key)).Json()));
        var actor = data.User(TestData.Marc);
        var readonlyRole = new UserSystemRole { UserId = actor, Role = SystemRole.ReadOnly, Source = RoleSource.Manual, GrantedAt = f.Clock.Now };
        await f.DbAsync(async db => { db.UserRoles.Add(readonlyRole); return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(project.Id, "import", body, TestData.Marc, key)).StatusCode);
        await f.DbAsync(db => db.UserRoles.Where(row => row.Id == readonlyRole.Id).ExecuteDeleteAsync());
        await Enable(false); Assert.Equal(HttpStatusCode.NotFound, (await Send(project.Id, "import", body, TestData.Marc, key)).StatusCode);
        await Enable(); var civil = data.ProjectDiscipline(project.Id, "Civil");
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(row => row.Id == civil)).LeadUserId = null; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(project.Id, "import", body, TestData.Marc, key)).StatusCode);
        Assert.Equal(1, f.Db(db => db.Deliverables.Count(row => row.ProjectId == project.Id)));
    }

    [Fact]
    public async Task Late_fortieth_insert_failure_rolls_back_creates_keys_members_notifications_private_publication_logs_and_receipt()
    {
        await Enable();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value = true })).EnsureSuccessStatusCode();
        var project = await data.Project();
        var input = await Input(project.Id, headers: ["title", "owner email"], rows: Enumerable.Range(1, 40).Select(index => new[] { $"Drawing{index:00}", TestData.Jill }).ToArray());
        var preview = await Preview(project.Id, input); Assert.True(preview["canCommit"]!.GetValue<bool>()); var before = State(project.Id);
        // Private test-database fault injection only. The sequence observes attempts across rollback; no product schema/migration is added.
        await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE SEQUENCE hub.drawing_import_attempts;
            CREATE FUNCTION hub.drawing_import_fail() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                PERFORM nextval('hub.drawing_import_attempts');
                IF NEW.name='Drawing40' THEN RAISE EXCEPTION 'Synthetic final-row storage failure'; END IF;
                RETURN NEW;
            END; $$;
            CREATE TRIGGER drawing_import_fail BEFORE INSERT ON hub.deliverable
            FOR EACH ROW EXECUTE FUNCTION hub.drawing_import_fail();
            """));
        var failed = await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Equal(40, await f.DbAsync(db => db.Database.SqlQueryRaw<int>("SELECT last_value::int AS \"Value\" FROM hub.drawing_import_attempts").SingleAsync()));
        Assert.Equal(before, State(project.Id));
    }

    [Fact]
    public async Task Archived_is_readonly_and_complete_requires_a_correction_reason_without_changing_manual_create()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id, headers: ["title"], rows: [["Correction drawing"]]);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var preview = await Preview(project.Id, input); Assert.True(preview["correctionReasonRequired"]!.GetValue<bool>()); Assert.False(preview["canCommit"]!.GetValue<bool>());
        input["reason"] = "Synthetic completed-project correction"; preview = await Preview(project.Id, input); Assert.True(preview["canCommit"]!.GetValue<bool>());
        var made = await (await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"))).Json(); var id = made["rows"]![0]!.G("id");
        Assert.Equal(input["reason"], f.Db(db => db.ActivityLog.Single(row => row.ItemId == id && row.Action == "Created").Reason));
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(project.Id, "import-preview", input)).StatusCode);
        var options = await (await f.As(TestData.Pm).GetAsync(Route(project.Id, "import-options"))).Json(); Assert.False(options["canImport"]!.GetValue<bool>());
        var ordinary = await data.Project(); await Enable(false);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{ordinary.Id}/deliverables", new { name = "Manual remains available", projectDisciplineId = data.ProjectDiscipline(ordinary.Id, "Civil"), deliverableTypeId = await data.DeliverableType() })).Json(201);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(ordinary.Id, "import-preview", await Input(ordinary.Id))).StatusCode);
    }

    [Fact]
    public async Task Client_number_pattern_is_a_warning_duplicates_create_and_deleted_records_are_never_restored()
    {
        await Enable(); var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).ClientDrawingNumberPattern = "C-###"; return await db.SaveChangesAsync(); });
        var input = await Input(project.Id, headers: ["title", "client drawing number", "revision"], rows: [["Existing text", "C12", "WORKING"]]); var preview = await Preview(project.Id, input);
        Assert.True(preview["canCommit"]!.GetValue<bool>()); Assert.Contains(preview["rows"]![0]!["warnings"]!.AsArray(), warning => warning!.S("code") == "numberPattern");
        var first = await (await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"))).Json(); var oldId = first["rows"]![0]!.G("id");
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(row => row.Id == oldId)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        preview = await Preview(project.Id, input); var second = await (await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"))).Json();
        Assert.NotEqual(oldId, second["rows"]![0]!.G("id")); Assert.NotNull(f.Db(db => db.Deliverables.IgnoreQueryFilters().Single(row => row.Id == oldId).DeletedAt));
        Assert.Empty(f.Db(db => db.DeliverableIssues.Where(row => row.ProjectId == project.Id).ToArray()));
    }

    [Theory]
    [InlineData("numeric")]
    [InlineData("formula")]
    [InlineData("time")]
    public async Task Unsafe_or_lossy_xlsx_values_and_forbidden_headers_never_become_creates(string kind)
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id, "xlsx", ["title", "client drawing number", "due date"], [["Drawing", "0012", "2026-11-02"]]);
        var replacement = kind switch { "numeric" => "<c r=\"B2\"><v>12</v></c>", "formula" => "<c r=\"B2\" t=\"str\"><f>1+1</f><v>0012</v></c>", _ => "<c r=\"C2\" t=\"d\"><v>2026-11-02T12:00:00</v></c>" };
        input["contentBase64"] = Convert.ToBase64String(Table("xlsx", ["title", "client drawing number", "due date"], [["Drawing", "0012", "2026-11-02"]], sheet => sheet.Replace(kind == "time" ? Inline("C2", "2026-11-02") : Inline("B2", "0012"), replacement, StringComparison.Ordinal)));
        var before = State(project.Id); var preview = await Preview(project.Id, input); Assert.False(preview["canCommit"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import", CommitBody(input, preview), key: Guid.NewGuid().ToString("N"))).StatusCode); Assert.Equal(before, State(project.Id));
        var forbidden = await Input(project.Id, headers: ["title", "status", "issued date"], rows: [["Drawing", "Issued", "2026-11-02"]]);
        Assert.False((await Preview(project.Id, forbidden))["canCommit"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Body_envelope_limits_and_unknown_members_are_refused_and_hidden_rows_stay_in_preview()
    {
        await Enable(); var project = await data.Project(); var input = await Input(project.Id, "xlsx");
        input["contentBase64"] = Convert.ToBase64String(Table("xlsx", ["title"], [["Hidden drawing"]], sheet => sheet.Replace("<row r=\"2\">", "<row r=\"2\" hidden=\"1\">", StringComparison.Ordinal)));
        var preview = await Preview(project.Id, input); Assert.True(preview["rows"]![0]!["hidden"]!.GetValue<bool>()); Assert.True(preview["canCommit"]!.GetValue<bool>());
        var extra = new Dictionary<string, object?>(input) { ["fromTaskId"] = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import-preview", extra)).StatusCode);
        input["contentBase64"] = new string('A', ((2 * 1024 * 1024 + 2) / 3) * 4 + 4);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import-preview", input)).StatusCode);
        input["contentBase64"] = "not-base64";
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(project.Id, "import-preview", input)).StatusCode);
    }

    static string Inline(string coordinate, string text) => $"<c r=\"{coordinate}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(text)}</t></is></c>";
    static byte[] Table(string format, string[] headers, string[][] records, Func<string, string>? transform = null)
    {
        var rows = new[] { headers }.Concat(records).ToArray();
        if (format == "csv") return Encoding.UTF8.GetBytes(string.Join('\n', rows.Select(row => string.Join(',', row.Select(value => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")))));
        const string main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        var sheet = $"<worksheet xmlns=\"{main}\"><sheetData>" + string.Concat(rows.Select((row, index) => $"<row r=\"{index + 1}\">" + string.Concat(row.Select((value, column) => Inline(((char)('A' + column)).ToString() + (index + 1), value))) + "</row>")) + "</sheetData></worksheet>";
        if (transform is not null) sheet = transform(sheet);
        var parts = new Dictionary<string, string> {
            ["[Content_Types].xml"] = "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>",
            ["_rels/.rels"] = $"<Relationships xmlns=\"{pkg}\"><Relationship Id=\"office\" Type=\"{rel}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>",
            ["xl/workbook.xml"] = $"<workbook xmlns=\"{main}\" xmlns:r=\"{rel}\"><sheets><sheet name=\"Drawings\" sheetId=\"1\" r:id=\"one\"/></sheets></workbook>",
            ["xl/_rels/workbook.xml.rels"] = $"<Relationships xmlns=\"{pkg}\"><Relationship Id=\"one\" Type=\"{rel}/worksheet\" Target=\"worksheets/sheet1.xml\"/></Relationships>",
            ["xl/worksheets/sheet1.xml"] = sheet
        };
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var (name, value) in parts) { using var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false)); writer.Write(value); }
        return output.ToArray();
    }
}
