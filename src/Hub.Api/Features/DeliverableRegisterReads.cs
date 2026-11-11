using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

public static partial class DeliverableEndpoints
{
    public const string RegisterFlag = "register_screens";
    public sealed record DeliverableRegisterInfo(string? ClientDrawingNumber, string? NumberPattern, bool? NumberMatchesPattern, DeliverableCurrentIssue? CurrentIssue);
    public sealed record DeliverableCurrentIssue(Guid Id, string? Revision, DateOnly IssuedDate, Guid? PurposeOfIssueId, string? Purpose,
        Guid? ReturnCodeId, string? ReturnCode, string? ReturnLabel, DateOnly? ResponseDate);
    public sealed record DeliverableIssueRegisterInfo(int RowVersion, Guid? PurposeOfIssueId, string? Purpose,
        Guid? ReturnCodeId, string? ReturnCode, string? ReturnLabel, DateOnly? ResponseDate);
    public sealed record RegisterIssueIdentity(Guid Id, DateTimeOffset CreatedAt);
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record IssueResponseBody([property: JsonRequired] Guid? ReturnCodeId, [property: JsonRequired] DateOnly? ResponseDate,
        int? RowVersion, int? IssueRowVersion, string? Reason);
    public sealed record IssueResponseSaved(Guid Id, Guid IssueId, int RowVersion, int IssueRowVersion);
    sealed record RegisterSource(Guid Id, Guid ProjectId, string? ClientDrawingNumber, Guid? CurrentIssueId, string? Pattern);

    /// Explicit real issuance identity wins. Legacy timestamp ties remain unknown, never broken by an invented issue order.
    public static Guid? CurrentRegisterIssue(Guid? currentIssueId, IReadOnlyList<RegisterIssueIdentity> issues)
    {
        if (currentIssueId is { } current) return issues.Any(issue => issue.Id == current) ? current : null;
        if (issues.Count == 0) return null;
        var latest = issues.Max(issue => issue.CreatedAt);
        var candidates = issues.Where(issue => issue.CreatedAt == latest).ToArray();
        return candidates.Length == 1 ? candidates[0].Id : null;
    }

    static DeliverableIssue? CurrentIssue(Guid? currentIssueId, IEnumerable<DeliverableIssue> source)
    {
        var issues = source.ToArray();
        var id = CurrentRegisterIssue(currentIssueId, issues.Select(issue => new RegisterIssueIdentity(issue.Id, issue.CreatedAt)).ToArray());
        return id is { } key ? issues.Single(issue => issue.Id == key) : null;
    }

    static async Task<List<DeliverableIssue>> RegisterIssues(HubDb db, Guid[] ids) => ids.Length == 0 ? [] : await db.DeliverableIssues.AsNoTracking()
        .Where(issue => ids.Contains(issue.DeliverableId) && db.Deliverables.Any(deliverable => deliverable.Id == issue.DeliverableId && deliverable.ProjectId == issue.ProjectId))
        .ToListAsync();

    static async Task<List<DeliverableRow>> WithRegisterInfo(HubDb db, List<DeliverableRow> rows)
    {
        if (rows.Count == 0) return rows;
        var ids = rows.Select(row => row.Id).ToArray();
        var sources = await db.Deliverables.AsNoTracking().Where(deliverable => ids.Contains(deliverable.Id))
            .Select(deliverable => new RegisterSource(deliverable.Id, deliverable.ProjectId, deliverable.ClientDrawingNumber, deliverable.CurrentIssueId,
                db.Projects.Where(project => project.Id == deliverable.ProjectId).Select(project => project.ClientDrawingNumberPattern).FirstOrDefault())).ToDictionaryAsync(source => source.Id);
        var issues = (await RegisterIssues(db, ids)).ToLookup(issue => issue.DeliverableId);
        return rows.Select(row => {
            if (!sources.TryGetValue(row.Id, out var source) || source.ProjectId != row.ProjectId) return row with { RegisterInfo = new(null, null, null, null) };
            var issue = CurrentIssue(source.CurrentIssueId, issues[row.Id].Where(issue => issue.ProjectId == row.ProjectId));
            return row with { RegisterInfo = new(source.ClientDrawingNumber, source.Pattern, DeliverableNumberingEndpoints.Matches(source.ClientDrawingNumber, source.Pattern),
                issue is null ? null : new(issue.Id, issue.Revision, issue.IssuedDate, issue.PurposeOfIssueId, issue.PurposeLabelSnapshot,
                    issue.ReturnCodeId, issue.ReturnCodeSnapshot, issue.ReturnLabelSnapshot, issue.ResponseDate)) };
        }).ToList();
    }

