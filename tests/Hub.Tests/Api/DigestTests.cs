using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 051 Part C, slice S10 (§55.4): the one-line "Admin review due" reminder in every Admin's daily digest, and nothing
/// for anyone else. Each test gets its own database, so the digest job sees exactly the people the test arranged.
public sealed class DigestTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public DigestTests() => data = new TestData(f);
    public void Dispose() => f.Dispose();

    HttpClient Admin => f.As(TestData.Admin);

    /// Runs the digest job at 10:00 Halifax on the given Monday-to-Friday date and returns each person's email for that day.
    async Task<Func<string, Task<EmailMessage?>>> Day(int y, int m, int d)
    {
        f.Clock.Now = new DateTimeOffset(y, m, d, 13, 0, 0, TimeSpan.Zero);
        await f.RunJob<Hub.Api.Infrastructure.DigestJob>();
        return email =>
        {
            var key = $"digest:{data.User(email)}:{y:0000}-{m:00}-{d:00}";
            return f.DbAsync(db => db.Emails.SingleOrDefaultAsync(e => e.DedupKey == key));
        };
    }

    /// Alex has an overdue task, so Alex's digest is sent whatever the Admin review says.
    async Task AlexIsOverdue()
    {
        var p = await data.Project();
        await data.NewTask(p.Id, TestData.Pm, new { name = "Late grading plan", assigneeId = data.User(TestData.Alex), dueDate = "2026-09-10" });
        await f.Evaluate(p.Id);
    }

    [Fact]
    [Trait("flag-on", "admin_review")]
    public async Task An_overdue_Admin_review_adds_one_line_to_each_Admins_digest_until_it_is_recorded() // AC-ADR-03, FR-ADR-04
    {
        (await Admin.Put("/api/v1/admin/settings/flag.admin_review", new { value = true })).EnsureSuccessStatusCode();
        await AlexIsOverdue();

        // Never recorded counts as due; the line says so, and the Admin gets a digest for it alone.
        var sent = await Day(2026, 9, 14);
        var jordan = (await sent(TestData.Admin))!;
        Assert.Equal("Hub digest — Admin review due", jordan.Subject);
        Assert.Contains("Admin review due: none has been recorded", jordan.BodyText);
        Assert.Contains("/admin/review", jordan.BodyText);

        // Anyone else's digest never carries it: Alex has work to report and no line, Rita has nothing and gets nothing.
        var alex = (await sent(TestData.Alex))!;
        Assert.Contains("OVERDUE (1)", alex.BodyText);
        Assert.DoesNotContain("Admin review", alex.BodyText + alex.Subject);
        Assert.Null(await sent(TestData.Rita));

        // Recorded: the line stops the next day, and with nothing else to say the Admin gets no digest.
        (await Admin.Post("/api/v1/admin/review", new { })).EnsureSuccessStatusCode();
        sent = await Day(2026, 9, 15);
        Assert.Null(await sent(TestData.Admin));
        Assert.NotNull(await sent(TestData.Alex));

        // 88 days after the review it is not yet due; 91 days after, it is due since the 90th day.
        sent = await Day(2026, 12, 11);
        Assert.Null(await sent(TestData.Admin));
        sent = await Day(2026, 12, 14);
        var due = (await Admin.GetAsync("/api/v1/admin/review").Result.Json()).S("dueOn");
        Assert.Equal("2026-12-13", due);
        jordan = (await sent(TestData.Admin))!;
        Assert.Contains($"Admin review due since {due}", jordan.BodyText);
        Assert.DoesNotContain("none has been recorded", jordan.BodyText);
        Assert.DoesNotContain("Admin review", (await sent(TestData.Alex))!.BodyText);

        // The line sits beside real content too, without changing that digest's subject.
        var p = await data.Project();
        await data.NewTask(p.Id, TestData.Pm, new { name = "Admin's own late task", assigneeId = data.User(TestData.Admin), dueDate = "2026-09-10" });
        await f.Evaluate(p.Id);
        sent = await Day(2026, 12, 15);
        jordan = (await sent(TestData.Admin))!;
        Assert.Equal("Hub digest — 1 overdue", jordan.Subject);
        Assert.Contains("Admin review due since 2026-12-13", jordan.BodyText);
        Assert.Contains("OVERDUE (1)", jordan.BodyText);

        (await Admin.Post("/api/v1/admin/review", new { })).EnsureSuccessStatusCode();
        sent = await Day(2026, 12, 16);
        Assert.DoesNotContain("Admin review", (await sent(TestData.Admin))!.BodyText);
    }

    [Fact]
    [Trait("flag-off", "admin_review")]
    public async Task With_the_flag_off_no_digest_carries_the_line() // AC-ADR-06, FR-ADR-08
    {
        await AlexIsOverdue();
        var sent = await Day(2026, 9, 14);
        Assert.Null(await sent(TestData.Admin)); // nothing to say, nothing sent, as before the packet
        var alex = (await sent(TestData.Alex))!;
        Assert.Equal("Hub digest — 1 overdue", alex.Subject);
        Assert.DoesNotContain("Admin review", alex.BodyText);
    }
}
