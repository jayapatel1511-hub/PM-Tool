using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// Project templates (§12.14, FR-TPL-01/02, FR-ADM-04; packet 012). A template family has at most one Draft and numbered
/// published versions; publishing gives the Draft the next version and retires the one before it. Only Drafts change,
/// and a project copies the version it was made from, so later versions never touch it (E-12).
public static class TemplateEndpoints
{
    public sealed record HeaderBody(string Name, string? Description, Guid? ProjectTypeId);
    public sealed record DisciplineIn(Guid DisciplineId, bool IsDefaultIncluded);
    public sealed record MilestoneIn(string Ref, string Name, string MilestoneType, string Anchor, int? Offset, Guid? CompletesPhaseId, bool IsClientFacing);
    public sealed record DeliverableIn(string Ref, Guid DisciplineId, string Name, Guid DeliverableTypeId, string? MilestoneRef, int? Offset, bool RequiresReview, string? Description);
    public sealed record TaskIn(string Ref, Guid DisciplineId, string? DeliverableRef, string Name, string? Description, bool RequiresReview, string? Priority,
        decimal? EstimatedHours, int? Offset, string? AssignTo);
    public sealed record DependencyIn(string Predecessor, string Successor);
    public sealed record StructureBody(HeaderBody? Header, DisciplineIn[] Disciplines, MilestoneIn[] Milestones, DeliverableIn[] Deliverables, TaskIn[] Tasks, DependencyIn[] Dependencies, int? RowVersion);
    public sealed record PreviewBody(DateOnly? StartDate, Dictionary<Guid, DateOnly?>? MilestoneDates, Guid[]? DisciplineIds);
    public sealed record PackBody(Guid TemplateId, Guid DisciplineId, Guid? LeadUserId, Dictionary<Guid, Guid?>? Mapping);
    public sealed record VersionBody(int? RowVersion);
    public sealed record BasisSuggestionBody(Guid TemplateDisciplineId, string Kind, string Title, string Scope, string Statement,
        decimal? NumericValue, string? Units, string? SourceSystem, string? StableSourceId, string? SourceUrl, string? DeclaredRevision);

