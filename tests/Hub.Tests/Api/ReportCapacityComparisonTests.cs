using System.IO.Compression;
using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Member = Hub.Api.Features.ReportEndpoints.CapacityComparisonMember;

namespace Hub.Tests.Api;

public sealed class ReportCapacityComparisonProjectionTests
{
    static readonly DateOnly Monday = new(2026, 11, 2);
    static readonly DateTimeOffset AsOf = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    static readonly Guid Civil = Guid.Parse("00000000-0000-0000-0000-000000000001");
    static Member Person(decimal capacity = 40, decimal confirmed = 45, decimal expected = 5, decimal possible = 90, bool partial = false, bool provisional = false) =>
        new(Guid.NewGuid(), Civil, capacity, confirmed, expected, possible, partial, provisional);
    static ReportEndpoints.ReportCapacityComparison Project(IReadOnlyList<Member>[] people, string unit = "hours", string period = "weeks", decimal? standard = 40,
        IReadOnlyDictionary<Guid, string>? names = null) => ReportEndpoints.ProjectCapacityComparison(Enumerable.Range(0, people.Length).Select(index => Monday.AddDays(index * 7)).ToArray(),
            people, names ?? new Dictionary<Guid, string> { [Civil] = "Civil" }, unit, period, standard, AsOf, "CompleteForVisibleParameters");

    [Fact]
    public void Demand_uses_confirmed_expected_without_possible_or_approved_duplication_and_clamps_only_after_group_sum()
    {
        var row = Assert.Single(Project([new[] { Person() }]).Rows);
        Assert.Equal((40m, 50m, 10m, 90m), (row.Capacity.Value, row.Demand.Value, row.Deficit.Value, row.Possible.Value));
        var shared = Assert.Single(Project([new[] { Person(40, 60, 0, 0), Person(40, 20, 0, 0) }]).Rows);
        Assert.Equal(0m, shared.Deficit.Value); // no summing individual shortages while the discipline has spare supply
    }

    [Fact]
    public void Month_sums_positive_weekly_group_shortfalls_without_netting_spare_weeks_and_converts_once()
    {
        var person = Person(40, 50, 0, 0);
        var result = Project([new[] { person }, new[] { person with { Confirmed = 30 } }], "fte", "months");
        var row = Assert.Single(result.Rows); Assert.Equal((1m, 1m, .125m, 10m), (row.Capacity.Value, row.Demand.Value, row.Deficit.Value, row.Deficit.KnownHours));
        Assert.Equal((2, 80m), (result.Periods[0].WeekCount, result.Periods[0].DenominatorHours));
        var tiny = Assert.Single(Project([new[] { Person(0, .025m, 0, 0) }], "fte").Rows); Assert.Equal(.000625m, tiny.Deficit.Value);
    }

    [Fact]
    public void Horizon_partial_is_conservative_in_every_included_week_while_provisional_only_affects_its_availability_week()
    {
        var person = Person(partial: true, provisional: true);
        var result = Project([new[] { person }, new[] { person with { Partial = false, Provisional = false } }]);
        Assert.All(result.Rows, row => { Assert.Null(row.Demand.Value); Assert.Null(row.Deficit.Value); Assert.Null(row.Deficit.KnownHours); Assert.Equal(1, row.PartialPeople); });
        Assert.Equal("ProvisionalAvailability", result.Rows[0].Capacity.Coverage); Assert.Null(result.Rows[0].Capacity.Value); Assert.Equal(40m, result.Rows[0].Capacity.KnownHours);
        Assert.Equal(40m, result.Rows[1].Capacity.Value); Assert.Equal(0, result.Rows[1].ProvisionalPeople);
        var month = Assert.Single(Project([new[] { person }, new[] { person with { Provisional = false } }], period: "months").Rows);
        Assert.Null(month.Deficit.Value); Assert.Null(month.Deficit.KnownHours); Assert.Equal((1, 1), (month.PartialPeople, month.ProvisionalPeople));
    }

