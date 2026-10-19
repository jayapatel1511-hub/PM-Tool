using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReadinessExceptionRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReadinessException), "ReadinessException", ["AssessmentId", "BasisVersionId", "ApprovedBy", "VerifierId", "LimitedWork", "Risk", "ExpiresOn"], false, "no item-history surface today; child and organisation history retain existing queries"); }
