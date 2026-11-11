using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record FileScreenRead(DateTimeOffset AsOf, string OrgTimeZone, DateOnly WeekFrom, DateOnly WeekThrough, FileScreenApplied Applied,
    IReadOnlyList<FileScreenDisciplineChoice> Disciplines, bool DisciplinesTruncated, IReadOnlyList<FileScreenAuthorChoice> Authors, bool AuthorsTruncated,
    IReadOnlyList<FileScreenLinkContext> Links, FileScreenMissingDeliverables MissingDeliverables);
public sealed record FileScreenApplied(Guid? DisciplineId, Guid? AddedById, bool AddedThisWeek);
public sealed record FileScreenDisciplineChoice(Guid Id, string Name, bool IsActive);
public sealed record FileScreenAuthorChoice(Guid Id, string DisplayName, bool IsActive);
public sealed record FileScreenLinkContext(Guid LinkId, int RowVersion, bool CanChange, FileScreenDisciplineChoice? Discipline,
    DocumentLinkReferenceContext ReferenceContext);
public sealed record FileScreenMissingDeliverables(int Total, bool Truncated, IReadOnlyList<FileScreenMissingDeliverable> Items);
public sealed record FileScreenMissingDeliverable(Guid Id, Guid ProjectId, string ProjectNumber, string ProjectName, string Key, string Name, string Status,
    Guid DisciplineId, string DisciplineName, string? CurrentRevision, int RowVersion, bool CanAddLink);

/// <summary>S44 / FR-SCR-77: a scoped link library read, not an upload, delivery or source-registration command.</summary>
public static class FileScreenReads
{
    const int LinkLimit = 2000, MissingLimit = 2000, ChoiceLimit = 200;
    sealed record OwnerLabel(string Kind, Guid Id, Guid ProjectId, string Key, string Name, Guid ProjectDisciplineId);
    sealed record MemberContext(Guid ProjectId, List<string> Roles, Guid? PrimaryDisciplineId);
    sealed record LeadContext(Guid ProjectId, Guid Id);
    sealed record OwnerDiscipline(Guid Id, Guid ProjectId, FileScreenDisciplineChoice Discipline);

    static FileScreenApplied Parse(string? disciplineId, string? addedById, string? addedThisWeek)
    {
        static Guid? Id(string? value, string field)
        {
            if (string.IsNullOrEmpty(value)) return null;
            Check.That(Guid.TryParse(value, out var id) && id != Guid.Empty, field, "error.invalid");
            return id;
        }
        Check.That(string.IsNullOrEmpty(addedThisWeek) || addedThisWeek is "true" or "false", "addedThisWeek", "error.invalid");
        return new(Id(disciplineId, "disciplineId"), Id(addedById, "addedById"), addedThisWeek == "true");
    }

