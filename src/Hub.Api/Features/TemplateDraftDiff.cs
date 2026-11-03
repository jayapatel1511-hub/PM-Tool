using System.Globalization;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record TemplateDraftSource(Guid Id, string Name, int? Version);
public sealed record TemplateDiffCounts(int Added, int Removed, int Changed);
public sealed record TemplateDiffFieldChange(string Field, string? Before, string? After);
public sealed record TemplateDraftChange(string Kind, string Change, string Ref, string? BeforeName, string? AfterName, IReadOnlyList<TemplateDiffFieldChange> Fields);
public sealed record TemplateDraftDiffRead(string State, DateTimeOffset AsOf, int DraftRowVersion, TemplateDraftSource? Source, TemplateDiffCounts? Counts,
    IReadOnlyList<TemplateDraftChange> Changes, int Page, int PageSize, int? TotalCount, IReadOnlyList<string> IncompleteSections);

/// S48: only the recorded copy event establishes identity. Display labels never establish a match.
public static class TemplateDraftDiff
{
    sealed record Item(string Kind, string Ref, string Name, Dictionary<string, string?> Fields, Dictionary<string, string?> Labels);
    sealed record Clone(string Kind, string SourceRef, string DraftRef);
    sealed record SnapshotV1(int SchemaVersion, Guid SourceId, Guid FamilyId, int? SourceVersion, string SourceName, int SourceRowVersion,
        DateTimeOffset CapturedAt, List<Item> Items, List<Clone> Clones);
    static readonly HashSet<string> mappedKinds = ["Milestone", "Deliverable", "Task", "Role", "Basis", "Risk"];
    static readonly HashSet<string> knownKinds = ["Header", "Discipline", "Dependency", "Milestone", "Deliverable", "Task", "Role", "Basis", "Risk"];
    static readonly Dictionary<string, string[]> fieldsByKind = new() {
        ["Header"] = ["name", "description", "projectTypeId", "offsetDayKind"], ["Discipline"] = ["defaultIncluded", "order"],
        ["Milestone"] = ["name", "milestoneType", "anchor", "offset", "completesPhaseId", "clientFacing", "order"],
        ["Deliverable"] = ["name", "disciplineId", "deliverableTypeId", "milestoneRef", "offset", "requiresReview", "description", "order"],
        ["Task"] = ["name", "disciplineId", "deliverableRef", "description", "requiresReview", "priority", "hours", "offset", "assignTo", "order"],
        ["Role"] = ["name", "disciplineId", "startMilestoneRef", "startOffset", "endMilestoneRef", "endOffset", "hours", "note", "order"],
        ["Basis"] = ["name", "disciplineId", "basisKind", "scope", "statement", "numericValue", "units", "sourceSystem", "stableSourceId", "sourceUrl", "declaredRevision"],
        ["Risk"] = ["name", "probability", "impact", "mitigation", "triggerIndicator", "order"], ["Dependency"] = ["predecessor", "successor"]
    };
    static readonly Dictionary<string, string> relations = new() { ["milestoneRef"] = "Milestone", ["deliverableRef"] = "Deliverable",
        ["startMilestoneRef"] = "Milestone", ["endMilestoneRef"] = "Milestone", ["predecessor"] = "Task", ["successor"] = "Task" };

