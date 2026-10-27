using System.Linq.Expressions;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Admin review (§55.4, FR-ADR-01..05, FR-ADR-07 API): nine computed checks, each a count that opens its list, and
/// recorded reviews. A check's count is its list's length, so the two always agree (FR-ADR-02). Nothing here edits
/// anything else: every fix goes through the existing tool that `Link` names (FR-ADR-05).
// ponytail: a count loads its whole list (organisation-sized: users, live projects, settings); count in SQL over the
// same query if a list ever grows past a few thousand rows.
public static class AdminReviewEndpoints
{
    public sealed record CheckRow(Guid? Id, string Name, string? Detail, DateTimeOffset? At, string Link);
    public sealed record CheckCount(string Key, string Title, string Rule, int Count, string ListUrl);
    public sealed record CheckList(string Key, string Title, string Rule, IReadOnlyList<CheckRow> Rows);
    public sealed record CheckRecord(string Key, int Count, string? Note);
    sealed record ExportRow(string Check, string Rule, int Count, string? Note);
    public sealed record ReviewRecord(Guid Id, DateTimeOffset ReviewedAt, Guid ReviewerId, string Reviewer, string? Note, IReadOnlyList<CheckRecord> Checks);
    public sealed record Overview(IReadOnlyList<CheckCount> Checks, ReviewRecord? Last, DateOnly? DueOn, bool Overdue);
    public sealed record CheckNote(string? Key, string? Note);
    public sealed record RecordBody(string? Note, IReadOnlyList<CheckNote>? Checks);

    public sealed record AdminReviewStripCount(string Key, string Title, int Count);
    public sealed record AdminReviewStrip(DateTimeOffset? ReviewedAt, IReadOnlyList<AdminReviewStripCount> Checks);

    public const string Flag = "admin_review";

    sealed record Ctx(HubDb Db, OrgSettings Settings, DateTimeOffset Now, DateTimeOffset? LastReviewAt);
    sealed record Def(string Key, Func<Ctx, Task<List<CheckRow>>> Rows, string? Flag = null, Func<OrgSettings, int>? Days = null);

    static readonly string[] LiveStatuses = [ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold];

    // FR-ADR-01, in the specification's order. A new check needs a specification change (051-D8).
    static readonly Def[] Defs =
    [
        new("inactiveOnTeams", c => People(c, u => !u.IsActive
            && (c.Db.Projects.Any(p => p.ProjectManagerId == u.Id && LiveStatuses.Contains(p.Status))
                || c.Db.ProjectMembers.Any(m => m.UserId == u.Id && m.RemovedAt == null
                    && (m.Roles.Contains(ProjectRole.PM) || m.Roles.Contains(ProjectRole.TeamMember) || m.Roles.Contains(ProjectRole.Reviewer))
                    && c.Db.Projects.Any(p => p.Id == m.ProjectId && LiveStatuses.Contains(p.Status)))
                || c.Db.ProjectDisciplines.Any(d => d.LeadUserId == u.Id && d.IsActive
                    && c.Db.Projects.Any(p => p.Id == d.ProjectId && LiveStatuses.Contains(p.Status)))),
            u => $"/people/{u}/reassign-work")),
        new("noManager", c => People(c, u => u.IsActive && u.SupervisorId == null, _ => "/admin/users")),
        new("noHomeUnit", c => People(c, u => u.IsActive && u.UnitId == null, _ => "/admin/structure"), Flag: "org_structure"),
        new("inAppRoles", async c => Sorted(await c.Db.UserRoles.AsNoTracking().Where(r => r.Source == RoleSource.Manual)
            .Join(c.Db.Users.Where(u => u.IsActive), r => r.UserId, u => u.Id, (r, u) => new CheckRow(u.Id, u.DisplayName, r.Role, r.GrantedAt, "/admin/users"))
            .ToListAsync())),
        new("noSignIn", c =>
        {
            var before = c.Now.AddDays(-c.Settings.AdminReviewIdleDays);
            return People(c, u => u.IsActive && (u.LastSignInAt ?? u.CreatedAt) < before, _ => "/admin/users", u => u.LastSignInAt);
        }, Days: s => s.AdminReviewIdleDays),
        new("inactiveProjectManager", async c => await c.Db.Projects.AsNoTracking()
            .Where(p => LiveStatuses.Contains(p.Status) && c.Db.Users.Any(u => u.Id == p.ProjectManagerId && !u.IsActive))
            .OrderBy(p => p.ProjectNumber)
            .Select(p => new CheckRow(p.Id, p.ProjectNumber + " " + p.Name, c.Db.Users.Where(u => u.Id == p.ProjectManagerId).Select(u => u.DisplayName).FirstOrDefault(),
                null, "/projects/" + p.ProjectNumber + "/settings")).ToListAsync()),
        new("idleProjects", async c =>
        {
            var before = c.Now.AddDays(-c.Settings.StatusStaleDays);
            return Sorted((await c.Db.Projects.AsNoTracking().Where(p => p.Status == ProjectStatus.Active || p.Status == ProjectStatus.Setup)
                .Select(p => new CheckRow(p.Id, p.ProjectNumber + " " + p.Name, null,
                    c.Db.ActivityLog.Where(a => a.ProjectId == p.Id).Max(a => (DateTimeOffset?)a.OccurredAt) ?? p.CreatedAt, "/projects/" + p.ProjectNumber))
                .ToListAsync()).Where(r => r.At < before));
        }, Days: s => s.StatusStaleDays),
        // A seeded default ("Created" by the migration step) is not a change; every later write is, flags included.
        new("settingsChanged", async c =>
        {
            var q = c.Db.ActivityLog.AsNoTracking().Where(a => a.ItemType == ItemType.Setting && a.Action != "Created" && a.ItemKey != null);
            if (c.LastReviewAt is { } since) q = q.Where(a => a.OccurredAt > since);
            return Sorted(await q.GroupBy(a => a.ItemKey!).Select(g => new CheckRow(null, g.Key, null, g.Max(a => (DateTimeOffset?)a.OccurredAt), "/admin/settings")).ToListAsync());
        }),
        new("inactiveReference", async c =>
        {
            var live = c.Db.Projects.Where(p => LiveStatuses.Contains(p.Status)).Select(p => p.Id);
            return [
                .. await StillUsed<Discipline>(c, "disciplines", x => c.Db.ProjectDisciplines.Any(d => d.DisciplineId == x.Id && d.IsActive && live.Contains(d.ProjectId))),
                .. await StillUsed<Office>(c, "offices", x => c.Db.Projects.Any(p => p.OfficeId == x.Id && live.Contains(p.Id))),
                .. await StillUsed<Client>(c, "clients", x => c.Db.Projects.Any(p => p.ClientId == x.Id && live.Contains(p.Id))),
                .. await StillUsed<DeliverableType>(c, "deliverableTypes", x => c.Db.Deliverables.Any(d => d.DeliverableTypeId == x.Id && live.Contains(d.ProjectId))),
                .. await StillUsed<Phase>(c, "phases", x => c.Db.Projects.Any(p => p.PhaseId == x.Id && live.Contains(p.Id))),
            ];
        }),
    ];

