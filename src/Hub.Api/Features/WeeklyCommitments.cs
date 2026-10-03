using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class WeeklyCommitmentsEndpoints
{
    public sealed record ProposeBody(Guid RequestId, int TargetRowVersion, DateOnly WeekStart, DateOnly TargetDate,
        string IntendedOutput, string CompletionCriteria, string? Reason = null);
    public sealed record MoveBody(Guid RequestId, int RowVersion, string ToState, string Reason, string? EvidenceUrl);
    public sealed record SnapshotBody(Guid RequestId, DateOnly WeekStart, string Reason);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/weekly-commitments", List);
        api.MapGet("/projects/{projectId:guid}/weekly-commitments/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/weekly-commitments/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/{targetType}/{targetId:guid}", Propose)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/{id:guid}/transition", Move)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/weekly-commitments/snapshot", Snapshot)
            .WithMetadata(new Coordination.AtomicCommand());
    }

    // FR-RDY-05: a promise week starts on the project's coordination day (Monday when unset). Rows keep the week
    // start they were recorded with, so a week recorded before the day changed can still be listed and closed.
    static async Task Week(HubDb db, Project project, DateOnly date, bool allowRecorded = false)
    {
        var day = Weekday.Parse(project.CoordinationDay);
        Check.That(date.DayOfWeek == day || allowRecorded &&
            (await db.OutputCommitments.AnyAsync(c => c.ProjectId == project.Id && c.WeekStart == date) ||
             await db.WeeklyPlanSnapshots.AnyAsync(s => s.ProjectId == project.Id && s.WeekStart == date)),
            "weekStart", "readiness.week_start", day);
    }

    static Task<Coordination.Result> Propose(Guid projectId, string targetType, Guid targetId, ProposeBody body,
        Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.propose", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                await Week(db, project, body.WeekStart);
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
                (row.Seq, row.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "commitment");
                db.OutputCommitments.Add(row);
                db.Audit.Note(row, reason: Check.Optional(body.Reason, "reason"));
                // A chair's proposal stays Proposed until the performer confirms (AC-RDY-05); the performer's own proposal sends nothing.
                await NotifyCommitment(notify, project, target.Key, row, NotificationEvents.CommitmentProposed, [row.PerformerId],
                    Text.Get("notify.commitment_proposed", target.Key));
                return row;
            });

    static Task<Coordination.Result> Move(Guid projectId, Guid id, MoveBody body, Access access, HubDb db,
        TimeProvider clock, SettingsStore settings, Notifier notify) =>
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
                var key = row.TargetType == "Task"
                    ? await db.Tasks.Where(t => t.Id == row.TargetId).Select(t => t.Key).FirstAsync()
                    : await db.Deliverables.Where(d => d.Id == row.TargetId).Select(d => d.Key).FirstAsync();
                await NotifyCommitment(notify, project, key, row, NotificationEvents.CommitmentChanged, [row.PerformerId, row.CreatedBy],
                    Text.Get("notify.commitment_changed", key, row.State));
                return row;
            });

    // Queued inside the command transaction: a refused, conflicting or replayed command adds no notice (FR-MDC-03, FR-MDC-06).
    static Task NotifyCommitment(Notifier notify, Project project, string key, OutputCommitment row, string eventType,
        Guid?[] recipients, string title) =>
        notify.Send(eventType, recipients, new NotifyItem(project.Id, "OutputCommitment", row.Id, key,
            $"/projects/{project.ProjectNumber}/readiness", project.ProjectNumber), title);

    static Task<Coordination.Result> Snapshot(Guid projectId, SnapshotBody body, Access access, HubDb db,
        TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "commitment.snapshot", body },
            access, db, clock, async (project, ctx) =>
            {
                await Week(db, project, body.WeekStart, allowRecorded: true);
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

    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var row = await db.OutputCommitments.AsNoTracking().SingleOrDefaultAsync(c => c.ProjectId == projectId && c.Id == id)
            ?? throw ApiException.NotFound();
        var own = Permissions.NamedCoordinationAction(access.Actor, ctx, row.PerformerId).Ok;
        var manage = Permissions.ManageCoordination(access.Actor, ctx, Guid.Empty).Ok;
        var openWeek = !await db.WeeklyPlanSnapshots.AnyAsync(s => s.ProjectId == projectId && s.WeekStart == row.WeekStart);
        var proposed = row.State == CommitmentState.Proposed && row.SnapshotId is null;
        var closable = row.State == CommitmentState.Committed && row.SnapshotId is not null;
        var events = await db.OutputCommitmentEvents.AsNoTracking().Where(e => e.ProjectId == projectId && e.CommitmentId == id)
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).ToListAsync();
        return new { Commitment = row, Events = events,
            CanCommit = own && proposed && openWeek, CanRecordMet = own && closable,
            CanRecordNotMet = (own || manage) && closable,
            CanWithdraw = own && proposed || (own || manage) && closable };
    }

    static readonly Col[] ExportColumns = [new("key", "key"), new("work", "item"), new("performer", "performer"),
        new("weekStart", "weekStart", "date"), new("targetDate", "promiseTargetDate", "date"), new("intendedOutput", "intendedOutput"),
        new("completionCriteria", "completionCriteria"), new("state", "status"), new("readinessAtCommit", "readinessAtCommit"),
        new("snapshotAt", "snapshotAt", "datetime"), new("completionEvidenceUrl", "completionEvidence")];

    static IQueryable<OutputCommitment> TargetWindow(IQueryable<OutputCommitment> query, DateOnly? from, DateOnly? to) =>
        query.Where(c => (!from.HasValue || c.TargetDate >= from.Value) && (!to.HasValue || c.TargetDate <= to.Value));

    /// FR-MDC-06: the promises the readiness page shows for a window (those due in it), with their recorded week and snapshot.
    static async Task<IResult> ExportRows(Guid projectId, DateOnly from, DateOnly to, string? format, HttpContext http, Access access,
        HubDb db, SettingsStore settings, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false);
        Check.That(to >= from && to.DayNumber - from.DayNumber <= 83, "to", "error.invalid");
        var rows = await TargetWindow(db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId), from, to)
            .OrderBy(c => c.WeekStart).ThenBy(c => c.TargetDate).ThenBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .Take(Export.MaxRows + 1).ToListAsync();
        var performers = rows.Select(r => r.PerformerId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(u => performers.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var work = await ReadinessEndpoints.WorkLabels(db, projectId, rows.Select(r => r.TargetId).ToArray());
        var snapshotIds = rows.Where(r => r.SnapshotId != null).Select(r => r.SnapshotId!.Value).Distinct().ToArray();
        var snapshots = await db.WeeklyPlanSnapshots.AsNoTracking().Where(s => snapshotIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.CapturedAt);
        var data = rows.Select(c => new { c.Key, Work = work.GetValueOrDefault(c.TargetId), Performer = names.GetValueOrDefault(c.PerformerId),
            c.WeekStart, c.TargetDate, c.IntendedOutput, c.CompletionCriteria, c.State, c.ReadinessAtCommit,
            SnapshotAt = c.SnapshotId is { } s ? snapshots.GetValueOrDefault(s) : (DateTimeOffset?)null, c.CompletionEvidenceUrl });
        return await ExportFile.Send(db, settings, format, Text.Get("export.weeklyCommitments", project.ProjectNumber), ExportColumns,
            JsonSerializer.SerializeToNode(data, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access), project.Id,
            $"{project.ProjectNumber}-weekly-commitments", clock);
    }

    /// One recorded week (`weekStart`) or every recorded week start within `from`..`to`, so weeks recorded on an
    /// earlier coordination day stay visible beside the current ones. Target dates filter rows, not snapshot outcomes;
    /// paging is opt-in so existing recorded-week callers retain their response contract.
    static async Task<object> List(Guid projectId, DateOnly? weekStart, DateOnly? from, DateOnly? to,
        DateOnly? targetFrom, DateOnly? targetTo, int? page, int? pageSize, Access access, HubDb db)
    {
        var (project, _) = await access.Project(projectId, false);
        if (weekStart is { } week) await Week(db, project, week, allowRecorded: true);
        Check.That(weekStart is null || from is null && to is null, "weekStart", "error.invalid");
        DateOnly? lo = weekStart ?? from, hi = weekStart ?? to;
        Check.That(lo is null || hi is null || hi >= lo && hi.Value.DayNumber - lo.Value.DayNumber <= 90, "to", "error.invalid");
        Check.That(targetFrom is null || targetTo is null || targetTo >= targetFrom &&
            targetTo.Value.DayNumber - targetFrom.Value.DayNumber <= 83, "targetTo", "error.invalid");
        var query = db.OutputCommitments.AsNoTracking().Where(c => c.ProjectId == projectId &&
            (!lo.HasValue || c.WeekStart >= lo.Value) && (!hi.HasValue || c.WeekStart <= hi.Value));
        var visible = TargetWindow(query, targetFrom, targetTo);
        var total = await visible.CountAsync();
        var paged = page.HasValue || pageSize.HasValue;
        var (pg, size) = Http.Paging(page, pageSize);
        var ordered = visible.OrderBy(c => c.WeekStart).ThenBy(c => c.TargetDate).ThenBy(c => c.CreatedAt).ThenBy(c => c.Id);
        var rows = await (paged ? ordered.Skip((pg - 1) * size).Take(size) : ordered.Take(500)).ToListAsync();
        var snapshots = await db.WeeklyPlanSnapshots.AsNoTracking().Where(s => s.ProjectId == projectId &&
            (!lo.HasValue || s.WeekStart >= lo.Value) && (!hi.HasValue || s.WeekStart <= hi.Value)).ToListAsync();
        var outcome = await query.Where(c => c.SnapshotId != null)
            .GroupBy(c => new { c.SnapshotId, c.State }).Select(g => new { g.Key.SnapshotId, g.Key.State, Count = g.Count() })
            .ToListAsync();
        var summaries = snapshots.Select(s => new { s.Id, s.WeekStart, s.CapturedAt, s.CommittedCount,
            Met = outcome.Where(o => o.SnapshotId == s.Id && o.State == CommitmentState.Met).Sum(o => o.Count),
            Withdrawn = outcome.Where(o => o.SnapshotId == s.Id && o.State == CommitmentState.Withdrawn).Sum(o => o.Count) });
        if (paged) return new { Commitments = rows, Total = total, Page = pg, PageSize = size, Truncated = false, Snapshots = summaries };
        return new { Commitments = rows, Total = total, Truncated = total > rows.Count, Snapshots = summaries };
    }
}
