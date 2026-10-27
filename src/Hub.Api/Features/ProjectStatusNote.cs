using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ProjectStatusNote
{
    public sealed record NextMilestone(string Name, DateOnly? Date);
    public sealed record NoteHeader(string ComputedHealth, string? ReportedHealth, NextMilestone? NextMilestone, int OpenAttention);
    /// The project's status note with the health header beside it, as the Projects and Portfolio responses carry it under statusNote.
    public sealed record NoteView(string? Text, Guid? AuthorId, string? Author, DateTimeOffset? At, NoteHeader Header);

    public static async Task<NoteView?> Read(Project project, HubDb db, TimeProvider clock)
        => (await ReadMany([project], db, clock)).GetValueOrDefault(project.Id);

    public static async Task<Dictionary<Guid, NoteView>> ReadMany(IEnumerable<Project> projects, HubDb db, TimeProvider clock)
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
            return new NoteView(p.StatusNote, p.StatusNoteBy, p.StatusNoteBy is { } author ? authors.GetValueOrDefault(author) : null, p.StatusNoteAt,
                new(computed, p.Status == ProjectStatus.Active && p.HealthOverrideExpiresAt > clock.GetUtcNow() ? p.HealthOverride : null,
                    next == null ? null : new(next.Name, next.Date), attention.GetValueOrDefault(p.Id)));
        });
    }
}
