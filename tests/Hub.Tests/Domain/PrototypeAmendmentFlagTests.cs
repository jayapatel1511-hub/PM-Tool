using Hub.Domain;
using Xunit;

namespace Hub.Tests.Domain;

/// Contract checks for the dark flag catalog introduced by packets 052 and 053.
public sealed class PrototypeAmendmentFlagTests
{
    public static TheoryData<string, string> NewFlags => new()
    {
        { "visual_system", "052" },
        { "screen_patterns", "053" },
        { "home_overview", "053" },
        { "work_screens", "053" },
        { "project_screens", "053" },
        { "capacity_screens", "053" },
        { "register_screens", "053" },
        { "people_screens", "053" },
    };

    [Theory]
    [MemberData(nameof(NewFlags))]
    [Trait("flag-off", "visual_system")]
    [Trait("flag-off", "screen_patterns")]
    [Trait("flag-off", "home_overview")]
    [Trait("flag-off", "work_screens")]
    [Trait("flag-off", "project_screens")]
    [Trait("flag-off", "capacity_screens")]
    [Trait("flag-off", "register_screens")]
    [Trait("flag-off", "people_screens")]
    public void Every_new_flag_is_declared_with_its_packet_metadata(string name, string packet)
    {
        var flag = Assert.Single(Flags.All, f => f.Name == name);
        Assert.Equal(packet, flag.Packet);
        Assert.Equal(new SettingDef($"flag.{name}", SettingKind.Bool, false, "flags"),
            Assert.Single(Flags.Defs([flag])));
        Assert.False(new OrgSettings().IsFlagOn(name));
        Assert.False(OrgSettings.From(new Dictionary<string, System.Text.Json.JsonElement>()).IsFlagOn(name));
    }

    [Theory]
    [MemberData(nameof(NewFlags))]
    [Trait("flag-on", "visual_system")]
    [Trait("flag-on", "screen_patterns")]
    [Trait("flag-on", "home_overview")]
    [Trait("flag-on", "work_screens")]
    [Trait("flag-on", "project_screens")]
    [Trait("flag-on", "capacity_screens")]
    [Trait("flag-on", "register_screens")]
    [Trait("flag-on", "people_screens")]
    public void Selected_new_flag_can_be_on_without_turning_on_the_other_flags(string name, string _)
    {
        using var json = System.Text.Json.JsonDocument.Parse("true");
        var settings = OrgSettings.From(new Dictionary<string, System.Text.Json.JsonElement> { [$"flag.{name}"] = json.RootElement.Clone() });
        Assert.True(settings.IsFlagOn(name));
        Assert.All(Flags.All[^8..].Select(f => f.Name).Where(other => other != name), other => Assert.False(settings.IsFlagOn(other)));
    }
}
