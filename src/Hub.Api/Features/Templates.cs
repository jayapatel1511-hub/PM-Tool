using System.Text.Json;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Project templates (§12.14, FR-TPL-01/02, FR-ADM-04; packet 012). A template family has at most one Draft and numbered
/// published versions; publishing gives the Draft the next version and retires the one before it. Only Drafts change,
/// and a project copies the version it was made from, so later versions never touch it (E-12).
public static class TemplateEndpoints
{
    public sealed record HeaderBody(string Name, string? Description, Guid? ProjectTypeId, string? OffsetDayKind = null);
    public sealed record DisciplineIn(Guid DisciplineId, bool IsDefaultIncluded);
    public sealed record MilestoneIn(string Ref, string Name, string MilestoneType, string Anchor, int? Offset, Guid? CompletesPhaseId, bool IsClientFacing);
    public sealed record DeliverableIn(string Ref, Guid DisciplineId, string Name, Guid DeliverableTypeId, string? MilestoneRef, int? Offset, bool RequiresReview, string? Description);
    public sealed record TaskIn(string Ref, Guid DisciplineId, string? DeliverableRef, string Name, string? Description, bool RequiresReview, string? Priority,
        decimal? EstimatedHours, int? Offset, string? AssignTo);
    public sealed record TemplateRoleIn(string Ref, Guid? DisciplineId, string Label, string? StartMilestoneRef, int StartOffsetDays,
        string? EndMilestoneRef, int EndOffsetDays, decimal Hours, string? Note);
    public sealed record DependencyIn(string Predecessor, string Successor);
    public sealed record StructureBody(HeaderBody? Header, DisciplineIn[] Disciplines, MilestoneIn[] Milestones, DeliverableIn[] Deliverables, TaskIn[] Tasks, DependencyIn[] Dependencies, int? RowVersion)
    {
        public TemplateRoleIn[]? Roles { get; init; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public TemplateRiskIn[]? Risks { get; init; }
    }
    public sealed record PreviewBody(DateOnly? StartDate, Dictionary<Guid, DateOnly?>? MilestoneDates, Guid[]? DisciplineIds, Guid? OfficeId = null);
    public sealed record PreviewMilestone(Guid Id, string Name, string MilestoneType, int SortOrder, DateOnly? Date, string Source);
    public sealed record PreviewCounts(int Milestones, int Deliverables, int Tasks, int Dependencies,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Roles = null);
    public sealed record PreviewUndated(int Milestones, int Deliverables, int Tasks);
    public sealed record PreviewRole(Guid Id, string Label, Guid? DisciplineId, string? DisciplineName, DateOnly? From, DateOnly? Through,
        decimal Hours, string? Note, string? SkipReason);
    public sealed record PreviewResult(IEnumerable<PreviewMilestone> Milestones, PreviewCounts Counts, PreviewUndated Undated,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<PreviewRole>? Roles = null);
    public sealed record PackBody(Guid TemplateId, Guid DisciplineId, Guid? LeadUserId, Dictionary<Guid, Guid?>? Mapping);
    public sealed record VersionBody(int? RowVersion);
    public sealed record BasisSuggestionBody(Guid TemplateDisciplineId, string Kind, string Title, string Scope, string Statement,
        decimal? NumericValue, string? Units, string? SourceSystem, string? StableSourceId, string? SourceUrl, string? DeclaredRevision);

