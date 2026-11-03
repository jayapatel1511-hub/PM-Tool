using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// FR-SCR-28..32: page-local list facts and a fresh, authorized project peek.
/// Staffing reuses the Planner's read model; open roles never enter person arithmetic.
public static class ProjectListCompleteness
{
    public sealed record StaffingView(int OpenRoles, int? OverCapacityPeople, bool LimitedScope, bool PartialView,
        bool OwnPlannerOnly, int LookaheadWeeks, DateOnly From, DateOnly Through);
    public sealed record SpanView(DateOnly Today, int OpenMilestones);
    public sealed record ProgressView(DateOnly Today, int? ElapsedPct, int? ProgressPct);
    /// Independent descriptive facts only. No clamp, ratio/index between the two,
    /// health input or mutation; unknown/reversed/zero-length spans remain unavailable.
    public static ProgressView Progress(DateOnly? start, DateOnly? target, DateOnly today, int? progress)
        => new(today, start is { } from && target is { } through && through > from
            ? (int)Math.Floor((today.DayNumber - from.DayNumber) * 100m / (through.DayNumber - from.DayNumber)) : null, progress);
    public sealed record PeekLead(Guid? Id, string? Name, string State);
    public sealed record PeekDiscipline(Guid Id, string Name, PeekLead Lead, int OverdueTasks);
    public sealed record PeekView(Guid Id, string ProjectNumber, string Name, IReadOnlyList<ProjectEndpoints.ProjectMilestoneRef> Milestones,
        IReadOnlyList<PeekDiscipline> Disciplines, ProjectStatusNote.NoteView? StatusNote, bool StatusNoteEnabled, DateTimeOffset AsOf);

    sealed record Membership(Guid ProjectId, Guid UserId);

    public static async Task<List<ProjectEndpoints.ProjectRow>> Enrich(List<ProjectEndpoints.ProjectRow> rows, Access access, HubDb db,
        SettingsStore store, OrgSettings settings, TimeProvider clock, bool includeSpan = true)
    {
        if (rows.Count == 0) return rows;
        var projects = rows.Select(r => r.Id).ToArray();
        var today = clock.Today(settings); var from = Workload.WeekOf(today); var weeks = settings.StaffingLookaheadWeeks;
        var through = from.AddDays(weeks * 7 - 1);
        var milestones = includeSpan ? await db.Milestones.AsNoTracking().Where(m => projects.Contains(m.ProjectId) && !m.IsComplete && !m.IsCancelled)
            .GroupBy(m => m.ProjectId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count) : null;
        var roles = await db.OpenRoles.AsNoTracking().Where(r => projects.Contains(r.ProjectId) && r.Status == OpenRoleStatus.Open)
            .GroupBy(r => r.ProjectId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count);
        var members = await db.ProjectMembers.AsNoTracking().Where(m => projects.Contains(m.ProjectId) && m.RemovedAt == null
                && db.Users.Any(u => u.Id == m.UserId && u.IsActive))
            .Select(m => new Membership(m.ProjectId, m.UserId)).Distinct().ToListAsync();
        var memberIds = members.Select(m => m.UserId).Distinct().ToArray();
        var ownOnly = !Permissions.ViewWorkload(access.Actor).Ok && !await PeopleScope.ReadsAsUnitLeader(db, settings, access.Actor);
        var people = await PeopleScope.Widen(db, access.Actor, PlanningEndpoints.People(db, access));
        if (ownOnly) people = people.Where(p => p.Id == access.Actor.Id);
        var scoped = await people.Where(p => memberIds.Contains(p.Id)).Select(p => p.Id).Distinct().ToArrayAsync();
        var scopedSet = scoped.ToHashSet();
        var reads = new Dictionary<Guid, PlanningEndpoints.PersonRow>();
        // GridQuery's personIds limit is intentional. Batches preserve the very same
        // authorization, draft policy, calendars, allocation coverage and PLN-10 predicate.
        async Task ReadBatch(Guid[] batch)
        {
            try
            {
                var grid = await PlanningEndpoints.PreviewGrid(new(From: from, Weeks: weeks,
                    PersonId: ownOnly ? access.Actor.Id : null, PersonIds: string.Join(',', batch)), access, db, store, clock);
                foreach (var person in grid.People) reads[person.Id] = person;
            }
            catch (ApiException error) when (error.Status == 409 && error.Code == "allocation_calendar_changed")
            {
                // A confirmed allocation can lose its last eligible day after a calendar
                // change. Isolate that person's unavailable read; all other conflicts,
                // authorization failures and unexpected errors must still propagate.
                if (batch.Length <= 1) return;
                var middle = batch.Length / 2;
                await ReadBatch(batch[..middle]);
                await ReadBatch(batch[middle..]);
            }
        }
        foreach (var batch in scoped.Order().Chunk(50)) await ReadBatch(batch);
        return rows.Select(row =>
        {
            var team = members.Where(m => m.ProjectId == row.Id).Select(m => m.UserId).Distinct().ToArray();
            var visible = team.Where(scopedSet.Contains).ToArray();
            var missing = visible.Any(id => !reads.ContainsKey(id) || reads[id].Cells.Length != weeks);
            var partial = visible.Any(id => reads.TryGetValue(id, out var p) && (p.Partial || p.Cells.Any(c => c.Provisional)));
            int? over = missing ? null : visible.Count(id => reads[id].Cells.Any(c => c.OverPlanned));
            return row with {
                Staffing = new(roles.GetValueOrDefault(row.Id), over, team.Any(id => !scopedSet.Contains(id)), partial || missing, ownOnly, weeks, from, through),
                Span = includeSpan ? new(today, milestones!.GetValueOrDefault(row.Id)) : row.Span,
            };
        }).ToList();
    }

