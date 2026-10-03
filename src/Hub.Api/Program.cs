using System.Threading.RateLimiting;
using System.Security.Claims;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using System.Net;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---------- Database (one PostgreSQL database, §23.1) ----------
var dsb = new NpgsqlDataSourceBuilder(cfg.GetConnectionString("Hub") ?? "Host=localhost;Port=55432;Database=hub;Username=hub");
if (cfg["Db:UseManagedIdentity"] == "true")
{
    // Entra-authenticated PostgreSQL: no database password exists anywhere (§21 secret management).
    var cred = new DefaultAzureCredential();
    dsb.UsePeriodicPasswordProvider(async (_, ct) =>
        (await cred.GetTokenAsync(new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), ct)).Token,
        TimeSpan.FromMinutes(50), TimeSpan.FromSeconds(10));
}
var dataSource = dsb.Build();
builder.Services.AddSingleton(dataSource);
builder.Services.AddDbContext<HubDb>(o => o.UseNpgsql(dataSource, x => x.MigrationsHistoryTable("__ef_migrations", "hub")));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SettingsStore>();
builder.Services.AddSingleton<EvaluationSignal>();
builder.Services.AddScoped<AuditContext>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddScoped<Access>();
builder.Services.AddHttpClient("graph");
builder.Services.AddSingleton<GraphToken>();
builder.Services.AddSingleton<IUserDirectory, GraphDirectory>();

// ---------- Background work hosted in the API (§23.5) ----------
builder.Services.AddSingleton<IJob, DirectorySyncJob>();
builder.Services.AddSingleton<JobHost>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<JobHost>());
HubModules.AddServices(builder.Services, cfg);

// Requests, dependencies and logs go to Application Insights when the environment provides it (§23.8): the failed-request
// alert and the OpsAlert and heartbeat signals read them there. Query strings are redacted by the instrumentation.
if (!string.IsNullOrEmpty(cfg["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();

builder.AddHubAuth();
// §21: a request body holds at most 1 MB. There are no uploads; the largest legitimate body, a template structure, is a few kilobytes.
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 1024 * 1024);
var tunnelProxy =builder.Environment.IsStaging() && cfg.GetValue<bool>("Hosting:LocalTunnelProxy")
    && IPAddress.TryParse(cfg["Hosting:LocalTunnelProxyAddress"], out var configuredTunnelProxy) ? configuredTunnelProxy : null;
builder.Services.AddRateLimiter(o =>
{
    // §21: a per-user limit protects the API from runaway clients.
    o.RejectionStatusCode = 429;
    o.OnRejected = (ctx, _) => // §25: tell the client when to try again
    {
        var after = ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? (int)Math.Ceiling(wait.TotalSeconds) : 60;
        ctx.HttpContext.Response.Headers.RetryAfter = after.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirst("oid")?.Value ?? AuthSetup.ClientKey(ctx, tunnelProxy),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = int.TryParse(cfg["RateLimit:PerMinute"], out var n) ? n : 600, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("local-sign-in", ctx => RateLimitPartition.GetFixedWindowLimiter(AuthSetup.ClientKey(ctx, tunnelProxy),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) }));
});
// The same five attempts a minute per login name, so guesses at one login cannot be spread over many client addresses.
builder.Services.AddSingleton(_ => PartitionedRateLimiter.Create<string, string>(name =>
    RateLimitPartition.GetFixedWindowLimiter(name, _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) })));
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

// The homedev review origin is bound to loopback behind a local Cloudflare Tunnel.
// Trust only that local hop, and only its original scheme, for HTTPS and Origin checks.
if (app.Environment.IsStaging() && cfg.GetValue<bool>("Hosting:LocalTunnelProxy"))
{
    if (!IPAddress.TryParse(cfg["Hosting:LocalTunnelProxyAddress"], out var proxyAddress))
        throw new InvalidOperationException("Hosting:LocalTunnelProxyAddress must identify the review bridge gateway.");
    var forwarded = new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedProto,
        ForwardLimit = 1
    };
    forwarded.KnownProxies.Add(IPAddress.Loopback);
    forwarded.KnownProxies.Add(IPAddress.IPv6Loopback);
    forwarded.KnownProxies.Add(proxyAddress);
    app.UseForwardedHeaders(forwarded);
}

app.UseMiddleware<ProblemMiddleware>();
app.UseMiddleware<SecurityHeaders>();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
if (AuthSetup.LocalAuthAllowed(app.Environment, cfg))
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method))
        {
            var origin = ctx.Request.Headers.Origin.ToString();
            var expected = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
            if (!StringComparer.OrdinalIgnoreCase.Equals(origin, expected)) { ctx.Response.StatusCode = 403; return; }
        }
        await next(ctx);
    });
