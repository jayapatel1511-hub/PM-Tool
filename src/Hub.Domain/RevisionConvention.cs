using System.Globalization;
using System.Text.RegularExpressions;

namespace Hub.Domain;

public sealed record RevisionConvention(string Preset, string[]? Codes = null)
{
    public string Pattern => Preset switch { "letters" => "A, B, C …", "numbers" => "0, 1, 2 …", "pc" => "P01, P02 … then C01, C02 …", _ => string.Join(", ", Codes ?? []) };
    public int? Position(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value)) return null;
        return Preset switch
        {
            "letters" => value.Length == 1 && value[0] is >= 'A' and <= 'Z' ? value[0] - 'A' : null,
            "numbers" => Regex.IsMatch(value, "^(0|[1-9][0-9]*)$", RegexOptions.CultureInvariant) && int.TryParse(value, CultureInfo.InvariantCulture, out var number) ? number : null,
            "pc" => Regex.IsMatch(value, "^[PC](0[1-9]|[1-9][0-9])$", RegexOptions.CultureInvariant) ? (value[0] == 'P' ? 0 : 100) + int.Parse(value[1..], CultureInfo.InvariantCulture) : null,
            "custom" => Array.FindIndex(Codes ?? [], c => string.Equals(c.Trim(), value, StringComparison.OrdinalIgnoreCase)) is var i && i >= 0 ? i : null,
            _ => null,
        };
    }
    public string[] Marks(string? code, string? lastIssued)
    {
        var position = Position(code);
        if (position == null) return [$"Non-standard revision: expected {Pattern}"];
        return Position(lastIssued) is { } previous && position < previous ? ["Out of sequence"] : [];
    }
    public bool Valid => Preset is "letters" or "numbers" or "pc" || Preset == "custom" && Codes is { Length: > 0 and <= 200 }
        && Codes.All(c => !string.IsNullOrWhiteSpace(c) && c.Length <= 50)
        && Codes.Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Codes.Length;
}
