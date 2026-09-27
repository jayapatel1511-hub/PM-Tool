using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class AllocationSchemaTests(HubFactory f)
{
    readonly TestData data = new(f);

    [Fact]
    public async Task Migration_enforces_availability_and_exact_work_slice_uniqueness()
    {
        var project = await data.Project();
        var personId = data.User(TestData.Alex);
        var day = new DateOnly(2026, 10, 5);
        var allocation = new ResourceAllocation
        {
            ProjectId = project.Id, PersonId = personId, FromDate = day,
            ThroughDate = day, PlannedHours = 4, Status = AllocationStatus.Proposed,
        };
        await f.DbAsync(async db =>
        {
            db.Allocations.Add(allocation);
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride
            {
                PersonId = personId, WorkDate = day, AvailableHours = 4,
                Category = AvailabilityCategory.Reduced,
            });
            db.AllocationWorkLinks.Add(new AllocationWorkLink
            {
                AllocationId = allocation.Id, PersonId = personId, WorkType = "Task",
                WorkId = Guid.NewGuid(), WorkDate = day,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        Assert.Equal(4, f.Db(db => db.AvailabilityOverrides.Single(x => x.PersonId == personId && x.WorkDate == day).AvailableHours));
        var link = f.Db(db => db.AllocationWorkLinks.AsNoTracking().Single(x => x.AllocationId == allocation.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.AllocationWorkLinks.Add(new AllocationWorkLink
            {
                AllocationId = allocation.Id, PersonId = personId, WorkType = link.WorkType,
                WorkId = link.WorkId, WorkDate = day,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
        await f.DbAsync(async db =>
        {
            var active = await db.AllocationWorkLinks.SingleAsync(x => x.Id == link.Id);
            active.ReleasedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            return 0;
        });
        await f.DbAsync(async db =>
        {
            db.AllocationWorkLinks.Add(new AllocationWorkLink
            {
                AllocationId = allocation.Id, PersonId = personId, WorkType = link.WorkType,
                WorkId = link.WorkId, WorkDate = day,
            });
            await db.SaveChangesAsync();
            return 0;
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride
            {
                PersonId = personId, WorkDate = day.AddDays(1), AvailableHours = -1,
                Category = AvailabilityCategory.Reduced,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.DbAsync(async db =>
        {
            db.AllocationWorkLinks.Add(new AllocationWorkLink
            {
                AllocationId = allocation.Id, PersonId = personId, WorkType = "Review",
                WorkId = Guid.NewGuid(), WorkDate = day,
            });
            await db.SaveChangesAsync();
            return 0;
        }));
    }
}
