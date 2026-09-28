using Hub.Domain;
namespace Hub.Api.Data;

public abstract class CoordinationRecord : Audited, IAuditable
{
    public Guid ProjectId { get; set; }
    public abstract string AuditType { get; }
    public Guid? AuditProjectId => ProjectId;
    public virtual string? AuditKey => null;
    public virtual string? AuditName => null;
}
public class CoordinationCommand : Entity
{
    public Guid ProjectId { get; set; }
    public Guid ActorId { get; set; }
    public Guid RequestId { get; set; }
    public string PayloadHash { get; set; } = "";
    public Guid ResultId { get; set; }
    public int ResultVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
public class ReviewPackage : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public Guid CoordinatorId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public string Status { get; set; } = ReviewStatus.Draft;
    public Guid? CurrentRoundId { get; set; }
    public int RoundNumber { get; set; }
    public bool RequiredForIssue { get; set; }
    public string AuditType => "ReviewPackage";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}
public class ReviewRound : CoordinationRecord
{
    public Guid PackageId { get; set; }
    public int Number { get; set; }
    public string Purpose { get; set; } = "";
    public string? Reason { get; set; }
    public string? RemovalImpact { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public string Status { get; set; } = ReviewStatus.Draft;
    public override string AuditType => "ReviewRound";
}
public class ReviewManifestItem : CoordinationRecord
{
    public Guid RoundId { get; set; }
    public Guid SourceRevisionId { get; set; }
    public Guid DeliverableId { get; set; }
    public Guid[] AuthorIds { get; set; } = [];
    public override string AuditType => "ReviewManifestItem";
}
public class DisciplineReview : CoordinationRecord
{
    public Guid RoundId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public Guid ReviewerId { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = DisciplineReviewStatus.Pending;
    public string? Rationale { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public override string AuditType => "DisciplineReview";
}
public class ReviewFinding : CoordinationRecord
{
    public Guid PackageId { get; set; }
    public Guid RoundId { get; set; }
    public Guid? CarriedFromId { get; set; }
    public Guid? IssueId { get; set; }
    public Guid SourceRevisionId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public Guid OriginatorId { get; set; }
    public Guid ResolverId { get; set; }
    public Guid VerifierId { get; set; }
    public string Text { get; set; } = "";
    public string Severity { get; set; } = "Blocking";
    public string Status { get; set; } = FindingStatus.Open;
    public string? Response { get; set; }
    public string? EvidenceUrl { get; set; }
    public Guid? WithdrawalAcknowledgedBy { get; set; }
    public override string AuditType => "ReviewFinding";
    public override string? AuditName => Text;
}
public class FindingEvent : CoordinationRecord
{
    public Guid FindingId { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public override string AuditType => "FindingEvent";
}
public class SourceHead : CoordinationRecord
{
    public string Identity { get; set; } = "";
    public Guid CurrentRevisionId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public override string AuditType => "SourceHead";
}
public class InputUse : CoordinationRecord
{
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid OwnerId { get; set; }
    public string SourceIdentity { get; set; } = "";
    public Guid SourceRevisionId { get; set; }
    public string IntendedUse { get; set; } = "";
    public DateTimeOffset AdoptedAt { get; set; }
    public Guid AdoptedBy { get; set; }
    public override string AuditType => "InputUse";
}
public class InputAdoption : CoordinationRecord
{
    public Guid InputUseId { get; set; }
    public Guid SourceRevisionId { get; set; }
    public string IntendedUse { get; set; } = "";
    public string Reason { get; set; } = "";
    public override string AuditType => "InputAdoption";
}
public class ChangeNotice : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public Guid OwnerId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public Guid OldRevisionId { get; set; }
    public Guid NewRevisionId { get; set; }
    public string Description { get; set; } = "";
    public string Scope { get; set; } = "";
    public DateOnly EffectiveDate { get; set; }
    public DateOnly AssessmentDueDate { get; set; }
    public string Status { get; set; } = ChangeStatus.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public string AuditType => "ChangeNotice";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}
public class ChangeAssessment : CoordinationRecord
{
    public Guid ChangeNoticeId { get; set; }
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ReviewerId { get; set; }
    public Guid? InputUseId { get; set; }
    public Guid? HandoffId { get; set; }
    public Guid RevisionUsedId { get; set; }
    public string Status { get; set; } = AssessmentStatus.Pending;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? Rationale { get; set; }
    public string? EvidenceUrl { get; set; }
    public Guid? CorrectionTaskId { get; set; }
    public decimal? EffortImpactHours { get; set; }
    public int? DateImpactDays { get; set; }
    public bool RetainOldRevision { get; set; } = true;
    public Guid? RetentionApprovedBy { get; set; }
    public string? RetentionReason { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public override string AuditType => "ChangeAssessment";
}
