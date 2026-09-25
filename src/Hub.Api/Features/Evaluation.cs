using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Attention items, snoozes and the health override (§12.12, §16.4, FR-ATT-01..03, FR-HLT-01..03).
public static class EvaluationEndpoints
{
    public sealed record SnoozeBody(int Days, string Note);
    public sealed record OverrideBody(string Health, string Note, int? RowVersion);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/attention", async (Guid id, bool? includeSnoozed, Guid? disciplineId, string? severity, Access access, HubDb db, EvaluationService eval, TimeProvider clock) =>
        {
            await access.Project(id, track: false);
            await eval.EnsureFresh(id);
            return await Items(db, db.Attention.AsNoTracking().Where(a => a.ProjectId == id), clock.GetUtcNow(), includeSnoozed == true, disciplineId, severity);
        });

        api.MapPost("/attention/{id:guid}/snooze", async (Guid id, SnoozeBody body, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
        {
            var a = await db.Attention.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(a.ProjectId);
            Access.Demand(Permissions.Snooze(access.Actor, ctx));
            Check.That(body.Days is >= 1 and <= 30, "days", "attention.snooze_days"); // ATT-03
            var note = Check.Reason(body.Note, "note");
            var z = new AttentionSnooze { ProjectId = a.ProjectId, RuleId = a.RuleId, ItemType = a.ItemType, ItemId = a.ItemId, SnoozedBy = me.Id,
                SnoozedUntil = clock.GetUtcNow().AddDays(body.Days), Note = note, SeverityAtSnooze = a.Severity, CreatedAt = clock.GetUtcNow() };
            db.Snoozes.Add(z);
            db.LogEvent(ItemType.Snooze, a.ItemId, "Snoozed", "attention", a.ProjectId, a.ItemKey, $"{a.RuleId} {a.ItemName}",
                new[] { new { field = "SnoozedUntil", old = (object?)null, @new = (object)z.SnoozedUntil.ToString("yyyy-MM-dd") } }, note, a.ProjectDisciplineId);
            await db.SaveChangesAsync();
            return Results.Ok(new { z.Id, z.SnoozedUntil });
        });

        api.MapPost("/projects/{id:guid}/health-override", async (Guid id, OverrideBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.HealthOverride(access.Actor, ctx));
            await Http.CheckVersion(db, http, p, body.RowVersion);
            Check.OneOf(body.Health, Health.Overridable, "health"); // cannot set Grey (§16.4)
            var note = Check.Reason(body.Note, "note");
            var s = await store.Get(db);
            p.HealthOverride = body.Health;
            p.HealthOverrideNote = note;
            p.HealthOverrideBy = me.Id;
            p.HealthOverrideAt = clock.GetUtcNow();
            p.HealthOverrideExpiresAt = clock.GetUtcNow().AddDays(s.HealthOverrideExpiryDays);
            db.Audit.Note(p, action: "HealthOverrideSet", reason: note);
            await db.SaveChangesAsync();
            return Results.Ok(new { p.Id, p.RowVersion, p.HealthOverrideExpiresAt });
        });

        api.MapDelete("/projects/{id:guid}/health-override", async (Guid id, HttpContext http, Access access, HubDb db) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.HealthOverride(access.Actor, ctx));
            await Http.CheckVersion(db, http, p, null);
            p.HealthOverride = null;
            p.HealthOverrideNote = null;
            p.HealthOverrideExpiresAt = null;
            db.Audit.Note(p, action: "HealthOverrideCleared");
            await db.SaveChangesAsync();
            return Results.Ok(new { p.Id, p.RowVersion });
        });

        // Derived state for one project: health with reasons, counts, discipline table (§16, §13.1 inputs).
        api.MapGet("/projects/{id:guid}/state", async (Guid id, Access access, HubDb db, EvaluationService eval) =>
        {
            await access.Project(id, track: false);
            await eval.EnsureFresh(id);
            var st = await db.ProjectStates.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id);
            return st is null ? Results.NoContent() : Results.Ok(new
            {
                st.ComputedHealth, HealthReasons = J.El(st.HealthReasons), Inputs = J.El(st.Inputs), Counts = J.El(st.Counts),
                Disciplines = J.El(st.DisciplineStates), st.NextMilestoneId, st.NextSubmissionMilestoneId, st.ProgressPct, st.EvaluatedAt,
            });
        });
    }

    /// Ranked attention rows with names and age; snoozed items are hidden unless asked for (ATT-03, ATT-04).
    public static async Task<object> Items(HubDb db, IQueryable<AttentionItem> q, DateTimeOffset now, bool includeSnoozed, Guid? disciplineId = null, string? severity = null, int? limit = null)
    {
        if (disciplineId is { } d) q = q.Where(a => a.ProjectDisciplineId == d);
        var sev = Http.List(severity);
        if (sev.Length > 0) q = q.Where(a => sev.Contains(a.Severity));
        var rows = await q.ToListAsync();
        var snoozes = await db.Snoozes.AsNoTracking().Where(z => z.EndedAt == null && z.SnoozedUntil > now && rows.Select(r => r.ItemId).Contains(z.ItemId)).ToListAsync();
        var ownerIds = rows.Select(r => r.OwnerUserId).OfType<Guid>().Distinct().ToList();
        var names = await db.Users.Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.IsActive ? u.DisplayName : u.DisplayName + " (Inactive)");
        var projects = await db.Projects.Where(p => rows.Select(r => r.ProjectId).Distinct().Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProjectNumber);
        var list = rows.Select(a =>
        {
            var z = snoozes.FirstOrDefault(x => x.RuleId == a.RuleId && x.ItemType == a.ItemType && x.ItemId == a.ItemId && Severity.Rank(a.Severity) >= Severity.Rank(x.SeverityAtSnooze));
            return new
            {
                a.Id, a.ProjectId, ProjectNumber = projects.GetValueOrDefault(a.ProjectId), a.RuleId, RuleName = Evaluator.RuleNames[a.RuleId], a.ItemType, a.ItemId,
                a.ItemKey, a.ItemName, a.Severity, a.Message, Why = J.El(a.Why), a.RouteToUserIds, a.OwnerUserId,
                OwnerName = a.OwnerUserId is { } o ? names.GetValueOrDefault(o) : null, a.ProjectDisciplineId, a.DaysOverdueOrBlocked, a.Priority, a.DueDate,
                a.FirstDetectedAt, AgeDays = (int)(now - a.FirstDetectedAt).TotalDays,
                Snoozed = z is not null, SnoozedUntil = z?.SnoozedUntil, SnoozeNote = z?.Note,
            };
        }).ToList();
        var ranked = list.OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).ThenBy(a => Priority.Rank(a.Priority))
            .ThenBy(a => a.DueDate ?? DateOnly.MaxValue).ThenBy(a => a.RuleId == "A-03" ? 0 : 1).ThenBy(a => a.ItemKey).ToList();
        var shown = ranked.Where(a => includeSnoozed || !a.Snoozed).ToList();
        // A cross-project list shows the most urgent first and says how many more there are (packet 011 scale run).
        return new { items = shown.Take(limit ?? int.MaxValue), snoozed = ranked.Count(a => a.Snoozed), total = ranked.Count(a => !a.Snoozed), truncated = shown.Count > (limit ?? int.MaxValue) };
    }
}
