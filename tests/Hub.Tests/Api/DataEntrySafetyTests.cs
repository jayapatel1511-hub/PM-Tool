using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

/// §55.2 Part A settings behind `flag.data_entry_safety` (FR-SAV-01, FR-SAV-03, FR-SAV-08, AC-SAV-06): with the flag off,
/// `/me` and the Admin settings list are as before; with it on, both carry `autosave_delay_ms` and `draft_keep_days`.
[Collection("api")]
public sealed class DataEntrySafetyTests(HubFactory f)
{
    async Task<JsonObject> Settings() => (await f.As(TestData.Alex).GetAsync("/api/v1/me").Result.Json())["settings"]!.AsObject();

    [Fact, Trait("flag-off", "data_entry_safety"), Trait("flag-on", "data_entry_safety")]
    public async Task Autosave_and_draft_settings_reach_the_client_only_with_the_flag()
    {
        var admin = f.As(TestData.Admin);
        try
        {
            foreach (var on in new[] { false, true })
            {
                (await admin.Put("/api/v1/admin/settings/flag.data_entry_safety", new { value = on })).EnsureSuccessStatusCode();
                var settings = await Settings();
                var rows = (await admin.GetAsync("/api/v1/admin/settings").Result.Json()).AsArray().Select(r => r!.S("key")).ToList();
                Assert.Equal(on, settings.ContainsKey("autosaveDelayMs"));
                Assert.Equal(on, settings.ContainsKey("draftKeepDays"));
                Assert.Equal(on, rows.Contains("autosave_delay_ms"));
                Assert.Equal(on, rows.Contains("draft_keep_days"));
                if (on) Assert.Equal((800, 7), (settings.I("autosaveDelayMs"), settings.I("draftKeepDays")));
            }
            (await admin.Put("/api/v1/admin/settings/autosave_delay_ms", new { value = 1500 })).EnsureSuccessStatusCode();
            Assert.Equal(1500, (await Settings()).I("autosaveDelayMs"));
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.Put("/api/v1/admin/settings/autosave_delay_ms", new { value = 100 })).StatusCode);
        }
        finally
        {
            (await admin.Put("/api/v1/admin/settings/autosave_delay_ms", new { value = 800 })).EnsureSuccessStatusCode();
            (await admin.Put("/api/v1/admin/settings/flag.data_entry_safety", new { value = false })).EnsureSuccessStatusCode();
        }
    }
}
