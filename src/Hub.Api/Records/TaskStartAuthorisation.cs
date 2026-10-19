using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class TaskStartAuthorisationRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.TaskStartAuthorisation), "TaskStartAuthorisation", ["TaskId", "AuthorisedBy", "Reason", "ReadinessAtAuthorisation", "Unknown", "Blocked", "StartedBy", "StartedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
