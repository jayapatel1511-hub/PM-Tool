using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DecisionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Decision), "Decision", ["Subject", "Description", "OwnerUserId", "OwnerExternalPartyId", "RequiredByDate", "ImpactLevel", "ImpactDescription", "Status", "DecisionText", "DecisionDate", "DecidedById", "DeferralReason", "CancelledReason", "DeletedAt"], true, null)
    {
        NameOrder = 6,
        Names = async (db, list) => (await db.Decisions.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Subject }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Subject}")),
        ExportNameOrder = 7,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Decisions.IgnoreQueryFilters().Where(d => list.Contains(d.Id) && visible.Contains(d.ProjectId)).Select(d => new { d.Id, d.Key, d.Subject }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Subject)); },
        Project = async (db, id) => await db.Decisions.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] DecisionExtrasClientCols = [
        new("key", "key", "key"), new("subject", "subject"), new("description", "toDecide"), new("requiredBy", "requiredBy", "date"),
        new("timing", "timing"), new("impact", "impact"), new("status", "status"), new("owner", "owner"),
    ];
    public static readonly Hub.Api.Features.Col[] ListExportsDecisionCols = [
        new("key", "key", "key"), new("subject", "subject"), new("ownerName", "owner"), new("requestedByName", "requestedBy"), new("dateRequested", "requested", "date"),
        new("requiredByDate", "requiredBy", "date"), new("days", "days", "number"), new("impactLevel", "impact"), new("status", "status"), new("blocking", "blockingCount", "number"),
        new("decisionText", "decision"), new("decisionDate", "decisionDate", "date"),
    ];
}