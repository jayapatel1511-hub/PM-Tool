using System.Text.Json;
using System.Text.Json.Nodes;
using System.Data.Common;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

internal sealed class CalendarScreenData(HubFactory f)
{
    public readonly TestData Data = new(f);
    public Task Flag(string name, bool on) => SetFlag(name, on);
    async Task SetFlag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();
    public async Task<JsonNode> Read(Guid project, string query = "", string who = TestData.Pm, int expect = 200) =>
        await (await f.As(who).GetAsync($"/api/v1/calendar/screen?from=2026-11-02&to=2026-11-08&projectIds={project}{query}")).Json(expect);
    public async Task<JsonNode> Preview(string type, Guid id, int version, Guid project, string date = "2026-11-05", string who = TestData.Pm, int expect = 200) =>
        await (await f.As(who).GetAsync($"/api/v1/calendar/screen/{type}/{id}/date-preview?dueDate={date}&rowVersion={version}&projectIds={project}")).Json(expect);
    public async Task<JsonNode> Deliverable(Guid project, Guid? owner = null, string? due = "2026-11-03", Guid? milestone = null) =>
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", new { name = "Synthetic Calendar drawing", projectDisciplineId = Data.ProjectDiscipline(project, "Civil"),
            deliverableTypeId = await Data.DeliverableType(), ownerId = owner ?? Data.User(TestData.Pm), dueDate = due, startDate = "2026-11-02", milestoneId = milestone })).Json(201);
    public async Task<JsonNode> Milestone(Guid project) => await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/milestones", new { name = "Synthetic Calendar milestone", milestoneType = "Design Submission", date = "2026-11-04" })).Json(201);
    public string State() => f.Db(db => JsonSerializer.Serialize(new {
        Tasks = db.Tasks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.DueDate, row.StartDate, row.OriginalDueDate, row.AssigneeId, row.Status, row.RowVersion }).ToArray(),
        Deliverables = db.Deliverables.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.DueDate, row.StartDate, row.OwnerId, row.Status, row.RowVersion }).ToArray(),
        Milestones = db.Milestones.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.Date, row.OriginalDate, row.RowVersion }).ToArray(),
        Audit = db.ActivityLog.Count(), Outbox = db.Outbox.Count(), Notices = db.Notifications.Count(), Mail = db.Emails.Count(), Receipts = db.Idempotency.Count(),
        Seen = db.WorkSeenAcknowledgements.Count(), SeenReceipts = db.WorkSeenCommandReceipts.Count(),
        Views = db.SavedViews.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.Filters, row.OwnerId, row.Scope, row.ProjectId }).ToArray()
    }));
    public async Task<Guid> Person(Guid supervisor, decimal capacity = 40) {
        var discipline = await Data.Discipline("Civil");
        return await f.DbAsync(async db => {
        var user = new AppUser { Id = Guid.NewGuid(), Email = $"calendar-{Guid.NewGuid():N}@example.test", DisplayName = "Synthetic Calendar person " + Guid.NewGuid().ToString("N")[..6],
            IsActive = true, SupervisorId = supervisor, WeeklyCapacityHours = capacity, HomeDisciplineId = discipline };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
        });
    }
    public async Task Leave(Guid person, string day, decimal? hours, string status = "Approved") => await f.DbAsync(async db => {
        db.ApprovedLeaves.Add(new ApprovedLeave { PersonId = person, SourceSystem = "SyntheticCalendar", ExternalId = Guid.NewGuid().ToString("N"),
            Status = status, FromDate = DateOnly.Parse(day), ThroughDate = DateOnly.Parse(day), HoursPerDay = hours }); return await db.SaveChangesAsync();
    });
}

public sealed class CalendarScreenTests : IDisposable
{
    readonly HubFactory f = new(); readonly CalendarScreenData c;
    public CalendarScreenTests() => c = new(f);
    public void Dispose() => f.Dispose();

    [Fact]
    public async Task Off_keeps_the_old_range_and_events_and_refuses_only_the_addition()
    {
        var project = await c.Data.Project(); await c.Flag("work_screens", false);
        var old = await (await f.As(TestData.Pm).GetAsync($"/api/v1/calendar?from=2026-11-02&to=2026-11-08&projectIds={project.Id}")).Json();
        Assert.Equal(new[] { "timeZone", "from", "to", "entries" }, old.AsObject().Select(pair => pair.Key));
        await c.Read(project.Id, expect: 404);
        await c.Preview("Task", Guid.NewGuid(), 1, project.Id, expect: 404);
    }

