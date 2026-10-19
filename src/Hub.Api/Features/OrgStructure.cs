using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Api.Features;

/// Company structure read and administration endpoints (§54). Root wiring maps this feature after the flag exists.
public static class OrgStructureEndpoints
{
    public sealed record LevelBody(string Name, int? SortOrder);
    public sealed record UnitBody(string Name, Guid LevelId, Guid? ParentId, Guid? LeaderId, Guid? DisciplineId, Guid? OfficeId);
    public sealed record PlacementBody(Guid? UnitId, int? RowVersion = null);
    public sealed record LevelOrderBody(Guid[] Ids, Dictionary<Guid, int> Versions);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/lead-unit", async (Guid id, Access access) =>
        { var (project, _) = await access.Project(id, false); return new { project.LeadUnitId }; }).RequireFlag("org_structure");
        var org = api.MapGroup("/organisation").RequireFlag("org_structure");
        org.MapGet("", async (HubDb db, CurrentUser me) =>
        {
            var levels = await db.Set<OrgLevel>().AsNoTracking().OrderBy(x => x.SortOrder).Select(x => new { x.Id, x.Name, x.SortOrder }).ToListAsync();
            var units = await db.Set<OrgUnit>().AsNoTracking().OrderBy(x => x.Name).Select(x => new
            {
                x.Id, x.Name, x.ParentId, x.LevelId, Level = db.Set<OrgLevel>().Where(l => l.Id == x.LevelId).Select(l => l.Name).FirstOrDefault(),
                x.LeaderId, Leader = db.Users.Where(u => u.Id == x.LeaderId).Select(u => u.DisplayName).FirstOrDefault(), x.DisciplineId, x.OfficeId, x.ClosedOn,
                HeadCount = db.Users.Count(u => u.IsActive && EF.Property<Guid?>(u, "UnitId") == x.Id)
            }).ToListAsync();
            var settings = await db.Settings.AsNoTracking().Where(s => s.Key == "include_indirect_reports").Select(s => s.Value).SingleOrDefaultAsync();
            var scope = await PeopleScope.Load(db, me.Actor, settings == "true");
            var placements = await db.Users.AsNoTracking().Where(u => u.IsActive).Select(u => u.UnitId).ToListAsync();
            var tree = units.Select(u => new OrgTree.Unit(u.Id, u.ParentId, 0, u.LeaderId)).ToArray();
            units = units.Select(u => u with { HeadCount = placements.Count(id => id != null && OrgTree.Descendants(u.Id, tree).Contains(id.Value)) }).ToList();
            var people = await db.Users.AsNoTracking().Where(u => u.IsActive && scope.Contains(u.Id)).Select(u => new
            {
                u.Id, u.RowVersion, u.DisplayName, u.Email, UnitId = EF.Property<Guid?>(u, "UnitId"), u.SupervisorId
            }).ToListAsync();
            var managers = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.SupervisorId);
            var cycleIds = me.Actor.Admin ? managers.Where(u => OrgTree.CreatesCycle(u.Key, u.Value, managers)).Select(u => u.Key).ToArray() : [];
            var cycles = me.Actor.Admin ? await db.Users.AsNoTracking().Where(u => cycleIds.Contains(u.Id)).Select(u => new { u.Id, u.DisplayName }).ToListAsync() : null;
            return new { levels, units, people, ManagerCycles = cycles, IncludeIndirectReports = settings == "true" };
        });

        org.MapGet("/placements", async (string? ids, HubDb db, Access access, CurrentUser me) =>
        {
            var chosen = Http.Ids(ids); Check.That(chosen.Length <= 1000, "ids", "error.range");
            var people = await PeopleScope.Widen(db, access.Actor, WorkloadEndpoints.People(db, access, me));
            if (!access.Actor.ReadOnly) people = people.Union(await AvailabilityEndpoints.VisiblePeople(db, access));
            return await people.Where(u => chosen.Contains(u.Id)).Select(u => new { u.Id, u.UnitId }).ToListAsync();
        });

        var admin = api.MapGroup("/admin/organisation").AddEndpointFilter(async (ctx, next) =>
            {
                Access.Demand(Permissions.Administer(ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>().Actor));
                return await next(ctx);
            }).RequireFlag("org_structure").AddEndpointFilter<OrgWriteFilter>();
        admin.MapGet("/levels", async (HubDb db) => await db.Set<OrgLevel>().OrderBy(x => x.SortOrder).ToListAsync());
        admin.MapPost("/levels/order", async (LevelOrderBody body, HubDb db) =>
        {
            var levels = await db.Set<OrgLevel>().ToListAsync();
            Check.That(body.Ids.Length == levels.Count && body.Ids.Distinct().Count() == levels.Count && levels.All(l => body.Ids.Contains(l.Id)), "ids", "error.invalid");
            foreach (var level in levels) Coordination.Version(level, body.Versions.GetValueOrDefault(level.Id, -1));
            // Temporary orders stay inside this transaction; only the final old/new order is audited.
            await db.Database.ExecuteSqlRawAsync("UPDATE hub.org_level SET sort_order = -1000000 - sort_order");
            for (var i = 0; i < body.Ids.Length; i++)
            { var level = levels.Single(l => l.Id == body.Ids[i]); level.SortOrder = i; db.Entry(level).Property(l => l.SortOrder).IsModified = true; }
            await db.SaveChangesAsync();
            return Results.Ok(levels.OrderBy(l => l.SortOrder));
        });
        admin.MapPost("/levels", async (LevelBody body, HubDb db) =>
        {
            var level = new OrgLevel { Name = Check.Required(body.Name, "name", 120), SortOrder = body.SortOrder ?? await db.Set<OrgLevel>().CountAsync() };
            Check.That(level.SortOrder >= 0, "sortOrder", "error.range");
            db.Set<OrgLevel>().Add(level); await db.SaveChangesAsync();
            return Results.Created($"/api/v1/admin/organisation/levels/{level.Id}", level);
        });
        admin.MapPatch("/levels/{id:guid}", async (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var level = await db.Set<OrgLevel>().FindAsync(id) ?? throw ApiException.NotFound();
            var patch = new Patch(body); await Http.CheckVersion(db, http, level, patch.RowVersion); if (patch.Has("name")) level.Name = Check.Required(patch.Str("name"), "name", 120); if (patch.Has("sortOrder")) level.SortOrder = patch.Int("sortOrder") ?? level.SortOrder;
            Check.That(level.SortOrder >= 0, "sortOrder", "error.range");
            await db.SaveChangesAsync(); return Results.Ok(level);
        });
        admin.MapGet("/units", async (HubDb db) => await db.Set<OrgUnit>().OrderBy(x => x.Name).ToListAsync());
        admin.MapPost("/units", async (UnitBody body, HubDb db) =>
        {
            await ValidateUnit(db, body.LevelId, body.ParentId, null);
            var unit = new OrgUnit { Name = Check.Required(body.Name, "name", 200), LevelId = body.LevelId, ParentId = body.ParentId, LeaderId = body.LeaderId, DisciplineId = body.DisciplineId, OfficeId = body.OfficeId };
            db.Set<OrgUnit>().Add(unit); await db.SaveChangesAsync(); return Results.Created($"/api/v1/admin/organisation/units/{unit.Id}", unit);
        });
        admin.MapPatch("/units/{id:guid}", async (Guid id, JsonElement body, HubDb db, HttpContext http) =>
        {
            var unit = await db.Set<OrgUnit>().FindAsync(id) ?? throw ApiException.NotFound(); var p = new Patch(body);
            await Http.CheckVersion(db, http, unit, p.RowVersion);
            var parent = p.Has("parentId") ? p.Id("parentId") : unit.ParentId; var level = p.Has("levelId") ? p.Id("levelId") : unit.LevelId;
            await ValidateUnit(db, level ?? throw ApiException.Invalid("levelId", "error.required"), parent, id);
            if (p.Has("name")) unit.Name = Check.Required(p.Str("name"), "name", 200); if (p.Has("parentId")) unit.ParentId = parent; if (p.Has("levelId")) unit.LevelId = level.Value;
            if (p.Has("leaderId")) unit.LeaderId = p.Id("leaderId"); if (p.Has("disciplineId")) unit.DisciplineId = p.Id("disciplineId"); if (p.Has("officeId")) unit.OfficeId = p.Id("officeId");
            await db.SaveChangesAsync(); return Results.Ok(unit);
        });
        admin.MapPost("/units/{id:guid}/close", async (Guid id, HubDb db, TimeProvider clock, HttpContext http) =>
        {
            var unit = await db.Set<OrgUnit>().FindAsync(id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, unit, null);
            Check.That(!await db.Set<OrgUnit>().AnyAsync(x => x.ParentId == id && x.ClosedOn == null), "unit", "org.open_children");
            Check.That(!await db.Users.AnyAsync(x => x.IsActive && EF.Property<Guid?>(x, "UnitId") == id), "unit", "org.active_people");
            unit.ClosedOn ??= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPatch("/users/{id:guid}/placement", async (Guid id, PlacementBody body, HubDb db, HttpContext http) =>
        {
            var user = await db.Users.FindAsync(id) ?? throw ApiException.NotFound();
            await Http.CheckVersion(db, http, user, body.RowVersion);
            if (body.UnitId is { } unit && !await db.Set<OrgUnit>().AnyAsync(x => x.Id == unit && x.ClosedOn == null)) throw ApiException.Invalid("unitId", "error.not_found");
            db.Entry(user).Property("UnitId").CurrentValue = body.UnitId; await db.SaveChangesAsync(); return Results.NoContent();
        });
        admin.MapPost("/import/preview", async (JsonElement body, HubDb db) => await Import(body, db, false));
        admin.MapPost("/import/apply", async (JsonElement body, HubDb db) => await Tx.Run(db, async () => await Import(body, db, true)));
    }

    sealed record ImportRow(string UnitPath, string Level, string LeaderEmail, string Discipline, string Office, string Email, string ManagerEmail);

    static async Task<IResult> Import(JsonElement body, HubDb db, bool apply)
    {
        var csv = body.ValueKind == JsonValueKind.String ? body.GetString() : body.TryGetProperty("csv", out var value) ? value.GetString() : null;
        Check.That(!string.IsNullOrWhiteSpace(csv), "csv", "error.required");
        var rows = Parse(csv!); var errors = new List<string>();
        var levels = await db.Set<OrgLevel>().AsNoTracking().ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var users = await db.Users.ToListAsync();
        var userByEmail = users.ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);
        var disciplines = await db.Disciplines.Where(d => d.IsActive).ToDictionaryAsync(d => d.Name, d => d.Id, StringComparer.OrdinalIgnoreCase);
        var offices = await db.Offices.Where(o => o.IsActive).ToDictionaryAsync(o => o.Name, o => o.Id, StringComparer.OrdinalIgnoreCase);
        var existing = await db.Set<OrgUnit>().ToListAsync();
        var planned = existing.ToDictionary(u => u.Id, u => new OrgUnit { Id = u.Id, Name = u.Name, ParentId = u.ParentId, LevelId = u.LevelId,
            LeaderId = u.LeaderId, DisciplineId = u.DisciplineId, OfficeId = u.OfficeId, ClosedOn = u.ClosedOn });
        string UnitPath(OrgUnit unit)
        {
            var names = new List<string>(); var seen = new HashSet<Guid>();
            for (OrgUnit? current = unit; current != null; current = current.ParentId is { } id ? planned.GetValueOrDefault(id) : null)
            { if (!seen.Add(current.Id)) throw ApiException.Invalid("csv", "org.cycle"); names.Add(current.Name); }
            names.Reverse(); return string.Join(" / ", names);
        }
        string Normalize(string path) => string.Join(" / ", path.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var byPath = planned.Values.ToDictionary(UnitPath, StringComparer.OrdinalIgnoreCase);
        var changes = new List<object>();
        foreach (var row in rows.Where(r => r.Level.Length > 0).OrderBy(r => r.UnitPath.Count(c => c == '/')))
        {
            var path = Normalize(row.UnitPath);
            if (path.Length == 0) { errors.Add("unit_path required for a unit"); continue; }
            if (!levels.TryGetValue(row.Level, out var level)) { errors.Add($"unknown level: {row.Level}"); continue; }
            var split = path.LastIndexOf(" / ", StringComparison.Ordinal);
            var parentPath = split < 0 ? null : path[..split];
            if (parentPath != null && !byPath.ContainsKey(parentPath)) { errors.Add($"parent path must be defined: {parentPath}"); continue; }
            var parentId = parentPath == null ? (Guid?)null : byPath[parentPath].Id;
            if (!byPath.TryGetValue(path, out var unit))
            {
                unit = new OrgUnit { Name = split < 0 ? path : path[(split + 3)..], ParentId = parentId, LevelId = level.Id };
                planned.Add(unit.Id, unit); byPath.Add(path, unit);
            }
            else unit.LevelId = level.Id;
            if (unit.ClosedOn != null) errors.Add($"closed unit: {path}");
            if (row.LeaderEmail.Length > 0)
            { if (!userByEmail.TryGetValue(row.LeaderEmail, out var leader) || !leader.IsActive) errors.Add($"unknown leader: {row.LeaderEmail}"); else unit.LeaderId = leader.Id; }
            if (row.Discipline.Length > 0)
            { if (!disciplines.TryGetValue(row.Discipline, out var discipline)) errors.Add($"unknown discipline: {row.Discipline}"); else unit.DisciplineId = discipline; }
            if (row.Office.Length > 0)
            { if (!offices.TryGetValue(row.Office, out var office)) errors.Add($"unknown office: {row.Office}"); else unit.OfficeId = office; }
            var original = existing.FirstOrDefault(u => u.Id == unit.Id);
            changes.Add(new { kind = original == null ? "create" : original.LevelId != unit.LevelId || original.LeaderId != unit.LeaderId || original.DisciplineId != unit.DisciplineId || original.OfficeId != unit.OfficeId ? "update" : "unchanged", unitPath = path });
        }
        var orders = levels.Values.ToDictionary(l => l.Id, l => l.SortOrder);
        foreach (var unit in planned.Values)
            if (unit.ParentId is { } parent && (!planned.TryGetValue(parent, out var p) || unit.ClosedOn == null && p.ClosedOn != null || !OrgTree.IsLevelOrderValid(orders[unit.LevelId], orders[p.LevelId]))) errors.Add($"invalid level order: {unit.Name}");
        var placements = new List<(AppUser User, Guid? UnitId, Guid? ManagerId)>();
        errors.AddRange(rows.Where(r => r.Email.Length > 0).GroupBy(r => r.Email, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => $"duplicate email: {g.Key}"));
        var managers = users.ToDictionary(u => u.Id, u => u.SupervisorId);
        foreach (var row in rows.Where(r => r.Email.Length > 0))
        {
            if (!userByEmail.TryGetValue(row.Email, out var user) || !user.IsActive) { errors.Add($"unknown person: {row.Email}"); continue; }
            Guid? unit = user.UnitId, manager = user.SupervisorId;
            if (row.UnitPath.Length > 0)
            { if (!byPath.TryGetValue(Normalize(row.UnitPath), out var target) || target.ClosedOn != null) errors.Add($"unknown unit: {row.UnitPath}"); else unit = target.Id; }
            if (row.ManagerEmail.Length > 0)
            { if (!userByEmail.TryGetValue(row.ManagerEmail, out var boss) || !boss.IsActive) errors.Add($"unknown manager: {row.ManagerEmail}"); else manager = boss.Id; }
            managers[user.Id] = manager;
            placements.Add((user, unit, manager)); changes.Add(new { kind = unit != user.UnitId || manager != user.SupervisorId ? "move" : "unchanged", email = row.Email });
        }
        foreach (var placement in placements)
            if (OrgTree.CreatesCycle(placement.User.Id, placement.ManagerId, managers)) errors.Add($"manager cycle: {placement.User.Email}");
        if (errors.Count > 0)
        {
            if (apply) throw ApiException.Rule("org.import_invalid", "error.validation", new { errors, applied = false });
            return Results.UnprocessableEntity(new { errors, applied = false });
        }
        if (apply)
        {
            foreach (var unit in planned.Values)
            {
                var current = existing.FirstOrDefault(u => u.Id == unit.Id);
                if (current == null) db.Add(unit);
                else { current.LevelId = unit.LevelId; current.ParentId = unit.ParentId; current.Name = unit.Name;
                    current.LeaderId = unit.LeaderId; current.DisciplineId = unit.DisciplineId; current.OfficeId = unit.OfficeId; }
            }
            foreach (var placement in placements) { placement.User.UnitId = placement.UnitId; placement.User.SupervisorId = placement.ManagerId; }
            await db.SaveChangesAsync();
        }
        return Results.Ok(new { rows = rows.Count, changes, creates = planned.Count - existing.Count, applied = apply });
    }

    static List<ImportRow> Parse(string csv)
    {
        using var parser = new TextFieldParser(new StringReader(csv)) { HasFieldsEnclosedInQuotes = true, Delimiters = [","] };
        if (parser.EndOfData) return [];
        var headers = parser.ReadFields() ?? [];
        var wanted = new[] { "unit_path", "level", "leader_email", "discipline", "office", "email", "manager_email" };
        var index = wanted.ToDictionary(x => x, x => Array.FindIndex(headers, h => string.Equals(h?.Trim(), x, StringComparison.OrdinalIgnoreCase)));
        if (index["unit_path"] < 0) throw ApiException.Invalid("csv", "error.required");
        var rows = new List<ImportRow>();
        while (!parser.EndOfData)
        {
            var fields = parser.ReadFields() ?? [];
            string V(string key) => index[key] >= 0 && index[key] < fields.Length ? fields[index[key]].Trim() : "";
            rows.Add(new ImportRow(V("unit_path"), V("level"), V("leader_email"), V("discipline"), V("office"), V("email"), V("manager_email")));
        }
        return rows;
    }

    static async Task<string> Path(HubDb db, OrgUnit unit)
    {
        var names = new List<string>(); var current = unit; var seen = new HashSet<Guid>();
        while (seen.Add(current.Id)) { names.Add(current.Name); if (current.ParentId is not { } parent) break; current = await db.Set<OrgUnit>().FirstAsync(x => x.Id == parent); }
        names.Reverse(); return string.Join(" / ", names);
    }

    static async Task ValidateUnit(HubDb db, Guid levelId, Guid? parentId, Guid? movingId)
    {
        var level = await db.Set<OrgLevel>().Where(x => x.Id == levelId).Select(x => (int?)x.SortOrder).FirstOrDefaultAsync() ?? throw ApiException.Invalid("levelId", "error.not_found");
        if (parentId is not { } parent) return;
        var p = await db.Set<OrgUnit>().Where(x => x.Id == parent).Select(x => new { x.LevelId, x.ClosedOn }).FirstOrDefaultAsync() ?? throw ApiException.Invalid("parentId", "error.not_found");
        Check.That(p.ClosedOn == null, "parentId", "org.open_children");
        var parentOrder = await db.Set<OrgLevel>().Where(x => x.Id == p.LevelId).Select(x => x.SortOrder).FirstAsync();
        Check.That(OrgTree.IsLevelOrderValid(level, parentOrder), "parentId", "org.level_order");
        var parents = await db.Set<OrgUnit>().Select(x => new { x.Id, x.ParentId }).ToDictionaryAsync(x => x.Id, x => x.ParentId);
        if (movingId is { } moving) Check.That(!OrgTree.CreatesCycle(moving, parent, parents), "parentId", "org.cycle");
    }
}

