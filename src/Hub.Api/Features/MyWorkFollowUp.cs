using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Hub.Api.Features;

public static class MyWorkFollowUpEndpoints
{
    sealed record Row(Guid Id, string ItemType, string Key, string Title, Guid ProjectId, string Status, DateOnly? DueDate, Guid? OwnerId, DateTimeOffset UpdatedAt);
    public sealed record FollowUpItem(Guid Id, string ItemType, string Key, string Title, Guid ProjectId, string ProjectNumber, string Status, DateOnly? DueDate, Guid? OwnerId, string? OwnerName);
    public sealed record FollowUpResponse(int Total, IEnumerable<FollowUpItem> Items);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/work/watching", (Access access, HubDb db, SettingsStore store, TimeProvider clock) => Read(false, access, db, store, clock));
        api.MapGet("/me/work/requested", (Access access, HubDb db, SettingsStore store, TimeProvider clock) => Read(true, access, db, store, clock));
    }

    static bool Open(Row row) => row.ItemType switch {
        ItemType.Task => row.Status is not (TaskStatuses.Complete or TaskStatuses.Cancelled),
        ItemType.Deliverable => row.Status is not (DeliverableStatus.Accepted or DeliverableStatus.Issued or DeliverableStatus.Cancelled),
        ItemType.Milestone => row.Status == "Open",
        ItemType.Decision => row.Status is DecisionStatus.Pending or DecisionStatus.UnderReview or DecisionStatus.Deferred,
        ItemType.Risk => RiskStatus.IsOpen(row.Status), ItemType.Issue => IssueStatus.IsOpen(row.Status), ItemType.Action => ActionStatus.IsOpen(row.Status),
        "Handoff" => row.Status is not (HandoffStatus.Incorporated or HandoffStatus.Cancelled),
        "ChangeNotice" => row.Status is not (ChangeStatus.Closed or ChangeStatus.Cancelled),
        "OpenRole" => row.Status == OpenRoleStatus.Open, _ => false };

    static async Task<Ok<FollowUpResponse>> Read(bool requested, Access access, HubDb db, SettingsStore store, TimeProvider clock)
    {
        if (!(await store.Get(db)).IsFlagOn("my_work_follow_up")) throw ApiException.NotFound();
        var me = access.Actor.Id;
        var projects = access.VisibleProjects().Select(p => p.Id);
        var watches = db.Watchers.Where(w => w.UserId == me && (w.Source == "Manual" || w.Source == "Mention") && projects.Contains(w.ProjectId));
        var rows = new List<Row>();
        if (!requested) {
            rows.AddRange(await db.Tasks.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Task && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Task, t.Key, t.Name, t.ProjectId, t.Status, t.DueDate, t.AssigneeId, t.UpdatedAt)).ToListAsync());
            rows.AddRange(await db.Deliverables.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Deliverable && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Deliverable, t.Key, t.Name, t.ProjectId, t.Status, t.DueDate, t.OwnerId, t.UpdatedAt)).ToListAsync());
            rows.AddRange(await db.Milestones.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Milestone && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Milestone, t.Key, t.Name, t.ProjectId, t.IsComplete || t.IsCancelled ? "Closed" : "Open", t.Date, null, t.UpdatedAt)).ToListAsync());
            rows.AddRange(await db.Risks.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Risk && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Risk, t.Key, t.Title, t.ProjectId, t.Status, t.ReviewDate, t.OwnerId, t.UpdatedAt)).ToListAsync());
            rows.AddRange(await db.Issues.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Issue && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Issue, t.Key, t.Title, t.ProjectId, t.Status, t.TargetResolutionDate, t.OwnerId, t.UpdatedAt)).ToListAsync());
            rows.AddRange(await db.Actions.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && watches.Any(w => w.ItemType == ItemType.Action && w.ItemId == t.Id))
                .Select(t => new Row(t.Id, ItemType.Action, t.Key, t.Text, t.ProjectId, t.Status, t.DueDate, t.OwnerUserId, t.UpdatedAt)).ToListAsync());
        }
        rows.AddRange(await db.Decisions.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && (requested ? t.RequestedById == me : watches.Any(w => w.ItemType == ItemType.Decision && w.ItemId == t.Id)))
            .Select(t => new Row(t.Id, ItemType.Decision, t.Key, t.Subject, t.ProjectId, t.Status, t.RequiredByDate, t.OwnerUserId ?? t.OwnerExternalPartyId, t.StatusChangedAt ?? t.CreatedAt)).ToListAsync());
        if (requested) {
            rows.AddRange(await db.Handoffs.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && t.CreatedBy == me)
                .Select(t => new Row(t.Id, "Handoff", t.Key, t.Title, t.ProjectId, t.Status, t.NeededBy, t.ReceivingOwnerId, t.CreatedAt)).ToListAsync());
            rows.AddRange(await db.ChangeNotices.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && t.CreatedBy == me)
                .Select(t => new Row(t.Id, "ChangeNotice", t.Key, t.Title, t.ProjectId, t.Status, t.AssessmentDueDate, t.OwnerId, t.CreatedAt)).ToListAsync());
            rows.AddRange(await db.OpenRoles.AsNoTracking().Where(t => projects.Contains(t.ProjectId) && t.CreatedBy == me)
                .Select(t => new Row(t.Id, "OpenRole", "", t.RoleLabel, t.ProjectId, t.Status, t.FromDate, t.CreatedBy, t.CreatedAt)).ToListAsync());
        }
        var closedIds = rows.Where(r => !Open(r)).Select(r => r.Id).ToArray();
        var closedAt = requested ? await db.ActivityLog.AsNoTracking().Where(a => a.ItemId != null && closedIds.Contains(a.ItemId.Value) && a.Categories.Contains("status"))
            .GroupBy(a => a.ItemId!.Value).Select(g => new { g.Key, At = g.Max(x => x.OccurredAt) }).ToDictionaryAsync(x => x.Key, x => x.At) : [];
        rows = rows.Where(r => Open(r) || requested && closedAt.GetValueOrDefault(r.Id, r.UpdatedAt) >= clock.GetUtcNow().AddDays(-14)).OrderBy(r => !Open(r)).ThenBy(r => r.DueDate).ThenBy(r => r.Key).ToList();
        var pids = rows.Select(r => r.ProjectId).Distinct().ToArray();
        var numbers = await access.VisibleProjects().Where(p => pids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.ProjectNumber);
        var ownerIds = rows.Select(r => r.OwnerId).OfType<Guid>().Distinct().ToArray();
        var owners = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName);
        var externalOwners = await db.ExternalParties.AsNoTracking().Where(p => pids.Contains(p.ProjectId) && ownerIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        foreach (var (id, name) in externalOwners) owners.TryAdd(id, name);
        return TypedResults.Ok(new FollowUpResponse(rows.Count, rows.Select(r => new FollowUpItem(r.Id, r.ItemType, r.Key, r.Title, r.ProjectId,
            numbers[r.ProjectId], r.Status, r.DueDate, r.OwnerId, r.OwnerId is { } id ? owners.GetValueOrDefault(id) : null))));
    }
}
