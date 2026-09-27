using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Finish-to-Start dependencies (§12.6, D-01..D-18, FR-DEP-01..08).
public static class DependencyEndpoints
{
    public sealed record AddBody(Guid? PredecessorTaskId, Guid? SuccessorTaskId, string? Note, int? LagDays);
    public sealed record DeliverableBody(Guid? PredecessorDeliverableId, Guid? SuccessorDeliverableId, string? Note, int? LagDays);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/tasks/{id:guid}/dependencies", async (Guid id, Access access, HubDb db) =>
        {
            var (t, _, _) = await TaskEndpoints.Load(db, access, id);
            return new { dependsOn = await Edges(db, t.Id, predecessors: true), blocks = await Edges(db, t.Id, predecessors: false) };
        });

        api.MapPost("/tasks/{id:guid}/dependencies", async (Guid id, AddBody body, Access access, HubDb db) =>
        {
            var (t, p, ctx) = await TaskEndpoints.Load(db, access, id);
            var (pred, succ) = body.PredecessorTaskId is { } pr ? (pr, t.Id) : body.SuccessorTaskId is { } su ? (t.Id, su) : throw ApiException.Invalid("predecessorTaskId", "error.required");
            var (edge, warnings) = await Tx.Run(db, async () =>
            {
                var r = await AddEdge(db, access, p, ctx, pred, succ, body.Note, body.LagDays ?? 0);
                await db.SaveChangesAsync();
                return r;
            });
            return Results.Created($"/api/v1/dependencies/{edge.Id}", new { edge.Id, warnings });
        });

        api.MapDelete("/dependencies/{id:guid}", async (Guid id, Access access, HubDb db, Notifier notify, TimeProvider clock, CurrentUser me) =>
        {
            var e = await db.Dependencies.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (p, ctx) = await access.Project(e.ProjectId);
            var pred = await db.Tasks.FirstAsync(x => x.Id == e.PredecessorTaskId);
            var succ = await db.Tasks.FirstAsync(x => x.Id == e.SuccessorTaskId);
            Access.Demand(Permissions.ManageDependency(access.Actor, ctx, await TaskEndpoints.Facts(db, pred), await TaskEndpoints.Facts(db, succ))); // D-10
            e.DeletedAt = clock.GetUtcNow();
            e.DeletedBy = me.Id;
            e.AuditKey = $"{pred.Key} → {succ.Key}";
            await db.SaveChangesAsync();
            await notify.Send(NotificationEvents.DependencyRemoved, succ.AssigneeId, TaskEndpoints.Item(p, succ),
                Text.Get("notify.dependency_removed", await notify.ActorName(), pred.Key, succ.Key, succ.Name));
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // FR-DEP-09 (packet 021): explicit Finish-to-Start links between deliverables, with an optional lag; loops refused (D-03).
        api.MapPost("/deliverables/{id:guid}/dependencies", async (Guid id, DeliverableBody body, Access access, HubDb db) =>
        {
            var d = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (p, ctx) = await access.Project(d.ProjectId);
            Check.That(body.PredecessorDeliverableId is null != body.SuccessorDeliverableId is null, "predecessorDeliverableId", "dep.one_end");
            var (predId, succId) = body.PredecessorDeliverableId is { } pid ? (pid, id) : (id, body.SuccessorDeliverableId!.Value);
            var edge = await Tx.Run(db, async () =>
            {
                await db.Database.ExecuteSqlAsync($"SELECT 1 FROM hub.project WHERE id = {p.Id} FOR UPDATE");
                var pred = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == predId) ?? throw ApiException.Invalid("predecessorDeliverableId", "error.not_found");
                var succ = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == succId) ?? throw ApiException.Invalid("successorDeliverableId", "error.not_found");
                if (pred.ProjectId != p.Id || succ.ProjectId != p.Id) throw ApiException.Rule("cross_project", "dep.same_project");
                Access.Demand(Permissions.ManageDeliverableDependency(access.Actor, ctx, pred.ProjectDisciplineId, succ.ProjectDisciplineId));
                if (pred.Id == succ.Id) throw ApiException.Conflict("dependency_self", "dep.self");
                if (await db.DeliverableDependencies.AnyAsync(x => x.PredecessorDeliverableId == pred.Id && x.SuccessorDeliverableId == succ.Id))
                    throw ApiException.Conflict("dependency_duplicate", "dep.duplicate");
                var edges = await db.DeliverableDependencies.Where(x => x.ProjectId == p.Id).Select(x => new { x.PredecessorDeliverableId, x.SuccessorDeliverableId }).ToListAsync();
                if (Graph.CycleIfAdded(edges.Select(e => (e.PredecessorDeliverableId, e.SuccessorDeliverableId)), pred.Id, succ.Id) is { } loop)
                {
                    var keys = await db.Deliverables.Where(x => loop.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Key);
                    var path = loop.Select(x => keys[x]).ToList();
                    throw ApiException.Conflict("dependency_cycle", "dep.cycle", new { cyclePath = path }, string.Join(" → ", path));
                }
                var lag = body.LagDays ?? 0;
                Check.That(lag is >= 0 and <= 365, "lagDays", "dep.lag");
                var edge = new DeliverableDependency { ProjectId = p.Id, PredecessorDeliverableId = pred.Id, SuccessorDeliverableId = succ.Id, LagDays = lag,
                    Note = Check.Optional(body.Note, "note", 500), CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = $"{pred.Key} → {succ.Key}" };
                db.DeliverableDependencies.Add(edge);
                await db.SaveChangesAsync();
                return edge;
            });
            return Results.Created($"/api/v1/deliverable-dependencies/{edge.Id}", new { edge.Id });
        });

        api.MapDelete("/deliverable-dependencies/{id:guid}", async (Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
        {
            var e = await db.DeliverableDependencies.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (_, ctx) = await access.Project(e.ProjectId);
            var ends = await db.Deliverables.Where(x => x.Id == e.PredecessorDeliverableId || x.Id == e.SuccessorDeliverableId).ToDictionaryAsync(x => x.Id);
            var (pred, succ) = (ends[e.PredecessorDeliverableId], ends[e.SuccessorDeliverableId]);
            Access.Demand(Permissions.ManageDeliverableDependency(access.Actor, ctx, pred.ProjectDisciplineId, succ.ProjectDisciplineId));
            e.DeletedAt = clock.GetUtcNow();
            e.DeletedBy = me.Id;
            e.AuditKey = $"{pred.Key} → {succ.Key}";
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        // FR-DEP-07: transitive predecessors and successors with their states, up to the chain depth limit.
        api.MapGet("/tasks/{id:guid}/chain", async (Guid id, int? depth, Access access, HubDb db, SettingsStore store) =>
        {
            var (t, _, _) = await TaskEndpoints.Load(db, access, id);
            var limit = Math.Clamp(depth ?? (await store.Get(db)).ChainDepthLimit, 1, 50);
            var edges = await db.Dependencies.Where(d => d.ProjectId == t.ProjectId).Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId }).ToListAsync();
            List<(Guid Id, int Level, Guid Parent)> Walk(bool up)
            {
                var result = new List<(Guid, int, Guid)>();
                var seen = new HashSet<Guid> { t.Id };
                var frontier = new List<Guid> { t.Id };
                for (var level = 1; level <= limit && frontier.Count > 0; level++)
                {
                    var next = new List<Guid>();
                    foreach (var f in frontier)
                        foreach (var n in up ? edges.Where(e => e.SuccessorTaskId == f).Select(e => e.PredecessorTaskId) : edges.Where(e => e.PredecessorTaskId == f).Select(e => e.SuccessorTaskId))
                            if (seen.Add(n)) { result.Add((n, level, f)); next.Add(n); }
                    frontier = next;
                }
                return result;
            }
            var up = Walk(true);
            var down = Walk(false);
            var ids = up.Select(x => x.Id).Concat(down.Select(x => x.Id)).Append(t.Id).ToList();
            var rows = (await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(x => ids.Contains(x.Id)))).ToDictionary(r => (Guid)((dynamic)r).Id);
            return new
            {
                task = rows[t.Id], depth = limit,
                predecessors = up.Select(x => new { level = x.Level, parentId = x.Parent, task = rows[x.Id] }),
                successors = down.Select(x => new { level = x.Level, parentId = x.Parent, task = rows[x.Id] }),
            };
        });

        // FR-002: candidates for a new dependency, with loop-creating tasks listed as disabled.
        api.MapGet("/tasks/{id:guid}/dependency-candidates", async (Guid id, string? q, string? direction, Access access, HubDb db) =>
        {
            var (t, _, _) = await TaskEndpoints.Load(db, access, id);
            var edges = await db.Dependencies.Where(d => d.ProjectId == t.ProjectId).Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId }).ToListAsync();
            var graph = edges.Select(e => (e.PredecessorTaskId, e.SuccessorTaskId)).ToList();
            var query = db.Tasks.AsNoTracking().Where(x => x.ProjectId == t.ProjectId && x.Id != t.Id);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{q.Trim()}%") || EF.Functions.ILike(x.Key, $"%{q.Trim()}%"));
            var list = await query.OrderBy(x => x.Seq).Take(30).Select(x => new { x.Id, x.Key, x.Name, x.Status, x.DueDate }).ToListAsync();
            var asPred = direction != "successor";
            return list.Select(x =>
            {
                var existing = edges.Any(e => asPred ? e.PredecessorTaskId == x.Id && e.SuccessorTaskId == t.Id : e.PredecessorTaskId == t.Id && e.SuccessorTaskId == x.Id);
                var cycle = asPred ? Graph.CycleIfAdded(graph, x.Id, t.Id) : Graph.CycleIfAdded(graph, t.Id, x.Id);
                return new { x.Id, x.Key, x.Name, x.Status, x.DueDate, disabled = existing || cycle is not null, reason = existing ? Text.Get("dep.duplicate") : cycle is not null ? Text.Get("dep.would_cycle") : null };
            });
        });
    }

    static async Task<List<object>> Edges(HubDb db, Guid taskId, bool predecessors) =>
        (await db.Dependencies.AsNoTracking().Where(d => predecessors ? d.SuccessorTaskId == taskId : d.PredecessorTaskId == taskId)
            .Select(d => new
            {
                DependencyId = d.Id, d.Note, d.LagDays,
                Task = db.Tasks.Where(x => x.Id == (predecessors ? d.PredecessorTaskId : d.SuccessorTaskId)).Select(x => new
                {
                    x.Id, x.Key, x.Name, x.Status, x.DueDate, x.StartDate, x.AssigneeId, AssigneeName = db.Users.Where(u => u.Id == x.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                    Overdue = db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsOverdue).FirstOrDefault(),
                    Blocked = db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsBlocked).FirstOrDefault(),
                    Waiting = db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsWaiting).FirstOrDefault(),
                }).First(),
            }).ToListAsync())
        .Select(d => (object)new { d.DependencyId, d.Note, d.LagDays, d.Task, Satisfied = d.Task.Status is TaskStatuses.Complete or TaskStatuses.Cancelled }).ToList();

    /// Adds pred → succ inside the caller's transaction with the project row locked, so concurrent
    /// additions cannot race past the cycle check (§24.5, D-02, D-03, D-16, D-17).
    public static async Task<(TaskDependency Edge, List<string> Warnings)> AddEdge(HubDb db, Access access, Project p, ProjectContext ctx, Guid predId, Guid succId, string? note, int lagDays = 0)
    {
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM hub.project WHERE id = {p.Id} FOR UPDATE");
        var pred = await db.Tasks.FirstOrDefaultAsync(x => x.Id == predId) ?? throw ApiException.Invalid("predecessorTaskId", "error.not_found");
        var succ = await db.Tasks.FirstOrDefaultAsync(x => x.Id == succId) ?? throw ApiException.Invalid("successorTaskId", "error.not_found");
        if (pred.ProjectId != p.Id || succ.ProjectId != p.Id) throw ApiException.Rule("cross_project", "dep.same_project"); // D-02 (cross-project is Phase 3)
        Access.Demand(Permissions.ManageDependency(access.Actor, ctx, await TaskEndpoints.Facts(db, pred), await TaskEndpoints.Facts(db, succ)));
        if (pred.Id == succ.Id) throw ApiException.Conflict("dependency_self", "dep.self");
        if (await db.Dependencies.AnyAsync(d => d.PredecessorTaskId == pred.Id && d.SuccessorTaskId == succ.Id)) throw ApiException.Conflict("dependency_duplicate", "dep.duplicate");
        var edges = await db.Dependencies.Where(d => d.ProjectId == p.Id).Select(d => new { d.PredecessorTaskId, d.SuccessorTaskId }).ToListAsync();
        if (Graph.CycleIfAdded(edges.Select(e => (e.PredecessorTaskId, e.SuccessorTaskId)), pred.Id, succ.Id) is { } loop)
        {
            var keys = await db.Tasks.Where(x => loop.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Key);
            var path = loop.Select(x => keys[x]).ToList();
            throw ApiException.Conflict("dependency_cycle", "dep.cycle", new { cyclePath = path }, string.Join(" → ", path));
        }
        Check.That(lagDays is >= 0 and <= 365, "lagDays", "error.positive");
        var edge = new TaskDependency { ProjectId = p.Id, PredecessorTaskId = pred.Id, SuccessorTaskId = succ.Id, Note = Check.Optional(note, "note", 500), LagDays = lagDays,
            CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = $"{pred.Key} → {succ.Key}" };
        db.Dependencies.Add(edge);
        var warnings = new List<string>();
        if (succ.Status == TaskStatuses.Complete) warnings.Add(Text.Get("dep.successor_complete", succ.Key)); // D-17
        if (pred.Status == TaskStatuses.Complete) warnings.Add(Text.Get("dep.predecessor_complete", pred.Key)); // D-16
        return (edge, warnings);
    }
}
