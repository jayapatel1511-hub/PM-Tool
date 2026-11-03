using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Request/read-scope coverage in private synthetic databases. These tests do not operate the preserved QA24 runtime.
/// S35 R1: AC-SCR-61 cross-package filters; AC-SCR-84 flag combinations and unchanged off reads.
public sealed class ReviewRegisterTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData data;
    public ReviewRegisterTests() => data = new(f);
    public void Dispose() => f.Dispose();
    sealed record ReviewRegisterSetup(Project Project, Guid Deliverable, Guid Source, Guid Package, Guid Round);
    sealed record ReviewRegisterState(int Findings, int Packages, int PackageVersions, int NextReview, int Logs,
        int Commands, int Receipts, int Occurrences, int SeenCommands, int Acknowledgements, int VisitRevisions);
    string Root(Guid project) => $"/api/v1/projects/{project}";
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
    async Task Flag(string name, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Get(Guid project, string query = "", string who = TestData.Pm, int status = 200)
        => await (await f.As(who).GetAsync(Root(project) + "/review-findings" + query)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    static Guid[] Ids(JsonNode page) => page["items"]!.AsArray().Select(row => row!.G("id")).ToArray();
    static JsonNode Row(JsonNode page, Guid id) => page["items"]!.AsArray().Single(row => row!.G("id") == id)!;

    async Task<ReviewRegisterSetup> Package(Project? project = null, string title = "Review register package")
    {
        project ??= await data.Project(); var civil = data.ProjectDiscipline(project.Id, "Civil");
        var electrical = data.ProjectDiscipline(project.Id, "Electrical");
        var deliverable = (await Post(TestData.Marc, Root(project.Id) + "/deliverables", new { name = title + " source", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
        var source = (await Post(TestData.Alex, Root(project.Id) + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, data.User(TestData.Alex), "Deliverable", "register", title + " source", "A", "https://example.test/review.pdf",
            "Synthetic author", "Synthetic scope", null, null, null, null, null, null))).G("id");
        var package = (await Post(TestData.Marc, Root(project.Id) + "/reviews", new ReviewEndpoints.CreateBody(Guid.NewGuid(), title,
            "Check the scoped drawing", civil, data.User(TestData.Marc), [source], Assignments(project.Id), false, null))).G("id");
        await Post(TestData.Marc, Root(project.Id) + $"/reviews/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start the genuine review round"));
        var round = f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value);
        return new(project, deliverable, source, package, round);
    }
    ReviewEndpoints.Assignment[] Assignments(Guid project) => [
        new(data.ProjectDiscipline(project, "Civil"), data.User(TestData.Pm), new DateOnly(2026, 11, 12)),
        new(data.ProjectDiscipline(project, "Electrical"), data.User(TestData.Omar), new DateOnly(2026, 11, 12))
    ];
    async Task<Guid> Finding(ReviewRegisterSetup s, string text, string severity = "Blocking", string discipline = "Electrical", string resolver = TestData.Alex)
    {
        f.Clock.Now = f.Clock.Now.AddMinutes(1); // Stable source CreatedAt ordering, independent of UUID randomness/timing.
        var who = discipline == "Civil" ? TestData.Pm : TestData.Omar;
        return (await Post(who, Root(s.Project.Id) + $"/reviews/{s.Package}/findings", new ReviewEndpoints.FindingBody(Guid.NewGuid(), Version<ReviewPackage>(s.Package),
            s.Source, data.ProjectDiscipline(s.Project.Id, discipline), data.User(resolver), text, severity))).G("id");
    }
    async Task Action(ReviewRegisterSetup s, Guid finding, string action, string who, string? evidence = null, string? responseOutcome = null)
        => await Post(who, Root(s.Project.Id) + $"/reviews/{s.Package}/findings/{finding}/action", new ReviewEndpoints.FindingAction(Guid.NewGuid(), Version<ReviewFinding>(finding), action,
            "Recorded synthetic review action", evidence, null, responseOutcome));
    ReviewRegisterState State(Guid project) => f.Db(db => new ReviewRegisterState(
        db.ReviewFindings.Count(row => row.ProjectId == project), db.ReviewPackages.IgnoreQueryFilters().Count(row => row.ProjectId == project),
        db.ReviewPackages.IgnoreQueryFilters().Where(row => row.ProjectId == project).Sum(row => row.RowVersion), db.Projects.Single(row => row.Id == project).NextReviewSeq,
        db.ActivityLog.Count(row => row.ProjectId == project), db.CoordinationCommands.Count(row => row.ProjectId == project), db.Idempotency.Count(),
        db.WorkAssignmentOccurrences.Count(), db.WorkSeenCommandReceipts.Count(), db.WorkSeenAcknowledgements.Count(), db.UserSettings.Sum(row => row.MyWorkSeenRevision)));

    [Fact]
    public async Task Two_packages_full_paging_and_four_filters_use_the_whole_authorized_scope()
    {
        await Flag("register_screens", true); var a = await Package(title: "Package A"); var b = await Package(a.Project, "Package B");
        var civil = data.ProjectDiscipline(a.Project.Id, "Civil"); var electrical = data.ProjectDiscipline(a.Project.Id, "Electrical");
        var a1 = await Finding(a, "A1 civil blocking", discipline: "Civil");
        var a2 = await Finding(a, "A2 electrical advisory", "Advisory", resolver: TestData.Marc);
        var a3 = await Finding(a, "A3 electrical blocking");
        var b1 = await Finding(b, "B1 civil advisory", "Advisory", "Civil", TestData.Marc);
        var b2 = await Finding(b, "B2 civil blocking", discipline: "Civil");
        var b3 = await Finding(b, "B3 electrical advisory", "Advisory");
        var before = State(a.Project.Id); var all = await Get(a.Project.Id, "?pageSize=200");
        Assert.Equal(6, all.I("totalCount")); Assert.Equal(new[] { a1, a2, a3, b1, b2, b3 }, Ids(all));
        var collected = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var part = await Get(a.Project.Id, $"?page={page}&pageSize=2"); collected.AddRange(Ids(part));
            Assert.Equal(6, part.I("totalCount")); Assert.Equal(page, part.I("page")); Assert.Equal(2, part.I("pageSize"));
            Assert.Equal(new[] { civil, electrical }.Order(), part["options"]!["disciplines"]!.AsArray().Select(row => row!.G("id")).Order());
            Assert.Equal(2, part["options"]!["originators"]!.AsArray().Count); Assert.Equal(2, part["options"]!["resolutionOwners"]!.AsArray().Count);
        }
        Assert.Equal(Ids(all), collected); Assert.Equal(6, collected.Distinct().Count());
        var cases = new[] {
            (Query: $"?disciplineId={civil}", Expected: new[] { a1, b1, b2 }),
            (Query: "?severity=Blocking", Expected: new[] { a1, a3, b2 }),
            (Query: $"?originatorKind=User&originatorId={data.User(TestData.Omar)}", Expected: new[] { a2, a3, b3 }),
            (Query: $"?resolverId={data.User(TestData.Marc)}", Expected: new[] { a2, b1 }),
            (Query: $"?disciplineId={civil}&severity=Blocking&originatorKind=User&originatorId={data.User(TestData.Pm)}&resolverId={data.User(TestData.Alex)}", Expected: new[] { a1, b2 })
        };
        foreach (var test in cases)
        {
            var filtered = await Get(a.Project.Id, test.Query); Assert.Equal(test.Expected, Ids(filtered)); Assert.Equal(test.Expected.Length, filtered.I("totalCount"));
            Assert.Equal(2, filtered["options"]!["originators"]!.AsArray().Count); // Not just selected-filter/current-page options.
        }
        var empty = await Get(a.Project.Id, "?severity=Blocking&originatorKind=ExternalParty"); Assert.Empty(Ids(empty)); Assert.Equal(0, empty.I("totalCount"));
        Assert.All(empty["options"]!["originators"]!.AsArray(), originator => { Assert.Equal("User", originator!.S("kind")); Assert.Null(originator["externalPartyId"]); });
        var outside = await Get(a.Project.Id, $"?disciplineId={Guid.NewGuid()}"); Assert.Empty(Ids(outside)); Assert.Equal(0, outside.I("totalCount"));
        Assert.Empty(Ids(await Get(a.Project.Id, "?page=2147483647&pageSize=200")));
        Assert.Equal(before, State(a.Project.Id));
    }

    [Fact]
    public async Task Current_scope_does_not_double_count_carried_findings_and_explicit_history_retains_exact_identity()
    {
        await Flag("register_screens", true); var s = await Package(); var old = await Finding(s, "Carry the real unresolved comment");
        await Post(TestData.Marc, Root(s.Project.Id) + $"/reviews/{s.Package}/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(s.Package),
            "Review changed scope", [s.Source], Assignments(s.Project.Id), "New scope requires a new review round", null));
        var carried = f.Db(db => db.ReviewFindings.Single(row => row.PackageId == s.Package && row.CarriedFromId == old).Id);
        var current = await Get(s.Project.Id, who: TestData.Alex); Assert.Equal(new[] { carried }, Ids(current)); Assert.Equal(1, current.I("totalCount"));
        Assert.True(Row(current, carried)["isCurrentRound"]!.GetValue<bool>()); Assert.Equal(old, Row(current, carried).G("carriedFromId"));
        Assert.True(Row(current, carried)["actionability"]!["canAct"]!.GetValue<bool>());
        var history = await Get(s.Project.Id, "?allRounds=true", TestData.Alex); Assert.Equal(new[] { old, carried }, Ids(history));
        var earlier = Row(history, old); Assert.Equal(s.Round, earlier.G("roundId")); Assert.Equal(1, earlier.I("roundNumber"));
        Assert.False(earlier["isCurrentRound"]!.GetValue<bool>()); Assert.False(earlier["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.Equal("historical_round", earlier["actionability"]!.S("reason"));
    }

    [Fact]
    public async Task Blocking_withdrawal_pending_ack_is_distinct_and_only_the_named_coordinator_has_its_primary_action()
    {
        await Flag("register_screens", true); var s = await Package(); var blocking = await Finding(s, "Withdrawal needs acknowledgement");
        var advisory = await Finding(s, "Advisory withdrawal is complete", "Advisory");
        await Action(s, blocking, FindingStatus.Withdrawn, TestData.Omar); await Action(s, advisory, FindingStatus.Withdrawn, TestData.Omar);
        var coordinator = await Get(s.Project.Id, who: TestData.Marc); Assert.Equal(new[] { blocking }, Ids(coordinator));
        var row = Row(coordinator, blocking); Assert.Equal(FindingStatus.Withdrawn, row.S("status")); Assert.True(row["pendingWithdrawalAcknowledgement"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.AcknowledgeWithdrawal, row["actionability"]!.S("nextAction")); Assert.True(row["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.Equal(data.User(TestData.Marc), row["actionability"]!["nextActor"]!.G("id"));
        var pm = await Get(s.Project.Id); Assert.False(Row(pm, blocking)["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.Equal("not_your_action", Row(pm, blocking)["actionability"]!.S("reason"));
        await Action(s, blocking, "acknowledgeWithdrawal", TestData.Marc); Assert.Empty(Ids(await Get(s.Project.Id)));
    }

    [Fact]
    public async Task Responded_finding_names_the_actual_verifier_and_closed_findings_leave_the_unresolved_scope()
    {
        await Flag("register_screens", true); var s = await Package(); var finding = await Finding(s, "Independent closure stays separate");
        await Action(s, finding, FindingStatus.Responded, TestData.Alex, "https://example.test/response-evidence.pdf", "Clarified");
        var verifier = Row(await Get(s.Project.Id, who: TestData.Omar), finding);
        Assert.Equal(FindingStatus.Responded, verifier.S("status")); Assert.Equal(data.User(TestData.Alex), verifier["resolutionOwner"]!.G("id"));
        Assert.Equal(data.User(TestData.Omar), verifier["verifier"]!.G("id")); Assert.Equal(data.User(TestData.Omar), verifier["actionability"]!["nextActor"]!.G("id"));
        Assert.Equal(RecordContextAction.VerifyClosure, verifier["actionability"]!.S("nextAction")); Assert.True(verifier["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.False(Row(await Get(s.Project.Id, who: TestData.Alex), finding)["actionability"]!["canAct"]!.GetValue<bool>());
        await Action(s, finding, FindingStatus.VerifiedClosed, TestData.Omar);
        Assert.Empty(Ids(await Get(s.Project.Id))); Assert.Empty(Ids(await Get(s.Project.Id, "?allRounds=true")));
    }

    [Theory]
    [InlineData(ReviewStatus.Cancelled)]
    [InlineData(ReviewStatus.Superseded)]
    public async Task Terminal_packages_remain_labelled_readonly_and_approved_advisory_comments_are_not_hidden(string terminal)
    {
        await Flag("register_screens", true); var a = await Package(); var advisory = await Finding(a, "Approved package advisory remains open", "Advisory");
        foreach (var assignment in f.Db(db => db.DisciplineReviews.Where(row => row.RoundId == a.Round).ToArray()))
        {
            var who = assignment.ProjectDisciplineId == data.ProjectDiscipline(a.Project.Id, "Civil") ? TestData.Pm : TestData.Omar;
            await Post(who, Root(a.Project.Id) + $"/reviews/{a.Package}/assignments/{assignment.Id}/decision",
                new ReviewEndpoints.DecisionBody(Guid.NewGuid(), Version<DisciplineReview>(assignment.Id), DisciplineReviewStatus.Approved, "Review complete with advisory note"));
        }
        var b = await Package(a.Project); var frozen = await Finding(b, "Terminal unresolved advisory", "Advisory");
        if (terminal == ReviewStatus.Cancelled)
            await Post(TestData.Marc, Root(b.Project.Id) + $"/reviews/{b.Package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(b.Package), "cancel", "Cancel the genuine review package"));
        else
            await f.DbAsync(async db => { (await db.ReviewPackages.SingleAsync(row => row.Id == b.Package)).Status = ReviewStatus.Superseded; return await db.SaveChangesAsync(); }); // Preserved terminal legacy-state fixture.
        var rows = await Get(a.Project.Id, who: TestData.Alex); Assert.Equal(2, rows.I("totalCount"));
        Assert.Equal(ReviewStatus.Approved, Row(rows, advisory).S("packageStatus")); Assert.Equal(FindingStatus.Open, Row(rows, advisory).S("status"));
        Assert.True(Row(rows, advisory)["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.Equal(terminal, Row(rows, frozen).S("packageStatus")); Assert.False(Row(rows, frozen)["actionability"]!["isPackageLive"]!.GetValue<bool>());
        Assert.False(Row(rows, frozen)["actionability"]!["canAct"]!.GetValue<bool>()); Assert.Equal("package_terminal", Row(rows, frozen)["actionability"]!.S("reason"));
    }

    [Fact]
    public async Task Actual_people_are_retained_with_inactive_or_unknown_labels_and_no_private_facts_or_person_substitution()
    {
        await Flag("register_screens", true); var s = await Package(); var finding = await Finding(s, "Retain actual named identities");
        var alex = data.User(TestData.Alex); var omar = data.User(TestData.Omar);
        var name = f.Db(db => db.Users.Single(row => row.Id == alex).DisplayName);
        await f.DbAsync(async db => {
            (await db.Users.SingleAsync(row => row.Id == alex)).IsActive = false;
            (await db.Users.SingleAsync(row => row.Id == omar)).DisplayName = ""; // Legacy missing-name fixture, not a fabricated account.
            return await db.SaveChangesAsync();
        });
        var result = await Get(s.Project.Id); var row = Row(result, finding);
        Assert.Equal(alex, row["resolutionOwner"]!.G("id")); Assert.Equal(name, row["resolutionOwner"]!.S("name")); Assert.Equal("Inactive", row["resolutionOwner"]!.S("state"));
        Assert.Equal("User", row["originator"]!.S("kind")); Assert.Equal(omar, row["originator"]!.G("userId")); Assert.Null(row["originator"]!["externalPartyId"]);
        Assert.Equal("Unknown", row["originator"]!.S("state")); Assert.Null(row["originator"]!["name"]);
        Assert.Equal(omar, row["verifier"]!.G("id")); Assert.Equal("Unknown", row["verifier"]!.S("state")); Assert.False(row["actionability"]!["canAct"]!.GetValue<bool>());
        Assert.DoesNotContain("@hub.test", result.ToJsonString()); Assert.DoesNotContain("personFacts", result.ToJsonString());
        Assert.Equal(omar, result["options"]!["originators"]![0]!.G("userId"));
    }

    [Fact]
    public async Task Readonly_table_parity_preserves_every_business_and_private_seen_counter()
    {
        await Flag("register_screens", true); var s = await Package(); var finding = await Finding(s, "Resolver action remains named");
        var before = State(s.Project.Id); var pm = await Get(s.Project.Id); var readOnly = await Get(s.Project.Id, who: TestData.Rita); var owner = await Get(s.Project.Id, who: TestData.Alex);
        Assert.Equal(Ids(pm), Ids(readOnly)); Assert.Equal(pm.I("totalCount"), readOnly.I("totalCount"));
        foreach (var field in new[] { "originator", "resolutionOwner", "verifier", "source" })
            Assert.True(JsonNode.DeepEquals(Row(pm, finding)[field], Row(readOnly, finding)[field]));
        Assert.False(Row(readOnly, finding)["actionability"]!["canAct"]!.GetValue<bool>()); Assert.Equal("perm.read_only", Row(readOnly, finding)["actionability"]!.S("reason"));
        Assert.False(Row(pm, finding)["actionability"]!["canAct"]!.GetValue<bool>()); Assert.Equal("not_your_action", Row(pm, finding)["actionability"]!.S("reason"));
        Assert.True(Row(owner, finding)["actionability"]!["canAct"]!.GetValue<bool>()); Assert.Equal(RecordContextAction.RespondToComment, Row(owner, finding)["actionability"]!.S("nextAction"));
        Assert.Equal(before, State(s.Project.Id));
    }

    [Theory]
    [InlineData("round")]
    [InlineData("source")]
    [InlineData("discipline")]
    [InlineData("deliverable")]
    public async Task Malformed_foreign_relationships_are_omitted_from_rows_counts_and_options(string malformed)
    {
        await Flag("register_screens", true); var visible = await Package(title: "Public package"); var finding = await Finding(visible, "Foreign relation sentinel");
        var other = await Package(title: "PRIVATE FOREIGN PACKAGE");
        // Deliberate test-database corruption: valid FKs with mismatched project/parent relationships.
        await f.DbAsync(async db => {
            if (malformed == "round") (await db.ReviewFindings.SingleAsync(row => row.Id == finding)).RoundId = other.Round;
            if (malformed == "source") (await db.ReviewFindings.SingleAsync(row => row.Id == finding)).SourceRevisionId = other.Source;
            if (malformed == "discipline") (await db.ReviewFindings.SingleAsync(row => row.Id == finding)).ProjectDisciplineId = data.ProjectDiscipline(other.Project.Id, "Civil");
            if (malformed == "deliverable")
                await db.SourceRevisions.Where(row => row.Id == visible.Source).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.DeliverableId, (Guid?)other.Deliverable)); // Bypass immutable guard only in corruption fixture.
            return await db.SaveChangesAsync();
        });
        var read = await Get(visible.Project.Id, "?allRounds=true"); Assert.Empty(Ids(read)); Assert.Equal(0, read.I("totalCount"));
        Assert.Empty(read["options"]!["disciplines"]!.AsArray()); Assert.Empty(read["options"]!["originators"]!.AsArray()); Assert.Empty(read["options"]!["resolutionOwners"]!.AsArray());
        Assert.DoesNotContain(other.Package.ToString(), read.ToJsonString()); Assert.DoesNotContain(other.Source.ToString(), read.ToJsonString()); Assert.DoesNotContain("PRIVATE", read.ToJsonString());
    }

    [Fact]
    public async Task Deleted_parents_are_hidden_and_deleted_same_project_source_work_is_unavailable_history()
    {
        await Flag("register_screens", true); var s = await Package(); var finding = await Finding(s, "Source history remains real");
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(row => row.Id == s.Deliverable)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var history = Row(await Get(s.Project.Id), finding);
        Assert.Equal(s.Source, history["source"]!.G("id")); Assert.False(history["source"]!["available"]!.GetValue<bool>()); Assert.Null(history["source"]!["deliverableId"]);
        await f.DbAsync(async db => { (await db.ReviewPackages.SingleAsync(row => row.Id == s.Package)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var gone = await Get(s.Project.Id, "?allRounds=true"); Assert.Empty(Ids(gone)); Assert.Equal(0, gone.I("totalCount"));
        Assert.Empty(gone["options"]!["originators"]!.AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync(Root(s.Project.Id) + $"/review-findings/{finding}")).StatusCode);
    }

    [Fact]
    public async Task Restricted_project_access_and_foreign_filter_values_never_expand_authorized_options()
    {
        await Flag("register_screens", true); var open = await Package(); await Finding(open, "Visible comment");
        var restricted = await Package(title: "PRIVATE RESTRICTED PACKAGE"); await Finding(restricted, "PRIVATE RESTRICTED COMMENT");
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == restricted.Project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Root(restricted.Project.Id) + "/review-findings")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync(Root(Guid.NewGuid()) + "/review-findings")).StatusCode);
        var filtered = await Get(open.Project.Id, $"?disciplineId={data.ProjectDiscipline(restricted.Project.Id, "Civil")}", TestData.Rita);
        Assert.Empty(Ids(filtered)); Assert.Equal(0, filtered.I("totalCount"));
        Assert.DoesNotContain(restricted.Package.ToString(), filtered.ToJsonString()); Assert.DoesNotContain("PRIVATE", filtered.ToJsonString());
        Assert.All(filtered["options"]!["disciplines"]!.AsArray(), row => Assert.Equal(data.ProjectDiscipline(open.Project.Id, "Electrical"), row!.G("id")));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Register_and_exact_locator_flags_are_independent_and_legacy_review_routes_remain_available(bool register, bool patterns)
    {
        var s = await Package(); var finding = await Finding(s, "Exact locator flags");
        await Flag("register_screens", register); await Flag("screen_patterns", patterns);
        var before = State(s.Project.Id); var table = await f.As(TestData.Alex).GetAsync(Root(s.Project.Id) + "/review-findings");
        Assert.Equal(register ? HttpStatusCode.OK : HttpStatusCode.NotFound, table.StatusCode);
        var child = await f.As(TestData.Alex).GetAsync(Root(s.Project.Id) + $"/review-findings/{finding}");
        Assert.Equal(register || patterns ? HttpStatusCode.OK : HttpStatusCode.NotFound, child.StatusCode);
        if (register || patterns)
        {
            var locator = await child.Json(); Assert.Equal(finding, locator.G("id")); Assert.Equal(s.Package, locator.G("packageId")); Assert.Equal(s.Round, locator.G("roundId"));
            Assert.Equal(patterns, locator.AsObject().ContainsKey("context"));
            if (patterns) Assert.NotNull(locator["context"]);
        }
        (await f.As(TestData.Alex).GetAsync(Root(s.Project.Id) + "/reviews")).EnsureSuccessStatusCode();
        Assert.Equal(before, State(s.Project.Id));
    }

    [Theory]
    [InlineData("?severity=Unknown")]
    [InlineData("?originatorKind=Account")]
    [InlineData("?disciplineId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?resolverId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?originatorId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?allRounds=unexpected")]
    public async Task Invalid_filters_are_refused_without_read_side_writes(string query)
    {
        await Flag("register_screens", true); var s = await Package(); await Finding(s, "Filter validation"); var before = State(s.Project.Id);
        await Get(s.Project.Id, query, status: 400); Assert.Equal(before, State(s.Project.Id));
    }
}
