using System.Security.Claims;
using System.Text.Encodings.Web;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Hub.Api.Infrastructure;

/// The signed-in person, resolved once per request (§8.1 system layer).
public sealed class CurrentUser
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public Guid? SupervisorId { get; set; }
    public HashSet<string> Roles { get; set; } = [];
    public bool IsTemplateEditor { get; set; }
    public bool Resolved { get; set; }

    public bool Is(string role) => Roles.Contains(role);
    public bool IsAdmin => Is(SystemRole.Admin);
    public bool IsExecutive => Is(SystemRole.Executive);
    public bool IsSupervisor => Is(SystemRole.Supervisor);
    public bool IsSystemPM => Is(SystemRole.ProjectManager);
    public bool IsReadOnly => Is(SystemRole.ReadOnly) && !IsAdmin;
    public Actor Actor => new(Id, IsActive, Roles);
}

public static class AuthSetup
{
    public const string DevScheme = "Dev";
    public const string LocalScheme = "LocalPassword";
    /// The local-password cookie's fingerprint of the verifier that signed it in (LocalPasswordStore.Stamp).
    public const string StampClaim = "local_stamp";

    /// Sign-in throttling key. Behind the homedev tunnel every request arrives from the bridge
    /// gateway, so Cloudflare's client address keeps the limit per person rather than shared by all.
    public static string ClientKey(HttpContext ctx, System.Net.IPAddress? tunnelProxy)
    {
        var remote = ctx.Connection.RemoteIpAddress;
        var hop = remote is null ? null : remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote;
        var forwarded = ctx.Request.Headers["CF-Connecting-IP"];
        if (tunnelProxy is not null && hop?.Equals(tunnelProxy) == true && forwarded.Count == 1
            && System.Net.IPAddress.TryParse(forwarded[0], out var client))
            return "cf:" + (client.IsIPv4MappedToIPv6 ? client.MapToIPv4() : client);
        return remote?.ToString() ?? "unknown";
    }

    public static bool LocalAuthAllowed(IHostEnvironment env, IConfiguration cfg) =>
        (env.IsDevelopment() || env.IsStaging() || env.IsEnvironment("Testing")) && cfg["Auth:Mode"] == LocalScheme;

    public static bool DevAuthAllowed(IHostEnvironment env, IConfiguration cfg) =>
        (env.IsDevelopment() || env.IsEnvironment("Testing")) && cfg["Auth:Mode"] == "Development";

    public static void AddHubAuth(this WebApplicationBuilder b)
    {
        var dev = DevAuthAllowed(b.Environment, b.Configuration);
        var local = LocalAuthAllowed(b.Environment, b.Configuration);
        if (b.Configuration["Auth:Mode"] == LocalScheme && !local)
            throw new InvalidOperationException("LocalPassword authentication is permitted only in Development, Staging or Testing.");
        if (local)
        {
            var file = b.Configuration["Auth:Local:UsersFile"];
            var json = b.Configuration["Auth:Local:UsersJson"];
            var keys = b.Configuration["Auth:Local:KeyDirectory"];
            if (string.IsNullOrWhiteSpace(keys) || string.IsNullOrWhiteSpace(file) == string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException("LocalPassword requires exactly one credential source and a persistent data protection key directory.");
            b.Services.AddSingleton(new LocalPasswordStore(json ?? file!, json is not null));
            Directory.CreateDirectory(keys);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            b.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
        }
        var auth = b.Services.AddAuthentication(dev ? DevScheme : local ? LocalScheme : JwtBearerDefaults.AuthenticationScheme);
        if (dev)
            auth.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevScheme, _ => { });
        else if (local)
            auth.AddCookie(LocalScheme, o =>
            {
                o.Cookie.Name = "__Host-hub-review";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Strict;
                o.Cookie.Path = "/";
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = false;
                o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
                o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
                // Rotating or removing a person's verifier ends their sessions: a cookie is only good with the stamp it was issued with.
                o.Events.OnValidatePrincipal = async c =>
                {
                    var store = c.HttpContext.RequestServices.GetRequiredService<LocalPasswordStore>();
                    if (Guid.TryParse(c.Principal?.FindFirst("local_user_id")?.Value, out var id)
                        && c.Principal!.FindFirst(StampClaim)?.Value is { } stamp && store.Stamp(id) == stamp) return;
                    c.RejectPrincipal();
                    await c.HttpContext.SignOutAsync(LocalScheme);
                };
            });
        else
            auth.AddJwtBearer(o =>
            {
                // Entra ID v2 tokens issued for the Hub API audience only (§21, §23.6).
                var tenant = b.Configuration["Auth:Entra:TenantId"];
                o.Authority = $"https://login.microsoftonline.com/{tenant}/v2.0";
                o.Audience = b.Configuration["Auth:Entra:Audience"];
                o.MapInboundClaims = false;
                o.TokenValidationParameters.ValidateIssuer = true;
                o.TokenValidationParameters.RoleClaimType = "roles";
                o.TokenValidationParameters.NameClaimType = "name";
            });
        b.Services.AddAuthorization(o => o.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }

    /// One address with something either side of a single @ and no spaces: catches typos, not a full RFC 5322 check.
    public static bool IsEmail(string s)
    {
        var at = s.IndexOf('@');
        return s.Length <= 200 && at > 0 && at == s.LastIndexOf('@') && at < s.Length - 1 && !s.Any(char.IsWhiteSpace);
    }

    /// `Auth:Local:BootstrapAdmins`: "email|Display Name" entries separated by semicolons. A malformed entry stops
    /// start-up so a typo cannot leave the Hub without its administrator.
    public static List<(string Email, string Name)> ParseBootstrapAdmins(string spec)
    {
        var admins = new List<(string Email, string Name)>();
        foreach (var item in spec.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = item.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !IsEmail(parts[0]) || parts[1].Length is 0 or > 200)
                throw new InvalidOperationException("Auth:Local:BootstrapAdmins must list \"email|Display Name\" entries separated by semicolons.");
            if (!admins.Exists(a => a.Email.Equals(parts[0], StringComparison.OrdinalIgnoreCase))) admins.Add((parts[0], parts[1]));
        }
        return admins;
    }

