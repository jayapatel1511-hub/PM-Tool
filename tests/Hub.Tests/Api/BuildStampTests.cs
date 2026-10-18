using System.Net;
using System.Text.Json;

namespace Hub.Tests.Api;

/// FR-LIVE-01, FR-LIVE-02, AC-LIVE-01: every response carries X-Hub-Build; /health adds build and is never cached.
public sealed class BuildStampTests
{
    static async Task AssertStamped(HubFactory f, string build)
    {
        var c = f.As(TestData.Admin);

        var api = await c.GetAsync("/api/v1/me");
        Assert.True(api.IsSuccessStatusCode);
        Assert.Equal(build, api.Headers.GetValues("X-Hub-Build").Single());

        var file = await c.GetAsync("/favicon.svg");
        Assert.True(file.IsSuccessStatusCode);
        Assert.Equal(build, file.Headers.GetValues("X-Hub-Build").Single());

        var missing = await c.GetAsync("/api/v1/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);
        Assert.Equal(build, missing.Headers.GetValues("X-Hub-Build").Single());

        var health = await f.CreateClient().GetAsync("/health");
        Assert.True(health.IsSuccessStatusCode);
        Assert.Equal(build, health.Headers.GetValues("X-Hub-Build").Single());
        Assert.Equal("no-store", health.Headers.CacheControl?.ToString());
        using var doc = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal(build, doc.RootElement.GetProperty("build").GetString());
    }

    static async Task WithWebRoot(Func<string, Task> body)
    {
        var root = Directory.CreateTempSubdirectory("hub-webroot-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><title>Hub</title>");
            File.WriteAllText(Path.Combine(root, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            await body(root);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public Task Without_configuration_the_build_is_dev() => WithWebRoot(async root =>
    {
        using var f = new HubFactory { Settings = { ["webroot"] = root } };
        await AssertStamped(f, "dev");
    });

    [Fact]
    public Task The_configured_build_is_sent_everywhere() => WithWebRoot(async root =>
    {
        const string sha = "0123456789abcdef0123456789abcdef01234567";
        using var f = new HubFactory { Settings = { ["webroot"] = root, ["HUB_BUILD"] = sha } };
        await AssertStamped(f, sha);
    });
}
