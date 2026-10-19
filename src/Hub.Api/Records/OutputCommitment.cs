using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class OutputCommitmentRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.OutputCommitment), "OutputCommitment", ["SnapshotId", "TargetType", "TargetId", "PerformerId", "IntendedOutput", "CompletionCriteria", "TargetDate", "WeekStart", "ReadinessAtCommit", "State", "CompletionEvidenceUrl"], false, "no item-history surface today; child and organisation history retain existing queries"); }
