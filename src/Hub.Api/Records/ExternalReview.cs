using Hub.Api.Data;

namespace Hub.Api.Records;

public static class ExternalReviewRecord
{
    public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.ExternalReview), "ExternalReview",
        ["PackageId", "RoundId", "DeliverableIssueId", "ExternalPartyId", "ReceivedDate", "ReturnCodeId", "ReturnCodeSnapshot",
            "ReturnLabelSnapshot", "PartyNameSnapshot", "PartyOrganisationSnapshot"], false,
        "incoming review provenance is read through its package; project and organisation history retain existing queries");
}