app.UseAuthentication();
app.UseMiddleware<ProvisioningMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapGet("/health", async (HubDb db, TimeProvider clock) =>
{
    // Reveals availability only; outbox lag above 10 minutes reports Degraded (§23.8).
    try
    {
        var oldest = await db.Outbox.Where(o => o.ProcessedAt == null).MinAsync(o => (DateTimeOffset?)o.CreatedAt);
        var degraded = oldest is { } t && clock.GetUtcNow() - t > TimeSpan.FromMinutes(10);
        return Results.Json(new { status = degraded ? "Degraded" : "Healthy" });
    }
    catch { return Results.Json(new { status = "Unhealthy" }, statusCode: 503); }
}).AllowAnonymous();

var api = app.MapGroup("/api/v1");
if (AuthSetup.LocalAuthAllowed(app.Environment, cfg))
{
    api.MapPost("/auth/local/sign-in", async (LocalPasswordStore store, HubDb db, HttpContext ctx, LocalSignIn input, PartitionedRateLimiter<string> perName) =>
    {
        // Names compare as the verifier store matches them (case-insensitive); an impossible name shares one bucket.
        using var attempt = perName.AttemptAcquire(input.UserName?.Trim().ToUpperInvariant() is { Length: > 0 and <= 64 } name ? name : "");
        if (!attempt.IsAcquired)
        {
            var after = attempt.TryGetMetadata(MetadataName.RetryAfter, out var wait) ? (int)Math.Ceiling(wait.TotalSeconds) : 60;
            ctx.Response.Headers.RetryAfter = after.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }
        string? stamp = null;
        var id = input.UserName is { Length: > 0 } && input.Password is { Length: > 0 } ? store.Verify(input.UserName, input.Password, out stamp) : null;
        if (id is null || !await db.Users.AnyAsync(u => u.Id == id && u.IsActive)) return Results.Unauthorized();
        var claims = new[] { new Claim("oid", $"local:{id}"), new Claim("local_user_id", id.ToString()!), new Claim(AuthSetup.StampClaim, stamp!) };
        await ctx.SignInAsync(AuthSetup.LocalScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSetup.LocalScheme)));
        return Results.NoContent();
    }).AllowAnonymous().RequireRateLimiting("local-sign-in");
    api.MapPost("/auth/local/sign-out", async (HttpContext ctx) => { await ctx.SignOutAsync(AuthSetup.LocalScheme); return Results.NoContent(); });
}
if (app.Environment.IsDevelopment()) app.MapOpenApi("/api/v1/openapi.json").AllowAnonymous();
else app.MapOpenApi("/api/v1/openapi.json");
MeEndpoints.Map(api);
AdminEndpoints.Map(api);
ActivityEndpoints.Map(api);
HubModules.Map(api);
app.MapFallback("/api/{**rest}", (HttpContext _) => throw ApiException.NotFound()); // §25.6: an unknown API path is a 404 problem, not the SPA page
app.MapFallbackToFile("index.html").AllowAnonymous();

// Development-only sign-in list: lets the SPA's user picker work without Entra (never mapped in production).
if (AuthSetup.DevAuthAllowed(app.Environment, cfg))
    app.MapGet("/api/dev/users", async (HubDb db) => await db.Users.Where(u => u.IsActive).OrderBy(u => u.DisplayName)
        .Select(u => new { u.Email, u.DisplayName, u.JobTitle, Roles = u.Roles.Select(r => r.Role) }).ToListAsync()).AllowAnonymous();

var reviewDemo = cfg.GetValue<bool>("Seed:ReviewDemo");
if (reviewDemo && !(app.Environment.IsDevelopment() || app.Environment.IsStaging() || app.Environment.IsEnvironment("Testing")))
    throw new InvalidOperationException("Seed:ReviewDemo is permitted only in Development, Staging or Testing.");

if (cfg["Db:Migrate"] != "false")
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<HubDb>();
    await db.Database.MigrateAsync();
    await Seed.Reference(db);
    if (AuthSetup.DevAuthAllowed(app.Environment, cfg) && cfg["Seed:DevUsers"] != "false")
    {
        await Seed.DevUsers(db);
        await ReferenceTemplate.Seed(db); // Appendix A, for trying the template wizard
    }
    if (reviewDemo)
        await ReviewDemoSeed.Seed(db, scope.ServiceProvider.GetRequiredService<TimeProvider>());
    await AuthSetup.BootstrapAdmins(db, cfg, app.Environment, app.Logger);
}

app.Run();

public partial class Program;
public sealed record LocalSignIn(string UserName, string Password);
