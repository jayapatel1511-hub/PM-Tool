using Hub.Api.Data;
using Hub.Domain;

namespace Hub.Api.Infrastructure;

public sealed record AuditNote(string? Action, string? Reason, string? Key, string[]? Categories, string? Snapshot);

/// Per-request (or per-job) context that HubDb.SaveChanges writes into every activity-log row.
public sealed class AuditContext
{
    public Guid? ActorId { get; set; }
    public string ActorType { get; set; } = "User"; // User | System | Admin
    public Guid CorrelationId { get; set; } = Guid.CreateVersion7();
    public string Source { get; set; } = "API";
    /// Reason applied to every row of the next save unless a note overrides it.
    public string? Reason { get; set; }
    readonly Dictionary<object, AuditNote> notes = new(ReferenceEqualityComparer.Instance);

    public AuditContext Note(object entity, string? action = null, string? reason = null, string? key = null, string[]? categories = null, string? snapshot = null)
    {
        notes[entity] = new AuditNote(action, reason, key, categories, snapshot);
        return this;
    }

    public AuditNote? NoteFor(object entity) => notes.GetValueOrDefault(entity);
    public void Clear() { notes.Clear(); Reason = null; }

    public void AsSystem(string source = "Job") { ActorType = "System"; ActorId = null; Source = source; }
}

/// Explicit allow-lists of logged fields per entity (§20.4) and the category each change falls in.
public static class AuditRules
{
    public static readonly IReadOnlyDictionary<Type, string[]> Fields = new Dictionary<Type, string[]>
    {
        [typeof(AppUser)] = ["DisplayName", "Email", "JobTitle", "OfficeId", "SupervisorId", "IsActive", "WeeklyCapacityHours", "IsTemplateEditor"],
        [typeof(UserSystemRole)] = ["UserId", "Role", "Source"],
        [typeof(Office)] = ["Name", "Code", "TimeZone", "IsActive", "SortOrder"],
        [typeof(Discipline)] = ["Name", "Code", "Colour", "IsActive", "SortOrder"],
        [typeof(Client)] = ["Name", "ShortName", "IsActive", "SortOrder"],
        [typeof(ProjectType)] = ["Name", "IsActive", "SortOrder"],
        [typeof(Phase)] = ["Name", "IsActive", "SortOrder"],
        [typeof(DeliverableType)] = ["Name", "DefaultDisciplineId", "IsActive", "SortOrder"],
        [typeof(OrgSetting)] = ["Value"],
        [typeof(Holiday)] = ["OfficeId", "Date", "Name"],
        [typeof(Project)] = ["ProjectNumber", "Name", "ClientId", "ClientReference", "ProjectManagerId", "OfficeId", "ProjectTypeId", "Description",
            "Location", "Status", "PhaseId", "StartDate", "TargetCompletionDate", "Priority", "Visibility", "InternalNotes", "CoordinationDay",
            "AllowViewerComments", "HealthOverride", "HealthOverrideNote", "HealthOverrideExpiresAt", "LastCoordinationReviewedAt"],
        [typeof(ProjectDiscipline)] = ["DisciplineId", "LeadUserId", "IsActive", "SortOrder"],
        [typeof(ProjectMember)] = ["UserId", "Roles", "PrimaryDisciplineId", "RemovedAt"],
        [typeof(ExternalParty)] = ["Name", "Organisation", "Email", "Role", "IsClient", "Notes", "IsActive"],
        [typeof(Milestone)] = ["Name", "MilestoneType", "Date", "Description", "ProjectDisciplineId", "CompletesPhaseId", "IsClientFacing",
            "IsComplete", "CompletedDate", "IsCancelled", "CancelledReason", "DeletedAt"],
        [typeof(Deliverable)] = ["Name", "ProjectDisciplineId", "DeliverableTypeId", "Description", "OwnerId", "ReviewerId", "MilestoneId",
            "StartDate", "DueDate", "Priority", "Status", "Revision", "IssuedDate", "IssuedTo", "TransmittalUrl", "AcceptedDate",
            "OnHoldReason", "CancelledReason", "RequiresReview", "DeletedAt"],
        [typeof(WorkTask)] = ["Name", "Description", "ProjectDisciplineId", "DeliverableId", "MilestoneId", "AssigneeId", "ReviewerId",
            "RequiresReview", "Priority", "StartDate", "DueDate", "Status", "ProgressPct", "EstimatedHours", "ManualBlockType",
            "ManualBlockReason", "OnHoldReason", "CancelledReason", "ReviewRound", "DeletedAt"],
        [typeof(TaskDependency)] = ["PredecessorTaskId", "SuccessorTaskId", "LagDays", "Note", "DeletedAt"],
        [typeof(DeliverableDependency)] = ["PredecessorDeliverableId", "SuccessorDeliverableId", "LagDays", "Note", "DeletedAt"],
        [typeof(TaskTimeEntry)] = ["TaskId", "UserId", "WorkDate", "Hours", "Note", "DeletedAt"],
        [typeof(Decision)] = ["Subject", "Description", "OwnerUserId", "OwnerExternalPartyId", "RequiredByDate", "ImpactLevel",
            "ImpactDescription", "Status", "DecisionText", "DecisionDate", "DecidedById", "DeferralReason", "CancelledReason", "DeletedAt"],
        [typeof(ItemLink)] = ["TargetType", "TargetId", "Relation", "DeletedAt"],
        [typeof(Risk)] = ["Title", "Description", "OwnerId", "Probability", "Impact", "Mitigation", "TriggerIndicator", "ReviewDate",
            "Status", "RealisedIssueId", "ProjectDisciplineId", "DeletedAt"],
        [typeof(Issue)] = ["Title", "Description", "OwnerId", "Severity", "TargetResolutionDate", "Resolution", "ResolvedDate", "Status",
            "OriginRiskId", "ProjectDisciplineId", "DeletedAt"],
        [typeof(Meeting)] = ["Title", "MeetingDate", "MeetingType", "NotesLink", "CalendarEventId", "DeletedAt"],
        [typeof(MeetingAction)] = ["Text", "OwnerType", "OwnerUserId", "OwnerDisciplineId", "OwnerExternalPartyId", "DueDate", "Status",
            "RelatedTaskId", "RelatedDecisionId", "DeletedAt"],
        [typeof(Comment)] = ["CommentKind", "ReviewRound", "DeletedAt"],
        [typeof(DocumentLink)] = ["Title", "Url", "LinkType", "DeletedAt"],
        [typeof(CalendarEvent)] = ["ProjectId", "Type", "Title", "StartAt", "EndAt", "OwnerId", "Location", "Description", "Visibility", "CancelledAt"],
        [typeof(ProjectTemplate)] = ["Name", "Description", "ProjectTypeId", "Version", "Status"],
    };

