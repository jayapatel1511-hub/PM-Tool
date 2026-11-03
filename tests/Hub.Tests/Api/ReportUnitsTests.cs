using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class ReportUnitsArithmeticTests
{
    static DateOnly[] November => [new(2026, 11, 2), new(2026, 11, 9), new(2026, 11, 16), new(2026, 11, 23)];
    static ReportEndpoints.ReportUnitCell Quantity(decimal[] hours, decimal? standard = 40m, string unit = "fte") =>
        ReportUnitMath.Quantity("period", hours.Select(value => (decimal?)value).ToArray(), hours, 0, unit, standard);

    [Fact]
    public void Four_exact_weeks_use_one_standard_denominator_for_rows_and_aggregate()
    {
        var period = Assert.Single(ReportUnitMath.Periods(November, "months", 40)); Assert.Equal(("2026-11", 4, 160m), (period.Key, period.WeekCount, period.DenominatorHours));
        Assert.Equal(1m, Quantity([40, 40, 40, 40]).Value); Assert.Equal(.75m, Quantity([30, 30, 30, 30]).Value);
        Assert.Equal(1.75m, Quantity([70, 70, 70, 70]).Value);
    }

    [Fact]
    public void Thursday_assigns_cross_month_and_year_weeks_and_partial_month_uses_only_displayed_weeks()
    {
        var periods = ReportUnitMath.Periods([new(2026, 11, 23), new(2026, 11, 30), new(2026, 12, 7)], "months", 40);
        Assert.Equal(new[] { "2026-11", "2026-12" }, periods.Select(period => period.Key)); Assert.Equal(new[] { 1, 2 }, periods.Select(period => period.WeekCount));
        Assert.Equal(80m, periods[1].DenominatorHours); Assert.Equal(1m, Quantity([40, 40]).Value);
        Assert.Equal("2026-01", Assert.Single(ReportUnitMath.Periods([new(2025, 12, 29)], "months", 40)).Key);
    }

    [Fact]
    public void Period_conversion_uses_sub_tenth_source_hours_and_does_not_sum_weekly_FTE()
    {
        var month = Quantity([.044m, .044m, .044m]); Assert.Equal(.132m, month.KnownHours); Assert.Equal(.0011m, month.Value);
        Assert.NotEqual(month.Value, new[] { .044m, .044m, .044m }.Sum(hours => Quantity([hours]).Value!.Value));
        Assert.Equal(-.0625m, Quantity([-5m, 0m]).Value); // exact negative remaining hours are not clamped by a display transform
    }

    [Fact]
    public void Zero_or_unknown_standard_is_unavailable_and_known_zero_hours_remains_zero()
    {
        foreach (var standard in new decimal?[] { null, 0m, -1m })
        {
            var fte = Quantity([40, 40], standard); Assert.Null(fte.Value); Assert.Equal(80m, fte.KnownHours); Assert.Equal("StandardUnavailable", fte.Coverage);
            Assert.Equal(80m, Quantity([40, 40], standard, "hours").Value);
        }
        Assert.Equal(0m, Quantity([0m, 0m]).Value);
    }

    [Fact]
    public void Unknown_hours_and_unestimated_inputs_keep_known_subtotals_and_coverage()
    {
        var unknown = ReportUnitMath.Quantity("period", [40m, null], [40m, 10m], 0, "fte", 40, "VisibleOnly");
        Assert.Null(unknown.Value); Assert.Equal(50m, unknown.KnownHours); Assert.Equal("KnownSubtotal", unknown.Coverage);
        var unestimated = ReportUnitMath.Quantity("period", [4m, 8m], [4m, 8m], 3, "hours", 40);
        Assert.Null(unestimated.Value); Assert.Equal((12m, 3), (unestimated.KnownHours, unestimated.MissingInputs));
        Assert.Equal("VisibleOnly", ReportUnitMath.Quantity("period", [4m], [4m], 0, "hours", 40, "VisibleOnly").Coverage);
    }

    [Fact]
    public void Invalid_periods_units_and_noncanonical_displayed_weeks_refuse()
    {
        Assert.Throws<ArgumentException>(() => ReportUnitMath.Periods(November, "days", 40));
        Assert.Throws<ArgumentException>(() => ReportUnitMath.Periods([November[0], November[0]], "months", 40));
        Assert.Throws<ArgumentException>(() => ReportUnitMath.Periods([November[1], November[0]], "months", 40));
        Assert.Throws<ArgumentException>(() => ReportUnitMath.Periods([new(2026, 11, 3)], "months", 40));
        Assert.Throws<ArgumentException>(() => Quantity([4], unit: "percentage")); Assert.Empty(ReportUnitMath.Periods([], "months", 40));
    }
}

