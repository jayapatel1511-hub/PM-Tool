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
            // Provisioning may attach an Entra account to a user with a matching email and no object ID.
            // Reserve a non-Entra ID for demo people, including records created by earlier seed versions.
            // Development auth uses this reserved ID; real Entra object IDs are GUIDs.
            var demoId = "dev-" + email;
            if (existing is not null)
            {
                if (existing.EntraObjectId is null) existing.EntraObjectId = demoId;
                if (existing.EntraObjectId != demoId)
                    throw new InvalidOperationException($"Synthetic review email {email} is already linked to another identity.");
                return (existing, false);
            }
            var user = new AppUser { DisplayName = name, Email = email, JobTitle = title, OfficeId = office.Id, EntraObjectId = demoId };
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

        // Keep the review database useful for every Tuesday persona while preserving reviewer edits.
        // These definitions intentionally mirror Seed.DevUsers; this opt-in seed must not enable
        // development authentication or alter the development-only user picker.
        await Person("Jordan Lee", "jordan@hub.test", "Applications Administrator", SystemRole.Admin);
        var (lena, _) = await Person("Lena Brooks", "lena@hub.test", "Regional Manager", SystemRole.Executive, SystemRole.Supervisor);
        var (sam, samAdded) = await Person("Sam Patel", "sam@hub.test", "Civil Group Manager", SystemRole.Supervisor);
        var (priya, priyaAdded) = await Person("Priya Nair", "priya@hub.test", "Project Manager", SystemRole.ProjectManager);
        var (marc, marcAdded) = await Person("Marc Dubois", "marc@hub.test", "Senior Civil Engineer", SystemRole.ProjectManager, SystemRole.Supervisor);
        var (alex, alexAdded) = await Person("Alex Chen", "alex@hub.test", "Civil Designer (EIT)");
        var (jill, jillAdded) = await Person("Jill Martin", "jill@hub.test", "Civil Designer");
        var (diane, dianeAdded) = await Person("Diane Roy", "diane@hub.test", "Senior Technical Reviewer");
        var (omar, omarAdded) = await Person("Omar Haddad", "omar@hub.test", "Geotechnical Lead");
        await Person("Rita Gomez", "rita@hub.test", "Auditor", SystemRole.ReadOnly);

        // Only newly provisioned children receive the default reporting line. Existing review
        // records may have been deliberately reorganized and must remain untouched on restart.
        if (samAdded) sam.SupervisorId = lena.Id;
        if (priyaAdded) priya.SupervisorId = lena.Id;
        if (marcAdded) marc.SupervisorId = sam.Id;
        if (alexAdded) alex.SupervisorId = sam.Id;
        if (jillAdded) jill.SupervisorId = sam.Id;
        if (dianeAdded) diane.SupervisorId = lena.Id;
        if (omarAdded) omar.SupervisorId = lena.Id;

        // Find the client by fixture provenance first, so a reviewer rename is preserved.
        var clientId = await db.Projects.Where(p => p.ExternalSource == Source).Select(p => (Guid?)p.ClientId).FirstOrDefaultAsync();
        var client = clientId is { } knownClient
            ? await db.Clients.FirstOrDefaultAsync(c => c.Id == knownClient)
            : await db.Clients.FirstOrDefaultAsync(c => c.Name == "Harbour Municipality" || c.Name == "Synthetic Review Client");
        if (client is null)
        {
            client = new Client { Name = "Harbour Municipality", ShortName = "Harbour" };
            db.Clients.Add(client);
        }
        // Jay requested ordinary display copy; clean only the exact old fixture defaults.
        if (client.Name == "Synthetic Review Client") client.Name = "Harbour Municipality";
        if (client.ShortName == "Demo") client.ShortName = "Harbour";
        var disciplines = await db.Disciplines.Where(d => d.Code == "PM" || d.Code == "CIV")
            .ToDictionaryAsync(d => d.Code, d => d.Id);
        var drawingType = await db.DeliverableTypes.Where(t => t.Name == "Drawing Package").Select(t => t.Id).FirstAsync();

        async Task Project(string number, string name, AppUser pm, AppUser civilOwner, AppUser reviewer, string deliverableName, string taskName)
        {
            var existing = await db.Projects.FirstOrDefaultAsync(p => p.ExternalSource == Source && p.ExternalId == number);
            if (existing is not null)
            {
                if (existing.Name == name + " (Demo)") existing.Name = name;
                if (existing.Description == "SYNTHETIC DEMO DATA — fictional client, project and work for application review only.")
                    existing.Description = "Coordinate the civil design package, review comments and multidisciplinary inputs.";
                return;
            }
            if (await db.Projects.AnyAsync(p => p.ProjectNumber == number))
                throw new InvalidOperationException($"Project number {number} is already used by non-demo data.");
            var project = new Project
            {
                ProjectNumber = number, Name = name, ClientId = client.Id, OfficeId = office.Id, ProjectManagerId = pm.Id,
                Description = "Coordinate the civil design package, review comments and multidisciplinary inputs.",
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

        await Project("DEMO-101", "Community Facility Site Servicing", taylor, jay, yagmur,
            "Site Servicing Concept Drawings", "Prepare preliminary servicing layout");
        await Project("DEMO-102", "Harbour Road Civil Design", jay, jay, taylor,
            "Road and Grading Concept Drawings", "Develop road and grading concept");
        await Project("DEMO-103", "Waterfront Drainage Review", yagmur, yagmur, jay,
            "Drainage Concept Drawings", "Prepare drainage design inputs");

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
