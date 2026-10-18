using System.Text.Json.Nodes;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

/// §43.4 with no declared flags: `/me` gains only an empty `flags` map and Admin settings are unchanged (AC-DLV-05, AC-DLV-09).
[Collection("api")]
public sealed class FlagsApiTests(HubFactory f)
{
    [Fact]
    public async Task Me_returns_an_empty_flags_map_beside_its_unchanged_fields()
    {
        var me = (await f.As(TestData.Alex).GetAsync("/api/v1/me").Result.Json()).AsObject();
        Assert.Equal(["id", "displayName", "email", "jobTitle", "officeId", "supervisorId", "isTemplateEditor", "roles", "systemRoles",
            "capabilities", "settings", "preferences", "flags"], me.Select(p => p.Key));
        Assert.Empty(me["flags"]!.AsObject());
    }

    [Fact]
    public async Task Admin_settings_list_is_unchanged()
    {
        var rows = (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings").Result.Json()).AsArray();
        Assert.Equal(OrgSettings.Defs.Select(d => d.Key), rows.Select(r => r!.S("key")));
        Assert.All(rows, r => Assert.Equal(["key", "kind", "group", "default", "value"], r!.AsObject().Select(p => p.Key)));
        Assert.DoesNotContain(rows, r => r!.S("group") == "flags");
    }
}
