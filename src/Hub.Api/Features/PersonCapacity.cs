using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class PersonCapacity
{
    public sealed record Day(decimal Normal, decimal Available, decimal Holiday, decimal Leave, decimal Additional, bool Provisional);

    public static async Task<Dictionary<(Guid, DateOnly), Day>> Load(HubDb db, Guid[] people, DateOnly from, DateOnly through, OrgSettings settings)
    {
        var users = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).Select(u => new { u.Id, u.OfficeId, u.WeeklyCapacityHours }).ToListAsync();
        var schedules = await db.PersonSchedules.AsNoTracking().Where(s => people.Contains(s.PersonId) && s.FromDate <= through).OrderByDescending(s => s.FromDate).ToListAsync();
        var overrides = await db.AvailabilityOverrides.AsNoTracking().Where(o => people.Contains(o.PersonId) && o.ReconciledLeaveId == null && o.WorkDate >= from && o.WorkDate <= through).ToDictionaryAsync(o => (o.PersonId, o.WorkDate));
        var leaves = await db.ApprovedLeaves.AsNoTracking().Where(l => people.Contains(l.PersonId) && l.Status != "Cancelled" && l.FromDate <= through && l.ThroughDate >= from)
            .Select(l => new { l.PersonId, l.Status, l.FromDate, l.ThroughDate, l.HoursPerDay }).ToListAsync();
        var holidays = await db.Holidays.AsNoTracking().Where(h => h.Date >= from && h.Date <= through).ToListAsync();
        var result = new Dictionary<(Guid, DateOnly), Day>();
        foreach (var user in users)
        for (var date = from; date <= through; date = date.AddDays(1))
        {
            var schedule = schedules.FirstOrDefault(s => s.PersonId == user.Id && s.FromDate <= date);
            var days = schedule?.WorkingDays ?? [1, 2, 3, 4, 5];
            var normal = days.Contains((int)date.DayOfWeek) ? (schedule?.WeeklyHours ?? user.WeeklyCapacityHours ?? settings.DefaultWeeklyCapacityHours) / days.Length : 0;
            var holiday = holidays.Any(h => h.Date == date && (h.OfficeId == null || h.OfficeId == user.OfficeId));
            var value = overrides.GetValueOrDefault((user.Id, date));
            var baseline = holiday ? 0 : normal;
            var available = value?.AvailableHours ?? baseline;
            var records = leaves.Where(l => l.PersonId == user.Id && l.FromDate <= date && l.ThroughDate >= date).ToArray();
            var legacyAway = value?.Category is "Unavailable" or "Reduced" ? Math.Max(0, baseline - available) : 0;
            // Overlaps count once, and a full working day also covers additional availability.
            var hrAway = records.Length == 0 ? 0 : records.Any(l => l.HoursPerDay == null) ? Math.Max(normal, available) : records.Max(l => l.HoursPerDay!.Value);
            var reduction = records.Any(l => l.HoursPerDay == null) ? available : Math.Min(available, Math.Max(0, hrAway - legacyAway));
            result[(user.Id, date)] = new(normal, Math.Max(0, available - reduction), holiday ? normal : 0,
                Math.Min(baseline, Math.Max(legacyAway, hrAway)), Math.Max(0, available - reduction - Math.Max(0, baseline - Math.Max(legacyAway, hrAway))),
                value?.Category is "Unavailable" or "Reduced" || records.Any(l => l.Status == "Unverified"));
        }
        return result;
    }

    internal static async Task<Dictionary<Guid, WorkCalendar>> Calendars(HubDb db, Guid[] people, DateOnly through)
    {
        var users = await db.Users.AsNoTracking().Where(u => people.Contains(u.Id)).ToListAsync();
        var schedules = await db.PersonSchedules.AsNoTracking().Where(s => people.Contains(s.PersonId) && s.FromDate <= through).OrderByDescending(s => s.FromDate).ToListAsync();
        var holidays = await db.Holidays.AsNoTracking().ToListAsync();
        return users.ToDictionary(u => u.Id, u => new WorkCalendar(holidays.Where(h => h.OfficeId == null || h.OfficeId == u.OfficeId).Select(h => h.Date),
            date => (schedules.FirstOrDefault(s => s.PersonId == u.Id && s.FromDate <= date)?.WorkingDays ?? [1, 2, 3, 4, 5]).Contains((int)date.DayOfWeek)));
    }

    internal static async Task LockPeople(HubDb db, IEnumerable<Guid> people)
    {
        foreach (var id in people.Distinct().Order())
            await db.Database.SqlQueryRaw<Guid>("SELECT id AS \"Value\" FROM hub.app_user WHERE id = {0} FOR UPDATE", id).ToListAsync();
    }

    internal static async Task Touch(HubDb db, Guid person, DateOnly from, DateOnly through)
    {
        var versions = await db.PersonDateVersions.Where(v => v.PersonId == person && v.WorkDate >= from && v.WorkDate <= through).ToDictionaryAsync(v => v.WorkDate);
        foreach (var local in db.PersonDateVersions.Local.Where(v => v.PersonId == person && v.WorkDate >= from && v.WorkDate <= through)) versions[local.WorkDate] = local;
        for (var day = from; day <= through; day = day.AddDays(1))
            if (versions.TryGetValue(day, out var version)) db.Entry(version).Property(v => v.WorkDate).IsModified = true;
            else db.PersonDateVersions.Add(new() { PersonId = person, WorkDate = day, RowVersion = 1 });
    }
}