public sealed class OrgWriteFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Method == "GET") return await next(context);
        var db = context.HttpContext.RequestServices.GetRequiredService<HubDb>();
        return await Tx.Run(db, async () =>
        {
            await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({"hub-org-structure"}))");
            return await next(context);
        });
    }
}

public static class OrgStructureValidation
{
    public static async Task BeforeSave(HubDb db, CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        foreach (var entry in db.ChangeTracker.Entries<AppUser>().Where(e => e.State is EntityState.Added or EntityState.Modified)) users[entry.Entity.Id] = entry.Entity;
        var units = await db.Set<OrgUnit>().AsNoTracking().ToDictionaryAsync(u => u.Id, ct);
        foreach (var entry in db.ChangeTracker.Entries<OrgUnit>().Where(e => e.State is EntityState.Added or EntityState.Modified)) units[entry.Entity.Id] = entry.Entity;
        var levels = await db.Set<OrgLevel>().AsNoTracking().ToDictionaryAsync(l => l.Id, l => l.SortOrder, ct);
        foreach (var entry in db.ChangeTracker.Entries<OrgLevel>().Where(e => e.State is EntityState.Added or EntityState.Modified)) levels[entry.Entity.Id] = entry.Entity.SortOrder;
        Check.That(levels.Values.All(v => v >= 0) && levels.Values.Distinct().Count() == levels.Count, "sortOrder", "error.invalid");
        var parents = units.ToDictionary(u => u.Key, u => u.Value.ParentId);
        foreach (var unit in units.Values)
        {
            Check.That(unit.ClosedOn != null || unit.ParentId == null || units[unit.ParentId.Value].ClosedOn == null, "parentId", "org.open_children");
            Check.That(!OrgTree.CreatesCycle(unit.Id, unit.ParentId, parents), "parentId", "org.cycle");
            Check.That(unit.ParentId == null || units.TryGetValue(unit.ParentId.Value, out var parent) && OrgTree.IsLevelOrderValid(levels[unit.LevelId], levels[parent.LevelId]), "levelId", "org.level_order");
        }
        foreach (var entry in db.ChangeTracker.Entries<OrgUnit>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var unit = entry.Entity;
            Check.That(unit.LeaderId == null || users.TryGetValue(unit.LeaderId.Value, out var leader) && leader.IsActive, "leaderId", "team.inactive_user");
            if (unit.ClosedOn != null)
            {
                Check.That(!users.Values.Any(u => u.IsActive && u.UnitId == unit.Id), "unit", "org.active_people");
                Check.That(!units.Values.Any(u => u.ParentId == unit.Id && u.ClosedOn == null), "unit", "org.open_children");
            }
        }
        var managers = users.ToDictionary(u => u.Key, u => u.Value.SupervisorId);
        foreach (var entry in db.ChangeTracker.Entries<AppUser>().Where(e => e.State is EntityState.Added or EntityState.Modified))
        {
            var user = entry.Entity;
            if (entry.State == EntityState.Added || entry.Property(u => u.SupervisorId).IsModified)
                Check.That(!OrgTree.CreatesCycle(user.Id, user.SupervisorId, managers), "supervisorId", "org.cycle");
            if (entry.State == EntityState.Added || entry.Property(u => u.UnitId).IsModified || entry.Property(u => u.IsActive).IsModified && user.IsActive)
                Check.That(user.UnitId == null || units.TryGetValue(user.UnitId.Value, out var unit) && unit.ClosedOn == null, "unitId", "error.not_found");
        }
    }
}
