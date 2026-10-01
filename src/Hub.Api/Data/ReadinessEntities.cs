using Hub.Domain;

namespace Hub.Api.Data;

public class ReadinessAssessment : CoordinationRecord
{
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid OwnerId { get; set; }
    public string IntendedOutput { get; set; } = "";
    public string CompletionCriteria { get; set; } = "";
    public string State { get; set; } = ReadinessState.NeedsAssessment;
    public DateTimeOffset EvaluatedAt { get; set; }
    public override string AuditType => "ReadinessAssessment";
}

public class ReadinessCheckRecord : CoordinationRecord
{
    public Guid AssessmentId { get; set; }
    public string Code { get; set; } = "";
    public bool? Applies { get; set; }
    public bool? Satisfied { get; set; }
    public string? Reason { get; set; }
    public string? EvidenceUrl { get; set; }
    public Guid? RecordedBy { get; set; }
    public override string AuditType => "ReadinessCheck";
}

public class WorkConstraint : CoordinationRecord
{
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public Guid RemovalOwnerId { get; set; }
    public Guid AffectedOwnerId { get; set; }
    public DateOnly NeededBy { get; set; }
    public string SourceUrl { get; set; } = "";
    public string State { get; set; } = ConstraintState.Open;
    public string? ResolutionEvidenceUrl { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public override string AuditType => "WorkConstraint";
    public override string? AuditName => Description;
}

public class ReadinessException : CoordinationRecord
{
    public Guid AssessmentId { get; set; }
    public Guid BasisVersionId { get; set; }
    public Guid ApprovedBy { get; set; }
    public Guid VerifierId { get; set; }
    public string LimitedWork { get; set; } = "";
    public string Risk { get; set; } = "";
    public DateOnly ExpiresOn { get; set; }
    public override string AuditType => "ReadinessException";
}

/// Jay's Submission Gate relationship (2026-10-01): work waits on prerequisite submission packages in its own
/// project. Removal keeps the row, its reason and who removed it.
public class ReadinessSubmissionPrerequisite : CoordinationRecord
{
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid PackageId { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public string? RemovalReason { get; set; }
    public override string AuditType => "ReadinessSubmissionPrerequisite";
}

public class WeeklyPlanSnapshot : CoordinationRecord
{
    public DateOnly WeekStart { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public Guid CapturedBy { get; set; }
    public int CommittedCount { get; set; }
    public override string AuditType => "WeeklyPlanSnapshot";
}

public class OutputCommitment : CoordinationRecord
{
    public Guid? SnapshotId { get; set; }
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid PerformerId { get; set; }
    public string IntendedOutput { get; set; } = "";
    public string CompletionCriteria { get; set; } = "";
    public DateOnly TargetDate { get; set; }
    public DateOnly WeekStart { get; set; }
    public string ReadinessAtCommit { get; set; } = ReadinessState.NeedsAssessment;
    public string State { get; set; } = CommitmentState.Proposed;
    public string? CompletionEvidenceUrl { get; set; }
    public override string AuditType => "OutputCommitment";
    public override string? AuditName => IntendedOutput;
}

public class OutputCommitmentEvent : CoordinationRecord
{
    public Guid CommitmentId { get; set; }
    public string FromState { get; set; } = "";
    public string ToState { get; set; } = "";
    public string Reason { get; set; } = "";
    public string? EvidenceUrl { get; set; }
    public Guid ActorId { get; set; }
    public override string AuditType => "OutputCommitmentEvent";
}
