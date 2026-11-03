using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;

namespace Hub.Api.Features;

/// Optional presentation evidence on response DTOs. Assigned once after the authorized snapshot read;
/// these fields are never entities, canonical changes, or private acknowledgement writes.
public interface IWorkScreenResponse
{
    WorkSeenDecoration? Seen { get; set; }
    WorkActivityFact? WorkActivity { get; set; }
}

public static class WorkScreenResponseMetadata
{
    // Caller has authorized this exact canonical object and holds its RepeatableRead snapshot.
    public static async Task<T> AttachDetailAsync<T>(T row, object canonical, Access access, HubDb db,
        OrgSettings settings, TimeProvider clock) where T : IWorkScreenResponse
    {
        if (!settings.IsFlagOn("work_screens")) return row;
        var bound = WorkAssignmentPublication.Bound(db, canonical);
        var now = clock.GetUtcNow();
        var facts = await WorkScreenReads.ReadAsync(db, access, settings,
            new(access.Actor.Id, DateOnly.FromDateTime(Clock.Local(now, settings).DateTime), now), [bound]);
        return Attach(row, bound.Key.ItemType, bound.Key.ItemId, facts);
    }

    public static T Attach<T>(T row, string itemType, Guid itemId, WorkScreenReads.WorkScreenReadEvidence facts) where T : IWorkScreenResponse
    {
        var key = new WorkSeenKey(itemType, itemId);
        row.Seen = facts.Seen.GetValueOrDefault(key);
        row.WorkActivity = facts.Activity.GetValueOrDefault(key);
        return row;
    }
}

public static partial class TaskQueries
{
    public partial record Row : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class TaskEndpoints
{
    public sealed partial record TaskDetailResponse : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class DeliverableEndpoints
{
    public sealed partial record DeliverableRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record DeliverableDetail : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class DecisionEndpoints
{
    public sealed partial record DecisionDetailResponse : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class MyWorkEndpoints
{
    public sealed partial record MyWorkDecision : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record MyWorkProject : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record MyWorkMilestone : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class MyWorkFollowUpEndpoints
{
    public sealed partial record FollowUpItem : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class MeetingEndpoints
{
    public sealed partial record MeetingActionRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record ActionDetail : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class ProjectEndpoints
{
    public sealed partial record ProjectDetail : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class HandoffEndpoints
{
    public sealed partial record HandoffDetail : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class ChangeEndpoints
{
    public sealed partial record ChangeDetail : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class RegisterEndpoints
{
    public sealed partial record RiskDetailData : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record IssueDetailData : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class EvaluationEndpoints
{
    public sealed partial record AttentionRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}

public static partial class DisciplineCoordinationEndpoints
{
    public sealed partial record CoordinationHandoffRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationBlockerGroup : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationChangeRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationReviewRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationIssueRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationInputUseRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record CoordinationLinkedAction : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record SubmissionContextRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record StaffingContextRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
    public sealed partial record StartabilityRow : IWorkScreenResponse
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkSeenDecoration? Seen { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public WorkActivityFact? WorkActivity { get; set; }
    }
}
