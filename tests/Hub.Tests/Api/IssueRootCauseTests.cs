using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

public sealed class IssueRootCauseTests
{
    [Fact] // AC-SCR-71: explicit organisation category, required selection, canonical activity and response.
    public async Task Resolution_requires_cause_and_records_the_checked_label_with_one_normal_activity()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        var before = t.State(p.Id); await t.Move(issue, cause: null, status: 400); Assert.Equal(before, t.State(p.Id));
        var reply = await t.Move(issue, cause); Assert.Equal(new[] { "id", "status", "rowVersion" }, reply.AsObject().Select(row => row.Key));
        var row = t.F.Db(db => db.Issues.Single(row => row.Id == issue));
        Assert.Equal(IssueStatus.Resolved, row.Status); Assert.Equal(cause, row.RootCauseCategoryId); Assert.Equal("Coordination gap", row.RootCauseLabelSnapshot);
        Assert.Equal(1, t.F.Db(db => db.ActivityLog.Count(log => log.ItemId == issue && log.Action == "Resolved")));
        var detail = await t.Detail(issue); Assert.Equal("Coordination gap", detail["issue"]!["rootCause"]!.S("recordedName"));
        var summary = await t.Summary(p.Id); Assert.Equal(1, summary.I("resolvedTotal")); Assert.Equal(1, summary["causes"]![0]!.I("count"));
    }

    [Theory] // AC-SCR-71 / AC-SCR-84: new metadata never waives existing validation, authority or optimistic concurrency.
    [InlineData("missing")][InlineData("empty")][InlineData("unknown")][InlineData("wrong-kind")][InlineData("retired")]
    [InlineData("resolution")][InlineData("future")][InlineData("stale")][InlineData("readonly")][InlineData("outsider")]
    public async Task Refused_resolution_is_atomic(string failure)
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); Guid? cause = await t.Cause();
        if (failure == "retired") await t.EditCause(cause.Value, new { isActive = false });
        if (failure == "missing") cause = null; if (failure == "empty") cause = Guid.Empty; if (failure == "unknown") cause = Guid.NewGuid();
        if (failure == "wrong-kind") cause = t.Data.User(TestData.Marc);
        var before = t.State(p.Id);
        await t.Move(issue, cause, who: failure == "readonly" ? TestData.Rita : failure == "outsider" ? TestData.Jill : TestData.Alex,
            resolution: failure == "resolution" ? "" : "The clash was resolved", date: failure == "future" ? "2099-01-01" : null,
            version: failure == "stale" ? t.Version(issue) + 1 : null, status: failure is "readonly" or "outsider" ? 403 : failure == "stale" ? 409 : 400);
        Assert.Equal(before, t.State(p.Id));
    }

    [Fact] // AC-SCR-71: classification cannot be supplied on another transition or overwrite history through a text edit.
    public async Task Non_resolution_cause_is_refused_and_ordinary_edit_retains_the_recorded_pair()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        var before = t.State(p.Id); await t.Move(issue, cause, to: IssueStatus.InProgress, status: 400); Assert.Equal(before, t.State(p.Id));
        await t.Move(issue, cause);
        await (await t.F.As(TestData.Alex).Patch($"/api/v1/issues/{issue}", new { title = "Corrected issue text" }, t.Version(issue))).Json();
        var row = t.F.Db(db => db.Issues.Single(row => row.Id == issue)); Assert.Equal(cause, row.RootCauseCategoryId); Assert.Equal("Coordination gap", row.RootCauseLabelSnapshot);
        Assert.Equal(1, t.F.Db(db => db.ActivityLog.Count(log => log.ItemId == issue && log.Action == "Resolved")));
    }

    [Fact] // AC-SCR-71 / AC-SCR-84: canonical completed-project correction and write authority remain in force.
    public async Task Complete_project_resolution_requires_the_original_correction_reason()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == p.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var before = t.State(p.Id);
        await t.Move(issue, cause, who: TestData.Pm, status: 400);
        await t.Move(issue, cause, who: TestData.Pm, reason: "four", status: 400);
        await t.Move(issue, cause, who: TestData.Alex, reason: "Correct the completed project record", status: 403);
        Assert.Equal(before, t.State(p.Id));
        await t.Move(issue, cause, who: TestData.Pm, reason: "Correct the completed project record");
        Assert.Equal(ProjectStatus.Complete, t.F.Db(db => db.Projects.Single(row => row.Id == p.Id).Status));
        Assert.Equal(cause, t.F.Db(db => db.Issues.Single(row => row.Id == issue).RootCauseCategoryId));
    }

    [Fact] // AC-SCR-71: a provider failure rolls back the issue, cause pair, activity, version and command receipt together.
    public async Task Provider_failure_does_not_leave_a_partial_resolution()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        // Scoped to this fixture's disposable PostgreSQL database; no production trigger is introduced.
        await t.F.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION hub.s41_test_reject_resolution() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'Synthetic resolution failure'; END $$;
            CREATE TRIGGER s41_test_reject_resolution BEFORE UPDATE ON hub.issue FOR EACH ROW WHEN (NEW.root_cause_category_id IS NOT NULL) EXECUTE FUNCTION hub.s41_test_reject_resolution();
            """));
        var before = t.State(p.Id);
        try { await t.Move(issue, cause, status: 500); Assert.Equal(before, t.State(p.Id)); }
        finally { await t.F.DbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER s41_test_reject_resolution ON hub.issue; DROP FUNCTION hub.s41_test_reject_resolution();")); }
    }

    [Fact] // AC-SCR-71: fresh resolution, reopening retention and legacy off re-resolution are separate facts.
    public async Task Reopen_retains_previous_cause_but_new_resolution_never_reuses_it_implicitly()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        await t.Move(issue, cause); await t.Move(issue, to: IssueStatus.InProgress, reason: "The recorded clash has returned");
        Assert.Equal(cause, t.F.Db(db => db.Issues.Single(row => row.Id == issue).RootCauseCategoryId));
        Assert.Equal(0, (await t.Summary(p.Id)).I("resolvedTotal"));
        var before = t.State(p.Id); await t.Move(issue, status: 400); Assert.Equal(before, t.State(p.Id));
        await t.EditCause(cause, new { name = "Coordination renamed", isActive = false });
        before = t.State(p.Id); await t.Move(issue, cause, status: 400); Assert.Equal(before, t.State(p.Id));
        await t.Flag(false); await t.Move(issue);
        var row = t.F.Db(db => db.Issues.Single(row => row.Id == issue)); Assert.Null(row.RootCauseCategoryId); Assert.Null(row.RootCauseLabelSnapshot);
        await t.Flag(true); Assert.Equal(1, (await t.Summary(p.Id)).I("notRecordedCount"));
        Assert.Equal(2, t.F.Db(db => db.ActivityLog.Count(log => log.ItemId == issue && log.Action == "Resolved")));
    }

    [Fact] // AC-SCR-71: root cause does not replace current independent coordination verification or evidence.
    public async Task Coordination_issue_requires_existing_independent_verification_before_resolution()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var cause = await t.Cause();
        var issue = (await t.Post(TestData.Alex, $"/api/v1/projects/{p.Id}/issues", new { title = "Real coordination issue", severity = "High", ownerId = t.Data.User(TestData.Alex),
            projectDisciplineId = t.Data.ProjectDiscipline(p.Id, "Civil"), issueType = "Coordination", locations = new[] { new { kind = "SiteArea", siteArea = "Synthetic yard" } } }, 201)).G("id");
        var before = t.State(p.Id); await t.Move(issue, cause, status: 400); Assert.Equal(before, t.State(p.Id));
        await t.Post(TestData.Pm, $"/api/v1/issues/{issue}/verification", new { verifierId = t.Data.User(TestData.Marc), status = "Proposed", note = "Appoint an independent verifier", rowVersion = t.Version(issue) }, 201);
        await t.Post(TestData.Marc, $"/api/v1/issues/{issue}/verification", new { verifierId = t.Data.User(TestData.Marc), status = "Verified", evidenceUrl = "https://example.test/verified.pdf", rowVersion = t.Version(issue) }, 201);
        await t.Move(issue, cause); Assert.Equal(IssueStatus.Resolved, (await t.Detail(issue))["issue"]!.S("status"));
    }

    [Theory] // AC-SCR-84: auth-only pre-replay guard honors charset and does not rerun stale/current-state feasibility.
    [InlineData(false)][InlineData(true)]
    public async Task Cached_feature_resolution_replays_once_and_refuses_while_off_or_authority_revoked(bool utf16)
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        var body = new RegisterEndpoints.IssueMove("Resolved", null, "One checked resolution", null, t.Version(issue), cause); var key = Guid.NewGuid().ToString();
        async Task<JsonNode> Send(int status = 200)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/issues/{issue}/transition") { Content = new StringContent(JsonSerializer.Serialize(body, JsonOpts.Web), utf16 ? Encoding.Unicode : Encoding.UTF8, "application/json") };
            request.Headers.Add("Idempotency-Key", key); return await (await t.F.As(TestData.Alex).SendAsync(request)).Json(status);
        }
        var first = await Send(); var before = t.State(p.Id); Assert.True(JsonNode.DeepEquals(first, await Send())); Assert.Equal(before, t.State(p.Id));
        await t.Flag(false); before = t.State(p.Id); await Send(404); Assert.Equal(before, t.State(p.Id));
        await t.Flag(true);
        await t.F.DbAsync(async db => { var row = await db.Issues.SingleAsync(row => row.Id == issue); row.OwnerId = t.Data.User(TestData.Marc); row.RaisedById = t.Data.User(TestData.Marc); return await db.SaveChangesAsync(); });
        before = t.State(p.Id); await Send(403); Assert.Equal(before, t.State(p.Id));
    }

    [Fact] // AC-SCR-84: an old committed request remains an old unknown cause; optional extension preserves serialized fields.
    public async Task Old_body_receipt_and_flag_off_shapes_remain_unchanged_after_enable()
    {
        using var t = new IssueRootCauseFixture(); var p = await t.Data.Project(); var issue = await t.Issue(p.Id);
        var old = new { toStatus = "Resolved", reason = (string?)null, resolution = "Legacy resolution", resolvedDate = (DateOnly?)null, rowVersion = t.Version(issue) };
        Assert.Equal(JsonSerializer.Serialize(old, JsonOpts.Web), JsonSerializer.Serialize(new RegisterEndpoints.IssueMove(old.toStatus, old.reason, old.resolution, old.resolvedDate, old.rowVersion), JsonOpts.Web));
        var key = Guid.NewGuid().ToString(); var client = t.F.As(TestData.Alex); client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var first = await (await client.Post($"/api/v1/issues/{issue}/transition", old)).Json(); await t.Flag(true);
        var before = t.State(p.Id); var replay = await (await client.Post($"/api/v1/issues/{issue}/transition", old)).Json();
        Assert.True(JsonNode.DeepEquals(first, replay)); Assert.Equal(before, t.State(p.Id)); Assert.Null((await t.Detail(issue))["issue"]!["rootCause"]);
        await t.Flag(false); before = t.State(p.Id);
        await t.Move(issue, Guid.NewGuid(), status: 404); Assert.Equal(before, t.State(p.Id));
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/issues/root-causes")).StatusCode);
        Assert.False((await t.Detail(issue))["issue"]!.AsObject().ContainsKey("rootCause"));
    }

    [Fact] // AC-SCR-71: actual FOR SHARE lock blocks a concurrent non-key retirement/rename through resolution commit.
    public async Task Resolution_snapshot_commits_before_waiting_category_retirement()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); var cause = await t.Cause();
        await using var connection = new NpgsqlConnection($"Host=localhost;Port=55432;Database={t.F.Database};Username=hub"); await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var hold = new NpgsqlCommand("SELECT id FROM hub.issue WHERE id=@id FOR UPDATE", connection, transaction)) { hold.Parameters.AddWithValue("id", issue); await hold.ExecuteScalarAsync(); }
        var released = false; Task<JsonNode>? retirement = null; var resolution = t.Move(issue, cause);
        try
        {
            await WaitFor("UPDATE hub.issue SET"); // API has locked the category and now waits on this issue's UPDATE.
            retirement = t.EditCause(cause, new { name = "Retired after resolution", isActive = false });
            await WaitFor("UPDATE hub.issue_root_cause_category SET"); // FOR KEY SHARE would not hold this non-key UPDATE.
            Assert.False(resolution.IsCompleted); Assert.False(retirement.IsCompleted);
            await transaction.CommitAsync(); released = true;
            await resolution.WaitAsync(TimeSpan.FromSeconds(20)); await retirement.WaitAsync(TimeSpan.FromSeconds(20));
        }
        finally { if (!released) await transaction.RollbackAsync(); }
        Assert.Equal("Coordination gap", t.F.Db(db => db.Issues.Single(row => row.Id == issue).RootCauseLabelSnapshot));
        Assert.False(t.F.Db(db => db.IssueRootCauseCategories.Single(row => row.Id == cause).IsActive));
        async Task WaitFor(string query)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await using var observer = new NpgsqlConnection(connection.ConnectionString); await observer.OpenAsync(timeout.Token);
            while (true)
            {
                await using var command = new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock' AND replace(query,chr(34),'') ILIKE @query", observer);
                command.Parameters.AddWithValue("query", "%" + query + "%");
                if (Convert.ToInt64(await command.ExecuteScalarAsync(timeout.Token)) > 0) return;
                await Task.Delay(20, timeout.Token);
            }
        }
    }
}

internal sealed class IssueRootCauseFixture : IDisposable
{
    internal HubFactory F { get; } = new(); internal TestData Data { get; }
    internal IssueRootCauseFixture() { Data = new(F); F.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => F.Dispose();
    internal async Task Flag(bool on) => (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).EnsureSuccessStatusCode();
    internal async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await F.As(who).Post(path, body)).Json(status);
    internal int Version(Guid issue) => F.Db(db => db.Issues.Single(row => row.Id == issue).RowVersion);
    internal async Task<Guid> Cause(string name = "Coordination gap") => (await Post(TestData.Admin, "/api/v1/admin/issueRootCauses", new { name }, 201)).G("id");
    internal async Task<JsonNode> EditCause(Guid id, object body) => await (await F.As(TestData.Admin).Patch($"/api/v1/admin/issueRootCauses/{id}", body,
        F.Db(db => db.IssueRootCauseCategories.Single(row => row.Id == id).RowVersion))).Json();
    internal async Task<Guid> Issue(Guid project, string title = "Synthetic issue", string owner = TestData.Alex) => (await Post(TestData.Alex, $"/api/v1/projects/{project}/issues",
        new { title, severity = "Medium", ownerId = Data.User(owner), projectDisciplineId = Data.ProjectDiscipline(project, "Civil") }, 201)).G("id");
    internal async Task<JsonNode> Move(Guid id, Guid? cause = null, string to = "Resolved", string who = TestData.Alex, string resolution = "The clash was resolved", string? reason = null, string? date = null, int? version = null, int status = 200) =>
        await Post(who, $"/api/v1/issues/{id}/transition", new { toStatus = to, resolution, reason, resolvedDate = date, rowVersion = version ?? Version(id), rootCauseCategoryId = cause }, status);
    internal async Task<JsonNode> Detail(Guid id) => await (await F.As(TestData.Pm).GetAsync($"/api/v1/issues/{id}")).Json();
    internal async Task<JsonNode> Summary(Guid project, string who = TestData.Pm) => await (await F.As(who).GetAsync($"/api/v1/projects/{project}/issues/root-causes")).Json();
    internal string State(Guid project, bool excludeExports = false) => F.Db(db => JsonSerializer.Serialize(new {
        issues = db.Issues.IgnoreQueryFilters().Where(row => row.ProjectId == project).OrderBy(row => row.Id).Select(row => new { row.Id, row.Status, row.Resolution, row.ResolvedDate, row.RootCauseCategoryId, row.RootCauseLabelSnapshot, row.RowVersion, row.LastActivityAt }).ToArray(),
        logs = db.ActivityLog.Count(row => row.ProjectId == project && (!excludeExports || row.Action != "Exported")), receipts = db.Idempotency.Count(), commands = db.CoordinationCommands.Count(), sources = db.SourceRevisions.Count(),
        heads = db.SourceHeads.Count(), findings = db.ReviewFindings.Count(), seen = db.WorkSeenAcknowledgements.Count(), seenReceipts = db.WorkSeenCommandReceipts.Count(), occurrences = db.WorkAssignmentOccurrences.Count()
    }, JsonOpts.Web));
}
