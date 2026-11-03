using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-SCR-45: one visible-project deadline adapter for planner and allocation week headers.
public static class PlanningDeadlines
{
    public sealed record PlanningDeadlineRow(string ItemType, Guid Id, string Key, string Name, Guid ProjectId, string ProjectNumber, DateOnly? Date);
    public sealed record PlanningDeadlineContext(DateOnly[] Weeks, PlanningDeadlineRow[] Dated, PlanningDeadlineRow[] Undated, DateTimeOffset AsOf);

    internal static async Task<PlanningDeadlineContext> Read(IEnumerable<Guid> linkedProjects, DateOnly[] weeks, Access access, HubDb db, TimeProvider clock)
    {
        var wanted = linkedProjects.Distinct().ToArray();
        var visible = access.VisibleProjects().Where(p => wanted.Contains(p.Id));
        var projects = await visible.Select(p => new { p.Id, p.ProjectNumber }).ToDictionaryAsync(p => p.Id, p => p.ProjectNumber);
        var ids = projects.Keys.ToArray(); var from = weeks[0]; var through = weeks[^1].AddDays(6);
        var milestones = await db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled
                && (m.Date == null || m.Date >= from && m.Date <= through))
            .Select(m => new { m.Id, m.Key, m.Name, m.ProjectId, m.Date }).ToListAsync();
        var deliverables = await db.Deliverables.AsNoTracking().Where(d => ids.Contains(d.ProjectId)
                && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted
                && (d.DueDate == null || d.DueDate >= from && d.DueDate <= through))
            .Select(d => new { d.Id, d.Key, d.Name, d.ProjectId, d.DueDate }).ToListAsync();
        var rows = milestones.Select(m => new PlanningDeadlineRow(ItemType.Milestone, m.Id, m.Key, m.Name, m.ProjectId, projects[m.ProjectId], m.Date))
            .Concat(deliverables.Select(d => new PlanningDeadlineRow(ItemType.Deliverable, d.Id, d.Key, d.Name, d.ProjectId, projects[d.ProjectId], d.DueDate)))
            .OrderBy(d => d.Date).ThenBy(d => d.ItemType).ThenBy(d => d.ProjectNumber).ThenBy(d => d.Key).ThenBy(d => d.Id).ToArray();
        return new(weeks, rows.Where(d => d.Date != null).ToArray(), rows.Where(d => d.Date == null).ToArray(), clock.GetUtcNow());
    }
}
