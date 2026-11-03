using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Hub.Tests.Api;

public sealed class WorkSeenMigrationTests
{
    sealed record Expected(string ItemType, Guid ItemId, string Role, Guid RecipientId, int SourceVersion);

    static async Task<string> CanonicalFingerprint(HubFactory f)
    {
        // Fixed maintained table allowlist; compare full source rows, not just the fields the seed happens to read.
        string[] tables = ["task", "deliverable", "decision", "meeting_action", "risk", "issue", "handoff", "change_notice", "project", "activity_log"];
        var fields = tables.Select(table => $"'{table}', (SELECT jsonb_agg(to_jsonb(t) ORDER BY id) FROM hub.{table} t)");
        return await f.DbAsync(db => db.Database.SqlQueryRaw<string>($"SELECT jsonb_build_object({string.Join(",", fields)})::text AS \"Value\"").SingleAsync());
    }

    [Fact]
    public async Task Previous_schema_upgrade_seeds_truthful_all_role_history_without_canonical_or_private_writes()
    {
        using var s = new WorkSeenTestContext(); var f = s.Factory; var d = s.Data;
        f.Clock.SetDate(2026, 11, 2);
        var (project, cases) = await WorkScreenReadEnvelopeTests.NineDetails(s);
        var hidden = await d.Project();
        var hiddenTask = await d.NewTask(hidden.Id, TestData.Pm,
            new { assigneeId = d.User(TestData.Alex), reviewerId = d.User(TestData.Marc) });
        var taskId = cases.Single(item => item.Kind == "Task").Id;
        var hiddenId = hiddenTask.G("id");
        var alex = d.User(TestData.Alex);
        await s.Enable();
        var migration = f.Db(db => db.Database.GetMigrations().Single(name => name.EndsWith("_PrivateWorkSeenLedger", StringComparison.Ordinal)));
        await f.DbAsync(async db => {
            var all = db.Database.GetMigrations().ToArray();
            var at = Array.IndexOf(all, migration); Assert.True(at > 0);
            await db.GetService<IMigrator>().MigrateAsync(all[at - 1]);
            return 0;
        });
        // Legacy fixture changes happen against the PREVIOUS schema, with no assignment ledger available.
        await f.DbAsync(async db => {
            await db.Database.ExecuteSqlAsync($"UPDATE hub.task SET row_version=0, status='Complete', progress_pct=100, completed_at={f.Clock.Now}, deleted_at={f.Clock.Now} WHERE id={hiddenId}");
            return await db.Database.ExecuteSqlAsync($"UPDATE hub.project SET status='Archived', visibility='Restricted' WHERE id={hidden.Id}");
        });
        var expected = await f.DbAsync(async db => {
            var canonical = new List<object>();
            canonical.AddRange(await db.Tasks.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Deliverables.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Decisions.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Actions.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Risks.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Issues.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Handoffs.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.ChangeNotices.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            canonical.AddRange(await db.Projects.IgnoreQueryFilters().AsNoTracking().ToListAsync());
            return canonical.Select(row => WorkAssignmentPublication.Bound(db, row)).SelectMany(bound => bound.Roles
                .Select(role => new Expected(bound.Key.ItemType, bound.Key.ItemId, role.Role, role.RecipientId, bound.SourceVersion))).ToHashSet();
        });
        Assert.Equal(12, expected.Select(row => (row.ItemType, row.Role)).Distinct().Count());
        Assert.Contains(expected, row => row.ItemId == hiddenId && row.SourceVersion == 0 && row.RecipientId == alex);
        var before = await CanonicalFingerprint(f);
        await f.DbAsync(async db => { await db.GetService<IMigrator>().MigrateAsync(migration); return 0; });
        Assert.Equal(before, await CanonicalFingerprint(f));
        var seeded = f.Db(db => db.WorkAssignmentOccurrences.AsNoTracking().ToArray());
        Assert.True(expected.SetEquals(seeded.Select(row => new Expected(row.ItemType, row.ItemId, row.Role, row.RecipientId, row.SourceVersion))));
        Assert.All(seeded, row => {
            Assert.Equal("BaselineSeed", row.Provenance); Assert.Null(row.SourceActivityId); Assert.Null(row.SourceOccurredAt);
            Assert.NotEqual(default, row.RecordedAt); Assert.NotEqual(Guid.Empty, row.Token);
        });
        Assert.Equal(0, f.Db(db => db.WorkSeenAcknowledgements.Count()));
        Assert.Equal(0, f.Db(db => db.WorkSeenCommandReceipts.Count()));
        Assert.False(f.Db(db => db.UserSettings.Any(row => row.MyWorkFirstVisitAt != null || row.MyWorkSeenRevision != 0)));
        var baseline = seeded.Single(row => row.ItemId == taskId && row.Role == "Assignee").Token;
        await s.Visit();
        Assert.Equal(before, await CanonicalFingerprint(f));
        var acknowledged = f.Db(db => db.WorkSeenAcknowledgements.Where(row => row.RecipientId == alex).Select(row => row.Token).ToHashSet());
        Assert.True(acknowledged.SetEquals(seeded.Where(row => row.RecipientId == alex).Select(row => row.Token)));
        Assert.Contains(seeded.Single(row => row.ItemId == hiddenId && row.RecipientId == alex).Token, acknowledged);
        await s.Assign(taskId, d.User(TestData.Jill)); await s.Assign(taskId, alex);
        var latest = s.Latest(taskId);
        var published = f.Db(db => db.WorkAssignmentOccurrences.Single(row => row.Token == latest));
        Assert.NotEqual(baseline, published.Token); Assert.Equal("Published", published.Provenance);
        Assert.NotNull(published.SourceActivityId); Assert.NotNull(published.SourceOccurredAt);
        Assert.DoesNotContain(published.Token, acknowledged);
    }
}
