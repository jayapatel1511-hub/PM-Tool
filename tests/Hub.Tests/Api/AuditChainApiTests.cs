using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Hub.Api.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class AuditChainApiTests
{
    static async Task Enable(HubFactory f)
    {
        await f.DbAsync(async db => { await AuditChainInitialization.Run(db, f.Clock.Now); return 0; });
        f.Services.GetRequiredService<SettingsStore>().Invalidate();
    }

    [Fact]
    [Trait("flag-on", "verifiable_log")]
    public async Task Legacy_ten_thousand_rows_are_sealed_once_and_saved_records_survive_append_then_detect_tampering()
    {
        using var f = new HubFactory(); var d = new TestData(f); var project = await d.Project();
        await f.DbAsync(async db =>
        {
            db.ActivityLog.AddRange(Enumerable.Range(0, 10_000).Select(i => new ActivityLog { ProjectId = project.Id,
                OccurredAt = f.Clock.Now.AddSeconds(-1).AddTicks(i * 10), ActorType = "System", ItemType = "Task", Action = "Legacy", Reason = "Original", Changes = "[]" }));
            return await db.SaveChangesAsync();
        });
        await Enable(f); await d.NewTask(project.Id);
        var admin = f.As(TestData.Admin);
        var verified = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{project.Id}/verify")).Json();
        Assert.True(verified["valid"]!.GetValue<bool>()); Assert.True(verified["total"]!.GetValue<long>() >= 10_000);
        var marker = await f.DbAsync(db => db.ActivityLog.Where(a => a.ProjectId == project.Id && a.Action == "Chain started").SingleAsync());
        Assert.True(JsonDocument.Parse(marker.Changes).RootElement[0].GetProperty("new").GetInt32() >= 10_000);
        await (await admin.GetAsync($"/api/v1/projects/{project.Id}/activity")).Json();
        var record = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{project.Id}/record?format=json")).Json();
        await d.NewTask(project.Id);
        var anchored = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{project.Id}/verify?headSeq={record["headSeq"]}&headHash={record["headHash"]}")).Json();
        Assert.True(anchored["valid"]!.GetValue<bool>());
        var target = await f.DbAsync(db => db.ActivityLog.Where(a => a.ProjectId == project.Id && a.Action == "Legacy").OrderBy(a => a.ChainSeq).FirstAsync());
        await f.DbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE hub.activity_log DISABLE TRIGGER activity_log_no_update");
            try { await db.Database.ExecuteSqlAsync($"UPDATE hub.activity_log SET reason={"Tampered by test operator"} WHERE id={target.Id}"); }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE hub.activity_log ENABLE TRIGGER activity_log_no_update"); }
            return 0;
        });
        var failed = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{project.Id}/verify")).Json();
        Assert.False(failed["valid"]!.GetValue<bool>()); Assert.Equal(target.ChainSeq, failed["firstBrokenSeq"]!.GetValue<long>());
        var mails = await f.DbAsync(db => db.Emails.CountAsync(e => e.DedupKey != null && e.DedupKey.StartsWith($"audit-chain:{project.Id}:")));
        await (await admin.GetAsync($"/api/v1/admin/audit-chains/{project.Id}/verify")).Json();
        Assert.Equal(mails, await f.DbAsync(db => db.Emails.CountAsync(e => e.DedupKey != null && e.DedupKey.StartsWith($"audit-chain:{project.Id}:"))));
        Assert.True(mails > 0);
        Assert.Equal(1, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == project.Id && a.Action == "Chain started")));
    }

    [Fact]
    public async Task Fifty_parallel_edits_have_gap_free_sequences_and_failed_hash_rolls_back_the_change()
    {
        using var f = new HubFactory(); var d = new TestData(f); var project = await d.Project();
        var tasks = new List<(Guid Id, int Version)>();
        for (var i = 0; i < 50; i++) { var task = await d.NewTask(project.Id); tasks.Add((task.G("id"), await d.TaskVersion(task))); }
        await Enable(f);
        await Task.WhenAll(tasks.Select(async (t, i) => await (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{t.Id}", new { name = "Parallel " + i }, t.Version)).Json()));
        var rows = await f.DbAsync(db => db.ActivityLog.AsNoTracking().Where(a => a.ChainId == project.Id).OrderBy(a => a.ChainSeq).ToListAsync());
        Assert.Equal(Enumerable.Range(1, rows.Count).Select(i => (long?)i), rows.Select(r => r.ChainSeq));
        Assert.Equal(AuditChain.ZeroHash, rows[0].PrevHash);
        for (var i = 1; i < rows.Count; i++) Assert.Equal(rows[i - 1].RowHash, rows[i].PrevHash);
        await Assert.ThrowsAnyAsync<JsonException>(() => f.DbAsync(async db =>
        {
            var task = await db.Tasks.SingleAsync(t => t.Id == tasks[0].Id); task.Name = "Must roll back";
            db.ActivityLog.Add(new ActivityLog { ProjectId = project.Id, OccurredAt = f.Clock.Now, Changes = "{" });
            return await db.SaveChangesAsync();
        }));
        Assert.NotEqual("Must roll back", await f.DbAsync(db => db.Tasks.Where(t => t.Id == tasks[0].Id).Select(t => t.Name).SingleAsync()));
        var rowId = rows[0].Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db => { var row = await db.ActivityLog.SingleAsync(r => r.Id == rowId); row.Reason = "Forbidden"; return await db.SaveChangesAsync(); }));
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/admin/audit-chains")).StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.verifiable_log", new { value = false })).StatusCode);
    }

    [Fact]
    public async Task Partial_checks_cannot_promote_an_unverified_prefix()
    {
        using var f = new HubFactory(); var d = new TestData(f); var p = await d.Project();
        await Enable(f); await d.NewTask(p.Id);
        var admin = f.As(TestData.Admin);
        var head = await f.DbAsync(db => db.Set<ActivityChainHead>().AsNoTracking().SingleAsync(h => h.ChainId == p.Id));
        Assert.Equal(0, head.LastVerifiedSeq);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify?from={head.HeadSeq + 1}")).StatusCode);
        await (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify?from={head.HeadSeq}")).Json();
        var partial = await f.DbAsync(db => db.Set<ActivityChainHead>().AsNoTracking().SingleAsync(h => h.ChainId == p.Id));
        Assert.Equal(0, partial.LastVerifiedSeq); Assert.Null(partial.LastVerifiedAt);
        await (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify?headSeq=1")).Json();
        var prefixIntegrity = await (await admin.GetAsync($"/api/v1/projects/{p.Id}/activity/integrity")).Json();
        Assert.Null(prefixIntegrity["valid"]); Assert.Equal(1, prefixIntegrity["verifiedEntries"]!.GetValue<long>());
        var report = await (await admin.GetAsync($"/api/v1/reports/project-activity-log?projectId={p.Id}")).Json();
        Assert.Contains(report["parameters"]!.AsArray(), x => x!["label"]!.GetValue<string>().StartsWith("Log integrity") && x["value"]!.GetValue<string>() == "Log awaiting verification");
        await (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify")).Json();
        await d.NewTask(p.Id);
        var verified = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify?from={head.HeadSeq + 1}")).Json();
        Assert.True(verified["valid"]!.GetValue<bool>());
        var after = await f.DbAsync(db => db.Set<ActivityChainHead>().AsNoTracking().SingleAsync(h => h.ChainId == p.Id));
        Assert.Equal(after.HeadSeq, after.LastVerifiedSeq);
        await d.NewTask(p.Id);
        await f.DbAsync(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE hub.activity_log DISABLE TRIGGER activity_log_no_update");
            try { await db.Database.ExecuteSqlAsync($"UPDATE hub.activity_log SET row_hash={new string('f', 64)} WHERE chain_id={p.Id} AND chain_seq={after.LastVerifiedSeq}"); }
            finally { await db.Database.ExecuteSqlRawAsync("ALTER TABLE hub.activity_log ENABLE TRIGGER activity_log_no_update"); }
            return 0;
        });
        var broken = await (await admin.GetAsync($"/api/v1/admin/audit-chains/{p.Id}/verify?from={after.LastVerifiedSeq + 1}")).Json();
        Assert.False(broken["valid"]!.GetValue<bool>()); Assert.Equal(after.LastVerifiedSeq, broken["firstBrokenSeq"]!.GetValue<long>());
    }

    [Fact]
    public async Task Runtime_insert_select_role_appends_but_cannot_initialize_or_update_seals()
    {
        using var f = new HubFactory(); var d = new TestData(f); var p = await d.Project(); var task = await d.NewTask(p.Id);
        var refused = await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.verifiable_log", new { value = true });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, refused.StatusCode);
        await Enable(f);
        await f.DbAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var role = "audit_test_" + Guid.NewGuid().ToString("N");
            await db.Database.ExecuteSqlRawAsync($"CREATE ROLE {role} NOLOGIN; GRANT USAGE ON SCHEMA hub TO {role}; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA hub TO {role}; REVOKE UPDATE, DELETE ON hub.activity_log FROM {role}; SET LOCAL ROLE {role}");
            await Assert.ThrowsAsync<InvalidOperationException>(() => AuditChainInitialization.Run(db, f.Clock.Now));
            var row = await db.Tasks.SingleAsync(t => t.Id == task.G("id")); row.Name = "Runtime append";
            await db.SaveChangesAsync();
            Assert.True(await db.ActivityLog.AnyAsync(a => a.ChainId == p.Id && a.Action == "Updated"));
            var update = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE hub.activity_log SET chain_seq = chain_seq WHERE false"));
            Assert.Equal("42501", update.SqlState);
            await tx.RollbackAsync();
            return 0;
        });
    }
}