    public static async Task<Ok<PeekView>> Peek(Guid id, Access access, HubDb db, SettingsStore store, EvaluationService eval, TimeProvider clock)
    {
        var settings = await store.Get(db); Http.RequireFlag(settings, "project_screens");
        await access.Project(id, track: false);
        await eval.EnsureFresh(id);
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var (project, _) = await access.Project(id, track: false);
        var milestoneRows = await MilestoneEndpoints.Rows(db, db.Milestones.AsNoTracking().Where(m => m.ProjectId == id && !m.IsComplete && !m.IsCancelled), settings);
        var milestones = milestoneRows.OrderBy(m => m.Date ?? DateOnly.MaxValue).ThenBy(m => m.Key, StringComparer.Ordinal).ThenBy(m => m.Id).Take(3)
            .Select(m => new ProjectEndpoints.ProjectMilestoneRef(m.Id, m.Key, m.Name, m.Date, m.Status)).ToArray();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == id && d.IsActive).OrderBy(d => d.SortOrder).ThenBy(d => d.Id)
            .Select(d => new { d.Id, Name = d.Discipline!.Name, d.LeadUserId,
                Lead = db.Users.Where(u => u.Id == d.LeadUserId).Select(u => new { u.DisplayName, u.IsActive }).FirstOrDefault(),
                Overdue = db.Tasks.Count(t => t.ProjectId == id && t.ProjectDisciplineId == d.Id
                    && db.TaskStates.Any(s => s.ProjectId == id && s.TaskId == t.Id && s.IsOverdue)) }).ToListAsync();
        var noteOn = settings.IsFlagOn("project_status_note");
        return TypedResults.Ok(new PeekView(project.Id, project.ProjectNumber, project.Name, milestones,
            disciplines.Select(d => new PeekDiscipline(d.Id, d.Name, new(d.LeadUserId, d.Lead?.DisplayName,
                d.LeadUserId is null ? "Unassigned" : d.Lead is null ? "Unknown" : d.Lead.IsActive ? "Active" : "Inactive"), d.Overdue)).ToArray(),
            noteOn ? await ProjectStatusNote.Read(project, db, clock) : null, noteOn, clock.GetUtcNow()));
    }
}
