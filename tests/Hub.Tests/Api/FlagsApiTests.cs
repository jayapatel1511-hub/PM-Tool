using System.Text.Json.Nodes;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

/// §43.4: `/me` gains a `flags` map of every declared flag, all off by default; other Admin settings rows are unchanged (AC-DLV-05, AC-DLV-09).
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
        var rows = (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings").Result.Json()).AsArray();
        Assert.Equal(OrgSettings.Defs.Select(d => d.Key), rows.Select(r => r!.S("key")));
        Assert.All(rows.Where(r => r!.S("group") != "flags"), r => Assert.Equal(["key", "kind", "group", "default", "value"], r!.AsObject().Select(p => p.Key)));
        Assert.All(rows.Where(r => r!.S("group") == "flags"), r => Assert.Equal(["key", "kind", "group", "default", "value", "packet"], r!.AsObject().Select(p => p.Key)));
    }
}