    public sealed record TemplateCreated(Guid Id, int RowVersion);
    /// The family's Draft: the one just started (201) or the one already there (200).
    public sealed record TemplateDraft(Guid Id, int RowVersion);
    public sealed record TemplatePublished(Guid Id, int Version, string Status, int RowVersion);
    public sealed record BasisSuggestionCreated(Guid Id);
    /// One version in the list. `roles` is the template's role count, written (between tasks and projects) only while flag.template_roles is on.
    public sealed record TemplateListRow(Guid Id, Guid FamilyId, string Name, string? Description, Guid? ProjectTypeId, int? Version, string Status, DateTimeOffset? PublishedAt,
        DateTimeOffset UpdatedAt, int RowVersion, int Disciplines, int Milestones, int Deliverables, int Tasks, [property: JsonPropertyOrder(2)] int Projects,
        [property: JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Roles = null);
    public sealed record TemplateList(bool CanEdit, IReadOnlyList<TemplateListRow> Templates);
    public sealed record TemplateFamilyVersion(Guid Id, int? Version, string Status, DateTimeOffset? PublishedAt,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Projects = null);
    /// TemplateDisciplineId is what a basis suggestion is added against; saving ignores it.
    public sealed record TemplateDisciplineRead(Guid DisciplineId, bool IsDefaultIncluded, Guid TemplateDisciplineId);
    public sealed record TemplateBasisRead(Guid Id, Guid DisciplineId, string Kind, string Title, string Scope, string Statement, decimal? NumericValue, string? Units,
        string? SourceSystem, string? StableSourceId, string? SourceUrl, string? DeclaredRevision);
    /// The whole template as the editor reads and saves it. `roles` (between tasks and dependencies) is written only while flag.template_roles is on
    /// and `offsetDayKind` (last) only while flag.template_working_days is on, so with both off the document is the one before those packets.
    public sealed record TemplateRead(Guid Id, Guid FamilyId, string Name, string? Description, Guid? ProjectTypeId, int? Version, string Status, DateTimeOffset? PublishedAt,
        int RowVersion, bool CanEdit, IReadOnlyList<TemplateFamilyVersion> Family, IReadOnlyList<TemplateDisciplineRead> Disciplines, IReadOnlyList<TemplateBasisRead> BasisSuggestions,
        IReadOnlyList<MilestoneIn> Milestones, IReadOnlyList<DeliverableIn> Deliverables, IReadOnlyList<TaskIn> Tasks, [property: JsonPropertyOrder(2)] IReadOnlyList<DependencyIn> Dependencies,
        [property: JsonPropertyOrder(1), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TemplateRoleIn>? Roles = null,
        [property: JsonPropertyOrder(3), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? OffsetDayKind = null,
        [property: JsonPropertyOrder(4), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<TemplateRiskIn>? Risks = null,
        [property: JsonPropertyOrder(5), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TemplateVersionScreenRead? Screen = null);
    public sealed record PackProjectMilestone(Guid Id, string Key, string Name, DateOnly? Date);
    public sealed record PackMilestoneMap(Guid TemplateMilestoneId, string Name, Guid? ProjectMilestoneId);
    public sealed record PackDiscipline(Guid DisciplineId, bool InProject, int Deliverables, int Tasks, IReadOnlyList<PackMilestoneMap> Mapping);
    public sealed record PackProposalRead(Guid TemplateId, string Name, int Version, IReadOnlyList<PackProjectMilestone> ProjectMilestones, IReadOnlyList<PackDiscipline> Disciplines);
    public sealed record PackAdded(int Deliverables, int Tasks, int Dependencies);

    /// §49.4 (FR-RCV-10 to FR-RCV-13): the "Offsets count" setting and working-day offsets. Off, templates read, save and
    /// plan exactly as before.
    public const string WorkingDaysFlag = "template_working_days";
    const int MaxOffset = 3650;
    static readonly string[] Roles = [AssignToRole.DisciplineLead, AssignToRole.PM, AssignToRole.Unassigned];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/templates", List);
        api.MapPost("/templates", async Task<Created<TemplateCreated>> (HeaderBody body, Access access, HubDb db, SettingsStore store) =>
        {
            Editor(access);
            var t = new ProjectTemplate { FamilyId = Guid.CreateVersion7(), Status = TemplateStatus.Draft };
            await Header(t, body, db, (await store.Get(db)).IsFlagOn(WorkingDaysFlag));
            db.Templates.Add(t);
            await db.SaveChangesAsync();
            return TypedResults.Created($"/api/v1/templates/{t.Id}", new TemplateCreated(t.Id, t.RowVersion));
        });
        api.MapGet("/templates/{id:guid}", async Task<Ok<TemplateRead>> (Guid id, string? changePage, string? changePageSize,
            Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
            TypedResults.Ok(await Read(await Visible(db, access, id), db, access, store, clock, changePage, changePageSize)));
        api.MapPut("/templates/{id:guid}/structure", Save);
        api.MapPost("/templates/{id:guid}/design-basis", AddBasisSuggestion);
        api.MapDelete("/templates/{id:guid}/design-basis/{suggestionId:guid}", RemoveBasisSuggestion);
        api.MapPost("/templates/{id:guid}/draft", NewDraft);
        api.MapPost("/templates/{id:guid}/publish", Publish);
        api.MapPost("/templates/{id:guid}/retire", async Task<NoContent> (Guid id, Access access, HubDb db) =>
        {
            Editor(access);
            return await TemplateVersionProvenance.InFamily<NoContent>(db, id, async () => {
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_not_published", "template.not_published");
            t.Status = TemplateStatus.Retired;
            db.Audit.Note(t, action: "Retired");
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
            });
        });
        api.MapDelete("/templates/{id:guid}", async Task<NoContent> (Guid id, Access access, HubDb db) =>
        {
            Editor(access);
            return await TemplateVersionProvenance.InFamily<NoContent>(db, id, async () => {
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            if (t.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
            await Tx.Run(db, async () => { await ClearChildren(db, id); db.Audit.Note(t, action: "Discarded"); db.Templates.Remove(t); return await db.SaveChangesAsync(); });
            return TypedResults.NoContent();
            });
        });
        api.MapPost("/templates/{id:guid}/preview", async (Guid id, PreviewBody body, Access access, HubDb db, SettingsStore store) =>
        {
            var t = await Visible(db, access, id);
            var (snap, parts) = await Snapshot(db, t.Id);
            var included = body.DisciplineIds is { } ids ? parts.Disciplines.Where(d => ids.Contains(d.DisciplineId)) : parts.Disciplines.Where(d => d.IsDefaultIncluded);
            var plan = TemplatePlanner.Plan(snap, body.StartDate, body.MilestoneDates ?? [], included.Select(d => d.Id).ToHashSet(), await Calendar(db, store, t, body.OfficeId));
            var holidays = body.OfficeId is { } office ? await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == office).Select(h => h.Date).ToListAsync() : [];
            var rolePlan = TemplateRolePlanner.Plan(parts.Roles.Select(r => new TplRole(r.Id, r.RoleLabel, r.TemplateDisciplineId,
                r.StartMilestoneId, r.StartOffsetDays, r.EndMilestoneId, r.EndOffsetDays, r.PlannedHours, r.Note)),
                plan.Milestones.ToDictionary(m => m.Id, m => m.Date), included.Select(d => d.Id).ToHashSet(), await Calendar(db, store, t, body.OfficeId) is null ? "CalendarDays" : "WorkingDays", new WorkCalendar(holidays));
            var disciplineNames = await db.Disciplines.AsNoTracking().ToDictionaryAsync(d => d.Id, d => d.Name);
            var names = parts.Milestones.ToDictionary(m => m.Id);
            var result = new PreviewResult(
                plan.Milestones.Select(m => new PreviewMilestone(m.Id, names[m.Id].Name, names[m.Id].MilestoneType, names[m.Id].SortOrder, m.Date, m.Source)),
                new PreviewCounts(plan.Milestones.Count, plan.Deliverables.Count, plan.Tasks.Count, plan.Dependencies.Count, rolePlan.Count),
                new PreviewUndated(plan.UndatedMilestones, plan.UndatedDeliverables, plan.UndatedTasks),
                rolePlan.Select(r => new PreviewRole(r.Id, r.Label, r.DisciplineId,
                    r.DisciplineId is { } rd ? disciplineNames.GetValueOrDefault(parts.Disciplines.Single(d => d.Id == rd).DisciplineId) : null,
                    r.From, r.Through, r.Hours, r.Note, r.SkipReason)).ToArray());
            return await RolesEnabled(db) ? result : WithoutPreviewRoles(result);
        });

        api.MapGet("/projects/{id:guid}/template-packs", PackProposal);
        api.MapPost("/projects/{id:guid}/template-packs", AddPack);
        TemplateVersionProvenance.Map(api);
    }

    /// Basis suggestions (031 FR-BAS-07) change only on a Draft, one at a time; each is logged on the template, whose
    /// version moves as it does when the structure is saved. There is no edit: remove and add again.
    static async Task<Created<BasisSuggestionCreated>> AddBasisSuggestion(Guid id, BasisSuggestionBody body, Access access, HubDb db)
    {
        Editor(access);
        return await TemplateVersionProvenance.InFamily<Created<BasisSuggestionCreated>>(db, id, async () => {
        var template = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (template.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
        Check.OneOf(body.Kind, BasisKind.All, "kind");
        Check.Required(body.Title, "title", 200);
        Check.Required(body.Scope, "scope", 500);
        Check.Required(body.Statement, "statement", 4000);
        Check.That(body.NumericValue is null || !string.IsNullOrWhiteSpace(body.Units), "units", "template.basis_units");
        Check.That(await db.TemplateDisciplines.AnyAsync(x => x.Id == body.TemplateDisciplineId && x.TemplateId == id),
            "templateDisciplineId", "template.bad_discipline");
        if (body.SourceUrl is not null) Coordination.Url(body.SourceUrl);
        var suggestion = new TemplateDesignBasis { TemplateId = id, TemplateDisciplineId = body.TemplateDisciplineId,
            Kind = body.Kind, Title = body.Title.Trim(), Scope = body.Scope.Trim(), Statement = body.Statement.Trim(),
            NumericValue = body.NumericValue, Units = body.Units?.Trim(), SourceSystem = body.SourceSystem?.Trim(),
            StableSourceId = body.StableSourceId?.Trim(), SourceUrl = body.SourceUrl?.Trim(), DeclaredRevision = body.DeclaredRevision?.Trim() };
        db.TemplateDesignBases.Add(suggestion);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        db.Audit.Note(template, action: "BasisSuggestionAdded", reason: suggestion.Title);
        await db.SaveChangesAsync();
        return TypedResults.Created($"/api/v1/templates/{id}/design-basis/{suggestion.Id}", new BasisSuggestionCreated(suggestion.Id));
        });
    }

    static async Task<NoContent> RemoveBasisSuggestion(Guid id, Guid suggestionId, Access access, HubDb db)
    {
        Editor(access);
        return await TemplateVersionProvenance.InFamily<NoContent>(db, id, async () => {
        var template = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (template.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
        var suggestion = await db.TemplateDesignBases.FirstOrDefaultAsync(x => x.Id == suggestionId && x.TemplateId == id) ?? throw ApiException.NotFound();
        db.TemplateDesignBases.Remove(suggestion);
        template.UpdatedAt = DateTimeOffset.UtcNow;
        db.Audit.Note(template, action: "BasisSuggestionRemoved", reason: suggestion.Title);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
        });
    }

    static async Task<bool> RolesEnabled(HubDb db) => (await db.CurrentSettings()).IsFlagOn("template_roles");

    static PreviewResult WithoutPreviewRoles(PreviewResult result) => result with { Counts = result.Counts with { Roles = null }, Roles = null };

    // ---------- Reading ----------

    /// Editors see every version and draft; everyone else sees published templates, for the creation wizard (§8.5.1).
    static async Task<Ok<TemplateList>> List(Access access, HubDb db, SettingsStore store)
    {
        var editor = IsEditor(access);
        var scoped = (await store.Get(db)).IsFlagOn(TemplateVersionProvenance.Flag);
        await using var snapshot = scoped ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        var visible = access.VisibleProjectIds();
        var q = db.Templates.AsNoTracking();
        if (!editor) q = q.Where(t => t.Status == TemplateStatus.Published);
        var rows = await q.OrderBy(t => t.Name).ThenByDescending(t => t.Version).Select(t => new TemplateListRow(
            t.Id, t.FamilyId, t.Name, t.Description, t.ProjectTypeId, t.Version, t.Status, t.PublishedAt, t.UpdatedAt, t.RowVersion,
            db.TemplateDisciplines.Count(x => x.TemplateId == t.Id), db.TemplateMilestones.Count(x => x.TemplateId == t.Id),
            db.TemplateDeliverables.Count(x => x.TemplateId == t.Id), db.TemplateTasks.Count(x => x.TemplateId == t.Id), db.Projects.Count(p => p.CreatedFromTemplateId == t.Id && (!scoped || visible.Contains(p.Id))),
            db.TemplateRoles.Count(x => x.TemplateId == t.Id))).ToListAsync();
        return TypedResults.Ok(new TemplateList(editor, await RolesEnabled(db) ? rows : [.. rows.Select(r => r with { Roles = null })]));
    }

    internal static async Task<ProjectTemplate> Visible(HubDb db, Access access, Guid id)
    {
        var t = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Published && !IsEditor(access)) throw ApiException.NotFound();
        return t;
    }

    public sealed record Parts(List<TemplateDiscipline> Disciplines, List<TemplateMilestone> Milestones, List<TemplateDeliverable> Deliverables, List<TemplateTask> Tasks, List<TemplateDependency> Dependencies, List<TemplateRole> Roles);

    static async Task<(TemplateSnap Snap, Parts Parts)> Snapshot(HubDb db, Guid id)
    {
        var parts = new Parts(
            await db.TemplateDisciplines.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateMilestones.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateDeliverables.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateTasks.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateDependencies.AsNoTracking().Where(x => x.TemplateId == id).ToListAsync(),
            await db.TemplateRoles.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToListAsync());
        var position = parts.Tasks.ToDictionary(k => k.Id, k => k.SortOrder); // dependencies in task order, so every read lists them the same way
        parts.Dependencies.Sort((a, b) => (position[a.PredecessorTemplateTaskId], position[a.SuccessorTemplateTaskId]).CompareTo((position[b.PredecessorTemplateTaskId], position[b.SuccessorTemplateTaskId])));
        var snap = new TemplateSnap(
            parts.Milestones.Select(m => new TplMilestone(m.Id, m.SortOrder, m.Anchor, m.OffsetDaysFromAnchor)).ToList(),
            parts.Deliverables.Select(d => new TplDeliverable(d.Id, d.TemplateDisciplineId, d.TemplateMilestoneId, d.DueOffsetDays)).ToList(),
            parts.Tasks.Select(k => new TplTask(k.Id, k.TemplateDisciplineId, k.TemplateDeliverableId, k.DueOffsetDays)).ToList(),
            parts.Dependencies.Select(d => new TplDependency(d.PredecessorTemplateTaskId, d.SuccessorTemplateTaskId)).ToList());
        return (snap, parts);
    }

    /// FR-RCV-12: a working-day template counts the office's calendar (weekdays without organisation-wide and that office's
    /// holidays, whatever working_days_enabled says); a calendar-day template, or any template with the flag off, gets none.
    public static async Task<WorkCalendar?> Calendar(HubDb db, SettingsStore store, ProjectTemplate t, Guid? officeId) =>
        t.OffsetDayKind == DayKind.WorkingDays && (await store.Get(db)).IsFlagOn(WorkingDaysFlag)
            ? new WorkCalendar(await db.Holidays.AsNoTracking().Where(h => h.OfficeId == null || h.OfficeId == officeId).Select(h => h.Date).ToListAsync())
            : null;

    static async Task<TemplateRead> Read(ProjectTemplate t, HubDb db, Access access, SettingsStore store, TimeProvider clock,
        string? changePage = null, string? changePageSize = null, bool includeScreen = true)
    {
        var settings = await store.Get(db);
        var people = settings.IsFlagOn(TemplateVersionProvenance.Flag) && includeScreen;
        var risksEnabled = settings.IsFlagOn(TemplateRiskSeeds.Flag);
        await using var snapshot = includeScreen && (people || risksEnabled) && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead) : null;
        if (includeScreen && (people || risksEnabled)) t = await Visible(db, access, t.Id);
        var asOf = clock.GetUtcNow();
        var (page, size) = people ? TemplateVersionProvenance.Paging(changePage, changePageSize, "changePage", "changePageSize") : (1, 50);
        var (_, p) = await Snapshot(db, t.Id);
        var disc = p.Disciplines.ToDictionary(d => d.Id, d => d.DisciplineId);
        var familyRows = await db.Templates.AsNoTracking().Where(x => x.FamilyId == t.FamilyId).OrderByDescending(x => x.Version)
            .Select(x => new FamilyVersionRow(x.Id, x.Version, x.Status, x.PublishedAt)).ToListAsync();
        var familyIds = familyRows.Select(row => row.Id).ToArray();
        var counts = people ? await access.VisibleProjects().AsNoTracking().Where(project => project.CreatedFromTemplateId != null && familyIds.Contains(project.CreatedFromTemplateId.Value))
            .GroupBy(project => project.CreatedFromTemplateId!.Value).Select(group => new TemplateUseCount(group.Key, group.Count())).ToDictionaryAsync(row => row.Id, row => row.Count)
            : new Dictionary<Guid, int>();
        var family = familyRows.Select(row => new TemplateFamilyVersion(row.Id, row.Version, row.Status, row.PublishedAt, people ? counts.GetValueOrDefault(row.Id) : null)).ToArray();
        var basis = await db.TemplateDesignBases.AsNoTracking().Where(x => x.TemplateId == t.Id).OrderBy(x => x.Id).ToListAsync();
        return new TemplateRead(t.Id, t.FamilyId, t.Name, t.Description, t.ProjectTypeId, t.Version, t.Status, t.PublishedAt, t.RowVersion, IsEditor(access) && t.Status == TemplateStatus.Draft,
            family,
            [.. p.Disciplines.Select(d => new TemplateDisciplineRead(d.DisciplineId, d.IsDefaultIncluded, d.Id))],
            [.. basis.Select(b => new TemplateBasisRead(b.Id, disc[b.TemplateDisciplineId], b.Kind, b.Title, b.Scope, b.Statement,
                b.NumericValue, b.Units, b.SourceSystem, b.StableSourceId, b.SourceUrl, b.DeclaredRevision))],
            [.. p.Milestones.Select(m => new MilestoneIn(m.Id.ToString(), m.Name, m.MilestoneType, m.Anchor, m.OffsetDaysFromAnchor, m.CompletesPhaseId, m.IsClientFacing))],
            [.. p.Deliverables.Select(d => new DeliverableIn(d.Id.ToString(), disc[d.TemplateDisciplineId], d.Name, d.DeliverableTypeId, d.TemplateMilestoneId?.ToString(), d.DueOffsetDays, d.RequiresReview, d.Description))],
            [.. p.Tasks.Select(k => new TaskIn(k.Id.ToString(), disc[k.TemplateDisciplineId], k.TemplateDeliverableId?.ToString(), k.Name, k.Description, k.RequiresReview, k.Priority, k.EstimatedHours, k.DueOffsetDays, k.AssignToRole))],
            [.. p.Dependencies.Select(d => new DependencyIn(d.PredecessorTemplateTaskId.ToString(), d.SuccessorTemplateTaskId.ToString()))],
            await RolesEnabled(db) ? [.. p.Roles.Select(r => new TemplateRoleIn(r.Id.ToString(), r.TemplateDisciplineId is { } rd ? disc[rd] : null, r.RoleLabel, p.Milestones.FirstOrDefault(m => m.Id == r.StartMilestoneId)?.Id.ToString(), r.StartOffsetDays, p.Milestones.FirstOrDefault(m => m.Id == r.EndMilestoneId)?.Id.ToString(), r.EndOffsetDays, r.PlannedHours, r.Note))] : null,
            settings.IsFlagOn(WorkingDaysFlag) ? t.OffsetDayKind : null,
            risksEnabled ? TemplateRiskSeeds.Wire(await TemplateRiskSeeds.Load(db, t.Id)) : null,
            people ? new TemplateVersionScreenRead(counts.GetValueOrDefault(t.Id), t.Status == TemplateStatus.Draft ? await TemplateDraftDiff.Read(db, t, settings, asOf, page, size) : null) : null);
    }

    // ---------- Editing a Draft (FR-001) ----------

    static bool IsEditor(Access access) => Permissions.ManageTemplates(access.Actor, access.Me.IsTemplateEditor).Ok;
    static void Editor(Access access) => Access.Demand(Permissions.ManageTemplates(access.Actor, access.Me.IsTemplateEditor));

    static async Task Header(ProjectTemplate t, HeaderBody body, HubDb db, bool workingDays)
    {
        if (workingDays && body.OffsetDayKind is { } kind) { Check.OneOf(kind, DayKind.All, "offsetDayKind"); t.OffsetDayKind = kind; }
        t.Name = Check.Required(body.Name, "name", 200);
        t.Description = Check.Optional(body.Description, "description", 4000);
        if (body.ProjectTypeId is { } pt) Check.That(await db.ProjectTypes.AnyAsync(x => x.Id == pt), "projectTypeId", "error.not_found");
        t.ProjectTypeId = body.ProjectTypeId;
    }

    static async Task ClearChildren(HubDb db, Guid id)
    {
        await db.TemplateRisks.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateDesignBases.Where(x => x.TemplateId == id).ExecuteDeleteAsync(); // they reference the disciplines
        await db.TemplateDependencies.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateTasks.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateRoles.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateDeliverables.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateMilestones.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateDisciplines.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
    }

    /// The whole structure is saved at once, so references between rows (by `ref`) are checked together.
    static async Task<Ok<TemplateRead>> Save(Guid id, StructureBody body, HttpContext http, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Editor(access);
        return await TemplateVersionProvenance.InFamily<Ok<TemplateRead>>(db, id, async () => {
        var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
        await Http.CheckVersion(db, http, t, body.RowVersion);
        var riskSeeds = TemplateRiskSeeds.Replace(id, body.Risks, await TemplateRiskSeeds.Load(db, id), (await store.Get(db)).IsFlagOn(TemplateRiskSeeds.Flag));
        if (body.Header is { } h) await Header(t, h, db, (await store.Get(db)).IsFlagOn(WorkingDaysFlag));

        var refIds = new Dictionary<string, Guid>();
        Guid NewRef(string r, string field)
        {
            Check.That(!string.IsNullOrWhiteSpace(r) && !refIds.ContainsKey(r), field, "template.bad_ref");
            return refIds[r] = Guid.TryParse(r, out var existing) ? existing : Guid.CreateVersion7();
        }
        var active = await db.Disciplines.Where(d => d.IsActive).Select(d => d.Id).ToListAsync();
        Check.That(body.Disciplines.Select(d => d.DisciplineId).Distinct().Count() == body.Disciplines.Length && body.Disciplines.All(d => active.Contains(d.DisciplineId)), "disciplines", "template.bad_discipline");
        var discRows = body.Disciplines.Select((d, i) => new TemplateDiscipline { Id = Guid.CreateVersion7(), TemplateId = id, DisciplineId = d.DisciplineId, IsDefaultIncluded = d.IsDefaultIncluded, SortOrder = i }).ToList();
        var byDiscipline = discRows.ToDictionary(d => d.DisciplineId, d => d.Id);
        Guid Disc(Guid disciplineId, string field) => byDiscipline.TryGetValue(disciplineId, out var x) ? x : throw ApiException.Invalid(field, "template.bad_discipline");
        Guid? OptionalDisc(Guid? disciplineId, string field) => disciplineId is null ? null : Disc(disciplineId.Value, field);
        static int? Off(int? o, string field) { Check.That(o is null || o.Value >= -MaxOffset && o.Value <= MaxOffset, field, "template.offset"); return o; }

        var phases = await db.Phases.Select(x => x.Id).ToListAsync();
        var milestones = body.Milestones.Select((m, i) =>
        {
            Check.OneOf(m.MilestoneType, MilestoneType.All, "milestones");
            Check.OneOf(m.Anchor, TemplateAnchor.All, "milestones");
            Check.That(m.CompletesPhaseId is null || phases.Contains(m.CompletesPhaseId.Value), "milestones", "error.not_found");
            return new TemplateMilestone
            {
                Id = NewRef(m.Ref, "milestones"), TemplateId = id, Name = Check.Required(m.Name, "milestones", 200), MilestoneType = m.MilestoneType, SortOrder = i + 1,
                Anchor = m.Anchor, OffsetDaysFromAnchor = Off(m.Offset, "milestones"), CompletesPhaseId = m.CompletesPhaseId, IsClientFacing = m.IsClientFacing,
            };
        }).ToList();
        var types = await db.DeliverableTypes.Where(x => x.IsActive).Select(x => x.Id).ToListAsync();
        var msIds = milestones.Select(m => m.Id).ToHashSet();
        Guid? Ref(string? r, HashSet<Guid> allowed, string field) =>
            r is null ? null : refIds.TryGetValue(r, out var g) && allowed.Contains(g) ? g : throw ApiException.Invalid(field, "template.bad_ref");
        var deliverables = body.Deliverables.Select((d, i) =>
        {
            Check.That(types.Contains(d.DeliverableTypeId), "deliverables", "error.not_found");
            var disc = Disc(d.DisciplineId, "deliverables");
            var ms = Ref(d.MilestoneRef, msIds, "deliverables");
            return new TemplateDeliverable
            {
                Id = NewRef(d.Ref, "deliverables"), TemplateId = id, TemplateDisciplineId = disc, Name = Check.Required(d.Name, "deliverables", 200),
                DeliverableTypeId = d.DeliverableTypeId, TemplateMilestoneId = ms, DueOffsetDays = Off(d.Offset, "deliverables"), RequiresReview = d.RequiresReview,
                SortOrder = i + 1, Description = Check.Optional(d.Description, "deliverables", 4000),
            };
        }).ToList();
        var delIds = deliverables.Select(d => d.Id).ToHashSet();
        var tasks = body.Tasks.Select((k, i) =>
        {
            if (k.Priority is not null) Check.OneOf(k.Priority, Priority.All, "tasks");
            if (k.AssignTo is not null) Check.OneOf(k.AssignTo, Roles, "tasks");
            Check.That(k.EstimatedHours is null or >= 0 and <= 10000, "tasks", "template.estimate");
            var disc = Disc(k.DisciplineId, "tasks");
            var del = Ref(k.DeliverableRef, delIds, "tasks");
            return new TemplateTask
            {
                Id = NewRef(k.Ref, "tasks"), TemplateId = id, TemplateDisciplineId = disc, TemplateDeliverableId = del, Name = Check.Required(k.Name, "tasks", 200),
                Description = Check.Optional(k.Description, "tasks", 4000), RequiresReview = k.RequiresReview, Priority = k.Priority ?? Priority.Medium,
                EstimatedHours = k.EstimatedHours, DueOffsetDays = Off(k.Offset, "tasks"), AssignToRole = k.AssignTo ?? AssignToRole.Unassigned, SortOrder = i + 1,
            };
        }).ToList();
        var taskIds = tasks.Select(k => k.Id).ToHashSet();
        var deps = body.Dependencies.Select(d => new TemplateDependency
        {
            Id = Guid.CreateVersion7(), TemplateId = id, PredecessorTemplateTaskId = Ref(d.Predecessor, taskIds, "dependencies")!.Value, SuccessorTemplateTaskId = Ref(d.Successor, taskIds, "dependencies")!.Value,
        }).ToList();
        Check.That(deps.All(d => d.PredecessorTemplateTaskId != d.SuccessorTemplateTaskId) && deps.DistinctBy(d => (d.PredecessorTemplateTaskId, d.SuccessorTemplateTaskId)).Count() == deps.Count, "dependencies", "template.bad_dependency");
        Check.That(!HasCycle(deps), "dependencies", "template.cycle");
        var rolesEnabled = await RolesEnabled(db);
        var roles = (rolesEnabled ? body.Roles ?? [] : []).Select((r, i) =>
        {
            var discipline = OptionalDisc(r.DisciplineId, "roles");
            var start = Ref(r.StartMilestoneRef, msIds, "roles");
            var end = Ref(r.EndMilestoneRef, msIds, "roles");
            Check.That(start != null && end != null, "roles", "template.role_anchors");
            Check.That(r.StartOffsetDays is >= -MaxOffset and <= MaxOffset, "roles", "template.offset");
            Check.That(r.EndOffsetDays is >= -MaxOffset and <= MaxOffset, "roles", "template.offset");
            Check.That(r.Hours > 0 && r.Hours <= 10000, "roles", "template.role_hours");
            return new TemplateRole { Id = NewRef(r.Ref, "roles"), TemplateId = id, TemplateDisciplineId = discipline,
                RoleLabel = Check.Required(r.Label, "roles", OpenRoleLimits.Label), SortOrder = i, StartMilestoneId = start, StartOffsetDays = r.StartOffsetDays,
                EndMilestoneId = end, EndOffsetDays = r.EndOffsetDays,
                PlannedHours = r.Hours, Note = Check.Optional(r.Note, "roles", 2000) };
        }).ToList();

        if (!rolesEnabled || body.Roles is null)
        {
            var previous = await db.TemplateRoles.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync();
            var previousDisciplines = await db.TemplateDisciplines.AsNoTracking().Where(x => x.TemplateId == id).ToDictionaryAsync(x => x.Id, x => x.DisciplineId);
            // With the flag on, a client that omits roles may not orphan them. With it off the editor cannot see roles, so a
            // removed milestone or discipline only drops that reference and the save behaves as before the amendment.
            if (rolesEnabled)
                Check.That(previous.All(r => (r.TemplateDisciplineId is not { } d || previousDisciplines.TryGetValue(d, out var discipline) && byDiscipline.ContainsKey(discipline))
                    && (r.StartMilestoneId is null || msIds.Contains(r.StartMilestoneId.Value)) && (r.EndMilestoneId is null || msIds.Contains(r.EndMilestoneId.Value))), "roles", "template.roles_in_use");
            roles = previous.Select((r, i) => new TemplateRole { Id = r.Id, TemplateId = id,
                TemplateDisciplineId = r.TemplateDisciplineId is { } rd && previousDisciplines.TryGetValue(rd, out var discipline) && byDiscipline.TryGetValue(discipline, out var mapped) ? mapped : null,
                RoleLabel = r.RoleLabel, SortOrder = i, StartMilestoneId = r.StartMilestoneId is { } sm && msIds.Contains(sm) ? sm : null, StartOffsetDays = r.StartOffsetDays,
                EndMilestoneId = r.EndMilestoneId is { } em && msIds.Contains(em) ? em : null, EndOffsetDays = r.EndOffsetDays,
                PlannedHours = r.PlannedHours, Note = r.Note }).ToList();
        }

        // Basis suggestions move to their discipline's new row. Removing a discipline that still has suggestions is refused
        // until they are removed, so a save never drops one silently; one added meanwhile moves the version, refusing this save.
        var basis = await db.TemplateDesignBases.AsNoTracking().Where(x => x.TemplateId == id).ToListAsync();
        var was = await db.TemplateDisciplines.AsNoTracking().Where(x => x.TemplateId == id).ToDictionaryAsync(x => x.Id, x => x.DisciplineId);
        var stranded = basis.GroupBy(b => was[b.TemplateDisciplineId]).Where(g => !byDiscipline.ContainsKey(g.Key)).ToList();
        if (stranded.Count > 0)
        {
            var ids = stranded.Select(g => g.Key).ToList();
            var names = await db.Disciplines.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Name);
            throw new ApiException(400, "validation", Text.Get("error.validation"), new Dictionary<string, string[]>
                { ["disciplines"] = stranded.Select(g => Text.Get("template.basis_in_use", names[g.Key], g.Count())).ToArray() });
        }
        foreach (var b in basis) b.TemplateDisciplineId = byDiscipline[was[b.TemplateDisciplineId]];

        await Tx.Run(db, async () =>
        {
            await ClearChildren(db, id);
            db.TemplateDisciplines.AddRange(discRows);
            db.TemplateMilestones.AddRange(milestones);
            db.TemplateDeliverables.AddRange(deliverables);
            db.TemplateTasks.AddRange(tasks);
            db.TemplateDependencies.AddRange(deps);
            db.TemplateRoles.AddRange(roles);
            db.TemplateDesignBases.AddRange(basis);
            db.TemplateRisks.AddRange(riskSeeds);
            t.UpdatedAt = DateTimeOffset.UtcNow; // the header carries the draft's version even when only children changed
            db.Audit.Note(t, action: "StructureSaved");
            return await db.SaveChangesAsync();
        });
        return TypedResults.Ok(await Read(t, db, access, store, clock, includeScreen: false));
        });
    }

    /// Dependencies must leave the tasks orderable (Kahn); a cycle could never be scheduled.
    static bool HasCycle(List<TemplateDependency> deps)
    {
        var indegree = deps.SelectMany(d => new[] { d.PredecessorTemplateTaskId, d.SuccessorTemplateTaskId }).Distinct().ToDictionary(x => x, _ => 0);
        foreach (var d in deps) indegree[d.SuccessorTemplateTaskId]++;
        var ready = new Queue<Guid>(indegree.Where(x => x.Value == 0).Select(x => x.Key));
        var seen = 0;
        while (ready.TryDequeue(out var n))
        {
            seen++;
            foreach (var d in deps.Where(d => d.PredecessorTemplateTaskId == n))
                if (--indegree[d.SuccessorTemplateTaskId] == 0) ready.Enqueue(d.SuccessorTemplateTaskId);
        }
        return seen < indegree.Count;
    }

    /// Changing a published template starts (or returns) its family's Draft; the published version stays as it is.
    static async Task<Results<Ok<TemplateDraft>, Created<TemplateDraft>>> NewDraft(Guid id, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        Editor(access);
        return await TemplateVersionProvenance.InFamily<Results<Ok<TemplateDraft>, Created<TemplateDraft>>>(db, id, async () => {
        var src = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.FamilyId == src.FamilyId && x.Status == TemplateStatus.Draft) is { } existing)
            return TypedResults.Ok(new TemplateDraft(existing.Id, existing.RowVersion));
        var (_, p) = await Snapshot(db, id);
        var basis = await db.TemplateDesignBases.AsNoTracking().Where(x => x.TemplateId == id).ToListAsync();
        var risks = await TemplateRiskSeeds.Load(db, id);
        var draft = new ProjectTemplate { FamilyId = src.FamilyId, Name = src.Name, Description = src.Description, ProjectTypeId = src.ProjectTypeId, Status = TemplateStatus.Draft,
            OffsetDayKind = src.OffsetDayKind };
        var map = new Dictionary<Guid, Guid>();
        Guid N(Guid old) => map[old] = Guid.CreateVersion7();
        await Tx.Run(db, async () =>
        {
            db.Templates.Add(draft);
            await db.SaveChangesAsync();
            db.TemplateDisciplines.AddRange(p.Disciplines.Select(d => new TemplateDiscipline { Id = N(d.Id), TemplateId = draft.Id, DisciplineId = d.DisciplineId, SortOrder = d.SortOrder, IsDefaultIncluded = d.IsDefaultIncluded }).ToList());
            db.TemplateMilestones.AddRange(p.Milestones.Select(m => new TemplateMilestone { Id = N(m.Id), TemplateId = draft.Id, Name = m.Name, MilestoneType = m.MilestoneType, SortOrder = m.SortOrder,
                Anchor = m.Anchor, OffsetDaysFromAnchor = m.OffsetDaysFromAnchor, CompletesPhaseId = m.CompletesPhaseId, IsClientFacing = m.IsClientFacing }).ToList());
            db.TemplateDeliverables.AddRange(p.Deliverables.Select(d => new TemplateDeliverable { Id = N(d.Id), TemplateId = draft.Id, TemplateDisciplineId = map[d.TemplateDisciplineId], Name = d.Name,
                DeliverableTypeId = d.DeliverableTypeId, TemplateMilestoneId = d.TemplateMilestoneId is { } m ? map[m] : null, DueOffsetDays = d.DueOffsetDays, RequiresReview = d.RequiresReview,
                SortOrder = d.SortOrder, Description = d.Description }).ToList());
            db.TemplateTasks.AddRange(p.Tasks.Select(k => new TemplateTask { Id = N(k.Id), TemplateId = draft.Id, TemplateDisciplineId = map[k.TemplateDisciplineId],
                TemplateDeliverableId = k.TemplateDeliverableId is { } d ? map[d] : null, Name = k.Name, Description = k.Description, RequiresReview = k.RequiresReview, Priority = k.Priority,
                EstimatedHours = k.EstimatedHours, DueOffsetDays = k.DueOffsetDays, AssignToRole = k.AssignToRole, SortOrder = k.SortOrder }).ToList());
            db.TemplateDependencies.AddRange(p.Dependencies.Select(d => new TemplateDependency { Id = Guid.CreateVersion7(), TemplateId = draft.Id,
                PredecessorTemplateTaskId = map[d.PredecessorTemplateTaskId], SuccessorTemplateTaskId = map[d.SuccessorTemplateTaskId] }).ToList());
            db.TemplateRoles.AddRange(p.Roles.Select(r => new TemplateRole { Id = N(r.Id), TemplateId = draft.Id,
                TemplateDisciplineId = r.TemplateDisciplineId is { } rd ? map[rd] : null, RoleLabel = r.RoleLabel, SortOrder = r.SortOrder,
                StartMilestoneId = r.StartMilestoneId is { } sm ? map[sm] : null, StartOffsetDays = r.StartOffsetDays,
                EndMilestoneId = r.EndMilestoneId is { } em ? map[em] : null,
                EndOffsetDays = r.EndOffsetDays, PlannedHours = r.PlannedHours, Note = r.Note }).ToList());
            db.TemplateDesignBases.AddRange(basis.Select(b => new TemplateDesignBasis { Id = N(b.Id), TemplateId = draft.Id,
                TemplateDisciplineId = map[b.TemplateDisciplineId], Kind = b.Kind, Title = b.Title, Scope = b.Scope, Statement = b.Statement,
                NumericValue = b.NumericValue, Units = b.Units, SourceSystem = b.SourceSystem, StableSourceId = b.StableSourceId,
                SourceUrl = b.SourceUrl, DeclaredRevision = b.DeclaredRevision }).ToList());
            db.TemplateRisks.AddRange(risks.Select(risk => TemplateRiskSeeds.Clone(risk, draft.Id, N(risk.Id))).ToArray());
            if ((await store.Get(db)).IsFlagOn(TemplateVersionProvenance.Flag)) await TemplateDraftDiff.Capture(db, src, draft, map, clock.GetUtcNow());
            db.Audit.Note(draft, action: "DraftStarted", reason: src.Version is { } v ? $"v{v}" : null);
            return await db.SaveChangesAsync();
        });
        return TypedResults.Created($"/api/v1/templates/{draft.Id}", new TemplateDraft(draft.Id, draft.RowVersion));
        });
    }

    static async Task<Ok<TemplatePublished>> Publish(Guid id, VersionBody? body, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        Editor(access);
        return await TemplateVersionProvenance.InFamily<Ok<TemplatePublished>>(db, id, async () => {
        var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
        await Http.CheckVersion(db, http, t, body?.RowVersion);
        Check.That(await db.TemplateMilestones.AnyAsync(x => x.TemplateId == id) || await db.TemplateDeliverables.AnyAsync(x => x.TemplateId == id), "structure", "template.empty");
        await Tx.Run(db, async () =>
        {
            foreach (var old in await db.Templates.Where(x => x.FamilyId == t.FamilyId && x.Status == TemplateStatus.Published).ToListAsync())
            {
                old.Status = TemplateStatus.Retired;
                db.Audit.Note(old, action: "Superseded");
            }
            t.Version = (await db.Templates.Where(x => x.FamilyId == t.FamilyId).MaxAsync(x => x.Version) ?? 0) + 1;
            t.Status = TemplateStatus.Published;
            t.PublishedAt = clock.GetUtcNow();
            db.Audit.Note(t, action: "Published");
            return await db.SaveChangesAsync();
        });
        return TypedResults.Ok(new TemplatePublished(t.Id, t.Version ?? 0, t.Status, t.RowVersion));
        });
    }

    // ---------- Adding a discipline pack to a project (FR-007, FR-TPL-02) ----------

    static string Norm(string s) => s.Trim().ToLowerInvariant();

    /// Per template discipline: what it would add, and each template milestone it uses mapped to a project milestone by name.
    static async Task<Ok<PackProposalRead>> PackProposal(Guid id, Guid templateId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(id, track: false);
        Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
        var t = await Visible(db, access, templateId);
        if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_unavailable", "template.unavailable");
        var (_, p) = await Snapshot(db, t.Id);
        var projectMs = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsCancelled).OrderBy(m => m.SortOrder).Select(m => new PackProjectMilestone(m.Id, m.Key, m.Name, m.Date)).ToListAsync();
        var have = await db.ProjectDisciplines.Where(x => x.ProjectId == id && x.IsActive).Select(x => x.DisciplineId).ToListAsync();
        return TypedResults.Ok(new PackProposalRead(t.Id, t.Name, t.Version ?? 0, projectMs,
            [.. p.Disciplines.Select(d =>
            {
                var dels = p.Deliverables.Where(x => x.TemplateDisciplineId == d.Id).ToList();
                var used = dels.Select(x => x.TemplateMilestoneId).OfType<Guid>().Distinct().ToHashSet();
                return new PackDiscipline(d.DisciplineId, have.Contains(d.DisciplineId), dels.Count, p.Tasks.Count(k => k.TemplateDisciplineId == d.Id),
                    [.. p.Milestones.Where(m => used.Contains(m.Id)).Select(m => new PackMilestoneMap(m.Id, m.Name, projectMs.FirstOrDefault(x => Norm(x.Name) == Norm(m.Name))?.Id))]);
            })]));
    }

    static async Task<Ok<PackAdded>> AddPack(Guid id, PackBody body, Access access, HubDb db, TeamService team, Notifier notify, TimeProvider clock, SettingsStore store)
        => await Tx.Notice<Ok<PackAdded>>(db, store, async () =>
    {
        var (p, ctx) = await access.Project(id);
        Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
        var t = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == body.TemplateId) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_unavailable", "template.unavailable");
        var (snap, parts) = await Snapshot(db, t.Id);
        var tdisc = parts.Disciplines.FirstOrDefault(d => d.DisciplineId == body.DisciplineId) ?? throw ApiException.Invalid("disciplineId", "template.bad_discipline");
        var mapping = body.Mapping ?? [];
        var projectMs = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsCancelled).ToDictionaryAsync(m => m.Id);
        Check.That(mapping.Values.All(v => v is null || projectMs.ContainsKey(v.Value)), "mapping", "error.not_found");
        // Every template milestone takes its mapped project milestone's date; unmapped ones leave their items undated.
        var typed = parts.Milestones.ToDictionary(m => m.Id, m => mapping.GetValueOrDefault(m.Id) is { } pm ? projectMs[pm].Date : null);
        var plan = TemplatePlanner.Plan(snap, p.StartDate, typed, new HashSet<Guid> { tdisc.Id }, await Calendar(db, store, t, p.OfficeId));
        var result = await Tx.Run(db, async () =>
        {
            var pd = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.ProjectId == id && x.DisciplineId == body.DisciplineId);
            if (pd is null)
            {
                pd = new ProjectDiscipline { ProjectId = id, DisciplineId = body.DisciplineId, SortOrder = await db.ProjectDisciplines.CountAsync(x => x.ProjectId == id) };
                db.ProjectDisciplines.Add(pd);
                await db.SaveChangesAsync();
            }
            else pd.IsActive = true;
            if (body.LeadUserId is { } lead) await team.SetLead(p, pd, lead);
            var created = await Instantiate(db, team, p, parts, plan, new Dictionary<Guid, ProjectDiscipline> { [tdisc.Id] = pd },
                m => mapping.GetValueOrDefault(m) is { } pm ? pm : null, access.Me.Id, clock.GetUtcNow(), createMilestones: false);
            db.LogEvent(ItemType.Project, p.Id, "AddedFromTemplate", "structure", p.Id, p.ProjectNumber, p.Name,
                reason: Text.Get("template.pack_reason", t.Name, t.Version ?? 0, created.Deliverables, created.Tasks));
            await db.SaveChangesAsync();
            return created;
        });
        if (p.Status != ProjectStatus.Setup) // Setup holds assignment notices until activation (§17.5)
            foreach (var (user, n) in result.Assigned)
                await notify.Send(NotificationEvents.TaskAssigned, user, TeamService.Item(p) with { Link = $"/my-work?tab=overview&project={p.Id}" }, Text.Get("notify.batched_assignments", n, p.ProjectNumber, p.Name));
        return TypedResults.Ok(new PackAdded(result.Deliverables, result.Tasks, result.Dependencies));
    });

    public sealed record Created(int Milestones, int Deliverables, int Tasks, int Dependencies, Dictionary<Guid, int> Assigned);

    /// Copies a plan into a project with each item's template origin (§12.14 steps 3–6). Assignees resolve from roles:
    /// the discipline's lead, the PM, or no one; deliverables belong to their discipline's lead.
    public static async Task<Created> Instantiate(HubDb db, TeamService team, Project p, Parts parts, TemplatePlan plan, Dictionary<Guid, ProjectDiscipline> pds,
        Func<Guid, Guid?>? milestoneFor, Guid pm, DateTimeOffset now, bool createMilestones)
    {
        var msMap = new Dictionary<Guid, Guid>();
        if (createMilestones)
        {
            var next = await Keys.Reserve(db, p.Id, "milestone", plan.Milestones.Count);
            var tms = parts.Milestones.ToDictionary(m => m.Id);
            foreach (var pm_ in plan.Milestones)
            {
                var m = tms[pm_.Id];
                var seq = next++;
                var row = new Milestone
                {
                    ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "milestone", seq), Name = m.Name, MilestoneType = m.MilestoneType, Date = pm_.Date, OriginalDate = pm_.Date,
                    CompletesPhaseId = m.CompletesPhaseId, IsClientFacing = m.IsClientFacing, SortOrder = seq, TemplateMilestoneId = m.Id,
                };
                db.Milestones.Add(row);
                msMap[m.Id] = row.Id;
            }
        }
        Guid? Ms(Guid? tm) => tm is not { } x ? null : createMilestones ? msMap.GetValueOrDefault(x) : milestoneFor?.Invoke(x);

        var tdel = parts.Deliverables.ToDictionary(d => d.Id);
        var delMap = new Dictionary<Guid, Guid>();
        var nextD = await Keys.Reserve(db, p.Id, "deliverable", plan.Deliverables.Count);
        foreach (var pl in plan.Deliverables)
        {
            var d = tdel[pl.Id];
            var pd = pds[d.TemplateDisciplineId];
            var seq = nextD++;
            var row = new Deliverable
            {
                ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "deliverable", seq), Name = d.Name, ProjectDisciplineId = pd.Id, DeliverableTypeId = d.DeliverableTypeId,
                Description = d.Description, OwnerId = pd.LeadUserId, MilestoneId = Ms(d.TemplateMilestoneId), DueDate = pl.Due, OriginalDueDate = pl.Due,
                RequiresReview = d.RequiresReview, Priority = Priority.Medium, LastActivityAt = now, SortOrder = seq, TemplateDeliverableId = d.Id,
            };
            db.Deliverables.Add(row);
            delMap[d.Id] = row.Id;
        }

        var ttask = parts.Tasks.ToDictionary(k => k.Id);
        var taskMap = new Dictionary<Guid, Guid>();
        var assigned = new Dictionary<Guid, int>();
        var nextT = await Keys.Reserve(db, p.Id, "task", plan.Tasks.Count);
        foreach (var pl in plan.Tasks)
        {
            var k = ttask[pl.Id];
            var pd = pds[k.TemplateDisciplineId];
            Guid? assignee = k.AssignToRole switch { AssignToRole.DisciplineLead => pd.LeadUserId, AssignToRole.PM => pm, _ => null };
            var seq = nextT++;
            var row = new WorkTask
            {
                ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "task", seq), Name = k.Name, Description = k.Description, ProjectDisciplineId = pd.Id,
                DeliverableId = k.TemplateDeliverableId is { } dd ? delMap[dd] : null, AssigneeId = assignee, RequiresReview = k.RequiresReview, Priority = k.Priority,
                EstimatedHours = k.EstimatedHours, DueDate = pl.Due, OriginalDueDate = pl.Due, LastActivityAt = now, StatusChangedAt = now, SortOrder = seq, TemplateTaskId = k.Id,
            };
            db.Tasks.Add(row);
            taskMap[k.Id] = row.Id;
            if (assignee is { } a) { assigned[a] = assigned.GetValueOrDefault(a) + 1; await team.EnsureMember(p, a, ProjectRole.TeamMember); }
        }
        foreach (var d in plan.Dependencies)
            db.Dependencies.Add(new TaskDependency { ProjectId = p.Id, PredecessorTaskId = taskMap[d.Predecessor], SuccessorTaskId = taskMap[d.Successor], CreatedBy = pm, CreatedAt = now });
        return new Created(msMap.Count, delMap.Count, taskMap.Count, plan.Dependencies.Count, assigned);
    }

    /// For the create hook: the published template and its parts, or a refusal the wizard can show (US2 scenario 5).
    public static async Task<(ProjectTemplate T, TemplateSnap Snap, Parts Parts)> ForInstantiation(HubDb db, Guid templateId)
    {
        var t = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == templateId) ?? throw ApiException.Invalid("templateId", "error.not_found");
        if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_unavailable", "template.unavailable");
        var (snap, parts) = await Snapshot(db, t.Id);
        return (t, snap, parts);
    }
}

