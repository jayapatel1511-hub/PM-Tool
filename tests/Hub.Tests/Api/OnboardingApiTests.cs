using System.Net;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class OnboardingApiTests(HubFactory f)
{
    async Task Flag(bool on) => await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.onboarding_card", new { value = on }).Result.Json();

    [Fact, Trait("flag-off", "onboarding_card"), Trait("flag-on", "onboarding_card")] // AC-PRO-02, AC-PRO-03, AC-PRO-23
    public async Task Read_is_flag_gated_and_hide_show_persists_without_activity()
    {
        await Flag(false);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync("/api/v1/me/onboarding")).StatusCode);
        await Flag(true);
        try
        {
            var first = await (await f.As(TestData.Pm).GetAsync("/api/v1/me/onboarding")).Json();
            Assert.NotNull(first["groups"]);
            var user = await f.DbAsync(db => db.Users.SingleAsync(u => u.Email == TestData.Pm));
            var before = await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ActorUserId == user.Id));
            (await f.As(TestData.Pm).Post("/api/v1/me/onboarding/hide", new { })).EnsureSuccessStatusCode();
            Assert.NotNull(await f.DbAsync(db => db.UserSettings.Where(x => x.UserId == user.Id).Select(x => x.OnboardingHiddenAt).SingleAsync()));
            (await f.As(TestData.Pm).Post("/api/v1/me/onboarding/show", new { })).EnsureSuccessStatusCode();
            Assert.Null(await f.DbAsync(db => db.UserSettings.Where(x => x.UserId == user.Id).Select(x => x.OnboardingHiddenAt).SingleAsync()));
            Assert.Equal(before, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ActorUserId == user.Id)));
        }
        finally { await Flag(false); }
    }

    [Fact, Trait("flag-on", "onboarding_card")] // §51.2 catalogue: "Everyone except Read Only"
    public async Task Standard_user_without_workload_access_gets_the_own_plan_step()
    {
        await Flag(true);
        try
        {
            var card = await (await f.As(TestData.Alex).GetAsync("/api/v1/me/onboarding")).Json();
            var keys = card["groups"]!.AsArray().SelectMany(g => g!["steps"]!.AsArray()).Select(s => s!["key"]!.GetValue<string>()).ToArray();
            Assert.Contains("self-plan", keys);
        }
        finally { await Flag(false); }
    }

    [Fact, Trait("flag-on", "onboarding_card")] // AC-PRO-02
    public async Task Read_only_gets_no_steps()
    {
        await Flag(true);
        try
        {
            var body = await (await f.As(TestData.Rita).GetAsync("/api/v1/me/onboarding")).Json();
            Assert.Empty(body["groups"]!.AsArray());
        }
        finally { await Flag(false); }
    }
}
