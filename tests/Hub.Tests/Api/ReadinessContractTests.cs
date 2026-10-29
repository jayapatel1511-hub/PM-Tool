using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// The readiness reads answer with typed records that must serialise to the bytes the Readiness page already reads (040-S15-Readiness).
/// Its own host and database, as DeliverableContractTests: a class in the shared "api" collection changes which data-dependent reads the shape snapshot sees.
public sealed class ReadinessContractTests(HubFactory f) : IClassFixture<HubFactory>
{
    readonly TestData data = new(f);

    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static JsonNode One(JsonNode? n) => Assert.Single(n!.AsArray())!;
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    // The entities these reads used to serialise whole: their own columns first, then the audit identity, the stamps, the row version and the id.
    static readonly string[] AssessmentKeys = ["targetType", "targetId", "ownerId", "intendedOutput", "completionCriteria", "state", "evaluatedAt",
        "auditType", "projectId", "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] CheckKeys = ["assessmentId", "code", "applies", "satisfied", "reason", "evidenceUrl", "recordedBy",
        "auditType", "projectId", "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] ExceptionKeys = ["assessmentId", "basisVersionId", "approvedBy", "verifierId", "limitedWork", "risk", "expiresOn",
        "auditType", "projectId", "auditProjectId", "auditKey", "auditName", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];
    static readonly string[] ConstraintKeys = ["seq", "key", "targetType", "targetId", "category", "description", "removalOwnerId", "affectedOwnerId", "neededBy", "sourceUrl",
        "state", "resolutionEvidenceUrl", "verifiedBy", "verifiedAt", "linkedType", "linkedId",
        "auditType", "auditKey", "auditName", "projectId", "auditProjectId", "createdAt", "createdBy", "updatedAt", "updatedBy", "rowVersion", "id"];

    sealed record World(Project P, string Root, Guid Milestone, Guid Deliverable, Guid Revision, Guid Task, string Readiness);