    [Fact]
    public async Task Filters_use_actual_source_assignee_owner_and_work_discipline_and_exclude_unmapped_kinds()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var alex = c.Data.User(TestData.Alex);
        var task = await c.Data.NewTask(project.Id, TestData.Pm, new { name = "Synthetic assigned deadline", assigneeId = alex, dueDate = "2026-11-02" });
        var drawing = await c.Deliverable(project.Id, c.Data.User(TestData.Marc)); var milestone = await c.Milestone(project.Id);
        var ev = await (await f.As(TestData.Alex).Post("/api/v1/calendar/events", new { type = "Meeting", title = "Synthetic Alex event", projectId = project.Id,
            start = "2026-11-03T09:00", end = "2026-11-03T10:00", visibility = "Project" })).Json(201);
        var before = c.State(); var person = await c.Read(project.Id, $"&personId={alex}");
        Assert.Equal(new[] { task.G("id"), ev.G("id") }.Order(), person["range"]!["entries"]!.AsArray().Select(row => row!.G("id")).Order());
        var work = await c.Read(project.Id, $"&disciplineId={await c.Data.Discipline("Civil")}");
        Assert.Equal(new[] { task.G("id"), drawing.G("id") }.Order(), work["range"]!["entries"]!.AsArray().Select(row => row!.G("id")).Order());
        Assert.DoesNotContain(milestone.G("id").ToString(), work["range"]!.ToJsonString()); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Explicit_empty_types_is_not_all_and_tray_pages_have_exact_current_visible_totals()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project();
        for (var index = 0; index < 3; index++) await c.Data.NewTask(project.Id, TestData.Pm, new { name = "Synthetic undated " + index });
        var dated = await c.Data.NewTask(project.Id, TestData.Pm, new { name = "Synthetic visible dated source", dueDate = "2026-11-04" });
        var owner = c.Data.User(TestData.Pm); await c.Leave(owner, "2026-11-04", null); var before = c.State();
        var omitted = await c.Read(project.Id, "&showAway=true");
        Assert.Contains(omitted["range"]!["entries"]!.AsArray(), row => row!.G("id") == dated.G("id")); Assert.Equal(3, omitted["undated"]!.I("totalCount"));
        foreach (var wire in new[] { "&types=none", "&types=" })
        {
            var hidden = await c.Read(project.Id, wire + "&showAway=true");
            Assert.Empty(hidden["range"]!["entries"]!.AsArray()); Assert.Equal(0, hidden["undated"]!.I("totalCount"));
            Assert.Contains(hidden["away"]!["days"]!.AsArray(), row => row!.G("personId") == owner && row.S("date") == "2026-11-04");
        }
        foreach (var mixed in new[] { "none,Deadline", "Deadline,none", "none,unknown", "unknown" }) await c.Read(project.Id, "&types=" + mixed, expect: 400);
        var first = await c.Read(project.Id, "&types=Deadline&undatedPageSize=2"); var second = await c.Read(project.Id, "&types=Deadline&undatedPageSize=2&undatedPage=2");
        Assert.Equal(3, first["undated"]!.I("totalCount")); Assert.Equal(2, first["undated"]!["items"]!.AsArray().Count); Assert.Single(second["undated"]!["items"]!.AsArray());
        Assert.Equal(3, first["undated"]!["items"]!.AsArray().Concat(second["undated"]!["items"]!.AsArray()).Select(row => row!.G("id")).Distinct().Count());
        await c.Read(project.Id, "&undatedPage=2147483647&undatedPageSize=100", expect: 400);
        Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Away_uses_the_Planner_cohort_not_broader_supervisor_availability_and_never_discloses_import_details()
    {
        await c.Flag("work_screens", true); await c.Flag("capacity_screens", false); var project = await c.Data.Project();
        var report = await c.Person(c.Data.User(TestData.Sam)); var hidden = await c.Person(c.Data.User(TestData.Diane));
        await c.Leave(report, "2026-11-04", null); await c.Leave(report, "2026-11-05", 4); await c.Leave(hidden, "2026-11-04", null);
        var before = c.State(); var read = await c.Read(project.Id, "&types=&showAway=true", TestData.Sam);
        Assert.Equal("Available", read["away"]!.S("state")); var days = read["away"]!["days"]!.AsArray();
        Assert.Contains(days, row => row!.G("personId") == report && row.S("date") == "2026-11-04" && row.S("state") == "Full");
        Assert.Contains(days, row => row!.G("personId") == report && row.S("date") == "2026-11-05" && row["hours"]!.GetValue<decimal>() == 4 && row.S("state") == "Reduced");
        Assert.DoesNotContain(hidden.ToString(), read.ToJsonString()); Assert.DoesNotContain("SyntheticCalendar", read.ToJsonString());
        Assert.DoesNotContain("externalId", read.ToJsonString()); Assert.DoesNotContain("reason", read["away"]!.ToJsonString(), StringComparison.OrdinalIgnoreCase); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Zero_capacity_is_unknown_unverified_is_labelled_and_nonworking_days_are_not_fabricated_full_absences()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var supervisor = c.Data.User(TestData.Sam);
        var zero = await c.Person(supervisor, 0); var normal = await c.Person(supervisor);
        await c.Leave(zero, "2026-11-04", null); await c.Leave(normal, "2026-11-05", 4, "Unverified"); await c.Leave(normal, "2026-11-08", null);
        var read = await c.Read(project.Id, "&showAway=true", TestData.Sam); var rows = read["away"]!["days"]!.AsArray();
        var unknown = Assert.Single(rows, row => row!.G("personId") == zero)!; Assert.Equal("Unknown", unknown.S("state")); Assert.Null(unknown["hours"]);
        Assert.Contains(rows, row => row!.G("personId") == normal && row.S("date") == "2026-11-05" && row["unverified"]!.GetValue<bool>());
        Assert.DoesNotContain(rows, row => row!.G("personId") == normal && row.S("date") == "2026-11-08");
    }

