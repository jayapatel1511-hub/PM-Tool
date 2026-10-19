using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class CalendarEventRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.CalendarEvent), "CalendarEvent", ["ProjectId", "Type", "Title", "StartAt", "EndAt", "OwnerId", "Location", "Description", "Visibility", "CancelledAt"], false, "personal record; no item-history surface today"); }
