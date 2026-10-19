using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueVerificationRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.IssueVerification), "IssueVerification", ["IssueId", "VerifierId", "Status", "EvidenceUrl", "Note", "VerifiedAt"], false, "no item-history surface today; child and organisation history retain existing queries"); }
