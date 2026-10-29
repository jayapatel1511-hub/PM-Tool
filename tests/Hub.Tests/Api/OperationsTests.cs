using System.Net;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hub.Tests.Api;

/// Packet 011 FR-006: the conditions operators are alerted on (§22, §23.8), as worked examples of the pure check and
/// through the admin Operations endpoint and the watchdog job.
[Collection("api")]
public sealed class OperationsTests(HubFactory f)
{
    static readonly OrgSettings Halifax = new(); // America/Halifax, digests at 07:00, no weekend digests
    // Thursday 2026-09-24 08:05 in Halifax (UTC-3).
    static readonly DateTimeOffset Now = new(2026, 9, 24, 11, 5, 0, TimeSpan.Zero);

    static OpsFacts Facts(DateTimeOffset? outbox = null, DateTimeOffset? nightly = null, DateTimeOffset? first = null,
        (string, DateTimeOffset)[]? failed = null, int notBuilt = 0, int unsent = 0, DateTimeOffset? now = null) =>
        new(now ?? Now, Halifax, outbox, nightly ?? Now.AddHours(-8), first ?? Now.AddDays(-30), failed ?? [], notBuilt, unsent);

    static string[] Kinds(OpsFacts facts) => OpsChecks.Evaluate(facts).Select(p => p.Kind).ToArray();

    [Fact]
    public void Request_bodies_are_limited_to_one_megabyte() // T-11: the largest legitimate body is a template structure of a few kilobytes
    {
        var kestrel = f.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value;
        Assert.Equal(1024 * 1024, kestrel.Limits.MaxRequestBodySize);
    }

    [Fact]
    public void Each_condition_raises_its_alert_and_only_past_its_threshold()
    {
        Assert.Empty(Kinds(Facts()));
        Assert.Empty(Kinds(Facts(outbox: Now.AddMinutes(-9))));
        Assert.Equal(["EvaluationDelayed"], Kinds(Facts(outbox: Now.AddMinutes(-11))));
        Assert.Empty(Kinds(Facts(nightly: Now.AddHours(-25))));
        Assert.Equal(["NightlyMissed"], Kinds(Facts(nightly: Now.AddHours(-27))));
        // A new installation counts from its first job run; with no runs at all there is nothing to judge.
        Assert.Equal(["NightlyMissed"], Kinds(new OpsFacts(Now, Halifax, null, null, Now.AddHours(-30), [], 0, 0)));
        Assert.Empty(Kinds(new OpsFacts(Now, Halifax, null, null, Now.AddHours(-2), [], 0, 0)));
        Assert.Empty(Kinds(new OpsFacts(Now, Halifax, null, null, null, [], 0, 0)));
        Assert.Equal(["JobFailed", "JobFailed"], Kinds(Facts(failed: [("email-send", Now.AddMinutes(-3)), ("daily-digest", Now.AddMinutes(-1))])));
        Assert.Equal(["DigestLate"], Kinds(Facts(notBuilt: 2)));
        Assert.Equal(["DigestLate"], Kinds(Facts(unsent: 1)));
        Assert.Empty(Kinds(Facts(unsent: 1, now: Now.AddMinutes(-10)))); // 07:55: not late yet
        var saturday = new DateTimeOffset(2026, 9, 26, 12, 0, 0, TimeSpan.Zero);
        Assert.Empty(Kinds(Facts(unsent: 1, now: saturday, nightly: saturday.AddHours(-8)))); // weekend digests off
        Assert.True(OpsChecks.DigestDue(saturday, Halifax with { WeekendDigests = true }));
    }

