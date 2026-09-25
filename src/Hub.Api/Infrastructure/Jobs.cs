using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// A scheduled background job hosted in the API process (§23.5). Runs are recorded in `job_run`
/// and guarded by a PostgreSQL advisory lock so scaled-out instances never run a job twice.
public interface IJob
{
    string Name { get; }
    bool IsDue(DateTimeOffset nowUtc, DateTimeOffset? lastSuccessUtc, OrgSettings s);
    Task<object?> Run(IServiceProvider sp, CancellationToken ct);
}

public static class Schedule
{
    public static bool Every(TimeSpan interval, DateTimeOffset now, DateTimeOffset? last) => last is null || now - last >= interval;

    /// Due once per organisation-local day, at or after `hhmm`.
    public static bool DailyAt(string hhmm, DateTimeOffset now, DateTimeOffset? last, OrgSettings s)
    {
        var local = Clock.Local(now, s);
        if (TimeOnly.FromDateTime(local.DateTime) < TimeOnly.Parse(hhmm)) return false;
        return last is null || Clock.LocalDate(last.Value, s) < DateOnly.FromDateTime(local.DateTime);
    }
}

public sealed class JobHost(IServiceProvider root, TimeProvider clock, ILogger<JobHost> log, IEnumerable<IJob> jobs, IConfiguration cfg) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (cfg["Jobs:Enabled"] == "false") return;
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        // Each job runs on its own, so a long nightly run never holds up email, digests or the watchdog (packet 011).
        var running = new Dictionary<string, Task>();
        while (!ct.IsCancellationRequested)
        {
            foreach (var job in jobs)
            {
                if (running.TryGetValue(job.Name, out var busy) && !busy.IsCompleted) continue;
                running[job.Name] = Task.Run(async () =>
                {
                    try { await RunIfDue(job, false, ct); }
                    catch (Exception e) when (!ct.IsCancellationRequested) { log.LogError(e, "Job {Job} failed to start", job.Name); }
                }, ct);
            }
            await Task.Delay(TimeSpan.FromSeconds(20), ct);
        }
    }

    public async Task<bool> RunIfDue(IJob job, bool force, CancellationToken ct)
    {
        using var lockScope = root.CreateScope();
        var db = lockScope.ServiceProvider.GetRequiredService<HubDb>();
        var s = await lockScope.ServiceProvider.GetRequiredService<SettingsStore>().Get(db);
        var last = await db.JobRuns.Where(r => r.JobName == job.Name && r.Status == "Succeeded").MaxAsync(r => (DateTimeOffset?)r.StartedAt, ct);
        if (!force && !job.IsDue(clock.GetUtcNow(), last, s)) return false;

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            var got = await db.Database.SqlQuery<bool>($"SELECT pg_try_advisory_lock(hashtext({job.Name})) AS \"Value\"").SingleAsync(ct);
            if (!got) return false;
            var run = new JobRun { JobName = job.Name, StartedAt = clock.GetUtcNow() };
            db.JobRuns.Add(run);
            await db.SaveChangesAsync(ct);
            try
            {
                using var work = root.CreateScope();
                work.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
                var details = await job.Run(work.ServiceProvider, ct);
                run.Status = "Succeeded";
                run.Details = details is null ? null : JsonSerializer.Serialize(details, JsonOpts.Web);
            }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                log.LogError(e, "Job {Job} failed", job.Name);
                run.Status = "Failed";
                run.Details = JsonSerializer.Serialize(new { error = e.GetType().Name });
            }
            run.FinishedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(CancellationToken.None);
            return run.Status == "Succeeded";
        }
        finally
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_unlock(hashtext({job.Name}))", CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }
}