    /// First Admin for local-password sign-in, which has no directory roles: at start-up, after migrations, each listed
    /// person exists (created active if missing) and holds Admin. Idempotent; never removes a role, renames or reactivates anyone.
    public static async Task BootstrapAdmins(HubDb db, IConfiguration cfg, IHostEnvironment env, ILogger log)
    {
        var spec = cfg["Auth:Local:BootstrapAdmins"];
        if (string.IsNullOrWhiteSpace(spec)) return;
        if (!LocalAuthAllowed(env, cfg)) { log.LogWarning("Auth:Local:BootstrapAdmins is ignored: it applies only to local-password sign-in."); return; }
        db.Audit.AsSystem("LocalBootstrap");
        foreach (var (email, name) in ParseBootstrapAdmins(spec))
        {
            var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == email);
            if (user is null)
            {
                user = new AppUser { Email = email, DisplayName = name, IsActive = true };
                db.Users.Add(user);
                db.Audit.Note(user, action: "Provisioned");
                log.LogInformation("Bootstrap admin {Email} created", email);
            }
            else if (!user.IsActive) { log.LogWarning("Bootstrap admin {Email} is inactive and was left unchanged", email); continue; }
            if (user.Roles.Exists(r => r.Role == SystemRole.Admin)) continue;
            var deliberatelyRemoved = await db.ActivityLog.AsNoTracking().AnyAsync(a =>
                a.ItemType == ItemType.User && a.ItemKey == user.Email && a.ItemName == SystemRole.Admin && a.Action == "RoleRemoved");
            if (deliberatelyRemoved)
            {
                log.LogWarning("Bootstrap admin {Email} was deliberately removed and was left unchanged", email);
                continue;
            }
            var role = new UserSystemRole { UserId = user.Id, Role = SystemRole.Admin, Source = RoleSource.Manual, GrantedAt = DateTimeOffset.UtcNow };
            db.UserRoles.Add(role); // through the set: a preset key reached only by navigation is tracked as an update
            db.Audit.Note(role, action: "RoleAdded", key: email);
            log.LogInformation("Bootstrap admin {Email} granted Admin", email);
        }
        await db.SaveChangesAsync();
    }
}

/// Development and test sign-in: `X-Dev-User: email` names a user; optional `X-Dev-Roles` carries
/// Entra app-role values so group-role sync can be exercised. Never registered in production.
public sealed class DevAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e)
    : AuthenticationHandler<AuthenticationSchemeOptions>(o, l, e)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var email = Request.Headers["X-Dev-User"].FirstOrDefault() ?? Request.Query["devUser"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(email)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim>
        {
            new("oid", "dev-" + email.Trim().ToLowerInvariant()),
            new("preferred_username", email.Trim()),
            new("name", Request.Headers["X-Dev-Name"].FirstOrDefault() ?? email.Split('@')[0]),
        };
        if (Request.Headers.TryGetValue("X-Dev-Roles", out var roles))
        {
            claims.Add(new Claim("dev_roles_present", "1"));
            foreach (var r in roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                claims.Add(new Claim("roles", r));
        }
        var id = new ClaimsIdentity(claims, AuthSetup.DevScheme, "name", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(id), AuthSetup.DevScheme)));
    }
}

