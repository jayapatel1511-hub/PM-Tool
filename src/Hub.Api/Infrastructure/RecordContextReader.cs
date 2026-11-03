using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using System.Text.Json.Serialization;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Infrastructure;

public sealed record RecordContextPerson(Guid? Id, string? Name, string State, string Kind = "User");
public sealed record RecordContextInputView(string Kind, Guid Id, string Key, string Title, DateOnly? NeededBy,
    IReadOnlyList<RecordContextPerson> NextActors, string NextAction, bool Primary,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ActorState = null);
public sealed record RecordContextReviewPackage(Guid? Id, string? Key, string? Title, string? Status, bool? Satisfied, string? Reason);
public sealed record RecordContextView(RecordContextPerson Owner, IReadOnlyList<RecordContextPerson> NextActors,
    DateOnly? NeededBy, string NextAction, bool Terminal, string ActorState, bool RequiresReviewGate,
    IReadOnlyList<string> NextTransitions, IReadOnlyList<RecordContextInputView> Inputs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecordContextReviewPackage? ReviewPackageGate = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? UnavailableInputCount = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? Available = null);

/// Hydrates only explicit, same-project records after the owning reader has authorised the project.
/// All state/actor selection remains in RecordContextRules; this class never grants a command.
public sealed class RecordContextReader(HubDb db, Project project, OrgSettings settings)
{
    sealed record Loaded(RecordContextFacts Facts, string Key, string Title, RecordContextReviewPackage? ReviewPackageGate = null);
    readonly Dictionary<(string Kind, Guid Id), Loaded> records = [];
    readonly Dictionary<string, RecordContextPerson> people = new(StringComparer.Ordinal);
    readonly Dictionary<(string Kind, Guid Id), Entity?> notificationPrimaries = [];
    readonly Dictionary<Guid, TaskState?> notificationTaskStates = [];
    readonly Dictionary<Guid, MilestoneState?> notificationMilestoneStates = [];

    sealed record ChangeWork(Guid Id, string Key, string Title, Guid? OwnerId, Guid DisciplineId, string Status);
    sealed record ChangeSnapshot(ChangeNotice Notice, IReadOnlyList<ChangeAssessment> Assessments, HashSet<Guid> Complete,
        SourceRevision? Revision, SourceHead? Head, Dictionary<(string, Guid), ChangeWork> Work,
        Dictionary<Guid, WorkTask> Corrections, Dictionary<Guid, InputUse> Uses, HashSet<Guid> EligiblePeople, HashSet<Guid> ActiveDisciplines);
    readonly Dictionary<Guid, ChangeSnapshot> changes = [];
    readonly Dictionary<Guid, (ChangeNotice Notice, IReadOnlyList<ChangeAssessment> Assessments, IReadOnlyCollection<Guid> Complete, SourceRevision Revision, SourceHead? Head)> changeSeeds = [];

    public void SeedChange(ChangeNotice notice, IReadOnlyList<ChangeAssessment> assessments, IReadOnlyCollection<Guid> complete, SourceRevision revision, SourceHead? head)
    {
        if (notice.ProjectId != project.Id) throw ApiException.NotFound();
        changeSeeds[notice.Id] = (notice, assessments, complete, revision, head);
    }

    public (string Key, string Title) Identity(string kind, Guid id) => records.TryGetValue((kind, id), out var row)
        ? (row.Key, row.Title) : throw ApiException.NotFound();

    readonly HashSet<string> pendingPeople = new(StringComparer.Ordinal);
    readonly HashSet<string> resolvedPeople = new(StringComparer.Ordinal);
    ReviewFindingContextData? reviewFindings;
    public void SeedReview(ReviewPackage package, IReadOnlyList<ReviewRound> rounds, IReadOnlyList<DisciplineReview> assignments,
        IReadOnlyList<ReviewFinding> findings, IReadOnlyList<ReviewManifestItem> manifest, IReadOnlyList<FindingEvent> events)
    {
        if (!settings.IsFlagOn("screen_patterns")) return;
        (reviewFindings ??= new(db, project, settings)).SeedPackage(package, rounds, assignments, findings, manifest, events);
        pendingPeople.Add(package.CoordinatorId.ToString());
        foreach (var f in findings.Where(x => x.ProjectId == project.Id && x.PackageId == package.Id))
            foreach (var id in new[] { f.ResolverId, f.VerifierId, f.OriginatorId }.OfType<Guid>()) pendingPeople.Add(id.ToString());
    }

