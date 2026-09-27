using Hub.Domain;

namespace Hub.Api.Data;

public class DesignBasisEntry : ProjectItem, IAuditable
{
    public string Kind { get; set; } = BasisKind.Assumption;
    public string Title { get; set; } = "";
    public Guid OwnerId { get; set; }
    public Guid ProjectDisciplineId { get; set; }
    public Guid? IndependentApproverId { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public string AuditType => "DesignBasisEntry";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => Key;
    public string? AuditName => Title;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}

public class DesignBasisVersion : CoordinationRecord
{
    public Guid EntryId { get; set; }
    public int Number { get; set; }
    public Guid? SupersedesVersionId { get; set; }
    public string Status { get; set; } = BasisStatus.Proposed;
    public string Scope { get; set; } = "";
    public string Statement { get; set; } = "";
    public decimal? NumericValue { get; set; }
    public string? Units { get; set; }
    public string? SourceSystem { get; set; }
    public string? StableSourceId { get; set; }
    public string? SourceUrl { get; set; }
    public string? DeclaredRevision { get; set; }
    public DateOnly? ConfirmationDueDate { get; set; }
    public Guid? DecisionId { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? ConfirmationRationale { get; set; }
    public override string AuditType => "DesignBasisVersion";
    public override string? AuditName => Statement;
}

public class BasisUse : CoordinationRecord
{
    public Guid VersionId { get; set; }
    public string TargetType { get; set; } = "";
    public Guid TargetId { get; set; }
    public Guid OwnerId { get; set; }
    public string IntendedUse { get; set; } = "";
    public override string AuditType => "BasisUse";
}

public class BasisConflict : CoordinationRecord
{
    public Guid LeftVersionId { get; set; }
    public Guid RightVersionId { get; set; }
    public bool Resolved { get; set; }
    public Guid? ResolutionVersionId { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public override string AuditType => "BasisConflict";
}

public class BasisAssumptionDisposition : CoordinationRecord
{
    public Guid VersionId { get; set; }
    public string Scope { get; set; } = "";
    public Guid OwnerId { get; set; }
    public Guid ApprovedBy { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public string Reason { get; set; } = "";
    public override string AuditType => "BasisAssumptionDisposition";
}