    static async Task<List<CheckRow>> People(Ctx c, Expression<Func<AppUser, bool>> rule, Func<Guid, string> link, Func<AppUser, DateTimeOffset?>? at = null) =>
        [.. (await c.Db.Users.AsNoTracking().Where(rule).OrderBy(u => u.DisplayName).ToListAsync())
            .Select(u => new CheckRow(u.Id, u.DisplayName, u.Email, at?.Invoke(u), link(u.Id)))];

    static List<CheckRow> Sorted(IEnumerable<CheckRow> rows) => [.. rows.OrderBy(r => r.Name, StringComparer.Ordinal).ThenBy(r => r.Detail, StringComparer.Ordinal)];

    /// Deactivated reference data still set on a live record; `kind` is the reference list it is reactivated from.
    static Task<List<CheckRow>> StillUsed<T>(Ctx c, string kind, Expression<Func<T, bool>> used) where T : RefData =>
        c.Db.Set<T>().AsNoTracking().Where(x => !x.IsActive).Where(used).OrderBy(x => x.Name)
            .Select(x => new CheckRow(x.Id, x.Name, kind, null, "/admin/reference/" + kind)).ToListAsync();

    static IEnumerable<Def> Shown(OrgSettings s) => Defs.Where(k => k.Flag is null || s.IsFlagOn(k.Flag));

    static string Title(Def k, OrgSettings s) => k.Days is { } d ? Text.Get($"adminReview.{k.Key}", d(s)) : Text.Get($"adminReview.{k.Key}");
    static string Rule(Def k, OrgSettings s) => k.Days is { } d ? Text.Get($"adminReview.{k.Key}.rule", d(s)) : Text.Get($"adminReview.{k.Key}.rule");

    static async Task<Ctx> Context(HubDb db, SettingsStore store, TimeProvider clock) =>
        new(db, await store.Get(db), clock.GetUtcNow(), await db.Set<AdminReview>().MaxAsync(r => (DateTimeOffset?)r.ReviewedAt));

