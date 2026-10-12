using System.Text;

namespace Hub.Domain;

public sealed record CalendarFeedEvent(string Uid, DateOnly From, DateOnly Through, string Summary, int Sequence, DateTimeOffset ChangedAt, bool Cancelled, string? Url = null);

/// RFC 5545: all-day end dates are exclusive and content lines fold at 75 UTF-8 octets.
public static class ICalendar
{
    public static string Write(IEnumerable<CalendarFeedEvent> events)
    {
        var lines = new List<string> { "BEGIN:VCALENDAR", "VERSION:2.0", "PRODID:-//Tuesday//Personal calendar//EN", "CALSCALE:GREGORIAN" };
        foreach (var e in events.OrderBy(e => e.Uid))
        {
            lines.AddRange(["BEGIN:VEVENT", "UID:" + Escape(e.Uid), "DTSTAMP:" + e.ChangedAt.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'"),
                "DTSTART;VALUE=DATE:" + e.From.ToString("yyyyMMdd"), "DTEND;VALUE=DATE:" + e.Through.AddDays(1).ToString("yyyyMMdd"),
                "SEQUENCE:" + e.Sequence, "STATUS:" + (e.Cancelled ? "CANCELLED" : "CONFIRMED"),
                "SUMMARY:" + Escape(e.Cancelled ? "Cancelled" : e.Summary), "TRANSP:TRANSPARENT"]);
            if (!e.Cancelled && e.Url != null) lines.Add("URL:" + e.Url);
            lines.Add("END:VEVENT");
        }
        lines.Add("END:VCALENDAR"); return string.Join("\r\n", lines.Select(Fold)) + "\r\n";
    }

    static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n").Replace(";", "\\;").Replace(",", "\\,");
    static string Fold(string line)
    {
        var text = new StringBuilder(); var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            if (octets + rune.Utf8SequenceLength > 75) { text.Append("\r\n "); octets = 1; }
            text.Append(rune.ToString()); octets += rune.Utf8SequenceLength;
        }
        return text.ToString();
    }
}
