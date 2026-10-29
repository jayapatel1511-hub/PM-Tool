using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Administration (§13.15, FR-ADM-01, FR-ADM-03, FR-ORG-01..07). Every route requires the Admin role.
public static class AdminEndpoints
{
    public sealed record RetryOutboxResponse(int Retried);
    public sealed record HolidayBody(Guid? OfficeId, DateOnly? Date, string Name);

    /// One reference-data entry as the lists and commands answer it, written flat in the order the entity has always serialised: the
    /// kind's own columns (the records below), then name, active flag, order, audit identity, stamps, row version and id.
    public record ReferenceRow(string Name, bool IsActive, int SortOrder, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id);
    public sealed record OfficeRow(string Code, string TimeZone, string Name, bool IsActive, int SortOrder, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
        : ReferenceRow(Name, IsActive, SortOrder, AuditType, AuditProjectId, AuditKey, AuditName, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion, Id);
    public sealed record DisciplineRow(string Code, string Colour, string Name, bool IsActive, int SortOrder, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
        : ReferenceRow(Name, IsActive, SortOrder, AuditType, AuditProjectId, AuditKey, AuditName, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion, Id);
    public sealed record ClientRow(string? ShortName, string Name, bool IsActive, int SortOrder, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
        : ReferenceRow(Name, IsActive, SortOrder, AuditType, AuditProjectId, AuditKey, AuditName, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion, Id);
    public sealed record DeliverableTypeRow(Guid? DefaultDisciplineId, string Name, bool IsActive, int SortOrder, string AuditType, Guid? AuditProjectId, string? AuditKey, string? AuditName,
        DateTimeOffset CreatedAt, Guid? CreatedBy, DateTimeOffset UpdatedAt, Guid? UpdatedBy, int RowVersion, Guid Id)
        : ReferenceRow(Name, IsActive, SortOrder, AuditType, AuditProjectId, AuditKey, AuditName, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, RowVersion, Id);
    /// How many projects use an entry (deactivating it asks first, FR-015).
    public sealed record ReferenceUsage(int Projects);

    public sealed record HolidayRow(Guid Id, Guid? OfficeId, string? Office, DateOnly Date, string Name);
    public sealed record HolidayAdded(Guid Id);

    /// A setting with its default and current value; a flag row also names its packet (§43.4 FR-DLV-07), every other row leaves `packet` out.
    public sealed record SettingRow(string Key, string Kind, string Group, object Default, JsonElement Value, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Packet = null);
    public sealed record SettingSaved(string Key, JsonElement Value);
    sealed record AdminSettingsChangedPayload(string Key);

    public sealed record OpsJob(string Job, DateTimeOffset StartedAt, DateTimeOffset? FinishedAt, string Status, DateTimeOffset? LastSuccess, int Failed24h);
    public sealed record OpsDeadLetter(Guid Id, string EventType, string? ProjectNumber, int Attempts, string? LastError, DateTimeOffset CreatedAt, DateTimeOffset DeadLetteredAt);
    public sealed record OperationsRead(DateTimeOffset AsOf, List<OpsProblem> Problems, List<OpsJob> Jobs, int PendingChanges, List<OpsDeadLetter> DeadLetters, int DeadLetterCount);

    public sealed record AdminUserRole(string Role, string Source);
    public sealed record AdminUserRow(Guid Id, string DisplayName, string Email, string? JobTitle, Guid? OfficeId, Guid? SupervisorId, Guid? HomeDisciplineId, bool IsActive,
        decimal? WeeklyCapacityHours, bool IsTemplateEditor, DateTimeOffset? LastSignInAt, int RowVersion, List<AdminUserRole> Roles);
    /// The id and new row version of a person the Admin added or changed.
    public sealed record UserSaved(Guid Id, int RowVersion);

    public static void Map(RouteGroupBuilder api)
    {
        var g = api.MapGroup("/admin").AddEndpointFilter(async (ctx, next) =>
        {
            Access.Demand(Permissions.Administer(ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>().Actor));
            return await next(ctx);
        });

        Ref<CommitmentMissCategory, ReferenceRow>(g.MapGroup("").RequireFlag("commitment_reliability"), "commitmentMissCategories", Row,
            (db, id) => db.OutputCommitmentEvents.CountAsync(e => e.MissCategoryId == id), (_, _) => { });
        Ref<Discipline, DisciplineRow>(g, "disciplines", Row,
            (db, id) => db.ProjectDisciplines.Where(x => x.DisciplineId == id).Select(x => x.ProjectId).Distinct().CountAsync(),
            (e, p) => { if (p.Has("code")) e.Code = Check.Required(p.Str("code"), "code", 10); if (p.Has("colour")) e.Colour = Check.Required(p.Str("colour"), "colour", 20); });
        Ref<Client, ClientRow>(g, "clients", Row,
            (db, id) => db.Projects.CountAsync(x => x.ClientId == id),
            (e, p) => { if (p.Has("shortName")) e.ShortName = Check.Optional(p.Str("shortName"), "shortName", 50); });
        Ref<Office, OfficeRow>(g, "offices", Row,
            (db, id) => db.Projects.CountAsync(x => x.OfficeId == id),
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
        Ref<DeliverableType, DeliverableTypeRow>(g, "deliverableTypes", Row,
            (db, id) => db.Deliverables.Where(x => x.DeliverableTypeId == id).Select(x => x.ProjectId).Distinct().CountAsync(),
            (e, p) => { if (p.Has("defaultDisciplineId")) e.DefaultDisciplineId = p.Id("defaultDisciplineId"); });
        Ref<Phase, ReferenceRow>(g, "phases", Row, (db, id) => db.Projects.CountAsync(x => x.PhaseId == id), (_, _) => { });
        Ref<ProjectType, ReferenceRow>(g, "projectTypes", Row, (db, id) => db.Projects.CountAsync(x => x.ProjectTypeId == id), (_, _) => { });

        // ---------- Holiday calendars (§10.4, packet 021): an office's statutory holidays, or the organisation's for everyone ----------
        g.MapGet("/holidays", async Task<Ok<List<HolidayRow>>> (Guid? officeId, int? year, HubDb db) => TypedResults.Ok(await db.Holidays.AsNoTracking()
            .Where(h => (officeId == null || h.OfficeId == officeId) && (year == null || h.Date.Year == year)).OrderBy(h => h.Date)
            .Select(h => new HolidayRow(h.Id, h.OfficeId, db.Offices.Where(o => o.Id == h.OfficeId).Select(o => o.Name).FirstOrDefault(), h.Date, h.Name)).ToListAsync()));
        g.MapPost("/holidays", async Task<Created<HolidayAdded>> (HolidayBody b, HubDb db) => await Tx.Run(db, async () =>
        {
            var date = b.Date ?? throw ApiException.Invalid("date", "error.required");
            if (b.OfficeId is { } o) Check.That(await db.Offices.AnyAsync(x => x.Id == o), "officeId", "error.not_found");
            if (await db.Holidays.AnyAsync(h => h.OfficeId == b.OfficeId && h.Date == date)) throw ApiException.Conflict("holiday_exists", "holiday.exists");
            var h = new Holiday { OfficeId = b.OfficeId, Date = date, Name = Check.Required(b.Name, "name", 120) };
            var people = await db.Users.Where(u => b.OfficeId == null || u.OfficeId == b.OfficeId).Select(u => u.Id).ToArrayAsync();
            await PersonCapacity.LockPeople(db, people);
            db.Holidays.Add(h);
            foreach (var person in people) await PersonCapacity.Touch(db, person, date, date);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/admin/holidays/{h.Id}", new HolidayAdded(h.Id));
        }));
        g.MapDelete("/holidays/{id:guid}", async Task<NoContent> (Guid id, HubDb db) => await Tx.Run(db, async () =>
        {
            var h = await db.Holidays.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var people = await db.Users.Where(u => h.OfficeId == null || u.OfficeId == h.OfficeId).Select(u => u.Id).ToArrayAsync();
            await PersonCapacity.LockPeople(db, people);
            foreach (var person in people) await PersonCapacity.Touch(db, person, h.Date, h.Date);
            db.Holidays.Remove(h); // configuration, not a work item; the removal is logged
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        }));

        // ---------- Settings (FR-014) ----------
        g.MapGet("/settings", async Task<Ok<List<SettingRow>>> (HubDb db) =>
        {
            var rows = await db.Settings.AsNoTracking().ToDictionaryAsync(r => r.Key, r => r.Value);
            return TypedResults.Ok(OrgSettings.Defs.Where(d => !OrgSettings.ShownWithFlag.TryGetValue(d.Key, out var flag) || rows.GetValueOrDefault($"flag.{flag}") == "true").Select(d =>
            {
                var value = rows.TryGetValue(d.Key, out var v) ? JsonDocument.Parse(v).RootElement.Clone() : JsonSerializer.SerializeToElement(d.Default, JsonOpts.Web);
                return new SettingRow(d.Key, d.Kind.ToString(), d.Group, d.Default, value, Flags.Find(d.Key)?.Packet);
            }).ToList());
        });

        g.MapPut("/settings/{key}", async Task<Ok<SettingSaved>> (string key, JsonElement body, HubDb db, SettingsStore store, TimeProvider clock, EvaluationSignal signal) =>
        {
            var def = OrgSettings.Def(key) ?? throw ApiException.Invalid("key", "setting.unknown");
            var value = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("value", out var v) ? v : body;
            if (OrgSettings.Validate(def, value) is { } err) throw ApiException.Invalid("value", err);
            if (key == "flag.verifiable_log" && value.ValueKind == JsonValueKind.False
                && await db.Set<ActivityChainHead>().AnyAsync())
                throw ApiException.Invalid("value", "audit.cannot_disable");
            if (key == "flag.verifiable_log" && value.ValueKind == JsonValueKind.True
                && await db.ActivityLog.AnyAsync(a => a.ChainId == null))
                throw ApiException.Invalid("value", "audit.initialize_required");
            var json = value.GetRawText();
            var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == key) ?? throw ApiException.Invalid("key", "setting.unknown");
            if (row.Value != json)
            {
                row.Value = json; row.UpdatedAt = clock.GetUtcNow(); row.UpdatedBy = db.Audit.ActorId;
                // The evaluator reads thresholds, health, rules, the calendar and `flag.backlog` (FR-BKL-05): re-evaluate every Active
                // project now, through the outbox, rather than leave health and milestone status stale until the nightly run (FR-BKL-09).
                if (def.Group is "thresholds" or "health" or "rules" or "calendar" || key == $"flag.{TaskRules.BacklogFlag}")
                    db.Outbox.Add(new OutboxEvent { EventType = "SettingsChanged", CreatedAt = clock.GetUtcNow(), Payload = JsonSerializer.Serialize(new AdminSettingsChangedPayload(key), JsonOpts.Web) });
            }
            await db.SaveChangesAsync();
            store.Invalidate();
            signal.Poke();
            return TypedResults.Ok(new SettingSaved(key, JsonDocument.Parse(row.Value).RootElement));
        });

        // ---------- Operations (§23.8, packet 011 FR-006): what the watchdog would alert on now, and each job's last run ----------
        g.MapGet("/operations", async Task<Ok<OperationsRead>> (HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var s = await store.Get(db);
            var now = clock.GetUtcNow();
            var problems = OpsChecks.Evaluate(await OpsChecks.Gather(db, s, now, now.AddHours(-24)));
            if (s.IsFlagOn("verifiable_log"))
                problems.AddRange(await db.Set<ActivityChainHead>().Where(h => h.FirstBrokenSeq != null)
                    .Select(h => new OpsProblem("AuditChainBroken", "Chain " + h.ChainId + "; entry " + h.FirstBrokenSeq)).ToListAsync());
            var jobs = new List<OpsJob>();
            foreach (var name in (await db.JobRuns.Select(r => r.JobName).Distinct().ToListAsync()).Order())
            {
                var last = await db.JobRuns.AsNoTracking().Where(r => r.JobName == name).OrderByDescending(r => r.StartedAt).FirstAsync();
                jobs.Add(new OpsJob(name, last.StartedAt, last.FinishedAt, last.Status,
                    await db.JobRuns.Where(r => r.JobName == name && r.Status == "Succeeded").MaxAsync(r => (DateTimeOffset?)r.StartedAt),
                    await db.JobRuns.CountAsync(r => r.JobName == name && r.Status == "Failed" && r.StartedAt >= now.AddHours(-24))));
            }
            var deadLetters = await db.Outbox.AsNoTracking().Where(o => o.ProcessedAt == null && o.DeadLetteredAt != null)
                .OrderByDescending(o => o.DeadLetteredAt).Take(200).Select(o => new OpsDeadLetter(o.Id, o.EventType,
                    db.Projects.Where(p => p.Id == o.ProjectId).Select(p => p.ProjectNumber).FirstOrDefault(),
                    o.Attempts, o.LastError, o.CreatedAt, o.DeadLetteredAt!.Value)).ToListAsync();
            var deadLetterCount = await db.Outbox.CountAsync(o => o.ProcessedAt == null && o.DeadLetteredAt != null);
            return TypedResults.Ok(new OperationsRead(now, problems, jobs, await db.Outbox.CountAsync(o => o.ProcessedAt == null && o.DeadLetteredAt == null), deadLetters, deadLetterCount));
        });

        g.MapPost("/operations/outbox/retry", async Task<Ok<RetryOutboxResponse>> (Guid? id, HubDb db, CurrentUser me, EvaluationSignal signal, ILoggerFactory logs) =>
        {
            var q = db.Outbox.Where(o => o.ProcessedAt == null && o.DeadLetteredAt != null);
            if (id is { } one) q = q.Where(o => o.Id == one);
            var rows = await q.ToListAsync();
            foreach (var e in rows) { e.Attempts = 0; e.DeadLetteredAt = null; e.LastError = null; }
            await db.SaveChangesAsync();
            signal.Poke();
            logs.CreateLogger("Hub.Operations").LogInformation("Admin {UserId} retried {Count} dead-lettered outbox events", me.Id, rows.Count);
            return TypedResults.Ok(new RetryOutboxResponse(rows.Count));
        });

        // ---------- Users and roles (FR-004, FR-016, FR-017, FR-027, E-26) ----------
        g.MapGet("/users", async Task<Ok<List<AdminUserRow>>> (HubDb db, string? q, bool? inactive, bool? noSupervisor, string? role) =>
        {
            var query = db.Users.AsNoTracking().Include(u => u.Roles).AsQueryable();
            if (inactive != true) query = query.Where(u => u.IsActive);
            if (noSupervisor == true) query = query.Where(u => u.SupervisorId == null);
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(u => u.Roles.Any(r => r.Role == role));
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(u => EF.Functions.ILike(u.DisplayName, $"%{q.Trim()}%") || EF.Functions.ILike(u.Email, $"%{q.Trim()}%"));
            return TypedResults.Ok(await query.OrderBy(u => u.DisplayName).Take(1000).Select(u => new AdminUserRow(
                u.Id, u.DisplayName, u.Email, u.JobTitle, u.OfficeId, u.SupervisorId, u.HomeDisciplineId, u.IsActive, u.WeeklyCapacityHours, u.IsTemplateEditor,
                u.LastSignInAt, u.RowVersion, u.Roles.Select(r => new AdminUserRole(r.Role, r.Source)).ToList())).ToListAsync());
        });

        // Local-password sign-in has no directory to add people at first sign-in, so an Admin adds them. Store the person's
        // future Entra sign-in name as the email: with no Entra object ID, their first Entra sign-in claims this record.
        g.MapPost("/users", async Task<Created<UserSaved>> (CreateUserBody b, HubDb db, IHostEnvironment env, IConfiguration cfg) =>
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
            return TypedResults.Created($"/api/v1/admin/users/{u.Id}", new UserSaved(u.Id, u.RowVersion));
        });

        g.MapPatch("/users/{id:guid}", async Task<Ok<UserSaved>> (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var p = new Patch(body);
            var u = await db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, u, p.RowVersion);
            if (p.Has("supervisorId"))
            {
                var sup = p.Id("supervisorId");
                Check.That(sup != u.Id, "supervisorId", "admin.self_supervisor");
                // An unchanged value is kept even when it is no longer active, so editing anything else still saves.
                if (sup is { } s && s != u.SupervisorId) Check.That(await db.Users.AnyAsync(x => x.Id == s && x.IsActive), "supervisorId", "error.not_found");
                u.SupervisorId = sup;
            }
            if (p.Has("officeId")) u.OfficeId = p.Id("officeId");
            if (p.Has("homeDisciplineId")) // FR-GRP-01 (D7): edited by an Admin, audited on the user record
            {
                var discipline = p.Id("homeDisciplineId");
                if (discipline is { } d && d != u.HomeDisciplineId) Check.That(await db.Disciplines.AnyAsync(x => x.Id == d && x.IsActive), "homeDisciplineId", "error.not_found");
                u.HomeDisciplineId = discipline;
            }
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
            return TypedResults.Ok(new UserSaved(u.Id, u.RowVersion));
        });

        g.MapPost("/users/{id:guid}/roles", async Task<NoContent> (Guid id, RoleBody body, HubDb db, CurrentUser me, TimeProvider clock) =>
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
            return TypedResults.NoContent();
        });

        g.MapDelete("/users/{id:guid}/roles/{role}", async Task<NoContent> (Guid id, string role, HubDb db) =>
        {
            var u = await db.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var r = u.Roles.FirstOrDefault(x => x.Role == role && x.Source == RoleSource.Manual) ?? throw ApiException.NotFound();
            if (role == SystemRole.Admin && !u.Roles.Any(x => x.Role == SystemRole.Admin && x.Source == RoleSource.Group)) await EnsureAnotherAdmin(db, u.Id);
            db.UserRoles.Remove(r);
            db.Audit.Note(r, action: "RoleRemoved", key: u.Email);
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        });

        // Organisation-level history of administrative changes (FR-021).
        g.MapGet("/activity", async Task<Ok<Page<ActivityEndpoints.ActivityEntry>>> (HubDb db, DateOnly? from, DateOnly? to, Guid? actorId, string? itemType, string? category, string? excludeAction, int? page, int? pageSize) =>
        {
            var (pg, size) = Http.Paging(page, pageSize);
            var q = db.ActivityLog.AsNoTracking().Where(a => a.ItemType != "PlanningEntry");
            if (from is { } f) q = q.Where(a => a.OccurredAt >= f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            if (to is { } t) q = q.Where(a => a.OccurredAt < t.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            if (actorId is { } aid) q = q.Where(a => a.ActorUserId == aid);
            if (!string.IsNullOrWhiteSpace(itemType)) q = q.Where(a => a.ItemType == itemType);
            if (!string.IsNullOrWhiteSpace(excludeAction)) q = q.Where(a => a.Action != excludeAction); // sign-ins swamp the organisation log
            if (!string.IsNullOrWhiteSpace(category)) q = q.Where(a => a.Categories.Contains(category));
            else q = q.Where(a => a.ProjectId == null);
            var total = await q.CountAsync();
            var rows = await q.OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).Skip((pg - 1) * size).Take(size).ToListAsync();
            return TypedResults.Ok(new Page<ActivityEndpoints.ActivityEntry>(await ActivityEndpoints.Render(db, rows), pg, size, total));
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
    static void Ref<T, TRow>(RouteGroupBuilder g, string kind, Func<T, TRow> row, Func<HubDb, Guid, Task<int>> usage, Action<T, Patch> extra) where T : RefData, new() where TRow : ReferenceRow
    {
        g.MapGet($"/{kind}", async Task<Ok<List<TRow>>> (HubDb db) => TypedResults.Ok((await db.Set<T>().AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync()).Select(row).ToList()));

        g.MapGet($"/{kind}/{{id:guid}}/usage", async Task<Ok<ReferenceUsage>> (Guid id, HubDb db) => TypedResults.Ok(new ReferenceUsage(await usage(db, id))));

        g.MapPost($"/{kind}", async Task<Created<TRow>> (JsonElement body, HubDb db) =>
        {
            var p = new Patch(body);
            var e = new T { Name = Check.Required(p.Str("name"), "name", 120), SortOrder = p.Int("sortOrder") ?? await db.Set<T>().CountAsync() };
            extra(e, p);
            db.Set<T>().Add(e);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/admin/{kind}/{e.Id}", row(e));
        });

        g.MapPatch($"/{kind}/{{id:guid}}", async Task<Ok<TRow>> (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var p = new Patch(body);
            var e = await db.Set<T>().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, e, p.RowVersion);
            if (p.Has("name")) e.Name = Check.Required(p.Str("name"), "name", 120);
            if (p.Has("sortOrder")) e.SortOrder = p.Int("sortOrder") ?? e.SortOrder;
            if (p.Has("isActive")) e.IsActive = p.Bool("isActive") ?? e.IsActive;
            extra(e, p);
            await db.SaveChangesAsync();
            return TypedResults.Ok(row(e));
        });
    }

    // Each kind's record: its own columns, then the shared ones, in the order the entity serialised them.
    static OfficeRow Row(Office e) => new(e.Code, e.TimeZone, e.Name, e.IsActive, e.SortOrder, e.AuditType, e.AuditProjectId, e.AuditKey, e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id);
    static DisciplineRow Row(Discipline e) => new(e.Code, e.Colour, e.Name, e.IsActive, e.SortOrder, e.AuditType, e.AuditProjectId, e.AuditKey, e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id);
    static ClientRow Row(Client e) => new(e.ShortName, e.Name, e.IsActive, e.SortOrder, e.AuditType, e.AuditProjectId, e.AuditKey, e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id);
    static DeliverableTypeRow Row(DeliverableType e) => new(e.DefaultDisciplineId, e.Name, e.IsActive, e.SortOrder, e.AuditType, e.AuditProjectId, e.AuditKey, e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id);
    /// The kinds with no columns of their own (phases, project types, missed-commitment categories).
    static ReferenceRow Row(RefData e) => new(e.Name, e.IsActive, e.SortOrder, e.AuditType, e.AuditProjectId, e.AuditKey, e.AuditName, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy, e.RowVersion, e.Id);
}
