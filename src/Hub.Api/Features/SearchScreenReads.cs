using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// S46: optional filters over the existing search cohorts. No discovery authority or person facts reader.
public static class SearchScreenReads
{
    const int SearchScreenOptionLimit = 200;
    public sealed record SearchScreenAppliedFilters(Guid? ProjectId, string? Status, Guid? DisciplineId)
    {
        internal bool HasFilters => ProjectId is not null || Status is not null || DisciplineId is not null;
    }
    public sealed record SearchScreenTab(string Type, bool Project, bool Status, bool Discipline, string[] StatusValues);
    public sealed record SearchScreenProjectChoice(Guid Id, string ProjectNumber, string Name);
    public sealed record SearchScreenDisciplineChoice(Guid Id, string Name, bool IsActive);
    public sealed record SearchScreenMetadata(SearchScreenAppliedFilters Applied, IReadOnlyList<SearchScreenTab> Tabs,
        IReadOnlyList<SearchScreenProjectChoice> Projects, bool ProjectsTruncated,
        IReadOnlyList<SearchScreenDisciplineChoice> Disciplines, bool DisciplinesTruncated);

    static readonly SearchScreenTab[] SearchScreenTabs = [
        new("projects", true, true, true, ProjectStatus.All),
        new("tasks", true, true, true, TaskStatuses.All),
        new("deliverables", true, true, true, DeliverableStatus.All),
        new("milestones", true, true, true, MilestoneStatus.All),
        new("decisions", true, true, false, DecisionStatus.All),
        new("handoffs", true, true, true, HandoffStatus.All),
        new("reviews", true, true, true, ReviewStatus.All),
        new("changes", true, true, true, ChangeStatus.All),
        new("submissions", true, true, false, SubmissionStatus.All),
        new("allocations", true, true, false, AllocationStatus.All),
        new("planning", true, true, true, ["Self", .. PlanningVisibility.All]),
        new("design-basis", true, true, true, BasisStatus.All),
        new("constraints", true, true, true, ConstraintState.All),
        new("commitments", true, true, true, CommitmentState.All),
        new("comments", true, false, false, []),
        new("people", false, true, true, ["Active", "Inactive"])
    ];

