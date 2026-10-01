using Hub.Domain;

namespace Hub.Api.Data;

/// Structured, permission-scoped context attached to an existing coordination issue.
public sealed class IssueLocation : CoordinationRecord
{
    public Guid IssueId { get; set; }
    public int IssueRowVersion { get; set; }
    public string Kind { get; set; } = "SiteArea";
    public string? SiteArea { get; set; }
    public string? Building { get; set; }
    public string? Level { get; set; }
    public string? Room { get; set; }
    public string? AssetSystem { get; set; }
    public string? Alignment { get; set; }
    public decimal? StartStation { get; set; }
    public decimal? EndStation { get; set; }
    public string? StationUnits { get; set; }
    public decimal? CoordinateX { get; set; }
    public decimal? CoordinateY { get; set; }
    public decimal? CoordinateZ { get; set; }
    public string? CoordinateReferenceSystem { get; set; }
    public string? CoordinateUnits { get; set; }
    public override string AuditType => "IssueLocation";
    public override string? AuditName => Kind;
}

public sealed class IssueDocumentReference : CoordinationRecord
{
    public Guid IssueId { get; set; }
    public int IssueRowVersion { get; set; }
    public string Kind { get; set; } = "Drawing";
    public string Identifier { get; set; } = "";
    public string Revision { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string? ExternalTopicId { get; set; }
    public string? ModelElementGuid { get; set; }
    public string? ViewpointUrl { get; set; }
    public bool IsAvailable { get; set; } = true;
    public override string AuditType => "IssueDocumentReference";
    public override string? AuditName => Identifier;
}

public sealed class IssueVerification : CoordinationRecord
{
    public Guid IssueId { get; set; }
    public int IssueRowVersion { get; set; }
    public Guid VerifierId { get; set; }
    public string Status { get; set; } = IssueVerificationStatus.Proposed;
    public string? EvidenceUrl { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public override string AuditType => "IssueVerification";
}

/// Records the required owner and independent verifier disposition when a linked source revision changes.
public sealed class IssueReferenceImpactAssessment : CoordinationRecord
{
    public Guid IssueId { get; set; }
    public Guid DocumentReferenceId { get; set; }
    public Guid PreviousRevisionId { get; set; }
    public Guid CurrentRevisionId { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? VerifierId { get; set; }
    public string Status { get; set; } = IssueReferenceImpactStatus.Pending;
    public string? OwnerDisposition { get; set; }
    public string? OwnerReason { get; set; }
    public Guid? OwnerDecidedBy { get; set; }
    public DateTimeOffset? OwnerDecidedAt { get; set; }
    public string? VerifierDisposition { get; set; }
    public string? VerifierReason { get; set; }
    public Guid? VerifierDecidedBy { get; set; }
    public DateTimeOffset? VerifierDecidedAt { get; set; }
    public override string AuditType => "IssueReferenceImpactAssessment";
}

/// FR-LOC-03: a project discipline affected by the issue, alongside its single resolution owner and primary discipline.
public sealed class IssueAffectedDiscipline : CoordinationRecord, IAuditable
{
    public Guid IssueId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public override string AuditType => "IssueAffectedDiscipline";
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}
