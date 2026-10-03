using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hub.Tests.Api;

/// Local-password pilot accounts: the first Admin comes from configuration, Admins add people, and logins load,
/// rotate and revoke from the verifier file without an API restart.
public sealed class LocalAccountsTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("hub-local-").FullName;
    readonly Dictionary<Guid, object> logins = [];
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    string Verifiers => Path.Combine(dir, "users.json");

    /// Writes the verifier file as the operator scripts do; the clock step makes the change certain on coarse file systems.
    void SaveLogins()
    {
        var before = File.Exists(Verifiers) ? File.GetLastWriteTimeUtc(Verifiers) : DateTime.UtcNow;
        File.WriteAllText(Verifiers, JsonSerializer.Serialize(new { users = logins.Values }));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(Verifiers, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.SetLastWriteTimeUtc(Verifiers, before.AddSeconds(1));
    }

    void SetLogin(Guid id, string userName, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, LocalPasswordStore.Iterations, HashAlgorithmName.SHA256, 32);
        logins[id] = new { userId = id.ToString(), userName, salt = Convert.ToHexStringLower(salt), hash = Convert.ToHexStringLower(hash) };
        SaveLogins();
    }

    static async Task<HttpResponseMessage> Send(HttpClient c, HttpMethod method, string url, string? cookie = null, object? body = null)
    {
        var req = new HttpRequestMessage(method, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Web);
        if (method != HttpMethod.Get) req.Headers.Add("Origin", "https://localhost"); // unsafe local-password requests must be same-origin
        if (cookie is not null) req.Headers.Add("Cookie", cookie);
        return await c.SendAsync(req);
    }

    static async Task<string> SignIn(HttpClient c, string userName, string password)
    {
        var r = await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-in", body: new { userName, password });
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        return string.Join("; ", r.Headers.GetValues("Set-Cookie").Select(v => v.Split(';', 2)[0]));
    }

    /// Gives each request the client address in `X-Test-Client`, standing in for different machines.
    sealed class ClientAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((ctx, more) =>
            {
                if (ctx.Request.Headers.TryGetValue("X-Test-Client", out var ip)) ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip.ToString());
                return more(ctx);
            });
            next(app);
        };
    }

    [Fact]
    public async Task One_login_name_is_limited_whichever_client_address_tries_it()
    {
        SaveLogins(); // no logins: every guess is wrong
        using var f = new HubFactory { Settings = {
            ["Auth:Mode"] = "LocalPassword", ["Auth:Local:UsersFile"] = Verifiers, ["Auth:Local:KeyDirectory"] = Path.Combine(dir, "keys"),
        } };
        using var app = f.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddTransient<IStartupFilter, ClientAddress>()));
        using var c = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        async Task<HttpStatusCode> Guess(string userName, int client)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/local/sign-in") { Content = JsonContent.Create(new { userName, password = "not the synthetic password" }, options: Web) };
            req.Headers.Add("Origin", "https://localhost");
            req.Headers.Add("X-Test-Client", $"198.51.100.{client}");
            return (await c.SendAsync(req)).StatusCode;
        }
        var codes = new List<HttpStatusCode>();
        for (var client = 1; client <= 6; client++) codes.Add(await Guess(client % 2 == 0 ? "Pat" : " pat", client)); // one name, six addresses
        Assert.Equal([.. Enumerable.Repeat(HttpStatusCode.Unauthorized, 5), HttpStatusCode.TooManyRequests], codes);
        Assert.Equal(HttpStatusCode.Unauthorized, await Guess("someone.else", 7)); // other names are not held up
    }

    [Fact]
    public async Task First_admin_comes_from_configuration_and_people_they_add_sign_in_rotate_and_revoke_without_a_restart()
    {
        SaveLogins(); // the empty bootstrap list
        using var f = new HubFactory { Settings = {
            ["Auth:Mode"] = "LocalPassword", ["Auth:Local:UsersFile"] = Verifiers, ["Auth:Local:KeyDirectory"] = Path.Combine(dir, "keys"),
            ["Auth:Local:BootstrapAdmins"] = "admin@hub.test|Review Admin",
        } };
        using var c = f.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        AppUser Person(string email) => f.Db(db => db.Users.Include(u => u.Roles).Single(u => u.Email == email));

        // Start-up created the configured admin, active, with a manual Admin role, logged as a System action.
        var admin = Person("admin@hub.test");
        Assert.True(admin.IsActive);
        Assert.Null(admin.EntraObjectId);
        Assert.Contains(admin.Roles, r => r.Role == SystemRole.Admin && r.Source == RoleSource.Manual);
        var boot = f.Db(db => db.ActivityLog.Where(a => a.Source == "LocalBootstrap").ToList());
        Assert.Equal(new[] { "Provisioned", "RoleAdded" }, boot.Select(a => a.Action).Order());
        Assert.All(boot, a => { Assert.Equal("System", a.ActorType); Assert.Null(a.ActorUserId); });

        // The operator adds the admin's verifier; the running API reads it without a restart.
        SetLogin(admin.Id, "admin", "admin synthetic password");
        var wrong = await Send(c, HttpMethod.Post, "/api/v1/auth/local/sign-in", body: new { userName = "admin", password = "admin synthetic passwordx" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var adminCookie = await SignIn(c, "admin", "admin synthetic password");
        var me = await (await Send(c, HttpMethod.Get, "/api/v1/me", adminCookie)).Json();
        Assert.Contains("Admin", me["systemRoles"]!.AsArray().Select(n => n!.GetValue<string>()));

        // The admin adds a person under their future Entra sign-in name, with title, office and supervisor.
        var office = f.Db(db => db.Offices.First().Id);
        var created = await (await Send(c, HttpMethod.Post, "/api/v1/admin/users", adminCookie,
            new { email = "Pat.Pilot@Example.com", displayName = "Pat Pilot", jobTitle = "Civil EIT", officeId = office, supervisorId = admin.Id })).Json(201);
        var patId = created.G("id");
        var pat = Person("Pat.Pilot@Example.com");
        Assert.Equal(patId, pat.Id);
        Assert.Equal(("Pat Pilot", "Civil EIT", (Guid?)office, (Guid?)admin.Id, true), (pat.DisplayName, pat.JobTitle, pat.OfficeId, pat.SupervisorId, pat.IsActive));
        Assert.Null(pat.EntraObjectId); // a later Entra sign-in with this email claims the record
        Assert.Empty(pat.Roles);
        Assert.True(f.Db(db => db.ActivityLog.Any(a => a.ItemId == patId && a.Action == "Created" && a.ActorType == "Admin" && a.ActorUserId == admin.Id)));

        // The same email in another case is the same person; malformed input is refused field by field.
        var again = await Send(c, HttpMethod.Post, "/api/v1/admin/users", adminCookie, new { email = "pat.pilot@example.com", displayName = "Again" });
        Assert.Equal("user_exists", (await again.Json(409)).S("code"));
        foreach (var (body, field) in new (object, string)[]
        {
            (new { email = "not an email", displayName = "X" }, "email"),
            (new { email = "x@example.com", displayName = " " }, "displayName"),
            (new { email = "x@example.com", displayName = "X", officeId = Guid.NewGuid() }, "officeId"),
            (new { email = "x@example.com", displayName = "X", supervisorId = Guid.NewGuid() }, "supervisorId"),
        })
            Assert.NotNull((await (await Send(c, HttpMethod.Post, "/api/v1/admin/users", adminCookie, body)).Json(400))["errors"]![field]);
        Assert.Equal(0, f.Db(db => db.Users.Count(u => u.Email == "x@example.com")));

        // Pat signs in once the operator issues a password, and cannot use Admin routes.
        SetLogin(patId, "pat", "pat synthetic password one");
        var patCookie = await SignIn(c, "pat", "pat synthetic password one");
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", patCookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(c, HttpMethod.Post, "/api/v1/admin/users", patCookie, new { email = "y@example.com", displayName = "Y" })).StatusCode);

        // Rotating Pat's password ends Pat's session and clears the cookie; the admin's session is untouched.
        SetLogin(patId, "pat", "pat synthetic password two");
        var stale = await Send(c, HttpMethod.Get, "/api/v1/me", patCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
        Assert.Contains(stale.Headers.GetValues("Set-Cookie"), v => v.StartsWith("__Host-hub-review=;", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, (await Send(c, HttpMethod.Get, "/api/v1/me", adminCookie)).StatusCode);

        // Removing the verifier ends the session signed in with the new password too.
        var patCookie2 = await SignIn(c, "pat", "pat synthetic password two");
        logins.Remove(patId);
        SaveLogins();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(c, HttpMethod.Get, "/api/v1/me", patCookie2)).StatusCode);

        // Bootstrapping again is idempotent: it adds Admin only where missing, never renames, demotes or reactivates.
        await f.DbAsync(async db => { db.Users.Add(new AppUser { Email = "gone@hub.test", DisplayName = "Gone", IsActive = false }); await db.SaveChangesAsync(); return 0; });
        var logged = f.Db(db => db.ActivityLog.Count());
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Auth:Mode"] = "LocalPassword", ["Auth:Local:BootstrapAdmins"] = "admin@hub.test|Renamed; pat.pilot@example.com|Pat; gone@hub.test|Gone",
        }).Build();
        var env = f.Services.GetRequiredService<IHostEnvironment>();
        for (var run = 0; run < 2; run++)
            await f.DbAsync(async db => { await AuthSetup.BootstrapAdmins(db, cfg, env, NullLogger.Instance); return 0; });
        Assert.Equal("Review Admin", Person("admin@hub.test").DisplayName);
        Assert.Single(Person("admin@hub.test").Roles, r => r.Role == SystemRole.Admin);
        Assert.Single(Person("Pat.Pilot@Example.com").Roles, r => r.Role == SystemRole.Admin && r.Source == RoleSource.Manual);
        Assert.False(Person("gone@hub.test").IsActive);
        Assert.Empty(Person("gone@hub.test").Roles);
        Assert.Equal(logged + 1, f.Db(db => db.ActivityLog.Count())); // only Pat's new role

        // A deliberate Admin-role removal remains authoritative across later startup runs.
        Assert.Equal(HttpStatusCode.NoContent, (await Send(c, HttpMethod.Delete,
            $"/api/v1/admin/users/{patId}/roles/{SystemRole.Admin}", adminCookie)).StatusCode);
        Assert.Empty(Person("Pat.Pilot@Example.com").Roles);
        var afterRemoval = f.Db(db => db.ActivityLog.Count());
        for (var run = 0; run < 2; run++)
            await f.DbAsync(async db => { await AuthSetup.BootstrapAdmins(db, cfg, env, NullLogger.Instance); return 0; });
        Assert.Empty(Person("Pat.Pilot@Example.com").Roles);
        Assert.Equal(afterRemoval, f.Db(db => db.ActivityLog.Count()));
    }
}

