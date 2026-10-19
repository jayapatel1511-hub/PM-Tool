using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class DeliverableDependencyRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.DeliverableDependency), "Dependency", ["PredecessorDeliverableId", "SuccessorDeliverableId", "DependencyType", "LagDays", "Note", "DeletedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
