using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReadinessAssessmentRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReadinessAssessment), "ReadinessAssessment", ["TargetType", "TargetId", "OwnerId", "IntendedOutput", "CompletionCriteria", "State"], false, "no item-history surface today; child and organisation history retain existing queries"); }
