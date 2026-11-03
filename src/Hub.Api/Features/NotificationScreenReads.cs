using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record NotificationScreenContextCoverage(int Known, int Unknown, DateTimeOffset AsOf);
public sealed record NotificationScreenFollow(Guid ProjectId, string ProjectNumber, string? Level, string? Source);
public sealed record NotificationScreenDelivery(string State, string HistoryCoverage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? KnownOccurrences = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? TotalOccurrences = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? RealEmailedOccurrences = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? UncertainOccurrences = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? AcceptedAt = null);
public sealed record NotificationScreenState(bool? NeedsYourAction, string ContextState, NotificationScreenFollow? Follow, NotificationScreenDelivery Delivery);
public sealed record NotificationScreenRow(NotificationEndpoints.NotificationRow Notification, NotificationScreenState Screen);
public sealed record NotificationScreenPage(IReadOnlyList<NotificationScreenRow> Items, int Page, int PageSize, int TotalCount, NotificationScreenContextCoverage ContextCoverage);

/// Canonical current action/follow reads and self-only delivery facts within one read snapshot.
public static class NotificationScreenReads
{
    sealed record Target(Guid Id, Guid ProjectId);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/notifications/screen", async Task<Ok<NotificationScreenPage>> (HubDb db, Access access, CurrentUser me, SettingsStore store,
            TimeProvider clock, bool? unread, string? type, Guid? projectId, bool? needsAction, int? page, int? pageSize) =>
        {
            var settings = await store.Get(db); Http.RequireFlag(settings, "people_screens");
            await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
            var asOf = clock.GetUtcNow(); var (pg, size) = Http.Paging(page, pageSize ?? 50);
            var query = (await PlanningEndpoints.VisibleNotifications(db, access)).AsNoTracking();
            if (unread == true) query = query.Where(row => row.ReadAt == null);
            var types = Http.List(type); if (types.Length > 0) query = query.Where(row => types.Contains(row.EventType));
            if (projectId is { } pid) query = query.Where(row => row.ProjectId == pid);
            var candidates = await query.OrderByDescending(row => row.UpdatedAt).ThenByDescending(row => row.Id).ToListAsync();
            var projects = new Dictionary<Guid, Project>();
            foreach (var id in candidates.Where(row => row.ProjectId != null).Select(row => row.ProjectId!.Value).Distinct())
                projects[id] = (await access.Project(id, track: false)).Project;
            var projectIds = projects.Keys.ToArray();
            var targets = await ValidTargets(db, candidates, projectIds);
            var readers = projects.ToDictionary(pair => pair.Key, pair => new RecordContextReader(db, pair.Value, settings));
            foreach (var group in targets.GroupBy(target => target.Project))
                await readers[group.Key].PreloadForNotifications(group.Select(target => (target.Kind, target.Id)).ToArray());
            var contexts = new Dictionary<(Guid Project, string Kind, Guid Id), bool?>();
            foreach (var target in targets)
                contexts[target] = Action(await readers[target.Project].ReadForNotification(target.Kind, target.Id), me.Id);
            bool? For(Notification row) => row.ProjectId is { } p && row.ItemType is { } kind && row.ItemId is { } item
                ? contexts.GetValueOrDefault((p, kind, item)) : null;
            var known = candidates.Count(row => For(row).HasValue);
            var filtered = needsAction == true ? candidates.Where(row => For(row) == true).ToArray() : candidates.ToArray();
            var selected = filtered.Skip((pg - 1) * size).Take(size).ToArray();
            var delivery = (await NotificationDelivery.ReadAggregatesAsync(db, me.Id,
                selected.Select(row => (row.Id, row.Count)).ToImmutableArray())).ToDictionary(row => row.NotificationId);
            var actorIds = selected.Where(row => row.ActorUserId != null).Select(row => row.ActorUserId!.Value).Distinct().ToArray();
            var actors = await db.Users.AsNoTracking().Where(user => actorIds.Contains(user.Id)).ToDictionaryAsync(user => user.Id, user => user.DisplayName);
            var follows = await db.Follows.AsNoTracking().Where(follow => follow.UserId == me.Id && projectIds.Contains(follow.ProjectId)).ToDictionaryAsync(follow => follow.ProjectId);
            var rows = selected.Select(row =>
            {
                var p = row.ProjectId is { } id ? projects.GetValueOrDefault(id) : null;
                var follow = p is null ? null : follows.GetValueOrDefault(p.Id);
                var descriptor = p is not null && (follow is null || FollowLevel.All.Contains(follow.Level))
                    ? new NotificationScreenFollow(p.Id, p.ProjectNumber, follow?.Level, follow?.Source) : null;
                var action = For(row);
                var receipt = delivery.GetValueOrDefault(row.Id);
                return new NotificationScreenRow(new(row.Id, row.EventType, row.ProjectId, p?.ProjectNumber, row.ItemType, row.ItemId, row.ItemKey,
                    row.Title, row.Body, row.LinkPath, row.ActorUserId, row.ActorUserId is { } actor ? actors.GetValueOrDefault(actor) : null,
                    row.Count, row.CreatedAt, row.UpdatedAt, row.ReadAt),
                    new(action, action.HasValue ? "Known" : "Unknown", descriptor, receipt is null ? new("Unknown", "Unavailable")
                        : new(receipt.State, receipt.HistoryCoverage, receipt.KnownCapturedCount, receipt.CurrentCount,
                            receipt.RealAcceptedCount, receipt.UncertainCount, receipt.AcceptedAt)));
            }).ToArray();
            return TypedResults.Ok(new NotificationScreenPage(rows, pg, size, filtered.Length, new(known, candidates.Count - known, asOf)));
        }).RequireFlag("people_screens");
    }

    static bool? Action(RecordContextView? context, Guid viewer)
    {
        if (context is null || context.Available == false) return null;
        if (context.Terminal) return false;
        if (context.NextAction == RecordContextAction.NotAssessed || context.UnavailableInputCount > 0) return null;
        if (context.NextActors.Any(actor => actor.Kind == "User" && actor.State == "Active" && actor.Id == viewer)) return true;
        return context.ActorState is "Active" or "External" or "None" ? false : null;
    }

    // Identity/visibility batching, not a second action evaluator. Preserve canonical soft-delete query filters.
    static async Task<HashSet<(Guid Project, string Kind, Guid Id)>> ValidTargets(HubDb db, IReadOnlyCollection<Notification> candidates, Guid[] projects)
    {
        var result = new HashSet<(Guid Project, string Kind, Guid Id)>();
        foreach (var group in candidates.Where(row => row.ProjectId != null && row.ItemType != null && row.ItemId != null).GroupBy(row => row.ItemType!))
        {
            var ids = group.Select(row => row.ItemId!.Value).Distinct().ToArray();
            IQueryable<Target>? source = group.Key switch
            {
                RecordContextKind.Task => db.Tasks.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Deliverable => db.Deliverables.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Milestone => db.Milestones.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Decision => db.Decisions.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Risk => db.Risks.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Issue => db.Issues.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Action => db.Actions.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.ReviewPackage => db.ReviewPackages.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.ReviewFinding => db.ReviewFindings.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.ChangeNotice => db.ChangeNotices.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.ChangeAssessment => db.ChangeAssessments.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.SubmissionPackage => db.SubmissionPackages.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.SubmissionCheck => db.SubmissionChecks.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.Handoff => db.Handoffs.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                RecordContextKind.WorkConstraint => db.WorkConstraints.AsNoTracking().Where(row => ids.Contains(row.Id) && projects.Contains(row.ProjectId)).Select(row => new Target(row.Id, row.ProjectId)),
                _ => null,
            };
            if (source is null) continue;
            var found = (await source.ToListAsync()).Select(target => (target.Id, target.ProjectId)).ToHashSet();
            foreach (var row in group)
                if (found.Contains((row.ItemId!.Value, row.ProjectId!.Value))) result.Add((row.ProjectId.Value, group.Key, row.ItemId.Value));
        }
        return result;
    }
}
