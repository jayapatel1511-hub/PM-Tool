using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Portfolio Dashboard (§13.12, FR-PORT-01, FR-HLT-03, §16.4, §16.5): which projects need help this week and why. It is
/// the project list's own query (same filters and permissions) plus tiles and an 8-week health trend from snapshots.
public static class PortfolioEndpoints
{
    static readonly Col[] Cols = Hub.Api.Records.ScreenLists.PortfolioCols;

    public sealed record PortfolioTiles(int Active, int Red, int Yellow, int Green, int Submissions14, int OverdueDecisions);
    /// `Projects` is the project list's own row plus the Portfolio columns. It stays a JsonArray (typed `unknown[]` in the contract) because the row
    /// is built from the list's rows (Projects.cs), which Reports and the export also read; it becomes a record when Projects.cs is converted.
    public sealed record PortfolioView(bool OwnOnly, PortfolioTiles Tiles, JsonArray Projects,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? AsOf = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PortfolioChanges? Changes = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, PortfolioCardFact>? CardFacts = null);
    public sealed record PortfolioCardFact(int Projects, int Clients);
    public sealed record PortfolioChangedProject(Guid Id, string ProjectNumber, string Name);
    /// Count is unknown when this metric has incomplete history. Observed and Projects
    /// remain exact positive evidence within the returned, currently authorized cohort.
    public sealed record PortfolioChangeMetric(int? Count, int Observed, int UnavailableProjects, IReadOnlyList<PortfolioChangedProject> Projects);
    public sealed record PortfolioChanges(int Days, DateOnly FromDate, DateTimeOffset Through, int ScopeProjects, bool ScopeCeilingReached,
        PortfolioChangeMetric Health, PortfolioChangeMetric Milestones, PortfolioChangeMetric Status);
    sealed class ReadClock(DateTimeOffset at) : TimeProvider { public override DateTimeOffset GetUtcNow() => at; }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/portfolio", async Task<Ok<PortfolioView>> ([AsParameters] ProjectEndpoints.ProjectQuery f, bool? overdueDecisions, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var s = await store.Get(db);
            var screens = s.IsFlagOn("project_screens"); var visual = s.IsFlagOn("visual_system");
            var asOf = clock.GetUtcNow();
            var readClock = screens || visual ? new ReadClock(asOf) : clock;
            await using var snapshot = screens ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
            var (rows, ownOnly) = await Rows(f, access, db, me, store, readClock, overdueDecisions, includeStaffing: screens);
            var today = readClock.Today(s);
            // AC-CNT-01: the Active tile sets status and keeps the other filters; a health tile also replaces computedHealth.
            // So both count the rows without the status and computed-health filters, which is what their links open.
            var open = f.Status is null && f.ComputedHealth is null ? rows : (await Rows(f with { Status = null, ComputedHealth = null }, access, db, me, store, readClock, overdueDecisions)).Rows;
            var active = open.Where(r => r!["status"]!.GetValue<string>() == ProjectStatus.Active).ToList();
            var computed = Http.List(f.ComputedHealth);
            int Count(string health) => active.Count(r => r!["computedHealth"]?.GetValue<string>() == health);
            bool Submission(JsonNode? r) => r?["nextSubmission"]?["date"]?.GetValue<string>() is { } d && DateOnly.Parse(d) >= today && DateOnly.Parse(d) <= today.AddDays(14);
            PortfolioCardFact Fact(IEnumerable<JsonNode?> cohort)
            {
                var items = cohort.ToArray();
                return new(items.Length, items.Select(r => r!["client"]!["id"]!.GetValue<string>()).Distinct().Count());
            }
            var cardFacts = visual ? new Dictionary<string, PortfolioCardFact> {
                ["active"] = Fact(active.Where(r => computed.Length == 0 || computed.Contains(r!["computedHealth"]?.GetValue<string>() ?? ""))),
                ["red"] = Fact(active.Where(r => r!["computedHealth"]?.GetValue<string>() == Health.Red)),
                ["yellow"] = Fact(active.Where(r => r!["computedHealth"]?.GetValue<string>() == Health.Yellow)),
                ["green"] = Fact(active.Where(r => r!["computedHealth"]?.GetValue<string>() == Health.Green)),
                ["submissions14"] = Fact(rows.Where(Submission)), ["overdueDecisions"] = Fact(rows.Where(r => (r?["overdueDecisions"]?.GetValue<int>() ?? 0) > 0)),
            } : null;
            var changes = screens ? await ChangeSummary(rows, db, s, asOf) : null;
            return TypedResults.Ok(new PortfolioView(ownOnly,
                new PortfolioTiles(
                    Active: active.Count(r => computed.Length == 0 || computed.Contains(r!["computedHealth"]?.GetValue<string>() ?? "")), Red: Count(Health.Red), Yellow: Count(Health.Yellow), Green: Count(Health.Green),
                    Submissions14: rows.Count(Submission),
                    OverdueDecisions: rows.Sum(r => r!["overdueDecisions"]?.GetValue<int>() ?? 0)),
                rows, screens || visual ? asOf : null, changes, cardFacts));
        });
        // FR-ADR-07, FR-ADR-08: the admin strip, for Executives (and Admins, who see the page itself); absent with the flag off.
        api.MapGet("/portfolio/admin-review", async (Access access, HubDb db, SettingsStore store) =>
        {
            var a = access.Actor;
            Access.Demand(a.Admin || a.Executive ? Allow.Yes : Allow.No("perm.admin_review_strip"));
            var s = await store.Get(db);
            Http.RequireFlag(s, AdminReviewEndpoints.Flag);
            return TypedResults.Ok(await AdminReviewEndpoints.LastReview(db, s));
        });
        api.MapGet("/portfolio/export", async (string? format, [AsParameters] ProjectEndpoints.ProjectQuery f, bool? overdueDecisions, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (rows, _) = await Rows(f, access, db, me, store, clock, overdueDecisions);
            var parameters = http.Request.Query.Where(k => k.Key != "format" && !string.IsNullOrWhiteSpace(k.Value)).Select(k => (Text.Get($"param.{k.Key}"), k.Value.ToString())).ToList();
            var settings = await store.Get(db);
            Col[] columns = [.. Cols,
                .. settings.IsFlagOn(DashboardEndpoints.RollupsFlag) ? new Col[] { new("overdueDeliverables", "overdueDeliverables", "number"), new("projectType", "projectType") } : [],
                .. settings.IsFlagOn("project_status_note") ? new Col[] { new("statusNote.text", "statusNote") } : []];
            return await ExportFile.Send(db, store, format, Text.Get("nav.portfolio"), columns, rows, parameters, null, "portfolio", clock);
        });
    }

    /// FR-003: Executives, Supervisors and Admins see every project they may view; a PM sees their own unless they ask for all.
    public static async Task<(JsonArray Rows, bool OwnOnly)> Rows(ProjectEndpoints.ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock, bool? overdueDecisions = null, bool includeStaffing = false)
    {
        var a = access.Actor;
        Access.Demand(Permissions.ViewPortfolio(a));
        var ownOnly = f.Mine ?? (a.SystemPM && !(a.Admin || a.Executive || a.Supervisor));
        var source = await ProjectEndpoints.Sorted(f with { Mine = ownOnly, Sort = f.Sort ?? "submission" }, access, db, me, store, clock);
        if (overdueDecisions == true) source = source.Where(r => r.OverdueDecisions > 0).ToList();
        var s = await store.Get(db);
        if (includeStaffing && s.IsFlagOn("project_screens")) source = await ProjectListCompleteness.Enrich(source, access, db, store, s, clock, includeSpan: false);
        var rows = JsonSerializer.SerializeToNode(source, JsonOpts.Web)!.AsArray();
        if (overdueDecisions == true) rows = new JsonArray(rows.Where(r => (r?["overdueDecisions"]?.GetValue<int>() ?? 0) > 0).Select(r => r!.DeepClone()).ToArray());
        var ids = rows.Select(r => Guid.Parse(r!["id"]!.GetValue<string>())).ToList();
        var today = clock.Today(s);
        // FR-RCV-16: overdue deliverables per row, counted by the deliverable list's `indicator=overdue` predicate, only while
        // `flag.status_rollups` is on. Rows are already the projects this viewer may see (AC-RCV-21).
        var overdue = s.IsFlagOn(DashboardEndpoints.RollupsFlag)
            ? await DeliverableEndpoints.Filter(db, db.Deliverables.AsNoTracking().Where(x => ids.Contains(x.ProjectId)), indicator: "overdue")
                .GroupBy(x => x.ProjectId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count)
            : null;
        var types = s.IsFlagOn(DashboardEndpoints.RollupsFlag) ? await db.ProjectTypes.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name) : null;
        var since = today.AddDays(-7 * 8 + 1);
        var snaps = (await db.HealthSnapshots.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && x.SnapshotDate >= since)
            .Select(x => new { x.ProjectId, x.SnapshotDate, x.ComputedHealth, x.ReportedHealth }).ToListAsync()).ToLookup(x => x.ProjectId);
        var notes = s.IsFlagOn("project_status_note") ? await ProjectStatusNote.ReadMany(await db.Projects.AsNoTracking().Where(p => ids.Contains(p.Id)).ToListAsync(), db, clock) : null;
        foreach (var r in rows)
        {
            if (notes is not null) r!["statusNote"] = JsonSerializer.SerializeToNode(notes.GetValueOrDefault(Guid.Parse(r!["id"]!.GetValue<string>())), JsonOpts.Web);
            var mine = snaps[Guid.Parse(r!["id"]!.GetValue<string>())].ToList();
            // FR-HLT-03: one point per week, the last snapshot of that week, oldest first.
            r["trend"] = new JsonArray([.. Enumerable.Range(0, 8).Select(i =>
            {
                var end = today.AddDays(-7 * (7 - i));
                var snap = mine.Where(x => x.SnapshotDate > end.AddDays(-7) && x.SnapshotDate <= end).MaxBy(x => x.SnapshotDate);
                return (JsonNode)new JsonObject { ["weekEnding"] = end.ToString("yyyy-MM-dd"), ["computed"] = snap?.ComputedHealth, ["reported"] = snap?.ReportedHealth };
            })]);
            if (overdue is not null) r["overdueDeliverables"] = overdue.GetValueOrDefault(Guid.Parse(r["id"]!.GetValue<string>()));
            if (types is not null) r["projectType"] = Guid.TryParse(r["projectTypeId"]?.GetValue<string>(), out var typeId) ? types.GetValueOrDefault(typeId) : null;
            r["why"] = string.Join("; ", (r["healthReasons"] as JsonArray ?? []).Select(x => x?["text"]?.GetValue<string>()).OfType<string>());
        }
        return (rows, ownOnly);
    }

    static async Task<PortfolioChanges> ChangeSummary(JsonArray rows, HubDb db, OrgSettings settings, DateTimeOffset asOf)
    {
        var today = Clock.LocalDate(asOf, settings); var days = settings.TrendCompareDays; var from = today.AddDays(-days);
        var projects = rows.Select(r => new PortfolioChangedProject(Guid.Parse(r!["id"]!.GetValue<string>()), r["projectNumber"]!.GetValue<string>(), r["name"]!.GetValue<string>())).ToArray();
        var ids = projects.Select(p => p.Id).ToArray();
        var snapshots = (await db.HealthSnapshots.AsNoTracking().Where(s => ids.Contains(s.ProjectId) && s.SnapshotDate >= from && s.SnapshotDate <= today)
            .Select(s => new HealthReading(s.ProjectId, s.SnapshotDate, s.ComputedHealth)).ToListAsync()).ToLookup(s => s.ProjectId);
        var current = await db.ProjectStates.AsNoTracking().Where(s => ids.Contains(s.ProjectId)).ToDictionaryAsync(s => s.ProjectId, s => s.ComputedHealth);
        // Activity is append-only. A retained project-creation record provides its
        // beginning; imported/legacy rows without that provenance remain incomplete.
        var begun = (await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && ids.Contains(a.ProjectId.Value)
            && a.ItemType == ItemType.Project && a.ItemId == a.ProjectId && a.Action == "Created" && a.OccurredAt <= asOf)
            .Select(a => a.ProjectId!.Value).Distinct().ToListAsync()).ToHashSet();
        // A broad UTC bound followed by organisation-calendar filtering avoids
        // inventing a UTC midnight or dropping a DST-boundary event.
        var broadFrom = asOf.AddDays(-days - 2);
        var activity = await db.ActivityLog.AsNoTracking().Where(a => a.ProjectId != null && ids.Contains(a.ProjectId.Value)
            && (a.ItemType == ItemType.Project || a.ItemType == ItemType.Milestone) && a.OccurredAt >= broadFrom && a.OccurredAt <= asOf)
            .Select(a => new ChangeActivity(a.ProjectId, a.ItemType, a.ItemId, a.Action, a.Categories, a.Changes, a.OccurredAt)).ToListAsync();
        var healthMoved = new HashSet<Guid>(); var statusMoved = new HashSet<Guid>(); var slipped = new Dictionary<Guid, HashSet<Guid>>();
        var healthUnknown = new HashSet<Guid>(); var milestoneUnknown = ids.Where(id => !begun.Contains(id)).ToHashSet(); var statusUnknown = milestoneUnknown.ToHashSet();
        foreach (var project in projects)
        {
            var readings = snapshots[project.Id].OrderBy(s => s.SnapshotDate).Where(s => Health.All.Contains(s.ComputedHealth)).ToArray();
            var dates = readings.Select(s => s.SnapshotDate).ToHashSet();
            var values = readings.Select(s => s.ComputedHealth).ToList();
            if (current.TryGetValue(project.Id, out var value) && Health.All.Contains(value)) values.Add(value);
            else healthUnknown.Add(project.Id);
            if (Enumerable.Range(0, days).Any(i => !dates.Contains(from.AddDays(i)))) healthUnknown.Add(project.Id);
            if (values.Distinct().Count() > 1) healthMoved.Add(project.Id);
        }
        foreach (var item in activity.Where(a => Clock.LocalDate(a.OccurredAt, settings) >= from))
        {
            var projectId = item.ProjectId!.Value; var isStatus = item.ItemType == ItemType.Project;
            var unknown = isStatus ? statusUnknown : milestoneUnknown;
            if (item.ItemId is null || isStatus && item.ItemId != projectId) { unknown.Add(projectId); continue; }
            var expectsPayload = isStatus ? item.Action == "StatusChanged" || item.Categories.Contains("status")
                : item.Action == "DateChanged" || item.Categories.Contains("date");
            var usableTarget = false;
            try
            {
                using var document = JsonDocument.Parse(item.Changes);
                if (document.RootElement.ValueKind != JsonValueKind.Array) { unknown.Add(projectId); continue; }
                foreach (var change in document.RootElement.EnumerateArray())
                {
                    if (change.ValueKind != JsonValueKind.Object || !change.TryGetProperty("field", out var field) || field.ValueKind != JsonValueKind.String) { unknown.Add(projectId); continue; }
                    if (field.GetString() != (isStatus ? "Status" : "Date")) continue;
                    if (!change.TryGetProperty("old", out var old) || !change.TryGetProperty("new", out var next)) { unknown.Add(projectId); continue; }
                    if (old.ValueKind == JsonValueKind.Null || next.ValueKind == JsonValueKind.Null)
                    {
                        // An explicit initial/cleared date is known non-slip evidence.
                        // A marked lifecycle transition still needs both valid statuses.
                        var other = old.ValueKind == JsonValueKind.Null ? next : old;
                        if (!isStatus && (other.ValueKind == JsonValueKind.Null || other.ValueKind == JsonValueKind.String
                            && DateOnly.TryParseExact(other.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))) usableTarget = true;
                        continue;
                    }
                    if (old.ValueKind != JsonValueKind.String || next.ValueKind != JsonValueKind.String) { unknown.Add(projectId); continue; }
                    if (isStatus)
                    {
                        if (!ProjectStatus.All.Contains(old.GetString()) || !ProjectStatus.All.Contains(next.GetString())) { unknown.Add(projectId); continue; }
                        usableTarget = true;
                        if (old.GetString() != next.GetString()) statusMoved.Add(projectId);
                    }
                    else if (DateOnly.TryParseExact(old.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var before)
                        && DateOnly.TryParseExact(next.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var after))
                    {
                        usableTarget = true;
                        if (after <= before) continue;
                        if (!slipped.TryGetValue(projectId, out var milestones)) slipped[projectId] = milestones = [];
                        milestones.Add(item.ItemId.Value);
                    }
                    else unknown.Add(projectId);
                }
            }
            catch (JsonException) { unknown.Add(projectId); }
            // Named/category-marked events cannot silently become known zero when
            // the corresponding transition was omitted from otherwise valid JSON.
            if (expectsPayload && !usableTarget) unknown.Add(projectId);
        }
        PortfolioChangeMetric Metric(HashSet<Guid> moved, HashSet<Guid> unknown, int? observed = null)
        {
            var count = observed ?? moved.Count;
            return new(unknown.Count == 0 ? count : null, count, unknown.Count, projects.Where(p => moved.Contains(p.Id)).OrderBy(p => p.ProjectNumber, StringComparer.Ordinal).ThenBy(p => p.Id).ToArray());
        }
        return new(days, from, asOf, projects.Length, projects.Length >= 5000,
            Metric(healthMoved, healthUnknown), Metric(slipped.Keys.ToHashSet(), milestoneUnknown, slipped.Values.Sum(s => s.Count)), Metric(statusMoved, statusUnknown));
    }
}

file sealed record HealthReading(Guid ProjectId, DateOnly SnapshotDate, string ComputedHealth);
file sealed record ChangeActivity(Guid? ProjectId, string ItemType, Guid? ItemId, string Action, List<string> Categories, string Changes, DateTimeOffset OccurredAt);
