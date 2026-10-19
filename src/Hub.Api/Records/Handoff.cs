using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class HandoffRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Handoff), "Handoff", ["Title", "SourceDeliverableId", "SourceRowVersion", "DeclaredRevision", "SourceUrl", "SendingDisciplineId", "ReceivingDisciplineId", "SendingOwnerId", "ReceivingOwnerId", "TargetTaskId", "TargetDeliverableId", "IntendedUse", "AcceptanceCriteria", "NeededBy", "PromisedBy", "Status", "CurrentRevisionId", "IncorporatedRevisionId", "DeletedAt"], true, null)
    {
        Project = async (db, id) => await db.Handoffs.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] HandoffsColumns = [new("key", "key"), new("title", "name"), new("senderName", "handoffSender"),
        new("receiverName", "handoffReceiver"), new("status", "status"), new("declaredRevision", "revision"),
        new("neededBy", "handoffNeeded", "date"), new("promisedBy", "handoffPromised", "date"), new("intendedUse", "handoffPurpose")];
}