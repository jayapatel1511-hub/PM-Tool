using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

/// FR-SCR-61 / AC-SCR-61, AC-SCR-84: outcome metadata uses the canonical finding workflow.
public sealed class ReviewResponseTests
{
    [Theory] // AC-SCR-61: only the four specified outcomes are valid response metadata.
    [InlineData("Incorporated")]
    [InlineData("Not incorporated")]
    [InlineData("Clarified")]
    [InlineData("Noted")]
    public void Exact_outcomes_are_valid_response_metadata(string outcome)
        => Assert.Equal(outcome, ReviewResponses.Validate(true, FindingStatus.Responded, outcome));

    [Theory] // AC-SCR-61: require selection without inferring or normalizing an outcome.
    [InlineData(null)]
    [InlineData("")]
    [InlineData("incorporated")]
    [InlineData(" Incorporated")]
    [InlineData("Not Incorporated")]
    [InlineData("Noted ")]
    public void Flag_on_fresh_response_requires_an_explicit_exact_outcome(string? outcome)
    {
        var error = Assert.Throws<ApiException>(() => ReviewResponses.Validate(true, FindingStatus.Responded, outcome));
        Assert.Equal(400, error.Status); Assert.Contains("responseOutcome", error.Errors!.Keys);
    }

    [Fact] // AC-SCR-84: the helper never validates or normalizes an old body while off.
    public void Flag_and_non_response_rules_do_not_create_another_transition_engine()
    {
        foreach (var action in new[] { FindingStatus.Responded, FindingStatus.Open, FindingStatus.VerifiedClosed, FindingStatus.Withdrawn, "assignResolver", "assignVerifier", "acknowledgeWithdrawal" })
        {
            Assert.Null(ReviewResponses.Validate(false, action, null));
            Assert.Equal(404, Assert.Throws<ApiException>(() => ReviewResponses.Validate(false, action, "Noted")).Status);
            if (action == FindingStatus.Responded) continue;
            Assert.Null(ReviewResponses.Validate(true, action, null));
            Assert.Equal(400, Assert.Throws<ApiException>(() => ReviewResponses.Validate(true, action, "Noted")).Status);
        }
    }

