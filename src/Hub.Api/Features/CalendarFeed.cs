using System.Security.Cryptography;
using System.Text;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public static class CalendarFeedEndpoints
{
    public sealed record VersionBody(int RowVersion);
    sealed record Source(string Key, DateOnly From, DateOnly Through, string Summary);

    public static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me/calendar-subscription", async (CurrentUser me, HubDb db) => {
            var row = await db.CalendarSubscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == me.Id);
            return new { Enabled = row?.TokenHash != null, RowVersion = row?.RowVersion ?? 0 };
        });
        api.MapPost("/me/calendar-subscription", Rotate);
        api.MapDelete("/me/calendar-subscription", Revoke);
        api.MapGet("/calendar-feed.ics", Feed).AllowAnonymous();
    }

    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    static async Task<IResult> Rotate(VersionBody body, CurrentUser me, HubDb db, HttpContext http)
    {
        return await Tx.Run(db, async () => {
            await PersonCapacity.LockPeople(db, [me.Id]);
            var row = await db.CalendarSubscriptions.SingleOrDefaultAsync(s => s.UserId == me.Id);
            AvailabilityEndpoints.Version(row?.RowVersion ?? 0, body.RowVersion);
            if (row == null) { row = new() { UserId = me.Id }; db.CalendarSubscriptions.Add(row); }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant(); row.TokenHash = Hash(token);
            db.LogEvent("CalendarSubscription", row.Id, "Rotated", "access"); await db.SaveChangesAsync();
            http.Response.Headers.CacheControl = "no-store";
            // Only this response contains the token. It is never saved in an idempotency receipt or audit record.
            return Results.Ok(new { Url = $"{http.Request.Scheme}://{http.Request.Host}/api/v1/calendar-feed.ics?token={token}", row.RowVersion });
        });
    }

    static async Task<IResult> Revoke(int rowVersion, CurrentUser me, HubDb db)
    {
        return await Tx.Run(db, async () => {
            await PersonCapacity.LockPeople(db, [me.Id]);
            var row = await db.CalendarSubscriptions.SingleOrDefaultAsync(s => s.UserId == me.Id) ?? throw ApiException.NotFound();
            AvailabilityEndpoints.Version(row.RowVersion, rowVersion); row.TokenHash = null;
            db.LogEvent("CalendarSubscription", row.Id, "Revoked", "access"); await db.SaveChangesAsync(); return Results.NoContent();
        });
    }

    static async Task<IResult> Feed(string? token, HubDb db, TimeProvider clock, HttpContext http)
    {
        http.Response.Headers.CacheControl = "private, no-store"; http.Response.Headers["Referrer-Policy"] = "no-referrer";
        if (token is not { Length: 64 } || !token.All(Uri.IsHexDigit)) return Results.Unauthorized();
        var hash = Hash(token);
        return await Tx.Run(db, async () => {
            var subscription = await db.CalendarSubscriptions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == hash);
            if (subscription == null) return Results.Unauthorized();
            await PersonCapacity.LockPeople(db, [subscription.UserId]);
            if (!await db.CalendarSubscriptions.AnyAsync(s => s.Id == subscription.Id && s.TokenHash == hash)) return Results.Unauthorized();
            var user = await db.Users.AsNoTracking().Include(u => u.Roles).SingleOrDefaultAsync(u => u.Id == subscription.UserId && u.IsActive);
            if (user == null) return Results.Unauthorized();
            var me = new CurrentUser { Id = user.Id, IsActive = true, Roles = user.Roles.Select(r => r.Role).ToHashSet(), SupervisorId = user.SupervisorId, Resolved = true };
            var access = new Access(db, me); var pids = access.VisibleProjectIds(); var uid = user.Id;
            var sources = new List<Source>();
            var tasks = await db.Tasks.AsNoTracking().Where(t => pids.Contains(t.ProjectId) && t.DueDate != null && t.DeletedAt == null
                && (t.AssigneeId == uid || t.ReviewerId == uid) && t.Status != TaskStatuses.Cancelled && t.Status != TaskStatuses.Complete).ToListAsync();
            sources.AddRange(tasks.Select(t => new Source("task-" + t.Id, t.DueDate!.Value, t.DueDate.Value, t.Key + " · " + t.Name)));
            var deliverables = await db.Deliverables.AsNoTracking().Where(d => pids.Contains(d.ProjectId) && d.DueDate != null && d.DeletedAt == null
                && (d.OwnerId == uid || d.ReviewerId == uid) && d.Status != DeliverableStatus.Cancelled && d.Status != DeliverableStatus.Accepted && d.Status != DeliverableStatus.Issued).ToListAsync();
            sources.AddRange(deliverables.Select(d => new Source("deliverable-" + d.Id, d.DueDate!.Value, d.DueDate.Value, d.Key + " · " + d.Name)));
            var mine = db.Projects.Where(p => p.ProjectManagerId == uid || db.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == uid && m.RemovedAt == null)).Select(p => p.Id);
            var milestones = await db.Milestones.AsNoTracking().Where(m => pids.Contains(m.ProjectId) && mine.Contains(m.ProjectId) && m.Date != null && m.DeletedAt == null && !m.IsCancelled && !m.IsComplete).ToListAsync();
            sources.AddRange(milestones.Select(m => new Source("milestone-" + m.Id, m.Date!.Value, m.Date.Value, m.Key + " · " + m.Name)));
            var reviews = await db.DisciplineReviews.AsNoTracking().Where(r => r.ReviewerId == uid && pids.Contains(r.ProjectId) && r.Status != DisciplineReviewStatus.Approved
                && db.ReviewPackages.Any(p => p.CurrentRoundId == r.RoundId && p.Status != ReviewStatus.Cancelled && p.Status != ReviewStatus.Superseded && p.Status != ReviewStatus.Draft))
                .Select(r => new { r.Id, r.DueDate, Package = db.ReviewPackages.Where(p => p.CurrentRoundId == r.RoundId).Select(p => p.Title).First() }).ToListAsync();
            sources.AddRange(reviews.Select(r => new Source("review-" + r.Id, r.DueDate, r.DueDate, Text.Get("calendar.discipline_review", r.Package))));
            if (!access.Actor.ReadOnly) {
                var leave = await db.ApprovedLeaves.AsNoTracking().Where(l => l.PersonId == uid && l.Status == "Approved").ToListAsync();
                sources.AddRange(leave.Select(l => new Source("leave-" + l.Id, l.FromDate, l.ThroughDate, l.HoursPerDay == null ? Text.Get("calendar.approved_leave") : Text.Get("calendar.partial_leave", l.HoursPerDay))));
            }
            var state = await db.CalendarFeedItems.Where(s => s.UserId == uid).ToListAsync(); var events = new List<CalendarFeedEvent>();
            foreach (var source in sources) {
                var fingerprint = Hash($"{source.From:yyyy-MM-dd}|{source.Through:yyyy-MM-dd}|{source.Summary}");
                var item = state.SingleOrDefault(s => s.SourceKey == source.Key);
                if (item == null) { item = new() { UserId = uid, SourceKey = source.Key }; db.CalendarFeedItems.Add(item); state.Add(item); }
                if (!item.Active || item.Fingerprint != fingerprint) { item.Sequence++; item.Fingerprint = fingerprint; item.FromDate = source.From; item.ThroughDate = source.Through; item.Active = true; item.UpdatedAt = clock.GetUtcNow(); }
                events.Add(new($"{item.Id}@pm.engcalchub.com", source.From, source.Through, source.Summary, item.Sequence, item.UpdatedAt, false,
                    $"{http.Request.Scheme}://{http.Request.Host}/my-work?myWeek={Workload.WeekOf(source.From):yyyy-MM-dd}"));
            }
            var keys = sources.Select(s => s.Key).ToHashSet();
            foreach (var item in state.Where(s => !keys.Contains(s.SourceKey))) {
                if (item.Active) { item.Sequence++; item.Active = false; item.Fingerprint = ""; item.UpdatedAt = clock.GetUtcNow(); }
                events.Add(new($"{item.Id}@pm.engcalchub.com", item.FromDate, item.ThroughDate, "", item.Sequence, item.UpdatedAt, true));
            }
            db.Audit.AsSystem("CalendarFeed"); await db.SaveChangesAsync();
            return Results.Text(ICalendar.Write(events), "text/calendar; charset=utf-8", Encoding.UTF8);
        });
    }
}
