using System.Data;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Hub.Api.Features;

public sealed record ReviewDisciplineSummary(Guid ProjectId, string Population, string TimingBasis, ReviewDisciplineSummaryRow[] Disciplines);
public sealed record ReviewDisciplineSummaryRow(Guid ProjectDisciplineId, Guid DisciplineId, string? Name, string State,
    int ApprovedPackageCount, int EligiblePackageCount, int NonApplicablePackageCount, int PackageSampleCount, int UnavailablePackageCount,
    decimal? MedianRoundsToApproval, int? MaximumRoundsToApproval, int StartedAssignmentRoundCount, int TimedAssignmentRoundCount,
    int UnavailableTimingAssignmentRoundCount, decimal? MedianDaysPerRound);

/// Actual approved-package discipline samples; never findings, people rankings or a parallel approval evaluator.
public static class ReviewDisciplineSummaryEndpoints
{
    public static void Map(RouteGroupBuilder api) => api.MapGet("/projects/{projectId:guid}/reviews/discipline-summary", Read).RequireFlag("register_screens");

    public static decimal? Median(IEnumerable<decimal> samples)
    {
        var values = samples.Order().ToArray();
        return values.Length == 0 ? null : values.Length % 2 == 1 ? values[values.Length / 2] : (values[values.Length / 2 - 1] + values[values.Length / 2]) / 2m;
    }

    static async Task<Ok<ReviewDisciplineSummary>> Read(Guid projectId, HttpContext http, Access access, HubDb db, SettingsStore store)
    {
        Http.RequireFlag(await store.Get(db), "register_screens");
        await using var snapshot = db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead) : null;
        await access.Project(projectId, false);
        Check.That(http.Request.Query.Count == 0, "query", "error.invalid");
        var packages = await db.ReviewPackages.AsNoTracking().Where(p => p.ProjectId == projectId && p.DeletedAt == null && p.Status == ReviewStatus.Approved).ToArrayAsync();
        var packageIds = packages.Select(p => p.Id).ToArray();
        var rounds = await db.ReviewRounds.AsNoTracking().Where(r => packageIds.Contains(r.PackageId)).ToArrayAsync();
        var roundIds = rounds.Select(r => r.Id).ToArray();
        var assignments = await db.DisciplineReviews.AsNoTracking().Where(a => roundIds.Contains(a.RoundId)).ToArrayAsync();
        var disciplines = await db.ProjectDisciplines.AsNoTracking().Where(d => d.ProjectId == projectId).ToArrayAsync();
        var referenceIds = disciplines.Select(d => d.DisciplineId).ToArray();
        var references = await db.Disciplines.AsNoTracking().Where(d => referenceIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id);
        var byRound = rounds.ToDictionary(r => r.Id); var byDiscipline = disciplines.ToDictionary(d => d.Id);
        if (rounds.Any(r => r.ProjectId != projectId) || assignments.Any(a => a.ProjectId != projectId || !byDiscipline.ContainsKey(a.ProjectDisciplineId))
            || rounds.GroupBy(r => (r.PackageId, r.Number)).Any(g => g.Count() != 1)
            || assignments.GroupBy(a => (a.RoundId, a.ProjectDisciplineId)).Any(g => g.Count() != 1)
            || disciplines.Any(d => !references.ContainsKey(d.DisciplineId)) || packages.Any(p => !byDiscipline.ContainsKey(p.ProjectDisciplineId))
            || packages.Any(p => p.CurrentRoundId is not { } id || !byRound.TryGetValue(id, out var r) || r.PackageId != p.Id || r.Number != p.RoundNumber))
            throw ApiException.Conflict("review_summary_integrity", "reviewDiscipline.integrity");
        var roundAssignments = assignments.ToLookup(a => a.RoundId); var packageRounds = rounds.ToLookup(r => r.PackageId);
        var rows = disciplines.OrderBy(d => d.SortOrder).ThenBy(d => references[d.DisciplineId].Name, StringComparer.Ordinal).ThenBy(d => d.Id).Select(d =>
        {
            var eligible = packages.Where(p => roundAssignments[p.CurrentRoundId!.Value].Any(a => a.ProjectDisciplineId == d.Id)).ToArray();
            var counts = new List<decimal>(); var durations = new List<decimal>(); var started = 0;
            foreach (var package in eligible)
            {
                var current = byRound[package.CurrentRoundId!.Value];
                var requiredRounds = packageRounds[package.Id].Where(r => r.Number <= current.Number && r.StartedAt.HasValue
                    && roundAssignments[r.Id].Any(a => a.ProjectDisciplineId == d.Id)).ToArray();
                if (current.StartedAt.HasValue) counts.Add(requiredRounds.Length);
                foreach (var round in requiredRounds)
                {
                    started++;
                    var assignment = roundAssignments[round.Id].Single(a => a.ProjectDisciplineId == d.Id);
                    if (assignment.Status is not (DisciplineReviewStatus.Approved or DisciplineReviewStatus.ChangesRequired)
                        || assignment.DecidedAt is not { } end || end < round.StartedAt!.Value) continue;
                    durations.Add((decimal)(end.UtcTicks - round.StartedAt!.Value.UtcTicks) / TimeSpan.TicksPerDay);
                }
            }
            var reference = references[d.DisciplineId];
            return new ReviewDisciplineSummaryRow(d.Id, d.DisciplineId, reference.Name, d.IsActive && reference.IsActive ? "Active" : "Inactive",
                packages.Length, eligible.Length, packages.Length - eligible.Length, counts.Count, eligible.Length - counts.Count,
                Median(counts), counts.Count == 0 ? null : (int)counts.Max(), started, durations.Count, started - durations.Count, Median(durations));
        }).ToArray();
        return TypedResults.Ok(new ReviewDisciplineSummary(projectId, "CurrentApprovedPackages", "LatestTerminalDisciplineDecision", rows));
    }
}
