using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Read-side visibility only. Stored audit payloads and their hashes are never changed.
public sealed class ActivityScreenFields(OrgSettings settings, HubDb? context = null)
{
    static readonly (string Flag, string Type, string[] Fields)[] FieldRules =
    [
        ("register_screens", "Decision", ["CategoryId", "OptionsConsidered", "Rationale"]),
        ("register_screens", "Issue", ["RootCauseCategoryId", "RootCauseLabelSnapshot"]),
        ("register_screens", "Risk", ["TemplateRiskId"]),
        ("people_screens", "Template", ["CopiedFromTemplateId"]),
        ("people_screens", "DocumentLink", ["SourceSystem", "StableSourceId", "DeclaredRevision", "ReferenceRegisteredBy", "ReferenceRegisteredAt", "ReferenceDeliverableId"]),
        ("capacity_screens", "User", ["PersonType", "PersonStartDate", "PersonEndDate"]),
        ("register_screens", "Deliverable", ["ClientDrawingNumber"]),
        ("register_screens", "Project", ["ClientDrawingNumberPattern"]),
        ("visual_system", "Project", ["IdentityColour"]),
        ("people_screens", "Member", ["ResponsibilityNote"]),
        ("register_screens", "ReviewFinding", ["ResponseOutcome"]),
        ("register_screens", "ReviewFinding", ["DeliverableIssueId", "ExternalReviewId"]),
        ("register_screens", "ReviewManifestItem", ["DeliverableIssueId"]),
        ("register_screens", "FindingEvent", ["ResponseOutcome"]),
        ("register_screens", "ReferenceData", ["RequiredReviewDisciplineId"]),
    ];
    static readonly (string Flag, string Type, string? Key)[] RowRules =
    [
        ("register_screens", "ReferenceData", nameof(DecisionCategory)),
        ("register_screens", "ReferenceData", nameof(IssueRootCauseCategory)),
        ("register_screens", "ReferenceData", nameof(DateReasonCategory)),
        ("register_screens", "ReferenceData", nameof(Hub.Api.Data.IssuePurpose)),
        ("register_screens", "ReferenceData", nameof(Hub.Api.Data.IssueReturnCode)),
        ("register_screens", "ExternalReview", null),
        ("capacity_screens", "ReferenceData", nameof(CertificationTag)),
        ("capacity_screens", "UserCertification", null),
        ("work_screens", "TaskContribution", null),
    ];

    readonly Dictionary<string, HashSet<string>> hiddenFields = FieldRules.Where(rule => !settings.IsFlagOn(rule.Flag))
        .GroupBy(rule => rule.Type).ToDictionary(group => group.Key,
            group => group.SelectMany(rule => rule.Fields).ToHashSet(StringComparer.OrdinalIgnoreCase));
    readonly string[] hiddenTypes = RowRules.Where(rule => !settings.IsFlagOn(rule.Flag) && rule.Key is null)
        .Select(rule => rule.Type).Distinct().ToArray();
    readonly string[] hiddenReferenceKeys = RowRules.Where(rule => !settings.IsFlagOn(rule.Flag) && rule.Type == "ReferenceData" && rule.Key is not null)
        .Select(rule => rule.Key!).Distinct().ToArray();

    public static async Task<ActivityScreenFields> Read(HubDb db)
    {
        var rows = await db.Settings.AsNoTracking().Where(row => row.Key.StartsWith("flag.")).ToListAsync();
        return new(OrgSettings.From(rows.ToDictionary(row => row.Key,
            row => JsonDocument.Parse(row.Value).RootElement.Clone())), db);
    }

    /// Apply row visibility inside SQL before counts, actor choices, digest groups or paging.
    public static IQueryable<ActivityLog> VisibleStored(HubDb db, IQueryable<ActivityLog> rows, bool? issuedReviewsEnabled = null)
    {
        foreach (var rule in RowRules)
        {
            var flag = "flag." + rule.Flag;
            var type = rule.Type;
            // Setting.Value is jsonb: this compares the JSON boolean true, including
            // its normalized whitespace representation; missing and JSON strings are off.
            if (rule.Key is { } key)
                rows = rows.Where(row => row.ItemType != type || row.ItemKey != key
                    || db.Settings.Any(setting => setting.Key == flag && setting.Value == "true"));
            else
                rows = rows.Where(row => row.ItemType != type
                    || db.Settings.Any(setting => setting.Key == flag && setting.Value == "true"));
        }
        var packages = db.ReviewPackages.Where(package => db.ReviewRounds.Any(round => round.PackageId == package.Id
            && db.ReviewManifestItems.Any(item => item.RoundId == round.Id && item.DeliverableIssueId != null))).Select(package => (Guid?)package.Id);
        var rounds = db.ReviewRounds.Where(round => packages.Contains(round.PackageId)).Select(round => (Guid?)round.Id);
        return rows.Where(row => issuedReviewsEnabled == true || issuedReviewsEnabled == null
                && db.Settings.Any(setting => setting.Key == "flag.register_screens" && setting.Value == "true")
            || !(row.ItemType == "ReviewPackage" && packages.Contains(row.ItemId)
                || row.ItemType == "ReviewRound" && rounds.Contains(row.ItemId)
                || row.ItemType == "ReviewManifestItem" && db.ReviewManifestItems.Any(item => item.Id == row.ItemId && rounds.Contains(item.RoundId))
                || row.ItemType == "DisciplineReview" && db.DisciplineReviews.Any(item => item.Id == row.ItemId && rounds.Contains(item.RoundId))
                || row.ItemType == "ReviewFinding" && db.ReviewFindings.Any(item => item.Id == row.ItemId && (item.ExternalReviewId != null || item.DeliverableIssueId != null))
                || row.ItemType == "FindingEvent" && db.FindingEvents.Any(item => item.Id == row.ItemId
                    && db.ReviewFindings.Any(finding => finding.Id == item.FindingId && (finding.ExternalReviewId != null || finding.DeliverableIssueId != null)))));
    }

    // Use before count/order/paging for organisation history. AuditKey is the stored CLR
    // reference kind, never its editable display name; old and unrelated references stay visible.
    public IQueryable<ActivityLog> Visible(IQueryable<ActivityLog> rows)
    {
        if (context is not null) rows = VisibleStored(context, rows, settings.IsFlagOn("register_screens"));
        return hiddenTypes.Length == 0 && hiddenReferenceKeys.Length == 0 ? rows
            : rows.Where(row => !hiddenTypes.Contains(row.ItemType)
                && (row.ItemType != "ReferenceData" || !hiddenReferenceKeys.Contains(row.ItemKey ?? "")));
    }

    public bool Visible(ActivityLog row) => !hiddenTypes.Contains(row.ItemType)
        && (row.ItemType != "ReferenceData" || !hiddenReferenceKeys.Contains(row.ItemKey ?? ""));

    public bool Hidden(string itemType, string field) => hiddenFields.TryGetValue(itemType, out var fields) && fields.Contains(field);

    public JsonElement MaskSnapshot(string itemType, JsonElement snapshot)
    {
        if (snapshot.ValueKind != JsonValueKind.Object || !snapshot.EnumerateObject().Any(field => Hidden(itemType, field.Name))) return snapshot;
        var node = JsonNode.Parse(snapshot.GetRawText())!.AsObject();
        foreach (var field in snapshot.EnumerateObject().Where(field => Hidden(itemType, field.Name))) node.Remove(field.Name);
        return JsonSerializer.Deserialize<JsonElement>(node.ToJsonString());
    }
}
