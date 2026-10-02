namespace Hub.Api.Data;

/// FR-RDY-02: a PM or Discipline Lead's authorisation to start a task that is not Ready. It keeps the readiness
/// state and reasons that were acknowledged and is used by at most one start; a database trigger keeps it immutable.
public class TaskStartAuthorisation : CoordinationRecord
{
    public Guid TaskId { get; set; }
    public Guid AuthorisedBy { get; set; }
    public string Reason { get; set; } = "";
    public string ReadinessAtAuthorisation { get; set; } = "";
    public string[] Unknown { get; set; } = [];
    public string[] Blocked { get; set; } = [];
    public Guid? StartedBy { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public override string AuditType => "TaskStartAuthorisation";
}
