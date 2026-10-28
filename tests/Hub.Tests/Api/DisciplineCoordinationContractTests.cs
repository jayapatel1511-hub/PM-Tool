using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Pins the bytes of GET /projects/{id}/discipline-coordination and GET /discipline-coordination (§25, FR-DCV-01 to FR-DCV-04) for the
/// typed records of packet 040 slice S15: every section's property names and order, the nulls that stay in a row, the scalar tail,
/// and the workspace wrapper with its project rows and choice lists.
[Collection("api")]
public sealed class DisciplineCoordinationContractTests(HubFactory f)
{
    readonly TestData d = new(f);
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    static JsonNode First(JsonNode n, string section) => n[section]!.AsArray().Single()!;

    sealed record Seeded(Project P, Guid Task, Guid Handoff, Guid Notice, Guid Review, Guid Issue, Guid Package, Guid Action);

    async Task<Seeded> Seed()
    {
        var p = await d.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var (civil, electrical) = (d.ProjectDiscipline(p.Id, "Civil"), d.ProjectDiscipline(p.Id, "Electrical"));
        var (alex, marc, omar, pm) = (d.User(TestData.Alex), d.User(TestData.Marc), d.User(TestData.Omar), d.User(TestData.Pm));
        var task = (await d.NewTask(p.Id, TestData.Omar, new { name = "Service alignment", assigneeId = omar, dueDate = "2026-09-20" }, "Electrical")).G("id");
        var gone = (await d.NewTask(p.Id, TestData.Omar, new { name = "Removed alignment", assigneeId = omar, dueDate = "2026-09-21" }, "Electrical")).G("id");
        var deliverable = (await f.As(TestData.Marc).Post(root + "/deliverables", new { name = "Survey basis", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = alex, revision = "A", requiresReview = false }).Result.Json(201)).G("id");
        var milestone = (await f.As(TestData.Pm).Post(root + "/milestones", new { name = "Permit submission", milestoneType = "Design Submission", date = "2026-09-30" }).Result.Json(201)).G("id");
        SourceRevision Revision(string r) => new() { ProjectId = p.Id, DeliverableId = deliverable, SourceKey = "survey", IdentityHash = $"survey-{r}", Title = "Survey basis", Revision = r, Url = $"https://example.test/survey-{r}.pdf" };
        var (a, b) = (Revision("A"), Revision("B"));
        var week = new DateOnly(2026, 9, 16);
        var handoff = new Handoff { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-H0001", Title = "Survey to Electrical", SourceDeliverableId = deliverable, SendingDisciplineId = civil,
            ReceivingDisciplineId = electrical, SendingOwnerId = alex, ReceivingOwnerId = omar, TargetTaskId = task, NeededBy = week, PromisedBy = week.AddDays(2), Status = HandoffStatus.Submitted };
        var notice = new ChangeNotice { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CH0001", Title = "Revised survey", OwnerId = marc, ProjectDisciplineId = civil, OldRevisionId = a.Id, NewRevisionId = b.Id,
            EffectiveDate = week, AssessmentDueDate = week, Status = ChangeStatus.Open };
        var issue = new Issue { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-I0001", Title = "Survey gap", RaisedById = marc, OwnerId = omar, ProjectDisciplineId = electrical, DateRaised = week };
        var review = new ReviewPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-RV0001", Title = "Survey review", CoordinatorId = marc, ProjectDisciplineId = civil, Status = ReviewStatus.InReview };
        var round = new ReviewRound { ProjectId = p.Id, PackageId = review.Id, Number = 1, Purpose = "Check", Status = ReviewStatus.InReview };
        var package = new SubmissionPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-SUB0001", Title = "Permit package", Purpose = "Permit", RecipientReference = "City", CoordinatorId = marc,
            MilestoneId = milestone, TargetDate = new DateOnly(2026, 9, 25) };
        var assessment = new ReadinessAssessment { ProjectId = p.Id, TargetType = "Task", TargetId = task, OwnerId = omar, IntendedOutput = "Alignment", CompletionCriteria = "Checked", State = ReadinessState.NeedsAssessment };
        await f.DbAsync(async db =>
        {
            db.SourceRevisions.AddRange(a, b);
            db.Handoffs.Add(handoff);
            db.ChangeNotices.Add(notice);
            db.ChangeAssessments.AddRange(
                new() { ProjectId = p.Id, ChangeNoticeId = notice.Id, TargetType = "Task", TargetId = task, OwnerId = omar, RevisionUsedId = a.Id },
                new() { ProjectId = p.Id, ChangeNoticeId = notice.Id, TargetType = "Task", TargetId = gone, OwnerId = omar, RevisionUsedId = a.Id });
            db.InputUses.Add(new() { ProjectId = p.Id, TargetType = "Task", TargetId = task, OwnerId = omar, SourceIdentity = "survey", SourceRevisionId = a.Id, IntendedUse = "Set the alignment",
                AdoptedAt = f.Clock.GetUtcNow(), AdoptedBy = omar });
            db.Issues.Add(issue);
            db.ReviewPackages.Add(review);
            db.ReviewRounds.Add(round);
            db.DisciplineReviews.Add(new() { ProjectId = p.Id, RoundId = round.Id, ProjectDisciplineId = electrical, ReviewerId = omar, DueDate = week });
            db.ReviewFindings.Add(new() { ProjectId = p.Id, PackageId = review.Id, RoundId = round.Id, IssueId = issue.Id, SourceRevisionId = a.Id, ProjectDisciplineId = electrical,
                OriginatorId = omar, ResolverId = marc, VerifierId = pm, Text = "Missing spot level", Severity = "Blocking" });
            db.SubmissionPackages.Add(package);
            db.WorkConstraints.Add(new() { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = task, Category = "Capacity", Description = "Two people away",
                RemovalOwnerId = pm, AffectedOwnerId = omar, NeededBy = week, SourceUrl = "https://example.test/c" });
            db.ReadinessAssessments.Add(assessment);
            foreach (var code in ReadinessCheckCode.All) db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = p.Id, AssessmentId = assessment.Id, Code = code }); // every check still unanswered
            var removed = await db.Tasks.SingleAsync(t => t.Id == gone);
            removed.DeletedAt = f.Clock.GetUtcNow();
            await db.SaveChangesAsync();
            review.CurrentRoundId = round.Id; // the package and its round point at each other, so the round is saved first
            return await db.SaveChangesAsync();
        });
        var meeting = await f.As(TestData.Pm).Post(root + "/meetings/current", new { }).Result.Json();
        var action = (await f.As(TestData.Pm).Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Resolve the survey input", ownerType = "User", ownerUserId = omar, dueDate = "2026-09-18",
            relatedTaskId = task, links = new[] { new { targetType = ItemType.Handoff, targetId = handoff.Id }, new { targetType = ItemType.ChangeNotice, targetId = notice.Id } } }).Result.Json(201)).G("id");
        return new(p, task, handoff.Id, notice.Id, review.Id, issue.Id, package.Id, action);
    }

    [Fact]
    public async Task Project_projection_keeps_every_section_name_order_and_null()
    {
        var s = await Seed();
        var window = "from=2026-09-01&to=2026-12-31";
        var r = await f.As(TestData.Omar).GetAsync($"/api/v1/projects/{s.P.Id}/discipline-coordination?{window}").Result.Json(); // Omar's whole-project view: the receiving owner, no discipline filter
        Assert.Equal(["evaluatedAt", "handoffs", "outgoing", "incoming", "outgoingPage", "incomingPage", "outgoingTotal", "incomingTotal", "handoffPage", "handoffPageSize", "changeTargets",
            "unavailableChangeTargets", "changeTargetsTotal", "unavailableChangeTargetsTotal", "reviews", "reviewsPage", "reviewsPageSize", "uses", "usesTotal", "usesPage", "usesPageSize",
            "linkedIssues", "linkedIssuesTotal", "linkedIssuesPage", "linkedIssuesPageSize", "handoffsTotal", "changes", "changesTotal", "changesPage", "changesPageSize", "reviewsTotal",
            "blockerGroups", "blockerGroupsTotal", "startability", "startabilityTotal", "startabilityPage", "startabilityPageSize", "startabilityFrom", "startabilityTo", "startabilityReadyTotal",
            "linkedActions", "linkedActionsTotal", "upcomingSubmissions", "upcomingSubmissionsTotal", "upcomingSubmissionsPage", "staffingConflicts", "staffingConflictsTotal",
            "staffingConflictsPage", "blockerGroupsPage", "page", "pageSize"], Keys(r));

        string[] handoff = ["id", "key", "title", "status", "neededBy", "promisedBy", "targetTaskId", "targetDeliverableId", "sendingOwnerId", "receivingOwnerId", "sendingDisciplineId", "receivingDisciplineId"];
        Assert.Equal(handoff, Keys(First(r, "handoffs")));
        Assert.Equal(handoff, Keys(First(r, "incoming")));
        Assert.Empty(r["outgoing"]!.AsArray()); // Electrical receives this one
        Assert.Equal((s.Handoff, "Submitted", "2026-09-16", "2026-09-18", s.Task, true), (First(r, "handoffs").G("id"), First(r, "handoffs").S("status"), First(r, "handoffs").S("neededBy"),
            First(r, "handoffs").S("promisedBy"), First(r, "handoffs").G("targetTaskId"), First(r, "handoffs")["targetDeliverableId"] is null));
        Assert.Equal((1, 1, 1, 1, 0, 1), (r.I("handoffsTotal"), r.I("incomingTotal"), r.I("outgoingPage"), r.I("incomingPage"), r.I("outgoingTotal"), r.I("handoffPage")));
        Assert.Equal(["handoffId", "handoffKey", "taskIds", "taskKeys"], Keys(First(r, "blockerGroups")));
        Assert.Equal((s.Handoff, 1, 1), (First(r, "blockerGroups").G("handoffId"), First(r, "blockerGroups")["taskIds"]!.AsArray().Count, First(r, "blockerGroups")["taskKeys"]!.AsArray().Count));

        Assert.Equal(["id", "key", "title", "status", "ownerId", "pendingAssessments", "acknowledgedPending", "projectPending"], Keys(First(r, "changes")));
        Assert.Equal((s.Notice, "Open", 2, 0, 2), (First(r, "changes").G("id"), First(r, "changes").S("status"), First(r, "changes").I("pendingAssessments"), First(r, "changes").I("acknowledgedPending"), First(r, "changes").I("projectPending")));
        Assert.Equal(["changeNoticeId", "targetType", "targetId", "available"], Keys(First(r, "changeTargets")));
        Assert.Equal((s.Task, "Task", true), (First(r, "changeTargets").G("targetId"), First(r, "changeTargets").S("targetType"), First(r, "changeTargets")["available"]!.GetValue<bool>()));
        Assert.Equal(["changeNoticeId", "count"], Keys(First(r, "unavailableChangeTargets")));
        Assert.Equal((1, 1, 1), (First(r, "unavailableChangeTargets").I("count"), r.I("changeTargetsTotal"), r.I("unavailableChangeTargetsTotal")));

        Assert.Equal(["id", "key", "title", "status", "coordinatorId", "outstandingDisciplines", "blockingFindings"], Keys(First(r, "reviews")));
        Assert.Equal((s.Review, "In Review", 1, 1), (First(r, "reviews").G("id"), First(r, "reviews").S("status"), First(r, "reviews").I("outstandingDisciplines"), First(r, "reviews").I("blockingFindings")));
        Assert.Equal(["id", "key", "title", "status", "ownerId", "ownerName", "projectDisciplineId"], Keys(First(r, "linkedIssues")));
        Assert.Equal((s.Issue, "Open", "Omar Haddad"), (First(r, "linkedIssues").G("id"), First(r, "linkedIssues").S("status"), First(r, "linkedIssues").S("ownerName")));
        Assert.Equal(["id", "targetType", "targetId", "ownerId", "sourceIdentity", "sourceRevisionId", "intendedUse", "adoptedAt", "adoptedBy", "rowVersion", "targetKey", "targetName", "sourceUrl", "sourceKey", "revision"], Keys(First(r, "uses")));
        Assert.Equal(("Task", "Service alignment", "https://example.test/survey-A.pdf", "survey", "A"), (First(r, "uses").S("targetType"), First(r, "uses").S("targetName"), First(r, "uses").S("sourceUrl"),
            First(r, "uses").S("sourceKey"), First(r, "uses").S("revision")));

        Assert.Equal(["id", "targetType", "targetId", "key", "name", "dueDate", "ownerId", "disciplineId", "state", "blocked", "unknown"], Keys(First(r, "startability")));
        Assert.Equal((s.Task, "Task", "2026-09-20", "2026-09-01", "2026-12-31", 1), (First(r, "startability").G("targetId"), First(r, "startability").S("targetType"), First(r, "startability").S("dueDate"),
            r.S("startabilityFrom"), r.S("startabilityTo"), r.I("startabilityTotal")));
        Assert.Equal(["id", "key", "text", "status", "dueDate", "sourceType", "sourceId"], Keys(r["linkedActions"]!.AsArray()[0]));
        Assert.Equal((2, s.Action), (r.I("linkedActionsTotal"), r["linkedActions"]!.AsArray()[0]!.G("id")));
        Assert.Equal(["id", "key", "title", "status", "effectiveStatus", "targetDate", "coordinatorId", "failingChecks"], Keys(First(r, "upcomingSubmissions")));
        Assert.Equal((s.Package, "Draft", "2026-09-25"), (First(r, "upcomingSubmissions").G("id"), First(r, "upcomingSubmissions").S("status"), First(r, "upcomingSubmissions").S("targetDate")));
        var failing = First(r, "upcomingSubmissions")["failingChecks"]!.AsArray();
        Assert.Equal(2, failing.Count); // an empty manifest and an empty check list
        Assert.Equal(["sourceId", "kind", "code", "message", "sourcePath"], Keys(failing[0]));
        Assert.Equal(["id", "key", "targetType", "targetId", "description", "neededBy", "state", "affectedOwnerId", "removalOwnerId"], Keys(First(r, "staffingConflicts")));
        Assert.Equal(("Two people away", "Open"), (First(r, "staffingConflicts").S("description"), First(r, "staffingConflicts").S("state")));
        Assert.Equal((0, 1, 1, 1), (r.I("pageSize"), r.I("page"), r.I("upcomingSubmissionsTotal"), r.I("staffingConflictsTotal"))); // unpaged: one page, size 0

        // A page request fills the paging scalars; the sending side sees the same handoff as outgoing.
        var civil = await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{s.P.Id}/discipline-coordination?{window}&page=1&pageSize=4").Result.Json();
        Assert.Equal(handoff, Keys(First(civil, "outgoing")));
        Assert.Equal((1, 4, 4), (civil.I("page"), civil.I("pageSize"), civil.I("handoffPageSize")));
    }

    [Fact]
    public async Task Workspace_projection_keeps_its_wrapper_rows_and_choice_lists()
    {
        var s = await Seed();
        var r = await f.As(TestData.Omar).GetAsync($"/api/v1/discipline-coordination?projectId={s.P.Id}&from=2026-09-01&to=2026-12-31").Result.Json();
        Assert.Equal(["evaluatedAt", "actorId", "projects", "projectChoices", "disciplines", "owners", "scope"], Keys(r));
        Assert.Equal(("Project", d.User(TestData.Omar)), (r.S("scope"), r.G("actorId")));
        var row = First(r, "projects");
        Assert.Equal(["id", "projectNumber", "name", "disciplineId", "data"], Keys(row));
        Assert.Equal((s.P.Id, s.P.ProjectNumber), (row.G("id"), row.S("projectNumber")));
        Assert.Equal(51, Keys(row["data"]).Length);
        var choice = r["projectChoices"]!.AsArray().First(x => x!.G("id") == s.P.Id)!; // the list holds every project the caller may filter by
        Assert.Equal(["id", "projectNumber", "name", "projectManagerId"], Keys(choice));
        Assert.Equal(s.P.Id, choice.G("id"));
        Assert.Equal(["id", "name"], Keys(r["disciplines"]!.AsArray()[0]));
        Assert.Equal(["Civil", "Electrical"], r["disciplines"]!.AsArray().Select(x => x!.S("name")));
        Assert.Equal(["id", "displayName"], Keys(r["owners"]!.AsArray()[0]));
        Assert.Contains("Omar Haddad", r["owners"]!.AsArray().Select(x => x!.S("displayName")));

        var all = await f.As(TestData.Omar).GetAsync("/api/v1/discipline-coordination").Result.Json();
        Assert.Equal("Workspace", all.S("scope"));
        Assert.Equal(["id", "projectNumber", "name", "disciplineId", "data"], Keys(all["projects"]!.AsArray().First(x => x!.G("id") == s.P.Id)));
    }
}
