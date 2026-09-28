using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class WeeklyCommitmentsEndpoints
{
    public sealed record ProposeBody(Guid RequestId, int TargetRowVersion, DateOnly WeekStart, DateOnly TargetDate,
        string IntendedOutput, string CompletionCriteria);
    public sealed record MoveBody(Guid RequestId, int RowVersion, string ToState, string Reason, string? EvidenceUrl);
    public sealed record SnapshotBody(Guid RequestId, DateOnly WeekStart, string Reason);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/weekly-commitments", List);
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/{targetType}/{targetId:guid}", Propose)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/{id:guid}/transition", Move)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/snapshot", Snapshot)
            .WithMetadata(new Coordination.AtomicCommand());
    }

    static void Week(DateOnly date) => Check.That(date.DayOfWeek == DayOfWeek.Monday, "weekStart", "error.invalid");

    static Task<Coordination.Result> Propose(Guid projectId, string targetType, Guid targetId, ProposeBody body,
        Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.propose", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                Week(body.WeekStart);
                Check.That(body.TargetDate >= body.WeekStart && body.TargetDate < body.WeekStart.AddDays(7),
                    "targetDate", "error.invalid");
                var target = await Coordination.Target(db, project, targetType, targetId);
                if (target.RowVersion != body.TargetRowVersion) throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                var own = Permissions.NamedCoordinationAction(access.Actor, ctx, target.OwnerId);
                if (!own.Ok) Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                Check.That(!await db.WeeklyPlanSnapshots.AnyAsync(s => s.ProjectId == project.Id && s.WeekStart == body.WeekStart),
                    "weekStart", "coord.stale");
                var row = new OutputCommitment { ProjectId = project.Id, TargetType = target.Type,
                    TargetId = target.Id, PerformerId = target.OwnerId, WeekStart = body.WeekStart,
                    TargetDate = body.TargetDate, IntendedOutput = Check.Required(body.IntendedOutput, "intendedOutput", 2000),
                    CompletionCriteria = Check.Required(body.CompletionCriteria, "completionCriteria", 2000) };
                db.OutputCommitments.Add(row);
                db.Audit.Note(row);
                return row;
            });

    static Task<Coordination.Result> Move(Guid projectId, Guid id, MoveBody body, Access access, HubDb db,
        TimeProvider clock, SettingsStore settings) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.transition", id, body },
            access, db, clock, async (project, ctx) =>
            {
                var row = await db.OutputCommitments.SingleOrDefaultAsync(c => c.ProjectId == project.Id && c.Id == id)
                    ?? throw ApiException.NotFound();
                Coordination.Version(row, body.RowVersion);
                Check.OneOf(body.ToState, CommitmentState.All, "toState");
                var reason = Check.Reason(body.Reason);
                var prior = row.State;
                if (body.ToState == CommitmentState.Committed)
                {
                    Check.That(prior == CommitmentState.Proposed && row.SnapshotId is null,
                        "toState", "coord.transition");
                    var target = await Coordination.Target(db, project, row.TargetType, row.TargetId);
                    Check.That(target.OwnerId == row.PerformerId, "performerId", "coord.stale");
                    Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, row.PerformerId));
                    var assessment = await db.ReadinessAssessments.SingleOrDefaultAsync(a => a.ProjectId == project.Id &&
                        a.TargetType == row.TargetType && a.TargetId == row.TargetId);
                    Check.That(assessment is not null && assessment.OwnerId == row.PerformerId,
                        "readiness", "coord.transition");
                    var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.AssessmentId == assessment!.Id).ToListAsync();
                    var evaluated = await ReadinessEndpoints.EvaluateCurrent(db, project, row.TargetType, row.TargetId,
                        assessment!, checks, clock.Today(await settings.Get(db)), clock.GetUtcNow());
                    Check.That(evaluated.State == ReadinessState.Ready, "readiness", "coord.transition");
                    Check.That(!await db.WorkConstraints.AnyAsync(c => c.ProjectId == project.Id &&
                        c.TargetType == row.TargetType && c.TargetId == row.TargetId &&
                        c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled),
                        "constraints", "coord.transition");
                    Check.That(!await db.WeeklyPlanSnapshots.AnyAsync(s => s.ProjectId == project.Id && s.WeekStart == row.WeekStart),
                        "weekStart", "coord.stale");
                    row.ReadinessAtCommit = evaluated.State;
                }
                else if (prior == CommitmentState.Proposed && body.ToState == CommitmentState.Withdrawn)
                {
                    Check.That(row.SnapshotId is null, "toState", "coord.transition");
                    Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, row.PerformerId));
                }
                else
                {
                    Check.That(row.SnapshotId is not null &&
                        WeeklyCommitmentRules.MayClose(prior, body.ToState,
                            !string.IsNullOrWhiteSpace(body.EvidenceUrl), !string.IsNullOrWhiteSpace(reason)),
                        "toState", "coord.transition");
                    var own = Permissions.NamedCoordinationAction(access.Actor, ctx, row.PerformerId);
                    if (!own.Ok)
                    {
                        if (body.ToState == CommitmentState.Met) Access.Demand(own);
                        Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, Guid.Empty));
                    }
                    if (body.ToState == CommitmentState.Met)
                        row.CompletionEvidenceUrl = Coordination.Url(body.EvidenceUrl);
                }
                row.State = body.ToState;
                db.OutputCommitmentEvents.Add(new OutputCommitmentEvent { ProjectId = project.Id,
                    CommitmentId = row.Id, FromState = prior, ToState = row.State, Reason = reason,
                    EvidenceUrl = string.IsNullOrWhiteSpace(body.EvidenceUrl) ? null : Coordination.Url(body.EvidenceUrl),
                    ActorId = access.Me.Id });
                db.Audit.Note(row, reason: reason);
                return row;
            });

    static Task<Coordination.Result> Snapshot(Guid projectId, SnapshotBody body, Access access, HubDb db,
        TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.snapshot", body },
            access, db, clock, async (project, ctx) =>
            {
                Week(body.WeekStart);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, Guid.Empty));
                Check.Reason(body.Reason);
                Check.That(!await db.WeeklyPlanSnapshots.AnyAsync(s => s.ProjectId == project.Id && s.WeekStart == body.WeekStart),
                    "weekStart", "coord.stale");
                var signed = await db.OutputCommitments.Where(c => c.ProjectId == project.Id &&
                    c.WeekStart == body.WeekStart && c.State == CommitmentState.Committed).ToListAsync();
                var snapshot = new WeeklyPlanSnapshot { ProjectId = project.Id, WeekStart = body.WeekStart,
                    CapturedAt = clock.GetUtcNow(), CapturedBy = access.Me.Id, CommittedCount = signed.Count };
                db.WeeklyPlanSnapshots.Add(snapshot);
                foreach (var row in signed) row.SnapshotId = snapshot.Id;
                db.Audit.Note(snapshot, reason: body.Reason);
                return snapshot;
            });

    static async Task<object> List(Guid projectId, DateOnly? weekStart, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        if (weekStart is { } week) Week(week);
        var query = db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId);
        if (weekStart is { } date) query = query.Where(c => c.WeekStart == date);
        var total = await query.CountAsync();
        var rows = await query.OrderBy(c => c.WeekStart).ThenBy(c => c.TargetDate).ThenBy(c => c.CreatedAt)
            .Take(500).ToListAsync();
        var snapshots = await db.WeeklyPlanSnapshots.AsNoTracking().Where(s => s.ProjectId == projectId &&
            (!weekStart.HasValue || s.WeekStart == weekStart.Value)).ToListAsync();
        var outcome = await db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId && c.SnapshotId != null &&
            (!weekStart.HasValue || c.WeekStart == weekStart.Value))
            .GroupBy(c => new { c.SnapshotId, c.State }).Select(g => new { g.Key.SnapshotId, g.Key.State, Count = g.Count() })
            .ToListAsync();
        return new { Commitments = rows, Total = total, Truncated = total > rows.Count,
            Snapshots = snapshots.Select(s => new { s.Id, s.WeekStart, s.CapturedAt, s.CommittedCount,
                Met = outcome.Where(o => o.SnapshotId == s.Id && o.State == CommitmentState.Met).Sum(o => o.Count),
                Withdrawn = outcome.Where(o => o.SnapshotId == s.Id && o.State == CommitmentState.Withdrawn).Sum(o => o.Count) }) };
    }
}
