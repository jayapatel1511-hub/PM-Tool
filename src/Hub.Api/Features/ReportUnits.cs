using System.Globalization;
using System.Text.Json.Nodes;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Exact report period arithmetic. Canonical producers supply their unrounded values; this helper never forecasts work.
public static class ReportUnitMath
{
    public static IReadOnlyList<ReportEndpoints.ReportPeriod> Periods(IReadOnlyList<DateOnly> weeks, string period, decimal? standardWeeklyHours)
    {
        if (period is not ("weeks" or "months")) throw new ArgumentException("Unsupported report period.", nameof(period));
        if (weeks.Any(week => week.DayOfWeek != DayOfWeek.Monday) || !weeks.SequenceEqual(weeks.Distinct().Order()))
            throw new ArgumentException("Displayed ISO weeks must be unique and ascending.", nameof(weeks));
        return weeks.GroupBy(week => period == "weeks" ? week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : week.AddDays(3).ToString("yyyy-MM", CultureInfo.InvariantCulture))
            .Select(group => new ReportEndpoints.ReportPeriod(group.Key, group.Key, group.ToArray(), group.Count(),
                standardWeeklyHours is > 0 ? group.Count() * standardWeeklyHours : null)).ToArray();
    }

    public static ReportEndpoints.ReportUnitCell Quantity(string path, IReadOnlyList<decimal?> hours, IReadOnlyList<decimal> knownHours,
        int missingInputs, string unit, decimal? standardWeeklyHours, string scopeCoverage = "CompleteForVisibleParameters")
    {
        if (unit is not ("hours" or "fte")) throw new ArgumentException("Unsupported report unit.", nameof(unit));
        if (hours.Count == 0 || hours.Count != knownHours.Count || missingInputs < 0) throw new ArgumentException("A period requires matching source weeks.");
        var known = knownHours.Sum();
        if (hours.Any(value => value is null) || missingInputs > 0) return new(path, "Hours", null, known, missingInputs, "KnownSubtotal");
        if (unit == "fte" && standardWeeklyHours is not > 0) return new(path, "Hours", null, known, 0, "StandardUnavailable");
        var exact = hours.Sum(value => value!.Value);
        return new(path, "Hours", unit == "fte" ? exact / (hours.Count * standardWeeklyHours!.Value) : exact, known, 0, scopeCoverage);
    }
}

public static partial class ReportEndpoints
{
    public sealed record ReportPeriod(string Key, string Label, IReadOnlyList<DateOnly> Weeks, int WeekCount, decimal? DenominatorHours);
    public sealed record ReportUnitCell(string Path, string Kind, decimal? Value, decimal? KnownHours, int MissingInputs, string Coverage);
    public sealed record ReportUnitRow(int RowIndex, string Kind, IReadOnlyList<ReportUnitCell> Cells);
    public sealed record ReportUnitView(string Unit, string Period, decimal? StandardWeeklyHours, int DisplayedWeekCount,
        IReadOnlyList<ReportPeriod> Periods, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportUnitRow> Rows, string ScopeCoverage, int UnestimatedTasks);

    sealed record ExactUnitMeasure(string Suffix, string Label, decimal?[] Hours, decimal[] KnownHours, int MissingInputs = 0, bool[]? Provisional = null);
    sealed record ExactUnitRow(int RowIndex, string Kind, string Coverage, ExactUnitMeasure[] Measures, IReadOnlyList<ReportUnitCell>? Context = null);
    sealed record ExactUnitSource(DateOnly[] Weeks, Col[] Columns, ExactUnitRow[] Rows);
    static bool HasReportUnits(Def definition) => definition.Code is "workload-employee" or "workload-discipline" or "team-capacity" or "allocations-by-source";
    static bool UsesReportUnits(Def definition, Ctx c) => c.InputsEnabled && HasReportUnits(definition)
        && !(definition.Code == "allocations-by-source" && c.Str("show") == "entries-without-project");
    static Param[] UnitParams => [new("unit", "select", ["hours", "fte"], "hours"), new("period", "select", ["weeks", "months"], "weeks")];
    static Param[] AppliedReportParams(Def definition, Ctx c) => [.. ReportParams(definition, c.S)
        .Where(parameter => parameter.Key is not ("unit" or "period") || UsesReportUnits(definition, c))];

