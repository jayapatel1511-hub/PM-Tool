using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Hub.Tests.Api;

/// Test-only response recorder. The tee forwards writes immediately, including event streams (§44.4).
public sealed class ShapeRecorder : IStartupFilter
{
    static readonly string Run = Guid.NewGuid().ToString("N");
    static readonly object WriteLock = new();
    readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>> shapes = new();
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, run) =>
        {
            var original = context.Response.Body;
            using var tee = new ResponseTee(original);
            context.Response.Body = tee;
            try { await run(); }
            finally
            {
                context.Response.Body = original;
                if (context.GetEndpoint() is RouteEndpoint endpoint && context.Response.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    try
                    {
                        using var json = JsonDocument.Parse(tee.Captured.ToArray());
                        var route = Regex.Replace(endpoint.RoutePattern.RawText ?? "", @"\{([^}:]+):[^}]+\}", "{$1}");
                        var key = $"{context.Request.Method} {route} {context.Response.StatusCode}";
                        var paths = shapes.GetOrAdd(key, _ => new());
                        Visit(json.RootElement, "$", paths);
                    }
                    catch (JsonException) { /* incomplete/empty responses have no JSON shape */ }
                }
            }
        });
        next(app);
    };

    internal static void Visit(JsonElement value, string path, ConcurrentDictionary<string, ConcurrentDictionary<string, byte>> paths)
    {
        var kind = value.ValueKind switch
        {
            JsonValueKind.Object => "object", JsonValueKind.Array => "array", JsonValueKind.String => "string",
            JsonValueKind.Number => "number", JsonValueKind.True or JsonValueKind.False => "boolean", _ => "null",
        };
        paths.GetOrAdd(path, _ => new())[kind] = 0;
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject()) Visit(property.Value, path + "." + (Guid.TryParseExact(property.Name, "D", out _) ? "{id}" : property.Name), paths);
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) Visit(item, path + "[]", paths);
    }

    public void Write(string name)
    {
        var root = Path.Combine(ContractTests.Root, "ci-artifacts/shapes");
        Directory.CreateDirectory(root);
        lock (WriteLock) File.WriteAllText(Path.Combine(root, "latest.json"), JsonSerializer.Serialize(new { run = Run }));
        root = Path.Combine(root, Run);
        Directory.CreateDirectory(root);
        var sorted = shapes.OrderBy(p => p.Key, StringComparer.Ordinal).ToDictionary(p => p.Key,
            p => p.Value.OrderBy(v => v.Key, StringComparer.Ordinal).ToDictionary(v => v.Key, v => v.Value.Keys.Order(StringComparer.Ordinal).ToArray()));
        File.WriteAllText(Path.Combine(root, name + ".json"), JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    sealed class ResponseTee(Stream target) : Stream
    {
        public MemoryStream Captured { get; } = new();
        const int Limit = 16 * 1024 * 1024;
        void Capture(ReadOnlySpan<byte> bytes) { if (Captured.Length + bytes.Length <= Limit) Captured.Write(bytes); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => target.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => target.FlushAsync(cancellationToken);
        public override void Write(byte[] buffer, int offset, int count) { Capture(buffer.AsSpan(offset, count)); target.Write(buffer, offset, count); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { Capture(buffer.Span); await target.WriteAsync(buffer, cancellationToken); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) Captured.Dispose(); base.Dispose(disposing); }
    }
}
