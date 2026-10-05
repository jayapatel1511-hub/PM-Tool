using Hub.Domain;

namespace Hub.Api.Data;

// Physical schema of §24. Table and column names are snake_case (HubDb.OnModelCreating).

public abstract class Entity { public Guid Id { get; set; } = Guid.CreateVersion7(); }

public abstract class Audited : Entity
{
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public int RowVersion { get; set; }
}

public interface ISoftDeletable { DateTimeOffset? DeletedAt { get; set; } Guid? DeletedBy { get; set; } }

/// Rows written to the activity log by HubDb.SaveChanges (§20.4).
public interface IAuditable
{
    string AuditType { get; }
    Guid? AuditProjectId { get; }
    string? AuditKey { get; }
    string? AuditName { get; }
    Guid? AuditDisciplineId => null;
}

// ---------- Organisation ----------

public class AppUser : Audited, IAuditable
{
    public string? EntraObjectId { get; set; }
    public string Email { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? JobTitle { get; set; }
    public Guid? OfficeId { get; set; }
    public Guid? SupervisorId { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal? WeeklyCapacityHours { get; set; }
    public DateTimeOffset? LastSignInAt { get; set; }
    public bool IsTemplateEditor { get; set; }
    public List<UserSystemRole> Roles { get; set; } = [];
    public string AuditType => ItemType.User;
    public Guid? AuditProjectId => null;
    public string? AuditKey => Email;
    public string? AuditName => DisplayName;
}

public class UserSystemRole : Entity, IAuditable
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = "";
    public string Source { get; set; } = RoleSource.Manual;
    public Guid? GrantedBy { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    public string AuditType => ItemType.User;
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => Role;
}

public abstract class RefData : Audited, IAuditable
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string AuditType => ItemType.ReferenceData;
    public Guid? AuditProjectId => null;
    public string? AuditKey => GetType().Name;
    public string? AuditName => Name;
}

public class Office : RefData { public string Code { get; set; } = ""; public string TimeZone { get; set; } = "America/Halifax"; }
public class Discipline : RefData { public string Code { get; set; } = ""; public string Colour { get; set; } = "#64748b"; }
public class Client : RefData { public string? ShortName { get; set; } }
public class ProjectType : RefData { }
public class Phase : RefData { }
public class DeliverableType : RefData { public Guid? DefaultDisciplineId { get; set; } }

public class OrgSetting : IAuditable
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "null"; // jsonb
    public Guid? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string AuditType => ItemType.Setting;
    public Guid? AuditProjectId => null;
    public string? AuditKey => Key;
    public string? AuditName => Key;
}

public class UserSetting
{
    public Guid UserId { get; set; }
    public bool DigestEnabled { get; set; } = true;
    public string? DigestTimeLocal { get; set; }
    public bool DenseRows { get; set; } = true;
    public bool WeeklySummaryEnabled { get; set; } = true;
    public string DigestSectionsOff { get; set; } = "[]"; // jsonb array of section codes (packet 020)
    public DateTimeOffset? LastDigestAt { get; set; }
    public DateTimeOffset? LastWeeklySummaryAt { get; set; }
}

