using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

/// §27.1: a new project may start from another project's structure: its disciplines, milestones without dates,
/// deliverables and tasks without people or dates, and the dependencies between them. Assignments, dates, comments and
/// history are never copied; cancelled items are left behind.
public sealed class CopyStructureHook(HubDb db, Access access, TimeProvider clock) : IProjectCreateHook
{
    public async Task AfterCreate(Project p, ProjectEndpoints.CreateBody body)
    {
        if (body.CopyFromProjectId is not { } srcId) return;
        var src = await access.VisibleProjects().AsNoTracking().FirstOrDefaultAsync(x => x.Id == srcId) ?? throw ApiException.Invalid("copyFromProjectId", "error.not_found");
        var now = clock.GetUtcNow();
        var reason = Text.Get("copy.reason", src.ProjectNumber);
        T Copied<T>(T e) where T : class { db.Audit.Note(e, action: "Copied", reason: reason); return e; }

        var disciplines = await db.ProjectDisciplines.Where(x => x.ProjectId == p.Id).ToListAsync();
        var pdMap = new Dictionary<Guid, Guid>();
        foreach (var s in await db.ProjectDisciplines.AsNoTracking().Where(x => x.ProjectId == srcId && x.IsActive).OrderBy(x => x.SortOrder).ToListAsync())
        {
            var mine = disciplines.FirstOrDefault(x => x.DisciplineId == s.DisciplineId);
            if (mine is null)
            {
                mine = Copied(new ProjectDiscipline { ProjectId = p.Id, DisciplineId = s.DisciplineId, SortOrder = disciplines.Count });
                db.ProjectDisciplines.Add(mine);
                disciplines.Add(mine);
            }
            pdMap[s.Id] = mine.Id;
        }

        var milestones = await db.Milestones.AsNoTracking().Where(x => x.ProjectId == srcId && !x.IsCancelled).OrderBy(x => x.Date).ThenBy(x => x.SortOrder).ToListAsync();
        var msMap = new Dictionary<Guid, Guid>();
        var next = await Keys.Reserve(db, p.Id, "milestone", milestones.Count);
        foreach (var m in milestones)
        {
            var seq = next++;
            var copy = Copied(new Milestone
            {
                ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "milestone", seq), Name = m.Name, MilestoneType = m.MilestoneType, Description = m.Description,
                ProjectDisciplineId = m.ProjectDisciplineId is { } mpd && pdMap.TryGetValue(mpd, out var npd) ? npd : null, CompletesPhaseId = m.CompletesPhaseId,
                IsClientFacing = m.IsClientFacing, SortOrder = m.SortOrder,
            });
            db.Milestones.Add(copy);
            msMap[m.Id] = copy.Id;
        }

