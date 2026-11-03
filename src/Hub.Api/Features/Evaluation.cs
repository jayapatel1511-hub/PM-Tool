using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Attention items, snoozes and the health override (§12.12, §16.4, FR-ATT-01..03, FR-HLT-01..03).
public static partial class EvaluationEndpoints
{
    public sealed record SnoozeBody(int Days, string Note);
    public sealed record OverrideBody(string Health, string Note, int? RowVersion);
    /// One ranked attention item with its rule, owner and age; `Snoozed*` stay null on an item that is not snoozed (§12.12).
    public sealed partial record AttentionRow(Guid Id, Guid ProjectId, string? ProjectNumber, string RuleId, string RuleName, string ItemType, Guid ItemId,
        string? ItemKey, string? ItemName, string Severity, string Message, JsonElement? Why, List<Guid> RouteToUserIds, Guid? OwnerUserId,
        string? OwnerName, Guid? ProjectDisciplineId, int DaysOverdueOrBlocked, string? Priority, DateOnly? DueDate,
        DateTimeOffset FirstDetectedAt, int AgeDays, bool Snoozed, DateTimeOffset? SnoozedUntil, string? SnoozeNote,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextView? Context = null);
    /// `Items` hides snoozed rows unless asked; `Snoozed` and `Total` count the two kinds; `Truncated` says a limit cut the list (ATT-03, ATT-04).
    public sealed record AttentionList(IReadOnlyList<AttentionRow> Items, int Snoozed, int Total, bool Truncated);
    public sealed record AttentionSnoozed(Guid Id, DateTimeOffset SnoozedUntil);
    public sealed record HealthOverrideApplied(Guid Id, int RowVersion, DateTimeOffset HealthOverrideExpiresAt);
    public sealed record HealthOverrideRemoved(Guid Id, int RowVersion);
    /// The stored derived state of one project (§16, §13.1 inputs); the JSON parts are the evaluator's own documents.
    public sealed record ProjectEvaluation(string ComputedHealth, JsonElement? HealthReasons, JsonElement? Inputs, JsonElement? Counts, JsonElement? Disciplines,
        Guid? NextMilestoneId, Guid? NextSubmissionMilestoneId, int? ProgressPct, DateTimeOffset EvaluatedAt);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{id:guid}/attention", async Task<Ok<AttentionList>> (Guid id, bool? includeSnoozed, Guid? disciplineId, string? severity, Access access, HubDb db, EvaluationService eval, TimeProvider clock, SettingsStore store) =>
        {
            await access.Project(id, track: false);
            await eval.EnsureFresh(id);
            var settings = await store.Get(db);
            await using var snapshot = settings.IsFlagOn("screen_patterns")
                ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
            var project = snapshot is not null ? (await access.Project(id, track: false)).Project : null;
            var result = await Items(db, db.Attention.AsNoTracking().Where(a => a.ProjectId == id), clock.GetUtcNow(), includeSnoozed == true, disciplineId, severity);
            if (project is null) return TypedResults.Ok(result);
            var reader = new RecordContextReader(db, project, settings);
            var contexts = new Dictionary<(string, Guid), RecordContextView>();
            var rows = new List<AttentionRow>();
            foreach (var row in result.Items)
            {
                if (!contexts.TryGetValue((row.ItemType, row.ItemId), out var context))
                    contexts[(row.ItemType, row.ItemId)] = context = await reader.ReadIfAvailable(row.ItemType, row.ItemId) is { } current
                        ? current with { Available = true } : RecordContextReader.Unavailable();
                rows.Add(row with { Context = context });
            }
            return TypedResults.Ok(result with { Items = rows });
        });

        api.MapPost("/attention/{id:guid}/snooze", async Task<Ok<AttentionSnoozed>> (Guid id, SnoozeBody body, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
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
            return TypedResults.Ok(new AttentionSnoozed(z.Id, z.SnoozedUntil));
        });

        api.MapPost("/projects/{id:guid}/health-override", async Task<Ok<HealthOverrideApplied>> (Guid id, OverrideBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me) =>
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
            var expires = clock.GetUtcNow().AddDays(s.HealthOverrideExpiryDays);
            p.HealthOverrideExpiresAt = expires;
            db.Audit.Note(p, action: "HealthOverrideSet", reason: note);
            await db.SaveChangesAsync();
            return TypedResults.Ok(new HealthOverrideApplied(p.Id, p.RowVersion, expires));
        });

        api.MapDelete("/projects/{id:guid}/health-override", async Task<Ok<HealthOverrideRemoved>> (Guid id, HttpContext http, Access access, HubDb db) =>
        {
            var (p, ctx) = await access.Project(id);
            Access.Demand(Permissions.HealthOverride(access.Actor, ctx));
            await Http.CheckVersion(db, http, p, null);
            p.HealthOverride = null;
            p.HealthOverrideNote = null;
            p.HealthOverrideExpiresAt = null;
            db.Audit.Note(p, action: "HealthOverrideCleared");
            await db.SaveChangesAsync();
            return TypedResults.Ok(new HealthOverrideRemoved(p.Id, p.RowVersion));
        });

        // Derived state for one project: health with reasons, counts, discipline table (§16, §13.1 inputs).
        api.MapGet("/projects/{id:guid}/state", async Task<Results<Ok<ProjectEvaluation>, NoContent>> (Guid id, Access access, HubDb db, EvaluationService eval) =>
        {
            await access.Project(id, track: false);
            await eval.EnsureFresh(id);
            var st = await db.ProjectStates.AsNoTracking().FirstOrDefaultAsync(x => x.ProjectId == id);
            if (st is null) return TypedResults.NoContent();
            return TypedResults.Ok(new ProjectEvaluation(st.ComputedHealth, J.El(st.HealthReasons), J.El(st.Inputs), J.El(st.Counts), J.El(st.DisciplineStates),
                st.NextMilestoneId, st.NextSubmissionMilestoneId, st.ProgressPct, st.EvaluatedAt));
        });
    }

    /// Ranked attention rows with names and age; snoozed items are hidden unless asked for (ATT-03, ATT-04).
    public static async Task<AttentionList> Items(HubDb db, IQueryable<AttentionItem> q, DateTimeOffset now, bool includeSnoozed, Guid? disciplineId = null, string? severity = null, int? limit = null)
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
            return new AttentionRow(a.Id, a.ProjectId, projects.GetValueOrDefault(a.ProjectId), a.RuleId, Evaluator.RuleNames[a.RuleId], a.ItemType, a.ItemId,
                a.ItemKey, a.ItemName, a.Severity, a.Message, J.El(a.Why), a.RouteToUserIds, a.OwnerUserId,
                a.OwnerUserId is { } o ? names.GetValueOrDefault(o) : null, a.ProjectDisciplineId, a.DaysOverdueOrBlocked, a.Priority, a.DueDate,
                a.FirstDetectedAt, (int)(now - a.FirstDetectedAt).TotalDays,
                z is not null, z?.SnoozedUntil, z?.Note);
        }).ToList();
        var ranked = list.OrderBy(a => Severity.Rank(a.Severity)).ThenByDescending(a => a.DaysOverdueOrBlocked).ThenBy(a => Priority.Rank(a.Priority))
            .ThenBy(a => a.DueDate ?? DateOnly.MaxValue).ThenBy(a => a.RuleId == "A-03" ? 0 : 1).ThenBy(a => a.ItemKey).ToList();
        var shown = ranked.Where(a => includeSnoozed || !a.Snoozed).ToList();
        // A cross-project list shows the most urgent first and says how many more there are (packet 011 scale run).
        return new AttentionList([.. shown.Take(limit ?? int.MaxValue)], ranked.Count(a => a.Snoozed), ranked.Count(a => !a.Snoozed), shown.Count > (limit ?? int.MaxValue));
    }
}