    public static void Map(RouteGroupBuilder api)
    {
        // Like every Admin route, a non-Admin is refused first (AC-OPS-17); with the flag off an Admin finds nothing (FR-ADR-08).
        var g = api.MapGroup("/admin/review").AddEndpointFilter(async (ctx, next) =>
        {
            Access.Demand(Permissions.Administer(ctx.HttpContext.RequestServices.GetRequiredService<CurrentUser>().Actor));
            return await next(ctx);
        }).RequireFlag(Flag);

        g.MapGet("", async (HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var c = await Context(db, store, clock);
            var checks = new List<CheckCount>();
            foreach (var k in Shown(c.Settings))
                checks.Add(new(k.Key, Title(k, c.Settings), Rule(k, c.Settings), (await k.Rows(c)).Count, $"/admin/review/checks/{k.Key}"));
            var last = await db.Set<AdminReview>().AsNoTracking().OrderByDescending(r => r.ReviewedAt).FirstOrDefaultAsync();
            var (due, overdue) = Due(last?.ReviewedAt, c.Settings, clock.Today(c.Settings));
            return TypedResults.Ok(new Overview(checks, last is null ? null : await Record(db, last), due, overdue));
        });

        g.MapGet("/checks/{key}", async (string key, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var c = await Context(db, store, clock);
            var k = Shown(c.Settings).FirstOrDefault(x => x.Key == key) ?? throw ApiException.NotFound();
            return TypedResults.Ok(new CheckList(k.Key, Title(k, c.Settings), Rule(k, c.Settings), await k.Rows(c)));
        });

        g.MapGet("/{id:guid}/export", ExportReview);

        // FR-ADR-03: the counts are taken here, at this moment, never from the client.
        g.MapPost("", async (RecordBody body, HubDb db, SettingsStore store, CurrentUser me, TimeProvider clock) =>
        {
            var c = await Context(db, store, clock);
            var shown = Shown(c.Settings).ToList();
            var notes = body.Checks ?? [];
            foreach (var n in notes) Check.That(shown.Any(k => k.Key == n.Key), "checks", "error.invalid");
            var checks = new List<CheckRecord>();
            foreach (var k in shown)
                checks.Add(new(k.Key, (await k.Rows(c)).Count, Check.Optional(notes.FirstOrDefault(n => n.Key == k.Key)?.Note, "checks", 2000)));
            var review = new AdminReview
            {
                ReviewedAt = c.Now, ReviewerId = me.Id, Note = Check.Optional(body.Note, "note"),
                Checks = JsonSerializer.Serialize(checks, JsonOpts.Web),
            };
            db.Set<AdminReview>().Add(review);
            db.LogEvent("AdminReview", review.Id, "Recorded", "admin", name: Text.Get("adminReview.title"), reason: review.Note);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/admin/review/{review.Id}", await Record(db, review));
        });
    }

    static readonly Col[] ExportCols = [new("check", "check"), new("rule", "rule"), new("count", "count", "number"), new("note", "note")];

    /// FR-ADR-06: one recorded review as CSV or XLSX, a header block (date, reviewer, note) then one row per check. Only counts and
    /// the reviewer's own notes leave the Hub, never a person list (§8.7); each export is logged like every other (§20.1).
    static async Task<IResult> ExportReview(Guid id, string? format, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var r = await db.Set<AdminReview>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var s = await store.Get(db);
        var rec = await Record(db, r);
        var rows = rec.Checks.Select(k => Defs.FirstOrDefault(d => d.Key == k.Key) is { } d
            ? new ExportRow(Title(d, s), Rule(d, s), k.Count, k.Note) : new ExportRow(k.Key, "", k.Count, k.Note));
        (string, string)[] header = [(Text.Get("col.date"), Clock.Local(rec.ReviewedAt, s).ToString("yyyy-MM-dd HH:mm")), (Text.Get("col.reviewer"), rec.Reviewer), (Text.Get("col.note"), rec.Note ?? "")];
        return await ExportFile.Send(db, store, format, Text.Get("adminReview.title"), ExportCols, JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), [], null, "admin-review", clock, header);
    }

    /// FR-ADR-04: the next review is due `admin_review_interval_days` after the last; due now when none was ever recorded.
    /// Shared by the page, the daily digest line and the Portfolio strip so they always agree.
    public static (DateOnly? DueOn, bool Overdue) Due(DateTimeOffset? lastReviewedAt, OrgSettings s, DateOnly today)
    {
        var due = lastReviewedAt is { } at ? Clock.LocalDate(at, s).AddDays(s.AdminReviewIntervalDays) : (DateOnly?)null;
        return (due, due is null || due < today);
    }

    /// FR-ADR-07: what Executives see on the Portfolio strip: the last recorded review's date and counts, nothing else.
    public static async Task<AdminReviewStrip> LastReview(HubDb db, OrgSettings s)
    {
        var last = await db.Set<AdminReview>().AsNoTracking().OrderByDescending(r => r.ReviewedAt).FirstOrDefaultAsync();
        var recorded = last is null ? [] : JsonSerializer.Deserialize<List<CheckRecord>>(last.Checks, JsonOpts.Web) ?? [];
        return new(last?.ReviewedAt, [.. Shown(s).Join(recorded, k => k.Key, r => r.Key, (k, r) => new AdminReviewStripCount(k.Key, Title(k, s), r.Count))]);
    }

    static async Task<ReviewRecord> Record(HubDb db, AdminReview r) => new(r.Id, r.ReviewedAt, r.ReviewerId,
        await db.Users.Where(u => u.Id == r.ReviewerId).Select(u => u.DisplayName).FirstAsync(), r.Note,
        JsonSerializer.Deserialize<List<CheckRecord>>(r.Checks, JsonOpts.Web) ?? []);
}