        var srcPds = pdMap.Keys.ToList();
        var deliverables = await db.Deliverables.AsNoTracking().Where(x => x.ProjectId == srcId && x.Status != DeliverableStatus.Cancelled && srcPds.Contains(x.ProjectDisciplineId))
            .OrderBy(x => x.Seq).ToListAsync();
        var dMap = new Dictionary<Guid, Guid>();
        next = await Keys.Reserve(db, p.Id, "deliverable", deliverables.Count);
        foreach (var d in deliverables)
        {
            var seq = next++;
            var copy = Copied(new Deliverable
            {
                ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "deliverable", seq), Name = d.Name, ProjectDisciplineId = pdMap[d.ProjectDisciplineId],
                DeliverableTypeId = d.DeliverableTypeId, Description = d.Description, MilestoneId = d.MilestoneId is { } dm && msMap.TryGetValue(dm, out var nm) ? nm : null,
                Priority = d.Priority, RequiresReview = d.RequiresReview, SortOrder = d.SortOrder, LastActivityAt = now, StatusChangedAt = now,
            });
            db.Deliverables.Add(copy);
            dMap[d.Id] = copy.Id;
        }

        var tasks = await db.Tasks.AsNoTracking().Where(x => x.ProjectId == srcId && x.Status != TaskStatuses.Cancelled && srcPds.Contains(x.ProjectDisciplineId))
            .OrderBy(x => x.Seq).ToListAsync();
        var tMap = new Dictionary<Guid, (Guid Id, string Key)>();
        next = await Keys.Reserve(db, p.Id, "task", tasks.Count);
        foreach (var t in tasks)
        {
            var seq = next++;
            var copy = Copied(new WorkTask
            {
                ProjectId = p.Id, Seq = seq, Key = Keys.Format(p.ProjectNumber, "task", seq), Name = t.Name, Description = t.Description, ProjectDisciplineId = pdMap[t.ProjectDisciplineId],
                DeliverableId = t.DeliverableId is { } td && dMap.TryGetValue(td, out var nd) ? nd : null,
                MilestoneId = t.MilestoneId is { } tm && msMap.TryGetValue(tm, out var tms) ? tms : null,
                RequiresReview = t.RequiresReview, Priority = t.Priority, EstimatedHours = t.EstimatedHours, SortOrder = t.SortOrder, LastActivityAt = now, StatusChangedAt = now,
            });
            db.Tasks.Add(copy);
            tMap[t.Id] = (copy.Id, copy.Key);
        }

        var ids = tasks.Select(t => t.Id).ToList();
        foreach (var e in await db.Dependencies.AsNoTracking().Where(x => ids.Contains(x.PredecessorTaskId) && ids.Contains(x.SuccessorTaskId)).ToListAsync())
        {
            var (pred, succ) = (tMap[e.PredecessorTaskId], tMap[e.SuccessorTaskId]);
            db.Dependencies.Add(Copied(new TaskDependency { ProjectId = p.Id, PredecessorTaskId = pred.Id, SuccessorTaskId = succ.Id, LagDays = e.LagDays, Note = e.Note,
                CreatedBy = access.Me.Id, CreatedAt = now, AuditKey = $"{pred.Key} → {succ.Key}" }));
        }
        var dids = deliverables.Select(d => d.Id).ToList();
        foreach (var e in await db.DeliverableDependencies.AsNoTracking().Where(x => dids.Contains(x.PredecessorDeliverableId) && dids.Contains(x.SuccessorDeliverableId)).ToListAsync())
            db.DeliverableDependencies.Add(Copied(new DeliverableDependency { ProjectId = p.Id, PredecessorDeliverableId = dMap[e.PredecessorDeliverableId],
                SuccessorDeliverableId = dMap[e.SuccessorDeliverableId], LagDays = e.LagDays, Note = e.Note, CreatedBy = access.Me.Id, CreatedAt = now }));
        await db.SaveChangesAsync();
    }
}

/// "Reassign work" (FR-ADM-02, E-01, §13.15): everything a person owns across projects, reassigned one by one or all at
/// once by an Admin or the person's Supervisor, each change logged and notified like any other reassignment.
public static class ReassignEndpoints
{
    public sealed record ItemRef(string Kind, Guid Id);
    public sealed record ReassignBody(ItemRef[] Items, Guid ToUserId);

    public sealed record OpenItem(string Kind, Guid Id, string? Key, string Name, string? Status, DateOnly? DueDate, Guid ProjectId, string ProjectNumber, string ProjectName);

