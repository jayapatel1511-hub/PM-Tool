using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class SourceRevisionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.SourceRevision), "SourceRevision", ["DeliverableId", "SourceRowVersion", "SourceKey", "Title", "Revision", "Url", "SourceIdentity", "SourceSystem", "ExternalIdentifier", "Issuer", "Scope", "SourceCheckedAt", "SupersedesId", "AuthorIds"], false, "no item-history surface today; child and organisation history retain existing queries")
    {
        NameOrder = 12,
        Names = async (db, list) => (await db.SourceRevisions.Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.SourceKey, x.Title, x.Revision }).ToListAsync()).Select(x => (x.Id, $"{x.SourceKey} {x.Title} · {x.Revision}"))
    };
}