using System.Collections.Immutable;
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
public interface IEmailSender
{
    string TransportKind => NoticeTransportKind.Unknown;
    Task Send(EmailMessage m, CancellationToken ct);
}

/// Optional positive transport observations. Task exceptions never establish non-acceptance.
public interface INoticeObservedEmailSender : IEmailSender
{
    Task<NoticeSendObservation> SendObserved(EmailMessage message, CancellationToken ct);
}

public static class NoticeEmailTransport
{
    public static async Task<NoticeSendObservation> SendAsync(IEmailSender sender, NoticeSendClaim claim, TimeProvider clock, CancellationToken ct)
    {
        if (!NoticeTransportKind.All.Contains(claim.TransportKind)) throw new NoticeProofException("InvalidTransportKind");
        if (sender.TransportKind != claim.TransportKind)
            return new(claim.TransportKind, NoticeTransportOutcome.Failed, clock.GetUtcNow(), null, NoticeFailureCertainty.DefinitelyNotAccepted);
        // A provider can never mutate a tracked queue row or choose a new destination/scope.
        var envelope = new EmailMessage { Id = claim.EmailId, UserId = claim.UserId, ToAddress = claim.Envelope.ToAddress,
            Subject = claim.Envelope.Subject, BodyText = claim.Envelope.BodyText, BodyHtml = claim.Envelope.BodyHtml,
            RequiredProjectIds = claim.Envelope.Scope.ProjectIds.ToArray(), RequiredPlanningEntryIds = claim.Envelope.Scope.PlanningEntryIds.ToArray() };
        try
        {
            if (sender is INoticeObservedEmailSender observed) return await observed.SendObserved(envelope, ct);
            await sender.Send(envelope, ct);
            var now = clock.GetUtcNow();
            return claim.TransportKind switch
            {
                NoticeTransportKind.Smtp or NoticeTransportKind.Graph => new(claim.TransportKind, NoticeTransportOutcome.Accepted, now, now, NoticeFailureCertainty.None),
                NoticeTransportKind.Log => new(claim.TransportKind, NoticeTransportOutcome.Simulated, now, null, NoticeFailureCertainty.None),
                _ => new(claim.TransportKind, NoticeTransportOutcome.Unknown, now, null, NoticeFailureCertainty.None),
            };
        }
        catch (Exception)
        { return new(claim.TransportKind, NoticeTransportOutcome.Uncertain, clock.GetUtcNow(), null, NoticeFailureCertainty.MayHaveAccepted); }
    }
}

public sealed class LogEmailSender(ILogger<LogEmailSender> log) : IEmailSender
{
    public string TransportKind => NoticeTransportKind.Log;
    // Logs the recipient ID and subject only; bodies may contain comment text (§21 logging).
    public Task Send(EmailMessage m, CancellationToken ct) { log.LogInformation("Email to user {UserId}: {Subject}", m.UserId, m.Subject); return Task.CompletedTask; }
}

public sealed class GraphEmailSender(IHttpClientFactory http, GraphToken token, IConfiguration cfg) : IEmailSender
{
    public string TransportKind => NoticeTransportKind.Graph;
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
    public string TransportKind => NoticeTransportKind.Smtp;
    public async Task Send(EmailMessage m, CancellationToken ct)
    {
        using var smtp = new SmtpClient(cfg["Email:Smtp:Host"], int.Parse(cfg["Email:Smtp:Port"] ?? "587")) { EnableSsl = true };
        if (cfg["Email:Smtp:User"] is { Length: > 0 } user) smtp.Credentials = new System.Net.NetworkCredential(user, cfg["Email:Smtp:Password"]); // from Key Vault
        using var msg = new MailMessage(cfg["Email:From"]!, m.ToAddress, m.Subject, m.BodyText);
        msg.Headers.Add("Auto-Submitted", "auto-generated");
        await smtp.SendMailAsync(msg, ct);
    }
}

