using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Pins the bytes of GET /search (§18.1, FR-SRCH-01/02) for the typed records of packet 040 slice S15: each group's property
/// names and order, the nulls that stay in the row, the groups and counts present for a type filter, and the exact-match shape.
[Collection("api")]
public sealed class SearchContractTests(HubFactory f)
{
    readonly TestData d = new(f);
    Guid U(string email) => d.User(email);
    static string[] Keys(JsonNode? n) => [.. n!.AsObject().Select(x => x.Key)];
    Task<JsonNode> Search(string who, string q, string extra = "") => f.As(who).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(q)}{extra}").Result.Json();
    Task<JsonNode> Post(string who, string path, object body) => f.As(who).Post(path, body).Result.Json(201);

    [Fact]
    public async Task Every_group_keeps_its_property_names_order_and_nulls()
    {
        var word = "zq" + Guid.NewGuid().ToString("N")[..8];
        var project = await d.Project(tweak: b => b["name"] = $"{word} Search Roads");
        var root = $"/api/v1/projects/{project.Id}";
        var civil = d.ProjectDiscipline(project.Id, "Civil");
        var (alex, marc, pm) = (U(TestData.Alex), U(TestData.Marc), U(TestData.Pm));
        var task = await d.NewTask(project.Id, TestData.Pm, new { name = $"{word} culvert", assigneeId = alex, dueDate = "2026-10-05", description = "Plain text" });
        var byText = await d.NewTask(project.Id, TestData.Pm, new { name = "Headwall check", description = $"Memo about {word} flows" });
        var deliverable = await Post(TestData.Pm, root + "/deliverables", new { name = $"{word} drawings", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(), ownerId = alex, dueDate = "2026-10-06" });
        var milestone = await Post(TestData.Pm, root + "/milestones", new { name = $"{word} submission", milestoneType = "Design Submission", date = "2026-11-02" });
        var party = await Post(TestData.Pm, root + "/external-parties", new { name = "Karen Li", isClient = true });
        await Post(TestData.Pm, root + "/decisions", new { subject = $"{word} pavement", description = "Options", ownerExternalPartyId = party.G("id"),
            requiredByDate = "2026-10-01", impactLevel = "High", impactDescription = "Drawings wait" });
        await Post(TestData.Pm, $"/api/v1/items/Task/{task.S("id")}/comments", new { body = $"Ask about the {word} culvert" });
        var week = new DateOnly(2026, 10, 5);
        SourceRevision Revision(string r) => new() { ProjectId = project.Id, DeliverableId = deliverable.G("id"), SourceKey = $"{word}-{r}", IdentityHash = $"{word}-{r}", Title = word, Revision = r, Url = $"https://example.test/{word}-{r}" };
        var (oldRevision, newRevision) = (Revision("A"), Revision("B"));
        await f.DbAsync(async db =>
        {
            db.SourceRevisions.AddRange(oldRevision, newRevision);
            db.Handoffs.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-H0001", Title = word + " handoff", SourceDeliverableId = deliverable.G("id"), SendingDisciplineId = civil,
                ReceivingDisciplineId = civil, SendingOwnerId = alex, ReceivingOwnerId = marc, TargetTaskId = task.G("id"), NeededBy = week });
            db.ReviewPackages.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-RV0001", Title = word + " review", CoordinatorId = marc, ProjectDisciplineId = civil });
            db.ChangeNotices.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-CH0001", Title = word + " change", OwnerId = marc, ProjectDisciplineId = civil,
                OldRevisionId = oldRevision.Id, NewRevisionId = newRevision.Id, EffectiveDate = week, AssessmentDueDate = week });
            db.SubmissionPackages.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-SUB0001", Title = word + " permit", Purpose = "Permit", RecipientReference = "City", CoordinatorId = marc,
                MilestoneId = milestone.G("id"), TargetDate = week });
            db.Allocations.Add(new() { ProjectId = project.Id, PersonId = alex, FromDate = week, ThroughDate = week.AddDays(4), PlannedHours = 4 });
            db.DesignBasisEntries.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-B0001", Title = word + " criterion", OwnerId = alex, ProjectDisciplineId = civil });
            db.WorkConstraints.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = task.G("id"), Category = "Handoff",
                Description = word + " constraint", RemovalOwnerId = pm, AffectedOwnerId = alex, NeededBy = week, SourceUrl = "https://example.test/c" });
            db.OutputCommitments.Add(new() { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-WC0001", TargetType = "Task", TargetId = task.G("id"), PerformerId = alex,
                IntendedOutput = word + " output", CompletionCriteria = "Checked", TargetDate = week, WeekStart = week });
            db.PlanningEntries.Add(new() { PersonId = alex, HoursPerWeek = 4, StartWeek = week, EndWeek = week.AddDays(7), Label = word + " leave", Visibility = PlanningVisibility.Published, CreatedBy = marc, LastValidatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });

        var r = await Search(TestData.Admin, word);
        Assert.Equal(["q", "exact", "groups", "counts"], Keys(r));
        Assert.Equal((word, true), (r.S("q"), r["exact"] is null));
        string[] groups = ["projects", "tasks", "deliverables", "milestones", "decisions", "handoffs", "reviews", "changes", "submissions", "allocations", "design-basis", "constraints", "commitments", "comments", "planning", "people"];
        Assert.Equal(groups, Keys(r["groups"])); // the order the groups have always been written in
        Assert.Equal(groups, Keys(r["counts"]));
        Assert.All(groups, g => { var expected = g switch { "tasks" => 2, "allocations" or "people" => 0, _ => 1 }; Assert.Equal((expected, expected), (r["counts"]![g]!.GetValue<int>(), r["groups"]![g]!.AsArray().Count)); }); // an empty group is still present
        JsonNode Row(string g) => r["groups"]![g]![0]!;

        Assert.Equal(["id", "projectNumber", "name", "status", "client", "pm"], Keys(Row("projects")));
        Assert.Equal((project.ProjectNumber, "Active"), (Row("projects").S("projectNumber"), Row("projects").S("status")));
        Assert.NotNull(Row("projects")["pm"]);
        var tasks = r["groups"]!["tasks"]!.AsArray();
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "assignee", "match"], Keys(tasks[0]));
        var named = tasks.Single(x => x!.S("key") == task.S("key"))!; var memo = tasks.Single(x => x!.S("key") == byText.S("key"))!;
        Assert.Equal(("Alex Chen", "2026-10-05", null), (named.S("assignee"), named.S("dueDate"), named["match"]?.GetValue<string>())); // a name match needs no snippet, and the null stays in the row
        Assert.Contains(word, memo.S("match")); Assert.Null(memo["assignee"]); Assert.Null(memo["dueDate"]);
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "owner", "match"], Keys(Row("deliverables")));
        Assert.Equal(("Alex Chen", null), (Row("deliverables").S("owner"), Row("deliverables")["match"]?.GetValue<string>()));
        Assert.Equal(["id", "key", "name", "date", "isComplete", "isCancelled", "projectNumber"], Keys(Row("milestones")));
        Assert.Equal(["id", "key", "name", "status", "requiredByDate", "projectNumber"], Keys(Row("decisions")));
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "owner"], Keys(Row("handoffs")));
        Assert.Equal(("Marc Dubois", "2026-10-05"), (Row("handoffs").S("owner"), Row("handoffs").S("dueDate")));
        Assert.Equal(["id", "key", "name", "status", "projectNumber"], Keys(Row("reviews")));
        Assert.Equal(["id", "key", "name", "status", "projectNumber"], Keys(Row("changes")));
        Assert.Equal(["id", "key", "name", "dueDate", "projectNumber", "owner"], Keys(Row("submissions")));
        Assert.Equal(["id", "key", "name", "projectNumber", "owner"], Keys(Row("design-basis")));
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "owner"], Keys(Row("constraints")));
        Assert.Equal(("Open", word + " constraint"), (Row("constraints").S("status"), Row("constraints").S("name")));
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "owner"], Keys(Row("commitments")));
        Assert.Equal(["id", "itemType", "itemId", "key", "name", "projectNumber", "author", "createdAt", "match"], Keys(Row("comments")));
        Assert.Equal(("Task", task.S("key")), (Row("comments").S("itemType"), Row("comments").S("key")));
        Assert.Equal(["id", "name", "status", "dueDate", "startWeek", "personId"], Keys(Row("planning")));
        Assert.Equal(("Alex Chen · " + word + " leave", "2026-10-12"), (Row("planning").S("name"), Row("planning").S("dueDate")));

        var allocation = (await Search(TestData.Admin, "Alex Chen", "&type=allocations&limit=200"))["groups"]!["allocations"]!.AsArray().Single(x => x!.S("projectNumber") == project.ProjectNumber)!; // matched by the person's name
        Assert.Equal(["id", "name", "status", "dueDate", "projectNumber"], Keys(allocation));
        Assert.Equal(("Alex Chen · Production", "Proposed", "2026-10-09"), (allocation.S("name"), allocation.S("status"), allocation.S("dueDate")));
        var people = (await Search(TestData.Admin, "Alex Chen", "&type=people"))["groups"]!["people"]![0]!;
        Assert.Equal(["id", "displayName", "email", "jobTitle", "isActive", "canViewWork"], Keys(people));
        Assert.Equal((true, true), (people["isActive"]!.GetValue<bool>(), people["canViewWork"]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Exact_type_filter_and_blank_term_keep_their_shapes()
    {
        var word = "zq" + Guid.NewGuid().ToString("N")[..8];
        var project = await d.Project(tweak: b => b["name"] = $"{word} Exact Roads");
        var task = await d.NewTask(project.Id, TestData.Pm, new { name = $"{word} culvert" });
        var number = await Search(TestData.Admin, project.ProjectNumber);
        Assert.Equal(["type", "id", "projectNumber", "key"], Keys(number["exact"]));
        Assert.Equal(("Project", true, project.ProjectNumber, project.ProjectNumber), (number["exact"]!.S("type"), number["exact"]!["id"] is null, number["exact"]!.S("projectNumber"), number["exact"]!.S("key")));
        var key = await Search(TestData.Admin, task.S("key").ToLowerInvariant());
        Assert.Equal(["type", "id", "projectNumber", "key"], Keys(key["exact"]));
        Assert.Equal(("Task", task.G("id"), project.ProjectNumber, task.S("key")), (key["exact"]!.S("type"), key["exact"]!.G("id"), key["exact"]!.S("projectNumber"), key["exact"]!.S("key")));
        Assert.Equal(["tasks"], Keys((await Search(TestData.Admin, word, "&type=tasks&limit=50&page=1"))["groups"]));
        Assert.Equal(["tasks"], Keys((await Search(TestData.Admin, word, "&type=tasks"))["counts"]));
        Assert.Equal(["q", "exact", "groups", "counts"], Keys(await Search(TestData.Admin, word, "&type=nothing")));
        Assert.Empty((await Search(TestData.Admin, word, "&type=nothing"))["groups"]!.AsObject());
        var blank = await Search(TestData.Admin, "  ");
        Assert.Equal("""{"q":"","exact":null,"groups":{},"counts":{}}""", blank.ToJsonString());
    }
}
