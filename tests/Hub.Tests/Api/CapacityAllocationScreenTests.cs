using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S30 tests own disposable databases. Reads must not reserve hours or change approval/concurrency/audit state.
public sealed class CapacityAllocationScreenTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Mon = new(2026, 11, 9);
    public CapacityAllocationScreenTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(string name, bool on = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();
    static decimal N(JsonNode row, string key) => row[key]!.GetValue<decimal>();
    string Root(Guid project) => $"/api/v1/projects/{project}/allocations";
    AllocationEndpoints.CreateBody Body(Guid person, Guid task, decimal hours = 30, DateOnly? from = null, DateOnly? through = null, Guid? role = null)
        => new(Guid.NewGuid(), person, AllocationPurpose.Production, from ?? Mon, through ?? Mon.AddDays(18), hours, [], [new("Task", task, from ?? Mon)], null, role);
    AllocationScreenReads.ProposalCapacityBody ReadBody(AllocationEndpoints.CreateBody body, bool include = true)
        => new(body.PersonId, body.FromDate, body.ThroughDate, include, body.Purpose, body.PlannedHours, body.Days, body.Links);
    async Task<(Project Project, Guid Alex, Guid Task)> Fixture()
    {
        var project = await d.Project(); var alex = d.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(user => user.Id == alex)).WeeklyCapacityHours = 40; return await db.SaveChangesAsync(); });
        var task = await d.NewTask(project.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = Mon, dueDate = Mon });
        return (project, alex, task.G("id"));
    }
    async Task<string> DurableState(Guid project) => await f.DbAsync(async db => {
        var roles = await db.OpenRoles.Where(role => role.ProjectId == project).OrderBy(role => role.Id)
            .Select(role => new { role.Status, role.RowVersion, role.FilledAllocationId }).ToArrayAsync();
        return System.Text.Json.JsonSerializer.Serialize(new {
            Allocations = await db.Allocations.CountAsync(a => a.ProjectId == project), Days = await db.AllocationDayOverrides.CountAsync(), Links = await db.AllocationWorkLinks.CountAsync(),
            Versions = await db.PersonDateVersions.CountAsync(), Commands = await db.CoordinationCommands.CountAsync(c => c.ProjectId == project),
            Activity = await db.ActivityLog.CountAsync(), Notifications = await db.Notifications.CountAsync(), Outbox = await db.Outbox.CountAsync(), Emails = await db.Emails.CountAsync(), Roles = roles,
        });
    });

    [Fact]
    public async Task Lena_Fill_preview_has_exact_three_weeks_before_after_and_daily_overload_without_saving()
    {
        var (p, alex, task) = await Fixture();
        // Lena is explicitly appointed project PM; Executive scope alone does not grant proposal authority.
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = d.User(TestData.Lena), Roles = [ProjectRole.PM], AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 5m, startDate = Mon, dueDate = Mon });
        await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 36m, startDate = Mon.AddDays(7), dueDate = Mon.AddDays(11) });
        var role = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = "Synthetic three-week Civil role", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), fromDate = Mon, throughDate = Mon.AddDays(18), plannedHours = 30 })).Json(201);
        await Flag("capacity_screens"); await Flag("allocation_preview");
        var before = await DurableState(p.Id); var body = Body(alex, task, role: role.G("id"));
        var preview = await (await f.As(TestData.Lena).Post(Root(p.Id) + "?preview=true", body)).Json();
        var read = preview["comparison"]!;
        Assert.Equal(("Complete", "2026-11-09", "2026-11-27", alex), (read.S("state"), read.S("fromDate"), read.S("throughDate"), read.G("personId")));
        var weeks = read["weeks"]!.AsArray();
        Assert.Equal(new[] { "2026-11-09", "2026-11-16", "2026-11-23" }, weeks.Select(week => week!.S("week")));
        Assert.Equal(new[] { 40m, 40m, 40m }, weeks.Select(week => N(week!, "availableHours")));
        Assert.Equal(new[] { 5m, 36m, 0m }, weeks.Select(week => N(week!, "beforeHours")));
        Assert.Equal(new[] { 15m, 46m, 10m }, weeks.Select(week => N(week!, "afterHours")));
        Assert.Equal(new[] { false, true, false }, weeks.Select(week => week!["overCapacity"]!.GetValue<bool>()));
        Assert.Equal(before, await DurableState(p.Id));
        Assert.Equal(OpenRoleStatus.Open, await f.DbAsync(db => db.OpenRoles.Where(r => r.Id == role.G("id")).Select(r => r.Status).SingleAsync()));
    }

    [Fact]
    public async Task Pure_read_uses_exact_dates_day_overrides_and_linked_work_once_with_section52_off()
    {
        var (p, alex, task) = await Fixture(); await Flag("capacity_screens");
        await f.DbAsync(async db => { var work = await db.Tasks.SingleAsync(t => t.Id == task); work.EstimatedHours = 8; work.StartDate = Mon.AddDays(1); work.DueDate = Mon.AddDays(1); return await db.SaveChangesAsync(); });
        var body = Body(alex, task, 16, Mon.AddDays(1), Mon.AddDays(2)) with { Days = [new(Mon.AddDays(1), 12), new(Mon.AddDays(2), 4)], Links = [new("Task", task, Mon.AddDays(1))] };
        var before = await DurableState(p.Id);
        var read = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview", ReadBody(body))).Json();
        var week = Assert.Single(read["weeks"]!.AsArray())!;
        Assert.Equal((40m, 8m, 16m, true), (N(week, "availableHours"), N(week, "beforeHours"), N(week, "afterHours"), week["overCapacity"]!.GetValue<bool>()));
        Assert.Equal(before, await DurableState(p.Id));
        var baseline = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview", ReadBody(body, false) with { PlannedHours = 0, Links = [] })).Json();
        Assert.Null(baseline["weeks"]![0]!["afterHours"]); Assert.Null(baseline["weeks"]![0]!["overCapacity"]);
        var invalid = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview", ReadBody(body) with { Links = [new("Task", Guid.NewGuid(), Mon.AddDays(1))] })).Json(400);
        Assert.NotNull(invalid["errors"]!["links"]);
        Assert.Equal(before, await DurableState(p.Id));
        await (await f.As(TestData.Pm).Post(Root(p.Id), body)).Json();
        var reservedState = await DurableState(p.Id);
        var duplicate = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview", ReadBody(body))).Json(400);
        Assert.NotNull(duplicate["errors"]!["links"]); Assert.Equal(reservedState, await DurableState(p.Id));
    }

    [Fact]
    public async Task Outside_planner_scope_has_no_person_or_figures_in_either_preview_read()
    {
        var (p, alex, task) = await Fixture(); await Flag("capacity_screens"); await Flag("allocation_preview");
        // Omar leads the project's Electrical discipline but is not Alex's supervisor or a PM in planner scope.
        var body = Body(alex, task); var before = await DurableState(p.Id);
        foreach (var path in new[] { "/capacity-preview", "?preview=true" })
        {
            var read = path.StartsWith('?') ? (await (await f.As(TestData.Omar).Post(Root(p.Id) + path, body)).Json())["comparison"]!
                : await (await f.As(TestData.Omar).Post(Root(p.Id) + path, ReadBody(body))).Json();
            Assert.Equal("OutsideScope", read.S("state")); Assert.Null(read["personId"]); Assert.Empty(read["weeks"]!.AsArray());
            Assert.DoesNotContain("Alex", read.ToJsonString());
        }
        Assert.Equal(before, await DurableState(p.Id));
    }

    [Fact]
    public async Task Hidden_work_suppresses_load_and_overload_but_does_not_leak_metadata_or_change_confirmation()
    {
        var (p, alex, task) = await Fixture(); var hidden = await d.Project();
        // Sam can propose on this project and supervise Alex; the restricted project's hidden work still blocks confirmation.
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = d.User(TestData.Sam), Roles = [ProjectRole.PM], AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        await d.NewTask(hidden.Id, extra: new { name = "SECRET CAPACITY ASSIGNMENT", assigneeId = alex, estimatedHours = 73.5m, startDate = Mon, dueDate = Mon });
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(project => project.Id == hidden.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        await Flag("capacity_screens"); await Flag("allocation_preview"); var before = await DurableState(p.Id); var body = Body(alex, task);
        var read = await (await f.As(TestData.Sam).Post(Root(p.Id) + "/capacity-preview", ReadBody(body))).Json();
        var preview = await (await f.As(TestData.Sam).Post(Root(p.Id) + "?preview=true", body)).Json();
        Assert.Equal("Partial", read.S("state")); Assert.Equal("Partial", preview["comparison"]!.S("state")); Assert.Empty(preview["weeks"]!.AsArray());
        Assert.All(read["weeks"]!.AsArray(), week => { Assert.Equal(40m, N(week!, "availableHours")); Assert.Null(week!["beforeHours"]); Assert.Null(week["afterHours"]); Assert.Null(week["overCapacity"]); });
        foreach (var secret in new[] { "SECRET", hidden.Id.ToString(), hidden.ProjectNumber, "73.5" }) Assert.DoesNotContain(secret, read.ToJsonString() + preview.ToJsonString());
        Assert.Equal(before, await DurableState(p.Id));
        var saved = await (await f.As(TestData.Sam).Post(Root(p.Id), body with { RequestId = Guid.NewGuid() })).Json();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Sam).GetAsync(Root(p.Id) + $"/{saved.G("id")}/confirmation-preview")).StatusCode);
        Assert.Equal(AllocationStatus.Proposed, await f.DbAsync(db => db.Allocations.Where(a => a.Id == saved.G("id")).Select(a => a.Status).SingleAsync()));
    }

    [Fact]
    public async Task Provisional_and_unestimated_work_never_claim_zero_load_or_a_complete_no_overload()
    {
        var (p, alex, task) = await Fixture(); await Flag("capacity_screens"); await Flag("allocation_preview");
        await d.NewTask(p.Id, extra: new { assigneeId = alex, dueDate = Mon.AddDays(2) });
        await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 8m });
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = alex, WorkDate = Mon, Category = AvailabilityCategory.Reduced, AvailableHours = 4 }); return await db.SaveChangesAsync(); });
        var preview = await (await f.As(TestData.Pm).Post(Root(p.Id) + "?preview=true", Body(alex, task))).Json();
        var read = preview["comparison"]!; Assert.Equal("Incomplete", read.S("state"));
        Assert.Equal((1, 1), (read.I("unestimatedTasks"), read.I("undatedTasks"))); Assert.Empty(preview["weeks"]!.AsArray());
        var first = read["weeks"]![0]!; Assert.True(first["provisional"]!.GetValue<bool>()); Assert.Null(first["availableHours"]); Assert.Null(first["overCapacity"]);
        Assert.All(read["weeks"]!.AsArray(), week => Assert.Null(week!["overCapacity"]));
    }

    async Task<Guid> Allocation(Project project, Guid person, decimal hours, string status, params Guid[] taskIds)
    {
        var created = await (await f.As(TestData.Pm).Post(Root(project.Id), new AllocationEndpoints.CreateBody(Guid.NewGuid(), person, AllocationPurpose.Production,
            new(2026, 11, 16), new(2026, 11, 20), hours, [], taskIds.Select(task => new AllocationEndpoints.LinkInput("Task", task, new(2026, 11, 16))).ToArray(), null))).Json();
        if (status == AllocationStatus.Confirmed)
            await f.DbAsync(async db => { (await db.Allocations.SingleAsync(a => a.Id == created.G("id"))).Status = status; return await db.SaveChangesAsync(); });
        return created.G("id");
    }

    [Fact]
    public async Task Civil_week47_shows_60_confirmed_16_proposed_90_remaining_separately_independent_of_list_filters()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var week = new DateOnly(2026, 11, 16);
        var work = await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 100m, startDate = week, dueDate = week.AddDays(4) });
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(task => task.Id == work.G("id"))).ProgressPct = 10; return await db.SaveChangesAsync(); });
        var other = await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = week, dueDate = week.AddDays(4) });
        await Allocation(p, alex, 60, AllocationStatus.Confirmed, work.G("id")); await Allocation(p, alex, 16, AllocationStatus.Proposed, other.G("id"));
        await Flag("capacity_screens"); var before = await DurableState(p.Id);
        var read = await (await f.As(TestData.Pm).GetAsync(Root(p.Id) + "/discipline-context?from=2026-11-16&weeks=1&q=nomatch&status=Cancelled&page=999")).Json();
        Assert.Equal("Complete", read.S("allocationCoverage"));
        var row = read["disciplines"]!.AsArray().Single(row => row!.S("disciplineName") == "Civil")!; var cell = Assert.Single(row["weeks"]!.AsArray())!;
        Assert.Equal((60m, 16m, 90m), (N(cell, "confirmedHours"), N(cell, "proposedHours"), N(cell, "remainingEstimateHours")));
        Assert.DoesNotContain(cell.AsObject().Select(pair => pair.Key), key => key.Contains("total", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(before, await DurableState(p.Id));
        // FR52 uses open due-week context: On Hold remains open even though canonical daily confirmation excludes it.
        var held = await d.NewTask(p.Id, extra: new { estimatedHours = 20m, dueDate = week.AddDays(2) });
        var terminal = await d.NewTask(p.Id, extra: new { estimatedHours = 777m, dueDate = week.AddDays(2) });
        await f.DbAsync(async db => {
            (await db.Tasks.SingleAsync(task => task.Id == held.G("id"))).Status = TaskStatuses.OnHold;
            (await db.Tasks.SingleAsync(task => task.Id == terminal.G("id"))).Status = TaskStatuses.Cancelled;
            return await db.SaveChangesAsync();
        });
        var withHeld = await (await f.As(TestData.Pm).GetAsync(Root(p.Id) + "/discipline-context?from=2026-11-16&weeks=1")).Json();
        var heldCell = withHeld["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Civil")!["weeks"]![0]!;
        Assert.Equal((60m, 16m, 110m), (N(heldCell, "confirmedHours"), N(heldCell, "proposedHours"), N(heldCell, "remainingEstimateHours")));
    }

    [Fact]
    public async Task Mixed_discipline_and_changed_calendar_are_explicit_while_partial_rows_and_missing_estimates_stay_unknown()
    {
        var p = await d.Project(); var alex = d.User(TestData.Alex); var week = new DateOnly(2026, 11, 16);
        var civil = await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = week, dueDate = week });
        var electrical = await d.NewTask(p.Id, as_: TestData.Pm, discipline: "Electrical", extra: new { assigneeId = alex, estimatedHours = 0m, startDate = week, dueDate = week });
        await d.NewTask(p.Id, extra: new { estimatedHours = 8m, dueDate = week }); await d.NewTask(p.Id, extra: new { dueDate = week }); await d.NewTask(p.Id, extra: new { estimatedHours = 9m });
        var mixed = await Allocation(p, alex, 17, AllocationStatus.Confirmed, civil.G("id"), electrical.G("id"));
        var separate = await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = week, dueDate = week });
        var changed = await Allocation(p, alex, 4, AllocationStatus.Proposed, separate.G("id"));
        await f.DbAsync(async db => { db.PersonSchedules.Add(new PersonSchedule { PersonId = alex, FromDate = week, WeeklyHours = 40, WorkingDays = [0] }); return await db.SaveChangesAsync(); });
        await Flag("capacity_screens");
        var path = Root(p.Id) + "/discipline-context?from=2026-11-16&weeks=1";
        var read = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        var unattributed = Assert.Single(read["disciplines"]!.AsArray().Where(row => row!["unattributed"]!.GetValue<bool>()))!;
        Assert.Equal(1, unattributed.I("unattributedAllocations")); Assert.Null(unattributed["weeks"]![0]!["confirmedHours"]); Assert.Equal(1, unattributed["weeks"]![0]!.I("unavailableConfirmed"));
        var row = read["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Civil")!;
        Assert.Equal(1, row.I("undatedTasks")); var cell = row["weeks"]![0]!;
        Assert.Null(cell["proposedHours"]); Assert.Equal(1, cell.I("unavailableProposed")); Assert.Null(cell["remainingEstimateHours"]); Assert.Equal(8m, N(cell, "knownRemainingEstimateHours")); Assert.Equal(1, cell.I("unestimatedTasks"));
        var lowerBound = await (await f.As(TestData.Alex).GetAsync(path)).Json(); Assert.Equal("VisibleOnly", lowerBound.S("allocationCoverage"));
        Assert.All(lowerBound["disciplines"]!.AsArray(), row => Assert.Equal(0m, N(row!["weeks"]![0]!, "knownConfirmedHours")));
        Assert.DoesNotContain(mixed.ToString(), lowerBound.ToJsonString()); Assert.DoesNotContain(changed.ToString(), lowerBound.ToJsonString());
        var unavailable = await (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview",
            new AllocationScreenReads.ProposalCapacityBody(alex, week, week.AddDays(4), false, AllocationPurpose.Production, 0, [], []))).Json();
        Assert.Equal("Unavailable", unavailable.S("state"));
        Assert.All(unavailable["weeks"]!.AsArray(), cell => { Assert.Null(cell!["beforeHours"]); Assert.Null(cell["afterHours"]); Assert.Null(cell["overCapacity"]); });
    }

    [Fact]
    public async Task Normally_superseded_review_round_moves_reserved_hours_to_Unattributed_without_losing_them()
    {
        var p = await d.Project(); var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new {
            name = "Synthetic allocation review source", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(),
            ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false,
        })).Json(201)).G("id");
        var revision = (await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), deliverable, Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "allocation-review", "Synthetic allocation review source", "A",
            "https://example.test/allocation-review.pdf", "Synthetic survey", "Synthetic review scope", null, null, null, null, null, null))).Json()).G("id");
        var assignments = new[] { new ReviewEndpoints.Assignment(electrical, d.User(TestData.Omar), Mon) };
        var reviewRoot = $"/api/v1/projects/{p.Id}/reviews";
        var package = (await (await f.As(TestData.Marc).Post(reviewRoot, new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Synthetic reservation review", "Check allocation attribution",
            civil, d.User(TestData.Marc), [revision], assignments, false, null))).Json()).G("id");
        await (await f.As(TestData.Marc).Post(reviewRoot + $"/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start synthetic review"))).Json();
        var round = f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value);
        var assignment = f.Db(db => db.DisciplineReviews.Single(row => row.RoundId == round).Id);
        var proposal = new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Omar), AllocationPurpose.Review, Mon, Mon, 12, [], [new("Review", assignment, Mon, 3)], null);
        var allocation = (await (await f.As(TestData.Pm).Post(Root(p.Id), proposal)).Json()).G("id");
        await Flag("capacity_screens");
        var path = Root(p.Id) + "/discipline-context?from=2026-11-09&weeks=1";
        async Task<JsonNode> Context() => await (await f.As(TestData.Pm).GetAsync(path)).Json();
        var state = await DurableState(p.Id); var before = await Context();
        Assert.Equal(12m, N(before["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Electrical")!["weeks"]![0]!, "proposedHours"));
        Assert.DoesNotContain(before["disciplines"]!.AsArray(), row => row!["unattributed"]!.GetValue<bool>());
        Assert.Equal(state, await DurableState(p.Id));

        // This is a normal API round replacement, not a fabricated dangling assignment.
        await (await f.As(TestData.Marc).Post(reviewRoot + $"/{package}/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(package),
            "Updated review scope", [revision], assignments, "New round for updated scope", null))).Json();
        Assert.Equal(ReviewStatus.Superseded, f.Db(db => db.ReviewRounds.Single(row => row.Id == round).Status));
        Assert.Equal(DisciplineReviewStatus.Pending, f.Db(db => db.DisciplineReviews.Single(row => row.Id == assignment).Status));
        Assert.NotEqual(round, f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId));
        Assert.True(f.Db(db => db.AllocationWorkLinks.Any(link => link.AllocationId == allocation && link.WorkId == assignment && link.ReleasedAt == null)));
        var invalid = await (await f.As(TestData.Pm).Post(Root(p.Id), proposal with { RequestId = Guid.NewGuid() })).Json(400);
        Assert.True(invalid["errors"]?["links"] is not null, "the same superseded assignment is ineligible for the existing canonical command");
        state = await DurableState(p.Id); var after = await Context();
        var unattributed = Assert.Single(after["disciplines"]!.AsArray().Where(row => row!["unattributed"]!.GetValue<bool>()))!;
        Assert.Equal((1, 12m, 12m), (unattributed.I("unattributedAllocations"), N(unattributed["weeks"]![0]!, "proposedHours"), N(unattributed["weeks"]![0]!, "knownProposedHours")));
        Assert.Equal(0m, N(after["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Electrical")!["weeks"]![0]!, "proposedHours"));
        Assert.Equal(AllocationStatus.Proposed, f.Db(db => db.Allocations.Single(row => row.Id == allocation).Status));
        Assert.Equal(state, await DurableState(p.Id));
    }

    [Fact]
    public async Task Missing_foreign_purpose_conflict_and_inactive_task_evidence_preserve_known_unattributed_hours()
    {
        var (p, alex, controlTask) = await Fixture(); var foreign = await d.Project();
        var foreignTask = await d.NewTask(foreign.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = Mon, dueDate = Mon });
        async Task<Guid> Reserve(decimal hours, Guid? role = null, Guid? task = null)
        {
            task ??= (await d.NewTask(p.Id, extra: new { assigneeId = alex, estimatedHours = 0m, startDate = Mon, dueDate = Mon })).G("id");
            return (await (await f.As(TestData.Pm).Post(Root(p.Id), Body(alex, task.Value, hours, Mon, Mon.AddDays(4), role))).Json()).G("id");
        }
        var control = await Reserve(4, task: controlTask); var missing = await Reserve(5); var foreignLink = await Reserve(6); var purposeConflict = await Reserve(7);
        // Filling a role with work from a different discipline is allowed by the existing command; the read must not guess a split.
        var role = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new {
            roleLabel = "Synthetic Electrical role with Civil work", projectDisciplineId = d.ProjectDiscipline(p.Id, "Electrical"), fromDate = Mon, throughDate = Mon.AddDays(4), plannedHours = 8,
        })).Json(201)).G("id");
        var roleConflict = await Reserve(8, role: role);
        Assert.Equal(roleConflict, f.Db(db => db.OpenRoles.Single(row => row.Id == role).FilledAllocationId));
        // These three clearly labelled legacy/corrupt evidence fixtures cannot be submitted through canonical Validate.
        await f.DbAsync(async db => {
            (await db.AllocationWorkLinks.SingleAsync(link => link.AllocationId == missing)).ReleasedAt = f.Clock.GetUtcNow();
            (await db.AllocationWorkLinks.SingleAsync(link => link.AllocationId == foreignLink)).WorkId = foreignTask.G("id");
            (await db.Allocations.SingleAsync(row => row.Id == purposeConflict)).Purpose = AllocationPurpose.Review;
            return await db.SaveChangesAsync();
        });
        await Flag("capacity_screens"); var path = Root(p.Id) + "/discipline-context?from=2026-11-09&weeks=1";
        async Task AssertHours(decimal civilHours, decimal unattributedHours, int count)
        {
            var state = await DurableState(p.Id); var read = await (await f.As(TestData.Pm).GetAsync(path)).Json();
            var civil = read["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Civil")!;
            var unknown = Assert.Single(read["disciplines"]!.AsArray().Where(row => row!["unattributed"]!.GetValue<bool>()))!;
            Assert.Equal(civilHours, N(civil["weeks"]![0]!, "proposedHours"));
            Assert.Equal((unattributedHours, count), (N(unknown["weeks"]![0]!, "knownProposedHours"), unknown.I("unattributedAllocations")));
            Assert.Equal(30m, read["disciplines"]!.AsArray().Sum(row => N(row!["weeks"]![0]!, "knownProposedHours")));
            foreach (var secret in new[] { foreign.Id.ToString(), foreign.ProjectNumber, foreignTask.G("id").ToString() }) Assert.DoesNotContain(secret, read.ToJsonString());
            Assert.Equal(state, await DurableState(p.Id));
        }
        await AssertHours(4, 26, 4);
        // Canonical allocation task eligibility excludes these existing statuses; all reservation hours remain visible as known evidence.
        foreach (var status in new[] { TaskStatuses.OnHold, TaskStatuses.Complete, TaskStatuses.Cancelled })
        {
            await f.DbAsync(async db => { var task = await db.Tasks.SingleAsync(row => row.Id == controlTask); task.Status = status; task.ProgressPct = status == TaskStatuses.Complete ? 100 : 0; return await db.SaveChangesAsync(); });
            await AssertHours(0, 30, 5);
        }
        Assert.Equal(AllocationStatus.Proposed, f.Db(db => db.Allocations.Single(row => row.Id == control).Status));
    }

    [Fact]
    public async Task Flag_off_routes_are_dark_and_existing_preview_shape_and_permissions_remain_unchanged()
    {
        var (p, alex, task) = await Fixture(); var body = Body(alex, task);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).Post(Root(p.Id) + "/capacity-preview", ReadBody(body))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync(Root(p.Id) + "/discipline-context")).StatusCode);
        await Flag("allocation_preview"); var preview = await (await f.As(TestData.Pm).Post(Root(p.Id) + "?preview=true", body)).Json();
        Assert.Equal(new[] { "preview", "weeks" }, preview.AsObject().Select(pair => pair.Key)); Assert.NotEmpty(preview["weeks"]!.AsArray());
        await Flag("capacity_screens");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post(Root(p.Id) + "/capacity-preview", ReadBody(body))).StatusCode);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(project => project.Id == p.Id)).Visibility = Visibility.Restricted; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Diane).GetAsync(Root(p.Id) + "/discipline-context")).StatusCode);
    }
}
