using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Portfolio Dashboard (§13.12, FR-PORT-01, FR-HLT-03, §16.4, §16.5): which projects need help this week and why. It is
/// the project list's own query (same filters and permissions) plus tiles and an 8-week health trend from snapshots.
public static class PortfolioEndpoints
{
    static readonly Col[] Cols = Hub.Api.Records.ScreenLists.PortfolioCols;

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/portfolio", async ([AsParameters] ProjectEndpoints.ProjectQuery f, bool? overdueDecisions, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (rows, ownOnly) = await Rows(f, access, db, me, store, clock, overdueDecisions);
            var today = clock.Today(await store.Get(db));
            // AC-CNT-01: the Active tile sets status and keeps the other filters; a health tile also replaces computedHealth.
            // So both count the rows without the status and computed-health filters, which is what their links open.
            var open = f.Status is null && f.ComputedHealth is null ? rows : (await Rows(f with { Status = null, ComputedHealth = null }, access, db, me, store, clock, overdueDecisions)).Rows;
            var active = open.Where(r => r!["status"]!.GetValue<string>() == ProjectStatus.Active).ToList();
            var computed = Http.List(f.ComputedHealth);
            int Count(string health) => active.Count(r => r!["computedHealth"]?.GetValue<string>() == health);
            return new
            {
                OwnOnly = ownOnly,
                Tiles = new
                {
                    Active = active.Count(r => computed.Length == 0 || computed.Contains(r!["computedHealth"]?.GetValue<string>() ?? "")), Red = Count(Health.Red), Yellow = Count(Health.Yellow), Green = Count(Health.Green),
                    Submissions14 = rows.Count(r => r!["nextSubmission"]?["date"]?.GetValue<string>() is { } d && DateOnly.Parse(d) >= today && DateOnly.Parse(d) <= today.AddDays(14)),
                    OverdueDecisions = rows.Sum(r => r!["overdueDecisions"]?.GetValue<int>() ?? 0),
                },
                Projects = rows,
            };
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
    public static async Task<(JsonArray Rows, bool OwnOnly)> Rows(ProjectEndpoints.ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock, bool? overdueDecisions = null)
    {
        var a = access.Actor;
        Access.Demand(Permissions.ViewPortfolio(a));
        var ownOnly = f.Mine ?? (a.SystemPM && !(a.Admin || a.Executive || a.Supervisor));
        var rows = JsonSerializer.SerializeToNode(await ProjectEndpoints.Sorted(f with { Mine = ownOnly, Sort = f.Sort ?? "submission" }, access, db, me, store, clock), JsonOpts.Web)!.AsArray();
        if (overdueDecisions == true) rows = new JsonArray(rows.Where(r => (r?["overdueDecisions"]?.GetValue<int>() ?? 0) > 0).Select(r => r!.DeepClone()).ToArray());
        var ids = rows.Select(r => Guid.Parse(r!["id"]!.GetValue<string>())).ToList();
        var s = await store.Get(db);
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
            if (notes is not null) r!["statusNote"] = notes.GetValueOrDefault(Guid.Parse(r!["id"]!.GetValue<string>()))?.DeepClone();
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
}
