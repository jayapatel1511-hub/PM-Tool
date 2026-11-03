using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Real-database adapter checks. The caller supplies scope/snapshot; no new HTTP route or permission is created for testing.
public sealed class CapacityExactDateReservationTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Mon = new(2026, 11, 9);
    public CapacityExactDateReservationTests() => d = new(f);
    public void Dispose() => f.Dispose();
    sealed record Reservation(Guid Id, Guid Task, Guid Person);
    async Task<Reservation> Reserve(Project p, decimal hours, DateOnly? from = null, DateOnly? through = null,
        AllocationEndpoints.DayInput[]? days = null, string creator = TestData.Pm, string status = AllocationStatus.Confirmed, Guid? role = null, Guid? person = null)
    {
        var who = person ?? d.User(TestData.Alex); var first = from ?? Mon; var last = through ?? Mon.AddDays(4);
        if (person is not null && person != d.User(TestData.Alex))
            await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/members", new { userId = who, roles = new[] { ProjectRole.TeamMember }, primaryDisciplineId = d.ProjectDiscipline(p.Id, "Civil") })).Json(201);
        var task = await d.NewTask(p.Id, extra: new { assigneeId = who, estimatedHours = 0m, startDate = first, dueDate = last });
        var saved = await (await f.As(creator).Post($"/api/v1/projects/{p.Id}/allocations", new AllocationEndpoints.CreateBody(Guid.NewGuid(), who,
            AllocationPurpose.Production, first, last, hours, days ?? [], [new("Task", task.G("id"), first)], null, role))).Json();
        if (status != AllocationStatus.Proposed)
            await f.DbAsync(async db => { (await db.Allocations.SingleAsync(row => row.Id == saved.G("id"))).Status = status; return await db.SaveChangesAsync(); });
        return new(saved.G("id"), task.G("id"), who);
    }
    Task<AllocationScreenReads.ExactDateReservations> Read(Guid project, DateOnly from, DateOnly through, bool complete = true, Guid[]? permitted = null)
        => f.DbAsync(db => AllocationScreenReads.ConfirmedByDiscipline(project, from, through,
            permitted is null ? db.Allocations.AsNoTracking() : db.Allocations.AsNoTracking().Where(row => permitted.Contains(row.Id)), complete, db, f.Clock));
    static AllocationScreenReads.ExactDateReservationDiscipline Civil(AllocationScreenReads.ExactDateReservations read)
        => Assert.Single(read.Disciplines.Where(row => row.DisciplineName == "Civil"));
    async Task<string> State() => await f.DbAsync(async db => JsonSerializer.Serialize(new {
        Allocations = await db.Allocations.OrderBy(row => row.Id).Select(row => new { row.Id, row.Status, row.RowVersion, row.PlannedHours, row.PersonId }).ToArrayAsync(),
        Links = await db.AllocationWorkLinks.CountAsync(), Days = await db.AllocationDayOverrides.CountAsync(), Dates = await db.PersonDateVersions.CountAsync(),
        Commands = await db.CoordinationCommands.CountAsync(), Activity = await db.ActivityLog.CountAsync(), Notices = await db.Notifications.CountAsync(),
        Outbox = await db.Outbox.CountAsync(), Emails = await db.Emails.CountAsync(), Tasks = await db.Tasks.CountAsync(), Plans = await db.PlanningEntries.CountAsync(),
        Roles = await db.OpenRoles.OrderBy(row => row.Id).Select(row => new { row.Id, row.Status, row.RowVersion, row.FilledAllocationId }).ToArrayAsync(),
    }));

    [Fact]
    public async Task Exact_inclusive_partial_weeks_apply_full_range_day_overrides_before_clipping_and_do_not_write()
    {
        var p = await d.Project();
        await Reserve(p, 100, through: Mon.AddDays(11), days: [new(Mon.AddDays(2), 28)]);
        // The other nine working days carry8h each; Tue10→Tue17 contains28+5*8, not rounded full weeks.
        var before = await State(); var read = await Read(p.Id, Mon.AddDays(1), Mon.AddDays(8));
        Assert.Equal((Mon.AddDays(1), Mon.AddDays(8), "Complete", f.Clock.GetUtcNow()), (read.FromDate, read.ThroughDate, read.AllocationCoverage, read.AsOf));
        Assert.Equal(((decimal?)68m, 68m, 0), (Civil(read).ConfirmedHours, Civil(read).KnownConfirmedHours, Civil(read).UnavailableConfirmed));
        Assert.Equal(28m, Civil(await Read(p.Id, Mon.AddDays(2), Mon.AddDays(2))).ConfirmedHours);
        Assert.Equal(100m, Civil(await Read(p.Id, Mon, Mon.AddDays(11))).ConfirmedHours);
        Assert.Equal(0m, Civil(await Read(p.Id, Mon.AddDays(5), Mon.AddDays(6))).ConfirmedHours);
        Assert.Equal(0m, Civil(await Read(p.Id, Mon.AddDays(14), Mon.AddDays(14))).ConfirmedHours);
        Assert.Equal(before, await State());
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/allocations/discipline-context")).StatusCode);
    }

    [Fact]
    public async Task Exact_decimal_last_day_remainder_and_existing_weekly_bytes_are_preserved()
    {
        var p = await d.Project(); await Reserve(p, 10, through: Mon.AddDays(2));
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_screens", new { value = true })).Json();
        var path = $"/api/v1/projects/{p.Id}/allocations/discipline-context?from=2026-11-09&weeks=1";
        var weeklyBefore = await (await f.As(TestData.Pm).GetAsync(path)).Json(); var state = await State();
        Assert.Equal(10m - 10m / 3m, Civil(await Read(p.Id, Mon.AddDays(1), Mon.AddDays(2))).KnownConfirmedHours);
        Assert.Equal(10m - 10m / 3m * 2m, Civil(await Read(p.Id, Mon.AddDays(2), Mon.AddDays(2))).KnownConfirmedHours);
        Assert.Equal(10m, Civil(await Read(p.Id, Mon, Mon.AddDays(2))).KnownConfirmedHours);
        Assert.Equal(state, await State());
        var weeklyAfter = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        Assert.Equal(weeklyBefore.ToJsonString(), weeklyAfter.ToJsonString());
        Assert.Equal(10m, weeklyAfter["disciplines"]!.AsArray().Single(row => row!["disciplineName"]?.GetValue<string>() == "Civil")!["weeks"]![0]!["confirmedHours"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Full_calendar_failure_remains_unknown_while_usable_zero_and_known_hours_remain_distinct()
    {
        var p = await d.Project(); var unknown = await Reserve(p, 5, through: Mon.AddDays(1));
        await Reserve(p, 40, through: Mon.AddDays(6), person: d.User(TestData.Jill));
        await f.DbAsync(async db => {
            var office = await db.Users.Where(user => user.Id == unknown.Person).Select(user => user.OfficeId).SingleAsync();
            db.PersonSchedules.Add(new PersonSchedule { PersonId = unknown.Person, FromDate = Mon, WeeklyHours = 40, WorkingDays = [1] });
            db.Holidays.Add(new Holiday { OfficeId = office, Date = Mon, Name = "Synthetic calendar removes the only eligible day" });
            return await db.SaveChangesAsync();
        });
        var state = await State(); var read = await Read(p.Id, Mon.AddDays(1), Mon.AddDays(1));
        Assert.Null(Civil(read).ConfirmedHours); Assert.Equal((10m, 1), (Civil(read).KnownConfirmedHours, Civil(read).UnavailableConfirmed));
        // The unusable Mon–Tue range still overlaps a Tuesday clip; its failure cannot be silently replaced by that day's zero.
        Assert.Equal(1, Civil(read).UnavailableConfirmed);
        var usableWeekend = await Read(p.Id, Mon.AddDays(5), Mon.AddDays(6));
        Assert.Equal((0m, 0), (Civil(usableWeekend).KnownConfirmedHours, Civil(usableWeekend).UnavailableConfirmed));
        Assert.Equal(0m, Civil(usableWeekend).ConfirmedHours); Assert.Equal(state, await State());
    }

    [Fact]
    public async Task Authorized_visible_only_query_is_never_widened_and_other_statuses_projects_and_plans_are_excluded()
    {
        var p = await d.Project(); var visible = await Reserve(p, 40, creator: TestData.Marc);
        var hidden = await Reserve(p, 99); await Reserve(p, 777, status: AllocationStatus.Proposed);
        await Reserve(p, 888, status: AllocationStatus.Completed); var other = await d.Project(); await Reserve(other, 333);
        await f.DbAsync(async db => {
            db.PlanningEntries.Add(new PlanningEntry { PersonId = visible.Person, ProjectId = p.Id, SourceCategory = PlanningSource.MajorProject,
                Label = "Synthetic confirmed planning entry is not a confirmed reservation", HoursPerWeek = 40, StartWeek = Mon, EndWeek = Mon,
                Confidence = PlanningConfidence.Confirmed, Visibility = PlanningVisibility.Published, CreatedBy = d.User(TestData.Pm), LastValidatedAt = f.Clock.GetUtcNow() });
            return await db.SaveChangesAsync();
        });
        // Obtain the selected scope from the real existing allocation list, not an invented permission rule in the adapter test.
        var list = await (await f.As(TestData.Marc).GetAsync($"/api/v1/projects/{p.Id}/allocations")).Json();
        var ids = list["items"]!.AsArray().Select(row => row!.G("id")).ToArray(); Assert.Equal(new[] { visible.Id }, ids);
        var state = await State(); var partial = await Read(p.Id, Mon, Mon.AddDays(4), false, ids);
        Assert.Equal("VisibleOnly", partial.AllocationCoverage); Assert.Null(Civil(partial).ConfirmedHours);
        Assert.Equal((40m, 0), (Civil(partial).KnownConfirmedHours, Civil(partial).UnavailableConfirmed));
        foreach (var secret in new[] { hidden.Id.ToString(), hidden.Task.ToString() }) Assert.DoesNotContain(secret, JsonSerializer.Serialize(partial));
        var complete = await Read(p.Id, Mon, Mon.AddDays(4)); Assert.Equal(139m, Civil(complete).ConfirmedHours);
        var empty = await Read(p.Id, Mon, Mon.AddDays(4), false, []); Assert.All(empty.Disciplines, row => { Assert.Null(row.ConfirmedHours); Assert.Equal(0m, row.KnownConfirmedHours); Assert.Equal(0, row.UnavailableConfirmed); });
        var invalid = await Assert.ThrowsAsync<ApiException>(() => Read(p.Id, Mon.AddDays(1), Mon)); Assert.Equal(400, invalid.Status);
        Assert.Equal(state, await State());
    }

    [Fact]
    public async Task Current_then_normally_superseded_review_retains_exact_known_reservation_under_Unattributed()
    {
        var p = await d.Project(); var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
        var deliverable = (await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new {
            name = "Synthetic exact-date review source", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false,
        })).Json(201)).G("id");
        var revision = (await (await f.As(TestData.Alex).Post($"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, d.User(TestData.Alex), "Deliverable", "exact-date", "Synthetic exact-date review source", "A", "https://example.test/exact.pdf",
            "Synthetic survey", "Synthetic scope", null, null, null, null, null, null))).Json()).G("id");
        var assignments = new[] { new ReviewEndpoints.Assignment(electrical, d.User(TestData.Omar), Mon) }; var root = $"/api/v1/projects/{p.Id}/reviews";
        var package = (await (await f.As(TestData.Marc).Post(root, new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Synthetic current round", "Exact reservation attribution", civil,
            d.User(TestData.Marc), [revision], assignments, false, null))).Json()).G("id");
        await (await f.As(TestData.Marc).Post(root + $"/{package}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(), Version<ReviewPackage>(package), "start", "Start synthetic review"))).Json();
        var round = f.Db(db => db.ReviewPackages.Single(row => row.Id == package).CurrentRoundId!.Value);
        var assignment = f.Db(db => db.DisciplineReviews.Single(row => row.RoundId == round).Id);
        var allocation = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/allocations", new AllocationEndpoints.CreateBody(Guid.NewGuid(), d.User(TestData.Omar),
            AllocationPurpose.Review, Mon, Mon, 12, [], [new("Review", assignment, Mon, 3)], null))).Json()).G("id");
        await f.DbAsync(async db => { (await db.Allocations.SingleAsync(row => row.Id == allocation)).Status = AllocationStatus.Confirmed; return await db.SaveChangesAsync(); });
        var read = await Read(p.Id, Mon, Mon); Assert.Equal(12m, Assert.Single(read.Disciplines.Where(row => row.DisciplineName == "Electrical")).ConfirmedHours);
        await (await f.As(TestData.Marc).Post(root + $"/{package}/rounds", new ReviewEndpoints.RoundBody(Guid.NewGuid(), Version<ReviewPackage>(package), "Updated exact-date scope",
            [revision], assignments, "New round for synthetic scope", null))).Json();
        Assert.Equal(ReviewStatus.Superseded, f.Db(db => db.ReviewRounds.Single(row => row.Id == round).Status));
        var state = await State(); read = await Read(p.Id, Mon, Mon);
        var unattributed = Assert.Single(read.Disciplines.Where(row => row.Unattributed));
        Assert.Equal(((decimal?)12m, 12m, 0), (unattributed.ConfirmedHours, unattributed.KnownConfirmedHours, unattributed.UnavailableConfirmed));
        Assert.Equal(0m, Assert.Single(read.Disciplines.Where(row => row.DisciplineName == "Electrical")).ConfirmedHours);
        Assert.Equal(state, await State());
    }

    [Fact]
    public async Task Current_role_conflict_is_Unattributed_stale_role_is_ignored_and_known_zero_stays_zero()
    {
        var p = await d.Project(); var role = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/open-roles", new {
            roleLabel = "Synthetic Electrical role with Civil reservation", projectDisciplineId = d.ProjectDiscipline(p.Id, "Electrical"), fromDate = Mon, throughDate = Mon.AddDays(4), plannedHours = 20,
        })).Json(201)).G("id");
        var allocation = await Reserve(p, 20, role: role);
        var read = await Read(p.Id, Mon.AddDays(1), Mon.AddDays(1));
        var unattributed = Assert.Single(read.Disciplines.Where(row => row.Unattributed)); Assert.Equal(4m, unattributed.KnownConfirmedHours); Assert.Equal(4m, unattributed.ConfirmedHours);
        var weekend = await Read(p.Id, Mon.AddDays(5), Mon.AddDays(6)); Assert.DoesNotContain(weekend.Disciplines, row => row.Unattributed);
        // Explicit persisted stale-role evidence: a cancelled role must not keep overriding otherwise current valid work attribution.
        await f.DbAsync(async db => { (await db.OpenRoles.SingleAsync(row => row.Id == role)).Status = OpenRoleStatus.Cancelled; return await db.SaveChangesAsync(); });
        var state = await State(); read = await Read(p.Id, Mon.AddDays(1), Mon.AddDays(1)); Assert.Equal(4m, Civil(read).ConfirmedHours); Assert.DoesNotContain(read.Disciplines, row => row.Unattributed);
        Assert.Equal(state, await State());
        // A missing current work link preserves the known reservation, including a real zero within an overlapping window.
        await f.DbAsync(async db => {
            (await db.AllocationWorkLinks.SingleAsync(link => link.AllocationId == allocation.Id)).ReleasedAt = f.Clock.GetUtcNow();
            (await db.Allocations.SingleAsync(row => row.Id == allocation.Id)).ThroughDate = Mon.AddDays(6);
            return await db.SaveChangesAsync();
        });
        read = await Read(p.Id, Mon.AddDays(5), Mon.AddDays(6)); unattributed = Assert.Single(read.Disciplines.Where(row => row.Unattributed));
        Assert.Equal(((decimal?)0m, 0m, 0), (unattributed.ConfirmedHours, unattributed.KnownConfirmedHours, unattributed.UnavailableConfirmed));
    }
}