    static async Task<IQueryable<Deliverable>> RegisterFilter(HubDb db, IQueryable<Deliverable> query, OrgSettings settings,
        Guid? purposeOfIssueId, Guid? returnCodeId, bool? noDueDate)
    {
        if (!settings.IsFlagOn(RegisterFlag))
        {
            Check.That(purposeOfIssueId is null && returnCodeId is null && noDueDate != true, "registerFilters", "deliverableRegister.disabled");
            return query;
        }
        if (noDueDate == true) query = query.Where(deliverable => deliverable.DueDate == null && deliverable.Status != DeliverableStatus.Issued
            && deliverable.Status != DeliverableStatus.Accepted && deliverable.Status != DeliverableStatus.Cancelled);
        if (purposeOfIssueId is { } purpose) Check.That(await db.Set<IssuePurpose>().AnyAsync(value => value.Id == purpose), "purposeOfIssueId", "error.not_found");
        if (returnCodeId is { } code) Check.That(await db.Set<IssueReturnCode>().AnyAsync(value => value.Id == code), "returnCodeId", "error.not_found");
        if (purposeOfIssueId is null && returnCodeId is null) return query;
        var sources = await query.Select(deliverable => new RegisterIssueSource(deliverable.Id, deliverable.ProjectId, deliverable.CurrentIssueId)).ToArrayAsync();
        var issues = (await RegisterIssues(db, sources.Select(source => source.Id).ToArray())).ToLookup(issue => issue.DeliverableId);
        var ids = sources.Where(source => {
            var current = CurrentIssue(source.CurrentIssueId, issues[source.Id].Where(issue => issue.ProjectId == source.ProjectId));
            return current is not null && (purposeOfIssueId is null || current.PurposeOfIssueId == purposeOfIssueId) && (returnCodeId is null || current.ReturnCodeId == returnCodeId);
        }).Select(source => source.Id).ToArray();
        return query.Where(deliverable => ids.Contains(deliverable.Id));
    }

    static async Task<string[]> NumberWarnings(HubDb db, Deliverable deliverable)
    {
        var pattern = await db.Projects.AsNoTracking().Where(project => project.Id == deliverable.ProjectId).Select(project => project.ClientDrawingNumberPattern).SingleAsync();
        return DeliverableNumberingEndpoints.Matches(deliverable.ClientDrawingNumber, pattern) == false ? [Text.Get("deliverableRegister.numberMismatch", pattern!)] : [];
    }

