using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S29 fixed recorded facts. Callers provide already-authorised people; dates never change eligibility or capacity.
public static class PersonFacts
{
    public sealed record Notices(string? PersonType, DateOnly? StartDate, DateOnly? EndDate, bool Starts, bool Ends);
    public sealed record CertificationFact(Guid Id, Guid TagId, string TagName, bool TagActive, DateOnly? ExpiresOn, string ExpiryState, int? DaysToExpiry, int RowVersion);
    public sealed record Facts(Notices Notices, CertificationFact[] Certifications);
    public sealed record CertificationChoice(Guid Id, string Name, bool IsActive);
    public sealed record EditorRead(Guid UserId, int RowVersion, bool CanEdit, Facts Facts, CertificationChoice[] Choices, DateOnly Today);
    public sealed record Saved(Guid Id, int RowVersion);
    public sealed record CertificationCreateGuard(bool Admin);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateBody(Guid CertificationTagId, DateOnly? ExpiresOn, int? RowVersion, string? Reason);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RemoveBody(string? Reason, int? RowVersion);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/certification-tags", async Task<Ok<CertificationChoice[]>> (HubDb db, SettingsStore store) =>
        { await Enabled(db, store); return TypedResults.Ok(await Choices(db)); });
        api.MapGet("/me/certifications", (Access access, HubDb db, SettingsStore store, TimeProvider clock) => Editor(access.Me.Id, false, access, db, store, clock));
        api.MapGet("/admin/users/{userId:guid}/certifications", (Guid userId, Access access, HubDb db, SettingsStore store, TimeProvider clock) => Editor(userId, true, access, db, store, clock));
        api.MapPost("/me/certifications", (CreateBody body, HttpContext http, Access access, HubDb db, SettingsStore store) => Create(access.Me.Id, false, body, http, access, db, store)).WithMetadata(new CertificationCreateGuard(false));
        api.MapPost("/admin/users/{userId:guid}/certifications", (Guid userId, CreateBody body, HttpContext http, Access access, HubDb db, SettingsStore store) => Create(userId, true, body, http, access, db, store)).WithMetadata(new CertificationCreateGuard(true));
        api.MapPatch("/me/certifications/{id:guid}", (Guid id, JsonElement body, HttpContext http, Access access, HubDb db, SettingsStore store) => Edit(access.Me.Id, false, id, body, http, access, db, store));
        api.MapPatch("/admin/users/{userId:guid}/certifications/{id:guid}", (Guid userId, Guid id, JsonElement body, HttpContext http, Access access, HubDb db, SettingsStore store) => Edit(userId, true, id, body, http, access, db, store));
        api.MapDelete("/me/certifications/{id:guid}", (Guid id, [Microsoft.AspNetCore.Mvc.FromBody] RemoveBody body, HttpContext http, Access access, HubDb db, SettingsStore store) => Remove(access.Me.Id, false, id, body, http, access, db, store));
        api.MapDelete("/admin/users/{userId:guid}/certifications/{id:guid}", (Guid userId, Guid id, [Microsoft.AspNetCore.Mvc.FromBody] RemoveBody body, HttpContext http, Access access, HubDb db, SettingsStore store) => Remove(userId, true, id, body, http, access, db, store));
        api.MapPatch("/admin/users/{userId:guid}/person-facts", SetFacts);
    }

    static async Task<OrgSettings> Enabled(HubDb db, SettingsStore store)
    { var settings = await store.Get(db); if (!settings.IsFlagOn("capacity_screens")) throw ApiException.NotFound(); return settings; }
    /// Admin routes answer 403 to non-Admins whatever the flag; only an Admin sees the flagged-off 404 (Jay, 2026-10-25).
    static Task<OrgSettings> Enabled(HubDb db, SettingsStore store, Access access, bool admin)
    { if (admin) Access.Demand(Permissions.Administer(access.Actor)); return Enabled(db, store); }
    /// Root's existing idempotency middleware calls this only for the two marked creates before claiming/replaying.
    /// Reuse its receipt machinery, but a replay must still obey today's flag and authority without returning stale facts.
    public static async Task AuthoriseCreateReplay(HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        if (http.GetEndpoint()?.Metadata.GetMetadata<CertificationCreateGuard>() is not { } guard) return;
        await Enabled(db, store, access, guard.Admin);
        var userId = access.Me.Id;
        if (guard.Admin && !Guid.TryParse(http.Request.RouteValues["userId"]?.ToString(), out userId)) throw ApiException.NotFound();
        var user = await Target(userId, guard.Admin, false, access, db);
        if (!guard.Admin) Access.Demand(user.IsActive && !access.Me.IsReadOnly ? Allow.Yes : Allow.No(user.IsActive ? "perm.read_only" : "personFacts.inactive"));
    }
    static Task<CertificationChoice[]> Choices(HubDb db) => db.CertificationTags.AsNoTracking().OrderBy(tag => tag.SortOrder).ThenBy(tag => tag.Name).ThenBy(tag => tag.Id)
        .Select(tag => new CertificationChoice(tag.Id, tag.Name, tag.IsActive)).ToArrayAsync();
    static Notices Notice(string? type, DateOnly? start, DateOnly? end, DateOnly today, int window)
        => new(type, start, end, start > today, end is { } date && date >= today && date.DayNumber - today.DayNumber <= window);
    static async Task<Dictionary<Guid, Notices>> LoadNotices(HubDb db, Guid[] ids, DateOnly today, int window)
    {
        var users = await db.Users.AsNoTracking().Where(user => ids.Contains(user.Id)).Select(user => new NoticeSource(user.Id, user.PersonType, user.PersonStartDate, user.PersonEndDate)).ToArrayAsync();
        return users.ToDictionary(user => user.Id, user => Notice(user.PersonType, user.PersonStartDate, user.PersonEndDate, today, window));
    }
    public static async Task<Dictionary<Guid, Notices>> ReadNotices(HubDb db, Guid[] authorisedIds, OrgSettings settings, TimeProvider clock)
        => settings.IsFlagOn("capacity_screens") ? await LoadNotices(db, authorisedIds.Distinct().ToArray(), clock.Today(settings), settings.AvailabilityWindowDays) : [];
    public static async Task<Dictionary<Guid, Notices>> ReadNotices(HubDb db, Guid[] authorisedIds, OrgSettings settings, DateOnly today)
        => settings.IsFlagOn("capacity_screens") ? await LoadNotices(db, authorisedIds.Distinct().ToArray(), today, settings.AvailabilityWindowDays) : [];
    public static async Task<Dictionary<Guid, Facts>> Read(HubDb db, Guid[] authorisedIds, OrgSettings settings, TimeProvider clock)
    {
        if (!settings.IsFlagOn("capacity_screens")) return [];
        return await ReadAt(db, authorisedIds, settings, clock.Today(settings));
    }
    public static async Task<Dictionary<Guid, Facts>> Read(HubDb db, Guid[] authorisedIds, OrgSettings settings, DateOnly today)
        => settings.IsFlagOn("capacity_screens") ? await ReadAt(db, authorisedIds, settings, today) : [];
    static async Task<Dictionary<Guid, Facts>> ReadAt(HubDb db, Guid[] authorisedIds, OrgSettings settings, DateOnly today)
    {
        var ids = authorisedIds.Distinct().ToArray();
        var notices = await LoadNotices(db, ids, today, settings.AvailabilityWindowDays);
        var certifications = await (from record in db.UserCertifications.AsNoTracking()
            join tag in db.CertificationTags.AsNoTracking() on record.CertificationTagId equals tag.Id
            where ids.Contains(record.UserId)
            orderby tag.SortOrder, tag.Name, record.Id
            select new CertificationRow(record.Id, record.UserId, record.CertificationTagId, tag.Name, tag.IsActive, record.ExpiresOn, record.RowVersion)).ToArrayAsync();
        return notices.ToDictionary(pair => pair.Key, pair => new Facts(pair.Value, certifications.Where(record => record.UserId == pair.Key).Select(record => {
            var days = record.ExpiresOn?.DayNumber - today.DayNumber;
            var state = days is null ? "Unknown" : days < 0 ? "Expired" : days <= settings.CertificationWarnDays ? "ExpiresIn" : "Recorded";
            return new CertificationFact(record.Id, record.CertificationTagId, record.Name, record.IsActive, record.ExpiresOn, state, state == "ExpiresIn" ? days : null, record.RowVersion);
        }).ToArray()));
    }
    static async Task<AppUser> Target(Guid userId, bool admin, bool write, Access access, HubDb db)
    {
        if (admin) Access.Demand(Permissions.Administer(access.Actor));
        else if (userId != access.Me.Id) throw ApiException.NotFound();
        var user = await (write ? db.Users : db.Users.AsNoTracking()).SingleOrDefaultAsync(user => user.Id == userId) ?? throw ApiException.NotFound();
        if (write && !admin) Access.Demand(user.IsActive && !access.Me.IsReadOnly ? Allow.Yes : Allow.No(user.IsActive ? "perm.read_only" : "personFacts.inactive"));
        return user;
    }
    static string? Reason(AppUser user, bool admin, Access access, string? reason)
        => admin && (user.Id != access.Me.Id || !user.IsActive) ? Check.Reason(reason) : Check.Optional(reason, "reason", 1000);
    static async Task<AppUser> LockedTarget(Guid userId, bool admin, Access access, HubDb db)
    {
        await PersonCapacity.LockPeople(db, [userId]);
        var user = await Target(userId, admin, true, access, db);
        // Authentication can already track the caller's AppUser. Refresh after the lock before comparing its version
        // or active status; an identity-map hit must not preserve pre-lock facts from another administrative write.
        await db.Entry(user).ReloadAsync();
        if (!admin) Access.Demand(user.IsActive && !access.Me.IsReadOnly ? Allow.Yes : Allow.No(user.IsActive ? "perm.read_only" : "personFacts.inactive"));
        return user;
    }
    static async Task<Ok<EditorRead>> Editor(Guid userId, bool admin, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await Enabled(db, store, access, admin);
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var user = await Target(userId, admin, false, access, db);
        var today = clock.Today(settings); var facts = await ReadAt(db, [userId], settings, today);
        return TypedResults.Ok(new EditorRead(userId, user.RowVersion, admin || user.IsActive && !access.Me.IsReadOnly, facts[userId], await Choices(db), today));
    }
    static async Task<Created<Saved>> Create(Guid userId, bool admin, CreateBody body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        await Enabled(db, store, access, admin);
        // Resolve authority before any target-person row lock; then re-read the target inside the transaction.
        await Target(userId, admin, false, access, db);
        return await Tx.Run(db, async () => {
            var user = await LockedTarget(userId, admin, access, db);
            await Http.CheckVersion(db, http, user, body.RowVersion);
            var reason = Reason(user, admin, access, body.Reason);
            Check.That(await db.CertificationTags.AnyAsync(tag => tag.Id == body.CertificationTagId && tag.IsActive), "certificationTagId", "personFacts.active_tag");
            if (await db.UserCertifications.AnyAsync(record => record.UserId == userId && record.CertificationTagId == body.CertificationTagId))
                throw ApiException.Conflict("certification_exists", "personFacts.duplicate");
            var record = new UserCertification { UserId = userId, CertificationTagId = body.CertificationTagId, ExpiresOn = body.ExpiresOn };
            db.UserCertifications.Add(record); db.Audit.Note(record, reason: reason); await db.SaveChangesAsync();
            return TypedResults.Created(admin ? $"/api/v1/admin/users/{userId}/certifications/{record.Id}" : $"/api/v1/me/certifications/{record.Id}", new Saved(record.Id, record.RowVersion));
        });
    }
    static void Shape(JsonElement body, string[] allowed)
    {
        Check.That(body.ValueKind == JsonValueKind.Object, "body", "error.invalid");
        var fields = body.EnumerateObject().ToArray();
        Check.That(fields.All(field => allowed.Contains(field.Name)) && fields.Select(field => field.Name).Distinct().Count() == fields.Length, "body", "error.invalid");
        foreach (var field in fields)
            Check.That(field.Value.ValueKind == JsonValueKind.Null || (field.Name == "rowVersion" ? field.Value.ValueKind == JsonValueKind.Number && field.Value.TryGetInt32(out _) : field.Value.ValueKind == JsonValueKind.String), field.Name, "error.invalid");
    }
    static DateOnly? Date(JsonElement body, string field)
    {
        if (!body.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        Check.That(DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date), field, "error.date");
        return date;
    }
    static async Task<Ok<Saved>> Edit(Guid userId, bool admin, Guid id, JsonElement body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        await Enabled(db, store, access, admin); await Target(userId, admin, false, access, db);
        Shape(body, ["expiresOn", "rowVersion", "reason"]); Check.That(body.TryGetProperty("expiresOn", out _), "expiresOn", "error.required");
        return await Tx.Run(db, async () => {
            var user = await LockedTarget(userId, admin, access, db);
            var record = await db.UserCertifications.SingleOrDefaultAsync(record => record.Id == id && record.UserId == userId) ?? throw ApiException.NotFound();
            var patch = new Patch(body); await Http.CheckVersion(db, http, record, patch.RowVersion);
            var reason = Reason(user, admin, access, patch.Str("reason"));
            record.ExpiresOn = Date(body, "expiresOn"); db.Audit.Note(record, reason: reason); await db.SaveChangesAsync();
            return TypedResults.Ok(new Saved(record.Id, record.RowVersion));
        });
    }
    static async Task<NoContent> Remove(Guid userId, bool admin, Guid id, RemoveBody body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        await Enabled(db, store, access, admin); await Target(userId, admin, false, access, db);
        return await Tx.Run(db, async () => {
            var user = await LockedTarget(userId, admin, access, db);
            var record = await db.UserCertifications.SingleOrDefaultAsync(record => record.Id == id && record.UserId == userId) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, record, body.RowVersion); var reason = Reason(user, admin, access, body.Reason);
            db.Audit.Note(record, reason: reason); db.UserCertifications.Remove(record); await db.SaveChangesAsync(); return TypedResults.NoContent();
        });
    }
    static async Task<Ok<Saved>> SetFacts(Guid userId, JsonElement body, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        await Enabled(db, store, access, true); await Target(userId, true, false, access, db);
        Shape(body, ["personType", "personStartDate", "personEndDate", "rowVersion", "reason"]);
        var patch = new Patch(body); Check.That(patch.Has("personType") || patch.Has("personStartDate") || patch.Has("personEndDate"), "body", "error.required");
        return await Tx.Run(db, async () => {
            var user = await LockedTarget(userId, true, access, db);
            await Http.CheckVersion(db, http, user, patch.RowVersion); var reason = Reason(user, true, access, patch.Str("reason"));
            if (patch.Has("personType")) { var type = patch.Str("personType"); Check.That(type is null or "Employee" or "Co-op" or "Contractor", "personType", "personFacts.type"); user.PersonType = type; }
            if (patch.Has("personStartDate")) user.PersonStartDate = Date(body, "personStartDate");
            if (patch.Has("personEndDate")) user.PersonEndDate = Date(body, "personEndDate");
            Check.That(user.PersonStartDate is null || user.PersonEndDate is null || user.PersonStartDate <= user.PersonEndDate, "personEndDate", "personFacts.date_order");
            db.Audit.Note(user, reason: reason); await db.SaveChangesAsync(); return TypedResults.Ok(new Saved(user.Id, user.RowVersion));
        });
    }
}

file sealed record NoticeSource(Guid Id, string? PersonType, DateOnly? PersonStartDate, DateOnly? PersonEndDate);
file sealed record CertificationRow(Guid Id, Guid UserId, Guid CertificationTagId, string Name, bool IsActive, DateOnly? ExpiresOn, int RowVersion);
