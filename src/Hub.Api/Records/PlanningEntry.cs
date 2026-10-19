using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class PlanningEntryRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.PlanningEntry), "PlanningEntry", ["PersonId", "HoursPerWeek", "StartWeek", "EndWeek", "Label", "SourceCategory", "ProjectId", "ProjectDisciplineId", "Confidence", "Visibility", "Notes", "LastValidatedAt", "DeletedAt"], false, "personal record; no item-history surface today"); }
