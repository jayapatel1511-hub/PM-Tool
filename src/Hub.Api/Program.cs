using System.Threading.RateLimiting;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
        RateLimitPartition.GetFixedWindowLimiter(ctx.User.FindFirst("oid")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = int.TryParse(cfg["RateLimit:PerMinute"], out var n) ? n : 600, Window = TimeSpan.FromMinutes(1) }));
});
builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var app = builder.Build();

app.UseMiddleware<ProblemMiddleware>();
app.UseMiddleware<SecurityHeaders>();
if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
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
}

app.Run();

public partial class Program;
