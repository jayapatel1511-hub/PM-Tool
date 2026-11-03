using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.Issue), "Issue", ["Title", "Description", "OwnerId", "Severity", "TargetResolutionDate", "Resolution", "ResolvedDate", "Status", "IssueType", "OriginRiskId", "ProjectDisciplineId", "DeletedAt", "RootCauseCategoryId", "RootCauseLabelSnapshot"], true, null)
    {
        NameOrder = 8,
        Names = async (db, list) => (await db.Issues.IgnoreQueryFilters().Where(x => list.Contains(x.Id)).Select(x => new { x.Id, x.Key, x.Title }).ToListAsync()).Select(x => (x.Id, $"{x.Key} {x.Title}")),
        ExportNameOrder = 9,
        ExportNames = async (db, access, list) => { var visible = access.VisibleProjectIds(); return (await db.Issues.IgnoreQueryFilters().Where(i => list.Contains(i.Id) && visible.Contains(i.ProjectId)).Select(i => new { i.Id, i.Key, i.Title }).ToListAsync())
            .Select(x => (x.Id, x.Key + " " + x.Title)); },
        Project = async (db, id) => await db.Issues.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
    };
    public static readonly Hub.Api.Features.Col[] RegistersIssueCols = [
        new("key", "key", "key"), new("title", "title"), new("issueType", "issueType", Label: "Issue type"), new("status", "status"), new("severity", "severity"),
        new("ownerName", "owner"), new("raisedByName", "raisedBy"), new("disciplineName", "discipline"), new("affectedDisciplineSummary", "affectedDisciplines", Label: "Affected disciplines"), new("dateRaised", "dateRaised", "date"), new("targetResolutionDate", "targetDate", "date"),
        new("daysOverdue", "daysOverdue", "number"), new("resolvedDate", "resolvedDate", "date"), new("resolution", "resolution"), new("originRiskKey", "originRisk"),
        new("locationSummary", "locationSummary", Label: "Location"), new("documentSummary", "documentSummary", Label: "References"),
        new("verificationStatus", "verificationStatus", Label: "Verification"),
    ];
}