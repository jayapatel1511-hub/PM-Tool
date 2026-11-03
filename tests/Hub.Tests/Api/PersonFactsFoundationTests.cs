using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Tests.Api;

/// S29 root-owned schema/vocabulary/settings boundaries; behavioural facts cases live in CapacityPersonFactsTests.
public sealed class PersonFactsFoundationTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public PersonFactsFoundationTests() => data = new(f);
    public void Dispose() => f.Dispose();
    Task Set(string key, object value) => f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value }).Result.Json();

    [Fact]
    public async Task Existing_people_have_unknown_nullable_facts_without_defaults_or_seeded_certifications()
    {
        var before = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/users?inactive=true")).Json();
        Assert.All(before.AsArray(), person => Assert.False(person!.AsObject().ContainsKey("personFacts")));
        var columns = await f.DbAsync(db => db.Database.SqlQueryRaw<string>("""
            SELECT column_name || ':' || is_nullable || ':' || coalesce(column_default, '<none>') AS "Value"
            FROM information_schema.columns WHERE table_schema='hub' AND table_name='app_user'
              AND column_name IN ('person_type','person_start_date','person_end_date') ORDER BY column_name
            """).ToListAsync());
        Assert.Equal(new[] { "person_end_date:YES:<none>", "person_start_date:YES:<none>", "person_type:YES:<none>" }, columns);
        Assert.False(await f.DbAsync(db => db.Users.AnyAsync(u => u.PersonType != null || u.PersonStartDate != null || u.PersonEndDate != null)));
        Assert.Equal(0, await f.DbAsync(db => db.CertificationTags.CountAsync()));
        Assert.Equal(0, await f.DbAsync(db => db.UserCertifications.CountAsync()));
    }

    [Fact]
    public async Task Database_rejects_invalid_person_facts_case_duplicate_tags_and_duplicate_or_foreign_certifications()
    {
        var alex = data.User(TestData.Alex);
        var type = await Assert.ThrowsAsync<PostgresException>(() => f.DbAsync(db => db.Database.ExecuteSqlAsync($"UPDATE hub.app_user SET person_type='Invented' WHERE id={alex}")));
        Assert.Equal("ck_app_user_person_type", type.ConstraintName);
        var dates = await Assert.ThrowsAsync<PostgresException>(() => f.DbAsync(db => db.Database.ExecuteSqlAsync($"UPDATE hub.app_user SET person_start_date='2026-12-02',person_end_date='2026-12-01' WHERE id={alex}")));
        Assert.Equal("ck_app_user_person_dates", dates.ConstraintName);
        // Explicit low-level schema tests: normal assignment and correction workflows are tested separately.
        var tag = new CertificationTag { Name = "Synthetic safety tag" };
        await f.DbAsync(async db => { db.CertificationTags.Add(tag); return await db.SaveChangesAsync(); });
        var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.CertificationTags.Add(new CertificationTag { Name = "SYNTHETIC SAFETY TAG" }); return await db.SaveChangesAsync(); }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.InnerException).SqlState);
        var invalidName = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.CertificationTags.Add(new CertificationTag { Name = " " }); return await db.SaveChangesAsync(); }));
        Assert.Equal("ck_certification_tag_name", Assert.IsType<PostgresException>(invalidName.InnerException).ConstraintName);
        await f.DbAsync(async db => { db.UserCertifications.Add(new UserCertification { UserId = alex, CertificationTagId = tag.Id }); return await db.SaveChangesAsync(); });
        var twice = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.UserCertifications.Add(new UserCertification { UserId = alex, CertificationTagId = tag.Id }); return await db.SaveChangesAsync(); }));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(twice.InnerException).SqlState);
        var foreign = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.UserCertifications.Add(new UserCertification { UserId = Guid.NewGuid(), CertificationTagId = tag.Id }); return await db.SaveChangesAsync(); }));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(foreign.InnerException).SqlState);
        var deleteTag = await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db => { db.CertificationTags.Remove(await db.CertificationTags.SingleAsync(t => t.Id == tag.Id)); return await db.SaveChangesAsync(); }));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(deleteTag.InnerException).SqlState);
        Assert.Null(await f.DbAsync(db => db.Users.Where(u => u.Id == alex).Select(u => u.PersonType).SingleAsync()));
        Assert.Equal(1, await f.DbAsync(db => db.UserCertifications.CountAsync()));
    }

    [Fact]
    public async Task Vocabulary_and_warning_settings_are_dark_without_usage_counts_and_keep_stored_values_when_disabled()
    {
        var keys = new[] { "certification_warn_days", "availability_window_days" };
        async Task<JsonNode> Settings() => await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json();
        foreach (var key in keys)
        {
            Assert.DoesNotContain((await Settings()).AsArray(), row => row!.S("key") == key);
            Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value = 20 })).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).GetAsync("/api/v1/admin/certificationTags")).StatusCode);
        await Set("flag.people_screens", true);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).GetAsync("/api/v1/admin/certificationTags")).StatusCode);
        await Set("flag.capacity_screens", true);
        var settings = (await Settings()).AsArray();
        Assert.Equal(30, settings.Single(row => row!.S("key") == keys[0])!.I("value"));
        Assert.Equal(60, settings.Single(row => row!.S("key") == keys[1])!.I("value"));
        await Set(keys[0], 0); await Set(keys[1], 60);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{keys[0]}", new { value = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{keys[0]}", new { value = 1.5 })).StatusCode);
        var receipt = Guid.NewGuid().ToString("N");
        Task<HttpResponseMessage> CreateTag()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/certificationTags")
            { Content = System.Net.Http.Json.JsonContent.Create(new { name = "Synthetic configured tag" }) };
            request.Headers.Add("Idempotency-Key", receipt);
            return f.As(TestData.Admin).SendAsync(request);
        }
        var tag = await (await CreateTag()).Json(201);
        Assert.Equal(tag.G("id"), (await (await CreateTag()).Json(201)).G("id"));
        Assert.False(tag.AsObject().ContainsKey("usage")); Assert.False(tag.AsObject().ContainsKey("people"));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).GetAsync($"/api/v1/admin/certificationTags/{tag.G("id")}/usage")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/admin/certificationTags", new { name = "Denied tag" })).StatusCode);
        await Set("flag.capacity_screens", false);
        Assert.Equal(HttpStatusCode.NotFound, (await CreateTag()).StatusCode);
        Assert.DoesNotContain((await Settings()).AsArray(), row => keys.Contains(row!.S("key")));
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{keys[0]}", new { value = 30 })).StatusCode);
        Assert.Equal(0, await f.DbAsync(db => db.Settings.Where(s => s.Key == keys[0]).Select(s => int.Parse(s.Value)).SingleAsync()));
        Assert.Equal(1, await f.DbAsync(db => db.CertificationTags.CountAsync()));
        Assert.Equal(1, await f.DbAsync(db => db.Idempotency.CountAsync(row => row.Key == receipt)));
    }
}
