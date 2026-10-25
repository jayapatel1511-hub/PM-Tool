using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

public sealed class OrgLevel : Audited, IAuditable
{
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public string AuditType => ItemType.ReferenceData;
    public Guid? AuditProjectId => null;
    public string? AuditKey => nameof(OrgLevel);
    public string? AuditName => Name;
}

public sealed class OrgUnit : Audited, IAuditable
{
    public string Name { get; set; } = "";
    public Guid? ParentId { get; set; }
    public Guid LevelId { get; set; }
    public Guid? LeaderId { get; set; }
    public Guid? DisciplineId { get; set; }
    public Guid? OfficeId { get; set; }
    public DateOnly? ClosedOn { get; set; }
    public string AuditType => ItemType.ReferenceData;
    public Guid? AuditProjectId => null;
    public string? AuditKey => Id.ToString();
    public string? AuditName => Name;
}

/// Shared model configuration called by HubDb.OnModelCreating (kept separate so schema work has one seam).
public static class OrgStructureModel
{
    public static void Configure(ModelBuilder mb)
    {
        mb.Entity<OrgLevel>(e =>
        {
            // HubDb renames every table afterwards, but these names still fix the foreign-key names the migrations
            // recorded (fk_app_user_org_units_unit_id); removing them is a model change.
            e.ToTable("org_levels", "hub");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasIndex(x => x.SortOrder).IsUnique();
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });
        mb.Entity<OrgUnit>(e =>
        {
            e.ToTable("org_units", "hub");
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.ParentId, x.Name }).IsUnique();
            e.HasIndex(x => x.LevelId);
            e.HasIndex(x => x.LeaderId);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.HasOne<OrgUnit>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<OrgLevel>().WithMany().HasForeignKey(x => x.LevelId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.LeaderId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Discipline>().WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<Office>().WithMany().HasForeignKey(x => x.OfficeId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