    [Fact]
    public async Task Calendar_views_reuse_personal_and_single_project_shared_ACL_keep_definitions_off_and_recompute_for_each_reader()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project();
        await c.Data.NewTask(project.Id, TestData.Pm, new { dueDate = "2026-11-02", assigneeId = c.Data.User(TestData.Alex) });
        var parameters = new Dictionary<string, string> { ["projects"] = project.Id.ToString(), ["mode"] = "month", ["showAway"] = "true", ["disciplineId"] = (await c.Data.Discipline("Civil")).ToString() };
        var saved = await (await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Synthetic shared Calendar", listType = "calendar", projectId = project.Id, scope = "Project", @params = parameters })).Json(201);
        var read = await (await f.As(TestData.Marc).GetAsync($"/api/v1/views?listType=calendar&projectId={project.Id}")).Json();
        var row = Assert.Single(read["views"]!.AsArray(), row => row!.G("id") == saved.G("id"))!;
        Assert.Equal("month", row["params"]!.S("mode")); Assert.Equal("true", row["params"]!.S("showAway"));
        await (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Refused shared Calendar", listType = "calendar", projectId = project.Id, scope = "Project", @params = parameters })).Json(403);
        var personal = await (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Synthetic personal Calendar", listType = "calendar", scope = "Personal", @params = parameters })).Json(201);
        await c.Flag("work_screens", false); await (await f.As(TestData.Pm).GetAsync($"/api/v1/views?listType=calendar&projectId={project.Id}")).Json(404);
        await c.Flag("work_screens", true); var restored = await (await f.As(TestData.Alex).GetAsync("/api/v1/views?listType=calendar")).Json();
        Assert.Contains(restored["views"]!.AsArray(), row => row!.G("id") == personal.G("id"));
        Assert.Equal(2, f.Db(db => db.SavedViews.Count(view => view.Id == saved.G("id") || view.Id == personal.G("id"))));
    }

    [Fact]
    public async Task Shared_views_refuse_mismatched_projects_and_private_workspace_definitions_without_persisting()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var other = await c.Data.Project(); var before = c.State();
        foreach (var parameters in new[] {
            new Dictionary<string, string> { ["projects"] = other.Id.ToString() },
            new Dictionary<string, string> { ["projects"] = project.Id.ToString() + "," + other.Id },
            new Dictionary<string, string> { ["projects"] = project.Id.ToString(), ["ws"] = Guid.NewGuid().ToString() }
        }) await (await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Refused Calendar definition", listType = "calendar", projectId = project.Id, scope = "Project", @params = parameters })).Json(400);
        Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Calendar_view_update_delete_off_guards_and_current_visibility_clean_definitions_without_rewriting_stored_filters()
    {
        await c.Flag("work_screens", true); await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var project = await c.Data.Project(); var civil = await c.Data.Discipline("Civil"); var alex = c.Data.User(TestData.Alex);
        // Project creation ignores an unknown visibility member; use the real restricted-visibility command first.
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { visibility = Visibility.Restricted }, c.Data.Version(project.Id))).Json();
        Assert.Equal(Visibility.Restricted, (await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{project.Id}")).Json()).S("visibility"));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}")).Json();
        var parameters = new Dictionary<string, string> { ["projects"] = project.Id.ToString(), ["mode"] = "month", ["disciplineId"] = civil.ToString(), ["showAway"] = "true" };
        var personal = await (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Synthetic retained Calendar", listType = "calendar", scope = "Personal", @params = parameters })).Json(201);
        var shared = await (await f.As(TestData.Pm).Post("/api/v1/views", new { name = "Synthetic current Calendar ACL", listType = "calendar", scope = "Project", projectId = project.Id, @params = parameters })).Json(201);
        var filters = await f.DbAsync(db => db.SavedViews.Where(row => row.Id == personal.G("id")).Select(row => row.Filters).SingleAsync());
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(row => row.ProjectId == project.Id && row.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        Assert.False(f.Db(db => db.ProjectMembers.Any(row => row.ProjectId == project.Id && row.UserId == alex && row.RemovedAt == null)));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{project.Id}")).Json(404);
        var before = c.State(); var read = await (await f.As(TestData.Alex).GetAsync("/api/v1/views?listType=calendar")).Json();
        var row = Assert.Single(read["views"]!.AsArray(), row => row!.G("id") == personal.G("id"))!;
        Assert.Contains("projects", row["dropped"]!.AsArray().Select(value => value!.GetValue<string>())); Assert.Null(row["params"]!["projects"]);
        Assert.Equal(civil.ToString(), row["params"]!.S("disciplineId")); Assert.Equal(before, c.State());
        Assert.Equal(filters, await f.DbAsync(db => db.SavedViews.Where(row => row.Id == personal.G("id")).Select(row => row.Filters).SingleAsync()));
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/views?listType=calendar&projectId={project.Id}")).Json(404);
        await c.Flag("work_screens", false);
        await (await f.As(TestData.Alex).Patch($"/api/v1/views/{personal.G("id")}", new { name = "Refused off edit" }, personal.I("rowVersion"))).Json(404);
        await (await f.As(TestData.Alex).DeleteAsync($"/api/v1/views/{personal.G("id")}")).Json(404);
        await c.Flag("work_screens", true);
        var edited = await (await f.As(TestData.Alex).Patch($"/api/v1/views/{personal.G("id")}", new { name = "Actual updated Calendar", @params = new Dictionary<string, string> { ["mode"] = "agenda", ["disciplineId"] = civil.ToString() } }, personal.I("rowVersion"))).Json();
        await (await f.As(TestData.Alex).Patch($"/api/v1/views/{personal.G("id")}", new { name = "Stale preference edit" }, personal.I("rowVersion"))).Json(409);
        Assert.True(edited.I("rowVersion") > personal.I("rowVersion"));
        Assert.Equal(204, (int)(await f.As(TestData.Alex).DeleteAsync($"/api/v1/views/{personal.G("id")}")).StatusCode);
        Assert.False(f.Db(db => db.SavedViews.Any(row => row.Id == personal.G("id")))); Assert.True(f.Db(db => db.SavedViews.Any(row => row.Id == shared.G("id"))));
    }

    [Fact]
    public async Task Calendar_view_parameters_reject_invalid_modes_dates_layers_and_nonexistent_people_without_new_preferences()
    {
        await c.Flag("work_screens", true); var before = c.State();
        foreach (var pair in new[] { ("mode", "year"), ("date", "2026-02-30"), ("hide", "Deadline,made-up"), ("showAway", "maybe"), ("personId", Guid.NewGuid().ToString()), ("disciplineId", Guid.NewGuid().ToString()) })
            await (await f.As(TestData.Alex).Post("/api/v1/views", new { name = "Refused invalid Calendar view", listType = "calendar", scope = "Personal", @params = new Dictionary<string, string> { [pair.Item1] = pair.Item2 } })).Json(400);
        Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Absence_overlap_is_not_summed_reconciled_legacy_is_not_counted_twice_and_office_holiday_is_not_full_away()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var person = await c.Person(c.Data.User(TestData.Sam));
        await c.Leave(person, "2026-11-04", 4); await c.Leave(person, "2026-11-04", 2, "Unverified");
        await c.Leave(person, "2026-11-05", 4); await c.Leave(person, "2026-11-06", null);
        await f.DbAsync(async db => {
            var reconciled = await db.ApprovedLeaves.SingleAsync(row => row.PersonId == person && row.FromDate == new DateOnly(2026, 11, 5));
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = person, WorkDate = new(2026, 11, 5), Category = AvailabilityCategory.Unavailable, AvailableHours = 0, ReconciledLeaveId = reconciled.Id, RowVersion = 1 });
            var office = new Office { Name = "Synthetic Calendar holiday office", Code = "SC" + Guid.NewGuid().ToString("N")[..6] }; db.Offices.Add(office);
            (await db.Users.SingleAsync(row => row.Id == person)).OfficeId = office.Id;
            db.Holidays.Add(new Holiday { Name = "Synthetic Calendar office holiday", Date = new(2026, 11, 6), OfficeId = office.Id }); return await db.SaveChangesAsync();
        });
        var before = c.State(); var read = await c.Read(project.Id, $"&showAway=true&personId={person}", TestData.Sam); var days = read["away"]!["days"]!.AsArray();
        Assert.Equal(4, Assert.Single(days, row => row!.S("date") == "2026-11-04")!["hours"]!.GetValue<decimal>());
        Assert.Equal("Reduced", Assert.Single(days, row => row!.S("date") == "2026-11-05")!.S("state"));
        Assert.DoesNotContain(days, row => row!.S("date") == "2026-11-06" && row.S("state") == "Full"); Assert.Equal(before, c.State());
    }

    [Fact]
    public async Task Post_DST_events_keep_organization_local_and_instant_fields_while_deadlines_remain_DateOnly()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project();
        var task = await c.Data.NewTask(project.Id, TestData.Pm, new { dueDate = "2026-11-02" });
        var ev = await (await f.As(TestData.Pm).Post("/api/v1/calendar/events", new { type = "Meeting", title = "Synthetic post-DST event", projectId = project.Id,
            start = "2026-11-02T09:00", end = "2026-11-02T10:00", visibility = "Project" })).Json(201);
        var read = await c.Read(project.Id); var entries = read["range"]!["entries"]!.AsArray();
        Assert.Equal("2026-11-02", Assert.Single(entries, row => row!.G("id") == task.G("id"))!.S("date"));
        var timed = Assert.Single(entries, row => row!.G("id") == ev.G("id"))!;
        Assert.Equal("2026-11-02T09:00", timed.S("start")); Assert.Equal(DateTimeOffset.Parse("2026-11-02T13:00:00Z"), DateTimeOffset.Parse(timed.S("startAt")));
    }

