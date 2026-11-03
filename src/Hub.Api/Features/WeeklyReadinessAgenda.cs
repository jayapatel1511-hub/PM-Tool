using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record WeeklyReadinessAgenda(Guid ProjectId, DateOnly From, DateOnly To, Guid? DisciplineId, string ConstraintScope,
    Page<WeeklyConstraintAgendaRow> Constraints, Page<WeeklyProposedAgendaRow> Proposals);
public sealed record WeeklyConstraintAgendaRow(Guid Id, string Key, string TargetType, Guid TargetId, string TargetState,
    string? TargetKey, string Description, string State, DateOnly NeededBy, RecordContextPerson RemovalOwner, RecordContextPerson AffectedOwner);
public sealed record WeeklyProposedAgendaRow(Guid Id, string Key, string TargetType, Guid TargetId, string TargetState,
    string? TargetKey, string IntendedOutput, DateOnly TargetDate, DateOnly WeekStart, RecordContextPerson Performer, string State);

/// Meeting scope, independent pages, no evaluation or command side effects.
public static class WeeklyReadinessAgendaEndpoints
{
    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/coordination/readiness-agenda", Read).RequireFlag("register_screens");
    internal static async Task<Dictionary<Guid, RecordContextPerson>> People(HubDb db, Project project, IEnumerable<Guid> source)
    {
        var ids = source.Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToArrayAsync();
        var current = (await Coordination.People(db, project).Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToArrayAsync()).ToHashSet();
        var removed = (await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == project.Id && ids.Contains(m.UserId) && m.RemovedAt != null).Select(m => m.UserId).ToArrayAsync()).ToHashSet();
        return ids.ToDictionary(id => id, id => { var user = users.SingleOrDefault(u => u.Id == id);
            return new RecordContextPerson(id, user?.DisplayName, user is null ? "Unknown" : !user.IsActive ? "Inactive" : current.Contains(id) ? "Active" : removed.Contains(id) ? "Removed" : "Unavailable"); });
    }

    sealed record Target(string Type, Guid Id, string Key, Guid DisciplineId, bool Deleted);
    static async Task<Ok<WeeklyReadinessAgenda>> Read(Guid projectId, DateOnly from, DateOnly to, Guid? disciplineId,
        int? constraintsPage, int? proposalsPage, int? pageSize, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "register_screens");
        await using var snapshot = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var (project, _) = await access.Project(projectId, false);
        Check.That(http.Request.Query.ContainsKey("from") && http.Request.Query.ContainsKey("to") && http.Request.Query.Keys.All(k =>
            k is "from" or "to" or "disciplineId" or "constraintsPage" or "proposalsPage" or "pageSize"), "query", "error.invalid");
        Check.That(to >= from && to.DayNumber - from.DayNumber <= 83, "to", "error.invalid");
        if (disciplineId.HasValue) Check.That(await db.ProjectDisciplines.AsNoTracking().AnyAsync(d => d.ProjectId == projectId && d.Id == disciplineId), "disciplineId", "coord.reference");
        var constraints = ReadinessEndpoints.ConstraintsThrough(db, projectId, to);
        var earliestWeek = DateOnly.FromDayNumber(Math.Max(0, from.DayNumber - 6));
        var proposals = WeeklyCommitmentsEndpoints.TargetWindow(db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId && c.State == CommitmentState.Proposed
            && c.SnapshotId == null && c.WeekStart >= earliestWeek && c.WeekStart <= to), from, to);
        if (disciplineId.HasValue)
        {
            constraints = constraints.Where(c => c.TargetType == ItemType.Task && db.Tasks.IgnoreQueryFilters().Any(t => t.ProjectId == projectId && t.Id == c.TargetId && t.ProjectDisciplineId == disciplineId)
                || c.TargetType == ItemType.Deliverable && db.Deliverables.IgnoreQueryFilters().Any(d => d.ProjectId == projectId && d.Id == c.TargetId && d.ProjectDisciplineId == disciplineId));
            proposals = proposals.Where(c => c.TargetType == ItemType.Task && db.Tasks.IgnoreQueryFilters().Any(t => t.ProjectId == projectId && t.Id == c.TargetId && t.ProjectDisciplineId == disciplineId)
                || c.TargetType == ItemType.Deliverable && db.Deliverables.IgnoreQueryFilters().Any(d => d.ProjectId == projectId && d.Id == c.TargetId && d.ProjectDisciplineId == disciplineId));
        }
        var (cp, size) = Http.Paging(constraintsPage, pageSize); var (pp, _) = Http.Paging(proposalsPage, pageSize);
        var ct = await constraints.CountAsync(); var pt = await proposals.CountAsync();
        var cs = await constraints.OrderBy(c => c.NeededBy).ThenBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((cp - 1) * size).Take(size).ToArrayAsync();
        var ps = await proposals.OrderBy(c => c.TargetDate).ThenBy(c => c.WeekStart).ThenBy(c => c.CreatedAt).ThenBy(c => c.Id).Skip((pp - 1) * size).Take(size).ToArrayAsync();
        var targets = cs.Select(c => (c.TargetType, c.TargetId)).Concat(ps.Select(c => (c.TargetType, c.TargetId))).Distinct().ToArray();
        var tids = targets.Where(x => x.TargetType == ItemType.Task).Select(x => x.TargetId).ToArray();
        var dids = targets.Where(x => x.TargetType == ItemType.Deliverable).Select(x => x.TargetId).ToArray();
        // Only explicitly referenced IDs are read; foreign facts are never projected.
        var foreign = await db.Tasks.IgnoreQueryFilters().AnyAsync(t => tids.Contains(t.Id) && t.ProjectId != projectId)
            || await db.Deliverables.IgnoreQueryFilters().AnyAsync(d => dids.Contains(d.Id) && d.ProjectId != projectId);
        Check.That(!foreign, "targetId", "coord.reference");
        var ts = await db.Tasks.IgnoreQueryFilters().AsNoTracking().Where(t => t.ProjectId == projectId && tids.Contains(t.Id))
            .Select(t => new Target(ItemType.Task, t.Id, t.Key, t.ProjectDisciplineId, t.DeletedAt != null)).ToListAsync();
        ts.AddRange(await db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(d => d.ProjectId == projectId && dids.Contains(d.Id))
            .Select(d => new Target(ItemType.Deliverable, d.Id, d.Key, d.ProjectDisciplineId, d.DeletedAt != null)).ToListAsync());
        var lookup = ts.ToDictionary(t => (t.Type, t.Id));
        Target? Find(string type, Guid id) => lookup.GetValueOrDefault((type, id)) is { Deleted: false } target ? target : null;
        var people = await People(db, project, cs.SelectMany(c => new[] { c.RemovalOwnerId, c.AffectedOwnerId }).Concat(ps.Select(p => p.PerformerId)));
        var crows = cs.Select(c => new WeeklyConstraintAgendaRow(c.Id, c.Key, c.TargetType, c.TargetId, Find(c.TargetType, c.TargetId) is null ? "Unavailable" : "Available",
            Find(c.TargetType, c.TargetId)?.Key, c.Description, c.State, c.NeededBy, people[c.RemovalOwnerId], people[c.AffectedOwnerId])).ToArray();
        var prows = ps.Select(c => new WeeklyProposedAgendaRow(c.Id, c.Key, c.TargetType, c.TargetId, Find(c.TargetType, c.TargetId) is null ? "Unavailable" : "Available",
            Find(c.TargetType, c.TargetId)?.Key, c.IntendedOutput, c.TargetDate, c.WeekStart, people[c.PerformerId], c.State)).ToArray();
        return TypedResults.Ok(new WeeklyReadinessAgenda(projectId, from, to, disciplineId, "NeededByThroughEndIncludingOverdue",
            new(crows, cp, size, ct), new(prows, pp, size, pt)));
    }
}