    [Theory] // AC-SCR-61: all four outcomes annotate a response, never an approval or closure.
    [InlineData("Incorporated")]
    [InlineData("Not incorporated")]
    [InlineData("Clarified")]
    [InlineData("Noted")]
    public async Task Response_updates_current_and_one_immutable_event_without_bypassing_independent_closure(string outcome)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New();
        var finding = await t.Finding(s, "Check the synthetic service clearance"); var version = t.Version<ReviewFinding>(finding);
        const string raw = "  The design response explains the recorded disposition.  ";
        var result = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, outcome, raw);
        Assert.Equal(new[] { "id", "rowVersion" }, result.AsObject().Select(property => property.Key));
        Assert.Equal(finding, result.G("id")); Assert.True(result.I("rowVersion") > version);
        var current = t.F.Db(db => db.ReviewFindings.AsNoTracking().Single(row => row.Id == finding));
        Assert.Equal(FindingStatus.Responded, current.Status); Assert.Equal(raw.Trim(), current.Response);
        Assert.Equal(outcome, current.ResponseOutcome); Assert.Equal(ReviewResponseFixture.Evidence, current.EvidenceUrl);
        var response = t.F.Db(db => db.FindingEvents.AsNoTracking().Single(row => row.FindingId == finding));
        Assert.Equal(FindingStatus.Responded, response.Action); Assert.Equal(raw.Trim(), response.Reason); Assert.Equal(outcome, response.ResponseOutcome);
        Assert.Equal(ReviewStatus.ChangesRequired, t.F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).Status));
        Assert.Equal(s.Package, t.F.Db(db => db.Deliverables.Single(row => row.Id == s.Deliverable).RequiredReviewPackageId));
        var detail = await t.Detail(s); Assert.Equal(outcome, t.Find(detail, finding).S("responseOutcome"));
        Assert.Equal(outcome, Assert.Single(detail["events"]!.AsArray())!.S("responseOutcome"));
        var register = await (await t.F.As(TestData.Rita).GetAsync(t.Root(s) + "/review-findings")).Json();
        Assert.Equal(outcome, Assert.Single(register["items"]!.AsArray())!.S("responseOutcome"));
        var before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.VerifiedClosed, TestData.Alex, status: 403);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Action(s, finding, FindingStatus.VerifiedClosed, TestData.Omar);
        Assert.Equal(FindingStatus.VerifiedClosed, t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).Status));
        Assert.Equal(outcome, t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        Assert.Null(t.F.Db(db => db.FindingEvents.Single(row => row.FindingId == finding && row.Action == FindingStatus.VerifiedClosed).ResponseOutcome));
    }

    [Theory] // AC-SCR-61: the one existing Reason is also the Not incorporated explanation.
    [InlineData(null, "The reason exists but no outcome was selected")]
    [InlineData("Unknown", "An invalid outcome cannot be recorded")]
    [InlineData("Not incorporated", "")]
    [InlineData("Not incorporated", "    ")]
    [InlineData("Not incorporated", "abcd")]
    [InlineData("Clarified", "no")]
    public async Task Invalid_outcome_or_existing_reason_refuses_without_any_business_or_receipt_write(string? outcome, string reason)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        var before = t.State(s.Project.Id);
        var problem = await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, outcome, reason, status: 400);
        Assert.Equal("validation", problem.S("code")); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Theory] // AC-SCR-61: outcome metadata does not weaken the existing required HTTP/HTTPS response evidence.
    [InlineData(null)]
    [InlineData("")]
    [InlineData("javascript:alert(1)")]
    public async Task Missing_or_invalid_response_evidence_is_refused_without_writes(string? evidence)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        var before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Incorporated", evidence: evidence, status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Incorporated");
        await t.Action(s, finding, FindingStatus.VerifiedClosed, TestData.Omar);
    }

    [Fact] // AC-SCR-61 / AC-SCR-84: retain historical responses, replace current metadata, clear it with resolver reassignment even off.
    public async Task Return_re_response_verifier_reassignment_and_resolver_reset_preserve_event_history()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Incorporated", "First retained response");
        var original = t.F.Db(db => db.FindingEvents.Single(row => row.FindingId == finding).Id);
        await t.Action(s, finding, FindingStatus.Open, TestData.Omar);
        Assert.Equal("Incorporated", t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Clarified", "Second retained response");
        await t.Action(s, finding, "assignVerifier", TestData.Pm, owner: t.Data.User(TestData.Pm));
        Assert.Equal("Clarified", t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        Assert.Equal("Second retained response", t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).Response));
        await t.Flag(false);
        await t.Action(s, finding, "assignResolver", TestData.Marc, owner: t.Data.User(TestData.Marc));
        var reset = t.F.Db(db => db.ReviewFindings.AsNoTracking().Single(row => row.Id == finding));
        Assert.Equal(FindingStatus.Open, reset.Status); Assert.Null(reset.Response); Assert.Null(reset.EvidenceUrl); Assert.Null(reset.ResponseOutcome);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Marc, reason: "Legacy response after feature was switched off");
        Assert.Null(t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        var events = t.F.Db(db => db.FindingEvents.AsNoTracking().Where(row => row.FindingId == finding).ToArray());
        Assert.Equal("Incorporated", events.Single(row => row.Id == original).ResponseOutcome);
        Assert.Equal("First retained response", events.Single(row => row.Id == original).Reason);
        Assert.Equal(2, events.Count(row => row.ResponseOutcome is not null));
        Assert.All(events.Where(row => row.Action != FindingStatus.Responded), row => Assert.Null(row.ResponseOutcome));
        var hidden = await t.Detail(s); Assert.All(hidden["events"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("responseOutcome")));
        await t.Flag(true); var visible = await t.Detail(s);
        Assert.False(t.Find(visible, finding).AsObject().ContainsKey("responseOutcome"));
        Assert.Equal(2, visible["events"]!.AsArray().Count(row => row!.AsObject().ContainsKey("responseOutcome")));
    }

    [Theory] // AC-SCR-61: no previous response/outcome is approval for a new round or changed source.
    [InlineData(false)]
    [InlineData(true)]
    public async Task Both_carry_paths_reset_the_new_finding_and_retain_the_original_response(bool publishedReplacement)
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Not incorporated", "The existing corridor cannot accommodate this change");
        Guid source = s.Source;
        if (publishedReplacement) source = await t.PublishReplacement(s);
        else await t.NewRound(s);
        var previous = t.F.Db(db => db.ReviewFindings.AsNoTracking().Single(row => row.Id == finding));
        var carried = t.F.Db(db => db.ReviewFindings.AsNoTracking().Single(row => row.PackageId == s.Package && row.CarriedFromId == finding));
        Assert.Equal("Not incorporated", previous.ResponseOutcome); Assert.NotNull(previous.Response); Assert.NotNull(previous.EvidenceUrl);
        Assert.Equal(FindingStatus.Open, carried.Status); Assert.Null(carried.Response); Assert.Null(carried.EvidenceUrl); Assert.Null(carried.ResponseOutcome);
        Assert.NotEqual(s.Round, carried.RoundId); Assert.Equal(source, carried.SourceRevisionId); Assert.Equal(previous.Text, carried.Text);
        Assert.Equal(carried.RoundId, t.F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).CurrentRoundId));
        Assert.Equal(ReviewStatus.Draft, t.F.Db(db => db.ReviewPackages.Single(row => row.Id == s.Package).Status));
        Assert.Equal("Not incorporated", t.F.Db(db => db.FindingEvents.Single(row => row.FindingId == finding).ResponseOutcome));
        Assert.False(t.F.Db(db => db.FindingEvents.Any(row => row.FindingId == carried.Id)));
    }

    [Fact] // AC-SCR-61: outcome metadata never authorizes a different actor, stale command or another action.
    public async Task Named_actor_stale_version_and_non_response_outcome_refusals_are_atomic()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        var before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Pm, "Noted", status: 403);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", version: t.Version<ReviewFinding>(finding) - 1, status: 409);
        await t.Action(s, finding, FindingStatus.Withdrawn, TestData.Omar, "Noted", status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted");
        before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.VerifiedClosed, TestData.Rita, status: 403);
        await t.Action(s, finding, "assignVerifier", TestData.Pm, owner: t.Data.User(TestData.Alex), status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-61: Complete correction reason remains mandatory and an outcome does not reopen the project.
    public async Task Complete_project_response_still_requires_a_reason_and_retains_the_project_status()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(true); var s = await t.New(); var finding = await t.Finding(s);
        await t.Action(s, finding, "assignResolver", TestData.Marc, owner: t.Data.User(TestData.Pm));
        // A preserved completed-project fixture; no completion workflow or acceptance is simulated by this test.
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == s.Project.Id)).Status = ProjectStatus.Complete; return await db.SaveChangesAsync(); });
        var before = t.State(s.Project.Id);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Pm, "Noted", reason: "", status: 400);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex, "Noted", status: 403);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Action(s, finding, FindingStatus.Responded, TestData.Pm, "Noted", reason: "Correct the response record after project completion");
        Assert.Equal(ProjectStatus.Complete, t.F.Db(db => db.Projects.Single(row => row.Id == s.Project.Id).Status));
        Assert.Equal(FindingStatus.Responded, t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).Status));
    }

    [Fact] // AC-SCR-61 / AC-SCR-84: real provider checks, including nullable legacy compatibility, not just EF metadata.
    public async Task Relational_constraints_enforce_length_enum_response_and_event_action_without_changing_legacy_rows()
    {
        using var t = new ReviewResponseFixture(); await t.Flag(false); var s = await t.New(); var finding = await t.Finding(s);
        await t.Action(s, finding, FindingStatus.Responded, TestData.Alex);
        var responseEvent = t.F.Db(db => db.FindingEvents.Single(row => row.FindingId == finding).Id);
        Assert.Null(t.F.Db(db => db.ReviewFindings.Single(row => row.Id == finding).ResponseOutcome));
        Assert.Null(t.F.Db(db => db.FindingEvents.Single(row => row.Id == responseEvent).ResponseOutcome));
        var before = t.State(s.Project.Id);

        async Task Reject(string sql, Guid id, string outcome, string code, string? constraint = null)
        {
            await t.F.DbAsync(async db => {
                await using var transaction = await db.Database.BeginTransactionAsync();
                // Raw SQL is deliberate: it proves the migrated database refuses data even without application validation.
                var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, outcome, id));
                Assert.Equal(code, error.SqlState);
                if (constraint is not null) Assert.Equal(constraint, error.ConstraintName);
                await transaction.RollbackAsync(); return 0;
            });
        }
        const string findingUpdate = "UPDATE hub.review_finding SET response_outcome = {0} WHERE id = {1}";
        const string eventUpdate = "UPDATE hub.finding_event SET response_outcome = {0} WHERE id = {1}";
        await Reject(findingUpdate, finding, new string('x', 33), PostgresErrorCodes.StringDataRightTruncation);
        await Reject(eventUpdate, responseEvent, new string('x', 33), PostgresErrorCodes.StringDataRightTruncation);
        await Reject(findingUpdate, finding, "Unknown", PostgresErrorCodes.CheckViolation, "ck_review_finding_response_outcome");
        await Reject(eventUpdate, responseEvent, "Unknown", PostgresErrorCodes.CheckViolation, "ck_finding_event_response_outcome");
        await Reject("UPDATE hub.review_finding SET response_outcome = {0}, response = NULL WHERE id = {1}",
            finding, "Noted", PostgresErrorCodes.CheckViolation, "ck_review_finding_response_outcome");
        await Reject("UPDATE hub.finding_event SET response_outcome = {0}, action = 'Open' WHERE id = {1}",
            responseEvent, "Noted", PostgresErrorCodes.CheckViolation, "ck_finding_event_response_outcome");

        await t.F.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            foreach (var outcome in new[] { "Incorporated", "Not incorporated", "Clarified", "Noted" })
            {
                // A retained response can have an outcome after a return to Open; outcome is not a workflow status.
                Assert.Equal(1, await db.Database.ExecuteSqlRawAsync("UPDATE hub.review_finding SET response_outcome = {0}, status = 'Open' WHERE id = {1}", outcome, finding));
                Assert.Equal(1, await db.Database.ExecuteSqlRawAsync(eventUpdate, outcome, responseEvent));
            }
            Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hub.review_finding SET response_outcome = NULL, response = NULL WHERE id = {finding}"));
            Assert.Equal(1, await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hub.finding_event SET response_outcome = NULL, action = 'Open' WHERE id = {responseEvent}"));
            await transaction.RollbackAsync(); return 0;
        });
        Assert.Equal(before, t.State(s.Project.Id));
    }
}