    static async Task<List<Item>> Content(HubDb db, ProjectTemplate template)
    {
        var id = template.Id;
        var disciplines = await db.TemplateDisciplines.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var milestones = await db.TemplateMilestones.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var deliverables = await db.TemplateDeliverables.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var tasks = await db.TemplateTasks.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var roles = await db.TemplateRoles.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var basis = await db.TemplateDesignBases.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var risks = await TemplateRiskSeeds.Load(db, id);
        var dependencies = await db.TemplateDependencies.AsNoTracking().Where(row => row.TemplateId == id).ToListAsync();
        var global = disciplines.ToDictionary(row => row.Id, row => row.DisciplineId);
        Guid? Discipline(Guid? row) => row is null ? null : global.TryGetValue(row.Value, out var value) ? value : throw new InvalidDataException();
        var result = new List<Item>();
        void Add(string kind, string reference, string name, params (string Field, object? Value)[] values)
        {
            var fields = values.ToDictionary(pair => pair.Field, pair => pair.Value is null ? null : pair.Value is decimal number
                ? number.ToString("G29", CultureInfo.InvariantCulture) : Convert.ToString(pair.Value, CultureInfo.InvariantCulture), StringComparer.Ordinal);
            result.Add(new(kind, reference, name, fields, new(fields, StringComparer.Ordinal)));
        }
        Add("Header", "header", template.Name, ("name", template.Name), ("description", template.Description), ("projectTypeId", template.ProjectTypeId), ("offsetDayKind", template.OffsetDayKind));
        foreach (var row in disciplines) Add("Discipline", row.DisciplineId.ToString(), row.DisciplineId.ToString(), ("defaultIncluded", row.IsDefaultIncluded), ("order", row.SortOrder));
        foreach (var row in milestones) Add("Milestone", row.Id.ToString(), row.Name, ("name", row.Name), ("milestoneType", row.MilestoneType), ("anchor", row.Anchor),
            ("offset", row.OffsetDaysFromAnchor), ("completesPhaseId", row.CompletesPhaseId), ("clientFacing", row.IsClientFacing), ("order", row.SortOrder));
        foreach (var row in deliverables) Add("Deliverable", row.Id.ToString(), row.Name, ("name", row.Name), ("disciplineId", Discipline(row.TemplateDisciplineId)),
            ("deliverableTypeId", row.DeliverableTypeId), ("milestoneRef", row.TemplateMilestoneId), ("offset", row.DueOffsetDays), ("requiresReview", row.RequiresReview), ("description", row.Description), ("order", row.SortOrder));
        foreach (var row in tasks) Add("Task", row.Id.ToString(), row.Name, ("name", row.Name), ("disciplineId", Discipline(row.TemplateDisciplineId)),
            ("deliverableRef", row.TemplateDeliverableId), ("description", row.Description), ("requiresReview", row.RequiresReview), ("priority", row.Priority),
            ("hours", row.EstimatedHours), ("offset", row.DueOffsetDays), ("assignTo", row.AssignToRole), ("order", row.SortOrder));
        foreach (var row in roles) Add("Role", row.Id.ToString(), row.RoleLabel, ("name", row.RoleLabel), ("disciplineId", Discipline(row.TemplateDisciplineId)),
            ("startMilestoneRef", row.StartMilestoneId), ("startOffset", row.StartOffsetDays), ("endMilestoneRef", row.EndMilestoneId), ("endOffset", row.EndOffsetDays), ("hours", row.PlannedHours), ("note", row.Note), ("order", row.SortOrder));
        foreach (var row in basis) Add("Basis", row.Id.ToString(), row.Title, ("name", row.Title), ("disciplineId", Discipline(row.TemplateDisciplineId)),
            ("basisKind", row.Kind), ("scope", row.Scope), ("statement", row.Statement), ("numericValue", row.NumericValue), ("units", row.Units), ("sourceSystem", row.SourceSystem),
            ("stableSourceId", row.StableSourceId), ("sourceUrl", row.SourceUrl), ("declaredRevision", row.DeclaredRevision));
        foreach (var row in risks) Add("Risk", row.Id.ToString(), row.Title, ("name", row.Title), ("probability", row.Probability), ("impact", row.Impact),
            ("mitigation", row.Mitigation), ("triggerIndicator", row.TriggerIndicator), ("order", row.SortOrder));
        foreach (var row in dependencies) Add("Dependency", $"{row.PredecessorTemplateTaskId}:{row.SuccessorTemplateTaskId}", "", ("predecessor", row.PredecessorTemplateTaskId), ("successor", row.SuccessorTemplateTaskId));
        var names = result.ToDictionary(row => (row.Kind, row.Ref), row => row.Name);
        var disciplineIds = disciplines.Select(row => row.DisciplineId).ToArray();
        var disciplineNames = await db.Disciplines.AsNoTracking().Where(row => disciplineIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.Name);
        var typeIds = deliverables.Select(row => row.DeliverableTypeId).ToArray();
        var typeNames = await db.DeliverableTypes.AsNoTracking().Where(row => typeIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.Name);
        var phaseIds = milestones.Select(row => row.CompletesPhaseId).OfType<Guid>().ToArray();
        var phaseNames = await db.Phases.AsNoTracking().Where(row => phaseIds.Contains(row.Id)).ToDictionaryAsync(row => row.Id, row => row.Name);
        var projectTypeName = template.ProjectTypeId is { } projectType ? await db.ProjectTypes.AsNoTracking().Where(row => row.Id == projectType).Select(row => row.Name).SingleOrDefaultAsync() : null;
        foreach (var row in result)
        {
            if (row.Kind == "Discipline" && Guid.TryParse(row.Ref, out var disciplineId))
                names[(row.Kind, row.Ref)] = disciplineNames.GetValueOrDefault(disciplineId) ?? row.Name;
            foreach (var pair in row.Fields)
            {
                if (pair.Value is null) continue;
                if (relations.TryGetValue(pair.Key, out var kind)) row.Labels[pair.Key] = names.GetValueOrDefault((kind, pair.Value)) ?? null;
                if (Guid.TryParse(pair.Value, out var reference))
                    row.Labels[pair.Key] = pair.Key switch { "disciplineId" => disciplineNames.GetValueOrDefault(reference), "deliverableTypeId" => typeNames.GetValueOrDefault(reference),
                        "completesPhaseId" => phaseNames.GetValueOrDefault(reference), "projectTypeId" => projectTypeName, _ => row.Labels[pair.Key] };
            }
        }
        return result.Select(row => row with { Name = row.Kind == "Dependency" ? $"{row.Labels["predecessor"]} → {row.Labels["successor"]}" : names[(row.Kind, row.Ref)] }).ToList();
    }

