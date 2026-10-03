using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace Hub.Api.Features;

public static class DesignBasisEndpoints
{
    public sealed record VersionInput(string Scope, string Statement, decimal? NumericValue, string? Units,
        string? SourceSystem, string? StableSourceId, string? SourceUrl, string? DeclaredRevision,
        DateOnly? ConfirmationDueDate, Guid? DecisionId);
    public sealed record CreateBody(Guid RequestId, string Kind, string Title, Guid OwnerId,
        Guid ProjectDisciplineId, Guid? IndependentApproverId, VersionInput Version, string? Reason,
        Guid? InspectedDuplicateId = null);
    public sealed record AssignBody(Guid RequestId, int EntryRowVersion, Guid OwnerId,
        Guid? IndependentApproverId, string Reason);
    public sealed record ProposeBody(Guid RequestId, int EntryRowVersion, int CurrentVersionRowVersion,
        VersionInput Version, string Reason, Guid? InspectedDuplicateId = null);
    public sealed record EditProposedBody(Guid RequestId, int EntryRowVersion, int VersionRowVersion,
        VersionInput Version, string Reason, Guid? InspectedDuplicateId = null);
    public sealed record ConfirmBody(Guid RequestId, int EntryRowVersion, int VersionRowVersion, string Rationale);
    public sealed record WithdrawBody(Guid RequestId, int EntryRowVersion, int VersionRowVersion, string Reason);
    public sealed record UseBody(Guid RequestId, Guid VersionId, string TargetType, Guid TargetId, string IntendedUse);
    public sealed record DispositionBody(Guid RequestId, int VersionRowVersion, string Scope, Guid OwnerId,
        DateOnly ExpiresOn, string Reason);
    public sealed record ImpactBody(Guid RequestId, int AssessmentRowVersion, int BasisUseRowVersion,
        int? NewVersionRowVersion, int TargetRowVersion, string Action, string Rationale, string EvidenceUrl);
    public sealed record ResolveConflictBody(Guid RequestId, int ConflictRowVersion, Guid ResolutionVersionId,
        int ResolutionVersionRowVersion, string Rationale);
    public sealed record ConflictSide(Guid VersionId, string EntryKey, string Scope, string Statement,
        decimal? NumericValue, string? Units);
    public sealed record Filter(string? Kind, string? Status, Guid? DisciplineId, string? Scope,
        bool? Overdue, Guid? AffectedWorkId);
    static readonly Col[] ExportColumns = [new("key", "key"), new("title", "name"),
        new("kind", "kind", Label: "Kind"), new("discipline", "discipline"),
        new("owner", "owner", Label: "Owner"), new("version", "revision", Label: "Version"),
        new("status", "status"), new("scope", "scope", Label: "Scope"),
        new("statement", "statement", Label: "Value or statement"), new("numericValue", "value", "number", "Numeric value"),
        new("units", "units", Label: "Units"), new("sourceSystem", "sourceSystem", Label: "Source system"),
        new("stableSourceId", "sourceId", Label: "Stable source ID"), new("sourceUrl", "sourceUrl", Label: "Source URL"),
        new("declaredRevision", "declaredRevision", Label: "Declared source revision"),
        new("confirmationDueDate", "due", "date", "Confirmation due date")];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/design-basis", List);
        api.MapGet("/projects/{projectId:guid}/design-basis/export", ExportRows);
        api.MapGet("/projects/{projectId:guid}/design-basis/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/design-basis", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/assign", Assign).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/propose", Propose).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/versions/{versionId:guid}/edit", EditProposed)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/versions/{versionId:guid}/confirm", Confirm).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/versions/{versionId:guid}/withdraw", Withdraw)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/uses", LinkUse).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/versions/{versionId:guid}/proceed", ProceedUnderAssumption)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/{id:guid}/impacts/{impactId:guid}/decide", DecideImpact)
            .WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/design-basis/conflicts/{conflictId:guid}/resolve", ResolveConflict)
            .WithMetadata(new Coordination.AtomicCommand());
    }

    // Queued inside the command transaction: one notice per recipient and entry; refused, stale or replayed
    // commands add none, and recipients must still hold project access (FR-MDC-02, FR-MDC-03, FR-MDC-06).
    // The link opens the affected entry in the register (`?basis=`), not just the register.
    internal static Task Notify(Notifier notify, Project project, DesignBasisEntry entry, string eventType, IEnumerable<Guid?> recipients, string title) =>
        notify.Send(eventType, recipients, new NotifyItem(project.Id, "DesignBasisEntry", entry.Id, entry.Key,
            $"/projects/{project.ProjectNumber}/design-basis?basis={entry.Id}", project.ProjectNumber), title);

    static async Task<DesignBasisEntry> Entry(HubDb db, Guid projectId, Guid id) =>
        await db.DesignBasisEntries.SingleOrDefaultAsync(e => e.ProjectId == projectId && e.Id == id) ?? throw ApiException.NotFound();

    static async Task<DesignBasisVersion> Version(HubDb db, Guid projectId, Guid entryId, Guid versionId) =>
        await db.DesignBasisVersions.SingleOrDefaultAsync(v => v.ProjectId == projectId && v.EntryId == entryId && v.Id == versionId)
            ?? throw ApiException.NotFound();

    static async Task ValidateSource(HubDb db, Project project, VersionInput input)
    {
        if (input.SourceUrl is not null) Coordination.Url(input.SourceUrl);
        if (input.DecisionId is { } decisionId)
            Check.That(await db.Decisions.AnyAsync(d => d.Id == decisionId && d.ProjectId == project.Id), "decisionId", "coord.reference");
    }

    static void RequireConfirmationDueDate(VersionInput input) =>
        Check.That(input.ConfirmationDueDate is not null, "confirmationDueDate", "error.required");

    static DesignBasisVersion NewVersion(Project project, Guid entryId, int number, Guid? supersedes, VersionInput input) => new()
    {
        ProjectId = project.Id, EntryId = entryId, Number = number, SupersedesVersionId = supersedes,
        Scope = Check.Required(input.Scope, "scope", 500), Statement = Check.Required(input.Statement, "statement", 4000),
        NumericValue = input.NumericValue, Units = input.Units?.Trim(), SourceSystem = input.SourceSystem?.Trim(),
        StableSourceId = input.StableSourceId?.Trim(), SourceUrl = input.SourceUrl?.Trim(),
        DeclaredRevision = input.DeclaredRevision?.Trim(), ConfirmationDueDate = input.ConfirmationDueDate,
        DecisionId = input.DecisionId,
    };

    static async Task InspectDuplicate(HubDb db, Guid projectId, string kind, string title, Guid disciplineId,
        string scope, Guid? excludeEntryId, Guid? inspectedId)
    {
        var duplicate = await db.DesignBasisEntries.Where(e => e.ProjectId == projectId && e.Id != excludeEntryId &&
            e.ProjectDisciplineId == disciplineId && e.Kind == kind && e.Title.ToLower() == title.ToLower() &&
            db.DesignBasisVersions.Any(v => v.EntryId == e.Id && v.Scope.ToLower() == scope.ToLower() && v.Status != BasisStatus.Withdrawn))
            .OrderBy(e => e.Id).Select(e => (Guid?)e.Id).FirstOrDefaultAsync();
        if (duplicate is { } existingId && inspectedId != existingId)
            throw ApiException.Conflict("basis_duplicate", "basis.duplicate", new { existingId });
    }

    static Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.create", body }, access, db, clock, async (project, ctx) =>
        {
            var create = Permissions.CreateBasis(access.Actor, ctx, body.ProjectDisciplineId, body.OwnerId, body.IndependentApproverId);
            if (!create) throw ApiException.Forbidden(create.Why!, create.Arg ?? await db.ProjectDisciplines
                .Where(d => d.Id == body.ProjectDisciplineId && d.ProjectId == project.Id).Select(d => d.Discipline!.Name).FirstOrDefaultAsync() ?? "");
            Check.OneOf(body.Kind, BasisKind.All, "kind");
            await Coordination.Discipline(db, project.Id, body.ProjectDisciplineId);
            await Coordination.Person(db, project, body.OwnerId);
            if (body.IndependentApproverId is { } approver) await Coordination.Person(db, project, approver, "independentApproverId");
            await ValidateSource(db, project, body.Version);
            RequireConfirmationDueDate(body.Version);
            var title = Check.Required(body.Title, "title", 200);
            var scope = Check.Required(body.Version.Scope, "scope", 500);
            await InspectDuplicate(db, project.Id, body.Kind, title, body.ProjectDisciplineId, scope, null, body.InspectedDuplicateId);
            var entry = new DesignBasisEntry { ProjectId = project.Id, Kind = body.Kind, Title = title,
                OwnerId = body.OwnerId, ProjectDisciplineId = body.ProjectDisciplineId,
                IndependentApproverId = body.IndependentApproverId };
            (entry.Seq, entry.Key) = await Keys.Next(db, project.Id, project.ProjectNumber, "basis");
            db.DesignBasisEntries.Add(entry);
            db.DesignBasisVersions.Add(NewVersion(project, entry.Id, 1, null, body.Version));
            db.Audit.Note(entry, reason: body.Reason);
            return entry;
        });

    static Task<Coordination.Result> Propose(Guid projectId, Guid id, ProposeBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.propose", id, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            Coordination.Version(entry, body.EntryRowVersion);
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
            if (entry.CurrentVersionId is not { } currentId) throw ApiException.Invalid("entryId", "basis.current");
            var current = await Version(db, project.Id, id, currentId);
            Coordination.Version(current, body.CurrentVersionRowVersion);
            Check.That(current.Status == BasisStatus.Confirmed, "entryId", "basis.current");
            Check.That(!await db.BasisImpactAssessments.AnyAsync(a => a.ProjectId == project.Id &&
                a.NewVersionId == current.Id && a.Status == AssessmentStatus.Pending), "entryId", "basis.current");
            Check.That(!await db.DesignBasisVersions.AnyAsync(v => v.EntryId == id && v.Status == BasisStatus.Proposed), "entryId", "basis.proposed");
            await ValidateSource(db, project, body.Version);
            RequireConfirmationDueDate(body.Version);
            await InspectDuplicate(db, project.Id, entry.Kind, entry.Title, entry.ProjectDisciplineId,
                Check.Required(body.Version.Scope, "scope", 500), entry.Id, body.InspectedDuplicateId);
            var nextNumber = await db.DesignBasisVersions.Where(v => v.EntryId == id).MaxAsync(v => v.Number) + 1;
            var next = NewVersion(project, id, nextNumber, current.Id, body.Version);
            db.DesignBasisVersions.Add(next);
            db.Audit.Note(next, reason: Check.Reason(body.Reason));
            return next;
        });

    static Task<Coordination.Result> EditProposed(Guid projectId, Guid id, Guid versionId, EditProposedBody body,
        Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.edit-proposed", id, versionId, body }, access, db, clock,
            async (project, ctx) =>
            {
                var entry = await Entry(db, project.Id, id);
                var version = await Version(db, project.Id, id, versionId);
                Coordination.Version(entry, body.EntryRowVersion);
                Coordination.Version(version, body.VersionRowVersion);
                var ownerAction = Permissions.NamedCoordinationAction(access.Actor, ctx, entry.OwnerId);
                if (!ownerAction.Ok) Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
                Check.That(version.Status == BasisStatus.Proposed, "versionId", "basis.proposed");
                Check.That(!await db.BasisUses.AnyAsync(u => u.ProjectId == project.Id && u.VersionId == versionId) &&
                    !await db.BasisAssumptionDispositions.AnyAsync(d => d.ProjectId == project.Id && d.VersionId == versionId),
                    "versionId", "basis.edit_linked");
                await ValidateSource(db, project, body.Version);
                RequireConfirmationDueDate(body.Version);
                await InspectDuplicate(db, project.Id, entry.Kind, entry.Title, entry.ProjectDisciplineId,
                    Check.Required(body.Version.Scope, "scope", 500), entry.Id, body.InspectedDuplicateId);
                var revised = NewVersion(project, id, version.Number, version.SupersedesVersionId, body.Version);
                version.Scope = revised.Scope;
                version.Statement = revised.Statement;
                version.NumericValue = revised.NumericValue;
                version.Units = revised.Units;
                version.SourceSystem = revised.SourceSystem;
                version.StableSourceId = revised.StableSourceId;
                version.SourceUrl = revised.SourceUrl;
                version.DeclaredRevision = revised.DeclaredRevision;
                version.ConfirmationDueDate = revised.ConfirmationDueDate;
                version.DecisionId = revised.DecisionId;
                db.Audit.Note(version, reason: Check.Reason(body.Reason));
                await SubmissionEndpoints.InvalidateForDesignBasisEntry(db, project.Id, entry.Id, notify);
                return version;
            });

    static Task<Coordination.Result> Assign(Guid projectId, Guid id, AssignBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.assign", id, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
            Coordination.Version(entry, body.EntryRowVersion);
            await Coordination.Person(db, project, body.OwnerId);
            if (body.IndependentApproverId is { } approver)
                await Coordination.Person(db, project, approver, "independentApproverId");
            Check.That(body.IndependentApproverId is null || body.IndependentApproverId != body.OwnerId,
                "independentApproverId", "basis.independent");
            entry.OwnerId = body.OwnerId;
            entry.IndependentApproverId = body.IndependentApproverId;
            db.Audit.Note(entry, reason: Check.Reason(body.Reason));
            return entry;
        });

    static Task<Coordination.Result> Confirm(Guid projectId, Guid id, Guid versionId, ConfirmBody body,
        Access access, HubDb db, TimeProvider clock, SettingsStore settings, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.confirm", id, versionId, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            Coordination.Version(entry, body.EntryRowVersion);
            var version = await Version(db, project.Id, id, versionId);
            Coordination.Version(version, body.VersionRowVersion);
            Access.Demand(Permissions.ConfirmBasis(access.Actor, ctx, entry.ProjectDisciplineId, entry.IndependentApproverId));
            var self = (await settings.Get(db)).AllowSelfReview;
            Check.That(self || access.Me.Id != entry.OwnerId, "approverId", "basis.independent");
            Check.That(!string.IsNullOrWhiteSpace(version.SourceSystem) &&
                !string.IsNullOrWhiteSpace(version.DeclaredRevision), "source", "basis.source");
            RequireConfirmationDueDate(new VersionInput(version.Scope, version.Statement, version.NumericValue, version.Units,
                version.SourceSystem, version.StableSourceId, version.SourceUrl, version.DeclaredRevision,
                version.ConfirmationDueDate, version.DecisionId));
            Check.That(BasisRules.MayConfirm(version.Status, version.NumericValue, version.Units,
                !string.IsNullOrWhiteSpace(version.SourceUrl), !string.IsNullOrWhiteSpace(body.Rationale),
                true), "versionId", version.NumericValue is not null && string.IsNullOrWhiteSpace(version.Units) ? "basis.units" : "basis.source");
            Coordination.Url(version.SourceUrl);
            DesignBasisVersion? prior = null;
            if (entry.CurrentVersionId is { } currentId)
            {
                Check.That(version.SupersedesVersionId == currentId, "versionId", "basis.current");
                prior = await Version(db, project.Id, id, currentId);
                Check.That(prior.Status == BasisStatus.Confirmed, "versionId", "basis.current");
                prior.Status = BasisStatus.Superseded;
                db.Audit.Note(prior, reason: body.Rationale);
            }
            else Check.That(version.Number == 1 && version.SupersedesVersionId is null, "versionId", "basis.current");
            version.Status = BasisStatus.Confirmed; version.ConfirmedBy = access.Me.Id;
            version.ConfirmedAt = clock.GetUtcNow(); version.ConfirmationRationale = Check.Reason(body.Rationale);
            entry.CurrentVersionId = version.Id;
            db.Audit.Note(version, reason: body.Rationale); db.Audit.Note(entry, reason: body.Rationale);
            if (prior is not null)
            {
                var entryVersionIds = await db.DesignBasisVersions.Where(v => v.ProjectId == project.Id && v.EntryId == id)
                    .Select(v => v.Id).ToListAsync();
                var uses = await db.BasisUses.Where(u => u.ProjectId == project.Id && entryVersionIds.Contains(u.VersionId)).ToListAsync();
                var owners = new List<Guid?>();
                foreach (var use in uses.GroupBy(u => new { u.TargetType, u.TargetId })
                    .Select(g => g.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).First()))
                {
                    db.BasisImpactAssessments.Add(new BasisImpactAssessment { ProjectId = project.Id,
                        BasisUseId = use.Id, OldVersionId = use.VersionId, NewVersionId = version.Id, OwnerId = use.OwnerId });
                    owners.Add(use.OwnerId);
                }
                await Notify(notify, project, entry, NotificationEvents.BasisImpactPending, owners, Text.Get("notify.basis_replaced", entry.Key));
            }
            var peers = await db.DesignBasisEntries.AsNoTracking().Where(e => e.ProjectId == project.Id && e.Id != id &&
                e.Kind == entry.Kind && e.ProjectDisciplineId == entry.ProjectDisciplineId &&
                e.Title.ToLower() == entry.Title.ToLower())
                .Join(db.DesignBasisVersions.AsNoTracking().Where(v => v.Status == BasisStatus.Confirmed),
                    e => e.Id, v => v.EntryId, (e, v) => v).ToListAsync();
            static string Value(DesignBasisVersion v) => v.NumericValue is { } number
                ? $"{number.ToString(CultureInfo.InvariantCulture)} {v.Units?.Trim()}" : v.Statement;
            var conflictOwners = new List<Guid?>();
            foreach (var peer in peers.Where(peer => BasisRules.ValuesConflict(entry.Kind, entry.Kind,
                BasisStatus.Confirmed, peer.Status, entry.ProjectDisciplineId, entry.ProjectDisciplineId,
                entry.Title, entry.Title, version.Scope, peer.Scope, Value(version), Value(peer))))
            {
                var left = version.Id.CompareTo(peer.Id) < 0 ? version.Id : peer.Id;
                var right = left == version.Id ? peer.Id : version.Id;
                if (!await db.BasisConflicts.AnyAsync(c => c.LeftVersionId == left && c.RightVersionId == right))
                {
                    db.BasisConflicts.Add(new BasisConflict { ProjectId = project.Id, LeftVersionId = left, RightVersionId = right });
                    conflictOwners.Add(await db.DesignBasisEntries.Where(e => e.Id == peer.EntryId).Select(e => e.OwnerId).FirstAsync());
                }
            }
            if (conflictOwners.Count > 0)
                await Notify(notify, project, entry, NotificationEvents.BasisConflictRaised, [entry.OwnerId, .. conflictOwners],
                    Text.Get("notify.basis_conflict", entry.Key));
            await SubmissionEndpoints.InvalidateForDesignBasisEntry(db, project.Id, entry.Id, notify);
            return version;
        });

    static Task<Coordination.Result> Withdraw(Guid projectId, Guid id, Guid versionId, WithdrawBody body,
        Access access, HubDb db, TimeProvider clock, Notifier notify) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.withdraw", id, versionId, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            Coordination.Version(entry, body.EntryRowVersion);
            var version = await Version(db, project.Id, id, versionId);
            Coordination.Version(version, body.VersionRowVersion);
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
            Check.That(BasisRules.MayWithdraw(version.Status), "versionId", "basis.withdraw");
            if (version.Status == BasisStatus.Confirmed)
                Check.That(!await db.DesignBasisVersions.AnyAsync(v => v.EntryId == id && v.Status == BasisStatus.Proposed),
                    "versionId", "basis.proposed");
            var reason = Check.Reason(body.Reason);
            version.Status = BasisStatus.Withdrawn;
            if (entry.CurrentVersionId == version.Id)
            {
                entry.CurrentVersionId = null;
                var ids = await db.DesignBasisVersions.Where(v => v.ProjectId == project.Id && v.EntryId == id)
                    .Select(v => v.Id).ToListAsync();
                var uses = await db.BasisUses.Where(u => u.ProjectId == project.Id && ids.Contains(u.VersionId)).ToListAsync();
                var owners = new List<Guid?>();
                foreach (var use in uses.GroupBy(u => new { u.TargetType, u.TargetId })
                    .Select(g => g.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).First()))
                    if (!await db.BasisImpactAssessments.AnyAsync(a => a.ProjectId == project.Id && a.BasisUseId == use.Id &&
                        a.OldVersionId == use.VersionId && a.WithdrawalVersionId == version.Id &&
                        a.NewVersionId == null && a.Status == AssessmentStatus.Pending))
                    {
                        db.BasisImpactAssessments.Add(new BasisImpactAssessment { ProjectId = project.Id,
                            BasisUseId = use.Id, OldVersionId = use.VersionId, NewVersionId = null,
                            WithdrawalVersionId = version.Id, OwnerId = use.OwnerId });
                        owners.Add(use.OwnerId);
                    }
                await Notify(notify, project, entry, NotificationEvents.BasisImpactPending, owners, Text.Get("notify.basis_withdrawn", entry.Key));
            }
            db.Audit.Note(version, action: "Withdrawn", reason: reason);
            db.Audit.Note(entry, reason: reason);
            await SubmissionEndpoints.InvalidateForDesignBasisEntry(db, project.Id, entry.Id, notify);
            return version;
        });

    static Task<Coordination.Result> ProceedUnderAssumption(Guid projectId, Guid id, Guid versionId,
        DispositionBody body, Access access, HubDb db, TimeProvider clock, SettingsStore settings) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.proceed", id, versionId, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            var version = await Version(db, project.Id, id, versionId);
            Coordination.Version(version, body.VersionRowVersion);
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
            Check.That(entry.Kind == BasisKind.Assumption && version.Status == BasisStatus.Proposed,
                "versionId", "basis.assumption");
            await Coordination.Person(db, project, body.OwnerId);
            Check.That((await settings.Get(db)).AllowSelfReview || access.Me.Id != body.OwnerId,
                "approvedBy", "basis.independent");
            Check.That(body.ExpiresOn >= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime), "expiresOn", "basis.assumption");
            var scope = Check.Required(body.Scope, "scope", 500);
            Check.That(string.Equals(scope, version.Scope, StringComparison.OrdinalIgnoreCase), "scope", "basis.assumption");
            var disposition = new BasisAssumptionDisposition { ProjectId = project.Id, VersionId = version.Id,
                Scope = scope, OwnerId = body.OwnerId, ApprovedBy = access.Me.Id, ExpiresOn = body.ExpiresOn,
                Reason = Check.Reason(body.Reason) };
            db.BasisAssumptionDispositions.Add(disposition);
            return disposition;
        });

    static Task<Coordination.Result> LinkUse(Guid projectId, Guid id, UseBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.use", id, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            var version = await Version(db, project.Id, id, body.VersionId);
            var target = await Coordination.Target(db, project, body.TargetType, body.TargetId);
            Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, target.OwnerId));
            var proceed = entry.Kind == BasisKind.Assumption && version.Status == BasisStatus.Proposed &&
                await db.BasisAssumptionDispositions.AnyAsync(d => d.ProjectId == project.Id && d.VersionId == version.Id &&
                    d.OwnerId == target.OwnerId && d.Scope.ToLower() == version.Scope.ToLower() &&
                    d.ExpiresOn >= DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
            Check.That(version.Status == BasisStatus.Confirmed && entry.CurrentVersionId == version.Id || proceed,
                "versionId", "basis.assumption");
            Check.That(!await db.BasisUses.AnyAsync(u => u.ProjectId == project.Id && u.TargetType == body.TargetType &&
                u.TargetId == body.TargetId && db.DesignBasisVersions.Any(v => v.Id == u.VersionId && v.EntryId == id)),
                "targetId", "basis.duplicate");
            var use = new BasisUse { ProjectId = project.Id, VersionId = version.Id, TargetType = target.Type,
                TargetId = target.Id, OwnerId = target.OwnerId, IntendedUse = Check.Required(body.IntendedUse, "intendedUse", 2000) };
            db.BasisUses.Add(use);
            return use;
        });

    static Task<Coordination.Result> DecideImpact(Guid projectId, Guid id, Guid impactId, ImpactBody body,
        Access access, HubDb db, TimeProvider clock, SettingsStore settings) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.impact", id, impactId, body }, access, db, clock, async (project, ctx) =>
        {
            var entry = await Entry(db, project.Id, id);
            var impact = await db.BasisImpactAssessments.SingleOrDefaultAsync(a => a.Id == impactId && a.ProjectId == project.Id)
                ?? throw ApiException.NotFound();
            Coordination.Version(impact, body.AssessmentRowVersion);
            Check.That(impact.Status == AssessmentStatus.Pending, "status", "basis.current");
            var old = await Version(db, project.Id, id, impact.OldVersionId);
            DesignBasisVersion? next = null;
            if (impact.NewVersionId is { } nextId)
            {
                next = await Version(db, project.Id, id, nextId);
                if (body.NewVersionRowVersion is { } nextRowVersion) Coordination.Version(next, nextRowVersion);
                Check.That(old.Status == BasisStatus.Superseded && next.Status == BasisStatus.Confirmed &&
                    entry.CurrentVersionId == next.Id && old.Number < next.Number, "versionId", "basis.current");
            }
            else
            {
                var decisionReopened = impact.WithdrawalVersionId is null && old.DecisionId is { } decisionId && await db.Decisions.AnyAsync(d =>
                    d.ProjectId == project.Id && d.Id == decisionId && d.Status == DecisionStatus.Pending);
                var withdrawal = impact.WithdrawalVersionId is { } withdrawalId && await db.DesignBasisVersions.AnyAsync(v =>
                    v.ProjectId == project.Id && v.EntryId == id && v.Id == withdrawalId && v.Status == BasisStatus.Withdrawn);
                Check.That((withdrawal && entry.CurrentVersionId is null) || decisionReopened,
                    "versionId", "basis.current");
            }
            var use = await db.BasisUses.SingleOrDefaultAsync(u => u.Id == impact.BasisUseId && u.ProjectId == project.Id)
                ?? throw ApiException.NotFound();
            Coordination.Version(use, body.BasisUseRowVersion);
            Check.That(use.VersionId == old.Id && use.OwnerId == impact.OwnerId, "basisUseId", "basis.current");
            var target = await Coordination.Target(db, project, use.TargetType, use.TargetId);
            if (target.RowVersion != body.TargetRowVersion || target.OwnerId != use.OwnerId)
                throw ApiException.Conflict("concurrency_conflict", "coord.stale");
            Check.OneOf(body.Action, ["Adopt", "Unaffected"], "action");
            Check.That(body.Action != "Adopt" || next is not null, "action", "basis.current");
            if (body.Action == "Adopt") Access.Demand(Permissions.NamedCoordinationAction(access.Actor, ctx, use.OwnerId));
            else
            {
                Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId));
                Check.That((await settings.Get(db)).AllowSelfReview || access.Me.Id != use.OwnerId,
                    "approverId", "basis.independent");
            }
            impact.Rationale = Check.Reason(body.Rationale);
            impact.EvidenceUrl = Coordination.Url(body.EvidenceUrl);
            impact.DecidedBy = access.Me.Id; impact.DecidedAt = clock.GetUtcNow();
            impact.Status = body.Action == "Adopt" ? AssessmentStatus.Resolved : AssessmentStatus.Unaffected;
            if (body.Action == "Adopt")
            {
                Check.That(next is not null, "action", "basis.current");
                Check.That(!await db.BasisUses.AnyAsync(u => u.ProjectId == project.Id && u.VersionId == next!.Id &&
                    u.TargetType == use.TargetType && u.TargetId == use.TargetId), "basisUseId", "basis.duplicate");
                db.BasisUses.Add(new BasisUse { ProjectId = project.Id, VersionId = next!.Id, TargetType = use.TargetType,
                    TargetId = use.TargetId, OwnerId = use.OwnerId, IntendedUse = use.IntendedUse });
            }
            db.Audit.Note(impact, reason: impact.Rationale);
            return impact;
        });

    static Task<Coordination.Result> ResolveConflict(Guid projectId, Guid conflictId, ResolveConflictBody body,
        Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "basis.conflict.resolve", conflictId, body }, access, db, clock, async (project, ctx) =>
        {
            var conflict = await db.BasisConflicts.SingleOrDefaultAsync(c => c.ProjectId == project.Id && c.Id == conflictId)
                ?? throw ApiException.NotFound();
            Coordination.Version(conflict, body.ConflictRowVersion);
            Check.That(!conflict.Resolved, "conflictId", "basis.conflict_resolved");
            var left = await db.DesignBasisVersions.SingleOrDefaultAsync(v => v.ProjectId == project.Id && v.Id == conflict.LeftVersionId)
                ?? throw ApiException.NotFound();
            var right = await db.DesignBasisVersions.SingleOrDefaultAsync(v => v.ProjectId == project.Id && v.Id == conflict.RightVersionId)
                ?? throw ApiException.NotFound();
            var resolution = await db.DesignBasisVersions.SingleOrDefaultAsync(v => v.ProjectId == project.Id && v.Id == body.ResolutionVersionId)
                ?? throw ApiException.NotFound();
            Coordination.Version(resolution, body.ResolutionVersionRowVersion);
            var leftEntry = await Entry(db, project.Id, left.EntryId);
            var rightEntry = await Entry(db, project.Id, right.EntryId);
            Check.That(resolution.Status == BasisStatus.Confirmed, "resolutionVersionId", "basis.current");
            Check.That(resolution.Id != left.Id && resolution.Id != right.Id &&
                (resolution.SupersedesVersionId == left.Id || resolution.SupersedesVersionId == right.Id),
                "resolutionVersionId", "basis.conflict_resolution");
            Access.Demand(Permissions.ManageCoordination(access.Actor, ctx, leftEntry.ProjectDisciplineId));
            Check.That(leftEntry.ProjectDisciplineId == rightEntry.ProjectDisciplineId,
                "conflictId", "basis.conflict_resolution");
            conflict.Resolved = true;
            conflict.ResolutionVersionId = resolution.Id;
            conflict.ResolvedBy = access.Me.Id;
            conflict.ResolvedAt = clock.GetUtcNow();
            db.Audit.Note(conflict, action: "Resolved", reason: Check.Reason(body.Rationale));
            return conflict;
        });

    static IQueryable<DesignBasisEntry> Query(Guid projectId, Filter filter, HubDb db, DateOnly today)
    {
        var query = db.DesignBasisEntries.AsNoTracking().Where(e => e.ProjectId == projectId);
        if (filter.Kind is { } kind) { Check.OneOf(kind, BasisKind.All, "kind"); query = query.Where(e => e.Kind == kind); }
        if (filter.DisciplineId is { } did) query = query.Where(e => e.ProjectDisciplineId == did);
        if (filter.Status is { } status) { Check.OneOf(status, BasisStatus.All, "status"); query = query.Where(e =>
            db.DesignBasisVersions.Where(v => v.EntryId == e.Id).OrderByDescending(v => v.Number)
                .Select(v => v.Status).FirstOrDefault() == status); }
        if (!string.IsNullOrWhiteSpace(filter.Scope))
        {
            var scope = filter.Scope.Trim().ToLower();
            query = query.Where(e => (db.DesignBasisVersions.Where(v => v.EntryId == e.Id)
                .OrderByDescending(v => v.Number).Select(v => v.Scope).FirstOrDefault() ?? "")
                .ToLower().Contains(scope));
        }
        if (filter.Overdue == true) query = query.Where(e => db.DesignBasisVersions.Any(v => v.EntryId == e.Id &&
            v.Status == BasisStatus.Proposed && v.ConfirmationDueDate < today &&
            !db.DesignBasisVersions.Any(later => later.EntryId == e.Id && later.Number > v.Number)));
        if (filter.AffectedWorkId is { } targetId) query = query.Where(e =>
            db.DesignBasisVersions.Any(v => v.EntryId == e.Id &&
                db.BasisUses.Any(u => u.VersionId == v.Id && u.TargetId == targetId)));
        return query;
    }

    static async Task<object> List(Guid projectId, [AsParameters] Filter filter,
        int? page, int? pageSize, Access access, HubDb db, TimeProvider clock)
    {
        await access.Project(projectId, false);
        var query = Query(projectId, filter, db, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        var (pg, size) = Http.Paging(page, pageSize);
        var entries = await query.OrderBy(e => e.Seq).Skip((pg - 1) * size).Take(size)
            .Select(e => new { e.Id, e.Key, e.Title, e.Kind, e.OwnerId, e.ProjectDisciplineId, e.IndependentApproverId,
                e.CurrentVersionId, e.RowVersion }).ToListAsync();
        var entryIds = entries.Select(e => e.Id).ToArray();
        var versions = await db.DesignBasisVersions.AsNoTracking().Where(v => entryIds.Contains(v.EntryId))
            .Select(v => new { v.Id, v.EntryId, v.Number, v.Status, v.Scope, v.ConfirmationDueDate }).ToListAsync();
        var conflictIds = versions.Select(v => v.Id).ToArray();
        var conflicts = await db.BasisConflicts.AsNoTracking().Where(c => !c.Resolved &&
            (conflictIds.Contains(c.LeftVersionId) || conflictIds.Contains(c.RightVersionId))).ToListAsync();
        var rows = entries.Select(e =>
        {
            var current = versions.FirstOrDefault(v => v.Id == e.CurrentVersionId);
            var latest = versions.Where(v => v.EntryId == e.Id).OrderByDescending(v => v.Number).FirstOrDefault();
            return (object)new { e.Id, e.Key, e.Title, e.Kind, e.OwnerId, e.ProjectDisciplineId, e.CurrentVersionId,
                e.RowVersion, CurrentStatus = current?.Status, CurrentScope = current?.Scope,
                LatestStatus = latest?.Status, LatestVersionId = latest?.Id, latest?.ConfirmationDueDate,
                ConflictCount = conflicts.Count(c => versions.Any(v => v.EntryId == e.Id &&
                    (c.LeftVersionId == v.Id || c.RightVersionId == v.Id))) };
        }).ToList();
        return new Page<object>(rows, pg, size, await query.CountAsync());
    }

    static async Task<IResult> ExportRows(Guid projectId, [AsParameters] Filter filter, string? format,
        HttpContext http, Access access, HubDb db, SettingsStore settings, TimeProvider clock)
    {
        var (project, _) = await access.Project(projectId, false);
        var entries = await Query(projectId, filter, db, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime))
            .OrderBy(e => e.Seq).Take(Export.MaxRows + 1).ToListAsync();
        if (entries.Count > Export.MaxRows) throw ApiException.Rule("export_too_large", "export.too_large", null, Export.MaxRows);
        var ids = entries.Select(e => e.Id).ToArray();
        var versions = await db.DesignBasisVersions.AsNoTracking().Where(v => v.ProjectId == projectId && ids.Contains(v.EntryId))
            .ToListAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == projectId)
            .Join(db.Disciplines, pd => pd.DisciplineId, d => d.Id, (pd, d) => new { pd.Id, d.Name })
            .ToDictionaryAsync(d => d.Id, d => d.Name);
        var ownerIds = entries.Select(e => e.OwnerId).Distinct().ToArray();
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var rows = entries.SelectMany(e => versions.Where(v => v.EntryId == e.Id).OrderBy(v => v.Number)
            .Select(v => new { e.Key, e.Title, e.Kind,
                Discipline = disciplines.GetValueOrDefault(e.ProjectDisciplineId),
                Owner = owners.GetValueOrDefault(e.OwnerId), Version = v.Number, v.Status, v.Scope, v.Statement,
                v.NumericValue, v.Units, v.SourceSystem, v.StableSourceId, v.SourceUrl, v.DeclaredRevision,
                v.ConfirmationDueDate })).ToList();
        return await ExportFile.Send(db, settings, format, $"{project.ProjectNumber} design basis", ExportColumns,
            JsonSerializer.SerializeToNode(rows, JsonOpts.Web)!.AsArray(), await ListExportEndpoints.Filters(db, http, access),
            project.Id, $"{project.ProjectNumber}-design-basis", clock);
    }

    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db, SettingsStore settings)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var entry = await Entry(db, projectId, id);
        var versions = await db.DesignBasisVersions.AsNoTracking().Where(v => v.ProjectId == projectId && v.EntryId == id)
            .OrderBy(v => v.Number).ToListAsync();
        var ids = versions.Select(v => v.Id).ToArray();
        var uses = await db.BasisUses.AsNoTracking().Where(u => u.ProjectId == projectId && ids.Contains(u.VersionId)).ToListAsync();
        var currentUseIds = uses.GroupBy(u => new { u.TargetType, u.TargetId })
            .Select(g => g.OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).First().Id).ToHashSet();
        var impacts = await db.BasisImpactAssessments.AsNoTracking().Where(a => a.ProjectId == projectId && ids.Contains(a.OldVersionId)).ToListAsync();
        var conflicts = await db.BasisConflicts.AsNoTracking().Where(c => c.ProjectId == projectId &&
            (ids.Contains(c.LeftVersionId) || ids.Contains(c.RightVersionId))).ToListAsync();
        var conflictVersionIds = conflicts.SelectMany(c => new[] { c.LeftVersionId, c.RightVersionId }).Distinct().ToArray();
        var conflictVersions = await db.DesignBasisVersions.AsNoTracking().Where(v => conflictVersionIds.Contains(v.Id)).ToDictionaryAsync(v => v.Id);
        var conflictEntryIds = conflictVersions.Values.Select(v => v.EntryId).Distinct().ToArray();
        var conflictKeys = await db.DesignBasisEntries.AsNoTracking().Where(e => conflictEntryIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Key);
        ConflictSide Side(Guid versionId)
        {
            var v = conflictVersions[versionId];
            return new ConflictSide(v.Id, conflictKeys.GetValueOrDefault(v.EntryId) ?? "", v.Scope,
                v.Statement, v.NumericValue, v.Units);
        }
        var dispositions = await db.BasisAssumptionDispositions.AsNoTracking().Where(d => d.ProjectId == projectId && ids.Contains(d.VersionId)).ToListAsync();
        return new { Entry = entry, Versions = versions.Select(v => new { Version = v,
                SourceMissing = string.IsNullOrWhiteSpace(v.SourceUrl) || string.IsNullOrWhiteSpace(v.SourceSystem) || string.IsNullOrWhiteSpace(v.DeclaredRevision) }),
            Uses = uses.Select(u => new { u.Id, u.VersionId, u.TargetType, u.TargetId, u.OwnerId, u.IntendedUse,
                u.RowVersion, IsCurrent = currentUseIds.Contains(u.Id) }), Impacts = impacts, Conflicts = conflicts.Select(c => new { c.Id, c.Resolved, c.RowVersion,
                Left = Side(c.LeftVersionId), Right = Side(c.RightVersionId) }),
            Dispositions = dispositions,
            CanManage = Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId).Ok,
            CanEditProposed = Permissions.NamedCoordinationAction(access.Actor, ctx, entry.OwnerId).Ok ||
                Permissions.ManageCoordination(access.Actor, ctx, entry.ProjectDisciplineId).Ok,
            CanConfirm = Permissions.ConfirmBasis(access.Actor, ctx, entry.ProjectDisciplineId, entry.IndependentApproverId).Ok &&
                ((await settings.Get(db)).AllowSelfReview || access.Me.Id != entry.OwnerId) };
    }
}
