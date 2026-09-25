using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Outbound mail adapter (§17.6, §26.1): Graph sendMail from one service mailbox, an SMTP relay
/// (Azure Communication Services or corporate), or the development log sender. No replies are accepted.
public interface IEmailSender { Task Send(EmailMessage m, CancellationToken ct); }

public sealed class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    // Logs the recipient ID and subject only; bodies may contain comment text (§21 logging).
    public Task Send(EmailMessage m, CancellationToken ct) { log.LogInformation("Email to user {UserId}: {Subject}", m.UserId, m.Subject); return Task.CompletedTask; }
}

public sealed class GraphEmailSender(IHttpClientFactory http, GraphToken token, IConfiguration cfg) : IEmailSender
{
    public async Task Send(EmailMessage m, CancellationToken ct)
    {
        var client = http.CreateClient("graph");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await token.Get(ct));
        var payload = new
        {
            message = new
            {
                subject = m.Subject,
                body = new { contentType = "Text", content = m.BodyText },
                toRecipients = new[] { new { emailAddress = new { address = m.ToAddress } } },
            },
            saveToSentItems = false,
        };
        var res = await client.PostAsync($"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(cfg["Graph:Mailbox"]!)}/sendMail",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        res.EnsureSuccessStatusCode();
    }
}

public sealed class SmtpEmailSender(IConfiguration cfg) : IEmailSender
{
    public async Task Send(EmailMessage m, CancellationToken ct)
    {
        using var smtp = new SmtpClient(cfg["Email:Smtp:Host"], int.Parse(cfg["Email:Smtp:Port"] ?? "587")) { EnableSsl = true };
        if (cfg["Email:Smtp:User"] is { Length: > 0 } user) smtp.Credentials = new System.Net.NetworkCredential(user, cfg["Email:Smtp:Password"]); // from Key Vault
        using var msg = new MailMessage(cfg["Email:From"]!, m.ToAddress, m.Subject, m.BodyText);
        msg.Headers.Add("Auto-Submitted", "auto-generated");
        await smtp.SendMailAsync(msg, ct);
    }
}

/// Sends queued email every 30 seconds with retries and backoff; at-least-once with de-duplication (§22).
public sealed class EmailJob : IJob
{
    public string Name => "email-send";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromSeconds(30), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var sender = sp.GetRequiredService<IEmailSender>();
        var now = sp.GetRequiredService<TimeProvider>().GetUtcNow();
        var batch = await db.Emails.Where(e => e.SentAt == null && e.Attempts < 8 && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.CreatedAt).Take(100).ToListAsync(ct);
        int sent = 0, failed = 0;
        foreach (var m in batch)
        {
            try { await sender.Send(m, ct); m.SentAt = now; sent++; }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                m.Attempts++; m.LastError = e.GetType().Name; m.NextAttemptAt = now.AddMinutes(Math.Pow(2, m.Attempts)); failed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return new { sent, failed };
    }
}
