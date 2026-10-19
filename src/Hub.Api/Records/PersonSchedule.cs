using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class PersonScheduleRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.PersonSchedule), "PersonSchedule", ["PersonId", "FromDate", "WeeklyHours", "WorkingDays", "WeekdayHours"], false, "personal record; no item-history surface today"); }
