namespace Hub.Api.Data;

/// Person-scoped weekly planning record (§10.9, packet 034). It is deliberately separate
/// from project allocations and task estimates.
public sealed class PlanningEntry : Audited, ISoftDeletable, IAuditable
{
    public Guid PersonId { get; set; }
    public decimal HoursPerWeek { get; set; }
    public DateOnly StartWeek { get; set; }
    public DateOnly EndWeek { get; set; }
    public string Label { get; set; } = "";
    public string SourceCategory { get; set; } = "Other";
    public Guid? ProjectId { get; set; }
    public Guid? ProjectDisciplineId { get; set; }
    public string Confidence { get; set; } = "Expected";
    public string Visibility { get; set; } = "Draft";
    public string? Notes { get; set; }
    public DateTimeOffset LastValidatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public string AuditType => "PlanningEntry";
    public Guid? AuditProjectId => null;
    public string? AuditKey => Id.ToString();
    public string? AuditName => Label;
}
