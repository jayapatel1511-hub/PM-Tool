using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Hub.Api.Infrastructure;

/// A refusal with an RFC 9457 problem body (§25.6). Messages come from Text.cs so they can be translated.
public sealed class ApiException(int status, string code, string message, IDictionary<string, string[]>? errors = null, object? extra = null)
    : Exception(message)
{
    public int Status => status;
    public string Code => code;
    public IDictionary<string, string[]>? Errors => errors;
    public object? Extra => extra;

    public static ApiException NotFound() => new(404, "not_found", Text.Get("error.not_found"));
    public static ApiException Forbidden(string messageKey, params object[] args) => new(403, "forbidden", Text.Get(messageKey, args));
    public static ApiException Invalid(string field, string messageKey, params object[] args) =>
        new(400, "validation", Text.Get("error.validation"), new Dictionary<string, string[]> { [field] = [Text.Get(messageKey, args)] });
    public static ApiException Rule(string code, string messageKey, object? extra = null, params object[] args) => new(422, code, Text.Get(messageKey, args), null, extra);
    public static ApiException Conflict(string code, string messageKey, object? extra = null, params object[] args) => new(409, code, Text.Get(messageKey, args), null, extra);
}

public static class Check
{
    public static void That(bool ok, string field, string messageKey, params object[] args) { if (!ok) throw ApiException.Invalid(field, messageKey, args); }
    public static string Required(string? v, string field, int max = 200)
    {
        var s = v?.Trim();
        That(!string.IsNullOrEmpty(s), field, "error.required");
        That(s!.Length <= max, field, "error.too_long", max);
        return s;
    }
    public static string? Optional(string? v, string field, int max = 4000)
    {
        var s = string.IsNullOrWhiteSpace(v) ? null : v.Trim();
        That(s is null || s.Length <= max, field, "error.too_long", max);
        return s;
    }
    /// G-09: reasons are free text of at least 5 characters.
    public static string Reason(string? v, string field = "reason")
    {
        var s = v?.Trim() ?? "";
        That(s.Length >= 5, field, "error.reason_required");
        return s;
    }
    public static void OneOf(string? v, string[] allowed, string field) => That(v is not null && allowed.Contains(v), field, "error.one_of", string.Join(", ", allowed));
}

public sealed class ProblemMiddleware(RequestDelegate next, ILogger<ProblemMiddleware> log, IHostEnvironment env)
{
    public async Task Invoke(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (ApiException e) { await Write(ctx, e.Status, e.Code, e.Message, e.Errors, e.Extra); }
        catch (DbUpdateConcurrencyException) { await Write(ctx, 409, "concurrency_conflict", Text.Get("error.concurrency"), null, null); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" })
        { await Write(ctx, 409, "duplicate", Text.Get("error.duplicate"), null, null); }
        catch (BadHttpRequestException e) { await Write(ctx, 400, "bad_request", e.Message, null, null); }
        catch (JsonException) { await Write(ctx, 400, "bad_request", Text.Get("error.bad_json"), null, null); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { }
        catch (Exception e)
        {
            // No internals leak; traceId lets support find the log entry.
            log.LogError(e, "Unhandled error {TraceId}", ctx.TraceIdentifier);
            // Development and test runs show the exception to speed diagnosis; production never does.
            var debug = env.IsDevelopment() || env.IsEnvironment("Testing") ? new { exception = e.ToString()[..Math.Min(e.ToString().Length, 3000)] } : null;
            await Write(ctx, 500, "server_error", Text.Get("error.server"), null, debug);
        }
    }

    static async Task Write(HttpContext ctx, int status, string code, string detail, IDictionary<string, string[]>? errors, object? extra)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var body = new Dictionary<string, object?>
        {
            ["type"] = $"https://hub.local/errors/{code}",
            ["title"] = status switch { 400 => "Validation failed", 401 => "Unauthorized", 403 => "Forbidden", 404 => "Not found", 409 => "Conflict", 422 => "Business rule", _ => "Error" },
            ["status"] = status,
            ["code"] = code,
            ["detail"] = detail,
            ["traceId"] = ctx.TraceIdentifier,
        };
        if (errors is not null) body["errors"] = errors;
        if (extra is not null) foreach (var p in JsonSerializer.SerializeToElement(extra, JsonOpts.Web).EnumerateObject()) body[p.Name] = p.Value;
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body, JsonOpts.Web));
    }
}

public static class JsonOpts { public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web); }
