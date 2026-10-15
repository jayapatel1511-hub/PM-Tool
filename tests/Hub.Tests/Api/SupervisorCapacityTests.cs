using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

/// Packet 038, AC-DIG-01–06: use the real planner day calculation and PostgreSQL preferences.
[Collection("api")]
public sealed class SupervisorCapacityTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly Monday = new(2026, 10, 19);

    async Task<(Guid Id, string Email, Guid Office)> Person()
    {
        var email = $"capacity-{Guid.NewGuid():N}@hub.test";
        var id = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id");
        var office = await f.DbAsync(async db =>
        {
            var o = new Office { Name = "Synthetic capacity check", Code = Guid.NewGuid().ToString("N") };
            db.Offices.Add(o);
            var user = await db.Users.SingleAsync(u => u.Id == id);
            user.OfficeId = o.Id; user.WeeklyCapacityHours = 40;
            await db.SaveChangesAsync(); return o.Id;
        });
        return (id, email, office);
    }

    async Task<JsonNode> Preferences(string email) => (await (await f.As(email).GetAsync("/api/v1/me/preferences")).Json())["digest"]!;

    [Fact]
    public async Task Concurrent_first_daily_and_weekly_preferences_keep_one_row_and_both_markers()
    {
        var p = await Person(); var now = f.Clock.Now;
        async Task Write(bool daily)
        {
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HubDb>();
            var pref = await UserPreferences.GetOrCreate(db, p.Id);
            if (daily) pref.LastDigestAt = now; else pref.LastWeeklySummaryAt = now;
            await db.SaveChangesAsync();
        }
        await Task.WhenAll(Write(true), Write(false));
        var row = f.Db(db => db.UserSettings.Single(s => s.UserId == p.Id));
        Assert.Equal(now, row.LastDigestAt); Assert.Equal(now, row.LastWeeklySummaryAt);
        Assert.True(row.HoldDigestWhenAway);
    }

    [Fact]
    public async Task Invalid_first_preference_save_does_not_create_a_record()
    {
        var p = await Person();
        var response = await f.As(p.Email).Put("/api/v1/me/preferences/digest", new { enabled = true, sectionsOff = new[] { "unknown" } });
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(f.Db(db => db.UserSettings.Any(s => s.UserId == p.Id)));
    }

    [Fact]
    public async Task Preference_defaults_on_opt_out_persists_and_omitted_field_preserves_the_choice()
    {
        var p = await Person(); var other = await Person();
        Assert.True((await Preferences(p.Email))["holdDigestWhenAway"]!.GetValue<bool>());
        (await f.As(p.Email).Put("/api/v1/me/preferences/digest", new { enabled = true, holdDigestWhenAway = false })).EnsureSuccessStatusCode();
        Assert.False(f.Db(db => db.UserSettings.Single(s => s.UserId == p.Id).HoldDigestWhenAway));
        (await f.As(p.Email).Put("/api/v1/me/preferences/digest", new { enabled = true, time = "08:30" })).EnsureSuccessStatusCode();
        Assert.False((await Preferences(p.Email))["holdDigestWhenAway"]!.GetValue<bool>());
        Assert.True((await Preferences(other.Email))["holdDigestWhenAway"]!.GetValue<bool>());
    }

    [Theory]
    [InlineData("Unavailable", true)]
    [InlineData("Reduced", false)]
    [InlineData("ApprovedFullDay", true)]
    [InlineData("ApprovedPartialDay", false)]
    [InlineData("Holiday", true)]
    [InlineData("NonWorkingDay", false)]
    [InlineData("CancelledLeave", false)]
    public async Task Hold_matches_the_planner_day_and_keeps_immediate_notices(string cause, bool held)
    {
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = new(2026, 10, 19, 13, 0, 0, TimeSpan.Zero);
            var p = await Person();
            var project = await d.Project();
            await d.NewTask(project.Id, extra: new { assigneeId = p.Id, dueDate = "2026-10-15" });
            await f.Evaluate(project.Id);
            await f.DbAsync(async db =>
            {
                if (cause is "Unavailable" or "Reduced") db.AvailabilityOverrides.Add(new() { PersonId = p.Id, WorkDate = Monday,
                    AvailableHours = cause == "Reduced" ? 4 : 0, Category = cause, RowVersion = 1 });
                if (cause.StartsWith("Approved", StringComparison.Ordinal) || cause == "CancelledLeave") db.ApprovedLeaves.Add(new() { PersonId = p.Id,
                    SourceSystem = "Synthetic HR", ExternalId = $"private-reference-{p.Id}", FromDate = Monday, ThroughDate = Monday,
                    HoursPerDay = cause == "ApprovedPartialDay" ? 4 : null, Status = cause == "CancelledLeave" ? "Cancelled" : "Approved" });
                if (cause == "Holiday") db.Holidays.Add(new() { OfficeId = p.Office, Date = Monday, Name = "Synthetic office holiday" });
                if (cause == "NonWorkingDay") db.PersonSchedules.Add(new() { PersonId = p.Id, FromDate = Monday, WeeklyHours = 32, WorkingDays = [2, 3, 4, 5] });
                return await db.SaveChangesAsync();
            });
            var notices = f.Db(db => db.Notifications.Count(n => n.UserId == p.Id));
            var activity = f.Db(db => db.ActivityLog.Count(a => a.ItemId == p.Id));
            await f.RunJob<DigestJob>();
            Assert.Equal(held ? 0 : 1, f.Db(db => db.Emails.Count(e => e.UserId == p.Id && e.Kind == "Digest")));
            Assert.Equal(notices, f.Db(db => db.Notifications.Count(n => n.UserId == p.Id)));
            Assert.Equal(activity, f.Db(db => db.ActivityLog.Count(a => a.ItemId == p.Id)));
            Assert.Equal(!held, f.Db(db => db.UserSettings.Any(s => s.UserId == p.Id && s.LastDigestAt != null)));
            if (held)
            {
                var details = f.Db(db => db.JobRuns.Where(r => r.JobName == "daily-digest").OrderByDescending(r => r.StartedAt).First().Details);
                Assert.True(JsonNode.Parse(details!)!["held"]!.GetValue<int>() > 0);
                using var scope = f.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<HubDb>();
                var before = await OpsChecks.Gather(db, new OrgSettings(), f.Clock.Now, saved);
                (await f.As(p.Email).Put("/api/v1/me/preferences/digest", new { enabled = true, holdDigestWhenAway = false })).EnsureSuccessStatusCode();
                var after = await OpsChecks.Gather(db, new OrgSettings(), f.Clock.Now, saved);
                Assert.Equal(before.DigestsNotBuilt + 1, after.DigestsNotBuilt);
                await f.RunJob<DigestJob>();
                Assert.Equal(1, f.Db(x => x.Emails.Count(e => e.UserId == p.Id && e.Kind == "Digest")));
            }
            Assert.DoesNotContain("private-reference", string.Join("\n", f.Db(db => db.Emails.Where(e => e.UserId == p.Id).Select(e => e.BodyText).ToArray())));
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Five_held_days_preserve_the_last_sent_window_and_the_return_digest_caps()
    {
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = new(2026, 10, 16, 13, 0, 0, TimeSpan.Zero);
            var p = await Person(); var project = await d.Project();
            await d.NewTask(project.Id, extra: new { assigneeId = p.Id, dueDate = "2026-10-15" });
            (await f.As(p.Email).Put($"/api/v1/projects/{project.Id}/follow", new { level = FollowLevel.AllActivity })).EnsureSuccessStatusCode();
            await f.Evaluate(project.Id); await f.RunJob<DigestJob>();
            var last = f.Db(db => db.UserSettings.Single(s => s.UserId == p.Id).LastDigestAt);
            await f.DbAsync(async db => { db.ApprovedLeaves.Add(new() { PersonId = p.Id, SourceSystem = "Synthetic HR", ExternalId = Guid.NewGuid().ToString(),
                FromDate = Monday, ThroughDate = Monday.AddDays(4) }); return await db.SaveChangesAsync(); });
            for (var day = 19; day <= 25; day++)
            {
                f.Clock.SetDate(2026, 10, day); await f.RunJob<DigestJob>();
                Assert.Equal(last, f.Db(db => db.UserSettings.Single(s => s.UserId == p.Id).LastDigestAt));
                if (day == 19)
                {
                    var change = await d.NewTask(project.Id, extra: new { name = "Unnotified work changed while away" });
                    (await f.As(TestData.Marc).Patch($"/api/v1/tasks/{change.G("id")}", new { dueDate = "2026-10-27", reason = "Unnotified date update during leave" }, await d.TaskVersion(change))).EnsureSuccessStatusCode();
                }
            }
            for (var i = 0; i < 12; i++) await d.NewTask(project.Id, extra: new { assigneeId = p.Id, dueDate = "2026-10-20", name = $"Changed while away {i}" });
            await f.Evaluate(project.Id);
            Assert.Equal(1, f.Db(db => db.Emails.Count(e => e.UserId == p.Id && e.Kind == "Digest")));
            f.Clock.SetDate(2026, 10, 26); await f.RunJob<DigestJob>();
            var mail = f.Db(db => db.Emails.Where(e => e.UserId == p.Id && e.Kind == "Digest").OrderByDescending(e => e.CreatedAt).First());
            Assert.Contains("2026-10-26", mail.BodyText);
            Assert.Contains("OVERDUE (13)", mail.BodyText);
            Assert.Contains("and 3 more", mail.BodyText);
            Assert.Contains("PROJECT UPDATES", mail.BodyText);
            Assert.Contains("Unnotified work changed while away", mail.BodyText);
            Assert.Equal(2, f.Db(db => db.Emails.Count(e => e.UserId == p.Id && e.Kind == "Digest")));
            await f.RunJob<DigestJob>();
            Assert.Equal(2, f.Db(db => db.Emails.Count(e => e.UserId == p.Id && e.Kind == "Digest")));
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Disabling_every_digest_section_does_not_raise_a_late_digest_alert()
    {
        var saved = f.Clock.Now;
        try
        {
            f.Clock.Now = new(2026, 10, 19, 13, 0, 0, TimeSpan.Zero);
            var p = await Person();
            using var scope = f.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<HubDb>();
            var before = await OpsChecks.Gather(db, new OrgSettings(), f.Clock.Now, saved);
            (await f.As(p.Email).Put("/api/v1/me/preferences/digest", new { enabled = true, sectionsOff = Digest.SectionCodes })).EnsureSuccessStatusCode();
            var after = await OpsChecks.Gather(db, new OrgSettings(), f.Clock.Now, saved);
            Assert.Equal(before.DigestsNotBuilt - 1, after.DigestsNotBuilt);
        }
        finally { f.Clock.Now = saved; }
    }

    [Fact]
    public async Task Additive_migration_defaults_existing_settings_on_without_changing_other_preferences()
    {
        using var isolated = new HubFactory();
        using var client = isolated.As("migration-default@hub.test");
        var person = (await (await client.GetAsync("/api/v1/me")).Json()).G("id");
        await isolated.DbAsync(async db =>
        {
            var migrator = db.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20261012014621_StaffingSignals");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO hub.user_setting (user_id, digest_enabled, dense_rows, weekly_summary_enabled, digest_sections_off, digest_time_local) VALUES ({person}, false, false, false, '[]'::jsonb, '08:30')");
            await migrator.MigrateAsync();
            var setting = await db.UserSettings.AsNoTracking().SingleAsync(s => s.UserId == person);
            Assert.True(setting.HoldDigestWhenAway);
            Assert.False(setting.DigestEnabled); Assert.False(setting.DenseRows); Assert.False(setting.WeeklySummaryEnabled);
            Assert.Equal("08:30", setting.DigestTimeLocal);
            return 0;
        });
    }
}