    [Fact]
    public async Task Pilot_measures_read_the_weekly_goals_from_snapshots_and_the_log() // FR-001, G1–G4
    {
        var d = new TestData(f);
        var p = await d.Project();
        var alex = d.User(TestData.Alex);
        var t = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, dueDate = "2026-09-30" });
        await d.NewTask(p.Id, TestData.Pm, new { name = "No owner yet" });
        await f.DbAsync(async db =>
        {
            db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = new DateOnly(2026, 9, 8), Counts = """{"tasksUnassigned":3,"tasksNoDueDate":2,"tasksBlocked":1}""" });
            db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = new DateOnly(2026, 9, 11), Counts = """{"tasksUnassigned":2,"tasksNoDueDate":2,"tasksBlocked":1}""" });
            db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = new DateOnly(2026, 9, 14), Counts = """{"tasksUnassigned":1,"tasksNoDueDate":1,"tasksBlocked":0}""" });
            return await db.SaveChangesAsync();
        });
        (await f.As(TestData.Pm).PostAsync($"/api/v1/projects/{p.Id}/coordination/reviewed", null)).EnsureSuccessStatusCode();
        await d.Move(TestData.Alex, t, "In Progress");

        var rows = (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/pilot-measures?projectId={p.Id}&weeks=2").Result.Json())["rows"]!.AsArray();
        Assert.Equal(new[] { "2026-09-07", "2026-09-14" }, rows.Select(r => r!.S("weekOf")));
        var (last, now) = (rows[0]!, rows[1]!);
        Assert.Equal((2, 2, 1, 0, 0), (last.I("unassigned"), last.I("noDueDate"), last.I("blocked"), last.I("reviewed"), last.I("updaters"))); // the week's last snapshot
        Assert.Equal((1, 1, 0, 1), (now.I("unassigned"), now.I("noDueDate"), now.I("blocked"), now.I("reviewed")));
        Assert.Equal((1, 1, 100), (now.I("people"), now.I("updaters"), now.I("updatersPct")));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/reports/pilot-measures?projectId={p.Id}")).StatusCode);
    }

    [Fact]
    public async Task Operators_see_the_checks_and_job_runs_and_the_watchdog_reports_them()
    {
        var clock = f.Clock.Now;
        var outbox = await f.DbAsync(async db =>
        {
            var e = new OutboxEvent { CreatedAt = clock.AddMinutes(-20), Payload = "{}" };
            db.Outbox.Add(e);
            db.JobRuns.Add(new JobRun { JobName = "email-send", StartedAt = clock.AddMinutes(-1), FinishedAt = clock.AddMinutes(-1), Status = "Failed" });
            await db.SaveChangesAsync();
            return e.Id;
        });
        try
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).GetAsync("/api/v1/admin/operations")).StatusCode);
            var ops = await f.As(TestData.Admin).GetAsync("/api/v1/admin/operations").Result.Json();
            var kinds = ops["problems"]!.AsArray().Select(p => p!.S("kind")).ToArray();
            Assert.Contains("EvaluationDelayed", kinds);
            Assert.Contains("JobFailed", kinds);
            Assert.True(ops.I("pendingChanges") >= 1);
            var email = ops["jobs"]!.AsArray().Single(j => j!.S("job") == "email-send")!;
            Assert.Equal("Failed", email.S("status"));
            Assert.True(email.I("failed24h") >= 1);

            await f.RunJob<OpsWatchdogJob>();
            var run = await f.DbAsync(db => db.JobRuns.Where(r => r.JobName == "ops-watchdog").OrderByDescending(r => r.StartedAt).FirstAsync());
            Assert.Equal("Succeeded", run.Status);
            Assert.Contains("EvaluationDelayed", run.Details);
        }
        finally
        {
            await f.DbAsync(async db => { var e = await db.Outbox.FirstAsync(x => x.Id == outbox); e.ProcessedAt = clock; return await db.SaveChangesAsync(); });
        }
    }

    async Task<string> Health(DateTimeOffset at)
    {
        // At a date before every other test's events, so only this test's events count toward the lag.
        var saved = f.Clock.Now;
        f.Clock.Now = at;
        try { return (await f.CreateClient().GetAsync("/health").Result.Json()).S("status"); }
        finally { f.Clock.Now = saved; }
    }

    async Task<int> PendingChanges() => (await f.As(TestData.Admin).GetAsync("/api/v1/admin/operations").Result.Json()).I("pendingChanges");

    [Fact]
    public async Task Ten_failed_evaluations_dead_letter_the_event_and_health_recovers() // FR-OPS-06, FR-OPS-07, AC-OPS-06
    {
        var p = await new TestData(f).Project();
        var past = new DateTimeOffset(2020, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var trigger = "fail_eval_" + p.Id.ToString("N")[^8..];
        var id = await f.DbAsync(async db =>
        {
            // This scenario owns one project-scoped event. Earlier shared-fixture settings
            // events now correctly retry every failed project; isolate them from this case.
            // EvaluationTests separately covers retaining and recovering the global event.
            await db.Outbox.ExecuteDeleteAsync();
            var e = new OutboxEvent { ProjectId = p.Id, CreatedAt = past.AddMinutes(-20), Payload = "{}" };
            db.Outbox.Add(e);
            await db.SaveChangesAsync();
            // Every evaluation of this project fails when it writes the project's state.
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION hub.{trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'test failure'; END $$;
                CREATE TRIGGER {trigger} BEFORE INSERT OR UPDATE ON hub.project_state FOR EACH ROW WHEN (NEW.project_id = '{p.Id}') EXECUTE FUNCTION hub.{trigger}();
                """);
            return e.Id;
        });
        try
        {
            Assert.Equal("Degraded", await Health(past));
            var pendingBefore = 0;
            for (var i = 1; i <= 10; i++)
            {
                if (i == 10) pendingBefore = await PendingChanges();
                await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);
                var e = await f.DbAsync(db => db.Outbox.AsNoTracking().FirstAsync(x => x.Id == id));
                Assert.Equal((i, i == 10, (DateTimeOffset?)null), (e.Attempts, e.DeadLetteredAt is not null, e.ProcessedAt));
            }
            await EvaluationWorker.ProcessOnce(f.Services, f.Clock, CancellationToken.None);
            Assert.Equal(10, (await f.DbAsync(db => db.Outbox.AsNoTracking().FirstAsync(x => x.Id == id))).Attempts); // no longer selected

            Assert.Equal("Healthy", await Health(past));
            Assert.Equal(pendingBefore - 1, await PendingChanges());
            var ops = await f.As(TestData.Admin).GetAsync("/api/v1/admin/operations").Result.Json();
            var dead = ops["problems"]!.AsArray().Single(x => x!.S("kind") == "OutboxDeadLettered")!;
            Assert.StartsWith("1 ", dead.S("detail"));
            await f.RunJob<OpsWatchdogJob>();
            var run = await f.DbAsync(db => db.JobRuns.Where(r => r.JobName == "ops-watchdog").OrderByDescending(r => r.StartedAt).FirstAsync());
            Assert.Contains("OutboxDeadLettered", run.Details);

            // A live event waiting 11 minutes still degrades /health.
            var live = await f.DbAsync(async db => { var e = new OutboxEvent { CreatedAt = past.AddMinutes(-11), Payload = "{}" }; db.Outbox.Add(e); await db.SaveChangesAsync(); return e.Id; });
            Assert.Equal("Degraded", await Health(past));
            await f.DbAsync(db => db.Outbox.Where(o => o.Id == live).ExecuteDeleteAsync());
        }
        finally
        {
            await f.DbAsync(async db =>
            {
                await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER IF EXISTS {trigger} ON hub.project_state; DROP FUNCTION IF EXISTS hub.{trigger}();");
                return await db.Outbox.Where(o => o.Id == id).ExecuteDeleteAsync();
            });
        }
    }

    [Fact]
    public async Task Dead_letters_raise_their_own_problem_and_leave_the_lag() // FR-OPS-07
    {
        Assert.Equal(["OutboxDeadLettered"], Kinds(Facts() with { DeadLettered = 1 }));
        Assert.Equal(["EvaluationDelayed", "OutboxDeadLettered"], Kinds(Facts(outbox: Now.AddMinutes(-11)) with { DeadLettered = 2 }));
        Assert.Empty(Kinds(Facts() with { DeadLettered = 0 }));

        var old = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var e = new OutboxEvent { CreatedAt = old, Payload = "{}", Attempts = 10, DeadLetteredAt = old };
        await f.DbAsync(db => { db.Outbox.Add(e); return db.SaveChangesAsync(); });
        try
        {
            var facts = await f.DbAsync(db => OpsChecks.Gather(db, Halifax, Now, Now));
            Assert.NotEqual(old, facts.OldestPendingOutbox);
            Assert.True(facts.DeadLettered >= 1);
        }
        finally { await f.DbAsync(db => db.Outbox.Where(o => o.Id == e.Id).ExecuteDeleteAsync()); }
    }

    [Fact]
    public async Task Migration_marks_stranded_events_dead_lettered() // FR-OPS-06, AC-OPS-07 (backfill part)
    {
        var at = f.Clock.Now;
        var marked = at.AddDays(-3);
        OutboxEvent E(int attempts, DateTimeOffset? processed = null, DateTimeOffset? dead = null) =>
            new() { CreatedAt = at, Payload = "{}", Attempts = attempts, ProcessedAt = processed, DeadLetteredAt = dead };
        var (stranded, retrying, done, already) = (E(10), E(9), E(10, processed: at), E(12, dead: marked));
        var ids = new[] { stranded.Id, retrying.Id, done.Id, already.Id };
        await f.DbAsync(async db =>
        {
            Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_OutboxDeadLetter"));
            db.Outbox.AddRange(stranded, retrying, done, already);
            await db.SaveChangesAsync();
            for (var run = 0; run < 2; run++) await db.Database.ExecuteSqlRawAsync(Hub.Api.Data.Migrations.OutboxDeadLetter.Backfill); // safe to re-run
            return 0;
        });
        try
        {
            var rows = await f.DbAsync(db => db.Outbox.AsNoTracking().Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id));
            Assert.NotNull(rows[stranded.Id].DeadLetteredAt);
            Assert.Null(rows[retrying.Id].DeadLetteredAt);
            Assert.Null(rows[done.Id].DeadLetteredAt);
            Assert.Equal(marked, rows[already.Id].DeadLetteredAt);
        }
        finally { await f.DbAsync(db => db.Outbox.Where(o => ids.Contains(o.Id)).ExecuteDeleteAsync()); }
    }
}

/// §25 (Rec): a create retried with the same Idempotency-Key returns the first response and makes nothing new.
[Collection("api")]
public sealed class IdempotencyTests(HubFactory f)
{
    readonly TestData d = new(f);

    [Fact]
    public async Task A_retried_create_with_the_same_key_is_replayed_not_repeated()
    {
        var p = await d.Project();
        var key = Guid.NewGuid().ToString("N");
        HttpRequestMessage Req(string path, object body)
        {
            var m = new HttpRequestMessage(HttpMethod.Post, path) { Content = System.Net.Http.Json.JsonContent.Create(body) };
            m.Headers.Add("Idempotency-Key", key);
            return m;
        }
        var body = new { name = "Retried over a flaky connection", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") };
        var first = await f.As(TestData.Marc).SendAsync(Req($"/api/v1/projects/{p.Id}/tasks", body));
        var second = await f.As(TestData.Marc).SendAsync(Req($"/api/v1/projects/{p.Id}/tasks", body));
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (first.StatusCode, second.StatusCode));
        Assert.Equal((await first.Json(201)).S("id"), (await second.Json(201)).S("id"));
        Assert.True(second.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, await f.DbAsync(db => db.Tasks.CountAsync(t => t.ProjectId == p.Id && t.Name == "Retried over a flaky connection")));

        var reused = await f.As(TestData.Marc).SendAsync(Req($"/api/v1/projects/{p.Id}/decisions", new { subject = "x" }));
        Assert.Equal(422, (int)reused.StatusCode); // the same key for another request is refused
        var otherPerson = await f.As(TestData.Pm).SendAsync(Req($"/api/v1/projects/{p.Id}/tasks", body)); // keys belong to one person
        Assert.NotEqual((await first.Json(201)).S("id"), (await otherPerson.Json(201)).S("id"));
    }

    static HttpRequestMessage Keyed(string key, string path, object body)
    {
        var m = new HttpRequestMessage(HttpMethod.Post, path) { Content = System.Net.Http.Json.JsonContent.Create(body) };
        m.Headers.Add("Idempotency-Key", key);
        return m;
    }

    Task<int> Receipts(string key) => f.DbAsync(db => db.Idempotency.CountAsync(r => r.Key == key));

    /// AC-OPS-13: two identical creates racing with one key make one task.
    [Fact]
    public async Task Racing_retries_with_one_key_create_one_task()
    {
        var p = await d.Project();
        var key = Guid.NewGuid().ToString("N");
        var body = new { name = "Sent twice at once", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") };
        var path = $"/api/v1/projects/{p.Id}/tasks";
        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => f.As(TestData.Marc).SendAsync(Keyed(key, path, body))));
        Assert.Equal(1, await f.DbAsync(db => db.Tasks.CountAsync(t => t.ProjectId == p.Id && t.Name == "Sent twice at once")));
        Assert.Equal(1, answers.Count(a => a.StatusCode == HttpStatusCode.Created && !a.Headers.Contains("Idempotent-Replayed")));
        foreach (var other in answers.Where(a => a.StatusCode != HttpStatusCode.Created || a.Headers.Contains("Idempotent-Replayed")))
            if (other.StatusCode == HttpStatusCode.Conflict)
            {
                Assert.Equal("idempotency_in_progress", (await other.Json(409)).S("code"));
                Assert.Equal("1", other.Headers.RetryAfter?.ToString());
            }
            else Assert.Equal(HttpStatusCode.Created, other.StatusCode);
        var later = await f.As(TestData.Marc).SendAsync(Keyed(key, path, body));
        Assert.Equal(HttpStatusCode.Created, later.StatusCode);
        Assert.True(later.Headers.Contains("Idempotent-Replayed"));
    }

    /// AC-OPS-14: a refused keyed request (400 or 422) leaves no receipt, and a corrected retry with the key runs.
    [Fact]
    public async Task A_refused_keyed_create_leaves_no_receipt()
    {
        var p = await d.Project();
        var key = Guid.NewGuid().ToString("N");
        var path = $"/api/v1/projects/{p.Id}/tasks";
        var invalid = await f.As(TestData.Marc).SendAsync(Keyed(key, path, new { name = "", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(0, await Receipts(key));
        var corrected = await f.As(TestData.Marc).SendAsync(Keyed(key, path, new { name = "Corrected after a refusal", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }));
        Assert.Equal(HttpStatusCode.Created, corrected.StatusCode);
        Assert.False(corrected.Headers.Contains("Idempotent-Replayed"));

        var t = await d.NewTask(p.Id);
        var ruleKey = Guid.NewGuid().ToString("N");
        var refused = await f.As(TestData.Marc).SendAsync(Keyed(ruleKey, $"/api/v1/tasks/{t.G("id")}/transition",
            new { toStatus = TaskStatuses.InProgress, rowVersion = await d.TaskVersion(t) })); // not Ready, no acknowledgement
        Assert.Equal("start_authorisation_required", (await refused.Json(422)).S("code"));
        Assert.Equal(0, await Receipts(ruleKey));
        var retried = await f.As(TestData.Marc).SendAsync(Keyed(ruleKey, $"/api/v1/tasks/{t.G("id")}/transition",
            new { toStatus = TaskStatuses.OnHold, rowVersion = await d.TaskVersion(t), reason = "Waiting for the survey" }));
        Assert.True(retried.IsSuccessStatusCode, await retried.Content.ReadAsStringAsync());
        Assert.Equal(1, await Receipts(ruleKey));
    }

    /// AC-OPS-14 and §48.11 D7: a live pending claim answers 409; an abandoned claim or an old receipt is replaced and the request runs.
    [Fact]
    public async Task An_abandoned_claim_or_old_receipt_is_replaced()
    {
        var p = await d.Project();
        var marc = d.User(TestData.Marc);
        var path = $"/api/v1/projects/{p.Id}/tasks";
        var now = f.Clock.GetUtcNow();
        object Body(string name) => new { name, projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") };

        var live = Guid.NewGuid().ToString("N");
        await f.DbAsync(db => IdempotencyMiddleware.Claim(db, marc, live, path, now.AddMinutes(-1)));
        var busy = await f.As(TestData.Marc).SendAsync(Keyed(live, path, Body("Waits for the first request")));
        Assert.Equal(HttpStatusCode.Conflict, busy.StatusCode);
        Assert.Equal("idempotency_in_progress", (await busy.Json(409)).S("code"));
        Assert.Equal("1", busy.Headers.RetryAfter?.ToString());
        var reusedWhilePending = await f.As(TestData.Marc).SendAsync(Keyed(live, $"/api/v1/projects/{p.Id}/decisions", new { subject = "x" }));
        Assert.Equal(422, (int)reusedWhilePending.StatusCode);

        var abandoned = Guid.NewGuid().ToString("N");
        await f.DbAsync(db => IdempotencyMiddleware.Claim(db, marc, abandoned, path, now.AddMinutes(-3)));
        var runs = await f.As(TestData.Marc).SendAsync(Keyed(abandoned, path, Body("Runs after an abandoned claim")));
        Assert.Equal(HttpStatusCode.Created, runs.StatusCode);
        Assert.False(runs.Headers.Contains("Idempotent-Replayed"));

        var old = Guid.NewGuid().ToString("N");
        await f.DbAsync(async db =>
        {
            db.Idempotency.Add(new IdempotencyRecord { UserId = marc, Key = old, Path = "/api/v1/elsewhere", Status = 201, Body = [], CreatedAt = now.AddDays(-1).AddMinutes(-1) });
            return await db.SaveChangesAsync();
        });
        var fresh = await f.As(TestData.Marc).SendAsync(Keyed(old, path, Body("Runs after an old receipt")));
        Assert.Equal(HttpStatusCode.Created, fresh.StatusCode);
        Assert.False(fresh.Headers.Contains("Idempotent-Replayed"));
        Assert.Equal(1, await Receipts(old));
        var replay = await f.As(TestData.Marc).SendAsync(Keyed(old, path, Body("Runs after an old receipt")));
        Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
    }

    /// FR-OPS-14: the previous image's lookup (receipts younger than one day) never returns a pending claim after a rollback.
    [Fact]
    public async Task A_pending_claim_is_invisible_to_a_one_day_receipt_lookup()
    {
        var marc = d.User(TestData.Marc);
        var key = Guid.NewGuid().ToString("N");
        var now = f.Clock.GetUtcNow();
        Assert.NotNull(await f.DbAsync(db => IdempotencyMiddleware.Claim(db, marc, key, "/api/v1/anything", now)));
        Assert.Null(await f.DbAsync(db => IdempotencyMiddleware.Claim(db, marc, key, "/api/v1/anything", now)));
        Assert.Equal(1, await Receipts(key));
        var previousImage = await f.DbAsync(db => db.Idempotency.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == marc && x.Key == key && x.CreatedAt > now.AddDays(-1)));
        Assert.Null(previousImage);
    }
}

/// Packet 011 threat model finding T-05: exported CSV text never runs as a spreadsheet formula.
public sealed class ExportSafetyTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://x\",\"go\")", "\"'=HYPERLINK(\"\"http://x\"\",\"\"go\"\")\"")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("-5", "-5")]
    [InlineData("2.5", "2.5")]
    [InlineData("Pier footing, east", "\"Pier footing, east\"")]
    // The same rules as the client's csvCell (web/src/lib/csv.ts): every separator a spreadsheet may split on is quoted,
    // so a semicolon-separated locale cannot turn the text after one into its own (formula) cell.
    [InlineData("Grade;=SUM(1;2)", "\"Grade;=SUM(1;2)\"")]
    [InlineData("Grade\t=SUM(1)", "\"Grade\t=SUM(1)\"")]
    [InlineData("\t=SUM(1)", "\"'\t=SUM(1)\"")]
    [InlineData("  =SUM(1)", "'  =SUM(1)")] // a formula behind leading spaces
    [InlineData("-1e5", "-1e5")]
    public void Formula_like_text_is_neutralised_and_numbers_are_kept(string value, string csv) => Assert.Equal(csv, Export.Csv(value));
}
