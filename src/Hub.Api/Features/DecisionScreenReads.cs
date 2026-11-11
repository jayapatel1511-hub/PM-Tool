using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record DecisionLagGroup(string OwnerKind, string Organisation, int DecidedCount, decimal MedianDays);
public sealed record DecisionLagRead(string Basis, int Total, int Attributed, int InvalidDateCount, int UnattributedCount, IReadOnlyList<DecisionLagGroup> Groups);
public sealed record DecisionScreenRead(DateTimeOffset AsOf, IReadOnlyList<DecisionCategoryChoice> Categories, DecisionLagRead Lag);

public static class DecisionScreenReads
{
    public static async Task<DecisionScreenRead> Read(Guid id, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, DecisionMetadata.Flag);
        await access.Project(id, track: false);
        Check.That(http.Request.Query.Count == 0, "query", "error.invalid");
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var asOf = clock.GetUtcNow(); var today = Clock.LocalDate(asOf, settings);
        var rows = await db.Decisions.AsNoTracking().Where(decision => decision.ProjectId == id && decision.Status == DecisionStatus.Decided)
            .Select(decision => new DecidedRow(decision.DateRequested, decision.DecisionDate, decision.OwnerUserId, decision.OwnerExternalPartyId)).ToListAsync();
        var users = rows.Select(row => row.OwnerUserId).OfType<Guid>().Distinct().ToArray();
        var actualUsers = (await db.Users.AsNoTracking().Where(user => users.Contains(user.Id)).Select(user => user.Id).ToArrayAsync()).ToHashSet();
        var parties = rows.Select(row => row.OwnerExternalPartyId).OfType<Guid>().Distinct().ToArray();
        var organisations = await db.ExternalParties.AsNoTracking().Where(party => party.ProjectId == id && parties.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, party => party.Organisation);
        var invalid = 0; var unattributed = 0;
        var samples = new Dictionary<(string Kind, string Organisation), List<int>>();
        foreach (var row in rows)
        {
            if (row.DecisionDate is not { } date || date > today || date < row.DateRequested) { invalid++; continue; }
            (string Kind, string Organisation)? owner = null;
            if (row.OwnerUserId is { } user && row.OwnerExternalPartyId is null && actualUsers.Contains(user)) owner = ("Internal", "Internal");
            else if (row.OwnerUserId is null && row.OwnerExternalPartyId is { } party && organisations.TryGetValue(party, out var label) && !string.IsNullOrWhiteSpace(label))
                owner = ("External", label.Trim());
            if (owner is null) { unattributed++; continue; }
            if (!samples.TryGetValue(owner.Value, out var values)) samples[owner.Value] = values = [];
            values.Add(date.DayNumber - row.DateRequested.DayNumber);
        }
        var groups = samples.OrderBy(pair => pair.Key.Kind, StringComparer.Ordinal).ThenBy(pair => pair.Key.Organisation, StringComparer.Ordinal).Select(pair =>
        {
            var values = pair.Value.Order().ToArray(); var middle = values.Length / 2;
            var median = values.Length % 2 == 1 ? values[middle] : ((decimal)values[middle - 1] + values[middle]) / 2;
            return new DecisionLagGroup(pair.Key.Kind, pair.Key.Organisation, values.Length, median);
        }).ToArray();
        var categories = await db.DecisionCategories.AsNoTracking().OrderBy(category => category.SortOrder).ThenBy(category => category.Name).ThenBy(category => category.Id)
            .Select(category => new DecisionCategoryChoice(category.Id, category.Name, category.IsActive)).ToListAsync();
        return new(asOf, categories, new("CurrentRegisteredValuesAllDecidedInProject", rows.Count, groups.Sum(group => group.DecidedCount), invalid, unattributed, groups));
    }
}

file sealed record DecidedRow(DateOnly DateRequested, DateOnly? DecisionDate, Guid? OwnerUserId, Guid? OwnerExternalPartyId);