    static void ValidateUnitForm(Def definition, Ctx c)
    {
        if (!c.InputsEnabled || definition.Code != "allocations-by-source" || c.Str("show") != "entries-without-project") return;
        Check.That(c.Str("unit") is null or "hours", "unit", "error.invalid");
        Check.That(c.Str("period") is null or "weeks", "period", "error.invalid");
    }

    static ExactUnitMeasure UnitMeasure(string suffix, string label, IEnumerable<decimal> exact, int missingInputs = 0,
        IEnumerable<bool>? unavailable = null, IEnumerable<bool>? provisional = null)
    {
        var known = exact.ToArray(); var unknown = unavailable?.ToArray(); var uncertain = provisional?.ToArray();
        return new(suffix, label, known.Select((hours, index) => missingInputs > 0 || unknown?[index] == true || uncertain?[index] == true ? (decimal?)null : hours).ToArray(), known, missingInputs, uncertain);
    }

    static void CaptureEmployeeUnits(Ctx c, List<DateOnly> weeks, List<WorkloadEndpoints.PersonLoad> people, Col[] columns)
    {
        if (!c.InputsEnabled) return;
        c.UnitMeasures = [("", Text.Get("reportUnits.estimatedHours")), ("Available", Text.Get("reportUnits.measure.available")), ("Confirmed", Text.Get("reportUnits.measure.confirmed")),
            ("Proposed", Text.Get("reportUnits.measure.proposed")), ("Committed", Text.Get("reportUnits.measure.committed"))];
        c.UnitSource = new(weeks.ToArray(), columns, WithUnitTotal(people.Select((person, index) => new ExactUnitRow(index, "Hours", person.PartialScope ? "VisibleOnly" : InputCoverage,
            [UnitMeasure("", c.UnitMeasures[0].Label, person.Cells.Select(cell => cell.Hours), person.Unestimated),
             UnitMeasure("Available", c.UnitMeasures[1].Label, person.Cells.Select(cell => cell.Available),
                 provisional: weeks.Select(week => person.ProvisionalWeeks?.Contains(week) ?? true)),
             UnitMeasure("Confirmed", c.UnitMeasures[2].Label, person.Cells.Select(cell => cell.Confirmed)),
             UnitMeasure("Proposed", c.UnitMeasures[3].Label, person.Cells.Select(cell => cell.Proposed)),
             UnitMeasure("Committed", c.UnitMeasures[4].Label, person.Cells.Select(cell => cell.Committed), person.Unestimated)],
            [new("capacity", "WeeklyRate", person.Capacity, null, 0, person.PartialScope ? "VisibleOnly" : InputCoverage),
             new("noDueDate", "UndatedHours", person.Unestimated > 0 ? null : person.NoDueDate, person.NoDueDate, person.Unestimated, person.Unestimated > 0 ? "KnownSubtotal" : person.PartialScope ? "VisibleOnly" : InputCoverage)] )).ToArray()));
    }

    static ExactUnitRow[] WithUnitTotal(ExactUnitRow[] rows)
    {
        if (rows.Length == 0) return rows;
        var measures = rows[0].Measures.Select(descriptor => {
            var source = rows.Select(row => row.Measures.Single(measure => measure.Suffix == descriptor.Suffix)).ToArray();
            return new ExactUnitMeasure(descriptor.Suffix, descriptor.Label,
                Enumerable.Range(0, descriptor.Hours.Length).Select(index => source.Any(measure => measure.Hours[index] is null) ? (decimal?)null : source.Sum(measure => measure.Hours[index]!.Value)).ToArray(),
                Enumerable.Range(0, descriptor.Hours.Length).Select(index => source.Sum(measure => measure.KnownHours[index])).ToArray(), source.Sum(measure => measure.MissingInputs),
                Enumerable.Range(0, descriptor.Hours.Length).Select(index => source.Any(measure => measure.Provisional?[index] == true)).ToArray());
        }).ToArray();
        return [.. rows, new(-1, "Total", rows.Any(row => row.Coverage == "VisibleOnly") ? "VisibleOnly" : InputCoverage, measures)];
    }

