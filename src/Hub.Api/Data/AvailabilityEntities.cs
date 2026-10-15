namespace Hub.Api.Data;

public sealed class ApprovedLeave : Audited, IAuditable
{
    public ApprovedLeave() { RowVersion = 1; }
    public Guid PersonId { get; set; }
    public string SourceSystem { get; set; } = "";
    public string ExternalId { get; set; } = "";
    public string SourceKind { get; set; } = "HRImport";
    public string Status { get; set; } = "Approved";
    public DateOnly FromDate { get; set; }
    public DateOnly ThroughDate { get; set; }
    public decimal? HoursPerDay { get; set; }
    public DateTimeOffset SourceUpdatedAt { get; set; }
    public DateTimeOffset ImportedAt { get; set; }
    public string AuditType => "ApprovedLeave";
    public Guid? AuditProjectId => null;
    public string? AuditKey => ExternalId;
    public string? AuditName => null;
}

public sealed class LeaveImportState : Audited, IAuditable
{
    public LeaveImportState() { RowVersion = 1; }
    public string SourceSystem { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public DateTimeOffset SourceUpdatedAt { get; set; }
    public string AuditType => "LeaveImportState";
    public Guid? AuditProjectId => null;
    public string? AuditKey => SourceSystem;
    public string? AuditName => null;
}

public sealed class PersonSchedule : Audited, IAuditable
{
    public PersonSchedule() { RowVersion = 1; }
    public Guid PersonId { get; set; }
    public DateOnly FromDate { get; set; }
    public decimal WeeklyHours { get; set; }
    public int[] WorkingDays { get; set; } = [1, 2, 3, 4, 5];
    public string AuditType => "PersonSchedule";
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => null;
}

public sealed class CalendarSubscription : Audited, IAuditable
{
    public CalendarSubscription() { RowVersion = 1; }
    public Guid UserId { get; set; }
    public string? TokenHash { get; set; }
    public string AuditType => "CalendarSubscription";
    public Guid? AuditProjectId => null;
    public string? AuditKey => null;
    public string? AuditName => null;
}

public sealed class CalendarFeedItem : Audited
{
    public CalendarFeedItem() { RowVersion = 1; }
    public Guid UserId { get; set; }
    public string SourceKey { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public DateOnly FromDate { get; set; }
    public DateOnly ThroughDate { get; set; }
    public int Sequence { get; set; }
    public bool Active { get; set; }
}

public sealed class LeaveImportPreview : Entity
{
    public Guid UserId { get; set; }
    public string RowsHash { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
}
