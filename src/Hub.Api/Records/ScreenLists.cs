using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Records;

public sealed record ScreenList(string Code, string[] Keys)
{
    public IReadOnlyDictionary<string, string> NotKept { get; } = new Dictionary<string, string>
    {
        ["page"] = "paging is transient", ["panel"] = "panel selection is transient", ["draftAccessReason"] = "access reasons must be supplied again",
    };
}

/// Lists spanning entities and the existing saved-view contract. Entity display parts share these declarations.
public static class ScreenLists
{
    public static readonly ScreenList[] All =
    [
        new("tasks", ["q", "disciplineId", "deliverableId", "status", "priority", "mine", "overdue", "blocked", "blocking", "dueThisWeek", "unassigned", "readyForReview", "waiting", "open", "stale", "dueSoon", "noDueDate", "dateInconsistent", "heldPastDue", "dueFrom", "dueTo", "milestoneId", "assigneeId", "ids", "sort", "group", "cols", "onHold"]),
        new("reviews", ["q", "status", "ownerId", "disciplineId", "mine"]),
        new("changes", ["q", "status", "ownerId", "mine"]),
        new("handoffs", ["q", "status", "direction", "disciplineId", "overdue"]),
        new("submissions", ["q", "status", "coordinatorId", "milestoneId", "targetFrom", "targetTo"]),
        new("allocations", ["q", "personId", "purpose", "status", "from", "to", "tab"]),
        new("design-basis", ["kind", "status", "discipline", "scope", "overdue", "affectedWorkId"]),
        new("readiness", ["from", "to"]),
        new("issues", ["q", "status", "severity", "ownerId", "disciplineId", "indicator", "location", "document", "revision", "verification", "alignment", "issueType", "stationFrom", "stationTo", "stationUnits", "group", "sort", "cols"]),
        new("board", ["q", "disciplineId", "deliverableId", "status", "priority", "mine", "overdue", "blocked", "blocking", "dueThisWeek", "unassigned", "readyForReview", "waiting", "open", "stale", "dueSoon", "noDueDate", "dateInconsistent", "heldPastDue", "dueFrom", "dueTo", "milestoneId", "assigneeId", "ids", "sort", "group", "cols", "swim", "side", "onHold"]),
        new("deliverables", ["q", "disciplineId", "status", "milestoneId", "ownerId", "typeId", "indicator", "dueFrom", "dueTo", "requiresReview", "group", "sort", "cols", "revisionWarning"]),
        new("decisions", ["q", "status", "ownerId", "ownerType", "impact", "requiredFrom", "requiredTo", "blocking", "indicator", "sort", "cols", "tab"]),
        new("milestones", ["type", "showCompleted"]),
        new("projects", ["q", "status", "pmId", "clientId", "officeId", "phaseId", "disciplineId", "health", "projectTypeId", "includeArchived", "mine", "starred", "priority", "sort", "submissionWithinDays", "computedHealth", "leadUnitId", "ids"]),
        new("portfolio", ["q", "status", "pmId", "clientId", "officeId", "phaseId", "disciplineId", "health", "projectTypeId", "mine", "submissionWithinDays", "computedHealth", "group", "overdueDecisions", "leadUnitId"]),
        new("staff", ["scope", "inactive", "office", "discipline", "indicator", "unitId"]),
        new("workload", ["supervisorId", "disciplineId", "officeId", "projectId", "indicator", "from", "sort", "groupBy", "unitId"]),
        new("planner", ["view", "from", "weeks", "week", "supervisorId", "disciplineId", "officeId", "personId", "projectId", "source", "confidence", "visibility", "indicator", "q", "sort", "includeMyDrafts", "groupBy", "layout", "stale", "to", "unitId", "withoutProject"]),
        new("workspace-tasks", ["q", "disciplineId", "deliverableId", "status", "priority", "mine", "overdue", "blocked", "blocking", "dueThisWeek", "unassigned", "readyForReview", "waiting", "open", "stale", "dueSoon", "noDueDate", "dateInconsistent", "heldPastDue", "dueFrom", "dueTo", "milestoneId", "assigneeId", "ids", "sort", "group", "cols", "projects", "ws", "projectId", "onHold", "createdBy"]),
        new("workspace-board", ["q", "disciplineId", "deliverableId", "status", "priority", "mine", "overdue", "blocked", "blocking", "dueThisWeek", "unassigned", "readyForReview", "waiting", "open", "stale", "dueSoon", "noDueDate", "dateInconsistent", "heldPastDue", "dueFrom", "dueTo", "milestoneId", "assigneeId", "ids", "sort", "group", "cols", "projects", "ws", "projectId", "swim", "side", "onHold", "createdBy"]),
        new("mywork", ["tab", "who", "projects", "ws", "project", "discipline", "status", "priority", "from", "to", "hideWaiting", "sort"]),
        new("coordination", ["discipline", "owner", "from", "to"]),
        new("workspace-coordination", ["tab", "projects", "ws", "projectId", "disciplineId", "ownerId", "from", "to"]),
    ];
    public static IReadOnlyDictionary<string, string[]> SavedViewKeys { get; } = All.ToDictionary(l => l.Code, l => l.Keys);
    public static readonly string[] SearchGroups = ["projects", "tasks", "deliverables", "milestones", "decisions", "handoffs", "reviews", "changes", "submissions", "allocations", "planning", "design-basis", "constraints", "commitments", "comments", "people"];

