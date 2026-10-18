using System.Net;
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class OperationsHardeningTests(HubFactory f)
{
    [Trait("Spec", "AC-OPS-07")]
    [Fact] // AC-OPS-07: Admin retry resets only dead letters and a PM cannot retry.
    public async Task Admin_retry_returns_dead_letters_to_the_worker()
    {
        var at = f.Clock.GetUtcNow();
        var ids = await f.DbAsync(async db =>
        {
            var a = new OutboxEvent { EventType = "Synthetic", Payload = "{}", Attempts = 10, DeadLetteredAt = at };
            var b = new OutboxEvent { EventType = "Synthetic", Payload = "{}", Attempts = 10, DeadLetteredAt = at.AddMinutes(-1) };
            db.Outbox.AddRange(a, b); await db.SaveChangesAsync(); return new[] { a.Id, b.Id };
        });
        var pm = f.As("priya@hub.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await pm.Post("/api/v1/admin/operations/outbox/retry", new { })).StatusCode);
        var admin = f.As(TestData.Admin);
        var ops = await (await admin.GetAsync("/api/v1/admin/operations")).Json();
        Assert.True(ops.I("deadLetterCount") >= 2);
        Assert.True(ops["deadLetters"]!.AsArray().Count <= 200);
        await (await admin.Post($"/api/v1/admin/operations/outbox/retry?id={ids[0]}", new { })).Json();
        await f.DbAsync(async db =>
        {
            var rows = await db.Outbox.Where(o => ids.Contains(o.Id)).OrderBy(o => o.Id).ToListAsync();
            var retried = rows.Single(o => o.Id == ids[0]);
            Assert.Equal(0, retried.Attempts); Assert.Null(retried.DeadLetteredAt);
            Assert.NotNull(rows.Single(o => o.Id == ids[1]).DeadLetteredAt);
            return true;
        });
        await admin.Post("/api/v1/admin/operations/outbox/retry", new { });
        Assert.Equal(0, await f.DbAsync(db => db.Outbox.CountAsync(o => ids.Contains(o.Id) && o.DeadLetteredAt != null)));
    }

    [Trait("Spec", "AC-OPS-21")]
    [Fact] // AC-OPS-21: preferences are per account and /me reflects the saved choice.
    public async Task Density_choice_is_saved_to_the_account()
    {
        var person = f.As("alex@hub.test");
        foreach (var dense in new[] { false, true })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await person.Put("/api/v1/me/preferences/density", new { denseRows = dense })).StatusCode);
            var me = await (await person.GetAsync("/api/v1/me")).Json();
            Assert.Equal(dense, me["preferences"]!["denseRows"]!.GetValue<bool>());
        }
    }

    [Trait("Spec", "AC-OPS-10")]
    [Fact] // AC-OPS-10: bounded authenticated log-only endpoint, 10 reports per user per minute.
    public async Task Client_errors_are_bounded_authenticated_rate_limited_and_write_no_rows()
    {
        using var host = new HubFactory();
        var admin = host.As(TestData.Admin);
        var logs = new ErrorLogs();
        host.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        var before = await host.DbAsync(db => db.ActivityLog.CountAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.CreateClient().Post("/api/v1/client-errors", new { reference = "TEST00" })).StatusCode);
        var payload = new { reference = "TEST00", route = "synthetic/:id", build = "dev", kind = "render", name = "Error", stackFrame = "app.js:1:2", message = "synthetic\nerror" };
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.NoContent, (await admin.Post("/api/v1/client-errors", payload)).StatusCode);
        Assert.Equal(10, logs.Messages.Count);
        Assert.All(logs.Messages, line =>
        {
            Assert.Contains("TEST00", line); Assert.True(line.Contains("name=Error"), line); Assert.Contains("app.js:1:2", line);
            Assert.DoesNotContain(line, char.IsControl); Assert.DoesNotContain(TestData.Admin, line);
        });
        var limited = await admin.Post("/api/v1/client-errors", payload);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.NotNull(limited.Headers.RetryAfter);
        // A separate identity retains its own allowance.
        Assert.Equal(HttpStatusCode.NoContent, (await host.As("alex@hub.test").Post("/api/v1/client-errors", payload)).StatusCode);
        Assert.Equal(before, await host.DbAsync(db => db.ActivityLog.CountAsync()));
    }

    [Trait("Spec", "AC-OPS-10")]
    [Fact] // AC-OPS-10: a chunked body cannot bypass the 8 KB cap.
    public async Task Oversized_client_errors_are_refused_before_deserialization()
    {
        var admin = f.As(TestData.Admin);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/client-errors", new ChunkedBody())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/v1/client-errors", new StringContent(new string('x', 9000), Encoding.UTF8, "application/json"))).StatusCode);
    }

    sealed class ErrorLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string category) => new ErrorLogger(category == "Hub.ClientErrors" ? Messages : null);
        public void Dispose() { }
        sealed class ErrorLogger(ConcurrentQueue<string>? target) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel level) => target is not null;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? error, Func<TState, Exception?, string> formatter) => target?.Enqueue(formatter(state, error));
        }
    }

    sealed class ChunkedBody : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(new byte[8193]).AsTask();
    }
}