/// A replayable response to a retried create (§25, Idempotency-Key, Rec); kept for a day.
public class IdempotencyRecord : Entity
{
    public Guid UserId { get; set; }
    public string Key { get; set; } = "";
    public string Path { get; set; } = "";
    public int Status { get; set; }
    public string? ContentType { get; set; }
    public byte[] Body { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
}

public class NotificationPreference : Entity
{
    public Guid UserId { get; set; }
    public string EventType { get; set; } = "";
    public bool InApp { get; set; }
    public bool Email { get; set; }
}

public class Holiday : Entity, IAuditable
{
    public Guid? OfficeId { get; set; }
    public DateOnly Date { get; set; }
    public string Name { get; set; } = "";
    public string AuditType => ItemType.Holiday;
    public Guid? AuditProjectId => null;
    public string? AuditKey => Date.ToString("yyyy-MM-dd");
    public string? AuditName => Name;
}

// ---------- Projects ----------

public class Project : Audited, IAuditable
{
    public string ProjectNumber { get; set; } = "";
    public string Name { get; set; } = "";
    public Guid ClientId { get; set; }
    public string? ClientReference { get; set; }
    public Guid ProjectManagerId { get; set; }
    public Guid OfficeId { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = ProjectStatus.Setup;
    public Guid? PhaseId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? TargetCompletionDate { get; set; }
    public string Priority { get; set; } = Domain.Priority.Medium;
    public string Visibility { get; set; } = Domain.Visibility.Open;
    public string? InternalNotes { get; set; }
    public string? CoordinationDay { get; set; }
    public bool AllowViewerComments { get; set; } = true;
    public string? HealthOverride { get; set; }
    public string? HealthOverrideNote { get; set; }
    public Guid? HealthOverrideBy { get; set; }
    public DateTimeOffset? HealthOverrideAt { get; set; }
    public DateTimeOffset? HealthOverrideExpiresAt { get; set; }
    public Guid? CreatedFromTemplateId { get; set; }
    public int? TemplateVersion { get; set; }
    public DateTimeOffset? LastCoordinationReviewedAt { get; set; }
    public Guid? LastCoordinationReviewedBy { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public int NextTaskSeq { get; set; } = 1;
    public int NextDeliverableSeq { get; set; } = 1;
    public int NextMilestoneSeq { get; set; } = 1;
    public int NextDecisionSeq { get; set; } = 1;
    public int NextRiskSeq { get; set; } = 1;
    public int NextIssueSeq { get; set; } = 1;
    public int NextActionSeq { get; set; } = 1;
    public int NextHandoffSeq { get; set; } = 1;
    public int NextReviewSeq { get; set; } = 1;
    public int NextSubmissionSeq { get; set; } = 1;
    public int NextChangeSeq { get; set; } = 1;
    public int NextBasisSeq { get; set; } = 1;
    public int NextConstraintSeq { get; set; } = 1;
    public int NextCommitmentSeq { get; set; } = 1;
    public string? ExternalSource { get; set; }
    public string? ExternalId { get; set; }
    public string AuditType => ItemType.Project;
    public Guid? AuditProjectId => Id;
    public string? AuditKey => ProjectNumber;
    public string? AuditName => Name;
}

public class ProjectDiscipline : Entity, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid DisciplineId { get; set; }
    public Guid? LeadUserId { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public Discipline? Discipline { get; set; }
    public string AuditType => ItemType.Discipline;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => null;
    public string? AuditName => Discipline?.Name;
    public Guid? AuditDisciplineId => Id;
}

public class ProjectMember : Entity, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public List<string> Roles { get; set; } = [];
    public Guid? PrimaryDisciplineId { get; set; }
    public Guid? AddedBy { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public string AuditType => ItemType.Member;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => null;
    public string? AuditName => null;
}

public class ProjectStar { public Guid UserId { get; set; } public Guid ProjectId { get; set; } public DateTimeOffset CreatedAt { get; set; } }

public class ExternalParty : Audited, IAuditable
{
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = "";
    public string? Organisation { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public bool IsClient { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string AuditType => ItemType.ExternalParty;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => null;
    public string? AuditName => Name;
}

public class ProjectHealthSnapshot : Entity
{
    public Guid ProjectId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public string ComputedHealth { get; set; } = Health.Grey;
    public string ReportedHealth { get; set; } = Health.Grey;
    public string Inputs { get; set; } = "{}";
    /// The day's task and register counts, for weekly trends such as the pilot measures (packet 011).
    public string Counts { get; set; } = "{}";
}

// ---------- Work ----------

public abstract class ProjectItem : Audited, ISoftDeletable
{
    public Guid ProjectId { get; set; }
    public string Key { get; set; } = "";
    public int Seq { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
}

public class Milestone : ProjectItem, IAuditable
{
    public string Name { get; set; } = "";
    public string MilestoneType { get; set; } = Domain.MilestoneType.Other;
    public DateOnly? Date { get; set; }
    public DateOnly? OriginalDate { get; set; }
    public string? Description { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public Guid? CompletesPhaseId { get; set; }
    public bool IsClientFacing { get; set; }
    public bool IsComplete { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public bool IsCancelled { get; set; }
    public string? CancelledReason { get; set; }
    public int SortOrder { get; set; }
    public Guid? TemplateMilestoneId { get; set; }
    public string AuditType => ItemType.Milestone;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Name;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

public class Deliverable : ProjectItem, IAuditable
{
    public Guid? RequiredReviewPackageId { get; set; }

    public string Name { get; set; } = "";
    public Guid ProjectDisciplineId { get; set; }
    public Guid DeliverableTypeId { get; set; }
    public string? Description { get; set; }
    public Guid? OwnerId { get; set; }
    public Guid? ReviewerId { get; set; }
    public Guid? MilestoneId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? OriginalStartDate { get; set; }
    public DateOnly? OriginalDueDate { get; set; }
    public string Priority { get; set; } = Domain.Priority.Medium;
    public string Status { get; set; } = DeliverableStatus.NotStarted;
    public string? PreviousStatus { get; set; }
    public string? Revision { get; set; }
    public DateOnly? IssuedDate { get; set; }
    public string? IssuedTo { get; set; }
    public string? TransmittalUrl { get; set; }
    public string? IssueNote { get; set; }
    public DateOnly? AcceptedDate { get; set; }
    public string? OnHoldReason { get; set; }
    public string? CancelledReason { get; set; }
    public bool RequiresReview { get; set; } = true;
    public Guid? TemplateDeliverableId { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public int SortOrder { get; set; }
    public string AuditType => ItemType.Deliverable;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Name;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

/// A manually registered immutable reference, shared with the later review/change packets.
public class SourceRevision : Audited, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid? DeliverableId { get; set; }
    public int SourceRowVersion { get; set; }
    public string SourceIdentity { get; set; } = "";
    public string SourceSystem { get; set; } = "Manual";
    public string ExternalIdentifier { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateTimeOffset? SourceCheckedAt { get; set; }
    public Guid? SupersedesId { get; set; }
    public Guid[] AuthorIds { get; set; } = [];
    public string IdentityHash { get; set; } = "";
    public string SourceKey { get; set; } = "";
    public string Title { get; set; } = "";
    public string Revision { get; set; } = "";
    public string Url { get; set; } = "";
    public string AuditType => "SourceRevision";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => SourceKey;
    public string? AuditName => Title;
}

/// One receipt per receiving owner and purpose. The sender is accountable for delivery.
public class Handoff : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public Guid SourceDeliverableId { get; set; }
    public int SourceRowVersion { get; set; }
    public string DeclaredRevision { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public Guid SendingDisciplineId { get; set; }
    public Guid ReceivingDisciplineId { get; set; }
    public Guid SendingOwnerId { get; set; }
    public Guid ReceivingOwnerId { get; set; }
    public Guid? TargetTaskId { get; set; }
    public Guid? TargetDeliverableId { get; set; }
    public string IntendedUse { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public DateOnly NeededBy { get; set; }
    public DateOnly? PromisedBy { get; set; }
    public string Status { get; set; } = HandoffStatus.Draft;
    public Guid? CurrentRevisionId { get; set; }
    public Guid? IncorporatedRevisionId { get; set; }
    public string AuditType => "Handoff";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => SendingDisciplineId;
}

/// Submission snapshots keep the actual sender, scope, dates and criteria after reassignment or resubmission.
public class HandoffRevision : Audited
{
    public Guid ProjectId { get; set; }
    public Guid HandoffId { get; set; }
    public Guid SourceRevisionId { get; set; }
    public Guid? PreviousRevisionId { get; set; }
    public Guid SendingOwnerId { get; set; }
    public Guid ReceivingOwnerId { get; set; }
    public string IntendedUse { get; set; } = "";
    public string AcceptanceCriteria { get; set; } = "";
    public DateOnly NeededBy { get; set; }
    public DateOnly PromisedBy { get; set; }
    public Guid? TargetTaskId { get; set; }
    public Guid? TargetDeliverableId { get; set; }
    public string? Response { get; set; }
}

public class HandoffReceiptEvent : Audited
{
    public Guid ProjectId { get; set; }
    public Guid HandoffId { get; set; }
    public Guid? RevisionId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string? Reason { get; set; }
    public string? CriteriaOutcome { get; set; }
}

/// Transactional receipts make concurrent and later retries safe; no confidential response body is cached.
public class HandoffCommand : Entity
{
    public Guid ProjectId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public Guid HandoffId { get; set; }
    public int ResultVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DeliverableIssue : Entity
{
    public Guid ProjectId { get; set; }
    public Guid DeliverableId { get; set; }
    public DateOnly IssuedDate { get; set; }
    public string? Revision { get; set; }
    public string? IssuedTo { get; set; }
    public string? TransmittalUrl { get; set; }
    public string? Note { get; set; }
    public Guid IssuedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class WorkTask : ProjectItem, IAuditable
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public Guid? DeliverableId { get; set; }
    public Guid? MilestoneId { get; set; }
    public Guid? AssigneeId { get; set; }
    public Guid? ReviewerId { get; set; }
    public bool RequiresReview { get; set; }
    public string Priority { get; set; } = Domain.Priority.Medium;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateOnly? OriginalStartDate { get; set; }
    public DateOnly? OriginalDueDate { get; set; }
    public string Status { get; set; } = TaskStatuses.NotStarted;
    public string? PreviousStatus { get; set; }
    public int ProgressPct { get; set; }
    public decimal? EstimatedHours { get; set; }
    public string? ManualBlockType { get; set; }
    public string? ManualBlockReason { get; set; }
    public DateTimeOffset? ManualBlockSetAt { get; set; }
    public Guid? ManualBlockSetBy { get; set; }
    public string? OnHoldReason { get; set; }
    public string? CancelledReason { get; set; }
    public int ReviewRound { get; set; }
    public int DueDateChangeCount { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset? ReviewRequestedAt { get; set; }
    public int SortOrder { get; set; }
    public Guid? TemplateTaskId { get; set; }
    public string AuditType => ItemType.Task;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Name;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

public class TaskDependency : Entity, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid PredecessorTaskId { get; set; }
    public Guid SuccessorTaskId { get; set; }
    public string DependencyType { get; set; } = "FinishToStart";
    public int LagDays { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => ItemType.Dependency;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }  // "1234-T0031 → 1234-T0042", set by the handler
    public string? AuditName => null;
}

public class DeliverableDependency : Entity, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid PredecessorDeliverableId { get; set; }
    public Guid SuccessorDeliverableId { get; set; }
    public int LagDays { get; set; }
    public string? Note { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => ItemType.Dependency;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }
    public string? AuditName => null;
}

public class TaskCollaborator : Entity
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public Guid? AddedBy { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

public class ItemWatcher : Entity
{
    public Guid ProjectId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public Guid UserId { get; set; }
    public string Source { get; set; } = "Manual"; // Mention, Manual, Assignment
}

public class TaskTimeEntry : Audited, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => ItemType.TimeEntry;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }
    public string? AuditName => null;
}

public class BoardOrder : Entity
{
    public Guid? ProjectId { get; set; }
    public Guid? WorkspaceId { get; set; }
    public Guid TaskId { get; set; }
    public double Position { get; set; }
}

// ---------- Registers ----------

public class Decision : ProjectItem, IAuditable
{
    public string Subject { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid RequestedById { get; set; }
    public Guid? OwnerUserId { get; set; }
    public Guid? OwnerExternalPartyId { get; set; }
    public DateOnly DateRequested { get; set; }
    public DateOnly RequiredByDate { get; set; }
    public DateOnly OriginalRequiredByDate { get; set; }
    public string ImpactLevel { get; set; } = Impact.Medium;
    public string ImpactDescription { get; set; } = "";
    public string Status { get; set; } = DecisionStatus.Pending;
    public string? DecisionText { get; set; }
    public DateOnly? DecisionDate { get; set; }
    public Guid? DecidedById { get; set; }
    public string? DeferralReason { get; set; }
    public string? CancelledReason { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public string AuditType => ItemType.Decision;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Subject;
}

public class ItemLink : Entity, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public string SourceType { get; set; } = "";
    public Guid SourceId { get; set; }
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Relation { get; set; } = ItemRelation.Related;
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => SourceType;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }
    public string? AuditName => null;
}

public class Risk : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid OwnerId { get; set; }
    public int Probability { get; set; } = 1;
    public int Impact { get; set; } = 1;
    public string? Mitigation { get; set; }
    public string? TriggerIndicator { get; set; }
    public DateOnly? ReviewDate { get; set; }
    public string Status { get; set; } = RiskStatus.Open;
    public Guid? RealisedIssueId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public string AuditType => ItemType.Risk;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

public class Issue : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public Guid RaisedById { get; set; }
    public Guid OwnerId { get; set; }
    public string Severity { get; set; } = Impact.Medium;
    public DateOnly DateRaised { get; set; }
    public DateOnly? TargetResolutionDate { get; set; }
    public string? Resolution { get; set; }
    public DateOnly? ResolvedDate { get; set; }
    public string Status { get; set; } = IssueStatus.Open;
    public string IssueType { get; set; } = Domain.IssueType.General;
    public Guid? OriginRiskId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public string AuditType => ItemType.Issue;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

public class Meeting : Audited, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = "";
    public DateOnly MeetingDate { get; set; }
    public string MeetingType { get; set; } = "Coordination";
    public string? NotesLink { get; set; }
    public Guid? CalendarEventId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => ItemType.Meeting;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => null;
    public string? AuditName => Title;
}

public class MeetingAction : ProjectItem, IAuditable
{
    public Guid MeetingId { get; set; }
    public string Text { get; set; } = "";
    public string OwnerType { get; set; } = ActionOwnerType.User;
    public Guid? OwnerUserId { get; set; }
    public Guid? OwnerDisciplineId { get; set; }
    public Guid? OwnerExternalPartyId { get; set; }
    public DateOnly? DueDate { get; set; }
    public string Status { get; set; } = ActionStatus.Open;
    public Guid? RelatedTaskId { get; set; }
    public Guid? RelatedDecisionId { get; set; }
    public DateTimeOffset? StatusChangedAt { get; set; }
    public DateTimeOffset LastActivityAt { get; set; }
    public string AuditType => ItemType.Action;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Text;
    public Guid? AuditDisciplineId => OwnerDisciplineId;
}

// ---------- Collaboration ----------

public class Comment : Entity, IAuditable
{
    public Guid ProjectId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public Guid AuthorId { get; set; }
    public string Body { get; set; } = "";
    public string CommentKind { get; set; } = Domain.CommentKind.General;
    public int? ReviewRound { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public bool DeletedByPm { get; set; }
    public string AuditType => Domain.ItemType.Comment;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }  // key of the commented item, set by the handler
    public string? AuditName => null;
}

public class CommentMention : Entity { public Guid CommentId { get; set; } public Guid UserId { get; set; } }

public class DocumentLink : Entity, ISoftDeletable, IAuditable
{
    public Guid ProjectId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string LinkType { get; set; } = Domain.LinkType.Other;
    public Guid AddedBy { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => Domain.ItemType.DocumentLink;
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey { get; set; }
    public string? AuditName => Title;
}

// ---------- System ----------

public class ActivityLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string ActorType { get; set; } = "User"; // User | System | Admin
    public Guid? ProjectId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public string ItemType { get; set; } = "";
    public Guid? ItemId { get; set; }
    public string? ItemKey { get; set; }
    public string? ItemName { get; set; }
    public string Action { get; set; } = "";
    public List<string> Categories { get; set; } = [];
    public string Changes { get; set; } = "[]"; // jsonb [{field, old, new}]
    public string? Reason { get; set; }
    public Guid? CorrelationId { get; set; }
    public string Source { get; set; } = "API"; // UI | API | Job | Migration
    public string? Snapshot { get; set; } // jsonb, deletions only
}

public class Notification : Entity
{
    public Guid UserId { get; set; }
    public string EventType { get; set; } = "";
    public Guid? ProjectId { get; set; }
    public string? ItemType { get; set; }
    public Guid? ItemId { get; set; }
    public string? ItemKey { get; set; }
    public string Title { get; set; } = "";
    public string? Body { get; set; }
    public string? LinkPath { get; set; }
    public string? CollapseKey { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? CorrelationId { get; set; }
    public int Count { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? EmailedAt { get; set; }
    public DateTimeOffset? DigestIncludedAt { get; set; }
}

public class EmailMessage : Entity
{
    public Guid[] RequiredPlanningEntryIds { get; set; } = [];
    public Guid[] RequiredProjectIds { get; set; } = [];
    public DateTimeOffset? SuppressedAt { get; set; }
    public Guid? UserId { get; set; }
    public string ToAddress { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyText { get; set; } = "";
    public string? BodyHtml { get; set; }
    public string Kind { get; set; } = "Immediate"; // Immediate | Digest | Weekly
    public string? DedupKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
}

public class ProjectFollow : Entity
{
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string Level { get; set; } = FollowLevel.AllActivity;
    public string Source { get; set; } = FollowSource.Assignment;
    public DateTimeOffset? LastSeenAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class OutboxEvent : Entity
{
    public string EventType { get; set; } = "ProjectChanged";
    public Guid? ProjectId { get; set; }
    public string Payload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

public class TaskState
{
    public Guid TaskId { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public bool IsDueSoon { get; set; }
    public bool IsWaiting { get; set; }
    public bool IsBlocked { get; set; }
    public DateOnly? BlockedSince { get; set; }
    public int DaysBlocked { get; set; }
    public string BlockedBy { get; set; } = "[]";
    public bool IsBlocking { get; set; }
    public int BlockingCount { get; set; }
    public List<Guid> BlockingTaskIds { get; set; } = [];
    public bool IsStale { get; set; }
    public int StaleDays { get; set; }
    public bool IsUnassigned { get; set; }
    public bool IsMissingDueDate { get; set; }
    public bool IsDateInconsistent { get; set; }
    public string InconsistencyDetail { get; set; } = "[]";
    public bool IsInactiveOwner { get; set; }
    public bool IsHeldPastDue { get; set; }
    public bool IsReviewStalled { get; set; }
    public List<Guid> AffectedMilestoneIds { get; set; } = [];
    public List<string> Notes { get; set; } = [];
    public DateTimeOffset EvaluatedAt { get; set; }
}

public class DeliverableState
{
    public Guid DeliverableId { get; set; }
    public Guid ProjectId { get; set; }
    public int? ProgressPct { get; set; }
    public int TaskTotal { get; set; }
    public int TaskComplete { get; set; }
    public int TaskCancelled { get; set; }
    public int TaskOpen { get; set; }
    public int TaskOverdue { get; set; }
    public int TaskBlocked { get; set; }
    public decimal EstimatedHoursTotal { get; set; }
    public decimal RemainingHours { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public bool IsDueSoon { get; set; }
    public bool IsAtRisk { get; set; }
    public string AtRiskReasons { get; set; } = "[]";
    public bool IsUnassigned { get; set; }
    public bool IsStale { get; set; }
    public bool IsDateInconsistent { get; set; }
    public string InconsistencyDetail { get; set; } = "[]";
    public int SlipDays { get; set; }
    public bool IsInactiveOwner { get; set; }
    public bool IssuedWithOpenWork { get; set; }
    public bool MilestoneCancelled { get; set; }
    public bool IsWaiting { get; set; }
    public bool IsBlocked { get; set; }
    public string BlockedBy { get; set; } = "[]";
    public int BlockingCount { get; set; } // explicit successors it holds up (packet 021)
    public List<Guid> DerivedPredecessorIds { get; set; } = [];
    public List<Guid> DerivedSuccessorIds { get; set; } = [];
    public DateTimeOffset EvaluatedAt { get; set; }
}

public class MilestoneState
{
    public Guid MilestoneId { get; set; }
    public Guid ProjectId { get; set; }
    public string? Status { get; set; }
    public string StatusReasons { get; set; } = "[]";
    public int? DaysRemaining { get; set; }
    public int SlipDays { get; set; }
    public int DeliverableTotal { get; set; }
    public int DeliverableIssued { get; set; }
    public int TaskTotal { get; set; }
    public int TaskComplete { get; set; }
    public int TaskOpen { get; set; }
    public int TaskOverdue { get; set; }
    public int TaskBlocked { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}

public class DecisionState
{
    public Guid DecisionId { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsOverdue { get; set; }
    public int DaysOverdue { get; set; }
    public bool IsDueSoon { get; set; }
    public int BlockingCount { get; set; }
    public List<Guid> BlockingTaskIds { get; set; } = [];
    public bool IsInactiveOwner { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}

public class ProjectState
{
    public Guid ProjectId { get; set; }
    public string ComputedHealth { get; set; } = Health.Grey;
    public string HealthReasons { get; set; } = "[]";
    public string Inputs { get; set; } = "{}";
    public string Counts { get; set; } = "{}";
    public string DisciplineStates { get; set; } = "[]";
    public Guid? NextMilestoneId { get; set; }
    public Guid? NextSubmissionMilestoneId { get; set; }
    public int? ProgressPct { get; set; }
    public int OverdueTasks { get; set; }
    public int BlockedTasks { get; set; }
    public int OverdueDecisions { get; set; }
    public int HighIssues { get; set; }
    public int AttentionCritical { get; set; }
    public int AttentionWarning { get; set; }
    public DateTimeOffset EvaluatedAt { get; set; }
}

public class AttentionItem : Entity
{
    public Guid ProjectId { get; set; }
    public string RuleId { get; set; } = "";
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public string? ItemKey { get; set; }
    public string? ItemName { get; set; }
    public string Severity { get; set; } = Domain.Severity.Info;
    public string Message { get; set; } = "";
    public string Why { get; set; } = "{}";
    public List<Guid> RouteToUserIds { get; set; } = [];
    public Guid? OwnerUserId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public int DaysOverdueOrBlocked { get; set; }
    public string? Priority { get; set; }
    public DateOnly? DueDate { get; set; }
    public DateTimeOffset FirstDetectedAt { get; set; }
    public DateTimeOffset LastEvaluatedAt { get; set; }
}

public class AttentionSnooze : Entity
{
    public Guid ProjectId { get; set; }
    public string RuleId { get; set; } = "";
    public string ItemType { get; set; } = "";
    public Guid ItemId { get; set; }
    public Guid SnoozedBy { get; set; }
    public DateTimeOffset SnoozedUntil { get; set; }
    public string Note { get; set; } = "";
    public string SeverityAtSnooze { get; set; } = Domain.Severity.Info;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
}

public class JobRun : Entity
{
    public string JobName { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public string Status { get; set; } = "Running";
    public string? Details { get; set; }
}

/// A named list definition (§18.4): filters, sort, columns and grouping, never results. Shared project views are edited
/// by several people, so they carry a row version like every other shared record.
public class SavedView : Audited
{
    public Guid OwnerId { get; set; }
    public string Scope { get; set; } = "Personal"; // Personal | Project
    public Guid? ProjectId { get; set; }
    public string ListType { get; set; } = "";
    public string Name { get; set; } = "";
    public string Filters { get; set; } = "{}";
    public string? Sort { get; set; }
    public string Columns { get; set; } = "[]";
    public string? GroupBy { get; set; }
    public bool IsDefault { get; set; }
}

public class Workspace : Entity
{
    public string Name { get; set; } = "";
    public Guid OwnerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public List<WorkspaceProject> Projects { get; set; } = [];
}

public class WorkspaceProject : Entity { public Guid WorkspaceId { get; set; } public Guid ProjectId { get; set; } public int SortOrder { get; set; } }

public class CalendarEvent : Audited, IAuditable
{
    public Guid? ProjectId { get; set; }
    public string Type { get; set; } = CalendarEventType.Meeting;
    public string Title { get; set; } = "";
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndAt { get; set; }
    public string TimeZone { get; set; } = "";
    public Guid OwnerId { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public string Visibility { get; set; } = EventVisibility.Project;
    public DateTimeOffset? CancelledAt { get; set; }
    public Guid? CancelledBy { get; set; }
    public string AuditType => ItemType.CalendarEvent;
    public Guid? AuditProjectId => Visibility == EventVisibility.Private ? null : ProjectId;
    public string? AuditKey => null;
    public string? AuditName => Title;
}

public class DashboardLayout { public Guid UserId { get; set; } public string Widgets { get; set; } = "[]"; public DateTimeOffset UpdatedAt { get; set; } }

// ---------- Templates (Phase 2) ----------

public class ProjectTemplate : Audited, IAuditable
{
    public Guid FamilyId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public Guid? ProjectTypeId { get; set; }
    public int? Version { get; set; }
    public string Status { get; set; } = TemplateStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public string AuditType => ItemType.Template;
    public Guid? AuditProjectId => null;
    public string? AuditKey => Version is null ? "draft" : $"v{Version}";
    public string? AuditName => Name;
}

public class TemplateDiscipline : Entity { public Guid TemplateId { get; set; } public Guid DisciplineId { get; set; } public int SortOrder { get; set; } public bool IsDefaultIncluded { get; set; } = true; }

public class TemplateDesignBasis : Entity
{
    public Guid TemplateId { get; set; }
    public Guid TemplateDisciplineId { get; set; }
    public string Kind { get; set; } = BasisKind.Assumption;
    public string Title { get; set; } = "";
    public string Scope { get; set; } = "";
    public string Statement { get; set; } = "";
    public decimal? NumericValue { get; set; }
    public string? Units { get; set; }
    public string? SourceSystem { get; set; }
    public string? StableSourceId { get; set; }
    public string? SourceUrl { get; set; }
    public string? DeclaredRevision { get; set; }
}

public class TemplateMilestone : Entity
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = "";
    public string MilestoneType { get; set; } = Domain.MilestoneType.Other;
    public int SortOrder { get; set; }
    public string Anchor { get; set; } = "ProjectStart"; // ProjectStart | PreviousMilestone | None
    public int? OffsetDaysFromAnchor { get; set; }
    public Guid? CompletesPhaseId { get; set; }
    public bool IsClientFacing { get; set; }
}

public class TemplateDeliverable : Entity
{
    public Guid TemplateId { get; set; }
    public Guid TemplateDisciplineId { get; set; }
    public string Name { get; set; } = "";
    public Guid DeliverableTypeId { get; set; }
    public Guid? TemplateMilestoneId { get; set; }
    public int? DueOffsetDays { get; set; }
    public bool RequiresReview { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Description { get; set; }
}

public class TemplateTask : Entity
{
    public Guid TemplateId { get; set; }
    public Guid? TemplateDeliverableId { get; set; }
    public Guid TemplateDisciplineId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool RequiresReview { get; set; }
    public string Priority { get; set; } = Domain.Priority.Medium;
    public decimal? EstimatedHours { get; set; }
    public int? DueOffsetDays { get; set; }
    public string AssignToRole { get; set; } = Domain.AssignToRole.Unassigned;
    public int SortOrder { get; set; }
}

public class TemplateDependency : Entity { public Guid TemplateId { get; set; } public Guid PredecessorTemplateTaskId { get; set; } public Guid SuccessorTemplateTaskId { get; set; } }
