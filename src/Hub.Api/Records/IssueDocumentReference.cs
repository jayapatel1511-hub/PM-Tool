using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Hub.Api.Records;
public static class IssueDocumentReferenceRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.IssueDocumentReference), "IssueDocumentReference", ["IssueId", "Kind", "Identifier", "Revision", "SourceUrl", "SourceSystem", "StableSourceId", "ExternalTopicId", "ModelElementGuid", "ViewpointUrl", "IsAvailable", "ReplacedById"], false, "no item-history surface today; child and organisation history retain existing queries"); }
