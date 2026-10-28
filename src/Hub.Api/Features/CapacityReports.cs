using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class ReportEndpoints
{
    /// One planning entry with no project, as the Allocations by source report lists it.
    sealed record UnlinkedEntryRow(string PersonName, string OwnerName, string Label, string Source, decimal HoursPerWeek, DateOnly StartWeek, DateOnly EndWeek, string Confidence, string Visibility);
    /// One route's opens in one week, bucketed by how long the page took, as the pilot route use report lists it.
    sealed record RouteUseRow(string RouteKey, DateOnly Week, int Opens, int Under500Ms, int From500To999Ms, int From1000To1999Ms, int From2000To4999Ms, int Over5000Ms);

    static Param[] CapacityParams => [new("supervisorId", "person"), Discipline, new("from", "date"), new("weeks", "number"), new("includeMyDrafts", "bool", Default: "true")];

    static async Task<PlanningEndpoints.GridResult> CapacityGrid(Ctx c)
    {
        var weeks = c.Int("weeks") ?? c.S.PlanningHorizonWeeks;
        Check.That(weeks is >= 1 and <= 26, "weeks", "error.range");
        var grid = await PlanningEndpoints.ReportGrid(new(From: c.Date("from"), Weeks: weeks, SupervisorId: c.Id("supervisorId"),
            DisciplineId: c.Id("disciplineId"), IncludeMyDrafts: c.Str("includeMyDrafts") != "false"), c.Access, c.Db, c.Store, c.Clock);
        c.Partial = grid.PartialView;
        return grid;
    }

    static Col[] CapacityColumns(PlanningEndpoints.GridResult grid) => [new("measure", "measure"), ..grid.Weeks.Select((w, i) => new Col($"w{i}", "hours", "number", w.ToString("yyyy-MM-dd")))];

    static string CapacityLink(Ctx c, string view, PlanningEndpoints.GridResult grid, bool includeUnit = false) => "/planner?" + string.Join("&", new[] { ("view", view), ("supervisorId", c.Str("supervisorId")),
        ("disciplineId", c.Str("disciplineId")), ("from", grid.Weeks[0].ToString("yyyy-MM-dd")), ("weeks", grid.Weeks.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("includeMyDrafts", c.Str("includeMyDrafts") ?? "true"),
        ("source", c.Str("source")), ("confidence", c.Str("confidence")), ("unitId", includeUnit ? c.Str("unitId") : null) }.Where(x => x.Item2 != null).Select(x => $"{x.Item1}={Uri.EscapeDataString(x.Item2!)}"));

    static async Task<Result> TeamCapacity(Ctx c)
    {
        var grid = await CapacityGrid(c);
        (string Key, Func<PlanningEndpoints.PersonRow, PlanningEndpoints.CellRow, decimal> Value)[] measures = [
            ("capacity", (_, v) => v.Capacity), ("timeAway", (_, v) => v.TimeAway), ("confirmed", (_, v) => v.Confirmed),
            ("expected", (_, v) => v.Expected), ("possible", (_, v) => v.Possible), ("remaining", (_, v) => v.Remaining),
            ("overPlanned", (_, v) => v.OverPlanned ? 1 : 0), ("underPlanned", (p, v) => !p.Partial && v.UnderPlanned ? 1 : 0),
            ("partial", (p, _) => p.Partial ? 1 : 0)];
        // The organisation slice is additive. Keep the pre-050 rows and columns byte-for-byte
        // equivalent while the flag is dark; historical placement is only consulted when enabled.
        if (!c.S.IsFlagOn("org_structure"))
        {
            var rows = new JsonArray();
            foreach (var (key, value) in measures)
            {
                var row = new JsonObject { ["measure"] = Text.Get($"capacityReport.{key}") };
                for (var i = 0; i < grid.Weeks.Length; i++) row[$"w{i}"] = decimal.Round(grid.People.Sum(p => value(p, p.Cells[i])), 1);
                rows.Add(row);
            }
            return new(rows, CapacityLink(c, "grid", grid), CapacityColumns(grid));
        }

        var people = grid.People;
        var ids = people.Select(p => p.Id).Distinct().ToArray();
        var units = await c.Db.Set<OrgUnit>().AsNoTracking().ToDictionaryAsync(u => u.Id);
        var tree = units.Values.Select(u => new OrgTree.Unit(u.Id, u.ParentId, 0, u.LeaderId)).ToArray();
        HashSet<Guid>? selected = null;
        if (c.Id("unitId") is { } unitId) selected = OrgTree.Descendants(unitId, tree).ToHashSet();

        var placements = new Dictionary<int, Dictionary<Guid, Guid?>>();
        for (var i = 0; i < grid.Weeks.Length; i++)
        {
            var week = grid.Weeks[i];
            var localEnd = week.AddDays(7).ToDateTime(TimeOnly.MinValue);
            var end = new DateTimeOffset(localEnd, Clock.Zone(c.S).GetUtcOffset(localEnd)).ToUniversalTime();
            placements[i] = await OrgStructureReports.At(c.Db, ids, end);
        }

        var groupKeys = placements.Values.SelectMany(x => x.Values)
            .Where(id => selected is null || id is { } value && selected.Contains(value))
            .Distinct().OrderBy(id => id is null ? "" : OrgStructureReports.UnitPath(id, units), StringComparer.Ordinal)
            .ThenBy(id => id is null ? Text.Get("report.noUnit") : units.GetValueOrDefault(id.Value)?.Name ?? Text.Get("report.noUnit"), StringComparer.Ordinal)
            .ToArray();
        var data = new JsonArray();
        foreach (var group in groupKeys)
        foreach (var (key, value) in measures)
        {
            var unit = group is { } id && units.TryGetValue(id, out var definition) ? definition.Name : Text.Get("report.noUnit");
            var row = new JsonObject
            {
                ["measure"] = Text.Get($"capacityReport.{key}"),
                ["unit"] = unit,
                ["unitPath"] = OrgStructureReports.UnitPath(group, units),
            };
            for (var i = 0; i < grid.Weeks.Length; i++)
            {
                var members = people.Where(p => placements[i].GetValueOrDefault(p.Id) == group);
                row[$"w{i}"] = decimal.Round(members.Sum(p => value(p, p.Cells[i])), 1);
            }
            data.Add(row);
        }
        var columns = new[] { new Col("measure", "measure"), new Col("unit", "unit"), new Col("unitPath", "unitPath") }
            .Concat(grid.Weeks.Select((w, i) => new Col($"w{i}", "hours", "number", w.ToString("yyyy-MM-dd")))).ToArray();
        return new(data, CapacityLink(c, "grid", grid, includeUnit: true), columns);
    }

    static async Task<Result> AllocationsBySource(Ctx c)
    {
        var grid = await CapacityGrid(c);
        if (c.Str("show") == "entries-without-project") {
            var entries = grid.People.SelectMany(p => p.Entries).Where(e => e.ProjectId == null && (c.Str("source") == null || e.SourceCategory == c.Str("source"))
                && (c.Str("confidence") == null || e.Confidence == c.Str("confidence"))).Select(e => new UnlinkedEntryRow(e.PersonName, e.OwnerName, e.Label,
                    Text.Get($"planning.source.{e.SourceCategory}"), e.HoursPerWeek, e.StartWeek, e.EndWeek, e.Confidence,
                    Text.Get(e.OwnerKind == "Self" ? "planning.visibility.Self" : $"planning.visibility.{e.Visibility}")));
            return new(Json(entries), CapacityLink(c, "list", grid) + "&withoutProject=true", [new("personName", "person"), new("ownerName", "owner"), new("label", "label"),
                new("source", "source"), new("hoursPerWeek", "hours", "number"), new("startWeek", "start", "date"), new("endWeek", "end", "date"), new("confidence", "confidence"), new("visibility", "visibility")]);
        }
        var entryIds = grid.People.SelectMany(p => p.Entries).Select(e => e.Id).Distinct().ToArray();
        var createdAt = await c.Db.PlanningEntries.AsNoTracking().Where(e => entryIds.Contains(e.Id)).ToDictionaryAsync(e => e.Id, e => e.CreatedAt);
        var sources = PlanningSource.All.Where(x => c.Str("source") == null || c.Str("source") == x).ToArray();
        var values = sources.Append("approved").ToDictionary(x => x, _ => new decimal[grid.Weeks.Length]);
        foreach (var person in grid.People)
        for (var i = 0; i < grid.Weeks.Length; i++) {
            var week = grid.Weeks[i]; var entries = person.Entries.Where(e => e.StartWeek <= week && e.EndWeek >= week).ToArray();
            var approved = person.ApprovedAllocations.GroupBy(a => a.ProjectId).ToDictionary(g => g.Key, g => g.Sum(a => a.Weeks[i].Hours));
            var bands = PlanningRules.Combine(person.Cells[i].Capacity, entries.Select(e => new EntryWeek(e.Id, e.ProjectId, e.Confidence, e.HoursPerWeek, e.StartWeek, createdAt[e.Id])).ToArray(), approved);
            if (c.Str("source") == null && (c.Str("confidence") == null || c.Str("confidence") == PlanningConfidence.Confirmed)) values["approved"][i] += bands.Approved;
            foreach (var entry in entries.Where(e => values.ContainsKey(e.SourceCategory) && (c.Str("confidence") == null || e.Confidence == c.Str("confidence"))))
                values[entry.SourceCategory][i] += bands.Entries.Single(x => x.Id == entry.Id).Counted;
        }
        var rows = new JsonArray();
        foreach (var source in sources.Append("approved").Append("total")) {
            var row = new JsonObject { ["measure"] = Text.Get(source is "approved" or "total" ? $"capacityReport.{source}" : $"planning.source.{source}") };
            for (var i = 0; i < grid.Weeks.Length; i++) row[$"w{i}"] = decimal.Round(source == "total" ? values.Values.Sum(x => x[i]) : values[source][i], 1);
            rows.Add(row);
        }
        return new(rows, CapacityLink(c, "list", grid), CapacityColumns(grid));
    }
    static async Task<Result> PilotRouteUse(Ctx c)
    {
        var weeks = c.Int("weeks") ?? 8; Check.That(weeks is >= 1 and <= 26, "weeks", "error.range");
        var first = Workload.WeekOf(c.Today).AddDays(-7 * (weeks - 1));
        var days = await c.Db.RouteCounts.AsNoTracking().Where(r => r.Day >= first && r.Day <= c.Today).ToListAsync();
        return new(Json(days.GroupBy(r => (r.RouteKey, Week: Workload.WeekOf(r.Day))).OrderBy(g => g.Key.Week).ThenBy(g => g.Key.RouteKey)
            .Select(g => new RouteUseRow(g.Key.RouteKey, g.Key.Week, g.Sum(x => x.Opens), g.Sum(x => x.Under500Ms),
                g.Sum(x => x.From500To999Ms), g.Sum(x => x.From1000To1999Ms), g.Sum(x => x.From2000To4999Ms), g.Sum(x => x.Over5000Ms)))));
    }

}
