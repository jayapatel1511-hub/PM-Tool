using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class WorkSeenReads
{
    public static async Task<IReadOnlyDictionary<WorkSeenKey, WorkSeenDecoration>> DecorateAsync(
        HubDb db, Access access, Hub.Domain.OrgSettings settings, WorkSeenContext context,
        IReadOnlyCollection<WorkSeenReadBound> items, CancellationToken ct = default)
    {
        var result = new Dictionary<WorkSeenKey, WorkSeenDecoration>();
        if (!WorkSeenEligibility.Own(access, settings, context)) return result;
        var current = await WorkSeenEligibility.ReadCurrentAsync(db, access, settings, context, items, ct);
        var enrolled = await db.UserSettings.AsNoTracking().Where(row => row.UserId == access.Actor.Id)
            .Select(row => row.MyWorkFirstVisitAt != null).FirstOrDefaultAsync(ct);
        var tokens = current.Values.SelectMany(value => value).Select(value => value.Token).Distinct().ToArray();
        var acknowledged = (await db.WorkSeenAcknowledgements.AsNoTracking().Where(row => row.RecipientId == access.Actor.Id && tokens.Contains(row.Token))
            .Select(row => row.Token).ToListAsync(ct)).ToHashSet();
        foreach (var (key, observed) in current)
            result[key] = new(observed.Count == 0 ? "Unavailable" : !enrolled ? "Unenrolled"
                : observed.Any(token => !acknowledged.Contains(token.Token)) ? "New" : "Seen", observed);
        return result;
    }
}
