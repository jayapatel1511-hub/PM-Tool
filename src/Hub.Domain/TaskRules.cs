namespace Hub.Domain;

/// §55.3 Backlog (FR-BKL-01, 051-D5): derived from a task's status and dates, never stored and never a status.
public static class TaskRules
{
    /// `flag.backlog` (FR-BKL-09): the filter, row field, counts, export column and the FR-BKL-05 exclusions.
    public const string BacklogFlag = "backlog";

    /// Open in the Backlog sense: Not Started or On Hold. Cancelled, Complete and started work are never in it.
    public static readonly string[] BacklogStatuses = [TaskStatuses.NotStarted, TaskStatuses.OnHold];

    /// "No due date and no start date": an open task with neither date. Setting either date schedules it.
    public static bool IsBacklog(string status, DateOnly? start, DateOnly? due) => start is null && due is null && BacklogStatuses.Contains(status);
}