    public static async Task<bool> ValidReference(HubDb db, Guid? projectId, string k, string value)
    {
        var ids = Http.Ids(value);
        return k switch
            {
                "disciplineId" when projectId is not null => await Count(db.ProjectDisciplines.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.IsActive).Select(x => x.Id), ids),
                "discipline" when projectId is not null => await Count(db.ProjectDisciplines.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.IsActive).Select(x => x.Id), ids),
                "disciplineId" or "discipline" => await Count(db.Disciplines.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "owner" when projectId is not null => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "deliverableId" => await Count(db.Deliverables.Where(x => ids.Contains(x.Id)).Select(x => x.Id), ids),
                "milestoneId" => await Count(db.Milestones.Where(x => ids.Contains(x.Id) && !x.IsCancelled).Select(x => x.Id), ids),
                "coordinatorId" or "personId" when projectId is not null => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive
                    && db.ProjectMembers.Any(m => m.ProjectId == projectId && m.UserId == x.Id && m.RemovedAt == null)).Select(x => x.Id), ids),
                "affectedWorkId" when projectId is not null => await Count(db.Tasks.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.DeletedAt == null).Select(x => x.Id)
                    .Concat(db.Deliverables.Where(x => ids.Contains(x.Id) && x.ProjectId == projectId && x.DeletedAt == null).Select(x => x.Id)), ids),
                "assigneeId" or "ownerId" or "pmId" or "supervisorId" => await Count(db.Users.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids)
                    || (k == "ownerId" && await Count(db.ExternalParties.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids)),
                "typeId" => await Count(db.DeliverableTypes.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "clientId" => await Count(db.Clients.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "officeId" or "office" => await Count(db.Offices.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "phaseId" => await Count(db.Phases.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "projectTypeId" => await Count(db.ProjectTypes.Where(x => ids.Contains(x.Id) && x.IsActive).Select(x => x.Id), ids),
                "unitId" or "leadUnitId" => ids.Length == 1 && ids[0] == Guid.Empty || await Count(db.Set<OrgUnit>().Where(u => ids.Contains(u.Id)).Select(u => u.Id), ids),
                "projectId" => await Count(db.Projects.Where(x => ids.Contains(x.Id)).Select(x => x.Id), ids),
                _ => true,
            };
    }
    static async Task<bool> Count(IQueryable<Guid> found, Guid[] ids) => ids.Length > 0 && await found.CountAsync() == ids.Length;
    public static readonly Hub.Api.Features.Col[] PortfolioCols = [
        new("projectNumber", "project"), new("name", "name"), new("pm.displayName", "pm"), new("client.name", "client"), new("office", "office"), new("phase", "phase"),
        new("status", "status"), new("computedHealth", "computedHealth"), new("reportedHealth", "reportedHealth"), new("why", "why"), new("healthOverrideNote", "overrideNote"),
        new("nextMilestone.name", "nextMilestone"), new("nextMilestone.date", "nextMilestoneDate", "date"), new("nextSubmission.name", "nextSubmission"),
        new("nextSubmission.date", "nextSubmissionDate", "date"), new("overdueTasks", "overdueTasks", "number"), new("blockedTasks", "blockedTasks", "number"),
        new("overdueDecisions", "overdueDecisions", "number"), new("highIssues", "highIssues", "number"), new("attentionCritical", "critical", "number"),
        new("attentionWarning", "warning", "number"),
    ];
    public static readonly Hub.Api.Features.Col[] WeeklyCommitmentsExportColumns = [new("key", "key"), new("work", "item"), new("performer", "performer"),
        new("weekStart", "weekStart", "date"), new("targetDate", "promiseTargetDate", "date"), new("intendedOutput", "intendedOutput"),
        new("completionCriteria", "completionCriteria"), new("state", "status"), new("readinessAtCommit", "readinessAtCommit"),
        new("snapshotAt", "snapshotAt", "datetime"), new("completionEvidenceUrl", "completionEvidence")];
    public static readonly Hub.Api.Features.Col[] ReadinessReadyColumns = [new("key", "key"), new("name", "name"), new("targetType", "type"), new("owner", "owner"),
        new("discipline", "discipline"), new("dueDate", "due", "date"), new("intendedOutput", "intendedOutput"),
        new("completionCriteria", "completionCriteria"), new("state", "readiness")];
    public static readonly Hub.Api.Features.Col[] ListExportsActivityCols = [
        new("occurredAt", "timestamp", "datetime"), new("actorName", "actor"), new("action", "action"), new("item", "item"), new("summary", "change"),
        new("reason", "reason"), new("source", "source"),
    ];
}
