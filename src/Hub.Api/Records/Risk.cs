using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class RiskRecord { public static RegistryEntry Entry => new(typeof(Hub.Api.Data.Risk), "Risk", ["Title", "Description", "OwnerId", "Probability", "Impact", "Mitigation", "TriggerIndicator", "ReviewDate", "Status", "RealisedIssueId", "ProjectDisciplineId", "DeletedAt"], true, null)
    {
        NameOrder = 7,
        Names = async (db, list) => (await db.Risks.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Title }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Title}")),
        ExportNameOrder = 8,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Risks.IgnoreQueryFilters().Where(r => list.Contains(r.Id) && visible.Contains(r.ProjectId)).Select(r => new { r.Id, r.Key, r.Title }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Title)); },
        Project = async (db, id) => await db.Risks.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] RegistersRiskCols = [
        new("key", "key", "key"), new("title", "title"), new("status", "status"), new("band", "severity"), new("score", "score", "number"),
        new("probability", "probability", "number"), new("impact", "impactScore", "number"), new("ownerName", "owner"), new("disciplineName", "discipline"),
        new("reviewDate", "reviewDate", "date"), new("mitigation", "mitigation"), new("triggerIndicator", "trigger"), new("realisedIssueKey", "realisedAs"),
    ];
}
