using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class TaskDependencyRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.TaskDependency), "Dependency", ["PredecessorTaskId", "SuccessorTaskId", "DependencyType", "LagDays", "Note", "DeletedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
