using System.Security.Claims;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Api.Infrastructure;

public static class SocialAuth
{
    public const string Scheme = "Social";
    public static readonly string[] Providers = ["Microsoft", "Google"];

    public static bool Configured(IConfiguration cfg, string provider) =>
        !string.IsNullOrWhiteSpace(cfg[$"Auth:Social:{provider}:ClientId"])
        && !string.IsNullOrWhiteSpace(cfg[$"Auth:Social:{provider}:ClientSecret"]);

    public static void Add(WebApplicationBuilder b, AuthenticationBuilder auth)
    {
        foreach (var p in Providers)
            if (string.IsNullOrWhiteSpace(b.Configuration[$"Auth:Social:{p}:ClientId"])
                != string.IsNullOrWhiteSpace(b.Configuration[$"Auth:Social:{p}:ClientSecret"]))
                throw new InvalidOperationException($"Social sign-in requires both {p} ClientId and ClientSecret.");
        if (!Providers.Any(p => Configured(b.Configuration, p)))
            throw new InvalidOperationException("Social sign-in requires at least one configured provider.");
        var keys = b.Configuration["Auth:Social:KeyDirectory"];
        if (string.IsNullOrWhiteSpace(keys))
            throw new InvalidOperationException("Social sign-in requires a persistent Auth:Social:KeyDirectory.");
        Directory.CreateDirectory(keys);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(keys, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        b.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
        b.Services.AddSingleton<LocalSessionStore>();
        b.Services.AddOptions<CookieAuthenticationOptions>(Scheme).Configure<LocalSessionStore, TimeProvider>((o, sessions, clock) =>
        {
            o.SessionStore = sessions;
            o.TimeProvider = clock;
        });
        auth.AddCookie(Scheme, o =>
        {
            o.Cookie.Name = "__Host-hub-social";
            o.Cookie.HttpOnly = true;
            o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            o.Cookie.SameSite = SameSiteMode.Lax; // OAuth callbacks must receive the session when linking a second provider.
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = false;
            o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
            o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
        });
        if (Configured(b.Configuration, "Microsoft")) auth.AddMicrosoftAccount("Microsoft", o => Configure(o, b.Configuration, "Microsoft"));
        if (Configured(b.Configuration, "Google")) auth.AddGoogle("Google", o =>
        {
            Configure(o, b.Configuration, "Google");
            o.ClaimActions.MapJsonKey("email_verified", "email_verified");
        });
    }

    static void Configure(OAuthOptions o, IConfiguration cfg, string provider)
    {
        o.ClientId = cfg[$"Auth:Social:{provider}:ClientId"]!;
        o.ClientSecret = cfg[$"Auth:Social:{provider}:ClientSecret"]!;
        o.SignInScheme = Scheme;
        o.UsePkce = true;
        o.SaveTokens = false;
        o.Events.OnTicketReceived = Complete;
        o.Events.OnRemoteFailure = c =>
        {
            c.HandleResponse();
            c.Response.Redirect("/login?authError=failed");
            return Task.CompletedTask;
        };
    }

    static async Task Complete(TicketReceivedContext ctx)
    {
        var provider = ctx.Scheme.Name;
        var subject = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = ctx.Principal?.FindFirstValue(ClaimTypes.Email);
        var name = ctx.Principal?.FindFirstValue(ClaimTypes.Name);
        string? linkId = null;
        ctx.Properties?.Items.TryGetValue("link_user_id", out linkId);
        var linking = linkId is not null;
        var destination = linking ? "/preferences" : "/login";
        void Refuse(string reason)
        {
            ctx.HandleResponse();
            ctx.Response.Redirect(destination + "?authError=" + reason);
        }
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200 || email is null || !AuthSetup.IsEmail(email))
        { Refuse("profile"); return; }
        if (provider == "Google" && !string.Equals(ctx.Principal?.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase))
        { Refuse("profile"); return; }

        Guid? linkUser = null;
        if (linking)
        {
            // Only the authenticated, same-origin link endpoint can put this ID in protected OAuth state.
            if (!Guid.TryParse(linkId, out var target))
            { Refuse("failed"); return; }
            linkUser = target;
        }
        var db = ctx.HttpContext.RequestServices.GetRequiredService<HubDb>();
        var clock = ctx.HttpContext.RequestServices.GetRequiredService<TimeProvider>();
        var (user, error) = await Resolve(db, provider, subject, email, name ?? email, linkUser);
        if (user is null) { Refuse(error!); return; }
        db.Audit.ActorId = user.Id;
        db.Audit.Source = "UI";
        user.LastSignInAt = clock.GetUtcNow();
        db.LogEvent(ItemType.User, user.Id, linking ? "SignInConnected" : "SignedIn", "access", key: user.Email, name: provider);
        await db.SaveChangesAsync();
        ctx.Principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", $"social:{user.Id}"), new Claim("local_user_id", user.Id.ToString())
        ], Scheme));
        ctx.Properties!.IssuedUtc = clock.GetUtcNow();
        ctx.Properties.ExpiresUtc = clock.GetUtcNow().AddHours(8);
        ctx.Properties.Items.Remove("link_user_id");
        ctx.ReturnUri = linking ? "/preferences?accountLinked=true" : "/login";
    }

    public static async Task<(AppUser? User, string? Error)> Resolve(HubDb db, string provider, string subject, string email, string name, Guid? linkUser = null)
    {
        var identity = await db.ExternalIdentities.SingleOrDefaultAsync(i => i.Provider == provider && i.Subject == subject);
        if (identity is not null)
        {
            if (linkUser is { } target && identity.UserId != target) return (null, "connected");
            var existing = await db.Users.SingleOrDefaultAsync(u => u.Id == identity.UserId);
            return existing is { IsActive: true } ? (existing, null) : (null, "inactive");
        }
        AppUser user;
        if (linkUser is { } id)
        {
            var target = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive);
            if (target is null) return (null, "inactive");
            if (await db.ExternalIdentities.AnyAsync(i => i.UserId == id && i.Provider == provider)) return (null, "connected");
            user = target;
        }
        else
        {
            // Email is profile data, never authority to take over an existing person's roles or work.
            if (await db.Users.AnyAsync(u => u.Email == email)) return (null, "email_exists");
            user = new AppUser { Email = email, DisplayName = name.Length > 200 ? name[..200] : name, IsActive = true };
            db.Users.Add(user);
            db.Audit.AsSystem("SocialSignIn");
            db.Audit.Note(user, action: "Provisioned");
        }
        db.ExternalIdentities.Add(new ExternalIdentity { Provider = provider, Subject = subject, UserId = user.Id });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        {
            db.ChangeTracker.Clear();
            var raced = await db.ExternalIdentities.SingleOrDefaultAsync(i => i.Provider == provider && i.Subject == subject);
            if (raced is null) return (null, "email_exists");
            if (linkUser is { } target && raced.UserId != target) return (null, "connected");
            var existing = await db.Users.SingleOrDefaultAsync(u => u.Id == raced.UserId && u.IsActive);
            return existing is null ? (null, "inactive") : (existing, null);
        }
        return (user, null);
    }

    public static void Map(RouteGroupBuilder api)
    {
        static string? Provider(string name, IConfiguration cfg) => Providers.FirstOrDefault(p => p.Equals(name, StringComparison.OrdinalIgnoreCase) && Configured(cfg, p));
        api.MapGet("/auth/social/sign-in/{provider}", (string provider, IConfiguration cfg) =>
            Provider(provider, cfg) is { } p
                ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/login" }, [p])
                : Results.NotFound()).AllowAnonymous().RequireRateLimiting("local-sign-in");
        api.MapPost("/auth/social/sign-out", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(Scheme);
            return Results.NoContent();
        });
        api.MapGet("/auth/social/providers", async (HubDb db, CurrentUser me, IConfiguration cfg) => new
        {
            available = Providers.Where(p => Configured(cfg, p)).ToArray(),
            connected = await db.ExternalIdentities.Where(i => i.UserId == me.Id).Select(i => i.Provider).ToArrayAsync()
        });
        api.MapPost("/auth/social/link/{provider}", (string provider, IConfiguration cfg, CurrentUser me) =>
        {
            if (Provider(provider, cfg) is not { } p) return Results.NotFound();
            var properties = new AuthenticationProperties { RedirectUri = "/preferences" };
            properties.Items["link_user_id"] = me.Id.ToString();
            return Results.Challenge(properties, [p]);
        }).RequireRateLimiting("local-sign-in");
    }
}
