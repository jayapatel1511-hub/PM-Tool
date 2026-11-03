using System.Globalization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Client copy of the canonical register: an explicit projection, never an internal row with hidden columns.
public static class DeliverableClientExportEndpoints
{
    public sealed record DeliverableClientExportRow(string Number, string? ClientDrawingNumber, string Title, string? Revision, string? PurposeOfIssue,
        DateOnly? IssueDate, string? ReturnCode, string Status);

    public static readonly Col[] Columns = [
        new("number", "clientDeliverableNumber"), new("clientDrawingNumber", "clientDrawingNumber"), new("title", "clientDeliverableTitle"),
        new("revision", "revision"), new("purposeOfIssue", "purposeOfIssue"), new("issueDate", "clientIssueDate", "date"),
        new("returnCode", "returnCode"), new("status", "status"),
    ];

    /// Working revision is meaningful only when there is no actual or legacy evidence of issuance.
    public static DeliverableClientExportRow Project(DeliverableEndpoints.DeliverableRow row, bool hasIssueEvidence)
    {
        var issue = row.RegisterInfo?.CurrentIssue;
        var unissued = !hasIssueEvidence && row.IssuedDate is null && row.IssuedTo is null && row.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted);
        var revision = issue is not null ? issue.Revision : unissued ? row.Revision : null;
        return new(row.Key, row.RegisterInfo?.ClientDrawingNumber, row.Name, revision, issue?.Purpose, issue?.IssuedDate, issue?.ReturnCode, row.Status);
    }

    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{id:guid}/deliverables/client-export", Send).RequireFlag(DeliverableEndpoints.RegisterFlag);

    static async Task<IResult> Send(Guid id, string? format, [AsParameters] DeliverableEndpoints.DeliverableQuery filter,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
        await access.Project(id, track: false);
        Check.OneOf(format ?? "csv", ["csv", "xlsx"], "format");
        // The current issue bundle and the unresolved issuance-evidence check belong to one read snapshot.
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var (project, _) = await access.Project(id, track: false);
        var source = await DeliverableEndpoints.List(id, filter with { IncludeTasks = false }, access, db, store);
        if (source.Count > Export.MaxRows) throw ApiException.Rule("export_too_large", "export.too_large", null, Export.MaxRows);
        var unresolved = source.Where(row => row.RegisterInfo?.CurrentIssue is null).Select(row => row.Id).ToArray();
        Guid[] evidence = unresolved.Length == 0 ? [] : await db.Deliverables.AsNoTracking().Where(deliverable => deliverable.ProjectId == id && unresolved.Contains(deliverable.Id)
            && (deliverable.CurrentIssueId != null || deliverable.TransmittalUrl != null || deliverable.IssueNote != null
                || db.DeliverableIssues.Any(issue => issue.ProjectId == id && issue.DeliverableId == deliverable.Id))).Select(deliverable => deliverable.Id).ToArrayAsync();
        var hasEvidence = evidence.ToHashSet();
        // Only eight allowed fields are serialized. Person fields and raw filters cannot enter the download.
        var rows = JsonSerializer.SerializeToNode(source.Select(row => Project(row, hasEvidence.Contains(row.Id))), JsonOpts.Web)!.AsArray();
        var filtered = new object?[] { filter.DisciplineId, filter.Status, filter.MilestoneId, filter.OwnerId, filter.TypeId, filter.DueFrom, filter.DueTo,
            filter.Indicator, filter.RequiresReview, filter.Q, filter.PurposeOfIssueId, filter.ReturnCodeId }
            .Any(value => value is not null && value is not string { Length: 0 }) || filter.RevisionWarning == true || filter.NoDueDate == true;
        // Generic ListExports.Filters resolves people and raw search text; this download deliberately uses fixed safe metadata.
        List<(string, string)> parameters = [
            (Text.Get("deliverableClient.scope"), Text.Get(filtered ? "deliverableClient.filtered" : "deliverableClient.register")),
            (Text.Get("deliverableClient.rows"), rows.Count.ToString(CultureInfo.InvariantCulture)),
            (Text.Get("deliverableClient.issueBasis"), Text.Get("deliverableClient.issueBasisHint")),
        ];
        var result = await ExportFile.Send(db, store, format, Text.Get("export.deliverableClient", project.ProjectNumber), Columns, rows,
            parameters, id, $"{project.ProjectNumber}-client-deliverables", clock);
        if (snapshot is not null) await snapshot.CommitAsync();
        return result;
    }
}