    public static async Task Capture(HubDb db, ProjectTemplate source, ProjectTemplate draft, IReadOnlyDictionary<Guid, Guid> map, DateTimeOffset now)
    {
        List<Item> items;
        try { items = await Content(db, source); } catch (InvalidDataException) { throw ApiException.Invalid("template", "error.invalid"); }
        var clones = items.Where(row => mappedKinds.Contains(row.Kind)).Select(row => {
            if (!Guid.TryParse(row.Ref, out var id) || !map.TryGetValue(id, out var clone)) throw ApiException.Invalid("template", "error.invalid");
            return new Clone(row.Kind, row.Ref, clone.ToString());
        }).ToList();
        draft.CopiedFromTemplateId = source.Id;
        draft.CopiedFromSnapshot = JsonSerializer.Serialize(new SnapshotV1(1, source.Id, source.FamilyId, source.Version, source.Name, source.RowVersion, now, items, clones), JsonOpts.Web);
    }

    public static async Task<TemplateDraftDiffRead> Read(HubDb db, ProjectTemplate draft, OrgSettings settings, DateTimeOffset asOf, int page, int size)
    {
        var incomplete = new List<string>();
        if (!settings.IsFlagOn(TemplateRiskSeeds.Flag)) incomplete.Add("Risk");
        if (!settings.IsFlagOn("template_roles")) incomplete.Add("Role");
        if (!settings.IsFlagOn(TemplateEndpoints.WorkingDaysFlag)) incomplete.Add("OffsetDayKind");
        TemplateDraftDiffRead Unknown(string state) => new(state, asOf, draft.RowVersion, null, null, [], page, size, null, incomplete);
        if (draft.CopiedFromTemplateId is null && draft.CopiedFromSnapshot is null) return Unknown("NotRecorded");
        SnapshotV1? baseline;
        try { baseline = draft.CopiedFromSnapshot is null ? null : JsonSerializer.Deserialize<SnapshotV1>(draft.CopiedFromSnapshot, JsonOpts.Web); }
        catch (JsonException) { return Unknown("Unavailable"); }
        if (baseline is null || baseline.SchemaVersion != 1 || baseline.SourceId != draft.CopiedFromTemplateId || baseline.FamilyId != draft.FamilyId
            || baseline.SourceVersion is not > 0 || string.IsNullOrWhiteSpace(baseline.SourceName) || baseline.Items is null || baseline.Clones is null
            || baseline.Items.Any(row => row is null || !knownKinds.Contains(row.Kind) || row.Fields is null || row.Labels is null || string.IsNullOrEmpty(row.Ref)
                || !row.Fields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(row.Labels.Keys)
                || !row.Fields.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(fieldsByKind[row.Kind])
                || (mappedKinds.Contains(row.Kind) || row.Kind == "Discipline") && (!Guid.TryParse(row.Ref, out var reference) || reference == Guid.Empty))
            || baseline.Items.Count(row => row.Kind == "Header" && row.Ref == "header") != 1
            || baseline.Items.DistinctBy(row => (row.Kind, row.Ref)).Count() != baseline.Items.Count
            || baseline.Clones.Any(row => row is null || !mappedKinds.Contains(row.Kind) || !Guid.TryParse(row.SourceRef, out _) || !Guid.TryParse(row.DraftRef, out _))
            || baseline.Clones.DistinctBy(row => (row.Kind, row.SourceRef)).Count() != baseline.Clones.Count
            || baseline.Clones.DistinctBy(row => (row.Kind, row.DraftRef)).Count() != baseline.Clones.Count) return Unknown("Unavailable");
        var sourceItems = baseline.Items.ToDictionary(row => (row.Kind, row.Ref));
        if (baseline.Items.Any(row => relations.Any(relation => row.Fields.GetValueOrDefault(relation.Key) is { } value && !sourceItems.ContainsKey((relation.Value, value)))
            || row.Fields.GetValueOrDefault("disciplineId") is { } discipline && !sourceItems.ContainsKey(("Discipline", discipline))
            || row.Kind == "Dependency" && row.Ref != row.Fields["predecessor"] + ":" + row.Fields["successor"])) return Unknown("Unavailable");
        if (baseline.Clones.Any(row => !sourceItems.ContainsKey((row.Kind, row.SourceRef)))
            || sourceItems.Values.Where(row => mappedKinds.Contains(row.Kind)).Any(row => !baseline.Clones.Any(clone => clone.Kind == row.Kind && clone.SourceRef == row.Ref))) return Unknown("Unavailable");
        List<Item> current;
        try { current = await Content(db, draft); } catch (InvalidDataException) { return Unknown("Unavailable"); }
        var origins = baseline.Clones.ToDictionary(row => (row.Kind, row.DraftRef), row => row.SourceRef);
        string NormalRef(string kind, string reference) => origins.GetValueOrDefault((kind, reference)) ?? "new:" + reference;
        bool Visible(Item row) => !incomplete.Contains(row.Kind);
        Item Normalize(Item row, bool isCurrent)
        {
            var fields = new Dictionary<string, string?>(row.Fields, StringComparer.Ordinal);
            var labels = new Dictionary<string, string?>(row.Labels, StringComparer.Ordinal);
            if (row.Kind == "Header" && incomplete.Contains("OffsetDayKind")) { fields.Remove("offsetDayKind"); labels.Remove("offsetDayKind"); }
            if (isCurrent) foreach (var relation in relations) if (fields.GetValueOrDefault(relation.Key) is { } value) fields[relation.Key] = NormalRef(relation.Value, value);
            var reference = row.Kind == "Dependency" ? fields["predecessor"] + ":" + fields["successor"]
                : isCurrent && mappedKinds.Contains(row.Kind) ? NormalRef(row.Kind, row.Ref) : row.Ref;
            return row with { Ref = reference, Fields = fields, Labels = labels };
        }
        var before = baseline.Items.Where(Visible).Select(row => Normalize(row, false)).ToDictionary(row => (row.Kind, row.Ref));
        var after = current.Where(Visible).Select(row => Normalize(row, true)).ToDictionary(row => (row.Kind, row.Ref));
        var changes = new List<TemplateDraftChange>();
        foreach (var key in before.Keys.Union(after.Keys).OrderBy(key => key.Kind, StringComparer.Ordinal).ThenBy(key => key.Ref, StringComparer.Ordinal))
        {
            var old = before.GetValueOrDefault(key); var next = after.GetValueOrDefault(key);
            var fields = (old?.Fields.Keys.AsEnumerable() ?? Enumerable.Empty<string>()).Union(next?.Fields.Keys.AsEnumerable() ?? Enumerable.Empty<string>()).Order(StringComparer.Ordinal)
                .Where(field => old?.Fields.GetValueOrDefault(field) != next?.Fields.GetValueOrDefault(field))
                .Select(field => new TemplateDiffFieldChange(field, old?.Labels.GetValueOrDefault(field), next?.Labels.GetValueOrDefault(field))).ToArray();
            if (old is not null && next is not null && fields.Length == 0) continue;
            changes.Add(new(key.Kind, old is null ? "Added" : next is null ? "Removed" : "Changed", key.Ref, old?.Name, next?.Name, fields));
        }
        return new("Available", asOf, draft.RowVersion, new(baseline.SourceId, baseline.SourceName, baseline.SourceVersion),
            new(changes.Count(row => row.Change == "Added"), changes.Count(row => row.Change == "Removed"), changes.Count(row => row.Change == "Changed")),
            changes.Skip((page - 1) * size).Take(size).ToArray(), page, size, changes.Count, incomplete);
    }
}
