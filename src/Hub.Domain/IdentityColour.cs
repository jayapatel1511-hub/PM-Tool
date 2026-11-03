namespace Hub.Domain;

/// Stored project identity colours from FR-VSN-07.  They are deliberately
/// separate from status, health, and metric colours.
public static class IdentityColour
{
    public const string Coral = "#ec6a3e";
    public const string Mint = "#20a36d";
    public const string Rose = "#d9467b";
    public const string Cyan = "#1597cc";
    public const string Lime = "#84a32a";

    public static readonly string[] Palette = [Coral, Mint, Rose, Cyan, Lime];

    public static bool IsValid(string? value) => value is not null && Palette.Contains(value, StringComparer.OrdinalIgnoreCase);

    public static string LeastUsed(IEnumerable<string?> colours)
    {
        var counts = colours.Where(c => c is not null).GroupBy(c => c!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        return Palette.OrderBy(c => counts.GetValueOrDefault(c)).First();
    }
}