    public static async Task<WorkspaceEndpoints.FilesRead> Read(string? projects, string? search, string? type,
        string? disciplineId, string? addedById, string? addedThisWeek, Access access, HubDb db, OrgSettings settings, TimeProvider clock)
    {
        Http.RequireFlag(settings, DocumentLinkReferences.Flag);
        var applied = Parse(disciplineId, addedById, addedThisWeek);
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var asOf = clock.GetUtcNow();
        var zone = Clock.Zone(settings);
        var today = Clock.LocalDate(asOf, settings);
        var weekFrom = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekThrough = weekFrom.AddDays(6);
        var fromUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekFrom.ToDateTime(TimeOnly.MinValue), zone));
        var untilUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(weekFrom.AddDays(7).ToDateTime(TimeOnly.MinValue), zone));
        var list = await Scope.Projects(access, db, projects).AsNoTracking().OrderBy(p => p.ProjectNumber).ThenBy(p => p.Id).ToListAsync();
        var pids = list.Select(p => p.Id).ToArray();

        // Visibility and actual owner/project coherence precede every count, choice and cap.
        // Soft-deleted links/owners remain excluded by the maintained query filters.
        var coherent = db.DocumentLinks.AsNoTracking().Where(link => pids.Contains(link.ProjectId)
            && (link.ItemType == ItemType.Project && link.ItemId == link.ProjectId
                || link.ItemType == ItemType.Task && db.Tasks.Any(task => task.Id == link.ItemId && task.ProjectId == link.ProjectId)
                || link.ItemType == ItemType.Deliverable && db.Deliverables.Any(deliverable => deliverable.Id == link.ItemId && deliverable.ProjectId == link.ProjectId)));

        // Choices describe the scoped cohort, independent of search/type/author/week selection.
        // Task's own discipline is used; a task's parent deliverable never supplies it.
        var representedDisciplines = db.ProjectDisciplines.AsNoTracking().Where(pd => pids.Contains(pd.ProjectId)
            && (db.Tasks.Any(task => task.ProjectId == pd.ProjectId && task.ProjectDisciplineId == pd.Id)
                || db.Deliverables.Any(deliverable => deliverable.ProjectId == pd.ProjectId && deliverable.ProjectDisciplineId == pd.Id)))
            .Select(pd => pd.DisciplineId);
        var disciplines = await db.Disciplines.AsNoTracking().Where(d => representedDisciplines.Contains(d.Id))
            .OrderBy(d => d.Name).ThenBy(d => d.Id).Take(ChoiceLimit + 1)
            .Select(d => new FileScreenDisciplineChoice(d.Id, d.Name, d.IsActive)).ToListAsync();
        var authors = await db.Users.AsNoTracking().Where(user => coherent.Any(link => link.AddedBy == user.Id))
            .OrderBy(user => user.DisplayName).ThenBy(user => user.Id).Take(ChoiceLimit + 1)
            .Select(user => new FileScreenAuthorChoice(user.Id, user.DisplayName, user.IsActive)).ToListAsync();

        var filtered = coherent;
        if (!string.IsNullOrWhiteSpace(type)) filtered = filtered.Where(link => link.LinkType == type);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            filtered = filtered.Where(link => EF.Functions.ILike(link.Title, term) || EF.Functions.ILike(link.Url, term));
        }
        if (applied.DisciplineId is { } did)
            filtered = filtered.Where(link => link.ItemType == ItemType.Task && db.Tasks.Any(task => task.Id == link.ItemId && task.ProjectId == link.ProjectId
                    && db.ProjectDisciplines.Any(pd => pd.Id == task.ProjectDisciplineId && pd.ProjectId == task.ProjectId && pd.DisciplineId == did))
                || link.ItemType == ItemType.Deliverable && db.Deliverables.Any(deliverable => deliverable.Id == link.ItemId && deliverable.ProjectId == link.ProjectId
                    && db.ProjectDisciplines.Any(pd => pd.Id == deliverable.ProjectDisciplineId && pd.ProjectId == deliverable.ProjectId && pd.DisciplineId == did)));
        if (applied.AddedById is { } uid) filtered = filtered.Where(link => link.AddedBy == uid);
        if (applied.AddedThisWeek) filtered = filtered.Where(link => link.AddedAt >= fromUtc && link.AddedAt < untilUtc && link.AddedAt <= asOf);
        var bounded = await filtered.OrderBy(link => link.Title).ThenBy(link => link.Id).Take(LinkLimit + 1).ToListAsync();
        var shown = bounded.Take(LinkLimit).ToArray();

        var taskIds = shown.Where(link => link.ItemType == ItemType.Task).Select(link => link.ItemId).Distinct().ToArray();
        var deliverableIds = shown.Where(link => link.ItemType == ItemType.Deliverable).Select(link => link.ItemId).Distinct().ToArray();
        var owners = (await db.Tasks.AsNoTracking().Where(task => pids.Contains(task.ProjectId) && taskIds.Contains(task.Id))
                .Select(task => new OwnerLabel(ItemType.Task, task.Id, task.ProjectId, task.Key, task.Name, task.ProjectDisciplineId)).ToListAsync())
            .Concat(await db.Deliverables.AsNoTracking().Where(deliverable => pids.Contains(deliverable.ProjectId) && deliverableIds.Contains(deliverable.Id))
                .Select(deliverable => new OwnerLabel(ItemType.Deliverable, deliverable.Id, deliverable.ProjectId, deliverable.Key, deliverable.Name, deliverable.ProjectDisciplineId)).ToListAsync())
            .ToDictionary(owner => (owner.Kind, owner.Id, owner.ProjectId));
        var ownerPdIds = owners.Values.Select(owner => owner.ProjectDisciplineId).Distinct().ToArray();
        var ownerDisciplines = await (from pd in db.ProjectDisciplines.AsNoTracking()
            join discipline in db.Disciplines.AsNoTracking() on pd.DisciplineId equals discipline.Id
            where pids.Contains(pd.ProjectId) && ownerPdIds.Contains(pd.Id)
            select new OwnerDiscipline(pd.Id, pd.ProjectId, new FileScreenDisciplineChoice(discipline.Id, discipline.Name, discipline.IsActive))).ToDictionaryAsync(row => row.Id);
        var authorIds = shown.Select(link => link.AddedBy).Distinct().ToArray();
        var actualAuthors = await db.Users.AsNoTracking().Where(user => authorIds.Contains(user.Id))
            .Select(user => new WorkspaceEndpoints.WorkspaceFileUserReference(user.Id, user.DisplayName, user.IsActive)).ToDictionaryAsync(user => user.Id);

        // Exactly the member/lead inputs used by Access.Context, loaded in two batches.
        // Do not add an active-discipline predicate: the canonical context does not have one.
        var members = await db.ProjectMembers.AsNoTracking().Where(member => pids.Contains(member.ProjectId) && member.UserId == access.Me.Id && member.RemovedAt == null)
            .Select(member => new MemberContext(member.ProjectId, member.Roles, member.PrimaryDisciplineId)).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(pd => pids.Contains(pd.ProjectId) && pd.LeadUserId == access.Me.Id)
            .Select(pd => new LeadContext(pd.ProjectId, pd.Id)).ToListAsync();
        var membersByProject = members.GroupBy(row => row.ProjectId).ToDictionary(group => group.Key, group => group.First());
        var leadsByProject = leads.GroupBy(row => row.ProjectId).ToDictionary(group => group.Key, group => group.Select(row => row.Id).ToHashSet());
        var contexts = list.ToDictionary(project => project.Id, project => {
            var member = membersByProject.GetValueOrDefault(project.Id);
            return new ProjectContext(project.Id, project.Status, project.Visibility, project.ProjectManagerId, project.AllowViewerComments,
                (member?.Roles ?? []).ToHashSet(), leadsByProject.GetValueOrDefault(project.Id) ?? new HashSet<Guid>(), member?.PrimaryDisciplineId);
        });
        var references = await DocumentLinkReferences.ReadContexts(db, shown, pids);
        var metadata = new List<FileScreenLinkContext>();
        foreach (var link in shown)
        {
            FileScreenDisciplineChoice? discipline = null;
            if (owners.TryGetValue((link.ItemType, link.ItemId, link.ProjectId), out var owner)
                && ownerDisciplines.TryGetValue(owner.ProjectDisciplineId, out var pd) && pd.ProjectId == link.ProjectId)
                discipline = pd.Discipline;
            metadata.Add(new(link.Id, link.RowVersion, DocumentLinkEndpoints.CanChange(link, access.Actor, contexts[link.ProjectId]), discipline, references[link.Id]));
        }

        var result = new List<WorkspaceEndpoints.FileProjectRow>();
        foreach (var project in list)
        {
            var own = shown.Where(link => link.ProjectId == project.Id).ToArray();
            var canAdd = Permissions.EditProject(access.Actor, contexts[project.Id]).Ok;
            if (own.Length == 0 && !canAdd) continue;
            var items = own.GroupBy(link => (link.ItemType, link.ItemId))
                .OrderBy(group => group.Key.ItemType == ItemType.Project ? 0 : 1)
                .ThenBy(group => owners.GetValueOrDefault((group.Key.ItemType, group.Key.ItemId, project.Id))?.Key).ThenBy(group => group.Key.ItemId)
                .Select(group => {
                    var owner = owners.GetValueOrDefault((group.Key.ItemType, group.Key.ItemId, project.Id));
                    return new WorkspaceEndpoints.FileItemRow(group.Key.ItemType, group.Key.ItemId, owner?.Key ?? project.ProjectNumber, owner?.Name ?? project.Name,
                        group.Select(link => {
                            var author = actualAuthors.GetValueOrDefault(link.AddedBy);
                            return new WorkspaceEndpoints.FileLinkRow(link.Id, link.Title, link.Url, link.LinkType, Links.IsUnc(link.Url), author?.DisplayName, link.AddedAt, author);
                        }).ToList());
                }).ToList();
            result.Add(new(project.Id, project.ProjectNumber, project.Name, canAdd, own.Length, items,
                settings.IsFlagOn("visual_system") ? project.IdentityColour : null));
        }

        // Independent direct-live-link anti-join: none of the link predicates above enter it.
        var missing = from deliverable in db.Deliverables.AsNoTracking()
            join project in db.Projects.AsNoTracking() on deliverable.ProjectId equals project.Id
            join pd in db.ProjectDisciplines.AsNoTracking() on deliverable.ProjectDisciplineId equals pd.Id
            join discipline in db.Disciplines.AsNoTracking() on pd.DisciplineId equals discipline.Id
            where pids.Contains(project.Id) && pd.ProjectId == project.Id
                && deliverable.Status != DeliverableStatus.Issued && deliverable.Status != DeliverableStatus.Accepted && deliverable.Status != DeliverableStatus.Cancelled
                && !db.DocumentLinks.Any(link => link.ProjectId == deliverable.ProjectId && link.ItemType == ItemType.Deliverable && link.ItemId == deliverable.Id)
            // Keep server-filtered/ordered members in a member-init projection until materialization.
            select new MissingDeliverableRow { Id = deliverable.Id, ProjectId = project.Id, ProjectNumber = project.ProjectNumber, ProjectName = project.Name,
                Key = deliverable.Key, Name = deliverable.Name, Status = deliverable.Status, DisciplineId = discipline.Id, DisciplineName = discipline.Name,
                CurrentRevision = deliverable.Revision, RowVersion = deliverable.RowVersion };
        if (applied.DisciplineId is { } missingDiscipline) missing = missing.Where(row => row.DisciplineId == missingDiscipline);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            missing = missing.Where(row => EF.Functions.ILike(row.Key, term) || EF.Functions.ILike(row.Name, term));
        }
        var missingTotal = await missing.CountAsync();
        var missingRows = await missing.OrderBy(row => row.ProjectNumber).ThenBy(row => row.Key).ThenBy(row => row.Id).Take(MissingLimit + 1).ToListAsync();
        var missingItems = missingRows.Take(MissingLimit).Select(row => new FileScreenMissingDeliverable(row.Id, row.ProjectId, row.ProjectNumber, row.ProjectName,
            row.Key, row.Name, row.Status, row.DisciplineId, row.DisciplineName, string.IsNullOrWhiteSpace(row.CurrentRevision) ? null : row.CurrentRevision.Trim(), row.RowVersion,
            DocumentLinkEndpoints.CanAdd(ItemType.Deliverable, access.Actor, contexts[row.ProjectId]).Ok)).ToArray();
        var screen = new FileScreenRead(asOf, zone.Id, weekFrom, weekThrough, applied, disciplines.Take(ChoiceLimit).ToArray(), disciplines.Count > ChoiceLimit,
            authors.Take(ChoiceLimit).ToArray(), authors.Count > ChoiceLimit, metadata,
            new(missingTotal, missingRows.Count > MissingLimit, missingItems));
        return new(result, shown.Length, bounded.Count > LinkLimit, LinkType.All, screen);
    }
}

file sealed class MissingDeliverableRow
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string ProjectNumber { get; init; } = "";
    public string ProjectName { get; init; } = "";
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public Guid DisciplineId { get; init; }
    public string DisciplineName { get; init; } = "";
    public string? CurrentRevision { get; init; }
    public int RowVersion { get; init; }
}
