using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class CheckEvidenceRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.CheckEvidence), "CheckEvidence", ["CheckId", "EvidenceUrl", "Note"], false, "no item-history surface today; child and organisation history retain existing queries"); }
