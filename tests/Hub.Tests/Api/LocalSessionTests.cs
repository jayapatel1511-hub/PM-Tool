using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class LocalSessionTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("hub-session-").FullName;
    readonly Guid userId = Guid.NewGuid();
    readonly HubFactory f = new();
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public void Dispose()
    {
        f.Dispose();
        Directory.Delete(dir, recursive: true);
    }

    string Verifiers => Path.Combine(dir, "users.json");

    void SaveVerifier(string userName = "session-user", string password = "synthetic session password")
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, LocalPasswordStore.Iterations, HashAlgorithmName.SHA256, 32);
        File.WriteAllText(Verifiers, JsonSerializer.Serialize(new
        {
            users = new[] { new { userId = userId.ToString(), userName, salt = Convert.ToHexStringLower(salt), hash = Convert.ToHexStringLower(hash) } }
        }));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Verifiers, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    async Task<(HubFactory App, HttpClient Client)> StartAsync()
    {
        f.Settings["Auth:Mode"] = "LocalPassword";
        f.Settings["Auth:Local:UsersFile"] = Verifiers;
        f.Settings["Auth:Local:KeyDirectory"] = Path.Combine(dir, "keys");
        SaveVerifier();
        var app = f;
        _ = app.Server;
        await f.DbAsync(async db =>
        {
            db.Users.Add(new AppUser { Id = userId, Email = "session-user@hub.test", DisplayName = "Session User", IsActive = true });
            await db.SaveChangesAsync();
            return 0;
        });
        return (app, app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false }));
    }

    static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string url, string? cookie = null, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Web);
        if (method != HttpMethod.Get) req.Headers.Add("Origin", "https://localhost");
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        return await c.SendAsync(req);
    }

    static async Task<string> SignIn(HttpClient c, string? cookie = null)
    {
        var response = await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-in", cookie, new { userName = "session-user", password = "synthetic session password" });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(v => v.Split(';', 2)[0]));
    }

    static ClaimsPrincipal Principal(Guid id = default, string stamp = "synthetic-stamp")
    {
        id = id == default ? Guid.NewGuid() : id;
        return new(new ClaimsIdentity([
            new Claim("oid", $"local:{id:D}"),
            new Claim("local_user_id", id.ToString()),
            new Claim(AuthSetup.StampClaim, stamp)
        ], AuthSetup.LocalScheme));
    }

    static AuthenticationTicket Ticket(DateTimeOffset expires) =>
        new(Principal(), new AuthenticationProperties { ExpiresUtc = expires }, AuthSetup.LocalScheme);

    [Fact]
    public async Task Logout_revokes_copied_cookie_but_leaves_another_session_usable()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            var first = await SignIn(c);
            var second = await SignIn(c);
            Assert.Equal(HttpStatusCode.NoContent, (await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-out", first)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", first)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", second)).StatusCode);
        }
    }

    [Fact]
    public async Task Sign_in_with_existing_cookie_mints_a_new_session()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            var first = await SignIn(c);
            var second = await SignIn(c, first);
            Assert.NotEqual(first, second);
            Assert.Equal(HttpStatusCode.NoContent, (await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-out", second)).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", first)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", second)).StatusCode);
        }
    }

    [Fact]
    public async Task Separate_store_instances_observe_persisted_session_and_remove()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            using var firstScope = f.Services.CreateScope();
            using var secondScope = f.Services.CreateScope();
            var first = ActivatorUtilities.CreateInstance<LocalSessionStore>(firstScope.ServiceProvider);
            var second = ActivatorUtilities.CreateInstance<LocalSessionStore>(secondScope.ServiceProvider);
            var id = await first.StoreAsync(Ticket(f.Clock.GetUtcNow().AddMinutes(5)));
            Assert.NotNull(await second.RetrieveAsync(id));
            await first.RemoveAsync(id);
            Assert.Null(await second.RetrieveAsync(id));
        }
    }

    [Fact]
    public async Task Expired_session_is_unavailable_and_renew_after_remove_does_not_resurrect_it()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            using var scope = f.Services.CreateScope();
            var store = ActivatorUtilities.CreateInstance<LocalSessionStore>(scope.ServiceProvider);
            var id = await store.StoreAsync(Ticket(f.Clock.GetUtcNow().AddMinutes(1)));
            f.Clock.Now = f.Clock.GetUtcNow().AddMinutes(2);
            Assert.Null(await store.RetrieveAsync(id));
            await store.RemoveAsync(id);
            await store.RenewAsync(id, Ticket(f.Clock.GetUtcNow().AddHours(1)));
            Assert.Null(await store.RetrieveAsync(id));
        }
    }

    [Fact]
    public async Task Sign_in_cleans_at_most_one_hundred_expired_sessions()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            await f.DbAsync(async db =>
            {
                for (var i = 0; i < 101; i++)
                    db.LocalAuthSessions.Add(new LocalAuthSession { Id = Guid.NewGuid(), ExpiresAt = f.Clock.GetUtcNow().AddMinutes(-1), Ticket = "synthetic" });
                return await db.SaveChangesAsync();
            });
            Assert.Equal(HttpStatusCode.NoContent, (await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-in", body: new { userName = "session-user", password = "synthetic session password" })).StatusCode);
            Assert.InRange(f.Db(db => db.LocalAuthSessions.Count(s => s.ExpiresAt <= f.Clock.GetUtcNow())), 1, 1);
        }
    }

    [Fact]
    public async Task Old_stateless_cookie_without_session_id_is_rejected()
    {
        var (app, c) = await StartAsync();
        using (c)
        {
            var protector = f.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(
                "Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationMiddleware", AuthSetup.LocalScheme, "v2");
            var format = new TicketDataFormat(protector);
            var stamp = f.Services.GetRequiredService<LocalPasswordStore>().Stamp(userId);
            Assert.NotNull(stamp);
            var oldCookie = format.Protect(new AuthenticationTicket(
                Principal(userId, stamp!), new AuthenticationProperties { ExpiresUtc = f.Clock.GetUtcNow().AddHours(1) }, AuthSetup.LocalScheme));
            var response = await Send(c, HttpMethod.Get, "/api/v1/me", $"__Host-hub-review={oldCookie}");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}
