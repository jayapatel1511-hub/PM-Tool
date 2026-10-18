using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ProjectStatusNote
{
    public static async Task<JsonNode?> Read(Project project, HubDb db, TimeProvider clock)
        => (await ReadMany([project], db, clock)).GetValueOrDefault(project.Id);

    public static async Task<Dictionary<Guid, JsonNode?>> ReadMany(IEnumerable<Project> projects, HubDb db, TimeProvider clock)
    {
        var list = projects.Where(p => p.StatusNote != null).ToArray(); var ids = list.Select(p => p.Id).ToArray();
        var states = await db.ProjectStates.AsNoTracking().Where(s => ids.Contains(s.ProjectId)).ToDictionaryAsync(s => s.ProjectId, s => s.ComputedHealth);
        var authorIds = list.Select(p => p.StatusNoteBy).OfType<Guid>().Distinct().ToArray();
        var authors = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var milestones = (await db.Milestones.AsNoTracking().Where(m => ids.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled && m.Date != null)
            .OrderBy(m => m.Date).ThenBy(m => m.Key).Select(m => new { m.ProjectId, m.Name, m.Date }).ToListAsync()).ToLookup(m => m.ProjectId);
        var attention = await db.Attention.AsNoTracking().Where(a => ids.Contains(a.ProjectId)).GroupBy(a => a.ProjectId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count);
        return list.ToDictionary(p => p.Id, p => {
            var computed = p.Status == ProjectStatus.Active ? states.GetValueOrDefault(p.Id, Health.Grey) : Health.Grey;
            var next = milestones[p.Id].FirstOrDefault();
            return JsonSerializer.SerializeToNode(new { Text = p.StatusNote, AuthorId = p.StatusNoteBy,
                Author = p.StatusNoteBy is { } author ? authors.GetValueOrDefault(author) : null, At = p.StatusNoteAt,
                Header = new { ComputedHealth = computed, ReportedHealth = p.Status == ProjectStatus.Active && p.HealthOverrideExpiresAt > clock.GetUtcNow() ? p.HealthOverride : null,
                    NextMilestone = next == null ? null : new { next.Name, next.Date }, OpenAttention = attention.GetValueOrDefault(p.Id) } }, JsonOpts.Web);
        });
    }
}
