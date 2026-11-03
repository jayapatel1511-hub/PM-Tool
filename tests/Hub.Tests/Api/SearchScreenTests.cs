using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// <summary>
/// S46: FR-SCR-79 / AC-SCR-79 filters, counts and directory search;
/// FR-SCR-84 / AC-SCR-84 unchanged flag-off wire and refusal behaviour.
/// </summary>
public sealed class SearchScreenTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public SearchScreenTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value })).Json();
    async Task<JsonNode> Search(string word, string extra = "", string who = TestData.Admin, int expect = 200) =>
        await (await f.As(who).GetAsync($"/api/v1/search?q={Uri.EscapeDataString(word)}{extra}")).Json(expect);
    static string Query(string type, string? status = null, Guid? project = null, Guid? discipline = null) => "&type=" + type
        + (status is null ? "" : "&status=" + Uri.EscapeDataString(status))
        + (project is null ? "" : "&projectId=" + project) + (discipline is null ? "" : "&disciplineId=" + discipline);
    static JsonArray Group(JsonNode result, string type) => result["groups"]![type]!.AsArray();
    static JsonNode Tab(JsonNode result, string type) => result["screen"]!["tabs"]!.AsArray().Single(tab => tab!.S("type") == type)!;
    static JsonNode Legacy(JsonNode result)
    {
        var clone = result.DeepClone(); clone.AsObject().Remove("screen");
        foreach (var group in clone["groups"]!.AsObject()) foreach (var row in group.Value!.AsArray()) row!.AsObject().Remove("person");
        return clone;
    }
    string State() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(), Notices = db.Notifications.Count(), Receipts = db.Idempotency.Count(),
        Projects = db.Projects.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Tasks = db.Tasks.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        People = db.Users.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.JobTitle, row.HomeDisciplineId }).ToArray(),
        Milestones = db.MilestoneStates.OrderBy(row => row.MilestoneId).Select(row => new { row.MilestoneId, row.Status, row.EvaluatedAt }).ToArray(),
        Basis = db.DesignBasisEntries.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.CurrentVersionId }).ToArray(),
        Planning = db.PlanningEntries.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray()
    }));

    [Fact]
    public async Task Eit_and_home_discipline_match_actual_optional_directory_fields_without_granting_person_panel_access()
    {
        var word = "s46" + Guid.NewGuid().ToString("N")[..8]; var alex = d.User(TestData.Alex); var diane = d.User(TestData.Diane);
        var home = new Discipline { Code = word[..10], Name = word + " Home", IsActive = true };
        await f.DbAsync(async db => {
            db.Disciplines.Add(home);
            var user = await db.Users.SingleAsync(user => user.Id == diane);
            user.JobTitle = "EIT " + word; user.HomeDisciplineId = home.Id;
            var self = await db.Users.SingleAsync(user => user.Id == alex);
            self.JobTitle = "EIT " + word; self.HomeDisciplineId = null;
            return await db.SaveChangesAsync();
        });
        await Flag(false);
        Assert.Empty(Group(await Search("EIT " + word, "&type=people", TestData.Alex), "people"));
        var baseline = await Search(TestData.Diane, "&type=people", TestData.Alex);
        Assert.Equal(baseline.ToJsonString(), (await Search(TestData.Diane, "&type=people&projectId=bad&status=bogus&disciplineId=bad", TestData.Alex)).ToJsonString());
        Assert.Empty(Group(await Search(home.Name, "&type=people", TestData.Alex), "people"));
        await Flag(true); var before = State();
        var title = await Search("EIT " + word, "&type=people", TestData.Alex);
        Assert.Equal(2, title["counts"]!.I("people")); Assert.Equal(new[] { alex, diane }.Order(), Group(title, "people").Select(row => row!.G("id")).Order());
        var homeMatch = await Search(home.Name, "&type=people", TestData.Alex);
        Assert.Equal(diane, Assert.Single(Group(homeMatch, "people"))!.G("id"));
        var filtered = await Search("EIT " + word, Query("people", "Active", discipline: home.Id), TestData.Alex);
        var person = Assert.Single(Group(filtered, "people"))!; Assert.Equal(diane, person.G("id"));
        Assert.False(person["canViewWork"]!.GetValue<bool>()); Assert.Equal(diane, person["person"]!.G("id"));
        Assert.False(person.AsObject().ContainsKey("personFacts")); Assert.False(person.AsObject().ContainsKey("certifications"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/people/{diane}/panel")).Json(404);
        Assert.Equal(before, State());
        await f.DbAsync(async db => { (await db.Users.SingleAsync(user => user.Id == diane)).IsActive = false; return await db.SaveChangesAsync(); });
        var inactive = await Search("EIT " + word, Query("people", "Inactive", discipline: home.Id));
        Assert.Equal(diane, Assert.Single(Group(inactive, "people"))!.G("id"));
        Assert.False(Group(inactive, "people")[0]!["isActive"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Task_filters_apply_before_counts_and_paging_keep_legacy_ranking_and_suppress_filtered_exact_jumps()
    {
        var word = "s46task" + Guid.NewGuid().ToString("N")[..8]; var p = await d.Project(); var other = await d.Project();
        var civil = await d.Discipline("Civil"); var electrical = await d.Discipline("Electrical");
        var first = await d.NewTask(p.Id, TestData.Pm, new { name = word + " first", assigneeId = d.User(TestData.Alex) });
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var second = await d.NewTask(p.Id, TestData.Pm, new { name = word + " second", assigneeId = d.User(TestData.Alex) });
        await d.Move(TestData.Alex, second, TaskStatuses.InProgress);
        await d.NewTask(p.Id, TestData.Pm, new { name = word + " electrical", assigneeId = d.User(TestData.Omar) }, "Electrical");
        await d.NewTask(other.Id, TestData.Pm, new { name = word + " another project" });
        await Flag(false); var old = await Search(word, "&type=tasks");
        var ignored = await Search(word, "&type=tasks&projectId=not-a-guid&status=not-a-status&disciplineId=not-a-guid");
        Assert.Equal(old.ToJsonString(), ignored.ToJsonString()); Assert.False(old.AsObject().ContainsKey("screen"));
        var exact = await Search(first.S("key")); Assert.NotNull(exact["exact"]);
        await Flag(true); var before = State();
        Assert.Equal(old.ToJsonString(), Legacy(await Search(word, "&type=tasks")).ToJsonString());
        Assert.Equal(exact.ToJsonString(), Legacy(await Search(first.S("key"))).ToJsonString());
        var selected = await Search(word, Query("tasks", TaskStatuses.InProgress, p.Id, civil));
        Assert.Equal(second.G("id"), Assert.Single(Group(selected, "tasks"))!.G("id")); Assert.Equal(1, selected["counts"]!.I("tasks"));
        var page1 = await Search(word, Query("tasks", project: p.Id, discipline: civil) + "&limit=1&page=1");
        var page2 = await Search(word, Query("tasks", project: p.Id, discipline: civil) + "&limit=1&page=2");
        Assert.Equal(2, page1["counts"]!.I("tasks")); Assert.Equal(2, page2["counts"]!.I("tasks"));
        Assert.NotEqual(Group(page1, "tasks")[0]!.G("id"), Group(page2, "tasks")[0]!.G("id"));
        Assert.Equal(1, (await Search(word, Query("tasks", project: p.Id, discipline: electrical)))["counts"]!.I("tasks"));
        Assert.Equal(0, (await Search(word, Query("tasks", project: Guid.NewGuid())))["counts"]!.I("tasks"));
        Assert.Equal(0, (await Search(word, Query("tasks", discipline: Guid.NewGuid())))["counts"]!.I("tasks"));
        Assert.Null((await Search(first.S("key"), Query("tasks", project: p.Id, discipline: civil)))["exact"]);
        Assert.Equal(before, State());
    }

    [Theory]
    [InlineData("&status=Active")]
    [InlineData("&type=tasks&status=Proposed")]
    [InlineData("&type=comments&status=Open")]
    [InlineData("&type=decisions&disciplineId=11111111-1111-1111-1111-111111111111")]
    [InlineData("&type=people&projectId=11111111-1111-1111-1111-111111111111")]
    [InlineData("&type=submissions&disciplineId=11111111-1111-1111-1111-111111111111")]
    [InlineData("&type=allocations&disciplineId=11111111-1111-1111-1111-111111111111")]
    [InlineData("&type=tasks&projectId=bad")]
    [InlineData("&type=tasks&disciplineId=00000000-0000-0000-0000-000000000000")]
    [InlineData("&type=not-a-tab&status=Active")]
    public async Task Active_filters_refuse_illegal_or_inapplicable_values_without_writes(string query)
    {
        await Flag(true); var before = State(); var error = await Search("validation", query, expect: 400);
        Assert.NotNull(error["errors"]); Assert.Equal(before, State());
    }

    sealed record SearchScreenFixture(Project Project, string Word, Guid Civil, Guid Electrical,
        Dictionary<string, (Guid Id, string Status)> Hits, Guid UnrecordedBasis, Guid NonSelfPlanning);

    async Task<SearchScreenFixture> Kinds()
    {
        var word = "s46kind" + Guid.NewGuid().ToString("N")[..8]; var p = await d.Project(tweak: body => body["name"] = word + " project");
        var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var alex = d.User(TestData.Alex); var marc = d.User(TestData.Marc); var omar = d.User(TestData.Omar); var pm = d.User(TestData.Pm);
        var week = new DateOnly(2026, 9, 21);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = word + " task", assigneeId = alex });
        await d.Move(TestData.Alex, task, TaskStatuses.InProgress);
        async Task<JsonNode> Post(string path, object body) => await (await f.As(TestData.Pm).Post(path, body)).Json(201);
        var root = $"/api/v1/projects/{p.Id}";
        var del = await Post(root + "/deliverables", new { name = word + " deliverable", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = alex, revision = "A" });
        var mile = await Post(root + "/milestones", new { name = word + " milestone", milestoneType = MilestoneType.DesignSubmission,
            date = week, projectDisciplineId = civil });
        var decision = await Post(root + "/decisions", new { subject = word + " decision", description = "Synthetic", ownerUserId = alex,
            requiredByDate = week, impactLevel = Impact.Low, impactDescription = "Synthetic" });
        var target = await d.NewTask(p.Id, TestData.Omar, new { assigneeId = omar }, "Electrical");
        var sourceVersion = f.Db(db => db.Deliverables.Single(row => row.Id == del.G("id")).RowVersion);
        var handoff = await Post(root + "/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), word + " handoff", del.G("id"), sourceVersion,
            "A", "https://example.test/source", electrical, alex, omar, target.G("id"), null, "Synthetic", "Synthetic", week, week, null, null));
        var review = new ReviewPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-RV0001", Title = word + " review", CoordinatorId = marc, ProjectDisciplineId = civil };
        SourceRevision Revision(string value) => new() { ProjectId = p.Id, DeliverableId = del.G("id"), IdentityHash = word + value, SourceKey = word + value,
            Title = word, Revision = value, Url = "https://example.test/" + value };
        var old = Revision("A"); var next = Revision("B");
        var change = new ChangeNotice { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CH0001", Title = word + " change", OwnerId = alex,
            ProjectDisciplineId = civil, OldRevisionId = old.Id, NewRevisionId = next.Id, EffectiveDate = week, AssessmentDueDate = week };
        var submission = new SubmissionPackage { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-SUB0001", Title = word + " submission", CoordinatorId = pm,
            MilestoneId = mile.G("id"), Purpose = "Synthetic", RecipientReference = "Synthetic", TargetDate = week };
        var allocation = new ResourceAllocation { ProjectId = p.Id, PersonId = alex, FromDate = week, ThroughDate = week.AddDays(4), PlannedHours = 4 };
        var basis = new DesignBasisEntry { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-B0001", Title = word + " current basis", OwnerId = alex, ProjectDisciplineId = civil };
        var missingBasis = new DesignBasisEntry { ProjectId = p.Id, Seq = 2, Key = p.ProjectNumber + "-B0002", Title = word + " unrecorded basis", OwnerId = alex, ProjectDisciplineId = civil };
        var constraint = new WorkConstraint { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = task.G("id"),
            Category = "Handoff", Description = word + " constraint", RemovalOwnerId = pm, AffectedOwnerId = alex, NeededBy = week, SourceUrl = "https://example.test/constraint" };
        var commitment = new OutputCommitment { ProjectId = p.Id, Seq = 1, Key = p.ProjectNumber + "-WC0001", TargetType = "Deliverable", TargetId = del.G("id"),
            PerformerId = alex, IntendedOutput = word + " output", CompletionCriteria = "Synthetic", WeekStart = week, TargetDate = week };
        var plan = new PlanningEntry { PersonId = alex, ProjectId = p.Id, ProjectDisciplineId = civil, SourceCategory = PlanningSource.MajorProject,
            HoursPerWeek = 4, StartWeek = week, EndWeek = week, Label = word + " self plan", Visibility = PlanningVisibility.Confirmed, CreatedBy = alex, LastValidatedAt = f.Clock.Now };
        var nonSelfPlan = new PlanningEntry { PersonId = alex, ProjectId = p.Id, ProjectDisciplineId = civil, SourceCategory = PlanningSource.MajorProject,
            HoursPerWeek = 4, StartWeek = week, EndWeek = week, Label = word + " manager plan", Visibility = PlanningVisibility.Confirmed, CreatedBy = pm, LastValidatedAt = f.Clock.Now };
        // Deliberately bounded synthetic read fixtures follow SearchContractTests. They do not claim command-flow verification.
        await f.DbAsync(async db => {
            db.ReviewPackages.Add(review); db.SourceRevisions.AddRange(old, next); db.ChangeNotices.Add(change); db.SubmissionPackages.Add(submission);
            db.Allocations.Add(allocation); db.DesignBasisEntries.AddRange(basis, missingBasis); db.WorkConstraints.Add(constraint); db.OutputCommitments.Add(commitment); db.PlanningEntries.AddRange(plan, nonSelfPlan);
            var user = await db.Users.SingleAsync(user => user.Id == alex); user.DisplayName = word + " Worker"; user.HomeDisciplineId = await d.Discipline("Civil");
            var state = await db.MilestoneStates.SingleOrDefaultAsync(state => state.MilestoneId == mile.G("id"));
            if (state is null) db.MilestoneStates.Add(new MilestoneState { MilestoneId = mile.G("id"), ProjectId = p.Id, Status = MilestoneStatus.AtRisk, EvaluatedAt = f.Clock.Now });
            else state.Status = MilestoneStatus.AtRisk;
            return await db.SaveChangesAsync();
        });
        await f.DbAsync(async db => {
            var version = new DesignBasisVersion { ProjectId = p.Id, EntryId = basis.Id, Number = 1, Scope = "Synthetic", Statement = "Synthetic" };
            db.DesignBasisVersions.Add(version); await db.SaveChangesAsync();
            (await db.DesignBasisEntries.SingleAsync(row => row.Id == basis.Id)).CurrentVersionId = version.Id;
            return await db.SaveChangesAsync();
        });
        return new(p, word, await d.Discipline("Civil"), await d.Discipline("Electrical"), new() {
            ["projects"] = (p.Id, ProjectStatus.Active), ["tasks"] = (task.G("id"), TaskStatuses.InProgress), ["deliverables"] = (del.G("id"), DeliverableStatus.NotStarted),
            ["milestones"] = (mile.G("id"), MilestoneStatus.AtRisk), ["decisions"] = (decision.G("id"), DecisionStatus.Pending), ["handoffs"] = (handoff.G("id"), HandoffStatus.Draft),
            ["reviews"] = (review.Id, ReviewStatus.Draft), ["changes"] = (change.Id, ChangeStatus.Draft), ["submissions"] = (submission.Id, SubmissionStatus.Draft),
            ["allocations"] = (allocation.Id, AllocationStatus.Proposed), ["design-basis"] = (basis.Id, BasisStatus.Proposed), ["constraints"] = (constraint.Id, ConstraintState.Open),
            ["commitments"] = (commitment.Id, CommitmentState.Proposed), ["planning"] = (plan.Id, "Self"), ["people"] = (alex, "Active")
        }, missingBasis.Id, nonSelfPlan.Id);
    }

    [Fact]
    public async Task Every_applicable_tab_uses_its_real_status_and_discipline_including_derived_current_and_target_relations()
    {
        var fixture = await Kinds(); await Flag(true); var before = State();
        foreach (var (type, expected) in fixture.Hits)
        {
            var metadata = await Search("", "&type=" + type); var tab = Tab(metadata, type);
            var project = tab["project"]!.GetValue<bool>() ? fixture.Project.Id : (Guid?)null;
            var discipline = tab["discipline"]!.GetValue<bool>() ? fixture.Civil : (Guid?)null;
            Assert.Contains(expected.Status, tab["statusValues"]!.AsArray().Select(value => value!.GetValue<string>()));
            var result = await Search(fixture.Word, Query(type, expected.Status, project, discipline));
            Assert.Equal(expected.Id, Assert.Single(Group(result, type))!.G("id")); Assert.Equal(1, result["counts"]!.I(type));
        }
        var receiving = await Search(fixture.Word, Query("handoffs", HandoffStatus.Draft, fixture.Project.Id, fixture.Electrical));
        Assert.Equal(fixture.Hits["handoffs"].Id, Assert.Single(Group(receiving, "handoffs"))!.G("id"));
        var basis = await Search(fixture.Word, Query("design-basis", project: fixture.Project.Id)); Assert.Equal(2, basis["counts"]!.I("design-basis"));
        Assert.DoesNotContain(Group(await Search(fixture.Word, Query("design-basis", BasisStatus.Proposed)), "design-basis"), row => row!.G("id") == fixture.UnrecordedBasis);
        var confirmed = await Search(fixture.Word, Query("planning", PlanningVisibility.Confirmed, fixture.Project.Id, fixture.Civil));
        Assert.Equal(fixture.NonSelfPlanning, Assert.Single(Group(confirmed, "planning"))!.G("id"));
        Assert.Equal(1, confirmed["counts"]!.I("planning"));
        Assert.DoesNotContain(Group(confirmed, "planning"), row => row!.G("id") == fixture.Hits["planning"].Id); // confirmed storage still displays Self for its author
        Assert.Equal(0, (await Search(fixture.Word, Query("planning", PlanningVisibility.Published)))["counts"]!.I("planning"));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Normally_completed_then_cancelled_milestone_matches_complete_and_not_cancelled()
    {
        var project = await d.Project(); var word = "s46both" + Guid.NewGuid().ToString("N")[..8];
        var client = f.As(TestData.Pm); var date = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var created = await (await client.Post($"/api/v1/projects/{project.Id}/milestones", new {
            name = word, milestoneType = MilestoneType.DesignSubmission, date, projectDisciplineId = d.ProjectDiscipline(project.Id, "Civil")
        })).Json(201);
        var id = created.G("id");
        var completed = await (await client.Post($"/api/v1/milestones/{id}/complete", new {
            completedDate = date, rowVersion = created.I("rowVersion")
        })).Json();
        await (await client.Post($"/api/v1/milestones/{id}/cancel", new {
            reason = "Synthetic cancellation after completion", rowVersion = completed.I("rowVersion")
        })).Json();
        Assert.True(f.Db(db => db.Milestones.Single(row => row.Id == id).IsComplete));
        Assert.True(f.Db(db => db.Milestones.Single(row => row.Id == id).IsCancelled));
        await Flag(true); var before = State();
        var complete = await Search(word, Query("milestones", MilestoneStatus.Complete, project.Id));
        Assert.Equal(id, Assert.Single(Group(complete, "milestones"))!.G("id")); Assert.Equal(1, complete["counts"]!.I("milestones"));
        var cancelled = await Search(word, Query("milestones", MilestoneStatus.Cancelled, project.Id));
        Assert.Empty(Group(cancelled, "milestones")); Assert.Equal(0, cancelled["counts"]!.I("milestones"));
        foreach (var status in new[] { MilestoneStatus.AtRisk, MilestoneStatus.OnTrack, MilestoneStatus.Overdue })
            Assert.Empty(Group(await Search(word, Query("milestones", status, project.Id)), "milestones"));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Hidden_and_archived_projects_never_gain_scope_through_controls_or_option_metadata()
    {
        var word = "s46scope" + Guid.NewGuid().ToString("N")[..8]; var visible = await d.Project();
        var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>()); var archived = await d.Project();
        await d.NewTask(visible.Id, TestData.Pm, new { name = word + " visible" });
        await d.NewTask(hidden.Id, TestData.Pm, new { name = word + " hidden" });
        await d.NewTask(archived.Id, TestData.Pm, new { name = word + " archived" });
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var hiddenVersion = f.Db(db => db.Projects.Single(project => project.Id == hidden.Id).RowVersion);
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, hiddenVersion)).Json();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(project => project.Id == archived.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        await Flag(true); var before = State();
        var own = await Search(word, "&type=tasks", TestData.Alex); Assert.Equal(1, own["counts"]!.I("tasks"));
        Assert.DoesNotContain(own["screen"]!["projects"]!.AsArray(), project => project!.G("id") == hidden.Id || project.G("id") == archived.Id);
        var hiddenFilter = await Search(word, Query("tasks", project: hidden.Id), TestData.Alex); Assert.Equal(0, hiddenFilter["counts"]!.I("tasks"));
        Assert.DoesNotContain(hidden.Name, hiddenFilter.ToJsonString());
        Assert.Equal(0, (await Search(word, Query("tasks", project: archived.Id), TestData.Alex))["counts"]!.I("tasks"));
        var included = await Search(word, Query("tasks", project: archived.Id) + "&includeArchived=true", TestData.Alex);
        Assert.Equal(1, included["counts"]!.I("tasks")); Assert.Contains(included["screen"]!["projects"]!.AsArray(), project => project!.G("id") == archived.Id);
        Assert.DoesNotContain(included["screen"]!["projects"]!.AsArray(), project => project!.G("id") == hidden.Id); Assert.Equal(before, State());
    }

    [Fact]
    public async Task Metadata_is_bounded_and_empty_search_stays_empty_without_persisting_recents_or_other_writes()
    {
        var client = await d.Client(); var office = await d.Office(); var pm = d.User(TestData.Pm);
        // Reference/read-cohort boundary fixture; not 205 real create workflows or a performance claim.
        await f.DbAsync(async db => {
            db.Disciplines.AddRange(Enumerable.Range(0, 205).Select(index => new Discipline { Code = "S46" + index.ToString("D3"), Name = "S46 option " + index.ToString("D3"), IsActive = true }));
            db.Projects.AddRange(Enumerable.Range(0, 205).Select(index => new Project { ProjectNumber = "S46-" + Guid.NewGuid().ToString("N"),
                Name = "S46 option " + index, ClientId = client, OfficeId = office, ProjectManagerId = pm, Status = ProjectStatus.Setup }));
            return await db.SaveChangesAsync();
        });
        await Flag(false); var old = await Search(""); Assert.Equal(["q", "exact", "groups", "counts"], old.AsObject().Select(value => value.Key));
        await Flag(true); var before = State(); var result = await Search("");
        Assert.Empty(result["groups"]!.AsObject()); Assert.Empty(result["counts"]!.AsObject()); Assert.Null(result["exact"]);
        Assert.Equal(200, result["screen"]!["projects"]!.AsArray().Count); Assert.True(result["screen"]!["projectsTruncated"]!.GetValue<bool>());
        Assert.Equal(200, result["screen"]!["disciplines"]!.AsArray().Count); Assert.True(result["screen"]!["disciplinesTruncated"]!.GetValue<bool>());
        Assert.Equal(SearchEndpoints.Groups, result["screen"]!["tabs"]!.AsArray().Select(tab => tab!.S("type")));
        Assert.Equal(["projectId", "status", "disciplineId"], result["screen"]!["applied"]!.AsObject().Select(value => value.Key));
        Assert.False(Tab(result, "comments")["status"]!.GetValue<bool>()); Assert.False(Tab(result, "people")["project"]!.GetValue<bool>());
        Assert.Equal(before, State());
    }
}
