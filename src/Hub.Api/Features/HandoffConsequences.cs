using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record HandoffReadinessLinks(string State, string Scope, int? TaskCount, int? DirectTaskCount,
    int? ConstraintLinkedTaskCount, int? DatedTaskCount, DateOnly? FirstRecordedDueDate,
    bool HasPromisedDateMismatch, bool IsOverdue, bool IsHistoricalMismatch, bool? DirectReceiptSatisfied);
public sealed record HandoffTaskRelationship(string Relationship, Guid? ConstraintId, string? ConstraintKey,
    string? ConstraintState, DateOnly? ConstraintNeededBy, RecordContextPerson? RemovalOwner, RecordContextPerson? AffectedOwner);
public sealed record HandoffLinkedTask(Guid Id, string Key, string Name, string Status, DateOnly? DueDate,
    IReadOnlyList<HandoffTaskRelationship> Relationships);
public sealed record HandoffLinkedTaskPage(Guid ProjectId, Guid HandoffId, HandoffReadinessLinks Summary, Page<HandoffLinkedTask>? Tasks);

/// S38: explicit current source relationships, never a graph or a new blocking evaluator.
public static class HandoffConsequencesEndpoints
{
    const string Scope = "ProjectOpenReadinessLinks";
    internal sealed class Edge { public Guid HandoffId { get; init; } public Guid TaskId { get; init; } }
    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/handoffs/{id:guid}/readiness-linked-tasks", Read).RequireFlag("register_screens");

    static IQueryable<WorkConstraint> Constraints(HubDb db, Guid projectId, Guid[] ids) => db.WorkConstraints.AsNoTracking()
        .Where(c => c.ProjectId == projectId && c.LinkedType == ItemType.Handoff && c.LinkedId.HasValue && ids.Contains(c.LinkedId.Value)
            && c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled);

