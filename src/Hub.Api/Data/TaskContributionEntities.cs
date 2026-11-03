namespace Hub.Api.Data;

/// A collaborator's own contribution, independently versioned from the accountable task.
public sealed class TaskContribution : Audited, IAuditable
{
    public Guid CollaboratorId { get; set; }
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public string TaskKeySnapshot { get; set; } = "";
    public string TaskNameSnapshot { get; set; } = "";
    public DateTimeOffset? MarkedAt { get; set; }
    public string? Note { get; set; }

    public string AuditType => "TaskContribution";
    public Guid? AuditProjectId => ProjectId;
    public string? AuditKey => TaskKeySnapshot;
    public string? AuditName => TaskNameSnapshot;
}
