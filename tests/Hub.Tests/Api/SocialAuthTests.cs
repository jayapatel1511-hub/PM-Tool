using System.Net;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class SocialAuthTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("tuesday-social-").FullName;
    readonly HubFactory f = new();
    readonly ProviderServer provider = new();
    WebApplicationFactory<Program>? app;
    HttpClient? client;

    sealed class ProviderServer : HttpMessageHandler
    {
        public string Subject = "synthetic-subject";
        public string Email = "person@outside.example";
        public bool Verified = true;
        public bool FailToken;
        public int Exchanges;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            object body;
            if (request.Method == HttpMethod.Post)
            {
                Interlocked.Increment(ref Exchanges);
                if (FailToken) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") });
                body = new { access_token = "synthetic-token", token_type = "Bearer", expires_in = 3600 };
            }
            else if (request.RequestUri!.Host == "graph.microsoft.com") body = new { id = Subject, mail = Email, displayName = "Social Person" };
            else body = new { id = Subject, email = Email, name = "Social Person", verified_email = Verified };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
        }
    }

    HttpClient Start()
    {
        f.Settings["Auth:Mode"] = "Social";
        f.Settings["Auth:Social:KeyDirectory"] = Path.Combine(dir, "keys");
        foreach (var p in new[] { "Microsoft", "Google" })
        {
            f.Settings[$"Auth:Social:{p}:ClientId"] = "synthetic-client";
            f.Settings[$"Auth:Social:{p}:ClientSecret"] = "synthetic-secret";
        }
        app = f.WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.Configure<GoogleOptions>("Google", o => o.Backchannel = new HttpClient(provider));
            s.Configure<MicrosoftAccountOptions>("Microsoft", o => o.Backchannel = new HttpClient(provider));
        }));
        return client = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
    }

    public void Dispose()
    {
        client?.Dispose();
        app?.Dispose();
        f.Dispose();
        Directory.Delete(dir, true);
    }

    static string Cookie(HttpResponseMessage r) => r.Headers.TryGetValues("Set-Cookie", out var values)
        ? string.Join("; ", values.Where(v => !v.StartsWith(".AspNetCore.Correlation") && !v.Contains("=;", StringComparison.Ordinal)).Select(v => v.Split(';')[0])) : "";

    static Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string path, string? cookie = null, string? origin = "https://localhost")
    {
        var request = new HttpRequestMessage(method, path);
        if (cookie is not null) request.Headers.Add("Cookie", cookie);
        if (method == HttpMethod.Post && origin is not null) request.Headers.Add("Origin", origin);
        return c.SendAsync(request);
    }

    async Task<(string Path, string Cookie)> Challenge(HttpClient c, string name, string? cookie = null, bool link = false)
    {
        var response = await Send(c, link ? HttpMethod.Post : HttpMethod.Get, $"/api/v1/auth/social/{(link ? "link" : "sign-in")}/{name}", cookie);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.Equal("code", query["response_type"]);
        Assert.False(query.ContainsKey("client_secret"));
        var correlation = string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(v => v.Split(';')[0]));
        return ($"/signin-{name.ToLowerInvariant()}?code=synthetic-code&state={Uri.EscapeDataString(query["state"].ToString())}", correlation + (cookie is null ? "" : "; " + cookie));
    }

    async Task<HttpResponseMessage> SignIn(HttpClient c, string name = "Google", string? cookie = null, bool link = false)
    {
        var challenge = await Challenge(c, name, cookie, link);
        return await Send(c, HttpMethod.Get, challenge.Path, challenge.Cookie);
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("Microsoft")]
    public async Task Provider_callback_registers_any_domain_without_elevated_roles_and_never_exposes_secrets(string name)
    {
        var c = Start();
        var configResponse = await c.GetAsync("/api/v1/config");
        var config = await configResponse.Json();
        Assert.Equal("Social", config["authMode"]!.GetValue<string>());
        Assert.Equal(2, config["providers"]!.AsArray().Count);
        Assert.DoesNotContain("synthetic-secret", await configResponse.Content.ReadAsStringAsync());
        var signed = await SignIn(c, name);
        Assert.Equal("/login", signed.Headers.Location?.ToString());
        var cookie = Cookie(signed);
        Assert.Contains("__Host-hub-social=", cookie);
        var me = await (await Send(c, HttpMethod.Get, "/api/v1/me", cookie)).Json();
        Assert.Equal(provider.Email, me["email"]!.GetValue<string>());
        Assert.Empty(me["systemRoles"]!.AsArray());
        Assert.False(me["capabilities"]!["admin"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-in", cookie)).StatusCode);
        var again = await SignIn(c, name, cookie);
        Assert.NotEqual(cookie, Cookie(again));
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", cookie)).StatusCode);
        Assert.Equal(1, f.Db(db => db.ExternalIdentities.Count()));
    }

    [Fact]
    public async Task Email_collision_cannot_claim_an_existing_admin_or_change_their_work()
    {
        var c = Start();
        var id = await f.DbAsync(async db =>
        {
            var u = new AppUser { Email = provider.Email, DisplayName = "Existing Admin", Roles = [new() { Role = SystemRole.Admin }] };
            db.Users.Add(u); await db.SaveChangesAsync(); return u.Id;
        });
        var r = await SignIn(c);
        Assert.Equal("/login?authError=email_exists", r.Headers.Location?.ToString());
        Assert.Equal("", Cookie(r));
        Assert.Equal(0, f.Db(db => db.ExternalIdentities.Count()));
        Assert.Equal("Existing Admin", f.Db(db => db.Users.Single(u => u.Id == id).DisplayName));
        Assert.Equal(1, f.Db(db => db.UserRoles.Count(u => u.UserId == id && u.Role == SystemRole.Admin)));
    }

    [Fact]
    public async Task Verified_existing_identity_mapping_preserves_user_id_profile_and_roles()
    {
        var c = Start();
        var id = await f.DbAsync(async db =>
        {
            var u = new AppUser { Email = "existing@outside.example", DisplayName = "Existing Admin", Roles = [new() { Role = SystemRole.Admin }] };
            db.Users.Add(u);
            db.ExternalIdentities.Add(new ExternalIdentity { Provider = "Microsoft", Subject = provider.Subject, UserId = u.Id });
            await db.SaveChangesAsync(); return u.Id;
        });
        var cookie = Cookie(await SignIn(c, "Microsoft"));
        var me = await (await Send(c, HttpMethod.Get, "/api/v1/me", cookie)).Json();
        Assert.Equal(id.ToString(), me["id"]!.GetValue<string>());
        Assert.Equal("Existing Admin", me["displayName"]!.GetValue<string>());
        Assert.Equal("existing@outside.example", me["email"]!.GetValue<string>());
        Assert.True(me["capabilities"]!["admin"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Session_expires_after_eight_hours_and_unknown_provider_is_refused()
    {
        var c = Start();
        Assert.Equal(HttpStatusCode.NotFound, (await c.GetAsync("/api/v1/auth/social/sign-in/not-a-provider")).StatusCode);
        var cookie = Cookie(await SignIn(c));
        f.Clock.Now = f.Clock.GetUtcNow().AddHours(8).AddSeconds(1);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", cookie)).StatusCode);
    }

    [Fact]
    public async Task Connecting_second_provider_keeps_one_user_and_cannot_reassign_another_users_identity()
    {
        var c = Start();
        var first = Cookie(await SignIn(c));
        var firstMe = await (await Send(c, HttpMethod.Get, "/api/v1/me", first)).Json();
        var linked = await SignIn(c, "Microsoft", first, link: true);
        Assert.Equal("/preferences?accountLinked=true", linked.Headers.Location?.ToString());
        var microsoft = Cookie(await SignIn(c, "Microsoft"));
        var sameMe = await (await Send(c, HttpMethod.Get, "/api/v1/me", microsoft)).Json();
        Assert.Equal(firstMe["id"]!.ToString(), sameMe["id"]!.ToString());
        provider.Subject = "another-synthetic-person"; provider.Email = "another@outside.example";
        var other = Cookie(await SignIn(c));
        provider.Subject = "synthetic-subject";
        var conflict = await SignIn(c, "Microsoft", other, link: true);
        Assert.Equal("/preferences?authError=connected", conflict.Headers.Location?.ToString());
        Assert.Equal(2, f.Db(db => db.Users.Count()));
    }

    [Fact]
    public async Task Linking_requires_an_authenticated_same_origin_post()
    {
        var c = Start();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Post, "/api/v1/auth/social/link/Google")).StatusCode);
        var cookie = Cookie(await SignIn(c));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, HttpMethod.Post, "/api/v1/auth/social/link/Microsoft", cookie, "https://evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, HttpMethod.Post, "/api/v1/auth/social/link/Microsoft", cookie, origin: null)).StatusCode);
    }

    [Theory]
    [InlineData("bad-state")]
    [InlineData("missing-correlation")]
    [InlineData("cancelled")]
    [InlineData("token-failed")]
    public async Task Failed_or_forged_callback_cannot_create_a_user_or_session(string failure)
    {
        var c = Start();
        var challenge = await Challenge(c, "Google");
        var path = failure == "bad-state" ? "/signin-google?code=synthetic&state=forged" : failure == "cancelled" ? challenge.Path.Replace("code=synthetic-code", "error=access_denied") : challenge.Path;
        provider.FailToken = failure == "token-failed";
        var r = await Send(c, HttpMethod.Get, path, failure == "missing-correlation" ? null : challenge.Cookie);
        Assert.Equal("/login?authError=failed", r.Headers.Location?.ToString());
        Assert.Equal("", Cookie(r));
        Assert.Equal(0, f.Db(db => db.Users.Count()));
        Assert.Equal(0, f.Db(db => db.LocalAuthSessions.Count()));
    }

    [Fact]
    public async Task Unverified_google_email_is_rejected_and_callback_cannot_be_replayed()
    {
        var c = Start();
        provider.Verified = false;
        Assert.Equal("/login?authError=profile", (await SignIn(c)).Headers.Location?.ToString());
        Assert.Equal(0, f.Db(db => db.Users.Count()));
        provider.Verified = true;
        var challenge = await Challenge(c, "Google");
        var signed = await Send(c, HttpMethod.Get, challenge.Path, challenge.Cookie);
        Assert.NotEqual("", Cookie(signed));
        var replay = await Send(c, HttpMethod.Get, challenge.Path);
        Assert.Equal("/login?authError=failed", replay.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Logout_revokes_copied_cookie_and_inactive_users_cannot_sign_in_or_use_existing_sessions()
    {
        var c = Start();
        var first = Cookie(await SignIn(c));
        var second = Cookie(await SignIn(c));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, HttpMethod.Post, "/api/v1/auth/social/sign-out", first, "https://evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(c, HttpMethod.Post, "/api/v1/auth/social/sign-out", first)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", second)).StatusCode);
        await f.DbAsync(async db => { var u = await db.Users.SingleAsync(); u.IsActive = false; await db.SaveChangesAsync(); return 0; });
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", second)).StatusCode);
        Assert.Equal("/login?authError=inactive", (await SignIn(c)).Headers.Location?.ToString());
    }
}