    const int MaxOffset = 3650;
    static readonly string[] Roles = [AssignToRole.DisciplineLead, AssignToRole.PM, AssignToRole.Unassigned];

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/templates", List);
        api.MapPost("/templates", async (HeaderBody body, Access access, HubDb db) =>
        {
            Editor(access);
            var t = new ProjectTemplate { FamilyId = Guid.CreateVersion7(), Status = TemplateStatus.Draft };
            await Header(t, body, db);
            db.Templates.Add(t);
            await db.SaveChangesAsync();
            return Results.Created($"/api/v1/templates/{t.Id}", new { t.Id, t.RowVersion });
        });
        api.MapGet("/templates/{id:guid}", async (Guid id, Access access, HubDb db) => await Read(await Visible(db, access, id), db, access));
        api.MapPut("/templates/{id:guid}/structure", Save);
        api.MapPost("/templates/{id:guid}/design-basis", AddBasisSuggestion);
        api.MapPost("/templates/{id:guid}/draft", NewDraft);
        api.MapPost("/templates/{id:guid}/publish", Publish);
        api.MapPost("/templates/{id:guid}/retire", async (Guid id, Access access, HubDb db) =>
        {
            Editor(access);
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_not_published", "template.not_published");
            t.Status = TemplateStatus.Retired;
            db.Audit.Note(t, action: "Retired");
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
        api.MapDelete("/templates/{id:guid}", async (Guid id, Access access, HubDb db) =>
        {
            Editor(access);
            var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
            if (t.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
            await Tx.Run(db, async () => { await ClearChildren(db, id); db.Audit.Note(t, action: "Discarded"); db.Templates.Remove(t); return await db.SaveChangesAsync(); });
            return Results.NoContent();
        });
        api.MapPost("/templates/{id:guid}/preview", async (Guid id, PreviewBody body, Access access, HubDb db) =>
        {
            var t = await Visible(db, access, id);
            var (snap, parts) = await Snapshot(db, t.Id);
            var included = body.DisciplineIds is { } ids ? parts.Disciplines.Where(d => ids.Contains(d.DisciplineId)) : parts.Disciplines.Where(d => d.IsDefaultIncluded);
            var plan = TemplatePlanner.Plan(snap, body.StartDate, body.MilestoneDates ?? [], included.Select(d => d.Id).ToHashSet());
            var names = parts.Milestones.ToDictionary(m => m.Id);
            return new
            {
                Milestones = plan.Milestones.Select(m => new { m.Id, names[m.Id].Name, names[m.Id].MilestoneType, names[m.Id].SortOrder, m.Date, m.Source }),
                Counts = new { Milestones = plan.Milestones.Count, Deliverables = plan.Deliverables.Count, Tasks = plan.Tasks.Count, Dependencies = plan.Dependencies.Count },
                Undated = new { Milestones = plan.UndatedMilestones, Deliverables = plan.UndatedDeliverables, Tasks = plan.UndatedTasks },
            };
        });

        api.MapGet("/projects/{id:guid}/template-packs", PackProposal);
        api.MapPost("/projects/{id:guid}/template-packs", AddPack);
    }

    static async Task<IResult> AddBasisSuggestion(Guid id, BasisSuggestionBody body, Access access, HubDb db)
    {
        Editor(access);
        var template = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        Check.That(template.Status == TemplateStatus.Draft, "templateId", "template.not_draft");
        Check.OneOf(body.Kind, BasisKind.All, "kind");
        Check.Required(body.Title, "title", 200);
        Check.Required(body.Scope, "scope", 500);
        Check.Required(body.Statement, "statement", 4000);
        Check.That(await db.TemplateDisciplines.AnyAsync(x => x.Id == body.TemplateDisciplineId && x.TemplateId == id),
            "templateDisciplineId", "template.bad_discipline");
        if (body.SourceUrl is not null) Coordination.Url(body.SourceUrl);
        var suggestion = new TemplateDesignBasis { TemplateId = id, TemplateDisciplineId = body.TemplateDisciplineId,
            Kind = body.Kind, Title = body.Title.Trim(), Scope = body.Scope.Trim(), Statement = body.Statement.Trim(),
            NumericValue = body.NumericValue, Units = body.Units?.Trim(), SourceSystem = body.SourceSystem?.Trim(),
            StableSourceId = body.StableSourceId?.Trim(), SourceUrl = body.SourceUrl?.Trim(), DeclaredRevision = body.DeclaredRevision?.Trim() };
        db.TemplateDesignBases.Add(suggestion);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/templates/{id}/design-basis/{suggestion.Id}", new { suggestion.Id });
    }

    // ---------- Reading ----------

    /// Editors see every version and draft; everyone else sees published templates, for the creation wizard (§8.5.1).
    static async Task<object> List(Access access, HubDb db)
    {
        var editor = IsEditor(access);
        var q = db.Templates.AsNoTracking();
        if (!editor) q = q.Where(t => t.Status == TemplateStatus.Published);
        var rows = await q.OrderBy(t => t.Name).ThenByDescending(t => t.Version).Select(t => new
        {
            t.Id, t.FamilyId, t.Name, t.Description, t.ProjectTypeId, t.Version, t.Status, t.PublishedAt, t.UpdatedAt, t.RowVersion,
            Disciplines = db.TemplateDisciplines.Count(x => x.TemplateId == t.Id), Milestones = db.TemplateMilestones.Count(x => x.TemplateId == t.Id),
            Deliverables = db.TemplateDeliverables.Count(x => x.TemplateId == t.Id), Tasks = db.TemplateTasks.Count(x => x.TemplateId == t.Id),
            Projects = db.Projects.Count(p => p.CreatedFromTemplateId == t.Id),
        }).ToListAsync();
        return new { CanEdit = editor, Templates = rows };
    }

    static async Task<ProjectTemplate> Visible(HubDb db, Access access, Guid id)
    {
        var t = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Published && !IsEditor(access)) throw ApiException.NotFound();
        return t;
    }

    public sealed record Parts(List<TemplateDiscipline> Disciplines, List<TemplateMilestone> Milestones, List<TemplateDeliverable> Deliverables, List<TemplateTask> Tasks, List<TemplateDependency> Dependencies);

    static async Task<(TemplateSnap Snap, Parts Parts)> Snapshot(HubDb db, Guid id)
    {
        var parts = new Parts(
            await db.TemplateDisciplines.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateMilestones.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateDeliverables.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateTasks.AsNoTracking().Where(x => x.TemplateId == id).OrderBy(x => x.SortOrder).ToListAsync(),
            await db.TemplateDependencies.AsNoTracking().Where(x => x.TemplateId == id).ToListAsync());
        var position = parts.Tasks.ToDictionary(k => k.Id, k => k.SortOrder); // dependencies in task order, so every read lists them the same way
        parts.Dependencies.Sort((a, b) => (position[a.PredecessorTemplateTaskId], position[a.SuccessorTemplateTaskId]).CompareTo((position[b.PredecessorTemplateTaskId], position[b.SuccessorTemplateTaskId])));
        var snap = new TemplateSnap(
            parts.Milestones.Select(m => new TplMilestone(m.Id, m.SortOrder, m.Anchor, m.OffsetDaysFromAnchor)).ToList(),
            parts.Deliverables.Select(d => new TplDeliverable(d.Id, d.TemplateDisciplineId, d.TemplateMilestoneId, d.DueOffsetDays)).ToList(),
            parts.Tasks.Select(k => new TplTask(k.Id, k.TemplateDisciplineId, k.TemplateDeliverableId, k.DueOffsetDays)).ToList(),
            parts.Dependencies.Select(d => new TplDependency(d.PredecessorTemplateTaskId, d.SuccessorTemplateTaskId)).ToList());
        return (snap, parts);
    }

    static async Task<object> Read(ProjectTemplate t, HubDb db, Access access)
    {
        var (_, p) = await Snapshot(db, t.Id);
        var disc = p.Disciplines.ToDictionary(d => d.Id, d => d.DisciplineId);
        var family = await db.Templates.AsNoTracking().Where(x => x.FamilyId == t.FamilyId).OrderByDescending(x => x.Version)
            .Select(x => new { x.Id, x.Version, x.Status, x.PublishedAt }).ToListAsync();
        return new
        {
            t.Id, t.FamilyId, t.Name, t.Description, t.ProjectTypeId, t.Version, t.Status, t.PublishedAt, t.RowVersion, CanEdit = IsEditor(access) && t.Status == TemplateStatus.Draft,
            Family = family,
            Disciplines = p.Disciplines.Select(d => new DisciplineIn(d.DisciplineId, d.IsDefaultIncluded)),
            Milestones = p.Milestones.Select(m => new MilestoneIn(m.Id.ToString(), m.Name, m.MilestoneType, m.Anchor, m.OffsetDaysFromAnchor, m.CompletesPhaseId, m.IsClientFacing)),
            Deliverables = p.Deliverables.Select(d => new DeliverableIn(d.Id.ToString(), disc[d.TemplateDisciplineId], d.Name, d.DeliverableTypeId, d.TemplateMilestoneId?.ToString(), d.DueOffsetDays, d.RequiresReview, d.Description)),
            Tasks = p.Tasks.Select(k => new TaskIn(k.Id.ToString(), disc[k.TemplateDisciplineId], k.TemplateDeliverableId?.ToString(), k.Name, k.Description, k.RequiresReview, k.Priority, k.EstimatedHours, k.DueOffsetDays, k.AssignToRole)),
            Dependencies = p.Dependencies.Select(d => new DependencyIn(d.PredecessorTemplateTaskId.ToString(), d.SuccessorTemplateTaskId.ToString())),
        };
    }

    // ---------- Editing a Draft (FR-001) ----------

    static bool IsEditor(Access access) => Permissions.ManageTemplates(access.Actor, access.Me.IsTemplateEditor).Ok;
    static void Editor(Access access) => Access.Demand(Permissions.ManageTemplates(access.Actor, access.Me.IsTemplateEditor));

    static async Task Header(ProjectTemplate t, HeaderBody body, HubDb db)
    {
        t.Name = Check.Required(body.Name, "name", 200);
        t.Description = Check.Optional(body.Description, "description", 4000);
        if (body.ProjectTypeId is { } pt) Check.That(await db.ProjectTypes.AnyAsync(x => x.Id == pt), "projectTypeId", "error.not_found");
        t.ProjectTypeId = body.ProjectTypeId;
    }

    static async Task ClearChildren(HubDb db, Guid id)
    {
        await db.TemplateDependencies.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateTasks.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateDeliverables.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateMilestones.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
        await db.TemplateDisciplines.Where(x => x.TemplateId == id).ExecuteDeleteAsync();
    }

    /// The whole structure is saved at once, so references between rows (by `ref`) are checked together.
    static async Task<object> Save(Guid id, StructureBody body, HttpContext http, Access access, HubDb db)
    {
        Editor(access);
        var t = await db.Templates.FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (t.Status != TemplateStatus.Draft) throw ApiException.Rule("template_not_draft", "template.not_draft");
        await Http.CheckVersion(db, http, t, body.RowVersion);
        if (body.Header is { } h) await Header(t, h, db);

        var refIds = new Dictionary<string, Guid>();
        Guid NewRef(string r, string field)
        {
            Check.That(!string.IsNullOrWhiteSpace(r) && !refIds.ContainsKey(r), field, "template.bad_ref");
            return refIds[r] = Guid.CreateVersion7();
        }
        var active = await db.Disciplines.Where(d => d.IsActive).Select(d => d.Id).ToListAsync();
        Check.That(body.Disciplines.Select(d => d.DisciplineId).Distinct().Count() == body.Disciplines.Length && body.Disciplines.All(d => active.Contains(d.DisciplineId)), "disciplines", "template.bad_discipline");
        var discRows = body.Disciplines.Select((d, i) => new TemplateDiscipline { Id = Guid.CreateVersion7(), TemplateId = id, DisciplineId = d.DisciplineId, IsDefaultIncluded = d.IsDefaultIncluded, SortOrder = i }).ToList();
        var byDiscipline = discRows.ToDictionary(d => d.DisciplineId, d => d.Id);
        Guid Disc(Guid disciplineId, string field) => byDiscipline.TryGetValue(disciplineId, out var x) ? x : throw ApiException.Invalid(field, "template.bad_discipline");
        static int? Off(int? o, string field) { Check.That(o is null || Math.Abs(o.Value) <= MaxOffset, field, "template.offset"); return o; }

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

        await Tx.Run(db, async () =>
        {
            await ClearChildren(db, id);
            db.TemplateDisciplines.AddRange(discRows);
            db.TemplateMilestones.AddRange(milestones);
            db.TemplateDeliverables.AddRange(deliverables);
            db.TemplateTasks.AddRange(tasks);
            db.TemplateDependencies.AddRange(deps);
            t.UpdatedAt = DateTimeOffset.UtcNow; // the header carries the draft's version even when only children changed
            db.Audit.Note(t, action: "StructureSaved");
            return await db.SaveChangesAsync();
        });
        return await Read(t, db, access);
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
    static async Task<IResult> NewDraft(Guid id, Access access, HubDb db)
    {
        Editor(access);
        var src = await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id) ?? throw ApiException.NotFound();
        if (await db.Templates.AsNoTracking().FirstOrDefaultAsync(x => x.FamilyId == src.FamilyId && x.Status == TemplateStatus.Draft) is { } existing)
            return Results.Ok(new { existing.Id, existing.RowVersion });
        var (_, p) = await Snapshot(db, id);
        var basis = await db.TemplateDesignBases.AsNoTracking().Where(x => x.TemplateId == id).ToListAsync();
        var draft = new ProjectTemplate { FamilyId = src.FamilyId, Name = src.Name, Description = src.Description, ProjectTypeId = src.ProjectTypeId, Status = TemplateStatus.Draft };
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
            db.TemplateDesignBases.AddRange(basis.Select(b => new TemplateDesignBasis { Id = Guid.CreateVersion7(), TemplateId = draft.Id,
                TemplateDisciplineId = map[b.TemplateDisciplineId], Kind = b.Kind, Title = b.Title, Scope = b.Scope, Statement = b.Statement,
                NumericValue = b.NumericValue, Units = b.Units, SourceSystem = b.SourceSystem, StableSourceId = b.StableSourceId,
                SourceUrl = b.SourceUrl, DeclaredRevision = b.DeclaredRevision }).ToList());
            db.Audit.Note(draft, action: "DraftStarted", reason: src.Version is { } v ? $"v{v}" : null);
            return await db.SaveChangesAsync();
        });
        return Results.Created($"/api/v1/templates/{draft.Id}", new { draft.Id, draft.RowVersion });
    }

    static async Task<object> Publish(Guid id, VersionBody? body, HttpContext http, Access access, HubDb db, TimeProvider clock)
    {
        Editor(access);
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
        return new { t.Id, t.Version, t.Status, t.RowVersion };
    }

    // ---------- Adding a discipline pack to a project (FR-007, FR-TPL-02) ----------

    static string Norm(string s) => s.Trim().ToLowerInvariant();

    /// Per template discipline: what it would add, and each template milestone it uses mapped to a project milestone by name.
    static async Task<object> PackProposal(Guid id, Guid templateId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(id, track: false);
        Access.Demand(Permissions.ManageTeam(access.Actor, ctx));
        var t = await Visible(db, access, templateId);
        if (t.Status != TemplateStatus.Published) throw ApiException.Rule("template_unavailable", "template.unavailable");
        var (_, p) = await Snapshot(db, t.Id);
        var projectMs = await db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsCancelled).OrderBy(m => m.SortOrder).Select(m => new { m.Id, m.Key, m.Name, m.Date }).ToListAsync();
        var have = await db.ProjectDisciplines.Where(x => x.ProjectId == id && x.IsActive).Select(x => x.DisciplineId).ToListAsync();
        return new
        {
            TemplateId = t.Id, t.Name, t.Version, ProjectMilestones = projectMs,
            Disciplines = p.Disciplines.Select(d =>
            {
                var dels = p.Deliverables.Where(x => x.TemplateDisciplineId == d.Id).ToList();
                var used = dels.Select(x => x.TemplateMilestoneId).OfType<Guid>().Distinct().ToHashSet();
                return new
                {
                    d.DisciplineId, InProject = have.Contains(d.DisciplineId), Deliverables = dels.Count, Tasks = p.Tasks.Count(k => k.TemplateDisciplineId == d.Id),
                    Mapping = p.Milestones.Where(m => used.Contains(m.Id)).Select(m => new { TemplateMilestoneId = m.Id, m.Name, ProjectMilestoneId = projectMs.FirstOrDefault(x => Norm(x.Name) == Norm(m.Name))?.Id }),
                };
            }),
        };
    }

    static async Task<object> AddPack(Guid id, PackBody body, Access access, HubDb db, TeamService team, Notifier notify, TimeProvider clock)
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
        var plan = TemplatePlanner.Plan(snap, p.StartDate, typed, new HashSet<Guid> { tdisc.Id });
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
                await notify.Send(NotificationEvents.TaskAssigned, user, TeamService.Item(p) with { Link = $"/my-work?project={p.Id}" }, Text.Get("notify.batched_assignments", n, p.ProjectNumber, p.Name));
        return new { result.Deliverables, result.Tasks, result.Dependencies };
    }

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
public sealed class TemplateHook(HubDb db, TeamService team, CurrentUser me, TimeProvider clock) : IProjectCreateHook
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
        var plan = TemplatePlanner.Plan(snap, p.StartDate, typed, pds.Keys.ToHashSet());
        var created = await TemplateEndpoints.Instantiate(db, team, p, parts, plan, pds, null, me.Id, clock.GetUtcNow(), createMilestones: true);
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
