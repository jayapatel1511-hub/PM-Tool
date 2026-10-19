using Hub.Api.Data;
namespace Hub.Api.Records;
public static class TaskPickupOfferRecord { public static RegistryEntry Entry { get; } = new(typeof(Hub.Api.Data.TaskPickupOffer), "TaskPickupOffer", ["TaskId", "OfferedById", "AssigneeId", "Reason", "HoursRemaining", "NeededBy", "Note", "OpenedAt", "ClosedAt", "CloseCause", "KeepWatching"], false, "no item-history surface; existing child and organisation queries retain their scope"); }