    static ReportUnitView? UnitView(Def definition, Ctx c, Result result, int? maximumRows = null)
    {
        if (!UsesReportUnits(definition, c)) return null;
        var source = c.UnitSource ?? throw new InvalidOperationException("A matrix report did not retain its exact period source.");
        var unit = c.Str("unit") ?? "hours"; var grouping = c.Str("period") ?? "weeks";
        decimal? standard = c.S.DefaultWeeklyCapacityHours;
        var periods = ReportUnitMath.Periods(source.Weeks, grouping, standard);
        var measures = source.Rows.FirstOrDefault(row => row.Kind == "Hours")?.Measures
            .Select(measure => (measure.Suffix, measure.Label)).ToArray() ?? c.UnitMeasures;
        var seriesPaths = source.Weeks.SelectMany((_, index) => measures.Select(measure => $"w{index}{measure.Suffix}")).ToHashSet();
        var first = Array.FindIndex(source.Columns, column => seriesPaths.Contains(column.Path));
        if (first < 0) first = source.Columns.Length;
        var separateCounts = source.Rows.Any(row => row.Kind == "Count") && (unit != "hours" || grouping != "weeks");
        var columns = source.Columns.Take(first).ToList();
        var unitLabel = Text.Get(unit == "fte" ? "reportUnits.fte" : "reportUnits.hours");
        string Path(int index, string suffix) => grouping == "weeks" && unit == "hours" ? $"w{index}{suffix}" : $"u{index}{suffix}";
        for (var p = 0; p < periods.Count; p++)
        foreach (var measure in measures)
        {
            var path = Path(p, measure.Suffix);
            var original = grouping == "weeks" && unit == "hours" ? source.Columns.FirstOrDefault(column => column.Path == path) : null;
            columns.Add(original ?? new Col(path, "hours", "number", definition.Code is "team-capacity" or "allocations-by-source"
                ? Text.Get("reportUnits.periodColumn", periods[p].Label, unitLabel, periods[p].WeekCount)
                : Text.Get("reportUnits.column", periods[p].Label, measure.Label, unitLabel, periods[p].WeekCount)));
        }
        if (separateCounts)
            columns.AddRange(source.Weeks.Select((week, index) => new Col($"countW{index}", "peopleCount", "number", Text.Get("reportUnits.weeklyPeople", week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))));
        columns.AddRange(source.Columns.Skip(first).Where(column => !seriesPaths.Contains(column.Path)));
        var rows = new List<ReportUnitRow>();
        static ReportUnitCell CountCell(string path, ExactUnitRow row, int index) => new(path, "Count", row.Measures[0].Hours[index], null, 0,
            row.Measures[0].Provisional?[index] == true ? "ProvisionalAvailability" : row.Coverage);
        foreach (var row in source.Rows.Where(row => maximumRows is null || row.RowIndex < maximumRows))
        {
            var cells = new List<ReportUnitCell>();
            for (var p = 0; p < periods.Count; p++)
            foreach (var descriptor in measures)
            {
                var path = Path(p, descriptor.Suffix);
                if (row.Kind == "Count")
                {
                    cells.Add(separateCounts ? new(path, "NotApplicable", null, null, 0, "NotApplicable")
                        : CountCell(path, row, p));
                    continue;
                }
                var measure = row.Measures.Single(measure => measure.Suffix == descriptor.Suffix);
                var indices = periods[p].Weeks.Select(week => Array.IndexOf(source.Weeks, week)).ToArray();
                var cell = ReportUnitMath.Quantity(path, indices.Select(index => measure.Hours[index]).ToArray(), indices.Select(index => measure.KnownHours[index]).ToArray(),
                    measure.MissingInputs, unit, standard, row.Coverage);
                cells.Add(indices.Any(index => measure.Provisional?[index] == true) ? cell with { Value = null, Coverage = "ProvisionalAvailability" } : cell);
            }
            if (separateCounts)
                for (var i = 0; i < source.Weeks.Length; i++) cells.Add(row.Kind == "Count"
                    ? CountCell($"countW{i}", row, i)
                    : new($"countW{i}", "NotApplicable", null, null, 0, "NotApplicable"));
            if (row.Context is { } context) cells.AddRange(context);
            rows.Add(new(row.RowIndex, row.Kind, cells));
        }
        return new(unit, grouping, standard, source.Weeks.Length, periods, columns.Select(column => new ReportColumn(column.Path, column.Title, column.Type)).ToArray(), rows,
            c.InputContext?.Coverage ?? (source.Rows.Any(row => row.Coverage == "VisibleOnly") ? "VisibleOnly" : InputCoverage), c.UnitUnestimated);
    }