/// Just-in-time provisioning (FR-AUTH-02) and group-role refresh (FR-AUTH-03) for every authenticated request.
public sealed class ProvisioningMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext ctx, HubDb db, CurrentUser me, AuditContext audit)
    {
        audit.Source = ctx.Request.Headers["X-Hub-Source"].FirstOrDefault() == "UI" ? "UI" : "API";
        if (ctx.User.Identity?.IsAuthenticated == true && ctx.Request.Path.StartsWithSegments("/api"))
        {
            var oid = ctx.User.FindFirst("oid")?.Value ?? ctx.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
            var email = ctx.User.FindFirst("preferred_username")?.Value ?? ctx.User.FindFirst("email")?.Value ?? "";
            var name = ctx.User.FindFirst("name")?.Value ?? email;
            if (string.IsNullOrEmpty(oid)) { ctx.Response.StatusCode = 401; return; }

            var isLocal = ctx.User.Identity.AuthenticationType == AuthSetup.LocalScheme;
            var localId = isLocal && Guid.TryParse(ctx.User.FindFirst("local_user_id")?.Value, out var parsed) ? parsed : Guid.Empty;
            var user = isLocal ? await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == localId) :
                await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.EntraObjectId == oid)
                ?? await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.EntraObjectId == null && u.Email == email);
            var isDev = ctx.User.Identity.AuthenticationType == AuthSetup.DevScheme;
            if (isLocal && (user is null || !user.IsActive)) { ctx.Response.StatusCode = 401; return; }
            if (user is null)
            {
                user = new AppUser { EntraObjectId = oid, Email = email, DisplayName = name, IsActive = true };
                db.Users.Add(user);
                audit.ActorType = "System";
                audit.Note(user, action: "Provisioned");
            }
            else
            {
                if (!isLocal) user.EntraObjectId ??= oid;
                // Matched by directory identity, so an email change in the directory updates the Hub (edge case).
                if (!isLocal && !string.IsNullOrEmpty(email) && user.Email != email) user.Email = email;
                if (!isDev && !isLocal && user.DisplayName != name) user.DisplayName = name;
            }
            if (!isLocal && (!isDev || ctx.User.HasClaim(c => c.Type == "dev_roles_present")))
                SyncGroupRoles(db, user, ctx.User.FindAll("roles").Select(c => c.Value));
            if (db.ChangeTracker.HasChanges())
            {
                audit.ActorId ??= user.Id;
                await db.SaveChangesAsync();
            }
            audit.ActorType = ctx.Request.Path.StartsWithSegments("/api/v1/admin") ? "Admin" : "User";
            me.Id = user.Id;
            me.Email = user.Email;
            me.DisplayName = user.DisplayName;
            me.IsActive = user.IsActive;
            me.SupervisorId = user.SupervisorId;
            me.IsTemplateEditor = user.IsTemplateEditor;
            me.Roles = [.. user.Roles.Select(r => r.Role)];
            me.Resolved = true;
            audit.ActorId = user.Id;
            if (!user.IsActive) throw ApiException.Forbidden("error.inactive_account");
        }
        await next(ctx);
    }

    static void SyncGroupRoles(HubDb db, AppUser user, IEnumerable<string> appRoles)
    {
        var wanted = appRoles.Select(r => SystemRole.AppRoles.GetValueOrDefault(r)).OfType<string>().ToHashSet();
        foreach (var r in user.Roles.Where(r => r.Source == RoleSource.Group && !wanted.Contains(r.Role)).ToList())
        {
            user.Roles.Remove(r);
            db.UserRoles.Remove(r);
        }
        // Through the set: a role added only to an existing person's navigation is tracked as an update and fails (409).
        foreach (var r in wanted.Where(w => !user.Roles.Any(x => x.Role == w && x.Source == RoleSource.Group)).ToList())
            db.UserRoles.Add(new UserSystemRole { UserId = user.Id, Role = r, Source = RoleSource.Group, GrantedAt = DateTimeOffset.UtcNow });
    }
}
