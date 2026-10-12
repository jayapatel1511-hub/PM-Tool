using Hub.Domain;

namespace Hub.Api.Data;

/// FR-ROLE-01: unfilled project demand for a role before a person is named. It is filled by proposing a normal
/// resource allocation that names it (D1), and it never enters a person's capacity (D3).
public sealed class OpenRole : CoordinationRecord, ISoftDeletable, IAuditable
{
    public string RoleLabel { get; set; } = "";
    public Guid? ProjectDisciplineId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ThroughDate { get; set; }
    public decimal PlannedHours { get; set; }
    public string Status { get; set; } = OpenRoleStatus.Open;
    public Guid? FilledAllocationId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public override string AuditType => "OpenRole";
    public override string? AuditName => RoleLabel;
    public Guid? AuditDisciplineId => ProjectDisciplineId;
}