    // A task of Alex's with an assessment, a deliverable's source revision and a milestone to build submission packages on.
    async Task<World> New()
    {
        var p = await data.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var civil = data.ProjectDiscipline(p.Id, "Civil");
        var alex = data.User(TestData.Alex);
        var milestone = (await Post(TestData.Pm, root + "/milestones", new MilestoneEndpoints.CreateBody("Permit submission",
            MilestoneType.DesignSubmission, new DateOnly(2026, 10, 30), null, null, null, true), 201)).G("id");
        var deliverable = (await Post(TestData.Marc, root + "/deliverables", new { name = "Survey package", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = alex, revision = "A", requiresReview = false }, 201)).G("id");
        var revision = (await Post(TestData.Alex, root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, alex, "Deliverable", "survey", "Survey package", "A", "https://example.test/survey-a.pdf",
            "Survey team", "Corridor", null, null, null, null, null, null))).G("id");
        var task = (await data.NewTask(p.Id, extra: new { assigneeId = alex })).G("id");
        var readiness = $"{root}/readiness/Task/{task}";
        await Post(TestData.Alex, readiness, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(task), "Grading layout", "Layout checked against the permit set"));
        return new World(p, root, milestone, deliverable, revision, task, readiness);
    }

    async Task<Guid> Package(World w) =>
        (await Post(TestData.Marc, w.Root + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Permit package", "Permit review",
            "Municipality", data.User(TestData.Marc), w.Milestone, new DateOnly(2026, 10, 30), [new(w.Revision)], [], null, null))).G("id");

    static void AssertStrings(JsonNode? n) { foreach (var x in n!.AsArray()) Assert.Equal(System.Text.Json.JsonValueKind.String, x!.GetValueKind()); }

    [Fact]
    public async Task Detail_answers_the_assessment_its_checks_its_sources_and_its_exceptions_in_the_order_the_page_reads()
    {
        var w = await New();
        var civil = data.ProjectDiscipline(w.P.Id, "Civil");

        // No link yet: the assessment, ten-odd checks, empty sources and exceptions (empty arrays, not omitted), and the codes still unknown.
        var plain = await Get(TestData.Alex, w.Readiness);
        Assert.Equal(["assessment", "checks", "sources", "unknown", "blocked", "exceptions"], Keys(plain));
        Assert.Equal(AssessmentKeys, Keys(plain["assessment"]));
        Assert.Equal(("Task", w.Task, "Grading layout", data.User(TestData.Alex)), (plain["assessment"]!.S("targetType"), plain["assessment"]!.G("targetId"), plain["assessment"]!.S("intendedOutput"), plain["assessment"]!.G("ownerId")));
        Assert.Equal(w.P.Id, plain["assessment"]!.G("projectId")); Assert.Equal("ReadinessAssessment", plain["assessment"]!.S("auditType")); Assert.Equal(w.P.Id, plain["assessment"]!.G("auditProjectId"));
        Assert.Null(plain["assessment"]!["auditKey"]); Assert.Null(plain["assessment"]!["auditName"]); Assert.Equal(0, plain["assessment"]!.I("rowVersion")); // null, not omitted
        Assert.NotEmpty(plain["checks"]!.AsArray());
        Assert.All(plain["checks"]!.AsArray(), c => Assert.Equal(CheckKeys, Keys(c)));
        Assert.Equal(["Basis", "Decision", "Handoff", "Predecessor", "Production Capacity", "Production Owner", "Review Capacity", "Review Gate", "Submission Gate"],
            plain["checks"]!.AsArray().Select(c => c!.S("code")).Order(StringComparer.Ordinal).ToArray()); // sorted by code, as the query orders them
        var owner = plain["checks"]!.AsArray().Single(c => c!.S("code") == "Production Owner")!;
        Assert.Equal(("ReadinessCheck", true, true), (owner.S("auditType"), owner["applies"]!.GetValue<bool>(), owner["satisfied"]!.GetValue<bool>()));
        Assert.Null(owner["evidenceUrl"]); Assert.False(string.IsNullOrWhiteSpace(owner.S("reason"))); // the production owner is current source evidence
        Assert.Empty(plain["sources"]!.AsArray()); Assert.Empty(plain["exceptions"]!.AsArray());
        AssertStrings(plain["unknown"]); AssertStrings(plain["blocked"]);
        Assert.Contains("Submission Gate", plain["unknown"]!.AsArray().Select(x => x!.GetValue<string>()));

        // A linked submission package is source evidence: the gate's code and the package as a record of its type, key, title and status.
        var package = await Package(w);
        await Post(TestData.Pm, w.Readiness + "/submission-prerequisites", new ReadinessEndpoints.PrerequisiteBody(Guid.NewGuid(), Version<WorkTask>(w.Task), package, "Permit set must be issued before grading"));
        var linked = await Get(TestData.Alex, w.Readiness);
        var source = One(linked["sources"]);
        Assert.Equal(["code", "record"], Keys(source));
        Assert.Equal(["type", "id", "key", "title", "status"], Keys(source["record"]));
        Assert.Equal(("Submission Gate", "SubmissionPackage", package, "Permit package", SubmissionStatus.Draft), (source.S("code"), source["record"]!.S("type"), source["record"]!.G("id"), source["record"]!.S("title"), source["record"]!.S("status")));
        Assert.Contains("Submission Gate", linked["blocked"]!.AsArray().Select(x => x!.GetValue<string>()));

        // An assumption exception: the whole record of the approval, its row version and audit identity included.
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var owner2 = data.User(TestData.Alex);
        var task = await data.NewTask(w.P.Id, extra: new { assigneeId = owner2 });
        var taskId = task.G("id");
        var basisRoot = $"{w.Root}/design-basis";
        var input = new DesignBasisEndpoints.VersionInput("Synthetic grading", "Assume utility depth", null, null, null, null, null, null, today.AddDays(3), null);
        var entry = await Post(TestData.Pm, basisRoot, new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption, "Utility depth for readiness", owner2, civil, data.User(TestData.Marc), input, null));
        var entryId = entry.G("id");
        var version = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId));
        await Post(TestData.Marc, $"{basisRoot}/{entryId}/versions/{version.Id}/proceed", new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(), version.RowVersion, input.Scope, owner2, today.AddDays(5), "Limited preliminary work"));
        await Post(TestData.Alex, $"{basisRoot}/{entryId}/uses", new DesignBasisEndpoints.UseBody(Guid.NewGuid(), version.Id, "Task", taskId, "Preliminary layout under assumption"));
        var path = $"{w.Root}/readiness/Task/{taskId}";
        var assessment = await Post(TestData.Alex, path, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(taskId), "Preliminary layout", "Layout reviewed against utility evidence"));
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            {
                check.Applies = check.Code is ReadinessCheckCode.Basis or ReadinessCheckCode.ProductionOwner;
                check.Satisfied = check.Applies == true ? check.Code == ReadinessCheckCode.ProductionOwner : null;
            }
            return await db.SaveChangesAsync();
        });
        await Post(TestData.Pm, path + "/exceptions", new ReadinessEndpoints.ExceptionBody(Guid.NewGuid(), assessment.I("rowVersion"), version.Id, version.RowVersion,
            data.User(TestData.Marc), "Only preliminary grading layout", "Utility location could alter grading", today.AddDays(1)));
        var excepted = await Get(TestData.Alex, path);
        Assert.Equal(ReadinessState.ProceedUnderAssumption, excepted["assessment"]!.S("state"));
        var exception = One(excepted["exceptions"]);
        Assert.Equal(ExceptionKeys, Keys(exception));
        Assert.Equal((version.Id, data.User(TestData.Pm), data.User(TestData.Marc), "Only preliminary grading layout", "ReadinessException"),
            (exception.G("basisVersionId"), exception.G("approvedBy"), exception.G("verifierId"), exception.S("limitedWork"), exception.S("auditType")));
        Assert.Equal(today.AddDays(1).ToString("yyyy-MM-dd"), exception.S("expiresOn"));
        Assert.Equal(CheckKeys, Keys(excepted["checks"]![0]));
        // A check the page has not assessed is written as null, never left out.
        var unassessed = excepted["checks"]!.AsArray().First(c => c!["satisfied"] is null)!;
        Assert.True(unassessed.AsObject().ContainsKey("satisfied")); Assert.True(unassessed.AsObject().ContainsKey("recordedBy")); Assert.True(unassessed.AsObject().ContainsKey("evidenceUrl"));

        // A person who cannot see the project, and a missing assessment, are refused as before.
        await Get(TestData.Alex, $"{w.Root}/readiness/Task/{Guid.NewGuid()}", 404);
        await Get(TestData.Alex, $"{w.Root}/readiness/Risk/{w.Task}", 400);
    }

    [Fact]
    public async Task Constraints_and_the_constraint_lookup_keep_their_property_names_nulls_and_linked_record()
    {
        var w = await New();
        var project = w.P;
        var decision = (await Post(TestData.Pm, $"{w.Root}/decisions", new { subject = "Confirm drainage outfall", description = "Outfall location decides the grading",
            ownerUserId = data.User(TestData.Alex), requiredByDate = "2026-10-01", impactLevel = "Medium", impactDescription = "Grading waits on it" }, 201)).G("id");
        var issue = (await Post(TestData.Alex, $"{w.Root}/issues", new { title = "Utility clash at the outfall", severity = "High", ownerId = data.User(TestData.Alex),
            projectDisciplineId = data.ProjectDiscipline(project.Id, "Civil") }, 201)).G("id");
        ReadinessEndpoints.ConstraintBody Body(string? type = null, Guid? id = null) => new(Guid.NewGuid(), Version<WorkTask>(w.Task), "Decision", "Await the outfall decision",
            data.User(TestData.Pm), new DateOnly(2026, 9, 21), "https://example.test/outfall", type, id);
        var path = w.Readiness + "/constraints";
        Assert.Empty((await Get(TestData.Alex, path)).AsArray()); // [] not omitted

        var withDecision = (await Post(TestData.Alex, path, Body(ItemType.Decision, decision))).G("id");
        var withIssue = (await Post(TestData.Alex, path, Body(ItemType.Issue, issue))).G("id");
        var plain = (await Post(TestData.Alex, path, Body())).G("id");
        await f.DbAsync(async db => { (await db.Issues.SingleAsync(i => i.Id == issue)).DeletedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); }); // a deleted record reads as unavailable

        var rows = (await Get(TestData.Rita, path)).AsArray();
        Assert.Equal([withDecision, withIssue, plain], rows.Select(r => r!.G("id")).ToArray());
        Assert.All(rows, r => Assert.Equal(["id", "key", "rowVersion", "targetType", "targetId", "category", "description", "removalOwnerId", "affectedOwnerId", "neededBy", "sourceUrl", "state",
            "resolutionEvidenceUrl", "verifiedBy", "verifiedAt", "linkedType", "linkedId", "createdAt", "createdBy", "linked"], Keys(r)));
        var first = rows[0]!;
        Assert.Equal(($"{project.ProjectNumber}-CT001", "Task", w.Task, "Decision", "Await the outfall decision", "2026-09-21", ConstraintState.Open),
            (first.S("key"), first.S("targetType"), first.G("targetId"), first.S("category"), first.S("description"), first.S("neededBy"), first.S("state")));
        Assert.Equal((data.User(TestData.Pm), data.User(TestData.Alex), ItemType.Decision, decision), (first.G("removalOwnerId"), first.G("affectedOwnerId"), first.S("linkedType"), first.G("linkedId")));
        Assert.Null(first["resolutionEvidenceUrl"]); Assert.Null(first["verifiedBy"]); Assert.Null(first["verifiedAt"]); // null, not omitted
        Assert.Equal(["type", "id", "key", "title", "status"], Keys(first["linked"]));
        Assert.Equal((ItemType.Decision, decision, "Confirm drainage outfall", DecisionStatus.Pending), (first["linked"]!.S("type"), first["linked"]!.G("id"), first["linked"]!.S("title"), first["linked"]!.S("status")));
        Assert.Equal(ItemType.Issue, rows[1]!.S("linkedType")); Assert.True(rows[1]!.AsObject().ContainsKey("linked")); Assert.Null(rows[1]!["linked"]); // linked, but gone
        Assert.Null(rows[2]!["linkedType"]); Assert.Null(rows[2]!["linkedId"]); Assert.Null(rows[2]!["linked"]);

        // The lookup a key or search link opens: where the constraint's work lives.
        var located = await Get(TestData.Alex, $"{w.Root}/readiness/constraints/{plain}");
        Assert.Equal(["id", "key", "targetType", "targetId"], Keys(located));
        Assert.Equal((plain, $"{project.ProjectNumber}-CT003", "Task", w.Task), (located.G("id"), located.S("key"), located.S("targetType"), located.G("targetId")));
        await Get(TestData.Alex, $"{w.Root}/readiness/constraints/{Guid.NewGuid()}", 404);
        await Get(TestData.Alex, $"{w.Root}/readiness/Task/{Guid.NewGuid()}/constraints", 400);
    }

    [Fact]
    public async Task Submission_prerequisites_keep_their_property_names_nulls_the_package_and_the_current_package()
    {
        var w = await New();
        var path = w.Readiness + "/submission-prerequisites";
        Assert.Empty((await Get(TestData.Alex, path)).AsArray()); // [] not omitted
        var first = await Package(w); var second = await Package(w);
        var one = (await Post(TestData.Pm, path, new ReadinessEndpoints.PrerequisiteBody(Guid.NewGuid(), Version<WorkTask>(w.Task), first, "Permit set must be issued first"))).G("id");
        var two = (await Post(TestData.Pm, path, new ReadinessEndpoints.PrerequisiteBody(Guid.NewGuid(), Version<WorkTask>(w.Task), second, "Review set must be issued too"))).G("id");
        await Post(TestData.Pm, $"{path}/{two}/remove", new ReadinessEndpoints.PrerequisiteRemoveBody(Guid.NewGuid(), Version<ReadinessSubmissionPrerequisite>(two), "Review set no longer applies"));

        var rows = (await Get(TestData.Alex, path)).AsArray();
        Assert.Equal([one, two], rows.Select(r => r!.G("id")).ToArray());
        Assert.All(rows, r => Assert.Equal(["id", "rowVersion", "packageId", "reason", "createdAt", "createdBy", "removedAt", "removedBy", "removalReason", "package", "effective", "listsOutput"], Keys(r)));
        var active = rows[0]!;
        Assert.Equal((first, "Permit set must be issued first", data.User(TestData.Pm), false), (active.G("packageId"), active.S("reason"), active.G("createdBy"), active["listsOutput"]!.GetValue<bool>()));
        Assert.Null(active["removedAt"]); Assert.Null(active["removedBy"]); Assert.Null(active["removalReason"]); // null, not omitted
        Assert.Equal(["id", "key", "title", "status"], Keys(active["package"])); Assert.Equal(["id", "key", "title", "status"], Keys(active["effective"]));
        Assert.Equal((first, "Permit package", SubmissionStatus.Draft), (active["package"]!.G("id"), active["package"]!.S("title"), active["package"]!.S("status")));
        Assert.Equal(first, active["effective"]!.G("id"));
        var removed = rows[1]!;
        Assert.Equal((data.User(TestData.Pm), "Review set no longer applies"), (removed.G("removedBy"), removed.S("removalReason"))); Assert.NotNull(removed["removedAt"]);
        Assert.Equal(second, removed["package"]!.G("id"));
        await Get(TestData.Alex, $"{w.Root}/readiness/Risk/{w.Task}/submission-prerequisites", 400);
    }

    [Fact]
    public async Task Window_keeps_its_envelope_the_constraint_rows_and_the_ready_outputs()
    {
        var p = await data.Project();
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var path = $"/api/v1/projects/{p.Id}/readiness/window?from={today:yyyy-MM-dd}&to={today.AddDays(6):yyyy-MM-dd}";
        var empty = await Get(TestData.Alex, path);
        Assert.Equal(["from", "to", "constraints", "constraintsTotal", "constraintsPage", "readyPage", "pageSize", "constraintsTruncated", "readyOutputs", "readyOutputsTotal", "readyOutputsTruncated"], Keys(empty));
        Assert.Equal((today.ToString("yyyy-MM-dd"), today.AddDays(6).ToString("yyyy-MM-dd"), 0, 1, 1, 500, false, 0, false), (empty.S("from"), empty.S("to"), empty.I("constraintsTotal"), empty.I("constraintsPage"),
            empty.I("readyPage"), empty.I("pageSize"), empty["constraintsTruncated"]!.GetValue<bool>(), empty.I("readyOutputsTotal"), empty["readyOutputsTruncated"]!.GetValue<bool>()));
        Assert.Empty(empty["constraints"]!.AsArray()); Assert.Empty(empty["readyOutputs"]!.AsArray()); // [] not omitted

        var target = await data.NewTask(p.Id, extra: new { assigneeId = data.User(TestData.Marc), startDate = today, dueDate = today.AddDays(2), estimatedHours = 4m });
        var targetId = target.G("id");
        var assessment = await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/readiness/Task/{targetId}", new ReadinessEndpoints.CreateBody(Guid.NewGuid(), Version<WorkTask>(targetId), "Site layout", "Drawing checked"));
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            {
                check.Applies = check.Code is not (ReadinessCheckCode.ProductionCapacity or ReadinessCheckCode.SubmissionGate);
                check.Satisfied = check.Applies == true ? true : null;
            }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            return await db.SaveChangesAsync();
        });
        var ready = await Get(TestData.Alex, path);
        Assert.Equal(1, ready.I("readyOutputsTotal"));
        var output = One(ready["readyOutputs"]);
        Assert.Equal(["id", "targetType", "targetId", "key", "name", "dueDate", "ownerId", "disciplineId", "intendedOutput", "completionCriteria", "state"], Keys(output));
        Assert.Equal((assessment.G("id"), "Task", targetId, "Site layout", "Drawing checked", ReadinessState.Ready, today.AddDays(2).ToString("yyyy-MM-dd"), data.User(TestData.Marc)),
            (output.G("id"), output.S("targetType"), output.G("targetId"), output.S("intendedOutput"), output.S("completionCriteria"), output.S("state"), output.S("dueDate"), output.G("ownerId")));

        await f.DbAsync(async db =>
        {
            db.WorkConstraints.Add(new WorkConstraint { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = targetId, Category = "Handoff", Description = "Await survey",
                RemovalOwnerId = data.User(TestData.Alex), AffectedOwnerId = data.User(TestData.Marc), NeededBy = today.AddDays(-1), SourceUrl = "https://example.test/survey", State = ConstraintState.Open });
            db.WorkConstraints.Add(new WorkConstraint { ProjectId = p.Id, Seq = 2, Key = p.ProjectNumber + "-CT0002", TargetType = "Task", TargetId = targetId, Category = "Decision", Description = "Await permit",
                RemovalOwnerId = data.User(TestData.Alex), AffectedOwnerId = data.User(TestData.Marc), NeededBy = today.AddDays(1), SourceUrl = "https://example.test/permit", State = ConstraintState.Open });
            return await db.SaveChangesAsync();
        });
        var blocked = await Get(TestData.Alex, path);
        Assert.Equal(["from", "to", "constraints", "constraintsTotal", "constraintsPage", "readyPage", "pageSize", "constraintsTruncated", "readyOutputs", "readyOutputsTotal", "readyOutputsTruncated"], Keys(blocked));
        Assert.Equal((2, 0), (blocked.I("constraintsTotal"), blocked.I("readyOutputsTotal")));
        var row = blocked["constraints"]![0]!;
        Assert.Equal(ConstraintKeys, Keys(row)); // the whole record of the constraint, as the page's window has always read it
        Assert.Equal((p.ProjectNumber + "-CT0001", "WorkConstraint", p.Id, p.ProjectNumber + "-CT0001", "Await survey", ConstraintState.Open), (row.S("key"), row.S("auditType"), row.G("auditProjectId"), row.S("auditKey"), row.S("auditName"), row.S("state")));
        Assert.Null(row["resolutionEvidenceUrl"]); Assert.Null(row["verifiedBy"]); Assert.Null(row["verifiedAt"]); Assert.Null(row["linkedType"]); Assert.Null(row["linkedId"]); // null, not omitted

        var paged = await Get(TestData.Alex, path + "&constraintsPage=2&readyPage=3&pageSize=1");
        Assert.Equal((2, 3, 1, 2, true), (paged.I("constraintsPage"), paged.I("readyPage"), paged.I("pageSize"), paged.I("constraintsTotal"), paged["constraints"]!.AsArray().Count == 1)); // a paged read is never truncated
        Assert.False(paged["constraintsTruncated"]!.GetValue<bool>()); Assert.Empty(paged["readyOutputs"]!.AsArray());
        await Get(TestData.Alex, $"/api/v1/projects/{p.Id}/readiness/window?from={today:yyyy-MM-dd}&to={today.AddDays(-1):yyyy-MM-dd}", 400);
    }
}
