using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-RDY-02 task start. Starting work (Not Started onto a path through In Progress) whose readiness is Not Ready or
/// Needs Assessment, including work never assessed, needs the starter's explicit acknowledgement, a reason and a PM or
/// Discipline Lead authorisation. A PM/lead starter authorises inline; anyone else uses an authorisation a PM/lead
/// recorded for the current readiness, once. Ready and Proceed under Assumption start normally. The guard runs after
/// the review, access and lifecycle guards in TaskEndpoints.ApplyTransition and never replaces them.
public static class TaskStartEndpoints
{
    public sealed record AuthoriseBody(Guid RequestId, bool Acknowledged, string? Reason);
    public sealed record StartReadiness(string State, bool Assessed, string[] Unknown, string[] Blocked, string? Note)
    {
        public bool NeedsAuthorisation => State is ReadinessState.NotReady or ReadinessState.NeedsAssessment;
    }

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/tasks/{id:guid}/start-readiness", Get);
        api.MapPost("/tasks/{id:guid}/start-authorisations", Authorise).WithMetadata(new Coordination.AtomicCommand());
    }

    public static bool IsStart(string from, string[] path) => from == TaskStatuses.NotStarted && path.Contains(TaskStatuses.InProgress);

    /// Current readiness from the shared evaluator; read-only (the assessment is not tracked, nothing is saved).
    public static async Task<StartReadiness> Current(HubDb db, Project p, Guid taskId, OrgSettings s, SettingsStore? store, TimeProvider clock)
    {
        var assessment = await db.ReadinessAssessments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.ProjectId == p.Id && a.TargetType == "Task" && a.TargetId == taskId);
        if (assessment is null) return new(ReadinessState.NeedsAssessment, false, [], [], null);
        var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == p.Id && c.AssessmentId == assessment.Id).ToListAsync();
        try
        {
            var r = await ReadinessEndpoints.EvaluateCurrent(db, p, "Task", taskId, assessment, checks, clock.Today(s), clock.GetUtcNow(), store);
            return new(r.State, true, [.. r.Unknown.Distinct().Order()], [.. r.Blocked.Distinct().Order()], null);
        }
        // The evaluator refuses work whose owner or discipline is no longer valid here; that is not an established readiness.
        catch (ApiException e) { return new(ReadinessState.NeedsAssessment, true, [], [], e.Errors?.Values.SelectMany(m => m).FirstOrDefault() ?? e.Message); }
    }

    /// For a start that passed every other guard: refuses with what is still needed, or uses/records the authorisation.
    public static async Task Guard(HubDb db, Access access, ProjectContext ctx, Project p, WorkTask t, bool acknowledged,
        string? reason, OrgSettings s, SettingsStore? store, TimeProvider clock)
    {
        var r = await Current(db, p, t.Id, s, store, clock);
        if (!r.NeedsAuthorisation) return;
        var canAuthorise = Permissions.ManageCoordination(access.Actor, ctx, t.ProjectDisciplineId).Ok;
        var usable = await Usable(db, t.Id, r);
        var why = reason?.Trim() ?? "";
        string[] needs = [.. new[] { acknowledged ? null : "acknowledgement", why.Length >= 5 ? null : "reason",
            usable is null && !canAuthorise ? "authorisation" : null }.OfType<string>()];
        if (needs.Length > 0)
            throw ApiException.Rule("start_authorisation_required", "task.start_needs",
                new { readinessState = r.State, r.Assessed, r.Unknown, r.Blocked, r.Note, needs, canAuthorise, authorisationId = usable?.Id },
                t.Key, r.State, Detail(r), Needs(needs));
        if (usable is null) db.TaskStartAuthorisations.Add(usable = Record(p.Id, t.Id, r, access.Me.Id, why));
        usable.StartedBy = access.Me.Id;
        usable.StartedAt = clock.GetUtcNow();
    }

    /// The newest unused authorisation recorded for exactly the current readiness state and reasons.
    static async Task<TaskStartAuthorisation?> Usable(HubDb db, Guid taskId, StartReadiness r) =>
        (await db.TaskStartAuthorisations.Where(a => a.TaskId == taskId && a.StartedAt == null && a.ReadinessAtAuthorisation == r.State)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToListAsync())
        .FirstOrDefault(a => a.Unknown.SequenceEqual(r.Unknown) && a.Blocked.SequenceEqual(r.Blocked));

    static TaskStartAuthorisation Record(Guid projectId, Guid taskId, StartReadiness r, Guid by, string reason)
    {
        Check.That(reason.Length <= 2000, "reason", "error.too_long", 2000);
        return new() { ProjectId = projectId, TaskId = taskId, AuthorisedBy = by, Reason = reason,
            ReadinessAtAuthorisation = r.State, Unknown = r.Unknown, Blocked = r.Blocked };
    }

    static string Detail(StartReadiness r)
    {
        if (r.Note is { } note) return Text.Get("task.start_not_evaluated", note);
        if (!r.Assessed) return Text.Get("task.start_not_assessed");
        string?[] parts = [r.Blocked.Length > 0 ? Text.Get("task.start_blocked", string.Join(", ", r.Blocked)) : null,
            r.Unknown.Length > 0 ? Text.Get("task.start_unknown", string.Join(", ", r.Unknown)) : null];
        var text = string.Join("; ", parts.OfType<string>());
        return text.Length == 0 ? "" : Text.Get("task.start_reasons", text);
    }

    static string Needs(string[] needs)
    {
        var w = needs.Select(n => Text.Get($"task.start_need.{n}")).ToArray();
        return w.Length == 1 ? w[0] : $"{string.Join(", ", w[..^1])} and {w[^1]}";
    }

    static async Task<object> Get(Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var t = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        var (p, ctx) = await access.Project(t.ProjectId, false);
        var r = await Current(db, p, t.Id, await store.Get(db), store, clock);
        var notStarted = t.Status == TaskStatuses.NotStarted;
        var usable = notStarted && r.NeedsAuthorisation ? await Usable(db, t.Id, r) : null;
        var by = usable is null ? null : await db.Users.Where(u => u.Id == usable.AuthorisedBy).Select(u => u.DisplayName).FirstOrDefaultAsync();
        return new
        {
            TaskId = t.Id, t.Key, t.Status, ReadinessState = r.State, r.Assessed, r.Unknown, r.Blocked, r.Note,
            NeedsAuthorisation = notStarted && r.NeedsAuthorisation,
            CanAuthorise = notStarted && Permissions.ManageCoordination(access.Actor, ctx, t.ProjectDisciplineId).Ok,
            Authorisation = usable is null ? null : new { usable.Id, usable.AuthorisedBy, AuthorisedByName = by, usable.Reason, usable.CreatedAt },
        };
    }

    static async Task<Coordination.Result> Authorise(Guid id, AuthoriseBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var projectId = await db.Tasks.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).FirstOrDefaultAsync()
            ?? throw ApiException.NotFound();
        return await Coordination.Run(projectId, body.RequestId, new { operation = "task.start_authorisation", id, body }, access, db, clock,
            async (p, ctx) =>
            {
                var t = await db.Tasks.AsNoTracking().FirstAsync(x => x.Id == id);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, t.ProjectDisciplineId));
                if (t.Status != TaskStatuses.NotStarted) throw ApiException.Rule("start_authorisation_not_started", "task.start_not_started");
                Check.That(body.Acknowledged, "acknowledged", "task.start_ack");
                var reason = Check.Reason(body.Reason);
                var r = await Current(db, p, t.Id, await store.Get(db), store, clock);
                if (!r.NeedsAuthorisation) throw ApiException.Rule("start_authorisation_not_needed", "task.start_not_needed", null, t.Key, r.State);
                var row = Record(p.Id, t.Id, r, access.Me.Id, reason);
                db.TaskStartAuthorisations.Add(row);
                return row;
            });
    }
}
