using Hub.Domain;

namespace Hub.Api.Data;

// Staffing commitments are separate from task estimates and actual time entries.
public class PersonAvailabilityOverride : Audited, IAuditable
{
    public Guid PersonId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal AvailableHours { get; set; }
    public string Category { get; set; } = AvailabilityCategory.Reduced;
    public string AuditType => "PersonAvailabilityOverride";
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => null;
}

public class ResourceAllocation : CoordinationRecord
{
    public Guid PersonId { get; set; }
    public string Purpose { get; set; } = AllocationPurpose.Production;
    public DateOnly FromDate { get; set; }
    public DateOnly ThroughDate { get; set; }
    public decimal PlannedHours { get; set; }
    public string Status { get; set; } = AllocationStatus.Proposed;
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public string? OverCapacityReason { get; set; }
    public string? ConfirmationSnapshot { get; set; }
    public override string AuditType => "ResourceAllocation";
}

public class AllocationDayOverride : Audited, IAuditable
{
    public Guid AllocationId { get; set; }
    public DateOnly WorkDate { get; set; }
    public decimal Hours { get; set; }
    public string AuditType => "AllocationDayOverride";
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => null;
}

public class AllocationWorkLink : Audited, IAuditable
{
    public Guid AllocationId { get; set; }
    public Guid PersonId { get; set; }
    public string WorkType { get; set; } = "Task";
    public Guid WorkId { get; set; }
    public DateOnly WorkDate { get; set; }
    // Review assignments have no task estimate; the request supplies their explicit dated demand.
    public decimal? ReviewHours { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public string AuditType => "AllocationWorkLink";
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => null;
}

// Every capacity-affecting command updates this row with an expected version.
public class PersonDateVersion : Audited
{
    public Guid PersonId { get; set; }
    public DateOnly WorkDate { get; set; }
}
