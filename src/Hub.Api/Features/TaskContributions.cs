using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record TaskContributionRead(Guid CollaboratorId, int RowVersion, DateTimeOffset? MarkedAt, string? Note,
    bool CanMark, bool CanWithdraw, string? RefusalReason);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TaskContributionSetBody([property: JsonRequired] Guid CollaboratorId, [property: JsonRequired, JsonNumberHandling(JsonNumberHandling.Strict)] int RowVersion,
    [property: JsonRequired] bool Marked, string? Note = null, string? Reason = null);

/// A collaborator's own acknowledgement, separate from the task's workflow and optimistic version.
public static class TaskContributions
{
    public static void Map(RouteGroupBuilder api) => api.MapPatch("/tasks/{id:guid}/contribution", Set).RequireFlag("work_screens");

    /// The caller owns the detail snapshot and has already loaded these exact current memberships.
    internal static async Task<IReadOnlyDictionary<Guid, TaskContributionRead>> ReadMany(HubDb db, Access access,
        ProjectContext context, IReadOnlyList<TaskCollaborator> collaborators)
    {
        var ids = collaborators.Select(c => c.Id).ToArray();
        var states = await db.Set<TaskContribution>().AsNoTracking().Where(c => ids.Contains(c.CollaboratorId))
            .ToDictionaryAsync(c => c.CollaboratorId);
        return collaborators.ToDictionary(c => c.Id, c => Read(c, states.GetValueOrDefault(c.Id), access.Actor, context));
    }

    static TaskContributionRead Read(TaskCollaborator collaborator, TaskContribution? state, Actor actor, ProjectContext context)
    {
        var allow = Permissions.MarkTaskContribution(actor, context, collaborator.UserId == actor.Id);
        var marked = state?.MarkedAt is not null;
        return new(collaborator.Id, state?.RowVersion ?? 0, state?.MarkedAt, state?.Note, allow.Ok && !marked, allow.Ok && marked,
            allow.Ok ? null : Text.Get(allow.Why ?? "perm.admin", allow.Arg ?? ""));
    }

    static async Task<Ok<TaskContributionRead>> Set(Guid id, TaskContributionSetBody body, Access access, HubDb db,
        SettingsStore store, TimeProvider clock)
    {
        Http.RequireFlag(await store.Get(db), "work_screens");
        Check.That(body.CollaboratorId != Guid.Empty, "collaboratorId", "error.required");
        Check.That(body.RowVersion >= 0, "rowVersion", "error.invalid");
        Check.That(body.Marked || body.Note is null, "note", "taskContribution.withdrawNote");
        var note = Check.Optional(body.Note, "note", 200);
        var reason = string.IsNullOrWhiteSpace(body.Reason) ? null : body.Reason.Trim();
        return await Tx.Run(db, async () =>
        {
            // Authorise before touching a relation, then load the canonical current task/context after the lock wait.
            var projectId = await db.Tasks.AsNoTracking().Where(t => t.Id == id).Select(t => (Guid?)t.ProjectId).SingleOrDefaultAsync()
                ?? throw ApiException.NotFound();
            await access.Project(projectId, track: false);
            var collaborator = await db.Collaborators.FromSqlInterpolated($"SELECT * FROM hub.task_collaborator WHERE id = {body.CollaboratorId} AND task_id = {id} AND user_id = {access.Me.Id} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync();
            var (task, project, context) = await TaskEndpoints.Load(db, access, id);
            var active = await db.Users.AsNoTracking().Where(u => u.Id == access.Me.Id).Select(u => u.IsActive).SingleAsync();
            var actor = access.Actor with { IsActive = active };
            Access.Demand(Permissions.MarkTaskContribution(actor, context, collaborator is not null));
            ProjectEndpoints.CorrectionReason(project, reason);

            // Locking the membership also serializes two first marks, where there is no contribution row to lock yet.
            var state = await db.Set<TaskContribution>().FromSqlInterpolated($"SELECT * FROM hub.task_contribution WHERE collaborator_id = {body.CollaboratorId} FOR UPDATE")
                .SingleOrDefaultAsync();
            var current = state ?? new TaskContribution { CollaboratorId = collaborator!.Id, TaskId = task.Id, UserId = actor.Id,
                ProjectId = task.ProjectId, TaskKeySnapshot = task.Key, TaskNameSnapshot = task.Name };
            await Http.CheckBodyVersion(db, current, body.RowVersion);
            if (body.Marked && current.MarkedAt is not null)
            {
                Check.That(current.Note == note, "note", "taskContribution.withdrawBeforeNote");
                return TypedResults.Ok(Read(collaborator!, current, actor, context));
            }
            if (!body.Marked && current.MarkedAt is null) return TypedResults.Ok(Read(collaborator!, state, actor, context));

            current.MarkedAt = body.Marked ? clock.GetUtcNow().ToUniversalTime() : null;
            current.Note = body.Marked ? note : null;
            if (state is null) { current.RowVersion = 1; db.Set<TaskContribution>().Add(current); }
            db.Audit.Note(current, action: body.Marked ? "ContributionMarked" : "ContributionWithdrawn", reason: reason, categories: ["contribution"]);
            await db.SaveChangesAsync();
            return TypedResults.Ok(Read(collaborator!, current, actor, context));
        });
    }
}
