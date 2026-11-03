using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Search adds presentation metadata to its existing authorised pages; it grants no person-panel access.
public sealed class SearchPersonReferencesTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public SearchPersonReferencesTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value })).Json();
    async Task<JsonNode> Search(string word, string who = TestData.Admin, string extra = "") =>
        await (await f.As(who).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(word)}{extra}")).Json();
    async Task<JsonNode> Post(string path, object body, string who = TestData.Pm) => await (await f.As(who).Post(path, body)).Json(201);
    static JsonNode Hit(JsonNode result, string group, Guid id) => result["groups"]![group]!.AsArray().Single(row => row!.G("id") == id)!;
    static string[] Keys(JsonNode node) => node.AsObject().Select(pair => pair.Key).ToArray();
    static JsonNode WithoutReferences(JsonNode result)
    {
        var copy = result.DeepClone();
        // S46 separately checks its new metadata; this comparison protects the legacy search projection.
        copy.AsObject().Remove("screen");
        foreach (var group in copy["groups"]!.AsObject())
            foreach (var hit in group.Value!.AsArray()) hit!.AsObject().Remove("person");
        return copy;
    }
    string ReadState() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(), Comments = db.Comments.Count(),
        Projects = db.Projects.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.ProjectManagerId }).ToArray(),
        Tasks = db.Tasks.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.AssigneeId }).ToArray(),
        Deliverables = db.Deliverables.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.OwnerId }).ToArray(),
        Handoffs = db.Handoffs.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.ReceivingOwnerId }).ToArray(),
        Submissions = db.SubmissionPackages.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.CoordinatorId }).ToArray(),
        Allocations = db.Allocations.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.PersonId }).ToArray(),
        Basis = db.DesignBasisEntries.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.OwnerId }).ToArray(),
        Constraints = db.WorkConstraints.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.RemovalOwnerId }).ToArray(),
        Commitments = db.OutputCommitments.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.PerformerId }).ToArray(),
        Planning = db.PlanningEntries.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.PersonId }).ToArray(),
        Users = db.Users.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.DisplayName, row.IsActive }).ToArray()
    }));

    sealed record ReferenceCase(string Group, Guid ItemId, Guid UserId, string Role);
    sealed record Fixture(string Word, ReferenceCase[] References, (string Group, Guid Id)[] Unlinked);

    async Task<Fixture> AllKinds()
    {
        var word = "searchperson" + Guid.NewGuid().ToString("N")[..8];
        var p = await d.Project(tweak: body => body["name"] = word + " project"); var root = $"/api/v1/projects/{p.Id}";
        var civil = d.ProjectDiscipline(p.Id, "Civil"); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill);
        var marc = d.User(TestData.Marc); var pm = d.User(TestData.Pm); var week = new DateOnly(2026, 9, 21);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = word + " task A", assigneeId = alex });
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var other = await d.NewTask(p.Id, TestData.Pm, new { name = word + " task B", assigneeId = jill });
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var unassigned = await d.NewTask(p.Id, TestData.Pm, new { name = word + " unassigned", assigneeId = (Guid?)null });
        f.Clock.Now = f.Clock.Now.AddMinutes(1); // the later comment must not tie another task's activity sort
        var del = await Post(root + "/deliverables", new { name = word + " deliverable", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = alex });
        var milestone = await Post(root + "/milestones", new { name = word + " milestone", milestoneType = MilestoneType.DesignSubmission, date = week });
        var party = await Post(root + "/external-parties", new { name = word + " Same name", isClient = true });
        var decision = await Post(root + "/decisions", new { subject = word + " external decision", description = "Synthetic",
            ownerExternalPartyId = party.G("id"), requiredByDate = week, impactLevel = Impact.Low, impactDescription = "Synthetic" });
        var comment = await Post($"/api/v1/items/Task/{task.G("id")}/comments", new { body = word + " discussion mentions Priya, but Marc is the author." }, TestData.Marc);
        var handoff = new Handoff { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-H0001", Title = word + " handoff",
            SourceDeliverableId = del.G("id"), SendingDisciplineId = civil, ReceivingDisciplineId = civil, SendingOwnerId = alex,
            ReceivingOwnerId = marc, TargetTaskId = task.G("id"), NeededBy = week };
        var review = new ReviewPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-RV0001", Title = word + " review", CoordinatorId = marc, ProjectDisciplineId = civil };
        SourceRevision Revision(string revision) => new() { ProjectId = p.Id, DeliverableId = del.G("id"), SourceKey = word + revision,
            IdentityHash = word + revision, Title = word, Revision = revision, Url = "https://example.test/" + revision };
        var old = Revision("A"); var next = Revision("B");
        var change = new ChangeNotice { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CH0001", Title = word + " change", OwnerId = marc,
            ProjectDisciplineId = civil, OldRevisionId = old.Id, NewRevisionId = next.Id, EffectiveDate = week, AssessmentDueDate = week };
        var submission = new SubmissionPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-SUB0001", Title = word + " submission",
            Purpose = "Permit", RecipientReference = "Synthetic", CoordinatorId = marc, MilestoneId = milestone.G("id"), TargetDate = week };
        var allocation = new ResourceAllocation { ProjectId = p.Id, PersonId = alex, FromDate = week, ThroughDate = week.AddDays(4), PlannedHours = 4 };
        var basis = new DesignBasisEntry { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-B0001", Title = word + " basis", OwnerId = alex, ProjectDisciplineId = civil };
        var constraint = new WorkConstraint { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = task.G("id"),
            Category = "Handoff", Description = word + " constraint", RemovalOwnerId = pm, AffectedOwnerId = alex, NeededBy = week, SourceUrl = "https://example.test/constraint" };
        var commitment = new OutputCommitment { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-WC0001", TargetType = "Task", TargetId = task.G("id"),
            PerformerId = alex, IntendedOutput = word + " output", CompletionCriteria = "Synthetic", TargetDate = week, WeekStart = week };
        var planning = new PlanningEntry { PersonId = alex, HoursPerWeek = 4, StartWeek = week, EndWeek = week.AddDays(7),
            Label = word + " planning", Visibility = PlanningVisibility.Published, CreatedBy = marc, LastValidatedAt = f.Clock.Now };
        var unowned = new Deliverable { ProjectId = p.Id, Seq = 2, Key = p.ProjectNumber + "-D0002", Name = word + " unowned",
            ProjectDisciplineId = civil, DeliverableTypeId = await d.DeliverableType() };
        // Isolated synthetic search fixtures follow the existing SearchContractTests pattern.
        // Their recipient FKs are real Users; no fabricated User, personal state or permission bypass is asserted.
        await f.DbAsync(async db => {
            db.SourceRevisions.AddRange(old, next); db.Handoffs.Add(handoff); db.ReviewPackages.Add(review); db.ChangeNotices.Add(change);
            db.SubmissionPackages.Add(submission); db.Allocations.Add(allocation); db.DesignBasisEntries.Add(basis);
            db.WorkConstraints.Add(constraint); db.OutputCommitments.Add(commitment); db.PlanningEntries.Add(planning); db.Deliverables.Add(unowned);
            var users = await db.Users.Where(user => user.Id == alex || user.Id == jill).ToArrayAsync();
            foreach (var user in users) user.DisplayName = word + " Same name";
            users.Single(user => user.Id == jill).IsActive = false;
            return await db.SaveChangesAsync();
        });
        return new(word, [new("projects", p.Id, pm, "pm"), new("tasks", task.G("id"), alex, "assignee"), new("tasks", other.G("id"), jill, "assignee"),
            new("deliverables", del.G("id"), alex, "owner"), new("handoffs", handoff.Id, marc, "receivingOwner"),
            new("submissions", submission.Id, marc, "coordinator"), new("allocations", allocation.Id, alex, "person"), new("design-basis", basis.Id, alex, "owner"),
            new("constraints", constraint.Id, pm, "removalOwner"), new("commitments", commitment.Id, alex, "performer"), new("comments", comment.G("id"), marc, "author"),
            new("planning", planning.Id, alex, "person"), new("people", alex, alex, "person"), new("people", jill, jill, "person")],
            [("tasks", unassigned.G("id")), ("deliverables", unowned.Id), ("milestones", milestone.G("id")), ("decisions", decision.G("id")), ("reviews", review.Id), ("changes", change.Id)]);
    }

    [Fact]
    public async Task Every_prescribed_fk_is_enriched_and_stripping_metadata_preserves_exact_legacy_payload_and_order()
    {
        var fixture = await AllKinds(); await Flag(false);
        var before = ReadState(); var dark = await Search(fixture.Word);
        foreach (var group in dark["groups"]!.AsObject())
            foreach (var hit in group.Value!.AsArray()) Assert.False(hit!.AsObject().ContainsKey("person"));
        Assert.Equal(before, ReadState());
        Assert.Equal(["q", "exact", "groups", "counts"], Keys(dark));
        var task = fixture.References.First(item => item.Group == "tasks");
        Assert.Equal(["id", "key", "name", "status", "dueDate", "projectNumber", "assignee", "match"], Keys(Hit(dark, "tasks", task.ItemId)));
        await Flag(true); before = ReadState(); var enabled = await Search(fixture.Word);
        foreach (var expected in fixture.References)
        {
            var row = Hit(enabled, expected.Group, expected.ItemId); var person = row["person"]!;
            Assert.Equal(["id", "displayName", "isActive", "role"], Keys(person));
            Assert.Equal(expected.UserId, person.G("id")); Assert.Equal(expected.Role, person.S("role"));
            var user = f.Db(db => db.Users.AsNoTracking().Single(user => user.Id == expected.UserId));
            Assert.Equal(user.DisplayName, person.S("displayName")); Assert.Equal(user.IsActive, person["isActive"]!.GetValue<bool>());
            if (expected.Group != "people") Assert.NotEqual(expected.ItemId, person.G("id"));
            if (expected.Group == "allocations")
            {
                Assert.EndsWith(" · " + AllocationPurpose.Production, row.S("name"));
                Assert.NotEqual(row.S("name"), person.S("displayName"));
            }
            Assert.False(row.AsObject().ContainsKey("personUserId"));
        }
        foreach (var (group, id) in fixture.Unlinked) Assert.False(Hit(enabled, group, id).AsObject().ContainsKey("person"));
        var sameNames = enabled["groups"]!["tasks"]!.AsArray().Where(row => row!["person"] is not null).Select(row => row!["person"]!).ToArray();
        Assert.Equal(2, sameNames.Length); Assert.Single(sameNames.Select(person => person.S("displayName")).Distinct());
        Assert.Equal(2, sameNames.Select(person => person.G("id")).Distinct().Count()); Assert.Contains(sameNames, person => !person["isActive"]!.GetValue<bool>());
        Assert.Equal(dark.ToJsonString(), WithoutReferences(enabled).ToJsonString()); Assert.Equal(before, ReadState());
        await Flag(false); before = ReadState(); Assert.Equal(dark.ToJsonString(), (await Search(fixture.Word)).ToJsonString()); Assert.Equal(before, ReadState());
    }

    [Fact]
    public async Task Paging_type_filters_exact_key_and_empty_search_keep_their_existing_results()
    {
        var fixture = await AllKinds(); var task = fixture.References.First(item => item.Group == "tasks");
        var key = f.Db(db => db.Tasks.Single(row => row.Id == task.ItemId).Key);
        await Flag(false);
        var first = await Search(fixture.Word, extra: "&type=tasks&limit=1&page=1");
        var second = await Search(fixture.Word, extra: "&type=tasks&limit=1&page=2");
        var exact = await Search(key); var empty = await Search("");
        Assert.NotEqual(first["groups"]!["tasks"]![0]!.G("id"), second["groups"]!["tasks"]![0]!.G("id"));
        Assert.Equal(3, first["counts"]!.I("tasks")); Assert.Single(first["groups"]!.AsObject());
        await Flag(true); var before = ReadState();
        Assert.Equal(first.ToJsonString(), WithoutReferences(await Search(fixture.Word, extra: "&type=tasks&limit=1&page=1")).ToJsonString());
        Assert.Equal(second.ToJsonString(), WithoutReferences(await Search(fixture.Word, extra: "&type=tasks&limit=1&page=2")).ToJsonString());
        Assert.Equal(exact.ToJsonString(), WithoutReferences(await Search(key)).ToJsonString());
        Assert.Equal(empty.ToJsonString(), WithoutReferences(await Search("")).ToJsonString()); Assert.Equal(before, ReadState());
    }

    [Fact]
    public async Task Restricted_and_planning_allocation_scopes_stay_narrow_and_directory_metadata_grants_no_panel_access()
    {
        var word = "searchscope" + Guid.NewGuid().ToString("N")[..8]; var diane = d.User(TestData.Diane);
        var hidden = await d.Project(tweak: body => { body["name"] = word + " restricted"; body["members"] = Array.Empty<object>(); });
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, hidden.RowVersion)).Json();
        await d.NewTask(hidden.Id, TestData.Pm, new { name = word + " hidden task", assigneeId = diane });
        // Real FKs in deliberately private synthetic records; GET must keep the original visibility predicates.
        await f.DbAsync(async db => {
            db.Allocations.Add(new ResourceAllocation { ProjectId = hidden.Id, PersonId = diane, FromDate = new(2026, 9, 21), ThroughDate = new(2026, 9, 25), PlannedHours = 4 });
            db.PlanningEntries.Add(new PlanningEntry { PersonId = diane, ProjectId = hidden.Id, SourceCategory = PlanningSource.MajorProject, Label = word + " private plan", HoursPerWeek = 4,
                StartWeek = new(2026, 9, 21), EndWeek = new(2026, 9, 28), Visibility = PlanningVisibility.Published, CreatedBy = d.User(TestData.Pm), LastValidatedAt = f.Clock.Now });
            (await db.Users.SingleAsync(user => user.Id == diane)).DisplayName = word + " Directory person";
            return await db.SaveChangesAsync();
        });
        await Flag(false); var dark = await Search(word, TestData.Alex);
        Assert.Empty(dark["groups"]!["projects"]!.AsArray()); Assert.Empty(dark["groups"]!["tasks"]!.AsArray());
        Assert.Empty(dark["groups"]!["allocations"]!.AsArray()); Assert.Empty(dark["groups"]!["planning"]!.AsArray());
        Assert.False(Hit(dark, "people", diane)["canViewWork"]!.GetValue<bool>());
        await Flag(true); var before = ReadState(); var enabled = await Search(word, TestData.Alex);
        Assert.Equal(dark.ToJsonString(), WithoutReferences(enabled).ToJsonString());
        Assert.Equal(diane, Hit(enabled, "people", diane)["person"]!.G("id"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/people/{diane}/panel")).Json(404);
        var admin = await Search(word);
        Assert.NotEmpty(admin["groups"]!["projects"]!.AsArray()); Assert.NotEmpty(admin["groups"]!["tasks"]!.AsArray());
        Assert.NotEmpty(admin["groups"]!["allocations"]!.AsArray()); Assert.NotEmpty(admin["groups"]!["planning"]!.AsArray());
        Assert.Equal(before, ReadState());
    }
}