    static async Task<Ok<IssueResponseSaved>> RecordIssueResponse(Guid id, Guid issueId, IssueResponseBody body, HttpContext http,
        Access access, HubDb db, SettingsStore store, TimeProvider clock, CurrentUser me)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, RegisterFlag);
        var projectId = await db.Deliverables.AsNoTracking().Where(deliverable => deliverable.Id == id).Select(deliverable => (Guid?)deliverable.ProjectId).SingleOrDefaultAsync()
            ?? throw ApiException.NotFound();
        await access.Project(projectId, false);
        return await Tx.Run(db, async () =>
        {
            await Coordination.Lock(db, projectId);
            var (deliverable, project, context) = await Load(db, access, id);
            var issue = await db.DeliverableIssues.SingleOrDefaultAsync(value => value.Id == issueId && value.DeliverableId == id && value.ProjectId == projectId)
                ?? throw ApiException.NotFound();
            Access.Demand(Permissions.DeliverableTransition(access.Actor, context, await Facts(db, deliverable), DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued));
            await Http.CheckVersion(db, http, deliverable, body.RowVersion);
            await Http.CheckBodyVersion(db, issue, body.IssueRowVersion); // a parent If-Match header cannot replace this independent baseline
            Check.That((body.ReturnCodeId is null) == (body.ResponseDate is null), "returnCodeId", "deliverableRegister.responsePair");
            var reason = Check.Optional(body.Reason, "reason", 2000);
            ProjectEndpoints.CorrectionReason(project, reason);
            IssueReturnCode? code = null;
            if (body.ReturnCodeId is { } codeId)
            {
                code = await db.Set<IssueReturnCode>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == codeId && (value.IsActive || value.Id == issue.ReturnCodeId))
                    ?? throw ApiException.Invalid("returnCodeId", "error.not_found");
                Check.That(body.ResponseDate >= issue.IssuedDate && body.ResponseDate <= clock.Today(settings), "responseDate", "deliverableRegister.responseDateRange");
            }
            if (body.ReturnCodeId == issue.ReturnCodeId && body.ResponseDate == issue.ResponseDate)
                return TypedResults.Ok(new IssueResponseSaved(id, issueId, deliverable.RowVersion, issue.RowVersion));
            if (issue.ReturnCodeId is not null || issue.ResponseDate is not null) reason = Check.Reason(reason);
            var previous = IssueResponseState.Of(issue);
            var previousCode = issue.ReturnCodeSnapshot;
            var previousDate = issue.ResponseDate;
            var retainSnapshot = code is not null && code.Id == issue.ReturnCodeId;
            issue.ReturnCodeSnapshot = retainSnapshot ? issue.ReturnCodeSnapshot : code?.Code;
            issue.ReturnLabelSnapshot = retainSnapshot ? issue.ReturnLabelSnapshot : code?.Name;
            issue.ReturnCodeId = code?.Id;
            issue.ResponseDate = body.ResponseDate;
            issue.ResponseRecordedBy = code is null ? null : me.Id;
            issue.ResponseRecordedAt = code is null ? null : clock.GetUtcNow();
            deliverable.LastActivityAt = clock.GetUtcNow();
            db.Entry(deliverable).Property(value => value.LastActivityAt).IsModified = true;
            db.LogEvent(ItemType.Deliverable, id, "IssueResponseRecorded", "deliverable", projectId, deliverable.Key, deliverable.Name,
                new[] { new IssueResponseChange("ReturnCode", previousCode, issue.ReturnCodeSnapshot),
                    new IssueResponseChange("ResponseDate", previousDate, issue.ResponseDate) }, reason, deliverable.ProjectDisciplineId,
                JsonSerializer.Serialize(new IssueResponseSnapshot(issueId, issue.Revision, previous, IssueResponseState.Of(issue)), JsonOpts.Web));
            await db.SaveChangesAsync();
            return TypedResults.Ok(new IssueResponseSaved(id, issueId, deliverable.RowVersion, issue.RowVersion));
        });
    }
}

file sealed record RegisterIssueSource(Guid Id, Guid ProjectId, Guid? CurrentIssueId);
// Activity JSON shapes: property names and order are the stored wire format.
file sealed record IssueResponseChange(string Field, object? Old, object? New);
file sealed record IssueResponseState(Guid? ReturnCodeId, string? ReturnCodeSnapshot, string? ReturnLabelSnapshot,
    DateOnly? ResponseDate, Guid? ResponseRecordedBy, DateTimeOffset? ResponseRecordedAt)
{
    public static IssueResponseState Of(DeliverableIssue issue) => new(issue.ReturnCodeId, issue.ReturnCodeSnapshot, issue.ReturnLabelSnapshot,
        issue.ResponseDate, issue.ResponseRecordedBy, issue.ResponseRecordedAt);
}
file sealed record IssueResponseSnapshot(Guid IssueId, string? Revision, IssueResponseState Previous, IssueResponseState Current);
