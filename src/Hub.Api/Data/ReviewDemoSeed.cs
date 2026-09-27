using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Data;

/// <summary>
/// Opt-in, synthetic review data. The dedicated review database retains edits across releases;
/// rerunning this seed only adds missing demo records and never resets reviewer changes.
/// </summary>
public static class ReviewDemoSeed
{
    public const string Source = "ReviewDemo";

    public static async Task Seed(HubDb db, TimeProvider clock)
    {
        db.Audit.AsSystem("ReviewDemoSeed");
        await using var tx = await db.Database.BeginTransactionAsync();
        var office = await db.Offices.FirstAsync();
        var now = clock.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        async Task<(AppUser User, bool Added)> Person(string name, string email, string title, params string[] roles)
        {
            var existing = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Email == email);
            if (existing is not null) return (existing, false);
            var user = new AppUser { DisplayName = name, Email = email, JobTitle = title, OfficeId = office.Id };
            foreach (var role in roles)
                user.Roles.Add(new UserSystemRole { UserId = user.Id, Role = role, Source = RoleSource.Manual, GrantedAt = now });
            db.Users.Add(user);
            return (user, true);
        }

        var (taylor, _) = await Person("Taylor", "taylor@hub.test", "Manager, Project Managers", SystemRole.ProjectManager, SystemRole.Supervisor);
        var (jay, _) = await Person("Jay", "jay@hub.test", "Civil Project Manager and Manager", SystemRole.ProjectManager, SystemRole.Supervisor);
        var (yagmur, yagmurAdded) = await Person("Yagmur", "yagmur@hub.test", "Project Manager", SystemRole.ProjectManager);
        // Set the reporting line once. A reviewer may later change it without a restart undoing their work.
        if (yagmurAdded) yagmur.SupervisorId = taylor.Id;

        var client = await db.Clients.FirstOrDefaultAsync(c => c.Name == "Synthetic Review Client");
        if (client is null)
        {
            client = new Client { Name = "Synthetic Review Client", ShortName = "Demo" };
            db.Clients.Add(client);
        }
        var disciplines = await db.Disciplines.Where(d => d.Code == "PM" || d.Code == "CIV")
            .ToDictionaryAsync(d => d.Code, d => d.Id);
        var drawingType = await db.DeliverableTypes.Where(t => t.Name == "Drawing Package").Select(t => t.Id).FirstAsync();

        async Task Project(string number, string name, AppUser pm, AppUser civilOwner, AppUser reviewer, string deliverableName, string taskName)
        {
            if (await db.Projects.AnyAsync(p => p.ExternalSource == Source && p.ExternalId == number)) return;
            if (await db.Projects.AnyAsync(p => p.ProjectNumber == number))
                throw new InvalidOperationException($"Project number {number} is already used by non-demo data.");
            var project = new Project
            {
                ProjectNumber = number, Name = name, ClientId = client.Id, OfficeId = office.Id, ProjectManagerId = pm.Id,
                Description = "SYNTHETIC DEMO DATA — fictional client, project and work for application review only.",
                Status = ProjectStatus.Active, StatusChangedAt = now, ActivatedAt = now,
                StartDate = today.AddDays(-14), TargetCompletionDate = today.AddDays(90),
                ExternalSource = Source, ExternalId = number,
                NextMilestoneSeq = 2, NextDeliverableSeq = 2, NextTaskSeq = 3,
            };
            db.Projects.Add(project);
            var pmDiscipline = new ProjectDiscipline { ProjectId = project.Id, DisciplineId = disciplines["PM"], LeadUserId = pm.Id, SortOrder = 0 };
            var civilDiscipline = new ProjectDiscipline { ProjectId = project.Id, DisciplineId = disciplines["CIV"], LeadUserId = civilOwner.Id, SortOrder = 1 };
            db.ProjectDisciplines.AddRange(pmDiscipline, civilDiscipline);
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = pm.Id, Roles = [ProjectRole.PM], PrimaryDisciplineId = pmDiscipline.Id, AddedAt = now });
            foreach (var user in new[] { civilOwner, reviewer }.Where(u => u.Id != pm.Id).DistinctBy(u => u.Id))
                db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = user.Id,
                    Roles = user.Id == reviewer.Id ? [ProjectRole.TeamMember, ProjectRole.Reviewer] : [ProjectRole.TeamMember],
                    PrimaryDisciplineId = civilDiscipline.Id, AddedAt = now });
            var milestone = new Milestone
            {
                ProjectId = project.Id, Seq = 1, Key = Keys.Format(number, "milestone", 1), Name = "30% Design Review",
                MilestoneType = MilestoneType.DesignSubmission, Date = today.AddDays(45), OriginalDate = today.AddDays(45),
                IsClientFacing = true, SortOrder = 1,
            };
            db.Milestones.Add(milestone);
            var deliverable = new Deliverable
            {
                ProjectId = project.Id, Seq = 1, Key = Keys.Format(number, "deliverable", 1), Name = deliverableName,
                ProjectDisciplineId = civilDiscipline.Id, DeliverableTypeId = drawingType, OwnerId = civilOwner.Id,
                ReviewerId = reviewer.Id, MilestoneId = milestone.Id, DueDate = today.AddDays(40), OriginalDueDate = today.AddDays(40),
                Status = DeliverableStatus.InProgress, Revision = "P01", LastActivityAt = now, SortOrder = 1,
            };
            db.Deliverables.Add(deliverable);
            db.Tasks.AddRange(
                new WorkTask
                {
                    ProjectId = project.Id, Seq = 1, Key = Keys.Format(number, "task", 1), Name = taskName,
                    ProjectDisciplineId = civilDiscipline.Id, DeliverableId = deliverable.Id, AssigneeId = civilOwner.Id,
                    ReviewerId = reviewer.Id, RequiresReview = true, Status = TaskStatuses.InProgress,
                    StartDate = today.AddDays(-7), DueDate = today.AddDays(14), OriginalStartDate = today.AddDays(-7), OriginalDueDate = today.AddDays(14),
                    EstimatedHours = 24, ProgressPct = 40, LastActivityAt = now, StatusChangedAt = now, SortOrder = 1,
                },
                new WorkTask
                {
                    ProjectId = project.Id, Seq = 2, Key = Keys.Format(number, "task", 2), Name = "Coordinate 30% review comments",
                    ProjectDisciplineId = pmDiscipline.Id, MilestoneId = milestone.Id, AssigneeId = pm.Id,
                    Status = TaskStatuses.NotStarted, DueDate = today.AddDays(35), OriginalDueDate = today.AddDays(35),
                    EstimatedHours = 8, LastActivityAt = now, StatusChangedAt = now, SortOrder = 2,
                });
        }

        await Project("DEMO-101", "Community Facility Site Servicing (Demo)", taylor, jay, yagmur,
            "Site Servicing Concept Drawings", "Prepare preliminary servicing layout");
        await Project("DEMO-102", "Harbour Road Civil Design (Demo)", jay, jay, taylor,
            "Road and Grading Concept Drawings", "Develop road and grading concept");
        await Project("DEMO-103", "Waterfront Drainage Review (Demo)", yagmur, yagmur, jay,
            "Drainage Concept Drawings", "Prepare drainage design inputs");

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
