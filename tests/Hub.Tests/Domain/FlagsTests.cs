using System.Reflection;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Xunit;

namespace Hub.Tests.Domain;

/// §43.4 feature flags: definitions, the shared guard, and the two checks of FR-DLV-10 (retirement and pair).
/// A flag's tests carry `[Trait("flag-off", "<name>")]` (today's behaviour unchanged) and `[Trait("flag-on", "<name>")]` (the addition).
public sealed class FlagsTests
{
    static readonly FlagDef Demo = new("demo", "999");

    [Fact]
    public void A_declared_flag_becomes_a_bool_setting_default_off_in_the_flags_group() // AC-DLV-04
    {
        var def = Assert.Single(Flags.Defs([Demo]));
        Assert.Equal(new SettingDef("flag.demo", SettingKind.Bool, false, "flags"), def);
        Assert.Empty(Flags.Defs([]));
    }

    [Fact]
    public void Only_declared_flags_are_flag_settings_and_each_starts_off() // AC-DLV-04, AC-DLV-09
    {
        Assert.Equal(Flags.All.Select(f => $"flag.{f.Name}"), OrgSettings.Defs.Where(d => d.Group == "flags" || d.Key.StartsWith("flag.", StringComparison.Ordinal)).Select(d => d.Key));
        Assert.All(OrgSettings.Defs.Where(d => d.Group == "flags"), d => Assert.Equal(false, d.Default));
        Assert.Equal(Flags.All.Select(f => f.Name), new OrgSettings().Flags.Keys);
        Assert.DoesNotContain(true, new OrgSettings().Flags.Values);
        Assert.DoesNotContain(true, OrgSettings.From(new Dictionary<string, System.Text.Json.JsonElement>()).Flags.Values);
    }

    [Fact]
    public void The_guard_answers_404_while_off_and_passes_when_on() // AC-DLV-06
    {
        var off = new OrgSettings();
        Assert.False(off.IsFlagOn("demo")); // a missing name is off
        Assert.Equal(404, Assert.Throws<ApiException>(() => Http.RequireFlag(off, "demo")).Status);
        Assert.Equal(404, Assert.Throws<ApiException>(() => Http.RequireFlag(off with { Flags = new Dictionary<string, bool> { ["demo"] = false } }, "demo")).Status);
        Http.RequireFlag(off with { Flags = new Dictionary<string, bool> { ["demo"] = true } }, "demo");
    }

    // ---------- Retirement (FR-DLV-10, AC-DLV-07) ----------

    /// Flags whose packet's acceptance.md records pilot sign-off for them: a line naming "pilot sign-off" and `flag.<name>`.
    static string[] Retired(IEnumerable<FlagDef> flags, Func<string, string?> acceptance) =>
        flags.Where(f => acceptance(f.Packet)?.Split('\n').Any(l => l.Contains("pilot sign-off", StringComparison.OrdinalIgnoreCase) && l.Contains($"`flag.{f.Name}`")) == true)
            .Select(f => f.Name).ToArray();

    static string? RepoAcceptance(string packet)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Hub.slnx"))) dir = dir.Parent;
        var packetDir = dir is null ? null : Directory.GetDirectories(Path.Combine(dir.FullName, "specs"), packet + "-*").FirstOrDefault();
        var file = packetDir is null ? null : Path.Combine(packetDir, "acceptance.md");
        return file is not null && File.Exists(file) ? File.ReadAllText(file) : null;
    }

    [Fact]
    public void Retirement_names_a_flag_signed_off_in_pilot_and_still_declared()
    {
        Assert.Equal(["demo"], Retired([Demo], _ => "| Pilot sign-off | `flag.demo` | Jay | 2026-11-02 |"));
        Assert.Empty(Retired([Demo], _ => "| Review | `flag.demo` on in review |"));
        Assert.Empty(Retired([Demo], _ => null));
    }

    [Fact]
    public void No_declared_flag_has_its_pilot_sign_off_recorded() =>
        Assert.True(Retired(Flags.All, RepoAcceptance) is [], $"Remove from Flags.cs (cleanup pull request): {string.Join(", ", Retired(Flags.All, RepoAcceptance))}");

    // ---------- Pair check (FR-DLV-10, AC-DLV-08) ----------

    static string[] MissingPairs(IEnumerable<FlagDef> flags, IEnumerable<(string Name, string Value)> traits)
    {
        var have = traits.ToHashSet();
        return flags.SelectMany(f => new[] { "flag-off", "flag-on" }.Where(tag => !have.Contains((tag, f.Name))).Select(tag => $"{f.Name}: no {tag} test")).ToArray();
    }

    static IEnumerable<(string, string)> AssemblyTraits() =>
        typeof(FlagsTests).Assembly.GetTypes().SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(m => m.GetCustomAttributesData()).Where(a => a.AttributeType == typeof(TraitAttribute))
            .Select(a => ((string)a.ConstructorArguments[0].Value!, (string)a.ConstructorArguments[1].Value!));

    [Fact]
    public void Pair_check_names_the_flag_and_the_missing_tag()
    {
        Assert.Equal(["demo: no flag-off test"], MissingPairs([Demo], [("flag-on", "demo")]));
        Assert.Equal(["demo: no flag-off test", "demo: no flag-on test"], MissingPairs([Demo], []));
        Assert.Empty(MissingPairs([Demo], [("flag-on", "demo"), ("flag-off", "demo")]));
    }

    [Fact]
    public void Every_declared_flag_has_a_flag_off_and_a_flag_on_test() =>
        Assert.True(MissingPairs(Flags.All, AssemblyTraits()) is [], string.Join("; ", MissingPairs(Flags.All, AssemblyTraits())));
}
