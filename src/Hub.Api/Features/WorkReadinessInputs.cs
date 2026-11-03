using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record WorkReadinessInput(string Code, bool? CheckApplies, bool? CheckSatisfied,
    RecordContextInputView Source, string Status, string Relationship, bool ContextAvailable,
    RecordContextInputView? LinkedSource, string? LinkedStatus, bool LinkedAvailable);
public sealed record WorkReadinessInputs(bool HasAssessment, bool Available, string State, string[] Unknown,
    string[] Blocked, IReadOnlyList<WorkReadinessInput> Inputs);
public sealed record WorkReadinessTask(Guid TaskId, WorkReadinessInputs Readiness);
public sealed record ReadinessApplicabilityCandidate(RecordContextPerson Person, bool Allowed, string? Refusal);
public sealed record ReadinessApplicabilityActors(string Role, string State, IReadOnlyList<ReadinessApplicabilityCandidate> Candidates);
public sealed record ReadinessDeliveryCheck(string Code, bool? Applies, bool? Satisfied, string? Reason, bool Unknown, bool Blocking);
public sealed record ReadinessDeliveryConstraint(Guid Id, RecordContextPerson RemovalOwner, RecordContextPerson AffectedOwner);
public sealed record ReadinessDeliveryPresentation(bool HasAssessment, bool Available, string State, DateTimeOffset EvaluatedAt,
    string[] Unknown, string[] Blocked, IReadOnlyList<ReadinessDeliveryCheck> Checks, IReadOnlyList<WorkReadinessInput> Inputs,
    ReadinessApplicabilityActors Applicability, IReadOnlyList<ReadinessDeliveryConstraint> ConstraintOwners);

