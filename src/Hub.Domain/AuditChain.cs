using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hub.Domain;

/// RFC 8785: ordinal UTF-16 keys, unchanged Unicode, and shortest IEEE-754 numbers.
public static class AuditChain
{
    public static readonly string ZeroHash = new('0', 64);
    public static string Hash(JsonElement input) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Canonical(input))));

    public static string Canonical(JsonElement value)
    {
        var b = new StringBuilder();
        Write(value, b);
        return b.ToString();
    }

    static void Write(JsonElement value, StringBuilder b)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                b.Append('{');
                var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                for (var i = 0; i < properties.Length; i++)
                {
                    if (i > 0) { if (properties[i - 1].Name == properties[i].Name) throw new JsonException("Duplicate canonical property."); b.Append(','); }
                    Quote(properties[i].Name, b); b.Append(':'); Write(properties[i].Value, b);
                }
                b.Append('}'); break;
            case JsonValueKind.Array:
                b.Append('['); var first = true;
                foreach (var item in value.EnumerateArray()) { if (!first) b.Append(','); first = false; Write(item, b); }
                b.Append(']'); break;
            case JsonValueKind.String: Quote(value.GetString()!, b); break;
            case JsonValueKind.Number: b.Append(Number(value.GetDouble())); break;
            case JsonValueKind.True: b.Append("true"); break;
            case JsonValueKind.False: b.Append("false"); break;
            case JsonValueKind.Null: b.Append("null"); break;
            default: throw new JsonException("Invalid canonical JSON value.");
        }
    }

    static void Quote(string value, StringBuilder b)
    {
        b.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"': b.Append("\\\""); break;
                case '\\': b.Append("\\\\"); break;
                case '\b': b.Append("\\b"); break;
                case '\t': b.Append("\\t"); break;
                case '\n': b.Append("\\n"); break;
                case '\f': b.Append("\\f"); break;
                case '\r': b.Append("\\r"); break;
                default:
                    if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1])) b.Append(c).Append(value[++i]);
                    else if (char.IsSurrogate(c)) throw new JsonException("Invalid Unicode in canonical JSON.");
                    else b.Append(c);
                    break;
            }
        }
        b.Append('"');
    }

    static string Number(double n)
    {
        if (!double.IsFinite(n)) throw new JsonException("Non-finite canonical number.");
        if (n == 0) return "0";
        var sign = n < 0 ? "-" : "";
        var text = Math.Abs(n).ToString("R", CultureInfo.InvariantCulture);
        var e = text.IndexOf('E');
        var mantissa = e < 0 ? text : text[..e];
        var exponent = e < 0 ? 0 : int.Parse(text[(e + 1)..], CultureInfo.InvariantCulture);
        var dot = mantissa.IndexOf('.');
        var digits = mantissa.Replace(".", "");
        var point = (dot < 0 ? mantissa.Length : dot) + exponent;
        if (point > 0 && point <= 21)
            return sign + (point >= digits.Length ? digits + new string('0', point - digits.Length) : digits[..point] + "." + digits[point..]);
        if (point <= 0 && point > -6) return sign + "0." + new string('0', -point) + digits;
        var power = point - 1;
        return sign + digits[0] + (digits.Length > 1 ? "." + digits[1..] : "") + "e" + (power >= 0 ? "+" : "") + power.ToString(CultureInfo.InvariantCulture);
    }
}
