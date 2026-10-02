using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-RDY-02 task start. Starting work (Not Started onto a path through In Progress) whose readiness is Not Ready or
/// Needs Assessment, including work never assessed, needs the starter's explicit acknowledgement, a reason and a PM or
/// Discipline Lead authorisation. A PM/lead starter authorises inline; anyone else uses an authorisation a PM/lead
/// recorded for the current readiness, once, while its authoriser is still an active PM or lead for the work. Ready and
/// Proceed under Assumption start normally. The guard runs after the review, access and lifecycle guards in
/// TaskEndpoints.ApplyTransition and never replaces them. Recording an authorisation in advance notifies the performer.
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
        var (usable, unusable) = await Find(db, p, t.Id, t.ProjectDisciplineId, r);
        var why = reason?.Trim() ?? "";
        string[] needs = [.. new[] { acknowledged ? null : "acknowledgement", why.Length >= 5 ? null : "reason",
            usable is null && !canAuthorise ? "authorisation" : null }.OfType<string>()];
        if (needs.Length > 0)
            throw ApiException.Rule("start_authorisation_required", "task.start_needs",
                new { readinessState = r.State, r.Assessed, r.Unknown, r.Blocked, r.Note, needs, canAuthorise, authorisationId = usable?.Id,
                    unusableAuthorisationId = needs.Contains("authorisation") ? unusable?.Id : null },
                t.Key, r.State, Detail(r), Needs(needs), needs.Contains("authorisation") && unusable is not null
                    ? Text.Get("task.start_auth_unusable", await Name(db, unusable.AuthorisedBy) ?? "") : "");
        if (usable is null) db.TaskStartAuthorisations.Add(usable = Record(p.Id, t.Id, r, access.Me.Id, why));
        usable.StartedBy = access.Me.Id;
        usable.StartedAt = clock.GetUtcNow();
    }

    /// The newest unused authorisation for exactly the current readiness state and reasons whose authoriser still holds
    /// that authority now; otherwise the newest such record that can no longer be used, so a refusal can say why.
    static async Task<(TaskStartAuthorisation? Usable, TaskStartAuthorisation? Unusable)> Find(HubDb db, Project p, Guid taskId,
        Guid disciplineId, StartReadiness r)
    {
        var rows = (await db.TaskStartAuthorisations.Where(a => a.TaskId == taskId && a.StartedAt == null && a.ReadinessAtAuthorisation == r.State)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).ToListAsync())
            .Where(a => a.Unknown.SequenceEqual(r.Unknown) && a.Blocked.SequenceEqual(r.Blocked)).ToList();
        foreach (var a in rows) if (await StillAuthorises(db, p, a.AuthorisedBy, disciplineId)) return (a, null);
        return (null, rows.FirstOrDefault());
    }

    /// Checked at use, not only when recorded: the authoriser must still be active, able to write here and the PM or the
    /// work's Discipline Lead. Builds their actor and project standing the way Access.Context does for the caller.
    static async Task<bool> StillAuthorises(HubDb db, Project p, Guid userId, Guid disciplineId)
    {
        var active = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => (bool?)u.IsActive).FirstOrDefaultAsync();
        if (active is null) return false;
        var roles = await db.UserRoles.AsNoTracking().Where(r => r.UserId == userId).Select(r => r.Role).ToListAsync();
        var member = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == p.Id && m.UserId == userId && m.RemovedAt == null)
            .Select(m => new { m.Roles, m.PrimaryDisciplineId }).FirstOrDefaultAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == p.Id && d.LeadUserId == userId).Select(d => d.Id).ToListAsync();
        var ctx = new ProjectContext(p.Id, p.Status, p.Visibility, p.ProjectManagerId, p.AllowViewerComments,
            (member?.Roles ?? []).ToHashSet(), leads.ToHashSet(), member?.PrimaryDisciplineId);
        return Permissions.ManageCoordination(new Actor(userId, active.Value, roles.ToHashSet()), ctx, disciplineId).Ok;
    }

    static Task<string?> Name(HubDb db, Guid userId) => db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstOrDefaultAsync();

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
        var (usable, unusable) = notStarted && r.NeedsAuthorisation ? await Find(db, p, t.Id, t.ProjectDisciplineId, r) : (null, null);
        async Task<object?> Show(TaskStartAuthorisation? a) => a is null ? null
            : new { a.Id, a.AuthorisedBy, AuthorisedByName = await Name(db, a.AuthorisedBy), a.Reason, a.CreatedAt };
        return new
        {
            TaskId = t.Id, t.Key, t.Status, ReadinessState = r.State, r.Assessed, r.Unknown, r.Blocked, r.Note,
            NeedsAuthorisation = notStarted && r.NeedsAuthorisation,
            CanAuthorise = notStarted && Permissions.ManageCoordination(access.Actor, ctx, t.ProjectDisciplineId).Ok,
            Authorisation = await Show(usable), UnusableAuthorisation = await Show(unusable),
        };
    }

    static async Task<Coordination.Result> Authorise(Guid id, AuthoriseBody body, Access access, HubDb db, SettingsStore store,
        Notifier notify, TimeProvider clock)
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
                // Queued in the command transaction: a refused, conflicting or replayed command adds no notice (FR-MDC-03, FR-MDC-06);
                // the performer must still hold project access when it is composed and delivered (FR-MDC-02).
                await notify.Send(NotificationEvents.TaskStartAuthorised, t.AssigneeId, TaskEndpoints.Item(p, t),
                    Text.Get("notify.task_start_authorised", await notify.ActorName(), t.Key, t.Name, r.State), reason);
                return row;
            });
    }
}
