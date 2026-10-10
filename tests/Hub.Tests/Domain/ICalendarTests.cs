using System.Text;
using Hub.Domain;
namespace Hub.Tests.Domain;
public sealed class ICalendarTests
{
    [Fact]
    public void All_day_events_escape_text_fold_utf8_and_sanitize_cancellations()
    {
        var from = new DateOnly(2026, 10, 9); var summary = "Design, review; " + new string('é', 100) + "\nBEGIN:VEVENT";
        var text = ICalendar.Write([new("stable@pm.engcalchub.com", from, from.AddDays(2), summary, 2, DateTimeOffset.UnixEpoch, false, "https://pm.engcalchub.com/my-work"), new("cancel@pm.engcalchub.com", from, from, "Secret project", 3, DateTimeOffset.UnixEpoch, true, "https://secret.test")]);
        Assert.Contains("DTEND;VALUE=DATE:20261012\r\n", text); Assert.Contains("SUMMARY:Design\\, review\\;", text);
        Assert.DoesNotContain("Secret project", text); Assert.Contains("STATUS:CANCELLED", text);
        Assert.Contains("URL:https://pm.engcalchub.com/my-work", text); Assert.DoesNotContain("secret.test", text);
        Assert.DoesNotContain("URL:", ICalendar.Write([new("without-url", from, from, "Work", 0, DateTimeOffset.UnixEpoch, false)]));
        Assert.All(text.Split("\r\n"), line => Assert.True(Encoding.UTF8.GetByteCount(line) <= 75));
        Assert.Equal(2, text.Split("\r\n").Count(line => line == "BEGIN:VEVENT"));
    }
}
