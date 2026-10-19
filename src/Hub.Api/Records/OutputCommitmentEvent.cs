using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class OutputCommitmentEventRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.OutputCommitmentEvent), "OutputCommitmentEvent", ["CommitmentId", "FromState", "ToState", "Reason", "EvidenceUrl", "ActorId", "MissCategoryId"], false, "no item-history surface today; child and organisation history retain existing queries"); }
