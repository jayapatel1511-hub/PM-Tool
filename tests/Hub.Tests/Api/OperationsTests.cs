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
    public void Formula_like_text_is_neutralised_and_numbers_are_kept(string value, string csv) => Assert.Equal(csv, Export.Csv(value));
}
