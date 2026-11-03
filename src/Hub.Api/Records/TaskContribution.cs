using Hub.Api.Data;

namespace Hub.Api.Records;

public static class TaskContributionRecord
{
    public static RegistryEntry Entry { get; } = new(typeof(TaskContribution), "TaskContribution", ["MarkedAt", "Note"], false,
        "no standalone item-history surface; the parent Task history retains its current project and source privacy");
}
