using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S27 plus the Home/Staffing planner read bridges: current scope and exact inputs, with dark selectors ignored.
public sealed class CapacityPlannerHeaderTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Week = new(2026, 11, 9);
    public CapacityPlannerHeaderTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();
    async Task<Guid> Person(string name, Guid? discipline = null, Guid? supervisor = null, decimal capacity = 40)
    {
        await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json();
        return await f.DbAsync(async db => {
            var person = new AppUser { DisplayName = name, Email = $"planner-header-{Guid.NewGuid():N}@hub.test", HomeDisciplineId = discipline,
                SupervisorId = supervisor ?? d.User(TestData.Sam), WeeklyCapacityHours = capacity };
            db.Users.Add(person); await db.SaveChangesAsync(); return person.Id;
        });
    }
    async Task Plan(Guid person, decimal hours, Guid? project = null, string? creator = null, string visibility = PlanningVisibility.Draft)
    {
        await f.DbAsync(async db => {
            db.PlanningEntries.Add(new PlanningEntry { PersonId = person, HoursPerWeek = hours, StartWeek = Week, EndWeek = Week,
                ProjectId = project, SourceCategory = project == null ? PlanningSource.Other : PlanningSource.MajorProject,
                Label = "Synthetic header plan", Confidence = PlanningConfidence.Expected, Visibility = visibility,
                CreatedBy = d.User(creator ?? TestData.Sam), LastValidatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
    }
    async Task<JsonNode> Milestone(Guid project, string name, DateOnly? date)
    {
        var created = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/milestones", new { name, milestoneType = MilestoneType.Other, date = date ?? Week })).Json(201);
        if (date is null)
        {
            // Copied/template and legacy structures may contain an undated milestone;
            // the normal creation command correctly requires a date.
            await f.DbAsync(async db => {
                (await db.Milestones.SingleAsync(m => m.Id == created.G("id"))).Date = null;
                return await db.SaveChangesAsync();
            });
        }
        return created;
    }
    async Task<JsonNode> Deliverable(Guid project, string name, DateOnly? due)
        => await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", new { name, projectDisciplineId = d.ProjectDiscipline(project, "Civil"), deliverableTypeId = await d.DeliverableType(), dueDate = due, requiresReview = false })).Json(201);
    string Grid(string extra = "") => $"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=2{extra}";

    [Fact]
    public async Task Planner_deadline_headers_use_only_visible_linked_projects_exact_dates_and_an_undated_tray()
    {
        var p = await d.Project(); var other = await d.Project(); var hidden = await d.Project(); var privateOnly = await d.Project(); var person = await Person("Synthetic scoped planner");
        await Plan(person, 8, p.Id);
        // These are real project links in the requested person/week, not merely unrelated secret records.
        // The restricted Published plan must mark the row partial; another owner's Private draft must affect nothing.
        await Plan(person, 8, hidden.Id, creator: TestData.Admin, visibility: PlanningVisibility.Published);
        await Plan(person, 16, privateOnly.Id, creator: TestData.Admin);
        await d.NewTask(other.Id, TestData.Pm, new { assigneeId = person, estimatedHours = 4m });
        var milestone = await Milestone(p.Id, "Visible milestone", Week.AddDays(1));
        var a = await Deliverable(p.Id, "Visible drawing A", Week.AddDays(2));
        var b = await Deliverable(p.Id, "Visible drawing B", Week.AddDays(3));
        var undated = await Deliverable(other.Id, "Undated assigned-work project drawing", null);
        var secret = await Deliverable(hidden.Id, "SECRET HEADER DRAWING", Week.AddDays(2));
        var excluded = new[] {
            secret,
            await Deliverable(hidden.Id, "SECRET HEADER UNDATED DRAWING", null),
            await Milestone(hidden.Id, "SECRET HEADER MILESTONE", Week.AddDays(1)),
            await Milestone(hidden.Id, "SECRET HEADER UNDATED MILESTONE", null),
            await Deliverable(privateOnly.Id, "PRIVATE-ONLY HEADER DRAWING", Week.AddDays(2)),
            await Deliverable(privateOnly.Id, "PRIVATE-ONLY HEADER UNDATED DRAWING", null),
            await Milestone(privateOnly.Id, "PRIVATE-ONLY HEADER MILESTONE", Week.AddDays(1)),
            await Milestone(privateOnly.Id, "PRIVATE-ONLY HEADER UNDATED MILESTONE", null),
        };
        var terminal = await Deliverable(p.Id, "Issued drawing", Week.AddDays(1));
        await f.DbAsync(async db => {
            (await db.Projects.SingleAsync(project => project.Id == hidden.Id)).Visibility = Visibility.Restricted;
            (await db.Deliverables.SingleAsync(deliverable => deliverable.Id == terminal.G("id"))).Status = DeliverableStatus.Issued;
            return await db.SaveChangesAsync();
        });
        await Flag("capacity_screens", false); var off = await (await f.As(TestData.Sam).GetAsync(Grid($"&personId={person}"))).Json();
        Assert.Null(off["deadlines"]);
        await Flag("capacity_screens", true); var on = await (await f.As(TestData.Sam).GetAsync(Grid($"&personId={person}"))).Json();
        var context = on["deadlines"]!;
        var personRow = Assert.Single(on["people"]!.AsArray())!;
        Assert.True(personRow["partial"]!.GetValue<bool>(), "the actual restricted Published plan was detected without returning it");
        Assert.Equal(8m, personRow["cells"]![0]!["expected"]!.GetValue<decimal>());
        Assert.Single(personRow["entries"]!.AsArray());
        Assert.Equal(new[] { milestone.G("id"), a.G("id"), b.G("id") }.Order(), context["dated"]!.AsArray().Select(row => row!.G("id")).Order());
        Assert.Equal("2026-11-10", context["dated"]!.AsArray().Single(row => row!.G("id") == milestone.G("id"))!.S("date"));
        Assert.Equal(undated.G("id"), Assert.Single(context["undated"]!.AsArray())!.G("id"));
        Assert.All(excluded, sentinel => Assert.DoesNotContain(sentinel.G("id").ToString(), on.ToJsonString()));
        Assert.DoesNotContain("SECRET HEADER", on.ToJsonString()); Assert.DoesNotContain("PRIVATE-ONLY HEADER", on.ToJsonString());
        Assert.DoesNotContain(hidden.Id.ToString(), on.ToJsonString()); Assert.DoesNotContain(privateOnly.Id.ToString(), on.ToJsonString());
        var legacy = on.DeepClone(); legacy.AsObject().Remove("deadlines"); Assert.Equal(off.ToJsonString(), legacy.ToJsonString());
        var narrowed = await (await f.As(TestData.Sam).GetAsync(Grid($"&personId={person}&projectId={p.Id}"))).Json();
        Assert.Empty(narrowed["deadlines"]!["undated"]!.AsArray());
        Assert.All(narrowed["deadlines"]!["dated"]!.AsArray(), row => Assert.Equal(p.Id, row!.G("projectId")));
    }

    [Fact]
    public async Task Allocation_deadline_week_strip_is_dark_project_authorized_and_keeps_exact_record_ids()
    {
        var project = await d.Project(); var milestone = await Milestone(project.Id, "Project deadline", Week);
        var undated = await Deliverable(project.Id, "No date drawing", null);
        var path = $"/api/v1/projects/{project.Id}/allocations/deadlines?from={Week:yyyy-MM-dd}&weeks=2";
        await Flag("capacity_screens", false); Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync(path)).StatusCode);
        await Flag("capacity_screens", true); var result = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Equal(2, result["weeks"]!.AsArray().Count);
        Assert.Equal(milestone.G("id"), Assert.Single(result["dated"]!.AsArray())!.G("id"));
        Assert.Equal(undated.G("id"), Assert.Single(result["undated"]!.AsArray())!.G("id"));
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).GetAsync(path.Replace("&weeks=2", "&weeks=27"))).StatusCode);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Team_project_selector_handles_more_than_50_people_and_keeps_their_other_project_plans()
    {
        var team = await d.Project(); var work = await d.Project(); var sam = d.User(TestData.Sam);
        var expected = await f.DbAsync(async db => {
            var people = Enumerable.Range(0, 51).Select(index => new AppUser { Email = $"team-filter-{Guid.NewGuid():N}@hub.test", DisplayName = $"Synthetic team {index:D2}", SupervisorId = sam, WeeklyCapacityHours = 40 }).ToArray();
            db.Users.AddRange(people); await db.SaveChangesAsync();
            foreach (var person in people)
            {
                db.ProjectMembers.Add(new ProjectMember { ProjectId = team.Id, UserId = person.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
                db.PlanningEntries.Add(new PlanningEntry { PersonId = person.Id, ProjectId = work.Id, SourceCategory = PlanningSource.MajorProject,
                    Label = "Other project counted hours", HoursPerWeek = 48, StartWeek = Week, EndWeek = Week,
                    Confidence = PlanningConfidence.Expected, Visibility = PlanningVisibility.Draft, CreatedBy = sam, LastValidatedAt = f.Clock.GetUtcNow() });
            }
            await db.SaveChangesAsync(); return people.Select(person => person.Id).ToArray();
        });
        var outsider = await Person("Not a team member"); await Plan(outsider, 48);
        var lightTeamMember = await Person("Light team plan"); await Plan(lightTeamMember, 4);
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = team.Id, UserId = lightTeamMember, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        await Flag("project_screens", false);
        var normal = await (await f.As(TestData.Sam).GetAsync(Grid("&indicator=OverPlanned"))).Json();
        var ignored = await (await f.As(TestData.Sam).GetAsync(Grid($"&indicator=OverPlanned&teamProjectId={Guid.NewGuid()}"))).Json();
        Assert.Equal(normal.ToJsonString(), ignored.ToJsonString());
        await Flag("project_screens", true);
        var result = await (await f.As(TestData.Sam).GetAsync(Grid($"&indicator=OverPlanned&teamProjectId={team.Id}"))).Json();
        Assert.Equal(expected.Order(), result["people"]!.AsArray().Select(person => person!.G("id")).Order());
        Assert.All(result["people"]!.AsArray(), person => { Assert.Equal(48m, person!["cells"]![0]!["expected"]!.GetValue<decimal>()); Assert.Equal(work.Id, Assert.Single(person["entries"]!.AsArray())!.G("projectId")); });
        Assert.DoesNotContain(outsider.ToString(), result.ToJsonString());
        var entries = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries?from={Week:yyyy-MM-dd}&weeks=2&indicator=OverPlanned&teamProjectId={team.Id}&pageSize=200")).Json();
        Assert.Equal(51, entries.I("totalCount"));
        Assert.DoesNotContain(outsider.ToString(), entries.ToJsonString());
    }

    [Fact]
    public async Task Team_selector_does_not_widen_own_scope_or_expose_removed_inactive_or_inaccessible_members()
    {
        await Flag("project_screens", true); var project = await d.Project();
        var removed = await Person("Removed synthetic"); var inactive = await Person("Inactive synthetic"); var outside = await Person("Unscoped synthetic", supervisor: d.User(TestData.Lena));
        await f.DbAsync(async db => {
            foreach (var person in new[] { removed, inactive, outside }) db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = person, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow(), RemovedAt = person == removed ? f.Clock.GetUtcNow() : null });
            (await db.Users.SingleAsync(person => person.Id == inactive)).IsActive = false;
            return await db.SaveChangesAsync();
        });
        var sam = await (await f.As(TestData.Sam).GetAsync(Grid($"&teamProjectId={project.Id}"))).Json();
        Assert.DoesNotContain(sam["people"]!.AsArray(), person => new[] { removed, inactive, outside }.Contains(person!.G("id")));
        var alex = d.User(TestData.Alex);
        var own = await (await f.As(TestData.Alex).GetAsync(Grid($"&teamProjectId={project.Id}&personId={alex}"))).Json();
        Assert.Equal(alex, Assert.Single(own["people"]!.AsArray())!.G("id"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync(Grid($"&teamProjectId={project.Id}&personId={d.User(TestData.Diane)}"))).StatusCode);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync(Grid($"&teamProjectId={project.Id}"))).StatusCode);
    }

    [Fact]
    public async Task Home_above_100_cohort_is_exact_distinct_from_configured_indicator_and_ignores_provisional_capacity()
    {
        var civil = await d.Discipline("Civil"); var above = await Person("Above literal 100", civil); var zero = await Person("Zero capacity with plans", civil, capacity: 0);
        var provisional = await Person("Provisional capacity", civil); var noDiscipline = await Person("No home discipline");
        await Plan(above, 40.5m); await Plan(zero, 4); await Plan(provisional, 44); await Plan(noDiscipline, 44);
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{provisional}/availability/{Week:yyyy-MM-dd}", new AllocationEndpoints.AvailabilityBody(0, 0, AvailabilityCategory.Unavailable))).Json();
        await Flag("home_overview", false);
        var normal = await (await f.As(TestData.Sam).GetAsync(Grid())).Json();
        var ignored = await (await f.As(TestData.Sam).GetAsync(Grid($"&indicator=Above100&homeDisciplineId={Guid.NewGuid()}"))).Json();
        Assert.Equal(normal.ToJsonString(), ignored.ToJsonString());
        await Flag("home_overview", true);
        var result = await (await f.As(TestData.Sam).GetAsync(Grid($"&indicator=Above100&homeDisciplineId={civil}"))).Json();
        Assert.Equal(new[] { above, zero }.Order(), result["people"]!.AsArray().Select(person => person!.G("id")).Order());
        var literal = result["people"]!.AsArray().Single(person => person!.G("id") == above)!;
        Assert.False(literal["cells"]![0]!["overPlanned"]!.GetValue<bool>()); // 40.5/40 is above 100 but below the canonical 105 threshold
        var noHome = await (await f.As(TestData.Sam).GetAsync(Grid($"&indicator=Above100&homeDisciplineId={Guid.Empty}"))).Json();
        Assert.Contains(noHome["people"]!.AsArray(), person => person!.G("id") == noDiscipline);
        Assert.DoesNotContain(noHome["people"]!.AsArray(), person => person!.G("id") == above);
        var list = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries?from={Week:yyyy-MM-dd}&weeks=1&indicator=Above100&homeDisciplineId={civil}")).Json();
        Assert.Equal(new[] { above, zero }.Order(), list["items"]!.AsArray().Select(entry => entry!.G("personId")).Distinct().Order());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correction_grid_and_scoped_entry_reads_keep_one_durable_audit_and_never_leak_without_reason(bool capacityScreens)
    {
        await Flag("capacity_screens", capacityScreens);
        var civil = await d.Discipline("Civil"); var project = await d.Project(); var person = await Person("Correction audit subject", civil);
        await f.DbAsync(async db => {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = person, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        var draft = await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = person, hoursPerWeek = 48m,
            startWeek = Week, endWeek = Week, label = "PRIVATE AUDIT REGRESSION DRAFT", sourceCategory = PlanningSource.Other,
            confidence = PlanningConfidence.Expected })).Json(201);
        var draftId = draft.G("id");
        int Audits() => f.Db(db => db.ActivityLog.Count(entry => entry.ItemType == "PlanningDraftAccess"));
        var before = Audits();
        var path = Grid($"&personId={person}");
        var ordinary = await (await f.As(TestData.Admin).GetAsync(path)).Json();
        Assert.DoesNotContain(draftId.ToString(), ordinary.ToJsonString());
        Assert.DoesNotContain("PRIVATE AUDIT REGRESSION DRAFT", ordinary.ToJsonString());
        Assert.Equal(before, Audits());
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).GetAsync(path + "&draftAccessReason=no")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).GetAsync(path + "&draftAccessReason=forbidden%20read")).StatusCode);
        Assert.Equal(before, Audits());

        var corrected = await (await f.As(TestData.Admin).GetAsync(path + "&draftAccessReason=grid%20audit%20regression")).Json();
        Assert.Equal(draftId, Assert.Single(Assert.Single(corrected["people"]!.AsArray())!["entries"]!.AsArray())!.G("id"));
        Assert.Equal(before + 1, Audits()); // a fresh Db scope/connection after HTTP completion proves the entry committed
        var audit = f.Db(db => db.ActivityLog.AsNoTracking().Single(entry => entry.ItemType == "PlanningDraftAccess" && entry.Reason == "grid audit regression"));
        Assert.Equal(d.User(TestData.Admin), audit.ActorUserId);
        Assert.Contains(person.ToString(), audit.Snapshot!);
        Assert.DoesNotContain("PRIVATE AUDIT REGRESSION DRAFT", audit.Snapshot!);
        var afterCorrection = await (await f.As(TestData.Admin).GetAsync(path)).Json();
        Assert.DoesNotContain(draftId.ToString(), afterCorrection.ToJsonString());
        Assert.Equal(before + 1, Audits());

        await Flag("project_screens", true); await Flag("home_overview", true);
        var cohort = $"&teamProjectId={project.Id}&homeDisciplineId={civil}&indicator=Above100";
        var normalCohort = await (await f.As(TestData.Admin).GetAsync(path + cohort)).Json();
        Assert.Empty(normalCohort["people"]!.AsArray());
        Assert.Equal(before + 1, Audits());
        var correctedCohort = await (await f.As(TestData.Admin).GetAsync(path + cohort + "&draftAccessReason=cohort%20grid%20audit")).Json();
        Assert.Equal(person, Assert.Single(correctedCohort["people"]!.AsArray())!.G("id"));
        Assert.Equal(before + 2, Audits());

        var entriesPath = $"/api/v1/planning/entries?from={Week:yyyy-MM-dd}&weeks=2&personId={person}{cohort}";
        Assert.Empty((await (await f.As(TestData.Admin).GetAsync(entriesPath)).Json())["items"]!.AsArray());
        Assert.Equal(before + 2, Audits());
        var entries = await (await f.As(TestData.Admin).GetAsync(entriesPath + "&draftAccessReason=cohort%20entry%20audit")).Json();
        Assert.Equal(draftId, Assert.Single(entries["items"]!.AsArray())!.G("id"));
        Assert.Equal(before + 3, Audits()); // cohort Read logs once; EntryRows must not log that same request twice
        Assert.Equal(1, f.Db(db => db.ActivityLog.Count(entry => entry.ItemType == "PlanningDraftAccess" && entry.Reason == "cohort entry audit")));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).GetAsync(path + $"&teamProjectId={Guid.NewGuid()}&draftAccessReason=missing%20project%20audit")).StatusCode);
        Assert.Equal(before + 3, Audits());
    }

    [Fact]
    public async Task Unit_only_leader_can_use_combined_team_home_and_above100_cohort_without_gaining_write_scope()
    {
        var civil = await d.Discipline("Civil"); var electrical = await d.Discipline("Electrical");
        var team = await d.Project(); var work = await d.Project();
        var included = await Person("Unit-only scoped Civil", civil, d.User(TestData.Lena));
        var otherDiscipline = await Person("Unit-only scoped Electrical", electrical, d.User(TestData.Lena));
        var unscoped = await Person("Outside led unit", civil, d.User(TestData.Lena));
        var units = await f.DbAsync(async db => {
            var maxSort = await db.Set<OrgLevel>().Select(level => (int?)level.SortOrder).MaxAsync() ?? 0;
            var rootLevel = new OrgLevel { Name = "Synthetic unit cohort region", SortOrder = maxSort + 1 };
            var childLevel = new OrgLevel { Name = "Synthetic unit cohort team", SortOrder = maxSort + 2 };
            db.Set<OrgLevel>().AddRange(rootLevel, childLevel);
            var root = new OrgUnit { Name = "Synthetic led region", LevelId = rootLevel.Id, LeaderId = d.User(TestData.Jill) };
            var child = new OrgUnit { Name = "Synthetic descendant team", LevelId = childLevel.Id, ParentId = root.Id };
            db.Set<OrgUnit>().AddRange(root, child);
            foreach (var person in new[] { included, otherDiscipline }) (await db.Users.SingleAsync(user => user.Id == person)).UnitId = child.Id;
            foreach (var person in new[] { included, otherDiscipline, unscoped }) db.ProjectMembers.Add(new ProjectMember { ProjectId = team.Id, UserId = person, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            await db.SaveChangesAsync(); return (Root: root.Id, Child: child.Id);
        });
        foreach (var person in new[] { included, otherDiscipline }) await Plan(person, 48, work.Id, creator: TestData.Admin, visibility: PlanningVisibility.Published);
        await Plan(unscoped, 71.5m, work.Id, creator: TestData.Admin, visibility: PlanningVisibility.Published);
        await f.DbAsync(async db => {
            foreach (var (person, label) in new[] { (included, "IN UNIT PRIVATE SENTINEL"), (unscoped, "OUTSIDE UNIT PRIVATE SENTINEL") })
                db.PlanningEntries.Add(new PlanningEntry { PersonId = person, Label = label, HoursPerWeek = 77.5m,
                    StartWeek = Week, EndWeek = Week, Confidence = PlanningConfidence.Expected, Visibility = PlanningVisibility.Draft,
                    SourceCategory = PlanningSource.Other, CreatedBy = d.User(TestData.Admin), LastValidatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        await Flag("org_structure", true); await Flag("project_screens", true); await Flag("home_overview", true); await Flag("capacity_screens", true);
        var cohort = $"&unitId={units.Root}&teamProjectId={team.Id}&homeDisciplineId={civil}&indicator=Above100";
        var result = await (await f.As(TestData.Jill).GetAsync(Grid(cohort))).Json();
        var row = Assert.Single(result["people"]!.AsArray())!;
        Assert.Equal(included, row.G("id")); Assert.Equal(48m, row["cells"]![0]!["expected"]!.GetValue<decimal>());
        Assert.DoesNotContain(otherDiscipline.ToString(), result.ToJsonString()); Assert.DoesNotContain(unscoped.ToString(), result.ToJsonString());
        Assert.DoesNotContain("PRIVATE SENTINEL", result.ToJsonString()); Assert.DoesNotContain("71.5", result.ToJsonString());
        var entries = await (await f.As(TestData.Jill).GetAsync($"/api/v1/planning/entries?from={Week:yyyy-MM-dd}&weeks=2{cohort}")).Json();
        Assert.Equal(included, Assert.Single(entries["items"]!.AsArray())!.G("personId"));
        Assert.DoesNotContain("PRIVATE SENTINEL", entries.ToJsonString()); Assert.DoesNotContain("Outside led unit", entries.ToJsonString());
        var exported = await f.As(TestData.Jill).GetStringAsync($"/api/v1/planning/entries/export?format=csv&from={Week:yyyy-MM-dd}&weeks=2{cohort}");
        Assert.Contains("Unit-only scoped Civil", exported);
        Assert.DoesNotContain("Outside led unit", exported); Assert.DoesNotContain("Unit-only scoped Electrical", exported);
        Assert.DoesNotContain("71.5", exported); Assert.DoesNotContain("PRIVATE SENTINEL", exported);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).Post("/api/v1/planning/entries", new { personId = included,
            hoursPerWeek = 4m, startWeek = Week, endWeek = Week, label = "Read permission must not become a write grant", sourceCategory = PlanningSource.Other })).StatusCode);
        await Flag("org_structure", false);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Jill).GetAsync(Grid(cohort))).StatusCode);
    }
}