    static readonly Dictionary<string, string> FieldCategory = new()
    {
        ["Status"] = "status", ["IsComplete"] = "status", ["IsCancelled"] = "status", ["CancelledAt"] = "status",
        ["AssigneeId"] = "assignment", ["ReviewerId"] = "assignment", ["OwnerId"] = "assignment", ["OwnerUserId"] = "assignment",
        ["OwnerExternalPartyId"] = "assignment", ["OwnerDisciplineId"] = "assignment", ["LeadUserId"] = "assignment",
        ["ProjectManagerId"] = "assignment", ["Roles"] = "assignment", ["UserId"] = "assignment",
        ["DueDate"] = "date", ["StartDate"] = "date", ["Date"] = "date", ["RequiredByDate"] = "date", ["IssuedDate"] = "date",
        ["CompletedDate"] = "date", ["TargetCompletionDate"] = "date", ["WorkDate"] = "date", ["ReviewDate"] = "date",
        ["TargetResolutionDate"] = "date", ["StartAt"] = "date", ["EndAt"] = "date", ["MeetingDate"] = "date",
        ["HealthOverride"] = "health", ["HealthOverrideNote"] = "health", ["HealthOverrideExpiresAt"] = "health",
        ["DeliverableId"] = "structure", ["MilestoneId"] = "structure", ["ProjectDisciplineId"] = "structure",
        ["DeletedAt"] = "deletion", ["RemovedAt"] = "deletion",
    };

    public static string? Category(string field) => FieldCategory.GetValueOrDefault(field);

    public static string? TypeCategory(IAuditable item) => item switch
    {
        AppUser or UserSystemRole or RefData or OrgSetting or Holiday or ProjectTemplate => "admin",
        Decision => "decision",
        Comment => "comment",
        TaskDependency or DeliverableDependency => "dependency",
        _ => null,
    };

    /// Writes that can change derived state queue the project for re-evaluation (§23.5).
    public static Guid? AffectsEvaluation(object e) => e switch
    {
        Project p => p.Id,
        Milestone m => m.ProjectId,
        Deliverable d => d.ProjectId,
        WorkTask t => t.ProjectId,
        TaskDependency td => td.ProjectId,
        DeliverableDependency dd => dd.ProjectId,
        Decision dc => dc.ProjectId,
        ItemLink il => il.ProjectId,
        Risk r => r.ProjectId,
        Issue i => i.ProjectId,
        MeetingAction a => a.ProjectId,
        ProjectDiscipline pd => pd.ProjectId,
        ProjectMember pm => pm.ProjectId,
        TaskCollaborator => null,
        AttentionSnooze s => s.ProjectId,
        _ => null,
    };
}

/// Wakes the evaluation worker after a save that queued outbox events.
public sealed class EvaluationSignal
{
    readonly SemaphoreSlim gate = new(0, 1);
    public void Poke() { if (gate.CurrentCount == 0) try { gate.Release(); } catch (SemaphoreFullException) { } }
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => gate.WaitAsync(timeout, ct);
}
