using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic.FileIO;

namespace Hub.Api.Features;

/// FR-RES-18..23: HR is the approval authority; every write is atomic and version checked.
public static class AvailabilityEndpoints
{
    public sealed record CsvBody(string Csv);
    public sealed record ImportRow(string SourceSystem, string ExternalId, string PersonKey, Guid PersonId, int PersonVersion,
        DateOnly FromDate, DateOnly ThroughDate, decimal? HoursPerDay, string Status, DateTimeOffset SourceUpdatedAt, int RowVersion);
    public sealed record ImportBody(Guid PreviewId, ImportRow[] Rows);
    public sealed record ReconcileBody(Guid PersonId, Guid LeaveId, PlanningEndpoints.DateVersionInput[] Dates);
    public sealed record ManualBody(Guid PersonId, string SourceSystem, string ExternalId, DateOnly FromDate, DateOnly ThroughDate, decimal? HoursPerDay, string Status, int RowVersion);
    public sealed record IdentityBody(string? SourceSystem, string? PersonKey, int RowVersion);
    public sealed record ScheduleBody(DateOnly FromDate, decimal WeeklyHours, int[] WorkingDays, int RowVersion);
    public static readonly string[] Headers = ["source_system", "external_id", "person_key", "status", "start_date", "end_date", "hours_per_day", "source_updated_at"];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/planning/availability", View);
        api.MapGet("/planning/availability/export", Export);
        api.MapGet("/planning/hr-identities", async (Access access, HubDb db) => {
            Access.Demand(Permissions.Administer(access.Actor));
            return await db.Users.AsNoTracking().Where(u => u.IsActive).OrderBy(u => u.DisplayName)
                .Select(u => new { u.Id, u.DisplayName, u.HrSourceSystem, u.HrPersonKey, u.RowVersion }).ToListAsync();
        });
        api.MapPut("/planning/hr-identities/{id:guid}", async (Guid id, IdentityBody body, Access access, HubDb db) => {
            Access.Demand(Permissions.Administer(access.Actor));
            var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id && u.IsActive) ?? throw ApiException.NotFound();
            Version(user.RowVersion, body.RowVersion);
            user.HrSourceSystem = Clean(body.SourceSystem); user.HrPersonKey = Clean(body.PersonKey);
            Check.That((user.HrSourceSystem == null) == (user.HrPersonKey == null), "personKey", "availability.identity_pair");
            if (user.HrSourceSystem != null && await db.Users.AnyAsync(u => u.Id != id && u.HrSourceSystem == user.HrSourceSystem && u.HrPersonKey == user.HrPersonKey))
                throw ApiException.Invalid("personKey", "availability.identity_duplicate");
            await db.SaveChangesAsync(); return Results.Ok(new { user.RowVersion });
        });
        api.MapPost("/planning/leave/import-preview", Preview);
        api.MapPost("/planning/leave/import", Apply);
        api.MapPost("/planning/leave/manual", Manual);
        api.MapPost("/planning/leave/reconcile", Reconcile);
        api.MapPut("/planning/schedules/{personId:guid}", Schedule);
    }

    static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : Check.Required(value, "identity", 120);
    internal static void Version(int actual, int expected) { if (actual != expected) throw ApiException.Conflict("concurrency_conflict", "coord.stale"); }
    static void Dates(DateOnly from, DateOnly through) => Check.That(from != default && through >= from && through < DateOnly.MaxValue && through.DayNumber - from.DayNumber <= 365, "throughDate", "error.date_range");
    static void Validate(string source, string external, DateOnly from, DateOnly through, decimal? hours, string status)
    {
        Check.Required(source, "sourceSystem", 120); Check.Required(external, "leaveId", 120); Dates(from, through);
        Check.That((hours is null or (> 0 and <= 24)) && (hours == null || hours == Math.Round(hours.Value, 2)), "hoursPerDay", "error.range");
        Check.OneOf(status, ["Approved", "Cancelled"], "status");
    }

    static IQueryable<AppUser> People(HubDb db, Access access)
    {
        if (access.Actor.ReadOnly) throw ApiException.Forbidden("perm.read_only");
        if (access.Actor.Admin || access.Actor.Executive || access.Actor.Supervisor) return db.Users.AsNoTracking().Where(u => u.IsActive);
        if (access.Actor.SystemPM) return PlanningEndpoints.People(db, access);
        var supervisor = access.Me.SupervisorId;
        return db.Users.AsNoTracking().Where(u => u.IsActive && (u.Id == access.Actor.Id || supervisor != null && u.SupervisorId == supervisor));
    }

    static bool Manage(Access access, AppUser user) => access.Actor.Admin || access.Actor.Supervisor && user.SupervisorId == access.Actor.Id;

    static async Task<object> View(DateOnly from, DateOnly through, Guid? personId, Access access, HubDb db, SettingsStore store)
    {
        Dates(from, through); var people = People(db, access); if (personId is { } id) people = people.Where(u => u.Id == id);
        var users = await people.OrderBy(u => u.DisplayName).ToListAsync(); var ids = users.Select(u => u.Id).ToArray();
        var capacity = await PersonCapacity.Load(db, ids, from, through, await store.Get(db));
        var leaves = await db.ApprovedLeaves.AsNoTracking().Where(l => ids.Contains(l.PersonId) && l.FromDate <= through && l.ThroughDate >= from).OrderBy(l => l.FromDate).ToListAsync();
        var schedules = await db.PersonSchedules.AsNoTracking().Where(s => ids.Contains(s.PersonId)).OrderBy(s => s.FromDate).ToListAsync();
        var legacy = await db.AvailabilityOverrides.AsNoTracking().Where(o => ids.Contains(o.PersonId) && o.WorkDate >= from && o.WorkDate <= through && o.Category != "Additional" && o.ReconciledLeaveId == null).ToListAsync();
        var holidays = await db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= through).OrderBy(h => h.Date).ToListAsync();
        var pids = access.VisibleProjectIds();
        var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.DeletedAt == null && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled
            && (ids.Contains(t.AssigneeId ?? Guid.Empty) || ids.Contains(t.ReviewerId ?? Guid.Empty)) && (t.DueDate == null || t.DueDate >= from && t.DueDate <= through))
            .Select(t => new { t.Id, t.Key, t.Name, t.ProjectId, t.AssigneeId, t.ReviewerId, t.DueDate }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.DeletedAt == null && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Issued
            && (ids.Contains(d.OwnerId ?? Guid.Empty) || ids.Contains(d.ReviewerId ?? Guid.Empty)) && (d.DueDate == null || d.DueDate >= from && d.DueDate <= through))
            .Select(d => new { d.Id, d.Key, d.Name, d.ProjectId, d.OwnerId, d.ReviewerId, d.DueDate }).ToListAsync();
        var concerns = new List<object>();
        void Concern(Guid person, Guid id, string key, string name, string type, Guid project, DateOnly? due, string role)
        {
            if (!ids.Contains(person)) return;
            var day = due is { } date ? capacity.GetValueOrDefault((person, date)) : null;
            if (due != null && (day == null || day.Leave <= 0 && day.Holiday <= 0)) return;
            concerns.Add(new { PersonId = person, Id = id, Key = key, Name = name, Type = type, ProjectId = project, DueDate = due, Role = role,
                Concern = due == null ? "Unknown" : day!.Available == 0 ? "Unavailable" : "PartialAbsence" });
        }
        foreach (var t in tasks) { if (t.AssigneeId is { } a) Concern(a, t.Id, t.Key, t.Name, "Task", t.ProjectId, t.DueDate, "Assignee"); if (t.ReviewerId is { } r) Concern(r, t.Id, t.Key, t.Name, "Task", t.ProjectId, t.DueDate, "Reviewer"); }
        foreach (var d in deliverables) { if (d.OwnerId is { } a) Concern(a, d.Id, d.Key, d.Name, "Deliverable", d.ProjectId, d.DueDate, "Owner"); if (d.ReviewerId is { } r) Concern(r, d.Id, d.Key, d.Name, "Deliverable", d.ProjectId, d.DueDate, "Reviewer"); }
        var reviews = await db.DisciplineReviews.AsNoTracking().Where(r => ids.Contains(r.ReviewerId) && pids.Contains(r.ProjectId)
            && r.DueDate >= from && r.DueDate <= through && r.Status != DisciplineReviewStatus.Approved
            && db.ReviewPackages.Any(p => p.CurrentRoundId == r.RoundId && p.Status != ReviewStatus.Cancelled && p.Status != ReviewStatus.Superseded && p.Status != ReviewStatus.Draft))
            .Select(r => new { r.ReviewerId, r.ProjectId, r.DueDate, PackageId = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Id).First(), Package = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Title).First() }).ToListAsync();
        foreach (var review in reviews) Concern(review.ReviewerId, review.PackageId, "", review.Package, "Review", review.ProjectId, review.DueDate, "Reviewer");
        return new {
            From = from, Through = through, CanImport = access.Actor.Admin,
            People = users.Select(u => new { u.Id, u.DisplayName, CanManage = Manage(access, u), WeeklyHours = u.WeeklyCapacityHours,
                Days = capacity.Where(x => x.Key.Item1 == u.Id).Select(x => new { Date = x.Key.Item2, x.Value.Normal, x.Value.Available, x.Value.Leave, x.Value.Holiday, x.Value.Provisional }),
                Schedules = Manage(access, u) ? schedules.Where(s => s.PersonId == u.Id).Select(s => new { s.FromDate, s.WeeklyHours, s.WorkingDays, s.RowVersion }) : null,
                Leave = leaves.Where(l => l.PersonId == u.Id).Select(l => new { l.Id, l.FromDate, l.ThroughDate, l.HoursPerDay, l.Status, l.SourceKind, l.SourceUpdatedAt, l.ImportedAt, l.RowVersion,
                    SourceSystem = Manage(access, u) ? l.SourceSystem : null, ExternalId = Manage(access, u) ? l.ExternalId : null, CanEdit = Manage(access, u) && l.SourceKind == "ManualHR" }),
                Unverified = legacy.Where(o => o.PersonId == u.Id).Select(o => new { o.WorkDate, o.AvailableHours, o.RowVersion }) }),
            Holidays = holidays.Select(h => new { h.Date, h.Name, h.OfficeId }), Coverage = concerns,
            Imports = access.Actor.Admin ? await db.LeaveImportStates.AsNoTracking().Select(s => new { s.SourceSystem, s.ImportedAt, s.SourceUpdatedAt }).ToListAsync() : null,
        };
    }

    static async Task<IResult> Export(DateOnly from, DateOnly through, Guid? personId, string? format, string? kind, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var view = JsonSerializer.SerializeToNode(await View(from, through, personId, access, db, store), JsonOpts.Web)!;
        var rows = new JsonArray();
        var leave = kind == "leave";
        foreach (var person in view["people"]!.AsArray())
            foreach (var item in person![leave ? "leave" : "days"]!.AsArray()) {
                var row = item!.DeepClone().AsObject(); row["person"] = person["displayName"]!.DeepClone(); rows.Add(row);
            }
        Col[] columns = leave ? [new("person", "person"), new("fromDate", "date", "date", "From"), new("throughDate", "date", "date", "Through"), new("hoursPerDay", "hours", "number", "Absent hours per day (blank = full day)"), new("status", "status"), new("sourceKind", "type"), new("sourceUpdatedAt", "date", Label: "Source updated"), new("importedAt", "date", Label: "Imported")]
            : [new("person", "person"), new("date", "date", "date"), new("normal", "hours", "number", "Contracted hours"), new("available", "hours", "number", "Available hours"), new("leave", "hours", "number", "Leave hours"), new("holiday", "hours", "number", "Holiday hours"), new("provisional", "status", Label: "Provisional")];
        return await ExportFile.Send(db, store, format, Text.Get("availability.export"), columns, rows, [("from", from.ToString("yyyy-MM-dd")), ("through", through.ToString("yyyy-MM-dd"))], null, leave ? "approved-leave" : "availability", clock);
    }

    static async Task<object> Preview(CsvBody body, Access access, HubDb db, TimeProvider clock, SettingsStore store)
    {
        Access.Demand(Permissions.Administer(access.Actor));
        Check.That(body.Csv != null && Encoding.UTF8.GetByteCount(body.Csv) <= 256 * 1024, "csv", "error.too_long", 262144);
        using var parser = new TextFieldParser(new StringReader(body.Csv!)) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(","); string[] header;
        try { header = parser.ReadFields() ?? []; }
        catch (MalformedLineException) { throw ApiException.Invalid("csv", "availability.csv_headers"); }
        if (header.Length > 0) header[0] = header[0].TrimStart('\uFEFF');
        Check.That(header.SequenceEqual(Headers), "csv", "availability.csv_headers");
        var users = await db.Users.AsNoTracking().Where(u => u.IsActive && u.HrSourceSystem != null).ToListAsync();
        var records = await db.ApprovedLeaves.AsNoTracking().ToListAsync();
        var rows = new List<ImportRow>(); var errors = new List<object>(); var identities = new Dictionary<(string, string), string>(); var line = 1;
        while (!parser.EndOfData)
        {
            Check.That(++line <= 1001, "csv", "availability.csv_limit");
            try {
                var cells = parser.ReadFields()!;
                if (cells.Length != Headers.Length) throw new FormatException();
                var source = cells[0]; var external = cells[1]; var key = cells[2];
                var signature = JsonSerializer.Serialize(cells);
                if (identities.TryGetValue((source, external), out var prior)) { if (prior == signature) continue; throw new FormatException(); }
                identities[(source, external)] = signature;
                var person = users.SingleOrDefault(u => u.HrSourceSystem == source && u.HrPersonKey == key);
                if (person == null) { errors.Add(new { Row = line, Error = "availability.unmapped" }); continue; }
                var from = DateOnly.ParseExact(cells[4], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var through = DateOnly.ParseExact(cells[5], "yyyy-MM-dd", CultureInfo.InvariantCulture);
                decimal? hours = cells[6].Length == 0 ? null : decimal.Parse(cells[6], CultureInfo.InvariantCulture);
                Validate(source, external, from, through, hours, cells[3]);
                var updated = DateTimeOffset.Parse(cells[7], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();
                Check.That(cells[7].EndsWith('Z') || cells[7].Length >= 6 && (cells[7][^6] is '+' or '-') && cells[7][^3] == ':', "sourceUpdatedAt", "availability.timestamp");
                Check.That(updated <= clock.GetUtcNow(), "sourceUpdatedAt", "availability.timestamp");
                var current = records.SingleOrDefault(l => l.SourceSystem == source && l.ExternalId == external);
                var row = new ImportRow(source, external, key, person.Id, person.RowVersion, from, through, hours, cells[3], updated, current?.RowVersion ?? 0);
                CheckCurrent(current, row); rows.Add(row);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException or MalformedLineException or ApiException) { errors.Add(new { Row = line, Error = "availability.invalid_row" }); }
        }
        Check.That(line > 1, "csv", "error.required");
        if (errors.Count == 0) {
            try { await ValidatePartial(db, rows, records, store); }
            catch (ApiException ex) { errors.Add(new { Row = 0, Error = ex.Errors?.Values.SelectMany(v => v).FirstOrDefault() ?? ex.Message }); }
        }
        Guid? previewId = null;
        if (errors.Count == 0) {
            var receipt = new LeaveImportPreview { UserId = access.Actor.Id, RowsHash = WeeklyPlanningEndpoints.Hash(JsonSerializer.Serialize(rows, JsonOpts.Web)), ExpiresAt = clock.GetUtcNow().AddHours(24) };
            db.LeaveImportPreviews.Add(receipt); await db.SaveChangesAsync(); previewId = receipt.Id;
        }
        bool Unchanged(ImportRow row) => records.Any(l => l.SourceSystem == row.SourceSystem && l.ExternalId == row.ExternalId && l.SourceKind == "HRImport" && Same(l, row) && l.SourceUpdatedAt == row.SourceUpdatedAt);
        return new { PreviewId = previewId, Rows = rows, Errors = errors, CanApply = errors.Count == 0,
            Creates = rows.Count(r => r.RowVersion == 0), Updates = rows.Count(r => r.RowVersion > 0 && !Unchanged(r) && r.Status != "Cancelled"),
            Cancellations = rows.Count(r => r.Status == "Cancelled" && !Unchanged(r)), Unchanged = rows.Count(Unchanged),
            ManualMatches = rows.Count(r => records.Any(l => l.SourceSystem == r.SourceSystem && l.ExternalId == r.ExternalId && l.SourceKind == "ManualHR")),
            AffectedWeeks = rows.Select(r => new { r.PersonId, FromWeek = Workload.WeekOf(r.FromDate), ThroughWeek = Workload.WeekOf(r.ThroughDate) }).Distinct() };

    }

    static async Task ValidatePartial(HubDb db, IReadOnlyList<ImportRow> rows, IReadOnlyList<ApprovedLeave> records, SettingsStore store)
    {
        var ids = rows.Select(r => r.PersonId).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var schedules = await db.PersonSchedules.AsNoTracking().Where(s => ids.Contains(s.PersonId)).OrderByDescending(s => s.FromDate).ToListAsync();
        var settings = await store.Get(db);
        var partial = records.Where(l => ids.Contains(l.PersonId) && !rows.Any(r => r.SourceSystem == l.SourceSystem && r.ExternalId == l.ExternalId))
            .Select(l => new ImportRow(l.SourceSystem, l.ExternalId, "", l.PersonId, 0, l.FromDate, l.ThroughDate, l.HoursPerDay, l.Status, l.SourceUpdatedAt, l.RowVersion))
            .Concat(rows).Where(r => r.Status == "Approved" && r.HoursPerDay != null).ToArray();
        foreach (var row in partial)
        for (var date = row.FromDate; date <= row.ThroughDate; date = date.AddDays(1)) {
            var schedule = schedules.FirstOrDefault(s => s.PersonId == row.PersonId && s.FromDate <= date);
            var days = schedule?.WorkingDays ?? [1, 2, 3, 4, 5];
            if (!days.Contains((int)date.DayOfWeek)) continue;
            Check.That(row.HoursPerDay <= (schedule?.WeeklyHours ?? users[row.PersonId].WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours) / days.Length, "hoursPerDay", "availability.schedule_hours");
            Check.That(!partial.Any(other => other != row && other.PersonId == row.PersonId && other.FromDate <= date && other.ThroughDate >= date), "rows", "availability.partial_overlap");
        }
    }

    static bool Same(ApprovedLeave l, ImportRow r) => l.PersonId == r.PersonId && l.FromDate == r.FromDate && l.ThroughDate == r.ThroughDate && l.HoursPerDay == r.HoursPerDay && l.Status == r.Status;
    static void CheckCurrent(ApprovedLeave? current, ImportRow row)
    {
        if (current == null) return;
        Check.That(current.PersonId == row.PersonId, "personKey", "availability.identity_changed");
        if (current.SourceKind == "ManualHR") return; // A manual copy's timestamp is not an HR export timestamp.
        if (row.SourceUpdatedAt < current.SourceUpdatedAt || row.SourceUpdatedAt == current.SourceUpdatedAt && !Same(current, row))
            throw ApiException.Conflict("stale_hr_record", "availability.stale_hr");
    }

    static async Task<IResult> Apply(ImportBody body, Access access, HubDb db, TimeProvider clock, SettingsStore store)
    {
        Access.Demand(Permissions.Administer(access.Actor));
        Check.That(body.Rows is { Length: > 0 and <= 1000 } && body.Rows.Select(r => (r.SourceSystem, r.ExternalId)).Distinct().Count() == body.Rows.Length, "rows", "error.invalid");
        return await Tx.Run(db, async () => {
            var preview = await db.LeaveImportPreviews.AsNoTracking().SingleOrDefaultAsync(p => p.Id == body.PreviewId && p.UserId == access.Actor.Id && p.ExpiresAt > clock.GetUtcNow());
            Check.That(preview != null && preview.RowsHash == WeeklyPlanningEndpoints.Hash(JsonSerializer.Serialize(body.Rows, JsonOpts.Web)), "previewId", "availability.preview_required");
            await PersonCapacity.LockPeople(db, body.Rows.Select(r => r.PersonId));
            foreach (var source in body.Rows.Select(r => r.SourceSystem).Distinct().Order())
                await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))", "hr:" + source);
            var users = await db.Users.AsNoTracking().Where(u => body.Rows.Select(r => r.PersonId).Contains(u.Id)).ToDictionaryAsync(u => u.Id);
            var systems = body.Rows.Select(r => r.SourceSystem).Distinct().ToArray();
            var records = await db.ApprovedLeaves.Where(l => systems.Contains(l.SourceSystem) || body.Rows.Select(r => r.PersonId).Contains(l.PersonId)).ToListAsync(); var changed = 0;
            await ValidatePartial(db, body.Rows, records, store);
            foreach (var row in body.Rows) {
                Validate(row.SourceSystem, row.ExternalId, row.FromDate, row.ThroughDate, row.HoursPerDay, row.Status);
                Check.That(row.SourceUpdatedAt <= clock.GetUtcNow() && row.SourceUpdatedAt != default, "sourceUpdatedAt", "availability.timestamp");
                if (!users.TryGetValue(row.PersonId, out var user) || !user.IsActive || user.HrSourceSystem != row.SourceSystem || user.HrPersonKey != row.PersonKey) throw ApiException.Conflict("hr_mapping_changed", "availability.mapping_changed");
                Version(user.RowVersion, row.PersonVersion);
                var current = records.SingleOrDefault(l => l.SourceSystem == row.SourceSystem && l.ExternalId == row.ExternalId);
                Version(current?.RowVersion ?? 0, row.RowVersion); CheckCurrent(current, row);
                if (current != null && Same(current, row) && current.SourceUpdatedAt == row.SourceUpdatedAt && current.SourceKind == "HRImport") continue;
                if (current == null) { current = new() { PersonId = row.PersonId, SourceSystem = row.SourceSystem, ExternalId = row.ExternalId }; db.ApprovedLeaves.Add(current); }
                else await PersonCapacity.Touch(db, current.PersonId, current.FromDate, current.ThroughDate);
                current.FromDate = row.FromDate; current.ThroughDate = row.ThroughDate; current.HoursPerDay = row.HoursPerDay; current.Status = row.Status;
                current.SourceKind = "HRImport"; current.SourceUpdatedAt = row.SourceUpdatedAt; current.ImportedAt = clock.GetUtcNow();
                await PersonCapacity.Touch(db, current.PersonId, current.FromDate, current.ThroughDate); changed++;
            }
            foreach (var source in systems) {
                var state = await db.LeaveImportStates.SingleOrDefaultAsync(s => s.SourceSystem == source);
                if (state == null) { state = new() { SourceSystem = source }; db.LeaveImportStates.Add(state); }
                state.ImportedAt = clock.GetUtcNow();
                var latest = body.Rows.Where(r => r.SourceSystem == source).Max(r => r.SourceUpdatedAt);
                if (latest > state.SourceUpdatedAt) state.SourceUpdatedAt = latest;
            }
            await db.SaveChangesAsync(); return Results.Ok(new { Applied = changed, Unchanged = body.Rows.Length - changed, ImportedAt = clock.GetUtcNow() });
        });
    }

    static async Task<IResult> Manual(ManualBody body, Access access, HubDb db, TimeProvider clock, SettingsStore store)
    {
        Validate(body.SourceSystem, body.ExternalId, body.FromDate, body.ThroughDate, body.HoursPerDay, body.Status);
        return await Tx.Run(db, async () => {
            await PersonCapacity.LockPeople(db, [body.PersonId]);
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == body.PersonId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Manage(access, user) ? Allow.Yes : Allow.No("perm.supervised_staff"));
            var row = await db.ApprovedLeaves.SingleOrDefaultAsync(l => l.SourceSystem == body.SourceSystem && l.ExternalId == body.ExternalId);
            Version(row?.RowVersion ?? 0, body.RowVersion);
            await ValidatePartial(db, [new(body.SourceSystem, body.ExternalId, "", body.PersonId, user.RowVersion, body.FromDate, body.ThroughDate, body.HoursPerDay, body.Status, clock.GetUtcNow(), body.RowVersion)], await db.ApprovedLeaves.AsNoTracking().Where(l => l.PersonId == body.PersonId).ToListAsync(), store);
            if (row != null) { Check.That(row.SourceKind == "ManualHR" && row.PersonId == body.PersonId, "leaveId", "availability.import_read_only"); await PersonCapacity.Touch(db, row.PersonId, row.FromDate, row.ThroughDate); }
            else { row = new() { PersonId = body.PersonId, SourceSystem = body.SourceSystem, ExternalId = body.ExternalId, SourceKind = "ManualHR" }; db.ApprovedLeaves.Add(row); }
            row.FromDate = body.FromDate; row.ThroughDate = body.ThroughDate; row.HoursPerDay = body.HoursPerDay; row.Status = body.Status;
            row.SourceUpdatedAt = row.ImportedAt = clock.GetUtcNow();
            await PersonCapacity.Touch(db, row.PersonId, row.FromDate, row.ThroughDate); await db.SaveChangesAsync();
            return Results.Ok(new { row.Id, row.RowVersion });
        });
    }

    static async Task<IResult> Reconcile(ReconcileBody body, Access access, HubDb db)
    {
        Check.That(body.Dates is { Length: > 0 and <= 366 } && body.Dates.Select(d => d.WorkDate).Distinct().Count() == body.Dates.Length, "dates", "error.invalid");
        return await Tx.Run(db, async () => {
            await PersonCapacity.LockPeople(db, [body.PersonId]);
            var person = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == body.PersonId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Manage(access, person) ? Allow.Yes : Allow.No("perm.supervised_staff"));
            var leave = await db.ApprovedLeaves.AsNoTracking().SingleOrDefaultAsync(l => l.Id == body.LeaveId && l.PersonId == body.PersonId) ?? throw ApiException.NotFound();
            var dates = body.Dates.Select(d => d.WorkDate).ToArray();
            Check.That(dates.All(d => d >= leave.FromDate && d <= leave.ThroughDate), "dates", "availability.reconcile_range");
            var rows = await db.AvailabilityOverrides.Where(o => o.PersonId == body.PersonId && dates.Contains(o.WorkDate) && o.Category != "Additional" && o.ReconciledLeaveId == null).ToListAsync();
            Check.That(rows.Count == dates.Length, "dates", "coord.stale");
            foreach (var row in rows) {
                Version(row.RowVersion, body.Dates.Single(d => d.WorkDate == row.WorkDate).RowVersion);
                row.ReconciledLeaveId = leave.Id; db.Audit.Note(row, reason: "Reconciled with an HR leave reference");
            }
            foreach (var date in dates) await PersonCapacity.Touch(db, person.Id, date, date);
            await db.SaveChangesAsync(); return Results.Ok(new { Count = rows.Count });
        });
    }

    static async Task<IResult> Schedule(Guid personId, ScheduleBody body, Access access, HubDb db, TimeProvider clock, SettingsStore store)
    {
        var today = clock.Today(await store.Get(db));
        Check.That(body.FromDate != default && body.FromDate >= today.AddYears(-2) && body.FromDate <= today.AddYears(2), "fromDate", "error.date_range");
        Check.That(body.WorkingDays is { Length: > 0 and <= 7 } && body.WorkingDays.All(d => d is >= 0 and <= 6) && body.WorkingDays.Distinct().Count() == body.WorkingDays.Length && body.WeeklyHours is >= 0 and <= 168 && body.WeeklyHours == Math.Round(body.WeeklyHours, 1) && body.WeeklyHours / body.WorkingDays.Length <= 24, "workingDays", "error.range");
        return await Tx.Run(db, async () => {
            await PersonCapacity.LockPeople(db, [personId]); var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == personId && u.IsActive) ?? throw ApiException.NotFound();
            Access.Demand(Manage(access, user) ? Allow.Yes : Allow.No("perm.supervised_staff"));
            var row = await db.PersonSchedules.SingleOrDefaultAsync(s => s.PersonId == personId && s.FromDate == body.FromDate); Version(row?.RowVersion ?? 0, body.RowVersion);
            if (row == null) { row = new() { PersonId = personId, FromDate = body.FromDate }; db.PersonSchedules.Add(row); }
            row.WeeklyHours = body.WeeklyHours; row.WorkingDays = body.WorkingDays.Order().ToArray();
            await PersonCapacity.Touch(db, personId, body.FromDate, today.AddYears(2)); await db.SaveChangesAsync(); return Results.Ok(new { row.Id, row.RowVersion });
        });
    }
}
