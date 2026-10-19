using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

public static class CommitmentReliabilityEndpoints
{
    public sealed record ConfirmBody(Guid RequestId, int RowVersion, bool Return = false, string? Reason = null);
    public sealed record MissCategoryRow(Guid Id, string Name, bool IsActive);
    public sealed record SnapshotRow(Guid Id, DateOnly WeekStart, DateTimeOffset CapturedAt, CommitmentReliability Reliability);
    public sealed record MissRow(Guid? CategoryId, string? Category, int Count, IEnumerable<Guid> Commitments);
    public sealed record AwaitingRow(Guid Id, string Key, int RowVersion, bool CanConfirm);
    public sealed record ReadResponse(IEnumerable<SnapshotRow> Snapshots, IEnumerable<MissRow> Misses, IEnumerable<AwaitingRow> Awaiting);
    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/commitment-reliability", Read).RequireFlag("commitment_reliability");
        api.MapGet("/commitment-miss-categories", async (HubDb db) => TypedResults.Ok(await db.Set<CommitmentMissCategory>().AsNoTracking()
            .OrderBy(c => c.SortOrder).Select(c => new MissCategoryRow(c.Id, c.Name, c.IsActive)).ToListAsync())).RequireFlag("commitment_reliability");
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/{id:guid}/confirm", Confirm)
            .RequireFlag("commitment_reliability").WithMetadata(new Coordination.AtomicCommand());
    }

    static Task<Coordination.Result> Confirm(Guid projectId, Guid id, ConfirmBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.confirm", id, body }, access, db, clock, async (project, ctx) =>
        {
            var row = await db.OutputCommitments.SingleOrDefaultAsync(c => c.Id == id && c.ProjectId == projectId) ?? throw ApiException.NotFound();
            Coordination.Version(row, body.RowVersion);
            Check.That(row.State == CommitmentState.AwaitingConfirmation, "state", "coord.transition");
            var target = await Coordination.Target(db, project, row.TargetType, row.TargetId);
            Access.Demand(row.PerformerId != access.Me.Id && (Permissions.IsPM(access.Actor, ctx) || Permissions.IsDL(ctx, target.DisciplineId))
                ? Allow.Yes : Allow.No("perm.owner"));
            var reason = body.Return ? Check.Reason(body.Reason) : Check.Optional(body.Reason, "reason");
            row.State = body.Return ? CommitmentState.Committed : CommitmentState.Met;
            db.OutputCommitmentEvents.Add(new OutputCommitmentEvent { ProjectId = projectId, CommitmentId = id,
                FromState = CommitmentState.AwaitingConfirmation, ToState = row.State, Reason = reason ?? "Confirmed",
                ActorId = access.Me.Id, EvidenceUrl = row.CompletionEvidenceUrl });
            db.Audit.Note(row, reason: reason);
            return row;
        });

    public static async Task<Ok<ReadResponse>> Read(Guid projectId, Guid? disciplineId, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        var (project, ctx) = await access.Project(projectId, false);
        var settings = await store.Get(db);
        var snapshots = await db.WeeklyPlanSnapshots.AsNoTracking().Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.WeekStart).Take(settings.CommitmentTrendWeeks).ToListAsync();
        var snapshotIds = snapshots.Select(s => s.Id).ToArray();
        var rows = await db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId && c.SnapshotId != null && snapshotIds.Contains(c.SnapshotId.Value))
            .ToListAsync();
        var taskIds = rows.Where(c => c.TargetType == "Task").Select(c => c.TargetId).ToArray();
        var deliverableIds = rows.Where(c => c.TargetType == "Deliverable").Select(c => c.TargetId).ToArray();
        var disciplines = await db.Tasks.IgnoreQueryFilters().Where(t => taskIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, t => t.ProjectDisciplineId);
        foreach (var item in await db.Deliverables.IgnoreQueryFilters().Where(d => deliverableIds.Contains(d.Id)).Select(d => new { d.Id, d.ProjectDisciplineId }).ToListAsync())
            disciplines[item.Id] = item.ProjectDisciplineId;
        if (disciplineId != null) rows = rows.Where(c => disciplines.GetValueOrDefault(c.TargetId) == disciplineId).ToList();
        var ids = rows.Where(c => c.State == CommitmentState.NotMet).Select(c => c.Id).ToArray();
        var events = await db.OutputCommitmentEvents.AsNoTracking().Where(e => ids.Contains(e.CommitmentId) && e.ToState == CommitmentState.NotMet)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id).ToListAsync();
        var categories = await db.Set<CommitmentMissCategory>().AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);
        var today = clock.Today(settings);
        return TypedResults.Ok(new ReadResponse(
            snapshots.Select(s => new SnapshotRow(s.Id, s.WeekStart, s.CapturedAt,
                CommitmentReliabilityRules.Calculate(disciplineId == null ? s.CommittedCount : rows.Count(c => c.SnapshotId == s.Id),
                    rows.Count(c => c.SnapshotId == s.Id && c.State == CommitmentState.Met), rows.Count(c => c.SnapshotId == s.Id && c.State == CommitmentState.Withdrawn),
                    today >= s.WeekStart.AddDays(7) ? rows.Count(c => c.SnapshotId == s.Id && c.State == CommitmentState.Committed) : 0,
                    rows.Count(c => c.SnapshotId == s.Id && c.State == CommitmentState.AwaitingConfirmation), settings))),
            events.GroupBy(e => e.CommitmentId).Select(g => g.First()).GroupBy(e => e.MissCategoryId)
                .Select(g => new MissRow(g.Key, g.Key is { } key ? categories.GetValueOrDefault(key) : "Not recorded", g.Count(), g.Select(e => e.CommitmentId))),
            rows.Where(c => c.State == CommitmentState.AwaitingConfirmation).Select(c => new AwaitingRow(c.Id, c.Key, c.RowVersion,
                c.PerformerId != access.Me.Id && (Permissions.IsPM(access.Actor, ctx) || Permissions.IsDL(ctx, disciplines.GetValueOrDefault(c.TargetId)))))
        ));
    }
}