/// Entra (and development) sign-in: people and their directory roles arrive with sign-in, not through the Admin create route.
[Collection("api")]
public sealed class DirectoryProvisioningTests(HubFactory f)
{
    [Fact]
    public async Task Creating_people_is_refused_outside_local_password_sign_in()
    {
        var r = await f.As(TestData.Admin).Post("/api/v1/admin/users", new { email = $"n{Guid.NewGuid():N}"[..10] + "@hub.test", displayName = "Not Created" });
        Assert.Equal("local_sign_in_only", (await r.Json(422)).S("code"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post("/api/v1/admin/users", new { email = "z@hub.test", displayName = "Z" })).StatusCode);
    }

    [Fact]
    public async Task An_existing_person_who_joins_a_directory_role_group_gains_the_role_at_next_sign_in() // AC-AUTH-03
    {
        var email = $"gain{Guid.NewGuid():N}"[..12] + "@hub.test";
        var first = await (await f.As(email, "none").GetAsync("/api/v1/me")).Json();
        Assert.False(first["capabilities"]!["createProject"]!.GetValue<bool>());
        var later = await (await f.As(email, "Hub.ProjectManager").GetAsync("/api/v1/me")).Json(); // was 409 on every request
        Assert.True(later["capabilities"]!["createProject"]!.GetValue<bool>());
    }
}
