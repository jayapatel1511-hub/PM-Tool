using System.Globalization;
using System.Text.RegularExpressions;

namespace Hub.Domain;

public static class PlanningVisibility
{
    public const string Draft = "Draft", Published = "Published", Confirmed = "Confirmed";
    public static readonly string[] All = [Draft, Published, Confirmed];
}

public static class PlanningConfidence
{
    public const string Confirmed = "Confirmed", Expected = "Expected", Possible = "Possible";
    public static readonly string[] All = [Confirmed, Expected, Possible];
}

public static class PlanningSource
{
    public const string MajorProject = "MajorProject", OtherProject = "OtherProject", Proposal = "Proposal",
        BusinessDevelopment = "BusinessDevelopment", Training = "Training", Admin = "Admin", Supervision = "Supervision",
        InternalInitiative = "InternalInitiative", FieldWork = "FieldWork", Other = "Other";
    public static readonly string[] All = [MajorProject, OtherProject, Proposal, BusinessDevelopment, Training, Admin, Supervision, InternalInitiative, FieldWork, Other];
}

public static class PlanningIndicator
{
    public const string OverPlanned = "OverPlanned", UnderPlanned = "UnderPlanned", StalePlan = "StalePlan";
}

public static class PlanningWarning
{
    public const string ProjectNotActive = "ProjectNotActive", OwnerCannotManage = "OwnerCannotManage", AboveCapacity = "AboveCapacity";
}

public sealed record QuickAdd(decimal Hours, string Label, bool Possible);
public sealed record EntryWeek(Guid Id, Guid? ProjectId, string Confidence, decimal Hours, DateOnly StartWeek, DateTimeOffset CreatedAt);
public sealed record EntryCount(Guid Id, decimal Counted, decimal Covered);
public sealed record WeekBands(decimal Approved, decimal Confirmed, decimal Expected, decimal Possible, decimal Remaining, IReadOnlyList<EntryCount> Entries);
public sealed record UnderWeek(decimal Capacity, decimal TimeAway, decimal Planned, bool UnestimatedDue);
public sealed record PlanningEntryFacts(Guid PersonId, Guid OwnerId, Guid? PersonSupervisorId, string Visibility, bool IsSelfEntry, bool OwnerStillManages, bool IsActive = true, bool InViewerScope = false, bool ProjectVisible = true);

