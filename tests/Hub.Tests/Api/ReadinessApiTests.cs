using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class ReadinessApiTests(HubFactory f)
{
    readonly TestData data = new(f);

    async Task<Guid> IsolatedOwner(Project project)
    {
        var disciplineId = data.ProjectDiscipline(project.Id, "Civil");
        var user = new AppUser { Email = $"capacity-{Guid.NewGuid():N}@hub.test", DisplayName = "Capacity test owner",
            OfficeId = project.OfficeId, WeeklyCapacityHours = 40m };
        await f.DbAsync(async db =>
        {
            db.Users.Add(user);
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = user.Id,
                Roles = [ProjectRole.TeamMember], PrimaryDisciplineId = disciplineId });
            await db.SaveChangesAsync(); return 0;
        });
        return user.Id;
    }

    [Fact]
    public async Task Readiness_window_rechecks_ready_output_and_exposes_same_project_constraint()
    {
        var project = await data.Project();
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var path = $"/api/v1/projects/{project.Id}/readiness/window?from={today:yyyy-MM-dd}&to={today.AddDays(6):yyyy-MM-dd}";
        var target = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Marc),
            startDate = today, dueDate = today.AddDays(2), estimatedHours = 4m });
        var targetId = target.G("id");
        var targetVersion = f.Db(db => db.Tasks.Single(t => t.Id == targetId).RowVersion);
        var assessment = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/readiness/Task/{targetId}",
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Site layout", "Drawing checked"))).Json();
        await f.DbAsync(async db =>
        {
            var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checks)
            {
                check.Applies = check.Code is not (ReadinessCheckCode.ProductionCapacity or ReadinessCheckCode.SubmissionGate); // an unlinked gate is never satisfied
                check.Satisfied = check.Applies == true ? true : null;
            }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            await db.SaveChangesAsync(); return 0;
        });
        var ready = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(1, ready["readyOutputsTotal"]!.GetValue<int>());
        Assert.Equal(targetId, ready["readyOutputs"]!.AsArray().Single()!.G("targetId"));
        var nextReady = await (await f.As(TestData.Alex).GetAsync(path + "&readyPage=2&pageSize=1")).Json();
        Assert.Empty(nextReady["readyOutputs"]!.AsArray());
        Assert.Equal(1, nextReady.I("readyOutputsTotal"));
        Assert.False(nextReady["readyOutputsTruncated"]!.GetValue<bool>());

        await f.DbAsync(async db =>
        {
            db.WorkConstraints.Add(new WorkConstraint { ProjectId = project.Id, Seq = 1, Key = project.ProjectNumber + "-CT0001", TargetType = "Task", TargetId = targetId,
                Category = "Handoff", Description = "Await survey", RemovalOwnerId = data.User(TestData.Alex),
                AffectedOwnerId = data.User(TestData.Marc), NeededBy = today.AddDays(-1),
                SourceUrl = "https://example.test/survey", State = ConstraintState.Open });
            await db.SaveChangesAsync(); return 0;
        });
        var blocked = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(1, blocked["constraintsTotal"]!.GetValue<int>());
        Assert.Equal(0, blocked["readyOutputsTotal"]!.GetValue<int>());
        Assert.False(blocked["constraintsTruncated"]!.GetValue<bool>());
        Assert.False(blocked["readyOutputsTruncated"]!.GetValue<bool>());
        var laterConstraint = new WorkConstraint { ProjectId = project.Id, Seq = 2, Key = project.ProjectNumber + "-CT0002", TargetType = "Task", TargetId = targetId,
            Category = "Approval", Description = "Await permit", RemovalOwnerId = data.User(TestData.Alex),
            AffectedOwnerId = data.User(TestData.Marc), NeededBy = today.AddDays(1), SourceUrl = "https://example.test/permit", State = ConstraintState.Open };
        await f.DbAsync(async db => { db.WorkConstraints.Add(laterConstraint); return await db.SaveChangesAsync(); });
        var nextConstraints = await (await f.As(TestData.Alex).GetAsync(path + "&constraintsPage=2&pageSize=1")).Json();
        Assert.Equal(laterConstraint.Id, nextConstraints["constraints"]!.AsArray().Single()!.G("id"));
        Assert.Equal(2, nextConstraints.I("constraintsTotal"));
        var csv = await (await f.As(TestData.Alex).GetAsync(path.Replace("/window?", "/window/export?") + "&list=constraints&format=csv&constraintsPage=2&pageSize=1")).EnsureSuccessStatusCode().Content.ReadAsStringAsync();
        Assert.Contains("Await survey", csv); Assert.Contains("Await permit", csv);
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0;
        });
        await (await f.As(TestData.Rita).GetAsync(path)).Json(404);
    }

    [Fact]
    public async Task Production_capacity_distinguishes_available_unavailable_and_unknown()
    {
        var project = await data.Project();
        var owner = await IsolatedOwner(project);
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner, startDate = today, dueDate = today.AddDays(2), estimatedHours = 8m });
        var taskId = task.G("id");
        var available = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", taskId, today, f.Clock.Now));
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == taskId)).EstimatedHours = 80m; await db.SaveChangesAsync(); return 0; });
        var unavailable = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", taskId, today, f.Clock.Now));
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == taskId)).EstimatedHours = null; await db.SaveChangesAsync(); return 0; });
        var unknown = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", taskId, today, f.Clock.Now));

        Assert.True(available.Applies);
        Assert.Equal(true, available.Satisfied);
        Assert.Equal(false, unavailable.Satisfied);
        Assert.Null(unknown.Satisfied);

        await f.DbAsync(async db =>
        {
            (await db.Tasks.SingleAsync(t => t.Id == taskId)).EstimatedHours = 8m;
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = owner, WorkDate = today,
                AvailableHours = 0m, Category = AvailabilityCategory.Unavailable });
            await db.SaveChangesAsync(); return 0;
        });
        var blockedToday = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", taskId, today, f.Clock.Now));
        Assert.Equal(false, blockedToday.Satisfied);
    }

    [Fact]
    public async Task Readiness_review_gate_rechecks_required_package_and_current_round()
    {
        var project = await data.Project();
        var disciplineId = data.ProjectDiscipline(project.Id, "Civil");
        var deliverable = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new
        {
            name = "Review gated package", projectDisciplineId = disciplineId, deliverableTypeId = await data.DeliverableType(),
            ownerId = data.User(TestData.Alex), requiresReview = true
        })).Json(201);
        var deliverableId = deliverable.G("id");
        var path = $"/api/v1/projects/{project.Id}/readiness/Deliverable/{deliverableId}";
        var targetVersion = f.Db(db => db.Deliverables.Single(d => d.Id == deliverableId).RowVersion);
        var assessment = await (await f.As(TestData.Alex).Post(path,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Issue package", "Required review is approved"))).Json();
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
            {
                check.Applies = check.Code == ReadinessCheckCode.ProductionOwner;
                check.Satisfied = check.Applies == true;
            }
            await db.SaveChangesAsync(); return 0;
        });
        var missingPackage = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, missingPackage["assessment"]!.S("state"));
        Assert.False(missingPackage["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ReviewGate)!["satisfied"]!.GetValue<bool>());
        Guid packageId = Guid.Empty;
        await f.DbAsync(async db =>
        {
            var package = new ReviewPackage { ProjectId = project.Id, Key = "REV-GATE", Seq = 1, Title = "Required review",
                Purpose = "Confirm the package", CoordinatorId = data.User(TestData.Marc), ProjectDisciplineId = disciplineId,
                Status = ReviewStatus.ChangesRequired, RoundNumber = 1, RequiredForIssue = true };
            var round = new ReviewRound { ProjectId = project.Id, PackageId = package.Id, Number = 1, Purpose = package.Purpose,
                Status = ReviewStatus.ChangesRequired };
            db.ReviewPackages.Add(package);
            await db.SaveChangesAsync();
            db.ReviewRounds.Add(round);
            await db.SaveChangesAsync();
            var source = new SourceRevision { ProjectId = project.Id, DeliverableId = deliverableId,
                SourceRowVersion = 1, SourceIdentity = $"readiness-{Guid.NewGuid():N}",
                SourceKey = "REVIEW-SOURCE", ExternalIdentifier = "REV-A", Issuer = "Synthetic review",
                Scope = "Readiness gate", Title = "Review source", Revision = "A", Url = "https://review.example.test/rev-a",
                IdentityHash = Guid.NewGuid().ToString("N") };
            db.SourceRevisions.Add(source);
            await db.SaveChangesAsync();
            db.ReviewManifestItems.Add(new ReviewManifestItem { ProjectId = project.Id, RoundId = round.Id,
                DeliverableId = deliverableId, SourceRevisionId = source.Id, AuthorIds = [data.User(TestData.Alex)] });
            package.CurrentRoundId = round.Id; packageId = package.Id;
            (await db.Deliverables.SingleAsync(d => d.Id == deliverableId)).RequiredReviewPackageId = package.Id;
            await db.SaveChangesAsync(); return 0;
        });
        var blocked = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, blocked["assessment"]!.S("state"));
        var blockedGate = blocked["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ReviewGate)!;
        Assert.True(blockedGate["applies"]!.GetValue<bool>());
        Assert.False(blockedGate["satisfied"]!.GetValue<bool>());

        await f.DbAsync(async db =>
        {
            (await db.ReviewPackages.SingleAsync(p => p.Id == packageId)).Status = ReviewStatus.Approved;
            (await db.ReviewRounds.SingleAsync(r => r.PackageId == packageId)).Status = ReviewStatus.Approved;
            await db.SaveChangesAsync(); return 0;
        });
        var approved = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, approved["assessment"]!.S("state"));
        var clearedGate = approved["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ReviewGate)!;
        Assert.True(clearedGate["satisfied"]!.GetValue<bool>());

        await f.DbAsync(async db =>
        {
            var work = await db.Deliverables.SingleAsync(d => d.Id == deliverableId);
            work.RequiresReview = false; work.RequiredReviewPackageId = null;
            var manualGate = await db.ReadinessChecks.SingleAsync(c => c.AssessmentId == assessment.G("id") && c.Code == ReadinessCheckCode.ReviewGate);
            manualGate.Applies = true; manualGate.Satisfied = false; manualGate.Reason = "Manual review remains required";
            await db.SaveChangesAsync(); return 0;
        });
        var manuallyRequired = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, manuallyRequired["assessment"]!.S("state"));
    }

    [Fact]
    public async Task Production_capacity_counts_confirmed_reservation_and_day_override()
    {
        var project = await data.Project();
        var owner = await IsolatedOwner(project);
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var task = await data.NewTask(project.Id,
            extra: new { assigneeId = owner, startDate = today, dueDate = today, estimatedHours = 4m });
        var allocation = new ResourceAllocation { ProjectId = project.Id, PersonId = owner, FromDate = today,
            ThroughDate = today, PlannedHours = 4m, Status = AllocationStatus.Confirmed };
        await f.DbAsync(async db =>
        {
            db.Allocations.Add(allocation);
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = owner, WorkDate = today,
                AvailableHours = 4m, Category = AvailabilityCategory.Reduced });
            db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = allocation.Id, WorkDate = today, Hours = 4m });
            db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = allocation.Id, PersonId = owner,
                WorkType = "Task", WorkId = task.G("id"), WorkDate = today });
            await db.SaveChangesAsync();
            return 0;
        });

        var result = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), today, f.Clock.Now));
        Assert.True(result.Satisfied == true, result.Reason);

        var competing = new ResourceAllocation { ProjectId = project.Id, PersonId = owner, FromDate = today,
            ThroughDate = today, PlannedHours = 4m, Status = AllocationStatus.Confirmed };
        await f.DbAsync(async db =>
        {
            db.Allocations.Add(competing);
            db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = competing.Id, WorkDate = today, Hours = 4m });
            await db.SaveChangesAsync();
            return 0;
        });
        result = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), today, f.Clock.Now));
        Assert.Equal(false, result.Satisfied);
        Assert.Contains("exceed", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Production_capacity_keeps_other_project_demand_and_undated_deliverable_work_unknown()
    {
        var project = await data.Project();
        var owner = await IsolatedOwner(project);
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner,
            startDate = today, dueDate = today, estimatedHours = 4m });
        var other = await data.Project();
        var otherDisciplineId = data.ProjectDiscipline(other.Id, "Civil");
        await f.DbAsync(async db =>
        {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = other.Id, UserId = owner,
                Roles = [ProjectRole.TeamMember], PrimaryDisciplineId = otherDisciplineId });
            await db.SaveChangesAsync(); return 0;
        });
        var otherTask = await data.NewTask(other.Id, extra: new { assigneeId = owner,
            startDate = today, dueDate = today, estimatedHours = 8m });
        var crossProject = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), today, f.Clock.Now));
        Assert.Null(crossProject.Satisfied);
        Assert.Contains("other active workload", crossProject.Reason, StringComparison.OrdinalIgnoreCase);
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == otherTask.G("id"))).DueDate = null; await db.SaveChangesAsync(); return 0; });
        var undatedOtherProject = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Task", task.G("id"), today, f.Clock.Now));
        Assert.Null(undatedOtherProject.Satisfied);

        var deliverableOwner = await IsolatedOwner(project);
        var disciplineId = data.ProjectDiscipline(project.Id, "Civil");
        var typeId = await data.DeliverableType();
        var deliverable = new Deliverable { ProjectId = project.Id, Key = "D" + Guid.NewGuid().ToString("N")[..8],
            Name = "Undated production", ProjectDisciplineId = disciplineId, DeliverableTypeId = typeId,
            OwnerId = deliverableOwner, DueDate = today.AddDays(1) };
        var linked = new WorkTask { ProjectId = project.Id, Key = "T" + Guid.NewGuid().ToString("N")[..8],
            Name = "Required layout", ProjectDisciplineId = disciplineId, DeliverableId = deliverable.Id,
            AssigneeId = deliverableOwner, EstimatedHours = 80m };
        await f.DbAsync(async db => { db.Deliverables.Add(deliverable); db.Tasks.Add(linked); await db.SaveChangesAsync(); return 0; });
        var undated = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Deliverable", deliverable.Id, today, f.Clock.Now));
        Assert.Null(undated.Satisfied);
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == linked.Id)).DueDate = today.AddDays(2); await db.SaveChangesAsync(); return 0; });
        var afterDeadline = await f.DbAsync(db => ReadinessEndpoints.ProductionCapacity(db, project, "Deliverable", deliverable.Id, today, f.Clock.Now));
        Assert.Null(afterDeadline.Satisfied);
    }

    [Fact]
    public async Task Review_capacity_requires_confirmed_dated_links_and_rechecks_overload()
    {
        var project = await data.Project();
        var reviewer = await IsolatedOwner(project);
        var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
        var disciplineId = data.ProjectDiscipline(project.Id, "Civil");
        var deliverable = new Deliverable { ProjectId = project.Id, Key = "D" + Guid.NewGuid().ToString("N")[..8],
            Name = "Capacity reviewed deliverable", ProjectDisciplineId = disciplineId, DeliverableTypeId = await data.DeliverableType(),
            OwnerId = data.User(TestData.Alex), DueDate = today, RequiresReview = true };
        var package = new ReviewPackage { ProjectId = project.Id, Key = "R" + Guid.NewGuid().ToString("N")[..8], Seq = 1,
            Title = "Capacity review", Purpose = "Check capacity", CoordinatorId = data.User(TestData.Marc), ProjectDisciplineId = disciplineId,
            Status = ReviewStatus.InReview, RoundNumber = 1, RequiredForIssue = true };
        var round = new ReviewRound { ProjectId = project.Id, PackageId = package.Id, Number = 1, Purpose = package.Purpose, Status = ReviewStatus.InReview };
        var assignment = new DisciplineReview { ProjectId = project.Id, RoundId = round.Id, ProjectDisciplineId = disciplineId,
            ReviewerId = reviewer, DueDate = today, Status = DisciplineReviewStatus.Pending };
        var allocation = new ResourceAllocation { ProjectId = project.Id, PersonId = reviewer, Purpose = AllocationPurpose.Review,
            FromDate = today, ThroughDate = today, PlannedHours = 4m, Status = AllocationStatus.Confirmed };
        await f.DbAsync(async db =>
        {
            db.Deliverables.Add(deliverable); db.ReviewPackages.Add(package); await db.SaveChangesAsync();
            db.ReviewRounds.Add(round); await db.SaveChangesAsync();
            package.CurrentRoundId = round.Id; db.DisciplineReviews.Add(assignment); deliverable.RequiredReviewPackageId = package.Id;
            db.Allocations.Add(allocation); await db.SaveChangesAsync();
            db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = allocation.Id, WorkDate = today, Hours = 4m });
            db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = allocation.Id, PersonId = reviewer, WorkType = "Review",
                WorkId = assignment.Id, WorkDate = today, ReviewHours = 4m });
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = reviewer, WorkDate = today,
                AvailableHours = 8m, Category = AvailabilityCategory.Additional });
            await db.SaveChangesAsync(); return 0;
        });

        var fit = await f.DbAsync(db => ReadinessEndpoints.ReviewCapacity(db, project, "Deliverable", deliverable.Id, today));
        Assert.Equal(true, fit.Satisfied);
        var otherProject = await data.Project();
        var outside = new ResourceAllocation { ProjectId = otherProject.Id, PersonId = reviewer, Purpose = AllocationPurpose.Review,
            FromDate = today, ThroughDate = today, PlannedHours = 1m, Status = AllocationStatus.Confirmed };
        await f.DbAsync(async db => { db.Allocations.Add(outside); await db.SaveChangesAsync(); return 0; });
        var outOfScope = await f.DbAsync(db => ReadinessEndpoints.ReviewCapacity(db, project, "Deliverable", deliverable.Id, today));
        Assert.Null(outOfScope.Satisfied);
        await f.DbAsync(async db => { db.Allocations.Remove(await db.Allocations.SingleAsync(a => a.Id == outside.Id)); await db.SaveChangesAsync(); return 0; });
        await f.DbAsync(async db =>
        {
            var competing = new ResourceAllocation { ProjectId = project.Id, PersonId = reviewer, Purpose = AllocationPurpose.Production,
                FromDate = today, ThroughDate = today, PlannedHours = 2m, Status = AllocationStatus.Confirmed };
            db.Allocations.Add(competing);
            db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = competing.Id, WorkDate = today, Hours = 2m });
            var overrideRow = await db.AvailabilityOverrides.SingleAsync(o => o.PersonId == reviewer && o.WorkDate == today);
            overrideRow.AvailableHours = 7m; overrideRow.Category = AvailabilityCategory.Reduced;
            await db.SaveChangesAsync(); return 0;
        });
        var competingFits = await f.DbAsync(db => ReadinessEndpoints.ReviewCapacity(db, project, "Deliverable", deliverable.Id, today));
        Assert.Equal(true, competingFits.Satisfied); // 4 review + 2 competing, not 4 added to each allocation.
        await f.DbAsync(async db =>
        {
            var overrideRow = await db.AvailabilityOverrides.SingleAsync(o => o.PersonId == reviewer && o.WorkDate == today);
            overrideRow.AvailableHours = 5m;
            await db.SaveChangesAsync(); return 0;
        });
        var overload = await f.DbAsync(db => ReadinessEndpoints.ReviewCapacity(db, project, "Deliverable", deliverable.Id, today));
        Assert.Equal(false, overload.Satisfied);
        await f.DbAsync(async db =>
        {
            db.AllocationWorkLinks.RemoveRange(db.AllocationWorkLinks.Where(l => l.WorkId == assignment.Id));
            await db.SaveChangesAsync(); return 0;
        });
        var missing = await f.DbAsync(db => ReadinessEndpoints.ReviewCapacity(db, project, "Deliverable", deliverable.Id, today));
        Assert.Null(missing.Satisfied);
    }

    [Fact]
    public async Task Scoped_assumption_exception_expires_without_erasing_approval()
    {
        var savedClock = f.Clock.Now;
        try
        {
            var today = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
            var project = await data.Project();
            var owner = data.User(TestData.Alex);
            var civil = data.ProjectDiscipline(project.Id, "Civil");
            var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
            var taskId = task.G("id");
            var taskVersion = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
            var basisRoot = $"/api/v1/projects/{project.Id}/design-basis";
            var input = new DesignBasisEndpoints.VersionInput("Synthetic grading", "Assume utility depth", null,
                null, null, null, null, null, today.AddDays(3), null);
            var entry = await (await f.As(TestData.Pm).Post(basisRoot,
                new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption, "Utility depth for readiness",
                    owner, civil, data.User(TestData.Marc), input, null))).Json();
            var entryId = entry.G("id");
            var version = f.Db(db => db.DesignBasisVersions.Single(v => v.EntryId == entryId));
            var proceedPath = $"{basisRoot}/{entryId}/versions/{version.Id}/proceed";
            await (await f.As(TestData.Marc).Post(proceedPath,
                new DesignBasisEndpoints.DispositionBody(Guid.NewGuid(), version.RowVersion, input.Scope,
                    owner, today.AddDays(5), "Limited preliminary work"))).Json();
            await (await f.As(TestData.Alex).Post($"{basisRoot}/{entryId}/uses",
                new DesignBasisEndpoints.UseBody(Guid.NewGuid(), version.Id, "Task", taskId,
                    "Preliminary layout under assumption"))).Json();
            var path = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}";
            var assessment = await (await f.As(TestData.Alex).Post(path,
                new ReadinessEndpoints.CreateBody(Guid.NewGuid(), taskVersion, "Preliminary layout",
                    "Layout reviewed against utility evidence"))).Json();
            await f.DbAsync(async db =>
            {
                foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync())
                {
                    check.Applies = check.Code is ReadinessCheckCode.Basis or ReadinessCheckCode.ProductionOwner;
                    check.Satisfied = check.Applies == true ? check.Code == ReadinessCheckCode.ProductionOwner : null;
                }
                await db.SaveChangesAsync();
                return 0;
            });
            Assert.Equal(ReadinessState.NotReady, (await (await f.As(TestData.Alex).GetAsync(path)).Json())["assessment"]!.S("state"));
            var body = new ReadinessEndpoints.ExceptionBody(Guid.NewGuid(), assessment.I("rowVersion"),
                version.Id, version.RowVersion, data.User(TestData.Marc), "Only preliminary grading layout",
                "Utility location could alter grading", today.AddDays(1));
            await (await f.As(TestData.Alex).Post(path + "/exceptions", body)).Json(403);
            await (await f.As(TestData.Pm).Post(path + "/exceptions", body with
            { RequestId = Guid.NewGuid(), AssessmentRowVersion = -1 })).Json(409);
            var approved = await (await f.As(TestData.Pm).Post(path + "/exceptions", body)).Json();
            Assert.Equal(approved.G("id"), (await (await f.As(TestData.Pm).Post(path + "/exceptions", body)).Json()).G("id"));
            var assigned = f.Db(db => db.Notifications.Where(n => n.ItemId == approved.G("id") &&
                n.EventType == NotificationEvents.ReadinessExceptionApproved).ToList());
            var notice = Assert.Single(assigned);
            Assert.Equal(data.User(TestData.Marc), notice.UserId);
            Assert.Equal(1, notice.Count);
            Assert.Contains($"panel=Task:{taskId}", notice.LinkPath);
            var permitted = await (await f.As(TestData.Alex).GetAsync(path)).Json();
            Assert.Equal(ReadinessState.ProceedUnderAssumption, permitted["assessment"]!.S("state"));
            Assert.Single(permitted["exceptions"]!.AsArray());
            f.Clock.Now = savedClock.AddDays(2);
            var expired = await (await f.As(TestData.Alex).GetAsync(path)).Json();
            Assert.Equal(ReadinessState.NeedsAssessment, expired["assessment"]!.S("state"));
            Assert.Single(expired["exceptions"]!.AsArray());
            f.Clock.Now = savedClock;
            await f.DbAsync(async db =>
            {
                (await db.DesignBasisVersions.SingleAsync(v => v.Id == version.Id)).Status = BasisStatus.Superseded;
                await db.SaveChangesAsync();
                return 0;
            });
            var changed = await (await f.As(TestData.Alex).GetAsync(path)).Json();
            Assert.Equal(ReadinessState.NeedsAssessment, changed["assessment"]!.S("state"));
            Assert.Single(changed["exceptions"]!.AsArray());
        }
        finally { f.Clock.Now = savedClock; }
    }

    [Fact]
    public async Task Detail_recomputes_linked_predecessor_instead_of_cached_ready_state()
    {
        var project = await data.Project();
        var predecessor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var successor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex),
            startDate = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime),
            dueDate = DateOnly.FromDateTime(f.Clock.Now.UtcDateTime).AddDays(2), estimatedHours = 4m });
        await f.DbAsync(async db =>
        {
            db.Dependencies.Add(new TaskDependency { ProjectId = project.Id, PredecessorTaskId = predecessor.G("id"), SuccessorTaskId = successor.G("id"), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync(); return 0;
        });
        var id = successor.G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == id).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{id}";
        var assessment = await (await f.As(TestData.Alex).Post(path,
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Output", "Criteria"))).Json();
        await f.DbAsync(async db =>
        {
            var checks = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checks)
            {
                check.Applies = check.Code is not (ReadinessCheckCode.ProductionCapacity or ReadinessCheckCode.SubmissionGate); // an unlinked gate is never satisfied
                check.Satisfied = check.Applies == true ? true : null;
            }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            await db.SaveChangesAsync(); return 0;
        });
        var detail = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, detail["assessment"]!.S("state"));
        var predecessorCheck = detail["checks"]!.AsArray().Single(c => c!["code"]!.GetValue<string>() == ReadinessCheckCode.Predecessor)!;
        Assert.True(predecessorCheck["applies"]!.GetValue<bool>());
        Assert.False(predecessorCheck["satisfied"]!.GetValue<bool>());
        var monday = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var commitments = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var proposal = await (await f.As(TestData.Pm).Post($"{commitments}/Task/{id}",
            new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), version, monday, monday.AddDays(2),
                "Blocked successor output", "Predecessor complete"))).Json();
        await (await f.As(TestData.Alex).Post($"{commitments}/{proposal.G("id")}/transition",
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), proposal.I("rowVersion"),
                CommitmentState.Committed, "Cannot sign a blocked output", null))).Json(400);
        await f.DbAsync(async db =>
        {
            (await db.Dependencies.SingleAsync(d => d.SuccessorTaskId == id)).DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(); return 0;
        });
        var withoutDeletedLink = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, withoutDeletedLink["assessment"]!.S("state"));
        var constraintId = Guid.NewGuid();
        await f.DbAsync(async db =>
        {
            db.WorkConstraints.Add(new WorkConstraint { Id = constraintId, ProjectId = project.Id,
                TargetType = "Task", TargetId = id, Category = "Handoff", Description = "Await source confirmation",
                RemovalOwnerId = data.User(TestData.Pm), AffectedOwnerId = data.User(TestData.Alex),
                NeededBy = monday.AddDays(2), SourceUrl = "https://example.test/source",
                State = ConstraintState.ResolutionProposed });
            await db.SaveChangesAsync(); return 0;
        });
        var proposedRemoval = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, proposedRemoval["assessment"]!.S("state"));
        var blockedHandoff = proposedRemoval["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        Assert.True(blockedHandoff["applies"]!.GetValue<bool>());
        Assert.False(blockedHandoff["satisfied"]!.GetValue<bool>());
        await f.DbAsync(async db =>
        {
            (await db.WorkConstraints.SingleAsync(c => c.Id == constraintId)).State = ConstraintState.VerifiedRemoved;
            await db.SaveChangesAsync(); return 0;
        });
        var verifiedRemoval = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.Ready, verifiedRemoval["assessment"]!.S("state"));
        var clearedHandoff = verifiedRemoval["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        Assert.True(clearedHandoff["satisfied"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Applicability_after_first_live_read_does_not_restore_cached_ready_state()
    {
        var project = await data.Project();
        var predecessor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var successor = await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) });
        var successorId = successor.G("id");
        var assessmentId = Guid.NewGuid();
        await f.DbAsync(async db =>
        {
            db.Dependencies.Add(new TaskDependency { ProjectId = project.Id, PredecessorTaskId = predecessor.G("id"), SuccessorTaskId = successorId });
            db.ReadinessAssessments.Add(new ReadinessAssessment { Id = assessmentId, ProjectId = project.Id, TargetType = "Task", TargetId = successorId,
                OwnerId = data.User(TestData.Alex), IntendedOutput = "Successor output", CompletionCriteria = "Predecessor complete", State = ReadinessState.Ready });
            foreach (var code in ReadinessCheckCode.All)
                db.ReadinessChecks.Add(new ReadinessCheckRecord { ProjectId = project.Id, AssessmentId = assessmentId, Code = code,
                    Applies = code is not (ReadinessCheckCode.ProductionCapacity or ReadinessCheckCode.SubmissionGate),
                    Satisfied = code is not (ReadinessCheckCode.ProductionCapacity or ReadinessCheckCode.SubmissionGate) });
            await db.SaveChangesAsync();
            return 0;
        });

        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{successorId}";
        var first = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NotReady, first["assessment"]!.S("state"));
        var rows = f.Db(db => new
        {
            Assessment = db.ReadinessAssessments.Single(a => a.Id == assessmentId),
            Gate = db.ReadinessChecks.Single(c => c.AssessmentId == assessmentId && c.Code == ReadinessCheckCode.SubmissionGate),
        });

        await (await f.As(TestData.Pm).Post($"{path}/checks/{Uri.EscapeDataString(ReadinessCheckCode.SubmissionGate)}/applicability",
            new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), rows.Assessment.RowVersion, rows.Gate.RowVersion, false, "No prerequisite package applies", null))).Json();

        var saved = f.Db(db => new
        {
            Assessment = db.ReadinessAssessments.Single(a => a.Id == assessmentId),
            Predecessor = db.ReadinessChecks.Single(c => c.AssessmentId == assessmentId && c.Code == ReadinessCheckCode.Predecessor),
        });
        Assert.Equal(ReadinessState.NotReady, saved.Assessment.State);
        Assert.False(saved.Predecessor.Satisfied);
    }

    [Fact]
    public async Task Open_constraint_lists_as_blocking_but_never_hides_an_unknown_check()
    {
        var project = await data.Project();
        var task = (await data.NewTask(project.Id, extra: new { assigneeId = data.User(TestData.Alex) })).G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == task).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{task}";
        var assessment = (await (await f.As(TestData.Alex).Post(path, new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version, "Layout", "Checked"))).Json()).G("id");
        await f.DbAsync(async db =>
        {
            foreach (var check in await db.ReadinessChecks.Where(c => c.AssessmentId == assessment).ToListAsync())
                check.Applies = check.Code == ReadinessCheckCode.Decision ? null : check.Code == ReadinessCheckCode.ProductionOwner;
            return await db.SaveChangesAsync();
        });
        var constraint = (await (await f.As(TestData.Alex).Post($"{path}/constraints", new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), version,
            "Scope", "Confirm the scope change", data.User(TestData.Pm), DateOnly.FromDateTime(f.Clock.Now.UtcDateTime.AddDays(3)),
            "https://example.test/scope"))).Json()).G("id");
        string[] Codes(JsonNode n, string key) => [.. n[key]!.AsArray().Select(x => x!.GetValue<string>())];

        var both = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NeedsAssessment, both["assessment"]!.S("state")); // §10.8: unknown first
        Assert.Equal([ReadinessCheckCode.Decision], Codes(both, "unknown"));
        Assert.Equal([ReadinessRules.ConstraintBlocker], Codes(both, "blocked")); // the known failure stays listed
        var start = await (await f.As(TestData.Alex).GetAsync($"/api/v1/tasks/{task}/start-readiness")).Json();
        Assert.Equal((ReadinessState.NeedsAssessment, true), (start.S("readinessState"), start["needsAuthorisation"]!.GetValue<bool>()));

        var detail = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        var decision = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Decision)!;
        await (await f.As(TestData.Pm).Post($"{path}/checks/Decision/applicability", new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(),
            detail["assessment"]!.I("rowVersion"), decision.I("rowVersion"), false, "No decision gates this layout", null))).Json();
        var constrained = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal((ReadinessState.NotReady, ReadinessRules.ConstraintBlocker), (constrained["assessment"]!.S("state"), Assert.Single(Codes(constrained, "blocked"))));
        Assert.True((await (await f.As(TestData.Alex).GetAsync($"/api/v1/tasks/{task}/start-readiness")).Json())["needsAuthorisation"]!.GetValue<bool>());

        await (await f.As(TestData.Pm).Post($"{path}/constraints/{constraint}/transition", new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(),
            f.Db(db => db.WorkConstraints.Single(c => c.Id == constraint).RowVersion), ConstraintState.Cancelled, "Scope change withdrawn", null))).Json();
        Assert.Equal(ReadinessState.Ready, (await (await f.As(TestData.Alex).GetAsync(path)).Json())["assessment"]!.S("state"));
    }

    [Fact]
    public async Task Chair_proposal_requires_performer_confirmation_and_snapshot_retains_withdrawal()
    {
        var project = await data.Project();
        var owner = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var taskId = task.G("id");
        var targetVersion = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var monday = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        monday = monday.AddDays(-((int)monday.DayOfWeek + 6) % 7);
        var root = $"/api/v1/projects/{project.Id}/weekly-commitments";
        var proposal = new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), targetVersion, monday,
            monday.AddDays(3), "Civil drawing review package", "Drawing reviewed with source register");
        var row = await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json();
        Assert.Equal(row.G("id"), (await (await f.As(TestData.Pm).Post($"{root}/Task/{taskId}", proposal)).Json()).G("id"));
        Assert.Equal(CommitmentState.Proposed, f.Db(db => db.OutputCommitments.Single(c => c.Id == row.G("id")).State));
        var promiseDetail = root + $"/{row.G("id")}";
        var chairDetail = await (await f.As(TestData.Pm).GetAsync(promiseDetail)).Json();
        Assert.False(chairDetail["canCommit"]!.GetValue<bool>());
        var performerDetail = await (await f.As(TestData.Alex).GetAsync(promiseDetail)).Json();
        Assert.True(performerDetail["canCommit"]!.GetValue<bool>());
        var readOnlyDetail = await (await f.As(TestData.Rita).GetAsync(promiseDetail)).Json();
        Assert.False(readOnlyDetail["canCommit"]!.GetValue<bool>());
        Assert.False(readOnlyDetail["canWithdraw"]!.GetValue<bool>());
        var move = $"{root}/{row.G("id")}/transition";
        var commit = new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), row.I("rowVersion"),
            CommitmentState.Committed, "Performer accepts the defined output", null);
        await (await f.As(TestData.Pm).Post(move, commit)).Json(403);
        await (await f.As(TestData.Alex).Post(move, commit with { RequestId = Guid.NewGuid() })).Json(400);
        var assessment = await (await f.As(TestData.Alex).Post($"/api/v1/projects/{project.Id}/readiness/Task/{taskId}",
            new ReadinessEndpoints.CreateBody(Guid.NewGuid(), targetVersion, "Civil drawing review package",
                "Drawing reviewed with source register"))).Json();
        await f.DbAsync(async db =>
        {
            var checkRows = await db.ReadinessChecks.Where(c => c.AssessmentId == assessment.G("id")).ToListAsync();
            foreach (var check in checkRows) { check.Applies = check.Code == ReadinessCheckCode.ProductionOwner; check.Satisfied = check.Applies == true ? true : null; }
            (await db.ReadinessAssessments.SingleAsync(a => a.Id == assessment.G("id"))).State = ReadinessState.Ready;
            await db.SaveChangesAsync();
            return 0;
        });
        await (await f.As(TestData.Alex).Post(move, commit with { RequestId = Guid.NewGuid() })).Json();
        var snap = await (await f.As(TestData.Pm).Post(root + "/snapshot",
            new WeeklyCommitmentsEndpoints.SnapshotBody(Guid.NewGuid(), monday, "Coordination week closed"))).Json();
        Assert.Equal(1, f.Db(db => db.WeeklyPlanSnapshots.Single(s => s.Id == snap.G("id")).CommittedCount));
        await f.DbAsync(async db => { (await db.Tasks.SingleAsync(t => t.Id == taskId)).AssigneeId = data.User(TestData.Omar);
            await db.SaveChangesAsync(); return 0; });
        var committed = f.Db(db => db.OutputCommitments.Single(c => c.Id == row.G("id")));
        await (await f.As(TestData.Pm).Post(move,
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committed.RowVersion,
                CommitmentState.Met, "Chair cannot attest another performer met criteria", "https://example.test/review"))).Json(403);
        await (await f.As(TestData.Pm).Post(move,
            new WeeklyCommitmentsEndpoints.MoveBody(Guid.NewGuid(), committed.RowVersion,
                CommitmentState.Withdrawn, "Performer reassigned after snapshot", null))).Json();
        var list = await (await f.As(TestData.Pm).GetAsync(root + $"?weekStart={monday:yyyy-MM-dd}")).Json();
        var outcome = list["snapshots"]!.AsArray().Single()!;
        Assert.Equal(1, outcome.I("committedCount"));
        Assert.Equal(1, outcome.I("withdrawn"));
        Assert.Equal(0, outcome.I("met"));
        Assert.Single(f.Db(db => db.OutputCommitmentEvents.Where(e => e.CommitmentId == row.G("id") &&
            e.ToState == CommitmentState.Withdrawn).ToList()));
        var history = await (await f.As(TestData.Pm).GetAsync(promiseDetail)).Json();
        Assert.Equal(2, history["events"]!.AsArray().Count);
        Assert.False(history["canCommit"]!.GetValue<bool>());
        Assert.False(history["canRecordMet"]!.GetValue<bool>());
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0; });
        await (await f.As(TestData.Rita).GetAsync(promiseDetail)).Json(404);
    }

    [Fact]
    public async Task Named_owner_creates_unassessed_output_with_scoped_and_retry_safe_command()
    {
        var project = await data.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = Visibility.Restricted;
            await db.SaveChangesAsync(); return 0; });
        var owner = data.User(TestData.Alex);
        var task = await data.NewTask(project.Id, extra: new { assigneeId = owner });
        var taskId = task.G("id");
        var version = f.Db(db => db.Tasks.Single(t => t.Id == taskId).RowVersion);
        var path = $"/api/v1/projects/{project.Id}/readiness/Task/{taskId}";
        var body = new ReadinessEndpoints.CreateBody(Guid.NewGuid(), version,
            "Preliminary site servicing layout", "Civil reviewer checks drawing and references");
        await (await f.As(TestData.Rita).Post(path, body)).Json(404);
        await (await f.As(TestData.Pm).Post(path, body with { RequestId = Guid.NewGuid() })).Json(403);
        await (await f.As(TestData.Alex).Post(path, body with { RequestId = Guid.NewGuid(), TargetRowVersion = version - 1 })).Json(409);
        var created = await (await f.As(TestData.Alex).Post(path, body)).Json();
        Assert.Equal(created.G("id"), (await (await f.As(TestData.Alex).Post(path, body)).Json()).G("id"));
        var detail = await (await f.As(TestData.Alex).GetAsync(path)).Json();
        Assert.Equal(ReadinessState.NeedsAssessment, detail["assessment"]!.S("state"));
        Assert.Equal(ReadinessCheckCode.All.Length, detail["checks"]!.AsArray().Count);
        Assert.All(detail["checks"]!.AsArray().Where(c => c!.S("code") != ReadinessCheckCode.ProductionOwner &&
            c.S("code") != ReadinessCheckCode.ProductionCapacity),
            c => { Assert.Null(c!["applies"]); Assert.Null(c["satisfied"]); });
        var handoff = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        var productionOwner = detail["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.ProductionOwner)!;
        Assert.True(productionOwner["applies"]!.GetValue<bool>());
        Assert.True(productionOwner["satisfied"]!.GetValue<bool>());
        await (await f.As(TestData.Pm).Post(path + "/checks/Production%20Owner/applicability",
            new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"),
                productionOwner.I("rowVersion"), false, "No owner claimed", null))).Json(400);
        var applicabilityPath = path + "/checks/Handoff/applicability";
        var applies = new ReadinessEndpoints.ApplicabilityBody(Guid.NewGuid(), detail["assessment"]!.I("rowVersion"),
            handoff.I("rowVersion"), true, "Accepted source handoff required for this layout", null);
        await (await f.As(TestData.Alex).Post(applicabilityPath, applies)).Json(403);
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies with { RequestId = Guid.NewGuid(), CheckRowVersion = -1 })).Json(409);
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies)).Json();
        await (await f.As(TestData.Pm).Post(applicabilityPath, applies)).Json();
        var updated = await (await f.As(TestData.Pm).GetAsync(path)).Json();
        var updatedHandoff = updated["checks"]!.AsArray().Single(c => c!.S("code") == ReadinessCheckCode.Handoff)!;
        Assert.True(updatedHandoff["applies"]!.GetValue<bool>());
        Assert.Null(updatedHandoff["satisfied"]);
        Assert.Equal(ReadinessState.NeedsAssessment, updated["assessment"]!.S("state"));
        var constraintPath = path + "/constraints";
        var createConstraint = new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), version, "Handoff",
            "Obtain accepted drainage input", data.User(TestData.Pm), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            "https://review.example.test/handoff/1");
        var constraint = await (await f.As(TestData.Alex).Post(constraintPath, createConstraint)).Json();
        Assert.Equal(constraint.G("id"), (await (await f.As(TestData.Alex).Post(constraintPath, createConstraint)).Json()).G("id"));
        var row = (await (await f.As(TestData.Alex).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.Open, row.S("state"));
        var movePath = constraintPath + $"/{constraint.G("id")}/transition";
        var propose = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), row.I("rowVersion"),
            ConstraintState.ResolutionProposed, "Source handoff delivered", "https://review.example.test/handoff/accepted");
        await (await f.As(TestData.Alex).Post(movePath, propose)).Json(403);
        await (await f.As(TestData.Pm).Post(movePath, propose)).Json();
        var proposed = (await (await f.As(TestData.Pm).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.ResolutionProposed, proposed.S("state"));
        var verify = new ReadinessEndpoints.ConstraintMoveBody(Guid.NewGuid(), proposed.I("rowVersion"),
            ConstraintState.VerifiedRemoved, "Affected work owner checked accepted handoff", null);
        await (await f.As(TestData.Pm).Post(movePath, verify)).Json(403);
        await (await f.As(TestData.Alex).Post(movePath, verify with { RequestId = Guid.NewGuid(), RowVersion = -1 })).Json(409);
        await (await f.As(TestData.Alex).Post(movePath, verify)).Json();
        var removed = (await (await f.As(TestData.Alex).GetAsync(constraintPath)).Json()).AsArray().Single()!;
        Assert.Equal(ConstraintState.VerifiedRemoved, removed.S("state"));
        Assert.Equal(data.User(TestData.Alex), removed.G("verifiedBy"));
        await (await f.As(TestData.Rita).GetAsync(path)).Json(404);
        await (await f.As(TestData.Alex).Post(path, body with { RequestId = Guid.NewGuid() })).Json(409);
    }
}
