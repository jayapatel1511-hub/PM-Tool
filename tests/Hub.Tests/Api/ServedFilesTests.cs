namespace Hub.Tests.Api;

/// AC-OPS-01, AC-OPS-02: build metadata, including Vite's private manifest, is never served.
public sealed class ServedFilesTests
{
    [Fact]
    public async Task Dot_prefixed_build_files_are_never_served()
    {
        var root = Directory.CreateTempSubdirectory("hub-private-assets-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".vite"));
            File.WriteAllText(Path.Combine(root, "index.html"), "<!doctype html><title>Tuesday</title>");
            File.WriteAllText(Path.Combine(root, ".vite", "manifest.json"), "{\"private\":true}");
            using var f = new HubFactory { Settings = { ["webroot"] = root } };
            var c = f.As(TestData.Admin);
            foreach (var path in new[] { "/.vite/manifest.json", "/landing/capture.json", "/landing/README.md", "/landing/fonts/sources.json" })
                Assert.Equal(System.Net.HttpStatusCode.NotFound, (await c.GetAsync(path)).StatusCode);
        }
        finally { Directory.Delete(root, true); }
    }
}
