using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class CalendarSubscriptionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.CalendarSubscription), "CalendarSubscription", ["UserId"], false, "personal record; no item-history surface today"); }
