using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Hub.Api.Features;

/// Task and deliverable dependencies (§12.6, D-01..D-18, FR-DEP-01..08) with a link type (§40, FR-DEPT-01).
public static class DependencyEndpoints
{
    public sealed record AddBody(Guid? PredecessorTaskId, Guid? SuccessorTaskId, string? Note, int? LagDays, string? Type = null);
    public sealed record DeliverableBody(Guid? PredecessorDeliverableId, Guid? SuccessorDeliverableId, string? Note, int? LagDays, string? Type = null);
    public sealed record UnavailableDate(DateOnly Date, string Message, DateOnly? NextAvailableDate);
    public sealed record DateContextRead(DateContext Dates,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UnavailableDate[]? Availability = null,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateSuccessor[]? Tasks = null);

    /// The task at the other end of a link, with the facts the dependency list shows.
    public sealed record DependencyTaskRef(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, DateOnly? StartDate, Guid? AssigneeId, string? AssigneeName,
        bool Overdue, bool Blocked, bool Waiting);
    public sealed record TaskDependencyEdge(Guid DependencyId, string? Note, int LagDays, string Type, DependencyTaskRef Task, bool Satisfied);
    public sealed record TaskDependencyList(IReadOnlyList<TaskDependencyEdge> DependsOn, IReadOnlyList<TaskDependencyEdge> Blocks);
    public sealed record DependencyAdded(Guid Id, IReadOnlyList<string> Warnings);
    public sealed record DeliverableDependencyAdded(Guid Id);
    /// One task reached from `ParentId` at `Level` steps from the task asked about.
    public sealed record ChainStep(int Level, Guid ParentId, TaskQueries.Row Task);
    public sealed record TaskChain(TaskQueries.Row Task, int Depth, IReadOnlyList<ChainStep> Predecessors, IReadOnlyList<ChainStep> Successors);
    public sealed record DependencyCandidate(Guid Id, string Key, string Name, string Status, DateOnly? DueDate, bool Disabled, string? Reason);

    /// Query projections: the two ends of a link, and one link with the facts the list judges it by.
    sealed record LinkEnds(Guid Predecessor, Guid Successor);
    sealed record PredecessorState(string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);
    sealed record EdgeRead(Guid DependencyId, string? Note, int LagDays, string Type, PredecessorState Pred, DependencyTaskRef Task);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/items/{type}/{id:guid}/date-context", Dates);
        api.MapGet("/tasks/{id:guid}/dependencies", async Task<Ok<TaskDependencyList>> (Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        {
            var (t, p, _) = await TaskEndpoints.Load(db, access, id);
            var judge = await Judge(db, p, store, clock);
            return TypedResults.Ok(new TaskDependencyList(await Edges(db, judge, t.Id, predecessors: true), await Edges(db, judge, t.Id, predecessors: false)));
        });

        api.MapPost("/tasks/{id:guid}/dependencies", async Task<Created<DependencyAdded>> (Guid id, AddBody body, Access access, HubDb db) =>
        {
            var (t, p, ctx) = await TaskEndpoints.Load(db, access, id);
            var (pred, succ) = body.PredecessorTaskId is { } pr ? (pr, t.Id) : body.SuccessorTaskId is { } su ? (t.Id, su) : throw ApiException.Invalid("predecessorTaskId", "error.required");
            var (edge, warnings) = await Tx.Run(db, async () =>
            {
                var r = await AddEdge(db, access, p, ctx, pred, succ, body.Note, body.LagDays ?? 0, body.Type);
                await db.SaveChangesAsync();
                return r;
            });
            return TypedResults.Created($"/api/v1/dependencies/{edge.Id}", new DependencyAdded(edge.Id, warnings));
        });

        api.MapDelete("/dependencies/{id:guid}", async Task<NoContent> (Guid id, Access access, HubDb db, Notifier notify, TimeProvider clock, CurrentUser me, SettingsStore store) =>
            await Tx.Notice<NoContent>(db, store, async () =>
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
            return TypedResults.NoContent();
        }));

