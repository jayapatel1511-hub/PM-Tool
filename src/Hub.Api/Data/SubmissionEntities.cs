using Hub.Domain;

namespace Hub.Api.Data;

public class SubmissionPackage : ProjectItem, IAuditable
{
    public string Title { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string RecipientReference { get; set; } = "";
    public Guid CoordinatorId { get; set; }
    public Guid MilestoneId { get; set; }
    public DateOnly TargetDate { get; set; }
    public string Status { get; set; } = SubmissionStatus.Draft;
    public int ManifestVersion { get; set; } = 1;
    public Guid? SupersedesPackageId { get; set; }
    public string AuditType => "SubmissionPackage";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
}

public class SubmissionManifestItem : CoordinationRecord
{
    public Guid PackageId { get; set; }
    public int ManifestVersion { get; set; }
    public Guid DeliverableId { get; set; }
    public Guid SourceRevisionId { get; set; }
    public Guid? ReviewRoundId { get; set; }
    public bool Required { get; set; } = true;
    public override string AuditType => "SubmissionManifestItem";
}

public class SubmissionCheck : CoordinationRecord
{
    public Guid PackageId { get; set; }
    public int ManifestVersion { get; set; }
    public string Kind { get; set; } = "";
    public Guid? SourceId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public Guid OwnerId { get; set; }
    public bool Required { get; set; } = true;
    public string Status { get; set; } = SubmissionCheckStatus.Pending;
    public string? EvidenceRule { get; set; }
    public string? EvidenceUrl { get; set; }
    public string? Reason { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public override string AuditType => "SubmissionCheck";
}

public class CheckEvidence : CoordinationRecord
{
    public Guid CheckId { get; set; }
    public string EvidenceUrl { get; set; } = "";
    public string Note { get; set; } = "";
    public override string AuditType => "CheckEvidence";
}

// The database trigger prevents updates, deletes and truncation after insertion.
public class SubmissionIssue : Entity, IAuditable
{
    public Guid ProjectId { get; set; }
    public Guid PackageId { get; set; }
    public int ManifestVersion { get; set; }
    public string ManifestSnapshot { get; set; } = "[]";
    public string CheckSnapshot { get; set; } = "[]";
    public Guid AuthorisedBy { get; set; }
    public DateTimeOffset AuthorisedAt { get; set; }
    public string Destination { get; set; } = "";
    public string TransmittalUrl { get; set; } = "";
    public string AuditType => "SubmissionIssue";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => null;
    public string? AuditName => Destination;
}