    internal static IQueryable<Edge> Edges(HubDb db, Guid projectId, Guid[] ids)
    {
        var tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled);
        var direct = from h in db.Handoffs.AsNoTracking()
            join t in tasks on h.TargetTaskId equals (Guid?)t.Id
            where h.ProjectId == projectId && ids.Contains(h.Id) && h.Status != HandoffStatus.Cancelled
            select new Edge { HandoffId = h.Id, TaskId = t.Id };
        var linked = from c in Constraints(db, projectId, ids)
            join t in tasks on c.TargetId equals t.Id
            where c.TargetType == ItemType.Task && (c.State == ConstraintState.Open || c.State == ConstraintState.ResolutionProposed)
            select new Edge { HandoffId = c.LinkedId!.Value, TaskId = t.Id };
        return direct.Union(linked);
    }

    // Caller owns project authorization, register_screens and its repeatable-read snapshot.
    internal static async Task<IReadOnlyDictionary<Guid, HandoffReadinessLinks>> ReadBatch(HubDb db, Project project, IEnumerable<Guid> selected, DateOnly today)
    {
        var ids = selected.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<Guid, HandoffReadinessLinks>();
        var handoffs = await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == project.Id && ids.Contains(h.Id)).ToArrayAsync();
        var bad = (await db.Handoffs.AsNoTracking().Where(h => h.ProjectId == project.Id && ids.Contains(h.Id) &&
            ((h.TargetTaskId == null) == (h.TargetDeliverableId == null)
            || h.TargetTaskId.HasValue && !db.Tasks.IgnoreQueryFilters().Any(t => t.ProjectId == project.Id && t.Id == h.TargetTaskId)
            || h.TargetDeliverableId.HasValue && !db.Deliverables.IgnoreQueryFilters().Any(d => d.ProjectId == project.Id && d.Id == h.TargetDeliverableId)))
            .Select(h => h.Id).ToListAsync()).ToHashSet();
        var badConstraints = await Constraints(db, project.Id, ids).Where(c =>
            (c.State != ConstraintState.Open && c.State != ConstraintState.ResolutionProposed)
            || (c.TargetType != ItemType.Task && c.TargetType != ItemType.Deliverable)
            || c.TargetType == ItemType.Task && !db.Tasks.IgnoreQueryFilters().Any(t => t.ProjectId == project.Id && t.Id == c.TargetId)
            || c.TargetType == ItemType.Deliverable && !db.Deliverables.IgnoreQueryFilters().Any(d => d.ProjectId == project.Id && d.Id == c.TargetId))
            .Select(c => c.LinkedId!.Value).Distinct().ToArrayAsync();
        bad.UnionWith(badConstraints);
        var edges = Edges(db, project.Id, ids);
        var counts = await (from e in edges join t in db.Tasks.AsNoTracking() on e.TaskId equals t.Id
            group t by e.HandoffId into g
            select new { Id = g.Key, Count = g.Count(), Dated = g.Count(t => t.DueDate.HasValue), First = g.Min(t => t.DueDate) }).ToDictionaryAsync(x => x.Id);
        var linkedCounts = await (from c in Constraints(db, project.Id, ids)
            join t in db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled) on c.TargetId equals t.Id
            where c.TargetType == ItemType.Task
            select new { HandoffId = c.LinkedId!.Value, TaskId = t.Id }).Distinct().GroupBy(x => x.HandoffId)
            .Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Id, x => x.Count);
        var directIds = (await (from h in db.Handoffs.AsNoTracking()
            join t in db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled) on h.TargetTaskId equals (Guid?)t.Id
            where h.ProjectId == project.Id && ids.Contains(h.Id) && h.Status != HandoffStatus.Cancelled
            select h.Id).ToArrayAsync()).ToHashSet();
        return handoffs.ToDictionary(h => h.Id, h => {
            var unknown = bad.Contains(h.Id); counts.TryGetValue(h.Id, out var count);
            var satisfied = h.Status is HandoffStatus.Accepted or HandoffStatus.Incorporated;
            var mismatch = HandoffRules.DateMismatch(h.NeededBy, h.PromisedBy);
            return new HandoffReadinessLinks(unknown ? "Unavailable" : "Known", Scope, unknown ? null : count?.Count ?? 0,
                unknown ? null : directIds.Contains(h.Id) ? 1 : 0, unknown ? null : linkedCounts.GetValueOrDefault(h.Id),
                unknown ? null : count?.Dated ?? 0, unknown ? null : count?.First, mismatch,
                HandoffRules.Overdue(h.Status, h.NeededBy, today, project.Status), mismatch && (satisfied || h.Status == HandoffStatus.Cancelled),
                !unknown && h.TargetTaskId.HasValue && h.Status != HandoffStatus.Cancelled ? satisfied : null);
        });
    }

    static async Task<Ok<HandoffLinkedTaskPage>> Read(Guid projectId, Guid id, int? page, int? pageSize,
        HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db); Http.RequireFlag(settings, "register_screens");
        await using var snapshot = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        var (project, _) = await access.Project(projectId, false);
        Check.That(http.Request.Query.Keys.All(k => k is "page" or "pageSize"), "query", "error.invalid");
        var h = await db.Handoffs.AsNoTracking().SingleOrDefaultAsync(h => h.ProjectId == projectId && h.Id == id) ?? throw ApiException.NotFound();
        var summary = (await ReadBatch(db, project, [id], clock.Today(settings)))[id];
        var (pg, size) = Http.Paging(page, pageSize);
        if (summary.State != "Known") return TypedResults.Ok(new HandoffLinkedTaskPage(projectId, id, summary, null));
        var taskIds = Edges(db, projectId, [id]).Select(e => e.TaskId);
        var tasks = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == projectId && taskIds.Contains(t.Id))
            .OrderBy(t => t.Key).ThenBy(t => t.Id).Skip((pg - 1) * size).Take(size).ToArrayAsync();
        var shown = tasks.Select(t => t.Id).ToArray();
        var constraints = await Constraints(db, projectId, [id]).Where(c => c.TargetType == ItemType.Task && shown.Contains(c.TargetId))
            .OrderBy(c => c.Key).ThenBy(c => c.Id).ToArrayAsync();
        var people = await WeeklyReadinessAgendaEndpoints.People(db, project, constraints.SelectMany(c => new[] { c.RemovalOwnerId, c.AffectedOwnerId }));
        RecordContextPerson Person(Guid value) => people.GetValueOrDefault(value) ?? new(value, null, "Unknown");
        var rows = tasks.Select(t => {
            var relationships = new List<HandoffTaskRelationship>();
            if (h.TargetTaskId == t.Id && h.Status != HandoffStatus.Cancelled) relationships.Add(new("DirectTarget", null, null, null, null, null, null));
            relationships.AddRange(constraints.Where(c => c.TargetId == t.Id).Select(c => new HandoffTaskRelationship("Constraint", c.Id, c.Key, c.State, c.NeededBy, Person(c.RemovalOwnerId), Person(c.AffectedOwnerId))));
            return new HandoffLinkedTask(t.Id, t.Key, t.Name, t.Status, t.DueDate, relationships);
        }).ToArray();
        return TypedResults.Ok(new HandoffLinkedTaskPage(projectId, id, summary, new(rows, pg, size, summary.TaskCount!.Value)));
    }
}
