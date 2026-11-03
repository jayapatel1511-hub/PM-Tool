using System.Net;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

public sealed class IssueRootCauseVocabularyTests
{
    [Fact] // AC-SCR-71 / AC-SCR-84: organisation-managed catalogue starts empty; proposed D14 names are not policy seeds.
    public async Task Catalogue_has_no_seed_and_admin_manages_versioned_audited_values_without_deletion()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true);
        Assert.Empty((await (await t.F.As(TestData.Admin).GetAsync("/api/v1/admin/issueRootCauses")).Json()).AsArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await t.F.As(TestData.Alex).GetAsync("/api/v1/admin/issueRootCauses")).StatusCode);
        var cause = await t.Cause(); var version = t.F.Db(db => db.IssueRootCauseCategories.Single(row => row.Id == cause).RowVersion);
        await (await t.F.As(TestData.Admin).Patch($"/api/v1/admin/issueRootCauses/{cause}", new { name = "Changed cause" }, version + 1)).Json(409);
        await t.EditCause(cause, new { name = "Coordination revised", isActive = false });
        var choices = (await (await t.F.As(TestData.Pm).GetAsync("/api/v1/reference")).Json())["issueRootCauses"]!.AsArray();
        Assert.Equal(cause, Assert.Single(choices)!.G("id")); Assert.False(choices[0]!["isActive"]!.GetValue<bool>());
        Assert.True(t.F.Db(db => db.ActivityLog.Any(row => row.ItemId == cause)));
        Assert.False((await t.F.As(TestData.Admin).DeleteAsync($"/api/v1/admin/issueRootCauses/{cause}")).IsSuccessStatusCode);
        await t.Flag(false);
        Assert.False((await (await t.F.As(TestData.Pm).GetAsync("/api/v1/reference")).Json()).AsObject().ContainsKey("issueRootCauses"));
        Assert.Equal(HttpStatusCode.NotFound, (await t.F.As(TestData.Admin).GetAsync("/api/v1/admin/issueRootCauses")).StatusCode);
    }

    [Theory] // AC-SCR-71: real organisation validation, no arbitrary per-issue cause string.
    [InlineData("")][InlineData("   ")][InlineData("bad\nname")]
    public async Task Invalid_names_and_case_insensitive_duplicates_are_refused(string invalid)
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); await t.Cause();
        await t.Post(TestData.Admin, "/api/v1/admin/issueRootCauses", new { name = invalid }, 400);
        await t.Post(TestData.Admin, "/api/v1/admin/issueRootCauses", new { name = new string('x', 121) }, 400);
        await t.Post(TestData.Admin, "/api/v1/admin/issueRootCauses", new { name = "coordination GAP" }, 409);
        Assert.Equal(1, t.F.Db(db => db.IssueRootCauseCategories.Count()));
    }

    [Fact] // AC-SCR-71 / AC-SCR-84: nullable legacy compatibility and complete recorded pair are enforced by the provider.
    public async Task Provider_allows_unknown_legacy_resolution_but_rejects_half_recorded_pair_and_bad_fk()
    {
        using var t = new IssueRootCauseFixture(); var p = await t.Data.Project(); var issue = await t.Issue(p.Id); await t.Move(issue);
        Assert.Null(t.F.Db(db => db.Issues.Single(row => row.Id == issue).RootCauseCategoryId));
        await t.Flag(true); var cause = await t.Cause(); var before = t.State(p.Id);
        await t.F.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hub.issue SET root_cause_category_id={cause} WHERE id={issue}"));
            Assert.Equal(PostgresErrorCodes.CheckViolation, error.SqlState); await transaction.RollbackAsync(); return 0;
        });
        await t.F.DbAsync(async db => {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE hub.issue SET root_cause_category_id={Guid.NewGuid()}, root_cause_label_snapshot={'X'.ToString()} WHERE id={issue}"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, error.SqlState); await transaction.RollbackAsync(); return 0;
        });
        Assert.Equal(before, t.State(p.Id));
    }

    [Fact] // AC-SCR-84: vocabulary creates recheck flag and Admin before generic cached replies.
    public async Task Cached_catalogue_create_is_hidden_when_feature_is_disabled()
    {
        using var t = new IssueRootCauseFixture(); await t.Flag(true); var key = Guid.NewGuid().ToString(); var client = t.F.As(TestData.Admin);
        client.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var first = await (await client.Post("/api/v1/admin/issueRootCauses", new { name = "Explicit fixture category" })).Json(201);
        var again = await (await client.Post("/api/v1/admin/issueRootCauses", new { name = "Explicit fixture category" })).Json(201); Assert.Equal(first.G("id"), again.G("id"));
        await t.Flag(false); await (await client.Post("/api/v1/admin/issueRootCauses", new { name = "Explicit fixture category" })).Json(404);
        Assert.Equal(1, t.F.Db(db => db.IssueRootCauseCategories.Count()));
    }
}
