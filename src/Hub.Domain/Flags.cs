namespace Hub.Domain;

/// A release toggle (§43.4 FR-DLV-07): its name and the packet that declares it, for example "048".
public sealed record FlagDef(string Name, string Packet);

/// Feature flags (§43.4). A flag gates only an addition that has never been on in pilot (FR-DLV-09); no existing
/// feature is ever flagged. The list starts empty; each packet that adds a flag ends with the cleanup slice that removes it.
public static class Flags
{
    public static readonly FlagDef[] All = [new("show_deleted", "045")];

    /// `flag.<name>` as a Bool setting, default off, group `flags`, generated like `rule_enabled.*`.
    public static IEnumerable<SettingDef> Defs(IEnumerable<FlagDef> flags) =>
        flags.Select(f => new SettingDef($"flag.{f.Name}", SettingKind.Bool, false, "flags"));

    public static FlagDef? Find(string settingKey) =>
        settingKey.StartsWith("flag.", StringComparison.Ordinal) ? All.FirstOrDefault(f => $"flag.{f.Name}" == settingKey) : null;
}