public static partial class PlanningRules
{
    [GeneratedRegex(@"^\s*(~)?\s*(\d{1,3}(?:\.\d)?)\s*(?:(?:h|hr|hrs|hour|hours)\b)?\s*(?:[-–—:]\s*)?(\S.*?)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuickAddPattern();

    public static string? ValidateWeeks(DateOnly start, DateOnly end)
    {
        if (start.DayOfWeek != DayOfWeek.Monday || end.DayOfWeek != DayOfWeek.Monday) return "planning.week";
        if (end < start) return "error.date_range";
        return end.DayNumber - start.DayNumber > 721 ? "planning.span" : null;
    }

    public static string? ValidateHours(decimal hours, int maxPerWeek)
        => hours > 0 && hours <= maxPerWeek && decimal.Round(hours * 2, 0) == hours * 2 ? null : "planning.hours";

    public static decimal WeekHours(DateOnly start, DateOnly end, decimal perWeek, DateOnly week)
        => week >= start && week <= end && week.DayOfWeek == DayOfWeek.Monday ? perWeek : 0m;

    public static QuickAdd? ParseQuickAdd(string? text, int maxPerWeek)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = QuickAddPattern().Match(text);
        if (!match.Success || !decimal.TryParse(match.Groups[2].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours)) return null;
        var label = match.Groups[3].Value.Trim();
        if (label.Length is < 1 or > 120 || label.Equals("h", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("hr", StringComparison.OrdinalIgnoreCase) || label.Equals("hrs", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("hour", StringComparison.OrdinalIgnoreCase) || label.Equals("hours", StringComparison.OrdinalIgnoreCase) ||
            ValidateHours(hours, maxPerWeek) is not null) return null;
        return new QuickAdd(hours, label, match.Groups[1].Success);
    }

    public static (string Visibility, string Confidence) Defaults(bool self, bool possible)
        => self ? (PlanningVisibility.Confirmed, possible ? PlanningConfidence.Possible : PlanningConfidence.Confirmed)
                : (PlanningVisibility.Draft, possible ? PlanningConfidence.Possible : PlanningConfidence.Expected);

    public static bool StepVisibility(bool self, string from, string to)
        => !self && PlanningVisibility.All.Contains(from) && PlanningVisibility.All.Contains(to) && from != to;

    public static (decimal TimeAway, decimal Additional) TimeAway(decimal normalDay, decimal? overrideHours, string? category)
    {
        if (normalDay < 0 || overrideHours is < 0) throw new ArgumentOutOfRangeException();
        if (overrideHours is null || category is not ("Unavailable" or "Reduced" or "Additional")) return (0, 0);
        var available = overrideHours.Value;
        return category == "Additional"
            ? (0, Math.Max(0, available - normalDay))
            : (Math.Max(0, normalDay - available), 0);
    }

    public static WeekBands Combine(decimal capacity, IReadOnlyList<EntryWeek> entries, IReadOnlyDictionary<Guid, decimal> approvedByProject)
    {
        if (capacity < 0 || entries.Any(e => e.Hours < 0)) throw new ArgumentOutOfRangeException(nameof(capacity));
        var covered = new Dictionary<Guid, decimal>();
        var counts = new List<EntryCount>();
        decimal confirmed = approvedByProject.Values.Sum(), expected = 0, possible = 0;
        foreach (var entry in entries.OrderBy(e => e.Confidence == PlanningConfidence.Confirmed ? 0 : e.Confidence == PlanningConfidence.Expected ? 1 : 2).ThenBy(e => e.StartWeek).ThenBy(e => e.CreatedAt).ThenBy(e => e.Id))
        {
            var cover = entry.ProjectId is Guid p && approvedByProject.TryGetValue(p, out var approved)
                ? Math.Min(entry.Hours, Math.Max(0, approved - covered.GetValueOrDefault(p))) : 0;
            if (entry.ProjectId is Guid project) covered[project] = covered.GetValueOrDefault(project) + cover;
            var counted = entry.Hours - cover;
            counts.Add(new EntryCount(entry.Id, counted, cover));
            if (entry.Confidence == PlanningConfidence.Confirmed) confirmed += counted;
            else if (entry.Confidence == PlanningConfidence.Expected) expected += counted;
            else possible += counted;
        }
        return new WeekBands(approvedByProject.Values.Sum(), confirmed, expected, possible, capacity - confirmed - expected, counts);
    }

    public static bool OverPlanned(WeekBands bands, decimal capacity, int overPct)
        => capacity == 0 ? bands.Confirmed + bands.Expected > 0 : (bands.Confirmed + bands.Expected) * 100 > capacity * overPct;

    public static bool[] UnderPlanned(IReadOnlyList<UnderWeek> weeks, int firstEvaluable, int underPct, int underWeeks, bool partial)
    {
        var result = new bool[weeks.Count];
        if (partial || underPct == 0 || underWeeks < 1) return result;
        for (var i = Math.Max(0, firstEvaluable); i < weeks.Count; i++)
        {
            var span = weeks.Skip(i).Take(underWeeks).ToArray();
            result[i] = span.Length == underWeeks && span.All(w => w.TimeAway == 0 && !w.UnestimatedDue &&
                (w.Capacity > 0 && w.Planned * 100 < w.Capacity * underPct));
        }
        return result;
    }

    public static bool Stale(DateOnly endWeek, DateOnly currentWeek, DateTimeOffset lastValidated, DateTimeOffset now, int staleDays)
        => endWeek >= currentWeek && (now - lastValidated).TotalDays > staleDays;
}
