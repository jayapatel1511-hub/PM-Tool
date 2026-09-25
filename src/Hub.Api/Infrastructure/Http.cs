using System.Text.Json;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public static class Http
{
    /// G-07 / §25.8: the version the client read, from If-Match or the body.
    public static int? IfMatch(HttpContext ctx, int? bodyVersion = null)
    {
        var h = ctx.Request.Headers.IfMatch.FirstOrDefault()?.Trim('"', ' ', 'W', '/');
        return int.TryParse(h, out var v) ? v : bodyVersion;
    }

    /// Refuses a stale write with who changed the item and when (AC-TSK-12).
    public static async Task CheckVersion(HubDb db, HttpContext ctx, Audited entity, int? bodyVersion = null)
    {
        var v = IfMatch(ctx, bodyVersion) ?? throw new ApiException(428, "precondition_required", Text.Get("error.if_match"));
        if (v == entity.RowVersion) return;
        var by = entity.UpdatedBy is { } uid ? await db.Users.Where(u => u.Id == uid).Select(u => u.DisplayName).FirstOrDefaultAsync() : null;
        throw new ApiException(409, "concurrency_conflict", Text.Get("error.concurrency_by", by ?? "someone", entity.UpdatedAt.ToString("u")),
            extra: new { changedBy = by, changedAt = entity.UpdatedAt, currentRowVersion = entity.RowVersion });
    }

    public static void ETag(HttpContext ctx, Audited e) => ctx.Response.Headers.ETag = $"\"{e.RowVersion}\"";

    /// Paging envelope (§25.3); page size capped at 200.
    public static (int Page, int Size) Paging(int? page, int? pageSize) => (Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 50, 1, 200));

    public static string[] List(string? csv) => string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static Guid[] Ids(string? csv) => List(csv).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToArray();
}

public sealed record Page<T>(IReadOnlyList<T> Items, [property: System.Text.Json.Serialization.JsonPropertyName("page")] int PageNumber, int PageSize, int TotalCount, string? Sort = null, object? Filters = null);

/// Security headers (§21): CSP, no sniffing, referrer policy, framing denied.
public sealed class SecurityHeaders(RequestDelegate next, IConfiguration cfg)
{
    readonly string csp = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; "
        + $"connect-src 'self' https://login.microsoftonline.com {cfg["Csp:ConnectSrc"]}; frame-src https://login.microsoftonline.com; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

    public Task Invoke(HttpContext ctx)
    {
        var h = ctx.Response.Headers;
        h["X-Content-Type-Options"] = "nosniff";
        h["Referrer-Policy"] = "strict-origin-when-cross-origin";
        h["X-Frame-Options"] = "DENY";
        h["Content-Security-Policy"] = csp;
        if (ctx.Request.Path.StartsWithSegments("/api")) h.CacheControl = "no-store";
        return next(ctx);
    }
}

/// Organisation settings cached for 30 seconds and refreshed immediately after an Admin change.
public sealed class SettingsStore(TimeProvider clock)
{
    OrgSettings? cached;
    DateTimeOffset loadedAt;

    public async Task<OrgSettings> Get(HubDb db)
    {
        if (cached is not null && clock.GetUtcNow() - loadedAt < TimeSpan.FromSeconds(30)) return cached;
        var rows = await db.Settings.AsNoTracking().ToListAsync();
        cached = OrgSettings.From(rows.ToDictionary(r => r.Key, r => JsonDocument.Parse(r.Value).RootElement.Clone()));
        loadedAt = clock.GetUtcNow();
        return cached;
    }

    public void Invalidate() => cached = null;
}

public static class Clock
{
    /// G-03: "today" is the calendar date in the organisation time zone.
    public static DateOnly Today(this TimeProvider clock, OrgSettings s) => DateOnly.FromDateTime(Local(clock.GetUtcNow(), s).DateTime);

    public static DateTimeOffset Local(DateTimeOffset utc, OrgSettings s) => TimeZoneInfo.ConvertTime(utc, Zone(s));

    public static TimeZoneInfo Zone(OrgSettings s)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(s.OrgTimeZone); } catch { return TimeZoneInfo.Utc; }
    }

    public static DateOnly LocalDate(DateTimeOffset utc, OrgSettings s) => DateOnly.FromDateTime(Local(utc, s).DateTime);
}

/// JSON merge-patch reader (§25.1 PATCH semantics): a property that is present is applied, even when null.
public sealed class Patch(JsonElement e)
{
    public JsonElement Raw => e;
    public bool Has(string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out _);
    JsonElement? Get(string n) => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    public string? Str(string n) => Get(n) is { } v ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()) : null;
    public bool? Bool(string n) => Get(n) is { } v && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;
    public int? Int(string n) => Get(n) is { } v && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i
        : Get(n) is { ValueKind: not JsonValueKind.Number } ? throw ApiException.Invalid(n, "error.positive") : null;
    public decimal? Dec(string n) => Get(n) is { } v ? (v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : throw ApiException.Invalid(n, "error.positive")) : null;
    public Guid? Id(string n) => Get(n) is { } v ? (Guid.TryParse(v.GetString(), out var g) ? g : throw ApiException.Invalid(n, "error.not_found")) : null;
    public DateOnly? Date(string n) => Get(n) is { } v ? (DateOnly.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : throw ApiException.Invalid(n, "error.date")) : null;
    public DateTimeOffset? Time(string n) => Get(n) is { } v ? (DateTimeOffset.TryParse(v.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var d) ? d.ToUniversalTime() : throw ApiException.Invalid(n, "error.date")) : null;
    public string[]? Strs(string n) => Get(n) is { ValueKind: JsonValueKind.Array } v ? v.EnumerateArray().Select(x => x.GetString() ?? "").ToArray() : null;
    public Guid[]? Ids(string n) => Get(n) is { ValueKind: JsonValueKind.Array } v ? v.EnumerateArray().Select(x => Guid.Parse(x.GetString()!)).ToArray() : null;
    public int? RowVersion => Int("rowVersion");
}

public static class J
{
    /// jsonb text as a JSON value for responses; null stays null (a default JsonElement cannot be serialised).
    public static JsonElement? El(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();
}