    static Result UnitExport(Result original, ReportUnitView view)
    {
        var rows = new JsonArray([.. original.Rows.Select(row => row?.DeepClone())]);
        foreach (var row in view.Rows)
        {
            JsonNode target;
            if (row.RowIndex < 0) { target = new JsonObject { [view.Columns[0].Path] = Text.Get("reportUnits.total") }; rows.Add(target); }
            else target = rows[row.RowIndex]!;
            foreach (var cell in row.Cells)
            {
                target[cell.Path] = cell.Value is { } value ? JsonValue.Create(value) : null;
                if (cell.Kind is not ("Hours" or "UndatedHours" or "Count")) continue;
                if (cell.Kind is "Hours" or "UndatedHours") target[$"{cell.Path}KnownHours"] = cell.KnownHours is { } known ? JsonValue.Create(known) : null;
                target[$"{cell.Path}Coverage"] = Text.Get($"reportUnits.coverage.{cell.Coverage}");
                if (cell.Kind is "Hours" or "UndatedHours") target[$"{cell.Path}MissingInputs"] = cell.MissingInputs;
            }
        }
        var evidence = view.Rows.SelectMany(row => row.Cells).Where(cell => cell.Kind is "Hours" or "UndatedHours" or "Count").ToLookup(cell => cell.Path);
        var columns = view.Columns.SelectMany(column => {
            var result = new List<Col> { new(column.Path, "hours", column.Type, column.Header) };
            var cells = evidence[column.Path].ToArray();
            if (cells.Length == 0) return result;
            var hours = cells.Any(cell => cell.Kind is "Hours" or "UndatedHours");
            if (hours) result.Add(new($"{column.Path}KnownHours", "hours", "number", Text.Get(cells.Any(cell => cell.Coverage == "ProvisionalAvailability")
                ? "reportUnits.recordedHoursColumn" : "reportUnits.knownHoursColumn", column.Header)));
            result.Add(new($"{column.Path}Coverage", "partialScope", Label: Text.Get("reportUnits.coverageColumn", column.Header)));
            if (hours) result.Add(new($"{column.Path}MissingInputs", "count", "number", Text.Get("reportUnits.missingColumn", column.Header)));
            return result;
        }).ToArray();
        return new(rows, original.ListLink, columns);
    }

    static List<(string, string)> UnitExportHeader(ReportUnitView view, IReadOnlyList<(string, string)> parameters) => [
        (Text.Get("reportUnits.unit"), Text.Get(view.Unit == "fte" ? "reportUnits.fte" : "reportUnits.hours")),
        (Text.Get("reportUnits.period"), Text.Get(view.Period == "months" ? "reportUnits.months" : "reportUnits.weeks")),
        (Text.Get("reportUnits.standard"), view.StandardWeeklyHours?.ToString(CultureInfo.InvariantCulture) ?? Text.Get("reportUnits.unavailable")),
        (Text.Get("reportUnits.displayedWeeks"), view.DisplayedWeekCount.ToString(CultureInfo.InvariantCulture)),
        (Text.Get("reportUnits.scope"), Text.Get($"reportUnits.coverage.{view.ScopeCoverage}")),
        (Text.Get("reportUnits.filters"), string.Join("; ", parameters.Select(parameter => $"{parameter.Item1}: {parameter.Item2}"))),
        (Text.Get("reportUnits.missing"), view.UnestimatedTasks.ToString(CultureInfo.InvariantCulture)),
        .. view.Periods.Select(period => (period.Label, Text.Get("reportUnits.periodBasis", period.WeekCount,
            string.Join("; ", period.Weeks.Select(week => week.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))),
            period.DenominatorHours?.ToString(CultureInfo.InvariantCulture) ?? Text.Get("reportUnits.unavailable")))) ];
}