    [Fact]
    public async Task Actual_self_Planner_read_allows_self_away_but_a_visible_business_owner_does_not_grant_peer_absence()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); var alex = c.Data.User(TestData.Alex); var marc = c.Data.User(TestData.Marc);
        await c.Deliverable(project.Id, marc); await c.Leave(alex, "2026-11-04", null); await c.Leave(marc, "2026-11-04", null);
        var actualSelf = await (await f.As(TestData.Alex).GetAsync($"/api/v1/planning/grid?from=2026-11-02&weeks=1&personId={alex}")).Json();
        Assert.Contains(actualSelf["people"]!.AsArray(), row => row!.G("id") == alex);
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/planning/grid?from=2026-11-02&weeks=1&personId={marc}")).Json(403);
        var self = await c.Read(project.Id, $"&showAway=true&personId={alex}", TestData.Alex);
        Assert.Contains(self["away"]!["days"]!.AsArray(), row => row!.G("personId") == alex && row.S("state") == "Full");
        var peer = await c.Read(project.Id, $"&showAway=true&personId={marc}", TestData.Alex);
        Assert.Contains(peer["range"]!["entries"]!.AsArray(), row => row!.S("sourceType") == "Deliverable");
        Assert.Equal("Unavailable", peer["away"]!.S("state")); Assert.Empty(peer["away"]!["days"]!.AsArray());
        var globalPlanner = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?from=2026-11-02&weeks=1&personId={marc}")).Json();
        Assert.Contains(globalPlanner["people"]!.AsArray(), row => row!.G("id") == marc);
        Assert.Contains((await c.Read(project.Id, $"&showAway=true&personId={marc}", TestData.Admin))["away"]!["days"]!.AsArray(), row => row!.G("personId") == marc);
    }

    [Theory]
    [InlineData(TestData.Diane)]
    [InlineData(TestData.Rita)]
    public async Task Unit_and_read_only_unit_leaders_get_the_actual_Planner_cohort_without_peer_or_write_authority(string viewer)
    {
        await c.Flag("work_screens", true); await c.Flag("org_structure", true); var project = await c.Data.Project();
        var person = await c.Person(c.Data.User(TestData.Sam)); var hidden = await c.Person(c.Data.User(TestData.Lena)); var leader = c.Data.User(viewer);
        var marker = await c.Data.NewTask(project.Id, TestData.Pm, new { assigneeId = person, dueDate = "2026-11-04" });
        await f.DbAsync(async db => {
            var level = await db.Set<OrgLevel>().FirstAsync(); var unit = new OrgUnit { Name = "Synthetic Calendar led unit", LevelId = level.Id, LeaderId = leader };
            db.Set<OrgUnit>().Add(unit); await db.SaveChangesAsync(); (await db.Users.SingleAsync(row => row.Id == person)).UnitId = unit.Id; return await db.SaveChangesAsync();
        });
        await c.Leave(person, "2026-11-04", null); await c.Leave(hidden, "2026-11-04", null);
        var planner = await (await f.As(viewer).GetAsync($"/api/v1/planning/grid?from=2026-11-02&weeks=1&personId={person}")).Json();
        Assert.Contains(planner["people"]!.AsArray(), row => row!.G("id") == person);
        await (await f.As(viewer).GetAsync($"/api/v1/planning/grid?from=2026-11-02&weeks=1&personId={hidden}")).Json(404);
        var read = await c.Read(project.Id, "&types=Deadline&showAway=true", viewer);
        Assert.Contains(read["away"]!["days"]!.AsArray(), row => row!.G("personId") == person); Assert.DoesNotContain(hidden.ToString(), read.ToJsonString());
        if (viewer == TestData.Rita) Assert.False(Assert.Single(read["deadlines"]!.AsArray(), row => row!.G("id") == marker.G("id"))!["canMove"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Large_undated_cohort_keeps_exact_totals_and_choices_without_hydrating_permission_facts_for_every_candidate()
    {
        await c.Flag("work_screens", true); var project = await c.Data.Project(); await c.Data.NewTask(project.Id, TestData.Pm, new { assigneeId = c.Data.User(TestData.Alex) });
        var capture = new ReadCommands();
        using var app = f.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<HubDb>(options => options.AddInterceptors(capture))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Pm);
        var path = $"/api/v1/calendar/screen?from=2026-11-02&to=2026-11-08&projectIds={project.Id}&types=Deadline&undatedPageSize=1";
        await (await client.GetAsync(path)).Json(); // warm the existing settings/auth caches, not the data under test
        capture.Sql.Clear(); var small = await (await client.GetAsync(path)).Json(); var smallQueries = capture.Sql.Count;
        Assert.Equal(1, small["undated"]!.I("totalCount")); Assert.True(smallQueries > 0);
        var discipline = c.Data.ProjectDiscipline(project.Id, "Civil"); var alex = c.Data.User(TestData.Alex); var creator = c.Data.User(TestData.Pm);
        // Labelled synthetic scale setup in this disposable provider database; no operational rows or command engine is involved.
        await f.DbAsync(async db => {
            var p = await db.Projects.SingleAsync(row => row.Id == project.Id); var first = p.NextTaskSeq;
            for (var index = 0; index < 250; index++) db.Tasks.Add(new WorkTask { ProjectId = p.Id, ProjectDisciplineId = discipline,
                Seq = first + index, Key = $"{p.ProjectNumber}-T{first + index:0000}", Name = "Synthetic undated scale " + index, AssigneeId = alex, CreatedBy = creator, Status = TaskStatuses.NotStarted });
            p.NextTaskSeq += 250; return await db.SaveChangesAsync();
        });
        capture.Sql.Clear(); var large = await (await client.GetAsync(path)).Json(); var largeQueries = capture.Sql.Count;
        Assert.Equal(251, large["undated"]!.I("totalCount")); Assert.Single(large["undated"]!["items"]!.AsArray()); Assert.Equal(smallQueries, largeQueries);
        Assert.Equal(small["people"]!.ToJsonString(), large["people"]!.ToJsonString()); Assert.Equal(small["disciplines"]!.ToJsonString(), large["disciplines"]!.ToJsonString());
    }

    sealed class ReadCommands : DbCommandInterceptor
    {
        internal readonly List<string> Sql = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        { Sql.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
