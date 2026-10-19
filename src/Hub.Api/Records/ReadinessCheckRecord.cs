using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class ReadinessCheckRecordDefinition { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ReadinessCheckRecord), "ReadinessCheck", ["AssessmentId", "Code", "Applies", "Satisfied", "Reason", "EvidenceUrl", "RecordedBy"], false, "no item-history surface today; child and organisation history retain existing queries"); }