    public const string TaskAssignee = "TaskAssignee", TaskReviewer = "TaskReviewer", DeliverableOwner = "DeliverableOwner",
        DeliverableReviewer = "DeliverableReviewer", DecisionOwner = "DecisionOwner", DisciplineLead = "DisciplineLead";

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/users/{id:guid}/open-work", async (Guid id, Access access, HubDb db) =>
        {
            var person = await Person(db, access, id);
            return new { Person = new { person.Id, person.DisplayName, person.IsActive, person.Email }, Items = await OpenWork(db, access, id) };
        });
        api.MapPost("/users/{id:guid}/reassign", Reassign);
    }

    static async Task<AppUser> Person(HubDb db, Access access, Guid id)
    {
        var person = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id) ?? throw ApiException.NotFound();
        Access.Demand(Permissions.ActOnStaff(access.Actor, person.SupervisorId)); // Admins for anyone, Supervisors for direct reports (§25.7)
        return person;
    }

    static readonly string[] ClosedProjects = [ProjectStatus.Archived, ProjectStatus.Cancelled];

    /// Open items only, on projects the caller can see that are not Archived or Cancelled.
    static async Task<List<OpenItem>> OpenWork(HubDb db, Access access, Guid id)
    {
        var pids = access.VisibleProjects().Where(p => !ClosedProjects.Contains(p.Status)).Select(p => p.Id);
        var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && (t.AssigneeId == id || t.ReviewerId == id)
                && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled)
            .Select(t => new { t.Id, t.Key, t.Name, t.Status, t.DueDate, t.ProjectId, t.AssigneeId, t.ReviewerId }).ToListAsync();
        var dels = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && (d.OwnerId == id || d.ReviewerId == id)
                && d.Status != DeliverableStatus.Issued && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Cancelled)
            .Select(d => new { d.Id, d.Key, d.Name, d.Status, d.DueDate, d.ProjectId, d.OwnerId, d.ReviewerId }).ToListAsync();
        var decisions = await db.Decisions.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.OwnerUserId == id
                && (d.Status == DecisionStatus.Pending || d.Status == DecisionStatus.UnderReview || d.Status == DecisionStatus.Deferred))
            .Select(d => new { d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, d.ProjectId }).ToListAsync();
        var leads = await db.ProjectDisciplines.AsNoTracking().Where(pd => pids.Contains(pd.ProjectId) && pd.LeadUserId == id && pd.IsActive)
            .Select(pd => new { pd.Id, pd.ProjectId, Name = pd.Discipline!.Name }).ToListAsync();
        var projectIds = tasks.Select(t => t.ProjectId).Concat(dels.Select(d => d.ProjectId)).Concat(decisions.Select(d => d.ProjectId)).Concat(leads.Select(l => l.ProjectId)).Distinct().ToList();
        var projects = await db.Projects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => new { p.ProjectNumber, p.Name });
        OpenItem Row(string kind, Guid itemId, string? key, string name, string? status, DateOnly? due, Guid projectId) =>
            new(kind, itemId, key, name, status, due, projectId, projects[projectId].ProjectNumber, projects[projectId].Name);
        return
        [
            .. tasks.Where(t => t.AssigneeId == id).Select(t => Row(TaskAssignee, t.Id, t.Key, t.Name, t.Status, t.DueDate, t.ProjectId)),
            .. tasks.Where(t => t.ReviewerId == id).Select(t => Row(TaskReviewer, t.Id, t.Key, t.Name, t.Status, t.DueDate, t.ProjectId)),
            .. dels.Where(d => d.OwnerId == id).Select(d => Row(DeliverableOwner, d.Id, d.Key, d.Name, d.Status, d.DueDate, d.ProjectId)),
            .. dels.Where(d => d.ReviewerId == id).Select(d => Row(DeliverableReviewer, d.Id, d.Key, d.Name, d.Status, d.DueDate, d.ProjectId)),
            .. decisions.Select(d => Row(DecisionOwner, d.Id, d.Key, d.Subject, d.Status, d.RequiredByDate, d.ProjectId)),
            .. leads.Select(l => Row(DisciplineLead, l.Id, null, l.Name, null, null, l.ProjectId)),
        ];
    }

    static async Task<IResult> Reassign(Guid id, ReassignBody body, Access access, HubDb db, TeamService team, Notifier notify, SettingsStore store, TimeProvider clock)
    {
        var person = await Person(db, access, id);
        Check.That(body.ToUserId != id, "toUserId", "reassign.same_person");
        var to = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == body.ToUserId && u.IsActive) ?? throw ApiException.Invalid("toUserId", "team.inactive_user");
        var s = await store.Get(db);
        var open = await OpenWork(db, access, id);
        var wanted = body.Items.Select(i => (i.Kind, i.Id)).ToHashSet();
        var chosen = open.Where(o => wanted.Contains((o.Kind, o.Id))).ToList();
        Check.That(chosen.Count == wanted.Count, "items", "reassign.not_open");
        var actor = await notify.ActorName();
        var skipped = new List<object>();
        var perProject = new Dictionary<Guid, int>();
        var projects = new Dictionary<Guid, Project>();
        async Task<Project> P(Guid pid) => projects.TryGetValue(pid, out var p) ? p : projects[pid] = await db.Projects.FirstAsync(x => x.Id == pid);
        await Tx.Run(db, async () =>
        {
            foreach (var o in chosen)
            {
                var (kind, itemId, pid) = (o.Kind, o.Id, o.ProjectId);
                var p = await P(pid);
                switch (kind)
                {
                    case TaskAssignee or TaskReviewer:
                    {
                        var t = await db.Tasks.FirstAsync(x => x.Id == itemId);
                        var other = kind == TaskAssignee ? t.ReviewerId : t.AssigneeId;
                        if (other == to.Id && !s.AllowSelfReview) { skipped.Add(new { t.Key, Reason = Text.Get("task.self_review") }); continue; } // R-02
                        if (kind == TaskAssignee) t.AssigneeId = to.Id; else t.ReviewerId = to.Id;
                        await team.EnsureMember(p, to.Id, kind == TaskAssignee ? ProjectRole.TeamMember : ProjectRole.Reviewer);
                        await notify.Send(kind == TaskAssignee ? NotificationEvents.TaskAssigned : NotificationEvents.ReviewerSet, to.Id, TaskEndpoints.Item(p, t),
                            Text.Get(kind == TaskAssignee ? "notify.task_assigned" : "notify.reviewer_set", actor, t.Key, t.Name));
                        break;
                    }
                    case DeliverableOwner or DeliverableReviewer:
                    {
                        var d = await db.Deliverables.FirstAsync(x => x.Id == itemId);
                        if (kind == DeliverableOwner) d.OwnerId = to.Id; else d.ReviewerId = to.Id;
                        await team.EnsureMember(p, to.Id, kind == DeliverableOwner ? ProjectRole.TeamMember : ProjectRole.Reviewer);
                        if (kind == DeliverableOwner)
                            await notify.Send(NotificationEvents.DeliverableOwned, to.Id, DeliverableEndpoints.Item(p, d), Text.Get("notify.deliverable_owned", actor, d.Key, d.Name));
                        break;
                    }
                    case DecisionOwner:
                    {
                        var d = await db.Decisions.FirstAsync(x => x.Id == itemId);
                        d.OwnerUserId = to.Id;
                        await team.EnsureMember(p, to.Id, ProjectRole.TeamMember);
                        await notify.Send(NotificationEvents.DecisionAssigned, to.Id, DecisionEndpoints.Item(p, d), Text.Get("notify.decision_assigned", actor, d.Key, d.Subject));
                        break;
                    }
                    case DisciplineLead:
                        await team.SetLead(p, await db.ProjectDisciplines.FirstAsync(x => x.Id == itemId), to.Id);
                        break;
                }
                perProject[pid] = perProject.GetValueOrDefault(pid) + 1;
            }
            db.Audit.Reason = Text.Get("reassign.reason", person.DisplayName);
            await db.SaveChangesAsync();
            foreach (var (pid, n) in perProject) // each affected project's PM hears once
            {
                var p = await P(pid);
                await notify.Send(NotificationEvents.SupervisorStaffing, p.ProjectManagerId, TeamService.Item(p),
                    Text.Get("notify.work_reassigned", actor, n, person.DisplayName, p.ProjectNumber, to.DisplayName));
            }
            await db.SaveChangesAsync();
            return 0;
        });
        return Results.Ok(new { Reassigned = chosen.Count - skipped.Count, Skipped = skipped });
    }
}
