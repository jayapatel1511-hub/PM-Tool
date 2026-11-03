using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// 053 S09 / AC-SCR-11 and S07 capacity-widget preference integration. Each case owns its database.
public sealed class HomeVisualCapacityTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    static readonly DateOnly Week = new(2026, 9, 14);
    public HomeVisualCapacityTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.home_overview", new { value = on })).EnsureSuccessStatusCode();
    async Task<JsonNode> Home(string user = TestData.Sam, string scope = "none", string extra = "") => await (await f.As(user).GetAsync($"/api/v1/home?projects={scope}{extra}")).Json();
    async Task<JsonNode> Grid(string user, string query = "") => await (await f.As(user).GetAsync($"/api/v1/planning/grid?from={Week:yyyy-MM-dd}&weeks=1&includeMyDrafts=true{query}")).Json();
    static decimal N(JsonNode node, string key) => node[key]!.GetValue<decimal>();
    PlanningEntry Entry(Guid person, decimal hours, string confidence = PlanningConfidence.Expected, Guid? owner = null,
        string visibility = PlanningVisibility.Published, Guid? project = null, string label = "Synthetic Home capacity plan") => new()
    {
        PersonId = person, HoursPerWeek = hours, StartWeek = Week, EndWeek = Week, Confidence = confidence,
        Visibility = visibility, ProjectId = project, Label = label, SourceCategory = project is null ? PlanningSource.Other : PlanningSource.MajorProject,
        CreatedBy = owner ?? d.User(TestData.Admin), CreatedAt = f.Clock.Now, LastValidatedAt = f.Clock.Now
    };
    static decimal Sum(JsonNode grid, string key) => grid["people"]!.AsArray().Sum(p => N(p!["cells"]![0]!, key));

    [Fact]
    public async Task Numeric_example_matches_one_canonical_planner_read_and_exact_group_drilldowns()
    {
        await Flag(true);
        var civil = await d.Discipline("Civil");
        var people = Enumerable.Range(0, 16).Select(i => new AppUser { Email = $"home-capacity-{Guid.NewGuid():N}@hub.test", DisplayName = $"Synthetic capacity {i:00}", WeeklyCapacityHours = 40.5m, HomeDisciplineId = civil }).ToArray();
        var owner = d.User(TestData.Admin);
        await f.DbAsync(async db =>
        {
            foreach (var user in await db.Users.ToListAsync()) user.WeeklyCapacityHours = 0;
            db.Users.AddRange(people);
            for (var i = 0; i < 10; i++) db.PlanningEntries.Add(Entry(people[i].Id, 50, i == 0 ? PlanningConfidence.Confirmed : PlanningConfidence.Expected, owner));
            db.PlanningEntries.Add(Entry(people[10].Id, 29, owner: owner));
            db.PlanningEntries.Add(Entry(people[15].Id, 13, PlanningConfidence.Possible, owner));
            foreach (var person in people.Take(6)) db.ApprovedLeaves.Add(new ApprovedLeave { PersonId = person.Id, SourceSystem = "SyntheticHomeCapacity", ExternalId = person.Id.ToString(), FromDate = Week, ThroughDate = Week, HoursPerDay = 6, Status = "Approved", SourceUpdatedAt = f.Clock.Now, ImportedAt = f.Clock.Now });
            return await db.SaveChangesAsync();
        });
        var body = await Home(TestData.Lena); // Home has no projects; capacity is independently scoped to people.
        Assert.Equal(0, body["metrics"]!.I("activeProjects"));
        var capacity = body["weeklyCapacity"]!; var totals = capacity["totals"]!;
        Assert.Equal(648m, N(totals, "grossHours")); Assert.Equal(36m, N(totals, "timeAwayHours"));
        Assert.Equal(612m, N(totals, "availableHours")); Assert.Equal(529m, N(totals, "plannedHours"));
        Assert.Equal(83m, N(totals, "freeHours")); Assert.Equal(86, totals.I("pct")); Assert.Equal(13m, N(totals, "possibleHours"));
        Assert.Equal(10, totals.I("above100People"));
        var grid = await Grid(TestData.Lena);
        Assert.Equal(Sum(grid, "capacity"), N(totals, "availableHours"));
        Assert.Equal(Sum(grid, "confirmed") + Sum(grid, "expected"), N(totals, "plannedHours"));
        Assert.Equal(Sum(grid, "remaining"), N(totals, "freeHours"));
        Assert.Equal(Sum(grid, "timeAway"), N(totals, "timeAwayHours"));
        Assert.Equal(grid["people"]!.AsArray().Count, totals.I("people"));
        var group = capacity["disciplines"]!.AsArray().Single(x => x!["disciplineId"]?.GetValue<Guid>() == civil)!;
        Assert.Equal(16, group["totals"]!.I("people")); Assert.Equal(83m, N(group["totals"]!, "freeHours"));
        var above = await (await f.As(TestData.Lena).GetAsync(capacity.S("above100Path").Replace("/planner?", "/api/v1/planning/grid?"))).Json();
        Assert.Equal(totals.I("above100People"), above["people"]!.AsArray().Count);
        var sameGroup = await (await f.As(TestData.Lena).GetAsync(group.S("plannerPath").Replace("/planner?", "/api/v1/planning/grid?"))).Json();
        Assert.Equal(16, sameGroup["people"]!.AsArray().Count);
        Assert.All(sameGroup["people"]!.AsArray(), p => Assert.Equal(civil, p!.G("homeDisciplineId")));
    }

    [Fact]
    public async Task Own_drafts_count_other_private_drafts_do_not_and_partial_free_is_unavailable_without_metadata()
    {
        await Flag(true);
        var sam = d.User(TestData.Sam); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill); var admin = d.User(TestData.Admin);
        var hidden = await d.Project();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == hidden.Id)).Visibility = Visibility.Restricted;
            db.PlanningEntries.AddRange(Entry(alex, 11, owner: admin), Entry(alex, 7, owner: sam, visibility: PlanningVisibility.Draft),
                Entry(alex, 33, owner: admin, visibility: PlanningVisibility.Draft, label: "OTHER PRIVATE DRAFT"),
                Entry(alex, 28, owner: admin, project: hidden.Id, label: "HIDDEN HOME PLAN"), Entry(alex, 13, PlanningConfidence.Possible, admin), Entry(jill, 17, owner: admin));
            return await db.SaveChangesAsync();
        });
        var body = await Home(); var totals = body["weeklyCapacity"]!["totals"]!; var grid = await Grid(TestData.Sam);
        Assert.Equal(35m, N(totals, "plannedHours")); Assert.Equal(13m, N(totals, "possibleHours"));
        Assert.Equal(1, totals.I("partialPeople")); Assert.Null(totals["freeHours"]);
        Assert.Equal(Sum(grid, "confirmed") + Sum(grid, "expected"), N(totals, "plannedHours"));
        Assert.Equal((int)Math.Round(100m * 35m / Sum(grid, "capacity"), MidpointRounding.AwayFromZero), totals.I("pct"));
        Assert.DoesNotContain(hidden.Id.ToString(), body.ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, body.ToJsonString());
        Assert.DoesNotContain("HIDDEN HOME PLAN", body.ToJsonString()); Assert.DoesNotContain("OTHER PRIVATE DRAFT", body.ToJsonString());
    }

    [Fact]
    public async Task Capacity_is_omitted_without_capability_or_flag_and_old_response_is_unchanged_after_rollback()
    {
        var before = await Home();
        Assert.Null(before["weeklyCapacity"]);
        await Flag(true);
        var plain = await Home(TestData.Alex); var readOnly = await Home(TestData.Rita);
        foreach (var body in new[] { plain, readOnly })
        {
            Assert.Null(body["weeklyCapacity"]);
            Assert.DoesNotContain(body["layout"]!.AsArray(), w => w!.S("id") == "weeklyCapacity");
        }
        Assert.NotNull((await Home())["weeklyCapacity"]);
        await Flag(false);
        var after = await Home(); before.AsObject().Remove("asOf"); after.AsObject().Remove("asOf");
        Assert.Equal(before.ToJsonString(), after.ToJsonString());
    }

    [Fact]
    public async Task Hidden_capacity_preference_survives_off_saves_and_returns_with_exact_saved_visibility()
    {
        await Flag(true); var sam = f.As(TestData.Sam);
        var widgets = new[] { new { id = "weeklyCapacity", hidden = true }, new { id = "metrics", hidden = false } };
        var saved = await (await sam.Put("/api/v1/me/dashboard-layout", new { widgets })).Json();
        Assert.True(saved["layout"]![0]!["hidden"]!.GetValue<bool>());
        await Flag(false);
        var legacy = await Home();
        Assert.DoesNotContain(legacy["layout"]!.AsArray(), w => w!.S("id") == "weeklyCapacity");
        await (await sam.Put("/api/v1/me/dashboard-layout", new { widgets = new[] { new { id = "metrics", hidden = false }, new { id = "upcomingDeadlines", hidden = true } } })).Json();
        await Flag(true);
        var back = await Home();
        var capacity = Assert.Single(back["layout"]!.AsArray().Where(w => w!.S("id") == "weeklyCapacity"))!;
        Assert.True(capacity["hidden"]!.GetValue<bool>());
        Assert.True(back["layout"]!.AsArray().Single(w => w!.S("id") == "upcomingDeadlines")!["hidden"]!.GetValue<bool>());
        var restored = await (await sam.DeleteAsync("/api/v1/me/dashboard-layout")).Json();
        Assert.Equal(new[] { "metrics", "tasksByProject", "tasksByStatus", "weeklyCapacity", "upcomingDeadlines" }, restored["layout"]!.AsArray().Select(w => w!.S("id")));
    }

    [Fact]
    public async Task Zero_or_provisional_capacity_never_produces_a_percentage_or_verified_free()
    {
        await Flag(true); var alex = d.User(TestData.Alex); var admin = d.User(TestData.Admin);
        await f.DbAsync(async db =>
        {
            foreach (var u in await db.Users.ToListAsync()) u.WeeklyCapacityHours = 0;
            db.PlanningEntries.Add(Entry(alex, 8, owner: admin));
            return await db.SaveChangesAsync();
        });
        var zero = (await Home())["weeklyCapacity"]!["totals"]!;
        Assert.Equal(0m, N(zero, "availableHours")); Assert.Null(zero["pct"]); Assert.Equal(-8m, N(zero, "freeHours"));
        Assert.Equal(1, zero.I("above100People"));
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == alex)).WeeklyCapacityHours = 40;
            db.ApprovedLeaves.Add(new ApprovedLeave { PersonId = alex, SourceSystem = "SyntheticHomeCapacity", ExternalId = Guid.NewGuid().ToString(), FromDate = Week, ThroughDate = Week, HoursPerDay = 4, Status = "Unverified", SourceUpdatedAt = f.Clock.Now, ImportedAt = f.Clock.Now });
            return await db.SaveChangesAsync();
        });
        var provisional = (await Home())["weeklyCapacity"]!["totals"]!;
        Assert.Equal(1, provisional.I("provisionalPeople")); Assert.Null(provisional["pct"]); Assert.Null(provisional["freeHours"]);
        Assert.Null(provisional["availableHours"]); Assert.Null(provisional["grossHours"]); Assert.Equal(8m, N(provisional, "plannedHours"));
        Assert.Equal(0, provisional.I("above100People"));
    }

    [Fact]
    public async Task Current_week_uses_organisation_date_and_ignores_Home_date_project_scope()
    {
        await Flag(true);
        f.Clock.Now = new DateTimeOffset(2026, 9, 21, 2, 30, 0, TimeSpan.Zero); // Sunday in Halifax, Monday in UTC.
        var body = await Home(extra: "&from=2027-01-01&to=2027-01-10&projectStatus=Setup");
        var capacity = body["weeklyCapacity"]!;
        Assert.Equal("2026-09-14", capacity.S("week")); Assert.Equal("2026-09-20", capacity.S("through"));
        Assert.Contains("from=2026-09-14", capacity.S("plannerPath")); Assert.DoesNotContain("projects=", capacity.S("plannerPath"));
        Assert.True(capacity["totals"]!.I("people") > 0); Assert.Equal(0, body["metrics"]!.I("activeProjects"));
    }

    [Fact]
    public void Missing_person_week_is_unknown_not_zero_and_exact_aggregation_precedes_percentage_rounding()
    {
        PlanningEndpoints.PersonRow Person(PlanningEndpoints.CellRow[] cells) => new(Guid.NewGuid(), "Synthetic aggregate input", null, null, 40,
            false, false, false, [], cells, [], [], null, null, null, false);
        var known = new PlanningEndpoints.CellRow(Week, 40, 0, 0, 0, 10, 0, 19, 30, 0, 0, false, false, 0, 0, 0, false);
        var result = WorkspaceEndpoints.CapacityTotals([Person([known]), Person([])], Week);
        Assert.Equal(2, result.People); Assert.Equal(1, result.MissingWeeks);
        Assert.Null(result.AvailableHours); Assert.Null(result.PlannedHours); Assert.Null(result.Pct); Assert.Null(result.FreeHours);
        var exact = WorkspaceEndpoints.CapacityTotals([Person([known with { Capacity = 3, Confirmed = 1, Remaining = 2 }]), Person([known with { Capacity = 3, Confirmed = 2, Remaining = 1 }])], Week);
        Assert.Equal(50, exact.Pct); Assert.Equal(3m, exact.FreeHours); Assert.Equal(38m, exact.PossibleHours);
    }
}
