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