/// Shared private-database workflow builder for these three new files. No preserved QA runtime is used.
internal sealed class ReviewResponseFixture : IDisposable
{
    internal const string Evidence = "https://example.test/response-evidence.pdf";
    internal HubFactory F { get; } = new();
    internal TestData Data { get; }
    internal sealed record Setup(Project Project, Guid Deliverable, Guid Source, Guid Package, Guid Round, Guid Civil, Guid Electrical);
    internal ReviewResponseFixture() { Data = new(F); F.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => F.Dispose();
    internal string Root(Setup s) => $"/api/v1/projects/{s.Project.Id}";
    internal string ActionPath(Setup s, Guid finding) => Root(s) + $"/reviews/{s.Package}/findings/{finding}/action";
    internal int Version<T>(Guid id) where T : Audited => F.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
    internal async Task Flag(bool enabled) => (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = enabled })).EnsureSuccessStatusCode();
    internal async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await F.As(who).Post(path, body)).Json(status);
    internal async Task<JsonNode> Detail(Setup s, string who = TestData.Pm) => await (await F.As(who).GetAsync(Root(s) + $"/reviews/{s.Package}")).Json();
    internal JsonNode Find(JsonNode detail, Guid finding) => detail["findings"]!.AsArray().Single(row => row!.G("id") == finding)!;
    internal ReviewEndpoints.Assignment[] Assignments(Setup s) => [
        new(s.Civil, Data.User(TestData.Pm), new(2026, 11, 12)), new(s.Electrical, Data.User(TestData.Omar), new(2026, 11, 12))
    ];

    internal async Task<Setup> New(Project? project = null, string title = "Synthetic response package")
    {
        project ??= await Data.Project(); var civil = Data.ProjectDiscipline(project.Id, "Civil"); var electrical = Data.ProjectDiscipline(project.Id, "Electrical");
        var root = $"/api/v1/projects/{project.Id}";
        var deliverable = (await Post(TestData.Marc, root + "/deliverables", new { name = title + " drawing", projectDisciplineId = civil,
            deliverableTypeId = await Data.DeliverableType(), ownerId = Data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        var source = (await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, Data.User(TestData.Alex), "Deliverable", "response-sheet", title + " drawing", "A",
            "https://example.test/source-A.pdf", "Synthetic author", "Synthetic scope", null, null, null, null, null, null))).G("id");
        var provisional = new Setup(project, deliverable, source, Guid.Empty, Guid.Empty, civil, electrical);
        var package = (await Post(TestData.Marc, root + "/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), title, "Check synthetic engineering scope",
            civil, Data.User(TestData.Marc), [source], Assignments(provisional), true, null))).G("id");
        await Post(TestData.Marc, root + $"/reviews/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start the genuine synthetic round"));
        var round = F.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value);
        return provisional with { Package = package, Round = round };
    }

    internal async Task<Guid> Finding(Setup s, string text = "Check the recorded service clearance", string severity = "Blocking", string discipline = "Electrical")
    {
        F.Clock.Now = F.Clock.Now.AddMinutes(1);
        return (await Post(discipline == "Civil" ? TestData.Pm : TestData.Omar, Root(s) + $"/reviews/{s.Package}/findings",
            new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(s.Package), s.Source,
                discipline == "Civil" ? s.Civil : s.Electrical, Data.User(TestData.Alex), text, severity))).G("id");
    }
    internal Task<JsonNode> Action(Setup s, Guid finding, string action, string who, string? outcome = null,
        string reason = "Recorded synthetic review action", string? evidence = Evidence, Guid? owner = null, int? version = null, int status = 200, Guid? request = null)
        => Post(who, ActionPath(s, finding), new ReviewEndpoints.FindingAction(request ?? Guid.NewGuid(), version ?? Version<ReviewFinding>(finding),
            action, reason, evidence, owner, outcome), status);
    internal async Task NewRound(Setup s) => await Post(TestData.Marc, Root(s) + $"/reviews/{s.Package}/rounds",
        new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(s.Package), "Fresh synthetic review scope", [s.Source], Assignments(s), "The changed scope needs a fresh round", null));
    internal async Task<Guid> PublishReplacement(Setup s)
    {
        var head = F.Db(db => db.SourceHeads.AsNoTracking().Single(row => row.ProjectId == s.Project.Id && row.CurrentRevisionId == s.Source));
        var change = await Post(TestData.Alex, Root(s) + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), s.Deliverable,
            Version<Deliverable>(s.Deliverable), s.Civil, Data.User(TestData.Alex), "Deliverable", "response-sheet", "Replacement synthetic source", "B",
            "https://example.test/source-B.pdf", "Synthetic author", "Changed synthetic scope", null, s.Source, head.RowVersion,
            "A genuine replacement changes the reviewed content", new(2026, 11, 2), new(2026, 11, 12)));
        var notice = change.G("id"); var source = F.Db(db => db.ChangeNotices.Single(row => row.Id == notice).NewRevisionId);
        await Post(TestData.Alex, Root(s) + $"/changes/{notice}/publish", new ChangeEndpoints.PublishBody(Guid.NewGuid(), Version<ChangeNotice>(notice), head.RowVersion, null));
        return source;
    }
    internal string State(Guid project) => F.Db(db => JsonSerializer.Serialize(new {
        findings = db.ReviewFindings.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        events = db.FindingEvents.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        packages = db.ReviewPackages.IgnoreQueryFilters().AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        rounds = db.ReviewRounds.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        commands = db.CoordinationCommands.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        logs = db.ActivityLog.Count(row => row.ProjectId == project && row.Action != "Exported"),
        nextReview = db.Projects.Single(row => row.Id == project).NextReviewSeq,
        receipts = db.Idempotency.Count(), occurrences = db.WorkAssignmentOccurrences.Count(), seenCommands = db.WorkSeenCommandReceipts.Count(),
        acknowledgements = db.WorkSeenAcknowledgements.Count(), visitRevisions = db.UserSettings.Sum(row => row.MyWorkSeenRevision)
    }, JsonOpts.Web));
}
