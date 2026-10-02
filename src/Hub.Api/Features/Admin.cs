using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Administration (§13.15, FR-ADM-01, FR-ADM-03, FR-ORG-01..07). Every route requires the Admin role.
public static class AdminEndpoints
{
    public sealed record HolidayBody(Guid? OfficeId, DateOnly? Date, string Name);

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/admin").AddEndpointFilter(async (ctx, next) =>
        {
            Access.Demand(Permissions.Administer(ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>().Actor));
            return await next(ctx);
        });

        Ref<Discipline>(g, "disciplines", (db, id) => db.ProjectDisciplines.Where(x => x.DisciplineId == id).Select(x => x.ProjectId).Distinct().CountAsync(),
            (e, p) => { if (p.Has("code")) e.Code = Check.Required(p.Str("code"), "code", 10); if (p.Has("colour")) e.Colour = Check.Required(p.Str("colour"), "colour", 20); });
        Ref<Client>(g, "clients", (db, id) => db.Projects.CountAsync(x => x.ClientId == id),
            (e, p) => { if (p.Has("shortName")) e.ShortName = Check.Optional(p.Str("shortName"), "shortName", 50); });
        Ref<Office>(g, "offices", (db, id) => db.Projects.CountAsync(x => x.OfficeId == id),
            (e, p) =>
            {
                if (p.Has("code")) e.Code = Check.Required(p.Str("code"), "code", 10);
                if (p.Has("timeZone"))
                {
                    var tz = Check.Required(p.Str("timeZone"), "timeZone", 64);
                    Check.That(OrgSettings.Validate(OrgSettings.Def("org_time_zone")!, JsonSerializer.SerializeToElement(tz)) is null, "timeZone", "setting.timezone");
                    e.TimeZone = tz;
                }
            });
        Ref<DeliverableType>(g, "deliverableTypes", (db, id) => db.Deliverables.Where(x => x.DeliverableTypeId == id).Select(x => x.ProjectId).Distinct().CountAsync(),
            (e, p) => { if (p.Has("defaultDisciplineId")) e.DefaultDisciplineId = p.Id("defaultDisciplineId"); });
        Ref<Phase>(g, "phases", (db, id) => db.Projects.CountAsync(x => x.PhaseId == id), (_, _) => { });
        Ref<ProjectType>(g, "projectTypes", (db, id) => db.Projects.CountAsync(x => x.ProjectTypeId == id), (_, _) => { });

        // ---------- Holiday calendars (§10.4, packet 021): an office's statutory holidays, or the organisation's for everyone ----------
        g.MapGet("/holidays", async (Guid? officeId, int? year, HubDb db) => await db.Holidays.AsNoTracking()
            .Where(h => (officeId == null || h.OfficeId == officeId) && (year == null || h.Date.Year == year)).OrderBy(h => h.Date)
            .Select(h => new { h.Id, h.OfficeId, Office = db.Offices.Where(o => o.Id == h.OfficeId).Select(o => o.Name).FirstOrDefault(), h.Date, h.Name }).ToListAsync());
        g.MapPost("/holidays", async (HolidayBody b, HubDb db) =>
        {
            var date = b.Date ?? throw ApiException.Invalid("date", "error.required");
            if (b.OfficeId is { } o) Check.That(await db.Offices.AnyAsync(x => x.Id == o), "officeId", "error.not_found");
            if (await db.Holidays.AnyAsync(h => h.OfficeId == b.OfficeId && h.Date == date)) throw ApiException.Conflict("holiday_exists", "holiday.exists");
            var h = new Holiday { OfficeId = b.OfficeId, Date = date, Name = Check.Required(b.Name, "name", 120) };
            db.Holidays.Add(h);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/admin/holidays/{h.Id}", new { h.Id });
        });
        g.MapDelete("/holidays/{id:guid}", async (Guid id, HubDb db) =>
        {
            var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            db.Holidays.Remove(h); // configuration, not a work item; the removal is logged
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // ---------- Settings (FR-014) ----------
        g.MapGet("/settings", async (HubDb db) =>
        {
            var rows = await db.Settings.AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.Value);
            return OrgSettings.Defs.Select(d => new
            {
                d.Key, Kind = d.Kind.ToString(), d.Group,
                Default = d.Default,
                Value = rows.TryGetValue(d.Key, out var v) ? JsonDocument.Parse(v).RootElement.Clone() : JsonSerializer.SerializeToElement(d.Default, JsonOpts.Web),
            });
        });

        g.MapPut("/settings/{key}", async (string key, JsonElement body, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var def = OrgSettings.Def(key) ?? throw ApiException.Invalid("key", "setting.unknown");
            var value = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("value", out var v) ? v : body;
            if (OrgSettings.Validate(def, value) is { } err) throw ApiException.Invalid("value", err);
            var json = value.GetRawText();
            var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key) ?? throw ApiException.Invalid("key", "setting.unknown");
            if (row.Value != json) { row.Value = json; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = db.Audit.ActorId; }
            await db.SaveChangesAsync();
            store.Invalidate();
            return Results.Ok(new { key, value = JsonDocument.Parse(row.Value).RootElement });
        });

        // ---------- Operations (§23.8, packet 011 FR-006): what the watchdog would alert on now, and each job's last run ----------
        g.MapGet("/operations", async (HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var s = await store.Get(db);
            var now = clock.GetUtcNow();
            var problems = OpsChecks.Evaluate(await OpsChecks.Gather(db, s, now, now.AddHours(-24)));
            var jobs = new List<object>();
            foreach (var name in (await db.JobRuns.Select(r => r.JobName).Distinct().ToListAsync()).Order())
            {
                var last = await db.JobRuns.AsNoTracking().Where(r => r.JobName == name).OrderByDescending(r => r.StartedAt).FirstAsync();
                jobs.Add(new
                {
                    Job = name, last.StartedAt, last.FinishedAt, last.Status,
                    LastSuccess = await db.JobRuns.Where(r => r.JobName == name && r.Status == "Succeeded").MaxAsync(r => (DateTimeOffset?)r.StartedAt),
                    Failed24h = await db.JobRuns.CountAsync(r => r.JobName == name && r.Status == "Failed" && r.StartedAt >= now.AddHours(-24)),
                });
            }
            return new { AsOf = now, Problems = problems, Jobs = jobs, PendingChanges = await db.Outbox.CountAsync(o => o.ProcessedAt == null) };
        });

        // ---------- Users and roles (FR-004, FR-016, FR-017, FR-027, E-26) ----------
        g.MapGet("/users", async (HubDb db, string? q, bool? inactive, bool? noSupervisor, string? role) =>
        {
            var query = db.Users.AsNoTracking().Include(u => u.Roles).AsQueryable();
            if (inactive != true) query = query.Where(u => u.IsActive);
            if (noSupervisor == true) query = query.Where(u => u.SupervisorId == null);
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Roles.Any(r => r.Role == role));
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => EF.Functions.ILike(u.DisplayName, $"%{q.Trim()}%") || EF.Functions.ILike(u.Email, $"%{q.Trim()}%"));
            return await query.OrderBy(u => u.DisplayName).Take(1000).Select(u => new
            {
                u.Id, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.SupervisorId, u.IsActive, u.WeeklyCapacityHours, u.IsTemplateEditor,
                u.LastSignInAt, u.RowVersion, Roles = u.Roles.Select(r => new { r.Role, r.Source }),
            }).ToListAsync();
        });

        // Local-password sign-in has no directory to add people at first sign-in, so an Admin adds them. Store the person's
        // future Entra sign-in name as the email: with no Entra object ID, their first Entra sign-in claims this record.
        g.MapPost("/users", async (CreateUserBody b, HubDb db, IHostEnvironment env, IConfiguration cfg) =>
        {
            if (!AuthSetup.LocalAuthAllowed(env, cfg)) throw ApiException.Rule("local_sign_in_only", "admin.create_user_local_only");
            var email = Check.Required(b.Email, "email", 200);
            Check.That(AuthSetup.IsEmail(email), "email", "admin.email_invalid");
            var u = new AppUser
            {
                Email = email, DisplayName = Check.Required(b.DisplayName, "displayName", 200), JobTitle = Check.Optional(b.JobTitle, "jobTitle", 200),
                OfficeId = b.OfficeId, SupervisorId = b.SupervisorId,
            };
            if (b.OfficeId is { } o) Check.That(await db.Offices.AnyAsync(x => x.Id == o), "officeId", "error.not_found");
            if (b.SupervisorId is { } s) Check.That(await db.Users.AnyAsync(x => x.Id == s && x.IsActive), "supervisorId", "error.not_found");
            if (await db.Users.AnyAsync(x => x.Email == email)) throw ApiException.Conflict("user_exists", "admin.user_exists");
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/admin/users/{u.Id}", new { u.Id, u.RowVersion });
        });

        g.MapPatch("/users/{id:guid}", async (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var p = new Patch(body);
            var u = await db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, u, p.RowVersion);
            if (p.Has("supervisorId"))
            {
                var sup = p.Id("supervisorId");
                Check.That(sup != u.Id, "supervisorId", "admin.self_supervisor");
                if (sup is { } s) Check.That(await db.Users.AnyAsync(x => x.Id == s && x.IsActive), "supervisorId", "error.not_found");
                u.SupervisorId = sup;
            }
            if (p.Has("officeId")) u.OfficeId = p.Id("officeId");
            if (p.Has("weeklyCapacityHours"))
            {
                var c = p.Dec("weeklyCapacityHours");
                Check.That(c is null or (>= 0 and <= 80), "weeklyCapacityHours", "error.positive");
                u.WeeklyCapacityHours = c;
            }
            if (p.Has("isTemplateEditor")) u.IsTemplateEditor = p.Bool("isTemplateEditor") ?? false;
            if (p.Has("isActive"))
            {
                var active = p.Bool("isActive") ?? true;
                if (!active && u.Roles.Any(r => r.Role == SystemRole.Admin)) await EnsureAnotherAdmin(db, u.Id);
                u.IsActive = active;
            }
            await db.SaveChangesAsync();
            return Results.Ok(new { u.Id, u.RowVersion });
        });

        g.MapPost("/users/{id:guid}/roles", async (Guid id, RoleBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
        {
            Check.OneOf(body.Role, SystemRole.All, "role");
            var u = await db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            if (!u.Roles.Any(r => r.Role == body.Role && r.Source == RoleSource.Manual))
            {
                var r = new UserSystemRole { UserId = u.Id, Role = body.Role, Source = RoleSource.Manual, GrantedBy = me.Id, GrantedAt = clock.GetUtcNow() };
                db.UserRoles.Add(r);
                db.Audit.Note(r, action: "RoleAdded", key: u.Email);
                await db.SaveChangesAsync();
            }
            return Results.NoContent();
        });

        g.MapDelete("/users/{id:guid}/roles/{role}", async (Guid id, string role, HubDb db) =>
        {
            var u = await db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var r = u.Roles.FirstOrDefault(x => x.Role == role && x.Source == RoleSource.Manual) ?? throw ApiException.NotFound();
            if (role == SystemRole.Admin && !u.Roles.Any(x => x.Role == SystemRole.Admin && x.Source == RoleSource.Group)) await EnsureAnotherAdmin(db, u.Id);
            db.UserRoles.Remove(r);
            db.Audit.Note(r, action: "RoleRemoved", key: u.Email);
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // Organisation-level history of administrative changes (FR-021).
        g.MapGet("/activity", async (HubDb db, DateOnly? from, DateOnly? to, Guid? actorId, string? itemType, string? category, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize);
            var q = db.ActivityLog.AsNoTracking().AsQueryable();
            if (from is { } f) q = q.Where(a => a.OccurredAt >= f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            if (to is { } t) q = q.Where(a => a.OccurredAt < t.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            if (actorId is { } aid) q = q.Where(a => a.ActorUserId == aid);
            if (!string.IsNullOrWhiteSpace(itemType)) q = q.Where(a => a.ItemType == itemType);
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(a => a.Categories.Contains(category));
            else q = q.Where(a => a.ProjectId == null);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return new Page<object>(await ActivityEndpoints.Render(db, rows), pg, size, total);
        });
    }

    public sealed record RoleBody(string Role);
    public sealed record CreateUserBody(string? Email, string? DisplayName, string? JobTitle, Guid? OfficeId, Guid? SupervisorId);

    static async Task EnsureAnotherAdmin(HubDb db, Guid userId)
    {
        var others = await db.Users.CountAsync(u => u.Id != userId && u.IsActive && u.Roles.Any(r => r.Role == SystemRole.Admin));
        if (others == 0) throw ApiException.Rule("last_admin", "admin.last_admin");
    }

    /// Generic list/create/update/deactivate for one kind of reference data. Entries in use are never
    /// deleted; deactivation reports how many projects use them first (FR-015, E-18).
    static void Ref<T>(RouteGroupBuilder g, string kind, Func<HubDb, Guid, Task<int>> usage, Action<T, Patch> extra) where T : RefData, new()
    {
        g.MapGet($"/{kind}", async (HubDb db) => await db.Set<T>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync());

        g.MapGet($"/{kind}/{{id:guid}}/usage", async (Guid id, HubDb db) => new { projects = await usage(db, id) });

        g.MapPost($"/{kind}", async (JsonElement body, HubDb db) =>
        {
            var p = new Patch(body);
            var e = new T { Name = Check.Required(p.Str("name"), "name", 120), SortOrder = p.Int("sortOrder") ?? await db.Set<T>().CountAsync() };
            extra(e, p);
            db.Set<T>().Add(e);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/admin/{kind}/{e.Id}", e);
        });

        g.MapPatch($"/{kind}/{{id:guid}}", async (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var p = new Patch(body);
            var e = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, e, p.RowVersion);
            if (p.Has("name")) e.Name = Check.Required(p.Str("name"), "name", 120);
            if (p.Has("sortOrder")) e.SortOrder = p.Int("sortOrder") ?? e.SortOrder;
            if (p.Has("isActive")) e.IsActive = p.Bool("isActive") ?? e.IsActive;
            extra(e, p);
            await db.SaveChangesAsync();
            return Results.Ok(e);
        });
    }
}
