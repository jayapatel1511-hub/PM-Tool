using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record DateReasonChoice(Guid Id, string Name, bool IsActive, int SortOrder);
public sealed record DateReasonCount(Guid CategoryId, string Name, bool IsActive, int Count, string ActivityCategory);
public sealed record DateReasonSummary(Guid ProjectId, int Moves, int NotRecorded, DateReasonCount[] Reasons);
sealed record DateEntry(List<string> Categories, string Changes);

/// FR-SCR-65, AC-SCR-65 (053-D13): a milestone or deliverable date move records an organisation reason category beside the
/// free-text reason. The category rides on the move's own activity entries as a tag, so the entry holds both and its hash covers it.
public static class DateReasons
{
    public const string Flag = "register_screens";
    const string Prefix = "dateReason:";
    public static string Tag(Guid categoryId) => Prefix + categoryId.ToString("D");

    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{id:guid}/milestones/date-reasons", Read).RequireFlag(Flag);

    /// With the flag on, a move needs an active category while the organisation keeps at least one; the save tags its date entries.
    public static async Task Require(HubDb db, SettingsStore store, Guid? categoryId)
    {
        if (!(await store.Get(db)).IsFlagOn(Flag)) return;
        if (categoryId is { } id)
        {
            var active = await db.DateReasonCategories.FromSqlInterpolated($"SELECT * FROM hub.date_reason_category WHERE id = {id} FOR SHARE")
                .AsNoTracking().AnyAsync(category => category.IsActive);
            Check.That(active, "dateReasonCategoryId", "dateReason.activeRequired");
            db.Audit.DateReasonCategoryId = id;
        }
        else Check.That(!await db.DateReasonCategories.AnyAsync(category => category.IsActive), "dateReasonCategoryId", "dateReason.required");
    }

    /// "Why dates moved": the project's own milestone and deliverable moves by category; cascaded shifts follow their source move.
    static async Task<Ok<DateReasonSummary>> Read(Guid id, Access access, HubDb db)
    {
        await access.Project(id, false);
        var entries = await (await ActivityEndpoints.Visible(db, access, id))
            .Where(a => (a.ItemType == ItemType.Milestone || a.ItemType == ItemType.Deliverable) && a.Action != "Cascade" && a.Categories.Contains("date"))
            .Select(a => new DateEntry(a.Categories, a.Changes)).ToListAsync();
        // A move changes a date that was already set; a first date is not a move.
        var moves = entries.Where(a => System.Text.Json.JsonDocument.Parse(a.Changes).RootElement.EnumerateArray().Any(change =>
            change.GetProperty("field").GetString() is "Date" or "DueDate" or "StartDate" && change.GetProperty("old").ValueKind != System.Text.Json.JsonValueKind.Null)).ToList();
        var tagged = moves.Select(a => a.Categories.FirstOrDefault(c => c.StartsWith(Prefix, StringComparison.Ordinal))).ToList();
        var ids = tagged.OfType<string>().Select(tag => Guid.Parse(tag[Prefix.Length..])).Distinct().ToArray();
        var names = await db.DateReasonCategories.AsNoTracking().Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id);
        var counts = tagged.OfType<string>().GroupBy(tag => Guid.Parse(tag[Prefix.Length..]))
            .Select(g => new DateReasonCount(g.Key, names.TryGetValue(g.Key, out var c) ? c.Name : Text.Get("dateReason.unknown"), c?.IsActive ?? false, g.Count(), Tag(g.Key)))
            .OrderBy(r => names.TryGetValue(r.CategoryId, out var c) ? c.SortOrder : int.MaxValue).ThenBy(r => r.Name, StringComparer.Ordinal).ToArray();
        return TypedResults.Ok(new DateReasonSummary(id, moves.Count, tagged.Count(tag => tag is null), counts));
    }
}
