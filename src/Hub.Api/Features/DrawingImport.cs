using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrawingImportDefaults(Guid? ProjectDisciplineId = null, Guid? DeliverableTypeId = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrawingImportPreviewBody([property: JsonRequired] string Format, [property: JsonRequired] string ContentBase64,
    string? Worksheet = null, string? SourceReference = null, DrawingImportDefaults? Defaults = null, string? Reason = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DrawingImportCommitBody([property: JsonRequired] string Format, [property: JsonRequired] string ContentBase64,
    string? Worksheet = null, string? SourceReference = null, DrawingImportDefaults? Defaults = null, string? Reason = null,
    string? PreviewFingerprint = null, int? ProjectRowVersion = null);
public sealed record DrawingImportDisciplineOption(Guid Id, string Name, bool CanCreate);
public sealed record DrawingImportTypeOption(Guid Id, string Name);
public sealed record DrawingImportOptions(int ProjectRowVersion, bool CorrectionReasonRequired, bool CanImport,
    DrawingImportDisciplineOption[] Disciplines, DrawingImportTypeOption[] Types);
public sealed record DrawingImportMessage(string Code, string Field, string Message);
public sealed record DrawingImportValues(string Name, Guid ProjectDisciplineId, string ProjectDisciplineName,
    Guid DeliverableTypeId, string DeliverableTypeName, Guid OwnerId, string OwnerName, Guid? ReviewerId, string? ReviewerName,
    Guid? MilestoneId, string? MilestoneKey, DateOnly? StartDate, DateOnly? DueDate, string Priority,
    string? Revision, bool RequiresReview, string? ClientDrawingNumber, string? Description, bool OwnerFromLead, bool DueFromMilestone);
public sealed record DrawingImportPreviewRow(int SourceRow, bool SourceRowInferred, bool Hidden, string Classification,
    string? SourceRowKey, DrawingImportValues? Values, DrawingImportMessage[] Errors, DrawingImportMessage[] Warnings);
public sealed record DrawingImportPreview(string Format, string? SourceReference, string? SelectedSheet, string? ContentSha256,
    string? PreviewFingerprint, int ProjectRowVersion, bool CorrectionReasonRequired, bool Complete,
    DrawingImportSheet[] Sheets, DrawingImportDiagnostic[] Diagnostics, DrawingImportPreviewRow[] Rows,
    int Creates, int Errors, bool CanCommit);
public sealed record DrawingImportCreatedRow(int SourceRow, Guid Id, string Key, int RowVersion, Guid ProjectDisciplineId, string[] Warnings);
public sealed record DrawingImportCreated(DrawingImportCreatedRow[] Rows, int Total);

/// Create-only drawing intake. Preview has no business/receipt writes; all prepared creates use one ambient receipt transaction.
public static class DrawingImportEndpoints
{
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/deliverables/import-options", Options).RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapPost("/projects/{projectId:guid}/deliverables/import-preview", Preview)
            .WithMetadata(new DrawingImportReceipts.AtomicRequest(), new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(3 * 1024 * 1024))
            .RequireFlag(DeliverableEndpoints.RegisterFlag);
        api.MapPost("/projects/{projectId:guid}/deliverables/import", Commit)
            .WithMetadata(new DrawingImportReceipts.AtomicRequest(), new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(3 * 1024 * 1024))
            .RequireFlag(DeliverableEndpoints.RegisterFlag);
    }

    static async Task<Ok<DrawingImportOptions>> Options(Guid projectId, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
        var (project, context) = await access.Project(projectId, false);
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Include(row => row.Discipline)
            .Where(row => row.ProjectId == projectId && row.IsActive).OrderBy(row => row.SortOrder).ThenBy(row => row.Id).ToListAsync();
        var choices = disciplines.Select(row => new DrawingImportDisciplineOption(row.Id, row.Discipline!.Name,
            access.Actor.IsActive && Permissions.CreateDeliverable(access.Actor, context, row.Id).Ok)).ToArray();
        var types = await db.DeliverableTypes.AsNoTracking().Where(row => row.IsActive).OrderBy(row => row.SortOrder).ThenBy(row => row.Id)
            .Select(row => new DrawingImportTypeOption(row.Id, row.Name)).ToArrayAsync();
        return TypedResults.Ok(new DrawingImportOptions(project.RowVersion, ProjectStatus.NeedsCorrectionReason(project.Status), choices.Any(row => row.CanCreate), choices, types));
    }

    static async Task<Ok<DrawingImportPreview>> Preview(Guid projectId, DrawingImportPreviewBody body, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
        // A consistent read snapshot, not an upload/preview reservation or a command receipt.
        await using var snapshot = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        return TypedResults.Ok((await DrawingImportRows.Prepare(projectId, body, access, db, store)).Preview);
    }

    static async Task<Ok<DrawingImportCreated>> Commit(Guid projectId, DrawingImportCommitBody body, HttpContext http,
        Access access, HubDb db, SettingsStore store, TimeProvider clock, TeamService team, Notifier notify)
    {
        Http.RequireFlag(await store.Get(db), DeliverableEndpoints.RegisterFlag);
        var result = await DrawingImportReceipts.Execute<DrawingImportCreated>(projectId, body, http, access, db, store, clock, async (project, context) =>
        {
            // These baseline checks deliberately run only for a new intent, never an already committed exact retry.
            await Http.CheckBodyVersion(db, project, body.ProjectRowVersion);
            if (string.IsNullOrWhiteSpace(body.PreviewFingerprint))
                throw new ApiException(428, "precondition_required", Text.Get("drawingImport.previewRequired"));
            var input = new DrawingImportPreviewBody(body.Format, body.ContentBase64, body.Worksheet, body.SourceReference, body.Defaults, body.Reason);
            var prepared = await DrawingImportRows.Prepare(projectId, input, access, db, store);
            if (!prepared.Preview.CanCommit)
                throw new ApiException(400, "drawing_import_errors", Text.Get("drawingImport.rowErrors"), extra: new PreviewProblem(prepared.Preview));
            if (!string.Equals(body.PreviewFingerprint, prepared.Preview.PreviewFingerprint, StringComparison.Ordinal))
                throw ApiException.Conflict("drawing_import_preview_changed", "drawingImport.previewChanged", new PreviewProblem(prepared.Preview));

            // ALL rows, source references, permissions and restricted memberships were prepared before the first key.
            var batch = Guid.CreateVersion7(); var created = new List<DrawingImportCreatedRow>();
            var oldSource = db.Audit.Source; var oldReason = db.Audit.Reason;
            try
            {
                foreach (var row in prepared.Rows)
                {
                    db.Audit.Source = "Import";
                    db.Audit.Reason = prepared.Reason;
                    var provenance = new Dictionary<string, object?> {
                        ["batchId"] = batch, ["sourceRow"] = row.Preview.SourceRow, ["format"] = prepared.Preview.Format,
                        ["contentHash"] = prepared.Preview.ContentSha256
                    };
                    if (prepared.Preview.SelectedSheet is { } sheet) provenance["sheet"] = sheet;
                    if (prepared.Preview.SourceReference is { } reference) provenance["sourceReference"] = reference;
                    if (row.Preview.SourceRowKey is { } key) provenance["sourceRowKey"] = key;
                    var made = await DeliverableEndpoints.CreatePrepared(row.Plan!, access, db, team, notify, JsonSerializer.Serialize(provenance, JsonOpts.Web));
                    created.Add(new(row.Preview.SourceRow, made.Id, made.Key, made.RowVersion, row.Plan!.Discipline.Id,
                        made.Warnings.Concat(row.Preview.Warnings.Select(warning => warning.Message)).Distinct(StringComparer.Ordinal).ToArray()));
                }
            }
            finally { db.Audit.Source = oldSource; db.Audit.Reason = oldReason; }
            return new DrawingImportCreated(created.ToArray(), created.Count);
        }, async (saved, project, context) =>
        {
            if (!access.Actor.IsActive) throw ApiException.Forbidden("perm.inactive");
            foreach (var disciplineId in saved.Rows.Select(row => row.ProjectDisciplineId).Distinct())
            {
                var discipline = await db.ProjectDisciplines.AsNoTracking().SingleOrDefaultAsync(row => row.Id == disciplineId && row.ProjectId == projectId);
                if (discipline is null || !discipline.IsActive) throw ApiException.Forbidden("perm.dl_own");
                Access.Demand(Permissions.CreateDeliverable(access.Actor, context, disciplineId));
            }
        });
        return TypedResults.Ok(result);
    }
}

// Problem-body extension: serialized with JsonOpts.Web as {"preview": ...}.
file sealed record PreviewProblem(DrawingImportPreview Preview);