    [Fact]
    public void Expanded_row_cap_retains_whole_stably_ordered_identity_groups_with_true_and_returned_counts()
    {
        var people = Enumerable.Range(1, 1001).Select(index => Person() with { DisciplineId = new Guid(index, 0, 0, new byte[8]) }).ToArray();
        var names = people.ToDictionary(person => person.DisciplineId!.Value, _ => "Same label");
        var result = Project([people, people], names: names);
        Assert.Equal((1000, 1001, true, 2000), (result.ReturnedGroups, result.TotalGroups, result.Truncated, result.Rows.Count));
        Assert.All(result.Rows.GroupBy(row => row.Key), group => Assert.Equal(new[] { "2026-11-02", "2026-11-09" }, group.Select(row => row.PeriodKey)));
        var again = Project([people.Reverse().ToArray(), people.Reverse().ToArray()], names: names);
        Assert.Equal(result.Rows.Select(row => (row.Key, row.PeriodKey)), again.Rows.Select(row => (row.Key, row.PeriodKey)));
        Assert.Equal((1001, false), (Project([people, people], period: "months", names: names).ReturnedGroups, Project([people, people], period: "months", names: names).Truncated));
        var unset = Project([new[] { people[0], Person() with { DisciplineId = null } }], names: names);
        Assert.Equal(2, unset.TotalGroups); Assert.Contains(unset.Rows, row => row.Key == "discipline:none" && row.DisciplineId is null);
    }

    [Fact]
    public void Unknown_standard_and_unknown_source_do_not_turn_into_zero_or_an_inferred_deficit_subtotal()
    {
        var person = Person(); var fte = Assert.Single(Project([new[] { person }], "fte", standard: 0).Rows);
        Assert.Null(fte.Deficit.Value); Assert.Equal((10m, "StandardUnavailable"), (fte.Deficit.KnownHours, fte.Deficit.Coverage));
        Assert.Equal(10m, Assert.Single(Project([new[] { person }], standard: null).Rows).Deficit.Value);
        var missing = Assert.Single(Project([new[] { person with { Capacity = null } }]).Rows);
        Assert.Null(missing.Capacity.Value); Assert.Null(missing.Deficit.Value); Assert.Null(missing.Deficit.KnownHours);
        Assert.Empty(Project([]).Rows);
    }

    [Fact]
    public void Incompatible_or_duplicate_canonical_inputs_refuse_instead_of_being_silently_double_counted()
    {
        var person = Person(); Assert.Throws<ArgumentException>(() => Project([new[] { person, person }]));
        Assert.Throws<ArgumentException>(() => Project([new[] { person with { Confirmed = -1 } }]));
        Assert.Throws<ArgumentException>(() => Project([new[] { person }], "percent"));
    }
}

