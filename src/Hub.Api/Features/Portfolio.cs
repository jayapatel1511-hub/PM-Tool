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
    static readonly Col[] Cols =
    [
        new("projectNumber", "project"), new("name", "name"), new("pm.displayName", "pm"), new("client.name", "client"), new("office", "office"), new("phase", "phase"),
        new("status", "status"), new("computedHealth", "computedHealth"), new("reportedHealth", "reportedHealth"), new("why", "why"), new("healthOverrideNote", "overrideNote"),
        new("nextMilestone.name", "nextMilestone"), new("nextMilestone.date", "nextMilestoneDate", "date"), new("nextSubmission.name", "nextSubmission"),
        new("nextSubmission.date", "nextSubmissionDate", "date"), new("overdueTasks", "overdueTasks", "number"), new("blockedTasks", "blockedTasks", "number"),
        new("overdueDecisions", "overdueDecisions", "number"), new("highIssues", "highIssues", "number"), new("attentionCritical", "critical", "number"),
        new("attentionWarning", "warning", "number"),
    ];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/portfolio", async ([AsParameters] ProjectEndpoints.ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (rows, ownOnly) = await Rows(f, access, db, me, store, clock);
            var today = clock.Today(await store.Get(db));
            var active = rows.Where(r => r!["status"]!.GetValue<string>() == ProjectStatus.Active).ToList();
            int Count(string health) => active.Count(r => r!["computedHealth"]?.GetValue<string>() == health);
            return new
            {
                OwnOnly = ownOnly,
                Tiles = new
                {
                    Active = active.Count, Red = Count(Health.Red), Yellow = Count(Health.Yellow), Green = Count(Health.Green),
                    Submissions14 = rows.Count(r => r!["nextSubmission"]?["date"]?.GetValue<string>() is { } d && DateOnly.Parse(d) >= today && DateOnly.Parse(d) <= today.AddDays(14)),
                    OverdueDecisions = rows.Sum(r => r!["overdueDecisions"]?.GetValue<int>() ?? 0),
                },
                Projects = rows,
            };
        });
        api.MapGet("/portfolio/export", async (string? format, [AsParameters] ProjectEndpoints.ProjectQuery f, HttpContext http, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock) =>
        {
            var (rows, _) = await Rows(f, access, db, me, store, clock);
            var parameters = http.Request.Query.Where(k => k.Key != "format" && !string.IsNullOrWhiteSpace(k.Value)).Select(k => (Text.Get($"param.{k.Key}"), k.Value.ToString())).ToList();
            return await ExportFile.Send(db, store, format, Text.Get("nav.portfolio"), Cols, rows, parameters, null, "portfolio", clock);
        });
    }

    /// FR-003: Executives, Supervisors and Admins see every project they may view; a PM sees their own unless they ask for all.
    public static async Task<(JsonArray Rows, bool OwnOnly)> Rows(ProjectEndpoints.ProjectQuery f, Access access, HubDb db, CurrentUser me, SettingsStore store, TimeProvider clock)
    {
        var a = access.Actor;
        Access.Demand(Permissions.ViewPortfolio(a));
        var ownOnly = f.Mine ?? (a.SystemPM && !(a.Admin || a.Executive || a.Supervisor));
        var rows = JsonSerializer.SerializeToNode(await ProjectEndpoints.Sorted(f with { Mine = ownOnly, Sort = f.Sort ?? "submission" }, access, db, me, store, clock), JsonOpts.Web)!.AsArray();
        var ids = rows.Select(r => Guid.Parse(r!["id"]!.GetValue<string>())).ToList();
        var today = clock.Today(await store.Get(db));
        var since = today.AddDays(-7 * 8 + 1);
        var snaps = (await db.HealthSnapshots.AsNoTracking().Where(x => ids.Contains(x.ProjectId) && x.SnapshotDate >= since)
            .Select(x => new { x.ProjectId, x.SnapshotDate, x.ComputedHealth, x.ReportedHealth }).ToListAsync()).ToLookup(x => x.ProjectId);
        foreach (var r in rows)
        {
            var mine = snaps[Guid.Parse(r!["id"]!.GetValue<string>())].ToList();
            // FR-HLT-03: one point per week, the last snapshot of that week, oldest first.
            r["trend"] = new JsonArray([.. Enumerable.Range(0, 8).Select(i =>
            {
                var end = today.AddDays(-7 * (7 - i));
                var snap = mine.Where(x => x.SnapshotDate > end.AddDays(-7) && x.SnapshotDate <= end).MaxBy(x => x.SnapshotDate);
                return (JsonNode)new JsonObject { ["weekEnding"] = end.ToString("yyyy-MM-dd"), ["computed"] = snap?.ComputedHealth, ["reported"] = snap?.ReportedHealth };
            })]);
            r["why"] = string.Join("; ", (r["healthReasons"] as JsonArray ?? []).Select(x => x?["text"]?.GetValue<string>()).OfType<string>());
        }
        return (rows, ownOnly);
    }
}
