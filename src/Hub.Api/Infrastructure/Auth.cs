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
        foreach (var r in wanted.Where(w => !user.Roles.Any(x => x.Role == w && x.Source == RoleSource.Group)))
            user.Roles.Add(new UserSystemRole { UserId = user.Id, Role = r, Source = RoleSource.Group, GrantedAt = DateTimeOffset.UtcNow });
    }
}
