using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Presentation facts for an already authorised task page, inside the caller's existing read snapshot.
/// This never evaluates or changes canonical task/review state and never widens the source cohort.
public static class BoardScreenReads
{
    public sealed record LeadFact(string State, Guid? Id, string? Name);
    public sealed record BoardFact(DateTimeOffset? ReviewRequestedAt, int? ReviewWaitingDays, int ReviewStaleDays, LeadFact DisciplineLead, LeadFact ProjectManager);

    public static async Task<IReadOnlyList<TaskQueries.Row>> Apply(HubDb db, IReadOnlyList<TaskQueries.Row> rows, OrgSettings settings, DateTimeOffset now)
    {
        if (rows.Count == 0) return rows;
        var taskIds = rows.Select(row => row.Id).Distinct().ToArray();
        var projectIds = rows.Select(row => row.ProjectId).Distinct().ToArray();
        var disciplineIds = rows.Select(row => row.ProjectDisciplineId).Distinct().ToArray();
        var tasks = await db.Tasks.AsNoTracking().Where(task => taskIds.Contains(task.Id) && projectIds.Contains(task.ProjectId))
            .Select(task => new { task.Id, task.ProjectId, task.Status, task.ReviewRequestedAt }).ToDictionaryAsync(task => task.Id);
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(discipline => disciplineIds.Contains(discipline.Id) && projectIds.Contains(discipline.ProjectId))
            .Select(discipline => new { discipline.Id, discipline.ProjectId, discipline.LeadUserId }).ToDictionaryAsync(discipline => discipline.Id);
        var projects = await db.Projects.AsNoTracking().Where(project => projectIds.Contains(project.Id))
            .Select(project => new { project.Id, project.ProjectManagerId }).ToDictionaryAsync(project => project.Id);
        var peopleIds = disciplines.Values.Select(discipline => discipline.LeadUserId).OfType<Guid>()
            .Concat(projects.Values.Select(project => project.ProjectManagerId)).Distinct().ToArray();
        var people = await db.Users.AsNoTracking().Where(person => peopleIds.Contains(person.Id)).Select(person => new { person.Id, person.DisplayName })
            .ToDictionaryAsync(person => person.Id, person => person.DisplayName);
        LeadFact Person(Guid? id) => id is not { } value ? new("Unassigned", null, null)
            : people.TryGetValue(value, out var name) && !string.IsNullOrWhiteSpace(name) ? new("Known", value, name) : new("Unavailable", value, null);
        var today = Clock.LocalDate(now, settings);
        return rows.Select(row => {
            var source = tasks.GetValueOrDefault(row.Id);
            var requested = source is not null && source.ProjectId == row.ProjectId && source.Status == row.Status ? source.ReviewRequestedAt : null;
            int? days = null;
            if ((row.Status is TaskStatuses.ReadyForReview or TaskStatuses.InReview) && requested is { } entered && entered <= now)
            {
                var age = today.DayNumber - Clock.LocalDate(entered, settings).DayNumber;
                if (age >= 0) days = age;
            }
            var discipline = disciplines.GetValueOrDefault(row.ProjectDisciplineId);
            var lead = discipline is not null && discipline.ProjectId == row.ProjectId ? Person(discipline.LeadUserId) : new LeadFact("Unavailable", null, null);
            var manager = projects.TryGetValue(row.ProjectId, out var project) ? Person(project.ProjectManagerId) : new LeadFact("Unavailable", null, null);
            return row with { Board = new BoardFact(requested, days, settings.ReviewStaleDays, lead, manager) };
        }).ToArray();
    }
}
