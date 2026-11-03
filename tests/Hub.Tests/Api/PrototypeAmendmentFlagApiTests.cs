using System.Net;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Api;

/// API contract checks for packets 052 and 053's dark flag catalog.
[Collection("api")]
public sealed class PrototypeAmendmentFlagApiTests(HubFactory f)
{
    static readonly string[] NewFlags = ["visual_system", "screen_patterns", "home_overview", "work_screens", "project_screens", "capacity_screens", "register_screens", "people_screens"];

    [Fact]
    public async Task Me_serializes_exactly_the_new_declared_flags_off_by_default()
    {
        var me = await (await f.As(TestData.Alex).GetAsync("/api/v1/me")).Json();
        var flags = me["flags"]!.AsObject();
        Assert.Equal(Flags.All.Select(x => x.Name), flags.Select(x => x.Key));
        Assert.Equal(NewFlags, flags.Where(x => NewFlags.Contains(x.Key)).Select(x => x.Key));
        Assert.All(NewFlags, name => Assert.False(flags[name]!.GetValue<bool>()));
    }

    [Fact]
    public async Task Admin_can_update_one_new_flag_and_other_new_flags_remain_off()
    {
        var admin = f.As(TestData.Admin);
        try
        {
            foreach (var selected in NewFlags)
            {
                foreach (var name in NewFlags)
                    (await admin.Put($"/api/v1/admin/settings/flag.{name}", new { value = false })).EnsureSuccessStatusCode();

                (await admin.Put($"/api/v1/admin/settings/flag.{selected}", new { value = true })).EnsureSuccessStatusCode();
                var me = await (await f.As(TestData.Alex).GetAsync("/api/v1/me")).Json();
                foreach (var name in NewFlags)
                    Assert.Equal(name == selected, me["flags"]![name]!.GetValue<bool>());
            }
        }
        finally
        {
            foreach (var name in NewFlags)
                (await admin.Put($"/api/v1/admin/settings/flag.{name}", new { value = false })).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task Non_admin_cannot_update_the_new_flags()
    {
        var response = await f.As(TestData.Alex).Put("/api/v1/admin/settings/flag.visual_system", new { value = true });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_rows_expose_only_declared_flag_metadata_without_extra_fields()
    {
        var rows = (await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/settings")).Json()).AsArray();
        var flagRows = rows.Where(r => r!.S("group") == "flags").ToArray();
        Assert.Equal(Flags.All.Select(f => $"flag.{f.Name}"), flagRows.Select(r => r!.S("key")));
        Assert.All(flagRows, row => Assert.Equal(["key", "kind", "group", "default", "value", "packet"], row!.AsObject().Select(p => p.Key)));
        Assert.All(flagRows, row => Assert.Equal(Flags.Find(row!.S("key"))!.Packet, row.S("packet")));
    }
}