        // FR-DEP-09 (packet 021): explicit links between deliverables, typed (FR-DEPT-01), with an optional lag; loops refused (D-03).
        api.MapPost("/deliverables/{id:guid}/dependencies", async Task<Created<DeliverableDependencyAdded>> (Guid id, DeliverableBody body, Access access, HubDb db) =>
        {
            var d = await db.Deliverables.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            var (p, ctx) = await access.Project(d.ProjectId);
            Check.That(body.PredecessorDeliverableId is null != body.SuccessorDeliverableId is null, "predecessorDeliverableId", "dep.one_end");
            var (predId, succId) = body.PredecessorDeliverableId is { } pid ? (pid, id) : (id, body.SuccessorDeliverableId!.Value);
            var type = LinkType(body.Type);
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
                var edges = await db.DeliverableDependencies.Where(x => x.ProjectId == p.Id).Select(x => new LinkEnds(x.PredecessorDeliverableId, x.SuccessorDeliverableId)).ToListAsync();
                if (Graph.CycleIfAdded(edges.Select(e => (e.Predecessor, e.Successor)), pred.Id, succ.Id) is { } loop)
                {
                    var keys = await db.Deliverables.Where(x => loop.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Key);
                    var path = loop.Select(x => keys[x]).ToList();
                    throw ApiException.Conflict("dependency_cycle", "dep.cycle", new { cyclePath = path }, string.Join(" → ", path));
                }
                var lag = body.LagDays ?? 0;
                Check.That(lag is >= 0 and <= 365, "lagDays", "dep.lag_rule");
                var edge = new DeliverableDependency { ProjectId = p.Id, PredecessorDeliverableId = pred.Id, SuccessorDeliverableId = succ.Id, DependencyType = type, LagDays = lag,
                    Note = Check.Optional(body.Note, "note", 500), CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = $"{pred.Key} → {succ.Key}" };
                db.DeliverableDependencies.Add(edge);
                await db.SaveChangesAsync();
                return edge;
            });
            return TypedResults.Created($"/api/v1/deliverable-dependencies/{edge.Id}", new DeliverableDependencyAdded(edge.Id));
        });

        api.MapDelete("/deliverable-dependencies/{id:guid}", async Task<NoContent> (Guid id, Access access, HubDb db, TimeProvider clock, CurrentUser me) =>
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
            return TypedResults.NoContent();
        });

        // FR-DEP-07: transitive predecessors and successors with their states, up to the chain depth limit.
        api.MapGet("/tasks/{id:guid}/chain", async Task<Ok<TaskChain>> (Guid id, int? depth, Access access, HubDb db, SettingsStore store) =>
        {
            var (t, _, _) = await TaskEndpoints.Load(db, access, id);
            var limit = Math.Clamp(depth ?? (await store.Get(db)).ChainDepthLimit, 1, 50);
            var edges = await db.Dependencies.Where(d => d.ProjectId == t.ProjectId).Select(d => new LinkEnds(d.PredecessorTaskId, d.SuccessorTaskId)).ToListAsync();
            List<(Guid Id, int Level, Guid Parent)> Walk(bool up)
            {
                var result = new List<(Guid, int, Guid)>();
                var seen = new HashSet<Guid> { t.Id };
                var frontier = new List<Guid> { t.Id };
                for (var level = 1; level <= limit && frontier.Count > 0; level++)
                {
                    var next = new List<Guid>();
                    foreach (var f in frontier)
                        foreach (var n in up ? edges.Where(e => e.Successor == f).Select(e => e.Predecessor) : edges.Where(e => e.Predecessor == f).Select(e => e.Successor))
                            if (seen.Add(n)) { result.Add((n, level, f)); next.Add(n); }
                    frontier = next;
                }
                return result;
            }
            var up = Walk(true);
            var down = Walk(false);
            var ids = up.Select(x => x.Id).Concat(down.Select(x => x.Id)).Append(t.Id).ToList();
            var rows = (await TaskQueries.Rows(db, db.Tasks.AsNoTracking().Where(x => ids.Contains(x.Id)), access.Actor)).ToDictionary(r => r.Id);
            return TypedResults.Ok(new TaskChain(rows[t.Id], limit,
                [.. up.Select(x => new ChainStep(x.Level, x.Parent, rows[x.Id]))],
                [.. down.Select(x => new ChainStep(x.Level, x.Parent, rows[x.Id]))]));
        });

        // FR-002: candidates for a new dependency, with loop-creating tasks listed as disabled.
        api.MapGet("/tasks/{id:guid}/dependency-candidates", async Task<Ok<List<DependencyCandidate>>> (Guid id, string? q, string? direction, Access access, HubDb db) =>
        {
            var (t, _, _) = await TaskEndpoints.Load(db, access, id);
            var edges = await db.Dependencies.Where(d => d.ProjectId == t.ProjectId).Select(d => new LinkEnds(d.PredecessorTaskId, d.SuccessorTaskId)).ToListAsync();
            var graph = edges.Select(e => (e.Predecessor, e.Successor)).ToList();
            var query = db.Tasks.AsNoTracking().Where(x => x.ProjectId == t.ProjectId && x.Id != t.Id);
            if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => EF.Functions.ILike(x.Name, $"%{q.Trim()}%") || EF.Functions.ILike(x.Key, $"%{q.Trim()}%"));
            var list = await query.OrderBy(x => x.Seq).Take(30).Select(x => new DependencyCandidate(x.Id, x.Key, x.Name, x.Status, x.DueDate, false, null)).ToListAsync();
            var asPred = direction != "successor";
            return TypedResults.Ok(list.Select(x =>
            {
                var existing = edges.Any(e => asPred ? e.Predecessor == x.Id && e.Successor == t.Id : e.Predecessor == t.Id && e.Successor == x.Id);
                var cycle = asPred ? Graph.CycleIfAdded(graph, x.Id, t.Id) : Graph.CycleIfAdded(graph, t.Id, x.Id);
                return x with { Disabled = existing || cycle is not null, Reason = existing ? Text.Get("dep.duplicate") : cycle is not null ? Text.Get("dep.would_cycle") : null };
            }).ToList());
        });
    }

    static async Task<Ok<DateContextRead>> Dates(string type, Guid id, string? startDate, string? dueDate,
        Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var settings = await store.Get(db);
        Http.RequireFlag(settings, "date_context");
        return TypedResults.Ok(await ReadDateContext(type, id, startDate, dueDate, access, db, settings, clock));
    }

    // Pure canonical projection shared by the independently flagged Calendar screen. The public route retains its own guard.
    internal static async Task<DateContextRead> ReadDateContext(string type, Guid id, string? startDate, string? dueDate,
        Access access, HubDb db, OrgSettings settings, TimeProvider clock)
    {
        Project project; Guid? person; DateLimit? parent = null; List<DateItem> items; List<DependencySnap> links;
        var task = type == ItemType.Task;
        if (task)
        {
            var (item, p, _) = await TaskEndpoints.Load(db, access, id); project = p; person = item.AssigneeId;
            if (item.DeliverableId is { } did)
                parent = await db.Deliverables.AsNoTracking().Where(d => d.Id == did && d.DueDate != null).Select(d => new DateLimit(d.Key, d.DueDate!.Value)).FirstOrDefaultAsync();
            items = await db.Tasks.AsNoTracking().Where(t => t.ProjectId == project.Id)
                .Select(t => new DateItem(t.Id, t.Key, t.StartDate, t.DueDate, t.Status == TaskStatuses.Complete || t.Status == TaskStatuses.Cancelled)).ToListAsync();
            links = await db.Dependencies.AsNoTracking().Where(e => e.ProjectId == project.Id)
                .Select(e => new DependencySnap(e.Id, e.PredecessorTaskId, e.SuccessorTaskId, e.LagDays, e.DependencyType)).ToListAsync();
        }
        else if (type == ItemType.Deliverable)
        {
            var item = await db.Deliverables.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id) ?? throw ApiException.NotFound();
            (project, _) = await access.Project(item.ProjectId); person = item.OwnerId;
            if (item.MilestoneId is { } mid)
                parent = await db.Milestones.AsNoTracking().Where(m => m.Id == mid && !m.IsCancelled && m.Date != null).Select(m => new DateLimit(m.Key, m.Date!.Value)).FirstOrDefaultAsync();
            items = await db.Deliverables.AsNoTracking().Where(d => d.ProjectId == project.Id)
                .Select(d => new DateItem(d.Id, d.Key, d.StartDate, d.DueDate, d.Status == DeliverableStatus.Issued || d.Status == DeliverableStatus.Accepted || d.Status == DeliverableStatus.Cancelled)).ToListAsync();
            links = await db.DeliverableDependencies.AsNoTracking().Where(e => e.ProjectId == project.Id)
                .Select(e => new DependencySnap(e.Id, e.PredecessorDeliverableId, e.SuccessorDeliverableId, e.LagDays, e.DependencyType)).ToListAsync();
        }
        else throw ApiException.NotFound();
        var original = items.SingleOrDefault(item => item.Id == id) ?? throw ApiException.NotFound();
        DateOnly? Proposed(string? value, DateOnly? current, string field) => value is null ? current : value.Length == 0 ? null
            : DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
                ? date : throw ApiException.Invalid(field, "error.date");
        var proposed = original with { StartDate = Proposed(startDate, original.StartDate, "startDate"), DueDate = Proposed(dueDate, original.DueDate, "dueDate") };
        var judge = await Judge(db, project, settings, clock.Today(settings));
        var dates = DependencyRules.Context(proposed, items, links, parent, task, project.Status == ProjectStatus.Active, judge.After);
        DateSuccessor[]? children = null;
        if (!task)
        {
            var rows = project.Status != ProjectStatus.Active ? [] : await db.Tasks.AsNoTracking()
                .Where(t => t.DeliverableId == id && t.ProjectId == project.Id && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.DueDate != null)
                .OrderBy(t => t.Seq).Select(t => new DateItem(t.Id, t.Key, null, t.DueDate, false)).ToListAsync();
            children = rows.Select(t => (Task: t, Reason: DependencyRules.ParentDates(proposed.Key, proposed.DueDate, t.DueDate, true)))
                .Where(t => t.Reason is not null).Select(t => new DateSuccessor(t.Task.Id, t.Task.Key, t.Reason!)).ToArray();
        }
        UnavailableDate[]? availability = null;
        if (person is { } uid && await PlanningEndpoints.People(db, access).AnyAsync(u => u.Id == uid))
        {
            var from = Workload.WeekOf(judge.Today); var through = from.AddDays(settings.PlanningHorizonWeeks * 7 - 1);
            var days = await PersonCapacity.Load(db, [uid], from, through, settings);
            var chosen = new[] { proposed.StartDate, proposed.DueDate, dates.EarliestStart?.Date, dates.EarliestDue?.Date }.OfType<DateOnly>().Distinct().Order().ToArray();
            var warnings = new List<UnavailableDate>();
            foreach (var date in chosen)
            {
                if (!days.TryGetValue((uid, date), out var day))
                    day = (await PersonCapacity.Load(db, [uid], date, date, settings))[(uid, date)];
                if (day.Available > 0) continue;
                var next = days.Where(d => d.Key.Item2 > date && d.Value.Available > 0).OrderBy(d => d.Key.Item2).Select(d => (DateOnly?)d.Key.Item2).FirstOrDefault();
                warnings.Add(new(date, Text.Get("date_context.unavailable"), next));
            }
            availability = warnings.ToArray();
        }
        return new DateContextRead(dates, availability, children);
    }

    static async Task<List<TaskDependencyEdge>> Edges(HubDb db, LinkJudge judge, Guid taskId, bool predecessors) =>
        (await db.Dependencies.AsNoTracking().Where(d => predecessors ? d.SuccessorTaskId == taskId : d.PredecessorTaskId == taskId)
            .Select(d => new EdgeRead(d.Id, d.Note, d.LagDays, d.DependencyType,
                db.Tasks.Where(x => x.Id == d.PredecessorTaskId).Select(x => new PredecessorState(x.Status, x.StartedAt, x.CompletedAt)).First(),
                db.Tasks.Where(x => x.Id == (predecessors ? d.PredecessorTaskId : d.SuccessorTaskId)).Select(x => new DependencyTaskRef(
                    x.Id, x.Key, x.Name, x.Status, x.DueDate, x.StartDate, x.AssigneeId, db.Users.Where(u => u.Id == x.AssigneeId).Select(u => u.DisplayName).FirstOrDefault(),
                    db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsOverdue).FirstOrDefault(),
                    db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsBlocked).FirstOrDefault(),
                    db.TaskStates.Where(s => s.TaskId == x.Id).Select(s => s.IsWaiting).FirstOrDefault())).First())).ToListAsync())
        .Select(d => new TaskDependencyEdge(d.DependencyId, d.Note, d.LagDays, d.Type, d.Task,
            judge.Satisfied(d.Type, d.Pred.Status, d.Pred.StartedAt, judge.Local(d.Pred.CompletedAt), d.LagDays))).ToList();

    /// FR-DEPT-01: an omitted type is Finish-to-Start; anything outside the vocabulary is refused.
    public static string LinkType(string? type)
    {
        var t = type ?? DependencyTypes.FinishToStart;
        Check.That(DependencyTypes.All.Contains(t), "type", "dep.type_rule");
        return t;
    }

    /// AC-DEPT-05: API reads judge a link with the evaluator's own rule (DependencyRules), the project's working-day
    /// calendar and the organisation's today, so a satisfied tick never disagrees with Waiting or Blocked.
    public sealed record LinkJudge(OrgSettings S, Func<DateOnly, int, DateOnly> After, DateOnly Today)
    {
        public DateOnly? Local(DateTimeOffset? t) => t is { } v ? Clock.LocalDate(v, S) : null;
        public bool Satisfied(string type, string predStatus, DateTimeOffset? predStartedAt, DateOnly? predFinishedOn, int lag) =>
            DependencyRules.Satisfied(type, predStatus, Local(predStartedAt), predFinishedOn, lag, After, Today);
    }

    /// The calendar matches EvaluationService: organisation-wide and the project office's holidays, when working days are on.
    public static async Task<LinkJudge> Judge(HubDb db, Project p, OrgSettings s, DateOnly today)
    {
        WorkCalendar? calendar = null;
        if (s.WorkingDaysEnabled)
            calendar = new WorkCalendar(await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == p.OfficeId).Select(h => h.Date).ToListAsync());
        return new(s, DependencyRules.After(calendar), today);
    }

    public static async Task<LinkJudge> Judge(HubDb db, Project p, SettingsStore store, TimeProvider clock)
    {
        var s = await store.Get(db);
        return await Judge(db, p, s, clock.Today(s));
    }

    /// Adds pred → succ inside the caller's transaction with the project row locked, so concurrent
    /// additions cannot race past the cycle check (§24.5, D-02, D-03, D-16, D-17).
    public static async Task<(TaskDependency Edge, List<string> Warnings)> AddEdge(HubDb db, Access access, Project p, ProjectContext ctx, Guid predId, Guid succId, string? note, int lagDays = 0,
        string? type = null)
    {
        var kind = LinkType(type);
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM hub.project WHERE id = {p.Id} FOR UPDATE");
        var pred = await db.Tasks.FirstOrDefaultAsync(x => x.Id == predId) ?? throw ApiException.Invalid("predecessorTaskId", "error.not_found");
        var succ = await db.Tasks.FirstOrDefaultAsync(x => x.Id == succId) ?? throw ApiException.Invalid("successorTaskId", "error.not_found");
        if (pred.ProjectId != p.Id || succ.ProjectId != p.Id) throw ApiException.Rule("cross_project", "dep.same_project"); // D-02 (cross-project is Phase 3)
        Access.Demand(Permissions.ManageDependency(access.Actor, ctx, await TaskEndpoints.Facts(db, pred), await TaskEndpoints.Facts(db, succ)));
        if (pred.Id == succ.Id) throw ApiException.Conflict("dependency_self", "dep.self");
        if (await db.Dependencies.AnyAsync(d => d.PredecessorTaskId == pred.Id && d.SuccessorTaskId == succ.Id)) throw ApiException.Conflict("dependency_duplicate", "dep.duplicate");
        var edges = await db.Dependencies.Where(d => d.ProjectId == p.Id).Select(d => new LinkEnds(d.PredecessorTaskId, d.SuccessorTaskId)).ToListAsync();
        if (Graph.CycleIfAdded(edges.Select(e => (e.Predecessor, e.Successor)), pred.Id, succ.Id) is { } loop)
        {
            var keys = await db.Tasks.Where(x => loop.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Key);
            var path = loop.Select(x => keys[x]).ToList();
            throw ApiException.Conflict("dependency_cycle", "dep.cycle", new { cyclePath = path }, string.Join(" → ", path));
        }
        Check.That(lagDays is >= 0 and <= 365, "lagDays", "error.positive");
        var edge = new TaskDependency { ProjectId = p.Id, PredecessorTaskId = pred.Id, SuccessorTaskId = succ.Id, DependencyType = kind, Note = Check.Optional(note, "note", 500), LagDays = lagDays,
            CreatedBy = access.Me.Id, CreatedAt = DateTimeOffset.UtcNow, AuditKey = $"{pred.Key} → {succ.Key}" };
        db.Dependencies.Add(edge);
        var warnings = new List<string>();
        if (succ.Status == TaskStatuses.Complete) warnings.Add(Text.Get("dep.successor_complete", succ.Key)); // D-17
        if (pred.Status == TaskStatuses.Complete) warnings.Add(Text.Get("dep.predecessor_complete", pred.Key)); // D-16
        return (edge, warnings);
    }
}