/// Canonical readiness evidence is separate from the evaluator's materialized Task blockers.
/// Detached checks are evaluated by the existing readiness rules; nothing is saved or transitioned.
public static class WorkReadinessInputReader
{
    public static async Task<WorkReadinessInputs?> Read(HubDb db, Project project, Guid taskId,
        OrgSettings settings, SettingsStore store, TimeProvider clock)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        // The owning route has authorised this project. Cancelled/deleted work has no current readiness target.
        if (!await db.Tasks.AsNoTracking().AnyAsync(t => t.ProjectId == project.Id && t.Id == taskId && t.Status != TaskStatuses.Cancelled)) return null;
        var evaluated = await Evaluate(db, project, ItemType.Task, taskId, settings, store, clock.GetUtcNow());
        return await Inputs(db, project, settings, evaluated);
    }

    internal sealed record Evaluation(string TargetType, Guid TargetId, bool HasAssessment, bool Available,
        ReadinessAssessment Assessment, IReadOnlyList<ReadinessCheckRecord> Checks, ReadinessResult Result,
        List<ReadinessEndpoints.CheckSource> Sources, Dictionary<Guid, WorkConstraint> Constraints, DateTimeOffset EvaluatedAt);

    // Seeded detached records allow the window and detail to evaluate once in their authorised snapshot.
    internal static async Task<Evaluation> Evaluate(HubDb db, Project project, string type, Guid id,
        OrgSettings settings, SettingsStore store, DateTimeOffset now, ReadinessAssessment? assessment = null,
        IReadOnlyList<ReadinessCheckRecord>? seededChecks = null, bool loaded = false)
    {
        if (!loaded) assessment = await db.ReadinessAssessments.AsNoTracking().SingleOrDefaultAsync(a =>
            a.ProjectId == project.Id && a.TargetType == type && a.TargetId == id);
        var hasAssessment = assessment is not null;
        var checks = hasAssessment ? seededChecks ?? await db.ReadinessChecks.AsNoTracking().Where(c => c.ProjectId == project.Id && c.AssessmentId == assessment!.Id).ToListAsync()
            : ReadinessCheckCode.All.Select(code => new ReadinessCheckRecord { Code = code }).ToList();
        assessment ??= new ReadinessAssessment { Id = Guid.Empty, ProjectId = project.Id, TargetType = type, TargetId = id };
        if (checks.Count != ReadinessCheckCode.All.Length || checks.Select(c => c.Code).Distinct().Count() != checks.Count
            || checks.Any(c => !ReadinessCheckCode.All.Contains(c.Code)))
        {
            assessment.State = ReadinessState.NeedsAssessment; assessment.EvaluatedAt = now;
            return new(type, id, hasAssessment, false, assessment, checks,
                new(ReadinessState.NeedsAssessment, ReadinessCheckCode.All, []), [], [], now);
        }
        var sources = new List<ReadinessEndpoints.CheckSource>();
        var result = await ReadinessEndpoints.EvaluateCurrent(db, project, type, id, assessment, checks,
            DateOnly.FromDateTime(Clock.Local(now, settings).DateTime), now, store, sources);
        var constraints = await db.WorkConstraints.AsNoTracking().Where(c => c.ProjectId == project.Id && c.TargetType == type
            && c.TargetId == id && c.State != ConstraintState.VerifiedRemoved && c.State != ConstraintState.Cancelled).ToDictionaryAsync(c => c.Id);
        // Other/Scope constraints also block readiness through ConstraintBlocker; they remain exact source records.
        foreach (var constraint in constraints.Values.Where(c => !sources.Any(s => s.Record.Type == RecordContextKind.WorkConstraint && s.Record.Id == c.Id)))
            sources.Add(new(ReadinessRules.ConstraintBlocker, new(RecordContextKind.WorkConstraint, constraint.Id, constraint.Key, constraint.Description, constraint.State)));
        return new(type, id, hasAssessment, true, assessment, checks, result, sources, constraints, now);
    }

    internal static async Task<WorkReadinessInputs> Inputs(HubDb db, Project project, OrgSettings settings, Evaluation evaluation)
    {
        var (_, _, hasAssessment, isAvailable, _, checks, result, sources, constraints, _) = evaluation;
        if (!isAvailable) return new(hasAssessment, false, result.State, result.Unknown, result.Blocked, []);
        var reader = new RecordContextReader(db, project, settings);
        async Task<(RecordContextInputView Input, bool Available)> Fact(ReadinessEndpoints.LinkedRecord record)
        {
            var context = await reader.ReadIfAvailable(record.Type, record.Id);
            return (new(record.Type, record.Id, record.Key, record.Title, context?.NeededBy,
                context?.NextActors ?? [], context?.NextAction ?? "not_assessed", false, context?.ActorState ?? "Unknown"), context is not null && context.Available != false);
        }
        var inputs = new List<WorkReadinessInput>();
        foreach (var source in sources.Distinct().OrderBy(s => s.Code, StringComparer.Ordinal).ThenBy(s => s.Record.Type, StringComparer.Ordinal)
            .ThenBy(s => s.Record.Key, StringComparer.Ordinal).ThenBy(s => s.Record.Id))
        {
            var (fact, available) = await Fact(source.Record);
            var check = checks.SingleOrDefault(c => c.Code == source.Code);
            RecordContextInputView? linkedFact = null;
            string? linkedStatus = null;
            var linkedAvailable = true;
            var relationship = source.Record.Type == ItemType.Handoff ? "DirectTarget" : "Source";
            if (source.Record.Type == RecordContextKind.WorkConstraint && constraints.TryGetValue(source.Record.Id, out var constraint))
            {
                relationship = "Constraint";
                if (constraint.LinkedType is { } kind && constraint.LinkedId is { } linkedId)
                {
                    var linked = kind is ItemType.Decision or ItemType.Issue or ItemType.Handoff
                        ? await ReadinessEndpoints.Linkable(db, project.Id, kind, linkedId).SingleOrDefaultAsync() : null;
                    linkedAvailable = linked is not null;
                    if (linked is not null) { (linkedFact, _) = await Fact(linked); linkedStatus = linked.Status; }
                }
            }
            inputs.Add(new(source.Code, source.Code == ReadinessRules.ConstraintBlocker ? true : check?.Applies,
                source.Code == ReadinessRules.ConstraintBlocker ? false : check?.Satisfied, fact, source.Record.Status,
                relationship, available, linkedFact, linkedStatus, linkedAvailable));
        }
        return new(hasAssessment, true, result.State, result.Unknown, result.Blocked, inputs);
    }

    internal static async Task<ReadinessDeliveryPresentation> Present(HubDb db, Project project, OrgSettings settings,
        Evaluation evaluation, Guid disciplineId, Guid? ownerId)
    {
        var inputs = await Inputs(db, project, settings, evaluation);
        var actors = await ApplicabilityActors(db, project, disciplineId, ownerId);
        var constraints = evaluation.Constraints.Values.ToList();
        var ids = constraints.SelectMany(c => new[] { c.RemovalOwnerId, c.AffectedOwnerId }).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var eligible = (await Coordination.People(db, project).Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync()).ToHashSet();
        var removed = (await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == project.Id && ids.Contains(m.UserId) && m.RemovedAt != null)
            .Select(m => m.UserId).ToListAsync()).ToHashSet();
        RecordContextPerson Person(Guid id, RecordContextPerson? current = null)
        {
            if (!users.TryGetValue(id, out var user)) return new(id, null, "Unknown");
            return (current ?? new(id, user.DisplayName, user.IsActive ? "Active" : "Inactive")) with {
                State = !user.IsActive ? "Inactive" : eligible.Contains(id) ? "Active" : removed.Contains(id) ? "Removed" : "Unavailable"
            };
        }
        var reader = new RecordContextReader(db, project, settings);
        var owners = new List<ReadinessDeliveryConstraint>();
        foreach (var constraint in constraints.OrderBy(c => c.NeededBy).ThenBy(c => c.Id))
        {
            var context = await reader.ReadIfAvailable(RecordContextKind.WorkConstraint, constraint.Id);
            owners.Add(new(constraint.Id, Person(constraint.RemovalOwnerId, context?.Owner), Person(constraint.AffectedOwnerId)));
        }
        return new(evaluation.HasAssessment, evaluation.Available, evaluation.Result.State, evaluation.EvaluatedAt,
            evaluation.Result.Unknown, evaluation.Result.Blocked,
            evaluation.Available ? [.. evaluation.Checks.Select(c => new ReadinessDeliveryCheck(c.Code, c.Applies, c.Satisfied,
                c.Reason, evaluation.Result.Unknown.Contains(c.Code), evaluation.Result.Blocked.Contains(c.Code)))] : [], inputs.Inputs, actors, owners);
    }

    static async Task<ReadinessApplicabilityActors> ApplicabilityActors(HubDb db, Project project, Guid disciplineId, Guid? ownerId)
    {
        // Current project appointments only. Former members and organisation-wide PMs are not inferred candidates.
        var members = await db.ProjectMembers.AsNoTracking().Where(m => m.ProjectId == project.Id && m.RemovedAt == null).ToListAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == project.Id).ToListAsync();
        var discipline = disciplines.SingleOrDefault(d => d.Id == disciplineId);
        var ids = members.Where(m => m.Roles.Contains(ProjectRole.PM)).Select(m => m.UserId).Append(project.ProjectManagerId)
            .Concat(discipline is { IsActive: true, LeadUserId: { } lead } ? [lead] : Array.Empty<Guid>()).Distinct().ToArray();
        var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id);
        var roles = await db.UserRoles.AsNoTracking().Where(r => ids.Contains(r.UserId)).ToListAsync();
        var ownerEligible = ownerId is { } owner && await Coordination.People(db, project).AnyAsync(u => u.Id == owner);
        var candidates = new List<ReadinessApplicabilityCandidate>();
        foreach (var id in ids)
        {
            users.TryGetValue(id, out var user);
            var actor = new Actor(id, user?.IsActive == true, roles.Where(r => r.UserId == id).Select(r => r.Role).ToHashSet());
            var member = members.SingleOrDefault(m => m.UserId == id);
            var context = new ProjectContext(project.Id, project.Status, project.Visibility, project.ProjectManagerId, project.AllowViewerComments,
                (member?.Roles ?? []).ToHashSet(), disciplines.Where(d => d.LeadUserId == id).Select(d => d.Id).ToHashSet(), member?.PrimaryDisciplineId);
            var allowed = Permissions.ManageCoordination(actor, context, disciplineId);
            // Commands additionally validate the target through Coordination.Target before the permission check.
            if (allowed && !ownerEligible) allowed = Allow.No("coord.person");
            if (allowed && discipline?.IsActive != true) allowed = Allow.No("coord.reference");
            candidates.Add(new(new(id, user?.DisplayName, user is null ? "Unknown" : user.IsActive ? "Active" : "Inactive"), allowed.Ok,
                allowed.Ok ? null : Text.Get(allowed.Why ?? "perm.pm_or_dl", allowed.Arg ?? "")));
        }
        var eligible = candidates.Count(c => c.Allowed);
        return new("PMOrDisciplineLead", eligible > 1 ? "Multiple" : eligible == 1 ? "Single"
            : candidates.Count == 0 || candidates.All(c => c.Person.State == "Unknown") ? "Unknown" : "Refused",
            [.. candidates.OrderBy(c => c.Person.Name, StringComparer.Ordinal).ThenBy(c => c.Person.Id)]);
    }
}
