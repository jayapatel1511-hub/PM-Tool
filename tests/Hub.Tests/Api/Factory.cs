using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace Hub.Tests.Api;

/// A settable clock so date rules can be exercised ("today" is controlled by the test).
public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 9, 14, 13, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
    public void SetDate(int y, int m, int d) => Now = new DateTimeOffset(y, m, d, 13, 0, 0, TimeSpan.Zero);
}

/// One PostgreSQL database per test run, migrated once; tests create their own uniquely named data.
public sealed class HubFactory : WebApplicationFactory<Program>
{
    public bool ReviewDemo { get; set; }
    /// Host settings applied after the defaults, for example local-password sign-in.
    public Dictionary<string, string> Settings { get; } = [];
    public string Database { get; } = "hub_test_" + Guid.NewGuid().ToString("N")[..8];
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder b)
    {
        using (var c = new NpgsqlConnection("Host=localhost;Port=55432;Database=hub;Username=hub"))
        {
            c.Open();
            using var cmd = new NpgsqlCommand($"CREATE DATABASE {Database}", c);
            try { cmd.ExecuteNonQuery(); } catch (PostgresException e) when (e.SqlState == "42P04") { }
        }
        b.UseEnvironment("Testing");
        b.UseSetting("ConnectionStrings:Hub", $"Host=localhost;Port=55432;Database={Database};Username=hub;Include Error Detail=true");
        b.UseSetting("Auth:Mode", "Development");
        b.UseSetting("Seed:ReviewDemo", ReviewDemo.ToString());
        b.UseSetting("Jobs:Enabled", "false");
        b.UseSetting("Evaluation:Worker", "false");
        b.UseSetting("RateLimit:PerMinute", "100000");
        foreach (var (key, value) in Settings) b.UseSetting(key, value);
        b.ConfigureServices(s =>
        {
            s.RemoveAll<TimeProvider>();
            s.AddSingleton<TimeProvider>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        NpgsqlConnection.ClearAllPools();
        using var c = new NpgsqlConnection("Host=localhost;Port=55432;Database=hub;Username=hub");
        c.Open();
        using var cmd = new NpgsqlCommand($"DROP DATABASE IF EXISTS {Database} WITH (FORCE)", c);
        cmd.ExecuteNonQuery();
    }

    /// Runs the project's evaluation synchronously (the background worker is off in tests).
    public async Task Evaluate(Guid projectId)
    {
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<Hub.Api.Infrastructure.AuditContext>().AsSystem();
        await scope.ServiceProvider.GetRequiredService<Hub.Api.Infrastructure.EvaluationService>().EvaluateProject(projectId);
    }

    public async Task RunJob<T>() where T : Hub.Api.Infrastructure.IJob
    {
        var host = Services.GetRequiredService<Hub.Api.Infrastructure.JobHost>();
        var job = Services.GetServices<Hub.Api.Infrastructure.IJob>().OfType<T>().Single();
        Assert.True(await host.RunIfDue(job, force: true, CancellationToken.None));
    }

    public HttpClient As(string email, string? appRoles = null)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Add("X-Dev-User", email);
        if (appRoles is not null) c.DefaultRequestHeaders.Add("X-Dev-Roles", appRoles);
        return c;
    }

    public T Db<T>(Func<HubDb, T> f)
    {
        using var scope = Services.CreateScope();
        return f(scope.ServiceProvider.GetRequiredService<HubDb>());
    }

    public async Task<T> DbAsync<T>(Func<HubDb, Task<T>> f)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HubDb>();
        db.Audit.AsSystem("Migration");
        return await f(db);
    }
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<HubFactory>;

public static class HttpJson
{
    static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static async Task<JsonNode> Json(this HttpResponseMessage r, int expect = 200)
    {
        var body = await r.Content.ReadAsStringAsync();
        Assert.True((int)r.StatusCode == expect, $"Expected {expect} but got {(int)r.StatusCode}: {body}");
        return string.IsNullOrEmpty(body) ? new JsonObject() : JsonNode.Parse(body)!;
    }

    public static Task<HttpResponseMessage> Post(this HttpClient c, string url, object body) => c.PostAsJsonAsync(url, body, Web);
    public static Task<HttpResponseMessage> Put(this HttpClient c, string url, object body) => c.PutAsJsonAsync(url, body, Web);

    public static Task<HttpResponseMessage> Patch(this HttpClient c, string url, object body, int? rowVersion = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch, url) { Content = JsonContent.Create(body, options: Web) };
        if (rowVersion is { } v) req.Headers.TryAddWithoutValidation("If-Match", $"\"{v}\"");
        return c.SendAsync(req);
    }

    public static Task<HttpResponseMessage> Action(this HttpClient c, string url, object body, int? rowVersion)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body, options: Web) };
        if (rowVersion is { } v) req.Headers.TryAddWithoutValidation("If-Match", $"\"{v}\"");
        return c.SendAsync(req);
    }

    public static string S(this JsonNode n, string p) => n[p]!.GetValue<string>();
    public static int I(this JsonNode n, string p) => n[p]!.GetValue<int>();
    public static Guid G(this JsonNode n, string p) => Guid.Parse(n[p]!.GetValue<string>());
}
