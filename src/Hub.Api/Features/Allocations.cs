using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class AllocationEndpoints
{
    public sealed record DayInput(DateOnly WorkDate, decimal Hours);
    public sealed record LinkInput(string WorkType, Guid WorkId, DateOnly WorkDate);
    public sealed record CreateBody(Guid RequestId, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string? Reason);
    public sealed record EditBody(Guid RequestId, int RowVersion, Guid PersonId, string Purpose, DateOnly FromDate, DateOnly ThroughDate,
        decimal PlannedHours, DayInput[] Days, LinkInput[] Links, string Reason);
    public sealed record CancelBody(Guid RequestId, int RowVersion, string Reason);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/projects/{projectId:guid}/allocations", List);
        api.MapGet("/projects/{projectId:guid}/allocations/{id:guid}", Detail);
        api.MapPost("/projects/{projectId:guid}/allocations", Create).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/edit", Edit).WithMetadata(new Coordination.AtomicCommand());
        api.MapPost("/projects/{projectId:guid}/allocations/{id:guid}/cancel", Cancel).WithMetadata(new Coordination.AtomicCommand());
    }

    static async Task<object> List(Guid projectId, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var actor = access.Actor;
        var manager = Permissions.IsPM(actor, ctx);
        return await db.Allocations.AsNoTracking().Where(a => a.ProjectId == projectId &&
                (manager || a.CreatedBy == actor.Id || (ctx.IsMember && (actor.Supervisor || actor.Admin) &&
                    db.Users.Any(u => u.Id == a.PersonId && (actor.Admin || u.SupervisorId == actor.Id)))))
            .OrderBy(a => a.FromDate).Select(a => new { a.Id, a.PersonId, a.Purpose, a.FromDate, a.ThroughDate,
                a.PlannedHours, a.Status, a.RowVersion }).ToListAsync();
    }

    static async Task<object> Detail(Guid projectId, Guid id, Access access, HubDb db)
    {
        var (_, ctx) = await access.Project(projectId, false);
        var actor = access.Actor;
        var manager = Permissions.IsPM(actor, ctx);
        var a = await db.Allocations.AsNoTracking().FirstOrDefaultAsync(a => a.ProjectId == projectId && a.Id == id &&
            (manager || a.CreatedBy == actor.Id || (ctx.IsMember && (actor.Supervisor || actor.Admin) &&
                db.Users.Any(u => u.Id == a.PersonId && (actor.Admin || u.SupervisorId == actor.Id))))) ?? throw ApiException.NotFound();
        return new { a.Id, a.ProjectId, a.PersonId, a.Purpose, a.FromDate, a.ThroughDate, a.PlannedHours, a.Status,
            a.ConfirmedBy, a.ConfirmedAt, a.OverCapacityReason, a.ConfirmationSnapshot, a.RowVersion,
            Days = await db.AllocationDayOverrides.AsNoTracking().Where(d => d.AllocationId == id).OrderBy(d => d.WorkDate)
                .Select(d => new { d.WorkDate, d.Hours }).ToListAsync(),
            Links = await db.AllocationWorkLinks.AsNoTracking().Where(l => l.AllocationId == id && l.ReleasedAt == null)
                .OrderBy(l => l.WorkDate).Select(l => new { l.WorkType, l.WorkId, l.WorkDate }).ToListAsync() };
    }

    static async Task Validate(HubDb db, Project p, Guid personId, string purpose, DateOnly from, DateOnly through,
        decimal hours, DayInput[] days, LinkInput[] links, SettingsStore store)
    {
        await Coordination.Person(db, p, personId, "personId");
        Check.OneOf(purpose, AllocationPurpose.All, "purpose");
        Check.That(from != default && through >= from && through.DayNumber - from.DayNumber <= 366, "throughDate", "error.date_range");
        Check.That(hours > 0 && hours <= 10000, "plannedHours", "error.positive");
        Check.That(days is { Length: <= 366 } && days.All(d => d.WorkDate >= from && d.WorkDate <= through && d.Hours >= 0)
            && days.Select(d => d.WorkDate).Distinct().Count() == days.Length, "days", "error.invalid");
        Check.That(links is { Length: > 0 and <= 500 } && links.All(l => l.WorkDate >= from && l.WorkDate <= through)
            && links.Select(l => (l.WorkType, l.WorkId, l.WorkDate)).Distinct().Count() == links.Length, "links", "error.invalid");
        var settings = await store.Get(db);
        var office = await db.Users.Where(u => u.Id == personId).Select(u => u.OfficeId).SingleAsync();
        var holidays = settings.WorkingDaysEnabled
            ? await db.Holidays.Where(h => h.OfficeId == null || h.OfficeId == office).Select(h => h.Date).ToListAsync() : [];
        var calendar = settings.WorkingDaysEnabled ? new WorkCalendar(holidays) : WorkCalendar.Weekdays;
        try { AllocationRules.Spread(from, through, hours, calendar, days.ToDictionary(d => d.WorkDate, d => d.Hours)); }
        catch (ArgumentException) { throw ApiException.Invalid("days", "error.invalid"); }
        foreach (var link in links)
        {
            Check.That(link.WorkId != Guid.Empty, "links", "error.invalid");
            if (link.WorkType == "Task")
            {
                Check.That(purpose == AllocationPurpose.Production && await db.Tasks.AnyAsync(t => t.Id == link.WorkId
                    && t.ProjectId == p.Id && t.AssigneeId == personId
                    && t.Status != TaskStatuses.Complete && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.OnHold
                    && t.DeletedAt == null),
                    "links", "coord.reference");
            }
            else if (link.WorkType == "Review")
            {
                Check.That(purpose == AllocationPurpose.Review && await db.DisciplineReviews.AnyAsync(r => r.Id == link.WorkId
                    && r.ProjectId == p.Id && r.ReviewerId == personId && db.ReviewPackages.Any(pkg =>
                        pkg.ProjectId == p.Id && pkg.CurrentRoundId == r.RoundId
                        && pkg.Status != ReviewStatus.Cancelled && pkg.Status != ReviewStatus.Superseded)),
                    "links", "coord.reference");
            }
            else throw ApiException.Invalid("links", "error.invalid");
        }
    }

    static Task<Coordination.Result> Create(Guid projectId, CreateBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.create", body }, access, db, clock, async (project, ctx) =>
        {
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate,
                body.PlannedHours, body.Days, body.Links, store);
            var a = new ResourceAllocation { ProjectId = project.Id, PersonId = body.PersonId, Purpose = body.Purpose,
                FromDate = body.FromDate, ThroughDate = body.ThroughDate, PlannedHours = body.PlannedHours };
            db.Allocations.Add(a);
            foreach (var day in body.Days)
                db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = a.Id, WorkDate = day.WorkDate, Hours = day.Hours });
            foreach (var link in body.Links)
                db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = a.Id, PersonId = a.PersonId,
                    WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate });
            db.Audit.Note(a, reason: body.Reason);
            return a;
        });

    static Task<Coordination.Result> Edit(Guid projectId, Guid id, EditBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.edit", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || a.CreatedBy == access.Actor.Id
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(a.Status is AllocationStatus.Proposed or AllocationStatus.Confirmed, "status", "error.invalid");
            await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate,
                body.PlannedHours, body.Days, body.Links, store);
            var oldLinks = await db.AllocationWorkLinks.Where(l => l.AllocationId == a.Id && l.ReleasedAt == null).ToListAsync();
            var oldDays = await db.AllocationDayOverrides.Where(d => d.AllocationId == a.Id).ToListAsync();
            var now = clock.GetUtcNow();
            foreach (var link in oldLinks) link.ReleasedAt = now;
            db.AllocationDayOverrides.RemoveRange(oldDays);
            // Free unique person/work/day slices before replacement, within Coordination.Run's transaction.
            await db.SaveChangesAsync();
            foreach (var day in body.Days)
                db.AllocationDayOverrides.Add(new AllocationDayOverride { AllocationId = a.Id, WorkDate = day.WorkDate, Hours = day.Hours });
            foreach (var link in body.Links)
                db.AllocationWorkLinks.Add(new AllocationWorkLink { AllocationId = a.Id, PersonId = body.PersonId,
                    WorkType = link.WorkType, WorkId = link.WorkId, WorkDate = link.WorkDate });
            a.PersonId = body.PersonId; a.Purpose = body.Purpose; a.FromDate = body.FromDate; a.ThroughDate = body.ThroughDate;
            a.PlannedHours = body.PlannedHours; a.Status = AllocationRules.AfterMaterialEdit(a.Status);
            a.ConfirmedBy = null; a.ConfirmedAt = null; a.OverCapacityReason = null; a.ConfirmationSnapshot = null;
            // A child-only edit must invalidate the allocation version seen by another editor.
            db.Entry(a).Property(x => x.PlannedHours).IsModified = true;
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            return a;
        });

    static Task<Coordination.Result> Cancel(Guid projectId, Guid id, CancelBody body, Access access, HubDb db, TimeProvider clock) =>
        Coordination.Run(projectId, body.RequestId, new { operation = "allocation.cancel", id, body }, access, db, clock, async (project, ctx) =>
        {
            var a = await db.Allocations.FirstOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == id) ?? throw ApiException.NotFound();
            Coordination.Version(a, body.RowVersion);
            Access.Demand(Permissions.ProposeAllocation(access.Actor, ctx));
            Access.Demand(Permissions.IsPM(access.Actor, ctx) || a.CreatedBy == access.Actor.Id
                ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(AllocationRules.Step(a.Status, AllocationStatus.Cancelled), "status", "error.invalid");
            a.Status = AllocationStatus.Cancelled;
            foreach (var link in await db.AllocationWorkLinks.Where(l => l.AllocationId == id && l.ReleasedAt == null).ToListAsync())
                link.ReleasedAt = clock.GetUtcNow();
            db.Audit.Note(a, reason: Check.Reason(body.Reason));
            return a;
        });
}