/// Legacy retries retain their old contract. Enhanced sends use committed claims and factual outcomes.
public sealed class EmailJob : IJob
{
    public string Name => "email-send";
    public bool IsDue(DateTimeOffset now, DateTimeOffset? last, OrgSettings s) => Schedule.Every(TimeSpan.FromSeconds(30), now, last);

    public async Task<object?> Run(IServiceProvider sp, CancellationToken ct)
    {
        var db = sp.GetRequiredService<HubDb>();
        var sender = sp.GetRequiredService<IEmailSender>();
        var clock = sp.GetRequiredService<TimeProvider>(); var now = clock.GetUtcNow();
        var batch = await NotificationDelivery.LegacyEmails(db.Emails).Where(e => e.SentAt == null && e.SuppressedAt == null && e.Attempts < 8 && (e.NextAttemptAt == null || e.NextAttemptAt <= now))
            .OrderBy(e => e.CreatedAt).Take(100).ToListAsync(ct);
        int sent = 0, failed = 0;
        foreach (var m in batch)
        {
            if (m.RequiredProjectIds.Length > 0 && (m.UserId is not { } uid || !await EmailProjectAccess.Allowed(db, uid, m.RequiredProjectIds)))
            { m.SuppressedAt = now; m.LastError = "ProjectAccessRemoved"; continue; }
            if (m.Kind == "PlanningWithdrawal" && (m.UserId is not { } recipient || !await db.Users.AnyAsync(u => u.Id == recipient && u.IsActive)))
            { m.SuppressedAt = now; m.LastError = "PlanningAccessRemoved"; continue; }
            if (m.RequiredPlanningEntryIds.Length > 0 && (m.UserId is not { } planningUser || !await Hub.Api.Features.PlanningEndpoints.PlanningEmailAllowed(db, planningUser, m.RequiredPlanningEntryIds)))
            { m.SuppressedAt = now; m.LastError = "PlanningAccessRemoved"; continue; }
            try { await sender.Send(m, ct); m.SentAt = now; sent++; }
            catch (Exception e) when (!ct.IsCancellationRequested)
            {
                m.Attempts++; m.LastError = e.GetType().Name; m.NextAttemptAt = now.AddMinutes(Math.Pow(2, m.Attempts)); failed++;
            }
        }
        await db.SaveChangesAsync(ct);
        var flag = await db.Settings.AsNoTracking().Where(row => row.Key == "flag.people_screens").Select(row => row.Value).SingleOrDefaultAsync(ct);
        if (flag is null || !JsonDocument.Parse(flag).RootElement.GetBoolean()) return new { sent, failed, enhancedSent = 0, enhancedObserved = 0, policyExamined = 0 };

        int enhancedSent = 0, enhancedObserved = 0, policyExamined = 0;
        // JobHost holds this job's PostgreSQL advisory lock across the run. Claims already InFlight at
        // this run's start belong to an interrupted earlier run; recovery observes, never resends them.
        var legacyIds = NotificationDelivery.LegacyEmails(db.Emails).Select(row => row.Id);
        var orphaned = await db.Emails.AsNoTracking().Where(row => !legacyIds.Contains(row.Id)
            && row.TransportOutcome == NoticeTransportOutcome.InFlight && row.ProviderAcceptedAt == null && row.AcceptanceUncertainAt == null)
            .OrderBy(row => row.NoticeClaimedAt).ThenBy(row => row.Id).Take(100)
            .Select(row => new { row.Id, row.UserId, row.NoticeClaimToken, row.NoticeClaimVersion }).ToArrayAsync(ct);
        foreach (var row in orphaned)
        {
            if (row.UserId is not { } user || row.NoticeClaimToken is not { } token || row.NoticeClaimVersion is not { } version)
            { await Quarantine(sp, row.Id, clock.GetUtcNow(), ct); continue; }
            await Recipient(sp, user, async context => await NotificationDelivery.ObserveOrphanedClaimAsync(context, row.Id, token, version, clock.GetUtcNow(), ct), ct);
            enhancedObserved++;
        }
        var pending = await NotificationDelivery.SelectPendingAsync(db, clock.GetUtcNow(), 100, ct);
        foreach (var group in pending.GroupBy(row => row.UserId))
        {
            var ids = group.Select(row => row.IntentId).ToImmutableArray();
            var result = await Recipient(sp, group.Key, context => NotificationDelivery.ReconsiderPendingAsync(context, ids, clock.GetUtcNow(), ct), ct);
            policyExamined += result.Examined;
        }
        // Independent limits: dormant, terminal and not-due enhanced work cannot occupy legacy slots.
        var enhanced = await db.Emails.AsNoTracking().Where(row => !legacyIds.Contains(row.Id) && row.SentAt == null && row.SuppressedAt == null
            && row.ProviderAcceptedAt == null && row.AcceptanceUncertainAt == null
            && (row.TransportOutcome == null || row.TransportOutcome == NoticeTransportOutcome.Queued || row.TransportOutcome == NoticeTransportOutcome.Failed)
            && (row.NextAttemptAt == null || row.NextAttemptAt <= now))
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id).Take(100)
            .Select(row => new { row.Id, row.UserId }).ToArrayAsync(ct);
        foreach (var row in enhanced)
        {
            ct.ThrowIfCancellationRequested();
            if (row.UserId is not { } user)
            { await Quarantine(sp, row.Id, clock.GetUtcNow(), ct); continue; }
            var kind = sender.TransportKind;
            var result = await Recipient(sp, user, async context =>
            {
                var current = await context.Db.Emails.AsNoTracking().Where(email => email.Id == row.Id).Select(email => email.TransportOutcome).SingleAsync(ct);
                return await NotificationDelivery.ClaimAsync(context, row.Id, kind, current == NoticeTransportOutcome.Failed
                    ? NoticeClaimPurpose.KnownFailureRetry : NoticeClaimPurpose.Initial, clock.GetUtcNow(), ct);
            }, ct);
            if (result.Claim is not { } claim) continue;
            // Recipient transaction is committed before invoking any sender. Complete even after cancellation.
            var observation = await NoticeEmailTransport.SendAsync(sender, claim, clock, ct);
            await Recipient(sp, user, context => NotificationDelivery.CompleteAsync(context, claim, observation, CancellationToken.None), CancellationToken.None);
            enhancedObserved++;
            if (observation.Outcome == NoticeTransportOutcome.Accepted) enhancedSent++;
            ct.ThrowIfCancellationRequested();
        }
        return new { sent, failed, enhancedSent, enhancedObserved, policyExamined };
    }

    static async Task<T> Recipient<T>(IServiceProvider sp, Guid user, Func<NoticeRecipientContext, Task<T>> work, CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        return await Tx.Run(db, async () =>
        {
            var context = await NotificationDelivery.LockRecipientAsync(db, user, ct); var result = await work(context);
            await db.SaveChangesAsync(ct); return result;
        });
    }
    static async Task Quarantine(IServiceProvider sp, Guid emailId, DateTimeOffset now, CancellationToken ct)
    {
        await using var scope = sp.CreateAsyncScope(); scope.ServiceProvider.GetRequiredService<AuditContext>().AsSystem();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        await Tx.Run(db, async () =>
        { var result = await NotificationDelivery.QuarantineMalformedAsync(db, emailId, now, ct); await db.SaveChangesAsync(ct); return result; });
    }
}

/// Recheck scoped handoff/digest emails just before delivery; the queue is not an access grant.
public static class EmailProjectAccess
{
    public static async Task<bool> Allowed(HubDb db, Guid userId, Guid[] projectIds)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.IsActive)) return false;
        var elevated = await db.UserRoles.AnyAsync(r => r.UserId == userId && (r.Role == SystemRole.Admin || r.Role == SystemRole.Executive));
        var ids = projectIds.Distinct().ToArray();
        return await db.Projects.CountAsync(p => ids.Contains(p.Id)
            && p.Status != ProjectStatus.Archived && p.Status != ProjectStatus.Cancelled
            && (elevated || p.Visibility != Visibility.Restricted || p.ProjectManagerId == userId
                || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == userId && m.RemovedAt == null))) == ids.Length;
    }
}
