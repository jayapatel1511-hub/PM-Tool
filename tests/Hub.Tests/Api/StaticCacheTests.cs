namespace Hub.Tests.Api;

/// FR-LIVE-10, AC-LIVE-09: immutable hashed assets, no-cache index.html, other static files and the API unchanged.
public sealed class StaticCacheTests
{
    [Fact]
    public async Task Assets_are_immutable_and_index_is_never_cached()
    {
        var root = Directory.CreateTempSubdirectory("hub-webroot-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "assets"));
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><title>Hub</title>");
            File.WriteAllText(Path.Combine(root, "assets", "app-3f9a1c2b.js"), "console.log(1)");
            File.WriteAllText(Path.Combine(root, "favicon.svg"), "<svg xmlns=\"http://www.w3.org/2000/svg\"/>");
            using var f = new HubFactory { Settings = { ["webroot"] = root } };
            var c = f.As(TestData.Admin);

            async Task<string?> CacheControl(string url, string? body = null)
            {
                var r = await c.GetAsync(url);
                Assert.True(r.IsSuccessStatusCode, $"{url} answered {(int)r.StatusCode}");
                if (body is not null) Assert.Contains(body, await r.Content.ReadAsStringAsync());
                return r.Headers.CacheControl?.ToString();
            }

            Assert.Equal("public, max-age=31536000, immutable", await CacheControl("/assets/app-3f9a1c2b.js", "console.log"));
            Assert.Equal("no-cache", await CacheControl("/", "<title>Hub</title>"));
            Assert.Equal("no-cache", await CacheControl("/index.html", "<title>Hub</title>"));
            Assert.Equal("no-cache", await CacheControl("/projects/P-1/tasks", "<title>Hub</title>"));
            Assert.Null(await CacheControl("/favicon.svg", "<svg"));
            Assert.Equal("no-store", await CacheControl("/api/v1/me"));
        }
        finally { Directory.Delete(root, true); }
    }
}