/// Private HubFactory data, normal commands, no operational records or direct workflow mutations.
public sealed class ReportCapacityComparisonTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    static readonly DateOnly Monday = new(2026, 11, 2);
    public ReportCapacityComparisonTests() { d = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(string key, bool on = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value = on })).EnsureSuccessStatusCode();
    async Task Enable() { await Flag("capacity_reports"); await Flag("capacity_screens"); }
    Task<JsonNode> Run(string query = "from=2026-11-02&weeks=1", string viewer = TestData.Sam, string code = "team-capacity", int status = 200) =>
        f.As(viewer).GetAsync($"/api/v1/reports/{code}?{query}").Result.Json(status);
    static JsonNode Chart(JsonNode report) => report["capacityComparison"]!;
    static JsonNode Row(JsonNode report, Guid discipline, string period = "2026-11-02") => Chart(report)["rows"]!.AsArray().Single(row => row!["disciplineId"] is not null && row.G("disciplineId") == discipline && row.S("periodKey") == period)!;
    static decimal Value(JsonNode row, string field) => row[field]!["value"]!.GetValue<decimal>();
    async Task<Guid> Person(Guid discipline, decimal capacity = 40)
    {
        var person = (await (await f.As($"comparison-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
        var version = f.Db(db => db.Users.Single(user => user.Id == person).RowVersion);
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{person}", new { supervisorId = d.User(TestData.Sam), weeklyCapacityHours = capacity, homeDisciplineId = discipline }, version)).Json();
        return person;
    }
    Task<JsonNode> Plan(Guid person, decimal hours, string confidence = PlanningConfidence.Confirmed, DateOnly? week = null, Guid? project = null, string viewer = TestData.Sam) =>
        f.As(viewer).Post("/api/v1/planning/entries", new { personId = person, projectId = project, label = "Synthetic comparison plan", sourceCategory = project is null ? PlanningSource.Admin : PlanningSource.MajorProject,
            confidence, visibility = PlanningVisibility.Published, hoursPerWeek = hours, startWeek = week ?? Monday, endWeek = week ?? Monday }).Result.Json(201);

    [Fact]
    public async Task Real_Civil_row_has_exact_ten_hour_deficit_from_same_canonical_grid_and_no_person_payload()
    {
        var civil = await d.Discipline("Civil"); var person = await Person(civil); await Enable();
        await Plan(person, 45); await Plan(person, 5, PlanningConfidence.Expected); await Plan(person, 70, PlanningConfidence.Possible);
        var report = await Run(); var row = Row(report, civil);
        Assert.Equal((40m, 50m, 10m, 70m), (Value(row, "capacity"), Value(row, "demand"), Value(row, "deficit"), Value(row, "possible")));
        var grid = await (await f.As(TestData.Sam).GetAsync("/api/v1/planning/grid?from=2026-11-02&weeks=1")).Json();
        var canonical = grid["people"]!.AsArray().Single(item => item!.G("id") == person)!["cells"]![0]!;
        Assert.Equal(canonical["capacity"]!.GetValue<decimal>(), Value(row, "capacity"));
        Assert.Equal(-canonical["remaining"]!.GetValue<decimal>(), Value(row, "deficit"));
        Assert.Equal("PLN09ConfirmedExpected", Chart(report).S("basis")); Assert.Equal("homeDiscipline", Chart(report).S("groupBy"));
        Assert.DoesNotContain(person.ToString(), Chart(report).ToJsonString()); Assert.DoesNotContain("Synthetic comparison plan", Chart(report).ToJsonString());
        Assert.Equal(report["taskInputs"]!["asOf"]!.GetValue<DateTimeOffset>(), Chart(report)["asOf"]!.GetValue<DateTimeOffset>());
    }

    [Fact]
    public async Task Historical_unit_membership_and_parent_descendant_filter_match_the_existing_actual_measure_cohort()
    {
        await Enable(); await Flag("org_structure"); var civil = await d.Discipline("Civil"); var person = await Person(civil);
        var admin = f.As(TestData.Admin); var levels = (await (await admin.GetAsync("/api/v1/admin/organisation/levels")).Json()).AsArray();
        var region = levels.First(level => level!.S("name") == "Region")!.G("id"); var team = levels.First(level => level!.S("name") == "Team")!.G("id");
        var root = (await (await admin.Post("/api/v1/admin/organisation/units", new { name = "Synthetic comparison region", levelId = region })).Json(201)).G("id");
        async Task<Guid> Unit(string name) => (await (await admin.Post("/api/v1/admin/organisation/units", new { name, levelId = team, parentId = root })).Json(201)).G("id");
        var first = await Unit("Synthetic comparison first"); var second = await Unit("Synthetic comparison second");
        async Task Place(Guid unit) => await (await admin.Patch($"/api/v1/admin/organisation/users/{person}/placement", new { unitId = unit }, f.Db(db => db.Users.Single(user => user.Id == person).RowVersion))).Json(204);
        await Place(first); await Plan(person, 50); f.Clock.SetDate(2026, 11, 9); await Place(second); await Plan(person, 50, week: Monday.AddDays(7));
        var former = await Run($"from=2026-11-02&weeks=2&unitId={first}", TestData.Admin);
        Assert.Equal((40m, 0m), (Value(Row(former, civil), "capacity"), Value(Row(former, civil, "2026-11-09"), "capacity")));
        Assert.Equal((10m, 0m), (Value(Row(former, civil), "deficit"), Value(Row(former, civil, "2026-11-09"), "deficit")));
        var parent = await Run($"from=2026-11-02&weeks=2&unitId={root}", TestData.Admin);
        Assert.Equal((40m, 40m), (Value(Row(parent, civil), "capacity"), Value(Row(parent, civil, "2026-11-09"), "capacity")));
        var empty = await Run($"from=2026-12-07&weeks=1&unitId={first}", TestData.Admin); Assert.Empty(Chart(empty)["rows"]!.AsArray()); Assert.Equal(0, Chart(empty).I("returnedGroups"));
    }

    [Fact]
    public async Task Horizon_private_work_and_weekly_provisional_availability_remain_unknown_without_private_source_details()
    {
        await Enable(); var civil = await d.Discipline("Civil"); var person = await Person(civil);
        var hidden = await d.Project(); (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).Json();
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{hidden.Id}/members", new { userId = person, roles = new[] { ProjectRole.TeamMember }, primaryDisciplineId = d.ProjectDiscipline(hidden.Id, "Civil") })).Json(201);
        await Plan(person, 50); await Plan(person, 80, week: Monday.AddDays(7), project: hidden.Id, viewer: TestData.Admin);
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{person}/availability/2026-11-02", new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json();
        var report = await Run("from=2026-11-02&weeks=2"); var first = Row(report, civil); var next = Row(report, civil, "2026-11-09");
        foreach (var row in new[] { first, next }) { Assert.Null(row["demand"]!["value"]); Assert.Null(row["deficit"]!["value"]); Assert.Null(row["deficit"]!["knownHours"]); Assert.Equal(1, row.I("partialPeople")); }
        Assert.Equal("ProvisionalAvailability", first["capacity"]!.S("coverage")); Assert.Null(first["capacity"]!["value"]); Assert.Equal(36m, first["capacity"]!["knownHours"]!.GetValue<decimal>());
        Assert.Equal(40m, Value(next, "capacity")); Assert.Equal(0, next.I("provisionalPeople"));
        Assert.DoesNotContain(hidden.Id.ToString(), Chart(report).ToJsonString()); Assert.DoesNotContain(hidden.ProjectNumber, Chart(report).ToJsonString());
        var monthly = Row(await Run("from=2026-11-02&weeks=2&unit=fte&period=months"), civil, "2026-11"); Assert.Null(monthly["deficit"]!["value"]); Assert.Null(monthly["deficit"]!["knownHours"]);
    }

    [Fact]
    public async Task Capture_is_read_only_and_exports_keep_identical_matrix_bytes_and_no_chart_parameters()
    {
        await Enable(); var civil = await d.Discipline("Civil"); var person = await Person(civil); await Plan(person, 50);
        const string query = "from=2026-11-02&weeks=2&unit=fte&period=months";
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var path = $"/api/v1/reports/team-capacity/export?{query}&format={format}"; var before = await f.As(TestData.Sam).GetByteArrayAsync(path);
            var durable = f.Db(db => new { Plans = db.PlanningEntries.Count(), Activity = db.ActivityLog.Count(), Dates = db.PersonDateVersions.Count() });
            var report = await Run(query); var row = Row(report, civil, "2026-11"); Assert.Equal(.125m, Value(row, "deficit"));
            Assert.Equal(durable, f.Db(db => new { Plans = db.PlanningEntries.Count(), Activity = db.ActivityLog.Count(), Dates = db.PersonDateVersions.Count() }));
            var after = await f.As(TestData.Sam).GetByteArrayAsync(path);
            if (format == "csv") Assert.Equal(before, after);
            else
            {
                // ZIP entry timestamps use wall time. Compare every packaged part byte-for-byte instead.
                using var oldArchive = new ZipArchive(new MemoryStream(before));
                using var newArchive = new ZipArchive(new MemoryStream(after));
                var oldEntries = oldArchive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
                var newEntries = newArchive.Entries.OrderBy(entry => entry.FullName, StringComparer.Ordinal).ToArray();
                Assert.Equal(oldEntries.Select(entry => entry.FullName), newEntries.Select(entry => entry.FullName));
                for (var index = 0; index < oldEntries.Length; index++)
                {
                    using var oldPart = oldEntries[index].Open(); using var newPart = newEntries[index].Open();
                    using var oldBytes = new MemoryStream(); using var newBytes = new MemoryStream();
                    await oldPart.CopyToAsync(oldBytes); await newPart.CopyToAsync(newBytes);
                    Assert.Equal(oldBytes.ToArray(), newBytes.ToArray());
                }
            }
            Assert.False(report["savedParameters"]!.AsObject().ContainsKey("chartPeriod")); Assert.False(report["savedParameters"]!.AsObject().ContainsKey("chartPage"));
        }
    }

    [Fact]
    public async Task Owner_flags_permission_and_other_forms_do_not_expose_comparison_and_visual_flag_is_independent()
    {
        await Flag("capacity_reports"); var off = await Run(); Assert.False(off.AsObject().ContainsKey("capacityComparison"));
        await Flag("capacity_screens"); await Flag("visual_system", false); Assert.NotNull((await Run())["capacityComparison"]);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/reports/team-capacity")).StatusCode);
        Assert.False((await Run("", code: "workload-discipline")).AsObject().ContainsKey("capacityComparison"));
        Assert.False((await Run(code: "allocations-by-source")).AsObject().ContainsKey("capacityComparison"));
        await Flag("capacity_screens", false); Assert.True(JsonNode.DeepEquals(off, await Run()));
        await Flag("capacity_reports", false); await Run(status: 404);
    }
}
