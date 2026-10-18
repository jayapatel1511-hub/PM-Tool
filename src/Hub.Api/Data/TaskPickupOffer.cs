using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

/// The open handover state attached to a task (§53.2). It is not a project item.
public sealed class TaskPickupOffer : Entity, IAuditable
{
    public Guid TaskId { get; set; }
    public Guid OfferedById { get; set; }
    public Guid? AssigneeId { get; set; }
    public string Reason { get; set; } = "";
    public decimal? HoursRemaining { get; set; }
    public DateOnly? NeededBy { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset OpenedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public string? CloseCause { get; set; }
    public bool KeepWatching { get; set; } = true;

    public string AuditType => "TaskPickupOffer";
    public Guid? AuditProjectId => null;
    public string? AuditKey => TaskId.ToString();
    public string? AuditName => Reason;

    public const string PickedUp = "picked up";
    public const string AssignedByManager = "assigned by manager";
    public const string Withdrawn = "withdrawn";
    public const string TaskClosed = "task closed";
    public const string ReassignedElsewhere = "reassigned elsewhere";
}

/// Kept separate so HubDb can call this from its model builder without putting pickup rules in the DbContext.
public static class TaskPickupModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<TaskPickupOffer>(e =>
        {
            e.ToTable("task_pickup_offer");
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Note).HasMaxLength(4000);
            e.Property(x => x.HoursRemaining).HasPrecision(7, 2);
            e.HasIndex(x => x.TaskId).IsUnique().HasFilter("closed_at IS NULL");
            e.HasIndex(x => new { x.ClosedAt, x.OpenedAt });
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OfferedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AssigneeId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
