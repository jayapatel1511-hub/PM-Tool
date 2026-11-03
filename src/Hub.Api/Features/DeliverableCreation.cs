using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class DeliverableEndpoints
{
    /// Read-only validation of the normal create command. A plan is not a reservation;
    /// a batch caller must prepare every row again inside its commit transaction.
    internal sealed record CreatePlan(CreateBody Body, Project Project, ProjectContext Context,
        ProjectDiscipline Discipline, Guid OwnerId, Milestone? Milestone, DateOnly? DueDate,
        string? ClientDrawingNumber, OrgSettings Settings);

    internal static async Task<CreatePlan> PrepareCreate(Guid projectId, CreateBody body,
        Access access, HubDb db, SettingsStore store)
    {
        var (project, context) = await access.Project(projectId);
        var settings = await store.Get(db);
        if (!settings.IsFlagOn("small_ux")) body = body with { FromTaskId = null };
        Check.That(settings.IsFlagOn(RegisterFlag) || body.ClientDrawingNumber is null, "clientDrawingNumber", "deliverableRegister.disabled");
        var clientNumber = settings.IsFlagOn(RegisterFlag) ? DeliverableNumberingEndpoints.Number(body.ClientDrawingNumber) : null;
        var discipline = await db.ProjectDisciplines.FirstOrDefaultAsync(x => x.Id == body.ProjectDisciplineId && x.ProjectId == projectId)
            ?? throw ApiException.Invalid("projectDisciplineId", "error.required");
        var permission = Permissions.CreateDeliverable(access.Actor, context, discipline.Id);
        if (!permission) throw ApiException.Forbidden(permission.Why!, await DisciplineName(db, discipline.Id));
        Check.That(discipline.IsActive, "projectDisciplineId", "deliverable.inactive_discipline");
        Check.That(await db.DeliverableTypes.AnyAsync(x => x.Id == body.DeliverableTypeId), "deliverableTypeId", "error.required");
        var owner = body.OwnerId ?? discipline.LeadUserId ?? throw ApiException.Invalid("ownerId", "error.required");
        await ActivePerson(db, owner, "ownerId");
        if (body.ReviewerId is { } reviewer) await ActivePerson(db, reviewer, "reviewerId");
        Milestone? milestone = null;
        if (body.MilestoneId is { } milestoneId)
            milestone = await db.Milestones.FirstOrDefaultAsync(x => x.Id == milestoneId && x.ProjectId == projectId)
                ?? throw ApiException.Invalid("milestoneId", "error.not_found");
        var due = body.DueDate ?? milestone?.Date;
        if (body.StartDate is { } start && due is { } end) Check.That(start <= end, "startDate", "deliverable.start_after_due");
        if (body.Priority is not null) Check.OneOf(body.Priority, Priority.All, "priority");
        body = body with { Name = Check.Required(body.Name, "name", 200),
            Description = Check.Optional(body.Description, "description", 8000), Revision = Check.Optional(body.Revision, "revision", 50) };
        return new(body, project, context, discipline, owner, milestone, due, clientNumber, settings);
    }

    /// The single in-process creator used by manual and batch intake. Keys, membership,
    /// Created activity, assignment publication and notifications share the caller's transaction.
    internal static async Task<DeliverableCreated> CreatePrepared(CreatePlan plan, Access access,
        HubDb db, TeamService team, Notifier notify, string? importSnapshot = null)
    {
        var body = plan.Body;
        var project = plan.Project;
        var deliverable = await Tx.Run(db, async () =>
        {
            var (seq, key) = await Keys.Next(db, project.Id, project.ProjectNumber, "deliverable");
            var created = new Deliverable
            {
                ProjectId = project.Id, Seq = seq, Key = key, Name = body.Name,
                ProjectDisciplineId = plan.Discipline.Id, DeliverableTypeId = body.DeliverableTypeId,
                Description = body.Description, OwnerId = plan.OwnerId, ReviewerId = body.ReviewerId,
                MilestoneId = body.MilestoneId, StartDate = body.StartDate, DueDate = plan.DueDate,
                OriginalStartDate = body.StartDate, OriginalDueDate = plan.DueDate, Priority = body.Priority ?? Priority.Medium,
                Revision = body.Revision, RequiresReview = body.RequiresReview ?? true,
                LastActivityAt = DateTimeOffset.UtcNow, SortOrder = seq, ClientDrawingNumber = plan.ClientDrawingNumber,
            };
            db.Deliverables.Add(created);
            if (importSnapshot is not null) db.Audit.Note(created, snapshot: importSnapshot);
            await team.EnsureMember(project, plan.OwnerId, ProjectRole.TeamMember);
            await team.EnsureMember(project, body.ReviewerId, ProjectRole.Reviewer);
            await db.SaveChangesAsync();
            if (body.FromTaskId is { } taskId)
            {
                var task = await db.Tasks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == taskId && x.ProjectId == project.Id)
                    ?? throw ApiException.Invalid("fromTaskId", "deliverable.task_project");
                Access.Demand(Permissions.CreateDeliverable(access.Actor, plan.Context, task.ProjectDisciplineId));
                Check.That(await db.ProjectDisciplines.AnyAsync(x => x.Id == task.ProjectDisciplineId && x.IsActive), "fromTaskId", "deliverable.inactive_discipline");
                await DecisionEndpoints.NewLink(db, access, project, ItemType.Task, task.Id, task.Key,
                    ItemType.Deliverable, created.Id, ItemRelation.Related);
            }
            await notify.Send(NotificationEvents.DeliverableOwned, plan.OwnerId, Item(project, created),
                Text.Get("notify.deliverable_owned", await notify.ActorName(), created.Key, created.Name));
            await db.SaveChangesAsync();
            return created;
        });
        var warnings = Warnings(deliverable, plan.Milestone)
            .Concat(plan.Settings.IsFlagOn(RegisterFlag) ? await NumberWarnings(db, deliverable) : []).ToArray();
        return new(deliverable.Id, deliverable.Key, deliverable.RowVersion, deliverable.DueDate, warnings);
    }
}
