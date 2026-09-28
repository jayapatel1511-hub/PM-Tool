using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class ReadinessEndpoints
{
    public sealed record CreateBody(Guid RequestId, int TargetRowVersion, string IntendedOutput, string CompletionCriteria);
    public sealed record ApplicabilityBody(Guid RequestId, int AssessmentRowVersion, int CheckRowVersion,
        bool Applies, string Reason, string? EvidenceUrl);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}", Create)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/readiness/{targetType}/{targetId:guid}/checks/{code}/applicability", SetApplicability)
            .WithMetadata(new Coordination.AtomicCommand());
    }

    static Task<Coordination.Result> Create(Guid projectId, string targetType, Guid targetId, CreateBody body,
        Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.create", targetType, targetId, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                if (target.RowVersion != body.TargetRowVersion)
                    throw ApiException.Conflict("concurrency_conflict", "coord.stale");
                Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, target.OwnerId));
                if (await db.ReadinessAssessments.AnyAsync(a => a.ProjectId == project.Id &&
                    a.TargetType == target.Type && a.TargetId == target.Id))
                    throw ApiException.Conflict("readiness_exists", "error.duplicate");
                var assessment = new ReadinessAssessment { ProjectId = project.Id, TargetType = target.Type,
                    TargetId = target.Id, OwnerId = target.OwnerId,
                    IntendedOutput = Check.Required(body.IntendedOutput, "intendedOutput", 2000),
                    CompletionCriteria = Check.Required(body.CompletionCriteria, "completionCriteria", 2000),
                    State = ReadinessState.NeedsAssessment, EvaluatedAt = clock.GetUtcNow() };
                db.ReadinessAssessments.Add(assessment);
                foreach (var code in ReadinessCheckCode.All)
                    db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = project.Id,
                        AssessmentId = assessment.Id, Code = code });
                return assessment;
            });

    static Task<Coordination.Result> SetApplicability(Guid projectId, string targetType, Guid targetId, string code,
        ApplicabilityBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "readiness.applicability", targetType, targetId, code, body },
            access, db, clock, async (project, ctx) =>
            {
                var target = await Coordination.Target(db, project, targetType, targetId);
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, target.DisciplineId));
                var assessment = await db.ReadinessAssessments.SingleOrDefaultAsync(a => a.ProjectId == project.Id &&
                    a.TargetType == target.Type && a.TargetId == target.Id) ?? throw ApiException.NotFound();
                Coordination.Version(assessment, body.AssessmentRowVersion);
                Check.OneOf(code, ReadinessCheckCode.All, "code");
                var check = await db.ReadinessChecks.SingleOrDefaultAsync(c => c.ProjectId == project.Id &&
                    c.AssessmentId == assessment.Id && c.Code == code) ?? throw ApiException.NotFound();
                Coordination.Version(check, body.CheckRowVersion);
                check.Applies = body.Applies; check.Satisfied = body.Applies ? check.Satisfied : null;
                check.Reason = Check.Reason(body.Reason);
                check.EvidenceUrl = string.IsNullOrWhiteSpace(body.EvidenceUrl) ? null : Coordination.Url(body.EvidenceUrl);
                check.RecordedBy = access.Me.Id;
                var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.Id).ToListAsync();
                assessment.State = ReadinessRules.Evaluate(checks.Select(c => new ReadinessCheck(c.Code, c.Applies, c.Satisfied)),
                    null, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)).State;
                assessment.EvaluatedAt = clock.GetUtcNow();
                db.Audit.Note(check, reason: check.Reason);
                db.Audit.Note(assessment, reason: check.Reason);
                return check;
            });

    static async Task<object> Detail(Guid projectId, string targetType, Guid targetId, Access access, HubDb db)
    {
        await access.Project(projectId, false);
        Check.OneOf(targetType, ["Task", "Deliverable"], "targetType");
        var assessment = await db.ReadinessAssessments.AsNoTracking().SingleOrDefaultAsync(a =>
            a.ProjectId == projectId && a.TargetType == targetType && a.TargetId == targetId) ?? throw ApiException.NotFound();
        var checks = await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == projectId &&
            c.AssessmentId == assessment.Id).OrderBy(c => c.Code).ToListAsync();
        return new { Assessment = assessment, Checks = checks };
    }
}