    SubmissionContextData? submissions;
    readonly Dictionary<(string Kind, Guid Id), Loaded?> coordinationTargets = [];
    public void SeedSubmission(SubmissionPackage package, IReadOnlyList<SubmissionCheck> checks, SubmissionReadinessResult? readiness, string state)
    {
        if (!settings.IsFlagOn("screen_patterns")) return;
        (submissions ??= new(db, project)).SeedPackage(package, checks, readiness, state);
        pendingPeople.Add(package.CoordinatorId.ToString()); pendingPeople.Add(project.ProjectManagerId.ToString());
        foreach (var check in checks.Where(x => x.ProjectId == project.Id && x.PackageId == package.Id)) pendingPeople.Add(check.OwnerId.ToString());
    }

    public string? SubmissionCheckStatus(Guid id) => records.TryGetValue((RecordContextKind.SubmissionCheck, id), out var record)
        && record.Facts.State != "Unknown" ? record.Facts.State : null;

    public async Task<RecordContextView?> ReadLoaded(string kind, Guid id)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        return records.TryGetValue((kind, id), out var record) ? await View(record) : await Read(kind, id);
    }

    public async Task<RecordContextView?> ReadIfAvailable(string kind, Guid id)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        var loaded = records.TryGetValue((kind, id), out var record) ? record : await Load(kind, id, []);
        return loaded is null ? null : await View(loaded);
    }

    /// Internal fact consumer for an already authorized notification project. Public context presentation
    /// remains screen_patterns-owned; this uses the same loaders/rules without enabling that flag.
    internal async Task<RecordContextView?> ReadForNotification(string kind, Guid id)
    {
        Http.RequireFlag(settings, "people_screens");
        var loaded = records.TryGetValue((kind, id), out var record) ? record : await Load(kind, id, []);
        return loaded is null ? null : await View(loaded);
    }

    /// Request/project-local primary seeding only; facts and action decisions still use the same Load/View/rules.
    internal async Task PreloadForNotifications(IReadOnlyCollection<(string Kind, Guid Id)> targets)
    {
        Http.RequireFlag(settings, "people_screens");
        var keys = targets.Distinct().ToArray();
        foreach (var group in keys.GroupBy(key => key.Kind))
        {
            var ids = group.Select(key => key.Id).ToArray();
            switch (group.Key)
            {
                case RecordContextKind.Task:
                    await SeedNotificationPrimaries(group.Key, ids, db.Tasks.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id)));
                    var taskStates = await db.TaskStates.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.TaskId)).ToDictionaryAsync(row => row.TaskId);
                    foreach (var id in ids) notificationTaskStates[id] = taskStates.GetValueOrDefault(id);
                    break;
                case RecordContextKind.Deliverable:
                    await SeedNotificationPrimaries(group.Key, ids, db.Deliverables.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.Milestone:
                    await SeedNotificationPrimaries(group.Key, ids, db.Milestones.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id)));
                    var milestoneStates = await db.MilestoneStates.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.MilestoneId)).ToDictionaryAsync(row => row.MilestoneId);
                    foreach (var id in ids) notificationMilestoneStates[id] = milestoneStates.GetValueOrDefault(id);
                    break;
                case RecordContextKind.Decision:
                    await SeedNotificationPrimaries(group.Key, ids, db.Decisions.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.Risk:
                    await SeedNotificationPrimaries(group.Key, ids, db.Risks.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.Issue:
                    await SeedNotificationPrimaries(group.Key, ids, db.Issues.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.Action:
                    await SeedNotificationPrimaries(group.Key, ids, db.Actions.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.ReviewPackage:
                    await SeedNotificationPrimaries(group.Key, ids, db.ReviewPackages.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.Handoff:
                    await SeedNotificationPrimaries(group.Key, ids, db.Handoffs.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
                case RecordContextKind.WorkConstraint:
                    await SeedNotificationPrimaries(group.Key, ids, db.WorkConstraints.AsNoTracking().Where(row => row.ProjectId == project.Id && ids.Contains(row.Id))); break;
            }
        }
        foreach (var key in keys)
            if (!records.ContainsKey(key)) await Load(key.Kind, key.Id, []);
        // One existing LoadPeople pass can hydrate the complete canonical actor cohort before each View is rendered.
        foreach (var loaded in records.Values) pendingPeople.UnionWith(ActorIds(loaded.Facts));
    }

    async Task SeedNotificationPrimaries<T>(string kind, Guid[] ids, IQueryable<T> query) where T : Entity
    {
        var rows = await query.ToDictionaryAsync(row => row.Id);
        foreach (var id in ids) notificationPrimaries[(kind, id)] = rows.GetValueOrDefault(id);
    }

    async Task<T?> NotificationPrimary<T>(string kind, Guid id, Func<Task<T?>> load) where T : Entity =>
        notificationPrimaries.TryGetValue((kind, id), out var row) ? (T?)row : await load();

    public static RecordContextView Unavailable()
    {
        var result = RecordContextRules.Evaluate(new("Unavailable", "unavailable", "Unknown"));
        return new(new(null, null, "Unknown"), [], result.NeededBy, result.NextAction, result.IsTerminal,
            result.ActorState.ToString(), result.RequiresReviewGate, result.NextTransitions, [], Available: false);
    }

    // Coordination includes package/provenance summaries outside the twelve named item
    // action mappings. Keep their known owner, without inventing a date or command.
    // This does not register InputUse as an exact source locator for submission checks.
    public async Task<RecordContextView?> CoordinationSummary(string kind, Guid id)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        if (kind == "ReviewPackage")
        {
            var row = await db.ReviewPackages.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id) ?? throw ApiException.NotFound();
            return await View(new(new(kind, id.ToString(), row.Status, User(row.CoordinatorId)), row.Key, row.Title));
        }
        if (kind == "InputUse")
        {
            var lookup = await db.InputUses.AsNoTracking().Where(x => x.ProjectId == project.Id && x.Id == id)
                .Select(x => new { Row = x, SourceAvailable = db.SourceRevisions.Any(r => r.ProjectId == project.Id && r.Id == x.SourceRevisionId) })
                .SingleOrDefaultAsync() ?? throw ApiException.NotFound();
            var row = lookup.Row;
            var identity = (row.TargetType, row.TargetId);
            if (!coordinationTargets.TryGetValue(identity, out var target))
            {
                target = row.TargetType is ItemType.Task or ItemType.Deliverable
                    ? records.TryGetValue(identity, out var loaded) ? loaded : await Load(row.TargetType, row.TargetId, []) : null;
                coordinationTargets[identity] = target;
            }
            var inputs = new List<RecordContextInput> { new(target?.Facts ?? new("Unknown", "unavailable", "Unknown")) };
            if (!lookup.SourceAvailable) inputs.Add(new(new("Unknown", "unavailable", "Unknown")));
            return await View(new(new(kind, id.ToString(), "Unknown", User(row.OwnerId), BlockingInputs: inputs), "", row.IntendedUse));
        }
        throw ApiException.NotFound();
    }

    static string? User(Guid? id) => id?.ToString();
    static string External(Guid id) => $"external:{id}";
    static string DisciplineOwner(Guid id) => $"discipline:{id}";

    public async Task<RecordContextView?> Task(WorkTask task)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        // The caller's authorised entity is authoritative; never rebind an id to another project.
        if (task.ProjectId != project.Id) throw ApiException.NotFound();
        return await Read(RecordContextKind.Task, task.Id);
    }

    /// The owning detail reader must authorize project access before calling this method.
    /// The load is always constrained to that authorized project; the disabled path does no reads.
    public async Task<RecordContextView?> Read(string kind, Guid id)
    {
        if (!settings.IsFlagOn("screen_patterns")) return null;
        var loaded = await Load(kind, id, []);
        if (loaded is null) throw ApiException.NotFound();
        return await View(loaded);
    }

    async Task<RecordContextView> View(Loaded loaded)
    {
        await LoadPeople(loaded.Facts);
        var facts = WithPeople(loaded.Facts);
        var result = RecordContextRules.Evaluate(facts);
        var inputs = new List<RecordContextInputView>();
        var unavailable = 0;
        foreach (var input in facts.Inputs)
        {
            if (!Guid.TryParse(input.Id, out var inputId) || !records.TryGetValue((input.Kind, inputId), out var record)) { unavailable++; continue; }
            var next = RecordContextRules.Evaluate(input.Facts);
            inputs.Add(new(input.Kind, inputId, record.Key, record.Title, next.NeededBy,
                next.NextActors.Select(Person).ToArray(), next.NextAction,
                result.PrimaryInputId == input.Id && result.PrimaryInputKind == input.Kind, next.ActorState.ToString()));
        }
        return new(Person(result.OwnerId), result.NextActors.Select(Person).ToArray(), result.NeededBy,
            result.NextAction, result.IsTerminal, result.ActorState.ToString(), result.RequiresReviewGate,
            result.NextTransitions, inputs.OrderBy(x => x.Kind, StringComparer.Ordinal)
                .ThenBy(x => x.Key, StringComparer.Ordinal).ThenBy(x => x.Id).ToArray(), loaded.ReviewPackageGate, unavailable > 0 ? unavailable : null);
    }

    async Task<Loaded?> Load(string kind, Guid id, HashSet<(string, Guid)> path)
    {
        // A malformed/cyclic graph is unassessed, never an ordinary Start/Complete command.
        if (path.Count >= Math.Max(1, settings.ChainDepthLimit) || !path.Add((kind, id)))
            return new(new(kind, id.ToString(), "Unknown"), "", "");
        try
        {
            Loaded? result = null;
            if (kind == RecordContextKind.Task)
            {
                var t = await NotificationPrimary(kind, id, () => db.Tasks.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (t is null) return null;
                var state = notificationTaskStates.TryGetValue(id, out var seededState) ? seededState
                    : await db.TaskStates.AsNoTracking().SingleOrDefaultAsync(x => x.TaskId == id && x.ProjectId == project.Id);
                var inputs = new List<RecordContextInput>();
                if (state is { IsWaiting: true } or { IsBlocked: true })
                {
                    JsonElement[] blockers;
                    try { blockers = JsonSerializer.Deserialize<JsonElement[]>(state.BlockedBy, JsonOpts.Web) ?? []; }
                    catch (JsonException) { blockers = []; }
                    var seenInputs = new HashSet<(string Kind, Guid Id)>();
                    foreach (var blocker in blockers)
                    {
                        // BlockedBy is historical JSON and may contain null or partial
                        // objects. Treat those records as an unknown input instead of
                        // allowing a malformed row to take down task detail.
                        if (blocker.ValueKind != JsonValueKind.Object || !blocker.TryGetProperty("type", out var typeValue) || typeValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(typeValue.GetString()))
                        {
                            inputs.Add(new(new(RecordContextKind.Task, "malformed", "Unknown")));
                            continue;
                        }
                        var type = typeValue.GetString()!;
                        Guid? blockerId = null;
                        if (blocker.TryGetProperty("id", out var idValue) && idValue.ValueKind == JsonValueKind.String && Guid.TryParse(idValue.GetString(), out var parsedId)) blockerId = parsedId;
                        var inputKind = type.ToLowerInvariant() switch
                        {
                            "task" => RecordContextKind.Task, "decision" => RecordContextKind.Decision,
                            "handoff" => RecordContextKind.Handoff, "constraint" or "workconstraint" => RecordContextKind.WorkConstraint,
                            _ => "Unknown",
                        };
                        if (blockerId is { } uniqueId && !seenInputs.Add((inputKind, uniqueId))) continue;
                        var input = blockerId is { } sourceId ? await Load(inputKind, sourceId, path) : null;
                        // Retain an unassessed fact for missing/manual inputs without leaking stale names or ids in the view.
                        var inputFacts = input?.Facts ?? new(inputKind, blockerId?.ToString() ?? "manual", "Unknown");
                        inputs.Add(new(inputFacts));
                    }
                }
                result = new(new(kind, id.ToString(), t.Status, User(t.AssigneeId), t.DueDate,
                    AssigneeId: User(t.AssigneeId), ReviewerId: User(t.ReviewerId), BlockingInputs: inputs,
                    Waiting: state?.IsWaiting == true, Blocked: state?.IsBlocked == true)
                    { PreviousStatus = t.PreviousStatus }, t.Key, t.Name);
            }
            else if (kind == RecordContextKind.Deliverable)
            {
                var d = await NotificationPrimary(kind, id, () => db.Deliverables.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (d is null) return null;
                var gate = DeliverableStatus.IsTerminal(d.Status) ? null : await ReviewPackageGate(d);
                result = new(new(kind, id.ToString(), d.Status, User(d.OwnerId), d.DueDate,
                    ReviewerId: User(d.ReviewerId), RequiresReviewGate: d.RequiresReview)
                    { PreviousStatus = d.PreviousStatus, HasRequiredReviewPackage = d.RequiredReviewPackageId is not null, RequiredReviewSatisfied = gate?.Satisfied }, d.Key, d.Name, gate);
            }
            else if (kind == RecordContextKind.Milestone)
            {
                var m = await NotificationPrimary(kind, id, () => db.Milestones.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (m is null) return null;
                var state = notificationMilestoneStates.TryGetValue(id, out var seededState) ? seededState?.Status
                    : await db.MilestoneStates.AsNoTracking().Where(x => x.ProjectId == project.Id && x.MilestoneId == id).Select(x => x.Status).SingleOrDefaultAsync();
                // Completion/cancellation are authoritative record facts; an unevaluated
                // non-terminal milestone is unassessed instead of being invented as On Track.
                var status = m.IsComplete ? MilestoneStatus.Complete : m.IsCancelled ? MilestoneStatus.Cancelled : state ?? "Unknown";
                result = new(new(kind, id.ToString(), status, User(project.ProjectManagerId), m.Date,
                    ProjectManagerId: User(project.ProjectManagerId)), m.Key, m.Name);
            }
            else if (kind == RecordContextKind.Decision)
            {
                var d = await NotificationPrimary(kind, id, () => db.Decisions.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (d is null) return null;
                var owner = d.OwnerUserId is { } uid ? User(uid) : d.OwnerExternalPartyId is { } eid ? External(eid) : null;
                result = new(new(kind, id.ToString(), d.Status, owner, d.RequiredByDate, DecisionOwnerId: owner), d.Key, d.Subject);
            }
            else if (kind == RecordContextKind.Risk)
            {
                var r = await NotificationPrimary(kind, id, () => db.Risks.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (r is null) return null;
                result = new(new(kind, id.ToString(), r.Status, User(r.OwnerId), r.ReviewDate), r.Key, r.Title);
            }
            else if (kind == RecordContextKind.Issue)
            {
                var issue = await NotificationPrimary(kind, id, () => db.Issues.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (issue is null) return null;
                result = new(new(kind, id.ToString(), issue.Status, User(issue.OwnerId), issue.TargetResolutionDate), issue.Key, issue.Title);
            }
            else if (kind == RecordContextKind.Action)
            {
                var action = await NotificationPrimary(kind, id, () => db.Actions.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (action is null) return null;
                var discipline = action.OwnerType == ActionOwnerType.Discipline
                    ? await db.ProjectDisciplines.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == action.OwnerDisciplineId) : null;
                var owner = action.OwnerType switch
                {
                    ActionOwnerType.User => User(action.OwnerUserId),
                    ActionOwnerType.ExternalParty => action.OwnerExternalPartyId is { } party ? External(party) : null,
                    ActionOwnerType.Discipline => action.OwnerDisciplineId is { } owned ? DisciplineOwner(owned) : null,
                    _ => null,
                };
                var converted = await db.ItemLinks.AsNoTracking().Where(x => x.SourceId == id && x.Relation == ItemRelation.ConvertedToTask)
                    .Select(x => new { x.ProjectId, x.SourceType, x.TargetType, x.TargetId }).Distinct().ToListAsync();
                var related = new List<RecordContextInput>();
                if (converted.Count == 1 && converted[0].ProjectId == project.Id && converted[0].SourceType == ItemType.Action && converted[0].TargetType == ItemType.Task)
                {
                    var target = await Load(RecordContextKind.Task, converted[0].TargetId, path);
                    related.Add(new(target?.Facts ?? new(RecordContextKind.Task, "unavailable", "Unknown")));
                }
                result = new(new(kind, id.ToString(), action.Status, owner, action.DueDate, BlockingInputs: related)
                {
                    OwnerKind = action.OwnerType, DisciplineLeadId = User(discipline?.LeadUserId),
                    DisciplineOwnerAvailable = discipline is not null, FollowsTask = converted.Count > 0,
                }, action.Key, action.Text);
            }
            else if (kind == RecordContextKind.ReviewPackage)
            {
                var package = await NotificationPrimary(kind, id, () => db.ReviewPackages.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (package is null) return null;
                result = new(new(kind, id.ToString(), package.Status, User(package.CoordinatorId)), package.Key, package.Title);
            }
            else if (kind == RecordContextKind.SubmissionPackage)
            {
                var snapshot = await (submissions ??= new(db, project)).Read(id);
                if (snapshot is null) return null;
                var inputs = new List<RecordContextInput>();
                foreach (var check in snapshot.Checks)
                {
                    var child = await Load(RecordContextKind.SubmissionCheck, check.Row.Id, path);
                    if (child is not null) inputs.Add(new(child.Facts, Failed: check.State == Hub.Domain.SubmissionCheckStatus.Fail, Pending: check.State == Hub.Domain.SubmissionCheckStatus.Pending));
                }
                if (snapshot.Unmapped) inputs.Add(new(new("Unknown", "unavailable", "Unknown")));
                result = new(new(kind, id.ToString(), snapshot.State, User(snapshot.Package.CoordinatorId), snapshot.Package.TargetDate,
                    CoordinatorId: User(snapshot.Package.CoordinatorId), ProjectManagerId: User(project.ProjectManagerId), BlockingInputs: inputs)
                { SubmissionProgress = new(snapshot.Readiness is not null, snapshot.CoordinatorEligible, snapshot.ManagerEligible, snapshot.Unmapped) }, snapshot.Package.Key, snapshot.Package.Title);
            }
            else if (kind == RecordContextKind.SubmissionCheck)
            {
                var found = await (submissions ??= new(db, project)).FindCheck(id);
                if (found is null) return null;
                var (snapshot, check) = found.Value;
                var terminal = snapshot.Package.Status is SubmissionStatus.Issued or SubmissionStatus.Superseded or SubmissionStatus.Cancelled;
                var sources = new List<RecordContextInput>();
                var known = true;
                if (!terminal)
                {
                    foreach (var reference in check.References)
                    {
                        var source = reference.RecordId is { } sourceId && reference.RecordKind is { } sourceKind
                            ? await Load(sourceKind, sourceId, path) : null;
                        if (source is null) { known = false; sources.Add(new(new("Unknown", "unavailable", "Unknown"))); }
                        else sources.Add(new(source.Facts));
                    }
                    if (check.Blocking && check.Row.Kind != SubmissionCheckKind.Applicability && sources.Count == 0
                        && check.Codes.Any(c => c is not ("submission.coordinator_inactive" or "submission.check_owner_inactive"))) known = false;
                }
                result = new(new(kind, id.ToString(), check.State ?? "Unknown", User(check.Row.OwnerId), NeededBy: null, BlockingInputs: sources)
                { SubmissionCheckProgress = new(check.Blocking, check.Known, check.OwnerEligible, known, terminal) }, snapshot.Package.Key, check.Row.EvidenceRule ?? check.Row.Kind);
            }
            else if (kind == RecordContextKind.ReviewFinding)
            {
                var finding = await (reviewFindings ??= new(db, project, settings)).Read(id);
                if (finding is null) return null;
                result = new(finding.Facts, finding.Key, finding.Title);
            }
            else if (kind == RecordContextKind.ChangeNotice)
            {
                var snapshot = await ChangeData(id);
                if (snapshot is null) return null;
                var inputs = new List<RecordContextInput>();
                foreach (var a in snapshot.Assessments)
                {
                    if (a.ProjectId != project.Id)
                    {
                        inputs.Add(new(new(RecordContextKind.ChangeAssessment, "unavailable", "Unknown")));
                        continue;
                    }
                    var step = await Load(RecordContextKind.ChangeAssessment, a.Id, path);
                    if (step is not null) inputs.Add(new(step.Facts));
                }
                result = new(new(kind, id.ToString(), snapshot.Notice.Status, User(snapshot.Notice.OwnerId), snapshot.Notice.AssessmentDueDate,
                    BlockingInputs: inputs), snapshot.Notice.Key, snapshot.Notice.Title);
            }
            else if (kind == RecordContextKind.ChangeAssessment)
            {
                var a = changes.Values.SelectMany(x => x.Assessments).FirstOrDefault(x => x.Id == id && x.ProjectId == project.Id)
                    ?? await db.ChangeAssessments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == project.Id);
                if (a is null) return null;
                var snapshot = await ChangeData(a.ChangeNoticeId);
                if (snapshot is null) return null; // A deleted parent never exposes its surviving child.
                var target = snapshot.Work.GetValueOrDefault((a.TargetType, a.TargetId));
                var matches = target is not null && target.Status != TaskStatuses.Cancelled && target.OwnerId == a.OwnerId;
                var eligible = matches && snapshot.EligiblePeople.Contains(a.OwnerId) && snapshot.ActiveDisciplines.Contains(target!.DisciplineId);
                var use = a.InputUseId is { } useId ? snapshot.Uses.GetValueOrDefault(useId) : null;
                var adopted = use is not null && use.TargetType == a.TargetType && use.TargetId == a.TargetId
                    && use.SourceRevisionId == snapshot.Notice.NewRevisionId && snapshot.Revision is { } revision
                    && use.SourceIdentity == Coordination.Identity(revision);
                var correction = a.CorrectionTaskId is { } correctionId ? snapshot.Corrections.GetValueOrDefault(correctionId) : null;
                var correctionEligible = correction is not null && correction.Status != TaskStatuses.Cancelled
                    && correction.AssigneeId is { } assignee && snapshot.EligiblePeople.Contains(assignee)
                    && snapshot.ActiveDisciplines.Contains(correction.ProjectDisciplineId);
                var inputs = new List<RecordContextInput>();
                if (correction is not null)
                {
                    var child = await Load(RecordContextKind.Task, correction.Id, path);
                    if (child is not null) inputs.Add(new(child.Facts));
                }
                var safeComplete = snapshot.Complete.Contains(id) && (a.RetainOldRevision || adopted)
                    && (a.Status != AssessmentStatus.Resolved || correction is { Status: TaskStatuses.Complete });
                var facts = new RecordContextFacts(kind, id.ToString(), a.Status, User(a.OwnerId), snapshot.Notice.AssessmentDueDate,
                    ReviewerId: User(a.ReviewerId), BlockingInputs: inputs)
                {
                    AssessmentProgress = new(snapshot.Notice.Status, safeComplete, matches, eligible,
                        !string.IsNullOrWhiteSpace(a.Rationale), !string.IsNullOrWhiteSpace(a.EvidenceUrl), a.RetainOldRevision,
                        a.RetentionApprovedBy is not null, adopted,
                        target is not null && target.Status is not (DeliverableStatus.Issued or DeliverableStatus.Accepted)
                            && snapshot.Head?.CurrentRevisionId == snapshot.Notice.NewRevisionId,
                        correctionEligible, a.ReviewerId is { } reviewer && snapshot.EligiblePeople.Contains(reviewer),
                        a.ReviewerId is { } verifier && ReviewRules.Independent(verifier,
                            [a.OwnerId, correction?.AssigneeId ?? Guid.Empty, correction?.CreatedBy ?? Guid.Empty], settings.AllowSelfReview)),
                };
                result = new(facts, target?.Key ?? snapshot.Notice.Key, target?.Title ?? snapshot.Notice.Title);
            }
            else if (kind == RecordContextKind.Handoff)
            {
                var h = await NotificationPrimary(kind, id, () => db.Handoffs.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (h is null) return null;
                result = new(new(kind, id.ToString(), h.Status, User(h.SendingOwnerId), h.NeededBy,
                    SendingOwnerId: User(h.SendingOwnerId), ReceivingOwnerId: User(h.ReceivingOwnerId)), h.Key, h.Title);
            }
            else if (kind == RecordContextKind.WorkConstraint)
            {
                var c = await NotificationPrimary(kind, id, () => db.WorkConstraints.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id));
                if (c is null) return null;
                result = new(new(kind, id.ToString(), c.State, User(c.RemovalOwnerId), c.NeededBy,
                    RemovalOwnerId: User(c.RemovalOwnerId), AffectedOwnerId: User(c.AffectedOwnerId)), c.Key, c.Description);
            }
            if (result is not null) records[(kind, id)] = result;
            return result;
        }
        finally { path.Remove((kind, id)); }
    }

    async Task<ChangeSnapshot?> ChangeData(Guid id)
    {
        if (changes.TryGetValue(id, out var cached)) return cached;
        ChangeNotice? notice;
        IReadOnlyList<ChangeAssessment> assessments;
        HashSet<Guid> complete;
        SourceRevision? revision;
        SourceHead? head;
        if (changeSeeds.TryGetValue(id, out var seed))
        {
            notice = seed.Notice; assessments = seed.Assessments; complete = seed.Complete.ToHashSet();
            revision = seed.Revision; head = seed.Head;
        }
        else
        {
            notice = await db.ChangeNotices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.ProjectId == project.Id);
            if (notice is null) return null;
            assessments = await db.ChangeAssessments.AsNoTracking().Where(x => x.ChangeNoticeId == id).ToListAsync();
            var incomplete = await ChangeEndpoints.Incomplete(db).Where(x => x.ChangeNoticeId == id).Select(x => x.Id).ToListAsync();
            complete = assessments.Where(x => !incomplete.Contains(x.Id)).Select(x => x.Id).ToHashSet();
            revision = await db.SourceRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == notice.NewRevisionId);
            head = revision is null ? null : await Coordination.Head(db, revision);
        }
        var scoped = assessments.Where(x => x.ProjectId == project.Id).ToArray();
        var taskIds = scoped.Where(x => x.TargetType == ItemType.Task).Select(x => x.TargetId)
            .Concat(scoped.Select(x => x.CorrectionTaskId).OfType<Guid>()).Distinct().ToArray();
        var tasks = await db.Tasks.AsNoTracking().Where(x => x.ProjectId == project.Id && taskIds.Contains(x.Id)).ToListAsync();
        var work = tasks.ToDictionary(x => (ItemType.Task, x.Id), x => new ChangeWork(x.Id, x.Key, x.Name, x.AssigneeId, x.ProjectDisciplineId, x.Status));
        var deliverableIds = scoped.Where(x => x.TargetType == ItemType.Deliverable).Select(x => x.TargetId).Distinct().ToArray();
        foreach (var d in await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == project.Id && deliverableIds.Contains(x.Id)).ToListAsync())
            work[(ItemType.Deliverable, d.Id)] = new(d.Id, d.Key, d.Name, d.OwnerId, d.ProjectDisciplineId, d.Status);
        var useIds = scoped.Select(x => x.InputUseId).OfType<Guid>().Distinct().ToArray();
        var uses = await db.InputUses.AsNoTracking().Where(x => x.ProjectId == project.Id && useIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var eligible = (await Coordination.People(db, project).Select(x => x.Id).ToListAsync()).ToHashSet();
        var disciplines = (await db.ProjectDisciplines.AsNoTracking().Where(x => x.ProjectId == project.Id && x.IsActive).Select(x => x.Id).ToListAsync()).ToHashSet();
        var snapshot = new ChangeSnapshot(notice, assessments, complete, revision, head, work, tasks.ToDictionary(x => x.Id), uses, eligible, disciplines);
        changes[id] = snapshot;
        return snapshot;
    }

    async Task<RecordContextReviewPackage?> ReviewPackageGate(Deliverable deliverable)
    {
        if (deliverable.RequiredReviewPackageId is not { } id) return null;
        var package = await ReviewIssuedSources.VisiblePackages(db, false).AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == project.Id && x.Id == id);
        if (package is null) return new(null, null, null, null, null, null);
        if (package.Status == ReviewStatus.Approved && package.CurrentRoundId is null)
            return new(package.Id, package.Key, package.Title, package.Status, null, null);
        try
        {
            // Reuse the command's read-only gate, including independence, published/current
            // sources and revision matching. Stored Approved alone never implies a pass.
            await ReviewEndpoints.Gate(db, project, deliverable, null, settings.AllowSelfReview, lockRows: false);
            return new(package.Id, package.Key, package.Title, package.Status, true, null);
        }
        catch (ApiException error) when (error.Status is 400 or 404 or 422)
        {
            var reason = error.Status == 404 ? null : error.Errors?.Values.SelectMany(x => x).FirstOrDefault() ?? error.Message;
            return new(package.Id, package.Key, package.Title, package.Status, error.Status == 404 ? null : false, reason);
        }
    }

    static IEnumerable<string> ActorIds(RecordContextFacts f)
    {
        foreach (var id in new[] { f.OwnerId, f.AssigneeId, f.ReviewerId, f.DecisionOwnerId, f.SendingOwnerId,
            f.ReceivingOwnerId, f.RemovalOwnerId, f.AffectedOwnerId, f.ProjectManagerId, f.CoordinatorId,
            f.CheckingOwnerId, f.OriginatorId, f.VerifierId, f.DisciplineLeadId })
            if (id is not null) yield return id;
        foreach (var id in f.PendingAssessmentOwnerIds.Concat(f.RequiredUpdateOwnerIds)) yield return id;
        foreach (var input in f.Inputs)
            foreach (var id in ActorIds(input.Facts)) yield return id;
    }

    async Task LoadPeople(RecordContextFacts facts)
    {
        var ids = ActorIds(facts).Concat(pendingPeople).Where(x => !resolvedPeople.Contains(x)).Distinct(StringComparer.Ordinal).ToArray();
        if (ids.Length == 0) return;
        var userIds = ids.Where(x => Guid.TryParse(x, out _)).Select(Guid.Parse).ToArray();
        if (userIds.Length > 0) foreach (var u in await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id)).ToListAsync())
            people[u.Id.ToString()] = new(u.Id, u.DisplayName, u.IsActive ? "Active" : "Inactive");
        var disciplineIds = ids.Where(x => x.StartsWith("discipline:", StringComparison.Ordinal)).Select(x => Guid.Parse(x[11..])).ToArray();
        if (disciplineIds.Length > 0) foreach (var d in await db.ProjectDisciplines.AsNoTracking().Where(x => x.ProjectId == project.Id && disciplineIds.Contains(x.Id))
            .Select(x => new { x.Id, Name = x.Discipline!.Name, x.IsActive }).ToListAsync())
            people[DisciplineOwner(d.Id)] = new(d.Id, d.Name, d.IsActive ? "Active" : "Inactive", "Discipline");
        var externalIds = ids.Where(x => x.StartsWith("external:", StringComparison.Ordinal)).Select(x => Guid.Parse(x[9..])).ToArray();
        if (externalIds.Length > 0) foreach (var p in await db.ExternalParties.AsNoTracking().Where(x => x.ProjectId == project.Id && externalIds.Contains(x.Id)).ToListAsync())
            people[External(p.Id)] = new(p.Id, p.Name, p.IsActive ? "External" : "Inactive", "ExternalParty");
        resolvedPeople.UnionWith(ids); // Include negative results so missing identities are not repeatedly queried.
        pendingPeople.Clear();
    }

    RecordContextFacts WithPeople(RecordContextFacts f) => f with
    {
        Actors = ActorIds(f).Distinct(StringComparer.Ordinal).Select(id => new RecordContextActor(id,
            people.TryGetValue(id, out var p) && Enum.TryParse<RecordContextActorState>(p.State, out var state) ? state : RecordContextActorState.Unknown)).ToArray(),
        BlockingInputs = f.Inputs.Select(input => input with { Facts = WithPeople(input.Facts) }).ToArray(),
    };

    RecordContextPerson Person(RecordContextActor actor) => Person(actor.Id);
    RecordContextPerson Person(string? id) => id is null ? new(null, null, "Unassigned")
        : people.GetValueOrDefault(id) ?? (id.StartsWith("external:", StringComparison.Ordinal)
            ? new(Guid.TryParse(id[9..], out var externalId) ? externalId : null, null, "Unknown", "ExternalParty")
            : id.StartsWith("discipline:", StringComparison.Ordinal)
                ? new(Guid.TryParse(id[11..], out var disciplineId) ? disciplineId : null, null, "Unknown", "Discipline")
                : new(Guid.TryParse(id, out var guid) ? guid : null, null, "Unknown"));
}