/// Creating a project from a template (§12.14, FR-004..FR-006). The wizard's chosen disciplines arrive as the project's
/// disciplines (with their leads); the template's other disciplines, and their items and dependencies, are left out.
public sealed class TemplateHook(HubDb db, TeamService team, CurrentUser me, TimeProvider clock, SettingsStore store) : IProjectCreateHook
{
    public async Task AfterCreate(Project p, ProjectEndpoints.CreateBody body)
    {
        if (body.TemplateId is not { } templateId) return;
        Check.That(body.CopyFromProjectId is null, "templateId", "template.one_source");
        var (t, snap, parts) = await TemplateEndpoints.ForInstantiation(db, templateId);
        var disciplines = parts.Disciplines;
        var projectDisciplines = await db.ProjectDisciplines.Where(x => x.ProjectId == p.Id).ToListAsync();
        var pds = disciplines.Where(d => projectDisciplines.Any(x => x.DisciplineId == d.DisciplineId))
            .ToDictionary(d => d.Id, d => projectDisciplines.First(x => x.DisciplineId == d.DisciplineId));
        var typed = new Dictionary<Guid, DateOnly?>();
        if (body.Template is { ValueKind: JsonValueKind.Object } tpl && tpl.TryGetProperty("milestoneDates", out var dates) && dates.ValueKind == JsonValueKind.Object)
            foreach (var d in dates.EnumerateObject())
            {
                Check.That(Guid.TryParse(d.Name, out var mid), "template", "template.bad_ref");
                typed[mid] = d.Value.ValueKind == JsonValueKind.Null ? null
                    : DateOnly.TryParse(d.Value.GetString(), System.Globalization.CultureInfo.InvariantCulture, out var date) ? date : throw ApiException.Invalid("template", "error.date");
            }
        var plan = TemplatePlanner.Plan(snap, p.StartDate, typed, pds.Keys.ToHashSet(), await TemplateEndpoints.Calendar(db, store, t, p.OfficeId));
        var created = await TemplateEndpoints.Instantiate(db, team, p, parts, plan, pds, null, me.Id, clock.GetUtcNow(), createMilestones: true);
        if ((await store.Get(db)).IsFlagOn(TemplateRiskSeeds.Flag))
            await TemplateRiskSeeds.Instantiate(db, p, await TemplateRiskSeeds.Load(db, t.Id), clock.GetUtcNow());
        if ((await store.Get(db)).IsFlagOn("template_roles"))
        {
            var calendarFactory = await OpenRoleEndpoints.Calendars(db, [p.Id]);
            var rolePlan = TemplateRolePlanner.Plan(parts.Roles.Select(r => new TplRole(r.Id, r.RoleLabel, r.TemplateDisciplineId, r.StartMilestoneId,
                r.StartOffsetDays, r.EndMilestoneId, r.EndOffsetDays, r.PlannedHours, r.Note)),
                plan.Milestones.ToDictionary(m => m.Id, m => m.Date), pds.Keys.ToHashSet(), await TemplateEndpoints.Calendar(db, store, t, p.OfficeId) is null ? "CalendarDays" : "WorkingDays", calendarFactory(p.Id));
            foreach (var role in rolePlan.Where(x => x.SkipReason is null))
            {
                var disciplineId = role.DisciplineId is { } discipline ? pds[discipline].Id : (Guid?)null;
                var open = new OpenRole { ProjectId = p.Id, ProjectDisciplineId = disciplineId, RoleLabel = role.Label,
                    FromDate = role.From!.Value, ThroughDate = role.Through!.Value, PlannedHours = role.Hours, Note = role.Note };
                db.OpenRoles.Add(open);
                db.Audit.Note(open, action: "Created", reason: "TemplateRole");
            }
        }
        var basisSuggestions = await db.TemplateDesignBases.AsNoTracking().Where(x => x.TemplateId == t.Id).ToListAsync();
        if (basisSuggestions.Count > 0)
        {
            var nextBasis = await Keys.Reserve(db, p.Id, "basis", basisSuggestions.Count);
            foreach (var suggestion in basisSuggestions)
            {
                if (!pds.TryGetValue(suggestion.TemplateDisciplineId, out var pd)) continue;
                var entry = new DesignBasisEntry { ProjectId = p.Id, Kind = suggestion.Kind, Title = suggestion.Title,
                    OwnerId = pd.LeadUserId ?? me.Id, ProjectDisciplineId = pd.Id };
                (entry.Seq, entry.Key) = (nextBasis++, Keys.Format(p.ProjectNumber, "basis", nextBasis - 1));
                db.DesignBasisEntries.Add(entry);
                db.DesignBasisVersions.Add(new DesignBasisVersion { ProjectId = p.Id, EntryId = entry.Id, Number = 1,
                    Status = BasisStatus.Proposed, Scope = suggestion.Scope, Statement = suggestion.Statement,
                    NumericValue = suggestion.NumericValue, Units = suggestion.Units, SourceSystem = suggestion.SourceSystem,
                    StableSourceId = suggestion.StableSourceId, SourceUrl = suggestion.SourceUrl, DeclaredRevision = suggestion.DeclaredRevision });
            }
        }
        p.CreatedFromTemplateId = t.Id;
        p.TemplateVersion = t.Version;
        db.Audit.Note(p, action: "CreatedFromTemplate", reason: Text.Get("template.created_reason", t.Name, t.Version ?? 0, created.Milestones, created.Deliverables, created.Tasks, created.Dependencies));
        await db.SaveChangesAsync();
    }
}

file sealed record FamilyVersionRow(Guid Id, int? Version, string Status, DateTimeOffset? PublishedAt);
file sealed record TemplateUseCount(Guid Id, int Count);
