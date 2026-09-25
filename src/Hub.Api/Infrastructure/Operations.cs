using System.Globalization;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// What the watchdog reads each run (§22, §23.8; packet 011 FR-006).
public sealed record OpsFacts(
    DateTimeOffset Now, OrgSettings Settings,
    DateTimeOffset? OldestPendingOutbox, DateTimeOffset? LastNightlySuccess, DateTimeOffset? FirstJobRun,
    IReadOnlyList<(string Job, DateTimeOffset At)> FailedSince, int DigestsNotBuilt, int DigestsUnsent);

public sealed record OpsProblem(string Kind, string Detail);

/// Operator alert conditions as one pure function. A failed request rate is alerted from request telemetry instead, and
/// a missing heartbeat catches a stopped job host (both in infra/main.bicep).
public static class OpsChecks
{
    public static readonly TimeSpan EvaluationLag = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan NightlyGrace = TimeSpan.FromHours(26);
    public static readonly TimeOnly DigestDeadline = new(8, 0);

    public static List<OpsProblem> Evaluate(OpsFacts f)
    {
        var problems = new List<OpsProblem>();
        if (f.OldestPendingOutbox is { } oldest && f.Now - oldest > EvaluationLag)
            problems.Add(new("EvaluationDelayed", $"oldest change waiting {(int)(f.Now - oldest).TotalMinutes} min"));
        // A new installation has had no night yet: count from the first job run.
        var since = f.LastNightlySuccess ?? f.FirstJobRun;
        if (since is { } s && f.Now - s > NightlyGrace)
            problems.Add(new("NightlyMissed", f.LastNightlySuccess is null ? "no successful nightly run yet" : $"last success {s:u}"));
        foreach (var (job, at) in f.FailedSince) problems.Add(new("JobFailed", $"{job} at {at:u}"));
        if (DigestDue(f.Now, f.Settings) && f.DigestsNotBuilt + f.DigestsUnsent > 0)
            problems.Add(new("DigestLate", $"{f.DigestsNotBuilt} not built, {f.DigestsUnsent} not sent by {DigestDeadline:HH\\:mm}"));
        return problems;
    }

    /// Past 08:00 organisation time on a digest day (weekends only when weekend digests are on).
    public static bool DigestDue(DateTimeOffset now, OrgSettings s)
    {
        var local = Clock.Local(now, s);
        if (!s.WeekendDigests && local.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        return TimeOnly.FromDateTime(local.DateTime) >= DigestDeadline;
    }

    /// Reads the facts from the database; `since` is when the previous watchdog run started.
    public static async Task<OpsFacts> Gather(HubDb db, OrgSettings s, DateTimeOffset now, DateTimeOffset since, CancellationToken ct = default)
    {
        var oldest = await db.Outbox.Where(o => o.ProcessedAt == null).MinAsync(o => (DateTimeOffset?)o.CreatedAt, ct);
        var nightly = await db.JobRuns.Where(r => r.JobName == NightlyJob.JobName && r.Status == "Succeeded").MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct);
        var first = await db.JobRuns.MinAsync(r => (DateTimeOffset?)r.StartedAt, ct);
        var failed = (await db.JobRuns.Where(r => r.Status == "Failed" && r.StartedAt >= since).OrderBy(r => r.StartedAt).Select(r => new { r.JobName, r.StartedAt }).ToListAsync(ct))
            .Select(r => (r.JobName, r.StartedAt)).ToList();
        int notBuilt = 0, unsent = 0;
        if (OpsChecks.DigestDue(now, s))
        {
            var zone = Clock.Zone(s);
            var today = DateOnly.FromDateTime(Clock.Local(now, s).DateTime);
            var dayStart = new DateTimeOffset(today.ToDateTime(TimeOnly.MinValue), zone.GetUtcOffset(today.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
            // Digests due by 08:00: each active person with digests on whose chosen time is at or before it.
            var prefs = await db.UserSettings.AsNoTracking().ToDictionaryAsync(x => x.UserId, ct);
            foreach (var id in await db.Users.Where(u => u.IsActive).Select(u => u.Id).ToListAsync(ct))
            {
                var p = prefs.GetValueOrDefault(id);
                if (p is { DigestEnabled: false }) continue;
                if (!TimeOnly.TryParseExact(p?.DigestTimeLocal ?? s.DigestSendTimeLocal, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) || at > DigestDeadline) continue;
                var scheduled = new DateTimeOffset(today.ToDateTime(at), zone.GetUtcOffset(today.ToDateTime(at))).ToUniversalTime();
                if (p?.LastDigestAt is not { } last || last < scheduled) notBuilt++;
            }
            unsent = await db.Emails.CountAsync(e => e.Kind == "Digest" && e.CreatedAt >= dayStart && e.SentAt == null, ct);
        }
        return new OpsFacts(now, s, oldest, nightly, first, failed, notBuilt, unsent);
    }
}

/// Every five minutes: logs each problem as an error named OpsAlert (the alert rule's signal) and a heartbeat.
public sealed class OpsWatchdogJob(ILogger<OpsWatchdogJob> log) : IJob
{
    public string Name => "ops-watchdog";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromMinutes(5), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        var s = await sp.GetRequiredService<SettingsStore>().Get(db);
        var since = await db.JobRuns.Where(r => r.JobName == Name && r.Status == "Succeeded").MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct) ?? now.AddMinutes(-5);
        var problems = OpsChecks.Evaluate(await OpsChecks.Gather(db, s, now, since, ct));
        foreach (var p in problems) log.LogError("OpsAlert {Kind}: {Detail}", p.Kind, p.Detail);
        log.LogInformation("OpsHeartbeat");
        return new { problems = problems.Select(p => p.Kind) };
    }
}
