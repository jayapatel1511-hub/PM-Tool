using System.Text.Json.Nodes;
using Hub.Domain;

namespace Hub.Api.Features;

/// Interactive HTML references from declared report projections only. Never resolves a label or arbitrary GUID.
internal static class ReportPeople
{
    static JsonNode? At(JsonNode? value, string path)
    {
        foreach (var part in path.Split('.')) value = value is JsonObject obj ? obj[part] : null;
        return value;
    }
    static string? Text(JsonNode? value) => value is JsonValue node && node.TryGetValue<string>(out var text) ? text : null;
    static Guid? Id(JsonNode? value) => Guid.TryParse(value?.ToString(), out var id) && id != Guid.Empty ? id : null;

    public static JsonArray Add(string code, string? defaultItemType, JsonArray rows, bool enabled)
    {
        if (!enabled) return rows;
        foreach (var row in rows.OfType<JsonObject>())
        {
            var references = new JsonObject();
            var type = Text(row["itemType"]) ?? defaultItemType;
            var sourceId = Id(row["entryId"]) ?? Id(row["memberId"]) ?? Id(row["id"]);
            var sourceKey = sourceId is { } key ? $"{code}:{type ?? "row"}:{key:D}" : null;
            void User(string column, string idPath, string? namePath = null, string? activePath = null)
            {
                var id = Id(At(row, idPath));
                var name = Text(At(row, namePath ?? column));
                if (id is null || string.IsNullOrWhiteSpace(name) || sourceKey is null) return;
                var reference = new JsonObject { ["kind"] = "User", ["id"] = id.Value, ["displayName"] = name, ["sourceKey"] = sourceKey };
                if (activePath is not null && At(row, activePath) is JsonValue active && active.TryGetValue<bool>(out var isActive)) reference["isActive"] = isActive;
                references[column] = reference;
            }
            switch (type)
            {
                case ItemType.Task:
                    User("assigneeName", "assigneeId", activePath: "assigneeActive");
                    User("reviewerName", "reviewerId", activePath: "reviewerActive");
                    User("createdByName", "createdBy");
                    break;
                case ItemType.Deliverable:
                    User("ownerName", "ownerId", activePath: "ownerActive");
                    User("reviewerName", "reviewerId");
                    break;
                case ItemType.Decision:
                    if (row["ownerExternalPartyId"] is null) User("ownerName", "ownerUserId");
                    User("requestedByName", "requestedById");
                    break;
                case ItemType.Action:
                    if (Text(row["ownerType"]) == "User") User("ownerName", "ownerUserId");
                    User("leadName", "leadId");
                    break;
                case ItemType.Risk:
                case ItemType.Issue:
                    User("ownerName", "ownerId");
                    User("raisedByName", "raisedById");
                    break;
            }
            switch (code)
            {
                case "review-queue":
                    // This report explicitly projects both kinds into the same assignee/reviewer column names.
                    User("assigneeName", "assigneeId");
                    User("reviewerName", "reviewerId");
                    break;
                case "project-activity-log":
                    if (Text(row["actorType"]) is "User" or "Admin") User("actorName", "actorId");
                    break;
                case "projects-at-risk":
                    User("pm.displayName", "pm.id", activePath: "pm.isActive");
                    break;
                case "staff-assignments":
                    User("person", "personId");
                    User("addedBy", "addedById");
                    break;
                case "task-hours":
                    User("person", "personId");
                    break;
                case "workload-employee":
                    User("person", "personId");
                    User("supervisor", "supervisorId");
                    break;
                case "allocations-by-source":
                    User("personName", "personId");
                    User("ownerName", "ownerId");
                    break;
            }
            if (references.Count > 0) row["userReferences"] = references;
        }
        return rows;
    }
}