/// Real authorised report reads, source precision, persistence/export and the independent dark flag.
public sealed class ReportUnitsTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public ReportUnitsTests() { d = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(string key, bool value = true) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Run(string code, string query = "", string who = TestData.Sam, int status = 200) => await (await f.As(who).GetAsync($"/api/v1/reports/{code}{(query.Length > 0 ? "?" + query : "")}")).Json(status);
    static JsonNode View(JsonNode report) => report["unitView"]!;
    static JsonNode Cell(JsonNode report, int row, string path) => View(report)["rows"]!.AsArray().Single(item => item!.I("rowIndex") == row)!["cells"]!.AsArray().Single(cell => cell!.S("path") == path)!;
    static string Query(JsonNode parameters) => string.Join("&", parameters.AsObject().Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value!.GetValue<string>())}"));
    async Task<(Guid Id, string Name)> Person(decimal capacity = 40m, Guid? office = null)
    {
        var me = await (await f.As($"unit-report-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json();
        var version = await f.DbAsync(db => db.Users.Where(user => user.Id == me.G("id")).Select(user => user.RowVersion).SingleAsync());
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{me.G("id")}", new { supervisorId = d.User(TestData.Sam), weeklyCapacityHours = capacity, officeId = office }, version)).Json();
        return (me.G("id"), me.S("displayName"));
    }
    static int PersonIndex(JsonNode report, string name) => report["rows"]!.AsArray().Select((row, index) => (row, index)).Single(item => item.row!.S("person") == name).index;
    async Task<(string[] Headers, string[][] Rows)> ExportRows(string code, string query, string format, string firstHeader)
    {
        var bytes = await f.As(TestData.Sam).GetByteArrayAsync($"/api/v1/reports/{code}/export?{query}&format={format}");
        string[][] records;
        if (format == "xlsx")
        {
            using var zip = new ZipArchive(new MemoryStream(bytes)); using var data = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            records = XDocument.Load(data).Descendants(ns + "row").Select(row => row.Elements(ns + "c").Select(cell => cell.Value).ToArray()).ToArray();
        }
        else records = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF').Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => {
            var cells = new List<string>(); var cell = new StringBuilder(); var quoted = false;
            for (var index = 0; index < line.Length; index++)
                if (line[index] == '"') { if (quoted && index + 1 < line.Length && line[index + 1] == '"') { cell.Append('"'); index++; } else quoted = !quoted; }
                else if (line[index] == ',' && !quoted) { cells.Add(cell.ToString()); cell.Clear(); } else cell.Append(line[index]);
            cells.Add(cell.ToString()); return cells.ToArray();
        }).ToArray();
        var header = Array.FindIndex(records, row => row[0] == firstHeader); Assert.True(header >= 0);
        return (records[header], records.Skip(header + 1).ToArray());
    }
    static string ExportValue((string[] Headers, string[][] Rows) table, string rowName, string column)
    {
        var index = Array.IndexOf(table.Headers, column); Assert.True(index >= 0, $"Missing export column: {column}");
        return Assert.Single(table.Rows.Where(row => row[0] == rowName))[index];
    }

    [Fact]
    public async Task Employee_month_FTE_uses_standard40_despite_custom32_and_exact_aggregate_while_workload_stays8_weeks()
    {
        var p = await d.Project(); var alice = await Person(); var bob = await Person(32);
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alice.Id, estimatedHours = 160m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = bob.Id, estimatedHours = 120m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        await Flag("capacity_screens"); var report = await Run("workload-employee", $"projectId={p.Id}&unit=fte&period=months");
        Assert.Equal(("fte", "months", 40m, 8), (View(report).S("unit"), View(report).S("period"), View(report)["standardWeeklyHours"]!.GetValue<decimal>(), View(report).I("displayedWeekCount")));
        var a = PersonIndex(report, alice.Name); var b = PersonIndex(report, bob.Name);
        Assert.Equal(1m, Cell(report, a, "u0")["value"]!.GetValue<decimal>()); Assert.Equal(.75m, Cell(report, b, "u0")["value"]!.GetValue<decimal>());
        Assert.Equal(1.75m, Cell(report, -1, "u0")["value"]!.GetValue<decimal>()); Assert.Equal(280m, Cell(report, -1, "u0")["knownHours"]!.GetValue<decimal>());
        Assert.Equal(("WeeklyRate", 32m), (Cell(report, b, "capacity").S("kind"), Cell(report, b, "capacity")["value"]!.GetValue<decimal>()));
        Assert.Equal(new[] { 4, 4 }, View(report)["periods"]!.AsArray().Select(period => period!.I("weekCount")));
        Assert.Equal(("fte", "months"), (report["savedParameters"]!.S("unit"), report["savedParameters"]!.S("period")));
        Assert.True(JsonNode.DeepEquals(report["savedParameters"], report["taskInputs"]!["parameters"]));
        await Run("workload-employee", "weeks=26&unit=fte", status: 400); await Run("workload-employee", "from=2026-11-30&period=months", status: 400);
        var csv = await f.As(TestData.Sam).GetByteArrayAsync($"/api/v1/reports/workload-employee/export?projectId={p.Id}&unit=fte&period=months&format=csv");
        Assert.Equal(new byte[] { 239, 187, 191 }, csv.Take(3)); var text = Encoding.UTF8.GetString(csv);
        Assert.Contains("Standard full-time hours per week,40", text); Assert.Contains("Displayed ISO weeks,8", text); Assert.Contains("1.75", text); Assert.Contains("Applied report parameters", text);
        using var zip = new ZipArchive(new MemoryStream(await f.As(TestData.Sam).GetByteArrayAsync($"/api/v1/reports/workload-employee/export?projectId={p.Id}&unit=fte&period=months&format=xlsx")));
        using var data = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()); Assert.Contains("<v>1.75</v>", await data.ReadToEndAsync());
        using var metadata = new StreamReader(zip.GetEntry("xl/worksheets/sheet2.xml")!.Open()); var basis = await metadata.ReadToEndAsync(); Assert.Contains("Standard full-time hours per week", basis); Assert.Contains("Displayed ISO weeks", basis);
    }

    [Fact]
    public async Task Employee_and_discipline_months_use_exact_sub_tenth_source_instead_of_preformatted_week_values()
    {
        var p = await d.Project(); var office = await d.Office(); var person = await Person(office: office);
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = .1m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        // Stored estimates have one decimal place; each exact .025h week rounds to0h in legacy rows.
        await Flag("capacity_screens");
        foreach (var (code, query) in new[] { ("workload-employee", $"projectId={p.Id}"), ("workload-discipline", $"officeId={office}") })
        {
            var report = await Run(code, query + "&unit=fte&period=months"); Assert.Equal(0m, report["rows"]![0]!["w0"]!.GetValue<decimal>());
            Assert.Equal(.1m, Cell(report, 0, "u0")["knownHours"]!.GetValue<decimal>()); Assert.Equal(.000625m, Cell(report, 0, "u0")["value"]!.GetValue<decimal>());
            Assert.Equal(.000625m, Cell(report, -1, "u0")["value"]!.GetValue<decimal>());
            var csv = await f.As(TestData.Sam).GetStringAsync($"/api/v1/reports/{code}/export?{query}&unit=fte&period=months&format=csv"); Assert.Contains("0.000625", csv);
        }
    }

    [Fact]
    public async Task Capacity_partial_month_uses_displayed_weeks_only_and_horizon_not_internal_lookahead()
    {
        var person = await Person(); await Flag("capacity_reports");
        await (await f.As(TestData.Sam).Post("/api/v1/planning/entries", new { personId = person.Id, label = "Synthetic exact FTE source", sourceCategory = PlanningSource.Admin,
            confidence = PlanningConfidence.Confirmed, visibility = PlanningVisibility.Published, hoursPerWeek = 40m, startWeek = "2026-11-30", endWeek = "2026-12-07" })).Json(201);
        await Flag("capacity_screens");
        var report = await Run("allocations-by-source", "from=2026-11-30&weeks=2&source=Admin&unit=fte&period=months");
        var period = Assert.Single(View(report)["periods"]!.AsArray()); Assert.Equal(("2026-12", 2, 80m), (period!.S("key"), period.I("weekCount"), period["denominatorHours"]!.GetValue<decimal>()));
        Assert.Equal(1m, Cell(report, 0, "u0")["value"]!.GetValue<decimal>()); Assert.Equal(80m, Cell(report, 0, "u0")["knownHours"]!.GetValue<decimal>());
        Assert.Equal(1m, Cell(report, report["rows"]!.AsArray().Count - 1, "u0")["value"]!.GetValue<decimal>());
        foreach (var horizon in new[] { 1, 26 })
        {
            var view = View(await Run("team-capacity", $"from=2026-11-30&weeks={horizon}&unit=fte&period=months"));
            Assert.Equal(horizon, view.I("displayedWeekCount")); Assert.Equal(horizon, view["periods"]!.AsArray().Sum(item => item!.I("weekCount")));
        }
        await Run("team-capacity", "weeks=0&unit=fte", status: 400); await Run("team-capacity", "weeks=27&period=months", status: 400);
    }

    [Fact]
    public async Task Team_capacity_months_keep_weekly_people_counts_separate_and_never_convert_or_sum_them()
    {
        await Person(); await Flag("capacity_reports"); await Flag("capacity_screens");
        var report = await Run("team-capacity", "from=2026-11-02&weeks=4&unit=fte&period=months");
        Assert.Equal(4, View(report)["columns"]!.AsArray().Count(column => column!.S("path").StartsWith("countW", StringComparison.Ordinal)));
        for (var row = 0; row < 9; row++)
        {
            if (row < 6) { Assert.Equal("NotApplicable", Cell(report, row, "countW0").S("kind")); Assert.Null(Cell(report, row, "countW0")["value"]); }
            else
            {
                Assert.Equal("NotApplicable", Cell(report, row, "u0").S("kind")); Assert.Null(Cell(report, row, "u0")["value"]);
                for (var week = 0; week < 4; week++) { var cell = Cell(report, row, $"countW{week}"); Assert.Equal("Count", cell.S("kind")); Assert.Equal(report["rows"]![row]![$"w{week}"]!.GetValue<decimal>(), cell["value"]!.GetValue<decimal>()); }
            }
        }
    }

    [Fact]
    public async Task Missing_estimates_zero_estimates_partial_scope_and_zero_standard_remain_explicitly_distinct()
    {
        var p = await d.Project(); var person = await Person();
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, dueDate = "2026-11-27" });
        await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 0m, dueDate = "2026-11-27" });
        await Flag("capacity_screens"); var report = await Run("workload-employee", $"projectId={p.Id}&unit=fte&period=months");
        var cell = Cell(report, 0, "u0"); Assert.Null(cell["value"]); Assert.Equal(0m, cell["knownHours"]!.GetValue<decimal>()); Assert.Equal((1, "KnownSubtotal"), (cell.I("missingInputs"), cell.S("coverage")));
        Assert.Equal(1, View(report).I("unestimatedTasks")); Assert.Equal("VisibleOnly", View(report).S("scopeCoverage"));
        var knownProject = await d.Project(); await d.NewTask(knownProject.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 0m, dueDate = "2026-11-27" });
        var known = await Run("workload-employee", $"projectId={knownProject.Id}&unit=fte&period=months"); Assert.Equal(0m, Cell(known, 0, "u0")["value"]!.GetValue<decimal>());
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/default_weekly_capacity_hours", new { value = 0 })).Json();
        var unavailable = await Run("workload-employee", $"projectId={knownProject.Id}&unit=fte&period=months"); Assert.Equal(0m, View(unavailable)["standardWeeklyHours"]!.GetValue<decimal>());
        Assert.Null(Cell(unavailable, 0, "u0")["value"]); Assert.Equal("StandardUnavailable", Cell(unavailable, 0, "u0").S("coverage"));
        var hours = await Run("workload-employee", $"projectId={knownProject.Id}&unit=hours&period=months"); Assert.Equal(0m, Cell(hours, 0, "u0")["value"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Positive_undated_known_subtotal_and_missing_inputs_survive_both_exports_without_FTE_conversion()
    {
        var project = await d.Project(); var person = await Person();
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 8m });
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person.Id });
        await Flag("capacity_screens"); var query = $"projectId={project.Id}&unit=fte&period=months";
        var report = await Run("workload-employee", query); var index = PersonIndex(report, person.Name); var cell = Cell(report, index, "noDueDate");
        Assert.Equal("UndatedHours", cell.S("kind")); Assert.Null(cell["value"]); Assert.Equal(8m, cell["knownHours"]!.GetValue<decimal>());
        Assert.Equal((1, "KnownSubtotal"), (cell.I("missingInputs"), cell.S("coverage"))); Assert.Equal(8m, report["rows"]![index]!["noDueDate"]!.GetValue<decimal>());
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var exported = await ExportRows("workload-employee", query, format, "Person");
            Assert.Equal("", ExportValue(exported, person.Name, "No due date (h)"));
            Assert.Equal(8m, decimal.Parse(ExportValue(exported, person.Name, "No due date (h) · known source hours (h)"), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("Unknown total; known source hours retained", ExportValue(exported, person.Name, "No due date (h) · coverage"));
            Assert.Equal("1", ExportValue(exported, person.Name, "No due date (h) · unestimated inputs"));
        }
    }

    [Fact]
    public async Task Reduced_availability_stays_provisional_in_employee_and_team_months_counts_and_both_exports()
    {
        var project = await d.Project(); var person = await Person();
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 40m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{person.Id}/availability/2026-11-02", new AllocationEndpoints.AvailabilityBody(0, 4, AvailabilityCategory.Reduced))).Json();
        await Flag("capacity_reports"); await Flag("capacity_screens");
        var employeeQuery = $"projectId={project.Id}&unit=fte&period=months"; var employee = await Run("workload-employee", employeeQuery); var personIndex = PersonIndex(employee, person.Name);
        foreach (var index in new[] { personIndex, -1 })
        {
            var cell = Cell(employee, index, "u0Available"); Assert.Null(cell["value"]); Assert.Equal("ProvisionalAvailability", cell.S("coverage"));
            Assert.Equal(156m, cell["knownHours"]!.GetValue<decimal>()); Assert.Equal(0, cell.I("missingInputs"));
        }
        Assert.Equal(1m, Cell(employee, personIndex, "u1Available")["value"]!.GetValue<decimal>()); // unaffected December keeps exact known availability
        Assert.Equal(.25m, Cell(employee, personIndex, "u0")["value"]!.GetValue<decimal>()); // work estimates do not acquire availability uncertainty
        Assert.Equal(36m, employee["rows"]![personIndex]!["w0Available"]!.GetValue<decimal>()); // legacy numeric source stays intact
        var hours = await Run("workload-employee", $"projectId={project.Id}&unit=hours&period=months");
        Assert.Null(Cell(hours, PersonIndex(hours, person.Name), "u0Available")["value"]); Assert.Equal("ProvisionalAvailability", Cell(hours, -1, "u0Available").S("coverage"));
        var availabilityHeader = View(employee)["columns"]!.AsArray().Single(column => column!.S("path") == "u0Available")!.S("header");
        foreach (var format in new[] { "csv", "xlsx" })
        {
            var exported = await ExportRows("workload-employee", employeeQuery, format, "Person");
            Assert.Equal("", ExportValue(exported, person.Name, availabilityHeader));
            Assert.Equal(156m, decimal.Parse(ExportValue(exported, person.Name, $"{availabilityHeader} · recorded source hours (h; may be provisional; see coverage)"), System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal("Availability provisional; recorded source values are unverified", ExportValue(exported, person.Name, $"{availabilityHeader} · coverage"));
        }
        foreach (var organisation in new[] { false, true })
        {
            await Flag("org_structure", organisation);
            const string query = "from=2026-11-02&weeks=4&unit=fte&period=months"; var team = await Run("team-capacity", query);
            var header = View(team)["columns"]!.AsArray().Single(column => column!.S("path") == "u0")!.S("header");
            var exports = new List<(string[] Headers, string[][] Rows)>();
            foreach (var format in new[] { "csv", "xlsx" }) exports.Add(await ExportRows("team-capacity", query, format, "Measure"));
            foreach (var row in new[] { 0, 1, 5 }) // capacity, time away, remaining depend on the provisional source day
            {
                var cell = Cell(team, row, "u0"); Assert.Null(cell["value"]); Assert.Equal("ProvisionalAvailability", cell.S("coverage"));
                Assert.NotNull(cell["knownHours"]); Assert.Equal(0, cell.I("missingInputs"));
                var measure = team["rows"]![row]!.S("measure");
                foreach (var exported in exports)
                {
                    Assert.Equal("", ExportValue(exported, measure, header));
                    Assert.Equal("Availability provisional; recorded source values are unverified", ExportValue(exported, measure, $"{header} · coverage"));
                    Assert.Equal(cell["knownHours"]!.GetValue<decimal>(), decimal.Parse(ExportValue(exported, measure, $"{header} · recorded source hours (h; may be provisional; see coverage)"), System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            Assert.Equal(4m, Cell(team, 1, "u0")["knownHours"]!.GetValue<decimal>());
            foreach (var row in new[] { 6, 7 }) { Assert.Null(Cell(team, row, "countW0")["value"]); Assert.Equal("ProvisionalAvailability", Cell(team, row, "countW0").S("coverage")); }
            Assert.Equal("AvailabilityDependentCount", Cell(team, 6, "countW1").S("coverage"));
            Assert.NotNull(Cell(team, 6, "countW1")["value"]); Assert.NotNull(Cell(team, 8, "countW0")["value"]);
            foreach (var exported in exports)
            {
                var countHeader = View(team)["columns"]!.AsArray().Single(column => column!.S("path") == "countW0")!.S("header");
                var countRow = team["rows"]![6]!.S("measure"); Assert.Equal("", ExportValue(exported, countRow, countHeader));
                Assert.Equal("Availability provisional; recorded source values are unverified", ExportValue(exported, countRow, $"{countHeader} · coverage"));
                var unaffectedHeader = View(team)["columns"]!.AsArray().Single(column => column!.S("path") == "countW1")!.S("header");
                Assert.Equal("Recorded weekly indicator count; availability coverage is not assessed", ExportValue(exported, countRow, $"{unaffectedHeader} · coverage"));
            }
        }
    }

    [Theory]
    [InlineData("hours", "weeks")]
    [InlineData("hours", "months")]
    [InlineData("fte", "weeks")]
    [InlineData("fte", "months")]
    public async Task Every_unit_period_pair_keeps_identical_S23_task_cohort_and_exact_canonical_metric_scope(string unit, string period)
    {
        var p = await d.Project(); var person = await Person(); var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id });
        await Flag("capacity_reports"); await Flag("capacity_screens");
        foreach (var (code, scope) in new[] { ("workload-employee", $"projectId={p.Id}"), ("workload-discipline", ""), ("team-capacity", "weeks=2&from=2026-11-02"), ("allocations-by-source", "weeks=2&from=2026-11-02&show=hours-by-source") })
        {
            var report = await Run(code, $"{scope}{(scope.Length > 0 ? "&" : "")}unit={unit}&period={period}"); var inputs = report["taskInputs"]!;
            Assert.Equal((1, 1), (inputs["unestimated"]!.I("count"), inputs["openUndated"]!.I("count"))); Assert.Equal((unit, period), (inputs["parameters"]!.S("unit"), inputs["parameters"]!.S("period")));
            var columns = View(report)["columns"]!.AsArray();
            if (unit == "hours" && period == "weeks") Assert.True(JsonNode.DeepEquals(report["columns"], View(report)["columns"]));
            else
            {
                var firstPeriod = View(report)["periods"]![0]!; var unitLabel = unit == "fte" ? "FTE" : "h";
                var periodLabel = firstPeriod.S("label"); var weekCount = firstPeriod.I("weekCount");
                var measures = code == "workload-employee"
                    ? new[] { ("", "Estimated work"), ("Available", "Available"), ("Confirmed", "Confirmed reservation"), ("Proposed", "Proposed request"), ("Committed", "Committed load") }
                    : new[] { ("", "Estimated work") };
                foreach (var (suffix, measure) in measures)
                {
                    var header = columns.Single(column => column!.S("path") == $"u0{suffix}")!.S("header");
                    Assert.Equal(code is "team-capacity" or "allocations-by-source"
                        ? $"{periodLabel} ({unitLabel}; ISO weeks: {weekCount})"
                        : $"{periodLabel} · {measure} ({unitLabel}; ISO weeks: {weekCount})", header);
                    if (unit == "fte") { Assert.Contains("FTE", header); Assert.DoesNotContain("(h)", header); Assert.DoesNotContain("Hours (FTE", header); }
                }
            }
            if (code is "workload-employee" or "workload-discipline")
            {
                var undated = columns.Single(column => column!.S("path") == "noDueDate")!;
                Assert.Equal("No due date (h)", undated.S("header"));
                Assert.True(JsonNode.DeepEquals(report["columns"]!.AsArray().Single(column => column!.S("path") == "noDueDate"), undated));
            }
            if (code == "workload-employee")
            {
                var capacity = columns.Single(column => column!.S("path") == "capacity")!;
                Assert.Equal("Capacity (h/week)", capacity.S("header"));
                Assert.True(JsonNode.DeepEquals(report["columns"]!.AsArray().Single(column => column!.S("path") == "capacity"), capacity));
                Assert.Contains("Available (h)", report["columns"]!.AsArray().Single(column => column!.S("path") == "w0Available")!.S("header"));
            }
            var page = await Run($"{code}/task-inputs/unestimated", Query(inputs["parameters"]!) + "&page=1&pageSize=1");
            Assert.Equal(1, page.I("total")); Assert.Equal(task.G("id"), Assert.Single(page["items"]!.AsArray())!.G("id")); Assert.True(JsonNode.DeepEquals(inputs["parameters"], page["parameters"]));
        }
    }

    [Fact]
    public async Task Strict_unit_period_and_nonmatrix_forms_refuse_instead_of_silently_becoming_a_different_display()
    {
        await Flag("capacity_screens"); await Flag("capacity_reports");
        foreach (var code in new[] { "workload-employee", "workload-discipline", "team-capacity", "allocations-by-source" })
        foreach (var query in new[] { "unit=percentage", "unit=%20fte%20", "unit=fte&unit=hours", "period=days", "period=months&period=weeks" })
        { await Run(code, query, status: 400); await Run($"{code}/task-inputs/unestimated", query, status: 400); Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).GetAsync($"/api/v1/reports/{code}/export?{query}&format=csv")).StatusCode); }
        await Run("allocations-by-source", "show=entries-without-project&unit=fte", status: 400); await Run("allocations-by-source", "show=entries-without-project&period=months", status: 400);
        var entries = await Run("allocations-by-source", "show=entries-without-project&unit=hours&period=weeks"); Assert.False(entries.AsObject().ContainsKey("unitView")); Assert.False(entries["savedParameters"]!.AsObject().ContainsKey("unit")); Assert.False(entries["savedParameters"]!.AsObject().ContainsKey("period"));
        await Run("task-hours", "unit=fte", TestData.Alex, 400);
        await Run("team-capacity", "unit=fte", TestData.Alex, 403); await Flag("capacity_reports", false); await Run("team-capacity", "unit=fte", status: 404);
    }

    [Fact]
    public async Task Saved_views_copy_parameters_and_crossfield_cleaning_retain_unit_period_without_widening_scope()
    {
        var civil = await d.Discipline("Civil"); await Flag("capacity_screens"); await Flag("capacity_reports");
        var parameters = new Dictionary<string, string> { ["disciplineId"] = civil.ToString(), ["unit"] = "fte", ["period"] = "months" };
        var saved = await (await f.As(TestData.Sam).Post("/api/v1/views", new { name = "Synthetic exact monthly FTE", listType = "report:workload-employee", @params = parameters, scope = "Personal" })).Json(201);
        var list = await (await f.As(TestData.Sam).GetAsync("/api/v1/views?listType=report:workload-employee")).Json(); var view = list["views"]!.AsArray().Single(view => view!.G("id") == saved.G("id"))!;
        Assert.Equal(("fte", "months", civil.ToString()), (view["params"]!.S("unit"), view["params"]!.S("period"), view["params"]!.S("disciplineId")));
        var opened = await Run("workload-employee", Query(view["params"]!)); Assert.Equal(("fte", "months"), (View(opened).S("unit"), View(opened).S("period")));
        foreach (var invalid in new[] { new Dictionary<string, string> { ["show"] = "entries-without-project", ["unit"] = "fte" }, new Dictionary<string, string> { ["show"] = "entries-without-project", ["period"] = "months" } })
            await (await f.As(TestData.Sam).Post("/api/v1/views", new { name = "Synthetic incompatible form", listType = "report:allocations-by-source", @params = invalid, scope = "Personal" })).Json(400);
    }

    [Fact]
    public async Task Dark_flag_keeps_original_wire_and_export_and_hidden_scope_remains_visible_only_without_private_hours()
    {
        var p = await d.Project(); var person = await Person(); await d.NewTask(p.Id, TestData.Pm, new { assigneeId = person.Id, estimatedHours = 40m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        var hidden = await d.Project();
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).EnsureSuccessStatusCode();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{hidden.Id}", new { visibility = Visibility.Restricted }, d.Version(hidden.Id))).Json();
        Assert.Equal(Visibility.Restricted, f.Db(db => db.Projects.Single(project => project.Id == hidden.Id).Visibility));
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync($"/api/v1/projects/{hidden.Id}")).StatusCode);
        await d.NewTask(hidden.Id, TestData.Pm, new { name = "PRIVATE S25 SOURCE SENTINEL", assigneeId = person.Id, estimatedHours = 9999m, startDate = "2026-11-02", dueDate = "2026-11-27" });
        var query = $"projectId={p.Id}"; var before = await Run("workload-employee", query); var csv = await f.As(TestData.Sam).GetByteArrayAsync($"/api/v1/reports/workload-employee/export?{query}&format=csv"); Assert.False(before.AsObject().ContainsKey("unitView"));
        await Flag("capacity_screens"); var visible = await Run("workload-employee", "unit=fte&period=months"); Assert.Equal(.25m, Cell(visible, PersonIndex(visible, person.Name), "u0")["value"]!.GetValue<decimal>()); Assert.Equal("VisibleOnly", View(visible).S("scopeCoverage")); Assert.DoesNotContain("PRIVATE S25 SOURCE SENTINEL", visible.ToJsonString());
        var empty = await Run("workload-employee", $"projectId={hidden.Id}&unit=fte&period=months"); Assert.Empty(empty["rows"]!.AsArray()); Assert.Empty(View(empty)["rows"]!.AsArray());
        await Flag("capacity_screens", false); Assert.True(JsonNode.DeepEquals(before, await Run("workload-employee", query)));
        Assert.Equal(csv, await f.As(TestData.Sam).GetByteArrayAsync($"/api/v1/reports/workload-employee/export?{query}&format=csv"));
        Assert.True(JsonNode.DeepEquals(before, await Run("workload-employee", query + "&unit=fte&period=months")));
    }
}
