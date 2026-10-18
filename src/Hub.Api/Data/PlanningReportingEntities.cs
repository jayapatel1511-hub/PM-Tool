namespace Hub.Api.Data;

/// Packet 047 template demand; offsets are anchored to template milestones.
public sealed class TemplateRole : Entity
{
    public Guid TemplateId { get; set; }
    public Guid? TemplateDisciplineId { get; set; }
    public string RoleLabel { get; set; } = "";
    public int SortOrder { get; set; }
    public Guid? StartMilestoneId { get; set; }
    public int StartOffsetDays { get; set; }
    public Guid? EndMilestoneId { get; set; }
    public int EndOffsetDays { get; set; }
    public decimal PlannedHours { get; set; }
    public string? Note { get; set; }
}

/// Aggregate route usage. Deliberately contains no actor, session, item, URL or text.
public sealed class RouteCount : Entity
{
    public DateOnly Day { get; set; }
    public string RouteKey { get; set; } = "";
    public int Opens { get; set; }
    public int Under500Ms { get; set; }
    public int From500To999Ms { get; set; }
    public int From1000To1999Ms { get; set; }
    public int From2000To4999Ms { get; set; }
    public int Over5000Ms { get; set; }
}
