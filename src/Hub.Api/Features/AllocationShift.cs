using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static partial class AllocationEndpoints
{
    public sealed record AllocationShiftVersion(Guid Id, int RowVersion);
    public sealed record AllocationShiftPreviewBody(string Mode, int Weeks, Guid[] AllocationIds);
    public sealed record AllocationShiftBody(Guid RequestId, string Mode, int Weeks, AllocationShiftVersion[] Allocations, string Reason);
    public sealed record AllocationShiftRole(Guid Id, string Label, DateOnly FromDate, DateOnly ThroughDate);
    public sealed record AllocationShiftRow(Guid Id, int RowVersion, Guid PersonId, string PersonName, string Status,
        DateOnly FromDate, DateOnly ThroughDate, DateOnly NewFromDate, DateOnly NewThroughDate, bool RequiresConfirmation, AllocationShiftRole[] UnchangedRoles);
    public sealed record AllocationShiftPreview(string Mode, int Weeks, AllocationShiftRow[] Allocations, DateTimeOffset AsOf);
    sealed record ShiftPlan(ResourceAllocation Allocation, EditBody Body);

    static int ShiftDays(string mode, int weeks)
    {
        Check.OneOf(mode, ["Selected", "WholeProject"], "mode");
        Check.That(weeks != 0, "weeks", "error.range");
        try { return checked(weeks * 7); }
        catch (OverflowException) { throw ApiException.Invalid("weeks", "error.range"); }
    }
    static DateOnly ShiftDate(DateOnly date, int days)
    {
        try { return date.AddDays(days); }
        catch (ArgumentOutOfRangeException) { throw ApiException.Invalid("weeks", "error.date_range"); }
    }
    static async Task<ResourceAllocation[]> ShiftRows(Guid projectId, string mode, Guid[] ids, ProjectContext context, Access access, HubDb db, bool apply)
    {
        Access.Demand(Permissions.ProposeAllocation(access.Actor, context));
        Check.That(ids is not null && ids.Distinct().Count() == ids.Length && ids.All(id => id != Guid.Empty), "allocations", "error.invalid");
        if (mode == "WholeProject") Access.Demand(Permissions.IsPM(access.Actor, context) ? Allow.Yes : Allow.No("perm.pm"));
        if (mode == "Selected") Check.That(ids.Length > 0, "allocations", "error.required");
        var query = db.Allocations.Where(row => row.ProjectId == projectId);
        if (mode == "WholeProject") query = query.Where(row => row.Status == AllocationStatus.Proposed || row.Status == AllocationStatus.Confirmed);
        else query = query.Where(row => ids.Contains(row.Id));
        var rows = await (apply ? query : query.AsNoTracking()).OrderBy(row => row.Id).ToArrayAsync();
        if (mode == "Selected" && rows.Length != ids.Length) throw ApiException.NotFound();
        if (mode == "WholeProject" && apply && !rows.Select(row => row.Id).SequenceEqual(ids.Order()))
            throw ApiException.Conflict("allocation_shift_changed", "allocation.shift_changed");
        Check.That(rows.Length > 0, "allocations", "allocation.shift_empty");
        foreach (var row in rows)
        {
            Access.Demand(Permissions.IsPM(access.Actor, context) || row.CreatedBy == access.Actor.Id ? Allow.Yes : Allow.No("perm.owner"));
            Check.That(row.Status is AllocationStatus.Proposed or AllocationStatus.Confirmed, "status", "error.invalid");
        }
        return rows;
    }
    static async Task<ShiftPlan[]> ShiftPlans(Project project, ResourceAllocation[] rows, int delta, Guid requestId, string reason, HubDb db, SettingsStore store)
    {
        var ids = rows.Select(row => row.Id).ToArray();
        var days = await db.AllocationDayOverrides.AsNoTracking().Where(day => ids.Contains(day.AllocationId)).ToArrayAsync();
        var links = await db.AllocationWorkLinks.AsNoTracking().Where(link => ids.Contains(link.AllocationId) && link.ReleasedAt == null).ToArrayAsync();
        var plans = new List<ShiftPlan>();
        foreach (var row in rows)
        {
            try
            {
                var body = new EditBody(requestId, row.RowVersion, row.PersonId, row.Purpose, ShiftDate(row.FromDate, delta), ShiftDate(row.ThroughDate, delta), row.PlannedHours,
                    days.Where(day => day.AllocationId == row.Id).Select(day => new DayInput(ShiftDate(day.WorkDate, delta), day.Hours)).ToArray(),
                    links.Where(link => link.AllocationId == row.Id).Select(link => new LinkInput(link.WorkType, link.WorkId, ShiftDate(link.WorkDate, delta), link.ReviewHours)).ToArray(), reason);
                await Validate(db, project, body.PersonId, body.Purpose, body.FromDate, body.ThroughDate, body.PlannedHours, body.Days, body.Links, store);
                plans.Add(new(row, body));
            }
            catch (ApiException error) { throw new ApiException(error.Status, error.Code, error.Message, error.Errors, new ShiftRefusedAllocation(row.Id)); }
        }
        // Reject an occupied target slice without revealing the other allocation or its creator/person metadata.
        var targets = plans.SelectMany(plan => plan.Body.Links.Select(link => (plan.Body.PersonId, link.WorkType, link.WorkId, link.WorkDate))).ToArray();
        Check.That(targets.Distinct().Count() == targets.Length, "links", "error.invalid");
        var people = rows.Select(row => row.PersonId).Distinct().ToArray();
        var from = plans.Min(plan => plan.Body.FromDate); var through = plans.Max(plan => plan.Body.ThroughDate);
        var occupied = await db.AllocationWorkLinks.AsNoTracking().Where(link => !ids.Contains(link.AllocationId) && people.Contains(link.PersonId)
            && link.ReleasedAt == null && link.WorkDate >= from && link.WorkDate <= through)
            .Select(link => new ShiftOccupiedLink(link.PersonId, link.WorkType, link.WorkId, link.WorkDate)).ToArrayAsync();
        var existing = occupied.Select(link => (link.PersonId, link.WorkType, link.WorkId, link.WorkDate)).ToHashSet();
        if (targets.Any(existing.Contains)) throw ApiException.Conflict("allocation_shift_conflict", "allocation.shift_conflict");
        return plans.ToArray();
    }

    static async Task<Ok<AllocationShiftPreview>> ShiftPreview(Guid projectId, AllocationShiftPreviewBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        if (!(await store.Get(db)).IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        var delta = ShiftDays(body.Mode, body.Weeks);
        if (body.Mode == "WholeProject") Check.That(body.AllocationIds is { Length: 0 }, "allocations", "error.invalid");
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        var (project, context) = await access.Project(projectId, false);
        var rows = await ShiftRows(projectId, body.Mode, body.AllocationIds, context, access, db, false);
        var plans = await ShiftPlans(project, rows, delta, Guid.Empty, "", db, store);
        var people = rows.Select(row => row.PersonId).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(person => people.Contains(person.Id)).ToDictionaryAsync(person => person.Id, person => person.DisplayName);
        var ids = rows.Select(row => row.Id).ToArray();
        var roles = await db.OpenRoles.AsNoTracking().Where(role => role.ProjectId == projectId && role.Status == OpenRoleStatus.Filled
            && role.FilledAllocationId != null && ids.Contains(role.FilledAllocationId.Value))
            .Select(role => new ShiftFilledRole(role.FilledAllocationId, new AllocationShiftRole(role.Id, role.RoleLabel, role.FromDate, role.ThroughDate))).ToArrayAsync();
        return TypedResults.Ok(new AllocationShiftPreview(body.Mode, body.Weeks, plans.Select(plan => new AllocationShiftRow(plan.Allocation.Id, plan.Allocation.RowVersion,
            plan.Allocation.PersonId, names[plan.Allocation.PersonId], plan.Allocation.Status, plan.Allocation.FromDate, plan.Allocation.ThroughDate,
            plan.Body.FromDate, plan.Body.ThroughDate, plan.Allocation.Status == AllocationStatus.Confirmed,
            roles.Where(role => role.FilledAllocationId == plan.Allocation.Id).OrderBy(role => role.Value.Id).Select(role => role.Value).ToArray())).ToArray(), clock.GetUtcNow()));
    }

    static async Task<Coordination.Result> Shift(Guid projectId, AllocationShiftBody body, Access access, HubDb db, SettingsStore store, TimeProvider clock, Notifier notify)
    {
        if (!(await store.Get(db)).IsFlagOn("capacity_screens")) throw ApiException.NotFound();
        var delta = ShiftDays(body.Mode, body.Weeks);
        Check.That(body.Allocations is { Length: > 0 } && body.Allocations.All(row => row is not null), "allocations", "error.required");
        return await Coordination.Run(projectId, body.RequestId, new AllocationShiftPayload("allocation.shift", body), access, db, clock, async (project, context) =>
        {
            var rows = await ShiftRows(projectId, body.Mode, body.Allocations.Select(row => row.Id).ToArray(), context, access, db, true);
            var versions = body.Allocations.ToDictionary(row => row.Id, row => row.RowVersion);
            foreach (var row in rows) Coordination.Version(row, versions[row.Id]);
            var reason = Check.Reason(body.Reason);
            foreach (var person in rows.Where(row => row.Status == AllocationStatus.Confirmed).Select(row => row.PersonId).Distinct().Order()) await LockPerson(db, person);
            var plans = await ShiftPlans(project, rows, delta, body.RequestId, reason, db, store);
            await ReleaseMaterial(db, rows.Select(row => row.Id).ToArray(), clock.GetUtcNow());
            foreach (var person in rows.Where(row => row.Status == AllocationStatus.Confirmed).GroupBy(row => row.PersonId))
                await TouchDates(db, person.Key, person.SelectMany(row => Enumerable.Range(0, row.ThroughDate.DayNumber - row.FromDate.DayNumber + 1).Select(index => row.FromDate.AddDays(index))));
            foreach (var plan in plans) await ApplyMaterial(project, plan.Allocation, plan.Body, db, notify, touchDates: false);
            return rows[0];
        });
    }
}

file sealed record ShiftOccupiedLink(Guid PersonId, string WorkType, Guid WorkId, DateOnly WorkDate);
file sealed record ShiftFilledRole(Guid? FilledAllocationId, AllocationEndpoints.AllocationShiftRole Value);
// Wire shapes: the refusal is problem-body JSON and the payload hash is a stored receipt; names and order are fixed.
file sealed record ShiftRefusedAllocation(Guid AllocationId);
file sealed record AllocationShiftPayload(string Operation, AllocationEndpoints.AllocationShiftBody Body);
