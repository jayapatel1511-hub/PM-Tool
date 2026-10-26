using System.Text.Json.Nodes;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

/// §43.4: `/me` gains only a `flags` map of the declared flags, all off by default, and Admin settings add only the flag rows (AC-DLV-05, AC-DLV-09).
[Collection("api")]
public sealed class FlagsApiTests(HubFactory f)
{
    [Fact]
    public async Task Me_returns_the_declared_flags_off_beside_its_unchanged_fields()
    {
        var me = (await f.As(TestData.Alex).GetAsync("/api/v1/me").Result.Json()).AsObject();
        Assert.Equal(["id", "displayName", "email", "jobTitle", "officeId", "supervisorId", "isTemplateEditor", "roles", "systemRoles",
            "capabilities", "settings", "preferences", "flags"], me.Select(p => p.Key));
        Assert.Equal(Flags.All.Select(x => x.Name), me["flags"]!.AsObject().Select(p => p.Key));
        Assert.All(me["flags"]!.AsObject(), p => Assert.False(p.Value!.GetValue<bool>()));
    }

    [Fact]
    public async Task Admin_settings_list_is_unchanged()
    {
        var admin = f.As(TestData.Admin);
        try
        {
            foreach (var enabled in new[] { false, true })
            {
                (await admin.Put("/api/v1/admin/settings/flag.route_counter", new { value = enabled })).EnsureSuccessStatusCode();
                var rows = (await admin.GetAsync("/api/v1/admin/settings").Result.Json()).AsArray();
                // Settings of other dark additions stay hidden while their flags are off (FR-DLV-09).
                Assert.Equal(OrgSettings.Defs.Where(d => !OrgSettings.ShownWithFlag.TryGetValue(d.Key, out var flag) || (enabled && flag == "route_counter")).Select(d => d.Key), rows.Select(r => r!.S("key")));
                Assert.Equal(enabled, rows.Any(r => r!.S("key") == "route_counter_enabled"));
                Assert.All(rows.Where(r => r!.S("group") != "flags"), r => Assert.Equal(["key", "kind", "group", "default", "value"], r!.AsObject().Select(p => p.Key)));
                Assert.Equal(Flags.All.Select(x => $"flag.{x.Name}"), rows.Where(r => r!.S("group") == "flags").Select(r => r!.S("key")));
                Assert.All(rows.Where(r => r!.S("group") == "flags"), r => Assert.Equal(Flags.Find(r!.S("key"))!.Packet, r!.S("packet")));
            }
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.route_counter", new { value = false })).EnsureSuccessStatusCode(); }
    }
    // Row actions are client-only; browser flag pairs cover rendering, this covers their authenticated setting bridge.
    [Fact, Trait("flag-off", "row_quick_actions"), Trait("flag-on", "row_quick_actions")]
    public async Task Row_actions_flag_starts_dark_and_admin_can_expose_it_to_the_client()
    {
        var admin = f.As(TestData.Admin);
        try
        {
            foreach (var on in new[] { false, true })
            {
                (await admin.Put("/api/v1/admin/settings/flag.row_quick_actions", new { value = on })).EnsureSuccessStatusCode();
                var me = await f.As(TestData.Pm).GetAsync("/api/v1/me").Result.Json();
                Assert.Equal(on, me["flags"]!["row_quick_actions"]!.GetValue<bool>());
            }
        }
        finally { (await admin.Put("/api/v1/admin/settings/flag.row_quick_actions", new { value = false })).EnsureSuccessStatusCode(); }
    }

}
