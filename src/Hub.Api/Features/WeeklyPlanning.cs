using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class WeeklyPlanningEndpoints
{
    public sealed record Selection(Guid Id, int RowVersion);
    public sealed record BulkBody(string Operation, DateOnly Week, string Snapshot, Selection[] Entries);
    public sealed record Candidate(Guid Id, int RowVersion, Guid PersonId, string PersonName, string Label, decimal HoursPerWeek, string Confidence, string Visibility, string[] Warnings);
    sealed record PreviewData(string Operation, DateOnly Week, string Snapshot, Candidate[] Entries);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/planning/week-preview", Preview);
        api.MapPost("/planning/week-apply", Apply);
    }

    static void Validate(string operation, DateOnly week)
    {
        Check.OneOf(operation, ["Copy", "Publish"], "operation");
        Check.That(week != default && week.DayOfWeek == DayOfWeek.Monday, "week", "planning.week");
    }

    static async Task<PreviewData> Data(string operation, DateOnly week, Access access, HubDb db, SettingsStore store)
    {
        Validate(operation, week);
        var source = operation == "Copy" ? week.AddDays(-7) : week;
        var entries = await PlanningEndpoints.VisibleEntries(db, access).Where(e => e.CreatedBy == access.Actor.Id && e.PersonId != access.Actor.Id
            && e.StartWeek <= source && e.EndWeek >= source && (operation != "Publish" || e.Visibility == PlanningVisibility.Draft)).OrderBy(e => e.Id).ToListAsync();
        var people = await db.Users.AsNoTracking().Where(u => entries.Select(e => e.PersonId).Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        entries = entries.Where(e => people[e.PersonId].IsActive && Permissions.CreatePlanningEntry(access.Actor, e.PersonId, people[e.PersonId].SupervisorId).Ok).ToList();
        var ids = entries.Select(e => e.PersonId).Distinct().ToArray();
        var target = await PlanningEndpoints.VisibleEntries(db, access).Where(e => ids.Contains(e.PersonId) && e.StartWeek <= week && e.EndWeek >= week).OrderBy(e => e.Id).ToListAsync();
        var capacity = await PersonCapacity.Load(db, ids, week, week.AddDays(6), await store.Get(db));
        var projects = await db.Projects.AsNoTracking().Where(p => entries.Select(e => e.ProjectId).Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var candidates = entries.Select(e => {
            var warnings = new List<string>();
            if (operation == "Copy" && e.EndWeek >= week) warnings.Add("ContinuingEntry");
            if (operation == "Copy" && target.Any(t => t.CreatedBy == access.Actor.Id && t.PersonId == e.PersonId && t.Label == e.Label && t.ProjectId == e.ProjectId && t.SourceCategory == e.SourceCategory)) warnings.Add("DuplicateEntry");
            if (e.ProjectId is { } pid && (!projects.TryGetValue(pid, out var p) || !WorkloadEndpoints.LiveProjects.Contains(p.Status))) warnings.Add("ProjectNotActive");
            var days = capacity.Where(d => d.Key.Item1 == e.PersonId).Select(d => d.Value).ToArray();
            if (e.HoursPerWeek + target.Where(t => t.PersonId == e.PersonId && t.Id != e.Id).Sum(t => t.HoursPerWeek) > days.Sum(d => d.Available)) warnings.Add("AboveCapacity");
            if (days.Any(d => d.Provisional)) warnings.Add("ProvisionalCapacity");
            return new Candidate(e.Id, e.RowVersion, e.PersonId, people[e.PersonId].DisplayName, e.Label, e.HoursPerWeek, e.Confidence, e.Visibility, warnings.ToArray());
        }).ToArray();
        var snapshot = Hash(JsonSerializer.Serialize(new { operation, week,
            entries = entries.Select(e => new { e.Id, e.RowVersion }), target = target.Select(e => new { e.Id, e.RowVersion }),
            people = people.Values.OrderBy(u => u.Id).Select(u => new { u.Id, u.RowVersion }),
            capacity = capacity.OrderBy(x => x.Key.Item1).ThenBy(x => x.Key.Item2).Select(x => new { Person = x.Key.Item1, Day = x.Key.Item2, x.Value }) }, JsonOpts.Web));
        return new(operation, week, snapshot, candidates);
    }

    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    static async Task<object> Preview(string operation, DateOnly week, Access access, HubDb db, SettingsStore store) => await Data(operation, week, access, db, store);

    static async Task<IResult> Apply(BulkBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify)
    {
        Validate(body.Operation, body.Week);
        Check.That(body.Entries is { Length: > 0 and <= 500 } && body.Entries.Select(e => e.Id).Distinct().Count() == body.Entries.Length, "entries", "error.invalid");
        return await Tx.Run(db, async () => {
            var selectedIds = body.Entries.Select(e => e.Id).ToArray();
            var subjects = await db.PlanningEntries.AsNoTracking().Where(e => selectedIds.Contains(e.Id)).Select(e => e.PersonId).ToArrayAsync();
            await PersonCapacity.LockPeople(db, subjects);
            var current = await Data(body.Operation, body.Week, access, db, store);
            if (current.Snapshot != body.Snapshot) throw ApiException.Conflict("concurrency_conflict", "coord.stale");
            foreach (var selection in body.Entries) {
                var candidate = current.Entries.SingleOrDefault(e => e.Id == selection.Id) ?? throw ApiException.NotFound();
                AvailabilityEndpoints.Version(candidate.RowVersion, selection.RowVersion);
                Check.That(!candidate.Warnings.Contains("ProjectNotActive"), "projectId", "planning.project_inactive");
            }
            var entries = await db.PlanningEntries.Where(e => selectedIds.Contains(e.Id)).ToListAsync(); var changed = new List<PlanningEntry>();
            foreach (var entry in entries) {
                if (body.Operation == "Copy") {
                    var copy = new PlanningEntry { PersonId = entry.PersonId, HoursPerWeek = entry.HoursPerWeek, StartWeek = body.Week, EndWeek = body.Week,
                        Label = entry.Label, SourceCategory = entry.SourceCategory, ProjectId = entry.ProjectId, ProjectDisciplineId = entry.ProjectDisciplineId,
                        Confidence = entry.Confidence, Visibility = PlanningVisibility.Draft, Notes = entry.Notes, LastValidatedAt = clock.GetUtcNow() };
                    db.PlanningEntries.Add(copy); db.Audit.Note(copy, action: "Copied", reason: "Copied from previous week"); changed.Add(copy);
                } else {
                    entry.Visibility = PlanningVisibility.Published; entry.LastValidatedAt = clock.GetUtcNow(); db.Audit.Note(entry, action: "Published"); changed.Add(entry);
                }
            }
            await db.SaveChangesAsync();
            if (body.Operation == "Publish")
                foreach (var group in changed.GroupBy(e => e.PersonId)) {
                    var first = group.First();
                    await notify.Send(NotificationEvents.PlanningEntryChanged, [group.Key], new NotifyItem(null, "PlanningEntry", first.Id, null, $"/my-work?myWeek={body.Week:yyyy-MM-dd}", null), Text.Get("planning.week_published", body.Week.ToString("yyyy-MM-dd"), group.Count()));
                }
            await db.SaveChangesAsync();
            return Results.Ok(new { Count = changed.Count, Items = changed.Select(e => new { e.Id, e.PersonId, e.RowVersion }), body.Week });
        });
    }
}
