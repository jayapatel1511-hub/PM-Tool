using Hub.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

/// Shared persistent tickets let sign-out revoke copied cookies on every API instance.
public sealed class LocalSessionStore(IServiceScopeFactory scopes, IDataProtectionProvider protection, TimeProvider clock) : ITicketStore
{
    readonly TicketDataFormat format = new(protection.CreateProtector("Hub.LocalSessionStore.v1"));

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var now = clock.GetUtcNow();
        var expired = db.LocalAuthSessions.Where(s => s.ExpiresAt <= now).OrderBy(s => s.ExpiresAt).Take(100).Select(s => s.Id);
        await db.LocalAuthSessions.Where(s => expired.Contains(s.Id)).ExecuteDeleteAsync();
        var session = new LocalAuthSession
        {
            Id = Guid.NewGuid(),
            ExpiresAt = ticket.Properties.ExpiresUtc ?? throw new InvalidOperationException("Local sessions require an expiry."),
            Ticket = format.Protect(ticket),
        };
        db.LocalAuthSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id.ToString("N");
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        if (!Guid.TryParseExact(key, "N", out var id)) return null;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var now = clock.GetUtcNow();
        var ticket = await db.LocalAuthSessions.Where(s => s.Id == id && s.ExpiresAt > now).Select(s => s.Ticket).SingleOrDefaultAsync();
        return ticket is null ? null : format.Unprotect(ticket);
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext context, CancellationToken cancellationToken)
    {
        // Fresh sign-in must issue a new reference: an old cookie must never acquire the next person's identity.
        return (HttpMethods.IsPost(context.Request.Method) && context.Request.Path.StartsWithSegments("/api/v1/auth/local/sign-in"))
            || context.Request.Path == "/signin-google" || context.Request.Path == "/signin-microsoft"
            ? Task.FromResult<AuthenticationTicket?>(null) : RetrieveAsync(key);
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        if (!Guid.TryParseExact(key, "N", out var id)) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        var expires = ticket.Properties.ExpiresUtc ?? throw new InvalidOperationException("Local sessions require an expiry.");
        var value = format.Protect(ticket);
        // Update only: a request racing sign-out must never recreate the revoked session.
        await db.LocalAuthSessions.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpiresAt, expires).SetProperty(x => x.Ticket, value));
    }

    public async Task RemoveAsync(string key)
    {
        if (!Guid.TryParseExact(key, "N", out var id)) return;
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        await db.LocalAuthSessions.Where(s => s.Id == id).ExecuteDeleteAsync();
    }
}