    public static SearchScreenAppliedFilters Parse(bool enabled, string? type, string? projectId, string? status, string? disciplineId)
    {
        // New controls were previously unknown query text. Do not bind them as Guid before this gate.
        if (!enabled) return new(null, null, null);
        static Guid? Id(string? text, string field)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            Check.That(Guid.TryParse(text, out var value) && value != Guid.Empty, field, "error.invalid");
            return value;
        }
        var project = Id(projectId, "projectId"); var discipline = Id(disciplineId, "disciplineId");
        status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
        var selected = type is null ? null : SearchScreenTabs.SingleOrDefault(tab => tab.Type == type);
        if (type is not null && (project is not null || discipline is not null || status is not null))
        {
            Check.That(selected is not null, "type", "error.invalid");
            Check.That(project is null || selected!.Project, "projectId", "error.invalid");
            Check.That(discipline is null || selected!.Discipline, "disciplineId", "error.invalid");
        }
        if (status is not null)
        {
            Check.That(selected is { Status: true }, "status", "error.invalid");
            Check.OneOf(status, selected!.StatusValues, "status");
        }
        return new(project, status, discipline);
    }

    public static async Task<SearchScreenMetadata> Metadata(HubDb db, IQueryable<Project> visibleProjects, SearchScreenAppliedFilters filters)
    {
        // The caller supplies its original visible/includeArchived cohort, not a selected/term-narrowed replacement.
        var projects = await visibleProjects.AsNoTracking().OrderBy(project => project.ProjectNumber).ThenBy(project => project.Id)
            .Take(SearchScreenOptionLimit + 1).Select(project => new SearchScreenProjectChoice(project.Id, project.ProjectNumber, project.Name)).ToListAsync();
        var disciplines = await db.Disciplines.AsNoTracking().OrderBy(discipline => discipline.Name).ThenBy(discipline => discipline.Id)
            .Take(SearchScreenOptionLimit + 1).Select(discipline => new SearchScreenDisciplineChoice(discipline.Id, discipline.Name, discipline.IsActive)).ToListAsync();
        // The canonical catalog owns tab order; the mapping above owns only each tab's capabilities.
        var tabs = SearchEndpoints.Groups.Select(type => SearchScreenTabs.Single(tab => tab.Type == type)).ToArray();
        return new(filters, tabs, projects.Take(SearchScreenOptionLimit).ToArray(), projects.Count > SearchScreenOptionLimit,
            disciplines.Take(SearchScreenOptionLimit).ToArray(), disciplines.Count > SearchScreenOptionLimit);
    }

    public static IQueryable<T> StoredStatus<T>(IQueryable<T> query, SearchScreenAppliedFilters filters, string property = "Status") where T : class =>
        filters.Status is { } status ? query.Where(row => EF.Property<string>(row, property) == status) : query;

    public static IQueryable<T> ProjectDiscipline<T>(IQueryable<T> query, HubDb db, SearchScreenAppliedFilters filters) where T : ProjectItem
    {
        if (filters.DisciplineId is not { } discipline) return query;
        return query.Where(row => db.ProjectDisciplines.Any(pd => pd.Id == EF.Property<Guid?>(row, "ProjectDisciplineId")
            && pd.ProjectId == row.ProjectId && pd.DisciplineId == discipline));
    }

    public static IQueryable<Project> Projects(IQueryable<Project> query, HubDb db, SearchScreenAppliedFilters filters)
    {
        query = StoredStatus(query, filters);
        return filters.DisciplineId is { } discipline
            ? query.Where(project => db.ProjectDisciplines.Any(pd => pd.ProjectId == project.Id && pd.DisciplineId == discipline)) : query;
    }

    public static IQueryable<Milestone> Milestones(IQueryable<Milestone> query, HubDb db, SearchScreenAppliedFilters filters)
    {
        query = ProjectDiscipline(query, db, filters);
        return filters.Status switch {
            MilestoneStatus.Complete => query.Where(row => row.IsComplete),
            MilestoneStatus.Cancelled => query.Where(row => !row.IsComplete && row.IsCancelled),
            { } status => query.Where(row => !row.IsCancelled && !row.IsComplete
                && db.MilestoneStates.Any(state => state.MilestoneId == row.Id && state.Status == status)),
            _ => query
        };
    }

    public static IQueryable<Handoff> Handoffs(IQueryable<Handoff> query, HubDb db, SearchScreenAppliedFilters filters)
    {
        query = StoredStatus(query, filters);
        return filters.DisciplineId is { } discipline ? query.Where(row => db.ProjectDisciplines.Any(pd => pd.ProjectId == row.ProjectId
            && pd.DisciplineId == discipline && (pd.Id == row.SendingDisciplineId || pd.Id == row.ReceivingDisciplineId))) : query;
    }

    public static IQueryable<DesignBasisEntry> Basis(IQueryable<DesignBasisEntry> query, HubDb db, SearchScreenAppliedFilters filters)
    {
        query = ProjectDiscipline(query, db, filters);
        return filters.Status is { } status ? query.Where(row => db.DesignBasisVersions.Any(version => version.Id == row.CurrentVersionId
            && version.EntryId == row.Id && version.ProjectId == row.ProjectId && version.Status == status)) : query;
    }

    public static IQueryable<T> TargetDiscipline<T>(IQueryable<T> query, HubDb db, SearchScreenAppliedFilters filters) where T : CoordinationRecord
    {
        if (filters.DisciplineId is not { } discipline) return query;
        return query.Where(row =>
            EF.Property<string>(row, "TargetType") == "Task" && db.Tasks.Any(task => task.Id == EF.Property<Guid>(row, "TargetId")
                && task.ProjectId == row.ProjectId && db.ProjectDisciplines.Any(pd => pd.Id == task.ProjectDisciplineId
                    && pd.ProjectId == row.ProjectId && pd.DisciplineId == discipline))
            || EF.Property<string>(row, "TargetType") == "Deliverable" && db.Deliverables.Any(deliverable => deliverable.Id == EF.Property<Guid>(row, "TargetId")
                && deliverable.ProjectId == row.ProjectId && db.ProjectDisciplines.Any(pd => pd.Id == deliverable.ProjectDisciplineId
                    && pd.ProjectId == row.ProjectId && pd.DisciplineId == discipline)));
    }

    public static IQueryable<PlanningEntry> Planning(IQueryable<PlanningEntry> query, HubDb db, SearchScreenAppliedFilters filters)
    {
        if (filters.ProjectId is { } project) query = query.Where(row => row.ProjectId == project);
        if (filters.DisciplineId is { } discipline) query = query.Where(row => db.ProjectDisciplines.Any(pd => pd.Id == row.ProjectDisciplineId
            && pd.ProjectId == row.ProjectId && pd.DisciplineId == discipline));
        return filters.Status switch {
            "Self" => query.Where(row => row.CreatedBy == row.PersonId),
            { } status => query.Where(row => row.CreatedBy != row.PersonId && row.Visibility == status),
            _ => query
        };
    }

    public static IQueryable<AppUser> People(IQueryable<AppUser> query, SearchScreenAppliedFilters filters)
    {
        if (filters.DisciplineId is { } discipline) query = query.Where(user => user.HomeDisciplineId == discipline);
        return filters.Status switch {
            "Active" => query.Where(user => user.IsActive), "Inactive" => query.Where(user => !user.IsActive), _ => query
        };
    }
}
